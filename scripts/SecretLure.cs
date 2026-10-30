using Godot;
using System;
using WizardSurvivors.scripts;

// LURE (.ai/achievements-and-secrets.md section 5, "Conditions to meet"): only enemies that walk into a
// zone count, and the zone takes them - the Swallow-Hole's black mud. Distinct from cull, which counts
// kills near a place: this asks the player to stand at the edge and let them come, which the shared flow
// field makes work, since a horde chasing a wizard at the rim walks straight across.
//
// An enemy whose feet (its origin) come within Radius of this node is taken: out of "enemies" that frame
// (Enemy.SetTargetable, the one authority on group membership), then freed - no death, no XP, no drop,
// no kill counted. Ordinary bodies only by default: an elite, a miniboss or a boss is never swallowed
// (Takes), and a corpse never counts twice. Needed taken and it is Filled.
//
// A code-made Node2D, no scene, no Area2D: it walks EnemyRegistry.Snapshot (.ai/performance.md) once a
// frame, and only while its director is active and the player is within ActiveRange - the swarm is only
// ever round the player, so a zone he is far from has nothing to take. Progress is the owner's run state
// (State.Step: how many it has taken). It draws nothing: the owner draws the hole, and Took says where.
//
// What it does NOT do is the hazard to the player the Swallow-Hole also is (wading in slows him and costs
// HP): that is the vault's own rule, not the verb's.
public partial class SecretLure : Node2D
{
	public SecretDirector Director { get; set; }

	/// <summary>Where the progress lives: Step is how many it has taken.</summary>
	public SecretDirector.SiteState State { get; set; }

	/// <summary>An enemy whose feet come this close to the zone's centre (this node) is taken.</summary>
	public float Radius { get; set; } = 80f;

	/// <summary>How many fill it.</summary>
	public int Needed { get; set; } = 30;

	/// <summary>It looks for bodies only while the player is this near.</summary>
	public float ActiveRange { get; set; } = 1600f;

	/// <summary>Which enemies it may take. Default: ordinary bodies - never an elite, a miniboss or a boss.</summary>
	public Func<Enemy, bool> Takes { get; set; } = enemy => !enemy.IsMiniBoss;

	/// <summary>Called for each one taken, with where it went in.</summary>
	public event Action<Vector2> Took;

	/// <summary>Called once, when the count reaches Needed.</summary>
	public event Action Filled;

	public bool IsFilled => State != null && State.Step >= Needed;

	public override void _Ready()
	{
		State ??= new SecretDirector.SiteState();
	}

	public override void _Process(double delta)
	{
		if (IsFilled || Director == null || !Director.Active)
			return;
		Player player = Director.Player;
		if (player == null || player.GlobalPosition.DistanceSquaredTo(GlobalPosition) > ActiveRange * ActiveRange)
			return;

		Vector2 centre = GlobalPosition;
		float r2 = Radius * Radius;
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (enemy == null || !IsInstanceValid(enemy) || enemy.IsDying || !enemy.IsTargetable)
				continue;
			Vector2 at = enemy.GlobalPosition;
			if (at.DistanceSquaredTo(centre) > r2 || !Takes(enemy))
				continue;
			enemy.SetTargetable(false);
			enemy.QueueFree();
			State.Step++;
			Took?.Invoke(at);
			if (IsFilled)
			{
				Filled?.Invoke();
				return;
			}
		}
	}
}
