using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// BC1, THE FALLEN STAR - the Broken Circle's spell vault (.ai/achievements-and-secrets.md 8.3). It pays
// Starfall: the star that came down on the moot's heath, learned by gathering what was left of it.
//
// SEES. A line of burnt heather running dead straight across the heath - nothing else on a moor is
// straight - glinting at intervals: seven warm shards lie along it. The trail is the far tell and the
// near one at once, so the seen mark is taken from the nearest point of it to the player (TellPosition),
// wherever he crosses it. It runs two chunks and more from the crater, back the way he is likely to come
// (its bearing is the crater's toward the start, turned up to fifty degrees either way, from the seed),
// and singed tufts and fused glass thicken round the crater itself (its FarTell, the glass only near).
//
// DOES. Gather, on SecretGather: walk over each shard, in any order - nothing resets, the order is his.
// Each taken lights a point on the crater's rim. With all seven, linger: stand in the crater for
// LingerSeconds and the star's light rises out of its stone; step out and it sinks back as fast as it
// rose (never past nothing), so a crowd that drives him out costs him the time and not the shards. A walk
// of the trail and two seconds still: inside the vault's twenty for a player who follows it.
//
// PAYS. The light goes to him and the vault opens (SecretVault.Open): the first time ever Starfall, granted
// and saved at once, offered from the next level-up; in a later run the same walk pays a chest from the
// secret budget. The stone lies split and empty after.
//
// Nothing here is solid and nothing is a target: he stands in the crater, and the horde with him. Progress
// lives in the run state: the shards in Key + "/shards" (SecretGather's marks), the linger in the vault's
// own State.Progress, opened in State.Done.
//
// Art: tools/art/secret_circle.py (the crater in two states, the shards, the burn, the far tell). On the
// world's 2-unit grid; the glints and the rising light in opaque stops, fading only by dither.
public partial class FallenStar : SecretVault
{
	/// <summary>How many shards lie along the trail (8.3: seven).</summary>
	public const int Shards = 7;

	/// <summary>How long the trail runs from the crater: from two chunks out, as a vault's far tell does.</summary>
	public const float TrailMin = 2300f;
	public const float TrailMax = 2800f;

	/// <summary>How far either way of the bearing back to the start the trail may turn, in radians (about 50 degrees).</summary>
	public const float TrailTurn = 0.9f;

	/// <summary>Walking this near a shard takes it.</summary>
	public const float ShardReach = 44f;

	/// <summary>Standing this near the crater's middle is standing in it; this long there opens it (8.3: 2 s).</summary>
	public const float LingerRadius = 80f;
	public const float LingerSeconds = 2f;

	/// <summary>One burn mark every this many px along the trail.</summary>
	public const float BurnStep = 42f;

	private const string CraterPath = "res://assets/bonelight/world/heath/fallen-star-crater.png";
	private const string ShardPath = "res://assets/bonelight/world/heath/star-shard.png";
	private static readonly string[] BurnPaths =
	{
		"res://assets/bonelight/world/heath/scorch-1.png",
		"res://assets/bonelight/world/heath/scorch-2.png",
		"res://assets/bonelight/world/heath/scorch-3.png",
	};

	// The strips (secret_circle.py at x2). The crater: two 192 x 120 frames, the bowl's middle on the node
	// (art 48, 34), its rim an ellipse 80 x 40 round it, the star's heart at (0, -4). A shard: three 28 x 28
	// frames, its contact line 24 px down under its middle.
	private static readonly Vector2 CraterCell = new(192f, 120f);
	private static readonly Vector2 CraterAnchor = new(-96f, -68f);
	private static readonly Vector2 Heart = new(0f, -4f);
	private static readonly Vector2 Rim = new(80f, 40f);
	private static readonly Vector2 ShardCell = new(28f, 28f);
	private static readonly Vector2 ShardFoot = new(14f, 24f);

