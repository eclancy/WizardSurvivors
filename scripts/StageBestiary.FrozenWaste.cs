namespace WizardSurvivors.scripts;

public static partial class StageBestiary
{
	/// <summary>
	/// Chapter 6, the Frozen Waste (stage_5): the frozen north and its beasts. Boss: the Still Warden.
	/// </summary>
	/// <remarks>
	/// "A wizard lies under the ice, and the cold stands guard." The thanes, sled hounds and drum
	/// callers of the north, frozen and taken. The Rime Guard, the Still Warden's halberdier, is this
	/// chapter's already: it fills no slot because only the boss calls it up, so it is listed as an
	/// attendant for the ownership check. Designed in .ai/bestiary.md; every slot stands in with the
	/// Cursed Dungeon's creature until its own is drawn.
	/// </remarks>
	private static class FrozenWaste
	{
		private const string Stage = "stage_5";

		internal static readonly BestiaryCreature RimeGuard =
			Creature(Stage, "frost_rime_guard", "Rime Guard", "res://scenes/RimeGuard.tscn", "RimeGuard");

		internal static readonly BestiaryCreature Rimebound =
			Creature(Stage, "frost_rimebound", "Rimebound", "res://scenes/FrostRimebound.tscn", "FrostRimebound");
		internal static readonly BestiaryCreature SledHound =
			Creature(Stage, "frost_sled_hound", "Sled Hound", "res://scenes/FrostSledHound.tscn", "FrostSledHound");
		internal static readonly BestiaryCreature FloeBearer =
			Creature(Stage, "frost_floe_bearer", "Floe-Bearer", "res://scenes/FrostFloeBearer.tscn", "FrostFloeBearer");
		internal static readonly BestiaryCreature Thane =
			Creature(Stage, "frost_thane", "Frost Thane", "res://scenes/FrostThane.tscn", "FrostThane");
		internal static readonly BestiaryCreature RimeSeer =
			Creature(Stage, "frost_rime_seer", "Rime Seer", "res://scenes/FrostRimeSeer.tscn", "FrostRimeSeer");
		internal static readonly BestiaryCreature Totem =
			Creature(Stage, "frost_totem", "Whalebone Totem", "res://scenes/FrostTotem.tscn", "FrostTotem");
		internal static readonly BestiaryCreature IceRam =
			Creature(Stage, "frost_ice_ram", "Ice Ram", "res://scenes/FrostIceRam.tscn", "FrostIceRam");
		internal static readonly BestiaryCreature Frostburst =
			Creature(Stage, "frost_frostburst", "Frostburst", "res://scenes/FrostFrostburst.tscn", "FrostFrostburst");
		internal static readonly BestiaryCreature Hailbringer =
			Creature(Stage, "frost_hailbringer", "Hailbringer", "res://scenes/FrostHailbringer.tscn", "FrostHailbringer");
		internal static readonly BestiaryCreature DrumCaller =
			Creature(Stage, "frost_drum_caller", "Drum-Caller", "res://scenes/FrostDrumCaller.tscn", "FrostDrumCaller");
		internal static readonly BestiaryCreature Frostkeeper =
			Creature(Stage, "frost_frostkeeper", "The Frostkeeper", "res://scenes/FrostFrostkeeper.tscn", "FrostFrostkeeper");

		internal static ChapterBestiary Build()
		{
			BestiarySlot bulk = Planned(BestiaryRole.Bulk, Rimebound, CursedDungeon.Shrouded);
			BestiarySlot fast = Planned(BestiaryRole.Fast, SledHound, CursedDungeon.GraveRunner);
			BestiarySlot heavy = Planned(BestiaryRole.Heavy, FloeBearer, CursedDungeon.Shieldbearer);
			BestiarySlot wall = Planned(BestiaryRole.Wall, Thane, CursedDungeon.BarrowKnight);

			return new ChapterBestiary
			{
				StageId = Stage,
				Slots = new[]
				{
					bulk, fast, heavy, wall,
					Planned(BestiaryRole.Ranged, RimeSeer, CursedDungeon.Hexer),
					Planned(BestiaryRole.Turret, Totem, CursedDungeon.SkullSentry),
					Planned(BestiaryRole.Charger, IceRam, CursedDungeon.CryptLunger),
					Planned(BestiaryRole.Exploder, Frostburst, CursedDungeon.GraveBloat),
					Planned(BestiaryRole.Slammer, Hailbringer, CursedDungeon.Gravebreaker),
					Planned(BestiaryRole.Summoner, DrumCaller, CursedDungeon.BoneCaller),
					Planned(BestiaryRole.MiniBoss, Frostkeeper, CursedDungeon.Warden),
				},
				// The old Ice case, band for band.
				Table = new[]
				{
					Band(fast, 0.36f, 0.75f),
					Band(bulk, 0.32f, 1.0f),
					Band(wall, 0.20f, 1.4f),
					Band(heavy, 0.12f, 2.2f),
				},
				Attendants = new[] { RimeGuard },
			};
		}
	}
}
