using Godot;
using System;

/// <summary>
/// The drawn bow that hovers at the player's shoulder while Hunter's Draw is charging, and the mark
/// on the enemy the arrow will hit.
///
/// This is the whole readability story for the spell. Hunter's Draw fires on a player action -
/// planting your feet - rather than on a timer, so without a visible bow there is no way to tell
/// how much charge you have banked or which way the arrow will go. The limbs bend, the string
/// pulls back and the nock slides with the charge, the whole thing points at the enemy that will
/// be hit, and it flashes once it is fully drawn and starting to tire.
///
/// BIGGER, AND IT SAYS WHAT IT IS AIMING AT (2026-10-25). Reported: "hunters draw should be bigger so
/// the player can see what they're aiming." The bow was a 13 px arc with a 24 px arrow beside a
/// wizard twice its height, which at the camera's 0.85 is a smudge on a phone. Everything here is now
/// about double, outlined in occlusion so it holds on the pale floors (sand, ice) as well as the dark
/// ones, and the arrow carries the charge state in its colour: dim while stopping would only relax the
/// bow (under Player.HuntersDrawMinLooseCharge), bright once stopping would loose it, and glowing at
/// full draw. The enemy the arrow is pointing at wears a hunter's mark - four corner brackets round its
/// body that close in as the draw deepens and lock tight at full draw. That is a mark on ONE enemy,
/// not an area marker: the spell has no area, and nothing is drawn on the ground.
///
/// Drawn procedurally rather than from a sprite sheet: the pose has to interpolate continuously
/// with charge, which a handful of animation frames cannot do. Nothing here allocates per frame -
/// the point arrays are fields, and the mark is one child node drawn on its own.
/// </summary>
public partial class BowDrawVisual : Node2D
{
	// Held out in front of the player along the aim line rather than centred on them. Drawn over
	// the sprite it just reads as clutter on top of the wizard; pushed out here it reads as an
	// object being held, and leaves the player's own silhouette intact. World units: the camera's
	// 0.85 shows all of these at 85%.
	[Export] public float ForwardOffset { get; set; } = 30f;
	// The figure is taller than it is wide, so the bow is held further out when the aim is up or
	// down: at 30 it sat on the hat when aiming north and over the boots when aiming south. The
	// offset follows an ellipse, ForwardOffset across and this up and down.
	[Export] public float ForwardOffsetVertical { get; set; } = 42f;
	[Export] public float ShoulderOffset { get; set; } = -6f;
	[Export] public float BowRadius { get; set; } = 26f;
	// Half-angle of the bow's arc. Around 62 degrees keeps the tips clearly apart so the shape
	// reads as a C; much wider and the limbs close up into a blob.
	[Export] public float BowHalfAngleDegrees { get; set; } = 62f;
	[Export] public float MaxStringPull { get; set; } = 22f;
	// Long enough that at full draw the head sits just past the riser, as a real drawn arrow does.
	[Export] public float ArrowLength { get; set; } = 52f;

	[Export] public Color LimbColor { get; set; } = new Color(0.74f, 0.52f, 0.27f, 1f);
	// Fainter than the arrow. The string is the least important line in the drawing and a bright one
	// competes with the arrow and turns the whole thing into a triangle.
	[Export] public Color StringColor { get; set; } = new Color(0.88f, 0.90f, 0.95f, 0.75f);
	[Export] public Color ArrowColor { get; set; } = new Color(0.66f, 0.93f, 1.0f, 1f);
	[Export] public Color FullDrawColor { get; set; } = new Color(0.60f, 1.0f, 0.85f, 1f);
	// The arrow while stopping would only relax the bow: there, but not yet a threat.
	[Export] public Color ArrowIdleColor { get; set; } = new Color(0.62f, 0.70f, 0.76f, 0.85f);

	// Occlusion, not black (.ai/art-direction.md): the backing every line sits on, so the bow holds
	// on sand and snow as well as on dungeon stone.
	private static readonly Color Outline = new Color(0.02f, 0.03f, 0.05f, 0.9f);

	// 0 when relaxed, 1 at full draw.
	private float charge;
	private float aimAngle;
	// Seconds spent at full draw, used only to drive the strain flash.
	private float holdSeconds;
	// The charge at which stopping looses the arrow rather than relaxing the bow.
	private float looseCharge = 0.25f;

	// The hunter's mark, on the enemy the arrow will hit. Its own node, out of the bow's transform
	// (TopLevel), so it sits on the enemy in world space, upright, and above the swarm.
	private Node2D mark;
	private bool hasTarget;
	private Rect2 targetBody;

	// Reused every frame. 17 points is a smooth arc at this size; 13 showed its corners once doubled.
	private readonly Vector2[] limbPoints = new Vector2[17];
	private readonly Vector2[] head = new Vector2[3];
	private readonly Vector2[] headBacking = new Vector2[3];
	private readonly Vector2[] corner = new Vector2[3];

