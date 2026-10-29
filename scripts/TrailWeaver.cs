using Godot;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Mirefoot: drops a pool of bog behind the player as they walk.
//
// The interesting part is the CADENCE. Every other spell in the game fires on a clock, so standing
// still and running flat out produce exactly the same output. This one fires on **distance
// travelled** - it costs nothing to own if you never move and pays out constantly if you kite, and
// it is the first spell whose rate is set by how the player is playing rather than by a timer.
//
// That makes it the deliberate counterweight to Solar Flare, which rewards holding still, and the
// partner of Hunter's Draw, which charges while moving and fires when you stop. Three spells that
// read the same input and disagree about what to do with it is a build decision.
//
// It is a persistent child of the Player, like Flamethrower - but the pools it drops are parented
// to the ARENA, because a pool that followed the player would be an aura and the whole point is
// that it stays where it was laid. The pools do not draw themselves: one MireSurface, also in the
// arena, draws the whole trail as one bog under the actors (scripts/MireSurface.cs).
//
// **Its own five milestones (2026-10-29, .ai/spell-roster.md section 2e).** It wore the default
// archetype's five names, each a number on the same trail. Each id now names something the bog DOES,
// read here by id (SpellData.HasEvolution); the ids are unchanged, so saves and the Ascension browser
// carry over:
//   - Sucking Mud: the first enemy to wade into each pool is held there, mud heaped round its feet
//     (LingeringZone.GripSeconds, drawn by MireSurface).
//   - Broad Wake (the proposal's Long Stride): every stride lays a pool with two smaller ones beside it,
//     so the trail is three pools across. The proposal's "pools join into a continuous trail" had come
//     true on 2026-10-28 for every trail - overlapping pools are one bog since MireSurface - so a card
//     that said it would have shown nothing new.
//   - Fen Gas: the pools breathe clouds of marsh gas that poison what they hang over (FenGas).
//   - Great Bog: stop, and the bog round you spreads into one marsh three pools wide that drags
//     everything in it to a crawl; walking, the pools are wider and bite harder.
//   - Will-o'-Wisps: marsh lights rise out of the pools and seek enemies (WispSwarm).
public partial class TrailWeaver : Node2D
{
	public const string SuckingMudId = "mirefoot_amplified_impact";
	public const string BroadWakeId = "mirefoot_hyper_acceleration";
	public const string FenGasId = "mirefoot_reactive_catalyst";
	public const string GreatBogId = "mirefoot_titan_cataclysm";
	public const string WillOWispsId = "mirefoot_infinite_horizon";

	/// <summary>Sucking Mud: how long a pool holds the first enemy into it.</summary>
	public const float MudHoldSeconds = 1.0f;
	/// <summary>Broad Wake: a flanking pool's size against the pool it is laid beside.</summary>
	public const float FlankScale = 0.6f;
	/// <summary>Great Bog: the marsh's reach against a pool's, and the crawl it drags to.</summary>
	public const float MarshScale = 3f;
	public const float MarshSlow = 0.3f;
	/// <summary>Great Bog: how long the wizard stands before the marsh wells up, and how long it lasts.</summary>
	public const float StillSeconds = 0.35f;
	public const float MarshSeconds = 3f;
	// While he keeps standing, a fresh marsh wells up this often over the last, so the one he stands in
	// never dries round him.
	private const float MarshEvery = 1.5f;
	// He is standing still while he stays within this of where he stopped (a shove from the crowd is not a step).
	private const float StillSlack = 6f;

	[Export] public PackedScene PoolScene { get; set; }

	// Far enough apart that a trail reads as a dotted line rather than a smear, and close enough
	// that a normal kiting arc leaves a continuous barrier behind it.
	[Export] public float DistancePerDrop { get; set; } = 72f;

	// A hard ceiling on live pools. Without one, a fast player with cooldown reduction carpets the
	// arena and every frame pays for hundreds of radius tests - the swarm-heavy allocation rule in
	// CLAUDE.md applies to zones as much as to particles.
	[Export] public int MaxActivePools { get; set; } = 10;

	public SpellData SpellData { get; set; }
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float DurationMultiplier { get; set; } = 1.0f;
	public float CooldownMultiplier { get; set; } = 1.0f;
	public int CurrentLevel { get; set; } = 1;
	public Player PlayerRef { get; set; }

	private Vector2 lastPosition;
	private Vector2 lastDrop;
	private float travelled = 0f;
	private bool tracking = false;
	private readonly List<LingeringZone> active = new();
	private MireSurface surface;
	private readonly RandomNumberGenerator rng = new();
	// Great Bog: where he stopped, how long he has stood there, and when the next marsh is due.
	private Vector2 stillAnchor;
	private float stillFor;
	private float marshDue;
	// Fen Gas and Will-o'-Wisps breathe out of the pools in turn.
	private float gasTimer;
	private float wispTimer;
	private int breathNext;

