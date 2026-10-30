using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// C2, THE GEODE HEART - the Sunken Cave's spell vault, and Eric's giant crystal (.ai/achievements-and-
// secrets.md 8.4.1): "a giant crystal that needs to be destroyed to find a related spell". It pays Frost
// Shard, which makes every death shatter - learned by shattering the biggest thing in the cave.
//
// SEES. From two chunks out the cave's small crystal clusters grow commoner, and from one chunk out more
// of them carry hairline cracks (its FarTell). Then a crystal three men tall, two hundred pixels from foot
// to tip, grown up through the cavern floor - the same water-blue as the clusters that led here - with the
// warm glint breathing deep inside it: the light he missed, locked in stone.
//
// DOES. Strike: every point of damage the player lands on an enemy within StrikeReach of its foot counts
// (a Breakable, on the StrikeListener the thaw was built round), and Needed breaks it, in four drawn
// stages - hairline from the first hit, then cracked, splitting, shattered. It is never a target and never
// in "enemies": the player stands at its foot and fights, and the swarm brings the damage to him. Its foot
// is solid - a body on the ground's physics layer, stamped into the navigation flags (SolidFootprints) -
// so it stops boots and bodies and the flow field routes the horde round it, which makes the pocket it
// stands in the best ground in the run for a minute.
//
// PAYS. It shatters outward: a ring of frost and its own shards out to FreezeRadius, freezing every enemy
// in it for FreezeSeconds (ApplySlow(0), which respects a boss's floor), and secret_shatter - the game's
// loudest secret sound, once. Then the warm light rises out of the shards and goes to him, and on arrival
// the vault opens (SecretVault.Open): the first time ever Frost Shard, granted and saved at once, offered
// from the next level-up; in a later run the same burst and a chest from the secret budget.
//
// Art: tools/art/secret_cave.py (the strip, the shards, the cracked clusters). Its stages are the
// Breakable's frames; the glint, the ring, the shards in flight and the rising light are drawn here.
public partial class GeodeHeart : SecretVault
{
	/// <summary>Hits on enemies standing within this of the foot count (8.4.1: 140).</summary>
	public const float StrikeReach = 140f;

	/// <summary>Weighted damage that breaks it (8.4.1: 2,000). Whether 2,000 is right is for eyes on the phone.</summary>
	public const float DamageNeeded = 2000f;

	/// <summary>The shatter's reach and how long it freezes (8.4.1: 300 px, 2 s).</summary>
	public const float FreezeRadius = 300f;
	public const float FreezeSeconds = 2f;

	/// <summary>The solid foot: the body, and the cells stamped solid in the navigation grid.</summary>
	public const float FootRadius = 56f;
	private static readonly Vector2 FootBody = new(116f, 40f);

	private const string SheetPath = "res://assets/bonelight/world/cave/geode-heart.png";
	private const string ShardsPath = "res://assets/bonelight/world/cave/geode-shards.png";
	// The strip's geometry (secret_cave.py at x2): a 192 x 224 frame, its foot at (96, 208), the heart at
	// (98, 104) - so the heart is 2 px right of the foot and 104 above it.
	private static readonly Vector2 SheetAnchor = new(-96f, -208f);
	private static readonly Vector2 HeartOffset = new(2f, -104f);
	private const int ShardFrames = 6;
	private const float ShardCell = 24f;

	/// <summary>The far tell, one instance (its textures load once): .ai/achievements-and-secrets.md 8.4.1, "Sees".</summary>
	private static readonly SecretFarTell Tell = new()
	{
		Textures = new[]
		{
			"res://assets/bonelight/world/crystal-blue.png",
			"res://assets/bonelight/world/crystal-violet.png",
		},
		NearTextures = new[]
		{
			"res://assets/bonelight/world/cave/crystal-blue-cracked.png",
			"res://assets/bonelight/world/cave/crystal-violet-cracked.png",
		},
		InnerRadius = SecretCatalog.VaultFootprint,
		NearRadius = 1536f,
		OuterRadius = 3072f,
		// The cave's own scatter is well under one crystal a chunk, so even a few dozen read as "commoner".
		PeakPerChunk = 72,
		NearShare = 0.85f,
	};

