using Godot;
using WizardSurvivors.scripts;

/// <summary>
/// What the lantern-tender needs from the lanterns it keeps. <see cref="BogLanternsEvent"/> is the
/// only implementation; a tender spawned anywhere else (a probe, a photograph) has none and simply
/// never rekindles.
/// </summary>
/// <remarks>
/// The event owns the lanterns - where they are, whether they burn, how standing in one snuffs it -
/// and the tender owns its ward. This is the whole of what passes between them, so neither reaches
/// into the other's state.
/// </remarks>
public interface ILanternKeeper
{
	/// <summary>The snuffed lantern nearest <paramref name="from"/> within <paramref name="reach"/>, or -1.</summary>
	int NearestSnuffedLantern(Vector2 from, float reach);

	/// <summary>Where lantern <paramref name="index"/>'s flame is, in world space.</summary>
	Vector2 LanternFlame(int index);

	/// <summary>Lights lantern <paramref name="index"/> again; the event's own snuffing puts it out.</summary>
	void Rekindle(int index);

	bool IsLanternLit(int index);
}

// The Blighted Swamp side event's last fight: THE LANTERN-TENDER (.ai/side-events.md, "The lantern-
// tender"). With all four bog lanterns snuffed, the thing that tends them comes for whoever put them
// out. It was an ordinary swamp creature at elite scale until 2026-10-28; Eric: "lantern tender should
// have a glowing lantern and be bigger so it's much easier to see him. he should fire animated fire
// strikes and have a few different special abilities that the player needs to deal with".
//
// It is found by its light: it carries one of the bog lanterns on a crook taller than itself, on the
// 96 cell at x2 (tools/art/swamp_tender.py), and the lantern burns with their cold flame and their far
// tell - a glow and a column of light that reach past the edge of the screen. Everything it does comes
// out of that lantern, and each thing asks the player for something different:
//
//   FIRE STRIKES - keep changing course. It raises the lantern and throws three gobbets of cold fire
//   in high arcs (five once it is wounded), each onto a ring marked on the ground for the whole of its
//   flight, where it bursts into a column of flame (LanternFireStrike). They are aimed where the player
//   is going - his position, then a step and two steps along the way he is walking - so a straight
//   kite runs into the second and third. Turning, or stopping and stepping aside, beats them.
//
//   SPILT OIL - step sideways, and do not step back. Up close it swings its oil can: a wedge in front
//   of it is marked, the splash lands on it, and the wedge stays alight for a few seconds afterwards
//   (BurningOil). The only lasting ground in the fight, so the ground it last stood on is not safe.
//
//   THE DARK - come into its light. A player who keeps his distance sees it plant and lift the
//   lantern high: its light pools on the ground round it, a ring at the pool's edge, and the dark
//   closes in over everything outside it. When the dark falls, whoever is still outside the light is
//   struck. The anti-kite move, and the reverse of every ring in the game: here the ring is where it
//   is SAFE. Inverts the event too, which was won by standing in the lanterns' light to put it out.
//
//   REKINDLE - put it out again. Once, at half health, it reaches its lantern toward the nearest
//   lantern the player snuffed and a thread of flame crawls to it and lights it. While that lantern
//   burns the tender is warded and takes a quarter of every hit; the thread from it to the lantern
//   says why. The answer is the event's own verb: go back and stand in that light until it goes out.
//
// None of it is a boss's whole mechanic. The strikes are the Still Warden's volleys thrown from a body
// that walks; the oil is the slammer's cone with ground left burning behind it; the dark is the first
// "get in" ring; the ward is Mother Rot's shield-by-proxy answered by standing rather than by killing.
//
// Built from the shared pieces: AttackTelegraph is every clock, GroundSlamAttack the oil's cone (and,
// inside LanternFireStrike, every strike's ring), so no shape is drawn one size and hit at another.
public partial class LanternTenderEnemy : Enemy
{
	/// <summary>
	/// The lantern's flame, in this node's local pixels with the sprite facing right: where its light is
	/// drawn and its strikes leave from. tools/art/swamp_tender.LANTERN_FLAME, (x - 48, y - 48) x 2, plus
	/// the sprite's offset. Change one, change both.
	/// </summary>
	[Export] public Vector2 LanternFlameOffset { get; set; } = new(64f, -68f);

