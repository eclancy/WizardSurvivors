using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

/// <summary>
/// What Frost Shard's milestones add to the shatter (.ai/spell-roster.md section 2h): the dead standing a
/// moment as statues of ice before they break again, the shards Ice Flurry flings flying to what they hit,
/// Avalanche's shatters set off by the shatter's own kills, and Meltwater's pools that the next shatter
/// freezes. One node per arena. The shatter itself stays Player.ShatterAt; this holds what outlives it.
/// </summary>
/// <remarks>
/// Frost Shard fires nothing: every death shatters (Player.OnEnemyDiedAt). Its cards used to be the shatter
/// with a number changed - a harder slow, more flung shards, a bigger burst - and the flung shards were
/// struck the instant the shatter went off, drawn as a puff where they landed. Each card now changes what
/// the player sees a death do.
///
/// THE SHATTER GUARD. A shatter that kills does not shatter again (Player.shatterInProgress): one level,
/// so a dense screen is a burst rather than a chain reaction and a frame spike. Everything here that
/// strikes after the shatter went off - a shard landing, a statue breaking, a pool freezing - strikes
/// inside that guard (Player.FrostHit), so it keeps the rule. Avalanche is the one card that breaks it, on
/// purpose and to a fixed depth: a kill inside the guard is handed to <see cref="KilledInShatter"/>, which
/// shatters that body a beat later, one generation deeper, up to <see cref="AvalancheDepth"/>.
///
/// Everything is a slot in a fixed array, ticked here: nothing is allocated per death or per frame, and
/// the node stops processing when nothing is live. The pools lie on the ground (DrawLayers.Ground) and
/// never on blocked ground; statues, shards and the ice a pool freezes into are in the air
/// (DrawLayers.Air), on a child.
/// </remarks>
public partial class FrostShatter : Node2D
{
	/// <summary>Ice Statues: the dead freeze where they fall and break a second time a moment later.</summary>
	public const string StatueId = "frost_shard_glacial_spike";
	public const float StatueSeconds = 1.0f;
	/// <summary>Of the shatter, when the statue breaks.</summary>
	public const float StatueShare = 0.6f;

	/// <summary>Ice Flurry: shards flung out of every shatter at enemies just beyond it.</summary>
	public const string IceFlurryId = "frost_shard_ice_flurry";
	public const float ShardSpeed = 560f;
	/// <summary>How far past the burst a flung shard looks for its enemy, in bursts.</summary>
	public const float ShardReach = 2.5f;

	/// <summary>Avalanche: what a shatter kills shatters in turn, a beat later, rippling out through the pack.</summary>
	public const string AvalancheId = "frost_shard_absolute_zero";
	public const int AvalancheDepth = 3;
	public const float AvalancheBeatSeconds = 0.15f;

