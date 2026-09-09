using Godot;
using WizardSurvivors.scripts;

// Mini-boss reward pickup: grants one level immediately without adding XP.
public partial class LevelUpPickup : PickupBase
{
	protected override void OnPickedUp(Node player)
	{
		if (player.HasMethod("LevelUpImmediately"))
		{
			player.Call("LevelUpImmediately");
			SfxPlayer.Global(SfxCatalog.LevelUp);
			QueueFree();
		}
	}
}
