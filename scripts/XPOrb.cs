using Godot;
using System;

public partial class XPOrb : Area2D
{
	[Export] public int Value { get; set; } = 2;
	[Export] public float AttractDistance { get; set; } = 80f;
	[Export] public float AttractSpeed { get; set; } = 200f;
	[Export] public float HoverAmplitude { get; set; } = 2.0f;
	[Export] public float HoverSpeed { get; set; } = 3.2f;

	// Value thresholds for the three art tiers. Enemy.cs drops 2 and 3 for ordinary kills, 5 for
	// elites and 50 for bosses, so these split exactly where the drop table already does.
	[Export] public int MediumOrbValue { get; set; } = 5;
	[Export] public int LargeOrbValue { get; set; } = 25;

	private CharacterBody2D? player = null;
	private bool attracted = false;
	private AnimatedSprite2D? sprite = null;
	private Vector2 spriteBasePosition = Vector2.Zero;
	// The orb art is authored 16x16 and drawn at the contract's x2, which the scene sets. The
	// script no longer touches Scale at all - see UpdateIdleMotion.
	private Vector2 spriteBaseScale = new Vector2(2.0f, 2.0f);
	private float motionTime = 0f;
	private float motionPhase = 0f;

	public override void _Ready()
	{
		motionPhase = (GetInstanceId() % 29) * 0.19f;
		sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (sprite != null)
		{
			sprite.Scale = spriteBaseScale;
			spriteBasePosition = sprite.Position;
			sprite.Play(TierAnimation());
		}
		var cs = GetNode<CollisionShape2D>("CollisionShape2D");
		if (cs != null)
			cs.Disabled = false;
		Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
	}

	public override void _Process(double delta)
	{
		motionTime += (float)delta;
		UpdateIdleMotion();

		if (player == null)
		{
			var first = GetTree().GetFirstNodeInGroup("player");
			if (first is CharacterBody2D cb) player = cb;
		}
		if (player != null)
		{
			float dynamicAttractDistance = AttractDistance;
			if (player is Player typedPlayer)
			{
				dynamicAttractDistance += typedPlayer.MagnetBonus;
			}

			var dist = GlobalPosition.DistanceTo(player.GlobalPosition);
			if (dist < dynamicAttractDistance) attracted = true;
			if (attracted)
			{
				var dir = (player.GlobalPosition - GlobalPosition).Normalized();
				GlobalPosition += dir * AttractSpeed * (float)delta;
			}
		}
	}

	/// <summary>Which of the three tiers this orb's value puts it in. Bigger and a different
	/// hue, because size alone does not survive a crowded screen and hue alone does not survive
	/// being 16px: two orbs at different distances already look like different sizes.</summary>
	private string TierAnimation()
	{
		if (Value >= LargeOrbValue) return "large";
		if (Value >= MediumOrbValue) return "medium";
		return "small";
	}

	private void UpdateIdleMotion()
	{
		if (sprite == null)
			return;

		// The breath and the spin are in the FRAMES now, not in the transform. Rotating and
		// scaling a 16px pixel-art sprite rendered at x2 with nearest filtering is exactly the
		// sub-pixel crawl .ai/art-direction.md exists to prevent: the edge boils and the orb
		// shimmers against a floor that is holding perfectly still. Rotation is gone; Scale is
		// set once in _Ready and never touched again.
		//
		// The hover survives because it is a translation, but it is SNAPPED TO WHOLE PIXELS.
		// A float offset resamples the sprite on every frame, which is the same bug wearing a
		// different hat - and at x2 render, one art pixel is two device pixels, so quantising to
		// 2.0 keeps the orb on the same grid as everything else it sits next to.
		float hoverWave = Mathf.Sin((motionTime * HoverSpeed) + motionPhase);
		float attractBlend = attracted ? 0.45f : 1.0f;
		float offset = Mathf.Round(hoverWave * HoverAmplitude * attractBlend) * 2.0f;
		sprite.Position = spriteBasePosition + new Vector2(0f, offset);
	}

	private void OnBodyEntered(Node body)
	{
		if (body.IsInGroup("player") && body.HasMethod("AddXp"))
		{
			body.Call("AddXp", Value);
			QueueFree();
		}
	}
}
