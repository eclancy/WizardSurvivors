using Godot;
using System;

namespace WizardSurvivors.scripts;

// Mirefoot's bog: every live pool of one trail, drawn as ONE surface on the ground.
//
// "improve the mirefoot animation. it shouldnt just draw semi transparent circles that overlap each
// other and the player." Each LingeringZone used to draw itself - a translucent disc at z 1 - so a
// trail was a row of stacked lenses, darker wherever two pools overlapped (which, laid 72 px apart at
// a 46 px radius, is everywhere), and the newest pool sat over the wizard and every skeleton in it.
// The pools still do everything that matters - the damage, the slow, the reach, when they go - and
// this node only draws them (MirePool.tscn sets LingeringZone.DrawsItself = false):
//
//   - UNDER THE ACTORS. The lowest of the spells' ground layers: over the walkable decor (-20), just
//     under the craters, the scorch, the magma pools and rifts (CraterGround.GroundZ, -15) and the fire
//     in a crater (-14), and under an event's site (-5), a secret's seed (-4), the freeze marks (-2)
//     and every ground effect at -1 - so a trail never hides what the player is looking for or
//     another spell's tell, and a meteor that lands in the bog punches its crater over the water.
//   - ONE SURFACE. scenes/shaders/mire_surface.gdshader shades each 2 px cell once, from the union of
//     every pool that reaches it, so where pools overlap there is one bog with one waterline and
//     nothing doubles. Opaque, dithered, Bonelight tones only.
//   - THE RADIUS IS THE HIT. Each pool is drawn out of LingeringZone.Radius, the number its damage
//     tests, and its bank only ever wanders inward of it. It seeps in from a third of its size and is
//     whole before its first bite; when it dries the water draws back and leaves damp ground out to
//     the old edge (still the reach, still biting), and only its last moment dissolves it.
//   - ALIVE. Bubbles swell and burst and roll a ring out, duckweed drifts, ripple lines and a sheen of
//     sky slide across it, and rings roll out round the feet of whatever is wading in it.
//
// One node per trail, a sibling of the pools in the arena. NOTHING IS ALLOCATED PER FRAME: a pool's
// numbers are fixed once laid, so the shader's arrays are uploaded only when a pool comes or goes (a
// Variant of a packed array carries a finalizable disposer, and the first build uploaded three a frame
// - 0.4 KB a frame in the profiler's "mire" section); each pool's age is worked out in the shader from
// one float clock. The waders move every frame, so they are not uniforms: each is a quad of its own
// carrying its offset and phase in its vertices, gathered from the enemy grid with a struct visitor.
public partial class MireSurface : Node2D
{
	/// <summary>The most pools one surface draws - the shader's array size. A trail holds 10 to 15.</summary>
	public const int MaxPools = 32;
	private const int MaxWaders = 16;

	// Just under the craters and the other ground spells, over the walkable decor at -20 (see the header).
	public const int GroundZIndex = CraterGround.GroundZ - 1;

	// A pool seeps in over this long, and never longer than most of its first tick - so it has spread
	// to its full reach before it first bites (LingeringZone ticks first at TickSeconds).
	private const float SeepSeconds = 0.3f;
	private const float SeepShareOfTick = 0.8f;
	// The last of a pool dissolves over this long (or this share of a short life). When it starts to
	// dry, and how far its water draws in, are mire_surface.gdshader's DRY_FROM and DRY_SHRINK.
	private const float GoneSeconds = 0.45f;
	private const float GoneShare = 0.15f;
	private const float DryFrom = 0.55f;
	private const float DryShrink = 0.4f;
	// How far out of its circle a pool's quad reaches: one art pixel, so a cell on the rim is whole.
	private const float QuadPad = 2f;
	// A wader's rings: their centre below the body's (toward its feet), and the half-size of the quad
	// they roll out in. mire_surface.gdshader's wader_ring reaches 40 px across and 20 down.
	private const float Feet = 10f;
	private static readonly Vector2 WaderHalfSize = new(44f, 24f);
	// A wader's quad carries its offset in UV shifted this far in x (the shader's WADER_UV).
	private const float WaderUv = 1000f;

	private static Shader mireShader;
	private static readonly StringName ParamPools = "pools";
	private static readonly StringName ParamLives = "lives";
	private static readonly StringName ParamPoolCount = "pool_count";
	private static readonly StringName ParamTime = "time";

	private struct Pool
	{
		public LingeringZone Zone;
		public Vector2 Centre;
		public float Radius;
		public float Born;
		public float Duration;
		public float Seep;
		public float Gone;
		public float Seed;
	}

