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
	// How far a Twin Volley shard flies before it expires. A shard is aimed at open ground
	// rather than at an enemy, so nothing else would ever stop one - without this a fork in an
	// empty corner sends two projectiles across the whole arena. Kept short so the fork reads as
	// a burst at the impact point.
	[Export] public float SplitTravelDistance { get; set; } = 260f;
	// Distance this projectile may travel from its spawn before expiring. 0 = unlimited, which is
	// every missile the player fires; only Twin Volley shards set it.
	public float MaxTravelDistance { get; set; } = 0f;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float DurationMultiplier { get; set; } = 1.0f;
	// 0 for a missile the player fired, 1 for a Twin Volley shard. Shards never fork again -
	// without this a dense pack would cascade until the frame budget died.
	public int SplitGeneration { get; set; } = 0;
	// The enemy a Twin Volley shard is forbidden to damage: the one its parent just hit. A
	// shard is born overlapping that body, so without this it would collide on its first frame
	// and spend itself on the original target instead of ever leaving the impact point.
	public Node SplitIgnoreTarget { get; set; } = null;
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
	private bool hasSplit = false;

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
		hasSplit = false;
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
		float travelled = (GlobalPosition - spawnPosition).Length();
		if (travelled > range)
		{
			target = null;
		}

		if (MaxTravelDistance > 0f && travelled > MaxTravelDistance)
		{
			QueueFree();
			return;
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
		if (area == SplitIgnoreTarget)
			return;
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
		if (body == SplitIgnoreTarget)
			return;
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

		TrySplitOnHit(hitTarget);

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

	// Twin Volley (magic_missile_twin_volley). The first enemy the missile touches makes it
	// fork: two shards fly straight out of the impact point at right angles to the missile's
	// heading, one to each side, for a fraction of the damage. It fires once per missile even
	// with pierce, because the upgrade is a fork on the initial hit rather than a rider on
	// every hit.
	//
	// The shards deliberately do not home and do not pick targets. Homing shards made the fork
	// a second seeking volley - it could not miss, so it was worth taking regardless of how the
	// player positioned, and in a pack it looked like three missiles converging on the same
	// knot. A fixed 90-degree spray is a positional upgrade instead: it pays when the missile
	// hits the near edge of a line of enemies, and does nothing in the open.
	private void TrySplitOnHit(Node hitTarget)
	{
		if (hasSplit || SplitGeneration > 0)
			return;
		if (!SpellData.HasEffectFlag(SpellEffect.SplitOnHit))
			return;

		// EffectValue is a percentage of this missile's damage, set by the evolution that
		// granted the fork. The fallback mirrors Twin Volley's tuned value so a shard is never
		// a no-op if some future caller leaves it unset.
		float percent = SpellData.GetEffectValueAtLevel(SpellEffect.SplitOnHit, CurrentLevel);
		if (percent <= 0f)
			percent = 30f;

		// A shard is another copy of this scene, so the missile reloads its own PackedScene
		// rather than the player having to hand one down. If that ever fails, say so - a
		// silently missing fork reads as a balance problem instead of a wiring one.
		PackedScene scene = string.IsNullOrEmpty(SceneFilePath)
			? null
			: ResourceLoader.Load<PackedScene>(SceneFilePath);
		if (scene == null)
		{
			GD.PushWarning($"[MagicMissile] Twin Volley could not reload its own scene (SceneFilePath='{SceneFilePath}'); no shards were fired.");
			return;
		}

		// Mark before spawning: a shard added to the tree can collide on the same frame, and
		// re-entering here would fork twice.
		hasSplit = true;

		// Square to the missile's heading at the moment of impact. direction is normalised and
		// never zero for a missile in flight, but a fork fired from a standstill would give two
		// shards with no heading at all, so fall back to the sprite's facing.
		Vector2 heading = direction.LengthSquared() > 0f ? direction : Vector2.Right.Rotated(Rotation);
		SpawnSplitShard(scene, heading.Rotated(MathF.PI / 2f), percent, hitTarget);
		SpawnSplitShard(scene, heading.Rotated(-MathF.PI / 2f), percent, hitTarget);
	}

	private void SpawnSplitShard(PackedScene scene, Vector2 shardDirection, float percent, Node hitTarget)
	{
		var shard = scene.Instantiate<Area2D>();
		// Nudge the shard clear of the body its parent just struck so the fork reads as two
		// shards peeling away rather than three impacts stacked on one enemy.
		Vector2 spawnOffset = shardDirection * 18f;
		shard.Position = GlobalPosition + spawnOffset;
		// Carry the evolution tint across by hand: ApplyLegendaryVisual only runs on the
		// missiles the player fires, so a shard would otherwise render untinted.
		shard.Modulate = Modulate;

		if (shard is MagicMissile shardScript)
		{
			shardScript.SpellData = SpellData;
			shardScript.DamageMultiplier = DamageMultiplier * (percent / 100f);
			shardScript.AreaMultiplier = AreaMultiplier;
			shardScript.DurationMultiplier = DurationMultiplier;
			shardScript.MaxTravelDistance = SplitTravelDistance;
			shardScript.SplitGeneration = SplitGeneration + 1;
			shardScript.SplitIgnoreTarget = hitTarget;
			shardScript.PlayerRef = PlayerRef;
			shardScript.SetSpellLevel(CurrentLevel);
		}

		GetParent().AddChild(shard);

		// Null target: Shoot only locks on when handed an enemy, so passing none is what makes
		// the shard fly straight instead of curving into whatever is nearest.
		if (shard is MagicMissile readyShard)
		{
			Vector2 from = GlobalPosition + spawnOffset;
			readyShard.Shoot(from, from + shardDirection, null);
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
