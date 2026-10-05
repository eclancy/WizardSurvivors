using Godot;

// The arrow at the edge of the screen that says where a side event is (.ai/side-events.md).
//
// A Control on the UI layer rather than something drawn in the world, because the whole point is
// the case where the target is off screen. When the target IS on screen it shrinks to a small
// chevron hovering over it, so the player can tell the thing they are looking at is the objective.
public partial class SideEventPointer : Control
{
	/// <summary>The world position to point at, or null to draw nothing.</summary>
	public Vector2? Target { get; set; }

	private const float EdgeInset = 34f;
	private static readonly Color Fill = new Color(0.96f, 0.86f, 0.55f, 0.95f);
	private static readonly Color Outline = new Color(0f, 0f, 0f, 0.85f);
	private readonly Vector2[] triangle = new Vector2[3];
	private readonly Vector2[] outline = new Vector2[4];

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsPreset(LayoutPreset.FullRect);
	}

	public override void _Process(double delta) => QueueRedraw();

	public override void _Draw()
	{
		if (Target == null)
			return;

		Viewport viewport = GetViewport();
		Rect2 screen = viewport.GetVisibleRect();
		Vector2 onScreen = viewport.GetCanvasTransform() * Target.Value;
		Vector2 centre = screen.Size * 0.5f;

		Rect2 inner = screen.Grow(-EdgeInset);
		if (inner.HasPoint(onScreen))
		{
			// Visible: a small chevron above the target, bobbing so it reads as a marker, not decor.
			float bob = Mathf.Sin(Time.GetTicksMsec() * 0.006f) * 3f;
			DrawArrow(onScreen + new Vector2(0f, -40f + bob), Vector2.Down, 9f);
			return;
		}

		// Off screen: walk from the centre toward the target and stop at the inset edge.
		Vector2 direction = (onScreen - centre).Normalized();
		float scaleX = direction.X != 0f ? (inner.Size.X * 0.5f) / Mathf.Abs(direction.X) : float.MaxValue;
		float scaleY = direction.Y != 0f ? (inner.Size.Y * 0.5f) / Mathf.Abs(direction.Y) : float.MaxValue;
		Vector2 edge = centre + direction * Mathf.Min(scaleX, scaleY);
		DrawArrow(edge, direction, 13f);
	}

	private void DrawArrow(Vector2 tip, Vector2 direction, float size)
	{
		Vector2 side = new Vector2(-direction.Y, direction.X);
		triangle[0] = tip;
		triangle[1] = tip - direction * size * 1.6f + side * size;
		triangle[2] = tip - direction * size * 1.6f - side * size;
		DrawColoredPolygon(triangle, Fill);
		outline[0] = triangle[0];
		outline[1] = triangle[1];
		outline[2] = triangle[2];
		outline[3] = triangle[0];
		DrawPolyline(outline, Outline, 2f);
	}
}
