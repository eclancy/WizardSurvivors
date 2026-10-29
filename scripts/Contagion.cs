using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// Contagion: a plague put on one enemy that jumps to everything near it when it dies
// (.ai/side-events.md). Kept by the lantern-tender in the Blighted Swamp.
//
// **The cadence nothing else has: it spreads on death, by itself.** The cast only ever touches one
// enemy. Everything after that is the swarm infecting itself - a carrier that dies in a pack seeds
// the whole pack, each of those seeds its neighbours, and the plague walks through the crowd for as
// long as the crowd stays packed. Against stragglers it does almost nothing, which is the point:
// it is a spell about density, where Frost Shard's on-kill burst is a spell about killing.
//
// **Pure Poison.** Poison had no pure spell, so it could never reach its capstone.
//
// Bounded twice, because a chain reaction in a swarm is unbounded otherwise: a cast can infect at
// most MaxInfected enemies in total, and a mark lapses after MarkSeconds if its carrier survives.
//
// **Marsh Fever** (the level 4 element path into Water, ElementPaths): the marks and the bursts of
// a jump are repainted in Water's ramp, and every infection drags its carrier (the rider, applied by
// Player.DealDamageToEnemy on the infecting hit).
//
// **Its own four cards** (2026-10-29, .ai/spell-roster.md section 2e), each a new way the plague
// moves, drawn in the air over the bodies it reaches. The ids are the template's.
//   - Fever Burst (level 4): a carrier that dies bursts - everything in the jump is hit, carrier or not
//     - and leaves a fever cloud hanging where it fell for FeverCloudSeconds that infects whatever
//     walks into it. (The proposal's "dosing all near it" was what a death already did; the cloud and
//     the hit are what is new.)
//   - Outbreak (level 4): the first carrier bursts the moment it is infected, so the plague is in the
//     pack at once - and still jumps again when that carrier dies. (The proposal's name, Quickening,
//     is Gale Blade's card; this is its behaviour under its own name.)
//   - Pandemic (level 8): every PandemicSeconds, every carrier on the field bursts at once without
//     dying - one beat for all of them (a shared clock) - and each burst infects and hits what is round it.
//   - Plague Wind (level 8): each cast also looses a cloud of the plague that drifts out from the
//     wizard through the crowd and on past it, infecting everything it passes. It stops at a maze wall.
public partial class Contagion : Node2D, IPaintsElementPath
{
	public const string FeverBurstId = "contagion_amplified_impact";
	public const string OutbreakId = "contagion_hyper_acceleration";
	public const string PandemicId = "contagion_titan_cataclysm";
	public const string PlagueWindId = "contagion_infinite_horizon";

	// Fever Burst: the hit a dying carrier's burst deals everything in its jump (a share of the plague's
	// damage), and the cloud it leaves: how long it hangs, and how wide it is as a share of the jump.
	public const float FeverHit = 0.8f;
	public const float FeverCloudSeconds = 1.5f;
	public const float FeverCloudShare = 0.6f;

	// Outbreak: the cast's burst reaches this share of a jump - the carrier's own pack, not the next one.
	public const float OutbreakShare = 0.6f;

	// Pandemic: the beat, and the hit each burst deals. One hit per enemy per beat however many carriers
	// stand round it, so a beat costs at most one hit an enemy on the field.
	public const float PandemicSeconds = 2.0f;
	public const float PandemicHit = 1.0f;

	// Plague Wind: how fast the cloud drifts, for how long, and how wide it is as a share of the jump.
	public const float WindSpeed = 85f;
	public const float WindSeconds = 5f;
	public const float WindShare = 0.7f;

	[Export] public float SpreadRadius { get; set; } = 95f;
	[Export] public float MarkSeconds { get; set; } = 6f;
	[Export] public int MaxInfected { get; set; } = 24;

	public int Damage { get; set; } = 6;
	public SpellData SpellData { get; set; }
	public Player PlayerRef { get; set; }

	// How much faster the poison runs: each tick bites PoisonRate times harder and the poison runs out
	// PoisonRate times sooner, so the dose is the same and a carrier dies sooner - which is what makes
	// the plague spread sooner. The mark, and so the spread on death, is unchanged. 1 unless a card's
	// Speed says otherwise (none does since 2026-10-29).
	public float PoisonRate { get; set; } = 1f;

	// Carriers one cast may infect, each with its own plague.
	public const int MaxCarriersPerCast = 6;

	private struct Mark
	{
		public Enemy Enemy;
		public Vector2 LastSeen;
		public float Remaining;
	}

