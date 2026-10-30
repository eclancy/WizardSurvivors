using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// What Shadow Bolt's milestones leave behind it (.ai/spell-roster.md section 2e): Black Sigil's marks
/// at the feet of what it cursed, Creeping Rot's paths of rot along every arc, and Anathema's pall of
/// night where the curse ends. One node per arena; the bolt (ChainLightning, which only Shadow Bolt's
/// cards ever switch these on for) hands it each one.
/// </summary>
/// <remarks>
/// Every sigil, path and pall is a slot in a fixed array, ticked on one half-second clock and drawn
/// out of the reach it tests, so nothing is allocated per hop or per frame. Each carries the curse the
/// bolt carried (<see cref="Curse"/>) and lays it on what it reaches the way the bolt does - poison,
/// slow and Vulnerable through Enemy.Apply*, stacking-resistant as they always are.
///
/// ON THE GROUND (.ai/godot-engine.md, "Draw layers"): the paths and the palls lie at
/// <see cref="DrawLayers.Ground"/>; a sigil is a mark round a body's feet, so it is drawn by a child at
/// <see cref="DrawLayers.GroundTell"/>, under every body and over the ground's own pools.
///
/// PIXEL ART ON THE WORLD'S GRID (the pixel-art pass, 2026-10-29; .ai/art-replacement-manifest.md
/// section 9). Seven kinds of antialiased, translucent draw call until then. Each path and pall is now
/// one quad from a fixed pool, painted by scenes/shaders/shadow_curse.gdshader out of the reach it
/// curses (a pall a circle, as its curse is, where it was drawn squashed); a sigil and a breaking sigil's
/// front are stamped cell by cell (PixelInk), fading by dissolving.
/// </remarks>
public partial class ShadowCurse : Node2D
{
	/// <summary>The curse one hit of the bolt lays, as the bolt computed it for its level.</summary>
	public readonly record struct Curse(int PoisonTick, float PoisonSeconds, float SlowMultiplier, float SlowSeconds,
		float Vulnerable, float VulnerableSeconds, int HitDamage);

	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; set; }

	private const int MaxSigils = 48;
	private const int MaxPaths = 32;
	private const int MaxPalls = 10;
	private const float TickSeconds = 0.5f;
	// A sigil cannot be broken by the cast that laid it: a branch landing on it a hop later is the same
	// curse arriving, not a second spell striking.
	private const float SigilArmSeconds = 0.3f;
	private const float BurstSeconds = 0.35f;
	// Half a body either side of a path still treads in it.
	private const float PathHalfWidth = 16f;

	private struct Sigil
	{
		public Enemy Target;
		public ulong Id;
		public float Age;
		public float Life;
		public float Radius;
		public int BurstDamage;
		public Curse Curse;
		public bool Live;
	}

	private struct Path
	{
		public Vector2 A;
		public Vector2 B;
		public float Age;
		public float Life;
		public uint Seed;
		public Curse Curse;
		public bool Live;
	}

	private struct Pall
	{
		public Vector2 At;
		public float Radius;
		public float Age;
		public float Life;
		public uint Seed;
		public Curse Curse;
		public bool Live;
	}

	// A sigil that broke: a ring of shadow thrown out to the reach it exposed.
	private struct Break
	{
		public Vector2 At;
		public float Radius;
		public float Age;
		public bool Live;
	}

	private readonly Sigil[] sigils = new Sigil[MaxSigils];
	private readonly Path[] paths = new Path[MaxPaths];
	private readonly Pall[] palls = new Pall[MaxPalls];
	private readonly Break[] breaks = new Break[MaxSigils];
	private int nextSigil;
	private int nextPath;
	private int nextPall;
	private int nextBreak;
	private int liveSigils;
	private uint seedClock;
	private float tick = TickSeconds;
	private float clock;
	private bool breaking;
	private Node2D marks;
	private readonly CurseQuad[] pathQuads = new CurseQuad[MaxPaths];
	private readonly CurseQuad[] pallQuads = new CurseQuad[MaxPalls];

	private static Shader curseShader;
	private static readonly StringName ParamMode = "mode";
	private static readonly StringName ParamLength = "path_length";
	private static readonly StringName ParamReach = "reach";
	private static readonly StringName ParamStrength = "strength";
	private static readonly StringName ParamTime = "time";
	private static readonly StringName ParamSeed = "seed";

	private static readonly ElementColors.Ramp Dark = ElementColors.GetRamp(Element.Darkness);
	private static readonly ElementColors.Ramp Rot = ElementColors.GetRamp(Element.Poison);
	// Robe violet's deep (.ai/art-direction.md section 3): the dark a sigil sits in.
	private static readonly Color Night = new(0.078f, 0.063f, 0.122f);

	private static ShadowCurse current;

	/// <summary>The arena's curse layer for this spell, made the first time a card lays anything.</summary>
	public static ShadowCurse For(Node arena, SpellData spell, Player player)
	{
		if (current != null && IsInstanceValid(current) && current.GetParent() == arena && current.SpellData == spell)
			return current;
		if (arena == null || !IsInstanceValid(arena))
			return null;
		current = new ShadowCurse { Name = "ShadowCurse", SpellData = spell, PlayerRef = player };
		arena.AddChild(current);
		return current;
	}

	public override void _Ready()
	{
		ZIndex = DrawLayers.Ground;
		ZAsRelative = false;
		// At the world's origin, unturned and unscaled, so its units are the world's and so is its grid.
		TopLevel = true;
		GlobalPosition = Vector2.Zero;
		curseShader ??= GD.Load<Shader>("res://scenes/shaders/shadow_curse.gdshader");
		for (int i = 0; i < MaxPaths; i++)
			pathQuads[i] = MakeQuad($"Path{i}", 0);
		for (int i = 0; i < MaxPalls; i++)
			pallQuads[i] = MakeQuad($"Pall{i}", 1);
		marks = new Node2D { Name = "Sigils", ZIndex = DrawLayers.GroundTell, ZAsRelative = false };
		AddChild(marks);
		marks.Draw += DrawSigils;
		Player.EnemyHit += OnEnemyHit;
		SetProcess(false);
	}

	public override void _ExitTree()
	{
		Player.EnemyHit -= OnEnemyHit;
		if (marks != null && IsInstanceValid(marks))
			marks.Draw -= DrawSigils;
		if (current == this)
			current = null;
	}

	private CurseQuad MakeQuad(string name, int mode)
	{
		var material = new ShaderMaterial { Shader = curseShader };
		material.SetShaderParameter(ParamMode, mode);
		var quad = new CurseQuad { Name = name, Material = material, Visible = false, Pall = mode == 1 };
		AddChild(quad);
		return quad;
	}

	/// <summary>Black Sigil: marks <paramref name="target"/> until the next hit it takes breaks the mark.</summary>
	public void MarkSigil(Enemy target, float seconds, float radius, int burstDamage, Curse curse)
	{
		if (target == null || !IsInstanceValid(target) || target.IsDying)
			return;
		ulong id = target.GetInstanceId();
		int slot = -1;
		for (int i = 0; i < MaxSigils; i++)
		{
			if (sigils[i].Live && sigils[i].Id == id)
			{
				slot = i;
				break;
			}
		}
		if (slot < 0)
		{
			slot = nextSigil;
			nextSigil = (nextSigil + 1) % MaxSigils;
		}
		// A fresh mark on a marked enemy keeps its arming: the cast that laid it cannot break it either.
		float age = sigils[slot].Live && sigils[slot].Id == id ? MathF.Min(sigils[slot].Age, SigilArmSeconds) : 0f;
		sigils[slot] = new Sigil { Target = target, Id = id, Age = age, Life = seconds, Radius = radius, BurstDamage = burstDamage, Curse = curse, Live = true };
		Wake();
	}

	/// <summary>Creeping Rot: a path of rot from <paramref name="a"/> to <paramref name="b"/>.</summary>
	public void LayPath(Vector2 a, Vector2 b, float seconds, Curse curse)
	{
		paths[nextPath] = new Path { A = a, B = b, Age = 0f, Life = seconds, Seed = ++seedClock * 2654435761u, Curse = curse, Live = true };
		Show(pathQuads[nextPath], a, (b - a).Angle(), a.DistanceTo(b), PathHalfWidth, paths[nextPath].Seed);
		nextPath = (nextPath + 1) % MaxPaths;
		Wake();
	}

	/// <summary>Anathema: a pall of night over <paramref name="at"/>, cursing everything under it every half second.</summary>
	public void DropPall(Vector2 at, float radius, float seconds, Curse curse)
	{
		palls[nextPall] = new Pall { At = at, Radius = radius, Age = 0f, Life = seconds, Seed = ++seedClock * 2654435761u, Curse = curse, Live = true };
		Show(pallQuads[nextPall], at, 0f, 0f, radius, palls[nextPall].Seed);
		nextPall = (nextPall + 1) % MaxPalls;
		Wake();
	}

	private void Wake() => SetProcess(true);

	// A pooled quad laid on its path or pall: the shader measures every cell in its frame.
	private static void Show(CurseQuad quad, Vector2 at, float angle, float length, float reach, uint seed)
	{
		if (quad == null)
			return;
		quad.GlobalPosition = at;
		quad.GlobalRotation = angle;
		quad.Length = length;
		quad.Reach = reach;
		var material = (ShaderMaterial)quad.Material;
		material.SetShaderParameter(ParamLength, length);
		material.SetShaderParameter(ParamReach, reach);
		material.SetShaderParameter(ParamSeed, (seed % 997u) * 0.731f);
		material.SetShaderParameter(ParamStrength, 0f);
		quad.Visible = true;
		quad.QueueRedraw();
	}

	private void Tend(CurseQuad quad, bool live, float strength)
	{
		if (quad == null)
			return;
		if (!live)
		{
			quad.Visible = false;
			return;
		}
		var material = (ShaderMaterial)quad.Material;
		material.SetShaderParameter(ParamStrength, strength);
		material.SetShaderParameter(ParamTime, clock);
	}

	// A hit from any OTHER spell breaks the sigil on what it struck. Shadow Bolt's own hits never do -
	// its next cast, its paths and palls, the bursts themselves - so one break never sets off the next
	// and the sigils are broken by the loadout, which is the spell's job: making the other five hit
	// harder. (Breaking on its own next cast made a lone Shadow Bolt a damage spell: x3.8 of Withering
	// Mark at level 7, 2026-10-29, --enemy-hp=10.)
	private void OnEnemyHit(Enemy enemy, int amount, SpellData source)
	{
		if (breaking || liveSigils == 0 || enemy == null || source == SpellData)
			return;
		ulong id = enemy.GetInstanceId();
		for (int i = 0; i < MaxSigils; i++)
		{
			ref Sigil s = ref sigils[i];
			if (!s.Live || s.Id != id || s.Age < SigilArmSeconds)
				continue;
			s.Live = false;
			BreakSigil(enemy.GlobalPosition, s.Radius, s.BurstDamage, s.Curse);
			return;
		}
	}

	private void BreakSigil(Vector2 at, float radius, int damage, Curse curse)
	{
		breaking = true;
		try
		{
			float r2 = radius * radius;
			foreach (Enemy enemy in EnemyRegistry.Snapshot)
			{
				if (!IsInstanceValid(enemy) || enemy.IsDying || enemy.GlobalPosition.DistanceSquaredTo(at) > r2)
					continue;
				if (damage > 0 && PlayerRef != null && IsInstanceValid(PlayerRef))
					PlayerRef.DealDamageToEnemy(enemy, damage, allowElementalChain: false, source: SpellData);
				Lay(enemy, curse);
			}
		}
		finally
		{
			breaking = false;
		}
		breaks[nextBreak] = new Break { At = at, Radius = radius, Age = 0f, Live = true };
		nextBreak = (nextBreak + 1) % MaxSigils;
		if (GetParent() is Node2D arena)
			ImpactDebrisLayer.Spawn(arena, at, radius, Dark, Colors.White);
		Wake();
	}

	private static void Lay(Enemy enemy, in Curse curse)
	{
		if (!IsInstanceValid(enemy) || enemy.IsDying)
			return;
		if (curse.PoisonTick > 0)
			enemy.ApplyPoison(curse.PoisonTick, curse.PoisonSeconds);
		if (curse.SlowMultiplier < 1f)
			enemy.ApplySlow(curse.SlowMultiplier, curse.SlowSeconds);
		if (curse.Vulnerable > 0f)
			enemy.ApplyVulnerable(curse.Vulnerable, curse.VulnerableSeconds);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		int live = 0;
		liveSigils = 0;
		for (int i = 0; i < MaxSigils; i++)
		{
			ref Sigil s = ref sigils[i];
			if (!s.Live) continue;
			s.Age += dt;
			if (s.Age >= s.Life || s.Target == null || !IsInstanceValid(s.Target) || s.Target.IsDying)
			{
				s.Live = false;
				s.Target = null;
				continue;
			}
			liveSigils++;
			live++;
		}
		for (int i = 0; i < MaxPaths; i++)
		{
			if (!paths[i].Live) continue;
			paths[i].Age += dt;
			if (paths[i].Age >= paths[i].Life) paths[i].Live = false;
			else live++;
			Tend(pathQuads[i], paths[i].Live, Fade(paths[i].Age, paths[i].Life, 0.15f, 0.6f));
		}
		for (int i = 0; i < MaxPalls; i++)
		{
			if (!palls[i].Live) continue;
			palls[i].Age += dt;
			if (palls[i].Age >= palls[i].Life) palls[i].Live = false;
			else live++;
			Tend(pallQuads[i], palls[i].Live, Fade(palls[i].Age, palls[i].Life, 0.25f, 0.7f));
		}
		for (int i = 0; i < MaxSigils; i++)
		{
			if (!breaks[i].Live) continue;
			breaks[i].Age += dt;
			if (breaks[i].Age >= BurstSeconds) breaks[i].Live = false;
			else live++;
		}

		QueueRedraw();
		marks.QueueRedraw();
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
		CurseWhatStands();
	}

	// One pass over the swarm a tick: a body on a path is cursed by it, a body under a pall is cursed
	// and struck by it - once a tick each, however many overlap.
	private void CurseWhatStands()
	{
		bool anyPath = false, anyPall = false;
		for (int i = 0; i < MaxPaths && !anyPath; i++) anyPath = paths[i].Live;
		for (int i = 0; i < MaxPalls && !anyPall; i++) anyPall = palls[i].Live;
		if (!anyPath && !anyPall)
			return;
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (!IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			Vector2 p = enemy.GlobalPosition;
			if (anyPath)
			{
				for (int i = 0; i < MaxPaths; i++)
				{
					ref Path path = ref paths[i];
					if (path.Live && DistanceSquaredToSegment(p, path.A, path.B) <= PathHalfWidth * PathHalfWidth)
					{
						Lay(enemy, path.Curse);
						break;
					}
				}
			}
			if (anyPall)
			{
				for (int i = 0; i < MaxPalls; i++)
				{
					ref Pall pall = ref palls[i];
					if (!pall.Live || p.DistanceSquaredTo(pall.At) > pall.Radius * pall.Radius)
						continue;
					if (pall.Curse.HitDamage > 0 && PlayerRef != null && IsInstanceValid(PlayerRef))
						PlayerRef.DealDamageToEnemy(enemy, pall.Curse.HitDamage, allowElementalChain: false, source: SpellData);
					Lay(enemy, pall.Curse);
					break;
				}
			}
		}
	}

	private static float DistanceSquaredToSegment(Vector2 p, Vector2 a, Vector2 b)
	{
		Vector2 ab = b - a;
		float len2 = ab.LengthSquared();
		float t = len2 > 0.0001f ? Mathf.Clamp((p - a).Dot(ab) / len2, 0f, 1f) : 0f;
		return p.DistanceSquaredTo(a + ab * t);
	}

	// The paths and palls are their quads' (shadow_curse.gdshader). Drawn here: a broken sigil's front -
	// the shadow it lets out, thrown to the reach it curses as a ring of the Darkness's light that
	// thins and dissolves as it goes, a one-shot animation over the debris ImpactDebris throws.
	public override void _Draw()
	{
		for (int i = 0; i < MaxSigils; i++)
		{
			Break b = breaks[i];
			if (!b.Live) continue;
			float t = b.Age / BurstSeconds;
			float r = b.Radius * (0.3f + 0.7f * t);
			PixelInk.Ring(this, b.At, r, r, Dark.Edge, 2, (1f - t) * 1.4f);
			PixelInk.Ring(this, b.At, r, r, t < 0.5f ? Dark.Hot : Dark.Mid, 1, (1f - t) * 1.6f);
		}
	}

	// A sigil at a cursed body's feet: a ring of shadow with four hooked marks turning in it, pulsing
	// brighter once it is armed - the moment the next hit will break it. A mark on one body, not an
	// area: drawn flat to the ground, half as tall as it is wide, and stamped on the grid.
	private void DrawSigils()
	{
		for (int i = 0; i < MaxSigils; i++)
		{
			Sigil s = sigils[i];
			if (!s.Live || s.Target == null || !IsInstanceValid(s.Target))
				continue;
			float a = Fade(s.Age, s.Life, 0.12f, 0.4f);
			bool armed = s.Age >= SigilArmSeconds;
			// Armed, it pulses between the Darkness's hot and mid; before that it is dim.
			bool bright = armed && MathF.Sin(clock * 7f + i) > -0.2f;
			Color ring = armed ? Dark.Mid : Dark.Edge;
			Color hook = bright ? Dark.Hot : Dark.Mid;
			Vector2 at = s.Target.GlobalPosition + new Vector2(0f, 12f);
			// Wider than the body's own shadow and drawn heavy, so it reads round a pack's feet: the first
			// build's thin ring was lost under the bodies in a crowd photograph (2026-10-29).
			const float r = 19f;
			PixelInk.Ring(marks, at, r + 2f, (r + 2f) * 0.5f, Night, 2, a * 1.2f);
			PixelInk.Ring(marks, at, r, r * 0.5f, ring, 1, a * 1.4f);
			float spin = clock * 1.6f + i;
			for (int k = 0; k < 4; k++)
			{
				Vector2 dir = Vector2.FromAngle(spin + k * Mathf.Pi * 0.5f);
				Vector2 squash = new(1f, 0.5f);
				Vector2 tip = dir * r * 0.78f;
				Vector2 hookEnd = tip + dir.Orthogonal() * 5f;
				PixelInk.Line(marks, at + dir * r * 0.22f * squash, at + tip * squash, hook, 1, a * 1.4f);
				PixelInk.Line(marks, at + tip * squash, at + hookEnd * squash, hook, 1, a * 1.4f);
			}
		}
	}

	/// <summary>One path's or pall's quad: the shader decides every pixel and discards the rest.</summary>
	private sealed partial class CurseQuad : Node2D
	{
		public bool Pall { get; set; }
		public float Length { get; set; }
		public float Reach { get; set; }

		public override void _Draw()
		{
			float r = Reach + 2f;
			if (Pall)
				DrawRect(new Rect2(-r, -r, r * 2f, r * 2f), Colors.White);
			else
				DrawRect(new Rect2(-r, -r, Length + r * 2f, r * 2f), Colors.White);
		}
	}

	private static float Fade(float age, float life, float inSeconds, float outSeconds) =>
		Mathf.Clamp(age / inSeconds, 0f, 1f) * Mathf.Clamp((life - age) / outSeconds, 0f, 1f);
}
