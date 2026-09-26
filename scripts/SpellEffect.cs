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
    SpawnMinions = 15,
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
    Vulnerability = 20
}