	public override void _Ready()
	{
		// Above the player sprite but below the HUD, so the bow never disappears behind the robe.
		ZIndex = 3;
		Visible = false;

		mark = new Node2D
		{
			Name = "HuntersMark",
			TopLevel = true,
			// Over the bodies (0) and their status marks (3), under the player's own effects (5+).
			ZIndex = 4,
			ZAsRelative = false,
			Visible = false,
		};
		mark.Draw += DrawMark;
		AddChild(mark);
	}

	public override void _ExitTree()
	{
		if (mark != null && IsInstanceValid(mark))
			mark.Draw -= DrawMark;
	}

	/// <summary>Updates the pose. Call every frame the bow is nocked.</summary>
	/// <param name="drawCharge">0-1 draw progress.</param>
	/// <param name="aimDirection">Where the arrow currently points; zero keeps the last aim.</param>
	/// <param name="fullDrawHoldSeconds">Seconds held at full draw, for the strain flash.</param>
	/// <param name="target">The enemy the arrow will hit if loosed now, or null for none in range.</param>
	/// <param name="looseThreshold">The charge at which stopping looses rather than relaxes.</param>
	public void UpdateDraw(float drawCharge, Vector2 aimDirection, float fullDrawHoldSeconds,
		Node2D target, float looseThreshold)
	{
		charge = Mathf.Clamp(drawCharge, 0f, 1f);
		holdSeconds = MathF.Max(0f, fullDrawHoldSeconds);
		looseCharge = Mathf.Clamp(looseThreshold, 0f, 1f);
		if (aimDirection != Vector2.Zero)
			aimAngle = aimDirection.Angle();

		Visible = charge > 0.01f;
		Position = new Vector2(MathF.Cos(aimAngle) * ForwardOffset, MathF.Sin(aimAngle) * ForwardOffsetVertical + ShoulderOffset);
		Rotation = aimAngle;
		QueueRedraw();

		hasTarget = Visible && target != null && IsInstanceValid(target);
		if (mark != null)
		{
			mark.Visible = hasTarget;
			if (hasTarget)
			{
				mark.GlobalPosition = target.GlobalPosition;
				targetBody = BodyOf(target);
				mark.QueueRedraw();
			}
		}
	}

	/// <summary>Hides the bow. Called the moment the volley is loosed or the draw relaxes away.</summary>
	public void Clear()
	{
		charge = 0f;
		holdSeconds = 0f;
		hasTarget = false;
		Visible = false;
		if (mark != null)
			mark.Visible = false;
		QueueRedraw();
	}

	// The enemy's body, relative to its origin and at its current scale, from the same measurement
	// the status marks are placed by (Enemy.StatusFxGeometry, read once per enemy off its sprite).
	// That measures the CELL, which a figure does not fill: boxed at full size the brackets took in
	// the enemy beside it too and the mark stopped saying which one. These fractions fit it to the
	// figure, feet on the ground line.
	private const float FigureHeightShare = 0.8f;
	private const float FigureWidthShare = 0.62f;

	private static Rect2 BodyOf(Node2D target)
	{
		float scale = target.GlobalTransform.X.Length();
		float feet = 6f, height = 40f, width = 30f;
		if (target is Enemy enemy)
		{
			StatusFxGeometry geo = enemy.StatusFxGeometry;
			feet = geo.FeetY;
			height = (geo.FeetY - geo.HeadY) * FigureHeightShare;
			width = 2f * geo.HalfWidth * FigureWidthShare;
		}
		return new Rect2(-width * 0.5f * scale, (feet - height) * scale, width * scale, height * scale);
	}

	// A 6 Hz throb once the shot is fully drawn: the arms are tiring and the bow is about to loose on
	// its own. Nothing else on screen would tell the player that is coming.
	private float Strain => charge >= 0.999f ? 0.5f + 0.5f * MathF.Sin(holdSeconds * 12f) : 0f;

	private Color ArrowInk()
	{
		if (charge < looseCharge)
			return ArrowIdleColor;
		return ArrowColor.Lerp(FullDrawColor, MathF.Max((charge - looseCharge) / MathF.Max(0.01f, 1f - looseCharge) * 0.4f, Strain));
	}

