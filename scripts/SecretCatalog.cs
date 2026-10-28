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
}

/// <summary>What a secret pays once ever, on the save. Never a spell, a wizard or a boon - there is
/// no value for one, and that is the rule (.ai/stage-secrets.md section 1).</summary>
public enum SecretMetaReward
{
	/// <summary>A mark on the Secrets page, and nothing else. Most secrets.</summary>
	PageOnly,
	/// <summary>One found level of a comfort stat (issue #20), stacking above the shop's cap.</summary>
	FoundLevel,
	/// <summary>A one-time Arcane Energy lump.</summary>
	ArcaneEnergy,
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
	public required SecretVerb Verb { get; init; }
	/// <summary>The chapter's rarest and farthest secret, and the only kind that pays beyond the page.</summary>
	public bool IsKeepsake { get; init; }
	/// <summary>What finding it does in the run, in words for the toast.</summary>
	public required string RunReward { get; init; }
	public SecretMetaReward MetaReward { get; init; } = SecretMetaReward.PageOnly;
	/// <summary>For FoundLevel: the ArcaneUpgrades id (vitality, recovery, magnet, movespeed).</summary>
	public string MetaRewardId { get; init; } = "";
	/// <summary>For ArcaneEnergy: how much. 150 at most.</summary>
	public int MetaAmount { get; init; }
}

/// <summary>
/// Every stage secret, and nothing defined anywhere else (.ai/stage-secrets.md section 7.5) - like
/// UnlockCatalog and ApotheosisCatalog. RegressionChecks.ValidateSecretCatalog holds it to the rules.
/// </summary>
/// <remarks>
/// Built so far (issue #20, 2026-10-28): the Enchanted Forest's first secret and its keepsake. The
/// Fenced Bloom is worked by walking a lap round a flower pen and drops a seed; the First Tree, far
/// out, takes the seed and pays the first found level. The rest of section 6 is still a plan.
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

	public static readonly IReadOnlyList<SecretDefinition> All = new[]
	{
		new SecretDefinition
		{
			Id = FencedBloomId,
			StageId = "stage_0",
			DisplayName = "The Fenced Bloom",
			FoundLine = "A moonflower somebody kept, behind a fence. Walk all the way round it and it opens.",
			Verb = SecretVerb.Circle,
			RunReward = "Healed by half, and the garden heals you while you stand in it",
		},
		new SecretDefinition
		{
			Id = FirstTreeId,
			StageId = "stage_0",
			DisplayName = "The First Tree",
			FoundLine = "A dead clearing far out in the wood, waiting for a seed from the moonflower.",
			Verb = SecretVerb.Carry,
			IsKeepsake = true,
			RunReward = "Fully healed",
			MetaReward = SecretMetaReward.FoundLevel,
			MetaRewardId = "vitality",
		},
	};

	public static SecretDefinition Get(string id) =>
		All.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

	public static IEnumerable<SecretDefinition> ForStage(string stageId) =>
		All.Where(s => s.StageId.Equals(stageId, StringComparison.OrdinalIgnoreCase));

	public static bool StageHas(string stageId, string secretId) =>
		ForStage(stageId).Any(s => s.Id.Equals(secretId, StringComparison.OrdinalIgnoreCase));

	/// <summary>What finding it pays on the save, in words, for the toast and the page.</summary>
	public static string DescribeMeta(SecretDefinition secret) => secret.MetaReward switch
	{
		SecretMetaReward.FoundLevel => $"+1 found level of {StatName(secret.MetaRewardId)}, for good",
		SecretMetaReward.ArcaneEnergy => $"+{secret.MetaAmount} Arcane Energy",
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
