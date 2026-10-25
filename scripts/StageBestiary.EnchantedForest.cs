namespace WizardSurvivors.scripts;

public static partial class StageBestiary
{
	/// <summary>
	/// Chapter 1, the Enchanted Forest (stage_0): evil forest creatures. Boss: Elderbark, the Treant.
	/// </summary>
	/// <remarks>
	/// The first chapter with a cast of its own: the dire wolf, the bramble and the wisp
	/// (tools/art/foes_forest.py). Wolves are the bulk of it and they are FAST and light - the lesson
	/// of this wood is that you cannot outrun it - brambles are the wall you go around, and wisps are
	/// the back line. Everything else here was a crypt creature walking about under the trees; each now
	/// has a forest creature designed for its job, and stands in for it until that one is drawn.
	///
	/// No heavy. The forest table has never carried a 2.2x band, and adding one would make the
	/// opening chapter harder; its weight is the bramble wall and the wolves' numbers.
	/// </remarks>
	private static class EnchantedForest
	{
		private const string Stage = "stage_0";

		internal static readonly BestiaryCreature DireWolf =
			Creature(Stage, "forest_dire_wolf", "Dire Wolf", "res://scenes/ForestWolf.tscn", "ForestWolf");
		internal static readonly BestiaryCreature Bramble =
			Creature(Stage, "forest_bramble", "Bramble", "res://scenes/ForestBramble.tscn", "ForestBramble");
		internal static readonly BestiaryCreature Wisp =
			Creature(Stage, "forest_wisp", "Wisp", "res://scenes/ForestWisp.tscn", "ForestWisp");

		// Designed, not drawn yet (.ai/bestiary.md, the Enchanted Forest).
		internal static readonly BestiaryCreature Leafwight =
			Creature(Stage, "forest_leafwight", "Leafwight", "res://scenes/ForestLeafwight.tscn", "ForestLeafwight");
		internal static readonly BestiaryCreature Beater =
			Creature(Stage, "forest_beater", "Hollow Beater", "res://scenes/ForestBeater.tscn", "ForestBeater");
		internal static readonly BestiaryCreature Dryad =
			Creature(Stage, "forest_dryad", "Hollowed Dryad", "res://scenes/ForestDryad.tscn", "ForestDryad");
		internal static readonly BestiaryCreature Effigy =
			Creature(Stage, "forest_effigy", "Wicker Effigy", "res://scenes/ForestEffigy.tscn", "ForestEffigy");
		internal static readonly BestiaryCreature Gloamstag =
			Creature(Stage, "forest_gloamstag", "Gloamstag", "res://scenes/ForestGloamstag.tscn", "ForestGloamstag");
		internal static readonly BestiaryCreature Burstpod =
			Creature(Stage, "forest_burstpod", "Burstpod", "res://scenes/ForestBurstpod.tscn", "ForestBurstpod");
		internal static readonly BestiaryCreature HollowBear =
			Creature(Stage, "forest_hollow_bear", "Hollow Bear", "res://scenes/ForestHollowBear.tscn", "ForestHollowBear");
		internal static readonly BestiaryCreature Huntsman =
			Creature(Stage, "forest_huntsman", "The Huntsman", "res://scenes/ForestHuntsman.tscn", "ForestHuntsman");
		internal static readonly BestiaryCreature Gamekeeper =
			Creature(Stage, "forest_gamekeeper", "The Gamekeeper", "res://scenes/ForestGamekeeper.tscn", "ForestGamekeeper");

		internal static ChapterBestiary Build()
		{
			BestiarySlot bulk = Planned(BestiaryRole.Bulk, Leafwight, CursedDungeon.Shrouded);
			BestiarySlot fast = Planned(BestiaryRole.Fast, Beater, CursedDungeon.GraveRunner);
			BestiarySlot wall = Own(BestiaryRole.Wall, Bramble);
			BestiarySlot wolves = Own(BestiaryRole.Extra, DireWolf);
			BestiarySlot wisps = Own(BestiaryRole.Extra, Wisp);

			return new ChapterBestiary
			{
				StageId = Stage,
				Slots = new[]
				{
					bulk, fast,
					Absent(BestiaryRole.Heavy, "the forest table has never carried a heavy band; its weight is the bramble wall and the wolves' numbers"),
					wall, wolves, wisps,
					Planned(BestiaryRole.Ranged, Dryad, CursedDungeon.Hexer),
					Planned(BestiaryRole.Turret, Effigy, CursedDungeon.SkullSentry),
					Planned(BestiaryRole.Charger, Gloamstag, CursedDungeon.CryptLunger),
					Planned(BestiaryRole.Exploder, Burstpod, CursedDungeon.GraveBloat),
					Planned(BestiaryRole.Slammer, HollowBear, CursedDungeon.Gravebreaker),
					Planned(BestiaryRole.Summoner, Huntsman, CursedDungeon.BoneCaller),
					Planned(BestiaryRole.MiniBoss, Gamekeeper, CursedDungeon.Warden),
				},
				// The old Forest case, band for band.
				Table = new[]
				{
					Band(wolves, 0.40f, 0.85f),
					Band(wall, 0.26f, 1.5f),
					Band(wisps, 0.14f, 0.8f),
					Band(bulk, 0.12f, 1.0f),
					Band(fast, 0.08f, 0.75f),
				},
			};
		}
	}
}
