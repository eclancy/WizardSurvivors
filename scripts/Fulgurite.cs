using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// A5, THE FULGURITE - the Scorched Sands' spell vault (.ai/achievements-and-secrets.md 8.9). It pays
// Stormsweep, the lash of wind and lightning - learned by standing beside the glass the lightning made
// while a dry storm comes down on it.
//
// SEES. From two chunks out, beads of fused sand-glass lie in the dunes, commoner toward one place, and
// from a chunk out little branches of the glass stand out of the sand (its FarTell). Then the place: a
// branching tree of glass standing in the dunes, the shape of the bolt that made it, with the warm glint in
// its root - and over it, always, a cloud: the only one in the Sands' sky.
//
// DOES. Storm, a kind of hour: a cycle of its own on the pace clock (Node2DGame.PaceSeconds, so a shortened
// run keeps its rhythm). Every StormPeriod pace seconds a dry storm comes over: for GatherSeconds the cloud
// gathers - spreads, darkens, flickers - and then StrikeCount bolts come down round the glass a second apart,
// each telegraphed on the ground first (TelegraphedGroundHit, the game's one warning placed away from its
// cause: StrikeDamage to him if he is under it, and the same to every enemy under it). Two of them come down
// where he is standing when they are called. Stand within HoldRadius of the glass through the strikes - he
// may move inside it, and must, to step out from under them - and the glass draws the last bolt into
// itself and rings. Leave the circle once and the last bolt lands in the sand like the rest; the next
// storm comes round. Arriving late costs a wait, never the secret.
//
// PAYS. Ringing, the light comes up out of its root and goes to him (SecretVault.Open): Stormsweep the first
// time ever, granted and saved at once and offered from the next level-up; in a later run a chest from the
// secret budget. Its foot is solid, as the Geode Heart's is. It stands down with the boss: no storm comes.
//
// Art: tools/art/secret_sands.py (the glass in three states, the beads and twigs). The cloud, the bolts and
// the light are stamped cells on the world's grid (PixelInk, VaultLight); the warnings are the shared
// enemy telegraph, which is the one drawing the doc names.
public partial class Fulgurite : SecretVault
{
	/// <summary>Pace seconds from one storm to the next (8.9: ninety), and when the first comes.</summary>
	public const float StormPeriod = 90f;
	public const float FirstStorm = 60f;

	/// <summary>The cloud gathers this long before the first bolt (8.9: ten seconds).</summary>
	public const float GatherSeconds = 10f;

	/// <summary>Bolts a storm, a second apart (8.9: six over six seconds); the last is drawn into the glass.</summary>
	public const int StrikeCount = 6;
	public const float StrikeGap = 1f;

	/// <summary>Each bolt's warning: how long, how wide, and what it does (8.9: 3 damage, enemies too).</summary>
	public const float StrikeWindUp = 0.9f;
	public const float StrikeRadius = 52f;
	public const int StrikeDamage = 3;

	/// <summary>Stand within this of the glass through the strikes (8.9: 120 px).</summary>
	public const float HoldRadius = 120f;

	/// <summary>The storm's whole length: the gather, the bolts, the last one landing.</summary>
	public const float StormSeconds = GatherSeconds + (StrikeCount - 1) * StrikeGap + StrikeWindUp + 0.1f;

	public const float FootRadius = 40f;

	private const string ArtDir = "res://assets/bonelight/world/sands/";
	// tools/art/secret_sands.py: a 64 x 96 frame with its root at (32, 88), at x2.
	private static readonly Vector2 SheetAnchor = new(-64f, -176f);
	private static readonly Vector2 RootOffset = new(0f, -2f);
	public const float BeadFoot = 5f;

	private static readonly SecretFarTell Tell = new()
	{
		Textures = new[] { ArtDir + "glass-beads-1.png", ArtDir + "glass-beads-2.png", ArtDir + "glass-beads-3.png" },
		NearTextures = new[] { ArtDir + "glass-twig-1.png", ArtDir + "glass-twig-2.png" },
		InnerRadius = SecretCatalog.VaultFootprint,
		NearRadius = 1536f,
		OuterRadius = 3072f,
		PeakPerChunk = 50,
		NearShare = 0.6f,
		FootFromBottom = BeadFoot,
	};

	public override SecretFarTell FarTell => Tell;

	public override IEnumerable<(Vector2 Centre, float Radius)> SolidFootprints => new[] { (Position, FootRadius) };

	public override Vector2 TellPosition => GlobalPosition + RootOffset;

	/// <summary>Raised as each bolt is called (where it will land) and as it lands (where, and how many enemies it hit).</summary>
	public event Action<Vector2> StrikeCalled;
	public event Action<Vector2, int> StrikeLanded;

	/// <summary>True while a storm is on (gathering or striking).</summary>
	public bool StormOn => stormIndex >= 0;

