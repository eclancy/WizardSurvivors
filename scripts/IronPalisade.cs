using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// Iron Palisade: a line of stakes driven up between the player and the nearest pack (.ai/side-events.md,
// issue #64). Carried by the caravan master on the Scorched Sands road.
//
// **The wall.** The one delivery shape #64 still listed as missing: something that occupies space
// against the swarm. It is not a physics body - a body on a layer the enemies collide with would
// need a new layer and every enemy mask changed to match (CLAUDE.md, collision layers), and it would
// stop the player too. Instead anything inside the line is shoved back out the far side every tick
// and cut once per CutIntervalSeconds, which holds a front exactly as a fence would while letting
// the player walk through their own wall.
//
// **Pure Metal.** Metal had no pure spell, so it could never reach its capstone.
//
// The shove rides Enemy.ApplyKnockback, like Hollow Star's pull, so boss knockback resistance
// applies without a special case.
//
// **Lightning Rod** (the level 4 element path into Lightning, ElementPaths): the stakes' lit edge is
// repainted in Lightning's ramp and sparks crawl off their tips; every cut staggers (the rider,
// applied by Player.DealDamageToEnemy).
//
// **Its own four cards** (2026-10-29, .ai/spell-roster.md section 2e), each a change to the wall you can
// see. The ids are the template's.
//   - Spiked Rail (level 4): a spiked rail runs along the stakes; what touches the wall is thrown back
//     RailThrow times as hard and bleeds - drops falling from it - for BleedSeconds.
//   - Caltrops (level 4): when the wall comes down its stakes break into caltrops strewn on its far side,
//     which cut and slow whatever walks over them for CaltropSeconds. The proposal's Quick Raise (the
//     wall raised in a ring round you where you stop) is Sundering's verb, the Geomancer's Apotheosis
//     ("hold": a ring round you that shoves the near ones back), so the card leaves something behind instead.
//   - Iron Ring (level 8): the stakes close into a ring round the nearest pack: what is inside is
//     shoved back in, what is outside kept out, and whatever leans on the stakes is cut.
//   - Rolling Wall (level 8): the wall marches forward at a walk once it is up, shoving what it meets
//     ahead of it and cutting it as it goes. It halts at water and walls.
public partial class IronPalisade : Node2D, IPaintsElementPath
{
	public const string SpikedRailId = "iron_palisade_amplified_impact";
	public const string CaltropsId = "iron_palisade_hyper_acceleration";
	public const string IronRingId = "iron_palisade_titan_cataclysm";
	public const string RollingWallId = "iron_palisade_infinite_horizon";

	// Spiked Rail: how much harder the wall throws, and the bleed - a share of a cut, every BleedTick
	// seconds for BleedSeconds after the last cut.
	public const float RailThrow = 1.8f;
	public const float BleedShare = 0.25f;
	public const float BleedSeconds = 2f;
	private const float BleedTick = 0.5f;
	private const int MaxBleeding = 32;

	// Caltrops: how long they lie, how deep a strip of ground past the wall they cover, what each tick
	// over them cuts for (a share of a cut) and how hard they slow.
	public const float CaltropSeconds = 3.5f;
	public const float CaltropDepth = 56f;
	public const float CaltropShare = 0.75f;
	public const float CaltropSlow = 0.5f;
	private const int MaxCaltrops = 48;

	// Iron Ring: its radius as a share of the wall's half-length - the line's stakes and more, bent round.
	public const float RingRadiusShare = 1.4f;

	// Rolling Wall: how fast it marches.
	public const float MarchSpeed = 75f;

	[Export] public float HalfLength { get; set; } = 80f;
	[Export] public float HalfThickness { get; set; } = 14f;
	[Export] public float LifeSeconds { get; set; } = 4f;
	[Export] public float ShoveSpeed { get; set; } = 240f;
	[Export] public float CutIntervalSeconds { get; set; } = 0.5f;
	[Export] public float RiseSeconds { get; set; } = 0.15f;

	public int Damage { get; set; } = 8;
	public SpellData SpellData { get; set; }
	public Player PlayerRef { get; set; }

	/// <summary>Unit vector pointing away from the player, across the wall. Set before adding.</summary>
	public Vector2 Facing { get; set; } = Vector2.Right;

