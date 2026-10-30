using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using WizardSurvivors.scripts;

// THE LAST BRIDGE'S RULES (.ai/endgame-castle.md 3, piece C1). The chapter is a crossing, not a stand:
// the span falls in behind the player from pace 0:30 (BridgeCollapseFront), most of the garrison comes at
// him from ahead, and at 15:00 he is in the gate yard under the castle wall, wherever he dawdled, when the
// Constable comes. When the Constable falls the gate is the way home.
//
// What it answers, by hook (ChapterRules):
//   1. RunStarted          the front, the backdrop, and the furniture streamed along the bridge as he walks
//                          it: the parapets on both shores, the corner turrets, the gatehouse towers, the
//                          castle wall and its gate (bridge.json, tools/art/props_bridge.py).
//   2. TryPickSpawnPoint   65% AHEAD along the deck (they come from his castle), 15% BEHIND him but ahead
//                          of the front, 20% at the PARAPETS (climbing over) - off screen, as always, and
//                          always on the deck: the usual ring is mostly over the drop.
//   3. IsPlacementBlocked  the fallen deck behind the front.
//   4. BossSpawnPoint      the gate's foot.
//   5. VictoryPortalPoint  the gate itself.
//   7. CreateObjective     the strip under the run timer: the fall, the player and the gate on one line.
// and, on its own clock: the murder-holes in each gatehouse passage, and the archers' volleys over the gate
// yard before 15:00 (3.8).
//
// The budget, the pace curve, the XP curve and the cast are all the run's own (.ai/run-pacing.md): only
// WHERE things appear is the bridge's. The shape it reads is BridgeBand (LevelGenerator.Band.cs).
public partial class BridgeRules : ChapterRules
{
	// --- The front --------------------------------------------------------------------------------

	/// <summary>When the span starts to fall behind him, on the pace clock (3.3).</summary>
	public const float FrontStartPace = 30f;

	/// <summary>
	/// The front's speed in px a pace second: from the back of the bridgehead at 0:30 to the gate yard's
	/// mouth at 15:00. About 31 - an eighth of walking speed is 28, and the bridgehead was given room
	/// behind the start so that a player who never leaves it has about a minute and a half (the front
	/// reaches the start at 1:21 and the bridgehead's far end at 1:44).
	/// </summary>
	public static readonly float FrontSpeed =
		(BridgeBand.GateYardMouth - BridgeBand.HeadBack) / (Node2DGame.ReferenceRunSeconds - FrontStartPace);

	// Pace seconds the front has been held (the Span Lantern, .ai/endgame-castle.md 3.9 - a secret's to
	// call, through HoldFront).
	private float heldPace;
	// --bridge-front=PX (debug builds): the front starts PX behind the start at pace 0 and is moving from
	// the first frame, so a photograph or a probe can have it on screen without waiting a minute.
	private float? debugFrontLead;

	/// <summary>Where the front is, px along the bridge: the deck behind it has fallen.</summary>
	public float FrontS
	{
		get
		{
			float pace = Game?.PaceSeconds ?? 0f;
			float s = debugFrontLead is float lead
				? -lead + FrontSpeed * Mathf.Max(0f, pace - heldPace)
				: BridgeBand.HeadBack + FrontSpeed * Mathf.Max(0f, pace - FrontStartPace - heldPace);
			return Mathf.Clamp(s, BridgeBand.HeadBack, BridgeBand.GateYardMouth);
		}
	}

	/// <summary>True once the front has started to move (it draws nothing before).</summary>
	public bool FrontMoving => debugFrontLead != null || (Game?.PaceSeconds ?? 0f) >= FrontStartPace;

	/// <summary>Hold the front where it is for <paramref name="paceSeconds"/> more (the Span Lantern, C9).</summary>
	public void HoldFront(float paceSeconds) => holdRemaining += Mathf.Max(0f, paceSeconds);
	private float holdRemaining;
	private float lastPace = -1f;

	/// <summary>A world point's distance along the bridge, px.</summary>
	public float AlongOf(Vector2 world) => BridgeBand.AlongOf(world - Origin);

	/// <summary>A world point's distance across the bridge, px (south-east positive).</summary>
	public float AcrossOf(Vector2 world) => BridgeBand.AcrossOf(world - Origin);

	/// <summary>The world point <paramref name="s"/> along and <paramref name="t"/> across.</summary>
	public Vector2 WorldAt(float s, float t) => Origin + BridgeBand.LocalAt(s, t);

	private Vector2 Origin => Game?.StageOrigin ?? Vector2.Zero;

	// --- Lifetime -----------------------------------------------------------------------------------

