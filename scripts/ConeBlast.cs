using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts;

// Cone of Cold (Ice x2 double-weight, issue #28): a single-cast cone-shaped burst in the aim/
// nearest-enemy direction, dealing damage and slowing everything hit. New "cone" archetype - a
// manual angle+range sweep rather than an Area2D collision shape, since all existing active spells
// are homing-projectile, self-pulse, or orbit shaped.
public partial class ConeBlast : Node2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float BaseRange { get; set; } = 150f;
	[Export] public float ConeAngleDegrees { get; set; } = 60f;
	[Export] public bool GuaranteedSlow { get; set; } = true;
	[Export] public float SlowMultiplier { get; set; } = 0.5f;
	[Export] public float SlowDuration { get; set; } = 2.0f;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;

	private int damage = 5;
	private float range = 150f;
	private float scaledSlowMultiplier = 0.5f;
	private float scaledSlowDuration = 2.0f;

	// As long as the spray takes to fly out, land and cool (ImpactDebris).
	private const float WaveDuration = ImpactDebris.LifeSeconds;
	private static readonly ElementColors.Ramp IceRamp = ElementColors.GetRamp(Element.Ice);

	// The Long Winter (issue #61, ApotheosisCatalog): the cone opens into a full ring.
	private const float LongWinterFreezeSeconds = 1.6f;
	private float HalfAngleRad => SpellData?.IsApotheosis == true ? Mathf.Pi : Mathf.DegToRad(ConeAngleDegrees * 0.5f);
	private float waveTime = -1f;

	public override void _Ready()
	{
		RefreshComputedStats();
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	public void Fire(Vector2 origin, Vector2 direction)
	{
		GlobalPosition = origin;
		if (direction.LengthSquared() > 0.0001f)
			Rotation = direction.Angle();

		// The group, like every other AoE (CLAUDE.md). This walked CurrentScene's direct children,
		// which in a real run happens to be where enemies live and in the balance harness is not -
		// there the game is one node down, so Cone of Cold has dealt nothing in every bot run.
		var enemies = EnemyRegistry.Snapshot.OfType<Node2D>();
		var player = PlayerRef as Player;
		Vector2 dirNorm = direction.LengthSquared() > 0.0001f ? direction.Normalized() : Vector2.Right;
		float halfAngleRad = HalfAngleRad;
		// Deep Freeze (the level 4 the Long Winter requires) freezes for its own seconds; the Long
		// Winter never freezes for less than its 1.6 s. Shorter than the 1.8 s cooldown on purpose:
		// a 2 s freeze on a 1.8 s cone held everything in front of the Frostweaver still forever,
		// which is the Long Winter's job and not a level 4's.
		float freezeSeconds = SpellData?.GetFreezeSecondsAtLevel(CurrentLevel) ?? 0f;
		if (SpellData?.IsApotheosis == true)
			freezeSeconds = MathF.Max(freezeSeconds, LongWinterFreezeSeconds);
		float knockbackSpeed = SpellData?.GetKnockbackSpeedAtLevel(CurrentLevel) ?? 0f;

		foreach (var e in enemies)
		{
			var toEnemy = e.GlobalPosition - origin;
			float dist = toEnemy.Length();
			if (dist > range || dist <= 0.01f)
				continue;

			float angle = MathF.Abs(dirNorm.AngleTo(toEnemy.Normalized()));
			if (angle <= halfAngleRad)
			{
				player?.DealDamageToEnemy(e, damage, source: SpellData);
				// The Long Winter (issue #61) and Deep Freeze: a root, through the existing freeze
				// path. A boss's MinSlowMultiplier turns it into a hard slow on its own, which is the spec.
				if (freezeSeconds > 0f && e.HasMethod("ApplySlow"))
					e.Call("ApplySlow", 0.0f, freezeSeconds);
				else if (GuaranteedSlow && e.HasMethod("ApplySlow"))
					e.Call("ApplySlow", scaledSlowMultiplier, scaledSlowDuration);
				// Glacier Tide: the wall of ice shoves what it hits straight away from the caster.
				if (knockbackSpeed > 0f && e is Enemy shoved)
					shoved.ApplyKnockback(toEnemy / dist * knockbackSpeed);
			}
		}

		waveTime = 0f;
		QueueRedraw();

		var timer = GetTree().CreateTimer(WaveDuration);
		timer.Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
	}

	public override void _Process(double delta)
	{
		if (waveTime < 0f)
			return;
		waveTime += (float)delta;
		QueueRedraw();
	}

	// The cone is a spray of ice thrown out from the player across exactly the angle that is hit,
	// the furthest of it landing on exactly the range - so the spray's own reach is the cone's. It was
	// a flat translucent wedge with a bright arc on its leading edge: an area marker (2026-10-23).
	// The Long Winter's full turn is the same spray all the way round.
	public override void _Draw()
	{
		if (waveTime < 0f)
			return;

		float halfAngle = HalfAngleRad;
		float sweep = 2f * halfAngle;
		// Denser than a burst: a cone is a spray, and a sparse one reads as a few stray shards.
		int count = Mathf.Clamp((int)(range * sweep / 8f), 12, 56);
		ImpactDebris.Draw(this, Vector2.Zero, range, waveTime, (uint)(GetInstanceId() % 9973), IceRamp, count,
			Colors.White, -halfAngle, sweep);
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 5) * DamageMultiplier));
		range = MathF.Max(20f, (SpellData?.GetRangeAtLevel(CurrentLevel) ?? BaseRange) * AreaMultiplier);
		float slowPower = SpellData?.GetEffectValueAtLevel(SpellEffect.SlowPower, CurrentLevel) ?? 0f;
		float slowDurationBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.SlowDuration, CurrentLevel) ?? 0f;
		scaledSlowMultiplier = Mathf.Clamp(SlowMultiplier - slowPower, 0f, 0.98f);
		scaledSlowDuration = MathF.Max(0.1f, SlowDuration + slowDurationBonus);
	}
}
