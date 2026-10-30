using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// R1, THE BROKEN WARD - the Mystic Ruins' spell vault (.ai/achievements-and-secrets.md 8.7). It pays Kindled
// Ward, a made light that keeps the dark off him - learned by making an old ward stand again.
//
// SEES. From two chunks out the ruins' rubble gives way to broken pieces of ward stone, a notch or two cut
// in each, commoner toward one place, and from a chunk out the last of the light is still in some of
// their notches (its FarTell). Then the place: five standing stones in a ring round a flat heart-stone, each
// stone cut with one to five notches, all of them dark - and in the heart-stone's cup, where the ward's
// light was, the warm glint breathing.
//
// DOES. Sequence: touch the stones in the order of their notches - one, two, three, four, five. Each right
// stone lights its notches with the ward's light. A wrong one dims them all to embers, and it starts again
// (a mistake resets, never locks: .ai/stage-secrets.md section 8). The order round the ring is the seed's,
// so it is never simply clockwise. He touches a stone by walking up to it: the stones are solid - bodies on
// the ground's physics layer, stamped into the navigation flags - so the horde routes round them.
//
// PAYS. The fifth lights the heart and the ward stands again for WardSeconds: a wall of its light on the
// ring, and whatever tries to come in is thrown back out at it (Enemy.ApplyKnockback, so a boss's
// resistance holds). Then it breaks again, from the inside, and its light leaves the ring and goes to him
// (SecretVault.Open): Kindled Ward the first time ever, granted and saved at once and offered from the next
// level-up; in a later run the same thirty seconds and a chest from the secret budget. It stands down with
// the boss: the ward breaks at once and the light still goes to him.
//
// THE RUINS' WALLS. The stones are laid on open ground through the placer (LayOut tests every stone's spot,
// turning the ring until all five are clear), so broken walls in the ruins can never stand where a stone
// must. The ward's own footprint is the director's ordinary check.
//
// Art: tools/art/secret_ruins.py (the stones, the heart, the fragments). The wall of light, the sparks and
// the light that goes to him are stamped cells on the world's grid (PixelInk, VaultLight).
public partial class BrokenWard : SecretVault
{
	/// <summary>The stones' feet stand on this ring round the middle.</summary>
	public const float RingRadius = 190f;

	/// <summary>The ward's wall: an enemy whose feet come inside it is thrown back out.</summary>
	public const float WardRadius = 214f;

	/// <summary>How long the ward stands (8.7: thirty seconds).</summary>
	public const float WardSeconds = 30f;

	/// <summary>His feet this near a stone's foot touch it: he walks up against it.</summary>
	public const float TouchRadius = 50f;

	/// <summary>The throw at the wall, px/s, as a knockback (a boss's resistance takes its share).</summary>
	public const float PushSpeed = 460f;

	public const int StoneCount = 5;

	private const string ArtDir = "res://assets/bonelight/world/ruins/";
	// tools/art/secret_ruins.py: a stone's frame is 26 x 48 art pixels with its foot on row 43, so at x2 the
	// foot is 2 * (48 - 43) - 1 texture pixels up from the bottom; a fragment's FRAG_MARGIN is 4.
	private const float StoneFootFromBottom = 9f;
	public const float FragmentFoot = 7f;
	// The stone's body is a 30 x 14 foot; what is stamped solid in the navigation grid is wider, because a
	// cell is stamped only if its middle is inside the circle and a cell is 48 px: 34 always takes the cell
	// the foot stands in (the Geode Heart stamps 56 for the same reason).
	private const float StoneBodyRadius = 34f;

	private static readonly SecretFarTell Tell = new()
	{
		Textures = new[] { ArtDir + "ward-fragment-1.png", ArtDir + "ward-fragment-2.png", ArtDir + "ward-fragment-3.png" },
		NearTextures = new[] { ArtDir + "ward-fragment-warm-1.png", ArtDir + "ward-fragment-warm-2.png" },
		InnerRadius = SecretCatalog.VaultFootprint,
		NearRadius = 1536f,
		OuterRadius = 3072f,
		PeakPerChunk = 44,
		NearShare = 0.55f,
		FootFromBottom = FragmentFoot,
	};

