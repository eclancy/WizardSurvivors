using Godot;
using System;
using WizardSurvivors.scripts;

// Bramble Seed: a seed dropped at the player's feet that grows into a thorn patch over several
// seconds (.ai/side-events.md). Grown back in the corrupted grove of the Enchanted Forest.
//
// **The payload nothing else has: it gets bigger the longer it lives.** Every zone in the game is
// at full size the moment it appears. A seed is small and harmless for its first seconds and wide
// and dangerous at the end, so its value is in where the player will be later rather than where
// they are now - plant, kite a loop, and drag the swarm back across what has grown.
//
// **Pure Grass.** Grass had no pure spell, so it could never reach its capstone.
//
// At most MaxAlive patches stand at once; a new seed past that withers the oldest.
//
// **Ironbriar** (the level 4 element path into Metal, ElementPaths): the living green of the roots,
// their thorns and tips are repainted in Metal's ramp - steel thorns on bark.
public partial class BrambleSeed : Node2D, IPaintsElementPath
{
	[Export] public float GrowSeconds { get; set; } = 6f;
	[Export] public float HoldSeconds { get; set; } = 3f;
	[Export] public float StartRadius { get; set; } = 16f;
	[Export] public float MaxRadius { get; set; } = 110f;
	[Export] public float TickSeconds { get; set; } = 0.5f;
	[Export] public float SlowMultiplier { get; set; } = 0.6f;
	// How long the slow outlasts the thicket once a body is out of it (the SlowDuration level line).
	public float SlowLingerSeconds { get; set; } = 0f;

	public const int MaxAlive = 3;

	/// <summary>
	/// Patches the player may have standing at once: MaxAlive, and one more for every count the spell
	/// has past its base (Infinite Horizon). Bounded by the count, which no level raises.
	/// </summary>
	public static int CapFor(SpellData spell) =>
		MaxAlive + Math.Max(0, (spell?.GetProjectileCountAtLevel(spell.CurrentLevel) ?? 1) - (spell?.BaseProjectileCount ?? 1));

	// Wildwood, Vainamoinen's Apotheosis (verb: swarm). A patch sown by the player is generation 0;
	// once it finishes growing it throws WildwoodSeeds of its own at its edge, generation 1, which
	// do not seed again. Bounded by construction: at most MaxAlive x (1 + WildwoodSeeds) patches.
	public int Generation { get; set; } = 0;
	private const int WildwoodSeeds = 3;
	private bool seeded;

	public int Damage { get; set; } = 7;
	public SpellData SpellData { get; set; }
	public Player PlayerRef { get; set; }

	private float age;
	private float tickTimer;

	/// <summary>
	/// The one hit authority. The tick below measures enemies against it and _Draw is built out of
	/// it, so the roots can never be drawn further than they hurt: this spell was once painted twice
	/// as wide as its damage (.ai/gameplay-design.md), and the fix only holds while both read here.
	/// </summary>
	public float Radius => Mathf.Lerp(StartRadius, MaxRadius, Mathf.Clamp(age / GrowSeconds, 0f, 1f));

	/// <summary>True once a newer seed has pushed this one out; it is pulling its roots back in.</summary>
	public bool IsWithering => witherAt >= 0f;

	/// <summary>True once the patch has stopped hurting - pushed out, or its hold over - and is only
	/// drawing its roots back in. Not counted against MaxAlive.</summary>
	public bool IsSpent => IsWithering || age >= GrowSeconds + HoldSeconds;

	// When the patch is gone: its hold, then RetractSeconds of the roots going back into the ground
	// AFTER it has stopped hurting - or WitherSeconds after it was pushed out. The roots used to start
	// retracting while the patch still hurt, and a disc at the full radius covered for them; with the
	// disc gone (no area markers), the roots have to be the honest extent to the last hurting frame.
	private float Lifetime => IsWithering
		? Mathf.Min(GrowSeconds + HoldSeconds + RetractSeconds, witherAt + WitherSeconds)
		: GrowSeconds + HoldSeconds + RetractSeconds;
	private float witherAt = -1f;

	public override void _Ready()
	{
		// Under every actor, over the ground decor (-20): roots come out of the floor, so the player and
		// the swarm stand on them. At 1, as the old disc was, the thicket was drawn over the wizard.
		ZIndex = -1;
		if (SpellData?.GetBranchElement() is Element branch)
			PaintElementPath(branch);
		BuildRoots();
	}

