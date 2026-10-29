using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

public partial class VoidLance : Area2D
{
	// VOID LANCE'S CARDS, EACH ITS OWN (.ai/spell-roster.md section 2h, phase 2, 2026-10-29). Three of its
	// five were numbers on the lance - open longer, longer and wider, twice the size and +200% - and
	// Gravity Spike dragged the crowd onto it, which is Hollow Star's pull. Each now changes what the lance
	// does where it can be seen, out of what it is: a short-lived lance of shadow thrust out of the wizard's
	// hands, where his position is the aim. The ids, and the element each gains, are the ones they always
	// were, so saves carry over; the behaviour is switched on by id (SpellData.HasEvolution).

	/// <summary>Abyssal Scar: as the lance withdraws it leaves a scar of void along its line (VoidScars).</summary>
	public const string ScarId = "void_lance_abyssal_pierce";
	public const float ScarSeconds = 2.0f;
	public const float ScarTickSeconds = 0.4f;
	/// <summary>Of the lance's hit, every tick, to whatever stands on the scar.</summary>
	public const float ScarShare = 0.4f;

	/// <summary>Void Glaive: the lance runs out of both hands, ahead and behind.</summary>
	public const string GlaiveId = "void_lance_rift_blade";
	/// <summary>The blade behind, as a share of the lance ahead.</summary>
	public const float GlaiveBackShare = 0.75f;

	/// <summary>Dread: everything the lance runs through is frightened - it turns and runs (Enemy.ApplyFear).</summary>
	public const string DreadId = "void_lance_gravity_spike";
	public const float DreadSeconds = 1.6f;

	/// <summary>
	/// Witching Hour: the lance stays out and turns round the wizard like the hand of a clock, cutting
	/// everything it crosses (Player.FireVoidLanceSpell keeps the one lance alive).
	/// </summary>
	public const string WitchingHourId = "void_lance_annihilation_beam";
	/// <summary>One full turn round the wizard.</summary>
	public const float WitchingTurnSeconds = 2.6f;
	/// <summary>How long the hand stays out after the last cast that found an enemy in reach, in cooldowns.</summary>
	public const float WitchingKeepCooldowns = 2.5f;

	/// <summary>Cosmic Collapse: the lance ends in a black hole that collapses and bursts.</summary>
	public const string CollapseId = "void_lance_cosmic_collapse";

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

	/// <summary>True for Witching Hour's hand: it follows the wizard and turns until it is no longer kept.</summary>
	public bool Clockwork { get; private set; }

	private int damage = 8;
	private float duration = 0.72f;
	private float lifetime = 0f;
	private float damageTimer = 0f;
	private float currentLength = 280f;
	private float currentWidth = 18f;
	private Line2D lanceLine;
	private Polygon2D lanceFill;
	private Polygon2D lanceBack;
	private readonly HashSet<Node2D> hitThisPulse = new();
	private readonly HashSet<Node2D> hitOnEnter = new();
	private RandomNumberGenerator particleRng = new();
	private float particleSpawnTimer = 0f;

	// What the milestones make of this lance, read once it is cast.
	private bool glaive;
	private bool dread;
	private bool scarred;
	private bool collapse;
	private float collapseAge = -1f;
	private float collapseRadius;

	// The motes of void the lance sheds, drawn by the lance itself out of a fixed ring: the lance used to
	// make a Sprite2D and a Tween for every one, forty a second, which a hand that never withdraws
	// (Witching Hour) would have kept doing for the rest of the run.
	private const int MaxMotes = 40;
	private readonly Vector2[] moteAt = new Vector2[MaxMotes];
	private readonly float[] moteAge = new float[MaxMotes];
	private readonly float[] moteSize = new float[MaxMotes];
	private int nextMote;
	private Node2D overlay;

	private static readonly Color MoteColor = new(0.15f, 0.35f, 1f, 0.85f);
	private static readonly ElementColors.Ramp DarkRamp = ElementColors.GetRamp(Element.Darkness);
	private static readonly Color VoidBlack = new(0.03f, 0.01f, 0.06f, 0.95f);
	private const float MoteSeconds = 0.45f;

	public override void _Ready()
	{
		// The lance is in the air, over the bodies it runs through (DrawLayers).
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
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

		// Over the blade, as the sprites they replace were (z 2 inside the lance): the motes and the hole.
		overlay = new Node2D { Name = "Overlay", ZIndex = 2 };
		AddChild(overlay);
		overlay.Draw += DrawOverlay;

		particleRng.Randomize();
		UpdateVisuals();
	}

