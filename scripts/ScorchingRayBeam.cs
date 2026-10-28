using Godot;
using System;
using System.Linq;

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
public partial class ScorchingRayBeam : Node2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float ExtendDuration { get; set; } = 0.11f;
	[Export] public float RetractDuration { get; set; } = 0.10f;
	/// <summary>How long the beam stays on at full length after it lands: the look of a laser, not its damage.</summary>
	[Export] public float HoldDuration { get; set; } = 0.16f;

	// A THIN FIERY RED LASER THAT SPLASHES INTO THE GROUND (2026-10-28). Eric: "scorching ray looks like
	// a big purple tube. instead make it a thin fiery red laser splashing into the ground." It was a
	// 10-13 px orange bar with square ends, and ApplyLegendaryVisual multiplied it by the spell's
	// evolution/element tint, which turned it violet. Now: a white-hot core three pixels wide inside a
	// narrow flickering red glow, never tinted (Fire resets the Modulate), ending at the target's feet in
	// a splash - a scorched glow on the ground and embers thrown out of it - drawn in _Draw. It holds at
	// full length for HoldDuration so the splash is seen; the damage still lands the frame it arrives.
	private const float FootDrop = 16f;
	private static readonly Color CoreCold = new(1f, 0.78f, 0.45f);
	private static readonly Color CoreHot = new(1f, 0.97f, 0.86f);
	private static readonly Color GlowRed = new(1f, 0.16f, 0.06f);
	private static readonly Color Ember = new(1f, 0.62f, 0.16f);
	private const int Embers = 8;
	private Vector2 splashAt;
	private float splashAlpha;
	[Export] public float HitFallbackRadius { get; set; } = 26f;

	// The burst an evolution's Area grows, before that Area: at x1.3 (Amplified Impact) 39 px, at x2
	// (Titan Cataclysm) 60 px, at both 78 px - times the player's area.
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
		Retracting
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
	private bool burnedThrough = false;
	private Node2D? targetNode;
	private bool didDamage = false;

	// One cast at a time on the main thread, so the scan for what stands behind the target shares one
	// buffer rather than allocating per ray.
	private static readonly Node2D[] pierceBuffer = new Node2D[MaxPierce];
	private static readonly float[] pierceAlong = new float[MaxPierce];
	private Line2D? beamLine;
	private Line2D? heatLine;
	private Sprite2D? impactSprite;
	private float shimmerTime = 0f;

	public override void _Ready()
	{
		beamLine = GetNodeOrNull<Line2D>("BeamLine");
		heatLine = GetNodeOrNull<Line2D>("HeatLine");
		impactSprite = GetNodeOrNull<Sprite2D>("ImpactSprite");
		// The splash is drawn by this node, which sits at the arena's origin in a y-sorted arena: at
		// z 0 it went under the very enemy it was splashing at. Over the actors, under the lines.
		ZIndex = 5;
		RefreshComputedStats();
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
		// Its own colours, always: the spell's evolution and element tints (Player.ApplyLegendaryVisual)
		// multiplied the fire into a violet tube. A Legendary keeps a touch of its gold.
		Modulate = SpellData?.IsLegendary == true ? new Color(1f, 0.95f, 0.78f) : Colors.White;
		if (impactSprite != null)
			impactSprite.Visible = false;
		UpdateVisual(origin, origin, 0f);
	}

	public override void _Process(double delta)
	{
		if (phase == BeamPhase.Idle)
			return;

		shimmerTime += (float)delta;

		Vector2 targetPos = GetTargetPosition();

		if (phase == BeamPhase.Extending)
		{
			phaseTimer += (float)delta;
			float t = Mathf.Clamp(phaseTimer / Mathf.Max(0.01f, ExtendDuration / speed), 0f, 1f);
			Vector2 beamEnd = origin.Lerp(targetPos, t);
			UpdateVisual(origin, beamEnd, t);

			if (t >= 1f)
			{
				ApplyDamageAtImpact(targetPos);
				phase = HoldDuration > 0f ? BeamPhase.Holding : BeamPhase.Retracting;
				phaseTimer = 0f;
			}
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
				QueueFree();
		}
	}

	private Vector2 GetTargetPosition()
	{
		if (targetNode != null && IsInstanceValid(targetNode))
			lockedTargetPosition = targetNode.GlobalPosition;
		return lockedTargetPosition;
	}

	private void ApplyDamageAtImpact(Vector2 impactPosition)
	{
		if (didDamage)
			return;

		didDamage = true;
		throughPosition = impactPosition;
		if (PlayerRef == null)
			return;

		// The source rides every hit now, as for every other spell: it is what credits the ray in the
		// per-spell damage table, picks its impact sound and lets a per-spell crit damage apply.
		float critBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.CritChance, CurrentLevel) ?? 0f;
		Node2D? struck = null;
		if (targetNode != null && IsInstanceValid(targetNode) && targetNode.IsInGroup("enemies") && targetNode.HasMethod("TakeDamage"))
		{
			struck = targetNode;
		}
		else
		{
			struck = EnemyRegistry.Snapshot
				.OfType<Node2D>()
				.Where(e => IsInstanceValid(e) && e.HasMethod("TakeDamage") && impactPosition.DistanceTo(e.GlobalPosition) <= HitFallbackRadius)
				.OrderBy(e => impactPosition.DistanceTo(e.GlobalPosition))
				.FirstOrDefault();
		}
		if (struck == null)
			return;

		PlayerRef.DealDamageToEnemy(struck, damage, critBonus, source: SpellData);
		BurnThrough(impactPosition, struck, critBonus);
		Burst(impactPosition, struck, critBonus);
	}

	// Pierce: the enemies standing behind the target on the beam's line, nearest first, out to the
	// spell's range from the caster - each takes the ray's full damage, as a piercing bolt's victims do.
	private void BurnThrough(Vector2 impact, Node2D struck, float critBonus)
	{
		int want = Math.Min(pierce, MaxPierce);
		Vector2 line = impact - origin;
		float reach = line.Length();
		if (want <= 0 || reach < 1f || PlayerRef == null)
			return;
		Vector2 dir = line / reach;
		Vector2 side = new Vector2(-dir.Y, dir.X);

		int found = 0;
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Node2D enemy || enemy == struck || !IsInstanceValid(enemy) || !enemy.HasMethod("TakeDamage"))
				continue;
			Vector2 rel = enemy.GlobalPosition - impact;
			float along = rel.Dot(dir);
			if (along <= 0f || reach + along > range || MathF.Abs(rel.Dot(side)) > PierceHalfWidth)
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
		if (found > 0)
		{
			burnedThrough = true;
			throughPosition = impact + dir * pierceAlong[found - 1];
		}
	}

	// Area: a burst where the ray lands, for a share of its damage, drawn to exactly its radius.
	private void Burst(Vector2 impact, Node2D struck, float critBonus)
	{
		if (splashRadius <= 0f || PlayerRef == null)
			return;
		int splashDamage = Math.Max(1, Mathf.RoundToInt(damage * SplashDamageFraction));
		float r2 = splashRadius * splashRadius;
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Node2D enemy || enemy == struck || !IsInstanceValid(enemy) || !enemy.HasMethod("TakeDamage"))
				continue;
			if (impact.DistanceSquaredTo(enemy.GlobalPosition) <= r2)
				PlayerRef.DealDamageToEnemy(enemy, splashDamage, critBonus, source: SpellData);
		}
		if (GetParent() is Node2D arena)
			ImpactDebrisLayer.Spawn(arena, impact, splashRadius, ElementColors.GetRamp(Element.Fire), Colors.White);
	}

	private void UpdateVisual(Vector2 from, Vector2 to, float alphaScale)
	{
		float clampedAlpha = Mathf.Clamp(alphaScale, 0f, 1f);
		// Into the ground at the target's feet, not its middle: the splash is on the floor.
		Vector2 ground = to + new Vector2(0f, FootDrop * clampedAlpha);
		Vector2 beamDir = (ground - from).Normalized();
		Vector2 normal = new Vector2(-beamDir.Y, beamDir.X);
		float flicker = 0.82f + 0.36f * MathF.Abs(MathF.Sin(shimmerTime * 57f));
		Vector2 wobbleOffset = normal * Mathf.Sin(shimmerTime * 42f) * 0.8f;

		if (beamLine != null)
		{
			beamLine.SetPointPosition(0, ToLocal(from));
			beamLine.SetPointPosition(1, ToLocal(ground));
			beamLine.Width = (2.5f + 1.5f * Heat) * Mathf.Lerp(0.6f, 1f, clampedAlpha);
			Color core = CoreCold.Lerp(CoreHot, Heat);
			beamLine.Modulate = new Color(core, 0.98f * clampedAlpha);
		}

		if (heatLine != null)
		{
			heatLine.SetPointPosition(0, ToLocal(from + wobbleOffset));
			heatLine.SetPointPosition(1, ToLocal(ground + wobbleOffset));
			heatLine.Width = (7f + 3f * Heat) * flicker * Mathf.Lerp(0.6f, 1f, clampedAlpha);
			heatLine.Modulate = new Color(GlowRed, (0.5f + 0.2f * Heat) * clampedAlpha);
		}

		splashAt = ground;
		splashAlpha = (phase == BeamPhase.Extending) ? 0f : clampedAlpha;
		QueueRedraw();
	}

	// The splash where the laser meets the ground: a scorched glow squashed flat onto the floor, a hot
	// point in it, and embers thrown up and out, turning with the shimmer so it boils.
	public override void _Draw()
	{
		if (splashAlpha <= 0.01f)
			return;
		Vector2 at = ToLocal(splashAt);
		float a = splashAlpha;
		float grow = 1f + 0.5f * Heat;
		DrawSetTransform(at, 0f, new Vector2(1f, 0.42f));
		DrawCircle(Vector2.Zero, 20f * grow, new Color(GlowRed, 0.4f * a));
		DrawCircle(Vector2.Zero, 11f * grow, new Color(Ember, 0.62f * a));
		DrawCircle(Vector2.Zero, 4f * grow, new Color(CoreHot, 0.9f * a));
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
		for (int i = 0; i < Embers; i++)
		{
			// Up and out: a half fan above the floor, each ember on its own beat.
			float phaseOf = (shimmerTime * 3.2f + i * 0.37f) % 1f;
			float angle = Mathf.Pi + Mathf.Pi * (i + 0.5f) / Embers + 0.25f * MathF.Sin(i * 2.1f + shimmerTime * 9f);
			Vector2 dir = Vector2.FromAngle(angle);
			float reach = (8f + 16f * phaseOf) * grow;
			Vector2 p0 = at + dir * reach * 0.55f;
			Vector2 p1 = at + dir * reach;
			Color c = Ember.Lerp(GlowRed, phaseOf);
			DrawLine(p0, p1, new Color(c, (1f - phaseOf) * a), 1.6f);
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
	}
}
