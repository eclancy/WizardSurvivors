using Godot;
using System;

namespace WizardSurvivors.scripts;

// Shared script for simple "AoE pulse" offensive spells (issue #13 roster expansion): Solar Flare,
// Toxic Spore Burst. Behaves like ArcaneExplosion (auto-repeating pulse around the caster, one
// persistent instance per equipped spell) plus an optional guaranteed status effect applied to
// everything caught in the blast.
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
	[Export] public float RingExpandDuration { get; set; } = 0.22f;
	[Export] public float RingRetractDuration { get; set; } = 0.14f;
	[Export] public Color VisualColor { get; set; } = new Color(1.0f, 0.84f, 0.20f, 0.85f);

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
	// instead of the shared PlaceholderShape ring. Solar Flare does; Toxic Spore Burst still uses the ring.
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
		var player = PlayerRef as Player;
		// Iterate the "enemies" group, like every other AoE in the project. This used to walk
		// CurrentScene's *direct children* only, so any enemy parented to a spawner or container
		// was skipped entirely and silently took no damage from the pulse.
		foreach (var node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D e || !IsInstanceValid(e))
				continue;
			if (GlobalPosition.DistanceTo(e.GlobalPosition) > range)
				continue;

			player?.DealDamageToEnemy(e, damage);
			if (GuaranteedPoison && e.HasMethod("ApplyPoison"))
				e.Call("ApplyPoison", PoisonDamagePerTick, PoisonDuration);
		}

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
		range = (SpellData?.GetRangeAtLevel(CurrentLevel) ?? 100f) * AreaMultiplier;
		cooldown = MathF.Max(0.05f, (SpellData?.GetCooldownAtLevel(CurrentLevel) ?? 1.5f) * CooldownMultiplier);
	}
}
