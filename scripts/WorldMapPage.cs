using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

/// <summary>
/// The Sketchbook's page: a map of its own, apart from the world (.ai/world-map.md, section 4). A loose
/// sheet of squared paper on the dark, the kids' drawings on it as themselves, and the selected wizard
/// standing at the end of its pencil road.
/// </summary>
/// <remarks>
/// Eric, 2026-10-30: "sketchbook level should not be attached to the map, remove it from the map. Once
/// you beat the whole campaign, you should be able to navigate to it, but it shows up as its own
/// separate map that doesn't mix in with the flow of the actual world map." So nothing here walks:
/// there is one place on the page and he is standing at it.
///
/// The page is generated (tools/art/world_map.py, sketchbook.png). What is drawn on it is the kids', laid
/// at runtime from their own files - untinted, never redrawn, recoloured, cropped or derived, at the same
/// whole scale as the paper (CLAUDE.md, the kids' art).
///
/// WHOLE SCREEN PIXELS, WorldMapView's rule: the nearest whole number of screen pixels to x2 per art
/// pixel, one step smaller at a time if the sheet would not fit, and every position snapped.
/// </remarks>
public partial class WorldMapPage : Control
{
	private WorldMapLayout layout;
	private Texture2D page;
	private Texture2D shadow;
	private readonly List<(Texture2D Texture, Vector2 At)> drawings = new();
	private WorldMapView.Figure figure = new();
	private double animClock;
	private float refuseClock;

	/// <summary>Screen pixels per art pixel at the last draw. For the harness.</summary>
	public int ScreenPixelsPerArtPixel { get; private set; }

	public WorldMapPage()
	{
		TextureFilter = TextureFilterEnum.Nearest;
		ClipContents = true;
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public void Setup(WorldMapLayout map, CharacterData character)
	{
		layout = map;
		page = WorldMapView.Load(WorldMapLayout.Directory + map.PageImage);
		shadow = WorldMapView.Load(WorldMapLayout.Directory + "shadow.png");
		drawings.Clear();
		foreach (var (path, at) in map.PageDrawings)
		{
			// The kids' drawings, from their own files, untouched.
			Texture2D drawing = WorldMapView.Load(path);
			if (drawing != null)
				drawings.Add((drawing, at));
		}
		figure = WorldMapView.Figure.Of(character);
		QueueRedraw();
	}

	/// <summary>A refused start: the page gives a short shake, as the world does.</summary>
	public void Refuse() => refuseClock = 0.3f;

	public override void _Process(double delta)
	{
		animClock += delta;
		if (refuseClock > 0f)
			refuseClock = Mathf.Max(0f, refuseClock - (float)delta);
		QueueRedraw();
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, Size), new Color("#05070C"));
		if (layout == null || page == null)
			return;

		Transform2D toScreen = GetViewport().GetFinalTransform() * GetGlobalTransformWithCanvas();
		float screenPerUnit = toScreen.X.Length();
		if (screenPerUnit <= 0.0001f)
			screenPerUnit = 1f;
		int screenPixels = Math.Max(1, (int)Math.Round(WorldMapView.ArtScale * screenPerUnit, MidpointRounding.AwayFromZero));
		// A step down while the sheet would not fit the space it is given.
		Vector2 sheet = layout.PageSize;
		while (screenPixels > 1 && (sheet.X * screenPixels / screenPerUnit > Size.X || sheet.Y * screenPixels / screenPerUnit > Size.Y))
			screenPixels--;
		ScreenPixelsPerArtPixel = screenPixels;
		float u = screenPixels / screenPerUnit;
		Transform2D fromScreen = toScreen.AffineInverse();

		Vector2 shake = refuseClock > 0f ? new Vector2(Mathf.Sin(refuseClock * 60f) * 3f * refuseClock / 0.3f, 0f) : Vector2.Zero;
		Vector2 origin = (Size - sheet * u) / 2f + shake * u;
		origin = fromScreen * (toScreen * origin).Round();

		DrawTextureRect(page, new Rect2(origin, page.GetSize() * u), false);
		foreach (var (texture, at) in drawings)
		{
			Vector2 topLeft = fromScreen * (toScreen * (origin + at * u)).Round();
			DrawTextureRect(texture, new Rect2(topLeft, texture.GetSize() * u), false);
		}

		// He stands at the end of the pencil road, facing into the page.
		Vector2 feet = layout.PageStand;
		if (shadow != null)
		{
			Vector2 shadowAt = fromScreen * (toScreen * (origin + (feet - new Vector2(shadow.GetWidth() / 2f, 3f)) * u)).Round();
			DrawTextureRect(shadow, new Rect2(shadowAt, shadow.GetSize() * u), false);
		}
		WorldMapView.DrawFigure(this, figure.Idle, figure.IdleFps, animClock, figure, false, feet, origin, u, toScreen, fromScreen);
	}
}