	private static readonly SecretFarTell Tell = new()
	{
		Textures = new[]
		{
			"res://assets/bonelight/world/heath/scorch-tuft-1.png",
			"res://assets/bonelight/world/heath/scorch-tuft-2.png",
		},
		NearTextures = new[]
		{
			"res://assets/bonelight/world/heath/star-glass-1.png",
			"res://assets/bonelight/world/heath/star-glass-2.png",
		},
		InnerRadius = SecretCatalog.VaultFootprint,
		NearRadius = 1536f,
		OuterRadius = 3072f,
		PeakPerChunk = 40,
		NearShare = 0.6f,
		FootFromBottom = 4f,
	};

	public override SecretFarTell FarTell => Tell;

	/// <summary>The near tell is the trail: the point of it nearest the player, wherever he crosses it.</summary>
	public override Vector2 TellPosition
	{
		get
		{
			Player player = Player;
			if (player == null)
				return GlobalPosition;
			Vector2 a = trailStart, b = GlobalPosition;
			Vector2 ab = b - a;
			float t = ab.LengthSquared() > 1f ? Mathf.Clamp((player.GlobalPosition - a).Dot(ab) / ab.LengthSquared(), 0f, 1f) : 1f;
			return a + ab * t;
		}
	}

	/// <summary>The shards, gathered in any order. Public for the probe.</summary>
	public SecretGather Gather { get; private set; }

	/// <summary>Where the trail begins, far out on the heath.</summary>
	public Vector2 TrailStart => trailStart;

	/// <summary>The burn marks along the trail that found open ground.</summary>
	public int BurnCount => burn.Count;

	/// <summary>Seconds stood in the crater with every shard gathered, toward LingerSeconds.</summary>
	public float Lingered => State?.Progress ?? 0f;

	/// <summary>True once the star's light has left its stone this run.</summary>
	public bool Risen => opening >= 0f || IsOpened;

	private Vector2 trailStart;
	private List<Vector2> points = new();
	private readonly List<(Vector2 At, int Variant)> marks = new();
	private readonly List<(Vector2 At, int Variant)> burn = new();
	private Texture2D crater;
	private Texture2D shard;
	private Texture2D[] burnTextures;
	private Node2D burnLayer;
	private Node2D shardLayer;
	private Node2D effects;
	private float clock;
	private float opening = -1f;
	private float[] flare;

	private const float RiseSeconds = 0.5f;
	private const float FlySeconds = 0.9f;
	private const float FlareSeconds = 0.6f;

	public override void LayOut(ExplorePlacer placer)
	{
		// The trail's bearing: back toward the start, turned by the seed, so a player walking out from the
		// start is likely to cross it; its length from two chunks.
		float back = (placer.Origin - Position).Angle();
		float bearing = back + (placer.NextFloat() * 2f - 1f) * TrailTurn;
		float length = Mathf.Lerp(TrailMin, TrailMax, placer.NextFloat());
		Vector2 dir = Vector2.FromAngle(bearing);
		trailStart = Position + dir * length;
		// The shards strewn along it, from its far end to just short of the crater's lip.
		points = SecretGather.AlongTrail(placer, trailStart, Position + dir * 300f, Shards, 90f, 40f);
		// The burn: a mark every BurnStep along the line, a little off it either way - every roll drawn
		// here, before any ground is asked about, so a mark that lands in a tarn shifts nothing after it.
		Vector2 side = new(-dir.Y, dir.X);
		Vector2 lip = Position + dir * 170f;
		int n = Math.Max(2, (int)((length - 170f) / BurnStep));
		for (int i = 0; i < n; i++)
		{
			float t = i / (float)(n - 1);
			float off = (placer.NextFloat() * 2f - 1f) * 18f;
			int variant = Math.Min(2, (int)(placer.NextFloat() * 3f));
			marks.Add((trailStart.Lerp(lip, t) + side * off, variant));
		}
	}

