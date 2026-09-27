using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// One dive: go under, travel somewhere else, come back up. The bookkeeping half of a burrowing
/// boss, with the untargetability and the drawing left to the caller.
/// </summary>
/// <remarks>
/// Three of the eight chapter bosses burrow, which is two more than it takes for the travel maths
/// to be written three times and disagree three ways. Same shape and same reasoning as
/// <see cref="AttackTelegraph"/>: a plain object the owner ticks, not a Node, because a swarm-heavy
/// scene should not pay for a child node per behaviour.
///
/// WHY A BURROW IS WORTH HAVING AT ALL. A stationary or slow boss in a bullet heaven has one
/// failure mode: a built player parks at maximum range and melts it, and the only lever left is
/// more health, which makes the fight longer rather than harder. Going untargetable paces the
/// fight with time the player cannot spend damaging, forces them to break position and re-acquire,
/// and turns a damage check into an anticipation problem. It also gives the boss a reason to
/// reposition that reads as intent rather than as pathfinding.
///
/// It does NOT make the owner untargetable itself. That is <c>Enemy.SetTargetable</c>, and keeping
/// the two apart is deliberate: the caller decides whether a dive is a safe one, and a boss that
/// could only be hit when this class said so would be a second authority on who is in the
/// "enemies" group.
/// </remarks>
public sealed class BurrowMove
{
	public enum Beat
	{
		/// <summary>Above ground, doing nothing.</summary>
		Idle,
		/// <summary>Went under this frame: drop out of the group and hide the sprite.</summary>
		Submerged,
		/// <summary>Under and moving. The caller drives position from <see cref="CurrentPosition"/>.</summary>
		Travelling,
		/// <summary>Arrived and came up this frame: rejoin the group and show the sprite.</summary>
		Surfaced,
	}

	/// <summary>Seconds spent under, start to finish.</summary>
	public float TravelSeconds { get; set; }

	private Vector2 from;
	private Vector2 to;
	private float elapsed = -1f;

	public BurrowMove(float travelSeconds)
	{
		TravelSeconds = Mathf.Max(0.05f, travelSeconds);
	}

	public bool IsUnder => elapsed >= 0f;

	/// <summary>0 at the dive, 1 at the surface.</summary>
	public float Progress => elapsed >= 0f ? Mathf.Clamp(elapsed / Mathf.Max(0.01f, TravelSeconds), 0f, 1f) : 0f;

	/// <summary>Where the owner should be right now. Straight-line travel between the two points.</summary>
	public Vector2 CurrentPosition => from.Lerp(to, Progress);

	/// <summary>Where it will come up. Drawn as the surfacing tell before it gets there.</summary>
	public Vector2 Destination => to;

	/// <summary>Begins a dive. Ignored if one is already running.</summary>
	public void Begin(Vector2 start, Vector2 destination)
	{
		if (elapsed >= 0f)
			return;

		from = start;
		to = destination;
		elapsed = 0f;
	}

	public Beat Tick(float delta)
	{
		if (elapsed < 0f)
			return Beat.Idle;

		// Zero exactly on the frame Begin was called, so the caller gets one Submerged beat to
		// react to before any travel has happened.
		if (elapsed == 0f)
		{
			elapsed = Mathf.Max(0.0001f, delta);
			return Beat.Submerged;
		}

		elapsed += delta;
		if (elapsed < TravelSeconds)
			return Beat.Travelling;

		elapsed = -1f;
		return Beat.Surfaced;
	}

	/// <summary>Cancels a dive without a Surfaced beat, for a boss that dies mid-travel.</summary>
	public void Abort() => elapsed = -1f;
}
