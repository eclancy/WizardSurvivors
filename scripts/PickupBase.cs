using Godot;

// Shared base for magnet-attracted ground pickups (issue #25's Bonus Drop Table: HealthPickup,
// BuffItem). Handles the move-toward-player-on-proximity behavior (same pattern as XPOrb.cs) so
// each pickup type only needs to implement its own OnPickedUp() effect.
public partial class PickupBase : Area2D
{
	[Export] public float AttractDistance { get; set; } = 80f;
	[Export] public float AttractSpeed { get; set; } = 200f;
	[Export] public float HoverAmplitude { get; set; } = 3.5f;
	[Export] public float HoverSpeed { get; set; } = 2.6f;
	[Export] public float SpinSpeed { get; set; } = 0.8f;
	[Export] public float PulseStrength { get; set; } = 0.08f;

	private CharacterBody2D? player = null;
	private bool attracted = false;
	private Node2D? visualRoot = null;
	private Vector2 visualBasePosition = Vector2.Zero;
	private Vector2 visualBaseScale = Vector2.One;
	private float motionTime = 0f;
	private float motionPhase = 0f;

	public override void _Ready()
	{
		motionPhase = (GetInstanceId() % 31) * 0.17f;
		visualRoot = GetNodeOrNull<Node2D>("PlaceholderShape");
		if (visualRoot != null)
		{
			visualBasePosition = visualRoot.Position;
			visualBaseScale = visualRoot.Scale;
		}
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
				dynamicAttractDistance += typedPlayer.MagnetBonus;

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
		if (visualRoot == null)
			return;

		float hoverWave = Mathf.Sin((motionTime * HoverSpeed) + motionPhase);
		float pulseWave = Mathf.Sin((motionTime * (HoverSpeed * 1.35f)) + motionPhase * 1.7f);
		float attractBlend = attracted ? 0.35f : 1.0f;

		visualRoot.Position = visualBasePosition + new Vector2(0f, hoverWave * HoverAmplitude * attractBlend);
		visualRoot.Rotation = hoverWave * SpinSpeed * 0.08f * attractBlend;
		visualRoot.Scale = visualBaseScale * (1.0f + pulseWave * PulseStrength * attractBlend);
	}

	private void OnBodyEntered(Node body)
	{
		if (body.IsInGroup("player"))
			OnPickedUp(body);
	}

	protected virtual void OnPickedUp(Node player) { }
}
