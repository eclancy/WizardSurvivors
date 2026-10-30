namespace WizardSurvivors.scripts;

/// <summary>
/// The evolutions of the four spells that fly on ElementalBolt - Molten Shard, Riptide, Hunter's Draw
/// and Gale Blade - by id, with the numbers their behaviour is built from (.ai/spell-roster.md 2e,
/// "Every spell its own evolutions").
/// </summary>
/// <remarks>
/// Every card of these four used to be a bolt's number on a bolt: +35% damage and larger, -30% cooldown
/// and faster, +2 pierce, three times the size, three more. Each now changes what the player SEES the
/// spell do - a shape, a behaviour, something left behind - drawn from what the spell already is, and
/// that behaviour is switched on by the milestone's id (SpellData.HasEvolution, which reads the level 4,
/// level 8 and Apotheosis selections). The ids never moved: renaming a branch keeps its id, so the
/// Apotheosis gates (Tituba's Riptide path), RiptideWave.TitanEvolutionId and the ascension browser's
/// SeenAscensionIds carry over. The numbers live here rather than in the scripts so the cards in
/// SpellEvolutionCatalog quote the same constants the code runs on, and a retune cannot leave a card
/// promising the old one.
/// </remarks>
public static class BoltEvolutions
{
	// --- Molten Shard: a homing shard at a random enemy that burns away a share of its health ---

	/// <summary>Slag Trail: its flight leaves a line of burning slag.</summary>
	public const string SlagTrailId = "molten_shard_amplified_impact";
	public const float SlagSeconds = 2.0f;
	public const float SlagTickSeconds = 0.4f;
	/// <summary>Of the shard's hit, every tick, to whatever stands on the slag.</summary>
	public const float SlagTickShare = 0.3f;
	/// <summary>Half the slag's width; the hit adds a body (RiptideWave.BodyAllowance) as every zone does.</summary>
	public const float SlagHalfWidth = 7f;

	/// <summary>Splitting Shard: on its first hit the shard splits in two that home on new targets.</summary>
	public const string SplittingShardId = "molten_shard_hyper_acceleration";
	public const float SplitShare = 0.65f;
	public const int SplitPierce = 1;
	public const float SplitScale = 0.72f;
	/// <summary>How far from the split the halves look for their new targets.</summary>
	public const float SplitReach = 300f;

	/// <summary>Magma Core: the shard lodges in its target and erupts a moment later.</summary>
	public const string MagmaCoreId = "molten_shard_reactive_catalyst";
	public const float MagmaFuseSeconds = 0.8f;
	public const float MagmaRadius = 62f;
	/// <summary>Of the shard's hit, to everything the eruption reaches - the host included.</summary>
	public const float MagmaShare = 1.3f;

	/// <summary>Caldera: the shard dives into the ground at its first hit and a lava pool wells up.</summary>
	public const string CalderaId = "molten_shard_titan_cataclysm";
	public const float CalderaSeconds = 4.0f;
	public const float CalderaRadius = 72f;
	public const float CalderaTickSeconds = 0.5f;
	public const float CalderaTickShare = 0.45f;
	/// <summary>How long the pool takes to well up to its full width.</summary>
	public const float CalderaWellSeconds = 0.35f;

	/// <summary>Forge Swarm: shards gather in orbit as they are made, then all launch at once.</summary>
	public const string ForgeSwarmId = "molten_shard_infinite_horizon";
	/// <summary>The orbit launches when it holds this many.</summary>
	public const int ForgeVolley = 8;
	public const float ForgeOrbitRadius = 52f;
	/// <summary>Radians a second the forged ring turns.</summary>
	public const float ForgeSpin = 2.4f;

	// --- Riptide: a wave fired once down a line, piercing what it passes ---

	/// <summary>Breaker: the wave throws what it breaks over (the card's KnockbackBonus). Tituba's level 4.</summary>
	public const string BreakerId = "riptide_amplified_impact";

	/// <summary>Groundswell: the wave builds as it runs - narrow off your hand, wide by the end of its run.</summary>
	public const string GroundswellId = "riptide_hyper_acceleration";
	public const float GroundswellStart = 0.6f;
	public const float GroundswellEnd = 1.8f;

	/// <summary>Tidal Wall: the titanic wave (RiptideWave.TitanEvolutionId). Tituba's level 8.</summary>
	/// <remarks>
	/// "tidal wall starts too big, it should start small and grow as it moves away from you. it's also
	/// firing far too fast" (Eric, 0.9.2). It was a wall three times a level 8 wave's width from the hand
	/// on, every 1.25 s - three of them on the screen at once, the wizard under the nearest (measured
	/// 2026-10-30: 47% of a PC screen at the peak, a third on average, his sprite covered 59% of the time).
	/// Now it leaves the hand at the size a wave would be without the card (1 / TidalWallScale of its
	/// full height) and rises to its full height by the end of its run, hit and drawing alike (the node's
	/// scale is both, ElementalBolt.Growth), and the sea takes TidalWallCooldown times as long to gather:
	/// one wall at a time, an event rather than a stream. Its damage a hit stayed at +60%: the three walls
	/// were overkilling one pack, and at normal enemy health a wall this wide is limited by how many enemies
	/// reach it, not by its hit - x1.6, x1.7 and x2.2 measured 491, 495 and 518 a second at level 8, against
	/// the old wall's 552 the same day (.ai/spell-roster.md, 2i).
	/// </remarks>
	public const string TidalWallId = "riptide_titan_cataclysm";
	/// <summary>The card's ScaleMultiplier: how many times a wave's width the wall is when it has risen.</summary>
	public const float TidalWallScale = 3.0f;
	/// <summary>The card's CooldownMultiplier: how much longer the sea takes to gather between walls.</summary>
	public const float TidalWallCooldown = 3.0f;
	/// <summary>The card's DamageMultiplier.</summary>
	public const float TidalWallDamage = 1.6f;

