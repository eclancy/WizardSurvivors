using Godot;
using System;

/// <summary>
/// A static, cycling arena hazard: floor spikes that rise and retract, or a wall vent that
/// breathes fire. Damages the player while it is extended and never touches enemies.
///
/// Stages were previously flat, empty fields where the only thing to read was the swarm. A hazard
/// gives the floor itself an opinion: it punishes the panicked backpedal into a corner and rewards
/// knowing the ground. The cycle is deliberately slow and telegraphed - the sprite spends
/// <see cref="RiseSeconds"/> visibly extending before it can hurt anything - so a death is always
/// the player's read being wrong, never the hazard being unfair.
/// </summary>
public partial class StageHazard : Area2D
{
	private enum Phase { Dormant, Rising, Active, Falling }

	[Export] public int Damage { get; set; } = 3;
	/// <summary>How long it sits harmless between cycles.</summary>
	[Export] public float DormantSeconds { get; set; } = 2.6f;
	/// <summary>Telegraph window: extending, and visibly so, but not yet dangerous.</summary>
	[Export] public float RiseSeconds { get; set; } = 0.45f;
	/// <summary>How long it stays out and damaging.</summary>
	[Export] public float ActiveSeconds { get; set; } = 1.1f;
	[Export] public float FallSeconds { get; set; } = 0.35f;
	/// <summary>Seconds between repeat hits while the player stands in it.</summary>
	[Export] public float DamageInterval { get; set; } = 0.6f;
	/// <summary>Randomised at spawn so a field of hazards does not pulse in lockstep.</summary>
	[Export] public float StartPhaseOffset { get; set; } = 0f;

	/// <summary>
	/// True for art that is a flicker loop rather than an extend sequence. The spike strip runs
	/// frame 0 (flush) to frame 3 (fully out), so it can be scrubbed by progress; the flamethrower
	/// strip is a flame guttering, whose last frame is empty, so scrubbing it would show nothing
	/// at the moment it is most dangerous. Those play as a loop while active and hide otherwise.
	/// </summary>
	[Export] public bool LoopWhileActive { get; set; } = false;

	/// <summary>Radius of the floor marker drawn under the hazard.</summary>
	[Export] public float MarkerRadius { get; set; } = 20f;

	/// <summary>
	/// Colour of the arming glow and the armed rim on the floor marker.
	///
	/// This has to match what the hazard actually is. The marker was fire-coloured for every
	/// hazard, which on a spike plate stacked a glowing orange socket under a set of black spikes
	/// and read as two separate traps in the same tile - a fire trap with spikes on top. The
	/// telegraph still has to be bright enough to see (that is why it is drawn at all), so the fix
	/// is to change its colour rather than to remove it: steel for a plate somebody bolted to the
	/// floor, fire for a vent. The default stays fire so a vent needs no configuration.
	/// </summary>
	[Export] public Color MarkerAccent { get; set; } = new Color(1.0f, 0.42f, 0.16f);

	private AnimatedSprite2D? sprite;
	private Phase phase = Phase.Dormant;
	private float phaseTimer;
	private float damageTimer;
	private float extension;
	private bool playerInside;

	public override void _Ready()
	{
		AddToGroup("stage_hazards");
		sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		// Detect the player's body only. Enemies walk over hazards untouched: making them
		// two-sided would turn every hazard into a free turret and gut the risk.
		CollisionLayer = 0;
		CollisionMask = 1;
		Monitoring = true;

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;

		// Under the characters but above the floor, so a hazard never hides an enemy.
		ZIndex = -1;
		phaseTimer = -Mathf.Abs(StartPhaseOffset);
		ApplyFrameForPhase(0f);
	}

	public override void _ExitTree()
	{
		BodyEntered -= OnBodyEntered;
		BodyExited -= OnBodyExited;
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

		if (phase != Phase.Active || !playerInside)
			return;

		damageTimer += d;
		if (damageTimer < DamageInterval)
			return;

		damageTimer = 0f;
		foreach (Node2D body in GetOverlappingBodies())
		{
			if (body is Player player && IsInstanceValid(player))
				player.TakeDamage(Damage);
		}
	}

	// Drives the 4-frame art from the phase: frame 0 flush with the floor, frame 3 fully out.
	private void ApplyFrameForPhase(float progress)
	{
		extension = phase switch
		{
			Phase.Dormant => 0f,
			Phase.Rising => progress,
			Phase.Active => 1f,
			_ => 1f - progress
		};
		QueueRedraw();

		if (sprite?.SpriteFrames == null)
			return;

		if (LoopWhileActive)
		{
			sprite.Visible = phase == Phase.Active;
			if (sprite.Visible && !sprite.IsPlaying())
				sprite.Play(sprite.Animation);
			else if (!sprite.Visible)
				sprite.Stop();
			return;
		}

		int lastFrame = Math.Max(0, sprite.SpriteFrames.GetFrameCount(sprite.Animation) - 1);
		sprite.Frame = Mathf.Clamp(Mathf.RoundToInt(extension * lastFrame), 0, lastFrame);
		// Dim while harmless so "safe" and "will hurt you" are distinguishable at a glance even
		// with the sprite part-way out.
		sprite.Modulate = phase == Phase.Active
			? new Color(1f, 1f, 1f, 1f)
			: new Color(0.78f, 0.78f, 0.86f, 0.80f);
	}

	// The pack's hazard art was drawn for dark dungeon flooring: the spikes are near-black and the
	// flame is a few small orange wisps, and neither reads against a bright grass arena. A hazard
	// the player cannot see is not a challenge, it is a random tax, so the telegraph is drawn here
	// rather than left to the sprite - a socket in the floor that fills and glows as it arms. The
	// sprite still sits on top and supplies the detail.
	public override void _Draw()
	{
		float r = MarkerRadius;
		// Squashed, because the arena is viewed at a shallow top-down angle like every other
		// ground-level effect in the game.
		var squash = new Vector2(1f, 0.55f);
		DrawSetTransform(Vector2.Zero, 0f, squash);

		DrawCircle(Vector2.Zero, r, new Color(0.06f, 0.05f, 0.07f, 0.42f));

		if (extension > 0.01f)
		{
			// Core that brightens as it arms, so the player can read "about to fire" from the
			// marker alone without counting animation frames. MarkerAccent decides whether that
			// reads as heat or as metal.
			var core = new Color(MarkerAccent.R, MarkerAccent.G, MarkerAccent.B, 0.18f + 0.42f * extension);
			DrawCircle(Vector2.Zero, r * (0.35f + 0.6f * extension), core);
		}

		Color rim = extension >= 0.999f
			? new Color(MarkerAccent.R, MarkerAccent.G, MarkerAccent.B, 0.95f).Lightened(0.12f)
			: new Color(0.62f, 0.55f, 0.50f, 0.55f + 0.4f * extension);
		DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 40, rim, extension >= 0.999f ? 3f : 2f, true);
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body is Player)
			playerInside = true;
	}

	private void OnBodyExited(Node2D body)
	{
		if (body is Player)
			playerInside = false;
	}
}
