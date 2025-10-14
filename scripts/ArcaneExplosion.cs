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
		private float elapsed = 0f;
		private float duration = 0.6f;
		private float fireTimer = 0f;
		private bool canExplode = true;
		private ParticleProcessMaterial particleMaterial;
		private CollisionShape2D collisionShape;

		public override void _Ready()
		{
			GD.Print("ArcaneExplosion ready");
			collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
			collisionShape.Shape = new CircleShape2D { Radius = Range };
			BodyEntered += OnBodyEntered;

			var particles = GetNode<GpuParticles2D>("Particles");
			particles.Emitting = false;
			particleMaterial = particles.ProcessMaterial as ParticleProcessMaterial;
			if (particleMaterial != null)
				particleMaterial.Set("emission_ring_radius", 0f);
		}

		public override void _Process(double delta)
		{
			fireTimer += (float)delta;
			if (!canExplode && fireTimer >= Cooldown)
			{
				canExplode = true;
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
		}

		private void OnBodyEntered(Node body)
		{
			if (!canExplode)
				return;
			if (body.IsInGroup("enemies"))
			{
				GD.Print($"ArcaneExplosion: Enemy entered range, triggering explosion");
				Explode(body);
				canExplode = false;
				fireTimer = 0f;
				elapsed = 0f;
				var particles = GetNode<GpuParticles2D>("Particles");
				particles.Emitting = true;
				// Optionally, queue free after effect
				var timer = new Timer();
				timer.WaitTime = duration;
				timer.OneShot = true;
				AddChild(timer);
				timer.Timeout += () => particles.Emitting = false;
				timer.Start();
			}
		}

		private void Explode(Node enemy)
		{
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
		}
	}
}
