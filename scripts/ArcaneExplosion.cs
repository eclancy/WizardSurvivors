using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts
{
	public partial class ArcaneExplosion : Area2D
	{
		[Export] public SpellData SpellData { get; set; }
		[Export] public int CurrentLevel { get; set; } = 1;
		[Export] public float BaseKnockbackRange { get; set; } = 100f;
		[Export] public float BaseKnockbackSpeed { get; set; } = 2.0f;
		public float DamageMultiplier { get; set; } = 1.0f;
		public float AreaMultiplier { get; set; } = 1.0f;
		public float CooldownMultiplier { get; set; } = 1.0f;
		public float DurationMultiplier { get; set; } = 1.0f;

		private Weapon weapon;
		public Weapon Weapon
		{
			get => weapon;
			set
			{
				weapon = value;
				ApplyLegacyWeapon(value);
				UpdateScale();
			}
		}

		private int damage = 5;
		private float range = 100f;
		private float cooldown = 1.5f;
		private float knockbackRange = 100f;
		private float knockbackSpeed = 2.0f;

		public Node2D PlayerRef;
		[Export]
		public float TotalLifetime = 5.0f; // total time before freeing the node; 0 = infinite
		private float lifeElapsed = 0f;
		private float elapsed = 0f;
		private float duration = 0.6f;
		private float fireTimer = 0f;
		// cooldown tracking
		private ParticleProcessMaterial particleMaterial;
		private GpuParticles2D particles;
		private AnimatedSprite2D animatedSprite;
		private CollisionShape2D collisionShape;

		public override void _Ready()
		{
			GD.Print("ArcaneExplosion ready");
			RefreshComputedStats();
			// We'll trigger explosions on a cooldown timer regardless of enemy proximity
			particles = GetNode<GpuParticles2D>("Particles");
			animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
			collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");

			if (animatedSprite != null)
			{
				animatedSprite.Visible = false;
				animatedSprite.AnimationFinished += OnAnimationFinished;
			}

			if (particles != null)
			{
				particles.Emitting = false;
				particleMaterial = particles.ProcessMaterial as ParticleProcessMaterial;
				if (particleMaterial != null)
					particleMaterial.Set("emission_ring_radius", 0f);
			}

			// Apply initial scaling if weapon is already set
			UpdateScale();

			// Trigger immediately on spawn, then start cooldown timer
			TriggerExplosion();
			fireTimer = 0f;
		}

		private void UpdateScale()
		{
			if (animatedSprite == null)
				return;

			// The sprite is 64x64 pixels. Scale it to match the weapon's range.
			// Assuming the base sprite at scale 1.0 represents a range of ~32 pixels (half the sprite size)
			float baseRange = 32.0f;
			float scaleFactor = range / baseRange;

			animatedSprite.Scale = new Vector2(scaleFactor, scaleFactor);

			// Also scale the collision shape to match
			if (collisionShape != null && collisionShape.Shape is CircleShape2D circle)
			{
				// The original circle radius is ~23, scale it proportionally
				circle.Radius = 23.0f * scaleFactor;
			}
		}

		public override void _Process(double delta)
		{
			// follow player if assigned
			if (PlayerRef != null)
			{
				GlobalPosition = PlayerRef.GlobalPosition;
			}

			// update overall lifetime and free when exceeded
			if (TotalLifetime > 0f)
			{
				lifeElapsed += (float)delta;
				if (lifeElapsed >= TotalLifetime)
				{
					QueueFree();
					return;
				}
			}
			// handle cooldown-based automatic explosion triggering
			fireTimer += (float)delta;
			if (fireTimer >= cooldown)
			{
				TriggerExplosion();
				fireTimer = 0f;
			}

			if (particleMaterial == null)
				return;

			if (elapsed < duration)
			{
				elapsed += (float)delta;
				float t = Mathf.Clamp(elapsed / duration, 0f, 1f);
				particleMaterial.Set("emission_ring_radius", Mathf.Lerp(0f, range, t));
			}
			// visual expansion handled by elapsed/duration
		}

		private void TriggerExplosion()
		{
			GD.Print("ArcaneExplosion: Triggering timed explosion");
			// Play the animation once
			if (animatedSprite != null)
			{
				animatedSprite.Visible = true;
				animatedSprite.Play();
			}

			// Apply to all enemies within range
			var parent = GetTree().CurrentScene;
			var enemies = parent.GetChildren()
			.OfType<Node2D>()
			.Where(n => n.IsInGroup("enemies"));
			foreach (var e in enemies)
			{
				float dist = GlobalPosition.DistanceTo(e.GlobalPosition);
				if (dist <= range)
				{
					float knockback = knockbackRange * (1f - (dist / (2f * range)));
					if (knockback < knockbackRange * 0.5f)
						knockback = knockbackRange * 0.5f;

					bool isVortex = SpellData != null && SpellData.HasEffectFlag(SpellEffect.VortexPull);
					Vector2 dir = isVortex
						? (GlobalPosition - e.GlobalPosition).Normalized()
						: (e.GlobalPosition - GlobalPosition).Normalized();

					if (e.HasMethod("ApplyKnockback"))
						e.Call("ApplyKnockback", dir * knockback * knockbackSpeed);
					if (e.HasMethod("TakeDamage"))
					{
						(PlayerRef as Player)?.DealDamageToEnemy(e, damage, source: SpellData);
					}
				}
			}

			// Trigger particles and visual expansion
			if (particles != null)
			{
				particles.Emitting = true;
			}
			elapsed = 0f;
		}

		private void OnAnimationFinished()
		{
			if (animatedSprite != null)
				animatedSprite.Visible = false;
		}

		public void SetSpellLevel(int level)
		{
			CurrentLevel = Math.Max(1, level);
			RefreshComputedStats();
			UpdateScale();
		}

		private void RefreshComputedStats()
		{
			if (weapon != null)
			{
				ApplyLegacyWeapon(weapon);
				return;
			}

			damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 5) * DamageMultiplier));
			range = (SpellData?.GetRangeAtLevel(CurrentLevel) ?? 100f) * AreaMultiplier * (SpellData?.GetAreaMultiplierAtLevel(CurrentLevel) ?? 1f);
			cooldown = MathF.Max(0.05f, (SpellData?.GetCooldownAtLevel(CurrentLevel) ?? 1.5f) * CooldownMultiplier);
			knockbackRange = MathF.Max(0f, BaseKnockbackRange + (SpellData?.GetEffectValueAtLevel(SpellEffect.Knockback, CurrentLevel) ?? 0f));
			knockbackSpeed = MathF.Max(0f, BaseKnockbackSpeed);
			duration = MathF.Max(0.1f, 0.6f * DurationMultiplier);
		}

		private void ApplyLegacyWeapon(Weapon value)
		{
			if (value == null) return;

			damage = value.Damage;
			range = value.Range;
			cooldown = MathF.Max(0.05f, value.Cooldown);
			knockbackRange = value.KnockbackRange;
			knockbackSpeed = value.KnockbackSpeed;
		}
	}
}
