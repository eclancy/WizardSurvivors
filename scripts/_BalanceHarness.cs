using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

// A headless run of the real game, played by a bot, reported as numbers.
//
// The problem it solves: every balance question in this project - is Frost Shard doing all the
// work, does Arcane Explosion ever come due, is Cinderbreath's duty cycle too slow - needs a full
// run to answer, and reading a run by eye gives you an impression rather than a figure.
// GameStats has always counted total damage; it now counts damage per spell, and this drives the
// game hard enough to fill that table.
//
// Run:
//   "$GODOT_BIN" --headless --path . scenes/_BalanceHarness.tscn -- --minutes=10 --character=1
//
// Flags: --minutes, --timescale, --character, --seed, --out
//
// THREE THINGS IT IS NOT, and reading the output without knowing them will mislead you:
//
//   1. **The bot is not a player.** It kites the nearest threat and nothing else - no baiting, no
//      corner abuse, no deliberate positioning for a cone. Treat its damage-taken number as an
//      upper bound and its uptime on short-range spells as a lower one.
//   2. **Time is scaled.** Godot ticks physics against real time, so a fifteen-minute run takes
//      fifteen real minutes unless Engine.TimeScale is raised. Raising it makes each physics step
//      cover more ground, so contacts and separation get coarser. Comparisons between runs at the
//      SAME timescale are sound; absolute numbers drift as it climbs.
//   3. **The level-up policy is fixed** (fill empty slots, then level the lowest). That is a
//      deliberately dumb build, which is the point: it is the same dumb build every run, so two
//      runs differ by the change under test rather than by what the bot felt like picking.
public partial class _BalanceHarness : Node
{
	[Export] public float Minutes { get; set; } = 8f;
	[Export] public float TimeScale { get; set; } = 6f;
	[Export] public int CharacterIndex { get; set; } = 1;
	[Export] public int Seed { get; set; } = 1234;
	[Export] public string OutPath { get; set; } = "user://balance-report.txt";

	private Node game;
	private Player player;
	private float elapsed;
	private bool finished;
	private int levelUpsHandled;
	private float lastProgressLog;

	private static readonly string[] MoveActions = { "ui_left", "ui_right", "ui_up", "ui_down" };

