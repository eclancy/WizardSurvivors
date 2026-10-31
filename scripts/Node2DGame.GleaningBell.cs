using Godot;
using WizardSurvivors.scripts;

// WHO DROPS THE GLEANING BELL, AND WHERE IT LANDS (GleaningBell.cs; .ai/passives-and-items.md 8i).
//
// It rides on the chest carriers - an elite, a Warden, the Sketchbook's ghost - because those are the
// bodies the player kills on purpose (DropTable's rule for chests), and it is rolled on its own, whether
// or not the carrier's chest pays. Not on IsMiniBoss: the Sketchbook's ordinary table sends Rockem
// wearing it, and Elim's ghost used to come one kill in forty there, which would have made the bell a
// stream in that chapter alone.
//
// The chance rises with every carrier that paid none and resets when one does (DropTable gives the
// numbers and why), and two rules keep it a find rather than a habit: never two lying on the field at
// once, and never two within GleaningBellMinSecondsApart run seconds - the second would call in a field
// the first just emptied. A carrier killed while either rule holds is not counted toward the next one.
// Nothing the player owns changes any of it.
public partial class Node2DGame
{
	private readonly RandomNumberGenerator gleaningRng = NewGleaningRng();
	private int carriersSinceGleaningBell;
	private float lastGleaningBellAt = float.NegativeInfinity;

	/// <summary>Bells dropped this run, for the balance harness.</summary>
	public int GleaningBellsDropped { get; private set; }

	// Its own stream, so a bell's roll never shifts which chest or which spawn the run's other rolls draw.
	private static RandomNumberGenerator NewGleaningRng()
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		return rng;
	}

	/// <summary>The chance the next carrier drops a bell, after <paramref name="carriersWithout"/> that paid none.</summary>
	public static float GleaningBellChance(int carriersWithout) =>
		Mathf.Clamp(DropTable.GleaningBellBaseChance + DropTable.GleaningBellChanceStep * Mathf.Max(0, carriersWithout), 0f, 1f);

	// Called from DropCarriedChest, after its boss-fight and run-over guards.
	private void TryDropGleaningBell(Vector2 at)
	{
		if (GleaningBell.Live.Count > 0 || timeElapsed - lastGleaningBellAt < DropTable.GleaningBellMinSecondsApart)
			return;
		if (gleaningRng.Randf() >= GleaningBellChance(carriersSinceGleaningBell))
		{
			carriersSinceGleaningBell++;
			return;
		}
		carriersSinceGleaningBell = 0;
		lastGleaningBellAt = timeElapsed;
		GleaningBellsDropped++;
		PlaceGleaningBell(at);
	}

	/// <summary>
	/// A bell thrown out of a body at <paramref name="at"/>: it lands a short hop away, off the line a
	/// chest from the same body is set out along, never closer to the player than
	/// <see cref="DropTable.GleaningBellMinPlayerDistance"/>, and on ground he can stand on.
	/// </summary>
	public void PlaceGleaningBell(Vector2 at)
	{
		if (player == null || !IsInstanceValid(player))
			return;
		Vector2 away = at - player.GlobalPosition;
		Vector2 direction = away.LengthSquared() > 1f ? away.Normalized() : Vector2.Up;
		// PlaceChestNear sets a chest out along the line from him to the body, so the bell leaves it
		// sixty degrees off that line, to one side or the other.
		direction = direction.Rotated((gleaningRng.Randf() < 0.5f ? -1f : 1f) * Mathf.Pi / 3f);
		Vector2 land = at + direction * DropTable.GleaningBellTossDistance;
		Vector2 fromPlayer = land - player.GlobalPosition;
		if (fromPlayer.Length() < DropTable.GleaningBellMinPlayerDistance)
			land = player.GlobalPosition + (fromPlayer.LengthSquared() > 1f ? fromPlayer.Normalized() : direction)
				* DropTable.GleaningBellMinPlayerDistance;
		// Deferred, as the chest is: a death can land inside a physics callback, where adding an Area2D
		// is refused.
		var bell = new GleaningBell
		{
			Position = ReachablePoint(land),
			TossFrom = at,
			TossSeconds = DropTable.GleaningBellTossSeconds,
		};
		CallDeferred(Node.MethodName.AddChild, bell);
	}
}
