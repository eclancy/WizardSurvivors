namespace WizardSurvivors.scripts;

public static partial class StageBestiary
{
	/// <summary>
	/// The Sketchbook (stage_8): Eric's kids' drawings, and nothing else. Boss: Wizard Dude.
	/// </summary>
	/// <remarks>
	/// UNCHANGED, AND NOT PART OF PHASE 2. Moved here from Node2DGame's switch and its IsSketchbookStage
	/// swaps, band for band and slot for slot: Smilers are the bulk and they are fast and flimsy,
	/// one-eyes the middle, zombies the slow wall, vampires the sprinters, the message chases, and
	/// Rockem takes the top band as the heavy and is also the chapter's recurring miniboss. Blobby is
	/// its charger and Pushy Dude its slammer (a cone: it is called Pushy, so it shoves the way it faces).
	///
	/// The ranged, turret, exploder and summoner roles are absent rather than reskinned: the kids drew
	/// no counterpart, and a chapter with three skeletons in it is not their chapter any more.
	///
	/// The ghost (Elim's Boo) is an attendant: it fills no slot because Node2DGame.TickGhostMiniBoss
	/// sends it on its own clock, twice a run. The dog is a companion, not an enemy, so it is not here.
	/// </remarks>
	private static class Sketchbook
	{
		private const string Stage = "stage_8";

		internal static readonly BestiaryCreature Smiler =
			Creature(Stage, "kids_smiler", "Smiler", "res://scenes/KidSmiler.tscn", "KidSmiler");
		internal static readonly BestiaryCreature OneEye =
			Creature(Stage, "kids_one_eye", "One-Eye", "res://scenes/KidOneEye.tscn", "KidOneEye");
		internal static readonly BestiaryCreature Zombie =
			Creature(Stage, "kids_zombie", "Zombie", "res://scenes/KidZombie.tscn", "KidZombie");
		internal static readonly BestiaryCreature Vampire =
			Creature(Stage, "kids_vampire", "Vampire", "res://scenes/KidVampire.tscn", "KidVampire");
		internal static readonly BestiaryCreature Message =
			Creature(Stage, "kids_message", "The Message", "res://scenes/KidMessage.tscn", "KidMessage");
		internal static readonly BestiaryCreature Rockem =
			Creature(Stage, "kids_rockem", "Rockem", "res://scenes/KidRockem.tscn", "KidRockem");
		internal static readonly BestiaryCreature Blobby =
			Creature(Stage, "kids_blobby", "Blobby", "res://scenes/KidBlobby.tscn", "KidBlobby");
		internal static readonly BestiaryCreature Pushy =
			Creature(Stage, "kids_pushy", "Pushy Dude", "res://scenes/KidPushy.tscn", "KidPushy");
		internal static readonly BestiaryCreature Ghost =
			Creature(Stage, "kids_ghost", "The Ghost", "res://scenes/BooEnemy.tscn", "BooEnemy");

		internal static ChapterBestiary Build()
		{
			const string NoCounterpart = "the kids drew no counterpart, and the chapter takes nothing they did not draw";
			BestiarySlot bulk = Own(BestiaryRole.Bulk, Smiler);
			BestiarySlot fast = Own(BestiaryRole.Fast, Vampire);
			BestiarySlot heavy = Own(BestiaryRole.Heavy, Rockem);
			BestiarySlot wall = Own(BestiaryRole.Wall, Zombie);
			BestiarySlot oneEyes = Own(BestiaryRole.Extra, OneEye);
			BestiarySlot messages = Own(BestiaryRole.Extra, Message);

			return new ChapterBestiary
			{
				StageId = Stage,
				ArtContractExempt = true,
				Slots = new[]
				{
					bulk, fast, heavy, wall, oneEyes, messages,
					Absent(BestiaryRole.Ranged, NoCounterpart),
					Absent(BestiaryRole.Turret, NoCounterpart),
					Own(BestiaryRole.Charger, Blobby),
					Absent(BestiaryRole.Exploder, NoCounterpart),
					Own(BestiaryRole.Slammer, Pushy),
					Absent(BestiaryRole.Summoner, NoCounterpart),
					Own(BestiaryRole.MiniBoss, Rockem),
				},
				// The old Sketchbook case, band for band.
				Table = new[]
				{
					Band(bulk, 0.28f, 0.8f),
					Band(oneEyes, 0.20f, 1.0f),
					Band(wall, 0.17f, 1.5f),
					Band(fast, 0.15f, 0.85f),
					Band(messages, 0.13f, 1.1f),
					Band(heavy, 0.07f, 2.4f),
				},
				Attendants = new[] { Ghost },
			};
		}
	}
}
