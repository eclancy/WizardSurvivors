namespace WizardSurvivors.scripts;

public static partial class SecretCatalog
{
	/// <summary>
	/// The Cursed Dungeon (stage_1), in the castle now: .ai/achievements-and-secrets.md 8.5. Its side event
	/// is the Gaoler's key (carry). The maze: every site snaps to a cell, and no secret ever moves a wall.
	/// </summary>
	/// <remarks>
	/// Built: D5 the Cold Forge, the vault (cull; 2026-10-30, in the Dungeon as it is now - the move into
	/// the castle changes nothing about it). To build: D1 Chalk Marks, D2 the Alarm Bell, D3 the Hollow
	/// Flagstone (strike, on Breakable), D4 the Gaoler's Ledger (the keepsake - guarded), D6 the Empty Cell
	/// and D7 the Oubliette.
	/// </remarks>
	private static class CursedDungeon
	{
		private const string Stage = "stage_1";

		internal static SecretDefinition[] Build() => new[]
		{
			// Cull: forty kills inside the hearth ring in front of the anvil light the coals until the forge
			// roars (ColdForge.cs). Moved to the nearest hall room; the hearth is solid ground.
			Vault(ColdForgeId, Stage, "The Cold Forge", SecretVerb.Cull, "molten_shard", "Molten Shard",
				seen: "A dead smithy in the gaol: a cold hearth, an anvil, and a warm glint on the anvil's horn.",
				found: "The forge that made the shackles. Every kill on its hearth set an ember in the coals until it roared.") with
			{
				IsBuilt = true,
				CreateSite = () => new global::ColdForge(),
				RepeatReward = "It roared again, and gave up a chest",
			},
		};
	}

	public const string ColdForgeId = "cold_forge";
}
