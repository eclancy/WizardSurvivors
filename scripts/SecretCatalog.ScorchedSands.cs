namespace WizardSurvivors.scripts;

public static partial class SecretCatalog
{
	public const string FulguriteId = "fulgurite";

	/// <summary>
	/// The Scorched Sands (stage_6): .ai/achievements-and-secrets.md 8.9. Its side event is the caravan
	/// master (catch).
	/// </summary>
	/// <remarks>
	/// Built: A5 the Fulgurite, the spell vault (2026-10-30) - storm, a kind of hour. To build: A1 the Buried
	/// Cart (strike, on Breakable), A2 Milestones, A3 the Hidden Water (the keepsake - linger) and A4 the
	/// Mirage.
	/// </remarks>
	private static class ScorchedSands
	{
		private const string Stage = "stage_6";

		internal static SecretDefinition[] Build() => new[]
		{
			// The storm is a kind of hour: a cycle of its own on the pace clock, with a build you can see. Stand
			// within 120 px of the glass through the six bolts and it draws the last into itself (Fulgurite.cs).
			Vault(FulguriteId, Stage, "The Fulgurite", SecretVerb.Hour, "stormsweep", "Stormsweep",
				seen: "A branching tree of glass standing in the dunes, under a sky darker over it than anywhere else.",
				found: "Glass the lightning left in the sand. You stood by it through the dry storm, and it drew the last strike into itself.") with
			{
				IsBuilt = true,
				CreateSite = () => new global::Fulgurite(),
				RepeatReward = "It draws the storm down again, and gives up a chest",
			},
		};
	}
}
