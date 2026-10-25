using Godot;
using System;

/// <summary>
/// A static, cycling arena hazard: a plate of holes that spikes shoot up out of, or an iron floor
/// vent that breathes fire. Damages the player while it is active and never touches enemies.
///
/// Stages were previously flat, empty fields where the only thing to read was the swarm. A hazard
/// gives the floor itself an opinion: it punishes the panicked backpedal into a corner and rewards
/// knowing the ground. The cycle is deliberately slow and telegraphed - the art spends
/// <see cref="RiseSeconds"/> visibly warning before it can hurt anything - so a death is always
/// the player's read being wrong, never the hazard being unfair.
/// </summary>
/// <remarks>
/// THE ART IS THE TELL AND THE PLATE IS THE AREA (2026-10). Reported: "the floor fire traps should be
/// an animated vent, and the floor spikes should be a bunch of holes." Each hazard's SpriteFrames
/// (tools/art/props_fx.py, which writes the .tres too) has one animation per phase - `idle`, `tell`,
/// `active`, `cool` - and this plays them from its own clock: the warning is the holes' tips glinting
/// and the grate's slots heating up, not a ring. The ring this used to draw round both is gone,
/// because the plate and the grate are now drawn exactly the size of the CollisionShape2D, and the
/// player is tested by his BOOTS against that shape (Player.FootprintOverlaps) - so a boot on the
/// iron is a hit and a boot off it never is. It used to test his body, the middle of his sprite 36 px
/// above his boots, which made the danger a patch of floor south of the art that nothing drew.
///
/// Unchanged: the phases, their durations, the damage, the bite on the frame it arms, the repeat
/// interval and the start offsets. A hazard is still an Area2D (nothing else needs to change for it),
/// but it no longer monitors: there is one player, and his footprint is asked directly while active.
/// </remarks>
public partial class StageHazard : Area2D
{
	private enum Phase { Dormant, Rising, Active, Falling }

	[Export] public int Damage { get; set; } = 3;
	/// <summary>How long it sits harmless between cycles.</summary>
	[Export] public float DormantSeconds { get; set; } = 2.6f;
	/// <summary>Telegraph window: warning, and visibly so, but not yet dangerous.</summary>
	[Export] public float RiseSeconds { get; set; } = 0.45f;
	/// <summary>How long it stays out and damaging.</summary>
	[Export] public float ActiveSeconds { get; set; } = 1.1f;
	[Export] public float FallSeconds { get; set; } = 0.35f;
	/// <summary>Seconds between repeat hits while the player stands in it.</summary>
	[Export] public float DamageInterval { get; set; } = 0.6f;
	/// <summary>Randomised at spawn so a field of hazards does not pulse in lockstep.</summary>
	[Export] public float StartPhaseOffset { get; set; } = 0f;

	/// <summary>
	/// True for art whose `active` animation is a loop (the vent's fire); false for one that is a
	/// motion and then a pose (the spikes shoot up over <see cref="ActiveShootSeconds"/> and hold).
	/// </summary>
	[Export] public bool LoopWhileActive { get; set; } = false;

	/// <summary>How long a non-looping `active` animation takes to play through before holding its last frame.</summary>
	[Export] public float ActiveShootSeconds { get; set; } = 0.08f;

	private static readonly StringName AnimIdle = "idle";
	private static readonly StringName AnimTell = "tell";
	private static readonly StringName AnimActive = "active";
	private static readonly StringName AnimCool = "cool";

	// Anything further than this from the hazard cannot have a boot on it; skips the shape test.
	private const float ReachSquared = 160f * 160f;

	private AnimatedSprite2D? sprite;
	private CollisionShape2D? area;
	private Phase phase = Phase.Dormant;
	private float phaseTimer;
	private float damageTimer;
	private bool playerInside;

	public override void _Ready()
	{
		AddToGroup("stage_hazards");
		sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		area = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		// Nothing detects it and it detects nothing: the player's boots are asked directly (above).
		// Enemies walk over hazards untouched: making them two-sided would turn every hazard into a
		// free turret and gut the risk.
		CollisionLayer = 0;
		CollisionMask = 0;
		Monitoring = false;
		Monitorable = false;

		// Under the characters but above the floor, so a hazard never hides an enemy.
		ZIndex = -1;
		phaseTimer = -Mathf.Abs(StartPhaseOffset);
		ApplyFrameForPhase(0f);
	}

	public override void _PhysicsProcess(double delta)
	{
		float d = (float)delta;
		phaseTimer += d;

		float duration = phase switch
		{
			Phase.Dormant => DormantSeconds,
			Phase.Rising => RiseSeconds,
			Phase.Active => ActiveSeconds,
			_ => FallSeconds
		};

		if (phaseTimer >= duration)
		{
			phaseTimer = 0f;
			phase = phase switch
			{
				Phase.Dormant => Phase.Rising,
				Phase.Rising => Phase.Active,
				Phase.Active => Phase.Falling,
				_ => Phase.Dormant
			};
			if (phase == Phase.Active)
				damageTimer = DamageInterval;   // bite immediately on the frame it arms
		}

		ApplyFrameForPhase(duration <= 0f ? 1f : Mathf.Clamp(phaseTimer / duration, 0f, 1f));

		if (phase != Phase.Active)
			return;
		Player? player = Node2DGame.Current?.RunPlayer;
		playerInside = player != null && !player.IsDead && BootOnIt(player);
		if (!playerInside)
			return;

		damageTimer += d;
		if (damageTimer < DamageInterval)
			return;

		damageTimer = 0f;
		player!.TakeDamage(Damage);
		BitesLanded++;
	}

	/// <summary>Bites landed on the player this process, for _BalanceHarness --check-feet: proof the boots test fires.</summary>
	public static int BitesLanded { get; private set; }

	// The player's footprint against this hazard's own shape - the shape the plate is drawn to.
	private bool BootOnIt(Player player)
	{
		if (area?.Shape == null || (player.FootPosition - GlobalPosition).LengthSquared() > ReachSquared)
			return false;
		return player.FootprintOverlaps(area.Shape, area.GlobalTransform);
	}

	// One animation per phase: idle and the vent's fire play on their own; the tell and the cooling
	// are scrubbed by the phase's progress, so the warning is exactly as long as the rise; the
	// spikes' shot is scrubbed over ActiveShootSeconds and then holds, out, for the rest of it.
	private void ApplyFrameForPhase(float progress)
	{
		if (sprite?.SpriteFrames == null)
			return;
		switch (phase)
		{
			case Phase.Dormant:
				PlayLooped(AnimIdle);
				break;
			case Phase.Rising:
				Scrub(AnimTell, progress);
				break;
			case Phase.Active:
				if (LoopWhileActive)
					PlayLooped(AnimActive);
				else
					Scrub(AnimActive, ActiveShootSeconds <= 0f ? 1f : Mathf.Clamp(phaseTimer / ActiveShootSeconds, 0f, 1f));
				break;
			default:
				Scrub(AnimCool, progress);
				break;
		}
	}

	private void PlayLooped(StringName anim)
	{
		if (sprite!.Animation != anim || !sprite.IsPlaying())
			sprite.Play(anim);
	}

	private void Scrub(StringName anim, float t)
	{
		if (sprite!.IsPlaying())
			sprite.Stop();
		if (sprite.Animation != anim)
			sprite.Animation = anim;
		int count = Math.Max(1, sprite.SpriteFrames.GetFrameCount(anim));
		int frame = Math.Min(count - 1, (int)(t * count));
		if (sprite.Frame != frame)
			sprite.Frame = frame;
	}
}