	/// <summary>Iron Ring: the stakes stand on a circle of this radius round the node, not along a line.</summary>
	public float RingRadius { get; set; }
	private bool IsRing => RingRadius > 0f;

	private float age;
	private float tickTimer;
	// Instance id -> the age at which it may be cut again. Never pruned: a wall lives four seconds
	// and meets a few dozen enemies, and pruning would cost an allocation per frame.
	private readonly Dictionary<ulong, float> nextCutAt = new();

	// The cards, read once from the spell in _Ready.
	private bool spikedRail;
	private bool caltrops;
	private bool rolling;

	// Spiked Rail's bleeding: who, how long left, and when each next bleeds. Swap-removed, never resized.
	private readonly Enemy[] bleeder = new Enemy[MaxBleeding];
	private readonly float[] bleedLeft = new float[MaxBleeding];
	private readonly float[] bleedNext = new float[MaxBleeding];
	private int bleedCount;

	// Caltrops, strewn when the wall falls: where each lies (local), and the age they were strewn at.
	private readonly Vector2[] caltropAt = new Vector2[MaxCaltrops];
	private int caltropCount;
	private float strewnAt = -1f;
	private Node2D ground;

	// Iron Ring: which stakes stand on blocked ground (water, a wall) and are not raised. Made in _Ready.
	private bool[] ringStakeBlocked;
	private int ringStakes;

	private const int StakeCount = 9;
	// The base wall's spacing: nine stakes along 160 px.
	private const float StakeSpacing = 20f;
	// THE LOOK (2026-10-29, the pixel-art pass: "does not look up to our typical visual fidelity"). Every
	// stake was a DrawLine in a grey no palette holds, rotated with the wall, over a translucent black
	// disc; the rail and its spikes thin smooth lines; the caltrops four lines each; the blood a red at 95%.
	// Now everything is drawn in WORLD coordinates (this node turns with the wall, so _Draw undoes its
	// transform first) on the 2 px art grid, in the steel row (.ai/art-direction.md section 3), lit from
	// the upper left as everything is: a stake is three columns of art pixels - lit, base, shade - with a
	// point catching the key, an iron band, and occlusion at its foot on the side away from the light.
	// The rail and its spikes are SpellStrokes on a child (`rail`), a bar shaded by the same key; the
	// caltrops are little iron caltrops of pixels; the bleeding is drops of the ember row. Every fade -
	// the wall going, the caltrops going, a bleed running out - is SpellInk's dither, never a translucency.
	// The stakes stand where they stood; they are a little taller (26 and 20 px against 22 and 17 - a
	// height the hit never read: the wall holds the band either side of its line) so that, drawn at a
	// real art pixel's width, a palisade reads as one rather than as a row of pegs, and the rail runs
	// behind them at two-thirds of that height.
	private static readonly Color SteelHi = new Color(0.863f, 0.910f, 0.957f);     // steel hi #DCE8F4
	private static readonly Color SteelLit = new Color(0.722f, 0.784f, 0.863f);    // steel lit #B8C8DC
	private static readonly Color SteelBase = new Color(0.416f, 0.494f, 0.596f);   // steel base #6A7E98
	private static readonly Color SteelShade = new Color(0.227f, 0.290f, 0.384f);  // steel shade #3A4A62
	private static readonly Color SteelDeep = new Color(0.125f, 0.157f, 0.220f);   // steel deep #202838
	private static readonly Color Occ = new Color(0.020f, 0.027f, 0.047f);         // #05070C
	private static readonly Color BloodFresh = new Color(0.847f, 0.141f, 0.180f);  // ember base #D8242E
	private static readonly Color Blood = new Color(0.541f, 0.071f, 0.125f);       // ember shade #8A1220
	// The lit side of a stake; an element path repaints it in its own ramp and lights sparks.
	private Color ironLit = SteelLit;
	private Color spark = new Color(0f, 0f, 0f, 0f);
	private Color sparkHot = new Color(0f, 0f, 0f, 0f);
	private Node2D rail;
	private SpellStrokes railStrokes;

	private bool Standing => age < LifeSeconds;

