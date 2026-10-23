using System.Collections.Generic;
using Godot;
using WizardSurvivors.scripts;

// An arrow at the edge of the screen for every point of interest that is marked and off screen
// (.ai/side-events.md, "Finding the event").
//
// Marked means seen once, or revealed by the chapter's wayfinder (PointOfInterest.Marked). The
// arrow is a DIAMOND badge with the place's pictogram in it and a short tail pointing outward - a
// third shape beside the chest's round bubble (ChestPointer, a reward) and the objective's gold
// triangle (SideEventPointer, what to do now), because a place, a reward and an objective are three
// different promises. His places wear an arcane-cyan rim; a wayfinder seen and not yet taken wears
// gold, the colour of things he missed. The badge and pictograms are generated
// (tools/art/explore_props.py) and drawn at x2 on whole pixels; only the tail, which turns, is drawn
// here.
//
// Nothing is drawn for a point on screen (it is its own marker: a lantern's light, a cell), for a
// point the objective arrow is already on (PointsOfInterest.ObjectiveTarget), or during the boss.
// Reads the static list rather than a group: it runs every frame of a 140-enemy swarm.
public partial class PoiPointer : Control
{
	// Clear of the HUD band across the top (health, timer, the objective line) and of the chest
	// bubbles, which sit 52 in. Safe-area insets are added on a phone.
	private const float InsetSide = 66f;
	private const float InsetTop = 132f;
	private const float InsetBottom = 76f;
	private const float TailLength = 11f;
	// Half the badge's drawn size (24 x 24 art at x2).
	private const float BadgeHalf = 24f;

	private static readonly Color HisRim = new Color(0.416f, 0.722f, 0.910f);   // arcane cyan lit #6AB8E8
	private static readonly Color FoundRim = new Color(0.847f, 0.690f, 0.290f); // gold lit #D8B04A
	private static readonly Color Shadow = new Color(0f, 0f, 0f, 0.5f);

	private readonly Vector2[] tail = new Vector2[3];
	private readonly Dictionary<string, Texture2D> glyphs = new();
	private Texture2D badgeHis;
	private Texture2D badgeFound;
	private bool wasDrawing;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsPreset(LayoutPreset.FullRect);
		TextureFilter = TextureFilterEnum.Nearest;
		badgeHis = Load(ExploreArt.BadgePath(false));
		badgeFound = Load(ExploreArt.BadgePath(true));
		foreach (string glyph in ExploreArt.Glyphs)
			glyphs[glyph] = Load(ExploreArt.GlyphPath(glyph));
	}

	private static Texture2D Load(string path) => ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;

	public override void _Process(double delta)
	{
		if (wasDrawing || (!PointsOfInterest.Suspended && PointsOfInterest.AnyMarked))
			QueueRedraw();
	}

	public override void _Draw()
	{
		wasDrawing = false;
		if (PointsOfInterest.Suspended || PointsOfInterest.Live.Count == 0)
			return;

		Viewport viewport = GetViewport();
		Rect2 screen = viewport.GetVisibleRect();
		Transform2D canvas = viewport.GetCanvasTransform();
		var (left, top, right, bottom) = SafeArea.Insets(viewport);
		var inner = new Rect2(
			screen.Position + new Vector2(InsetSide + left, InsetTop + top),
			screen.Size - new Vector2(2f * InsetSide + left + right, InsetTop + InsetBottom + top + bottom));
		Vector2 centre = inner.GetCenter();
		Vector2? objective = PointsOfInterest.ObjectiveTarget;
		// The same slow breath as the chest bubble: noticeable at the edge of vision, never alarming.
		float pulse = 0.85f + 0.15f * Mathf.Sin(Time.GetTicksMsec() * 0.004f);

		foreach (PointOfInterest point in PointsOfInterest.Live)
		{
			if (!point.Marked)
				continue;
			if (objective is Vector2 o && o.DistanceSquaredTo(point.Position) < 4f)
				continue;
			Vector2 onScreen = canvas * point.Position;
			if (screen.Grow(-20f).HasPoint(onScreen))
				continue;

			Vector2 direction = (onScreen - centre).Normalized();
			float scaleX = direction.X != 0f ? (inner.Size.X * 0.5f) / Mathf.Abs(direction.X) : float.MaxValue;
			float scaleY = direction.Y != 0f ? (inner.Size.Y * 0.5f) / Mathf.Abs(direction.Y) : float.MaxValue;
			Vector2 at = (centre + direction * Mathf.Min(scaleX, scaleY)).Round();
			DrawBadge(at, direction, point, pulse);
			wasDrawing = true;
		}
	}

	private void DrawBadge(Vector2 at, Vector2 direction, PointOfInterest point, float pulse)
	{
		Color rim = point.IsWayfinder ? FoundRim : HisRim;
		Vector2 side = new Vector2(-direction.Y, direction.X);
		// The tail first, from under the badge's rim out toward the place. A diamond's edge is
		// nearest along the diagonals, so the root sits a little inside the half-width.
		tail[0] = at + direction * (BadgeHalf + TailLength);
		tail[1] = at + direction * (BadgeHalf - 8f) + side * 7f;
		tail[2] = at + direction * (BadgeHalf - 8f) - side * 7f;
		DrawColoredPolygon(tail, new Color(rim, 0.6f + 0.4f * pulse));

		Texture2D badge = point.IsWayfinder ? badgeFound : badgeHis;
		if (badge != null)
		{
			DrawTexture(badge, at + new Vector2(-BadgeHalf, -BadgeHalf + 2f), new Color(Shadow, Shadow.A));
			DrawTexture(badge, at - new Vector2(BadgeHalf, BadgeHalf));
		}
		else
		{
			DrawCircle(at, BadgeHalf * 0.75f, new Color(0.04f, 0.06f, 0.09f, 0.92f));
			DrawArc(at, BadgeHalf * 0.75f, 0f, Mathf.Tau, 24, rim, 2f);
		}

		if (glyphs.TryGetValue(point.Glyph, out Texture2D glyph) && glyph != null)
		{
			Vector2 size = glyph.GetSize();
			DrawTexture(glyph, at - (size * 0.5f).Round(), new Color(1f, 1f, 1f, 0.75f + 0.25f * pulse));
		}
	}
}
