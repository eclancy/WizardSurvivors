using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// The shapes a wave can arrive in.
/// </summary>
/// <remarks>
/// The spawner places every enemy independently: a bearing and a distance, rolled per enemy. That
/// produces a crowd but never a *formation*, and a crowd of singles is the one thing the genre this
/// game is modelled on does not do. Waves there arrive as authored shapes - a wall you have to cut
/// through, a stream crossing the arena, a ring closing in - and the shape is most of what makes a
/// wave read as an event rather than as more of the same.
///
/// Kept as pure geometry with no engine state so the arithmetic can be checked without a running
/// game, which is the only way anything in this project gets verified cheaply.
/// </remarks>
public enum SpawnFormation
{
	/// <summary>A line across the player's path. The wall you run into.</summary>
	Arc,

	/// <summary>Single file along one bearing, arriving in sequence rather than together.</summary>
	Column,

	/// <summary>Two groups on the flanks, squeezing in from both sides at once.</summary>
	Pincer,

	/// <summary>Evenly all the way around. The punctuation mark: you are surrounded, now.</summary>
	Ring,
}

public static class SpawnFormations
{
	/// <summary>How wide an Arc spreads, in radians. Just under a third of a circle.</summary>
	private const float ArcSpread = 1.9f;

	/// <summary>How wide each half of a Pincer spreads.</summary>
	private const float PincerSpread = 0.7f;

	/// <summary>How far apart Column members sit, as a fraction of the base spawn radius.</summary>
	private const float ColumnStep = 0.16f;

	/// <summary>
	/// Which shape a wave takes, given how far into the run it is.
	/// </summary>
	/// <param name="minutesElapsed">Run time, in minutes.</param>
	/// <param name="roll">A uniform roll in [0, 1).</param>
	/// <remarks>
	/// The order things unlock in is the difficulty curve. An Arc is a shape you can walk around,
	/// so it comes first. A Pincer takes away one of the two directions you would walk. A Ring
	/// takes away all of them, so it arrives last and stays rare - a shape that means "you are
	/// surrounded" stops meaning anything if it is every third wave.
	/// </remarks>
	public static SpawnFormation Pick(float minutesElapsed, float roll)
	{
		if (minutesElapsed < 3.0f)
			return roll < 0.72f ? SpawnFormation.Arc : SpawnFormation.Column;

		if (minutesElapsed < 7.0f)
		{
			if (roll < 0.46f) return SpawnFormation.Arc;
			if (roll < 0.74f) return SpawnFormation.Column;
			return SpawnFormation.Pincer;
		}

		if (roll < 0.34f) return SpawnFormation.Arc;
		if (roll < 0.54f) return SpawnFormation.Column;
		if (roll < 0.86f) return SpawnFormation.Pincer;
		return SpawnFormation.Ring;
	}

	/// <summary>
	/// Where member <paramref name="index"/> of <paramref name="count"/> stands in the formation.
	/// </summary>
	/// <param name="baseBearing">
	/// The bearing the wave is arriving on - already biased toward the player's heading by the
	/// caller, so a formation lands in front of a moving player rather than wherever.
	/// </param>
	/// <returns>
	/// A bearing, and a multiplier on whatever spawn radius the caller computes for that bearing.
	/// The radius is a multiplier rather than an absolute so the caller keeps owning the
	/// screen-relative distance, which differs per bearing on a non-square viewport.
	/// </returns>
	public static (float Bearing, float RadiusScale) Placement(
		SpawnFormation formation, int index, int count, float baseBearing)
	{
		int safeCount = Mathf.Max(1, count);
		// Position across the formation in [-0.5, 0.5]. A single member sits dead centre.
		float across = safeCount <= 1 ? 0f : (index / (float)(safeCount - 1)) - 0.5f;

		switch (formation)
		{
			case SpawnFormation.Column:
				// One bearing, stepped outward. The far members are a beat behind the near ones,
				// which is the whole point - a stream arrives over several seconds.
				return (baseBearing, 1f + (index * ColumnStep));

			case SpawnFormation.Pincer:
			{
				// Alternating flanks rather than first-half/second-half, so an odd count does not
				// quietly load one side heavier than the other.
				float side = (index % 2 == 0) ? 1f : -1f;
				int rank = index / 2;
				int perSide = Mathf.Max(1, (safeCount + 1) / 2);
				float alongFlank = perSide <= 1 ? 0f : (rank / (float)(perSide - 1)) - 0.5f;
				return (baseBearing + (side * Mathf.Pi * 0.5f) + (alongFlank * PincerSpread), 1f);
			}

			case SpawnFormation.Ring:
				// Evenly around the full circle, offset off the base bearing so the ring is not
				// always aligned the same way relative to travel.
				return (baseBearing + (Mathf.Pi * 2f * index / safeCount), 1f);

			case SpawnFormation.Arc:
			default:
				// A line across the path. Members further from the centre are pushed very slightly
				// further out, so the wall is shallowly concave and closes on the player together
				// rather than at the ends first.
				return (baseBearing + (across * ArcSpread), 1f + (Mathf.Abs(across) * 0.12f));
		}
	}
}
