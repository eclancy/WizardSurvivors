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
// **The animation IS the range indicator.** A tongue's look is a life, not a loop: a bud, a full
// tongue, a tear, embers, played once over Lifetime, and Lifetime times Speed is the spell's range. So
// a flame that has visibly gone out has also stopped hurting things, and the player never has to learn
// a reach that differs from what is drawn - the same rule GroundSlamAttack established, kept by
// construction rather than by matching two numbers by hand.
//
// PIXEL ART (2026-10-29, the pass that followed "Update all spells to the same quality we expect
// now"): the tongue was a five-frame sprite strip turned to its heading and scaled by the jet's width,
// which rotated and sheared its art pixels at every heading but four and made them uneven at every
// width but 1. It is now drawn by scenes/shaders/cinder_tongue.gdshader, which snaps to the world's
// 2 px cell before it turns into the tongue's frame: one quad, its life, hit radius and seed in the
// vertex colour, one shared material per ramp so every tongue on screen batches. The flame is laid
// out on HitRadius - the reach the burn tests: its front on it, its fire inside it - and what streams
// behind it is the tail.
//
// It is parented to the ARENA, not the player. A flamethrower throws fire and the fire stays where
// it was thrown; puffs that followed the caster would be an aura wearing a costume.
public partial class FlamePuff : Node2D
{
	[Export] public float Speed { get; set; } = 340f;
	[Export] public float Lifetime { get; set; } = 0.5f;

	// The reach of the flame as drawn (cinder_tongue.gdshader puts its front on this and keeps all its
	// fire inside it), not its tail - the tail is what the flame leaves and does not deal damage.
	[Export] public float HitRadius { get; set; } = 20f;

	public int Damage { get; set; } = 4;
	public SpellData SpellData { get; set; }
	public Player PlayerRef { get; set; }

	private const string ShaderPath = "res://scenes/shaders/cinder_tongue.gdshader";
	private static Shader tongueShader;
	private static ShaderMaterial fireMaterial;
	private static readonly Dictionary<Element, ShaderMaterial> pathMaterials = new();
	// The hit radius rides in a colour channel as radius / RadiusScale (cinder_tongue.gdshader).
	private const float RadiusScale = 128f;
	private float seed;

	/// <summary>
	/// The tongue's material: plain fire, or an element path's ramp (Hellbreath). One per ramp, cached, so
	/// a burn that throws sixteen tongues a second shares one material and the renderer batches them.
	/// </summary>
	public static ShaderMaterial MaterialFor(Element? branch)
	{
		tongueShader ??= GD.Load<Shader>(ShaderPath);
		if (branch is not Element element)
		{
			if (fireMaterial == null || !IsInstanceValid(fireMaterial))
				fireMaterial = new ShaderMaterial { Shader = tongueShader };
			return fireMaterial;
		}
		if (pathMaterials.TryGetValue(element, out ShaderMaterial cached) && IsInstanceValid(cached))
			return cached;
		ElementColors.Ramp ramp = ElementColors.GetRamp(element);
		var material = new ShaderMaterial { Shader = tongueShader };
		material.SetShaderParameter("ramp_core", ramp.Core);
		material.SetShaderParameter("ramp_hot", ramp.Hot);
		material.SetShaderParameter("ramp_mid", ramp.Mid);
		material.SetShaderParameter("ramp_edge", ramp.Edge);
		pathMaterials[element] = material;
		return material;
	}

	// Ember Wake (Flamethrower.EmberWakeId): set by the emitter on the tongues that drop an ember, which
	// falls where the tongue is at EmberAtFraction of its life. Null on every other tongue.
	public CinderGround EmberGround { get; set; }
	public float EmberAtFraction { get; set; } = 1f;
	public float EmberRadius { get; set; }
	public float EmberSeconds { get; set; }
	public int EmberDamage { get; set; }

	private float age = 0f;

	// One hit per enemy per puff. Without it a puff that happens to travel along an enemy's path
	// deals damage on every single frame it overlaps them, which turns a glancing flame into an
	// instant kill and makes the spell's damage depend on frame rate.
	private readonly HashSet<ulong> alreadyHit = new();

	public override void _Ready()
	{
		ZIndex = 5;
		Material = MaterialFor(SpellData?.GetBranchElement());
		seed = (GetInstanceId() % 997) / 997f;
	}

	// One quad round the tongue - the head out to HitRadius ahead and either side, the tail behind - in
	// the node's own units (it is turned to its heading and scaled by the jet's width). Its life, radius
	// and seed ride in the vertex colour, so its modulate must stay white.
	public override void _Draw()
	{
		float scale = MathF.Max(0.01f, MathF.Abs(Scale.X));
		float r = HitRadius / scale;
		var parameters = new Color(Mathf.Clamp(age / MathF.Max(0.01f, Lifetime), 0f, 1f),
			MathF.Min(HitRadius, RadiusScale - 1f) / RadiusScale, seed, 1f);
		DrawRect(new Rect2(-r * 2.1f, -r * 1.35f, r * 3.3f, r * 2.7f), parameters);
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
		else
			QueueRedraw();
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