	/// <summary>It does not need to touch anyone - its lantern reaches - so inside this it stands.</summary>
	[Export] public float HoldDistance { get; set; } = 150f;

	[ExportGroup("Fire strikes")]
	[Export] public float StrikeIntervalSeconds { get; set; } = 3.4f;
	/// <summary>The lantern raised and brightening before a volley leaves it.</summary>
	[Export] public float StrikeWindUpSeconds { get; set; } = 0.5f;
	/// <summary>Each flame's time in the air: its ring is on the ground for all of it. The real tell.</summary>
	[Export] public float StrikeFlightSeconds { get; set; } = 1.1f;
	/// <summary>Between one flame leaving the lantern and the next.</summary>
	[Export] public float StrikeThrowGapSeconds { get; set; } = 0.16f;
	[Export] public int StrikeCount { get; set; } = 3;
	/// <summary>Strikes a volley once wounded (at or below RekindleHealthFraction).</summary>
	[Export] public int StrikeCountWounded { get; set; } = 5;
	/// <summary>How far ahead along his walk each strike after the first is aimed, in seconds of it.</summary>
	[Export] public float StrikeLeadSeconds { get; set; } = 0.45f;
	[Export] public float StrikeRadius { get; set; } = 64f;
	[Export] public int StrikeDamage { get; set; } = 3;
	[Export] public float StrikeRange { get; set; } = 820f;

	[ExportGroup("Spilt oil")]
	[Export] public float OilIntervalSeconds { get; set; } = 5.5f;
	[Export] public float OilWindUpSeconds { get; set; } = 0.85f;
	/// <summary>The wedge's length: of the splash, and of the fire it leaves.</summary>
	[Export] public float OilReach { get; set; } = 230f;
	[Export] public float OilHalfAngleDegrees { get; set; } = 38f;
	[Export] public int OilDamage { get; set; } = 3;
	/// <summary>It swings when he is this close. At most the reach, or the splash could not touch him.</summary>
	[Export] public float OilEngageDistance { get; set; } = 210f;
	[Export] public float OilBurnSeconds { get; set; } = 4.5f;
	[Export] public float OilBurnTickSeconds { get; set; } = 0.6f;
	[Export] public int OilBurnDamage { get; set; } = 1;

	[ExportGroup("The dark")]
	[Export] public float DarkFirstDelaySeconds { get; set; } = 8f;
	[Export] public float DarkIntervalSeconds { get; set; } = 14f;
	/// <summary>Long, because the answer is a walk: from DarkMaxDistance to inside the light at a crowd's pace.</summary>
	[Export] public float DarkWindUpSeconds { get; set; } = 2.2f;
	/// <summary>The light's edge: inside it is safe when the dark falls. Drawn and tested from this one number.</summary>
	[Export] public float DarkLightRadius { get; set; } = 280f;
	/// <summary>It only lifts the lantern at someone keeping his distance; nearer, the oil is the answer.</summary>
	[Export] public float DarkMinDistance { get; set; } = 360f;
	/// <summary>And never at someone too far to reach the light in the wind-up.</summary>
	[Export] public float DarkMaxDistance { get; set; } = 620f;
	[Export] public int DarkDamage { get; set; } = 4;

	[ExportGroup("Rekindle")]
	[Export] public float RekindleHealthFraction { get; set; } = 0.5f;
	[Export] public float RekindleWindUpSeconds { get; set; } = 1.6f;
	/// <summary>How far a snuffed lantern can be and still be rekindled.</summary>
	[Export] public float RekindleReach { get; set; } = 1500f;
	/// <summary>The fraction of every hit it takes while a lantern it rekindled burns.</summary>
	[Export] public float WardDamageTaken { get; set; } = 0.25f;

