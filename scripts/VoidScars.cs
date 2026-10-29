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
/// Every scar is a slot in a fixed array, ticked on one clock: nothing is allocated per scar or per
/// frame, and the node stops processing when nothing is live. Each slot owns one quad (ScarQuad), made
/// once with the node, that scenes/shaders/void_scar.gdshader paints as pixel art out of the very
/// numbers Cut measures - HalfWidth for the crack, HalfWidth + BodyAllowance for the fissures off it
/// (2026-10-29; it was three antialiased polylines a scar).
/// </remarks>
public partial class VoidScars : Node2D
{
	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; set; }

	private const int MaxScars = 12;
	// Half the crack's width, and a body either side of it still stands on it.
	private const float HalfWidth = 5f;
	private const float BodyAllowance = 10f;

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
	private readonly ScarQuad[] quads = new ScarQuad[MaxScars];
	private float clock;

	private static Shader scarShader;
	private static readonly StringName ParamLength = "scar_length";
	private static readonly StringName ParamHalfWidth = "half_width";
	private static readonly StringName ParamReach = "reach";
	private static readonly StringName ParamOpen = "open";
	private static readonly StringName ParamClosing = "closing";
	private static readonly StringName ParamTime = "time";
	private static readonly StringName ParamSeed = "seed";
	private static readonly StringName ParamGilded = "gilded";
	// How long a scar takes to tear open, and the share of its life it spends closing.
	private const float OpenSeconds = 0.12f;
	private const float ClosingShare = 0.34f;

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
		scarShader ??= GD.Load<Shader>("res://scenes/shaders/void_scar.gdshader");
		bool gilded = SpellData?.IsLegendary ?? false;
		for (int i = 0; i < MaxScars; i++)
		{
			var material = new ShaderMaterial { Shader = scarShader };
			material.SetShaderParameter(ParamHalfWidth, HalfWidth);
			material.SetShaderParameter(ParamReach, HalfWidth + BodyAllowance);
			material.SetShaderParameter(ParamGilded, gilded);
			quads[i] = new ScarQuad { Name = $"Scar{i}", Material = material, Visible = false, Reach = HalfWidth + BodyAllowance };
			AddChild(quads[i]);
		}
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
		ScarQuad quad = quads[next];
		if (quad != null)
		{
			// Laid along its line in the world: the shader measures every cell in the scar's own frame.
			quad.GlobalPosition = a;
			quad.GlobalRotation = (b - a).Angle();
			quad.Length = a.DistanceTo(b);
			var material = (ShaderMaterial)quad.Material;
			material.SetShaderParameter(ParamLength, quad.Length);
			material.SetShaderParameter(ParamSeed, (scars[next].Seed % 997u) * 0.731f);
			material.SetShaderParameter(ParamOpen, 0f);
			material.SetShaderParameter(ParamClosing, 0f);
			quad.Visible = true;
			quad.QueueRedraw();
		}
		next = (next + 1) % MaxScars;
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
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
				if (quads[i] != null)
					quads[i].Visible = false;
				continue;
			}
			any = true;
			if (quads[i]?.Material is ShaderMaterial material)
			{
				// Tears open fast, holds, and closes over its last third from the ends inward.
				float t = s.Age / s.Life;
				material.SetShaderParameter(ParamOpen, MathF.Min(1f, s.Age / OpenSeconds));
				material.SetShaderParameter(ParamClosing, t > 1f - ClosingShare ? (t - (1f - ClosingShare)) / ClosingShare : 0f);
				material.SetShaderParameter(ParamTime, clock);
			}
		}
		tick -= dt;
		if (tick <= 0f)
		{
			tick = VoidLance.ScarTickSeconds;
			Cut();
		}
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

	/// <summary>One scar's quad, laid along it: the shader decides every pixel and discards the rest.</summary>
	private partial class ScarQuad : Node2D
	{
		public float Length { get; set; }
		public float Reach { get; set; }

		public override void _Draw()
		{
			if (Length > 0f)
				DrawRect(new Rect2(-2f, -Reach - 2f, Length + 4f, Reach * 2f + 4f), Colors.White);
		}
	}
}
