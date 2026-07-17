using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts;

// Shared script for straightforward "elemental bolt" offensive spells (issue #13 roster expansion):
// Frost Shard, Shadow Bolt, Thorn Vine, Gale Blade, Molten Shard, Chain Lightning, Void Lance,
// Glacial Spike. Behaves like MagicMissile (homing bolt with optional pierce) plus optional
// guaranteed on-hit status effects and a single chain-jump to a second nearby enemy, configured
// per-scene via exported flags instead of one bespoke script per spell.
public partial class ElementalBolt : Area2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float BaseSpeed { get; set; } = 350f;
	[Export] public float BaseDuration { get; set; } = 5.0f;
	[Export] public int BasePierce { get; set; } = 0;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float DurationMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;

	// Guaranteed on-hit status effects, independent of the player's element-tier bonuses (#16).
	[Export] public bool GuaranteedSlow { get; set; } = false;
	[Export] public float SlowMultiplier { get; set; } = 0.5f;
	[Export] public float SlowDuration { get; set; } = 2.0f;
	[Export] public bool GuaranteedPoison { get; set; } = false;
	[Export] public int PoisonDamagePerTick { get; set; } = 2;
	[Export] public float PoisonDuration { get; set; } = 3.0f;
	[Export] public bool ChainToSecondTarget { get; set; } = false;
	[Export] public float ChainRadius { get; set; } = 150f;
	[Export] public float ChainDamageMultiplier { get; set; } = 0.6f;

	private int damage = 1;
	private float range = 500f;
	private float speed = 350f;
	private float duration = 5.0f;
	private int pierce = 0;
	private float areaRadius = 8f;

	private Vector2 direction = Vector2.Zero;
	private Node target = null;
	private float turnSpeed = 6.0f;
	private float lifetime = 0f;
	private int pierceCount = 0;
	private Vector2 spawnPosition = Vector2.Zero;

	public override void _Ready()
	{
		RefreshComputedStats();
		var cs = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (cs?.Shape is CircleShape2D shape)
			shape.Radius = areaRadius;
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
		spawnPosition = from;
		if (enemyTarget is Node2D enemyNode)
		{
			float distToEnemy = (enemyNode.GlobalPosition - from).Length();
			target = distToEnemy <= range ? enemyTarget : null;
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
			if (target is Node2D targetNode)
			{
				var toTarget = targetNode.GlobalPosition - GlobalPosition;
				if (toTarget.Length() > 0)
				{
					var tdir = toTarget.Normalized();
					var alpha = MathF.Min(1f, turnSpeed * (float)delta);
					direction = (direction * (1f - alpha) + tdir * alpha).Normalized();
					Rotation = direction.Angle();
				}
			}
		}

		if ((GlobalPosition - spawnPosition).Length() > range)
			target = null;

		Position += direction * speed * (float)delta;
		lifetime += (float)delta;
		if (duration > 0 && lifetime > duration) QueueFree();
	}

	private void OnAreaEntered(Area2D area) => HandleHit(area);
	private void OnBodyEntered(Node body) => HandleHit(body);

	private void HandleHit(Node enemy)
	{
		if (!enemy.IsInGroup("enemies") || !enemy.HasMethod("TakeDamage"))
			return;

		var player = PlayerRef as Player;
		float critBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.CritChance, CurrentLevel) ?? 0f;
		player?.DealDamageToEnemy(enemy, damage, critBonus);
		ApplyGuaranteedEffects(enemy);

		if (ChainToSecondTarget && player != null && enemy is Node2D hitNode2D)
		{
			var second = GetTree().GetNodesInGroup("enemies")
				.OfType<Node2D>()
				.Where(e => e != hitNode2D && IsInstanceValid(e) && hitNode2D.GlobalPosition.DistanceTo(e.GlobalPosition) <= ChainRadius)
				.OrderBy(e => hitNode2D.GlobalPosition.DistanceTo(e.GlobalPosition))
				.FirstOrDefault();
			if (second != null)
			{
				int chainDamage = Math.Max(1, Mathf.RoundToInt(damage * ChainDamageMultiplier));
				player.DealDamageToEnemy(second, chainDamage);
				ApplyGuaranteedEffects(second);
			}
		}

		pierceCount++;
		if (pierceCount > pierce) QueueFree();
	}

	private void ApplyGuaranteedEffects(Node enemy)
	{
		if (GuaranteedSlow && enemy.HasMethod("ApplySlow"))
			enemy.Call("ApplySlow", SlowMultiplier, SlowDuration);
		if (GuaranteedPoison && enemy.HasMethod("ApplyPoison"))
			enemy.Call("ApplyPoison", PoisonDamagePerTick, PoisonDuration);
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 1) * DamageMultiplier));
		range = SpellData?.GetRangeAtLevel(CurrentLevel) ?? 500f;
		speed = MathF.Max(1f, BaseSpeed + (SpellData?.GetEffectValueAtLevel(SpellEffect.ProjectileSpeed, CurrentLevel) ?? 0f));
		duration = MathF.Max(0f, BaseDuration * DurationMultiplier);
		pierce = Math.Max(0, BasePierce + (int)MathF.Round(SpellData?.GetEffectValueAtLevel(SpellEffect.Pierce, CurrentLevel) ?? 0f));
		areaRadius = MathF.Max(2f, (8f + (SpellData?.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel) ?? 0f)) * AreaMultiplier);
	}
}
