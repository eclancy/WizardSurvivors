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
/// low over the ground with its glow pooled on the floor under it - it lies at
/// <see cref="DrawLayers.Ground"/>, under every body, as the trail a ward leaves and not a thing in the
/// air (.ai/godot-engine.md, "Draw layers").
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
	private Color glow = new(1.0f, 0.86f, 0.46f);
	private Color body = new(1.0f, 0.93f, 0.66f);
	private Color core = new(1.0f, 0.99f, 0.90f);

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

	/// <summary>The ward's own three tones, so the trail is the light that shed it.</summary>
	public void Paint(Color haloTone, Color bodyTone, Color coreTone)
	{
		glow = haloTone;
		body = bodyTone;
		core = coreTone;
	}

	public override void _Ready()
	{
		ZIndex = DrawLayers.Ground;
		ZAsRelative = false;
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

	// Each wisp: its glow pooled flat on the floor out to the radius it sears, and the mote itself
	// bobbing just over it, swelling in as it is shed and guttering as it goes out.
	public override void _Draw()
	{
		for (int i = 0; i < MaxWisps; i++)
		{
			Wisp w = wisps[i];
			if (!w.Live) continue;
			float born = Mathf.Clamp(w.Age / BirthSeconds, 0f, 1f);
			float fade = Mathf.Clamp((w.Life - w.Age) / FadeSeconds, 0f, 1f);
			float a = born * fade;
			float beat = (w.Seed % 997) * 0.013f;
			float flicker = 1f + 0.12f * MathF.Sin(clock * 8.3f + beat * 6f);
			Vector2 at = ToLocal(w.At);
			DrawSetTransform(at, 0f, new Vector2(1f, CraterGround.Squash * 0.7f));
			DrawCircle(Vector2.Zero, w.Radius * 0.95f, new Color(glow, 0.12f * a));
			DrawCircle(Vector2.Zero, w.Radius * 0.55f, new Color(glow, 0.18f * a));
			DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
			Vector2 mote = at + new Vector2(MathF.Sin(clock * 2.1f + beat * 9f) * 2f, -6f - 3f * MathF.Sin(clock * 3.3f + beat * 5f));
			DrawCircle(mote, 4.5f * flicker * (0.6f + 0.4f * fade), new Color(body, 0.5f * a));
			DrawCircle(mote, 2.2f * flicker, new Color(core, 0.95f * a));
		}
	}
}
