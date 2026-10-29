using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Scorching Ray beam: extends from caster to target, then retracts into the target. Damage is
// applied once when the beam fully reaches the target.
//
// What its evolutions buy (it used to take the default archetype's words for a bolt, and read only
// damage, cooldown and count): Speed is how fast the beam reaches its target; Pierce is how many
// enemies it burns through BEHIND its target, along the beam, which is then drawn through to the last
// of them; Area is a burst where it lands - there is none until an evolution grows one - that scorches
// what stands beside the target for SplashDamageFraction, thrown out to exactly its radius on the
// arena's debris layer (ImpactDebrisLayer), so the burst is drawn as wide as it hurts.
//
// EVERY CARD ITS OWN (.ai/spell-roster.md section 2e, built 2026-10-29). Each milestone changes what
// a beam does where you can see it, and its numbers ride along:
//   - Flashpoint (level 4): the burst grows with the beam's heat, and a target held to full heat
//     flashes over in a blast FlashpointHeatGrowth wider at the ray's full damage.
//   - Handoff (level 4): a ray whose target dies swings on to the nearest enemy and burns it too, as
//     hot as it was, once a cast - and that enemy is held at that heat into the next cast
//     (Player.HandOffScorch).
//   - Burn Through (level 4): the beam runs on through its target to the edge of the screen, burning
//     everything on its line.
//   - Prism (level 8): the ray splits where it strikes into PrismSplits rays at the enemies round its
//     target, each carrying its heat on.
//   - Sunlance (level 8): the ray widens into a lance that stays lit and sweeps on from its target to
//     the next, and the next, burning everything it crosses.
// The ids are the archetype's, kept so every save's choices and the Ascension browser carry over.
public partial class ScorchingRayBeam : Node2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float ExtendDuration { get; set; } = 0.11f;
	[Export] public float RetractDuration { get; set; } = 0.10f;
	/// <summary>How long the beam stays on at full length after it lands: the look of a laser, not its damage.</summary>
	[Export] public float HoldDuration { get; set; } = 0.16f;

	public const string FlashpointId = "scorching_ray_amplified_impact";
	public const string HandoffId = "scorching_ray_hyper_acceleration";
	public const string BurnThroughId = "scorching_ray_reactive_catalyst";
	public const string PrismId = "scorching_ray_titan_cataclysm";
	public const string SunlanceId = "scorching_ray_infinite_horizon";

	/// <summary>Flashpoint: the burst is this much wider at full heat than on a fresh target.</summary>
	// 0.6, not the first build's 1 (twice as wide): with the old card's +35% the level 7 ray measured 755
	// a second against the old Amplified Impact's 410 (2026-10-29, --enemy-hp=30), and 76 at level 4.
	public const float FlashpointHeatGrowth = 0.6f;
	/// <summary>Handoff: how many times one ray may swing on in a cast, and how far from the fallen it looks.</summary>
	// Once, not the first build's twice: with two swings the card measured 52 and 80 a second at level 4
	// and 83-91 at level 5 (2026-10-29), against the old Hyper Acceleration's 43 and 60 - the strongest
	// level 4 of the four, over the band. A swarm body falls to one or two rays, so a second swing was
	// nearly always taken.
	public const int MaxHandoffs = 1;
	public const float HandoffReach = 170f;
	private const float SwingSeconds = 0.09f;
	/// <summary>Prism: the rays a strike splits into, how far round the target they reach, and their share.</summary>
	// Four, carrying their heat on (Split), with +60% on the card. Three at +70% measured x1.50 of the spell
	// at level 7 (969 against 646, 2026-10-29, --enemy-hp=30) - the old Titan's burst, three times as wide,
	// hit more of a packed crowd - and four at +100% x1.69, because the harder rays only killed their held
	// targets sooner and started cold. With the heat carried on, +100% made x2.85 and 512 at level 8 in
	// the band run, over the band; +60% is the cut.
	public const int PrismSplits = 4;
	public const float PrismReach = 190f;
	// 0.8 with +70% on the card: at 0.6 and +25% the Ascension measured x1.26 of the spell at level 7.
	public const float PrismDamageShare = 0.8f;
	/// <summary>Sunlance: how many times a lance sweeps on, how far it looks for the next, and how wide it burns.</summary>
	public const int SunlanceSweeps = 2;
	public const float SweepReach = 210f;
	private const float SweepSeconds = 0.16f;
	public const float LanceHalfWidth = 26f;
	private const int MaxSweepHits = 40;
	// Burn Through, when the screen's edge cannot be read (a headless run with no camera): this far.
	private const float BurnThroughFallbackReach = 760f;
	private const int MaxBurnThrough = 24;

	// A THIN FIERY RED LASER THAT SPLASHES INTO THE GROUND (2026-10-28). Eric: "scorching ray looks like
	// a big purple tube. instead make it a thin fiery red laser splashing into the ground." It was a
	// 10-13 px orange bar with square ends, and ApplyLegendaryVisual multiplied it by the spell's
	// evolution/element tint, which turned it violet. Now: a white-hot core inside a narrow flickering red
	// glow, never tinted (Fire resets the Modulate), ending at the target's feet in a splash - a pool of
	// molten light on the ground and droplets and sparks thrown out of it. It holds at full length for
	// HoldDuration so the splash is seen; the damage still lands the frame it arrives.
	private const float FootDrop = 16f;
	// Prism's split rays are the ray's light broken by the Arcane it took: a violet edge round the same
	// white-hot core, so they read as the same fire split, not as a second spell.
	private static readonly ElementColors.Ramp ArcaneRamp = ElementColors.GetRamp(Element.Arcane);
	private static readonly ElementColors.Ramp FireRamp = ElementColors.GetRamp(Element.Fire);
	private Vector2 splashAt;
	private float splashAlpha;

	// COOLER (2026-10-28, Eric: "make the animation of scorching ray cooler"). The thin laser stays; what
	// it gained is a life: a flare and a four-point glint at the wizard as it fires, a white-hot head
	// racing out ahead of the beam, pulses of heat running down it while it holds, a white flash and a
	// crown of molten droplets where it lands, sparks thrown up in arcs that fall back under gravity, a
	// curl of smoke, and a scorch on the ground - a char with glowing cracks - that cools for
	// ScorchSeconds after the beam has gone.
	//
	// PIXEL ART (2026-10-29, Eric: "Update all spells to the same quality we expect now"). All of that was
	// two Line2Ds and nine antialiased draw calls - translucent washes, soft circles, a stroked ring -
	// sitting badly in a hard-edged, nearest-filtered game. Now the beam, its flare, head, pulses, pool,
	// flash, flashover, smoke and Prism's split rays are one shader (scenes/shaders/scorching_ray.gdshader)
	// that snaps every fragment to the world's 2 px cell before it measures anything, so the laser is a
	// staircase of square art pixels at any angle; the droplets, sparks and glint are square cells stamped
	// on the same grid in _Draw; and the scorch is a burn on the floor (ScorchGround). Every colour is an
	// opaque Fire, ember-row or Arcane tone, and fading is dithered. The shockwave ring went: it was drawn
	// at a fixed 40 px round a ray that strikes one body, which promised an area the ray does not have
	// (.ai/art-direction.md section 3, "no area markers"); the droplets are the splash instead.
	private const int Sparks = 10;
	private const float SparkLife = 0.42f;
	private const float SparkGravity = 560f;
	private const int Droplets = 8;
	private const float DropletSeconds = 0.28f;
	private const float ScorchSeconds = 1.3f;
	private const float FlashSeconds = 0.14f;
	private static readonly Color FireCore = new(1f, 0.957f, 0.816f);      // fire core #FFF4D0
	private static readonly Color FireHot = new(1f, 0.784f, 0.282f);       // fire hot #FFC848
	private static readonly Color EmberHi = new(1f, 0.753f, 0.659f);       // ember hi #FFC0A8
	private static readonly Color EmberLit = new(1f, 0.416f, 0.290f);      // ember lit #FF6A4A
	private static readonly Color EmberBase = new(0.847f, 0.141f, 0.180f); // ember base #D8242E
	private const float Grid = WhipLimb.Grid;

	private static Shader? rayShader;
	private static readonly StringName ParamBeamFrom = "beam_from";
	private static readonly StringName ParamBeamTo = "beam_to";
	private static readonly StringName ParamBeamCover = "beam_cover";
	private static readonly StringName ParamHeat = "heat";
	private static readonly StringName ParamLance = "lance";
	private static readonly StringName ParamPulses = "pulses";
	private static readonly StringName ParamHead = "head";
	private static readonly StringName ParamTime = "time";
	private static readonly StringName ParamSeed = "seed";
	private static readonly StringName ParamFringeFrom = "fringe_from";
	private static readonly StringName ParamFringeHalf = "fringe_half";
	private static readonly StringName ParamFringeCover = "fringe_cover";
	private static readonly StringName ParamFringeFlames = "fringe_flames";
	private static readonly StringName ParamMuzzle = "muzzle";
	private static readonly StringName ParamImpactAt = "impact_at";
	private static readonly StringName ParamSince = "since";
	private static readonly StringName ParamSplash = "splash";
	private static readonly StringName ParamSmoke = "smoke";
	private static readonly StringName ParamGrow = "grow";
	private static readonly StringName ParamFlashRadius = "flash_radius";
	private static readonly StringName ParamPrismFrom = "prism_from";
	private static readonly StringName ParamPrismCount = "prism_count";
	private static readonly StringName ParamPrismCover = "prism_cover";
	private static readonly StringName[] ParamPrismEnds = { "prism_a", "prism_b", "prism_c", "prism_d" };
	private ShaderMaterial? rayMaterial;
	// The quads the shader paints: one along the beam, one along each split ray (turned to them), and one
	// holding the boxes over the strike and the wizard's hands. They only say where to look - every
	// fragment shades the whole ray at its cell, so two quads that overlap paint the same pixel alike.
	private RayQuad? beamQuad;
	private RayQuad? fxQuad;
	private readonly RayQuad?[] prismQuads = new RayQuad?[PrismSplits];
	// A Legendary's gold, multiplied into every colour (the shader and the cells); the node's own modulate
	// stays white, because the scorch under it carries its numbers in a vertex colour.
	private Color gild = Colors.White;
	// The Sunlance's sweep: how much of its band of flames is still drawn after it stops sweeping.
	private float sweepGlow;
	private float impactTime = -1f;
	private float muzzleAlpha;
	private Vector2 headAt;
	private bool headShown;
	private Vector2 beamFrom;
	private Vector2 beamTo;
	private float beamAlpha;
	private float afterglow;
	private ScorchMark? scorch;
	[Export] public float HitFallbackRadius { get; set; } = 26f;

	// The burst an evolution's Area grows, before that Area: at x1.3 (Flashpoint) 39 px - times the
	// player's area, and with Flashpoint up to twice that on a beam at full heat.
	public const float BaseSplashRadius = 30f;
	public const float SplashDamageFraction = 0.5f;
	// How far either side of the beam's line an enemy's centre may be and still be burned through: the
	// beam's own half width and half an enemy body (the allowance RiptideWave measures the same way).
	public const float PierceHalfWidth = 18f;
	private const int MaxPierce = 12;

	public float DamageMultiplier { get; set; } = 1.0f;

	/// <summary>
	/// How hot this beam is, 0 (a fresh target) to 1 (held on it for Player.ScorchRampCasts casts):
	/// the ramp on a held target (Player.FireScorchingRay). The damage is in DamageMultiplier; this is
	/// how it looks - a held beam thickens and burns from orange toward white, so the player can see
	/// which beam is cooking the boss.
	/// </summary>
	public float Heat { get; set; }
	public float AreaMultiplier { get; set; } = 1.0f;
	public Player? PlayerRef { get; set; }

	private enum BeamPhase
	{
		Idle,
		Extending,
		Holding,
		// Handoff: the beam's end swings from a fallen target to the next.
		Swinging,
		// Sunlance: the lance's end sweeps from its target to the next, burning what it crosses.
		Sweeping,
		Retracting,
		// The beam is gone; its scorch cools on the ground, then the node frees itself.
		Afterglow
	}

	private BeamPhase phase = BeamPhase.Idle;
	private float phaseTimer = 0f;
	private int damage = 1;
	private float speed = 1f;
	private int pierce = 0;
	private float range = 450f;
	private float splashRadius = 0f;
	private Vector2 origin = Vector2.Zero;
	private Vector2 lockedTargetPosition = Vector2.Zero;
	// Where the beam ends once it has burned through what stood behind its target; the target until then.
	private Vector2 throughPosition = Vector2.Zero;
	// Where the band it burned through starts: the target it struck.
	private Vector2 throughFrom = Vector2.Zero;
	private bool burnedThrough = false;
	private Node2D? targetNode;
	private bool didDamage = false;

	private bool flashpoint;
	private bool handoff;
	private bool burnThrough;
	private bool prism;
	private bool sunlance;
	private int handoffsLeft;
	private int sweepsLeft;
	// Where a swing or a sweep starts, and the enemy it ends on.
	private Vector2 moveFrom;
	private float flashRadius;
	// Sunlance: what this lance has burned this cast, so a sweep that passes a body twice burns it once.
	private readonly ulong[] sweepHit = new ulong[MaxSweepHits];
	private int sweepHitCount;
	// Where the lance's end pointed last frame, as a bearing and a length from the wizard.
	private float sweepAngleTo;
	private float sweepLengthTo;
	// Prism: where the split rays end, while they are drawn.
	private readonly Vector2[] prismEnds = new Vector2[PrismSplits];
	private int prismCount;
	private Vector2 prismFrom;

	// One cast at a time on the main thread, so the scan for what stands behind the target shares one
	// buffer rather than allocating per ray.
	private static readonly Node2D[] pierceBuffer = new Node2D[MaxBurnThrough];
	private static readonly float[] pierceAlong = new float[MaxBurnThrough];
	private static readonly List<Enemy> nearBuffer = new(64);
	private static readonly Enemy[] prismBuffer = new Enemy[PrismSplits];
	private static readonly float[] prismDistance = new float[PrismSplits];
	private float shimmerTime = 0f;

	public override void _Ready()
	{
		// The beam, its splash, its split rays and its lance are in the air, over every body (DrawLayers);
		// the shader's quads are children on the same layer, drawn under the cells this node stamps.
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		rayShader ??= GD.Load<Shader>("res://scenes/shaders/scorching_ray.gdshader");
		rayMaterial = new ShaderMaterial { Shader = rayShader };
		// Two rays cast together should not flicker in step.
		rayMaterial.SetShaderParameter(ParamSeed, (GetInstanceId() % 997) * 0.731f);
		beamQuad = AddQuad();
		fxQuad = AddQuad();
		// The scorch lies on the ground, under every actor.
		scorch = new ScorchMark { ZAsRelative = false, ZIndex = DrawLayers.Ground, Visible = false };
		AddChild(scorch);
		RefreshComputedStats();
	}

	private RayQuad AddQuad()
	{
		var quad = new RayQuad { Material = rayMaterial, ShowBehindParent = true, Visible = false };
		AddChild(quad);
		return quad;
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	public void Fire(Vector2 from, Node2D? target, Vector2 fallbackTargetPosition)
	{
		origin = from;
		targetNode = target;
		lockedTargetPosition = target != null && IsInstanceValid(target) ? target.GlobalPosition : fallbackTargetPosition;
		didDamage = false;
		phase = BeamPhase.Extending;
		phaseTimer = 0f;
		impactTime = -1f;
		afterglow = 0f;
		handoffsLeft = handoff ? MaxHandoffs : 0;
		sweepsLeft = sunlance ? SunlanceSweeps : 0;
		sweepHitCount = 0;
		prismCount = 0;
		// Its own colours, always: the spell's evolution and element tints (Player.ApplyLegendaryVisual)
		// multiplied the fire into a violet tube. A Legendary keeps a touch of its gold - in the colours it
		// draws, not in the modulate, which would reach the scorch's numbers (ScorchGround).
		Modulate = Colors.White;
		gild = SpellData?.IsLegendary == true ? new Color(1f, 0.95f, 0.78f) : Colors.White;
		if (beamQuad != null)
			beamQuad.Modulate = gild;
		if (fxQuad != null)
			fxQuad.Modulate = gild;
		UpdateVisual(origin, origin, 0f);
	}

	public override void _Process(double delta)
	{
		if (phase == BeamPhase.Idle)
			return;

		shimmerTime += (float)delta;
		// Sunlance's band of flames is drawn while it sweeps (that is when it burns) and goes out after.
		sweepGlow = phase == BeamPhase.Sweeping ? 1f : MathF.Max(0f, sweepGlow - (float)delta / 0.12f);

		if (phase == BeamPhase.Afterglow)
		{
			afterglow += (float)delta;
			float cool = afterglow / ScorchSeconds;
			if (scorch != null)
				scorch.Cool = cool;
			if (cool >= 1f)
				QueueFree();
			else
				UpdateAfterglow(); // the last sparks and the smoke finish their flight
			return;
		}

		Vector2 targetPos = GetTargetPosition();

		if (phase == BeamPhase.Extending)
		{
			phaseTimer += (float)delta;
			float t = Mathf.Clamp(phaseTimer / Mathf.Max(0.01f, ExtendDuration / speed), 0f, 1f);
			Vector2 beamEnd = origin.Lerp(targetPos, t);
			UpdateVisual(origin, beamEnd, t);

			if (t >= 1f)
				Arrive(targetPos, directHit: true);
			return;
		}

		if (phase == BeamPhase.Swinging || phase == BeamPhase.Sweeping)
		{
			phaseTimer += (float)delta;
			float seconds = phase == BeamPhase.Swinging ? SwingSeconds : SweepSeconds;
			float t = Mathf.Clamp(phaseTimer / Mathf.Max(0.01f, seconds / speed), 0f, 1f);
			Vector2 end = moveFrom.Lerp(targetPos, t);
			if (phase == BeamPhase.Sweeping)
				SweepTo(end);
			UpdateVisual(origin, end, 1f);
			if (t >= 1f)
				Arrive(targetPos, directHit: phase == BeamPhase.Swinging || !AlreadySwept(targetNode));
			return;
		}

		if (phase == BeamPhase.Holding)
		{
			phaseTimer += (float)delta;
			UpdateVisual(origin, burnedThrough ? throughPosition : targetPos, 1f);
			if (phaseTimer >= HoldDuration)
			{
				phase = BeamPhase.Retracting;
				phaseTimer = 0f;
			}
			return;
		}

		if (phase == BeamPhase.Retracting)
		{
			phaseTimer += (float)delta;
			float t = Mathf.Clamp(phaseTimer / Mathf.Max(0.01f, RetractDuration / speed), 0f, 1f);
			// A beam that burned through holds the line it burned, to the last enemy behind the target.
			Vector2 end = burnedThrough ? throughPosition : targetPos;
			Vector2 beamStart = origin.Lerp(end, t);
			UpdateVisual(beamStart, end, 1f - t);
			if (t >= 1f)
			{
				// The beam goes; the node stays while its scorch cools (and frees itself then).
				phase = BeamPhase.Afterglow;
				afterglow = 0f;
			}
		}
	}

	// The beam has reached a target - its first, or the next after a swing or a sweep: it strikes, and
	// then swings on (Handoff, the target fell), sweeps on (Sunlance), or holds.
	private void Arrive(Vector2 at, bool directHit)
	{
		bool fell = StrikeAt(at, directHit);
		impactTime = shimmerTime;
		LeaveScorch(splashAt);
		phaseTimer = 0f;

		if (fell && handoffsLeft > 0 && FindNext(at, HandoffReach) is Node2D next)
		{
			handoffsLeft--;
			BeginMove(BeamPhase.Swinging, at, next);
			PlayerRef?.HandOffScorch(next, Heat);
			return;
		}
		if (sweepsLeft > 0 && FindNext(at, SweepReach) is Node2D onward)
		{
			sweepsLeft--;
			BeginMove(BeamPhase.Sweeping, at, onward);
			return;
		}
		phase = HoldDuration > 0f ? BeamPhase.Holding : BeamPhase.Retracting;
	}

	private void BeginMove(BeamPhase move, Vector2 from, Node2D next)
	{
		phase = move;
		moveFrom = from;
		targetNode = next;
		lockedTargetPosition = next.GlobalPosition;
		burnedThrough = false;
		if (move == BeamPhase.Sweeping)
		{
			Vector2 a = from - origin;
			sweepAngleTo = a.Angle();
			sweepLengthTo = a.Length();
		}
	}

	// The nearest living enemy to a point, within reach of it and within the spell's range of the wizard,
	// that this beam is not already on and (for a lance) has not already burned.
	private Node2D? FindNext(Vector2 from, float reach)
	{
		Node2D? best = null;
		float bestDistance = reach * reach;
		float rangeSq = range * range;
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (!IsInstanceValid(enemy) || enemy.IsDying || enemy == targetNode)
				continue;
			Vector2 p = enemy.GlobalPosition;
			float d = from.DistanceSquaredTo(p);
			if (d >= bestDistance || origin.DistanceSquaredTo(p) > rangeSq || AlreadySwept(enemy))
				continue;
			bestDistance = d;
			best = enemy;
		}
		return best;
	}

	private bool AlreadySwept(Node2D? enemy)
	{
		if (enemy == null || sweepHitCount == 0)
			return false;
		ulong id = enemy.GetInstanceId();
		for (int i = 0; i < sweepHitCount; i++)
		{
			if (sweepHit[i] == id)
				return true;
		}
		return false;
	}

	// Sunlance: the lance's end has moved to `end` this frame. Everything whose bearing from the wizard it
	// has swept past since the last frame, within the lance's length and half its width, burns once.
	private void SweepTo(Vector2 end)
	{
		Vector2 rel = end - origin;
		float angle = rel.Angle();
		float length = rel.Length();
		float from = sweepAngleTo;
		float step = Mathf.AngleDifference(from, angle);
		float lo = MathF.Min(0f, step), hi = MathF.Max(0f, step);
		float reach = MathF.Max(length, sweepLengthTo) + PierceHalfWidth;
		float halfWidth = LanceHalfWidth * (1f + Heat);
		sweepAngleTo = angle;
		sweepLengthTo = length;
		if (PlayerRef == null)
			return;
		float critBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.CritChance, CurrentLevel) ?? 0f;
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (!IsInstanceValid(enemy) || enemy.IsDying || sweepHitCount >= MaxSweepHits)
				continue;
			Vector2 p = enemy.GlobalPosition - origin;
			float d = p.Length();
			if (d > reach || d < 1f)
				continue;
			float slack = MathF.Atan2(halfWidth, d);
			float off = Mathf.AngleDifference(from, p.Angle());
			if (off < lo - slack || off > hi + slack || AlreadySwept(enemy))
				continue;
			sweepHit[sweepHitCount++] = enemy.GetInstanceId();
			PlayerRef.DealDamageToEnemy(enemy, damage, critBonus, source: SpellData);
		}
	}

	private Vector2 GetTargetPosition()
	{
		if (targetNode != null && IsInstanceValid(targetNode))
			lockedTargetPosition = targetNode.GlobalPosition;
		return lockedTargetPosition;
	}

	// The strike where the beam lands: its target (unless a lance already burned it on the way), then
	// what stands behind it, its burst and its split rays. True when the target fell to it.
	private bool StrikeAt(Vector2 impactPosition, bool directHit)
	{
		didDamage = true;
		throughPosition = impactPosition;
		burnedThrough = false;
		if (PlayerRef == null)
			return false;

		// The source rides every hit now, as for every other spell: it is what credits the ray in the
		// per-spell damage table, picks its impact sound and lets a per-spell crit damage apply.
		float critBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.CritChance, CurrentLevel) ?? 0f;
		Node2D? struck = null;
		// An Enemy in the registry is a live target (EnemyRegistry is the "enemies" group as an array), so the
		// per-hit string lookups this used to make are not needed.
		if (targetNode is Enemy held && IsInstanceValid(held) && held.InEnemyRegistry)
		{
			struck = held;
		}
		else
		{
			// The target is gone: whatever stands nearest where it was, within reach of the beam's end.
			float best = HitFallbackRadius * HitFallbackRadius;
			foreach (Enemy enemy in EnemyRegistry.Snapshot)
			{
				if (!IsInstanceValid(enemy) || enemy.IsDying)
					continue;
				float d = impactPosition.DistanceSquaredTo(enemy.GlobalPosition);
				if (d <= best)
				{
					best = d;
					struck = enemy;
				}
			}
		}
		if (struck == null)
			return false;

		if (directHit)
		{
			PlayerRef.DealDamageToEnemy(struck, damage, critBonus, source: SpellData);
			if (sunlance && sweepHitCount < MaxSweepHits)
				sweepHit[sweepHitCount++] = struck.GetInstanceId();
		}
		BurnThrough(impactPosition, struck, critBonus);
		Burst(impactPosition, struck, critBonus);
		Split(impactPosition, struck, critBonus);
		return struck is Enemy fallen && (fallen.IsDying || fallen.Health <= 0);
	}

	// Pierce: the enemies standing behind the target on the beam's line, nearest first, out to the
	// spell's range from the caster - each takes the ray's full damage, as a piercing bolt's victims do.
	// Burn Through: everything on the line, out to the edge of the screen, and the beam is drawn there.
	private void BurnThrough(Vector2 impact, Node2D struck, float critBonus)
	{
		Vector2 line = impact - origin;
		float reach = line.Length();
		if (reach < 1f || PlayerRef == null)
			return;
		Vector2 dir = line / reach;
		float limit = burnThrough ? ScreenEdgeAlong(dir) : range;
		int want = burnThrough ? MaxBurnThrough : Math.Min(pierce, MaxPierce);
		if (want <= 0)
			return;
		Vector2 side = new Vector2(-dir.Y, dir.X);

		int found = 0;
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (enemy == struck || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			Vector2 rel = enemy.GlobalPosition - impact;
			float along = rel.Dot(dir);
			if (along <= 0f || reach + along > limit || MathF.Abs(rel.Dot(side)) > PierceHalfWidth)
				continue;
			// Insertion into the nearest `want`, so the beam burns the next bodies in line, not any.
			if (found == want && along >= pierceAlong[found - 1])
				continue;
			int slot = found < want ? found++ : want - 1;
			while (slot > 0 && pierceAlong[slot - 1] > along)
			{
				pierceAlong[slot] = pierceAlong[slot - 1];
				pierceBuffer[slot] = pierceBuffer[slot - 1];
				slot--;
			}
			pierceAlong[slot] = along;
			pierceBuffer[slot] = enemy;
		}

		for (int i = 0; i < found; i++)
		{
			if (IsInstanceValid(pierceBuffer[i]))
				PlayerRef.DealDamageToEnemy(pierceBuffer[i], damage, critBonus, source: SpellData);
			pierceBuffer[i] = null!;
		}
		if (burnThrough)
		{
			// The beam runs on to the edge of the screen whatever it found there.
			burnedThrough = true;
			throughPosition = origin + dir * limit;
		}
		else if (found > 0)
		{
			burnedThrough = true;
			throughPosition = impact + dir * pierceAlong[found - 1];
		}
		// The band the pierce test burned starts at the target; the shader draws it from here on out.
		throughFrom = impact;
	}

	// How far from the wizard, along `dir`, the edge of what the camera shows is - the screen the
	// player is looking at, on a phone or a PC alike. Read once a ray.
	private float ScreenEdgeAlong(Vector2 dir)
	{
		Viewport viewport = GetViewport();
		if (viewport == null)
			return BurnThroughFallbackReach;
		Transform2D toWorld = viewport.GetCanvasTransform().AffineInverse();
		Rect2 visible = viewport.GetVisibleRect();
		Vector2 a = toWorld * visible.Position;
		Vector2 b = toWorld * visible.End;
		Vector2 min = new(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y));
		Vector2 max = new(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y));
		float t = float.MaxValue;
		if (MathF.Abs(dir.X) > 0.0001f)
			t = MathF.Min(t, ((dir.X > 0f ? max.X : min.X) - origin.X) / dir.X);
		if (MathF.Abs(dir.Y) > 0.0001f)
			t = MathF.Min(t, ((dir.Y > 0f ? max.Y : min.Y) - origin.Y) / dir.Y);
		// A camera that does not frame the wizard (a headless run, a menu) says nothing useful.
		return t > 60f && t < 3000f ? t : BurnThroughFallbackReach;
	}

	// Area: a burst where the ray lands, for a share of its damage, drawn to exactly its radius.
	// Flashpoint: the burst grows with the beam's heat, and at full heat the target flashes over at the
	// ray's full damage.
	private void Burst(Vector2 impact, Node2D struck, float critBonus)
	{
		if (splashRadius <= 0f || PlayerRef == null)
			return;
		float radius = splashRadius;
		int splashDamage = Math.Max(1, Mathf.RoundToInt(damage * SplashDamageFraction));
		bool flashover = false;
		if (flashpoint)
		{
			radius *= 1f + FlashpointHeatGrowth * Heat;
			if (Heat >= 0.999f)
			{
				flashover = true;
				splashDamage = damage;
			}
		}
		float r2 = radius * radius;
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (enemy == struck || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			if (impact.DistanceSquaredTo(enemy.GlobalPosition) <= r2)
				PlayerRef.DealDamageToEnemy(enemy, splashDamage, critBonus, source: SpellData);
		}
		flashRadius = flashover ? radius : 0f;
		if (GetParent() is Node2D arena)
			ImpactDebrisLayer.Spawn(arena, impact, radius, FireRamp, flashover ? new Color(1f, 1f, 0.9f) : Colors.White);
	}

	// Prism: the ray breaks where it strikes into PrismSplits rays at the enemies nearest its target, each
	// for PrismDamageShare of the ray, its heat and all.
	private void Split(Vector2 impact, Node2D struck, float critBonus)
	{
		prismCount = 0;
		if (!prism || PlayerRef == null)
			return;
		EnemyRegistry.CollectNear(impact, PrismReach, nearBuffer);
		float reachSq = PrismReach * PrismReach;
		int found = 0;
		foreach (Enemy enemy in nearBuffer)
		{
			if (enemy == struck || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			float d = impact.DistanceSquaredTo(enemy.GlobalPosition);
			if (d > reachSq || (found == PrismSplits && d >= prismDistance[found - 1]))
				continue;
			int slot = found < PrismSplits ? found++ : PrismSplits - 1;
			while (slot > 0 && prismDistance[slot - 1] > d)
			{
				prismDistance[slot] = prismDistance[slot - 1];
				prismBuffer[slot] = prismBuffer[slot - 1];
				slot--;
			}
			prismDistance[slot] = d;
			prismBuffer[slot] = enemy;
		}
		nearBuffer.Clear();
		int splitDamage = Math.Max(1, Mathf.RoundToInt(damage * PrismDamageShare));
		prismFrom = impact;
		for (int i = 0; i < found; i++)
		{
			Enemy target = prismBuffer[i];
			prismBuffer[i] = null!;
			if (!IsInstanceValid(target))
				continue;
			prismEnds[prismCount++] = target.GlobalPosition + new Vector2(0f, FootDrop);
			PlayerRef.DealDamageToEnemy(target, splitDamage, critBonus, source: SpellData);
			// The split carries the heat on: what it burned is held that hot into the next cast, as a
			// Handoff's next target is. Without it the stronger rays only killed their held targets
			// sooner and started cold again, and the Ascension measured x1.69 of the spell at level 7.
			if (IsInstanceValid(target) && !target.IsDying)
				PlayerRef.HandOffScorch(target, Heat);
		}
		if (prismCount > 0 && GetParent() is Node2D arena)
		{
			for (int i = 0; i < prismCount; i++)
			{
				ImpactDebrisLayer.Spawn(arena, prismEnds[i], 16f, ArcaneRamp, Colors.White);
				// A quad along each split ray for the shader, made the first time this ray splits.
				if (prismQuads[i] == null)
				{
					RayQuad quad = AddQuad();
					quad.Modulate = gild;
					prismQuads[i] = quad;
				}
			}
		}
	}

	private void UpdateVisual(Vector2 from, Vector2 to, float alphaScale)
	{
		float clampedAlpha = Mathf.Clamp(alphaScale, 0f, 1f);
		// Into the ground at the target's feet, not its middle: the splash is on the floor.
		Vector2 ground = to + new Vector2(0f, FootDrop * clampedAlpha);
		splashAt = ground;
		splashAlpha = (phase == BeamPhase.Extending) ? 0f : clampedAlpha;
		beamFrom = from;
		beamTo = ground;
		beamAlpha = clampedAlpha;
		// The head leads the beam out; the flare at the wizard blooms as it fires and dies through the hold.
		headShown = phase == BeamPhase.Extending;
		headAt = ground;
		muzzleAlpha = phase switch
		{
			BeamPhase.Extending => Mathf.Min(1f, 0.4f + clampedAlpha),
			BeamPhase.Holding => 1f - 0.7f * Mathf.Clamp(phaseTimer / Mathf.Max(0.01f, HoldDuration), 0f, 1f),
			BeamPhase.Swinging or BeamPhase.Sweeping => 0.8f,
			_ => 0.3f * clampedAlpha,
		};
		PushShader();
		QueueRedraw();
	}

	// The beam is gone: the smoke and the last sparks finish, over the cooling scorch.
	private void UpdateAfterglow()
	{
		PushShader();
		QueueRedraw();
	}

	private bool BeamOn => phase is BeamPhase.Extending or BeamPhase.Holding or BeamPhase.Retracting
		or BeamPhase.Swinging or BeamPhase.Sweeping;

	// Every number the shader draws from, and the quads it paints. The beam is drawn to the ground under
	// what it struck; the band it burns is drawn out of the half widths the hit tests use (PierceHalfWidth
	// behind a target it burns through, the lance's width while it sweeps); the flashover out to the radius
	// its burst hit. Uniforms are plain values - nothing here allocates.
	private void PushShader()
	{
		if (rayMaterial == null)
			return;
		bool beamOn = BeamOn;
		float lance = sunlance ? 2.2f + 1.3f * Heat : 1f;
		float grow = 1f + 0.5f * Heat;
		float cover = !beamOn ? 0f : phase == BeamPhase.Extending ? 1f : beamAlpha;
		rayMaterial.SetShaderParameter(ParamBeamFrom, beamFrom);
		rayMaterial.SetShaderParameter(ParamBeamTo, beamTo);
		rayMaterial.SetShaderParameter(ParamBeamCover, cover);
		rayMaterial.SetShaderParameter(ParamHeat, Heat);
		rayMaterial.SetShaderParameter(ParamLance, lance);
		rayMaterial.SetShaderParameter(ParamPulses, phase is BeamPhase.Holding or BeamPhase.Sweeping or BeamPhase.Swinging);
		rayMaterial.SetShaderParameter(ParamHead, headShown && beamOn);
		rayMaterial.SetShaderParameter(ParamTime, shimmerTime);
		rayMaterial.SetShaderParameter(ParamMuzzle, beamOn ? muzzleAlpha : 0f);
		rayMaterial.SetShaderParameter(ParamGrow, grow);

		// The band it burns: Sunlance's sweep (LanceHalfWidth wider the hotter, as SweepTo tests), or the
		// line behind a target it burned through (PierceHalfWidth, as BurnThrough tests).
		Vector2 run = beamTo - beamFrom;
		float length = run.Length();
		float fringeHalf = 0f, fringeFrom = 0f, fringeCover = 0f;
		bool flames = false;
		if (sunlance && sweepGlow > 0f)
		{
			fringeHalf = LanceHalfWidth * (1f + Heat);
			fringeCover = sweepGlow * cover;
			flames = true;
		}
		else if (burnedThrough && length > 1f)
		{
			fringeHalf = PierceHalfWidth;
			fringeFrom = MathF.Max(0f, (throughFrom - beamFrom).Dot(run / length));
			fringeCover = cover;
		}
		rayMaterial.SetShaderParameter(ParamFringeFrom, fringeFrom);
		rayMaterial.SetShaderParameter(ParamFringeHalf, fringeHalf);
		rayMaterial.SetShaderParameter(ParamFringeCover, fringeCover);
		rayMaterial.SetShaderParameter(ParamFringeFlames, flames);

		float since = impactTime < 0f ? -1f : shimmerTime - impactTime;
		rayMaterial.SetShaderParameter(ParamImpactAt, splashAt);
		rayMaterial.SetShaderParameter(ParamSince, since);
		rayMaterial.SetShaderParameter(ParamSplash, beamOn ? splashAlpha : 0f);
		rayMaterial.SetShaderParameter(ParamSmoke, phase == BeamPhase.Afterglow ? Mathf.Clamp(1f - afterglow / 0.6f, 0f, 1f) : 1f);
		rayMaterial.SetShaderParameter(ParamFlashRadius, flashRadius);

		Vector2 split = prismFrom + new Vector2(0f, FootDrop);
		int prisms = beamOn ? prismCount : 0;
		rayMaterial.SetShaderParameter(ParamPrismFrom, split);
		rayMaterial.SetShaderParameter(ParamPrismCount, prisms);
		rayMaterial.SetShaderParameter(ParamPrismCover, beamOn ? beamAlpha : 0f);
		for (int i = 0; i < prismCount; i++)
			rayMaterial.SetShaderParameter(ParamPrismEnds[i], prismEnds[i]);

		// The quads. Along the beam, turned to it, as wide as its sheath, crackle and band.
		if (beamQuad != null)
		{
			beamQuad.Visible = cover > 0f && length > 0.5f;
			if (beamQuad.Visible)
			{
				float half = (1.1f + 0.9f * Heat) * lance + Grid * (3f + 4f * lance) + 2f;
				half = MathF.Max(half, fringeHalf + Grid);
				beamQuad.Position = ToLocal(beamFrom);
				beamQuad.Rotation = run.Angle();
				beamQuad.Set(new Rect2(-10f, -half, length + 20f, half * 2f));
			}
		}
		// Over the strike (the flash, the pool, the flashover, the head and the smoke rising off it) and over
		// the wizard's hands.
		if (fxQuad != null)
		{
			Vector2 at = ToLocal(splashAt);
			float wide = MathF.Max(24f * grow, flashRadius) + 4f;
			float up = MathF.Max(wide, 46f + 12f * grow);
			var strike = new Rect2(at.X - wide, at.Y - up, wide * 2f, up + wide);
			float m = 8f * grow + 4f;
			Vector2 hands = ToLocal(beamFrom);
			fxQuad.Visible = beamOn || (since >= 0f && phase == BeamPhase.Afterglow && afterglow < 0.6f);
			fxQuad.Set(strike, new Rect2(hands.X - m, hands.Y - m, m * 2f, m * 2f));
		}
		// Along each split ray.
		for (int i = 0; i < PrismSplits; i++)
		{
			RayQuad? quad = prismQuads[i];
			if (quad == null)
				continue;
			quad.Visible = i < prisms;
			if (!quad.Visible)
				continue;
			Vector2 ray = prismEnds[i] - split;
			quad.Position = ToLocal(split);
			quad.Rotation = ray.Angle();
			quad.Set(new Rect2(-8f, -8f, ray.Length() + 16f, 16f));
		}
	}

	private void LeaveScorch(Vector2 at)
	{
		scorch?.Add(ToLocal(at), 1f + 0.5f * Heat + (flashRadius > 0f ? 0.8f : 0f));
	}

	// What the shader does not draw: the glint at the wizard's hands, the crown of molten droplets the
	// strike throws, and the sparks - square cells on the world's 2 px grid, in opaque Fire and ember tones,
	// over the beam (its quads are behind this node).
	public override void _Draw()
	{
		float grow = 1f + 0.5f * Heat;
		bool beamOn = BeamOn;

		// The glint at the wizard: four arms of light, square to the grid and then diagonal by turns - a
		// twinkle that turns, a pixel wide, the white-hot half nearest his hands.
		if (beamOn && muzzleAlpha > 0.05f)
		{
			float flick = Hash(Mathf.FloorToInt(shimmerTime / 0.035f), 3) > 0.5f ? 1f : 0.85f;
			bool diagonal = Mathf.FloorToInt(shimmerTime / 0.09f) % 2 == 1;
			int arm = Mathf.FloorToInt((11f + 5f * Heat) * flick * muzzleAlpha * (diagonal ? 0.7f : 1f) / Grid);
			Vector2 c = ToLocal(beamFrom);
			for (int k = 2; k <= arm; k++)
			{
				Color col = Gilded(k * 2 <= arm ? FireCore : k < arm ? EmberHi : EmberLit);
				float d = k * Grid;
				if (diagonal)
				{
					Stamp(c + new Vector2(d, d), Grid, col);
					Stamp(c + new Vector2(-d, d), Grid, col);
					Stamp(c + new Vector2(d, -d), Grid, col);
					Stamp(c + new Vector2(-d, -d), Grid, col);
				}
				else
				{
					Stamp(c + new Vector2(d, 0f), Grid, col);
					Stamp(c + new Vector2(-d, 0f), Grid, col);
					Stamp(c + new Vector2(0f, d), Grid, col);
					Stamp(c + new Vector2(0f, -d), Grid, col);
				}
			}
		}

		if (impactTime < 0f)
			return;
		Vector2 at = ToLocal(splashAt);
		float since = shimmerTime - impactTime;

		// The splash: droplets of molten light thrown out flat across the floor from where it struck,
		// hopping as they go and cooling down the ramp - the splash Eric asked for, where a stroked ring
		// at a fixed size used to be.
		if (since < DropletSeconds)
		{
			float u = since / DropletSeconds;
			float eased = 1f - (1f - u) * (1f - u);
			Color col = Gilded(u < 0.25f ? FireCore : u < 0.5f ? EmberHi : u < 0.75f ? EmberLit : EmberBase);
			for (int i = 0; i < Droplets; i++)
			{
				float h = Hash(i, 91), h2 = Hash(i, 17);
				float a = i * Mathf.Tau / Droplets + (h - 0.5f) * 0.6f;
				float reach = (16f + 10f * h2) * grow * eased;
				var pos = at + new Vector2(MathF.Cos(a) * reach, MathF.Sin(a) * reach * 0.42f - MathF.Sin(MathF.PI * u) * (5f + 5f * h) * grow);
				Stamp(pos, u < 0.35f ? Grid * 2f : Grid, col);
			}
		}

		// Sparks: a burst the instant it lands, then a stream while it burns - thrown up and out in arcs
		// that fall back under gravity, each on its own beat. The stream stops once the beam has gone; the
		// ones in the air finish their fall.
		for (int i = 0; i < Sparks; i++)
		{
			if (since < SparkLife * 1.2f)
				DrawSpark(at, since, i, -1, 1.35f * grow);
			float clock = since + i * (SparkLife / Sparks);
			int cycle = (int)(clock / SparkLife);
			float t = clock - cycle * SparkLife;
			if (t > since || (phase == BeamPhase.Afterglow && since - t > since - afterglow))
				continue;
			DrawSpark(at, t, i, cycle, grow);
		}
	}

	// One spark t seconds into its flight: a cell at its head and a short streak of cells behind it along
	// its flight, a stop cooler, shortening to the head alone as it dies. Its launch is a fixed
	// pseudo-random function of its index and cycle, so every flight differs and nothing allocates.
	private void DrawSpark(Vector2 at, float t, int i, int cycle, float strength)
	{
		float life = cycle < 0 ? SparkLife * 1.2f : SparkLife;
		if (t < 0f || t >= life)
			return;
		float h = MathF.Abs(MathF.Sin((i * 12.9898f + cycle * 78.233f) * 43758.5453f)) % 1f;
		float h2 = MathF.Abs(MathF.Sin((i * 93.989f + cycle * 17.31f) * 24634.634f)) % 1f;
		float angle = -Mathf.Pi * 0.5f + (h - 0.5f) * (cycle < 0 ? 3.0f : 2.2f);
		float speed = (90f + 110f * h2) * strength;
		Vector2 v0 = Vector2.FromAngle(angle) * speed;
		Vector2 pos = at + v0 * t + new Vector2(0f, 0.5f * SparkGravity * t * t);
		Vector2 vel = v0 + new Vector2(0f, SparkGravity * t);
		float age = t / life;
		Color head = Gilded(age < 0.2f ? FireCore : age < 0.45f ? EmberHi : age < 0.75f ? EmberLit : EmberBase);
		Color tail = Gilded(age < 0.2f ? EmberHi : age < 0.45f ? EmberLit : EmberBase);
		// The streak: where it was a fiftieth of a second ago, in cells, fewer as it cools.
		Vector2 back = -vel * 0.022f;
		int cells = age > 0.8f ? 0 : Math.Min(4, (int)(back.Length() / Grid));
		for (int k = cells; k >= 1; k--)
			Stamp(pos + back * (k / (float)Math.Max(1, cells)), Grid, tail);
		Stamp(pos, Grid, head);
	}

	private void Stamp(Vector2 centre, float size, Color colour) => DrawRect(WhipLimb.Cell(centre, size), colour);

	private Color Gilded(Color c) => new(c.R * gild.R, c.G * gild.G, c.B * gild.B, 1f);

	private static float Hash(int i, int salt)
	{
		uint h = (uint)i * 374761393u + (uint)salt * 668265263u;
		h = (h ^ (h >> 13)) * 1274126177u;
		h ^= h >> 16;
		return (h & 0xFFFFFF) / 16777216f;
	}

	/// <summary>
	/// One quad the ray's shader paints - only where to look; every fragment shades the whole ray. Turned and
	/// placed by the beam, redrawn only when its rectangle changes.
	/// </summary>
	private partial class RayQuad : Node2D
	{
		private Rect2 first;
		private Rect2 second;
		private int count;

		public void Set(Rect2 rect)
		{
			if (count == 1 && rect.IsEqualApprox(first))
				return;
			first = rect;
			count = 1;
			QueueRedraw();
		}

		public void Set(Rect2 rect, Rect2 other)
		{
			if (count == 2 && rect.IsEqualApprox(first) && other.IsEqualApprox(second))
				return;
			first = rect;
			second = other;
			count = 2;
			QueueRedraw();
		}

		public override void _Draw()
		{
			if (count > 0)
				DrawRect(first, Colors.White);
			if (count > 1)
				DrawRect(second, Colors.White);
		}
	}

	/// <summary>
	/// The burns the ray leaves on the floor: each a char with glowing cracks in it, which cool from white
	/// to dark red and then crumble away (ScorchGround, scenes/shaders/cinder_scorch.gdshader). A child of
	/// the beam on the ground layer, so it lies under the enemies while the splash is drawn over them. A ray
	/// that swings on or sweeps leaves one where each strike landed (up to MaxSpots).
	/// </summary>
	private partial class ScorchMark : Node2D
	{
		private const int MaxSpots = 4;
		private readonly Vector2[] spots = new Vector2[MaxSpots];
		private readonly float[] sizes = new float[MaxSpots];
		private int count;

		private float cool;
		/// <summary>0 just struck to 1 gone.</summary>
		public float Cool
		{
			get => cool;
			set
			{
				cool = value;
				QueueRedraw();
			}
		}

		public override void _Ready()
		{
			Material = ScorchGround.Material;
		}

		public void Add(Vector2 at, float size)
		{
			if (count == MaxSpots)
				return;
			spots[count] = at;
			sizes[count] = size;
			count++;
			cool = 0f;
			Visible = true;
			QueueRedraw();
		}

		public override void _Draw()
		{
			float left = 1f - Mathf.Clamp((cool - 0.55f) / 0.45f, 0f, 1f);
			float glow = 1f - Mathf.Clamp(cool / 0.6f, 0f, 1f);
			uint seed = (uint)(GetInstanceId() % 9973);
			for (int s = 0; s < count; s++)
				ScorchGround.Draw(this, spots[s], 17f * sizes[s], glow, left, seed + (uint)s * 131u);
		}
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 3) * DamageMultiplier));
		speed = MathF.Max(0.25f, SpellData?.GetSpeedMultiplier() ?? 1f);
		pierce = SpellData?.GetPierceAtLevel(CurrentLevel) ?? 0;
		range = SpellData?.GetRangeAtLevel(CurrentLevel) ?? 450f;
		// No burst at all until an evolution's Area grows one: the base ray strikes one enemy.
		float evolvedArea = SpellData?.GetAreaMultiplierAtLevel(CurrentLevel) ?? 1f;
		splashRadius = evolvedArea > 1.001f ? BaseSplashRadius * evolvedArea * AreaMultiplier : 0f;
		flashpoint = SpellData?.HasEvolution(FlashpointId) == true;
		handoff = SpellData?.HasEvolution(HandoffId) == true;
		burnThrough = SpellData?.HasEvolution(BurnThroughId) == true;
		prism = SpellData?.HasEvolution(PrismId) == true;
		sunlance = SpellData?.HasEvolution(SunlanceId) == true;
	}
}
