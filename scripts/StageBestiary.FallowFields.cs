namespace WizardSurvivors.scripts;

public static partial class StageBestiary
{
	/// <summary>
	/// Chapter 2, the Fallow Fields (stage_9): the people of the land and what they kept. Boss: the
	/// Hayward.
	/// </summary>
	/// <remarks>
	/// "He takes a tithe of everything that grows here." The farms beyond the wood - gleaners, field
	/// hands, the cunning woman, their oxen, boars and rooks - taken, and the iron weathercock he set
	/// over them. Designed in .ai/campaign.md (the cast table is there, in the format of
	/// .ai/bestiary.md section 4); every slot stands in with the Cursed Dungeon's creature until its
	/// own is drawn, exactly as the other chapters began.
	///
	/// THE GENTLEST TABLE AFTER THE FOREST'S. Mean health multiplier 1.07 against the Forest's 1.02 and
	/// the Sunken Cave's 1.00 early / 1.18 late: mostly bulk and runners, a little wall, a token of
	/// heavy so the ox is seen. No late table - the step up is the next chapter's job.
	/// </remarks>
	private static class FallowFields
	{
		private const string Stage = "stage_9";

		internal static readonly BestiaryCreature Gleaner =
			Creature(Stage, "fields_gleaner", "The Gleaner", "res://scenes/FieldsGleaner.tscn", "FieldsGleaner");
		internal static readonly BestiaryCreature Rook =
			Creature(Stage, "fields_rook", "Harvest Rook", "res://scenes/FieldsRook.tscn", "FieldsRook");
		internal static readonly BestiaryCreature YokedOx =
			Creature(Stage, "fields_yoked_ox", "Yoked Ox", "res://scenes/FieldsYokedOx.tscn", "FieldsYokedOx");
		internal static readonly BestiaryCreature Rick =
			Creature(Stage, "fields_rick", "The Rick", "res://scenes/FieldsRick.tscn", "FieldsRick");
		internal static readonly BestiaryCreature CunningWoman =
			Creature(Stage, "fields_cunning_woman", "Cunning Woman", "res://scenes/FieldsCunningWoman.tscn", "FieldsCunningWoman");
		internal static readonly BestiaryCreature Weathercock =
			Creature(Stage, "fields_weathercock", "Iron Weathercock", "res://scenes/FieldsWeathercock.tscn", "FieldsWeathercock");
		internal static readonly BestiaryCreature Boar =
			Creature(Stage, "fields_boar", "Tusked Boar", "res://scenes/FieldsBoar.tscn", "FieldsBoar");
		internal static readonly BestiaryCreature TitheSack =
			Creature(Stage, "fields_tithe_sack", "Tithe Sack", "res://scenes/FieldsTitheSack.tscn", "FieldsTitheSack");
		internal static readonly BestiaryCreature Haymaker =
			Creature(Stage, "fields_haymaker", "Haymaker", "res://scenes/FieldsHaymaker.tscn", "FieldsHaymaker");
		internal static readonly BestiaryCreature Sower =
			Creature(Stage, "fields_sower", "The Sower", "res://scenes/FieldsSower.tscn", "FieldsSower");
		// His jailer on the land: the one who counts what is owed. Also what the well's event asks for
		// if it wants a heavy body (it asks for the Heavy, the ox, as the caravan master does).
		internal static readonly BestiaryCreature TitheReeve =
			Creature(Stage, "fields_tithe_reeve", "The Tithe-Reeve", "res://scenes/FieldsTitheReeve.tscn", "FieldsTitheReeve");

		internal static ChapterBestiary Build()
		{
			BestiarySlot bulk = Planned(BestiaryRole.Bulk, Gleaner, CursedDungeon.Shrouded);
			BestiarySlot fast = Planned(BestiaryRole.Fast, Rook, CursedDungeon.GraveRunner);
			BestiarySlot heavy = Planned(BestiaryRole.Heavy, YokedOx, CursedDungeon.Shieldbearer);
			BestiarySlot wall = Planned(BestiaryRole.Wall, Rick, CursedDungeon.BarrowKnight);

			return new ChapterBestiary
			{
				StageId = Stage,
				Slots = new[]
				{
					bulk, fast, heavy, wall,
					Planned(BestiaryRole.Ranged, CunningWoman, CursedDungeon.Hexer),
					Planned(BestiaryRole.Turret, Weathercock, CursedDungeon.SkullSentry),
					Planned(BestiaryRole.Charger, Boar, CursedDungeon.CryptLunger),
					Planned(BestiaryRole.Exploder, TitheSack, CursedDungeon.GraveBloat),
					Planned(BestiaryRole.Slammer, Haymaker, CursedDungeon.Gravebreaker),
					Planned(BestiaryRole.Summoner, Sower, CursedDungeon.BoneCaller),
					Planned(BestiaryRole.MiniBoss, TitheReeve, CursedDungeon.Warden),
				},
				// 0.46 + 0.28 x 0.75 + 0.22 x 1.4 + 0.04 x 2.2 = 1.066.
				Table = new[]
				{
					Band(bulk, 0.46f, 1.0f),
					Band(fast, 0.28f, 0.75f),
					Band(wall, 0.22f, 1.4f),
					Band(heavy, 0.04f, 2.2f),
				},
			};
		}
	}
}
