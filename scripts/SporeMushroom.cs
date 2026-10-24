using Godot;
using System;
using WizardSurvivors.scripts;

// Toxic Spore Burst: mushrooms sown among the enemies. Each one sprouts, swells for a moment - the
// warning is the cap itself, visibly fattening - and then bursts in a cloud of spores that flies out
// to exactly the reach it hits, and poisons everything it reaches.
//
// No ring (2026-10-23, "remove aoe markers from abilities, their animations should make it clear
// what the hitbox is"). It used to draw a ring at the burst radius through the whole swell, a flash
// ring at the burst, and a flat disc and a rim under Lingering Rot's cloud and every ember patch.
// Now the spores do it: a ring of puffs lands on the rim of every burst and the rest fill it, all
// thrown out from the cap in the first eighth of a second, so the cloud's edge IS the reach; the
// cloud that lingers is those same puffs, turning in place; a patch is the flames that fill it.
//
// It replaced a pulse around the player (ElementalPulse, which Solar Flare still uses) that the
// player called "very small", and whose Colossal Ascension visibly did nothing. Three reasons, all
// gone with it: the pulse measured its reach from GetRangeAtLevel and never read
// GetAreaMultiplierAtLevel, so Titan Cataclysm's "+100% Area" never reached the hit; the sprite was
// scaled to three quarters of that reach and squashed; and the evolution's x2 visual scale was
// applied once, when the persistent pulse was created at level 1 - before any evolution existed.
//
// THE ONE HIT AUTHORITY IS Cap.Radius. The burst tests it, and everything drawn for it (the spores'
// flight and the rim they land on, Lingering Rot's cloud, Ember Caps' patches) is laid out of it, with
// every puff and flame placed so its outer edge is inside it. Area from levels, from the player and
// from every evolution is folded into it once, in For().
//
// ONE NODE, SEVERAL MUSHROOMS. A sown mushroom is one node; the ones Mycelial Bloom sprouts from it
// are slots in its fixed Cap array rather than new nodes, so the chain - the only place this spell
// makes things quickly - instantiates nothing and allocates nothing while it runs. The node frees
// itself when the last of its caps has finished. Drawn in two layers: the ground (this node, under
// the actors) and an overlay child above them for the mushrooms and the burst.
public partial class SporeMushroom : Node2D
{
	// --- tuning. The cards in SpellEvolutionCatalog interpolate these, so a change here is a change there. ---
	public const float BaseBurstRadius = 80f;
	public const float BaseFuseSeconds = 1.1f;
	public const int BasePoisonTick = 2;
	public const float BasePoisonSeconds = 3f;
	// How far a mushroom is sown in front of its enemy, toward the player: they are walking that way,
	// and a mushroom planted on where an enemy stands now swells behind it.
	public const float LeadPixels = 70f;

	public const string LingeringRotId = "toxic_spore_lingering_rot";
	public const string EmberCapsId = "toxic_spore_ember_caps";
	public const string GiantCapsId = "toxic_spore_giant_caps";
	public const string DoomcapId = "toxic_spore_doomcap";
	public const string MycelialBloomId = "toxic_spore_mycelial_bloom";

	// Lingering Rot: the cloud a burst leaves, as wide as the burst.
	public const float CloudSeconds = 3.5f;
	// Ember Caps: patches of burning ground inside the burst.
	public const int EmberPatches = 4;
	public const int DoomcapEmberPatches = 7;
	public const float EmberRadiusFraction = 0.34f;
	public const float EmberSeconds = 2.5f;
	public const float EmberTickFraction = 0.3f;
	// Doomcap: one titan instead of the scatter, grown for a fixed time whatever else slows the caps.
	public const float DoomcapFuseSeconds = 2.6f;
	public const float DoomcapDamagePerMushroom = 0.15f;
	// Mycelial Bloom: children past the rim of each burst, two then one.
	public const int BloomFirstChildren = 2;
	public const int BloomSecondChildren = 1;
	public const float BloomRadiusScale = 0.8f;
	public const float BloomDamageScale = 0.5f;
	public const float BloomFuseScale = 0.5f;
	private const float BloomTravelSeconds = 0.18f;
	private const float BloomReach = 1.25f;