	private readonly List<Mark> marks = new();
	private readonly List<Vector2> pendingSpreads = new();
	private readonly List<(Vector2 At, float Age, uint Seed, int Count, float Reach)> bursts = new();
	private readonly List<(Vector2 At, float Age)> fevers = new();
	private uint burstSeed;
	private readonly HashSet<ulong> everInfected = new();
	private float pollTimer;

	// The cards, read once from the spell in _Ready.
	private bool fever;
	private bool pandemic;

	// Plague Wind's cloud: where it is, which way it drifts, and how long it has left (0: none).
	private Vector2 windAt;
	private Vector2 windDir;
	private float windLeft;

	// Pandemic's beat, shared by every plague on the field so they burst together: one clock, advanced
	// once a frame by whichever plague runs first, and the beat each plague last answered.
	private static double pandemicClock;
	private static ulong pandemicClockFrame;
	private static long pandemicHitBeat = -1;
	private static readonly HashSet<ulong> pandemicHitThisBeat = new();
	private long answeredBeat = -1;

	// The broad phase for a jump. One poll at a time.
	private static readonly List<Enemy> near = new(64);

	// Poison's hot tone for the carrier's mark, and its ramp for the spores a jump throws; an element
	// path repaints both in its own.
	private Color plague = new Color(0.62f, 0.86f, 0.30f, 0.9f);
	private ElementColors.Ramp spores = ElementColors.GetRamp(Element.Poison);

	// The spores a jump throws are in the air, over the bodies they land on; the carriers' marks are on
	// the floor at their feet, under them (DrawLayers). One node at z 1 drew both over every body. The
	// fever clouds and the plague wind are in the air too.
	private Node2D air;

	public override void _Ready()
	{
		ZAsRelative = false;
		ZIndex = DrawLayers.GroundTell;
		air = new Node2D { Name = "Spores", ZAsRelative = false, ZIndex = DrawLayers.Air };
		AddChild(air);
		air.Draw += DrawSpores;
		if (SpellData?.GetBranchElement() is Element branch)
		{
			ElementColors.Ramp ramp = ElementColors.GetRamp(branch);
			plague = new Color(ramp.Hot, 0.9f);
			spores = ramp;
		}
		if (SpellData != null)
		{
			fever = SpellData.HasEvolution(FeverBurstId);
			pandemic = SpellData.HasEvolution(PandemicId);
		}
		if (pandemic)
			answeredBeat = (long)(pandemicClock / PandemicSeconds);
	}

	/// <summary>
	/// Infects the first carrier. Call once, after adding to the tree. Under Outbreak the carrier bursts
	/// at once, as if it had died, and keeps its mark to burst again when it does.
	/// </summary>
	public void InfectFirst(Enemy carrier)
	{
		Infect(carrier);
		if (SpellData?.HasEvolution(OutbreakId) == true && carrier != null && IsInstanceValid(carrier) && !carrier.IsDying)
			Burst(carrier.GlobalPosition, 0f, ImpactDebris.CountFor(SpreadRadius * OutbreakShare), reach: SpreadRadius * OutbreakShare);
	}

	/// <summary>
	/// Plague Wind: a cloud of this plague set drifting from <paramref name="from"/> along
	/// <paramref name="direction"/>. What it passes is infected into this plague, inside its MaxInfected.
	/// </summary>
	public void LooseWind(Vector2 from, Vector2 direction)
	{
		windAt = from;
		windDir = direction.LengthSquared() > 0.0001f ? direction.Normalized() : Vector2.Right;
		windLeft = WindSeconds;
	}

	private void Infect(Enemy enemy)
	{
		if (enemy == null || !IsInstanceValid(enemy) || enemy.IsDying || everInfected.Count >= MaxInfected)
			return;
		if (!everInfected.Add(enemy.GetInstanceId()))
			return;

		marks.Add(new Mark { Enemy = enemy, LastSeen = enemy.GlobalPosition, Remaining = MarkSeconds });
		// The poison carries the damage over time; the hit on infection is what credits the spell
		// in the per-spell damage table and makes the jump visible as a number.
		float rate = MathF.Max(0.25f, PoisonRate);
		enemy.ApplyPoison(Math.Max(1, Mathf.RoundToInt(Damage * rate)), MarkSeconds / rate);
		if (PlayerRef != null && IsInstanceValid(PlayerRef))
			PlayerRef.DealDamageToEnemy(enemy, Damage, source: SpellData);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		QueueRedraw();
		air?.QueueRedraw();

		for (int i = bursts.Count - 1; i >= 0; i--)
		{
			var b = bursts[i];
			b.Age += dt;
			if (b.Age > ImpactDebris.LifeSeconds) bursts.RemoveAt(i); else bursts[i] = b;
		}
		for (int i = fevers.Count - 1; i >= 0; i--)
		{
			var f = fevers[i];
			f.Age += dt;
			if (f.Age > FeverCloudSeconds) fevers.RemoveAt(i); else fevers[i] = f;
		}

		if (windLeft > 0f)
		{
			windLeft -= dt;
			Vector2 next = windAt + windDir * WindSpeed * dt;
			// A wall stops a shot, and it stops the wind: the cloud breaks up against it.
			if (MazeNavigation.Active?.IsWallAt(next) == true)
				windLeft = MathF.Min(windLeft, 0.3f);
			else
				windAt = next;
		}

		if (pandemic)
			Pulse(dt);

		pollTimer -= dt;
		if (pollTimer <= 0f)
		{
			pollTimer = 0.1f;
			Poll(0.1f);
		}

		if (marks.Count == 0 && pendingSpreads.Count == 0 && bursts.Count == 0 && fevers.Count == 0 && windLeft <= 0f)
			QueueFree();
	}

