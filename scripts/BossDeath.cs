using Godot;
using System;
using WizardSurvivors.scripts;

// A BOSS'S FALL: the presentation that plays on the corpse after Enemy.StartDeath has already paid
// out, left the "enemies" group and zeroed its collision on the frame the last hit landed. Nothing
// here is gameplay. It cannot hurt the player, it never rejoins the group, it drops nothing - it
// only shows what the thing was, coming apart (2026-10-28: "bosses should have cool death animations
// that relate to what they are").
//
// THIS IS THE STAGE, NOT THE PLAY. Each boss writes its own fall in its own class (BossEnemy's death
// hooks, OnDeathBegin / TickDeath / DrawDeath*), and this node gives every one of them the same four
// things, so no boss has to build any of them:
//
//   PIECES of the frame the boss died on: the body cut into rectangles of its own sprite, each with a
//   pivot, which can be moved, turned, squashed, darkened, sunk into the ground (rows clipped below
//   the ground line) or dissolved from the top. A tree splits because its two halves are two pieces.
//   They are the boss's own pixels, so a regenerated sheet is picked up with no change here.
//
//   DEBRIS from one generated atlas (tools/art/boss_deaths.py): leaves, keys, chain links, pages, ice
//   shards, straw. Thrown in a ground plane with a height above it, so a key arcs, lands where its
//   shadow was, bounces and skids. Drawn at x2 on the art grid, turned only in quarter turns and
//   mirrored, never rotated off the grid: debris is art, and art keeps its pixels.
//
//   PUFFS - dust, sand, steam, smoke - as discs on the 2 px art grid that swell and fade.
//
//   THREE LAYERS: the ground (absolute z -15, under every actor, with the stains and the craters -
//   CraterGround's rule), the air (over the body), and light (additive, for every glow, spark and
//   mote: a light source adds, it does not paint).
//
// COST. One boss dies at a time, and a Boss Rush fells ten in a run but never two at once. Every
// array here is allocated when the fall begins and nothing after it; the bits are a fixed pool of
// structs integrated in place. No Tween, no scene, no per-frame string.
public partial class BossDeath : Node2D
{
	/// <summary>The debris atlas, written by tools/art/boss_deaths.py.</summary>
	public const string DebrisAtlasPath = "res://assets/bonelight/effects/boss-death-debris.png";

	/// <summary>One cell of the debris atlas, in art pixels.</summary>
	public const int DebrisCell = 16;
	private const int DebrisColumns = 8;

	/// <summary>One art pixel on the ground (art-direction section 1: everything renders at x2).</summary>
	public const float Px = 2f;

	private const int MaxBits = 260;
	private const int MaxPuffs = 48;

	// Shared by every fall: one texture and one additive material for the run.
	private static Texture2D debrisAtlas;
	private static CanvasItemMaterial additive;

	public BossEnemy Boss { get; private set; }
	private AnimatedSprite2D sprite;
	private Vector2 spriteRest;

	/// <summary>Seconds before this body's fall begins. The Hollow Choir's voices go out in turn.</summary>
	public float Delay { get; private set; }

	/// <summary>The fall's length, from its own start. The corpse is freed when it runs out.</summary>
	public float Seconds { get; private set; } = 2.6f;

	private float age;
	private float previousT = -1f;
	private bool begun;
	private bool freed;

	/// <summary>The fall's own clock: seconds since it began, negative while it waits its turn.</summary>
	public float T => age - Delay;

	// The frame the boss fell on.
	private Texture2D sheet;
	private Rect2 frameRegion;
	private Vector2 frameOrigin;

	/// <summary>The frame's size in art pixels - 96 x 96 for every boss cell.</summary>
	public Vector2 FrameSize { get; private set; } = new(96f, 96f);

	/// <summary>Local pixels per art pixel of the body: the boss sprite's own scale.</summary>
	public Vector2 ArtScale { get; private set; } = new(2f, 2f);

	/// <summary>True when the body faced the other way: every art-space x is mirrored for it.</summary>
	public bool Mirrored { get; private set; }

	/// <summary>The body's base tint (its scene modulate and any enrage), which its own debris wears too.</summary>
	public Color Tint { get; private set; } = Colors.White;

	/// <summary>False when it fell while under the ground (a burrower poisoned out of reach): no body to cut.</summary>
	public bool BodyWasVisible { get; private set; } = true;

	/// <summary>
	/// +1 when the player stood to the left of it, so "away from him" is to the right - in ART space,
	/// already mirrored for <see cref="Mirrored"/>, so a choreography can use it straight.
	/// </summary>
	public float ArtAway { get; private set; } = 1f;

	/// <summary>The contact line under the body: art row cell-2 (art-direction section 5), in local space.</summary>
	public Vector2 Foot => ArtToLocal(new Vector2(FrameSize.X * 0.5f, FrameSize.Y - 1f));

	/// <summary>Art row of the feet in every boss frame.</summary>
	public float GroundRow => FrameSize.Y - 2f;

	private Node2D ground;
	private Node2D bodyLayer;
	private Node2D air;
	private Node2D light;
	private CanvasItem eliteMarker;
	private Color eliteMarkerRest = Colors.White;

