using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// Does standing still do what the cards say? Drives the real game scene, headless, through a fixed
// script of standing, walking and being shoved, and checks the Brazier's heat, Taproot's stance and
// the healing it pays against the numbers in Player and BoonCatalog.
//
// Run:
//   "$GODOT_BIN" --headless --path . scenes/_StandStillProbe.tscn
//
// Exit code 0 when every check passes, 1 otherwise; every step prints a STANDSTILL line.
//
// WHY THIS EXISTS. The Brazier's whole design is two rates - heat builds over 4 s still and cools
// over 3 s walking - and a one-word slip (a reset instead of a slope, a displacement test instead of
// an input test) produces a set that compiles, validates and plays wrong. None of that is visible to
// dotnet build or RegressionChecks, and by eye "it seems to fill up" is all anyone can say.
//
// The player holds all three Brazier pieces and the Taproot boon from the first frame; every enemy is
// cleared every frame, so nothing levels him up, hurts him or pauses the tree. The script runs on the
// physics clock, so a step of N seconds is exactly the frames the player spent in it.
//
// WHAT IT CANNOT TELL YOU: whether the embers read at a glance, or whether +30% is worth the risk of
// standing in a swarm. Those need eyes and hands (_UiShot --heat=X, and a real run).
public partial class _StandStillProbe : Node
{
	private enum Act { Still, Walk, StillShoved }

	private readonly struct Step
	{
		public readonly string Name;
		public readonly Act Act;
		public readonly float Seconds;
		// Expected at the end of the step. NaN means "not checked".
		public readonly float Heat;
		public readonly bool? Rooted;
		public readonly bool WoundAtStart;

		public Step(string name, Act act, float seconds, float heat, bool? rooted, bool woundAtStart = false)
		{
			Name = name;
			Act = act;
			Seconds = seconds;
			Heat = heat;
			Rooted = rooted;
			WoundAtStart = woundAtStart;
		}
	}

	// The script. Heat expectations follow from Player.BrazierHeatUpSeconds (4) and
	// BrazierCoolDownSeconds (3); rooted from BoonCatalog.StanceSeconds (1.5).
	private static readonly Step[] Script =
	{
		new("still 2.0 s from cold", Act.Still, 2.0f, 0.50f, true),
		// Three 40 px shoves with no input: being moved is not moving.
		new("still 1.0 s, shoved 3 times", Act.StillShoved, 1.0f, 0.75f, true),
		new("still 1.5 s more (full at 4 s)", Act.Still, 1.5f, 1.00f, true),
		// A sidestep costs a slice, not the stack - and breaks the stance outright.
		new("walk 0.3 s (a sidestep)", Act.Walk, 0.3f, 0.90f, false),
		new("still 0.6 s (re-stoked)", Act.Still, 0.6f, 1.00f, false),
		new("walk 1.5 s", Act.Walk, 1.5f, 0.50f, false),
		new("walk 2.0 s more (cold at 3 s)", Act.Walk, 2.0f, 0.00f, false),
		new("still 1.6 s (stance takes hold)", Act.Still, 1.6f, 0.40f, true),
		// Healing: wounded at the start of each, measured across it.
		new("still 3.0 s rooted, wounded", Act.Still, 3.0f, 1.00f, true, woundAtStart: true),
		new("walk 3.0 s, wounded", Act.Walk, 3.0f, 0.00f, false, woundAtStart: true),
	};

	private const float HeatTolerance = 0.03f;
	private const float GiveUpRealSeconds = 90f;

	private Node game;
	private Player player;
	private bool ready;
	private bool finished;
	private int stepIndex = -1;
	private float stepElapsed;
	private int shovesDone;
	private int hpAtStepStart;
	private float healedStill = float.NaN;
	private float healedWalking = float.NaN;
	private ulong startedMsec;
	private readonly List<string> failures = new();

	private static readonly string[] MoveActions = { "move_left", "move_right", "move_up", "move_down" };
	private static readonly string[] Brazier = { ChestItemCatalog.EmberFlask, ChestItemCatalog.InfernoCore, ChestItemCatalog.ObsidianHeart };

	public override void _Ready()
	{
		// A probe must never write the player's real save.
		SaveManager.WritesDisabled = true;
		// Always, so the watchdog runs whatever the tree does; the script itself only advances on
		// physics frames the player also ran (see _PhysicsProcess).
		ProcessMode = ProcessModeEnum.Always;
		Engine.TimeScale = 1f;
		Global.SelectedCharacterIdx = 1;
		Global.SelectedStageIdx = 0;
		startedMsec = Time.GetTicksMsec();

		game = ResourceLoader.Load<PackedScene>("res://scenes/node_2d_game.tscn").Instantiate();
		AddChild(game);
	}

	public override void _ExitTree()
	{
		foreach (string a in MoveActions)
			Input.ActionRelease(a);
	}

	public override void _Process(double delta)
	{
		if (!finished && (Time.GetTicksMsec() - startedMsec) / 1000f > GiveUpRealSeconds)
		{
			failures.Add($"gave up after {GiveUpRealSeconds} real seconds at step {stepIndex}");
			Finish();
		}
	}

