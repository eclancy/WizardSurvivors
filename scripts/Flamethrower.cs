using Godot;
using System;

namespace WizardSurvivors.scripts;

// Cinderbreath: a sustained jet of fire, thrown as individual tongues.
//
// This node no longer draws or damages anything. It is an emitter: while it is lit it throws a
// FlamePuff every EmitInterval into a cone, and the puffs carry the damage, the reach and the art.
//
// WHY IT CHANGED. The first version drew the whole cone in _Draw as three shaded polygons and
// tested that cone analytically. Both halves were correct and the result still did not read as
// fire: a filled wedge has nothing in it for the eye to track, so it looked like a lit area rather
// than something being poured. Splitting it into tongues costs a sprite sheet and buys the thing
// the spell is actually for - you can see the fire leave the staff, travel, and go out.
//
// WHAT SURVIVED, because it was the good part:
//   - The duty cycle. Lit for BurnSeconds, dark for the spell's cooldown. An always-on jet is an
//     aura, and an aura is the always-on passive the boon rework removed.
//   - Re-aiming on an interval rather than per frame. Ten times a second is faster than anything
//     can walk out of a 55 degree arc, and it is one enemy-group scan instead of sixty.
//   - Living as a persistent child of the Player, so the nozzle travels with the caster.
//
// The puffs themselves are parented to the arena, not to this node - see FlamePuff.
public partial class Flamethrower : Node2D
{
	[Export] public PackedScene FlamePuffScene { get; set; }

	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;

	// Short. A flamethrower that outranged a bolt would simply be a better bolt; the trade is that
	// it covers a whole arc but only just past arm's reach, so using it means letting them close.
	[Export] public float BaseRange { get; set; } = 170f;
	[Export] public float ConeAngleDegrees { get; set; } = 55f;

	[Export] public float BurnSeconds { get; set; } = 1.5f;

	// How often a tongue leaves the nozzle. Fast enough to read as a continuous jet, slow enough
	// that a burn is about sixteen puffs rather than ninety - this is the spell's whole per-frame
	// cost, and CLAUDE.md is explicit about not allocating per frame in swarm-heavy effects.
	[Export] public float EmitInterval { get; set; } = 0.09f;

	// Each tongue is thrown at a slightly different angle and speed. Without the jitter every puff
	// follows the same line and the cone collapses into a single stuttering arrow.
	[Export] public float SpeedJitter { get; set; } = 0.18f;

	[Export] public float AimRefreshSeconds { get; set; } = 0.1f;

	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float CooldownMultiplier { get; set; } = 1.0f;
	public Player PlayerRef;

	private float range = 170f;
	private int damagePerPuff = 4;
	private float rechargeSeconds = 1.4f;
	// What the evolutions buy a jet (it took the default archetype's words for a bolt and read none of
	// them but damage and cooldown). Area is how WIDE each tongue burns - its hit radius, and its sprite
	// with it, so it is drawn as wide as it hurts - and never how far: a flamethrower that outranged a
	// bolt would be a better bolt (see BaseRange). Speed is how fast tongues leave the nozzle. Count is
	// how many tongues each throw sends, at their own jittered angles. A tongue already burns through
	// everything it touches, so there is no pierce to buy.
	private float tongueWidth = 1f;
	private float burnSeconds = 1.5f;
	private float throwRate = 1f;
	private int tonguesPerThrow = 1;
	private const int MaxTonguesPerThrow = 4;
	// An element path (Hellbreath) repaints the fire in that element's ramp. The puffs and the ground
	// are parented to the arena, so the evolution tint Player puts on this emitter never reached them:
	// each tongue takes its ramp from the spell (FlamePuff.MaterialFor), and this repaint of the crater
	// fire's sprite is handed to the ground. Null while the spell has not branched.
	private ShaderMaterial pathMaterial;

	// One aim per tongue in a throw (2026-10-28: "cinderbreath ascension that spreads 3 tongues should
	// all target enemies in different directions"). They all went into the one cone at the nearest enemy,
	// so three tongues were one thicker jet. Now each is its own jet: the first at the nearest enemy, each
	// next at the nearest enemy at least a cone's width away in bearing from every jet already aimed, and
	// any jet left without one fanned evenly round from the first. A single tongue aims as it always did.
	private readonly float[] aims = new float[MaxTonguesPerThrow];
	private readonly Node2D[] aimTargets = new Node2D[MaxTonguesPerThrow];
	// How far a second jet will look for its own enemy: a little past the jet's reach.
	private const float SpreadSearchRangeFactor = 2.2f;