	public override SecretFarTell FarTell => Tell;

	public override IEnumerable<(Vector2 Centre, float Radius)> SolidFootprints => new[] { (Position, FootRadius) };

	public override Vector2 TellPosition => GlobalPosition + HeartOffset;

	/// <summary>The crystal: its strike, its stages and its progress. Public for the probe.</summary>
	public Breakable Crystal { get; private set; }

	/// <summary>True once the shatter's beat has run in this run (the light has left it).</summary>
	public bool HasShattered { get; private set; }

	private Node2D effects;
	private Texture2D shards;
	private float clock;
	private float burst = -1f;      // seconds since the shatter, while its ring and shards are in the air
	private float mote = -1f;       // seconds the warm light has been travelling to him
	private Vector2 moteFrom;
	private readonly List<(float Angle, float Reach, float Spin, int Frame)> flying = new();

	private const float BurstSeconds = 0.7f;
	private const float MoteDelay = 0.45f;
	private const float MoteSeconds = 0.9f;

	public override void _Ready()
	{
		Texture2D sheet = ResourceLoader.Exists(SheetPath) ? GD.Load<Texture2D>(SheetPath) : null;
		shards = ResourceLoader.Exists(ShardsPath) ? GD.Load<Texture2D>(ShardsPath) : null;
		if (sheet == null || shards == null)
			GD.PushError("GeodeHeart: its art is missing; re-run python tools/art/secret_cave.py, then import.");

		Crystal = new Breakable
		{
			Name = "Crystal",
			Needed = DamageNeeded,
			Reach = StrikeReach,
			StageCount = 3,
			// Its own state, apart from the vault's: broken (the crystal) and opened (the vault) are two beats.
			State = Director.StateOf(Key + "/crystal"),
			CanProgress = () => Active,
			Watcher = () => Player,
			Sheet = sheet,
			SheetOffset = SheetAnchor,
		};
		Crystal.Broke += OnBroke;
		AddChild(Crystal);

		// The solid foot: on the ground's physics layer, where boots and bodies meet terrain, as the
		// flower pen's fence is. It stays when the crystal breaks - the stump is still there.
		var body = new StaticBody2D
		{
			Name = "Foot",
			CollisionLayer = 1u << (LevelTilePainter.TerrainCollisionLayer - 1),
			CollisionMask = 0,
		};
		body.AddChild(new CollisionShape2D { Position = new Vector2(0f, -4f), Shape = new RectangleShape2D { Size = FootBody } });
		AddChild(body);

		// The light and the burst fly over everything (DrawLayers.Air), not sorted among the bodies.
		effects = new Node2D { Name = "Light", ZIndex = DrawLayers.Air, ZAsRelative = false };
		effects.Draw += DrawEffects;
		AddChild(effects);

		HasShattered = Crystal.IsBroken;
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
		if (burst >= 0f)
		{
			burst += dt;
			if (burst > BurstSeconds)
				burst = -1f;
		}
		if (mote >= 0f)
		{
			mote += dt;
			if (mote >= MoteDelay + MoteSeconds)
			{
				mote = -1f;
				// Rewards in flight are paid even if the boss has come meanwhile (stage-secrets section 4).
				Open(GlobalPosition + new Vector2(0f, 110f));
			}
		}
		Player player = Player;
		if (player != null && player.GlobalPosition.DistanceSquaredTo(GlobalPosition) < 1600f * 1600f)
			effects?.QueueRedraw();
	}

	// The frame after the last hit: the shatter, the freeze, and the light let go.
	private void OnBroke()
	{
		if (HasShattered)
			return;
		HasShattered = true;
		Vector2 foot = GlobalPosition;
		SfxPlayer.AtPosition(SfxCatalog.SecretShatter, foot + HeartOffset, 0f);
		float r2 = FreezeRadius * FreezeRadius;
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (enemy != null && IsInstanceValid(enemy) && !enemy.IsDying && enemy.GlobalPosition.DistanceSquaredTo(foot) <= r2)
				enemy.ApplySlow(0f, FreezeSeconds);
		}

