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
// that it stays where it was laid.
public partial class TrailWeaver : Node2D
{
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
	private float travelled = 0f;
	private bool tracking = false;
	private readonly List<LingeringZone> active = new();

	public void SetSpellLevel(int level)
	{
		CurrentLevel = System.Math.Max(1, level);
	}

	public override void _Process(double delta)
	{
		if (PlayerRef == null || PoolScene == null)
			return;

		if (!tracking)
		{
			// Establish a baseline on the first frame instead of counting the distance from the
			// origin, which would drop a pool immediately and in the wrong place.
			tracking = true;
			lastPosition = PlayerRef.GlobalPosition;
			return;
		}

		Vector2 now = PlayerRef.GlobalPosition;
		travelled += now.DistanceTo(lastPosition);
		lastPosition = now;

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
		Node2D arena = PlayerRef.GetParent<Node2D>();
		if (arena == null)
			return;

		PruneDeadPools();
		if (active.Count >= MaxActivePools)
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
			zone.Radius *= AreaMultiplier;
			zone.Duration *= DurationMultiplier;
			active.Add(zone);
		}

		arena.AddChild(pool);
		pool.GlobalPosition = where;
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
