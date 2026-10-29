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
	private static readonly Color Iron = new Color(0.34f, 0.36f, 0.42f);
	private static readonly Color Shadow = new Color(0f, 0f, 0f, 0.35f);
	private static readonly Color Blood = new Color(0.62f, 0.08f, 0.10f, 0.95f);
	// The lit edge of a stake; an element path repaints it in its own ramp and lights sparks.
	private Color ironLit = new Color(0.72f, 0.74f, 0.80f);
	private Color spark = new Color(0f, 0f, 0f, 0f);

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
			ground = new Node2D { Name = "Caltrops", ZAsRelative = false, ZIndex = DrawLayers.Ground };
			AddChild(ground);
			ground.Draw += DrawCaltrops;
		}
	}

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

	// Four iron points, one always up, each with its shadow: the shape of a caltrop at any turn.
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
		Color lit = new Color(ironLit, fade);
		Color dark = new Color(Iron, fade);
		Color shade = new Color(Shadow, Shadow.A * fade);
		float rot = IsRing ? 0f : Rotation;
		for (int i = 0; i < caltropCount; i++)
		{
			Vector2 rest = caltropAt[i];
			Vector2 from = IsRing ? rest.Normalized() * RingRadius : new Vector2(0f, rest.Y);
			// The ground node is a child of a rotated wall: draw in its frame, points kept upright on screen.
			Vector2 at = from.Lerp(rest, thrown);
			Vector2 up = new Vector2(0f, -1f).Rotated(-rot);
			Vector2 side = new Vector2(1f, 0f).Rotated(-rot + i);
			ground.DrawCircle(at + new Vector2(1.5f, 1.5f).Rotated(-rot), 3f, shade);
			ground.DrawLine(at, at + up * 5f, dark, 2f);
			ground.DrawLine(at, at + side * 4f, dark, 2f);
			ground.DrawLine(at, at - side.Rotated(0.9f) * 4f, dark, 2f);
			ground.DrawLine(at + new Vector2(-0.5f, 0f).Rotated(-rot), at + up * 5f + new Vector2(-0.5f, 0f).Rotated(-rot), lit, 1f);
		}
	}

	// --- the stakes ------------------------------------------------------------------------------

	public override void _Draw()
	{
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
				DrawStake(RingDir(i) * RingRadius, i, rise, fade);
			}
			if (spikedRail)
				DrawRingRail(rise, fade);
		}
		else
		{
			// A longer wall is more stakes at the same spacing, not the same nine spread thin: the stakes
			// are the thing that hits (.ai/spell-roster.md, section 2c), so a gap drawn in the fence would
			// say enemies walk through where they do not.
			int stakes = Math.Max(StakeCount, Mathf.RoundToInt(HalfLength * 2f / StakeSpacing) + 1);
			for (int i = 0; i < stakes; i++)
			{
				float y = Mathf.Lerp(-HalfLength, HalfLength, i / (float)(stakes - 1));
				// Stakes stagger slightly so the line reads as driven by hand, not drawn with a ruler.
				float x = (i % 2 == 0 ? -3f : 3f);
				DrawStake(new Vector2(x, y), i, rise, fade);
			}
			if (spikedRail)
				DrawLineRail(rise, fade);
		}
		DrawBleeding();
	}

	private void DrawStake(Vector2 foot, int i, float rise, float fade)
	{
		float height = (i % 3 == 0 ? 22f : 17f) * rise;
		// Rolling Wall: the stakes heave in turn as the wall walks, a stamp running down the line.
		if (rolling && rise >= 1f)
			height -= 3f * MathF.Max(0f, MathF.Sin(age * 9f + i * 1.3f));
		DrawCircle(foot + new Vector2(2f, 2f), 4f, new Color(Shadow, Shadow.A * fade));
		// Drawn "up" in screen terms regardless of rotation, so the stakes always stand upright.
		Vector2 up = new Vector2(0f, -1f).Rotated(-Rotation) * height;
		DrawLine(foot, foot + up, new Color(Iron, fade), 5f);
		DrawLine(foot + new Vector2(-1f, 0f), foot + up + new Vector2(-1f, 0f), new Color(ironLit, fade), 1.5f);

		// Lightning Rod: a spark crawling off the tip, on a few stakes at a time, re-struck ten
		// times a second. Three short jags in the screen's up, no taller than the stake is wide.
		if (spark.A > 0f && rise >= 1f && ((int)(age * 10f) + i) % 3 == 0)
		{
			Vector2 screenUp = new Vector2(0f, -1f).Rotated(-Rotation);
			Vector2 screenSide = new Vector2(1f, 0f).Rotated(-Rotation);
			float jag = ((int)(age * 10f) + i * 7) % 2 == 0 ? 2.5f : -2.5f;
			Vector2 tip = foot + up;
			Vector2 a = tip + screenUp * 3f + screenSide * jag;
			Vector2 b = a + screenUp * 3f - screenSide * jag * 1.4f;
			Color lit = new Color(spark, fade);
			DrawLine(tip, a, lit, 1.5f);
			DrawLine(a, b, lit, 1.5f);
		}
	}

	// Spiked Rail: an iron bar run along the stakes at two-thirds of their height, with a spike thrust out
	// toward the enemy side between each pair. Along the line in its own frame; round the ring segment
	// by segment, skipping the gaps where a stake was not raised.
	private void DrawLineRail(float rise, float fade)
	{
		Vector2 up = new Vector2(0f, -1f).Rotated(-Rotation) * 13f * rise;
		Vector2 a = new Vector2(0f, -HalfLength) + up;
		Vector2 b = new Vector2(0f, HalfLength) + up;
		DrawLine(a, b, new Color(Iron, fade), 3f);
		DrawLine(a + new Vector2(0f, -0.5f).Rotated(-Rotation), b + new Vector2(0f, -0.5f).Rotated(-Rotation), new Color(ironLit, fade), 1f);
		int spikes = Math.Max(4, Mathf.RoundToInt(HalfLength * 2f / StakeSpacing));
		for (int i = 0; i < spikes; i++)
		{
			float y = Mathf.Lerp(-HalfLength, HalfLength, (i + 0.5f) / spikes);
			Vector2 at = new Vector2(0f, y) + up;
			DrawLine(at, at + new Vector2(9f, 0f), new Color(ironLit, fade), 2f);
		}
	}

	private void DrawRingRail(float rise, float fade)
	{
		Vector2 up = new Vector2(0f, -13f * rise);
		for (int i = 0; i < ringStakes; i++)
		{
			int next = (i + 1) % ringStakes;
			if (ringStakeBlocked[i] || ringStakeBlocked[next])
				continue;
			Vector2 a = RingDir(i) * RingRadius + up;
			Vector2 b = RingDir(next) * RingRadius + up;
			DrawLine(a, b, new Color(Iron, fade), 3f);
			// Spikes point both ways on a ring: in at the penned, out at the rest.
			Vector2 mid = (a + b) * 0.5f;
			Vector2 outward = RingDir(i).Lerp(RingDir(next), 0.5f).Normalized();
			DrawLine(mid, mid + outward * (i % 2 == 0 ? 8f : -8f), new Color(ironLit, fade), 2f);
		}
	}

	// Spiked Rail: blood dripping from what is bleeding - two drops at a time falling down the body.
	private void DrawBleeding()
	{
		for (int i = 0; i < bleedCount; i++)
		{
			Enemy enemy = bleeder[i];
			if (enemy == null || !IsInstanceValid(enemy))
				continue;
			Vector2 body = ToLocal(enemy.GlobalPosition);
			float alpha = MathF.Min(1f, bleedLeft[i] / 0.4f);
			for (int k = 0; k < 2; k++)
			{
				float fall = (age * 1.8f + k * 0.5f + i * 0.37f) % 1f;
				// Down the screen whatever the wall's turn.
				Vector2 drop = new Vector2((k == 0 ? -4f : 3f), -10f + 16f * fall).Rotated(-Rotation);
				DrawRect(WhipLimb.Cell(body + drop, WhipLimb.Grid * (fall < 0.7f ? 2f : 1f)), new Color(Blood, Blood.A * alpha * (1f - fall * 0.5f)));
			}
		}
	}
}
