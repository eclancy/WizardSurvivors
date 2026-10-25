using Godot;
using System.Collections.Generic;
using System.Text.Json;
using WizardSurvivors.scripts;

// The Broken Circle's side event: relight the circle (.ai/campaign.md section 6, .ai/side-events.md).
//
// "The circle where the wizards met. He came here first." Out on the heath stand the stones of the
// moot - eight of them, and a torn hollow where the ninth was pulled up (it is the boss). Each has a
// ward-rune cut in its face. Come to the circle and one rune lights; reach that stone before it
// gutters (about six seconds) and the next lights - never the stone beside it. Six in a row and the
// ward burns again round the whole ring, and what is left of it, a fleck still lit, is the player's:
// the Gilded Mote. Miss one and every rune goes dark, the count starts again, and the heath sends
// its people in at him.
//
// WHAT IT ASKS: FOLLOW. A route the event sets, against a clock, through the swarm. The grove asks
// the player to stay; the lanterns to explore, in any order; the well (the Fields') to defend; this
// is none of those - where to go next is decided for him, and the question is whether he can get
// there in time with the horde between.
//
// THE STONES ARE SOLID AND ARE NEVER TARGETS (the breakables rule, .ai/side-events.md): a sprite and a
// small footprint on the ground's physics layer, like a tree trunk, and nothing in "enemies". They
// outlast the event - won, lost or stood down by the boss, the circle is still standing on the heath
// - so when the event resolves they are handed to the run (StandOnTheHeath) before it frees itself.
public partial class MootFiresEvent : SideEvent
{
	/// <summary>Places in the ring. One is the ninth's empty socket; the rest are stones.</summary>
	[Export] public int RingPlaces { get; set; } = 9;
	[Export] public float RingRadius { get; set; } = 520f;
	/// <summary>Stones to light in one unbroken run.</summary>
	[Export] public int RunLength { get; set; } = 6;
	/// <summary>Seconds a lit rune waits for the player.</summary>
	[Export] public float RuneSeconds { get; set; } = 6.0f;
	/// <summary>How close to a stone's foot the player has to come to take its rune.</summary>
	[Export] public float TouchRadius { get; set; } = 110f;
	/// <summary>The dark beat after a miss before the first rune of the next run lights.</summary>
	[Export] public float RestartPauseSeconds { get; set; } = 1.6f;
	[Export] public int WaveSize { get; set; } = 8;
	[Export] public float WaveRadius { get; set; } = 440f;
	/// <summary>The stone's footprint on the ground's physics layer - a trunk's, so a body slides off it.</summary>
	[Export] public float StoneFootRadius { get; set; } = 18f;

	public const string Manifest = "res://assets/bonelight/world/heath/moot.json";

	private sealed class Stone
	{
		public Vector2 Foot;
		public int Place;
		public Node2D Node;
		public Sprite2D Sprite;
		public bool Lit;
		public float LitAt = -1f;
	}

	private readonly List<Stone> stones = new();
	private Vector2 socketAt;
	private Sprite2D socketSprite;
	private Texture2D stoneTexture;
	private int frameW, frameH, groundRow, anchorX, runeY;
	private Stone target;
	private float targetLeft;
	private int count;
	private float restartIn = -1f;
	private float flareUntil = -1f;
	private bool handedOver;

	protected override string Announcement => "The stones of the moot stand dark.";
	protected override string ChestLine => "The circle burns again - a chest is left in it";

	protected override string Objective => restartIn >= 0f
		? $"The runes went dark. Relight the circle  (0 of {RunLength})"
		: $"Reach the lit stone  ({count} of {RunLength})  {Mathf.CeilToInt(Mathf.Max(0f, targetLeft))}s";

	protected override Vector2? PointerTarget => target?.Foot;

	// No moot pictogram in the shared set (ExploreArt.Glyphs); the cairn's stones are the nearest.
	protected override string SiteGlyph => "cairn";
	// The whole ring is kept clear of decor and must stand on open ground.
	protected override float SiteFootprint => RingRadius + 90f;
	// He has come to the circle when its near stones are on screen.
	protected override float ArrivalRadius => RingRadius + 260f;
	// His, while it waits: cold, the light his places give off (.ai/side-events.md, the far tell).
	protected override Color TellColor => new Color(0.62f, 0.78f, 0.95f);

	// The ward's own light, when a rune burns: the light ramp (.ai/art-direction.md section 3) - the
	// one warm thing the event makes, because the ward was the wizards' and not his.
	private static readonly Color WardCore = new Color(1f, 1f, 1f);
	private static readonly Color WardHot = new Color(1f, 0.973f, 0.847f);  // #FFF8D8
	private static readonly Color WardMid = new Color(1f, 0.941f, 0.659f);  // #FFF0A8
	private static readonly Color WardEdge = new Color(1f, 0.776f, 0.235f); // #FFC63C

