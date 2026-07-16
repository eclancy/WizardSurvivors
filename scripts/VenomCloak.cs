using Godot;

namespace WizardSurvivors.scripts;

// Venom Cloak (Poison + Darkness, issue #13): periodically poisons nearby enemies.
public partial class VenomCloak : PassiveSpellEffect
{
	private const float PoisonRadius = 100f;
	private const float PoisonDuration = 3.0f;

	public VenomCloak()
	{
		UsesPulseTimer = true;
	}

	protected override void OnPulseTick()
	{
		int damagePerTick = 1 + (CurrentLevel / 2);
		foreach (var enemy in GetNearbyEnemies(PoisonRadius))
		{
			if (enemy.HasMethod("ApplyPoison"))
				enemy.Call("ApplyPoison", damagePerTick, PoisonDuration);
		}
	}
}
