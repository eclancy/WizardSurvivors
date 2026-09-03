using Godot;
using System;
using WizardSurvivors.scripts;

// The stage boss. Everything about chasing, damage, status and death already lives in Enemy, so
// this only adds what makes a boss a boss: an identity the run result can record, a telegraphed
// ground-slam that punishes standing next to it, an enrage below a third health, and a signal the
// run listens for so a kill can end the level in victory.
//
// The first boss (Elderbark, the Treant) is deliberately the simplest of the three shapes we
// considered: a slow, immense melee threat that rewards sustained damage and punishes a
// glass-cannon burst build that cannot hold the line. It needs no new targeting or projectile
// code, which is why it goes first.
public partial class BossEnemy : Enemy
{
	[Signal] public delegate void BossDefeatedEventHandler(string bossId);

	[Export] public string BossId { get; set; } = string.Empty;
	[Export] public string BossDisplayName { get; set; } = "Boss";

	// Ground slam: the boss plants its feet, a warning ring grows for the telegraph, then everything
	// inside the ring takes a heavy hit. The telegraph is the whole fight - the slam is only unfair
	// if the player cannot see it coming.
	[Export] public float SlamIntervalSeconds { get; set; } = 4.6f;
	[Export] public float SlamTelegraphSeconds { get; set; } = 0.85f;
	[Export] public float SlamRadius { get; set; } = 150f;
	[Export] public int SlamDamage { get; set; } = 4;

	// Below this fraction of max health the boss speeds up and slams harder. One threshold, not
	// three phases: a first boss should teach "it gets worse near the end", not a dance routine.
	[Export] public float EnrageHealthFraction { get; set; } = 0.34f;
	[Export] public float EnrageSpeedMultiplier { get; set; } = 1.35f;
	[Export] public float EnrageSlamIntervalMultiplier { get; set; } = 0.66f;

	// Cooldown and wind-up both live in the shared telegraph clock, which RangedEnemy runs too.
	private AttackTelegraph slam;
	// Seconds since the slam landed, used to fade the impact ring out. Negative = no ring to draw.
	private float impactFlashRemaining = -1f;
	private const float ImpactFlashSeconds = 0.28f;
	private bool enraged;
	private bool defeatAnnounced;

	public override void _Ready()
	{
		// Bosses are always elites as far as the rest of the game is concerned: the gold ring marker,
		// the fat XP drop and the elite headcount all key off this. Set before base._Ready, which is
		// where Enemy builds the marker.
		IsMiniBoss = true;
		base._Ready();
		slam = new AttackTelegraph(SlamIntervalSeconds, SlamTelegraphSeconds);
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		TickEnrage();
		TickSlam((float)delta);
	}

	// Planting the boss for its wind-up is the base class's job now: returning Zero holds it exactly
	// where the warning ring is drawn, which is the only place the slam is honest.
	protected override Vector2 AdjustSteering(Vector2 chaseDirection, float distanceToPlayer)
	{
		return slam != null && slam.IsWindingUp ? Vector2.Zero : chaseDirection;
	}

	private void TickEnrage()
	{
		if (enraged || HealthFraction > EnrageHealthFraction)
			return;

		enraged = true;
		Speed *= EnrageSpeedMultiplier;
		SlamIntervalSeconds *= EnrageSlamIntervalMultiplier;
		slam.IntervalSeconds = SlamIntervalSeconds;
		// Darker and hotter, so the change in pace has a visual cause rather than feeling like a bug.
		SetBaseModulate(new Color(1.0f, 0.62f, 0.52f));
	}

	private void TickSlam(float delta)
	{
		if (impactFlashRemaining >= 0f)
			impactFlashRemaining -= delta;

		Node2D target = TargetPlayer;
		// Only wind up when the player is close enough that the slam could plausibly land, otherwise
		// the boss spends the fight rooted in place slamming empty ground.
		bool inSlamRange = target != null
			&& IsInstanceValid(target)
			&& GlobalPosition.DistanceTo(target.GlobalPosition) <= SlamRadius * 2.2f;

		switch (slam.Tick(delta, inSlamRange))
		{
			case AttackTelegraph.Beat.Started:
				PlayAttackAnimation();
				break;
			case AttackTelegraph.Beat.Resolved:
				ResolveSlam();
				break;
		}
	}

	private void ResolveSlam()
	{
		impactFlashRemaining = ImpactFlashSeconds;

		Node2D target = TargetPlayer;
		if (target == null || !IsInstanceValid(target))
			return;

		if (GlobalPosition.DistanceTo(target.GlobalPosition) > SlamRadius)
			return;

		// Duck-typed like every other damage call in this project: the boss does not need to know
		// what a Player is, only that whatever it is standing on can take a hit.
		if (target.HasMethod("TakeDamage"))
			target.Call("TakeDamage", SlamDamage);
	}

	public override void _Draw()
	{
		// The growing warning ring. Drawn on the boss itself rather than as a separate node so a
		// slam allocates nothing - this runs in a scene that already has a swarm in it.
		if (slam != null && slam.IsWindingUp)
		{
			float progress = slam.WindUpProgress;
			float radius = Mathf.Lerp(SlamRadius * 0.35f, SlamRadius, progress);
			var warning = new Color(1.0f, 0.45f, 0.20f, Mathf.Lerp(0.35f, 0.85f, progress));
			DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 48, warning, 3.5f, true);
			DrawCircle(Vector2.Zero, radius, new Color(1.0f, 0.35f, 0.12f, 0.10f));
			return;
		}

		if (impactFlashRemaining > 0f)
		{
			float fade = Mathf.Clamp(impactFlashRemaining / ImpactFlashSeconds, 0f, 1f);
			DrawArc(Vector2.Zero, SlamRadius, 0f, Mathf.Tau, 48, new Color(1.0f, 0.85f, 0.55f, fade), 6f * fade, true);
		}
	}

	protected override void StartDeath()
	{
		bool alreadyDying = defeatAnnounced;
		defeatAnnounced = true;
		base.StartDeath();

		// Guard the emit rather than the call: base.StartDeath is itself re-entrant-safe, but the
		// run must never be told it won twice.
		if (!alreadyDying)
			EmitSignal(SignalName.BossDefeated, BossId);
	}
}