	/// <summary>The lanterns it keeps. Set by BogLanternsEvent; null anywhere else.</summary>
	public ILanternKeeper Lanterns { get; set; }

	/// <summary>True while a lantern it rekindled is still lit.</summary>
	public bool IsWarded => warded;

	private static Color FlameCore => LanternFireStrike.FlameCore;
	private static Color FlameHot => LanternFireStrike.FlameHot;
	private static Color FlameMid => LanternFireStrike.FlameMid;
	// The fog that closes in: occlusion (#05070C), the one dark the palette shares.
	private static readonly Color Dark = new(0.020f, 0.027f, 0.047f);
	private const float DarkMaxAlpha = 0.58f;
	private const float DarkLiftSeconds = 0.6f;
	private const float DeathFadeSeconds = 0.6f;
	private const float WardBreakSeconds = 0.45f;

	private AttackTelegraph strike;
	private GroundSlamAttack oil;
	private AttackTelegraph dark;
	private AttackTelegraph rekindle;
	private bool darkPrimed;
	private float age;
	private float darkLift = -1f;
	private bool rekindleDone;
	private bool rekindlePending;
	private int rekindleIndex = -1;
	private bool warded;
	private float wardBreak = -1f;
	private float deathFade = -1f;
	private RandomNumberGenerator aimRng = new();

	private AnimatedSprite2D sprite;
	private Node2D lightLayer;
	private Node2D darkLayer;

	public override void _Ready()
	{
		base._Ready();
		sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		aimRng.Randomize();
		Rebuild();

		// Its light: drawn additively over the actors like the event's lanterns, so it brightens what
		// stands in it and never hides it. Over the dark (below), because the lantern is what the dark
		// cannot put out.
		lightLayer = new Node2D { Name = "LanternLight", ZIndex = 8, Material = ExploreArt.Additive, TextureFilter = TextureFilterEnum.Linear };
		lightLayer.Draw += OnLightDraw;
		AddChild(lightLayer);
		// The dark it lets in: over the actors and under every light, so the strikes' fire still shows.
		darkLayer = new Node2D { Name = "Dark", ZIndex = 7 };
		darkLayer.Draw += OnDarkDraw;
		AddChild(darkLayer);
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		if (lightLayer != null && IsInstanceValid(lightLayer))
			lightLayer.Draw -= OnLightDraw;
		if (darkLayer != null && IsInstanceValid(darkLayer))
			darkLayer.Draw -= OnDarkDraw;
	}

	private void Rebuild()
	{
		strike = new AttackTelegraph(StrikeIntervalSeconds, StrikeWindUpSeconds);
		oil = new GroundSlamAttack(OilIntervalSeconds, OilWindUpSeconds, OilReach, OilDamage, FlameMid)
		{
			Shape = TelegraphShape.Cone,
			HalfAngleDegrees = OilHalfAngleDegrees,
		};
		dark = new AttackTelegraph(DarkIntervalSeconds, DarkWindUpSeconds);
		// Its interval is never meant to come round: it is primed once, at half health, and fires once.
		rekindle = new AttackTelegraph(1000000f, RekindleWindUpSeconds);
		darkPrimed = false;
		darkLift = -1f;
		rekindlePending = false;
		rekindleIndex = -1;
		warded = false;
		wardBreak = -1f;
		age = 0f;
		volleyLandsAt = 0f;
	}

	/// <summary>True while any of its four attacks is winding up: it does one thing at a time.</summary>
	private bool Busy => strike.IsWindingUp || oil.IsWindingUp || dark.IsWindingUp || rekindle.IsWindingUp;

	// Until the last flame of a volley has landed. The specials wait for it: a wedge or a dark drawn
	// over three rings still falling is two tells read at once, and attention is the budget
	// (.ai/enemy-behaviour.md section 1). The strikes themselves need no such wait - they never start
	// while a special is winding up (Busy), and their own wind-up is the lantern brightening.
	private float volleyLandsAt;
	private bool VolleyInTheAir => age < volleyLandsAt;

