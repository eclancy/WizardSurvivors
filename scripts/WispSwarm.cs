using Godot;
using System;

namespace WizardSurvivors.scripts;

// Mirefoot's Will-o'-Wisps (TrailWeaver.WillOWispsId, its Ascension, 2026-10-29): marsh lights rise out of
// the pools and go looking for something to burn.
//
// Each pool of the trail lets a wisp up now and then (TrailWeaver spreads Interval over its pools). A
// wisp climbs out of the water for RiseSeconds, then fixes on the nearest living enemy within SeekRange of
// itself and flies at it, turning as it goes; touching it, the wisp breaks on it for its damage and is
// gone. One that finds nothing drifts on its own and fades out at the end of its Life. Every hit is one
// the eye saw: a wisp strikes only the body it was seen to reach.
//
// Drawn in the air over every body (DrawLayers.Air) - a lit mote with a halo and a short tail, in the
// Grass ramp, flickering - on the 2 px art grid, in opaque stops; going out, it sheds itself to a point. One node per arena (For); a fixed ring of wisps; the
// nearest-enemy query is the enemy grid's with a struct visitor; nothing allocated per frame.
public partial class WispSwarm : Node2D
{
	/// <summary>Seconds between one wisp of a pool and its next (spread over the trail's pools).</summary>
	public const float Interval = 1.3f;
	/// <summary>A wisp's hit, as a multiple of one bite of the bog.</summary>
	public const float Share = 2.5f;
	public const float SeekRange = 240f;
	private const float Speed = 260f;
	private const float Turn = 7f;
	private const float RiseSeconds = 0.3f;
	private const float Life = 3.2f;
	private const float HitRadius = 13f;
	private const int MaxWisps = 32;

	private static WispSwarm live;

	private struct Wisp
	{
		public Vector2 At;
		public Vector2 Velocity;
		public float Born;
		public Enemy Target;
		public int Damage;
		public float Seed;
		public bool Live;
	}

	private readonly Wisp[] wisps = new Wisp[MaxWisps];
	private int next;
	private float clock;
	private bool drew;
	private SpellData source;
	private Player player;

	private static readonly Color Occ = new Color(0.020f, 0.027f, 0.047f, 1f);
	private static readonly Color Core = new Color(0.957f, 1.0f, 0.863f, 1f);   // grass core #F4FFDC
	private static readonly Color Hot = new Color(0.812f, 0.933f, 0.580f, 1f);  // grass hot #CFEE94
	private static readonly Color Mid = new Color(0.608f, 0.831f, 0.333f, 1f);  // grass mid #9BD455
	private static readonly Color Tail = new Color(0.706f, 0.863f, 1.0f, 1f);   // water hot #B4DCFF

	public static WispSwarm For(Node2D arena)
	{
		if (live != null && IsInstanceValid(live) && live.GetParent() == arena)
			return live;
		live = new WispSwarm();
		arena.AddChild(live);
		return live;
	}

	public override void _Ready()
	{
		Name = "WispSwarm";
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
	}

	public override void _ExitTree()
	{
		if (live == this)
			live = null;
	}

	/// <summary>A wisp rises out of the bog at <paramref name="at"/> (world), to strike for <paramref name="damage"/>.</summary>
	public void Rise(Vector2 at, int damage, SpellData spell, Player owner)
	{
		source = spell;
		player = owner;
		wisps[next] = new Wisp
		{
			At = at,
			Velocity = new Vector2(0f, -40f),
			Born = clock,
			Damage = Math.Max(1, damage),
			Seed = (next * 0.618034f + clock) % 1f * Mathf.Tau,
			Live = true,
		};
		next = (next + 1) % MaxWisps;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		bool any = false;
		for (int i = 0; i < MaxWisps; i++)
		{
			ref Wisp w = ref wisps[i];
			if (!w.Live)
				continue;
			float age = clock - w.Born;
			if (age >= Life)
			{
				w.Live = false;
				continue;
			}
			any = true;
			if (age < RiseSeconds)
			{
				w.At += w.Velocity * dt;
				continue;
			}
			if (w.Target == null || !IsInstanceValid(w.Target) || w.Target.IsDying)
			{
				var nearest = new NearestLiving { From = w.At, BestDistance = float.MaxValue };
				EnemyRegistry.ForEachNear(w.At, SeekRange, ref nearest);
				w.Target = nearest.Best;
			}
			Vector2 want = w.Velocity;
			if (w.Target != null)
			{
				Vector2 to = w.Target.GlobalPosition - w.At;
				float d = to.Length();
				if (d <= HitRadius)
				{
					w.Live = false;
					if (player != null && IsInstanceValid(player))
						player.DealDamageToEnemy(w.Target, w.Damage, source: source);
					continue;
				}
				want = to / d * Speed;
			}
			w.Velocity = w.Velocity.Lerp(want, Mathf.Clamp(Turn * dt, 0f, 1f));
			w.At += w.Velocity * dt;
		}
		if (any || drew)
			QueueRedraw();
		drew = any;
	}

	// Opaque Grass stops on the 2 px grid. A wisp going out sheds itself from the outside in - its tail,
	// then its halo, then its glow, to one white-hot pixel - rather than fading (2026-10-29: it faded in
	// alpha over its last 0.4 s, and its tail was drawn at 80%).
	public override void _Draw()
	{
		float g = WhipLimb.Grid;
		for (int i = 0; i < MaxWisps; i++)
		{
			if (!wisps[i].Live)
				continue;
			float age = clock - wisps[i].Born;
			float fade = Mathf.Clamp((Life - age) / 0.4f, 0f, 1f);
			// A flicker of its own: every few frames the halo drops out for one.
			bool lit = MathF.Sin(age * 23f + wisps[i].Seed * 7f) > -0.7f;
			Vector2 p = ToLocal(wisps[i].At);
			Vector2 back = wisps[i].Velocity.LengthSquared() > 1f ? -wisps[i].Velocity.Normalized() : Vector2.Down;
			if (fade >= 1f)
			{
				DrawRect(WhipLimb.Cell(p + back * g * 3f, g), Tail);
				DrawRect(WhipLimb.Cell(p + back * g * 5f, g), Mid);
			}
			if (fade > 0.25f)
				DrawRect(WhipLimb.Cell(p, g * 4f), Occ);
			if (lit && fade > 0.6f)
			{
				DrawRect(WhipLimb.Cell(p + new Vector2(0f, -g * 2f), g), Mid);
				DrawRect(WhipLimb.Cell(p + new Vector2(0f, g * 2f), g), Mid);
				DrawRect(WhipLimb.Cell(p + new Vector2(-g * 2f, 0f), g), Mid);
				DrawRect(WhipLimb.Cell(p + new Vector2(g * 2f, 0f), g), Mid);
			}
			if (fade > 0.25f)
				DrawRect(WhipLimb.Cell(p, g * 2f), Hot);
			DrawRect(WhipLimb.Cell(p, g), Core);
		}
	}

	private struct NearestLiving : INeighbourVisitor
	{
		public Vector2 From;
		public Enemy Best;
		public float BestDistance;

		public void Visit(Enemy enemy, Vector2 position)
		{
			if (enemy.IsDying)
				return;
			float d = From.DistanceSquaredTo(position);
			if (d < BestDistance)
			{
				BestDistance = d;
				Best = enemy;
			}
		}
	}
}
