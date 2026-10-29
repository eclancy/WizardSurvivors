using Godot;
using System;
using System.Collections.Generic;
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
//
// **Its own four cards** (2026-10-29, .ai/spell-roster.md section 2e: "lets make all mutations more
// unique per spell"). Each develops the pull, which is this spell's and no other's; the ids are the
// template's, so John Dee's path (Crush, then Black Sun) and every save carry over.
//   - Crush (level 4): the collapse hits harder for every body in the knot, and the core swells as
//     the knot fills - CrushPerBody a body, up to CrushMostBodies.
//   - Twin Stars (level 4): two wells a cast, on two packs, that drift together while they pull,
//     tethered, so the two knots are dragged into one and the two collapses land on it.
//   - Black Sun (level 8): the star does not go out. It pulls and collapses BlackSunCollapses times,
//     re-pulling between, and the motes circling its core count the collapses it has left.
//   - Nova Seed (level 8): the collapse throws NovaShards shards of the star at the nearest packs
//     outside it; each opens a smaller well where it lands, which knots that pack in turn.
public partial class HollowStar : Node2D, IPaintsElementPath
{
	public const string CrushId = "hollow_star_amplified_impact";
	public const string TwinStarsId = "hollow_star_hyper_acceleration";
	public const string BlackSunId = "hollow_star_titan_cataclysm";
	public const string NovaSeedId = "hollow_star_infinite_horizon";

	// Crush: the collapse's damage x (1 + CrushPerBody x the bodies inside it), counting no more than
	// CrushMostBodies - a knot of fifteen is a full one; past it the swarm only stacks deeper.
	public const float CrushPerBody = 0.06f;
	public const int CrushMostBodies = 15;

	// Twin Stars: the twin opens on a pack at least a reach from the first and at most this many
	// reaches away, so the two can meet within one pull. They stop this share of a collapse apart.
	public const float TwinMostApartReaches = 3.5f;
	private const float TwinMeetGap = 0.3f;

	// Black Sun: collapses a star makes, and each re-pull's length as a share of its first pull.
	public const int BlackSunCollapses = 3;
	public const float BlackSunRepullShare = 0.6f;

	// Nova Seed: shards a collapse throws, the size and damage of the well each opens (shares of the
	// star's), how long a seeded well pulls, and how far out a shard may land, in the star's reaches.
	public const int NovaShards = 3;
	public const float NovaSeedSize = 0.65f;
	public const float NovaSeedDamage = 0.6f;
	public const float NovaSeedPullShare = 0.6f;
	public const float NovaThrowReaches = 3f;
	private const float NovaShardSeconds = 0.35f;

	/// <summary>Twin Stars: the well this one was opened with. They drift together while they pull.</summary>
	public HollowStar Partner { get; set; }

	/// <summary>0 for a well the player cast; 1 for one a Nova Seed shard opened, which throws none.</summary>
	public int Generation { get; set; }

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

	// How long the collapse holds what it gathered where it closed (a root: Enemy.ApplySlow at 0), so
	// the knot stays a knot for the area spells beside it. 0 - nothing holds it - unless an evolution
	// carries RootDuration (no card does since 2026-10-29; Infinite Horizon's hold became Nova Seed). Set
	// by Player.OpenHollowStar.
	public float HoldSeconds { get; set; }

	// The scene's PullRadius, which Player.FireHollowStar spaces a cast's wells by before any well exists.
	public const float DefaultPullRadius = 150f;
	// Wells a single cast may open (Twin Stars' pair, with room for level bonuses).
	public const int MaxPerCast = 6;

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

	// The cards, read once from the spell in _Ready.
	private bool crush;
	private bool blackSun;
	private bool novaSeed;
	// Collapses still to come: 1, or Black Sun's three.
	private int collapsesLeft = 1;
	// How long the pull now running lasts: PullSeconds, then Black Sun's shorter re-pulls.
	private float pullDuration;
	// Bodies inside the collapse at the last pull tick, and at the collapse itself (Crush draws both).
	private int knot;
	private int crushed;
	// Twin Stars: how fast this well closes on its partner, set when the first pull starts.
	private float driftSpeed;

