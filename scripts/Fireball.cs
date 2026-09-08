using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts;

// Fireball (Fire x2 double-weight, issue #13): lobs a slow, high-damage projectile that explodes
// in an area on impact or at max range/duration.
public partial class Fireball : Area2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float BaseSpeed { get; set; } = 180f;
	[Export] public float BaseDuration { get; set; } = 4.0f;
	[Export] public float BaseExplosionRadius { get; set; } = 95.0f;
	[Export] public float BaseVisualScale { get; set; } = 1.0f;
	[Export] public float VisualScalePerLevel { get; set; } = 0.08f;
	[Export] public float VisualScalePerAreaBonus { get; set; } = 0.01f;
	[Export] public float BaseCollisionRadius { get; set; } = 6.0f;
	[Export] public float CollisionRadiusPerLevel { get; set; } = 0.4f;
	[Export] public float CollisionRadiusPerAreaBonus { get; set; } = 0.06f;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float DurationMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;

	private int damage = 6;
	private float range = 400f;
	private float speed = 180f;
	private float duration = 4.0f;
	private float explosionRadius = 40f;

	private Vector2 direction = Vector2.Zero;
	private float lifetime = 0f;
	private Vector2 spawnPosition = Vector2.Zero;
	private bool exploded = false;

	public override void _Ready()
	{
		RefreshComputedStats();
		Connect("area_entered", new Callable(this, nameof(OnAreaEntered)));
		Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	public void Shoot(Vector2 from, Vector2 to)
	{
		GlobalPosition = from;
		direction = (to - from).Normalized();
		Rotation = direction.Angle();
		spawnPosition = from;
		lifetime = 0f;
		exploded = false;
	}

	public override void _Process(double delta)
	{
		if (exploded)
			return;

		Position += direction * speed * (float)delta;
		lifetime += (float)delta;
		if ((duration > 0 && lifetime > duration) || (GlobalPosition - spawnPosition).Length() > range)
		{
			Explode();
		}
	}

	private void OnAreaEntered(Area2D area)
	{
		if (!exploded && area.IsInGroup("enemies") && area.HasMethod("TakeDamage"))
			Explode();
	}

	private void OnBodyEntered(Node body)
	{
		if (!exploded && body.IsInGroup("enemies") && body.HasMethod("TakeDamage"))
			Explode();
	}

	private void Explode()
	{
		if (exploded)
			return;
		exploded = true;

		foreach (var node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is Node2D enemy2D && IsInstanceValid(enemy2D) && GlobalPosition.DistanceTo(enemy2D.GlobalPosition) <= explosionRadius)
			{
				if (PlayerRef is Player playerObj && IsInstanceValid(playerObj))
				{
					playerObj.DealDamageToEnemy(enemy2D, damage, source: SpellData);
				}
				else if (enemy2D.HasMethod("TakeDamage"))
				{
					enemy2D.Call("TakeDamage", damage);
				}
			}
		}

		var visual = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (visual != null)
			visual.Visible = false;

		var trail = GetNodeOrNull<GpuParticles2D>("TrailParticles");
		if (trail != null)
			trail.Emitting = false;

		var burst = GetNodeOrNull<GpuParticles2D>("ExplosionParticles");
		if (burst != null)
		{
			burst.Emitting = true;
			burst.Restart();
		}

		var cs = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (cs != null)
			cs.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);

		var timer = GetTree().CreateTimer(0.4);
		timer.Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
	}

	private void RefreshComputedStats()
	{
		float areaSizeBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel) ?? 0f;
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 6) * DamageMultiplier));
		range = SpellData?.GetRangeAtLevel(CurrentLevel) ?? 400f;
		speed = MathF.Max(1f, BaseSpeed);
		duration = MathF.Max(0f, BaseDuration * DurationMultiplier);
		explosionRadius = MathF.Max(4f, (BaseExplosionRadius + areaSizeBonus) * AreaMultiplier);

		var visual = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (visual != null)
		{
			float levelScale = BaseVisualScale + (Math.Max(0, CurrentLevel - 1) * VisualScalePerLevel);
			float finalScale = MathF.Max(0.1f, levelScale + (areaSizeBonus * VisualScalePerAreaBonus));
			visual.Scale = new Vector2(finalScale, finalScale);
			visual.Play("default");
		}

		var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.Shape as CircleShape2D;
		if (shape != null)
		{
			shape.Radius = MathF.Max(2f,
				BaseCollisionRadius
				+ (Math.Max(0, CurrentLevel - 1) * CollisionRadiusPerLevel)
				+ (areaSizeBonus * CollisionRadiusPerAreaBonus));
		}
	}
}
