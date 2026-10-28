namespace WizardSurvivors.scripts;

public enum SpellEffect
{
    None = 0,
    Pierce = 1,
    Chain = 2,
    Freeze = 3,
    Burn = 4,
    Knockback = 5,
    CritChance = 6,
    AreaSize = 7,
    ProjectileSpeed = 8,
    SlowPower = 9,
    SlowDuration = 10,
    RootDuration = 11,
    DotDamage = 12,
    ZoneDuration = 13,
    ExplosionOnHit = 14,
    // 15 was SpawnMinions. Nothing ever read it, and the summon it was meant for became the Familiar,
    // its own node (.ai/spell-roster.md). Retired 2026-10-28 (issue #69). The number stays unused
    // rather than renumbered: a .tres stores the number, not the name.
    VortexPull = 16,
    Lifesteal = 17,
    // Projectile forks on its first hit and seeks other enemies. EffectValue is the
    // percentage of the parent's damage each shard carries.
    SplitOnHit = 18,

    // Adds to the crit MULTIPLIER for this spell only, where CritChance adds to the odds.
    // Read in Player.DealDamageToEnemy off the SpellData that dealt the hit, which is why it works
    // without threading a new argument through twenty call sites.
    CritDamage = 19,

    // How much extra damage a Vulnerable debuff applied by this spell makes the target take.
    // Separate from CritDamage on purpose: one raises the caster's crits, the other weakens the
    // target for everything the player owns.
    Vulnerability = 20,

    // WHAT A SPELL GRANTS THE WIZARD while it is held, rather than what it does to an enemy. Only the
    // Familiar taken as a cat reads them (Familiar.GrantsOf, Player.FireFamiliar): the cat's level lines
    // say how much faster every spell casts and how many more hits the wizard slips, and Player folds
    // them into the attack speed every cooldown divides by and the dodge roll, live, never into a stored
    // field - so a grant cannot be applied twice, and leaves the moment the cat does.
    // Added to the wizard's attack speed (0.02 = every spell's cooldown runs 2% faster).
    CastSpeed = 21,
    // Added to the wizard's dodge chance, under the dodge cap.
    Evasion = 22,
}
