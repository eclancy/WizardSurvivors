using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

/// <summary>
/// Shared "which enemies are nearest" search.
///
/// Two call sites need it and they have to agree: the magic missile firing loop spreading a
/// volley across distinct targets, and Twin Volley picking the two enemies a missile forks
/// onto. Written as one helper rather than two loops so a future change to how targets are
/// chosen cannot land in one place and not the other.
/// </summary>
public static class EnemyTargeting
{
	/// <summary>Upper bound on a single pick, so a bad caller cannot ask for an unbounded scan buffer.</summary>
	public const int MaxPick = 16;

	/// <summary>
	/// Fills <paramref name="results"/> with up to <paramref name="count"/> members of the
	/// "enemies" group nearest to <paramref name="origin"/>, closest first.
	///
	/// The caller owns the list so a call site that runs on a cooldown tick in a swarm can
	/// reuse one allocation instead of producing garbage every cast.
	/// </summary>
	/// <param name="maxDistance">Enemies beyond this are ignored entirely.</param>
	/// <param name="exclude">A node that must never be picked - typically the enemy a projectile just hit.</param>
	public static void CollectNearest(
		SceneTree tree,
		Vector2 origin,
		int count,
		List<Node2D> results,
		float maxDistance = float.MaxValue,
		Node exclude = null)
	{
		results.Clear();
		if (tree == null || count <= 0)
			return;

		count = Math.Min(count, MaxPick);

		// Distances are kept alongside the results rather than recomputed during insertion:
		// the candidate list is the whole swarm, and the pick is small.
		float[] distances = new float[count];
		int found = 0;

		foreach (var candidate in tree.GetNodesInGroup("enemies"))
		{
			if (candidate is not Node2D enemy || enemy == exclude)
				continue;

			float dist = origin.DistanceTo(enemy.GlobalPosition);
			if (dist > maxDistance)
				continue;

			// Full, and no closer than the furthest we already hold.
			if (found == count && dist >= distances[found - 1])
				continue;

			int slot = found;
			while (slot > 0 && distances[slot - 1] > dist)
				slot--;

			if (slot >= count)
				continue;

			if (found < count)
				found++;
			else
				results.RemoveAt(count - 1);

			for (int i = found - 1; i > slot; i--)
				distances[i] = distances[i - 1];

			distances[slot] = dist;
			results.Insert(slot, enemy);
		}
	}
}
