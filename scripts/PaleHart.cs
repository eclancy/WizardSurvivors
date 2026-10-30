using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// F5, THE PALE HART - the Enchanted Forest's spell vault (.ai/achievements-and-secrets.md 8.1). It pays
// Hunter's Draw: the bow that hangs where the hart leads, learned by following the one thing in the wood
// that runs from him rather than at him.
//
// SEES. From two chunks out the moss carries hoofprints and the bracken is bent over, all of it pointing
// one way (its FarTell, FaceSite): toward the clearing's edge where a hart of warm light stands. It is the
// near tell - "the only warm thing that moves and is not you" - so the seen mark is taken from wherever
// the hart is (TellPosition).
//
// DOES. Follow, on SecretMover. When he comes within StartleRadius the hart bounds to the next stand,
// about 400 px on, and waits there facing him - six times, along a path laid from the seed (LayOut) - and
// at the last it stands under an old hunter's high seat where a bow of light hangs. Touch the seat (walk
// to its foot) and the bow's light leaves it and comes to him. Wander LeashRadius off while it is part-way
// along and it goes back to its start: a chase given up is a chase to begin again, never a lock (stage-
// secrets rule 4). About fifteen seconds of following, inside the vault's twenty.
//
// PAYS. Opened (SecretVault.Open): the first time ever Hunter's Draw, granted and saved at once, offered
// from the next level-up; in a later run the same walk pays a chest from the secret budget. The hart lowers
// its head and goes out, pixel by pixel.
//
// Nothing here is solid and nothing is a target: the hart and the seat are never in "enemies", and the
// seat is a sprite sorted among the bodies by its feet (the node y-sorts its children with the arena), so
// he walks round and under it. Its state lives in the run state: the hart's stand in Key + "/hart"
// (SecretMover), opened in the vault's own (State.Done).
//
// Art: tools/art/secret_forest.py (the hart's ten frames, the seat with and without its bow, the prints and
// the bracken). Drawn on the world's 2-unit grid, in opaque stops, fading only by dither (the secrets'
// pixel-art pass).
public partial class PaleHart : SecretVault
{
	/// <summary>How many times it bounds on (8.1: six), and how far each bound is (8.1: about 400 px).</summary>
	public const int Stands = 6;
	public const float BoundMin = 380f;
	public const float BoundMax = 440f;

	/// <summary>He comes this near and it goes on (8.1: 250).</summary>
	public const float StartleRadius = 250f;

	/// <summary>He wanders this far off it, part-way along, and it goes home (8.1: 1500).</summary>
	public const float LeashRadius = 1500f;

	/// <summary>Walking this near the seat's foot, once the hart stands under it, is touching it.</summary>
	public const float SeatReach = 90f;

	/// <summary>The open ground each stand is laid on: the seat's sprite is 88 px wide, its legs splay.</summary>
	public const float StandFootprint = 120f;

	private const string HartPath = "res://assets/bonelight/world/forest/pale-hart.png";
	private const string SeatPath = "res://assets/bonelight/world/forest/high-seat.png";

	// The strips' geometry (secret_forest.py at x2). The hart: ten 96 x 96 frames (five poses facing right,
	// the same five facing left), its contact line at y 92 under its middle. The seat: two 88 x 136 frames
	// (with the bow, without), its feet's middle at (44, 130), the bow's grip at (70, 60).
	private static readonly Vector2 HartCell = new(96f, 96f);
	private static readonly Vector2 HartAnchor = new(-48f, -92f);
	private static readonly Vector2 SeatCell = new(88f, 136f);
	private static readonly Vector2 SeatAnchor = new(-44f, -130f);
	private static readonly Vector2 BowOffset = new(26f, -70f);

	/// <summary>The far tell (8.1): hoofprints in the moss and bracken bent one way, from two chunks out.</summary>
	private static readonly SecretFarTell Tell = new()
	{
		Textures = new[]
		{
			"res://assets/bonelight/world/forest/bracken-bent-1.png",
			"res://assets/bonelight/world/forest/bracken-bent-2.png",
			"res://assets/bonelight/world/forest/hoofprints-1.png",
		},
		// Within a chunk the prints are fresh, and most of what is there is prints.
		NearTextures = new[]
		{
			"res://assets/bonelight/world/forest/hoofprints-1.png",
			"res://assets/bonelight/world/forest/hoofprints-2.png",
		},
		InnerRadius = SecretCatalog.VaultFootprint,
		NearRadius = 1536f,
		OuterRadius = 3072f,
		PeakPerChunk = 46,
		NearShare = 0.7f,
		FootFromBottom = 4f,
		FaceSite = true,
	};