	private readonly RandomNumberGenerator rng = new();
	private BridgeCollapseFront front;
	private BridgeBackdrop backdrop;
	private BridgeFurniture furniture;
	private Control objective;
	private Control onboardingTip;
	private float objectiveRedraw;
	private float murderHoleTimer;
	private float volleyTimer;
	private int furthestPiece = -1;

	public override void _Ready()
	{
		rng.Randomize();
		// No scattered props on the bridge. Node2DGame scatters its curated set per chunk later in its
		// _Ready (BuildCuratedProps), and an empty theme list there means every theme, not none; everything
		// that stands on the bridge is the furniture below, which the front takes with the deck. A brazier
		// scattered on the deck would stand on in the drop once the deck under it fell.
		Game.EnableCuratedProps = false;
		if (OS.IsDebugBuild())
		{
			foreach (string arg in OS.GetCmdlineUserArgs())
			{
				if (arg.StartsWith("--bridge-front=")
					&& float.TryParse(arg.Substring("--bridge-front=".Length), System.Globalization.NumberStyles.Float,
						System.Globalization.CultureInfo.InvariantCulture, out float lead))
					debugFrontLead = lead;
			}
		}
	}

	public override void RunStarted()
	{
		front = new BridgeCollapseFront { Name = "BridgeCollapseFront", Rules = this };
		Game.AddChild(front);
		backdrop = new BridgeBackdrop { Name = "BridgeBackdrop", Rules = this };
		Game.AddChild(backdrop);
		furniture = new BridgeFurniture(this);
		furniture.Update(0f);
		GD.Print($"BRIDGE: {BridgeBand.Pieces.Length} pieces, {BridgeBand.End - BridgeBand.HeadBack:0} px from the cliff to the castle wall; "
			+ $"the front falls at {FrontSpeed:0.0} px a pace second from {FrontStartPace:0} s"
			+ (debugFrontLead is float lead ? $" (--bridge-front: {lead:0} px behind the start from the first frame)" : ""));
	}

	public override void _ExitTree()
	{
		furniture?.Release();
		furniture = null;
	}

	public override void _Process(double delta)
	{
		if (Game == null || !IsInstanceValid(Game))
			return;
		float d = (float)delta;

		// The Span Lantern's hold, in pace seconds, spent only while the front would be moving.
		float pace = Game.PaceSeconds;
		if (lastPace >= 0f && holdRemaining > 0f && FrontMoving)
		{
			float step = Mathf.Min(holdRemaining, Mathf.Max(0f, pace - lastPace));
			heldPace += step;
			holdRemaining -= step;
		}
		lastPace = pace;

		Player player = Game.RunPlayer;
		if (player != null)
		{
			float s = AlongOf(player.GlobalPosition);
			furniture?.Update(s);
			NoteProgress(s, pace);
			if (Node2DGame.InPlay && !player.IsDead)
			{
				TickMurderHoles(player, s, d);
				TickVolleys(player, s, d);
			}
		}
		furniture?.UpdateFade(player);

		objectiveRedraw -= d;
		if (objective != null && objectiveRedraw <= 0f)
		{
			objectiveRedraw = 0.1f;
			// A new player's onboarding tips use the same strip of the HUD (Node2DGame.OnboardingTipTop): the
			// bridge's line steps aside while one is up rather than printing under it.
			onboardingTip ??= Game.GetNodeOrNull<Control>("UIOverlay/OnboardingTipLabel");
			objective.Visible = onboardingTip == null || !onboardingTip.Visible;
			objective.QueueRedraw();
		}
	}

	// One line each time he first reaches a new piece: the harness's evidence that the bot got to the yard.
	private void NoteProgress(float s, float pace)
	{
		int piece = BridgeBand.PieceIndexAt(s);
		if (piece <= furthestPiece)
			return;
		furthestPiece = piece;
		BridgeBand.Piece p = BridgeBand.Pieces[piece];
		if (p.Kind == BridgeBand.PieceKind.Bridgehead)
			return;
		GD.Print($"BRIDGE: reached {p.Kind} {p.Index + 1} at pace {pace:0} s ({FormatClock(pace)}), {s:0} px along; the front at {FrontS:0}.");
	}

