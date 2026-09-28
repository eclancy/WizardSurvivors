using Godot;
using System.Linq;
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

	// The sheet is authored at the 48px elite cell and saved at x2, so a frame is 96px on disk.
	private const int BladeCellPixels = 96;
	private const int BladeFrames = 8;

	// Ghost trail. One afterimage per blade per interval, which at 0.05s and two blades is forty
	// short-lived sprites a second - cheap, and the whole reason the blade reads as spinning
	// rather than sliding. Tuned by interval rather than by count so more blades do not mean a
	// denser trail behind each one.
	private const float TrailInterval = 0.05f;
	private const float TrailFadeSeconds = 0.34f;
	private float trailTimer = 0f;

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

		// Ours now, drawn by tools/art/spirit_blade.py. The old sheet was pack art on the 32px
		// projectile cell; this is the 48px elite cell rendered at x2, which is how a thing gets
		// bigger in this project - art-direction.md section 1 allows exactly one render scale.
		var texture = GD.Load<Texture2D>("res://assets/bonelight/effects/spirit-blade.png");
		// No AddAnimation here: a new SpriteFrames already HAS a "default" animation, and adding
		// it again logs "SpriteFrames already has animation 'default'" once per weapon spawned -
		// which in a fifteen-minute run is hundreds of lines of console noise hiding real errors.
		orbitSpriteFrames.SetAnimationSpeed("default", 12); // 12 FPS

		for (int f = 0; f < BladeFrames; f++)
		{
			var atlas = new AtlasTexture();
			atlas.Atlas = texture;
			atlas.Region = new Rect2(f * BladeCellPixels, 0, BladeCellPixels, BladeCellPixels);
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

	// Drops a fading copy of each blade where it currently is.
	//
	// Parented to this node rather than to the blade, so a ghost stays where it was shed instead
	// of orbiting along with its parent - a trail that travels with the thing making it is not a
	// trail, it is a smear that never moves relative to the blade.
	private void ShedGhosts()
	{
		if (orbitingObjects == null || orbitSpriteFrames == null)
			return;

		foreach (Node2D blade in orbitingObjects)
		{
			if (blade == null || !IsInstanceValid(blade))
				continue;

			var sprite = blade.GetChildren().OfType<AnimatedSprite2D>().FirstOrDefault();
			if (sprite == null)
				continue;

			var ghost = new Sprite2D
			{
				Texture = orbitSpriteFrames.GetFrameTexture("default", sprite.Frame),
				Position = blade.Position,
				Rotation = blade.Rotation,
				Scale = blade.Scale * 0.92f,
				// Behind the live blades, so the trail never sits on top of the thing casting it.
				ZIndex = -1,
				Modulate = new Color(0.72f, 0.58f, 1.0f, 0.42f),
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest
			};
			AddChild(ghost);

			Tween fade = ghost.CreateTween();
			fade.SetParallel(true);
			fade.TweenProperty(ghost, "modulate:a", 0.0f, TrailFadeSeconds);
			fade.TweenProperty(ghost, "scale", ghost.Scale * 0.72f, TrailFadeSeconds);
			fade.SetParallel(false);
			fade.TweenCallback(Callable.From(() => ghost.QueueFree()));
		}
	}

	// Collision handlers
	private void OnAreaEntered(Area2D area)
	{
		if (area.IsInGroup("enemies"))
		{
			GD.Print("SpiritualWeapon hit enemy (area)");
			(PlayerRef as Player)?.DealDamageToEnemy(area, damage, source: SpellData);
		}
	}

	private void OnBodyEntered(Node body)
	{
		if (body.IsInGroup("enemies"))
		{
			GD.Print("SpiritualWeapon hit enemy (body)");
			(PlayerRef as Player)?.DealDamageToEnemy(body, damage, source: SpellData);
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

			// Turned to the tangent so the blade sweeps along its own path. Without this it keeps
			// a fixed heading while its position goes round, which reads as a picture being
			// dragged in a circle rather than as a weapon being swung.
			orbitingObjects[i].Rotation = angle + Mathf.Pi * 0.5f;
		}

		trailTimer += (float)delta;
		if (trailTimer >= TrailInterval)
		{
			trailTimer = 0f;
			ShedGhosts();
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
