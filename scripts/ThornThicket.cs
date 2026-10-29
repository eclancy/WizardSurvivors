using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Thorned Vine's Thicket (ThornVine.ThicketId, its Ascension that deepens Poison, 2026-10-29): where the
// whip lands, thorny brambles stay on the ground.
//
// The shape is the lash's own: the band its first stroke swept, from InnerShare of the vine's length out
// to its tip, over the arc it travelled before it snagged. So a lash that swung the whole 170 degrees
// leaves a crescent of thorns across the front of the pack, and one that snagged early leaves a short
// wedge - what is left is exactly where the whip went. For Seconds it hurts and poisons whatever stands
// in it every TickSeconds, and drags at it; then it dries brown and draws back into the ground, the way
// the vine itself withers.
//
// THE BAND IS THE HIT. The brambles are laid along arcs between the inner and outer radius and over the
// swept angle, and the tick tests that same annular sector (plus a body's width at its edges, as the
// lash does). Nothing marks it but the brambles.
//
// On the ground, under every body (DrawLayers.Ground). A bramble is never laid on blocked ground - water
// or a wall (.ai/terrain.md) - so a lash swung over a tarn leaves its thorns on the banks only. The
// stems are worked out once, when it is laid; the drawing is recorded while it grows and while it
// withers and left alone in between, so a standing thicket costs no redraw.
//
// THINNED (2026-10-29, after the first in-game look). Every lash lays one, and at level 8 the vine lashes
// about once a second, so three stood at once and a long fight carpeted the ground round the wizard in
// looping strands that read as clutter, not as a thicket. Now at most MaxStanding stand: laying another
// withers the oldest at once (it stops cutting as it starts to dry). And the stems are three, held
// closer to their arcs, so what is left is a band of bramble across the swept arc rather than a scribble.
//
// Every stamp is an opaque ramp stop on the 2 px grid. Drying, each one turns to the earth row and the
// gold row's soil on its own fixed beat (Dried), where it used to lerp toward a brown no ramp holds
// (2026-10-29, the pixel-art pass).
public partial class ThornThicket : Node2D
{
	/// <summary>Thickets that may stand at once; another withers the oldest.</summary>
	public const int MaxStanding = 2;

	public const float Seconds = 3f;
	public const float TickSeconds = 0.5f;
	/// <summary>A tick's hit, as a share of the lash that laid the thicket.</summary>
	public const float TickShare = 0.4f;
	/// <summary>How hard the brambles drag: what is in them moves at this share of its speed.</summary>
	public const float SlowMultiplier = 0.6f;
	/// <summary>The band starts this share of the vine's length out from its root.</summary>
	public const float InnerShare = 0.3f;

	private const float GrowSeconds = 0.25f;
	private const float WitherSeconds = 0.45f;
	private const float BodyTolerance = 12f;
	private const int Stems = 3;
	private const float StampSpacing = 4f;

	private static readonly List<Enemy> scratch = new();
	private static readonly List<ThornThicket> standing = new();

	private float inner;
	private float outer;
	private float start;
	private float side;
	private float swept;
	private int tickDamage;
	private int poisonTick;
	private float poisonSeconds;
	private SpellData source;
	private Player player;

	private float age;
	private float tickTimer;
	private Vector2[][] stems;
	private bool[][] open;

	private static readonly Color Occ = new Color(0.020f, 0.027f, 0.047f, 1f);
	private static readonly Color Bark = new Color(0.478f, 0.314f, 0.141f, 1f);      // earth edge #7A5024
	private static readonly Color GrassEdge = new Color(0.290f, 0.549f, 0.227f, 1f); // grass edge #4A8C3A
	private static readonly Color GrassMid = new Color(0.608f, 0.831f, 0.333f, 1f);  // grass mid #9BD455
	private static readonly Color GrassHot = new Color(0.812f, 0.933f, 0.580f, 1f);  // grass hot #CFEE94
	private static readonly Color PoisonMid = new Color(0.706f, 0.878f, 0.290f, 1f); // poison mid #B4E04A
	// What it dries to: the earth row and the gold row's soil (ThornVine's wither), stop for stop.
	private static readonly Color EarthMid = new Color(0.788f, 0.604f, 0.369f, 1f);  // earth mid #C99A5E
	private static readonly Color GoldShade = new Color(0.329f, 0.247f, 0.063f, 1f); // gold shade #543F10
	private static readonly Color GoldDeep = new Color(0.180f, 0.133f, 0.031f, 1f);  // gold deep #2E2208

