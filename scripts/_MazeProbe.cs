using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

// Measures whether the horde can actually reach the player, headless. Built for the maze; it reads
// the same navigation on open stages, where the obstacles are lakes.
//
// Run:
//   "$GODOT_BIN" --headless --path . scenes/_MazeProbe.tscn -- --stage=1 --seconds=240 --bot=roam
//   ... --stage=3 --no-open-routing   (Swamp, enemies chasing in a straight line as they did before
//                                      open stages had routing: the A/B for it)
//
// WHY THIS EXISTS. The Cursed Dungeon was reported as "enemies trapped in other corridors most of
// the time". A screenshot shows that it happens but not how often, and a fix judged by eye from two
// photographs is a fix judged on whichever two seconds the camera caught. This reads the shared flow
// field (MazeNavigation) for every live enemy every few seconds and reports four numbers:
//
//   reach    - share of enemies standing on a cell the field can route from at all
//   detour   - median of walking distance / straight-line distance; 1.0 is a straight run, and a
//              perfect maze puts neighbours seen through one wall twenty tiles of corridor apart
//   stalled  - share of enemies more than 4 tiles out whose walking distance has not fallen by a
//              tile and a half in three seconds: pinned on a corner, or queued in a jam
//   close    - share within 8 walking tiles of the player
//
// The player is immortal and either stands still (--bot=still) or roams the corridors, turning
// whenever it walks into a wall (--bot=roam), which is the case that actually strands enemies.
// It renders nothing; whether the maze READS still needs a photograph (_UiShot).
public partial class _MazeProbe : Node
{
	[Export] public int StageIndex { get; set; } = 1;
	[Export] public float Seconds { get; set; } = 240f;
	[Export] public float TimeScale { get; set; } = 4f;
	[Export] public string Bot { get; set; } = "roam";
	[Export] public long Seed { get; set; } = 20261014;

	private const float SampleEvery = 5f;
	private const float StallWindow = 3f;
	private const float WarmupSeconds = 45f;

	private Node game;
	private Player player;
	private float elapsed;
	private float sinceSample;
	private bool finished;

	private readonly Dictionary<ulong, (float At, float Path)> lastPath = new();
	private readonly List<float> detours = new();
	private readonly List<float> recomputeMs = new();
	private int samples;
	private double sumReach, sumStalled, sumPinned, sumClose, sumDetour, sumAlive;

	// Roam state.
	private Vector2 heading = Vector2.Right;
	private Vector2 lastPos;
	private float sinceMoveCheck, sinceTurn;
	private readonly RandomNumberGenerator rng = new() { Seed = 4242 };

	private static readonly string[] MoveActions = { "move_left", "move_right", "move_up", "move_down" };
	private static readonly Vector2[] Cardinals = { Vector2.Right, Vector2.Left, Vector2.Down, Vector2.Up };

