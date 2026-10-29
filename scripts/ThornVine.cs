using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Thorned Vine (id thorn_vine, Grass + Poison). A thorned vine bursts out of the ground beside the
// densest pack in range, coils, lashes across the pack in one wide sweep, and withers back into the
// ground.
//
// "thorn vine looks ridiculous - it should be a whip animation that grows from the ground and then
// slaps enemies in an area, like a tentacle. rename it 'Thorned Vine'." It was a homing bolt with a
// green sprite on it. The display name changed; the id and every save key are still thorn_vine.
//
// **What it hits, and in what order.** The sweep reaches enemies in the order its line passes over
// them, and it cuts through pierce + 1 of them (BasePierce plus the spell's Pierce) before it SNAGS
// on the last one and stops there. That keeps the pierce the bolt had meaningful - "Pierce +1" is
// still one more enemy a cast - and keeps the payload what it was: the bolt hit up to that many in
// its line, the lash hits up to that many in its arc. Every one of them is poisoned as before.
//
// **Drawn where it hurts.** The sweep is tested against the vine's own laid-out tip each frame
// (WhipLimb): an enemy is struck when the line from the root to the drawn tip has swept past it and
// it is within the drawn length, plus BodyTolerance for the body it is standing in. On a snag the
// vine stops on the enemy it stopped on. Nothing is struck that the vine was not drawn across.
//
// **Not Black Tentacles, and not Bramble Seed.** All three come out of the ground. Black Tentacles is
// a dark rift that lingers, slows, and lashes single targets with many limbs; Bramble Seed is a
// patch of roots that grows where it is sown and hurts by standing. This is ONE green limb, alive
// for a second, whose verb is the sweep. It shares only the limb (WhipLimb) with the tentacles.
//
// Pixel art: stamped onto the 2 px art grid by WhipLimb, in the Grass ramp with bark at the root,
// pale thorns, leaves, and poison at the tip and where it strikes. Nothing allocates per frame.
//
// **Its own five milestones (2026-10-29, .ai/spell-roster.md section 2e).** It wore the default
// archetype's five names - Amplified Impact, Hyper Acceleration, Reactive Catalyst, Titan Cataclysm,
// Infinite Horizon - and each was a number on the same lash. Each id now names something the vine
// DOES, read here by id (SpellData.HasEvolution); the ids are unchanged, so saves and the Ascension
// browser carry over:
//   - Barbed Lash: the thorns break off in what it cuts, and a barbed enemy tears itself open as it
//     walks, leaving a trail of sap (ThornBarbs).
//   - Snapback: the whip cracks out and back - a second stroke over the same arc, the other way.
//   - Entangle: what it cuts is rooted where it stands, the vine coiled round its feet.
//   - Thicket: the swept arc is left a band of thorny brambles on the ground (ThornThicket).
//   - Hydra Vine: one root rises in the thick of the pack and splits into three heads, each lashing a
//     third of the ring round it (Player.FireThornVine, LaunchHead).
public partial class ThornVine : Node2D
{
	public const string BarbedLashId = "thorn_vine_amplified_impact";
	public const string SnapbackId = "thorn_vine_hyper_acceleration";
	public const string EntangleId = "thorn_vine_reactive_catalyst";
	public const string ThicketId = "thorn_vine_titan_cataclysm";
	public const string HydraVineId = "thorn_vine_infinite_horizon";

	/// <summary>Entangle: how long what the vine cuts is rooted.</summary>
	public const float EntangleSeconds = 0.6f;
	/// <summary>Hydra Vine: heads a root splits into, and the share of the ring each one lashes.</summary>
	public const int HydraHeads = 3;
	private const float HydraHeadSweepDegrees = 130f;
	/// <summary>Hydra Vine: a head reaches this much farther than a lone vine, from the middle of the pack.</summary>
	public const float HydraReach = 1.2f;

	[Export] public float VineLength { get; set; } = 125f;
	[Export] public float SweepDegrees { get; set; } = 170f;
	[Export] public int BasePierce { get; set; } = 3;
	[Export] public int PoisonDamagePerTick { get; set; } = 2;
	[Export] public float PoisonDuration { get; set; } = 3.0f;
	// An enemy is a body, not a point: struck when the vine's line comes this close to its centre.
	[Export] public float BodyTolerance { get; set; } = 12f;