	// The frame counts are tools/art/spore_mushrooms.py's PHASES: change both.
	public const int SproutFrames = 5;
	public const int SwellFrames = 4;
	public const int BurstFrames = 5;
	private const int PuffFrames = 6;
	private const int EmberFrames = 4;
	private const int PuffCell = 16;
	private const float PuffHalf = PuffCell; // a 16 px puff drawn at x2 is 32 across
	private const float SproutShare = 0.4f;
	private const float BurstSeconds = 0.5f;
	private const float PuffLife = 0.42f;
	// How long the spores take to fly from the cap to where they burst: the motion that shows the reach.
	private const float PuffFlightSeconds = 0.12f;
	// Spacing of the rim's puffs, centre to centre: a 32 px puff every 34 px is an edge, not a dotted line.
	private const float RimSpacing = 34f;
	private const float TickSeconds = 0.5f;

	public enum SporeKind { Poison, Fire }

	/// <summary>Everything one cast decides about the mushrooms it sows.</summary>
	public struct Sowing
	{
		public int Damage;
		public float Radius;
		public int PoisonTick;
		public float PoisonSeconds;
		public float FuseSeconds;
		public SporeKind Kind;
		public bool Titan;
		public bool Giant;
		public float CloudSeconds;
		public float EmberSeconds;
		public int BloomGenerations;
	}

	/// <summary>
	/// The one place a spell's level, its evolutions and the player's stats become a mushroom.
	/// <paramref name="count"/> is how many the cast sows, which Doomcap folds into its damage.
	/// </summary>
	public static Sowing For(SpellData spell, float playerArea, float damageMultiplier, float durationMultiplier, int count)
	{
		int level = spell.CurrentLevel;
		bool titan = spell.HasEvolution(DoomcapId);
		var sow = new Sowing
		{
			// Every area term reaches the radius, the evolutions' included (GetAreaMultiplierAtLevel
			// walks CollectActive) - the term the old pulse never read.
			Radius = (BaseBurstRadius + spell.GetEffectValueAtLevel(SpellEffect.AreaSize, level))
				* playerArea * spell.GetAreaMultiplierAtLevel(level),
			Kind = spell.HasEvolution(EmberCapsId) ? SporeKind.Fire : SporeKind.Poison,
			PoisonSeconds = BasePoisonSeconds * durationMultiplier,
			FuseSeconds = titan ? DoomcapFuseSeconds : BaseFuseSeconds / MathF.Max(0.1f, spell.GetSpeedMultiplier()),
			Titan = titan,
			// Giant Caps draws its caps on the large-actor cell; with Doomcap the titan wins.
			Giant = !titan && spell.HasEvolution(GiantCapsId),
			CloudSeconds = spell.HasEvolution(LingeringRotId) ? CloudSeconds * durationMultiplier : 0f,
			EmberSeconds = EmberSeconds * durationMultiplier,
			BloomGenerations = spell.HasEvolution(MycelialBloomId) ? 2 : 0,
		};
		int damage = Math.Max(1, Mathf.RoundToInt(spell.GetDamageAtLevel(level) * damageMultiplier));
		if (titan)
			damage = Math.Max(1, Mathf.RoundToInt(damage * (1f + DoomcapDamagePerMushroom * Math.Max(1, count))));
		sow.Damage = damage;
		// Ember spores burn instead of poisoning: the trade Ember Caps makes with the Poison tag.
		sow.PoisonTick = sow.Kind == SporeKind.Poison
			? BasePoisonTick + Mathf.RoundToInt(spell.GetEffectValueAtLevel(SpellEffect.DotDamage, level))
				+ spell.GetPoisonTickBonusAtLevel(level)
			: 0;
		return sow;
	}

	[Export] public Texture2D MushroomPoison { get; set; }
	[Export] public Texture2D MushroomFire { get; set; }
	[Export] public Texture2D GiantPoison { get; set; }
	[Export] public Texture2D GiantFire { get; set; }
	[Export] public Texture2D DoomcapPoison { get; set; }
	[Export] public Texture2D DoomcapFire { get; set; }
	[Export] public Texture2D PuffPoison { get; set; }
	[Export] public Texture2D PuffFire { get; set; }
	[Export] public Texture2D EmberFlame { get; set; }