	private static string FormatClock(float seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";

	// --- Hook 2: spawn points on the band -----------------------------------------------------------

	// Shares of 3.3: ahead, behind (but ahead of the front), at the parapets.
	private const float AheadShare = 0.65f;
	private const float BehindShare = 0.15f;
	// Kept off the shore by about a body, so a spawn never stands with its feet over the drop.
	private const float SpawnShoreMargin = 70f;
	// And this far ahead of the front, so nothing appears on ground about to fall.
	private const float SpawnFrontMargin = 220f;

	public override bool TryPickSpawnPoint(Vector2 playerPosition, out Vector2 point)
	{
		float ps = AlongOf(playerPosition);
		Vector2 half = VisibleHalfExtents();
		// Straight along the axis the screen's edge is the nearer of its two half-sizes over cos 45.
		float edge = Mathf.Min(half.X, half.Y) * 1.41421356f;
		float roll = rng.Randf();
		int first = roll < AheadShare ? 0 : roll < AheadShare + BehindShare ? 1 : 2;
		for (int k = 0; k < 3; k++)
		{
			int kind = (first + k) % 3;
			bool found = kind switch
			{
				0 => TryAhead(playerPosition, ps, edge, half, out point),
				1 => TryBehind(playerPosition, ps, edge, half, out point),
				_ => TryParapet(playerPosition, ps, half, out point),
			};
			if (found)
				return true;
		}
		// Nothing off screen fits (he is pressed against the castle wall with the front at his heels):
		// still a point on the deck, never the usual ring, which is mostly over the drop.
		float s = Mathf.Clamp(ps + edge, FrontS + SpawnFrontMargin, BridgeBand.End - 120f);
		point = WorldAt(s, 0f);
		return true;
	}

	private bool TryAhead(Vector2 player, float ps, float edge, Vector2 half, out Vector2 point)
	{
		float s = ps + edge * 1.08f + rng.RandfRange(0f, 330f);
		return TryAlong(player, s, +1f, half, out point);
	}

	private bool TryBehind(Vector2 player, float ps, float edge, Vector2 half, out Vector2 point)
	{
		float s = ps - edge * 1.08f - rng.RandfRange(0f, 330f);
		return TryAlong(player, s, -1f, half, out point);
	}

	// A point at s with a random t across the deck there, pushed further along (in the direction given)
	// until it is off screen. A deck wider than the screen is tall (a yard on a phone) can need a push.
	private bool TryAlong(Vector2 player, float s, float direction, Vector2 half, out Vector2 point)
	{
		float min = Mathf.Max(FrontS + SpawnFrontMargin, BridgeBand.Start + SpawnShoreMargin);
		float max = BridgeBand.End - SpawnShoreMargin;
		float t = float.NaN;
		for (int i = 0; i < 12; i++)
		{
			if (s < min || s > max)
				break;
			float hw = BridgeBand.HalfWidthAt(s) - SpawnShoreMargin;
			if (hw > 0f)
			{
				if (float.IsNaN(t) || Mathf.Abs(t) > hw)
					t = rng.RandfRange(-hw, hw);
				Vector2 candidate = WorldAt(s, t);
				if (IsOffScreen(player, candidate, half))
				{
					point = candidate;
					return true;
				}
			}
			s += direction * 60f;
		}
		point = default;
		return false;
	}

	private bool TryParapet(Vector2 player, float ps, Vector2 half, out Vector2 point)
	{
		float side = rng.Randf() < 0.5f ? -1f : 1f;
		float min = Mathf.Max(FrontS + SpawnFrontMargin, BridgeBand.Start + SpawnShoreMargin);
		for (int i = 0; i < 8; i++)
		{
			float s = ps + rng.RandfRange(-900f, 1500f);
			if (s < min || s > BridgeBand.End - SpawnShoreMargin)
				continue;
			float hw = BridgeBand.HalfWidthAt(s) - SpawnShoreMargin;
			if (hw <= 0f)
				continue;
			Vector2 candidate = WorldAt(s, side * hw);
			if (IsOffScreen(player, candidate, half))
			{
				point = candidate;
				return true;
			}
			side = -side;
		}
		point = default;
		return false;
	}

	private const float OffScreenMargin = 40f;

	private static bool IsOffScreen(Vector2 player, Vector2 candidate, Vector2 half)
	{
		Vector2 o = candidate - player;
		return Mathf.Abs(o.X) > half.X + OffScreenMargin || Mathf.Abs(o.Y) > half.Y + OffScreenMargin;
	}

	// Half the visible world rectangle: the viewport over the gameplay zoom (Node2DGame does the same for
	// its own ring, privately).
	private Vector2 VisibleHalfExtents()
	{
		Vector2 viewport = GetViewport().GetVisibleRect().Size;
		float zoom = 1f;
		var camera = Game.RunPlayer?.GetNodeOrNull<Camera2D>("Camera2D");
		if (camera != null && camera.Zoom.X > 0.01f)
			zoom = camera.Zoom.X;
		return viewport / (2f * zoom);
	}

	// --- Hooks 3, 4, 5 ------------------------------------------------------------------------------

	public override bool IsPlacementBlocked(Vector2 point) =>
		FrontMoving && AlongOf(point) < FrontS + BridgeBand.TilePx;

	/// <summary>The gate's foot, where the Constable comes down from the wall (3.6).</summary>
	public override Vector2? BossSpawnPoint(BossDefinition boss) => WorldAt(BridgeBand.GateS - 260f, 0f);

	/// <summary>The gate in the castle wall: walking in ends the run (3.3).</summary>
	public override Vector2? VictoryPortalPoint(Vector2 bossFellAt) => WorldAt(BridgeBand.GateS - 40f, 0f);

	// --- The murder-holes and the volleys (3.8) ----------------------------------------------------

	// Pitch poured through the gatehouse roof in a telegraphed ring as he passes under: a reason not to
	// fight in the narrows. Every 2.4 s while he is in a gatehouse passage, where he stands.
	private const float MurderHoleInterval = 2.4f;
	private const float MurderHoleWindUp = 1.1f;
	private const float MurderHoleRadius = 70f;
	private const int MurderHoleDamage = 4;
	private static readonly Color PitchColor = new(0.96f, 0.52f, 0.18f, 0.9f);

	private void TickMurderHoles(Player player, float s, float delta)
	{
		int i = BridgeBand.PieceIndexAt(s);
		if (i < 0 || BridgeBand.Pieces[i].Kind != BridgeBand.PieceKind.Gatehouse)
		{
			// The first ring comes a beat after he enters, not on the threshold.
			murderHoleTimer = MurderHoleInterval * 0.4f;
			return;
		}
		murderHoleTimer -= delta;
		if (murderHoleTimer > 0f)
			return;
		murderHoleTimer = MurderHoleInterval;
		Vector2 at = player.GlobalPosition + new Vector2(rng.RandfRange(-24f, 24f), rng.RandfRange(-16f, 16f));
		TelegraphedGroundHit.Place(Game, at, MurderHoleWindUp, MurderHoleRadius, MurderHoleDamage, PitchColor);
	}

	// The archers on the castle wall, before the Constable comes: a volley of three rings onto the gate
	// yard every few seconds, one where he stands. The yard is the most dangerous ground on the bridge
	// before 15:00, by design (3.3: arriving early is a choice, not an exploit).
	private const float VolleyInterval = 3.2f;
	private const float VolleyWindUp = 1.25f;
	private const float VolleyRadius = 58f;
	private const int VolleyDamage = 3;
	private static readonly Color BoltColor = new(0.78f, 0.84f, 0.96f, 0.85f);

	private void TickVolleys(Player player, float s, float delta)
	{
		if (s < BridgeBand.GateYardMouth || Game.BossFightActive || Game.RunFinished || Game.VictoryPortalOpen)
		{
			volleyTimer = 1.5f;
			return;
		}
		volleyTimer -= delta;
		if (volleyTimer > 0f)
			return;
		volleyTimer = VolleyInterval;
		Vector2 p = player.GlobalPosition;
		TelegraphedGroundHit.Place(Game, p + new Vector2(rng.RandfRange(-30f, 30f), rng.RandfRange(-20f, 20f)),
			VolleyWindUp, VolleyRadius, VolleyDamage, BoltColor);
		for (int k = 0; k < 2; k++)
		{
			Vector2 at = p + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * rng.RandfRange(120f, 240f);
			if (BridgeBand.IsOnDeck(at - Origin, 20f))
				TelegraphedGroundHit.Place(Game, at, VolleyWindUp + 0.2f * (k + 1), VolleyRadius, VolleyDamage, BoltColor);
		}
	}

	// --- Hook 7: the strip under the run timer ------------------------------------------------------

	public override Control CreateObjective()
	{
		objective = new Control { Name = "BridgeObjective", MouseFilter = Control.MouseFilterEnum.Ignore };
		objective.Draw += DrawObjective;
		return objective;
	}

	// The whole bridge on one line, back of the bridgehead at the left and the gate at the right: the fallen
	// part in the abyss's dark, the gatehouses as ticks, the player as a bone-white mark, the front as a
	// ragged edge, and the gate as a small arch that lights his cold arcane when it is the way home. The
	// bridge's goal is not a secret, so showing it is no pointer (3.3).
	private void DrawObjective()
	{
		if (objective == null || Game == null)
			return;
		Vector2 size = objective.Size;
		float width = Mathf.Min(440f, size.X * 0.62f);
		float x0 = (size.X - width) * 0.5f;
		float y = Mathf.Round(size.Y * 0.5f);
		float span = BridgeBand.End - BridgeBand.Start;
		float X(float s) => x0 + Mathf.Clamp((s - BridgeBand.Start) / span, 0f, 1f) * width;

		var deck = new Color(0.36f, 0.42f, 0.52f, 0.95f);
		var outline = new Color(0.02f, 0.03f, 0.05f, 0.85f);
		var fallen = new Color(0.04f, 0.05f, 0.08f, 0.95f);
		var bone = new Color(0.93f, 0.95f, 1.0f, 1f);

		objective.DrawRect(new Rect2(x0 - 2f, y - 4f, width + 4f, 8f), outline);
		objective.DrawRect(new Rect2(x0, y - 2f, width, 4f), deck);
		foreach (BridgeBand.Piece p in BridgeBand.Pieces)
		{
			if (p.Kind == BridgeBand.PieceKind.Gatehouse)
			{
				float gx = Mathf.Round(X(p.Mid));
				objective.DrawRect(new Rect2(gx - 2f, y - 6f, 4f, 12f), outline);
				objective.DrawRect(new Rect2(gx - 1f, y - 5f, 2f, 10f), deck);
			}
		}
		if (FrontMoving)
		{
			float fx = X(FrontS);
			objective.DrawRect(new Rect2(x0, y - 2f, fx - x0, 4f), fallen);
			// The ragged edge of the fall.
			for (int k = -1; k <= 1; k++)
				objective.DrawRect(new Rect2(Mathf.Round(fx) - 1f + (k & 1), y - 2f + k * 2f, 2f, 2f), new Color(0.55f, 0.36f, 0.30f, 1f));
		}
		// The gate: a little arch, his cold light in it once it is the way home.
		float ax = Mathf.Round(X(BridgeBand.GateS));
		bool open = Game.VictoryPortalOpen;
		objective.DrawRect(new Rect2(ax - 6f, y - 9f, 12f, 14f), outline);
		objective.DrawRect(new Rect2(ax - 5f, y - 8f, 10f, 12f), deck);
		objective.DrawRect(new Rect2(ax - 2f, y - 4f, 4f, 8f), open ? new Color(0.66f, 0.85f, 1f, 1f) : outline);
		// The player.
		if (Game.RunPlayer is Player player)
		{
			float px = Mathf.Round(X(AlongOf(player.GlobalPosition)));
			objective.DrawRect(new Rect2(px - 3f, y - 6f, 6f, 12f), outline);
			objective.DrawRect(new Rect2(px - 2f, y - 5f, 4f, 10f), bone);
		}
	}
}

/// <summary>
/// The bridge's furniture, streamed along it as the player walks: the parapet on both shores, a turret at
/// every corner where the shore turns, the two towers of each gatehouse, and the castle wall with its gate.
/// All of it is the generated sheet (tools/art/props_bridge.py, bridge.json), sliced by region, so the whole
/// parapet is one texture and batches. Not a Node: it owns a holder node under the run and frees it.
/// </summary>
/// <remarks>
/// WHERE THE SHORES ARE. A shore along the bridge is the line x + y = c (world px from the stage origin),
/// c = +-K * 48 for its piece's K; a shore across it, where the deck narrows or widens, is x - y = e * 48 for
/// the first or last cell row of the water (BridgeBand). Both run through tile corners, so a piece stands on
/// every 48 px step of the line. That is where WaterShape draws the shore, give or take its 4 px wander.
///
/// HOW EACH SITS. Every piece stands among the bodies and is y-sorted with them, at the sort line its
/// manifest gives (props_bridge.py's SORT_DY): a wall with the deck in front of it (the top-left shore, a jog
/// with the drop north-east, the castle wall) sorts at its highest point, so whatever stands at it is drawn in
/// front of it; a wall with the deck behind it (the bottom-right shore, a jog with the drop south-west) sorts at
/// its lowest, so it hides the boots of whatever stands at it, as a wall in three-quarter view does. The towers
/// and the parapets then sort against each other too (a tower in the drop behind a parapet is behind it). The
/// towers thin round him when he is behind one (canopy_fade.gdshader, the trees' material).
/// </remarks>
public sealed class BridgeFurniture
{
	public const string Manifest = "res://assets/bonelight/world/bridge/bridge.json";