	public override void _Ready()
	{
		ZIndex = DrawLayers.Ground;
		ZAsRelative = false;
		crater = ResourceLoader.Exists(CraterPath) ? GD.Load<Texture2D>(CraterPath) : null;
		shard = ResourceLoader.Exists(ShardPath) ? GD.Load<Texture2D>(ShardPath) : null;
		burnTextures = new Texture2D[BurnPaths.Length];
		for (int i = 0; i < BurnPaths.Length; i++)
			burnTextures[i] = ResourceLoader.Exists(BurnPaths[i]) ? GD.Load<Texture2D>(BurnPaths[i]) : null;
		if (crater == null || shard == null || Array.Exists(burnTextures, t => t == null))
			GD.PushError("FallenStar: its art is missing; re-run python tools/art/secret_circle.py, then import.");
		if (points.Count == 0)
		{
			trailStart = GlobalPosition + new Vector2(-TrailMin, 0f);
			points = SecretGather.AlongTrail(Director.Game.CreateExplorePlacer(8, 5f, 7f), trailStart, GlobalPosition, Shards);
		}
		flare = new float[points.Count];
		Array.Fill(flare, -1f);

		// Only open ground keeps its mark: a tarn or a granite tor breaks the line, as it would.
		TerrainStreamer terrain = Director.Game.Terrain;
		foreach (var mark in marks)
			if (terrain == null || (!terrain.IsBlockedAtWorld(mark.At) && !terrain.IsWallAtWorld(mark.At)))
				burn.Add(mark);

		// The burn lies flat on the heath under everything, drawn once; the shards lie on it, under the
		// bodies; the light that leaves them flies over everything.
		burnLayer = new Node2D { Name = "Burn", ZIndex = DrawLayers.Decal, ZAsRelative = false, TopLevel = true };
		burnLayer.Draw += DrawBurn;
		AddChild(burnLayer);
		burnLayer.GlobalPosition = Vector2.Zero;

		shardLayer = new Node2D { Name = "Shards", ZIndex = DrawLayers.GroundTell, ZAsRelative = false, TopLevel = true };
		shardLayer.Draw += DrawShards;
		AddChild(shardLayer);
		shardLayer.GlobalPosition = Vector2.Zero;

		Gather = new SecretGather
		{
			Name = "Gather",
			Director = Director,
			State = Director.StateOf(Key + "/shards"),
			Points = points,
			TouchRadius = ShardReach,
			DrawSelf = false,
		};
		Gather.Taken += OnTaken;
		AddChild(Gather);

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
		if (burnLayer != null && IsInstanceValid(burnLayer))
			burnLayer.Draw -= DrawBurn;
		if (shardLayer != null && IsInstanceValid(shardLayer))
			shardLayer.Draw -= DrawShards;
		if (effects != null && IsInstanceValid(effects))
			effects.Draw -= DrawEffects;
		if (Gather != null)
			Gather.Taken -= OnTaken;
		base._ExitTree();
	}

	private void OnTaken(int index)
	{
		if (index >= 0 && index < flare.Length)
			flare[index] = 0f;
		SfxPlayer.AtPosition(SfxCatalog.BearingFound, points[index], 0f);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		for (int i = 0; i < flare.Length; i++)
			if (flare[i] >= 0f && (flare[i] += dt) > FlareSeconds)
				flare[i] = -1f;

		Player player = Player;
		SecretDirector.SiteState state = State;
		if (opening >= 0f)
		{
			opening += dt;
			if (opening >= RiseSeconds + FlySeconds)
			{
				opening = -1f;
				// Rewards in flight are paid even if the boss has come meanwhile (stage-secrets section 4).
				Open(GlobalPosition + new Vector2(0f, 110f));
			}
		}
		else if (player != null && state != null && !state.Done && Active && Gather.IsComplete)
		{
			bool inside = player.GlobalPosition.DistanceTo(GlobalPosition) <= LingerRadius;
			state.Progress = inside ? state.Progress + dt : MathF.Max(0f, state.Progress - dt);
			if (state.Progress >= LingerSeconds)
				Rise();
		}

		if (player == null)
			return;
		// Drawn only while some of it could be on screen.
		bool near = player.GlobalPosition.DistanceSquaredTo(GlobalPosition) < 1600f * 1600f;
		bool nearShard = false;
		foreach (Vector2 p in points)
			if (player.GlobalPosition.DistanceSquaredTo(p) < 1600f * 1600f)
				nearShard = true;
		if (near)
			QueueRedraw();
		if (near || nearShard)
		{
			shardLayer.QueueRedraw();
			effects.QueueRedraw();
		}
	}

