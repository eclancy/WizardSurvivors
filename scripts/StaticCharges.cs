using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

/// <summary>
/// Magic Missile's Overcharge (.ai/spell-roster.md section 2h): the sparks of static its darts leave on
/// what they strike, and the burst of lightning when one enemy carries the last of them. One node per
/// arena; the dart (MagicMissile) hands it each spark.
/// </summary>
/// <remarks>
/// A dart's hit was the whole of it; now each one leaves a spark crackling round its enemy, one more for
/// every dart that reaches it, and the <see cref="MagicMissile.OverchargeSparks"/>th overloads it: lightning
/// bursts out of it to exactly the reach it strikes, hitting it and everything round it for
/// <see cref="MagicMissile.OverchargeShare"/> times a dart. A spark that no dart renews goes out after
/// <see cref="MagicMissile.SparkSeconds"/>, so the darts have to keep finding the same enemy - which the
/// spell's nearest-first aim does on its own against an elite or a boss, and a crowd does by numbers.
///
/// Every charge and burst is a slot in a fixed array: nothing is allocated per dart or per frame, and the
/// node stops processing when nothing is live. In the air (DrawLayers.Air), over the bodies it crackles on.
/// The burst's hits pass the spell as their source, but never add a spark of their own: a spark is laid
/// only by a dart, so one overload cannot set off the next.
/// </remarks>
public partial class StaticCharges : Node2D
{
	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; set; }

	private const int MaxCharged = 48;
	private const int MaxBursts = 16;
	private const float BurstSeconds = 0.3f;
	private const int BurstArcs = 7;
	private const float BodyAllowance = 8f;
	// Where the sparks circle a body, and how high above its feet.
	private const float SparkRing = 13f;
	private const float SparkLift = 12f;

	private struct Charge
	{
		public Enemy Target;
		public ulong Id;
		public int Sparks;
		public float Age;
		public bool Live;
	}

	private struct Burst
	{
		public Vector2 At;
		public float Radius;
		public float Age;
		public uint Seed;
		public bool Live;
	}

	private readonly Charge[] charges = new Charge[MaxCharged];
	private readonly Burst[] bursts = new Burst[MaxBursts];
	private int nextCharge;
	private int nextBurst;
	private uint seedClock;
	private float clock;
	private readonly List<Enemy> scratch = new(64);

	private static readonly ElementColors.Ramp Lightning = ElementColors.GetRamp(Element.Lightning);
	private static readonly Color Outline = new(0.04f, 0.04f, 0.08f, 0.75f);

	private static StaticCharges current;

	/// <summary>The arena's static for this spell, made the first time a dart lays a spark.</summary>
	public static StaticCharges For(Node arena, SpellData spell, Player player)
	{
		if (current != null && IsInstanceValid(current) && current.IsInsideTree() && current.GetParent() == arena && current.SpellData == spell)
			return current;
		if (arena == null || !IsInstanceValid(arena) || player == null)
			return null;
		current = new StaticCharges { Name = "StaticCharges", SpellData = spell, PlayerRef = player };
		arena.AddChild(current);
		return current;
	}

	public override void _Ready()
	{
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		SetProcess(false);
	}

	public override void _ExitTree()
	{
		if (current == this)
			current = null;
	}

	/// <summary>One dart's spark on <paramref name="target"/>; the last of them overloads it.</summary>
	public void AddSpark(Enemy target, int burstDamage, float radius)
	{
		if (target == null || !IsInstanceValid(target) || target.IsDying)
			return;
		ulong id = target.GetInstanceId();
		int slot = -1;
		for (int i = 0; i < MaxCharged; i++)
		{
			if (charges[i].Live && charges[i].Id == id)
			{
				slot = i;
				break;
			}
		}
		if (slot < 0)
		{
			slot = nextCharge;
			nextCharge = (nextCharge + 1) % MaxCharged;
			charges[slot] = new Charge { Target = target, Id = id, Sparks = 0, Live = true };
		}
		charges[slot].Sparks++;
		charges[slot].Age = 0f;
		SetProcess(true);
		if (charges[slot].Sparks < MagicMissile.OverchargeSparks)
			return;
		charges[slot].Live = false;
		Overload(target.GlobalPosition, radius, burstDamage);
	}

	private void Overload(Vector2 at, float radius, int burstDamage)
	{
		bursts[nextBurst] = new Burst { At = at, Radius = radius, Age = 0f, Seed = ++seedClock * 2654435761u, Live = true };
		nextBurst = (nextBurst + 1) % MaxBursts;
		if (PlayerRef != null && IsInstanceValid(PlayerRef))
		{
			// The caller's own list, so the walk is safe while the burst kills.
			EnemyRegistry.CollectNear(at, radius + BodyAllowance, scratch);
			foreach (Enemy enemy in scratch)
			{
				if (IsInstanceValid(enemy) && !enemy.IsDying && at.DistanceTo(enemy.GlobalPosition) <= radius + BodyAllowance)
					PlayerRef.DealDamageToEnemy(enemy, burstDamage, source: SpellData);
			}
			scratch.Clear();
		}
		if (GetParent() is Node2D arena)
			ImpactDebrisLayer.Spawn(arena, at, radius, Lightning, Colors.White);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		bool any = false;
		for (int i = 0; i < MaxCharged; i++)
		{
			ref Charge c = ref charges[i];
			if (!c.Live)
				continue;
			c.Age += dt;
			if (c.Age >= MagicMissile.SparkSeconds || !IsInstanceValid(c.Target) || c.Target.IsDying)
			{
				c.Live = false;
				c.Target = null;
				continue;
			}
			any = true;
		}
		for (int i = 0; i < MaxBursts; i++)
		{
			ref Burst b = ref bursts[i];
			if (!b.Live)
				continue;
			b.Age += dt;
			if (b.Age >= BurstSeconds)
			{
				b.Live = false;
				continue;
			}
			any = true;
		}
		QueueRedraw();
		if (!any)
			SetProcess(false);
	}

	public override void _Draw()
	{
		// The sparks: one jagged little bolt per spark, turning round the body at chest height, the
		// newest brightest. A spark about to go out flickers.
		for (int i = 0; i < MaxCharged; i++)
		{
			ref Charge c = ref charges[i];
			if (!c.Live || !IsInstanceValid(c.Target))
				continue;
			Vector2 centre = ToLocal(c.Target.GlobalPosition) + new Vector2(0f, -SparkLift);
			float fading = c.Age > MagicMissile.SparkSeconds - 0.5f ? 0.5f + 0.5f * MathF.Sin(clock * 40f) : 1f;
			for (int k = 0; k < c.Sparks; k++)
			{
				float a = clock * 5f + Mathf.Tau * k / MagicMissile.OverchargeSparks + i;
				Vector2 p = centre + new Vector2(MathF.Cos(a), MathF.Sin(a) * 0.6f) * SparkRing;
				DrawSpark(p, a + MathF.PI * 0.5f, fading);
			}
		}

		// The overload: bolts thrown out of the body to exactly the reach that was struck, white at the
		// root and the element's mid at the tip, and a flash at the heart that goes first.
		for (int i = 0; i < MaxBursts; i++)
		{
			ref Burst b = ref bursts[i];
			if (!b.Live)
				continue;
			float t = b.Age / BurstSeconds;
			float life = 1f - t;
			Vector2 centre = ToLocal(b.At);
			DrawCircle(centre, b.Radius * 0.28f * (1f - t * 0.6f), Lightning.Core with { A = MathF.Max(0f, 1f - t * 2.2f) });
			uint seed = b.Seed;
			for (int k = 0; k < BurstArcs; k++)
			{
				float baseAngle = Mathf.Tau * k / BurstArcs + Unit(ref seed) * 0.6f;
				Vector2 dir = new Vector2(MathF.Cos(baseAngle), MathF.Sin(baseAngle));
				Vector2 side = dir.Orthogonal();
				Vector2 prev = centre;
				const int Steps = 4;
				for (int s = 1; s <= Steps; s++)
				{
					float along = b.Radius * s / Steps;
					float jag = s == Steps ? 0f : (Unit(ref seed) - 0.5f) * b.Radius * 0.28f;
					// Each segment re-rolls a little every few frames, so a standing bolt still crackles.
					jag += MathF.Sin(clock * 50f + k * 3f + s) * 2f;
					Vector2 next = centre + dir * along + side * jag;
					DrawLine(prev, next, Outline with { A = Outline.A * life }, 4f);
					DrawLine(prev, next, (s <= 2 ? Lightning.Core : Lightning.Mid) with { A = life }, 2f);
					prev = next;
				}
			}
		}
	}

	private void DrawSpark(Vector2 p, float angle, float alpha)
	{
		Vector2 d = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 4f;
		Vector2 s = d.Orthogonal() * 0.6f;
		Vector2 a = p - d, m1 = p - d * 0.2f + s, m2 = p + d * 0.2f - s, z = p + d;
		DrawLine(a, m1, Outline with { A = Outline.A * alpha }, 3.5f);
		DrawLine(m1, m2, Outline with { A = Outline.A * alpha }, 3.5f);
		DrawLine(m2, z, Outline with { A = Outline.A * alpha }, 3.5f);
		DrawLine(a, m1, Lightning.Hot with { A = alpha }, 1.6f);
		DrawLine(m1, m2, Lightning.Core with { A = alpha }, 1.6f);
		DrawLine(m2, z, Lightning.Hot with { A = alpha }, 1.6f);
	}

	private static float Unit(ref uint seed)
	{
		seed ^= seed << 13;
		seed ^= seed >> 17;
		seed ^= seed << 5;
		return (seed & 0xFFFFFF) / (float)0x1000000;
	}
}