	private readonly Bit[] bits = new Bit[MaxBits];
	private int bitCount;
	private readonly Puff[] puffs = new Puff[MaxPuffs];
	private int puffCount;
	private Piece[] pieces = new Piece[8];
	private int pieceCount;
	private readonly RandomNumberGenerator rng = new();

	/// <summary>Called by BossEnemy.BeginOwnDeath, before the node is added under the corpse.</summary>
	public void Configure(BossEnemy boss, AnimatedSprite2D bodySprite, Vector2 bodyRest, float delay, float seconds, Vector2? witness)
	{
		Boss = boss;
		sprite = bodySprite;
		spriteRest = bodyRest;
		Delay = Mathf.Max(0f, delay);
		Seconds = Mathf.Max(0.2f, seconds);
		// Seeded from the boss's id, so the same fall throws its debris the same way every time it is
		// photographed - and differently from every other boss's. FNV-1a by hand: string.GetHashCode
		// is randomised per process in .NET.
		uint seed = 2166136261u;
		foreach (char c in boss?.BossId ?? "boss")
			seed = (seed ^ c) * 16777619u;
		rng.Seed = seed;
		float away = 1f;
		if (witness is Vector2 w && boss != null)
			away = boss.GlobalPosition.X >= w.X ? 1f : -1f;
		bool mirrored = bodySprite != null && bodySprite.FlipH;
		ArtAway = mirrored ? -away : away;
	}

	public override void _Ready()
	{
		Name = "BossDeath";
		debrisAtlas ??= ResourceLoader.Exists(DebrisAtlasPath) ? GD.Load<Texture2D>(DebrisAtlasPath) : null;
		if (debrisAtlas == null)
			GD.PushError($"BossDeath: no debris atlas at '{DebrisAtlasPath}' - re-run tools/art/boss_deaths.py and import. The falls will throw nothing.");
		if (additive == null || !IsInstanceValid(additive))
			additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

		// Ground first, at absolute -15 like a crater: under every actor whatever the arena's y-sort
		// says. Then the body's pieces, then the air over them, then the light over everything.
		ground = new Node2D { Name = "Ground", ZIndex = CraterGround.GroundZ, ZAsRelative = false };
		bodyLayer = new Node2D { Name = "Body" };
		air = new Node2D { Name = "Air" };
		light = new Node2D { Name = "Light", Material = additive };
		AddChild(ground);
		AddChild(bodyLayer);
		AddChild(air);
		AddChild(light);
		ground.Draw += DrawGround;
		air.Draw += DrawAir;
		light.Draw += DrawLight;

		eliteMarker = Boss?.GetNodeOrNull<CanvasItem>("EliteMarker");
		if (eliteMarker != null)
			eliteMarkerRest = eliteMarker.Modulate;
	}

	public override void _ExitTree()
	{
		if (ground != null && IsInstanceValid(ground))
			ground.Draw -= DrawGround;
		if (air != null && IsInstanceValid(air))
			air.Draw -= DrawAir;
		if (light != null && IsInstanceValid(light))
			light.Draw -= DrawLight;
	}

	public override void _Process(double delta)
	{
		if (freed || Boss == null || !IsInstanceValid(Boss))
			return;

		float dt = (float)delta;
		previousT = T;
		age += dt;
		if (T < 0f)
			return;

		if (!begun)
		{
			begun = true;
			previousT = 0f;
			CaptureFrame();
			Boss.OnDeathBegin(this);
		}

		float t = T;
		Boss.TickDeath(this, t, dt);
		StepBits(t, dt);
		for (int i = 0; i < pieceCount; i++)
			pieces[i].Apply(this);

		// The gold ring under an elite says "this one is worth chasing". A corpse is not.
		if (eliteMarker != null && IsInstanceValid(eliteMarker))
			eliteMarker.Modulate = new Color(eliteMarkerRest, eliteMarkerRest.A * (1f - Span(t, 0f, 0.3f)));

		ground.QueueRedraw();
		air.QueueRedraw();
		light.QueueRedraw();
		if (Boss.RedrawsWhileDying)
			Boss.QueueRedraw();

		if (t >= Seconds)
		{
			freed = true;
			Boss.QueueFree();
		}
	}

	// The frame it fell on: the walk's current frame rather than whatever one-shot was playing. The
	// killing hit has just started "hurt", which knocks the figure two columns over and shears its
	// top - and every piece rectangle a boss cuts is measured on the walk.
	private void CaptureFrame()
	{
		if (sprite == null || !IsInstanceValid(sprite) || sprite.SpriteFrames == null)
		{
			BodyWasVisible = false;
			return;
		}

		SpriteFrames frames = sprite.SpriteFrames;
		StringName anim = frames.HasAnimation(MovingAnimation) ? MovingAnimation : sprite.Animation;
		int frame = sprite.Animation == anim ? sprite.Frame : 0;
		frame = Mathf.Clamp(frame, 0, Mathf.Max(0, frames.GetFrameCount(anim) - 1));
		Texture2D texture = frames.GetFrameTexture(anim, frame);
		if (texture is AtlasTexture atlas && atlas.Atlas != null)
		{
			sheet = atlas.Atlas;
			frameRegion = atlas.Region;
		}
		else if (texture != null)
		{
			sheet = texture;
			frameRegion = new Rect2(Vector2.Zero, texture.GetSize());
		}

		FrameSize = frameRegion.Size;
		ArtScale = sprite.Scale.Abs();
		Mirrored = sprite.FlipH;
		Tint = sprite.Modulate;
		BodyWasVisible = sprite.Visible && sheet != null;
		Vector2 topLeft = (sprite.Centered ? -FrameSize * 0.5f : Vector2.Zero) + sprite.Offset;
		frameOrigin = spriteRest + topLeft * ArtScale;
	}