	/// <summary>
	/// Lays a thicket over the band a lash swept from <paramref name="root"/>: out to
	/// <paramref name="length"/>, from <paramref name="startAngle"/> through <paramref name="sweptAngle"/>
	/// radians in direction <paramref name="sweepSide"/> (+1 or -1).
	/// </summary>
	public static void Lay(Node2D arena, Vector2 root, float length, float startAngle, float sweepSide, float sweptAngle,
		int lashDamage, int poisonTick, float poisonSeconds, SpellData source, Player player)
	{
		if (sweptAngle <= 0.05f || length < 20f)
			return;
		var thicket = new ThornThicket
		{
			inner = length * InnerShare,
			outer = length,
			start = startAngle,
			side = sweepSide,
			swept = sweptAngle,
			tickDamage = Math.Max(1, Mathf.RoundToInt(lashDamage * TickShare)),
			poisonTick = poisonTick,
			poisonSeconds = poisonSeconds,
			source = source,
			player = player,
		};
		// The oldest standing thicket withers to make room.
		for (int i = standing.Count - 1; i >= 0; i--)
		{
			if (!IsInstanceValid(standing[i]))
				standing.RemoveAt(i);
		}
		while (standing.Count >= MaxStanding)
			standing[0].Wither();
		arena.AddChild(thicket);
		thicket.GlobalPosition = root;
		thicket.LayStems();
		standing.Add(thicket);
	}

	// Starts drying now: it stops cutting, and draws back into the ground over WitherSeconds.
	private void Wither()
	{
		standing.Remove(this);
		age = MathF.Max(age, Seconds - WitherSeconds);
	}

	public override void _ExitTree() => standing.Remove(this);

	public override void _Ready()
	{
		Name = "ThornThicket";
		ZAsRelative = false;
		ZIndex = DrawLayers.Ground;
	}

	// Three stems along arcs across the band, each wandering a little in and out on a short wave -
	// near enough their arcs to read as a band, far enough to cross; every stamp tested against
	// blocked ground once.
	private void LayStems()
	{
		MazeNavigation nav = MazeNavigation.Active;
		stems = new Vector2[Stems][];
		open = new bool[Stems][];
		float phase = (GetInstanceId() % 997) * 0.37f;
		for (int s = 0; s < Stems; s++)
		{
			float r = inner + (outer - inner) * (s + 0.5f) / Stems;
			int count = Math.Max(4, (int)(r * swept / StampSpacing));
			stems[s] = new Vector2[count];
			open[s] = new bool[count];
			for (int i = 0; i < count; i++)
			{
				float q = swept * i / (count - 1);
				float a = start + side * q;
				float wobble = (outer - inner) / Stems * 0.3f * MathF.Sin(q * r / 13f + phase + s * 2.1f);
				Vector2 local = new Vector2(MathF.Cos(a), MathF.Sin(a)) * (r + wobble);
				stems[s][i] = local;
				open[s][i] = nav == null || !nav.IsBlockedAt(GlobalPosition + local);
			}
		}
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		age += dt;
		if (age >= Seconds)
		{
			QueueFree();
			return;
		}
		tickTimer -= dt;
		if (tickTimer <= 0f && age < Seconds - WitherSeconds)
		{
			tickTimer = TickSeconds;
			Tick();
		}
		if (age < GrowSeconds + dt || age > Seconds - WitherSeconds - dt)
			QueueRedraw();
	}

