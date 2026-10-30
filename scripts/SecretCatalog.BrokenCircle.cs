namespace WizardSurvivors.scripts;

public static partial class SecretCatalog
{
	/// <summary>
	/// Chapter 3, the Broken Circle (stage_10): .ai/achievements-and-secrets.md 8.3. Its side event is the
	/// moot (follow the runes).
	/// </summary>
	/// <remarks>
	/// To build: BC1 the Fallen Star (the vault - gather, on SecretGather, then linger), BC2 the Hare's
	/// Form, BC3 the Shepherd's Bothy and BC4 the Speaker's Stone (the keepsake - sequence).
	/// </remarks>
	private static class BrokenCircle
	{
		private const string Stage = "stage_10";

		internal static SecretDefinition[] Build() => new[]
		{
			Vault("fallen_star", Stage, "The Fallen Star", SecretVerb.Gather, "starfall", "Starfall",
				seen: "A line of burnt heather running dead straight across the heath, glinting at intervals.",
				found: "A star that fell on the heath and broke. Its seven shards gathered, the crater gave up its light."),
		};
	}
}
