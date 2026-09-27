using Godot;
using System;
using WizardSurvivors.scripts;

// The stage boss. Everything about chasing, damage, status and death already lives in Enemy, so
// this only adds what makes a boss a boss: an identity the run result can record, a telegraphed
// attack that punishes standing in the wrong place, health thresholds that change how it fights,
// and a signal the run listens for so a kill can end the level in victory.
//
// Used bare, it is Elderbark: a slow, immense melee threat with one ground slam and one enrage. It
// is also the base every other chapter boss subclasses, and the division of labour between them is
// deliberate. THIS CLASS OWNS THE CLOCK - cooldown, wind-up, phase thresholds, the victory signal -
// and a subclass owns only what is different about its fight. A boss that needed its own copy of
// any of the above would be the third place in the project where a wind-up got written by hand.
//
// The five hooks a subclass has, and nothing else:
//
//   BuildSlam()            what the telegraphed attack IS - shape, reach, damage, colour
//   BuildPhaseThresholds() the health fractions where the fight changes
//   OnPhaseEntered(n)      what changes at each one
//   PlantsDuringWindUp     false for a boss that keeps moving through its own tell (a charge)
//   WantsToAttack(...)     when the wind-up is allowed to begin
//
// See .ai/enemy-behaviour.md for the eight chapter sketches and which primitive each needs.
public partial class BossEnemy : Enemy
{
	[Signal] public delegate void BossDefeatedEventHandler(string bossId);

	[Export] public string BossId { get; set; } = string.Empty;
	[Export] public string BossDisplayName { get; set; } = "Boss";

	// Ground slam: the boss plants its feet, a warning grows for the telegraph, then everything
	// inside it takes a heavy hit. The telegraph is the whole fight - the slam is only unfair if the
	// player cannot see it coming.
	[Export] public float SlamIntervalSeconds { get; set; } = 4.6f;
	[Export] public float SlamTelegraphSeconds { get; set; } = 0.85f;
	[Export] public float SlamRadius { get; set; } = 150f;
	[Export] public int SlamDamage { get; set; } = 4;

	// Below this fraction of max health the boss speeds up and slams harder. One threshold by
	// default, not three phases: the first boss should teach "it gets worse near the end", not a
	// dance routine. Subclasses that want a real second phase override BuildPhaseThresholds.
	[Export] public float EnrageHealthFraction { get; set; } = 0.34f;
	[Export] public float EnrageSpeedMultiplier { get; set; } = 1.35f;
	[Export] public float EnrageSlamIntervalMultiplier { get; set; } = 0.66f;

	// How far out the boss will commit to a wind-up, as a multiple of its reach. Above 1 so it
	// starts while the player is still approaching.
	[Export] public float SlamEngageRadiusMultiplier { get; set; } = 2.2f;

	// Cooldown, wind-up, warning shape and the hit itself all live in the shared telegraph, which
	// SlammerEnemy and ExploderEnemy run too.
	private GroundSlamAttack slam;
	private float[] phaseThresholds = Array.Empty<float>();
	private bool defeatAnnounced;

	/// <summary>The telegraphed attack, for subclasses that re-aim or retune it mid-fight.</summary>
	protected GroundSlamAttack Slam => slam;

	/// <summary>
	/// How many thresholds have been crossed. 0 is the opening fight; 1 is the enrage on a plain
	/// boss. Read it in <c>_PhysicsProcess</c> to gate behaviour a later phase adds.
	/// </summary>
	protected int CurrentPhase { get; private set; }

	/// <summary>The same number, readable from outside. For the HUD and for _BossProbe.</summary>
	public int BossPhase => CurrentPhase;

	/// <summary>
	/// What the boss health bar shows, 0 to 1. Its own health for every ordinary boss, and the
	/// whole set for a boss that is more than one body.
	/// </summary>
	/// <remarks>
	/// The HUD reads this rather than <c>HealthFraction</c> because the Hollow Choir is three linked
	/// bodies and the bar has to mean the fight rather than whichever third the run holds a
	/// reference to - a bar that jumped back to full on a revive would be lying about progress the
	/// player really had made.
	/// </remarks>
	public virtual float BossHealthFraction => HealthFraction;

	public override void _Ready()
	{
		// Bosses are always elites as far as the rest of the game is concerned: the gold ring marker,
		// the fat XP drop and the elite headcount all key off this. Set before base._Ready, which is
		// where Enemy builds the marker.
		IsMiniBoss = true;
		base._Ready();
		slam = BuildSlam();
		phaseThresholds = BuildPhaseThresholds() ?? Array.Empty<float>();
	}

