namespace WizardSurvivors.scripts;

public static partial class SecretCatalog
{
	public const string FencedBloomId = "fenced_bloom";
	public const string FirstTreeId = "first_tree";

	/// <summary>
	/// Chapter 1, the Enchanted Forest (stage_0): .ai/achievements-and-secrets.md 8.1. Its side event is
	/// the grove (stand still), so no long holds here.
	/// </summary>
	/// <remarks>
	/// Built: F1 the Fenced Bloom and F4 the First Tree, its keepsake (issue #20). To build: F5 the Pale
	/// Hart (the vault - follow, on SecretMover), F2 the Fairy Ring, F3 the Outward Path, F6 the Struck
	/// Oak (strike, on Breakable) and F7 the Woodcutter's Clearing.
	/// </remarks>
	private static class EnchantedForest
	{
		private const string Stage = "stage_0";

		internal static SecretDefinition[] Build() => new[]
		{
			new SecretDefinition
			{
				Id = FencedBloomId,
				StageId = Stage,
				DisplayName = "The Fenced Bloom",
				FoundLine = "A moonflower somebody kept, behind a fence. Walk all the way round it and it opens.",
				SeenLine = "A walled garden in the wood, and in it one pale flower that turns to follow you.",
				Verb = SecretVerb.Circle,
				RunReward = "Healed by half, and the garden heals you while you stand in it",
				// Every flower pen is one, as it streams in: no forced anchor (.ai/stage-secrets.md 12).
				Install = director => director.HookLandmarks("flower_pen", "FencedBloom",
					landmark => new global::FencedBloom { Site = landmark }),
			},
			new SecretDefinition
			{
				Id = FirstTreeId,
				StageId = Stage,
				DisplayName = "The First Tree",
				FoundLine = "A dead clearing far out in the wood, waiting for a seed from the moonflower.",
				SeenLine = "A dead clearing ringed with old stumps, and in the middle a bed of turned earth with nothing in it.",
				Verb = SecretVerb.Carry,
				IsKeepsake = true,
				RunReward = "Fully healed",
				MetaReward = SecretMetaReward.FoundLevel,
				MetaRewardId = "vitality",
				Placement = SecretPlacement.Keepsake,
				Footprint = global::FirstTree.Footprint,
				CreateSite = () => new global::FirstTree(),
			},
			Vault("pale_hart", Stage, "The Pale Hart", SecretVerb.Follow, "hunters_draw", "Hunter's Draw",
				seen: "A hart of warm light at the edge of a clearing, the only warm thing that moves and is not you. It is watching you.",
				found: "A hart of light that led you, a stand at a time, to an old hunter's high seat. A bow of light hung there."),
		};
	}
}