	private readonly BridgeRules rules;
	private readonly Node2D root;
	private readonly Node2D standing;  // towers, turrets, the wall: among the bodies
	private readonly Dictionary<string, (AtlasTexture Texture, Vector2 Anchor, float SortDy)> pieces = new();
	private readonly Dictionary<int, List<(Sprite2D Node, float S)>> sections = new();
	private readonly List<Sprite2D> gates = new();
	private readonly ShaderMaterial fade;
	private bool gateOpen;

	// Sections a chunk long along the bridge, kept from two behind the player to three ahead.
	private const float SectionLength = 1536f;
	private const int KeepBehind = 2;
	private const int KeepAhead = 3;
	// A piece goes with the deck once the front is this far past its anchor (half a "/" step).
	private const float FallenSlack = 34f;

	public BridgeFurniture(BridgeRules rules)
	{
		this.rules = rules;
		root = new Node2D { Name = "BridgeFurniture" };
		// Nested y-sort merges with the game's, so these sort among the bodies (Node2DGame is y-sorted).
		standing = new Node2D { Name = "Standing", YSortEnabled = true };
		root.YSortEnabled = true;
		root.AddChild(standing);
		rules.Game.AddChild(root);
		fade = new ShaderMaterial { Shader = GD.Load<Shader>("res://scenes/shaders/canopy_fade.gdshader") };
		LoadPieces();
	}

