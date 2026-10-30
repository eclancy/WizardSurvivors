using Godot;
using WizardSurvivors.scripts;

// What the speed potion looks like while it lasts (BuffItem, Player.ApplyTemporaryBuff).
//
// Two layers, on one clock:
//  - For the first second, AFTERIMAGES: the player's own past poses left behind him, tinted Wind,
//    each fainter than the one in front. This is the "you are fast now" beat.
//  - For the rest of the buff, a faint STREAK AT HIS FEET while he moves - three short wind lines
//    trailing behind his boots, shortening over the buff's last second, so the player can tell the
//    potion is still running without a HUD icon and can see it run out.
//
// A child of the player at ZIndex -1: under him and under the swarm, on the ground, which is where
// a trail belongs and where it cannot hide an enemy. Past positions are kept in world space and
// converted with ToLocal at draw time. Every buffer is allocated once, here; nothing per frame.
//
// One per player. A second potion refreshes it (Player.ApplyTemporaryBuff refreshes the buff the
// same way) rather than stacking a second trail.
//
// Two nodes of this class, one per layer, because the layers need different materials: the ghosts
// go through a small shader that draws the sprite as a flat Wind-coloured silhouette (a tinted copy of
// the sprite kept the purple robe and read as a second wizard, not as speed), and the streaks are
// plain stamped cells.
//
// PIXEL ART (2026-10-29, "Update all spells to the same quality we expect now"). The ghosts were drawn
// at fractional alpha, so six overlapping ones made a soft translucent smear, and the streaks were
// additive antialiased lines that faded in alpha. Now a ghost is opaque Wind mid where it is drawn at
// all, and it is thinned by an ordered Bayer dither on the sprite's own art pixels - the older the ghost,
// the fewer of its pixels - so it stays as crisp as the sprite it was taken from; and the streaks are
// opaque Wind cells on the world's 2 px grid that shorten as the potion runs out instead of fading.
public partial class HasteTrail : Node2D
{
	// presence (the draw's modulate alpha, 0..1) is how many of the silhouette's art pixels are drawn:
	// a 4x4 Bayer threshold on the texel, so the dither is the sprite's own grid.
	private const string SilhouetteShader = @"shader_type canvas_item;
uniform vec4 tint : source_color;
varying float presence;
float bayer4(vec2 c) {
	int x = int(mod(c.x, 4.0));
	int y = int(mod(c.y, 4.0));
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[x + y * 4] + 0.5) / 16.0;
}
void vertex() {
	presence = COLOR.a;
}
void fragment() {
	vec4 texel = texture(TEXTURE, UV);
	vec2 art_pixel = floor(UV / TEXTURE_PIXEL_SIZE);
	if (texel.a < 0.5 || presence <= bayer4(art_pixel))
		discard;
	COLOR = vec4(tint.rgb, 1.0);
}";

	private bool streakLayer;
	private const int Ghosts = 6;
	private const float SampleSeconds = 0.045f;
	private const float AfterimageSeconds = 1.0f;
	private const float AfterimageFadeSeconds = 0.4f;
	private const int Streaks = 3;

	private Player player;
	private AnimatedSprite2D body;
	private Node2D feet;

	private readonly Vector2[] ghostAt = new Vector2[Ghosts];
	private readonly Texture2D[] ghostFrame = new Texture2D[Ghosts];
	private readonly bool[] ghostFlip = new bool[Ghosts];
	private int ghostCount;
	private int ghostHead;
	private float sampleTimer;
	private float sinceRefresh;
	private float clock;

	// Wind's ramp (.ai/art-direction.md section 3), held saturated because Wind is one of the four
	// cold elements that compete with a cold floor.
	private static readonly Color GhostTint = new Color(0.706f, 0.933f, 0.878f, 1f);   // Wind mid  #B4EEE0
	private static readonly Color StreakColour = new Color(0.918f, 0.988f, 0.965f, 1f); // Wind hot #EAFCF6
	private static readonly Color StreakEdge = new Color(0.310f, 0.682f, 0.620f, 1f);   // Wind edge #4FAE9E

	/// <summary>Starts the trail on <paramref name="player"/>, or restarts the one already there.</summary>
	public static void Attach(Player player)
	{
		if (player == null || !IsInstanceValid(player))
			return;
		if (player.GetNodeOrNull<HasteTrail>("HasteTrail") is HasteTrail existing)
		{
			existing.Refresh();
			return;
		}
		player.AddChild(new HasteTrail { Name = "HasteTrail", player = player });
		player.AddChild(new HasteTrail { Name = "HasteStreaks", player = player, streakLayer = true });
	}

	public override void _Ready()
	{
		ZIndex = -1;
		body = player?.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		feet = player?.GetNodeOrNull<Node2D>("Feet/CollisionShape2D");
		if (!streakLayer)
		{
			var silhouette = new ShaderMaterial { Shader = new Shader { Code = SilhouetteShader } };
			silhouette.SetShaderParameter("tint", GhostTint);
			Material = silhouette;
		}
		Refresh();
	}

	private void Refresh()
	{
		sinceRefresh = 0f;
		ghostCount = 0;
		sampleTimer = 0f;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (player == null || !IsInstanceValid(player) || player.TemporaryBuffSecondsRemaining <= 0f)
		{
			QueueFree();
			return;
		}
		sinceRefresh += dt;
		clock += dt;

		sampleTimer -= dt;
		if (!streakLayer && sampleTimer <= 0f && sinceRefresh < AfterimageSeconds + AfterimageFadeSeconds)
		{
			sampleTimer = SampleSeconds;
			SampleGhost();
		}
		QueueRedraw();
	}

	private void SampleGhost()
	{
		if (body?.SpriteFrames == null)
			return;
		Vector2 at = player.GlobalPosition;
		// Only while he moves: a ghost on top of a standing player is just a smudge.
		int newest = (ghostHead - 1 + Ghosts) % Ghosts;
		if (ghostCount > 0 && ghostAt[newest].DistanceSquaredTo(at) < 9f)
			return;
		ghostAt[ghostHead] = at;
		ghostFrame[ghostHead] = body.SpriteFrames.GetFrameTexture(body.Animation, body.Frame);
		ghostFlip[ghostHead] = body.FlipH;
		ghostHead = (ghostHead + 1) % Ghosts;
		ghostCount = Mathf.Min(Ghosts, ghostCount + 1);
	}

	public override void _Draw()
	{
		if (streakLayer)
			DrawStreaks();
		else
			DrawAfterimages();
	}

	private void DrawAfterimages()
	{
		if (body == null || ghostCount == 0)
			return;
		float strength = sinceRefresh < AfterimageSeconds
			? 1f
			: 1f - Mathf.Clamp((sinceRefresh - AfterimageSeconds) / AfterimageFadeSeconds, 0f, 1f);
		if (strength <= 0f)
			return;

		// Oldest first, so the newest ghost - nearest the player and the most opaque - is on top.
		for (int k = ghostCount - 1; k >= 0; k--)
		{
			int index = (ghostHead - 1 - k + Ghosts * 2) % Ghosts;
			Texture2D frame = ghostFrame[index];
			if (frame == null)
				continue;
			Vector2 size = frame.GetSize();
			Vector2 scale = body.Scale * new Vector2(ghostFlip[index] ? -1f : 1f, 1f);
			Vector2 origin = ToLocal(ghostAt[index]) + body.Position;
			Vector2 topLeft = (body.Centered ? -size / 2f : Vector2.Zero) + body.Offset;
			float presence = strength * 0.6f * (1f - (float)k / Ghosts);
			DrawSetTransform(origin, 0f, scale);
			// The shader replaces the colour with the tint; the alpha is how much of it is drawn.
			DrawTexture(frame, topLeft, new Color(1f, 1f, 1f, presence));
		}
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	private void DrawStreaks()
	{
		Vector2 velocity = player.Velocity;
		float speed = velocity.Length();
		if (speed < 20f)
			return;
		// Shortening over the buff's last second, so it can be seen running out - and shorter at a walk.
		float remaining = Mathf.Clamp(player.TemporaryBuffSecondsRemaining, 0f, 1f);
		float strength = remaining * Mathf.Clamp(speed / 160f, 0f, 1f);
		if (strength <= 0.05f)
			return;

		Vector2 dir = velocity / speed;
		Vector2 side = new Vector2(-dir.Y, dir.X);
		Vector2 foot = feet != null ? ToLocal(feet.GlobalPosition) + new Vector2(0f, 6f) : new Vector2(0f, 30f);
		float g = PixelCells.Grid;
		for (int i = 0; i < Streaks; i++)
		{
			// The outer two go first as it runs out.
			if (i != 1 && strength < 0.4f)
				continue;
			// Each line slides back along the trail and restarts, so the streak flows rather than sits; it
			// is shorter the further back it has slid.
			float phase = Mathf.PosMod(clock * 3.2f + i * 0.37f, 1f);
			float lateral = (i - 1) * 6f;
			Vector2 start = foot - dir * (4f + 10f * phase) + side * lateral;
			float length = (15f + 8f * (1f - phase)) * strength * (1f - phase * 0.6f);
			Color colour = i == 1 ? StreakColour : StreakEdge;
			for (float d = 0f; d <= length; d += g)
				DrawRect(PixelCells.Snap(this, start - dir * d, g), d > length * 0.6f ? StreakEdge : colour);
		}
	}
}
