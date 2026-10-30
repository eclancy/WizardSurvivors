namespace WizardSurvivors.scripts;

public static partial class SecretCatalog
{
	/// <summary>
	/// The Mystic Ruins (stage_4): .ai/achievements-and-secrets.md 8.7. No side event.
	/// </summary>
	/// <remarks>
	/// To build: R1 the Broken Ward (the vault - sequence), R2 the Misfiled Page (follow, on SecretMover),
	/// R3 the Sundial Vault (the keepsake - hour), R4 the Statue's Eye (strike, on Breakable) and R5 the
	/// Lecture Hall (cull).
	/// </remarks>
	private static class MysticRuins
	{
		private const string Stage = "stage_4";

		internal static SecretDefinition[] Build() => new[]
		{
			Vault("broken_ward", Stage, "The Broken Ward", SecretVerb.Sequence, "kindled_ward", "Kindled Ward",
				seen: "Five ward stones in a ring, each cut with a different number of notches, all of them dark.",
				found: "A ward broken from the inside. Touched in the order of their notches, its stones stood again, and its light came to you."),
		};
	}
}