	/// <summary>True while he has stayed within HoldRadius since the strikes began (this storm).</summary>
	public bool Held { get; private set; }

	/// <summary>True once the glass has rung this run.</summary>
	public bool HasRung => ringClock >= 0f || IsOpened;

	/// <summary>Seconds into the current storm, or -1. For the probe.</summary>
	public float StormSecond { get; private set; } = -1f;

	private Sprite2D glass;
	private Node2D effects;
	private float clock;
	private int stormIndex = -1;
	private int called;
	private float ringClock = -1f;
	private readonly VaultLight light = new();
	private readonly List<Strike> strikes = new();
	// For _UiShot's poses only: added to the pace clock so a storm can be stood in at the shutter.
	private float probeShift;

	private sealed class Strike
	{
		public Vector2 At;
		public float Called;
		public bool Landed;
		public bool IntoGlass;
		public float Seed;
	}

	public override void _Ready()
	{
		string path = ArtDir + "fulgurite.png";
		Texture2D sheet = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		if (sheet == null)
			GD.PushError($"Fulgurite: {path} is missing; re-run python tools/art/secret_sands.py, then import.");
		// Sorted among the bodies at its root, as the Geode Heart is.
		glass = new Sprite2D { Name = "Glass", Texture = sheet, Hframes = 3, Centered = false, Offset = SheetAnchor };
		AddChild(glass);
		var body = new StaticBody2D
		{
			Name = "Foot",
			CollisionLayer = 1u << (LevelTilePainter.TerrainCollisionLayer - 1),
			CollisionMask = 0,
		};
		body.AddChild(new CollisionShape2D { Position = new Vector2(0f, -2f), Shape = new RectangleShape2D { Size = new Vector2(30f, 12f) } });
		AddChild(body);
		// The cloud, the bolts and the light are in the air, over everything, on the world's grid.
		effects = new Node2D { Name = "Sky", ZIndex = DrawLayers.Air, ZAsRelative = false, TopLevel = true };
		effects.Draw += DrawSky;
		AddChild(effects);
		effects.GlobalPosition = Vector2.Zero;
	}

	public override void _ExitTree()
	{
		if (effects != null && IsInstanceValid(effects))
			effects.Draw -= DrawSky;
		base._ExitTree();
	}

	private float Pace => (Director?.Game?.PaceSeconds ?? 0f) + probeShift;

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		if (ringClock >= 0f)
		{
			ringClock += dt;
			if (ringClock >= 0.6f && ringClock - dt < 0.6f)
				light.Release(GlobalPosition + RootOffset);
		}
		if (light.Tick(dt))
			// Rewards in flight are paid even if the boss has come meanwhile (stage-secrets section 4).
			Open(GlobalPosition + new Vector2(0f, 90f));

		RunStorm();
		LandStrikes();
		if (glass != null)
			glass.Frame = ringClock >= 0f && ringClock < 3f ? 2 : (StormOn && StormSecond >= GatherSeconds - 2f ? 1 : 0);

