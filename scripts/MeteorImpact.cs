using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts;

// Meteor Swarm impact visual: two red telegraph circles expand, then a sky icon drops into the
// center and applies AoE damage on impact.
public partial class MeteorImpact : Node2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float TelegraphDuration { get; set; } = 0.9f;
	[Export] public float BaseExplosionRadius { get; set; } = 60f;
	[Export] public float FallStartHeight { get; set; } = 260f;
	[Export] public float FallStartFraction { get; set; } = 0.58f;

	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;

	private int damage = 8;
	private float explosionRadius = 60f;
	private float telegraphTimer = 0f;
	private bool impacted = false;
	private Vector2 impactPosition = Vector2.Zero;

	private PlaceholderShape? telegraphOuter;
	private PlaceholderShape? telegraphInner;
	private Sprite2D? fallingMeteor;

	public override void _Ready()
	{
		telegraphOuter = GetNodeOrNull<PlaceholderShape>("TelegraphOuter");
		telegraphInner = GetNodeOrNull<PlaceholderShape>("TelegraphInner");
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
			return;

		telegraphTimer += (float)delta;
		float totalDuration = Mathf.Max(0.05f, TelegraphDuration);
		float t = Mathf.Clamp(telegraphTimer / totalDuration, 0f, 1f);

		UpdateTelegraph(t);
		UpdateFallingMeteor(t);

		if (telegraphTimer >= totalDuration)
			Impact();
	}

	private void UpdateTelegraph(float t)
	{
		float outerRadius = Mathf.Lerp(10f, explosionRadius, t);
		float innerRadius = Mathf.Lerp(6f, explosionRadius * 0.62f, t);

		if (telegraphOuter != null)
		{
			telegraphOuter.Visible = true;
			telegraphOuter.Ring = true;
			telegraphOuter.Radius = outerRadius;
			telegraphOuter.RingWidth = Mathf.Lerp(3.5f, 7.5f, t);
			telegraphOuter.ShapeColor = new Color(1f, 0.12f, 0.08f, Mathf.Lerp(0.35f, 0.82f, t));
			telegraphOuter.QueueRedraw();
		}

		if (telegraphInner != null)
		{
			telegraphInner.Visible = true;
			telegraphInner.Ring = true;
			telegraphInner.Radius = innerRadius;
			telegraphInner.RingWidth = Mathf.Lerp(2.5f, 5.5f, t);
			telegraphInner.ShapeColor = new Color(1f, 0.22f, 0.12f, Mathf.Lerp(0.28f, 0.74f, t));
			telegraphInner.QueueRedraw();
		}
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

		var parent = GetTree().CurrentScene;
		var enemies = parent?.GetChildren().OfType<Node2D>().Where(n => n.IsInGroup("enemies")) ?? Enumerable.Empty<Node2D>();
		var player = PlayerRef as Player;
		foreach (var enemy in enemies)
		{
			if (impactPosition.DistanceTo(enemy.GlobalPosition) <= explosionRadius)
				player?.DealDamageToEnemy(enemy, damage);
		}

		var burst = GetNodeOrNull<GpuParticles2D>("ExplosionParticles");
		if (burst != null)
		{
			burst.Emitting = true;
			burst.Restart();
		}

		if (fallingMeteor != null)
			fallingMeteor.Visible = false;
		if (telegraphOuter != null)
			telegraphOuter.Visible = false;
		if (telegraphInner != null)
			telegraphInner.Visible = false;

		var timer = GetTree().CreateTimer(0.4);
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

		if (telegraphOuter != null)
		{
			telegraphOuter.Visible = true;
			telegraphOuter.Ring = true;
			telegraphOuter.Radius = 10f;
			telegraphOuter.RingWidth = 3.5f;
			telegraphOuter.ShapeColor = new Color(1f, 0.12f, 0.08f, 0.35f);
			telegraphOuter.QueueRedraw();
		}

		if (telegraphInner != null)
		{
			telegraphInner.Visible = true;
			telegraphInner.Ring = true;
			telegraphInner.Radius = 6f;
			telegraphInner.RingWidth = 2.5f;
			telegraphInner.ShapeColor = new Color(1f, 0.22f, 0.12f, 0.28f);
			telegraphInner.QueueRedraw();
		}
	}
}
