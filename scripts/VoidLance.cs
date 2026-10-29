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
	private readonly HashSet<Node2D> hitThisPulse = new();
	private readonly HashSet<Node2D> hitOnEnter = new();

	// What the milestones make of this lance, read once it is cast.
	private bool glaive;
	private bool dread;
	private bool scarred;
	private bool collapse;
	private float collapseAge = -1f;
	private float collapseRadius;

	// THE LANCE IS DRAWN BY ONE SHADER ON ONE QUAD (scenes/shaders/void_lance.gdshader, 2026-10-29), out of
	// the numbers the hit test uses: the blade, its motes, Void Glaive's blade behind, Witching Hour's smear
	// and Cosmic Collapse's hole. It was a translucent antialiased Line2D under a Polygon2D with vertex
	// colours, motes drawn as smooth squares and the hole as a DrawCircle and two DrawArcs - off the
	// Bonelight contract at every angle but four (.ai/art-direction.md sections 1 and 3).
	private static Shader lanceShader;
	private static readonly StringName ParamReach = "reach";
	private static readonly StringName ParamBackReach = "back_reach";
	private static readonly StringName ParamHalfWidth = "half_width";
	private static readonly StringName ParamHitRadius = "hit_radius";
	private static readonly StringName ParamTime = "time";
	private static readonly StringName ParamSeed = "seed";
	private static readonly StringName ParamHeat = "heat";
	private static readonly StringName ParamFade = "fade";
	private static readonly StringName ParamTrail = "trail";
	private static readonly StringName ParamHoleRadius = "hole_radius";
	private static readonly StringName ParamHoleT = "hole_t";
	private static readonly StringName ParamGilded = "gilded";
	private ShaderMaterial material;
	// Seconds since the lance was made: the shader's clock, which KeepAlive does not reset.
	private float age;
	// The quad as last drawn, in the node's own units; redrawn only when the lance's size changes.
	private Rect2 drawnQuad;

	// The thrust's white flash, and how long the lance takes to dissolve as it withdraws.
	private const float HeatSeconds = 0.1f;
	private const float FadeSeconds = 0.14f;
	// Witching Hour's smear: the ground swept in the last few hundredths of a second, as an arc that is
	// never longer than this many px at the tip.
	private const float TrailSeconds = 0.07f;
	private const float TrailMaxArc = 44f;

	public override void _Ready()
	{
		// The lance is in the air, over the bodies it runs through (DrawLayers).
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		lanceShader ??= GD.Load<Shader>("res://scenes/shaders/void_lance.gdshader");
		material = new ShaderMaterial { Shader = lanceShader };
		Material = material;
		// Two lances cast a moment apart should not pulse in step.
		material.SetShaderParameter(ParamSeed, (GetInstanceId() % 997) * 0.731f);
		RefreshComputedStats();
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
		CollapseAtTerminus();
		UpdateVisuals();
	}

	private void ReadMilestones()
	{
		glaive = SpellData?.HasEvolution(GlaiveId) == true;
		dread = SpellData?.HasEvolution(DreadId) == true;
		scarred = SpellData?.HasEvolution(ScarId) == true;
		collapse = SpellData?.HasEvolution(CollapseId) == true;
		material?.SetShaderParameter(ParamGilded, SpellData?.IsLegendary ?? false);
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
		UpdateVisuals();
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
		age += dt;
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

		if (collapseAge >= 0f)
			collapseAge += dt;

		UpdateVisuals();
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

	// Every number the shader draws from, in world px, out of the same lengths the hit test measures
	// (HitLength, HitRadius, Butt, the collapse's radius): the blade is the hit (void_lance.gdshader).
	private void UpdateVisuals()
	{
		if (material == null)
			return;
		float sx = MathF.Max(0.01f, MathF.Abs(Scale.X));
		float sy = MathF.Max(0.01f, MathF.Abs(Scale.Y));
		float reach = HitLength;
		float back = glaive ? reach * GlaiveBackShare : 0f;
		float hit = HitRadius;
		material.SetShaderParameter(ParamReach, reach);
		material.SetShaderParameter(ParamBackReach, back);
		material.SetShaderParameter(ParamHalfWidth, currentWidth * 0.5f * sy);
		material.SetShaderParameter(ParamHitRadius, hit);
		material.SetShaderParameter(ParamTime, age);
		// Witching Hour's hand flares once, when it is first thrust out, not at every cast that keeps it.
		material.SetShaderParameter(ParamHeat, MathF.Max(0f, 1f - (Clockwork ? age : lifetime) / HeatSeconds));
		material.SetShaderParameter(ParamFade, Mathf.Clamp((lifetime - (duration - FadeSeconds)) / FadeSeconds, 0f, 1f));
		// Witching Hour's smear: what the hand swept in the last TrailSeconds, held to TrailMaxArc at the
		// tip so a long hand does not drag a fan behind it.
		float trail = Clockwork ? MathF.Min(Mathf.Tau / WitchingTurnSeconds * TrailSeconds, TrailMaxArc / MathF.Max(1f, reach)) : 0f;
		material.SetShaderParameter(ParamTrail, trail);

		// Cosmic Collapse's hole: it swells to the radius its burst struck over the first 30% of
		// CollapseSeconds and caves in over the rest.
		float holeT = collapseAge >= 0f && collapseAge < CollapseSeconds ? collapseAge / CollapseSeconds : -1f;
		float hole = holeT < 0f ? 0f : collapseRadius * (holeT < 0.3f ? holeT / 0.3f : 1f - (holeT - 0.3f) / 0.7f);
		material.SetShaderParameter(ParamHoleT, holeT);
		material.SetShaderParameter(ParamHoleRadius, hole);

		// The one quad the shader paints, in the node's own units: the blade ahead and behind, the body's
		// allowance either side, the hole at its fullest, and the smear's arc behind a turning hand.
		float side = MathF.Max(hit, collapse ? collapseRadius : 0f);
		if (trail > 0f)
			side = MathF.Max(side, hit + MathF.Max(reach, back) * trail);
		float ahead = reach + (collapse ? collapseRadius : 0f) + 4f;
		var quad = new Rect2((-back - 4f) / sx, -(side + 4f) / sy, (back + ahead + 8f) / sx, (side + 4f) * 2f / sy);
		if (!quad.IsEqualApprox(drawnQuad))
		{
			drawnQuad = quad;
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		if (drawnQuad.Size.X > 0f)
			DrawRect(drawnQuad, Colors.White);
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 8) * DamageMultiplier));
		if (!Clockwork)
			duration = MathF.Max(0.16f, (BaseDuration + (SpellData?.GetEffectValueAtLevel(SpellEffect.ZoneDuration, CurrentLevel) ?? 0f)) * DurationMultiplier);
		currentLength = MathF.Min(420f, BaseLength + (Math.Max(0, CurrentLevel - 1) * LengthPerLevel));
		currentWidth = MathF.Max(8f, BaseWidth + (Math.Max(0, CurrentLevel - 1) * WidthPerLevel));
		UpdateVisuals();
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
