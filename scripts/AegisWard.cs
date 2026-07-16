using Godot;

namespace WizardSurvivors.scripts;

// Aegis Ward (Metal + Light, issue #13): periodically grants the player an absorbing shield.
public partial class AegisWard : PassiveSpellEffect
{
	public AegisWard()
	{
		UsesPulseTimer = true;
	}

	protected override void OnPulseTick()
	{
		if (OwnerPlayer == null)
			return;

		int shieldAmount = 8 + (CurrentLevel * 4);
		OwnerPlayer.AddShield(shieldAmount);
	}
}