	public override SecretFarTell FarTell => Tell;

	// Where each stone stands from the middle, and how many notches it has: the seed's (LayOut).
	private readonly Vector2[] stoneAt = new Vector2[StoneCount];
	private readonly int[] notches = new int[StoneCount];
	private bool laidOut;

	public override IEnumerable<(Vector2 Centre, float Radius)> SolidFootprints
	{
		get
		{
			EnsureLayout();
			var feet = new (Vector2, float)[StoneCount];
			for (int i = 0; i < StoneCount; i++)
				feet[i] = (Position + stoneAt[i], StoneBodyRadius);
			return feet;
		}
	}

	/// <summary>How many stones are lit in order this run (0 to 5). Public for the probe.</summary>
	public int Lit => State?.Step ?? 0;

	/// <summary>The ward's own run state: Progress its seconds standing, Step 1 once formed, Done once broken again.</summary>
	private SecretDirector.SiteState Ward => Director?.StateOf(Key + "/ward");

	public bool WardStanding => Ward is { Step: 1, Done: false };

	public bool WardBroken => Ward?.Done == true;

	/// <summary>A stone's foot, in the world, and its notch count. For the probe.</summary>
	public Vector2 StoneFoot(int i) => GlobalPosition + stoneAt[i];

	public int NotchesOf(int i) => notches[i];

	private readonly Sprite2D[] stones = new Sprite2D[StoneCount];
	private Sprite2D heart;
	private Node2D wall;
	private Node2D effects;
	private float clock;
	private float dimClock = -1f;
	private int touching = -1;
	private float lastFlashSound;
	private readonly VaultLight light = new();
	private readonly List<(Vector2 At, float Born)> flashes = new();

	/// <summary>
	/// The ring from the seed: a turn of it and an order of the notch counts round it, every roll drawn
	/// first; then the turn is stepped until all five stones stand on open ground (a wall, water or a fence
	/// never under one), the first clear turn kept.
	/// </summary>
	public override void LayOut(ExplorePlacer placer)
	{
		float turn = placer.NextFloat() * Mathf.Tau;
		int[] order = { 1, 2, 3, 4, 5 };
		float[] rolls = new float[StoneCount];
		for (int i = 0; i < StoneCount; i++)
			rolls[i] = placer.NextFloat();
		// Fisher-Yates on the rolls.
		for (int i = StoneCount - 1; i > 0; i--)
		{
			int j = Math.Min(i, (int)(rolls[i] * (i + 1)));
			(order[i], order[j]) = (order[j], order[i]);
		}
		float chosen = turn;
		for (int k = 0; k < 24; k++)
		{
			// Within one stone's spacing (72 degrees): a turn past it is the same ring again.
			float t = turn + k * Mathf.Tau / (StoneCount * 24f);
			bool clear = true;
			for (int i = 0; i < StoneCount && clear; i++)
				clear = placer.IsClear(Position + RingPoint(t, i), StoneBodyRadius + 10f);
			if (clear)
			{
				chosen = t;
				break;
			}
		}
		for (int i = 0; i < StoneCount; i++)
		{
			stoneAt[i] = RingPoint(chosen, i);
			notches[i] = order[i];
		}
		laidOut = true;
	}

	private static Vector2 RingPoint(float turn, int i)
	{
		float a = turn + i * Mathf.Tau / StoneCount;
		// A circle in the world, as the wall and its push are: the drawn ring and the tested one are one shape.
		return new Vector2(MathF.Cos(a) * RingRadius, MathF.Sin(a) * RingRadius).Round();
	}

	// A ward built without the director's placer (nothing in play does that) stands in the plain order.
	private void EnsureLayout()
	{
		if (laidOut)
			return;
		for (int i = 0; i < StoneCount; i++)
		{
			stoneAt[i] = RingPoint(0f, i);
			notches[i] = i + 1;
		}
		laidOut = true;
	}

