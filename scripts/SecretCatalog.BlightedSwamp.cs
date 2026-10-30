namespace WizardSurvivors.scripts;

public static partial class SecretCatalog
{
	/// <summary>
	/// The Blighted Swamp (stage_3): .ai/achievements-and-secrets.md 8.6. Its side event is the bog
	/// lanterns (explore).
	/// </summary>
	/// <remarks>
	/// To build: S5 the Swallow-Hole (the vault - lure, on SecretLure), S1 Lily Crossing, S2 the Living
	/// Lantern, S3 the Warm Wisp (follow, on SecretMover), S4 the Sunken Chapel (the keepsake) and S6 the
	/// Peat Bank (strike, on Breakable).
	/// </remarks>
	private static class BlightedSwamp
	{
		private const string Stage = "stage_3";

		internal static SecretDefinition[] Build() => new[]
		{
			Vault("swallow_hole", Stage, "The Swallow-Hole", SecretVerb.Lure, "gravewell", "Gravewell",
				seen: "A sinkhole of black mud in the bog. The reeds lean toward it, and something glints at the bottom.",
				found: "A hole in the bog that swallows what walks into it. Thirty of his went in, and it gave up what it held."),
		};
	}
}