	private static readonly StringName MovingAnimation = "moving";

	// --- Clock helpers ------------------------------------------------------------------------------

	/// <summary>True on the one frame the fall's clock passes <paramref name="time"/>: for one-off beats.</summary>
	public bool Crossed(float time) => previousT < time && T >= time;

	/// <summary>0 before <paramref name="from"/>, 1 after <paramref name="to"/>, linear between.</summary>
	public static float Span(float t, float from, float to) =>
		to <= from ? (t >= to ? 1f : 0f) : Mathf.Clamp((t - from) / (to - from), 0f, 1f);

	public static float EaseIn(float u) => u * u;
	public static float EaseOut(float u) => 1f - (1f - u) * (1f - u);
	public static float EaseInOut(float u) => u < 0.5f ? 2f * u * u : 1f - 2f * (1f - u) * (1f - u);

	/// <summary>A shudder: whole art pixels, alternating, at <paramref name="hz"/>, scaled by <paramref name="amount"/>.</summary>
	public static float Shudder(float t, float amount, float hz) =>
		MathF.Round(MathF.Sin(t * hz * Mathf.Tau) * amount);

	/// <summary>A cheap hash to 0..1, for layouts that must not move from frame to frame.</summary>
	public static float Hash(uint seed, int i)
	{
		uint h = (uint)i * 374761393u + seed * 668265263u;
		h = (h ^ (h >> 13)) * 1274126177u;
		h ^= h >> 16;
		return (h & 0xFFFFFF) / 16777216f;
	}

	public float Rand(float from, float to) => rng.RandfRange(from, to);

	// --- Body space ---------------------------------------------------------------------------------

	/// <summary>An art pixel of the frame (unmirrored, as the generator drew it) in local space, at rest.</summary>
	public Vector2 ArtToLocal(Vector2 art)
	{
		Vector2 local = frameOrigin + art * ArtScale;
		if (Mirrored)
			local.X = 2f * spriteRest.X - local.X;
		return local;
	}

	/// <summary>An art pixel carried by <paramref name="piece"/>'s current transform, in local space.</summary>
	public Vector2 PieceToLocal(Piece piece, Vector2 art)
	{
		Vector2 v = (art - (Vector2)piece.Pivot) * ArtScale * piece.Scale;
		v = v.Rotated(piece.Rotation) + frameOrigin + (Vector2)piece.Pivot * ArtScale + piece.Offset;
		if (Mirrored)
			v.X = 2f * spriteRest.X - v.X;
		return v;
	}

	/// <summary>An art-space direction (x as drawn) in local space.</summary>
	public Vector2 ArtDirection(Vector2 direction) => Mirrored ? new Vector2(-direction.X, direction.Y) : direction;

	/// <summary>
	/// Cuts a piece out of the frame: one or more rectangles of it (art pixels) that move together
	/// about <paramref name="pivot"/>. The first piece hides the boss's own sprite, so a fall that cuts
	/// the body into parts never shows the whole body behind them.
	/// </summary>
	public Piece Cut(Vector2I pivot, params Rect2I[] rects)
	{
		var piece = new Piece(pivot, rects);
		if (sheet == null || !BodyWasVisible)
		{
			piece.Build(bodyLayer, null, frameRegion, false, additive);
			return piece;
		}

		piece.Build(bodyLayer, sheet, frameRegion, true, additive);
		if (pieceCount == pieces.Length)
			Array.Resize(ref pieces, pieces.Length * 2);
		pieces[pieceCount++] = piece;
		if (sprite != null && IsInstanceValid(sprite))
		{
			sprite.Visible = false;
			sprite.Pause();
		}
		piece.Apply(this);
		return piece;
	}

	/// <summary>The whole frame as one piece, pivoting on the middle of its feet.</summary>
	public Piece CutWhole() =>
		Cut(new Vector2I((int)(FrameSize.X * 0.5f), (int)GroundRow),
			new Rect2I(0, 0, (int)FrameSize.X, (int)FrameSize.Y));

	/// <summary>The boss's own sprite, for a fall that only fades it (the kids' Wizard Dude).</summary>
	public AnimatedSprite2D BodySprite => sprite;

	/// <summary>
	/// A part of the body that moves as one. All of its numbers are in the frame's own space as the
	/// generator drew it - unmirrored, rotation positive clockwise - and the death mirrors them for a
	/// body that faced the other way.
	/// </summary>
	public sealed class Piece
	{
		public Vector2I Pivot { get; }
		private readonly Rect2I[] rects;
		private Node2D holder;
		private Node2D glowHolder;
		private Sprite2D[] parts;
		private Sprite2D[] glowParts;
		private Rect2 region;

