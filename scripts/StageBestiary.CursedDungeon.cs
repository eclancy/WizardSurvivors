namespace WizardSurvivors.scripts;

public static partial class StageBestiary
{
	/// <summary>
	/// Chapter 2, the Cursed Dungeon (stage_1): the crypt's evil undead. Boss: the Gaoler.
	/// </summary>
	/// <remarks>
	/// THE CHAPTER THE OLD CAST CAME HOME TO. Every creature here was already drawn - the Bonelight
	/// roster of tools/art/roster.py and sprite_warden.py - and until now it was rolled in every
	/// chapter. It is the buried in grave linen and the buried who were soldiers, a taken caster, a
	/// skull he built, and his jailer, which is a crypt's cast and nobody else's. Every other chapter
	/// borrows these as stand-ins until its own creatures are drawn (.ai/bestiary.md, Phase 2).
	///
	/// Three of the scenes never set an EnemyType and so report as "Enemy", which is declared here as
	/// it is. Left alone on purpose: renaming a shipped type changes what the lifetime kill tally
	/// (LifetimeStats.KillsByEnemyType) has been counting all along.
	/// </remarks>
	private static class CursedDungeon
	{
		private const string Stage = "stage_1";

		internal static readonly BestiaryCreature Shrouded =
			Creature(Stage, "crypt_shrouded", "The Shrouded", "res://scenes/enemy.tscn", "Enemy");
		internal static readonly BestiaryCreature GraveRunner =
			Creature(Stage, "crypt_grave_runner", "Grave Runner", "res://scenes/FastEnemy.tscn", "Enemy");
		internal static readonly BestiaryCreature Shieldbearer =
			Creature(Stage, "crypt_shieldbearer", "Shieldbearer", "res://scenes/TankEnemy.tscn", "Enemy");
		internal static readonly BestiaryCreature BarrowKnight =
			Creature(Stage, "crypt_barrow_knight", "Barrow Knight", "res://scenes/SlowEnemy.tscn", "SlowEnemy");
		internal static readonly BestiaryCreature Hexer =
			Creature(Stage, "crypt_hexer", "Hexer", "res://scenes/HexerEnemy.tscn", "Hexer");
		internal static readonly BestiaryCreature SkullSentry =
			Creature(Stage, "crypt_skull_sentry", "Skull Sentry", "res://scenes/SkullSentry.tscn", "SkullSentry");
		internal static readonly BestiaryCreature CryptLunger =
			Creature(Stage, "crypt_lunger", "Crypt Lunger", "res://scenes/LungerEnemy.tscn", "Lunger");
		internal static readonly BestiaryCreature GraveBloat =
			Creature(Stage, "crypt_grave_bloat", "Grave Bloat", "res://scenes/ExploderEnemy.tscn", "Exploder");
		internal static readonly BestiaryCreature Gravebreaker =
			Creature(Stage, "crypt_gravebreaker", "Gravebreaker", "res://scenes/SlammerEnemy.tscn", "Slammer");
		internal static readonly BestiaryCreature BoneCaller =
			Creature(Stage, "crypt_bone_caller", "Bone-Caller", "res://scenes/SummonerEnemy.tscn", "Summoner");
		// The dark wizard's jailer. Also the Gaoler's key carrier (GaolersKeyEvent asks for this
		// chapter's MiniBoss), which is the side event it was written for.
		internal static readonly BestiaryCreature Warden =
			Creature(Stage, "crypt_warden", "The Warden", "res://scenes/WardenEnemy.tscn", "Warden");

		internal static ChapterBestiary Build()
		{
			BestiarySlot bulk = Own(BestiaryRole.Bulk, Shrouded);
			BestiarySlot fast = Own(BestiaryRole.Fast, GraveRunner);
			BestiarySlot heavy = Own(BestiaryRole.Heavy, Shieldbearer);
			BestiarySlot wall = Own(BestiaryRole.Wall, BarrowKnight);

			return new ChapterBestiary
			{
				StageId = Stage,
				Slots = new[]
				{
					bulk, fast, heavy, wall,
					Own(BestiaryRole.Ranged, Hexer),
					Own(BestiaryRole.Turret, SkullSentry),
					Own(BestiaryRole.Charger, CryptLunger),
					Own(BestiaryRole.Exploder, GraveBloat),
					Own(BestiaryRole.Slammer, Gravebreaker),
					Own(BestiaryRole.Summoner, BoneCaller),
					Own(BestiaryRole.MiniBoss, Warden),
				},
				// The old Castle case, band for band.
				Table = new[]
				{
					Band(heavy, 0.20f, 2.2f),
					Band(wall, 0.36f, 1.4f),
					Band(bulk, 0.30f, 1.0f),
					Band(fast, 0.14f, 0.75f),
				},
			};
		}
	}
}