	/// <summary>
	/// The telegraphed attack this boss opens with. Override to change its shape - a cone or a lane
	/// tells the player which way to dodge, which a ring cannot.
	/// </summary>
	protected virtual GroundSlamAttack BuildSlam() =>
		new(SlamIntervalSeconds, SlamTelegraphSeconds, SlamRadius, SlamDamage, new Color(1.0f, 0.45f, 0.20f));

	/// <summary>
	/// Health fractions, descending, at which the fight changes. The default is the single enrage
	/// threshold, so a boss that overrides nothing behaves exactly as Elderbark always has.
	/// </summary>
	protected virtual float[] BuildPhaseThresholds() => new[] { EnrageHealthFraction };

	/// <summary>
	/// Called once when the boss crosses into phase <paramref name="phase"/> (1-based). The default
	/// is the enrage: faster on its feet, faster on its slam, and visibly hotter so the change in
	/// pace has a cause rather than feeling like a bug.
	/// </summary>
	protected virtual void OnPhaseEntered(int phase) => ApplyEnrage();

	/// <summary>
	/// Whether the boss holds still through its wind-up. True for anything that draws its warning on
	/// the ground it is standing on; false for a charge, which carries the warning with it.
	/// </summary>
	/// <remarks>
	/// This is the whole of the travelling-telegraph primitive. <see cref="GroundSlamAttack"/>
	/// already draws on the caster and resolves at the caster's position on the frame it lands, so
	/// a boss that simply does not plant has a moving attack with no new machinery at all.
	/// </remarks>
	protected virtual bool PlantsDuringWindUp => true;

	/// <summary>
	/// Whether a wind-up may begin this frame. The default only commits when the player is close
	/// enough that the attack could plausibly land, so the boss does not spend the fight rooted in
	/// place slamming empty ground.
	/// </summary>
	protected virtual bool WantsToAttack(Node2D target, float distanceToPlayer) =>
		distanceToPlayer <= SlamRadius * SlamEngageRadiusMultiplier;

	/// <summary>Called on the frame the wind-up begins. Aim here, not during it.</summary>
	protected virtual void OnAttackStarted(Node2D target)
	{
		PlayAttackAnimation();
		if (target != null && IsInstanceValid(target))
			slam.AimAt(GlobalPosition, target.GlobalPosition);
	}

	/// <summary>Called on the frame the attack lands, exactly once per wind-up.</summary>
	protected virtual void OnAttackResolved(Node2D target) => slam.ResolveAgainst(this, target);

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		TickPhases();
		TickSlam((float)delta);
	}

	// Planting the boss for its wind-up is the base class's job: returning Zero holds it exactly
	// where the warning is drawn, which is the only place the attack is honest. A boss that opts out
	// of planting keeps chasing, and the warning travels with it.
	protected override Vector2 AdjustSteering(Vector2 chaseDirection, float distanceToPlayer)
	{
		return PlantsDuringWindUp && slam != null && slam.IsWindingUp ? Vector2.Zero : chaseDirection;
	}

	// Thresholds are crossed in order and only ever forward: a boss healed back above one does not
	// un-enrage, because a phase that can be undone teaches the player nothing.
	private void TickPhases()
	{
		while (CurrentPhase < phaseThresholds.Length && HealthFraction <= phaseThresholds[CurrentPhase])
		{
			CurrentPhase++;
			OnPhaseEntered(CurrentPhase);
		}
	}

	/// <summary>The default phase change: faster, harder, hotter. Callable from an override.</summary>
	protected void ApplyEnrage()
	{
		Speed *= EnrageSpeedMultiplier;
		SlamIntervalSeconds *= EnrageSlamIntervalMultiplier;
		if (slam != null)
			slam.IntervalSeconds = SlamIntervalSeconds;
		SetBaseModulate(new Color(1.0f, 0.62f, 0.52f));
	}

	private void TickSlam(float delta)
	{
		Node2D target = TargetPlayer;
		bool valid = target != null && IsInstanceValid(target);
		float distance = valid ? GlobalPosition.DistanceTo(target.GlobalPosition) : float.MaxValue;

		switch (slam.Tick(delta, valid && WantsToAttack(target, distance)))
		{
			case AttackTelegraph.Beat.Started:
				OnAttackStarted(target);
				break;
			case AttackTelegraph.Beat.Resolved:
				OnAttackResolved(target);
				break;
		}
	}

	// The warning is drawn on the boss itself rather than as a separate node, so an attack
	// allocates nothing - this runs in a scene that already has a swarm in it.
	public override void _Draw() => slam?.Draw(this);

	// A boss death is a run-defining event, so it skips the throttling and the heavy/small
	// split that ordinary deaths go through and plays its own sound outright.
	protected override void PlayDeathSound()
	{
		SfxPlayer.AtPosition(SfxCatalog.BossDeath, GlobalPosition, 0.0f);
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
