using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

/// <summary>How a secret is worked: the interaction vocabulary of .ai/stage-secrets.md section 6.</summary>
public enum SecretVerb
{
	Touch,
	Circle,
	Linger,
	Strike,
	Cull,
	Carry,
	Sequence,
	Follow,
	Hour,
	Guarded,
	/// <summary>Touch every one of a set, in any order, within a run (.ai/achievements-and-secrets.md 5).</summary>
	Gather,
	/// <summary>Only enemies that walk into a zone count; he stands at the edge and lets them come (5).</summary>
	Lure,
}

/// <summary>
/// What a secret pays once ever, on the save. Never a wizard or a boon - there is no value for one,
/// and that is the rule (.ai/achievements-and-secrets.md 6.2, revising .ai/stage-secrets.md section 1).
/// </summary>
public enum SecretMetaReward
{
	/// <summary>A mark on the Secrets page, and nothing else. Most secrets.</summary>
	PageOnly,
	/// <summary>One found level of a comfort stat (issue #20), stacking above the shop's cap.</summary>
	FoundLevel,
	/// <summary>A one-time Arcane Energy lump.</summary>
	ArcaneEnergy,
	/// <summary>
	/// A spell, once ever, through the secret's derived deed (<see cref="SecretCatalog.AsAchievement"/>):
	/// granted on the save, never put into the run, and offered from the next level-up. A spell vault
	/// only, one a chapter (.ai/achievements-and-secrets.md 6.2 and 6.4).
	/// </summary>
	Spell,
}

/// <summary>
/// Where the run puts a secret, at stage start, from the terrain seed (.ai/stage-secrets.md section 4;
/// .ai/achievements-and-secrets.md 6.4). The SecretDirector does the placing for every kind but
/// <see cref="Custom"/>, so a chapter's secret is a catalog entry and a node, and never an edit to the
/// director.
/// </summary>
public enum SecretPlacement
{
	/// <summary>
	/// The secret puts itself in the world through <see cref="SecretDefinition.Install"/> - the Fenced
	/// Bloom rides every flower pen as it streams in. (A secret that is not built is never placed, whatever
	/// its placement says: the director only reads <see cref="SecretCatalog.ForStage"/>.)
	/// </summary>
	Custom,
	/// <summary>The chapter's guaranteed near secret: three to four chunks out, its bearing at least 90
	/// degrees from the side event's. Stream 9.</summary>
	Anchor,
	/// <summary>The chapter's keepsake: ten to twelve chunks out; at least 90 degrees from the anchor's
	/// bearing when there is one, and on the seed's own bearing when not (the First Tree). Stream 7.</summary>
	Keepsake,
	/// <summary>A spell vault (.ai/achievements-and-secrets.md 6.4): five to seven chunks out, at least 90
	/// degrees from the side event's and the keepsake's bearings, in every run of its chapter. Stream 8.</summary>
	Vault,
}

/// <summary>One stage secret: what it is called, where it lives, how it is worked and what it pays.</summary>
/// <remarks>A record so a chapter can build on A1's written-down vault with <c>with</c> (<see cref="SecretCatalog"/>'s Vault).</remarks>
public sealed record SecretDefinition
{
	public required string Id { get; init; }
	/// <summary>A StageCatalog id - the chapter it belongs to.</summary>
	public required string StageId { get; init; }
	public required string DisplayName { get; init; }
	/// <summary>The line on the Secrets page and under the name when it is found: what it was.</summary>
	public required string FoundLine { get; init; }
	/// <summary>
	/// What the player saw, once its near tell has been on screen (.ai/achievements-and-secrets.md 4.3):
	/// an observation, never an instruction. The achievements page shows it, with the name, between
	/// sealed and found.
	/// </summary>
	public string SeenLine { get; init; } = "";
	public required SecretVerb Verb { get; init; }
	/// <summary>The chapter's rarest and farthest secret, and the only kind that pays beyond the page.</summary>
	public bool IsKeepsake { get; init; }
	/// <summary>
	/// A spell vault (.ai/achievements-and-secrets.md 6.4): in every run of its chapter, five to seven
	/// chunks out, large, with a far tell - and it pays one spell, once ever, through its deed.
	/// </summary>
	public bool IsVault { get; init; }
	/// <summary>What finding it does in the run, in words for the toast.</summary>
	public required string RunReward { get; init; }
	/// <summary>
	/// The toast's line when it is found again in a later run, if that pays something else - a vault
	/// whose spell is already on the save pays a chest (6.4). Empty: <see cref="RunReward"/> again.
	/// </summary>
	public string RepeatReward { get; init; } = "";
	public SecretMetaReward MetaReward { get; init; } = SecretMetaReward.PageOnly;
	/// <summary>For FoundLevel: the ArcaneUpgrades id (vitality, recovery, magnet, movespeed).</summary>
	public string MetaRewardId { get; init; } = "";
	/// <summary>For ArcaneEnergy: how much. 150 at most.</summary>
	public int MetaAmount { get; init; }
	/// <summary>
	/// For a vault (<see cref="SecretMetaReward.Spell"/>): the spell its deed grants. UnlockCatalog
	/// sources that spell from this secret's deed; RegressionChecks holds the two to each other.
	/// </summary>
	public string SpellUnlockId { get; init; } = "";
	/// <summary>The spell's display name, for the deed's reward line. Checked against the spell's resource.</summary>
	public string SpellName { get; init; } = "";
	/// <summary>
	/// False while it exists only in the catalog: written down, with its deed and its reward, but not
	/// yet in the world (the chapter agents of .ai/achievements-and-secrets.md section 9 build them).
	/// The pattern of a chapter's IsPlayable: an unbuilt secret is left out of <see cref="SecretCatalog.All"/>,
	/// so no screen counts or names it, and its deed is evaluated but not listed.
	/// </summary>
	public bool IsBuilt { get; init; } = true;

