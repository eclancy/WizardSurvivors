using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Glacial Spike's Rimefoot (IceSpike.RimefootId, 2026-10-29): the ground a spike erupted from is left
// frozen, and whatever crosses it is slowed.
//
// It is the spike's own footprint: the radius it hit at, laid the moment it strikes, for Seconds. The
// slow is re-applied every TickSeconds to whatever stands on it, with a short linger, so it lasts exactly
// as long as an enemy is on the ice and wears off just after it steps off - the LingeringZone rule - and
// the stacking-resistant rule (Enemy.ApplySlow) means overlapping patches never slow twice.
//
// Drawn by scenes/shaders/frost_patch.gdshader on one quad, on the ground under every body
// (DrawLayers.Ground): a crust of rime, cracks out of the hole the spike withdrew into, thawing from the
// edge at the end. The radius drawn is the radius tested. Never laid on blocked ground - IceSpike only
// lays one where a spike rose, and a spike never rises from water or a wall.
public partial class FrostPatch : Node2D
{
	public const float Seconds = 3f;
	/// <summary>What crosses the ice moves at this share of its speed.</summary>
	public const float SlowMultiplier = 0.55f;
	private const float TickSeconds = 0.2f;
	private const string ShaderPath = "res://scenes/shaders/frost_patch.gdshader";

	private static Shader shader;
	private static readonly List<Enemy> scratch = new();
	private static readonly StringName ParamAge = "age";

	public float Radius { get; private set; }
	private float age;
	private float tick;
	private ShaderMaterial material;

	/// <summary>Freezes the ground round <paramref name="at"/> (world) out to <paramref name="radius"/>.</summary>
	public static void Lay(Node2D arena, Vector2 at, float radius)
	{
		if (arena == null || radius < 4f)
			return;
		var patch = new FrostPatch { Radius = radius };
		arena.AddChild(patch);
		patch.GlobalPosition = at;
		patch.Configure(at);
	}

	public override void _Ready()
	{
		Name = "FrostPatch";
		ZAsRelative = false;
		ZIndex = DrawLayers.Ground;
	}

	private void Configure(Vector2 at)
	{
		shader ??= GD.Load<Shader>(ShaderPath);
		if (shader == null)
		{
			GD.PushError($"FrostPatch: {ShaderPath} failed to load.");
			return;
		}
		material = new ShaderMaterial { Shader = shader };
		material.SetShaderParameter("centre", at);
		material.SetShaderParameter("radius", Radius);
		material.SetShaderParameter("life", Seconds);
		material.SetShaderParameter("seed", (float)(GetInstanceId() % 997) * 0.731f);
		material.SetShaderParameter(ParamAge, 0f);
		Material = material;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		age += dt;
		if (age >= Seconds)
		{
			QueueFree();
			return;
		}
		material?.SetShaderParameter(ParamAge, age);
		tick -= dt;
		if (tick > 0f)
			return;
		tick = TickSeconds;
		Vector2 at = GlobalPosition;
		float r2 = Radius * Radius;
		EnemyRegistry.CollectNear(at, Radius, scratch);
		foreach (Enemy enemy in scratch)
		{
			if (IsInstanceValid(enemy) && !enemy.IsDying && at.DistanceSquaredTo(enemy.GlobalPosition) <= r2)
				enemy.ApplySlow(SlowMultiplier, TickSeconds * 1.75f);
		}
		scratch.Clear();
	}

	// One quad over the patch; the shader decides every cell and draws nothing past the radius.
	public override void _Draw()
	{
		float half = Radius + 2f;
		DrawRect(new Rect2(-half, -half, half * 2f, half * 2f), Colors.White);
	}
}