	private void Tick()
	{
		if (player == null || !IsInstanceValid(player))
			return;
		Vector2 at = GlobalPosition;
		EnemyRegistry.CollectNear(at, outer + BodyTolerance, scratch);
		float lo = MathF.Max(0f, inner - BodyTolerance);
		float hi = outer + BodyTolerance;
		foreach (Enemy enemy in scratch)
		{
			if (!IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			Vector2 offset = enemy.GlobalPosition - at;
			float d = offset.Length();
			if (d < lo || d > hi || d < 1f)
				continue;
			float slack = MathF.Atan2(BodyTolerance, d);
			float q = side * Mathf.Wrap(offset.Angle() - start, -Mathf.Pi, Mathf.Pi);
			if (q < -slack || q > swept + slack)
				continue;
			player.DealDamageToEnemy(enemy, tickDamage, source: source);
			if (!IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			enemy.ApplyPoison(poisonTick, poisonSeconds);
			enemy.ApplySlow(SlowMultiplier, TickSeconds * 1.6f);
		}
		scratch.Clear();
	}

	public override void _Draw()
	{
		if (stems == null)
			return;
		float grow = Mathf.Clamp(age / GrowSeconds, 0f, 1f);
		float wither = Mathf.Clamp((age - (Seconds - WitherSeconds)) / WitherSeconds, 0f, 1f);
		float g = WhipLimb.Grid;
		// Three passes: the occlusion under everything, then the stems and canes - solid, three art pixels
		// thick, lit along their upper edge - then the thorns, so a thorn is never cut by a stem drawn after it.
		for (int pass = 0; pass < 3; pass++)
		{
			for (int s = 0; s < Stems; s++)
			{
				Vector2[] pts = stems[s];
				// Grows out along the sweep the way the lash went, and draws back into the ground from both
				// ends as it withers.
				int shown = (int)(pts.Length * grow);
				int cut = (int)(pts.Length * 0.5f * wither);
				for (int i = cut; i < shown - cut; i++)
				{
					if (!open[s][i])
						continue;
					Vector2 p = pts[i];
					// A cane now and then, springing off the stem across the band: what makes a few stems a
					// bramble rather than three lines.
					bool cane = i % 11 == (s * 4) % 11 && i + 2 < pts.Length;
					Vector2 caneDir = Vector2.Zero;
					if (cane)
					{
						Vector2 along = (pts[i + 2] - p).Normalized();
						Vector2 radial = p.Normalized() * (((i / 11) + s) % 2 == 0 ? 1f : -1f);
						caneDir = (along * 0.7f + radial).Normalized();
					}
					Vector2 outward = p.Normalized() * ((i / 5) % 2 == 0 ? 1f : -1f);
					bool thorny = i % 5 == 2;
					switch (pass)
					{
						case 0:
							DrawRect(WhipLimb.Cell(p, g * 4f), Occ);
							for (int k = 1; cane && k <= 4; k++)
								DrawRect(WhipLimb.Cell(p + caneDir * g * 2f * k, g * 3f), Occ);
							if (thorny)
								DrawRect(WhipLimb.Cell(p + outward * g * 3f, g * 2f), Occ);
							break;
						case 1:
							DrawRect(WhipLimb.Cell(p, g * 3f), i % 9 == 4 ? Dried(Bark, GoldDeep, wither, i * 3 + s) : Dried(GrassEdge, GoldShade, wither, i * 3 + s));
							DrawRect(WhipLimb.Cell(p + new Vector2(-g, -g), g), Dried(GrassMid, Bark, wither, i * 3 + s + 1));
							for (int k = 1; cane && k <= 4; k++)
								DrawRect(WhipLimb.Cell(p + caneDir * g * 2f * k, g * 2f), Dried(GrassEdge, GoldShade, wither, i * 3 + s + k * 5));
							break;
						default:
							if (thorny)
							{
								DrawRect(WhipLimb.Cell(p + outward * g * 2.5f, g), Dried(GrassMid, Bark, wither, i * 3 + s + 2));
								DrawRect(WhipLimb.Cell(p + outward * g * 3.5f, g), Dried(GrassHot, EarthMid, wither, i * 3 + s + 3));
							}
							if (cane)
								DrawRect(WhipLimb.Cell(p + caneDir * g * 9f, g), Dried(GrassHot, EarthMid, wither, i * 3 + s + 4));
							// A poison bud now and then on the outer stems.
							if (s > 0 && i % 13 == 6)
							{
								DrawRect(WhipLimb.Cell(p + new Vector2(0f, -g * 2.5f), g * 3f), Occ);
								DrawRect(WhipLimb.Cell(p + new Vector2(0f, -g * 2.5f), g * 2f), Dried(PoisonMid, Bark, wither, i * 3 + s + 6));
							}
							break;
					}
				}
			}
		}
	}

	// A tone chosen per stamp between the living one and the dried one: the wither eats the green stamp by
	// stamp, in an order fixed per stamp, rather than lerping it toward a brown no ramp holds (2026-10-29).
	private static Color Dried(Color living, Color dried, float wither, int stamp) =>
		Mathf.PosMod(stamp * 0.6180339f, 1f) < wither ? dried : living;
}
