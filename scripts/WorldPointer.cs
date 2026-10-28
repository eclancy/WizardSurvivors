using Godot;

// A bubble at the edge of the screen for one thing the player should be able to find: the boss while
// it is off screen, and the arcane portal that opens when it falls (2026-10-28: "bosses should have an
// arrow to point where they are when they're off screen" and "add an arrow pointing to it if it's off
// screen"). The same badge-and-tail as ChestPointer, so every off-screen marker in the game reads as
// one family, but each in its own rim colour - the boss in blood red, the way home in arcane cyan -
// and drawing whatever picture it is given: the boss's own current frame, the portal's drawn ring.
//
// Node2DGame sets Target every frame (null to draw nothing). It draws nothing for a target already on
// screen, which is its own marker.
public partial class WorldPointer : Control
{
	/// <summary>The world position to point at, or null to draw nothing.</summary>
	public Vector2? Target { get; set; }

	/// <summary>The picture in the badge; when null, DrawGlyph draws one.</summary>
	public Texture2D? Icon { get; set; }

	/// <summary>Draws the picture when there is no Icon: called with the badge's centre and radius.</summary>
	public System.Action<WorldPointer, Vector2, float>? DrawGlyph { get; set; }

	public Color Rim { get; set; } = new(0.96f, 0.80f, 0.42f, 1f);

	/// <summary>A little bigger than a chest's bubble: a boss or the way out matters more.</summary>
	public float Radius { get; set; } = 25f;

	/// <summary>
	/// How far outside the screen the target's point may be and still count as seen: a boss is drawn
	/// far bigger than the point it stands on, so its body is on screen well before its feet are.
	/// </summary>
	public float VisibleMargin { get; set; } = 0f;

	/// <summary>
	/// How far down from the top edge the badge keeps: under the boss's bar (44-78) and the line of
	/// banners and tips below it, which a badge at the chest's inset sat behind.
	/// </summary>
	public float TopInset { get; set; } = 140f;

	// ChestPointer's inset, so a boss and a chest in the same direction sit on the same ring.
	private const float EdgeInset = 52f;
	private const float TailLength = 13f;

	private static readonly Color Fill = new(0.09f, 0.08f, 0.12f, 0.92f);
	private static readonly Color Shadow = new(0f, 0f, 0f, 0.55f);

	private readonly Vector2[] tail = new Vector2[3];
	private bool wasDrawing;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsPreset(LayoutPreset.FullRect);
	}

	public override void _Process(double delta)
	{
		if (Target.HasValue || wasDrawing)
			QueueRedraw();
	}

	public override void _Draw()
	{
		wasDrawing = false;
		if (Target is not Vector2 target)
			return;

		Viewport viewport = GetViewport();
		Rect2 screen = viewport.GetVisibleRect();
		Vector2 onScreen = viewport.GetCanvasTransform() * target;
		if (screen.Grow(VisibleMargin - 8f).HasPoint(onScreen))
			return;

		Rect2 inner = screen.GrowIndividual(-EdgeInset, -TopInset, -EdgeInset, -EdgeInset);
		Vector2 centre = inner.GetCenter();
		Vector2 direction = (onScreen - centre).Normalized();
		float scaleX = direction.X != 0f ? (inner.Size.X * 0.5f) / Mathf.Abs(direction.X) : float.MaxValue;
		float scaleY = direction.Y != 0f ? (inner.Size.Y * 0.5f) / Mathf.Abs(direction.Y) : float.MaxValue;
		Vector2 at = centre + direction * Mathf.Min(scaleX, scaleY);

		// A slow breath, as ChestPointer's, a little quicker: this one is the thing to go to.
		float pulse = 1f + 0.07f * Mathf.Sin(Time.GetTicksMsec() * 0.006f);
		float r = Radius * pulse;
		Vector2 side = new(-direction.Y, direction.X);
		tail[0] = at + direction * (r + TailLength);
		tail[1] = at + direction * (r - 4f) + side * 9f;
		tail[2] = at + direction * (r - 4f) - side * 9f;

		DrawCircle(at + new Vector2(0f, 2f), r + 2f, Shadow);
		DrawColoredPolygon(tail, Rim);
		DrawCircle(at, r + 2.5f, Rim);
		DrawCircle(at, r, Fill);

		if (Icon != null)
		{
			// Fitted inside the badge whatever the frame's size: a boss frame is far bigger than a chest's.
			Vector2 size = Icon.GetSize();
			float fit = (r * 1.55f) / Mathf.Max(1f, Mathf.Max(size.X, size.Y));
			Vector2 drawn = size * fit;
			DrawTextureRect(Icon, new Rect2(at - drawn * 0.5f, drawn), false);
		}
		else
		{
			DrawGlyph?.Invoke(this, at, r);
		}
		wasDrawing = true;
	}
}