	public override void _Draw()
	{
		if (charge <= 0.01f)
			return;

		bool fullyDrawn = charge >= 0.999f;
		float strain = Strain;
		Color limb = LimbColor.Lerp(FullDrawColor, strain * 0.5f);
		Color arrow = ArrowInk();

		// The bow is drawn in local space with +X as the aim direction, so the riser sits ahead of
		// the origin and the arrow flies out along +X. The node's Rotation does the aiming.
		float halfAngle = Mathf.DegToRad(BowHalfAngleDegrees);
		for (int i = 0; i < limbPoints.Length; i++)
		{
			float t = i / (float)(limbPoints.Length - 1);
			float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
			// Deeper draw bends the limbs back toward the archer, so the bow visibly loads up.
			float radius = BowRadius * (1f - 0.18f * charge * MathF.Cos(angle));
			limbPoints[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
		}

		Vector2 topTip = limbPoints[0];
		Vector2 bottomTip = limbPoints[^1];
		// The nock is pulled back along -X, away from the aim direction.
		Vector2 nock = new Vector2(-MaxStringPull * charge, 0f);
		Vector2 tip = nock + new Vector2(ArrowLength, 0f);
		const float headLength = 12f;
		const float headHalfWidth = 6.5f;
		Vector2 headBase = tip - new Vector2(headLength, 0f);

		// Every backing first, then every ink over it, so no outline cuts across a line drawn before
		// it - the limbs, string and arrow read as one object rather than three stacked stickers.
		DrawPolyline(limbPoints, Outline, 9f, true);
		DrawLine(topTip, nock, new Color(Outline, 0.6f), 3.6f, true);
		DrawLine(nock, bottomTip, new Color(Outline, 0.6f), 3.6f, true);
		DrawLine(nock + new Vector2(-2f, 0f), headBase, Outline, 7.5f, true);
		headBacking[0] = tip + new Vector2(3.5f, 0f);
		headBacking[1] = headBase + new Vector2(-2f, -headHalfWidth - 2.5f);
		headBacking[2] = headBase + new Vector2(-2f, headHalfWidth + 2.5f);
		DrawColoredPolygon(headBacking, Outline);
		// The fletching's backing, two feathers splayed back from the nock.
		DrawLine(nock + new Vector2(9f, 0f), nock + new Vector2(1f, -6f), Outline, 5.5f, true);
		DrawLine(nock + new Vector2(9f, 0f), nock + new Vector2(1f, 6f), Outline, 5.5f, true);

		// The limbs: wood, with a lit edge on the side away from the archer.
		DrawPolyline(limbPoints, limb, 5f, true);
		DrawPolyline(limbPoints, limb.Lightened(0.35f), 1.5f, true);

		DrawLine(topTip, nock, StringColor, 1.7f, true);
		DrawLine(nock, bottomTip, StringColor, 1.7f, true);

		// The arrow is the one line that has to be unmistakable - it is the aim indicator - so it is
		// the brightest thing in the drawing and the only one with a filled head.
		DrawLine(nock, headBase + new Vector2(1f, 0f), arrow, 3.6f, true);
		head[0] = tip;
		head[1] = headBase + new Vector2(0f, -headHalfWidth);
		head[2] = headBase + new Vector2(0f, headHalfWidth);
		DrawColoredPolygon(head, arrow);
		DrawLine(nock + new Vector2(9f, 0f), nock + new Vector2(1f, -6f), arrow, 2.6f, true);
		DrawLine(nock + new Vector2(9f, 0f), nock + new Vector2(1f, 6f), arrow, 2.6f, true);

		// Fully drawn: the head burns. A small halo on the point of the arrow, throbbing with the
		// strain, rather than a ring round the bow - a ring this size would read as an area.
		if (fullyDrawn)
		{
			float glow = 0.35f + 0.45f * strain;
			DrawCircle(tip - new Vector2(headLength * 0.4f, 0f), 9f + 3f * strain, new Color(FullDrawColor, glow * 0.35f));
			DrawCircle(tip - new Vector2(headLength * 0.4f, 0f), 4.5f, new Color(1f, 1f, 1f, glow));
		}
	}

	// The hunter's mark: four corner brackets round the target's body. Wide and faint when the draw
	// has only begun, closing in as it deepens, tight and throbbing at full draw - so the mark says
	// both which enemy and how hard, and a glance at the swarm finds it without looking at the bow.
	private void DrawMark()
	{
		if (!hasTarget || charge <= 0.01f)
			return;

		float strain = Strain;
		Color ink = ArrowInk();
		float gap = Mathf.Lerp(14f, 3f, charge);
		Rect2 box = targetBody.Grow(gap);
		float arm = MathF.Min(11f, MathF.Min(box.Size.X, box.Size.Y) * 0.3f);
		float width = charge >= 0.999f ? 3.4f + 1.2f * strain : 3f;

		for (int pass = 0; pass < 2; pass++)
		{
			Color c = pass == 0 ? Outline : ink;
			float w = pass == 0 ? width + 3f : width;
			Bracket(box.Position, new Vector2(1f, 0f), new Vector2(0f, 1f), arm, c, w);
			Bracket(new Vector2(box.End.X, box.Position.Y), new Vector2(-1f, 0f), new Vector2(0f, 1f), arm, c, w);
			Bracket(new Vector2(box.Position.X, box.End.Y), new Vector2(1f, 0f), new Vector2(0f, -1f), arm, c, w);
			Bracket(box.End, new Vector2(-1f, 0f), new Vector2(0f, -1f), arm, c, w);
		}
	}

	private void Bracket(Vector2 at, Vector2 alongX, Vector2 alongY, float arm, Color color, float width)
	{
		corner[0] = at + alongX * arm;
		corner[1] = at;
		corner[2] = at + alongY * arm;
		mark.DrawPolyline(corner, color, width, true);
	}
}
