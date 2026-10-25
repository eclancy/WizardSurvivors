namespace WizardSurvivors.scripts;

public static partial class StageBestiary
{
	/// <summary>
	/// Chapter 5, the Mystic Ruins (stage_4): the scholars and the stone that kept the wards. Boss:
	/// the Archivist.
	/// </summary>
	/// <remarks>
	/// "The old wards were broken from the inside." The readers, the statues and the guardians of a
	/// school of wards, turned on it - and one thing he built to break them. Designed in
	/// .ai/bestiary.md; every slot stands in with the Cursed Dungeon's creature until its own is drawn.
	/// </remarks>
	private static class MysticRuins
	{
		private const string Stage = "stage_4";

		internal static readonly BestiaryCreature Unread =
			Creature(Stage, "ruins_unread", "The Unread", "res://scenes/RuinsUnread.tscn", "RuinsUnread");
		internal static readonly BestiaryCreature Folio =
			Creature(Stage, "ruins_folio", "Loose Folio", "res://scenes/RuinsFolio.tscn", "RuinsFolio");
		internal static readonly BestiaryCreature WardedStatue =
			Creature(Stage, "ruins_warded_statue", "Warded Statue", "res://scenes/RuinsWardedStatue.tscn", "RuinsWardedStatue");
		internal static readonly BestiaryCreature Gargoyle =
			Creature(Stage, "ruins_gargoyle", "Gargoyle", "res://scenes/RuinsGargoyle.tscn", "RuinsGargoyle");
		internal static readonly BestiaryCreature Magister =
			Creature(Stage, "ruins_magister", "Hollow Magister", "res://scenes/RuinsMagister.tscn", "RuinsMagister");
		internal static readonly BestiaryCreature Obelisk =
			Creature(Stage, "ruins_obelisk", "Breach Obelisk", "res://scenes/RuinsObelisk.tscn", "RuinsObelisk");
		internal static readonly BestiaryCreature WardCat =
			Creature(Stage, "ruins_ward_cat", "Ward-Cat", "res://scenes/RuinsWardCat.tscn", "RuinsWardCat");
		internal static readonly BestiaryCreature CrackedUrn =
			Creature(Stage, "ruins_cracked_urn", "Cracked Urn", "res://scenes/RuinsCrackedUrn.tscn", "RuinsCrackedUrn");
		internal static readonly BestiaryCreature LintelBearer =
			Creature(Stage, "ruins_lintel_bearer", "Lintel-Bearer", "res://scenes/RuinsLintelBearer.tscn", "RuinsLintelBearer");
		internal static readonly BestiaryCreature Lector =
			Creature(Stage, "ruins_lector", "The Lector", "res://scenes/RuinsLector.tscn", "RuinsLector");
		internal static readonly BestiaryCreature Censor =
			Creature(Stage, "ruins_censor", "The Censor", "res://scenes/RuinsCensor.tscn", "RuinsCensor");

		internal static ChapterBestiary Build()
		{
			BestiarySlot bulk = Planned(BestiaryRole.Bulk, Unread, CursedDungeon.Shrouded);
			BestiarySlot fast = Planned(BestiaryRole.Fast, Folio, CursedDungeon.GraveRunner);
			BestiarySlot heavy = Planned(BestiaryRole.Heavy, WardedStatue, CursedDungeon.Shieldbearer);
			BestiarySlot wall = Planned(BestiaryRole.Wall, Gargoyle, CursedDungeon.BarrowKnight);

			return new ChapterBestiary
			{
				StageId = Stage,
				Slots = new[]
				{
					bulk, fast, heavy, wall,
					Planned(BestiaryRole.Ranged, Magister, CursedDungeon.Hexer),
					Planned(BestiaryRole.Turret, Obelisk, CursedDungeon.SkullSentry),
					Planned(BestiaryRole.Charger, WardCat, CursedDungeon.CryptLunger),
					Planned(BestiaryRole.Exploder, CrackedUrn, CursedDungeon.GraveBloat),
					Planned(BestiaryRole.Slammer, LintelBearer, CursedDungeon.Gravebreaker),
					Planned(BestiaryRole.Summoner, Lector, CursedDungeon.BoneCaller),
					Planned(BestiaryRole.MiniBoss, Censor, CursedDungeon.Warden),
				},
				// The old Ruins case, band for band (with the band the cut Orc held already folded back in).
				Table = new[]
				{
					Band(heavy, 0.26f, 2.2f),
					Band(wall, 0.32f, 1.4f),
					Band(bulk, 0.28f, 1.0f),
					Band(fast, 0.14f, 0.75f),
				},
			};
		}
	}
}
