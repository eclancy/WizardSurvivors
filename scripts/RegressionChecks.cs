using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

public static class RegressionChecks
{
	public static List<string> RunAll()
	{
		var warnings = new List<string>();
		ValidateDynamicRewardBounds(warnings);
		ValidateTelemetryHistoryCap(warnings);
		ValidateCharacterRosterBasics(warnings);
		ValidateSaveDefaults(warnings);
		ValidatePlaytestChecklistDefaults(warnings);
		ValidatePresetRewardOrdering(warnings);
		ValidateSpellEvolutionCoverage(warnings);
		ValidateChestSetPresentation(warnings);
		ValidateBossCatalog(warnings);
		ValidateRangedEnemies(warnings);
		ValidateAttackPatternEnemies(warnings);
		ValidateStartingCharacters(warnings);
		ValidateElementTags(warnings);
		ValidateUnlockCatalog(warnings);
		ValidateAchievements(warnings);
		ValidateStageCatalog(warnings);
		ValidateAudio(warnings);
		return warnings;
	}

	// Sound is the one system where a missing asset is not a crash and not a visible glitch - it
	// is silence, which looks exactly like a design decision. So the whole catalog is checked at
	// startup rather than discovered one absent hit at a time.
	private static void ValidateAudio(List<string> warnings)
	{
		int missing = 0;
		foreach (string name in SfxCatalog.AllNames)
		{
			string path = SfxCatalog.PathFor(name);
			if (ResourceLoader.Exists(path))
				continue;
			missing++;
			if (missing <= 5)
				warnings.Add($"Audio: missing sound '{name}' (expected {path}). Run: python tools/audio/build.py");
		}
		if (missing > 5)
			warnings.Add($"Audio: {missing - 5} further sounds missing from {SfxCatalog.Directory}");

		// A missing bus is not fatal - MusicPlayer and SfxPlayer both fall back to Master - but it
		// silently collapses the volume sliders onto one control, which is the bug that shipped
		// before default_bus_layout.tres existed.
		if (AudioServer.GetBusIndex(MusicPlayer.MusicBusName) < 0)
			warnings.Add("Audio: no 'Music' bus; the music slider is falling back to Master and now duplicates it.");
		if (AudioServer.GetBusIndex(SfxPlayer.SfxBusName) < 0)
			warnings.Add("Audio: no 'SFX' bus; the effects slider is falling back to Master and now duplicates it.");

		// The duck lowers SFX_Bed and leaves SFX_Priority alone. Without both, every sound lands
		// on one bus, the duck would fight the volume slider, and a critical sound would duck
		// itself - so the whole thing degrades to no ducking at all, silently.
		if (AudioServer.GetBusIndex(SfxPlayer.BedBusName) < 0)
			warnings.Add($"Audio: no '{SfxPlayer.BedBusName}' bus; ducking is disabled.");
		if (AudioServer.GetBusIndex(SfxPlayer.PriorityBusName) < 0)
			warnings.Add($"Audio: no '{SfxPlayer.PriorityBusName}' bus; critical sounds will duck themselves.");

		int sfxBus = AudioServer.GetBusIndex(SfxPlayer.SfxBusName);
		if (sfxBus >= 0 && AudioServer.GetBusEffectCount(sfxBus) == 0)
		{
			// tools/audio/mixsim.py measured the summed mix peaking at +1.5 to +2.5 dBFS in every
			// scenario, including the sparse early game. Without a limiter here it clips.
			warnings.Add("Audio: the SFX bus has no limiter; the summed mix clips in every measured scenario.");
		}

		// The tiers only work because Critical is rare. Promote enough sounds into it and the
		// duck never releases, the priority bus stops meaning anything, and the mix pumps.
		int critical = 0;
		foreach (string name in SfxCatalog.AllNames)
		{
			if (SfxCatalog.TierOf(name) == SfxCatalog.SfxTier.Critical)
				critical++;
		}
		if (critical > MaxCriticalSounds)
		{
			warnings.Add($"Audio: {critical} sounds are tier Critical (limit {MaxCriticalSounds}). "
				+ "Critical ducks everything else and is never stolen; it has to stay rare to mean anything.");
		}
	}

	// Ten today. The ceiling is deliberately close to that number so raising it is a decision
	// somebody makes on purpose rather than a threshold nobody notices drifting.
	private const int MaxCriticalSounds = 14;


	// The stage list is the only screen that says what a chapter is and what the dark wizard has
	// done to it, and a blank string there is invisible to the compiler: the card simply renders
	// one line shorter. Index/id agreement matters more - Global.SelectedStageIdx is an index into
	// this array, while unlocks are recorded against the id, so a mismatch unlocks the wrong place.
	private static void ValidateStageCatalog(List<string> warnings)
	{
		var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		for (int i = 0; i < StageCatalog.All.Count; i++)
		{
			StageDefinition stage = StageCatalog.All[i];

			if (stage.Index != i)
				warnings.Add($"Stage '{stage.Id}' sits at position {i} but declares Index {stage.Index}; a selection would enter a different chapter than the card clicked.");

			if (!string.Equals(stage.Id, $"stage_{i}", StringComparison.OrdinalIgnoreCase))
				warnings.Add($"Stage at position {i} has id '{stage.Id}', not 'stage_{i}'; boss unlocks and save entries match on that id.");

			if (!seenIds.Add(stage.Id))
				warnings.Add($"Stage id '{stage.Id}' appears more than once, so one of the two can never be unlocked.");

			if (string.IsNullOrWhiteSpace(stage.DisplayName))
				warnings.Add($"Stage '{stage.Id}' has no DisplayName, so its card and the game-over screen would both be blank.");

			if (string.IsNullOrWhiteSpace(stage.FlavorText))
				warnings.Add($"Stage '{stage.Id}' has no FlavorText, so its card and the in-run intro banner say nothing about the place.");

			if (string.IsNullOrWhiteSpace(stage.CorruptionText))
				warnings.Add($"Stage '{stage.Id}' has no CorruptionText, so its card never says how the dark wizard holds it.");

			if (!stage.IsPlayable && string.IsNullOrWhiteSpace(stage.LockedHint) && stage.Gate != StageGate.CampaignComplete)
				warnings.Add($"Stage '{stage.Id}' is not playable and has no LockedHint, so its card locks with no explanation.");
		}
	}