	/// <summary>
	/// Pushed out by a newer seed. It used to vanish on the frame the fourth seed landed; now the
	/// roots pull back into the ground over WitherSeconds, and it stops hurting at once so the
	/// three-patch cap holds exactly.
	/// </summary>
	public void Wither()
	{
		if (!IsWithering)
			witherAt = age;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		age += dt;
		QueueRedraw();
		if (age >= Lifetime)
		{
			QueueFree();
			return;
		}

		if (!seeded && Generation == 0 && age >= GrowSeconds && SpellData?.IsApotheosis == true)
		{
			seeded = true;
			Scatter();
		}

		tickTimer -= dt;
		if (tickTimer > 0f || IsSpent)
			return;
		tickTimer = TickSeconds;

		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;

		float radiusSquared = Radius * Radius;
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy enemy || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			if (GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition) > radiusSquared)
				continue;
			PlayerRef.DealDamageToEnemy(enemy, Damage, source: SpellData);
			enemy.ApplySlow(SlowMultiplier, TickSeconds + 0.2f + SlowLingerSeconds);
		}
	}

	private void Scatter()
	{
		var scene = GD.Load<PackedScene>(SceneFilePath);
		Node parent = GetParent();
		if (scene == null || parent == null)
			return;
		float turn = (float)GD.RandRange(0.0, Mathf.Tau);
		for (int i = 0; i < WildwoodSeeds; i++)
		{
			if (scene.Instantiate() is not BrambleSeed child)
				continue;
			child.Generation = Generation + 1;
			child.SpellData = SpellData;
			child.PlayerRef = PlayerRef;
			child.Damage = Damage;
			child.MaxRadius = MaxRadius * 0.8f;
			child.HoldSeconds = HoldSeconds;
			child.GrowSeconds = GrowSeconds;
			child.SlowMultiplier = SlowMultiplier;
			child.SlowLingerSeconds = SlowLingerSeconds;
			parent.AddChild(child);
			float a = turn + i * Mathf.Tau / WildwoodSeeds;
			child.GlobalPosition = GlobalPosition + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * MaxRadius * 1.6f;
		}
	}

	// --- the roots ----------------------------------------------------------------------------
	//
	// "It should look more like roots writhing out." The patch used to be a green disc with fourteen
	// static spokes at its edge, which read as a decal. Now it is thorned roots: they burst out of
	// the seed in a staggered half-second, writhe the whole time (a wave runs down each one from the
	// seed to the tip), whip now and then, and at the end pull back into the ground and dry out.
	//
	// Drawn, not a sprite sheet, for the reason Radius gives: a root is laid out in polar terms, its
	// distance from the seed running 0..length and length never more than Radius, so the writhing
	// bends a root sideways and can never carry it past the ground it hurts. The long roots end
	// exactly on Radius, which is what makes the edge of the thicket the edge of the damage - and the
	// only thing that does: the faint disc and rim that used to sit under them at the full radius
	// were an area marker, and went (2026-10-23). The roots now hold their full length until the
	// patch has stopped hurting, and only then pull back in (Lifetime).
	//
	// Everything per frame is written into arrays allocated once in BuildRoots. Up to twelve of
	// these stand at once under Wildwood, so _Draw must not allocate.

	private const int LongRoots = 6;
	private const int ShortRoots = 5;
	private const int RootCount = LongRoots + ShortRoots;
	private const int Segments = 10;
	// A root is three polylines, thick to thin: the base, the middle and the tip.
	private const int BaseEnd = 4;
	private const int MidEnd = 7;
	private const int ThornsPerRoot = 3;
	private const float WitherSeconds = 0.55f;
	// The last stretch of every life: roots draw back and dry out.
	private const float RetractSeconds = 0.6f;
	private const float DrySeconds = 1.1f;

	private readonly float[] rootAngle = new float[RootCount];
	private readonly float[] rootLength = new float[RootCount];
	private readonly float[] rootDelay = new float[RootCount];
	private readonly float[] rootPhase = new float[RootCount];
	private readonly float[] rootCurl = new float[RootCount];
	private readonly float[] rootWidth = new float[RootCount];
	private readonly float[] rootWhipEvery = new float[RootCount];
	private Vector2[][] baseRun;
	private Vector2[][] midRun;
	private Vector2[][] tipRun;
	private Vector2[] thornLines;
	private Vector2[] path;

	// Grass and Earth from the emissive table (.ai/art-direction.md section 3), plus the shared
	// contact shadow for the outline so a root reads on a mossy forest floor as well as on stone.
	private static readonly Color Occ = new Color(0.02f, 0.03f, 0.05f, 0.85f);
	private static readonly Color Soil = new Color(0.16f, 0.11f, 0.06f, 0.55f);
	private static readonly Color Bark = new Color(0.48f, 0.31f, 0.14f, 1f);        // Earth edge #7A5024
	private static readonly Color Dry = new Color(0.36f, 0.27f, 0.19f, 1f);
	private static readonly Color Clod = new Color(0.79f, 0.60f, 0.37f, 0.8f);      // Earth mid #C99A5E
	// The Grass half of the palette, repainted by an element path (PaintElementPath).
	private Color living = new Color(0.29f, 0.55f, 0.23f, 1f);                     // Grass edge #4A8C3A
	private Color thornTip = new Color(0.81f, 0.93f, 0.58f, 1f);                   // Grass hot #CFEE94
	private Color tipGlow = new Color(0.61f, 0.83f, 0.33f, 0.9f);                  // Grass mid #9BD455

	// Once, from _Ready: the same four slots in the element's ramp, so a branched patch reads as
	// that element at a glance and the bark beneath still says it is a root.
	private void PaintElementPath(Element element)
	{
		ElementColors.Ramp ramp = ElementColors.GetRamp(element);
		living = ramp.Edge;
		thornTip = ramp.Core;
		tipGlow = new Color(ramp.Hot, 0.9f);
	}

	private void BuildRoots()
	{
		// Seeded per patch so three patches side by side do not writhe in step.
		var rng = new RandomNumberGenerator { Seed = (ulong)GetInstanceId() };
		float turn = rng.Randf() * Mathf.Tau;
		for (int i = 0; i < RootCount; i++)
		{
			bool isLong = i < LongRoots;
			int slot = isLong ? i : i - LongRoots;
			int slots = isLong ? LongRoots : ShortRoots;
			float spread = Mathf.Tau / slots;
			rootAngle[i] = turn + slot * spread + (isLong ? 0f : spread * 0.5f) + rng.RandfRange(-0.18f, 0.18f);
			rootLength[i] = isLong ? 1f : rng.RandfRange(0.48f, 0.74f);
			rootDelay[i] = rng.RandfRange(0f, 0.32f) + (isLong ? 0f : 0.12f);
			rootPhase[i] = rng.Randf() * Mathf.Tau;
			rootCurl[i] = (rng.Randf() < 0.5f ? -1f : 1f) * rng.RandfRange(0.35f, 0.75f);
			rootWidth[i] = isLong ? rng.RandfRange(7.0f, 8.5f) : rng.RandfRange(4.8f, 5.8f);
			rootWhipEvery[i] = rng.RandfRange(1.5f, 2.6f);
		}

		baseRun = new Vector2[RootCount][];
		midRun = new Vector2[RootCount][];
		tipRun = new Vector2[RootCount][];
		for (int i = 0; i < RootCount; i++)
		{
			baseRun[i] = new Vector2[BaseEnd + 1];
			midRun[i] = new Vector2[MidEnd - BaseEnd + 1];
			tipRun[i] = new Vector2[Segments - MidEnd + 1];
		}
		path = new Vector2[Segments + 1];
		thornLines = new Vector2[ThornsPerRoot * 4];
	}

	private static float Smooth(float x)
	{
		x = Mathf.Clamp(x, 0f, 1f);
		return x * x * (3f - 2f * x);
	}

	public override void _Draw()
	{
		if (baseRun == null)
			return;

		float r = Radius;
		float left = Lifetime - age;
		float fade = Mathf.Clamp(left / 0.4f, 0f, 1f);
		float retract = Smooth(left / RetractSeconds);
		float dry = 1f - Mathf.Clamp(left / DrySeconds, 0f, 1f);

		// Torn soil where the seed went in, wider as the roots thicken.
		float mound = 5f + 4f * Mathf.Clamp(age / GrowSeconds, 0f, 1f);
		DrawCircle(Vector2.Zero, mound + 3f, new Color(Soil, Soil.A * fade));

		for (int i = 0; i < RootCount; i++)
			DrawRoot(i, r, retract, dry, fade);

		DrawCircle(Vector2.Zero, mound * 0.55f, new Color(Occ, Occ.A * fade));
	}

	private void DrawRoot(int i, float radius, float retract, float dry, float fade)
	{
		// Erupt: each root shoots out to the current radius over a quarter second after its delay,
		// overshooting its writhe while it does - that is the whip of something breaking ground.
		float since = age - rootDelay[i];
		if (since <= 0f)
			return;
		float erupt = Smooth(since / 0.26f);
		float length = radius * rootLength[i] * erupt * retract;
		if (length < 2f)
			return;

		// The writhe is a wave travelling down the root from seed to tip; the whip is a short
		// periodic surge of that wave, at a different interval on every root.
		float whipClock = (age + rootPhase[i]) % rootWhipEvery[i];
		float whip = whipClock < 0.3f ? Mathf.Sin(whipClock / 0.3f * Mathf.Pi) : 0f;
		float burst = 1f - erupt;
		// Radians, so a long root sways further sideways than a short one, as a real one would.
		float amplitude = (0.16f + 0.22f * whip + 0.35f * burst) * (1f - 0.6f * dry);
		float speed = 3.1f + 1.7f * whip;

		for (int j = 0; j <= Segments; j++)
		{
			float s = (float)j / Segments;
			float wave = Mathf.Sin(age * speed + rootPhase[i] - s * 4.2f);
			// The tip hooks over, and the hook breathes.
			float curl = rootCurl[i] * Smooth((s - 0.62f) / 0.38f) * (0.8f + 0.35f * Mathf.Sin(age * 2.3f + rootPhase[i]));
			float theta = rootAngle[i] + amplitude * s * wave + curl;
			float d = s * length;
			path[j] = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * d;
		}

		for (int j = 0; j <= BaseEnd; j++)
			baseRun[i][j] = path[j];
		for (int j = BaseEnd; j <= MidEnd; j++)
			midRun[i][j - BaseEnd] = path[j];
		for (int j = MidEnd; j <= Segments; j++)
			tipRun[i][j - MidEnd] = path[j];

		// Bark at the base, greening toward the living tip: a root, not a vine.
		Color body = Bark.Lerp(living, 0.2f).Lerp(Dry, dry);
		Color midColour = Bark.Lerp(living, 0.5f).Lerp(Dry, dry);
		Color tipColour = living.Lerp(Dry, dry);
		midColour.A = fade;
		body.A = fade;
		tipColour.A = fade;
		Color outline = new Color(Occ, Occ.A * fade);
		float w = rootWidth[i] * (0.6f + 0.4f * erupt);

		float wMid = w * 0.62f;
		float wTip = w * 0.32f;
		DrawPolyline(baseRun[i], outline, w + 2f);
		DrawPolyline(midRun[i], outline, wMid + 2f);
		DrawPolyline(tipRun[i], outline, wTip + 2f);
		// Polyline segments are separate quads, so a thick one shows a notch at every bend. A disc on
		// each of the base's joints fills it; the thinner runs do not need it.
		for (int j = 1; j <= BaseEnd; j++)
			DrawCircle(path[j], w * 0.5f + 1f, outline);
		for (int j = 1; j <= BaseEnd; j++)
			DrawCircle(path[j], w * 0.5f, body);
		DrawPolyline(baseRun[i], body, w);
		DrawPolyline(midRun[i], midColour, wMid);
		DrawPolyline(tipRun[i], tipColour, wTip);

		// Thorns: hooked barbs along the middle of the root, alternating sides and raked back toward
		// the seed. Held inside s = 0.85 and pointed sideways, so a barb never reaches past Radius.
		int t = 0;
		for (int k = 0; k < ThornsPerRoot; k++)
		{
			int j = 3 + k * 2;
			Vector2 at = path[j];
			Vector2 along = (path[j + 1] - path[j - 1]).Normalized();
			Vector2 side = new Vector2(-along.Y, along.X) * ((k % 2 == 0) ? 1f : -1f);
			float barb = 3.5f + w * 0.5f;
			thornLines[t++] = at;
			thornLines[t++] = at + side * barb - along * (barb * 0.6f);
			thornLines[t++] = at + along * 2f;
			thornLines[t++] = at + side * barb - along * (barb * 0.6f);
		}
		DrawMultiline(thornLines, new Color(thornTip.Lerp(Dry, dry), fade), 1.5f);

		// A living tip, and a clod of earth thrown up where the root first broke ground.
		if (dry < 0.6f)
			DrawCircle(path[Segments], 1.8f + 1.2f * whip, new Color(tipGlow, tipGlow.A * fade * (1f - dry)));
		if (burst > 0.05f)
		{
			// Capped at the root's own length, so even the debris stays on the ground that hurts.
			float flung = Mathf.Min(length, length * 0.5f + 14f * (1f - burst));
			Vector2 thrown = path[Segments].Normalized() * flung;
			DrawCircle(thrown, 2.2f * burst + 0.6f, new Color(Clod, Clod.A * burst * fade));
		}
	}
}