	public override void _Ready()
	{
		// The stakes stand up over the bodies they hold (DrawLayers.Air: what strikes, a marching wall);
		// the caltrops a fallen wall leaves lie on the floor under them, on a node of their own.
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		Rotation = IsRing ? 0f : Facing.Angle();
		if (SpellData?.GetBranchElement() is Element branch)
		{
			ElementColors.Ramp ramp = ElementColors.GetRamp(branch);
			ironLit = ramp.Mid;
			spark = ramp.Core;
			sparkHot = ramp.Hot;
		}
		if (SpellData != null)
		{
			spikedRail = SpellData.HasEvolution(SpikedRailId);
			caltrops = SpellData.HasEvolution(CaltropsId);
			rolling = !IsRing && SpellData.HasEvolution(RollingWallId);
		}
		if (spikedRail)
			ShoveSpeed *= RailThrow;
		if (IsRing)
			ringStakes = Math.Max(12, Mathf.RoundToInt(Mathf.Tau * RingRadius / StakeSpacing));
		if (caltrops)
		{
			ground = new Node2D { Name = "Caltrops", ZAsRelative = false, ZIndex = DrawLayers.Ground, Material = SpellInk.DitherByCell };
			AddChild(ground);
			ground.Draw += DrawCaltrops;
		}
		// The stakes, the sparks and the blood: cells on the world's grid, faded by dither.
		Material = SpellInk.DitherByCell;
		if (spikedRail)
		{
			railStrokes = new SpellStrokes((IsRing ? ringStakes : MaxLineStakes) * 2 + 2);
			railStrokes.SetTones(SteelHi, SteelLit, SteelBase, SteelShade, SteelDeep, Occ);
			// Behind the stakes it is run along, at their layer: the stakes are what hits, so they stay
			// whole in front of it and the rail shows between them.
			rail = new Node2D { Name = "Rail", ShowBehindParent = true, Material = railStrokes.Material };
			AddChild(rail);
			rail.Draw += DrawRail;
		}
	}

	// The most stakes a line wall stands, for the rail's buffer: a long wall at the old spacing.
	private const int MaxLineStakes = 64;

	private Vector2 RingDir(int stake)
	{
		float a = stake * Mathf.Tau / Math.Max(1, ringStakes);
		return new Vector2(MathF.Cos(a), MathF.Sin(a));
	}

	// Which of the ring's stakes would stand on water or a wall, once the ring is where it was raised
	// (the caster places it after adding it, so not in _Ready).
	private void SurveyRing()
	{
		ringStakeBlocked = new bool[ringStakes];
		MazeNavigation nav = MazeNavigation.Active;
		for (int i = 0; i < ringStakes && nav != null; i++)
			ringStakeBlocked[i] = nav.IsBlockedAt(GlobalPosition + RingDir(i) * RingRadius);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		age += dt;
		QueueRedraw();
		ground?.QueueRedraw();
		rail?.QueueRedraw();
		if (IsRing && ringStakeBlocked == null)
			SurveyRing();

		if (!Standing)
		{
			if (caltrops && strewnAt < 0f)
				Strew();
			bool caltropsLie = strewnAt >= 0f && age - strewnAt < CaltropSeconds && caltropCount > 0;
			if (!caltropsLie && bleedCount == 0)
			{
				QueueFree();
				return;
			}
		}
		else if (rolling && age >= RiseSeconds)
		{
			March(dt);
		}

		tickTimer -= dt;
		if (tickTimer > 0f || age < RiseSeconds)
			return;
		tickTimer = 0.1f;

		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;
		if (Standing)
			HoldTheLine();
		if (strewnAt >= 0f)
			TreadOnCaltrops();
		if (bleedCount > 0)
			Bleed();
	}

	// Rolling Wall: forward at a walk, unless the ground ahead of its middle is water or a wall.
	private void March(float dt)
	{
		Vector2 next = GlobalPosition + Facing * MarchSpeed * dt;
		if (MazeNavigation.Active?.IsBlockedAt(next + Facing * (HalfThickness + 6f)) == true)
			return;
		GlobalPosition = next;
	}