	// Scenes carrying a RangedEnemy script. There is no catalog for ordinary enemies - Node2DGame
	// holds them as fields - so this list is maintained by hand the same way SpellResourcePaths is.
	private static readonly string[] RangedEnemyScenePaths =
	{
		"res://scenes/CultistEnemy.tscn",
		"res://scenes/SkullSentry.tscn",
	};

	// A ranged enemy is the first one whose behaviour depends on a second scene resolving at
	// runtime. Every way this breaks is silent: a missing bolt scene, or a wind-up of zero, both
	// produce an enemy that still walks and still looks fine, so nobody finds out until a player
	// wonders why the caster never does anything - or takes a hit with no tell at all. A dotnet
	// build sees none of it.
	private static void ValidateRangedEnemies(List<string> warnings)
	{
		foreach (string path in RangedEnemyScenePaths)
		{
			if (!ResourceLoader.Exists(path))
			{
				warnings.Add($"Ranged enemy scene '{path}' is missing, so it would never spawn.");
				continue;
			}

			var scene = GD.Load<PackedScene>(path);
			if (scene == null)
			{
				warnings.Add($"Ranged enemy scene '{path}' failed to load.");
				continue;
			}

			Node probe = scene.Instantiate();
			if (probe is not RangedEnemy ranged)
			{
				warnings.Add($"Ranged enemy scene '{path}' has no RangedEnemy script attached, so it would chase and melee like every other enemy.");
				probe?.Free();
				continue;
			}

			// The three ranges only mean anything as an ordered set. Out of order, the enemy either
			// backs away from a spot it is also trying to reach, or holds station outside the range
			// it can shoot from - both read as an enemy that has lost interest in the fight.
			if (ranged.RetreatDistance >= ranged.StandoffDistance || ranged.StandoffDistance >= ranged.AttackRange)
			{
				warnings.Add($"Ranged enemy '{path}' has ranges out of order (retreat {ranged.RetreatDistance}, standoff {ranged.StandoffDistance}, attack {ranged.AttackRange}); they must increase in that order.");
			}

			if (ranged.CastWindUpSeconds <= 0f)
				warnings.Add($"Ranged enemy '{path}' has no cast wind-up, so its shot arrives with no tell the player can read.");

			if (ranged.BoltsPerVolley < 1)
				warnings.Add($"Ranged enemy '{path}' fires {ranged.BoltsPerVolley} bolts per volley, so it would telegraph and shoot nothing.");

			// Stacked bolts are the one way a volley can be dishonest: several bolts on the exact
			// same heading look like one shot, land as one hit, and quietly multiply its damage.
			if (ranged.BoltsPerVolley > 1 && ranged.VolleySpreadDegrees <= 0f)
				warnings.Add($"Ranged enemy '{path}' fires {ranged.BoltsPerVolley} bolts with no spread, so they overlap into what looks like a single shot dealing several times its listed damage.");

			ValidateEnemyProjectile(warnings, path, ranged.ProjectileScenePath);
			ranged.Free();
		}
	}

	// The four attack-pattern enemies (issue #33). Every one of them promises the player something
	// before it hits: a streak along the line it will charge, a ring where the ground is about to
	// be hit, a ring where minions are about to stand. A wind-up of zero silently turns any of
	// those promises into damage that simply arrives, and the enemy still walks around looking
	// perfectly fine - so nothing but a check like this one catches it. A dotnet build sees none
	// of it: the scenes and their exported tuning only exist in Godot.
	private static readonly string[] AttackPatternEnemyScenePaths =
	{
		"res://scenes/LungerEnemy.tscn",
		"res://scenes/SlammerEnemy.tscn",
		"res://scenes/ExploderEnemy.tscn",
		"res://scenes/SummonerEnemy.tscn",
	};

	private static void ValidateAttackPatternEnemies(List<string> warnings)
	{
		foreach (string path in AttackPatternEnemyScenePaths)
		{
			if (!ResourceLoader.Exists(path))
			{
				warnings.Add($"Attack-pattern enemy scene '{path}' is missing, so it would never spawn.");
				continue;
			}

			var scene = GD.Load<PackedScene>(path);
			if (scene == null)
			{
				warnings.Add($"Attack-pattern enemy scene '{path}' failed to load.");
				continue;
			}

			Node probe = scene.Instantiate();
			switch (probe)
			{
				case LungerEnemy lunger:
					ValidateLunger(warnings, path, lunger);
					break;
				case SlammerEnemy slammer:
					ValidateSlammer(warnings, path, slammer);
					break;
				case ExploderEnemy exploder:
					ValidateExploder(warnings, path, exploder);
					break;
				case SummonerEnemy summoner:
					ValidateSummoner(warnings, path, summoner);
					break;
				default:
					warnings.Add($"Attack-pattern enemy scene '{path}' has no attack-pattern script attached, so it would chase and melee like every other enemy.");
					break;
			}

			probe?.Free();
		}
	}

