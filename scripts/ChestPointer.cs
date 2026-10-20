using Godot;

// A small bubble at the edge of the screen for every chest the player cannot see.
//
// The map is infinite now (.ai/terrain.md, section 7) and a chest drops wherever an elite died, so
// a chest that fell behind the swarm was easy to lose for good. This says where it is without a
// minimap: a round badge with the chest in it, a tail pointing outward toward it, pinned to the
// screen edge on the line from the centre. It is a different shape and colour from the side event's
// gold arrow (SideEventPointer) on purpose - an objective and a reward are different promises - and
// it draws nothing at all for a chest already on screen, which is its own marker.
//
// Reads ChestReward.Live rather than a scene-tree group: a group query hands back a fresh array
// every frame, and this runs every frame of a 140-enemy swarm.
public partial class ChestPointer : Control
{
	// Far enough in that the bubble clears the phone's rounded corners and the HUD bar's edge.
	private const float EdgeInset = 52f;
	private const float Radius = 21f;
	private const float TailLength = 11f;
	private const float IconSize = 34f;

	private static readonly Color Fill = new Color(0.09f, 0.08f, 0.12f, 0.92f);
	private static readonly Color Rim = new Color(0.96f, 0.80f, 0.42f, 1f);
	private static readonly Color Shadow = new Color(0f, 0f, 0f, 0.55f);

	private readonly Vector2[] tail = new Vector2[3];
	private Texture2D icon;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsPreset(LayoutPreset.FullRect);
		// The idle chest's first frame: the same object the player is looking for, not a symbol for it.
		Texture2D sheet = GD.Load<Texture2D>("res://assets/bonelight/props/chest.png");
		if (sheet != null)
			icon = new AtlasTexture { Atlas = sheet, Region = new Rect2(0, 0, 64, 64) };
	}

	public override void _Process(double delta)
	{
		// Nothing to redraw while no chest exists, which is most of a run.
		if (ChestReward.Live.Count > 0 || wasDrawing)
			QueueRedraw();
	}

	private bool wasDrawing;

	public override void _Draw()
	{
		wasDrawing = false;
		if (ChestReward.Live.Count == 0)
			return;

		Viewport viewport = GetViewport();
		Rect2 screen = viewport.GetVisibleRect();
		Transform2D canvas = viewport.GetCanvasTransform();
		Vector2 centre = screen.Size * 0.5f;
		Rect2 inner = screen.Grow(-EdgeInset);
		// A slow breath rather than a blink: noticeable at the edge of vision, never alarming.
		float pulse = 1f + 0.06f * Mathf.Sin(Time.GetTicksMsec() * 0.005f);

		foreach (ChestReward chest in ChestReward.Live)
		{
			if (!IsInstanceValid(chest) || chest.Opened)
				continue;

			Vector2 onScreen = canvas * chest.GlobalPosition;
			// Grown by the chest's own half-size, so the bubble goes the moment any of it is visible.
			if (screen.Grow(-8f).HasPoint(onScreen))
				continue;

			Vector2 direction = (onScreen - centre).Normalized();
			float scaleX = direction.X != 0f ? (inner.Size.X * 0.5f) / Mathf.Abs(direction.X) : float.MaxValue;
			float scaleY = direction.Y != 0f ? (inner.Size.Y * 0.5f) / Mathf.Abs(direction.Y) : float.MaxValue;
			Vector2 at = centre + direction * Mathf.Min(scaleX, scaleY);
			DrawBubble(at, direction, pulse);
			wasDrawing = true;
		}
	}

	private void DrawBubble(Vector2 at, Vector2 direction, float pulse)
	{
		float r = Radius * pulse;
		Vector2 side = new Vector2(-direction.Y, direction.X);

		// The tail first, so the badge's rim closes over its root.
		tail[0] = at + direction * (r + TailLength);
		tail[1] = at + direction * (r - 4f) + side * 8f;
		tail[2] = at + direction * (r - 4f) - side * 8f;

		DrawCircle(at + new Vector2(0f, 2f), r + 2f, Shadow);
		DrawColoredPolygon(tail, Rim);
		DrawCircle(at, r + 2f, Rim);
		DrawCircle(at, r, Fill);

		if (icon != null)
		{
			float size = IconSize * pulse;
			DrawTextureRect(icon, new Rect2(at - new Vector2(size, size) * 0.5f, new Vector2(size, size)), false);
		}
	}
}
