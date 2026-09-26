using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

// Black Tentacles (Poison + Earth, issue #28): summons a stationary AoE zone at the target/impact
// point for a duration; enemies inside are slowed and take repeated damage ticks. New "placed
// zone" archetype - a targeted offensive cast rather than a self-centered passive pulse.
public partial class BlackTentacles : Node2D
{
	private const int TentaclesBackZIndex = -50;

	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float ZoneDuration { get; set; } = 3.0f;
	[Export] public float TickInterval { get; set; } = 0.5f;
	[Export] public float BaseRadius { get; set; } = 90f;
	[Export] public float PulseSpeed { get; set; } = 4.8f;
	[Export] public float PulseStrength { get; set; } = 0.07f;
	[Export] public float SlowMultiplier { get; set; } = 0.45f;
	[Export] public int WiggleLineCount { get; set; } = 7;
	[Export] public float WiggleWidth { get; set; } = 2.5f;
	public float CooldownMultiplier { get; set; } = 1.0f;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float DurationMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;

	private int damage = 3;
	private float radius = 55f;
	private float duration = 3.0f;
	private float elapsed = 0f;
	private float tickTimer = 0f;
	private float visualTime = 0f;
	private float scaledTickInterval = 0.5f;
	private float scaledSlowDuration = 1.2f;
	private readonly HashSet<Node2D> enemiesInside = new HashSet<Node2D>();

	// --- Visual state ---------------------------------------------------------------
	//
	// Which of the three faces this cast wears. Read once from the spell's selected level 8
	// evolution rather than every frame, because GetVisualTag walks the evolution objects.
	private enum Face { Pool, ElderGodMaw, ShadowForest }

	private Face face = Face.Pool;

	// A per-cast seed so two pools side by side are not the same blob rotated. The shape is
	// otherwise deterministic, which matters: a silhouette that reshuffles every frame reads as
	// static rather than as something alive.
	private float shapeSeed;

	// Preallocated. _Draw runs every frame for the whole duration and the old version allocated a
	// fresh Vector2[14] per arm per frame, which is exactly what CLAUDE.md says not to do in a
	// swarm-heavy effect.
	private const int BlobPoints = 30;
	private const int ArmSegments = 9;
	private readonly Vector2[] blobBuffer = new Vector2[BlobPoints];
	private readonly Vector2[] armBuffer = new Vector2[ArmSegments * 2];
	private readonly Vector2[] mawBuffer = new Vector2[22];

	// The darkness and poison element ramps from art-direction.md section 3, and occlusion. The
	// pool is a hole in the world, so its middle is the darkest value the palette has.
	private static readonly Color VoidCore = new Color(0.020f, 0.027f, 0.047f, 0.94f);   // occ
	private static readonly Color VoidEdge = new Color(0.208f, 0.125f, 0.361f, 0.88f);   // darkness edge
	private static readonly Color VoidMid = new Color(0.541f, 0.361f, 0.769f, 0.85f);    // darkness mid
	private static readonly Color VoidHot = new Color(0.706f, 0.549f, 0.910f, 0.95f);    // darkness hot
	private static readonly Color RotMid = new Color(0.706f, 0.878f, 0.290f, 0.85f);     // poison mid
	private static readonly Color RotHot = new Color(0.863f, 0.957f, 0.549f, 0.95f);     // poison hot

	public override void _Ready()
	{
		// Keep the zone visually underneath enemies regardless of insertion order.
		ZAsRelative = false;
		ZIndex = TentaclesBackZIndex;

		var rng = new RandomNumberGenerator();
		rng.Randomize();
		shapeSeed = rng.RandfRange(0f, Mathf.Tau);

		RefreshComputedStats();
	}

	// The ascension decides what this looks like, and the two level 8 branches are different
	// enough that they had to. "Elder God Maw" promising a gigantic devouring maw and then drawing
	// the same circle as every other cast is the card lying about what it bought.
	private void RefreshFace()
	{
		string tag = SpellData?.GetVisualTag() ?? string.Empty;
		face = tag switch
		{
			"ElderGodMaw" => Face.ElderGodMaw,
			"ShadowForest" => Face.ShadowForest,
			_ => Face.Pool,
		};
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
		RefreshFace();
	}