	private void LoadPieces()
	{
		string text = FileAccess.GetFileAsString(Manifest);
		if (string.IsNullOrEmpty(text))
		{
			GD.PushError($"BridgeFurniture: {Manifest} is missing; the bridge has no parapets. Run: python tools/art/props_bridge.py, then import.");
			return;
		}
		using JsonDocument doc = JsonDocument.Parse(text);
		JsonElement r = doc.RootElement;
		string sheetPath = "res://assets/bonelight/world/bridge/" + r.GetProperty("sheet").GetString();
		var sheet = GD.Load<Texture2D>(sheetPath);
		if (sheet == null)
		{
			GD.PushError($"BridgeFurniture: {sheetPath} is not imported; the bridge has no parapets. Run the import.");
			return;
		}
		foreach (JsonProperty p in r.GetProperty("pieces").EnumerateObject())
		{
			JsonElement e = p.Value;
			var region = new Rect2(e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(),
				e.GetProperty("w").GetInt32(), e.GetProperty("h").GetInt32());
			var texture = new AtlasTexture { Atlas = sheet, Region = region };
			float sortDy = e.TryGetProperty("sort_dy", out JsonElement dy) ? dy.GetInt32() : 0f;
			pieces[p.Name] = (texture, new Vector2(e.GetProperty("ax").GetInt32(), e.GetProperty("ay").GetInt32()), sortDy);
		}
	}