	// EVERY CARD ITS OWN (.ai/spell-roster.md section 2e, 2026-10-29: "yes lets make all mutations more
	// unique per spell"). Each milestone changes what the jet DOES where you can see it; the numbers ride
	// along. The ids are the archetype's, kept so the Pyromancer's Apotheosis path (Backdraft, then
	// Dragon's Breath) and every save's choices carry over; the cards in SpellEvolutionCatalog say what
	// each does with these numbers, which are read from here and from nowhere else.
	//
	// Backdraft: as a burn goes out, the fire rolls back into a burst at the tip of every jet.
	public const string BackdraftId = "cinderbreath_amplified_impact";
	public const float BackdraftRadius = 58f;
	public const float BackdraftDamageShare = 3f;
	// Ember Wake: every second tongue drops an ember where it goes out, across the swept cone.
	public const string EmberWakeId = "cinderbreath_hyper_acceleration";
	public const int EmberEvery = 2;
	public const float EmberRadius = 20f;
	public const float EmberSeconds = 2f;
	public const float EmberDamageShare = 0.5f;
	// Dragon's Breath: the jet reaches farther - 75%, not the proposal's double, which at level 8 would
	// be 480 against a bolt's 450 (BaseRange's rule) - and where a burn ends a wall of fire stands across
	// its path.
	public const string DragonsBreathId = "cinderbreath_titan_cataclysm";
	public const float DragonReach = 1.75f;
	public const float WallSeconds = 3f;
	public const float WallDamageShare = 1f;
	public const float WallHalfThickness = 16f;
	// The wall's half-length as a share of the reach: the cone's own chord where the wall stands.
	private const float WallHalfLengthShare = 0.4f;
	private const float WallAtShare = 0.85f;
	// Fire Fan: three jets, each turned on its own enemy (SpreadAims). Read through the count; named here
	// so the card and the hook point at one id.
	public const string FireFanId = "cinderbreath_infinite_horizon";

	private bool backdraft;
	private bool emberWake;
	private bool dragonsBreath;
	private int tongueCounter;
	private CinderGround cinderGround;

	private bool burning = false;
	private float stateSeconds = 0f;
	private float emitSeconds = 0f;
	private float aimSeconds = 0f;

	private readonly RandomNumberGenerator rng = new();

	public override void _Ready()
	{
		rng.Randomize();
		RefreshComputedStats();

		// Starts lit. A starter spell that opens the run with a second and a half of nothing reads
		// as broken rather than as recharging.
		burning = true;
		stateSeconds = 0f;
		emitSeconds = EmitInterval;
		PlayIgnitionCue();
	}

	// One cast cue per burn, on the frame the jet lights. The emitter is persistent, so the fire
	// loop's cue only ever reached its creation and every later burn started in silence. Per burn
	// rather than per puff: sixteen cues a burn is a buzz, not a cast. Creation plays it from
	// _Ready and the fire loop stays quiet for that frame, so the first burn is heard once.
	private void PlayIgnitionCue()
	{
		SfxPlayer.Cast(SfxCatalog.DominantElement(SpellData), GlobalPosition);
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	private void RefreshComputedStats()
	{
		if (SpellData == null)
		{
			range = BaseRange * AreaMultiplier;
			return;
		}

		damagePerPuff = Math.Max(1, Mathf.RoundToInt(SpellData.GetDamageAtLevel(CurrentLevel) * DamageMultiplier));

		float spellRange = SpellData.GetRangeAtLevel(CurrentLevel);
		range = MathF.Max(40f, (spellRange > 0f ? spellRange : BaseRange) * AreaMultiplier);

		rechargeSeconds = MathF.Max(0.2f, SpellData.GetCooldownAtLevel(CurrentLevel) * CooldownMultiplier);

		// With the level table's Area line (a fraction), and Zone Duration keeps the jet lit longer: the
		// Area and Duration tags' level-ups, shown on their cards and read by nothing until 2026-10-26.
		tongueWidth = MathF.Max(0.25f, SpellData.GetAreaMultiplierAtLevel(CurrentLevel)
			* (1f + SpellData.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel)));
		burnSeconds = BurnSeconds + SpellData.GetEffectValueAtLevel(SpellEffect.ZoneDuration, CurrentLevel);
		throwRate = MathF.Max(0.25f, SpellData.GetSpeedMultiplier());
		tonguesPerThrow = Math.Clamp(SpellData.GetProjectileCountAtLevel(CurrentLevel), 1, MaxTonguesPerThrow);

		// Kept current here because the emitter outlives the level 4 pick that branches it: the fire
		// loop refreshes it every cooldown.
		pathMaterial = SpellData.GetBranchElement() is Element branch ? ElementRampMaterial.For(branch) : null;

		backdraft = SpellData.HasEvolution(BackdraftId);
		emberWake = SpellData.HasEvolution(EmberWakeId);
		dragonsBreath = SpellData.HasEvolution(DragonsBreathId);
		if (dragonsBreath)
			range *= DragonReach;
		if (cinderGround != null && IsInstanceValid(cinderGround))
			cinderGround.PathMaterial = pathMaterial;
	}

