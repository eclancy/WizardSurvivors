using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts;

// Glacial Spike: erupts a spike of ice from the ground at a target point. It quickly spurts up
// (the spritesheet plays forward, fast), deals AoE damage to every enemy within its hit radius at
// the peak, then shakes as it retracts back into the ground (the sheet plays in reverse with a
// horizontal jitter) before freeing itself. Bigger spikes (Area) cover more ground and therefore
// hit more enemies; higher projectile counts spawn several spikes at once (see Player.FireIceSpikes).
public partial class IceSpike : Node2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float BaseHitRadius { get; set; } = 44f;
	[Export] public float ShakeAmplitude { get; set; } = 3.5f;
	[Export] public float ShakeFrequency { get; set; } = 42f;

	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;

	private AnimatedSprite2D spikeAnimation;
	private int damage = 7;
	private float hitRadius = 44f;
	private bool retracting = false;
	private bool damageDealt = false;
	private float shakeTime = 0f;
	private float baseAnimX = 0f;
	private Vector2 baseAnimScale = Vector2.One;

	public override void _Ready()
	{
		spikeAnimation = GetNodeOrNull<AnimatedSprite2D>("SpikeAnimation");
		if (spikeAnimation != null)
		{
			baseAnimX = spikeAnimation.Position.X;
			baseAnimScale = spikeAnimation.Scale;
			spikeAnimation.Connect("animation_finished", new Callable(this, nameof(OnAnimationFinished)));
		}
		RefreshComputedStats();
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	public void CastAt(Vector2 position)
	{
		GlobalPosition = position;
		retracting = false;
		damageDealt = false;
		shakeTime = 0f;
		if (spikeAnimation != null)
		{
			spikeAnimation.Position = new Vector2(baseAnimX, spikeAnimation.Position.Y);
			spikeAnimation.SpeedScale = 1.6f; // quick spurt out of the ground
			spikeAnimation.Frame = 0;
			spikeAnimation.Play("default");
		}
	}

	public override void _Process(double delta)
	{
		if (retracting && spikeAnimation != null)
		{
			shakeTime += (float)delta;
			float jitter = Mathf.Sin(shakeTime * ShakeFrequency) * ShakeAmplitude;
			spikeAnimation.Position = new Vector2(baseAnimX + jitter, spikeAnimation.Position.Y);
		}
	}

	private void OnAnimationFinished()
	{
		if (!retracting)
		{
			// Fully erupted: strike everything in range, then shake back down into the ground.
			DealDamage();
			EmitFrostBurst();
			retracting = true;
			shakeTime = 0f;
			if (spikeAnimation != null)
			{
				spikeAnimation.SpeedScale = 0.7f; // slower, trembling retract
				spikeAnimation.PlayBackwards("default");
			}
		}
		else if (IsInstanceValid(this))
		{
			QueueFree();
		}
	}

	private void DealDamage()
	{
		if (damageDealt)
			return;
		damageDealt = true;

		var player = PlayerRef as Player;
		foreach (var enemy in GetTree().GetNodesInGroup("enemies").OfType<Node2D>())
		{
			if (IsInstanceValid(enemy) && GlobalPosition.DistanceTo(enemy.GlobalPosition) <= hitRadius)
				player?.DealDamageToEnemy(enemy, damage);
		}
	}

	private void EmitFrostBurst()
	{
		var burst = GetNodeOrNull<GpuParticles2D>("FrostParticles");
		if (burst != null)
		{
			burst.Emitting = true;
			burst.Restart();
		}
	}

	private void RefreshComputedStats()
	{
		float sizeScale = AreaMultiplier;
		if (SpellData != null)
			sizeScale *= 1.0f + SpellData.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel);
		sizeScale = MathF.Max(0.25f, sizeScale);

		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 7) * DamageMultiplier));
		hitRadius = MathF.Max(6f, BaseHitRadius * sizeScale);

		if (spikeAnimation != null)
			spikeAnimation.Scale = baseAnimScale * sizeScale;
	}
}