	public void Release()
	{
		if (GodotObject.IsInstanceValid(root))
			root.QueueFree();
		sections.Clear();
	}

	/// <summary>Keeps the sections round <paramref name="playerS"/> furnished and forgets the rest.</summary>
	public void Update(float playerS)
	{
		if (pieces.Count == 0)
			return;
		int at = Mathf.FloorToInt((playerS - BridgeBand.Start) / SectionLength);
		int last = Mathf.FloorToInt((BridgeBand.End + 400f - BridgeBand.Start) / SectionLength);
		for (int i = Math.Max(0, at - KeepBehind); i <= Math.Min(last, at + KeepAhead); i++)
		{
			if (!sections.ContainsKey(i))
				sections[i] = Build(i);
		}
		List<int> drop = null;
		foreach (int i in sections.Keys)
			if (i < at - KeepBehind - 1 || i > at + KeepAhead + 1)
				(drop ??= new List<int>()).Add(i);
		if (drop != null)
		{
			foreach (int i in drop)
			{
				foreach ((Sprite2D n, float _) in sections[i])
					if (GodotObject.IsInstanceValid(n))
						n.QueueFree();
				sections.Remove(i);
			}
		}

		// What the front has passed fell with the deck: hidden, so no parapet stands over the drop.
		float front = rules.FrontMoving ? rules.FrontS : float.MinValue;
		foreach (List<(Sprite2D Node, float S)> list in sections.Values)
		{
			foreach ((Sprite2D node, float s) in list)
			{
				bool keep = s + FallenSlack >= front;
				if (node.Visible != keep && GodotObject.IsInstanceValid(node))
					node.Visible = keep;
			}
		}

		// The gate opens with the way home.
		bool open = rules.Game.VictoryPortalOpen;
		if (open != gateOpen && pieces.TryGetValue(open ? "gate_open" : "gate_shut", out var gate))
		{
			gateOpen = open;
			foreach (Sprite2D g in gates)
				if (GodotObject.IsInstanceValid(g))
					g.Texture = gate.Texture;
		}
	}

	/// <summary>The towers thin round him when he is behind one, as the trees' crowns do.</summary>
	public void UpdateFade(Player player)
	{
		if (player != null && GodotObject.IsInstanceValid(player))
			fade.SetShaderParameter("player_pos", player.GlobalPosition);
	}

