using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

public partial class VoidLance : Area2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float BaseDuration { get; set; } = 0.72f;
	[Export] public float BaseLength { get; set; } = 280f;
	[Export] public float LengthPerLevel { get; set; } = 24f;
	[Export] public float BaseWidth { get; set; } = 18f;
	[Export] public float WidthPerLevel { get; set; } = 2.5f;
	[Export] public float PulseFrequency { get; set; } = 5f;
	[Export] public float DamageInterval { get; set; } = 0.75f;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float DurationMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef { get; set; }

	private int damage = 8;
	private float duration = 0.72f;
	private float lifetime = 0f;
	private float damageTimer = 0f;
	private float currentLength = 280f;
	private float currentWidth = 18f;
	private Line2D lanceLine;
	private Polygon2D lanceFill;
	private readonly HashSet<Node2D> hitThisPulse = new();
	private readonly HashSet<Node2D> hitOnEnter = new();
	private readonly List<Sprite2D> activeParticles = new();
	private RandomNumberGenerator particleRng = new();
	private Texture2D particleTexture;
	private float particleSpawnTimer = 0f;

	public override void _Ready()
	{
		RefreshComputedStats();
		lanceLine = GetNodeOrNull<Line2D>("LanceLine");
		if (lanceLine == null)
		{
			lanceLine = new Line2D { Name = "LanceLine" };
			AddChild(lanceLine);
		}
		lanceLine.Visible = true;
		lanceLine.Width = MathF.Max(2f, currentWidth * 0.18f);
		lanceLine.DefaultColor = new Color(0.06f, 0.03f, 0.12f, 0.72f);
		lanceLine.JointMode = Line2D.LineJointMode.Round;
		lanceLine.BeginCapMode = Line2D.LineCapMode.Round;
		lanceLine.EndCapMode = Line2D.LineCapMode.Round;
		lanceLine.Points = new[] { new Vector2(0f, 0f), new Vector2(currentLength, 0f) };

		lanceFill = GetNodeOrNull<Polygon2D>("LanceFill");
		if (lanceFill == null)
		{
			lanceFill = new Polygon2D { Name = "LanceFill" };
			AddChild(lanceFill);
		}
		lanceFill.ZIndex = 1;
		lanceFill.Color = new Color(0.12f, 0.16f, 0.42f, 0.95f);

		particleRng.Randomize();
		EnsureParticleTexture();
		UpdateVisuals();
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	public void CastFromPlayer(Node2D caster, Vector2 targetWorldPosition)
	{
		GlobalPosition = caster.GlobalPosition;
		Vector2 direction = targetWorldPosition - caster.GlobalPosition;
		if (direction.LengthSquared() < 0.001f)
			direction = Vector2.Right;
		else
			direction = direction.Normalized();

		Rotation = direction.Angle();
		lifetime = 0f;
		damageTimer = 0f;
		hitThisPulse.Clear();
		hitOnEnter.Clear();
		if (lanceLine != null)
			lanceLine.Points = new[] { new Vector2(0f, 0f), new Vector2(currentLength, 0f) };
		UpdateVisuals();
	}

	public override void _Process(double delta)
	{
		lifetime += (float)delta;
		damageTimer += (float)delta;
		if (lifetime >= duration)
		{
			QueueFree();
			return;
		}

		ApplyEnterHits();
		if (damageTimer >= DamageInterval)
		{
			damageTimer = 0f;
			ApplyDamagePulse();
		}

		particleSpawnTimer += (float)delta;
		while (particleSpawnTimer >= 0.025f)
		{
			particleSpawnTimer -= 0.025f;
			SpawnParticle();
		}

		UpdateVisuals();
	}

	private void ApplyEnterHits()
	{
		Vector2 start = GlobalPosition;
		Vector2 end = GlobalPosition + (Vector2.Right * currentLength).Rotated(Rotation);
		float radius = currentWidth * 0.5f + 6f;

		foreach (Node enemyNode in GetTree().GetNodesInGroup("enemies"))
		{
			if (enemyNode is not Node2D enemy || !IsInstanceValid(enemy) || !enemy.HasMethod("TakeDamage") || !hitOnEnter.Add(enemy))
				continue;

			float distance = DistanceToSegment(enemy.GlobalPosition, start, end);
			if (distance > radius)
			{
				hitOnEnter.Remove(enemy);
				continue;
			}

			ApplyDamageAndPush(enemy);
		}
	}

	private void ApplyDamagePulse()
	{
		hitThisPulse.Clear();
		Vector2 start = GlobalPosition;
		Vector2 end = GlobalPosition + (Vector2.Right * currentLength).Rotated(Rotation);
		float radius = currentWidth * 0.5f + 6f;

		foreach (Node enemyNode in GetTree().GetNodesInGroup("enemies"))
		{
			if (enemyNode is not Node2D enemy || !IsInstanceValid(enemy) || !enemy.HasMethod("TakeDamage"))
				continue;

			float distance = DistanceToSegment(enemy.GlobalPosition, start, end);
			if (distance <= radius)
				hitThisPulse.Add(enemy);
		}

		foreach (Node2D enemy in hitThisPulse)
			ApplyDamageAndPush(enemy);
	}

	private void ApplyDamageAndPush(Node2D enemy)
	{
		if (PlayerRef is global::Player player)
			player.DealDamageToEnemy(enemy, Math.Max(1, damage));

		if (enemy is CharacterBody2D body)
		{
			Vector2 toPlayer = (PlayerRef?.GlobalPosition ?? GlobalPosition) - enemy.GlobalPosition;
			Vector2 toPlayerDirection = toPlayer.LengthSquared() > 0.0001f ? toPlayer.Normalized() : -Vector2.Right.Rotated(Rotation);
			Vector2 lateralDirection = new Vector2(-toPlayerDirection.Y, toPlayerDirection.X);
			float lateralBias = Math.Sign(Mathf.Sin((enemy.GlobalPosition + GlobalPosition).Length() * 0.02f + lifetime * 2.5f));
			if (lateralBias == 0f)
				lateralBias = 1f;
			Vector2 pushDirection = lateralDirection * lateralBias;
			float pushStrength = MathF.Min(24f, 8f + currentWidth * 0.16f);
			float enemyMoveSpeed = 90f;
			Vector2 desiredMomentum = toPlayerDirection * MathF.Max(1f, enemyMoveSpeed * 0.35f);
			body.Velocity = desiredMomentum + pushDirection * pushStrength;
		}
	}

	private void UpdateVisuals()
	{
		if (lanceLine == null)
			return;

		float pulseWave = 0.65f + (0.35f * (1.0f + Mathf.Sin(lifetime * PulseFrequency * 1.25f)) * 0.5f);
		float pulseScale = Mathf.Clamp(pulseWave, 0.55f, 1.35f);
		float ghostAlpha = 0.32f + (0.38f * (0.5f + 0.5f * Mathf.Sin(lifetime * PulseFrequency * 1.1f)));
		lanceLine.Width = MathF.Max(2f, currentWidth * 0.18f * pulseScale);
		lanceLine.DefaultColor = new Color(0.04f, 0.02f, 0.10f, ghostAlpha);
		lanceLine.Modulate = new Color(0.78f, 0.22f, 1.0f, ghostAlpha + 0.12f);

		if (lanceFill == null)
			return;

		var polygon = BuildLancePolygon(currentLength, currentWidth * pulseScale);
		lanceFill.Polygon = polygon;
		lanceFill.VertexColors = BuildLanceVertexColors(polygon, ghostAlpha);
		lanceFill.Modulate = new Color(0.88f, 0.72f, 1.0f, ghostAlpha + 0.2f);
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 8) * DamageMultiplier));
		duration = MathF.Max(0.16f, (BaseDuration + (SpellData?.GetEffectValueAtLevel(SpellEffect.ZoneDuration, CurrentLevel) ?? 0f)) * DurationMultiplier);
		currentLength = MathF.Min(420f, BaseLength + (Math.Max(0, CurrentLevel - 1) * LengthPerLevel));
		currentWidth = MathF.Max(8f, BaseWidth + (Math.Max(0, CurrentLevel - 1) * WidthPerLevel));
		if (lanceLine != null)
			lanceLine.Points = new[] { new Vector2(0f, 0f), new Vector2(currentLength, 0f) };
		UpdateVisuals();
	}

	private Vector2[] BuildLancePolygon(float length, float width)
	{
		float halfWidth = MathF.Max(8f, width * 0.5f);
		float frontWidth = MathF.Max(4f, halfWidth * 0.35f);
		float midWidth = MathF.Max(frontWidth, halfWidth);
		float backWidth = MathF.Max(4f, halfWidth * 0.32f);
		float taperStart = length * 0.22f;
		float taperEnd = length * 0.78f;
		return new[]
		{
			new Vector2(0f, 0f),
			new Vector2(taperStart * 0.55f, -backWidth),
			new Vector2(taperStart, -midWidth * 0.55f),
			new Vector2(length * 0.34f, -midWidth * 0.78f),
			new Vector2(length * 0.5f, -midWidth),
			new Vector2(length * 0.66f, -midWidth * 0.78f),
			new Vector2(taperEnd, -midWidth * 0.55f),
			new Vector2(length * 0.92f, -frontWidth),
			new Vector2(length, 0f),
			new Vector2(length * 0.92f, frontWidth),
			new Vector2(taperEnd, midWidth * 0.55f),
			new Vector2(length * 0.66f, midWidth * 0.78f),
			new Vector2(length * 0.5f, midWidth),
			new Vector2(length * 0.34f, midWidth * 0.78f),
			new Vector2(taperStart, midWidth * 0.55f),
			new Vector2(taperStart * 0.55f, backWidth),
		};
	}

	private Color[] BuildLanceVertexColors(Vector2[] polygon, float ghostAlpha)
	{
		var colors = new Color[polygon.Length];
		float phase = lifetime * 2.4f;
		for (int i = 0; i < polygon.Length; i++)
		{
			float x = polygon[i].X / MathF.Max(1f, currentLength * 0.5f);
			float y = polygon[i].Y / MathF.Max(1f, currentWidth * 0.5f);
			float distanceFromCenter = MathF.Abs(x) * 0.65f + MathF.Abs(y) * 0.35f;
			float gradientT = Mathf.Clamp(1f - distanceFromCenter, 0f, 1f);
			float wave = 0.5f + 0.5f * Mathf.Sin(phase + x * 3.4f + y * 2.2f);
			float blueMix = Mathf.Clamp(0.35f + wave * 0.35f, 0.2f, 0.7f);
			float purpleMix = Mathf.Clamp(0.25f + wave * 0.35f, 0.15f, 0.7f);
			float alpha = Mathf.Clamp(ghostAlpha + (0.18f * wave), 0.12f, 0.75f);
			if (gradientT > 0.82f)
				colors[i] = new Color(0.08f + blueMix * 0.08f, 0.14f, 0.38f + blueMix * 0.16f, alpha);
			else if (gradientT > 0.45f)
				colors[i] = new Color(0.22f + purpleMix * 0.14f, 0.08f, 0.60f + purpleMix * 0.14f, alpha);
			else
				colors[i] = new Color(0f, 0f, 0f, Mathf.Clamp(alpha * 0.85f, 0.08f, 0.7f));
		}
		return colors;
	}

	private void EnsureParticleTexture()
	{
		if (particleTexture != null)
			return;

		var image = Image.Create(6, 6, false, Image.Format.Rgba8);
		image.Fill(new Color(1f, 1f, 1f, 1f));
		particleTexture = ImageTexture.CreateFromImage(image);
	}

	private void SpawnParticle()
	{
		EnsureParticleTexture();
		float progress = particleRng.RandfRange(0f, currentLength);
		float offsetY = particleRng.RandfRange(-currentWidth * 0.5f, currentWidth * 0.5f);
		var particle = new Sprite2D
		{
			Texture = particleTexture,
			Centered = false,
			Position = new Vector2(progress, offsetY),
			Scale = new Vector2(1f, 1f) * particleRng.RandfRange(0.6f, 1.2f),
			Modulate = new Color(0.15f, 0.35f, 1f, 0.85f),
			ZIndex = 2
		};
		AddChild(particle);
		activeParticles.Add(particle);

		var tween = CreateTween();
		tween.TweenProperty(particle, "modulate:a", 0f, 0.45f).SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(particle, "scale", Vector2.Zero, 0.45f).SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.Out);
		tween.TweenCallback(Callable.From(() =>
		{
			if (IsInstanceValid(particle))
			{
				activeParticles.Remove(particle);
				particle.QueueFree();
			}
		}));
	}

	private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
	{
		Vector2 ab = b - a;
		float lengthSquared = ab.LengthSquared();
		if (lengthSquared <= 0.0001f)
			return a;

		float t = Mathf.Clamp(((point - a).Dot(ab)) / lengthSquared, 0f, 1f);
		return a + ab * t;
	}

	private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
	{
		Vector2 projection = ClosestPointOnSegment(point, a, b);
		return (point - projection).Length();
	}
}