		// Its shards, out to the ring's edge: eighteen, spread round, each a frame and a spin of its own,
		// from a fixed pattern (nothing here needs the run's random stream).
		flying.Clear();
		for (int k = 0; k < 18; k++)
		{
			float angle = k * Mathf.Tau / 18f + 0.17f * (k % 3);
			float reach = FreezeRadius * (0.72f + 0.28f * ((k * 7) % 5) / 4f);
			flying.Add((angle, reach, (k % 2 == 0 ? 1f : -1f) * (4f + k % 4), k % ShardFrames));
		}
		burst = 0f;
		mote = 0f;
		moteFrom = foot + HeartOffset;
	}

	/// <summary>Breaks it now, as if the last hit had landed: for _UiShot's photographs and nothing in play.</summary>
	public void BreakForProbe() => Crystal?.BreakNow();

	// --- drawing ----------------------------------------------------------------------------------------

	private void DrawEffects()
	{
		Vector2 heart = HeartOffset;

		// The near tell: the warm glint, breathing, deep in the stone - until the light has left it.
		if (!HasShattered)
		{
			// A little brighter as it cracks: the light is nearer the surface.
			float strength = 0.75f + 0.25f * Crystal.Stage / 3f;
			effects.DrawCircle(heart, 14f, new Color(GoldHi, 0.06f + 0.06f * Breath(clock)));
			DrawGlint(effects, heart, clock, strength);
		}

		if (burst >= 0f)
		{
			float t = Mathf.Clamp(burst / BurstSeconds, 0f, 1f);
			float spread = 1f - (1f - t) * (1f - t);
			// The frost going out: one ring of the shatter's own cold light, ending on the freeze's edge.
			float ring = FreezeRadius * spread;
			effects.DrawArc(Vector2.Zero, ring, 0f, Mathf.Tau, 64, new Color(0.78f, 0.96f, 1.0f, 0.55f * (1f - t)), 6f * (1f - t) + 1f);
			effects.DrawArc(Vector2.Zero, ring * 0.92f, 0f, Mathf.Tau, 64, new Color(0.50f, 0.86f, 0.97f, 0.35f * (1f - t)), 2f);
			// The shards, flung from the heart's height down to the ring.
			if (shards != null)
			{
				foreach (var (angle, reach, spin, frame) in flying)
				{
					Vector2 dir = Vector2.FromAngle(angle);
					Vector2 at = heart.Lerp(dir * reach * new Vector2(1f, 0.7f), spread) + new Vector2(0f, -40f * MathF.Sin(t * MathF.PI));
					effects.DrawSetTransform(at, spin * t, Vector2.One);
					effects.DrawTextureRectRegion(shards, new Rect2(-ShardCell * 0.5f, -ShardCell * 0.5f, ShardCell, ShardCell),
						new Rect2(frame * ShardCell, 0f, ShardCell, ShardCell), new Color(1f, 1f, 1f, 1f - t * t));
				}
				effects.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
			}
		}

		// The warm light rising out of the shards, then going to him.
		if (mote >= 0f && Player is Player player)
		{
			Vector2 from = moteFrom - GlobalPosition;
			Vector2 at;
			if (mote < MoteDelay)
				at = from + new Vector2(0f, -36f * (mote / MoteDelay));
			else
			{
				float t = Mathf.Clamp((mote - MoteDelay) / MoteSeconds, 0f, 1f);
				float eased = t * t * (3f - 2f * t);
				Vector2 start = from + new Vector2(0f, -36f);
				Vector2 end = player.GlobalPosition - GlobalPosition + new Vector2(0f, -40f);
				at = start.Lerp(end, eased) + new Vector2(0f, -60f * MathF.Sin(t * MathF.PI));
			}
			effects.DrawCircle(at, 16f, new Color(GoldHi, 0.18f));
			effects.DrawCircle(at, 8f, new Color(GoldHi, 0.35f));
			DrawGlint(effects, at, 0.6f, 1f);
		}
	}
}
