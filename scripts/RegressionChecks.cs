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
		ValidateApotheosisCatalog(warnings);
		ValidateChestSetPresentation(warnings);
		ValidateFullSetObjects(warnings);
		ValidateTomeRelics(warnings);
		ValidateCollectorEconomy(warnings);
		ValidateBossCatalog(warnings);
		ValidateBossPatterns(warnings);
		ValidateRangedEnemies(warnings);
		ValidateMiniBoss(warnings);
		ValidateAttackPatternEnemies(warnings);
		ValidateBestiary(warnings);
		ValidateStartingCharacters(warnings);
		ValidateElementTags(warnings);
		ValidateElementPaths(warnings);
		ValidateEvolutionReaders(warnings);
		ValidateLevelUpReaders(warnings);
		ValidateStarfall(warnings);
		ValidateCycloneGust(warnings);
		ValidateFear(warnings);
		ValidateSecretCatalog(warnings);
		ValidateStageRewards(warnings);
		ValidateWorldMap(warnings);
		ValidateBossRush(warnings);
		ValidateHazardPlacement(warnings);
		ValidateFamiliar(warnings);
		ValidateSpellVariants(warnings);
		ValidateSpiritualWeapon(warnings);
		ValidateFacingFamily(warnings);
		ValidateSpawnFormations(warnings);
		ValidateSwarmPressure(warnings);
		ValidateBoonSources(warnings);
		ValidateBoonPresentation(warnings);
		ValidateChestRarity(warnings);
		ValidateElementReachability(warnings);
		ValidateUnlockCatalog(warnings);
		ValidateAchievements(warnings);
		ValidateStageCatalog(warnings);
		ValidateCampaign(warnings);
		ValidateCampaignMigration(warnings);
		ValidateGateReachability(warnings);
		ValidateAchievementMigration(warnings);
		ValidateRetiredChargeRefund(warnings);
		ValidateNoPixelsInText(warnings);
		ValidateAudio(warnings);
		ValidateMusic(warnings);
		ValidateInputMap(warnings);
		ValidateBlockingTerrain(warnings);
		ValidateTerrainSeams(warnings);
		ValidateWaterShapes(warnings);
		ValidateGroundArt(warnings);
		ValidateNavigation(warnings);
		ValidateMazeHazards(warnings);
		ValidateStageHazards(warnings);
		ValidateStatusEffectAtlas(warnings);
		ValidateFlowerPen(warnings);
		ValidateSpellClassificationText(warnings);
		ValidateExploreArt(warnings);
		ValidateStageHints(warnings);
		ValidateGravewell(warnings);
		ValidateCraters(warnings);
		ValidateEvolutionIcons(warnings);
		return warnings;
	}

	// The chapter hints (StageHintCatalog) name things four other catalogs own - a chapter, a
	// discovery site, the side event that stands on it, a sound - and every one of them fails
	// silently in a run: an unknown stage never plays, a site its event does not pay never comes
	// from anywhere (SideEventDirector only starts a hint whose site is its own event's), a missing
	// sound is a caption with nothing under it. And one rule a build cannot see: a hint never says
	// the name of what it is about. A rescued wizard's name is his reward, and a found spell's name
	// in a caption is the answer printed on the question.
	private static void ValidateStageHints(List<string> warnings)
	{
		var seen = new HashSet<string>();
		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (StageHint hint in StageHintCatalog.All)
		{
			string at = $"Hint {hint.StageId}";
			if (!seen.Add(hint.StageId))
				warnings.Add($"{at}: a chapter has two hints; ForStage only ever plays the first.");
			if (StageCatalog.All.All(s => s.Id != hint.StageId))
				warnings.Add($"{at}: no such chapter in StageCatalog.");

			var entry = SideEventDirector.ForStage(hint.StageId);
			if (entry == null)
				warnings.Add($"{at}: the chapter has no side event, so the hint has nowhere to come from.");
			else if (entry.Value.SiteId != hint.SiteId)
				warnings.Add($"{at}: names site '{hint.SiteId}', but the chapter's event pays '{entry.Value.SiteId}'; it will never play.");

			var gives = UnlockCatalog.ForDiscoverySite(hint.SiteId).ToList();
			if (gives.Count == 0)
				warnings.Add($"{at}: site '{hint.SiteId}' gives nothing in UnlockCatalog, so there is never anything to hint at.");

			if (!SfxCatalog.AllNames.Contains(hint.Sound))
				warnings.Add($"{at}: sound '{hint.Sound}' is not in SfxCatalog (tools/audio/sfx.py, then the catalog).");

			if (hint.Lines.Length == 0)
				warnings.Add($"{at}: has no lines.");

			names.Clear();
			foreach (UnlockDefinition give in gives)
			{
				names.Add(give.Id.Replace('_', ' '));
				if (give.Kind == UnlockKind.Character)
				{
					string name = CharacterRoster.GetAll().FirstOrDefault(c => c.Id == give.Id)?.Name;
					if (!string.IsNullOrWhiteSpace(name))
						names.Add(name);
				}
				else
				{
					string name = ContentValidator.LoadAllSpells().FirstOrDefault(s => s.Id == give.Id)?.Name;
					if (!string.IsNullOrWhiteSpace(name))
						names.Add(name);
				}
			}
			foreach (string line in hint.Lines)
			{
				if (string.IsNullOrWhiteSpace(line))
					warnings.Add($"{at}: has a blank line.");
				else if (line.Length > StageHintCatalog.MaxLineLength)
					warnings.Add($"{at}: \"{line}\" is {line.Length} characters (limit {StageHintCatalog.MaxLineLength}); it will not fit two lines on a phone.");
				foreach (string name in names)
				{
					if (line.Contains(name, StringComparison.OrdinalIgnoreCase))
						warnings.Add($"{at}: \"{line}\" names '{name}', which is the reward, not the hint.");
				}
			}
		}
	}

	// Gravewell's pit is a sheet read by frame index, its collapse a shader loaded by path, and its
	// trigger is drawn as the art's bank - all three invisible to a build, and every one of them fails
	// silently in a run: a null texture draws nothing (which is how the spell came to read as nothing
	// at all), a missing shader draws no collapse, and a trigger moved without the art draws a bank
	// that lies about what springs it. Re-run: python tools/art/gravewell.py, then import.
	private static void ValidateGravewell(List<string> warnings)
	{
		const string scenePath = "res://scenes/Gravewell.tscn";
		if (GD.Load<PackedScene>(scenePath)?.Instantiate() is not GravewellTrap pit)
		{
			warnings.Add($"Gravewell: {scenePath} did not instantiate as a GravewellTrap.");
			return;
		}
		var pitSize = new Vector2(GravewellTrap.PitCell * (GravewellTrap.OpenFrames + GravewellTrap.ArmedFrames + GravewellTrap.CollapseFrames), GravewellTrap.PitCell);
		var graspSize = new Vector2(GravewellTrap.GraspW * GravewellTrap.GraspFrames, GravewellTrap.GraspH);
		if (pit.PitTexture == null)
			warnings.Add("Gravewell: the pit sheet is missing or not imported; the trap will be invisible.");
		else if (pit.PitTexture.GetSize() != pitSize)
			warnings.Add($"Gravewell: the pit sheet is {pit.PitTexture.GetSize()}, expected {pitSize}.");
		if (pit.GraspTexture == null)
			warnings.Add("Gravewell: the grasp sheet is missing or not imported; caught enemies will not read as held.");
		else if (pit.GraspTexture.GetSize() != graspSize)
			warnings.Add($"Gravewell: the grasp sheet is {pit.GraspTexture.GetSize()}, expected {graspSize}.");
		if (!ResourceLoader.Exists(GravewellTrap.CollapseShaderPath))
			warnings.Add($"Gravewell: {GravewellTrap.CollapseShaderPath} is missing; the collapse will draw nothing.");
		if (MathF.Abs(pit.TriggerRadius - GravewellTrap.ArtBankRadius * 2f) > 4f)
			warnings.Add($"Gravewell: TriggerRadius {pit.TriggerRadius} no longer matches the bank drawn at {GravewellTrap.ArtBankRadius * 2f} px.");
		pit.Free();
	}

	// The craters (Meteor Swarm, Cataclysm) are a shader loaded by path and a fire sheet read by cell
	// and row, and both fail silently in a run: a missing shader draws every crater as a plain quad of
	// its parameters' colour, an unimported sheet draws no fire at all, and a sheet of another shape
	// draws the wrong rows. Re-run: python tools/art/crater_fire.py, then import.
	private static void ValidateCraters(List<string> warnings)
	{
		if (!ResourceLoader.Exists(CraterGround.ShaderPath))
			warnings.Add($"Craters: {CraterGround.ShaderPath} is missing; every crater would draw as a flat quad.");
		Texture2D fire = ResourceLoader.Exists(CraterFire.TexturePath) ? GD.Load<Texture2D>(CraterFire.TexturePath) : null;
		var size = new Vector2(CraterFire.CellW * CraterFire.Frames, CraterFire.CellH * (CraterFire.RowSmoke + 1));
		if (fire == null)
			warnings.Add($"Craters: {CraterFire.TexturePath} is missing or not imported; craters would burn with no fire.");
		else if (fire.GetSize() != size)
			warnings.Add($"Craters: the fire sheet is {fire.GetSize()}, expected {size} ({CraterFire.Frames} frames of {CraterFire.CellW}x{CraterFire.CellH} in {CraterFire.RowSmoke + 1} rows).");
	}

	// Starfall (.ai/spell-roster.md, "Starfall") is one node the Player makes from its StarfallScene
	// export. Unassigned, FireStarfall returns NoTarget every frame and the spell owns a slot and does
	// nothing, with no error anywhere; a scene that is not a Starfall fails the cast the same way. Its
	// two ascensions and two of its level 4 cards (Shooting Stars, Skipping Stars) are read by id
	// (HasEvolution), so an option renamed in the catalog would leave a card that changes nothing - the
	// ids are checked against the options the spell actually offers.
	private static void ValidateStarfall(List<string> warnings)
	{
		const string scenePath = "res://scenes/Starfall.tscn";
		if (GD.Load<PackedScene>(scenePath)?.Instantiate() is not Starfall rain)
			warnings.Add($"Starfall: {scenePath} did not instantiate as a Starfall; the spell will never fall.");
		else
		{
			if (rain.BaseStarRadius <= 0f || rain.FallSeconds <= 0f)
				warnings.Add($"Starfall: BaseStarRadius {rain.BaseStarRadius} and FallSeconds {rain.FallSeconds} must both be positive.");
			rain.Free();
		}

		SceneState player = GD.Load<PackedScene>("res://scenes/player.tscn")?.GetState();
		bool assigned = false;
		for (int i = 0; player != null && i < player.GetNodePropertyCount(0); i++)
			assigned |= player.GetNodePropertyName(0, i) == "StarfallScene";
		if (!assigned)
			warnings.Add("Starfall: player.tscn does not assign StarfallScene; the spell would take a slot and do nothing.");

		SpellData spell = ContentValidator.LoadAllSpells().FirstOrDefault(s => s?.Id == "starfall");
		if (spell == null)
		{
			warnings.Add("Starfall: SpellData_Starfall.tres is not in ContentValidator.SpellResourcePaths.");
			return;
		}
		SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
		var level4 = spell.Level4Options.Select(o => o?.Id).ToList();
		var level8 = spell.Level8Options.Select(o => o?.Id).ToList();
		foreach (string id in new[] { Starfall.ShootingStarsId, Starfall.FrostfallId, Starfall.SkippingStarsId })
			if (!level4.Contains(id))
				warnings.Add($"Starfall: level 4 offers no '{id}'.");
		foreach (string id in new[] { Starfall.StarburstId, Starfall.ConstellationId })
			if (!level8.Contains(id))
				warnings.Add($"Starfall: level 8 offers no '{id}', so the node's behaviour for it is unreachable.");
	}

	// The stage secrets (issue #20, .ai/stage-secrets.md section 7.5). Every rule here guards something a
	// run cannot show: a secret that paid a spell, a wizard or a boon would put the campaign's last gate
	// behind a seed and a walk (there is no enum value for one, which is the first half of the rule);
	// a found level of Growth, Amount or a charge would undercut the Apotheosis floor measured with the
	// shop at its cap; and two keepsakes of one stat would let a save find it twice.
	// THE BOSS RUSH (issue #12, BossRushCatalog). Its roster is derived, not written, so what can go
	// wrong is the derivation: a campaign boss dropped or doubled, a finale that is not the arena's
	// (the Emberdeep's, StageCatalog.GateChapter: the run would end on the wrong fight - and a castle
	// boss built before C2's RoadOrder would land after it), or arrivals that are not strictly before the
	// ordinary boss time and in order (two bosses at once, or one after the finale has already won).
	// Traps fit their map (2026-10-28, "there shouldn't be fire vents outside. double check that all
	// traps make sense for the map they're in"). A flame vent is an iron grate set in a floor: indoors
	// only. Spike plates are built too: only where somebody built things. The lists are the decision.
	// The castle's three are written in now (.ai/endgame-castle.md 1.9) so its chapters' agents never edit
	// this file to place a trap: the Court and the Throne are indoors, and the bridge is built though open.
	private static readonly StageEnvironmentKind[] IndoorChapters =
		{ StageEnvironmentKind.Castle, StageEnvironmentKind.Court, StageEnvironmentKind.Throne };
	private static readonly StageEnvironmentKind[] BuiltChapters =
	{
		StageEnvironmentKind.Castle, StageEnvironmentKind.Ruins,
		StageEnvironmentKind.Bridge, StageEnvironmentKind.Court, StageEnvironmentKind.Throne,
	};

	private static void ValidateHazardPlacement(List<string> warnings)
	{
		foreach (StageEnvironmentKind kind in Enum.GetValues<StageEnvironmentKind>())
		{
			StageEnvironmentProfile profile = StageEnvironmentCatalog.Get(kind);
			if (profile.HazardFlameChance > 0f && !IndoorChapters.Contains(kind))
				warnings.Add($"Hazards: {kind} places flame vents ({profile.HazardFlameChance:0.##}), but it is outdoors - a vent is a grate in a floor.");
			if (profile.HazardSpikes && !BuiltChapters.Contains(kind))
				warnings.Add($"Hazards: {kind} places spike plates, but nobody built anything there.");
		}
	}

	private static void ValidateBossRush(List<string> warnings)
	{
		IReadOnlyList<BossDefinition> roster = BossRushCatalog.Roster;
		BossDefinition finale = BossRushCatalog.Finale;
		if (roster.Count < 2)
		{
			warnings.Add($"Boss Rush: the roster has {roster.Count} bosses; a rush needs at least two.");
			return;
		}
		if (finale == null || !ReferenceEquals(roster[roster.Count - 1], finale))
			warnings.Add($"Boss Rush: the roster ends on '{roster[roster.Count - 1].Id}', not the arena's boss '{finale?.Id}' ({BossRushCatalog.Arena?.DisplayName}).");
		if (roster.Select(b => b.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != roster.Count)
			warnings.Add("Boss Rush: a boss is in the roster twice.");
		foreach (BossDefinition boss in roster)
		{
			int stage = BossCatalog.StageIndexOf(boss);
			if (stage < 0 || StageCatalog.CampaignPositionOf(StageCatalog.GetByIndex(stage)?.Id) < 0)
				warnings.Add($"Boss Rush: '{boss.Id}' is not a campaign chapter's boss.");
		}

		float previous = 0f;
		for (int i = 0; i < roster.Count; i++)
		{
			float at = BossRushCatalog.ArrivalPaceSeconds(i, roster.Count, Node2DGame.ReferenceRunSeconds);
			if (at <= previous || at > Node2DGame.ReferenceRunSeconds)
				warnings.Add($"Boss Rush: boss {i + 1} ('{roster[i].Id}') arrives at pace {at:0}s, after {previous:0}s; arrivals must climb to {Node2DGame.ReferenceRunSeconds:0}s.");
			previous = at;
		}
		if (!Mathf.IsEqualApprox(previous, Node2DGame.ReferenceRunSeconds))
			warnings.Add($"Boss Rush: the finale arrives at pace {previous:0}s, not at the ordinary boss time.");
	}

	// THE SECRETS (.ai/stage-secrets.md 7.5, with .ai/achievements-and-secrets.md 6.2's revised payment
	// rule): every secret written down, built or planned. A secret may pay a spell now - only a spell
	// vault, one a chapter, and only a spell the unlock catalog sources from that vault's deed - and
	// still never a wizard or a boon (SecretMetaReward has no value for either).
	private static void ValidateSecretCatalog(List<string> warnings)
	{
		var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var foundStats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var keepsakesByStage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		var vaultsByStage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, string> spellNames = ContentValidator.LoadAllSpells()
			.Where(s => s != null && !string.IsNullOrWhiteSpace(s.Id))
			.GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);
		foreach (SecretDefinition secret in SecretCatalog.Defined)
		{
			if (!ids.Add(secret.Id))
				warnings.Add($"Secrets: '{secret.Id}' is listed twice.");
			StageDefinition stage = StageCatalog.GetById(secret.StageId);
			if (stage == null)
				warnings.Add($"Secrets: '{secret.Id}' names a stage that is not in StageCatalog: '{secret.StageId}'.");
			if (string.IsNullOrWhiteSpace(secret.DisplayName) || string.IsNullOrWhiteSpace(secret.FoundLine))
				warnings.Add($"Secrets: '{secret.Id}' has no name or no found line for the Secrets page.");
			// Its deed is Hidden, and a hidden achievement once seen shows what was seen (4.3).
			if (string.IsNullOrWhiteSpace(secret.SeenLine))
				warnings.Add($"Secrets: '{secret.Id}' has no seen line, so its achievement has nothing to say once its tell has been seen.");
			if (secret.IsKeepsake && secret.IsVault)
				warnings.Add($"Secrets: '{secret.Id}' is both a keepsake and a spell vault; a chapter's keepsake and its vault are two secrets.");
			// How a built secret gets into a run (SecretDirector, piece A4): a placed one names its node, a
			// custom one its install; and a vault is placed as a vault (6.4), a keepsake as a keepsake.
			if (secret.IsBuilt && (secret.Placement == SecretPlacement.Custom ? secret.Install == null : secret.CreateSite == null))
				warnings.Add($"Secrets: '{secret.Id}' is built but has no {(secret.Placement == SecretPlacement.Custom ? "Install" : "CreateSite")}, so no run ever puts it in the world.");
			if (secret.IsVault && secret.Placement != SecretPlacement.Vault)
				warnings.Add($"Secrets: '{secret.Id}' is a spell vault placed as {secret.Placement}; a vault is placed five to seven chunks out (SecretPlacement.Vault).");
			if (secret.IsKeepsake && secret.Placement != SecretPlacement.Keepsake)
				warnings.Add($"Secrets: '{secret.Id}' is a keepsake placed as {secret.Placement}; a keepsake is placed ten to twelve chunks out (SecretPlacement.Keepsake).");

			bool paysSpell = secret.MetaReward == SecretMetaReward.Spell;
			if (paysSpell != !string.IsNullOrWhiteSpace(secret.SpellUnlockId))
				warnings.Add($"Secrets: '{secret.Id}' pays {secret.MetaReward} but names spell '{secret.SpellUnlockId}'; a spell is named exactly when one is paid.");
			if (secret.IsVault != paysSpell)
				warnings.Add($"Secrets: '{secret.Id}' {(secret.IsVault ? "is a spell vault that pays no spell" : "pays a spell but is not a spell vault")}; only a vault pays a spell, and a vault pays one.");
			if (secret.IsVault)
				vaultsByStage[secret.StageId] = (vaultsByStage.TryGetValue(secret.StageId, out int v) ? v : 0) + 1;
			if (paysSpell && !string.IsNullOrWhiteSpace(secret.SpellUnlockId))
			{
				UnlockDefinition source = UnlockCatalog.GetSpell(secret.SpellUnlockId);
				string deedId = AchievementDefinitions.SecretDeedId(secret.Id);
				if (source == null)
					warnings.Add($"Secrets: '{secret.Id}' pays spell '{secret.SpellUnlockId}', which has no unlock catalog entry.");
				else if (source.Source != UnlockSource.Achievement || !source.SourceId.Equals(deedId, StringComparison.OrdinalIgnoreCase))
					warnings.Add($"Secrets: '{secret.Id}' pays '{secret.SpellUnlockId}', but the catalog sources that spell from {source.Source} '{source.SourceId}', not this vault's deed '{deedId}'.");
				if (!spellNames.TryGetValue(secret.SpellUnlockId, out string name) || !name.Equals(secret.SpellName, StringComparison.Ordinal))
					warnings.Add($"Secrets: '{secret.Id}' names its spell '{secret.SpellName}', but '{secret.SpellUnlockId}' is called '{name ?? "(no resource)"}'.");
			}

			if (secret.IsKeepsake)
				keepsakesByStage[secret.StageId] = (keepsakesByStage.TryGetValue(secret.StageId, out int n) ? n : 0) + 1;
			else if (secret.MetaReward != SecretMetaReward.PageOnly && !secret.IsVault)
				warnings.Add($"Secrets: '{secret.Id}' is not a keepsake but pays {secret.MetaReward}; only a keepsake pays beyond the page, and a vault its spell.");
			if (secret.MetaReward == SecretMetaReward.FoundLevel)
			{
				if (!SecretCatalog.FoundLevelStats.Contains(secret.MetaRewardId, StringComparer.OrdinalIgnoreCase))
					warnings.Add($"Secrets: '{secret.Id}' pays a found level of '{secret.MetaRewardId}'; only {string.Join(", ", SecretCatalog.FoundLevelStats)} may be found.");
				else if (!foundStats.Add(secret.MetaRewardId))
					warnings.Add($"Secrets: a second keepsake pays a found level of '{secret.MetaRewardId}' ('{secret.Id}'); one of each, ever.");
			}
			if (secret.MetaReward == SecretMetaReward.ArcaneEnergy
				&& (secret.MetaAmount <= 0 || secret.MetaAmount > SecretCatalog.MaxKeepsakeArcane))
				warnings.Add($"Secrets: '{secret.Id}' pays {secret.MetaAmount} Arcane Energy; a keepsake pays 1 to {SecretCatalog.MaxKeepsakeArcane}.");
			if (secret.StageId.Equals("stage_8", StringComparison.OrdinalIgnoreCase) && secret.MetaReward != SecretMetaReward.PageOnly)
				warnings.Add($"Secrets: '{secret.Id}' is the Sketchbook's and pays {secret.MetaReward}; its secrets pay the page only.");
		}
		foreach (var (stageId, count) in keepsakesByStage)
			if (count > 1)
				warnings.Add($"Secrets: {stageId} has {count} keepsakes; a chapter has one.");
		foreach (var (stageId, count) in vaultsByStage)
			if (count > 1)
				warnings.Add($"Secrets: {stageId} has {count} spell vaults; a chapter has one at most.");
	}

	// WHAT EACH CHAPTER HOLDS (StageRewards, .ai/world-map.md section 9) is what the world map's card
	// shows as silhouettes. It is derived from four catalogs, and each of them can grow a chapter's
	// unlock that the derivation does not see: a discovery at a site no side event stands on, a reveal
	// chapter that is not a chapter, a deed for a boss with no chapter. Each is checked against the list.
	// Then the prose net: an unlock whose hint (or whose deed's description) NAMES a chapter must be on
	// that chapter's card. That is the check that would catch a new deed tied to a place in words but
	// not in data - the Coursing without its RevealStageId, say - which every other rule here would miss.
	private static void ValidateStageRewards(List<string> warnings)
	{
		IReadOnlyList<StageReward> all = StageRewards.All;
		bool OnCard(string stageId, string id) => all.Any(r =>
			r.StageId.Equals(stageId, StringComparison.OrdinalIgnoreCase) && r.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
		// A spell a chapter's vault pays is on that chapter's card as the vault's secret slot (planned
		// vaults join the card when built): the link is in data, SecretDefinition.StageId.
		bool Has(string stageId, string id) => OnCard(stageId, id) || SecretCatalog.Defined.Any(s =>
			s.StageId.Equals(stageId, StringComparison.OrdinalIgnoreCase) && s.SpellUnlockId.Equals(id, StringComparison.OrdinalIgnoreCase));

		foreach (UnlockDefinition found in UnlockCatalog.All.Where(d => !string.IsNullOrWhiteSpace(d.SiteId)))
		{
			List<StageDefinition> sites = StageCatalog.All
				.Where(s => string.Equals(global::SideEventDirector.ForStage(s.Id)?.SiteId, found.SiteId, StringComparison.OrdinalIgnoreCase))
				.ToList();
			if (sites.Count != 1)
				warnings.Add($"Stage rewards: '{found.Id}' is found at site '{found.SiteId}', which {(sites.Count == 0 ? "no chapter's side event stands on" : $"{sites.Count} chapters' side events stand on")}, so no single chapter's card can show it.");
			else if (!OnCard(sites[0].Id, found.Id))
				warnings.Add($"Stage rewards: '{found.Id}' is found in {sites[0].DisplayName} but its card does not list it.");
		}

		foreach (UnlockDefinition revealed in UnlockCatalog.All.Where(d => !string.IsNullOrWhiteSpace(d.RevealStageId)))
		{
			StageDefinition stage = StageCatalog.GetById(revealed.RevealStageId);
			if (stage == null)
				warnings.Add($"Stage rewards: '{revealed.Id}' is revealed by '{revealed.RevealStageId}', which is not a chapter.");
			else if (!OnCard(stage.Id, revealed.Id))
				warnings.Add($"Stage rewards: '{revealed.Id}' is tied to {stage.DisplayName} but its card does not list it.");
		}

		foreach (AchievementDefinition deed in AchievementDefinitions.All.Where(d => !string.IsNullOrWhiteSpace(d.BossId)))
		{
			BossDefinition boss = BossCatalog.GetById(deed.BossId);
			int index = boss != null ? BossCatalog.StageIndexOf(boss) : -1;
			if (index < 0)
			{
				warnings.Add($"Stage rewards: the deed '{deed.Id}' is for boss '{deed.BossId}', who has no chapter.");
				continue;
			}
			StageDefinition stage = StageCatalog.GetByIndex(index);
			foreach (string id in RewardIdsOf(deed))
			{
				if (!OnCard(stage.Id, id))
					warnings.Add($"Stage rewards: {stage.DisplayName}'s warden pays '{id}' ('{deed.Id}') but its card does not list it.");
			}
		}

		foreach (SecretDefinition secret in SecretCatalog.All)
		{
			if (StageCatalog.GetById(secret.StageId) != null && !OnCard(secret.StageId, secret.Id))
				warnings.Add($"Stage rewards: the secret '{secret.Id}' is in {secret.StageId} but its card does not list it.");
		}

		// The prose net.
		var texts = new List<(string Text, IEnumerable<string> Ids)>();
		foreach (UnlockDefinition unlock in UnlockCatalog.All.Where(d => d.Source != UnlockSource.Starter))
			texts.Add((unlock.LockedHint, new[] { unlock.Id }));
		foreach (AchievementDefinition deed in AchievementDefinitions.All)
		{
			List<string> ids = RewardIdsOf(deed).ToList();
			if (ids.Count > 0)
				texts.Add((deed.Description, ids));
		}
		foreach (var (text, ids) in texts)
		{
			if (string.IsNullOrWhiteSpace(text))
				continue;
			foreach (StageDefinition stage in StageCatalog.All)
			{
				string name = stage.DisplayName.StartsWith("The ", StringComparison.Ordinal) ? stage.DisplayName.Substring(4) : stage.DisplayName;
				if (text.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
					continue;
				foreach (string id in ids)
				{
					if (!Has(stage.Id, id))
						warnings.Add($"Stage rewards: '{id}' is described as {stage.DisplayName}'s (\"{text}\") but is not on its card - tie it to the chapter in data (a boss deed, a side-event site, or RevealStageId).");
				}
			}
		}

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (StageReward reward in all)
		{
			if (!seen.Add(reward.ToString()))
				warnings.Add($"Stage rewards: {reward} is listed twice.");
		}
		foreach (StageDefinition stage in StageCatalog.All.Where(s => s.IsPlayable))
		{
			if (!all.Any(r => r.StageId.Equals(stage.Id, StringComparison.OrdinalIgnoreCase)))
				warnings.Add($"Stage rewards: {stage.DisplayName} holds nothing, so its card has no row - give its boss a deed.");
		}
	}

	private static IEnumerable<string> RewardIdsOf(AchievementDefinition deed)
	{
		if (!string.IsNullOrWhiteSpace(deed.SpellUnlockId))
			yield return deed.SpellUnlockId;
		if (!string.IsNullOrWhiteSpace(deed.BoonUnlockId))
			yield return deed.BoonUnlockId;
		if (!string.IsNullOrWhiteSpace(deed.CharacterUnlockId))
			yield return deed.CharacterUnlockId;
		// Arcane Energy is listed under the deed's own id, but only for a chapter's own deed: a
		// lifetime-total deed that pays Energy belongs to no chapter and names none.
		if (deed.CurrencyReward > 0 && !string.IsNullOrWhiteSpace(deed.BossId))
			yield return deed.Id;
	}

	// THE WORLD MAP'S GEOMETRY is generated (tools/art/world_map.py -> worldmap.json), so a chapter
	// added to StageCatalog is on no map until somebody places it - and the screen would quietly leave
	// it out. Every chapter and the Boss Rush must have a stop; the road must visit the campaign in its
	// order, the seat after the last chapter, and the chapters outside the campaign after everything;
	// the Sketchbook lies off the world picture (.ai/world-map.md, section 4); and every picture the
	// screen draws must exist.
	private static void ValidateWorldMap(List<string> warnings)
	{
		WorldMapLayout map = WorldMapLayout.Load(out string problem);
		if (map == null)
		{
			warnings.Add($"World map: {problem}");
			return;
		}
		// A chapter not built yet may stand off the map for now (.ai/endgame-castle.md 1.9): the world map
		// is its own piece (C8), and drawing the castle waits on the Dungeon's move. Its missing stop is
		// then an item on that chapter's campaign to-do line (ValidateCampaignToDo), and the screen leaves
		// it out quietly (WorldMap.BuildStops). A PLAYABLE chapter with no stop is a fault, as always, so
		// none can flip with its place undrawn.
		foreach (StageDefinition stage in StageCatalog.All)
		{
			if (map.Get(stage.Id) == null && stage.IsPlayable)
				warnings.Add($"World map: {stage.DisplayName} ({stage.Id}) has no place on the map - add it to PLACES in tools/art/world_map.py and draw its landmark.");
		}
		WorldMapStop rush = map.Get(WorldMapLayout.BossRushId);
		if (rush == null)
			warnings.Add("World map: the Boss Rush has no place on the map.");

		float last = -1f;
		foreach (StageDefinition stage in StageCatalog.Campaign)
		{
			WorldMapStop stop = map.Get(stage.Id);
			if (stop == null)
				continue;
			if (stop.ArcLength <= last)
				warnings.Add($"World map: the road reaches {stage.DisplayName} out of campaign order.");
			last = Math.Max(last, stop.ArcLength);
			if (!stop.OnMap || stop.Focus.X < 0 || stop.Focus.Y < 0 || stop.Focus.X > map.Size.X || stop.Focus.Y > map.Size.Y)
				warnings.Add($"World map: {stage.DisplayName} is a campaign chapter but stands off the world picture.");
		}
		if (rush != null && rush.ArcLength <= last)
			warnings.Add("World map: the road reaches the Boss Rush's seat before the last chapter.");
		float beyond = Math.Max(last, rush?.ArcLength ?? last);
		foreach (StageDefinition stage in StageCatalog.All.Where(s => !StageCatalog.IsInCampaign(s)))
		{
			WorldMapStop stop = map.Get(stage.Id);
			if (stop == null)
				continue;
			if (stop.ArcLength <= beyond)
				warnings.Add($"World map: {stage.DisplayName} is outside the campaign but the road reaches it before the campaign's end.");
			if (stop.OnMap)
				warnings.Add($"World map: {stage.DisplayName} is outside the campaign and must lie off the world picture.");
		}

		var pictures = new List<string> { "world.png", map.PageImage, "ring-open.png", "ring-locked.png", "ring-current.png",
			"shadow.png", "secret.png", "keepsake.png", "arrow-left.png", "arrow-right.png" };
		foreach (WorldMapCompass.MarkKind kind in Enum.GetValues(typeof(WorldMapCompass.MarkKind)))
			pictures.Add("mark-" + kind.ToString().ToLowerInvariant() + ".png");
		foreach (WorldMapStop stop in map.Stops)
		{
			if (!string.IsNullOrEmpty(stop.ShroudImage))
				pictures.Add(stop.ShroudImage);
		}
		foreach (string picture in pictures)
		{
			if (!ResourceLoader.Exists(WorldMapLayout.Directory + picture))
				warnings.Add($"World map: {WorldMapLayout.Directory + picture} is missing or not imported.");
		}
		foreach (var (texture, _) in map.PageDrawings)
		{
			if (!ResourceLoader.Exists(texture))
				warnings.Add($"World map: the page's drawing {texture} is missing.");
		}
	}

	// Cyclone Slash's running whirlwind (issue #68) reads its one Ascension by id: Typhoon Core is the
	// whirlwind that never collapses. Renamed in the catalog, the card would still promise a permanent
	// hurricane and the ring would still fall in every time the player stopped.
	private static void ValidateCycloneGust(List<string> warnings)
	{
		SpellData spell = ContentValidator.LoadAllSpells().FirstOrDefault(s => s?.Id == "cyclone_slash");
		if (spell == null)
		{
			warnings.Add("Cyclone Slash: SpellData_CycloneSlash.tres is not in ContentValidator.SpellResourcePaths.");
			return;
		}
		SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
		if (!spell.Level8Options.Any(o => o?.Id == OrbitingBlade.TyphoonEvolutionId))
			warnings.Add($"Cyclone Slash: level 8 offers no '{OrbitingBlade.TyphoonEvolutionId}', so the permanent hurricane always collapses.");
	}

	// Fear (issue #69) is decided entirely inside Enemy.ApplyFear, and every way it can go wrong is
	// silent in a run: a fear that never lands, or one that lands on a boss and stops the fight. Checked
	// on a bare enemy that is never added to the tree.
	private static void ValidateFear(List<string> warnings)
	{
		const string scenePath = "res://scenes/enemy.tscn";
		if (GD.Load<PackedScene>(scenePath)?.Instantiate() is not Enemy ordinary)
		{
			warnings.Add($"Fear: {scenePath} did not instantiate as an Enemy.");
			return;
		}
		ordinary.ApplyFear(Player.FearSeconds);
		if (!ordinary.IsFeared)
			warnings.Add("Fear: an ordinary enemy did not take ApplyFear; Darkness's fear never lands.");
		ordinary.Free();

		if (GD.Load<PackedScene>(scenePath)?.Instantiate() is Enemy elite)
		{
			elite.IsMiniBoss = true;
			elite.ApplyFear(Player.FearSeconds);
			if (elite.IsFeared)
				warnings.Add("Fear: an elite or boss (IsMiniBoss) took ApplyFear; it must be immune.");
			elite.Free();
		}
		if (Player.FearChance <= 0f || Player.FearChance >= 0.5f || Player.FearSeconds <= 0f)
			warnings.Add($"Fear: chance {Player.FearChance} and {Player.FearSeconds} s are outside what the tag card describes as a chance.");
	}

	// The Familiar (.ai/spell-roster.md, "Familiar") is one node the Player makes from its FamiliarScene
	// export, taken as a dog, a crow or a cat (SpellData.Variants), and every look it can take is a
	// SpriteFrames file loaded by name. All of it fails silently in a run: an unassigned export takes a slot
	// and summons nothing, a missing or unimported sheet is an invisible creature biting things, and a
	// pounce or howl strip shorter than the six frames Familiar.cs sets by hand throws on the frame index.
	// Its fifteen milestones (five a form) are read by id (HasEvolution), so an option renamed in the catalog
	// would be a card that changes nothing; and the two the save remembers from before forms must still
	// name milestones that exist (Familiar.RenamedMilestones). Re-run: python tools/art/familiar.py, then import.
	private static void ValidateFamiliar(List<string> warnings)
	{
		const string scenePath = "res://scenes/Familiar.tscn";
		if (GD.Load<PackedScene>(scenePath)?.Instantiate() is not Familiar pack)
			warnings.Add($"Familiar: {scenePath} did not instantiate as a Familiar; the spell would summon nothing.");
		else
		{
			if (pack.RunSpeed <= 0f || pack.LeapSeconds <= 0f || pack.BaseBiteRadius <= 0f || pack.BlinkDistance <= 0f)
				warnings.Add("Familiar: RunSpeed, LeapSeconds, BaseBiteRadius and BlinkDistance must all be positive.");
			pack.Free();
		}

		SceneState player = GD.Load<PackedScene>("res://scenes/player.tscn")?.GetState();
		bool assigned = false;
		for (int i = 0; player != null && i < player.GetNodePropertyCount(0); i++)
			assigned |= player.GetNodePropertyName(0, i) == "FamiliarScene";
		if (!assigned)
			warnings.Add("Familiar: player.tscn does not assign FamiliarScene; the spell would take a slot and summon nothing.");

		foreach (string form in Familiar.FormNames)
		{
			string path = Familiar.FramesPath(form);
			if (!ResourceLoader.Exists(path) || GD.Load<SpriteFrames>(path) is not SpriteFrames frames)
			{
				warnings.Add($"Familiar: {path} is missing or not imported; that form would be invisible.");
				continue;
			}
			foreach (string anim in new[] { "idle", "run", "pounce", "howl" })
			{
				if (!frames.HasAnimation(anim))
					warnings.Add($"Familiar: {path} has no '{anim}' animation.");
				else if (anim is "pounce" or "howl" && frames.GetFrameCount(anim) != 6)
					warnings.Add($"Familiar: {path} '{anim}' has {frames.GetFrameCount(anim)} frames; Familiar.cs sets frames 0-5 by hand.");
			}
		}

		SpellData spell = ContentValidator.LoadAllSpells().FirstOrDefault(s => s?.Id == "familiar");
		if (spell == null)
		{
			warnings.Add("Familiar: SpellData_Familiar.tres is not in ContentValidator.SpellResourcePaths.");
			return;
		}
		var expected = new Dictionary<string, (string[] Four, string[] Eight)>
		{
			[Familiar.DogId] = (new[] { Familiar.GreatHoundId, Familiar.RimehoundId, Familiar.LittermatesId },
				new[] { Familiar.CerberusId, Familiar.AnnwnId }),
			[Familiar.CrowId] = (new[] { Familiar.RavenId, Familiar.StormcrowId, Familiar.MurderId },
				new[] { Familiar.MorriganId, Familiar.GriffinId }),
			[Familiar.CatId] = (new[] { Familiar.GrimalkinId, Familiar.CaitSithId, Familiar.PurrId },
				new[] { Familiar.CathPalugId, Familiar.NineLivesId }),
		};
		var everyMilestone = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (variantId, (four, eight)) in expected)
		{
			SpellData form = ContentValidator.LoadAllSpellForms().FirstOrDefault(s => s.SelectedVariantId == variantId);
			if (form == null)
			{
				warnings.Add($"Familiar: no form '{variantId}' in SpellData_Familiar.tres; Familiar.FormOf would fall back to the dog for it.");
				continue;
			}
			if (Familiar.FormOf(form) == Familiar.Kind.Dog && variantId != Familiar.DogId)
				warnings.Add($"Familiar: form '{variantId}' reads as the dog (Familiar.FormOf).");
			var level4 = form.Level4Options.Select(o => o?.Id).ToList();
			var level8 = form.Level8Options.Select(o => o?.Id).ToList();
			everyMilestone.UnionWith(level4.Concat(level8).Where(id => id != null));
			foreach (string id in four)
				if (!level4.Contains(id))
					warnings.Add($"Familiar: '{variantId}' offers no level 4 '{id}', so the look and behaviour Familiar reads for it are unreachable.");
			foreach (string id in eight)
				if (!level8.Contains(id))
					warnings.Add($"Familiar: '{variantId}' offers no level 8 '{id}', so the look and behaviour Familiar reads for it are unreachable.");
		}
		foreach (var renamed in Familiar.RenamedMilestones)
			if (!everyMilestone.Contains(renamed.Value))
				warnings.Add($"Familiar: the save's old '{renamed.Key}' reads as '{renamed.Value}' (SaveData.IsAscensionSeen), which no form offers.");
		// The cat grants and the others do not: a dog or a crow that granted cast speed would be the cat's
		// whole worth handed to a form that already out-damages it.
		foreach (SpellData form in ContentValidator.LoadAllSpellForms().Where(s => s.Id == "familiar"))
		{
			bool grants = Familiar.GrantsOf(form).Any;
			if (grants != (Familiar.FormOf(form) == Familiar.Kind.Cat))
				warnings.Add($"Familiar: '{form.SelectedVariantId}' {(grants ? "grants" : "grants nothing")}; only the cat grants the wizard anything.");
		}
		ValidateCoursing(warnings);
	}

	// THE COURSING (AchievementDefinitions "coursing", which frees Laelaps): a Black Hare run down by a hound.
	// What fails silently: the hare's scene naming another EnemyType (Familiar.Strike would count nothing,
	// and the deed could never be done), the deed's measure not reading the run's count or the lifetime best
	// (a deed done and never granted), the best not surviving the save, and the hound counted as a wizard
	// by the final chapter's gate (an optional deed made mandatory, and the Emberdeep closed, live, to every
	// save that had opened it).
	private static void ValidateCoursing(List<string> warnings)
	{
		const string harePath = "res://scenes/HeathBlackHare.tscn";
		if (GD.Load<PackedScene>(harePath)?.Instantiate() is not Enemy hare)
			warnings.Add($"Coursing: {harePath} did not instantiate as an Enemy; the deed's quarry is missing.");
		else
		{
			if (hare.EnemyType != Familiar.CoursedEnemyType)
				warnings.Add($"Coursing: the Black Hare's EnemyType is '{hare.EnemyType}', but Familiar counts '{Familiar.CoursedEnemyType}'; the deed could never be done.");
			hare.Free();
		}

		AchievementDefinition deed = AchievementDefinitions.GetById("coursing");
		if (deed == null)
		{
			warnings.Add("Coursing: no 'coursing' achievement; Laelaps can never be freed.");
			return;
		}
		if (!deed.CharacterUnlockId.Equals("laelaps", StringComparison.OrdinalIgnoreCase))
			warnings.Add($"Coursing: the deed frees '{deed.CharacterUnlockId}', not Laelaps.");
		var shortRun = new global::RunResult { HaresCoursed = AchievementDefinitions.CoursingTarget - 1, StageId = "stage_10" };
		var fullRun = new global::RunResult { HaresCoursed = AchievementDefinitions.CoursingTarget, StageId = "stage_10" };
		if (deed.IsComplete(AchievementContext.ForRun(new SaveData(), shortRun)))
			warnings.Add($"Coursing: {AchievementDefinitions.CoursingTarget - 1} hares completes the deed; it asks for {AchievementDefinitions.CoursingTarget}.");
		if (!deed.IsComplete(AchievementContext.ForRun(new SaveData(), fullRun)))
			warnings.Add("Coursing: a run that coursed enough hares does not complete the deed; it reads neither the run nor the lifetime best.");
		var save = new SaveData();
		save.Lifetime.Absorb(fullRun);
		LifetimeStats back = LifetimeStats.FromDictionary(save.Lifetime.ToGodotDictionary());
		if (back.BestHaresCoursed != AchievementDefinitions.CoursingTarget || !deed.IsComplete(AchievementContext.ForSave(new SaveData { Lifetime = back })))
			warnings.Add("Coursing: the best run's hares do not survive the save (LifetimeStats.BestHaresCoursed), so the menu would forget the deed.");

		if (UnlockCatalog.AllWizardIds.Contains("laelaps", StringComparer.OrdinalIgnoreCase))
			warnings.Add("Coursing: Laelaps is counted by the final chapter's gate (UnlockCatalog.AllWizardIds); he is not a wizard, and his deed is optional.");
		if (!UnlockCatalog.AllCharacterIds.Contains("laelaps", StringComparer.OrdinalIgnoreCase))
			warnings.Add("Coursing: Laelaps is missing from UnlockCatalog.AllCharacterIds, so the tools that own everything do not own him.");
	}

	// SPELL FORMS (SpellData.Variants; .ai/decisions/2026-10-28-spell-variants.md). A form replaces its
	// spell's name, icon, numbers, level table and milestones in the run's copy, and is chosen by its id in a
	// level-up selection whose second half means "form" for a new spell and "milestone" for a held one -
	// none of which a build can see. What this holds, for every spell with forms:
	//   - every form has an id that is unique, starts with "<spell id>_", and is no milestone's id; a name, a
	//     prompt for the page, and an icon of its own (not the spell's, not a sibling's);
	//   - every form's milestones are its own: three level 4s and two level 8s, ids starting with the form's
	//     id, shared with no other form - which is what gives each its own icon and its own seen-ascension
	//     record - and a reader row of its own (EvolutionStatReaders.TryGet(SpellData));
	//   - a form never changes the spell's element tags (the reachability work counts the spell once);
	//   - its table is levels 2 to MaxLevel, in order;
	//   - no word only a form has (its prompt, name, words, traits) gives a size in pixels (2026-10-28, Eric:
	//     "never mention px, add a %"). Its description and milestones are ValidateNoPixelsInText's, which
	//     walks every form.

	private static void ValidateSpellVariants(List<string> warnings)
	{
		var variantIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var evolutionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var formsById = new Dictionary<string, SpellData>(StringComparer.OrdinalIgnoreCase);
		foreach (SpellData spell in ContentValidator.LoadAllSpellForms())
		{
			foreach (SpellEvolutionOption option in (spell.Level4Options ?? new()).Concat(spell.Level8Options ?? new()))
			{
				if (option != null && !string.IsNullOrEmpty(option.Id))
					evolutionIds.Add(option.Id);
			}
			if (!string.IsNullOrEmpty(spell.SelectedVariantId))
				formsById[spell.SelectedVariantId] = spell;
		}

		foreach (SpellData template in ContentValidator.LoadAllSpells())
		{
			if (template == null || !template.HasVariants)
				continue;
			string id = template.Id;
			if (string.IsNullOrWhiteSpace(template.VariantPrompt))
				warnings.Add($"Spell forms: '{id}' has forms but no VariantPrompt, so the page of them would ask nothing.");
			CheckNoPixels(warnings, id, template.Name, template.Description, template.VariantPrompt);
			var icons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (template.Icon?.ResourcePath is string own && own.Length > 0)
				icons.Add(own);
			var milestoneOwner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (SpellVariant variant in template.Variants)
			{
				if (variant == null || string.IsNullOrWhiteSpace(variant.Id))
				{
					warnings.Add($"Spell forms: '{id}' has a form with no id; it could never be chosen.");
					continue;
				}
				string vid = variant.Id;
				if (!variantIds.Add(vid))
					warnings.Add($"Spell forms: form id '{vid}' is used twice.");
				if (!vid.StartsWith(id + "_", StringComparison.OrdinalIgnoreCase))
					warnings.Add($"Spell forms: '{vid}' does not start with '{id}_'; a form's id is its spell's, extended.");
				if (evolutionIds.Contains(vid))
					warnings.Add($"Spell forms: '{vid}' is also a milestone's id, so 'spell:{vid}' would mean two things (SpellVariants.Selection).");
				if (string.IsNullOrWhiteSpace(variant.DisplayName) || string.IsNullOrWhiteSpace(variant.ShortName))
					warnings.Add($"Spell forms: '{vid}' needs a DisplayName and a ShortName.");
				string iconPath = variant.Icon?.ResourcePath ?? string.Empty;
				if (variant.Icon == null)
					warnings.Add($"Spell forms: '{vid}' has no icon; its card, the loadout strip and the results would show the spell's.");
				else if (iconPath.Length > 0 && !icons.Add(iconPath))
					warnings.Add($"Spell forms: '{vid}' shares its icon ({iconPath}) with its spell or a sibling; each form has its own.");
				CheckNoPixels(warnings, vid, variant.DisplayName, variant.Description, variant.Traits);
				int previous = 0;
				foreach (SpellLevelUpgrade upgrade in variant.LevelUpgrades ?? new())
				{
					if (upgrade == null || upgrade.Level < 2 || upgrade.Level > template.MaxLevel || upgrade.Level < previous)
						warnings.Add($"Spell forms: '{vid}' has a level-up out of order or outside 2..{template.MaxLevel}.");
					previous = upgrade?.Level ?? previous;
				}
				if (variant.BaseCooldown <= 0f || variant.BaseProjectileCount < 1 || variant.BaseDamage < 1)
					warnings.Add($"Spell forms: '{vid}' needs a positive damage, cooldown and count.");

				if (!formsById.TryGetValue(vid, out SpellData form))
				{
					warnings.Add($"Spell forms: '{vid}' could not be applied to a copy of '{id}' (SpellVariants.Preview).");
					continue;
				}
				if (!EvolutionStatReaders.HasRow(vid))
					warnings.Add($"Spell forms: '{vid}' has no row of its own in EvolutionStatReaders; its milestones are checked against the spell's.");
				var baseTags = template.GetElementWeights();
				var formTags = form.GetElementWeights();
				if (baseTags.Count != formTags.Count || baseTags.Any(p => !formTags.TryGetValue(p.Key, out int w) || w != p.Value))
					warnings.Add($"Spell forms: '{vid}' changes '{id}'s element tags; a form is a way of playing the spell, never a different carrier.");
				if ((form.Level4Options?.Count ?? 0) != 3 || (form.Level8Options?.Count ?? 0) != 2)
					warnings.Add($"Spell forms: '{vid}' has {form.Level4Options?.Count ?? 0} level 4 and {form.Level8Options?.Count ?? 0} level 8 milestones; every form has 3 and 2 of its own.");
				foreach (SpellEvolutionOption option in (form.Level4Options ?? new()).Concat(form.Level8Options ?? new()))
				{
					if (option == null)
						continue;
					if (!option.Id.StartsWith(vid + "_", StringComparison.OrdinalIgnoreCase))
						warnings.Add($"Spell forms: '{vid}' offers '{option.Id}', which is not its own (a form's milestone ids start with '{vid}_').");
					if (milestoneOwner.TryGetValue(option.Id, out string other) && !other.Equals(vid, StringComparison.OrdinalIgnoreCase))
						warnings.Add($"Spell forms: '{option.Id}' is offered by both '{other}' and '{vid}'.");
					milestoneOwner[option.Id] = vid;
				}
			}
		}
	}

	private static void CheckNoPixels(List<string> warnings, string owner, params string[] texts)
	{
		foreach (string text in texts)
		{
			if (!string.IsNullOrEmpty(text) && PixelUnit.IsMatch(text))
				warnings.Add($"Spell forms: '{owner}' says \"{text}\"; a size on a card is a percentage, never pixels.");
		}
	}

	// Spiritual Weapon (scripts/SpiritualWeapon.cs): one node the Player makes from an export, drawing each
	// form from a pre-rotated sheet, and every milestone read by its id. What a build cannot see: the scene
	// unassigned or the wrong type (a slot that summons nothing), a sheet missing or not imported (an
	// invisible weapon), a sheet of the wrong shape (the wrong heading for every frame), and a milestone the
	// node reads by an id the catalog no longer offers (a card that changes nothing).
	private static void ValidateSpiritualWeapon(List<string> warnings)
	{
		const string scenePath = "res://scenes/SpiritualWeaponScene.tscn";
		if (GD.Load<PackedScene>(scenePath)?.Instantiate() is not SpiritualWeapon node)
			warnings.Add($"Spiritual Weapon: {scenePath} did not instantiate as a SpiritualWeapon; the spell would swing nothing.");
		else
			node.Free();

		SceneState player = GD.Load<PackedScene>("res://scenes/player.tscn")?.GetState();
		bool assigned = false;
		for (int i = 0; player != null && i < player.GetNodePropertyCount(0); i++)
			assigned |= player.GetNodePropertyName(0, i) == "SpiritualWeaponScene";
		if (!assigned)
			warnings.Add("Spiritual Weapon: player.tscn does not assign SpiritualWeaponScene; no spell would fire at all (Player stops the fire loop without it).");

		// The dagger is Twin Blades' (2026-10-30), on the projectile cell.
		foreach (var (name, cell) in new[] { ("axe", 48), ("hammer", 48), ("sword", 48), ("greataxe", 96), ("maul", 96), ("greatsword", 96), ("dagger", 32) })
		{
			string path = $"res://assets/bonelight/effects/spirit-{name}.png";
			if (!ResourceLoader.Exists(path) || GD.Load<Texture2D>(path) is not Texture2D sheet)
			{
				warnings.Add($"Spiritual Weapon: {path} is missing or not imported; that form would be invisible. Run tools/art/spirit_weapons.py and import.");
				continue;
			}
			if (sheet.GetWidth() != cell * 32 || sheet.GetHeight() % cell != 0)
				warnings.Add($"Spiritual Weapon: {path} is {sheet.GetWidth()}x{sheet.GetHeight()}; SpiritualWeapon draws 32 headings of a {cell} cell a row.");
		}
		foreach (string problem in SpiritualWeapon.CutProblems())
			warnings.Add($"Spiritual Weapon: {problem}");

		SpellData template = ContentValidator.LoadAllSpells().FirstOrDefault(s => s?.Id == "spiritual_weapon");
		if (template == null)
		{
			warnings.Add("Spiritual Weapon: SpellData_SpiritualWeapon.tres is not in ContentValidator.SpellResourcePaths.");
			return;
		}
		var expected = new Dictionary<string, (string[] Four, string[] Eight)>
		{
			[SpiritualWeapon.AxeId] = (new[] { SpiritualWeapon.BroadaxeId, SpiritualWeapon.HeadsmanId, SpiritualWeapon.DawnsplitterId },
				new[] { SpiritualWeapon.GreataxeId, SpiritualWeapon.WhirlingAxesId }),
			[SpiritualWeapon.HammerId] = (new[] { SpiritualWeapon.AftershockId, SpiritualWeapon.EarthshakerId, SpiritualWeapon.HallowedGroundId },
				new[] { SpiritualWeapon.TitansMaulId, SpiritualWeapon.HammerfallId }),
			[SpiritualWeapon.SwordId] = (new[] { SpiritualWeapon.TwinBladesId, SpiritualWeapon.RiposteId, SpiritualWeapon.SoulSiphonId },
				new[] { SpiritualWeapon.GreatswordId, SpiritualWeapon.ThousandCutsId }),
		};
		foreach (var (variantId, (four, eight)) in expected)
		{
			SpellData form = ContentValidator.LoadAllSpellForms().FirstOrDefault(s => s.SelectedVariantId == variantId);
			if (form == null)
			{
				warnings.Add($"Spiritual Weapon: no form '{variantId}' in the .tres; SpiritualWeapon.FormOf would fall back to the axe for it.");
				continue;
			}
			var level4 = form.Level4Options.Select(o => o?.Id).ToList();
			var level8 = form.Level8Options.Select(o => o?.Id).ToList();
			foreach (string id in four)
				if (!level4.Contains(id))
					warnings.Add($"Spiritual Weapon: '{variantId}' offers no level 4 '{id}', which SpiritualWeapon reads by id.");
			foreach (string id in eight)
				if (!level8.Contains(id))
					warnings.Add($"Spiritual Weapon: '{variantId}' offers no level 8 '{id}', which SpiritualWeapon reads by id.");
		}
	}

	// The facing family (.ai/spell-roster.md): Stormsweep and Sleet Runes are each one node the Player
	// makes from an export. Unassigned, or a scene that is not the right type, and the fire helper
	// returns NoTarget every frame - the spell owns a slot and does nothing, with no error anywhere.
	// A rune is one region of a pre-rotated sheet, and a sheet of the wrong shape draws the wrong
	// heading for every rune without failing. Their ascensions are read by id (HasEvolution), so an
	// option renamed in the catalog would leave a card that changes nothing.
	private static void ValidateFacingFamily(List<string> warnings)
	{
		if (GD.Load<PackedScene>("res://scenes/Stormsweep.tscn")?.Instantiate() is not Stormsweep sweep)
			warnings.Add("Stormsweep: res://scenes/Stormsweep.tscn did not instantiate as a Stormsweep; the spell will never sweep.");
		else
		{
			if (sweep.BaseHalfBand <= 0f || sweep.ArcGap <= 0f)
				warnings.Add($"Stormsweep: BaseHalfBand {sweep.BaseHalfBand} and ArcGap {sweep.ArcGap} must both be positive.");
			sweep.Free();
		}

		if (GD.Load<PackedScene>("res://scenes/SleetRunes.tscn")?.Instantiate() is not SleetRunes runes)
			warnings.Add("Sleet Runes: res://scenes/SleetRunes.tscn did not instantiate as a SleetRunes; no rune will fly.");
		else
		{
			if (runes.BaseSpeed <= 0f || runes.StreamGap <= 0f || runes.HitRadius <= 0f)
				warnings.Add($"Sleet Runes: BaseSpeed {runes.BaseSpeed}, StreamGap {runes.StreamGap} and HitRadius {runes.HitRadius} must all be positive.");
			Texture2D sheet = runes.RuneSheet;
			if (sheet == null)
				warnings.Add("Sleet Runes: the scene has no RuneSheet (or assets/bonelight/effects/sleet-rune.png is not imported); runes would fly invisible.");
			else if (sheet.GetWidth() != sheet.GetHeight() * SleetRunes.Directions)
				warnings.Add($"Sleet Runes: the rune sheet is {sheet.GetWidth()}x{sheet.GetHeight()}, not {SleetRunes.Directions} square cells in a row; every rune would be drawn at the wrong heading.");
			runes.Free();
		}

		SceneState player = GD.Load<PackedScene>("res://scenes/player.tscn")?.GetState();
		foreach (string export in new[] { "StormsweepScene", "SleetRunesScene" })
		{
			bool assigned = false;
			for (int i = 0; player != null && i < player.GetNodePropertyCount(0); i++)
				assigned |= player.GetNodePropertyName(0, i) == export;
			if (!assigned)
				warnings.Add($"Facing family: player.tscn does not assign {export}; the spell would take a slot and do nothing.");
		}

		CheckOptionIds(warnings, "stormsweep",
			new[] { Stormsweep.HighVoltageId, Stormsweep.IronCurrentId, Stormsweep.RapidDischargeId },
			new[] { Stormsweep.ThunderclapId, Stormsweep.CardinalStormId });
		CheckOptionIds(warnings, "sleet_runes",
			new[] { SleetRunes.DeepRimeId, SleetRunes.RuneFlurryId, SleetRunes.FarWindId },
			new[] { SleetRunes.SigilFanId, SleetRunes.TurningGaleId });
	}

	private static void CheckOptionIds(List<string> warnings, string spellId, string[] level4, string[] level8)
	{
		SpellData spell = ContentValidator.LoadAllSpells().FirstOrDefault(s => s?.Id == spellId);
		if (spell == null)
		{
			warnings.Add($"Facing family: '{spellId}' is not in ContentValidator.SpellResourcePaths.");
			return;
		}
		SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
		var offered4 = spell.Level4Options.Select(o => o?.Id).ToList();
		var offered8 = spell.Level8Options.Select(o => o?.Id).ToList();
		foreach (string id in level4)
			if (!offered4.Contains(id))
				warnings.Add($"Facing family: '{spellId}' level 4 offers no '{id}'.");
		foreach (string id in level8)
			if (!offered8.Contains(id))
				warnings.Add($"Facing family: '{spellId}' level 8 offers no '{id}', so the node's behaviour for it is unreachable.");
	}

	// The pause screen's spell details are written in SpellClassificationText's words. A new enum
	// value without a phrase would fall back to its code name on screen, which is the thing that
	// was reported as unreadable - so a missing phrase is a warning here, not a silent fallback.
	private static void ValidateSpellClassificationText(List<string> warnings)
	{
		foreach (SpellTargetingMode mode in Enum.GetValues<SpellTargetingMode>())
			if (string.IsNullOrEmpty(SpellClassificationText.Aim(mode)))
				warnings.Add($"Spell details: SpellTargetingMode.{mode} has no plain-English phrase in SpellClassificationText.Aim.");
		foreach (SpellDamageShape shape in Enum.GetValues<SpellDamageShape>())
			if (string.IsNullOrEmpty(SpellClassificationText.Shape(shape)))
				warnings.Add($"Spell details: SpellDamageShape.{shape} has no plain-English phrase in SpellClassificationText.Shape.");
		foreach (SpellScalingTag tag in Enum.GetValues<SpellScalingTag>())
			if (string.IsNullOrEmpty(SpellClassificationText.Scaling(tag)))
				warnings.Add($"Spell details: SpellScalingTag.{tag} has no plain-English word in SpellClassificationText.Scaling.");
	}

	// The exploration art (.ai/side-events.md, "Finding the event"): every sprite explore.json names
	// is imported and stands inside its own picture, the lantern knows where its flame is, every
	// glyph an arrow can carry exists, and every event chapter's wayfinder has a sprite. A missing
	// PNG draws nothing and says nothing (CLAUDE.md), which here would be a lantern that is only
	// light, or an arrow with no pictogram in it. Re-run: python tools/art/explore_props.py, then import.
	private static void ValidateExploreArt(List<string> warnings)
	{
		Dictionary<string, ExploreSprite> sprites = ExploreArt.Load();
		foreach (string id in new[] { "bog-lantern", "arcane-compass", "gaolers-map", "bearing-cairn" })
		{
			if (!sprites.TryGetValue(id, out ExploreSprite s))
			{
				warnings.Add($"Explore: '{id}' is missing from {ExploreArt.Manifest}.");
				continue;
			}
			if (s.Texture == null)
				warnings.Add($"Explore: '{id}.png' is not imported; it will not be drawn.");
			if (s.Frames <= 0 || s.Ground <= 0 || s.Ground > s.FrameH || s.AnchorX <= 0 || s.AnchorX >= s.FrameW)
				warnings.Add($"Explore: '{id}' has its ground line or foot column outside its frame.");
			if (id == "bog-lantern" && (s.Flame == Vector2.Zero || s.LitFrames <= 0 || s.LitFrames >= s.Frames))
				warnings.Add("Explore: the bog lantern needs a flame position and fewer lit frames than frames (the last is dark).");
		}
		foreach (string glyph in ExploreArt.Glyphs)
		{
			if (!ResourceLoader.Exists(ExploreArt.GlyphPath(glyph)))
				warnings.Add($"Explore: arrow glyph '{glyph}' is missing or not imported ({ExploreArt.GlyphPath(glyph)}).");
		}
		foreach (bool wayfinder in new[] { false, true })
		{
			if (!ResourceLoader.Exists(ExploreArt.BadgePath(wayfinder)))
				warnings.Add($"Explore: arrow badge {ExploreArt.BadgePath(wayfinder)} is missing or not imported.");
		}
		foreach (StageDefinition stage in StageCatalog.All)
		{
			var entry = SideEventDirector.ForStage(stage.Id);
			if (entry != null && Array.IndexOf(ExploreArt.Glyphs, WayfinderPickup.GlyphFor(entry.Value.Wayfinder)) < 0)
				warnings.Add($"Explore: {stage.Id}'s wayfinder {entry.Value.Wayfinder} has no arrow glyph.");
		}
	}

	// The stage hazards (StageHazard): the plate and the grate on the floor ARE the danger area - there
	// is no marker any more - and the player's boots are tested against the CollisionShape2D. So the
	// idle frame (the plate or grate alone, nothing standing up off it) must cover that shape's bounds
	// to the pixel, and every phase must have its animation: a missing `tell` is a hazard that bites
	// with no warning, which nothing else would notice. Re-run: python tools/art/props_fx.py, then import.
	private static void ValidateStageHazards(List<string> warnings)
	{
		foreach (string path in new[] { "res://scenes/SpikeTrap.tscn", "res://scenes/FlameVent.tscn" })
		{
			var scene = GD.Load<PackedScene>(path);
			Node root = scene?.Instantiate();
			if (root is not StageHazard)
			{
				warnings.Add($"Hazards: {path} did not load as a StageHazard.");
				root?.Free();
				continue;
			}
			var sprite = root.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
			var area = root.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
			SpriteFrames frames = sprite?.SpriteFrames;
			if (frames == null || area?.Shape == null)
			{
				warnings.Add($"Hazards: {path} has no SpriteFrames or no collision shape.");
				root.Free();
				continue;
			}
			foreach (string anim in new[] { "idle", "tell", "active", "cool" })
			{
				if (!frames.HasAnimation(anim) || frames.GetFrameCount(anim) == 0)
					warnings.Add($"Hazards: {path} has no '{anim}' animation, so that phase shows nothing.");
			}
			Rect2 want = area.Shape switch
			{
				CircleShape2D circle => new Rect2(-circle.Radius, -circle.Radius, circle.Radius * 2f, circle.Radius * 2f),
				RectangleShape2D rect => new Rect2(-rect.Size / 2f, rect.Size),
				_ => new Rect2(),
			};
			want.Position += area.Position;
			if (frames.HasAnimation("idle") && frames.GetFrameTexture("idle", 0) is AtlasTexture idle
				&& idle.Atlas?.GetImage() is Image sheet)
			{
				if (sheet.IsCompressed()) sheet.Decompress();
				Rect2I region = (Rect2I)idle.Region;
				Rect2I used = sheet.GetRegion(region).GetUsedRect();
				// The frame is centred on the node (AnimatedSprite2D.Centered), then shifted by its offset.
				Vector2 corner = -new Vector2(region.Size.X, region.Size.Y) / 2f + sprite.Offset;
				var drawn = new Rect2(corner + used.Position, used.Size);
				if (Mathf.Abs(drawn.Position.X - want.Position.X) > 1f || Mathf.Abs(drawn.Position.Y - want.Position.Y) > 1f
					|| Mathf.Abs(drawn.Size.X - want.Size.X) > 1f || Mathf.Abs(drawn.Size.Y - want.Size.Y) > 1f)
					warnings.Add($"Hazards: {path}'s idle art covers {drawn} but it hurts on {want}: the plate on the floor no longer shows where it bites.");
			}
			else
			{
				warnings.Add($"Hazards: {path}'s idle frame is not an imported atlas frame, so its footprint cannot be checked.");
			}
			root.Free();
		}
	}

	// The status marks (StatusEffectLayer) are drawn out of one atlas by row and frame index, so a
	// sheet regenerated with a row added, or never imported at all, would draw the wrong mark or
	// nothing - silently either way, because DrawTextureRectRegion on a null or short texture does
	// not complain. Re-run: python tools/art/status_fx.py, then import.
	private static void ValidateStatusEffectAtlas(List<string> warnings)
	{
		if (!ResourceLoader.Exists(StatusEffectLayer.AtlasPath))
		{
			warnings.Add($"Status marks: {StatusEffectLayer.AtlasPath} is missing or not imported; no status effects will show on enemies.");
			return;
		}
		Texture2D atlas = GD.Load<Texture2D>(StatusEffectLayer.AtlasPath);
		var expected = new Vector2(StatusEffectLayer.Cell * StatusEffectLayer.Frames, StatusEffectLayer.Cell * StatusEffectLayer.Rows);
		if (atlas == null)
			warnings.Add($"Status marks: {StatusEffectLayer.AtlasPath} exists but did not load as a texture.");
		else if (atlas.GetSize() != expected)
			warnings.Add($"Status marks: {StatusEffectLayer.AtlasPath} is {atlas.GetSize()}, expected {expected} ({StatusEffectLayer.Rows} rows of {StatusEffectLayer.Frames} {StatusEffectLayer.Cell}px frames).");
	}

	// Enemy navigation on a hand-made lake (.ai/terrain.md, "Enemy navigation"). Three things a build
	// cannot see and a playtest only shows as "the swarm piles up on the shore again":
	//   - a field built a slice at a time over several frames must equal one built whole, or the
	//     resumable queue has lost or double-counted a cell;
	//   - the straight-walk test must see the lake, or no enemy on an open stage ever reads the field;
	//   - the flow on the far side must go round the lake, the short way, not into it.
	// Also that a ring of water seals its middle off, so nothing is ever spawned on an island.
	private static void ValidateNavigation(List<string> warnings)
	{
		const int size = 40;
		const int tile = 48;
		var solid = new bool[size * size];
		var wall = new bool[size * size];
		// A lake from x 12 to 30, y 15 to 20: the way round its left end (x 11) is shorter than its
		// right (x 31) for anything standing above x 20.
		for (int y = 15; y <= 20; y++)
			for (int x = 12; x <= 30; x++)
				solid[x + y * size] = true;
		// A one-tile ring of water with dry ground inside it: an island.
		for (int y = 2; y <= 8; y++)
			for (int x = 30; x <= 36; x++)
				if (x == 30 || x == 36 || y == 2 || y == 8)
					solid[x + y * size] = true;

		Vector2 Cell(int x, int y) => new Vector2((x + 0.5f) * tile, (y + 0.5f) * tile);
		Vector2 player = Cell(20, 32);
		Vector2 behind = Cell(20, 6);

		var whole = new MazeNavigation(size, tile, Vector2.Zero, isMaze: false);
		whole.RebuildFromGrid(solid, wall, Vector2I.Zero, player);

		// Built first round another cell, then moved to the player's and stepped in tiny slices.
		var sliced = new MazeNavigation(size, tile, Vector2.Zero, isMaze: false) { CellsPerFrame = 37 };
		sliced.RebuildFromGrid(solid, wall, Vector2I.Zero, Cell(5, 35));
		int frames = 0;
		sliced.Update(player, 1f);
		while (sliced.IsBuildingField && frames++ < 1000)
			sliced.Update(player, 0.01f);
		if (frames < 2)
			warnings.Add("Navigation: the sliced field finished in one step - the regression check is not exercising resumption.");
		int mismatched = 0;
		for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
				if (whole.PathTilesAt(Cell(x, y)) != sliced.PathTilesAt(Cell(x, y)))
					mismatched++;
		if (mismatched > 0)
			warnings.Add($"Navigation: a field built in slices differs from one built whole on {mismatched} cells (MazeNavigation.StepField).");

		if (whole.HasClearWalk(behind, player))
			warnings.Add("Navigation: the straight walk across a lake reads as clear - open-stage enemies would never route round water.");
		if (!whole.HasClearWalk(Cell(5, 6), Cell(5, 32)))
			warnings.Add("Navigation: a straight walk on dry ground reads as blocked.");

		Vector2 flow = whole.FlowDirectionAt(behind);
		if (flow.X > -0.2f)
			warnings.Add($"Navigation: the flow behind a lake is {flow}, which does not head round its near (left) end.");
		float walk = whole.PathTilesAt(behind);
		if (walk < 27f)
			warnings.Add($"Navigation: walking distance round the lake is {walk} tiles, no longer than the 26-tile straight line - the lake is not in the field.");

		if (whole.IsReachable(Cell(33, 5)))
			warnings.Add("Navigation: dry ground inside a ring of water counts as reachable - enemies could be spawned on an island.");
		if (!whole.IsReachable(Cell(5, 5)))
			warnings.Add("Navigation: open ground connected to the player counts as unreachable.");
		if (whole.IsWallAt(behind) || !whole.HasLineOfSight(behind, player))
			warnings.Add("Navigation: water is stopping shots - only maze walls should.");
	}

	// Stage hazards on the maze stand in the middle of a hallway or a hall (.ai/terrain.md section 6,
	// "Traps in the middle of the path"). LevelGenerator.MazeHallwayMiddle does it from the lattice;
	// this checks it from the WALLS: on real generated maze ground, every point it snaps to must be
	// open floor at least half a corridor from the nearest wall tile. In a six-tile corridor that is
	// only true on the centreline, so a trap that has drifted toward a wall, or into one, fails here
	// whatever the snapping code thinks it did. And since 2026-10-28 ("make the traps less random") it
	// must be one of the two kinds of middle - a hallway's, half a cell along one axis from a cell
	// centre, or a hall's, half a cell along both - never a junction and never a random point down a
	// corridor. Points within the hazards' clearance of the start are left out, as the game leaves
	// them out.
	private static void ValidateMazeHazards(List<string> warnings)
	{
		const int tile = 48;
		const int chunk = 32;
		const int margin = 48;
		var generator = new LevelGenerator();
		float need = LevelGenerator.MazeCorridorTiles / 2f * tile - 1f;
		int checkedCount = 0, failed = 0, halls = 0, hallways = 0, strays = 0;
		string firstFailure = null, firstStray = null;
		foreach (ulong seed in new ulong[] { 20261014UL, 4242UL, 987654321UL })
		{
			// One padded grid: index [0, 0] is world tile (-margin, -margin); the middle is chunk (0, 0).
			string[,] grid = generator.GenerateChunk(StageEnvironmentKind.Castle, seed, 0, 0, chunk, margin, 6f, 4f);
			int size = grid.GetLength(0);
			bool WallTile(int tx, int ty)
			{
				int gx = tx + margin, gy = ty + margin;
				return gx < 0 || gy < 0 || gx >= size || gy >= size || grid[gx, gy] == LevelGenerator.WallTerrain;
			}
			bool IsWall(Vector2 local) => WallTile(Mathf.FloorToInt(local.X / tile), Mathf.FloorToInt(local.Y / tile));

			var rng = new LevelGenerator.Rng(LevelGenerator.Hash(seed, 7, 7, 0x4A2D));
			for (int k = 0; k < 400; k++)
			{
				// Well inside the grid, so the walls a point can see are all generated.
				var p = new Vector2(
					(-margin + 12 + rng.NextFloat() * (size - 24)) * tile,
					(-margin + 12 + rng.NextFloat() * (size - 24)) * tile);
				Vector2 q = LevelGenerator.MazeHallwayMiddle(p, tile, IsWall);
				if (q.Length() < 320f)
					continue;
				checkedCount++;
				float clearance = float.MaxValue;
				int qx = Mathf.FloorToInt(q.X / tile), qy = Mathf.FloorToInt(q.Y / tile);
				for (int ty = qy - 6; ty <= qy + 6; ty++)
				{
					for (int tx = qx - 6; tx <= qx + 6; tx++)
					{
						if (!WallTile(tx, ty)) continue;
						float dx = Mathf.Max(Mathf.Max(tx * tile - q.X, q.X - (tx + 1) * tile), 0f);
						float dy = Mathf.Max(Mathf.Max(ty * tile - q.Y, q.Y - (ty + 1) * tile), 0f);
						clearance = Mathf.Min(clearance, Mathf.Sqrt(dx * dx + dy * dy));
					}
				}
				if (clearance < need)
				{
					failed++;
					firstFailure ??= $"seed {seed}: {p} snapped to {q}, {clearance:0} px from a wall";
				}
				float pitch = LevelGenerator.MazeCellTiles * tile;
				float halfPitch = pitch * 0.5f;
				bool onCellX = Mathf.IsZeroApprox(Mathf.PosMod(q.X, pitch)), onCellY = Mathf.IsZeroApprox(Mathf.PosMod(q.Y, pitch));
				bool onHalfX = Mathf.IsEqualApprox(Mathf.PosMod(q.X, pitch), halfPitch);
				bool onHalfY = Mathf.IsEqualApprox(Mathf.PosMod(q.Y, pitch), halfPitch);
				if ((onCellX && onHalfY) || (onHalfX && onCellY)) hallways++;
				else if (onHalfX && onHalfY) halls++;
				else
				{
					strays++;
					firstStray ??= $"seed {seed}: {p} snapped to {q}";
				}
			}
		}
		if (failed > 0)
			warnings.Add($"Maze: {failed} of {checkedCount} stage hazards would stand less than half a corridor ({need + 1:0} px) "
				+ $"from a wall (first: {firstFailure}). LevelGenerator.MazeHallwayMiddle no longer finds the middle of the path.");
		if (strays > 0)
			warnings.Add($"Maze: {strays} of {checkedCount} stage hazards stand neither in a hallway's middle nor a hall's "
				+ $"(first: {firstStray}) - the traps are scattered again.");
		if (checkedCount < 600 || halls == 0 || hallways == 0)
			warnings.Add($"Maze: the hazard check saw {checkedCount} points ({hallways} mid-hallway, {halls} mid-hall) - too few, "
				+ "or a kind of ground it never reached, so it is not testing what it claims.");
	}

	// Water stops the player because assets/bonelight/tiles/tiles.json says "blocking": true on
	// its sixteen tiles - and that file is GENERATED. Re-run tools/art/tiles.py from a checkout
	// where the BLOCKERS set has been lost and the flag quietly disappears: the art still renders,
	// the game still builds, the tiles still paint, and the player walks across the lake. Nothing
	// else in the project would notice.
	private static void ValidateBlockingTerrain(List<string> warnings)
	{
		const string manifest = "res://assets/bonelight/tiles/tiles.json";
		CuratedTileCatalog catalog = CuratedTileCatalog.LoadMany(manifest);
		if (catalog == null)
		{
			warnings.Add($"Tiles: {manifest} did not load; terrain blocking cannot be checked.");
			return;
		}

		if (!catalog.IsBlockingTerrain("water"))
		{
			warnings.Add("Tiles: water is not marked blocking in tiles.json, so it can be walked on. "
				+ "Check BLOCKERS in tools/art/tiles.py and re-run: python tools/art/tiles.py");
		}

		// The maze walls are the "void" material. Without it LevelTilePainter refuses to paint them,
		// and before that guard it painted them as whatever floor came first - sand.
		if (!catalog.HasGroundMaterial("void"))
		{
			warnings.Add("Tiles: the 'void' material (maze walls) is missing from tiles.json. "
				+ "Check MATERIALS in tools/art/tiles.py and re-run: python tools/art/tiles.py");
		}

		// The inverse, so the flag cannot creep onto terrain that is meant to be crossed. Lava and
		// the pits hurt you for standing in them, which only works if you can stand in them.
		foreach (string crossable in new[]
		{
			"grass", "meadow", "moss", "dirt", "overgrowth", "sand", "cobble", "dark_dirt", "mossy_rock",
			"rocky_pit", "ice", "lava", "pit",
		})
		{
			if (catalog.IsBlockingTerrain(crossable))
				warnings.Add($"Tiles: '{crossable}' is marked blocking, which makes it a wall.");
		}
	}

	// Everything a stage paints has to exist, and nothing reports it if it does not: a terrain
	// missing from tiles.json paints as nothing (EnsureTerrainSources caches the miss), a missing
	// ground-detail set paints as nothing, and a missing landmark sprite places no landmark. The
	// natural-ground pass (.ai/terrain.md section 8) added nine names across three files, so this
	// holds the palettes, GroundDetail's rules and the art against each other.
	// The res:// path of every frame in a tile manifest (tiles.json's "frames").
	private static IEnumerable<string> ManifestTilePaths(string manifestPath)
	{
		string text = Godot.FileAccess.GetFileAsString(manifestPath);
		if (string.IsNullOrEmpty(text))
			yield break;
		using var doc = System.Text.Json.JsonDocument.Parse(text);
		if (!doc.RootElement.TryGetProperty("frames", out var frames))
			yield break;
		foreach (var frame in frames.EnumerateArray())
		{
			string path = frame.TryGetProperty("path", out var p) ? p.GetString() : null;
			if (!string.IsNullOrEmpty(path))
				yield return path.StartsWith("res://") ? path : "res://" + path;
		}
	}

	private static void ValidateGroundArt(List<string> warnings)
	{
		CuratedTileCatalog catalog = CuratedTileCatalog.LoadMany(
			"res://assets/bonelight/tiles/tiles.json", "res://assets/kidsart/tiles.json");
		var generator = new LevelGenerator();
		foreach (StageEnvironmentKind kind in Enum.GetValues<StageEnvironmentKind>())
		{
			LevelLayout layout = generator.Describe(kind);
			foreach (string terrain in layout.Terrains)
			{
				if (catalog.TileVariants(terrain, "fill").Count == 0)
					warnings.Add($"Ground: {kind} stamps '{terrain}', which tiles.json does not define. Re-run: python tools/art/tiles.py");
			}
			if (!string.IsNullOrEmpty(layout.GroundTerrain) && catalog.FillTilesFor(layout.GroundTerrain).Count == 0)
				warnings.Add($"Ground: {kind} stands on terrain '{layout.GroundTerrain}', which has no fill tiles.");
			else if (string.IsNullOrEmpty(layout.GroundTerrain) && !catalog.HasGroundMaterial(layout.GroundMaterial))
				warnings.Add($"Ground: {kind} stands on material '{layout.GroundMaterial}', which no manifest defines.");
		}

		Dictionary<string, List<string>> detail = LevelTilePainter.LoadDetailManifest("res://assets/bonelight/tiles/detail/detail.json");
		foreach (string set in GroundDetail.AllSets())
		{
			if (!detail.ContainsKey(set))
				warnings.Add($"Ground: detail set '{set}' is named by GroundDetail but missing from detail.json. Re-run: python tools/art/ground_detail.py");
		}

		// The tile atlas the painter draws from (tools/art/tile_atlas.py). A tile missing from it
		// still paints - LevelTilePainter gives it a texture of its own - but costs a draw call per
		// cell again, which is the frame rate the atlas exists to save (.ai/performance.md).
		Dictionary<string, Vector2I> atlas = LevelTilePainter.LoadAtlasManifest("res://assets/bonelight/tiles/atlas.json", 48, out _);
		if (atlas.Count == 0 || !ResourceLoader.Exists("res://assets/bonelight/tiles/atlas.png"))
			warnings.Add("Ground: the tile atlas (atlas.json / atlas.png) is missing, so every tile is its own draw call. Re-run: python tools/art/tile_atlas.py");
		else
		{
			var unpacked = new List<string>();
			foreach (string path in ManifestTilePaths("res://assets/bonelight/tiles/tiles.json"))
				if (!atlas.ContainsKey(path)) unpacked.Add(path);
			foreach (List<string> variants in detail.Values)
				foreach (string path in variants)
					if (!atlas.ContainsKey(path)) unpacked.Add(path);
			if (unpacked.Count > 0)
				warnings.Add($"Ground: {unpacked.Count} tile(s) are not in the tile atlas (first: {unpacked[0]}). Re-run: python tools/art/tile_atlas.py");
		}

		// The Cursed Dungeon's jail walls (tools/art/jail_walls.py): a plain variant for every role the
		// painter can ask for, the one-tile stubs the spawn clearing leaves among them, and the four
		// shadows. A role without art paints as the old black void tile (LevelTilePainter.WallTileFor),
		// which is a wall that works and looks like a hole - nothing else would say so.
		var wallRoles = new List<string>();
		foreach (AutotileRole role in Enum.GetValues<AutotileRole>())
			wallRoles.Add(TerrainAutotiler.RoleKey(role));
		wallRoles.AddRange(new[] { "patch_v", "patch_cap", "patch_face" });
		foreach (string role in wallRoles)
		{
			if (!catalog.TileVariants(LevelGenerator.WallTerrain, role).Any(e => e.Role == role && e.Dressing == null))
				warnings.Add($"Ground: the maze walls have no plain '{role}' tile, so those cells paint as the black void. Re-run: python tools/art/tiles.py");
		}
		if (!catalog.TileVariants(LevelGenerator.WallTerrain, "edge_bottom").Any(e => e.Dressing == "sconce"))
			warnings.Add("Ground: the maze walls' faces have no lit sconce. Re-run: python tools/art/tiles.py");

		// A chapter's own wall set (StageEnvironmentProfile.WallSet; court_walls.py, ruin_walls.py...) is
		// held to the same roles the moment the manifest has any of it: the painter takes a set whole or not
		// at all (LevelTilePainter.WallArtSet), so a set missing a role would paint that role as the void.
		// A set nobody has drawn yet paints the jail walls, and is its chapter's to-do, not a fault.
		foreach (StageEnvironmentKind kind in Enum.GetValues<StageEnvironmentKind>())
		{
			string set = StageEnvironmentCatalog.Get(kind).WallSet;
			if (set == StageEnvironmentProfile.DefaultWallSet || catalog.TileVariants(set, "fill").Count == 0)
				continue;
			foreach (string role in wallRoles)
			{
				if (!catalog.TileVariants(set, role).Any(e => e.Role == role && e.Dressing == null))
					warnings.Add($"Ground: {kind}'s wall set '{set}' has no plain '{role}' tile, so those cells paint as the black void. Re-run: python tools/art/tiles.py");
			}
		}
		foreach (string shade in new[] { "shade_n", "shade_w", "shade_nw", "shade_corner" })
		{
			if (!catalog.TileVariants("wall_shadow", shade).Any(e => e.Role == shade))
				warnings.Add($"Ground: the maze walls' '{shade}' shadow tile is missing; walls will stand on the floor with no shadow. Re-run: python tools/art/tiles.py");
		}

		// The garden: its parts, and the layout its art was drawn to against the one its fence is
		// built from. A pen drawn twelve cells wide with a fence collided at ten would put an
		// invisible rail through a flower bed and leave a drawn one you walk through.
		WorldSprites.Pen pen = WorldSprites.LoadPen();
		if (pen == null)
			warnings.Add($"Ground: {WorldSprites.PenManifest} is missing; no flower pens will be drawn. Re-run: python tools/art/ground_detail.py");
		else
		{
			foreach (string part in new[] { "back", "front", "side", "post", "flower", "garden" })
			{
				if (!pen.Parts.TryGetValue(part, out GroundedSprite sprite) || sprite.Texture == null)
					warnings.Add($"Ground: flower pen part '{part}' is missing or not imported; no flower pens will be drawn.");
			}
			if (pen.CellsW != FlowerPenLayout.CellsW || pen.CellsH != FlowerPenLayout.CellsH
				|| pen.GateX != FlowerPenLayout.GateX || pen.GateW != FlowerPenLayout.GateW
				|| pen.SideGateY != FlowerPenLayout.SideGateY || pen.SideGateH != FlowerPenLayout.SideGateH
				|| pen.Line * 2 != 48 || pen.Segment != 48)
				warnings.Add($"Ground: flower-pen.json was drawn to {pen.CellsW}x{pen.CellsH} cells, gates {pen.GateX}+{pen.GateW} and {pen.SideGateY}+{pen.SideGateH}, "
					+ $"but FlowerPenLayout is {FlowerPenLayout.CellsW}x{FlowerPenLayout.CellsH}, gates {FlowerPenLayout.GateX}+{FlowerPenLayout.GateW} "
					+ $"and {FlowerPenLayout.SideGateY}+{FlowerPenLayout.SideGateH}: the fence would collide where it is not drawn. Re-run: python tools/art/ground_detail.py");
		}

		// Trees and bushes: every sprite in the manifest loads, stands inside its own picture, and a
		// tree has a trunk to stop at (a tree with no footprint is a 300 px sprite you walk through).
		foreach (GroundedSprite s in WorldSprites.LoadTrees().Values)
		{
			if (s.Texture == null)
				warnings.Add($"Ground: tree or bush '{s.Id}' is not imported; it will never be placed. Run the import.");
			if (s.Ground <= 0 || s.Ground > s.Height || s.AnchorX <= 0 || s.AnchorX >= s.Width)
				warnings.Add($"Ground: tree or bush '{s.Id}' has its ground line or trunk column outside its picture.");
			if ((s.Kind == "tree") != (s.Trunk > 0))
				warnings.Add($"Ground: '{s.Id}' is a {s.Kind} with a trunk of {s.Trunk} px: trees block at the trunk, bushes never.");
		}
	}

	// The flower pen's fence in the navigation (.ai/terrain.md section 8). The fence used to stop
	// nothing; now it stops the boots and the horde, and the only thing standing between "solid
	// fence" and "a wall the swarm wedges on" is that its cells are in the grid and its gates are not.
	// So, on a pen stamped exactly as Node2DGame.PlanChunkFeatures stamps one:
	//   - the rails are solid and stop no shot, all four gates are open;
	//   - a straight walk through the fence reads as blocked, so an enemy follows the field;
	//   - the inside is reachable, and from just outside a rail only the long way, through a gate;
	//   - an enemy beside a side rail is sent along it, not into it;
	//   - no stretch of rail is long: a gate is never more than MaxRailToGateCells away (2026-10-28,
	//     the swarm queued at two narrow gates and "got stuck");
	//   - every rectangle of the rails' collision lies on fence cells, so a body is never stopped
	//     on a cell the field calls open (the rule water keeps, section 3).
	private const int MaxRailToGateCells = 8;

	private static void ValidateFlowerPen(List<string> warnings)
	{
		const int size = 40;
		const int tile = 48;
		const int px0 = 14, py0 = 14;
		var solid = new bool[size * size];
		var wall = new bool[size * size];
		for (int y = 0; y < FlowerPenLayout.CellsH; y++)
			for (int x = 0; x < FlowerPenLayout.CellsW; x++)
				if (FlowerPenLayout.IsFenceCell(x, y))
					solid[(px0 + x) + (py0 + y) * size] = true;

		Vector2 Cell(int x, int y) => new Vector2((x + 0.5f) * tile, (y + 0.5f) * tile);
		const int w = FlowerPenLayout.CellsW, h = FlowerPenLayout.CellsH;
		Vector2 flower = Cell(px0 + w / 2, py0 + h / 2);
		// The second row: a stretch of the west rail clear of its gate, as far from one as a rail gets.
		int railRow = py0 + 1;
		Vector2 west = Cell(px0 - 2, railRow);
		Vector2 inside = Cell(px0 + 1, railRow);
		Vector2 besideRail = Cell(px0 - 1, railRow + 1);

		var nav = new MazeNavigation(size, tile, Vector2.Zero, isMaze: false);
		nav.RebuildFromGrid(solid, wall, Vector2I.Zero, flower);

		if (!nav.IsBlockedAt(Cell(px0, railRow)) || !nav.IsBlockedAt(Cell(px0 + 2, py0)))
			warnings.Add("Navigation: a flower pen's rail is not solid in the grid - the horde would wedge on the fence.");
		if (nav.IsBlockedAt(Cell(px0 + FlowerPenLayout.GateX, py0)) || nav.IsBlockedAt(Cell(px0 + FlowerPenLayout.GateX, py0 + h - 1))
			|| nav.IsBlockedAt(Cell(px0, py0 + FlowerPenLayout.SideGateY)) || nav.IsBlockedAt(Cell(px0 + w - 1, py0 + FlowerPenLayout.SideGateY)))
			warnings.Add("Navigation: a flower pen's gate is solid - the garden has lost a way in.");
		if (nav.IsWallAt(Cell(px0, railRow)) || !nav.HasLineOfSight(west, inside))
			warnings.Add("Navigation: a garden fence is stopping shots - only maze walls should.");
		if (nav.HasClearWalk(west, inside))
			warnings.Add("Navigation: the straight walk through a garden fence reads as clear - enemies would never route to a gate.");
		if (!nav.IsReachable(flower) || !nav.IsReachable(west))
			warnings.Add("Navigation: the inside of a flower pen, or the ground outside it, counts as unreachable.");
		// Three cells apart across the rail; without the fence in the field the outside would be three
		// tiles further from the flower at most. Through a gate it is several more.
		float detour = nav.PathTilesAt(west) - nav.PathTilesAt(inside);
		float straight = west.DistanceTo(inside) / tile;
		if (detour < straight + 2f)
			warnings.Add($"Navigation: from just outside a pen's rail the flower is {detour:0.0} tiles further than from just inside, {straight:0.0} apart - the fence is not in the field.");
		// The longest run of rail between a corner and a gate, in cells.
		int longest = Math.Max(Math.Max(FlowerPenLayout.GateX, w - FlowerPenLayout.GateX - FlowerPenLayout.GateW),
			Math.Max(FlowerPenLayout.SideGateY, h - FlowerPenLayout.SideGateY - FlowerPenLayout.SideGateH));
		if (longest > MaxRailToGateCells || FlowerPenLayout.GateW * tile < 2 * 88 || FlowerPenLayout.SideGateH * tile < 2 * 88)
			warnings.Add($"Navigation: a flower pen has a rail {longest} cells long or a gate narrower than two of the widest body (176 px) - the swarm queues at it.");
		Vector2 flow = nav.FlowDirectionAt(besideRail);
		if (flow != Vector2.Zero && Mathf.Abs(flow.X) > Mathf.Abs(flow.Y) && flow.X > 0f)
			warnings.Add($"Navigation: the flow beside a pen's west rail is {flow}, into the fence rather than along it to a gate.");

		foreach (Rect2 rail in FlowerPenLayout.RailRects(tile))
		{
			foreach (Vector2 corner in new[] { rail.Position, rail.End - Vector2.One * 0.01f,
				new Vector2(rail.Position.X, rail.End.Y - 0.01f), new Vector2(rail.End.X - 0.01f, rail.Position.Y) })
			{
				int cx = Mathf.FloorToInt(corner.X / tile), cy = Mathf.FloorToInt(corner.Y / tile);
				if (!FlowerPenLayout.IsFenceCell(cx, cy))
				{
					warnings.Add($"Navigation: the flower pen's rail collision {rail} reaches cell ({cx}, {cy}), which the grid calls open.");
					break;
				}
			}
		}
	}

	// THE SEAM RULE (.ai/terrain.md, "Chunk streaming"). Endless stages are generated a chunk at a
	// time, each padded with TerrainStreamer.Margin cells of its neighbours, and the border only comes
	// out the same from both sides if that margin is wide enough for everything that reads past a
	// cell: the cleanup passes and the autotile role. Nothing would report a margin that is too
	// narrow - the game builds and runs, and there is simply a line across the world every 1536 px.
	// So: a chunk resolved with the shipping margin must match the same chunk resolved with twice
	// it, cell for cell; and two neighbours' raw grids must agree wherever they overlap.
	//
	// The maze half checks the other promise: corridors connect across chunk borders. Nine chunks
	// round the start are stitched together and flooded from the spawn point; almost all of the
	// floor must be reached inside that window alone.
	// The water's shape (WaterShape) is built per chunk from a window reaching into its neighbours,
	// and two chunks build the cells along their shared border. If those disagree, the shore steps
	// at every chunk border - and so does its collision, since the polygons are the bodies. So build
	// two neighbours on the Swamp, which has the most water, and compare every cell both build.
	private static void ValidateWaterShapes(List<string> warnings)
	{
		const ulong seed = 20261016;
		const int c = TerrainStreamer.ChunkTiles;
		const int m = TerrainStreamer.Margin;
		var generator = new LevelGenerator();
		var autotiler = new TerrainAutotiler();
		LevelLayout layout = generator.Describe(StageEnvironmentKind.Swamp);

		// Resolved water flags over chunks (-1..2, -1..1), as the streamer would hold them.
		const int x0 = -1, y0 = -1, wide = 4 * c, high = 3 * c;
		var wet = new bool[wide * high];
		for (int cy = -1; cy <= 1; cy++)
		{
			for (int cx = -1; cx <= 2; cx++)
			{
				TileResolution[,] r = autotiler.Resolve(generator.GenerateChunk(StageEnvironmentKind.Swamp, seed, cx, cy, c, m), layout.BaseTerrain);
				for (int y = 0; y < c; y++)
					for (int x = 0; x < c; x++)
						wet[(cx - x0) * c + x + ((cy - y0) * c + y) * wide] = r[x + m, y + m].Terrain == "water";
			}
		}

		WaterShape Build(Vector2I chunk)
		{
			var shape = new WaterShape(c);
			Vector2I min = shape.WindowMin(chunk * c);
			int side = shape.WindowCells;
			for (int y = 0; y < side; y++)
				for (int x = 0; x < side; x++)
				{
					int wx = min.X + x - x0 * c, wy = min.Y + y - y0 * c;
					shape.WetBuffer[x + y * side] = wx >= 0 && wy >= 0 && wx < wide && wy < high && wet[wx + wy * wide];
				}
			shape.Build(chunk * c, seed);
			return shape;
		}

		WaterShape left = Build(new Vector2I(0, 0));
		WaterShape right = Build(new Vector2I(1, 0));
		int compared = 0, differ = 0, wetEmpty = 0;
		for (int y = -WaterShape.ShapeRing; y < c + WaterShape.ShapeRing; y++)
		{
			for (int x = c - WaterShape.ShapeRing; x < c + WaterShape.ShapeRing; x++)
			{
				int a = left.KindAt(x, y), b = right.KindAt(x, y);
				compared++;
				if (a != b) { differ++; continue; }
				if (a != 2) continue;
				ReadOnlySpan<Vector2> pa = left.PolygonAt(x, y), pb = right.PolygonAt(x, y);
				bool same = pa.Length == pb.Length;
				for (int i = 0; same && i < pa.Length; i++)
					same = pa[i].DistanceSquaredTo(pb[i]) < 1e-4f;
				if (!same) differ++;
			}
		}
		for (int y = 0; y < c; y++)
			for (int x = 0; x < c; x++)
				if (left.KindAt(x, y) == 3) wetEmpty++;
		if (differ > 0)
			warnings.Add($"Water: chunks (0, 0) and (1, 0) build {differ} of {compared} shared border cells differently, so "
				+ "the shore and its collision step at every chunk border. WaterShape.WindowMargin is too narrow, or something "
				+ "in it reads the chunk rather than the world.");
		if (wetEmpty > 0)
			warnings.Add($"Water: {wetEmpty} water cells of chunk (0, 0) come out with no water drawn in them at all. The "
				+ "blocked grid still calls them water, so they are an invisible no-go square for the flow field.");
	}

	private static void ValidateTerrainSeams(List<string> warnings)
	{
		const ulong seed = 20261016;
		const int c = TerrainStreamer.ChunkTiles;
		const int m = TerrainStreamer.Margin;
		var generator = new LevelGenerator();
		var autotiler = new TerrainAutotiler();

		// The two open stages that stamp the most, and every chapter on a topology built in a partial file
		// of LevelGenerator's (the bridge's band, the Court's layout, the Ruins' walls, the Cave's
		// caverns): each is held to the rule the day its file is built, with no edit here.
		var seamKinds = new List<StageEnvironmentKind> { StageEnvironmentKind.Swamp, StageEnvironmentKind.Volcanic };
		foreach (StageEnvironmentKind kind in Enum.GetValues<StageEnvironmentKind>())
		{
			StageTopology built = generator.Describe(kind).Topology;
			if (built != StageTopology.Open && built != StageTopology.Maze)
				seamKinds.Add(kind);
		}

		foreach (StageEnvironmentKind kind in seamKinds)
		{
			LevelLayout layout = generator.Describe(kind);
			foreach ((int cx, int cy) in new[] { (0, 0), (3, -2) })
			{
				// Cleaned as the game cleans it: walled ground is not (Node2DGame, TerrainStreamer).
				TileResolution[,] narrow = autotiler.Resolve(generator.GenerateChunk(kind, seed, cx, cy, c, m), layout.BaseTerrain, cleanup: !layout.Walled);
				TileResolution[,] wide = autotiler.Resolve(generator.GenerateChunk(kind, seed, cx, cy, c, m * 2), layout.BaseTerrain, cleanup: !layout.Walled);
				int differ = 0;
				for (int x = 0; x < c; x++)
					for (int y = 0; y < c; y++)
						if (narrow[x + m, y + m].Terrain != wide[x + m * 2, y + m * 2].Terrain
							|| narrow[x + m, y + m].Role != wide[x + m * 2, y + m * 2].Role)
							differ++;
				if (differ > 0)
					warnings.Add($"Terrain: {kind} chunk ({cx}, {cy}) resolves {differ} cells differently with a wider "
						+ "margin, so chunk borders will seam. TerrainStreamer.Margin is too narrow for the cleanup passes.");
			}

			// Raw grids of two horizontal neighbours, over the cells both of them generate.
			string[,] left = generator.GenerateChunk(kind, seed, 0, 0, c, m);
			string[,] right = generator.GenerateChunk(kind, seed, 1, 0, c, m);
			int rawDiffer = 0;
			for (int wx = c - m; wx < c + m; wx++)
				for (int y = 0; y < c + 2 * m; y++)
					if (left[wx + m, y] != right[wx - c + m, y])
						rawDiffer++;
			if (rawDiffer > 0)
				warnings.Add($"Terrain: {kind} chunks (0, 0) and (1, 0) disagree on {rawDiffer} raw cells they share. "
					+ "Something in LevelGenerator draws from an order-dependent RNG instead of a hash of the tile.");
		}

		// Every walled topology in a partial file (the Ruins' broken walls, the Cave's caverns, and whatever is
		// built after them) is held to "connected by construction" too: ExplorePlacer and
		// Node2DGame.IsExploreGroundClear skip the walk-away test on walled ground because of it.
		foreach (StageEnvironmentKind kind in seamKinds)
			if (generator.Describe(kind).Walled)
				ValidateNoSealedPockets(generator, kind, seed, warnings);

		LevelLayout maze = generator.Describe(StageEnvironmentKind.Castle);
		if (maze.Topology != StageTopology.Maze)
			return;
		const int span = 3 * c;
		var open = new bool[span, span];
		for (int cy = -1; cy <= 1; cy++)
		{
			for (int cx = -1; cx <= 1; cx++)
			{
				string[,] g = generator.GenerateChunk(StageEnvironmentKind.Castle, seed, cx, cy, c, m);
				for (int x = 0; x < c; x++)
					for (int y = 0; y < c; y++)
						open[(cx + 1) * c + x, (cy + 1) * c + y] = g[x + m, y + m] == maze.BaseTerrain;
			}
		}
		int total = 0;
		foreach (bool o in open) if (o) total++;
		var seen = new bool[span, span];
		var stack = new Stack<(int, int)>();
		stack.Push((c, c));
		seen[c, c] = true;
		int reached = 0;
		while (stack.Count > 0)
		{
			(int x, int y) = stack.Pop();
			reached++;
			foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
			{
				int nx = x + dx, ny = y + dy;
				if (nx < 0 || ny < 0 || nx >= span || ny >= span || seen[nx, ny] || !open[nx, ny]) continue;
				seen[nx, ny] = true;
				stack.Push((nx, ny));
			}
		}
		if (!open[c, c] || reached < total * 0.9f)
			warnings.Add($"Terrain: the maze round the start reaches only {reached} of {total} floor cells across nine chunks; "
				+ "corridors are not connecting across chunk borders (LevelGenerator.DoorMask).");
	}

	// Nine chunks round the start of a walled topology, flooded over every cell a body can stand on (not wall,
	// not water). A pocket is a piece of that ground which neither holds the start nor reaches the edge of the
	// nine: whatever is placed in it - an enemy, a chest, a secret - could never walk out, and nothing on
	// walled ground checks, because the ground is meant to be connected by construction
	// (LevelGenerator.Caverns.cs says why it is). The start must be standing ground as well. With the user
	// argument --terrain-stats it also prints how much of the nine is wall and water, for tuning.
	private static void ValidateNoSealedPockets(LevelGenerator generator, StageEnvironmentKind kind, ulong seed, List<string> warnings)
	{
		const int c = TerrainStreamer.ChunkTiles;
		const int m = TerrainStreamer.Margin;
		const int span = 3 * c;
		var walk = new bool[span, span];
		int walls = 0, water = 0;
		for (int cy = -1; cy <= 1; cy++)
		{
			for (int cx = -1; cx <= 1; cx++)
			{
				string[,] g = generator.GenerateChunk(kind, seed, cx, cy, c, m);
				for (int x = 0; x < c; x++)
				{
					for (int y = 0; y < c; y++)
					{
						string t = g[x + m, y + m];
						walls += t == LevelGenerator.WallTerrain ? 1 : 0;
						water += t == "water" ? 1 : 0;
						walk[(cx + 1) * c + x, (cy + 1) * c + y] = t != LevelGenerator.WallTerrain && t != "water";
					}
				}
			}
		}
		if (OS.GetCmdlineUserArgs().Contains("--terrain-stats"))
			GD.Print($"Terrain stats: {kind} nine chunks round the start, seed {seed}: "
				+ $"wall {100f * walls / (span * span):0.0}%, water {100f * water / (span * span):0.0}%");

		if (!walk[c, c])
			warnings.Add($"Terrain: {kind}'s start (world tile 0, 0) is not standing ground; the run would begin in a wall or water.");

		var label = new int[span, span];
		var stack = new Stack<(int, int)>();
		int pocketCells = 0, pockets = 0;
		(int, int)? firstPocket = null;
		int next = 0;
		for (int sx = 0; sx < span; sx++)
		{
			for (int sy = 0; sy < span; sy++)
			{
				if (!walk[sx, sy] || label[sx, sy] != 0)
					continue;
				next++;
				label[sx, sy] = next;
				stack.Push((sx, sy));
				int size = 0;
				bool edge = false, start = false;
				while (stack.Count > 0)
				{
					(int x, int y) = stack.Pop();
					size++;
					edge |= x == 0 || y == 0 || x == span - 1 || y == span - 1;
					start |= x == c && y == c;
					foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
					{
						int nx = x + dx, ny = y + dy;
						if (nx < 0 || ny < 0 || nx >= span || ny >= span || label[nx, ny] != 0 || !walk[nx, ny]) continue;
						label[nx, ny] = next;
						stack.Push((nx, ny));
					}
				}
				if (!edge && !start)
				{
					pockets++;
					pocketCells += size;
					firstPocket ??= (sx - c, sy - c);
				}
			}
		}
		if (pockets > 0)
			warnings.Add($"Terrain: {kind} has {pockets} sealed pocket(s), {pocketCells} cells of standing ground walled or "
				+ $"watered in, round the start (first near world tile {firstPocket}). Its topology claims to be connected "
				+ "by construction, and nothing on walled ground checks: whatever is placed in one cannot walk out.");
	}

	// The input map lives in project.godot as a wall of serialised event objects, which is the one
	// place in this project where a merge can silently delete a feature: drop the four lines that
	// carry the joypad and the game still builds, still runs, and simply cannot be played with a
	// controller. Nothing else would report it, because there is no controller in a headless run.
	//
	// So this checks the SHAPE of the map rather than the bindings themselves - that each action
	// exists, and that the ones a pad needs actually carry a pad event.
	private static void ValidateInputMap(List<string> warnings)
	{
		// Every action any script reads by name. A typo here is a silent no-op at runtime, because
		// Input.IsActionPressed on an unknown action just returns false.
		string[] required =
		{
			"ui_accept", "ui_cancel", "pause",
			"ui_up", "ui_down", "ui_left", "ui_right",
			"move_up", "move_down", "move_left", "move_right",
		};

		foreach (string action in required)
		{
			if (!InputMap.HasAction(action))
				warnings.Add($"Input: action '{action}' is missing from project.godot; everything that reads it is dead.");
		}

		// A is select, Start is pause, and the left stick both moves and scrolls. Each of those is
		// one event in project.godot and none of them is visible in any C# file.
		RequireJoypadButton(warnings, "ui_accept", JoyButton.A, "A cannot press a menu option");
		RequireJoypadButton(warnings, "ui_cancel", JoyButton.Start, "Start cannot back out of a menu");
		RequireJoypadButton(warnings, "pause", JoyButton.Start, "Start cannot pause a run");

		RequireJoypadAxis(warnings, "move_up", JoyAxis.LeftY, "the left stick cannot move the player");
		RequireJoypadAxis(warnings, "move_left", JoyAxis.LeftX, "the left stick cannot move the player");
		RequireJoypadAxis(warnings, "ui_down", JoyAxis.LeftY, "the left stick cannot scroll a menu");
		RequireJoypadAxis(warnings, "ui_right", JoyAxis.LeftX, "the left stick cannot scroll a menu");

		// Movement reads move_* precisely because ui_* has to be deaf to a thumb resting on the
		// stick. If the two deadzones ever converge, one of the two jobs is being done wrongly.
		if (InputMap.HasAction("ui_down") && InputMap.HasAction("move_down")
			&& InputMap.ActionGetDeadzone("move_down") >= InputMap.ActionGetDeadzone("ui_down"))
		{
			warnings.Add("Input: move_* deadzone is no longer below ui_*'s. Movement then throws away "
				+ "the first half of the stick, which is the reason the two action sets are separate.");
		}
	}

	private static void RequireJoypadButton(List<string> warnings, string action, JoyButton button, string consequence)
	{
		if (!InputMap.HasAction(action))
			return;

		bool found = InputMap.ActionGetEvents(action)
			.Any(e => e is InputEventJoypadButton pad && pad.ButtonIndex == button);

		if (!found)
			warnings.Add($"Input: '{action}' has no {button} button event - {consequence}.");
	}

	private static void RequireJoypadAxis(List<string> warnings, string action, JoyAxis axis, string consequence)
	{
		if (!InputMap.HasAction(action))
			return;

		bool found = InputMap.ActionGetEvents(action)
			.Any(e => e is InputEventJoypadMotion motion && motion.Axis == axis);

		if (!found)
			warnings.Add($"Input: '{action}' has no {axis} motion event - {consequence}.");
	}

	// Music fails the same silent way, plus one of its own: a WAV whose .import does not say
	// edit/loop_mode=2 (Forward) plays its minute once and stops, which in a fifteen-minute run is
	// fourteen minutes of nothing. That setting lives in the .import file, not the WAV, so deleting
	// the .import loses it - the comparison is against LoopModeEnum.Disabled to catch exactly that.
	// The stems must also be one length, or AudioStreamSynchronized drifts them apart a loop at a time.
	private static void ValidateMusic(List<string> warnings)
	{
		foreach (string path in MusicCatalog.AllTracks())
		{
			if (!ResourceLoader.Exists(path))
			{
				warnings.Add($"Music: missing '{path}'. Run: python tools/audio/music.py");
				continue;
			}
			if (ResourceLoader.Load<AudioStream>(path) is AudioStreamWav wav
				&& wav.LoopMode == AudioStreamWav.LoopModeEnum.Disabled)
			{
				warnings.Add($"Music: '{path}' does not loop. Set edit/loop_mode=2 in its .import.");
			}
		}

		// Two stage rosters must agree (MusicCatalog's note says why it is not yet folded into
		// StageCatalog). A chapter with no row plays the Forest - audible, but in the wrong key -
		// and a row for a chapter that does not exist is a renumbering nobody followed through.
		var stageIndices = new HashSet<int>(StageCatalog.All.Select(s => s.Index));
		foreach (StageDefinition stage in StageCatalog.All)
		{
			if (!MusicCatalog.HasSetForStage(stage.Index))
				warnings.Add($"Music: chapter {stage.Index} ({stage.DisplayName}) has no set in MusicCatalog.PerStage and plays '{MusicCatalog.DefaultRunSet.Id}'.");
		}
		foreach (int index in MusicCatalog.StageIndices)
		{
			if (!stageIndices.Contains(index))
				warnings.Add($"Music: MusicCatalog.PerStage has a set for stage {index}, which StageCatalog does not list.");
		}

		foreach (MusicSet set in MusicCatalog.AllSets())
		{
			double length = -1.0;
			foreach (string path in set.Stems)
			{
				if (!ResourceLoader.Exists(path) || ResourceLoader.Load<AudioStream>(path) is not AudioStream stream)
					continue;
				double l = stream.GetLength();
				if (length >= 0.0 && Math.Abs(l - length) > 0.001)
					warnings.Add($"Music: the '{set.Id}' stems differ in length ({length:0.000} s and {l:0.000} s at {path}); they will drift apart.");
				length = l;
			}
		}
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

			// Exempt the one chapter that is not somewhere he has been. CorruptionText exists to
			// say what the dark wizard has done to a place, and demanding it of a chapter outside
			// the campaign would mean inventing fiction to satisfy a check.
			if (string.IsNullOrWhiteSpace(stage.CorruptionText)
				&& stage.EnvironmentKind != StageEnvironmentKind.Sketchbook)
			{
				warnings.Add($"Stage '{stage.Id}' has no CorruptionText, so its card never says how the dark wizard holds it.");
			}

			if (!stage.IsPlayable && string.IsNullOrWhiteSpace(stage.LockedHint) && stage.Gate != StageGate.PreviousBossAndWizards)
				warnings.Add($"Stage '{stage.Id}' is not playable and has no LockedHint, so its card locks with no explanation.");
		}
	}

	// THE CAMPAIGN ORDER (.ai/campaign.md). StageCatalog's CampaignOrder is the only place it is
	// written, and every rule about which chapter opens which reads it - so a slip in it is a chapter
	// nobody can reach, a gate that points forward, or a finale that opens early. A build sees none of
	// it, and neither does a player until he is stuck.
	private static void ValidateCampaign(List<string> warnings)
	{
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string id in StageCatalog.CampaignOrderIds)
		{
			if (StageCatalog.GetById(id) == null)
				warnings.Add($"Campaign: the order names '{id}', which is not a stage in StageCatalog.");
			// A chapter listed twice comes after itself: the order would be a cycle.
			if (!seen.Add(id))
				warnings.Add($"Campaign: '{id}' appears twice in the order, so it would have to be beaten to open itself.");
		}

		IReadOnlyList<StageDefinition> campaign = StageCatalog.Campaign;
		if (campaign.Count == 0)
		{
			warnings.Add("Campaign: the order is empty, so nothing opens anything.");
			return;
		}

		if (campaign[0].Gate != StageGate.Open || !campaign[0].IsPlayable)
			warnings.Add($"Campaign: the first chapter, {campaign[0].DisplayName}, must be open and playable, or a new save has nowhere to start.");

		// The gate chapters (.ai/achievements-and-secrets.md 2.5, .ai/endgame-castle.md 1.9): the last
		// chapter is one, and once the road reaches the first, every chapter after it is boss-gated or is
		// another gate chapter (the castle's Throne). Nothing past a gate is ever open from the start.
		StageDefinition final = StageCatalog.FinalChapter;
		StageDefinition firstGate = StageCatalog.GateChapter;
		if (final == null || final.Gate != StageGate.PreviousBossAndWizards)
			warnings.Add("Campaign: the last chapter in the order must be a gate chapter (its previous boss and every wizard freed before it).");
		if (firstGate != null)
		{
			foreach (StageDefinition after in campaign.Skip(StageCatalog.CampaignPositionOf(firstGate.Id) + 1))
			{
				if (!StageCatalog.IsBossGated(after.Gate))
					warnings.Add($"Campaign: {after.DisplayName} comes after the gate chapter {firstGate.DisplayName} but is gated {after.Gate}; past the gate the road is boss-gated or another gate.");
			}
		}

		foreach (StageDefinition stage in StageCatalog.All)
		{
			bool inCampaign = StageCatalog.IsInCampaign(stage);
			if (!inCampaign && stage.Gate != StageGate.Open)
				warnings.Add($"Campaign: {stage.DisplayName} ({stage.Id}) is outside the order but gated {stage.Gate}, so nothing could ever open it.");
			if (inCampaign && stage.Gate == StageGate.Open && StageCatalog.CampaignPositionOf(stage.Id) > 0)
				warnings.Add($"Campaign: {stage.DisplayName} is in the middle of the order but always open, so the chapters before it gate nothing.");
		}

		for (int i = 0; i < campaign.Count; i++)
		{
			StageDefinition stage = campaign[i];
			if (!StageCatalog.IsBossGated(stage.Gate))
				continue;
			bool isGate = stage.Gate == StageGate.PreviousBossAndWizards;

			StageDefinition previous = StageCatalog.PreviousChapter(stage);
			if (previous == null)
			{
				warnings.Add($"Campaign: {stage.DisplayName} has no playable chapter before it, so no boss can ever open it.");
				continue;
			}
			if (StageCatalog.CampaignPositionOf(previous.Id) >= i)
				warnings.Add($"Campaign: {stage.DisplayName} is gated on {previous.DisplayName}, which is not earlier in the order.");
			if (!stage.IsPlayable)
				continue;
			if (BossCatalog.ForStageIndex(previous.Index) == null)
				warnings.Add($"Campaign: {stage.DisplayName} is opened by the boss of {previous.DisplayName}, which has none in BossCatalog.");
			// A gate chapter is never written open by its boss alone: that would skip the wizards.
			if (isGate && StageCatalog.ChapterOpenedBy(previous.Id) == stage)
				warnings.Add($"Campaign: beating {previous.DisplayName} writes the gate chapter {stage.DisplayName} open, without the wizards it counts.");
			else if (!isGate && StageCatalog.ChapterOpenedBy(previous.Id) != stage)
				warnings.Add($"Campaign: {stage.DisplayName}'s gate names {previous.DisplayName}, but beating {previous.DisplayName} opens {StageCatalog.ChapterOpenedBy(previous.Id)?.DisplayName ?? "nothing"}.");
		}

		// END TO END. A new save in memory, every available chapter's boss beaten the way a victory
		// records it (the lifetime record, and the chapter written to the save), until nothing new
		// opens. Every playable chapter on the road - before the first gate - must be reached that way;
		// a gate needs the wizards as well (ValidateGateReachability walks a new save through those).
		var save = new SaveData();
		for (int pass = 0; pass <= campaign.Count; pass++)
		{
			bool changed = false;
			foreach (StageDefinition stage in campaign)
			{
				BossDefinition boss = BossCatalog.ForStageIndex(stage.Index);
				if (boss == null || !GlobalStatsManager.IsStageAvailable(save, stage) || save.Lifetime.HasDefeatedBoss(boss.Id))
					continue;
				RecordVictory(save, boss, stage);
				changed = true;
			}
			if (!changed)
				break;
		}
		int gatePosition = firstGate != null ? StageCatalog.CampaignPositionOf(firstGate.Id) : campaign.Count;
		foreach (StageDefinition stage in campaign)
		{
			if (stage.IsPlayable && StageCatalog.CampaignPositionOf(stage.Id) < gatePosition
				&& !GlobalStatsManager.IsStageAvailable(save, stage))
				warnings.Add($"Campaign: {stage.DisplayName} cannot be reached by beating every boss in order from a new save.");
		}
		foreach (StageDefinition gate in StageCatalog.GateChapters.Where(s => s.IsPlayable))
		{
			if (GlobalStatsManager.IsStageAvailable(save, gate) && !GlobalStatsManager.IsGateMet(save, gate))
				warnings.Add($"Campaign: {gate.DisplayName} opens on bosses alone, without the wizards its gate counts.");
			SaveData through = SaveThroughGates();
			if (!GlobalStatsManager.IsStageAvailable(through, gate))
				warnings.Add($"Campaign: {gate.DisplayName} stays closed to a save that has beaten its previous boss and freed every wizard.");
		}

		// NOTHING UNBUILT IS EVER WRITTEN INTO A SAVE (.ai/endgame-castle.md 1.9). An unbuilt gate's
		// previous playable chapter is earlier than it will be - the Throne's is the Emberdeep until the
		// bridge and the Court exist - so a save with the Warden of the Deep beaten and every wizard free
		// "meets" it; writing that down (RecordMetGates, at every run's end) would hand the Throne to that
		// save the day it flips, past a bridge and a court it never crossed. Every boss beaten and every
		// wizard free is the save most likely to be handed something.
		SaveData veteran = SaveThroughGates();
		foreach (BossDefinition beaten in BossCatalog.All)
			veteran.Lifetime.BossesDefeated.Add(beaten.Id);
		foreach (StageDefinition written in GlobalStatsManager.RecordMetGates(veteran).Where(s => !s.IsPlayable))
			warnings.Add($"Campaign: {written.DisplayName} is not playable, but a save that has beaten every boss had it written into its opened chapters (GlobalStatsManager.RecordMetGates).");

		ValidateCampaignToDo(warnings);
	}

	// Every wizard free and the boss before every gate chapter beaten: what opens each gate.
	private static SaveData SaveThroughGates()
	{
		var save = new SaveData();
		foreach (string wizardId in UnlockCatalog.AllWizardIds)
			GlobalStatsManager.UnlockCharacter(save, wizardId);
		foreach (StageDefinition gate in StageCatalog.GateChapters)
		{
			StageDefinition previous = StageCatalog.PreviousChapter(gate);
			BossDefinition boss = previous != null ? BossCatalog.ForStageIndex(previous.Index) : null;
			if (boss != null)
				save.Lifetime.BossesDefeated.Add(boss.Id);
		}
		return save;
	}

	// What a boss victory does to the save, the two halves Node2DGame performs at run end: the run
	// folds into the lifetime record, and the chapter the order says it opens is written.
	private static void RecordVictory(SaveData save, BossDefinition boss, StageDefinition stage)
	{
		save.Lifetime.Absorb(new global::RunResult
		{
			Outcome = global::RunOutcome.Victory,
			BossId = boss.Id,
			StageId = stage.Id,
		});
		StageDefinition opened = BossCatalog.ChapterOpenedBy(boss);
		if (opened != null)
			GlobalStatsManager.UnlockStage(save, opened.Id);
	}

	// Every spell and wizard owned and no boss beaten: the shape of a save that met the old, schema 18
	// gate. The migration samples use it; the gate itself needs a boss now (SaveThroughGates).
	private static SaveData SaveWithEverything()
	{
		var save = new SaveData();
		foreach (string spellId in UnlockCatalog.AllSpellIds)
			GlobalStatsManager.UnlockSpell(save, spellId);
		foreach (string wizardId in UnlockCatalog.AllWizardIds)
			GlobalStatsManager.UnlockCharacter(save, wizardId);
		return save;
	}

	// The Phase 2 to-do list for chapters still being built (.ai/campaign.md, the Phase 2 split), one
	// line per chapter, in the manner of the bestiary's. It shortens by itself: each item is a file
	// that does not exist yet, or a line not yet filled in, and the line goes when the chapter is
	// playable. A PLAYABLE chapter with any of these is not a to-do but a fault, and says so.
	private static void ValidateCampaignToDo(List<string> warnings)
	{
		// The world map is asked once, for the castle's chapters that have no stop yet (ValidateWorldMap
		// leaves those to this list).
		WorldMapLayout map = WorldMapLayout.Load(out _);
		var generator = new LevelGenerator();
		foreach (StageDefinition stage in StageCatalog.Campaign)
		{
			var todo = new List<string>();
			BossDefinition boss = BossCatalog.ForStageIndex(stage.Index);
			if (boss != null && boss.IsOnStandIn)
				todo.Add($"boss {boss.DisplayName} fights as '{ShortScenePath(boss.StandInScenePath)}' until '{ShortScenePath(boss.ScenePath)}' exists");
			MusicSet own = MusicCatalog.OwnSetForStage(stage.Index);
			if (own != null && own.StandIn != null && !own.IsRendered)
				todo.Add($"music plays '{own.StandIn.Id}' until {own.Id}_bed/_pulse/_lead.wav are rendered and imported");
			// The castle's plumbing (.ai/endgame-castle.md 1.9): a topology named and not yet built, rules
			// designed and still the null line, a place on the world map not yet drawn.
			LevelLayout ground = generator.Describe(stage.EnvironmentKind);
			if (ground.DeclaredTopology != ground.Topology)
				todo.Add($"its ground is open field until LevelGenerator.{ground.DeclaredTopology}.cs builds the {ground.DeclaredTopology} topology");
			if (ChapterRulesCatalog.IsAwaitingRules(stage.Id))
				todo.Add("its rules are still the null line in ChapterRulesCatalog");
			// (A playable chapter with no stop is ValidateWorldMap's fault to report, not a to-do.)
			if (!stage.IsPlayable && map != null && map.Get(stage.Id) == null)
				todo.Add("it has no place on the world map (PLACES in tools/art/world_map.py)");

			if (todo.Count == 0)
				continue;
			if (stage.IsPlayable)
				warnings.Add($"Campaign: {stage.DisplayName} is playable but still has stand-ins - " + string.Join("; ", todo) + ".");
			else
				warnings.Add($"Campaign to-do (.ai/campaign.md 7, .ai/endgame-castle.md 9): {stage.DisplayName} ({stage.Id}) is not playable yet - "
					+ string.Join("; ", todo) + "; then IsPlayable = true, last.");
		}

		// A thing found in the world needs somewhere to find it. A discovery whose site no chapter's
		// side event pays out can never be had - a fault once its chapter is built, a to-do before.
		foreach (UnlockDefinition found in UnlockCatalog.All.Where(d => !string.IsNullOrWhiteSpace(d.SiteId)))
		{
			StageDefinition holder = StageCatalog.All.FirstOrDefault(s =>
				SideEventDirector.ForStage(s.Id)?.SiteId.Equals(found.SiteId, StringComparison.OrdinalIgnoreCase) == true);
			if (holder == null)
				warnings.Add($"Campaign to-do (Phase 2, .ai/campaign.md): {found.Kind} '{found.Id}' is found at '{found.SiteId}', and no chapter's side event (SideEventDirector.ForStage) is that site yet, so nobody can find it.");
			else if (!holder.IsPlayable)
				warnings.Add($"Campaign to-do (Phase 2, .ai/campaign.md): {found.Kind} '{found.Id}' is found in {holder.DisplayName}, which is not playable yet.");
		}
	}

	// SCHEMA 13 ON SAMPLE SAVES (SaveData.MigrateToCampaignOrder). Each is a save as schema 12 wrote
	// it, migrated in memory and then asked which chapters it can enter. The rules being held: nobody
	// loses a chapter they had; a boss a save has beaten opens what it opens NOW; and a save already
	// past the two early chapters has them, so they can never block anyone.
	// Schema 18 (Save and Augury removed): a save holding two Hoarded Insights and one Augury is paid
	// 110 + 190 + 130 = 430 back, and the two rows leave it; a save that bought none is untouched; and
	// nothing still reads the retired ids, or the refund would be a refund for something still sold.
	private static void ValidateRetiredChargeRefund(List<string> warnings)
	{
		var bought = new SaveData { SchemaVersion = 17, TotalCurrency = 50 };
		bought.ArcaneUpgradeLevels["banked_levels"] = 2;
		bought.ArcaneUpgradeLevels["auguries"] = 1;
		bought.ArcaneUpgradeLevels["bans"] = 1;
		bought.Migrate(log: false);
		if (bought.TotalCurrency != 50 + 430 || bought.ArcaneUpgradeLevels.ContainsKey("banked_levels")
			|| bought.ArcaneUpgradeLevels.ContainsKey("auguries") || !bought.ArcaneUpgradeLevels.ContainsKey("bans"))
			warnings.Add($"Migration: schema 18 should refund 430 for two Hoarded Insights and an Augury and keep the ban; "
				+ $"the purse is {bought.TotalCurrency} (from 50) and the rows are {string.Join(", ", bought.ArcaneUpgradeLevels.Keys)}.");

		var none = new SaveData { SchemaVersion = 17, TotalCurrency = 50 };
		none.Migrate(log: false);
		if (none.TotalCurrency != 50)
			warnings.Add($"Migration: schema 18 paid {none.TotalCurrency - 50} to a save that bought no retired charge.");
	}

	private static void ValidateCampaignMigration(List<string> warnings)
	{
		var fresh = new SaveData();
		foreach (StageDefinition stage in StageCatalog.All)
		{
			if (GlobalStatsManager.IsStageAvailable(fresh, stage) && stage.Gate != StageGate.Open)
				warnings.Add($"Migration: a brand-new save can enter {stage.DisplayName}, which is gated {stage.Gate}.");
		}

		// (what the old save held, what must be open after, what must still be closed)
		CheckMigration(warnings, "beat Elderbark", stored: new[] { "stage_1" },
			open: new[] { "stage_0", "stage_1", "stage_2" }, closed: new[] { "stage_3" });
		CheckMigration(warnings, "beat Elderbark and the Gaoler", stored: new[] { "stage_1", "stage_2" },
			open: new[] { "stage_0", "stage_1", "stage_2", "stage_3" }, closed: new[] { "stage_4" });
		CheckMigration(warnings, "beat everything up to the Still Warden",
			stored: new[] { "stage_1", "stage_2", "stage_3", "stage_4", "stage_5", "stage_6" },
			open: new[] { "stage_0", "stage_1", "stage_2", "stage_3", "stage_4", "stage_5", "stage_6" },
			closed: new[] { "stage_7" });
		// Known only from the lifetime record, and only from a deed: the Gaoler opens the Swamp now.
		CheckMigration(warnings, "the Gaoler in the lifetime record only", stored: new string[0],
			open: new[] { "stage_1", "stage_2", "stage_3" }, closed: new[] { "stage_4" },
			lifetimeBosses: new[] { "forest_treant", "cave_choir", "castle_warden" });
		CheckMigration(warnings, "the Gaoler's deed only", stored: new[] { "stage_1" },
			open: new[] { "stage_1", "stage_2", "stage_3" }, closed: new[] { "stage_4" },
			deeds: new[] { "castle_conqueror" });
		// A shape a real save has: chapters open out of the old order's sequence.
		CheckMigration(warnings, "a gap in the old order", stored: new[] { "stage_1", "stage_3", "stage_4" },
			open: new[] { "stage_0", "stage_1", "stage_2", "stage_3", "stage_4" }, closed: new[] { "stage_5" });

		// A save that had met the old gate - every spell and wizard - can still enter the finale after
		// every step (schema 19 writes it open for such a save, whatever the gate has become).
		SaveData done = SaveWithEverything();
		done.SchemaVersion = 12;
		done.Migrate(log: false);
		// The gate chapter (the Emberdeep), which is what such a save had met - not FinalChapter, which is
		// the castle's Throne and not playable yet, so reading it would quietly check nothing.
		StageDefinition final = StageCatalog.GateChapter;
		if (final != null && final.IsPlayable && !GlobalStatsManager.IsStageAvailable(done, final))
			warnings.Add($"Migration: a save with every spell and wizard cannot enter {final.DisplayName} after schema 13.");
	}

	private static void CheckMigration(List<string> warnings, string label, string[] stored, string[] open,
		string[] closed, string[] lifetimeBosses = null, string[] deeds = null)
	{
		var save = new SaveData { SchemaVersion = 12 };
		save.UnlockedStageIds.AddRange(stored);
		foreach (string bossId in lifetimeBosses ?? new string[0])
			save.Lifetime.BossesDefeated.Add(bossId);
		save.UnlockedAchievementIds.AddRange(deeds ?? new string[0]);

		if (!save.Migrate(log: false))
			warnings.Add($"Migration ({label}): a schema-12 save was not migrated.");

		// Nobody loses anything: every chapter the save had opened is still there and still enterable.
		foreach (string id in stored)
		{
			StageDefinition had = StageCatalog.GetById(id);
			if (!save.UnlockedStageIds.Contains(id) || (had != null && had.IsPlayable && !GlobalStatsManager.IsStageAvailable(save, had)))
				warnings.Add($"Migration ({label}): the save lost {had?.DisplayName ?? id}.");
		}
		foreach (string id in open)
		{
			StageDefinition stage = StageCatalog.GetById(id);
			if (stage != null && stage.IsPlayable && !GlobalStatsManager.IsStageAvailable(save, stage))
				warnings.Add($"Migration ({label}): {stage.DisplayName} should be open after schema 13 and is not.");
		}
		foreach (string id in closed)
		{
			StageDefinition stage = StageCatalog.GetById(id);
			if (stage != null && GlobalStatsManager.IsStageAvailable(save, stage))
				warnings.Add($"Migration ({label}): {stage.DisplayName} opened, though nothing this save did earns it.");
		}

		// The early chapters never block anyone: a save that is at the Cave or past it has earned
		// both, whether or not they are built yet - and so has them the day they become playable.
		bool pastThem = save.UnlockedStageIds.Any(id => StageCatalog.CampaignPositionOf(id) > StageCatalog.CampaignPositionOf("stage_10"));
		foreach (string early in new[] { "stage_9", "stage_10" })
		{
			StageDefinition stage = StageCatalog.GetById(early);
			if (pastThem && stage != null && !GlobalStatsManager.IsStageEarned(save, stage))
				warnings.Add($"Migration ({label}): the save is past {stage.DisplayName} but has not earned it, so it would block him when it opens.");
		}

		// Once is enough: a second migration finds nothing to do.
		int before = save.UnlockedStageIds.Count;
		if (save.Migrate(log: false) || save.UnlockedStageIds.Count != before)
			warnings.Add($"Migration ({label}): migrating twice changed the save again.");
	}

	// GATE REACHABILITY (.ai/achievements-and-secrets.md 2.6; .ai/endgame-castle.md 1.6). The rule the
	// old final gate broke: a gate must never count something that can only be earned behind it. It
	// counted every spell, and Fireball was paid only by the boss behind it, so no save written since
	// schema 8 could open the Emberdeep - and nothing said so, because the old check asked only whether
	// a save that already owned everything got in. This asks it of every gate chapter, two ways:
	//   - on paper: everything the gate counts has a source before it - its previous boss is earlier
	//     in the order, and every wizard it counts is a starter, a Feat's that names no chapter at or
	//     past it, or a chapter's deed earlier in the campaign; never a secret's, which is missable;
	//   - in memory: a brand-new save walks the road before the gate - every boss beaten as a victory
	//     records it, every side event won, every built secret found, every Feat done - and must then
	//     be able to enter it. And the gate must want both halves: without the boss, or without any one
	//     wizard it counts, it stays shut.
	// It is also what keeps Tituba out of the Emberdeep's count once the Dungeon moves behind it, and in
	// the Throne's; and the last gate must count every wizard, which is what makes "he is reachable only
	// when every wizard is free" true where he is.
	private static void ValidateGateReachability(List<string> warnings)
	{
		List<StageDefinition> gates = StageCatalog.GateChapters.ToList();
		if (gates.Count == 0)
		{
			warnings.Add("Gate reachability: the campaign has no gate chapter (StageGate.PreviousBossAndWizards).");
			return;
		}

		StageDefinition lastGate = gates[gates.Count - 1];
		foreach (string wizardId in UnlockCatalog.AllWizardIds)
		{
			if (!GlobalStatsManager.CountsTowardGate(wizardId, lastGate))
				warnings.Add($"Gate reachability: the last gate, {lastGate.DisplayName}, does not count '{wizardId}', so the campaign can end with him still held.");
		}

		foreach (StageDefinition gate in gates)
		{
			int gatePosition = StageCatalog.CampaignPositionOf(gate.Id);
			bool BeforeGate(string stageId)
			{
				if (string.IsNullOrWhiteSpace(stageId))
					return true;
				int position = StageCatalog.CampaignPositionOf(stageId);
				return position < 0 ? StageCatalog.GetById(stageId)?.Gate == StageGate.Open : position < gatePosition;
			}

			// On paper.
			StageDefinition previous = StageCatalog.PreviousChapter(gate);
			BossDefinition gateBoss = previous != null ? BossCatalog.ForStageIndex(previous.Index) : null;
			if (gateBoss == null || StageCatalog.CampaignPositionOf(previous.Id) >= gatePosition)
				warnings.Add($"Gate reachability: {gate.DisplayName}'s gate names no boss before it, so half of it can never be met.");
			List<string> counted = GlobalStatsManager.GateWizardIds(gate).ToList();
			foreach (string wizardId in counted)
			{
				UnlockDefinition wizard = UnlockCatalog.GetCharacter(wizardId);
				if (wizard == null || wizard.Source == UnlockSource.Starter)
					continue;
				AchievementDefinition deed = UnlockCatalog.SourceDeedOf(wizard);
				if (deed == null)
					warnings.Add($"Gate reachability: {gate.DisplayName} counts '{wizardId}', whose source '{wizard.SourceId}' is no achievement, so nothing can free him.");
				else if (deed.Kind == AchievementKind.Secret)
					warnings.Add($"Gate reachability: {gate.DisplayName} counts '{wizardId}', whom a secret frees ('{deed.Id}'); a secret is missable, and no gate may wait on one.");
				else if (!BeforeGate(deed.StageId))
					warnings.Add($"Gate reachability: {gate.DisplayName} counts '{wizardId}', freed by '{deed.Id}' in {StageCatalog.DisplayNameFor(deed.StageId)}, which is not before the gate.");
			}

			// In memory: a new save walks the road.
			var save = new SaveData();
			for (int pass = 0; pass <= StageCatalog.Campaign.Count + 1; pass++)
			{
				bool changed = false;
				foreach (StageDefinition stage in StageCatalog.Campaign)
				{
					if (!BeforeGate(stage.Id) || !GlobalStatsManager.IsStageAvailable(save, stage))
						continue;
					BossDefinition boss = BossCatalog.ForStageIndex(stage.Index);
					if (boss != null && !save.Lifetime.HasDefeatedBoss(boss.Id))
					{
						WinOnPaper(save, boss, stage);
						changed = true;
					}
					string site = global::SideEventDirector.ForStage(stage.Id)?.SiteId;
					if (!string.IsNullOrWhiteSpace(site) && !save.Lifetime.HasWonEvent(site))
					{
						DiscoveryRewards.GrantSite(save, site);
						changed = true;
					}
					foreach (SecretDefinition secret in SecretCatalog.ForStage(stage.Id))
					{
						if (save.HasFoundSecret(secret.Id))
							continue;
						AchievementManager.Complete(save, AchievementDefinitions.SecretDeedId(secret.Id));
						changed = true;
					}
				}
				foreach (AchievementDefinition feat in AchievementDefinitions.All.Where(d =>
					(d.Kind == AchievementKind.Feat || d.Kind == AchievementKind.Collection) && BeforeGate(d.StageId)))
				{
					if (GlobalStatsManager.IsAchievementUnlocked(save, feat.Id))
						continue;
					AchievementManager.Complete(save, feat.Id);
					changed = true;
				}
				if (!changed)
					break;
			}
			if (!GlobalStatsManager.IsStageEarned(save, gate))
			{
				var held = counted.Where(id => !GlobalStatsManager.IsCharacterUnlocked(save, id)).ToList();
				warnings.Add($"Gate reachability: a new save that does everything before {gate.DisplayName} still cannot enter it"
					+ $" - boss {(gateBoss != null && save.Lifetime.HasDefeatedBoss(gateBoss.Id) ? "beaten" : "not beaten")}"
					+ (held.Count > 0 ? $", still held: {string.Join(", ", held)}" : "") + ".");
			}

			// Both halves.
			var noBoss = new SaveData();
			foreach (string id in counted)
				GlobalStatsManager.UnlockCharacter(noBoss, id);
			if (GlobalStatsManager.IsStageEarned(noBoss, gate))
				warnings.Add($"Gate reachability: {gate.DisplayName} opens with every wizard free but {gateBoss?.DisplayName ?? "its boss"} unbeaten.");
			foreach (string missing in counted.Where(id => !UnlockCatalog.IsStarter(id, UnlockKind.Character)))
			{
				var oneShort = new SaveData();
				if (gateBoss != null)
					oneShort.Lifetime.BossesDefeated.Add(gateBoss.Id);
				foreach (string id in counted.Where(id => !id.Equals(missing, StringComparison.OrdinalIgnoreCase)))
					GlobalStatsManager.UnlockCharacter(oneShort, id);
				if (GlobalStatsManager.IsStageEarned(oneShort, gate))
					warnings.Add($"Gate reachability: {gate.DisplayName} opens with '{missing}' still held.");
			}
		}
	}

	// A boss victory as the end of a run records it: the run's achievements (ApplyRunAchievements folds
	// the run into the lifetime record and pays the deeds), and the chapter the order says it opens.
	private static void WinOnPaper(SaveData save, BossDefinition boss, StageDefinition stage)
	{
		AchievementManager.ApplyRunAchievements(save, new global::RunResult
		{
			Outcome = global::RunOutcome.Victory,
			BossId = boss.Id,
			StageId = stage.Id,
		});
		StageDefinition opened = BossCatalog.ChapterOpenedBy(boss);
		if (opened != null)
			GlobalStatsManager.UnlockStage(save, opened.Id);
	}

	// SCHEMA 19 ON SAMPLE SAVES (SaveData.MigrateToAchievementUnlocks, .ai/achievements-and-secrets.md
	// section 3). Each is a save as schema 18 wrote it, migrated in memory. The rules being held: nothing
	// owned is taken away; a done deed is paid its current reward and never Arcane Energy; a won event
	// shows its deed done; a save that had met the old gate still enters the Emberdeep; a new save is
	// granted nothing; and a second pass changes nothing. Then two older saves, run through every step,
	// because schema 19 froze what the old steps read - a pre-campaign save must still not be handed
	// the rescued wizards, and a save that had everything at 9 must still be credited its discoveries.
	private static void ValidateAchievementMigration(List<string> warnings)
	{
		bool OwnsSpell(SaveData s, string id) => GlobalStatsManager.IsSpellUnlockedForLevelUp(s, id);

		SaveData Sample(string label, Action<SaveData> shape, Action<SaveData> check)
		{
			var save = new SaveData { SchemaVersion = 18, TotalCurrency = 100 };
			shape(save);
			var ownedBefore = save.UnlockedSpellIds.Concat(save.UnlockedBoonIds).Concat(save.UnlockedCharacterIds)
				.Concat(save.UnlockedStageIds).Concat(save.UnlockedAchievementIds).ToList();
			if (!save.Migrate(log: false))
				warnings.Add($"Achievement migration ({label}): a schema-18 save was not migrated.");
			var ownedAfter = save.UnlockedSpellIds.Concat(save.UnlockedBoonIds).Concat(save.UnlockedCharacterIds)
				.Concat(save.UnlockedStageIds).Concat(save.UnlockedAchievementIds).ToList();
			foreach (string id in ownedBefore.Where(id => !ownedAfter.Contains(id, StringComparer.OrdinalIgnoreCase)))
				warnings.Add($"Achievement migration ({label}): the save lost '{id}'.");
			if (save.TotalCurrency != 100)
				warnings.Add($"Achievement migration ({label}): the purse changed from 100 to {save.TotalCurrency}; schema 19 pays unlocks, never Arcane Energy.");
			check(save);

			// Twice: the step again, on the migrated save, changes nothing.
			string once = Json.Stringify(save.ToGodotDictionary());
			save.MigrateToAchievementUnlocks();
			AchievementManager.Reconcile(save);
			if (save.Migrate(log: false) || Json.Stringify(save.ToGodotDictionary()) != once)
				warnings.Add($"Achievement migration ({label}): migrating twice changed the save again.");
			return save;
		}

		Sample("bought Gravewell", s => s.UnlockedSpellIds.Add("gravewell"), s =>
		{
			if (!OwnsSpell(s, "gravewell"))
				warnings.Add("Achievement migration (bought Gravewell): the bought spell is gone.");
			if (GlobalStatsManager.IsAchievementUnlocked(s, AchievementDefinitions.SecretDeedId("swallow_hole")) || s.HasFoundSecret("swallow_hole"))
				warnings.Add("Achievement migration (bought Gravewell): its vault reads as found; buying the spell found nothing.");
		});

		Sample("Survivor done", s =>
		{
			s.UnlockedAchievementIds.Add("survivor");
			s.UnlockedSpellIds.Add("frost_shard");
			s.Lifetime.BestTimeByStage["stage_0"] = 640f;
		}, s =>
		{
			if (!OwnsSpell(s, "frost_shard") || !OwnsSpell(s, "hollow_star"))
				warnings.Add($"Achievement migration (Survivor done): should own Frost Shard and Hollow Star; owns {string.Join(", ", s.UnlockedSpellIds)}.");
		});

		Sample("the Long Coil and the Warden beaten", s =>
		{
			s.UnlockedAchievementIds.AddRange(new[] { "sands_unwound", "emberdeep_ended" });
			s.UnlockedSpellIds.Add("fireball");
			s.UnlockedBoonIds.Add("sunsteel_filament");
			s.Lifetime.BossesDefeated.AddRange(new[] { "sands_coil", "emberdeep_warden" });
		}, s =>
		{
			if (!OwnsSpell(s, "fireball") || !GlobalStatsManager.IsBoonUnlocked(s, "sunsteel_filament"))
				warnings.Add("Achievement migration (the Long Coil and the Warden beaten): should own Fireball and Sunsteel Filament.");
		});

		Sample("the Long Coil beaten, not the Warden", s =>
		{
			s.UnlockedAchievementIds.Add("sands_unwound");
			s.UnlockedBoonIds.Add("sunsteel_filament");
			s.Lifetime.BossesDefeated.Add("sands_coil");
		}, s =>
		{
			if (!OwnsSpell(s, "fireball"))
				warnings.Add("Achievement migration (the Long Coil beaten): Unwound done should pay Fireball, which moved to it.");
		});

		Sample("met the old gate without the Long Coil", s =>
		{
			foreach (string id in UnlockCatalog.AllSpellIds.Where(id => !UnlockCatalog.IsStarter(id, UnlockKind.Spell)))
				s.UnlockedSpellIds.Add(id);
			foreach (string id in UnlockCatalog.AllWizardIds.Where(id => !UnlockCatalog.IsStarter(id, UnlockKind.Character)))
				s.UnlockedCharacterIds.Add(id);
		}, s =>
		{
			StageDefinition final = StageCatalog.GetById("stage_7");
			if (final != null && final.IsPlayable && !GlobalStatsManager.IsStageAvailable(s, final))
				warnings.Add("Achievement migration (met the old gate without the Long Coil): the save could enter the Emberdeep at 18 and cannot now.");
		});

		Sample("owns Bramble Seed", s => s.UnlockedSpellIds.Add("bramble_seed"), s =>
		{
			if (!s.Lifetime.HasWonEvent("the_grove") || !GlobalStatsManager.IsAchievementUnlocked(s, AchievementDefinitions.EventDeedId("the_grove")))
				warnings.Add("Achievement migration (owns Bramble Seed): the grove's deed should read as done.");
			if (s.Lifetime.EventsWon.Count != 1)
				warnings.Add($"Achievement migration (owns Bramble Seed): events won {string.Join(", ", s.Lifetime.EventsWon)}; only the grove was.");
		});

		Sample("a fresh save", _ => { }, s =>
		{
			if (s.UnlockedSpellIds.Count + s.UnlockedBoonIds.Count + s.UnlockedCharacterIds.Count + s.UnlockedStageIds.Count > 0
				|| s.UnlockedAchievementIds.Count > 0 || s.Lifetime.EventsWon.Count > 0 || s.GlimpsedSecretIds.Count > 0)
				warnings.Add("Achievement migration (a fresh save): it was granted or credited with something.");
		});

		// Through every step from before the campaign: the legacy grant, and no rescued wizard.
		var legacy = new SaveData { SchemaVersion = 7 };
		legacy.Migrate(log: false);
		if (GlobalStatsManager.IsCharacterUnlocked(legacy, "tituba") || GlobalStatsManager.IsCharacterUnlocked(legacy, "vainamoinen"))
			warnings.Add("Achievement migration: a pre-campaign save was handed a rescued wizard; schema 8 must read its frozen list, not the live catalog.");
		if (!OwnsSpell(legacy, "fireball") || !OwnsSpell(legacy, "spiritual_weapon"))
			warnings.Add("Achievement migration: a pre-campaign save lost the legacy grant of schema 8.");

		// From 9, with everything that existed then: credited the discoveries (10) and John Dee (12),
		// and so the schema 18 gate, and so the Emberdeep (19).
		var nine = new SaveData { SchemaVersion = 9 };
		string[] laterSpells = { "starfall", "familiar", "stormsweep", "sleet_runes", "iron_palisade", "contagion", "bramble_seed" };
		foreach (string id in UnlockCatalog.AllSpellIds.Where(id => !UnlockCatalog.IsStarter(id, UnlockKind.Spell)
			&& !laterSpells.Contains(id, StringComparer.OrdinalIgnoreCase)))
			nine.UnlockedSpellIds.Add(id);
		nine.Migrate(log: false);
		foreach (string id in new[] { "tituba", "vainamoinen", "john_dee" })
		{
			if (!GlobalStatsManager.IsCharacterUnlocked(nine, id))
				warnings.Add($"Achievement migration: a save that had everything at schema 9 was not credited '{id}' on the way up.");
		}
		StageDefinition emberdeep = StageCatalog.GetById("stage_7");
		if (emberdeep != null && emberdeep.IsPlayable && !GlobalStatsManager.IsStageAvailable(nine, emberdeep))
			warnings.Add("Achievement migration: a save that had everything at schema 9 cannot enter the Emberdeep after every step.");
	}

	// The recurring miniboss. Node2DGame loads it by path on a timer and returns quietly when the
	// load fails, so every way this breaks produces the same symptom: the miniboss never arrives,
	// which reads as a tuning decision rather than as a broken reference. Checked here for the
	// same reason the ranged enemies are - a dotnet build sees none of it.
	private const string MiniBossScenePath = "res://scenes/WardenEnemy.tscn";

	private static void ValidateMiniBoss(List<string> warnings)
	{
		if (!ResourceLoader.Exists(MiniBossScenePath))
		{
			warnings.Add($"Miniboss scene '{MiniBossScenePath}' is missing, so the recurring miniboss would never spawn.");
			return;
		}

		var scene = GD.Load<PackedScene>(MiniBossScenePath);
		if (scene == null)
		{
			warnings.Add($"Miniboss scene '{MiniBossScenePath}' failed to load, so the recurring miniboss would never spawn.");
			return;
		}

		Node probe = scene.Instantiate();
		if (probe is not Enemy miniBoss)
		{
			warnings.Add($"Miniboss scene '{MiniBossScenePath}' has no Enemy script attached.");
			probe?.Free();
			return;
		}

		// IsMiniBoss is what the spawn tick counts to keep one on screen at a time, and it is also
		// what the reward and telemetry paths read. Off, this is an ordinary enemy with 220 HP.
		if (!miniBoss.IsMiniBoss)
			warnings.Add($"Miniboss scene '{MiniBossScenePath}' does not have IsMiniBoss set, so it would spawn as an ordinary enemy and could stack.");

		var sprite = miniBoss.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (sprite?.SpriteFrames == null)
			warnings.Add($"Miniboss scene '{MiniBossScenePath}' has no SpriteFrames, so it would arrive invisible.");
		else if (!sprite.SpriteFrames.HasAnimation("moving"))
			warnings.Add($"Miniboss scene '{MiniBossScenePath}' has no 'moving' animation, so it would stand still while it chases.");

		miniBoss.Free();
	}

	// Scenes carrying a RangedEnemy script. There is no catalog for ordinary enemies - Node2DGame
	// holds them as fields - so this list is maintained by hand the same way SpellResourcePaths is.
	private static readonly string[] RangedEnemyScenePaths =
	{
		"res://scenes/HexerEnemy.tscn",
		"res://scenes/ForestWisp.tscn",
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

			ValidateRangedEnemy(warnings, path, ranged);
			ranged.Free();
		}
	}

	// One ranged enemy's tuning. Split out so ValidateBestiary runs the same checks on every caster and
	// turret a chapter's roster names, without each new scene having to be added to the list above.
	private static void ValidateRangedEnemy(List<string> warnings, string path, RangedEnemy ranged)
	{
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
		// The Still Warden guard is a SlammerEnemy with the telegraph shape flipped to a cone,
		// so it validates through exactly the same path and needs no case of its own.
		"res://scenes/RimeGuard.tscn",
		// The Sketchbook's ghost (Elim's Boo): a wail ring and a phase that marks where it re-forms.
		"res://scenes/BooEnemy.tscn",
		// The Blighted Swamp event's lantern-tender: strikes, spilt oil, the dark, the rekindle.
		BogLanternsEvent.TenderScenePath,
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
				case BooEnemy ghost:
					ValidateGhost(warnings, path, ghost);
					break;
				case LanternTenderEnemy tender:
					ValidateLanternTender(warnings, path, tender);
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

		// A cone is only worth having because it can be stepped out of sideways. At 90 degrees or
		// more it is a half-plane, there is no sideways left, and it is a worse ring.
		if (slammer.SlamShape == TelegraphShape.Cone
			&& (slammer.SlamHalfAngleDegrees <= 0f || slammer.SlamHalfAngleDegrees >= 90f))
		{
			warnings.Add($"Slammer '{path}' has a cone half-angle of {slammer.SlamHalfAngleDegrees} degrees; outside 0-90 it is either nothing or a half-plane the player cannot step out of.");
		}
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

	private static void ValidateGhost(List<string> warnings, string path, BooEnemy ghost)
	{
		if (ghost.WailWindUpSeconds <= 0f)
			warnings.Add($"Ghost '{path}' has no wail wind-up, so its ring lands with no warning.");
		if (ghost.PhaseTellSeconds <= 0f)
			warnings.Add($"Ghost '{path}' has no phase tell, so the ring it places where it re-forms lands unseen.");
		if (ghost.WailRadius <= 0f || ghost.WailDamage <= 0)
			warnings.Add($"Ghost '{path}' wails at radius {ghost.WailRadius} for {ghost.WailDamage}, so the wail is decorative.");
		if (ghost.PhaseRadius <= 0f || ghost.PhaseDamage <= 0)
			warnings.Add($"Ghost '{path}' phases at radius {ghost.PhaseRadius} for {ghost.PhaseDamage}, so its mark hits nothing.");
		if (ghost.WailEngageRadiusMultiplier < 1f)
			warnings.Add($"Ghost '{path}' engages at {ghost.WailEngageRadiusMultiplier}x its wail radius; below 1 the player is inside the ring before the tell begins.");
		// The same rule as the bosses' burrows: the mark must land before the ghost re-forms on it,
		// or its arrival is itself an untelegraphed hit and the ring a lie about when it is safe.
		if (ghost.PhaseTravelSeconds <= ghost.PhaseTellSeconds)
			warnings.Add($"Ghost '{path}' crosses in {ghost.PhaseTravelSeconds}s but its mark takes {ghost.PhaseTellSeconds}s; it would re-form before its own warning lands.");
		if (ghost.PhaseMinDistance >= ghost.PhaseMaxDistance)
			warnings.Add($"Ghost '{path}' can never phase: PhaseMinDistance {ghost.PhaseMinDistance} is not below PhaseMaxDistance {ghost.PhaseMaxDistance}.");
		if (!ghost.IsMiniBoss)
			warnings.Add($"Ghost '{path}' is not IsMiniBoss, so it would be recycled to the spawn ring mid-fight, shoved by the player and pay ordinary XP.");
	}

	// The player walks 220 px a second (Player.Speed) and loses some of it to every body he pushes, so
	// the dark is only fair if the walk into the light fits its wind-up at well under that.
	private const float TenderDarkWalkPxPerSecond = 170f;

	// The lantern-tender (LanternTenderEnemy, .ai/side-events.md): four attacks, each a promise made
	// before it lands, and every way one breaks is quiet - a zero flight is a strike out of nowhere, a
	// light edge the player cannot reach in time is a hit he was never given, a dark that can be cast on
	// someone already in the light is a tell for nothing, a missing strip is a fire that draws nothing.
	private static void ValidateLanternTender(List<string> warnings, string path, LanternTenderEnemy tender)
	{
		string who = $"Lantern-tender '{path}'";

		// No zero wind-ups: the throw, every strike's flight (its ring is the tell), the oil, the dark,
		// the rekindle.
		if (tender.StrikeWindUpSeconds <= 0f)
			warnings.Add($"{who} has no strike wind-up, so a volley leaves the lantern with no tell.");
		if (tender.StrikeFlightSeconds < 0.6f)
			warnings.Add($"{who} lands its strikes {tender.StrikeFlightSeconds}s after throwing; the ring on the ground is the whole warning and needs at least 0.6s to be walked out of.");
		if (tender.OilWindUpSeconds <= 0f)
			warnings.Add($"{who} has no oil wind-up, so the splash lands with no wedge shown.");
		if (tender.DarkWindUpSeconds <= 0f)
			warnings.Add($"{who} has no dark wind-up, so the dark falls with no light to run to.");
		if (tender.RekindleWindUpSeconds <= 0f)
			warnings.Add($"{who} has no rekindle wind-up, so its ward arrives with no thread to say where from.");

		if (tender.StrikeRadius <= 0f || tender.StrikeDamage <= 0 || tender.StrikeCount < 1)
			warnings.Add($"{who} throws {tender.StrikeCount} strikes of radius {tender.StrikeRadius} for {tender.StrikeDamage}, so its fire strikes are decorative.");
		if (tender.StrikeCountWounded < tender.StrikeCount)
			warnings.Add($"{who} throws fewer strikes wounded ({tender.StrikeCountWounded}) than whole ({tender.StrikeCount}).");
		if (tender.OilReach <= 0f || tender.OilDamage <= 0)
			warnings.Add($"{who} spills oil {tender.OilReach} px for {tender.OilDamage}, so the splash hits nothing.");
		if (tender.OilBurnSeconds <= 0f || tender.OilBurnTickSeconds <= 0f || tender.OilBurnDamage <= 0)
			warnings.Add($"{who} leaves oil burning {tender.OilBurnSeconds}s, {tender.OilBurnDamage} every {tender.OilBurnTickSeconds}s, so nothing is left alight.");
		// The slammer's cone rule: at 90 degrees or more a wedge is a half-plane with no side to step to.
		if (tender.OilHalfAngleDegrees <= 0f || tender.OilHalfAngleDegrees >= 90f)
			warnings.Add($"{who} spills oil in a {tender.OilHalfAngleDegrees} degree half-angle; outside 0-90 there is no sideways out of it.");
		// Aimed at someone further than it reaches, the splash could never touch him.
		if (tender.OilEngageDistance <= 0f || tender.OilEngageDistance > tender.OilReach)
			warnings.Add($"{who} swings its oil at {tender.OilEngageDistance} px but it only reaches {tender.OilReach}, so it would mark a wedge that cannot hit.");
		if (tender.DarkDamage <= 0 || tender.DarkLightRadius <= 0f)
			warnings.Add($"{who} lets the dark in for {tender.DarkDamage} outside {tender.DarkLightRadius} px, so the dark is decorative.");
		// Only ever at someone outside the light, and never at someone too far to reach it in the tell.
		if (tender.DarkMinDistance <= tender.DarkLightRadius)
			warnings.Add($"{who} can lift the dark at {tender.DarkMinDistance} px, inside its own light ({tender.DarkLightRadius}), where it asks nothing.");
		if (tender.DarkMinDistance >= tender.DarkMaxDistance)
			warnings.Add($"{who} can never lift the dark: DarkMinDistance {tender.DarkMinDistance} is not below DarkMaxDistance {tender.DarkMaxDistance}.");
		float walk = (tender.DarkMaxDistance - tender.DarkLightRadius) / Mathf.Max(0.01f, tender.DarkWindUpSeconds);
		if (walk > TenderDarkWalkPxPerSecond)
			warnings.Add($"{who} lifts the dark at up to {tender.DarkMaxDistance} px with {tender.DarkWindUpSeconds}s to reach its light at {tender.DarkLightRadius}: {walk:0} px/s, over the {TenderDarkWalkPxPerSecond} a player in a crowd can be asked for.");
		if (tender.WardDamageTaken <= 0f || tender.WardDamageTaken >= 1f)
			warnings.Add($"{who} takes {tender.WardDamageTaken} of each hit while warded; at 0 it is unkillable until snuffed, at 1 the ward does nothing.");
		if (tender.RekindleHealthFraction <= 0f || tender.RekindleHealthFraction >= 1f)
			warnings.Add($"{who} rekindles at {tender.RekindleHealthFraction} health; it must be strictly between 0 and 1.");
		if (!tender.IsMiniBoss)
			warnings.Add($"{who} is not IsMiniBoss, so it would be recycled to the spawn ring mid-fight, shoved by the player and pay ordinary XP.");

		var sprite = tender.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		SpriteFrames frames = sprite?.SpriteFrames;
		if (frames == null)
			warnings.Add($"{who} has no SpriteFrames, so it would arrive invisible (tools/art/swamp_tender.py).");
		else
		{
			foreach (string anim in new[] { "moving", "attack", "death" })
				if (!frames.HasAnimation(anim) || frames.GetFrameCount(anim) == 0)
					warnings.Add($"{who} has no '{anim}' animation (tools/art/swamp_tender.py).");
			if (frames.HasAnimation("death") && frames.GetAnimationLoop("death"))
				warnings.Add($"{who} has a looping death, so its body would never free.");
		}

		// The fire is drawn from strips nothing else loads; a missing one draws nothing and says nothing.
		foreach (string strip in new[] { LanternFireStrike.StrikeTexturePath, LanternFireStrike.EmberTexturePath, BurningOil.TexturePath })
			if (!ResourceLoader.Exists(strip))
				warnings.Add($"{who}: '{strip}' is missing or not imported, so that part of its fire draws nothing (tools/art/swamp_tender.py, then import).");
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

	// THE BESTIARY (.ai/bestiary.md). Every chapter's roster (StageBestiary.<Chapter>.cs) names a scene
	// for every job its spawner has, and a dotnet build sees none of those scenes. Each way one breaks
	// is quiet: a missing body is a band of the table that never spawns, a caster wearing a chaser's
	// script never shoots, a sheet with a looping death leaves a corpse that never frees, and a new
	// creature copied from the wrong scene retunes a chapter nobody meant to retune.
	//
	// The last warning per chapter is not a fault: it is the Phase 2 to-do list. It names every slot
	// still filled by a stand-in or by another chapter's creature, and it shortens by itself as each
	// creature's scene is created at the path its roster already gives it.
	private static void ValidateBestiary(List<string> warnings)
	{
		var byId = new Dictionary<string, BestiaryCreature>(StringComparer.OrdinalIgnoreCase);
		var byPath = new Dictionary<string, BestiaryCreature>(StringComparer.OrdinalIgnoreCase);
		var homeByType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (BestiaryCreature creature in StageBestiary.AllCreatures())
		{
			if (!byId.TryAdd(creature.Id, creature))
				warnings.Add($"Bestiary: creature id '{creature.Id}' is declared twice ({byId[creature.Id].DisplayName}, {creature.DisplayName}).");
			// One scene, two creatures: two homes, and the ownership check below cannot say whose it is.
			if (!byPath.TryAdd(creature.ScenePath, creature))
				warnings.Add($"Bestiary: {creature} and {byPath[creature.ScenePath]} are both declared at '{creature.ScenePath}'.");
			if (StageCatalog.GetById(creature.HomeStageId) == null)
				warnings.Add($"Bestiary: {creature} belongs to '{creature.HomeStageId}', which is not a chapter.");
			// Enemy.DropXp pays these four names as a tank (5) or a runner (3) instead of 2. No shipped
			// scene uses them, so a new creature given one would be the only enemy in the game paying it.
			if (BestiaryXpTierTypes.Contains(creature.EnemyType))
				warnings.Add($"Bestiary: {creature} has EnemyType '{creature.EnemyType}', which Enemy.DropXp pays as an XP tier of its own; give it a name of its own.");
			// The lifetime kill tally is keyed by type, so two chapters' creatures sharing one would count as one.
			if (homeByType.TryGetValue(creature.EnemyType, out string? home) && !home.Equals(creature.HomeStageId, StringComparison.OrdinalIgnoreCase))
				warnings.Add($"Bestiary: EnemyType '{creature.EnemyType}' is used in two chapters ({home}, {creature.HomeStageId}); kills of both would be tallied as one creature.");
			else
				homeByType[creature.EnemyType] = creature.HomeStageId;
		}

		foreach (StageDefinition stage in StageCatalog.All)
		{
			if (stage.IsPlayable && StageBestiary.ForStageId(stage.Id) == null)
				warnings.Add($"Bestiary: {stage.DisplayName} ({stage.Id}) is playable and has no roster in StageBestiary.All, so it would spawn the Cursed Dungeon's cast.");
		}

		// Each scene is built once and freed at the end, however many slots and chapters name it.
		var probes = new Dictionary<string, Enemy?>(StringComparer.OrdinalIgnoreCase);
		var behaviourChecked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			foreach (ChapterBestiary chapter in StageBestiary.All)
				ValidateChapterBestiary(warnings, chapter, probes, behaviourChecked);
		}
		finally
		{
			foreach (Enemy? probe in probes.Values)
				probe?.Free();
		}
	}

	private static readonly HashSet<string> BestiaryXpTierTypes = new(StringComparer.OrdinalIgnoreCase)
	{
		"Tank", "TankEnemy", "Fast", "FastEnemy",
	};

	private static void ValidateChapterBestiary(List<string> warnings, ChapterBestiary chapter,
		Dictionary<string, Enemy?> probes, HashSet<string> behaviourChecked)
	{
		string name = chapter.ChapterName;
		StageDefinition? stage = StageCatalog.GetById(chapter.StageId);
		if (stage == null)
		{
			warnings.Add($"Bestiary: a roster names '{chapter.StageId}', which is not a chapter.");
			return;
		}

		foreach (BestiaryRole role in StageBestiary.RequiredRoles)
		{
			int count = chapter.Slots.Count(s => s.Role == role);
			if (count == 0)
				warnings.Add($"Bestiary: {name} has no {role} slot; give it a creature, or an Absent slot saying why it has none.");
			else if (count > 1)
				warnings.Add($"Bestiary: {name} has {count} {role} slots and the spawner only ever reads the first.");
		}

		var todo = new List<string>();
		foreach (BestiarySlot slot in chapter.Slots)
		{
			if (slot.IsAbsent)
			{
				if (string.IsNullOrWhiteSpace(slot.AbsentReason))
					warnings.Add($"Bestiary: {name} has no {slot.Role} and does not say why.");
				// The table and the clock always need a body; only a rolled role can be skipped.
				if (slot.Role is BestiaryRole.Bulk or BestiaryRole.MiniBoss or BestiaryRole.Extra)
					warnings.Add($"Bestiary: {name}'s {slot.Role} cannot be absent - the table and the miniboss clock always need a body.");
				continue;
			}

			BestiaryCreature creature = slot.Creature!;
			if (!creature.IsDrawn)
			{
				if (slot.StandIn == null)
				{
					warnings.Add($"Bestiary: {name}'s {slot.Role} is {creature}, but there is no scene at '{creature.ScenePath}' and no stand-in, so the slot spawns nothing.");
					continue;
				}
				todo.Add($"{slot.Role} {creature.DisplayName} ({ShortScenePath(creature.ScenePath)})");
			}
			else if (!IsHome(creature, chapter))
			{
				todo.Add($"{slot.Role} is {creature.DisplayName}, borrowed from {HomeName(creature)}");
			}

			BestiaryCreature resolved = slot.Resolved!;
			Enemy? probe = ProbeBestiaryScene(warnings, resolved, probes, $"{name}'s {slot.Role}");
			if (probe == null)
				continue;

			ValidateBestiaryRole(warnings, chapter, slot.Role, resolved, probe);
			ValidateBestiaryArt(warnings, resolved, probe);
			ValidateBestiaryBehaviour(warnings, resolved.ScenePath, probe, behaviourChecked);

			// A drawn creature is a new look at the numbers its slot was tuned on, unless its roster
			// says otherwise and why.
			if (creature.IsDrawn && slot.StandIn != null && string.IsNullOrWhiteSpace(slot.StatsChange))
			{
				Enemy? template = ProbeBestiaryScene(warnings, slot.StandIn, probes, $"{name}'s {slot.Role} stand-in");
				if (template != null)
					ValidateBestiaryParity(warnings, creature, probe, slot.StandIn, template);
			}

			// A chapter's own summoner calls up the chapter's own. (A stand-in summoner is already on the list.)
			if (probe is SummonerEnemy summoner && IsHome(resolved, chapter))
			{
				BestiaryCreature? minion = StageBestiary.CreatureAt(summoner.MinionScenePath);
				if (minion == null)
					warnings.Add($"Bestiary: {resolved} calls up '{summoner.MinionScenePath}', which no chapter's roster names.");
				else if (!IsHome(minion, chapter))
					todo.Add($"{resolved.DisplayName} calls up {minion.DisplayName}, from {HomeName(minion)}");
			}
		}

		ValidateBestiaryTable(warnings, chapter, chapter.Table, "table");
		if (chapter.LateTable.Count > 0)
		{
			ValidateBestiaryTable(warnings, chapter, chapter.LateTable, "late table");
			if (chapter.LateTableFromPaceMinutes <= 0f)
				warnings.Add($"Bestiary: {name} has a late table from pace minute {chapter.LateTableFromPaceMinutes}, so its opening table is never rolled.");
		}

		foreach (BestiaryCreature attendant in chapter.Attendants)
		{
			if (!IsHome(attendant, chapter))
				warnings.Add($"Bestiary: {name} lists {attendant} as an attendant, but it belongs to {HomeName(attendant)}.");
			Enemy? probe = ProbeBestiaryScene(warnings, attendant, probes, $"{name}'s attendant");
			if (probe != null)
				ValidateBestiaryArt(warnings, attendant, probe);
		}

		// What the chapter's boss calls up in the fight is part of the chapter's cast too.
		BossDefinition? boss = BossCatalog.ForStageIndex(stage.Index);
		foreach (string addPath in BossAddScenePaths(boss))
		{
			BestiaryCreature? add = StageBestiary.CreatureAt(addPath);
			if (add == null)
				warnings.Add($"Bestiary: {boss!.DisplayName} calls up '{addPath}', which no roster names; list it as one of {name}'s attendants.");
			else if (!IsHome(add, chapter))
				todo.Add($"{boss!.DisplayName} calls up {add.DisplayName}, from {HomeName(add)}");
		}

		if (todo.Count > 0)
			warnings.Add($"Bestiary to-do (Phase 2, .ai/bestiary.md): {name} still spawns {todo.Count} creatures that are not its own - " + string.Join("; ", todo) + ".");
	}

	private static bool IsHome(BestiaryCreature creature, ChapterBestiary chapter) =>
		creature.HomeStageId.Equals(chapter.StageId, StringComparison.OrdinalIgnoreCase);

	private static string HomeName(BestiaryCreature creature) =>
		StageCatalog.GetById(creature.HomeStageId)?.DisplayName ?? creature.HomeStageId;

	private static string ShortScenePath(string path) => path.StartsWith("res://") ? path.Substring(6) : path;

	private static Enemy? ProbeBestiaryScene(List<string> warnings, BestiaryCreature creature,
		Dictionary<string, Enemy?> probes, string context)
	{
		if (probes.TryGetValue(creature.ScenePath, out Enemy? cached))
			return cached;

		Enemy? enemy = null;
		if (!ResourceLoader.Exists(creature.ScenePath))
		{
			warnings.Add($"Bestiary: {context} is {creature}, whose scene '{creature.ScenePath}' does not exist.");
		}
		else
		{
			Node? node = GD.Load<PackedScene>(creature.ScenePath)?.Instantiate();
			if (node is Enemy built)
				enemy = built;
			else
			{
				warnings.Add($"Bestiary: {context} is {creature}, but '{creature.ScenePath}' has no Enemy script on its root, so it cannot spawn.");
				node?.Free();
			}
		}
		probes[creature.ScenePath] = enemy;
		return enemy;
	}

	// The behaviour class a role needs. The spawner hands every role the same health curve and
	// elite treatment, so a role filled by the wrong class still spawns - it just does the wrong thing.
	private static void ValidateBestiaryRole(List<string> warnings, ChapterBestiary chapter, BestiaryRole role,
		BestiaryCreature creature, Enemy probe)
	{
		string name = chapter.ChapterName;
		if (probe.EnemyType != creature.EnemyType)
			warnings.Add($"Bestiary: {creature} is declared as EnemyType '{creature.EnemyType}' but its scene says '{probe.EnemyType}'; the miniboss clock and the kill tally read the declared one.");

		string? wrong = role switch
		{
			BestiaryRole.Ranged when probe is not RangedEnemy { Rooted: false } => "a RangedEnemy that is not Rooted",
			BestiaryRole.Turret when probe is not RangedEnemy { Rooted: true } => "a RangedEnemy with Rooted set",
			BestiaryRole.Charger when probe is not LungerEnemy => "a LungerEnemy",
			BestiaryRole.Exploder when probe is not ExploderEnemy => "an ExploderEnemy",
			BestiaryRole.Slammer when probe is not SlammerEnemy => "a SlammerEnemy",
			BestiaryRole.Summoner when probe is not SummonerEnemy => "a SummonerEnemy",
			BestiaryRole.MiniBoss when !probe.IsMiniBoss || probe is BossEnemy => "an Enemy with IsMiniBoss set (and not a boss)",
			_ when probe is BossEnemy => "an ordinary enemy, not a boss",
			_ => null,
		};
		if (wrong != null)
			warnings.Add($"Bestiary: {name}'s {role} is {creature}, which is a {probe.GetType().Name}; the role needs {wrong}.");

		// A table body flagged IsMiniBoss would block the player and pay the miniboss's 50 XP on every
		// spawn. Node2DGame.SpawnEnemy clears the flag on everything the table rolls, so this is the
		// scene's own mistake caught early, and the side events' fallback to the table (which keeps
		// the scene's flag) is still covered. The kids' Rockem is both the Sketchbook's heavy and its
		// miniboss, as drawn, so its scene carries the flag for the miniboss and is exempt here.
		bool body = role is BestiaryRole.Bulk or BestiaryRole.Fast or BestiaryRole.Heavy or BestiaryRole.Wall or BestiaryRole.Extra;
		if (body && probe.IsMiniBoss && !IsArtContractExempt(creature))
			warnings.Add($"Bestiary: {name}'s {role} is {creature}, which has IsMiniBoss set, so every one the table rolls blocks the player and pays 50 XP.");
	}

	private static bool IsArtContractExempt(BestiaryCreature creature) =>
		StageBestiary.ForStageId(creature.HomeStageId)?.ArtContractExempt == true;

	// The animations a behaviour plays, held to .ai/art-direction.md section 5. Every one of these
	// falls back silently in a run (Enemy.StartDeath frees a sheet with no death, PlayAttackAnimation
	// skips a sheet with no attack), which is right for the kids' drawings and wrong for anything drawn
	// to the contract.
	private static void ValidateBestiaryArt(List<string> warnings, BestiaryCreature creature, Enemy probe)
	{
		var sprite = probe.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		SpriteFrames? frames = sprite?.SpriteFrames;
		if (frames == null)
		{
			warnings.Add($"Bestiary: {creature} has no AnimatedSprite2D with SpriteFrames, so it would spawn invisible.");
			return;
		}

		bool exempt = IsArtContractExempt(creature);
		if (!frames.HasAnimation("moving"))
			warnings.Add($"Bestiary: {creature} has no 'moving' animation, so it stands still while it chases.");
		else
		{
			if (!frames.GetAnimationLoop("moving"))
				warnings.Add($"Bestiary: {creature}'s 'moving' does not loop, so it walks one cycle and freezes.");
			// A PNG Godot has not imported loads as nothing and draws nothing, with no error at all.
			Texture2D? first = frames.GetFrameCount("moving") > 0 ? frames.GetFrameTexture("moving", 0) : null;
			if (first == null || (first is AtlasTexture atlas && atlas.Atlas == null))
				warnings.Add($"Bestiary: {creature}'s 'moving' has no texture - is its PNG imported? It would spawn invisible.");
		}

		if (frames.HasAnimation("death"))
		{
			if (frames.GetAnimationLoop("death"))
				warnings.Add($"Bestiary: {creature}'s 'death' loops, so it never finishes and the corpse never frees.");
		}
		else if (!exempt)
			warnings.Add($"Bestiary: {creature} has no 'death' animation, so it vanishes on its last hit; the art contract makes one mandatory.");

		bool telegraphs = probe is RangedEnemy or LungerEnemy or SlammerEnemy or ExploderEnemy or SummonerEnemy or BooEnemy;
		if (frames.HasAnimation("attack"))
		{
			if (frames.GetAnimationLoop("attack"))
				warnings.Add($"Bestiary: {creature}'s 'attack' loops, so its wind-up pose never hands back to 'moving'.");
		}
		else if (telegraphs && !exempt)
			warnings.Add($"Bestiary: {creature} telegraphs but has no 'attack' animation, so its body does nothing while it winds up.");

		if (frames.HasAnimation("hurt") && frames.GetAnimationLoop("hurt"))
			warnings.Add($"Bestiary: {creature}'s 'hurt' loops, so the first hit leaves it flinching for good.");
	}

	// The timing checks every behaviour class already has, run on any roster scene the hand-kept
	// lists above do not name - so a chapter's new caster or slammer is held to them without anyone
	// editing those lists.
	private static void ValidateBestiaryBehaviour(List<string> warnings, string path, Enemy probe, HashSet<string> done)
	{
		if (!done.Add(path))
			return;
		if (probe is RangedEnemy ranged)
		{
			if (!RangedEnemyScenePaths.Contains(path, StringComparer.OrdinalIgnoreCase))
				ValidateRangedEnemy(warnings, path, ranged);
			return;
		}
		if (AttackPatternEnemyScenePaths.Contains(path, StringComparer.OrdinalIgnoreCase))
			return;
		switch (probe)
		{
			case LungerEnemy lunger: ValidateLunger(warnings, path, lunger); break;
			case SlammerEnemy slammer: ValidateSlammer(warnings, path, slammer); break;
			case ExploderEnemy exploder: ValidateExploder(warnings, path, exploder); break;
			case SummonerEnemy summoner: ValidateSummoner(warnings, path, summoner); break;
			case BooEnemy ghost: ValidateGhost(warnings, path, ghost); break;
		}
	}

	private static void ValidateBestiaryTable(List<string> warnings, ChapterBestiary chapter,
		IReadOnlyList<BestiaryTableEntry> table, string which)
	{
		string name = chapter.ChapterName;
		if (table.Count == 0)
		{
			warnings.Add($"Bestiary: {name} has an empty {which}, so nothing ordinary ever spawns.");
			return;
		}

		double total = 0.0;
		foreach (BestiaryTableEntry band in table)
		{
			total += band.Share;
			if (band.Share <= 0f)
				warnings.Add($"Bestiary: {name}'s {which} has a band with share {band.Share}, which never rolls.");
			if (band.HealthMultiplier <= 0f)
				warnings.Add($"Bestiary: {name}'s {which} has a band at health x{band.HealthMultiplier}, which spawns dead.");
			if (!chapter.Slots.Contains(band.Slot))
				warnings.Add($"Bestiary: {name}'s {which} rolls a slot that is not in its roster ({band.Slot.Role}).");
			else if (band.Slot.IsAbsent)
				warnings.Add($"Bestiary: {name}'s {which} rolls its {band.Slot.Role}, which it says it does not have.");
			else if (band.Slot.Role is not (BestiaryRole.Bulk or BestiaryRole.Fast or BestiaryRole.Heavy or BestiaryRole.Wall or BestiaryRole.Extra))
				warnings.Add($"Bestiary: {name}'s {which} rolls its {band.Slot.Role}, which already has a roll of its own ahead of the table.");
		}
		// The last band takes what is left over, so a short table silently over-weights it and a long
		// one silently never reaches its last bands.
		if (Math.Abs(total - 1.0) > 0.002)
			warnings.Add($"Bestiary: {name}'s {which} shares add up to {total:0.###}, not 1.");
	}

	// The balance-bearing numbers of each behaviour class. A Phase 2 creature copies its stand-in's
	// root properties and changes its art, its EnemyType and its colours - not these.
	private static readonly string[] EnemyParityProperties =
	{
		"Speed", "Health", "ContactDamage", "KnockbackResistance", "MinSlowMultiplier", "IsMiniBoss",
	};

	private static readonly Dictionary<Type, string[]> BehaviourParityProperties = new()
	{
		[typeof(RangedEnemy)] = new[]
		{
			"StandoffDistance", "RetreatDistance", "AttackRange", "Rooted", "CastIntervalSeconds", "CastWindUpSeconds",
			"ProjectileDamage", "ProjectileSpeed", "BoltsPerVolley", "VolleySpreadDegrees", "BoltLeadFraction",
		},
		[typeof(LungerEnemy)] = new[]
		{
			"LungeIntervalSeconds", "LungeWindUpSeconds", "LungeDurationSeconds", "LungeSpeedMultiplier", "RecoverySeconds",
			"RecoverySpeedMultiplier", "LungeRange", "MinimumLungeDistance", "RepositionSpeedMultiplier", "RepositionExitMultiplier",
		},
		[typeof(SlammerEnemy)] = new[]
		{
			"SlamIntervalSeconds", "SlamTelegraphSeconds", "SlamRadius", "SlamDamage", "SlamEngageRadiusMultiplier",
			"SlamShape", "SlamHalfAngleDegrees",
		},
		[typeof(ExploderEnemy)] = new[]
		{
			"FuseSeconds", "BlastRadius", "BlastDamage", "TriggerRadiusMultiplier", "BlastLingerSeconds",
		},
		[typeof(SummonerEnemy)] = new[]
		{
			"SummonIntervalSeconds", "SummonWindUpSeconds", "MinionsPerSummon", "MaxActiveMinions", "MaxTotalSummons",
			"MinionHealthFraction", "SummonSpawnRadius", "StandoffDistance", "RetreatDistance",
		},
	};

	private static void ValidateBestiaryParity(List<string> warnings, BestiaryCreature creature, Enemy probe,
		BestiaryCreature standIn, Enemy template)
	{
		if (probe.GetType() != template.GetType())
		{
			warnings.Add($"Bestiary: {creature} is a {probe.GetType().Name} but the slot it fills was tuned on {standIn}, a {template.GetType().Name}.");
			return;
		}

		var differences = new List<string>();
		IEnumerable<string> properties = EnemyParityProperties;
		if (BehaviourParityProperties.TryGetValue(probe.GetType(), out string[]? behaviour))
			properties = properties.Concat(behaviour);
		foreach (string property in properties)
		{
			var info = probe.GetType().GetProperty(property);
			if (info == null)
				continue;
			object? mine = info.GetValue(probe);
			object? theirs = info.GetValue(template);
			bool same = mine is float a && theirs is float b ? Math.Abs(a - b) < 0.0001f : Equals(mine, theirs);
			if (!same)
				differences.Add($"{property} {mine} vs {theirs}");
		}

		// The body's size: within a third either way, so a new silhouette can have the hitbox it is
		// drawn with without turning a chaser into a wall or a needle.
		float myExtent = BodyExtent(probe), theirExtent = BodyExtent(template);
		if (theirExtent > 0f && (myExtent < theirExtent * 0.7f || myExtent > theirExtent * 1.3f))
			differences.Add($"body size {myExtent:0.#} vs {theirExtent:0.#}");

		// Every basic body sits at one scale; a different one is a body a different size on screen.
		Vector2? myScale = probe.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D")?.Scale;
		Vector2? theirScale = template.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D")?.Scale;
		if (myScale.HasValue && theirScale.HasValue && !myScale.Value.IsEqualApprox(theirScale.Value))
			differences.Add($"sprite scale {myScale.Value} vs {theirScale.Value}");

		if (differences.Count > 0)
			warnings.Add($"Bestiary: {creature} differs from {standIn}, the stat block its slot was tuned on ({string.Join(", ", differences)}). Match it, or give its Planned slot a statsChange saying why.");
	}

	private static float BodyExtent(Node node) =>
		node.GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.Shape switch
		{
			CircleShape2D circle => circle.Radius,
			RectangleShape2D rect => Mathf.Max(rect.Size.X, rect.Size.Y) * 0.5f,
			CapsuleShape2D capsule => Mathf.Max(capsule.Radius, capsule.Height * 0.5f),
			_ => 0f,
		};

	// The enemy scenes a chapter's boss calls up during its fight. Two bosses do: Mother Rot sheds
	// her spawn and the Still Warden calls its guards. (The Hollow Choir calls up copies of itself.)
	private static IEnumerable<string> BossAddScenePaths(BossDefinition? boss)
	{
		// A boss on its stand-in calls up the stand-in's adds; that is what the run does.
		if (boss == null || string.IsNullOrWhiteSpace(boss.SpawnScenePath) || !ResourceLoader.Exists(boss.SpawnScenePath))
			yield break;
		Node? probe = GD.Load<PackedScene>(boss.SpawnScenePath)?.Instantiate();
		string? add = probe switch
		{
			MotherRotBoss rot => rot.SpawnScenePath,
			StillWardenBoss warden => warden.GuardScenePath,
			_ => null,
		};
		probe?.Free();
		if (!string.IsNullOrWhiteSpace(add))
			yield return add!;
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
			// A boon counts, and so does currency. The check is "does beating this pay out",
			// not "does it pay out a spell": two chapter clears pay in a boon because the shop
			// and the achievement track draw on one finite pool, and the Sketchbook pays in
			// Arcane Energy because it is a bonus chapter and must not hold campaign content.
			if (!string.IsNullOrWhiteSpace(boss.Id)
				&& !AchievementDefinitions.All.Any(a =>
					(!string.IsNullOrWhiteSpace(a.SpellUnlockId)
						|| !string.IsNullOrWhiteSpace(a.BoonUnlockId)
						|| !string.IsNullOrWhiteSpace(a.CharacterUnlockId)
						|| a.CurrencyReward > 0)
					&& a.IsComplete(BuildVictoryProbe(boss.Id))))
			{
				warnings.Add($"Boss '{boss.Id}' matches no achievement, so defeating it pays nothing at all.");
			}

			// What a victory opens is the campaign order's to say now (StageCatalog.ChapterOpenedBy),
			// and ValidateCampaign checks that. A boss still fighting on a stand-in is on the Phase 2
			// to-do list there too, not here: the stand-in scene is checked as the scene that spawns.
		}
	}

	// The tuning a boss fight is actually made of, none of which a dotnet build can see: every
	// number below lives in a .tscn and only resolves in Godot.
	//
	// The failures these catch all look like nothing while they are happening. A wind-up of zero
	// is an attack that simply arrives, and the boss still walks around looking perfectly fine. A
	// charge tell of 1.0 is a boss that plants for its whole wind-up and then never moves, which
	// reads as a boss that has stopped working. A burrow shorter than the tell it places means
	// the boss surfaces before its own warning resolves and the warning lands behind it.
	private static void ValidateBossPatterns(List<string> warnings)
	{
		foreach (BossDefinition boss in BossCatalog.All)
		{
			// A boss on its stand-in is that other boss, already checked under its own entry.
			if (boss.IsOnStandIn || string.IsNullOrWhiteSpace(boss.ScenePath) || !ResourceLoader.Exists(boss.ScenePath))
				continue;

			var scene = GD.Load<PackedScene>(boss.ScenePath);
			if (scene?.Instantiate() is not BossEnemy probe)
				continue;

			// THE ONE RULE EVERY BOSS SHARES. Everything below it is per-fight.
			if (probe.SlamTelegraphSeconds <= 0f)
				warnings.Add($"Boss '{boss.Id}' has no telegraph, so its attack lands with no warning at all.");

			if (probe.SlamDamage <= 0)
				warnings.Add($"Boss '{boss.Id}' deals {probe.SlamDamage} attack damage, so its whole telegraphed attack is decorative.");

			if (probe.SlamRadius <= 0f)
				warnings.Add($"Boss '{boss.Id}' has a reach of {probe.SlamRadius}, so it would draw a warning and hit nothing.");

			if (probe.EnrageHealthFraction <= 0f || probe.EnrageHealthFraction >= 1f)
				warnings.Add($"Boss '{boss.Id}' has EnrageHealthFraction {probe.EnrageHealthFraction}; outside 0-1 the fight either never escalates or opens enraged.");

			// THE FALL (BossDeath). Long enough to be seen, short enough that the arena is not held
			// for a corpse: about the length of boss_death (2.4 s), which plays under it.
			if (probe.DeathSeconds < 1.2f || probe.DeathSeconds + probe.DeathDelaySeconds > 3.5f)
				warnings.Add($"Boss '{boss.Id}' falls for {probe.DeathSeconds}s after {probe.DeathDelaySeconds}s; outside 1.2-3.5 s it either vanishes or holds the arena for a corpse.");

			ValidateBossPattern(warnings, probe);
			probe.Free();
		}

		// What every fall throws comes from one generated sheet. Missing, or short of the cells the
		// falls name, it throws nothing and says nothing (BossDeath draws no debris without it).
		Texture2D debris = ResourceLoader.Exists(BossDeath.DebrisAtlasPath) ? GD.Load<Texture2D>(BossDeath.DebrisAtlasPath) : null;
		int lastCell = BossDeath.Cell.Hide;
		if (debris == null)
			warnings.Add($"The boss death debris atlas '{BossDeath.DebrisAtlasPath}' is missing: re-run tools/art/boss_deaths.py and import.");
		else if (debris.GetHeight() < (lastCell / 8 + 1) * BossDeath.DebrisCell || debris.GetWidth() < 8 * BossDeath.DebrisCell)
			warnings.Add($"The boss death debris atlas is {debris.GetWidth()}x{debris.GetHeight()}, short of cell {lastCell} that BossDeath.Cell names.");
	}

	// Split out only because the switch is long. Everything in it is one boss idea plus the
	// number that would silently switch that idea off.
	private static void ValidateBossPattern(List<string> warnings, BossEnemy probe)
	{
		switch (probe)
		{
			// The Ninth Stone holds its own geometry to the player's walk (the gap walkable, the rest
			// of the ring closed, time to walk out); NinthStoneBoss.TuningProblems says what broke.
			case NinthStoneBoss stone:
				warnings.AddRange(stone.TuningProblems());
				break;

			case GaolerBoss gaoler:
				// Below 0 there is no planted tell before the charge; at 1 the charge never
				// happens, because the wind-up ends before it starts moving.
				if (gaoler.ChargeTellFraction <= 0f || gaoler.ChargeTellFraction >= 1f)
					warnings.Add($"Gaoler has ChargeTellFraction {gaoler.ChargeTellFraction}; at 0 it charges with no tell and at 1 it never charges at all.");
				if (gaoler.ChargeSpeedMultiplier <= 1f)
					warnings.Add($"Gaoler charges at {gaoler.ChargeSpeedMultiplier}x its walk, so the lane it draws promises a reach it never covers.");
				if (gaoler.DismountHealthFraction <= probe.EnrageHealthFraction)
					warnings.Add($"Gaoler dismounts at {gaoler.DismountHealthFraction} and enrages at {probe.EnrageHealthFraction}; the dismount has to come first or phase two is skipped.");
				break;

			case HollowChoirBoss choir:
				if (choir.VoiceCount < 2)
					warnings.Add($"Hollow Choir has {choir.VoiceCount} voices, so nothing can revive anything and the linked-health fight is an ordinary boss.");
				if (choir.ReviveSeconds <= 0f)
					warnings.Add("Hollow Choir revives instantly, so the window to drop all three at once does not exist.");
				if (choir.ReviveHealthFraction <= 0f || choir.ReviveHealthFraction >= 1f)
					warnings.Add($"Hollow Choir revives at {choir.ReviveHealthFraction} health; at or above 1 the fight cannot end.");
				break;

			case MotherRotBoss rot:
				if (rot.RegenPerSecond <= 0f)
					warnings.Add("Mother Rot does not regenerate, so killing her spawn achieves nothing and the whole decision the fight poses is gone.");
				if (!ResourceLoader.Exists(rot.SpawnScenePath))
					warnings.Add($"Mother Rot spawn scene '{rot.SpawnScenePath}' is missing, so she would never split.");
				if (rot.MaxLiveSpawn < rot.SpawnPerSplit)
					warnings.Add($"Mother Rot caps live spawn at {rot.MaxLiveSpawn} but sheds {rot.SpawnPerSplit} at a time, so a split can silently produce nothing.");
				break;

			case ArchivistBoss archivist:
				if (archivist.SweepDegreesPerSecond <= 0f)
					warnings.Add("The Archivist beam does not turn, so a stationary boss threatens exactly one spot on the floor.");
				if (archivist.SweepHalfAngle <= 0f || archivist.SweepHalfAngle >= 90f)
					warnings.Add($"The Archivist beam half-angle is {archivist.SweepHalfAngle} degrees; outside 0-90 it is either nothing or a half-plane.");
				if (archivist.ResurfaceTellSeconds >= archivist.BurrowTravelSeconds)
					warnings.Add($"The Archivist surfaces in {archivist.BurrowTravelSeconds}s but marks the ground for {archivist.ResurfaceTellSeconds}s, so it arrives before its own warning resolves.");
				// It is always winding up - that is how the beam stays on screen - so an interval
				// at or above the wind-up turns a continuous sweep into a strobe.
				if (probe.SlamIntervalSeconds >= probe.SlamTelegraphSeconds)
					warnings.Add($"The Archivist interval ({probe.SlamIntervalSeconds}s) is not below its wind-up ({probe.SlamTelegraphSeconds}s), so its beam blinks off between sweeps instead of turning continuously.");
				break;

			case StillWardenBoss warden:
				if (warden.ShardsPerVolley <= 0 || warden.IceVolleyIntervalSeconds <= 0f)
					warnings.Add("The Still Warden drops no ice, so a boss that cannot chase has no pressure at all.");
				if (warden.IceScatterRadius <= warden.IceShardRadius)
					warnings.Add($"The Still Warden scatters ice within {warden.IceScatterRadius} using a {warden.IceShardRadius} radius, so every volley covers the player and none of it is dodgeable.");
				if (!ResourceLoader.Exists(warden.GuardScenePath))
					warnings.Add($"The Still Warden guard scene '{warden.GuardScenePath}' is missing, so it stands unguarded.");
				break;

			case LongCoilBoss coil:
				if (coil.SurfacedSeconds <= 0f)
					warnings.Add("The Long Coil is never above ground, so it can never be damaged and the fight cannot end.");
				if (coil.ResurfaceTellSeconds >= coil.DiveTravelSeconds)
					warnings.Add($"The Long Coil surfaces in {coil.DiveTravelSeconds}s but marks the ground for {coil.ResurfaceTellSeconds}s, so it arrives before its own warning resolves.");
				if (coil.BodySegments < 2)
					warnings.Add($"The Long Coil has {coil.BodySegments} body segments, so the one boss built around having a body behind it does not have one.");
				break;

			case DeepWardenBoss deep:
				if (deep.ChargeTellFraction <= 0f || deep.ChargeTellFraction >= 1f)
					warnings.Add($"The Warden of the Deep has ChargeTellFraction {deep.ChargeTellFraction}; at 0 it charges with no tell and at 1 it never charges.");
				if (deep.SweepDegreesPerSecond <= 0f)
					warnings.Add("The Warden of the Deep sweep does not turn, so its second phase is a fixed wedge.");
				if (deep.SurfacedSeconds <= 0f)
					warnings.Add("The Warden of the Deep is never above ground in its third phase, so the fight stalls there.");
				break;
		}
	}

	// Actually builds the boss once. A .tscn that exists but has the wrong script - or no script -
	// still passes ResourceLoader.Exists and then fails at minute fifteen, where nobody is watching.
	// Instantiate does not run _Ready (that needs the tree), so this is a cheap structural probe.
	private static void ValidateBossScene(List<string> warnings, BossDefinition boss)
	{
		// The scene a run spawns: the boss's own, or its stand-in while it is still being built.
		string path = boss.SpawnScenePath;
		if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
		{
			warnings.Add($"Boss '{boss.Id}' has a missing scene: '{path}'.");
			return;
		}

		var scene = GD.Load<PackedScene>(path);
		if (scene == null)
		{
			warnings.Add($"Boss '{boss.Id}' scene '{path}' failed to load.");
			return;
		}

		Node probe = scene.Instantiate();
		if (probe is not BossEnemy)
			warnings.Add($"Boss '{boss.Id}' scene '{path}' does not have a BossEnemy script attached, so the run could never be won.");

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

	// The Full Set Enchantment screen renders each set from its icon and its Effects list, so a set that ships
	// with neither is invisible or blank to the player. This cannot check that the numbers still
	// agree with Player.RefreshChestSetEffects - that switch is imperative - but it does catch a
	// new set being added without its presentation data filled in.
	// Issue #63: every Full Set Enchantment is the pieces of one object, and a piece may belong to
	// more than one (The Crown is in the Jewelled Crown and the Regalia). What that needs checked:
	// every relic is a piece of something, every piece is a relic that can actually drop, nothing is
	// listed twice in one set, and a set's partial threshold is below its size - a "two-piece taste"
	// on a two-piece set would be paid at the same moment as the full effect and mean nothing.
	// Nested sets can never block each other here because relics are never consumed: holding The
	// Crown for one set is holding it for the other.
	private static void ValidateFullSetObjects(List<string> warnings)
	{
		var pool = new HashSet<string>(ChestItemCatalog.AllItemIds, StringComparer.OrdinalIgnoreCase);
		var pieces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var set in ChestItemCatalog.Sets)
		{
			if (set.RequiredItemIds == null || set.RequiredItemIds.Length < 2)
			{
				warnings.Add($"Full Set Enchantment '{set.Id}' has fewer than two pieces; an object that cannot come apart is not a set.");
				continue;
			}
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string id in set.RequiredItemIds)
			{
				if (!seen.Add(id))
					warnings.Add($"Full Set Enchantment '{set.Id}' lists '{id}' twice.");
				if (!pool.Contains(id))
					warnings.Add($"Full Set Enchantment '{set.Id}' needs '{id}', which is not in ChestItemCatalog.AllItemIds, so it can never drop and the set can never complete.");
				pieces.Add(id);
			}
			if (set.PartialItemCount > 0 && set.PartialItemCount >= set.RequiredItemIds.Length)
				warnings.Add($"Full Set Enchantment '{set.Id}' pays its partial at {set.PartialItemCount} of {set.RequiredItemIds.Length} pieces, the same moment as the full effect.");
		}
		foreach (string id in ChestItemCatalog.AllItemIds)
		{
			// The tome relics are the one exception, and the opposite rule holds for them: they must
			// be in NO set (ChestItemCatalog.TomeRelicIds says why). ValidateTomeRelics checks that.
			if (ChestItemCatalog.IsTomeRelic(id))
				continue;
			if (!pieces.Contains(id))
				warnings.Add($"Relic '{id}' ({ChestItemCatalog.GetDisplayName(id)}) is a piece of no Full Set Enchantment. Every relic is part of an object (#63).");
		}
	}

	// The two relics that act on the spell book (.ai/passives-and-items.md, section 4b). Spells are
	// permanent once taken, so these are the only way the book changes, and each rule below is one a
	// build cannot see: a set that needed the Folio would make a set chase a hunt for the rarest
	// relic; a tag on either would double-dip the element budget a seventh slot already is; and a
	// Marginalia id that collided with a spell or boon would route a real pick to the fallback card.
	private static void ValidateTomeRelics(List<string> warnings)
	{
		foreach (string id in ChestItemCatalog.TomeRelicIds)
		{
			if (!ChestItemCatalog.AllItemIds.Contains(id, StringComparer.OrdinalIgnoreCase))
				warnings.Add($"Tome relic '{id}' is not in ChestItemCatalog.AllItemIds, so no chest can ever offer it.");
			if (ChestItemCatalog.GetElementTags(id).Count > 0)
				warnings.Add($"Tome relic '{id}' carries an element tag; a tome relic changes the book, and the book's spells carry the tags.");
			if (ChestItemCatalog.GetAssociatedSets(id).Count > 0)
				warnings.Add($"Tome relic '{id}' is a piece of a Full Set Enchantment; tome relics are one per run and must complete nothing.");
			if (ChestItemCatalog.GetDescription(id) == ChestItemCatalog.GetDescription("__no_such_relic__"))
				warnings.Add($"Tome relic '{id}' has no description of its own.");
		}
		if (ChestItemCatalog.GetRarity(ChestItemCatalog.BlankFolio) != ChestItemRarity.Relic)
			warnings.Add("The Blank Folio is not Relic rarity; a seventh spell slot belongs in the rarest tier.");

		string marginalia = Player.MarginaliaId;
		if (BoonCatalog.IsBoon(marginalia) || UnlockCatalog.AllSpellIds.Contains(marginalia, StringComparer.OrdinalIgnoreCase))
			warnings.Add($"'{marginalia}' is both the Marginalia fallback card and a spell or boon id; Node2DGame would route the real pick to the fallback.");
		if (!ResourceLoader.Exists("res://assets/bonelight/ui/icons/marginalia.png"))
			warnings.Add("The Marginalia card has no icon (res://assets/bonelight/ui/icons/marginalia.png); run python tools/art/relic_icons.py and import.");
	}

	// The collector economy (.ai/passives-and-items.md, section 8). Each rule here fails quietly in
	// play: a drop table whose chances sum past 1 at full Luck silently stops dropping the last item
	// in the roll; a sixth relic page overruns a chest screen laid out and measured for five; and a
	// schema-12 migration that lost its credit would close the Emberdeep on a player who had opened it.
	private static void ValidateCollectorEconomy(List<string> warnings)
	{
		const int ChestPagesLaidOut = 5;
		int mostOffers = DropTable.BaseChestOffers + 1 // the Relic Key
			+ CharacterRoster.GetAll().Count(c => c != null && "chest_options_1".Equals(c.StartingPassiveId?.Trim(), StringComparison.OrdinalIgnoreCase));
		if (mostOffers > ChestPagesLaidOut)
			warnings.Add($"A chest can offer {mostOffers} relics, but the chest screen is laid out and measured for {ChestPagesLaidOut}.");

		// Read into locals: compared as constants, a rule that holds today is "unreachable code".
		float orb = DropTable.BonusOrbChance, flask = DropTable.HealthFlaskChance, potion = DropTable.SpeedPotionChance;
		float sum = (orb + flask + potion) * (1f + DropTable.LuckScale);
		if (sum >= 1f || orb <= 0f || flask <= 0f || potion <= 0f)
			warnings.Add($"DropTable's bonus chances are out of range: {sum:0.000} a kill at full Luck, and each must be above 0.");
		float elite = DropTable.EliteChestChance, nested = DropTable.NestedChestChance;
		if (elite <= 0f || elite > 1f || nested >= 1f)
			warnings.Add("DropTable's chest chances are out of range: an elite's must be in (0, 1], and a nested chest's below 1 or the Hidden Drawer never stops.");
		int bigger = DropTable.GreaterOrbValue;
		if (bigger <= 2)
			warnings.Add($"A bigger XP orb is worth {bigger}, no more than the ordinary orb it replaces.");

		// Schema 12: a save that had everything at 11 is credited with John Dee; one that did not is not.
		var complete = new SaveData { SchemaVersion = 11 };
		foreach (string id in UnlockCatalog.AllSpellIds)
			complete.UnlockedSpellIds.Add(id);
		foreach (string id in UnlockCatalog.AllWizardIds.Where(id => !id.Equals("john_dee", StringComparison.OrdinalIgnoreCase)))
			complete.UnlockedCharacterIds.Add(id);
		complete.Migrate();
		// The gate chapter, not the last: the Emberdeep was the finale these saves had met, and the last
		// chapter is the castle's Throne now - not playable yet, which would turn this check off quietly.
		StageDefinition finale = StageCatalog.GateChapter;
		if (!GlobalStatsManager.IsCharacterUnlocked(complete, "john_dee")
			|| (finale != null && finale.IsPlayable && !GlobalStatsManager.IsStageAvailable(complete, finale)))
			warnings.Add("A save that had freed every wizard and recovered every spell at schema 11 loses the Emberdeep: SaveData.MigrateCollectorIntoCompletedCampaign did not credit John Dee, or schema 19 did not keep the gate open.");
		var partial = new SaveData { SchemaVersion = 11 };
		partial.Migrate();
		if (GlobalStatsManager.IsCharacterUnlocked(partial, "john_dee"))
			warnings.Add("A schema-11 save that had not met the campaign gate was handed John Dee by the schema-12 migration; he is earned by opening chests.");
	}

	private static void ValidateChestSetPresentation(List<string> warnings)
	{
		foreach (var set in ChestItemCatalog.Sets)
		{
			if (set.Effects == null || set.Effects.Length == 0)
				warnings.Add($"Chest set '{set.Id}' declares no Effects, so the Full Set Enchantment screen has nothing to list.");

			if (string.IsNullOrWhiteSpace(set.IconPath) || !ResourceLoader.Exists(set.IconPath))
				warnings.Add($"Chest set '{set.Id}' has a missing icon: '{set.IconPath}'.");

			if (set.RequiredItemIds == null || set.RequiredItemIds.Length == 0)
				warnings.Add($"Chest set '{set.Id}' requires no items, so it completes immediately.");
		}

		ValidateChestItemIcons(warnings);
	}

	// Relics are told apart by their icon in the chest menu, the HUD strip and the Full Set Enchantment screen,
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

	// The Apotheosis covenant (issue #60). Every one of these produces a gate that can never open, or
	// a reward that takes the player OUT of the tier that earned it, and none of them is visible to
	// dotnet build. A typo in a required evolution id is the classic one: the offer simply never
	// appears, forever, and nothing says why.
	private static void ValidateApotheosisCatalog(List<string> warnings)
	{
		var spells = ContentValidator.LoadAllSpells();
		var characters = CharacterRoster.GetAll();
		var seenCharacters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var seenVerbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (ApotheosisDefinition definition in ApotheosisCatalog.All)
		{
			string label = $"Apotheosis '{definition.Form?.Id}'";
			if (definition.Form == null || definition.Form.MilestoneLevel != ApotheosisCatalog.MilestoneLevel)
				warnings.Add($"{label} has no form, or a form whose MilestoneLevel is not {ApotheosisCatalog.MilestoneLevel}, so SpellData.ApplyEvolution would refuse it.");

			if (definition.CharacterId.Equals("test_wizard", StringComparison.OrdinalIgnoreCase))
				warnings.Add($"{label} is given to the Test Wizard, which picks its own loadout and is exempt from the covenant.");
			// One per wizard - or, for a wizard whose starting spell is taken in a form, one per form.
			if (!seenCharacters.Add($"{definition.CharacterId}:{definition.VariantId}"))
				warnings.Add($"{label}: character '{definition.CharacterId}' has more than one Apotheosis{(string.IsNullOrEmpty(definition.VariantId) ? "" : $" for '{definition.VariantId}'")}.");
			if (!seenVerbs.Add(definition.Verb))
				warnings.Add($"{label}: verb '{definition.Verb}' is already claimed. Each Apotheosis must own a verb no other does (#59).");

			CharacterData character = characters.FirstOrDefault(c => c.Id.Equals(definition.CharacterId, StringComparison.OrdinalIgnoreCase));
			if (character == null)
			{
				warnings.Add($"{label} names character '{definition.CharacterId}', which is not in CharacterRoster.");
				continue;
			}
			if (character.StartingSpellResource == null
				|| !character.StartingSpellResource.Id.Equals(definition.SpellId, StringComparison.OrdinalIgnoreCase))
			{
				warnings.Add($"{label} transforms '{definition.SpellId}', but {character.Id} starts with '{character.StartingSpellResource?.Id}'. An Apotheosis is the final form of the wizard's OWN starting spell.");
			}

			SpellData template = spells.FirstOrDefault(s => s.Id.Equals(definition.SpellId, StringComparison.OrdinalIgnoreCase));
			if (template == null)
			{
				warnings.Add($"{label} names spell '{definition.SpellId}', which is not in ContentValidator.SpellResourcePaths.");
				continue;
			}

			// A spell taken in a form: the Apotheosis is one form's, and its required path is that form's own
			// milestones (SpellData.Variants; ApotheosisDefinition.VariantId).
			SpellData spell;
			if (string.IsNullOrEmpty(definition.VariantId))
			{
				if (template.HasVariants)
					warnings.Add($"{label} transforms '{definition.SpellId}', which is taken in a form; it must name the form whose path it follows (VariantId).");
				spell = (SpellData)template.Duplicate(true);
				SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
			}
			else
			{
				SpellVariant form = template.FindVariant(definition.VariantId);
				spell = form == null ? null : SpellVariants.Preview(template, form);
				if (spell == null)
				{
					warnings.Add($"{label} names form '{definition.VariantId}', which '{definition.SpellId}' does not have.");
					continue;
				}
			}
			string element = definition.RequiredElement.ToString();
			if (!spell.ElementWeights.ContainsKey(element))
				warnings.Add($"{label} requires {element}, which '{spell.Id}' does not carry.");

			CheckRequiredPath(warnings, label, element, spell.Level4Options, definition.RequiredLevel4EvolutionId, 4);
			CheckRequiredPath(warnings, label, element, spell.Level8Options, definition.RequiredLevel8EvolutionId, 8);

			if (definition.Form == null)
				continue;

			// Preserve or increase, never reduce, and never a new element: the form may only add
			// weight to the element that qualified it.
			foreach (var pair in definition.Form.BonusElementWeights)
			{
				if (!pair.Key.Equals(element, StringComparison.OrdinalIgnoreCase))
					warnings.Add($"{label} adds {pair.Key}; a form may only deepen {element}, never name another element.");
				if (pair.Value < 0)
					warnings.Add($"{label} REDUCES {pair.Key}, which would drop the player out of the tier that earned it.");
			}

			// The two-element ceiling, across base, path and form together.
			var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var key in spell.ElementWeights.Keys) distinct.Add(key);
			foreach (var option in spell.Level4Options.Concat(spell.Level8Options))
			{
				if (option.Id.Equals(definition.RequiredLevel4EvolutionId, StringComparison.OrdinalIgnoreCase)
					|| option.Id.Equals(definition.RequiredLevel8EvolutionId, StringComparison.OrdinalIgnoreCase))
				{
					foreach (var key in option.BonusElementWeights.Keys) distinct.Add(key);
				}
			}
			foreach (var key in definition.Form.BonusElementWeights.Keys) distinct.Add(key);
			if (distinct.Count > 2)
				warnings.Add($"{label} ends up naming {distinct.Count} elements ({string.Join(", ", distinct)}); no spell may ever name more than two.");
		}
	}

	private static void CheckRequiredPath(List<string> warnings, string label, string element,
		Godot.Collections.Array<SpellEvolutionOption> options, string requiredId, int milestone)
	{
		SpellEvolutionOption option = options?.FirstOrDefault(o => o != null && o.Id.Equals(requiredId, StringComparison.OrdinalIgnoreCase));
		if (option == null)
		{
			warnings.Add($"{label} requires level {milestone} option '{requiredId}', which the spell does not offer. The gate can never open.");
			return;
		}
		// A required branch must never lead away from the wizard's own element (#61), or tier 6 is
		// harder on the exact build the gate demands.
		foreach (var key in option.BonusElementWeights.Keys)
		{
			if (!key.Equals(element, StringComparison.OrdinalIgnoreCase))
				warnings.Add($"{label}: required level {milestone} option '{requiredId}' branches into {key}, away from {element}.");
		}
		if (!string.IsNullOrEmpty(option.ReplacesElement))
			warnings.Add($"{label}: required level {milestone} option '{requiredId}' trades away {option.ReplacesElement}; a required path may only deepen {element}.");
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
	// Wave formation geometry. None of this is visible to a build, and all of it fails quietly in
	// play: a formation with a bad placement does not crash, it just spawns a wave that looks like
	// the scatter it was meant to replace, which is indistinguishable from the feature not being
	// finished.
	// Every element must be able to reach its 6-instance capstone.
	//
	// This is the check that guarded the passives-to-boons shift, and it is why that shift did not
	// silently strand five elements below their top tier. Passive spells carried roughly a third of
	// the element weight in the game - four of Metal's six carriers, two of Water's three - and
	// nothing breaks loudly when a carrier disappears: the element simply never reaches a threshold
	// again, and the bonus at the end of it becomes text nobody ever sees.
	//
	// It stays because the same thing can happen to any future content change.
	//
	// Slightly pessimistic by construction: magic_missile is built in code rather than loaded from
	// a resource, so its Arcane and Lightning tags are not counted. Both elements are far above the
	// floor, so a false pass is not possible from that omission - only a false failure, which would
	// be visible rather than silent.
	// Every boon needs exactly one way to be obtained, for the same reason every spell does: one
	// with no catalog entry is defined, offered by nothing, and invisible.
	// Rarity and element tags on chest items, which are two separate tables that have to agree.
	//
	// The rule they encode is the point: a tag belongs only on a scarce item. If a Common ever
	// picks one up, element weight stops being a budget - and nothing in play would look wrong,
	// the thresholds would just start arriving early and the reason would be invisible.
	private static void ValidateChestRarity(List<string> warnings)
	{
		foreach (string itemId in ChestItemCatalog.AllItemIds)
		{
			// Tome relics use rarity for how often they are offered and nothing else: they carry no
			// tag at any rarity (ValidateTomeRelics).
			if (ChestItemCatalog.IsTomeRelic(itemId))
				continue;
			ChestItemRarity rarity = ChestItemCatalog.GetRarity(itemId);
			IReadOnlyList<(string Element, int Weight)> tags = ChestItemCatalog.GetElementTags(itemId);
			bool scarce = rarity is ChestItemRarity.Rare or ChestItemRarity.Relic;

			if (!scarce && tags.Count > 0)
				warnings.Add($"Chest item '{itemId}' is {rarity} but carries {tags.Count} element tags; only Rare and Relic items may.");

			if (scarce && tags.Count == 0)
				warnings.Add($"Chest item '{itemId}' is {rarity} but carries no element tag, which is most of what that rarity is for.");

			if (rarity == ChestItemRarity.Relic && tags.Count != 2)
				warnings.Add($"Relic '{itemId}' carries {tags.Count} element tags; a Relic carries two.");

			if (rarity == ChestItemRarity.Rare && tags.Count != 1)
				warnings.Add($"Rare '{itemId}' carries {tags.Count} element tags; a Rare carries one.");

			CheckTagList(warnings, $"Chest item '{itemId}'", tags);
		}

		foreach (ChestSetDefinition set in ChestItemCatalog.Sets)
		{
			if (set.ElementTags.Length != 2)
				warnings.Add($"Full Set Enchantment '{set.Id}' grants {set.ElementTags.Length} element tags; a completed set grants two.");

			CheckTagList(warnings, $"Set '{set.Id}'", set.ElementTags);
		}
	}

	private static void CheckTagList(List<string> warnings, string owner, IReadOnlyList<(string Element, int Weight)> tags)
	{
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (element, weight) in tags)
		{
			if (!Enum.TryParse<Element>(element, true, out _))
				warnings.Add($"{owner} carries unknown element '{element}'.");
			if (!seen.Add(element))
				warnings.Add($"{owner} carries {element} twice; one instance per element, as with every other tag source.");
			if (weight != 1)
				warnings.Add($"{owner} carries {element} at weight {weight}; tags are one instance each.");
		}
	}

	private static void ValidateBoonSources(List<string> warnings)
	{
		foreach (BoonDefinition boon in BoonCatalog.All)
		{
			int sources = UnlockCatalog.All.Count(d =>
				d.Kind == UnlockKind.Boon && d.Id.Equals(boon.Id, StringComparison.OrdinalIgnoreCase));

			if (sources == 0)
				warnings.Add($"Boon '{boon.Id}' has no unlock source, so it can never be offered.");
			else if (sources > 1)
				warnings.Add($"Boon '{boon.Id}' has {sources} unlock sources; each thing needs exactly one.");

			// Same rule as spells: never the same element twice on one thing.
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var (element, weight) in boon.ElementWeights)
			{
				if (!Enum.TryParse<Element>(element, true, out _))
					warnings.Add($"Boon '{boon.Id}' carries unknown element '{element}'.");
				if (!seen.Add(element))
					warnings.Add($"Boon '{boon.Id}' carries {element} twice; base element weights are one per element.");
				if (weight != 1)
					warnings.Add($"Boon '{boon.Id}' carries {element} at weight {weight}; boons carry one instance per element.");
			}
		}
	}

	// A boon's card is its icon and its stat line, and both fail silently. The icon is drawn by
	// tools/art/boon_icons.py only for a boon with an ELEMENT entry there, and an unimported or
	// ungenerated PNG loads as null and the card shows an empty reliquary window. The stat line is
	// BoonCatalog.DescribeEffect's switch, whose default arm is an empty string - so a new BoonEffect
	// without an arm would be dealt as a card that says nothing at all.
	private static void ValidateBoonPresentation(List<string> warnings)
	{
		foreach (BoonDefinition boon in BoonCatalog.All)
		{
			if (!ResourceLoader.Exists(boon.IconPath))
				warnings.Add($"Boon '{boon.Id}' has no icon at '{boon.IconPath}'; add it to ELEMENT (and ICONS) in tools/art/boon_icons.py, run it and import.");
			if (string.IsNullOrWhiteSpace(BoonCatalog.DescribeEffect(boon)))
				warnings.Add($"Boon '{boon.Id}' has an empty stat line; BoonCatalog.DescribeEffect has no arm for {boon.Effect}.");
		}
		// Read into a local: compared as a constant, a rule that holds today is "unreachable code".
		float stance = BoonCatalog.StanceSeconds;
		if (stance <= 0f)
			warnings.Add("BoonCatalog.StanceSeconds is not positive, so a stance boon would hold even while walking.");
	}

	private static void ValidateElementReachability(List<string> warnings)
	{
		// Two pools with two separate caps: spells fill six slots, boons fill six of their own.
		// Summing them as one pool would let a six-boon element borrow spell slots it cannot use.
		var spellContributions = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
		var boonContributions = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
		// The second pass (.ai/achievements-and-secrets.md 6.3, item 5): only what a player has on the
		// road - starters, the shop's boons, Feats, and chapter deeds before the gate - and never a spell a
		// secret pays, since a secret is missable. No element's capstone may wait on one.
		var roadSpellContributions = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
		var roadBoonContributions = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

		static void RecordInto(Dictionary<string, List<int>> pool, string element, int weight)
		{
			if (weight <= 0)
				return;

			if (!pool.TryGetValue(element, out List<int> list))
			{
				list = new List<int>();
				pool[element] = list;
			}

			list.Add(weight);
		}

		void Record(string element, int weight) => RecordInto(spellContributions, element, weight);

		foreach (SpellData template in ContentValidator.LoadAllSpells())
		{
			if (template?.ElementWeights == null || string.IsNullOrWhiteSpace(template.Id))
				continue;

			// A spell taken in a form is ONE carrier whichever form it is taken in, so its best per element
			// is the best of its forms - never one entry per form, which would count three slots for one.
			var best = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			foreach (SpellData spell in SpellVariants.Forms(template))
			{
				SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
				List<(string Id, Dictionary<string, int> Gains)> level4 =
					MaterialiseGains(spell.Level4Options, spell.Id, warnings);
				List<(string Id, Dictionary<string, int> Gains)> level8 =
					MaterialiseGains(spell.Level8Options, spell.Id, warnings);

				// Every element this spell could ever touch - the ones it already carries AND the ones
				// it can branch into. Walking only the base tags missed branches entirely, which is how
				// the first run of this check under-counted Darkness: Obsidian Spike is a pure Earth
				// spell whose level 4 branch grants Darkness, and that carrier was invisible.
				var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (var pair in spell.ElementWeights)
					candidates.Add(pair.Key);
				foreach (var option in level4.Concat(level8))
				{
					foreach (var gain in option.Gains)
						candidates.Add(gain.Key);
				}

				foreach (string element in candidates)
				{
					// The best this one spell could ever contribute to this one element: its base tag,
					// plus the most generous evolution available at each milestone.
					int baseWeight = spell.ElementWeights.TryGetValue(element, out int w) ? w : 0;
					int total = baseWeight + BestGain(level4, element) + BestGain(level8, element);
					best[element] = best.TryGetValue(element, out int had) ? Math.Max(had, total) : total;
				}
			}
			bool onRoad = IsOnTheRoad(UnlockCatalog.GetSpell(template.Id));
			foreach (var pair in best)
			{
				Record(pair.Key, pair.Value);
				if (onRoad)
					RecordInto(roadSpellContributions, pair.Key, pair.Value);
			}
		}

		foreach (BoonDefinition boon in BoonCatalog.All)
		{
			bool onRoad = IsOnTheRoad(UnlockCatalog.Get(boon.Id, UnlockKind.Boon));
			foreach (var (element, weight) in boon.ElementWeights)
			{
				RecordInto(boonContributions, element, weight);
				if (onRoad)
					RecordInto(roadBoonContributions, element, weight);
			}
		}

		foreach (Element element in Enum.GetValues<Element>())
		{
			string name = element.ToString();
			int fromSpells = TopN(spellContributions, name, Player.MaxSpellSlots);
			int fromBoons = TopN(boonContributions, name, Player.MaxBoonSlots);
			int reachable = fromSpells + fromBoons;

			if (reachable < 6)
			{
				warnings.Add($"Element '{name}' can reach at most {reachable} instances ({fromSpells} from spells, {fromBoons} from boons), so its tier-6 bonus is unreachable. It needs more carriers.");
				continue;
			}

			int roadSpells = TopN(roadSpellContributions, name, Player.MaxSpellSlots);
			int roadBoons = TopN(roadBoonContributions, name, Player.MaxBoonSlots);
			if (roadSpells + roadBoons < 6)
				warnings.Add($"Element '{name}' reaches only {roadSpells + roadBoons} instances ({roadSpells} from spells, {roadBoons} from boons) without the spells hidden in secrets and past the gate, so its tier-6 bonus waits on a secret. Give it a carrier on the road (.ai/achievements-and-secrets.md 6.3, item 5).");
		}
	}

	// Whether an unlock can be had on the road, before the first gate, without a secret: a starter, a
	// shop boon, or an achievement that is no secret and names no chapter at or past the gate.
	private static bool IsOnTheRoad(UnlockDefinition unlock)
	{
		if (unlock == null)
			return false;
		if (unlock.Source == UnlockSource.Starter || unlock.Source == UnlockSource.Purchase)
			return true;
		AchievementDefinition deed = UnlockCatalog.SourceDeedOf(unlock);
		if (deed == null || deed.Kind == AchievementKind.Secret)
			return false;
		return IsBeforeFirstGate(deed.StageId);
	}

	// A chapter a new save can reach before the first gate: in the campaign before it, or outside the
	// campaign and open from the start. An empty chapter (a Feat's) is before every gate.
	private static bool IsBeforeFirstGate(string stageId)
	{
		if (string.IsNullOrWhiteSpace(stageId))
			return true;
		StageDefinition gate = StageCatalog.GateChapter;
		int position = StageCatalog.CampaignPositionOf(stageId);
		if (position < 0)
			return StageCatalog.GetById(stageId)?.Gate == StageGate.Open;
		return gate == null || position < StageCatalog.CampaignPositionOf(gate.Id);
	}

	private static int TopN(Dictionary<string, List<int>> pool, string element, int slots)
	{
		if (!pool.TryGetValue(element, out List<int> list))
			return 0;

		list.Sort();
		list.Reverse();
		return list.Take(slots).Sum();
	}

	private static int BestGain(List<(string Id, Dictionary<string, int> Gains)> options, string element)
	{
		int best = 0;
		foreach (var option in options)
		{
			if (option.Gains.TryGetValue(element, out int gain) && gain > best)
				best = gain;
		}

		return best;
	}

	private static void ValidateSpawnFormations(List<string> warnings)
	{
		foreach (SpawnFormation shape in Enum.GetValues<SpawnFormation>())
		{
			for (int count = 1; count <= 12; count++)
			{
				var bearings = new List<float>();
				int leftFlank = 0;
				int rightFlank = 0;

				for (int i = 0; i < count; i++)
				{
					var (bearing, radiusScale) = SpawnFormations.Placement(shape, i, count, 0f);

					if (float.IsNaN(bearing) || float.IsInfinity(bearing))
					{
						warnings.Add($"Formation {shape} member {i} of {count} has a bearing of {bearing}.");
						continue;
					}

					// A scale at or below zero puts the member on top of the player, which is the
					// same trap that silently invalidated an A/B of the spawn ring.
					if (radiusScale <= 0f)
						warnings.Add($"Formation {shape} member {i} of {count} has radius scale {radiusScale}; it would spawn inside the player.");

					bearings.Add(bearing);
					if (shape == SpawnFormation.Pincer)
					{
						if (Mathf.Sin(bearing) >= 0f) rightFlank++;
						else leftFlank++;
					}
				}

				if (bearings.Count != count)
					continue;

				// A Ring has to actually surround: its members must span most of a circle.
				if (shape == SpawnFormation.Ring && count >= 4)
				{
					float span = bearings.Max() - bearings.Min();
					if (span < Mathf.Pi * 1.4f)
						warnings.Add($"Formation Ring of {count} spans only {span:0.00} rad; it would read as an arc, not a ring.");
				}

				// An Arc has to stay in front. One that wraps past a half circle is a ring with a
				// gap, and the player would be flanked by something advertised as a wall.
				if (shape == SpawnFormation.Arc && count >= 2)
				{
					float span = bearings.Max() - bearings.Min();
					if (span > Mathf.Pi)
						warnings.Add($"Formation Arc of {count} spans {span:0.00} rad, more than half a circle.");
				}

				// A Pincer has to squeeze from both sides. Loading one flank makes it an arc that
				// happens to be off to one side.
				if (shape == SpawnFormation.Pincer && count >= 2 && Math.Abs(leftFlank - rightFlank) > 1)
					warnings.Add($"Formation Pincer of {count} splits {leftFlank}/{rightFlank} across its flanks; it should be even.");
			}
		}
	}

	// The swarm that closes on a player who falls behind (Node2DGame.SwarmPressure.cs). The knobs are
	// checked where a bad value would silently undo the feature or break a promise another system
	// makes - a ring margin under 1 puts bodies on camera out of nothing, a zero rise time is full
	// pressure on the first full frame - and the fold itself is run on two real enemy scenes, because
	// a fold that loses health or XP would quietly make falling behind easier rather than harder.
	private static void ValidateSwarmPressure(List<string> warnings)
	{
		if (Node2DGame.SwarmDefaultStartPaceSeconds < 0f || Node2DGame.SwarmDefaultStartPaceSeconds >= Node2DGame.ReferenceRunSeconds)
			warnings.Add($"Swarm pressure starts at pace {Node2DGame.SwarmDefaultStartPaceSeconds} s, outside the {Node2DGame.ReferenceRunSeconds} s reference run.");
		if (Node2DGame.SwarmDefaultRiseSeconds < 5f || Node2DGame.SwarmDefaultFallSeconds < 1f)
			warnings.Add($"Swarm pressure rises in {Node2DGame.SwarmDefaultRiseSeconds} s and falls in {Node2DGame.SwarmDefaultFallSeconds} s; a near-zero rise is full pressure on the first full frame.");
		if (Node2DGame.SwarmDefaultChokedShareToRise <= 0f || Node2DGame.SwarmDefaultChokedShareToRise > 1f)
			warnings.Add($"Swarm choked share {Node2DGame.SwarmDefaultChokedShareToRise} must be in (0, 1].");
		if (Node2DGame.SwarmDefaultFullEdgeMargin < 1f)
			warnings.Add($"Swarm full-pressure ring margin {Node2DGame.SwarmDefaultFullEdgeMargin} is under the screen edge; bodies would appear on camera.");
		if (Node2DGame.SwarmDefaultRingTighten < 0f || Node2DGame.SwarmDefaultRingTighten >= 1f)
			warnings.Add($"Swarm ring tighten {Node2DGame.SwarmDefaultRingTighten} must be in [0, 1).");
		if (Node2DGame.SwarmDefaultStragglerSpeedBonus < 0f || Node2DGame.SwarmDefaultNearSpeedBonus < 0f
			|| Node2DGame.SwarmDefaultNearSpeedBonus > Node2DGame.SwarmDefaultStragglerSpeedBonus)
			warnings.Add("Swarm speed bonuses must be non-negative, and a body on screen must never be sped more than a straggler.");
		if (Node2DGame.SwarmDefaultSwellPressure <= 0f || Node2DGame.SwarmDefaultSwellPressure > 1f
			|| Node2DGame.SwarmDefaultTrailRecyclePressure <= 0f || Node2DGame.SwarmDefaultTrailRecyclePressure > 1f)
			warnings.Add("Swarm fold and send-round thresholds must be in (0, 1].");
		if (Node2DGame.SwarmDefaultMaxMass < 2 || Node2DGame.SwarmDefaultSwellsPerSweep < 1)
			warnings.Add($"Swarm max mass {Node2DGame.SwarmDefaultMaxMass} and folds per sweep {Node2DGame.SwarmDefaultSwellsPerSweep} would fold nothing.");
		// XP that keeps pace with the run (Node2DGame.XpPacing.cs). Negative would make late kills pay
		// less than early ones; past about x5 at the boss the gates would come in the opening minutes.
		float endMultiplier = 1f + Node2DGame.XpDefaultGrowthPerPaceMinute * Node2DGame.ReferenceRunSeconds / 60f;
		if (Node2DGame.XpDefaultGrowthPerPaceMinute < 0f || endMultiplier > 5f)
			warnings.Add($"XP growth {Node2DGame.XpDefaultGrowthPerPaceMinute}/pace minute makes XP worth x{endMultiplier:0.00} at the boss; keep it in [0, 5].");

		var scene = GD.Load<PackedScene>("res://scenes/enemy.tscn");
		if (scene?.Instantiate() is not Enemy absorber || scene.Instantiate() is not Enemy absorbed)
		{
			warnings.Add("Swarm fold check: res://scenes/enemy.tscn did not instantiate as an Enemy.");
			return;
		}
		absorber.Health = 40;
		absorbed.Health = 25;
		int contact = absorber.ContactDamage;
		absorber.AbsorbIntoSwarm(absorbed);
		if (absorber.Health != 65 || absorber.SwarmMass != 2)
			warnings.Add($"Swarm fold: 40 + 25 health and mass 1 + 1 came out as {absorber.Health} health, mass {absorber.SwarmMass}.");
		for (int i = 0; i < 3; i++)
		{
			var more = (Enemy)scene.Instantiate();
			more.Health = 10;
			absorber.AbsorbIntoSwarm(more);
		}
		if (absorber.SwarmMass != 5 || absorber.ContactDamage != contact + 2)
			warnings.Add($"Swarm fold: a mass-5 body should touch for {contact + 2}, not {absorber.ContactDamage} (mass {absorber.SwarmMass}).");
		absorber.Free();
	}

	private static void ValidateElementTags(List<string> warnings)
	{
		// One pass, and every Godot array is materialised into a plain List before it is read more
		// than once. Enumerating a Godot.Collections.Array creates a fresh managed wrapper per
		// element per enumeration, so re-walking Level4Options inside a loop over Level8Options
		// produced enough short-lived wrappers to crash the finaliser mid-validation.
		// Every form of a spell taken in one (ContentValidator.LoadAllSpellForms): each has its own milestones.
		foreach (SpellData spell in ContentValidator.LoadAllSpellForms())
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

			string label = ContentValidator.Label(spell);
			List<(string Id, Dictionary<string, int> Gains)> level4 = MaterialiseGains(spell.Level4Options, label, warnings);
			List<(string Id, Dictionary<string, int> Gains)> level8 = MaterialiseGains(spell.Level8Options, label, warnings);
			ValidateEvolutionElementCeiling(label, baseWeights, level4, level8, warnings);
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
			// A traded-away element rides in the same map under a sentinel, so every reader of the
			// gains sees it without the tuple changing shape: BestGain never picks it (it is the
			// smallest int there is) and the ceiling below removes the element instead of adding.
			if (!string.IsNullOrEmpty(evo?.ReplacesElement))
			{
				if (!Enum.TryParse<Element>(evo.ReplacesElement, true, out _))
					warnings.Add($"Evolution '{evo.Id}' on '{spellId}' replaces element '{evo.ReplacesElement}', which is not a known element, so nothing is traded away.");
				else if (evo.BonusElementWeights != null && evo.BonusElementWeights.ContainsKey(evo.ReplacesElement))
					warnings.Add($"Evolution '{evo.Id}' on '{spellId}' both replaces and grants {evo.ReplacesElement}.");
				else
					gains[evo.ReplacesElement] = ReplacedElement;
			}
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

	// The gain MaterialiseGains records for an element a branch trades away.
	private const int ReplacedElement = int.MinValue;

	// Two ceilings that only hold by agreement between catalog entries, and that a player would
	// find by out-scaling the game rather than by anything looking broken:
	//
	//   1. No spell may end up naming more than TWO distinct elements. A hybrid has both of its
	//      slots filled already, so its evolutions may only deepen what it carries - or TRADE one
	//      element for another (SpellEvolutionOption.ReplacesElement), which keeps it at two;
	//      branching to a new element on top is what a pure spell trades its attunement for.
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
					// Trades first, as SpellData.GetElementWeights applies them, then the gains.
					foreach (var pair in gains)
					{
						if (pair.Value != ReplacedElement)
							continue;
						if (!totals.Remove(pair.Key))
							warnings.Add($"Spell '{spellId}' trades away {pair.Key} via '{(gains == early.Gains ? early.Id : late.Id)}', which it does not carry at that point.");
					}
					foreach (var pair in gains)
					{
						if (pair.Value != ReplacedElement)
							totals[pair.Key] = totals.TryGetValue(pair.Key, out int existing) ? existing + pair.Value : pair.Value;
					}
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

	// Element paths (.ai/spell-roster.md, "Element paths"; ElementPaths). The rules the user set, which
	// no single catalog entry can see broken:
	//
	//   1. A pure spell's level 4 offers BOTH a branch that deepens its own element and one that adds
	//      a different element - so the milestone is a decision, never a free tag.
	//   2. A hybrid's path is deepening its other half (a third element is never allowed), so each of
	//      its two elements is deepened by some level 4 or level 8 option.
	//   3. A branch into a new element looks like it (a tint) and names its tag on the card; a rider,
	//      if it has one, is the status that element carries (ElementPaths.RiderFor), with numbers
	//      its status can apply.
	//   4. Every element has a pure spell, because only a pure spell can carry an element to its
	//      capstone.
	//
	// Options are flattened to plain data first, for the reason ValidateElementTags gives.
	private static void ValidateElementPaths(List<string> warnings)
	{
		var pureElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (SpellData spell in ContentValidator.LoadAllSpellForms())
		{
			if (spell?.ElementWeights == null || string.IsNullOrWhiteSpace(spell.Id))
				continue;

			var baseElements = new HashSet<string>(spell.ElementWeights.Keys, StringComparer.OrdinalIgnoreCase);
			bool isPure = baseElements.Count == 1;
			if (isPure)
				pureElements.UnionWith(baseElements);

			SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
			var options = new List<(int Milestone, SpellEvolutionOption Option, List<string> Gains)>();
			foreach (var (milestone, list) in new[] { (4, spell.Level4Options), (8, spell.Level8Options) })
			{
				if (list == null)
					continue;
				foreach (SpellEvolutionOption evo in list.ToList())
				{
					if (evo == null)
						continue;
					var gains = evo.BonusElementWeights == null
						? new List<string>()
						: evo.BonusElementWeights.Where(p => p.Value > 0).Select(p => p.Key).ToList();
					options.Add((milestone, evo, gains));
				}
			}

			if (isPure)
			{
				string own = baseElements.First();
				bool deepens = options.Any(o => o.Milestone == 4 && o.Gains.Any(g => g.Equals(own, StringComparison.OrdinalIgnoreCase)));
				bool branches = options.Any(o => o.Milestone == 4 && o.Gains.Any(g => !baseElements.Contains(g)));
				if (!deepens)
					warnings.Add($"Pure spell '{spell.Id}' has no level 4 branch that deepens {own}; a pure spell's milestone must offer one.");
				if (!branches)
					warnings.Add($"Pure spell '{spell.Id}' has no level 4 element path - no branch adds a second element to {own}, so the milestone is never an element decision.");
			}
			else
			{
				foreach (string element in baseElements)
				{
					if (!options.Any(o => o.Gains.Any(g => g.Equals(element, StringComparison.OrdinalIgnoreCase))))
						warnings.Add($"Hybrid '{spell.Id}' never deepens {element}; a hybrid's element path is deepening either half.");
				}
			}

			foreach (var (milestone, evo, gains) in options)
			{
				foreach (string gain in gains.Where(g => !baseElements.Contains(g)))
				{
					if (evo.ModulateColor == Colors.White)
						warnings.Add($"Evolution '{evo.Id}' branches '{spell.Id}' into {gain} but keeps the spell's look (no ModulateColor); an element path must look like its element.");
					if (string.IsNullOrEmpty(evo.Description) || evo.Description.IndexOf(gain, StringComparison.OrdinalIgnoreCase) < 0)
						warnings.Add($"Evolution '{evo.Id}' branches '{spell.Id}' into {gain} but its description never names the {gain} tag.");
				}

				if (evo.Rider == ElementRider.None)
					continue;
				bool carried = gains.Any(g => Enum.TryParse<Element>(g, true, out Element e) && ElementPaths.RiderFor(e) == evo.Rider);
				if (!carried)
					warnings.Add($"Evolution '{evo.Id}' on '{spell.Id}' carries a {evo.Rider} rider, but no element it grants ({string.Join(", ", gains)}) carries {evo.Rider} (ElementPaths.RiderFor).");
				if (!ElementPaths.IsValid(evo.Rider, evo.RiderMagnitude, evo.RiderSeconds))
					warnings.Add($"Evolution '{evo.Id}' on '{spell.Id}' has a {evo.Rider} rider of {evo.RiderMagnitude} for {evo.RiderSeconds} s, which its status cannot apply.");
			}
		}

		foreach (Element element in Enum.GetValues<Element>())
		{
			if (!pureElements.Contains(element.ToString()))
				warnings.Add($"Element '{element}' has no pure spell, so nothing can carry it to its 6-instance capstone.");
		}
	}

	// Every number on a level 4, level 8 or Apotheosis card must be one its spell's script reads
	// (EvolutionStatReaders). Thirteen spells shared default evolutions written for a bolt, and a pit in
	// the ground offered "projectile speed" and "pierce" that nothing it ran ever read - a field that is
	// set and never read compiles, loads and shows on the card, so only a player could see it
	// (2026-10-24, "isn't it an aoe? does that even make sense?"). This is the check that sees it, for
	// every spell in ContentValidator.SpellResourcePaths, the next one to land on the default archetype
	// included. What the hand-written cards still promise and never built is listed, with the promise,
	// in EvolutionStatReaders.KnownUnbuilt; an entry there that stops being true is reported as stale,
	// so the list can only shrink. Options are flattened to plain lists first, for the reason
	// ValidateElementTags gives.
	// No pixel counts in anything a player reads (2026-10-28: "spiritual weapon mutation says it increases
	// by 60px. never mention px, add a %"). A pixel is the canvas's unit, not the player's: a size is a
	// percentage of what it was, or words ("a small splash", "farther with Area"). Walks every spell's
	// description and evolution cards - for a spell taken in a form, every form's (LoadAllSpellForms), since
	// each has its own words and milestones - every Apotheosis form, every boon and every relic.
	private static readonly System.Text.RegularExpressions.Regex PixelUnit =
		new(@"\d\s*px\b|\bpx\b|\bpixels?\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

	private static void ValidateNoPixelsInText(List<string> warnings)
	{
		void Check(string where, string text)
		{
			if (!string.IsNullOrEmpty(text) && PixelUnit.IsMatch(text))
				warnings.Add($"Text: {where} says \"{PixelUnit.Match(text).Value}\" - give a size as a percentage or in words, never in pixels: \"{text}\"");
		}

		foreach (SpellData spell in ContentValidator.LoadAllSpellForms())
		{
			if (spell == null)
				continue;
			Check($"spell {ContentValidator.Label(spell)}", spell.Description);
			SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
			foreach (SpellEvolutionOption option in (spell.Level4Options?.ToList() ?? new()).Concat(spell.Level8Options?.ToList() ?? new()))
			{
				Check($"evolution {option.Id}", option.Description);
				Check($"evolution {option.Id}'s synergy line", option.SynergyDescription);
			}
		}
		foreach (ApotheosisDefinition definition in ApotheosisCatalog.All)
			Check($"Apotheosis {definition.Form?.Id}", definition.Form?.Description);
		foreach (BoonDefinition boon in BoonCatalog.All)
		{
			Check($"boon {boon.Id}", boon.Description);
			Check($"boon {boon.Id}'s effect", BoonCatalog.ExplainEffect(boon));
		}
		foreach (string itemId in ChestItemCatalog.AllItemIds)
			Check($"relic {itemId}", ChestItemCatalog.GetDescription(itemId));
	}

	private static void ValidateEvolutionReaders(List<string> warnings)
	{
		var unbuilt = new HashSet<(string, EvolutionStat, SpellEffect)>();
		foreach (UnbuiltPromise promise in EvolutionStatReaders.KnownUnbuilt)
			unbuilt.Add((promise.OptionId.ToLowerInvariant(), promise.Stat, promise.Effect));
		var stillUnbuilt = new HashSet<(string, EvolutionStat, SpellEffect)>();

		// Every form of a spell taken in one, against its form's row (EvolutionStatReaders.TryGet(SpellData)).
		foreach (SpellData spell in ContentValidator.LoadAllSpellForms())
		{
			if (spell == null || string.IsNullOrWhiteSpace(spell.Id))
				continue;
			if (!EvolutionStatReaders.TryGet(spell, out SpellStatReaders readers))
			{
				warnings.Add($"Spell '{ContentValidator.Label(spell)}' has no entry in EvolutionStatReaders, so nothing checks that its evolutions' numbers reach it. Add the fields its script reads.");
				continue;
			}

			SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
			var options = new List<SpellEvolutionOption>();
			if (spell.Level4Options != null)
				options.AddRange(spell.Level4Options.ToList());
			if (spell.Level8Options != null)
				options.AddRange(spell.Level8Options.ToList());
			foreach (ApotheosisDefinition definition in ApotheosisCatalog.All)
			{
				if (definition.Form != null && definition.SpellId.Equals(spell.Id, StringComparison.OrdinalIgnoreCase)
					&& (string.IsNullOrEmpty(definition.VariantId) || definition.VariantId.Equals(spell.SelectedVariantId, StringComparison.OrdinalIgnoreCase)))
					options.Add(definition.Form);
			}

			foreach (SpellEvolutionOption option in options)
			{
				if (option == null)
					continue;
				string id = option.Id.ToLowerInvariant();
				EvolutionStat set = EvolutionStatReaders.StatsSetBy(option);
				EvolutionStat unread = set & ~readers.Stats & ~EvolutionStat.Effect;
				// A rider's numbers with no rider to carry them are read by nothing, whatever the spell.
				if (option.Rider == ElementRider.None && (set & EvolutionStat.Rider) != 0)
					unread |= EvolutionStat.Rider;

				foreach (EvolutionStat stat in Enum.GetValues<EvolutionStat>())
				{
					if (stat == EvolutionStat.None || stat == EvolutionStat.Effect || (unread & stat) == 0)
						continue;
					ReportUnread(warnings, unbuilt, stillUnbuilt, spell.Id, readers, option, (id, stat, SpellEffect.None), EvolutionStatField(stat));
				}

				if (option.Effect != SpellEffect.None && !readers.ReadsEffect(option.Effect))
					ReportUnread(warnings, unbuilt, stillUnbuilt, spell.Id, readers, option, (id, EvolutionStat.Effect, option.Effect), $"Effect {option.Effect}");
				else if (option.Effect == SpellEffect.None && MathF.Abs(option.EffectValue) > 0.0001f)
					ReportUnread(warnings, unbuilt, stillUnbuilt, spell.Id, readers, option, (id, EvolutionStat.Effect, SpellEffect.None), $"an EffectValue of {option.EffectValue} with no Effect");
			}
		}

		foreach (var entry in unbuilt)
		{
			if (!stillUnbuilt.Contains(entry))
			{
				string field = entry.Item2 != EvolutionStat.Effect ? EvolutionStatField(entry.Item2)
					: entry.Item3 == SpellEffect.None ? "an EffectValue with no Effect" : $"Effect {entry.Item3}";
				warnings.Add($"EvolutionStatReaders.KnownUnbuilt lists '{entry.Item1}' for {field}, which is no longer an unread number on any card - it was built, the option stopped setting it, or the option is gone. Take it off the list.");
			}
		}
	}

	// The same rule for the ordinary level-ups, levels 2 to 8: every number a level-up card shows must be
	// one its spell reads. ValidateEvolutionReaders covered only the milestone cards, and the level table
	// is half data (the .tres) and half code (Player.EnsureRelevantLevelUps adds a level-up per scaling
	// tag), so this checks the table after Player.PrepareSpellLevelUps, as a run sees it. Added
	// 2026-10-26, when the milestone cards' list of unbuilt promises reached zero.
	private static void ValidateLevelUpReaders(List<string> warnings)
	{
		// Every form's own level table (a form replaces the spell's, SpellVariants.Apply), against its row.
		foreach (SpellData spell in ContentValidator.LoadAllSpellForms())
		{
			if (spell == null || string.IsNullOrWhiteSpace(spell.Id) || spell.IsPassive)
				continue;
			if (!EvolutionStatReaders.TryGet(spell, out SpellStatReaders readers))
				continue; // ValidateEvolutionReaders reports the missing entry

			Player.PrepareSpellLevelUps(spell);
			var reported = new HashSet<string>();
			string label = ContentValidator.Label(spell);
			foreach (SpellLevelUpgrade upgrade in spell.LevelUpgrades)
			{
				if (upgrade == null)
					continue;
				void Check(bool set, EvolutionStat stat, string field)
				{
					if (set && (readers.Stats & stat) == 0 && reported.Add(field))
						warnings.Add($"Spell '{label}' level {upgrade.Level} grants {field}, which it never reads ({readers.ReadIn}). A level-up must change the spell it levels.");
				}
				Check(upgrade.DamageBonus != 0, EvolutionStat.Damage, "DamageBonus");
				Check(MathF.Abs(upgrade.CooldownBonus) > 0.0001f, EvolutionStat.Cooldown, "CooldownBonus");
				Check(upgrade.ProjectileCountBonus != 0, EvolutionStat.Projectiles, "ProjectileCountBonus");
				Check(MathF.Abs(upgrade.RangeBonus) > 0.0001f, EvolutionStat.Range, "RangeBonus");
				Check(upgrade.ChainArcBonus != 0, EvolutionStat.ChainArcs, "ChainArcBonus");
				Check(upgrade.ChainBranchBonus != 0, EvolutionStat.ChainBranches, "ChainBranchBonus");
				// Read only by ChainLightning, beside its arcs.
				Check(MathF.Abs(upgrade.ChainChanceBonus) > 0.0001f, EvolutionStat.ChainArcs, "ChainChanceBonus");
				Check(upgrade.PoisonTickBonus != 0, EvolutionStat.PoisonTick, "PoisonTickBonus");
				if (upgrade.Effect != SpellEffect.None && !readers.ReadsEffect(upgrade.Effect) && reported.Add(upgrade.Effect.ToString()))
					warnings.Add($"Spell '{label}' level {upgrade.Level} grants Effect {upgrade.Effect} {upgrade.EffectValue}, which it never reads ({readers.ReadIn}). A level-up must change the spell it levels.");
			}
		}
	}

	private static void ReportUnread(List<string> warnings, HashSet<(string, EvolutionStat, SpellEffect)> unbuilt,
		HashSet<(string, EvolutionStat, SpellEffect)> stillUnbuilt, string spellId, SpellStatReaders readers,
		SpellEvolutionOption option, (string, EvolutionStat, SpellEffect) key, string what)
	{
		if (unbuilt.Contains(key))
		{
			stillUnbuilt.Add(key);
			return;
		}
		warnings.Add($"Evolution '{option.Id}' on '{spellId}' sets {what}, which '{spellId}' never reads ({readers.ReadIn}). "
			+ "A number on a card must reach the spell: give the option a stat the spell reads, or make the spell read this one and add it to its EvolutionStatReaders entry.");
	}

	private static string EvolutionStatField(EvolutionStat stat) => stat switch
	{
		EvolutionStat.Damage => "DamageBonus/DamageMultiplier",
		EvolutionStat.Cooldown => "CooldownBonus/CooldownMultiplier",
		EvolutionStat.Projectiles => "ProjectileCountBonus",
		EvolutionStat.Range => "RangeBonus",
		EvolutionStat.Area => "AreaMultiplier",
		EvolutionStat.Pierce => "PierceBonus",
		EvolutionStat.ChainArcs => "ChainArcBonus",
		EvolutionStat.ChainBranches => "ChainBranchBonus",
		EvolutionStat.Knockback => "KnockbackBonus",
		EvolutionStat.Slow => "SlowMagnitudeBonus",
		EvolutionStat.PoisonTick => "PoisonTickBonus",
		EvolutionStat.Speed => "SpeedMultiplier",
		EvolutionStat.Scale => "ScaleMultiplier",
		EvolutionStat.Rider => "Rider/RiderMagnitude/RiderSeconds",
		_ => stat.ToString(),
	};

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

			// No unlock check on the starting spell any more: Player.GetLevelUpOptions always offers a
			// character its own starting spell, which the rescued wizards need - they open on spells
			// the save may not own yet (.ai/side-events.md).

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

			if (definition.Kind == UnlockKind.Boon && BoonCatalog.GetById(definition.Id) == null)
				warnings.Add($"Unlock catalog lists boon '{definition.Id}', which BoonCatalog does not define.");

			if (definition.Source == UnlockSource.Achievement
				&& AchievementDefinitions.GetById(definition.SourceId) == null)
			{
				warnings.Add($"Unlock '{definition.Id}' points at achievement '{definition.SourceId}', which does not exist, so it can never be earned.");
			}

			// EVERY SPELL IS EARNED (.ai/achievements-and-secrets.md section 2): a spell is a starter or an
			// achievement's - a Feat, a chapter's deed or a secret - and a wizard the same. The shop sells
			// boons only, and Discovery is retired: a side event's unlock is its Road deed's reward.
			if (definition.Source == UnlockSource.Discovery)
				warnings.Add($"Unlock '{definition.Id}' is sourced Discovery, which is retired; source it from its side event's Road deed (AchievementDefinitions.EventDeedId) with its SiteId.");
			else if (definition.Kind != UnlockKind.Boon && definition.Source == UnlockSource.Purchase)
				warnings.Add($"{definition.Kind} '{definition.Id}' is bought with Arcane Energy; since schema 19 every {definition.Kind.ToString().ToLowerInvariant()} is a starter or an achievement's.");

			// A side event's unlock names its site, and its source is that site's deed - the deed reads
			// its reward from the site, so the two must agree or the event pays nothing.
			if (!string.IsNullOrWhiteSpace(definition.SiteId)
				&& !definition.SourceId.Equals(AchievementDefinitions.EventDeedId(definition.SiteId), StringComparison.OrdinalIgnoreCase))
				warnings.Add($"Unlock '{definition.Id}' is paid at site '{definition.SiteId}' but sourced from '{definition.SourceId}', not that site's deed '{AchievementDefinitions.EventDeedId(definition.SiteId)}'.");

			// A spell a vault pays: its locked hint is the Spell Index's line for it, and must be the
			// deed's sealed line - the chapter, never the place or the verb (decision 7).
			AchievementDefinition source = UnlockCatalog.SourceDeedOf(definition);
			if (source != null && source.Kind == AchievementKind.Secret
				&& !definition.LockedHint.TrimEnd('.').Equals(source.SealedLine, StringComparison.Ordinal))
				warnings.Add($"Unlock '{definition.Id}' is hidden in a secret, and its hint \"{definition.LockedHint}\" is not its deed's sealed line \"{source.SealedLine}.\"");

			ValidateSecret(definition, warnings);
		}

		// A side event's site pays at most one thing of each kind: its deed has one field per kind.
		foreach (IGrouping<string, UnlockDefinition> site in UnlockCatalog.All
			.Where(d => !string.IsNullOrWhiteSpace(d.SiteId)).GroupBy(d => d.SiteId, StringComparer.OrdinalIgnoreCase))
		{
			foreach (IGrouping<UnlockKind, UnlockDefinition> kind in site.GroupBy(d => d.Kind))
				if (kind.Count() > 1)
					warnings.Add($"Site '{site.Key}' pays {kind.Count()} {kind.Key}s; its deed carries one of each kind.");
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
			// A wizard freed by an achievement (John Dee) keeps the same agreement a spell does.
			if (!string.IsNullOrWhiteSpace(achievement.CharacterUnlockId))
			{
				UnlockDefinition wizard = UnlockCatalog.GetCharacter(achievement.CharacterUnlockId);
				if (wizard != null && (wizard.Source != UnlockSource.Achievement || !wizard.SourceId.Equals(achievement.Id, StringComparison.OrdinalIgnoreCase)))
					warnings.Add($"Achievement '{achievement.Id}' frees '{achievement.CharacterUnlockId}', but the catalog sources that wizard from {wizard.Source} '{wizard.SourceId}' - one of the two is wrong.");
			}

			// And a boon (the Long Coil's, an event's), the same agreement.
			if (!string.IsNullOrWhiteSpace(achievement.BoonUnlockId))
			{
				UnlockDefinition boon = UnlockCatalog.Get(achievement.BoonUnlockId, UnlockKind.Boon);
				if (boon == null)
					warnings.Add($"Achievement '{achievement.Id}' grants boon '{achievement.BoonUnlockId}', which has no unlock catalog entry.");
				else if (boon.Source != UnlockSource.Achievement || !boon.SourceId.Equals(achievement.Id, StringComparison.OrdinalIgnoreCase))
					warnings.Add($"Achievement '{achievement.Id}' grants '{achievement.BoonUnlockId}', but the catalog sources that boon from {boon.Source} '{boon.SourceId}' - one of the two is wrong.");
			}

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

		foreach (StageDefinition gate in StageCatalog.GateChapters)
		{
			if (GlobalStatsManager.IsGateMet(fresh, gate) || GlobalStatsManager.IsStageEarned(fresh, gate))
				warnings.Add($"A brand-new save already satisfies {gate.DisplayName}'s gate, so it is not gated by anything.");
		}

		// The other direction, for every spell: its achievement exists and grants it. The forward check
		// above walks the deeds; this one walks the spells, so a spell pointing at a deed that pays
		// something else is caught from its own side too.
		foreach (UnlockDefinition spell in UnlockCatalog.All.Where(d => d.Kind == UnlockKind.Spell && d.Source == UnlockSource.Achievement))
		{
			AchievementDefinition deed = AchievementDefinitions.GetById(spell.SourceId);
			if (deed != null && !deed.SpellUnlockId.Equals(spell.Id, StringComparison.OrdinalIgnoreCase))
				warnings.Add($"Spell '{spell.Id}' is sourced from '{deed.Id}', which grants '{deed.SpellUnlockId}' instead, so it can never be earned.");
		}

		ValidateCuration(fresh, warnings);
	}

	// A secret wizard's card is a sealed blank until its reveal condition holds (UnlockDefinition.
	// IsSecret, .ai/side-events.md "Secret wizards"). Every way that goes wrong is silent: a secret
	// with no reveal, or one pointing at a chapter that does not exist or never opens, is a card that
	// stays sealed for ever and looks exactly like one the player has not reached yet.
	private static void ValidateSecret(UnlockDefinition definition, List<string> warnings)
	{
		if (!definition.IsSecret)
		{
			if (!string.IsNullOrWhiteSpace(definition.RevealStageId))
				warnings.Add($"Unlock '{definition.Id}' names a reveal chapter '{definition.RevealStageId}' but is not secret, so nothing reads it.");
			return;
		}

		if (definition.Kind != UnlockKind.Character)
			warnings.Add($"Unlock '{definition.Id}' is a secret {definition.Kind}; only the character select reads IsSecret, so it would show as an ordinary lock.");
		if (definition.Source == UnlockSource.Starter)
			warnings.Add($"Starter '{definition.Id}' is marked secret; a starter is never locked, so the secret can never show.");

		if (string.IsNullOrWhiteSpace(definition.RevealStageId))
		{
			warnings.Add($"Secret '{definition.Id}' has no reveal condition (RevealStageId), so its card stays sealed for ever.");
			return;
		}

		StageDefinition stage = StageCatalog.GetById(definition.RevealStageId);
		if (stage == null)
		{
			warnings.Add($"Secret '{definition.Id}' is revealed by chapter '{definition.RevealStageId}', which StageCatalog does not define, so its card stays sealed for ever.");
			return;
		}
		if (!stage.IsPlayable)
			warnings.Add($"Secret '{definition.Id}' is revealed by '{stage.DisplayName}', which is not playable yet, so its card stays sealed.");
		// A gate opens only once the wizards it counts are free, and a wizard nobody has seen cannot be
		// looked for: a wizard a gate counts must be revealed by a chapter BEFORE that gate, or he would
		// stay sealed until after he was needed (.ai/endgame-castle.md 1.6, generalised from the one gate).
		if (definition.Kind == UnlockKind.Character)
		{
			int revealAt = StageCatalog.CampaignPositionOf(stage.Id);
			foreach (StageDefinition gate in StageCatalog.GateChapters)
			{
				if (GlobalStatsManager.CountsTowardGate(definition.Id, gate)
					&& (revealAt < 0 ? stage.Gate != StageGate.Open : revealAt >= StageCatalog.CampaignPositionOf(gate.Id)))
					warnings.Add($"Secret '{definition.Id}' is counted by {gate.DisplayName}'s gate but revealed by '{stage.DisplayName}', which is not before it - he would stay sealed until after he was needed.");
			}
		}
		if (GlobalStatsManager.IsCharacterRevealed(new SaveData(), definition.Id) && stage.Gate != StageGate.Open)
			warnings.Add($"Secret '{definition.Id}' reads as revealed on a brand-new save although '{stage.DisplayName}' is closed on one; IsCharacterRevealed has stopped honouring the reveal.");

		// A wizard found at a side event is revealed by the chapter that holds him: the place the
		// player is shown is the place he is freed from. Anything else hints at the wrong map.
		if (!string.IsNullOrWhiteSpace(definition.SiteId))
		{
			var site = SideEventDirector.ForStage(stage.Id);
			if (site == null || !site.Value.SiteId.Equals(definition.SiteId, StringComparison.OrdinalIgnoreCase))
				warnings.Add($"Secret '{definition.Id}' is found at '{definition.SiteId}' but revealed by '{stage.DisplayName}', whose side event is '{site?.SiteId ?? "none"}'; reveal him with the chapter that holds him.");
		}
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

			// A secret's deed always pays its mark on the page (the found level or Arcane lump of a
			// keepsake is SecretDirector's to pay, never the deed's - so it can never be paid twice).
			bool grantsSomething = !string.IsNullOrWhiteSpace(achievement.SpellUnlockId)
				|| !string.IsNullOrWhiteSpace(achievement.BoonUnlockId)
				|| !string.IsNullOrWhiteSpace(achievement.CharacterUnlockId)
				|| achievement.CurrencyReward > 0
				|| achievement.Kind == AchievementKind.Secret;
			if (!grantsSomething)
				warnings.Add($"Achievement '{achievement.Id}' grants nothing, so completing it is not a reward.");

			if (!string.IsNullOrWhiteSpace(achievement.CharacterUnlockId)
				&& UnlockCatalog.GetCharacter(achievement.CharacterUnlockId) == null)
			{
				warnings.Add($"Achievement '{achievement.Id}' frees '{achievement.CharacterUnlockId}', which has no unlock catalog entry.");
			}

			ValidateAchievementShape(achievement, warnings);
		}

		// Derivation is complete in both directions: every side event's site and every secret has its
		// deed, and the words table names no site that has none (a renamed site would leave words behind).
		var sites = StageCatalog.All.Select(s => global::SideEventDirector.ForStage(s.Id)?.SiteId)
			.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
		foreach (string site in sites)
		{
			if (AchievementDefinitions.GetById(AchievementDefinitions.EventDeedId(site)) == null)
				warnings.Add($"Achievements: the side event at '{site}' has no deed, so winning it records nothing.");
			if (!AchievementDefinitions.HasSideEventWords(site))
				warnings.Add($"Achievements: the side event at '{site}' has no name, line or reward words (AchievementDefinitions.SideEventWords).");
		}
		foreach (string site in AchievementDefinitions.SideEventWordSites)
		{
			if (!sites.Contains(site, StringComparer.OrdinalIgnoreCase))
				warnings.Add($"Achievements: there are words for a side event at '{site}', which no chapter's side event stands on.");
		}
		foreach (SecretDefinition secret in SecretCatalog.Defined)
		{
			if (AchievementDefinitions.GetById(AchievementDefinitions.SecretDeedId(secret.Id)) == null)
				warnings.Add($"Achievements: the secret '{secret.Id}' has no deed, so finding it records nothing.");
		}

		// The reward words name what the deed actually grants (the derived deeds' words are written by
		// hand beside an id read from another catalog; a renamed spell or a moved reward would leave
		// the card saying the old thing).
		Dictionary<string, string> spellNames = ContentValidator.LoadAllSpells()
			.Where(s => s != null && !string.IsNullOrWhiteSpace(s.Id))
			.GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);
		foreach (AchievementDefinition achievement in AchievementDefinitions.All)
		{
			var names = new List<string>();
			if (!string.IsNullOrWhiteSpace(achievement.SpellUnlockId))
				names.Add(spellNames.TryGetValue(achievement.SpellUnlockId, out string spell) ? spell : achievement.SpellUnlockId);
			if (!string.IsNullOrWhiteSpace(achievement.BoonUnlockId))
				names.Add(BoonCatalog.GetById(achievement.BoonUnlockId)?.Name ?? achievement.BoonUnlockId);
			if (!string.IsNullOrWhiteSpace(achievement.CharacterUnlockId))
				names.Add(CharacterRoster.GetAll().FirstOrDefault(c => c != null
					&& c.Id.Equals(achievement.CharacterUnlockId, StringComparison.OrdinalIgnoreCase))?.Name ?? achievement.CharacterUnlockId);
			foreach (string name in names)
			{
				if (achievement.RewardText.IndexOf(name, StringComparison.Ordinal) < 0)
					warnings.Add($"Achievement '{achievement.Id}' grants {name}, but its reward line \"{achievement.RewardText}\" does not say so.");
			}
		}
	}

	// The fields .ai/achievements-and-secrets.md 2.6 added must agree with each other, and every one of
	// them disagreeing is silent: a Hidden deed with no seen line is a blank row, a Road deed with no
	// chapter is grouped nowhere, a deed completed in the run with no fact to measure is never done.
	private static void ValidateAchievementShape(AchievementDefinition deed, List<string> warnings)
	{
		string id = deed.Id;
		if (string.IsNullOrWhiteSpace(deed.DisplayName) || string.IsNullOrWhiteSpace(deed.Description) || string.IsNullOrWhiteSpace(deed.RewardText))
			warnings.Add($"Achievement '{id}' has no name, line or reward words, so its row says nothing.");

		bool hasChapter = !string.IsNullOrWhiteSpace(deed.StageId);
		StageDefinition stage = StageCatalog.GetById(deed.StageId);
		if (hasChapter && stage == null)
			warnings.Add($"Achievement '{id}' belongs to '{deed.StageId}', which is not a chapter in StageCatalog.");

		switch (deed.Kind)
		{
			case AchievementKind.Feat:
			case AchievementKind.Collection:
				if (deed.Visibility != AchievementVisibility.Always)
					warnings.Add($"Achievement '{id}' is a {deed.Kind} shown {deed.Visibility}; a {deed.Kind} is listed from the first run.");
				break;
			case AchievementKind.Road:
				if (!hasChapter || deed.Visibility != AchievementVisibility.ChapterOpen)
					warnings.Add($"Achievement '{id}' is a Road deed with chapter '{deed.StageId}' shown {deed.Visibility}; a chapter's deed names its chapter and is listed once it is open.");
				break;
			case AchievementKind.Secret:
				if (!hasChapter || deed.Visibility != AchievementVisibility.Hidden || string.IsNullOrWhiteSpace(deed.SecretId))
					warnings.Add($"Achievement '{id}' is a Secret deed that is not Hidden, names no chapter or mirrors no secret.");
				break;
		}

		if (deed.Visibility == AchievementVisibility.Hidden)
		{
			if (string.IsNullOrWhiteSpace(deed.SealedLine) || string.IsNullOrWhiteSpace(deed.SeenLine))
				warnings.Add($"Achievement '{id}' is Hidden with no sealed line or no seen line (.ai/achievements-and-secrets.md 4.3), so its row has nothing to show before it is found.");
			if (AchievementDefinitions.IsListed(deed, new SaveData()))
				warnings.Add($"Achievement '{id}' is Hidden but listed on a brand-new save.");
		}

		// A deed for a secret wizard is sealed until his chapter opens, and says so with a line.
		if (AchievementDefinitions.IsSealed(deed, new SaveData()) && string.IsNullOrWhiteSpace(deed.SealedLine))
			warnings.Add($"Achievement '{id}' frees a secret wizard but has no sealed line for the row it shows before his chapter opens.");

		bool mirrors = !string.IsNullOrWhiteSpace(deed.SiteId) || !string.IsNullOrWhiteSpace(deed.SecretId);
		if (deed.CompletesInRun != mirrors)
			warnings.Add($"Achievement '{id}' {(deed.CompletesInRun ? "completes in the run but mirrors no site or secret" : "mirrors a site or a secret but is not completed in the run")}.");
		if (!string.IsNullOrWhiteSpace(deed.SiteId)
			&& !string.Equals(global::SideEventDirector.ForStage(deed.StageId)?.SiteId, deed.SiteId, StringComparison.OrdinalIgnoreCase))
			warnings.Add($"Achievement '{id}' mirrors site '{deed.SiteId}', which is not the side event of its chapter '{deed.StageId}'.");
		if (!string.IsNullOrWhiteSpace(deed.SecretId)
			&& SecretCatalog.Get(deed.SecretId)?.StageId.Equals(deed.StageId, StringComparison.OrdinalIgnoreCase) != true)
			warnings.Add($"Achievement '{id}' mirrors secret '{deed.SecretId}', which is not a secret of its chapter '{deed.StageId}'.");

		// The measure and the fact agree: recording the fact a derived deed mirrors completes it.
		if (mirrors)
		{
			var save = new SaveData();
			AchievementManager.Complete(save, id);
			if (!deed.IsComplete(AchievementContext.ForSave(save)) || !GlobalStatsManager.IsAchievementUnlocked(save, id))
				warnings.Add($"Achievement '{id}': completing it in the run does not satisfy its own measure, so the page and the save would disagree.");
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

	/// <summary>
	/// Every evolution has its own icon (EvolutionIcons; Eric, 2026-10-28: "mutations and ascensions
	/// should all have their own unique altered icons"): every level 4 and level 8 option of every spell
	/// in ContentValidator.SpellResourcePaths, and every Apotheosis form, has an imported picture at
	/// EvolutionIcons.PathFor(id). A missing one still plays - the card falls back to its spell's icon
	/// in the branch's tint, which is exactly what the request was about - so it is reported here, where
	/// a new card nobody drew is caught the first time the game starts. The fix is always the same:
	/// `_AscensionAudit -- --json=tools/art/evolution_catalog.json`, `python tools/art/evolution_icons.py`,
	/// then `--import`.
	/// </summary>
	private static void ValidateEvolutionIcons(List<string> warnings)
	{
		var missing = new List<string>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		void Check(string id)
		{
			if (!string.IsNullOrWhiteSpace(id) && seen.Add(id) && !EvolutionIcons.Exists(id))
				missing.Add(id);
		}

		foreach (SpellData template in ContentValidator.LoadAllSpellForms())
		{
			if (template == null || string.IsNullOrWhiteSpace(template.Id))
				continue;
			// A copy, so filling the cards in here never changes the shared resource a run will load. (A
			// form is already a copy; copying it again is harmless.)
			var spell = (SpellData)template.Duplicate(true);
			SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
			foreach (SpellEvolutionOption option in spell.Level4Options ?? new Godot.Collections.Array<SpellEvolutionOption>())
				Check(option?.Id);
			foreach (SpellEvolutionOption option in spell.Level8Options ?? new Godot.Collections.Array<SpellEvolutionOption>())
				Check(option?.Id);
		}
		foreach (ApotheosisDefinition definition in ApotheosisCatalog.All)
			Check(definition.Form?.Id);

		if (seen.Count == 0)
			warnings.Add("ValidateEvolutionIcons found no evolutions to check - ContentValidator.LoadAllSpells returned none.");
		if (missing.Count > 0)
			warnings.Add($"{missing.Count} of {seen.Count} evolutions have no icon of their own under {EvolutionIcons.Directory} "
				+ $"and show their spell's tinted icon instead: {string.Join(", ", missing)}. Run _AscensionAudit --json, "
				+ "tools/art/evolution_icons.py, then --import.");
	}
}
