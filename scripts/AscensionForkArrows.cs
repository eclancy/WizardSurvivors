using Godot;
using System.Collections.Generic;

/// <summary>
/// The fork in an ascension tree: a trunk out of the spell, splitting into one arrow per path. An
/// open path - one the player can take, or could the moment the spell reaches it - is a solid gold
/// line with a spark running down it; a closed one is a dashed line in the quiet ink.
/// </summary>
/// <remarks>
/// From the Pixel 9: "if there are two options available for the same spell, it should show the
/// spell on top, and two arrows pointing below to the 2 ascension options available". The browser
/// had been a list of rows, one per path, each saying "Ascension of Cinderbreath" in small print -
/// the fact that two rows were the SAME decision was a thing to read, not a thing to see.
///
/// TWO SHAPES. <see cref="Layout.Fork"/> sits in a strip between the spell and a row of cards: the
/// trunk drops from the spell's centre, a bar runs to each card's centre, and an arrowhead lands on
/// the top of each card. <see cref="Layout.Spine"/> is for cards stacked in a column (a width too
/// narrow for two side by side): a spine runs down the left and an arrow turns into each card at its
/// middle, so the fork still reads as one spell becoming one of several things.
///
/// MEASURED FROM THE CARDS, NOT ASSUMED. Every point is taken from where the source and targets
/// actually are (their global rects, which share this layer's transform), and it redraws whenever
/// any of them moves - so a card that wraps to an extra line, or a phone that lays the row out
/// differently, cannot leave an arrow pointing at the gap between two cards.
///
/// CALM, like every other live thing on this screen: the spark runs down once every 2.4 s and takes
/// 1.4 s to do it, and the glow under an open line breathes on the Ascension button's 2.6 s clock.
/// The clock is Time.GetTicksMsec, which runs while the level-up has the tree paused. Nothing
/// processes unless a path is open. Every segment is axis-aligned and drawn as a rect on whole
/// pixels, so the lines are as crisp as the pixel frames around them; no allocation per frame.
///
/// INKS (tools/art/ui_contrast.py, "ascension tree"): an open line is gold base on the vellum page
/// (3.8:1, a graphic, against 3:1); a closed line is the meta ink (6.1:1). The glow and the spark
/// are decoration on top of a line that passes without them.
/// </remarks>
public partial class AscensionForkArrows : Control
{
	public enum Layout
	{
		/// <summary>Cards side by side below: trunk, bar, an arrow down into each.</summary>
		Fork,
		/// <summary>Cards stacked to the right: a spine down the left, an arrow into each.</summary>
		Spine,
	}

	// gold base, gold lit, gold hi; flesh shade (CodexRows.Vellum.InkMeta).
	public static readonly Color OpenLine = new Color(0.541f, 0.416f, 0.118f);
	public static readonly Color OpenGlow = new Color(0.847f, 0.690f, 0.290f);
	public static readonly Color SparkCore = new Color(1.0f, 0.941f, 0.753f);
	public static readonly Color ClosedLine = new Color(0.431f, 0.267f, 0.314f);

	private const float OpenWidth = 3f;
	private const float ClosedWidth = 2f;
	private const float GlowWidth = 9f;
	private const float HeadHalfWidth = 8f;
	private const float HeadLength = 10f;
	private const float DashOn = 6f;
	private const float DashOff = 5f;
	private const float SparkCycleSeconds = 2.4f;
	private const float SparkTravelSeconds = 1.4f;
	private const float BreathSeconds = 2.6f;

	public Layout Shape { get; set; } = Layout.Fork;

	/// <summary>Where the trunk comes from: the spell node (Fork). Unused by the spine.</summary>
	public Control Source { get; set; }

	private readonly List<Control> targets = new();
	private readonly List<bool> open = new();
	private readonly Vector2[] head = new Vector2[3];
	private readonly Vector2[] path = new Vector2[4];
	private bool anyOpen;

