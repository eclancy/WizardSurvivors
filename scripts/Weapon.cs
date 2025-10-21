using System;
using System.Collections;
using System.Linq;
using Godot;

namespace WizardSurvivors.scripts;

public enum WeaponId
{
    MagicMissile,
    ArcaneExplosion,
    SpiritualWeapon,
    Fireball,
    IceShard
}

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

    // Added for projectile and explosion logic
    public float Area { get; set; } = 16.0f; // For MagicMissile collision radius
    public float Speed { get; set; } = 400f; // For MagicMissile movement speed
    public float Duration { get; set; } = 5.0f; // For projectile lifetime
    public float KnockbackSpeed { get; set; } = 2.0f; // For ArcaneExplosion knockback speed
    public float Cooldown { get; set; } = 1.5f; // For ArcaneExplosion cooldown

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
                NumberOfProjectiles = 1,
                Area = 16.0f,
                Speed = 400f,
                Duration = 5.0f,
                KnockbackSpeed = 0f,
                Cooldown = 0.5f
            },
            new Weapon {
                Id = WeaponId.ArcaneExplosion,
                Name = "Arcane Explosion",
                Damage = 5,
                AttackSpeed = 0.5f, // Only 1 attack every 2 seconds
                Range = 100f,
                ProjectileScenePath = "res://scenes/ArcaneExplosion.tscn",
                KnockbackRange = 100f,
                Pierce = 500, // High pierce to hit all enemies in range
                Area = 100f,
                Speed = 0f,
                Duration = 1.0f,
                KnockbackSpeed = 2.0f,
                Cooldown = 1.5f
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

    public Weapon GetWeaponLevelUp(Weapon weapon)
    {
        var allWeapons = GetArcaneWeapons();
        var baseWeapon = allWeapons.ToList().FirstOrDefault(w => w.Id == weapon.Id);
        if (baseWeapon == null)
        {
            GD.PrintErr("Error: Weapon ID not found.");
            return null;
        }

        return GetWeaponUpgrade(weapon);
    }


    private static Weapon GetWeaponUpgrade(Weapon weapon)
    {
        // Level up
        weapon.Level += 1;
        switch (weapon.Id)
        {
            case WeaponId.MagicMissile:
                return new Weapon
                {
                    Id = WeaponId.MagicMissile,
                    Damage = 10 + (weapon.Level - 1) * 5,
                    Pierce = (int)Math.Truncate(weapon.Level / 3.0f),
                    NumberOfProjectiles = (weapon.Level >= 5) ? 2 : 1,
                    Level = weapon.Level,

                    // Retain other properties - better ways to do this I'm sure but this works
                    Name = weapon.Name,
                    AttackSpeed = weapon.AttackSpeed,
                    Range = weapon.Range,
                    ProjectileScenePath = weapon.ProjectileScenePath,
                    KnockbackRange = weapon.KnockbackRange,
                };
            case WeaponId.ArcaneExplosion:
                return new Weapon
                {
                    Id = WeaponId.ArcaneExplosion,
                    Damage = 8 + (weapon.Level - 1) * 4,
                    Level = weapon.Level,

                    // Retain other properties - better ways to do this I'm sure but this works
                    Name = weapon.Name,
                    AttackSpeed = weapon.AttackSpeed,
                    Range = weapon.Range,
                    ProjectileScenePath = weapon.ProjectileScenePath,
                    KnockbackRange = weapon.KnockbackRange,
                    Pierce = weapon.Pierce,
                    NumberOfProjectiles = weapon.NumberOfProjectiles
                };
            case WeaponId.SpiritualWeapon:
                return new Weapon
                {
                    Id = WeaponId.SpiritualWeapon,
                    Damage = 12 + (weapon.Level - 1) * 6,
                    AttackSpeed = 2.5f,
                    Range = 0f,
                    Pierce = 1200,
                    NumberOfProjectiles = 3,
                    Level = weapon.Level,

                    // Retain other properties - better ways to do this I'm sure but this works
                    Name = weapon.Name,
                    ProjectileScenePath = weapon.ProjectileScenePath,
                    KnockbackRange = weapon.KnockbackRange
                };
            default:
                GD.PrintErr("Error: Weapon ID not found for upgrade.");
                return null;
        }
    }
}

