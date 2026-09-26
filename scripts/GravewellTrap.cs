using Godot;
using System;

namespace WizardSurvivors.scripts;

// Gravewell: a pit set into the ground that waits, and collapses when something walks over it.
//
// Two archetypes at once, both missing from the roster until now.
//
// **Aim: random ground near the player.** It is not thrown at an enemy and it is not centred on the
// caster. It is put somewhere nearby and the enemies decide whether it was a good spot, which is
// area denial rather than aiming - the thing Santa Water does in Vampire Survivors and nothing in
// our roster did.
//
// **Cadence: proximity.** The timer only decides when a trap is *placed*. What decides when it goes
// off is an enemy stepping on it, so the spell can sit doing nothing for seconds and then resolve
// at the exact moment the crowd arrives. A trap that fired on a clock would just be a slow bomb.
//
// It expires on its own so a player kiting in a circle cannot paper the arena with live traps.
public partial class GravewellTrap : Node2D
{
	[Export] public float ArmSeconds { get; set; } = 0.55f;
	[Export] public float TriggerRadius { get; set; } = 42f;
	[Export] public float BlastRadius { get; set; } = 94f;
	[Export] public float MaxLifetime { get; set; } = 12.0f;
	[Export] public float RootSeconds { get; set; } = 1.1f;

	// How long the collapse stays on screen after it resolves. Purely visual; the damage is dealt
	// on the frame it triggers.
	[Export] public float CollapseSeconds { get; set; } = 0.3f;

	public int Damage { get; set; } = 14;
	public SpellData SpellData { get; set; }
	public Player PlayerRef { get; set; }

	private enum Phase { Arming, Armed, Collapsing }

	private Phase phase = Phase.Arming;
	private float phaseSeconds = 0f;
	private float totalSeconds = 0f;

	public override void _Ready()
	{
		// Under everything. A trap the player cannot see past is a trap that gets them killed.
		ZIndex = 1;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		phaseSeconds += dt;
		totalSeconds += dt;
		QueueRedraw();

		switch (phase)
		{
			case Phase.Arming:
				// It cannot be triggered while arming, which is what stops a trap placed into a
				// crowd from resolving on the frame it lands. The wind-up is the cost of the aim
				// being free.
				if (phaseSeconds >= ArmSeconds)
					Enter(Phase.Armed);
				break;

			case Phase.Armed:
				if (AnythingStandingOnIt())
					Collapse();
				else if (totalSeconds >= MaxLifetime)
					QueueFree();
				break;

			case Phase.Collapsing:
				if (phaseSeconds >= CollapseSeconds)
					QueueFree();
				break;
		}
	}

	private void Enter(Phase next)
	{
		phase = next;
		phaseSeconds = 0f;
	}

	private bool AnythingStandingOnIt()
	{
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy || !IsInstanceValid(node))
				continue;

			if (GlobalPosition.DistanceTo(enemy.GlobalPosition) <= TriggerRadius)
				return true;
		}

		return false;
	}

	private void Collapse()
	{
		Enter(Phase.Collapsing);

		if (PlayerRef == null)
			return;

		// The blast is wider than the trigger, on purpose: whatever set it off is caught, and so is
		// whatever was following it. A trap whose blast matched its trigger would only ever hit one
		// enemy, which is the wrong shape for a spell that spends time waiting.
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy || !IsInstanceValid(node))
				continue;

			if (GlobalPosition.DistanceTo(enemy.GlobalPosition) > BlastRadius)
				continue;

			if (node.HasMethod("TakeDamage"))
				PlayerRef.DealDamageToEnemy(node, Damage, source: SpellData);

			// Multiplier 0 is a root, per Enemy.ApplySlow. Rooting what survives is most of the
			// value: the trap is area denial, and an enemy pinned in the hole is denied area.
			if (node.HasMethod("ApplySlow"))
				node.Call("ApplySlow", 0.0f, RootSeconds);
		}
	}

	public override void _Draw()
	{
		var rim = new Color(0.38f, 0.30f, 0.24f, 0.85f);
		var pit = new Color(0.04f, 0.05f, 0.07f, 0.80f);

		switch (phase)
		{
			case Phase.Arming:
			{
				// A ring that closes as it arms, so the player can read "not yet" at a glance.
				float t = Mathf.Clamp(phaseSeconds / MathF.Max(0.01f, ArmSeconds), 0f, 1f);
				DrawArc(Vector2.Zero, TriggerRadius, 0f, Mathf.Tau * t, 24,
					new Color(0.55f, 0.45f, 0.35f, 0.7f), 2.0f);
				break;
			}

			case Phase.Armed:
			{
				DrawCircle(Vector2.Zero, TriggerRadius, pit);
				DrawArc(Vector2.Zero, TriggerRadius, 0f, Mathf.Tau, 26, rim, 2.0f);
				break;
			}

			case Phase.Collapsing:
			{
				// One expanding ring at the real blast radius, fading out. Drawn at the number that
				// was actually tested - the GroundSlamAttack rule.
				float t = Mathf.Clamp(phaseSeconds / MathF.Max(0.01f, CollapseSeconds), 0f, 1f);
				float radius = Mathf.Lerp(TriggerRadius, BlastRadius, t);
				DrawCircle(Vector2.Zero, radius, new Color(0.10f, 0.08f, 0.14f, 0.45f * (1f - t)));
				DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 30,
					new Color(0.62f, 0.52f, 0.72f, 0.8f * (1f - t)), 3.0f);
				break;
			}
		}
	}
}
