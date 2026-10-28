using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// Chapter 3, the Broken Circle: THE NINTH STONE.
//
// "There were nine stones. There are eight, and one that walks." A standing stone of the moot with a
// split down its face and his mark in the split. It closes the circle on the player.
//
// THE VERB NO OTHER BOSS ASKS: FIND THE WAY OUT. Every seven seconds or so a ring of stone-falls is
// marked round where the player stands, with ONE GAP two stones wide, and the middle of the ring is
// marked too. The ring lands first and its stones STAND where they fell - the circle is closed but
// for the gap - and then the middle falls. Standing still is hit by the middle; running out over
// the stones is hit by the stones; the only way out is the gap, read off the ground in the moment
// the ring is drawn. The Still Warden punishes standing; this asks where to go.
//
// WHY THE STONES STAND, AND WHY THE RING IS QUICKER THAN THE MIDDLE (.ai/campaign.md section 6
// specified 1.6 s to the ring and the middle half a second after it). The player walks 220 px a
// second, and a ring of radius 170 is cleared - stones and all - in about a second in any
// direction. With the ring on a 1.6 s tell he is out over the stones before they land, and with
// stones that only land and vanish he can wait for them, then walk out anywhere: either way the gap
// decides nothing. So the ring closes fast (0.9 s: running out over it lands you under a stone), the
// stones it drops stand as a wall until the middle has fallen, and the middle keeps the generous
// tell (2.2 s from the first frame). The gap is on screen from that first frame, so an early
// chapter's read is long - the ask is only that it is the gap you walk out of.
//
// Between closes it walks slowly at the player, and its own slam is a small ring at its feet for
// anyone who hugs it. Below half health every close is two in a row: the second centred where the
// first was escaped, its gap on another bearing - read twice. Kept to two phases: it is chapter 3.
//
// Built from the shared primitives: TelegraphedGroundHit for every stone and the middle,
// GroundSlamAttack's Ring for its own slam. The standing stones are the one thing of its own - a
// sprite and a footprint on the ground's physics layer (LevelTilePainter.TerrainCollisionLayer),
// which the wizard's boots and every enemy meet, exactly as a tree trunk is (.ai/terrain.md 10),
// and which free themselves. They are parented to the arena, not to the boss, so a boss that dies
// mid-close never deletes a wall the player is already walking round.
public partial class NinthStoneBoss : BossEnemy
{
	/// <summary>Seconds from the start of one close to the start of the next.</summary>
	[Export] public float CloseIntervalSeconds { get; set; } = 7.0f;
	/// <summary>Seconds before the first close, so the arrival banner is read before anything falls.</summary>
	[Export] public float FirstCloseDelaySeconds { get; set; } = 3.0f;

	/// <summary>Radius of the ring of stone-falls, round where the player stood when it began.</summary>
	[Export] public float RingRadius { get; set; } = 170f;
	/// <summary>Places round the ring. <see cref="RingStones"/> of them get a stone; the rest are the gap, together.</summary>
	[Export] public int RingSlots { get; set; } = 10;
	[Export] public int RingStones { get; set; } = 8;
	/// <summary>Reach of each stone's fall - what its warning draws and what it hits.</summary>
	[Export] public float StoneHitRadius { get; set; } = 52f;
	/// <summary>The footprint each fallen stone stands on, which the boots and the swarm meet.</summary>
	[Export] public float StoneBodyRadius { get; set; } = 50f;
	[Export] public int StoneDamage { get; set; } = 4;

	/// <summary>Seconds from the first frame of a close until the ring lands and closes.</summary>
	[Export] public float RingTellSeconds { get; set; } = 0.9f;
	/// <summary>Seconds from the first frame of a close until the middle lands.</summary>
	[Export] public float MiddleTellSeconds { get; set; } = 2.2f;
	/// <summary>Seconds the fallen stones keep standing after the middle has landed.</summary>
	[Export] public float StoneLingerSeconds { get; set; } = 0.6f;

	[Export] public float MiddleRadius { get; set; } = 120f;
	[Export] public int MiddleDamage { get; set; } = 6;

	/// <summary>Below half health a close is followed by a second one at once (phase 1).</summary>
	[Export] public float PhaseTwoCloseIntervalSeconds { get; set; } = 8.0f;
	/// <summary>The second close's gap is at least this far round from the first's.</summary>
	[Export] public float SecondGapMinTurnDegrees { get; set; } = 100f;

	[Export] public Color StoneFallColor { get; set; } = new Color(0.96f, 0.82f, 0.42f);
	[Export] public Color MiddleColor { get; set; } = new Color(1.0f, 0.42f, 0.24f);
	[Export] public Color SlamColor { get; set; } = new Color(0.86f, 0.80f, 0.98f);

