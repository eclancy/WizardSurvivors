using Godot;
using System;

namespace WizardSurvivors.scripts;

// Generic IPulseVisual for any ElementalPulse spell that wants a real sprite-sheet animation
// instead of the procedural PlaceholderShape ring: it holds one AnimatedSprite2D child, scales it
// to the pulse's actual damage radius, and replays the animation from frame 0 on every pulse.
//
// It also draws a thin ring at that radius. The artwork alone is decorative and does not tell the
// player how far the pulse actually reaches, which the ring it replaced did do - so the ring stays,
// just underneath the art.
//
// Kept spell-agnostic on purpose: art, tint and frame rate all live in the .tscn, so a new pulse
// spell only needs a "Visual" node with this script and its own SpriteFrames.
public partial class PulseSpriteVisual : Node2D, IPulseVisual
{
	// Width in pixels of one animation frame at scale 1, used to turn the spell's radius into a
	// sprite scale so an Area-boosted pulse actually draws bigger.
	[Export] public float FrameSize { get; set; } = 72f;
	// How much of that frame the artwork fills. The pack's frames leave a margin, so scaling on
	// frame size alone would draw the effect smaller than the damage radius.
	[Export] public float ArtworkFillFraction { get; set; } = 0.82f;
	[Export] public float GroundOffset { get; set; } = 4f;
	[Export] public float GroundSquash { get; set; } = 0.62f;
	[Export] public float RingDuration { get; set; } = 0.42f;
	[Export] public Color RingColor { get; set; } = new Color(0.55f, 1.0f, 0.36f, 0.75f);

	private AnimatedSprite2D? sprite;
	private float radius = 90f;
	private float ringElapsed = 0f;
	private bool ringActive = false;

	public override void _Ready()
	{
		sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (sprite != null)
			sprite.Visible = false;
	}

	public void Burst(float burstRadius)
	{
		radius = MathF.Max(8f, burstRadius);
		ringElapsed = 0f;
		ringActive = true;

		if (sprite != null)
		{
			float artworkPixels = MathF.Max(1f, FrameSize * ArtworkFillFraction);
			float scale = (radius * 2f) / artworkPixels;
			// Squashed vertically so the burst reads as spreading across the floor rather than as
			// a sphere centred on the wizard's chest.
			sprite.Scale = new Vector2(scale, scale * GroundSquash);
			sprite.Position = new Vector2(0f, GroundOffset);
			sprite.Visible = true;
			sprite.Frame = 0;
			sprite.Play();
		}
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		// One-shot animations stop on their last frame; hide once finished so a long cooldown does
		// not leave a frozen cloud sitting on the ground.
		if (sprite != null && sprite.Visible && !sprite.IsPlaying())
			sprite.Visible = false;

		if (!ringActive)
			return;

		ringElapsed += (float)delta;
		if (ringElapsed >= RingDuration)
			ringActive = false;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (!ringActive)
			return;

		float progress = Mathf.Clamp(ringElapsed / MathF.Max(0.01f, RingDuration), 0f, 1f);
		float wave = 1f - (1f - progress) * (1f - progress);
		float fade = 1f - progress;

		DrawSetTransform(new Vector2(0f, GroundOffset), 0f, new Vector2(1f, GroundSquash));
		DrawArc(Vector2.Zero, MathF.Max(1f, radius * wave), 0f, Mathf.Tau, 48,
			new Color(RingColor.R, RingColor.G, RingColor.B, RingColor.A * fade),
			Mathf.Lerp(1.5f, 6f, fade), true);
	}
}
