using Godot;
using System;

namespace WizardSurvivors.scripts;

public partial class SpiritualWeapon : Node2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float BaseOrbitSpeed { get; set; } = 1.0f;
	[Export] public float BaseArea { get; set; } = 12.0f;
	[Export] public float BaseActiveDuration { get; set; } = 0.8f;
	[Export] public float BurstSpinMultiplier { get; set; } = 2.2f;
	[Export] public float EndSpinRampMultiplier { get; set; } = 1.5f;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float AttackSpeedMultiplier { get; set; } = 1.0f;
	public float DurationMultiplier { get; set; } = 1.0f;
	public int ProjectileCountBonus { get; set; } = 0;

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
	public Node2D PlayerRef;

	private Node2D[] orbitingObjects;
	private float orbitAngle = 0f;
	private SpriteFrames orbitSpriteFrames;

	private int damage = 8;
	private float range = 100f;
	private float orbitSpeed = 1.0f;
	private int projectileCount = 2;
	private float areaRadius = 12f;
	private bool visualsReady = false;
	private float totalLifetime = 0.8f;
	private float elapsedLifetime = 0f;

	public override void _Ready()
	{
		GD.Print("SpiritualWeapon ready");
		RefreshComputedStats();
		CreateSharedFrames();
		visualsReady = true;
		EnsureOrbitingObjects();
		elapsedLifetime = 0f;
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
		if (!visualsReady)
			return;

		EnsureOrbitingObjects();
	}

	private void CreateSharedFrames()
	{
		orbitSpriteFrames = new SpriteFrames();

		var texture = GD.Load<Texture2D>("res://assets/spiritual_weapon.png");
		orbitSpriteFrames.AddAnimation("default");
		orbitSpriteFrames.SetAnimationSpeed("default", 12); // 12 FPS

		for (int f = 0; f < 8; f++)
		{
			var atlas = new AtlasTexture();
			atlas.Atlas = texture;
			atlas.Region = new Rect2(f * 32, 0, 32, 32);
			orbitSpriteFrames.AddFrame("default", atlas);
		}
	}

	private void EnsureOrbitingObjects()
	{
		if (!visualsReady)
			return;

		if (projectileCount < 1)
			projectileCount = 1;

		if (orbitingObjects != null && orbitingObjects.Length == projectileCount)
		{
			// Keep collision visuals in sync even when projectile count does not change.
			foreach (Node2D obj in orbitingObjects)
			{
				if (obj is Area2D area)
				{
					var shape = area.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
					if (shape?.Shape is CircleShape2D circle)
					{
						circle.Radius = areaRadius;
					}
				}
			}
			return;
		}

		if (orbitingObjects != null)
		{
			foreach (var obj in orbitingObjects)
			{
				if (obj != null && IsInstanceValid(obj))
				{
					obj.QueueFree();
				}
			}
		}

		orbitingObjects = new Node2D[projectileCount];
		for (int i = 0; i < projectileCount; i++)
		{
			var area = new Area2D();
			area.CollisionMask = 2;
			var animSprite = new AnimatedSprite2D();
			animSprite.SpriteFrames = orbitSpriteFrames;
			animSprite.Animation = "default";
			animSprite.Play();
			area.AddChild(animSprite);

			var shape = new CollisionShape2D();
			var circle = new CircleShape2D();
			circle.Radius = areaRadius;
			shape.Shape = circle;
			area.AddChild(shape);
			area.Monitoring = true;
			area.Monitorable = true;
			area.Connect("area_entered", new Callable(this, nameof(OnAreaEntered)));
			area.Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
			AddChild(area);
			orbitingObjects[i] = area;
		}
	}

	// Collision handlers
	private void OnAreaEntered(Area2D area)
	{
		if (area.IsInGroup("enemies"))
		{
			GD.Print("SpiritualWeapon hit enemy (area)");
			area.Call("TakeDamage", damage);
		}
	}

	private void OnBodyEntered(Node body)
	{
		if (body.IsInGroup("enemies"))
		{
			GD.Print("SpiritualWeapon hit enemy (body)");
			body.Call("TakeDamage", damage);
		}
	}

	public override void _Process(double delta)
	{
		// Always center on player (since this is a child of Player)
		Position = Vector2.Zero;
		if (orbitingObjects == null) return;

		elapsedLifetime += (float)delta;
		if (elapsedLifetime >= totalLifetime)
		{
			QueueFree();
			return;
		}

		// Orbit logic
		float lifeRatio = totalLifetime > 0f ? Mathf.Clamp(elapsedLifetime / totalLifetime, 0f, 1f) : 1f;
		float burstSpeed = orbitSpeed * BurstSpinMultiplier * Mathf.Lerp(1.0f, EndSpinRampMultiplier, lifeRatio);
		orbitAngle += burstSpeed * (float)delta;
		float angleStep = 2f * Mathf.Pi / projectileCount;
		for (int i = 0; i < projectileCount; i++)
		{
			float angle = orbitAngle + i * angleStep;
			Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * range;
			orbitingObjects[i].Position = offset;
		}
	}

	private void RefreshComputedStats()
	{
		if (weapon != null)
		{
			ApplyLegacyWeapon(weapon);
			return;
		}

		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 8) * DamageMultiplier));
		range = (SpellData?.GetRangeAtLevel(CurrentLevel) ?? 100f) * AreaMultiplier;
		projectileCount = Math.Max(1, (SpellData?.GetProjectileCountAtLevel(CurrentLevel) ?? 2) + ProjectileCountBonus);
		orbitSpeed = MathF.Max(0.1f, (BaseOrbitSpeed + (SpellData?.GetEffectValueAtLevel(SpellEffect.ProjectileSpeed, CurrentLevel) ?? 0f)) * AttackSpeedMultiplier);
		areaRadius = MathF.Max(2f, (BaseArea + (SpellData?.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel) ?? 0f)) * AreaMultiplier);
		totalLifetime = MathF.Max(0.15f, BaseActiveDuration * MathF.Max(0.1f, DurationMultiplier));
	}

	private void ApplyLegacyWeapon(Weapon value)
	{
		if (value == null) return;

		damage = value.Damage;
		range = value.Range;
		orbitSpeed = MathF.Max(0.1f, value.AttackSpeed);
		projectileCount = Math.Max(1, value.NumberOfProjectiles);
		areaRadius = MathF.Max(2f, BaseArea);
	}
}
