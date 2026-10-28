using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// Chapter 4, the Blighted Swamp: MOTHER ROT.
//
// She sheds. Every quarter of her health she throws off a pair of rot spawn, and for as long as
// any of them is still standing she is knitting herself back together out of them.
//
// THE DECISION THE FIGHT POSES. Damage on the boss is damage that is being undone; damage on the
// spawn is damage that is not going into the boss at all. Neither is right on its own, and the
// split changes as the fight goes: early, the spawn are few and the regen is survivable, so you
// push; late, there are six of them and the regen outruns anything but clearing them first. It is
// the same choice the Summoner poses at minute five, asked with a boss health bar behind it.
//
// This is also the only boss in the game whose health goes UP, which is worth saying out loud:
// the bar moving the wrong way is the mechanic announcing itself, and the regen ring drawn under
// her is there so it never reads as a bug.
public partial class MotherRotBoss : BossEnemy
{
	/// <summary>What she sheds. An ordinary enemy scene, so it dies and drops XP like anything else.</summary>
	[Export] public string SpawnScenePath { get; set; } = "res://scenes/SlowEnemy.tscn";

	[Export] public int SpawnPerSplit { get; set; } = 2;

	/// <summary>Health a spawn gets, as a fraction of hers. Scaled from her own so it tracks the run.</summary>
	[Export] public float SpawnHealthFraction { get; set; } = 0.06f;

	[Export] public float SpawnRingRadius { get; set; } = 74f;

	/// <summary>Health per second she pulls back while at least one spawn is alive.</summary>
	[Export] public float RegenPerSecond { get; set; } = 26f;

	/// <summary>Hard ceiling on live spawn, for the same reason the Summoner has one: frame budget.</summary>
	[Export] public int MaxLiveSpawn { get; set; } = 8;

	[Export] public Color RotColor { get; set; } = new Color(0.52f, 0.78f, 0.34f);

	private PackedScene spawnScene;
	private readonly List<Enemy> liveSpawn = new();
	// Regen is a float and health is an int, so it accumulates here and is spent a whole point at
	// a time. Rounding every frame would either lose all of it or double it, depending on the sign.
	private float regenCarry;

	public override void _Ready()
	{
		base._Ready();
		spawnScene = ResourceLoader.Load<PackedScene>(SpawnScenePath);
		if (spawnScene == null)
			GD.PushError($"MotherRotBoss: spawn scene not found at '{SpawnScenePath}', so she will never split.");
	}

	protected override GroundSlamAttack BuildSlam() =>
		new(SlamIntervalSeconds, SlamTelegraphSeconds, SlamRadius, SlamDamage, RotColor);

	// Four thresholds, evenly spaced, because the mechanic is a rhythm rather than a surprise. The
	// last one enrages on top of the split.
	protected override float[] BuildPhaseThresholds() => new[] { 0.8f, 0.6f, 0.4f, 0.2f };

	protected override void OnPhaseEntered(int phase)
	{
		Split();
		if (phase >= 4)
			ApplyEnrage();
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		PruneSpawn();
		TickRegen((float)delta);
	}

	private void PruneSpawn()
	{
		for (int i = liveSpawn.Count - 1; i >= 0; i--)
		{
			Enemy spawn = liveSpawn[i];
			// The group test, not just validity: a corpse mid death animation is still a live node
			// and should stop feeding her the moment it is killed, not when it finishes falling.
			if (spawn == null || !IsInstanceValid(spawn) || !spawn.IsInGroup("enemies"))
				liveSpawn.RemoveAt(i);
		}
	}

	private void TickRegen(float delta)
	{
		if (IsDying || liveSpawn.Count == 0 || Health >= MaxHealth)
		{
			regenCarry = 0f;
			return;
		}

		regenCarry += RegenPerSecond * delta;
		int whole = Mathf.FloorToInt(regenCarry);
		if (whole <= 0)
			return;

		regenCarry -= whole;
		Health = Mathf.Min(MaxHealth, Health + whole);
	}