	private static void ValidateLunger(List<string> warnings, string path, LungerEnemy lunger)
	{
		if (lunger.LungeWindUpSeconds <= 0f)
			warnings.Add($"Lunger '{path}' has no wind-up, so its charge starts with no tell the player can read.");

		// A "dash" at or below walking pace is just a pause followed by more walking, and the
		// streak drawn during the wind-up would promise a reach it never covers.
		if (lunger.LungeSpeedMultiplier <= 1f)
			warnings.Add($"Lunger '{path}' has LungeSpeedMultiplier {lunger.LungeSpeedMultiplier}, so its charge is no faster than its walk.");

		if (lunger.MinimumLungeDistance >= lunger.LungeRange)
			warnings.Add($"Lunger '{path}' can never charge: MinimumLungeDistance {lunger.MinimumLungeDistance} is not below LungeRange {lunger.LungeRange}.");

		// The recovery is the reward for dodging. Without it the charge has no cost and the enemy
		// is simply faster than the player with extra steps.
		if (lunger.RecoverySeconds <= 0f)
			warnings.Add($"Lunger '{path}' has no recovery window, so reading and dodging its charge gains the player nothing.");
	}

	private static void ValidateSlammer(List<string> warnings, string path, SlammerEnemy slammer)
	{
		if (slammer.SlamTelegraphSeconds <= 0f)
			warnings.Add($"Slammer '{path}' has no telegraph, so its slam lands with no warning ring.");

		if (slammer.SlamRadius <= 0f)
			warnings.Add($"Slammer '{path}' has a slam radius of {slammer.SlamRadius}, so it would draw a ring and hit nothing.");

		if (slammer.SlamDamage <= 0)
			warnings.Add($"Slammer '{path}' deals {slammer.SlamDamage} slam damage, so its whole attack is decorative.");

		// Below 1 it only commits once the player is already inside the ring, which spends the
		// wind-up warning them about a hit they were given no room to avoid.
		if (slammer.SlamEngageRadiusMultiplier < 1f)
			warnings.Add($"Slammer '{path}' engages at {slammer.SlamEngageRadiusMultiplier}x its radius; below 1 the player is inside the ring before the tell begins.");
	}

	private static void ValidateExploder(List<string> warnings, string path, ExploderEnemy exploder)
	{
		if (exploder.FuseSeconds <= 0f)
			warnings.Add($"Exploder '{path}' has no fuse, so it detonates on arrival with no chance to kill it or step away.");

		if (exploder.BlastRadius <= 0f)
			warnings.Add($"Exploder '{path}' has a blast radius of {exploder.BlastRadius}, so it would detonate harmlessly.");

		if (exploder.BlastDamage <= 0)
			warnings.Add($"Exploder '{path}' deals {exploder.BlastDamage} blast damage, so nothing is at stake in letting it reach you.");
	}

	private static void ValidateSummoner(List<string> warnings, string path, SummonerEnemy summoner)
	{
		if (summoner.SummonWindUpSeconds <= 0f)
			warnings.Add($"Summoner '{path}' has no wind-up, so minions appear with no ring showing where.");

		if (summoner.MaxActiveMinions < 1 || summoner.MinionsPerSummon < 1)
			warnings.Add($"Summoner '{path}' is capped at {summoner.MaxActiveMinions} active / {summoner.MinionsPerSummon} per summon, so it would telegraph and summon nothing.");

		// The lifetime cap is the anti-farming bound. Set below the live cap it would stop the
		// summoner before it ever fills the arena once, which is not what either cap is for.
		if (summoner.MaxTotalSummons < summoner.MaxActiveMinions)
			warnings.Add($"Summoner '{path}' has MaxTotalSummons {summoner.MaxTotalSummons} below MaxActiveMinions {summoner.MaxActiveMinions}, so it can never reach its own live cap.");

		if (summoner.RetreatDistance >= summoner.StandoffDistance)
			warnings.Add($"Summoner '{path}' has ranges out of order (retreat {summoner.RetreatDistance}, standoff {summoner.StandoffDistance}); retreat must be the smaller.");

		if (summoner.MinionHealthFraction <= 0f || summoner.MinionHealthFraction > 1f)
			warnings.Add($"Summoner '{path}' has MinionHealthFraction {summoner.MinionHealthFraction}; it must be above 0 and at most 1, or minions are free kills or tougher than their summoner.");

		ValidateSummonerMinion(warnings, path, summoner.MinionScenePath);
	}

	private static void ValidateSummonerMinion(List<string> warnings, string ownerPath, string minionPath)
	{
		if (string.IsNullOrWhiteSpace(minionPath) || !ResourceLoader.Exists(minionPath))
		{
			warnings.Add($"Summoner '{ownerPath}' points at a missing minion scene: '{minionPath}'.");
			return;
		}

		var scene = GD.Load<PackedScene>(minionPath);
		if (scene == null)
		{
			warnings.Add($"Minion scene '{minionPath}' failed to load, so '{ownerPath}' would telegraph and summon nothing.");
			return;
		}

		Node probe = scene.Instantiate();
		if (probe is not Enemy)
		{
			warnings.Add($"Minion scene '{minionPath}' has no Enemy script attached, so '{ownerPath}' cannot set its health or track it.");
		}
		else if (probe is SummonerEnemy)
		{
			// Each generation carries its own MaxTotalSummons, so summoners summoning summoners is
			// bounded but grows exponentially - and every one of them is another live tell to draw.
			warnings.Add($"Summoner '{ownerPath}' summons another summoner ('{minionPath}'), which multiplies across generations.");
		}

		probe?.Free();
	}

