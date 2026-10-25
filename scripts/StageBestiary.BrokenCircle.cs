namespace WizardSurvivors.scripts;

public static partial class StageBestiary
{
	/// <summary>
	/// Chapter 3, the Broken Circle (stage_10): the heath where the wizards met, and its people.
	/// Boss: the Ninth Stone.
	/// </summary>
	/// <remarks>
	/// "The circle where the wizards met. He came here first." The shepherds, wallers and warreners
	/// of the high heath, the stewards who kept the moot for the wizards, its bulls, adders and black
	/// hares - taken - and the iron rod he drove into the hill. Designed in .ai/campaign.md; every slot
	/// stands in with the Cursed Dungeon's creature until its own is drawn.
	///
	/// THE STEP BETWEEN THE FIELDS AND THE CAVE. Mean health multiplier 1.06 to pace 5:00, then 1.11 -
	/// a late table like the Cave's, so the third chapter leans toward the fourth without reaching
	/// the Cave's own 1.18. No extra bands, nothing tankier than the shared heavy.
	/// </remarks>
	private static class BrokenCircle
	{
		private const string Stage = "stage_10";

		internal static readonly BestiaryCreature Shepherd =
			Creature(Stage, "heath_shepherd", "The Shepherd", "res://scenes/HeathShepherd.tscn", "HeathShepherd");
		internal static readonly BestiaryCreature BlackHare =
			Creature(Stage, "heath_black_hare", "Black Hare", "res://scenes/HeathBlackHare.tscn", "HeathBlackHare");
		internal static readonly BestiaryCreature Waller =
			Creature(Stage, "heath_waller", "The Waller", "res://scenes/HeathWaller.tscn", "HeathWaller");
		internal static readonly BestiaryCreature Bull =
			Creature(Stage, "heath_bull", "Heath Bull", "res://scenes/HeathBull.tscn", "HeathBull");
		internal static readonly BestiaryCreature Steward =
			Creature(Stage, "heath_steward", "Moot-Steward", "res://scenes/HeathSteward.tscn", "HeathSteward");
		internal static readonly BestiaryCreature ThunderRod =
			Creature(Stage, "heath_thunder_rod", "Thunder Rod", "res://scenes/HeathThunderRod.tscn", "HeathThunderRod");
		internal static readonly BestiaryCreature Adder =
			Creature(Stage, "heath_adder", "The Adder", "res://scenes/HeathAdder.tscn", "HeathAdder");
		internal static readonly BestiaryCreature FleeceBale =
			Creature(Stage, "heath_fleece_bale", "Fleece Bale", "res://scenes/HeathFleeceBale.tscn", "HeathFleeceBale");
		internal static readonly BestiaryCreature FireBeater =
			Creature(Stage, "heath_fire_beater", "Fire-Beater", "res://scenes/HeathFireBeater.tscn", "HeathFireBeater");
		internal static readonly BestiaryCreature Warrener =
			Creature(Stage, "heath_warrener", "The Warrener", "res://scenes/HeathWarrener.tscn", "HeathWarrener");
		// His jailer at the moot: the one who spoke the law to the wizards' council.
		internal static readonly BestiaryCreature Lawspeaker =
			Creature(Stage, "heath_lawspeaker", "The Lawspeaker", "res://scenes/HeathLawspeaker.tscn", "HeathLawspeaker");

		internal static ChapterBestiary Build()
		{
			BestiarySlot bulk = Planned(BestiaryRole.Bulk, Shepherd, CursedDungeon.Shrouded);
			BestiarySlot fast = Planned(BestiaryRole.Fast, BlackHare, CursedDungeon.GraveRunner);
			BestiarySlot heavy = Planned(BestiaryRole.Heavy, Waller, CursedDungeon.Shieldbearer);
			BestiarySlot wall = Planned(BestiaryRole.Wall, Bull, CursedDungeon.BarrowKnight);

			return new ChapterBestiary
			{
				StageId = Stage,
				Slots = new[]
				{
					bulk, fast, heavy, wall,
					Planned(BestiaryRole.Ranged, Steward, CursedDungeon.Hexer),
					Planned(BestiaryRole.Turret, ThunderRod, CursedDungeon.SkullSentry),
					Planned(BestiaryRole.Charger, Adder, CursedDungeon.CryptLunger),
					Planned(BestiaryRole.Exploder, FleeceBale, CursedDungeon.GraveBloat),
					Planned(BestiaryRole.Slammer, FireBeater, CursedDungeon.Gravebreaker),
					Planned(BestiaryRole.Summoner, Warrener, CursedDungeon.BoneCaller),
					Planned(BestiaryRole.MiniBoss, Lawspeaker, CursedDungeon.Warden),
				},
				// 0.48 + 0.28 x 0.75 + 0.20 x 1.4 + 0.04 x 2.2 = 1.058.
				Table = new[]
				{
					Band(bulk, 0.48f, 1.0f),
					Band(fast, 0.28f, 0.75f),
					Band(wall, 0.20f, 1.4f),
					Band(heavy, 0.04f, 2.2f),
				},
				// 0.42 + 0.28 x 0.75 + 0.22 x 1.4 + 0.08 x 2.2 = 1.114.
				LateTable = new[]
				{
					Band(bulk, 0.42f, 1.0f),
					Band(fast, 0.28f, 0.75f),
					Band(wall, 0.22f, 1.4f),
					Band(heavy, 0.08f, 2.2f),
				},
				LateTableFromPaceMinutes = 5.0f,
			};
		}
	}
}