	public override void _Ready()
	{
		EnsureLayout();
		// The stones sort among the bodies, each at its own foot.
		YSortEnabled = true;

		Texture2D sheet = LoadArt("ward-stone.png");
		Texture2D heartSheet = LoadArt("ward-heart.png");
		heart = new Sprite2D
		{
			Name = "Heart",
			Texture = heartSheet,
			Hframes = 2,
			ZIndex = DrawLayers.Decal,
			ZAsRelative = false,
		};
		AddChild(heart);
		for (int i = 0; i < StoneCount; i++)
		{
			stones[i] = new Sprite2D
			{
				Name = $"Stone{i}",
				Texture = sheet,
				Hframes = 15,
				Centered = true,
				Position = stoneAt[i],
				Offset = new Vector2(0f, -48f + StoneFootFromBottom),
			};
			AddChild(stones[i]);
			// Solid: on the ground's physics layer, where boots and bodies meet terrain (the Geode Heart's way).
			var body = new StaticBody2D
			{
				Name = $"StoneFoot{i}",
				Position = stoneAt[i],
				CollisionLayer = 1u << (LevelTilePainter.TerrainCollisionLayer - 1),
				CollisionMask = 0,
			};
			body.AddChild(new CollisionShape2D { Position = new Vector2(0f, -4f), Shape = new RectangleShape2D { Size = new Vector2(30f, 14f) } });
			AddChild(body);
		}

		// The wall lies on the floor under the bodies; the sparks and the light fly over them. Both on the
		// world's grid (TopLevel at the origin, as PixelInk asks).
		wall = new Node2D { Name = "Wall", ZIndex = DrawLayers.GroundTell, ZAsRelative = false, TopLevel = true };
		wall.Draw += DrawWall;
		AddChild(wall);
		wall.GlobalPosition = Vector2.Zero;
		effects = new Node2D { Name = "Light", ZIndex = DrawLayers.Air, ZAsRelative = false, TopLevel = true };
		effects.Draw += DrawEffects;
		AddChild(effects);
		effects.GlobalPosition = Vector2.Zero;
		ShowFrames();
	}

	private static Texture2D LoadArt(string file)
	{
		string path = ArtDir + file;
		Texture2D texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		if (texture == null)
			GD.PushError($"BrokenWard: {path} is missing; re-run python tools/art/secret_ruins.py, then import.");
		return texture;
	}