	public SpellData SpellData { get; private set; }
	public Player PlayerRef { get; private set; }

	private struct Cap
	{
		public Vector2 At;      // local; the foot of the mushroom and the centre of its burst
		public Vector2 From;    // the cap it sprouted from (Mycelial Bloom), for the thread
		public float Age;       // seconds since it began to sprout; negative while mycelium travels
		public float Fuse;      // sprout + swell
		public float Since;     // seconds since it burst
		public float Radius;    // THE hit radius, and the drawn one
		public int Damage;
		public int Generation;
		public bool Live;
		public bool Burst;
	}

	private struct Puff
	{
		public Vector2 Offset;
		public float Delay;
	}

	private struct Patch
	{
		public Vector2 At;
		public float Radius;
	}

	private const int MaxCaps = 1 + BloomFirstChildren + BloomFirstChildren * BloomSecondChildren;
	private readonly Cap[] caps = new Cap[MaxCaps];
	private int capCount;
	private Puff[] puffs;
	private int puffsPerCap;
	private int rimPerCap;
	private Patch[] patches;
	private int patchesPerCap;
	private Sowing sow;
	private Node2D overlay;
	private float tickTimer;

	// Burning is held to once per enemy per half second ACROSS every mushroom, not per mushroom.
	// There is no burn status to be stacking-resistant (.ai/gameplay-design.md, "Status effects"),
	// so this is the same rule kept by hand: measured without it, a crowd pinned on the player stood
	// in the patches of five overlapping mushrooms at once and Ember Caps out-damaged every spell in
	// the game at level 6. A shared game clock, bucketed by the tick, and the set of enemies burned
	// in the current bucket - reused, so nothing is allocated once it has grown.
	private static double emberClock;
	private static ulong emberClockFrame;
	private static long emberBucket = -1;
	private static readonly System.Collections.Generic.HashSet<ulong> burnedThisBucket = new();

	// Mycelial Bloom's thread, in the Grass ramp's mid and edge (.ai/art-direction.md). Static so a
	// frame of drawing allocates nothing. Everything else drawn is the generated art.
	private static readonly Color ThreadMid = new Color("#9BD455");
	private static readonly Color ThreadEdge = new Color("#4A8C3A");

	/// <summary>Called by Player before the node enters the tree.</summary>
	public void Configure(SpellData spell, Player player, in Sowing sowing)
	{
		SpellData = spell;
		PlayerRef = player;
		sow = sowing;
	}

	public override void _Ready()
	{
		// Under the actors, over the ground decor: the cloud and the burning patches are on the floor.
		ZIndex = -1;
		overlay = new Node2D { Name = "Overlay", ZIndex = 2 };
		AddChild(overlay);
		overlay.Draw += DrawOverlay;

		// A ring of puffs on the root cap's rim, RimSpacing apart, and enough more inside to fill the
		// disc about as densely at any size: 19 at the base radius, 52 on a Doomcap. Children share the
		// counts; they are smaller, so their rims are a little denser.
		rimPerCap = Mathf.Clamp((int)MathF.Round(Mathf.Tau * MathF.Max(0f, sow.Radius - PuffHalf) / RimSpacing), 6, 28);
		puffsPerCap = rimPerCap + Mathf.Clamp((int)(sow.Radius * sow.Radius / 900f), 4, 24);
		puffs = new Puff[MaxCaps * puffsPerCap];
		patchesPerCap = sow.Kind == SporeKind.Fire ? (sow.Titan ? DoomcapEmberPatches : EmberPatches) : 0;
		patches = new Patch[MaxCaps * Math.Max(1, patchesPerCap)];

		caps[0] = new Cap
		{
			At = Vector2.Zero,
			From = Vector2.Zero,
			Fuse = MathF.Max(0.2f, sow.FuseSeconds),
			Radius = MathF.Max(8f, sow.Radius),
			Damage = Math.Max(1, sow.Damage),
			Live = true,
		};
		capCount = 1;

		// The titan is the one mushroom worth hearing arrive; an ordinary sowing is covered by the
		// cast cue the fire loop already plays.
		if (sow.Titan)
			SfxPlayer.AtPosition(SfxCatalog.SpellSummon, GlobalPosition);
	}

