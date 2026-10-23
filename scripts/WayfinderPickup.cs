using Godot;
using WizardSurvivors.scripts;

/// <summary>Which thing a chapter hides near the start to reveal its points of interest.</summary>
public enum WayfinderKind
{
	/// <summary>The default: a brass compass, open on a flat stone.</summary>
	Compass,
	/// <summary>The Cursed Dungeon: the jailer's own map of the cells, nailed to a post.</summary>
	GaolersMap,
	/// <summary>The Frozen Waste: a snow-capped cairn with a bearing plate on its top stone.</summary>
	BearingCairn,
}

// The thing that turns exploring into navigating (.ai/side-events.md, "The wayfinder").
//
// Reported: "on each map there is a way that you can get the key points of interest to appear as
// navigation arrows once you find the arcane compass / or some other way to unlock it on certain
// maps". So every chapter with a side event hides one of these 1.5 to 2.5 chunks from the start
// (SideEventDirector). Walk onto it and it is taken: every point of interest on the map - the event,
// each of its parts - gets an arrow at the edge of the screen for the rest of the run (PoiPointer).
//
// IT IS SOMETHING HE MISSED, so it carries the secrets' tell (.ai/stage-secrets.md section 2) and
// not the events': warm gold light that BREATHES on a slow 2.4 s period, where his lanterns and his
// far tells flicker cold. Up close, the breathing glint drawn into its frames; from further, a thin
// warm column and a wide faint glow, the same far tell the events have, in the other colour.
//
// Found with the feet, like everything else in the game: no button, no prompt. Code-only, no scene:
// a distance check, a sprite from tools/art/explore_props.py, and a light layer.
public partial class WayfinderPickup : Node2D
{
	public Node2DGame Game { get; set; }
	public WayfinderKind Kind { get; set; }
	public PointOfInterest Site { get; set; }

	/// <summary>Open ground kept round it, so no tree hides it and no spike plate sits on it.</summary>
	public const float Footprint = 90f;

	// From his boots (Player.FootPosition) to its foot: about stepping on it.
	private const float TakeRadius = 46f;
	private const float BreathSeconds = 2.4f;
	private const float VanishSeconds = 0.8f;
	private const float LineSeconds = 5f;

	public static string GlyphFor(WayfinderKind kind) => kind switch
	{
		WayfinderKind.GaolersMap => "map",
		WayfinderKind.BearingCairn => "cairn",
		_ => "compass",
	};

	private static string SpriteFor(WayfinderKind kind) => kind switch
	{
		WayfinderKind.GaolersMap => "gaolers-map",
		WayfinderKind.BearingCairn => "bearing-cairn",
		_ => "arcane-compass",
	};

	// Where each sprite's glint is, from its foot, in world pixels: explore_props.py calls glint() at
	// cell (10, 15) on the compass, (15, 7) on the map's nail and (12, 9) on the cairn's plate, against
	// a foot at column 16, ground row 29, all at x2.
	private static Vector2 GlintOffset(WayfinderKind kind) => kind switch
	{
		WayfinderKind.GaolersMap => new Vector2(-1f, -44f),
		WayfinderKind.BearingCairn => new Vector2(-7f, -40f),
		_ => new Vector2(-11f, -28f),
	};

	/// <summary>What the line under the timer says when it is taken. Information, not mood.</summary>
	public static string TakenLine(WayfinderKind kind) => kind switch
	{
		WayfinderKind.GaolersMap => "The Gaoler's map - the cells he keeps are marked",
		WayfinderKind.BearingCairn => "A bearing cairn - what lies under the ice is marked",
		_ => "The Arcane Compass - what he keeps here is marked",
	};

	/// <summary>True once taken (it lingers a moment, vanishing).</summary>
	public bool Taken { get; private set; }

	private Sprite2D sprite;
	private ExploreSprite art;
	private Node2D lightLayer;
	private float clock;
	private float takenAge;

	// Gold, the trim and lantern row (.ai/art-direction.md section 3): hi #FFF0C0, lit #D8B04A.
	private static readonly Color Warm = new Color(1f, 0.941f, 0.753f);
	private static readonly Color WarmDeep = new Color(0.847f, 0.690f, 0.290f);

	public override void _Ready()
	{
		// Its node is its sort key, put SortLift above its foot like every grounded sprite
		// (WorldSprites), so the wizard is drawn in front of it when his boots are south of it.
		Position = Site.Position - new Vector2(0f, WorldSprites.SortLift);
		art = ExploreArt.Get(SpriteFor(Kind));
		if (art?.Texture != null)
		{
			sprite = ExploreArt.MakeGrounded(art, new Vector2(0f, WorldSprites.SortLift));
			AddChild(sprite);
		}
		else
		{
			GD.PushError($"WayfinderPickup: {SpriteFor(Kind)}.png or explore.json is missing; only its light will show. "
				+ "Re-run: python tools/art/explore_props.py, then import.");
		}

		lightLayer = new Node2D
		{
			Name = "Light",
			ZIndex = 3,
			Material = ExploreArt.Additive,
			TextureFilter = TextureFilterEnum.Linear,
		};
		lightLayer.Draw += OnLightDraw;
		AddChild(lightLayer);
	}

