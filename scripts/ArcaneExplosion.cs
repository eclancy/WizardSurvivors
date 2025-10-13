using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts
{
	public partial class ArcaneExplosion : Node2D
	{
		// These values get overwritten by the Weapon data when instantiated
		[Export]
		public float Range = 100f;
		[Export]
		public float KnockbackRange = 300f;
		[Export]
		public float KnockbackSpeed = 2.0f; // New: variable knockback speed
		[Export]
		public int Damage = 15;
		[Export]
		public int Pierce = 2;
		public Node2D PlayerRef;
		private float elapsed = 0f;
		private float duration = 0.6f;
		private ParticleProcessMaterial particleMaterial;

		public override void _Ready()
		{
			GD.Print("ArcaneExplosion ready");
			ApplyExplosion();

			var particles = GetNode<GpuParticles2D>("Particles");
			particles.Emitting = true;
			particleMaterial = particles.ProcessMaterial as ParticleProcessMaterial;

			// Set initial scale to 0
			if (particleMaterial != null)
				particleMaterial.Set("emission_ring_radius", 0f);

			// Timer to queue free after effect
			var timer = new Timer();
			timer.WaitTime = duration;
			timer.OneShot = true;
			AddChild(timer);
			timer.Timeout += () => QueueFree();
			timer.Start();
		}

		public override void _Process(double delta)
		{
			if (particleMaterial == null)
				return;

			elapsed += (float)delta;
			float t = Mathf.Clamp(elapsed / duration, 0f, 1f);
			particleMaterial.Set("emission_ring_radius", Mathf.Lerp(0f, Range, t));
		}

		private void ApplyExplosion()
		{
			GD.Print("ArcaneExplosion: Applying explosion effect");
			// Find all enemies in range
			var parent = GetTree().CurrentScene;
			var enemies = parent.GetChildren()
				.OfType<Node2D>()
				.Where(n => n.IsInGroup("enemies"));
			//int pierceLeft = Pierce;
			GD.Print($"Number of enemies found: {enemies.Count()}");
			foreach (var enemy in enemies)
			{
				float dist = GlobalPosition.DistanceTo(enemy.GlobalPosition);
				// if (dist <= Range && pierceLeft > 0)
				if (dist <= Range)
				{
					GD.Print($"Enemy at {enemy.GlobalPosition} is within range {Range} (distance {dist})");
					// Knockback scaling: full at half range, half at max range
					float knockback = KnockbackRange * (1f - (dist / (2f * Range)));
					if (knockback < KnockbackRange * 0.5f)
						knockback = KnockbackRange * 0.5f;
					Vector2 dir = (enemy.GlobalPosition - GlobalPosition).Normalized();
					// Try to call a method on the enemy for knockback and damage
					if (enemy.HasMethod("ApplyKnockback"))
						enemy.Call("ApplyKnockback", dir * knockback * KnockbackSpeed);
					if (enemy.HasMethod("TakeDamage"))
						enemy.Call("TakeDamage", Damage);
					//pierceLeft--;
				}
			}
		}
	}
}
