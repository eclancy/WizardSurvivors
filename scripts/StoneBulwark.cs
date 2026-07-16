namespace WizardSurvivors.scripts;

// Stone Bulwark (Earth + Metal, issue #13): purely passive flat damage reduction (armor). No
// timer or reaction needed - Player.TakeDamage() sums GetFlatDamageReduction() across all
// equipped passive spells.
public partial class StoneBulwark : PassiveSpellEffect
{
	public override int GetFlatDamageReduction()
	{
		return 1 + CurrentLevel;
	}
}
