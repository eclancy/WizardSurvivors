using Godot;
using WizardSurvivors.scripts;

// The words of a chapter hint (StageHintDirector), at the edge of the screen the sound came from.
//
// A dark stone band with the line on it and a short tail pointing off toward the source - a speech
// bubble's tail, because the caption says where the voice is as much as what it says. Its rim is
// the colour of the event's far tell, the light the player will see when he gets close. It is a
// fourth shape beside the three the edge of the screen already carries (the objective's gold
// triangle, the chest's round bubble, the place's diamond, .ai/side-events.md "The arrows"), and
// it only ever shows while none of those points at the same place.
//
// Placed every frame from the player's position on the canvas and the direction to the source:
// pushed out along that line to the edge of a safe inner rectangle - under the HUD band (the touch
// stick's reserved band on a phone), clear of a phone's bottom third where the thumb sits, inset by
// the safe area - and never over the wizard himself. It takes no input at all (MouseFilter.Ignore,
// and so does its label): a finger on it lands on the touch stick beneath as if it were not there.
//
// Legibility (.ai/level-up-screen.md 4a): the engine sans at the Body role, pale ink on a near-black
// band - parchment on #0D0F17 is about 15:1, well over the 4.5:1 floor - with the rim and the tail
// carrying the colour, never the words.
public partial class StageHintCaption : Control
{
	private const float FadeInSeconds = 0.35f;
	private const float HoldSeconds = 4.8f;
	private const float FadeOutSeconds = 0.9f;

	private const float PadX = 16f;
	private const float PadY = 10f;
	private const float TailLength = 20f;
	private const float TailHalfWidth = 10f;
	// Clear of the HUD across the top on a PC: the timer, the boss bar's slot, the objective line and
	// the onboarding tip all end by 128; the loadout column on the left by about 150.
	private const float WideTop = 150f;
	private const float WideBottom = 40f;
	private const float Side = 18f;
	// On a phone: under the touch stick's band (TouchControls.DefaultHudBandHeight) plus a little,
	// and out of the bottom 30% of the screen, where the thumb holding the stick is.
	private const float NarrowBandGap = 18f;
	private const float NarrowThumbShare = 0.30f;
	private const float WideMaxTextWidth = 430f;
	private const float NarrowMaxTextWidth = 470f;
	// The wizard at the centre of the screen, with his bow out: the caption is never put over him.
	private static readonly Vector2 PlayerKeepOut = new Vector2(78f, 96f);

	private static readonly Color Band = new Color(0.05f, 0.06f, 0.09f, 0.92f);
	private static readonly Color SpeechInk = new Color(0.97f, 0.93f, 0.84f);
	private static readonly Color SoundInk = new Color(0.84f, 0.89f, 0.94f);

	private Label label;
	private StyleBoxFlat bandStyle;
	private readonly Vector2[] tail = new Vector2[3];
	private readonly Vector2[] tailRim = new Vector2[3];

	private Node2D anchor;
	private Vector2 source;
	private Color rim = Colors.White;
	private Vector2 textSize;
	private float age = -1f;
	private Rect2 box;
	private Vector2 tailDirection;

	/// <summary>True while a caption is on screen (fading in, held or fading out).</summary>
	public bool Showing => age >= 0f;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsPreset(LayoutPreset.FullRect);
		Visible = false;

		label = new Label
		{
			Name = "HintLine",
			MouseFilter = MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Body);
		AddChild(label);

