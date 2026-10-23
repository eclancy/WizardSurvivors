using System;
using System.Collections.Generic;
using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// Places a chapter's side event and its wayfinder out in the world at stage start, as a pure
/// function of the run's terrain seed (.ai/side-events.md, "Where it is").
/// </summary>
/// <remarks>
/// THE STAGE-SECRETS RULES, APPLIED TO EVENTS (.ai/stage-secrets.md section 4). Where a site is
/// depends on (terrain seed, stage, what the ground is) and nothing that happens in the run:
/// <list type="bullet">
/// <item>Every roll is drawn BEFORE its position is tested, and the fallback search is a fixed
/// pattern, so a rejected spot never shifts what comes after it.</item>
/// <item>A site has a footprint, not a point: its centre and a ring round it must all be open ground
/// (no water, no wall, no hazard, no fence, no landmark) - <see cref="Ground.IsClear"/>, which
/// Node2DGame answers from the streamer's chunk data. Asking about far ground generates that
/// chunk's DATA (not its paint), the same data the chunk will be painted from when the player gets
/// there, so a site can never be put on a lake that has not been drawn yet.</item>
/// <item>On the maze a candidate snaps to a corridor cell's centre: every corridor block is floor,
/// and the endless maze is connected (.ai/terrain.md section 7), so any cell can be walked to.</item>
/// </list>
/// Because the terrain is itself a pure function of the seed, so is every site: a chunk forgotten
/// and repainted when the player walks back is furnished round the same site, and a probe with
/// --terrain-seed=N gets the same map every time.
/// </remarks>
public sealed class ExplorePlacer
{
	/// <summary>What the placer needs to know about the ground. Supplied by Node2DGame.</summary>
	public sealed class Ground
	{
		/// <summary>True if a footprint of this radius round this point is open ground the player can reach.</summary>
		public Func<Vector2, float, bool> IsClear { get; init; }
		/// <summary>The run's start: distances and bearings are measured from here.</summary>
		public Vector2 Origin { get; init; }
		/// <summary>World pixels in one terrain chunk (TerrainStreamer.ChunkTiles x tile size): the unit the bands are in.</summary>
		public float ChunkPixels { get; init; } = 1536f;
		/// <summary>Maze stages snap every site to a corridor cell's centre, this far apart; 0 on an open stage.</summary>
		public float MazeCellPixels { get; init; }
	}

	private LevelGenerator.Rng rng;
	private readonly Ground ground;
	private readonly List<(Vector2 Centre, float Radius)> reserved = new();

	public ExplorePlacer(ulong seed, int stageIndex, int stream, Ground ground)
	{
		this.ground = ground;
		rng = new LevelGenerator.Rng(LevelGenerator.Hash(seed, stageIndex, stream, SaltExplore));
	}

	private const int SaltExplore = 0x5E7E;

	/// <summary>Where the event's first site was rolled, in radians from east; NaN until then.</summary>
	public float EventBearing { get; private set; } = float.NaN;

	/// <summary>The event band, in chunks from the start. Set by the director before PlaceSites.</summary>
	public float EventMinChunks { get; init; } = 4f;
	public float EventMaxChunks { get; init; } = 6f;

	/// <summary>Footprints placed so far: the decor passes keep out of them (Node2DGame.ReserveExploreGround).</summary>
	public IReadOnlyList<(Vector2 Centre, float Radius)> Reserved => reserved;

	public Vector2 Origin => ground.Origin;

	/// <summary>The next roll in this placer's stream, 0 to 1.</summary>
	public float NextFloat() => rng.NextFloat();

	/// <summary>
	/// The event's main site: a seeded bearing and a seeded distance in the event band, then the
	/// nearest open footprint to that spot. Its bearing is remembered, so the wayfinder can be put
	/// somewhere else.
	/// </summary>
	public Vector2 EventSite(float footprint)
	{
		float bearing = NextFloat() * Mathf.Tau;
		float distance = Mathf.Lerp(EventMinChunks, EventMaxChunks, NextFloat()) * ground.ChunkPixels;
		float searchFrom = NextFloat() * Mathf.Tau;
		if (float.IsNaN(EventBearing))
			EventBearing = bearing;
		return Search(ground.Origin + Direction(bearing) * distance, footprint, searchFrom);
	}

	/// <summary>A site on a given bearing, at a seeded distance between two bands (chunks from the start).</summary>
	public Vector2 AtBearing(float bearing, float minChunks, float maxChunks, float footprint)
	{
		float distance = Mathf.Lerp(minChunks, maxChunks, NextFloat()) * ground.ChunkPixels;
		float searchFrom = NextFloat() * Mathf.Tau;
		return Search(ground.Origin + Direction(bearing) * distance, footprint, searchFrom);
	}

