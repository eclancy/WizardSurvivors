using Godot;

namespace WizardSurvivors.scripts;

// Tidal Barrier (Water + Wind, issue #13): periodically knocks back and slows nearby enemies.
public partial class TidalBarrier : PassiveSpellEffect
{
	private const float PulseRadius = 100f;
	private const float SlowMultiplier = 0.5f;
	private const float SlowDuration = 1.5f;
	private const float WaveDuration = 0.34f;
	private float waveTime = -1f;

	public TidalBarrier()
	{
		UsesPulseTimer = true;
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (waveTime < 0f)
			return;

		waveTime += (float)delta;
		if (waveTime > WaveDuration)
			waveTime = -1f;
		QueueRedraw();
	}

	protected override void OnPulseTick()
	{
		if (OwnerPlayer == null)
			return;

		waveTime = 0f;
		QueueRedraw();

		float knockbackBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.Knockback, CurrentLevel) ?? 0f;
		float slowPower = SpellData?.GetEffectValueAtLevel(SpellEffect.SlowPower, CurrentLevel) ?? 0f;
		float slowDurationBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.SlowDuration, CurrentLevel) ?? 0f;
		float knockbackStrength = 150f + (CurrentLevel * 10f) + knockbackBonus;
		float scaledSlowMultiplier = Mathf.Clamp(SlowMultiplier - slowPower, 0f, 0.98f);
		float scaledSlowDuration = Mathf.Max(0.1f, SlowDuration + slowDurationBonus);
		foreach (var enemy in GetNearbyEnemies(PulseRadius))
		{
			var direction = enemy.GlobalPosition - OwnerPlayer.GlobalPosition;
			direction = direction.Length() > 0.01f ? direction.Normalized() : Vector2.Right;

			if (enemy.HasMethod("ApplyKnockback"))
				enemy.Call("ApplyKnockback", direction * knockbackStrength);
			if (enemy.HasMethod("ApplySlow"))
				enemy.Call("ApplySlow", scaledSlowMultiplier, scaledSlowDuration);
		}
	}

	public override void _Draw()
	{
		if (waveTime < 0f)
			return;

		float t = Mathf.Clamp(waveTime / WaveDuration, 0f, 1f);
		float eased = 1f - Mathf.Pow(1f - t, 2f);
		float radius = Mathf.Lerp(14f, PulseRadius, eased);
		float fade = 1f - t;

		// Core surge disk.
		DrawCircle(Vector2.Zero, radius * 0.52f, new Color(0.24f, 0.62f, 1.0f, 0.10f * fade));

		// Expanding push ring.
		DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 56, new Color(0.38f, 0.82f, 1.0f, 0.86f * fade), Mathf.Lerp(10f, 2f, t), true);

		// Secondary inner wave for layered water feel.
		DrawArc(Vector2.Zero, radius * 0.72f, 0f, Mathf.Tau, 44, new Color(0.58f, 0.90f, 1.0f, 0.62f * fade), Mathf.Lerp(7f, 1.5f, t), true);
	}
}
