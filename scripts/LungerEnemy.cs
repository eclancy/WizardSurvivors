using Godot;
using WizardSurvivors.scripts;

// Burst chase (issue #33). It walks slower than an ordinary chaser, plants, shows the player which
// way it is about to go, then covers ground far faster than anything else in the roster - and is
// left winded and slow afterwards.
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

	[Export] public Color TellColor { get; set; } = new Color(1.0f, 0.42f, 0.24f);

	private AttackTelegraph lunge;
	// The direction is committed the frame the wind-up ends, and never re-aimed. A dash that
	// tracked the player mid-flight would make the tell a lie about where it is safe to stand.
	private Vector2 lungeDirection = Vector2.Zero;
	private float lungeRemaining;
	private float recoveryRemaining;
	private bool repositioning;
	// Captured after Node2DGame has applied its run scaling and elite multipliers, so the dash is
	// a multiple of the speed this individual actually walks at.
	private float baseSpeed;

	public override void _Ready()
	{
		base._Ready();
		baseSpeed = Speed;
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

	public override void _Draw()
	{
		// The tell is a line, not a ring: this attack is dangerous along one heading and harmless
		// two steps to either side, and the drawing has to say which. Enemy already calls
		// QueueRedraw every physics frame, so this costs nothing beyond the draw itself.
		if (lunge == null || !lunge.IsWindingUp)
			return;

		Node2D target = TargetPlayer;
		Vector2 aim = Vector2.Right;
		if (target != null && IsInstanceValid(target))
		{
			Vector2 local = ToLocal(target.GlobalPosition);
			if (local.LengthSquared() > 0.0001f)
				aim = local.Normalized();
		}

		float progress = lunge.WindUpProgress;
		float reach = baseSpeed * LungeSpeedMultiplier * LungeDurationSeconds;
		// The streak grows to the distance the dash actually covers, so its length is a promise
		// about where the charge ends rather than decoration.
		Vector2 tip = aim * reach * progress;

		var track = new Color(TellColor.R, TellColor.G, TellColor.B, 0.16f + 0.30f * progress);
		DrawLine(Vector2.Zero, tip, track, 7.0f, true);
		DrawCircle(tip, Mathf.Lerp(3.0f, 8.0f, progress), new Color(TellColor.R, TellColor.G, TellColor.B, 0.55f));
		// A brace ring under its feet, so a lunger winding up inside a crowd is still findable.
		DrawArc(Vector2.Zero, Mathf.Lerp(26f, 16f, progress), 0f, Mathf.Tau, 24, new Color(TellColor.R, TellColor.G, TellColor.B, 0.75f), 2.5f, true);
	}
}
