using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// Chain Lightning's Electrocution (.ai/spell-roster.md section 2h): the first enemy each cast strikes
/// is held in the current - convulsing, arcs crawling over it, struck again every
/// <see cref="ChainLightning.ElectrocuteTickSeconds"/> - while the chain hops on. One node per arena; the
/// bolt (ChainLightning) hands it each body to hold.
/// </summary>
/// <remarks>
/// The card it replaced (Thunderhead: twice the damage and a hop) was the spell's answer to an elite: all
/// of it pointed at one thing. This is the same answer made visible - the bolt's nearest-first aim means
/// the body it holds is the one walking at the wizard, and an elite or a boss that keeps walking at him
/// is held again every cast. The shake is Enemy.ApplyShock, the stagger every hop already gives, renewed
/// on every tick; the hits pass the spell as their source (so the Lattice links what is held).
///
/// A fixed array of holds, ticked and drawn here: nothing is allocated per hold or per frame, and the node
/// stops processing when nothing is held. In the air (DrawLayers.Air), over the body it holds.
///
/// Pixel art on the world's 2-unit grid since 2026-10-29 (PixelInk; .ai/art-replacement-manifest.md
/// section 9): the crawl and the leap are stamped cell by cell - a gold edge under a white-hot seam, as
/// Chain Lightning's arcs are - and the light on the body is a dithered patch, where they were
/// antialiased DrawLines under a translucent outline and a translucent DrawCircle.
/// </remarks>
public partial class Electrocutions : Node2D
{
	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; set; }

	private const int MaxHeld = 8;
	// The crawl round the body: radius, height of its middle above the feet, and arcs at once.
	private const float CrawlRadius = 18f;
	private const float CrawlLift = 13f;
	private const int CrawlArcs = 3;

	private struct Grip
	{
		public Enemy Target;
		public ulong Id;
		public float Left;
		public float Tick;
		public int Damage;
		public uint Seed;
		public bool Live;
		public Vector2 LastAt;
		// Where the current leapt from when its last body died, and how long ago: drawn for a moment.
		public Vector2 JumpFrom;
		public float JumpAge;
	}

	private const float JumpShowSeconds = 0.12f;
	private readonly System.Collections.Generic.List<Enemy> scratch = new(32);

	// The nearest living enemy to where a held body fell, not one already held.
	private Enemy NearestTo(Vector2 at, Enemy fallen)
	{
		Enemy best = null;
		float bestD = ChainLightning.ElectrocuteJumpReach * ChainLightning.ElectrocuteJumpReach;
		EnemyRegistry.CollectNear(at, ChainLightning.ElectrocuteJumpReach, scratch);
		foreach (Enemy enemy in scratch)
		{
			if (enemy == fallen || !IsInstanceValid(enemy) || enemy.IsDying || IsHeld(enemy))
				continue;
			float d = at.DistanceSquaredTo(enemy.GlobalPosition);
			if (d < bestD)
			{
				bestD = d;
				best = enemy;
			}
		}
		scratch.Clear();
		return best;
	}

	private bool IsHeld(Enemy enemy)
	{
		for (int i = 0; i < MaxHeld; i++)
		{
			if (holds[i].Live && holds[i].Target == enemy)
				return true;
		}
		return false;
	}

	private readonly Grip[] holds = new Grip[MaxHeld];
	private int next;
	private float clock;
	private uint seedClock;

	private static readonly ElementColors.Ramp Lightning = ElementColors.GetRamp(Element.Lightning);

	private static Electrocutions current;

	/// <summary>The arena's holds for this spell, made the first time a bolt holds anything.</summary>
	public static Electrocutions For(Node arena, SpellData spell, Player player)
	{
		if (current != null && IsInstanceValid(current) && current.IsInsideTree() && current.GetParent() == arena && current.SpellData == spell)
			return current;
		if (arena == null || !IsInstanceValid(arena) || player == null)
			return null;
		current = new Electrocutions { Name = "Electrocutions", SpellData = spell, PlayerRef = player };
		arena.AddChild(current);
		return current;
	}

	public override void _Ready()
	{
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		// At the world's origin, unturned and unscaled, so its units are the world's and so is its grid.
		TopLevel = true;
		GlobalPosition = Vector2.Zero;
		SetProcess(false);
	}

	public override void _ExitTree()
	{
		if (current == this)
			current = null;
	}

	/// <summary>Holds <paramref name="target"/> for <paramref name="seconds"/>, striking it for <paramref name="tickDamage"/> each tick.</summary>
	public void Hold(Enemy target, float seconds, int tickDamage)
	{
		if (target == null || !IsInstanceValid(target) || target.IsDying)
			return;
		ulong id = target.GetInstanceId();
		int slot = -1;
		for (int i = 0; i < MaxHeld; i++)
		{
			if (holds[i].Live && holds[i].Id == id)
			{
				slot = i;
				break;
			}
		}
		if (slot < 0)
		{
			slot = next;
			next = (next + 1) % MaxHeld;
		}
		// A body already held is held again for the full time, its tick clock left running.
		float tick = holds[slot].Live && holds[slot].Id == id ? holds[slot].Tick : ChainLightning.ElectrocuteTickSeconds;
		holds[slot] = new Grip { Target = target, Id = id, Left = seconds, Tick = tick, Damage = tickDamage, Seed = ++seedClock * 2654435761u, Live = true,
			LastAt = target.GlobalPosition, JumpAge = JumpShowSeconds };
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		bool any = false;
		for (int i = 0; i < MaxHeld; i++)
		{
			ref Grip h = ref holds[i];
			if (!h.Live)
				continue;
			if (!IsInstanceValid(h.Target) || h.Target.IsDying)
			{
				// Killed in the current: it leaps to the nearest enemy and holds that for what is left.
				Enemy next = h.Left > ChainLightning.ElectrocuteTickSeconds ? NearestTo(h.LastAt, h.Target) : null;
				if (next == null)
				{
					h.Live = false;
					h.Target = null;
					continue;
				}
				h.JumpFrom = h.LastAt;
				h.JumpAge = 0f;
				h.Target = next;
				h.Id = next.GetInstanceId();
			}
			h.LastAt = h.Target.GlobalPosition;
			h.JumpAge += dt;
			h.Left -= dt;
			h.Tick -= dt;
			if (h.Tick <= 0f)
			{
				h.Tick += ChainLightning.ElectrocuteTickSeconds;
				Enemy target = h.Target;
				target.ApplyShock(ChainLightning.ElectrocuteTickSeconds + 0.05f, 4.5f, 54f);
				if (PlayerRef != null && IsInstanceValid(PlayerRef))
					PlayerRef.DealDamageToEnemy(target, h.Damage, allowElementalChain: false, source: SpellData);
			}
			if (h.Left <= 0f)
			{
				h.Live = false;
				h.Target = null;
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
		for (int i = 0; i < MaxHeld; i++)
		{
			ref Grip h = ref holds[i];
			if (!h.Live || !IsInstanceValid(h.Target))
				continue;
			Vector2 centre = ToLocal(h.Target.GlobalPosition) + new Vector2(0f, -CrawlLift);
			float fade = MathF.Min(1f, h.Left / 0.2f);
			// The arcs re-roll fifteen times a second, so the current visibly crawls over the body.
			uint seed = h.Seed + (uint)(clock * 15f) * 2246822519u;
			for (int k = 0; k < CrawlArcs; k++)
			{
				float a0 = Unit(ref seed) * Mathf.Tau;
				float a1 = a0 + 1.6f + Unit(ref seed) * 1.6f;
				Vector2 prev = centre + new Vector2(MathF.Cos(a0), MathF.Sin(a0) * 1.15f) * CrawlRadius;
				const int Steps = 4;
				for (int s = 1; s <= Steps; s++)
				{
					float a = a0 + (a1 - a0) * s / Steps;
					float rr = CrawlRadius * (0.55f + 0.6f * Unit(ref seed));
					Vector2 p = centre + new Vector2(MathF.Cos(a), MathF.Sin(a) * 1.15f) * rr;
					PixelInk.Line(this, prev, p, Lightning.Edge, 2, fade * 1.5f);
					PixelInk.Line(this, prev, p, s % 2 == 0 ? Lightning.Core : Lightning.Hot, 1, fade * 1.5f);
					prev = p;
				}
			}
			// A flicker of the current's light on the body, strongest on the tick.
			// The leap from a body that died held to the one it holds now, for a moment.
			if (h.JumpAge < JumpShowSeconds)
			{
				Vector2 from = ToLocal(h.JumpFrom) + new Vector2(0f, -CrawlLift);
				Vector2 to = centre;
				Vector2 side = (to - from).Orthogonal().Normalized();
				Vector2 prevJump = from;
				uint jseed = h.Seed ^ 0x9E3779B9u;
				for (int s = 1; s <= 4; s++)
				{
					Vector2 p = from.Lerp(to, s / 4f) + (s < 4 ? side * (Unit(ref jseed) - 0.5f) * 14f : Vector2.Zero);
					PixelInk.Line(this, prevJump, p, Lightning.Edge, 3);
					PixelInk.Line(this, prevJump, p, Lightning.Core, 1);
					prevJump = p;
				}
			}
			float flash = 1f - Mathf.Clamp(h.Tick / ChainLightning.ElectrocuteTickSeconds, 0f, 1f);
			PixelInk.Disc(this, centre, 5f + 4f * flash, Lightning.Hot, (0.2f + 0.45f * flash) * fade);
		}
	}

	private static float Unit(ref uint seed)
	{
		seed ^= seed << 13;
		seed ^= seed >> 17;
		seed ^= seed << 5;
		return (seed & 0xFFFFFF) / (float)0x1000000;
	}
}
