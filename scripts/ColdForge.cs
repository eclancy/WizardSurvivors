using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// D5, THE COLD FORGE - the Cursed Dungeon's spell vault (.ai/achievements-and-secrets.md 8.5). It pays
// Molten Shard: the forge that made the shackles, lit again by what dies on its hearth.
//
// SEES. From two chunks out, dropped shackles and soot on the gaol's floor, and nearer, clinker from the
// forge (its FarTell, the clinker only near). Then, in a hall room, a dead smithy: a cold hearth under a
// sooted hood, an anvil in front of it with a warm glint on its horn, and in front of the anvil a ring of
// hearth bricks laid in the floor. The glint is the near tell.
//
// DOES. Cull. Every enemy the player kills standing inside the ring (RingRadius from the vault's node, the
// ring's own middle - the ring is drawn from the same number) sets an ember in the dead coals, one art
// pixel of fire in the coal bed, in a fixed order. KillsNeeded of them and the forge roars. It asks him to
// fight on the hearth and let the swarm come to him there - the ring is wide enough that it does, inside
// the vault's twenty seconds once the swarm is thick. Kills anywhere else count for nothing.
//
// PAYS. The forge roars, and the molten light leaves the anvil's horn and comes to him: the vault opens
// (SecretVault.Open) - the first time ever Molten Shard, granted and saved at once, offered from the next
// level-up; in a later run the same kills pay a chest from the secret budget.
//
// IN A HALL ROOM. The Dungeon is the maze; a site there snaps to a corridor cell's centre (ExplorePlacer),
// and a 360 px ring and the forge behind it do not fit in a corridor. So LayOut moves the vault to the
// nearest hall - four corridor blocks joined where the maze knocked out the post between them, 672 px
// square - searching outward from the cell the placer chose, and reserves it. A pure function of the seed:
// the maze is. Off the maze (never, today) it stays where it was put.
//
// The forge's hearth is solid (a body on the terrain layer, its cells stamped into the navigation flags);
// the ring and the anvil are walked over. Nothing here is in "enemies". Progress lives in the run state:
// State.Step the embers lit, State.Done opened.
//
// Art: tools/art/secret_dungeon.py (the forge cold and roaring, the anvil, the ring, the far tell). On the
// world's 2-unit grid; the embers, the glint and the light in opaque stops, fading only by dither.
public partial class ColdForge : SecretVault
{
	/// <summary>Kills on the hearth that light it (8.5: 40).</summary>
	public const int KillsNeeded = 40;

	/// <summary>The hearth ring's radius: a kill whose body fell within this of the node counts.</summary>
	public const float RingRadius = 180f;

	/// <summary>Where the forge's foot and the anvil's stand, from the ring's middle.</summary>
	public static readonly Vector2 ForgeFoot = new(0f, -270f);
	public static readonly Vector2 AnvilFoot = new(-56f, -205f);

	/// <summary>The ring's middle from the hall's: a little south, so the forge stands against the north wall.</summary>
	private static readonly Vector2 HallToRing = new(0f, 30f);

	/// <summary>How far round the placed cell the hall search goes, in maze cells.</summary>
	private const int HallSearchCells = 8;

	/// <summary>The open ground a hall must have round its middle to hold the forge and its ring.</summary>
	private const float HallClearance = 250f;

	private const string ForgePath = "res://assets/bonelight/world/dungeon/cold-forge.png";
	private const string AnvilPath = "res://assets/bonelight/world/dungeon/cold-forge-anvil.png";
	private const string RingPath = "res://assets/bonelight/world/dungeon/hearth-ring.png";

	// The strips (secret_dungeon.py at x2). The forge: three 144 x 152 frames (cold, roaring, roaring), its
	// foot's middle at (72, 148). The anvil: 60 x 44, its foot at (30, 40), its horn's tip at (-24, -28) from
	// it. The ring: 376 x 376 with its middle on the node.
	private static readonly Vector2 ForgeCell = new(144f, 152f);
	private static readonly Vector2 ForgeAnchor = new(-72f, -148f);
	private static readonly Vector2 AnvilAnchor = new(-30f, -40f);
	private static readonly Vector2 Horn = new(-24f, -28f);
	private static readonly Vector2 RingAnchor = new(-188f, -188f);