	[Export] public string FallenStoneTexturePath { get; set; } = "res://assets/bonelight/enemies/heath-ninth-stone-fallen.png";

	// The yardsticks the tuning is held against (TuningProblems): the player's walk, the width of
	// his boots' footprint (player.tscn, Feet), and the opening that has to stay walkable.
	private const float PlayerWalkSpeed = 220f;
	private const float FeetWidth = 56f;
	private const float MinGapOpening = 110f;

	private sealed class Close
	{
		public Vector2 Centre;
		public float GapBearing;
		public float Elapsed;
		public bool RingLanded;
		public bool MiddleLanded;
		public bool ChainsAnother;
	}

	private readonly List<Close> closes = new();
	private float untilClose;
	private bool doubleClose;
	private Texture2D fallenStoneTexture;

	public override void _Ready()
	{
		base._Ready();
		untilClose = FirstCloseDelaySeconds;
		fallenStoneTexture = ResourceLoader.Exists(FallenStoneTexturePath) ? GD.Load<Texture2D>(FallenStoneTexturePath) : null;
		if (fallenStoneTexture == null)
			GD.PushError($"NinthStoneBoss: no fallen-stone art at '{FallenStoneTexturePath}' - the stones will block unseen. Re-run tools/art/boss_ninth_stone.py and import.");
		foreach (string problem in TuningProblems())
			GD.PushError("NinthStoneBoss: " + problem);
	}

	// Its own slam: a small ring at its feet, for anyone who hugs it between closes.
	protected override GroundSlamAttack BuildSlam() =>
		new(SlamIntervalSeconds, SlamTelegraphSeconds, SlamRadius, SlamDamage, SlamColor);

	protected override float[] BuildPhaseThresholds() => new[] { EnrageHealthFraction };

	// Not the stock enrage. A stone that suddenly ran would be a different fight; this one reads
	// twice instead, which is the same lesson asked harder.
	protected override void OnPhaseEntered(int phase)
	{
		doubleClose = true;
		CloseIntervalSeconds = PhaseTwoCloseIntervalSeconds;
		SetBaseModulate(new Color(0.88f, 0.86f, 1.0f));
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (IsDying)
			return;
		float dt = (float)delta;
		TickCloses(dt);

		untilClose -= dt;
		if (untilClose > 0f)
			return;
		untilClose = CloseIntervalSeconds;
		BeginClose(doubleClose, null);
	}

	private void BeginClose(bool chainsAnother, float? avoidBearing)
	{
		Node2D target = TargetPlayer;
		Node arena = GetParent();
		if (target == null || !IsInstanceValid(target) || arena == null || !IsInstanceValid(arena))
			return;

		float bearing = GD.Randf() * Mathf.Tau;
		if (avoidBearing is float avoid)
		{
			// Somewhere in the far arc from the last gap, so the second read is a new one.
			float turn = Mathf.DegToRad(SecondGapMinTurnDegrees);
			bearing = avoid + turn + GD.Randf() * (Mathf.Tau - 2f * turn);
		}

		var close = new Close
		{
			Centre = target.GlobalPosition,
			GapBearing = bearing,
			ChainsAnother = chainsAnother,
		};
		closes.Add(close);
		PlayAttackAnimation();

		int stoneDamage = ScaleOutgoingDamage(StoneDamage);
		foreach (Vector2 stone in StonePositions(close))
			TelegraphedGroundHit.Place(arena, stone, RingTellSeconds, StoneHitRadius, stoneDamage, StoneFallColor);
		TelegraphedGroundHit.Place(arena, close.Centre, MiddleTellSeconds, MiddleRadius, ScaleOutgoingDamage(MiddleDamage), MiddleColor);
	}

	private void TickCloses(float delta)
	{
		for (int i = closes.Count - 1; i >= 0; i--)
		{
			Close close = closes[i];
			close.Elapsed += delta;

			if (!close.RingLanded && close.Elapsed >= RingTellSeconds)
			{
				close.RingLanded = true;
				StandStones(close);
			}

			if (!close.MiddleLanded && close.Elapsed >= MiddleTellSeconds)
			{
				close.MiddleLanded = true;
				closes.RemoveAt(i);
				// Read twice: the second ring closes round wherever the first was escaped to.
				if (close.ChainsAnother)
					BeginClose(false, close.GapBearing);
			}
		}
	}

