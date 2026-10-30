using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// Every number that decides what falls out of a kill and where a chest comes from, in one place.
/// </summary>
/// <remarks>
/// The numbers used to live in four files and agree by accident: the bonus chance on
/// <c>Player.GetBonusDropChance</c>, the even three-way split in <c>Enemy.TryDropBonusItem</c>, the
/// chest clock in <c>Node2DGame</c>, and the chest-frequency bonus that three relics fed into BOTH
/// the chest clock and the bonus chance. That last one is how the speed potion flooded a run: the
/// Signet Ring's "+20% chests" also added twenty points to a 3% per-kill drop chance, and a third of
/// every bonus drop was a potion. The measured table, before and after, is in
/// .ai/passives-and-items.md section 8.
///
/// Two rules the numbers keep:
/// - **The XP orb half of the bonus table is unchanged.** It is part of the XP economy the level
///   gates were measured on (.ai/run-pacing.md, "2.16 XP a kill"), so only the consumables moved.
/// - **Chests come from things the player kills on purpose**, never from a clock. A chest is the
///   reward for an elite, a Warden, the Sketchbook's ghost or a side event.
/// </remarks>
public static class DropTable
{
	// --- the bonus table: what an ordinary kill may drop besides its own orb (issue #25) -----------

	/// <summary>Per-kill chance of a bonus XP orb, before Luck. A third of the old 3%: unchanged.</summary>
	public const float BonusOrbChance = 0.01f;

	/// <summary>The bonus orb is the kill's orb times this: 2 x 8 = 16 XP, a medium orb.</summary>
	public const int BonusOrbMultiplier = 8;

	/// <summary>Per-kill chance of a health flask (10 HP), before Luck. Was a third of 3%.</summary>
	public const float HealthFlaskChance = 0.004f;

	/// <summary>
	/// Per-kill chance of a speed potion, before Luck. Was a third of 3% - 52 in a measured run. About ten
	/// a run at this rate (measured at 0.2%: four, which read as gone rather than rare).
	/// </summary>
	public const float SpeedPotionChance = 0.003f;

	/// <summary>
	/// Real seconds of play that must pass between two speed potions, however lucky the rolls. The
	/// potion is an 8-second +50% attack speed / +30% move speed burst; at the old rate one landed
	/// every ten to twenty seconds late in a run, so it was nearly always on and stopped being an
	/// event. Real seconds, not the pace clock: this is a rhythm, how often a burst may be felt.
	/// </summary>
	public const float MinSecondsBetweenSpeedPotions = 45f;

	/// <summary>
	/// How far Luck multiplies the three chances: 1 + this x Luck (0..1). At full Luck the bonus
	/// table drops 3.33 times as often, which is the old "3% rising to 10% at max Luck".
	/// </summary>
	public const float LuckScale = 7f / 3f;

	public enum BonusDrop { None, XpOrb, HealthFlask, SpeedPotion }

	/// <summary>
	/// Which bonus drop a kill pays, from one roll in [0, 1). One roll for all three keeps a kill to
	/// at most one bonus item, as it always was. The potion's spacing is the caller's to enforce.
	/// </summary>
	public static BonusDrop Roll(float roll, float luck01)
	{
		float luck = 1f + LuckScale * Mathf.Clamp(luck01, 0f, 1f);
		float orb = BonusOrbChance * luck;
		float flask = orb + HealthFlaskChance * luck;
		float potion = flask + SpeedPotionChance * luck;
		if (roll < orb) return BonusDrop.XpOrb;
		if (roll < flask) return BonusDrop.HealthFlask;
		if (roll < potion) return BonusDrop.SpeedPotion;
		return BonusDrop.None;
	}

	// --- greater orbs: what the old chest-frequency relics pay instead -----------------------------

