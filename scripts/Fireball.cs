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

	// PIXEL ART SINCE 2026-10-29 ("does not look up to our typical visual fidelity"). The ball was a 32 px
	// sprite saved at x2 and shown at 1.35 to 2, rotated to the heading - art pixels 2.7 to 4 world px wide
	// on a tilted grid - trailing GPU particles one world px wide in a translucent orange, and its blast
	// was 24 more of them over a radius of 165: the reach the damage tested was drawn by nothing. Now the
	// ball is one quad on this node through scenes/shaders/fireball.gdshader, snapped to the world's 2 px
	// grid at any heading and scale, its white-hot heart exactly the collision circle; the blast is a burst
	// of fire on FireburstLayer drawn out to exactly explosionRadius, with ImpactDebris thrown to the same
	// radius; and Pyroclasm's pool is magma (BlastPoolSurface) rather than a translucent disc. The
	// evolutions are painted, not tinted - a multiply had turned Hellfire Nova's heart flat red.
	private const string ShaderPath = "res://scenes/shaders/fireball.gdshader";
	private static Shader shader;
	private static readonly StringName ParamCore = "core_radius";
	private static readonly StringName ParamBody = "body";
	private static readonly StringName ParamTime = "time";
	private static readonly StringName ParamSeed = "seed";
	private static readonly StringName ParamIron = "iron";
	private static readonly StringName ParamMagma = "magma";
	private static readonly StringName ParamCluster = "cluster";
	private static readonly StringName ParamHell = "hell";
	private static readonly StringName ParamSun = "sun";
	private static readonly StringName ParamGilded = "gilded";
	public const string PyroclasmId = "fireball_pyroclasm";
	public const string ClusterId = "fireball_cluster";
	public const string IronCometId = "fireball_comet";
	public const string HellfireNovaId = "fireball_hellfire_nova";
	public const string ArmageddonSunId = "fireball_armageddon_sun";
	// The old sprite's flame radius at a visual scale of 1: its outer disc, 8 art px of a 32 px cell at x2.
	private const float BodyPerVisualScale = 16f;
	private ShaderMaterial material;
	private float visualRadius = 21.6f;
	private float collisionRadius = 6f;
	private float drawTime;

	public override void _Ready()
	{
		// In the air, and so is its blast: over the bodies, not sorted in with them (DrawLayers). The
		// ground it leaves burning is a LingeringZone on the ground layer.
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		shader ??= GD.Load<Shader>(ShaderPath);
		material = new ShaderMaterial { Shader = shader };
		Material = material;
		RefreshComputedStats();
		ApplyLook();
		Connect("area_entered", new Callable(this, nameof(OnAreaEntered)));
		Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
	}

	// Which evolution's ball this is, painted by the shader (see its header). Read once: a fireball in
	// flight does not change its evolutions.
	private void ApplyLook()
	{
		if (material == null)
			return;
		material.SetShaderParameter(ParamSeed, (GetInstanceId() % 997) * 0.731f);
		material.SetShaderParameter(ParamIron, SpellData?.HasEvolution(IronCometId) ?? false);
		material.SetShaderParameter(ParamMagma, SpellData?.HasEvolution(PyroclasmId) ?? false);
		material.SetShaderParameter(ParamCluster, SpellData?.HasEvolution(ClusterId) ?? false);
		material.SetShaderParameter(ParamHell, SpellData?.HasEvolution(HellfireNovaId) ?? false);
		material.SetShaderParameter(ParamSun, SpellData?.HasEvolution(ArmageddonSunId) ?? false);
		material.SetShaderParameter(ParamGilded, SpellData?.IsLegendary ?? false);
		material.SetShaderParameter(ParamCore, collisionRadius);
		material.SetShaderParameter(ParamBody, visualRadius);
	}

	/// <summary>How the blast is painted: the Legendary's gold, Armageddon Sun's light, Hellfire Nova's red rows.</summary>
	private FireburstLayer.Style BlastStyle =>
		SpellData?.IsLegendary == true ? FireburstLayer.Style.Gold
		: SpellData?.HasEvolution(ArmageddonSunId) == true ? FireburstLayer.Style.Sun
		: SpellData?.HasEvolution(HellfireNovaId) == true ? FireburstLayer.Style.Hellfire
		: FireburstLayer.Style.Fire;

	// One quad over the ball, the flame streaming behind it and the sparks off it, in node units (the
	// shader's frame is this node's, rotated to the heading). The shader decides every pixel.
	public override void _Draw()
	{
		if (exploded)
			return;
		float r = visualRadius;
		float tail = r * 2.6f;
		float back = r * 0.6f + (tail + r) * 0.9f + 4f;
		DrawRect(new Rect2(-back, -r * 1.5f, back + r * 1.1f, r * 3f), Colors.White);
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

		drawTime += (float)delta;
		material?.SetShaderParameter(ParamTime, drawTime);
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

		foreach (var node in EnemyRegistry.Snapshot)
		{
			if (node is Node2D enemy2D && IsInstanceValid(enemy2D) && GlobalPosition.DistanceTo(enemy2D.GlobalPosition) <= explosionRadius)
			{
				if (PlayerRef is Player playerObj && IsInstanceValid(playerObj))
				{
					playerObj.DealDamageToEnemy(enemy2D, damage, source: SpellData);
				}
				else if (enemy2D.HasMethod("TakeDamage"))
				{
					// Two arguments - see the note in KidDog.TickBite.
					enemy2D.Call("TakeDamage", damage, false);
				}
			}
		}

		// The ball is gone; the blast is drawn to exactly the radius just tested, fire and the debris it
		// throws, on the arena's shared layers, since this node frees itself before they are done.
		QueueRedraw();
		if (GetParent() is Node2D blastArena)
		{
			FireburstLayer.Spawn(blastArena, GlobalPosition, explosionRadius, BlastStyle);
			ImpactDebrisLayer.Spawn(blastArena, GlobalPosition, explosionRadius, ElementColors.GetRamp(Element.Fire), Colors.White);
		}

		var cs = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (cs != null)
			cs.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);

		LeaveMagmaPool();
		ScatterClusterBombs();

		var timer = GetTree().CreateTimer(0.4);
		timer.Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
	}

	// Pyroclasm (Effect Burn = seconds): the blast leaves a pool of magma that burns what stands in
	// it. Meteor Swarm's Magma Craters is the same promise, and both pay a quarter of the hit every
	// half second. The pool is a LingeringZone in fire's colours, inside the blast it came from. It
	// promised this from the start and nothing read the Burn until 2026-10-26.
	private const float MagmaTickShare = 0.25f;
	private const float MagmaRadiusShare = 0.6f;

	private void LeaveMagmaPool()
	{
		float seconds = SpellData?.GetEffectValueAtLevel(SpellEffect.Burn, CurrentLevel) ?? 0f;
		if (seconds <= 0f || PlayerRef is not Player player || GetParent() is not Node parent)
			return;
		var pool = new LingeringZone
		{
			Name = "MagmaPool",
			Radius = explosionRadius * MagmaRadiusShare,
			Duration = seconds * DurationMultiplier,
			TickSeconds = 0.5f,
			AppliesSlow = false,
			DamagePerTick = Math.Max(1, Mathf.RoundToInt(damage * MagmaTickShare)),
			SpellData = SpellData,
			PlayerRef = player,
		};
		// Melt with a crust floating on it, out to the radius it burns - not a translucent disc
		// (scenes/shaders/magma_pool.gdshader).
		BlastPoolSurface.Attach(pool, BlastPoolSurface.Kind.Magma);
		parent.CallDeferred(Node.MethodName.AddChild, pool);
		// Its parent is the fireball's, so the fireball's own position is where it lands.
		pool.Position = Position;
	}

	// Cluster Bombs: every projectile past the first is a bomblet thrown out of the blast, landing
	// just past its edge a moment later for half the damage in half the radius. Fireball always flew
	// one ball and never split, so Cluster's "+2 projectiles" reached nothing until 2026-10-26.
	private const float ClusterDelaySeconds = 0.14f;
	private const float ClusterDamageShare = 0.5f;
	private const float ClusterRadiusShare = 0.45f;

	private void ScatterClusterBombs()
	{
		int bomblets = (SpellData?.GetProjectileCountAtLevel(CurrentLevel) ?? 1) - 1;
		if (bomblets <= 0 || PlayerRef is not Player player || GetParent() is not Node2D arena)
			return;
		Vector2 centre = GlobalPosition;
		float spin = GD.Randf() * Mathf.Tau;
		float bombRadius = explosionRadius * ClusterRadiusShare;
		int bombDamage = Math.Max(1, Mathf.RoundToInt(damage * ClusterDamageShare));
		SpellData source = SpellData;
		FireburstLayer.Style style = BlastStyle;
		var timer = GetTree().CreateTimer(ClusterDelaySeconds, processAlways: false, processInPhysics: true);
		timer.Timeout += () =>
		{
			if (!IsInstanceValid(player) || !IsInstanceValid(arena))
				return;
			for (int i = 0; i < bomblets; i++)
			{
				float a = spin + Mathf.Tau * i / bomblets;
				Vector2 at = centre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * explosionRadius * 0.9f;
				foreach (var node in EnemyRegistry.Snapshot)
				{
					if (node is Node2D enemy && IsInstanceValid(enemy) && at.DistanceTo(enemy.GlobalPosition) <= bombRadius)
						player.DealDamageToEnemy(enemy, bombDamage, source: source);
				}
				// The bomblet's own burst of fire, to exactly the radius it hit, with its debris.
				FireburstLayer.Spawn(arena, at, bombRadius, style);
				ImpactDebrisLayer.Spawn(arena, at, bombRadius, ElementColors.GetRamp(Element.Fire), Colors.White);
			}
		};
	}

	private void RefreshComputedStats()
	{
		float areaSizeBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel) ?? 0f;
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 6) * DamageMultiplier));
		range = SpellData?.GetRangeAtLevel(CurrentLevel) ?? 400f;
		// The evolutions' speed (Armageddon Sun's slow drift) and Area (Hellfire Nova, Armageddon Sun)
		// as well as the player's. Both cards promised them and neither reached the blast until
		// 2026-10-26; their ScaleMultiplier had been growing the drawing over an unchanged hit.
		speed = MathF.Max(1f, BaseSpeed * (SpellData?.GetSpeedMultiplier() ?? 1f));
		duration = MathF.Max(0f, BaseDuration * DurationMultiplier);
		explosionRadius = MathF.Max(4f, (BaseExplosionRadius + areaSizeBonus) * AreaMultiplier * (SpellData?.GetAreaMultiplierAtLevel(CurrentLevel) ?? 1f));

		// The flame's size, from the numbers that scaled the old sprite, so the ball reads as large as it did.
		float levelScale = BaseVisualScale + (Math.Max(0, CurrentLevel - 1) * VisualScalePerLevel);
		float finalScale = MathF.Max(0.1f, levelScale + (areaSizeBonus * VisualScalePerAreaBonus));
		visualRadius = BodyPerVisualScale * finalScale;

		collisionRadius = MathF.Max(2f,
			BaseCollisionRadius
			+ (Math.Max(0, CurrentLevel - 1) * CollisionRadiusPerLevel)
			+ (areaSizeBonus * CollisionRadiusPerAreaBonus));
		var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.Shape as CircleShape2D;
		if (shape != null)
			shape.Radius = collisionRadius;
		if (material != null)
		{
			material.SetShaderParameter(ParamCore, collisionRadius);
			material.SetShaderParameter(ParamBody, visualRadius);
		}
		QueueRedraw();
	}
}
