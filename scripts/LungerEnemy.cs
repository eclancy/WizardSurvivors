using Godot;
using WizardSurvivors.scripts;

// Burst chase (issue #33). It walks slower than an ordinary chaser, plants, shows a claw over its
// head that fills as it winds up (TelegraphIcon), then dashes at the player far faster than anything
// else in the roster - and is left winded and slow afterwards.
//
// What it adds to a run is a reason to keep moving *before* you are cornered. A plain chaser is
// outrun by walking away from it; this one cannot be, so the answer is to not be standing on the
// line it charges down. That is a positioning rule the swarm alone never teaches.
//
// The trade is deliberately generous. The wind-up is long, the direction is locked at the moment it
// starts, and the recovery is longer than the dash - so a player who reads the tell steps aside and
// gets a free window on a slow target, while a player who ignores it takes the hit and keeps taking
// it. Nothing here should ever catch someone who was already moving sideways.
public partial class LungerEnemy : Enemy
{
	[Export] public float LungeIntervalSeconds { get; set; } = 3.4f;
	[Export] public float LungeWindUpSeconds { get; set; } = 0.7f;
	[Export] public float LungeDurationSeconds { get; set; } = 0.42f;
	[Export] public float LungeSpeedMultiplier { get; set; } = 4.2f;

	// After the dash it is slower than its own walk for this long: the punish window. Longer than
	// the dash on purpose - the charge should cost it something even when it connects.
	[Export] public float RecoverySeconds { get; set; } = 0.85f;
	[Export] public float RecoverySpeedMultiplier { get; set; } = 0.35f;

	/// <summary>Beyond this it keeps walking; a charge from off-screen is not a tell, it is a surprise.</summary>
	[Export] public float LungeRange { get; set; } = 420f;

	/// <summary>Closer than this it cannot charge - a dash that starts on top of you cannot be stepped out of.</summary>
	[Export] public float MinimumLungeDistance { get; set; } = 90f;

	// Having come out of a charge too close to start another, it backs off until there is room.
	//
	// This is not decoration, it is what keeps the enemy being itself. It closes roughly 310px
	// during one cooldown, which is wider than the whole band it is allowed to charge from - so
	// without a reposition it walks into contact range after its first dash and spends the rest of
	// its life as an ordinary, unusually slow melee chaser. Backing off makes it hit-and-run, which
	// is the only thing in the roster that fights that way.
	[Export] public float RepositionSpeedMultiplier { get; set; } = 0.8f;

	/// <summary>Stops backing off at this multiple of the minimum distance. Above 1 so it does not jitter on the boundary.</summary>
	[Export] public float RepositionExitMultiplier { get; set; } = 1.6f;

	private AttackTelegraph lunge;
	// The direction is committed the frame the wind-up ends, and never re-aimed. A dash that
	// tracked the player mid-flight would make the tell a lie about where it is safe to stand.
	private Vector2 lungeDirection = Vector2.Zero;
	private float lungeRemaining;
	private float recoveryRemaining;
	private bool repositioning;

	public override void _Ready()
	{
		base._Ready();
		lunge = new AttackTelegraph(LungeIntervalSeconds, LungeWindUpSeconds);
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		TickLunge((float)delta);
	}

	// Four states share one hook, in the order they happen: planted through the wind-up, locked to
	// the committed heading through the dash, winded through the recovery, then backing off if it
	// ended up too close to charge again. Speed is expressed by the length of the returned vector
	// rather than by mutating Speed, so nothing else in Enemy has to know a dash is happening.
	protected override Vector2 AdjustSteering(Vector2 chaseDirection, float distanceToPlayer)
	{
		if (lunge != null && lunge.IsWindingUp)
			return Vector2.Zero;

		if (lungeRemaining > 0f)
			return lungeDirection * LungeSpeedMultiplier;

		if (recoveryRemaining > 0f)
			return chaseDirection * RecoverySpeedMultiplier;

		// Hysteresis, not a bare distance test: it enters the back-off inside the minimum and only
		// leaves it well outside, so a lunger sitting at the boundary drifts out to charging range
		// instead of twitching in and out of reverse every frame.
		if (repositioning)
			repositioning = distanceToPlayer < MinimumLungeDistance * RepositionExitMultiplier;
		else
			repositioning = distanceToPlayer < MinimumLungeDistance;

		if (repositioning)
			return -chaseDirection * RepositionSpeedMultiplier;

		return chaseDirection;
	}

	private void TickLunge(float delta)
	{
		if (lungeRemaining > 0f)
		{
			lungeRemaining -= delta;
			if (lungeRemaining <= 0f)
			{
				lungeRemaining = 0f;
				recoveryRemaining = RecoverySeconds;
			}
			QueueRedraw();
			return;
		}

		if (recoveryRemaining > 0f)
			recoveryRemaining = Mathf.Max(0f, recoveryRemaining - delta);

		switch (lunge.Tick(delta, WantsToLunge()))
		{
			case AttackTelegraph.Beat.Started:
				PlayAttackAnimation();
				break;
			case AttackTelegraph.Beat.Resolved:
				StartLunge();
				break;
		}
	}

	private bool WantsToLunge()
	{
		Node2D target = TargetPlayer;
		if (target == null || !IsInstanceValid(target))
			return false;

		float distance = GlobalPosition.DistanceTo(target.GlobalPosition);
		return distance <= LungeRange && distance >= MinimumLungeDistance;
	}

	private void StartLunge()
	{
		Node2D target = TargetPlayer;
		if (target == null || !IsInstanceValid(target))
			return;

		Vector2 toTarget = target.GlobalPosition - GlobalPosition;
		lungeDirection = toTarget.LengthSquared() > 0.0001f ? toTarget.Normalized() : Vector2.Right;
		lungeRemaining = LungeDurationSeconds;
	}

	// A recycled lunger starts over. Without this, one relocated by Node2DGame.RespawnEnemy could
	// arrive at the spawn ring mid-dash, travelling at four times its speed in a direction it
	// committed to somewhere off-screen.
	public override void ResetForRespawn(Vector2 newPos, int newHealth)
	{
		base.ResetForRespawn(newPos, newHealth);
		lunge = new AttackTelegraph(LungeIntervalSeconds, LungeWindUpSeconds);
		lungeRemaining = 0f;
		recoveryRemaining = 0f;
		repositioning = false;
		lungeDirection = Vector2.Zero;
	}

	// The tell is the claw over its head (TelegraphIcon), filling through the wind-up and flashing as it
	// goes. It used to be a translucent streak growing along the floor toward the player plus a brace
	// ring under its feet - the "jump attacks" Eric named in 0.9.2 ("transparent buildups animations
	// are weird... remove them"). The streak never said anything the claw does not: the dash is aimed
	// at the player on the frame the wind-up ends, so "it is coming at me, now" is the whole read, and
	// the answer is the same sideways step.
	protected override bool ReadAttackTell(out TelegraphGlyph glyph, out float progress)
	{
		glyph = TelegraphGlyph.Leap;
		progress = lunge?.WindUpProgress ?? 0f;
		return lunge != null && lunge.IsWindingUp;
	}
}