	private static void ValidateEnemyProjectile(List<string> warnings, string ownerPath, string projectilePath)
	{
		if (string.IsNullOrWhiteSpace(projectilePath) || !ResourceLoader.Exists(projectilePath))
		{
			warnings.Add($"Ranged enemy '{ownerPath}' points at a missing projectile scene: '{projectilePath}'.");
			return;
		}

		var scene = GD.Load<PackedScene>(projectilePath);
		if (scene == null)
		{
			warnings.Add($"Projectile scene '{projectilePath}' failed to load, so '{ownerPath}' would telegraph and then fire nothing.");
			return;
		}

		Node probe = scene.Instantiate();
		if (probe is not EnemyProjectile bolt)
		{
			warnings.Add($"Projectile scene '{projectilePath}' has no EnemyProjectile script attached.");
			probe?.Free();
			return;
		}

		// The bolt finds the player through its collision shape; without one it passes straight
		// through and the caster is decorative.
		if (bolt.GetNodeOrNull<CollisionShape2D>("CollisionShape2D") == null)
			warnings.Add($"Projectile scene '{projectilePath}' has no CollisionShape2D, so its bolts would pass through the player.");

		bolt.Free();
	}

	// A boss is the only way to win a level, so a broken entry silently costs the player the ending
	// of that stage - the run would just keep going past the timer with nothing to kill. None of
	// this is checkable from a dotnet build: the scene path and its script only resolve in Godot.
	private static void ValidateBossCatalog(List<string> warnings)
	{
		foreach (BossDefinition boss in BossCatalog.All)
		{
			int stageIndex = BossCatalog.StageIndexOf(boss);

			if (string.IsNullOrWhiteSpace(boss.Id))
				warnings.Add($"Boss for stage {stageIndex} has no Id, so its victory cannot be recorded or matched to an achievement.");

			ValidateBossScene(warnings, boss);

			if (boss.Health <= 0)
				warnings.Add($"Boss '{boss.Id}' has non-positive Health, so it would die on spawn.");

			// Boss ids are matched exactly by the achievements now, so a typo on either side is a
			// boss whose kill silently grants nothing. The probe records the win into a throwaway
			// save's lifetime stats, which is the same path a real victory takes.
			if (!string.IsNullOrWhiteSpace(boss.Id)
				&& !AchievementDefinitions.All.Any(a => !string.IsNullOrWhiteSpace(a.SpellUnlockId) && a.IsComplete(BuildVictoryProbe(boss.Id))))
			{
				warnings.Add($"Boss '{boss.Id}' matches no achievement, so defeating it unlocks no spell.");
			}

			if (!string.IsNullOrWhiteSpace(boss.UnlocksStageId) && !boss.UnlocksStageId.StartsWith("stage_", StringComparison.OrdinalIgnoreCase))
				warnings.Add($"Boss '{boss.Id}' unlocks '{boss.UnlocksStageId}', which is not a stage id of the form 'stage_N'.");
		}
	}

	// Actually builds the boss once. A .tscn that exists but has the wrong script - or no script -
	// still passes ResourceLoader.Exists and then fails at minute fifteen, where nobody is watching.
	// Instantiate does not run _Ready (that needs the tree), so this is a cheap structural probe.
	private static void ValidateBossScene(List<string> warnings, BossDefinition boss)
	{
		if (string.IsNullOrWhiteSpace(boss.ScenePath) || !ResourceLoader.Exists(boss.ScenePath))
		{
			warnings.Add($"Boss '{boss.Id}' has a missing scene: '{boss.ScenePath}'.");
			return;
		}

		var scene = GD.Load<PackedScene>(boss.ScenePath);
		if (scene == null)
		{
			warnings.Add($"Boss '{boss.Id}' scene '{boss.ScenePath}' failed to load.");
			return;
		}

		Node probe = scene.Instantiate();
		if (probe is not BossEnemy)
			warnings.Add($"Boss '{boss.Id}' scene '{boss.ScenePath}' does not have a BossEnemy script attached, so the run could never be won.");

		probe?.Free();
	}

	// The smallest RunResult that can satisfy a boss achievement: a victory carrying that boss id.
	// Deliberately leaves every other field at its default so only the boss clause can match.
	private static AchievementContext BuildVictoryProbe(string bossId)
	{
		var run = new global::RunResult
		{
			Outcome = global::RunOutcome.Victory,
			BossId = bossId,
			StageId = "stage_0",
		};

		var save = new SaveData();
		save.Lifetime.Absorb(run);
		return AchievementContext.ForRun(save, run);
	}

	// The synergy screen renders each set from its icon and its Effects list, so a set that ships
	// with neither is invisible or blank to the player. This cannot check that the numbers still
	// agree with Player.RefreshChestSetEffects - that switch is imperative - but it does catch a
	// new set being added without its presentation data filled in.
	private static void ValidateChestSetPresentation(List<string> warnings)
	{
		foreach (var set in ChestItemCatalog.Sets)
		{
			if (set.Effects == null || set.Effects.Length == 0)
				warnings.Add($"Chest set '{set.Id}' declares no Effects, so the synergy screen has nothing to list.");

			if (string.IsNullOrWhiteSpace(set.IconPath) || !ResourceLoader.Exists(set.IconPath))
				warnings.Add($"Chest set '{set.Id}' has a missing icon: '{set.IconPath}'.");

			if (set.RequiredItemIds == null || set.RequiredItemIds.Length == 0)
				warnings.Add($"Chest set '{set.Id}' requires no items, so it completes immediately.");
		}

		ValidateChestItemIcons(warnings);
	}