	/// <summary>The hearth's body, from the forge's foot.</summary>
	private static readonly Rect2 Hearth = new(-50f, -44f, 100f, 44f);

	// The forty places an ember is lit, in the order they light, in the forge's art pixels - the generator's
	// ember_spots(), printed by it; the foot is art (36, 74).
	private static readonly (int X, int Y)[] EmberSpots =
	{
		(40, 47), (50, 42), (48, 47), (22, 45), (47, 47), (27, 42), (49, 42), (34, 46), (36, 42), (30, 46),
		(27, 46), (55, 46), (39, 43), (27, 47), (53, 47), (51, 44), (39, 42), (22, 42), (40, 44), (52, 43),
		(41, 44), (33, 42), (40, 43), (44, 45), (35, 47), (49, 45), (27, 43), (41, 42), (46, 44), (28, 42),
		(47, 46), (50, 43), (21, 46), (52, 45), (54, 43), (41, 43), (45, 46), (50, 47), (51, 42), (29, 45),
	};

	// The Fire ramp's edge, mid and hot: an ember new-lit, settling, and the few that burn hottest.
	private static readonly Color FireEdge = new(0.722f, 0.220f, 0.102f);
	private static readonly Color FireMid = new(0.910f, 0.478f, 0.102f);
	private static readonly Color FireHot = new(1.0f, 0.784f, 0.282f);

	private static readonly SecretFarTell Tell = new()
	{
		Textures = new[]
		{
			"res://assets/bonelight/world/dungeon/shackles-1.png",
			"res://assets/bonelight/world/dungeon/shackles-2.png",
			"res://assets/bonelight/world/dungeon/soot-1.png",
			"res://assets/bonelight/world/dungeon/soot-2.png",
		},
		NearTextures = new[]
		{
			"res://assets/bonelight/world/dungeon/clinker-1.png",
			"res://assets/bonelight/world/dungeon/clinker-2.png",
		},
		InnerRadius = SecretCatalog.VaultFootprint,
		NearRadius = 1536f,
		OuterRadius = 3072f,
		PeakPerChunk = 40,
		NearShare = 0.6f,
		FootFromBottom = 4f,
	};

	public override SecretFarTell FarTell => Tell;

	public override IEnumerable<(Vector2 Centre, float Radius)> SolidFootprints => new[] { (Position + ForgeFoot + new Vector2(0f, -22f), 56f) };

	/// <summary>
	/// Seen once the smithy is: the middle of it, between the ring's far edge and the anvil, so the hearth,
	/// the anvil and its glint are on screen together when the mark is taken (the glint itself is the near tell).
	/// </summary>
	public override Vector2 TellPosition => GlobalPosition + new Vector2(0f, -100f);

	/// <summary>Embers lit, toward KillsNeeded.</summary>
	public int Embers => State?.Step ?? 0;

	/// <summary>True once the forge has roared this run.</summary>
	public bool Roared => roaring >= 0f || IsOpened || Embers >= KillsNeeded;

	/// <summary>True if LayOut found a hall room for it (for the probe and --explore-log).</summary>
	public bool InHall { get; private set; }

	private Node2D forge;
	private Sprite2D anvil;
	private Node2D ring;
	private Node2D effects;
	private Texture2D forgeSheet;
	private float clock;
	private float roaring = -1f;
	private bool listening;

	private const float RoarSeconds = 0.9f;
	private const float FlySeconds = 0.9f;

