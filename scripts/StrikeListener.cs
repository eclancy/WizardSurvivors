using Godot;
using System;
using WizardSurvivors.scripts;

// STRIKE (.ai/stage-secrets.md 7.3; .ai/achievements-and-secrets.md section 5): damage the player lands on
// enemies near a point. Extracted from the Frozen Waste's thaw (ThawEvent), which was the first thing in
// the game to be broken by the fighting round it rather than by being hit - and that is the rule this
// carries for everything breakable after it: the thing is NEVER a target and never joins "enemies", so no
// spell is ever aimed at it and the loadout stays on the swarm. The swarm is always at the player's feet,
// so standing beside the thing and fighting IS breaking it.
//
// A plain object, not a node: whoever owns it starts it when the player arrives and stops it when it is
// done or stood down, so a site nobody has found costs nothing per hit. Listens on Player.EnemyDamagedAt,
// the one event every landed hit raises, with where the enemy stood and what spell did it.
public sealed class StrikeListener
{
	/// <summary>The point the damage is measured from, in world pixels.</summary>
	public Vector2 Centre { get; set; }

	/// <summary>A hit on an enemy standing within this of <see cref="Centre"/> counts.</summary>
	public float Radius { get; set; }

	/// <summary>
	/// What a hit from this spell is worth per point of damage, or null for 1. The thaw's fire counts
	/// three times over; a breakable that cares about nothing but damage leaves it null.
	/// </summary>
	public Func<SpellData, float> Weight { get; set; }

	/// <summary>Called with each counted hit's weighted amount and where the enemy stood.</summary>
	public Action<float, Vector2> Struck { get; set; }

	/// <summary>The weighted damage counted since this listener was made.</summary>
	public float Landed { get; private set; }

	public bool IsListening { get; private set; }

	public StrikeListener(Vector2 centre, float radius)
	{
		Centre = centre;
		Radius = radius;
	}

	public void Start()
	{
		if (IsListening)
			return;
		Player.EnemyDamagedAt += OnEnemyDamagedAt;
		IsListening = true;
	}

	public void Stop()
	{
		if (!IsListening)
			return;
		Player.EnemyDamagedAt -= OnEnemyDamagedAt;
		IsListening = false;
	}

	private void OnEnemyDamagedAt(Vector2 where, int amount, SpellData source)
	{
		if (where.DistanceTo(Centre) > Radius)
			return;
		float weighted = amount * (Weight?.Invoke(source) ?? 1f);
		Landed += weighted;
		Struck?.Invoke(weighted, where);
	}

	/// <summary>The thaw's rule, for anything that wants fire to count three times: true when the spell carries Fire.</summary>
	public static bool IsFire(SpellData source) =>
		source?.ElementWeights != null && source.ElementWeights.ContainsKey("Fire");
}