	public override void _ExitTree()
	{
		if (overlay != null && IsInstanceValid(overlay))
			overlay.Draw -= DrawOverlay;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (Engine.GetProcessFrames() != emberClockFrame)
		{
			emberClockFrame = Engine.GetProcessFrames();
			emberClock += dt;
		}
		bool anyLive = false;
		for (int i = 0; i < capCount; i++)
		{
			if (!caps[i].Live)
				continue;
			anyLive = true;
			caps[i].Age += dt;
			if (!caps[i].Burst)
			{
				if (caps[i].Age >= caps[i].Fuse)
					BurstCap(i);
			}
			else
			{
				caps[i].Since += dt;
				if (caps[i].Since >= AfterBurstSeconds())
					caps[i].Live = false;
			}
		}

		tickTimer += dt;
		if (tickTimer >= TickSeconds)
		{
			tickTimer -= TickSeconds;
			TickLingering();
		}

		if (!anyLive)
		{
			QueueFree();
			return;
		}
		QueueRedraw();
		overlay.QueueRedraw();
	}

	private float AfterBurstSeconds()
	{
		float seconds = BurstSeconds;
		if (sow.CloudSeconds > 0f)
			seconds = MathF.Max(seconds, sow.CloudSeconds);
		if (patchesPerCap > 0)
			seconds = MathF.Max(seconds, sow.EmberSeconds);
		return seconds;
	}

	private void BurstCap(int index)
	{
		caps[index].Burst = true;
		caps[index].Since = 0f;
		Cap cap = caps[index];

		if (PlayerRef != null && IsInstanceValid(PlayerRef))
		{
			Vector2 centre = GlobalPosition + cap.At;
			float r2 = cap.Radius * cap.Radius;
			foreach (Node node in GetTree().GetNodesInGroup("enemies"))
			{
				if (node is not Node2D enemy || !IsInstanceValid(enemy))
					continue;
				if (enemy.GlobalPosition.DistanceSquaredTo(centre) > r2)
					continue;
				PlayerRef.DealDamageToEnemy(enemy, cap.Damage, source: SpellData);
				// Stacking-resistant (Enemy.ApplyPoison): overlapping bursts refresh, never add.
				if (sow.PoisonTick > 0 && enemy is Enemy target)
					target.ApplyPoison(sow.PoisonTick, sow.PoisonSeconds);
			}
		}

		SeedPuffs(index, cap);
		if (patchesPerCap > 0)
			SeedPatches(index, cap);
		if (cap.Generation < sow.BloomGenerations)
			SproutChildren(cap);
	}

	// The first rimPerCap puffs land on the rim - their outer edge exactly on the hit radius, which is
	// what the burst's edge is now that nothing else draws it - and the rest anywhere inside, never so
	// near the rim that one is drawn past it.
	private void SeedPuffs(int index, in Cap cap)
	{
		float reach = MathF.Max(0f, cap.Radius - PuffHalf);
		int start = index * puffsPerCap;
		float turn = GD.Randf() * Mathf.Tau;
		for (int k = 0; k < puffsPerCap; k++)
		{
			bool rim = k < rimPerCap;
			float a = rim ? turn + Mathf.Tau * (k + 0.3f * (GD.Randf() - 0.5f)) / rimPerCap : GD.Randf() * Mathf.Tau;
			int inner = k - rimPerCap;
			// Inside: the first few near the middle, so the cloud is full at the cap and not hollow.
			float d = rim ? reach : inner < 3 ? reach * 0.3f * GD.Randf() : reach * 0.88f * MathF.Sqrt(GD.Randf());
			puffs[start + k] = new Puff
			{
				Offset = new Vector2(MathF.Cos(a), MathF.Sin(a)) * d,
				Delay = rim ? GD.Randf() * 0.04f : GD.Randf() * 0.14f,
			};
		}
	}

