using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Pixel art in a menu, drawn at a whole number of SCREEN pixels per art pixel at any window size,
/// and animated if it has more than one frame.
/// </summary>
/// <remarks>
/// Why it exists: the character select drew each wizard into a SubViewport at canvas resolution
/// (160x160 for a card) and showed that as a texture. Two things then blurred it, and both got
/// worse the bigger the window:
///   - a SubViewport has its own default texture filter, LINEAR, not the project's nearest - so the
///     wizard was bilinearly resampled at x3 inside it;
///   - the window then stretched the whole 1280x720 canvas by a fraction (canvas_items, 2.1375 on a
///     2736x1824 screen, 1.5 at 1080p, 1.25 in the default 1600x900 window), so those 160 soft
///     pixels were magnified again: every art pixel smeared across six or seven screen pixels.
/// The fix is to not render at canvas resolution at all. This draws the frame straight onto the
/// canvas, nearest-filtered, and sizes it from the screen rather than the canvas: it reads how many
/// screen pixels one canvas unit covers here (the window's stretch times every scale above it),
/// picks the whole number of screen pixels per art pixel nearest the scale it was asked for, and
/// draws the art that many canvas units big - 2.807 units per art pixel at 2.1375, which is exactly
/// 6 screen pixels. The top-left corner is snapped to a screen pixel so no edge falls between two.
///
/// So "a higher-resolution portrait on a big screen" is the same 48x48 art at a bigger WHOLE scale,
/// never a redrawn or resampled one (.ai/art-direction.md, section 6a).
///
/// Nothing allocates per frame: a redraw is queued only when the frame changes (six a second), the
/// window is resized, or the control moves (a list scrolling under it).
/// </remarks>
public partial class PixelArtRect : Control
{
	private Texture2D[] frames = Array.Empty<Texture2D>();
	private int frame;
	private double clock;
	private Viewport watchedViewport;

	/// <summary>
	/// The art scale this was laid out for, in canvas units per art pixel - what it would be on a
	/// window that shows the canvas 1:1. The drawn scale is the nearest whole number of SCREEN pixels
	/// to this, so on a 1:1 window they are the same.
	/// </summary>
	public int CanvasScale { get; set; } = 3;

	public float FramesPerSecond { get; set; } = 6f;

	/// <summary>Screen pixels per art pixel at the last draw. For the harness and for logs.</summary>
	public int ScreenPixelsPerArtPixel { get; private set; }

	public PixelArtRect()
	{
		TextureFilter = TextureFilterEnum.Nearest;
		MouseFilter = MouseFilterEnum.Ignore;
	}

	/// <param name="frames">One frame, or an animation played on a loop.</param>
	/// <param name="canvasScale">See <see cref="CanvasScale"/>.</param>
	/// <param name="box">The square the art must fit in, in canvas units; it is the control's minimum size.</param>
	public PixelArtRect(IReadOnlyList<Texture2D> frames, int canvasScale, float box) : this()
	{
		SetFrames(frames);
		CanvasScale = Math.Max(1, canvasScale);
		CustomMinimumSize = new Vector2(box, box);
	}

	public void SetFrames(IReadOnlyList<Texture2D> newFrames)
	{
		var kept = new List<Texture2D>();
		if (newFrames != null)
		{
			foreach (Texture2D texture in newFrames)
			{
				if (texture != null)
					kept.Add(texture);
			}
		}
		frames = kept.ToArray();
		frame = 0;
		clock = 0;
		SetProcess(frames.Length > 1);
		QueueRedraw();
	}

	public override void _Ready()
	{
		// Scrolling moves this without resizing it, and the snap to the screen grid depends on where
		// it is, so it has to hear about moves.
		SetNotifyTransform(true);
		SetProcess(frames.Length > 1);
		watchedViewport = GetViewport();
		watchedViewport?.Connect(Viewport.SignalName.SizeChanged, new Callable(this, MethodName.OnViewportSizeChanged));
	}

	public override void _ExitTree()
	{
		if (watchedViewport != null && IsInstanceValid(watchedViewport)
			&& watchedViewport.IsConnected(Viewport.SignalName.SizeChanged, new Callable(this, MethodName.OnViewportSizeChanged)))
		{
			watchedViewport.Disconnect(Viewport.SignalName.SizeChanged, new Callable(this, MethodName.OnViewportSizeChanged));
		}
		watchedViewport = null;
	}

	private void OnViewportSizeChanged() => QueueRedraw();

	public override void _Notification(int what)
	{
		if (what == NotificationTransformChanged || what == NotificationResized)
			QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (frames.Length < 2 || FramesPerSecond <= 0f)
			return;

		clock += delta;
		int next = (int)(clock * FramesPerSecond) % frames.Length;
		if (next == frame)
			return;
		frame = next;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (frames.Length == 0)
			return;

		Texture2D texture = frames[Math.Clamp(frame, 0, frames.Length - 1)];
		Vector2 art = texture.GetSize();
		if (art.X <= 0f || art.Y <= 0f)
			return;

		// Canvas-local units to the pixels of the window's render target: the stretch the window
		// applies to the canvas, times every transform from here up to the canvas.
		Transform2D toScreen = GetViewport().GetFinalTransform() * GetGlobalTransformWithCanvas();
		float screenPerUnit = toScreen.X.Length();
		if (screenPerUnit <= 0.0001f)
			screenPerUnit = 1f;

		ScreenPixelsPerArtPixel = WholeScreenScale(CanvasScale, screenPerUnit, art, Size);
		float unitsPerArtPixel = ScreenPixelsPerArtPixel / screenPerUnit;
		Vector2 drawn = art * unitsPerArtPixel;
		Vector2 topLeft = (Size - drawn) * 0.5f;

		// Snap the corner onto a screen pixel. Nearest sampling at a whole scale gives every art
		// pixel the same width wherever the corner falls, but a corner exactly half a pixel off puts
		// sample points on texel edges, where rounding can shave a row off one frame and not the next.
		Vector2 screenCorner = toScreen * topLeft;
		topLeft = toScreen.AffineInverse() * screenCorner.Round();

		DrawTextureRect(texture, new Rect2(topLeft, drawn), false);
	}

	/// <summary>
	/// The whole number of screen pixels per art pixel nearest <paramref name="canvasScale"/> on this
	/// window, capped so the art still fits <paramref name="box"/> (canvas units). Never below 1.
	/// </summary>
	public static int WholeScreenScale(int canvasScale, float screenPerUnit, Vector2 art, Vector2 box)
	{
		int wanted = (int)Math.Round(canvasScale * screenPerUnit, MidpointRounding.AwayFromZero);
		int fits = int.MaxValue;
		if (box.X > 0f && box.Y > 0f)
			fits = (int)Math.Floor(Math.Min(box.X / art.X, box.Y / art.Y) * screenPerUnit + 0.001f);
		return Math.Max(1, Math.Min(wanted, fits));
	}
}
