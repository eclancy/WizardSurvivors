using Godot;

namespace WizardSurvivors.scripts;

// Frozen Bulwark (Ice x2, issue #13): chance to freeze/root nearby attackers whenever the player
// takes damage.
public partial class FrozenBulwark : PassiveSpellEffect
{
	private const float FreezeRadius = 120f;
	private const float TriggerCooldown = 0.5f;
	private float cooldownRemaining = 0f;
	private readonly RandomNumberGenerator rng = new RandomNumberGenerator();

	public override void _Ready()
	{
		base._Ready();
		rng.Randomize();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (cooldownRemaining > 0f)
			cooldownRemaining -= (float)delta;
	}

	protected override void OnPlayerDamaged(int amount)
	{
		if (cooldownRemaining > 0f)
			return;

		cooldownRemaining = TriggerCooldown;
		float freezeChance = Mathf.Clamp(0.15f + (CurrentLevel * 0.05f), 0f, 0.6f);
		if (rng.Randf() > freezeChance)
			return;

		float freezeDuration = 1.0f + (CurrentLevel * 0.1f) + (SpellData?.GetEffectValueAtLevel(SpellEffect.RootDuration, CurrentLevel) ?? 0f);
		foreach (var enemy in GetNearbyEnemies(FreezeRadius))
		{
			if (enemy.HasMethod("ApplySlow"))
				enemy.Call("ApplySlow", 0f, freezeDuration);
		}
	}
}
