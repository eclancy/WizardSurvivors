namespace WizardSurvivors.scripts;

// How a spell selects where to act.
public enum SpellTargetingMode
{
	Auto = 0,
	Self = 1,
	NearestEnemy = 2,
	GroundAtEnemy = 3,
	MultiTarget = 4,
	DirectionalCone = 5,
	// The way the player is facing (Player.FacingDirection / FacingSide): the facing family, which
	// never aims at an enemy (.ai/spell-variety.md, phase 1).
	Facing = 6
}

// The primary way a spell applies damage.
public enum SpellDamageShape
{
	Auto = 0,
	ProjectileHit = 1,
	BeamHit = 2,
	RadiusBurst = 3,
	PersistentZone = 4,
	ContactOrbit = 5,
	ChainJump = 6,
	// A creature that goes and fights on its own (the Familiar): not a projectile, not an orbit.
	Companion = 7
}

[System.Flags]
public enum SpellScalingTag
{
	None = 0,
	Damage = 1 << 0,
	Cooldown = 1 << 1,
	Area = 1 << 2,
	Range = 1 << 3,
	ProjectileCount = 1 << 4,
	ProjectileSpeed = 1 << 5,
	Pierce = 1 << 6,
	Chain = 1 << 7,
	Crit = 1 << 8,
	Slow = 1 << 9,
	Root = 1 << 10,
	Knockback = 1 << 11,
	Dot = 1 << 12,
	Duration = 1 << 13
}

/// <summary>
/// The classification in words a player reads. The pause screen used to print the enum names
/// themselves ("Targeting NearestEnemy | Shape ProjectileHit | Scaling Damage, ProjectileSpeed"),
/// which the player reported as hard to read. Every value of all three enums has a phrase here;
/// RegressionChecks.ValidateSpellClassificationText fails if one is added without one.
/// </summary>
public static class SpellClassificationText
{
	/// <summary>Completes "Aims ...".</summary>
	public static string Aim(SpellTargetingMode mode) => mode switch
	{
		SpellTargetingMode.Auto => "wherever the spell itself decides",
		SpellTargetingMode.Self => "around you",
		SpellTargetingMode.NearestEnemy => "at the nearest enemy",
		SpellTargetingMode.GroundAtEnemy => "at the ground under an enemy",
		SpellTargetingMode.MultiTarget => "at several enemies at once",
		SpellTargetingMode.DirectionalCone => "in a cone in front of you",
		SpellTargetingMode.Facing => "the way you are facing, never at an enemy",
		_ => null,
	};

	/// <summary>Completes "Hits with ...".</summary>
	public static string Shape(SpellDamageShape shape) => shape switch
	{
		SpellDamageShape.Auto => "whatever the spell makes",
		SpellDamageShape.ProjectileHit => "a projectile that strikes what it meets",
		SpellDamageShape.BeamHit => "a beam",
		SpellDamageShape.RadiusBurst => "a burst that hits everything in an area",
		SpellDamageShape.PersistentZone => "a zone that keeps hurting what stands in it",
		SpellDamageShape.ContactOrbit => "something circling you that hits on contact",
		SpellDamageShape.ChainJump => "a hit that jumps from enemy to enemy",
		SpellDamageShape.Companion => "a creature of yours that runs down what comes near and bites it",
		_ => null,
	};

	/// <summary>One scaling flag as a noun: what an upgrade to it improves.</summary>
	public static string Scaling(SpellScalingTag tag) => tag switch
	{
		SpellScalingTag.None => "nothing",
		SpellScalingTag.Damage => "damage",
		SpellScalingTag.Cooldown => "cooldown",
		SpellScalingTag.Area => "area",
		SpellScalingTag.Range => "range",
		SpellScalingTag.ProjectileCount => "number of projectiles",
		SpellScalingTag.ProjectileSpeed => "projectile speed",
		SpellScalingTag.Pierce => "pierce",
		SpellScalingTag.Chain => "chain jumps",
		SpellScalingTag.Crit => "critical hits",
		SpellScalingTag.Slow => "slow",
		SpellScalingTag.Root => "root",
		SpellScalingTag.Knockback => "knockback",
		SpellScalingTag.Dot => "damage over time",
		SpellScalingTag.Duration => "duration",
		_ => null,
	};

	/// <summary>Every flag set in <paramref name="tags"/>, as an English list: "damage, area and duration".</summary>
	public static string ScalingList(SpellScalingTag tags)
	{
		var words = new System.Collections.Generic.List<string>();
		foreach (SpellScalingTag tag in System.Enum.GetValues<SpellScalingTag>())
		{
			if (tag != SpellScalingTag.None && (tags & tag) != 0)
				words.Add(Scaling(tag) ?? tag.ToString());
		}
		if (words.Count == 0)
			return Scaling(SpellScalingTag.None);
		if (words.Count == 1)
			return words[0];
		return string.Join(", ", words.GetRange(0, words.Count - 1)) + " and " + words[words.Count - 1];
	}
}
