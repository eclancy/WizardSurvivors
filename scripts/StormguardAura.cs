using Godot;
using System.Linq;

namespace WizardSurvivors.scripts;

// Stormguard Aura (Lightning + Metal, issue #13): strikes the nearest enemy with retaliatory
// lightning whenever the player takes damage.
public partial class StormguardAura : PassiveSpellEffect
{
	private const float StrikeRadius = 220f;
	private const float TriggerCooldown = 0.4f;
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

		cooldownRemaining = TriggerCooldown;
		var nearest = GetNearbyEnemies(StrikeRadius)
			.OrderBy(e => OwnerPlayer.GlobalPosition.DistanceTo(e.GlobalPosition))
			.FirstOrDefault();

		if (nearest != null && nearest.HasMethod("TakeDamage"))
		{
			int strikeDamage = 4 + (CurrentLevel * 3);
			nearest.Call("TakeDamage", strikeDamage);
		}
	}
}
