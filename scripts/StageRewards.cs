using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

/// <summary>What a slot on a chapter's card is.</summary>
public enum StageRewardKind
{
	Wizard,
	Spell,
	Boon,
	ArcaneEnergy,
	Secret,
	Keepsake,
}

/// <summary>How the chapter pays it.</summary>
public enum StageRewardSource
{
	/// <summary>The chapter's boss falls (the deed whose <c>BossId</c> is the chapter's boss).</summary>
	Warden,
	/// <summary>The chapter's side event is won (a <c>Discovery</c> entry at the event's site).</summary>
	SideEvent,
	/// <summary>A deed done in the chapter (an unlock whose <c>RevealStageId</c> is the chapter).</summary>
	Deed,
	/// <summary>One of the chapter's stage secrets (<see cref="SecretCatalog"/>).</summary>
	Secret,
}

/// <summary>One thing a chapter holds.</summary>
public sealed class StageReward
{
	public string StageId { get; init; } = string.Empty;
	public StageRewardKind Kind { get; init; }
	public StageRewardSource Source { get; init; }

	/// <summary>Spell, boon or character id; the secret's id; for Arcane Energy, the deed's id.</summary>
	public string Id { get; init; } = string.Empty;

	/// <summary>The deed that pays it, when a deed does (the warden's, the coursing).</summary>
	public string DeedId { get; init; } = string.Empty;

	/// <summary>Arcane Energy paid, for <see cref="StageRewardKind.ArcaneEnergy"/>.</summary>
	public int Amount { get; init; }

	public override string ToString() => $"{StageId}:{Kind}:{Id}";
}

/// <summary>
/// What each chapter holds - the single authority the world map's card reads (.ai/world-map.md,
/// section 9). It invents nothing: every entry is derived from the catalog that already owns it, so
/// a chapter's card cannot drift from what the chapter actually pays.
/// </summary>
/// <remarks>
/// Four sources, in this order on a card:
///   1. the warden: the deed whose <see cref="AchievementDefinition.BossId"/> is the chapter's boss
///      (<see cref="BossCatalog.ForStageIndex"/>), and whatever it grants - a spell, a boon, a wizard,
///      or Arcane Energy (the Sketchbook's);
///   2. the side event: <see cref="SideEventDirector.ForStage"/>'s site, and every
///      <see cref="UnlockSource.Discovery"/> entry at it (<see cref="UnlockCatalog.ForDiscoverySite"/>);
///   3. a deed done there: an unlock whose <see cref="UnlockDefinition.RevealStageId"/> is the chapter
///      (Laelaps, freed by coursing hares on the Broken Circle);
///   4. its secrets and its keepsake (<see cref="SecretCatalog.ForStage"/>).
/// RegressionChecks.ValidateStageRewards holds every one of those catalogs to this list, and runs a
/// prose net over the unlock hints, so an unlock tied to a chapter in words but not in data is caught.
/// </remarks>
public static class StageRewards
{
	private static List<StageReward> all;

	public static IReadOnlyList<StageReward> All => all ??= Build();

	public static IReadOnlyList<StageReward> ForStage(string stageId) =>
		All.Where(r => r.StageId.Equals(stageId ?? string.Empty, StringComparison.OrdinalIgnoreCase)).ToList();

	/// <summary>The chapter's deed for its boss, or null (a chapter whose boss pays nothing has none).</summary>
	public static AchievementDefinition WardenDeed(StageDefinition stage)
	{
		BossDefinition boss = stage != null ? BossCatalog.ForStageIndex(stage.Index) : null;
		return boss == null ? null : AchievementDefinitions.All.FirstOrDefault(
			d => d.BossId.Equals(boss.Id, StringComparison.OrdinalIgnoreCase));
	}

