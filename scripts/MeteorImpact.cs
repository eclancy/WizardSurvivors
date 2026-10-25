using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts;

// Meteor Swarm impact: a meteor falls out of the sky onto a point, and on landing hits everything
// within explosionRadius and throws burning rock out to exactly that radius.
//
// It used to open two red rings at the impact point and grow them to the blast radius for the
// whole of the fall - a "this is where it will hit" area marker, the thing the playtest asked to be
// rid of (2026-10-23). The falling meteor is the warning now, and the debris (ImpactDebris) is the
// only thing that draws the reach: thrown at the moment of the hit, out to the radius it tested.
public partial class MeteorImpact : Node2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float TelegraphDuration { get; set; } = 0.9f;
	[Export] public float BaseExplosionRadius { get; set; } = 60f;
	[Export] public float FallStartHeight { get; set; } = 260f;
	// The meteor is the only warning now there are no rings, so it is in the sky for most of the wait
	// rather than appearing for its last 0.4 s.
	[Export] public float FallStartFraction { get; set; } = 0.2f;

	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;

	private int damage = 8;
	private float explosionRadius = 60f;
	private float telegraphTimer = 0f;
	private bool impacted = false;
	private float sinceImpact = 0f;
	private Vector2 impactPosition = Vector2.Zero;
	private static readonly ElementColors.Ramp FireRamp = ElementColors.GetRamp(Element.Fire);

	private Sprite2D? fallingMeteor;

	public override void _Ready()
	{
		fallingMeteor = GetNodeOrNull<Sprite2D>("FallingMeteor");
		RefreshComputedStats();
		ResetVisualState();
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	public void CastAt(Vector2 position)
	{
		GlobalPosition = position;
		impactPosition = position;
		telegraphTimer = 0f;
		impacted = false;
		ResetVisualState();
	}

	public override void _Process(double delta)
	{
		if (impacted)
		{
			sinceImpact += (float)delta;
			QueueRedraw();
			return;
		}

		telegraphTimer += (float)delta;
		float totalDuration = Mathf.Max(0.05f, TelegraphDuration);
		float t = Mathf.Clamp(telegraphTimer / totalDuration, 0f, 1f);

		UpdateFallingMeteor(t);

		if (telegraphTimer >= totalDuration)
			Impact();
	}

	// Burning rock thrown out of the impact to exactly explosionRadius - the reach that was hit.
	public override void _Draw()
	{
		if (impacted)
			ImpactDebris.Draw(this, Vector2.Zero, explosionRadius, sinceImpact, (uint)(GetInstanceId() % 9973),
				FireRamp, ImpactDebris.CountFor(explosionRadius), Colors.White);
	}

	private void UpdateFallingMeteor(float t)
	{
		if (fallingMeteor == null)
			return;

		float startFraction = Mathf.Clamp(FallStartFraction, 0.05f, 0.95f);
		if (t < startFraction)
		{
			fallingMeteor.Visible = false;
			return;
		}

		float fallT = Mathf.Clamp((t - startFraction) / (1f - startFraction), 0f, 1f);
		float eased = 1f - Mathf.Pow(1f - fallT, 3f);
		Vector2 startPos = impactPosition + new Vector2(0f, -Mathf.Max(60f, FallStartHeight));
		fallingMeteor.Visible = true;
		fallingMeteor.GlobalPosition = startPos.Lerp(impactPosition, eased);
		fallingMeteor.Scale = Vector2.One * Mathf.Lerp(0.58f, 0.92f, eased);
		fallingMeteor.Rotation = Mathf.Lerp(-0.15f, 0.35f, eased);
		fallingMeteor.Modulate = new Color(1f, 0.78f, 0.5f, Mathf.Lerp(0.45f, 1f, eased));
	}

	private void Impact()
	{
		if (impacted)
			return;
		impacted = true;

		// The group, like every AoE (CLAUDE.md) - the same fix ConeBlast and GroundSpike had. The
		// scene's direct children are the enemies only when the run IS the scene, so under the balance
		// harness Meteor Swarm hit nothing. Read through EnemyRegistry, the group without the native
		// array (.ai/performance.md).
		var enemies = EnemyRegistry.Snapshot;
		var player = PlayerRef as Player;
		foreach (var enemy in enemies)
		{
			if (impactPosition.DistanceTo(enemy.GlobalPosition) <= explosionRadius)
				player?.DealDamageToEnemy(enemy, damage, source: SpellData);
		}

		var burst = GetNodeOrNull<GpuParticles2D>("ExplosionParticles");
		if (burst != null)
		{
			burst.Emitting = true;
			burst.Restart();
		}

		if (fallingMeteor != null)
			fallingMeteor.Visible = false;
		QueueRedraw();

		var timer = GetTree().CreateTimer(ImpactDebris.LifeSeconds + 0.02);
		timer.Timeout += () => { if (IsInstanceValid(this)) QueueFree(); };
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 8) * DamageMultiplier));
		explosionRadius = MathF.Max(8f, BaseExplosionRadius * AreaMultiplier);
	}

	private void ResetVisualState()
	{
		if (fallingMeteor != null)
		{
			fallingMeteor.Visible = false;
			fallingMeteor.Rotation = 0f;
			fallingMeteor.Scale = Vector2.One * 0.58f;
		}
	}
}
