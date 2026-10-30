namespace WizardSurvivors.scripts;

public static partial class StageBestiary
{
	/// <summary>
	/// Chapter 10, the Last Bridge (stage_11): the gate's garrison. Boss: the Constable.
	/// </summary>
	/// <remarks>
	/// "His seat is across the water." His household, taken - a garrison in the steel family - with the
	/// built things of a fortified crossing (the gargoyles on the parapet, the burning pitch) carrying his
	/// fire, the carve-out .ai/world-and-tone.md allows. Designed in .ai/endgame-castle.md 3.5; nothing
	/// may echo the Constable (a tall armoured figure with a ring of keys and a halberd).
	///
	/// STAND-INS ARE THE EMBERDEEP'S, not the Cursed Dungeon's as the road's were: built things, right for
	/// his house, and the chapter the bridge leaves from. Each slot spawns its stand-in until a scene exists
	/// at the planned path (piece C6, .ai/bestiary.md section 8's recipe), with no edit here.
	///
	/// THE TABLE is the Emberdeep's, band for band (mean 1.28): the bridge is a travel chapter and changes
	/// where the swarm comes from, never how hard it is (3.3). Measure it with the bot once the band exists
	/// (C1); if the band starves the spawn supply, the spawn split moves, not this table.
	/// </remarks>
	private static class Bridge
	{
		private const string Stage = "stage_11";

		internal static readonly BestiaryCreature Pikeman =
			Creature(Stage, "bridge_pikeman", "Gate Pikeman", "res://scenes/BridgePikeman.tscn", "BridgePikeman");
		internal static readonly BestiaryCreature Hound =
			Creature(Stage, "bridge_hound", "Bridge Hound", "res://scenes/BridgeHound.tscn", "BridgeHound");
		internal static readonly BestiaryCreature Pavisier =
			Creature(Stage, "bridge_pavisier", "Pavisier", "res://scenes/BridgePavisier.tscn", "BridgePavisier");
		internal static readonly BestiaryCreature Ram =
			Creature(Stage, "bridge_ram", "The Ram", "res://scenes/BridgeRam.tscn", "BridgeRam");
		internal static readonly BestiaryCreature Crossbowman =
			Creature(Stage, "bridge_crossbowman", "Crossbowman", "res://scenes/BridgeCrossbowman.tscn", "BridgeCrossbowman");
		internal static readonly BestiaryCreature Gargoyle =
			Creature(Stage, "bridge_gargoyle", "Gargoyle", "res://scenes/BridgeGargoyle.tscn", "BridgeGargoyle");
		internal static readonly BestiaryCreature Knight =
			Creature(Stage, "bridge_knight", "Gate Knight", "res://scenes/BridgeKnight.tscn", "BridgeKnight");
		internal static readonly BestiaryCreature PitchBarrel =
			Creature(Stage, "bridge_pitch_barrel", "Pitch Barrel", "res://scenes/BridgePitchBarrel.tscn", "BridgePitchBarrel");
		internal static readonly BestiaryCreature Warder =
			Creature(Stage, "bridge_warder", "The Warder", "res://scenes/BridgeWarder.tscn", "BridgeWarder");
		// Keeper of the gate's dogs; its minion is the Bridge Hound.
		internal static readonly BestiaryCreature Houndmaster =
			Creature(Stage, "bridge_houndmaster", "Houndmaster", "res://scenes/BridgeHoundmaster.tscn", "BridgeHoundmaster");
		// The miniboss, on the 48 cell: keeper of the gate's windlass, its great key across his back.
		internal static readonly BestiaryCreature Porter =
			Creature(Stage, "bridge_porter", "The Porter", "res://scenes/BridgePorter.tscn", "BridgePorter");

		internal static ChapterBestiary Build()
		{
			BestiarySlot bulk = Planned(BestiaryRole.Bulk, Pikeman, Emberdeep.SlagThrall);
			BestiarySlot fast = Planned(BestiaryRole.Fast, Hound, Emberdeep.Chainhound);
			BestiarySlot heavy = Planned(BestiaryRole.Heavy, Pavisier, Emberdeep.Bulwark);
			BestiarySlot wall = Planned(BestiaryRole.Wall, Ram, Emberdeep.KilnBrute);

			return new ChapterBestiary
			{
				StageId = Stage,
				Slots = new[]
				{
					bulk, fast, heavy, wall,
					Planned(BestiaryRole.Ranged, Crossbowman, Emberdeep.Kindler),
					Planned(BestiaryRole.Turret, Gargoyle, Emberdeep.FlueMask),
					Planned(BestiaryRole.Charger, Knight, Emberdeep.Ironjaw),
					Planned(BestiaryRole.Exploder, PitchBarrel, Emberdeep.Bellows),
					Planned(BestiaryRole.Slammer, Warder, Emberdeep.TripHammer),
					Planned(BestiaryRole.Summoner, Houndmaster, Emberdeep.Kennelmaster),
					Planned(BestiaryRole.MiniBoss, Porter, Emberdeep.Chainwright),
				},
				// The Emberdeep's table, band for band: 0.24 x 2.2 + 0.38 + 0.24 x 0.75 + 0.14 x 1.4 = 1.284.
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
