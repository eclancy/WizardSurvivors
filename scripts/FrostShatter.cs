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
///
/// PIXEL ART ON THE WORLD'S GRID (the pixel-art pass, 2026-10-29; .ai/art-replacement-manifest.md
/// section 9). A statue is its corpse's own frame remapped into the Ice ramp by brightness
/// (frost_statue.gdshader) where it was the frame under an over-bright tint; a pool is one quad from a
/// fixed pool (meltwater.gdshader), a circle as its freeze is, where it was translucent squashed circles;
/// the shards in flight, the spikes a pool freezes into and a statue's cracks are stamped cell by cell
/// (PixelInk), where they were antialiased lines and polygons fading by alpha.
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
	private Node2D statueLayer;
	private readonly PoolQuad[] poolQuads = new PoolQuad[MaxPools];
	private readonly List<Enemy> scratch = new(64);

	private static Shader poolShader;
	private static Shader statueShader;
	private static readonly StringName ParamRadius = "radius";
	private static readonly StringName ParamSpread = "spread";
	private static readonly StringName ParamWet = "wet";
	private static readonly StringName ParamTime = "time";
	private static readonly StringName ParamSeed = "seed";
	private readonly List<Enemy> picks = new(8);

	private static readonly ElementColors.Ramp Ice = ElementColors.GetRamp(Element.Ice);
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
		// At the world's origin, unturned and unscaled, so its units are the world's and so is its grid.
		TopLevel = true;
		GlobalPosition = Vector2.Zero;
		poolShader ??= GD.Load<Shader>("res://scenes/shaders/meltwater.gdshader");
		statueShader ??= GD.Load<Shader>("res://scenes/shaders/frost_statue.gdshader");
		for (int i = 0; i < MaxPools; i++)
		{
			poolQuads[i] = new PoolQuad { Name = $"Pool{i}", Material = new ShaderMaterial { Shader = poolShader }, Visible = false };
			AddChild(poolQuads[i]);
		}
		// The statues first, so the cracks the air layer draws lie over them.
		statueLayer = new Node2D { Name = "Statues", ZAsRelative = false, ZIndex = DrawLayers.Air, Material = new ShaderMaterial { Shader = statueShader } };
		AddChild(statueLayer);
		statueLayer.Draw += DrawStatues;
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
		if (statueLayer != null && IsInstanceValid(statueLayer))
			statueLayer.Draw -= DrawStatues;
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
		PoolQuad quad = poolQuads[nextPool];
		if (quad != null)
		{
			quad.GlobalPosition = where;
			quad.Radius = pools[nextPool].Radius;
			var material = (ShaderMaterial)quad.Material;
			material.SetShaderParameter(ParamRadius, quad.Radius);
			material.SetShaderParameter(ParamSeed, (pools[nextPool].Seed % 997u) * 0.731f);
			material.SetShaderParameter(ParamSpread, 0f);
			quad.Visible = true;
			quad.QueueRedraw();
		}
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
				if (poolQuads[i] != null)
					poolQuads[i].Visible = false;
				continue;
			}
			any = true;
			PoolQuad quad = poolQuads[i];
			if (quad != null)
			{
				// A frozen pool is the spikes it burst into, in the air; the water is gone.
				quad.Visible = p.FrozenAge < 0f;
				var material = (ShaderMaterial)quad.Material;
				material.SetShaderParameter(ParamSpread, MathF.Min(1f, p.Age / 0.3f));
				material.SetShaderParameter(ParamWet, MathF.Min(1f, (MeltSeconds - p.Age) / 1f));
				material.SetShaderParameter(ParamTime, clock);
			}
		}

		statueLayer.QueueRedraw();
		air.QueueRedraw();
		if (!any)
			SetProcess(false);
	}

	// The statues: each its corpse's frame where it fell, through frost_statue.gdshader, which reads the
	// colour of the call - red how far it has frosted over (its first tenth of a second), green how near
	// it is to breaking - and remaps the frame into the Ice ramp.
	private void DrawStatues()
	{
		for (int i = 0; i < MaxStatues; i++)
		{
			ref Statue s = ref statues[i];
			if (!s.Live || s.Texture == null)
				continue;
			float t = s.Age / StatueSeconds;
			float frost = MathF.Min(1f, s.Age / 0.1f);
			float flash = t > 0.82f ? (t - 0.82f) / 0.18f : 0f;
			Vector2 size = s.Texture.GetSize();
			Vector2 topLeft = (s.Centered ? -size * 0.5f : Vector2.Zero) + s.Offset;
			statueLayer.DrawSetTransform(s.At, 0f, s.Scale);
			statueLayer.DrawTexture(s.Texture, topLeft, new Color(frost, flash, 0f, 1f));
		}
		statueLayer.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	// In the air, stamped on the grid: the cracks running over a statue as it nears the break, the flung
	// shards, and the ice a freezing pool bursts into.
	private void DrawAir()
	{
		for (int i = 0; i < MaxStatues; i++)
		{
			ref Statue s = ref statues[i];
			if (!s.Live || s.Texture == null)
				continue;
			float t = s.Age / StatueSeconds;
			if (t <= 0.5f)
				continue;
			Vector2 size = s.Texture.GetSize();
			Vector2 c = s.At;
			float k = (t - 0.5f) / 0.5f;
			float h = MathF.Abs(size.Y * s.Scale.Y) * 0.4f;
			PixelInk.Line(air, c + new Vector2(-2f, -h), c + new Vector2(3f, -h * 0.2f), Ice.Core, 1, k * 2f);
			PixelInk.Line(air, c + new Vector2(3f, -h * 0.2f), c + new Vector2(-1f, h * 0.5f * k), Ice.Core, 1, k * 2f);
			PixelInk.Line(air, c + new Vector2(3f, -h * 0.2f), c + new Vector2(h * 0.4f * k, h * 0.1f), Ice.Core, 1, k * 2f);
		}

		// A shard in flight: a sliver of ice, its point white-hot, a streak of the deep ice behind it.
		for (int i = 0; i < MaxShards; i++)
		{
			ref Shard s = ref shards[i];
			if (!s.Live)
				continue;
			Vector2 p = s.At;
			Vector2 d = s.Dir;
			PixelInk.Line(air, p - d * 12f, p - d * 2f, Ice.Edge, 2);
			PixelInk.Line(air, p - d * 7f, p, Ice.Hot, 1);
			PixelInk.Block(air, p, 2, Ice.Core);
		}

		for (int i = 0; i < MaxPools; i++)
		{
			ref Pool p = ref pools[i];
			if (!p.Live || p.FrozenAge < 0f)
				continue;
			// A ring of ice spikes standing up out of the pool, thrown up fast and shattering as they go:
			// the deep ice at the foot, the mid up the blade, a white-hot edge where the key lights it.
			float t = p.FrozenAge / FreezeShowSeconds;
			float rise = MathF.Min(1f, t / 0.25f);
			float fade = 1f - MathF.Max(0f, (t - 0.5f) / 0.5f);
			uint seed = p.Seed;
			const int Spikes = 7;
			for (int k = 0; k < Spikes; k++)
			{
				float a = Mathf.Tau * k / Spikes + Unit(ref seed) * 0.5f;
				float rr = p.Radius * (0.25f + 0.6f * Unit(ref seed));
				Vector2 foot = p.At + new Vector2(MathF.Cos(a) * rr, MathF.Sin(a) * rr);
				float h = (10f + 8f * Unit(ref seed)) * rise;
				if (h < PixelInk.Grid)
					continue;
				PixelInk.Line(air, foot, foot + new Vector2(0f, -h * 0.55f), Ice.Edge, 3, fade * 1.5f);
				PixelInk.Line(air, foot + new Vector2(0f, -2f), foot + new Vector2(0f, -h), Ice.Mid, 2, fade * 1.5f);
				PixelInk.Line(air, foot + new Vector2(-2f, -4f), foot + new Vector2(-2f, -h + 2f), Ice.Core, 1, fade * 1.5f);
			}
		}
	}

	/// <summary>One pool's quad, round its centre: the shader decides every pixel and discards the rest.</summary>
	private sealed partial class PoolQuad : Node2D
	{
		public float Radius { get; set; }

		public override void _Draw()
		{
			float r = Radius + 2f;
			DrawRect(new Rect2(-r, -r, r * 2f, r * 2f), Colors.White);
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