	public SpellData SpellData { get; set; }
	public int CurrentLevel { get; set; } = 1;
	public float DamageMultiplier { get; set; } = 1f;
	// The player's area times the spell's evolutions' (Player.FireThornVine).
	public float AreaMultiplier { get; set; } = 1f;
	public Player PlayerRef { get; set; }

	// Recoil is Snapback's: the vine drawn back off the snag before it cracks the other way. Done is
	// a withered vine that has nothing left to draw but Entangle's coils, kept until the last root ends.
	private enum Stage { Grow, Coil, Lash, Hold, Recoil, Wither, Done }

	private const float GrowSeconds = 0.22f;
	private const float CoilSeconds = 0.14f;
	private const float LashSeconds = 0.20f;
	private const float HoldSeconds = 0.14f;
	private const float RecoilSeconds = 0.10f;
	private const float WitherSeconds = 0.35f;
	private const int MaxStrikes = 32;
	// Entangle's coils, one per enemy rooted, drawn at its feet until the root ends.
	private const int MaxCoils = 24;

	private int damage = 3;
	private float length = 100f;
	private int maxHits = 4;
	private int poisonTick = 2;
	private float poisonSeconds = 3f;
	// Any SpeedMultiplier a milestone grants: the vine grows and lashes faster. None of its own five
	// carries one since 2026-10-29 (Hyper Acceleration's 1.25 became Snapback's second stroke).
	private float tempo = 1f;
	// The milestones that change what the vine does (read by id in SetSpellLevel).
	private bool barbed;
	private bool snapback;
	private bool entangle;
	private bool thicket;
	// 0 for the first stroke, 1 for Snapback's return.
	private int stroke;
	// The arc the first stroke swept, for Thicket: where it started, which way, and how far.
	private float firstStart;
	private float firstSide;
	private float firstSwept;

	private Stage stage = Stage.Grow;
	private float stageAge;
	private float age;
	// Unscaled seconds since launch: a root is real time, whatever the vine's tempo.
	private float realAge;
	private float startAngle;
	private float sweep;
	private float side = 1f;
	private float swept;
	private bool snagged;
	private float snagProgress;
	private int hits;
	private float poseAngle;
	private float poseLength;
	private float seed;
	private bool launched;

	private readonly WhipLimb limb = new WhipLimb();
	private readonly List<Enemy> candidates = new List<Enemy>();
	private readonly List<bool> struck = new List<bool>();
	private readonly Vector2[] strikeAt = new Vector2[MaxStrikes];
	private readonly float[] strikeTime = new float[MaxStrikes];
	private int strikeCount;
	private readonly Enemy[] coiled = new Enemy[MaxCoils];
	private readonly float[] coilUntil = new float[MaxCoils];
	private int coilCount;
	private float coilsEnd;