	public override void LayOut(ExplorePlacer placer)
	{
		Node2DGame game = Director?.Game;
		if (game == null || MazeNavigation.Active is not { IsMaze: true })
			return;
		// The nearest hall to the cell the placer chose: the middle of a removed post, half a cell
		// diagonally from a cell's centre, open and with room round it. Cells searched in rings outward,
		// each ring in a fixed order, so the same seed always finds the same hall.
		float pitch = LevelGenerator.MazeCellTiles * game.ProceduralTilePixels;
		float half = pitch * 0.5f;
		Vector2 start = placer.Snap(Position);
		for (int r = 0; r <= HallSearchCells; r++)
		{
			for (int j = -r; j <= r; j++)
			{
				for (int i = -r; i <= r; i++)
				{
					if (Math.Max(Math.Abs(i), Math.Abs(j)) != r)
						continue;
					Vector2 cell = start + new Vector2(i, j) * pitch;
					for (int k = 0; k < 4; k++)
					{
						Vector2 post = cell + new Vector2(k % 2 == 0 ? -half : half, k < 2 ? -half : half);
						if (game.Terrain == null || !IsHall(game, post) || !game.IsExploreGroundClear(post, HallClearance))
							continue;
						Position = post + HallToRing;
						InHall = true;
						placer.Reserve(Position, SecretCatalog.VaultFootprint);
						return;
					}
				}
			}
		}
	}

	// A hall: no wall anywhere on two circles round the post, at 290 and 200 px - a 672 px room's inside
	// holds both, and a crossing of corridors whose post still stands (or a single corridor) fails on the
	// post's own corners, which lie on them. Asked of the chunk data, which is made for far ground on demand.
	private static bool IsHall(Node2DGame game, Vector2 post)
	{
		if (game.Terrain.IsWallAtWorld(post))
			return false;
		foreach (float r in new[] { 290f, 200f })
			for (int k = 0; k < 24; k++)
				if (game.Terrain.IsWallAtWorld(post + Vector2.FromAngle(k * Mathf.Tau / 24f) * r))
					return false;
		return true;
	}

	public override void _Ready()
	{
		// The forge and the anvil sort among the bodies by their feet: the node y-sorts its children with the arena's.
		YSortEnabled = true;
		forgeSheet = ResourceLoader.Exists(ForgePath) ? GD.Load<Texture2D>(ForgePath) : null;
		Texture2D anvilTexture = ResourceLoader.Exists(AnvilPath) ? GD.Load<Texture2D>(AnvilPath) : null;
		Texture2D ringTexture = ResourceLoader.Exists(RingPath) ? GD.Load<Texture2D>(RingPath) : null;
		if (forgeSheet == null || anvilTexture == null || ringTexture == null)
			GD.PushError("ColdForge: its art is missing; re-run python tools/art/secret_dungeon.py, then import.");
		if (!InHall)
			GD.Print($"ColdForge: no hall room within {HallSearchCells} cells; it stands in a corridor at {GlobalPosition}.");

		// The ring lies in the floor under everything.
		ring = new Node2D { Name = "HearthRing", ZIndex = DrawLayers.Ground, ZAsRelative = false };
		ring.Draw += () =>
		{
			if (ringTexture == null)
				return;
			SpellInk.AlignToWorldGrid(ring);
			ring.DrawTexture(ringTexture, RingAnchor);
		};
		AddChild(ring);

		forge = new Node2D { Name = "Forge", Position = ForgeFoot };
		forge.Draw += DrawForge;
		AddChild(forge);
		var body = new StaticBody2D
		{
			Name = "Hearth",
			CollisionLayer = 1u << (LevelTilePainter.TerrainCollisionLayer - 1),
			CollisionMask = 0,
		};
		body.AddChild(new CollisionShape2D { Position = Hearth.GetCenter(), Shape = new RectangleShape2D { Size = Hearth.Size } });
		forge.AddChild(body);

		anvil = new Sprite2D { Name = "Anvil", Texture = anvilTexture, Centered = false, Offset = AnvilAnchor };
		AddChild(anvil);
		anvil.GlobalPosition = SpellInk.Snap(GlobalPosition + AnvilFoot);

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
		Listen(false);
		if (effects != null && IsInstanceValid(effects))
			effects.Draw -= DrawEffects;
		if (forge != null && IsInstanceValid(forge))
			forge.Draw -= DrawForge;
		base._ExitTree();
	}

	private void Listen(bool on)
	{
		if (on == listening)
			return;
		if (on)
			global::Player.EnemyKilled += OnEnemyKilled;
		else
			global::Player.EnemyKilled -= OnEnemyKilled;
		listening = on;
	}

