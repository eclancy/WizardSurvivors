using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts
{
	public partial class ArcaneExplosion : Area2D
	{
		// These values get overwritten by the Weapon data when instantiated
		private Weapon weapon;
		public Weapon Weapon
		{
			get => weapon;
			set
			{
				weapon = value;
				UpdateScale();
			}
		}
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
			if (Weapon == null || animatedSprite == null)
				return;

			// The sprite is 64x64 pixels. Scale it to match the weapon's range.
			// Assuming the base sprite at scale 1.0 represents a range of ~32 pixels (half the sprite size)
			float baseRange = 32.0f;
			float scaleFactor = Weapon.Range / baseRange;

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
			if (fireTimer >= Weapon.Cooldown)
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
				particleMaterial.Set("emission_ring_radius", Mathf.Lerp(0f, Weapon.Range, t));
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
				if (Weapon != null && dist <= Weapon.Range)
				{
					float knockback = Weapon.KnockbackRange * (1f - (dist / (2f * Weapon.Range)));
					if (knockback < Weapon.KnockbackRange * 0.5f)
						knockback = Weapon.KnockbackRange * 0.5f;
					Vector2 dir = (e.GlobalPosition - GlobalPosition).Normalized();
					if (e.HasMethod("ApplyKnockback"))
						e.Call("ApplyKnockback", dir * knockback * Weapon.KnockbackSpeed);
					if (e.HasMethod("TakeDamage"))
						e.Call("TakeDamage", Weapon.Damage);
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
	}
}