	// The probe is the game's parent, so this runs before the player's own physics frame: input set
	// here is what that frame's MovePlayer reads.
	public override void _PhysicsProcess(double delta)
	{
		if (finished)
			return;

		if (player == null || !IsInstanceValid(player))
		{
			player = FindPlayer(game);
			return;
		}

		ClearEnemies();
		if (GetTree().Paused)
			return;

		if (!ready)
		{
			foreach (string relic in Brazier)
				player.AddChestItem(relic);
			if (!player.TryAddBoon("taproot"))
				failures.Add("could not grant the Taproot boon");
			if (!player.IsBrazierLit)
				failures.Add("the Brazier did not light with all three pieces held");
			GD.Print($"STANDSTILL setup: brazier lit={player.IsBrazierLit}, stance boon={player.HasStanceBoon},"
				+ $" chest damage without heat {(player.GetChestDamageBonusPercent() - player.BrazierDamageBonus) * 100f:0}%");
			ready = true;
			BeginStep(0);
			return;
		}

		stepElapsed += (float)delta;
		Step step = Script[stepIndex];
		if (step.Act == Act.StillShoved && shovesDone < 3 && stepElapsed >= (shovesDone + 1) * step.Seconds / 4f)
		{
			player.GlobalPosition += new Vector2(40f, 0f);
			shovesDone++;
		}

		if (stepElapsed + 0.0001f < step.Seconds)
			return;

		EndStep(step);
		if (stepIndex + 1 < Script.Length)
			BeginStep(stepIndex + 1);
		else
			Finish();
	}

	private void BeginStep(int index)
	{
		stepIndex = index;
		stepElapsed = 0f;
		shovesDone = 0;
		Step step = Script[index];
		if (step.WoundAtStart)
			player.CurrentHP = Math.Max(1, player.MaxHP - 60);
		hpAtStepStart = player.CurrentHP;
		bool walk = step.Act == Act.Walk;
		foreach (string a in MoveActions)
			Input.ActionRelease(a);
		if (walk)
			Input.ActionPress("move_right", 1f);
	}

	private void EndStep(Step step)
	{
		float heat = player.BrazierHeat;
		bool rooted = player.IsStanceRooted;
		int healed = player.CurrentHP - hpAtStepStart;
		GD.Print($"STANDSTILL {step.Name,-34} still={player.IsStandingStill,-5} stillFor={player.StillSeconds,5:0.00}s"
			+ $" heat={heat,4:0.00} spellDamage=+{player.BrazierDamageBonus * 100f,4:0.0}% spellArea=+{player.BrazierAreaBonus * 100f,4:0.0}%"
			+ $" rooted={rooted,-5}" + (step.WoundAtStart ? $" healed={healed}" : ""));

		if (!float.IsNaN(step.Heat) && MathF.Abs(heat - step.Heat) > HeatTolerance)
			failures.Add($"{step.Name}: heat {heat:0.000}, expected {step.Heat:0.00}");
		if (step.Rooted is bool want && rooted != want)
			failures.Add($"{step.Name}: rooted {rooted}, expected {want}");
		if (MathF.Abs(player.BrazierDamageBonus - Player.BrazierDamageAtFullHeat * heat) > 0.0001f
			|| MathF.Abs(player.BrazierAreaBonus - Player.BrazierAreaAtFullHeat * heat) > 0.0001f)
			failures.Add($"{step.Name}: the bonus does not scale with the heat");
		if (step.WoundAtStart)
		{
			if (step.Act == Act.Walk)
				healedWalking = healed;
			else
				healedStill = healed;
		}
	}

	private void Finish()
	{
		finished = true;
		foreach (string a in MoveActions)
			Input.ActionRelease(a);

		// Taproot's 1.5 HP a second over 3 s is 4.5 HP on top of whatever else regenerates, which is
		// the same in both steps; allow a point either side for the whole-HP accumulator.
		if (!float.IsNaN(healedStill) && !float.IsNaN(healedWalking))
		{
			float extra = healedStill - healedWalking;
			GD.Print($"STANDSTILL healing over 3 s: {healedStill} rooted, {healedWalking} walking - Taproot paid {extra} (expected about 4.5)");
			if (extra < 3.5f || extra > 5.5f)
				failures.Add($"Taproot paid {extra} HP over 3 s rooted, expected about 4.5");
		}

		foreach (string failure in failures)
			GD.Print("STANDSTILL FAIL " + failure);
		GD.Print(failures.Count == 0 ? "STANDSTILL PASS - every check held" : $"STANDSTILL {failures.Count} check(s) failed");
		GetTree().Quit(failures.Count == 0 ? 0 : 1);
	}

	private void ClearEnemies()
	{
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
			if (IsInstanceValid(node) && !node.IsQueuedForDeletion())
				node.QueueFree();
	}

	private static Player FindPlayer(Node root)
	{
		if (root is Player found)
			return found;
		foreach (Node child in root.GetChildren())
		{
			Player inner = FindPlayer(child);
			if (inner != null)
				return inner;
		}
		return null;
	}
}
