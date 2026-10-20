using Godot;
using WizardSurvivors.scripts;

// Health Pickup (issue #25's Bonus Drop Table): restores a flat amount of HP on contact.
//
// It had its sound but nothing to see; it now has the same small burst as the speed potion, in
// Light's warm ramp and rising rather than thrown outward (PickupFlourish.Kind.Mend).
public partial class HealthPickup : PickupBase
{
	[Export] public int HealAmount { get; set; } = 10;

	protected override void OnPickedUp(Node player)
	{
		if (player.HasMethod("Heal"))
		{
			player.Call("Heal", HealAmount);
			SfxPlayer.AtPosition(SfxCatalog.PickupHealth, GlobalPosition, 0.03f);
			if (player is Node2D body)
				PickupFlourish.Play(body, PickupFlourish.Kind.Mend);
			QueueFree();
		}
	}
}
