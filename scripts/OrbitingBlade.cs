using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

// Cyclone Slash (Wind x2, issue #13): a spinning blade of wind that repeatedly hits everything
// nearby. Self-contained orbiting-blade pattern (code-created blades) so it doesn't depend on
// SpiritualWeapon.cs's hardcoded texture.
//
// **A running whirlwind** (issue #68, 2026-10-28). It was an orbit at 60 px doing Spiritual Weapon's
// job, and the plan to make it a sweep the way the player faces was overtaken by Stormsweep, which
// already is one. So it stays a ring and gains a demand: KEEP MOVING. The gust builds while the
// player walks (GustBuildSeconds to full) and the ring widens from CollapsedRadius to FullRadius of
// its orbit and spins faster; when he stops it collapses to a tight, slow ring within
// GustCollapseSeconds. It is the opposite of Solar Flare, the Brazier and Taproot, which all pay for
// standing still, and it reads the same IsStandingStill they do - intent, not displacement - so the
// four cannot disagree about what "still" is. Typhoon Core, the "permanent hurricane", is the
// Ascension that frees it: it never collapses. The drawing is the ring: each blade a bright crescent
// on the orbit with the wind it has just cut trailing behind it, longer and brighter as the gust
// rises, so the player can see the charge they are spending by stopping.
public partial class OrbitingBlade : Node2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float BaseOrbitSpeed { get; set; } = 2.5f;
	[Export] public float BaseOrbitRadius { get; set; } = 60f;
	[Export] public float HitCooldown { get; set; } = 0.4f;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float AttackSpeedMultiplier { get; set; } = 1.0f;
	public int ProjectileCountBonus { get; set; } = 0;
	public Node2D PlayerRef;

	private int damage = 4;
	private float orbitSpeed = 2.5f;
	private float orbitRadius = 60f;
	private int bladeCount = 2;
	private float orbitAngle = 0f;
	private Area2D[] blades;
	private readonly Dictionary<Node, float> hitCooldowns = new();

	private const float BaseBladeRadius = 10f;
	private float bladeRadius = BaseBladeRadius;

	// The gust: 0 collapsed, 1 at full. Linear both ways, eased where it is read.
	public const string TyphoonEvolutionId = "cyclone_slash_typhoon_core";
	private const float GustBuildSeconds = 2.5f;
	private const float GustCollapseSeconds = 0.5f;
	private const float CollapsedRadius = 0.6f;
	private const float FullRadius = 1.6f;
	private const float CollapsedSpin = 0.6f;
	private const float FullSpin = 1.5f;
	private const float FullBladeGrowth = 1.3f;
	private float gust;
	private bool permanent;
	private float radiusNow;
	private float bladeNow;
	private CircleShape2D[] bladeShapes;

	/// <summary>How far the gust has built, 0 (collapsed, standing still) to 1 (full, kept moving).</summary>
	public float Gust => gust;

	private static readonly Color WindCore = new Color(0.86f, 0.99f, 1.0f, 0.95f);
	private static readonly Color WindTrail = new Color(0.62f, 0.93f, 1.0f, 1.0f);

	// Vortex Reaper (Effect VortexPull = pull speed): every PullSeconds, what is near the ring but
	// outside it is dragged toward it, through Enemy.ApplyKnockback as Hollow Star's pull is, so a
	// boss's KnockbackResistance holds it. Nothing read the pull until 2026-10-26.
	private const float PullSeconds = 0.4f;
	private const float PullReach = 2.2f;
	private float vortexPull;
	private float pullTimer;

	// THE ENDLESS CHASE - Laelaps's Apotheosis (ApotheosisCatalog; the verb is "reflect", the last of the
	// ADR's unclaimed verbs that fits a whirlwind). The hound fated to catch whatever it hunted: every
	// enemy bolt that comes into the whirlwind - the ring as it stands now, so a collapsed one guards close
	// and a full one wide - is caught in it and thrown back at the enemy nearest where it was caught, for
	// ReflectDamageMultiplier times a blade's cut. Through the ordinary damage pipeline, as the spell's own.
	// A streak of the wind's light runs from the catch to the enemy it strikes, so the throw is seen.
	public const string EndlessChaseId = "cyclone_slash_endless_chase";
	public const float ReflectDamageMultiplier = 3f;
	private const float ReflectReach = 360f;
	private const int MaxStreaks = 8;
	private const float StreakSeconds = 0.22f;
	private struct Streak
	{
		public Vector2 From;
		public Vector2 To;
		public float Age;
		public bool Live;
	}
	private readonly Streak[] throws = new Streak[MaxStreaks];
	private int nextStreak;
	private readonly List<Enemy> reflectBuffer = new(32);

	/// <summary>Enemy bolts the Endless Chase has thrown back this run (the balance report).</summary>
	public static int StatBoltsReflected { get; private set; }

	public override void _Ready()
	{
		RefreshComputedStats();
		EnsureBlades();
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
		EnsureBlades();
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 4) * DamageMultiplier));
		// The evolutions' speed and Area as well as the player's (Dual Windblades, Tailwind Surge,
		// Typhoon Core). It read only the player's, so all three cards' numbers were unread until
		// 2026-10-26. Area widens the orbit and each blade with it, so a bigger hurricane is not a
		// wider ring with the same two pebbles going round it.
		float evolutionArea = SpellData?.GetAreaMultiplierAtLevel(CurrentLevel) ?? 1f;
		orbitSpeed = MathF.Max(0.1f, BaseOrbitSpeed * AttackSpeedMultiplier * (SpellData?.GetSpeedMultiplier() ?? 1f));
		// With the Area tag's level line (Effect AreaSize, a fraction) on the orbit too.
		orbitRadius = MathF.Max(10f, BaseOrbitRadius * AreaMultiplier * evolutionArea
			* (1f + (SpellData?.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel) ?? 0f)));
		bladeRadius = BaseBladeRadius * MathF.Sqrt(MathF.Max(1f, evolutionArea));
		vortexPull = SpellData?.GetEffectValueAtLevel(SpellEffect.VortexPull, CurrentLevel) ?? 0f;
		bladeCount = Math.Max(1, (SpellData?.GetProjectileCountAtLevel(CurrentLevel) ?? 2) + ProjectileCountBonus);
		permanent = SpellData?.HasEvolution(TyphoonEvolutionId) ?? false;
	}

	private void EnsureBlades()
	{
		if (blades != null)
		{
			foreach (var b in blades)
				b?.QueueFree();
		}

		blades = new Area2D[bladeCount];
		bladeShapes = new CircleShape2D[bladeCount];
		for (int i = 0; i < bladeCount; i++)
		{
			var area = new Area2D
			{
				CollisionMask = 2,
				Monitoring = true,
				Monitorable = true
			};
			// The blade is drawn by _Draw, on the orbit, from the same radius as this shape.
			var shape = new CircleShape2D { Radius = bladeNow > 0f ? bladeNow : bladeRadius };
			bladeShapes[i] = shape;
			area.AddChild(new CollisionShape2D { Shape = shape });
			area.Connect("area_entered", new Callable(this, nameof(OnHit)));
			area.Connect("body_entered", new Callable(this, nameof(OnHit)));
			AddChild(area);
			blades[i] = area;
		}
	}

	public override void _Process(double delta)
	{
		Position = Vector2.Zero;
		float dt = (float)delta;
		bool moving = PlayerRef is Player walker && !walker.IsStandingStill;
		gust = permanent ? 1f : Mathf.Clamp(gust + (moving ? dt / GustBuildSeconds : -dt / GustCollapseSeconds), 0f, 1f);
		float eased = gust * gust * (3f - 2f * gust);
		radiusNow = orbitRadius * Mathf.Lerp(CollapsedRadius, FullRadius, eased);
		float bladeTarget = bladeRadius * Mathf.Lerp(1f, FullBladeGrowth, eased);
		bool resize = MathF.Abs(bladeTarget - bladeNow) > 0.25f;
		bladeNow = resize ? bladeTarget : bladeNow;
		if (blades != null)
		{
			orbitAngle += orbitSpeed * Mathf.Lerp(CollapsedSpin, FullSpin, eased) * dt;
			for (int i = 0; i < blades.Length; i++)
			{
				if (blades[i] == null) continue;
				float angle = orbitAngle + (Mathf.Tau / blades.Length) * i;
				blades[i].Position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radiusNow;
				// Only when it has moved a quarter pixel: a shape resized every frame is a physics
				// update per blade per frame on a spell that is up for the whole run.
				if (resize && bladeShapes?[i] != null)
					bladeShapes[i].Radius = bladeNow;
			}
		}
		QueueRedraw();

		if (vortexPull > 0f)
		{
			pullTimer -= (float)delta;
			if (pullTimer <= 0f)
			{
				pullTimer = PullSeconds;
				PullTowardRing();
			}
		}

		if (SpellData != null && SpellData.IsApotheosis && SpellData.SelectedApotheosisId == EndlessChaseId)
			ReflectBolts();
		for (int i = 0; i < MaxStreaks; i++)
		{
			if (!throws[i].Live)
				continue;
			throws[i].Age += dt;
			if (throws[i].Age >= StreakSeconds)
				throws[i].Live = false;
		}

		clock += (float)delta;
		pruneTimer -= (float)delta;
		if (pruneTimer <= 0f && hitCooldowns.Count > 0)
		{
			pruneTimer = PruneSeconds;
			PruneHitCooldowns();
		}
	}

	// The cooldowns hold the clock time each body may be cut again, not a count-down. They were counted
	// down every frame through Keys.ToList() - a new list per frame on a persistent spell, in the swarm
	// (.ai/performance.md) - and now nothing is touched per frame: a hit compares against the clock,
	// and expired or freed entries are swept twice a second into a reused list.
	private const float PruneSeconds = 0.5f;
	private float clock;
	private float pruneTimer;
	private readonly List<Node> expired = new();

	private void PruneHitCooldowns()
	{
		expired.Clear();
		foreach (KeyValuePair<Node, float> entry in hitCooldowns)
		{
			if (entry.Value <= clock || !IsInstanceValid(entry.Key))
				expired.Add(entry.Key);
		}
		foreach (Node key in expired)
			hitCooldowns.Remove(key);
		expired.Clear();
	}

	private void ReflectBolts()
	{
		if (PlayerRef is not Player player)
			return;
		float reach = radiusNow + bladeNow;
		Vector2 centre = GlobalPosition;
		IReadOnlyList<EnemyProjectile> bolts = EnemyProjectile.Live;
		for (int i = 0; i < bolts.Count; i++)
		{
			EnemyProjectile bolt = bolts[i];
			if (bolt == null || !IsInstanceValid(bolt) || !bolt.InFlight || bolt.ClaimedBy != null)
				continue;
			Vector2 at = bolt.GlobalPosition;
			if (at.DistanceSquaredTo(centre) > reach * reach)
				continue;
			Enemy target = NearestEnemy(at);
			bolt.Destroy(Element.Wind);
			StatBoltsReflected++;
			if (target == null)
				continue;
			player.DealDamageToEnemy(target, Mathf.RoundToInt(damage * ReflectDamageMultiplier), source: SpellData);
			throws[nextStreak] = new Streak { From = at, To = target.GlobalPosition, Live = true };
			nextStreak = (nextStreak + 1) % MaxStreaks;
		}
	}

	private Enemy NearestEnemy(Vector2 at)
	{
		EnemyRegistry.CollectNear(at, ReflectReach, reflectBuffer);
		Enemy best = null;
		float bestD = ReflectReach * ReflectReach;
		foreach (Enemy e in reflectBuffer)
		{
			if (e == null || !IsInstanceValid(e) || e.IsDying)
				continue;
			float d = e.GlobalPosition.DistanceSquaredTo(at);
			if (d < bestD)
			{
				bestD = d;
				best = e;
			}
		}
		reflectBuffer.Clear();
		return best;
	}

	private void PullTowardRing()
	{
		Vector2 centre = GlobalPosition;
		// The ring as it stands now, so a collapsed whirlwind pulls the crowd onto a tight ring and a
		// full one onto a wide one.
		float orbitRadius = radiusNow > 0f ? radiusNow : this.orbitRadius;
		float reach = orbitRadius * PullReach;
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy enemy || !IsInstanceValid(enemy))
				continue;
			Vector2 toCentre = centre - enemy.GlobalPosition;
			float distance = toCentre.Length();
			// Only from outside the ring: the blades are on it, and a pull past it would drag the
			// crowd onto the caster.
			if (distance > reach || distance <= orbitRadius)
				continue;
			enemy.ApplyKnockback(toCentre / distance * MathF.Min(vortexPull, (distance - orbitRadius) * 3f));
		}
	}

	// The ring of wind. Each blade is a bright disc on the orbit - the size it hits at - inside a
	// crescent of the wind it has just cut, trailing behind it; faint streaks turn inside the ring,
	// more of them as the gust builds. Nothing is drawn wider than the orbit plus a blade.
	public override void _Draw()
	{
		if (blades == null || blades.Length == 0 || radiusNow <= 0f)
			return;
		float eased = gust * gust * (3f - 2f * gust);

		int streaks = 3 + Mathf.RoundToInt(5f * eased);
		for (int s = 0; s < streaks; s++)
		{
			float start = -orbitAngle * 0.7f + s * Mathf.Tau / streaks;
			float r = radiusNow * (s % 2 == 0 ? 0.8f : 0.92f);
			DrawArc(Vector2.Zero, r, start, start + Mathf.Lerp(0.35f, 0.8f, eased), 10,
				new Color(WindTrail, 0.08f + 0.16f * eased), Mathf.Lerp(1f, 2.5f, eased), true);
		}

		float trail = Mathf.Lerp(0.45f, 1.5f, eased);
		for (int i = 0; i < blades.Length; i++)
		{
			float angle = orbitAngle + (Mathf.Tau / blades.Length) * i;
			DrawArc(Vector2.Zero, radiusNow, angle - trail, angle, 16,
				new Color(WindTrail, 0.18f + 0.17f * eased), bladeNow * 1.1f, true);
			DrawArc(Vector2.Zero, radiusNow, angle - trail * 0.35f, angle, 8,
				new Color(WindTrail, 0.45f + 0.2f * eased), bladeNow * 1.6f, true);
			DrawCircle(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radiusNow, bladeNow, WindCore);
		}

		// The Endless Chase's throws: a streak of the wind's light from the catch to what it struck, the
		// head of it running out along the line and the whole of it fading.
		for (int i = 0; i < MaxStreaks; i++)
		{
			if (!throws[i].Live)
				continue;
			float t = throws[i].Age / StreakSeconds;
			Vector2 from = throws[i].From - GlobalPosition;
			Vector2 to = throws[i].To - GlobalPosition;
			Vector2 head = from.Lerp(to, MathF.Min(1f, t * 2.2f));
			float alpha = 1f - t;
			DrawLine(from, head, new Color(WindTrail, 0.5f * alpha), 5f, true);
			DrawLine(from, head, new Color(WindCore, 0.9f * alpha), 2f, true);
			DrawCircle(head, 4f * alpha + 1f, new Color(WindCore, alpha));
		}
	}

	private void OnHit(Node other)
	{
		if (other == null || !other.IsInGroup("enemies") || !other.HasMethod("TakeDamage"))
			return;
		if (hitCooldowns.TryGetValue(other, out float readyAt) && readyAt > clock)
			return;

		hitCooldowns[other] = clock + HitCooldown;
		// source matters: without it this damage is unattributable, and the balance report shows
		// the spell doing nothing rather than showing what it did.
		(PlayerRef as Player)?.DealDamageToEnemy(other, damage, source: SpellData);
	}
}
