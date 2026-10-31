namespace WizardSurvivors.scripts;

public static partial class SecretCatalog
{
	public const string BrokenWardId = "broken_ward";

	/// <summary>
	/// The Mystic Ruins (stage_4): .ai/achievements-and-secrets.md 8.7. No side event.
	/// </summary>
	/// <remarks>
	/// Built: R1 the Broken Ward, the spell vault (2026-10-30) - sequence. To build: R2 the Misfiled Page
	/// (follow, on SecretMover), R3 the Sundial Vault (the keepsake - hour), R4 the Statue's Eye (strike, on
	/// Breakable) and R5 the Lecture Hall (cull).
	/// </remarks>
	private static class MysticRuins
	{
		private const string Stage = "stage_4";

		internal static SecretDefinition[] Build() => new[]
		{
			// Sequence: five stones touched in the order of their notches; the ward stands thirty seconds and
			// throws the horde back at its wall, then its light goes to him (BrokenWard.cs).
			Vault(BrokenWardId, Stage, "The Broken Ward", SecretVerb.Sequence, "kindled_ward", "Kindled Ward",
				seen: "Five ward stones in a ring, each cut with a different number of notches, all of them dark.",
				found: "A ward broken from the inside. Touched in the order of their notches, its stones stood again, and its light came to you.") with
			{
				IsBuilt = true,
				CreateSite = () => new global::BrokenWard(),
				RepeatReward = "The ward stands again for a while, and gives up a chest",
			},
		};
	}
}