	private readonly Pool[] slots = new Pool[MaxPools];
	private int count;
	private bool poolsChanged;
	private readonly Vector4[] poolData = new Vector4[MaxPools];
	private readonly Vector4[] lifeData = new Vector4[MaxPools];
	private readonly Vector2[] waderAt = new Vector2[MaxWaders];
	private readonly float[] waderPhase = new float[MaxWaders];
	private readonly Enemy[] waderBodies = new Enemy[MaxWaders];
	private int waderCount;
	private readonly Vector2[] quad = new Vector2[4];
	private readonly Color[] quadColours = { Colors.White, Colors.White, Colors.White, Colors.White };
	private readonly Vector2[] quadUv = new Vector2[4];
	private ShaderMaterial material;
	private float clock;
	private bool drewLastFrame;
	private uint seedClock;

	/// <summary>
	/// Set when the trail that laid these pools is gone: the surface lets the last of them finish and
	/// then frees itself, so a pool never goes on biting with nothing drawn over it.
	/// </summary>
	public bool Orphaned { get; set; }

	public override void _Ready()
	{
		Name = "MireSurface";
		ZAsRelative = false;
		ZIndex = GroundZIndex;
		mireShader ??= GD.Load<Shader>("res://scenes/shaders/mire_surface.gdshader");
		material = new ShaderMaterial { Shader = mireShader };
		Material = material;
	}

	/// <summary>
	/// Draws <paramref name="zone"/> from now until it goes, centred on <paramref name="centre"/> (world).
	/// Its Radius, Duration and TickSeconds must be final. False when the surface is full.
	/// </summary>
	public bool Add(LingeringZone zone, Vector2 centre)
	{
		if (zone == null)
			return false;
		// At the cap, the trail has just freed its oldest pool to make room; it is still in the slots
		// until this frame's _Process, so let it go now.
		if (count >= MaxPools && (!IsInstanceValid(slots[0].Zone) || slots[0].Zone.IsQueuedForDeletion()))
		{
			Array.Copy(slots, 1, slots, 0, count - 1);
			slots[--count] = default;
		}
		if (count >= MaxPools)
			return false;
		float duration = MathF.Max(0.05f, zone.Duration);
		slots[count++] = new Pool
		{
			Zone = zone,
			Centre = centre,
			Radius = zone.Radius,
			// On this node's clock, which runs (and pauses) with the zone's own.
			Born = clock - zone.Age,
			Duration = duration,
			Seep = Mathf.Clamp(zone.TickSeconds * SeepShareOfTick, 0.05f, SeepSeconds),
			Gone = MathF.Min(GoneSeconds, duration * GoneShare),
			// A fresh seed a pool, so two pools never bubble in step.
			Seed = (++seedClock * 0.6180339f) % 1f * 97f,
		};
		poolsChanged = true;
		return true;
	}

	public override void _Process(double delta)
	{
		var perfStart = FrameProfiler.Begin();
		clock += (float)delta;

		// The pools that have gone - freed by their own clock, or by the trail making room - stop being
		// drawn the frame they go. Order is kept: the oldest pool owns where it overlaps a newer one.
		int kept = 0;
		for (int i = 0; i < count; i++)
		{
			Pool pool = slots[i];
			if (!IsInstanceValid(pool.Zone) || pool.Zone.Age >= pool.Duration)
				continue;
			slots[kept++] = pool;
		}
		if (kept != count)
			poolsChanged = true;
		for (int i = kept; i < count; i++)
			slots[i] = default;
		count = kept;

		if (count == 0)
		{
			waderCount = 0;
			if (drewLastFrame)
				QueueRedraw();
			drewLastFrame = false;
			if (Orphaned)
				QueueFree();
			FrameProfiler.End(FrameProfiler.Section.Mirefoot, perfStart);
			return;
		}

		if (poolsChanged)
		{
			poolsChanged = false;
			for (int i = 0; i < MaxPools; i++)
			{
				Pool pool = slots[i];
				poolData[i] = i < count ? new Vector4(pool.Centre.X, pool.Centre.Y, pool.Radius, pool.Seed) : Vector4.Zero;
				lifeData[i] = i < count ? new Vector4(pool.Born, pool.Duration, pool.Seep, pool.Gone) : Vector4.Zero;
			}
			material.SetShaderParameter(ParamPools, poolData);
			material.SetShaderParameter(ParamLives, lifeData);
			material.SetShaderParameter(ParamPoolCount, count);
		}
		material.SetShaderParameter(ParamTime, clock);

		// Whatever is standing in the water - not on the damp ground it has drawn back from. The radius is
		// mire_surface.gdshader's spread and shore without the bank's wander, which only pulls it in.
		var waders = new WaderVisitor { At = waderAt, Phase = waderPhase, Bodies = waderBodies };
		for (int i = 0; i < count; i++)
		{
			Pool pool = slots[i];
			float age = clock - pool.Born;
			float seep = Mathf.Clamp(age / pool.Seep, 0f, 1f);
			float goneAt = pool.Duration - pool.Gone;
			float dryAt = MathF.Min(goneAt, pool.Duration * DryFrom);
			float dry = Smooth((age - dryAt) / MathF.Max(0.01f, goneAt - dryAt));
			float water = pool.Radius * (0.33f + 0.67f * (1f - (1f - seep) * (1f - seep))) * (1f - DryShrink * dry);
			waders.Centre = pool.Centre;
			waders.RadiusSquared = water * water;
			EnemyRegistry.ForEachNear(pool.Centre, water, ref waders);
		}
		for (int i = waders.Count; i < waderCount; i++)
			waderBodies[i] = null;
		waderCount = waders.Count;

		QueueRedraw();
		drewLastFrame = true;
		FrameProfiler.End(FrameProfiler.Section.Mirefoot, perfStart);
	}

