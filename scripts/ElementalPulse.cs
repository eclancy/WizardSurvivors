using Godot;
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

	// Blinding Flash (solar_flare_blinding_flash): the chance a pulse staggers and shoves what it
	// hits, and how long the stagger holds. Its KnockbackBonus is the shove (SpellData.GetKnockbackSpeedAtLevel).
	private const float BlindingFlashChance = 0.3f;
	private const float BlindingFlashStunSeconds = 0.4f;

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

	public override void _Process(double delta)
	{
		if (PlayerRef != null)
			GlobalPosition = PlayerRef.GlobalPosition;

		UpdateRingPulse((float)delta);

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
		// Iterate the "enemies" group, like every other AoE in the project. This used to walk
		// CurrentScene's *direct children* only, so any enemy parented to a spawner or container
		// was skipped entirely and silently took no damage from the pulse.
		foreach (var node in EnemyRegistry.Snapshot)
		{
			if (node is not Node2D e || !IsInstanceValid(e))
				continue;
			if (GlobalPosition.DistanceTo(e.GlobalPosition) > range)
				continue;

			player?.DealDamageToEnemy(e, damage, source: SpellData);
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

		FireCoronaBeams(player);

		if (particles != null)
			particles.Restart();

		if (customVisual != null)
		{
			// A bespoke visual owns its own animation and timing, so the generic ring tween below
			// (and the ringPulseActive state it drives) is skipped entirely for those spells.
			customVisual.Burst(range);
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

		ringPulseTimer = 0f;
		ringPulseActive = true;
	}

	// Corona Discharge (solar_flare_corona_discharge): every pulse also throws CoronaBeams sunbeams out
	// past the flare, evenly spaced and turned a little each pulse, each striking everything along it
	// for CoronaBeamShare of the pulse. The card promised eight sunbeams from the start and the pulse
	// fired none; then it was damage alone (2026-10-26, morning), which is not what an Ascension should
	// feel like. Built that afternoon. SolarFlareVisual draws the beams at exactly this length and width.
	public const string CoronaDischargeId = "solar_flare_corona_discharge";
	public const int CoronaBeams = 8;
	private const float CoronaBeamReach = 2.6f;
	private const float CoronaBeamHalfWidth = 14f;
	private const float CoronaBeamShare = 0.75f;
	private float coronaTurn;

	private void FireCoronaBeams(Player player)
	{
		if (SpellData == null || !SpellData.HasEvolution(CoronaDischargeId))
			return;
		coronaTurn += 0.41f;
		float length = range * CoronaBeamReach;
		float halfWidth = CoronaBeamHalfWidth * MathF.Max(1f, AreaMultiplier);
		int beamDamage = Math.Max(1, Mathf.RoundToInt(damage * CoronaBeamShare));
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
			visual.Radius = Mathf.Lerp(2f, range, t);
			visual.RingWidth = Mathf.Lerp(4f, 10f, t);
			visual.ShapeColor = new Color(VisualColor.R, VisualColor.G, VisualColor.B, Mathf.Lerp(0.55f, 0.9f, t) * VisualColor.A);
			visual.QueueRedraw();
			return;
		}

		if (ringPulseTimer < total)
		{
			float t = (ringPulseTimer - expand) / retract;
			visual.Radius = range;
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
