using Godot;
using System;
using WizardSurvivors.scripts;

// Hollow Star: a dark well opened in the thick of the swarm that drags everything round it into one
// knot, then collapses on what it gathered (issue #64).
//
// **The shape no other spell has: it moves the enemies.** Every other spell in the roster takes the
// swarm where it finds it. This one rearranges it - and the value is mostly not its own damage but
// what the knot does for everything else the player owns. A cone, an eruption or a Gravewell aimed
// at one tight clump does the work it would otherwise need three casts for. VortexPull used to exist
// only as an Arcane Explosion rider; this is the spell that is ABOUT it.
//
// **Pure Darkness.** Darkness had no pure spell, and only a pure spell can carry an element to its
// capstone (evolution options are the only route to a second tag), so the element could never be
// fully built. Hollow Star is that spell.
//
// The pull rides Enemy.ApplyKnockback rather than a new movement path, which is what makes bosses
// behave without special cases: KnockbackResistance already scales it, so a boss leans into the well
// and the Archivist (resistance 1.0) ignores it entirely. Untargetable enemies have left the
// "enemies" group and are never touched.
public partial class HollowStar : Node2D
{
	// A beat of warning before anything moves, so the player can read where the knot will form.
	[Export] public float OpenSeconds { get; set; } = 0.25f;
	[Export] public float PullSeconds { get; set; } = 1.6f;
	[Export] public float CollapseSeconds { get; set; } = 0.3f;
	[Export] public float PullRadius { get; set; } = 150f;
	// What the collapse hits. Smaller than the reach on purpose: the pull is what fills it, so a
	// well that caught nothing in its pull also hits little when it closes.
	[Export] public float CollapseRadius { get; set; } = 80f;
	// Pull speed in px/s, eased down near the centre (speed = min(PullSpeed, distance x
	// PullStiffness)) so the swarm settles into a knot instead of overshooting through it.
	[Export] public float PullSpeed { get; set; } = 240f;
	[Export] public float PullStiffness { get; set; } = 4.5f;
	// How often the pull is re-applied. ApplyKnockback holds a constant velocity for its first
	// ~0.3 s, so a tenth of a second keeps the drag continuous at a tenth of the group scans a
	// per-frame pull would cost.
	[Export] public float PullTickSeconds { get; set; } = 0.1f;

	public int Damage { get; set; } = 18;
	public SpellData SpellData { get; set; }
	public Player PlayerRef { get; set; }

	private enum Phase { Opening, Pulling, Collapsing }

	private Phase phase = Phase.Opening;
	private float phaseSeconds = 0f;
	private float pullTickTimer = 0f;
	private float spin = 0f;

	// Preallocated: _Draw runs every frame for the whole life of the well (see BlackTentacles).
	private const int ArmCount = 3;
	private const int ArmPoints = 14;
	private readonly Vector2[][] armPoints = new Vector2[ArmCount][];

	private static readonly Color CoreColor = new Color(0.03f, 0.02f, 0.05f, 0.92f);
	private static readonly Color ArmColor = new Color(0.42f, 0.20f, 0.62f, 0.75f);
	private static readonly Color ReachColor = new Color(0.30f, 0.14f, 0.44f, 0.35f);
	private static readonly Color RimColor = new Color(0.72f, 0.52f, 0.95f, 0.85f);

	public override void _Ready()
	{
		// Under the enemies it is dragging, so the knot reads as bodies over a hole.
		ZIndex = 1;
		for (int a = 0; a < ArmCount; a++)
			armPoints[a] = new Vector2[ArmPoints];
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		phaseSeconds += dt;
		QueueRedraw();

		switch (phase)
		{
			case Phase.Opening:
				spin += dt * 2f;
				if (phaseSeconds >= OpenSeconds)
					Enter(Phase.Pulling);
				break;

			case Phase.Pulling:
				spin += dt * 6f;
				pullTickTimer -= dt;
				if (pullTickTimer <= 0f)
				{
					pullTickTimer = PullTickSeconds;
					Pull();
				}
				if (phaseSeconds >= PullSeconds)
					Collapse();
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

	private void Pull()
	{
		float reachSquared = PullRadius * PullRadius;
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Enemy enemy || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;

			Vector2 toCentre = GlobalPosition - enemy.GlobalPosition;
			float distanceSquared = toCentre.LengthSquared();
			if (distanceSquared > reachSquared)
				continue;

			float distance = MathF.Sqrt(distanceSquared);
			// Already in the knot. Pulling an enemy sitting on the centre only makes it jitter.
			if (distance < 6f)
				continue;

			float speed = MathF.Min(PullSpeed, distance * PullStiffness);
			enemy.ApplyKnockback(toCentre / distance * speed);
		}
	}

	private void Collapse()
	{
		Enter(Phase.Collapsing);

		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;

		float radiusSquared = CollapseRadius * CollapseRadius;
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy || !IsInstanceValid(enemy))
				continue;
			if (GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition) > radiusSquared)
				continue;

			PlayerRef.DealDamageToEnemy(node, Damage, source: SpellData);
		}
	}

	public override void _Draw()
	{
		switch (phase)
		{
			case Phase.Opening:
			{
				// The reach ring draws itself closed as the well opens - the same "not yet" read the
				// Gravewell arming ring gives, at the radius the pull will actually use.
				float t = Mathf.Clamp(phaseSeconds / MathF.Max(0.01f, OpenSeconds), 0f, 1f);
				DrawArc(Vector2.Zero, PullRadius, spin, spin + Mathf.Tau * t, 40, ReachColor, 2.0f);
				DrawCircle(Vector2.Zero, CollapseRadius * 0.3f * t, CoreColor);
				break;
			}

			case Phase.Pulling:
			{
				DrawArc(Vector2.Zero, PullRadius, 0f, Mathf.Tau, 40, ReachColor, 1.5f);
				DrawArms();
				// The core swells as the pull runs, so the moment of collapse can be anticipated.
				float t = Mathf.Clamp(phaseSeconds / MathF.Max(0.01f, PullSeconds), 0f, 1f);
				float core = Mathf.Lerp(CollapseRadius * 0.3f, CollapseRadius * 0.45f, t);
				DrawCircle(Vector2.Zero, core, CoreColor);
				DrawArc(Vector2.Zero, core, 0f, Mathf.Tau, 24, RimColor, 1.5f);
				break;
			}

			case Phase.Collapsing:
			{
				// One ring at the real collapse radius, fading out. Drawn at the number that hits.
				float t = Mathf.Clamp(phaseSeconds / MathF.Max(0.01f, CollapseSeconds), 0f, 1f);
				float radius = Mathf.Lerp(CollapseRadius * 0.45f, CollapseRadius, t);
				DrawCircle(Vector2.Zero, radius, new Color(0.10f, 0.04f, 0.16f, 0.5f * (1f - t)));
				DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 32, new Color(RimColor, RimColor.A * (1f - t)), 3.0f);
				break;
			}
		}
	}

	// Three arms spiralling in from the reach to the core, turning with the pull.
	private void DrawArms()
	{
		float inner = CollapseRadius * 0.3f;
		for (int a = 0; a < ArmCount; a++)
		{
			Vector2[] points = armPoints[a];
			float baseAngle = spin + a * Mathf.Tau / ArmCount;
			for (int i = 0; i < ArmPoints; i++)
			{
				float t = i / (float)(ArmPoints - 1);
				float radius = Mathf.Lerp(PullRadius * 0.95f, inner, t);
				float angle = baseAngle + t * 3.2f;
				points[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
			}
			DrawPolyline(points, ArmColor, 2.5f);
		}
	}
}
