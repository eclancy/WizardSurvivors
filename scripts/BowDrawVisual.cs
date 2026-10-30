using Godot;
using System;
using WizardSurvivors.scripts;

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
/// PIXEL ART (2026-10-29). It was antialiased polylines on translucent outlines, a smooth polygon head and
/// two soft circles for the full-draw glow, turned with the aim onto a rotated grid; the brackets were
/// antialiased polylines too. The bow is now one quad drawn by scenes/shaders/bow_draw.gdshader, which
/// snaps every fragment to the world's 2 px cell and measures the limbs, string, arrow and glow in the
/// bow's frame from the same numbers as before - so the pose still bends continuously with the charge -
/// in opaque Earth, bone, Metal and Wind tones on an art pixel of occlusion. The brackets are whole art
/// pixels: grid-snapped rectangles, an art pixel of occlusion under one (two at full draw) of ink.
///
/// Drawn procedurally rather than from a sprite sheet: the pose has to interpolate continuously
/// with charge, which a handful of animation frames cannot do. Nothing here allocates per frame -
/// the bow's numbers are shader parameters, and the mark is one child node drawn on its own.
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

	// The brackets' inks, by the charge (bow_draw.gdshader's arrow): Metal's edge while stopping would
	// only relax the bow, Wind's mid once it would loose, Wind's hot at full draw, flashing white with
	// the strain. Occlusion, not black (.ai/art-direction.md), is the backing every one sits on.
	private static readonly Color Occlusion = new Color(0.020f, 0.027f, 0.047f);  // #05070C
	private static readonly Color MetalEdge = new Color(0.369f, 0.447f, 0.565f);  // metal edge #5E7290
	private static readonly Color WindMid = new Color(0.706f, 0.933f, 0.878f);    // wind mid #B4EEE0
	private static readonly Color WindHot = new Color(0.918f, 0.988f, 0.965f);    // wind hot #EAFCF6
	private static readonly Color Core = new Color(1f, 1f, 1f);                   // metal core #FFFFFF

	private static Shader bowShader;
	private static readonly StringName ParamBowRadius = "bow_radius";
	private static readonly StringName ParamHalfAngle = "half_angle";
	private static readonly StringName ParamCharge = "charge";
	private static readonly StringName ParamPull = "pull";
	private static readonly StringName ParamArrowLength = "arrow_length";
	private static readonly StringName ParamInk = "ink";
	private static readonly StringName ParamStrain = "strain";
	private static readonly StringName ParamFull = "full";
	private ShaderMaterial material;

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

	public override void _Ready()
	{
		// Above the player sprite but below the HUD, so the bow never disappears behind the robe.
		ZIndex = 3;
		Visible = false;

		bowShader ??= GD.Load<Shader>("res://scenes/shaders/bow_draw.gdshader");
		material = new ShaderMaterial { Shader = bowShader };
		Material = material;
		material.SetShaderParameter(ParamBowRadius, BowRadius);
		material.SetShaderParameter(ParamHalfAngle, Mathf.DegToRad(BowHalfAngleDegrees));
		material.SetShaderParameter(ParamArrowLength, ArrowLength);

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
		material.SetShaderParameter(ParamCharge, charge);
		material.SetShaderParameter(ParamPull, MaxStringPull * charge);
		material.SetShaderParameter(ParamInk, InkLevel());
		material.SetShaderParameter(ParamStrain, Strain);
		material.SetShaderParameter(ParamFull, charge >= 0.999f);

		hasTarget = Visible && target != null && IsInstanceValid(target);
		if (mark != null)
		{
			mark.Visible = hasTarget;
			if (hasTarget)
			{
				// On the art grid, so the brackets' pixels line up with the body's.
				Vector2 at = target.GlobalPosition;
				mark.GlobalPosition = new Vector2(MathF.Round(at.X / WhipLimb.Grid) * WhipLimb.Grid, MathF.Round(at.Y / WhipLimb.Grid) * WhipLimb.Grid);
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

	// The arrow's ink (bow_draw.gdshader): 0 while stopping would only relax the bow, 1 once it would
	// loose, dithering toward 2 as the draw deepens, and 2 at full draw.
	private float InkLevel()
	{
		if (charge < looseCharge)
			return 0f;
		if (charge >= 0.999f)
			return 2f;
		return 1f + 0.6f * (charge - looseCharge) / MathF.Max(0.01f, 1f - looseCharge);
	}

	// One quad over the bow at its deepest draw and the arrow on it; the shader decides every pixel.
	public override void _Draw()
	{
		float back = MaxStringPull + 10f;
		float front = MathF.Max(BowRadius, ArrowLength - 0f) + 8f;
		float half = BowRadius + 8f;
		DrawRect(new Rect2(-back, -half, back + front, half * 2f), Colors.White);
	}

	// The hunter's mark: four corner brackets round the target's body. Wide when the draw has only
	// begun, closing in as it deepens, tight and throbbing at full draw - so the mark says both which
	// enemy and how hard, and a glance at the swarm finds it without looking at the bow. Whole art
	// pixels: each arm a grid-snapped rectangle, an art pixel of occlusion under the ink.
	private void DrawMark()
	{
		if (!hasTarget || charge <= 0.01f)
			return;

		bool fullyDrawn = charge >= 0.999f;
		float strain = Strain;
		Color ink = charge < looseCharge ? MetalEdge : fullyDrawn ? (strain > 0.6f ? Core : WindHot) : WindMid;
		float g = WhipLimb.Grid;
		float gap = Mathf.Lerp(14f, 3f, charge);
		Rect2 box = targetBody.Grow(gap);
		Vector2 from = Snap(box.Position);
		Vector2 to = Snap(box.End);
		float arm = MathF.Max(g * 2f, Snap(MathF.Min(11f, MathF.Min(to.X - from.X, to.Y - from.Y) * 0.3f)));
		float width = fullyDrawn ? g * 2f : g;

		for (int pass = 0; pass < 2; pass++)
		{
			Color c = pass == 0 ? Occlusion : ink;
			float grow = pass == 0 ? g : 0f;
			Bracket(from, new Vector2(1f, 0f), new Vector2(0f, 1f), arm, width, grow, c);
			Bracket(new Vector2(to.X, from.Y), new Vector2(-1f, 0f), new Vector2(0f, 1f), arm, width, grow, c);
			Bracket(new Vector2(from.X, to.Y), new Vector2(1f, 0f), new Vector2(0f, -1f), arm, width, grow, c);
			Bracket(to, new Vector2(-1f, 0f), new Vector2(0f, -1f), arm, width, grow, c);
		}
	}

	private static float Snap(float v) => MathF.Round(v / WhipLimb.Grid) * WhipLimb.Grid;

	private static Vector2 Snap(Vector2 v) => new Vector2(Snap(v.X), Snap(v.Y));

	// One corner: an arm along each edge from the corner, `width` thick on the inside of the box.
	private void Bracket(Vector2 at, Vector2 alongX, Vector2 alongY, float arm, float width, float grow, Color color)
	{
		mark.DrawRect(new Rect2(at, Vector2.Zero).Expand(at + alongX * arm + alongY * width).Grow(grow), color);
		mark.DrawRect(new Rect2(at, Vector2.Zero).Expand(at + alongY * arm + alongX * width).Grow(grow), color);
	}
}
