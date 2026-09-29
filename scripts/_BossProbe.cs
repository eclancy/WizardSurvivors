using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

// Drives one chapter boss, headless, from the real game scene, and prints what it actually did.
//
// Run:
//   "$GODOT_BIN" --headless --path . scenes/_BossProbe.tscn -- --stage=1
//   "$GODOT_BIN" --headless --path . scenes/_BossProbe.tscn -- --stage=all
//
// WHY THIS EXISTS. A boss is fifteen minutes into a run, so every one of these fights had been
// written, compiled, and validated without anybody watching it move. dotnet build sees none of it
// - not the scene, not the exported tuning, not the telegraph - and RegressionChecks only sees
// numbers, so a boss whose burrow never surfaces or whose phase change throws on the frame it
// fires passes both and fails in front of a player.
//
// It shortens the survival clock to a few seconds and then BLEEDS the boss on a fixed timer
// rather than waiting for the loadout to kill it. That is the whole trick: a scripted drain walks
// every boss through every phase threshold in a known time, so the probe reports on the fight
// rather than on whatever build the level-up RNG happened to hand the bot.
//
// WHAT IT CANNOT TELL YOU. Nothing here is a judgement about whether a fight is fun, fair or
// readable - it renders nothing and reads nothing. It answers four questions only: did the boss
// arrive, did it attack, did the burrowers actually submerge and come back, and did it die
// cleanly. Everything else still needs eyes.
public partial class _BossProbe : Node
{
	[Export] public int StageIndex { get; set; } = 1;
	[Export] public float TimeScale { get; set; } = 8f;
	[Export] public float BossTimerSeconds { get; set; } = 6f;
	[Export] public float DrainSeconds { get; set; } = 26f;
	[Export] public float GiveUpSeconds { get; set; } = 140f;
	[Export] public bool FocusOneBody { get; set; }

	private Node game;
	private Node2D player;
	private BossEnemy boss;
	private float elapsed;
	private float sinceDrain;
	private bool finished;

	// What the run is asked to prove, collected as it happens.
	private bool sawBoss;
	private bool sawUntargetable;
	private bool sawRetargetable;
	private int peakPhase;
	private int drainTicks;
	private float bossArrivedAt;
	private readonly List<string> notes = new();

	private static readonly string[] MoveActions = { "move_left", "move_right", "move_up", "move_down" };

