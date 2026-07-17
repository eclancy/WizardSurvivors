using Godot;

// Health Pickup (issue #25's Bonus Drop Table): restores a flat amount of HP on contact.
public partial class HealthPickup : PickupBase
{
	[Export] public int HealAmount { get; set; } = 10;

	protected override void OnPickedUp(Node player)
	{
		if (player.HasMethod("Heal"))
		{
			player.Call("Heal", HealAmount);
			QueueFree();
		}
	}
}
