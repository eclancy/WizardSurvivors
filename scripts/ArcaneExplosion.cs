using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts
{
	public partial class ArcaneExplosion : Area2D
	{
		[Export] public SpellData SpellData { get; set; }
		[Export] public int CurrentLevel { get; set; } = 1;
		// Charge-driven, not timed. Player counts casts of the OTHER five spells and detonates
		// this when the count comes due - see Player.NotifyArcaneCharge. Left as an export so the
		// old behaviour is one checkbox away if the charge version does not survive playtesting.
		[Export] public bool ChargeDriven { get; set; } = true;

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
			// The blast is in the air, over the bodies it catches. A child of the player at z 0, it was
			// sorted at his feet, under every body south of him (DrawLayers). Its rift is on the ground layer.
			ZAsRelative = false;
			ZIndex = DrawLayers.Air;
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
			// Timed firing only when the charge path is switched off. A spell that both ticks on
			// a clock AND detonates on charge would fire twice as often as either rule describes.
			if (!ChargeDriven)
			{
				fireTimer += (float)delta;
				if (fireTimer >= cooldown)
				{
					TriggerExplosion();
					fireTimer = 0f;
				}
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

		/// <summary>Detonate now. Called by Player when the charge comes due.</summary>
		public void TriggerNow()
		{
			TriggerExplosion();
			fireTimer = 0f;
		}

		private void TriggerExplosion()
		{
			// Play the animation once
			if (animatedSprite != null)
			{
				animatedSprite.Visible = true;
				animatedSprite.Play();
			}

			// Apply to all enemies within range.
			//
			// The group, not GetTree().CurrentScene.GetChildren(). The old form only saw enemies
			// that were DIRECT children of the scene root, so anything parented under a container
			// - a spawner, a formation node, a test harness - was silently immune. CLAUDE.md is
			// explicit that AoE iterates the group for exactly this reason.
			// Event Horizon's freeze (Effect Freeze) is a singularity, so it draws the blast inward as
			// well as holding what it catches: the card promised both, and until 2026-10-26 did neither.
			float freezeSeconds = SpellData?.GetFreezeSecondsAtLevel(CurrentLevel) ?? 0f;
			bool isVortex = freezeSeconds > 0f || (SpellData != null && SpellData.HasEffectFlag(SpellEffect.VortexPull));
			var enemies = EnemyRegistry.Snapshot.OfType<Node2D>();
			foreach (var e in enemies)
			{
				float dist = GlobalPosition.DistanceTo(e.GlobalPosition);
				if (dist <= range)
				{
					float knockback = knockbackRange * (1f - (dist / (2f * range)));
					if (knockback < knockbackRange * 0.5f)
						knockback = knockbackRange * 0.5f;

					Vector2 dir = isVortex
						? (GlobalPosition - e.GlobalPosition).Normalized()
						: (e.GlobalPosition - GlobalPosition).Normalized();

					if (e.HasMethod("ApplyKnockback"))
						e.Call("ApplyKnockback", dir * knockback * knockbackSpeed);
					if (e.HasMethod("TakeDamage"))
					{
						(PlayerRef as Player)?.DealDamageToEnemy(e, damage, source: SpellData);
					}
					// Frozen after the pull is set, so the pull still lands them nearer the middle:
					// a knockback carries on through a freeze (Enemy, the shock/knockback branch).
					if (freezeSeconds > 0f && e is Enemy held)
						held.ApplySlow(0f, freezeSeconds);
				}
			}

			OpenLingeringRift();

			// Trigger particles and visual expansion
			if (particles != null)
			{
				particles.Emitting = true;
			}
			elapsed = 0f;
		}

		// Lingering Rift (Effect ZoneDuration = seconds): the ground the blast went off on keeps hurting
		// for a while after. It promised "a pulsating hazard zone" and ArcaneExplosion left none until
		// 2026-10-26. The rift is a LingeringZone, Mirefoot's pool, in arcane colour: it stays where the
		// blast was while the player walks on, which is what makes it a zone.
		private const float RiftTickShare = 0.25f;
		private const float RiftRadiusShare = 0.8f;

		private void OpenLingeringRift()
		{
			float seconds = SpellData?.GetEffectValueAtLevel(SpellEffect.ZoneDuration, CurrentLevel) ?? 0f;
			if (seconds <= 0f || PlayerRef is not Player player)
				return;
			Node parent = player.GetParent();
			if (parent == null)
				return;

			ElementColors.Ramp ramp = ElementColors.GetRamp(Element.Arcane);
			var rift = new LingeringZone
			{
				Name = "LingeringRift",
				Radius = range * RiftRadiusShare,
				Duration = seconds * DurationMultiplier,
				TickSeconds = 0.5f,
				AppliesSlow = false,
				DamagePerTick = Math.Max(1, Mathf.RoundToInt(damage * RiftTickShare)),
				FillColor = ramp.Edge with { A = 0.34f },
				EdgeColor = ramp.Hot with { A = 0.6f },
				SpellData = SpellData,
				PlayerRef = player,
			};
			parent.AddChild(rift);
			rift.GlobalPosition = GlobalPosition;
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
			// With the level table's Area line (Effect AreaSize, a fraction), read since 2026-10-26.
			range = (SpellData?.GetRangeAtLevel(CurrentLevel) ?? 100f) * AreaMultiplier * (SpellData?.GetAreaMultiplierAtLevel(CurrentLevel) ?? 1f)
				* (1f + (SpellData?.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel) ?? 0f));
			cooldown = MathF.Max(0.05f, (SpellData?.GetCooldownAtLevel(CurrentLevel) ?? 1.5f) * CooldownMultiplier);
			// Effect Knockback from the level table and KnockbackBonus from an evolution (Dual
			// Shockwave) are the same units; the second was read by nothing until 2026-10-26.
			knockbackRange = MathF.Max(0f, BaseKnockbackRange + (SpellData?.GetEffectValueAtLevel(SpellEffect.Knockback, CurrentLevel) ?? 0f)
				+ (SpellData?.GetKnockbackBonusAtLevel(CurrentLevel) ?? 0f));
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
