namespace WizardSurvivors.scripts;

public static partial class SecretCatalog
{
	public const string SwallowHoleId = "swallow_hole";

	/// <summary>
	/// The Blighted Swamp (stage_3): .ai/achievements-and-secrets.md 8.6. Its side event is the bog
	/// lanterns (explore).
	/// </summary>
	/// <remarks>
	/// Built: S5 the Swallow-Hole, the spell vault (2026-10-30) - lure, on SecretLure. To build: S1 Lily
	/// Crossing, S2 the Living Lantern, S3 the Warm Wisp (follow, on SecretMover), S4 the Sunken Chapel (the
	/// keepsake) and S6 the Peat Bank (strike, on Breakable).
	/// </remarks>
	private static class BlightedSwamp
	{
		private const string Stage = "stage_3";

		internal static SecretDefinition[] Build() => new[]
		{
			// Lure: thirty ordinary bodies walked into its mud fill it, and it gives up the light (SwallowHole.cs).
			// Not solid - the flow field runs straight through it - so it is a hazard to him instead.
			Vault(SwallowHoleId, Stage, "The Swallow-Hole", SecretVerb.Lure, "gravewell", "Gravewell",
				seen: "A sinkhole of black mud in the bog. The reeds lean toward it, and something glints at the bottom.",
				found: "A hole in the bog that swallows what walks into it. Thirty of his went in, and it gave up what it held.") with
			{
				IsBuilt = true,
				CreateSite = () => new global::SwallowHole(),
				RepeatReward = "It swallows thirty again, and gives up a chest",
			},
		};
	}
}