	public override SecretFarTell FarTell => Tell;

	/// <summary>The near tell is the hart itself, wherever it has got to.</summary>
	public override Vector2 TellPosition => Hart != null && IsInstanceValid(Hart) ? Hart.GlobalPosition : GlobalPosition;

	/// <summary>The hart, a SecretMover. Public for the probe.</summary>
	public HartFigure Hart { get; private set; }

	/// <summary>The stands, home first, the seat last. Laid from the seed in LayOut.</summary>
	public IReadOnlyList<Vector2> Path => path;

	/// <summary>Where the high seat stands: the path's last stand.</summary>
	public Vector2 Seat => path.Count > 0 ? path[path.Count - 1] : GlobalPosition;

	/// <summary>True once he has touched the seat and the bow's light has left it.</summary>
	public bool BowTaken => opening >= 0f || IsOpened;

	private List<Vector2> path = new();
	private Sprite2D seat;
	private Node2D effects;
	private float clock;
	private float opening = -1f;       // seconds since he touched the seat, while the light travels

	private const float RiseSeconds = 0.45f;
	private const float FlySeconds = 0.9f;

	public override void LayOut(ExplorePlacer placer)
	{
		// Every stand on open ground, each a bound from the last and clear of the ones before, so the hart
		// never doubles back onto a stand it has left; all of it within the vault's own reach.
		path = SecretMover.LayPath(placer, Position, Stands, BoundMin, BoundMax, StandFootprint, 300f, 4000f);
	}

	public override void _Ready()
	{
		// The seat sorts among the bodies by its feet: the node y-sorts its children with the arena's.
		YSortEnabled = true;
		Texture2D hartSheet = ResourceLoader.Exists(HartPath) ? GD.Load<Texture2D>(HartPath) : null;
		Texture2D seatSheet = ResourceLoader.Exists(SeatPath) ? GD.Load<Texture2D>(SeatPath) : null;
		if (hartSheet == null || seatSheet == null)
			GD.PushError("PaleHart: its art is missing; re-run python tools/art/secret_forest.py, then import.");
		if (path.Count == 0)
			path = new List<Vector2> { GlobalPosition };

		seat = new Sprite2D
		{
			Name = "HighSeat",
			Texture = seatSheet,
			Hframes = 2,
			Frame = BowTaken ? 1 : 0,
			Centered = false,
			Offset = SeatAnchor,
		};
		AddChild(seat);
		seat.GlobalPosition = SpellInk.Snap(Seat);

		Hart = new HartFigure
		{
			Name = "Hart",
			Director = Director,
			Vault = this,
			State = Director.StateOf(Key + "/hart"),
			Path = path,
			StartleRadius = StartleRadius,
			LeashRadius = LeashRadius,
			HopSeconds = 0.7f,
			HopHeight = 44f,
			DrawSelf = false,
			Sheet = hartSheet,
		};
		AddChild(Hart);

		effects = new Node2D
		{
			Name = "Light",
			ZIndex = DrawLayers.Air,
			ZAsRelative = false,
			TopLevel = true,
			Material = SpellInk.DitherByCell,
		};
		effects.Draw += DrawEffects;
		AddChild(effects);
		effects.GlobalPosition = Vector2.Zero;
	}

	public override void _ExitTree()
	{
		if (effects != null && IsInstanceValid(effects))
			effects.Draw -= DrawEffects;
		base._ExitTree();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		Player player = Player;

		if (opening >= 0f)
		{
			opening += dt;
			if (opening >= RiseSeconds + FlySeconds)
			{
				opening = -1f;
				// Rewards in flight are paid even if the boss has come meanwhile (stage-secrets section 4).
				Open(Seat + new Vector2(0f, 110f));
			}
		}
		else if (!IsOpened && Active && player != null && Hart != null && Hart.IsSettled
			&& player.GlobalPosition.DistanceTo(Seat) <= SeatReach)
		{
			TakeBow();
		}

		if (player != null && (player.GlobalPosition.DistanceSquaredTo(GlobalPosition) < 4000f * 4000f))
			effects?.QueueRedraw();
	}

	// He has touched the seat: the bow's light leaves it, rises and goes to him; the hart goes out.
	private void TakeBow()
	{
		opening = 0f;
		if (seat != null)
			seat.Frame = 1;
		SfxPlayer.AtPosition(SfxCatalog.EventReward, Seat, 0f);
	}

