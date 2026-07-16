using Godot;

namespace WizardSurvivors.scripts;

// Tidal Barrier (Water + Wind, issue #13): periodically knocks back and slows nearby enemies.
public partial class TidalBarrier : PassiveSpellEffect
{
	private const float PulseRadius = 100f;
	private const float SlowMultiplier = 0.5f;
	private const float SlowDuration = 1.5f;

	public TidalBarrier()
	{
		UsesPulseTimer = true;
	}

	protected override void OnPulseTick()
	{
		if (OwnerPlayer == null)
			return;

		float knockbackStrength = 150f + (CurrentLevel * 10f);
		foreach (var enemy in GetNearbyEnemies(PulseRadius))
		{
			var direction = enemy.GlobalPosition - OwnerPlayer.GlobalPosition;
			direction = direction.Length() > 0.01f ? direction.Normalized() : Vector2.Right;

			if (enemy.HasMethod("ApplyKnockback"))
				enemy.Call("ApplyKnockback", direction * knockbackStrength);
			if (enemy.HasMethod("ApplySlow"))
				enemy.Call("ApplySlow", SlowMultiplier, SlowDuration);
		}
	}
}
