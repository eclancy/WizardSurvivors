using Godot;

// What the speed potion looks like while it lasts (BuffItem, Player.ApplyTemporaryBuff).
//
// Two layers, on one clock:
//  - For the first second, AFTERIMAGES: the player's own past poses left behind him, tinted Wind,
//    each fainter than the one in front. This is the "you are fast now" beat.
//  - For the rest of the buff, a faint STREAK AT HIS FEET while he moves - three short wind lines
//    trailing behind his boots, fading out over the buff's last second, so the player can tell the
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
// go through a one-line shader that draws the sprite as a flat Wind-coloured silhouette (a tinted
// copy of the sprite kept the purple robe and read as a second wizard, not as speed), and the
// streaks are drawn additively so a thin line still shows on a dark forest floor.
public partial class HasteTrail : Node2D
{
	private const string SilhouetteShader = @"shader_type canvas_item;
uniform vec4 tint : source_color;
void fragment() {
	COLOR = vec4(tint.rgb, COLOR.a);
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
		if (streakLayer)
		{
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		}
		else
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
			float alpha = strength * 0.5f * (1f - (float)k / Ghosts);
			DrawSetTransform(origin, 0f, scale);
			// The shader replaces the colour with the tint; only the alpha is read from here.
			DrawTexture(frame, topLeft, new Color(1f, 1f, 1f, alpha));
		}
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	private void DrawStreaks()
	{
		Vector2 velocity = player.Velocity;
		float speed = velocity.Length();
		if (speed < 20f)
			return;
		// Fade over the buff's last second, so it can be seen running out.
		float remaining = Mathf.Clamp(player.TemporaryBuffSecondsRemaining, 0f, 1f);
		float alpha = 0.7f * remaining * Mathf.Clamp(speed / 160f, 0f, 1f);
		if (alpha <= 0.01f)
			return;

		Vector2 dir = velocity / speed;
		Vector2 side = new Vector2(-dir.Y, dir.X);
		Vector2 foot = feet != null ? ToLocal(feet.GlobalPosition) + new Vector2(0f, 6f) : new Vector2(0f, 30f);
		for (int i = 0; i < Streaks; i++)
		{
			// Each line slides back along the trail and restarts, so the streak flows rather than sits.
			float phase = Mathf.PosMod(clock * 3.2f + i * 0.37f, 1f);
			float lateral = (i - 1) * 6f;
			Vector2 start = foot - dir * (4f + 10f * phase) + side * lateral;
			Vector2 end = start - dir * (15f + 8f * (1f - phase));
			float a = alpha * (1f - phase);
			DrawLine(start, end, new Color(i == 1 ? StreakColour : StreakEdge, a), i == 1 ? 2.5f : 2f);
		}
	}
}
