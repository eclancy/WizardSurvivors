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
// Flags: --minutes, --timescale, --character, --seed, --out, --sample-offers (boon frequency),
// --stage=N, --walk=DEG (walk a straight bearing, to exercise the endless terrain far from the start),
// --check-feet (count frames the footprint overlaps water or a wall), --no-open-nav (no enemy
// navigation on an open stage, for a same-build cost comparison). --stress-swarm, read by Node2DGame,
// fills to the enemy cap at once and prints PERF lines, navigation cost among them.
// Under --walk the report also carries a crowd line (how often the walk was held up by enemies
// ahead of it - see MeasureCrowd), and every report counts the enemies spawned by type.
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
	// --seek-apotheosis: steer level-ups toward the wizard's final form and report when it arrives.
	[Export] public bool SeekApotheosis { get; set; }
	// --all-unlocked: own everything, in memory, with saving switched off for the run.
	[Export] public bool AllUnlocked { get; set; }
	// --immortal: held at full health. The bot dies in a couple of minutes on its own, which ends a
	// measurement of the late game before it starts. XP comes from kills, so this does not move the
	// clock the card arrives on - it only lets the run reach it.
	[Export] public bool Immortal { get; set; }
	// Negative leaves Player.UpgradeOfferChance as the game ships it; anything else overrides it, so
	// the scarcity can be tuned run by run without a rebuild.
	[Export] public float UpgradeChance { get; set; } = -1f;
	// Same for the XP curve (Player.XpFirstLevelCost, Player.XpCostPerLevel).
	[Export] public int XpFirst { get; set; } = -1;
	[Export] public int XpStep { get; set; } = -1;
	[Export] public float TimeScale { get; set; } = 6f;
	[Export] public int CharacterIndex { get; set; } = 1;
	[Export] public int Seed { get; set; } = 1234;
	[Export] public string OutPath { get; set; } = "user://balance-report.txt";
	// --sample-offers=N: at every level-up, deal N extra hands from the player's state at that moment
	// and count the boons in them. The real hands are counted anyway (see CountHand), but a run sees
	// only thirty-odd of them, which is too few to tell a 20% boon rate from a 30% one. The extra
	// deals draw on the level-up RNG and re-roll whether upgrades are in this level-up's pool, so the
	// run diverges from an unsampled one - never compare a sampled run's damage table to anything.
	// Skipped under --seek-apotheosis, where a sample would also eat a pending augury.
	[Export] public int SampleOffers { get; set; }
	// --stage=N: the chapter to play (Global.SelectedStageIdx). Left alone when absent, so older
	// reports stay comparable.
	[Export] public int StageIndex { get; set; } = -1;
	// --walk=DEG: ignore the kiting and walk a straight bearing (0 = east, 90 = south) for the whole
	// run, side-stepping whatever blocks it. Exists to drive the endless terrain far from the start
	// (TerrainStreamer) - the kiting bot leans back to the origin and never leaves the first chunks.
	// Pair with --immortal, and with --terrain-log for the streamer's per-crossing timings.
	[Export] public float WalkBearingDegrees { get; set; } = float.NaN;
	// --check-feet: every physics frame, asks whether the player's footprint (Player's Feet body,
	// shrunk by a pixel so resting against a shore is not counted) overlaps water or a maze wall,
	// and counts the frames it does. Zero is the answer "his feet never hung over the water". The
	// same footprint grown by a pixel counts the frames he was up against a shore or wall at all,
	// which is what says the zero means something - a walk that never met the water would score it
	// too. Also counts the walk's stuck checks, the measure of snagging on a shoreline. Pair with --walk.
	[Export] public bool CheckFeet { get; set; }
	private int feetOverlapFrames;
	private int feetTouchFrames;
	private int feetCheckedFrames;
	private int stuckEvents;
	private PhysicsShapeQueryParameters2D feetQuery;
	private PhysicsShapeQueryParameters2D feetTouchQuery;
	// Enemy centres inside the water or wall COLLISION, every physics frame - the exact form of the
	// cell test in the 30 s line. A shoreline cell is solid whole while its polygon covers only part
	// of it, so a body can legitimately stand with its centre on the bank half of one; only this
	// says whether something (a shove, say) actually put a body in the lake.
	private PhysicsPointQueryParameters2D enemyTerrainQuery;
	private long enemyTerrainFrames, enemyTerrainChecks;
	// The crowd measure, on whenever --walk is: how often a player walking into the horde is held
	// up by it. Every unpaused physics step, the step's actual travel against Player.Speed, split by
	// whether an enemy was touching him on the side he was walking toward. "Pinned" is a step that
	// made under a quarter of his speed with one there - what a crowd body-blocking him looks like.
	// Measured from positions, the same as the game's own measuredPlayerVelocity, so it cannot be
	// flattered by a velocity the body asked for and did not get.
	private const float CrowdContactDistance = 52f;
	private Vector2 crowdLastPosition;
	private bool crowdHasLast;
	private int crowdSteps, crowdContactSteps, crowdPinnedSteps, crowdSlowSteps;
	private double crowdFreeRatioSum, crowdContactRatioSum;
	private int crowdContactEnemySum;
	// Every enemy that entered the game, by EnemyType - the spawn table as it actually played, so a
	// roster question (does the ghost turn up outside the Sketchbook?) is answered by a run rather
	// than by reading SelectEnemyForCurrentStage. A recycled enemy does not re-enter the tree, so
	// each body counts once.
	private readonly SortedDictionary<string, int> spawnedByType = new();

	private Node game;
	private Player player;
	private float elapsed;
	private bool finished;
	private int levelUpsHandled;
	// Level-ups that offered nothing at all - should always be zero (see AutoPickLevelUp).
	private int deadLevelUps;
	private float lastProgressLog;
	private int windowFrames;
	private double windowProcessMs, windowPhysicsMs, windowWorstMs;

	private static readonly string[] MoveActions = { "move_left", "move_right", "move_up", "move_down" };

	public override void _Ready()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--minutes=")) Minutes = float.Parse(arg.Substring(10));
			else if (arg.StartsWith("--timescale=")) TimeScale = float.Parse(arg.Substring(12));
			else if (arg.StartsWith("--character=")) CharacterIndex = int.Parse(arg.Substring(12));
			else if (arg.StartsWith("--seed=")) Seed = int.Parse(arg.Substring(7));
			else if (arg.StartsWith("--out=")) OutPath = arg.Substring(6);
			else if (arg == "--seek-apotheosis") SeekApotheosis = true;
			else if (arg == "--all-unlocked") AllUnlocked = true;
			else if (arg == "--immortal") Immortal = true;
			else if (arg == "--check-feet") CheckFeet = true;
			else if (arg.StartsWith("--sample-offers=")) SampleOffers = int.Parse(arg.Substring(16));
			else if (arg.StartsWith("--xp-first=")) XpFirst = int.Parse(arg.Substring(11));
			else if (arg.StartsWith("--xp-step=")) XpStep = int.Parse(arg.Substring(10));
			else if (arg.StartsWith("--stage=")) StageIndex = int.Parse(arg.Substring(8));
			// No navigation window on an open stage: the code as it was before open stages had one,
			// for a same-build cost comparison (MazeNavigation.BuildOnOpenStages).
			else if (arg == "--no-open-nav") MazeNavigation.BuildOnOpenStages = false;
			// No status marks, tints or slowed walks (StatusEffectLayer.Disabled), for a same-build
			// cost A/B against --status-all. The debuffs themselves still land.
			else if (arg == "--no-status-fx") StatusEffectLayer.Disabled = true;
			else if (arg.StartsWith("--walk=")) WalkBearingDegrees = float.Parse(arg.Substring(7), System.Globalization.CultureInfo.InvariantCulture);
			else if (arg.StartsWith("--upgrade-chance=")) UpgradeChance = float.Parse(arg.Substring(17), System.Globalization.CultureInfo.InvariantCulture);
		}

		// Immune to the pause. A level-up pauses the tree, and a driver that pauses with it can
		// never make the choice that would unpause it.
		ProcessMode = ProcessModeEnum.Always;
		StatusEffectLayer.MeasureCost = true;
		StatusEffectLayer.ResetPerf();

		GD.Print($"HARNESS minutes={Minutes} timescale={TimeScale} character={CharacterIndex} seed={Seed}");

		GD.Seed((ulong)Seed);
		Global.SelectedCharacterIdx = CharacterIndex;
		if (StageIndex >= 0)
			Global.SelectedStageIdx = StageIndex;
		Engine.TimeScale = TimeScale;

		GameStats.ResetRunTelemetry();

		// Every spell, boon and wizard owned, in memory only, with saving switched off for the run.
		// What the measurement needs is "when CAN the card arrive", which should not depend on which
		// unlocks the machine's save happens to have.
		if (AllUnlocked)
		{
			var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
			if (saveManager != null)
			{
				SaveManager.WritesDisabled = true;
				var data = new SaveData();
				foreach (string id in UnlockCatalog.AllSpellIds) data.UnlockedSpellIds.Add(id);
				foreach (string id in UnlockCatalog.AllBoonIds) data.UnlockedBoonIds.Add(id);
				foreach (string id in UnlockCatalog.AllWizardIds) data.UnlockedCharacterIds.Add(id);
				// And every level-up charge the shop sells, at its cap: a player who has finished the
				// shop argues with the roll, and a measurement that does not is measuring a new save.
				data.ArcaneUpgradeLevels["rerolls"] = 5;
				data.ArcaneUpgradeLevels["bans"] = 3;
				data.ArcaneUpgradeLevels["banked_levels"] = 3;
				data.ArcaneUpgradeLevels["auguries"] = 3;
				saveManager.ReplaceInMemory(data);
			}
		}

		game = ResourceLoader.Load<PackedScene>("res://scenes/node_2d_game.tscn").Instantiate();
		game.ChildEnteredTree += OnGameChildEntered;
		AddChild(game);
	}

	private void OnGameChildEntered(Node node)
	{
		if (node is not Enemy enemy)
			return;
		spawnedByType[enemy.EnemyType] = spawnedByType.TryGetValue(enemy.EnemyType, out int n) ? n + 1 : 1;
	}

	public override void _ExitTree()
	{
		// Left raised, a scene change would carry it into whatever loads next.
		Engine.TimeScale = 1.0f;
		MazeNavigation.BuildOnOpenStages = true;
		StatusEffectLayer.Disabled = false;
		StatusEffectLayer.MeasureCost = false;
		foreach (string a in MoveActions)
			Input.ActionRelease(a);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!float.IsNaN(WalkBearingDegrees))
			MeasureCrowd((float)delta);
		if (!CheckFeet || finished || player == null || !IsInstanceValid(player) || player.IsDead)
			return;
		if (player.GetNodeOrNull<CollisionShape2D>("Feet/CollisionShape2D") is not CollisionShape2D feet
			|| feet.Shape is not CapsuleShape2D capsule)
			return;

		if (feetQuery == null)
		{
			PhysicsShapeQueryParameters2D Probe(float grow) => new()
			{
				Shape = new CapsuleShape2D { Radius = capsule.Radius + grow, Height = capsule.Height + 2f * grow },
				CollisionMask = 1u << (LevelTilePainter.TerrainCollisionLayer - 1),
				CollideWithAreas = false,
			};
			feetQuery = Probe(-1f);
			feetTouchQuery = Probe(1f);
		}
		PhysicsDirectSpaceState2D space = player.GetWorld2D().DirectSpaceState;
		enemyTerrainQuery ??= new PhysicsPointQueryParameters2D
		{
			CollisionMask = 1u << (LevelTilePainter.TerrainCollisionLayer - 1),
			CollideWithAreas = false,
		};
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy)
				continue;
			enemyTerrainChecks++;
			enemyTerrainQuery.Position = enemy.GlobalPosition;
			if (space.IntersectPoint(enemyTerrainQuery, 1).Count > 0)
				enemyTerrainFrames++;
		}
		feetQuery.Transform = feet.GlobalTransform;
		feetTouchQuery.Transform = feet.GlobalTransform;
		feetCheckedFrames++;
		if (space.IntersectShape(feetTouchQuery, 1).Count > 0)
			feetTouchFrames++;
		if (space.IntersectShape(feetQuery, 1).Count > 0)
		{
			feetOverlapFrames++;
			if (feetOverlapFrames <= 5)
				GD.Print($"HARNESS feet overlap terrain at t={elapsed:0.0}s, footprint {player.FootPosition}");
		}
	}

	// Runs before the player in tree order, so a step measures the travel of the step before it and
	// the contact at the position it set out from - the same offset every step, so it is consistent.
	private void MeasureCrowd(float delta)
	{
		if (finished || player == null || !IsInstanceValid(player) || player.IsDead || delta <= 0f)
			return;
		// A level-up pauses the tree and the harness is not paused with it, so without this every
		// frame spent on a level-up card would score as a pin.
		if (GetTree().Paused)
		{
			crowdHasLast = false;
			return;
		}

		Vector2 position = player.GlobalPosition;
		Vector2 intent = new Vector2(
			Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left"),
			Input.GetActionStrength("move_down") - Input.GetActionStrength("move_up"));
		if (!crowdHasLast || intent.LengthSquared() < 0.01f)
		{
			crowdLastPosition = position;
			crowdHasLast = true;
			return;
		}

		float ratio = (position - crowdLastPosition).Length() / Mathf.Max(1f, player.Speed * delta);
		crowdLastPosition = position;
		Vector2 heading = intent.Normalized();

		int touching = 0;
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy)
				continue;
			Vector2 offset = enemy.GlobalPosition - position;
			if (offset.LengthSquared() < CrowdContactDistance * CrowdContactDistance && offset.Dot(heading) > 0f)
				touching++;
		}

		crowdSteps++;
		if (touching > 0)
		{
			crowdContactSteps++;
			crowdContactRatioSum += ratio;
			crowdContactEnemySum += touching;
			if (ratio < 0.25f)
				crowdPinnedSteps++;
			else if (ratio < 0.6f)
				crowdSlowSteps++;
		}
		else
		{
			crowdFreeRatioSum += ratio;
		}
	}

	private string CrowdSummary()
	{
		int free = Math.Max(1, crowdSteps - crowdContactSteps);
		int contact = Math.Max(1, crowdContactSteps);
		return $"crowd: {crowdContactSteps} of {crowdSteps} walking steps with an enemy ahead in contact"
			+ $" ({100f * crowdContactSteps / Math.Max(1, crowdSteps):0.0}%), {crowdContactEnemySum / (double)contact:0.00} touching on average;"
			+ $" speed ratio {crowdContactRatioSum / contact:0.00} in contact vs {crowdFreeRatioSum / free:0.00} clear;"
			+ $" pinned (<25%) {crowdPinnedSteps} ({100f * crowdPinnedSteps / contact:0.0}% of contact steps),"
			+ $" slowed (25-60%) {crowdSlowSteps} ({100f * crowdSlowSteps / contact:0.0}%); {stuckEvents} stuck checks";
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
			if (UpgradeChance >= 0f)
				player.UpgradeOfferChance = UpgradeChance;
			if (XpFirst >= 0) player.XpFirstLevelCost = XpFirst;
			if (XpStep >= 0) player.XpCostPerLevel = XpStep;
			player.XPToNextLevel = player.CalculateXPForLevel(player.CurrentLevel);
			GD.Print($"HARNESS upgrade chance {player.UpgradeOfferChance:0.00}, xp curve {player.XpFirstLevelCost} + {player.XpCostPerLevel}/level");
		}

		// Averaged over the window rather than read once: the monitors report the last frame only,
		// and one frame in thirty seconds says more about what the collector was doing than about the
		// game. Worst is kept alongside, since a hitch is what a player feels.
		double processMs = Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0;
		double physicsMs = Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0;
		windowFrames++;
		windowProcessMs += processMs;
		windowPhysicsMs += physicsMs;
		windowWorstMs = Math.Max(windowWorstMs, processMs + physicsMs);

		if (elapsed - lastProgressLog >= 30f)
		{
			lastProgressLog = elapsed;
			int frames = Math.Max(1, windowFrames);
			GD.Print($"HARNESS frame mean over {windowFrames} frames: process={windowProcessMs / frames:0.00}ms"
				+ $" physics={windowPhysicsMs / frames:0.00}ms worst process+physics={windowWorstMs:0.0}ms");
			windowFrames = 0;
			windowProcessMs = windowPhysicsMs = windowWorstMs = 0.0;
			// The status marks' own share of that: their prune and both draw passes, timed inside
			// the layer. Headless, the draw commands go to the dummy renderer, so this is the CPU
			// cost of issuing them and not what the GPU then does with them.
			long fxFrames = Math.Max(1, StatusEffectLayer.PerfFrames);
			GD.Print($"HARNESS status fx: {StatusEffectLayer.PerfTicks * 1e6 / System.Diagnostics.Stopwatch.Frequency / fxFrames:0.0}us/frame"
				+ $" of which drawing {StatusEffectLayer.PerfDrawTicks * 1e6 / System.Diagnostics.Stopwatch.Frequency / fxFrames:0.0}us"
				+ $" marks/frame={StatusEffectLayer.PerfMarks / (double)fxFrames:0.0}"
				+ (StatusEffectLayer.Disabled ? " (disabled)" : ""));
			StatusEffectLayer.ResetPerf();
			GD.Print($"HARNESS t={elapsed:0}s level={player.CurrentLevel} enemies={GetTree().GetNodesInGroup("enemies").Count}"
				+ $" pos=({player.GlobalPosition.X:0},{player.GlobalPosition.Y:0}) nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}"
				+ $" orbs={GetTree().GetNodesInGroup(XPOrb.GroupName).Count} fps={Engine.GetFramesPerSecond()}"
				+ $" process={Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.0}ms"
				+ $" physics={Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000:0.0}ms"
				+ $" pairs={Performance.GetMonitor(Performance.Monitor.Physics2DCollisionPairs)}");
			if (!float.IsNaN(WalkBearingDegrees))
				GD.Print("HARNESS " + CrowdSummary());
			if (CheckFeet)
			{
				// And the other half of moving the ground to its own physics layer: enemies must
				// still be stopped by it. An enemy body is far wider than a water tile's shore inset,
				// so its centre on a water or wall cell means it walked into one.
				int enemiesIn = 0, enemiesAll = 0;
				if (Node2DGame.Current?.Terrain is TerrainStreamer terrain)
					foreach (Node node in GetTree().GetNodesInGroup("enemies"))
					{
						if (node is not Node2D enemy)
							continue;
						enemiesAll++;
						if (terrain.IsBlockedAtWorld(enemy.GlobalPosition) || terrain.IsWallAtWorld(enemy.GlobalPosition))
							enemiesIn++;
					}
				GD.Print($"HARNESS feet: {feetOverlapFrames} of {feetCheckedFrames} frames overlapping terrain,"
					+ $" {feetTouchFrames} against it, {stuckEvents} stuck checks;"
					+ $" {enemiesIn} of {enemiesAll} enemies inside water or a wall;"
					+ $" {enemyTerrainFrames} of {enemyTerrainChecks} enemy-frames with a centre inside its collision");
			}
		}

		// A huge pool rather than a top-up: at 4x several physics steps land between two _Process
		// calls, and a surrounded bot could drain a normal pool in between.
		if (Immortal) { player.MaxHP = Math.Max(player.MaxHP, 100000); player.CurrentHP = player.MaxHP; }
		if (SeekApotheosis) TrackTier();
		AutoPickLevelUp();
		DriveMovement();

		if (elapsed >= Minutes * 60f || player.IsDead)
			Finish(player.IsDead ? "died" : "reached the clock");
	}

	// --- the bot ---------------------------------------------------------------------

	// Kite: move directly away from the weighted centre of everything close, and lean toward the
	// arena middle so it cannot reverse into a wall and be pinned there.
	private float stuckCheckAt, detourUntil;
	private Vector2 stuckCheckFrom, detourDirection;
	private float detourSide = 1f;

	private void DriveMovement()
	{
		if (!float.IsNaN(WalkBearingDegrees))
		{
			Walk();
			return;
		}

		// Immortal runs hold their ground. Kiting is how the bot survives, and a bot that cannot die
		// has no reason to - while running from everything means a close-range spell (Cinderbreath)
		// never touches an enemy, which made the Pyromancer look unable to level at all.
		if (Immortal)
		{
			// ...except to pick up XP. A long-range spell kills where the bot is not, and a bot that
			// never walks to the orbs never levels - the Stormcaller sat at level 3 for eleven minutes.
			Vector2? orb = NearestXpOrb();
			if (orb is Vector2 at && at.DistanceTo(player.GlobalPosition) > 16f)
			{
				Vector2 to = (at - player.GlobalPosition).Normalized();
				// A straight line to the orb can run into blocking terrain, and the bot used to stay
				// wedged there for the rest of the run - Vainamoinen stood still for ten minutes while
				// five hundred orbs piled up on the far side of a rock. Not having moved for half a
				// second while trying to is "stuck": side-step for a second, alternating sides.
				if (elapsed < detourUntil)
					to = detourDirection;
				else if (elapsed - stuckCheckAt >= 0.5f)
				{
					if (player.GlobalPosition.DistanceTo(stuckCheckFrom) < 4f)
					{
						detourSide = -detourSide;
						detourDirection = new Vector2(-to.Y, to.X) * detourSide;
						detourUntil = elapsed + 1f;
						to = detourDirection;
					}
					stuckCheckAt = elapsed;
					stuckCheckFrom = player.GlobalPosition;
				}
				Press("move_right", "move_left", to.X);
				Press("move_down", "move_up", to.Y);
				return;
			}
			foreach (string a in MoveActions)
				Input.ActionRelease(a);
			return;
		}

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

		Press("move_right", "move_left", desired.X);
		Press("move_down", "move_up", desired.Y);
	}

	// Straight along the bearing; when that has not moved the bot for half a second, slide along
	// the obstacle to one side for a while, alternating sides, and try the bearing again. Each
	// failure in a row slides twice as long, so a long wall is searched further and further out
	// either side instead of the bot pacing the same two cells for ever (a maze wall did that).
	// Crude, and enough to cross lakes, woods and a braided maze.
	private float walkSideUntil;
	private float walkSide = 1f;
	private int walkFailures;
	private float walkProgressFrom;

	private void Walk()
	{
		float rad = Mathf.DegToRad(WalkBearingDegrees);
		Vector2 bearing = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
		// Maze only: every stage has a navigation window now, but planning round lakes would change
		// what an open-stage --walk measures compared with older reports.
		if (MazeNavigation.Active is MazeNavigation maze && maze.IsMaze && WalkMaze(maze, bearing))
			return;
		Vector2 to = bearing;
		if (elapsed < walkSideUntil)
			to = (new Vector2(-bearing.Y, bearing.X) * walkSide + bearing * 0.15f).Normalized();
		else if (elapsed - stuckCheckAt >= 0.5f)
		{
			float along = player.GlobalPosition.Dot(bearing);
			if (along - walkProgressFrom > 400f)
			{
				walkFailures = 0;
				walkProgressFrom = along;
			}
			if (player.GlobalPosition.DistanceTo(stuckCheckFrom) < 8f)
			{
				walkSide = -walkSide;
				walkSideUntil = elapsed + 1.2f * Mathf.Pow(2f, Mathf.Min(walkFailures, 5));
				walkFailures++;
				stuckEvents++;
			}
			stuckCheckAt = elapsed;
			stuckCheckFrom = player.GlobalPosition;
		}
		Press("move_right", "move_left", to.X);
		Press("move_down", "move_up", to.Y);
	}

	// A maze has pockets that open back the way you came, and no amount of side-stepping gets a
	// bearing-follower out of one. So in a maze the bot plans: a breadth-first search over the live
	// navigation window (MazeNavigation.IsBlockedAt, the walls the game itself routes enemies round),
	// on a one-tile grid round the bot, to the reachable cell furthest along the bearing, and walks
	// that route. Replanned every couple of seconds as the window moves with it.
	private readonly List<Vector2> walkRoute = new();
	private float walkReplanAt;

	private bool WalkMaze(MazeNavigation maze, Vector2 bearing)
	{
		if (walkRoute.Count == 0 || elapsed >= walkReplanAt)
		{
			PlanMazeRoute(maze, bearing);
			walkReplanAt = elapsed + 2f;
		}
		// The feet, not GlobalPosition: they are what the walls stop (Player.FootPosition), and a
		// route planned for the middle of the sprite runs waypoints along a wall that the feet,
		// 36 px lower, can never get within 30 px of - the bot then stood still for good.
		Vector2 at = player.FootPosition;
		while (walkRoute.Count > 0 && walkRoute[0].DistanceTo(at) < 30f)
			walkRoute.RemoveAt(0);
		if (walkRoute.Count == 0)
			return false;
		Vector2 to = (walkRoute[0] - at).Normalized();

		// The route knows the walls, not the props: a rack or a rock in the corridor stops the bot
		// dead on a route that is otherwise right. Not having moved for half a second, it steps
		// sideways for a moment, alternating sides, then carries on.
		if (elapsed < walkSideUntil)
			to = (new Vector2(-to.Y, to.X) * walkSide + to * 0.3f).Normalized();
		else if (elapsed - stuckCheckAt >= 0.5f)
		{
			if (at.DistanceTo(stuckCheckFrom) < 8f)
			{
				walkSide = -walkSide;
				walkSideUntil = elapsed + 0.6f;
			}
			stuckCheckAt = elapsed;
			stuckCheckFrom = at;
		}
		Press("move_right", "move_left", to.X);
		Press("move_down", "move_up", to.Y);
		return true;
	}

	private void PlanMazeRoute(MazeNavigation maze, Vector2 bearing)
	{
		walkRoute.Clear();
		// Inside the navigation window, which reaches at least 56 tiles from the player (Node2DGame).
		const int radius = 50;
		const int size = radius * 2 + 1;
		float step = maze.TileSize;
		Vector2 origin = player.FootPosition;
		var parent = new int[size * size];
		System.Array.Fill(parent, -2);
		var queue = new Queue<int>();
		int start = radius + radius * size;
		parent[start] = -1;
		queue.Enqueue(start);
		int best = start;
		float bestScore = float.MinValue;
		int[] dx = { 1, -1, 0, 0 };
		int[] dy = { 0, 0, 1, -1 };
		while (queue.Count > 0)
		{
			int c = queue.Dequeue();
			int cx = c % size, cy = c / size;
			Vector2 world = origin + new Vector2(cx - radius, cy - radius) * step;
			float score = (world - origin).Dot(bearing);
			if (score > bestScore) { bestScore = score; best = c; }
			for (int d = 0; d < 4; d++)
			{
				int nx = cx + dx[d], ny = cy + dy[d];
				if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
				int n = nx + ny * size;
				if (parent[n] != -2) continue;
				Vector2 nw = origin + new Vector2(nx - radius, ny - radius) * step;
				// The cell, and the footprint's half-width (28) either side of the step, must all be floor.
				if (maze.IsBlockedAt(nw) || maze.IsBlockedAt((world + nw) * 0.5f)
					|| maze.IsBlockedAt(nw + new Vector2(dy[d], dx[d]) * 28f) || maze.IsBlockedAt(nw - new Vector2(dy[d], dx[d]) * 28f))
					continue;
				parent[n] = c;
				queue.Enqueue(n);
			}
		}
		for (int c = best; c >= 0 && c != start; c = parent[c])
			walkRoute.Insert(0, origin + new Vector2(c % size - radius, c / size - radius) * step);
	}

	private Vector2? NearestXpOrb()
	{
		Vector2? best = null;
		float bestDistance = float.MaxValue;
		// Enemy.DropXp parents its orb to GetTree().CurrentScene, which under the harness is THIS
		// node, not the game. Searching only the game found nothing, ever: the walk-to-orbs below
		// never fired, and every immortal measurement before it collected only what drifted into
		// the pickup radius - which flattered short-range spells and starved anything that kills
		// at a distance. Both are searched so a change to where orbs live cannot hide them again.
		foreach (Node root in new Node[] { this, game })
		{
			foreach (Node child in root.GetChildren())
			{
				if (child is not XPOrb orb || orb.IsQueuedForDeletion()) continue;
				float d = orb.GlobalPosition.DistanceSquaredTo(player.GlobalPosition);
				if (d < bestDistance) { bestDistance = d; best = orb.GlobalPosition; }
			}
		}
		return best;
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
		// A real level-up pauses the tree, and a picked menu is queued for deletion before it goes.
		// Acting on anything else re-sent one choice 1,639 times into a run that had four level-ups.
		if (!GetTree().Paused || menu.IsQueuedForDeletion())
			return;

		IReadOnlyList<LevelUpOption> options = menu.GetOfferedOptions() ?? new List<LevelUpOption>();
		IReadOnlyList<LevelUpOption> ascensions = menu.GetAscensionOptions();
		// A DEAD LEVEL-UP: nothing on the screen but Skip. The rule is that this never happens
		// (.ai/gameplay-design.md, "A level-up is never a dead pick" - Marginalia is the card that
		// keeps it), so it is counted and said out loud rather than quietly stalling the run, which
		// is what returning here used to do: the tree stayed paused until the clock ran out.
		if (options.Count == 0 && ascensions.Count == 0)
		{
			deadLevelUps++;
			GD.PrintErr($"HARNESS DEAD LEVEL-UP at t={elapsed:0}s, level {player?.CurrentLevel ?? 0}: no card and no ascension");
			menu.EmitSignal(LevelUpMenu.SignalName.SkipRequested);
			return;
		}

		CountHand(options);

		if (!SeekApotheosis && ascensions.Count > 0)
		{
			LevelUpOption up = ascensions[0];
			SpellEvolutionOption branch = up.EvolutionChoices.FirstOrDefault(e => !up.EvolutionLockReason.ContainsKey(e.Id));
			if (branch != null)
			{
				levelUpsHandled++;
				menu.EmitSignal(LevelUpMenu.SignalName.WeaponSelected, $"{up.SpellId}:{branch.Id}");
				return;
			}
		}
		if (options.Count == 0)
			return;

		if (SeekApotheosis)
		{
			string choice = PickTowardApotheosis(options, ascensions, menu.Charges);
			// Charges are not picks: the menu stays open and re-deals (reroll, ban) or remembers
			// (augury), and the next frame decides again.
			if (choice == RerollAction)
			{
				rerollsUsed++;
				menu.EmitSignal(LevelUpMenu.SignalName.RerollRequested);
				return;
			}
			if (choice.StartsWith(BanAction))
			{
				bansUsed++;
				menu.EmitSignal(LevelUpMenu.SignalName.BanRequested, choice.Substring(BanAction.Length));
				return;
			}
			if (choice.StartsWith(AuguryAction))
			{
				auguriesUsed++;
				auguryUsedThisLevel = true;
				menu.EmitSignal(LevelUpMenu.SignalName.AuguryRequested, choice.Substring(AuguryAction.Length));
				return;
			}
			auguryUsedThisLevel = false;
			rerollsThisLevel = 0;
			levelUpsHandled++;
			menu.EmitSignal(LevelUpMenu.SignalName.WeaponSelected, choice);
			return;
		}

		// Fill the loadout first, then deepen the shallowest thing in it. A bot that always took
		// the first card would report on card ORDER rather than on the spells.
		// A full book is offered no new spell any more, so "fill" ends by itself when the book is
		// full; a Marginalia card is IsNewUnlock and only ever dealt alone, so it is taken here too.
		LevelUpOption pick = options.FirstOrDefault(o => o.IsNewUnlock)
			?? options.OrderBy(o => o.NextLevel).First();

		levelUpsHandled++;
		menu.EmitSignal(LevelUpMenu.SignalName.WeaponSelected, pick.SpellId);
	}

	// --- --seek-apotheosis (issue #59: "report the game clock when the card appeared") ---------
	// A player who knows what they want: take the final form the moment it is offered; walk the
	// starting spell down its required path; otherwise take whatever carries the wizard's element,
	// preferring evolution branches that deepen it. Anything else falls back to the default policy.
	// It is a measurement of the earliest plausible arrival, not of a typical run.

	// Resolved once. Calling CharacterRoster.GetAll every frame loads seven resources a frame, and
	// the churn of short-lived wrappers crashed the run in the finalizer (0xC000001D).
	private ApotheosisDefinition seekTarget;
	private bool seekTargetResolved;

	private ApotheosisDefinition SeekTarget()
	{
		if (!seekTargetResolved)
		{
			seekTargetResolved = true;
			string characterId = CharacterRoster.GetByIndex(CharacterIndex)?.Id;
			seekTarget = ApotheosisCatalog.ForCharacter(characterId);
			// Said out loud, because a run with no target falls back to taking the first card and
			// otherwise reads exactly like a wizard who could not reach his form.
			GD.Print(seekTarget != null
				? $"HARNESS seeking {seekTarget.Form.DisplayName} for '{characterId}' ({seekTarget.RequiredElement} tier 6)"
				: $"HARNESS NO SEEK TARGET for character index {CharacterIndex} (id '{characterId ?? "null"}') - picks fall back to the first card");
		}
		return seekTarget;
	}

	private float apotheosisOfferedAt = -1f;
	private float apotheosisTakenAt = -1f;
	private int peakTier;

	private void TrackTier()
	{
		ApotheosisDefinition target = SeekTarget();
		if (target == null || player == null) return;
		int tier = player.GetElementTier(target.RequiredElement);
		if (tier > peakTier)
		{
			peakTier = tier;
			GD.Print($"HARNESS {target.RequiredElement} reached tier {tier} at t={elapsed:0}s ({elapsed / 60f:0.0} min)");
		}
	}

	private const string RerollAction = "!reroll";
	private const string BanAction = "!ban:";
	private const string AuguryAction = "!augury:";
	private int rerollsUsed, bansUsed, auguriesUsed;
	private bool auguryUsedThisLevel;
	// Counted here as well as read from the bar, so a bar that is not refreshed after a use can
	// never make the bot spend a charge the game will refuse, forever.
	private int rerollsThisLevel;

	private string PickTowardApotheosis(IReadOnlyList<LevelUpOption> options, IReadOnlyList<LevelUpOption> ascensions, LevelUpCharges charges)
	{
		ApotheosisDefinition target = SeekTarget();
		if (target == null)
			return options.First().SpellId;
		string element = target.RequiredElement.ToString();

		// The strip first: it is above the cards, and everything in it is the reward for a plan.
		LevelUpOption final = ascensions.FirstOrDefault(o => o.MilestoneLevel == ApotheosisCatalog.MilestoneLevel);
		if (final != null)
		{
			if (apotheosisOfferedAt < 0f) apotheosisOfferedAt = elapsed;
			apotheosisTakenAt = elapsed;
			GD.Print($"HARNESS APOTHEOSIS offered and taken at t={elapsed:0}s ({elapsed / 60f:0.0} min)");
			return $"{final.SpellId}:{target.Form.Id}";
		}
		LevelUpOption ownAscension = ascensions.FirstOrDefault(o => o.SpellId == target.SpellId
			&& !o.EvolutionLockReason.ContainsKey(target.RequiredLevel8EvolutionId)
			&& o.EvolutionChoices.Any(e => e.Id == target.RequiredLevel8EvolutionId));
		if (ownAscension != null)
			return $"{ownAscension.SpellId}:{target.RequiredLevel8EvolutionId}";
		foreach (LevelUpOption other in ascensions.Where(o => o.SpellId != target.SpellId))
		{
			SpellEvolutionOption deepen = other.EvolutionChoices.FirstOrDefault(e => !other.EvolutionLockReason.ContainsKey(e.Id)
				&& e.BonusElementWeights != null && e.BonusElementWeights.ContainsKey(target.RequiredElement.ToString()));
			if (deepen != null)
				return $"{other.SpellId}:{deepen.Id}";
		}

		// Cards stop at level 7; the Ascension to 8 is taken from the strip above.
		LevelUpOption signature = options.FirstOrDefault(o => o.SpellId == target.SpellId);
		if (signature != null)
		{
			if (signature.IsEvolutionMilestone && signature.MilestoneLevel == 4)
				return $"{signature.SpellId}:{target.RequiredLevel4EvolutionId}";
			if (signature.IsEvolutionMilestone && signature.MilestoneLevel == 8)
				return $"{signature.SpellId}:{target.RequiredLevel8EvolutionId}";
			return signature.SpellId;
		}

		bool Carries(LevelUpOption o) => o.SpellElementTags != null && o.SpellElementTags.ContainsKey(element);
		// Only a card that raises the count NOW is progress: a new carrier, a boon that carries it,
		// or a milestone with a branch that deepens it. Levelling a held hybrid from 1 to 3 adds
		// nothing, and taking it because it "carries" the element is how the bot sat on Grass 4 for
		// ten minutes with every charge unspent - Thorn Vine was always on offer, so it never
		// argued with the roll for a card that would actually have counted.
		bool Raises(LevelUpOption o) => Carries(o) && (o.IsNewUnlock || o.IsBoon
			|| (o.IsEvolutionMilestone && o.EvolutionChoices != null && o.EvolutionChoices.Any(e =>
				e.BonusElementWeights != null && e.BonusElementWeights.ContainsKey(element))));
		LevelUpOption raising = options
			.Where(Raises)
			.OrderBy(o => o.SpellElementTags.Count)       // pure before hybrid
			.ThenBy(o => o.IsNewUnlock ? 0 : 1)
			.FirstOrDefault();
		if (raising != null)
			return WithEvolution(raising, element);

		// Nothing on offer helps. Argue with the roll before settling: name a carrier for next time,
		// strike an off-element spell from the pool, or deal again.
		if (charges != null)
		{
			int slotsUsed = player.GetEquippedSpells().Count;
			if (charges.Auguries > 0 && auguriesUsed < 3 && !auguryUsedThisLevel && charges.AuguryCandidates != null)
			{
				// A carrier spell while there is a slot for it; otherwise a boon that carries the
				// element. Boons are rare (Player.BoonOfferChance) and naming one is exactly what a
				// player chasing a thin element spends an augury on.
				LevelUpOption named = (slotsUsed < player.SpellSlots
						? charges.AuguryCandidates
							.Where(o => o.IsNewUnlock && !o.IsBoon && Carries(o))
							.OrderBy(o => o.SpellElementTags.Count)
							.FirstOrDefault()
						: null)
					?? (player.CanTakeMoreBoons
						? charges.AuguryCandidates.FirstOrDefault(o => o.IsBoon && Carries(o))
						: null);
				if (named != null)
					return AuguryAction + named.SpellId;
			}
			if (charges.Bans > 0 && bansUsed < 3)
			{
				LevelUpOption offElement = options.FirstOrDefault(o => o.IsNewUnlock && !o.IsBoon && !Carries(o));
				if (offElement != null)
					return BanAction + offElement.SpellId;
			}
			if (charges.Rerolls > 0 && rerollsThisLevel < 5)
			{
				rerollsThisLevel++;
				return RerollAction;
			}
		}

		// The roll would not give way. Walking a held carrier toward its deepening milestone is the
		// next best thing, and still costs no slot.
		LevelUpOption towardMilestone = options
			.Where(o => !o.IsNewUnlock && Carries(o))
			.OrderBy(o => o.SpellElementTags.Count)
			.ThenByDescending(o => o.NextLevel)
			.FirstOrDefault();
		if (towardMilestone != null)
			return WithEvolution(towardMilestone, element);

		// Nothing carries the element. A focused player does not spend a slot on an off-element
		// spell - six slots are the whole budget for tier 6 - so take what costs no slot: a boon,
		// then an upgrade of something already held. A new off-element spell is the last resort.
		LevelUpOption fallback = options.FirstOrDefault(o => o.IsBoon)
			?? options.Where(o => !o.IsNewUnlock && !o.IsBoon).OrderBy(o => o.NextLevel).FirstOrDefault()
			?? options.First();
		return WithEvolution(fallback, element);
	}

	// A milestone card needs a branch named, or the level is taken with no evolution at all.
	private static string WithEvolution(LevelUpOption option, string element)
	{
		if (!option.IsEvolutionMilestone || option.EvolutionChoices == null || option.EvolutionChoices.Count == 0)
			return option.SpellId;
		SpellEvolutionOption deepen = option.EvolutionChoices.FirstOrDefault(e =>
			e.BonusElementWeights != null && e.BonusElementWeights.ContainsKey(element));
		return $"{option.SpellId}:{(deepen ?? option.EvolutionChoices[0]).Id}";
	}

	// --- how often a boon is on the screen ----------------------------------------------
	// Boons are meant to be rare (.ai/passives-and-items.md, "How rare a boon is"). These count what
	// the bot was actually shown, one entry per hand dealt - a reroll or a ban deals a new hand.
	private IReadOnlyList<LevelUpOption> lastCountedHand;
	private int handsSeen, handsWithBoon, boonCardsSeen;
	private int sampledHands, sampledHandsWithBoon, sampledBoonCards;

	private void CountHand(IReadOnlyList<LevelUpOption> options)
	{
		// The menu keeps one list per deal, so the same reference means the same hand seen again
		// on a later frame, not a new one.
		if (ReferenceEquals(options, lastCountedHand) || options.Count == 0)
			return;
		lastCountedHand = options;

		int boons = options.Count(o => o.IsBoon);
		handsSeen++;
		boonCardsSeen += boons;
		if (boons > 0) handsWithBoon++;

		if (SampleOffers <= 0 || SeekApotheosis || player == null)
			return;
		for (int i = 0; i < SampleOffers; i++)
		{
			player.BeginLevelUpOffer();
			int n = player.GetLevelUpOptions(options.Count).Count(o => o.IsBoon);
			sampledHands++;
			sampledBoonCards += n;
			if (n > 0) sampledHandsWithBoon++;
		}
	}

	private static string Rate(int hits, int total) =>
		total <= 0 ? "n/a" : $"{hits}/{total} = {100f * hits / total:0.0}% (one in {(hits > 0 ? (float)total / hits : 0f):0.0})";

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
			$"  dead level-ups: {deadLevelUps}   marginalia taken: {player?.MarginaliaTaken ?? 0}   spell slots: {player?.SpellSlots ?? Player.MaxSpellSlots}",
			SeekApotheosis ? $"  charges used : {rerollsUsed} rerolls, {bansUsed} bans, {auguriesUsed} auguries" : "",
			SeekApotheosis ? $"  apotheosis   : {(apotheosisTakenAt < 0f ? "never offered" : $"taken at {apotheosisTakenAt:0}s ({apotheosisTakenAt / 60f:0.0} min)")}; peak element tier {peakTier}" : "",
			$"  boon hands   : {Rate(handsWithBoon, handsSeen)}; {boonCardsSeen} boon cards",
			SampleOffers > 0 && !SeekApotheosis ? $"  boon sampled : {Rate(sampledHandsWithBoon, sampledHands)}; {sampledBoonCards} boon cards ({(sampledHands > 0 ? (float)sampledBoonCards / sampledHands : 0f):0.000} per hand)" : "",
			!float.IsNaN(WalkBearingDegrees) ? "  " + CrowdSummary() : "",
			$"  taken        : {GameStats.RunDamageTaken} damage in {GameStats.RunHitsTaken} hits",
			$"  stage        : {Global.SelectedStageIdx}; spawned " + string.Join(", ", spawnedByType.Select(p => $"{p.Key} {p.Value}")),
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
