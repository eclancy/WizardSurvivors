namespace WizardSurvivors.scripts;

// Fortune's Favor (Arcane + Light, issue #27): grants a flat bonus to the player's effective Luck
// stat while equipped, stacking with shop/map Luck levels (#23). Pure passive - no pulse timer,
// no reaction hook; Player.GetEffectiveLuckLevel() sums this across all equipped copies.
public partial class FortunesFavor : PassiveSpellEffect
{
	public override int GetLuckBonus()
	{
		return 2 + (CurrentLevel - 1);
	}
}
