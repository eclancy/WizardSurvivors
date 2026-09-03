using Godot;
using WizardSurvivors.scripts;

// An enemy that keeps its distance and throws bolts instead of closing. Every other enemy in the
// roster is the same creature mechanically - run at the player, damage on touch - which left a run
// with exactly one positioning rule: do not stand in the swarm. This one adds a second: something in
// the back is aiming at you, and the swarm in front is what stops you reaching it.
//
// The design is a trade the player can always take. It is fragile, it plants itself for a visible
// wind-up before every shot, and its bolt is slow enough to walk out of - so a player who reads the
// tell pays nothing, and a player who ignores the back line pays steadily. Nothing here should ever
// hit someone who was already moving.
public partial class RangedEnemy : Enemy
{
	// Ranges are a set, not three independent knobs: retreat < standoff < attack. Inside retreat it
	// backs off, between the two it strafes, beyond standoff it closes, and it never fires from
	// further than attack range. RegressionChecks enforces the ordering.
	[Export] public float StandoffDistance { get; set; } = 250f;
	[Export] public float RetreatDistance { get; set; } = 160f;
	[Export] public float AttackRange { get; set; } = 430f;

	// A rooted caster never moves: it is a turret, and the three ranges above go unused. What that
	// buys is a different kind of pressure from a mobile shooter - it makes a piece of ground unsafe
	// rather than following you, so the answer is to route around it or spend the time to kill it.
	// Making this a field rather than a subclass is deliberate: "turret" is a tuning choice on a
	// scene, not a new behaviour, and one class is easier to keep honest than two.
	[Export] public bool Rooted { get; set; } = false;

	[Export] public float CastIntervalSeconds { get; set; } = 3.1f;
	[Export] public float CastWindUpSeconds { get; set; } = 0.75f;
	[Export] public int ProjectileDamage { get; set; } = 2;
	[Export] public float ProjectileSpeed { get; set; } = 235f;
	[Export] public string ProjectileScenePath { get; set; } = "res://scenes/EnemyProjectile.tscn";

	// A volley fans evenly around the aim line, so the middle bolt of an odd-numbered one still goes
	// straight at the player. One bolt and zero spread is a plain shot.
	[Export] public int BoltsPerVolley { get; set; } = 1;
	[Export] public float VolleySpreadDegrees { get; set; } = 0f;

	// Both the charge tell and the bolt, so a second caster type can be told apart from this one at
	// a glance rather than by its silhouette alone.
	[Export] public Color BoltBodyColor { get; set; } = new Color(0.55f, 0.24f, 0.86f);
	[Export] public Color BoltCoreColor { get; set; } = new Color(0.85f, 0.62f, 1.0f);

	/// <summary>Distance in front of the caster the bolt appears, so it never spawns inside its own body.</summary>
	[Export] public float BoltSpawnOffset { get; set; } = 18f;

	private AttackTelegraph cast;
	private PackedScene projectileScene;
	// Which way it circles. Fixed per enemy so a group of casters fans out instead of all sliding
	// the same way, and stable so an individual does not jitter left-right on the spot.
	private float strafeSign = 1f;

	public override void _Ready()
	{
		base._Ready();
		cast = new AttackTelegraph(CastIntervalSeconds, CastWindUpSeconds);
		strafeSign = (GetInstanceId() % 2 == 0) ? 1f : -1f;

		projectileScene = ResourceLoader.Load<PackedScene>(ProjectileScenePath);
		if (projectileScene == null)
			GD.PushError($"RangedEnemy '{EnemyType}': projectile scene not found at '{ProjectileScenePath}', so it will never shoot.");
	}

	// Approach, hold, or back off - and stand perfectly still while winding up, because the charge
	// glow is a promise about where the shot comes from.
	protected override Vector2 AdjustSteering(Vector2 chaseDirection, float distanceToPlayer)
	{
		if (Rooted || (cast != null && cast.IsWindingUp))
			return Vector2.Zero;

		if (distanceToPlayer > StandoffDistance)
			return chaseDirection;

		if (distanceToPlayer < RetreatDistance)
			return -chaseDirection;

		// In the band between the two it circles rather than freezing, so a caster at range is still
		// a moving target and does not read as a turret the player can ignore until convenient.
		return new Vector2(-chaseDirection.Y, chaseDirection.X) * strafeSign;
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		TickCast((float)delta);
	}

	private void TickCast(float delta)
	{
		switch (cast.Tick(delta, CanSeeTargetInRange()))
		{
			case AttackTelegraph.Beat.Started:
				PlayAttackAnimation();
				break;
			case AttackTelegraph.Beat.Resolved:
				FireVolley();
				break;
		}
	}

	private bool CanSeeTargetInRange()
	{
		Node2D target = TargetPlayer;
		if (target == null || !IsInstanceValid(target))
			return false;

		if (GlobalPosition.DistanceTo(target.GlobalPosition) > AttackRange)
			return false;

		// Distance is checked first so the line-of-sight walk only runs for casters that could
		// actually shoot. Open stages have no navigation grid and skip it entirely.
		var nav = MazeNavigation.Active;
		return nav == null || nav.HasLineOfSight(GlobalPosition, target.GlobalPosition);
	}