		/// <summary>Local pixels moved from rest, as if unmirrored (x is mirrored for a mirrored body).</summary>
		public Vector2 Offset;
		/// <summary>Radians about the pivot, positive clockwise as drawn.</summary>
		public float Rotation;
		/// <summary>On top of the body's own scale, about the pivot.</summary>
		public Vector2 Scale = Vector2.One;
		/// <summary>Multiplied with the body's tint: darkening and fading.</summary>
		public Color Color = Colors.White;
		/// <summary>An additive twin of the same pixels, in this colour - a flare. Alpha 0 is off.</summary>
		public Color Glow = new(1f, 1f, 1f, 0f);
		/// <summary>Art rows shown, top inclusive to bottom exclusive: dissolving from the top, sinking from the bottom.</summary>
		public float VisibleTop;
		public float VisibleBottom = 4096f;
		public bool Visible = true;

		internal Piece(Vector2I pivot, Rect2I[] rects)
		{
			Pivot = pivot;
			this.rects = rects;
		}

		/// <summary>Sinks it <paramref name="rows"/> art rows into the ground: lowered, and clipped at the ground line.</summary>
		public void Sink(BossDeath death, float rows)
		{
			Offset = new Vector2(Offset.X, rows * death.ArtScale.Y);
			VisibleBottom = death.FrameSize.Y - rows;
		}

		internal void Build(Node2D parent, Texture2D sheet, Rect2 frameRegion, bool visible, Material glowMaterial)
		{
			region = frameRegion;
			holder = new Node2D { Visible = visible };
			glowHolder = new Node2D { Visible = false, Material = glowMaterial };
			parts = new Sprite2D[rects.Length];
			glowParts = new Sprite2D[rects.Length];
			for (int i = 0; i < rects.Length; i++)
			{
				parts[i] = new Sprite2D { Texture = sheet, Centered = false, RegionEnabled = true };
				glowParts[i] = new Sprite2D { Texture = sheet, Centered = false, RegionEnabled = true };
				holder.AddChild(parts[i]);
				glowHolder.AddChild(glowParts[i]);
			}
			parent.AddChild(holder);
			parent.AddChild(glowHolder);
		}

		internal void Apply(BossDeath death)
		{
			if (holder == null)
				return;

			bool show = Visible && death.BodyWasVisible && Color.A > 0.003f;
			holder.Visible = show;
			glowHolder.Visible = show && Glow.A > 0.003f;
			if (!show)
				return;

			float sign = death.Mirrored ? -1f : 1f;
			Vector2 pivotLocal = death.frameOrigin + (Vector2)Pivot * death.ArtScale;
			if (death.Mirrored)
				pivotLocal.X = 2f * death.spriteRest.X - pivotLocal.X;
			Vector2 position = pivotLocal + new Vector2(sign * Offset.X, Offset.Y);
			// Whole local pixels, so the art grid holds while it slides; turning cannot be helped.
			position = new Vector2(MathF.Round(position.X), MathF.Round(position.Y));
			var scale = new Vector2(sign * death.ArtScale.X * Scale.X, death.ArtScale.Y * Scale.Y);

			holder.Position = position;
			holder.Rotation = sign * Rotation;
			holder.Scale = scale;
			holder.Modulate = death.Tint * Color;
			if (glowHolder.Visible)
			{
				glowHolder.Position = position;
				glowHolder.Rotation = sign * Rotation;
				glowHolder.Scale = scale;
				glowHolder.Modulate = Glow;
			}

			int top = Mathf.RoundToInt(VisibleTop);
			int bottom = Mathf.RoundToInt(VisibleBottom);
			for (int i = 0; i < rects.Length; i++)
			{
				Rect2I r = rects[i];
				int y0 = Math.Max(r.Position.Y, top);
				int y1 = Math.Min(r.End.Y, bottom);
				bool any = y1 > y0;
				parts[i].Visible = any;
				glowParts[i].Visible = any;
				if (!any)
					continue;
				var src = new Rect2(region.Position + new Vector2(r.Position.X, y0), new Vector2(r.Size.X, y1 - y0));
				var at = new Vector2(r.Position.X - Pivot.X, y0 - Pivot.Y);
				parts[i].RegionRect = src;
				parts[i].Position = at;
				glowParts[i].RegionRect = src;
				glowParts[i].Position = at;
			}
		}
	}

	// --- Debris -------------------------------------------------------------------------------------

	/// <summary>The atlas's cells (tools/art/boss_deaths.py's CELLS, in the same order).</summary>
	public static class Cell
	{
		// Elderbark: four leaf tumble frames, then the bark, a bone out of the ribcage, moss.
		public const int Leaf = 0, BarkLong = 4, BarkChunk = 5, Bone = 6, Moss = 7;
		// The Gaoler.
		public const int KeyLong = 8, KeyShort = 9, LinkFlat = 10, LinkEdge = 11, Shackle = 12, Ring = 13;
		// The Archivist: four page tumble frames, then two shards of tablet.
		public const int Page = 16, TabletShardA = 20, TabletShardB = 21;
		// The Still Warden.
		public const int ShardLong = 24, ShardMid = 25, ShardSmall = 26, IceChunk = 27, PlateShard = 28;
		// The Long Coil and the Warden of the Deep.
		public const int Scale = 32, HornTip = 33, ArmourA = 34, ArmourB = 35, Spike = 36, Slag = 37;
		// The Hayward, the Ninth Stone.
		public const int StrawA = 40, StrawB = 41, StrawC = 42, Withy = 43, ChipA = 44, ChipB = 45, ChipC = 46, Heather = 47;
		// Mother Rot, and earth for anything that tears out of the ground.
		public const int Clod = 48, Gob = 49, Hide = 50;
	}