	// --- layout -------------------------------------------------------------------------------

	public override void PlaceSites(ExplorePlacer placer)
	{
		Vector2 centre = AddSite(placer.EventSite(SiteFootprint)).Position;
		// Which place is the ninth's socket, and where the ring starts: from the site, so the same
		// seed lays out the same circle.
		int seed = Mathf.Abs((int)(centre.X * 7f + centre.Y * 13f));
		int socket = seed % RingPlaces;
		float phase = (seed % 360) * Mathf.Pi / 180f;
		for (int i = 0; i < RingPlaces; i++)
		{
			float a = phase + i * Mathf.Tau / RingPlaces;
			Vector2 at = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * RingRadius;
			// A place that falls on a tarn or a fence is walked in or out along its spoke to open ground.
			at = OpenAlongSpoke(placer, centre, at);
			if (i == socket)
				socketAt = at;
			else
				stones.Add(new Stone { Foot = at, Place = i });
		}
	}

	private Vector2 OpenAlongSpoke(ExplorePlacer placer, Vector2 centre, Vector2 at)
	{
		Vector2 spoke = (at - centre).Normalized();
		foreach (float d in new[] { 0f, -60f, 60f, -120f, 120f })
		{
			Vector2 p = at + spoke * d;
			if (placer.IsClear(p, 50f))
				return p;
		}
		return at;
	}

	protected override void OnPlaced()
	{
		LoadArt();
		foreach (Stone stone in stones)
		{
			stone.Node = MakeStone(ToLocal(stone.Foot));
			AddChild(stone.Node);
		}
		if (socketTexture != null)
		{
			socketSprite = new Sprite2D { Texture = socketTexture, Position = ToLocal(socketAt), ZIndex = 0 };
			AddChild(socketSprite);
		}
	}

	private Texture2D socketTexture;

	private void LoadArt()
	{
		string text = Godot.FileAccess.GetFileAsString(Manifest);
		if (string.IsNullOrEmpty(text))
		{
			GD.PushError($"MootFiresEvent: no {Manifest}. Re-run: python tools/art/explore_moot.py, then import.");
			return;
		}
		using JsonDocument doc = JsonDocument.Parse(text);
		JsonElement s = doc.RootElement.GetProperty("stone");
		string path = s.GetProperty("path").GetString();
		stoneTexture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		frameW = s.GetProperty("frame_w").GetInt32();
		frameH = s.GetProperty("frame_h").GetInt32();
		groundRow = s.GetProperty("ground").GetInt32();
		anchorX = s.GetProperty("anchor_x").GetInt32();
		runeY = s.GetProperty("rune_y").GetInt32();
		string socketPath = doc.RootElement.GetProperty("socket").GetProperty("path").GetString();
		socketTexture = ResourceLoader.Exists(socketPath) ? GD.Load<Texture2D>(socketPath) : null;
		if (stoneTexture == null || socketTexture == null)
			GD.PushError("MootFiresEvent: the moot's stones are not imported; the circle will be light without stone. Run the Godot import.");
	}

