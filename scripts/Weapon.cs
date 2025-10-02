using Godot;

namespace WizardSurvivors.scripts;

public class Weapon
{
    public WeaponId Id { get; set; }
    public string Name { get; set; }
    public int Damage { get; set; }
    public float AttackSpeed { get; set; } // Attacks per second
    public float Range { get; set; } // In pixels
    public string ProjectileScenePath { get; set; } // Path to the projectile scene
    public float KnockbackRange { get; set; } = 0f; // Default to 0 if not specified
    public int Pierce { get; set; } = 0; // Pierce damage will be 100% for all targets
    public int NumberOfProjectiles { get; set; } = 1; // Default to 1 if not specified
    public int Level { get; set; } = 1; // Default to level 1

    public Weapon[] GetArcaneWeapons()
    {

        return
        [
            new Weapon {
                Id = WeaponId.MagicMissile,
                Name = "Magic Missile",
                Damage = 10,
                AttackSpeed = 1.0f,
                Range = 500f,
                ProjectileScenePath = "res://scenes/MagicMissile.tscn",
                KnockbackRange = 0f,
                Pierce = 0,
                NumberOfProjectiles = 1
            },
            new Weapon {
                Id = WeaponId.ArcaneExplosion,
                Name = "Arcane Explosion",
                Damage = 15,
                AttackSpeed = 0.2f, // Only 1 attack every 5 seconds
                Range = 300f,
                ProjectileScenePath = "res://scenes/ArcaneExplosion.tscn",
                KnockbackRange = 50f,
                Pierce = 2
            },
            new Weapon {
                Id = WeaponId.SpiritualWeapon,
                Name = "Spiritual Weapon",
                Damage = 8,
                AttackSpeed = 2.0f, // 2 attacks per second
                Range = 0f,
                ProjectileScenePath = "res://scenes/SpiritualWeapon.tscn",
                KnockbackRange = 0f,
                Pierce = 1000, // Effectively infinite pierce
                NumberOfProjectiles = 2 // Shoots 2 projectiles in a spread
            }
        ];
    }
}

public enum WeaponId
{
    MagicMissile,
    ArcaneExplosion,
    SpiritualWeapon,
    Fireball,
    IceShard
}