	// The stones of one close, in world space: every slot but the gap's. The gap is the run of
	// (RingSlots - RingStones) empty slots centred on the close's bearing.
	private IEnumerable<Vector2> StonePositions(Close close)
	{
		int slots = Mathf.Max(3, RingSlots);
		int gapSlots = Mathf.Clamp(slots - RingStones, 1, slots - 2);
		float step = Mathf.Tau / slots;
		// Slot 0 sits half a gap round from the bearing, so the gap straddles it symmetrically.
		float first = close.GapBearing + step * (gapSlots + 1) * 0.5f;
		for (int k = 0; k < slots - gapSlots; k++)
		{
			float a = first + step * k;
			yield return close.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * RingRadius;
		}
	}

	// The ring has landed: every stone that did not fall on the player stands where it fell, as a
	// wall round the middle with one gap in it, until the middle has landed and a beat after.
	private void StandStones(Close close)
	{
		Node arena = GetParent();
		if (arena == null || !IsInstanceValid(arena))
			return;

		Vector2? feet = TargetPlayer is Player p && IsInstanceValid(p) ? p.FootPosition : null;
		float lifetime = MiddleTellSeconds - RingTellSeconds + StoneLingerSeconds;
		foreach (Vector2 where in StonePositions(close))
		{
			// A stone that landed on him has done its damage and broke; a wall raised round his
			// boots would only trap him inside it.
			if (feet is Vector2 f && f.DistanceTo(where) < StoneBodyRadius + FeetWidth * 0.5f)
				continue;
			arena.AddChild(MakeStandingStone(where, lifetime));
		}
	}