	/// <summary>
	/// An ordinary kill's orb that comes up one size larger is worth this instead of 2: a medium orb.
	/// Player.GetGreaterOrbChance is the chance, fed by the Signet Ring, the Tithe of Wormwood and the
	/// Regalia. Each point of chance is worth 0.08 XP a kill against a base of 2.16, so +5% is about
	/// +18% XP - the same as the Orb of Antioch's flat +15%, in a form that shows on the ground.
	/// </summary>
	public const int GreaterOrbValue = 10;

	// --- chests: who carries one ---------------------------------------------------------------

	/// <summary>
	/// The chance an ordinary elite drops a chest when it dies. Relics raise it. 0.65 puts a run at about
	/// fifteen chests (one guaranteed elite, about fifteen more killed, four Wardens) against the clock's
	/// twenty: measured at 0.5 it was twelve (8 elite, 4 Warden), which left the sets too far out of reach.
	/// </summary>
	public const float EliteChestChance = 0.65f;

	/// <summary>
	/// Seconds a carried chest takes to rise out of the ground where its carrier fell, during which
	/// it cannot be opened - the side event's rise (ChestReward.MaterialiseSeconds), shorter. An elite
	/// usually dies touching the player, and a chest that opened on the frame it appeared would be
	/// claimed before it was seen.
	/// </summary>
	public const float CarriedChestRiseSeconds = 0.6f;

	/// <summary>A carried chest that would land closer than this to the player is set out to it.</summary>
	public const float CarriedChestMinDistance = 90f;

	/// <summary>The Hidden Drawer's chance that an opened chest leaves another behind.</summary>
	public const float NestedChestChance = 0.25f;

	/// <summary>Seconds the chest found in a chest takes to rise, as a side event's does.</summary>
	public const float NestedChestRiseSeconds = 1.2f;

	/// <summary>Relics a chest offers before any bonus. The Relic Key and John Dee add one each.</summary>
	public const int BaseChestOffers = 3;

	// --- the Gleaning Bell: every XP orb on the map, called home (GleaningBell.cs) -------------------
	//
	// Eric: "there should be an item or spell or boon or collectible item that collects all missed XP.
	// (i dont want it to be required, so people take it every run)". So it is not a choice at all: it
	// is a rare find, carried by the same bodies that carry chests, and nothing raises its chance - no
	// relic, no boon, not Luck - so there is nothing to build around and nothing to pick every run.
	// .ai/passives-and-items.md section 8i has the measured counts.

	/// <summary>
	/// The chance the first carrier killed (an elite, a Warden, the Sketchbook's ghost) drops a bell.
	/// Every carrier killed without one adds <see cref="GleaningBellChanceStep"/>, and a bell resets it:
	/// a rising chance rather than a flat one, so a run sees one or two and almost never none. At a flat
	/// 7.5% the twenty-odd carriers of a run would drop none about one run in five and three or more
	/// about one in five; this way the first comes at about the ninth carrier (minute seven or eight)
	/// and the second, when there is one, late in the run, when what lies uncollected is worth most.
	/// </summary>
	public const float GleaningBellBaseChance = 0.02f;

	/// <summary>What each carrier that paid no bell adds to the next one's chance.</summary>
	public const float GleaningBellChanceStep = 0.015f;

	/// <summary>
	/// Run seconds that must pass after a bell drops before another may. A second bell straight after
	/// the first would call in a field the first just emptied. Real seconds, not the pace clock: how long
	/// the field takes to fill again is kills, and kills are capped in real seconds (.ai/run-pacing.md 3.3).
	/// </summary>
	public const float GleaningBellMinSecondsApart = 90f;

	/// <summary>Seconds the bell spends thrown out of its carrier before it lands and can be taken.</summary>
	public const float GleaningBellTossSeconds = 0.45f;

	/// <summary>How far from its carrier a bell lands; and never closer than this to the player.</summary>
	public const float GleaningBellTossDistance = 56f;
	public const float GleaningBellMinPlayerDistance = 80f;
}
