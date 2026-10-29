using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// Wisp Trail (Kindled Ward's level 4, .ai/spell-roster.md section 2e): the small lights a drifting ward
/// leaves behind it, each hanging low where it was shed for a few seconds and searing whatever walks
/// into it. One node per arena for every ward of the spell, so a ward that gutters out does not take its
/// trail with it.
/// </summary>
/// <remarks>
/// Every wisp is a slot in a fixed array, ticked on one half-second clock and drawn out of the radius
/// it tests, so nothing is allocated per wisp or per frame. A wisp is a mote of the ward's own light
/// low over the ground with flecks of its light settled on the floor under it, out to the radius it
/// sears - it lies at <see cref="DrawLayers.Ground"/>, under every body, as the trail a ward leaves and
/// not a thing in the air (.ai/godot-engine.md, "Draw layers").
///
/// Pixel art since 2026-10-29 (scenes/shaders/ward_wisp.gdshader): the glow was two translucent squashed
/// DrawCircles a wisp, which doubled into a brighter lens wherever two overlapped. Now one quad a wisp,
/// its numbers in the vertex colour, every wisp sharing this node's material - opaque cells of the ward's
/// ramp, Bayer dithering the only gradient.
/// </remarks>
public partial class WardWisps : Node2D
{
	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; set; }

	private const int MaxWisps = 72;
	private const float TickSeconds = 0.5f;
	private const float FadeSeconds = 0.6f;
	private const float BirthSeconds = 0.18f;

	private struct Wisp
	{
		public Vector2 At;
		public float Radius;
		public float Age;
		public float Life;
		public int Damage;
		public uint Seed;
		public bool Live;
	}

	private readonly Wisp[] wisps = new Wisp[MaxWisps];
	private int next;
	private uint seedClock;
	private float tick = TickSeconds;
	private float clock;
	private ShaderMaterial material;
	private static Shader wispShader;
	private static readonly StringName ParamTime = "time";
	// The radius rides in a colour channel as radius / RadiusScale (ward_wisp.gdshader).
	private const float RadiusScale = 128f;
	// The quad's least half height, to hold the mote floating over a small wisp (the shader's LIFT).
	private const float Lift = 14f;

	private static WardWisps current;

	/// <summary>The arena's wisp layer for this spell, made the first time a ward sheds one.</summary>
	public static WardWisps For(Node2D arena, SpellData spell, Player player)
	{
		if (current != null && IsInstanceValid(current) && current.GetParent() == arena && current.SpellData == spell)
			return current;
		current = new WardWisps { Name = "WardWisps", SpellData = spell, PlayerRef = player };
		arena.AddChild(current);
		return current;
	}

	/// <summary>The ward's own ramp, so the trail is the light that shed it.</summary>
	public void Paint(in ElementColors.Ramp ramp)
	{
		EnsureMaterial();
		material.SetShaderParameter("tone_core", ramp.Core);
		material.SetShaderParameter("tone_hot", ramp.Hot);
		material.SetShaderParameter("tone_mid", ramp.Mid);
		material.SetShaderParameter("tone_edge", ramp.Edge);
	}

	private void EnsureMaterial()
	{
		if (material != null)
			return;
		wispShader ??= GD.Load<Shader>("res://scenes/shaders/ward_wisp.gdshader");
		material = new ShaderMaterial { Shader = wispShader };
		Material = material;
	}

	public override void _Ready()
	{
		ZIndex = DrawLayers.Ground;
		ZAsRelative = false;
		EnsureMaterial();
		SetProcess(false);
	}

	public override void _ExitTree()
	{
		if (current == this)
			current = null;
	}

	public void Shed(Vector2 at, float radius, float seconds, int damage)
	{
		wisps[next] = new Wisp { At = at, Radius = radius, Age = 0f, Life = seconds, Damage = damage, Seed = ++seedClock * 2654435761u, Live = true };
		next = (next + 1) % MaxWisps;
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		material?.SetShaderParameter(ParamTime, clock);
		int live = 0;
		for (int i = 0; i < MaxWisps; i++)
		{
			if (!wisps[i].Live) continue;
			wisps[i].Age += dt;
			if (wisps[i].Age >= wisps[i].Life) wisps[i].Live = false;
			else live++;
		}
		QueueRedraw();
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
		Sear();
	}

	// One pass over the swarm a tick: a body standing in several wisps is seared once, by the strongest.
	private void Sear()
	{
		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (!IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			Vector2 p = enemy.GlobalPosition;
			int hurt = 0;
			for (int i = 0; i < MaxWisps; i++)
			{
				ref Wisp w = ref wisps[i];
				if (w.Live && w.Damage > hurt && p.DistanceSquaredTo(w.At) <= w.Radius * w.Radius)
					hurt = w.Damage;
			}
			if (hurt > 0)
				PlayerRef.DealDamageToEnemy(enemy, hurt, source: SpellData);
		}
	}

	// Each wisp: one quad round it, its numbers in the vertex colour (ward_wisp.gdshader) - the light it
	// has laid on the floor out to the radius it sears, and the mote bobbing just over it, swelling in as
	// it is shed and guttering as it goes out. The node's modulate must stay white.
	public override void _Draw()
	{
		Texture2D quad = ScorchGround.QuadTexture;
		for (int i = 0; i < MaxWisps; i++)
		{
			Wisp w = wisps[i];
			if (!w.Live) continue;
			float born = Mathf.Clamp(w.Age / BirthSeconds, 0f, 1f);
			float fade = Mathf.Clamp((w.Life - w.Age) / FadeSeconds, 0f, 1f);
			float radius = MathF.Min(w.Radius, RadiusScale - 1f);
			// On an even world pixel, so the wisp's art grid is the world's.
			Vector2 at = ToLocal(w.At);
			at = new Vector2(MathF.Round(at.X * 0.5f) * 2f, MathF.Round(at.Y * 0.5f) * 2f);
			var extent = new Vector2(radius, MathF.Max(radius * CraterGround.Squash, Lift));
			var parameters = new Color(born * fade, radius / RadiusScale, (w.Seed % 997u) / 997f, MathF.Max(0.01f, fade));
			DrawTextureRect(quad, new Rect2(at - extent, extent * 2f), false, parameters);
		}
	}
}