	private void FireVolley()
	{
		Node2D target = TargetPlayer;
		if (target == null || !IsInstanceValid(target) || projectileScene == null)
			return;

		// Parented to the arena rather than to the caster: killing it mid-flight must not delete a
		// bolt that is already the player's problem. Same reason Enemy parents its XP orbs out.
		Node arena = GetParent();
		if (arena == null)
			return;

		// Aimed where the player is standing now, with no lead. A bolt that predicts movement
		// punishes the player for reacting to the tell, which is the one thing it must never do.
		Vector2 toTarget = target.GlobalPosition - GlobalPosition;
		Vector2 aim = toTarget.LengthSquared() > 0.0001f ? toTarget.Normalized() : Vector2.Right;

		int bolts = Mathf.Max(1, BoltsPerVolley);
		float spread = Mathf.DegToRad(VolleySpreadDegrees);
		for (int i = 0; i < bolts; i++)
		{
			if (projectileScene.Instantiate() is not EnemyProjectile bolt)
			{
				GD.PushError($"RangedEnemy '{EnemyType}': '{ProjectileScenePath}' has no EnemyProjectile script attached.");
				return;
			}

			bolt.Damage = ProjectileDamage;
			bolt.Speed = ProjectileSpeed;
			bolt.BodyColor = BoltBodyColor;
			bolt.CoreColor = BoltCoreColor;

			Vector2 heading = aim.Rotated(VolleyOffsetFraction(i, bolts) * spread);
			bolt.LaunchInDirection(GlobalPosition + heading * BoltSpawnOffset, heading);
			arena.CallDeferred("add_child", bolt);
		}
	}

	// Where bolt i of n sits across the fan, as a fraction of the total spread in [-0.5, 0.5]. A
	// single bolt, and the middle of any odd volley, comes out at 0 - dead on the aim line.
	private static float VolleyOffsetFraction(int index, int count)
	{
		return count <= 1 ? 0f : (index / (float)(count - 1)) - 0.5f;
	}

	// A recycled caster starts its cooldown over. Without this, an enemy relocated by
	// Node2DGame.RespawnEnemy arrives at the spawn ring with a cooldown it already spent somewhere
	// off-screen, and gets a free shot before the player has seen it.
	public override void ResetForRespawn(Vector2 newPos, int newHealth)
	{
		base.ResetForRespawn(newPos, newHealth);
		cast = new AttackTelegraph(CastIntervalSeconds, CastWindUpSeconds);
	}

	public override void _Draw()
	{
		if (cast == null || !cast.IsWindingUp)
			return;

		// The tell. A mote gathers in front of the caster and brightens as the shot nears, and a
		// short aim tick shows which way it will go - together they read as "step sideways", which
		// is the answer. Enemy already calls QueueRedraw every physics frame, so this costs nothing
		// beyond the draw itself, and the geometry allocates nothing.
		float progress = cast.WindUpProgress;
		Node2D target = TargetPlayer;
		Vector2 aim = Vector2.Right;
		if (target != null && IsInstanceValid(target))
		{
			Vector2 local = ToLocal(target.GlobalPosition);
			if (local.LengthSquared() > 0.0001f)
				aim = local.Normalized();
		}

		Vector2 muzzle = aim * BoltSpawnOffset;
		float radius = Mathf.Lerp(2.5f, 9.0f, progress);

		var halo = new Color(BoltBodyColor.R, BoltBodyColor.G, BoltBodyColor.B, 0.20f + 0.25f * progress);
		DrawCircle(muzzle, radius * 2.1f, halo);
		DrawCircle(muzzle, radius, BoltBodyColor);
		DrawCircle(muzzle, radius * 0.45f, BoltCoreColor);

		// The aim ticks fade in over the second half only, so the first half of the wind-up says
		// "a shot is coming" and the second says "from here to there". One tick per bolt: a volley
		// that showed a single line and then arrived as a fan would be a lie about where it is safe
		// to stand, which is the whole thing this tell exists to tell the truth about.
		if (progress > 0.5f)
		{
			float lineAlpha = (progress - 0.5f) * 2f * 0.55f;
			var tickColor = new Color(BoltCoreColor.R, BoltCoreColor.G, BoltCoreColor.B, lineAlpha);
			int bolts = Mathf.Max(1, BoltsPerVolley);
			float spread = Mathf.DegToRad(VolleySpreadDegrees);
			for (int i = 0; i < bolts; i++)
			{
				Vector2 heading = aim.Rotated(VolleyOffsetFraction(i, bolts) * spread);
				DrawLine(heading * BoltSpawnOffset, heading * (BoltSpawnOffset + 46f), tickColor, 2.0f, true);
			}
		}
	}
}
