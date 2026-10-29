using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// One tongue of flame thrown by Cinderbreath.
//
// The spell used to be a single cone drawn in _Draw and tested analytically. That was honest about
// its reach and cost no art, but it read as a lit wedge rather than as fire - nothing in it moved
// except a sine wobble on the edge, so the eye had nothing to follow. Fire reads in this genre
// because you can watch individual tongues leave the caster, travel, and go out. This is one of
// those tongues.
//
// **The animation IS the range indicator.** The five frames of cinderbreath-flame.png are a life,
// not a loop: a bud, a full tongue, a tear, embers. The sprite's SpeedScale is set so the strip
// plays exactly once over Lifetime, and Lifetime times Speed is the spell's range. So a flame that
// has visibly gone out has also stopped hurting things, and the player never has to learn a reach
// that differs from what is drawn - the same rule GroundSlamAttack established, kept by
// construction rather than by matching two numbers by hand.
//
// It is parented to the ARENA, not the player. A flamethrower throws fire and the fire stays where
// it was thrown; puffs that followed the caster would be an aura wearing a costume.
public partial class FlamePuff : Node2D
{
	[Export] public float Speed { get; set; } = 340f;
	[Export] public float Lifetime { get; set; } = 0.5f;

	// Roughly the width of the flame head as drawn, not the whole 64px cell - the tail is smoke and
	// should not deal damage. See the note above about the drawn shape and the hit shape agreeing.
	[Export] public float HitRadius { get; set; } = 20f;

	// The strip is five frames authored to play in half a second at SpeedScale 1. Lifetime rescales
	// it rather than the other way round, so retuning the spell's range never desyncs the art.
	[Export] public float AuthoredStripSeconds { get; set; } = 0.5f;

	public int Damage { get; set; } = 4;
	public SpellData SpellData { get; set; }
	public Player PlayerRef { get; set; }

	// The element path's repaint (ElementRampMaterial), set by the emitter before the puff is added.
	// Null for plain fire.
	public ShaderMaterial RampMaterial { get; set; }

	// Ember Wake (Flamethrower.EmberWakeId): set by the emitter on the tongues that drop an ember, which
	// falls where the tongue is at EmberAtFraction of its life. Null on every other tongue.
	public CinderGround EmberGround { get; set; }
	public float EmberAtFraction { get; set; } = 1f;
	public float EmberRadius { get; set; }
	public float EmberSeconds { get; set; }
	public int EmberDamage { get; set; }

	private float age = 0f;
	private AnimatedSprite2D sprite;

	// One hit per enemy per puff. Without it a puff that happens to travel along an enemy's path
	// deals damage on every single frame it overlaps them, which turns a glancing flame into an
	// instant kill and makes the spell's damage depend on frame rate.
	private readonly HashSet<ulong> alreadyHit = new();

	public override void _Ready()
	{
		ZIndex = 5;

		sprite = GetNodeOrNull<AnimatedSprite2D>("Sprite");
		if (sprite != null)
		{
			sprite.SpeedScale = AuthoredStripSeconds / MathF.Max(0.05f, Lifetime);
			if (RampMaterial != null)
				sprite.Material = RampMaterial;
			sprite.Play();
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		age += dt;

		// Swept in steps no longer than the hit radius. Moving the whole frame's distance and testing
		// only where it landed let a tongue jump clean past everything whenever a frame ran long - a
		// tongue lives 0.42 s and covers its range in that time, so at a low frame rate, or under the
		// balance harness at 4x, it could cross the whole cone in one step and burn nothing.
		Vector2 travel = Vector2.Right.Rotated(Rotation) * Speed * dt;
		int steps = Mathf.Clamp(Mathf.CeilToInt(travel.Length() / Mathf.Max(4f, HitRadius)), 1, 8);
		Vector2 step = travel / steps;
		for (int i = 0; i < steps; i++)
		{
			GlobalPosition += step;
			BurnWhatItTouches();
		}

		if (EmberGround != null && age >= Lifetime * EmberAtFraction)
		{
			if (IsInstanceValid(EmberGround))
				EmberGround.DropEmber(GlobalPosition, EmberRadius, EmberSeconds, EmberDamage);
			EmberGround = null;
		}

		if (age >= Lifetime)
			QueueFree();
	}

	private void BurnWhatItTouches()
	{
		if (PlayerRef == null || Damage <= 0)
			return;

		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Node2D enemy || !IsInstanceValid(node))
				continue;

			if (alreadyHit.Contains(node.GetInstanceId()))
				continue;

			if (GlobalPosition.DistanceTo(enemy.GlobalPosition) > HitRadius)
				continue;

			if (!node.HasMethod("TakeDamage"))
				continue;

			alreadyHit.Add(node.GetInstanceId());
			PlayerRef.DealDamageToEnemy(node, Damage, source: SpellData);
		}
	}
}
