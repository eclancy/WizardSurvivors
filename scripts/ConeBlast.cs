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

	private const float WaveDuration = 0.35f;

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
		var enemies = GetTree().GetNodesInGroup("enemies").OfType<Node2D>();
		var player = PlayerRef as Player;
		Vector2 dirNorm = direction.LengthSquared() > 0.0001f ? direction.Normalized() : Vector2.Right;
		float halfAngleRad = HalfAngleRad;
		bool longWinter = SpellData?.IsApotheosis == true;

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
				// The Long Winter (issue #61): a root, through the existing freeze path. A boss's
				// MinSlowMultiplier turns it into a hard slow on its own, which is the spec.
				if (longWinter && e.HasMethod("ApplySlow"))
					e.Call("ApplySlow", 0.0f, LongWinterFreezeSeconds);
				else if (GuaranteedSlow && e.HasMethod("ApplySlow"))
					e.Call("ApplySlow", scaledSlowMultiplier, scaledSlowDuration);
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

	// Draws the cone as a light-blue wave that erupts from the player and grows outward to the
	// spell's range, spanning exactly the angle that enemies must be within to be hit.
	public override void _Draw()
	{
		if (waveTime < 0f)
			return;

		float t = Mathf.Clamp(waveTime / WaveDuration, 0f, 1f);
		float grow = 1f - Mathf.Pow(1f - t, 2f); // ease-out so the wave rushes outward then settles
		float currentRadius = range * grow;
		float fade = 1f - t;
		float halfAngle = HalfAngleRad;

		int segments = 28;
		var points = new Vector2[segments + 2];
		points[0] = Vector2.Zero;
		for (int i = 0; i <= segments; i++)
		{
			float a = -halfAngle + (2f * halfAngle) * (i / (float)segments);
			points[i + 1] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * currentRadius;
		}

		var fill = new Color(0.55f, 0.85f, 1.0f, 0.45f * fade); // light blue cone body
		DrawColoredPolygon(points, fill);

		var edge = new Color(0.78f, 0.95f, 1.0f, 0.85f * fade); // brighter leading wave front
		DrawArc(Vector2.Zero, currentRadius, -halfAngle, halfAngle, segments, edge, 4f, true);
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