	// One fallen stone: its art standing on the landing point (sorted at its foot, like a tree) and a
	// round footprint on the ground's physics layer. Frees itself; nothing holds a reference to it.
	private Node2D MakeStandingStone(Vector2 ground, float lifetime)
	{
		var node = new Node2D { Position = ground - new Vector2(0f, WorldSprites.SortLift) };
		if (fallenStoneTexture != null)
		{
			Vector2 size = fallenStoneTexture.GetSize();
			node.AddChild(new Sprite2D
			{
				Texture = fallenStoneTexture,
				Centered = false,
				// The art's contact line is its bottom eight rows (boss_ninth_stone.py, FALLEN_FOOT).
				Offset = new Vector2(-Mathf.Round(size.X * 0.5f), WorldSprites.SortLift - size.Y + 8f),
			});
		}
		var body = new StaticBody2D
		{
			Position = new Vector2(0f, WorldSprites.SortLift),
			CollisionLayer = 1u << (LevelTilePainter.TerrainCollisionLayer - 1),
			CollisionMask = 0,
		};
		body.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = StoneBodyRadius } });
		node.AddChild(body);

		// Thrown up out of the ground for a moment, then gone: a squash on arrival, a sink at the end.
		node.Scale = new Vector2(1f, 0.6f);
		Tween tween = node.CreateTween();
		tween.TweenProperty(node, "scale", Vector2.One, 0.12f);
		tween.TweenInterval(Mathf.Max(0.05f, lifetime - 0.3f));
		tween.TweenProperty(node, "modulate:a", 0f, 0.18f);
		tween.TweenCallback(Callable.From(node.QueueFree));
		return node;
	}

	/// <summary>
	/// What would make this fight unfair or pointless, in words. Empty when the numbers hold. Run at
	/// _Ready (each one is an error), and the line RegressionChecks.ValidateBossPattern wants for
	/// this boss is <c>case NinthStoneBoss stone: warnings.AddRange(stone.TuningProblems()); break;</c>.
	/// </summary>
	/// <remarks>
	/// The geometry is the fight, so it is checked as geometry: the gap must be walkable, the rest of
	/// the ring must not be, nothing between the stones and the middle may be safe, and the time
	/// between the ring closing and the middle falling must be enough to walk out of the gap.
	/// </remarks>
	public IEnumerable<string> TuningProblems()
	{
		int slots = RingSlots;
		int gapSlots = slots - RingStones;
		if (slots < 4 || RingStones < 3 || gapSlots < 1 || gapSlots > slots - 2)
		{
			yield return $"{RingStones} stones in {slots} places leaves no gap, or no ring.";
			yield break;
		}

		float step = Mathf.Tau / slots;
		float neighbourChord = 2f * RingRadius * Mathf.Sin(step * 0.5f);
		float gapChord = 2f * RingRadius * Mathf.Sin(step * (gapSlots + 1) * 0.5f);
		float widest = Mathf.Max(StoneBodyRadius, StoneHitRadius);

		if (gapChord - 2f * widest < MinGapOpening)
			yield return $"the gap opens {gapChord - 2f * widest:0} px between its two stones, under the {MinGapOpening:0} px a wizard and the swarm need - there is no way out.";
		if (neighbourChord - 2f * StoneBodyRadius >= FeetWidth * 0.7f)
			yield return $"neighbouring stones stand {neighbourChord - 2f * StoneBodyRadius:0} px apart, so the wall has holes a wizard walks through and the gap means nothing.";
		if (RingRadius - StoneBodyRadius - MiddleRadius > 8f)
			yield return $"{RingRadius - StoneBodyRadius - MiddleRadius:0} px between the middle's edge and the stones is safe ground inside the circle.";
		if (RingTellSeconds <= 0f || MiddleTellSeconds <= RingTellSeconds)
			yield return $"the ring lands at {RingTellSeconds}s and the middle at {MiddleTellSeconds}s; the circle must close before the middle falls.";
		if ((MiddleTellSeconds - RingTellSeconds) * PlayerWalkSpeed < MiddleRadius + FeetWidth)
			yield return $"{MiddleTellSeconds - RingTellSeconds:0.00}s between the ring closing and the middle falling is too short to walk out of the gap.";
		if (RingTellSeconds * PlayerWalkSpeed > RingRadius + StoneHitRadius)
			yield return $"a {RingTellSeconds}s ring tell is outrun over the stones in any direction, so the gap decides nothing.";
		if (StoneDamage <= 0 || MiddleDamage <= 0)
			yield return "a close deals no damage, so it is scenery.";
		if (CloseIntervalSeconds <= MiddleTellSeconds + StoneLingerSeconds)
			yield return $"a close every {CloseIntervalSeconds}s starts before the last one's stones have gone.";
		if (PhaseTwoCloseIntervalSeconds <= 2f * MiddleTellSeconds + StoneLingerSeconds)
			yield return $"a double close every {PhaseTwoCloseIntervalSeconds}s starts before the last pair has finished.";
	}

	// --- The fall (BossDeath) ---------------------------------------------------------------------
	//
	// "There were nine stones. There are eight, and one that walks." It ends as a stone again. The
	// mark in its split - the thing that made it walk - flares down the length of the split and then
	// LEAVES, a thread of his cold light streaming up out of the stone and away. What is left is only
	// granite on a ball of torn-up earth, and it falls the way a standing stone falls: whole, away from
	// the wizard, with nothing to stop it, onto the heath with a thud that throws up dust, chips and
	// heather. Then it settles into the ground it was pulled out of.
	private BossDeath.Piece deathStone;
	private BossDeath.Piece deathRootBall;
	private float markCarry;

	private const int RootRow = 74;
	private const float StoneTips = 0.55f;
	private const float StoneLands = 1.12f;

	// The split down its face as drawn (boss_ninth_stone.py), crown to root.
	private static readonly Vector2[] Split =
	{
		new(51f, 12f), new(50f, 22f), new(47f, 30f), new(46f, 44f), new(47f, 56f), new(46f, 68f),
	};
	private static readonly Vector2 Mark = new(49f, 38f);
	private static readonly Color HeathDust = new(0.50f, 0.50f, 0.56f, 0.65f);

	protected internal override float DeathSeconds => 2.7f;

	protected internal override void OnDeathBegin(BossDeath death)
	{
		// It falls away from the wizard, over the outer edge of its foot on that side.
		int foot = death.ArtAway > 0f ? 70 : 26;
		deathStone = death.Cut(new Vector2I(foot, RootRow), new Rect2I(0, 0, 96, RootRow));
		deathRootBall = death.Cut(new Vector2I(48, 94), new Rect2I(0, RootRow, 96, 96 - RootRow));
		markCarry = 0f;
	}

	protected internal override void TickDeath(BossDeath death, float t, float dt)
	{
		if (deathStone == null)
			return;

		float s = death.ArtScale.X;
		ElementColors.Ramp arcane = ElementColors.GetRamp(Element.Arcane);
		float sway = t < StoneTips ? BossDeath.Shudder(t, BossDeath.Span(t, 0.2f, StoneTips), 9f) * s : 0f;
		float fall = BossDeath.EaseIn(BossDeath.Span(t, StoneTips, StoneLands));
		float bounce = 0.05f * Mathf.Sin(Mathf.Pi * BossDeath.Span(t, StoneLands, StoneLands + 0.2f));
		deathStone.Rotation = death.ArtAway * (Mathf.DegToRad(88f) * fall - bounce);
		deathStone.Offset = new Vector2(sway, 0f);
		deathStone.Glow = new Color(arcane.Mid, 0.3f * BossDeath.Span(t, 0f, 0.1f) * (1f - BossDeath.Span(t, 0.3f, 0.6f)));

		// The ball of earth it was torn up with jolts when the stone comes down, and settles back in.
		float jolt = t > StoneLands ? BossDeath.Shudder(t, 1f - BossDeath.Span(t, StoneLands, StoneLands + 0.3f), 18f) * s : 0f;
		deathRootBall.Sink(death, (96f - RootRow) * BossDeath.EaseIn(BossDeath.Span(t, 1.45f, 2.4f)));
		deathRootBall.Offset += new Vector2(0f, jolt);

		float dim = BossDeath.Span(t, 1.3f, 2.0f);
		var granite = new Color(1f - 0.45f * dim, 1f - 0.45f * dim, 1f - 0.38f * dim, 1f - BossDeath.Span(t, 1.9f, 2.6f));
		deathStone.Color = granite;
		deathRootBall.Color = new Color(granite, 1f);

		// The mark leaving: his light streams up out of the split and away.
		if (t >= 0.3f && t < 0.95f)
		{
			markCarry += 44f * dt;
			while (markCarry >= 1f)
			{
				markCarry -= 1f;
				death.Emit(new BossDeath.Throw
				{
					Size = death.Rand(0f, 1f) < 0.35f ? 4f : 2f, Color = arcane.Hot, ColorB = arcane.Mid, Light = true,
					NoLand = true, SpeedMin = 4f, SpeedMax = 24f, UpMin = 40f, UpMax = 110f, Gravity = -170f, Drag = 0.6f,
					Flutter = 3f, Life = 0.9f, Fade = 0.5f,
				}, 1, Mark, new Vector2(2f, 6f));
			}
		}

		if (death.Crossed(StoneLands))
		{
			// Where the crown came down.
			Vector2 crown = death.PieceToLocal(deathStone, new Vector2(48f, 30f));
			var thud = new Vector2(crown.X, death.Foot.Y - 6f);
			death.DustRing(thud, 80f, 12, HeathDust, 0.9f, 20f);
			death.EmitAt(new BossDeath.Throw
			{
				Cell = BossDeath.Cell.ChipA, Variants = 3, Tinted = true, SpeedMin = 60f, SpeedMax = 220f,
				UpMin = 100f, UpMax = 240f, Bounce = 0.3f, Friction = 6f, Spin = 7f, Life = DeathSeconds - StoneLands, Fade = 0.45f,
			}, 16, thud, 8f, 60f);
			death.EmitAt(new BossDeath.Throw
			{
				Cell = BossDeath.Cell.Heather, SpeedMin = 30f, SpeedMax = 120f, UpMin = 90f, UpMax = 200f, Gravity = 500f,
				Drag = 0.8f, Bounce = 0f, Friction = 9f, Spin = 4f, Life = DeathSeconds - StoneLands, Fade = 0.45f,
			}, 6, thud, 6f, 50f);
			death.EmitAt(new BossDeath.Throw
			{
				Cell = BossDeath.Cell.Clod, SpeedMin = 30f, SpeedMax = 110f, UpMin = 80f, UpMax = 160f, Bounce = 0.1f,
				Friction = 8f, Spin = 5f, Life = DeathSeconds - StoneLands, Fade = 0.45f,
			}, 5, death.Foot, 10f, 30f);
		}
	}

	// The mark burning down the length of its split, then gone up out of it.
	protected internal override void DrawDeathLight(BossDeath death, CanvasItem canvas, float t)
	{
		if (deathStone == null || t > 0.7f)
			return;
		ElementColors.Ramp arcane = ElementColors.GetRamp(Element.Arcane);
		float run = BossDeath.EaseOut(BossDeath.Span(t, 0f, 0.28f));
		float fade = 1f - BossDeath.Span(t, 0.38f, 0.7f);
		float reach = run * (Split.Length - 1);
		for (int i = 0; i < Split.Length - 1 && i < reach; i++)
		{
			float part = Mathf.Min(1f, reach - i);
			Vector2 a = death.PieceToLocal(deathStone, Split[i]);
			Vector2 b = death.PieceToLocal(deathStone, Split[i].Lerp(Split[i + 1], part));
			BossDeath.Line(canvas, a, b, new Color(arcane.Mid, 0.6f * fade), BossDeath.Px * 2f);
			BossDeath.Line(canvas, a, b, new Color(arcane.Core, 0.95f * fade));
		}
		float flare = BossDeath.Span(t, 0f, 0.06f) * (1f - BossDeath.Span(t, 0.25f, 0.6f));
		BossDeath.Disc(canvas, death.PieceToLocal(deathStone, Mark), 9f, 9f, new Color(arcane.Hot, 0.85f * flare));
	}
}
