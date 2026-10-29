using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

/// <summary>
/// The drawn world on the world map: the picture, the page off its edge, the fog over held places,
/// the stand-point rings, and the selected wizard walking the road, under a camera that leads him.
/// </summary>
/// <remarks>
/// WHOLE SCREEN PIXELS (.ai/art-direction.md 6a). Everything here is drawn by this one control at a
/// whole number of SCREEN pixels per art pixel - the nearest whole number to x2 on this window - and
/// every position is snapped to a screen pixel, the rule PixelArtRect follows for one sprite. A
/// Camera2D over a world node would be the engine's way, and it would put the map and the wizard on
/// the canvas's fractional grid on any window that is not 1:1 with it.
///
/// THE WIZARD IS ONE NUMBER: his arc length along the road (.ai/world-map.md, section 3). The
/// selection sets a target arc length and he moves toward it along the road every frame, so he can
/// never teleport, a new target mid-walk turns him where he stands, and his animation is chosen
/// every frame from whether he has arrived - it cannot stick.
///
/// THE CAMERA LEADS ON A LEASH. It eases toward the selected place's focus and is then clamped so the
/// wizard stays inside the view: on a long jump it runs ahead until he reaches the frame's edge and
/// then drags him along. Nothing per frame allocates.
/// </remarks>
public partial class WorldMapView : Control
{
	/// <summary>Art pixels are drawn at this many canvas units on a 1:1 window (x2, the project's one render scale).</summary>
	public const int ArtScale = 2;

	/// <summary>The slowest the wizard runs, in art pixels a second - a brisk run between neighbours.</summary>
	public const float MinSpeed = 150f;
	/// <summary>The fastest, so a jump across the whole world is a run of about two seconds.</summary>
	public const float MaxSpeed = 1100f;
	/// <summary>Speed per art pixel left to go: the further the target, the faster he runs.</summary>
	public const float SpeedPerDistance = 3.0f;
	/// <summary>How quickly the camera closes on its target, per second (exponential).</summary>
	public const float CameraRate = 7f;

	public enum StopLook
	{
		Open,
		Locked,
		Unbuilt,
	}

	public sealed class ViewStop
	{
		public WorldMapStop Layout { get; init; }
		public StopLook Look { get; set; }
	}

	private WorldMapLayout layout;
	private ViewStop[] stops = Array.Empty<ViewStop>();
	private Texture2D[] shrouds = Array.Empty<Texture2D>();
	private Texture2D world;
	private Texture2D page;
	private Texture2D ringOpen;
	private Texture2D ringLocked;
	private Texture2D ringCurrent;
	private Texture2D shadow;
	private Texture2D lockGlyph;
	private readonly List<(Texture2D Texture, Vector2 At)> drawings = new();

	private Texture2D[] idleFrames = Array.Empty<Texture2D>();
	private Texture2D[] movingFrames = Array.Empty<Texture2D>();
	private float idleFps = 6f;
	private float movingFps = 10f;
	private bool sheetFacesLeft;
	private Color tint = Colors.White;

	private int selected;
	private float wizardS;
	private float targetS;
	private bool facingLeft;
	private double animClock;
	private Vector2 camera;
	private Vector2 lastWizardPosition;
	private float refuseClock;

	// The transform of the last draw, for hit-testing a tap.
	private Vector2 lastOrigin;
	private float lastUnitsPerArt = ArtScale;

	/// <summary>A stop was tapped or clicked on the map.</summary>
	public event Action<int> StopPressed;

	public int Selected => selected;
	public bool IsWalking => Mathf.Abs(targetS - wizardS) > 0.5f;
	public float WizardSpeed { get; private set; }
	public Vector2 WizardPosition => layout?.PointAt(wizardS) ?? Vector2.Zero;
	public Vector2 Camera => camera;
	/// <summary>Screen pixels per art pixel at the last draw. For the harness.</summary>
	public int ScreenPixelsPerArtPixel { get; private set; }

	public WorldMapView()
	{
		TextureFilter = TextureFilterEnum.Nearest;
		ClipContents = true;
		MouseFilter = MouseFilterEnum.Stop;
	}

