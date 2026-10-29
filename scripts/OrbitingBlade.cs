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
// Ascension that frees it: it never collapses. The drawing is the ring: each blade a sickle of wind
// on the orbit with the air it has just cut trailing behind it, longer and brighter as the gust
// rises, so the player can see the charge they are spending by stopping.
//
// THE LOOK IS PIXEL ART (2026-10-29, Eric: "does not look up to our typical visual fidelity"). It was
// antialiased DrawArc and DrawCircle at fractional alpha - see-through bands, soft white discs, plain
// stroked arcs. The whirlwind is now one quad over its whole reach drawn by
// scenes/shaders/cyclone_whirl.gdshader on the world's 2 px grid (the blades, their trails, Twin Gyres'
// inner ring, the gust's eddies, Typhoon Core's arms and Vortex Reaper's in-drawn air), fed this frame's
// radiusNow and bladeNow - the numbers the CircleShape2Ds hit with - so it is drawn exactly as it cuts.
// What flies loose of the ring (Wind Shear's crescents, the Endless Chase's throws) is stamped here in
// whole world-grid cells, opaque, from the same ramp. Nothing allocates per frame.
public partial class OrbitingBlade : Node2D, IPaintsElementPath
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

	// The look (see the header): the shader quad, its ramp (Wind's, or an element path's - Razor Gale's
	// Metal), and how far the quad reaches, redrawn only when that changes.
	private static Shader whirlShader;
	private ShaderMaterial whirlMaterial;
	private Node2D whirl;
	private float whirlExtent;
	private ElementColors.Ramp ramp = ElementColors.GetRamp(Element.Wind);
	private Vector2 drawOrigin;
	private static readonly StringName ParamRadius = "radius";
	private static readonly StringName ParamBlade = "blade";
	private static readonly StringName ParamBladeCount = "blade_count";
	private static readonly StringName ParamAngle = "angle";
	private static readonly StringName ParamTrail = "trail";
	private static readonly StringName ParamGust = "gust";
	private static readonly StringName ParamInnerRadius = "inner_radius";
	private static readonly StringName ParamInnerBlade = "inner_blade";
	private static readonly StringName ParamInnerCount = "inner_count";
	private static readonly StringName ParamInnerAngle = "inner_angle";
	private static readonly StringName ParamTyphoon = "typhoon";
	private static readonly StringName ParamPullReach = "pull_reach";
	private static readonly StringName ParamTime = "time";
	private static readonly StringName ParamRampCore = "ramp_core";
	private static readonly StringName ParamRampHot = "ramp_hot";
	private static readonly StringName ParamRampMid = "ramp_mid";
	private static readonly StringName ParamRampEdge = "ramp_edge";

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

		// The whirlwind's quad, drawn behind this node so the crescents and throws stamped here sit on it.
		whirlShader ??= GD.Load<Shader>("res://scenes/shaders/cyclone_whirl.gdshader");
		whirlMaterial = new ShaderMaterial { Shader = whirlShader };
		whirl = new Node2D { Name = "Whirl", ShowBehindParent = true, Material = whirlMaterial };
		AddChild(whirl);
		whirl.Draw += DrawWhirlQuad;
		PaintLook();
	}

	// Disconnected on the way out and connected again on a way back in (_Ready runs only once).
	public override void _EnterTree()
	{
		if (whirl != null)
			whirl.Draw += DrawWhirlQuad;
	}

	public override void _ExitTree()
	{
		if (whirl != null && IsInstanceValid(whirl))
			whirl.Draw -= DrawWhirlQuad;
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
		EnsureBlades();
		PaintLook();
	}

	// What changes only with the spell's level and cards: the ring counts, the forms, the ramp.
	private void PaintLook()
	{
		if (whirlMaterial == null)
			return;
		// Razor Gale's Metal, or Wind's own. Read here, never per frame: GetBranchElement walks dictionaries.
		ramp = ElementColors.GetRamp(SpellData?.GetBranchElement() ?? Element.Wind);
		whirlMaterial.SetShaderParameter(ParamBladeCount, bladeCount);
		whirlMaterial.SetShaderParameter(ParamInnerCount, innerCount);
		whirlMaterial.SetShaderParameter(ParamInnerRadius, innerCount > 0 ? orbitRadius * InnerRadius : 0f);
		whirlMaterial.SetShaderParameter(ParamInnerBlade, bladeRadius);
		whirlMaterial.SetShaderParameter(ParamTyphoon, permanent);
		whirlMaterial.SetShaderParameter(ParamRampCore, ramp.Core);
		whirlMaterial.SetShaderParameter(ParamRampHot, ramp.Hot);
		whirlMaterial.SetShaderParameter(ParamRampMid, ramp.Mid);
		whirlMaterial.SetShaderParameter(ParamRampEdge, ramp.Edge);
	}

	// The quad: everything the whirlwind can reach this frame - the outer ring and its blades, the inner
	// ring, and Vortex Reaper's drag - and not a cell more; the shader decides every pixel inside it.
	private void DrawWhirlQuad()
	{
		float e = whirlExtent;
		whirl.DrawRect(new Rect2(-e, -e, e * 2f, e * 2f), Colors.White);
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
		UpdateLook(eased);

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

	// This frame's numbers to the shader - the same radiusNow, bladeNow and angles the blades' shapes
	// were just placed with - and the quad resized when the whirlwind's reach has moved a cell.
	private void UpdateLook(float eased)
	{
		if (whirlMaterial != null)
		{
			whirlMaterial.SetShaderParameter(ParamRadius, radiusNow);
			whirlMaterial.SetShaderParameter(ParamBlade, bladeNow);
			whirlMaterial.SetShaderParameter(ParamAngle, orbitAngle);
			whirlMaterial.SetShaderParameter(ParamInnerAngle, innerAngle);
			whirlMaterial.SetShaderParameter(ParamGust, eased);
			whirlMaterial.SetShaderParameter(ParamTrail, Mathf.Lerp(0.45f, 1.5f, eased));
			float pullReach = vortexPull > 0f ? radiusNow * PullReach : 0f;
			whirlMaterial.SetShaderParameter(ParamPullReach, pullReach);
			whirlMaterial.SetShaderParameter(ParamTime, clock);
			float extent = MathF.Max(radiusNow + bladeNow, pullReach);
			if (innerCount > 0)
				extent = MathF.Max(extent, orbitRadius * InnerRadius + bladeRadius);
			extent = MathF.Ceiling(extent / WhipLimb.Grid) * WhipLimb.Grid + WhipLimb.Grid * 2f;
			if (MathF.Abs(extent - whirlExtent) >= WhipLimb.Grid)
			{
				whirlExtent = extent;
				whirl.QueueRedraw();
			}
		}
		// The stamped parts move every frame they exist, and only then.
		if (liveCrescents > 0 || AnyThrowLive())
			QueueRedraw();
		else if (drewLoose)
			QueueRedraw();
	}

	private bool drewLoose;

	private bool AnyThrowLive()
	{
		for (int i = 0; i < MaxStreaks; i++)
			if (throws[i].Live)
				return true;
		return false;
	}

	// What flies loose of the ring, stamped in whole cells of the WORLD's 2 px grid (the shader's grid),
	// opaque, from the ramp. The ring itself is the shader's (DrawWhirlQuad).
	public override void _Draw()
	{
		drewLoose = false;
		drawOrigin = GlobalPosition;
		float g = WhipLimb.Grid;

		// Wind Shear's crescents: a sickle of the wind, horns trailing, as big as what it cuts - every
		// cell inside its hit radius (MoveCrescents: the blade's size and 10 more) - outlined in the edge,
		// white-hot along its belly, a stop cooler toward the horns, a broken streak of air behind it.
		// Over its last third it breaks up cell by cell rather than fading.
		float hitR = (bladeNow > 0f ? bladeNow : bladeRadius) + 10f;
		for (int k = 0; k < MaxCrescents; k++)
		{
			if (!crescentLive[k])
				continue;
			drewLoose = true;
			float life = crescentTravelled[k] / ShearRange;
			float keep = life < 0.66f ? 1f : (1f - life) / 0.34f;
			Vector2 dir = crescentDir[k];
			float heading = dir.Angle();
			Vector2 hub = crescentAt[k] - dir * hitR * 0.7f;
			int steps = Math.Max(6, (int)(hitR * 2.3f / g));
			for (int s = 0; s <= steps; s++)
			{
				float u = -1f + 2f * s / steps;
				float a = heading + u * 1.15f;
				var spoke = new Vector2(MathF.Cos(a), MathF.Sin(a));
				float side = MathF.Abs(u);
				if (Shade(k * 64 + s) > keep)
					continue;
				// Outlined in the edge on its hit radius (as the ring's blades are, for a pale floor), then
				// white-hot along the belly inside it.
				Stamp(hub + spoke * hitR, g, ramp.Edge);
				Stamp(hub + spoke * (hitR - g), g, side < 0.4f ? ramp.Core : side < 0.75f ? ramp.Hot : ramp.Mid);
				if (side < 0.85f)
					Stamp(hub + spoke * (hitR - g * 2f), g, side < 0.4f ? ramp.Hot : ramp.Mid);
				if (side < 0.45f)
					Stamp(hub + spoke * (hitR - g * 3f), g, ramp.Edge);
			}
			for (int s = 2; s <= 5; s++)
				if (Shade(k * 64 + 40 + s) < keep * 0.8f)
					Stamp(crescentAt[k] - dir * (hitR * 0.3f + s * g * 2f), g, ramp.Edge);
		}

		// The Endless Chase's throws: the caught bolt flung along a line of the wind from the catch to what
		// it struck, the head running out ahead - a white-hot heart in the edge, so it reads on a pale floor -
		// the line behind it cooling and breaking up.
		for (int i = 0; i < MaxStreaks; i++)
		{
			if (!throws[i].Live)
				continue;
			drewLoose = true;
			float t = throws[i].Age / StreakSeconds;
			Vector2 from = throws[i].From;
			Vector2 head = from.Lerp(throws[i].To, MathF.Min(1f, t * 2.2f));
			float length = from.DistanceTo(head);
			int cells = Math.Min(120, (int)(length / g));
			Vector2 step = cells > 0 ? (head - from) / cells : Vector2.Zero;
			for (int c = 0; c < cells; c++)
			{
				float back = 1f - (float)c / Math.Max(1, cells);
				if (Shade(i * 128 + c) < back * 0.6f + t * 0.5f)
					continue;
				Stamp(from + step * c, g, back < 0.2f ? ramp.Hot : back < 0.55f ? ramp.Mid : ramp.Edge);
			}
			if (t < 0.8f)
			{
				Stamp(head, g * 3f, ramp.Edge);
				Stamp(head, g, ramp.Core);
			}
		}
	}

	// One cell of `size` px on the world's grid round `world`, drawn in this node's space.
	private void Stamp(Vector2 world, float size, Color colour)
	{
		float g = WhipLimb.Grid;
		float half = size * 0.5f;
		var corner = new Vector2(MathF.Round((world.X - half) / g) * g, MathF.Round((world.Y - half) / g) * g);
		DrawRect(new Rect2(corner - drawOrigin, size, size), colour);
	}

	// A fixed 0..1 per index: which cells of a breaking-up crescent or throw go first.
	private static float Shade(int i)
	{
		uint h = (uint)i * 2654435761u;
		h ^= h >> 15;
		h *= 2246822519u;
		h ^= h >> 13;
		return (h & 0xFFFF) / 65536f;
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
