namespace WizardSurvivors.scripts;

public static partial class SecretCatalog
{
	public const string GeodeHeartId = "geode_heart";

	/// <summary>
	/// Chapter 4, the Sunken Cave (stage_2): .ai/achievements-and-secrets.md 8.4. No side event.
	/// </summary>
	/// <remarks>
	/// To build: C2 the Geode Heart (the vault - strike, on Breakable), C1 Cold Lamps (the anchor), C3 the
	/// Choir Loft (the keepsake - untouched), C4 the Overlook and C5 the Blind Fish (follow, on SecretMover).
	/// </remarks>
	private static class SunkenCave
	{
		private const string Stage = "stage_2";

		internal static SecretDefinition[] Build() => new[]
		{
			Vault(GeodeHeartId, Stage, "The Geode Heart", SecretVerb.Strike, "frost_shard", "Frost Shard",
				seen: "A crystal three men tall, grown up through the cavern floor. The small ones near it are cracking.",
				found: "A crystal grown round a light he missed. Fighting at its foot broke it open."),
		};
	}
}