	// How far out a spore has flown, 0..1: thrown from the cap, fast and slowing, to where it bursts.
	private static float Flight(float since)
	{
		float u = Mathf.Clamp(since / PuffFlightSeconds, 0f, 1f);
		return 1f - (1f - u) * (1f - u);
	}

	private void SeedPatches(int index, in Cap cap)
	{
		float r = cap.Radius * EmberRadiusFraction;
		float reach = MathF.Max(0f, cap.Radius - r);
		int start = index * patchesPerCap;
		float turn = GD.Randf() * Mathf.Tau;
		// A Mycelial Bloom child lights half as many: twenty patches to one sowing was a carpet.
		int lit = cap.Generation == 0 ? patchesPerCap : Math.Max(1, patchesPerCap / 2);
		for (int k = 0; k < patchesPerCap; k++)
		{
			if (k >= lit)
			{
				patches[start + k] = new Patch { At = cap.At, Radius = 0f };
				continue;
			}
			float a = turn + Mathf.Tau * k / lit + (GD.Randf() - 0.5f) * 0.6f;
			float d = reach * Mathf.Lerp(0.25f, 1f, GD.Randf());
			patches[start + k] = new Patch { At = cap.At + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d, Radius = r };
		}
	}

	// Mycelial Bloom. Children sprout just past the parent's rim, leaning away from the player so
	// the chain runs out across the ground rather than back underfoot.
	private void SproutChildren(in Cap parent)
	{
		int children = parent.Generation == 0 ? BloomFirstChildren : BloomSecondChildren;
		Vector2 outward = Vector2.Zero;
		if (PlayerRef != null && IsInstanceValid(PlayerRef))
			outward = GlobalPosition + parent.At - PlayerRef.GlobalPosition;
		outward = outward.LengthSquared() > 1f ? outward.Normalized() : Vector2.Right.Rotated(GD.Randf() * Mathf.Tau);

		for (int k = 0; k < children && capCount < MaxCaps; k++)
		{
			float bend = children == 1 ? (GD.Randf() - 0.5f) * 0.8f : (k == 0 ? -0.65f : 0.65f);
			caps[capCount++] = new Cap
			{
				From = parent.At,
				At = parent.At + outward.Rotated(bend) * parent.Radius * BloomReach,
				Age = -BloomTravelSeconds,
				Fuse = MathF.Max(0.2f, caps[0].Fuse * BloomFuseScale),
				Radius = parent.Radius * BloomRadiusScale,
				Damage = Math.Max(1, Mathf.RoundToInt(caps[0].Damage * BloomDamageScale)),
				Generation = parent.Generation + 1,
				Live = true,
			};
		}
	}

