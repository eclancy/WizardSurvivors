namespace WizardSurvivors.scripts;

public static partial class SecretCatalog
{
	public const string GeodeHeartId = "geode_heart";

	/// <summary>
	/// Chapter 4, the Sunken Cave (stage_2): .ai/achievements-and-secrets.md 8.4. No side event.
	/// </summary>
	/// <remarks>
	/// Built: C2 the Geode Heart, the first spell vault (2026-10-30, piece A4's proof of the core; Eric's
	/// "giant crystal that needs to be destroyed to find a related spell"). To build: C1 Cold Lamps (the
	/// anchor), C3 the Choir Loft (the keepsake - untouched), C4 the Overlook and C5 the Blind Fish (follow,
	/// on SecretMover).
	/// </remarks>
	private static class SunkenCave
	{
		private const string Stage = "stage_2";

		internal static SecretDefinition[] Build() => new[]
		{
			// Strike, on Breakable: 2,000 landed near its foot breaks it in four stages, and the shatter
			// freezes the pocket. The run's toast says what it did; the deed grants Frost Shard (GeodeHeart.cs).
			Vault(GeodeHeartId, Stage, "The Geode Heart", SecretVerb.Strike, "frost_shard", "Frost Shard",
				seen: "A crystal three men tall, grown up through the cavern floor. The small ones near it are cracking.",
				found: "A crystal grown round a light he missed. Fighting at its foot broke it open.") with
			{
				IsBuilt = true,
				CreateSite = () => new global::GeodeHeart(),
				RepeatReward = "It shatters again, and gives up a chest",
			},
		};
	}
}
