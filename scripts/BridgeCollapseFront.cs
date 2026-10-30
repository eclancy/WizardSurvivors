using Godot;
using WizardSurvivors.scripts;

// THE BRIDGE FALLING IN BEHIND HIM (.ai/endgame-castle.md 3.3). His doing: from pace 0:30 the span falls
// along a straight front across the deck, from the back of the bridgehead to the gate yard's mouth at
// 15:00, about 31 px a pace second (BridgeRules.FrontSpeed). It is a moving HAZARD, not terrain - no cell
// changes and nothing is rebuilt - the way the Ninth Stone's ring is a hazard rather than a wall:
//
//   - behind the front is the drop: drawn over the fallen deck (the abyss's own surface, so it meets the
//     water beside it without a seam), with the broken end of the deck and its masonry falling away;
//   - standing in it costs 4 HP a second and slows him hard. Never instant death, and it is always on screen
//     before it reaches him: he walks at seven times its speed even slowed;
//   - an enemy behind it falls - gone, no XP, as if it had never been. Bosses and the elites that carry a
//     chest are spared (they are blockers, and a chest must not fall with its carrier): they walk out of it;
//   - XP orbs it overtakes are folded into one orb at its edge, as Node2DGame.SweepFarXpOrbs folds far orbs:
//     never lost, but fetching them means stepping back toward the fall.
//
// Nothing is placed behind it: BridgeRules.IsPlacementBlocked is the same line (hook 3).
public partial class BridgeCollapseFront : Node2D
{
	/// <summary>The rules it reads its position and the bridge's shape from. Set before it enters the tree.</summary>
	public BridgeRules Rules { get; set; }

	// 4 HP a second, in two ticks: the lava's tick (Node2DGame.HazardTickInterval) at half its bite.
	private const float DamageInterval = 0.5f;
	private const int DamagePerTick = 2;
	// "Slows the player hard": 55% off his speed while his boots are on the fallen deck. Applied to
	// Player.Speed as a factor and divided back out, so anything that multiplies his speed meanwhile (a relic
	// picked up in the drop) survives the round trip.
	private const float SlowFactor = 0.45f;
	// An enemy this far behind the front falls; a little past the line, so one standing on it is not culled
	// the frame the front reaches it.
	private const float FallMargin = 24f;
	private const float CullInterval = 0.2f;
	private const float FoldInterval = 1.0f;
	// Where the folded XP waits: this far ahead of the front, on the deck's middle third.
	private const float BankLead = 56f;

	private float damageTimer;
	private Player slowed;
	private float cullTimer;
	private float foldTimer;
	private XPOrb bank;
	private Texture2D abyss;
	private readonly RandomNumberGenerator rng = new();

	// Logged for the harness: real seconds he spent with his boots behind the front, and how many fell.
	private float secondsInDrop;
	private int fallen;
	private int folded;
	private float logTimer;

	// Masonry falling away from the broken edge, drawn in code (3.7: "particles drawn in code").
	private struct Block
	{
		public float T;       // across the bridge, px, where it broke off
		public float Age;
		public float Life;
		public float Size;
		public int Tone;
		public float Drift;   // its sideways drift, px a second
	}

	private readonly Block[] blocks = new Block[44];

	public override void _Ready()
	{
		ZAsRelative = false;
		// Over the painted ground, its detail and its decals; under the craters, the floor tells and every body.
		ZIndex = DrawLayers.Decal + 1;
		TextureRepeat = TextureRepeatEnum.Enabled;
		TextureFilter = TextureFilterEnum.Nearest;
		abyss = GD.Load<Texture2D>("res://assets/bonelight/tiles/water/abyss_surface.png");
		if (abyss == null)
			GD.PushError("BridgeCollapseFront: abyss_surface.png is not imported; the fallen deck would draw as nothing. Run: python tools/art/water.py, then import.");
		rng.Randomize();
		for (int i = 0; i < blocks.Length; i++)
			Respawn(ref blocks[i], rng.Randf());
	}

	public override void _ExitTree() => Unslow();

