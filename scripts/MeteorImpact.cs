using Godot;
using System;

namespace WizardSurvivors.scripts;

// Meteor Swarm, one meteor of a volley: it streaks in from the upper left onto a point, and on
// landing hits everything within explosionRadius, flashes the crater and throws burning rock out to
// exactly that radius.
//
// THE LOOK IS THE WEB DEMO'S (2026-10-25: "meteor swarm should look like the meteors on the web based
// demo version"). SurviveDemo.tsx draws a meteor as a tiny pixel-art head with a short stepped trail -
// four squares in the fire ramp, white-hot at the head, stepping back up a diagonal from the upper
// left - and the landing as the meteor-impact crater. That is what this draws, at the game's scale:
// the demo's numbers in demo pixels times 2.5 world px for the path and 3 for the squares (the demo
// shows its sprites at half the game's x2; the rest is so the head still reads on a phone), every
// square stamped on the 2 px art grid with no smoothing. The meteor used to be the crater sprite
// itself, tinted, rotated and slid straight down - an orange smear that read as nothing in particular.
//
// What the demo draws and this does not: its flickering red ellipse on the landing point for the
// whole wait. That is an area marker, and the player's spells carry none (.ai/art-direction.md,
// section 3; .ai/spell-roster.md, 2c). The streak is the warning; the reach is shown by the crater and
// the debris at the moment of the hit, both sized from the radius the hit tests and neither drawn past it.
//
// One node per meteor, drawn in code: a few rects in flight, one texture and the debris on landing,
// nothing allocated per frame. The volley's stagger is the delay CastAt is given - the node waits
// invisible, then falls.
public partial class MeteorImpact : Node2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float BaseExplosionRadius { get; set; } = 60f;
	// The crater flash (assets/bonelight/effects/meteor-impact.png, the demo's own art).
	[Export] public Texture2D CraterTexture { get; set; }
	// Seconds in the sky, entry to landing. The demo's 0.45.
	[Export] public float FallSeconds { get; set; } = 0.45f;
	// World px per demo px: PixelScale for the path, TrailScale for the squares. See the header. The
	// squares are the chunkier of the two so the head is still a few device pixels on a phone, where
	// the camera shows the same ground on a much smaller screen.
	[Export] public float PixelScale { get; set; } = 2.5f;
	[Export] public float TrailScale { get; set; } = 3f;

	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;

	// Where the meteor enters, from where it lands, in demo px: 60 left and 140 up (SurviveDemo.tsx).
	private static readonly Vector2 FallFrom = new Vector2(-60f, -140f);
	// The trail, head first: the demo's square sizes in demo px, and how far apart they sit up the
	// path (its step of 3 across and 7 up, which is the path's own slope).
	private static readonly float[] TrailSizes = { 4f, 4f, 3f, 3f };
	private static readonly float TrailSpacing = new Vector2(3f, 7f).Length();

	// The crater sprite: its centre in the 64 px frame, and how far its furthest ray reaches from it.
	// The flash is drawn so that reach lands on the hit radius, as the demo's 0.9 scale did on its 28.
	private static readonly Vector2 CraterCentre = new Vector2(32f, 40f);
	private const float CraterReach = 32f;
	private const float CraterSeconds = 0.35f;

	// Magma Craters (meteor_magma_crater, SpellEffect.Burn = seconds): the crater keeps burning, and
	// hurts what stands in it every tick for a share of the impact. The card said so and nothing read it.
	private const float MagmaTickSeconds = 0.5f;
	private const float MagmaTickShare = 0.25f;
	private const string FlamePath = "res://assets/bonelight/effects/spore-ember.png";
	private const int FlameCell = 16;
	private const int FlameFrames = 4;
	private static Texture2D flame;

	private static readonly ElementColors.Ramp FireRamp = ElementColors.GetRamp(Element.Fire);

	private int damage = 8;
	private float explosionRadius = 60f;
	private float meteorScale = 1f;
	private float burnSeconds;
	private int burnDamage;
	private float burnTick;
	// Counts up from -delay: below zero the meteor is still waiting its turn in the volley.
	private float age;
	private bool impacted;
	private float sinceImpact;
	private float lifeAfterImpact;
	// Magma Craters' burning ground, drawn under the actors (Ember Caps' patches sit there too): the
	// meteor, the flash and the rock are in the air over the pack, the fire is on the floor it stands on.
	private Node2D floor;

	public override void _Ready()
	{
		// Over the bodies it lands among, as the debris layer is; under the HUD.
		ZIndex = 6;
		RefreshComputedStats();
		if (burnSeconds > 0f)
		{
			floor = new Node2D { Name = "Magma", ZIndex = -1, ZAsRelative = false };
			AddChild(floor);
			floor.Draw += DrawMagma;
		}
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	/// <summary>Lands on <paramref name="position"/> FallSeconds after <paramref name="delay"/> has passed.</summary>
	public void CastAt(Vector2 position, float delay = 0f)
	{
		GlobalPosition = position;
		age = -MathF.Max(0f, delay);
		impacted = false;
		sinceImpact = 0f;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (!impacted)
		{
			age += dt;
			if (age < 0f)
				return;
			if (age >= FallSeconds)
				Impact();
			QueueRedraw();
			return;
		}

		sinceImpact += dt;
		if (burnSeconds > 0f && sinceImpact < burnSeconds)
		{
			burnTick -= dt;
			if (burnTick <= 0f)
			{
				burnTick += MagmaTickSeconds;
				HitWithin(burnDamage);
			}
		}

		if (sinceImpact >= lifeAfterImpact)
		{
			QueueFree();
			return;
		}
		QueueRedraw();
		floor?.QueueRedraw();
	}

	public override void _Draw()
	{
		if (!impacted)
		{
			if (age >= 0f)
				DrawStreak(Mathf.Clamp(age / MathF.Max(0.05f, FallSeconds), 0f, 1f));
			return;
		}

		DrawCrater();
		// Burning rock thrown out of the impact to exactly explosionRadius - the reach that was hit.
		ImpactDebris.Draw(this, Vector2.Zero, explosionRadius, sinceImpact, (uint)(GetInstanceId() % 9973),
			FireRamp, ImpactDebris.CountFor(explosionRadius), Colors.White);
	}

	// The demo's meteor: the head at k of the way down the path, three squares behind it stepping back
	// up the path, cooling down the ramp. Linear, as the demo's is - a streak does not ease in.
	private void DrawStreak(float k)
	{
		// A bigger rock, not a longer fall: the path is the same on screen whatever the blast, so
		// Extinction Event's meteor still enters in view.
		float unit = TrailScale * meteorScale;
		Vector2 head = FallFrom * PixelScale * (1f - k);
		Vector2 up = FallFrom.Normalized() * TrailSpacing * unit;
		for (int i = TrailSizes.Length - 1; i >= 0; i--)
		{
			Color c = i switch { 0 => FireRamp.Core, 1 => FireRamp.Hot, 2 => FireRamp.Mid, _ => FireRamp.Edge };
			// Whole art pixels, rounded up: the demo's 4 and 3 stay two different sizes at any scale.
			float size = MathF.Max(WhipLimb.Grid, MathF.Ceiling(TrailSizes[i] * unit / WhipLimb.Grid) * WhipLimb.Grid);
			DrawRect(WhipLimb.Cell(head + up * i, size), c);
		}
	}

	// The crater, at the size whose furthest ray lands on the hit radius; held, then gone quickly.
	private void DrawCrater()
	{
		if (CraterTexture == null || sinceImpact >= CraterSeconds)
			return;
		float scale = explosionRadius / CraterReach;
		float fade = Mathf.Clamp((CraterSeconds - sinceImpact) / (CraterSeconds * 0.4f), 0f, 1f);
		var rect = new Rect2(-CraterCentre * scale, CraterTexture.GetSize() * scale);
		DrawTextureRect(CraterTexture, rect, false, new Color(1f, 1f, 1f, fade));
	}

	// Magma Craters: the crater is the flames that FILL it (Cataclysm's and Ember Caps' burning ground) -
	// rings stepping in from the one whose outer edges sit on the radius, and one at the heart. Filled,
	// not ringed: a ring of flames round an empty middle is a rim held for as long as the zone lasts,
	// which is the area marker the player's spells do not draw (.ai/art-direction.md, section 3).
	private const float FlameRingStep = 24f;

	private void DrawMagma()
	{
		if (!impacted || sinceImpact >= burnSeconds)
			return;
		flame ??= GD.Load<Texture2D>(FlamePath);
		if (flame == null)
			return;
		float fade = Mathf.Clamp((burnSeconds - sinceImpact) / 1f, 0f, 1f);
		var tint = new Color(1f, 1f, 1f, fade);
		int seed = (int)(GetInstanceId() % 997);
		int frameClock = (int)(sinceImpact * 10f) + seed;

		int ring = 0;
		for (float r = explosionRadius - FlameCell; r > FlameCell * 0.9f; r -= FlameRingStep, ring++)
			DrawFlameRing(r, FlameCountFor(r), seed * 0.37f + ring * 0.45f, frameClock + ring, tint);
		DrawFlame(Vector2.Zero, frameClock + ring, tint);
	}

	public override void _ExitTree()
	{
		if (floor != null)
			floor.Draw -= DrawMagma;
	}

	private static int FlameCountFor(float ringRadius) => Mathf.Clamp(Mathf.RoundToInt(Mathf.Tau * ringRadius / 26f), 4, 24);

	private void DrawFlameRing(float ringRadius, int count, float turn, int frameClock, Color tint)
	{
		for (int f = 0; f < count; f++)
		{
			float a = Mathf.Tau * f / count + turn;
			DrawFlame(new Vector2(MathF.Cos(a), MathF.Sin(a)) * ringRadius, frameClock + f, tint);
		}
	}

	// One flame at x2 (32 across), centred on at, on the floor layer.
	private void DrawFlame(Vector2 at, int frameClock, Color tint)
	{
		int frame = ((frameClock % FlameFrames) + FlameFrames) % FlameFrames;
		var src = new Rect2(frame * FlameCell, 0, FlameCell, FlameCell);
		var dest = new Rect2(at.X - FlameCell, at.Y - FlameCell, FlameCell * 2f, FlameCell * 2f);
		floor.DrawTextureRectRegion(flame, dest, src, tint);
	}

	private void Impact()
	{
		if (impacted)
			return;
		impacted = true;
		sinceImpact = 0f;
		burnTick = MagmaTickSeconds;
		lifeAfterImpact = MathF.Max(MathF.Max(ImpactDebris.LifeSeconds, CraterSeconds), burnSeconds) + 0.02f;

		// The group, like every AoE (CLAUDE.md), read through EnemyRegistry - the group without the
		// native array (.ai/performance.md). Under the balance harness the scene's direct children are
		// not the enemies, which is why it once hit nothing.
		HitWithin(damage);
	}

	private void HitWithin(int amount)
	{
		if (PlayerRef is not Player player || !IsInstanceValid(player))
			return;
		Vector2 at = GlobalPosition;
		float r2 = explosionRadius * explosionRadius;
		foreach (var enemy in EnemyRegistry.Snapshot)
		{
			if (IsInstanceValid(enemy) && at.DistanceSquaredTo(enemy.GlobalPosition) <= r2)
				player.DealDamageToEnemy(enemy, amount, source: SpellData);
		}
	}

	private void RefreshComputedStats()
	{
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 8) * DamageMultiplier));
		// Level 3's "Area +0.16" is a fraction of the blast, as Glacial Spike reads it. Nothing read it here.
		float areaBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel) ?? 0f;
		explosionRadius = MathF.Max(8f, BaseExplosionRadius * (1f + areaBonus) * AreaMultiplier);
		// A wider blast is a bigger rock - Extinction Event's x3 is a visibly heavier meteor - but by the
		// square root, so the streak stays a streak.
		meteorScale = Mathf.Clamp(MathF.Sqrt(explosionRadius / MathF.Max(1f, BaseExplosionRadius)), 1f, 2.2f);
		burnSeconds = SpellData?.GetEffectValueAtLevel(SpellEffect.Burn, CurrentLevel) ?? 0f;
		burnDamage = Math.Max(1, Mathf.RoundToInt(damage * MagmaTickShare));
	}
}