	public override void _Ready()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--stage=")) StageIndex = int.Parse(arg.Substring(8));
			else if (arg.StartsWith("--timescale=")) TimeScale = float.Parse(arg.Substring(12));
			else if (arg.StartsWith("--drain=")) DrainSeconds = float.Parse(arg.Substring(8));
			else if (arg == "--focus") FocusOneBody = true;
		}

		// Immune to the pause, or the first level-up stops the probe dead.
		ProcessMode = ProcessModeEnum.Always;

		BossDefinition definition = BossCatalog.ForStageIndex(StageIndex);
		GD.Print($"BOSSPROBE stage={StageIndex} boss={definition?.Id ?? "NONE"} timescale={TimeScale}");
		if (definition == null)
		{
			GD.Print("BOSSPROBE no boss for that stage - nothing to drive.");
			GetTree().Quit();
			return;
		}

		Global.SelectedCharacterIdx = 1;
		Global.SelectedStageIdx = StageIndex;
		Engine.TimeScale = TimeScale;
		GameStats.ResetRunTelemetry();

		game = ResourceLoader.Load<PackedScene>("res://scenes/node_2d_game.tscn").Instantiate();
		// Set before the add so _Ready sees it: the boss phase is gated on this and fifteen real
		// minutes of survival is not a test anyone runs twice.
		game.Set("TimerVictorySeconds", BossTimerSeconds);
		AddChild(game);
	}

	public override void _ExitTree()
	{
		Engine.TimeScale = 1.0f;
		foreach (string a in MoveActions)
			Input.ActionRelease(a);
	}

	public override void _Process(double delta)
	{
		if (finished)
			return;

		elapsed += (float)delta;

		if (player == null || !IsInstanceValid(player))
		{
			player = FindNode<Player>(game);
			if (player == null)
				return;
		}

		AutoPickLevelUp();
		DriveMovement();
		TrackBoss((float)delta);

		if (elapsed >= GiveUpSeconds)
			Finish("ran out of clock");
	}

	// EVERY boss body on the field, not just the first one found. The Hollow Choir is three of
	// them and its whole mechanic is that they have to fall together, so a probe that drained one
	// would report a fight that never ends and tell you nothing about the one that exists.
	private readonly List<BossEnemy> bodies = new();

	private void TrackBoss(float delta)
	{
		bodies.Clear();
		CollectBosses(game, bodies);

		if (bodies.Count == 0)
		{
			// They were here and now they are not: that is the win, and the only clean way out.
			if (sawBoss)
				Finish("boss died");
			return;
		}

		if (!sawBoss)
		{
			sawBoss = true;
			bossArrivedAt = elapsed;
			boss = bodies[0];
			notes.Add($"{bodies.Count} bodies arrived at {elapsed:0.0}s with {bodies[0].Health} HP each");
		}

		foreach (BossEnemy body in bodies)
		{
			if (!body.IsTargetable)
				sawUntargetable = true;
			else if (sawUntargetable)
				sawRetargetable = true;

			peakPhase = Math.Max(peakPhase, body.BossPhase);
		}

		// The scripted drain. A fixed fraction of max health per tick, so every boss reaches every
		// threshold at a known time no matter what the bot is doing.
		sinceDrain += delta;
		if (sinceDrain < DrainInterval)
			return;

		sinceDrain = 0f;
		drainTicks++;
		// --focus drains only the first body. It exists for the Hollow Choir: draining all three at
		// one rate kills them on the same frame, so the revive - the entire mechanic - never fires
		// and the probe reports a fight it did not actually test.
		foreach (BossEnemy body in FocusOneBody ? bodies.Take(1) : bodies)
		{
			if (!IsInstanceValid(body))
				continue;
			int bite = Mathf.Max(1, Mathf.RoundToInt(body.MaxHealth * DrainInterval / Mathf.Max(0.5f, DrainSeconds)));
			body.TakeDamage(bite);
		}
	}

	private static void CollectBosses(Node root, List<BossEnemy> into)
	{
		if (root == null)
			return;
		if (root is BossEnemy hit && IsInstanceValid(hit) && !hit.IsDying)
			into.Add(hit);
		foreach (Node child in root.GetChildren())
			CollectBosses(child, into);
	}

	private const float DrainInterval = 0.25f;

	// Kite, exactly as the balance harness does. The bot is not the subject here - it exists so
	// the boss has something to chase and something to aim at.
	private void DriveMovement()
	{
		Vector2 away = Vector2.Zero;
		int counted = 0;

		foreach (Node n in GetTree().GetNodesInGroup("enemies"))
		{
			if (n is not Node2D e || !IsInstanceValid(n))
				continue;

			Vector2 offset = player.GlobalPosition - e.GlobalPosition;
			float d = offset.Length();
			if (d > 260f || d <= 0.01f)
				continue;

			away += offset / d * (1f - d / 260f);
			counted++;
		}

		Vector2 toCentre = -player.GlobalPosition;
		Vector2 desired = counted > 0
			? away.Normalized() + toCentre.Normalized() * 0.45f
			: toCentre.Normalized();

		if (desired.LengthSquared() > 0.0001f)
			desired = desired.Normalized();

		Press("move_right", "move_left", desired.X);
		Press("move_down", "move_up", desired.Y);
	}

	private static void Press(string positive, string negative, float axis)
	{
		if (axis > 0.02f)
		{
			Input.ActionRelease(negative);
			Input.ActionPress(positive, Math.Min(1f, axis));
		}
		else if (axis < -0.02f)
		{
			Input.ActionRelease(positive);
			Input.ActionPress(negative, Math.Min(1f, -axis));
		}
		else
		{
			Input.ActionRelease(positive);
			Input.ActionRelease(negative);
		}
	}

	private void AutoPickLevelUp()
	{
		LevelUpMenu menu = FindNode<LevelUpMenu>(game);
		if (menu == null || !menu.Visible)
			return;

		IReadOnlyList<LevelUpOption> options = menu.GetOfferedOptions();
		if (options == null || options.Count == 0)
			return;

		LevelUpOption pick = options.FirstOrDefault(o => o.IsNewUnlock && !o.RequiresSlotSwap)
			?? options.OrderBy(o => o.NextLevel).First();
		menu.EmitSignal(LevelUpMenu.SignalName.WeaponSelected, pick.SpellId);
	}

	private void Finish(string why)
	{
		finished = true;
		foreach (string a in MoveActions)
			Input.ActionRelease(a);

		BossDefinition definition = BossCatalog.ForStageIndex(StageIndex);
		GD.Print("");
		GD.Print($"BOSSPROBE RESULT  stage {StageIndex}  {definition?.DisplayName ?? "?"}");
		GD.Print($"  outcome        : {why} at {elapsed:0.0}s");
		GD.Print($"  boss arrived   : {(sawBoss ? $"yes, at {bossArrivedAt:0.0}s" : "NO - the fight never started")}");
		GD.Print($"  bodies left    : {bodies.Count}");
		GD.Print($"  phases entered : {peakPhase}");
		GD.Print($"  drain ticks    : {drainTicks}");
		GD.Print($"  went untargetable / came back : {sawUntargetable} / {sawRetargetable}");
		foreach (string note in notes)
			GD.Print($"  note           : {note}");

		// The one hard failure. Everything else is reported and judged by a person; a boss that
		// never arrives is a stage that cannot be completed, and that is worth an exit code.
		if (!sawBoss)
			GD.PushError($"BOSSPROBE stage {StageIndex}: the boss never spawned.");

		player = null;
		boss = null;
		game = null;
		GetTree().Quit();
	}

	private static T FindNode<T>(Node root) where T : Node
	{
		if (root == null)
			return null;
		if (root is T hit)
			return hit;
		foreach (Node child in root.GetChildren())
		{
			T found = FindNode<T>(child);
			if (found != null)
				return found;
		}
		return null;
	}
}
