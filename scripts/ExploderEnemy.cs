using Godot;
using WizardSurvivors.scripts;

// Suicide detonation (issue #33). It runs at the player faster than an ordinary chaser, and once it
// is close it plants, swells a warning ring, and blows itself apart.
//
// What it adds is a cost to letting things reach you. The swarm's ordinary answer - stand in a good
// spot and let contact damage tick while your spells clear the pile - stops being free, because one
// of the things in that pile is counting down. It makes spacing a decision rather than a habit.
//
// Two rules keep it fair, and both are deliberate:
//
// - Killing it during the wind-up cancels the blast entirely. Enemy.StartDeath turns physics off,
//   so the telegraph simply stops ticking and never resolves. An exploder that detonated on death
//   would punish the player for correctly identifying and bursting the dangerous thing, which is
//   the exact opposite of what this enemy is for.
// - It drops nothing when it detonates. The player did not kill it, so it does not pay out - and a
//   detonation that fed the XP curve would quietly make ignoring it the optimal play.
public partial class ExploderEnemy : Enemy
{
	[Export] public float FuseSeconds { get; set; } = 0.85f;
	[Export] public float BlastRadius { get; set; } = 110f;
	[Export] public int BlastDamage { get; set; } = 5;

	/// <summary>How close it must get before the fuse starts, as a multiple of the blast radius.</summary>
	[Export] public float TriggerRadiusMultiplier { get; set; } = 0.85f;

	[Export] public Color BlastWarningColor { get; set; } = new Color(0.95f, 0.35f, 0.35f);

	/// <summary>Seconds the spent blast ring lingers before the body is freed.</summary>
	[Export] public float BlastLingerSeconds { get; set; } = 0.3f;

	private GroundSlamAttack blast;
	private bool detonated;
	private float lingerRemaining;

	public override void _Ready()
	{
		base._Ready();
		blast = BuildBlast();
	}

	// The interval is the shortest the telegraph allows rather than something long meaning "once".
	// AttackTelegraph deliberately starts on a full cooldown so nothing attacks the frame it
	// spawns, so a nominal interval of 9999 does not mean "fires once" - it means the fuse is
	// never lit at all, and the enemy is a slightly fast chaser forever. The fuse only ever burns
	// once because Detonate ends the enemy, not because the cooldown is long.
	private GroundSlamAttack BuildBlast()
	{
		return new GroundSlamAttack(0.05f, FuseSeconds, BlastRadius, BlastDamage, BlastWarningColor);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (detonated)
		{
			// The body is inert but still on screen: age the ring out by hand, because base
			// _PhysicsProcess - which is what normally drives QueueRedraw - is skipped below.
			lingerRemaining -= (float)delta;
			blast.Tick((float)delta, false);
			QueueRedraw();
			if (lingerRemaining <= 0f)
				QueueFree();
			return;
		}

		base._PhysicsProcess(delta);
		TickFuse((float)delta);
	}

	// Planted once the fuse is lit. It must not drift: the ring is drawn at its feet and the blast
	// is measured from them, so a sliding exploder would detonate somewhere it never warned about.
	protected override Vector2 AdjustSteering(Vector2 chaseDirection, float distanceToPlayer)
	{
		return blast != null && blast.IsWindingUp ? Vector2.Zero : chaseDirection;
	}

	private void TickFuse(float delta)
	{
		Node2D target = TargetPlayer;
		bool inTriggerRange = target != null
			&& IsInstanceValid(target)
			&& GlobalPosition.DistanceTo(target.GlobalPosition) <= BlastRadius * TriggerRadiusMultiplier;

		switch (blast.Tick(delta, inTriggerRange))
		{
			case AttackTelegraph.Beat.Started:
				PlayAttackAnimation();
				// Hot and bright for the length of the fuse, so an exploder mid-crowd is findable
				// by colour as well as by the ring under it.
				SetBaseModulate(new Color(1.0f, 0.55f, 0.45f));
				break;
			case AttackTelegraph.Beat.Resolved:
				Detonate();
				break;
		}
	}

	private void Detonate()
	{
		if (detonated)
			return;

		detonated = true;
		lingerRemaining = Mathf.Max(0.05f, BlastLingerSeconds);
		blast.ResolveAgainst(this, TargetPlayer);

		// Leave the fight in the same order Enemy.StartDeath does - out of the group first, so an
		// AoE sweep or auto-target running later this frame skips a corpse that is already spent -
		// but without its rewards. See the class comment.
		if (IsInGroup("enemies"))
			RemoveFromGroup("enemies");
		Velocity = Vector2.Zero;
		CollisionLayer = 0;
		CollisionMask = 0;
	}

	public override void ResetForRespawn(Vector2 newPos, int newHealth)
	{
		base.ResetForRespawn(newPos, newHealth);
		blast = BuildBlast();
		detonated = false;
		lingerRemaining = 0f;
	}

	public override void _Draw() => blast?.Draw(this);
}
