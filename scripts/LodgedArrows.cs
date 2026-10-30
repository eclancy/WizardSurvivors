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
///
/// Pixel art (2026-10-29): each arrow was a stroked shaft and fletching over a translucent outline, a soft
/// circle for the wound and a translucent drop. Each is now one quad of
/// scenes/shaders/hunters_arrow.gdshader (mode B): the arrow the bow looses, its head buried in the body,
/// on the world's 2 px grid with an art pixel of occlusion behind it, the bead of blood at the wound and
/// the drop falling from it drawn in the same pass, in the red row's lit and base stops. It works loose
/// by tilting and dissolving through the Bayer matrix instead of fading.
/// </remarks>
public partial class LodgedArrows : Node2D
{
	private const int MaxArrows = 64;
	private const float ShaftLength = 22f;
	// The head is in the body: the point this far past the wound.
	private const float HeadDepth = 6f;
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
	private int next;
	private int live;
	private Player player;

	private static LodgedArrows current;

	private static Shader arrowShader;
	private static readonly StringName ParamWorldQuads = "world_quads";
	private static readonly StringName ParamArrowLength = "arrow_length";
	private static readonly StringName ParamWound = "wound";
	private static readonly StringName ParamGilded = "gilded";
	private ShaderMaterial material;
	private readonly Vector2[] quad = new Vector2[4];
	private readonly Color[] quadColours = new Color[4];
	private readonly Vector2[] quadUv = new Vector2[4];

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
		current.material?.SetShaderParameter(ParamGilded, spell?.IsLegendary ?? false);
		current.Add(host, heading, Math.Max(1, Mathf.RoundToInt(hitDamage * BoltEvolutions.BarbedTickShare)), spell);
	}

	public override void _Ready()
	{
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		arrowShader ??= GD.Load<Shader>("res://scenes/shaders/hunters_arrow.gdshader");
		material = new ShaderMaterial { Shader = arrowShader };
		Material = material;
		material.SetShaderParameter(ParamWorldQuads, true);
		material.SetShaderParameter(ParamArrowLength, ShaftLength + HeadDepth + 2f);
		material.SetShaderParameter(ParamWound, true);
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
		Transform2D toLocal = GlobalTransform.AffineInverse();
		float half = ShaftLength + HeadDepth + 12f;
		quadUv[0] = new Vector2(-half, -half);
		quadUv[1] = new Vector2(half, -half);
		quadUv[2] = new Vector2(half, half);
		quadUv[3] = new Vector2(-half, half);
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
			// The point is in the body, HeadDepth past the wound; the shader buries exactly that much. The
			// fletching's point runs the way the arrow flew, into the body.
			Vector2 point = wound + heading * HeadDepth;
			float drop = barb.DropAge < 0.4f ? MathF.Max(0.01f, barb.DropAge / 0.4f) : 0f;
			Color data = new Color(Mathf.PosMod(heading.Angle(), Mathf.Tau) / Mathf.Tau, HeadDepth / 64f, alpha, drop);
			quadColours[0] = quadColours[1] = quadColours[2] = quadColours[3] = data;
			for (int v = 0; v < 4; v++)
				quad[v] = toLocal * (point + quadUv[v]);
			DrawPrimitive(quad, quadColours, quadUv);
		}
	}
}
