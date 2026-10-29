using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// Molten Shard's two kinds of burning ground (.ai/spell-roster.md 2e): the line of slag a shard's
/// flight lays behind it (Slag Trail, its level 4) and the lava pool that wells up where a shard dives
/// into the ground (Caldera, its level 8).
/// </summary>
/// <remarks>
/// Code-only and drawn by itself, on the ground under every body (DrawLayers.Ground) - it is something
/// left behind, and a wizard walking across it walks over it. What is drawn is what burns: the slag's
/// line is tested against enemy positions at the width it is drawn plus a little of a body, and the
/// pool burns exactly the disc it has welled up to (as LingeringZone's pools do). No rim is drawn round
/// either (section 2c, no area markers): the crust is the edge of the molten metal, not a marker.
///
/// Pixel art (2026-10-29): both used to be translucent, antialiased circles, lines and polygons stacked
/// over one another. Each droplet of slag is now one quad drawn by scenes/shaders/molten_slag.gdshader and
/// the pool one quad drawn by scenes/shaders/caldera.gdshader, which decide every 2 px cell once in
/// opaque Fire-ramp and red-row tones with Bayer dithering the only gradient. A droplet's radius stays
/// inside SlagHalfWidth, the width its burn tests, and the pool is drawn to PoolRadiusNow, the disc it
/// burns, its bank wandering only inward of it.
///
/// Nothing allocates per frame. A trail is a ring of at most MaxPoints points, each with the time it
/// was laid; a point cools and goes out SlagSeconds after it was laid, so the line burns out from the
/// hand outward in the order the shard flew it, and the node frees itself once its last point is out.
/// </remarks>
public partial class MoltenSlag : Node2D
{
	private const int MaxPoints = 48;
	private const float PointSpacing = 12f;
	// How much of a body a trail reaches past its drawn width: the feet, not the whole box.
	private const float TrailBodyAllowance = 8f;

	private bool isPool;
	private int tickDamage;
	private SpellData spell;
	private Player player;
	private float life;
	private float tickSeconds;
	private float clock;
	private float tick;

	// The trail: a ring of points in world space, oldest at (head - count).
	private readonly Vector2[] points = new Vector2[MaxPoints];
	private readonly float[] born = new float[MaxPoints];
	private int head;
	private int count;
	private bool feeding = true;
	private float lastLaid;

	// The pool.
	private Vector2 centre;
	private float radius;
	private float poolSeed;

	// The drawing: one quad a droplet (or the pool), its offset from the droplet's centre in the UV and
	// the droplet's numbers in the vertex colour (molten_slag.gdshader). Filled in place every frame.
	private static Shader slagShader;
	private static Shader calderaShader;
	private static ShaderMaterial sharedSlag;
	private static readonly StringName ParamRadius = "radius";
	private static readonly StringName ParamClock = "clock";
	private static readonly StringName ParamCooling = "cooling";
	private static readonly StringName ParamFade = "fade";
	private static readonly StringName ParamSeed = "seed";
	private static readonly StringName ParamGilded = "gilded";
	private ShaderMaterial material;
	private readonly Vector2[] quad = new Vector2[4];
	private readonly Color[] quadColours = new Color[4];
	private readonly Vector2[] quadUv = new Vector2[4];

	/// <summary>A trail for a shard to feed as it flies; every tick it burns what stands on it for <paramref name="tickDamage"/>.</summary>
	public static MoltenSlag Trail(Node arena, int tickDamage, SpellData spell, Player player)
	{
		var slag = new MoltenSlag
		{
			Name = "SlagTrail",
			isPool = false,
			tickDamage = Math.Max(1, tickDamage),
			spell = spell,
			player = player,
			life = BoltEvolutions.SlagSeconds,
			tickSeconds = BoltEvolutions.SlagTickSeconds,
		};
		arena.AddChild(slag);
		return slag;
	}

	/// <summary>A pool of lava welling up at <paramref name="at"/> to <paramref name="fullRadius"/>.</summary>
	public static MoltenSlag Pool(Node arena, Vector2 at, float fullRadius, int tickDamage, SpellData spell, Player player)
	{
		var pool = new MoltenSlag
		{
			Name = "Caldera",
			isPool = true,
			centre = at,
			radius = MathF.Max(8f, fullRadius),
			tickDamage = Math.Max(1, tickDamage),
			spell = spell,
			player = player,
			life = BoltEvolutions.CalderaSeconds,
			tickSeconds = BoltEvolutions.CalderaTickSeconds,
		};
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		pool.poolSeed = rng.RandfRange(0f, 100f);
		arena.AddChild(pool);
		return pool;
	}

