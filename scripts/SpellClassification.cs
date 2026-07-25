namespace WizardSurvivors.scripts;

// How a spell selects where to act.
public enum SpellTargetingMode
{
	Auto = 0,
	Self = 1,
	NearestEnemy = 2,
	GroundAtEnemy = 3,
	MultiTarget = 4,
	DirectionalCone = 5
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
	ChainJump = 6
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