	/// <summary>
	/// A site a seeded step from <paramref name="from"/>: <paramref name="minStep"/> to
	/// <paramref name="maxStep"/> away at a seeded bearing, at least <paramref name="spacing"/> from
	/// every point in <paramref name="avoid"/> and within <paramref name="reach"/> of
	/// <paramref name="anchor"/>. Twelve bearings are tried, all rolled up front; the bog lanterns'
	/// chain is laid out with it.
	/// </summary>
	public Vector2 Near(Vector2 from, float minStep, float maxStep, float footprint,
		IReadOnlyList<Vector2> avoid, float spacing, Vector2 anchor, float reach)
	{
		float bearing = NextFloat() * Mathf.Tau;
		float step = Mathf.Lerp(minStep, maxStep, NextFloat());
		float searchFrom = NextFloat() * Mathf.Tau;
		Vector2 fallback = Vector2.Inf;
		for (int k = 0; k < 12; k++)
		{
			Vector2 candidate = Snap(from + Direction(bearing + k * Mathf.Tau / 12f) * step);
			if (candidate.DistanceTo(anchor) > reach || !Spaced(candidate, avoid, spacing))
				continue;
			if (ground.IsClear(candidate, footprint))
				return Keep(candidate, footprint);
			if (fallback == Vector2.Inf)
				fallback = candidate;
		}
		// Nothing open on the ring at that step: search outward from the first spaced candidate (or
		// the plain step), keeping the spacing.
		Vector2 start = fallback != Vector2.Inf ? fallback : from + Direction(bearing) * step;
		return Search(start, footprint, searchFrom, avoid, spacing);
	}

	/// <summary>Keeps decor out of a circle that is not a site's footprint (a road, say).</summary>
	public void Reserve(Vector2 centre, float radius) => reserved.Add((centre, radius));

	/// <summary>True if a footprint round this point is open ground. On a maze, asked of the snapped cell.</summary>
	public bool IsClear(Vector2 point, float footprint) => ground.IsClear(Snap(point), footprint);

	/// <summary>The nearest corridor cell centre on a maze; the point itself on an open stage.</summary>
	public Vector2 Snap(Vector2 point)
	{
		float cell = ground.MazeCellPixels;
		if (cell <= 0f)
			return point;
		Vector2 rel = point - ground.Origin;
		return ground.Origin + new Vector2(Mathf.Round(rel.X / cell) * cell, Mathf.Round(rel.Y / cell) * cell);
	}

	// How far the fallback search goes. A candidate that lands in a lake or a landmark walks outward
	// in rings until it finds open ground; a stage with no open ground within this of a spot is not a
	// stage this game has.
	private const float SearchStep = 96f;
	private const float SearchReach = 1600f;

	private Vector2 Search(Vector2 candidate, float footprint, float startAngle, IReadOnlyList<Vector2> avoid = null, float spacing = 0f)
	{
		Vector2 first = Snap(candidate);
		if (Spaced(first, avoid, spacing) && ground.IsClear(first, footprint))
			return Keep(first, footprint);

		float step = ground.MazeCellPixels > 0f ? ground.MazeCellPixels : SearchStep;
		for (float r = step; r <= SearchReach + 0.1f; r += step)
		{
			int around = Mathf.Max(8, Mathf.Min(24, Mathf.RoundToInt(Mathf.Tau * r / step)));
			for (int k = 0; k < around; k++)
			{
				Vector2 p = Snap(candidate + Direction(startAngle + k * Mathf.Tau / around) * r);
				if (Spaced(p, avoid, spacing) && ground.IsClear(p, footprint))
					return Keep(p, footprint);
			}
		}
		// Measured never to happen on the shipping stages; if it ever does, the site still exists
		// rather than the event silently not being there, and --explore-log says so.
		GD.PushWarning($"ExplorePlacer: no open ground within {SearchReach} px of {candidate}; placing there anyway.");
		return Keep(first, footprint);
	}

	private Vector2 Keep(Vector2 site, float footprint)
	{
		reserved.Add((site, footprint));
		return site;
	}

	private static bool Spaced(Vector2 p, IReadOnlyList<Vector2> avoid, float spacing)
	{
		if (avoid == null || spacing <= 0f)
			return true;
		foreach (Vector2 other in avoid)
			if (other.DistanceTo(p) < spacing)
				return false;
		return true;
	}

	private static Vector2 Direction(float angle) => new(Mathf.Cos(angle), Mathf.Sin(angle));
}