	public override void _ExitTree()
	{
		if (lightLayer != null && IsInstanceValid(lightLayer))
			lightLayer.Draw -= OnLightDraw;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		lightLayer?.QueueRedraw();

		if (Taken)
		{
			takenAge += dt;
			float t = Mathf.Clamp(takenAge / VanishSeconds, 0f, 1f);
			// It lifts off the ground and is gone, the way a thing you have picked up is.
			if (sprite != null)
			{
				sprite.Position = new Vector2(0f, -14f * (1f - (1f - t) * (1f - t)));
				sprite.Modulate = new Color(1f, 1f, 1f, 1f - t);
			}
			if (takenAge >= VanishSeconds)
				QueueFree();
			return;
		}

		if (sprite != null)
		{
			// The four frames are the glint's breath: trough, rising, crest, falling. Frame 2 is
			// centred on the light's crest so the two never disagree.
			float phase = Mathf.PosMod(clock / BreathSeconds, 1f);
			sprite.Frame = Mathf.FloorToInt(Mathf.PosMod(phase + 0.125f, 1f) * 4f) % Mathf.Max(1, art.Frames);
		}

		if (Game == null || Game.RunFinished)
			return;
		// No set-pieces in the duel, and nothing left to find once it starts.
		if (Game.BossFightActive)
		{
			Site.Done = true;
			QueueFree();
			return;
		}
		Player player = Game.RunPlayer;
		if (player == null || player.IsDead)
			return;
		if (player.FootPosition.DistanceTo(Site.Position) <= TakeRadius)
			Take();
	}

	/// <summary>Taken: every point of interest is revealed for the rest of the run.</summary>
	public void Take()
	{
		if (Taken)
			return;
		Taken = true;
		Site.Done = true;
		PointsOfInterest.Reveal();
		SfxPlayer.Global(SfxCatalog.BearingFound);
		ShowLine();
		if (OS.GetCmdlineUserArgs().Length > 0 && System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--explore-log") >= 0)
			GD.Print($"EXPLORE t={Game?.RunSeconds:0.0}s {Kind} taken at ({Site.Position.X:0},{Site.Position.Y:0}); {PointsOfInterest.Live.Count - 1} point(s) revealed");
	}

	// The objective line's slot is 84-112 under the timer; this goes just under it, so taking the
	// wayfinder while an event is running never writes over the event's own line.
	private void ShowLine()
	{
		var overlay = Game?.GetNodeOrNull<CanvasLayer>("UIOverlay");
		if (overlay == null)
			return;
		var label = new Label
		{
			Name = "WayfinderLine",
			Text = TakenLine(Kind),
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0f,
			AnchorRight = 1f,
			OffsetTop = 116f,
			OffsetBottom = 170f,
			OffsetLeft = 24f,
			OffsetRight = -24f,
			Modulate = new Color(1f, 1f, 1f, 0f),
		};
		ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Label);
		label.AddThemeColorOverride("font_color", Warm);
		label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
		label.AddThemeConstantOverride("outline_size", 6);
		overlay.AddChild(label);
		SafeArea.ApplyTo(label);
		// Its own tween, so the line outlives this node (which is gone in under a second).
		Tween tween = label.CreateTween();
		tween.TweenProperty(label, "modulate:a", 1f, 0.3f);
		tween.TweenInterval(LineSeconds - 1.3f);
		tween.TweenProperty(label, "modulate:a", 0f, 1f);
		tween.TweenCallback(Callable.From(label.QueueFree));
	}

	private void OnLightDraw()
	{
		if (lightLayer == null)
			return;
		Player player = Game?.RunPlayer;
		Vector2 foot = new Vector2(0f, WorldSprites.SortLift);
		if (Taken)
		{
			// A warm ring running out across the floor as it is lifted - the bearing taken.
			float t = Mathf.Clamp(takenAge / VanishSeconds, 0f, 1f);
			ExploreArt.DrawGlow(lightLayer, foot, 40f + 260f * t, new Color(Warm, 0.35f * (1f - t)));
			ExploreArt.DrawShaft(lightLayer, foot, 12f, 600f * (1f - t * 0.5f), new Color(WarmDeep, 0.4f * (1f - t)));
			return;
		}
		if (player == null || player.GlobalPosition.DistanceSquaredTo(Site.Position) > 2600f * 2600f)
			return;

		// The breath: 0.7 to 1.0, smooth, its crest where the glint's frame is.
		float breath = 0.85f - 0.15f * Mathf.Cos(Mathf.Tau * clock / BreathSeconds);
		ExploreArt.DrawGlow(lightLayer, foot, 420f, new Color(WarmDeep, 0.07f * breath));
		ExploreArt.DrawGlow(lightLayer, foot, 130f, new Color(WarmDeep, 0.2f * breath));
		ExploreArt.DrawShaft(lightLayer, foot, 16f, 480f, new Color(WarmDeep, 0.28f * breath));
		ExploreArt.DrawShaft(lightLayer, foot, 5f, 440f, new Color(Warm, 0.36f * breath));
		// Over the glint itself: small, so the glint stays a glint and does not become a spell.
		ExploreArt.DrawGlow(lightLayer, foot + GlintOffset(Kind), 22f, new Color(Warm, 0.55f * breath));
	}
}