	public void SetSpellLevel(int level)
	{
		CurrentLevel = System.Math.Max(1, level);
	}

	public override void _Ready() => rng.Randomize();

	public override void _ExitTree()
	{
		// The pools already laid finish their lives; the surface draws them out and then goes.
		if (surface != null && IsInstanceValid(surface))
			surface.Orphaned = true;
		surface = null;
	}

	private bool Has(string id) => SpellData != null && SpellData.HasEvolution(id);

	public override void _Process(double delta)
	{
		if (PlayerRef == null || PoolScene == null)
			return;
		float dt = (float)delta;

		if (!tracking)
		{
			// Establish a baseline on the first frame instead of counting the distance from the
			// origin, which would drop a pool immediately and in the wrong place.
			tracking = true;
			lastPosition = PlayerRef.GlobalPosition;
			lastDrop = lastPosition;
			stillAnchor = lastPosition;
			return;
		}

		Vector2 now = PlayerRef.GlobalPosition;
		travelled += now.DistanceTo(lastPosition);
		lastPosition = now;

		if (Has(GreatBogId))
			TendMarsh(now, dt);
		if (Has(FenGasId))
			Breathe(ref gasTimer, FenGas.Interval, dt, false);
		if (Has(WillOWispsId))
			Breathe(ref wispTimer, WispSwarm.Interval, dt, true);

		if (travelled < EffectiveSpacing())
			return;

		travelled = 0f;
		DropPool(now);
	}

	// Cooldown reduction has to mean something for a spell with no cooldown, so it buys density
	// instead of rate: the faster your spells recharge, the tighter the pools are laid. Level-up
	// cooldown bonuses fold in the same way, via the spell's own cooldown against its base.
	private float EffectiveSpacing()
	{
		float scale = CooldownMultiplier;
		if (SpellData != null && SpellData.BaseCooldown > 0.01f)
			scale *= SpellData.GetCooldownAtLevel(CurrentLevel) / SpellData.BaseCooldown;

		return Mathf.Max(20f, DistancePerDrop * scale);
	}

	private void DropPool(Vector2 where)
	{
		Vector2 stride = where - lastDrop;
		lastDrop = where;
		LingeringZone pool = LayPool(where, 1f, 0f, 0f);
		// Broad Wake: two smaller pools beside the one just laid, square to the way he is walking.
		if (pool == null || !Has(BroadWakeId) || stride.LengthSquared() < 1f)
			return;
		Vector2 side = new Vector2(-stride.Y, stride.X).Normalized() * pool.Radius * 0.9f;
		LayPool(where + side, FlankScale, 0f, 0f);
		LayPool(where - side, FlankScale, 0f, 0f);
	}

	/// <summary>
	/// Lays one pool at <paramref name="where"/>, <paramref name="sizeScale"/> times a pool's size. A
	/// <paramref name="seconds"/> or <paramref name="slow"/> above zero overrides the pool's own (a marsh).
	/// Never on blocked ground (.ai/terrain.md): the wizard's own footprints never are, but a flanking
	/// pool can be.
	/// </summary>
	private LingeringZone LayPool(Vector2 where, float sizeScale, float seconds, float slow)
	{
		Node2D arena = PlayerRef.GetParent<Node2D>();
		if (arena == null)
			return null;
		if (sizeScale < 1f && MazeNavigation.Active?.IsBlockedAt(where) == true)
			return null;

		PruneDeadPools();
		int cap = PoolCap();
		while (active.Count >= cap && active.Count > 0)
		{
			LingeringZone oldest = active[0];
			active.RemoveAt(0);
			if (IsInstanceValid(oldest))
				oldest.QueueFree();
		}

		var pool = PoolScene.Instantiate<Node2D>();
		if (pool is LingeringZone zone)
		{
			zone.SpellData = SpellData;
			zone.PlayerRef = PlayerRef;
			zone.DamagePerTick = System.Math.Max(1, Mathf.RoundToInt(
				(SpellData?.GetDamageAtLevel(CurrentLevel) ?? 2) * DamageMultiplier));
			// What the milestones buy a trail: Area is the pool's radius, which the pool draws itself out
			// of; Speed is how often a pool bites; Slow drags harder; Zone Duration keeps a pool longer -
			// Will-o'-Wisps', and the level 6 line the Duration tag generates.
			float evolvedArea = SpellData?.GetAreaMultiplierAtLevel(CurrentLevel) ?? 1f;
			// The level table's Area lines are pixels on the pool (+10 at 3, +12 at 6), read since 2026-10-26.
			zone.Radius = (zone.Radius + (SpellData?.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel) ?? 0f))
				* AreaMultiplier * evolvedArea * sizeScale;
			zone.Duration = seconds > 0f ? seconds * DurationMultiplier
				: (zone.Duration + (SpellData?.GetEffectValueAtLevel(SpellEffect.ZoneDuration, CurrentLevel) ?? 0f)) * DurationMultiplier;
			zone.TickSeconds /= Mathf.Max(0.25f, SpellData?.GetSpeedMultiplier() ?? 1f);
			// The Slow and Dot tags' level lines: SlowPower drags harder, SlowDuration lingers after a body
			// leaves the pool, DotDamage bites harder each tick. Read since 2026-10-26.
			zone.SlowMultiplier = Mathf.Max(0.1f, zone.SlowMultiplier - (SpellData?.GetSlowMagnitudeAtLevel(CurrentLevel) ?? 0f)
				- (SpellData?.GetEffectValueAtLevel(SpellEffect.SlowPower, CurrentLevel) ?? 0f));
			if (slow > 0f)
				zone.SlowMultiplier = Mathf.Min(zone.SlowMultiplier, slow);
			zone.SlowLingerSeconds = SpellData?.GetEffectValueAtLevel(SpellEffect.SlowDuration, CurrentLevel) ?? 0f;
			zone.DamagePerTick += Mathf.RoundToInt(SpellData?.GetEffectValueAtLevel(SpellEffect.DotDamage, CurrentLevel) ?? 0f);
			// Sucking Mud: the first to wade in is held.
			if (Has(SuckingMudId))
				zone.GripSeconds = MudHoldSeconds;
			active.Add(zone);
		}

