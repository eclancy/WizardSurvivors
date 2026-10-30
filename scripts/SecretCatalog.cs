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

/// <summary>One stage secret: what it is called, where it lives, how it is worked and what it pays.</summary>
public sealed class SecretDefinition
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
}

/// <summary>
/// Every stage secret, and nothing defined anywhere else (.ai/stage-secrets.md section 7.5) - like
/// UnlockCatalog and ApotheosisCatalog. RegressionChecks.ValidateSecretCatalog holds it to the rules.
/// </summary>
/// <remarks>
/// Built so far (issue #20, 2026-10-28): the Enchanted Forest's first secret and its keepsake. The
/// Fenced Bloom is worked by walking a lap round a flower pen and drops a seed; the First Tree, far
/// out, takes the seed and pays the first found level.
///
/// Written down but not built (2026-10-30, piece A1 of .ai/achievements-and-secrets.md): the nine spell
/// vaults. Each pays a spell the shop or a Feat used to, so every spell has an achievement behind it
/// from the day the unlock rule changes, and each has a derived deed (<see cref="AsAchievement"/>) the
/// unlock catalog can name. Piece A4 splits this file into one partial per chapter; the vaults move
/// into their chapters' files then, and each chapter's agent builds its vault and flips IsBuilt.
/// </remarks>
public static class SecretCatalog
{
	public const string FencedBloomId = "fenced_bloom";
	public const string FirstTreeId = "first_tree";

	/// <summary>The four stats a found level may raise: comfort, never what the Apotheosis floor was
	/// measured with at cap (.ai/stage-secrets.md section 3).</summary>
	public static readonly IReadOnlyList<string> FoundLevelStats = new[] { "vitality", "recovery", "magnet", "movespeed" };

	/// <summary>The most Arcane Energy one keepsake may pay.</summary>
	public const int MaxKeepsakeArcane = 150;

	/// <summary>Every secret written down, built or not. The deeds are derived from this list.</summary>
	public static readonly IReadOnlyList<SecretDefinition> Defined = new[]
	{
		new SecretDefinition
		{
			Id = FencedBloomId,
			StageId = "stage_0",
			DisplayName = "The Fenced Bloom",
			FoundLine = "A moonflower somebody kept, behind a fence. Walk all the way round it and it opens.",
			SeenLine = "A walled garden in the wood, and in it one pale flower that turns to follow you.",
			Verb = SecretVerb.Circle,
			RunReward = "Healed by half, and the garden heals you while you stand in it",
		},
		new SecretDefinition
		{
			Id = FirstTreeId,
			StageId = "stage_0",
			DisplayName = "The First Tree",
			FoundLine = "A dead clearing far out in the wood, waiting for a seed from the moonflower.",
			SeenLine = "A dead clearing ringed with old stumps, and in the middle a bed of turned earth with nothing in it.",
			Verb = SecretVerb.Carry,
			IsKeepsake = true,
			RunReward = "Fully healed",
			MetaReward = SecretMetaReward.FoundLevel,
			MetaRewardId = "vitality",
		},

		// THE SPELL VAULTS (.ai/achievements-and-secrets.md 2.1, 6.4 and section 8), one per chapter but
		// the Emberdeep. Planned: in the catalog, with deeds, not yet in the world. The seen lines say what
		// the near tell shows and never what to do; the found lines say what it was and what opened it.
		Vault("pale_hart", "stage_0", "The Pale Hart", SecretVerb.Follow, "hunters_draw", "Hunter's Draw",
			seen: "A hart of warm light at the edge of a clearing, the only warm thing that moves and is not you. It is watching you.",
			found: "A hart of light that led you, a stand at a time, to an old hunter's high seat. A bow of light hung there."),
		Vault("pinfold", "stage_9", "The Pinfold", SecretVerb.Guarded, "familiar", "Familiar",
			seen: "A walled stone pound with a barred gate. Behind the bars a hound of warm light lies with its head on its paws.",
			found: "The Hayward's pound, and a stray he kept in it. Its guards fell, the latch lifted, and the hound ran to you."),
		Vault("fallen_star", "stage_10", "The Fallen Star", SecretVerb.Gather, "starfall", "Starfall",
			seen: "A line of burnt heather running dead straight across the heath, glinting at intervals.",
			found: "A star that fell on the heath and broke. Its seven shards gathered, the crater gave up its light."),
		Vault("geode_heart", "stage_2", "The Geode Heart", SecretVerb.Strike, "frost_shard", "Frost Shard",
			seen: "A crystal three men tall, grown up through the cavern floor. The small ones near it are cracking.",
			found: "A crystal grown round a light he missed. Fighting at its foot broke it open."),
		Vault("cold_forge", "stage_1", "The Cold Forge", SecretVerb.Cull, "molten_shard", "Molten Shard",
			seen: "A dead smithy in the gaol: a cold hearth, an anvil, and a warm glint on the anvil's horn.",
			found: "The forge that made the shackles. Every kill on its hearth set an ember in the coals until it roared."),
		Vault("swallow_hole", "stage_3", "The Swallow-Hole", SecretVerb.Lure, "gravewell", "Gravewell",
			seen: "A sinkhole of black mud in the bog. The reeds lean toward it, and something glints at the bottom.",
			found: "A hole in the bog that swallows what walks into it. Thirty of his went in, and it gave up what it held."),
		Vault("broken_ward", "stage_4", "The Broken Ward", SecretVerb.Sequence, "kindled_ward", "Kindled Ward",
			seen: "Five ward stones in a ring, each cut with a different number of notches, all of them dark.",
			found: "A ward broken from the inside. Touched in the order of their notches, its stones stood again, and its light came to you."),
		// Stillness is a linger with no place but where he stands: the drift settles round his feet.
		Vault("buried_runes", "stage_5", "The Buried Runes", SecretVerb.Linger, "sleet_runes", "Sleet Runes",
			seen: "A drift field where the snow never stops blowing, except round anyone who stands still.",
			found: "Rune-stones under a drift. Standing still let the snow settle off them, and the three were dug out."),
		// The storm is a kind of hour: a cycle of its own on the pace clock, with a build you can see.
		Vault("fulgurite", "stage_6", "The Fulgurite", SecretVerb.Hour, "stormsweep", "Stormsweep",
			seen: "A branching tree of glass standing in the dunes, under a sky darker over it than anywhere else.",
			found: "Glass the lightning left in the sand. You stood by it through the dry storm, and it drew the last strike into itself."),
	};

	/// <summary>Every secret in the world: the built ones. What the screens and the world map count.</summary>
	public static readonly IReadOnlyList<SecretDefinition> All = Defined.Where(s => s.IsBuilt).ToArray();

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
		MetaReward = SecretMetaReward.Spell,
		SpellUnlockId = spellId,
		SpellName = spellName,
		IsBuilt = false,
	};

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
