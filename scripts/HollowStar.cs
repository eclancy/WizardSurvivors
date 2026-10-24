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
//
// **Eclipse** (the level 4 element path into Light, ElementPaths): the core stays black and
// everything round it burns in Light's ramp - arms, motes, rim and collapse gold, and a corona ringing the core
// while it swells. The collapse Weakens what it catches (the rider, applied by
// Player.DealDamageToEnemy), so the knot it gathered hits the player softer.
public partial class HollowStar : Node2D, IPaintsElementPath
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

	// THE SPIRIT MIRROR (John Dee's Apotheosis, verb "pull"). The reach is set by Player.FireHollowStar
	// (x SpiritMirrorReach); what is here is the second half - the well hands over what it gathered.
	public const float SpiritMirrorReach = 1.5f;
	public const float SpiritMirrorPullSpeed = 1.35f;
	private bool IsSpiritMirror => SpellData?.IsApotheosis == true;

	private enum Phase { Opening, Pulling, Collapsing }

	private Phase phase = Phase.Opening;
	private float phaseSeconds = 0f;
	private float pullTickTimer = 0f;
	private float spin = 0f;

	// Preallocated: _Draw runs every frame for the whole life of the well (see BlackTentacles).
	private const int ArmCount = 3;
	private const int ArmPoints = 14;
	private readonly Vector2[][] armPoints = new Vector2[ArmCount][];

	private const int MoteCount = 14;
	private uint debrisSeed;
	// What the collapse throws out: Darkness, or the element path's ramp (Eclipse's Light).
	private ElementColors.Ramp debrisRamp = ElementColors.GetRamp(Element.Darkness);

	private static readonly Color CoreColor = new Color(0.03f, 0.02f, 0.05f, 0.92f);
	// Darkness tones; an element path repaints them in its own ramp (the core stays black).
	private Color armColor = new Color(0.42f, 0.20f, 0.62f, 0.75f);
	private Color rimColor = new Color(0.72f, 0.52f, 0.95f, 0.85f);
	// The corona round the core, drawn only on an element path. Transparent otherwise.
	private Color coronaColor = new Color(0f, 0f, 0f, 0f);

	public override void _Ready()
	{
		// Under the enemies it is dragging, so the knot reads as bodies over a hole.
		ZIndex = 1;
		for (int a = 0; a < ArmCount; a++)
			armPoints[a] = new Vector2[ArmPoints];
		debrisSeed = (uint)(GetInstanceId() % 100003);
		if (SpellData?.GetBranchElement() is Element branch)
		{
			ElementColors.Ramp ramp = ElementColors.GetRamp(branch);
			debrisRamp = ramp;
			armColor = new Color(ramp.Edge, 0.8f);
			rimColor = new Color(ramp.Hot, 0.95f);
			coronaColor = new Color(ramp.Core, 0.55f);
		}
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
				// Held until the collapse's debris has landed and cooled; it hit on the first frame.
				if (phaseSeconds >= MathF.Max(CollapseSeconds, ImpactDebris.LifeSeconds))
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

		if (IsSpiritMirror)
			PullLootToPlayer();

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

	// Every XP orb and every pickup that answers the magnet (a potion, a flask - never a chest) inside
	// the reach flies to the player, from the frame the well closes. Orbs live in the group; pickups
	// are children of the current scene, where Enemy.TryDropBonusItem puts them, so they are read
	// there rather than by a walk of the whole tree.
	private void PullLootToPlayer()
	{
		float reachSquared = PullRadius * PullRadius;
		foreach (Node node in GetTree().GetNodesInGroup(XPOrb.GroupName))
		{
			if (node is XPOrb orb && IsInstanceValid(orb) && GlobalPosition.DistanceSquaredTo(orb.GlobalPosition) <= reachSquared)
				orb.PullToPlayer();
		}
		foreach (Node node in GetTree().CurrentScene?.GetChildren() ?? new Godot.Collections.Array<Node>())
		{
			if (node is PickupBase pickup && IsInstanceValid(pickup) && GlobalPosition.DistanceSquaredTo(pickup.GlobalPosition) <= reachSquared)
				pickup.PullToPlayer();
		}
	}

	// No ring marks the reach (2026-10-23: "remove aoe markers from abilities, their animations should
	// make it clear what the hitbox is"). The arms are the reach: they grow out from the core to
	// exactly PullRadius as the well opens, and while it pulls, motes are dragged in along them from
	// that same rim - so how far it pulls is shown by what it pulls. The collapse is debris thrown out
	// to exactly CollapseRadius (ImpactDebris), which is the only thing that says how far it hit.
	public override void _Draw()
	{
		switch (phase)
		{
			case Phase.Opening:
			{
				float t = Mathf.Clamp(phaseSeconds / MathF.Max(0.01f, OpenSeconds), 0f, 1f);
				DrawArms(t);
				DrawCircle(Vector2.Zero, CollapseRadius * 0.3f * t, CoreColor);
				break;
			}

			case Phase.Pulling:
			{
				DrawArms(1f);
				DrawMotes();
				// The core swells as the pull runs, so the moment of collapse can be anticipated.
				float t = Mathf.Clamp(phaseSeconds / MathF.Max(0.01f, PullSeconds), 0f, 1f);
				float core = Mathf.Lerp(CollapseRadius * 0.3f, CollapseRadius * 0.45f, t);
				// Eclipse's corona: a flickering ring of light hugging the black disc, inside the
				// collapse radius - it is the well's own rim, not a mark of its reach.
				if (coronaColor.A > 0f)
				{
					float flicker = 1f + 0.06f * MathF.Sin(spin * 3.1f);
					DrawArc(Vector2.Zero, core * 1.22f * flicker, 0f, Mathf.Tau, 32, coronaColor, 3.0f);
				}
				DrawCircle(Vector2.Zero, core, CoreColor);
				DrawArc(Vector2.Zero, core, 0f, Mathf.Tau, 24, rimColor, 1.5f);
				break;
			}

			case Phase.Collapsing:
			{
				// The core snaps shut, and what it gathered is thrown out to the collapse radius.
				float shut = 1f - Mathf.Clamp(phaseSeconds / 0.08f, 0f, 1f);
				if (shut > 0f)
					DrawCircle(Vector2.Zero, CollapseRadius * 0.45f * shut, CoreColor);
				ImpactDebris.Draw(this, Vector2.Zero, CollapseRadius, phaseSeconds, debrisSeed, debrisRamp,
					ImpactDebris.CountFor(CollapseRadius), Colors.White);
				break;
			}
		}
	}

	// Dust and light dragged in from the rim of the pull along a spiral, faster as it nears the core:
	// fourteen squares on the art grid, laid out from the clock so nothing is stored or allocated.
	private void DrawMotes()
	{
		float inner = CollapseRadius * 0.3f;
		for (int i = 0; i < MoteCount; i++)
		{
			float p = (i * 0.6180339f + phaseSeconds * 0.9f) % 1f;
			float r = Mathf.Lerp(PullRadius - WhipLimb.Grid, inner, p * p);
			float a = i * (Mathf.Tau / MoteCount) + spin * 0.5f + p * 2.4f;
			float size = p < 0.5f ? WhipLimb.Grid * 2f : WhipLimb.Grid;
			Color c = p < 0.6f ? armColor : rimColor;
			DrawRect(WhipLimb.Cell(new Vector2(MathF.Cos(a), MathF.Sin(a)) * r, size), c with { A = MathF.Min(1f, c.A + 0.2f) });
		}
	}

	// Three arms spiralling in from the reach to the core, turning with the pull. `grown` 0..1 is how
	// far out they have reached: they open from the core, and at 1 their outer ends are on PullRadius,
	// held inside it by half their width.
	private void DrawArms(float grown)
	{
		float inner = CollapseRadius * 0.3f;
		float outer = Mathf.Lerp(inner, PullRadius - 1.25f, grown);
		for (int a = 0; a < ArmCount; a++)
		{
			Vector2[] points = armPoints[a];
			float baseAngle = spin + a * Mathf.Tau / ArmCount;
			for (int i = 0; i < ArmPoints; i++)
			{
				float t = i / (float)(ArmPoints - 1);
				float radius = Mathf.Lerp(outer, inner, t);
				float angle = baseAngle + t * 3.2f;
				points[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
			}
			DrawPolyline(points, armColor, 2.5f);
		}
	}
}
