using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// What a hit does because of an element a milestone brought to the spell. One per element that has
/// a status of its own (.ai/gameplay-design.md, "Status effects"); Fire, Arcane, Wind, Earth and
/// Grass carry no status, so a branch into one of them carries no rider.
/// </summary>
public enum ElementRider
{
	None = 0,
	// Enemy.ApplyPoison(magnitude as damage a second, seconds).
	Poison = 1,
	// Enemy.ApplySlow(magnitude as the speed multiplier, seconds) - Ice's chill and Water's drag.
	Slow = 2,
	// Enemy.ApplyVulnerable(magnitude as extra damage taken, seconds).
	Vulnerable = 3,
	// Enemy.ApplyWeaken(magnitude as the share of its damage it loses, seconds).
	Weaken = 4,
	// Enemy.ApplyWither(magnitude as the share of max HP burned). Permanent; seconds unused.
	Wither = 5,
	// Enemy.ApplyShock(seconds): a stagger that stops it where it stands.
	Shock = 6,
}

/// <summary>
/// A spell node that repaints an element path in that element's own ramp (RiptideWave, Hollow Star,
/// Bramble Seed...). Player.ApplyLegendaryVisual leaves the evolution's multiply tint off such a
/// node when the spell has branched, because a tint laid over a repaint only darkens it.
/// </summary>
public interface IPaintsElementPath
{
}

/// <summary>
/// Element paths (.ai/spell-roster.md, "Element paths"): a pure spell's level 4 can deepen its own
/// element or branch into a second one, and a branch is a different spell from then on - it looks
/// like the element it took (ElementColors.GetEvolutionTint, or the spell's own repaint) and its
/// hits do what that element does. This is the one place an element is matched to its status, so
/// the catalog, the validator and the card text cannot disagree about it.
/// </summary>
public static class ElementPaths
{
	/// <summary>The status an element carries onto a hit, or None for the five that have none.</summary>
	public static ElementRider RiderFor(Element element) => element switch
	{
		Element.Poison => ElementRider.Poison,
		Element.Ice => ElementRider.Slow,
		Element.Water => ElementRider.Slow,
		Element.Darkness => ElementRider.Vulnerable,
		Element.Light => ElementRider.Weaken,
		Element.Metal => ElementRider.Wither,
		Element.Lightning => ElementRider.Shock,
		_ => ElementRider.None,
	};

	/// <summary>
	/// Applies one rider to one enemy. Every status underneath is stacking-resistant (strongest
	/// magnitude, longest remaining duration; Wither only ever takes the difference), so a zone that
	/// re-applies it every tick holds it rather than piling it up.
	/// </summary>
	public static void Apply(Enemy enemy, ElementRider rider, float magnitude, float seconds)
	{
		if (enemy == null || !GodotObject.IsInstanceValid(enemy) || enemy.IsDying)
			return;

		switch (rider)
		{
			case ElementRider.Poison:
				enemy.ApplyPoison(Mathf.Max(1, Mathf.RoundToInt(magnitude)), seconds);
				break;
			case ElementRider.Slow:
				enemy.ApplySlow(magnitude, seconds);
				break;
			case ElementRider.Vulnerable:
				enemy.ApplyVulnerable(magnitude, seconds);
				break;
			case ElementRider.Weaken:
				enemy.ApplyWeaken(magnitude, seconds);
				break;
			case ElementRider.Wither:
				enemy.ApplyWither(magnitude);
				break;
			case ElementRider.Shock:
				// The amplitude and frequency Chain Lightning's own impact uses, so a shocked enemy
				// shakes the same way whatever shocked it.
				enemy.ApplyShock(seconds, 5.0f, 54.0f);
				break;
		}
	}

	/// <summary>
	/// The rider in words, for a card: built from the numbers it applies, so the text cannot drift
	/// from what the hit does.
	/// </summary>
	public static string Describe(ElementRider rider, float magnitude, float seconds) => rider switch
	{
		ElementRider.Poison => $"hits poison for {Mathf.RoundToInt(magnitude)} dmg/s over {seconds:0.#} s",
		ElementRider.Slow => $"hits slow by {Mathf.RoundToInt((1f - magnitude) * 100f)}% for {seconds:0.#} s",
		ElementRider.Vulnerable => $"hits leave enemies Vulnerable (+{Mathf.RoundToInt(magnitude * 100f)}% damage taken) for {seconds:0.#} s",
		ElementRider.Weaken => $"hits Weaken enemies (-{Mathf.RoundToInt(magnitude * 100f)}% damage dealt) for {seconds:0.#} s",
		ElementRider.Wither => $"the first hit Withers {Mathf.RoundToInt(magnitude * 100f)}% of an enemy's max HP",
		ElementRider.Shock => $"hits stagger for {seconds:0.0#} s",
		_ => string.Empty,
	};

	/// <summary>True when the rider's numbers are something its status can apply.</summary>
	public static bool IsValid(ElementRider rider, float magnitude, float seconds) => rider switch
	{
		ElementRider.None => true,
		ElementRider.Poison => magnitude >= 1f && seconds > 0f,
		ElementRider.Slow => magnitude > 0f && magnitude < 1f && seconds > 0f,
		ElementRider.Vulnerable or ElementRider.Weaken => magnitude > 0f && magnitude <= 0.6f && seconds > 0f,
		ElementRider.Wither => magnitude > 0f && magnitude <= 0.25f,
		ElementRider.Shock => seconds > 0f && seconds <= 0.5f,
		_ => false,
	};
}
