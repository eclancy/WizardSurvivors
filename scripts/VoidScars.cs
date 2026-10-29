using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

/// <summary>
/// Void Lance's Abyssal Scar (.ai/spell-roster.md section 2h): the line a lance lay along, torn into the
/// ground as it withdraws - a jagged black crack lit violet along its lips - that cuts whatever stands on
/// it for <see cref="VoidLance.ScarShare"/> of the lance every <see cref="VoidLance.ScarTickSeconds"/>
/// while it lasts. One node per arena; the lance (VoidLance.LeaveScar) hands it each scar.
/// </summary>
/// <remarks>
/// The card it replaced held the lance itself open half a second longer, which read as a lance that
/// lingered - not as anything left behind. The scar is thin and dark and lies on the ground
/// (DrawLayers.Ground), under the bodies, so a field of them reads as the floor torn up where the lance
/// has been rather than as more lance. The lance stops it short of water and walls.
///
/// Every scar is a slot in a fixed array, ticked on one clock and drawn out of the band it tests: nothing
/// is allocated per scar or per frame, and the node stops processing when nothing is live.
/// </remarks>
public partial class VoidScars : Node2D
{
	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; set; }

	private const int MaxScars = 12;
	// Half the crack's width, and a body either side of it still stands on it.
	private const float HalfWidth = 5f;
	private const float BodyAllowance = 10f;
	private const int Kinks = 9;

	private struct Scar
	{
		public Vector2 A;
		public Vector2 B;
		public float Age;
		public float Life;
		public int Damage;
		public uint Seed;
		public bool Live;
	}

	private readonly Scar[] scars = new Scar[MaxScars];
	private int next;
	private float tick;
	private uint seedClock;
	private readonly List<Enemy> scratch = new(64);

	private static readonly ElementColors.Ramp Dark = ElementColors.GetRamp(Element.Darkness);
	private static readonly Color Void = new(0.03f, 0.01f, 0.06f, 0.92f);

	private static VoidScars current;

	/// <summary>The arena's scars for this spell, made the first time a lance leaves one.</summary>
	public static VoidScars For(Node arena, SpellData spell, Player player)
	{
		if (current != null && IsInstanceValid(current) && current.IsInsideTree() && current.GetParent() == arena && current.SpellData == spell)
			return current;
		if (arena == null || !IsInstanceValid(arena) || player == null)
			return null;
		current = new VoidScars { Name = "VoidScars", SpellData = spell, PlayerRef = player };
		arena.AddChild(current);
		return current;
	}

	public override void _Ready()
	{
		ZAsRelative = false;
		ZIndex = DrawLayers.Ground;
		SetProcess(false);
	}

	public override void _ExitTree()
	{
		if (current == this)
			current = null;
	}

	/// <summary>A scar from <paramref name="a"/> to <paramref name="b"/> (world) for <paramref name="seconds"/>.</summary>
	public void Lay(Vector2 a, Vector2 b, float seconds, int tickDamage)
	{
		scars[next] = new Scar { A = a, B = b, Age = 0f, Life = seconds, Damage = tickDamage, Seed = ++seedClock * 2654435761u, Live = true };
		next = (next + 1) % MaxScars;
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		bool any = false;
		for (int i = 0; i < MaxScars; i++)
		{
			ref Scar s = ref scars[i];
			if (!s.Live)
				continue;
			s.Age += dt;
			if (s.Age >= s.Life)
			{
				s.Live = false;
				continue;
			}
			any = true;
		}
		tick -= dt;
		if (tick <= 0f)
		{
			tick = VoidLance.ScarTickSeconds;
			Cut();
		}
		QueueRedraw();
		if (!any)
			SetProcess(false);
	}

	// Every live scar cuts what stands on it, once a tick: one broad-phase query per scar, round its
	// middle, then the exact distance to its line.
	private void Cut()
	{
		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;
		float reach = HalfWidth + BodyAllowance;
		for (int i = 0; i < MaxScars; i++)
		{
			ref Scar s = ref scars[i];
			if (!s.Live)
				continue;
			Vector2 mid = (s.A + s.B) * 0.5f;
			float half = s.A.DistanceTo(s.B) * 0.5f;
			int damage = s.Damage;
			Vector2 a = s.A, b = s.B;
			EnemyRegistry.CollectNear(mid, half + reach, scratch);
			foreach (Enemy enemy in scratch)
			{
				if (!IsInstanceValid(enemy) || enemy.IsDying)
					continue;
				Vector2 p = Geometry2D.GetClosestPointToSegment(enemy.GlobalPosition, a, b);
				if (p.DistanceSquaredTo(enemy.GlobalPosition) <= reach * reach)
					PlayerRef.DealDamageToEnemy(enemy, damage, source: SpellData);
			}
			scratch.Clear();
		}
	}

	public override void _Draw()
	{
		for (int i = 0; i < MaxScars; i++)
		{
			ref Scar s = ref scars[i];
			if (!s.Live)
				continue;
			// Opens fast, holds, and closes over its last third from the ends inward.
			float t = s.Age / s.Life;
			float open = MathF.Min(1f, s.Age / 0.12f);
			float closing = t > 0.66f ? (t - 0.66f) / 0.34f : 0f;
			Vector2 a = ToLocal(s.A), b = ToLocal(s.B);
			Vector2 along = b - a;
			Vector2 side = along.Orthogonal().Normalized();
			uint seed = s.Seed;
			Vector2 prev = a.Lerp(b, closing * 0.5f);
			for (int k = 1; k <= Kinks; k++)
			{
				float f = (float)k / Kinks;
				// Rolled before the skip, so a closing scar keeps the shape it opened with.
				float jag = k == Kinks ? 0f : (Unit(ref seed) - 0.5f) * 9f;
				if (f < closing * 0.5f || f > 1f - closing * 0.5f)
					continue;
				Vector2 p = a + along * f + side * jag;
				float w = HalfWidth * 2f * open * (1f - closing * 0.6f);
				DrawLine(prev, p, Dark.Edge with { A = 0.7f }, w + 3f);
				DrawLine(prev, p, Void, w);
				DrawLine(prev, p, Dark.Hot with { A = 0.55f * (1f - closing) }, 1.2f);
				prev = p;
			}
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