	// Planted for every wind-up, so each tell is drawn where it lands; standing when he is close
	// enough for the oil; chasing otherwise.
	protected override Vector2 AdjustSteering(Vector2 chaseDirection, float distanceToPlayer)
	{
		if (Busy || distanceToPlayer <= HoldDistance)
			return Vector2.Zero;
		return chaseDirection;
	}

	// The ward: a quarter of every hit while the lantern it rekindled burns. Rounded stochastically,
	// as Weaken is (ScaleOutgoingDamage), so a ward also thins the swarm of 1-damage hits on average
	// instead of rounding every one of them back to 1.
	protected override int AdjustIncomingDamage(int amount)
	{
		if (!warded || amount <= 0)
			return amount;
		float scaled = amount * Mathf.Clamp(WardDamageTaken, 0f, 1f);
		int whole = Mathf.FloorToInt(scaled);
		return aimRng.Randf() < scaled - whole ? whole + 1 : whole;
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		float dt = (float)delta;
		age += dt;
		if (darkLift >= 0f)
			darkLift += dt;
		if (wardBreak >= 0f)
			wardBreak += dt;

		Node2D target = TargetPlayer;
		bool hasTarget = target != null && IsInstanceValid(target);
		float distance = hasTarget ? GlobalPosition.DistanceTo(target.GlobalPosition) : float.MaxValue;

		TickWard();
		// One wind-up at a time, in priority order: each is only offered the start if nothing else has
		// started this frame or is already running.
		TickRekindle(dt);
		TickDark(dt, hasTarget, distance);
		TickOil(dt, hasTarget, distance);
		TickStrikes(dt, hasTarget, distance);
	}

	public override void _Process(double delta)
	{
		// The light outlives the body by its death strip: physics stops the frame it dies, and a lantern
		// that went out on the killing frame would read as the lantern being shot rather than its keeper.
		if (deathFade >= 0f)
			deathFade += (float)delta;
		lightLayer?.QueueRedraw();
		darkLayer?.QueueRedraw();
	}

	// --- fire strikes -------------------------------------------------------------------------------

	private void TickStrikes(float dt, bool hasTarget, float distance)
	{
		bool wants = hasTarget && distance <= StrikeRange && !Busy;
		switch (strike.Tick(dt, wants))
		{
			case AttackTelegraph.Beat.Started:
				PlayAttackAnimation();
				break;
			case AttackTelegraph.Beat.Resolved:
				ThrowVolley();
				break;
		}
	}

	private bool Wounded => HealthFraction <= RekindleHealthFraction;

	private void ThrowVolley()
	{
		Node2D target = TargetPlayer;
		Node arena = GetParent();
		if (target == null || !IsInstanceValid(target) || arena == null)
			return;

		Vector2 at = target.GlobalPosition;
		// Along the way he is walking. A player standing still would put every strike on one spot -
		// three hits looking like one, the volley's one dishonest shape - so then the line runs off to
		// one side of him at a strike and a quarter apart, and stepping the other way clears it.
		Vector2 step = MeasuredPlayerVelocity * StrikeLeadSeconds;
		float spacing = StrikeRadius * 1.25f;
		if (step.Length() < spacing)
		{
			Vector2 across = (at - GlobalPosition).Orthogonal();
			if (across.LengthSquared() < 0.01f)
				across = Vector2.Right;
			step = across.Normalized() * spacing * (aimRng.Randf() < 0.5f ? -1f : 1f);
		}
		Vector2 side = step.Orthogonal().Normalized() * StrikeRadius * 1.7f;

		int count = Mathf.Max(1, Wounded ? StrikeCountWounded : StrikeCount);
		Vector2 from = LanternWorld;
		for (int i = 0; i < count; i++)
		{
			// 0, 1, 2 along his walk; then (wounded) a pair either side of the first step, boxing in
			// the sideways dodge as well as the forward one.
			Vector2 spot = i switch
			{
				0 => at,
				1 => at + step,
				2 => at + step * 2f,
				_ => at + step + (i % 2 == 1 ? side : -side) * (1f + (i - 3) / 2),
			};
			LanternFireStrike.Throw(arena, from, spot, i * StrikeThrowGapSeconds, StrikeFlightSeconds,
				StrikeRadius, ScaleOutgoingDamage(StrikeDamage), FlameMid);
		}
		volleyLandsAt = age + (count - 1) * StrikeThrowGapSeconds + StrikeFlightSeconds;
		SfxPlayer.AtPosition(SfxCatalog.EnemyShoot, GlobalPosition, 0.07f);
	}

