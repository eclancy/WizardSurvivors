using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts;

// Obsidian Spike (the Geomancer's spell, pure Earth since the starter rework): erupts a cluster of
// black volcanic glass under the target after a short tell, dealing AoE damage at that spot.
//
// The look was redrawn 2026-10-28 (Eric: "obsidian spike icon and in game sprite doesn't look very
// cool, update"). It had been three tan triangles out of a 32 px cell, shown at x3 under a lavender
// multiply, with a puff of dark specks that never left a 20 px disc - rock, not glass, and nothing on
// screen said how far it hurt. Now, in order:
//   - THE TELL (TelegraphDuration, the same 0.22 s as before): the ground cracks at the target's feet
//     and glows, pebbles jump, and the point of the glass breaks through. Small and at the spot - the
//     thing itself coming, never the area it will hit (.ai/art-direction.md, "no area markers").
//   - THE HIT: the cluster punches up to full height (tools/art/spell_fx.py, obsidian_sheet), the cracks
//     run out across the ground to exactly the radius it hits, chips of glass are thrown out and land
//     inside it - the furthest third exactly on it - and the element's dust goes with them
//     (ImpactDebrisLayer). A wider eruption (Splinter Field, Tectonic Uprising) also brings up
//     splinters of glass where the cracks end.
//   - THE HOLD: the light runs up the ridge to a star at the point, the cracks cool down the ramp.
//   - THE COLLAPSE: the glass fractures, glowing, and falls back into the ground in pieces.
// Everything that reaches the radius is drawn out of `explosionRadius`, the number the hit tests,
// centred on the target's feet - an enemy whose feet are inside the cracks is an enemy that is hit.
// The node is never scaled (Player.FireGroundSpike drops ApplyLegendaryVisual's scale): the reach
// is drawn, so a scale would only make the drawing lie.
//
// What lies on the ground (the cracks, the chips' shadows) is drawn by a child at the ground layer the
// craters use (CraterGround.GroundZ, under every actor); what stands up or flies (the glass, the chips
// in the air) by this node, over the swarm - the spike erupts under its target, which is by definition
// inside the crowd, so under the bodies it was covered by the enemies it was hitting.
//
// Nothing is allocated per frame: the cracks, chips and splinters are laid out once, at the cast, into
// fixed arrays, and drawn from them with rects, lines and one texture.
public partial class GroundSpike : Node2D, IPaintsElementPath
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	// Seconds from the cast to the hit. 4/18 s is what the old sheet's frame 4 at 18 fps made it (it
	// erupted on that frame, well before this telegraph's old 0.5 s), kept exactly: a visual pass.
	[Export] public float TelegraphDuration { get; set; } = 4f / 18f;
	[Export] public float BaseExplosionRadius { get; set; } = 45f;
	// tools/art/spell_fx.py obsidian_sheet: 48x48 cells at x2, four rows - the cluster, a splinter, and
	// the two again lit by Darkness for Umbral Vein.
	[Export] public Texture2D Sheet { get; set; }
	// How far below the target's origin its feet are: where the glass comes out of the ground and the
	// cracks are centred. +28 is where the old spike's ground line stood.
	[Export] public float FootOffset { get; set; } = 28f;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;
	// Basalt Lance (obsidian_spike_basalt_lance): the eruption drives on through the column behind its
	// target - LanceFollowers more spikes in a line away from the caster, each a beat after the last and
	// LanceSpacing radii apart, so the needle the card describes is something the player watches run
	// through a crowd. It was +140% damage on the same round eruption until 2026-10-26, which read as
	// nothing having changed at the Ascension. Followers are spikes like any other: same damage, radius
	// and sheet, and LanceStep > 0 marks one, so a follower never starts a lance of its own.
	public const string BasaltLanceId = "obsidian_spike_basalt_lance";
	public const int LanceFollowers = 3;
	private const float LanceSpacing = 1.6f;
	private const float LanceBeatSeconds = 0.09f;
	public int LanceStep { get; set; }

	// --- PHASE 2 (.ai/spell-roster.md section 2h). Three cards that moved only numbers now each change
	// what the eruption does; their ids never moved (the Geomancer's path names two of them).
	//
	// SPLINTER FIELD (obsidian_spike_splinter_field, the Geomancer's level 4; once "35% wider"): the
	// eruption splinters through the ground - as its cracks run out, a splinter of glass punches up
	// under each of the SplinterCount nearest enemies just past its rim (out to SplinterReach radii),
	// one after another, striking what stands over it for SplinterShare of the spike.
	public const string SplinterFieldId = "obsidian_spike_splinter_field";
	public const int SplinterCount = 4;
	public const float SplinterReach = 2.0f;
	public const float SplinterShare = 0.5f;
	private const float SplinterHitShare = 0.4f;   // of the radius: a splinter's own reach round its spot
	private const float SplinterBeatSeconds = 0.035f;
	// DEEP STRATA (obsidian_spike_deep_strata; once +45% damage and a longer cast range): driven up from
	// far deeper, the spike throws everything it breaks out under up into the air (Enemy.ApplyLaunch):
	// LaunchSeconds aloft, doing nothing, and down where it went up. A miniboss, elite or boss is too
	// heavy to throw and reels instead (ApplyShock, LaunchStaggerSeconds).
	public const string DeepStrataId = "obsidian_spike_deep_strata";
	public const float LaunchSeconds = 0.6f;
	private const float LaunchHeight = 40f;
	private const float LaunchStaggerSeconds = 0.3f;
	private const int MaxLaunched = 16;
	// TECTONIC UPRISING (obsidian_spike_tectonic_uprising, the Geomancer's level 8; once +120% damage and
	// +60% area): where the spike broke out the ground stays open - a vent, its cracks smouldering - for
	// VentSeconds, and every VentBeatSeconds it throws up a fresh spike of glass under the nearest enemy
	// within VentReach radii, striking everything at that spot for VentShare of the spike.
	public const string TectonicId = "obsidian_spike_tectonic_uprising";
	public const float VentSeconds = 3f;
	public const float VentBeatSeconds = 0.5f;
	public const float VentReach = 1.6f;
	public const float VentShare = 0.6f;
	private const float VentHitShare = 0.5f;       // of the radius: a vent's spike's reach round its spot
	private const int MaxVentSpikes = 4;

	private bool splinterField;
	private bool deepStrata;
	private bool tectonic;
	// A spike thrown up by a vent or a splinter field is drawn from the sheet's splinter row, at a spot
	// from this node, from the age it broke out. Shared by both: neither card can be on the other's spot.
	private const int MaxShards = SplinterCount + MaxVentSpikes;
	private readonly Vector2[] shardAt = new Vector2[MaxShards];
	private readonly float[] shardFrom = new float[MaxShards];
	private readonly bool[] shardStruck = new bool[MaxShards];
	private readonly Enemy[] shardTarget = new Enemy[MaxShards];
	private readonly float[] shardShare = new float[MaxShards];
	private readonly float[] shardReach = new float[MaxShards];
	private int shardCount;          // splinters, laid at the eruption; vent spikes cycle after them
	private int nextVentShard;
	private float nextVentAt;
	private readonly Enemy[] launched = new Enemy[MaxLaunched];
	private static readonly System.Collections.Generic.List<Enemy> nearBuffer = new(64);

	// --- the sheet ---------------------------------------------------------------------------------
	private const float CellPx = 96f;           // a 48 px cell at x2
	private const float AnchorX = 50f;          // the cluster's foot, across the cell (art x 25)
	private const float GroundY = 88f;          // the ground line, down the cell (art y 44)
	private const int ClusterFrames = 12;
	private const int SplinterFrames = 7;
	private const int DarkRows = 2;             // rows 2 and 3 are rows 0 and 1 lit by Darkness
	// The cluster's frames before the hit end on these shares of the tell; after it, each is held
	// FrameSeconds (spell_fx.CLUSTER_FRAMES: 3 the hit, 4-6 the glint, 7 the fracture, 8-11 the fall).
	private static readonly float[] TellFrameEnds = { 0.45f, 0.72f, 1f };
	private const float FrameSeconds = 0.05f;
	private const float SplinterFrameSeconds = 0.045f;

	// --- the eruption's reach ----------------------------------------------------------------------
	// The cracks run from the feet to the radius in this long after the hit, and splinters come up as
	// the front reaches them.
	private const float CrackRunSeconds = 0.08f;
	// How long after the hit the cracks hold their heat, take to cool down the ramp, and then to fade.
	private const float HotSeconds = 0.1f;
	private const float CoolSeconds = 0.3f;
	private const float FadeSeconds = 0.14f;
	private const int MaxCracks = 9;
	private const int MaxForks = 5;
	private const int CrackPoints = 8;          // per crack or fork, mouth first
	private const int MaxChips = 18;
	private const float ChipFlightSeconds = 0.18f;
	private const float ChipRestSeconds = 0.32f; // after the hit, when the landed chips start to go
	private const int MaxSplinters = 5;
	// Splinters only once the eruption is wider than the cluster drawn on the sheet (whose splayed
	// blades already stand out to about 38 px): at the base radius they would stand inside it.
	private const float SplinterRadiusShare = 1.2f;

	private int damage = 6;
	private float explosionRadius = 45f;
	private float age;
	private bool erupted;
	private float lifeSeconds = 0.7f;
	private Node2D fissures;

	// The look of this cast, decided once in CastAt.
	private ElementColors.Ramp ramp = ElementColors.GetRamp(Element.Earth);
	private int rowOffset;
	private Color castTint = Colors.White;  // what ApplyLegendaryVisual asked for, passed to followers
	private Color glassTint = Colors.White;
	private Color glowTint = Colors.White;

	// Laid out once per cast. A crack is CrackPoints points from the mouth out; crackSegment is the
	// length of each of its bends, so the running front can be followed down it. A fork starts partway
	// along its crack (crackStart, that distance from the feet) and is drawn the same way.
	private readonly Vector2[] crackPoints = new Vector2[(MaxCracks + MaxForks) * CrackPoints];
	private readonly float[] crackStart = new float[MaxCracks + MaxForks];
	private readonly float[] crackSegment = new float[MaxCracks + MaxForks];
	private int crackCount;
	private int forkCount;
	private readonly Vector2[] chipDir = new Vector2[MaxChips];
	private readonly float[] chipReach = new float[MaxChips];
	private readonly float[] chipHop = new float[MaxChips];
	private readonly float[] chipDelay = new float[MaxChips];
	private readonly byte[] chipSize = new byte[MaxChips];
	private int chipCount;
	private readonly Vector2[] splinterAt = new Vector2[MaxSplinters];
	private readonly float[] splinterDelay = new float[MaxSplinters];
	private int splinterCount;
	private uint rng = 1u;

	// The glass's own colours (art-direction section 3, the violet row and bone hi) for the chips.
	private static readonly Color GlassHi = new Color(0x6E6296FF);
	private static readonly Color GlassLit = new Color(0x4E4470FF);
	private static readonly Color GlassShade = new Color(0x221C33FF);
	private static readonly Color Spec = new Color(0xEEF2FAFF);
	private static readonly Color Occ = new Color(0x05070CFF);

	public override void _Ready()
	{
		RefreshComputedStats();
		fissures = new Node2D { Name = "Fissures", ZIndex = CraterGround.GroundZ, ZAsRelative = false };
		AddChild(fissures);
		fissures.Draw += DrawFissures;
	}

	public override void _ExitTree()
	{
		if (fissures != null && IsInstanceValid(fissures))
			fissures.Draw -= DrawFissures;
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	public void CastAt(Vector2 position)
	{
		GlobalPosition = position;
		age = 0f;
		erupted = false;

		// The evolution's tint comes in as a modulate (Player.ApplyLegendaryVisual). Laid over black
		// glass and a white-hot core as a multiply, the dark ones (Tectonic Uprising's 0.36) took the
		// glass to a silhouette and the glow to mud, so it is taken off the node and kept as a hue: half
		// of it on the glass, a third on the light (CraterLayer's softened fire tint).
		castTint = Modulate;
		Modulate = Colors.White;
		float peak = MathF.Max(castTint.R, MathF.Max(castTint.G, castTint.B));
		Color hue = peak > 0.01f ? new Color(castTint.R / peak, castTint.G / peak, castTint.B / peak) : Colors.White;
		glassTint = Colors.White.Lerp(hue, 0.5f);
		glowTint = Colors.White.Lerp(hue, 0.3f);

		// The element path repaints itself (IPaintsElementPath): Umbral Vein's light is Darkness's.
		// Read once per cast - GetBranchElement walks Godot dictionaries.
		Element? branch = SpellData?.GetBranchElement();
		ramp = ElementColors.GetRamp(branch ?? Element.Earth);
		rowOffset = branch == Element.Darkness ? DarkRows : 0;

		LayOut();
		QueueRedraw();
		fissures?.QueueRedraw();
	}

	public override void _Process(double delta)
	{
		age += (float)delta;
		if (!erupted && age >= TelegraphDuration)
			Erupt();
		if (erupted)
			TickShards();
		if (age >= lifeSeconds)
		{
			QueueFree();
			return;
		}
		QueueRedraw();
		fissures.QueueRedraw();
	}

	private void Erupt()
	{
		if (erupted)
			return;
		erupted = true;

		// The group, like every AoE (CLAUDE.md) - see the same fix in ConeBlast. CurrentScene's children
		// are the enemies only when the run IS the scene, so under the balance harness this hit nothing.
		var player = PlayerRef as Player;
		int thrown = 0;
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Node2D e || GlobalPosition.DistanceTo(e.GlobalPosition) > explosionRadius)
				continue;
			player?.DealDamageToEnemy(e, damage, source: SpellData);
			// Deep Strata: what it broke out under goes up with it.
			if (deepStrata && e is Enemy body && IsInstanceValid(body) && !body.IsDying)
			{
				if (body.IsMiniBoss)
					body.ApplyShock(LaunchStaggerSeconds);
				else if (!body.IsAirborne)
				{
					body.ApplyLaunch(LaunchHeight, LaunchSeconds);
					if (thrown < MaxLaunched)
						launched[thrown++] = body;
				}
			}
		}

		// The element's dust, thrown out to exactly the radius it hit (the shared burst, art-direction
		// section 3), from the feet the cracks run from.
		if (GetParent() is Node2D arena)
			ImpactDebrisLayer.Spawn(arena, GlobalPosition + new Vector2(0f, FootOffset), explosionRadius, ramp, glowTint);

		if (splinterField)
			LaySplinters();
		if (tectonic)
			nextVentAt = age + VentBeatSeconds;

		if (LanceStep == 0 && SpellData != null && SpellData.HasEvolution(BasaltLanceId))
			DriveLance();
	}

	// Splinter Field: the nearest enemies past the rim, each with a splinter of glass coming up under it
	// a beat after the last, as the cracks reach out.
	private void LaySplinters()
	{
		float reach = explosionRadius * SplinterReach;
		EnemyRegistry.CollectNear(GlobalPosition, reach, nearBuffer);
		shardCount = 0;
		// Nearest first, one at a time: SplinterCount passes over a short list, no sort and no allocation.
		float floor = explosionRadius;
		for (int n = 0; n < SplinterCount; n++)
		{
			Enemy best = null;
			float bestD = reach;
			foreach (Enemy e in nearBuffer)
			{
				if (e == null || !IsInstanceValid(e) || e.IsDying)
					continue;
				float d = GlobalPosition.DistanceTo(e.GlobalPosition);
				if (d > floor && d < bestD && Array.IndexOf(shardTarget, e, 0, shardCount) < 0)
				{
					bestD = d;
					best = e;
				}
			}
			if (best == null)
				break;
			shardTarget[shardCount] = best;
			shardAt[shardCount] = best.GlobalPosition - GlobalPosition;
			shardFrom[shardCount] = age + CrackRunSeconds + SplinterBeatSeconds * shardCount;
			shardStruck[shardCount] = false;
			shardShare[shardCount] = SplinterShare;
			shardReach[shardCount] = explosionRadius * SplinterHitShare;
			shardCount++;
		}
		nearBuffer.Clear();
		nextVentShard = shardCount;
	}

	// The splinters and the vent's spikes: each strikes when it breaks the surface, under the enemy it
	// was laid for if that one is still standing (it walks a step in a beat), and a vent picks its next
	// enemy on its beat.
	private void TickShards()
	{
		var player = PlayerRef as Player;
		for (int i = 0; i < MaxShards; i++)
		{
			if (shardStruck[i] || shardFrom[i] <= 0f || age < shardFrom[i])
				continue;
			shardStruck[i] = true;
			Enemy target = shardTarget[i];
			if (target != null && IsInstanceValid(target) && !target.IsDying)
				shardAt[i] = target.GlobalPosition - GlobalPosition;
			shardTarget[i] = null;
			Vector2 at = GlobalPosition + shardAt[i];
			int hit = Math.Max(1, Mathf.RoundToInt(damage * shardShare[i]));
			float r2 = shardReach[i] * shardReach[i];
			foreach (Node node in EnemyRegistry.Snapshot)
			{
				if (node is Node2D e && e.GlobalPosition.DistanceSquaredTo(at) <= r2)
					player?.DealDamageToEnemy(e, hit, source: SpellData);
			}
		}

		// Tectonic Uprising: the vent's beat, while it is open.
		if (tectonic && age >= nextVentAt && age < TelegraphDuration + VentSeconds)
		{
			nextVentAt += VentBeatSeconds;
			float reach = explosionRadius * VentReach;
			EnemyRegistry.CollectNear(GlobalPosition, reach, nearBuffer);
			Enemy best = null;
			float bestD = reach * reach;
			foreach (Enemy e in nearBuffer)
			{
				if (e == null || !IsInstanceValid(e) || e.IsDying)
					continue;
				float d = GlobalPosition.DistanceSquaredTo(e.GlobalPosition);
				if (d < bestD)
				{
					bestD = d;
					best = e;
				}
			}
			nearBuffer.Clear();
			if (best != null)
			{
				int slot = nextVentShard;
				nextVentShard = shardCount + (nextVentShard - shardCount + 1) % MaxVentSpikes;
				shardTarget[slot] = best;
				shardAt[slot] = best.GlobalPosition - GlobalPosition;
				shardFrom[slot] = age + 0.06f;
				shardStruck[slot] = false;
				shardShare[slot] = VentShare;
				shardReach[slot] = explosionRadius * VentHitShare;
			}
		}
	}

	private void DriveLance()
	{
		if (PlayerRef == null || !IsInstanceValid(PlayerRef) || GetParent() is not Node parent || string.IsNullOrEmpty(SceneFilePath))
			return;
		Vector2 heading = GlobalPosition - PlayerRef.GlobalPosition;
		heading = heading.LengthSquared() > 0.01f ? heading.Normalized() : Vector2.Right;
		var scene = ResourceLoader.Load<PackedScene>(SceneFilePath);
		if (scene == null)
			return;
		Vector2 origin = GlobalPosition;
		// Captured now: the first spike frees itself when its sheet ends, before the last follower lands.
		SpellData spell = SpellData;
		Node2D caster = PlayerRef;
		float damageMultiplier = DamageMultiplier, areaMultiplier = AreaMultiplier;
		int level = CurrentLevel;
		Vector2 scale = Scale;
		// The tint the cast came with, not this node's modulate, which CastAt has already cleared.
		Color modulate = castTint;
		for (int step = 1; step <= LanceFollowers; step++)
		{
			Vector2 at = origin + heading * explosionRadius * LanceSpacing * step;
			int captured = step;
			var timer = GetTree().CreateTimer(LanceBeatSeconds * step);
			timer.Timeout += () =>
			{
				if (!IsInstanceValid(parent) || !IsInstanceValid(caster))
					return;
				var follower = scene.Instantiate<Node2D>();
				if (follower is GroundSpike spike)
				{
					spike.SpellData = spell;
					spike.DamageMultiplier = damageMultiplier;
					spike.AreaMultiplier = areaMultiplier;
					spike.LanceStep = captured;
					spike.SetSpellLevel(level);
					spike.PlayerRef = caster;
					spike.Scale = scale;
					spike.Modulate = modulate;
				}
				parent.AddChild(follower);
				(follower as GroundSpike)?.CastAt(at);
			};
		}
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 6) * DamageMultiplier));
		explosionRadius = MathF.Max(4f, BaseExplosionRadius * AreaMultiplier);
		splinterField = SpellData?.HasEvolution(SplinterFieldId) ?? false;
		deepStrata = SpellData?.HasEvolution(DeepStrataId) ?? false;
		tectonic = SpellData?.HasEvolution(TectonicId) ?? false;
	}

	// --- layout, once per cast -----------------------------------------------------------------------

	private void LayOut()
	{
		float r = explosionRadius;
		// Its own generator, seeded from the node: the layout must not draw on Godot's global one, or
		// every Geomancer cast would shift what a seeded balance-harness run deals and spawns after it.
		rng = (uint)(GetInstanceId() * 2654435761UL) | 1u;

		// Cracks: more of them on a wider eruption, evenly round at a random turn, each a jagged path
		// whose last point lies exactly on the radius and whose bends never pass it.
		crackCount = Math.Clamp(Mathf.RoundToInt(r / 9f), 5, MaxCracks);
		forkCount = 0;
		float turn = NextRandom() * Mathf.Tau;
		for (int k = 0; k < crackCount; k++)
		{
			float a = turn + (k + (NextRandom() - 0.5f) * 0.5f) * Mathf.Tau / crackCount;
			var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
			LayCrack(k, Vector2.Zero, dir, r, 0f, r * 0.13f);
			// Every other crack forks halfway out; the fork ends well inside the radius.
			if (k % 2 == 1 && forkCount < MaxForks)
			{
				int at = CrackPoints / 2;
				Vector2 from = crackPoints[k * CrackPoints + at];
				float side = NextRandom() < 0.5f ? -1f : 1f;
				Vector2 forkDir = dir.Rotated(side * (0.5f + NextRandom() * 0.3f));
				float length = MathF.Min(r * 0.32f, r - from.Length() - 4f);
				if (length > 8f)
				{
					LayCrack(MaxCracks + forkCount, from, forkDir, length, crackStart[k] + crackSegment[k] * at, length * 0.12f);
					forkCount++;
				}
			}
		}

		// Chips of glass: the front third land on the radius (their far edge on it), the rest short of it.
		chipCount = Math.Clamp(Mathf.RoundToInt(r / 5f), 8, MaxChips);
		for (int i = 0; i < chipCount; i++)
		{
			float a = (i + NextRandom() * 0.8f) * Mathf.Tau / chipCount;
			chipDir[i] = new Vector2(MathF.Cos(a), MathF.Sin(a));
			chipSize[i] = (byte)(NextRandom() < 0.4f ? 6 : 4);
			chipReach[i] = i % 3 == 0 ? r - chipSize[i] * 0.5f : r * (0.35f + 0.5f * NextRandom());
			chipHop[i] = 10f + NextRandom() * 18f;
			chipDelay[i] = NextRandom() * 0.03f;
		}

		// Splinters at the ends of every other crack, once the eruption outgrows the cluster.
		splinterCount = 0;
		if (r > BaseExplosionRadius * SplinterRadiusShare)
		{
			for (int k = 0; k < crackCount && splinterCount < MaxSplinters; k += 2)
			{
				Vector2 end = crackPoints[k * CrackPoints + CrackPoints - 1];
				splinterAt[splinterCount] = end * 0.84f;
				splinterDelay[splinterCount] = TelegraphDuration + CrackRunSeconds * 0.84f + NextRandom() * 0.03f;
				splinterCount++;
			}
		}

		lifeSeconds = TelegraphDuration + MathF.Max(MathF.Max(FrameSeconds * (ClusterFrames - 3), HotSeconds + CoolSeconds + FadeSeconds),
			ImpactDebris.LifeSeconds);
		// Phase 2: the node outlives its eruption for whatever it set going - the last splinter's rise,
		// the thrown bodies' fall (their shadows are drawn here), the vent's last spike and its cooling.
		float shardSeconds = SplinterFrames * SplinterFrameSeconds;
		if (splinterField)
			lifeSeconds = MathF.Max(lifeSeconds, TelegraphDuration + CrackRunSeconds + SplinterBeatSeconds * SplinterCount + shardSeconds);
		if (deepStrata)
			lifeSeconds = MathF.Max(lifeSeconds, TelegraphDuration + LaunchSeconds + 0.05f);
		if (tectonic)
			lifeSeconds = MathF.Max(lifeSeconds, TelegraphDuration + VentSeconds + 0.06f + shardSeconds + FadeSeconds);
	}

	// One crack (or fork) of CrackPoints points from `from` along `dir` for `length`, each bend pushed
	// sideways by up to `jitter`; the last point is exactly `length` out along `dir` from `from`.
	private void LayCrack(int index, Vector2 from, Vector2 dir, float length, float startDistance, float jitter)
	{
		Vector2 side = new Vector2(-dir.Y, dir.X);
		int baseIndex = index * CrackPoints;
		crackPoints[baseIndex] = from;
		for (int j = 1; j < CrackPoints; j++)
		{
			float along = length * j / (CrackPoints - 1);
			float sway = j == CrackPoints - 1 ? 0f : (NextRandom() - 0.5f) * 2f * jitter;
			crackPoints[baseIndex + j] = from + dir * along + side * sway;
		}
		crackStart[index] = startDistance;
		crackSegment[index] = length / (CrackPoints - 1);
	}

	// --- drawing -------------------------------------------------------------------------------------

	public override void _Draw()
	{
		if (Sheet == null)
			return;
		Vector2 foot = new Vector2(0f, FootOffset);
		float sinceHit = age - TelegraphDuration;

		// The splinters behind the cluster (north of its foot), the cluster, then the ones in front.
		for (int i = 0; i < splinterCount; i++)
			if (splinterAt[i].Y < 0f)
				DrawSplinter(i, foot);
		int frame = ClusterFrame();
		if (frame >= 0)
			DrawSheetFrame(foot, frame, rowOffset);
		for (int i = 0; i < splinterCount; i++)
			if (splinterAt[i].Y >= 0f)
				DrawSplinter(i, foot);

		// Splinter Field's splinters and the vent's spikes, each at the feet of what it came up under.
		for (int i = 0; i < MaxShards; i++)
		{
			if (shardFrom[i] <= 0f || age < shardFrom[i])
				continue;
			int shardFrame = (int)((age - shardFrom[i]) / SplinterFrameSeconds);
			if (shardFrame < SplinterFrames)
				DrawSheetFrame(foot + shardAt[i], shardFrame, 1 + rowOffset);
		}

		// The chips in the air, and lying where they landed until they go.
		if (sinceHit >= 0f)
		{
			float fade = Mathf.Clamp(1f - (sinceHit - ChipRestSeconds) / FadeSeconds, 0f, 1f);
			for (int i = 0; i < chipCount && fade > 0f; i++)
				DrawChip(i, foot, sinceHit - chipDelay[i], fade);
		}
	}

	private int ClusterFrame()
	{
		if (age < TelegraphDuration)
		{
			float share = age / MathF.Max(0.001f, TelegraphDuration);
			for (int i = 0; i < TellFrameEnds.Length; i++)
				if (share < TellFrameEnds[i])
					return i;
			return TellFrameEnds.Length - 1;
		}
		int after = 3 + (int)((age - TelegraphDuration) / FrameSeconds);
		return after < ClusterFrames ? after : -1;
	}

	private void DrawSheetFrame(Vector2 foot, int frame, int row)
	{
		var src = new Rect2(frame * CellPx, row * CellPx, CellPx, CellPx);
		var dst = new Rect2(MathF.Round(foot.X - AnchorX), MathF.Round(foot.Y - GroundY), CellPx, CellPx);
		DrawTextureRectRegion(Sheet, dst, src, glassTint);
	}

	private void DrawSplinter(int i, Vector2 foot)
	{
		float since = age - splinterDelay[i];
		if (since < 0f)
			return;
		int frame = (int)(since / SplinterFrameSeconds);
		if (frame >= SplinterFrames)
			return;
		DrawSheetFrame(foot + splinterAt[i], frame, 1 + rowOffset);
	}

	private void DrawChip(int i, Vector2 foot, float since, float fade)
	{
		if (since <= 0f)
			return;
		float u = Mathf.Clamp(since / ChipFlightSeconds, 0f, 1f);
		float eased = 1f - (1f - u) * (1f - u) * (1f - u);
		Vector2 ground = foot + chipDir[i] * chipReach[i] * eased;
		// Thrown up out of the ground and back down onto it: a hop, highest a third of the way out.
		float lift = chipHop[i] * 4f * u * (1f - u) * (1f - u * 0.4f);
		Vector2 at = ground - new Vector2(0f, lift);
		float size = chipSize[i];
		// Tumbling while it flies: whether it shows its lit face or its edge to the key changes every few
		// frames, and now and then the edge flashes - the glint that says glass.
		int face = u < 1f ? (int)(since / 0.04f + i) & 3 : 0;
		Color lit = (u < 1f && face == 1 ? Spec : face == 3 ? GlassLit : GlassHi) * glassTint;
		Color dark = (face == 2 ? GlassShade : GlassLit) * glassTint;
		lit.A = fade;
		dark.A = fade;
		Rect2 body = WhipLimb.Cell(at, size);
		DrawRect(body, dark);
		DrawRect(new Rect2(body.Position, new Vector2(WhipLimb.Grid, WhipLimb.Grid)), lit);
	}

	// The ground layer (CraterGround.GroundZ): the cracks, and the chips' shadows under them in flight.
	private void DrawFissures()
	{
		Vector2 foot = new Vector2(0f, FootOffset);
		if (age < TelegraphDuration)
		{
			// The tell: the ground starting to split at the feet, the first few cracks a short way out
			// and brightening as the glass comes - small, and at the spot.
			float share = age / MathF.Max(0.001f, TelegraphDuration);
			float reach = MathF.Min(explosionRadius * 0.3f, 6f + 10f * share);
			int shown = Math.Min(crackCount, 2 + (int)(share * 3f));
			for (int k = 0; k < shown; k++)
				DrawCrack(k, foot, reach, share < 0.5f ? 2 : 1, 1f, false);
			return;
		}

		float sinceHit = age - TelegraphDuration;
		float front = explosionRadius * Ease(sinceHit / CrackRunSeconds);
		float cool = Mathf.Clamp((sinceHit - HotSeconds) / CoolSeconds, 0f, 1f);
		float alpha = Mathf.Clamp(1f - (sinceHit - HotSeconds - CoolSeconds) / FadeSeconds, 0f, 1f);
		int shift = (int)(cool * 3f);
		DrawLaunchedShadows();
		// Tectonic Uprising: the vent's cracks do not go out - they smoulder at the ramp's mid, throbbing,
		// until it closes, and then fade as the eruption's did.
		if (tectonic && cool >= 1f)
		{
			float ventLeft = VentSeconds - sinceHit;
			float smoulder = (0.55f + 0.25f * MathF.Sin(age * 9f)) * Mathf.Clamp((ventLeft + FadeSeconds) / FadeSeconds, 0f, 1f);
			if (smoulder > alpha)
			{
				alpha = smoulder;
				shift = 1;
			}
		}
		if (alpha <= 0f)
			return;
		for (int k = 0; k < crackCount; k++)
			DrawCrack(k, foot, front, shift, alpha, true);
		for (int f = 0; f < forkCount; f++)
			DrawCrack(MaxCracks + f, foot, front, shift, alpha, true);

		// A chip in the air throws a speck of shadow on the ground under it (the flyer's detached occ).
		for (int i = 0; i < chipCount; i++)
		{
			float since = sinceHit - chipDelay[i];
			if (since <= 0f || since >= ChipFlightSeconds)
				continue;
			float eased = Ease(since / ChipFlightSeconds);
			Vector2 ground = foot + chipDir[i] * chipReach[i] * eased;
			fissures.DrawRect(WhipLimb.Cell(ground, WhipLimb.Grid * 2f), new Color(Occ, 0.55f));
		}
	}

	// Deep Strata: a body thrown up leaves its shadow on the ground under it - smaller and fainter the
	// higher it is - which is what says it is in the air and not simply drawn higher.
	private void DrawLaunchedShadows()
	{
		if (!deepStrata)
			return;
		float g = WhipLimb.Grid;
		for (int i = 0; i < MaxLaunched; i++)
		{
			Enemy e = launched[i];
			if (e == null)
				break;
			if (!IsInstanceValid(e) || e.IsDying || !e.IsAirborne)
				continue;
			Transform2D xf = e.GlobalTransform;
			Vector2 feet = xf.Origin + new Vector2(0f, e.StatusFxGeometry.FeetY * xf.X.Length()) - GlobalPosition;
			float lift = e.AirborneLift / LaunchHeight;
			float half = MathF.Round((12f - 5f * lift) / g) * g;
			var shade = new Color(Occ, 0.6f - 0.25f * lift);
			fissures.DrawRect(new Rect2(feet.X - half, feet.Y - g, half * 2f, g * 2f), shade);
			fissures.DrawRect(new Rect2(feet.X - half + g * 2f, feet.Y - g * 2f, half * 2f - g * 4f, g * 4f), shade);
		}
	}

	// One crack drawn out to `front` (a distance from the feet): white-hot to hot near the mouth, mid,
	// then edge toward its end, every stop pushed `shift` further down the ramp as it cools. It is two
	// flat strokes - a wide one a stop cooler, the glow in the ground either side of the split, and the
	// split itself over it - widest at the mouth and thinning to a single art pixel at the radius. Near
	// the mouth it has a lip of occlusion under it, so it reads as the ground opened, not a line on it.
	private void DrawCrack(int index, Vector2 foot, float front, int shift, float alpha, bool lip)
	{
		int baseIndex = index * CrackPoints;
		float segment = crackSegment[index];
		float start = crackStart[index];
		if (front <= start)
			return;
		float total = explosionRadius;
		for (int j = 0; j < CrackPoints - 1; j++)
		{
			float d0 = start + segment * j;
			if (d0 >= front)
				break;
			Vector2 a = foot + crackPoints[baseIndex + j];
			Vector2 b = foot + crackPoints[baseIndex + j + 1];
			float d1 = d0 + segment;
			if (d1 > front)
			{
				b = a.Lerp(b, (front - d0) / segment);
				d1 = front;
			}
			float share = (d0 + d1) * 0.5f / total;
			// Hot most of the way out: the cluster stands over the inner third, so what shows of a crack
			// is its outer run, and an outer run in the ramp's edge was a brown line on a teal floor.
			int stop = Math.Min(3, (share < 0.22f ? 0 : share < 0.58f ? 1 : share < 0.88f ? 2 : 3) + shift);
			float wide = share < 0.5f ? WhipLimb.Grid * 3f : share < 0.9f ? WhipLimb.Grid * 2f : 0f;
			if (lip && share < 0.45f)
				fissures.DrawLine(a + new Vector2(0f, WhipLimb.Grid * 1.5f), b + new Vector2(0f, WhipLimb.Grid * 1.5f), new Color(Occ, alpha * 0.8f), WhipLimb.Grid * 2f);
			if (wide > 0f)
				fissures.DrawLine(a, b, Stop(Math.Min(3, stop + 1), alpha), wide);
			fissures.DrawLine(a, b, Stop(stop, alpha), share < 0.25f ? WhipLimb.Grid * 1.5f : WhipLimb.Grid);
		}
	}

	private Color Stop(int stop, float alpha)
	{
		Color c = (stop == 0 ? ramp.Core : stop == 1 ? ramp.Hot : stop == 2 ? ramp.Mid : ramp.Edge) * glowTint;
		c.A = alpha;
		return c;
	}

	// xorshift32 to 0..1: the layout's dice (see LayOut).
	private float NextRandom()
	{
		rng ^= rng << 13;
		rng ^= rng >> 17;
		rng ^= rng << 5;
		return (rng & 0xFFFFFF) / 16777216f;
	}

	private static float Ease(float u)
	{
		u = Mathf.Clamp(u, 0f, 1f);
		return 1f - (1f - u) * (1f - u) * (1f - u);
	}
}