	private void Split()
	{
		Node arena = GetParent();
		if (spawnScene == null || arena == null || !IsInstanceValid(arena))
			return;

		int wanted = Mathf.Max(1, SpawnPerSplit);
		for (int i = 0; i < wanted; i++)
		{
			if (liveSpawn.Count >= MaxLiveSpawn)
				return;

			if (spawnScene.Instantiate() is not Enemy spawn)
			{
				GD.PushError($"MotherRotBoss: '{SpawnScenePath}' has no Enemy script attached.");
				return;
			}

			// Scaled from her own health, which Node2DGame already scaled for the difficulty preset
			// and the minute of the run. A flat number would make late spawn free kills, and free
			// kills would make the regen a pure gift.
			spawn.Health = Mathf.Max(1, Mathf.RoundToInt(MaxHealth * SpawnHealthFraction));

			liveSpawn.Add(spawn);
			// Parented to the arena, not to her: killing the mother must not delete the spawn that
			// are already the player's problem, and she is meant to be killable last.
			arena.AddChild(spawn);

			float angle = Mathf.Tau * i / wanted + GetInstanceId() % 360 * Mathf.Pi / 180f;
			spawn.GlobalPosition = GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * SpawnRingRadius;
		}
	}

	public override void _Draw()
	{
		base._Draw();

		if (liveSpawn.Count == 0 || IsDying)
			return;

		// One thread per living spawn, drawn from her toward each. It is the only thing on screen
		// saying WHY the bar is going the wrong way, and without it the regen reads as a bug.
		var thread = new Color(RotColor.R, RotColor.G, RotColor.B, 0.55f);
		foreach (Enemy spawn in liveSpawn)
		{
			if (spawn == null || !IsInstanceValid(spawn))
				continue;
			Vector2 local = spawn.GlobalPosition - GlobalPosition;
			DrawLine(local.Normalized() * RegenRingRadius, local, thread, 2.0f, true);
		}

		DrawArc(Vector2.Zero, RegenRingRadius, 0f, Mathf.Tau, 28, thread, 2.5f, true);
	}

	private const float RegenRingRadius = 30f;

	// --- The fall (BossDeath) ---------------------------------------------------------------------
	//
	// THE STAIN. She is the swollen thing that sheds and knits herself back - a dome of hide carrying
	// its sacs on its back (sprite_bosses.py: the only boss wider than it is tall). So she goes the
	// way a swollen thing goes. The sacs burst one after another, smallest first, each throwing its
	// green; the dome sags and slumps out flat; and under her the rot she was made of spreads across
	// the ground as a dark stain, bubbling, wider than she ever was, before it soaks away. Cold and
	// dark rather than gory (world-and-tone.md: "absence and cold are on-direction") - a stain on the
	// swamp, not a wound.
	private BossDeath.Piece deathDome;

	// The sacs on her back as drawn (art x, y, radius), in the order they burst: smallest first.
	private static readonly Vector3[] Sacs =
	{
		new(23f, 44f, 4f), new(5f, 87f, 3f), new(88f, 86f, 3f), new(65f, 37f, 6f), new(32f, 34f, 6f), new(50f, 30f, 8f),
	};
	private const float FirstBurst = 0.05f;
	private const float BurstEvery = 0.11f;

	protected internal override float DeathSeconds => 2.8f;

	protected internal override void OnDeathBegin(BossDeath death)
	{
		deathDome = death.CutWhole();
	}

	protected internal override void TickDeath(BossDeath death, float t, float dt)
	{
		if (deathDome == null)
			return;

		float s = death.ArtScale.X;
		float quiver = t < 0.7f ? BossDeath.Shudder(t, 1f, 14f) * s * 0.5f : 0f;
		float slump = BossDeath.EaseInOut(BossDeath.Span(t, 0.55f, 1.7f));
		deathDome.Offset = new Vector2(quiver, 0f);
		deathDome.Scale = new Vector2(1f + 0.38f * slump, 1f - 0.86f * slump);
		float dim = BossDeath.Span(t, 0.6f, 1.6f);
		deathDome.Color = new Color(1f - 0.5f * dim, 1f - 0.4f * dim, 1f - 0.55f * dim, 1f - BossDeath.Span(t, 1.5f, 2.05f));

		for (int i = 0; i < Sacs.Length; i++)
		{
			if (!death.Crossed(FirstBurst + i * BurstEvery))
				continue;
			Vector3 sac = Sacs[i];
			Vector2 at = death.PieceToLocal(deathDome, new Vector2(sac.X, sac.Y));
			float lift = Mathf.Max(0f, death.Foot.Y - at.Y);
			death.EmitAt(new BossDeath.Throw
			{
				Cell = BossDeath.Cell.Gob, SpeedMin = 40f, SpeedMax = 150f, UpMin = 90f, UpMax = 220f,
				Gravity = 620f, Bounce = 0f, Friction = 9f, Life = DeathSeconds - t - 0.1f, Fade = 0.6f, DepthJitter = 30f,
			}, 3 + (int)sac.Z, new Vector2(at.X, death.Foot.Y + death.Rand(-16f, 16f)), lift, sac.Z * s * 0.6f);
			death.PuffAt(at, sac.Z * s * 0.5f, sac.Z * s * 1.6f, 0.22f, new Color(0.86f, 0.96f, 0.55f, 0.9f), 0f, 0f, true);
		}

		if (death.Crossed(0.7f))
		{
			// Her hide, as she gives way.
			death.Emit(new BossDeath.Throw
			{
				Cell = BossDeath.Cell.Hide, Tinted = true, SpeedMin = 30f, SpeedMax = 110f, UpMin = 50f, UpMax = 130f,
				Bounce = 0.1f, Friction = 8f, Spin = 4f, Life = 1.9f, Fade = 0.5f,
			}, 6, new Vector2(48f, 60f), new Vector2(34f, 16f));
		}
	}