	public override void _ExitTree()
	{
		if (wall != null && IsInstanceValid(wall))
			wall.Draw -= DrawWall;
		if (effects != null && IsInstanceValid(effects))
			effects.Draw -= DrawEffects;
		base._ExitTree();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		if (dimClock >= 0f)
		{
			dimClock += dt;
			if (dimClock > 0.7f)
				dimClock = -1f;
		}
		for (int i = flashes.Count - 1; i >= 0; i--)
			if (clock - flashes[i].Born > 0.4f)
				flashes.RemoveAt(i);

		SecretDirector.SiteState ward = Ward;
		if (ward != null && ward.Step == 1 && !ward.Done)
		{
			// The ward stands its thirty seconds - or breaks at once when the boss comes.
			ward.Progress += dt;
			if (ward.Progress >= WardSeconds || !Active)
				BreakWard();
		}
		else if (Active && !IsOpened && Lit < StoneCount)
			WatchTouches();

		if (light.Tick(dt))
			// Rewards in flight are paid even if the boss has come meanwhile (stage-secrets section 4).
			Open(GlobalPosition + new Vector2(0f, 70f));

		ShowFrames();
		Player player = Player;
		if (player != null && player.GlobalPosition.DistanceSquaredTo(GlobalPosition) < 1600f * 1600f)
		{
			wall.QueueRedraw();
			effects.QueueRedraw();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!WardStanding)
			return;
		// The wall: whatever has its feet inside is thrown back out across it, a little harder the deeper
		// in it is (a body inside when it rose goes out as fast as one that walked into it).
		Vector2 c = GlobalPosition;
		float r2 = WardRadius * WardRadius;
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (enemy == null || !IsInstanceValid(enemy) || enemy.IsDying || !enemy.IsTargetable)
				continue;
			Vector2 at = enemy.GlobalPosition;
			float d2 = at.DistanceSquaredTo(c);
			if (d2 > r2)
				continue;
			float d = MathF.Sqrt(d2);
			Vector2 outward = d > 1f ? (at - c) / d : Vector2.Down;
			float depth = 1f - d / WardRadius;
			enemy.ApplyKnockback(outward * PushSpeed * (1f + depth));
			if (WardRadius - d < 24f)
				Flash(c + outward * WardRadius);
		}
	}

	private void Flash(Vector2 at)
	{
		if (flashes.Count >= 10 || (flashes.Count > 0 && clock - flashes[^1].Born < 0.08f))
			return;
		flashes.Add((at, clock));
		if (clock - lastFlashSound > 0.3f)
		{
			lastFlashSound = clock;
			SfxPlayer.Impact(Element.Light, at);
		}
	}

	// He touches a stone by walking up to it: the moment his feet come within reach, once, until he leaves.
	private void WatchTouches()
	{
		Player player = Player;
		if (player == null)
			return;
		Vector2 feet = player.FootPosition;
		int near = -1;
		for (int i = 0; i < StoneCount; i++)
		{
			if (feet.DistanceSquaredTo(StoneFoot(i)) < TouchRadius * TouchRadius)
			{
				near = i;
				break;
			}
		}
		if (near != touching && near >= 0)
			Touch(near);
		touching = near;
	}

	/// <summary>A stone touched: the next in the notches' order lights; one already lit does nothing; any other dims them all.</summary>
	public void Touch(int i)
	{
		SecretDirector.SiteState state = State;
		if (state == null || state.Step >= StoneCount)
			return;
		int n = notches[i];
		if (n <= state.Step)
			return;
		if (n == state.Step + 1)
		{
			state.Step++;
			SfxPlayer.Cast(Element.Light, StoneFoot(i));
			if (state.Step >= StoneCount)
				FormWard();
			return;
		}
		// Wrong: they go to embers and out. Start again.
		state.Step = 0;
		dimClock = 0f;
		SfxPlayer.Impact(Element.Earth, StoneFoot(i));
	}

	private void FormWard()
	{
		SecretDirector.SiteState ward = Ward;
		if (ward == null || ward.Step == 1)
			return;
		ward.Step = 1;
		ward.Progress = 0f;
		SfxPlayer.AtPosition(SfxCatalog.SpellEvolve, GlobalPosition, 0f);
	}

	// Its time is up (or the boss has come): it breaks from the inside, and the light goes to him.
	private void BreakWard()
	{
		SecretDirector.SiteState ward = Ward;
		if (ward == null || ward.Done)
			return;
		ward.Done = true;
		light.Release(GlobalPosition + new Vector2(0f, -4f));
		SfxPlayer.Impact(Element.Light, GlobalPosition);
	}

	private void ShowFrames()
	{
		bool standing = WardStanding;
		bool spent = WardBroken;
		for (int i = 0; i < StoneCount; i++)
		{
			if (stones[i] == null)
				continue;
			int n = notches[i];
			int state = !spent && (standing || n <= Lit) ? 1 : (dimClock >= 0f ? 2 : 0);
			stones[i].Frame = (n - 1) * 3 + state;
		}
		if (heart != null)
			heart.Frame = standing ? 1 : 0;
	}

	/// <summary>
	/// For _UiShot's photographs (--secret-stage=N), never in play: 1 two stones lit, 2 a wrong stone just
	/// touched (the embers), 3 the ward standing, 4 the ward breaking - the light, and the pay.
	/// </summary>
	public void PoseForProbe(int stage)
	{
		SecretDirector.SiteState state = State;
		if (state == null)
			return;
		if (stage <= 1)
		{
			state.Step = stage <= 0 ? 0 : 2;
			return;
		}
		if (stage == 2)
		{
			state.Step = 0;
			dimClock = 0f;
			return;
		}
		state.Step = StoneCount;
		FormWard();
		if (stage >= 4)
			Ward.Progress = WardSeconds - 0.01f;
	}

	// --- drawing ------------------------------------------------------------------------------------

	// The ward's light: Light's ramp (.ai/art-direction.md section 3), Kindled Ward's own.
	private static readonly Color LCore = new(1f, 1f, 1f);
	private static readonly Color LHot = new(1f, 0.973f, 0.847f);
	private static readonly Color LMid = new(1f, 0.941f, 0.659f);
	private static readonly Color LEdge = new(1f, 0.776f, 0.235f);

	// The wall on the floor: a band of the ward's light on the ring, broken and flickering, with tongues of
	// it standing up along it - densest just after it rises, guttering by dither in its last three seconds.
	private void DrawWall()
	{
		if (!WardStanding)
			return;
		float t = Ward.Progress;
		float rise = Mathf.Clamp(t / 0.5f, 0f, 1f);
		float gutter = Mathf.Clamp((WardSeconds - t) / 3f, 0f, 1f);
		float cover = rise * gutter;
		Vector2 c = GlobalPosition;
		float r = WardRadius * (0.6f + 0.4f * rise);
		int steps = 220;
		for (int k = 0; k < steps; k++)
		{
			float a = k * Mathf.Tau / steps;
			Vector2 p = c + new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r);
			// The band breaks and mends as it flickers.
			float flicker = 0.5f + 0.5f * MathF.Sin(a * 9f + clock * 5.3f) * MathF.Sin(a * 4f - clock * 3.1f);
			PixelInk.Cell(wall, p, flicker > 0.75f ? LHot : LEdge, cover * (0.55f + 0.45f * flicker));
			PixelInk.Cell(wall, p + new Vector2(0f, PixelInk.Grid), LEdge, cover * 0.5f);
		}
		// Tongues: forty short licks of its light standing up from the ring.
		for (int k = 0; k < 40; k++)
		{
			float a = k * Mathf.Tau / 40f + 0.07f * MathF.Sin(k * 1.7f);
			Vector2 p = c + new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r);
			float h = 6f + 10f * (0.5f + 0.5f * MathF.Sin(clock * 6f + k * 2.3f));
			PixelInk.Line(wall, p, p + new Vector2(0f, -h), LMid, 1, cover);
			PixelInk.Cell(wall, p + new Vector2(0f, -h), LCore, cover);
		}
	}

	private void DrawEffects()
	{
		// The near tell: the warm glint in the heart-stone's cup, until the ward rises.
		if (!IsOpened && !WardStanding && !WardBroken)
			VaultLight.DrawGlint(effects, GlobalPosition + new Vector2(0f, -2f), clock, 0.75f + 0.25f * Lit / StoneCount);
		// Where something was thrown back at the wall: a burst of its light.
		foreach (var (at, born) in flashes)
			VaultLight.DrawThrow(effects, at, (clock - born) / 0.4f, 6, 18f, LHot, LEdge, born);
		// Sparks going up off the wall while it stands.
		if (WardStanding)
		{
			float cover = Mathf.Clamp((WardSeconds - Ward.Progress) / 3f, 0f, 1f);
			for (int k = 0; k < 14; k++)
			{
				float life = (clock * 0.8f + k * 0.37f) % 1f;
				float a = k * 2.399f + MathF.Floor(clock * 0.8f + k * 0.37f) * 0.9f;
				Vector2 p = GlobalPosition + new Vector2(MathF.Cos(a) * WardRadius, MathF.Sin(a) * WardRadius)
					+ new Vector2(0f, -10f - 40f * life);
				PixelInk.Cell(effects, p, life < 0.4f ? LHot : LEdge, cover * (1f - life));
			}
		}
		if (light.InFlight && Player is Player player)
			light.Draw(effects, player.GlobalPosition + new Vector2(0f, -40f));
	}
}
