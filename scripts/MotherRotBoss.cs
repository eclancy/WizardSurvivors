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
}
