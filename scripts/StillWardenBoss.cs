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

	protected override void DrawGroundTells(CanvasItem floor)
	{
		// The floor it has claimed. Drawn under the slam ring (so first) and much fainter, so the
		// player can see the shape of the ground they are fighting for before anything is winding up.
		if (!IsDying)
		{
			var claim = new Color(RimeColor.R, RimeColor.G, RimeColor.B, 0.13f);
			floor.DrawArc(Vector2.Zero, GuardPostRadius, 0f, Mathf.Tau, 40, claim, 2f, true);
		}
		base.DrawGroundTells(floor);
	}

	// --- The fall (BossDeath) ---------------------------------------------------------------------
	//
	// THE COLD LETS GO. It is a thing frozen into the ground that made the ground its own, and it ends
	// the way ice ends under a blow: it cracks. White seams race out from the eye across the plate and
	// the crystals, the whole figure goes white along them, and then it shatters - ice everywhere,
	// plate with it - and the rime it had laid round itself, the ground it claimed, draws back in
	// towards where it stood and is gone. The shards lie winking on the frost a moment and melt.
	private BossDeath.Piece deathIce;
	private Vector2[][] deathCracks;

	private const float ShatterAt = 0.58f;
	private const int RimeSpecks = 70;

	protected internal override float DeathSeconds => 2.6f;

	protected internal override void OnDeathBegin(BossDeath death)
	{
		deathIce = death.CutWhole();
		// Seven seams out of the eye, spread round it - more of them downwards, where the body is.
		var from = new Vector2(47.5f, 18f);
		float[] headings = { -2.6f, -0.5f, 0.5f, 1.25f, 1.9f, 2.6f, Mathf.Pi * 0.5f };
		deathCracks = new Vector2[headings.Length][];
		for (int i = 0; i < headings.Length; i++)
			deathCracks[i] = death.CrackPath((uint)(i * 17 + 3), from, headings[i], i < 2 ? 10 : 24, 3f);
	}

	protected internal override void TickDeath(BossDeath death, float t, float dt)
	{
		if (deathIce == null)
			return;

		float s = death.ArtScale.X;
		ElementColors.Ramp ice = ElementColors.GetRamp(Element.Ice);
		float strain = BossDeath.Span(t, 0f, ShatterAt);
		deathIce.Offset = new Vector2(BossDeath.Shudder(t, strain, 24f) * s, 0f);
		deathIce.Glow = new Color(ice.Hot, 0.65f * BossDeath.EaseIn(BossDeath.Span(t, 0.3f, ShatterAt)));
		deathIce.Visible = t < ShatterAt;

		if (!death.Crossed(ShatterAt))
			return;

		var centre = new Vector2(47.5f, 52f);
		death.Emit(new BossDeath.Throw
		{
			Cell = BossDeath.Cell.ShardLong, Variants = 3, SpeedMin = 80f, SpeedMax = 270f, UpMin = 110f, UpMax = 290f,
			Gravity = 660f, Bounce = 0.25f, Friction = 6f, Spin = 7f, Life = 1.85f, Fade = 0.7f, DepthJitter = 18f,
			Glint = new Color(ice.Core, 0.9f),
		}, 26, centre, new Vector2(30f, 36f));
		death.Emit(new BossDeath.Throw
		{
			Cell = BossDeath.Cell.IceChunk, SpeedMin = 40f, SpeedMax = 150f, UpMin = 80f, UpMax = 180f,
			Bounce = 0.2f, Friction = 7f, Spin = 4f, Life = 1.85f, Fade = 0.7f,
		}, 6, centre, new Vector2(24f, 30f));
		death.Emit(new BossDeath.Throw
		{
			Cell = BossDeath.Cell.PlateShard, Tinted = true, SpeedMin = 50f, SpeedMax = 180f, UpMin = 70f, UpMax = 170f,
			Bounce = 0.3f, Friction = 6f, Spin = 6f, Life = DeathSeconds - ShatterAt, Fade = 0.45f,
		}, 10, new Vector2(47.5f, 30f), new Vector2(30f, 12f));
		death.Emit(new BossDeath.Throw
		{
			Size = 2f, Color = ice.Core, ColorB = ice.Mid, Light = true, NoLand = true, SpeedMin = 10f, SpeedMax = 60f,
			UpMin = 10f, UpMax = 80f, Gravity = -40f, Drag = 1.5f, Life = 1.1f, Fade = 0.7f, Flutter = 4f,
		}, 22, centre, new Vector2(30f, 40f));
		for (int i = 0; i < 6; i++)
		{
			Vector2 at = death.ArtToLocal(centre + new Vector2(death.Rand(-26f, 26f), death.Rand(-30f, 34f)));
			death.PuffAt(at, 6f, 24f, 0.4f, new Color(ice.Hot, 0.3f), 8f, death.Rand(0f, 0.08f), true);
		}
	}

	// The seams, racing out of the eye and blazing white just before it goes.
	protected internal override void DrawDeathLight(BossDeath death, CanvasItem canvas, float t)
	{
		if (deathIce == null || t >= ShatterAt)
			return;
		ElementColors.Ramp ice = ElementColors.GetRamp(Element.Ice);
		for (int i = 0; i < deathCracks.Length; i++)
		{
			float grown = BossDeath.EaseOut(BossDeath.Span(t, i * 0.035f, 0.42f + i * 0.02f));
			death.DrawCrack(canvas, deathIce, deathCracks[i], grown, new Color(ice.Core, 0.95f), new Color(ice.Mid, 0.45f));
		}
	}

	// The rime it laid round itself, out to the guards' posts: brightening as the cold gathers, then
	// drawing back in to where it stood after the shatter, and gone.
	protected internal override void DrawDeathGround(BossDeath death, CanvasItem canvas, float t)
	{
		if (deathIce == null)
			return;
		ElementColors.Ramp ice = ElementColors.GetRamp(Element.Ice);
		float reach = GuardPostRadius * (1f - BossDeath.EaseIn(BossDeath.Span(t, ShatterAt + 0.1f, 2.3f)));
		if (reach <= 4f)
			return;
		float bright = 0.35f + 0.4f * BossDeath.Span(t, 0f, ShatterAt) * (1f - BossDeath.Span(t, ShatterAt, ShatterAt + 0.5f));
		Vector2 centre = death.Foot;
		for (int k = 0; k < RimeSpecks; k++)
		{
			float a = Mathf.Tau * BossDeath.Hash(71u, k);
			float d = GuardPostRadius * Mathf.Sqrt(BossDeath.Hash(73u, k));
			if (d > reach)
				continue;
			Vector2 at = centre + new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d * 0.5f);
			float size = BossDeath.Hash(79u, k) < 0.3f ? BossDeath.Px * 2f : BossDeath.Px;
			Color c = BossDeath.Hash(83u, k) < 0.25f ? ice.Hot : ice.Mid;
			canvas.DrawRect(WhipLimb.Cell(at, size), new Color(c, bright * (d > reach - 12f ? 0.5f : 1f)));
		}
		// Specks, never a ring: a ring round a boss is a telegraph in this game, and this is not one.
	}
}
