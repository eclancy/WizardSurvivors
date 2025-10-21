using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts
{
	public partial class ArcaneExplosion : Area2D
	{
		// These values get overwritten by the Weapon data when instantiated
		[Export]
		public float Range = 100f;
		[Export]
		public float KnockbackRange = 300f;
		[Export]
		public float KnockbackSpeed = 2.0f;
		[Export]
		public int Damage = 15;
		[Export]
		public int Pierce = 2;
		[Export]
		public float Cooldown = 1.5f;
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

		public override void _Ready()
		{
			GD.Print("ArcaneExplosion ready");
			// We'll trigger explosions on a cooldown timer regardless of enemy proximity
			particles = GetNode<GpuParticles2D>("Particles");
			animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
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

			// Trigger immediately on spawn, then start cooldown timer
			TriggerExplosion();
			fireTimer = 0f;
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
			if (fireTimer >= Cooldown)
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
				particleMaterial.Set("emission_ring_radius", Mathf.Lerp(0f, Range, t));
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
				if (dist <= Range)
				{
					float knockback = KnockbackRange * (1f - (dist / (2f * Range)));
					if (knockback < KnockbackRange * 0.5f)
						knockback = KnockbackRange * 0.5f;
					Vector2 dir = (e.GlobalPosition - GlobalPosition).Normalized();
					if (e.HasMethod("ApplyKnockback"))
						e.Call("ApplyKnockback", dir * knockback * KnockbackSpeed);
					if (e.HasMethod("TakeDamage"))
						e.Call("TakeDamage", Damage);
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
			QueueFree();
		}
	}
}