	// --- spilt oil ----------------------------------------------------------------------------------

	private void TickOil(float dt, bool hasTarget, float distance)
	{
		bool wants = hasTarget && distance <= OilEngageDistance && !Busy && !VolleyInTheAir;
		switch (oil.Tick(dt, wants))
		{
			case AttackTelegraph.Beat.Started:
				PlayAttackAnimation();
				// Aimed once, as the slammer's cone is: a wedge that kept turning would leave no side.
				if (TargetPlayer != null && IsInstanceValid(TargetPlayer))
					oil.AimAt(GlobalPosition, TargetPlayer.GlobalPosition);
				break;
			case AttackTelegraph.Beat.Resolved:
				oil.ResolveAgainst(this, TargetPlayer);
				BurningOil.Spill(GetParent(), GlobalPosition, oil.Facing, OilReach, OilHalfAngleDegrees,
					OilBurnSeconds, OilBurnTickSeconds, ScaleOutgoingDamage(OilBurnDamage));
				break;
		}
	}

	// --- the dark -----------------------------------------------------------------------------------

	private void TickDark(float dt, bool hasTarget, float distance)
	{
		if (!darkPrimed && age >= DarkFirstDelaySeconds)
		{
			// The first lift opens earlier than the interval: Prime clears the wait, never the wind-up.
			dark.Prime();
			darkPrimed = true;
		}
		bool wants = darkPrimed && hasTarget && !warded && !rekindlePending
			&& distance >= DarkMinDistance && distance <= DarkMaxDistance && !Busy && !VolleyInTheAir;
		switch (dark.Tick(dt, wants))
		{
			case AttackTelegraph.Beat.Started:
				PlayAttackAnimation();
				break;
			case AttackTelegraph.Beat.Resolved:
				darkLift = 0f;
				Node2D target = TargetPlayer;
				if (target != null && IsInstanceValid(target) && OutsideTheLight(target.GlobalPosition)
					&& target.HasMethod("TakeDamage"))
				{
					int dealt = ScaleOutgoingDamage(DarkDamage);
					if (dealt > 0)
						target.Call("TakeDamage", dealt);
				}
				break;
		}
	}

	/// <summary>Whether the dark takes <paramref name="world"/> when it falls: the one test, against the radius drawn.</summary>
	public bool OutsideTheLight(Vector2 world) => GlobalPosition.DistanceTo(world) > DarkLightRadius;

	// --- rekindle and the ward ----------------------------------------------------------------------

	private void TickRekindle(float dt)
	{
		if (!rekindleDone && !rekindlePending && Lanterns != null && HealthFraction <= RekindleHealthFraction)
		{
			rekindleIndex = Lanterns.NearestSnuffedLantern(GlobalPosition, RekindleReach);
			if (rekindleIndex < 0)
				rekindleDone = true;      // nothing in reach to light: it never tries again
			else
			{
				rekindlePending = true;
				rekindle.Prime();
			}
		}

		switch (rekindle.Tick(dt, rekindlePending && !Busy && !VolleyInTheAir))
		{
			case AttackTelegraph.Beat.Started:
				PlayAttackAnimation();
				break;
			case AttackTelegraph.Beat.Resolved:
				rekindlePending = false;
				rekindleDone = true;
				if (Lanterns != null && rekindleIndex >= 0)
				{
					Lanterns.Rekindle(rekindleIndex);
					warded = Lanterns.IsLanternLit(rekindleIndex);
					GD.Print($"LanternTenderEnemy: rekindled lantern {rekindleIndex} at {HealthFraction:0.00} health; warded: {warded}");
					SfxPlayer.AtPosition(SfxCatalog.HintLanternFlare, Lanterns.LanternFlame(rekindleIndex), 0f);
				}
				break;
		}
	}

