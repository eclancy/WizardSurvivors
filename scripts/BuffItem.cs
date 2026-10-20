using Godot;
using WizardSurvivors.scripts;

// One-time Buff Item (issue #25's Bonus Drop Table): grants a short temporary attack-speed and
// move-speed buff on contact via Player.ApplyTemporaryBuff(). The player calls it the speed potion.
//
// It used to be silent and invisible: the only sign it had been taken was the flask disappearing.
// Now it has a moment of its own - a gust and a climbing shimmer (pickup_haste), a burst of Wind
// motes on the player (PickupFlourish), and afterimages that settle into a faint streak at his
// feet for as long as the buff runs (HasteTrail), so "am I still fast?" can be read off him.
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
			SfxPlayer.AtPosition(SfxCatalog.PickupHaste, GlobalPosition, 0.03f);
			if (player is Player typed)
			{
				PickupFlourish.Play(typed, PickupFlourish.Kind.Haste);
				HasteTrail.Attach(typed);
			}
			QueueFree();
		}
	}
}
