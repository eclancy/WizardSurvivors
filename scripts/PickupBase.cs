using Godot;

// Shared base for magnet-attracted ground pickups (issue #25's Bonus Drop Table: HealthPickup,
// BuffItem). Handles the move-toward-player-on-proximity behavior (same pattern as XPOrb.cs) so
// each pickup type only needs to implement its own OnPickedUp() effect.
public partial class PickupBase : Area2D
{
	[Export] public float AttractDistance { get; set; } = 80f;
	[Export] public float AttractSpeed { get; set; } = 200f;

	private CharacterBody2D? player = null;
	private bool attracted = false;

	public override void _Ready()
	{
		Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
	}

	public override void _Process(double delta)
	{
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

	private void OnBodyEntered(Node body)
	{
		if (body.IsInGroup("player"))
			OnPickedUp(body);
	}

	protected virtual void OnPickedUp(Node player) { }
}