	public override void _Ready()
	{
		ZAsRelative = false;
		ZIndex = DrawLayers.Ground;
		if (isPool)
		{
			calderaShader ??= GD.Load<Shader>("res://scenes/shaders/caldera.gdshader");
			material = new ShaderMaterial { Shader = calderaShader };
			material.SetShaderParameter(ParamSeed, poolSeed);
			material.SetShaderParameter(ParamGilded, spell?.IsLegendary ?? false);
		}
		else
		{
			// Every trail shares one material: a droplet's numbers are in its vertex colour.
			slagShader ??= GD.Load<Shader>("res://scenes/shaders/molten_slag.gdshader");
			if (sharedSlag == null || !IsInstanceValid(sharedSlag))
				sharedSlag = new ShaderMaterial { Shader = slagShader };
			material = sharedSlag;
		}
		Material = material;
	}

	/// <summary>Lays a point of slag where the shard now is, if it has come far enough from the last.</summary>
	public void Feed(Vector2 at)
	{
		if (!feeding || isPool)
			return;
		if (count > 0)
		{
			int newest = (head - 1 + MaxPoints) % MaxPoints;
			if (points[newest].DistanceSquaredTo(at) < PointSpacing * PointSpacing)
				return;
		}
		points[head] = at;
		born[head] = clock;
		head = (head + 1) % MaxPoints;
		count = Math.Min(MaxPoints, count + 1);
		lastLaid = clock;
	}

	/// <summary>The shard is gone: the line lays no more, and burns out on its own clock.</summary>
	public void StopFeeding() => feeding = false;

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		QueueRedraw();

		bool spent = isPool ? clock >= life : (!feeding && clock - lastLaid >= life);
		if (spent)
		{
			QueueFree();
			return;
		}

