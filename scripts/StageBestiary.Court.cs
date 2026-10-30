namespace WizardSurvivors.scripts;

public static partial class StageBestiary
{
	/// <summary>
	/// Chapter 12, the Court (stage_12): his household. Boss: the Seneschal.
	/// </summary>
	/// <remarks>
	/// "A court with no one left in it." His courtiers and servants in linen, taken, and the built things
	/// of a great house - empty armour, candelabra, a censer on legs - burning his fire. Designed in
	/// .ai/endgame-castle.md 5.7; nothing may echo the Seneschal (a steward with a staff of office).
	///
	/// STAND-INS ARE THE EMBERDEEP'S, as the bridge's are: built things, right for his house. Each slot
	/// spawns its stand-in until a scene exists at the planned path (piece C6), with no edit here.
	///
	/// THE TABLE is a step past the Cursed Dungeon's 1.35, which stands before it in the castle and "sits
	/// naturally between the Bridge's and the Court's" (4.1): the Dungeon's shape - wall-heavy, for halls
	/// - with a little more heavy. 0.22 x 2.2 + 0.36 x 1.4 + 0.28 + 0.14 x 0.75 = 1.373. A first guess;
	/// measure it with the bot once the Court's layout exists (C3), because a fixed building changes the
	/// spawn supply and the kill rate is the supply.
	/// </remarks>
	private static class Court
	{
		private const string Stage = "stage_12";

		internal static readonly BestiaryCreature Lackey =
			Creature(Stage, "court_lackey", "Lackey", "res://scenes/CourtLackey.tscn", "CourtLackey");
		internal static readonly BestiaryCreature Hound =
			Creature(Stage, "court_hound", "Court Hound", "res://scenes/CourtHound.tscn", "CourtHound");
		internal static readonly BestiaryCreature Guard =
			Creature(Stage, "court_guard", "Household Guard", "res://scenes/CourtGuard.tscn", "CourtGuard");
		internal static readonly BestiaryCreature Armour =
			Creature(Stage, "court_armour", "Empty Armour", "res://scenes/CourtArmour.tscn", "CourtArmour");
		internal static readonly BestiaryCreature Falconer =
			Creature(Stage, "court_falconer", "Falconer", "res://scenes/CourtFalconer.tscn", "CourtFalconer");
		internal static readonly BestiaryCreature Candelabrum =
			Creature(Stage, "court_candelabrum", "Candelabrum", "res://scenes/CourtCandelabrum.tscn", "CourtCandelabrum");
		internal static readonly BestiaryCreature Duellist =
			Creature(Stage, "court_duellist", "Duellist", "res://scenes/CourtDuellist.tscn", "CourtDuellist");
		internal static readonly BestiaryCreature Censer =
			Creature(Stage, "court_censer", "Censer", "res://scenes/CourtCenser.tscn", "CourtCenser");
		internal static readonly BestiaryCreature Usher =
			Creature(Stage, "court_usher", "Usher", "res://scenes/CourtUsher.tscn", "CourtUsher");
		// Winds a horn for the court hounds; its minion is the Court Hound.
		internal static readonly BestiaryCreature Huntsman =
			Creature(Stage, "court_huntsman", "Huntsman", "res://scenes/CourtHuntsman.tscn", "CourtHuntsman");
		// The miniboss, on the 48 cell: keeper of every room, a ring of keys the size of a shield. He also
		// guards the Treasury (5.4), which is CourtRules' to place, not the miniboss clock's.
		internal static readonly BestiaryCreature Chamberlain =
			Creature(Stage, "court_chamberlain", "The Chamberlain", "res://scenes/CourtChamberlain.tscn", "CourtChamberlain");

		internal static ChapterBestiary Build()
		{
			BestiarySlot bulk = Planned(BestiaryRole.Bulk, Lackey, Emberdeep.SlagThrall);
			BestiarySlot fast = Planned(BestiaryRole.Fast, Hound, Emberdeep.Chainhound);
			BestiarySlot heavy = Planned(BestiaryRole.Heavy, Guard, Emberdeep.Bulwark);
			BestiarySlot wall = Planned(BestiaryRole.Wall, Armour, Emberdeep.KilnBrute);

			return new ChapterBestiary
			{
				StageId = Stage,
				Slots = new[]
				{
					bulk, fast, heavy, wall,
					Planned(BestiaryRole.Ranged, Falconer, Emberdeep.Kindler),
					Planned(BestiaryRole.Turret, Candelabrum, Emberdeep.FlueMask),
					Planned(BestiaryRole.Charger, Duellist, Emberdeep.Ironjaw),
					Planned(BestiaryRole.Exploder, Censer, Emberdeep.Bellows),
					Planned(BestiaryRole.Slammer, Usher, Emberdeep.TripHammer),
					Planned(BestiaryRole.Summoner, Huntsman, Emberdeep.Kennelmaster),
					Planned(BestiaryRole.MiniBoss, Chamberlain, Emberdeep.Chainwright),
				},
				Table = new[]
				{
					Band(heavy, 0.22f, 2.2f),
					Band(wall, 0.36f, 1.4f),
					Band(bulk, 0.28f, 1.0f),
					Band(fast, 0.14f, 0.75f),
				},
			};
		}
	}
}
