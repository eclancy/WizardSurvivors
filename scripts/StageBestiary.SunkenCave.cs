namespace WizardSurvivors.scripts;

public static partial class StageBestiary
{
	/// <summary>
	/// Chapter 3, the Sunken Cave (stage_2): the drowned, and what lives without light. Boss: the
	/// Hollow Choir.
	/// </summary>
	/// <remarks>
	/// "His shadow followed the water down into the dark." The miners and pearl divers who worked
	/// these tunnels, and the blind things that lived in them, taken. Designed in .ai/bestiary.md;
	/// every slot stands in with the Cursed Dungeon's creature until its own is drawn.
	///
	/// The only chapter with a late table. It never had a case in the old environment switch and fell
	/// to the default branch, which changes the mix at five pace minutes; kept exactly.
	/// </remarks>
	private static class SunkenCave
	{
		private const string Stage = "stage_2";

		internal static readonly BestiaryCreature Drowned =
			Creature(Stage, "cave_drowned", "The Drowned", "res://scenes/CaveDrowned.tscn", "CaveDrowned");
		internal static readonly BestiaryCreature Gloomwing =
			Creature(Stage, "cave_gloomwing", "Gloomwing", "res://scenes/CaveGloomwing.tscn", "CaveGloomwing");
		internal static readonly BestiaryCreature Shieldclaw =
			Creature(Stage, "cave_shieldclaw", "Shieldclaw", "res://scenes/CaveShieldclaw.tscn", "CaveShieldclaw");
		internal static readonly BestiaryCreature HollowMiner =
			Creature(Stage, "cave_hollow_miner", "Hollow Miner", "res://scenes/CaveHollowMiner.tscn", "CaveHollowMiner");
		internal static readonly BestiaryCreature PearlDiver =
			Creature(Stage, "cave_pearl_diver", "Pearl Diver", "res://scenes/CavePearlDiver.tscn", "CavePearlDiver");
		internal static readonly BestiaryCreature EchoIdol =
			Creature(Stage, "cave_echo_idol", "Echo Idol", "res://scenes/CaveEchoIdol.tscn", "CaveEchoIdol");
		internal static readonly BestiaryCreature Leaper =
			Creature(Stage, "cave_leaper", "Cave Leaper", "res://scenes/CaveLeaper.tscn", "CaveLeaper");
		internal static readonly BestiaryCreature Bloatfish =
			Creature(Stage, "cave_bloatfish", "Bloatfish", "res://scenes/CaveBloatfish.tscn", "CaveBloatfish");
		internal static readonly BestiaryCreature Rockbreaker =
			Creature(Stage, "cave_rockbreaker", "Rockbreaker", "res://scenes/CaveRockbreaker.tscn", "CaveRockbreaker");
		internal static readonly BestiaryCreature RoostHermit =
			Creature(Stage, "cave_roost_hermit", "Roost Hermit", "res://scenes/CaveRoostHermit.tscn", "CaveRoostHermit");
		internal static readonly BestiaryCreature AnchorWarden =
			Creature(Stage, "cave_anchor_warden", "The Anchor Warden", "res://scenes/CaveAnchorWarden.tscn", "CaveAnchorWarden");

		internal static ChapterBestiary Build()
		{
			BestiarySlot bulk = Planned(BestiaryRole.Bulk, Drowned, CursedDungeon.Shrouded);
			BestiarySlot fast = Planned(BestiaryRole.Fast, Gloomwing, CursedDungeon.GraveRunner);
			BestiarySlot heavy = Planned(BestiaryRole.Heavy, Shieldclaw, CursedDungeon.Shieldbearer);
			BestiarySlot wall = Planned(BestiaryRole.Wall, HollowMiner, CursedDungeon.BarrowKnight);

			return new ChapterBestiary
			{
				StageId = Stage,
				Slots = new[]
				{
					bulk, fast, heavy, wall,
					Planned(BestiaryRole.Ranged, PearlDiver, CursedDungeon.Hexer),
					Planned(BestiaryRole.Turret, EchoIdol, CursedDungeon.SkullSentry),
					Planned(BestiaryRole.Charger, Leaper, CursedDungeon.CryptLunger),
					Planned(BestiaryRole.Exploder, Bloatfish, CursedDungeon.GraveBloat),
					Planned(BestiaryRole.Slammer, Rockbreaker, CursedDungeon.Gravebreaker),
					Planned(BestiaryRole.Summoner, RoostHermit, CursedDungeon.BoneCaller),
					Planned(BestiaryRole.MiniBoss, AnchorWarden, CursedDungeon.Warden),
				},
				// The old default branch, early and late, band for band - the wall appears twice
				// in the late table because it did in the switch.
				Table = new[]
				{
					Band(bulk, 0.78f, 1.0f),
					Band(fast, 0.14f, 0.75f),
					Band(wall, 0.08f, 1.4f),
				},
				LateTable = new[]
				{
					Band(fast, 0.20f, 0.75f),
					Band(wall, 0.15f, 1.4f),
					Band(heavy, 0.10f, 2.2f),
					Band(wall, 0.13f, 1.4f),
					Band(bulk, 0.42f, 1.0f),
				},
				LateTableFromPaceMinutes = 5.0f,
			};
		}
	}
}
