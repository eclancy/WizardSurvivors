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
		int dotBonus = Mathf.RoundToInt(SpellData?.GetEffectValueAtLevel(SpellEffect.DotDamage, CurrentLevel) ?? 0f);
		int damagePerTick = Mathf.Max(1, 1 + (CurrentLevel / 2) + dotBonus);
		foreach (var enemy in GetNearbyEnemies(PoisonRadius))
		{
			if (enemy.HasMethod("ApplyPoison"))
				enemy.Call("ApplyPoison", damagePerTick, PoisonDuration);
		}
	}

	public override int GetLuckBonus() => CurrentLevel > 0 ? 1 + (CurrentLevel / 2) : 0;
}