		arena.AddChild(pool);
		pool.GlobalPosition = where;
		if (pool is LingeringZone laid && !laid.DrawsItself)
			Surface(arena).Add(laid, where);
		return pool as LingeringZone;
	}

	// Great Bog: once he has stood his ground a moment, the bog round him spreads into one marsh, and
	// keeps welling up while he stays.
	private void TendMarsh(Vector2 now, float dt)
	{
		if (now.DistanceSquaredTo(stillAnchor) > StillSlack * StillSlack)
		{
			stillAnchor = now;
			stillFor = 0f;
			marshDue = 0f;
			return;
		}
		stillFor += dt;
		if (stillFor < StillSeconds)
			return;
		marshDue -= dt;
		if (marshDue > 0f)
			return;
		marshDue = MarshEvery;
		LayPool(stillAnchor, MarshScale, MarshSeconds, MarshSlow);
	}

	// Fen Gas and Will-o'-Wisps: the trail breathes out of its pools in turn, so the more pools it holds
	// the more often something rises, and every pool takes its turn.
	private void Breathe(ref float timer, float interval, float dt, bool wisp)
	{
		timer -= dt;
		if (timer > 0f)
			return;
		PruneDeadPools();
		if (active.Count == 0)
		{
			timer = 0.1f;
			return;
		}
		timer = interval / active.Count;
		LingeringZone pool = active[breathNext++ % active.Count];
		if (!IsInstanceValid(pool) || PlayerRef.GetParent() is not Node2D arena)
			return;
		float a = rng.Randf() * Mathf.Tau;
		Vector2 at = pool.GlobalPosition + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * pool.Radius * 0.6f * rng.Randf();
		if (wisp)
			WispSwarm.For(arena).Rise(at, Mathf.RoundToInt(pool.DamagePerTick * WispSwarm.Share), SpellData, PlayerRef);
		else
			FenGas.For(arena).Breathe(at, pool.DamagePerTick);
	}

	// The trail's one bog, made with its first pool and kept in the arena beside them.
	private MireSurface Surface(Node2D arena)
	{
		if (surface == null || !IsInstanceValid(surface))
		{
			surface = new MireSurface();
			arena.AddChild(surface);
		}
		return surface;
	}

	// The live pools a trail may hold: MaxActivePools, and one more for every count the spell has past
	// its base (Will-o'-Wisps) - a longer trail, not a denser one, which the cooldown already buys -
	// three times as many for Broad Wake's three-wide wake. Never more than MireSurface draws (32), so no
	// pool is ever biting without being seen.
	private int PoolCap()
	{
		int strides = MaxActivePools + System.Math.Max(0, (SpellData?.GetProjectileCountAtLevel(CurrentLevel) ?? 1) - (SpellData?.BaseProjectileCount ?? 1));
		if (Has(BroadWakeId))
			strides *= 3;
		return System.Math.Min(MireSurface.MaxPools, strides);
	}

	private void PruneDeadPools()
	{
		for (int i = active.Count - 1; i >= 0; i--)
		{
			if (!IsInstanceValid(active[i]))
				active.RemoveAt(i);
		}
	}
}