	// The ground the jet's fire is left on, made the first time a card leaves any. Parented to the arena,
	// so it stays where it fell; freed with the emitter.
	private CinderGround EnsureGround()
	{
		if (cinderGround != null && IsInstanceValid(cinderGround))
			return cinderGround;
		if (PlayerRef?.GetParent() is not Node2D arena)
			return null;
		cinderGround = new CinderGround { PlayerRef = PlayerRef, SpellData = SpellData };
		arena.AddChild(cinderGround);
		cinderGround.PathMaterial = pathMaterial;
		return cinderGround;
	}

	public override void _ExitTree()
	{
		if (cinderGround != null && IsInstanceValid(cinderGround))
			cinderGround.QueueFree();
		cinderGround = null;
	}

	// The moment a burn goes out: Backdraft's burst and Dragon's Breath's wall, at the tip of every jet.
	private void BurnOut()
	{
		if (!backdraft && !dragonsBreath)
			return;
		CinderGround ground = EnsureGround();
		if (ground == null)
			return;
		int jets = tonguesPerThrow > 1 ? tonguesPerThrow : 1;
		for (int t = 0; t < jets; t++)
		{
			Vector2 dir = Vector2.FromAngle(tonguesPerThrow > 1 ? aims[t] : Rotation);
			if (backdraft)
			{
				float radius = BackdraftRadius * tongueWidth * AreaMultiplier;
				ground.Backdraft(GlobalPosition + dir * range * 0.9f, radius,
					Math.Max(1, Mathf.RoundToInt(damagePerPuff * BackdraftDamageShare)));
			}
			if (dragonsBreath)
			{
				Vector2 centre = GlobalPosition + dir * range * WallAtShare;
				Vector2 half = dir.Orthogonal() * range * WallHalfLengthShare;
				ground.RaiseWall(centre - half, centre + half, WallHalfThickness * MathF.Sqrt(tongueWidth * AreaMultiplier),
					WallSeconds, Math.Max(1, Mathf.RoundToInt(damagePerPuff * WallDamageShare)));
			}
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		stateSeconds += dt;

		if (!burning)
		{
			if (stateSeconds >= rechargeSeconds)
			{
				burning = true;
				stateSeconds = 0f;
				// Throw on the frame it lights rather than one interval later, so the first thing
				// the player sees is fire and not a pause.
				emitSeconds = EmitInterval;
				RefreshAim();
				PlayIgnitionCue();
			}
			return;
		}

		aimSeconds += dt;
		if (aimSeconds >= AimRefreshSeconds)
		{
			aimSeconds = 0f;
			RefreshAim();
		}

		emitSeconds += dt;
		if (emitSeconds >= EmitInterval / throwRate)
		{
			emitSeconds = 0f;
			for (int t = 0; t < tonguesPerThrow; t++)
				ThrowOnePuff(tonguesPerThrow > 1 ? aims[t] : Rotation);
		}

		if (stateSeconds >= burnSeconds)
		{
			burning = false;
			stateSeconds = 0f;
			BurnOut();
		}
	}

	private void RefreshAim()
	{
		Node2D nearest = null;
		float best = float.MaxValue;

		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Node2D enemy || !IsInstanceValid(node))
				continue;

			float dist = GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition);
			if (dist < best)
			{
				best = dist;
				nearest = enemy;
			}
		}

		// No target: keep pointing wherever it last pointed. Snapping back to a default direction
		// makes the jet twitch every time the last enemy in range dies.
		if (nearest != null)
		{
			Vector2 toTarget = nearest.GlobalPosition - GlobalPosition;
			if (toTarget.LengthSquared() > 0.0001f)
				Rotation = toTarget.Angle();
		}

		if (tonguesPerThrow > 1)
			SpreadAims(nearest);
	}

	// Each further jet takes the nearest enemy whose bearing is at least a cone's width from every jet
	// already aimed, within SpreadSearchRangeFactor of the reach; one that finds none is fanned evenly
	// round from the first, so three jets with one enemy about still burn three ways. A pass over the
	// swarm per jet, ten times a second, into fixed arrays - nothing allocated.
	private void SpreadAims(Node2D first)
	{
		aims[0] = Rotation;
		aimTargets[0] = first;
		float minSeparation = Mathf.DegToRad(ConeAngleDegrees);
		float searchSq = range * SpreadSearchRangeFactor * range * SpreadSearchRangeFactor;
		for (int t = 1; t < tonguesPerThrow; t++)
		{
			Node2D pick = null;
			float best = float.MaxValue;
			float pickAngle = 0f;
			foreach (Node node in EnemyRegistry.Snapshot)
			{
				if (node is not Node2D enemy || !IsInstanceValid(node))
					continue;
				Vector2 to = enemy.GlobalPosition - GlobalPosition;
				float dist = to.LengthSquared();
				if (dist >= best || dist > searchSq || dist < 0.0001f)
					continue;
				float angle = to.Angle();
				bool apart = true;
				for (int k = 0; k < t && apart; k++)
					apart = MathF.Abs(Mathf.AngleDifference(aims[k], angle)) >= minSeparation;
				if (!apart)
					continue;
				best = dist;
				pick = enemy;
				pickAngle = angle;
			}
			aimTargets[t] = pick;
			aims[t] = pick != null ? pickAngle : Rotation + Mathf.Tau * t / tonguesPerThrow;
		}
	}

	private void ThrowOnePuff(float aim)
	{
		if (FlamePuffScene == null || PlayerRef == null)
			return;

		Node2D arena = PlayerRef.GetParent<Node2D>();
		if (arena == null)
			return;

		float halfAngle = Mathf.DegToRad(ConeAngleDegrees * 0.5f);
		float angle = aim + rng.RandfRange(-halfAngle, halfAngle);

		// Speed and lifetime are derived from range together, so the cone's reach is the spell's
		// range whatever the jitter does - a faster puff simply dies sooner.
		float speedScale = 1.0f + rng.RandfRange(-SpeedJitter, SpeedJitter);
		float lifetime = MathF.Max(0.12f, BaseFlightSeconds);
		float speed = (range / lifetime) * speedScale;

		var puff = FlamePuffScene.Instantiate<Node2D>();
		if (puff is FlamePuff script)
		{
			script.SpellData = SpellData;
			script.PlayerRef = PlayerRef;
			script.Damage = damagePerPuff;
			script.Speed = speed;
			script.Lifetime = lifetime / speedScale;
			script.HitRadius *= AreaMultiplier * tongueWidth;
			// Ember Wake: every EmberEvery-th tongue drops an ember somewhere along its flight, so the
			// embers fall across the whole cone it swept rather than in a ring at its reach.
			if (emberWake && tongueCounter++ % EmberEvery == 0 && EnsureGround() is CinderGround ground)
			{
				script.EmberGround = ground;
				script.EmberAtFraction = rng.RandfRange(0.35f, 0.95f);
				script.EmberRadius = EmberRadius * tongueWidth * AreaMultiplier;
				script.EmberSeconds = EmberSeconds;
				script.EmberDamage = Math.Max(1, Mathf.RoundToInt(damagePerPuff * EmberDamageShare));
			}
		}

		arena.AddChild(puff);
		puff.GlobalPosition = GlobalPosition;
		puff.Rotation = angle;
		// The evolutions' width is drawn as well as hit. (The player's own Area still grows only the
		// hit, as it did before the evolutions reached the jet.)
		if (MathF.Abs(tongueWidth - 1f) > 0.01f)
			puff.Scale = Vector2.One * tongueWidth;
	}

	// How long a tongue is in the air. Held constant so the animation always plays at a sane rate;
	// range is bought with speed instead.
	private const float BaseFlightSeconds = 0.42f;
}