		Player player = Player;
		if (player != null && player.GlobalPosition.DistanceSquaredTo(GlobalPosition) < 1800f * 1800f)
			effects.QueueRedraw();
	}

	// Where in the cycle the pace clock is: a storm runs from each FirstStorm + k * StormPeriod for StormSeconds.
	private void RunStorm()
	{
		float pace = Pace;
		int k = (int)MathF.Floor((pace - FirstStorm) / StormPeriod);
		float s = pace - FirstStorm - k * StormPeriod;
		bool on = pace >= FirstStorm && s < StormSeconds && Active && !IsOpened;
		if (!on)
		{
			// Bolts already called still land (LandStrikes); only the calling stops.
			if (stormIndex >= 0)
				EndStorm();
			StormSecond = -1f;
			return;
		}
		if (k != stormIndex)
		{
			stormIndex = k;
			called = 0;
			Held = false;
			strikes.Clear();
		}
		StormSecond = s;
		Player player = Player;
		bool inside = player != null && player.FootPosition.DistanceTo(GlobalPosition) <= HoldRadius;
		// The hold starts with the first bolt and lasts through the last: leaving once breaks it.
		if (s >= GatherSeconds && called == 0)
			Held = inside;
		else if (s >= GatherSeconds && !inside)
			Held = false;

		while (called < StrikeCount && s >= GatherSeconds + called * StrikeGap)
			Call(called++, player);
	}

	private void EndStorm()
	{
		stormIndex = -1;
		Held = false;
	}

	// One bolt called down: its warning on the ground now, the bolt on it StrikeWindUp later. Two of them
	// come where he stands; the rest round the glass on the storm's own pattern; the last, if he has held
	// the circle, is drawn into the glass and has no warning, because it lands on nothing of his.
	private void Call(int i, Player player)
	{
		float seed = stormIndex * 7.31f + i * 1.93f;
		var strike = new Strike { Called = clock, Seed = seed };
		if (i == StrikeCount - 1 && Held)
		{
			strike.At = GlobalPosition + new Vector2(0f, -150f);
			strike.IntoGlass = true;
		}
		else
		{
			bool atHim = (i == 1 || i == 3) && player != null && player.FootPosition.DistanceTo(GlobalPosition) <= HoldRadius * 1.6f;
			if (atHim)
				strike.At = player.FootPosition;
			else
			{
				// Alternately just inside the circle and out in the dunes round it.
				float a = seed * 2.4f;
				float r = i % 2 == 0 ? 60f + 40f * Frac(seed * 3.7f) : 160f + 110f * Frac(seed * 5.3f);
				strike.At = GlobalPosition + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
			}
			// The game's warning, placed away from what caused it: it hits him if he is under it.
			TelegraphedGroundHit.Place(Director.Game, strike.At, StrikeWindUp, StrikeRadius, StrikeDamage, BoltWarning);
		}
		strikes.Add(strike);
		StrikeCalled?.Invoke(strike.At);
	}

	private static float Frac(float v) => v - MathF.Floor(v);

	// The bolts land when their warnings do: on every enemy under one, and the last into the glass.
	private void LandStrikes()
	{
		foreach (Strike strike in strikes)
		{
			if (strike.Landed || clock - strike.Called < StrikeWindUp)
				continue;
			strike.Landed = true;
			if (strike.IntoGlass)
			{
				Ring();
				StrikeLanded?.Invoke(strike.At, 0);
				continue;
			}
			int hit = 0;
			float r2 = StrikeRadius * StrikeRadius;
			foreach (Enemy enemy in EnemyRegistry.Snapshot)
			{
				if (enemy == null || !IsInstanceValid(enemy) || enemy.IsDying || !enemy.IsTargetable)
					continue;
				if (enemy.GlobalPosition.DistanceSquaredTo(strike.At) > r2)
					continue;
				enemy.TakeDamage(StrikeDamage, false);
				hit++;
			}
			SfxPlayer.Impact(Element.Lightning, strike.At);
			StrikeLanded?.Invoke(strike.At, hit);
		}
	}

	// The last bolt went into it: it rings, and the light comes up out of its root.
	private void Ring()
	{
		if (ringClock >= 0f || IsOpened)
			return;
		ringClock = 0f;
		SfxPlayer.AtPosition(SfxCatalog.SpellStormsweep, GlobalPosition, 0f);
	}

	/// <summary>
	/// For _UiShot's photographs (--secret-stage=N), never in play: 1 the cloud gathering, 2 a bolt coming
	/// down, 3 the last bolt about to be called - drawn into the glass, the ring, the light and the pay, if he
	/// stands inside the circle (photograph it with --goto-offset inside HoldRadius). It shifts the storm's own
	/// clock; the run's pace clock is untouched.
	/// </summary>
	public void PoseForProbe(int stage)
	{
		float pace = Director?.Game?.PaceSeconds ?? 0f;
		float want = stage <= 1 ? GatherSeconds * 0.7f : stage == 2 ? GatherSeconds + 2.85f : GatherSeconds + (StrikeCount - 1) * StrikeGap - 0.05f;
		probeShift = FirstStorm + want - pace;
		stormIndex = -1;
	}

	// --- drawing ------------------------------------------------------------------------------------

	private static readonly Color BoltWarning = new(1f, 0.941f, 0.416f);       // lightning mid #FFF06A
	private static readonly Color BCore = new(1f, 1f, 0.941f);                 // lightning core #FFFFF0
	private static readonly Color BHot = new(1f, 0.973f, 0.690f);              // lightning hot #FFF8B0
	private static readonly Color BEdge = new(0.878f, 0.690f, 0.165f);         // lightning edge #E0B02A
	private static readonly Color CloudDeep = new(0.078f, 0.063f, 0.122f);     // violet deep #14101F
	private static readonly Color CloudShade = new(0.133f, 0.110f, 0.200f);    // violet shade #221C33
	private static readonly Color CloudBase = new(0.204f, 0.173f, 0.298f);     // violet base #342C4C
	private static readonly Color CloudLit = new(0.306f, 0.267f, 0.439f);      // violet lit #4E4470

	// Where the cloud hangs: high over the glass, so it sits above the tree's crown.
	private static readonly Vector2 CloudOffset = new(0f, -290f);

	private void DrawSky()
	{
		Vector2 foot = GlobalPosition;
		// The near tell: the warm glint in the root, until the light has left it.
		if (!HasRung)
			VaultLight.DrawGlint(effects, foot + RootOffset + new Vector2(0f, -4f), clock);

		// The cloud: a few puffs over the glass when the sky is still, more and darker as a storm gathers, a
		// flicker of the bolt's light inside it before the strikes.
		float gather = StormOn ? Mathf.Clamp(StormSecond / GatherSeconds, 0f, 1f) : 0f;
		int puffs = 5 + (int)(6 * gather);
		Vector2 sky = foot + CloudOffset;
		for (int k = 0; k < puffs; k++)
		{
			float drift = MathF.Sin(clock * 0.25f + k * 1.7f) * 10f;
			float x = (k - (puffs - 1) * 0.5f) * (40f + 6f * gather) + drift;
			float y = (k % 3 - 1) * 8f + MathF.Sin(k * 2.3f) * 5f;
			float r = 22f + 8f * Frac(k * 0.618f) + 8f * gather;
			Vector2 c = sky + new Vector2(x, y);
			// A flat puff, wider than tall: dark underneath, its body, and lit along its top on the key's side.
			Puff(c + new Vector2(0f, 4f), r * 1.7f, r * 0.8f, gather > 0.5f ? CloudDeep : CloudShade);
			Puff(c, r * 1.6f, r * 0.7f, gather > 0.5f ? CloudShade : CloudBase);
			Puff(c + new Vector2(-r * 0.4f, -r * 0.42f), r * 0.9f, r * 0.26f, gather > 0.7f ? CloudBase : CloudLit);
		}
		if (StormOn && gather > 0.6f && Frac(clock * 1.7f) < 0.12f)
		{
			// Lightning inside the cloud.
			Vector2 at = sky + new Vector2(MathF.Sin(clock * 3.1f) * 60f, 6f);
			PixelInk.Line(effects, at, at + new Vector2(14f, 8f), BHot, 1);
			PixelInk.Line(effects, at + new Vector2(14f, 8f), at + new Vector2(6f, 18f), BHot, 1);
		}

		// The bolts: from the cloud's underside to where they land, for a few frames after they land.
		foreach (Strike strike in strikes)
		{
			if (!strike.Landed)
				continue;
			float age = clock - strike.Called - StrikeWindUp;
			if (age < 0f || age > 0.22f)
				continue;
			float cover = 1f - age / 0.22f;
			DrawBolt(sky + new Vector2(MathF.Sin(strike.Seed) * 50f, 30f), strike.At, strike.Seed, cover, strike.IntoGlass);
			VaultLight.DrawThrow(effects, strike.At, age / 0.22f, 8, strike.IntoGlass ? 28f : 22f, BHot, BEdge, strike.Seed);
		}

		// Ringing: sparks of its light thrown off the glass.
		if (ringClock >= 0f && ringClock < 1.2f)
			VaultLight.DrawThrow(effects, foot + new Vector2(0f, -90f), ringClock / 1.2f, 16, 110f, BCore, BHot, 3.3f);

		if (light.InFlight && Player is Player player)
			light.Draw(effects, player.GlobalPosition + new Vector2(0f, -40f));
	}

	// An ellipse of art pixels, a row a rect, on the world's grid (PixelInk.Disc is a circle).
	private void Puff(Vector2 centre, float rx, float ry, Color color)
	{
		float g = PixelInk.Grid;
		int cy = PixelInk.CellOf(centre.Y), cx = PixelInk.CellOf(centre.X);
		int rows = Math.Max(1, (int)MathF.Round(ry / g));
		for (int y = -rows; y <= rows; y++)
		{
			float t = y / (float)rows;
			int half = (int)MathF.Round(rx / g * MathF.Sqrt(MathF.Max(0f, 1f - t * t)));
			effects.DrawRect(new Rect2((cx - half) * g, (cy + y) * g, (2 * half + 1) * g, g), color);
		}
	}

	// A bolt as pixel art (the Chain Lightning pass's way): a jagged path stamped on the grid, an under-stroke of
	// the ramp's gold edge two cells wider than a seam of hot light, white at the strike.
	private void DrawBolt(Vector2 from, Vector2 to, float seed, float cover, bool white)
	{
		const int Segments = 7;
		Vector2 along = to - from;
		Vector2 side = new Vector2(-along.Y, along.X).Normalized();
		Vector2 prev = from;
		for (int s = 1; s <= Segments; s++)
		{
			float t = s / (float)Segments;
			float jag = s == Segments ? 0f : (Frac(MathF.Sin(seed * 12.9f + s * 78.2f) * 437.5f) - 0.5f) * 44f;
			Vector2 next = from + along * t + side * jag;
			PixelInk.Line(effects, prev, next, BEdge, 3, cover);
			PixelInk.Line(effects, prev, next, white ? BCore : BHot, 1, cover);
			prev = next;
		}
		PixelInk.Block(effects, to, 3, BCore, cover);
	}
}