	public override void _Ready()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--stage=")) StageIndex = int.Parse(arg.Substring(8));
			else if (arg.StartsWith("--seconds=")) Seconds = float.Parse(arg.Substring(10));
			else if (arg.StartsWith("--timescale=")) TimeScale = float.Parse(arg.Substring(12));
			else if (arg.StartsWith("--bot=")) Bot = arg.Substring(6);
			else if (arg.StartsWith("--seed=")) Seed = long.Parse(arg.Substring(7));
			else if (arg == "--no-open-routing") MazeNavigation.OpenStageRouting = false;
		}

		ProcessMode = ProcessModeEnum.Always;
		Global.SelectedCharacterIdx = 1;
		Global.SelectedStageIdx = StageIndex;
		Engine.TimeScale = TimeScale;
		GameStats.ResetRunTelemetry();

		game = ResourceLoader.Load<PackedScene>("res://scenes/node_2d_game.tscn").Instantiate();
		// One maze for every run of the probe, so a before and an after are measured on the same
		// corridors. Set before the add so _Ready builds the terrain with it.
		game.Set("ProceduralSeed", Seed);
		AddChild(game);
		GD.Print($"MAZEPROBE stage={StageIndex} seed={Seed} bot={Bot} seconds={Seconds} timescale={TimeScale} open-routing={MazeNavigation.OpenStageRouting}");
	}

	public override void _ExitTree()
	{
		Engine.TimeScale = 1.0f;
		MazeNavigation.OpenStageRouting = true;
		foreach (string a in MoveActions)
			Input.ActionRelease(a);
	}

	public override void _Process(double delta)
	{
		if (finished)
			return;

		float d = (float)delta;
		elapsed += d;

		if (player == null || !IsInstanceValid(player))
		{
			player = FindNode<Player>(game);
			if (player == null)
				return;
			lastPos = player.GlobalPosition;
		}

		player.MaxHP = Math.Max(player.MaxHP, 100000);
		player.CurrentHP = player.MaxHP;
		AutoPickLevelUp();
		if (Bot == "roam")
			Roam(d);

		sinceSample += d;
		if (sinceSample >= SampleEvery)
		{
			sinceSample = 0f;
			Sample();
		}

		if (elapsed >= Seconds)
			Finish();
	}

	private void Sample()
	{
		MazeNavigation nav = MazeNavigation.Active;
		if (nav == null)
		{
			GD.Print("MAZEPROBE no navigation on this stage - nothing to measure.");
			Finish();
			return;
		}

		// What one whole recompute costs, in one go. In play it is built a slice a frame
		// (MazeNavigation.FieldCellsPerFrame), at most five times a second and not at all while the
		// player holds one cell, so this is the total a field costs rather than what any frame pays.
		var watch = System.Diagnostics.Stopwatch.StartNew();
		nav.RecomputeNow(player.FootPosition);
		watch.Stop();
		recomputeMs.Add((float)watch.Elapsed.TotalMilliseconds);

		int alive = 0, reach = 0, stalled = 0, eligible = 0, close = 0, pinned = 0;
		detours.Clear();
		Vector2 p = player.GlobalPosition;
		var seen = new HashSet<ulong>();
		foreach (Node n in GetTree().GetNodesInGroup("enemies"))
		{
			if (n is not Enemy e || !IsInstanceValid(e) || e is BossEnemy)
				continue;
			alive++;
			ulong id = e.GetInstanceId();
			seen.Add(id);
			float path = nav.PathTilesAt(e.GlobalPosition);
			if (path < 0f)
				continue;
			reach++;
			if (path <= 8f)
				close++;

			float straight = e.GlobalPosition.DistanceTo(p) / 48f;
			if (straight > 3f)
				detours.Add(path / straight);

			if (lastPath.TryGetValue(id, out var prev) && elapsed - prev.At >= StallWindow - 0.01f && prev.Path > 4f)
			{
				eligible++;
				if (prev.Path - path < 1.5f)
				{
					stalled++;
					if (IsAgainstWall(e))
						pinned++;
				}
			}
			if (!lastPath.ContainsKey(id) || elapsed - lastPath[id].At >= StallWindow - 0.01f)
				lastPath[id] = (elapsed, path);
		}
		foreach (ulong stale in lastPath.Keys.Where(k => !seen.Contains(k)).ToList())
			lastPath.Remove(stale);

		detours.Sort();
		float medianDetour = detours.Count > 0 ? detours[detours.Count / 2] : 0f;
		float reachPct = alive > 0 ? 100f * reach / alive : 0f;
		float stallPct = eligible > 0 ? 100f * stalled / eligible : 0f;
		float closePct = alive > 0 ? 100f * close / alive : 0f;
		float pinPct = eligible > 0 ? 100f * pinned / eligible : 0f;
		GD.Print($"MAZEPROBE t={elapsed:0}s alive={alive} reach={reachPct:0}% detour={medianDetour:0.00} stalled={stallPct:0}% pinned={pinPct:0}% close={closePct:0}%");

		if (elapsed >= WarmupSeconds && alive > 0)
		{
			samples++;
			sumAlive += alive;
			sumReach += reachPct;
			sumStalled += stallPct;
			sumPinned += pinPct;
			sumClose += closePct;
			sumDetour += medianDetour;
		}
	}

	// A stalled enemy whose last slide hit the maze (a TileMapLayer) rather than another body is
	// pinned on geometry - the flow field's fault, not a queue's.
	private static bool IsAgainstWall(Enemy e)
	{
		for (int i = 0; i < e.GetSlideCollisionCount(); i++)
		{
			if (e.GetSlideCollision(i).GetCollider() is TileMapLayer)
				return true;
		}
		return false;
	}

	// Walk one way until a wall stops it, then turn - a crude corridor crawler, which is all it needs
	// to be: the subject is whether the horde keeps up, not where the player goes.
	private void Roam(float delta)
	{
		sinceMoveCheck += delta;
		sinceTurn += delta;
		if (sinceMoveCheck >= 0.5f)
		{
			bool blocked = player.GlobalPosition.DistanceTo(lastPos) < 10f;
			lastPos = player.GlobalPosition;
			sinceMoveCheck = 0f;
			if (blocked || sinceTurn > 7f)
			{
				Vector2 next;
				do next = Cardinals[rng.RandiRange(0, 3)];
				while (next == heading);
				heading = next;
				sinceTurn = 0f;
			}
		}

		Press("move_right", "move_left", heading.X);
		Press("move_down", "move_up", heading.Y);
	}

	private static void Press(string positive, string negative, float axis)
	{
		if (axis > 0.02f) { Input.ActionRelease(negative); Input.ActionPress(positive, 1f); }
		else if (axis < -0.02f) { Input.ActionRelease(positive); Input.ActionPress(negative, 1f); }
		else { Input.ActionRelease(positive); Input.ActionRelease(negative); }
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

	private void Finish()
	{
		finished = true;
		foreach (string a in MoveActions)
			Input.ActionRelease(a);

		if (samples > 0)
		{
			GD.Print("");
			GD.Print($"MAZEPROBE RESULT stage {StageIndex} bot={Bot} ({samples} samples after {WarmupSeconds:0}s)");
			GD.Print($"  alive   : {sumAlive / samples:0}");
			GD.Print($"  reach   : {sumReach / samples:0.0}%");
			GD.Print($"  detour  : {sumDetour / samples:0.00}");
			GD.Print($"  stalled : {sumStalled / samples:0.0}%");
			GD.Print($"  pinned  : {sumPinned / samples:0.0}%  (stalled with a wall in the last slide)");
			recomputeMs.Sort();
			GD.Print($"  recompute: median {recomputeMs[recomputeMs.Count / 2]:0.00} ms, worst {recomputeMs[^1]:0.00} ms (desktop)");
			GD.Print($"  close   : {sumClose / samples:0.0}%");
		}

		player = null;
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
