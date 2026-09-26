using System.Collections.Generic;

// Global namespace, matching LevelUpOption and the rest of LevelUpMenu.Models.cs.
// The namespaces in this project are inconsistent by history; match the file you sit beside.
/// <summary>
/// The level-up charges a run is carrying, handed to <c>LevelUpMenu</c> so it can draw the bar.
/// </summary>
/// <remarks>
/// Inspired by the potion system in Halls of Torment: a small pool of one-shot tools that let the
/// player argue with the level-up roll instead of only accepting it. Mechanics reference only.
///
/// **Rerolls are per level-up; the other three are per run.** That difference is deliberate and it
/// is the whole reason they feel different. A reroll costs nothing to hold - it refills every time -
/// so it is a tactical button. A ban, a saved level-up and an augury are spent from a pool that does
/// not come back, so using one is a decision about the whole run.
///
/// All four are bought in the shop, so a player who never buys any sees exactly the screen the game
/// shipped with.
/// </remarks>
public sealed class LevelUpCharges
{
	/// <summary>Refilled at every level-up from the "rerolls" shop upgrade.</summary>
	public int Rerolls { get; set; } = 0;

	/// <summary>Removes a spell from the offer pool for the rest of the run.</summary>
	public int Bans { get; set; } = 0;

	/// <summary>Defers this level-up, buying an extra pick at the next one.</summary>
	public int Banks { get; set; } = 0;

	/// <summary>Names a spell that is guaranteed to appear in the next offer.</summary>
	public int Auguries { get; set; } = 0;

	/// <summary>
	/// Everything the player could legally be offered, for the augury picker.
	/// </summary>
	/// <remarks>
	/// Built by <c>Player.GetAuguryCandidates</c> from the same candidate pass the level-up roll
	/// uses, so the list can never promise something the roll would not have produced - which is the
	/// bug the obvious implementation (listing the whole spell catalog) walks straight into.
	/// </remarks>
	public List<LevelUpOption> AuguryCandidates { get; set; } = new();

	public bool HasAny => Rerolls > 0 || Bans > 0 || Banks > 0 || Auguries > 0;
}
