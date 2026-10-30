namespace WizardSurvivors.scripts;

// The draw layers a thing in the world picks from (2026-10-28: "when using the hammer, the hammer
// itself should be above enemies, but the quake it creates should be underneath their sprites. Same
// with meteors - the meteor and explosion should be rendered above enemies, but the fires should be
// rendered underneath").
//
// The arena y-sorts, and y-sort only orders things that share a z. So anything left at z 0 is sorted
// among the bodies by where its origin stands: a blast or a bolt is cut by every body south of its
// centre, and a ring painted on the floor lies across every body north of it. The rule is to pick a
// side. What lies on the floor goes under every body; what is in the air - a projectile, a blast, a
// swung weapon, a thrown spray - goes over them; only what stands on the ground the way a body does
// (a body, a familiar, a tree, a chest) stays at Bodies and is sorted. One effect is often both, and
// is then drawn on two nodes: SpiritualWeapon's weapon and its cracks, MeteorImpact and CraterLayer.
//
// Set ZAsRelative = false with these, so an effect parented under the player or an enemy lands on the
// layer it names. The census that found every effect drawn among the bodies is _UiShot --layer-census.
public static class DrawLayers
{
	/// <summary>The painted ground (LevelTilePainter).</summary>
	public const int Terrain = -100;

	/// <summary>Walkable decoration and scorch marks, flat on the ground.</summary>
	public const int Decal = -20;

	/// <summary>What lies on the ground: craters, pools, lanes (CraterGround.GroundZ; its fire is one above).</summary>
	public const int Ground = -15;

	/// <summary>
	/// A warning or a mark painted on the floor round a body's feet: under every body, over the craters and
	/// pools, so a burning crater can never hide a slam that is winding up.
	/// </summary>
	public const int GroundTell = -1;

	/// <summary>The bodies, y-sorted among themselves.</summary>
	public const int Bodies = 0;

	/// <summary>Anything in the air: projectiles, blasts, swung weapons, thrown sprays. Over every body.</summary>
	public const int Air = 6;

	/// <summary>
	/// The icon an enemy shows over its head while it winds up an attack (TelegraphIcon): over every body,
	/// every projectile and the lantern-tender's dark (7) and light (8), so a wind-up stays findable in a
	/// packed swarm; under the floating numbers.
	/// </summary>
	public const int AttackIcon = 12;

	/// <summary>Floating numbers and words: over everything in the world, under the HUD's canvas layers.</summary>
	public const int FloatingText = 20;
}
