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

	// --- PHASE 2 (.ai/spell-roster.md section 2h). Two level 4 cards that only moved numbers now each
	// change what the whirlwind does; their ids never moved (Laelaps' path names Tailwind Surge).
	//
	// TWIN GYRES (cyclone_slash_dual_blades, once Dual Windblades: +2 blades, +30% spin, on the one ring).
	// The two blades it adds are a second, inner ring turning the other way: it holds tight round the
	// wizard at InnerRadius of the orbit whatever the gust does, so when he runs and the outer ring swings
	// wide, what reaches him through the gap still meets a blade.
	public const string TwinGyresId = "cyclone_slash_dual_blades";
	public const int InnerBlades = 3;
	private const float InnerRadius = 0.5f;
	private const float InnerSpin = 1.2f;
	private int innerCount;
	private float innerAngle;
	// WIND SHEAR (cyclone_slash_tailwind_surge, Laelaps' required level 4, once "the blades spin 25%
	// faster"). At a full gust (ShearGust and over - always, under Typhoon Core) the blades shed crescents
	// of the wind they cut: in turn, one every ShearSeconds a blade, each flung off its blade along the
	// way it is turning and a little outward, flying ShearRange and cutting everything it crosses once for
	// ShearShare of a cut. The ring's reach, thrown past the ring - and only while he keeps running.
	public const string WindShearId = "cyclone_slash_tailwind_surge";
	public const float ShearGust = 0.8f;
	public const float ShearSeconds = 0.6f;
	public const float ShearRange = 170f;
	public const float ShearShare = 0.6f;
	private const float ShearSpeed = 480f;
	private const float ShearOutward = 0.45f;     // radians the throw leans out of the turn
	private const int MaxCrescents = 24;
	private const int CrescentHits = 6;          // the most one crescent cuts before it is spent
	private bool windShear;
	private float shedTimer;
	private int shedNext;
	private readonly Vector2[] crescentAt = new Vector2[MaxCrescents];
	private readonly Vector2[] crescentDir = new Vector2[MaxCrescents];
	private readonly float[] crescentTravelled = new float[MaxCrescents];
	private readonly bool[] crescentLive = new bool[MaxCrescents];
	private readonly ulong[] crescentHit = new ulong[MaxCrescents * CrescentHits];
	private readonly int[] crescentHitCount = new int[MaxCrescents];
	private int liveCrescents;
	private readonly List<Enemy> shearBuffer = new(32);

	public override void _Ready()
	{
		// The blades whirl in the air round him, over the bodies they cut. A child of the player at z 0,
		// they were sorted at his feet, and every body south of him hid them (DrawLayers).
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
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
		innerCount = SpellData?.HasEvolution(TwinGyresId) == true ? InnerBlades : 0;
		windShear = SpellData?.HasEvolution(WindShearId) ?? false;
	}

	private void EnsureBlades()
	{
		if (blades != null)
		{
			foreach (var b in blades)
				b?.QueueFree();
		}

		// The outer ring first (0 .. bladeCount-1), then Twin Gyres' inner ring.
		int total = bladeCount + innerCount;
		blades = new Area2D[total];
		bladeShapes = new CircleShape2D[total];
		for (int i = 0; i < total; i++)
		{
			var area = new Area2D
			{
				CollisionMask = 2,
				Monitoring = true,
				Monitorable = true
			};
			// The blade is drawn by _Draw, on the orbit, from the same radius as this shape.
			var shape = new CircleShape2D { Radius = i < bladeCount && bladeNow > 0f ? bladeNow : bladeRadius };
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
			innerAngle -= orbitSpeed * InnerSpin * dt;
			int outer = Math.Min(bladeCount, blades.Length);
			for (int i = 0; i < blades.Length; i++)
			{
				if (blades[i] == null) continue;
				if (i >= outer)
				{
					// Twin Gyres: the inner ring, the other way round, at a radius the gust never moves.
					float inner = innerAngle + Mathf.Tau / Math.Max(1, blades.Length - outer) * (i - outer);
					blades[i].Position = new Vector2(Mathf.Cos(inner), Mathf.Sin(inner)) * orbitRadius * InnerRadius;
					continue;
				}
				float angle = orbitAngle + (Mathf.Tau / outer) * i;
				blades[i].Position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radiusNow;
				// Only when it has moved a quarter pixel: a shape resized every frame is a physics
				// update per blade per frame on a spell that is up for the whole run.
				if (resize && bladeShapes?[i] != null)
					bladeShapes[i].Radius = bladeNow;
			}
		}
		if (windShear)
			ShedCrescents(dt);
		if (liveCrescents > 0)
			MoveCrescents(dt);
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

	// Wind Shear: one blade at a time, in turn round the ring, flings a crescent off along its turn.
	private void ShedCrescents(float dt)
	{
		shedTimer -= dt;
		if (shedTimer > 0f || gust < ShearGust || bladeCount <= 0)
			return;
		shedTimer = ShearSeconds / bladeCount;
		int slot = Array.IndexOf(crescentLive, false);
		if (slot < 0)
			return;
		int blade = shedNext % bladeCount;
		shedNext = (shedNext + 1) % bladeCount;
		float angle = orbitAngle + Mathf.Tau / bladeCount * blade;
		var outward = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
		// The way the ring turns at the blade (the angle grows, so the turn is a quarter turn ahead of it).
		var along = new Vector2(-outward.Y, outward.X);
		crescentDir[slot] = (along * MathF.Cos(ShearOutward) + outward * MathF.Sin(ShearOutward)).Normalized();
		crescentAt[slot] = GlobalPosition + outward * radiusNow;
		crescentTravelled[slot] = 0f;
		crescentHitCount[slot] = 0;
		crescentLive[slot] = true;
		liveCrescents++;
	}

	private void MoveCrescents(float dt)
	{
		var player = PlayerRef as Player;
		int cut = Math.Max(1, Mathf.RoundToInt(damage * ShearShare));
		float reach = bladeNow > 0f ? bladeNow : bladeRadius;
		MazeNavigation nav = MazeNavigation.Active;
		for (int k = 0; k < MaxCrescents; k++)
		{
			if (!crescentLive[k])
				continue;
			float step = ShearSpeed * dt;
			crescentAt[k] += crescentDir[k] * step;
			crescentTravelled[k] += step;
			// Spent at the end of its flight, on a maze wall, or once it has cut its fill.
			if (crescentTravelled[k] >= ShearRange || crescentHitCount[k] >= CrescentHits
				|| (nav != null && nav.IsWallAt(crescentAt[k])))
			{
				crescentLive[k] = false;
				liveCrescents--;
				continue;
			}
			// A body is a body, not a point: it cuts what it comes within its own size and a little of.
			float within = reach + 10f;
			EnemyRegistry.CollectNear(crescentAt[k], within, shearBuffer);
			foreach (Enemy e in shearBuffer)
			{
				if (e == null || !IsInstanceValid(e) || e.IsDying || crescentHitCount[k] >= CrescentHits)
					continue;
				if (e.GlobalPosition.DistanceSquaredTo(crescentAt[k]) > within * within)
					continue;
				ulong id = e.GetInstanceId();
				bool already = false;
				for (int j = 0; j < crescentHitCount[k]; j++)
				{
					if (crescentHit[k * CrescentHits + j] == id)
					{
						already = true;
						break;
					}
				}
				if (already)
					continue;
				crescentHit[k * CrescentHits + crescentHitCount[k]++] = id;
				player?.DealDamageToEnemy(e, cut, source: SpellData);
			}
			shearBuffer.Clear();
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
		int outer = Math.Min(bladeCount, blades.Length);
		// Twin Gyres' inner ring: the same blade in the same wind, trailing the other way, a stop dimmer
		// so the eye still reads the outer ring as the whirlwind.
		for (int i = outer; i < blades.Length; i++)
		{
			float inner = orbitRadius * InnerRadius;
			float angle = innerAngle + Mathf.Tau / Math.Max(1, blades.Length - outer) * (i - outer);
			DrawArc(Vector2.Zero, inner, angle, angle + 0.9f, 12, new Color(WindTrail, 0.22f), bladeRadius * 1.1f, true);
			DrawArc(Vector2.Zero, inner, angle, angle + 0.35f, 6, new Color(WindTrail, 0.5f), bladeRadius * 1.5f, true);
			DrawCircle(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * inner, bladeRadius, WindCore);
		}
		for (int i = 0; i < outer; i++)
		{
			float angle = orbitAngle + (Mathf.Tau / outer) * i;
			DrawArc(Vector2.Zero, radiusNow, angle - trail, angle, 16,
				new Color(WindTrail, 0.18f + 0.17f * eased), bladeNow * 1.1f, true);
			DrawArc(Vector2.Zero, radiusNow, angle - trail * 0.35f, angle, 8,
				new Color(WindTrail, 0.45f + 0.2f * eased), bladeNow * 1.6f, true);
			DrawCircle(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radiusNow, bladeNow, WindCore);
		}

		// Wind Shear's crescents: a sickle of the wind's light, its horns trailing, fading over its last third.
		for (int k = 0; k < MaxCrescents; k++)
		{
			if (!crescentLive[k])
				continue;
			float life = crescentTravelled[k] / ShearRange;
			float alpha = life < 0.66f ? 1f : (1f - life) / 0.34f;
			Vector2 at = crescentAt[k] - GlobalPosition;
			float heading = crescentDir[k].Angle();
			// As big as a blade's own crescent of wind (its trail is drawn 1.6 blades wide), so the eye
			// follows it off the ring: a sickle half that size was lost at the game's zoom.
			float r = (bladeNow > 0f ? bladeNow : bladeRadius) * 1.6f;
			Vector2 hub = at - crescentDir[k] * r * 0.7f;
			DrawArc(hub, r, heading - 1.2f, heading + 1.2f, 12, new Color(WindTrail, 0.6f * alpha), 6f, true);
			DrawArc(hub, r, heading - 0.75f, heading + 0.75f, 10, new Color(WindCore, 0.95f * alpha), 3f, true);
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
