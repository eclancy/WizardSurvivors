using Godot;

namespace WizardSurvivors.scripts;

// Blur (Arcane + Wind, issue #27): illusory distortion grants a flat chance to completely avoid
// an incoming hit ("dodge"), resolved in Player.TakeDamage before the shield pool and any flat
// damage reduction. Pure passive - no pulse timer, no reaction hook.
public partial class Blur : PassiveSpellEffect
{
	public override float GetDodgeChance()
	{
		return Mathf.Min(0.40f, 0.08f + (CurrentLevel - 1) * 0.03f);
	}
}
