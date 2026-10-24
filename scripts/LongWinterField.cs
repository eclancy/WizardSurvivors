using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// The Long Winter, the Frostweaver's Apotheosis (issue #61). Verb: stop - space, not damage.
//
// ConeBlast does the ring and the freeze itself (it opens to 360 degrees and roots instead of
// slowing once SpellData.IsApotheosis). This node is the other half: an enemy that dies while
// frozen shatters, and the shards freeze what is standing next to it.
//
// #61's warning, taken literally: freeze is stacking-resistant (strongest and longest win), so a
// wave that dies continuously next to its own neighbours could refresh them forever and become a
// permanent lock. So a shard can freeze a given enemy at most once per ShardImmunitySeconds, and the
// shard freeze is shorter than the ring's. Shatters are queued and resolved a few per frame, for the
// same reason Cataclysm's craters are: the damage they deal kills, and a kill raises the event again.
public partial class LongWinterField : Node2D
{
	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; set; }

	private const float ShardRadius = 74f;
	private const float ShardFreezeSeconds = 0.8f;
	private const float ShardImmunitySeconds = 2.5f;
	private const int ShattersPerFrame = 6;

	private readonly Queue<Vector2> pending = new();
	private readonly Dictionary<ulong, float> immuneUntil = new();
	private readonly List<(Vector2 At, float Age)> bursts = new();
	private float clock;

	private static readonly Color Shard = new Color(0.82f, 0.95f, 1.0f, 0.9f);

	public override void _Ready()
	{
		ZIndex = 1;
		Player.EnemyKilled += OnEnemyKilled;
	}

	public override void _ExitTree() => Player.EnemyKilled -= OnEnemyKilled;

	private void OnEnemyKilled(Enemy enemy, Vector2 at, SpellData source)
	{
		if (enemy != null && enemy.IsFrozen)
			pending.Enqueue(at);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		QueueRedraw();

		for (int i = bursts.Count - 1; i >= 0; i--)
		{
			var b = bursts[i];
			b.Age += dt;
			if (b.Age > 0.35f) bursts.RemoveAt(i); else bursts[i] = b;
		}

		for (int n = 0; n < ShattersPerFrame && pending.Count > 0; n++)
			Shatter(pending.Dequeue());
	}

	private void Shatter(Vector2 at)
	{
		bursts.Add((at, 0f));
		if (PlayerRef == null || !IsInstanceValid(PlayerRef)) return;
		int damage = Mathf.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(SpellData.CurrentLevel) ?? 4) * 0.3f));
		float r2 = ShardRadius * ShardRadius;
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy e || !IsInstanceValid(e) || e.IsDying || e.GlobalPosition.DistanceSquaredTo(at) > r2)
				continue;
			ulong id = e.GetInstanceId();
			if (immuneUntil.TryGetValue(id, out float until) && clock < until)
				continue;
			immuneUntil[id] = clock + ShardImmunitySeconds;
			e.ApplySlow(0f, ShardFreezeSeconds);
			PlayerRef.DealDamageToEnemy(e, damage, source: SpellData);
		}
	}

	public override void _Draw()
	{
		foreach (var (at, age) in bursts)
		{
			float t = age / 0.35f;
			Vector2 p = ToLocal(at);
			for (int k = 0; k < 8; k++)
			{
				float a = k * Mathf.Tau / 8f + 0.3f;
				Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
				DrawLine(p + dir * ShardRadius * t * 0.4f, p + dir * ShardRadius * t, new Color(Shard, Shard.A * (1f - t)), 2.5f);
			}
		}
	}
}