	public void CastAt(Vector2 position)
	{
		GlobalPosition = position;
		elapsed = 0f;
		tickTimer = 0f;
		visualTime = 0f;
		enemiesInside.Clear();
		UpdateEnemiesInsideAndApplyOnEntry();
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		elapsed += (float)delta;
		tickTimer += (float)delta;
		visualTime += (float)delta;
		UpdateEnemiesInsideAndApplyOnEntry();
		QueueRedraw();
		if (tickTimer >= scaledTickInterval)
		{
			tickTimer -= scaledTickInterval;
			Pulse();
		}
		if (elapsed >= duration)
			QueueFree();
	}

	public override void _Draw()
	{
		float pulse = 1f + Mathf.Sin(visualTime * PulseSpeed) * PulseStrength;
		float drawRadius = radius * pulse;

		switch (face)
		{
			case Face.ElderGodMaw:
				DrawElderGodMaw(drawRadius);
				break;
			case Face.ShadowForest:
				DrawShadowForest(drawRadius);
				break;
			default:
				DrawPool(drawRadius, Vector2.Zero, 1.0f, ArmsOnBasePool, shapeSeed);
				break;
		}
	}

	// --- The base pool --------------------------------------------------------------

	// A hole in the ground with things coming out of it.
	//
	// It used to be DrawCircle plus squiggles inside the disc, which is why it read as a uniform
	// circle with decoration rather than as anything alive: a perfect circle is the one silhouette
	// nothing organic has, and arms that never leave the disc are a pattern printed on the floor,
	// not limbs.
	//
	// So the rim is perturbed by three sine harmonics - the same trick the title screen's canopy
	// clearing uses, and for the same reason: one frequency reads as a wobble, three read as a
	// shape - and the arms now reach OUT past the rim, which is what makes them arms.
	private void DrawPool(float r, Vector2 centre, float scale, int arms, float seed)
	{
		float rr = r * scale;

		BuildBlob(blobBuffer, centre, rr, seed);
		DrawColoredPolygon(blobBuffer, VoidEdge);

		BuildBlob(blobBuffer, centre, rr * 0.82f, seed + 1.7f);
		DrawColoredPolygon(blobBuffer, VoidCore);

		// A broken rim light, so the hole has a lip. Unbroken, it reads as a drawn outline.
		BuildBlob(blobBuffer, centre, rr * 0.93f, seed);
		for (int i = 0; i < BlobPoints; i += 2)
			DrawLine(blobBuffer[i], blobBuffer[(i + 1) % BlobPoints], VoidMid, 2f);

		for (int i = 0; i < arms; i++)
			DrawArm(centre, rr, i, arms, seed);
	}

	// Radius perturbed by three harmonics plus a slow crawl, so the outline is irregular AND
	// alive without ever reshuffling.
	private void BuildBlob(Vector2[] buffer, Vector2 centre, float r, float seed)
	{
		for (int i = 0; i < BlobPoints; i++)
		{
			float a = Mathf.Tau * i / BlobPoints;
			float wobble = 1.0f
				+ Mathf.Sin(a * 3f + seed) * 0.13f
				+ Mathf.Sin(a * 5f - seed * 1.7f + visualTime * 0.8f) * 0.07f
				+ Mathf.Sin(a * 8f + seed * 0.4f - visualTime * 1.3f) * 0.04f;
			buffer[i] = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * wobble;
		}
	}