	// The burst sacs: dark where each one was, carried down with her as she slumps.
	protected internal override void DrawDeathAir(BossDeath death, CanvasItem canvas, float t)
	{
		if (deathDome == null || t > 2.05f)
			return;
		float alpha = 1f - BossDeath.Span(t, 1.5f, 2.05f);
		for (int i = 0; i < Sacs.Length; i++)
		{
			if (t < FirstBurst + i * BurstEvery)
				continue;
			Vector3 sac = Sacs[i];
			Vector2 at = death.PieceToLocal(deathDome, new Vector2(sac.X, sac.Y));
			float rx = sac.Z * death.ArtScale.X * deathDome.Scale.X * 0.85f;
			float ry = sac.Z * death.ArtScale.Y * deathDome.Scale.Y * 0.85f;
			BossDeath.Disc(canvas, at, rx, Mathf.Max(BossDeath.Px, ry), new Color(0.05f, 0.06f, 0.04f, 0.9f * alpha));
			BossDeath.Ring(canvas, at, rx + BossDeath.Px, Mathf.Max(BossDeath.Px * 1.5f, ry + BossDeath.Px), BossDeath.Px, new Color(0.42f, 0.58f, 0.13f, 0.8f * alpha));
		}
	}

	// The stain: lobes of rot spreading under her, a lighter wet edge, and bubbles rising in it.
	protected internal override void DrawDeathGround(BossDeath death, CanvasItem canvas, float t)
	{
		if (deathDome == null || t < 0.12f)
			return;

		ElementColors.Ramp poison = ElementColors.GetRamp(Element.Poison);
		float spread = BossDeath.EaseOut(BossDeath.Span(t, 0.12f, 1.75f));
		float fade = 1f - BossDeath.Span(t, 2.05f, DeathSeconds);
		float r = 26f + 150f * spread;
		Vector2 centre = death.Foot + new Vector2(0f, -6f);
		var edge = new Color(poison.Edge, 0.42f * fade);
		var body = new Color(0.07f, 0.10f, 0.05f, 0.86f * fade);
		const int lobes = 9;
		for (int pass = 0; pass < 2; pass++)
		{
			for (int k = 0; k < lobes; k++)
			{
				float a = Mathf.Tau * k / lobes + BossDeath.Hash(31u, k) * 0.6f;
				float reach = r * (0.25f + 0.32f * BossDeath.Hash(37u, k));
				float lobe = r * (0.42f + 0.22f * BossDeath.Hash(41u, k));
				Vector2 at = centre + new Vector2(Mathf.Cos(a) * reach, Mathf.Sin(a) * reach * 0.45f);
				if (pass == 0)
					BossDeath.Disc(canvas, at, lobe + 6f, (lobe + 6f) * 0.45f, edge);
				else
					BossDeath.Disc(canvas, at, lobe, lobe * 0.45f, body);
			}
		}
		BossDeath.Disc(canvas, centre, r * 0.55f, r * 0.26f, body);

		// Bubbles, each on its own clock: a ring of the rot's green that swells and pops.
		for (int k = 0; k < 14; k++)
		{
			float born = 0.4f + 1.8f * BossDeath.Hash(53u, k);
			float u = BossDeath.Span(t, born, born + 0.32f);
			if (u <= 0f || u >= 1f)
				continue;
			float a = Mathf.Tau * BossDeath.Hash(59u, k);
			float d = r * 0.7f * Mathf.Sqrt(BossDeath.Hash(61u, k));
			Vector2 at = centre + new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d * 0.45f);
			float br = 3f + 7f * u;
			BossDeath.Ring(canvas, at, br, br * 0.7f, BossDeath.Px, new Color(poison.Mid, 0.75f * fade * (1f - u * 0.5f)));
		}
	}
}
