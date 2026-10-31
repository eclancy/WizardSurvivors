namespace WizardSurvivors.scripts;

public static partial class SecretCatalog
{
	/// <summary>
	/// Chapter 3, the Broken Circle (stage_10): .ai/achievements-and-secrets.md 8.3. Its side event is the
	/// moot (follow the runes).
	/// </summary>
	/// <remarks>
	/// Built: BC1 the Fallen Star, the vault (gather, on SecretGather, then linger; 2026-10-30). To build:
	/// BC2 the Hare's Form, BC3 the Shepherd's Bothy and BC4 the Speaker's Stone (the keepsake - sequence).
	/// </remarks>
	private static class BrokenCircle
	{
		private const string Stage = "stage_10";

		internal static SecretDefinition[] Build() => new[]
		{
			// Gather seven shards strewn along a dead-straight burn, in any order, then linger 2 s in the
			// crater it leads to (FallenStar.cs). Nothing solid: he stands in the crater.
			Vault(FallenStarId, Stage, "The Fallen Star", SecretVerb.Gather, "starfall", "Starfall",
				seen: "A line of burnt heather running dead straight across the heath, glinting at intervals.",
				found: "A star that fell on the heath and broke. Its seven shards gathered, the crater gave up its light.") with
			{
				IsBuilt = true,
				CreateSite = () => new global::FallenStar(),
				RepeatReward = "Its shards gathered again, the crater gave up a chest",
			},
		};
	}

	public const string FallenStarId = "fallen_star";
}
