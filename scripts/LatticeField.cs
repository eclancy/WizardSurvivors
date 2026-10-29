using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// The Lattice, the Stormcaller's Apotheosis (issue #61). Verb: link - the field itself conducts.
//
// Everything Chain Lightning strikes stays linked for LinkSeconds, and the lattice discharges along
// its own edges every DischargeSeconds, so the damage is a function of how connected the crowd is
// rather than of how many times the spell fired. An enemy that walks within JoinRadius of a live
// node joins it.
//
// #61: "link count is quadratic if written naively. Cap edges per node, not total nodes." Each node
// connects to at most EdgesPerNode of its nearest neighbours inside EdgeRange, and the lattice holds
// at most MaxNodes. A discharge is not a fresh Chain Lightning hit for linking purposes, or the
// lattice would refresh itself forever.
public partial class LatticeField : Node2D
{
	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; set; }

	private const float LinkSeconds = 3f;
	private const float DischargeSeconds = 0.4f;
	private const float EdgeRange = 200f;
	private const float JoinRadius = 52f;
	private const int EdgesPerNode = 3;
	private const int MaxNodes = 40;

	private struct Node2
	{
		public Enemy Enemy;
		public float Expires;
	}

	private readonly List<Node2> nodes = new();
	private readonly List<(int A, int B)> edges = new();
	private readonly List<(float Dist, int Index)> scratch = new();
	private readonly List<Enemy> targets = new();
	private float clock;
	private float dischargeTimer;
	private bool discharging;

	// The pixel-art pass (2026-10-29: "does not look up to our typical visual fidelity"). The arcs were
	// DrawLine and every linked enemy a translucent DrawCircle - smooth lines and a soft yellow disc. They
	// are SpellStrokes now (scenes/shaders/spell_strokes.gdshader), on the world's 2 px grid in Lightning's
	// ramp: each arc a one-pixel hot spine with its edge dithered round it, kinked as it always was; each
	// linked body a link's halo (a status mark on one enemy, art-direction section 3 - not an area marker)
	// as a ring of the ramp's mid at the old halo's edge, dithered to half - a fill of dots across the
	// body read as a screen door.
	private SpellStrokes strokes;
	private const int MaxStrokes = MaxNodes * EdgesPerNode * 2 + MaxNodes;

	public override void _Ready()
	{
		ZIndex = 2;
		Player.EnemyHit += OnEnemyHit;
		strokes = new SpellStrokes(MaxStrokes);
		ElementColors.Ramp lightning = ElementColors.GetRamp(Element.Lightning);
		strokes.SetTones(lightning, lightning.Edge, new Color(0.020f, 0.027f, 0.047f));
		Material = strokes.Material;
	}

	public override void _ExitTree() => Player.EnemyHit -= OnEnemyHit;

	private void OnEnemyHit(Enemy enemy, int amount, SpellData source)
	{
		if (discharging || source == null || source.Id != SpellData?.Id)
			return;
		Link(enemy);
	}

	private void Link(Enemy enemy)
	{
		if (enemy == null || !IsInstanceValid(enemy) || enemy.IsDying)
			return;
		for (int i = 0; i < nodes.Count; i++)
		{
			if (nodes[i].Enemy == enemy)
			{
				nodes[i] = new Node2 { Enemy = enemy, Expires = clock + LinkSeconds };
				return;
			}
		}
		if (nodes.Count < MaxNodes)
			nodes.Add(new Node2 { Enemy = enemy, Expires = clock + LinkSeconds });
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		QueueRedraw();

		for (int i = nodes.Count - 1; i >= 0; i--)
		{
			var n = nodes[i];
			if (n.Enemy == null || !IsInstanceValid(n.Enemy) || n.Enemy.IsDying || clock >= n.Expires)
				nodes.RemoveAt(i);
		}

		dischargeTimer -= dt;
		if (dischargeTimer > 0f)
			return;
		dischargeTimer = DischargeSeconds;
		JoinNearby();
		BuildEdges();
		Discharge();
	}

	private void JoinNearby()
	{
		if (nodes.Count == 0 || nodes.Count >= MaxNodes)
			return;
		float r2 = JoinRadius * JoinRadius;
		int existing = nodes.Count;
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy e || e.IsDying || nodes.Count >= MaxNodes)
				continue;
			for (int i = 0; i < existing; i++)
			{
				if (nodes[i].Enemy != e && nodes[i].Enemy.GlobalPosition.DistanceSquaredTo(e.GlobalPosition) <= r2)
				{
					Link(e);
					break;
				}
			}
		}
	}

	private void BuildEdges()
	{
		edges.Clear();
		float range2 = EdgeRange * EdgeRange;
		for (int i = 0; i < nodes.Count; i++)
		{
			scratch.Clear();
			Vector2 here = nodes[i].Enemy.GlobalPosition;
			for (int j = 0; j < nodes.Count; j++)
			{
				if (j == i) continue;
				float d = here.DistanceSquaredTo(nodes[j].Enemy.GlobalPosition);
				if (d <= range2) scratch.Add((d, j));
			}
			scratch.Sort((a, b) => a.Dist.CompareTo(b.Dist));
			for (int k = 0; k < scratch.Count && k < EdgesPerNode; k++)
			{
				int j = scratch[k].Index;
				// Each undirected edge once.
				if (i < j || !edges.Contains((j, i)))
					edges.Add((System.Math.Min(i, j), System.Math.Max(i, j)));
			}
		}
	}

	private void Discharge()
	{
		if (edges.Count == 0 || PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;
		int damage = Mathf.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(SpellData.CurrentLevel) ?? 4) * 0.35f));
		discharging = true;
		// Snapshot the endpoints first: a hit can kill, and a kill must not reshape the list mid-walk.
		scratch.Clear();
		foreach (var (a, b) in edges)
		{
			scratch.Add((0f, a));
			scratch.Add((0f, b));
		}
		targets.Clear();
		foreach (var (_, index) in scratch)
			if (index < nodes.Count) targets.Add(nodes[index].Enemy);
		foreach (Enemy e in targets)
			if (IsInstanceValid(e) && !e.IsDying)
				PlayerRef.DealDamageToEnemy(e, damage, source: SpellData);
		discharging = false;
	}

	public override void _Draw()
	{
		strokes.Begin();
		foreach (var (a, b) in edges)
		{
			if (a >= nodes.Count || b >= nodes.Count) continue;
			Enemy ea = nodes[a].Enemy, eb = nodes[b].Enemy;
			if (!IsInstanceValid(ea) || !IsInstanceValid(eb)) continue;
			Vector2 pa = ea.GlobalPosition, pb = eb.GlobalPosition;
			// A kink in the middle, re-rolled every frame, so a standing edge still crackles.
			Vector2 mid = (pa + pb) * 0.5f + new Vector2(-(pb - pa).Y, (pb - pa).X).Normalized() * (float)GD.RandRange(-8.0, 8.0);
			Arc(pa, mid);
			Arc(mid, pb);
		}
		foreach (var n in nodes)
		{
			if (!IsInstanceValid(n.Enemy))
				continue;
			Vector2 at = n.Enemy.GlobalPosition;
			strokes.Add(new SpellStrokes.Stroke
			{
				A = at, B = at, HalfA = 7f, HalfB = 7f, Hollow = true,
				Glow = 3f, Density = 0.5f, GlowTone = 2,
			});
		}
		strokes.Draw(this, Colors.White);
	}

	private void Arc(Vector2 a, Vector2 b) => strokes.Add(new SpellStrokes.Stroke
	{
		A = a, B = b, HalfA = 1f, HalfB = 1f, Spine = 1, Body = 1, Glow = 1f, Density = 0.5f, GlowTone = 3,
	});
}