	public override void _Process(double delta)
	{
		if (Rules == null || Rules.Game == null || !IsInstanceValid(Rules.Game))
			return;
		float d = (float)delta;
		if (!Rules.FrontMoving)
		{
			Unslow();
			return;
		}
		float front = Rules.FrontS;

		Player player = Rules.Game.RunPlayer;
		bool inPlay = Node2DGame.InPlay;
		if (player != null && !player.IsDead && Rules.AlongOf(player.FootPosition) < front)
		{
			Slow(player);
			if (inPlay)
			{
				secondsInDrop += d;
				damageTimer += d;
				if (damageTimer >= DamageInterval)
				{
					damageTimer -= DamageInterval;
					player.TakeDamage(DamagePerTick);
				}
			}
		}
		else
		{
			Unslow();
			damageTimer = 0f;
		}

		if (inPlay)
		{
			cullTimer -= d;
			if (cullTimer <= 0f)
			{
				cullTimer = CullInterval;
				CullFallen(front);
			}
			foldTimer -= d;
			if (foldTimer <= 0f)
			{
				foldTimer = FoldInterval;
				FoldOrbs(front);
			}
			for (int i = 0; i < blocks.Length; i++)
			{
				blocks[i].Age += d;
				if (blocks[i].Age >= blocks[i].Life)
					Respawn(ref blocks[i], 0f);
			}
		}

		logTimer -= d;
		if (logTimer <= 0f && OS.GetCmdlineUserArgs().Length > 0)
		{
			logTimer = 60f;
			GD.Print($"BRIDGE front: at {front:0} px (pace {Rules.Game.PaceSeconds:0} s); he has stood {secondsInDrop:0.0} s on the fallen deck, "
				+ $"{fallen} enemies have fallen, {folded} orbs folded to its edge.");
		}
		QueueRedraw();
	}

	private void Slow(Player player)
	{
		if (slowed == player)
			return;
		Unslow();
		slowed = player;
		player.Speed *= SlowFactor;
	}

	private void Unslow()
	{
		if (slowed != null && IsInstanceValid(slowed))
			slowed.Speed /= SlowFactor;
		slowed = null;
	}