	// Relics are told apart by their icon in the chest menu, the HUD strip and the synergy screen,
	// so two relics sharing one image is a real readability bug rather than a cosmetic one. Three
	// pairs used to collide; this keeps them from drifting back together.
	private static void ValidateChestItemIcons(List<string> warnings)
	{
		var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (string itemId in ChestItemCatalog.AllItemIds)
		{
			string icon = ChestItemCatalog.GetIconPath(itemId);
			if (string.IsNullOrWhiteSpace(icon) || !ResourceLoader.Exists(icon))
			{
				warnings.Add($"Chest item '{itemId}' has a missing icon: '{icon}'.");
				continue;
			}

			if (seen.TryGetValue(icon, out string owner))
				warnings.Add($"Chest items '{owner}' and '{itemId}' share the icon '{icon}'.");
			else
				seen[icon] = itemId;
		}
	}

	private static void ValidateSpellEvolutionCoverage(List<string> warnings)
	{
		string[] testSpellIds = new[] { "magic_missile", "fireball", "arcane_explosion", "aegis_ward", "meteor_swarm" };
		foreach (string id in testSpellIds)
		{
			var spell = new SpellData { Id = id, Name = id };
			SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
			if (spell.Level4Options == null || spell.Level4Options.Count != 3)
				warnings.Add($"Spell '{id}' should have exactly 3 Level 4 evolution choices, got {spell.Level4Options?.Count ?? 0}.");
			if (spell.Level8Options == null || spell.Level8Options.Count != 2)
				warnings.Add($"Spell '{id}' should have exactly 2 Level 8 evolution choices, got {spell.Level8Options?.Count ?? 0}.");
		}
	}

	private static void ValidateDynamicRewardBounds(List<string> warnings)
	{
		var save = new SaveData();
		for (int i = 0; i < 8; i++)
		{
			save.RunTelemetryHistory.Add(new RunTelemetryRecord
			{
				Outcome = i < 4 ? "Defeat" : "Victory",
				TimeSurvived = 180f + (i * 25f),
				EnemiesKilled = 40 + (i * 7)
			});
		}

		var run = new RunResult
		{
			TimeSurvived = 480f,
			EnemiesKilled = 220,
			EquippedSpells = new List<RunSpellSnapshot>
			{
				new() { Id = "magic_missile", DisplayName = "Magic Missile" },
				new() { Id = "fireball", DisplayName = "Fireball" },
				new() { Id = "meteor_swarm", DisplayName = "Meteor Swarm" }
			}
		};

		float multiplier = GlobalStatsManager.GetDynamicArcaneRewardMultiplier(save, run, out _);
		if (multiplier < GlobalStatsManager.DynamicRewardMinMultiplier || multiplier > GlobalStatsManager.DynamicRewardMaxMultiplier)
			warnings.Add($"Dynamic reward multiplier out of bounds: {multiplier:0.000}");
	}

	private static void ValidateTelemetryHistoryCap(List<string> warnings)
	{
		var save = new SaveData();
		for (int i = 0; i < 40; i++)
		{
			save.RecordRunTelemetry(new RunResult
			{
				Outcome = i % 2 == 0 ? RunOutcome.Defeat : RunOutcome.Victory,
				TimeSurvived = 60f + i,
				EnemiesKilled = i
			});
		}

		if (save.RunTelemetryHistory.Count > 25)
			warnings.Add($"Telemetry history exceeded cap: {save.RunTelemetryHistory.Count}");
	}

	private static void ValidateCharacterRosterBasics(List<string> warnings)
	{
		var characters = CharacterRoster.GetAll();
		if (characters.Count == 0)
		{
			warnings.Add("Character roster returned zero entries.");
			return;
		}

		foreach (CharacterData character in characters)
		{
			if (character.StartingSpellResource == null)
				warnings.Add($"Character '{character.Id}' missing starting spell resource.");
		}
	}

