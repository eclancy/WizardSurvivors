using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// Chapter 6, the Frozen Waste: THE STILL WARDEN.
//
// It does not move and it does not reach. It is a thing frozen into the ground that has been
// standing there long enough to have made the ground its own, and the fight is against the arena
// rather than against the body in the middle of it.
//
// THREE LAYERS, AND ONLY THE THIRD IS THE BOSS.
//
//   ICE FALLS on marked ground, in volleys, wherever the player is standing. That is the layer
//   that punishes standing still, and it is the reason the fight has any pressure at all given
//   that the boss itself cannot follow anyone anywhere.
//
//   GUARDS are posted around it and only strike the wedge in front of them. They are ordinary
//   SlammerEnemies with the telegraph shape flipped to a cone, which is the whole implementation -
//   the "guards that only attack in the direction they face" idea needed no new enemy, because
//   the difference between it and a slammer is the shape of one warning.
//
//   THE WARDEN ITSELF only hits the ring it is standing in, and hard. It is a wall around a prize.
//
// WHY A BOSS THAT CANNOT CHASE. Every other boss in the campaign closes distance, so every other
// boss is answered by kiting, and by chapter six kiting is the only thing the player has been
// asked to do. This one inverts it: the safe ground is the ground away from the boss, and the
// boss is the only place worth being. The player has to come to it and stay, which is the exact
// thing every previous fight trained them out of.
public partial class StillWardenBoss : BossEnemy
{
	[Export] public float IceVolleyIntervalSeconds { get; set; } = 3.4f;
	[Export] public int ShardsPerVolley { get; set; } = 3;
	[Export] public float IceTellSeconds { get; set; } = 1.0f;
	[Export] public float IceShardRadius { get; set; } = 66f;
	[Export] public int IceShardDamage { get; set; } = 4;

	/// <summary>How far a shard may land from the player. Zero would be undodgeable.</summary>
	[Export] public float IceScatterRadius { get; set; } = 110f;

	[Export] public string GuardScenePath { get; set; } = "res://scenes/RimeGuard.tscn";
	[Export] public int GuardCount { get; set; } = 3;
	[Export] public float GuardPostRadius { get; set; } = 150f;
	[Export] public float GuardRepostSeconds { get; set; } = 14f;
	[Export] public float GuardHealthFraction { get; set; } = 0.05f;

	[Export] public Color RimeColor { get; set; } = new Color(0.55f, 0.82f, 0.95f);

	private PackedScene guardScene;
	private readonly List<Enemy> guards = new();
	private float untilVolley;
	private float untilRepost;

	public override void _Ready()
	{
		base._Ready();
		untilVolley = IceVolleyIntervalSeconds;
		untilRepost = 0f;

		guardScene = ResourceLoader.Load<PackedScene>(GuardScenePath);
		if (guardScene == null)
			GD.PushError($"StillWardenBoss: guard scene not found at '{GuardScenePath}', so it stands unguarded.");
	}

	protected override GroundSlamAttack BuildSlam() =>
		new(SlamIntervalSeconds, SlamTelegraphSeconds, SlamRadius, SlamDamage, RimeColor);

	protected override void OnPhaseEntered(int phase)
	{
		ApplyEnrage();
		// The enrage has to land somewhere other than movement speed on a boss that never moves.
		IceVolleyIntervalSeconds *= 0.6f;
		ShardsPerVolley += 2;
	}

	// Frozen in. It has no chase and no drift, and returning Zero also keeps the separation nudge
	// from sliding it out of the ring it is defending.
	protected override Vector2 AdjustSteering(Vector2 chaseDirection, float distanceToPlayer) => Vector2.Zero;

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		TickIce((float)delta);
		TickGuards((float)delta);
	}

	private void TickIce(float delta)
	{
		if (IsDying)
			return;

		untilVolley -= delta;
		if (untilVolley > 0f)
			return;

		untilVolley = IceVolleyIntervalSeconds;

		Node2D target = TargetPlayer;
		Node arena = GetParent();
		if (target == null || !IsInstanceValid(target) || arena == null || !IsInstanceValid(arena))
			return;

		for (int i = 0; i < Mathf.Max(1, ShardsPerVolley); i++)
		{
			// Scattered around where the player IS, not where they will be. Leading the shot would
			// punish the correct response, which is to keep moving.
			float angle = GD.Randf() * Mathf.Tau;
			float distance = Mathf.Sqrt(GD.Randf()) * IceScatterRadius;
			Vector2 where = target.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
			TelegraphedGroundHit.Place(arena, where, IceTellSeconds, IceShardRadius, ScaleOutgoingDamage(IceShardDamage), RimeColor);
		}
	}

	private void TickGuards(float delta)
	{
		for (int i = guards.Count - 1; i >= 0; i--)
		{
			Enemy guard = guards[i];
			if (guard == null || !IsInstanceValid(guard) || !guard.IsInGroup("enemies"))
				guards.RemoveAt(i);
		}

		untilRepost -= delta;
		if (untilRepost > 0f || IsDying)
			return;

		untilRepost = GuardRepostSeconds;
		PostGuards();
	}

	// Refills the posts rather than adding to them, so clearing the guards buys a real window and
	// the arena never silently accumulates a wall of them.
	private void PostGuards()
	{
		Node arena = GetParent();
		if (guardScene == null || arena == null || !IsInstanceValid(arena))
			return;

		int wanted = Mathf.Max(0, GuardCount - guards.Count);
		for (int i = 0; i < wanted; i++)
		{
			if (guardScene.Instantiate() is not Enemy guard)
			{
				GD.PushError($"StillWardenBoss: '{GuardScenePath}' has no Enemy script attached.");
				return;
			}

			guard.Health = Mathf.Max(1, Mathf.RoundToInt(MaxHealth * GuardHealthFraction));
			guards.Add(guard);
			arena.AddChild(guard);

			float angle = Mathf.Tau * (guards.Count - 1) / Mathf.Max(1, GuardCount);
			guard.GlobalPosition = GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * GuardPostRadius;
		}
	}

	public override void _Draw()
	{
		base._Draw();

		if (IsDying)
			return;

		// The floor it has claimed. Drawn under the slam ring and much fainter, so the player can
		// see the shape of the ground they are fighting for before anything is winding up.
		var claim = new Color(RimeColor.R, RimeColor.G, RimeColor.B, 0.13f);
		DrawArc(Vector2.Zero, GuardPostRadius, 0f, Mathf.Tau, 40, claim, 2f, true);
	}
}
