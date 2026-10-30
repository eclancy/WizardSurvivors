namespace WizardSurvivors.scripts;

public static partial class SecretCatalog
{
	/// <summary>
	/// The Scorched Sands (stage_6): .ai/achievements-and-secrets.md 8.9. Its side event is the caravan
	/// master (catch).
	/// </summary>
	/// <remarks>
	/// To build: A5 the Fulgurite (the vault - storm, a kind of hour), A1 the Buried Cart (strike, on
	/// Breakable), A2 Milestones, A3 the Hidden Water (the keepsake - linger) and A4 the Mirage.
	/// </remarks>
	private static class ScorchedSands
	{
		private const string Stage = "stage_6";

		internal static SecretDefinition[] Build() => new[]
		{
			// The storm is a kind of hour: a cycle of its own on the pace clock, with a build you can see.
			Vault("fulgurite", Stage, "The Fulgurite", SecretVerb.Hour, "stormsweep", "Stormsweep",
				seen: "A branching tree of glass standing in the dunes, under a sky darker over it than anywhere else.",
				found: "Glass the lightning left in the sand. You stood by it through the dry storm, and it drew the last strike into itself."),
		};
	}
}
