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

		var visual = GetNodeOrNull<PlaceholderShape>("PlaceholderShape");
		if (visual != null)
		{
			visual.Radius = range;
			visual.QueueRedraw();
		}

		var parent = GetTree().CurrentScene;
		var enemies = parent?.GetChildren().OfType<Node2D>().Where(n => n.IsInGroup("enemies")) ?? Enumerable.Empty<Node2D>();
		var player = PlayerRef as Player;
		Vector2 dirNorm = direction.LengthSquared() > 0.0001f ? direction.Normalized() : Vector2.Right;
		float halfAngleRad = Mathf.DegToRad(ConeAngleDegrees * 0.5f);

		foreach (var e in enemies)
		{
			var toEnemy = e.GlobalPosition - origin;
			float dist = toEnemy.Length();
			if (dist > range || dist <= 0.01f)
				continue;

			float angle = MathF.Abs(dirNorm.AngleTo(toEnemy.Normalized()));
			if (angle <= halfAngleRad)
			{
				player?.DealDamageToEnemy(e, damage);
				if (GuaranteedSlow && e.HasMethod("ApplySlow"))
					e.Call("ApplySlow", SlowMultiplier, SlowDuration);
			}
		}

		var timer = GetTree().CreateTimer(0.3);
		timer.Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 5) * DamageMultiplier));
		range = MathF.Max(20f, (SpellData?.GetRangeAtLevel(CurrentLevel) ?? BaseRange) * AreaMultiplier);
	}
}