	// One arm: emerges, writhes, retracts, on its own stagger. Drawn as a tapered polygon rather
	// than a polyline, because a constant-width stroke reads as a wire and a taper reads as a limb.
	private void DrawArm(Vector2 centre, float r, int index, int armCount, float seed)
	{
		float stagger = index / (float)Math.Max(1, armCount);
		// Emergence runs 0 -> 1 -> 0 over its cycle, so arms are always at different lengths and
		// the pool never looks like a fixed set of spikes.
		float cycle = Mathf.PosMod(visualTime * 0.55f + stagger, 1.0f);
		float emerge = Mathf.Sin(cycle * Mathf.Pi);
		if (emerge <= 0.02f)
			return;

		float baseAngle = Mathf.Tau * stagger + seed * 0.6f;
		float length = r * (0.55f + 0.85f * emerge);
		float halfWidth = r * 0.115f * (0.6f + 0.4f * emerge);

		for (int j = 0; j < ArmSegments; j++)
		{
			float t = j / (float)(ArmSegments - 1);
			// The curl is what stops them being spokes.
			float curl = Mathf.Sin(t * 2.4f + visualTime * 2.1f + index * 1.31f) * 0.55f * t;
			float a = baseAngle + curl;
			Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
			Vector2 along = centre + dir * Mathf.Lerp(r * 0.25f, length, t);
			Vector2 side = dir.Orthogonal() * halfWidth * (1f - t * 0.92f);
			armBuffer[j] = along + side;
			armBuffer[ArmSegments * 2 - 1 - j] = along - side;
		}

		DrawColoredPolygon(armBuffer, VoidEdge);

		// A highlight down the lit side and a bead of rot at the tip - the only colour in the
		// whole effect, and the reason it reads as diseased rather than merely dark.
		for (int j = 0; j < ArmSegments - 1; j++)
			DrawLine(armBuffer[j], armBuffer[j + 1], VoidMid, 1.5f);

		Vector2 tip = armBuffer[ArmSegments - 1];
		DrawCircle(tip, Mathf.Max(1.6f, r * 0.035f), emerge > 0.7f ? RotHot : RotMid);
	}

	// --- Elder God Maw ---------------------------------------------------------------

	// The ascension is called Elder God Maw and describes a gigantic maw that devours what walks
	// into it, so it draws a face: a throat, two rows of teeth that open and close, and eyes.
	private void DrawElderGodMaw(float r)
	{
		// Fewer, heavier arms, framing rather than filling.
		DrawPool(r, Vector2.Zero, 1.0f, ArmsOnMaw, shapeSeed);

		// How wide the jaws are, breathing on a slower clock than the pool pulse so the mouth
		// does not look like it is merely scaling with the zone.
		float gape = 0.35f + 0.45f * (0.5f + 0.5f * Mathf.Sin(visualTime * 1.9f));
		float mouthW = r * 0.86f;
		float mouthH = r * 0.74f * gape;

		// The throat. Occlusion, because the inside of it is the darkest thing on the screen.
		int half = mawBuffer.Length / 2;
		for (int i = 0; i < half; i++)
		{
			float t = i / (float)(half - 1);
			float x = Mathf.Lerp(-mouthW, mouthW, t);
			float taper = Mathf.Sin(t * Mathf.Pi);
			mawBuffer[i] = new Vector2(x, -mouthH * taper);
			mawBuffer[mawBuffer.Length - 1 - i] = new Vector2(x, mouthH * taper);
		}
		DrawColoredPolygon(mawBuffer, VoidCore);

		// A lip around the opening. Without it the throat is the same value as the middle of the
		// pool it sits in, so the mouth has no edge and the teeth read as floating triangles
		// rather than as a bite.
		for (int i = 0; i < mawBuffer.Length; i++)
			DrawLine(mawBuffer[i], mawBuffer[(i + 1) % mawBuffer.Length], VoidHot, 2.5f);

		// Teeth: triangles alternating from the top and bottom lip, so the bite interlocks.
		int teeth = 9;
		for (int i = 0; i < teeth; i++)
		{
			float t = (i + 0.5f) / teeth;
			float x = Mathf.Lerp(-mouthW * 0.92f, mouthW * 0.92f, t);
			float lip = mouthH * Mathf.Sin(t * Mathf.Pi);
			float toothLen = lip * 0.55f;
			bool upper = i % 2 == 0;
			float sign = upper ? -1f : 1f;
			var a = new Vector2(x - mouthW * 0.05f, sign * lip);
			var b = new Vector2(x + mouthW * 0.05f, sign * lip);
			var tip = new Vector2(x, sign * (lip - toothLen));
			DrawColoredPolygon(new[] { a, b, tip }, VoidHot);
		}

		// Eyes above the mouth. Two is a face; one is a cyclops and three is a bug, and the
		// silhouette taxonomy wants this to read as a thing that is looking at you.
		float eyeY = -r * 0.62f;
		float eyeX = r * 0.34f;
		foreach (float sx in new[] { -eyeX, eyeX })
		{
			DrawCircle(new Vector2(sx, eyeY), r * 0.13f, VoidCore);
			DrawCircle(new Vector2(sx, eyeY), r * 0.075f, RotHot);
			DrawCircle(new Vector2(sx + r * 0.02f, eyeY), r * 0.03f, VoidCore);
		}
	}

