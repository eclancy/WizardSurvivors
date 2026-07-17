using Godot;

// One-time Buff Item (issue #25's Bonus Drop Table): grants a short temporary attack-speed and
// move-speed buff on contact via Player.ApplyTemporaryBuff().
public partial class BuffItem : PickupBase
{
	[Export] public float AttackSpeedBonus { get; set; } = 0.5f;
	[Export] public float MoveSpeedBonus { get; set; } = 0.3f;
	[Export] public float Duration { get; set; } = 8.0f;

	protected override void OnPickedUp(Node player)
	{
		if (player.HasMethod("ApplyTemporaryBuff"))
		{
			player.Call("ApplyTemporaryBuff", AttackSpeedBonus, MoveSpeedBonus, Duration);
			QueueFree();
		}
	}
}
