namespace WizardSurvivors.scripts;

public static partial class StageBestiary
{
	/// <summary>
	/// Chapter 4, the Blighted Swamp (stage_3): the mired, and Mother Rot's brood. Boss: Mother Rot.
	/// </summary>
	/// <remarks>
	/// "The lanterns still burn. Nothing living lights them." The peat cutters and ferry folk of the
	/// fen, its toads and striders, and the swollen things Mother Rot sheds. The wall IS her brood:
	/// when it is drawn, MotherRotBoss.tscn's SpawnScenePath should point at it too, so what she sheds
	/// in the fight is what has been walking at the player all run (ValidateBestiary lists it until then).
	/// </remarks>
	private static class BlightedSwamp
	{
		private const string Stage = "stage_3";

		internal static readonly BestiaryCreature Mired =
			Creature(Stage, "swamp_mired", "The Mired", "res://scenes/SwampMired.tscn", "SwampMired");
		internal static readonly BestiaryCreature Strider =
			Creature(Stage, "swamp_strider", "Mire Strider", "res://scenes/SwampStrider.tscn", "SwampStrider");
		internal static readonly BestiaryCreature Mudback =
			Creature(Stage, "swamp_mudback", "Mudback", "res://scenes/SwampMudback.tscn", "SwampMudback");
		internal static readonly BestiaryCreature Broodling =
			Creature(Stage, "swamp_broodling", "Broodling", "res://scenes/SwampBroodling.tscn", "SwampBroodling");
		internal static readonly BestiaryCreature Lamplighter =
			Creature(Stage, "swamp_lamplighter", "The Lamplighter", "res://scenes/SwampLamplighter.tscn", "SwampLamplighter");
		internal static readonly BestiaryCreature FrogIdol =
			Creature(Stage, "swamp_frog_idol", "Frog Idol", "res://scenes/SwampFrogIdol.tscn", "SwampFrogIdol");
		internal static readonly BestiaryCreature BogHound =
			Creature(Stage, "swamp_bog_hound", "Bog Hound", "res://scenes/SwampBogHound.tscn", "SwampBogHound");
		internal static readonly BestiaryCreature BlisterToad =
			Creature(Stage, "swamp_blister_toad", "Blister Toad", "res://scenes/SwampBlisterToad.tscn", "SwampBlisterToad");
		internal static readonly BestiaryCreature PeatCutter =
			Creature(Stage, "swamp_peat_cutter", "Peat Cutter", "res://scenes/SwampPeatCutter.tscn", "SwampPeatCutter");
		internal static readonly BestiaryCreature ReedPiper =
			Creature(Stage, "swamp_reed_piper", "Reed Piper", "res://scenes/SwampReedPiper.tscn", "SwampReedPiper");
		internal static readonly BestiaryCreature Ferryman =
			Creature(Stage, "swamp_ferryman", "The Ferryman", "res://scenes/SwampFerryman.tscn", "SwampFerryman");

		internal static ChapterBestiary Build()
		{
			BestiarySlot bulk = Planned(BestiaryRole.Bulk, Mired, CursedDungeon.Shrouded);
			BestiarySlot fast = Planned(BestiaryRole.Fast, Strider, CursedDungeon.GraveRunner);
			BestiarySlot heavy = Planned(BestiaryRole.Heavy, Mudback, CursedDungeon.Shieldbearer);
			BestiarySlot wall = Planned(BestiaryRole.Wall, Broodling, CursedDungeon.BarrowKnight);

			return new ChapterBestiary
			{
				StageId = Stage,
				Slots = new[]
				{
					bulk, fast, heavy, wall,
					Planned(BestiaryRole.Ranged, Lamplighter, CursedDungeon.Hexer),
					Planned(BestiaryRole.Turret, FrogIdol, CursedDungeon.SkullSentry),
					Planned(BestiaryRole.Charger, BogHound, CursedDungeon.CryptLunger),
					Planned(BestiaryRole.Exploder, BlisterToad, CursedDungeon.GraveBloat),
					Planned(BestiaryRole.Slammer, PeatCutter, CursedDungeon.Gravebreaker),
					Planned(BestiaryRole.Summoner, ReedPiper, CursedDungeon.BoneCaller),
					Planned(BestiaryRole.MiniBoss, Ferryman, CursedDungeon.Warden),
				},
				// The old Swamp case, band for band.
				Table = new[]
				{
					Band(wall, 0.38f, 1.4f),
					Band(bulk, 0.26f, 1.0f),
					Band(fast, 0.22f, 0.75f),
					Band(heavy, 0.14f, 2.2f),
				},
			};
		}
	}
}