	// A stone standing on groundLocal, sorted at its foot (WorldSprites.SortLift), with a trunk's
	// footprint on the ground's physics layer: the boots and the swarm meet it, nothing targets it.
	private Node2D MakeStone(Vector2 groundLocal)
	{
		var node = new Node2D
		{
			Position = new Vector2(Mathf.Round(groundLocal.X), Mathf.Round(groundLocal.Y - WorldSprites.SortLift)),
			// Under this node's -5, so the stone sorts with the actors at 0.
			ZIndex = 5,
		};
		if (stoneTexture != null)
		{
			node.AddChild(new Sprite2D
			{
				Name = "Stone",
				Texture = stoneTexture,
				Hframes = 2,
				Centered = false,
				Offset = new Vector2(-anchorX, WorldSprites.SortLift - groundRow),
			});
		}
		var body = new StaticBody2D
		{
			Position = new Vector2(0f, WorldSprites.SortLift),
			CollisionLayer = 1u << (LevelTilePainter.TerrainCollisionLayer - 1),
			CollisionMask = 0,
		};
		body.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = StoneFootRadius } });
		node.AddChild(body);
		return node;
	}

	// --- the run ------------------------------------------------------------------------------

	protected override void Begin()
	{
		foreach (Stone stone in stones)
			stone.Sprite = stone.Node?.GetNodeOrNull<Sprite2D>("Stone");
		count = 0;
		LightNext();
	}

	protected override void Tick(float delta)
	{
		if (restartIn >= 0f)
		{
			restartIn -= delta;
			if (restartIn < 0f)
				LightNext();
			return;
		}
		if (target == null)
			return;

		targetLeft -= delta;
		if (PlayerWithin(target.Foot, TouchRadius))
		{
			count++;
			target.Lit = true;
			if (count >= RunLength)
			{
				flareUntil = Clock + 3f;
				foreach (Stone stone in stones)
					stone.Lit = true;
				Vector2 at = target.Foot;
				target = null;
				ShowFrames();
				Succeed(at);
				return;
			}
			LightNext();
			return;
		}

		if (targetLeft > 0f)
			return;

		// Missed. Every rune goes dark, the count starts again, and the heath sends its people in.
		foreach (Stone stone in stones)
		{
			stone.Lit = false;
			stone.LitAt = -1f;
		}
		target = null;
		count = 0;
		restartIn = RestartPauseSeconds;
		ShowFrames();
		if (Player != null)
			SpawnRing(Player.GlobalPosition, WaveSize, WaveRadius);
	}

	// The next rune: never the stone just taken, never one beside it in the ring, never one already
	// lit in this run - so every step crosses the circle and six steps are six stones.
	private void LightNext()
	{
		restartIn = -1f;
		Stone from = target;
		var choices = new List<Stone>();
		foreach (Stone stone in stones)
		{
			if (stone == from || stone.Lit)
				continue;
			if (from != null && RingDistance(stone.Place, from.Place) < 2)
				continue;
			choices.Add(stone);
		}
		if (choices.Count == 0)
		{
			foreach (Stone stone in stones)
				if (stone != from && !stone.Lit)
					choices.Add(stone);
		}
		if (choices.Count == 0)
			return;
		target = choices[(int)(GD.Randi() % (uint)choices.Count)];
		target.LitAt = Clock;
		targetLeft = RuneSeconds;
		ShowFrames();
	}

	private int RingDistance(int a, int b)
	{
		int d = Mathf.Abs(a - b) % RingPlaces;
		return Mathf.Min(d, RingPlaces - d);
	}

	private void ShowFrames()
	{
		foreach (Stone stone in stones)
		{
			if (stone.Sprite != null && IsInstanceValid(stone.Sprite))
				stone.Sprite.Frame = stone.Lit || stone == target ? 1 : 0;
		}
	}

	protected override void OnStoodDown()
	{
		target = null;
		foreach (Stone stone in stones)
			stone.Lit = false;
		ShowFrames();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (IsResolved && !handedOver)
			StandOnTheHeath();
	}

	// The circle outlasts the event: the base frees this node a few seconds after it resolves, and the
	// stones and the socket go to the run so they keep standing where they are.
	private void StandOnTheHeath()
	{
		handedOver = true;
		if (Game == null || !IsInstanceValid(Game))
			return;
		foreach (Stone stone in stones)
		{
			if (stone.Node == null || !IsInstanceValid(stone.Node))
				continue;
			stone.Node.Reparent(Game, true);
			stone.Node.ZIndex = 0;
		}
		if (socketSprite != null && IsInstanceValid(socketSprite))
		{
			socketSprite.Reparent(Game, true);
			socketSprite.ZIndex = -20;
		}
	}

	// --- drawing ------------------------------------------------------------------------------

	protected override void DrawLight(CanvasItem light)
	{
		// While nobody has come: his cold far tell over the ring's centre, as every event gives off.
		if (IsDormant)
		{
			base.DrawLight(light);
			return;
		}

		bool flaring = Clock < flareUntil;
		foreach (Stone stone in stones)
		{
			if (!NearPlayer(stone.Foot))
				continue;
			bool isTarget = stone == target;
			if (!stone.Lit && !isTarget)
				continue;
			Vector2 ground = ToLocal(stone.Foot);
			Vector2 rune = ground + new Vector2(0f, runeY - groundRow);
			float breath = isTarget ? 0.85f + 0.15f * Mathf.Sin(Clock * 6f) : 0.6f;
			float flare = flaring ? 1.6f : 1f;
			ExploreArt.DrawGlow(light, rune, 110f * flare, new Color(WardEdge, 0.22f * breath));
			ExploreArt.DrawGlow(light, rune, 34f, new Color(WardMid, 0.6f * breath));
			ExploreArt.DrawGlow(light, ground, 90f, new Color(WardEdge, 0.12f * breath * flare));
			if (isTarget || flaring)
			{
				// The column over the stone to run for, seen from beyond the edge of the screen.
				ExploreArt.DrawShaft(light, rune, 16f, 460f, new Color(WardMid, 0.32f * breath));
				ExploreArt.DrawShaft(light, rune, 5f, 420f, new Color(WardHot, 0.42f * breath));
			}
		}
	}

	public override void _Draw()
	{
		if (IsResolved || target == null || !NearPlayer(target.Foot))
			return;
		// The rune's clock: the reach round the stone, draining as the rune gutters.
		float left = Mathf.Clamp(targetLeft / RuneSeconds, 0f, 1f);
		bool standing = PlayerWithin(target.Foot, TouchRadius);
		DrawProgressArc(target.Foot, TouchRadius, left, left > 0.33f ? WardMid : WardEdge, 3f, standing ? 0.42f : 0.24f);
	}
}