	public override void _Ready()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--minutes=")) Minutes = float.Parse(arg.Substring(10));
			else if (arg.StartsWith("--timescale=")) TimeScale = float.Parse(arg.Substring(12));
			else if (arg.StartsWith("--character=")) CharacterIndex = int.Parse(arg.Substring(12));
			else if (arg.StartsWith("--seed=")) Seed = int.Parse(arg.Substring(7));
			else if (arg.StartsWith("--out=")) OutPath = arg.Substring(6);
		}

		// Immune to the pause. A level-up pauses the tree, and a driver that pauses with it can
		// never make the choice that would unpause it.
		ProcessMode = ProcessModeEnum.Always;

		GD.Print($"HARNESS minutes={Minutes} timescale={TimeScale} character={CharacterIndex} seed={Seed}");

		GD.Seed((ulong)Seed);
		Global.SelectedCharacterIdx = CharacterIndex;
		Engine.TimeScale = TimeScale;

		GameStats.ResetRunTelemetry();

		game = ResourceLoader.Load<PackedScene>("res://scenes/node_2d_game.tscn").Instantiate();
		AddChild(game);
	}

	public override void _ExitTree()
	{
		// Left raised, a scene change would carry it into whatever loads next.
		Engine.TimeScale = 1.0f;
		foreach (string a in MoveActions)
			Input.ActionRelease(a);
	}

	public override void _Process(double delta)
	{
		if (finished)
			return;

		// delta is already scaled by Engine.TimeScale, so this is game time, which is what the
		// run length should be measured in.
		elapsed += (float)delta;

		if (player == null || !IsInstanceValid(player))
		{
			player = FindNode<Player>(game);
			if (player == null)
				return;
		}

		if (elapsed - lastProgressLog >= 30f)
		{
			lastProgressLog = elapsed;
			GD.Print($"HARNESS t={elapsed:0}s level={player.CurrentLevel} enemies={GetTree().GetNodesInGroup("enemies").Count}");
		}

		AutoPickLevelUp();
		DriveMovement();

		if (elapsed >= Minutes * 60f || player.IsDead)
			Finish(player.IsDead ? "died" : "reached the clock");
	}

	// --- the bot ---------------------------------------------------------------------

	// Kite: move directly away from the weighted centre of everything close, and lean toward the
	// arena middle so it cannot reverse into a wall and be pinned there.
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
			if (d > KiteAwareness || d <= 0.01f)
				continue;

			// Nearer enemies push harder, so the bot flees the thing about to touch it rather
			// than the average of a crowd it is already clear of.
			away += offset / d * (1f - d / KiteAwareness);
			counted++;
		}

		Vector2 toCentre = -player.GlobalPosition;
		Vector2 desired = counted > 0
			? away.Normalized() + toCentre.Normalized() * CentreBias
			: toCentre.Normalized();

		if (desired.LengthSquared() > 0.0001f)
			desired = desired.Normalized();

		Press("ui_right", "ui_left", desired.X);
		Press("ui_down", "ui_up", desired.Y);
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

	// A level-up pauses the tree and waits for a choice, so without this the run stops dead at
	// the first one. The policy is fixed on purpose - see the note at the top.
	private void AutoPickLevelUp()
	{
		LevelUpMenu menu = FindNode<LevelUpMenu>(game);
		if (menu == null || !menu.Visible)
			return;

		IReadOnlyList<LevelUpOption> options = menu.GetOfferedOptions();
		if (options == null || options.Count == 0)
			return;

		// Fill the loadout first, then deepen the shallowest thing in it. A bot that always took
		// the first card would report on card ORDER rather than on the spells.
		LevelUpOption pick = options.FirstOrDefault(o => o.IsNewUnlock && !o.RequiresSlotSwap)
			?? options.OrderBy(o => o.NextLevel).First();

		levelUpsHandled++;
		menu.EmitSignal(LevelUpMenu.SignalName.WeaponSelected, pick.SpellId);
	}

	// --- the report ------------------------------------------------------------------

	private void Finish(string why)
	{
		finished = true;
		foreach (string a in MoveActions)
			Input.ActionRelease(a);

		var lines = new List<string>
		{
			"Wizard Survivors - balance run",
			$"  ended        : {why} at {elapsed / 60f:0.00} minutes of game time",
			$"  character    : {CharacterRoster.GetByIndex(CharacterIndex)?.Name ?? "?"} (index {CharacterIndex})",
			$"  timescale    : {TimeScale}x   seed: {Seed}",
			$"  player level : {player?.CurrentLevel ?? 0}   level-ups taken: {levelUpsHandled}",
			"",
		};

		if (player != null && IsInstanceValid(player))
		{
			lines.Add("  loadout      : " + string.Join(", ",
				player.GetEquippedSpells().Where(s => s != null).Select(s => $"{s.Id} L{s.CurrentLevel}")));
			var boons = player.GetOwnedBoonIds();
			lines.Add("  boons        : " + (boons.Count > 0 ? string.Join(", ", boons) : "none"));
			lines.Add("");
		}

		var table = GameStats.DamageBySource.OrderByDescending(p => p.Value).ToList();
		int total = table.Sum(p => p.Value);
		lines.Add($"  damage by source (attributed total {total})");
		if (total == 0)
		{
			lines.Add("    nothing attributed - did the run produce any damage at all?");
		}
		else
		{
			foreach (var pair in table)
			{
				float share = 100f * pair.Value / total;
				// A bar makes an outlier obvious at a glance, which a column of numbers does not.
				string bar = new string('#', Math.Max(0, (int)Math.Round(share / 2f)));
				lines.Add($"    {pair.Key,-22} {pair.Value,8}  {share,5:0.0}%  {bar}");
			}
		}

		lines.Add("");
		lines.Add($"  damage per minute (attributed): {total / Math.Max(0.01f, elapsed / 60f):0}");

		string text = string.Join("\n", lines);
		GD.Print("");
		GD.Print(text);

		using var f = FileAccess.Open(OutPath, FileAccess.ModeFlags.Write);
		f?.StoreString(text + "\n");
		GD.Print($"\nHARNESS wrote {ProjectSettings.GlobalizePath(OutPath)}");

		player = null;
		game = null;
		GetTree().Quit();
	}

	private const float KiteAwareness = 260f;
	private const float CentreBias = 0.45f;

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
