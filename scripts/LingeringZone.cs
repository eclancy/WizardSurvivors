using Godot;
using System;

namespace WizardSurvivors.scripts;

// A patch of ground that hurts things standing on it, for a while, and then stops.
//
// Deliberately generic and deliberately dumb: it does not move, aim, seek or expire early. It is
// dropped somewhere by something else, it ticks, it fades. Mirefoot drops a line of them behind the
// player; anything later that wants to leave something on the floor should use this rather than
// writing a third variant of the same twenty lines.
//
// It draws itself, like Cinderbreath and for the same reason - the shape that is drawn is computed
// from the same radius that is tested, so the tell cannot lie about its reach. What it draws is the
// pool, not a mark round it (2026-10-23, no area markers on the player's spells). The stroked rim it
// used to carry was the marker, and went. Mirefoot's pools no longer draw here at all
// (DrawsItself = false): a trail of them is one bog drawn by MireSurface, under the actors.
//
// PIXEL ART (2026-10-29). What still draws itself - Pyroclasm's magma pool and the Lingering Rift - was
// two translucent polygons and an antialiased bubble, fading in alpha. It is now one quad through
// scenes/shaders/lingering_pool.gdshader: every 2 px cell of the world shaded once and opaque, mostly a
// crust of occlusion with FillColor's tone molten between its plates, their edges glowing in EdgeColor's,
// and a bubble, its bank wandering only inward of Radius, dissolving from the edge in at the end. Only the
// two colours' RGB is read: the callers pass their element ramp's edge and hot.
public partial class LingeringZone : Node2D
{
	[Export] public float Radius { get; set; } = 46f;
	[Export] public float Duration { get; set; } = 4.0f;
	[Export] public float TickSeconds { get; set; } = 0.5f;

	[Export] public bool AppliesSlow { get; set; } = true;
	[Export] public float SlowMultiplier { get; set; } = 0.6f;
	// How long the slow outlasts the pool once a body leaves it (Mirefoot's SlowDuration level line).
	public float SlowLingerSeconds { get; set; } = 0f;

	[Export] public Color FillColor { get; set; } = new Color(0.20f, 0.38f, 0.30f, 0.46f);
	[Export] public Color EdgeColor { get; set; } = new Color(0.44f, 0.62f, 0.38f, 0.62f);

	// False when something else draws this zone. Mirefoot's pools are drawn together, as one bog under
	// the actors, by the trail's MireSurface (scripts/MireSurface.cs): a translucent disc a pool drew
	// over every other one doubled where they overlapped and sat over the player. The Magma Pool and
	// the Lingering Rift still draw themselves.
	[Export] public bool DrawsItself { get; set; } = true;

	public int DamagePerTick { get; set; } = 2;
	public SpellData SpellData { get; set; }
	public Player PlayerRef { get; set; }

	// Above zero, the first enemy found standing in the zone is held there - rooted (Enemy.ApplySlow at 0;
	// a boss's floor makes it a hard slow) - for this long. Once a zone, never again. Mirefoot's Sucking
	// Mud; its MireSurface draws the mud heaped round the held one's feet from Gripped and GrippedAt.
	public float GripSeconds { get; set; } = 0f;
	/// <summary>The enemy the zone has held, once it has (GripSeconds).</summary>
	public Enemy Gripped { get; private set; }
	/// <summary>When it was held, on <see cref="Age"/>; negative until then.</summary>
	public float GrippedAt { get; private set; } = -1f;

	/// <summary>Seconds since the zone was laid; it frees itself at <see cref="Duration"/>.</summary>
	public float Age => age;

	private float age = 0f;
	private float tickTimer = 0f;

	// The bank is randomised once, at birth (a seed), and then never recomputed. Two reasons: a pool
	// whose edge crawls every frame reads as a liquid simulation nobody asked for, and a trail of
	// eight identical circles reads as a stamp tool. Fixed-but-different is what puddles look like.
	private static Shader poolShader;
	private static readonly StringName ParamAge = "age";
	private ShaderMaterial poolMaterial;

	public override void _Ready()
	{
		// On the ground: over the terrain (-100) and the walkable decals (-20), under every actor (0),
		// where Scorching Ray's scorch and the meteor craters lie (CraterGround.GroundZ). It sat at z 1,
		// over the actors, so Pyroclasm's magma, Mirefoot's bog and the arcane rift were each painted
		// across the wizard walking through them (2026-10-28).
		ZIndex = CraterGround.GroundZ;
		ZAsRelative = false;
		if (!DrawsItself)
			return;

		var rng = new RandomNumberGenerator();
		rng.Randomize();
		poolShader ??= GD.Load<Shader>("res://scenes/shaders/lingering_pool.gdshader");
		poolMaterial = new ShaderMaterial { Shader = poolShader };
		poolMaterial.SetShaderParameter("radius", Radius);
		poolMaterial.SetShaderParameter("life", MathF.Max(0.01f, Duration));
		poolMaterial.SetShaderParameter("seed", rng.Randf() * 97f);
		poolMaterial.SetShaderParameter("body", new Color(FillColor, 1f));
		poolMaterial.SetShaderParameter("glow", new Color(EdgeColor, 1f));
		poolMaterial.SetShaderParameter(ParamAge, 0f);
		Material = poolMaterial;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		age += dt;

		tickTimer += dt;
		if (tickTimer >= TickSeconds)
		{
			tickTimer = 0f;
			ApplyToOccupants();
		}

		if (DrawsItself)
		{
			poolMaterial?.SetShaderParameter(ParamAge, age);
			QueueRedraw();
		}

		if (age >= Duration)
			QueueFree();
	}

	private void ApplyToOccupants()
	{
		if (PlayerRef == null)
			return;

		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Node2D enemy || !IsInstanceValid(node))
				continue;

			if (GlobalPosition.DistanceTo(enemy.GlobalPosition) > Radius)
				continue;

			if (DamagePerTick > 0 && node.HasMethod("TakeDamage"))
				PlayerRef.DealDamageToEnemy(node, DamagePerTick, source: SpellData);

			if (GripSeconds > 0f && Gripped == null && node is Enemy first && IsInstanceValid(first) && !first.IsDying)
			{
				Gripped = first;
				GrippedAt = age;
				first.ApplySlow(0f, GripSeconds);
			}

			// Re-applied every tick rather than once on entry, so the slow lasts exactly as long as
			// the enemy stays in the pool and wears off shortly after it leaves. Applying it once
			// with a long duration would let an enemy clip the edge and stay slowed across the map.
			if (AppliesSlow && node.HasMethod("ApplySlow"))
				node.Call("ApplySlow", SlowMultiplier, TickSeconds * 1.6f + SlowLingerSeconds);
		}
	}

	// One quad over the pool; lingering_pool.gdshader draws the pool inside it, out of Radius.
	public override void _Draw()
	{
		if (!DrawsItself)
			return;
		float r = Radius + 2f;
		DrawRect(new Rect2(-r, -r, r * 2f, r * 2f), Colors.White);
	}
}
