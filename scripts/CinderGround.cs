using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// What Cinderbreath leaves on the ground (.ai/spell-roster.md section 2e): Ember Wake's embers,
/// dropped where the tongues went out across the swept cone; Dragon's Breath's wall of fire, standing
/// across the jet's path where its reach ended; and Backdraft's burst at the tip as a burn goes out.
/// </summary>
/// <remarks>
/// One node per emitter, parented to the arena so the fire stays where it fell while the wizard walks
/// on. Every ember, wall and burst is a slot in a fixed array, ticked on one half-second clock and
/// drawn out of the radius or the band it tests, so nothing is allocated per tongue or per frame and
/// the fire drawn is the fire that hurts.
///
/// ON THE GROUND (.ai/godot-engine.md, "Draw layers"): the char under the embers and the wall is at
/// absolute <see cref="DrawLayers.Ground"/>, and the flames standing in it at
/// <see cref="CraterGround.FireZ"/>, one above - under every body, as Cataclysm's craters are. A burst
/// is thrown up into the air by ImpactDebrisLayer, over them. An element path (Hellbreath) repaints
/// the flames in its ramp: the fire node carries the path's ElementRampMaterial, the same one the
/// tongues wear, and the char below never needs it.
/// </remarks>
public partial class CinderGround : Node2D
{
	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; set; }

	private const int MaxEmbers = 48;
	private const int MaxWalls = 6;
	private const int MaxBursts = 8;
	private const float TickSeconds = 0.5f;
	// How much of a body beside a wall still stands in its fire: half a skeleton's width, as the beam's
	// pierce allowance is measured (ScorchingRayBeam.PierceHalfWidth).
	private const float BodyAllowance = 10f;
	// The flames along a wall, centre to centre, and how long a wall takes to catch from its middle out.
	private const float WallFlameSpacing = 16f;
	private const float WallCatchSeconds = 0.22f;
	private const float WallGoingOutSeconds = 0.6f;
	private const float EmberFadeSeconds = 0.45f;

	private struct Ember
	{
		public Vector2 At;
		public float Radius;
		public float Age;
		public float Life;
		public int Damage;
		public uint Seed;
		public bool Live;
	}

	private struct Wall
	{
		public Vector2 A;
		public Vector2 B;
		public float HalfThickness;
		public float Age;
		public float Life;
		public int Damage;
		public uint Seed;
		public bool Live;
	}

	// A backdraft's fire, after its blow has landed: a blaze in the bowl of the burst, burning down
	// inside a second (CraterFire's plain impact), with a scorch under it.
	private struct Burst
	{
		public Vector2 At;
		public float Radius;
		public float Age;
		public uint Seed;
		public bool Live;
	}

	private readonly Ember[] embers = new Ember[MaxEmbers];
	private readonly Wall[] walls = new Wall[MaxWalls];
	private readonly Burst[] bursts = new Burst[MaxBursts];
	private int nextEmber;
	private int nextWall;
	private int nextBurst;
	private int live;
	private uint seedClock;
	private float tick = TickSeconds;
	private Node2D ground;
	private static readonly ElementColors.Ramp FireRamp = ElementColors.GetRamp(Element.Fire);
	private static readonly Color Char = new(0.07f, 0.05f, 0.05f);
	private const float BurstSeconds = 1.6f;

	/// <summary>The element path's repaint for the flames (Hellbreath), or null for plain fire.</summary>
	public ShaderMaterial PathMaterial
	{
		set => Material = value;
	}

	public override void _Ready()
	{
		Name = "CinderGround";
		ZIndex = CraterGround.FireZ;
		ZAsRelative = false;
		ground = new Node2D { Name = "Char", ZIndex = DrawLayers.Ground, ZAsRelative = false };
		AddChild(ground);
		ground.Draw += DrawChar;
		SetProcess(false);
	}

	public override void _ExitTree()
	{
		if (ground != null && IsInstanceValid(ground))
			ground.Draw -= DrawChar;
	}

	/// <summary>An ember of Ember Wake: burns what stands in <paramref name="radius"/> every half second.</summary>
	public void DropEmber(Vector2 at, float radius, float seconds, int damage)
	{
		embers[nextEmber] = new Ember { At = at, Radius = radius, Age = 0f, Life = seconds, Damage = damage, Seed = NextSeed(), Live = true };
		nextEmber = (nextEmber + 1) % MaxEmbers;
		Wake();
	}

	/// <summary>
	/// Dragon's Breath's wall: from <paramref name="a"/> to <paramref name="b"/>, burning what stands
	/// within <paramref name="halfThickness"/> of it every half second for <paramref name="seconds"/>.
	/// </summary>
	public void RaiseWall(Vector2 a, Vector2 b, float halfThickness, float seconds, int damage)
	{
		walls[nextWall] = new Wall { A = a, B = b, HalfThickness = halfThickness, Age = 0f, Life = seconds, Damage = damage, Seed = NextSeed(), Live = true };
		nextWall = (nextWall + 1) % MaxWalls;
		Wake();
	}

	/// <summary>
	/// Backdraft: everything within <paramref name="radius"/> of <paramref name="at"/> takes
	/// <paramref name="damage"/> now, the burst is thrown out to exactly that radius, and a short blaze
	/// burns down where it rolled up.
	/// </summary>
	public void Backdraft(Vector2 at, float radius, int damage)
	{
		if (PlayerRef != null && IsInstanceValid(PlayerRef) && damage > 0)
		{
			float r2 = radius * radius;
			foreach (Enemy enemy in EnemyRegistry.Snapshot)
			{
				if (IsInstanceValid(enemy) && !enemy.IsDying && enemy.GlobalPosition.DistanceSquaredTo(at) <= r2)
					PlayerRef.DealDamageToEnemy(enemy, damage, source: SpellData);
			}
		}
		if (GetParent() is Node2D arena)
			ImpactDebrisLayer.Spawn(arena, at, radius, FireRamp, Colors.White);
		bursts[nextBurst] = new Burst { At = at, Radius = radius, Age = 0f, Seed = NextSeed(), Live = true };
		nextBurst = (nextBurst + 1) % MaxBursts;
		Wake();
	}

	private uint NextSeed() => ++seedClock * 2654435761u;

	private void Wake()
	{
		live = 1;
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		live = 0;
		for (int i = 0; i < MaxEmbers; i++)
		{
			if (!embers[i].Live) continue;
			embers[i].Age += dt;
			if (embers[i].Age >= embers[i].Life) embers[i].Live = false;
			else live++;
		}
		for (int i = 0; i < MaxWalls; i++)
		{
			if (!walls[i].Live) continue;
			walls[i].Age += dt;
			if (walls[i].Age >= walls[i].Life) walls[i].Live = false;
			else live++;
		}
		for (int i = 0; i < MaxBursts; i++)
		{
			if (!bursts[i].Live) continue;
			bursts[i].Age += dt;
			if (bursts[i].Age >= BurstSeconds) bursts[i].Live = false;
			else live++;
		}

		QueueRedraw();
		ground.QueueRedraw();
		if (live == 0)
		{
			tick = TickSeconds;
			SetProcess(false);
			return;
		}

		tick -= dt;
		if (tick > 0f)
			return;
		tick = TickSeconds;
		Burn();
	}

	// One pass over the swarm a tick: a body in several embers is burned once by the embers, and once
	// by the walls - the fire is one fire, not a stack of them.
	private void Burn()
	{
		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (!IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			Vector2 p = enemy.GlobalPosition;

			int emberDamage = 0;
			for (int i = 0; i < MaxEmbers; i++)
			{
				ref Ember e = ref embers[i];
				if (e.Live && e.Damage > emberDamage && p.DistanceSquaredTo(e.At) <= e.Radius * e.Radius)
					emberDamage = e.Damage;
			}
			if (emberDamage > 0)
				PlayerRef.DealDamageToEnemy(enemy, emberDamage, source: SpellData);

			if (!IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			int wallDamage = 0;
			for (int i = 0; i < MaxWalls; i++)
			{
				ref Wall w = ref walls[i];
				if (!w.Live || w.Damage <= wallDamage || w.Age < WallCatchSeconds * 0.5f)
					continue;
				float reach = w.HalfThickness + BodyAllowance;
				if (DistanceSquaredToSegment(p, w.A, w.B) <= reach * reach)
					wallDamage = w.Damage;
			}
			if (wallDamage > 0)
				PlayerRef.DealDamageToEnemy(enemy, wallDamage, source: SpellData);
		}
	}

	private static float DistanceSquaredToSegment(Vector2 p, Vector2 a, Vector2 b)
	{
		Vector2 ab = b - a;
		float len2 = ab.LengthSquared();
		float t = len2 > 0.0001f ? Mathf.Clamp((p - a).Dot(ab) / len2, 0f, 1f) : 0f;
		return p.DistanceSquaredTo(a + ab * t);
	}

	// The char under it all, on the ground child: each ember a scorched spot as wide as it burns, each
	// wall a scorched band as thick as it burns, fading as they go out.
	private void DrawChar()
	{
		for (int i = 0; i < MaxEmbers; i++)
		{
			Ember e = embers[i];
			if (!e.Live) continue;
			float fade = Mathf.Clamp((e.Life - e.Age) / EmberFadeSeconds, 0f, 1f);
			Vector2 at = ground.ToLocal(e.At);
			ground.DrawSetTransform(at, 0f, new Vector2(1f, CraterGround.Squash));
			ground.DrawCircle(Vector2.Zero, e.Radius * 0.95f, new Color(Char, 0.42f * fade));
			ground.DrawCircle(Vector2.Zero, e.Radius * 0.55f, new Color(Char, 0.35f * fade));
			// The bed of coals: a dull glow that cools as the ember dies.
			float glow = 0.55f * fade * (0.85f + 0.15f * MathF.Sin(e.Age * 9f + e.Seed * 0.001f));
			ground.DrawCircle(Vector2.Zero, e.Radius * 0.42f, new Color(FireRamp.Edge, glow));
			ground.DrawCircle(Vector2.Zero, e.Radius * 0.2f, new Color(FireRamp.Mid, glow * 0.8f));
		}
		ground.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
		for (int i = 0; i < MaxWalls; i++)
		{
			Wall w = walls[i];
			if (!w.Live) continue;
			float fade = Mathf.Clamp((w.Life - w.Age) / WallGoingOutSeconds, 0f, 1f);
			Vector2 a = ground.ToLocal(w.A), b = ground.ToLocal(w.B);
			// A burned band as thick as the fire tests, darkest along its spine, with the coals glowing in it.
			ground.DrawLine(a, b, new Color(Char, 0.26f * fade), w.HalfThickness * 2f);
			ground.DrawLine(a, b, new Color(Char, 0.3f * fade), w.HalfThickness * 1.1f);
			ground.DrawLine(a, b, new Color(FireRamp.Edge, 0.5f * fade), w.HalfThickness * 0.5f);
		}
		for (int i = 0; i < MaxBursts; i++)
		{
			Burst s = bursts[i];
			if (!s.Live) continue;
			float fade = 1f - Mathf.Clamp((s.Age - 0.6f) / (BurstSeconds - 0.6f), 0f, 1f);
			Vector2 at = ground.ToLocal(s.At);
			ground.DrawSetTransform(at, 0f, new Vector2(1f, CraterGround.Squash));
			ground.DrawCircle(Vector2.Zero, s.Radius * 0.5f, new Color(Char, 0.3f * fade));
			ground.DrawCircle(Vector2.Zero, s.Radius * 0.28f, new Color(Char, 0.3f * fade));
		}
		ground.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	// The flames, on this node: every ember's fire filling its radius and every burst's blaze
	// (CraterFire, the craters' own fire), then every wall's row of blazes; then every ember's and
	// burst's sparks, so one texture's draws stay together and batch.
	public override void _Draw()
	{
		for (int i = 0; i < MaxEmbers; i++)
		{
			Ember e = embers[i];
			if (e.Live)
				CraterFire.DrawFire(this, ToLocal(e.At), e.Radius + 12f, e.Age, e.Life, e.Seed, Colors.White);
		}
		for (int i = 0; i < MaxBursts; i++)
		{
			Burst s = bursts[i];
			if (s.Live)
				CraterFire.DrawFire(this, ToLocal(s.At), s.Radius, s.Age, 0f, s.Seed, Colors.White);
		}
		for (int i = 0; i < MaxWalls; i++)
		{
			if (walls[i].Live)
				DrawWall(walls[i]);
		}
		for (int i = 0; i < MaxBursts; i++)
		{
			Burst s = bursts[i];
			if (s.Live)
				CraterFire.DrawEmbers(this, ToLocal(s.At), s.Radius, s.Age, 0f, s.Seed, Colors.White);
		}
	}

	private static Texture2D sheet;

	// A wall is a row of blazes along its band, each on its own beat, catching from the middle out and
	// going out flame by flame, burning low at the ends. Walked from its northern end, so the nearer
	// flames are drawn over the further ones with nothing sorted.
	private void DrawWall(Wall w)
	{
		if (sheet == null || !IsInstanceValid(sheet))
			sheet = GD.Load<Texture2D>(CraterFire.TexturePath);
		if (sheet == null)
			return;
		Vector2 a = ToLocal(w.A), b = ToLocal(w.B);
		if (a.Y > b.Y)
			(a, b) = (b, a);
		float length = a.DistanceTo(b);
		int count = Math.Max(2, (int)(length / WallFlameSpacing) + 1);
		const float drawW = CraterFire.CellW * 2f, drawH = CraterFire.CellH * 2f;
		for (int k = 0; k < count; k++)
		{
			float t = count == 1 ? 0.5f : k / (float)(count - 1);
			float fromMiddle = MathF.Abs(t - 0.5f) * 2f;
			float h1 = Hash(w.Seed, k), h2 = Hash(w.Seed, k + 7919);
			float ignite = WallCatchSeconds * fromMiddle + h1 * 0.05f;
			float goesOut = w.Life - WallGoingOutSeconds * h2;
			if (w.Age < ignite || w.Age >= goesOut)
				continue;
			bool low = w.Age - ignite < 0.08f || goesOut - w.Age < 0.35f || fromMiddle > 0.86f;
			int row = low ? CraterFire.RowLick : CraterFire.RowBlaze;
			int frame = ((int)(w.Age * 12f + h1 * CraterFire.Frames)) % CraterFire.Frames;
			// Jittered a little across the band, so the wall is a hedge of fire and not a picket fence.
			Vector2 across = (b - a).Orthogonal().Normalized() * (h2 - 0.5f) * w.HalfThickness * 0.8f;
			Vector2 foot = a.Lerp(b, t) + across;
			var dest = new Rect2(Snap(foot.X - drawW * 0.5f), Snap(foot.Y - drawH + 2f), drawW, drawH);
			var src = new Rect2(frame * CraterFire.CellW, row * CraterFire.CellH, CraterFire.CellW, CraterFire.CellH);
			DrawTextureRectRegion(sheet, dest, src, Colors.White);
		}
	}

	private static float Snap(float v) => MathF.Round(v / WhipLimb.Grid) * WhipLimb.Grid;

	private static float Hash(uint seed, int i)
	{
		uint h = (uint)i * 374761393u + seed * 668265263u;
		h = (h ^ (h >> 13)) * 1274126177u;
		h ^= h >> 16;
		return (h & 0xFFFFFF) / 16777216f;
	}
}
