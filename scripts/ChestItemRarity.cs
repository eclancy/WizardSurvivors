// Global namespace, matching ChestItemCatalog which it belongs to. Namespaces are
// inconsistent across this project by long-standing habit; match the neighbour.
/// <summary>
/// How scarce a chest item is, and therefore how strong it is and whether it carries an element tag.
/// </summary>
/// <remarks>
/// The three move together on purpose. Rarity that only changed how often something appeared would
/// be a drop-rate table wearing a name; rarity that only changed magnitude would make the chest a
/// slot machine with no texture. Tying the element tag to it is what makes a Rare feel like a find
/// rather than a bigger number - tags are the tightest budget in the game.
/// </remarks>
public enum ChestItemRarity
{
	Common,
	Uncommon,
	Rare,

	/// <summary>Build-defining, and the only items that carry two element tags.</summary>
	Relic,
}
