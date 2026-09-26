using Godot;
using System;

namespace WizardSurvivors.scripts;

// Kindled Ward: a mote of light you make, which then goes and does its job without you.
//
// This is the autonomous-agent archetype the roster has never had - the one thing no amount of
// re-pointing an existing spell can fake, because every other spell resolves the instant it is
// cast and this one is still working several seconds later, somewhere you are not.
//
// IT IS NOT A COMPANION, and that is a hard constraint rather than flavour. `.ai/world-and-tone.md`
// is explicit that the player has no allies and no escort - everyone who could help is in a cell -
// and that everything glowing on screen other than the player is *something you made*: a ward, a
// flame, a spell. So this is a made thing. It has no face, no name and no loyalty; it is a lit
// object that drifts at whatever is nearest and burns it, and when it has burned out it is gone.
// A summoned creature with its own personality would quietly delete the premise of the game.
public partial class KindledWard : Node2D
{
	[Export] public float DriftSpeed { get; set; } = 95f;
	[Export] public float StrikeRadius { get; set; } = 34f;
	[Export] public float StrikeInterval { get; set; } = 0.6f;
	[Export] public float Lifetime { get; set; } = 9.0f;

	// Slower than the player and slower than most enemies, on purpose. A ward that could chase
	// anything down would make positioning irrelevant; one that drifts has to be *placed*, which
	// means the player casting it is still making a decision about where they are standing.
	[Export] public float TargetRefreshSeconds { get; set; } = 0.25f;

	public int Damage { get; set; } = 6;
	public SpellData SpellData { get; set; }
	public Player PlayerRef { get; set; }

	private float age = 0f;
	private float strikeTimer = 0f;
	private float retargetTimer = 0f;
	private float bobSeconds = 0f;
	private Node2D target;

	public override void _Ready()
	{
		ZIndex = 6;
		bobSeconds = (GetInstanceId() % 29) * 0.21f;
		AcquireTarget();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		age += dt;
		bobSeconds += dt;

		retargetTimer += dt;
		if (retargetTimer >= TargetRefreshSeconds)
		{
			retargetTimer = 0f;
			AcquireTarget();
		}

		if (target != null && IsInstanceValid(target))
		{
			Vector2 toTarget = target.GlobalPosition - GlobalPosition;
			if (toTarget.LengthSquared() > 1f)
				GlobalPosition += toTarget.Normalized() * DriftSpeed * dt;
		}

		strikeTimer += dt;
		if (strikeTimer >= StrikeInterval)
		{
			strikeTimer = 0f;
			Strike();
		}

		QueueRedraw();

		if (age >= Lifetime)
			QueueFree();
	}

	private void AcquireTarget()
	{
		Node2D nearest = null;
		float best = float.MaxValue;

		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy || !IsInstanceValid(node))
				continue;

			float dist = GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition);
			if (dist < best)
			{
				best = dist;
				nearest = enemy;
			}
		}

		target = nearest;
	}

	private void Strike()
	{
		if (PlayerRef == null)
			return;

		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy || !IsInstanceValid(node))
				continue;

			if (GlobalPosition.DistanceTo(enemy.GlobalPosition) > StrikeRadius)
				continue;

			if (node.HasMethod("TakeDamage"))
				PlayerRef.DealDamageToEnemy(node, Damage, source: SpellData);
		}
	}

	public override void _Draw()
	{
		// Guttering in the last second is the only warning the player gets that a ward is about to
		// go out, and it matters: the decision to cast the next one is made on that cue.
		float remaining = Lifetime - age;
		float fade = remaining < 1.0f ? Mathf.Max(0.15f, remaining) : 1.0f;
		float flicker = 1.0f + MathF.Sin(bobSeconds * 9.3f) * 0.08f;

		var halo = new Color(1.0f, 0.86f, 0.46f, 0.20f * fade);
		var body = new Color(1.0f, 0.93f, 0.66f, 0.55f * fade);
		var core = new Color(1.0f, 0.99f, 0.90f, 0.92f * fade);

		DrawCircle(Vector2.Zero, StrikeRadius * flicker, halo);
		DrawCircle(Vector2.Zero, 9f * flicker, body);
		DrawCircle(Vector2.Zero, 4.5f * flicker, core);
	}
}
