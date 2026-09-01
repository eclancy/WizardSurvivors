using Godot;
using System;

public partial class XPOrb : Area2D
{
	[Export] public int Value { get; set; } = 2;
	[Export] public float AttractDistance { get; set; } = 80f;
	[Export] public float AttractSpeed { get; set; } = 200f;
	[Export] public float HoverAmplitude { get; set; } = 2.5f;
	[Export] public float HoverSpeed { get; set; } = 3.2f;
	[Export] public float PulseStrength { get; set; } = 0.06f;
	[Export] public float SpinSpeed { get; set; } = 1.4f;

	private CharacterBody2D? player = null;
	private bool attracted = false;
	private AnimatedSprite2D? sprite = null;
	private Vector2 spriteBasePosition = Vector2.Zero;
	// The orb art is 16x16 like the rest of the pixel art, so it renders about 1:1 (the old
	// 150x150 gem had to be squeezed to 0.2 to fit, which is why it looked out of style).
	private Vector2 spriteBaseScale = new Vector2(1.0f, 1.0f);
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
			sprite.Play("default");
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

	private void UpdateIdleMotion()
	{
		if (sprite == null)
			return;

		float hoverWave = Mathf.Sin((motionTime * HoverSpeed) + motionPhase);
		float pulseWave = Mathf.Sin((motionTime * (HoverSpeed * 1.5f)) + motionPhase * 1.6f);
		float attractBlend = attracted ? 0.45f : 1.0f;

		sprite.Position = spriteBasePosition + new Vector2(0f, hoverWave * HoverAmplitude * attractBlend);
		sprite.Rotation = motionTime * SpinSpeed * attractBlend;
		sprite.Scale = spriteBaseScale * (1.0f + pulseWave * PulseStrength * attractBlend);
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
