namespace WizardSurvivors.scripts;

public static partial class SecretCatalog
{
	/// <summary>
	/// Chapter 2, the Fallow Fields (stage_9): .ai/achievements-and-secrets.md 8.2. Its side event is the
	/// well (defend).
	/// </summary>
	/// <remarks>
	/// To build: FF1 the Pinfold (the vault - guarded), FF2 the Last Sheaf, FF3 the Plough in the Furrow,
	/// FF4 the Tithe Barn (the keepsake - carry the sheaf) and FF5 the Scarecrow's Post.
	/// </remarks>
	private static class FallowFields
	{
		private const string Stage = "stage_9";

		internal static SecretDefinition[] Build() => new[]
		{
			Vault("pinfold", Stage, "The Pinfold", SecretVerb.Guarded, "familiar", "Familiar",
				seen: "A walled stone pound with a barred gate. Behind the bars a hound of warm light lies with its head on its paws.",
				found: "The Hayward's pound, and a stray he kept in it. Its guards fell, the latch lifted, and the hound ran to you."),
		};
	}
}