	public override void _ExitTree()
	{
		if (overlay != null && IsInstanceValid(overlay))
			overlay.Draw -= DrawOverlay;
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
		ReadMilestones();
		UpdateLinePoints();
		UpdateVisuals();
		CollapseAtTerminus();
	}

	private void ReadMilestones()
	{
		glaive = SpellData?.HasEvolution(GlaiveId) == true;
		dread = SpellData?.HasEvolution(DreadId) == true;
		scarred = SpellData?.HasEvolution(ScarId) == true;
		collapse = SpellData?.HasEvolution(CollapseId) == true;
		if (glaive && lanceBack == null)
		{
			lanceBack = new Polygon2D { Name = "LanceBack", ZIndex = 1 };
			AddChild(lanceBack);
		}
		if (lanceBack != null)
			lanceBack.Visible = glaive;
	}

	/// <summary>
	/// Witching Hour: this lance becomes the hand of a clock - it follows <paramref name="caster"/>, turns
	/// once round him every <see cref="WitchingTurnSeconds"/>, and lasts as long as it is kept.
	/// </summary>
	public void MakeClockwork(Node2D caster)
	{
		Clockwork = true;
		PlayerRef = caster;
	}

	/// <summary>Keeps Witching Hour's hand out for <paramref name="seconds"/> more; a scarred hand leaves its scar now.</summary>
	public void KeepAlive(float seconds)
	{
		lifetime = 0f;
		duration = MathF.Max(0.2f, seconds);
		// The dead leave the registry without ever leaving the hand, so a hand that lasts the run forgets
		// them here, once a cast, rather than holding every body it ever struck.
		hitOnEnter.RemoveWhere(IsGone);
		ReadMilestones();
		UpdateLinePoints();
		if (scarred)
			LeaveScar();
	}

	// The lance is drawn in its own space and an evolution's ScaleMultiplier (Cosmic Collapse) scales the
	// node, so the hit must be measured at the drawn size: it was measured at the unscaled one, and a x2
	// beam hurt only along its first half (fixed 2026-10-26).
	private float HitLength => currentLength * MathF.Abs(Scale.X);
	private float HitRadius => currentWidth * 0.5f * MathF.Abs(Scale.Y) + 6f;
	private Vector2 Terminus => GlobalPosition + (Vector2.Right * HitLength).Rotated(Rotation);
	// Void Glaive's blade behind the wizard is the lance's line run on past him the other way.
	private Vector2 Butt => glaive ? GlobalPosition - (Vector2.Right * HitLength * GlaiveBackShare).Rotated(Rotation) : GlobalPosition;

	// Cosmic Collapse: the lance ends in a black hole that bursts once, on the cast, for the lance's
	// damage across CollapseRadius times the evolutions' Area. It promised this and the lance had no
	// end blast at all until 2026-10-26; since 2026-10-29 the hole is drawn - a disc of void at the tip
	// that swells and then caves in on itself while the lance stands - where it was a spray of debris.
	private const float CollapseRadius = 60f;
	private const float CollapseSeconds = 0.5f;