	/// <summary>How a handful of debris is thrown. Defaults are a heavy chip that lands and skids.</summary>
	public struct Throw
	{
		/// <summary>Atlas cell, or -1 for a plain square of <see cref="Size"/> pixels.</summary>
		public int Cell;
		/// <summary>Consecutive cells to cycle through while airborne (a leaf's tumble), at <see cref="Fps"/>.</summary>
		public int Frames;
		public float Fps;
		/// <summary>Further cells to pick one of at random (a long key or a short one).</summary>
		public int Variants;
		public float Size;
		public Color Color;
		public Color ColorB;
		/// <summary>Multiply by the body's tint: true for its own material, false for anything that glows.</summary>
		public bool Tinted;
		/// <summary>Draw additively: sparks, embers, motes.</summary>
		public bool Light;
		/// <summary>Ground-plane direction (art space, 0 = right as drawn, pi/2 = toward the camera) and its spread.</summary>
		public float Angle;
		public float Spread;
		public float SpeedMin, SpeedMax;
		/// <summary>The ground plane is seen at a slant: depth travel is this share of across travel.</summary>
		public float Depth;
		public float UpMin, UpMax;
		/// <summary>Pixels a second squared; negative rises.</summary>
		public float Gravity;
		/// <summary>Fastest fall, pixels a second: a leaf's or a page's float. 0 for none.</summary>
		public float Terminal;
		public float Drag;
		public float Bounce;
		public float Friction;
		public float Flutter;
		public float FlutterHz;
		/// <summary>Quarter turns a second while airborne.</summary>
		public float Spin;
		/// <summary>Never lands: drifts and fades (motes, embers).</summary>
		public bool NoLand;
		public float Life;
		public float Fade;
		/// <summary>Ground-plane scatter of where the debris starts, around the point it is thrown from.</summary>
		public float DepthJitter;
		/// <summary>Once landed, a pixel of this light winks on it now and then: a key, a shard of ice.</summary>
		public Color Glint;

		public Throw()
		{
			Cell = -1;
			Frames = 1;
			Fps = 8f;
			Variants = 1;
			Size = Px;
			Color = Colors.White;
			ColorB = Colors.White;
			Tinted = false;
			Light = false;
			Angle = 0f;
			Spread = Mathf.Tau;
			SpeedMin = 40f;
			SpeedMax = 140f;
			Depth = 0.5f;
			UpMin = 80f;
			UpMax = 200f;
			Gravity = 620f;
			Terminal = 0f;
			Drag = 0.4f;
			Bounce = 0.3f;
			Friction = 6f;
			Flutter = 0f;
			FlutterHz = 1.5f;
			Spin = 0f;
			NoLand = false;
			Life = 2f;
			Fade = 0.4f;
			DepthJitter = 10f;
			Glint = new Color(1f, 1f, 1f, 0f);
		}
	}

	private struct Bit
	{
		public Vector2 P;
		public float H;
		public Vector2 V;
		public float VH;
		public float Gravity, Terminal, Drag, Bounce, Friction, Flutter, FlutterHz, Phase, Spin;
		public float Born, Die, Fade;
		public int Cell, Frames;
		public float Fps;
		public int Turns;
		public bool Flip, Landed, NoLand, Light;
		public float Size;
		public Color Color;
		public Color Glint;
	}

	/// <summary>Throws <paramref name="count"/> bits from an art point of the body at rest, scattered by <paramref name="artScatter"/>.</summary>
	public void Emit(in Throw spec, int count, Vector2 artPoint, Vector2 artScatter, float delay = 0f, float stagger = 0f)
	{
		for (int i = 0; i < count; i++)
		{
			Vector2 art = artPoint + new Vector2(rng.RandfRange(-1f, 1f) * artScatter.X, rng.RandfRange(-1f, 1f) * artScatter.Y);
			Vector2 at = ArtToLocal(art);
			float groundY = Foot.Y + rng.RandfRange(-spec.DepthJitter, spec.DepthJitter);
			Spawn(spec, new Vector2(at.X, groundY), Mathf.Max(0f, groundY - at.Y), delay + i * stagger);
		}
	}

