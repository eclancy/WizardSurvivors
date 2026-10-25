namespace WizardSurvivors.scripts;

public static partial class StageBestiary
{
	/// <summary>
	/// Chapter 8, the Emberdeep (stage_7): what he built, in his own seat. Boss: the Warden of the Deep.
	/// </summary>
	/// <remarks>
	/// "His seat, and the end of the road." The one chapter whose cast was MADE rather than taken, so
	/// the one where every creature burns instead of staring - the art contract's single carve-out
	/// (built things carry fire, taken things carry his cold mark) becomes the rule here, and says
	/// whose house this is before the last boss does. Designed in .ai/bestiary.md; every slot stands in
	/// with the Cursed Dungeon's creature until its own is drawn.
	/// </remarks>
	private static class Emberdeep
	{
		private const string Stage = "stage_7";

		internal static readonly BestiaryCreature SlagThrall =
			Creature(Stage, "ember_slag_thrall", "Slag Thrall", "res://scenes/EmberSlagThrall.tscn", "EmberSlagThrall");
		internal static readonly BestiaryCreature Chainhound =
			Creature(Stage, "ember_chainhound", "Chainhound", "res://scenes/EmberChainhound.tscn", "EmberChainhound");
		internal static readonly BestiaryCreature Bulwark =
			Creature(Stage, "ember_bulwark", "Iron Bulwark", "res://scenes/EmberBulwark.tscn", "EmberBulwark");
		internal static readonly BestiaryCreature KilnBrute =
			Creature(Stage, "ember_kiln_brute", "Kiln Brute", "res://scenes/EmberKilnBrute.tscn", "EmberKilnBrute");
		internal static readonly BestiaryCreature Kindler =
			Creature(Stage, "ember_kindler", "The Kindler", "res://scenes/EmberKindler.tscn", "EmberKindler");
		internal static readonly BestiaryCreature FlueMask =
			Creature(Stage, "ember_flue_mask", "Flue Mask", "res://scenes/EmberFlueMask.tscn", "EmberFlueMask");
		internal static readonly BestiaryCreature Ironjaw =
			Creature(Stage, "ember_ironjaw", "Ironjaw", "res://scenes/EmberIronjaw.tscn", "EmberIronjaw");
		internal static readonly BestiaryCreature Bellows =
			Creature(Stage, "ember_bellows", "Burst Bellows", "res://scenes/EmberBellows.tscn", "EmberBellows");
		internal static readonly BestiaryCreature TripHammer =
			Creature(Stage, "ember_trip_hammer", "Trip-Hammer", "res://scenes/EmberTripHammer.tscn", "EmberTripHammer");
		internal static readonly BestiaryCreature Kennelmaster =
			Creature(Stage, "ember_kennelmaster", "Kennelmaster", "res://scenes/EmberKennelmaster.tscn", "EmberKennelmaster");
		internal static readonly BestiaryCreature Chainwright =
			Creature(Stage, "ember_chainwright", "The Chainwright", "res://scenes/EmberChainwright.tscn", "EmberChainwright");

		internal static ChapterBestiary Build()
		{
			BestiarySlot bulk = Planned(BestiaryRole.Bulk, SlagThrall, CursedDungeon.Shrouded);
			BestiarySlot fast = Planned(BestiaryRole.Fast, Chainhound, CursedDungeon.GraveRunner);
			BestiarySlot heavy = Planned(BestiaryRole.Heavy, Bulwark, CursedDungeon.Shieldbearer);
			BestiarySlot wall = Planned(BestiaryRole.Wall, KilnBrute, CursedDungeon.BarrowKnight);

			return new ChapterBestiary
			{
				StageId = Stage,
				Slots = new[]
				{
					bulk, fast, heavy, wall,
					Planned(BestiaryRole.Ranged, Kindler, CursedDungeon.Hexer),
					Planned(BestiaryRole.Turret, FlueMask, CursedDungeon.SkullSentry),
					Planned(BestiaryRole.Charger, Ironjaw, CursedDungeon.CryptLunger),
					Planned(BestiaryRole.Exploder, Bellows, CursedDungeon.GraveBloat),
					Planned(BestiaryRole.Slammer, TripHammer, CursedDungeon.Gravebreaker),
					Planned(BestiaryRole.Summoner, Kennelmaster, CursedDungeon.BoneCaller),
					Planned(BestiaryRole.MiniBoss, Chainwright, CursedDungeon.Warden),
				},
				// The old Volcanic case, band for band.
				Table = new[]
				{
					Band(heavy, 0.24f, 2.2f),
					Band(bulk, 0.38f, 1.0f),
					Band(fast, 0.24f, 0.75f),
					Band(wall, 0.14f, 1.4f),
				},
			};
		}
	}
}
