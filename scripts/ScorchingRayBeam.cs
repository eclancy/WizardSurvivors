using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts;

// Scorching Ray beam: extends from caster to target, then retracts into the target. Damage is
// applied once when the beam fully reaches the target.
public partial class ScorchingRayBeam : Node2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float ExtendDuration { get; set; } = 0.11f;
	[Export] public float RetractDuration { get; set; } = 0.10f;
	[Export] public float HitFallbackRadius { get; set; } = 26f;

	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public Player? PlayerRef { get; set; }

	private enum BeamPhase
	{
		Idle,
		Extending,
		Retracting
	}

	private BeamPhase phase = BeamPhase.Idle;
	private float phaseTimer = 0f;
	private int damage = 1;
	private Vector2 origin = Vector2.Zero;
	private Vector2 lockedTargetPosition = Vector2.Zero;
	private Node2D? targetNode;
	private bool didDamage = false;
	private Line2D? beamLine;
	private Line2D? heatLine;
	private Sprite2D? impactSprite;
	private float shimmerTime = 0f;

	public override void _Ready()
	{
		beamLine = GetNodeOrNull<Line2D>("BeamLine");
		heatLine = GetNodeOrNull<Line2D>("HeatLine");
		impactSprite = GetNodeOrNull<Sprite2D>("ImpactSprite");
		RefreshComputedStats();
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	public void Fire(Vector2 from, Node2D? target, Vector2 fallbackTargetPosition)
	{
		origin = from;
		targetNode = target;
		lockedTargetPosition = target != null && IsInstanceValid(target) ? target.GlobalPosition : fallbackTargetPosition;
		didDamage = false;
		phase = BeamPhase.Extending;
		phaseTimer = 0f;
		UpdateVisual(origin, origin, 0f);
	}

	public override void _Process(double delta)
	{
		if (phase == BeamPhase.Idle)
			return;

		shimmerTime += (float)delta;

		Vector2 targetPos = GetTargetPosition();

		if (phase == BeamPhase.Extending)
		{
			phaseTimer += (float)delta;
			float t = Mathf.Clamp(phaseTimer / Mathf.Max(0.01f, ExtendDuration), 0f, 1f);
			Vector2 beamEnd = origin.Lerp(targetPos, t);
			UpdateVisual(origin, beamEnd, t);

			if (t >= 1f)
			{
				ApplyDamageAtImpact(targetPos);
				phase = BeamPhase.Retracting;
				phaseTimer = 0f;
			}
			return;
		}

		if (phase == BeamPhase.Retracting)
		{
			phaseTimer += (float)delta;
			float t = Mathf.Clamp(phaseTimer / Mathf.Max(0.01f, RetractDuration), 0f, 1f);
			Vector2 beamStart = origin.Lerp(targetPos, t);
			UpdateVisual(beamStart, targetPos, 1f - t);
			if (t >= 1f)
				QueueFree();
		}
	}

	private Vector2 GetTargetPosition()
	{
		if (targetNode != null && IsInstanceValid(targetNode))
			lockedTargetPosition = targetNode.GlobalPosition;
		return lockedTargetPosition;
	}

	private void ApplyDamageAtImpact(Vector2 impactPosition)
	{
		if (didDamage)
			return;

		didDamage = true;
		if (PlayerRef == null)
			return;

		float critBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.CritChance, CurrentLevel) ?? 0f;
		if (targetNode != null && IsInstanceValid(targetNode) && targetNode.IsInGroup("enemies") && targetNode.HasMethod("TakeDamage"))
		{
			PlayerRef.DealDamageToEnemy(targetNode, damage, critBonus);
			return;
		}

		Node2D? fallbackEnemy = GetTree().GetNodesInGroup("enemies")
			.OfType<Node2D>()
			.Where(e => IsInstanceValid(e) && e.HasMethod("TakeDamage") && impactPosition.DistanceTo(e.GlobalPosition) <= HitFallbackRadius)
			.OrderBy(e => impactPosition.DistanceTo(e.GlobalPosition))
			.FirstOrDefault();
		if (fallbackEnemy != null)
			PlayerRef.DealDamageToEnemy(fallbackEnemy, damage, critBonus);
	}

	private void UpdateVisual(Vector2 from, Vector2 to, float alphaScale)
	{
		float clampedAlpha = Mathf.Clamp(alphaScale, 0f, 1f);
		Vector2 beamDir = (to - from).Normalized();
		Vector2 normal = new Vector2(-beamDir.Y, beamDir.X);
		float wobble = Mathf.Sin(shimmerTime * 42f) * Mathf.Lerp(0.4f, 2.2f, clampedAlpha);
		Vector2 wobbleOffset = normal * wobble;

		if (beamLine != null)
		{
			beamLine.SetPointPosition(0, ToLocal(from));
			beamLine.SetPointPosition(1, ToLocal(to));
			beamLine.Width = Mathf.Lerp(7f, 12f, clampedAlpha);
			beamLine.Modulate = new Color(1f, 0.34f, 0.1f, 0.95f * clampedAlpha);
		}

		if (heatLine != null)
		{
			heatLine.SetPointPosition(0, ToLocal(from + wobbleOffset));
			heatLine.SetPointPosition(1, ToLocal(to + wobbleOffset));
			heatLine.Width = Mathf.Lerp(10f, 15f, clampedAlpha);
			heatLine.Modulate = new Color(1f, 0.72f, 0.22f, 0.45f * clampedAlpha);
		}

		if (impactSprite != null)
		{
			impactSprite.Visible = true;
			impactSprite.GlobalPosition = to;
			impactSprite.Rotation += 0.55f;
			float scale = Mathf.Lerp(0.30f, 0.52f, clampedAlpha);
			impactSprite.Scale = new Vector2(scale, scale);
			impactSprite.Modulate = new Color(1f, 0.72f, 0.42f, 0.95f * clampedAlpha);
		}
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 3) * DamageMultiplier));
	}
}
