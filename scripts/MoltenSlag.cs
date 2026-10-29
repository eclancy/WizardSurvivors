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
	private const int PoolSides = 16;

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
	private readonly float[] wobble = new float[PoolSides];
	private readonly Vector2[] polygon = new Vector2[PoolSides];
	private readonly Vector2[] bubbles = new Vector2[3];
	private readonly Vector2[] plate = new Vector2[6];

	private static readonly Color Crust = new Color(0.20f, 0.08f, 0.05f, 0.92f);

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
		for (int i = 0; i < PoolSides; i++)
			pool.wobble[i] = rng.RandfRange(0.92f, 1.04f);
		for (int i = 0; i < pool.bubbles.Length; i++)
			pool.bubbles[i] = new Vector2(rng.RandfRange(-0.45f, 0.45f), rng.RandfRange(-0.4f, 0.4f));
		arena.AddChild(pool);
		return pool;
	}

	public override void _Ready()
	{
		ZAsRelative = false;
		ZIndex = DrawLayers.Ground;
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

	// Where lump i lies: a little off the line it was laid on, on the 2 px art grid.
	private Vector2 LumpAt(int i)
	{
		Vector2 at = points[i] + new Vector2(Hash(i * 13 + 1) - 0.5f, Hash(i * 5 + 11) - 0.5f) * BoltEvolutions.SlagHalfWidth * 0.8f;
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
		// Everything here is laid in world space; the node itself never moves.
		DrawSetTransformMatrix(GlobalTransform.AffineInverse());
		ElementColors.Ramp fire = ElementColors.GetRamp(Element.Fire);
		if (isPool)
			DrawPool(fire);
		else
			DrawTrail(fire);
	}

	// Slag: a run of molten droplets, each its own size and a little off the line, so it reads as metal
	// spilled from the shard rather than a beam drawn on the floor. Each lump is a dark crust with molten
	// metal in it that cools from yellow-white through the fire ramp to a dull red and goes out,
	// flickering as it does; a thinner seam of the same joins one to the next.
	private void DrawTrail(in ElementColors.Ramp fire)
	{
		if (count < 2)
			return;
		int oldest = (head - count + MaxPoints) % MaxPoints;
		float halfWidth = BoltEvolutions.SlagHalfWidth;
		for (int pass = 0; pass < 3; pass++)
		{
			for (int k = 0; k < count; k++)
			{
				int i = (oldest + k) % MaxPoints;
				float age = clock - born[i];
				if (age >= life)
					continue;
				float heat = 1f - age / life;
				float fade = heat < 0.25f ? heat / 0.25f : 1f;
				float flicker = 0.78f + 0.22f * MathF.Sin(clock * 15f + k * 1.7f);
				// A lump's size and its step off the line, fixed by where it lies in the run.
				float lump = 0.65f + 0.45f * Hash(i * 7 + 3);
				Vector2 at = LumpAt(i);
				Vector2 next = k + 1 < count ? LumpAt((i + 1) % MaxPoints) : at;
				Color c;
				float r;
				if (pass == 0)
				{
					c = Crust with { A = Crust.A * fade };
					r = halfWidth * lump;
				}
				else if (pass == 1)
				{
					c = fire.Edge.Lerp(fire.Mid, heat) with { A = fade * (0.55f + 0.45f * heat) };
					r = halfWidth * lump * (0.45f + 0.3f * heat);
				}
				else
				{
					if (heat < 0.35f)
						continue;
					c = fire.Hot.Lerp(fire.Core, heat * heat) with { A = (heat - 0.35f) / 0.65f * flicker };
					r = MathF.Max(1f, halfWidth * lump * 0.28f * heat);
				}
				DrawCircle(at, r, c);
				if (k + 1 < count && clock - born[(i + 1) % MaxPoints] < life)
					DrawLine(at, next, c, r * 1.8f);
			}
		}

		// Embers: a few bright grains along the hot part, on the art grid.
		for (int k = 0; k < count; k += 3)
		{
			int i = (oldest + k) % MaxPoints;
			float heat = 1f - (clock - born[i]) / life;
			if (heat < 0.4f)
				continue;
			float flick = 0.5f + 0.5f * MathF.Sin(clock * 21f + k * 2.3f);
			Vector2 at = points[i] + new Vector2(MathF.Sin(k * 5.1f) * halfWidth * 0.6f, -2f - 4f * (1f - heat));
			at = new Vector2(Mathf.Round(at.X / 2f) * 2f, Mathf.Round(at.Y / 2f) * 2f);
			DrawRect(new Rect2(at, new Vector2(2f, 2f)), fire.Core with { A = heat * flick });
		}
	}

	// Lava: a crust ring, the molten body in the fire ramp, a white-hot heart that throbs, and bubbles
	// that swell and burst. In its last second it skins over and goes dark.
	private void DrawPool(in ElementColors.Ramp fire)
	{
		float r = PoolRadiusNow();
		if (r < 1f)
			return;
		float remaining = life - clock;
		float cooling = remaining < 1f ? 1f - MathF.Max(0f, remaining) : 0f;
		float fade = remaining < 0.4f ? MathF.Max(0f, remaining) / 0.4f : 1f;

		Fill(r, Crust with { A = Crust.A * fade });
		Fill(r * 0.86f, fire.Edge.Lerp(Crust, cooling) with { A = 0.95f * fade });
		Fill(r * 0.62f, fire.Mid.Lerp(fire.Edge, cooling) with { A = 0.9f * fade });
		float throb = 0.85f + 0.15f * MathF.Sin(clock * 6f);
		Fill(r * 0.34f * throb, fire.Hot.Lerp(fire.Mid, cooling) with { A = (1f - cooling) * fade });

		// Plates of crust riding on the melt, turning slowly with it, and thickening as it cools.
		for (int p = 0; p < bubbles.Length; p++)
		{
			float turn = clock * 0.35f + p * 2.1f;
			Vector2 at = centre + new Vector2(MathF.Cos(turn), MathF.Sin(turn)) * r * (0.45f + 0.12f * p);
			float size = r * (0.12f + 0.05f * p) * (1f + cooling);
			for (int v = 0; v < plate.Length; v++)
			{
				float a = Mathf.Tau * v / plate.Length + turn * 0.5f;
				plate[v] = at + new Vector2(MathF.Cos(a), MathF.Sin(a) * 0.7f) * size * wobble[(v + p * 3) % PoolSides];
			}
			DrawColoredPolygon(plate, Crust.Lerp(fire.Edge, 0.25f) with { A = 0.9f * fade });
		}

		for (int i = 0; i < bubbles.Length; i++)
		{
			float b = (clock * (0.9f + 0.25f * i) + i * 0.37f) % 1f;
			if (b > 0.8f || cooling > 0.6f)
				continue;
			Vector2 at = centre + bubbles[i] * r;
			float size = 1.5f + 3.5f * (b / 0.8f);
			DrawArc(at, size, 0f, Mathf.Tau, 10, fire.Core with { A = (1f - b) * fade }, 1.5f);
		}
	}

	private void Fill(float r, Color color)
	{
		for (int i = 0; i < PoolSides; i++)
		{
			float angle = Mathf.Tau * i / PoolSides;
			polygon[i] = centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r * wobble[i];
		}
		DrawColoredPolygon(polygon, color);
	}
}
