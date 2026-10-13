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

	private Node game;
	private Player player;
	private float elapsed;
	private bool finished;
	private int levelUpsHandled;
	private float lastProgressLog;

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
			else if (arg.StartsWith("--xp-first=")) XpFirst = int.Parse(arg.Substring(11));
			else if (arg.StartsWith("--xp-step=")) XpStep = int.Parse(arg.Substring(10));
			else if (arg.StartsWith("--upgrade-chance=")) UpgradeChance = float.Parse(arg.Substring(17), System.Globalization.CultureInfo.InvariantCulture);
		}

		// Immune to the pause. A level-up pauses the tree, and a driver that pauses with it can
		// never make the choice that would unpause it.
		ProcessMode = ProcessModeEnum.Always;

		GD.Print($"HARNESS minutes={Minutes} timescale={TimeScale} character={CharacterIndex} seed={Seed}");

		GD.Seed((ulong)Seed);
		Global.SelectedCharacterIdx = CharacterIndex;
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
			if (UpgradeChance >= 0f)
				player.UpgradeOfferChance = UpgradeChance;
			if (XpFirst >= 0) player.XpFirstLevelCost = XpFirst;
			if (XpStep >= 0) player.XpCostPerLevel = XpStep;
			player.XPToNextLevel = player.CalculateXPForLevel(player.CurrentLevel);
			GD.Print($"HARNESS upgrade chance {player.UpgradeOfferChance:0.00}, xp curve {player.XpFirstLevelCost} + {player.XpCostPerLevel}/level");
		}

		if (elapsed - lastProgressLog >= 30f)
		{
			lastProgressLog = elapsed;
			GD.Print($"HARNESS t={elapsed:0}s level={player.CurrentLevel} enemies={GetTree().GetNodesInGroup("enemies").Count}");
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
		if (options.Count == 0 && ascensions.Count == 0)
			return;

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
		LevelUpOption pick = options.FirstOrDefault(o => o.IsNewUnlock && !o.RequiresSlotSwap)
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
		LevelUpOption signature = options.FirstOrDefault(o => o.SpellId == target.SpellId && !o.RequiresSlotSwap);
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
		bool Raises(LevelUpOption o) => !o.RequiresSlotSwap && Carries(o) && (o.IsNewUnlock || o.IsBoon
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
			if (charges.Auguries > 0 && auguriesUsed < 3 && !auguryUsedThisLevel && slotsUsed < 6 && charges.AuguryCandidates != null)
			{
				LevelUpOption named = charges.AuguryCandidates
					.Where(o => o.IsNewUnlock && !o.IsBoon && Carries(o))
					.OrderBy(o => o.SpellElementTags.Count)
					.FirstOrDefault();
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
			.Where(o => !o.RequiresSlotSwap && !o.IsNewUnlock && Carries(o))
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
			?? options.FirstOrDefault(o => !o.RequiresSlotSwap)
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
			SeekApotheosis ? $"  charges used : {rerollsUsed} rerolls, {bansUsed} bans, {auguriesUsed} auguries" : "",
			SeekApotheosis ? $"  apotheosis   : {(apotheosisTakenAt < 0f ? "never offered" : $"taken at {apotheosisTakenAt:0}s ({apotheosisTakenAt / 60f:0.0} min)")}; peak element tier {peakTier}" : "",
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
