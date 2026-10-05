using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// Iron Palisade: a line of stakes driven up between the player and the nearest pack (.ai/side-events.md,
// issue #64). Carried by the caravan master on the Scorched Sands road.
//
// **The wall.** The one delivery shape #64 still listed as missing: something that occupies space
// against the swarm. It is not a physics body - a body on a layer the enemies collide with would
// need a new layer and every enemy mask changed to match (CLAUDE.md, collision layers), and it would
// stop the player too. Instead anything inside the line is shoved back out the far side every tick
// and cut once per CutIntervalSeconds, which holds a front exactly as a fence would while letting
// the player walk through their own wall.
//
// **Pure Metal.** Metal had no pure spell, so it could never reach its capstone.
//
// The shove rides Enemy.ApplyKnockback, like Hollow Star's pull, so boss knockback resistance
// applies without a special case.
public partial class IronPalisade : Node2D
{
	[Export] public float HalfLength { get; set; } = 80f;
	[Export] public float HalfThickness { get; set; } = 14f;
	[Export] public float LifeSeconds { get; set; } = 4f;
	[Export] public float ShoveSpeed { get; set; } = 240f;
	[Export] public float CutIntervalSeconds { get; set; } = 0.5f;
	[Export] public float RiseSeconds { get; set; } = 0.15f;

	public int Damage { get; set; } = 8;
	public SpellData SpellData { get; set; }
	public Player PlayerRef { get; set; }

	/// <summary>Unit vector pointing away from the player, across the wall. Set before adding.</summary>
	public Vector2 Facing { get; set; } = Vector2.Right;

	private float age;
	private float tickTimer;
	// Instance id -> the age at which it may be cut again. Never pruned: a wall lives four seconds
	// and meets a few dozen enemies, and pruning would cost an allocation per frame.
	private readonly Dictionary<ulong, float> nextCutAt = new();

	private const int StakeCount = 9;
	private static readonly Color Iron = new Color(0.34f, 0.36f, 0.42f);
	private static readonly Color IronLit = new Color(0.72f, 0.74f, 0.80f);
	private static readonly Color Shadow = new Color(0f, 0f, 0f, 0.35f);

	public override void _Ready()
	{
		ZIndex = 1;
		Rotation = Facing.Angle();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		age += dt;
		QueueRedraw();
		if (age >= LifeSeconds)
		{
			QueueFree();
			return;
		}

		tickTimer -= dt;
		if (tickTimer > 0f || age < RiseSeconds)
			return;
		tickTimer = 0.1f;

		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Enemy enemy || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;

			// In the wall's own frame: x across the wall (along Facing), y along it.
			Vector2 local = ToLocal(enemy.GlobalPosition);
			if (Mathf.Abs(local.X) > HalfThickness + 10f || Mathf.Abs(local.Y) > HalfLength + 8f)
				continue;

			enemy.ApplyKnockback(Facing * ShoveSpeed);
			ulong id = enemy.GetInstanceId();
			if ((nextCutAt.TryGetValue(id, out float readyAt) && age < readyAt) || PlayerRef == null || !IsInstanceValid(PlayerRef))
				continue;
			nextCutAt[id] = age + CutIntervalSeconds;
			PlayerRef.DealDamageToEnemy(enemy, Damage, source: SpellData);
		}
	}

	public override void _Draw()
	{
		float rise = Mathf.Clamp(age / RiseSeconds, 0f, 1f);
		float fade = Mathf.Clamp((LifeSeconds - age) / 0.3f, 0f, 1f);
		for (int i = 0; i < StakeCount; i++)
		{
			float y = Mathf.Lerp(-HalfLength, HalfLength, i / (float)(StakeCount - 1));
			// Stakes stagger slightly so the line reads as driven by hand, not drawn with a ruler.
			float x = (i % 2 == 0 ? -3f : 3f);
			float height = (i % 3 == 0 ? 22f : 17f) * rise;
			Vector2 foot = new Vector2(x, y);
			DrawCircle(foot + new Vector2(2f, 2f), 4f, new Color(Shadow, Shadow.A * fade));
			// Drawn "up" in screen terms regardless of rotation, so the stakes always stand upright.
			Vector2 up = new Vector2(0f, -1f).Rotated(-Rotation) * height;
			DrawLine(foot, foot + up, new Color(Iron, fade), 5f);
			DrawLine(foot + new Vector2(-1f, 0f), foot + up + new Vector2(-1f, 0f), new Color(IronLit, fade), 1.5f);
		}
	}
}