	/// <summary>An arrow into <paramref name="target"/>, solid and live when <paramref name="isOpen"/>.</summary>
	public void AddTarget(Control target, bool isOpen)
	{
		if (target == null)
			return;
		targets.Add(target);
		open.Add(isOpen);
		anyOpen |= isOpen;
	}

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		FocusMode = FocusModeEnum.None;
		// The level-up screen pauses the tree; a spark that froze with it would be a dot.
		ProcessMode = ProcessModeEnum.Always;
		Resized += QueueRedraw;
		if (Source != null)
			Source.ItemRectChanged += QueueRedraw;
		foreach (Control target in targets)
			target.ItemRectChanged += QueueRedraw;
		// A plate can move without its own rect changing - inside a wrapper whose siblings resize, as
		// a path with its own gate under it is - so a closed fork also redraws for its first frames,
		// until every container has settled. An open one redraws every frame anyway.
		settleFrames = 20;
		SetProcess(true);
	}

	private int settleFrames;

	public override void _ExitTree()
	{
		if (Source != null && IsInstanceValid(Source))
			Source.ItemRectChanged -= QueueRedraw;
		foreach (Control target in targets)
			if (IsInstanceValid(target))
				target.ItemRectChanged -= QueueRedraw;
	}

	public override void _Process(double delta)
	{
		if (IsVisibleInTree())
			QueueRedraw();
		if (settleFrames > 0 && --settleFrames == 0 && !anyOpen)
			SetProcess(false);
	}

	private static float Seconds() => Time.GetTicksMsec() / 1000f;

	// A point of another control, in this one's space. Everything here lives on one CanvasLayer with
	// no rotation or scale between, so a difference of global positions is the whole transform.
	private Vector2 Local(Control control, float fx, float fy)
	{
		Rect2 rect = control.GetGlobalRect();
		return rect.Position + new Vector2(rect.Size.X * fx, rect.Size.Y * fy) - GlobalPosition;
	}

	public override void _Draw()
	{
		if (targets.Count == 0)
			return;

		float breath = 0.5f - 0.5f * Mathf.Cos(Seconds() / BreathSeconds * Mathf.Tau);
		var glow = new Color(OpenGlow, 0.22f + 0.30f * breath);
		// Where along its path the spark is, 0..1, or -1 between runs.
		float phase = Seconds() % SparkCycleSeconds;
		float spark = phase <= SparkTravelSeconds ? phase / SparkTravelSeconds : -1f;
		if (spark >= 0f)
			spark = spark * spark * (3f - 2f * spark);

		if (Shape == Layout.Fork)
			DrawFork(glow, spark);
		else
			DrawSpine(glow, spark);
	}

	private void DrawFork(Color glow, float spark)
	{
		if (Source == null || !IsInstanceValid(Source))
			return;
		float sx = Mathf.Floor(Local(Source, 0.5f, 1f).X);
		float mid = Mathf.Floor(Size.Y * 0.42f);
		float foot = Mathf.Floor(Size.Y);

		// Glow first, under every open line, so the closed lines are never haloed by a neighbour.
		if (anyOpen)
		{
			VSeg(sx, 0f, mid, GlowWidth, glow);
			for (int i = 0; i < targets.Count; i++)
			{
				if (!open[i] || !IsInstanceValid(targets[i]))
					continue;
				float tx = Mathf.Floor(Local(targets[i], 0.5f, 0f).X);
				HSeg(mid, sx, tx, GlowWidth, glow);
				VSeg(tx, mid, foot - HeadLength, GlowWidth, glow);
			}
		}

		// The trunk is open when any path out of it is.
		if (anyOpen)
			VSeg(sx, 0f, mid + 1f, OpenWidth, OpenLine);
		else
			VDash(sx, 0f, mid, ClosedWidth, ClosedLine);

		for (int i = 0; i < targets.Count; i++)
		{
			if (!IsInstanceValid(targets[i]))
				continue;
			float tx = Mathf.Floor(Local(targets[i], 0.5f, 0f).X);
			if (open[i])
			{
				HSeg(mid, sx, tx, OpenWidth, OpenLine);
				VSeg(tx, mid, foot - HeadLength, OpenWidth, OpenLine);
			}
			else
			{
				HDash(mid, sx, tx, ClosedWidth, ClosedLine);
				VDash(tx, mid, foot - HeadLength, ClosedWidth, ClosedLine);
			}
			DrawHead(new Vector2(tx, foot), Vector2.Down, open[i] ? OpenLine : ClosedLine, open[i] ? 1f : 0.75f);

			if (open[i] && spark >= 0f)
			{
				path[0] = new Vector2(sx, 0f);
				path[1] = new Vector2(sx, mid);
				path[2] = new Vector2(tx, mid);
				path[3] = new Vector2(tx, foot - HeadLength);
				DrawSpark(PointAlong(spark));
			}
		}
	}

	private void DrawSpine(Color glow, float spark)
	{
		float cx = Mathf.Floor(Mathf.Min(Size.X * 0.4f, Size.X - HeadLength - 6f));
		float right = Mathf.Floor(Size.X);
		float last = 0f;
		for (int i = 0; i < targets.Count; i++)
			if (IsInstanceValid(targets[i]))
				last = Mathf.Max(last, Mathf.Floor(Local(targets[i], 0f, 0.5f).Y));

		if (anyOpen)
		{
			VSeg(cx, 0f, last, GlowWidth, glow);
			for (int i = 0; i < targets.Count; i++)
				if (open[i] && IsInstanceValid(targets[i]))
					HSeg(Mathf.Floor(Local(targets[i], 0f, 0.5f).Y), cx, right - HeadLength, GlowWidth, glow);
			VSeg(cx, 0f, last + 1f, OpenWidth, OpenLine);
		}
		else
		{
			VDash(cx, 0f, last, ClosedWidth, ClosedLine);
		}

		for (int i = 0; i < targets.Count; i++)
		{
			if (!IsInstanceValid(targets[i]))
				continue;
			float ty = Mathf.Floor(Local(targets[i], 0f, 0.5f).Y);
			if (open[i])
				HSeg(ty, cx, right - HeadLength, OpenWidth, OpenLine);
			else
				HDash(ty, cx, right - HeadLength, ClosedWidth, ClosedLine);
			DrawHead(new Vector2(right, ty), Vector2.Right, open[i] ? OpenLine : ClosedLine, open[i] ? 1f : 0.75f);

			if (open[i] && spark >= 0f)
			{
				path[0] = new Vector2(cx, 0f);
				path[1] = new Vector2(cx, ty);
				path[2] = new Vector2(cx, ty);
				path[3] = new Vector2(right - HeadLength, ty);
				DrawSpark(PointAlong(spark));
			}
		}
	}

	// The point a fraction of the way along path[0..3], by length.
	private Vector2 PointAlong(float t)
	{
		float total = 0f;
		for (int i = 0; i < 3; i++)
			total += path[i].DistanceTo(path[i + 1]);
		if (total <= 0f)
			return path[0];
		float want = t * total;
		for (int i = 0; i < 3; i++)
		{
			float len = path[i].DistanceTo(path[i + 1]);
			if (want <= len && len > 0f)
				return path[i].Lerp(path[i + 1], want / len);
			want -= len;
		}
		return path[3];
	}

	private void DrawSpark(Vector2 at)
	{
		Vector2 p = at.Floor();
		DrawRect(new Rect2(p - new Vector2(5f, 5f), new Vector2(11f, 11f)), new Color(OpenGlow, 0.45f), true);
		DrawRect(new Rect2(p - new Vector2(3f, 3f), new Vector2(7f, 7f)), OpenGlow, true);
		DrawRect(new Rect2(p - new Vector2(1f, 1f), new Vector2(3f, 3f)), SparkCore, true);
	}

	private void DrawHead(Vector2 tip, Vector2 direction, Color colour, float scale)
	{
		float half = Mathf.Round(HeadHalfWidth * scale);
		float length = Mathf.Round(HeadLength * scale);
		Vector2 back = tip - direction * length;
		Vector2 side = new Vector2(-direction.Y, direction.X) * half;
		head[0] = tip;
		head[1] = back + side;
		head[2] = back - side;
		DrawColoredPolygon(head, colour);
	}

	// Axis-aligned segments as rects on whole pixels. A 3-wide line centred on x covers x-1..x+1.
	private void VSeg(float x, float y0, float y1, float width, Color colour)
	{
		float top = Mathf.Min(y0, y1);
		float half = Mathf.Floor(width * 0.5f);
		DrawRect(new Rect2(x - half, top, width, Mathf.Abs(y1 - y0)), colour, true);
	}

	private void HSeg(float y, float x0, float x1, float width, Color colour)
	{
		float left = Mathf.Min(x0, x1);
		float half = Mathf.Floor(width * 0.5f);
		// Overlaps the corner by half a width so an elbow is square rather than notched.
		DrawRect(new Rect2(left - half, y - half, Mathf.Abs(x1 - x0) + width, width), colour, true);
	}

	private void VDash(float x, float y0, float y1, float width, Color colour)
	{
		for (float y = Mathf.Min(y0, y1); y < Mathf.Max(y0, y1); y += DashOn + DashOff)
			VSeg(x, y, Mathf.Min(y + DashOn, Mathf.Max(y0, y1)), width, colour);
	}

	private void HDash(float y, float x0, float x1, float width, Color colour)
	{
		float from = Mathf.Min(x0, x1);
		float to = Mathf.Max(x0, x1);
		for (float x = from; x < to; x += DashOn + DashOff)
		{
			float half = Mathf.Floor(width * 0.5f);
			DrawRect(new Rect2(x, y - half, Mathf.Min(DashOn, to - x), width), colour, true);
		}
	}
}