	// --- Shadow Forest ---------------------------------------------------------------

	// The ascension promises a field of tentacles erupting across the map, so one big pool is the
	// wrong shape for it entirely: it draws a scatter of small ones instead, each with an arm or
	// two, which is what "infestation" looks like.
	private void DrawShadowForest(float r)
	{
		for (int i = 0; i < ForestPools; i++)
		{
			float a = Mathf.Tau * i / ForestPools + shapeSeed;
			// Staggered radii so they do not sit on one ring.
			// Pushed right out toward the rim and alternated, because at 0.62/0.34 of the radius
			// the six of them overlapped into one lumpy mass - which is a worse silhouette than
			// the single circle this ascension was supposed to improve on.
			float ring = r * (i % 2 == 0 ? 1.02f : 0.66f);
			var centre = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ring;
			DrawPool(r, centre, 0.34f, ArmsOnForestPool, shapeSeed + i * 2.3f);
		}

		// One at the middle, so the cast still has a centre to read from.
		DrawPool(r, Vector2.Zero, 0.38f, ArmsOnForestPool, shapeSeed + 11.1f);
	}

	private const int ArmsOnBasePool = 7;
	private const int ArmsOnMaw = 5;
	private const int ArmsOnForestPool = 2;
	private const int ForestPools = 6;

	private void Pulse()
	{
		var stale = new List<Node2D>();
		foreach (var e in enemiesInside)
		{
			if (e == null || !IsInstanceValid(e) || GlobalPosition.DistanceTo(e.GlobalPosition) > radius)
				stale.Add(e);
			else
				ApplyZoneEffects(e);
		}

		foreach (var e in stale)
			enemiesInside.Remove(e);
	}

	private void UpdateEnemiesInsideAndApplyOnEntry()
	{
		var currentInside = new HashSet<Node2D>();
		foreach (var node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy || !IsInstanceValid(enemy))
				continue;

			if (GlobalPosition.DistanceTo(enemy.GlobalPosition) > radius)
				continue;

			currentInside.Add(enemy);
			if (!enemiesInside.Contains(enemy))
				ApplyZoneEffects(enemy);
		}

		enemiesInside.RemoveWhere(enemy => enemy == null || !IsInstanceValid(enemy) || !currentInside.Contains(enemy));
		foreach (var enemy in currentInside)
			enemiesInside.Add(enemy);
	}

	private void ApplyZoneEffects(Node2D enemy)
	{
		var player = PlayerRef as Player;
		player?.DealDamageToEnemy(enemy, damage, source: SpellData);
		if (enemy.HasMethod("ApplySlow"))
			enemy.Call("ApplySlow", SlowMultiplier, scaledSlowDuration);
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 2) * DamageMultiplier));
		radius = MathF.Max(10f, BaseRadius * AreaMultiplier);
		duration = MathF.Max(0.5f, (ZoneDuration + (SpellData?.GetEffectValueAtLevel(SpellEffect.ZoneDuration, CurrentLevel) ?? 0f)) * DurationMultiplier);
		scaledTickInterval = MathF.Max(0.08f, TickInterval * MathF.Max(0.01f, CooldownMultiplier));
		scaledSlowDuration = MathF.Max(0.1f, scaledTickInterval + 0.2f + (SpellData?.GetEffectValueAtLevel(SpellEffect.RootDuration, CurrentLevel) ?? 0f));
		QueueRedraw();
	}
}