	private void CollapseAtTerminus()
	{
		if (SpellData == null || !collapse || PlayerRef is not global::Player player
			|| GetParent() is not Node2D arena)
			return;
		Vector2 at = Terminus;
		float radius = CollapseRadius * SpellData.GetAreaMultiplierAtLevel(CurrentLevel);
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is Node2D enemy && IsInstanceValid(enemy) && at.DistanceTo(enemy.GlobalPosition) <= radius)
				player.DealDamageToEnemy(enemy, Math.Max(1, damage), source: SpellData);
		}
		ImpactDebrisLayer.Spawn(arena, at, radius, ElementColors.GetRamp(Element.Darkness), Colors.White);
		collapseAge = 0f;
		collapseRadius = radius;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		lifetime += dt;
		damageTimer += dt;
		if (Clockwork && PlayerRef != null && IsInstanceValid(PlayerRef))
		{
			GlobalPosition = PlayerRef.GlobalPosition;
			Rotation += Mathf.Tau / WitchingTurnSeconds * dt;
		}
		if (lifetime >= duration)
		{
			if (scarred && !Clockwork)
				LeaveScar();
			QueueFree();
			return;
		}

		ApplyEnterHits();
		if (damageTimer >= DamageInterval)
		{
			damageTimer = 0f;
			ApplyDamagePulse();
		}

		particleSpawnTimer += dt;
		while (particleSpawnTimer >= 0.025f)
		{
			particleSpawnTimer -= 0.025f;
			SpawnParticle();
		}
		for (int i = 0; i < MaxMotes; i++)
			moteAge[i] += dt;
		if (collapseAge >= 0f)
			collapseAge += dt;

		UpdateVisuals();
		overlay?.QueueRedraw();
	}

	// Abyssal Scar: the line the lance lay along, butt to tip, stopped short where the ground is water or
	// wall - the scar is on the ground, and nothing laid on the ground lies on blocked ground.
	private void LeaveScar()
	{
		if (PlayerRef is not global::Player player || GetParent() is not Node2D arena)
			return;
		Vector2 from = Butt;
		Vector2 to = Terminus;
		MazeNavigation nav = MazeNavigation.Active;
		if (nav != null)
		{
			// From the wizard outward each way, to the last open step.
			to = OpenAlong(nav, GlobalPosition, to);
			from = OpenAlong(nav, GlobalPosition, from);
		}
		if (from.DistanceSquaredTo(to) < 16f * 16f)
			return;
		VoidScars.For(arena, SpellData, player)?.Lay(from, to, ScarSeconds * DurationMultiplier,
			Math.Max(1, Mathf.RoundToInt(damage * ScarShare)));
	}

	private static Vector2 OpenAlong(MazeNavigation nav, Vector2 start, Vector2 end)
	{
		const float Step = 12f;
		float length = start.DistanceTo(end);
		if (length < 1f)
			return start;
		Vector2 dir = (end - start) / length;
		Vector2 last = start;
		for (float d = Step; d <= length; d += Step)
		{
			Vector2 p = start + dir * d;
			if (nav.IsBlockedAt(p))
				return last;
			last = p;
		}
		return end;
	}

	private void ApplyEnterHits()
	{
		Vector2 start = Butt;
		Vector2 end = Terminus;
		float radius = HitRadius;

		// Each body is struck as it comes inside the lance. A thrust is gone before anything leaves it and
		// comes back, so a body stays struck for the lance's life; Witching Hour's hand never goes, so a
		// body it has swept past is struck again when the hand comes round to it (a body that has left the
		// hand is let go of).
		foreach (Node enemyNode in EnemyRegistry.Snapshot)
		{
			if (enemyNode is not Node2D enemy || !IsInstanceValid(enemy) || !enemy.HasMethod("TakeDamage"))
				continue;

			float distance = DistanceToSegment(enemy.GlobalPosition, start, end);
			if (distance > radius)
			{
				if (Clockwork)
					hitOnEnter.Remove(enemy);
				continue;
			}

			if (hitOnEnter.Add(enemy))
				ApplyDamageAndPush(enemy);
		}
	}

	private void ApplyDamagePulse()
	{
		hitThisPulse.Clear();
		Vector2 start = Butt;
		Vector2 end = Terminus;
		float radius = HitRadius;

		foreach (Node enemyNode in EnemyRegistry.Snapshot)
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
		// With its source, which it went without until 2026-10-28: unattributed, its damage was missing
		// from the balance report, its hits made no impact sound, and as a Darkness spell it could not
		// frighten (Player.TryFrighten, issue #69).
		if (PlayerRef is global::Player player)
			player.DealDamageToEnemy(enemy, Math.Max(1, damage), source: SpellData);

		// Dread: certain fear where the Darkness tag's is a chance. Enemy.ApplyFear keeps its immunities -
		// elites, minibosses and bosses never, and nothing twice inside four seconds.
		if (dread && enemy is Enemy frightened)
			frightened.ApplyFear(DreadSeconds);

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

	private void UpdateLinePoints()
	{
		if (lanceLine == null)
			return;
		float back = glaive ? -currentLength * GlaiveBackShare : 0f;
		lanceLine.Points = new[] { new Vector2(back, 0f), new Vector2(currentLength, 0f) };
	}

	private void UpdateVisuals()
	{
		if (lanceLine == null)
			return;

		// A hand that never withdraws (Witching Hour) still breathes, but never fades to a ghost of itself.
		float pulseWave = 0.65f + (0.35f * (1.0f + Mathf.Sin(lifetime * PulseFrequency * 1.25f)) * 0.5f);
		float pulseScale = Mathf.Clamp(pulseWave, 0.55f, 1.35f);
		float ghostAlpha = 0.32f + (0.38f * (0.5f + 0.5f * Mathf.Sin(lifetime * PulseFrequency * 1.1f)));
		if (Clockwork)
			ghostAlpha = MathF.Max(ghostAlpha, 0.55f);
		lanceLine.Width = MathF.Max(2f, currentWidth * 0.18f * pulseScale);
		lanceLine.DefaultColor = new Color(0.04f, 0.02f, 0.10f, ghostAlpha);
		lanceLine.Modulate = new Color(0.78f, 0.22f, 1.0f, ghostAlpha + 0.12f);

		if (lanceFill == null)
			return;

		var polygon = BuildLancePolygon(currentLength, currentWidth * pulseScale, 1f);
		lanceFill.Polygon = polygon;
		lanceFill.VertexColors = BuildLanceVertexColors(polygon, ghostAlpha);
		lanceFill.Modulate = new Color(0.88f, 0.72f, 1.0f, ghostAlpha + 0.2f);
		if (glaive && lanceBack != null)
		{
			// The same blade, turned back on itself and shorter: drawn by the same builder mirrored.
			var backPolygon = BuildLancePolygon(currentLength * GlaiveBackShare, currentWidth * pulseScale, -1f);
			lanceBack.Polygon = backPolygon;
			lanceBack.VertexColors = BuildLanceVertexColors(backPolygon, ghostAlpha);
			lanceBack.Modulate = lanceFill.Modulate;
		}
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 8) * DamageMultiplier));
		if (!Clockwork)
			duration = MathF.Max(0.16f, (BaseDuration + (SpellData?.GetEffectValueAtLevel(SpellEffect.ZoneDuration, CurrentLevel) ?? 0f)) * DurationMultiplier);
		currentLength = MathF.Min(420f, BaseLength + (Math.Max(0, CurrentLevel - 1) * LengthPerLevel));
		currentWidth = MathF.Max(8f, BaseWidth + (Math.Max(0, CurrentLevel - 1) * WidthPerLevel));
		UpdateLinePoints();
		UpdateVisuals();
	}

	// side is 1 for the lance ahead and -1 for Void Glaive's blade behind (mirrored along the line).
	private Vector2[] BuildLancePolygon(float length, float width, float side)
	{
		float halfWidth = MathF.Max(8f, width * 0.5f);
		float frontWidth = MathF.Max(4f, halfWidth * 0.35f);
		float midWidth = MathF.Max(frontWidth, halfWidth);
		float backWidth = MathF.Max(4f, halfWidth * 0.32f);
		float taperStart = length * 0.22f;
		float taperEnd = length * 0.78f;
		var points = new[]
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
		if (side < 0f)
		{
			for (int i = 0; i < points.Length; i++)
				points[i] = new Vector2(-points[i].X, points[i].Y);
		}
		return points;
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

	private void SpawnParticle()
	{
		float back = glaive ? currentLength * GlaiveBackShare : 0f;
		float progress = particleRng.RandfRange(-back, currentLength);
		float offsetY = particleRng.RandfRange(-currentWidth * 0.5f, currentWidth * 0.5f);
		moteAt[nextMote] = new Vector2(progress, offsetY);
		moteAge[nextMote] = 0f;
		moteSize[nextMote] = particleRng.RandfRange(0.6f, 1.2f) * 6f;
		nextMote = (nextMote + 1) % MaxMotes;
	}

	private void DrawOverlay()
	{
		// The motes: each a small square of void-blue that shrinks and fades over MoteSeconds, as the
		// tweened sprites did. Drawn in the lance's space, so they turn with Witching Hour's hand.
		for (int i = 0; i < MaxMotes; i++)
		{
			float life = 1f - moteAge[i] / MoteSeconds;
			if (life <= 0f || moteSize[i] <= 0f)
				continue;
			float size = moteSize[i] * life;
			overlay.DrawRect(new Rect2(moteAt[i], new Vector2(size, size)), MoteColor with { A = MoteColor.A * life });
		}

		// Cosmic Collapse's hole, at the tip: a disc of void ringed in the element's light that swells to
		// the reach it struck and then caves in on itself, its rim brightest as it closes.
		if (collapseAge >= 0f && collapseAge < CollapseSeconds && collapseRadius > 0f)
		{
			float t = collapseAge / CollapseSeconds;
			float grow = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
			float sx = MathF.Max(0.01f, MathF.Abs(Scale.X));
			float r = collapseRadius / sx * MathF.Max(0f, grow);
			Vector2 tip = new Vector2(currentLength, 0f);
			if (r > 0.5f)
			{
				overlay.DrawCircle(tip, r, VoidBlack with { A = 0.85f });
				overlay.DrawArc(tip, r, 0f, Mathf.Tau, 32, DarkRamp.Hot with { A = 0.6f + 0.4f * t }, MathF.Max(1.5f, 3f / sx));
				overlay.DrawArc(tip, r * 0.62f, 0f, Mathf.Tau, 24, DarkRamp.Mid with { A = 0.5f * (1f - t) }, MathF.Max(1f, 2f / sx));
			}
		}
	}

	private static readonly Predicate<Node2D> IsGone = node => !IsInstanceValid(node) || (node is Enemy e && e.IsDying);

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