		bandStyle = new StyleBoxFlat { BgColor = Band };
		bandStyle.SetCornerRadiusAll(6);
		bandStyle.SetBorderWidthAll(2);
	}

	/// <summary>Puts a line up, pointing from <paramref name="from"/> toward <paramref name="worldSource"/>.</summary>
	public void ShowLine(string line, HintVoice voice, Node2D from, Vector2 worldSource, Color rimColor)
	{
		if (label == null)
			return;
		anchor = from;
		source = worldSource;
		rim = new Color(rimColor, 0.95f);
		bandStyle.BorderColor = rim;

		// Speech is set in quotation marks, as somebody's words; a sound plainly, as a caption of it.
		label.Text = voice == HintVoice.Speech ? $"“{line}”" : line;
		label.AddThemeColorOverride("font_color", voice == HintVoice.Speech ? SpeechInk : SoundInk);

		// Measured once per line, not per frame: as wide as the line wants up to the column, then
		// as tall as it wraps to at that width.
		Font font = label.GetThemeFont("font");
		int size = label.GetThemeFontSize("font_size");
		float maxWidth = ResponsiveLayout.IsNarrow(this) ? NarrowMaxTextWidth : WideMaxTextWidth;
		float natural = font.GetStringSize(label.Text, HorizontalAlignment.Left, -1, size).X;
		float width = Mathf.Min(natural + 2f, maxWidth);
		float height = font.GetMultilineStringSize(label.Text, HorizontalAlignment.Center, width, size).Y;
		textSize = new Vector2(Mathf.Ceil(width), Mathf.Ceil(height));

		age = 0f;
		Visible = true;
		Layout();
		QueueRedraw();
	}

	/// <summary>Starts the fade-out now (the boss has arrived, or the place has been found).</summary>
	public void Dismiss()
	{
		if (age >= 0f && age < FadeInSeconds + HoldSeconds)
			age = FadeInSeconds + HoldSeconds;
	}

	public override void _Process(double delta)
	{
		if (age < 0f)
			return;
		age += (float)delta;
		float total = FadeInSeconds + HoldSeconds + FadeOutSeconds;
		if (age >= total || anchor == null || !IsInstanceValid(anchor))
		{
			age = -1f;
			anchor = null;
			Visible = false;
			return;
		}
		float fadeIn = Mathf.Clamp(age / FadeInSeconds, 0f, 1f);
		float fadeOut = Mathf.Clamp((total - age) / FadeOutSeconds, 0f, 1f);
		Modulate = new Color(1f, 1f, 1f, Mathf.Min(fadeIn, fadeOut));
		Layout();
		QueueRedraw();
	}

	private void Layout()
	{
		if (anchor == null || !IsInstanceValid(anchor))
			return;
		Viewport viewport = GetViewport();
		Rect2 screen = viewport.GetVisibleRect();
		Transform2D canvas = viewport.GetCanvasTransform();
		Vector2 player = canvas * anchor.GlobalPosition;
		Vector2 toward = canvas * source - player;
		Vector2 d = toward.LengthSquared() > 1f ? toward.Normalized() : Vector2.Up;

		var (left, top, right, bottom) = SafeArea.Insets(viewport);
		bool narrow = ResponsiveLayout.IsNarrow(this);
		float innerTop = narrow ? TouchControls.DefaultHudBandHeight + top + NarrowBandGap : WideTop + top;
		float innerBottom = narrow
			? screen.Size.Y * (1f - NarrowThumbShare) - bottom
			: screen.Size.Y - WideBottom - bottom;
		var inner = new Rect2(screen.Position.X + Side + left, screen.Position.Y + innerTop,
			screen.Size.X - 2f * Side - left - right, innerBottom - innerTop);

		Vector2 size = textSize + new Vector2(2f * PadX, 2f * PadY);
		Vector2 half = size * 0.5f;
		// Out along the line to the source until it meets the inner rectangle's edge (the tail's
		// length short of it), then held inside.
		Vector2 edge = ExitPoint(player, d, inner);
		Vector2 centre = Clamp(edge - d * (TailLength + Mathf.Min(half.X, half.Y)), inner, half);

		// Never over the wizard: on a phone a caption to the side is wider than the half-screen
		// beside him. Step it up off him (or down, for a source below), and hold it inside again.
		var keepOut = new Rect2(player - PlayerKeepOut, PlayerKeepOut * 2f);
		if (new Rect2(centre - half, size).Intersects(keepOut))
		{
			centre.Y = d.Y > 0.35f
				? keepOut.End.Y + half.Y + TailLength
				: keepOut.Position.Y - half.Y - TailLength;
			centre = Clamp(centre, inner, half);
		}

		box = new Rect2((centre - half).Round(), size);
		tailDirection = d;
		label.Position = box.Position + new Vector2(PadX, PadY);
		label.Size = textSize;
	}

	// Where a ray from inside the rectangle leaves it.
	private static Vector2 ExitPoint(Vector2 from, Vector2 d, Rect2 rect)
	{
		Vector2 p = from;
		p.X = Mathf.Clamp(p.X, rect.Position.X, rect.End.X);
		p.Y = Mathf.Clamp(p.Y, rect.Position.Y, rect.End.Y);
		float tx = d.X > 0.0001f ? (rect.End.X - p.X) / d.X : d.X < -0.0001f ? (rect.Position.X - p.X) / d.X : float.MaxValue;
		float ty = d.Y > 0.0001f ? (rect.End.Y - p.Y) / d.Y : d.Y < -0.0001f ? (rect.Position.Y - p.Y) / d.Y : float.MaxValue;
		return p + d * Mathf.Max(0f, Mathf.Min(tx, ty));
	}

	private static Vector2 Clamp(Vector2 centre, Rect2 inner, Vector2 half)
	{
		float minX = inner.Position.X + half.X, maxX = inner.End.X - half.X;
		float minY = inner.Position.Y + half.Y, maxY = inner.End.Y - half.Y;
		return new Vector2(
			minX <= maxX ? Mathf.Clamp(centre.X, minX, maxX) : inner.GetCenter().X,
			minY <= maxY ? Mathf.Clamp(centre.Y, minY, maxY) : inner.GetCenter().Y);
	}

	public override void _Draw()
	{
		if (age < 0f)
			return;

		// The tail leaves the band where the line to the source crosses its edge.
		Vector2 centre = box.GetCenter();
		Vector2 d = tailDirection;
		Vector2 root = ExitPoint(centre, d, box.Grow(-1f));
		Vector2 side = new Vector2(-d.Y, d.X);
		float width = TailHalfWidth;
		tailRim[0] = root + d * (TailLength + 2.5f);
		tailRim[1] = root - d * 6f + side * (width + 2.5f);
		tailRim[2] = root - d * 6f - side * (width + 2.5f);
		DrawColoredPolygon(tailRim, rim);

		DrawStyleBox(bandStyle, box);

		tail[0] = root + d * TailLength;
		tail[1] = root - d * 6f + side * width;
		tail[2] = root - d * 6f - side * width;
		DrawColoredPolygon(tail, Band);
	}
}
