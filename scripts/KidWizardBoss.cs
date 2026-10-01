using Godot;
using WizardSurvivors.scripts;

// Wizard Dude, the boss of the Sketchbook — and the only boss in the game drawn by a child.
//
// The chapter is built out of twelve sprite sheets Eric's kids made, and this is the one that
// looks back at you: a wizard in a spotted hat, drawn once, with no walk cycle and no attack
// pose, because a nine-year-old draws the character rather than the animation. The fight is
// designed around that rather than around a sheet it does not have.
//
// WHAT IT DOES. It keeps its distance and throws the magic missile the same kids drew, in a fan,
// on a cone telegraph. The cone is the honest tell for a spread: the wedge it paints is exactly
// the ground the fan covers, so "step out of the cone" and "do not get hit" are the same
// instruction. It has no melee slam at all - the cone resolves into missiles instead of into a
// hit - which makes it the only boss in the game whose telegraph never damages anything by
// itself.
//
// WHY IT IS A RANGED BOSS AND NOTHING ELSE. Every other boss in the campaign is a primitive I
// built for it; this one had to be built for art that already existed. A charge would need a
// leaning pose, a burrow would need the figure to have a bottom edge that reads as sinking, and
// a sweep would need it to turn. It has one frame, standing, facing the player. A caster is what
// one standing frame can honestly be.
public partial class KidWizardBoss : BossEnemy
{
	[Export] public string MissileScenePath { get; set; } = "res://scenes/KidMissile.tscn";
	[Export] public int MissilesPerVolley { get; set; } = 5;
	[Export] public float MissileSpreadDegrees { get; set; } = 44f;
	[Export] public float MissileSpeed { get; set; } = 250f;
	[Export] public int MissileDamage { get; set; } = 3;

	/// <summary>How far out the missiles appear, so they do not spawn inside his own body.</summary>
	[Export] public float MissileSpawnOffset { get; set; } = 30f;

	/// <summary>Below this he backs away; above <see cref="StandoffDistance"/> he closes.</summary>
	[Export] public float RetreatDistance { get; set; } = 190f;
	[Export] public float StandoffDistance { get; set; } = 330f;

	[Export] public Color SpellColor { get; set; } = new Color(0.86f, 0.45f, 0.95f);

	private PackedScene missileScene;

	public override void _Ready()
	{
		base._Ready();
		missileScene = ResourceLoader.Load<PackedScene>(MissileScenePath);
		if (missileScene == null)
			GD.PushError($"KidWizardBoss: missile scene not found at '{MissileScenePath}', so he will never cast.");
	}

	// A cone, matching the fan. SlamRadius is how far the missiles are promised to reach rather
	// than a melee radius, and the wedge is drawn to exactly that.
	protected override GroundSlamAttack BuildSlam() =>
		new(SlamIntervalSeconds, SlamTelegraphSeconds, SlamRadius, SlamDamage, SpellColor)
		{
			Shape = TelegraphShape.Cone,
			HalfAngleDegrees = Mathf.Max(6f, MissileSpreadDegrees * 0.5f + 5f),
		};

	protected override void OnPhaseEntered(int phase)
	{
		ApplyEnrage();
		// The enrage buys more of the thing the fight is about. A wider fan rather than a faster
		// one: faster would out-run the tell, and the tell is the only warning the player gets.
		MissilesPerVolley += 2;
		MissileSpreadDegrees += 14f;
		Slam.HalfAngleDegrees = Mathf.Max(6f, MissileSpreadDegrees * 0.5f + 5f);
	}

	// Kiting, in the same ordered bands RangedEnemy uses: back off inside the retreat distance,
	// hold between the two, close beyond the standoff. A caster that walked into contact would be
	// fighting the fan it just threw.
	protected override Vector2 AdjustSteering(Vector2 chaseDirection, float distanceToPlayer)
	{
		// Planted for the wind-up, like everything else: the wedge is drawn from where he stands.
		if (Slam != null && Slam.IsWindingUp)
			return Vector2.Zero;

		if (distanceToPlayer < RetreatDistance)
			return -chaseDirection;

		return distanceToPlayer > StandoffDistance ? chaseDirection : Vector2.Zero;
	}

	protected override bool WantsToAttack(Node2D target, float distanceToPlayer) =>
		missileScene != null && distanceToPlayer <= SlamRadius;

	// The cone hits nobody. It exists to say where the fan is going, and the fan does the damage -
	// so this replaces the resolve rather than adding to it.
	protected override void OnAttackResolved(Node2D target) => FireVolley(target);

	private void FireVolley(Node2D target)
	{
		Node arena = GetParent();
		if (missileScene == null || arena == null || !IsInstanceValid(arena))
			return;

		// The same heading the wedge was drawn along. Reading Slam.Facing rather than re-aiming at
		// the player is the whole reason the tell can be trusted: re-aiming here would send the
		// fan somewhere the player was never shown.
		Vector2 aim = Slam.Facing;
		if (aim.LengthSquared() <= 0.0001f)
			aim = Vector2.Right;

		SfxPlayer.AtPosition(SfxCatalog.EnemyShoot, GlobalPosition, 0.07f);

		int count = Mathf.Max(1, MissilesPerVolley);
		float spread = Mathf.DegToRad(MissileSpreadDegrees);
		for (int i = 0; i < count; i++)
		{
			if (missileScene.Instantiate() is not EnemyProjectile missile)
			{
				GD.PushError($"KidWizardBoss: '{MissileScenePath}' has no EnemyProjectile script attached.");
				return;
			}

			missile.Damage = ScaleOutgoingDamage(MissileDamage);
			missile.Speed = MissileSpeed;

			// Evenly across the fan, and a single missile comes out dead on the aim line.
			float fraction = count == 1 ? 0f : i / (float)(count - 1) - 0.5f;
			Vector2 heading = aim.Rotated(fraction * spread);
			missile.LaunchInDirection(GlobalPosition + heading * MissileSpawnOffset, heading);
			// Parented to the arena, not to him: killing the caster must not delete missiles that
			// are already the player's problem.
			arena.CallDeferred("add_child", missile);
		}
	}
}
