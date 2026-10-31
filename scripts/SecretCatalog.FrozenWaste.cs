namespace WizardSurvivors.scripts;

public static partial class SecretCatalog
{
	public const string BuriedRunesId = "buried_runes";

	/// <summary>
	/// The Frozen Waste (stage_5): .ai/achievements-and-secrets.md 8.8. Its side event is the thaw (fight
	/// near), so no strike here: that is the thaw's.
	/// </summary>
	/// <remarks>
	/// Built: I2 the Buried Runes, the spell vault (2026-10-30) - stillness, then touch. To build: I1
	/// Footprints (the anchor - follow, then linger), I3 the Watch-Fire (the keepsake - guarded) and I4 the
	/// Ice Window (circle).
	/// </remarks>
	private static class FrozenWaste
	{
		private const string Stage = "stage_5";

		internal static SecretDefinition[] Build() => new[]
		{
			// Stillness is a linger with no place but where he stands: the drift settles round his feet, the
			// stones within 400 px show, and he digs all three (BuriedRunes.cs).
			Vault(BuriedRunesId, Stage, "The Buried Runes", SecretVerb.Linger, "sleet_runes", "Sleet Runes",
				seen: "A drift field where the snow never stops blowing, except round anyone who stands still.",
				found: "Rune-stones under a drift. Standing still let the snow settle off them, and the three were dug out.") with
			{
				IsBuilt = true,
				CreateSite = () => new global::BuriedRunes(),
				RepeatReward = "The snow settles off them again, and gives up a chest",
			},
		};
	}
}