	public void Setup(WorldMapLayout map, IReadOnlyList<ViewStop> viewStops, CharacterData character, int startIndex)
	{
		layout = map;
		stops = new ViewStop[viewStops.Count];
		shrouds = new Texture2D[viewStops.Count];
		for (int i = 0; i < viewStops.Count; i++)
		{
			stops[i] = viewStops[i];
			string image = viewStops[i].Layout.ShroudImage;
			shrouds[i] = string.IsNullOrEmpty(image) ? null : Load(WorldMapLayout.Directory + image);
		}
		world = Load(WorldMapLayout.Directory + "world.png");
		page = Load(WorldMapLayout.Directory + map.PageImage);
		ringOpen = Load(WorldMapLayout.Directory + "ring-open.png");
		ringLocked = Load(WorldMapLayout.Directory + "ring-locked.png");
		ringCurrent = Load(WorldMapLayout.Directory + "ring-current.png");
		shadow = Load(WorldMapLayout.Directory + "shadow.png");
		lockGlyph = StatIcons.Load("lock");
		drawings.Clear();
		foreach (var (path, at) in map.PageDrawings)
		{
			// The kids' drawings, from their own files, untouched (.ai/world-map.md, section 4).
			Texture2D drawing = Load(path);
			if (drawing != null)
				drawings.Add((drawing, at));
		}
		SetCharacter(character);

		selected = Mathf.Clamp(startIndex, 0, Math.Max(0, stops.Length - 1));
		wizardS = targetS = stops.Length > 0 ? stops[selected].Layout.ArcLength : 0f;
		lastWizardPosition = map.PointAt(wizardS);
		camera = stops.Length > 0 ? stops[selected].Layout.Focus : map.Size / 2f;
		// Placed, not walked: the wizard opens standing at his stop and the camera on it.
		CallDeferred(nameof(SnapCamera));
	}

