using Godot;
using System;

/// <summary>
/// The drawn bow that hovers at the player's shoulder while Hunter's Draw is charging.
///
/// This is the whole readability story for the spell. Hunter's Draw fires on a player action -
/// planting your feet - rather than on a timer, so without a visible bow there is no way to tell
/// how much charge you have banked or which way the arrow will go. The limbs bend, the string
/// pulls back and the nock slides with the charge, the whole thing points at the enemy that will
/// be hit, and it flashes once it is fully drawn and starting to tire.
///
/// Drawn procedurally rather than from a sprite sheet: the pose has to interpolate continuously
/// with charge, which a handful of animation frames cannot do.
/// </summary>
public partial class BowDrawVisual : Node2D
{
	// Held out in front of the player along the aim line rather than centred on them. Drawn over
	// the sprite it just reads as clutter on top of a 30px wizard; pushed out here it reads as an
	// object being held, and leaves the player's own silhouette intact.
	[Export] public float ForwardOffset { get; set; } = 15f;
	[Export] public float ShoulderOffset { get; set; } = -5f;
	[Export] public float BowRadius { get; set; } = 13f;
	// Half-angle of the bow's arc. Around 62 degrees keeps the tips clearly apart so the shape
	// reads as a C; much wider and the limbs close up into a blob at this size.
	[Export] public float BowHalfAngleDegrees { get; set; } = 62f;
	[Export] public float MaxStringPull { get; set; } = 10f;
	[Export] public float ArrowLength { get; set; } = 24f;

	[Export] public Color LimbColor { get; set; } = new Color(0.68f, 0.47f, 0.24f, 1f);
	// Deliberately faint. The string is the least important line in the drawing and at this size
	// a bright one competes with the arrow and turns the whole thing into a triangle.
	[Export] public Color StringColor { get; set; } = new Color(0.85f, 0.88f, 0.94f, 0.45f);
	[Export] public Color ArrowColor { get; set; } = new Color(0.66f, 0.93f, 1.0f, 1f);
	[Export] public Color FullDrawColor { get; set; } = new Color(0.60f, 1.0f, 0.85f, 1f);

	// 0 when relaxed, 1 at full draw.
	private float charge;
	private float aimAngle;
	// Seconds spent at full draw, used only to drive the strain flash.
	private float holdSeconds;

	public override void _Ready()
	{
		// Above the player sprite but below the HUD, so the bow never disappears behind the robe.
		ZIndex = 3;
		Visible = false;
	}

	/// <summary>Updates the pose. Call every frame the bow is nocked.</summary>
	/// <param name="drawCharge">0-1 draw progress.</param>
	/// <param name="aimDirection">Where the arrow currently points; zero keeps the last aim.</param>
	/// <param name="fullDrawHoldSeconds">Seconds held at full draw, for the strain flash.</param>
	public void UpdateDraw(float drawCharge, Vector2 aimDirection, float fullDrawHoldSeconds)
	{
		charge = Mathf.Clamp(drawCharge, 0f, 1f);
		holdSeconds = MathF.Max(0f, fullDrawHoldSeconds);
		if (aimDirection != Vector2.Zero)
			aimAngle = aimDirection.Angle();

		Visible = charge > 0.01f;
		Position = Vector2.Right.Rotated(aimAngle) * ForwardOffset + new Vector2(0f, ShoulderOffset);
		Rotation = aimAngle;
		QueueRedraw();
	}

	/// <summary>Hides the bow. Called the moment the volley is loosed or the draw relaxes away.</summary>
	public void Clear()
	{
		charge = 0f;
		holdSeconds = 0f;
		Visible = false;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (charge <= 0.01f)
			return;

		bool fullyDrawn = charge >= 0.999f;
		// A 6Hz throb once the shot is fully drawn: the arms are tiring and the bow is about to
		// loose on its own. Nothing else on screen would tell the player that is coming.
		float strain = fullyDrawn ? 0.5f + 0.5f * MathF.Sin(holdSeconds * 12f) : 0f;
		Color limb = LimbColor.Lerp(FullDrawColor, strain * 0.55f);
		Color arrow = ArrowColor.Lerp(FullDrawColor, MathF.Max(charge * 0.4f, strain));

		// The bow is drawn in local space with +X as the aim direction, so the riser sits behind
		// the origin and the arrow flies out along +X. The node's Rotation does the aiming.
		float halfAngle = Mathf.DegToRad(BowHalfAngleDegrees);
		var limbPoints = new Vector2[13];
		for (int i = 0; i < limbPoints.Length; i++)
		{
			float t = i / (float)(limbPoints.Length - 1);
			float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
			// Deeper draw bends the limbs back toward the archer, so the bow visibly loads up.
			float radius = BowRadius * (1f - 0.16f * charge * MathF.Cos(angle));
			limbPoints[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
		}
		// Dark backing stroke first: against a bright grass tile a bare wooden arc disappears.
		DrawPolyline(limbPoints, new Color(0.14f, 0.09f, 0.05f, 0.85f), 5f, true);
		DrawPolyline(limbPoints, limb, 2.6f, true);

		Vector2 topTip = limbPoints[0];
		Vector2 bottomTip = limbPoints[^1];
		// The nock is pulled back along -X, away from the aim direction.
		Vector2 nock = new Vector2(-MaxStringPull * charge, 0f);

		DrawLine(topTip, nock, StringColor, 1.2f, true);
		DrawLine(nock, bottomTip, StringColor, 1.2f, true);

		// The arrow is the one line that has to be unmistakable - it is the aim indicator - so it
		// gets its own dark outline and the brightest colour in the drawing.
		Vector2 tip = nock + new Vector2(ArrowLength, 0f);
		DrawLine(nock, tip, new Color(0.06f, 0.10f, 0.14f, 0.9f), 4.2f, true);
		DrawLine(nock, tip, arrow, 2.2f, true);
		DrawLine(tip, tip + new Vector2(-6f, -4f), arrow, 2.2f, true);
		DrawLine(tip, tip + new Vector2(-6f, 4f), arrow, 2.2f, true);

		if (fullyDrawn)
			DrawArc(Vector2.Zero, BowRadius + 4f, 0f, Mathf.Tau, 28,
				new Color(FullDrawColor.R, FullDrawColor.G, FullDrawColor.B, 0.20f + 0.25f * strain), 1.5f, true);
	}
}