	private void HoldTheLine()
	{
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy enemy || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;

			Vector2 shove;
			if (IsRing)
			{
				// On the ring: inside it is shoved back in, outside it is kept out. A stake not raised
				// (blocked ground) holds nothing.
				Vector2 offset = enemy.GlobalPosition - GlobalPosition;
				float d = offset.Length();
				if (MathF.Abs(d - RingRadius) > HalfThickness + 10f || d < 1f)
					continue;
				int stake = Mathf.PosMod(Mathf.RoundToInt(MathF.Atan2(offset.Y, offset.X) / Mathf.Tau * ringStakes), ringStakes);
				if (ringStakeBlocked[stake])
					continue;
				shove = (d < RingRadius ? -offset : offset) / d * ShoveSpeed;
			}
			else
			{
				// In the wall's own frame: x across the wall (along Facing), y along it. A rolling wall also
				// takes what is just ahead of its face, so the pack is carried before it rather than
				// stepped through.
				Vector2 local = ToLocal(enemy.GlobalPosition);
				float ahead = rolling ? 18f : 0f;
				if (local.X < -(HalfThickness + 10f) || local.X > HalfThickness + 10f + ahead || Mathf.Abs(local.Y) > HalfLength + 8f)
					continue;
				shove = Facing * (rolling ? MathF.Max(ShoveSpeed, MarchSpeed * 1.6f) : ShoveSpeed);
			}

			enemy.ApplyKnockback(shove);
			ulong id = enemy.GetInstanceId();
			if (nextCutAt.TryGetValue(id, out float readyAt) && age < readyAt)
				continue;
			nextCutAt[id] = age + CutIntervalSeconds;
			PlayerRef.DealDamageToEnemy(enemy, Damage, source: SpellData);
			if (spikedRail && IsInstanceValid(enemy) && !enemy.IsDying)
				StartBleeding(enemy);
		}
	}

	// --- Spiked Rail -----------------------------------------------------------------------------

	private void StartBleeding(Enemy enemy)
	{
		for (int i = 0; i < bleedCount; i++)
		{
			if (bleeder[i] == enemy)
			{
				bleedLeft[i] = BleedSeconds;
				return;
			}
		}
		if (bleedCount >= MaxBleeding)
			return;
		bleeder[bleedCount] = enemy;
		bleedLeft[bleedCount] = BleedSeconds;
		bleedNext[bleedCount] = BleedTick;
		bleedCount++;
	}

	private void Bleed()
	{
		int bite = Math.Max(1, Mathf.RoundToInt(Damage * BleedShare));
		for (int i = bleedCount - 1; i >= 0; i--)
		{
			Enemy enemy = bleeder[i];
			bool gone = enemy == null || !IsInstanceValid(enemy) || enemy.IsDying;
			if (!gone)
			{
				bleedLeft[i] -= 0.1f;
				bleedNext[i] -= 0.1f;
				if (bleedNext[i] <= 0f)
				{
					bleedNext[i] += BleedTick;
					PlayerRef.DealDamageToEnemy(enemy, bite, source: SpellData);
				}
			}
			if (gone || bleedLeft[i] <= 0f || !IsInstanceValid(enemy) || enemy.IsDying)
			{
				bleedCount--;
				bleeder[i] = bleeder[bleedCount];
				bleedLeft[i] = bleedLeft[bleedCount];
				bleedNext[i] = bleedNext[bleedCount];
				bleeder[bleedCount] = null;
			}
		}
	}

	// --- Caltrops --------------------------------------------------------------------------------

	// The strip they lie in: past the line on its far side, or inside the ring. Scattered evenly through
	// it, so where they are drawn is where they cut; none drawn on water or a wall.
	private void Strew()
	{
		strewnAt = age;
		caltropCount = 0;
		var rng = new RandomNumberGenerator { Seed = (ulong)GetInstanceId() };
		MazeNavigation nav = MazeNavigation.Active;
		float band = CaltropDepth - HalfThickness;
		float length = IsRing ? Mathf.Tau * (RingRadius - HalfThickness - band * 0.5f) : HalfLength * 2f;
		int count = Math.Clamp(Mathf.RoundToInt(length * band / 260f), 6, MaxCaltrops);
		for (int i = 0; i < count; i++)
		{
			Vector2 local;
			if (IsRing)
			{
				float a = (i + rng.Randf() * 0.8f) * Mathf.Tau / count;
				float d = RingRadius - HalfThickness - rng.Randf() * band;
				local = new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
			}
			else
			{
				float y = Mathf.Lerp(-HalfLength, HalfLength, (i + rng.Randf() * 0.8f) / count);
				local = new Vector2(HalfThickness + rng.Randf() * band, y);
			}
			if (nav != null && nav.IsBlockedAt(ToGlobal(local)))
				continue;
			caltropAt[caltropCount++] = local;
		}
	}

	private bool OnCaltrops(Enemy enemy)
	{
		if (IsRing)
		{
			float d = GlobalPosition.DistanceTo(enemy.GlobalPosition);
			return d <= RingRadius - HalfThickness + 6f && d >= RingRadius - CaltropDepth - 6f;
		}
		Vector2 local = ToLocal(enemy.GlobalPosition);
		return local.X >= HalfThickness - 6f && local.X <= CaltropDepth + 6f && Mathf.Abs(local.Y) <= HalfLength + 6f;
	}

	// Every half second while they lie: a cut and a slow for what stands on them.
	private float caltropTimer;
	private void TreadOnCaltrops()
	{
		if (age - strewnAt >= CaltropSeconds || caltropCount == 0)
			return;
		caltropTimer -= 0.1f;
		if (caltropTimer > 0f)
			return;
		caltropTimer = 0.5f;
		int cut = Math.Max(1, Mathf.RoundToInt(Damage * CaltropShare));
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy enemy || !IsInstanceValid(enemy) || enemy.IsDying || !OnCaltrops(enemy))
				continue;
			PlayerRef.DealDamageToEnemy(enemy, cut, source: SpellData);
			if (IsInstanceValid(enemy) && !enemy.IsDying)
				enemy.ApplySlow(CaltropSlow, 0.7f);
		}
	}

	// A caltrop: four iron points, one always up, the three on the ground lit and shaded by the key and
	// occlusion under the side away from it; mirrored every other one, so a scatter does not repeat.
	// Drawn in world coordinates on the 2 px grid, faded by dither as they go.
	private void DrawCaltrops()
	{
		if (strewnAt < 0f || ground == null)
			return;
		float since = age - strewnAt;
		if (since >= CaltropSeconds)
			return;
		float fade = Mathf.Clamp((CaltropSeconds - since) / 0.4f, 0f, 1f);
		// Scattered out from the stakes' feet over the first fifth of a second, as the wall breaks.
		float thrown = Mathf.Clamp(since / 0.2f, 0f, 1f);
		ground.DrawSetTransformMatrix(ground.GetGlobalTransform().AffineInverse());
		for (int i = 0; i < caltropCount; i++)
		{
			Vector2 rest = caltropAt[i];
			Vector2 from = IsRing ? rest.Normalized() * RingRadius : new Vector2(0f, rest.Y);
			Vector2 at = SpellInk.Snap(ToGlobal(from.Lerp(rest, thrown)));
			float m = i % 2 == 0 ? 1f : -1f;
			Px(ground, at, 0, -2, ironLit, fade);
			Px(ground, at, 0, -1, SteelBase, fade);
			Px(ground, at, -m, 0, i % 2 == 0 ? ironLit : SteelShade, fade);
			Px(ground, at, 0, 0, SteelHi, fade);
			Px(ground, at, m, 0, i % 2 == 0 ? SteelShade : SteelBase, fade);
			Px(ground, at, m, 1, Occ, fade);
			Px(ground, at, 0, 1, Occ, fade);
		}
	}

	// One art pixel, (ax, ay) art pixels from a point on the world's grid.
	private static void Px(CanvasItem canvas, Vector2 at, float ax, float ay, Color c, float coverage) =>
		canvas.DrawRect(new Rect2(at.X + ax * SpellInk.Grid, at.Y + ay * SpellInk.Grid, SpellInk.Grid, SpellInk.Grid),
			new Color(c, coverage));

	// --- the stakes ------------------------------------------------------------------------------

	public override void _Draw()
	{
		// The wall turns to face the pack; its stakes stand up on the screen. Draw in the world.
		DrawSetTransformMatrix(GetGlobalTransform().AffineInverse());
		if (!Standing)
		{
			DrawBleeding();
			return;
		}
		float rise = Mathf.Clamp(age / RiseSeconds, 0f, 1f);
		float fade = Mathf.Clamp((LifeSeconds - age) / 0.3f, 0f, 1f);
		if (IsRing)
		{
			if (ringStakeBlocked == null)
				return;
			for (int i = 0; i < ringStakes; i++)
			{
				if (ringStakeBlocked[i])
					continue;
				DrawStake(ToGlobal(RingDir(i) * RingRadius), i, rise, fade);
			}
		}
		else
		{
			// A longer wall is more stakes at the same spacing, not the same nine spread thin: the stakes
			// are the thing that hits (.ai/spell-roster.md, section 2c), so a gap drawn in the fence would
			// say enemies walk through where they do not.
			int stakes = LineStakes;
			for (int i = 0; i < stakes; i++)
				DrawStake(ToGlobal(LineFoot(i, stakes)), i, rise, fade);
		}
		DrawBleeding();
	}

	private const float StakeTall = 26f;
	private const float StakeShort = 20f;
	private const float RailHeight = 15f;

	private int LineStakes => Math.Max(StakeCount, Mathf.RoundToInt(HalfLength * 2f / StakeSpacing) + 1);

	// A line stake's foot, in the wall's frame. Stakes stagger slightly so the line reads as driven by
	// hand, not drawn with a ruler.
	private Vector2 LineFoot(int i, int stakes) =>
		new Vector2(i % 2 == 0 ? -3f : 3f, Mathf.Lerp(-HalfLength, HalfLength, i / (float)(stakes - 1)));

	// One stake, upright on the screen whatever the wall's turn: three columns of art pixels - the key
	// side lit, the middle base, the far side shade - its point the steel's hi, an iron band a third of
	// the way up, and occlusion at the foot on the side away from the key. 26 px tall every third stake
	// and 20 the rest (StakeTall, StakeShort); it rises out of the ground over RiseSeconds.
	private void DrawStake(Vector2 footWorld, int i, float rise, float fade)
	{
		float height = (i % 3 == 0 ? StakeTall : StakeShort) * rise;
		// Rolling Wall: the stakes heave in turn as the wall walks, a stamp running down the line.
		if (rolling && rise >= 1f)
			height -= 3f * MathF.Max(0f, MathF.Sin(age * 9f + i * 1.3f));
		int rows = Math.Max(1, Mathf.RoundToInt(height / SpellInk.Grid));
		Vector2 foot = SpellInk.Snap(footWorld);
		float g = SpellInk.Grid;
		float top = foot.Y - rows * g;
		// The contact: occlusion under the foot and off to the lower right.
		DrawRect(new Rect2(foot.X - g, foot.Y, g * 4f, g), new Color(Occ, fade));
		// The three columns, cut to a slanted point: the lit side stops one art pixel below the tip, the
		// shaded side two, so the top reads as a sharpened stake and not as a peg.
		if (rows > 1)
			DrawRect(new Rect2(foot.X - g, top + g, g, rows * g - g), new Color(ironLit, fade));
		if (rows > 2)
			DrawRect(new Rect2(foot.X + g, top + 2f * g, g, rows * g - 2f * g), new Color(SteelShade, fade));
		DrawRect(new Rect2(foot.X, top, g, rows * g), new Color(SteelBase, fade));
		// The point catching the key, and the band a third of the way up.
		DrawRect(new Rect2(foot.X, top, g, g), new Color(SteelHi, fade));
		if (rows >= 5)
		{
			float band = foot.Y - Mathf.RoundToInt(rows * 0.34f) * g - g;
			DrawRect(new Rect2(foot.X - g, band, g, g), new Color(SteelBase, fade));
			DrawRect(new Rect2(foot.X, band, g, g), new Color(SteelShade, fade));
			DrawRect(new Rect2(foot.X + g, band, g, g), new Color(SteelDeep, fade));
		}

		// Lightning Rod: a spark crawling off the tip, on a few stakes at a time, re-struck ten
		// times a second: art pixels zig-zagging up from the point, white-hot at the ends.
		if (spark.A > 0f && rise >= 1f && ((int)(age * 10f) + i) % 3 == 0)
		{
			float jag = ((int)(age * 10f) + i * 7) % 2 == 0 ? 1f : -1f;
			Vector2 tip = new Vector2(foot.X, top);
			Px(this, tip, 0, -1, sparkHot, fade);
			Px(this, tip, jag, -2, spark, fade);
			Px(this, tip, 0, -3, sparkHot, fade);
			Px(this, tip, -jag, -4, spark, fade);
		}
	}

	// Spiked Rail: an iron bar run along the stakes at two-thirds of their height (RailHeight), with a spike
	// thrust out toward the enemy side between each pair - SpellStrokes, shaded by the key: the bar's top
	// lit, its underside in shade. Along the line in its own frame; round the ring segment by segment,
	// skipping the gaps where a stake was not raised. Drawn in world coordinates, on the rail's child.
	private void DrawRail()
	{
		if (railStrokes == null || !Standing)
			return;
		float rise = Mathf.Clamp(age / RiseSeconds, 0f, 1f);
		float fade = Mathf.Clamp((LifeSeconds - age) / 0.3f, 0f, 1f);
		Vector2 up = new Vector2(0f, -RailHeight * rise);
		float gone = 1f - fade;
		railStrokes.Begin();
		if (IsRing)
		{
			if (ringStakeBlocked == null)
				return;
			for (int i = 0; i < ringStakes; i++)
			{
				int next = (i + 1) % ringStakes;
				if (ringStakeBlocked[i] || ringStakeBlocked[next])
					continue;
				Vector2 a = ToGlobal(RingDir(i) * RingRadius) + up;
				Vector2 b = ToGlobal(RingDir(next) * RingRadius) + up;
				Bar(a, b, gone);
				// Spikes point both ways on a ring: in at the penned, out at the rest.
				Vector2 mid = (a + b) * 0.5f;
				Vector2 outward = RingDir(i).Lerp(RingDir(next), 0.5f).Normalized();
				Spike(mid, mid + outward * (i % 2 == 0 ? 8f : -8f), gone);
			}
		}
		else
		{
			Vector2 a = ToGlobal(new Vector2(0f, -HalfLength)) + up;
			Vector2 b = ToGlobal(new Vector2(0f, HalfLength)) + up;
			Bar(a, b, gone);
			int spikes = Math.Max(4, Mathf.RoundToInt(HalfLength * 2f / StakeSpacing));
			for (int i = 0; i < spikes; i++)
			{
				Vector2 at = ToGlobal(new Vector2(0f, Mathf.Lerp(-HalfLength, HalfLength, (i + 0.5f) / spikes))) + up;
				Spike(at, at + Facing * 9f, gone);
			}
		}
		railStrokes.Draw(rail, Colors.White);
	}

	private void Bar(Vector2 a, Vector2 b, float gone) => railStrokes.Add(new SpellStrokes.Stroke
	{
		A = a, B = b, HalfA = 2.5f, HalfB = 2.5f, SpineHalf = 0.5f, Spine = 2, Body = 2, Lit = true, Dissolve = gone,
	});

	private void Spike(Vector2 a, Vector2 b, float gone) => railStrokes.Add(new SpellStrokes.Stroke
	{
		A = a, B = b, HalfA = 1.5f, HalfB = 0.5f, Spine = 1, Body = 2, Lit = true, Dissolve = gone,
	});

	// Spiked Rail: blood dripping from what is bleeding - two drops at a time falling down the body, the
	// ember row's base as it leaves and its shade as it falls, going out by dither as the bleed runs out.
	private void DrawBleeding()
	{
		for (int i = 0; i < bleedCount; i++)
		{
			Enemy enemy = bleeder[i];
			if (enemy == null || !IsInstanceValid(enemy))
				continue;
			Vector2 body = enemy.GlobalPosition;
			float alpha = MathF.Min(1f, bleedLeft[i] / 0.4f);
			for (int k = 0; k < 2; k++)
			{
				float fall = (age * 1.8f + k * 0.5f + i * 0.37f) % 1f;
				Vector2 drop = new Vector2(k == 0 ? -4f : 3f, -10f + 16f * fall);
				DrawRect(WhipLimb.Cell(body + drop, WhipLimb.Grid * (fall < 0.7f ? 2f : 1f)),
					new Color(fall < 0.35f ? BloodFresh : Blood, alpha * (1f - fall * 0.5f)));
			}
		}
	}
}