	// --- How it gets into a run (the SecretDirector reads these; nothing else does) ---------------

	/// <summary>How the director puts it in the world. A built secret needs a placement and a site, or an Install.</summary>
	public SecretPlacement Placement { get; init; } = SecretPlacement.Custom;
	/// <summary>
	/// Open ground kept round the site: the placer tests it, and decor stays out of it. A vault's is
	/// large (6.4: the size of the flower pen), so the place it stands in is a place to fight.
	/// </summary>
	public float Footprint { get; init; } = 160f;
	/// <summary>
	/// The node an Anchor, Keepsake or Vault is, made once a run: the director sets its id, its director
	/// and its position, hands it the placer to lay out anything else it needs (a path, a set of shards)
	/// and adds it to the run. Never called outside a run, so the validators never build a node.
	/// </summary>
	public Func<global::SecretSite> CreateSite { get; init; }
	/// <summary>For <see cref="SecretPlacement.Custom"/>: run once at stage start, to hook the secret into the world.</summary>
	public Action<global::SecretDirector> Install { get; init; }
}

/// <summary>
/// Every stage secret, and nothing defined anywhere else (.ai/stage-secrets.md section 7.5) - like
/// UnlockCatalog and ApotheosisCatalog. RegressionChecks.ValidateSecretCatalog holds it to the rules.
/// </summary>
/// <remarks>
/// ONE FILE PER CHAPTER (piece A4 of .ai/achievements-and-secrets.md, section 9). Each chapter's secrets
/// are written in its own partial, <c>SecretCatalog.&lt;Chapter&gt;.cs</c>, in a nested class with a
/// <c>Build()</c> - the StageBestiary pattern - so the agents building chapters in parallel each edit
/// their own file and never this one. A chapter's file holds its rows of section 8 and nothing else:
/// the entry, and through <see cref="SecretDefinition.CreateSite"/> the node that is the secret in the
/// world. <c>.ai/stage-secrets.md</c> section 13 is the recipe.
///
/// Built so far: the Enchanted Forest's Fenced Bloom and First Tree (issue #20, 2026-10-28), and the
/// Sunken Cave's Geode Heart, the first spell vault (2026-10-30). The other eight vaults are written
/// down (piece A1) with their deeds, words and spells, unbuilt; each chapter's agent builds its vault and
/// flips IsBuilt.
/// </remarks>
public static partial class SecretCatalog
{
	/// <summary>The four stats a found level may raise: comfort, never what the Apotheosis floor was
	/// measured with at cap (.ai/stage-secrets.md section 3).</summary>
	public static readonly IReadOnlyList<string> FoundLevelStats = new[] { "vitality", "recovery", "magnet", "movespeed" };

	/// <summary>The most Arcane Energy one keepsake may pay.</summary>
	public const int MaxKeepsakeArcane = 150;

	private static SecretDefinition[] defined;
	private static SecretDefinition[] built;

	/// <summary>
	/// Every secret written down, built or not, chapter by chapter. The deeds are derived from this list.
	/// This is the one line in the catalog that names every chapter, written once: adding a chapter's
	/// secrets never changes it. Only a chapter added to StageCatalog adds its file and a line here.
	/// </summary>
	/// <remarks>Built on first use, each chapter a nested class of its own: C# does not define the order of
	/// static field initialisers across the files of a partial class (StageBestiary's reason).</remarks>
	public static IReadOnlyList<SecretDefinition> Defined => defined ??= new[]
	{
		EnchantedForest.Build(),
		CursedDungeon.Build(),
		SunkenCave.Build(),
		BlightedSwamp.Build(),
		MysticRuins.Build(),
		FrozenWaste.Build(),
		ScorchedSands.Build(),
		Emberdeep.Build(),
		Sketchbook.Build(),
		FallowFields.Build(),
		BrokenCircle.Build(),
		// The castle (.ai/endgame-castle.md); the Cursed Dungeon keeps its own file when it moves in.
		Bridge.Build(),
		Court.Build(),
		Throne.Build(),
	}.SelectMany(chapter => chapter).ToArray();

