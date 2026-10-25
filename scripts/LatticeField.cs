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

	private static readonly Color Arc = new Color(1.0f, 0.96f, 0.55f, 0.85f);
	private static readonly Color Halo = new Color(1.0f, 0.95f, 0.60f, 0.25f);

	public override void _Ready()
	{
		ZIndex = 2;
		Player.EnemyHit += OnEnemyHit;
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
		foreach (var (a, b) in edges)
		{
			if (a >= nodes.Count || b >= nodes.Count) continue;
			Enemy ea = nodes[a].Enemy, eb = nodes[b].Enemy;
			if (!IsInstanceValid(ea) || !IsInstanceValid(eb)) continue;
			Vector2 pa = ToLocal(ea.GlobalPosition), pb = ToLocal(eb.GlobalPosition);
			// A kink in the middle, re-rolled every frame, so a standing edge still crackles.
			Vector2 mid = (pa + pb) * 0.5f + new Vector2(-(pb - pa).Y, (pb - pa).X).Normalized() * (float)GD.RandRange(-8.0, 8.0);
			DrawLine(pa, mid, Arc, 2f);
			DrawLine(mid, pb, Arc, 2f);
		}
		foreach (var n in nodes)
			if (IsInstanceValid(n.Enemy))
				DrawCircle(ToLocal(n.Enemy.GlobalPosition), 10f, Halo);
	}
}