	// One quad a pool, carrying its index in the UV - the shader decides every cell inside it and
	// throws away the ones another pool owns or no bog reaches - and then one a wader, drawn over the
	// water, carrying its offset from its ring's centre in the UV and its phase in the colour.
	public override void _Draw()
	{
		if (count == 0)
			return;
		var perfStart = FrameProfiler.Begin();
		Transform2D toLocal = GlobalTransform.AffineInverse();
		quadColours[0] = quadColours[1] = quadColours[2] = quadColours[3] = Colors.White;
		for (int i = 0; i < count; i++)
		{
			Vector2 c = toLocal * slots[i].Centre;
			float r = slots[i].Radius + QuadPad;
			quad[0] = c + new Vector2(-r, -r);
			quad[1] = c + new Vector2(r, -r);
			quad[2] = c + new Vector2(r, r);
			quad[3] = c + new Vector2(-r, r);
			Vector2 index = new Vector2(i, 0f);
			quadUv[0] = quadUv[1] = quadUv[2] = quadUv[3] = index;
			DrawPrimitive(quad, quadColours, quadUv);
		}
		Vector2 h = WaderHalfSize;
		for (int j = 0; j < waderCount; j++)
		{
			Vector2 c = toLocal * (waderAt[j] + new Vector2(0f, Feet));
			quad[0] = c + new Vector2(-h.X, -h.Y);
			quad[1] = c + new Vector2(h.X, -h.Y);
			quad[2] = c + new Vector2(h.X, h.Y);
			quad[3] = c + new Vector2(-h.X, h.Y);
			quadUv[0] = new Vector2(WaderUv - h.X, -h.Y);
			quadUv[1] = new Vector2(WaderUv + h.X, -h.Y);
			quadUv[2] = new Vector2(WaderUv + h.X, h.Y);
			quadUv[3] = new Vector2(WaderUv - h.X, h.Y);
			Color phase = new Color(waderPhase[j], 0f, 0f, 1f);
			quadColours[0] = quadColours[1] = quadColours[2] = quadColours[3] = phase;
			DrawPrimitive(quad, quadColours, quadUv);
		}
		FrameProfiler.End(FrameProfiler.Section.Mirefoot, perfStart);
	}

	// Gathers the bodies standing in one pool, once each across the whole trail, from the enemy grid
	// (positions as of the last physics step - a ripple a step behind is invisible).
	private struct WaderVisitor : INeighbourVisitor
	{
		public Vector2 Centre;
		public float RadiusSquared;
		public Vector2[] At;
		public float[] Phase;
		public Enemy[] Bodies;
		public int Count;

		public void Visit(Enemy enemy, Vector2 position)
		{
			if (Count >= Bodies.Length || enemy.IsDying || position.DistanceSquaredTo(Centre) > RadiusSquared)
				return;
			for (int i = 0; i < Count; i++)
			{
				if (ReferenceEquals(Bodies[i], enemy))
					return;
			}
			Bodies[Count] = enemy;
			At[Count] = position;
			// Each body's rings keep their own rhythm, so a crowd wading does not pulse in step.
			Phase[Count++] = (enemy.GetHashCode() & 1023) / 1024f;
		}
	}

	private static float Smooth(float x)
	{
		x = Mathf.Clamp(x, 0f, 1f);
		return x * x * (3f - 2f * x);
	}
}
