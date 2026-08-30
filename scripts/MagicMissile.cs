using Godot;
using System;


namespace WizardSurvivors.scripts;

public partial class MagicMissile : Area2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float BaseSpeed { get; set; } = 400f;
	[Export] public float BaseDuration { get; set; } = 5.0f;
	[Export] public float BaseArea { get; set; } = 16.0f;
	[Export] public int BasePierce { get; set; } = 0;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float DurationMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;

	private Weapon weapon;
	public Weapon Weapon
	{
		get => weapon;
		set
		{
			weapon = value;
			ApplyLegacyWeapon(value);
		}
	}

	private int damage = 1;
	private float range = 500f;
	private float speed = 400f;
	private float duration = 5.0f;
	private int pierce = 0;
	private float areaRadius = 16.0f;

	private Vector2 direction = Vector2.Zero;
	private Node target = null;
	private float turnSpeed = 6.0f;
	private float lifetime = 0f;
	private int pierceCount = 0;
	private Vector2 spawnPosition = Vector2.Zero;

	public override void _Ready()
	{
		RefreshComputedStats();

		var sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (sprite != null) sprite.Play("default");
		var cs = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (cs != null) cs.Disabled = false;
		var shape = cs?.Shape as CircleShape2D;
		if (shape != null) shape.Radius = areaRadius;
		Connect("area_entered", new Callable(this, nameof(OnAreaEntered)));
		Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	public void Shoot(Vector2 from, Vector2 to, Node enemyTarget = null)
	{
		GlobalPosition = from;
		direction = (to - from).Normalized();
		Rotation = direction.Angle();
		// Only lock onto a target if it is within effective range from the firing position.
		spawnPosition = from;
		if (enemyTarget is Node2D enemyNode)
		{
			float distToEnemy = (enemyNode.GlobalPosition - from).Length();
			if (distToEnemy <= range)
				target = enemyTarget;
			else
				target = null; // out of range, don't home
		}
		else
		{
			target = null;
		}
		lifetime = 0f;
		pierceCount = 0;
	}

	public override void _Process(double delta)
	{
		if (target != null && IsInstanceValid(target))
		{
			if (target is not Node2D targetNode) return;
			var toTarget = targetNode.GlobalPosition - GlobalPosition;
			if (toTarget.Length() > 0)
			{
				var tdir = toTarget.Normalized();
				var alpha = MathF.Min(1f, turnSpeed * (float)delta);
				direction = (direction * (1f - alpha) + tdir * alpha).Normalized();
				Rotation = direction.Angle();
			}
		}

		// If missile has travelled beyond its range from spawn, drop any target lock.
		if ((GlobalPosition - spawnPosition).Length() > range)
		{
			target = null;
		}

		Position += direction * speed * (float)delta;
		lifetime += (float)delta;
		var particles = GetNodeOrNull<GpuParticles2D>("GPUParticles2D");
		if (particles != null)
		{
			particles.Rotation = Rotation;
		}
		if (duration > 0 && lifetime > duration) QueueFree();
	}

	private void OnAreaEntered(Area2D area)
	{
		if (area.IsInGroup("enemies") && area.HasMethod("TakeDamage"))
		{
			(PlayerRef as Player)?.DealDamageToEnemy(area, damage);
			TriggerOnHitEffects(area);
			pierceCount++;
			if (pierceCount > pierce) QueueFree();
		}
	}

	private void OnBodyEntered(Node body)
	{
		if (body.IsInGroup("enemies") && body.HasMethod("TakeDamage"))
		{
			(PlayerRef as Player)?.DealDamageToEnemy(body, damage);
			TriggerOnHitEffects(body);
			pierceCount++;
			if (pierceCount > pierce) QueueFree();
		}
	}

	private void TriggerOnHitEffects(Node hitTarget)
	{
		if (SpellData == null)
			return;

		if (SpellData.HasEffectFlag(SpellEffect.ExplosionOnHit))
		{
			float splashRadius = MathF.Max(24f, areaRadius * 2.0f);
			var enemies = GetTree().GetNodesInGroup("enemies");
			foreach (var e in enemies)
			{
				if (e is Node2D n2d && n2d != hitTarget && GlobalPosition.DistanceTo(n2d.GlobalPosition) <= splashRadius)
				{
					(PlayerRef as Player)?.DealDamageToEnemy(n2d, Math.Max(1, damage / 2));
				}
			}
		}
	}

	private void RefreshComputedStats()
	{
		if (weapon != null)
		{
			ApplyLegacyWeapon(weapon);
			return;
		}

		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 1) * DamageMultiplier));
		range = SpellData?.GetRangeAtLevel(CurrentLevel) ?? 500f;
		speed = MathF.Max(1f, BaseSpeed + (SpellData?.GetEffectValueAtLevel(SpellEffect.ProjectileSpeed, CurrentLevel) ?? 0f));
		duration = MathF.Max(0f, BaseDuration * DurationMultiplier);
		pierce = Math.Max(0, BasePierce + (SpellData != null ? SpellData.GetPierceAtLevel(CurrentLevel) : 0));
		areaRadius = MathF.Max(2f, (BaseArea + (SpellData?.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel) ?? 0f)) * AreaMultiplier * (SpellData?.GetAreaMultiplierAtLevel(CurrentLevel) ?? 1f));
	}

	private void ApplyLegacyWeapon(Weapon value)
	{
		if (value == null) return;

		damage = value.Damage;
		range = value.Range;
		speed = value.Speed;
		duration = value.Duration;
		pierce = Math.Max(0, value.Pierce);
		areaRadius = MathF.Max(2f, value.Area);

		var cs = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (cs?.Shape is CircleShape2D circle)
		{
			circle.Radius = areaRadius;
		}
	}
}