	// The starting roster's one real invariant: a character is its element. Each playable character
	// opens with a spell carrying a single element weight, and no two of them open the same one, so
	// picking a character is picking which set of element thresholds the run starts pointed at.
	//
	// Both halves rot silently. A starting spell quietly retagged with a second element (Magic
	// Missile carries Arcane *and* Lightning, which is why it is no longer a starter) splits the
	// character's opening weight without changing anything visible; two characters sharing an
	// element makes one of them redundant without breaking a thing.
	// Two rules that only exist as agreement between data files, so nothing but this notices when
	// they break.
	//
	// 1. No spell carries the same element twice in its BASE tags. A spell used to be able to be
	//    "Fire x2", which made a single-element spell worth as much element weight as a hybrid and
	//    left nothing to distinguish the two. Weight is now capped at 1 per element, and what a
	//    pure spell gets instead is attunement (Player.GetAttunementMultiplier). Evolutions may
	//    still stack a second tag of the same element - that is the level 4 and level 8 choice -
	//    which is why this checks the resource's declared weights and not GetElementWeights().
	//
	// 2. Attunement is only worth anything to a spell that deals damage, so a pure spell that is
	//    passive gets no compensation at all for its halved element weight. Passives must carry
	//    two elements.
	private static void ValidateElementTags(List<string> warnings)
	{
		// One pass, and every Godot array is materialised into a plain List before it is read more
		// than once. Enumerating a Godot.Collections.Array creates a fresh managed wrapper per
		// element per enumeration, so re-walking Level4Options inside a loop over Level8Options
		// produced enough short-lived wrappers to crash the finaliser mid-validation.
		foreach (SpellData spell in ContentValidator.LoadAllSpells())
		{
			if (spell?.ElementWeights == null)
				continue;

			foreach (var pair in spell.ElementWeights)
			{
				if (pair.Value > 1)
					warnings.Add($"Spell '{spell.Id}' declares {pair.Key} with weight {pair.Value}; base element weights are capped at 1 per element, and depth comes from evolutions instead.");
			}

			SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);

			var baseWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			foreach (var pair in spell.ElementWeights)
				baseWeights[pair.Key] = pair.Value;

			List<(string Id, Dictionary<string, int> Gains)> level4 = MaterialiseGains(spell.Level4Options, spell.Id, warnings);
			List<(string Id, Dictionary<string, int> Gains)> level8 = MaterialiseGains(spell.Level8Options, spell.Id, warnings);
			ValidateEvolutionElementCeiling(spell.Id, baseWeights, level4, level8, warnings);
		}
	}

	// Flattens a milestone's options to plain data, checking each named element exists on the way -
	// an unknown element name is dropped in silence by SpellData.ApplyBonusElementWeights, so the
	// branch would simply grant nothing. A null entry stands for "took neither option".
	private static List<(string Id, Dictionary<string, int> Gains)> MaterialiseGains(
		Godot.Collections.Array<SpellEvolutionOption> options, string spellId, List<string> warnings)
	{
		var result = new List<(string, Dictionary<string, int>)> { ("none", new Dictionary<string, int>()) };
		if (options == null)
			return result;

		foreach (SpellEvolutionOption evo in options)
		{
			var gains = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			if (evo?.BonusElementWeights != null)
			{
				foreach (var pair in evo.BonusElementWeights)
				{
					if (!Enum.TryParse<Element>(pair.Key, true, out _))
					{
						warnings.Add($"Evolution '{evo.Id}' on '{spellId}' grants element '{pair.Key}', which is not a known element, so the branch grants nothing.");
						continue;
					}

					gains[pair.Key] = pair.Value;
				}
			}

			result.Add((evo?.Id ?? "none", gains));
		}

		return result;
	}

	// Two ceilings that only hold by agreement between catalog entries, and that a player would
	// find by out-scaling the game rather than by anything looking broken:
	//
	//   1. No spell may end up naming more than TWO distinct elements. A hybrid has both of its
	//      slots filled already, so its evolutions may only deepen what it carries; branching to a
	//      new element is what a pure spell trades its attunement for, and is its alone.
	//   2. Only a pure spell may reach 3 instances of one element. A hybrid able to put both of its
	//      gains into the same element would match a pure spell's depth and keep a second element
	//      as well, which is most of the reason to ever pick a pure spell.
	//
	// Checked over every level 4 x level 8 pairing, because both ceilings are properties of the
	// combination rather than of either option alone.
	private static void ValidateEvolutionElementCeiling(
		string spellId,
		Dictionary<string, int> baseWeights,
		List<(string Id, Dictionary<string, int> Gains)> level4,
		List<(string Id, Dictionary<string, int> Gains)> level8,
		List<string> warnings)
	{
		bool isPure = baseWeights.Count == 1;

		foreach (var early in level4)
		{
			foreach (var late in level8)
			{
				var totals = new Dictionary<string, int>(baseWeights, StringComparer.OrdinalIgnoreCase);
				foreach (var gains in new[] { early.Gains, late.Gains })
				{
					foreach (var pair in gains)
						totals[pair.Key] = totals.TryGetValue(pair.Key, out int existing) ? existing + pair.Value : pair.Value;
				}

				if (totals.Count > 2)
					warnings.Add($"Spell '{spellId}' can reach {totals.Count} elements via '{early.Id}' + '{late.Id}'; a spell may never name more than two.");

				if (isPure)
					continue;

				foreach (var pair in totals)
				{
					if (pair.Value >= 3)
						warnings.Add($"Hybrid '{spellId}' can reach {pair.Value} {pair.Key} via '{early.Id}' + '{late.Id}'; only a pure spell may reach 3 of one element.");
				}
			}
		}
	}

	private static void ValidateStartingCharacters(List<string> warnings)
	{
		var elementOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		foreach (CharacterData character in CharacterRoster.GetAll())
		{
			// The Test Wizard picks its loadout on the selection screen, so it has no fixed
			// starting element to check and is exempt by design.
			if (character == null || character.Id.Equals("test_wizard", StringComparison.OrdinalIgnoreCase))
				continue;

			if (!string.IsNullOrWhiteSpace(character.StartingPassiveId)
				&& !Player.CharacterPassiveBonusIds.Contains(character.StartingPassiveId.Trim()))
			{
				warnings.Add($"Character '{character.Id}' names starting passive '{character.StartingPassiveId}', which is not a recognised bonus id, so it silently grants nothing.");
			}

			SpellData starter = character.StartingSpellResource;
			if (starter == null)
				continue;

			if (!GlobalStatsManager.IsSpellUnlockedForLevelUp(null, starter.Id))
				warnings.Add($"Character '{character.Id}' starts with '{starter.Id}', which is not default-unlocked, so it can never be re-offered or upgraded from the level-up pool.");

			var weights = starter.ElementWeights;
			if (weights == null || weights.Count == 0)
			{
				warnings.Add($"Character '{character.Id}' starts with '{starter.Id}', which carries no element weight, so the character opens pointed at no element at all.");
				continue;
			}

			if (weights.Count > 1)
			{
				warnings.Add($"Character '{character.Id}' starts with '{starter.Id}', which carries {weights.Count} elements; a starter must carry exactly one so the character's opening weight is not split.");
				continue;
			}

			string element = string.Empty;
			foreach (Variant key in weights.Keys)
			{
				element = key.AsString();
				break;
			}

			if (elementOwners.TryGetValue(element, out string owner))
				warnings.Add($"Characters '{owner}' and '{character.Id}' both open on {element}; each starting character should claim a different element.");
			else
				elementOwners[element] = character.Id;
		}
	}

	// The check that would have caught the whole dead economy.
	//
	// Every spell and every achievement reward used to line up perfectly on paper while granting
	// nothing at all, because the thing that decided what was locked - a 35-entry default-unlocked
	// set - lived somewhere else and contained everything. Nothing compared the two. These checks
	// are all cross-references between catalogs that must agree, and every one of them fails
	// silently in play: an unreachable spell just never appears, and a reward that grants something
	// the player already owns looks exactly like one that worked.
	private static void ValidateUnlockCatalog(List<string> warnings)
	{
		var seen = new Dictionary<string, UnlockDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (UnlockDefinition definition in UnlockCatalog.All)
		{
			string key = $"{definition.Kind}:{definition.Id}";
			if (seen.ContainsKey(key))
				warnings.Add($"Unlock catalog has two entries for {definition.Kind} '{definition.Id}'; each thing must have exactly one source.");
			else
				seen[key] = definition;

			if (definition.Source != UnlockSource.Starter && string.IsNullOrWhiteSpace(definition.LockedHint))
				warnings.Add($"Unlock '{definition.Id}' has no LockedHint, so its locked card cannot tell the player how to get it.");

			if (definition.Source == UnlockSource.Purchase && definition.PurchaseCost <= 0)
				warnings.Add($"Unlock '{definition.Id}' is purchasable for {definition.PurchaseCost} Arcane Energy, so it is free.");

			if (definition.Source == UnlockSource.Achievement
				&& AchievementDefinitions.GetById(definition.SourceId) == null)
			{
				warnings.Add($"Unlock '{definition.Id}' points at achievement '{definition.SourceId}', which does not exist, so it can never be earned.");
			}
		}

		// Every spell the game can offer needs a source. A spell with no entry is locked forever -
		// which is the safe direction, but only if somebody is told about it.
		foreach (string path in ContentValidator.SpellResourcePaths)
		{
			var spell = GD.Load<SpellData>(path);
			if (spell == null || string.IsNullOrWhiteSpace(spell.Id))
				continue;

			if (UnlockCatalog.GetSpell(spell.Id) == null)
				warnings.Add($"Spell '{spell.Id}' has no unlock catalog entry, so it can never be offered at level-up.");
		}

		foreach (CharacterData character in CharacterRoster.GetAll())
		{
			if (character != null && UnlockCatalog.GetCharacter(character.Id) == null)
				warnings.Add($"Character '{character.Id}' has no unlock catalog entry, so it can never be selected.");
		}

		// The other direction: an achievement that grants something the catalog says comes from
		// somewhere else. Both halves would look correct on their own.
		foreach (AchievementDefinition achievement in AchievementDefinitions.All)
		{
			if (string.IsNullOrWhiteSpace(achievement.SpellUnlockId))
				continue;

			UnlockDefinition definition = UnlockCatalog.GetSpell(achievement.SpellUnlockId);
			if (definition == null)
				warnings.Add($"Achievement '{achievement.Id}' grants spell '{achievement.SpellUnlockId}', which has no unlock catalog entry.");
			else if (definition.Source != UnlockSource.Achievement || !definition.SourceId.Equals(achievement.Id, StringComparison.OrdinalIgnoreCase))
				warnings.Add($"Achievement '{achievement.Id}' grants '{achievement.SpellUnlockId}', but the catalog sources that from {definition.Source} '{definition.SourceId}' - one of the two is wrong.");
		}

		// A fresh save must be able to start: at least one chapter open, and every starting
		// character's opening spell available on turn one.
		var fresh = new SaveData();
		if (!StageCatalog.All.Any(s => GlobalStatsManager.IsStageAvailable(fresh, s)))
			warnings.Add("No chapter is available on a new save, so a fresh game cannot be started.");

		if (GlobalStatsManager.IsCampaignComplete(fresh))
			warnings.Add("A brand-new save already satisfies the campaign gate, so the final chapter is not gated by anything.");

		ValidateCuration(fresh, warnings);
	}

	// Achievements are measured, not asserted: one function produces both "am I done" and "how far
	// am I", so they cannot disagree. What can still go wrong is a definition that is unreachable,
	// unmeasurable, or already satisfied - and every one of those is invisible in play, because an
	// achievement that never fires looks exactly like one the player has not earned yet.
	private static void ValidateAchievements(List<string> warnings)
	{
		var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var fresh = AchievementContext.ForSave(new SaveData());

		foreach (AchievementDefinition achievement in AchievementDefinitions.All)
		{
			if (string.IsNullOrWhiteSpace(achievement.Id))
			{
				warnings.Add("An achievement has no Id, so it can never be recorded as unlocked.");
				continue;
			}

			if (!ids.Add(achievement.Id))
				warnings.Add($"Two achievements share the id '{achievement.Id}'; the second can never be earned separately.");

			if (achievement.Target <= 0f)
				warnings.Add($"Achievement '{achievement.Id}' has a target of {achievement.Target}, so it completes instantly.");

			if (achievement.Measure == null)
			{
				warnings.Add($"Achievement '{achievement.Id}' has no Measure, so it can never complete.");
				continue;
			}

			// A brand-new save must satisfy nothing. One that does is either mis-measured or has a
			// target of zero, and would fire on the player's very first run for no reason.
			if (achievement.IsComplete(fresh))
				warnings.Add($"Achievement '{achievement.Id}' is already complete on a new save.");

			bool grantsSomething = !string.IsNullOrWhiteSpace(achievement.SpellUnlockId)
				|| !string.IsNullOrWhiteSpace(achievement.CharacterUnlockId)
				|| achievement.CurrencyReward > 0;
			if (!grantsSomething)
				warnings.Add($"Achievement '{achievement.Id}' grants nothing, so completing it is not a reward.");

			if (!string.IsNullOrWhiteSpace(achievement.CharacterUnlockId)
				&& UnlockCatalog.GetCharacter(achievement.CharacterUnlockId) == null)
			{
				warnings.Add($"Achievement '{achievement.Id}' frees '{achievement.CharacterUnlockId}', which has no unlock catalog entry.");
			}
		}
	}

	// Curation's two bounds are easy to break from opposite directions and both fail quietly: too
	// generous and a player can narrow the pool until every level-up is the same three cards, too
	// strict and the shop sells a row that can never be bought.
	private static void ValidateCuration(SaveData fresh, List<string> warnings)
	{
		if (GlobalStatsManager.GetCurationSlotCapacity(fresh) != 0)
		{
			warnings.Add("A new save can already buy Redactions; curation is meant to be unavailable "
				+ "until the spellbook has grown past the minimum offerable pool.");
		}

		// With every spell unlocked, capacity must still leave the floor intact.
		var full = new SaveData();
		foreach (string id in UnlockCatalog.AllSpellIds)
			GlobalStatsManager.UnlockSpell(full, id);

		int unlocked = GlobalStatsManager.GetUnlockedSpellCount(full);
		int capacity = GlobalStatsManager.GetCurationSlotCapacity(full);
		if (unlocked - capacity < GlobalStatsManager.MinimumOfferablePool)
		{
			warnings.Add($"With every spell unlocked ({unlocked}), curation capacity {capacity} would allow the "
				+ $"pool to fall below the {GlobalStatsManager.MinimumOfferablePool}-spell floor.");
		}

		// And the floor must actually hold when the player spends every slot.
		full.ArcaneUpgradeLevels[GlobalStatsManager.CurationUpgradeId] = capacity;
		int removed = 0;
		foreach (string id in UnlockCatalog.AllSpellIds.ToList())
		{
			if (GlobalStatsManager.SetSpellRemovedFromPool(full, id, true))
				removed++;
		}

		if (unlocked - removed < GlobalStatsManager.MinimumOfferablePool)
		{
			warnings.Add($"Spending every Redaction removed {removed} of {unlocked} spells, leaving fewer than "
				+ $"the {GlobalStatsManager.MinimumOfferablePool} the floor promises.");
		}
	}

	private static void ValidateSaveDefaults(List<string> warnings)
	{
		var save = new SaveData();
		if (!save.EnableGameplayOnboardingTips)
			warnings.Add("New saves should enable onboarding tips by default.");

		if (save.HasToggledOnboardingTipsAtLeastOnce)
			warnings.Add("New saves should start with HasToggledOnboardingTipsAtLeastOnce = false.");

		if (save.PlaytestModeEnabled)
			warnings.Add("New saves should start with PlaytestModeEnabled = false.");

		if (!string.Equals(save.BalancePresetId, GlobalStatsManager.BalancePresetDefault, StringComparison.OrdinalIgnoreCase))
			warnings.Add($"New saves should default to '{GlobalStatsManager.BalancePresetDefault}' preset, got '{save.BalancePresetId}'.");
	}

	private static void ValidatePresetRewardOrdering(List<string> warnings)
	{
		float casual = GlobalStatsManager.GetArcaneRewardScaleForPreset(GlobalStatsManager.BalancePresetCasual);
		float normal = GlobalStatsManager.GetArcaneRewardScaleForPreset(GlobalStatsManager.BalancePresetDefault);
		float hardcore = GlobalStatsManager.GetArcaneRewardScaleForPreset(GlobalStatsManager.BalancePresetHardcore);

		if (!(casual < normal && normal < hardcore))
			warnings.Add($"Preset reward ordering should be Casual < Default < Hardcore, got {casual:0.00}, {normal:0.00}, {hardcore:0.00}.");
	}

	private static void ValidatePlaytestChecklistDefaults(List<string> warnings)
	{
		var save = new SaveData();
		if (save.PlaytestChecklistState == null)
			warnings.Add("Playtest checklist state should default to an empty map.");

		if (save.PlaytestChecklistState != null && save.PlaytestChecklistState.Count != 0)
			warnings.Add($"Playtest checklist defaults should be empty, got {save.PlaytestChecklistState.Count} entries.");
	}
}