	// Lingering Rot's clouds re-poison and Ember Caps' patches burn, on one half-second tick for
	// every cap on this node. Poison is stacking-resistant on its own (Enemy.ApplyPoison); a burn is
	// held to once per enemy per bucket across every mushroom (burnedThisBucket).
	private void TickLingering()
	{
		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;
		bool clouds = false, embers = false;
		for (int i = 0; i < capCount; i++)
		{
			if (!caps[i].Live || !caps[i].Burst)
				continue;
			clouds |= sow.CloudSeconds > 0f && caps[i].Since < sow.CloudSeconds;
			embers |= patchesPerCap > 0 && caps[i].Since < sow.EmberSeconds;
		}
		if (!clouds && !embers)
			return;
		long bucket = (long)(emberClock / TickSeconds);
		if (bucket != emberBucket)
		{
			emberBucket = bucket;
			burnedThisBucket.Clear();
		}

		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Enemy enemy || !IsInstanceValid(enemy))
				continue;
			Vector2 p = enemy.GlobalPosition - GlobalPosition;
			bool poisoned = false;
			bool burned = embers && burnedThisBucket.Contains(enemy.GetInstanceId());
			for (int i = 0; i < capCount && !(poisoned && burned); i++)
			{
				ref Cap cap = ref caps[i];
				if (!cap.Live || !cap.Burst)
					continue;
				if (clouds && !poisoned && cap.Since < sow.CloudSeconds
					&& p.DistanceSquaredTo(cap.At) <= cap.Radius * cap.Radius)
				{
					enemy.ApplyPoison(sow.PoisonTick, sow.PoisonSeconds);
					poisoned = true;
				}
				if (embers && !burned && cap.Since < sow.EmberSeconds)
				{
					int start = i * patchesPerCap;
					for (int k = 0; k < patchesPerCap; k++)
					{
						Patch patch = patches[start + k];
						if (patch.Radius <= 0f || p.DistanceSquaredTo(patch.At) > patch.Radius * patch.Radius)
							continue;
						PlayerRef.DealDamageToEnemy(enemy, Math.Max(1, Mathf.RoundToInt(cap.Damage * EmberTickFraction)), source: SpellData);
						burnedThisBucket.Add(enemy.GetInstanceId());
						burned = true;
						break;
					}
				}
			}
		}
	}

	// --- drawing ---------------------------------------------------------------------------------

	private Texture2D PuffTexture => sow.Kind == SporeKind.Fire ? PuffFire : PuffPoison;

	private static float Fade(float age, float life, float tail) =>
		Mathf.Clamp((life - age) / MathF.Max(0.01f, tail), 0f, 1f);

	// The ground layer: the cloud, the burning patches, the threads. The swell has no ground mark at
	// all - the fattening cap is the warning - and the burst is the spores, drawn on the overlay.
	public override void _Draw()
	{
		for (int i = 0; i < capCount; i++)
		{
			Cap cap = caps[i];
			if (!cap.Live)
				continue;

			if (cap.Generation > 0 && cap.Age < cap.Fuse * SproutShare)
				DrawThread(cap);
			if (cap.Age < 0f || !cap.Burst)
				continue;

			if (sow.CloudSeconds > 0f && cap.Since < sow.CloudSeconds)
				DrawCloud(i, cap);
			if (patchesPerCap > 0 && cap.Since < sow.EmberSeconds)
				DrawPatches(i, cap);
		}
	}

	private void DrawThread(in Cap cap)
	{
		float travel = Mathf.Clamp((cap.Age + BloomTravelSeconds) / BloomTravelSeconds, 0f, 1f);
		float fade = cap.Age > 0f ? 1f - cap.Age / MathF.Max(0.01f, cap.Fuse * SproutShare) : 1f;
		Vector2 end = cap.From.Lerp(cap.At, travel);
		DrawLine(cap.From, end, ThreadEdge with { A = 0.9f * fade }, 6f);
		DrawLine(cap.From, end, ThreadMid with { A = fade }, 3f);
	}

	// Lingering Rot. The cloud is the burst's own puffs left behind, turning slowly about the centre -
	// a rotation keeps each one exactly as far inside the rim as it landed, so the rim of puffs that
	// showed the burst's reach is still the rim of the ground that re-poisons. No disc, no ring.
	private void DrawCloud(int index, in Cap cap)
	{
		Texture2D tex = PuffTexture;
		// From the moment the burst's own puffs have opened, so the cloud takes over from them.
		if (tex == null || cap.Since < PuffLife)
			return;
		float fade = Fade(cap.Since, sow.CloudSeconds, 0.6f);
		int start = index * puffsPerCap;
		for (int k = 0; k < puffsPerCap; k++)
		{
			Puff puff = puffs[start + k];
			float spin = cap.Since * 0.35f * (k % 2 == 0 ? 1f : -1f);
			int frame = 1 + ((int)(cap.Since * 4f) + k) % 3;
			DrawPuff(this, tex, cap.At + puff.Offset.Rotated(spin), frame, new Color(1f, 1f, 1f, 0.7f * fade));
		}
	}

	private void DrawPatches(int index, in Cap cap)
	{
		float fade = Fade(cap.Since, sow.EmberSeconds, 0.5f);
		int start = index * patchesPerCap;
		for (int k = 0; k < patchesPerCap; k++)
		{
			Patch patch = patches[start + k];
			if (patch.Radius <= 0f || EmberFlame == null)
				continue;
			// The patch is the flames that fill it - one in the middle and a ring whose outer edges are
			// the patch's edge (a 16 px flame is 32 across at x2) - not a disc with a rim. The burn is
			// tested against the same radius.
			float room = MathF.Max(0f, patch.Radius - PuffHalf);
			int ring = Mathf.Clamp((int)MathF.Round(Mathf.Tau * room / 22f), 2, 8);
			for (int f = 0; f <= ring; f++)
			{
				float a = Mathf.Tau * f / ring + k;
				Vector2 at = f == ring ? patch.At : patch.At + new Vector2(MathF.Cos(a), MathF.Sin(a)) * room;
				int frame = ((int)(cap.Since * 10f) + f + k) % EmberFrames;
				var src = new Rect2(frame * PuffCell, 0, PuffCell, PuffCell);
				// Centred on its point rather than standing on it, so the tips stay inside the patch too.
				var dest = new Rect2(at.X - PuffCell, at.Y - PuffCell, PuffCell * 2f, PuffCell * 2f);
				DrawTextureRectRegion(EmberFlame, dest, src, new Color(1f, 1f, 1f, fade));
			}
		}
	}

	private static void DrawPuff(CanvasItem on, Texture2D tex, Vector2 at, int frame, Color modulate)
	{
		var src = new Rect2(frame * PuffCell, 0, PuffCell, PuffCell);
		var dest = new Rect2(at.X - PuffCell, at.Y - PuffCell, PuffCell * 2f, PuffCell * 2f);
		on.DrawTextureRectRegion(tex, dest, src, modulate);
	}

	// The overlay, above the actors: the mushrooms themselves, and the burst's spores.
	private void DrawOverlay()
	{
		bool fire = sow.Kind == SporeKind.Fire;
		Texture2D body = sow.Titan ? (fire ? DoomcapFire : DoomcapPoison)
			: sow.Giant ? (fire ? GiantFire : GiantPoison)
			: (fire ? MushroomFire : MushroomPoison);
		int cell = sow.Titan ? 64 : sow.Giant ? 48 : 32;
		Texture2D puffTex = PuffTexture;

		for (int i = 0; i < capCount; i++)
		{
			Cap cap = caps[i];
			if (!cap.Live || cap.Age < 0f)
				continue;

			int frame = -1;
			if (!cap.Burst)
			{
				float sprout = cap.Fuse * SproutShare;
				frame = cap.Age < sprout
					? Math.Min(SproutFrames - 1, (int)(cap.Age / sprout * SproutFrames))
					: SproutFrames + Math.Min(SwellFrames - 1, (int)((cap.Age - sprout) / MathF.Max(0.01f, cap.Fuse - sprout) * SwellFrames));
			}
			else if (cap.Since < BurstSeconds)
			{
				frame = SproutFrames + SwellFrames + Math.Min(BurstFrames - 1, (int)(cap.Since / BurstSeconds * BurstFrames));
			}

			if (frame >= 0 && body != null)
			{
				// Authored 1:1, shown at x2, the foot (row cell-2) on the cap's point.
				var src = new Rect2(frame * cell, 0, cell, cell);
				var dest = new Rect2(cap.At.X - cell, cap.At.Y - 2f * (cell - 1), cell * 2f, cell * 2f);
				overlay.DrawTextureRectRegion(body, dest, src);
			}

			if (cap.Burst && puffTex != null && cap.Since < PuffLife + 0.16f)
			{
				int start = i * puffsPerCap;
				for (int k = 0; k < puffsPerCap; k++)
				{
					Puff puff = puffs[start + k];
					float t = (cap.Since - puff.Delay) / PuffLife;
					if (t < 0f || t >= 1f)
						continue;
					// Thrown out from the cap to where it bursts: the cloud grows to the hit radius and
					// stops on it, which is the only thing that shows how far the burst reached.
					DrawPuff(overlay, puffTex, cap.At + puff.Offset * Flight(cap.Since - puff.Delay),
						Math.Min(PuffFrames - 1, (int)(t * PuffFrames)), Colors.White);
				}
			}
		}
	}
}
