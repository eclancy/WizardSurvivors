using Godot;
using WizardSurvivors.scripts;

// An enemy that keeps its distance and throws bolts instead of closing. Every other enemy in the
// roster is the same creature mechanically - run at the player, damage on touch - which left a run
// with exactly one positioning rule: do not stand in the swarm. This one adds a second: something in
// the back is aiming at you, and the swarm in front is what stops you reaching it.
//
// The design is a trade the player can always take. It is fragile, it plants itself for a visible
// wind-up before every shot, and its bolt is slow enough to walk out of - so a player who reads the
// tell pays nothing, and a player who ignores the back line pays steadily.
//
// This used to say "nothing here should ever hit someone who was already moving", and the shot was
// aimed with no lead at all. That rule made the caster the one enemy a kiting player could ignore
// completely: circle at a constant speed and every bolt lands where you were. The rule it is
// replaced by keeps what the original was protecting - reading the tell must still save you - by
// making the aim tick show the *led* heading. So the tell is still the whole truth about where the
// bolt is going; what no longer works is holding one heading and being immune for free. The lead is
// deliberately partial (see BoltLeadFraction), so changing speed or direction still beats it.
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

	/// <summary>
	/// How much of the full interception lead to take, 0 = aim where the player stands, 1 = aim at
	/// where they would be when the bolt lands.
	/// </summary>
	/// <remarks>
	/// A fraction of flight time rather than a fixed number of seconds, because a fixed cap is a
	/// no-op at the ranges these actually fire from: a bolt covers 300px in about 1.3s, over which
	/// a running player travels nearly 290px, so leading by a "generous" 0.45s still missed by
	/// almost 200px and changed nothing. Measured - a straight-line player took zero hits either
	/// way.
	///
	/// Kept below 1 so the shot is always a little behind a perfect intercept. That is affordable
	/// here precisely because the bolt is slow: over a 1.3s flight any change of direction after
	/// release beats it comfortably, so a led shot punishes holding one heading without ever being
	/// unavoidable. Set to 0 to restore the original no-lead behaviour exactly.
	/// </remarks>
	[Export] public float BoltLeadFraction { get; set; } = 0.75f;

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

		// The same aim the wind-up drew. Sharing one method is the point: if the bolt led and the
		// tick did not, the tell would be a lie about where it is safe to stand.
		Vector2 aim = ComputeAimDirection(target);

		// One shot cue per volley, not per bolt: a three-bolt spread is one action.
		SfxPlayer.AtPosition(SfxCatalog.EnemyShoot, GlobalPosition, 0.07f);
		int bolts = Mathf.Max(1, BoltsPerVolley);
		float spread = Mathf.DegToRad(VolleySpreadDegrees);
		for (int i = 0; i < bolts; i++)
		{
			if (projectileScene.Instantiate() is not EnemyProjectile bolt)
			{
				GD.PushError($"RangedEnemy '{EnemyType}': '{ProjectileScenePath}' has no EnemyProjectile script attached.");
				return;
			}

			bolt.Damage = ScaleOutgoingDamage(ProjectileDamage);
			bolt.Speed = ProjectileSpeed;
			bolt.BodyColor = BoltBodyColor;
			bolt.CoreColor = BoltCoreColor;

			Vector2 heading = aim.Rotated(VolleyOffsetFraction(i, bolts) * spread);
			bolt.LaunchInDirection(GlobalPosition + heading * BoltSpawnOffset, heading);
			arena.CallDeferred("add_child", bolt);
		}
	}

	/// <summary>The heading this caster is aiming along, leading the target by up to
	/// <see cref="BoltLeadFraction"/> of the way to a true intercept. Used by both the shot and
	/// the tell it draws.</summary>
	private Vector2 ComputeAimDirection(Node2D target)
	{
		Vector2 aimPoint = target.GlobalPosition;
		if (BoltLeadFraction > 0f && ProjectileSpeed > 0f)
		{
			// One pass, not an iterative intercept solve. The aim point moves as we lead, so a
			// single pass already lands short of a true intercept - which is the margin we want.
			float flightTime = GlobalPosition.DistanceTo(aimPoint) / ProjectileSpeed;
			aimPoint += MeasuredPlayerVelocity * flightTime * BoltLeadFraction;
		}

		Vector2 toTarget = aimPoint - GlobalPosition;
		return toTarget.LengthSquared() > 0.0001f ? toTarget.Normalized() : Vector2.Right;
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
		base._Draw();
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
			// The led heading, in local space. Exactly what FireVolley will use, so the tick the
			// player is reading is the line the bolt actually takes.
			Vector2 world = ComputeAimDirection(target);
			aim = ToLocal(GlobalPosition + world).Normalized();
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