	/// <summary>Throws from a point on the ground plane (local), <paramref name="height"/> pixels above it.</summary>
	public void EmitAt(in Throw spec, int count, Vector2 groundPoint, float height, float scatter = 0f, float delay = 0f, float stagger = 0f)
	{
		for (int i = 0; i < count; i++)
		{
			var jitter = new Vector2(rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f) * 0.5f) * scatter;
			Spawn(spec, groundPoint + jitter, height, delay + i * stagger);
		}
	}

	private void Spawn(in Throw spec, Vector2 groundPoint, float height, float delay)
	{
		if (bitCount >= MaxBits)
			return;

		float angle = spec.Angle + rng.RandfRange(-0.5f, 0.5f) * spec.Spread;
		float speed = rng.RandfRange(spec.SpeedMin, spec.SpeedMax);
		Vector2 direction = ArtDirection(new Vector2(MathF.Cos(angle), MathF.Sin(angle) * spec.Depth));
		Color color = spec.Color.Lerp(spec.ColorB, rng.Randf());
		if (spec.Tinted)
			color *= Tint;
		int cell = spec.Cell;
		if (cell >= 0 && spec.Variants > 1)
			cell += rng.RandiRange(0, spec.Variants - 1);

		float born = T + delay;
		bits[bitCount++] = new Bit
		{
			P = groundPoint,
			H = height,
			V = direction * speed,
			VH = rng.RandfRange(spec.UpMin, spec.UpMax),
			Gravity = spec.Gravity,
			Terminal = spec.Terminal,
			Drag = spec.Drag,
			Bounce = spec.Bounce,
			Friction = spec.Friction,
			Flutter = spec.Flutter,
			FlutterHz = spec.FlutterHz * rng.RandfRange(0.75f, 1.3f),
			Phase = rng.Randf() * Mathf.Tau,
			Spin = spec.Spin * (rng.Randf() < 0.5f ? -1f : 1f),
			Born = born,
			Die = born + spec.Life * rng.RandfRange(0.85f, 1f),
			Fade = Mathf.Max(0.01f, spec.Fade),
			Cell = cell,
			Frames = Math.Max(1, spec.Frames),
			Fps = spec.Fps,
			Turns = rng.RandiRange(0, 3),
			Flip = rng.Randf() < 0.5f,
			NoLand = spec.NoLand,
			Light = spec.Light,
			Size = WhipLimb.SnapSize(spec.Size),
			Color = color,
			Glint = spec.Glint,
		};
	}

	private void StepBits(float t, float dt)
	{
		for (int i = 0; i < bitCount; i++)
		{
			ref Bit b = ref bits[i];
			if (t < b.Born || t >= b.Die)
				continue;

			if (b.Landed)
			{
				b.V *= MathF.Exp(-b.Friction * dt);
				b.P += b.V * dt;
				continue;
			}

			b.VH -= b.Gravity * dt;
			if (b.Terminal > 0f && b.VH < -b.Terminal)
				b.VH = -b.Terminal;
			b.V *= MathF.Exp(-b.Drag * dt);
			b.P += b.V * dt;
			b.H += b.VH * dt;
			if (b.NoLand || b.H > 0f)
				continue;

			b.H = 0f;
			if (b.Bounce > 0f && b.VH < -90f)
			{
				b.VH = -b.VH * b.Bounce;
				b.V *= 0.65f;
				b.Spin *= 0.5f;
			}
			else
			{
				b.Landed = true;
				b.VH = 0f;
				// Settles at the quarter turn it was in the air, so nothing lands on a diagonal.
				b.Turns = (b.Turns + (int)((t - b.Born) * b.Spin)) & 3;
			}
		}
	}

	// A landed piece of metal or ice catches the light now and then: one hot pixel for a tenth of a
	// second, on its own beat, so a scatter of keys twinkles rather than blinking in step.
	private void DrawGlints(CanvasItem canvas)
	{
		float t = T;
		for (int i = 0; i < bitCount; i++)
		{
			ref Bit b = ref bits[i];
			if (!b.Landed || b.Glint.A <= 0f || t >= b.Die - b.Fade)
				continue;
			float beat = (t * 1.7f + b.Phase) % 1f;
			if (beat > 0.12f)
				continue;
			float a = b.Glint.A * (1f - MathF.Abs(beat - 0.06f) / 0.06f);
			Vector2 at = Snap(new Vector2(b.P.X - Px, b.P.Y - b.H - Px));
			canvas.DrawRect(new Rect2(at, new Vector2(Px, Px)), new Color(b.Glint, a));
			canvas.DrawRect(new Rect2(at + new Vector2(-Px, 0f), new Vector2(Px * 3f, Px)), new Color(b.Glint, a * 0.4f));
			canvas.DrawRect(new Rect2(at + new Vector2(0f, -Px), new Vector2(Px, Px * 3f)), new Color(b.Glint, a * 0.4f));
		}
	}

	private void DrawBits(CanvasItem canvas, bool landed, bool lightLayer)
	{
		float t = T;
		Texture2D atlas = debrisAtlas;
		for (int i = 0; i < bitCount; i++)
		{
			ref Bit b = ref bits[i];
			if (t < b.Born || t >= b.Die || b.Light != lightLayer || (b.Landed != landed && !b.Light))
				continue;

			float since = t - b.Born;
			float alpha = Mathf.Clamp((b.Die - t) / b.Fade, 0f, 1f);
			float sway = b.Landed ? 0f : b.Flutter * MathF.Sin(b.Phase + since * b.FlutterHz * Mathf.Tau);
			Vector2 at = Snap(new Vector2(b.P.X + sway, b.P.Y - b.H));
			var color = new Color(b.Color, b.Color.A * alpha);

			if (b.Cell < 0 || atlas == null)
			{
				canvas.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
				canvas.DrawRect(WhipLimb.Cell(at, b.Size), color);
				continue;
			}

			int cell = b.Cell;
			int turns = b.Turns;
			if (!b.Landed)
			{
				if (b.Frames > 1)
					cell += (int)(since * b.Fps) % b.Frames;
				turns = (turns + (int)(since * b.Spin)) & 3;
			}
			var src = new Rect2(cell % DebrisColumns * DebrisCell, cell / DebrisColumns * DebrisCell, DebrisCell, DebrisCell);
			canvas.DrawSetTransform(at, turns * (Mathf.Pi * 0.5f), new Vector2(b.Flip ? -Px : Px, Px));
			canvas.DrawTextureRectRegion(atlas, new Rect2(-DebrisCell * 0.5f, -DebrisCell * 0.5f, DebrisCell, DebrisCell), src, color);
		}
		canvas.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	// --- Puffs --------------------------------------------------------------------------------------

	private struct Puff
	{
		public Vector2 At;
		public float Born, Life, From, To, Rise;
		public Color Color;
		public bool Light;
	}

	/// <summary>A puff of dust, sand, steam or smoke: a disc on the art grid that swells, rises and fades.</summary>
	public void PuffAt(Vector2 at, float fromRadius, float toRadius, float life, Color color, float rise = 10f, float delay = 0f, bool lightLayer = false)
	{
		if (puffCount >= MaxPuffs)
			return;
		puffs[puffCount++] = new Puff
		{
			At = at, Born = T + delay, Life = Mathf.Max(0.05f, life), From = fromRadius, To = toRadius, Rise = rise,
			Color = color, Light = lightLayer,
		};
	}

	/// <summary>A ring of puffs round a point: the dust a heavy thing raises where it lands.</summary>
	public void DustRing(Vector2 at, float radius, int count, Color color, float life = 0.7f, float size = 14f)
	{
		for (int i = 0; i < count; i++)
		{
			float a = Mathf.Tau * i / count + rng.RandfRange(-0.3f, 0.3f);
			var offset = new Vector2(MathF.Cos(a) * radius, MathF.Sin(a) * radius * 0.45f) * rng.RandfRange(0.6f, 1f);
			PuffAt(at + offset, size * 0.4f, size * rng.RandfRange(0.8f, 1.3f), life * rng.RandfRange(0.8f, 1.15f), color, rng.RandfRange(6f, 18f), rng.RandfRange(0f, 0.08f));
		}
	}

	private void DrawPuffs(CanvasItem canvas, bool lightLayer)
	{
		float t = T;
		for (int i = 0; i < puffCount; i++)
		{
			ref Puff p = ref puffs[i];
			if (p.Light != lightLayer || t < p.Born || t >= p.Born + p.Life)
				continue;
			float u = (t - p.Born) / p.Life;
			float r = Mathf.Lerp(p.From, p.To, EaseOut(u));
			float alpha = p.Color.A * (1f - u) * (1f - u);
			// Two stops, a thin outer and a denser middle: a puff, not a coin.
			Vector2 at = p.At - new Vector2(0f, p.Rise * u);
			Disc(canvas, at, r, r * 0.8f, new Color(p.Color, alpha * 0.45f));
			Disc(canvas, at + new Vector2(-r * 0.12f, -r * 0.1f), r * 0.62f, r * 0.5f, new Color(p.Color, alpha * 0.5f));
		}
	}

	// --- The layers ---------------------------------------------------------------------------------

	private void DrawGround()
	{
		if (!begun || Boss == null || !IsInstanceValid(Boss))
			return;
		Boss.DrawDeathGround(this, ground, T);
		DrawBits(ground, true, false);
	}

	private void DrawAir()
	{
		if (!begun || Boss == null || !IsInstanceValid(Boss))
			return;
		Boss.DrawDeathAir(this, air, T);
		DrawPuffs(air, false);
		DrawBits(air, false, false);
	}

	private void DrawLight()
	{
		if (!begun || Boss == null || !IsInstanceValid(Boss))
			return;
		Boss.DrawDeathLight(this, light, T);
		DrawPuffs(light, true);
		DrawBits(light, false, true);
		DrawGlints(light);
	}

	// --- Drawing on the art grid --------------------------------------------------------------------
	// Everything a fall draws for itself is squares on the 2 px grid, never an anti-aliased curve: the
	// stain, the frost, the cracks are art, and art keeps its pixels.

	public static Vector2 Snap(Vector2 p) => new(MathF.Round(p.X / Px) * Px, MathF.Round(p.Y / Px) * Px);

	/// <summary>A filled ellipse, one 2 px row at a time.</summary>
	public static void Disc(CanvasItem canvas, Vector2 centre, float rx, float ry, Color color)
	{
		if (rx < Px * 0.5f || ry < Px * 0.5f || color.A <= 0.003f)
			return;
		centre = Snap(centre);
		int rows = Math.Max(1, (int)MathF.Ceiling(ry / Px));
		for (int k = -rows; k < rows; k++)
		{
			float y = (k + 0.5f) * Px;
			float f = 1f - (y * y) / (ry * ry);
			if (f <= 0f)
				continue;
			float half = MathF.Round(rx * MathF.Sqrt(f) / Px) * Px;
			if (half < Px)
				continue;
			canvas.DrawRect(new Rect2(centre.X - half, centre.Y + k * Px, half * 2f, Px), color);
		}
	}

	/// <summary>An ellipse's band between two radii (the outer at <paramref name="rx"/>, <paramref name="ry"/>).</summary>
	public static void Ring(CanvasItem canvas, Vector2 centre, float rx, float ry, float thickness, Color color)
	{
		if (rx < Px || ry < Px || color.A <= 0.003f)
			return;
		centre = Snap(centre);
		float irx = MathF.Max(0f, rx - thickness);
		float iry = MathF.Max(0f, ry - thickness * ry / rx);
		int rows = (int)MathF.Ceiling(ry / Px);
		for (int k = -rows; k < rows; k++)
		{
			float y = (k + 0.5f) * Px;
			float fo = 1f - (y * y) / (ry * ry);
			if (fo <= 0f)
				continue;
			float outer = MathF.Round(rx * MathF.Sqrt(fo) / Px) * Px;
			float fi = iry > 0f ? 1f - (y * y) / (iry * iry) : -1f;
			float inner = fi > 0f ? MathF.Round(irx * MathF.Sqrt(fi) / Px) * Px : 0f;
			float yy = centre.Y + k * Px;
			if (inner <= 0f)
			{
				canvas.DrawRect(new Rect2(centre.X - outer, yy, outer * 2f, Px), color);
				continue;
			}
			if (outer - inner >= Px)
			{
				canvas.DrawRect(new Rect2(centre.X - outer, yy, outer - inner, Px), color);
				canvas.DrawRect(new Rect2(centre.X + inner, yy, outer - inner, Px), color);
			}
		}
	}

	/// <summary>A line of squares on the grid.</summary>
	public static void Line(CanvasItem canvas, Vector2 a, Vector2 b, Color color, float size = Px)
	{
		float length = a.DistanceTo(b);
		int steps = Math.Max(1, (int)(length / Px));
		size = WhipLimb.SnapSize(size);
		for (int i = 0; i <= steps; i++)
			canvas.DrawRect(WhipLimb.Cell(a.Lerp(b, i / (float)steps), size), color);
	}

	/// <summary>Part of an ellipse's edge, from angle <paramref name="from"/> to <paramref name="to"/>, as squares.</summary>
	public static void Arc(CanvasItem canvas, Vector2 centre, float rx, float ry, float from, float to, Color color, float size = Px)
	{
		float length = MathF.Abs(to - from) * MathF.Max(rx, ry);
		int steps = Math.Max(2, (int)(length / Px));
		size = WhipLimb.SnapSize(size);
		for (int i = 0; i <= steps; i++)
		{
			float a = from + (to - from) * i / steps;
			canvas.DrawRect(WhipLimb.Cell(centre + new Vector2(MathF.Cos(a) * rx, MathF.Sin(a) * ry), size), color);
		}
	}

	/// <summary>
	/// A crack as art-space points: a walk out from <paramref name="from"/> along
	/// <paramref name="angle"/>, turning a little at every step and never doubling back, and stopping
	/// where the body does - a crack runs through the thing, never out into the air beside it. Built
	/// once, when the fall begins, and grown by <see cref="DrawCrack"/>.
	/// </summary>
	public Vector2[] CrackPath(uint seed, Vector2 from, float angle, int steps, float step)
	{
		var path = new Vector2[steps + 1];
		path[0] = from;
		int count = 1;
		float heading = angle;
		for (int i = 1; i <= steps; i++)
		{
			heading += (Hash(seed, i) - 0.5f) * 1.1f;
			heading = Mathf.Clamp(heading, angle - 0.9f, angle + 0.9f);
			Vector2 next = path[i - 1] + new Vector2(MathF.Cos(heading), MathF.Sin(heading)) * step;
			if (!Solid(next))
				break;
			path[count++] = next;
		}
		Array.Resize(ref path, count);
		return path;
	}

	private Image frameImage;

	/// <summary>True where the frame the boss fell on has a pixel, in art space.</summary>
	public bool Solid(Vector2 art)
	{
		if (art.X < 0f || art.Y < 0f || art.X >= FrameSize.X || art.Y >= FrameSize.Y)
			return false;
		if (sheet == null)
			return true;
		// Read once, when the first crack asks: the sheet is lossless (art-direction section 6), so
		// this is its pixels as drawn.
		frameImage ??= sheet.GetImage();
		if (frameImage == null || frameImage.IsEmpty())
			return true;
		int x = (int)(frameRegion.Position.X + art.X);
		int y = (int)(frameRegion.Position.Y + art.Y);
		if (x < 0 || y < 0 || x >= frameImage.GetWidth() || y >= frameImage.GetHeight())
			return false;
		return frameImage.GetPixel(x, y).A > 0.5f;
	}

	/// <summary>
	/// Draws the first <paramref name="grown"/> (0 to 1) of a crack on <paramref name="piece"/>,
	/// following it wherever it has moved: a core of <paramref name="core"/> in a wider <paramref name="glow"/>.
	/// </summary>
	public void DrawCrack(CanvasItem canvas, Piece piece, Vector2[] path, float grown, Color core, Color glow)
	{
		if (path == null || path.Length < 2 || grown <= 0f)
			return;
		float reach = grown * (path.Length - 1);
		for (int i = 0; i < path.Length - 1 && i < reach; i++)
		{
			float part = Mathf.Min(1f, reach - i);
			Vector2 a = piece != null ? PieceToLocal(piece, path[i]) : ArtToLocal(path[i]);
			Vector2 b = piece != null ? PieceToLocal(piece, path[i].Lerp(path[i + 1], part)) : ArtToLocal(path[i].Lerp(path[i + 1], part));
			if (glow.A > 0f)
				Line(canvas, a, b, glow, Px * 2f);
			Line(canvas, a, b, core, Px);
		}
	}
}
