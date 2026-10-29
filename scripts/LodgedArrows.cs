using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// Hunter's Draw's Barbed (.ai/spell-roster.md 2e): the arrows that stopped in an enemy, still in it,
/// and bleeding it. One node per arena draws and ticks every one of them.
/// </summary>
/// <remarks>
/// An arrow lodges where it is spent - in the last enemy its pierce reached - at the angle it flew,
/// head buried and shaft and fletching standing out of the body, and goes with the enemy wherever it
/// walks. While it is in, the wound bleeds: a share of the arrow's hit every tick, a drop of blood
/// falling from it each time, for BarbedSeconds; then it works loose and is gone. Several in one enemy
/// bleed it several times over, as barbs would. An enemy that dies or is recycled to the spawn ring
/// (a jump no walk could make) loses its arrows.
///
/// A fixed array of slots, drawn in one pass (ImpactDebrisLayer's way): nothing is instantiated or
/// allocated per arrow, and the node stops processing when none is lodged. In the air
/// (DrawLayers.Air) - it is in the body, over it, never under the feet.
/// </remarks>
public partial class LodgedArrows : Node2D
{
	private const int MaxArrows = 64;
	private const float ShaftLength = 22f;
	private const float LooseSeconds = 0.25f;
	private const float RecycleJump = 160f;

	private struct Barb
	{
		public Enemy Host;
		public Vector2 Offset;
		public Vector2 Heading;
		public Vector2 LastAt;
		public float Age;
		public float Tick;
		public float DropAge;
		public int Damage;
		public SpellData Spell;
		public bool Live;
	}

	private readonly Barb[] barbs = new Barb[MaxArrows];
	private readonly Vector2[] fletch = new Vector2[3];
	private int next;
	private int live;
	private Player player;

	private static LodgedArrows current;

	private static readonly Color Outline = new Color(0.02f, 0.03f, 0.05f, 0.9f);
	private static readonly Color Shaft = new Color(0.66f, 0.93f, 1.0f, 1f);
	private static readonly Color Blood = new Color(0.62f, 0.06f, 0.08f, 1f);

	/// <summary>Lodges an arrow that flew along <paramref name="heading"/> in <paramref name="host"/>.</summary>
	public static void Lodge(Node2D arena, Enemy host, Vector2 heading, int hitDamage, SpellData spell, Player player)
	{
		if (arena == null || !IsInstanceValid(arena) || host == null || !IsInstanceValid(host) || host.IsDying)
			return;
		if (current == null || !IsInstanceValid(current) || current.GetParent() != arena)
		{
			current = new LodgedArrows { Name = "LodgedArrows" };
			arena.AddChild(current);
		}
		current.player = player;
		current.Add(host, heading, Math.Max(1, Mathf.RoundToInt(hitDamage * BoltEvolutions.BarbedTickShare)), spell);
	}

	public override void _Ready()
	{
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		SetProcess(false);
	}

	private void Add(Enemy host, Vector2 heading, int tickDamage, SpellData spell)
	{
		ref Barb barb = ref barbs[next];
		if (!barb.Live)
			live++;
		next = (next + 1) % MaxArrows;
		// In the body, a little above the origin and off centre, so three arrows in one skeleton are three.
		float spread = (next * 7 % 11 - 5) * 1.4f;
		barb = new Barb
		{
			Host = host,
			Offset = new Vector2(spread, -8f + (next * 5 % 7 - 3)),
			Heading = heading.LengthSquared() > 0.0001f ? heading.Normalized() : Vector2.Right,
			LastAt = host.GlobalPosition,
			Age = 0f,
			Tick = BoltEvolutions.BarbedTickSeconds,
			DropAge = 1f,
			Damage = tickDamage,
			Spell = spell,
			Live = true,
		};
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		for (int i = 0; i < MaxArrows; i++)
		{
			ref Barb barb = ref barbs[i];
			if (!barb.Live)
				continue;
			Enemy host = barb.Host;
			bool gone = host == null || !IsInstanceValid(host) || host.IsDying;
			if (!gone && host.GlobalPosition.DistanceSquaredTo(barb.LastAt) > RecycleJump * RecycleJump)
				gone = true;
			barb.Age += dt;
			if (gone || barb.Age >= BoltEvolutions.BarbedSeconds)
			{
				barb.Live = false;
				barb.Host = null;
				barb.Spell = null;
				live--;
				continue;
			}
			barb.LastAt = host.GlobalPosition;
			barb.DropAge += dt;
			barb.Tick -= dt;
			if (barb.Tick <= 0f)
			{
				barb.Tick += BoltEvolutions.BarbedTickSeconds;
				barb.DropAge = 0f;
				if (player != null && IsInstanceValid(player))
					player.DealDamageToEnemy(host, barb.Damage, source: barb.Spell);
			}
		}
		QueueRedraw();
		if (live <= 0)
		{
			live = 0;
			SetProcess(false);
		}
	}

	public override void _Draw()
	{
		DrawSetTransformMatrix(GlobalTransform.AffineInverse());
		for (int i = 0; i < MaxArrows; i++)
		{
			ref Barb barb = ref barbs[i];
			if (!barb.Live || barb.Host == null || !IsInstanceValid(barb.Host))
				continue;
			float left = BoltEvolutions.BarbedSeconds - barb.Age;
			float alpha = left < LooseSeconds ? left / LooseSeconds : 1f;
			// Working loose at the end: the shaft sags a few degrees before it falls out.
			Vector2 heading = left < LooseSeconds ? barb.Heading.Rotated(0.5f * (1f - alpha)) : barb.Heading;
			Vector2 wound = barb.Host.GlobalPosition + barb.Offset;
			Vector2 tail = wound - heading * ShaftLength;
			Vector2 across = heading.Orthogonal() * 4.5f;

			DrawLine(wound + heading * 2f, tail - heading * 2f, Outline with { A = Outline.A * alpha }, 5f);
			DrawLine(wound, tail, Shaft with { A = alpha }, 2.4f);
			fletch[0] = tail + heading * 7f + across;
			fletch[1] = tail;
			fletch[2] = tail + heading * 7f - across;
			DrawPolyline(fletch, Outline with { A = Outline.A * alpha }, 4f);
			DrawPolyline(fletch, Shaft.Lightened(0.2f) with { A = alpha }, 2f);
			// The wound: a dark bead where the shaft goes in, and a drop of blood falling from it each tick.
			DrawCircle(wound, 2.4f, Blood with { A = alpha });
			if (barb.DropAge < 0.4f)
			{
				float fall = barb.DropAge / 0.4f;
				Vector2 drop = wound + new Vector2(0f, 3f + 14f * fall);
				drop = new Vector2(Mathf.Round(drop.X / 2f) * 2f, Mathf.Round(drop.Y / 2f) * 2f);
				DrawRect(new Rect2(drop, new Vector2(2f, 3f)), Blood with { A = alpha * (1f - fall * 0.6f) });
			}
		}
	}
}