	private static Texture2D Load(string path)
	{
		if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path))
		{
			GD.PushError($"WorldMapView: '{path}' is missing or not imported.");
			return null;
		}
		return GD.Load<Texture2D>(path);
	}

	private void SetCharacter(CharacterData character)
	{
		tint = CharacterVisuals.GetCharacterTint(character?.Id);
		sheetFacesLeft = CharacterVisuals.SpriteFacesLeft(character?.Id);
		SpriteFrames frames = character?.Frames;
		if (frames != null && frames.HasAnimation("idle"))
		{
			idleFrames = FramesOf(frames, "idle");
			idleFps = (float)frames.GetAnimationSpeed("idle");
			if (frames.HasAnimation("moving"))
			{
				movingFrames = FramesOf(frames, "moving");
				movingFps = (float)frames.GetAnimationSpeed("moving");
			}
		}
		else
		{
			// No sheet of his own (the Test Wizard, whose sprite is Eric's drawing and is never derived):
			// the idle built from the portrait, played quicker while he walks.
			idleFrames = CharacterVisuals.GetIdleFrames(character?.Portrait).ToArray();
			idleFps = CharacterVisuals.IdleFramesPerSecond;
		}
		if (movingFrames.Length == 0)
		{
			movingFrames = idleFrames;
			movingFps = idleFps * 2f;
		}
	}

	private static Texture2D[] FramesOf(SpriteFrames frames, string animation)
	{
		int count = frames.GetFrameCount(animation);
		var result = new Texture2D[count];
		for (int i = 0; i < count; i++)
			result[i] = frames.GetFrameTexture(animation, i);
		return result;
	}

	public void SetLook(int index, StopLook look)
	{
		if (index >= 0 && index < stops.Length)
			stops[index].Look = look;
	}

	/// <summary>Makes <paramref name="index"/> the selected stop. The wizard walks to it.</summary>
	public void Select(int index)
	{
		if (stops.Length == 0)
			return;
		selected = Mathf.Clamp(index, 0, stops.Length - 1);
		targetS = stops[selected].Layout.ArcLength;
	}

	/// <summary>A refused start: the view gives a short shake, so the press visibly landed.</summary>
	public void Refuse() => refuseClock = 0.3f;

	/// <summary>
	/// Where the wizard is among the stops: 2.5 is halfway from the third to the fourth. What the
	/// compass strip slides by.
	/// </summary>
	public float WizardStopProgress
	{
		get
		{
			if (stops.Length == 0)
				return 0f;
			for (int i = 0; i < stops.Length - 1; i++)
			{
				float a = stops[i].Layout.ArcLength;
				float b = stops[i + 1].Layout.ArcLength;
				if (wizardS <= b || i == stops.Length - 2)
				{
					if (wizardS <= a)
						return i;
					return i + Mathf.Clamp((wizardS - a) / Mathf.Max(0.001f, b - a), 0f, 1f);
				}
			}
			return 0f;
		}
	}

	public override void _Process(double delta)
	{
		if (layout == null)
			return;
		float dt = (float)delta;

		// THE WALK. Speed grows with the distance left, so five quick presses are one run, not five
		// queued walks, and a jump across the world is still a run along the road.
		float remaining = targetS - wizardS;
		if (Mathf.Abs(remaining) > 0.01f)
		{
			WizardSpeed = Mathf.Clamp(Mathf.Abs(remaining) * SpeedPerDistance, MinSpeed, MaxSpeed);
			float step = WizardSpeed * dt;
			wizardS = Mathf.Abs(remaining) <= step ? targetS : wizardS + Mathf.Sign(remaining) * step;
		}
		else
		{
			wizardS = targetS;
			WizardSpeed = 0f;
		}
		Vector2 position = layout.PointAt(wizardS);
		float dx = position.X - lastWizardPosition.X;
		if (Mathf.Abs(dx) > 0.05f)
			facingLeft = dx < 0f;
		lastWizardPosition = position;
		animClock += delta * (IsWalking ? Mathf.Clamp(WizardSpeed / MinSpeed, 1f, 2.5f) : 1f);

		// THE CAMERA: toward the selected place, inside the drawn world, and never without him.
		if (stops.Length > 0)
		{
			Vector2 target = stops[selected].Layout.Focus;
			float k = 1f - Mathf.Exp(-CameraRate * dt);
			camera = camera.Lerp(target, k);
		}
		camera = Constrain(camera);
		if (refuseClock > 0f)
			refuseClock = Mathf.Max(0f, refuseClock - dt);

		QueueRedraw();
	}

	/// <summary>Jumps the camera onto its target at once. The opening frame, and the harness.</summary>
	public void SnapCamera()
	{
		if (stops.Length > 0)
			camera = Constrain(stops[selected].Layout.Focus);
		QueueRedraw();
	}

	private Vector2 Constrain(Vector2 at)
	{
		if (layout == null)
			return at;
		Vector2 half = HalfViewInArt();
		// Inside the drawn world: the picture, and the page past its east edge.
		float minX = half.X;
		float maxX = layout.RightEdge - half.X;
		at.X = minX > maxX ? layout.RightEdge / 2f : Mathf.Clamp(at.X, minX, maxX);
		float minY = half.Y;
		float maxY = layout.Size.Y - half.Y;
		at.Y = minY > maxY ? layout.Size.Y / 2f : Mathf.Clamp(at.Y, minY, maxY);
		// The leash, last, so it wins: his whole figure stays in the frame with room to spare.
		Vector2 body = layout.PointAt(wizardS) + new Vector2(0f, -20f);
		float ax = Mathf.Max(0f, half.X - 40f);
		float ay = Mathf.Max(0f, half.Y - 44f);
		at.X = Mathf.Clamp(at.X, body.X - ax, body.X + ax);
		at.Y = Mathf.Clamp(at.Y, body.Y - ay, body.Y + ay);
		return at;
	}

	/// <summary>Half the view, in art pixels, at the scale the next draw will use.</summary>
	private Vector2 HalfViewInArt()
	{
		float units = UnitsPerArt(out _, out _);
		return Size / 2f / Mathf.Max(0.001f, units);
	}

	/// <summary>Canvas units per art pixel: the whole number of screen pixels nearest x2, in canvas units.</summary>
	private float UnitsPerArt(out Transform2D toScreen, out int screenPixels)
	{
		toScreen = GetViewport().GetFinalTransform() * GetGlobalTransformWithCanvas();
		float screenPerUnit = toScreen.X.Length();
		if (screenPerUnit <= 0.0001f)
			screenPerUnit = 1f;
		screenPixels = Math.Max(1, (int)Math.Round(ArtScale * screenPerUnit, MidpointRounding.AwayFromZero));
		return screenPixels / screenPerUnit;
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, Size), new Color("#05070C"));
		if (layout == null || world == null)
			return;

		float u = UnitsPerArt(out Transform2D toScreen, out int screenPixels);
		ScreenPixelsPerArtPixel = screenPixels;
		Transform2D fromScreen = toScreen.AffineInverse();
		Vector2 shake = refuseClock > 0f ? new Vector2(Mathf.Sin(refuseClock * 60f) * 3f * refuseClock / 0.3f, 0f) : Vector2.Zero;
		Vector2 origin = Size / 2f - (camera + shake) * u;
		origin = fromScreen * (toScreen * origin).Round();
		lastOrigin = origin;
		lastUnitsPerArt = u;

		Vector2 At(Vector2 art) => fromScreen * (toScreen * (origin + art * u)).Round();
		void Blit(Texture2D texture, Vector2 artTopLeft, Color? modulate = null)
		{
			if (texture == null)
				return;
			DrawTextureRect(texture, new Rect2(At(artTopLeft), texture.GetSize() * u), false, modulate ?? Colors.White);
		}

		DrawTextureRect(world, new Rect2(origin, world.GetSize() * u), false);
		Blit(page, layout.PageOrigin);
		foreach (var (texture, at) in drawings)
			Blit(texture, at);

		// His fog over the places he still holds, and the lock above each. UI icons are saved x2, so
		// a 24-pixel glyph is 12 art pixels.
		for (int i = 0; i < stops.Length; i++)
		{
			if (stops[i].Look == StopLook.Open || shrouds[i] == null)
				continue;
			Vector2 size = shrouds[i].GetSize();
			Vector2 focus = stops[i].Layout.Focus;
			Blit(shrouds[i], focus - size / 2f);
			if (stops[i].Look == StopLook.Unbuilt)
				Blit(shrouds[i], focus - size / 2f + new Vector2(3f, -2f));
			if (lockGlyph != null)
			{
				Vector2 glyph = lockGlyph.GetSize() / 2f;
				DrawTextureRect(lockGlyph, new Rect2(At(focus - glyph / 2f - new Vector2(0f, 6f)), glyph * u), false);
			}
		}

		for (int i = 0; i < stops.Length; i++)
		{
			Texture2D ring = i == selected ? ringCurrent : stops[i].Look == StopLook.Open ? ringOpen : ringLocked;
			if (ring != null)
				Blit(ring, stops[i].Layout.Stand - ring.GetSize() / 2f + new Vector2(0f, 1f));
		}

		// The wizard: his shadow, then him, feet on the road.
		Vector2 feet = layout.PointAt(wizardS);
		if (shadow != null)
			Blit(shadow, feet - new Vector2(shadow.GetWidth() / 2f, 3f));
		Texture2D[] frames = IsWalking ? movingFrames : idleFrames;
		float fps = IsWalking ? movingFps : idleFps;
		if (frames.Length > 0)
		{
			int frame = (int)(animClock * Math.Max(1f, fps)) % frames.Length;
			Texture2D sprite = frames[frame];
			if (sprite != null)
			{
				Vector2 cell = sprite.GetSize();
				// Feet on row cell-2, the contract's anchor (.ai/art-direction.md, section 5).
				Vector2 topLeft = At(feet - new Vector2(cell.X / 2f, cell.Y - 2f));
				Vector2 drawn = cell * u;
				bool flip = facingLeft != sheetFacesLeft;
				if (flip)
				{
					DrawSetTransform(topLeft + new Vector2(drawn.X, 0f), 0f, new Vector2(-1f, 1f));
					DrawTextureRect(sprite, new Rect2(Vector2.Zero, drawn), false, tint);
					DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
				}
				else
				{
					DrawTextureRect(sprite, new Rect2(topLeft, drawn), false, tint);
				}
			}
		}
	}

	/// <summary>The stop under a point of this control, or -1.</summary>
	public int StopAt(Vector2 local)
	{
		if (layout == null || lastUnitsPerArt <= 0f)
			return -1;
		Vector2 art = (local - lastOrigin) / lastUnitsPerArt;
		int best = -1;
		float bestDistance = 56f;
		for (int i = 0; i < stops.Length; i++)
		{
			float d = Mathf.Min(art.DistanceTo(stops[i].Layout.Focus), art.DistanceTo(stops[i].Layout.Stand));
			if (d < bestDistance)
			{
				bestDistance = d;
				best = i;
			}
		}
		return best;
	}

	/// <summary>Where a stop's landmark is on this control, in canvas units. For the harness.</summary>
	public Vector2 LocalOf(int index) =>
		index < 0 || index >= stops.Length ? Vector2.Zero : lastOrigin + stops[index].Layout.Focus * lastUnitsPerArt;

	public override void _GuiInput(InputEvent @event)
	{
		// The mouse button only: a finger arrives as one too (emulate_mouse_from_touch), and
		// listening to both would select twice (.ai/controller-and-focus.md, "Touch").
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press)
		{
			int hit = StopAt(press.Position);
			if (hit >= 0)
			{
				StopPressed?.Invoke(hit);
				AcceptEvent();
			}
		}
	}
}
