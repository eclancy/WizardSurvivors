using Godot;
using WizardSurvivors.scripts;

// Stop-and-attack (issue #33). It closes like an ordinary chaser, and once it is near enough it
// plants, grows a warning ring, and hits everything inside the ring - including ground it is not
// standing on the far side of.
//
// The point is that its threat has a shape. Every other melee enemy in the roster damages you by
// occupying the same pixel as you, so "do not touch it" is the whole rule and backing off one step
// is always enough. This one reaches, which means backing off one step is *not* enough, and the
// player has to read a radius rather than a silhouette. It is the ground-level version of the
// lesson Elderbark's slam teaches at the end of a stage, met at minute five instead of minute
// fifteen.
//
// It is slow and it plants for the whole wind-up, so it is also the easiest thing in the roster to
// simply walk away from. That is the trade: it punishes standing and fighting in one spot, and
// costs nothing at all to anyone who keeps circling.
public partial class SlammerEnemy : Enemy
{
	[Export] public float SlamIntervalSeconds { get; set; } = 3.2f;
	[Export] public float SlamTelegraphSeconds { get; set; } = 0.75f;
	[Export] public float SlamRadius { get; set; } = 96f;
	[Export] public int SlamDamage { get; set; } = 3;

	// How far out it will begin a wind-up, as a multiple of the radius. Above 1 so it commits while
	// the player is still approaching - a slam that only starts once you are already inside the
	// ring gives you the wind-up to walk out of a hit you had no chance to avoid taking.
	[Export] public float SlamEngageRadiusMultiplier { get; set; } = 1.5f;

	[Export] public Color SlamWarningColor { get; set; } = new Color(0.85f, 0.55f, 0.25f);

	private GroundSlamAttack slam;

	public override void _Ready()
	{
		base._Ready();
		slam = new GroundSlamAttack(SlamIntervalSeconds, SlamTelegraphSeconds, SlamRadius, SlamDamage, SlamWarningColor);
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		TickSlam((float)delta);
	}

	// Planted for the wind-up, chasing otherwise. Returning Zero also suppresses the separation
	// nudge, which matters here more than anywhere: the ring is drawn at this enemy's feet, so a
	// slammer that drifted a few pixels mid-tell would land its hit somewhere the player was not
	// warned about.
	protected override Vector2 AdjustSteering(Vector2 chaseDirection, float distanceToPlayer)
	{
		return slam != null && slam.IsWindingUp ? Vector2.Zero : chaseDirection;
	}

	private void TickSlam(float delta)
	{
		Node2D target = TargetPlayer;
		bool inRange = target != null
			&& IsInstanceValid(target)
			&& GlobalPosition.DistanceTo(target.GlobalPosition) <= SlamRadius * SlamEngageRadiusMultiplier;

		switch (slam.Tick(delta, inRange))
		{
			case AttackTelegraph.Beat.Started:
				PlayAttackAnimation();
				break;
			case AttackTelegraph.Beat.Resolved:
				slam.ResolveAgainst(this, TargetPlayer);
				break;
		}
	}

	// A recycled slammer starts its cooldown over, so one relocated to the spawn ring cannot arrive
	// with a wind-up already banked and slam before the player has seen it.
	public override void ResetForRespawn(Vector2 newPos, int newHealth)
	{
		base.ResetForRespawn(newPos, newHealth);
		slam = new GroundSlamAttack(SlamIntervalSeconds, SlamTelegraphSeconds, SlamRadius, SlamDamage, SlamWarningColor);
	}

	public override void _Draw() => slam?.Draw(this);
}
