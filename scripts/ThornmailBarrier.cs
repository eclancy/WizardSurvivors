using Godot;

namespace WizardSurvivors.scripts;

// Thornmail Barrier (Earth + Grass, issue #13): retaliates against nearby enemies whenever the
// player takes damage.
public partial class ThornmailBarrier : PassiveSpellEffect
{
	private const float RetaliationRadius = 90f;
	private const float RetaliationCooldown = 0.5f;
	private float cooldownRemaining = 0f;

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (cooldownRemaining > 0f)
			cooldownRemaining -= (float)delta;
	}

	protected override void OnPlayerDamaged(int amount)
	{
		if (cooldownRemaining > 0f || OwnerPlayer == null)
			return;

		cooldownRemaining = RetaliationCooldown;
		int retaliationDamage = 3 + (CurrentLevel * 2);
		foreach (var enemy in GetNearbyEnemies(RetaliationRadius))
		{
			if (enemy.HasMethod("TakeDamage"))
				OwnerPlayer.DealDamageToEnemy(enemy, retaliationDamage);
		}
	}

	public override int GetFlatDamageReduction() => CurrentLevel > 0 ? 1 + CurrentLevel : 0;
}