	/// <summary>Trident: three waves fanned from the hand at once, the middle one on the aim.</summary>
	public const string TridentId = "riptide_infinite_horizon";
	/// <summary>Degrees between neighbouring waves of the fan.</summary>
	public const float TridentSpreadDegrees = 24f;

	// --- Hunter's Draw: charge while moving, loose a volley when you stop ---

	/// <summary>Heavy Draw: a full draw's arrows are heavier, and throw back and stagger what they hit.</summary>
	public const string HeavyDrawId = "hunters_draw_amplified_impact";
	public const float HeavyStaggerSeconds = 0.35f;
	public const float HeavyArrowScale = 1.35f;

	/// <summary>Running Shot: while the bow is drawn, a single arrow every few steps at the nearest.</summary>
	public const string RunningShotId = "hunters_draw_hyper_acceleration";
	/// <summary>Ground covered between running shots.</summary>
	public const float RunningShotSpacing = 85f;
	/// <summary>Of the arrow's own hit with no draw behind it: a snap shot, not a drawn one.</summary>
	public const float RunningShotShare = 0.6f;

	/// <summary>Barbed: an arrow that stops in an enemy lodges there and bleeds it.</summary>
	public const string BarbedId = "hunters_draw_reactive_catalyst";
	public const float BarbedSeconds = 3.0f;
	public const float BarbedTickSeconds = 0.5f;
	/// <summary>Of the arrow's hit, every tick, while the arrow stays in.</summary>
	public const float BarbedTickShare = 0.25f;

	/// <summary>Rain of Arrows: the volley goes up, and comes down as a field over the pack.</summary>
	public const string RainOfArrowsId = "hunters_draw_titan_cataclysm";
	public const int RainDropsPerArrow = 6;
	/// <summary>The field the arrows fall over, before the player's Area.</summary>
	public const float RainRadius = 110f;
	/// <summary>Where one falling arrow strikes; the hit adds a body, as every zone does.</summary>
	public const float RainHitRadius = 14f;
	/// <summary>The volley's climb, before the first arrow comes down.</summary>
	public const float RainRiseSeconds = 0.4f;
	/// <summary>How long the field takes to land, first arrow to last.</summary>
	public const float RainSpreadSeconds = 0.55f;

	/// <summary>Hunter's Mark: a full draw marks the strongest enemy in reach, and the whole volley seeks it.</summary>
	public const string HuntersMarkId = "hunters_draw_infinite_horizon";
	public const float MarkSeconds = 5f;
	/// <summary>How much more the marked enemy takes from everything (Enemy.ApplyVulnerable).</summary>
	public const float MarkVulnerable = 0.25f;

	// --- Gale Blade: thrown out past its target and back to the hand, cutting on both legs ---

	/// <summary>Eddy: at the end of its throw the blade hangs, spinning, cutting all round it.</summary>
	public const string EddyId = "gale_blade_honed_edge";
	public const float EddySeconds = 0.6f;
	public const float EddyScale = 1.7f;
	/// <summary>How often the hanging blade cuts what is inside it again.</summary>
	public const float EddyRecutSeconds = 0.15f;

	/// <summary>Hooked Throw: the blade comes home on a wide curve, not back along its throw.</summary>
	public const string HookedThrowId = "gale_blade_keen_wind";
	public const float HookDegrees = 68f;

	/// <summary>Live Wire: a tether of lightning runs from the hand to the blade, cutting what it crosses.</summary>
	public const string LiveWireId = "gale_blade_quickening";
	public const float LiveWireTickSeconds = 0.2f;
	public const float LiveWireShare = 0.35f;
	public const float LiveWireHalfWidth = 6f;

	/// <summary>Thunderhead: each time the blade turns, lightning strikes where it hangs.</summary>
	public const string ThunderheadId = "gale_blade_executioners_gale";
	public const float ThunderRadius = 92f;
	public const float ThunderShare = 2.8f;
	public const float ThunderStaggerSeconds = 0.4f;

	/// <summary>Squall: at the end of its throw the blade bursts into five, and all five come home.</summary>
	public const string SquallId = "gale_blade_tempest_rhythm";
	public const int SquallBlades = 5;
	/// <summary>The fan of paths home, end to end.</summary>
	public const float SquallSpreadDegrees = 130f;
	public const float SquallShare = 0.75f;
}