		tick -= dt;
		if (tick > 0f)
			return;
		tick = tickSeconds;
		if (player == null || !IsInstanceValid(player))
			return;
		if (isPool)
			BurnPool();
		else
			BurnTrail();
	}

	private float PoolRadiusNow()
	{
		float t = Mathf.Clamp(clock / MathF.Max(0.01f, BoltEvolutions.CalderaWellSeconds), 0f, 1f);
		// Wells up fast and settles: an ease-out, so it reads as a surge rather than a growing circle.
		return radius * (1f - (1f - t) * (1f - t));
	}

	private void BurnPool()
	{
		float r = PoolRadiusNow();
		float rSquared = r * r;
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (IsInstanceValid(enemy) && !enemy.IsDying && centre.DistanceSquaredTo(enemy.GlobalPosition) <= rSquared)
				player.DealDamageToEnemy(enemy, tickDamage, source: spell);
		}
	}

	private void BurnTrail()
	{
		if (count < 2)
			return;
		float reach = BoltEvolutions.SlagHalfWidth + TrailBodyAllowance;
		// The live points' box, grown by the reach: most of the swarm is nowhere near a line.
		int oldest = (head - count + MaxPoints) % MaxPoints;
		Rect2 box = new Rect2(points[(head - 1 + MaxPoints) % MaxPoints], Vector2.Zero);
		int live = 0;
		for (int k = 0; k < count; k++)
		{
			int i = (oldest + k) % MaxPoints;
			if (clock - born[i] >= life)
				continue;
			box = box.Expand(points[i]);
			live++;
		}
		if (live == 0)
			return;
		box = box.Grow(reach);

		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (!IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			Vector2 p = enemy.GlobalPosition;
			if (!box.HasPoint(p))
				continue;
			for (int k = 0; k + 1 < count; k++)
			{
				int a = (oldest + k) % MaxPoints;
				int b = (oldest + k + 1) % MaxPoints;
				if (clock - born[b] >= life)
					continue;
				if (DistanceToSegment(p, points[a], points[b]) <= reach)
				{
					player.DealDamageToEnemy(enemy, tickDamage, source: spell);
					break;
				}
			}
		}
	}

	// Lump i's radius: its own size, fixed by where it lies in the run, never past the half-width it burns.
	private static float LumpRadius(int i) => BoltEvolutions.SlagHalfWidth * (0.7f + 0.3f * Hash(i * 7 + 3));

	// Where lump i lies: a little off the line it was laid on - never so far that it reaches past the
	// half-width the slag burns - on the 2 px art grid.
	private Vector2 LumpAt(int i)
	{
		float room = BoltEvolutions.SlagHalfWidth - LumpRadius(i);
		Vector2 at = points[i] + new Vector2(Hash(i * 13 + 1) - 0.5f, Hash(i * 5 + 11) - 0.5f) * 2f * room;
		return new Vector2(Mathf.Round(at.X / 2f) * 2f, Mathf.Round(at.Y / 2f) * 2f);
	}

	// A fixed pseudo-random 0-1 for an index: the same lump looks the same every frame.
	private static float Hash(int n)
	{
		uint h = (uint)n * 2654435761u;
		h ^= h >> 15;
		h *= 2246822519u;
		h ^= h >> 13;
		return (h & 0xFFFF) / 65535f;
	}

	private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
	{
		Vector2 ab = b - a;
		float lengthSquared = ab.LengthSquared();
		if (lengthSquared < 0.0001f)
			return p.DistanceTo(a);
		float t = Mathf.Clamp((p - a).Dot(ab) / lengthSquared, 0f, 1f);
		return p.DistanceTo(a + ab * t);
	}

	public override void _Draw()
	{
		// Everything is laid in world space and carried into the node's frame here; the node never moves.
		Transform2D toLocal = GlobalTransform.AffineInverse();
		if (isPool)
			DrawPool(toLocal);
		else
			DrawTrail(toLocal);
	}

	// Slag: a run of molten droplets, each its own size and a little off the line, so it reads as metal
	// spilled from the shard rather than a beam drawn on the floor; a smaller drip between each and the
	// next joins them up. Each cools from white-hot down the Fire ramp as its point ages
	// (molten_slag.gdshader), so the line goes out from the hand outward in the order it was laid.
	private void DrawTrail(Transform2D toLocal)
	{
		if (count < 2)
			return;
		int oldest = (head - count + MaxPoints) % MaxPoints;
		for (int k = 0; k < count; k++)
		{
			int i = (oldest + k) % MaxPoints;
			float age = clock - born[i];
			if (age >= life)
				continue;
			float heat = 1f - age / life;
			Vector2 at = LumpAt(i);
			Droplet(toLocal, at, LumpRadius(i), heat, Hash(i * 7 + 3));
			if (k + 1 >= count)
				continue;
			int j = (i + 1) % MaxPoints;
			float nextAge = clock - born[j];
			if (nextAge >= life)
				continue;
			// The drip between two lumps: on the line between them, half their size, as hot as the cooler.
			Vector2 between = (at + LumpAt(j)) * 0.5f;
			between = new Vector2(Mathf.Round(between.X / 2f) * 2f, Mathf.Round(between.Y / 2f) * 2f);
			Droplet(toLocal, between, BoltEvolutions.SlagHalfWidth * 0.55f, MathF.Min(heat, 1f - nextAge / life), Hash(i * 11 + 5));
		}
	}

	// One droplet's quad: round its centre, and tall enough above it for the ember it flicks up.
	private void Droplet(Transform2D toLocal, Vector2 at, float r, float heat, float id)
	{
		float side = r + WhipLimb.Grid;
		float up = r + WhipLimb.Grid * 6f;
		Color data = new Color(heat, id, r / 16f, 1f);
		quadColours[0] = quadColours[1] = quadColours[2] = quadColours[3] = data;
		quadUv[0] = new Vector2(-side, -up);
		quadUv[1] = new Vector2(side, -up);
		quadUv[2] = new Vector2(side, side);
		quadUv[3] = new Vector2(-side, side);
		for (int v = 0; v < 4; v++)
			quad[v] = toLocal * (at + quadUv[v]);
		DrawPrimitive(quad, quadColours, quadUv);
	}

	// Lava: the pool's numbers to the shader, and one quad round it. In its last second it skins over and
	// goes dark; in its last moment it dissolves from the bank in.
	private void DrawPool(Transform2D toLocal)
	{
		float r = PoolRadiusNow();
		if (r < 1f)
			return;
		float remaining = life - clock;
		material.SetShaderParameter(ParamRadius, r);
		material.SetShaderParameter(ParamClock, clock);
		material.SetShaderParameter(ParamCooling, remaining < 1f ? 1f - MathF.Max(0f, remaining) : 0f);
		material.SetShaderParameter(ParamFade, remaining < 0.4f ? MathF.Max(0f, remaining) / 0.4f : 1f);
		float side = r + WhipLimb.Grid * 2f;
		quadColours[0] = quadColours[1] = quadColours[2] = quadColours[3] = Colors.White;
		quadUv[0] = new Vector2(-side, -side);
		quadUv[1] = new Vector2(side, -side);
		quadUv[2] = new Vector2(side, side);
		quadUv[3] = new Vector2(-side, side);
		for (int v = 0; v < 4; v++)
			quad[v] = toLocal * (centre + quadUv[v]);
		DrawPrimitive(quad, quadColours, quadUv);
	}
}