	/// <summary>Touches the seat now, as if he had walked to it with the hart there: for _UiShot's photographs.</summary>
	public void TakeBowForProbe()
	{
		if (Hart != null)
			Hart.State.Step = Path.Count - 1;
		if (!BowTaken)
			TakeBow();
	}

	private Vector2 BowWorld => SpellInk.Snap(Seat) + BowOffset;

	// World units: the canvas is TopLevel at the origin, through the dither material.
	private void DrawEffects()
	{
		// The bow's glint: dim while the hart is still leading, full once it stands under the seat.
		if (!BowTaken)
			DrawGlint(effects, BowWorld, clock, Hart != null && Hart.IsSettled ? 1f : 0.45f);

		if (opening >= 0f && Player is Player player)
		{
			Vector2 at;
			if (opening < RiseSeconds)
				at = BowWorld + new Vector2(0f, -30f * (opening / RiseSeconds));
			else
			{
				float t = Mathf.Clamp((opening - RiseSeconds) / FlySeconds, 0f, 1f);
				float eased = t * t * (3f - 2f * t);
				Vector2 start = BowWorld + new Vector2(0f, -30f);
				at = start.Lerp(player.GlobalPosition + new Vector2(0f, -40f), eased) + new Vector2(0f, -50f * MathF.Sin(t * MathF.PI));
			}
			DrawLight(effects, at, 12f);
			DrawGlint(effects, at, 0.6f, 1f);
		}
	}

	/// <summary>
	/// The hart: a SecretMover that draws itself from the strip - standing and breathing while it waits,
	/// gathering, stretched and landing through a bound - facing him or the way it goes, its shadow on the
	/// ground under it so a bound reads as a bound. Drawn through the dither material on the world's grid,
	/// so it goes out (once the bow is taken) by losing whole art pixels, never by turning translucent.
	/// </summary>
	public partial class HartFigure : SecretMover
	{
		public PaleHart Vault { get; set; }
		public Texture2D Sheet { get; set; }

		private const int Poses = 5;
		private float idle;
		private float gone;   // 0 while it is there; to 1 as it goes out after the bow is taken

		private const float GoneSeconds = 1.2f;

		public override void _Ready()
		{
			base._Ready();
			Material = SpellInk.DitherByCell;
			if (Vault != null && Vault.IsOpened)
				gone = 1f;
		}

		public override void _Process(double delta)
		{
			base._Process(delta);
			float dt = (float)delta;
			idle += dt;
			if (Vault != null && Vault.BowTaken && gone < 1f)
				gone = MathF.Min(1f, gone + dt / GoneSeconds);
			if (Director?.Player is Player p && p.GlobalPosition.DistanceSquaredTo(GlobalPosition) < 1600f * 1600f)
				QueueRedraw();
		}

		public override void _Draw()
		{
			if (Sheet == null || gone >= 1f)
				return;
			float lift = HopT < 1f ? HopHeight * MathF.Sin(HopT * MathF.PI) : 0f;

			// Its shadow on the ground under it: a flat dithered ellipse of occlusion, smaller the higher
			// it is, on the world's grid.
			float coverage = 0.55f * (1f - gone) * (1f - 0.4f * lift / MathF.Max(1f, HopHeight));
			int half = lift > HopHeight * 0.5f ? 9 : 12;
			for (int row = -2; row <= 1; row++)
			{
				int w = row == -2 || row == 1 ? half - 4 : half;
				Vector2 corner = PixelCells.SnapPoint(this, new Vector2(-w * 2f, lift + row * 2f));
				DrawRect(new Rect2(corner, new Vector2(w * 4f, 2f)), new Color(0.02f, 0.027f, 0.047f, coverage));
			}

			int pose;
			if (HopT < 1f)
				pose = HopT < 0.22f ? 2 : HopT < 0.78f ? 3 : 4;
			else
				pose = ((int)(idle / (SecretSite.GlintPeriod * 0.5f))) % 2;   // a slow breath, never a flicker
			int frame = pose + (Facing.X < 0f ? Poses : 0);
			Vector2 at = PixelCells.SnapPoint(this, HartAnchor);
			DrawTextureRectRegion(Sheet, new Rect2(at, HartCell),
				new Rect2(new Vector2(frame * HartCell.X, 0f), HartCell), new Color(1f, 1f, 1f, 1f - gone));
		}
	}
}