	// The Grass ramp, Earth for bark and soil, Poison for what it leaves in a wound
	// (.ai/art-direction.md section 3), and the dry brown Bramble Seed withers to.
	private static readonly Color Occ = new Color(0.020f, 0.027f, 0.047f, 1f);
	private static readonly Color Bark = new Color(0.478f, 0.314f, 0.141f, 1f);      // earth edge #7A5024
	private static readonly Color Clod = new Color(0.788f, 0.604f, 0.369f, 1f);      // earth mid #C99A5E
	private static readonly Color GrassEdge = new Color(0.290f, 0.549f, 0.227f, 1f); // grass edge #4A8C3A
	private static readonly Color GrassMid = new Color(0.608f, 0.831f, 0.333f, 1f);  // grass mid #9BD455
	private static readonly Color GrassHot = new Color(0.812f, 0.933f, 0.580f, 1f);  // grass hot #CFEE94
	private static readonly Color PoisonMid = new Color(0.706f, 0.878f, 0.290f, 1f); // poison mid #B4E04A
	private static readonly Color PoisonHot = new Color(0.863f, 0.957f, 0.549f, 1f); // poison hot #DCF48C
	private static readonly Color Dry = new Color(0.36f, 0.27f, 0.19f, 1f);
	private static readonly Color GoldLit = new Color(0.847f, 0.690f, 0.290f, 1f);

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 3) * DamageMultiplier));
		float reachBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel) ?? 0f;
		length = MathF.Max(30f, (VineLength + reachBonus) * AreaMultiplier);
		maxHits = 1 + Math.Max(0, BasePierce + (SpellData?.GetPierceAtLevel(CurrentLevel) ?? 0));
		poisonTick = Math.Max(1, PoisonDamagePerTick + Mathf.RoundToInt(SpellData?.GetEffectValueAtLevel(SpellEffect.DotDamage, CurrentLevel) ?? 0f));
		poisonSeconds = MathF.Max(0.1f, PoisonDuration + (SpellData?.GetEffectValueAtLevel(SpellEffect.ZoneDuration, CurrentLevel) ?? 0f));
		tempo = MathF.Max(0.25f, SpellData?.GetSpeedMultiplier() ?? 1f);
		barbed = SpellData?.HasEvolution(BarbedLashId) ?? false;
		snapback = SpellData?.HasEvolution(SnapbackId) ?? false;
		entangle = SpellData?.HasEvolution(EntangleId) ?? false;
		thicket = SpellData?.HasEvolution(ThicketId) ?? false;
	}

	/// <summary>
	/// Plants the vine on <paramref name="caster"/>'s side of <paramref name="knot"/>, half its
	/// length out from it, aimed to sweep across it.
	/// </summary>
	public void Launch(Vector2 caster, Vector2 knot)
	{
		Vector2 toKnot = knot - caster;
		float distance = toKnot.Length();
		Vector2 heading = distance > 1f ? toKnot / distance : Vector2.Right;
		float rootDistance = MathF.Min(length * 0.5f, distance * 0.6f);
		Vector2 root = knot - heading * rootDistance;
		// A vine comes out of the ground, never out of a tarn or a wall (.ai/terrain.md): where the
		// spot between the wizard and the pack is blocked, it comes up under the pack instead.
		if (MazeNavigation.Active?.IsBlockedAt(root) == true)
			root = knot;
		Begin(root, heading.Angle(), Mathf.DegToRad(SweepDegrees));
	}

	/// <summary>
	/// Hydra Vine: one head of a root at <paramref name="root"/> (in the thick of the pack), lashing a
	/// third of the ring round it, centred on <paramref name="heading"/> (radians). Its heads share the
	/// root, so they come up as one plant.
	/// </summary>
	public void LaunchHead(Vector2 root, float heading)
	{
		length *= HydraReach;
		Begin(root, heading, Mathf.DegToRad(HydraHeadSweepDegrees));
	}

	private void Begin(Vector2 root, float heading, float sweepRadians)
	{
		// This spell draws its own forms at the size it hits; an evolution's ScaleMultiplier would
		// only grow the drawing, and its tint is a stand-in for spells without art. A legendary roll
		// gilds the thorns instead.
		Scale = Vector2.One;
		Modulate = Colors.White;
		GlobalPosition = new Vector2(MathF.Round(root.X / WhipLimb.Grid) * WhipLimb.Grid, MathF.Round(root.Y / WhipLimb.Grid) * WhipLimb.Grid);

		var rng = new RandomNumberGenerator { Seed = GetInstanceId() };
		seed = rng.Randf() * Mathf.Tau;
		side = rng.Randf() < 0.5f ? -1f : 1f;
		sweep = sweepRadians;
		startAngle = heading - side * sweep * 0.5f;
		poseAngle = startAngle;
		launched = true;
		// The audio manifest's proposed trigger: something arrives out of the ground.
		SfxPlayer.AtPosition(SfxCatalog.SpellSummon, GlobalPosition);
	}

	public override void _Ready()
	{
		// Grows out of the floor, but lashes over the bodies it hits; under the status marks (3).
		ZIndex = 1;
	}

	public override void _Process(double delta)
	{
		if (!launched)
			return;
		float dt = (float)delta * tempo;
		age += dt;
		stageAge += dt;
		realAge += (float)delta;
		switch (stage)
		{
			case Stage.Grow:
				if (stageAge >= GrowSeconds) Enter(Stage.Coil);
				break;
			case Stage.Coil:
				if (stageAge >= CoilSeconds) { Enter(Stage.Lash); CollectCandidates(); }
				break;
			case Stage.Lash:
				if (stageAge >= LashSeconds) Enter(Stage.Hold);
				break;
			case Stage.Hold:
				if (stageAge >= HoldSeconds)
				{
					if (stroke == 0)
						EndFirstStroke();
					if (snapback && stroke == 0)
						BeginReturnStroke();
					else
						Enter(Stage.Wither);
				}
				break;
			case Stage.Recoil:
				if (stageAge >= RecoilSeconds) { Enter(Stage.Lash); CollectCandidates(); }
				break;
			case Stage.Wither:
				if (stageAge >= WitherSeconds) Enter(Stage.Done);
				break;
			case Stage.Done:
				// Nothing of the vine is left but the coils round what it rooted.
				if (realAge >= coilsEnd) { QueueFree(); return; }
				break;
		}
		Pose();
		if (stage == Stage.Lash)
			Sweep();
		QueueRedraw();
	}

	// The first stroke is over: remember the arc it swept, and leave Thicket's brambles on it.
	private void EndFirstStroke()
	{
		firstStart = startAngle;
		firstSide = side;
		firstSwept = snagged ? snagProgress : sweep;
		if (!thicket || PlayerRef == null || !IsInstanceValid(PlayerRef) || GetParent() is not Node2D arena)
			return;
		ThornThicket.Lay(arena, GlobalPosition, length, firstStart, firstSide, firstSwept, damage, poisonTick, poisonSeconds, SpellData, PlayerRef);
	}

	// Snapback: the vine is drawn back off what it snagged on, and cracks back across the same arc the
	// other way - a fresh stroke, cutting through as many again.
	private void BeginReturnStroke()
	{
		float end = startAngle + side * (snagged ? snagProgress : sweep);
		sweep = snagged ? snagProgress : sweep;
		startAngle = end;
		side = -side;
		swept = 0f;
		snagged = false;
		snagProgress = 0f;
		hits = 0;
		stroke = 1;
		Enter(Stage.Recoil);
	}

	private void Enter(Stage next)
	{
		stage = next;
		stageAge = 0f;
	}

	// Lays the vine out for this frame.
	private void Pose()
	{
		float u;
		float angle, len, curl, waveAmp;
		float wave = age * 9f + seed;
		switch (stage)
		{
			case Stage.Grow:
				// Bursts out of the ground wound back past where the sweep will start.
				u = Mathf.Clamp(stageAge / GrowSeconds, 0f, 1f);
				angle = startAngle - side * 0.35f;
				len = length * 0.55f * EaseOutBack(u);
				curl = side * 1.2f * (1f - u) - side * 0.6f * u;
				waveAmp = 0.5f;
				break;
			case Stage.Coil:
				// Draws back and coils: the wind-up the player can read.
				u = Smooth(stageAge / CoilSeconds);
				angle = startAngle - side * (0.35f + 0.2f * u);
				len = length * (0.55f + 0.05f * u);
				curl = Mathf.Lerp(-side * 0.6f, -side * 1.5f, u);
				waveAmp = 0.15f;
				break;
			case Stage.Lash:
				// The whip: slow out of the coil, fast through the middle, reaching full length in
				// the first third. Stops dead where it snags.
				u = Mathf.Clamp(stageAge / LashSeconds, 0f, 1f);
				float progress = snagged ? snagProgress : sweep * Smooth(u);
				angle = startAngle + side * progress;
				len = length * Mathf.Min(1f, 0.6f + 1.2f * u);
				curl = snagged ? 0f : -side * 0.3f * MathF.Sin(Mathf.Pi * u) * (1f - u);
				waveAmp = snagged ? 0f : 0.1f * (1f - u);
				break;
			case Stage.Hold:
				angle = startAngle + side * (snagged ? snagProgress : sweep);
				len = length;
				curl = 0.06f * MathF.Sin(age * 40f);
				waveAmp = 0.05f;
				break;
			case Stage.Recoil:
				// Snapback: torn off the snag and drawn back, curling against the way it is about to crack.
				u = Smooth(stageAge / RecoilSeconds);
				angle = startAngle - side * 0.18f * u;
				len = length * (1f - 0.12f * u);
				curl = -side * 0.9f * u;
				waveAmp = 0.12f;
				break;
			default:
				// Wither: droops, dries and is drawn back into the ground.
				u = Smooth(stageAge / WitherSeconds);
				angle = startAngle + side * (snagged ? snagProgress : sweep);
				len = length * (1f - u);
				curl = side * 0.9f * u;
				waveAmp = 0.2f * u;
				break;
		}
		poseAngle = angle;
		poseLength = len;
		limb.Layout(Vector2.Zero, angle, len, curl, waveAmp, wave);
	}

	private void CollectCandidates()
	{
		candidates.Clear();
		struck.Clear();
		float reachSquared = (length + BodyTolerance) * (length + BodyTolerance);
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy enemy || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			if (GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition) > reachSquared)
				continue;
			candidates.Add(enemy);
			struck.Add(false);
		}
	}

	// How far along the sweep `angle` is, in radians from its start, in the direction it travels.
	private float Progress(float angle) => side * Mathf.Wrap(angle - startAngle, -Mathf.Pi, Mathf.Pi);

	// Strikes, in the order the line reaches them, everything the drawn vine has swept over.
	private void Sweep()
	{
		if (snagged)
			return;
		Vector2 tip = limb.Tip;
		if (tip.LengthSquared() > 1f)
			swept = MathF.Max(swept, Progress(tip.Angle()));
		float reach = tip.Length();

		while (hits < maxHits)
		{
			int next = -1;
			float nextProgress = float.MaxValue;
			for (int k = 0; k < candidates.Count; k++)
			{
				Enemy e = candidates[k];
				if (struck[k] || !IsInstanceValid(e) || e.IsDying)
					continue;
				Vector2 offset = e.GlobalPosition - GlobalPosition;
				float distance = offset.Length();
				if (distance > reach + BodyTolerance || distance < 1f)
					continue;
				float slack = MathF.Atan2(BodyTolerance, distance);
				float q = Progress(offset.Angle());
				if (q < -slack || q > swept + slack)
					continue;
				if (q < nextProgress)
				{
					nextProgress = q;
					next = k;
				}
			}
			if (next < 0)
				return;

			struck[next] = true;
			Strike(candidates[next]);
			hits++;
			if (hits >= maxHits)
			{
				// Snagged: the vine stops on the last thing it cut through.
				snagged = true;
				snagProgress = Mathf.Clamp(nextProgress, 0f, sweep);
				return;
			}
		}
	}

	private void Strike(Enemy enemy)
	{
		if (strikeCount < MaxStrikes)
		{
			strikeAt[strikeCount] = enemy.GlobalPosition - GlobalPosition;
			strikeTime[strikeCount] = age;
			strikeCount++;
		}
		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;
		PlayerRef.DealDamageToEnemy(enemy, damage, source: SpellData);
		if (!IsInstanceValid(enemy) || enemy.IsDying)
			return;
		enemy.ApplyPoison(poisonTick, poisonSeconds);
		// Barbed Lash: a thorn breaks off in the wound, and tears it as the enemy walks on.
		if (barbed && GetParent() is Node2D arena)
			ThornBarbs.For(arena).Barb(enemy, damage, SpellData, PlayerRef);
		// Entangle: rooted where it was cut (a boss's slow floor makes it a hard slow), the vine wound
		// round its feet for as long as the root holds.
		if (entangle)
		{
			enemy.ApplySlow(0f, EntangleSeconds);
			if (coilCount < MaxCoils)
			{
				coiled[coilCount] = enemy;
				coilUntil[coilCount++] = realAge + EntangleSeconds;
			}
			coilsEnd = MathF.Max(coilsEnd, realAge + EntangleSeconds);
		}
	}

	// --- drawing ---------------------------------------------------------------------------

	public override void _Draw()
	{
		if (!launched)
			return;
		DrawCoils();
		if (stage == Stage.Done)
			return;
		float dry = stage == Stage.Wither ? Smooth(stageAge / WitherSeconds) : 0f;
		bool gilded = SpellData?.IsLegendary ?? false;

		DrawSoil(dry);
		DrawArc(dry);

		if (poseLength >= WhipLimb.Grid * 2f)
		{
			float baseWidth = WhipLimb.SnapSize(Mathf.Clamp(length * 0.11f, 8f, 14f));
			limb.Draw(this, baseWidth, WhipLimb.Grid, Occ, Bark.Lerp(Dry, dry), GrassEdge.Lerp(Dry, dry), GrassMid.Lerp(Dry, dry));
			DrawThornsAndLeaves(baseWidth, dry, gilded);
			if (stage == Stage.Lash || stage == Stage.Hold)
				DrawRect(WhipLimb.Cell(limb.Tip, WhipLimb.Grid), PoisonHot);
		}

		DrawStrikes();
	}

	// Torn soil where it came up, and clods thrown out while it grows.
	private void DrawSoil(float dry)
	{
		float fade = 1f - dry;
		DrawRect(WhipLimb.Cell(Vector2.Zero, WhipLimb.Grid * 5f), new Color(Occ, fade));
		DrawRect(WhipLimb.Cell(Vector2.Zero + new Vector2(-WhipLimb.Grid, -WhipLimb.Grid), WhipLimb.Grid), new Color(Clod, fade));
		DrawRect(WhipLimb.Cell(Vector2.Zero + new Vector2(WhipLimb.Grid * 2f, 0f), WhipLimb.Grid), new Color(Bark, fade));
		if (stage != Stage.Grow)
			return;
		float u = Mathf.Clamp(stageAge / GrowSeconds, 0f, 1f);
		for (int k = 0; k < 7; k++)
		{
			float a = seed + k * 0.9f;
			float d = length * 0.28f * MathF.Sqrt(u) * (0.5f + 0.5f * MathF.Sin(seed * 3f + k));
			DrawRect(WhipLimb.Cell(new Vector2(MathF.Cos(a), MathF.Sin(a)) * d, WhipLimb.Grid), k % 2 == 0 ? Clod : Bark);
		}
	}

	// The whip arc: where the tip has swept, left hanging in the air for a moment after.
	private void DrawArc(float dry)
	{
		float fade;
		if (stage == Stage.Lash) fade = 1f;
		else if (stage == Stage.Hold) fade = 1f - stageAge / HoldSeconds;
		else return;
		if (swept <= 0f || fade <= 0f)
			return;
		float radius = MathF.Min(poseLength, length);
		float stepAngle = (WhipLimb.Grid * 2f) / MathF.Max(8f, radius);
		Color tone = new Color(GrassHot, 0.85f * fade);
		for (float q = 0f; q <= swept; q += stepAngle)
		{
			float a = startAngle + side * q;
			DrawRect(WhipLimb.Cell(new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius, WhipLimb.Grid), tone);
		}
	}

	// Pale thorns raked back toward the root, alternating sides, and three leaves.
	private void DrawThornsAndLeaves(float baseWidth, float dry, bool gilded)
	{
		Color thorn = (gilded ? GoldLit : GrassHot).Lerp(Dry, dry);
		int k = 0;
		for (float s = 0.12f; s < 0.92f; s += 0.075f, k++)
		{
			Vector2 dir = limb.DirectionAt(s);
			float sideSign = k % 2 == 0 ? 1f : -1f;
			Vector2 normal = new Vector2(-dir.Y, dir.X) * sideSign;
			float w = Mathf.Lerp(baseWidth, WhipLimb.Grid, s);
			Vector2 edge = limb.At(s) + normal * (w * 0.5f + WhipLimb.Grid * 0.5f);
			DrawRect(WhipLimb.Cell(edge, WhipLimb.Grid), thorn);
			DrawRect(WhipLimb.Cell(edge + normal * WhipLimb.Grid - dir * WhipLimb.Grid, WhipLimb.Grid), thorn);
		}

		for (int leaf = 0; leaf < 3; leaf++)
		{
			float s = 0.28f + leaf * 0.22f;
			Vector2 dir = limb.DirectionAt(s);
			float sideSign = leaf % 2 == 0 ? -1f : 1f;
			Vector2 normal = new Vector2(-dir.Y, dir.X) * sideSign;
			float w = Mathf.Lerp(baseWidth, WhipLimb.Grid, s);
			Vector2 stem = limb.At(s) + normal * (w * 0.5f + WhipLimb.Grid);
			Color leafColour = GrassMid.Lerp(Dry, dry);
			DrawRect(WhipLimb.Cell(stem, WhipLimb.Grid), GrassEdge.Lerp(Dry, dry));
			DrawRect(WhipLimb.Cell(stem + normal * WhipLimb.Grid + dir * WhipLimb.Grid, WhipLimb.Grid * 2f), leafColour);
			DrawRect(WhipLimb.Cell(stem + normal * WhipLimb.Grid * 2.5f + dir * WhipLimb.Grid * 2.5f, WhipLimb.Grid), leafColour);
		}
	}

	// Entangle: a loop of vine wound round the feet of everything rooted, growing round in the first
	// tenth of a second, thorns out, and drying and slackening in the last.
	private void DrawCoils()
	{
		for (int k = 0; k < coilCount; k++)
		{
			Enemy enemy = coiled[k];
			if (enemy == null || !IsInstanceValid(enemy) || enemy.IsDying || realAge >= coilUntil[k])
				continue;
			float since = realAge - (coilUntil[k] - EntangleSeconds);
			float grow = Mathf.Clamp(since / 0.1f, 0f, 1f);
			float dry = Mathf.Clamp((since - (EntangleSeconds - 0.12f)) / 0.12f, 0f, 1f);
			StatusFxGeometry body = enemy.StatusFxGeometry;
			Vector2 feet = ToLocal(enemy.GlobalPosition) + new Vector2(0f, body.FeetY - 2f);
			float rx = MathF.Max(10f, body.HalfWidth * 0.95f);
			float ry = MathF.Max(4f, rx * 0.38f);
			float turn = Mathf.Tau * grow;
			int steps = Math.Max(12, (int)(rx * 1.6f));
			Color stem = GrassEdge.Lerp(Dry, dry);
			Color lit = GrassMid.Lerp(Dry, dry);
			for (int pass = 0; pass < 2; pass++)
			{
				for (int i = 0; i <= steps; i++)
				{
					float a = seed + turn * i / steps;
					Vector2 at = feet + new Vector2(MathF.Cos(a) * rx, MathF.Sin(a) * ry);
					if (pass == 0)
						DrawRect(WhipLimb.Cell(at, WhipLimb.Grid * 3f), Occ);
					else
						DrawRect(WhipLimb.Cell(at, WhipLimb.Grid), MathF.Sin(a) < 0f ? lit : stem);
				}
			}
			if (grow < 1f)
				continue;
			// Three thorns raked outward, and the tendril's end curling up the shin.
			Color thorn = GrassHot.Lerp(Dry, dry);
			for (int t = 0; t < 3; t++)
			{
				float a = seed + 1.1f + t * 2.1f;
				Vector2 rim = feet + new Vector2(MathF.Cos(a) * (rx + WhipLimb.Grid * 1.5f), MathF.Sin(a) * (ry + WhipLimb.Grid));
				DrawRect(WhipLimb.Cell(rim, WhipLimb.Grid), thorn);
			}
			Vector2 tip = feet + new Vector2(MathF.Cos(seed) * rx, MathF.Sin(seed) * ry);
			DrawRect(WhipLimb.Cell(tip + new Vector2(0f, -WhipLimb.Grid * 2f), WhipLimb.Grid), lit);
			DrawRect(WhipLimb.Cell(tip + new Vector2(WhipLimb.Grid, -WhipLimb.Grid * 4f), WhipLimb.Grid), lit);
		}
	}

	// A splash of poison where each enemy was cut, fading over a fifth of a second.
	private void DrawStrikes()
	{
		for (int k = 0; k < strikeCount; k++)
		{
			float since = age - strikeTime[k];
			if (since > 0.2f)
				continue;
			float u = since / 0.2f;
			Color c = new Color(u < 0.4f ? PoisonHot : PoisonMid, 1f - u);
			Vector2 at = strikeAt[k];
			float spread = WhipLimb.Grid * (1f + 2f * u);
			DrawRect(WhipLimb.Cell(at, WhipLimb.Grid * 2f), new Color(GrassHot, 1f - u));
			DrawRect(WhipLimb.Cell(at + new Vector2(spread, -spread), WhipLimb.Grid), c);
			DrawRect(WhipLimb.Cell(at + new Vector2(-spread, -spread * 0.5f), WhipLimb.Grid), c);
			DrawRect(WhipLimb.Cell(at + new Vector2(spread * 0.4f, spread), WhipLimb.Grid), c);
		}
	}

	private static float Smooth(float x)
	{
		x = Mathf.Clamp(x, 0f, 1f);
		return x * x * (3f - 2f * x);
	}

	private static float EaseOutBack(float x)
	{
		x = Mathf.Clamp(x, 0f, 1f);
		const float c = 1.9f;
		float y = x - 1f;
		return 1f + (c + 1f) * y * y * y + c * y * y;
	}
}
