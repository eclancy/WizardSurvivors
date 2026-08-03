using Godot;

namespace WizardSurvivors.scripts;

// Guardian Vines (Grass x2, issue #13): periodically roots/immobilizes nearby enemies.
public partial class GuardianVines : PassiveSpellEffect
{
	private const float RootRadius = 110f;

	public GuardianVines()
	{
		UsesPulseTimer = true;
	}

	protected override void OnPulseTick()
	{
		float rootDuration = 1.0f + (CurrentLevel * 0.15f) + (SpellData?.GetEffectValueAtLevel(SpellEffect.RootDuration, CurrentLevel) ?? 0f);
		foreach (var enemy in GetNearbyEnemies(RootRadius))
		{
			if (enemy.HasMethod("ApplySlow"))
				enemy.Call("ApplySlow", 0f, rootDuration);
		}
	}

	public override int GetLuckBonus() => CurrentLevel > 0 ? 1 + CurrentLevel : 0;
}