	// Nova Seed: where this collapse's shards land, and how long they have flown (negative: none thrown).
	private readonly Vector2[] shardTo = new Vector2[NovaShards];
	private int shardCount;
	private float shardAge = -1f;
	private bool shardsLanded;
	// The shards fly over the bodies (DrawLayers.Air); the well stays on the ground under them.
	private Node2D air;
	private static PackedScene ownScene;
	// The collapse's and the throw's broad phase (EnemyRegistry.CollectNear). One collapse at a time.
	private static readonly List<Enemy> near = new(64);

	// Preallocated: _Draw runs every frame for the whole life of the well (see BlackTentacles).
	private const int ArmCount = 3;
	private const int ArmPoints = 14;
	private readonly Vector2[][] armPoints = new Vector2[ArmCount][];
	private const int TetherPoints = 16;
	private readonly Vector2[] tether = new Vector2[TetherPoints];

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
		// Under the enemies it is dragging, so the knot reads as bodies over a hole - which z 1 never was:
		// the actors are at 0, so the well was painted over them and over the wizard. On the ground layer
		// the craters use since 2026-10-28 (over the terrain and the walkable decals).
		ZAsRelative = false;
		ZIndex = DrawLayers.Ground;
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

		if (SpellData != null)
		{
			crush = SpellData.HasEvolution(CrushId);
			blackSun = SpellData.HasEvolution(BlackSunId);
			novaSeed = Generation == 0 && SpellData.HasEvolution(NovaSeedId);
		}
		collapsesLeft = blackSun ? BlackSunCollapses : 1;
		pullDuration = PullSeconds;
		if (novaSeed)
		{
			air = new Node2D { Name = "Shards", ZAsRelative = false, ZIndex = DrawLayers.Air };
			AddChild(air);
			air.Draw += DrawShards;
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
					BeginPull(PullSeconds);
				break;

			case Phase.Pulling:
				spin += dt * 6f;
				DriftToPartner(dt);
				pullTickTimer -= dt;
				if (pullTickTimer <= 0f)
				{
					pullTickTimer = PullTickSeconds;
					Pull();
				}
				if (phaseSeconds >= pullDuration)
					Collapse();
				break;

			case Phase.Collapsing:
				spin += dt * 2f;
				if (shardAge >= 0f)
				{
					shardAge += dt;
					air?.QueueRedraw();
					if (!shardsLanded && shardAge >= NovaShardSeconds)
						LandShards();
				}
				// Held until the collapse's debris has landed and cooled; it hit on the first frame.
				if (phaseSeconds >= MathF.Max(CollapseSeconds, ImpactDebris.LifeSeconds))
				{
					// Black Sun: the star is still there, and pulls again.
					if (collapsesLeft > 0)
						BeginPull(PullSeconds * BlackSunRepullShare);
					else if (shardAge < 0f || shardsLanded)
						QueueFree();
				}
				break;
		}
	}

	private void Enter(Phase next)
	{
		phase = next;
		phaseSeconds = 0f;
	}

	private void BeginPull(float seconds)
	{
		Enter(Phase.Pulling);
		pullDuration = seconds;
		pullTickTimer = 0f;
		// Twin Stars: close on the partner at a speed that meets it as this pull ends - each well covers
		// half the gap, less a little, so the two knots end pressed together rather than on one point.
		driftSpeed = 0f;
		if (HasPartner)
		{
			float gap = GlobalPosition.DistanceTo(Partner.GlobalPosition);
			driftSpeed = MathF.Max(0f, gap * 0.5f - CollapseRadius * TwinMeetGap) / MathF.Max(0.1f, seconds);
		}
	}

	private bool HasPartner => Partner != null && IsInstanceValid(Partner) && !Partner.IsQueuedForDeletion();

	// The one of a pair that throws Nova Seed's shards and draws the tether, so a merged pair does both once.
	private bool LeadsPair => !HasPartner || GetInstanceId() < Partner.GetInstanceId();

	// Twin Stars. The well moves, so the pull's centre moves with it and the knot is dragged along -
	// toward the midpoint of the pair, never onto water or a wall (MazeNavigation.IsBlockedAt).
	private void DriftToPartner(float dt)
	{
		if (driftSpeed <= 0f || !HasPartner)
			return;
		Vector2 mid = (GlobalPosition + Partner.GlobalPosition) * 0.5f;
		Vector2 toMid = mid - GlobalPosition;
		float distance = toMid.Length();
		float stop = CollapseRadius * TwinMeetGap * 0.5f;
		if (distance <= stop)
			return;
		Vector2 next = GlobalPosition + toMid / distance * MathF.Min(driftSpeed * dt, distance - stop);
		if (MazeNavigation.Active?.IsBlockedAt(next) == true)
			return;
		GlobalPosition = next;
	}

	private void Pull()
	{
		float reachSquared = PullRadius * PullRadius;
		float knotSquared = CollapseRadius * CollapseRadius;
		int inKnot = 0;
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy enemy || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;

			Vector2 toCentre = GlobalPosition - enemy.GlobalPosition;
			float distanceSquared = toCentre.LengthSquared();
			if (distanceSquared > reachSquared)
				continue;
			if (distanceSquared <= knotSquared)
				inKnot++;

			float distance = MathF.Sqrt(distanceSquared);
			// Already in the knot. Pulling an enemy sitting on the centre only makes it jitter.
			if (distance < 6f)
				continue;

			float speed = MathF.Min(PullSpeed, distance * PullStiffness);
			enemy.ApplyKnockback(toCentre / distance * speed);
		}
		knot = inKnot;
	}

	private void Collapse()
	{
		Enter(Phase.Collapsing);
		collapsesLeft--;

		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;

		if (IsSpiritMirror)
			PullLootToPlayer();

		float radiusSquared = CollapseRadius * CollapseRadius;
		EnemyRegistry.CollectNear(GlobalPosition, CollapseRadius, near);
		int damage = Damage;
		crushed = 0;
		if (crush)
		{
			foreach (Enemy enemy in near)
			{
				if (IsInstanceValid(enemy) && !enemy.IsDying && GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition) <= radiusSquared)
					crushed++;
			}
			damage = Math.Max(1, Mathf.RoundToInt(Damage * (1f + CrushPerBody * Math.Min(crushed, CrushMostBodies))));
		}

		foreach (Enemy enemy in near)
		{
			if (!IsInstanceValid(enemy) || GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition) > radiusSquared)
				continue;

			PlayerRef.DealDamageToEnemy(enemy, damage, source: SpellData);
			// What survives the collapse is held in the knot; a boss's MinSlowMultiplier makes it a slow.
			if (HoldSeconds > 0f && IsInstanceValid(enemy) && !enemy.IsDying)
				enemy.ApplySlow(0f, HoldSeconds);
		}
		near.Clear();

		if (novaSeed && collapsesLeft <= 0 && LeadsPair)
			ThrowShards();
	}

	// Nova Seed: the nearest enemies outside the collapse, out to NovaThrowReaches reaches, each at least a
	// seeded well's reach from the others - so each shard goes to its own pack, and the pack nearest is
	// the one knotted first. Three linear scans of the broad phase, nothing sorted or allocated.
	private void ThrowShards()
	{
		float throwReach = PullRadius * NovaThrowReaches;
		float innerSquared = CollapseRadius * CollapseRadius * 1.44f;
		float outerSquared = throwReach * throwReach;
		float apartSquared = PullRadius * NovaSeedSize * PullRadius * NovaSeedSize;
		MazeNavigation nav = MazeNavigation.Active;
		EnemyRegistry.CollectNear(GlobalPosition, throwReach, near);
		shardCount = 0;
		while (shardCount < NovaShards)
		{
			Enemy best = null;
			float bestSquared = float.MaxValue;
			foreach (Enemy enemy in near)
			{
				if (!IsInstanceValid(enemy) || enemy.IsDying)
					continue;
				float d2 = GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition);
				if (d2 <= innerSquared || d2 > outerSquared || d2 >= bestSquared)
					continue;
				bool taken = false;
				for (int s = 0; s < shardCount && !taken; s++)
					taken = shardTo[s].DistanceSquaredTo(enemy.GlobalPosition) < apartSquared;
				if (taken || nav?.IsBlockedAt(enemy.GlobalPosition) == true)
					continue;
				best = enemy;
				bestSquared = d2;
			}
			if (best == null)
				break;
			shardTo[shardCount++] = best.GlobalPosition;
		}
		near.Clear();
		if (shardCount > 0)
		{
			shardAge = 0f;
			shardsLanded = false;
		}
	}

	private void LandShards()
	{
		shardsLanded = true;
		Node arena = GetParent();
		if (arena == null || SpellData == null)
			return;
		ownScene ??= GD.Load<PackedScene>(SceneFilePath);
		if (ownScene == null)
			return;
		for (int i = 0; i < shardCount; i++)
		{
			if (ownScene.Instantiate() is not HollowStar seed)
				continue;
			seed.Generation = Generation + 1;
			seed.SpellData = SpellData;
			seed.PlayerRef = PlayerRef;
			seed.Damage = Math.Max(1, Mathf.RoundToInt(Damage * NovaSeedDamage));
			seed.PullRadius = PullRadius * NovaSeedSize;
			seed.CollapseRadius = CollapseRadius * NovaSeedSize;
			seed.PullSpeed = PullSpeed;
			seed.PullSeconds = PullSeconds * NovaSeedPullShare;
			seed.OpenSeconds = OpenSeconds * 0.5f;
			seed.HoldSeconds = HoldSeconds;
			seed.Modulate = Modulate;
			arena.AddChild(seed);
			seed.GlobalPosition = shardTo[i];
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
				DrawTether(t);
				DrawCircle(Vector2.Zero, CollapseRadius * 0.3f * t, CoreColor);
				break;
			}

			case Phase.Pulling:
			{
				DrawArms(1f);
				DrawTether(1f);
				DrawMotes();
				// The core swells as the pull runs, so the moment of collapse can be anticipated - and,
				// under Crush, with every body the knot holds: a full knot is a third as wide again.
				float t = Mathf.Clamp(phaseSeconds / MathF.Max(0.01f, pullDuration), 0f, 1f);
				float core = Mathf.Lerp(CollapseRadius * 0.3f, CollapseRadius * 0.45f, t) * CrushSwell(knot);
				// Eclipse's corona: a flickering ring of light hugging the black disc, inside the
				// collapse radius - it is the well's own rim, not a mark of its reach.
				if (coronaColor.A > 0f)
				{
					float flicker = 1f + 0.06f * MathF.Sin(spin * 3.1f);
					DrawArc(Vector2.Zero, core * 1.22f * flicker, 0f, Mathf.Tau, 32, coronaColor, 3.0f);
				}
				if (blackSun)
					DrawProminences(core);
				DrawCircle(Vector2.Zero, core, CoreColor);
				DrawArc(Vector2.Zero, core, 0f, Mathf.Tau, 24, rimColor, 1.5f);
				if (blackSun)
					DrawCollapsesLeft(core);
				break;
			}

			case Phase.Collapsing:
			{
				// The core snaps shut, and what it gathered is thrown out to the collapse radius. A Black
				// Sun with collapses still to come does not go out: its core shrinks back and holds.
				float shut = 1f - Mathf.Clamp(phaseSeconds / 0.08f, 0f, 1f);
				float swell = CollapseRadius * 0.45f * CrushSwell(crushed);
				if (collapsesLeft > 0)
				{
					float core = Mathf.Lerp(CollapseRadius * 0.3f, swell, shut);
					DrawProminences(core);
					DrawCircle(Vector2.Zero, core, CoreColor);
					DrawArc(Vector2.Zero, core, 0f, Mathf.Tau, 24, rimColor, 1.5f);
					DrawCollapsesLeft(core);
				}
				else if (shut > 0f)
				{
					DrawCircle(Vector2.Zero, swell * shut, CoreColor);
				}
				// Crush throws out more of what it crushed: two more pieces a body.
				int pieces = Math.Min(48, ImpactDebris.CountFor(CollapseRadius) + 2 * Math.Min(crushed, CrushMostBodies));
				ImpactDebris.Draw(this, Vector2.Zero, CollapseRadius, phaseSeconds, debrisSeed + (uint)collapsesLeft * 31u, debrisRamp,
					pieces, Colors.White);
				break;
			}
		}
	}

	// Crush: the core is up to a third as wide again with a full knot. 1 without the card.
	private float CrushSwell(int bodies) => crush ? 1f + 0.35f * Math.Min(bodies, CrushMostBodies) / CrushMostBodies : 1f;

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

	// Twin Stars: a filament of the dark strung between the two cores, sagging and twisting, with motes
	// running along it from either end to the middle - where the knots are being dragged to. Drawn by
	// one of the pair. `grown` 0..1 strings it out from this core as the wells open.
	private void DrawTether(float grown)
	{
		if (!HasPartner || !LeadsPair)
			return;
		Vector2 to = ToLocal(Partner.GlobalPosition) * grown;
		float length = to.Length();
		if (length < 4f)
			return;
		Vector2 side = new Vector2(-to.Y, to.X) / length;
		for (int i = 0; i < TetherPoints; i++)
		{
			float s = i / (float)(TetherPoints - 1);
			float sway = MathF.Sin(s * Mathf.Pi) * (6f + 4f * MathF.Sin(spin * 0.7f))
				+ 2.5f * MathF.Sin(s * 9f - spin * 2f) * MathF.Sin(s * Mathf.Pi);
			tether[i] = to * s + side * sway;
		}
		DrawPolyline(tether, CoreColor, 5.0f);
		DrawPolyline(tether, armColor, 2.5f);
		for (int i = 0; i < 6; i++)
		{
			float p = (i * 0.37f + phaseSeconds * 0.8f) % 1f;
			// Half the motes start at each end and meet in the middle.
			float s = i % 2 == 0 ? p * 0.5f : 1f - p * 0.5f;
			int at = Mathf.Clamp((int)(s * (TetherPoints - 1)), 0, TetherPoints - 1);
			DrawRect(WhipLimb.Cell(tether[at], WhipLimb.Grid * 2f), rimColor);
		}
	}

	// Black Sun: dark flares licking off the rim of a star that does not go out, turning slowly.
	private void DrawProminences(float core)
	{
		const int flares = 7;
		for (int i = 0; i < flares; i++)
		{
			float a = i * Mathf.Tau / flares + spin * 0.25f;
			float lick = 0.25f + 0.2f * MathF.Sin(spin * 1.7f + i * 2.3f);
			var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
			var across = new Vector2(-dir.Y, dir.X);
			Vector2 foot = dir * core * 0.92f;
			Vector2 tip = dir * core * (1f + lick) + across * core * 0.12f * MathF.Sin(spin + i);
			DrawLine(foot + across * core * 0.12f, tip, armColor, 3f);
			DrawLine(foot - across * core * 0.12f, tip, armColor, 3f);
			DrawRect(WhipLimb.Cell(tip, WhipLimb.Grid), rimColor);
		}
	}

	// Black Sun: one bright mote circles the core for every collapse it has left - three, then two, then one.
	private void DrawCollapsesLeft(float core)
	{
		for (int i = 0; i < collapsesLeft; i++)
		{
			float a = spin * 0.9f + i * Mathf.Tau / BlackSunCollapses;
			DrawRect(WhipLimb.Cell(new Vector2(MathF.Cos(a), MathF.Sin(a)) * (core + 7f), WhipLimb.Grid * 2f), rimColor with { A = 1f });
		}
	}

	// Nova Seed: shards of the star flung from the collapse on a low arc to where each will open a well -
	// a dark sliver with a lit edge and a short trail, landing as it shrinks.
	private void DrawShards()
	{
		if (shardAge < 0f || shardsLanded || air == null)
			return;
		float u = Mathf.Clamp(shardAge / NovaShardSeconds, 0f, 1f);
		float eased = 1f - (1f - u) * (1f - u);
		for (int i = 0; i < shardCount; i++)
		{
			Vector2 to = ToLocal(shardTo[i]);
			float length = to.Length();
			if (length < 1f)
				continue;
			Vector2 dir = to / length;
			// Thrown up and coming down: a hop as high as a fifth of the throw, in the screen's up.
			float hop = MathF.Sin(u * Mathf.Pi) * MathF.Min(40f, length * 0.2f);
			Vector2 at = to * eased + new Vector2(0f, -hop);
			Vector2 back = at - dir * 9f + new Vector2(0f, hop * 0.15f);
			air.DrawLine(back, at, CoreColor, 4f);
			air.DrawLine(back, at, rimColor, 1.5f);
			air.DrawRect(WhipLimb.Cell(at, WhipLimb.Grid * 2f), rimColor with { A = 1f });
		}
	}
}