	// Pandemic: the shared clock moves on once a frame, and on each new beat every carrier of this
	// plague bursts where it stands.
	private void Pulse(float dt)
	{
		ulong frame = Engine.GetProcessFrames();
		if (pandemicClockFrame != frame)
		{
			pandemicClockFrame = frame;
			pandemicClock += dt;
		}
		long beat = (long)(pandemicClock / PandemicSeconds);
		if (beat == answeredBeat)
			return;
		answeredBeat = beat;
		if (pandemicHitBeat != beat)
		{
			pandemicHitBeat = beat;
			pandemicHitThisBeat.Clear();
		}
		for (int i = marks.Count - 1; i >= 0; i--)
		{
			Enemy carrier = marks[i].Enemy;
			if (carrier != null && IsInstanceValid(carrier) && !carrier.IsDying)
				Burst(carrier.GlobalPosition, PandemicHit, 12, pandemicHitThisBeat);
		}
	}

	// How far into the beat the field is, 0..1 - the marks swell over its last fifth, the tell.
	private static float BeatPhase => (float)(pandemicClock % PandemicSeconds / PandemicSeconds);

	private void Poll(float step)
	{
		pendingSpreads.Clear();
		for (int i = marks.Count - 1; i >= 0; i--)
		{
			Mark mark = marks[i];
			bool dead = mark.Enemy == null || !IsInstanceValid(mark.Enemy) || mark.Enemy.IsDying;
			if (dead)
			{
				pendingSpreads.Add(mark.LastSeen);
				marks.RemoveAt(i);
				continue;
			}

			mark.LastSeen = mark.Enemy.GlobalPosition;
			mark.Remaining -= step;
			if (mark.Remaining <= 0f)
				marks.RemoveAt(i);
			else
				marks[i] = mark;
		}

		foreach (Vector2 origin in pendingSpreads)
		{
			Burst(origin, fever ? FeverHit : 0f, ImpactDebris.CountFor(SpreadRadius));
			if (fever)
				fevers.Add((origin, 0f));
		}
		pendingSpreads.Clear();

		// What walks into a fever cloud, or that the wind drifts over, catches the plague.
		foreach (var cloud in fevers)
			InfectWithin(cloud.At, SpreadRadius * FeverCloudShare);
		if (windLeft > 0f)
			InfectWithin(windAt, SpreadRadius * WindShare);
	}

	// A jump from `origin`: spores thrown out to exactly its reach (SpreadRadius unless `reach` says
	// otherwise), everything inside infected, and - with `hitShare` above 0 - hit for that share of the
	// plague's damage, carrier or not. A hit set passed in (Pandemic's beat) lets each enemy take one
	// such hit however many bursts reach it.
	private void Burst(Vector2 origin, float hitShare, int particles, HashSet<ulong> hitOnce = null, float reach = -1f)
	{
		float radius = reach > 0f ? reach : SpreadRadius;
		bursts.Add((origin, 0f, ++burstSeed + (uint)(GetInstanceId() % 9973), particles, radius));
		float radiusSquared = radius * radius;
		int hit = hitShare > 0f ? Math.Max(1, Mathf.RoundToInt(Damage * hitShare)) : 0;
		EnemyRegistry.CollectNear(origin, radius, near);
		foreach (Enemy neighbour in near)
		{
			if (!IsInstanceValid(neighbour) || neighbour.IsDying || origin.DistanceSquaredTo(neighbour.GlobalPosition) > radiusSquared)
				continue;
			Infect(neighbour);
			if (hit > 0 && PlayerRef != null && IsInstanceValid(PlayerRef) && IsInstanceValid(neighbour) && !neighbour.IsDying
				&& (hitOnce == null || hitOnce.Add(neighbour.GetInstanceId())))
				PlayerRef.DealDamageToEnemy(neighbour, hit, source: SpellData);
		}
		near.Clear();
	}

