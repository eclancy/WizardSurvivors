using Godot;

namespace WizardSurvivors.scripts;

// Haste (Wind + Lightning, issue #28): the first "buff the player's own stats" passive - a pulse
// that periodically grants a temporary attack-speed/move-speed buff via Player.ApplyTemporaryBuff()
// (the same mechanism BuffItem/#25 uses), distinct from the existing defensive-only passive roster
// (shields/reflect/DoT/root).
public partial class Haste : PassiveSpellEffect
{
	public Haste()
	{
		UsesPulseTimer = true;
	}

	protected override void OnPulseTick()
	{
		if (OwnerPlayer == null)
			return;

		float attackSpeedBonus = 0.15f + (CurrentLevel - 1) * 0.05f;
		float moveSpeedBonus = 0.10f + (CurrentLevel - 1) * 0.03f;
		OwnerPlayer.ApplyTemporaryBuff(attackSpeedBonus, moveSpeedBonus, 3.0f);
	}
}
