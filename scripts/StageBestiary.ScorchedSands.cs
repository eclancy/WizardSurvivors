namespace WizardSurvivors.scripts;

public static partial class StageBestiary
{
	/// <summary>
	/// Chapter 7, the Scorched Sands (stage_6): the caravan dead and the desert's scavengers. Boss:
	/// the Long Coil.
	/// </summary>
	/// <remarks>
	/// "He hid the water. The dead still walk the trade road." The porters, guards and travellers of a
	/// caravan that never reached water, and what feeds on them. The heavy is also the caravan master:
	/// CaravanMasterEvent asks for this chapter's Heavy, so the walker on the road is a laden porter
	/// wearing the elite treatment. Designed in .ai/bestiary.md; every slot stands in with the Cursed
	/// Dungeon's creature until its own is drawn.
	/// </remarks>
	private static class ScorchedSands
	{
		private const string Stage = "stage_6";

		internal static readonly BestiaryCreature Thirsting =
			Creature(Stage, "sands_thirsting", "The Thirsting", "res://scenes/SandsThirsting.tscn", "SandsThirsting");
		internal static readonly BestiaryCreature Vulture =
			Creature(Stage, "sands_vulture", "Bone Vulture", "res://scenes/SandsVulture.tscn", "SandsVulture");
		internal static readonly BestiaryCreature Porter =
			Creature(Stage, "sands_porter", "Laden Porter", "res://scenes/SandsPorter.tscn", "SandsPorter");
		internal static readonly BestiaryCreature CaravanGuard =
			Creature(Stage, "sands_caravan_guard", "Caravan Guard", "res://scenes/SandsCaravanGuard.tscn", "SandsCaravanGuard");
		internal static readonly BestiaryCreature Oracle =
			Creature(Stage, "sands_oracle", "Dune Oracle", "res://scenes/SandsOracle.tscn", "SandsOracle");
		internal static readonly BestiaryCreature Ballista =
			Creature(Stage, "sands_ballista", "Bone Ballista", "res://scenes/SandsBallista.tscn", "SandsBallista");
		internal static readonly BestiaryCreature Scorpion =
			Creature(Stage, "sands_scorpion", "Scorpion", "res://scenes/SandsScorpion.tscn", "SandsScorpion");
		internal static readonly BestiaryCreature SwollenCask =
			Creature(Stage, "sands_swollen_cask", "Swollen Cask", "res://scenes/SandsSwollenCask.tscn", "SandsSwollenCask");
		internal static readonly BestiaryCreature Flailbearer =
			Creature(Stage, "sands_flailbearer", "Flailbearer", "res://scenes/SandsFlailbearer.tscn", "SandsFlailbearer");
		internal static readonly BestiaryCreature CarrionCaller =
			Creature(Stage, "sands_carrion_caller", "Carrion Caller", "res://scenes/SandsCarrionCaller.tscn", "SandsCarrionCaller");
		internal static readonly BestiaryCreature TollKeeper =
			Creature(Stage, "sands_toll_keeper", "The Toll-Keeper", "res://scenes/SandsTollKeeper.tscn", "SandsTollKeeper");

		internal static ChapterBestiary Build()
		{
			BestiarySlot bulk = Planned(BestiaryRole.Bulk, Thirsting, CursedDungeon.Shrouded);
			BestiarySlot fast = Planned(BestiaryRole.Fast, Vulture, CursedDungeon.GraveRunner);
			BestiarySlot heavy = Planned(BestiaryRole.Heavy, Porter, CursedDungeon.Shieldbearer);
			BestiarySlot wall = Planned(BestiaryRole.Wall, CaravanGuard, CursedDungeon.BarrowKnight);

			return new ChapterBestiary
			{
				StageId = Stage,
				Slots = new[]
				{
					bulk, fast, heavy, wall,
					Planned(BestiaryRole.Ranged, Oracle, CursedDungeon.Hexer),
					Planned(BestiaryRole.Turret, Ballista, CursedDungeon.SkullSentry),
					Planned(BestiaryRole.Charger, Scorpion, CursedDungeon.CryptLunger),
					Planned(BestiaryRole.Exploder, SwollenCask, CursedDungeon.GraveBloat),
					Planned(BestiaryRole.Slammer, Flailbearer, CursedDungeon.Gravebreaker),
					Planned(BestiaryRole.Summoner, CarrionCaller, CursedDungeon.BoneCaller),
					Planned(BestiaryRole.MiniBoss, TollKeeper, CursedDungeon.Warden),
				},
				// The old Desert case, band for band.
				Table = new[]
				{
					Band(fast, 0.40f, 0.75f),
					Band(bulk, 0.32f, 1.0f),
					Band(wall, 0.18f, 1.4f),
					Band(heavy, 0.10f, 2.2f),
				},
			};
		}
	}
}