	// Ordinary enemies behind the line fall: out of the group and gone this frame, with nothing paid. The
	// snapshot is immutable, so freeing while walking it is safe (EnemyRegistry).
	private void CullFallen(float front)
	{
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (!IsInstanceValid(enemy) || enemy.IsDying || enemy.IsMiniBoss || enemy is BossEnemy)
				continue;
			if (Rules.AlongOf(enemy.GlobalPosition) > front - FallMargin)
				continue;
			enemy.SetTargetable(false);
			enemy.QueueFree();
			fallen++;
		}
	}

	// Every orb the front has passed is folded into one bank just ahead of it: the first one found becomes
	// the bank and is carried forward with the front; the rest add their value to it.
	private void FoldOrbs(float front)
	{
		if (bank != null && (!IsInstanceValid(bank) || bank.IsQueuedForDeletion()))
			bank = null;
		float edge = front + BankLead * 0.5f;
		foreach (Node node in GetTree().GetNodesInGroup(XPOrb.GroupName))
		{
			if (node is not XPOrb orb || orb == bank || orb.IsQueuedForDeletion())
				continue;
			if (Rules.AlongOf(orb.GlobalPosition) >= edge)
				continue;
			if (bank == null)
			{
				bank = orb;
				continue;
			}
			bank.Absorb(orb.Value);
			orb.QueueFree();
			folded++;
		}
		if (bank != null && Rules.AlongOf(bank.GlobalPosition) < front + BankLead)
		{
			// Onto the deck's middle third just ahead of the front, keeping its place across it.
			float t = Rules.AcrossOf(bank.GlobalPosition);
			float limit = BridgeBand.HalfWidthAt(front + BankLead) * 0.33f;
			bank.GlobalPosition = Rules.WorldAt(front + BankLead, Mathf.Clamp(t, -limit, limit));
		}
	}

	private void Respawn(ref Block b, float age01)
	{
		b.T = rng.RandfRange(-1f, 1f);
		b.Life = rng.RandfRange(0.8f, 1.6f);
		b.Age = age01 * b.Life;
		b.Size = rng.RandfRange(4f, 9f);
		b.Tone = rng.RandiRange(0, 2);
		b.Drift = rng.RandfRange(-6f, 6f);
	}

	// --- Drawing ------------------------------------------------------------------------------------

	// The stone row (bonelight.py): lit, base, shade, deep, and occlusion.
	private static readonly Color StoneLit = new("#2A3444");
	private static readonly Color StoneBase = new("#1E2634");
	private static readonly Color StoneShade = new("#141A26");
	private static readonly Color StoneDeep = new("#0B0F18");
	private static readonly Color Occ = new("#05070C");
	private static readonly Color StoneHi = new("#3A4658");
	private static readonly Color[] BlockTones = { StoneLit, StoneBase, StoneLit };

	private readonly Vector2[] quad = new Vector2[4];
	// The broken edge and one row of the face under it, reused while the deck's width is the same.
	private Vector2[] edge = System.Array.Empty<Vector2>();
	private Vector2[] row = System.Array.Empty<Vector2>();
	private readonly Vector2[] quadUv = new Vector2[4];
	private readonly Color[] quadColour = { Colors.White, Colors.White, Colors.White, Colors.White };

	// How far the fallen region reaches behind the front and across the bridge: past any screen.
	private const float FallenDepth = 3200f;
	private const float FallenHalfWidth = 1800f;
	// The broken end of the deck: its face, seen hanging into the drop, and the ragged line it broke along.
	private const float FaceHeight = 24f;
	private const float RaggedStep = 12f;

	public override void _Draw()
	{
		if (Rules == null || !Rules.FrontMoving || Rules.Game == null)
			return;
		float front = Rules.FrontS;
		Vector2 at = GlobalPosition;

		// The drop over the fallen deck: the abyss's own surface, tiled in world space from the world origin
		// as the water shader tiles it (one texel a world pixel), so it meets the abyss beside the bridge.
		if (abyss != null)
		{
			Vector2 size = abyss.GetSize();
			SetQuad(Rules.WorldAt(front - FallenDepth, -FallenHalfWidth), Rules.WorldAt(front, -FallenHalfWidth),
				Rules.WorldAt(front, FallenHalfWidth), Rules.WorldAt(front - FallenDepth, FallenHalfWidth), at);
			for (int i = 0; i < 4; i++)
				quadUv[i] = (quad[i] + at) / size;
			DrawPolygon(quad, quadColour, quadUv, abyss);
		}

		// The broken end, across the deck's width at the front (and a little past each shore, under the
		// parapet's last stones). Ragged along the line in 12 px steps, from a hash of where each step is
		// across the bridge - so the edge is the same shape as it moves, as a real break would be.
		float half = BridgeBand.HalfWidthAt(front) + 16f;
		if (half <= 16f)
			return;
		int steps = Mathf.CeilToInt(2f * half / RaggedStep);
		if (edge.Length != steps + 1)
			edge = new Vector2[steps + 1];
		if (row.Length != steps + 1)
			row = new Vector2[steps + 1];
		for (int i = 0; i <= steps; i++)
		{
			float t = -half + i * RaggedStep;
			int k = Mathf.FloorToInt(t / RaggedStep);
			edge[i] = Rules.WorldAt(front - Hash01(k, 11) * 10f, t) - at;
		}
		// The face: from the edge straight down the screen into the drop, lit at its top course and fading
		// through the deep stop to occlusion - one line of the face per pixel row, the edge moved down. Lines,
		// not polygons: a strip a few pixels tall along a ragged line is exactly what triangulation trips on.
		for (int y = 0; y < (int)FaceHeight; y++)
		{
			Color tone = y < 2 ? StoneLit : y < 6 ? StoneBase : y < 12 ? StoneShade : y < 17 ? StoneDeep : Occ;
			for (int i = 0; i <= steps; i++)
				row[i] = edge[i] + new Vector2(0f, y + 0.5f);
			DrawPolyline(row, tone, 1f);
		}
		// A joint every other step: the setts' ends in the face.
		for (int i = 0; i < steps; i += 2)
		{
			Vector2 m = (edge[i] + edge[i + 1]) * 0.5f;
			DrawRect(new Rect2(Mathf.Round(m.X), Mathf.Round(m.Y) + 3f, 1f, 6f), StoneDeep);
		}

		// The masonry falling away: blocks leaving the edge, dropping down the screen into the mist, smaller
		// and darker as they go.
		for (int i = 0; i < blocks.Length; i++)
		{
			ref Block blk = ref blocks[i];
			float u = blk.Age / blk.Life;
			Vector2 p = Rules.WorldAt(front - 6f, blk.T * (half - 8f)) - at;
			p += new Vector2(blk.Drift * blk.Age, FaceHeight * 0.4f + 140f * u * u);
			float size = Mathf.Max(1f, Mathf.Round(blk.Size * (1f - 0.6f * u)));
			Color tone = u < 0.45f ? BlockTones[blk.Tone] : u < 0.8f ? StoneDeep : Occ;
			var r = new Rect2(Mathf.Round(p.X), Mathf.Round(p.Y), size, size);
			DrawRect(r, tone);
			if (size >= 3f && u < 0.45f)
				DrawRect(new Rect2(r.Position, new Vector2(size, 1f)), StoneHi);
		}
	}

	private void SetQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector2 at)
	{
		quad[0] = a - at;
		quad[1] = b - at;
		quad[2] = c - at;
		quad[3] = d - at;
	}

	private static float Hash01(int k, int salt) =>
		(LevelGenerator.Hash(0x5EED, k, 0, salt) >> 11) * (1.0f / (1UL << 53));
}