	private List<(Sprite2D Node, float S)> Build(int section)
	{
		var made = new List<(Sprite2D Node, float S)>();
		float s0 = BridgeBand.Start + section * SectionLength;
		float s1 = s0 + SectionLength;
		BridgeBand.Piece[] all = BridgeBand.Pieces;
		for (int i = 0; i < all.Length; i++)
		{
			BridgeBand.Piece p = all[i];
			if (p.S1 <= s0 || p.S0 >= s1)
				continue;
			float a = Math.Max(p.S0, s0), b = Math.Min(p.S1, s1);
			// The two long shores, where the run of the piece's own K lies inside [a, b).
			Shore(made, p, i, -1, a, b);
			Shore(made, p, i, +1, a, b);
			// A gatehouse's two towers, at its middle.
			if (p.Kind == BridgeBand.PieceKind.Gatehouse && p.Mid >= s0 && p.Mid < s1)
				Towers(made, p);
		}
		// The jogs where the deck narrows or widens, and the castle wall at the end.
		for (int i = 0; i + 1 < all.Length; i++)
		{
			float js = all[i].S1;
			if (js >= s0 && js < s1 && all[i].K != all[i + 1].K)
				Jog(made, i);
		}
		if (BridgeBand.End >= s0 && BridgeBand.End < s1)
			CastleWall(made);
		return made;
	}

	// First and last e (tx - ty, the cell's along-index) whose cell centre is inside the piece.
	private static int FirstE(BridgeBand.Piece p) => Mathf.CeilToInt(p.S0 / BridgeBand.StepPx);
	private static int LastE(BridgeBand.Piece p) => Mathf.CeilToInt(p.S1 / BridgeBand.StepPx) - 1;

	// A long shore of piece p on one side (-1 the top-left, +1 the bottom-right): x + y = side * K * 48.
	// Lattice points on it are (48 i, c - 48 i), and a segment runs from point i up-right to point i + 1.
	// A segment is placed when the whole of it is beside this piece's deck, so the run ends a step short of
	// a jog and the corner turret covers the turn.
	private void Shore(List<(Sprite2D Node, float S)> made, BridgeBand.Piece p, int index, int side, float a, float b)
	{
		// The bridgehead is the cliff top: its edge is the cliff's, with no wall on it.
		if (p.Kind == BridgeBand.PieceKind.Bridgehead)
			return;
		int k = p.K;
		float c = side * k * BridgeBand.TilePx;
		float half = BridgeBand.TilePx * 0.70710678f;       // half a lattice step along s: 48 / sqrt 2
		// s of point i: (48 i - (c - 48 i)) / sqrt 2 = (96 i - c) / sqrt 2. A segment belongs to the section
		// its first point is in, and is placed only if both its points are half a step inside the piece.
		int iFrom = Mathf.CeilToInt((Math.Max(a, p.S0 + half) * 1.41421356f + c) / 96f);
		int iSection = Mathf.CeilToInt((b * 1.41421356f + c) / 96f) - 1;
		int iPiece = Mathf.FloorToInt(((p.S1 - half) * 1.41421356f + c) / 96f) - 1;
		int iTo = Math.Min(iSection, iPiece);
		string id = side < 0 ? "parapet_nw" : "parapet_se";
		for (int i = iFrom; i <= iTo; i++)
		{
			var at = new Vector2(48f * i, c - 48f * i);
			string variant = id;
			ulong h = LevelGenerator.Hash(rules.Game.TerrainSeed, i, side * 1000 + index, 0x7B1D);
			// His banner on one segment in twenty-five on the bottom-right shore, hung on the face the
			// viewer sees; a breach (the merlons gone) one in eighteen on either.
			if (side > 0 && h % 25 == 0 && pieces.ContainsKey(id + "_banner"))
				variant = id + "_banner";
			else if (h % 18 == 3 && pieces.ContainsKey(id + "_breach"))
				variant = id + "_breach";
			Place(made, variant, at, standing);
		}
	}

	// The jog between piece i and i + 1: on each side the shore turns across the bridge, from the
	// narrower piece's K to the wider's. Its line is x - y = e * 48 for the first water row (BridgeBand).
	private void Jog(List<(Sprite2D Node, float S)> made, int i)
	{
		BridgeBand.Piece a = BridgeBand.Pieces[i], b = BridgeBand.Pieces[i + 1];
		bool narrowing = b.K < a.K;
		// The water beside the narrower piece: its first row when it comes second, its last when first.
		int e = narrowing ? FirstE(b) : LastE(a);
		float u = e * BridgeBand.TilePx;            // the line x - y = u
		int kNarrow = Math.Min(a.K, b.K), kWide = Math.Max(a.K, b.K);
		// Where the bridge leaves the cliff top there is no wall across it, only the abutment's turrets.
		bool cliff = a.Kind == BridgeBand.PieceKind.Bridgehead;
		foreach (int side in new[] { -1, 1 })
		{
			// Along the jog, x + y runs from side * kNarrow * 48 to side * kWide * 48. Lattice points on
			// x - y = u are ((u + v) / 2, (v - u) / 2) for v = x + y of u's parity in 48s (so x and y land
			// on tile corners), a step of the line being 96 in v.
			float vFrom = side * kNarrow * BridgeBand.TilePx, vTo = side * kWide * BridgeBand.TilePx;
			float lo = Math.Min(vFrom, vTo), hi = Math.Max(vFrom, vTo);
			float v0 = LatticeV(lo + 48f, e);
			// A "\" segment runs from point v down-right to v + 96. The drop is on the narrower piece's
			// side of the line: north-east when narrowing (the inner face shows), south-west when widening
			// (the outer face and the flank show).
			string id = narrowing ? "parapet_ne" : "parapet_sw";
			if (!cliff)
			{
				for (float v = v0; v + 96f <= hi - 24f; v += 96f)
					Place(made, id, new Vector2((u + v) * 0.5f, (v - u) * 0.5f), standing);
			}
			// Turrets where it turns, which also cover what the water's rounding does to a corner.
			Place(made, "turret", new Vector2((u + vFrom) * 0.5f, (vFrom - u) * 0.5f), standing);
			if (!cliff)
				Place(made, "turret", new Vector2((u + vTo) * 0.5f, (vTo - u) * 0.5f), standing);
		}
	}

