using Godot;
using System;

namespace WizardSurvivors.scripts;

// Cinderbreath: a sustained jet of fire, thrown as individual tongues.
//
// This node no longer draws or damages anything. It is an emitter: while it is lit it throws a
// FlamePuff every EmitInterval into a cone, and the puffs carry the damage, the reach and the art.
//
// WHY IT CHANGED. The first version drew the whole cone in _Draw as three shaded polygons and
// tested that cone analytically. Both halves were correct and the result still did not read as
// fire: a filled wedge has nothing in it for the eye to track, so it looked like a lit area rather
// than something being poured. Splitting it into tongues costs a sprite sheet and buys the thing
// the spell is actually for - you can see the fire leave the staff, travel, and go out.
//
// WHAT SURVIVED, because it was the good part:
//   - The duty cycle. Lit for BurnSeconds, dark for the spell's cooldown. An always-on jet is an
//     aura, and an aura is the always-on passive the boon rework removed.
//   - Re-aiming on an interval rather than per frame. Ten times a second is faster than anything
//     can walk out of a 55 degree arc, and it is one enemy-group scan instead of sixty.
//   - Living as a persistent child of the Player, so the nozzle travels with the caster.
//
// The puffs themselves are parented to the arena, not to this node - see FlamePuff.
public partial class Flamethrower : Node2D
{
	[Export] public PackedScene FlamePuffScene { get; set; }

	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;

	// Short. A flamethrower that outranged a bolt would simply be a better bolt; the trade is that
	// it covers a whole arc but only just past arm's reach, so using it means letting them close.
	[Export] public float BaseRange { get; set; } = 170f;
	[Export] public float ConeAngleDegrees { get; set; } = 55f;

	[Export] public float BurnSeconds { get; set; } = 1.5f;

	// How often a tongue leaves the nozzle. Fast enough to read as a continuous jet, slow enough
	// that a burn is about sixteen puffs rather than ninety - this is the spell's whole per-frame
	// cost, and CLAUDE.md is explicit about not allocating per frame in swarm-heavy effects.
	[Export] public float EmitInterval { get; set; } = 0.09f;

	// Each tongue is thrown at a slightly different angle and speed. Without the jitter every puff
	// follows the same line and the cone collapses into a single stuttering arrow.
	[Export] public float SpeedJitter { get; set; } = 0.18f;

	[Export] public float AimRefreshSeconds { get; set; } = 0.1f;

	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float CooldownMultiplier { get; set; } = 1.0f;
	public Player PlayerRef;

	private float range = 170f;
	private int damagePerPuff = 4;
	private float rechargeSeconds = 1.4f;

	private bool burning = false;
	private float stateSeconds = 0f;
	private float emitSeconds = 0f;
	private float aimSeconds = 0f;

	private readonly RandomNumberGenerator rng = new();

	public override void _Ready()
	{
		rng.Randomize();
		RefreshComputedStats();

		// Starts lit. A starter spell that opens the run with a second and a half of nothing reads
		// as broken rather than as recharging.
		burning = true;
		stateSeconds = 0f;
		emitSeconds = EmitInterval;
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	private void RefreshComputedStats()
	{
		if (SpellData == null)
		{
			range = BaseRange * AreaMultiplier;
			return;
		}

		damagePerPuff = Math.Max(1, Mathf.RoundToInt(SpellData.GetDamageAtLevel(CurrentLevel) * DamageMultiplier));

		float spellRange = SpellData.GetRangeAtLevel(CurrentLevel);
		range = MathF.Max(40f, (spellRange > 0f ? spellRange : BaseRange) * AreaMultiplier);

		rechargeSeconds = MathF.Max(0.2f, SpellData.GetCooldownAtLevel(CurrentLevel) * CooldownMultiplier);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		stateSeconds += dt;

		if (!burning)
		{
			if (stateSeconds >= rechargeSeconds)
			{
				burning = true;
				stateSeconds = 0f;
				// Throw on the frame it lights rather than one interval later, so the first thing
				// the player sees is fire and not a pause.
				emitSeconds = EmitInterval;
				RefreshAim();
			}
			return;
		}

		aimSeconds += dt;
		if (aimSeconds >= AimRefreshSeconds)
		{
			aimSeconds = 0f;
			RefreshAim();
		}

		emitSeconds += dt;
		if (emitSeconds >= EmitInterval)
		{
			emitSeconds = 0f;
			ThrowOnePuff();
		}

		if (stateSeconds >= BurnSeconds)
		{
			burning = false;
			stateSeconds = 0f;
		}
	}

	private void RefreshAim()
	{
		Node2D nearest = null;
		float best = float.MaxValue;

		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy || !IsInstanceValid(node))
				continue;

			float dist = GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition);
			if (dist < best)
			{
				best = dist;
				nearest = enemy;
			}
		}

		// No target: keep pointing wherever it last pointed. Snapping back to a default direction
		// makes the jet twitch every time the last enemy in range dies.
		if (nearest == null)
			return;

		Vector2 toTarget = nearest.GlobalPosition - GlobalPosition;
		if (toTarget.LengthSquared() > 0.0001f)
			Rotation = toTarget.Angle();
	}

	private void ThrowOnePuff()
	{
		if (FlamePuffScene == null || PlayerRef == null)
			return;

		Node2D arena = PlayerRef.GetParent<Node2D>();
		if (arena == null)
			return;

		float halfAngle = Mathf.DegToRad(ConeAngleDegrees * 0.5f);
		float angle = Rotation + rng.RandfRange(-halfAngle, halfAngle);

		// Speed and lifetime are derived from range together, so the cone's reach is the spell's
		// range whatever the jitter does - a faster puff simply dies sooner.
		float speedScale = 1.0f + rng.RandfRange(-SpeedJitter, SpeedJitter);
		float lifetime = MathF.Max(0.12f, BaseFlightSeconds);
		float speed = (range / lifetime) * speedScale;

		var puff = FlamePuffScene.Instantiate<Node2D>();
		if (puff is FlamePuff script)
		{
			script.SpellData = SpellData;
			script.PlayerRef = PlayerRef;
			script.Damage = damagePerPuff;
			script.Speed = speed;
			script.Lifetime = lifetime / speedScale;
			script.HitRadius *= AreaMultiplier;
		}

		arena.AddChild(puff);
		puff.GlobalPosition = GlobalPosition;
		puff.Rotation = angle;
	}

	// How long a tongue is in the air. Held constant so the animation always plays at a sane rate;
	// range is bought with speed instead.
	private const float BaseFlightSeconds = 0.42f;
}