	private void Rise()
	{
		opening = 0f;
		SfxPlayer.AtPosition(SfxCatalog.EventReward, GlobalPosition, 0f);
	}

	/// <summary>Every shard gathered now, as if he had walked the trail: for _UiShot's photographs.</summary>
	public void GatherAllForProbe()
	{
		if (Gather?.State == null)
			return;
		Gather.State.Marks = points.Count >= 64 ? ulong.MaxValue : (1UL << points.Count) - 1UL;
		Gather.State.Step = points.Count;
	}

	// --- drawing: every texture on the world's 2-unit grid --------------------------------------------------

	private void DrawBurn()
	{
		foreach (var (at, variant) in burn)
		{
			Texture2D t = burnTextures[variant];
			if (t != null)
				burnLayer.DrawTexture(t, SpellInk.Snap(at - t.GetSize() * 0.5f));
		}
	}

	private void DrawShards()
	{
		if (shard == null)
			return;
		for (int i = 0; i < points.Count; i++)
		{
			if (Gather.IsTaken(i))
				continue;
			Vector2 corner = SpellInk.Snap(points[i] - ShardFoot);
			shardLayer.DrawTextureRectRegion(shard, new Rect2(corner, ShardCell), new Rect2(new Vector2((i % 3) * ShardCell.X, 0f), ShardCell));
			DrawGlint(shardLayer, points[i] + new Vector2(0f, -12f), clock + i * 0.37f);
		}
	}

	// The crater and the stone: the bowl on the node, on the world's grid; the rim's points of light, one for
	// each shard gathered; the stone's heart, brighter with every shard and rising while he lingers.
	public override void _Draw()
	{
		if (crater == null)
			return;
		SpellInk.AlignToWorldGrid(this);
		DrawTextureRectRegion(crater, new Rect2(CraterAnchor, CraterCell), new Rect2(new Vector2(Risen ? CraterCell.X : 0f, 0f), CraterCell));
		DrawSetTransform(Vector2.Zero);
		int gathered = Gather?.Gathered ?? 0;
		for (int i = 0; i < gathered && i < Shards; i++)
		{
			float a = -Mathf.Pi * 0.5f + i * Mathf.Tau / Shards;
			DrawGlint(this, new Vector2(MathF.Cos(a) * Rim.X, MathF.Sin(a) * Rim.Y), GlintPeriod * 0.5f, 0.7f);
		}
		if (Risen)
			return;
		if (Gather != null && Gather.IsComplete && Lingered > 0f)
			DrawLight(this, Heart, 4f + 10f * Mathf.Clamp(Lingered / LingerSeconds, 0f, 1f), 0.8f);
		else
			DrawGlint(this, Heart, clock, 0.45f + 0.55f * gathered / Shards);
	}

	// World units: TopLevel at the origin, through the dither material.
	private void DrawEffects()
	{
		// A taken shard's light lifts off it and goes out.
		for (int i = 0; i < flare.Length; i++)
		{
			if (flare[i] < 0f)
				continue;
			float t = flare[i] / FlareSeconds;
			DrawLight(effects, points[i] + new Vector2(0f, -12f - 36f * t), 7f, 1f - t);
		}
		if (opening < 0f || Player is not Player player)
			return;
		Vector2 from = GlobalPosition + Heart;
		Vector2 at;
		if (opening < RiseSeconds)
			at = from + new Vector2(0f, -40f * (opening / RiseSeconds));
		else
		{
			float t = Mathf.Clamp((opening - RiseSeconds) / FlySeconds, 0f, 1f);
			float eased = t * t * (3f - 2f * t);
			at = (from + new Vector2(0f, -40f)).Lerp(player.GlobalPosition + new Vector2(0f, -40f), eased)
				+ new Vector2(0f, -50f * MathF.Sin(t * MathF.PI));
		}
		DrawLight(effects, at, 12f);
		DrawGlint(effects, at, 0.6f, 1f);
	}
}
