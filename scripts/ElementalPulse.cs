using Godot;
using System.Collections.Generic;
using System;

namespace WizardSurvivors.scripts;

// Shared script for simple "AoE pulse" offensive spells (issue #13 roster expansion): Solar Flare
// (Toxic Spore Burst left it for its mushrooms, SporeMushroom). Behaves like ArcaneExplosion
// (auto-repeating pulse around the caster, one persistent instance per equipped spell) plus an
// optional guaranteed status effect applied to everything caught in the blast.
public partial class ElementalPulse : Area2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float CooldownMultiplier { get; set; } = 1.0f;
	public float DurationMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;
	[Export] public float TotalLifetime = 0f; // 0 = infinite (persistent child of Player)

	[Export] public bool GuaranteedPoison { get; set; } = false;
	[Export] public int PoisonDamagePerTick { get; set; } = 2;
	[Export] public float PoisonDuration { get; set; } = 3.0f;
	// Weaken applied to everything the pulse touches (Enemy.ApplyWeaken, issue #66). 0 = off.
	// Solar Flare's: a flash at arm's length that leaves whatever is closest swinging blind.
	[Export] public float WeakenFraction { get; set; } = 0f;
	[Export] public float WeakenDuration { get; set; } = 2.5f;
	[Export] public float RingExpandDuration { get; set; } = 0.22f;
	[Export] public float RingRetractDuration { get; set; } = 0.14f;
	[Export] public Color VisualColor { get; set; } = new Color(1.0f, 0.84f, 0.20f, 0.85f);

	// SOLAR FLARE GROWS WHILE YOU STAND STILL (#68, .ai/spell-roster.md section 3): the anti-kite
	// spell. Every pulse reads how long the player has held his ground and goes off wider and hotter
	// for it, up to StillRadiusGrowth more reach and StillDamageGrowth more damage after
	// StillGrowthSeconds; a step resets it. The stillness is Player.StillSeconds - intent, not
	// displacement, the same reading the Brazier and Taproot make - so a shove from the crowd never
	// cools the flare when it does not cool the Brazier, and the three cannot disagree about what
	// "still" is. It shows twice: the burst is drawn to the reach it hits at, and between bursts a
	// glow at his feet brightens as it builds (SolarFlareVisual.SetCharge) - never a ring at the
	// reach, which is the area marker .ai/spell-roster.md section 2c took off every player spell.
	[Export] public float StillGrowthSeconds { get; set; } = 4f;
	[Export] public float StillRadiusGrowth { get; set; } = 0.6f;
	[Export] public float StillDamageGrowth { get; set; } = 1.0f;

	private Node2D feetShape;

	/// <summary>How far the flare has built, 0 (just moved) to 1 (held for StillGrowthSeconds).</summary>
	public float StillCharge => StillGrowthSeconds > 0f && PlayerRef is Player holder && holder.IsStandingStill
		? Mathf.Clamp(holder.StillSeconds / StillGrowthSeconds, 0f, 1f)
		: 0f;

	// Blinding Flash (solar_flare_blinding_flash): the chance a pulse staggers and shoves what it
	// hits, and how long the stagger holds. Its KnockbackBonus is the shove (SpellData.GetKnockbackSpeedAtLevel).
	public const float BlindingFlashChance = 0.3f;
	public const float BlindingFlashStunSeconds = 0.4f;

	private int damage = 5;
	private float range = 100f;
	private float cooldown = 1.5f;
	private float lifeElapsed = 0f;
	private float fireTimer = 0f;
	private float ringPulseTimer = 0f;
	private bool ringPulseActive = false;
	private GpuParticles2D particles;
	private PlaceholderShape visual;
	// A spell may ship its own bespoke visual (a child node named "Visual" implementing IPulseVisual)
	// instead of the shared PlaceholderShape ring. Solar Flare, the one pulse spell left, does.
	private IPulseVisual customVisual;

	public override void _Ready()
	{
		RefreshComputedStats();
		particles = GetNodeOrNull<GpuParticles2D>("Particles");
		visual = GetNodeOrNull<PlaceholderShape>("PlaceholderShape");
		customVisual = GetNodeOrNull<Node2D>("Visual") as IPulseVisual;
		TriggerPulse();
		fireTimer = 0f;
	}

	public override void _ExitTree()
	{
		if (sunDogGlow != null && IsInstanceValid(sunDogGlow))
			sunDogGlow.Draw -= DrawSunDogs;
		if (prominenceCanvas != null && IsInstanceValid(prominenceCanvas))
			prominenceCanvas.Draw -= DrawProminences;
	}

	public override void _Process(double delta)
	{
		if (PlayerRef != null)
			GlobalPosition = PlayerRef.GlobalPosition;

		UpdateRingPulse((float)delta);
		BurnBolts();
		TickSunDogs((float)delta);
		TickProminences((float)delta);
		if (customVisual is SolarFlareVisual building)
		{
			// The Feet capsule, which Player.FitFeetToSprite moves for other cell sizes; the pulse sits
			// on the player's origin, so its position is already in this node's space.
			if (feetShape == null || !IsInstanceValid(feetShape))
				feetShape = PlayerRef?.GetNodeOrNull<Node2D>("Feet/CollisionShape2D");
			building.SetCharge(StillCharge, feetShape?.Position ?? new Vector2(0f, 36f));
		}

		if (TotalLifetime > 0f)
		{
			lifeElapsed += (float)delta;
			if (lifeElapsed >= TotalLifetime)
			{
				QueueFree();
				return;
			}
		}

		fireTimer += (float)delta;
		if (fireTimer >= cooldown)
		{
			TriggerPulse();
			fireTimer = 0f;
		}
	}

	private void TriggerPulse()
	{
		// The cue plays on every pulse, hit or not. This spell is a persistent emitter, so the fire
		// loop's cast cue never reaches it after creation (CastOutcome.Maintained) and it used to go
		// off in silence on an empty field. A pulse is a cast the player can see; it should be one
		// they can hear. Creation plays it here too - the fire loop stays quiet for that frame - so
		// the first pulse is heard once, not twice.
		SfxPlayer.Cast(SfxCatalog.DominantElement(SpellData), GlobalPosition);

		var player = PlayerRef as Player;
		float knockbackSpeed = SpellData?.GetKnockbackSpeedAtLevel(CurrentLevel) ?? 0f;
		// This pulse's reach and damage, grown by however long he has stood still for it.
		float charge = StillCharge;
		float reach = range * (1f + StillRadiusGrowth * charge);
		int hit = Math.Max(1, Mathf.RoundToInt(damage * (1f + StillDamageGrowth * charge)));
		// Iterate the "enemies" group, like every other AoE in the project. This used to walk
		// CurrentScene's *direct children* only, so any enemy parented to a spawner or container
		// was skipped entirely and silently took no damage from the pulse.
		foreach (var node in EnemyRegistry.Snapshot)
		{
			if (node is not Node2D e || !IsInstanceValid(e))
				continue;
			if (GlobalPosition.DistanceTo(e.GlobalPosition) > reach)
				continue;

			player?.DealDamageToEnemy(e, hit, source: SpellData);
			if (GuaranteedPoison && e.HasMethod("ApplyPoison"))
				e.Call("ApplyPoison", PoisonDamagePerTick, PoisonDuration);
			if (WeakenFraction > 0f && e is Enemy dazzled)
				dazzled.ApplyWeaken(WeakenFraction, WeakenDuration * DurationMultiplier);
			// Blinding Flash: some of what the flash catches reels - a stagger and a shove straight
			// out from the caster. Not every one: a pulse that pushed the whole ring away every two
			// seconds would hold the crowd at its edge, which is a ward and not a flash.
			if (knockbackSpeed > 0f && e is Enemy blinded && GD.Randf() < BlindingFlashChance)
			{
				Vector2 away = e.GlobalPosition - GlobalPosition;
				blinded.ApplyShock(BlindingFlashStunSeconds);
				blinded.ApplyKnockback((away.LengthSquared() > 0.01f ? away.Normalized() : Vector2.Right) * knockbackSpeed);
			}
		}

		FireCoronaBeams(player, reach, hit);
		FlareSunDogs(player, reach, hit);
		HurlProminences(reach, hit, charge);

		if (particles != null)
			particles.Restart();

		if (customVisual != null)
		{
			// A bespoke visual owns its own animation and timing, so the generic ring tween below
			// (and the ringPulseActive state it drives) is skipped entirely for those spells.
			customVisual.Burst(reach);
			return;
		}

		if (visual != null)
		{
			visual.Ring = true;
			visual.EnableIdleMotion = false;
			visual.Radius = 2f;
			visual.RingWidth = 6f;
			visual.ShapeColor = VisualColor;
			visual.QueueRedraw();
		}

		burstReach = reach;
		ringPulseTimer = 0f;
		ringPulseActive = true;
	}

	// The reach of the pulse the fallback ring is drawing, grown or not.
	private float burstReach = 100f;

	// Corona Discharge (solar_flare_corona_discharge): every pulse also throws CoronaBeams sunbeams out
	// past the flare, evenly spaced and turned a little each pulse, each striking everything along it
	// for CoronaBeamShare of the pulse. The card promised eight sunbeams from the start and the pulse
	// fired none; then it was damage alone (2026-10-26, morning), which is not what an Ascension should
	// feel like. Built that afternoon. SolarFlareVisual draws the beams at exactly this length and width.
	public const string CoronaDischargeId = "solar_flare_corona_discharge";

	// DAYBREAK AVATAR IS A WARD (2026-10-28). Eric: "one of the spells that can protect you from
	// projectiles should be an ascension for an aoe spell around you that destroys projectiles, like
	// garlic does in vampire survivors." The flare is the spell around the wizard, and Daybreak was the
	// Ascension that only made it bigger (+100% area, +100% damage): now every enemy bolt that comes
	// inside the flare's reach - the reach it has this moment, grown by standing still - burns away in
	// a burst of light, between pulses as well as on them. Ranged casters stop being a threat inside
	// it; they still are outside, and nothing else they do is touched.
	public const string DaybreakId = "solar_flare_daybreak";
	public static int StatBoltsBurned { get; private set; }

	private void BurnBolts()
	{
		IReadOnlyList<EnemyProjectile> bolts = EnemyProjectile.Live;
		if (bolts.Count == 0 || SpellData == null || !SpellData.HasEvolution(DaybreakId))
			return;
		float reach = range * (1f + StillRadiusGrowth * StillCharge);
		float r2 = reach * reach;
		for (int i = bolts.Count - 1; i >= 0; i--)
		{
			EnemyProjectile bolt = bolts[i];
			if (bolt == null || !IsInstanceValid(bolt) || !bolt.InFlight)
				continue;
			if (bolt.GlobalPosition.DistanceSquaredTo(GlobalPosition) > r2)
				continue;
			bolt.Destroy(Element.Light);
			StatBoltsBurned++;
		}
	}
	public const int CoronaBeams = 8;
	private const float CoronaBeamReach = 2.6f;
	private const float CoronaBeamHalfWidth = 14f;
	private const float CoronaBeamShare = 0.75f;
	private float coronaTurn;

	// The beams grow with the flare they come out of: the reach and damage of THIS pulse.
	private void FireCoronaBeams(Player player, float reach, int pulseDamage)
	{
		if (SpellData == null || !SpellData.HasEvolution(CoronaDischargeId))
			return;
		coronaTurn += 0.41f;
		float length = reach * CoronaBeamReach;
		float halfWidth = CoronaBeamHalfWidth * MathF.Max(1f, AreaMultiplier);
		int beamDamage = Math.Max(1, Mathf.RoundToInt(pulseDamage * CoronaBeamShare));
		Vector2 origin = GlobalPosition;
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Node2D e || !IsInstanceValid(e))
				continue;
			Vector2 offset = e.GlobalPosition - origin;
			float distance = offset.Length();
			if (distance > length || distance < 1f)
				continue;
			for (int i = 0; i < CoronaBeams; i++)
			{
				Vector2 dir = Vector2.FromAngle(coronaTurn + Mathf.Tau * i / CoronaBeams);
				if (offset.Dot(dir) <= 0f || MathF.Abs(offset.Cross(dir)) > halfWidth)
					continue;
				player?.DealDamageToEnemy(e, beamDamage, source: SpellData);
				break;
			}
		}
		if (customVisual is SolarFlareVisual flare)
			flare.SetBeams(CoronaBeams, length, halfWidth, coronaTurn);
	}

	// SUN DOGS (solar_flare_radiance, .ai/spell-roster.md 2h, 2026-10-29). The card was Radiant Glow:
	// +40% area and 25% faster pulses, a bigger flare more often and nothing a player could point at. A
	// sun dog is the real thing - a mock sun either side of the sun, on the halo round it. Two hang either
	// side of the wizard on the line through his middle and go off with every pulse, each a flare of its
	// own SunDogReach of this pulse's reach, for SunDogDamageShare of its damage (and the flare's Weaken).
	// They hang SunDogOffset of the flare's reach out from him, so they drift outward as it builds while
	// he stands still: the charge shows in where they hang as well as in the pool at his feet. Between
	// pulses each is a small sun in the air; on a pulse, the flare's own burst (a SolarFlareVisual of its
	// own) drawn out of the radius it hits. The pull, the beams and the ward stay the flare's alone.
	public const string SunDogsId = "solar_flare_radiance";
	public const float SunDogOffset = 1.6f;
	public const float SunDogReach = 0.6f;
	public const float SunDogDamageShare = 0.6f;
	private Node2D sunDogGlow;
	private readonly SolarFlareVisual[] sunDogBursts = new SolarFlareVisual[2];
	private float sunDogClock;

	private bool HasSunDogs => SpellData != null && SpellData.HasEvolution(SunDogsId);

	private void TickSunDogs(float delta)
	{
		if (!HasSunDogs)
		{
			if (sunDogGlow != null && IsInstanceValid(sunDogGlow) && sunDogGlow.Visible)
				sunDogGlow.Visible = false;
			return;
		}
		EnsureSunDogs();
		sunDogClock += delta;
		sunDogGlow.Visible = true;
		sunDogGlow.QueueRedraw();
	}

	private void EnsureSunDogs()
	{
		if (sunDogGlow != null && IsInstanceValid(sunDogGlow))
			return;
		// In the air, as the flare is (DrawLayers): a mock sun hangs in the sky, over the bodies.
		sunDogGlow = new Node2D { Name = "SunDogs", ZAsRelative = false, ZIndex = DrawLayers.Air };
		AddChild(sunDogGlow);
		sunDogGlow.Draw += DrawSunDogs;
		for (int i = 0; i < sunDogBursts.Length; i++)
		{
			sunDogBursts[i] = new SolarFlareVisual { Name = $"SunDogBurst{i}", RayCount = 10, EmberCount = 8 };
			AddChild(sunDogBursts[i]);
		}
	}

	private void FlareSunDogs(Player player, float reach, int pulseDamage)
	{
		if (!HasSunDogs)
			return;
		EnsureSunDogs();
		float distance = reach * SunDogOffset;
		float dogReach = reach * SunDogReach;
		float r2 = dogReach * dogReach;
		int dogDamage = Math.Max(1, Mathf.RoundToInt(pulseDamage * SunDogDamageShare));
		for (int side = 0; side < 2; side++)
		{
			var offset = new Vector2(side == 0 ? -distance : distance, 0f);
			Vector2 centre = GlobalPosition + offset;
			foreach (Enemy e in EnemyRegistry.Snapshot)
			{
				if (!IsInstanceValid(e) || e.GlobalPosition.DistanceSquaredTo(centre) > r2)
					continue;
				player?.DealDamageToEnemy(e, dogDamage, source: SpellData);
				if (WeakenFraction > 0f)
					e.ApplyWeaken(WeakenFraction, WeakenDuration * DurationMultiplier);
			}
			SolarFlareVisual burst = sunDogBursts[side];
			if (burst != null && IsInstanceValid(burst))
			{
				burst.Position = offset;
				burst.Burst(dogReach);
			}
		}
	}

	// Each mock sun between pulses: a halo, a glow and a white-gold kernel with four short rays turning
	// slowly, throbbing on its own beat. Small on purpose - it says where the next flare goes off, and the
	// burst says how far it reaches (2c: no ring at the reach).
	private static readonly Color SunCore = new Color(1.0f, 0.96f, 0.76f);
	private static readonly Color SunFlare = new Color(1.0f, 0.60f, 0.13f);

	private void DrawSunDogs()
	{
		float distance = range * (1f + StillRadiusGrowth * StillCharge) * SunDogOffset;
		for (int side = 0; side < 2; side++)
		{
			var at = new Vector2(side == 0 ? -distance : distance, 0f);
			float throb = 0.5f + 0.5f * MathF.Sin(sunDogClock * 3.1f + side * 1.7f);
			float r = Mathf.Lerp(5f, 6.5f, throb);
			sunDogGlow.DrawCircle(at, r * 2.6f, new Color(SunFlare.R, SunFlare.G, SunFlare.B, 0.12f + 0.06f * throb));
			sunDogGlow.DrawCircle(at, r * 1.6f, new Color(SunFlare.R, SunFlare.G, SunFlare.B, 0.35f));
			float turn = sunDogClock * 0.8f + side * 0.6f;
			for (int k = 0; k < 4; k++)
			{
				Vector2 dir = Vector2.FromAngle(turn + Mathf.Tau * k / 4f);
				sunDogGlow.DrawLine(at + dir * r * 1.1f, at + dir * r * 2.3f, new Color(SunCore.R, SunCore.G, SunCore.B, 0.7f), 2f);
			}
			sunDogGlow.DrawCircle(at, r, new Color(SunCore.R, SunCore.G, SunCore.B, 0.95f));
		}
	}

	// SOLAR PROMINENCE (solar_flare_scorching_heat, 2h). The card was Scorching Heat: +45% damage and the
	// Fire tag, a hotter number. A prominence is the sun's own: a loop of burning gas thrown up off its
	// edge that falls back. Every pulse hurls ProminenceLoops of them off the flare's rim - one more at
	// half charge and another at full, so the sun held longest throws most - each at a different enemy
	// standing just past the flare's edge, nearest first, out to ProminenceReach of this pulse's reach.
	// Each arcs over and comes down on it in a burst of fire ProminenceBurstShare of the reach across, for
	// ProminenceShare of the pulse. The stand-still spell reaching the rank that hangs back from it. Drawn
	// in the air on a TopLevel canvas, so a loop in flight stays where it was thrown while the wizard walks
	// on; a fixed pool, and nothing allocated per pulse.
	public const string ProminenceId = "solar_flare_scorching_heat";
	public const int ProminenceLoops = 2;
	public const float ProminenceReach = 2.4f;
	public const float ProminenceShare = 0.8f;
	public const float ProminenceBurstShare = 0.4f;
	// Past this share of the reach (the flare already burns what is inside it), and thrown from this share
	// out from the wizard's middle.
	private const float ProminenceInner = 1.1f;
	private const float ProminenceRoot = 0.35f;
	private const float ProminenceFlight = 0.4f;
	private const float ProminenceFade = 0.14f;
	private const int MaxProminences = 16;

	private struct Prominence
	{
		public Vector2 From;
		public Vector2 To;
		public float Height;
		public float Age;
		public float Burst;
		public int Damage;
		public bool Landed;
		public bool Live;
	}

	private readonly Prominence[] prominences = new Prominence[MaxProminences];
	private readonly Enemy[] prominenceTargets = new Enemy[4];
	private readonly float[] prominenceDistances = new float[4];
	private int nextProminence;
	private bool prominencesDrawn;
	private Node2D prominenceCanvas;
	private static readonly ElementColors.Ramp FireRamp = ElementColors.GetRamp(Element.Fire);

	private void HurlProminences(float reach, int pulseDamage, float charge)
	{
		if (SpellData == null || !SpellData.HasEvolution(ProminenceId))
			return;
		int loops = Math.Min(prominenceTargets.Length, ProminenceLoops + (charge >= 0.5f ? 1 : 0) + (charge >= 0.999f ? 1 : 0));
		// The nearest few past the rim, nearest first, kept in fixed slots.
		int found = 0;
		float outer = reach * ProminenceReach;
		Vector2 origin = GlobalPosition;
		foreach (Enemy e in EnemyRegistry.Snapshot)
		{
			if (!IsInstanceValid(e) || e.IsDying)
				continue;
			float d = origin.DistanceTo(e.GlobalPosition);
			if (d <= reach * ProminenceInner || d > outer)
				continue;
			int slot;
			if (found < loops)
				slot = found++;
			else if (d < prominenceDistances[loops - 1])
				slot = loops - 1;
			else
				continue;
			while (slot > 0 && prominenceDistances[slot - 1] > d)
			{
				prominenceTargets[slot] = prominenceTargets[slot - 1];
				prominenceDistances[slot] = prominenceDistances[slot - 1];
				slot--;
			}
			prominenceTargets[slot] = e;
			prominenceDistances[slot] = d;
		}
		if (found == 0)
			return;

		EnsureProminenceCanvas();
		int damage = Math.Max(1, Mathf.RoundToInt(pulseDamage * ProminenceShare));
		float burst = reach * ProminenceBurstShare;
		for (int i = 0; i < found; i++)
		{
			Enemy target = prominenceTargets[i];
			prominenceTargets[i] = null;
			// Led by where it is walking for as long as the loop is in the air, as a meteor is.
			Vector2 to = target.GlobalPosition + target.Velocity * ProminenceFlight;
			Vector2 dir = (to - origin).Normalized();
			// Thrown up from the body of the sun, not its rim: a loop that left the rim for an enemy a step
			// past it went straight up and straight down, a spark and not an arch.
			Vector2 from = origin + dir * reach * ProminenceRoot;
			prominences[nextProminence] = new Prominence
			{
				From = from,
				To = to,
				Height = 22f + 0.5f * from.DistanceTo(to),
				Burst = burst,
				Damage = damage,
				Live = true,
			};
			nextProminence = (nextProminence + 1) % MaxProminences;
		}
	}

	private void EnsureProminenceCanvas()
	{
		if (prominenceCanvas != null && IsInstanceValid(prominenceCanvas))
			return;
		prominenceCanvas = new Node2D { Name = "Prominences", TopLevel = true, ZAsRelative = false, ZIndex = DrawLayers.Air };
		AddChild(prominenceCanvas);
		prominenceCanvas.GlobalPosition = Vector2.Zero;
		prominenceCanvas.Draw += DrawProminences;
	}

	private void TickProminences(float delta)
	{
		if (prominenceCanvas == null || !IsInstanceValid(prominenceCanvas))
			return;
		bool any = false;
		for (int i = 0; i < MaxProminences; i++)
		{
			Prominence p = prominences[i];
			if (!p.Live)
				continue;
			any = true;
			p.Age += delta;
			if (!p.Landed && p.Age >= ProminenceFlight)
			{
				p.Landed = true;
				LandProminence(p);
			}
			if (p.Age >= ProminenceFlight + ProminenceFade)
				p.Live = false;
			prominences[i] = p;
		}
		// One more redraw after the last has gone, to clear it.
		if (any || prominencesDrawn)
			prominenceCanvas.QueueRedraw();
		prominencesDrawn = any;
	}

	private void LandProminence(in Prominence p)
	{
		if (PlayerRef is not Player player || !IsInstanceValid(player))
			return;
		float r2 = p.Burst * p.Burst;
		foreach (Enemy e in EnemyRegistry.Snapshot)
		{
			if (IsInstanceValid(e) && e.GlobalPosition.DistanceSquaredTo(p.To) <= r2)
				player.DealDamageToEnemy(e, p.Damage, source: SpellData);
		}
		if (player.GetParent() is Node2D arena)
			ImpactDebrisLayer.Spawn(arena, p.To, p.Burst, FireRamp, Colors.White);
	}

	// A loop: a lob from the rim to the mark, rising and falling, its head white-hot and its tail cooling
	// down the fire ramp, stamped on the 2 px grid. After it lands the tail drains into the burst.
	private Vector2 ArcPoint(in Prominence p, float u) =>
		p.From.Lerp(p.To, u) + new Vector2(0f, -MathF.Sin(MathF.PI * u) * p.Height);

	private void DrawProminences()
	{
		const int steps = 14;
		for (int i = 0; i < MaxProminences; i++)
		{
			Prominence p = prominences[i];
			if (!p.Live)
				continue;
			float head = Mathf.Clamp(p.Age / ProminenceFlight, 0f, 1f);
			float drained = p.Landed ? Mathf.Clamp((p.Age - ProminenceFlight) / ProminenceFade, 0f, 1f) : 0f;
			float tail = Mathf.Max(0f, head - 0.55f + 0.55f * drained);
			for (int s = 0; s <= steps; s++)
			{
				float w = s / (float)steps;
				float u = Mathf.Lerp(tail, head, w);
				Color c = w > 0.85f ? FireRamp.Core : w > 0.6f ? FireRamp.Hot : w > 0.3f ? FireRamp.Mid : FireRamp.Edge;
				float size = MathF.Max(WhipLimb.Grid, MathF.Round(Mathf.Lerp(4f, 10f, w) / WhipLimb.Grid) * WhipLimb.Grid);
				prominenceCanvas.DrawRect(WhipLimb.Cell(ArcPoint(p, u), size), c);
			}
		}
	}

	private void UpdateRingPulse(float delta)
	{
		if (!ringPulseActive || visual == null)
			return;

		ringPulseTimer += delta;
		float expand = Mathf.Max(0.01f, RingExpandDuration);
		float retract = Mathf.Max(0.01f, RingRetractDuration);
		float total = expand + retract;

		if (ringPulseTimer < expand)
		{
			float t = ringPulseTimer / expand;
			visual.Radius = Mathf.Lerp(2f, burstReach, t);
			visual.RingWidth = Mathf.Lerp(4f, 10f, t);
			visual.ShapeColor = new Color(VisualColor.R, VisualColor.G, VisualColor.B, Mathf.Lerp(0.55f, 0.9f, t) * VisualColor.A);
			visual.QueueRedraw();
			return;
		}

		if (ringPulseTimer < total)
		{
			float t = (ringPulseTimer - expand) / retract;
			visual.Radius = burstReach;
			visual.RingWidth = Mathf.Lerp(10f, 1f, t);
			visual.ShapeColor = new Color(VisualColor.R, VisualColor.G, VisualColor.B, Mathf.Lerp(0.9f, 0.12f, t) * VisualColor.A);
			visual.QueueRedraw();
			return;
		}

		ringPulseActive = false;
		visual.RingWidth = 0.01f;
		visual.ShapeColor = new Color(VisualColor.R, VisualColor.G, VisualColor.B, 0.0f);
		visual.QueueRedraw();
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 5) * DamageMultiplier));
		// The evolutions' Area as well as the player's. Radiant Glow and Daybreak promised a bigger
		// flare and this read only the player's, so both did nothing to its size until 2026-10-26.
		range = (SpellData?.GetRangeAtLevel(CurrentLevel) ?? 100f) * AreaMultiplier * (SpellData?.GetAreaMultiplierAtLevel(CurrentLevel) ?? 1f)
			* (1f + (SpellData?.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel) ?? 0f)); // the level table's Area line
		cooldown = MathF.Max(0.05f, (SpellData?.GetCooldownAtLevel(CurrentLevel) ?? 1.5f) * CooldownMultiplier);
	}
}
