using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

// Cyclone Slash (Wind x2, issue #13): a spinning blade of wind that repeatedly hits everything
// nearby. Self-contained orbiting-blade pattern (placeholder art, code-created blades) so it
// doesn't depend on SpiritualWeapon.cs's hardcoded texture.
public partial class OrbitingBlade : Node2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float BaseOrbitSpeed { get; set; } = 2.5f;
	[Export] public float BaseOrbitRadius { get; set; } = 60f;
	[Export] public float HitCooldown { get; set; } = 0.4f;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float AttackSpeedMultiplier { get; set; } = 1.0f;
	public int ProjectileCountBonus { get; set; } = 0;
	public Node2D PlayerRef;

	private int damage = 4;
	private float orbitSpeed = 2.5f;
	private float orbitRadius = 60f;
	private int bladeCount = 2;
	private float orbitAngle = 0f;
	private Area2D[] blades;
	private readonly Dictionary<Node, float> hitCooldowns = new();

	private const float BaseBladeRadius = 10f;
	private float bladeRadius = BaseBladeRadius;

	// Vortex Reaper (Effect VortexPull = pull speed): every PullSeconds, what is near the ring but
	// outside it is dragged toward it, through Enemy.ApplyKnockback as Hollow Star's pull is, so a
	// boss's KnockbackResistance holds it. Nothing read the pull until 2026-10-26.
	private const float PullSeconds = 0.4f;
	private const float PullReach = 2.2f;
	private float vortexPull;
	private float pullTimer;

	public override void _Ready()
	{
		RefreshComputedStats();
		EnsureBlades();
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
		EnsureBlades();
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 4) * DamageMultiplier));
		// The evolutions' speed and Area as well as the player's (Dual Windblades, Tailwind Surge,
		// Typhoon Core). It read only the player's, so all three cards' numbers were unread until
		// 2026-10-26. Area widens the orbit and each blade with it, so a bigger hurricane is not a
		// wider ring with the same two pebbles going round it.
		float evolutionArea = SpellData?.GetAreaMultiplierAtLevel(CurrentLevel) ?? 1f;
		orbitSpeed = MathF.Max(0.1f, BaseOrbitSpeed * AttackSpeedMultiplier * (SpellData?.GetSpeedMultiplier() ?? 1f));
		// With the Area tag's level line (Effect AreaSize, a fraction) on the orbit too.
		orbitRadius = MathF.Max(10f, BaseOrbitRadius * AreaMultiplier * evolutionArea
			* (1f + (SpellData?.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel) ?? 0f)));
		bladeRadius = BaseBladeRadius * MathF.Sqrt(MathF.Max(1f, evolutionArea));
		vortexPull = SpellData?.GetEffectValueAtLevel(SpellEffect.VortexPull, CurrentLevel) ?? 0f;
		bladeCount = Math.Max(1, (SpellData?.GetProjectileCountAtLevel(CurrentLevel) ?? 2) + ProjectileCountBonus);
	}

	private void EnsureBlades()
	{
		if (blades != null)
		{
			foreach (var b in blades)
				b?.QueueFree();
		}

		blades = new Area2D[bladeCount];
		for (int i = 0; i < bladeCount; i++)
		{
			var area = new Area2D
			{
				CollisionMask = 2,
				Monitoring = true,
				Monitorable = true
			};
			var cs = new CollisionShape2D { Shape = new CircleShape2D { Radius = bladeRadius } };
			area.AddChild(cs);
			var visual = new PlaceholderShape { ShapeColor = new Color(0.75f, 0.97f, 1.0f, 0.9f), Radius = bladeRadius };
			area.AddChild(visual);
			area.Connect("area_entered", new Callable(this, nameof(OnHit)));
			area.Connect("body_entered", new Callable(this, nameof(OnHit)));
			AddChild(area);
			blades[i] = area;
		}
	}

	public override void _Process(double delta)
	{
		Position = Vector2.Zero;
		if (blades != null)
		{
			orbitAngle += orbitSpeed * (float)delta;
			for (int i = 0; i < blades.Length; i++)
			{
				if (blades[i] == null) continue;
				float angle = orbitAngle + (Mathf.Tau / blades.Length) * i;
				blades[i].Position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * orbitRadius;
			}
		}

		if (vortexPull > 0f)
		{
			pullTimer -= (float)delta;
			if (pullTimer <= 0f)
			{
				pullTimer = PullSeconds;
				PullTowardRing();
			}
		}

		if (hitCooldowns.Count > 0)
		{
			foreach (var key in hitCooldowns.Keys.ToList())
			{
				hitCooldowns[key] -= (float)delta;
				if (hitCooldowns[key] <= 0f)
					hitCooldowns.Remove(key);
			}
		}
	}

	private void PullTowardRing()
	{
		Vector2 centre = GlobalPosition;
		float reach = orbitRadius * PullReach;
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy enemy || !IsInstanceValid(enemy))
				continue;
			Vector2 toCentre = centre - enemy.GlobalPosition;
			float distance = toCentre.Length();
			// Only from outside the ring: the blades are on it, and a pull past it would drag the
			// crowd onto the caster.
			if (distance > reach || distance <= orbitRadius)
				continue;
			enemy.ApplyKnockback(toCentre / distance * MathF.Min(vortexPull, (distance - orbitRadius) * 3f));
		}
	}

	private void OnHit(Node other)
	{
		if (other == null || !other.IsInGroup("enemies") || !other.HasMethod("TakeDamage"))
			return;
		if (hitCooldowns.ContainsKey(other))
			return;

		hitCooldowns[other] = HitCooldown;
		// source matters: without it this damage is unattributable, and the balance report shows
		// the spell doing nothing rather than showing what it did.
		(PlayerRef as Player)?.DealDamageToEnemy(other, damage, source: SpellData);
	}
}