	/// <summary>Every secret in the world: the built ones. What the screens and the world map count.</summary>
	public static IReadOnlyList<SecretDefinition> All => built ??= Defined.Where(s => s.IsBuilt).ToArray();

	/// <summary>
	/// A spell vault, as A1 wrote the nine down (.ai/achievements-and-secrets.md 2.1, 6.4 and section 8):
	/// its spell through its deed, the run's toast line, and - unbuilt - no place in the world. A chapter
	/// that builds its vault writes it with <c>with</c>: <c>Vault(...) with { IsBuilt = true, CreateSite =
	/// () => new MyVault(), Footprint = ... }</c>. The seen lines say what the near tell shows and never
	/// what to do; the found lines say what it was and what opened it.
	/// </summary>
	private static SecretDefinition Vault(string id, string stageId, string name, SecretVerb verb,
		string spellId, string spellName, string seen, string found) => new()
	{
		Id = id,
		StageId = stageId,
		DisplayName = name,
		FoundLine = found,
		SeenLine = seen,
		Verb = verb,
		IsVault = true,
		RunReward = $"{spellName} can be offered from your next level-up",
		// Found once, it still works (6.4): later runs pay its run reward and a chest from the budget.
		RepeatReward = "It still gives up a chest",
		MetaReward = SecretMetaReward.Spell,
		SpellUnlockId = spellId,
		SpellName = spellName,
		IsBuilt = false,
		Placement = SecretPlacement.Vault,
		Footprint = VaultFootprint,
	};

	/// <summary>A vault's default footprint: the place it stands in is a place to fight (6.4).</summary>
	public const float VaultFootprint = 420f;

	/// <summary>A secret by id, built or planned.</summary>
	public static SecretDefinition Get(string id) =>
		Defined.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

	/// <summary>The chapter's secrets that are in the world.</summary>
	public static IEnumerable<SecretDefinition> ForStage(string stageId) =>
		All.Where(s => s.StageId.Equals(stageId, StringComparison.OrdinalIgnoreCase));

	public static bool StageHas(string stageId, string secretId) =>
		ForStage(stageId).Any(s => s.Id.Equals(secretId, StringComparison.OrdinalIgnoreCase));

	/// <summary>
	/// The secret as an achievement (.ai/achievements-and-secrets.md 2.6): a view of it, not a second
	/// definition. Hidden until seen, completed the moment it is found, measured by the save's found
	/// list. It carries the vault's spell and nothing else: a keepsake's found level and Arcane lump are
	/// paid by SecretDirector.Found on the first find, never by the deed, so a deed reconciled or
	/// completed twice can never pay them twice.
	/// </summary>
	public static AchievementDefinition AsAchievement(SecretDefinition secret)
	{
		string id = secret.Id;
		return new AchievementDefinition
		{
			Id = AchievementDefinitions.SecretDeedId(id),
			DisplayName = secret.DisplayName,
			Description = secret.FoundLine,
			RewardText = RewardTextFor(secret),
			SpellUnlockId = secret.MetaReward == SecretMetaReward.Spell ? secret.SpellUnlockId ?? "" : "",
			Category = AchievementCategory.Exploration,
			Kind = AchievementKind.Secret,
			Visibility = AchievementVisibility.Hidden,
			CompletesInRun = true,
			StageId = secret.StageId,
			SecretId = id,
			SeenLine = secret.SeenLine,
			SealedLine = $"Hidden somewhere in {AchievementDefinitions.ChapterPhrase(secret.StageId)}",
			Measure = c => c.Save != null && c.Save.HasFoundSecret(id) ? 1f : 0f,
			Target = 1,
		};
	}

	private static string RewardTextFor(SecretDefinition secret) => secret.MetaReward switch
	{
		SecretMetaReward.Spell => $"Frees {secret.SpellName}",
		SecretMetaReward.PageOnly => "A mark on the page",
		_ => Capitalised(DescribeMeta(secret)),
	};

	private static string Capitalised(string text) =>
		string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

	/// <summary>What finding it pays on the save, in words, for the toast and the page.</summary>
	public static string DescribeMeta(SecretDefinition secret) => secret.MetaReward switch
	{
		SecretMetaReward.FoundLevel => $"+1 found level of {StatName(secret.MetaRewardId)}, for good",
		SecretMetaReward.ArcaneEnergy => $"+{secret.MetaAmount} Arcane Energy",
		SecretMetaReward.Spell => $"{secret.SpellName}, for good",
		_ => "",
	};

	private static string StatName(string upgradeId) => upgradeId switch
	{
		"vitality" => "Vitality (+5 max HP)",
		"recovery" => "Recovery (+0.4 HP/s)",
		"magnet" => "Magnet (+20 pickup radius)",
		"movespeed" => "Move Speed (+5%)",
		_ => upgradeId,
	};
}