	// The ward lasts exactly as long as the lantern: the event puts it out when he stands in its light.
	private void TickWard()
	{
		if (!warded || Lanterns == null || rekindleIndex < 0 || Lanterns.IsLanternLit(rekindleIndex))
			return;
		warded = false;
		wardBreak = 0f;
		GD.Print($"LanternTenderEnemy: lantern {rekindleIndex} snuffed again; the ward is broken");
	}

	// --- drawing ------------------------------------------------------------------------------------

	// Draws in world units on a child of this node. Elites are scaled up a few percent at spawn
	// (Node2DGame.EliteScaleMultiplier) and every hit punches the scale, so anything measured in the
	// world - a ring the player is tested against, a thread to a lantern - is drawn through the inverse
	// of the scale, which keeps the drawn radius the tested one at every scale.
	private Vector2 InverseScale => new(1f / Mathf.Max(0.01f, Mathf.Abs(Scale.X)), 1f / Mathf.Max(0.01f, Mathf.Abs(Scale.Y)));

	/// <summary>The flame's place in local pixels, mirrored when the sprite faces left.</summary>
	private Vector2 LanternLocal
	{
		get
		{
			bool flipped = sprite != null && sprite.FlipH;
			return new Vector2(flipped ? -LanternFlameOffset.X : LanternFlameOffset.X, LanternFlameOffset.Y);
		}
	}

	/// <summary>Where the lantern's flame is in the world: where its strikes leave from.</summary>
	public Vector2 LanternWorld => GlobalPosition + LanternLocal * Scale.Abs();