	private void InfectWithin(Vector2 at, float radius)
	{
		if (everInfected.Count >= MaxInfected)
			return;
		float radiusSquared = radius * radius;
		EnemyRegistry.CollectNear(at, radius, near);
		foreach (Enemy enemy in near)
		{
			if (IsInstanceValid(enemy) && at.DistanceSquaredTo(enemy.GlobalPosition) <= radiusSquared)
				Infect(enemy);
		}
		near.Clear();
	}

	public override void _Draw()
	{
		// Each carrier's mark at its feet: a status on one enemy, not an area, so it stays. Under
		// Pandemic the marks swell together before each beat - the tell that every carrier is about to go.
		float swell = 1f;
		if (pandemic)
		{
			float phase = BeatPhase;
			swell = phase > 0.8f ? 1f + 0.6f * (phase - 0.8f) / 0.2f : 1f;
		}
		foreach (Mark mark in marks)
		{
			Vector2 p = ToLocal(mark.LastSeen);
			DrawArc(p + new Vector2(0f, 10f), 9f * swell, 0f, Mathf.Tau, 16, plague, swell > 1f ? 3f : 2f);
		}
	}

	// A jump: plague spores thrown out of the corpse to exactly SpreadRadius, the reach that was
	// infected. It was a ring opening to that radius - an area marker - and went (2026-10-23). Over it,
	// Fever Burst's clouds and the plague wind: puffs of the plague's own ramp, drifting and thinning.
	private void DrawSpores()
	{
		foreach (var burst in bursts)
			ImpactDebris.Draw(air, ToLocal(burst.At), burst.Reach, burst.Age, burst.Seed, spores, burst.Count, Colors.White);
		foreach (var cloud in fevers)
		{
			float t = cloud.Age / FeverCloudSeconds;
			// Billows out over its first fifth, hangs, then thins away.
			float grow = Mathf.Clamp(cloud.Age / 0.25f, 0f, 1f);
			// Thin: the puffs overlap, and a cloud over the bodies must not hide the swarm it is in.
			float alpha = t < 0.7f ? 0.3f : 0.3f * (1f - t) / 0.3f;
			DrawCloud(ToLocal(cloud.At), SpreadRadius * FeverCloudShare * (0.6f + 0.4f * grow), alpha, cloud.Age, 7);
		}
		if (windLeft > 0f)
		{
			float alpha = 0.32f * Mathf.Clamp(windLeft / 0.4f, 0f, 1f) * Mathf.Clamp((WindSeconds - windLeft) / 0.3f, 0f, 1f);
			Vector2 at = ToLocal(windAt);
			float radius = SpreadRadius * WindShare;
			DrawCloud(at, radius, alpha, WindSeconds - windLeft, 9);
			// A thin trail of the plague streaming off its back.
			for (int i = 1; i <= 3; i++)
				DrawCloud(at - windDir * radius * 0.7f * i, radius * (0.5f - 0.1f * i), alpha * (0.6f - 0.15f * i), WindSeconds - windLeft + i, 3);
		}
	}

	// A cloud of `puffs` overlapping discs inside `radius` of `at`, turning and breathing on its own
	// clock; the outer puffs in the ramp's edge, the inner in its mid, so it reads as a cloud of the
	// plague's colour against any floor. Never wider than `radius`, the ground it infects.
	private void DrawCloud(Vector2 at, float radius, float alpha, float age, int puffs)
	{
		if (alpha <= 0.01f || radius < 4f)
			return;
		for (int i = 0; i < puffs; i++)
		{
			float a = i * Mathf.Tau / puffs + age * 0.6f + i * 0.4f;
			float breathe = 1f + 0.08f * MathF.Sin(age * 3f + i * 1.9f);
			float d = radius * (i % 2 == 0 ? 0.48f : 0.3f);
			float r = radius * (i % 2 == 0 ? 0.42f : 0.5f) * breathe;
			Color c = i % 2 == 0 ? spores.Edge : spores.Mid;
			DrawCircleOn(air, at + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d, r, new Color(c, alpha));
		}
		DrawCircleOn(air, at, radius * 0.35f, new Color(spores.Hot, alpha * 0.8f));
	}

	private static void DrawCircleOn(CanvasItem canvas, Vector2 at, float radius, Color color) => canvas.DrawCircle(at, radius, color);
}
