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
// --give-relics=id,id and --grant-boons=id,id hand the bot those from the start (saving off), e.g.
// the Brazier as ember_flask,inferno_core,obsidian_heart; the report's "stand still" line says how
// long the bot stood still and what heat and stance that earned. --stand-still never moves at all
// (pair with --immortal): the ceiling for the Brazier. Measured 2026-10-23, the --immortal bot is
// still only about a tenth of the time - it walks to XP orbs almost constantly. --aim-facing (with
// --immortal) turns the standing bot toward the nearest enemy, for the facing family (Stormsweep,
// Sleet Runes), which otherwise fire wherever the last orb lay.
// --spell=ID with --character=0 opens the Test Wizard on that spell and reports its damage per level
// (--partner=ID adds a second, never-levelled spell, for Frost Shard and Arcane Explosion; --enemy-hp=X
// multiplies the ordinary spawns' health - not --hp-scale, which is the PLAYER's - so a strong spell
// meets a crowd it cannot clear and two runs differ by what the spell does, not by the spawn rate);
// --solo then levels nothing else (--branch4= / --branch8= pick its evolutions), and
// --force-ascension takes --branch8 the moment the spell reaches its card cap, past the gates a solo
// bot can never meet. The per-level table carries an "all dmg" column with DoT ticks included.
// Under --walk the report also carries a crowd line (how often the walk was held up by enemies
// ahead of it - see MeasureCrowd), and every report counts the enemies spawned by type.
// Pick policies: the default below; --seek-apotheosis (the final form's floor); --policy=ordinary
// and --seek-ascensions (how many level 8 Ascensions a run gets, ordinary and planned - both also
// open chests); --full-shop adds every Arcane Codex upgrade to --all-unlocked. Every report says how
// many Ascensions were taken and when, what blocked each spell that did not ascend, and ends with a
// one-line HARNESS SUMMARY for tables.
// Falling behind (.ai/run-pacing.md, section 10): --survivor is a mortal bot that dodges and walks to
// orbs - the player who keeps up; add --pick-every=N (take one level-up in N) or --no-orbs (never
// walk to XP) for one who does not. Every report carries a per-minute pace ledger (HARNESS MINUTE
// lines live): level, level-ups, XP banked, kills, spawns, the crowd, damage taken, swarm pressure.
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
	// --seek-ascensions: the PLANNED policy - steer every pick toward taking as many level 8
	// Ascensions as the run allows (see PickTowardAscensions). The ceiling a player who plans for
	// them can reach, as --seek-apotheosis is the floor for the final form.
	[Export] public bool SeekAscensions { get; set; }
	// --policy=ordinary: the GOOD ORDINARY policy (PickOrdinary) - fill the book, leaning toward the
	// element it already holds most of, then level its strongest spells; takes every Ascension the
	// moment it is offered; argues with no roll. The default policy below is not this: it levels the
	// LOWEST spell, which spreads the upgrades evenly over six spells, so it brings all of them to 7
	// late or none of them - a measure of card order, not of how many Ascensions a run pays for
	// (.ai/gameplay-design.md, "How many Ascensions a run gets").
	[Export] public bool OrdinaryPolicy { get; set; }
	// --full-shop: with --all-unlocked, every Arcane Codex upgrade at its cap rather than only the
	// four level-up charges - Sage Spiral's +60% XP above all. The "powerups" half of the Ascension
	// ceiling; leave it off for anything compared with the Apotheosis floor, which is measured without.
	[Export] public bool FullShop { get; set; }
	// --all-unlocked: own everything, in memory, with saving switched off for the run.
	[Export] public bool AllUnlocked { get; set; }
	[Export] public bool FreshSave { get; set; }
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
	// --spell=ID: the Test Wizard's starting spell (Global.TestWizardStartingSpellId), so one spell
	// can be measured as the run's opener without a character built around it. Only the Test Wizard
	// reads it - pair with --character=0. The report then breaks that spell's damage down by level.
	[Export] public string SpellId { get; set; } = "";
	// --solo: with --spell, the bot takes only that spell's cards (and its ascension) and skips every
	// other level-up, so the per-level table measures the spell alone rather than a loadout whose
	// other five spells changed how many enemies it met. --branch4=ID / --branch8=ID name the
	// evolutions it takes (default: the first one on offer). Pair with --upgrade-chance=1, or most
	// level-ups carry no card for it and the spell levels at a crawl.
	[Export] public bool Solo { get; set; }
	[Export] public string Branch4 { get; set; } = "";
	[Export] public string Branch8 { get; set; } = "";
	private int soloSkips;
	// --force-ascension: with --solo and --branch8, ascend the spell the moment it reaches its card
	// cap, past the player-level and element gates. A solo bot holds one spell's tags and is level
	// 9 at minute 6, so it never meets either gate and a level 8 branch could not be measured at
	// all. The report says it was forced.
	[Export] public bool ForceAscension { get; set; }
	private bool forcedAscension;
	// --partner=ID: with --solo, a second spell equipped at level 1 and never levelled. Two spells cannot
	// be measured alone: Frost Shard only shatters what something else killed, and Arcane Explosion
	// is charged by the casts of the other spells, so solo both dealt nothing at all. The partner's
	// damage is its own source, so the attributed column stays the measured spell's.
	[Export] public string Partner { get; set; } = "";
	private bool partnerGiven;
	// Damage and unpaused seconds the --spell spell spent at each level, read off GameStats every
	// frame, so the report can say what it does per second at level 3 rather than across the run.
	private readonly double[] levelSeconds = new double[10];
	private readonly long[] levelDamage = new long[10];
	private int lastSpellDamage;
	// The same, from everything enemies lost - poison and other ticks arrive with no source, so a
	// poison spell's attributed column is only its impacts. Under --solo nothing else is dealing it.
	private readonly long[] levelAllDamage = new long[10];
	private int lastAllDamage;
	// --walk=DEG: ignore the kiting and walk a straight bearing (0 = east, 90 = south) for the whole
	// run, side-stepping whatever blocks it. Exists to drive the endless terrain far from the start
	// (TerrainStreamer) - the kiting bot leans back to the origin and never leaves the first chunks.
	// Pair with --immortal, and with --terrain-log for the streamer's per-crossing timings.
	[Export] public float WalkBearingDegrees { get; set; } = float.NaN;
	// --explore: the chapter's side event is out in the world from the start and only begins when
	// the player walks to it (.ai/side-events.md, "Finding the event"), so a bot that kites round
	// the origin never meets it. This one goes looking, the way a player who knows the game would:
	// to the wayfinder first, then along the arrows to every site of the event, following the
	// objective arrow (the Warden, the key, the cell, the caravan master, the tender) once it has
	// begun, then holds its ground as --immortal does. It logs each leg's game time and the distance
	// it actually walked - the measurement behind the placement bands. Writes are switched off: a
	// won event grants its discovery and saves on the spot.
	[Export] public bool Explore { get; set; }
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
	private int crowdSteps, crowdContactSteps, crowdPinnedSteps, crowdSlowSteps, crowdPinnedByOrdinarySteps;
	private double crowdFreeRatioSum, crowdContactRatioSum;
	private int crowdContactEnemySum;
	// Every enemy that entered the game, by EnemyType - the spawn table as it actually played, so a
	// roster question (does the ghost turn up outside the Sketchbook?) is answered by a run rather
	// than by reading SelectEnemyForCurrentStage. A recycled enemy does not re-enter the tree, so
	// each body counts once.
	private readonly SortedDictionary<string, int> spawnedByType = new();
	// --give-relics=id,id / --grant-boons=id,id: handed to the bot the moment the player exists, so a
	// set or a boon can be measured in a run that would otherwise meet it only by luck (the bot's
	// chests take the no-menu path and pay a Relic Key). Either switches saving off: a run carrying
	// things it never found must not pay its Arcane Energy into the real save.
	private string[] giveRelics = Array.Empty<string>();
	private readonly Dictionary<string, object> gameOverrides = new();
	private string[] grantBoons = Array.Empty<string>();
	// --stand-still: the bot never moves (see DriveMovement).
	[Export] public bool StandStill { get; set; }
	// --aim-facing: with --immortal, a bot that aims with its feet. Whenever it is standing (not walking
	// to an orb) it taps toward the nearest enemy for ONE physics frame every AimTapSeconds - enough to
	// turn Player.FacingDirection and Stormsweep's side to the threat, too little to move it (about 4 px).
	// Exists for the facing family (Stormsweep, Sleet Runes), which a bot facing wherever its last orb
	// lay undersells; the default bot is the floor, this the player who turns to face the crowd.
	[Export] public bool AimFacing { get; set; }
	private const float AimTapSeconds = 0.25f;
	private float nextAimTapAt;
	// --circle=R: with --immortal, a bot that never stops - it walks a circle R px across its start,
	// round and round, collecting what the magnet reaches on the way. Holding ground measures a player
	// who stands still and walking a bearing outruns the swarm, so neither could measure a spell that
	// pays for moving (Cyclone Slash's gust, issue #68): on a circle the crowd keeps catching up.
	[Export] public float CircleRadius { get; set; }
	private Vector2 circleCentre;
	private bool circleCentred;
	// --survivor: a MORTAL bot that plays the way a careful person does (DriveSurvivor) - steps away
	// from whatever is closing, walks to XP orbs and chests when the way is clear. The old mortal
	// kite bot fled in a straight line, collected nothing and died at level 2 in its second minute,
	// so it could not say whether a player who keeps up survives; this one exists to say that, and
	// whether one who falls behind dies (.ai/run-pacing.md, section 10). Pair with a pick policy.
	[Export] public bool Survivor { get; set; }
	// --no-orbs: with --survivor, never walk toward an orb or a chest - it gathers only what drifts
	// into the pickup radius. One way of falling behind: kills made, XP left on the ground.
	[Export] public bool NoOrbs { get; set; }
	// --pick-every=N: take one level-up in N and skip the rest. The other way of falling behind:
	// the XP comes in and the build does not grow. 1 (the default) takes every one.
	[Export] public int PickEvery { get; set; } = 1;
	private int weakLevelUpsSeen, weakSkips;
	private ulong weakDecidedMenu;
	// THE STAND-STILL LEDGER, on every run: unpaused physics time, how much of it the bot stood still
	// (Player.IsStandingStill - no movement input), and the Brazier heat and Taproot stance that
	// earned. Exists so "a bot that holds its ground makes the Brazier absurd" is a number, not a
	// worry. Measured: --immortal stands still about a tenth of the time (it walks to orbs), the
	// kiting bot never, --stand-still always.
	private double ledgerSeconds, ledgerStill, ledgerHeat, ledgerFullHeat, ledgerRooted;

	// Mirrors the caps in MainMenu.BuildUpgradeDefinitions (the shop has no catalog of its own to read).
	private static readonly (string Id, int Cap)[] FullShopCaps =
	{
		("damage", 8), ("recovery", 8), ("cooldowns", 8), ("area", 7), ("attack_speed", 8),
		("duration", 6), ("amount", 4), ("movespeed", 8), ("magnet", 7), ("growth", 6), ("greed", 8),
		("extra_lives", 3), ("vitality", 10), ("luck", 20), ("crit_chance", 20),
	};
	// THE DROP LEDGER: every pickup that entered the world, by kind, in five-minute windows - the
	// drop table as it actually played, so "the speed potions spawn a lot" is a count rather than an
	// impression (.ai/passives-and-items.md, section 8). Orbs and the bonus pickups are parented to
	// the current scene (this node, under the harness), chests to the game, so both are watched.
	// Counted on entering the tree: a value set before add_child is already there, which is how
	// Enemy.DropXp and TryDropBonusItem build their drops.
	private const float DropWindowSeconds = 300f;
	private readonly SortedDictionary<string, List<int>> dropsByKind = new();
	private readonly SortedDictionary<string, int> chestsBySource = new();
	private long xpDropped;
	private int chestsOpenedByBot;
	private int minibossesEntered, minibossesKilled;

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

	// The bot deals its own chests, by the run's rule (ChestItemCatalog.ShownOfferFade).
	private readonly ChestOfferMemory chestOfferMemory = new();
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
			else if (arg == "--seek-ascensions") SeekAscensions = true;
			else if (arg == "--policy=ordinary") OrdinaryPolicy = true;
			else if (arg == "--full-shop") FullShop = true;
			else if (arg == "--all-unlocked") AllUnlocked = true;
			else if (arg == "--immortal") Immortal = true;
			else if (arg == "--check-feet") CheckFeet = true;
			else if (arg.StartsWith("--sample-offers=")) SampleOffers = int.Parse(arg.Substring(16));
			else if (arg.StartsWith("--xp-first=")) XpFirst = int.Parse(arg.Substring(11));
			else if (arg.StartsWith("--xp-step=")) XpStep = int.Parse(arg.Substring(10));
			else if (arg.StartsWith("--stage=")) StageIndex = int.Parse(arg.Substring(8));
			else if (arg.StartsWith("--spell=")) SpellId = arg.Substring(8).Trim();
			else if (arg == "--solo") Solo = true;
			else if (arg.StartsWith("--branch4=")) Branch4 = arg.Substring(10).Trim();
			else if (arg.StartsWith("--branch8=")) Branch8 = arg.Substring(10).Trim();
			else if (arg == "--force-ascension") ForceAscension = true;
			else if (arg.StartsWith("--partner=")) Partner = arg.Substring(10).Trim();
			else if (arg.StartsWith("--enemy-hp=")) Node2DGame.HarnessEnemyHealthScale = float.Parse(arg.Substring(11), System.Globalization.CultureInfo.InvariantCulture);
			// No navigation window on an open stage: the code as it was before open stages had one,
			// for a same-build cost comparison (MazeNavigation.BuildOnOpenStages).
			else if (arg == "--no-open-nav") MazeNavigation.BuildOnOpenStages = false;
			// No status marks, tints or slowed walks (StatusEffectLayer.Disabled), for a same-build
			// cost A/B against --status-all. The debuffs themselves still land.
			else if (arg == "--no-status-fx") StatusEffectLayer.Disabled = true;
			else if (arg == "--explore") Explore = true;
			else if (arg.StartsWith("--walk=")) WalkBearingDegrees = float.Parse(arg.Substring(7), System.Globalization.CultureInfo.InvariantCulture);
			else if (arg.StartsWith("--upgrade-chance=")) UpgradeChance = float.Parse(arg.Substring(17), System.Globalization.CultureInfo.InvariantCulture);
			else if (arg.StartsWith("--give-relics=")) giveRelics = arg.Substring(14).Split(',', StringSplitOptions.RemoveEmptyEntries);
			else if (arg.StartsWith("--grant-boons=")) grantBoons = arg.Substring(14).Split(',', StringSplitOptions.RemoveEmptyEntries);
			else if (arg == "--stand-still") StandStill = true;
			else if (arg == "--aim-facing") AimFacing = true;
			else if (arg.StartsWith("--circle=")) CircleRadius = float.Parse(arg.Substring(9), System.Globalization.CultureInfo.InvariantCulture);
			else if (arg == "--survivor") Survivor = true;
			else if (arg == "--no-orbs") NoOrbs = true;
			else if (arg == "--log-hits") LogHits = true;
			else if (arg.StartsWith("--hp-scale=")) HpScale = float.Parse(arg.Substring(11), System.Globalization.CultureInfo.InvariantCulture);
			else if (arg.StartsWith("--xp-growth=")) gameOverrides["XpGrowthPerPaceMinute"] = float.Parse(arg.Substring(12), System.Globalization.CultureInfo.InvariantCulture);
			else if (arg == "--no-swarm") gameOverrides["EnableSwarmPressure"] = false;
			else if (arg.StartsWith("--pick-every=")) PickEvery = Math.Max(1, int.Parse(arg.Substring(13)));
			// A brand-new save, in memory, with saving off: every discovery still out in the world, so
			// the chapter hints (StageHintDirector) play on their pace schedule - an --all-unlocked
			// save owns them all and hears none. The bot never meets the event without --explore.
			else if (arg == "--fresh-save") FreshSave = true;
			// A Boss Rush (issue #12) instead of a chapter: the run picks its own arena
			// (BossRushCatalog), so --stage is ignored. Give it --minutes past the finale - the clock
			// the bot reads is real time, and every fight adds to it. Each arrival and fall is logged
			// as a BOSS RUSH line by the run itself.
			else if (arg == "--boss-rush") Global.BossRush = true;
		}

		// Immune to the pause. A level-up pauses the tree, and a driver that pauses with it can
		// never make the choice that would unpause it.
		ProcessMode = ProcessModeEnum.Always;
		StatusEffectLayer.MeasureCost = true;
		StatusEffectLayer.ResetPerf();

		GD.Print($"HARNESS minutes={Minutes} timescale={TimeScale} character={CharacterIndex} seed={Seed}");

		GD.Seed((ulong)Seed);
		chestRng.Seed = (ulong)(Seed * 7919 + 17);
		Global.SelectedCharacterIdx = CharacterIndex;
		if (!string.IsNullOrEmpty(SpellId))
		{
			Global.TestWizardStartingSpellId = SpellId;
			GD.Print($"HARNESS starting spell '{SpellId}' (read only by the Test Wizard){(Solo ? ", solo" : "")}");
		}
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
				// --full-shop: the rest of the Arcane Codex too, at the caps MainMenu.BuildUpgradeDefinitions
				// registers. Sage Spiral (growth) is the one that moves the level clock.
				if (FullShop)
				{
					foreach (var (id, cap) in FullShopCaps)
						data.ArcaneUpgradeLevels[id] = cap;
				}
				saveManager.ReplaceInMemory(data);
			}
		}

		if (FreshSave && !AllUnlocked && GetNodeOrNull<SaveManager>("/root/SaveManager") is SaveManager fresh)
		{
			SaveManager.WritesDisabled = true;
			fresh.ReplaceInMemory(new SaveData());
		}

		if (Explore || giveRelics.Length > 0 || grantBoons.Length > 0)
			SaveManager.WritesDisabled = true;

		game = ResourceLoader.Load<PackedScene>("res://scenes/node_2d_game.tscn").Instantiate();
		// --xp-growth=X and --no-swarm: set by name, so the flag is simply refused (and said so) on a
		// tree from before the property existed rather than failing to build there.
		foreach (var (name, value) in gameOverrides)
		{
			System.Reflection.PropertyInfo prop = typeof(Node2DGame).GetProperty(name);
			if (prop == null)
				GD.PrintErr($"HARNESS this build has no Node2DGame.{name}; override ignored");
			else
			{
				prop.SetValue(game, value);
				GD.Print($"HARNESS Node2DGame.{name} = {value}");
			}
		}
		game.ChildEnteredTree += OnGameChildEntered;
		ChildEnteredTree += CountDrop;
		AddChild(game);
	}

	private void OnGameChildEntered(Node node)
	{
		CountDrop(node);
		if (node is not Enemy enemy)
			return;
		spawnedByType[enemy.EnemyType] = spawnedByType.TryGetValue(enemy.EnemyType, out int n) ? n + 1 : 1;
		// Elites, event elites, the Warden, the ghost and the boss: everything a chest can come from.
		// IsMiniBoss is set before AddChild by every spawner that sets it at all.
		if (enemy.IsMiniBoss)
		{
			minibossesEntered++;
			enemy.TreeExiting += () => { if (!finished && enemy.IsDying) minibossesKilled++; };
		}
		// The pace ledger's kill and spawn counts. A recycled enemy never leaves the tree, so a body
		// counts once each way; a kill is booked when the corpse goes, a death animation late.
		ledgerSpawned++;
		enemy.TreeExiting += () => { if (!finished && enemy.IsDying) ledgerKills++; };
	}

	private void CountDrop(Node node)
	{
		if (finished)
			return;
		string kind = node switch
		{
			XPOrb orb => orb.Value >= orb.LargeOrbValue ? "xp orb, large" : orb.Value >= orb.MediumOrbValue ? "xp orb, medium" : "xp orb, small",
			HealthPickup => "health flask",
			BuffItem => "speed potion",
			LevelUpPickup => "level-up pickup",
			ChestReward => "chest",
			_ => null,
		};
		if (kind == null)
			return;
		if (node is XPOrb counted)
			xpDropped += counted.Value;
		if (node is ChestReward chest)
		{
			string source = string.IsNullOrEmpty(chest.Source) ? "unlabelled" : chest.Source;
			chestsBySource[source] = chestsBySource.TryGetValue(source, out int c) ? c + 1 : 1;
			chest.TreeExiting += () => { if (!finished && chest.Opened) chestsOpenedByBot++; };
		}

		if (!dropsByKind.TryGetValue(kind, out List<int> windows))
			dropsByKind[kind] = windows = new List<int>();
		int window = (int)(elapsed / DropWindowSeconds);
		while (windows.Count <= window)
			windows.Add(0);
		windows[window]++;
	}

	private IEnumerable<string> DropLedger()
	{
		float minutes = Math.Max(0.01f, elapsed / 60f);
		int windowCount = Math.Max(1, (int)Math.Ceiling(elapsed / DropWindowSeconds));
		string header = string.Join(" / ", Enumerable.Range(0, windowCount).Select(w => $"{w * 5}-{w * 5 + 5}"));
		yield return $"  drops        : spawned, per minute over the run, and per 5-minute window ({header} min)";
		foreach (var pair in dropsByKind)
		{
			int total = pair.Value.Sum();
			var cells = Enumerable.Range(0, windowCount).Select(w => w < pair.Value.Count ? pair.Value[w] : 0);
			yield return $"    {pair.Key,-16} {total,6}  {total / minutes,6:0.00}/min   {string.Join(" / ", cells)}";
		}
		yield return $"    xp dropped       {xpDropped,6}  {xpDropped / minutes,6:0.0}/min";
		yield return $"  chests       : {(chestsBySource.Count == 0 ? "none" : string.Join(", ", chestsBySource.Select(p => $"{p.Key} {p.Value}")))};"
			+ $" {chestsOpenedByBot} opened by the bot";
		yield return $"  minibosses   : {minibossesEntered} entered (elites, event elites, Wardens, ghosts, boss), {minibossesKilled} killed";
	}

	public override void _ExitTree()
	{
		// Left raised, a scene change would carry it into whatever loads next.
		Engine.TimeScale = 1.0f;
		Node2DGame.HarnessEnemyHealthScale = 1f;
		if (!string.IsNullOrEmpty(SpellId))
			Global.TestWizardStartingSpellId = string.Empty;
		MazeNavigation.BuildOnOpenStages = true;
		StatusEffectLayer.Disabled = false;
		StatusEffectLayer.MeasureCost = false;
		foreach (string a in MoveActions)
			Input.ActionRelease(a);
	}

	// Timed as a section of its own under --profile, so what the bot costs is never read as the game.
	public override void _PhysicsProcess(double delta)
	{
		var perfStart = FrameProfiler.Begin();
		PhysicsTick(delta);
		FrameProfiler.End(FrameProfiler.Section.Harness, perfStart);
	}

	private void PhysicsTick(double delta)
	{
		// The survivor steers on physics steps, not rendered frames: at the 5-8 fps a full arena runs
		// at headless on a shared machine, a frame is a quarter second of game time, and a bot that
		// reacted once a frame died to whatever arrived in between. Every second step is 30 Hz of game
		// time at any frame rate - a reaction time a person beats, not one the machine sets.
		if (Survivor && !finished && player != null && IsInstanceValid(player) && !player.IsDead && !GetTree().Paused
			&& (survivorPhysicsStep++ & 1) == 0)
			DriveSurvivor();
		LedgerStandStill((float)delta);
		SampleCap();
		if (!float.IsNaN(WalkBearingDegrees))
			MeasureCrowd((float)delta);
		MeasureOverlap((float)delta);
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

	// --check-feet also checks the stage hazards the run actually placed (.ai/terrain.md section 6):
	// how many, per painted chunk (the density), the closest two (StageHazardSeparation holds across
	// chunk borders too), how many have decor or a prop within 64 px (nothing stands on a trap), and on
	// the maze how many stand less than half a corridor from a wall - asked of the terrain, by sampling
	// rings round each hazard, not of the code that put it there. Zero is the answer "every trap is in
	// the middle of its path".
	private string HazardSummary()
	{
		if (Node2DGame.Current?.Terrain is not TerrainStreamer terrain)
			return "hazards: no streamed terrain";
		var spots = new List<Vector2>();
		foreach (Node node in GetTree().GetNodesInGroup("stage_hazards"))
			if (node is Node2D hazard && IsInstanceValid(hazard))
				spots.Add(hazard.GlobalPosition);
		bool maze = MazeNavigation.Active is { IsMaze: true };
		float need = LevelGenerator.MazeCorridorTiles / 2f * terrain.TilePixels - 1f;
		int offCentre = 0;
		if (maze)
		{
			foreach (Vector2 p in spots)
			{
				bool touches = terrain.IsWallAtWorld(p);
				for (int ring = 1; ring <= 3 && !touches; ring++)
				{
					float r = need * ring / 3f;
					for (int k = 0; k < 64 && !touches; k++)
					{
						float a = k * Mathf.Tau / 64f;
						touches = terrain.IsWallAtWorld(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
					}
				}
				if (touches)
					offCentre++;
			}
		}
		float closest = float.MaxValue;
		for (int i = 0; i < spots.Count; i++)
			for (int j = i + 1; j < spots.Count; j++)
				closest = Mathf.Min(closest, spots[i].DistanceTo(spots[j]));
		int underProps = 0;
		var props = new List<Vector2>();
		foreach (string group in new[] { "decor_props", "curated_props" })
			foreach (Node node in GetTree().GetNodesInGroup(group))
				if (node is Node2D prop)
					props.Add(prop.GlobalPosition);
		foreach (Vector2 p in spots)
			foreach (Vector2 q in props)
				if (p.DistanceTo(q) < 64f) { underProps++; break; }
		// On the maze, how many stand in a trapped hallway (Node2DGame.ExtendMazeTrapRow): a cell's pitch
		// from another along a straight line, which only a row puts them.
		int inRows = 0;
		if (maze)
		{
			float pitch = LevelGenerator.MazeCellTiles * terrain.TilePixels;
			foreach (Vector2 p in spots)
				foreach (Vector2 q in spots)
				{
					Vector2 d = q - p;
					if (Mathf.Abs(d.Length() - pitch) < 1f && (Mathf.IsZeroApprox(d.X) || Mathf.IsZeroApprox(d.Y))) { inRows++; break; }
				}
		}
		int painted = Math.Max(1, terrain.PaintedChunkCount);
		return $"hazards: {spots.Count} placed over {terrain.PaintedChunkCount} painted chunks ({spots.Count / (float)painted:0.00} a chunk),"
			+ $" closest two {(spots.Count > 1 ? closest.ToString("0") : "-")} px, {underProps} with decor or a prop within 64 px"
			+ (maze ? $", {offCentre} less than {need + 1:0} px from a wall, {inRows} in a hallway row" : "")
			+ $"; {StageHazard.BitesLanded} bites landed on the player's boots";
	}

	// Game seconds, unpaused only: a level-up card freezes the heat and the stance, and counting those
	// frames would credit the bot with standing still while it was reading.
	private void LedgerStandStill(float delta)
	{
		if (finished || player == null || !IsInstanceValid(player) || player.IsDead || GetTree().Paused)
			return;
		ledgerSeconds += delta;
		if (player.IsStandingStill)
			ledgerStill += delta;
		ledgerHeat += player.BrazierHeat * delta;
		if (player.BrazierHeat >= 0.999f)
			ledgerFullHeat += delta;
		if (player.IsStanceRooted)
			ledgerRooted += delta;
	}

	private string StandStillSummary()
	{
		double play = Math.Max(0.001, ledgerSeconds);
		double meanHeat = ledgerHeat / play;
		string brazier = player != null && IsInstanceValid(player) && player.IsBrazierLit
			? $"Brazier mean heat {meanHeat:0.00} (+{meanHeat * Player.BrazierDamageAtFullHeat * 100:0.0}% spell damage, +{meanHeat * Player.BrazierAreaAtFullHeat * 100:0.0}% area on average), full {100 * ledgerFullHeat / play:0}% of play"
			: "Brazier not held";
		string stance = player != null && IsInstanceValid(player) && player.HasStanceBoon
			? $"Taproot rooted {100 * ledgerRooted / play:0}% of play"
			: "Taproot not held";
		return $"stand still  : still {100 * ledgerStill / play:0}% of {play / 60:0.0} min of play; {brazier}; {stance}";
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
		bool blockerAhead = false;
		string firstOrdinary = null;
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy)
				continue;
			Vector2 offset = enemy.GlobalPosition - position;
			if (offset.LengthSquared() < CrowdContactDistance * CrowdContactDistance && offset.Dot(heading) > 0f)
			{
				touching++;
				if (enemy is Enemy e && e.BlocksPlayer)
					blockerAhead = true;
				else
					firstOrdinary ??= $"{enemy.GetType().Name} {System.IO.Path.GetFileName(enemy.SceneFilePath)} layer {(enemy as CollisionObject2D)?.CollisionLayer} at {offset.Length():0} px";
			}
		}

		crowdSteps++;
		if (touching > 0)
		{
			crowdContactSteps++;
			crowdContactRatioSum += ratio;
			crowdContactEnemySum += touching;
			if (ratio < 0.25f)
			{
				crowdPinnedSteps++;
				// An elite, miniboss or boss is meant to stop him (Enemy.BlocksPlayer); an ordinary body
				// never is (.ai/enemy-behaviour.md, 8 item 10), so a pin with none ahead is the bug.
				if (!blockerAhead)
				{
					crowdPinnedByOrdinarySteps++;
					if (crowdPinnedByOrdinarySteps <= 5)
					{
						// What the player's own body met on its last slide - the one thing that can stop him.
						var met = new System.Text.StringBuilder();
						for (int i = 0; i < player.GetSlideCollisionCount(); i++)
						{
							GodotObject collider = player.GetSlideCollision(i).GetCollider();
							met.Append(collider is Node n ? $" {n.GetType().Name}:{n.Name}" : $" {collider?.GetType().Name}");
						}
						// And the feet, which meet only the ground (water, walls, trunks) in the second pass.
						if (player.GetNodeOrNull<CharacterBody2D>("Feet") is CharacterBody2D feet)
							for (int i = 0; i < feet.GetSlideCollisionCount(); i++)
								met.Append($" feet:{(feet.GetSlideCollision(i).GetCollider() as Node)?.Name}");
						GD.Print($"HARNESS pinned by ordinary enemies at {position.X:0},{position.Y:0}: {touching} ahead, first {firstOrdinary}, speed ratio {ratio:0.00}; met:{(met.Length > 0 ? met.ToString() : " nothing")}; speed {player.Speed:0}");
					}
				}
			}
			else if (ratio < 0.6f)
				crowdSlowSteps++;
		}
		else
		{
			crowdFreeRatioSum += ratio;
		}
	}

	// Enemies sitting ON the player: a centre inside OverlapDistance of his, held. Enemy.MinPlayerSeparation
	// pushes a body out from 27 px at 230 px/s, so a body that stays inside 18 px for half a second is one
	// the separation is not freeing - "enemies getting stuck on the player" (2026-10-27). Every run reports
	// it: how long bodies spent on him, the longest one stay, and how many stays passed half a second.
	private const float OverlapDistance = 18f;
	private readonly Dictionary<ulong, float> overlapSeconds = new();
	private readonly List<ulong> overlapGone = new();
	private float overlapTotal, overlapLongest;
	private int overlapStuck, overlapStuckBlockers;
	private string overlapLongestWho = "-";

	private void MeasureOverlap(float delta)
	{
		if (finished || player == null || !IsInstanceValid(player) || player.IsDead || GetTree().Paused)
			return;
		Vector2 at = player.GlobalPosition;
		overlapGone.Clear();
		foreach (ulong id in overlapSeconds.Keys)
			overlapGone.Add(id);
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy enemy || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			if (enemy.GlobalPosition.DistanceSquaredTo(at) > OverlapDistance * OverlapDistance)
				continue;
			ulong id = enemy.GetInstanceId();
			overlapGone.Remove(id);
			overlapSeconds.TryGetValue(id, out float held);
			float now = held + delta;
			overlapSeconds[id] = now;
			overlapTotal += delta;
			if (held < 0.5f && now >= 0.5f)
			{
				overlapStuck++;
				if (enemy.BlocksPlayer)
					overlapStuckBlockers++;
			}
			if (now > overlapLongest)
			{
				overlapLongest = now;
				overlapLongestWho = $"{System.IO.Path.GetFileNameWithoutExtension(enemy.SceneFilePath)}{(enemy.BlocksPlayer ? " (blocker)" : "")}";
			}
		}
		foreach (ulong id in overlapGone)
			overlapSeconds.Remove(id);
	}

	private string OverlapSummary() =>
		$"on player  : {overlapTotal:0.0} enemy-seconds within {OverlapDistance:0} px of his centre; {overlapStuck} stays past 0.5 s"
		+ $" ({overlapStuckBlockers} by blockers); longest {overlapLongest:0.00} s ({overlapLongestWho})";

	private string CrowdSummary()
	{
		int free = Math.Max(1, crowdSteps - crowdContactSteps);
		int contact = Math.Max(1, crowdContactSteps);
		return $"crowd: {crowdContactSteps} of {crowdSteps} walking steps with an enemy ahead in contact"
			+ $" ({100f * crowdContactSteps / Math.Max(1, crowdSteps):0.0}%), {crowdContactEnemySum / (double)contact:0.00} touching on average;"
			+ $" speed ratio {crowdContactRatioSum / contact:0.00} in contact vs {crowdFreeRatioSum / free:0.00} clear;"
			+ $" pinned (<25%) {crowdPinnedSteps} ({100f * crowdPinnedSteps / contact:0.0}% of contact steps, {crowdPinnedByOrdinarySteps} with no blocker ahead),"
			+ $" slowed (25-60%) {crowdSlowSteps} ({100f * crowdSlowSteps / contact:0.0}%); {stuckEvents} stuck checks";
	}

	public override void _Process(double delta)
	{
		var perfStart = FrameProfiler.Begin();
		ProcessTick(delta);
		FrameProfiler.End(FrameProfiler.Section.Harness, perfStart);
	}

	private void ProcessTick(double delta)
	{
		if (finished)
			return;

		// delta is already scaled by Engine.TimeScale, so this is game time, which is what the
		// run length should be measured in.
		elapsed += (float)delta;
		ledgerFrames++;

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
			foreach (string relic in giveRelics)
				player.AddChestItem(relic.Trim());
			foreach (string boon in grantBoons)
				if (!player.TryAddBoon(boon.Trim()))
					GD.PrintErr($"HARNESS could not grant boon '{boon}'");
			if (giveRelics.Length > 0 || grantBoons.Length > 0)
				GD.Print($"HARNESS handed relics [{string.Join(", ", player.GetOwnedChestItems())}], boons [{string.Join(", ", player.GetOwnedBoonIds())}]");
			if (LogHits)
				player.DamageTaken += LogHit;
			shadowBaseHp = player.MaxHP;
			if (HpScale > 0f && Math.Abs(HpScale - 1f) > 0.001f)
			{
				player.MaxHP = Math.Max(1, Mathf.RoundToInt(player.MaxHP * HpScale));
				player.CurrentHP = player.MaxHP;
				GD.Print($"HARNESS hp scaled x{HpScale:0.##} to {player.MaxHP}");
			}
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
				GD.Print("HARNESS " + HazardSummary());
			}
		}

		// A huge pool rather than a top-up: at 4x several physics steps land between two _Process
		// calls, and a surrounded bot could drain a normal pool in between.
		if (Immortal) { player.MaxHP = Math.Max(player.MaxHP, 100000); player.CurrentHP = player.MaxHP; }
		if (SeekApotheosis || SeekAscensions) TrackTier();
		TrackSpellLevel((float)delta);
		TrackAscensions();
		TickPaceLedger();
		if (OpensChests) PrepareNearbyChests();
		AutoPickLevelUp();
		DriveMovement();

		if (elapsed >= Minutes * 60f || player.IsDead)
			Finish(player.IsDead ? "died" : "reached the clock");
	}

	// --- the pace ledger: one row per game minute --------------------------------------
	// What the level clock and the swarm were doing minute by minute, so a level-up cadence ("a
	// level every 16 s at minute 5, every 50 s at minute 14") and a falling-behind run's crowd are
	// numbers rather than impressions (.ai/run-pacing.md, section 10). XP is what the player has
	// banked - every level's cost plus the bar - so it is XP collected, after every multiplier.
	private sealed class LedgerRow
	{
		public int Minute, Level, LevelUps, Kills, Spawned, Alive, Near, DamageTaken, Hp, MaxHp, Folds, SentRound;
		public long Xp, XpGained;
		public float Pressure;
		// The share of trickle ticks in the minute that found the field full (Node2DGame.NoteTrickleTick).
		public float Choked;
		// The share of the minute the field sat at the trickle's cap (MaxEnemies less the formation
		// reserve, 131), sampled ten times a game second - the same measure on a build without the swarm.
		public float AtCap;
		// Physics game time over process game time for the minute. The spawner ticks in _Process and
		// the fight (movement, every spell) in _PhysicsProcess, and Godot runs at most 8 physics steps
		// a frame, so below 60 x timescale / 8 fps the fight falls behind the spawner. Under about
		// 0.95 the minute measured a slow machine, not the game: discard it.
		public float Clock;
		public float FrameMs;
	}

	private readonly List<LedgerRow> paceLedger = new();
	private int ledgerKills, ledgerSpawned;
	private int ledgerLastKills, ledgerLastSpawned, ledgerLastDamage, ledgerLastLevel = 1;
	private long ledgerLastXp;
	private ulong ledgerLastPhysicsFrames;
	private int ledgerLastFolds, ledgerLastSentRound, ledgerLastTicks, ledgerLastChoked;
	private int capSamples, capSamplesFull, capStep;

	// Ten samples a game second, from _PhysicsProcess, of whether the field is at the trickle's cap.
	private void SampleCap()
	{
		if (finished || GetTree().Paused || (capStep++ % 6) != 0 || Node2DGame.Current is not Node2DGame g)
			return;
		capSamples++;
		if (GetTree().GetNodeCountInGroup("enemies") >= g.MaxEnemies - g.FormationMaxSize)
			capSamplesFull++;
	}
	// Near is "on screen or nearly": the 1280x720 canvas at the 0.85 zoom shows about 750 x 420
	// either side of the player, so 600 is the crowd the player is actually standing in.
	private const float LedgerNearRadius = 600f;

	// The swarm's numbers are read by name, so this one harness file also builds and runs against a
	// tree from before the swarm existed - which is how the before half of a before/after is measured
	// with the same bot. Absent, a stat reads as zero and a speed scale as one.
	// GAME MILLISECONDS PER FRAME, the measure of how faithful a run is. Spells move and hit in
	// _Process, so a frame that covers a quarter second of game time skips some of their hits: at
	// timescale 2 a full field took this to 265 ms here (Godot runs at most eight physics steps a
	// frame), and the kills, so the XP, so the level clock all came out low - and lowest for the
	// weakest builds, which sit in the fullest fields. Godot's --fixed-fps N (an engine flag, before
	// the --) holds every frame at 1000/N ms of game time whatever the machine does. A minute over
	// about 35 ms is a minute the machine, not the game, decided (.ai/run-pacing.md 10.5).
	private int ledgerFrames;

	private static T SwarmStat<T>(string name) where T : struct
	{
		Node2DGame g = Node2DGame.Current;
		object value = g == null ? null : typeof(Node2DGame).GetProperty(name)?.GetValue(g);
		return value is T typed ? typed : default;
	}

	private static readonly Func<Enemy, float> SwarmScaleOf = BuildSwarmScaleReader();

	private static Func<Enemy, float> BuildSwarmScaleReader()
	{
		System.Reflection.MethodInfo getter = typeof(Enemy).GetProperty("SwarmSpeedScale")?.GetGetMethod();
		return getter != null
			? (Func<Enemy, float>)Delegate.CreateDelegate(typeof(Func<Enemy, float>), getter)
			: _ => 1f;
	}

	private long BankedXp()
	{
		long xp = player.CurrentXP;
		for (int l = 1; l < player.CurrentLevel; l++)
			xp += player.CalculateXPForLevel(l);
		return xp;
	}

	private void TickPaceLedger()
	{
		if (player == null || !IsInstanceValid(player))
			return;
		int minute = paceLedger.Count + 1;
		if (elapsed < minute * 60f)
			return;

		int near = 0, alive = 0;
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy)
				continue;
			alive++;
			if (enemy.GlobalPosition.DistanceSquaredTo(player.GlobalPosition) < LedgerNearRadius * LedgerNearRadius)
				near++;
		}
		long xp = BankedXp();
		var row = new LedgerRow
		{
			Minute = minute,
			Level = player.CurrentLevel,
			LevelUps = player.CurrentLevel - ledgerLastLevel,
			Xp = xp,
			XpGained = xp - ledgerLastXp,
			Kills = ledgerKills - ledgerLastKills,
			Spawned = ledgerSpawned - ledgerLastSpawned,
			Alive = alive,
			Near = near,
			DamageTaken = GameStats.RunDamageTaken - ledgerLastDamage,
			Hp = player.CurrentHP,
			MaxHp = player.MaxHP,
			Pressure = SwarmStat<float>("SwarmPressure"),
			Folds = SwarmStat<int>("SwarmSwellsThisRun") - ledgerLastFolds,
			SentRound = SwarmStat<int>("SwarmTrailRecyclesThisRun") - ledgerLastSentRound,
			Choked = (SwarmStat<int>("SwarmTrickleChokedThisRun") - ledgerLastChoked)
				/ (float)Math.Max(1, SwarmStat<int>("SwarmTrickleTicksThisRun") - ledgerLastTicks),
			AtCap = capSamplesFull / (float)Math.Max(1, capSamples),
			FrameMs = 60000f / Math.Max(1, ledgerFrames),
			Clock = (Engine.GetPhysicsFrames() - ledgerLastPhysicsFrames) * TimeScale
				/ (float)Engine.PhysicsTicksPerSecond / 60f,
		};
		ledgerLastPhysicsFrames = Engine.GetPhysicsFrames();
		ledgerLastFolds = SwarmStat<int>("SwarmSwellsThisRun");
		ledgerLastSentRound = SwarmStat<int>("SwarmTrailRecyclesThisRun");
		ledgerLastTicks = SwarmStat<int>("SwarmTrickleTicksThisRun");
		ledgerLastChoked = SwarmStat<int>("SwarmTrickleChokedThisRun");
		capSamples = capSamplesFull = 0;
		ledgerFrames = 0;
		paceLedger.Add(row);
		ledgerLastLevel = player.CurrentLevel;
		ledgerLastXp = xp;
		ledgerLastKills = ledgerKills;
		ledgerLastSpawned = ledgerSpawned;
		ledgerLastDamage = GameStats.RunDamageTaken;
		GD.Print($"HARNESS MINUTE m={row.Minute} level={row.Level} ups={row.LevelUps} xp={row.Xp} +xp={row.XpGained}"
			+ $" kills={row.Kills} spawned={row.Spawned} alive={row.Alive} near={row.Near} dmg={row.DamageTaken}"
			+ $" hp={row.Hp}/{row.MaxHp} atCap={row.AtCap:0.00} pressure={row.Pressure:0.00} choked={row.Choked:0.00} folds={row.Folds} round={row.SentRound} clock={row.Clock:0.00} frameMs={row.FrameMs:0} fps={Engine.GetFramesPerSecond()}");
	}

	// THE SHADOW HEALTH LEDGER, for --survivor --immortal: the bot plays the whole run, and the ledger
	// says when it would have died had it started with H times the wizard's health. Damage is the
	// minute's real damage taken (after armour and shields); regeneration is the base 0.25 a second
	// only, so a death here is if anything early. One run answers for every handicap, and it reaches
	// 15:00 either way, so the swarm's whole effect on it is in the same ledger.
	private int shadowBaseHp;
	private static readonly float[] ShadowScales = { 3f, 5f, 10f, 20f, 40f };

	private string ShadowDeathLine()
	{
		List<(float Scale, float Hp, float DiedAt)> deaths = ShadowDeaths();
		return deaths.Count == 0 ? "" : "  shadow death : " + string.Join("; ", deaths.Select(d =>
			$"x{d.Scale:0} ({d.Hp:0} hp): {(d.DiedAt < 0f ? "survives" : $"dies at {d.DiedAt:0.0}")}"));
	}

	private string ShadowSummary()
	{
		List<(float Scale, float Hp, float DiedAt)> deaths = ShadowDeaths();
		return deaths.Count == 0 ? "" : " shadow=[" + string.Join(",", deaths.Select(d =>
			$"x{d.Scale:0}:{(d.DiedAt < 0f ? "-" : $"{d.DiedAt:0.0}")}")) + "]";
	}

	private List<(float Scale, float Hp, float DiedAt)> ShadowDeaths()
	{
		var cells = new List<(float, float, float)>();
		if (!Survivor || !Immortal || paceLedger.Count == 0 || shadowBaseHp <= 0)
			return cells;
		foreach (float scale in ShadowScales)
		{
			float hp0 = shadowBaseHp * scale;
			float hp = hp0;
			float diedAt = -1f;
			foreach (LedgerRow r in paceLedger)
			{
				float regen = 0.25f * 60f;
				float next = hp - r.DamageTaken + regen;
				if (next <= 0f)
				{
					float net = Math.Max(1f, r.DamageTaken - regen);
					diedAt = r.Minute - 1 + Mathf.Clamp(hp / net, 0f, 1f);
					break;
				}
				hp = Math.Min(hp0, next);
			}
			cells.Add((scale, hp0, diedAt));
		}
		return cells;
	}

	private IEnumerable<string> PaceLedgerLines()
	{
		if (paceLedger.Count == 0)
			yield break;
		string shadow = ShadowDeathLine();
		if (shadow.Length > 0)
			yield return shadow;
		yield return "  pace ledger  : per game minute - level at its end, level-ups in it, mean gap between them,";
		yield return "                 XP banked in it, kills, spawns, enemies alive / within 600 px, damage taken, swarm pressure at";
		yield return "                 its end, bodies folded into others and sent round from behind (Node2DGame.SwarmPressure.cs),";
		yield return "                 and clock (physics time per game minute; under 0.95 the machine, not the game, set the minute)";
		yield return "                 and frame ms (game time per frame: 33 under --fixed-fps 30; over ~35 the minute is suspect)";
		yield return "    min  level  ups   gap s   +xp    kills  spawned  alive  near   dmg  at cap  choked  pressure  folds  round  clock  frame ms";
		foreach (LedgerRow r in paceLedger)
		{
			string gap = r.LevelUps > 0 ? $"{60f / r.LevelUps,5:0}" : "    -";
			yield return $"    {r.Minute,3}  {r.Level,5}  {r.LevelUps,3}   {gap}  {r.XpGained,5}  {r.Kills,6}  {r.Spawned,7}  {r.Alive,5}  {r.Near,4}  {r.DamageTaken,4}  {r.AtCap,6:0.00}  {r.Choked,6:0.00}  {r.Pressure,8:0.00}  {r.Folds,5}  {r.SentRound,5}  {r.Clock,5:0.00}  {r.FrameMs,8:0}";
		}
	}

	// --- --spell: the opener's damage, level by level ----------------------------------
	// Whatever GameStats attributed to the spell since the last frame belongs to the level it held
	// then. Paused frames (a level-up card) are not time the spell could fire in, so they count
	// for nothing; a spell that lands between two frames is booked to the level it had before.
	private void TrackSpellLevel(float delta)
	{
		if (string.IsNullOrEmpty(SpellId) || player == null)
			return;
		if (!partnerGiven && !string.IsNullOrEmpty(Partner))
		{
			partnerGiven = true;
			GD.Print($"HARNESS partner {Partner}: {(player.TryAddOrLevelSpell(Partner) ? "equipped" : "refused")}");
		}
		GameStats.DamageBySource.TryGetValue(SpellId, out int dealt);
		int gained = dealt - lastSpellDamage;
		lastSpellDamage = dealt;
		int allDealt = GameStats.RunDamageDealt;
		int allGained = allDealt - lastAllDamage;
		lastAllDamage = allDealt;
		SpellData spell = player.GetEquippedSpells().FirstOrDefault(s => s != null && s.Id == SpellId);
		if (spell == null)
			return;
		int level = Math.Clamp(spell.CurrentLevel, 0, levelSeconds.Length - 1);
		levelDamage[level] += gained;
		levelAllDamage[level] += allGained;
		// Through the same entry point the strip uses, so the branch is applied exactly as a taken
		// Ascension would be - only the offer's gates are skipped.
		if (ForceAscension && Solo && !forcedAscension && !string.IsNullOrEmpty(Branch8)
			&& spell.CurrentLevel == Player.AscensionSpellLevel - 1 && string.IsNullOrEmpty(spell.SelectedLevel8EvolutionId))
		{
			forcedAscension = true;
			bool ok = player.TryAddOrLevelSpell($"{SpellId}:{Branch8}");
			GD.Print($"HARNESS forced ascension {SpellId}:{Branch8} at {elapsed / 60f:0.00} min -> {(ok ? spell.SelectedLevel8EvolutionId : "refused")}");
		}
		if (!GetTree().Paused)
			levelSeconds[level] += delta;
	}

	private IEnumerable<string> SpellLevelLines()
	{
		if (string.IsNullOrEmpty(SpellId))
			yield break;
		SpellData spell = player?.GetEquippedSpells().FirstOrDefault(s => s != null && s.Id == SpellId);
		yield return $"  {SpellId} by level{(Solo ? $" (solo; {soloSkips} level-ups skipped)" : "")}"
			+ (spell != null ? $"; evolutions: {(string.IsNullOrEmpty(spell.SelectedLevel4EvolutionId) ? "-" : spell.SelectedLevel4EvolutionId)}"
				+ $" / {(string.IsNullOrEmpty(spell.SelectedLevel8EvolutionId) ? "-" : spell.SelectedLevel8EvolutionId)}" : "")
			+ (forcedAscension ? " (level 8 FORCED past its gates)" : "");
		// The last column is everything enemies lost while the spell held that level, DoT ticks
		// included. Only under --solo is that all this spell's; with a loadout it is the whole run's.
		yield return "    level   seconds   attributed      /s      all dmg      /s";
		for (int level = 1; level < levelSeconds.Length; level++)
		{
			if (levelSeconds[level] <= 0.0 && levelDamage[level] == 0)
				continue;
			double perSecond = levelSeconds[level] > 0.5 ? levelDamage[level] / levelSeconds[level] : 0.0;
			double allPerSecond = levelSeconds[level] > 0.5 ? levelAllDamage[level] / levelSeconds[level] : 0.0;
			yield return $"    L{level}  {levelSeconds[level],7:0.0}s  {levelDamage[level],8} dmg  {perSecond,7:0.0}/s  {levelAllDamage[level],8} dmg  {allPerSecond,7:0.0}/s";
		}
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
		if (Explore && !exploreDone && ExploreStep())
			return;

		// --stand-still: never moves at all, the ceiling for anything paid for standing still (the
		// Brazier's heat, Taproot). Pair with --immortal, or the swarm ends it in a minute. Not a
		// player - it gathers only what drifts into its pickup radius - so read its stand-still line,
		// not its level clock.
		if (StandStill)
		{
			foreach (string a in MoveActions)
				Input.ActionRelease(a);
			return;
		}

		// Steered from _PhysicsProcess (see there).
		if (Survivor)
			return;

		// Immortal runs hold their ground. Kiting is how the bot survives, and a bot that cannot die
		// has no reason to - while running from everything means a close-range spell (Cinderbreath)
		// never touches an enemy, which made the Pyromancer look unable to level at all.
		if (Immortal && CircleRadius > 0f)
		{
			DriveCircle();
			return;
		}

		if (Immortal)
		{
			// ...except to pick up XP. A long-range spell kills where the bot is not, and a bot that
			// never walks to the orbs never levels - the Stormcaller sat at level 3 for eleven minutes.
			// The two pick policies open chests as well, because a player does and their relics carry
			// tags (PrepareNearbyChests); the older policies keep walking to orbs only, so their
			// reports stay comparable.
			Vector2? orb = (OpensChests ? NearestChest() : null) ?? NearestXpOrb();
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
			if (AimFacing && elapsed >= nextAimTapAt && NearestEnemyOffset() is Vector2 threat)
			{
				nextAimTapAt = elapsed + AimTapSeconds;
				Vector2 turn = threat.Normalized();
				Press("move_right", "move_left", turn.X);
				Press("move_down", "move_up", turn.Y);
			}
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

	// --circle: the centre is set off to the bot's left on the first step, so it starts on the
	// circle; each step it walks the tangent, steered back toward the circle by how far it has
	// drifted off it (a shove, a blocker).
	private void DriveCircle()
	{
		if (!circleCentred)
		{
			circleCentre = player.GlobalPosition + Vector2.Left * CircleRadius;
			circleCentred = true;
		}
		Vector2 offset = player.GlobalPosition - circleCentre;
		float distance = offset.Length();
		Vector2 radial = distance > 1f ? offset / distance : Vector2.Right;
		Vector2 tangent = new Vector2(-radial.Y, radial.X);
		Vector2 to = (tangent + radial * ((CircleRadius - distance) / CircleRadius) * 2f).Normalized();
		Press("move_right", "move_left", to.X);
		Press("move_down", "move_up", to.Y);
	}

	// --- --survivor: a mortal bot that dodges and collects ------------------------------------
	// Sixteen headings, each scored: how much of the crowd inside its danger radius lies along it
	// (nearer counts far more, a blocker three times an ordinary body, and further out), against how
	// directly it leads to the nearest chest or orb, plus a little of the last heading so it does not
	// dither. It takes the best. With nothing close and nothing to collect it closes on the nearest
	// enemy until that is SurvivorEngageDistance away, so a short-range spell (Cinderbreath) still
	// reaches the front of the pack - a bot that only fled killed eleven enemies in its first minute
	// and died at level 2. A heading whose next step is hazard or blocking ground is penalised, and
	// one it has just failed to move along is barred for a second, which gets it round a tree or a
	// shore. Not clever - no baiting, no reading telegraphs or bolts - which is why --hp-scale exists.
	private const int SurvivorHeadings = 16;
	// A body is a threat inside SurvivorDangerBase plus what it covers in SurvivorDangerLeadSeconds:
	// about 140 px for a bramble, 170 for a wolf. Measured wider (210-250) the bot spent the run
	// stepping back from bodies its spells were about to kill, made a third of the immortal bot's
	// kills, and was overrun by minute three on either build - which measures a coward, not a player.
	private const float SurvivorDangerBase = 110f;
	private const float SurvivorDangerLeadSeconds = 0.45f;
	private const float SurvivorEngageDistance = 230f;
	private readonly float[] survivorDanger = new float[SurvivorHeadings];
	private readonly float[] survivorBlockedUntil = new float[SurvivorHeadings];
	// Enemies within reach of anything worth collecting, gathered by the danger pass so an orb's
	// safety costs no second walk of the group. An orb with a body this close to it is left for later.
	private readonly List<Vector2> survivorNearby = new();
	private const float SurvivorOrbSafety = 120f;

	private bool IsSafeToCollect(Vector2 at, float clearance)
	{
		float sq = clearance * clearance;
		foreach (Vector2 e in survivorNearby)
			if (e.DistanceSquaredTo(at) < sq)
				return false;
		return true;
	}

	private Vector2? NearestSafeOrb(Vector2 p)
	{
		Vector2? best = null;
		float bestDistance = 800f * 800f;
		foreach (Node node in GetTree().GetNodesInGroup(XPOrb.GroupName))
		{
			if (node is not XPOrb orb || orb.IsQueuedForDeletion())
				continue;
			float d = orb.GlobalPosition.DistanceSquaredTo(p);
			if (d < bestDistance && IsSafeToCollect(orb.GlobalPosition, SurvivorOrbSafety))
			{
				bestDistance = d;
				best = orb.GlobalPosition;
			}
		}
		return best;
	}
	private Vector2 survivorLastHeading = Vector2.Zero;
	private Vector2 survivorStuckFrom;
	private float survivorStuckCheckAt;
	private int survivorLastIndex = -1;
	private int survivorPhysicsStep;
	private Vector2 survivorDodge;
	// --hp-scale=X: the survivor's max health times X at the start of the run. The bot dodges far
	// worse than a person, and this says by how much, out loud, rather than hiding it in its
	// steering: the same handicap before and after a change keeps the comparison honest.
	[Export] public float HpScale { get; set; } = 1f;

	// --log-hits: one line per hit the player takes, with what was nearest - for tuning a bot, not
	// for a table. Raw (pre-mitigation) amounts, as the signal carries them.
	[Export] public bool LogHits { get; set; }

	private void LogHit(int amount)
	{
		string nearest = "nothing";
		float best = float.MaxValue;
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Enemy e)
				continue;
			float d = e.GlobalPosition.DistanceTo(player.GlobalPosition);
			if (d < best) { best = d; nearest = $"{e.EnemyType} at {d:0} px"; }
		}
		bool hazard = Node2DGame.Current?.Terrain?.IsHazardAtWorld(player.FootPosition) == true;
		GD.Print($"HARNESS HIT t={elapsed:0.0}s {amount} hp={player.CurrentHP}/{player.MaxHP} nearest {nearest}{(hazard ? ", ON HAZARD" : "")}");
	}

	private static Vector2 SurvivorHeading(int i) =>
		Vector2.FromAngle(i * Mathf.Tau / SurvivorHeadings);

	private void DriveSurvivor()
	{
		Vector2 p = player.GlobalPosition;
		Array.Clear(survivorDanger);
		survivorNearby.Clear();
		float closest = float.MaxValue;
		Vector2 nearestEnemy = p;
		bool threatened = false;
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Enemy e || !IsInstanceValid(e))
				continue;
			Vector2 offset = e.GlobalPosition - p;
			float d = offset.Length();
			if (d < closest)
			{
				closest = d;
				nearestEnemy = e.GlobalPosition;
			}
			if (d < SurvivorOrbSafety + 700f)
				survivorNearby.Add(e.GlobalPosition);
			// A fast body is a threat from further out: the radius grows by what it covers in about
			// two thirds of a second, so a wolf at 130 is watched from 250 and a bramble at 60 from 200.
			float radius = (SurvivorDangerBase + SurvivorDangerLeadSeconds * e.Speed * SwarmScaleOf(e)) * (e.IsMiniBoss ? 1.3f : 1f);
			if (d > radius || d < 0.01f)
				continue;
			threatened = true;
			float near = 1f - d / radius;
			float weight = near * near * (e.IsMiniBoss ? 3f : 1f) * (d < 60f ? 2f : 1f);
			Vector2 dir = offset / d;
			for (int i = 0; i < SurvivorHeadings; i++)
			{
				float c = SurvivorHeading(i).Dot(dir);
				if (c > 0f)
					survivorDanger[i] += weight * c * c;
			}
		}

		Vector2? goal = null;
		if (!NoOrbs)
		{
			goal = OpensChests && NearestChest() is Vector2 chest && IsSafeToCollect(chest, SurvivorOrbSafety * 0.75f) ? chest : null;
			goal ??= NearestSafeOrb(p);
		}
		Vector2 toGoal = goal is Vector2 g && g.DistanceTo(p) > 16f ? (g - p).Normalized() : Vector2.Zero;
		Vector2 toEnemy = closest > SurvivorEngageDistance && closest < 1200f && player.CurrentHP > player.MaxHP * 0.6f
			? (nearestEnemy - p).Normalized() : Vector2.Zero;

		// Bolts: the wisps' and hexers' 2-damage shots were most of what killed the first version.
		// One on a line that would hit is stepped off sideways, harder the sooner it lands.
		// Bolts are parented to the game; its children are many, so they are walked every other call
		// (15 Hz of game time) and the answer held between.
		if ((survivorPhysicsStep & 2) == 0)
		{
			survivorDodge = Vector2.Zero;
			foreach (Node child in game.GetChildren())
			{
				if (child is not EnemyProjectile bolt || bolt.IsQueuedForDeletion())
					continue;
				Vector2 rel = p - bolt.GlobalPosition;
				Vector2 dir = Vector2.FromAngle(bolt.GlobalRotation);
				float along = rel.Dot(dir);
				if (along <= 0f || along > 360f)
					continue;
				Vector2 off = rel - dir * along;
				if (off.Length() > 46f)
					continue;
				Vector2 side = off.LengthSquared() > 1f ? off.Normalized() : new Vector2(-dir.Y, dir.X);
				float urgency = Mathf.Clamp(1f - along / Mathf.Max(1f, bolt.Speed) / 1.5f, 0.2f, 1f);
				survivorDodge += side * urgency;
			}
		}
		Vector2 dodge = survivorDodge;
		if (dodge != Vector2.Zero)
			threatened = true;

		// Hurt, it values its skin over the orbs.
		float hpFraction = player.MaxHP > 0 ? player.CurrentHP / (float)player.MaxHP : 1f;
		float goalWeight = hpFraction < 0.4f ? 0.35f : 1f;
		if (!threatened && toGoal == Vector2.Zero && toEnemy == Vector2.Zero)
		{
			foreach (string a in MoveActions)
				Input.ActionRelease(a);
			survivorLastIndex = -1;
			return;
		}

		// Stuck on terrain: the heading it was trying is barred for a second.
		if (elapsed - survivorStuckCheckAt >= 0.5f)
		{
			if (survivorLastIndex >= 0 && p.DistanceTo(survivorStuckFrom) < 6f)
				survivorBlockedUntil[survivorLastIndex] = elapsed + 1f;
			survivorStuckCheckAt = elapsed;
			survivorStuckFrom = p;
		}

		TerrainStreamer terrain = Node2DGame.Current?.Terrain;
		Vector2 feet = player.FootPosition;
		int best = 0;
		float bestScore = float.MinValue;
		for (int i = 0; i < SurvivorHeadings; i++)
		{
			Vector2 h = SurvivorHeading(i);
			float score = goalWeight * h.Dot(toGoal) + 0.6f * h.Dot(toEnemy) + 2.5f * h.Dot(dodge)
				- 3f * survivorDanger[i] + 0.15f * h.Dot(survivorLastHeading);
			if (survivorBlockedUntil[i] > elapsed)
				score -= 5f;
			if (terrain != null)
			{
				Vector2 ahead = feet + h * 52f;
				if (terrain.IsHazardAtWorld(ahead) || terrain.IsBlockedAtWorld(ahead))
					score -= 3f;
			}
			if (score > bestScore)
			{
				bestScore = score;
				best = i;
			}
		}

		Vector2 heading = SurvivorHeading(best);
		survivorLastHeading = heading;
		survivorLastIndex = best;
		Press("move_right", "move_left", heading.X);
		Press("move_down", "move_up", heading.Y);
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
		WalkBearing(new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)), null);
	}

	// One step of a walk along a bearing; with a target, the maze route heads for the reachable cell
	// nearest the target rather than the one furthest along the bearing.
	private void WalkBearing(Vector2 bearing, Vector2? target)
	{
		// Maze only: every stage has a navigation window now, but planning round lakes would change
		// what an open-stage --walk measures compared with older reports.
		if (MazeNavigation.Active is MazeNavigation maze && maze.IsMaze && WalkMaze(maze, bearing, target))
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

	private bool WalkMaze(MazeNavigation maze, Vector2 bearing, Vector2? target = null)
	{
		if (walkRoute.Count == 0 || elapsed >= walkReplanAt)
		{
			PlanMazeRoute(maze, bearing, target);
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

	private void PlanMazeRoute(MazeNavigation maze, Vector2 bearing, Vector2? target = null)
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
			float score = target is Vector2 goal ? -world.DistanceTo(goal) : (world - origin).Dot(bearing);
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

	// --- --explore ------------------------------------------------------------------------

	private bool exploreDone;
	private string exploreLeg = "";
	private float exploreLegFrom;
	private float exploreLegWalked;
	private Vector2 exploreLastPosition;
	private bool exploreHasLast;
	private bool exploreSawFound;
	private bool exploreSawTaken;
	private bool[] exploreSiteLogged = System.Array.Empty<bool>();

	// One frame of the explore walk. False once there is nothing left to go to (the bot then holds
	// its ground or kites, as it would without the flag).
	private bool ExploreStep()
	{
		SideEventDirector director = game.GetNodeOrNull<SideEventDirector>("SideEventDirector");
		if (director == null)
			return false;
		SideEvent sideEvent = director.Event != null && IsInstanceValid(director.Event) ? director.Event : null;
		WayfinderPickup wayfinder = director.Wayfinder != null && IsInstanceValid(director.Wayfinder) ? director.Wayfinder : null;

		if (exploreHasLast)
			exploreLegWalked += player.GlobalPosition.DistanceTo(exploreLastPosition);
		exploreLastPosition = player.GlobalPosition;
		exploreHasLast = true;

		if (!exploreSawTaken && PointsOfInterest.Revealed)
		{
			exploreSawTaken = true;
			ExploreLog("wayfinder taken");
		}
		if (sideEvent != null && !exploreSawFound && sideEvent.IsFound)
		{
			exploreSawFound = true;
			ExploreLog(sideEvent.GetType().Name + " found (begun)");
		}
		if (sideEvent != null)
		{
			if (exploreSiteLogged.Length != sideEvent.Sites.Count)
				exploreSiteLogged = new bool[sideEvent.Sites.Count];
			for (int i = 0; i < sideEvent.Sites.Count; i++)
			{
				if (sideEvent.Sites[i].Done && !exploreSiteLogged[i])
				{
					exploreSiteLogged[i] = true;
					ExploreLog($"site {i} ({sideEvent.Sites[i].Glyph}) done");
				}
			}
		}

		Vector2? target = null;
		string leg;
		if (wayfinder != null && !wayfinder.Taken)
		{
			target = wayfinder.Site.Position;
			leg = "to the wayfinder";
		}
		else if (sideEvent != null && PointsOfInterest.ObjectiveTarget is Vector2 objective)
		{
			target = objective;
			leg = "after the objective";
		}
		else
		{
			float best = float.MaxValue;
			if (sideEvent != null)
			{
				foreach (PointOfInterest site in sideEvent.Sites)
				{
					if (site.Done) continue;
					float d = site.Position.DistanceSquaredTo(player.GlobalPosition);
					if (d < best) { best = d; target = site.Position; }
				}
			}
			leg = "to the event";
		}

		if (target == null)
		{
			exploreDone = true;
			ExploreLog("nothing left to find");
			foreach (string a in MoveActions)
				Input.ActionRelease(a);
			return false;
		}
		if (leg != exploreLeg)
		{
			exploreLeg = leg;
			exploreLegFrom = elapsed;
			exploreLegWalked = 0f;
			GD.Print($"HARNESS explore: t={elapsed:0.0}s heading {leg}, {player.GlobalPosition.DistanceTo(target.Value):0} px away in a straight line");
		}

		// Standing on it: the grove holds and the ice thaws with the bot in place.
		if (player.FootPosition.DistanceTo(target.Value) < 24f)
		{
			foreach (string a in MoveActions)
				Input.ActionRelease(a);
			return true;
		}
		Vector2 to = (target.Value - player.FootPosition).Normalized();
		WalkBearing(to, target);
		return true;
	}

	private void ExploreLog(string what) =>
		GD.Print($"HARNESS explore: t={elapsed:0.0}s {what} - leg [{exploreLeg}] took {elapsed - exploreLegFrom:0.0}s"
			+ $" and {exploreLegWalked:0} px walked; level {player.CurrentLevel}, pos ({player.GlobalPosition.X:0},{player.GlobalPosition.Y:0})");

	private Vector2? NearestXpOrb()
	{
		Vector2? best = null;
		float bestDistance = float.MaxValue;
		// Enemy.DropXp parents its orb to GetTree().CurrentScene, which under the harness is THIS
		// node, not the game. Searching only the game found nothing, ever: the walk-to-orbs below
		// never fired, and every immortal measurement before it collected only what drifted into
		// the pickup radius - which flattered short-range spells and starved anything that kills
		// at a distance. Both are searched so a change to where orbs live cannot hide them again.
		// Read from the orbs' own group, filtered to those two parents, rather than by walking the
		// children of both: the game has thousands of children (the props), and that walk every frame
		// was 9% of the main thread - harness cost that read as the game's (.ai/performance.md).
		foreach (Node node in GetTree().GetNodesInGroup(XPOrb.GroupName))
		{
			if (node is not XPOrb orb || orb.IsQueuedForDeletion()) continue;
			Node parent = orb.GetParent();
			if (parent != this && parent != game) continue;
			float d = orb.GlobalPosition.DistanceSquaredTo(player.GlobalPosition);
			if (d < bestDistance) { bestDistance = d; best = orb.GlobalPosition; }
		}
		return best;
	}

	// The nearest enemy's offset from the player, for --aim-facing. A walk of the snapshot: once a
	// quarter second, so its cost is nothing.
	private Vector2? NearestEnemyOffset()
	{
		Vector2? best = null;
		float bestDistance = float.MaxValue;
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (enemy == null || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			Vector2 offset = enemy.GlobalPosition - player.GlobalPosition;
			float d = offset.LengthSquared();
			if (d > 1f && d < bestDistance)
			{
				bestDistance = d;
				best = offset;
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
		// A real level-up pauses the tree, and a picked menu is queued for deletion before it goes.
		// Acting on anything else re-sent one choice 1,639 times into a run that had four level-ups.
		// The pause is read FIRST: the search below walks every node in the game, and run every frame
		// it was 83% of the main thread at minute one - the bot, not the game, was what made a
		// harness run crawl, and every frame-time number it printed was mostly its own (2026-10-23,
		// .ai/performance.md).
		if (!GetTree().Paused)
			return;
		LevelUpMenu menu = FindNode<LevelUpMenu>(game);
		if (menu == null || !menu.Visible)
			return;
		if (menu.IsQueuedForDeletion())
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
		NoteAscensionOffers(ascensions);

		// --pick-every=N: decided once per menu (a reroll re-deals into the same one), and the skipped
		// ones are skipped outright - no reroll, no bank - so the build simply stops keeping pace.
		if (PickEvery > 1 && menu.GetInstanceId() != weakDecidedMenu)
		{
			weakDecidedMenu = menu.GetInstanceId();
			weakLevelUpsSeen++;
			if (weakLevelUpsSeen % PickEvery != 1)
			{
				weakSkips++;
				menu.EmitSignal(LevelUpMenu.SignalName.SkipRequested);
				return;
			}
		}

		// The planned policy reads the strip itself: which branch, and the Apotheosis first.
		if (SeekAscensions)
		{
			Act(menu, PickTowardAscensions(options, ascensions, menu.Charges));
			return;
		}

		if (Solo && !string.IsNullOrEmpty(SpellId))
		{
			PickSolo(menu, options, ascensions);
			return;
		}

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
			Act(menu, PickTowardApotheosis(options, ascensions, menu.Charges));
			return;
		}

		if (OrdinaryPolicy)
		{
			Act(menu, PickOrdinary(options));
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

	// --- --solo: the opener's cards and nothing else ------------------------------------
	private void PickSolo(LevelUpMenu menu, IReadOnlyList<LevelUpOption> options, IReadOnlyList<LevelUpOption> ascensions)
	{
		LevelUpOption up = ascensions.FirstOrDefault(o => o.SpellId == SpellId);
		if (up != null)
		{
			SpellEvolutionOption branch = up.EvolutionChoices.FirstOrDefault(e => e.Id == Branch8 && !up.EvolutionLockReason.ContainsKey(e.Id))
				?? up.EvolutionChoices.FirstOrDefault(e => !up.EvolutionLockReason.ContainsKey(e.Id));
			if (branch != null)
			{
				levelUpsHandled++;
				menu.EmitSignal(LevelUpMenu.SignalName.WeaponSelected, $"{up.SpellId}:{branch.Id}");
				return;
			}
		}
		LevelUpOption card = options.FirstOrDefault(o => o.SpellId == SpellId && !o.IsNewUnlock);
		if (card != null)
		{
			string choice = card.SpellId;
			if (card.IsEvolutionMilestone && card.EvolutionChoices != null && card.EvolutionChoices.Count > 0)
			{
				string wanted = card.MilestoneLevel == 8 ? Branch8 : Branch4;
				SpellEvolutionOption branch = card.EvolutionChoices.FirstOrDefault(e => e.Id == wanted)
					?? card.EvolutionChoices[0];
				choice = $"{card.SpellId}:{branch.Id}";
			}
			levelUpsHandled++;
			menu.EmitSignal(LevelUpMenu.SignalName.WeaponSelected, choice);
			return;
		}
		soloSkips++;
		menu.EmitSignal(LevelUpMenu.SignalName.SkipRequested);
	}

	// Charges are not picks: the menu stays open and re-deals (reroll, ban) or remembers (augury),
	// and the next frame decides again. A bank closes the menu, and the pick comes back at the next
	// level-up as a second one (Node2DGame.extraPicksPending).
	private void Act(LevelUpMenu menu, string choice)
	{
		if (choice == RerollAction)
		{
			// rerollsThisLevel is counted by the policy that asked, as it always was.
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
		if (choice == BankAction)
		{
			banksUsed++;
			menu.EmitSignal(LevelUpMenu.SignalName.BankRequested);
			return;
		}
		if (choice == SkipAction)
		{
			menu.EmitSignal(LevelUpMenu.SignalName.SkipRequested);
			return;
		}
		levelUpsHandled++;
		menu.EmitSignal(LevelUpMenu.SignalName.WeaponSelected, choice);
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
	private const string BankAction = "!bank";
	private const string SkipAction = "!skip";
	private int rerollsUsed, bansUsed, auguriesUsed, banksUsed;
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

	// --- Ascensions: what every run reports, whatever its policy ---------------------------
	// "An average good run should get one or two Ascensions; a planned run with good powerups and
	// luck, up to four" (.ai/gameplay-design.md, "How many Ascensions a run gets"). These count them,
	// say when each came, and at the end say what held back every spell that did not ascend.

	private sealed class AscensionRecord
	{
		public string SpellId;
		public string BranchId;
		public float At;
		public int PlayerLevel;
	}

	private readonly List<AscensionRecord> ascensionsTaken = new();
	private readonly Dictionary<string, float> reachedCardCapAt = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, float> ascensionFirstOfferedAt = new(StringComparer.OrdinalIgnoreCase);
	private readonly SortedDictionary<int, float> levelReachedAt = new();
	private int lastSeenLevel;

	private void TrackAscensions()
	{
		if (player == null)
			return;
		if (player.CurrentLevel > lastSeenLevel)
		{
			for (int l = lastSeenLevel + 1; l <= player.CurrentLevel; l++)
				levelReachedAt[l] = elapsed;
			if (lastSeenLevel < Player.AscensionMinimumPlayerLevel && player.CurrentLevel >= Player.AscensionMinimumPlayerLevel)
				GD.Print($"HARNESS player level {Player.AscensionMinimumPlayerLevel} (the Ascension gate) at t={elapsed:0}s ({elapsed / 60f:0.0} min)");
			if (lastSeenLevel < ApotheosisCatalog.MinimumPlayerLevel && player.CurrentLevel >= ApotheosisCatalog.MinimumPlayerLevel)
				GD.Print($"HARNESS player level {ApotheosisCatalog.MinimumPlayerLevel} (the Apotheosis gate) at t={elapsed:0}s ({elapsed / 60f:0.0} min)");
			lastSeenLevel = player.CurrentLevel;
		}

		foreach (SpellData spell in player.GetEquippedSpells())
		{
			if (spell == null)
				continue;
			if (spell.CurrentLevel >= Player.CardLevelCap(spell) && !reachedCardCapAt.ContainsKey(spell.Id))
				reachedCardCapAt[spell.Id] = elapsed;
			if (!string.IsNullOrEmpty(spell.SelectedLevel8EvolutionId)
				&& !ascensionsTaken.Any(a => a.SpellId.Equals(spell.Id, StringComparison.OrdinalIgnoreCase)))
			{
				ascensionsTaken.Add(new AscensionRecord
				{
					SpellId = spell.Id,
					BranchId = spell.SelectedLevel8EvolutionId,
					At = elapsed,
					PlayerLevel = player.CurrentLevel,
				});
				GD.Print($"HARNESS ASCENSION #{ascensionsTaken.Count}: {spell.Id} -> {spell.SelectedLevel8EvolutionId}"
					+ $" at t={elapsed:0}s ({elapsed / 60f:0.0} min), player level {player.CurrentLevel}");
			}
			if (spell.IsApotheosis && apotheosisTakenAt < 0f)
				apotheosisTakenAt = elapsed;
		}
	}

	private void NoteAscensionOffers(IReadOnlyList<LevelUpOption> ascensions)
	{
		foreach (LevelUpOption offer in ascensions)
		{
			if (offer.MilestoneLevel == Player.AscensionSpellLevel && !ascensionFirstOfferedAt.ContainsKey(offer.SpellId))
				ascensionFirstOfferedAt[offer.SpellId] = elapsed;
		}
	}

	// What stands between each held spell and its Ascension at the end of the run, read from the same
	// preview the ascension browser draws (Player.BuildAscensionPreview), so it cannot disagree with
	// the game about the rule. The branch with the fewest unmet gates speaks for the spell.
	private List<string> DescribeAscensionStanding()
	{
		var lines = new List<string>();
		var counts = player.GetElementInstanceCounts();
		lines.Add("  elements     : " + (counts.Count == 0 ? "none" : string.Join(", ",
			counts.OrderByDescending(p => p.Value).Select(p => $"{p.Key} {p.Value}"))));
		foreach (SpellData spell in player.GetEquippedSpells())
		{
			if (spell == null)
				continue;
			string tags = string.Join("/", spell.GetElementWeights().Select(p => $"{p.Key}{(p.Value > 1 ? "x" + p.Value : "")}"));
			string head = $"    {spell.Id} L{spell.CurrentLevel} [{tags}]";
			AscensionRecord taken = ascensionsTaken.FirstOrDefault(a => a.SpellId.Equals(spell.Id, StringComparison.OrdinalIgnoreCase));
			if (taken != null)
			{
				lines.Add($"{head} ASCENDED {taken.BranchId} at {taken.At / 60f:0.0} min (level {taken.PlayerLevel})"
					+ (spell.IsApotheosis ? ", then APOTHEOSIS" : ""));
				continue;
			}
			List<AscensionInfo> preview = player.BuildAscensionPreview(spell);
			if (preview.Count == 0)
			{
				lines.Add($"{head} has no Ascension");
				continue;
			}
			AscensionInfo closest = preview.OrderBy(p => p.Requirements.Count(r => !r.Met)).First();
			string blocked = string.Join(", ", closest.Requirements.Where(r => !r.Met).Select(r => r.Kind switch
			{
				AscensionRequirementKind.SpellLevel => $"spell level {r.Have}/{r.Need}",
				AscensionRequirementKind.Element => $"element {r.Element} {r.Have}/{r.Need}",
				AscensionRequirementKind.PlayerLevel => $"player level {r.Have}/{r.Need}",
				_ => $"{r.Kind} {r.Have}/{r.Need}",
			}));
			lines.Add($"{head} " + (blocked.Length == 0 ? "READY, not taken" : "blocked by " + blocked));
		}
		return lines;
	}

	// One line a script can turn into a table.
	private string AscensionSummaryLine()
	{
		string policy = SeekAscensions ? "planned" : OrdinaryPolicy ? "ordinary" : SeekApotheosis ? "seek-apotheosis" : "default";
		string Level(int l) => levelReachedAt.TryGetValue(l, out float t) ? $"{t / 60f:0.0}" : "-";
		string mover = Survivor ? (NoOrbs ? "survivor-no-orbs" : "survivor") : Immortal ? (CircleRadius > 0f ? $"immortal-circle{CircleRadius:0}" : "immortal") : "kite";
		return $"HARNESS SUMMARY character={CharacterIndex} policy={policy}{(FullShop ? "+shop" : "")}{(PickEvery > 1 ? $"+pick1in{PickEvery}" : "")} seed={Seed}"
			+ $" stage={Global.SelectedStageIdx} mover={mover} end={(player != null && player.IsDead ? "died" : "clock")}@{elapsed / 60f:0.0}"
			+ $" xp={player?.XpFirstLevelCost ?? 0}+{player?.XpCostPerLevel ?? 0} growth={SwarmStat<float>("XpGrowthPerPaceMinute"):0.000}"
			+ $" swarm={(SwarmStat<bool>("EnableSwarmPressure") ? "on" : "off")}{(HpScale != 1f ? $" hp=x{HpScale:0.##}" : "")}"
			+ $" lvByMin=[{string.Join(",", paceLedger.Select(r => r.Level))}]"
			+ $" minClock={(paceLedger.Count > 0 ? paceLedger.Min(r => r.Clock) : 1f):0.00}"
			+ $" maxFrameMs={(paceLedger.Count > 0 ? paceLedger.Max(r => r.FrameMs) : 0f):0}"
			+ $" maxPressure={(paceLedger.Count > 0 ? paceLedger.Max(r => r.Pressure) : 0f):0.00}"
			+ $" folds={paceLedger.Sum(r => r.Folds)} round={paceLedger.Sum(r => r.SentRound)}{ShadowSummary()}"
			+ $" ascensions={ascensionsTaken.Count} at=[{string.Join(",", ascensionsTaken.Select(a => $"{a.At / 60f:0.0}"))}]"
			+ $" apotheosis={(apotheosisTakenAt < 0f ? "-" : $"{apotheosisTakenAt / 60f:0.0}")}"
			+ $" level={player?.CurrentLevel ?? 0} L10={Level(10)} L17={Level(17)} L20={Level(20)} L23={Level(23)} L26={Level(26)} L29={Level(29)}"
			+ $" atCap={reachedCardCapAt.Count} slots={player?.GetEquippedSpells().Count ?? 0}/{player?.SpellSlots ?? Player.MaxSpellSlots}"
			+ $" boons={player?.GetOwnedBoonIds().Count ?? 0} banks={banksUsed}";
	}

	// --- the GOOD ORDINARY policy (--policy=ordinary) ------------------------------------------
	// A player who reads the cards but does not plan for Ascensions: fills the book first, choosing
	// the new spell that shares the element the player already holds most of (the card shows the
	// element preview, and a tier bonus is the most visible thing on it); then levels the strongest
	// spell in the book; takes a mutation branch that deepens an element it holds; a boon only when
	// nothing else is on offer. Every Ascension and the Apotheosis are taken the moment the strip
	// shows one (the block above PickOrdinary's call). Spends no charge: no reroll, ban, augury or
	// save - an ordinary player does not argue with the roll. Chests: the rarest relic on offer.

	private string PickOrdinary(IReadOnlyList<LevelUpOption> options)
	{
		Dictionary<Element, int> counts = player.GetElementInstanceCounts();
		int Shared(LevelUpOption o) => o.SpellElementTags == null ? 0 : o.SpellElementTags.Keys
			.Select(k => Enum.TryParse(k, true, out Element e) && counts.TryGetValue(e, out int n) ? n : 0)
			.DefaultIfEmpty(0).Max();

		if (!player.IsSpellBookFull)
		{
			LevelUpOption fresh = options
				.Where(o => o.IsNewUnlock && !o.IsBoon && o.SpellId != Player.MarginaliaId)
				.OrderByDescending(Shared)
				.FirstOrDefault();
			if (fresh != null)
				return fresh.SpellId;
		}

		string signature = player.SignatureSpellId;
		LevelUpOption upgrade = options
			.Where(o => !o.IsNewUnlock && !o.IsBoon)
			.OrderByDescending(o => o.NextLevel)
			.ThenByDescending(o => o.SpellId.Equals(signature, StringComparison.OrdinalIgnoreCase))
			.FirstOrDefault();
		if (upgrade != null)
			return WithBranch(upgrade, DeepestFirst(counts));

		LevelUpOption boon = options.Where(o => o.IsBoon).OrderByDescending(Shared).FirstOrDefault();
		if (boon != null)
			return boon.SpellId;

		return WithBranch(options.First(), DeepestFirst(counts));
	}

	// Elements in the order the player holds them, deepest first.
	private static List<string> DeepestFirst(Dictionary<Element, int> counts) =>
		counts.OrderByDescending(p => p.Value).Select(p => p.Key.ToString()).ToList();

	// A milestone card needs a branch named. The first branch that deepens an element in the given
	// order of preference; otherwise the first branch.
	private static string WithBranch(LevelUpOption option, IReadOnlyList<string> preferredElements)
	{
		if (!option.IsEvolutionMilestone || option.EvolutionChoices == null || option.EvolutionChoices.Count == 0)
			return option.SpellId;
		foreach (string element in preferredElements)
		{
			SpellEvolutionOption deepen = option.EvolutionChoices.FirstOrDefault(e =>
				e != null && !option.EvolutionLockReason.ContainsKey(e.Id)
				&& e.BonusElementWeights != null && e.BonusElementWeights.ContainsKey(element));
			if (deepen != null)
				return $"{option.SpellId}:{deepen.Id}";
		}
		SpellEvolutionOption first = option.EvolutionChoices.FirstOrDefault(e => e != null && !option.EvolutionLockReason.ContainsKey(e.Id))
			?? option.EvolutionChoices[0];
		return $"{option.SpellId}:{first.Id}";
	}

	// --- the PLANNED policy (--seek-ascensions) -------------------------------------------------
	// A player who plans for as many Ascensions as the run allows and uses every tool for it:
	//
	//   - The strip first: the Apotheosis, then any Ascension (the starter on its Apotheosis path,
	//     otherwise the branch that deepens the focus element), because an earned one costs one pick.
	//   - Focus: the wizard's own element. Every Ascension needs 4 of an element its spell carries,
	//     and spells that share one element share its count, so a plan concentrates tags in one place.
	//   - Spells: fill the book with focus carriers (pure first), then with spells sharing an element
	//     already held, because a book that is not full has no upgrades on 40% of its level-ups
	//     (Player.UpgradeOfferChance). Then level the HIGHEST spell that can still ascend - finish one
	//     before starting the next, so each lands as early as it can.
	//   - Tags: a boon or chest relic that lifts a held spell's gate from 3 to 4 is worth more than any
	//     upgrade; a focus boon is worth a pick when nothing better is offered.
	//   - Charges: when nothing on the cards advances the plan, name a carrier with the augury, strike
	//     an off-plan spell with a ban, reroll, and as a last resort save the level-up for the next.
	//   - Chests: a relic that unblocks a gate, then XP (Compass Rose, Wormwood Tithe), then a focus tag.

	private const int PlanValueUnblocks = 200;
	private const int PlanValueUseful = 60;

	private Element PlanFocus()
	{
		ApotheosisDefinition target = SeekTarget();
		if (target != null)
			return target.RequiredElement;
		SpellData signature = player.GetEquippedSpells().FirstOrDefault(s => s != null
			&& s.Id.Equals(player.SignatureSpellId, StringComparison.OrdinalIgnoreCase));
		foreach (Element e in signature?.GetElementWeights().Keys ?? Enumerable.Empty<Element>())
			return e;
		return Element.Arcane;
	}

	// Elements that would unblock a held spell sitting near its card cap with its element gate one
	// short: the tag that turns a finished spell into an Ascension.
	private HashSet<string> ElementsOneShortOfAGate()
	{
		var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (SpellData spell in player.GetEquippedSpells())
		{
			if (spell == null || !string.IsNullOrEmpty(spell.SelectedLevel8EvolutionId))
				continue;
			if (spell.CurrentLevel < Player.CardLevelCap(spell) - 2)
				continue;
			foreach (AscensionInfo info in player.BuildAscensionPreview(spell))
			{
				AscensionRequirement gate = info.Requirements.FirstOrDefault(r => r.Kind == AscensionRequirementKind.Element);
				if (gate == null || gate.Met || gate.Missing > 1)
					continue;
				foreach (Element e in gate.AnyOf)
					wanted.Add(e.ToString());
			}
		}
		return wanted;
	}

	// Whether a held spell's Ascension is within reach of the plan: one of its elements is the focus,
	// or already stands at 3 or more. A spell that cannot get there is not worth a pick to level.
	private bool CanStillAscend(SpellData spell, Element focus, Dictionary<Element, int> counts)
	{
		if (spell == null || !string.IsNullOrEmpty(spell.SelectedLevel8EvolutionId))
			return false;
		foreach (Element e in spell.GetElementWeights().Keys)
		{
			if (e == focus || (counts.TryGetValue(e, out int n) && n >= Player.AscensionElementRequirement - 1))
				return true;
		}
		return false;
	}

	private int PlanValue(LevelUpOption o, Element focus, Dictionary<Element, int> counts, HashSet<string> unblocking)
	{
		string focusName = focus.ToString();
		bool carriesFocus = o.SpellElementTags != null && o.SpellElementTags.ContainsKey(focusName);
		int shared = o.SpellElementTags == null ? 0 : o.SpellElementTags.Keys
			.Select(k => Enum.TryParse(k, true, out Element e) && counts.TryGetValue(e, out int n) ? n : 0)
			.DefaultIfEmpty(0).Max();
		bool unblocks = o.SpellElementTags != null && o.SpellElementTags.Keys.Any(unblocking.Contains);

		if (o.SpellId == Player.MarginaliaId)
			return 1;
		if (o.IsBoon)
			return unblocks ? PlanValueUnblocks : carriesFocus ? 50 : 10 + shared;
		if (o.IsNewUnlock)
		{
			if (player.IsSpellBookFull)
				return 0;
			if (unblocks)
				return PlanValueUnblocks - 10;
			if (carriesFocus)
				return 105 + (o.SpellElementTags.Count == 1 ? 3 : 0);
			return shared >= 2 ? PlanValueUseful + shared : 20 + shared;
		}

		SpellData held = player.GetEquippedSpells().FirstOrDefault(s => s != null
			&& s.Id.Equals(o.SpellId, StringComparison.OrdinalIgnoreCase));
		if (held == null)
			return 5;
		// The wizard's own spell first: it is the first Ascension, carries the Apotheosis after it, and
		// a first smoke run that let any spell lead left the starter at level 1 for four minutes.
		// Then finish the spell closest to its cap; a spell that cannot reach its gate is levelled only
		// when nothing else is on offer. A held candidate at level 1 (110) still outranks a new focus
		// carrier (105-108): the book fills on the level-ups that roll no upgrades anyway.
		if (held.Id.Equals(player.SignatureSpellId, StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(held.SelectedLevel8EvolutionId))
			return 180 + held.CurrentLevel;
		return CanStillAscend(held, focus, counts) ? 100 + 10 * held.CurrentLevel : 30 + held.CurrentLevel;
	}

	private string PickTowardAscensions(IReadOnlyList<LevelUpOption> options, IReadOnlyList<LevelUpOption> ascensions, LevelUpCharges charges)
	{
		Element focus = PlanFocus();
		string focusName = focus.ToString();
		ApotheosisDefinition target = SeekTarget();

		LevelUpOption final = ascensions.FirstOrDefault(o => o.MilestoneLevel == ApotheosisCatalog.MilestoneLevel);
		if (final != null && final.EvolutionChoices.Count > 0)
		{
			GD.Print($"HARNESS APOTHEOSIS offered and taken at t={elapsed:0}s ({elapsed / 60f:0.0} min)");
			return $"{final.SpellId}:{final.EvolutionChoices[0].Id}";
		}
		foreach (LevelUpOption up in ascensions.Where(o => o.MilestoneLevel == Player.AscensionSpellLevel))
		{
			bool earned(SpellEvolutionOption e) => e != null && !up.EvolutionLockReason.ContainsKey(e.Id);
			SpellEvolutionOption branch =
				(target != null && up.SpellId == target.SpellId
					? up.EvolutionChoices.FirstOrDefault(e => earned(e) && e.Id == target.RequiredLevel8EvolutionId)
					: null)
				?? up.EvolutionChoices.FirstOrDefault(e => earned(e) && e.BonusElementWeights != null && e.BonusElementWeights.ContainsKey(focusName))
				?? up.EvolutionChoices.FirstOrDefault(earned);
			if (branch != null)
				return $"{up.SpellId}:{branch.Id}";
		}
		if (options.Count == 0)
			return SkipAction;

		Dictionary<Element, int> counts = player.GetElementInstanceCounts();
		HashSet<string> unblocking = ElementsOneShortOfAGate();
		LevelUpOption best = options.OrderByDescending(o => PlanValue(o, focus, counts, unblocking)).First();
		int bestValue = PlanValue(best, focus, counts, unblocking);

		// The starter's mutation is the Apotheosis path's; every other mutation deepens the focus,
		// or failing that the element the player holds most of.
		var branchOrder = new List<string> { focusName };
		branchOrder.AddRange(DeepestFirst(counts).Where(e => e != focusName));
		string Choose(LevelUpOption o)
		{
			if (target != null && o.SpellId == target.SpellId && o.IsEvolutionMilestone && o.MilestoneLevel == 4)
				return $"{o.SpellId}:{target.RequiredLevel4EvolutionId}";
			return WithBranch(o, branchOrder);
		}

		// The spell being finished: the starter until it ascends, then the highest-level spell that can
		// still ascend. A planner fishes for that one card with its free rerolls (they refill every
		// level-up) instead of spreading levels over whatever the hand shows. The first planned run
		// took the best card in each hand: the Pyromancer reached level 40, took three Ascensions,
		// and the clock ran out with two more spells sitting at levels 6 and 7.
		string signature = player.SignatureSpellId;
		SpellData finishing = player.GetEquippedSpells()
			.Where(s => s != null && s.CurrentLevel < Player.CardLevelCap(s) && CanStillAscend(s, focus, counts))
			.OrderByDescending(s => s.Id.Equals(signature, StringComparison.OrdinalIgnoreCase))
			.ThenByDescending(s => s.CurrentLevel)
			.FirstOrDefault();
		LevelUpOption finishingCard = finishing == null ? null : options.FirstOrDefault(o =>
			!o.IsNewUnlock && !o.IsBoon && o.SpellId.Equals(finishing.Id, StringComparison.OrdinalIgnoreCase));
		if (bestValue >= PlanValueUnblocks)
			return Choose(best);
		if (finishingCard != null)
			return Choose(finishingCard);
		// Filling the book with a carrier of the focus needs no fishing: it is wanted whenever it comes.
		if (!player.IsSpellBookFull && best.IsNewUnlock && !best.IsBoon && bestValue >= 105)
			return Choose(best);
		if (finishing != null && charges != null && charges.Rerolls > 0
			&& rerollsThisLevel < Math.Max(1, player.RerollsPerLevelUp)
			&& (player.UpgradesOnOfferThisLevelUp || player.IsSpellBookFull))
		{
			rerollsThisLevel++;
			return RerollAction;
		}
		if (bestValue >= PlanValueUseful)
			return Choose(best);

		// Nothing on the cards advances the plan. Argue with the roll before settling.
		if (charges != null)
		{
			if (charges.Auguries > 0 && !auguryUsedThisLevel && charges.AuguryCandidates != null)
			{
				LevelUpOption named = (!player.IsSpellBookFull
						? charges.AuguryCandidates
							.Where(o => o.IsNewUnlock && !o.IsBoon && o.SpellElementTags != null && o.SpellElementTags.ContainsKey(focusName))
							.OrderBy(o => o.SpellElementTags.Count)
							.FirstOrDefault()
						: null)
					?? (player.CanTakeMoreBoons
						? charges.AuguryCandidates.FirstOrDefault(o => o.IsBoon && o.SpellElementTags != null
							&& o.SpellElementTags.Keys.Any(k => unblocking.Contains(k) || k == focusName))
						: null);
				if (named != null)
					return AuguryAction + named.SpellId;
			}
			if (charges.Bans > 0)
			{
				LevelUpOption offPlan = options.FirstOrDefault(o => o.IsNewUnlock && !o.IsBoon
					&& o.SpellId != Player.MarginaliaId && PlanValue(o, focus, counts, unblocking) < PlanValueUseful);
				if (offPlan != null)
					return BanAction + offPlan.SpellId;
			}
			if (charges.Rerolls > 0 && rerollsThisLevel < Math.Max(1, player.RerollsPerLevelUp))
			{
				rerollsThisLevel++;
				return RerollAction;
			}
			// Saved for the next level-up rather than spent on something the plan does not want -
			// but only while the book has room, since a full book always has upgrades on offer.
			if (charges.Banks > 0 && !player.IsSpellBookFull && bestValue < 30)
				return BankAction;
		}
		return Choose(best);
	}

	// --- chests --------------------------------------------------------------------------------
	// Under the harness the game is not the current scene, so ChestReward takes its no-menu path and
	// grants its ItemId - which nothing sets, so every chest the older policies stumbled into was a
	// Relic Key. The two pick policies make the chest real instead: as the bot closes on one it is
	// dealt the same offer the chest screen would deal (ChestItemCatalog.GetChestItemOptions, three
	// relics, four with the Relic Key) and the policy chooses one, which the pickup then grants.
	private bool OpensChests => SeekAscensions || OrdinaryPolicy;
	private readonly HashSet<ulong> chestsDealt = new();
	private readonly RandomNumberGenerator chestRng = new();
	private int chestsOpened;
	private readonly List<string> chestPicks = new();
	private const float ChestDealDistance = 90f;

	private Vector2? NearestChest()
	{
		Vector2? best = null;
		float bestDistance = 700f * 700f;
		foreach (ChestReward chest in ChestReward.Live)
		{
			if (chest == null || !IsInstanceValid(chest) || chest.Opened)
				continue;
			float d = chest.GlobalPosition.DistanceSquaredTo(player.GlobalPosition);
			if (d < bestDistance) { bestDistance = d; best = chest.GlobalPosition; }
		}
		return best;
	}

	private void PrepareNearbyChests()
	{
		foreach (ChestReward chest in ChestReward.Live)
		{
			if (chest == null || !IsInstanceValid(chest) || chest.Opened || chestsDealt.Contains(chest.GetInstanceId()))
				continue;
			if (chest.GlobalPosition.DistanceTo(player.GlobalPosition) > ChestDealDistance)
				continue;
			chestsDealt.Add(chest.GetInstanceId());
			// The bot never claims the Penknife: it would have to name a spell to cut, and cutting
			// one is never how a plan for more Ascensions goes.
			List<string> offer = ChestItemCatalog.GetChestItemOptions(player.GetOwnedChestItems(), chestRng,
				player.HasRelicKey ? 4 : 3, new[] { ChestItemCatalog.ScribesPenknife }, chestOfferMemory);
			if (offer.Count == 0)
				continue;
			string pick = SeekAscensions ? PickChestForPlan(offer) : PickChestOrdinary(offer);
			chest.ItemId = pick;
			chestsOpened++;
			chestPicks.Add(pick);
		}
	}

	private static string PickChestOrdinary(List<string> offer) =>
		offer.OrderByDescending(id => (int)ChestItemCatalog.GetRarity(id)).First();

	private string PickChestForPlan(List<string> offer)
	{
		Element focus = PlanFocus();
		HashSet<string> unblocking = ElementsOneShortOfAGate();
		int Value(string id)
		{
			var tags = ChestItemCatalog.GetElementTags(id).Select(t => t.Element).ToList();
			if (tags.Any(unblocking.Contains)) return 300;
			if (id == ChestItemCatalog.WormwoodTithe) return 250;
			if (id == ChestItemCatalog.CompassRose) return 240;
			if (tags.Contains(focus.ToString())) return 200;
			return (int)ChestItemCatalog.GetRarity(id) * 10;
		}
		return offer.OrderByDescending(Value).First();
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
			SeekApotheosis || SeekAscensions ? $"  charges used : {rerollsUsed} rerolls, {bansUsed} bans, {auguriesUsed} auguries, {banksUsed} saved level-ups" : "",
			$"  apotheosis   : {(apotheosisTakenAt < 0f ? "never taken" : $"taken at {apotheosisTakenAt:0}s ({apotheosisTakenAt / 60f:0.0} min)")}"
				+ (SeekApotheosis || SeekAscensions ? $"; peak element tier {peakTier}" : ""),
			$"  ascensions   : {ascensionsTaken.Count} taken"
				+ (ascensionsTaken.Count > 0 ? " - " + string.Join("; ", ascensionsTaken.Select(a =>
					$"{a.SpellId} at {a.At / 60f:0.0} min (level {a.PlayerLevel}"
					+ (ascensionFirstOfferedAt.TryGetValue(a.SpellId, out float offered) && a.At - offered > 1f ? $", offered at {offered / 60f:0.0}" : "")
					+ ")")) : ""),
			$"  level clock  : " + string.Join(", ", new[] { 10, 17, 20, 23, 26, 29, 32 }
				.Where(levelReachedAt.ContainsKey).Select(l => $"L{l} {levelReachedAt[l] / 60f:0.0}")),
			OpensChests ? $"  chests       : {chestsOpened} opened - {string.Join(", ", chestPicks)}" : "",
			$"  boon hands   : {Rate(handsWithBoon, handsSeen)}; {boonCardsSeen} boon cards",
			SampleOffers > 0 && !SeekApotheosis ? $"  boon sampled : {Rate(sampledHandsWithBoon, sampledHands)}; {sampledBoonCards} boon cards ({(sampledHands > 0 ? (float)sampledBoonCards / sampledHands : 0f):0.000} per hand)" : "",
			!float.IsNaN(WalkBearingDegrees) ? "  " + CrowdSummary() : "",
			"  " + StandStillSummary(),
			"  " + OverlapSummary(),
			$"  taken        : {GameStats.RunDamageTaken} damage in {GameStats.RunHitsTaken} hits; {GameStats.RunHpLost} HP actually lost after armour and shields",
			// The pack's own count, and the proof no hound got stuck: every blink home is a recovery.
			Familiar.StatHoundFrames > 0 ? "  " + Familiar.StatsLine() : "",
			$"  stage        : {Global.SelectedStageIdx}; spawned " + string.Join(", ", spawnedByType.Select(p => $"{p.Key} {p.Value}")),
		};
		lines.AddRange(DropLedger());
		if (PickEvery > 1)
			lines.Add($"  weak picks   : took one level-up in {PickEvery}; skipped {weakSkips} of {weakLevelUpsSeen}");
		lines.AddRange(PaceLedgerLines());
		lines.Add("");

		if (player != null && IsInstanceValid(player))
		{
			lines.Add("  loadout      : " + string.Join(", ",
				player.GetEquippedSpells().Where(s => s != null).Select(s => $"{s.Id} L{s.CurrentLevel}")));
			var boons = player.GetOwnedBoonIds();
			lines.Add("  boons        : " + (boons.Count > 0 ? string.Join(", ", boons) : "none"));
			lines.Add("  relics       : " + string.Join(", ", player.GetOwnedChestItems()));
			lines.Add("  ascension standing (what held back each spell that did not ascend):");
			lines.AddRange(DescribeAscensionStanding());
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
		lines.AddRange(SpellLevelLines());
		lines.Add("");
		lines.Add(AscensionSummaryLine());

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