	private static List<StageReward> Build()
	{
		var result = new List<StageReward>();
		foreach (StageDefinition stage in StageCatalog.All)
		{
			var mine = new List<StageReward>();
			void Add(StageReward reward)
			{
				// One slot per thing: Tituba is both the Dungeon's side event and its reveal chapter.
				if (!mine.Any(r => r.Kind == reward.Kind && r.Id.Equals(reward.Id, StringComparison.OrdinalIgnoreCase)))
					mine.Add(reward);
			}

			AchievementDefinition warden = WardenDeed(stage);
			if (warden != null)
			{
				if (!string.IsNullOrWhiteSpace(warden.CharacterUnlockId))
					Add(new StageReward { StageId = stage.Id, Kind = StageRewardKind.Wizard, Source = StageRewardSource.Warden, Id = warden.CharacterUnlockId, DeedId = warden.Id });
				if (!string.IsNullOrWhiteSpace(warden.SpellUnlockId))
					Add(new StageReward { StageId = stage.Id, Kind = StageRewardKind.Spell, Source = StageRewardSource.Warden, Id = warden.SpellUnlockId, DeedId = warden.Id });
				if (!string.IsNullOrWhiteSpace(warden.BoonUnlockId))
					Add(new StageReward { StageId = stage.Id, Kind = StageRewardKind.Boon, Source = StageRewardSource.Warden, Id = warden.BoonUnlockId, DeedId = warden.Id });
				if (warden.CurrencyReward > 0)
					Add(new StageReward { StageId = stage.Id, Kind = StageRewardKind.ArcaneEnergy, Source = StageRewardSource.Warden, Id = warden.Id, DeedId = warden.Id, Amount = warden.CurrencyReward });
			}

			string site = global::SideEventDirector.ForStage(stage.Id)?.SiteId;
			if (!string.IsNullOrWhiteSpace(site))
			{
				foreach (UnlockDefinition found in UnlockCatalog.ForDiscoverySite(site))
					Add(new StageReward { StageId = stage.Id, Kind = KindOf(found.Kind), Source = StageRewardSource.SideEvent, Id = found.Id });
			}

			foreach (UnlockDefinition revealed in UnlockCatalog.All.Where(d =>
				d.RevealStageId.Equals(stage.Id, StringComparison.OrdinalIgnoreCase)))
			{
				Add(new StageReward
				{
					StageId = stage.Id,
					Kind = KindOf(revealed.Kind),
					Source = revealed.Source == UnlockSource.Discovery ? StageRewardSource.SideEvent : StageRewardSource.Deed,
					Id = revealed.Id,
					DeedId = revealed.Source == UnlockSource.Achievement ? revealed.SourceId : string.Empty,
				});
			}

			foreach (SecretDefinition secret in SecretCatalog.ForStage(stage.Id))
				Add(new StageReward
				{
					StageId = stage.Id,
					Kind = secret.IsKeepsake ? StageRewardKind.Keepsake : StageRewardKind.Secret,
					Source = StageRewardSource.Secret,
					Id = secret.Id,
				});

			// People first, then what they carried, then what was hidden - and the keepsake last.
			result.AddRange(mine.OrderBy(r => (int)r.Kind));
		}
		return result;
	}

	private static StageRewardKind KindOf(UnlockKind kind) => kind switch
	{
		UnlockKind.Character => StageRewardKind.Wizard,
		UnlockKind.Boon => StageRewardKind.Boon,
		_ => StageRewardKind.Spell,
	};

	/// <summary>Whether the save has this one: owned, found, or (for Energy) its deed done.</summary>
	public static bool IsFound(SaveData save, StageReward reward)
	{
		if (reward == null)
			return false;
		switch (reward.Kind)
		{
			case StageRewardKind.Wizard:
				return GlobalStatsManager.IsCharacterUnlocked(save, reward.Id);
			case StageRewardKind.Spell:
				return GlobalStatsManager.IsSpellUnlockedForLevelUp(save, reward.Id);
			case StageRewardKind.Boon:
				return GlobalStatsManager.IsBoonUnlocked(save, reward.Id);
			case StageRewardKind.Secret:
			case StageRewardKind.Keepsake:
				return save != null && save.HasFoundSecret(reward.Id);
			case StageRewardKind.ArcaneEnergy:
				if (GlobalStatsManager.IsAchievementUnlocked(save, reward.DeedId))
					return true;
				AchievementDefinition deed = AchievementDefinitions.GetById(reward.DeedId);
				return deed != null && save != null && deed.IsComplete(AchievementContext.ForSave(save));
			default:
				return false;
		}
	}

	/// <summary>
	/// True for a wizard the card may not even show as a silhouette: a secret whose reveal chapter is
	/// not open yet (the character select's rule, <see cref="GlobalStatsManager.IsCharacterRevealed"/>).
	/// </summary>
	public static bool IsSealed(SaveData save, StageReward reward) =>
		reward != null && reward.Kind == StageRewardKind.Wizard && !GlobalStatsManager.IsCharacterRevealed(save, reward.Id);

	/// <summary>
	/// The words for a slot's tooltip. A found thing is named; an unfound spell, boon or wizard says
	/// how it is found; an unfound secret says only that it is one (stage-secrets.md 7.6: a list of
	/// named blanks is a checklist), and a sealed wizard only that someone is held here.
	/// </summary>
	public static string Describe(SaveData save, StageReward reward, Func<StageReward, string> nameOf)
	{
		if (reward == null)
			return string.Empty;
		bool found = IsFound(save, reward);
		switch (reward.Kind)
		{
			case StageRewardKind.Secret:
				return found ? SecretCatalog.Get(reward.Id)?.DisplayName ?? "A secret" : "A secret, not yet found";
			case StageRewardKind.Keepsake:
				return found ? SecretCatalog.Get(reward.Id)?.DisplayName ?? "A keepsake" : "A keepsake, not yet found";
			case StageRewardKind.ArcaneEnergy:
				return $"{reward.Amount} Arcane Energy" + (found ? "" : $" - {AchievementDefinitions.GetById(reward.DeedId)?.Description}");
			case StageRewardKind.Wizard:
				if (found)
					return nameOf?.Invoke(reward) ?? reward.Id;
				return IsSealed(save, reward) ? "Someone is held here" : UnlockCatalog.GetLockedHint(reward.Id, UnlockKind.Character);
			case StageRewardKind.Boon:
				return found ? nameOf?.Invoke(reward) ?? reward.Id : UnlockCatalog.GetLockedHint(reward.Id, UnlockKind.Boon);
			default:
				return found ? nameOf?.Invoke(reward) ?? reward.Id : UnlockCatalog.GetLockedHint(reward.Id, UnlockKind.Spell);
		}
	}
}