	// The first v at or after <paramref name="from"/> (a multiple of 48) with the parity of e, so the point
	// ((u + v) / 2, (v - u) / 2) on x - y = 48 e is a tile corner.
	private static float LatticeV(float from, int e)
	{
		int n = Mathf.CeilToInt(from / 48f);
		if (((n - e) & 1) != 0)
			n++;
		return n * 48f;
	}

	// A gatehouse's towers stand in the drop either side of the narrows and fill it: the strip of drop beside
	// the passage is the gatehouse's length along the bridge and (span K - gatehouse K) steps across, and each
	// tower is anchored at that strip's middle. They stand on blocked ground already (water): nothing new
	// collides, and the passage between them is exactly the deck the band left.
	private void Towers(List<(Sprite2D Node, float S)> made, BridgeBand.Piece p)
	{
		int i = Array.IndexOf(BridgeBand.Pieces, p);
		int outer = Math.Min(BridgeBand.Pieces[i - 1].K, BridgeBand.Pieces[i + 1].K);
		float t = (p.K + outer) * 0.5f * BridgeBand.StepPx;
		Place(made, "tower_nw", BridgeBand.LocalAt(p.Mid, -t).Round(), standing, fade: true);
		Place(made, "tower_se", BridgeBand.LocalAt(p.Mid, t).Round(), standing, fade: true);
	}

	// The castle wall across the far end of the gate yard (x - y = u for the last deck row's next), the gate
	// in its middle. Its face looks back down the yard; the drop and his castle are beyond it.
	private void CastleWall(List<(Sprite2D Node, float S)> made)
	{
		BridgeBand.Piece yard = BridgeBand.Pieces[BridgeBand.Pieces.Length - 1];
		int e = LastE(yard) + 1;
		float u = e * BridgeBand.TilePx;
		float c = yard.K * BridgeBand.TilePx;
		float gateHalf = 3 * 96f;
		for (float v = LatticeV(-c - 48f, e); v + 96f <= c + 48f; v += 96f)
		{
			if (v + 96f > -gateHalf && v < gateHalf)
				continue;
			Place(made, "castle_wall", new Vector2((u + v) * 0.5f, (v - u) * 0.5f), standing);
		}
		Sprite2D gate = Place(made, gateOpen ? "gate_open" : "gate_shut", new Vector2(u * 0.5f, -u * 0.5f), standing);
		if (gate != null)
			gates.Add(gate);
		Place(made, "turret", new Vector2((u - c) * 0.5f, (-c - u) * 0.5f), standing);
		Place(made, "turret", new Vector2((u + c) * 0.5f, (c - u) * 0.5f), standing);
	}

	// One piece at a lattice point (relative to the stage origin). A standing piece's node is at its ground
	// line lifted by SortLift, so it sorts against the wizard's feet; the others sort among their own.
	private Sprite2D Place(List<(Sprite2D Node, float S)> made, string id, Vector2 local, Node2D parent, bool fade = false)
	{
		if (!pieces.TryGetValue(id, out var piece))
			return null;
		Vector2 world = rules.Game.StageOrigin + local;
		// The node is its sort key: the piece's sort line (its anchor unless the manifest moves it up, as the
		// gate's is), lifted by SortLift for what stands among the bodies.
		float lift = (parent == standing ? WorldSprites.SortLift : 0f) - piece.SortDy;
		var sprite = new Sprite2D
		{
			Texture = piece.Texture,
			Centered = false,
			Position = world - new Vector2(0f, lift),
			Offset = -piece.Anchor + new Vector2(0f, lift),
		};
		if (fade)
			sprite.Material = this.fade;
		parent.AddChild(sprite);
		made.Add((sprite, BridgeBand.AlongOf(local)));
		return sprite;
	}
}