	// A kill: if the body fell on the hearth, an ember. Only while the vault is still to light.
	private void OnEnemyKilled(Enemy enemy, Vector2 at, SpellData source)
	{
		SecretDirector.SiteState state = State;
		if (state == null || state.Done || roaring >= 0f || !Active || state.Step >= KillsNeeded)
			return;
		if (at.DistanceSquaredTo(GlobalPosition) > RingRadius * RingRadius)
			return;
		state.Step++;
		forge?.QueueRedraw();
		if (state.Step >= KillsNeeded)
			Roar();
	}

	private void Roar()
	{
		roaring = 0f;
		SfxPlayer.AtPosition(SfxCatalog.EventReward, GlobalPosition + ForgeFoot, 0f);
	}

	/// <summary>Lights the forge with <paramref name="embers"/> embers, as if that many had died on the hearth: for _UiShot's photographs.</summary>
	public void PoseForProbe(int embers)
	{
		if (State == null)
			return;
		State.Step = Math.Clamp(embers, 0, KillsNeeded);
		if (State.Step >= KillsNeeded && roaring < 0f && !IsOpened)
			Roar();
		forge?.QueueRedraw();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		Player player = Player;
		// Listen only while he is near enough to be killing on the hearth: a forge across the map costs nothing a kill.
		Listen(player != null && !IsOpened && roaring < 0f && player.GlobalPosition.DistanceSquaredTo(GlobalPosition) < 1400f * 1400f);

		if (roaring >= 0f)
		{
			roaring += dt;
			if (roaring >= RoarSeconds + FlySeconds)
			{
				roaring = -1f;
				// Rewards in flight are paid even if the boss has come meanwhile (stage-secrets section 4).
				Open(GlobalPosition + new Vector2(0f, 110f));
			}
		}
		if (player != null && player.GlobalPosition.DistanceSquaredTo(GlobalPosition) < 1600f * 1600f)
		{
			effects?.QueueRedraw();
			if (Roared)
				forge?.QueueRedraw();
		}
	}

	// The forge on the world's grid: cold, its embers lit one a kill; roaring once they are all in.
	private void DrawForge()
	{
		if (forgeSheet == null)
			return;
		SpellInk.AlignToWorldGrid(forge);
		int frame = Roared ? 1 + ((int)(clock * 8f)) % 2 : 0;
		forge.DrawTextureRectRegion(forgeSheet, new Rect2(ForgeAnchor, ForgeCell), new Rect2(new Vector2(frame * ForgeCell.X, 0f), ForgeCell));
		if (frame == 0)
		{
			int lit = Math.Min(Embers, EmberSpots.Length);
			for (int i = 0; i < lit; i++)
			{
				var (x, y) = EmberSpots[i];
				// Newest a dull edge, settling to the ramp's mid; one in five burns hot.
				Color c = i >= lit - 2 ? FireEdge : i % 5 == 0 ? FireHot : FireMid;
				forge.DrawRect(new Rect2((x - 36) * 2f, (y - 74) * 2f, 2f, 2f), c);
			}
		}
		forge.DrawSetTransform(Vector2.Zero);
	}

	// World units: TopLevel at the origin, through the dither material.
	private void DrawEffects()
	{
		Vector2 horn = SpellInk.Snap(GlobalPosition + AnvilFoot) + Horn;
		if (!Roared && !IsOpened)
		{
			// The glint on the horn, brightening as the coals fill.
			DrawGlint(effects, horn, clock, 0.6f + 0.4f * Embers / (float)KillsNeeded);
			return;
		}
		if (roaring < 0f || Player is not Player player)
			return;
		Vector2 at;
		if (roaring < RoarSeconds)
			at = horn + new Vector2(0f, -30f * (roaring / RoarSeconds));
		else
		{
			float t = Mathf.Clamp((roaring - RoarSeconds) / FlySeconds, 0f, 1f);
			float eased = t * t * (3f - 2f * t);
			at = (horn + new Vector2(0f, -30f)).Lerp(player.GlobalPosition + new Vector2(0f, -40f), eased)
				+ new Vector2(0f, -50f * MathF.Sin(t * MathF.PI));
		}
		DrawLight(effects, at, 12f);
		DrawGlint(effects, at, 0.6f, 1f);
	}
}