	/// <summary>Meltwater: every corpse melts into a pool, and the next shatter to reach a pool freezes it.</summary>
	public const string MeltwaterId = "frost_shard_blizzard_engine";
	public const float MeltSeconds = 4f;
	/// <summary>A pool's reach, as a share of the shatter's.</summary>
	public const float MeltRadiusShare = 0.85f;
	/// <summary>Of the shatter, to everything standing in a pool as it freezes.</summary>
	public const float MeltShare = 1.5f;

	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; set; }

	/// <summary>The generation of the shatter running now: 0 for a death, 1+ for Avalanche's.</summary>
	public int Generation { get; set; }

	private const int MaxStatues = 24;
	private const int MaxShards = 48;
	private const int MaxPending = 64;
	private const int MaxPools = 24;
	private const float FreezeShowSeconds = 0.35f;
	private const float BodyAllowance = 8f;

	private struct Statue
	{
		public Texture2D Texture;
		public Vector2 At;
		public Vector2 Scale;
		public Vector2 Offset;
		public bool Centered;
		public float Age;
		public bool Live;
	}

	private struct Shard
	{
		public Vector2 At;
		public Vector2 Dir;
		public Enemy Target;
		public Vector2 LastSeen;
		public int Damage;
		public float SlowMultiplier;
		public float SlowSeconds;
		public int Generation;
		public bool Live;
	}

	private struct Pending
	{
		public Vector2 At;
		public float Due;
		public int Generation;
		public bool Live;
	}

	private struct Pool
	{
		public Vector2 At;
		public float Radius;
		public float Age;
		public float FrozenAge;
		public uint Seed;
		public bool Live;
	}

	private readonly Statue[] statues = new Statue[MaxStatues];
	private readonly Shard[] shards = new Shard[MaxShards];
	private readonly Pending[] pending = new Pending[MaxPending];
	private readonly Pool[] pools = new Pool[MaxPools];
	private int nextStatue;
	private int nextShard;
	private int nextPool;
	private float clock;
	private uint seedClock;
	private Node2D air;
	private readonly List<Enemy> scratch = new(64);
	private readonly List<Enemy> picks = new(8);

	private static readonly ElementColors.Ramp Ice = ElementColors.GetRamp(Element.Ice);
	private static readonly ElementColors.Ramp Water = ElementColors.GetRamp(Element.Water);
	private static readonly Color Outline = new(0.02f, 0.04f, 0.08f, 0.85f);
	// The corpse's own sprite, frosted: blue-white, brighter than the slow and freeze looks on the living.
	private static readonly Color Frosted = new(0.9f, 1.2f, 1.6f, 1f);
	private static readonly StringName AnimatedSpriteNode = "AnimatedSprite2D";

	private static FrostShatter current;

	/// <summary>The arena's layer for this spell, made the first time a shatter needs it.</summary>
	public static FrostShatter For(Node arena, SpellData spell, Player player)
	{
		if (current != null && IsInstanceValid(current) && current.IsInsideTree() && current.GetParent() == arena && current.SpellData == spell)
			return current;
		if (arena == null || !IsInstanceValid(arena) || player == null)
			return null;
		current = new FrostShatter { Name = "FrostShatter", SpellData = spell, PlayerRef = player };
		arena.AddChild(current);
		return current;
	}

	public override void _Ready()
	{
		ZAsRelative = false;
		ZIndex = DrawLayers.Ground;
		air = new Node2D { Name = "Air", ZAsRelative = false, ZIndex = DrawLayers.Air };
		AddChild(air);
		air.Draw += DrawAir;
		Player.EnemyKilled += OnEnemyKilled;
		SetProcess(false);
	}

	public override void _ExitTree()
	{
		Player.EnemyKilled -= OnEnemyKilled;
		if (air != null && IsInstanceValid(air))
			air.Draw -= DrawAir;
		if (current == this)
			current = null;
	}

	private bool Has(string id) => SpellData != null && SpellData.HasEvolution(id);

	// --- Ice Statues -------------------------------------------------------------------------------

	// A body the player's hit killed outright - not one a shatter killed, which is inside the guard - is
	// frozen where it fell: its sprite is copied as it is this frame, frosted, and the corpse hidden.
	// Elites and bosses fall as they always did.
	private void OnEnemyKilled(Enemy enemy, Vector2 at, SpellData source)
	{
		if (PlayerRef == null || PlayerRef.ShatterGuarded || enemy == null || !IsInstanceValid(enemy) || enemy.IsMiniBoss || !Has(StatueId))
			return;
		var sprite = enemy.GetNodeOrNull<AnimatedSprite2D>(AnimatedSpriteNode.ToString());
		Texture2D texture = sprite?.SpriteFrames?.GetFrameTexture(sprite.Animation, sprite.Frame);
		if (texture == null)
			return;
		Vector2 scale = sprite.GlobalScale;
		if (sprite.FlipH)
			scale.X = -scale.X;
		statues[nextStatue] = new Statue
		{
			Texture = texture,
			At = sprite.GlobalPosition,
			Scale = scale,
			Offset = sprite.Offset,
			Centered = sprite.Centered,
			Age = 0f,
			Live = true,
		};
		nextStatue = (nextStatue + 1) % MaxStatues;
		sprite.Visible = false;
		Wake();
	}

	// --- Ice Flurry --------------------------------------------------------------------------------

	/// <summary>
	/// Flings <paramref name="count"/> shards out of a shatter at <paramref name="where"/> at the nearest
	/// enemies past its <paramref name="radius"/> and within <see cref="ShardReach"/> bursts; each flies to
	/// its enemy and strikes it for the shatter's damage and slow when it arrives.
	/// </summary>
	public void Fling(Vector2 where, float radius, int count, int damage, float slowMultiplier, float slowSeconds)
	{
		float reach = radius * ShardReach;
		picks.Clear();
		EnemyRegistry.CollectNear(where, reach, scratch);
		foreach (Enemy enemy in scratch)
		{
			if (!IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			float d = where.DistanceTo(enemy.GlobalPosition);
			if (d <= radius || d > reach)
				continue;
			// Kept sorted and no longer than the volley by insertion, as the instant shards were.
			int at = picks.Count;
			while (at > 0 && where.DistanceTo(picks[at - 1].GlobalPosition) > d)
				at--;
			if (at >= count)
				continue;
			picks.Insert(at, enemy);
			if (picks.Count > count)
				picks.RemoveAt(count);
		}
		scratch.Clear();
		foreach (Enemy target in picks)
		{
			Vector2 dir = (target.GlobalPosition - where).Normalized();
			shards[nextShard] = new Shard
			{
				At = where + dir * MathF.Min(radius * 0.5f, 20f),
				Dir = dir,
				Target = target,
				LastSeen = target.GlobalPosition,
				Damage = damage,
				SlowMultiplier = slowMultiplier,
				SlowSeconds = slowSeconds,
				Generation = Generation,
				Live = true,
			};
			nextShard = (nextShard + 1) % MaxShards;
		}
		picks.Clear();
		Wake();
	}

	// --- Avalanche ---------------------------------------------------------------------------------

	/// <summary>A body killed inside a shatter: with Avalanche, it shatters in its turn a beat later.</summary>
	public void KilledInShatter(Vector2 where)
	{
		if (Generation + 1 > AvalancheDepth || !Has(AvalancheId))
			return;
		for (int i = 0; i < MaxPending; i++)
		{
			if (pending[i].Live)
				continue;
			pending[i] = new Pending { At = where, Due = clock + AvalancheBeatSeconds, Generation = Generation + 1, Live = true };
			Wake();
			return;
		}
	}

	// --- Meltwater ---------------------------------------------------------------------------------

	/// <summary>Meltwater: the corpse at <paramref name="where"/> melts into a pool - never on water or a wall.</summary>
	public void LayPool(Vector2 where, float shatterRadius)
	{
		if (!Has(MeltwaterId))
			return;
		MazeNavigation nav = MazeNavigation.Active;
		if (nav != null && nav.IsBlockedAt(where))
			return;
		pools[nextPool] = new Pool { At = where, Radius = shatterRadius * MeltRadiusShare, Age = 0f, FrozenAge = -1f, Seed = ++seedClock * 2654435761u, Live = true };
		nextPool = (nextPool + 1) % MaxPools;
		Wake();
	}

	/// <summary>
	/// Meltwater: every unfrozen pool the shatter at <paramref name="where"/> reaches freezes, and bursts into
	/// ice under whatever stands in it. Called by the shatter, inside its guard.
	/// </summary>
	public void FreezePools(Vector2 where, float shatterRadius, int shatterDamage, float slowMultiplier, float slowSeconds)
	{
		if (!Has(MeltwaterId) || PlayerRef == null)
			return;
		int damage = Math.Max(1, Mathf.RoundToInt(shatterDamage * MeltShare));
		for (int i = 0; i < MaxPools; i++)
		{
			ref Pool p = ref pools[i];
			if (!p.Live || p.FrozenAge >= 0f || where.DistanceTo(p.At) > shatterRadius + p.Radius)
				continue;
			p.FrozenAge = 0f;
			Vector2 at = p.At;
			float reach = p.Radius + BodyAllowance;
			EnemyRegistry.CollectNear(at, reach, scratch);
			foreach (Enemy enemy in scratch)
			{
				if (IsInstanceValid(enemy) && !enemy.IsDying && at.DistanceTo(enemy.GlobalPosition) <= reach)
				{
					PlayerRef.DealDamageToEnemy(enemy, damage, source: SpellData);
					enemy.ApplySlow(slowMultiplier, slowSeconds);
				}
			}
			scratch.Clear();
			if (GetParent() is Node2D arena)
				ImpactDebrisLayer.Spawn(arena, at, p.Radius, Ice, Colors.White);
		}
	}

	// --- the clock ---------------------------------------------------------------------------------

	private void Wake() => SetProcess(true);

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		bool any = false;

		for (int i = 0; i < MaxStatues; i++)
		{
			ref Statue s = ref statues[i];
			if (!s.Live)
				continue;
			s.Age += dt;
			if (s.Age >= StatueSeconds)
			{
				s.Live = false;
				Vector2 at = s.At;
				s.Texture = null;
				PlayerRef?.ShatterFromFrost(at, StatueShare, 0);
				continue;
			}
			any = true;
		}

		for (int i = 0; i < MaxShards; i++)
		{
			ref Shard s = ref shards[i];
			if (!s.Live)
				continue;
			bool alive = s.Target != null && IsInstanceValid(s.Target) && !s.Target.IsDying;
			if (alive)
				s.LastSeen = s.Target.GlobalPosition;
			Vector2 to = s.LastSeen - s.At;
			float step = ShardSpeed * dt;
			if (to.Length() <= step + 6f)
			{
				s.Live = false;
				Enemy target = alive ? s.Target : null;
				s.Target = null;
				if (target != null)
					PlayerRef?.FrostHit(target, s.Damage, s.SlowMultiplier, s.SlowSeconds, s.Generation);
				if (GetParent() is Node2D arena)
					ImpactDebrisLayer.Spawn(arena, s.LastSeen, 14f, Ice, Colors.White);
				continue;
			}
			s.Dir = to.Normalized();
			s.At += s.Dir * step;
			any = true;
		}

		for (int i = 0; i < MaxPending; i++)
		{
			ref Pending p = ref pending[i];
			if (!p.Live)
				continue;
			if (clock >= p.Due)
			{
				p.Live = false;
				PlayerRef?.ShatterFromFrost(p.At, 1f, p.Generation);
				continue;
			}
			any = true;
		}

		for (int i = 0; i < MaxPools; i++)
		{
			ref Pool p = ref pools[i];
			if (!p.Live)
				continue;
			p.Age += dt;
			if (p.FrozenAge >= 0f)
				p.FrozenAge += dt;
			if (p.Age >= MeltSeconds || p.FrozenAge >= FreezeShowSeconds)
			{
				p.Live = false;
				continue;
			}
			any = true;
		}

		QueueRedraw();
		air.QueueRedraw();
		if (!any)
			SetProcess(false);
	}

	// The pools, on the ground: meltwater that spreads out of the corpse in its first moments, a pale
	// sheen on its near side, and fades in its last second.
	public override void _Draw()
	{
		for (int i = 0; i < MaxPools; i++)
		{
			ref Pool p = ref pools[i];
			if (!p.Live || p.FrozenAge >= 0f)
				continue;
			float spread = MathF.Min(1f, p.Age / 0.3f);
			float fade = MathF.Min(1f, (MeltSeconds - p.Age) / 1f);
			Vector2 c = ToLocal(p.At);
			float r = p.Radius * spread;
			DrawSetTransform(c, 0f, new Vector2(1f, 0.55f));
			DrawCircle(Vector2.Zero, r, Water.Edge with { A = 0.42f * fade });
			DrawCircle(new Vector2(-r * 0.1f, r * 0.05f), r * 0.75f, Water.Mid with { A = 0.3f * fade });
			DrawArc(Vector2.Zero, r, MathF.PI * 1.1f, MathF.PI * 1.7f, 10, Ice.Hot with { A = 0.55f * fade }, 1.5f);
			DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
		}
	}

	// In the air: the statues, the flung shards, and the ice a freezing pool bursts into.
	private void DrawAir()
	{
		for (int i = 0; i < MaxStatues; i++)
		{
			ref Statue s = ref statues[i];
			if (!s.Live || s.Texture == null)
				continue;
			// It frosts over in its first tenth of a second, and flashes white just before it breaks.
			float t = s.Age / StatueSeconds;
			float frost = MathF.Min(1f, s.Age / 0.1f);
			float flash = t > 0.82f ? (t - 0.82f) / 0.18f : 0f;
			Color tint = Colors.White.Lerp(Frosted, frost).Lerp(new Color(1.6f, 1.8f, 2.0f), flash);
			Vector2 size = s.Texture.GetSize();
			Vector2 topLeft = (s.Centered ? -size * 0.5f : Vector2.Zero) + s.Offset;
			air.DrawSetTransform(air.ToLocal(s.At), 0f, s.Scale);
			air.DrawTexture(s.Texture, topLeft, tint);
			air.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
			// Cracks running over it as it nears the break.
			if (t > 0.5f)
			{
				Vector2 c = air.ToLocal(s.At);
				float k = (t - 0.5f) / 0.5f;
				float h = MathF.Abs(size.Y * s.Scale.Y) * 0.4f;
				Color crack = Ice.Core with { A = 0.9f * k };
				air.DrawLine(c + new Vector2(-2f, -h), c + new Vector2(3f, -h * 0.2f), crack, 1.2f);
				air.DrawLine(c + new Vector2(3f, -h * 0.2f), c + new Vector2(-1f, h * 0.5f * k), crack, 1.2f);
				air.DrawLine(c + new Vector2(3f, -h * 0.2f), c + new Vector2(h * 0.4f * k, h * 0.1f), crack, 1f);
			}
		}

		for (int i = 0; i < MaxShards; i++)
		{
			ref Shard s = ref shards[i];
			if (!s.Live)
				continue;
			Vector2 p = air.ToLocal(s.At);
			Vector2 d = s.Dir;
			Vector2 side = d.Orthogonal();
			air.DrawLine(p - d * 10f, p, Outline, 4.5f);
			air.DrawLine(p - d * 10f, p - d * 3f, Ice.Mid, 2.5f);
			air.DrawColoredPolygon(new[] { p + d * 4f, p - d * 3f + side * 2.2f, p - d * 3f - side * 2.2f }, Ice.Core);
		}

		for (int i = 0; i < MaxPools; i++)
		{
			ref Pool p = ref pools[i];
			if (!p.Live || p.FrozenAge < 0f)
				continue;
			// A ring of ice spikes standing up out of the pool, thrown up fast and shattering as they go.
			float t = p.FrozenAge / FreezeShowSeconds;
			float rise = MathF.Min(1f, t / 0.25f);
			float fade = 1f - MathF.Max(0f, (t - 0.5f) / 0.5f);
			Vector2 c = air.ToLocal(p.At);
			uint seed = p.Seed;
			const int Spikes = 7;
			for (int k = 0; k < Spikes; k++)
			{
				float a = Mathf.Tau * k / Spikes + Unit(ref seed) * 0.5f;
				float rr = p.Radius * (0.25f + 0.6f * Unit(ref seed));
				Vector2 foot = c + new Vector2(MathF.Cos(a) * rr, MathF.Sin(a) * rr * 0.55f);
				float h = (10f + 8f * Unit(ref seed)) * rise;
				air.DrawColoredPolygon(new[] { foot + new Vector2(-3.5f, 0f), foot + new Vector2(0f, -h - 1.5f), foot + new Vector2(3.5f, 0f) }, Outline with { A = Outline.A * fade });
				air.DrawColoredPolygon(new[] { foot + new Vector2(-2.5f, 0f), foot + new Vector2(0f, -h), foot + new Vector2(2.5f, 0f) }, Ice.Mid with { A = fade });
				air.DrawLine(foot + new Vector2(-0.5f, -1f), foot + new Vector2(0f, -h + 2f), Ice.Core with { A = fade }, 1f);
			}
		}
	}

	private static float Unit(ref uint seed)
	{
		seed ^= seed << 13;
		seed ^= seed >> 17;
		seed ^= seed << 5;
		return (seed & 0xFFFFFF) / (float)0x1000000;
	}
}
