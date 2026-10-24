using Godot;
using System;

namespace WizardSurvivors.scripts;

// Solar Flare's bespoke visual, replacing the generic PlaceholderShape it used to share with
// Toxic Spore Burst. PlaceholderShape._Draw() lets Sunburst win over Ring, and SolarFlare.tscn set
// Sunburst while ElementalPulse set Ring at runtime, so the "expanding ring" animation was actually
// rendered as a growing *filled* disc - an opaque blob that swallowed the wizard and the swarm.
//
// Instead this draws the flare as light erupting from the player and washing outward, and nothing
// wide is opaque: the bright elements are thin arcs, tapered spokes and a small core, so enemies stay
// readable straight through the blast.
//
// THE DRAWING IS THE HIT (2026-10-23, "their animations should make it clear what the hitbox is").
// It used to be emitted inside a transform squashed to 0.42 high and dropped to the feet, to read as
// a ground effect - but ElementalPulse hits a CIRCLE round the player's origin, so an enemy straight
// above or below him at the full radius was hit with nothing drawn within half that distance of it.
// It is a circle on the player's origin now, the same one the pulse tests. The faint scorch disc
// under it went with the other flat discs, and the burst's own front - the leading shockwave, which
// travels out once at the moment of the hit and stops on the radius - is what shows the reach.
public partial class SolarFlareVisual : Node2D, IPulseVisual
{
	// Both stay at zero offset and no squash: the hit is a circle on the player's origin.
	[Export] public float GroundOffset { get; set; } = 0f;
	[Export] public float GroundSquash { get; set; } = 1f;
	[Export] public int RayCount { get; set; } = 14;
	[Export] public int EmberCount { get; set; } = 12;
	[Export] public float BurstDuration { get; set; } = 0.55f;
	[Export] public Color CoreColor { get; set; } = new Color(1.0f, 0.96f, 0.76f, 1.0f);
	[Export] public Color FlareColor { get; set; } = new Color(1.0f, 0.60f, 0.13f, 1.0f);
	[Export] public Color ScorchColor { get; set; } = new Color(0.85f, 0.20f, 0.04f, 1.0f);

	private float elapsed = 0f;
	private float radius = 90f;
	private float spin = 0f;
	private bool active = false;

	public override void _Ready()
	{
		Visible = false;
	}

	public void Burst(float burstRadius)
	{
		radius = MathF.Max(8f, burstRadius);
		elapsed = 0f;
		active = true;
		// Rotate the spoke pattern a little every cast so repeat pulses never stamp the same
		// starburst on the floor twice in a row.
		spin += 0.37f;
		Visible = true;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (!active)
			return;

		elapsed += (float)delta;
		if (elapsed >= BurstDuration)
		{
			active = false;
			Visible = false;
			return;
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (!active)
			return;

		float progress = Mathf.Clamp(elapsed / MathF.Max(0.01f, BurstDuration), 0f, 1f);
		// Ease-out: the shockwave leaves the feet fast and coasts as it reaches its edge.
		float wave = 1f - (1f - progress) * (1f - progress);
		float fade = 1f - progress;

		DrawSetTransform(new Vector2(0f, GroundOffset), 0f, new Vector2(1f, GroundSquash));

		DrawSpokes(wave, fade);
		DrawShockRings(wave, fade);
		DrawEmbers(wave, fade);
		DrawCore(progress);
	}

	// Tapered corona spokes reaching out from the feet, alternating long and short so the flare has
	// some internal structure instead of reading as an even wheel.
	private void DrawSpokes(float wave, float fade)
	{
		int rays = Math.Max(4, RayCount);
		float inner = radius * 0.10f;
		var points = new Vector2[3];
		for (int i = 0; i < rays; i++)
		{
			float lengthScale = (i % 2 == 0) ? 1.0f : 0.62f;
			float angle = spin + (Mathf.Tau * i / rays);
			var direction = Vector2.FromAngle(angle);
			var side = new Vector2(-direction.Y, direction.X);
			float outer = inner + ((radius * 0.95f) - inner) * wave * lengthScale;
			float halfWidth = radius * 0.055f * lengthScale * fade;

			points[0] = direction * outer;
			points[1] = (direction * inner) + (side * halfWidth);
			points[2] = (direction * inner) - (side * halfWidth);
			DrawColoredPolygon(points, new Color(FlareColor.R, FlareColor.G, FlareColor.B, 0.65f * fade * lengthScale));
		}
	}

	// The leading shockwave plus a dimmer trailing wave, both thin rings that thin out further as
	// the pulse dissipates. Both cool as they travel - holding the leading edge at CoreColor the whole
	// way out desaturated it into a flat grey hoop once it got large.
	private void DrawShockRings(float wave, float fade)
	{
		// Its outer edge on the radius at the end of the burst, never past it.
		float lead = MathF.Max(1f, radius * wave - Mathf.Lerp(1f, 4.5f, fade));
		var leadColor = CoreColor.Lerp(FlareColor, wave);
		DrawArc(Vector2.Zero, lead, 0f, Mathf.Tau, 64,
			new Color(leadColor.R, leadColor.G, leadColor.B, 0.95f * fade), Mathf.Lerp(2f, 9f, fade), true);

		float trail = MathF.Max(1f, lead * 0.68f);
		var trailColor = FlareColor.Lerp(ScorchColor, wave);
		DrawArc(Vector2.Zero, trail, 0f, Mathf.Tau, 48,
			new Color(trailColor.R, trailColor.G, trailColor.B, 0.6f * fade), Mathf.Lerp(1.5f, 6f, fade), true);
	}

	// Embers riding the wave outward. The scatter is derived from the loop index rather than an RNG
	// so it costs no allocation and no stored state per pulse; `spin` keeps it from repeating.
	private void DrawEmbers(float wave, float fade)
	{
		int count = Math.Max(0, EmberCount);
		for (int i = 0; i < count; i++)
		{
			float angle = (spin * 0.6f) + (Mathf.Tau * ((i * 0.6180339f) % 1f));
			float reach = radius * (0.55f + (0.5f * (((i * 7) % 5) / 5f)));
			float size = Mathf.Lerp(1.5f, 4.0f, (i % 3) / 2f) * fade;
			DrawCircle(Vector2.FromAngle(angle) * reach * wave, MathF.Max(0.5f, size),
				new Color(FlareColor.R, FlareColor.G, FlareColor.B, 0.8f * fade));
		}
	}

	// A brief white-hot kernel right at the feet. Kept small and short-lived on purpose: this is the
	// only near-white element, and at full radius it would be the old blob all over again.
	private void DrawCore(float progress)
	{
		float coreFade = Mathf.Clamp(1f - (progress * 2.6f), 0f, 1f);
		if (coreFade <= 0.01f)
			return;

		float coreRadius = radius * 0.20f * coreFade;
		DrawCircle(Vector2.Zero, coreRadius * 1.9f, new Color(FlareColor.R, FlareColor.G, FlareColor.B, 0.28f * coreFade));
		DrawCircle(Vector2.Zero, coreRadius, new Color(CoreColor.R, CoreColor.G, CoreColor.B, 0.85f * coreFade));
	}
}