	public override void _Draw()
	{
		base._Draw();
		// The oil's cone, on the ground in world units (see InverseScale).
		DrawSetTransform(Vector2.Zero, 0f, InverseScale);
		oil?.Draw(this);
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	// His light flickers (.ai/stage-secrets.md section 2), the same irregular clock as SideEvent.Flicker.
	private float Flicker()
	{
		float t = age;
		float f = 0.92f + 0.07f * Mathf.Sin(t * 7.3f) + 0.05f * Mathf.Sin(t * 17.9f + 1.3f) + 0.03f * Mathf.Sin(t * 31.3f);
		float gutter = Mathf.Sin(t * 1.37f + 0.7f);
		if (gutter > 0.94f)
			f -= (gutter - 0.94f) * 4f;
		return Mathf.Clamp(f, 0.6f, 1.15f);
	}

	private void OnLightDraw()
	{
		float life = deathFade < 0f ? 1f : 1f - Mathf.Clamp(deathFade / DeathFadeSeconds, 0f, 1f);
		if (life <= 0f || lightLayer == null)
			return;

		float strength = Flicker() * life;
		// A volley leaving and the lantern lifted for the dark both brighten it: the lantern IS the tell.
		float lift = 1f;
		if (strike.IsWindingUp)
			lift += 0.7f * strike.WindUpProgress;
		if (dark.IsWindingUp)
			lift += 1.1f * dark.WindUpProgress;
		if (rekindle.IsWindingUp)
			lift += 0.5f;

		Vector2 flame = LanternLocal;
		// The far tell, as the bog lanterns': a wide faint glow and a column standing up out of it, so the
		// tender is found from past the edge of the screen the way the lanterns were.
		ExploreArt.DrawGlow(lightLayer, flame, 320f, new Color(FlameMid, 0.08f * strength * lift));
		ExploreArt.DrawShaft(lightLayer, flame, 16f, 380f, new Color(FlameMid, 0.22f * strength));
		ExploreArt.DrawShaft(lightLayer, flame, 5f, 340f, new Color(FlameHot, 0.26f * strength));
		// The flame itself: halo, glow, core.
		ExploreArt.DrawGlow(lightLayer, flame, 140f * Mathf.Min(lift, 1.6f), new Color(FlameMid, 0.32f * strength));
		ExploreArt.DrawGlow(lightLayer, flame, 56f, new Color(FlameHot, 0.6f * strength));
		ExploreArt.DrawGlow(lightLayer, flame, 20f, new Color(FlameCore, 0.95f * strength));
		// Its light on the figure and the ground under it.
		ExploreArt.DrawGlow(lightLayer, new Vector2(flame.X * 0.5f, 30f), 120f, new Color(FlameMid, 0.12f * strength));

		// Everything below is measured in the world.
		lightLayer.DrawSetTransform(Vector2.Zero, 0f, InverseScale);
		Vector2 flameWorld = flame * Scale.Abs();

		if (dark.IsWindingUp)
		{
			// The pool it lifts its light over: the safe ground, lit as the dark closes on everything else.
			float p = dark.WindUpProgress;
			ExploreArt.DrawGlow(lightLayer, Vector2.Zero, DarkLightRadius * 1.2f, new Color(FlameMid, 0.22f * p * life));
			lightLayer.DrawArc(Vector2.Zero, DarkLightRadius, 0f, Mathf.Tau, 96,
				new Color(FlameHot, (0.25f + 0.55f * p) * life), 3f, true);
		}
		else if (darkLift >= 0f && darkLift < DarkLiftSeconds)
		{
			// The moment it falls: the light's edge flares white and fades with the dark, so a hit taken
			// outside it has a cause on screen.
			float k = darkLift / DarkLiftSeconds;
			lightLayer.DrawArc(Vector2.Zero, DarkLightRadius, 0f, Mathf.Tau, 96,
				new Color(FlameCore, 0.9f * (1f - k) * life), 6f * (1f - k) + 1f, true);
		}

		if (rekindle.IsWindingUp && Lanterns != null && rekindleIndex >= 0)
			DrawThread(flameWorld, Lanterns.LanternFlame(rekindleIndex) - GlobalPosition, rekindle.WindUpProgress, life);
		else if (warded && Lanterns != null && rekindleIndex >= 0)
		{
			DrawThread(flameWorld, Lanterns.LanternFlame(rekindleIndex) - GlobalPosition, 1f, life);
			// The ward on the body: a ring of its light round it and a slow orbit of cold motes on the
			// ring, pulsing - a status mark on one enemy, so it may be a ring (.ai/art-direction.md 3).
			float pulse = 0.75f + 0.25f * Mathf.Sin(age * 5f);
			lightLayer.DrawArc(new Vector2(0f, -6f), 70f, 0f, Mathf.Tau, 48, new Color(FlameHot, 0.55f * pulse * life), 3f, true);
			for (int i = 0; i < 8; i++)
			{
				float a = age * 1.6f + i * Mathf.Tau / 8f;
				Vector2 at = new Vector2(0f, -6f) + Vector2.FromAngle(a) * 70f;
				ExploreArt.DrawGlow(lightLayer, at, 22f, new Color(FlameHot, 0.85f * life));
				ExploreArt.DrawGlow(lightLayer, at, 8f, new Color(FlameCore, 0.9f * life));
			}
			ExploreArt.DrawGlow(lightLayer, new Vector2(0f, -6f), 110f, new Color(FlameMid, 0.22f * pulse * life));
		}

		if (wardBreak >= 0f && wardBreak < WardBreakSeconds)
		{
			// The ward going out: a ring of its light thrown off the body.
			float k = wardBreak / WardBreakSeconds;
			lightLayer.DrawArc(new Vector2(0f, 10f), Mathf.Lerp(40f, 140f, k), 0f, Mathf.Tau, 40,
				new Color(FlameHot, 0.8f * (1f - k)), 4f, true);
		}
		lightLayer.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	// A thread of cold fire from its lantern to the one it is lighting, crawling there over the
	// wind-up and holding steady after. Glows at a fixed spacing: one per 36 px, a few dozen at most.
	private void DrawThread(Vector2 from, Vector2 to, float progress, float life)
	{
		Vector2 span = to - from;
		float length = span.Length();
		if (length < 1f)
			return;
		Vector2 dir = span / length;
		float reach = length * Mathf.Clamp(progress, 0f, 1f);
		for (float d = 0f; d <= reach; d += 36f)
		{
			float wobble = Mathf.Sin(d * 0.05f - age * 6f) * 6f;
			Vector2 at = from + dir * d + dir.Orthogonal() * wobble;
			ExploreArt.DrawGlow(lightLayer, at, 16f, new Color(FlameHot, 0.5f * life));
		}
		// Its head, while it crawls.
		if (progress < 1f)
			ExploreArt.DrawGlow(lightLayer, from + dir * reach, 30f, new Color(FlameCore, 0.8f * life));
	}

	private void OnDarkDraw()
	{
		if (darkLayer == null)
			return;
		float alpha;
		if (dark.IsWindingUp)
		{
			float p = dark.WindUpProgress;
			alpha = DarkMaxAlpha * p * p * (3f - 2f * p);
		}
		else if (darkLift >= 0f && darkLift < DarkLiftSeconds)
			alpha = DarkMaxAlpha * (1f - darkLift / DarkLiftSeconds);
		else
			return;

		// The dark, from the light's edge outward to past any screen: one wide arc whose inner edge is
		// DarkLightRadius exactly, with a short soft band inside that so the edge reads as light failing
		// rather than as a cut-out. Drawn in the world's units, against the radius the hit is tested by.
		darkLayer.DrawSetTransform(Vector2.Zero, 0f, InverseScale);
		const float Far = 2600f;
		const float Soft = 36f;
		darkLayer.DrawArc(Vector2.Zero, DarkLightRadius + Soft + Far * 0.5f, 0f, Mathf.Tau, 96,
			new Color(Dark, alpha), Far, false);
		for (int i = 0; i < 3; i++)
		{
			float inner = DarkLightRadius + Soft * i / 3f;
			darkLayer.DrawArc(Vector2.Zero, inner + Soft / 6f, 0f, Mathf.Tau, 96,
				new Color(Dark, alpha * (i + 1) / 4f), Soft / 3f, false);
		}
		darkLayer.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	protected override void StartDeath()
	{
		deathFade = 0f;
		warded = false;
		base.StartDeath();
	}

	// Never recycled - it is an elite - but the contract (.ai/enemy-behaviour.md section 8, item 2) is
	// that anything holding a telegraph rebuilds it here, so no caller can bring it back mid-swing.
	public override void ResetForRespawn(Vector2 newPos, int newHealth)
	{
		base.ResetForRespawn(newPos, newHealth);
		Rebuild();
	}

	// --- for _UiShot ----------------------------------------------------------------------------------

	/// <summary>Which attack <see cref="DebugBegin"/> starts.</summary>
	public enum Move { Strikes, Oil, Dark, Rekindle }

	/// <summary>
	/// Starts one attack on the next frame whatever the range, for a photograph of its tell. Never
	/// called in play. It can only clear a wait, never shorten a wind-up (AttackTelegraph.Prime).
	/// </summary>
	public void DebugBegin(Move move)
	{
		switch (move)
		{
			case Move.Strikes:
				strike.Prime();
				break;
			case Move.Oil:
				oil.Prime();
				OilEngageDistance = float.MaxValue;
				break;
			case Move.Dark:
				darkPrimed = true;
				dark.Prime();
				DarkMinDistance = 0f;
				DarkMaxDistance = float.MaxValue;
				break;
			case Move.Rekindle:
				RekindleHealthFraction = 1f;
				break;
		}
	}
}
