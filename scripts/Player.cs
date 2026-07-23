using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

public partial class Player : CharacterBody2D
{
	[Signal] public delegate void XpGainedEventHandler(int amount);
	[Signal] public delegate void LevelGainedEventHandler();
	[Signal] public delegate void DiedEventHandler();
	// Emitted whenever the player takes damage, before HP is reduced (issue #22). Reactive passive
	// spells (e.g. Frozen Bulwark, Stormguard Aura) subscribe to this via PassiveSpellEffect.
	[Signal] public delegate void DamageTakenEventHandler(int amount);
	[Export] public float Speed { get; set; } = 220f;
	[Export] public PackedScene MagicMissileScene { get; set; }
	[Export] public PackedScene ArcaneExplosionScene { get; set; }
	[Export] public PackedScene SpiritualWeaponScene { get; set; }
	[Export] public PackedScene FireballScene { get; set; }
	[Export] public SpellData MagicMissileData { get; set; }
	[Export] public SpellData ArcaneExplosionData { get; set; }
	[Export] public SpellData SpiritualWeaponData { get; set; }
	[Export] public SpellData FireballData { get; set; }
	// Issue #13 roster expansion - 12 more offensive spells built on the shared ElementalBolt/
	// ElementalPulse/GroundSpike/OrbitingBlade scripts (see Player.EnsureCatalogDefaults and the
	// Fire*/FireOrRefresh* helpers below) instead of one bespoke script per spell.
	[Export] public PackedScene FrostShardScene { get; set; }
	[Export] public PackedScene ShadowBoltScene { get; set; }
	[Export] public PackedScene ThornVineScene { get; set; }
	[Export] public PackedScene GaleBladeScene { get; set; }
	[Export] public PackedScene MoltenShardScene { get; set; }
	[Export] public PackedScene ChainLightningScene { get; set; }
	[Export] public PackedScene VoidLanceScene { get; set; }
	[Export] public PackedScene GlacialSpikeScene { get; set; }
	[Export] public PackedScene SolarFlareScene { get; set; }
	[Export] public PackedScene ToxicSporeBurstScene { get; set; }
	[Export] public PackedScene ObsidianSpikeScene { get; set; }
	[Export] public PackedScene CycloneSlashScene { get; set; }
	// Issue #28 - D&D-inspired spells. ScorchingRayScene uses a beam animation script;
	// MeteorImpactScene uses a ground-targeted impact script.
	[Export] public PackedScene BlackTentaclesScene { get; set; }
	[Export] public PackedScene ConeOfColdScene { get; set; }
	[Export] public PackedScene ScorchingRayScene { get; set; }
	[Export] public PackedScene MeteorImpactScene { get; set; }
	[Export] public float FireInterval { get; set; } = 1.0f;
	[Export] public int StartingXP { get; set; } = 0;
	[Export] public int StartingLevel { get; set; } = 1;
	[Export] public int CurrentXP { get; set; } = 0;
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public int XPToNextLevel { get; set; } = 10;
	[Export] public float UpgradeOfferWeight { get; set; } = 3.0f;
	[Export] public float NewUnlockOfferWeight { get; set; } = 1.0f;

	[Export] public int MaxHP { get; set; } = 20;
	[Export] public int CurrentHP { get; set; } = 20;
	private ProgressBar hpBar;
	private Label earthMaxHpBonusLabel;
	private Dictionary<Node, float> enemyDamageCooldowns = new Dictionary<Node, float>();
	private const float DamageCooldownSeconds = 0.2f; // 12 frames at 60fps
	private HashSet<Node> overlappingEnemies = new HashSet<Node>();
	// Damage-absorbing shield pool (e.g. Aegis Ward), consumed before HP in TakeDamage().
	private int shieldPoints = 0;
	// Tracks the Earth element's max HP tier bonus currently applied to MaxHP, so it can be
	// added/removed incrementally as element instance counts shift during a run (issue #16).
	private int earthMaxHpBonusApplied = 0;




	// Track fire timers by spell id.
	public Dictionary<string, float> spellFireTimers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
	private Vector2 _velocity = Vector2.Zero;

	// Runtime spell instances equipped by the player.
	public List<SpellData> equippedSpells = new List<SpellData>();
	private Dictionary<string, SpellData> spellCatalog = new Dictionary<string, SpellData>(StringComparer.OrdinalIgnoreCase);
	private RandomNumberGenerator levelUpRng = new RandomNumberGenerator();
	public bool IsDead { get; private set; } = false;
	private float damageMultiplier = 1.0f;
	private float cooldownMultiplier = 1.0f;
	private float attackSpeedMultiplier = 1.0f;
	private float areaMultiplier = 1.0f;
	private float durationMultiplier = 1.0f;
	private int amountBonus = 0;
	private float growthMultiplier = 1.0f;
	private float recoveryPerSecond = 0.0f;
	private float recoveryAccumulator = 0.0f;
	// Temporary buff from a Bonus Drop Table one-time-use item (issue #25). Applied additively to
	// attackSpeedMultiplier/Speed on pickup and reverted when the timer expires, so every existing
	// consumer of those two fields benefits automatically without needing its own buff-aware code path.
	private float buffAttackSpeedBonus = 0f;
	private float buffMoveSpeedPixels = 0f;
	private float buffTimeRemaining = 0f;
	private int magnetBonus = 0;
	private int extraLives = 0;
	public int RerollsPerLevelUp { get; private set; } = 0;
	public int MagnetBonus => magnetBonus;
	// Crit chance stat (issue #26) and Luck stat (issue #23), both driven by SaveManager.ArcaneUpgradeLevels.
	private float baseCritChance = 0f;
	private int luckLevel = 0;
	[Export] public float CritDamageMultiplier { get; set; } = 1.6f;
	[Export] public float FireProximityRange { get; set; } = 100f;
	[Export] public float IceSlowDuration { get; set; } = 2.0f;
	[Export] public float PoisonDotDuration { get; set; } = 3.0f;
	[Export] public float LightningChainRadius { get; set; } = 150f;
	[Export] public float LightningChainDamageMultiplier { get; set; } = 0.6f;
	private RandomNumberGenerator combatRng = new RandomNumberGenerator();
	// Selected character (issue #29) - loaded from CharacterRoster based on Global.SelectedCharacterIdx.
	private CharacterData selectedCharacter;
	private AnimatedSprite2D? bodySprite;

	// Maximum number of spells the player can have equipped at once (issue #10).
	public const int MaxSpellSlots = 6;

	public override void _Ready()
	{
		AddToGroup("player");
		ConfigureEntityCollision();
		bodySprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_velocity = Vector2.Zero;
		ApplyArcaneUpgrades();
		spellFireTimers.Clear();
		XPToNextLevel = CalculateXPForLevel(CurrentLevel);
		InitializeSpellCatalog();
		levelUpRng.Randomize();
		combatRng.Randomize();
		ApplySelectedCharacter();

		// Create HP bar above player
		hpBar = new ProgressBar();
		hpBar.MinValue = 0;
		hpBar.MaxValue = MaxHP;
		hpBar.Value = CurrentHP;
		hpBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hpBar.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		hpBar.Position = new Vector2(-32, -48); // Adjust for your sprite size
		hpBar.Size = new Vector2(64, 8);
		AddChild(hpBar);

		earthMaxHpBonusLabel = new Label();
		earthMaxHpBonusLabel.Position = new Vector2(36, -56);
		earthMaxHpBonusLabel.AddThemeFontSizeOverride("font_size", 11);
		earthMaxHpBonusLabel.Modulate = ElementColors.GetColor(Element.Earth);
		AddChild(earthMaxHpBonusLabel);
		UpdateEarthMaxHpBonusLabel();

		var hurtBox = GetNode<Area2D>("HurtBox");
		if (hurtBox != null)
		{
			hurtBox.Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
			hurtBox.Connect("body_exited", new Callable(this, nameof(OnBodyExited)));
		}
	}

	private void ConfigureEntityCollision()
	{
		SetCollisionLayerValue(1, true);
		SetCollisionMaskValue(1, true);
		SetCollisionMaskValue(2, true);
	}

	// Loads the character selected in CharacterSelection (issue #29), applies its HP/speed modifiers,
	// and equips its starting spell + starting passive (if any) through
	// the normal TryAddOrLevelSpell flow. Falls back to Magic Missile if no character data is found
	// (e.g. running the Player scene directly without going through character selection).
	private void ApplySelectedCharacter()
	{
		selectedCharacter = CharacterRoster.GetByIndex(Global.SelectedCharacterIdx);
		if (selectedCharacter == null)
		{
			ApplyCharacterVisual(null);
			TryAddOrLevelSpell("magic_missile");
			return;
		}

		MaxHP = Math.Max(1, Mathf.RoundToInt(MaxHP * selectedCharacter.HealthModifier));
		CurrentHP = MaxHP;
		Speed *= selectedCharacter.SpeedModifier;

		string startingSpellId = selectedCharacter.StartingSpellResource?.Id;
		if (selectedCharacter.Id.Equals("test_wizard", StringComparison.OrdinalIgnoreCase)
			&& !string.IsNullOrWhiteSpace(Global.TestWizardStartingSpellId))
		{
			startingSpellId = Global.TestWizardStartingSpellId;
		}

		if (!string.IsNullOrWhiteSpace(startingSpellId))
			TryAddOrLevelSpell(startingSpellId, selectedCharacter.IsLegendaryStart);
		else
			TryAddOrLevelSpell("magic_missile");

		if (!string.IsNullOrWhiteSpace(selectedCharacter.StartingPassiveId))
		{
			if (!ApplyCharacterPassiveBonus(selectedCharacter.StartingPassiveId))
				TryAddOrLevelSpell(selectedCharacter.StartingPassiveId);
		}

		ApplyCharacterVisual(selectedCharacter);
	}

	private void ApplyCharacterVisual(CharacterData character)
	{
		if (bodySprite == null)
			return;

		Texture2D portrait = character?.Portrait;
		if (CharacterVisuals.TryBuildIdleFrames(portrait, out SpriteFrames characterFrames))
		{
			bodySprite.SpriteFrames = characterFrames;
			if (characterFrames.HasAnimation("idle"))
			{
				bodySprite.Animation = "idle";
				bodySprite.Play("idle");
			}
		}

		bodySprite.Modulate = CharacterVisuals.GetCharacterTint(character?.Id);
	}

	private bool ApplyCharacterPassiveBonus(string passiveId)
	{
		switch (passiveId.Trim().ToLowerInvariant())
		{
			case "cooldown_reduction_10":
				cooldownMultiplier *= 0.90f;
				return true;
			case "spell_damage_10":
				damageMultiplier *= 1.10f;
				return true;
			case "health_regen_0_5":
				recoveryPerSecond += 0.5f;
				return true;
			default:
				return false;
		}
	}

	private void ApplyArcaneUpgrades()
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null)
			return;

		int GetLevel(string id)
		{
			return saveManager.Data.ArcaneUpgradeLevels.TryGetValue(id, out int level) ? level : 0;
		}

		int damageLevel = GetLevel("damage");
		int recoveryLevel = GetLevel("recovery");
		int cooldownLevel = GetLevel("cooldowns");
		int areaLevel = GetLevel("area");
		int attackSpeedLevel = GetLevel("attack_speed");
		int durationLevel = GetLevel("duration");
		int amountLevel = GetLevel("amount");
		int moveSpeedLevel = GetLevel("movespeed");
		int magnetLevel = GetLevel("magnet");
		int growthLevel = GetLevel("growth");
		int extraLivesLevel = GetLevel("extra_lives");
		int rerollsLevel = GetLevel("rerolls");

		damageMultiplier = 1.0f + (damageLevel * 0.08f);
		recoveryPerSecond = recoveryLevel * 0.4f;
		cooldownMultiplier = MathF.Max(0.35f, 1.0f - (cooldownLevel * 0.05f));
		areaMultiplier = 1.0f + (areaLevel * 0.08f);
		attackSpeedMultiplier = 1.0f + (attackSpeedLevel * 0.06f);
		durationMultiplier = 1.0f + (durationLevel * 0.10f);
		amountBonus = amountLevel;
		growthMultiplier = 1.0f + (growthLevel * 0.10f);
		magnetBonus = magnetLevel * 20;
		extraLives = extraLivesLevel;
		RerollsPerLevelUp = rerollsLevel;

		float moveSpeedMultiplier = 1.0f + (moveSpeedLevel * 0.05f);
		Speed *= moveSpeedMultiplier;

		int vitalityLevel = 0;
		saveManager.Data.ArcaneUpgradeLevels.TryGetValue("vitality", out vitalityLevel);
		int bonusMaxHp = vitalityLevel * 5;
		if (bonusMaxHp > 0)
		{
			MaxHP += bonusMaxHp;
			CurrentHP += bonusMaxHp;
		}

		int critChanceLevel = GetLevel("crit_chance");
		baseCritChance = Math.Min(0.75f, critChanceLevel * 0.03f);
		luckLevel = GetLevel("luck");
	}

	private void InitializeSpellCatalog()
	{
		spellCatalog.Clear();

		AddSpellToCatalog(MagicMissileData ?? ResourceLoader.Load<SpellData>("res://SpellData.tres"));
		AddSpellToCatalog(ArcaneExplosionData ?? ResourceLoader.Load<SpellData>("res://SpellData_ArcaneExplosion.tres"));
		AddSpellToCatalog(SpiritualWeaponData ?? ResourceLoader.Load<SpellData>("res://SpellData_SpiritualWeapon.tres"));

		EnsureCatalogDefaults();
	}

	private void AddSpellToCatalog(SpellData spell)
	{
		if (spell == null || string.IsNullOrWhiteSpace(spell.Id))
			return;

		spellCatalog[spell.Id.Trim().ToLowerInvariant()] = spell;
	}

	private void EnsureCatalogDefaults()
	{
		if (!spellCatalog.ContainsKey("magic_missile"))
		{
			AddSpellToCatalog(CreateFallbackSpellData("magic_missile", "Magic Missile", 10, 0.5f, 1, 500f, "Fires a fast projectile at nearby enemies."));
		}

		if (!spellCatalog.ContainsKey("arcane_explosion"))
		{
			AddSpellToCatalog(CreateFallbackSpellData("arcane_explosion", "Arcane Explosion", 5, 1.5f, 1, 100f, "Creates a blast around the caster that damages nearby enemies."));
		}

		if (!spellCatalog.ContainsKey("spiritual_weapon"))
		{
			AddSpellToCatalog(CreateFallbackSpellData("spiritual_weapon", "Spiritual Weapon", 8, 1.0f, 2, 100f, "Summons spectral blades that strike enemies at intervals."));
		}

		// Defensive/passive spells (issue #13/#22), built on PassiveSpellEffect rather than the
		// projectile-firing pattern above.
		if (!spellCatalog.ContainsKey("aegis_ward"))
			AddSpellToCatalog(CreateDefensiveSpellData("aegis_ward", "Aegis Ward", 10f, "Periodically grants an absorbing shield.", ("Metal", 1), ("Light", 1)));

		if (!spellCatalog.ContainsKey("thornmail_barrier"))
			AddSpellToCatalog(CreateDefensiveSpellData("thornmail_barrier", "Thornmail Barrier", 1f, "Retaliates against nearby enemies when hit.", ("Earth", 1), ("Grass", 1)));

		if (!spellCatalog.ContainsKey("frozen_bulwark"))
			AddSpellToCatalog(CreateDefensiveSpellData("frozen_bulwark", "Frozen Bulwark", 1f, "Chance to freeze nearby attackers when hit.", ("Ice", 2)));

		if (!spellCatalog.ContainsKey("stormguard_aura"))
			AddSpellToCatalog(CreateDefensiveSpellData("stormguard_aura", "Stormguard Aura", 1f, "Strikes the nearest enemy with lightning when hit.", ("Lightning", 1), ("Metal", 1)));

		if (!spellCatalog.ContainsKey("venom_cloak"))
			AddSpellToCatalog(CreateDefensiveSpellData("venom_cloak", "Venom Cloak", 2.5f, "Periodically poisons nearby enemies.", ("Poison", 1), ("Darkness", 1)));

		if (!spellCatalog.ContainsKey("guardian_vines"))
			AddSpellToCatalog(CreateDefensiveSpellData("guardian_vines", "Guardian Vines", 6f, "Periodically roots nearby enemies.", ("Grass", 2)));

		if (!spellCatalog.ContainsKey("tidal_barrier"))
			AddSpellToCatalog(CreateDefensiveSpellData("tidal_barrier", "Tidal Barrier", 5f, "Periodically knocks back and slows nearby enemies.", ("Water", 1), ("Wind", 1)));

		if (!spellCatalog.ContainsKey("stone_bulwark"))
			AddSpellToCatalog(CreateDefensiveSpellData("stone_bulwark", "Stone Bulwark", 1f, "Passively reduces incoming damage.", ("Earth", 1), ("Metal", 1)));

		if (!spellCatalog.ContainsKey("blur"))
			AddSpellToCatalog(CreateDefensiveSpellData("blur", "Blur", 1f, "Illusory distortion grants a chance to avoid incoming hits entirely.", ("Arcane", 1), ("Wind", 1)));

		if (!spellCatalog.ContainsKey("fortunes_favor"))
			AddSpellToCatalog(CreateDefensiveSpellData("fortunes_favor", "Fortune's Favor", 1f, "Passively boosts your Luck.", ("Arcane", 1), ("Light", 1)));

		// New active offensive spells (issue #13 roster expansion).
		if (!spellCatalog.ContainsKey("fireball"))
			AddSpellToCatalog(FireballData ?? ResourceLoader.Load<SpellData>("res://SpellData_Fireball.tres"));

		if (!spellCatalog.ContainsKey("frost_shard"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_FrostShard.tres"));
		if (!spellCatalog.ContainsKey("shadow_bolt"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_ShadowBolt.tres"));
		if (!spellCatalog.ContainsKey("thorn_vine"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_ThornVine.tres"));
		if (!spellCatalog.ContainsKey("gale_blade"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_GaleBlade.tres"));
		if (!spellCatalog.ContainsKey("solar_flare"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_SolarFlare.tres"));
		if (!spellCatalog.ContainsKey("molten_shard"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_MoltenShard.tres"));
		if (!spellCatalog.ContainsKey("chain_lightning"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_ChainLightning.tres"));
		if (!spellCatalog.ContainsKey("toxic_spore_burst"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_ToxicSporeBurst.tres"));
		if (!spellCatalog.ContainsKey("obsidian_spike"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_ObsidianSpike.tres"));
		if (!spellCatalog.ContainsKey("cyclone_slash"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_CycloneSlash.tres"));
		if (!spellCatalog.ContainsKey("void_lance"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_VoidLance.tres"));
		if (!spellCatalog.ContainsKey("glacial_spike"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_GlacialSpike.tres"));

		// D&D-inspired spells (issue #28).
		if (!spellCatalog.ContainsKey("black_tentacles"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_BlackTentacles.tres"));
		if (!spellCatalog.ContainsKey("cone_of_cold"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_ConeOfCold.tres"));
		if (!spellCatalog.ContainsKey("scorching_ray"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_ScorchingRay.tres"));
		if (!spellCatalog.ContainsKey("meteor_swarm"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_MeteorSwarm.tres"));
		if (!spellCatalog.ContainsKey("haste"))
			AddSpellToCatalog(CreateDefensiveSpellData("haste", "Haste", 8f, "Periodically grants a brief attack-speed and move-speed surge.", ("Wind", 1), ("Lightning", 1)));
	}

	private static SpellData CreateDefensiveSpellData(string id, string name, float baseCooldown, string description, params (string element, int weight)[] elementWeights)
	{
		var spell = new SpellData
		{
			Id = id,
			Name = name,
			CurrentLevel = 1,
			MaxLevel = 8,
			BaseCooldown = baseCooldown,
			Description = description,
			IsPassive = true
		};

		foreach (var (element, weight) in elementWeights)
			spell.ElementWeights[element] = weight;

		return spell;
	}

	private static SpellData CreateFallbackSpellData(string id, string name, int baseDamage, float baseCooldown, int baseProjectileCount, float baseRange, string description)
	{
		var spell = new SpellData
		{
			Id = id,
			Name = name,
			CurrentLevel = 1,
			MaxLevel = 8,
			BaseDamage = baseDamage,
			BaseCooldown = baseCooldown,
			BaseProjectileCount = baseProjectileCount,
			BaseRange = baseRange,
			Description = description
		};

		// Fallback element tags, matching the #13 starter roster, in case the .tres resource fails to load.
		if (id.Equals("magic_missile", StringComparison.OrdinalIgnoreCase))
		{
			spell.ElementWeights["Arcane"] = 1;
			spell.ElementWeights["Lightning"] = 1;
		}
		else if (id.Equals("arcane_explosion", StringComparison.OrdinalIgnoreCase))
		{
			spell.ElementWeights["Arcane"] = 2;
		}
		else if (id.Equals("spiritual_weapon", StringComparison.OrdinalIgnoreCase))
		{
			spell.ElementWeights["Arcane"] = 1;
			spell.ElementWeights["Light"] = 1;
		}

		return spell;
	}

	// --- Elemental synergy system (issue #10 / #13) ---

	public Dictionary<Element, int> GetElementInstanceCounts()
	{
		var totals = new Dictionary<Element, int>();
		foreach (var spell in equippedSpells)
		{
			if (spell == null) continue;
			foreach (var pair in spell.GetElementWeights())
			{
				totals[pair.Key] = totals.TryGetValue(pair.Key, out int existing) ? existing + pair.Value : pair.Value;
			}
		}

		return totals;
	}

	// Returns the highest threshold (0, 2, 4, or 6) met by the given element's current instance count.
	public int GetElementTier(Element element)
	{
		var counts = GetElementInstanceCounts();
		int count = counts.TryGetValue(element, out int value) ? value : 0;
		if (count >= 6) return 6;
		if (count >= 4) return 4;
		if (count >= 2) return 2;
		return 0;
	}

	// Fills in a LevelUpOption's element preview fields (issue #15): for a brand-new spell, shows the
	// element counts added and their resulting totals; for an upgrade of an already-equipped spell,
	// shows the current standing (leveling up doesn't change its element weight contribution).
	private void ApplyElementPreview(LevelUpOption option, SpellData spellTemplate, Dictionary<Element, int> baselineCounts, bool isNewUnlock)
	{
		var weights = spellTemplate?.GetElementWeights();
		if (weights == null || weights.Count == 0)
			return;

		foreach (var pair in weights)
		{
			int baseCount = baselineCounts.TryGetValue(pair.Key, out int existing) ? existing : 0;
			int addedAmount = isNewUnlock ? pair.Value : 0;
			option.ElementContribution[pair.Key.ToString()] = addedAmount;
			option.ResultingElementCounts[pair.Key.ToString()] = baseCount + addedAmount;
		}
	}

	private float GetArcaneXpBonusMultiplier()
	{
		return GetElementTier(Element.Arcane) switch
		{
			6 => 1.35f,
			4 => 1.20f,
			2 => 1.10f,
			_ => 1.0f
		};
	}

	private float GetLightHealPercent()
	{
		return GetElementTier(Element.Light) switch
		{
			6 => 0.10f,
			4 => 0.06f,
			2 => 0.03f,
			_ => 0.0f
		};
	}

	// Called by damage-dealing spells after they hit an enemy, so the Light element can heal
	// the player for a percentage of damage dealt (see issue #16).
	public void NotifySpellDamageDealt(int damageDealt)
	{
		if (damageDealt <= 0)
			return;

		float healPercent = GetLightHealPercent();
		if (healPercent <= 0.0f)
			return;

		int healAmount = Mathf.RoundToInt(damageDealt * healPercent);
		if (healAmount > 0)
			Heal(healAmount);
	}

	// --- Remaining elemental tier bonuses (issue #16), self-contained (no on-hit hook needed) ---

	private float GetDarknessDamageReductionPercent()
	{
		return GetElementTier(Element.Darkness) switch
		{
			6 => 0.35f,
			4 => 0.20f,
			2 => 0.10f,
			_ => 0.0f
		};
	}

	private int GetMetalFlatDamageReduction()
	{
		return GetElementTier(Element.Metal) switch
		{
			6 => 4,
			4 => 2,
			2 => 1,
			_ => 0
		};
	}

	private float GetGrassRegenPerSecond()
	{
		return GetElementTier(Element.Grass) switch
		{
			6 => 4.0f,
			4 => 2.0f,
			2 => 1.0f,
			_ => 0.0f
		};
	}

	private int GetEarthMaxHpBonus()
	{
		return GetElementTier(Element.Earth) switch
		{
			6 => 100,
			4 => 50,
			2 => 20,
			_ => 0
		};
	}

	private float GetWindSpeedMultiplier()
	{
		return GetElementTier(Element.Wind) switch
		{
			6 => 1.35f,
			4 => 1.20f,
			2 => 1.10f,
			_ => 1.0f
		};
	}

	private float GetWaterCooldownMultiplier()
	{
		return GetElementTier(Element.Water) switch
		{
			6 => 0.82f,
			4 => 0.90f,
			2 => 0.95f,
			_ => 1.0f
		};
	}

	// --- Crit chance (issue #26) and Luck (issue #23) ---

	// Luck grants a small secondary bonus to crit chance on top of the player's own crit_chance stat.
	public float GetTotalCritChance() => Math.Min(0.9f, baseCritChance + (GetEffectiveLuckLevel() * 0.005f));

	// Normalized 0..1 progress toward "max Luck investment", for other systems (Legendary roll chance
	// #24, bonus drop chance #25) to scale their own formulas against, rather than reading luckLevel directly.
	public float GetLuckLevel01() => Math.Min(1.0f, GetEffectiveLuckLevel() * 0.05f);

	// Sums the shop/map Luck stat level with any flat bonuses from equipped passives (e.g. Fortune's
	// Favor, issue #27), so all Luck consumers see one consistent effective value.
	private int GetEffectiveLuckLevel()
	{
		int bonus = GetChildren().OfType<PassiveSpellEffect>().Sum(p => p.GetLuckBonus());
		return luckLevel + bonus;
	}

	// --- Legendary Spell Variant (issue #24) ---

	[Export] public Color LegendaryTintColor { get; set; } = new Color(1.4f, 1.1f, 0.35f, 1.0f);

	// Base 2% chance, scaling up to 10% at max Luck investment (see #24's decisions).
	public float GetLegendaryChance() => Math.Min(0.10f, 0.02f + (0.08f * GetLuckLevel01()));

	// Applies a placeholder "this is Legendary" gold tint to a spell's visual root (issue #24 -
	// eventually replaced by a proper hand-recolored palette per #30, faked with Modulate for now).
	private void ApplyLegendaryVisual(CanvasItem visual, SpellData spell)
	{
		if (visual != null && spell != null && spell.IsLegendary)
			visual.Modulate = LegendaryTintColor;
	}

	// --- Bonus Drop Table (issue #25) ---

	// Base 3% chance per enemy kill, scaling up to 10% at max Luck investment.
	public float GetBonusDropChance() => Math.Min(0.10f, 0.03f + (0.07f * GetLuckLevel01()));

	// Grants (or refreshes) a short temporary attack-speed/move-speed buff from a one-time-use
	// Bonus Drop Table item (BuffItem.cs). Applied additively to the live attackSpeedMultiplier/Speed
	// fields so every existing consumer benefits automatically; reverted when the timer expires.
	public void ApplyTemporaryBuff(float attackSpeedBonus, float moveSpeedBonus, float duration)
	{
		if (buffTimeRemaining > 0f)
		{
			attackSpeedMultiplier -= buffAttackSpeedBonus;
			Speed -= buffMoveSpeedPixels;
		}

		buffAttackSpeedBonus = attackSpeedBonus;
		buffMoveSpeedPixels = Speed * moveSpeedBonus;
		attackSpeedMultiplier += buffAttackSpeedBonus;
		Speed += buffMoveSpeedPixels;
		buffTimeRemaining = duration;
	}

	// --- Fire/Ice/Lightning/Poison tier bonuses (issue #16) - need the on-hit path below, unlike the 6 self-contained ones ---

	private float GetFireDamageBonusPercent()
	{
		return GetElementTier(Element.Fire) switch
		{
			6 => 0.35f,
			4 => 0.20f,
			2 => 0.10f,
			_ => 0.0f
		};
	}

	private float GetIceSlowPercent()
	{
		return GetElementTier(Element.Ice) switch
		{
			6 => 0.35f,
			4 => 0.20f,
			2 => 0.10f,
			_ => 0.0f
		};
	}

	private float GetLightningChainChance()
	{
		return GetElementTier(Element.Lightning) switch
		{
			6 => 0.35f,
			4 => 0.20f,
			2 => 0.10f,
			_ => 0.0f
		};
	}

	private int GetPoisonTickDamage()
	{
		return GetElementTier(Element.Poison) switch
		{
			6 => 8,
			4 => 4,
			2 => 2,
			_ => 0
		};
	}

	// Shared on-hit damage-application path (issues #16 and #26). All active spells and reactive
	// passive spells that deal damage to an enemy should call this instead of calling
	// enemy.TakeDamage() directly, so crit rolls and the Fire/Ice/Lightning/Poison element tiers apply
	// consistently everywhere instead of being re-implemented per spell script. bonusCritChance
	// lets an individual spell add to the roll via its own SpellEffect.CritChance level-upgrades
	// (see ElementalBolt.cs / Scorching Ray, issue #28) on top of the player's global crit_chance/Luck.
	public int DealDamageToEnemy(Node enemy, int baseDamage, float bonusCritChance = 0f, bool allowElementalChain = true)
	{
		if (enemy == null || !IsInstanceValid(enemy) || baseDamage <= 0 || !enemy.HasMethod("TakeDamage"))
			return 0;

		int finalDamage = baseDamage;

		bool isCrit = combatRng.Randf() < (GetTotalCritChance() + bonusCritChance);
		if (isCrit)
			finalDamage = Mathf.RoundToInt(finalDamage * CritDamageMultiplier);

		float fireBonus = GetFireDamageBonusPercent();
		if (fireBonus > 0.0f && enemy is Node2D enemyNode && IsInstanceValid(enemyNode)
			&& GlobalPosition.DistanceTo(enemyNode.GlobalPosition) <= FireProximityRange)
		{
			finalDamage = Mathf.RoundToInt(finalDamage * (1.0f + fireBonus));
		}
		finalDamage = Math.Max(1, finalDamage);

		enemy.Call("TakeDamage", finalDamage, isCrit);
		NotifySpellDamageDealt(finalDamage);
		TryChainLightningDamage(enemy, finalDamage, allowElementalChain);

		float iceSlow = GetIceSlowPercent();
		if (iceSlow > 0.0f && enemy.HasMethod("ApplySlow"))
			enemy.Call("ApplySlow", 1.0f - iceSlow, IceSlowDuration);

		int poisonTick = GetPoisonTickDamage();
		if (poisonTick > 0 && enemy.HasMethod("ApplyPoison"))
			enemy.Call("ApplyPoison", poisonTick, PoisonDotDuration);

		return finalDamage;
	}

	private void TryChainLightningDamage(Node sourceEnemy, int sourceDamage, bool allowElementalChain)
	{
		float chainChance = allowElementalChain ? GetLightningChainChance() : 0.0f;
		if (chainChance <= 0.0f || combatRng.Randf() >= chainChance || sourceEnemy is not Node2D sourceNode)
			return;

		var second = GetTree().GetNodesInGroup("enemies")
			.OfType<Node2D>()
			.Where(e => e != sourceNode && IsInstanceValid(e) && e.HasMethod("TakeDamage") && sourceNode.GlobalPosition.DistanceTo(e.GlobalPosition) <= LightningChainRadius)
			.OrderBy(e => sourceNode.GlobalPosition.DistanceTo(e.GlobalPosition))
			.FirstOrDefault();
		if (second == null)
			return;

		int chainDamage = Math.Max(1, Mathf.RoundToInt(sourceDamage * LightningChainDamageMultiplier));
		DealDamageToEnemy(second, chainDamage, allowElementalChain: false);
	}

	// Applies/refreshes the Earth element's max HP tier bonus. Called whenever the equipped spell
	// list changes, since element instance counts (and therefore the Earth tier) can shift during a run.
	private void RefreshElementalMaxHp()
	{
		int currentBonus = GetEarthMaxHpBonus();
		int delta = currentBonus - earthMaxHpBonusApplied;
		if (delta == 0)
		{
			UpdateEarthMaxHpBonusLabel();
			return;
		}

		MaxHP += delta;
		CurrentHP = Math.Clamp(CurrentHP + delta, 0, MaxHP);
		earthMaxHpBonusApplied = currentBonus;
		if (hpBar != null)
		{
			hpBar.MaxValue = MaxHP;
			hpBar.Value = CurrentHP;
		}
		UpdateEarthMaxHpBonusLabel();
	}

	private void UpdateEarthMaxHpBonusLabel()
	{
		if (earthMaxHpBonusLabel == null)
			return;

		earthMaxHpBonusLabel.Visible = earthMaxHpBonusApplied > 0;
		earthMaxHpBonusLabel.Text = earthMaxHpBonusApplied > 0 ? $"+{earthMaxHpBonusApplied} HP" : string.Empty;
	}

	public void TakeDamage(int amount)
	{
		if (IsDead)
			return;

		if (amount > 0)
		{
			float dodgeChance = Math.Min(0.75f, GetChildren().OfType<PassiveSpellEffect>().Sum(p => p.GetDodgeChance()));
			if (dodgeChance > 0.0f && combatRng.Randf() < dodgeChance)
			{
				PlayBlurDodgeEffect();
				return; // Blur (#27): incoming hit completely avoided - no signal, no HP loss, no reactions.
			}
		}

		if (amount > 0)
			EmitSignal(nameof(DamageTaken), amount);

		int mitigated = Math.Max(0, amount);
		float darknessReduction = GetDarknessDamageReductionPercent();
		if (darknessReduction > 0.0f && mitigated > 0)
		{
			mitigated = Math.Max(0, Mathf.RoundToInt(mitigated * (1.0f - darknessReduction)));
		}
		if (shieldPoints > 0 && mitigated > 0)
		{
			int absorbed = Math.Min(shieldPoints, mitigated);
			shieldPoints -= absorbed;
			mitigated -= absorbed;
		}
		if (mitigated > 0)
		{
			int flatReduction = GetChildren().OfType<PassiveSpellEffect>().Sum(p => p.GetFlatDamageReduction()) + GetMetalFlatDamageReduction();
			mitigated = Math.Max(0, mitigated - flatReduction);
		}

		CurrentHP = Math.Max(0, CurrentHP - mitigated);
		if (hpBar != null)
			hpBar.Value = CurrentHP;
		if (CurrentHP <= 0)
		{
			if (extraLives > 0)
			{
				extraLives--;
				CurrentHP = Math.Max(1, MaxHP / 2);
				if (hpBar != null)
					hpBar.Value = CurrentHP;
				return;
			}

			IsDead = true;
			GD.Print("Player died");
			EmitSignal(nameof(Died));
		}
	}

	private void PlayBlurDodgeEffect()
	{
		if (bodySprite?.SpriteFrames == null)
			return;

		Node2D? parent2D = GetParent<Node2D>();
		if (parent2D == null)
			return;

		for (int i = 0; i < 3; i++)
		{
			var ghost = new AnimatedSprite2D
			{
				SpriteFrames = bodySprite.SpriteFrames,
				Animation = bodySprite.Animation,
				Frame = bodySprite.Frame,
				FrameProgress = bodySprite.FrameProgress,
				Scale = bodySprite.Scale * (1.0f + i * 0.08f),
				GlobalPosition = bodySprite.GlobalPosition,
				Modulate = new Color(0.72f, 0.86f, 1.0f, 0.55f - i * 0.13f),
				ZIndex = 20
			};

			Vector2 drift = new Vector2(
				combatRng.RandfRange(-10f, 10f),
				combatRng.RandfRange(-8f, 6f));

			parent2D.AddChild(ghost);

			Tween tween = ghost.CreateTween();
			tween.SetParallel(true);
			tween.TweenProperty(ghost, "global_position", ghost.GlobalPosition + drift, 0.16f);
			tween.TweenProperty(ghost, "scale", ghost.Scale * 1.14f, 0.16f);
			tween.TweenProperty(ghost, "modulate:a", 0.0f, 0.16f);
			tween.SetParallel(false);
			tween.TweenCallback(Callable.From(() => ghost.QueueFree()));
		}

		bodySprite.Modulate = new Color(0.8f, 0.92f, 1.0f, 0.95f);
		Tween selfTween = bodySprite.CreateTween();
		selfTween.TweenProperty(bodySprite, "modulate", Colors.White, 0.1f);
	}

	public void Heal(int amount)
	{
		CurrentHP = Math.Min(MaxHP, CurrentHP + amount);
		if (hpBar != null)
			hpBar.Value = CurrentHP;
	}

	// Grants (or refreshes to the stronger value of) an absorbing shield pool (Aegis Ward, issue #13/#22).
	public void AddShield(int amount)
	{
		shieldPoints = Math.Max(shieldPoints, Math.Max(0, amount));
	}

	private static int CalculateXPForLevel(int level)
	{
		return (level * 10) + (level - 1) * 10;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsDead)
			return;

		MovePlayer(delta);

		// enemy damage cooldown logic
		// Update cooldown timers

		// Damage-over-time for overlapping enemies
		foreach (var enemy in overlappingEnemies)
		{
			if (!enemyDamageCooldowns.ContainsKey(enemy))
				enemyDamageCooldowns[enemy] = DamageCooldownSeconds; // So damage applies on next tick

			enemyDamageCooldowns[enemy] += (float)delta;
			if (enemyDamageCooldowns[enemy] >= DamageCooldownSeconds)
			{
				TakeDamage(1);
				enemyDamageCooldowns[enemy] = 0f;
			}
		}

		float effectiveRegenPerSecond = recoveryPerSecond + GetGrassRegenPerSecond();
		if (effectiveRegenPerSecond > 0.0f && CurrentHP > 0 && CurrentHP < MaxHP)
		{
			recoveryAccumulator += effectiveRegenPerSecond * (float)delta;
			while (recoveryAccumulator >= 1.0f)
			{
				Heal(1);
				recoveryAccumulator -= 1.0f;
			}
		}

		if (buffTimeRemaining > 0f)
		{
			buffTimeRemaining -= (float)delta;
			if (buffTimeRemaining <= 0f)
			{
				buffTimeRemaining = 0f;
				attackSpeedMultiplier -= buffAttackSpeedBonus;
				Speed -= buffMoveSpeedPixels;
				buffAttackSpeedBonus = 0f;
				buffMoveSpeedPixels = 0f;
			}
		}

		// Spell firing logic
		if (MagicMissileScene == null || ArcaneExplosionScene == null || SpiritualWeaponScene == null)
		{
			GD.PrintErr("Error: MagicMissileScene or ArcaneExplosionScene or SpiritualWeaponScene is not assigned in the Player script.");
			return;
		}

		foreach (var spell in equippedSpells)
		{
			if (spell == null || string.IsNullOrWhiteSpace(spell.Id))
				continue;

			if (!spellFireTimers.ContainsKey(spell.Id))
				spellFireTimers[spell.Id] = 0f;

			float interval = (spell.GetCooldownAtLevel(spell.CurrentLevel) * cooldownMultiplier * GetWaterCooldownMultiplier()) / attackSpeedMultiplier;
			interval = MathF.Max(0.05f, interval);
			spellFireTimers[spell.Id] += (float)delta;
			if (spellFireTimers[spell.Id] >= interval)
			{
				if (spell.Id.Equals("magic_missile", StringComparison.OrdinalIgnoreCase))
				{
					var enemies = GetTree().GetNodesInGroup("enemies");
					if (enemies.Count > 0)
					{
						Node2D nearest = null;
						float minDist = float.MaxValue;
						foreach (var e in enemies)
						{
							if (e is Node2D n2d)
							{
								float dist = GlobalPosition.DistanceTo(n2d.GlobalPosition);
								if (dist < minDist)
								{
									minDist = dist;
									nearest = n2d;
								}
							}
						}
						if (nearest != null)
						{
							float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
							if (minDist <= castRange)
							{
								int projectileCount = Math.Max(1, 1 + amountBonus);
								for (int p = 0; p < projectileCount; p++)
								{
									var missile = MagicMissileScene.Instantiate<Area2D>();
									missile.Position = GlobalPosition;
									var script = missile as MagicMissile;
									if (script != null)
									{
										script.SpellData = spell;
										script.DamageMultiplier = damageMultiplier;
										script.AreaMultiplier = areaMultiplier;
										script.DurationMultiplier = durationMultiplier;
										script.SetSpellLevel(spell.CurrentLevel);
										script.PlayerRef = this;
									}
									GetParent().AddChild(missile);
									ApplyLegendaryVisual(missile, spell);
									var shootMethod = missile.GetType().GetMethod("Shoot");
									if (shootMethod != null)
									{
										shootMethod.Invoke(missile, new object[] { GlobalPosition, nearest.GlobalPosition, nearest });
									}
								}
							}
						}
					}
				}
				else if (spell.Id.Equals("arcane_explosion", StringComparison.OrdinalIgnoreCase))
				{
					GD.Print("Firing Arcane Explosion");

					// Ensure only one ArcaneExplosion follows this player. If not present, create and attach to player.
					bool hasExplosion = GetChildren().OfType<Node>().Any(n => n is ArcaneExplosion);
					if (!hasExplosion)
					{
						var explosion = ArcaneExplosionScene.Instantiate<Area2D>();
						// Attach to player so it follows automatically; set local position to origin
						explosion.Position = Vector2.Zero;
						var script = explosion as ArcaneExplosion;
						if (script != null)
						{
							script.SpellData = spell;
							script.DamageMultiplier = damageMultiplier;
							script.AreaMultiplier = areaMultiplier;
							script.CooldownMultiplier = cooldownMultiplier / attackSpeedMultiplier;
							script.DurationMultiplier = durationMultiplier;
							script.SetSpellLevel(spell.CurrentLevel);
							// Make the explosion follow the player and persist (TotalLifetime = 0 means infinite)
							script.PlayerRef = this;
							script.TotalLifetime = 0f;
						}
						AddChild(explosion);
						ApplyLegendaryVisual(explosion, spell);
					}
					else
					{
						var existing = GetChildren().OfType<ArcaneExplosion>().FirstOrDefault();
						if (existing != null)
						{
							existing.SpellData = spell;
							existing.DamageMultiplier = damageMultiplier;
							existing.AreaMultiplier = areaMultiplier;
							existing.CooldownMultiplier = cooldownMultiplier / attackSpeedMultiplier;
							existing.DurationMultiplier = durationMultiplier;
							existing.SetSpellLevel(spell.CurrentLevel);
						}
					}

				}
				else if (spell.Id.Equals("spiritual_weapon", StringComparison.OrdinalIgnoreCase))
				{
					GD.Print("Firing Spiritual Weapon");
					var spiritualWeapon = SpiritualWeaponScene.Instantiate<Node2D>();
					// Attach to player so it follows automatically; set local position to origin.
					spiritualWeapon.Position = Vector2.Zero;
					var script = spiritualWeapon as SpiritualWeapon;
					if (script != null)
					{
						script.SpellData = spell;
						script.DamageMultiplier = damageMultiplier;
						script.AreaMultiplier = areaMultiplier;
						script.AttackSpeedMultiplier = attackSpeedMultiplier;
						script.DurationMultiplier = durationMultiplier;
						script.ProjectileCountBonus = amountBonus;
						script.SetSpellLevel(spell.CurrentLevel);
						script.PlayerRef = this;
					}
					AddChild(spiritualWeapon);
					ApplyLegendaryVisual(spiritualWeapon, spell);

				}
				else if (spell.Id.Equals("fireball", StringComparison.OrdinalIgnoreCase) && FireballScene != null)
				{
					var enemies = GetTree().GetNodesInGroup("enemies");
					if (enemies.Count > 0)
					{
						Node2D nearest = null;
						float minDist = float.MaxValue;
						foreach (var e in enemies)
						{
							if (e is Node2D n2d)
							{
								float dist = GlobalPosition.DistanceTo(n2d.GlobalPosition);
								if (dist < minDist)
								{
									minDist = dist;
									nearest = n2d;
								}
							}
						}
						if (nearest != null)
						{
							float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
							if (minDist <= castRange)
							{
								var fireball = FireballScene.Instantiate<Area2D>();
								fireball.Position = GlobalPosition;
								var script2 = fireball as Fireball;
								if (script2 != null)
								{
									script2.SpellData = spell;
									script2.DamageMultiplier = damageMultiplier;
									script2.AreaMultiplier = areaMultiplier;
									script2.DurationMultiplier = durationMultiplier;
									script2.SetSpellLevel(spell.CurrentLevel);
									script2.PlayerRef = this;
								}
								GetParent().AddChild(fireball);
								ApplyLegendaryVisual(fireball, spell);
								script2?.Shoot(GlobalPosition, nearest.GlobalPosition);
							}
						}
					}
				}
				else if (spell.Id.Equals("frost_shard", StringComparison.OrdinalIgnoreCase)) FireBoltSpell(spell, FrostShardScene);
				else if (spell.Id.Equals("shadow_bolt", StringComparison.OrdinalIgnoreCase)) FireBoltSpell(spell, ShadowBoltScene);
				else if (spell.Id.Equals("thorn_vine", StringComparison.OrdinalIgnoreCase)) FireBoltSpell(spell, ThornVineScene);
				else if (spell.Id.Equals("gale_blade", StringComparison.OrdinalIgnoreCase)) FireBoltSpell(spell, GaleBladeScene);
				else if (spell.Id.Equals("molten_shard", StringComparison.OrdinalIgnoreCase)) FireBoltSpell(spell, MoltenShardScene);
				else if (spell.Id.Equals("chain_lightning", StringComparison.OrdinalIgnoreCase)) FireChainLightningSpell(spell, ChainLightningScene);
				else if (spell.Id.Equals("void_lance", StringComparison.OrdinalIgnoreCase)) FireBoltSpell(spell, VoidLanceScene);
				else if (spell.Id.Equals("glacial_spike", StringComparison.OrdinalIgnoreCase)) FireBoltSpell(spell, GlacialSpikeScene);
				else if (spell.Id.Equals("solar_flare", StringComparison.OrdinalIgnoreCase)) FireOrRefreshElementalPulse(spell, SolarFlareScene);
				else if (spell.Id.Equals("toxic_spore_burst", StringComparison.OrdinalIgnoreCase)) FireOrRefreshElementalPulse(spell, ToxicSporeBurstScene);
				else if (spell.Id.Equals("obsidian_spike", StringComparison.OrdinalIgnoreCase)) FireGroundSpike(spell, ObsidianSpikeScene);
				else if (spell.Id.Equals("cyclone_slash", StringComparison.OrdinalIgnoreCase)) FireOrRefreshOrbitingBlade(spell, CycloneSlashScene);
				else if (spell.Id.Equals("black_tentacles", StringComparison.OrdinalIgnoreCase)) FireBlackTentacles(spell, BlackTentaclesScene);
				else if (spell.Id.Equals("cone_of_cold", StringComparison.OrdinalIgnoreCase)) FireConeBlast(spell, ConeOfColdScene);
				else if (spell.Id.Equals("scorching_ray", StringComparison.OrdinalIgnoreCase)) FireScorchingRay(spell, ScorchingRayScene);
				else if (spell.Id.Equals("meteor_swarm", StringComparison.OrdinalIgnoreCase)) FireMeteorSwarm(spell, MeteorImpactScene);
				spellFireTimers[spell.Id] = 0f;
			}
		}
	}

	// --- Shared firing helpers for the issue #13 roster expansion spells (ElementalBolt/ElementalPulse/
	// GroundSpike/OrbitingBlade) so each new spell only needs a SpellData .tres + scene, not a new
	// branch of bespoke firing logic. ---

	private void FireBoltSpell(SpellData spell, PackedScene scene)
	{
		if (scene == null) return;
		var enemies = GetTree().GetNodesInGroup("enemies");
		if (enemies.Count == 0) return;

		Node2D nearest = null;
		float minDist = float.MaxValue;
		foreach (var e in enemies)
		{
			if (e is Node2D n2d)
			{
				float dist = GlobalPosition.DistanceTo(n2d.GlobalPosition);
				if (dist < minDist) { minDist = dist; nearest = n2d; }
			}
		}
		if (nearest == null) return;

		float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
		if (minDist > castRange) return;

		var bolt = scene.Instantiate<Area2D>();
		bolt.Position = GlobalPosition;
		if (bolt is ElementalBolt eb)
		{
			eb.SpellData = spell;
			eb.DamageMultiplier = damageMultiplier;
			eb.AreaMultiplier = areaMultiplier;
			eb.DurationMultiplier = durationMultiplier;
			eb.SetSpellLevel(spell.CurrentLevel);
			eb.PlayerRef = this;
		}
		GetParent().AddChild(bolt);
		ApplyLegendaryVisual(bolt, spell);
		(bolt as ElementalBolt)?.Shoot(GlobalPosition, nearest.GlobalPosition, nearest);
	}

	private void FireChainLightningSpell(SpellData spell, PackedScene scene)
	{
		if (scene == null) return;
		FindNearestEnemy(out var nearest, out var minDist);
		if (nearest == null) return;

		float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
		if (minDist > castRange) return;

		var chain = scene.Instantiate<Area2D>();
		chain.Position = Vector2.Zero;
		if (chain is ChainLightning chainLightning)
		{
			chainLightning.SpellData = spell;
			chainLightning.DamageMultiplier = damageMultiplier;
			chainLightning.AreaMultiplier = areaMultiplier;
			chainLightning.DurationMultiplier = durationMultiplier;
			chainLightning.ProjectileCountBonus = amountBonus;
			chainLightning.SetSpellLevel(spell.CurrentLevel);
			chainLightning.PlayerRef = this;
		}

		GetParent().AddChild(chain);
		ApplyLegendaryVisual(chain, spell);
		(chain as ChainLightning)?.CastFromPlayer(this, nearest);
	}

	private void FireOrRefreshElementalPulse(SpellData spell, PackedScene scene)
	{
		if (scene == null) return;
		var existing = GetChildren().OfType<ElementalPulse>().FirstOrDefault(p => p.SpellData != null && p.SpellData.Id.Equals(spell.Id, StringComparison.OrdinalIgnoreCase));
		if (existing == null)
		{
			var pulse = scene.Instantiate<Area2D>();
			pulse.Position = Vector2.Zero;
			var script = pulse as ElementalPulse;
			if (script != null)
			{
				script.SpellData = spell;
				script.DamageMultiplier = damageMultiplier;
				script.AreaMultiplier = areaMultiplier;
				script.CooldownMultiplier = cooldownMultiplier / attackSpeedMultiplier;
				script.DurationMultiplier = durationMultiplier;
				script.SetSpellLevel(spell.CurrentLevel);
				script.PlayerRef = this;
				script.TotalLifetime = 0f;
			}
			AddChild(pulse);
			ApplyLegendaryVisual(pulse, spell);
		}
		else
		{
			existing.DamageMultiplier = damageMultiplier;
			existing.AreaMultiplier = areaMultiplier;
			existing.CooldownMultiplier = cooldownMultiplier / attackSpeedMultiplier;
			existing.DurationMultiplier = durationMultiplier;
			existing.SetSpellLevel(spell.CurrentLevel);
		}
	}

	private void FireGroundSpike(SpellData spell, PackedScene scene)
	{
		if (scene == null) return;
		var enemies = GetTree().GetNodesInGroup("enemies");
		if (enemies.Count == 0) return;

		Node2D nearest = null;
		float minDist = float.MaxValue;
		foreach (var e in enemies)
		{
			if (e is Node2D n2d)
			{
				float dist = GlobalPosition.DistanceTo(n2d.GlobalPosition);
				if (dist < minDist) { minDist = dist; nearest = n2d; }
			}
		}
		if (nearest == null) return;

		float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
		if (minDist > castRange) return;

		var spike = scene.Instantiate<Node2D>();
		var script = spike as GroundSpike;
		if (script != null)
		{
			script.SpellData = spell;
			script.DamageMultiplier = damageMultiplier;
			script.AreaMultiplier = areaMultiplier;
			script.SetSpellLevel(spell.CurrentLevel);
			script.PlayerRef = this;
		}
		GetParent().AddChild(spike);
		ApplyLegendaryVisual(spike, spell);
		script?.CastAt(nearest.GlobalPosition);
	}

	private void FireOrRefreshOrbitingBlade(SpellData spell, PackedScene scene)
	{
		if (scene == null) return;
		var existing = GetChildren().OfType<OrbitingBlade>().FirstOrDefault(o => o.SpellData != null && o.SpellData.Id.Equals(spell.Id, StringComparison.OrdinalIgnoreCase));
		if (existing == null)
		{
			var orbit = scene.Instantiate<Node2D>();
			orbit.Position = Vector2.Zero;
			var script = orbit as OrbitingBlade;
			if (script != null)
			{
				script.SpellData = spell;
				script.DamageMultiplier = damageMultiplier;
				script.AreaMultiplier = areaMultiplier;
				script.AttackSpeedMultiplier = attackSpeedMultiplier;
				script.ProjectileCountBonus = amountBonus;
				script.SetSpellLevel(spell.CurrentLevel);
				script.PlayerRef = this;
			}
			AddChild(orbit);
			ApplyLegendaryVisual(orbit, spell);
		}
		else
		{
			existing.DamageMultiplier = damageMultiplier;
			existing.AreaMultiplier = areaMultiplier;
			existing.AttackSpeedMultiplier = attackSpeedMultiplier;
			existing.ProjectileCountBonus = amountBonus;
			existing.SetSpellLevel(spell.CurrentLevel);
		}
	}

	// --- Firing helpers for the issue #28 D&D-inspired spells ---

	private void FindNearestEnemy(out Node2D nearest, out float minDist)
	{
		nearest = null;
		minDist = float.MaxValue;
		foreach (var e in GetTree().GetNodesInGroup("enemies"))
		{
			if (e is Node2D n2d)
			{
				float dist = GlobalPosition.DistanceTo(n2d.GlobalPosition);
				if (dist < minDist) { minDist = dist; nearest = n2d; }
			}
		}
	}

	private void FireBlackTentacles(SpellData spell, PackedScene scene)
	{
		if (scene == null) return;
		FindNearestEnemy(out var nearest, out var minDist);
		if (nearest == null) return;
		float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
		if (minDist > castRange) return;

		var zone = scene.Instantiate<Node2D>();
		var script = zone as BlackTentacles;
		if (script != null)
		{
			script.SpellData = spell;
			script.DamageMultiplier = damageMultiplier;
			script.AreaMultiplier = areaMultiplier;
			script.DurationMultiplier = durationMultiplier;
			script.SetSpellLevel(spell.CurrentLevel);
			script.PlayerRef = this;
		}
		GetParent().AddChild(zone);
		ApplyLegendaryVisual(zone, spell);
		script?.CastAt(nearest.GlobalPosition);
	}

	private void FireConeBlast(SpellData spell, PackedScene scene)
	{
		if (scene == null) return;
		FindNearestEnemy(out var nearest, out _);
		Vector2 direction = nearest != null ? (nearest.GlobalPosition - GlobalPosition) : Vector2.Right;

		var cone = scene.Instantiate<Node2D>();
		var script = cone as ConeBlast;
		if (script != null)
		{
			script.SpellData = spell;
			script.DamageMultiplier = damageMultiplier;
			script.AreaMultiplier = areaMultiplier;
			script.SetSpellLevel(spell.CurrentLevel);
			script.PlayerRef = this;
		}
		GetParent().AddChild(cone);
		ApplyLegendaryVisual(cone, spell);
		script?.Fire(GlobalPosition, direction);
	}

	// Scorching Ray (issue #28): fires beam lines to nearest enemies. Each beam extends from the
	// player to the target, then retracts into the target before despawning.
	private void FireScorchingRay(SpellData spell, PackedScene scene)
	{
		if (scene == null) return;
		var enemies = GetTree().GetNodesInGroup("enemies")
			.OfType<Node2D>()
			.Where(e => IsInstanceValid(e))
			.OrderBy(e => GlobalPosition.DistanceTo(e.GlobalPosition))
			.ToList();
		if (enemies.Count == 0) return;

		float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
		// Scales target count with level so the ray fans out to more enemies as it levels.
		int levelTargetBonus = (spell.CurrentLevel - 1) / 2;
		int rayCount = Math.Max(1, spell.GetProjectileCountAtLevel(spell.CurrentLevel) + amountBonus + levelTargetBonus);
		for (int i = 0; i < rayCount; i++)
		{
			var target = enemies[Math.Min(i, enemies.Count - 1)];
			if (GlobalPosition.DistanceTo(target.GlobalPosition) > castRange)
				continue;

			var ray = scene.Instantiate<Node2D>();
			ray.Position = Vector2.Zero;
			if (ray is ScorchingRayBeam beam)
			{
				beam.SpellData = spell;
				beam.DamageMultiplier = damageMultiplier;
				beam.AreaMultiplier = areaMultiplier;
				beam.SetSpellLevel(spell.CurrentLevel);
				beam.PlayerRef = this;
			}
			GetParent().AddChild(ray);
			ApplyLegendaryVisual(ray, spell);
			(ray as ScorchingRayBeam)?.Fire(GlobalPosition, target, target.GlobalPosition);
		}
	}

	// Meteor Swarm (issue #28): telegraphed multi-impact AoE - several delayed-detonation impact
	// zones (reusing GroundSpike.cs, same telegraph-then-explode pattern as Obsidian Spike) land
	// near the nearest enemy cluster simultaneously.
	private void FireMeteorSwarm(SpellData spell, PackedScene scene)
	{
		if (scene == null) return;
		FindNearestEnemy(out var nearest, out var minDist);
		float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
		Vector2 center = (nearest != null && minDist <= castRange) ? nearest.GlobalPosition : GlobalPosition;

		const int impactCount = 4;
		const float scatterRadius = 110f;
		for (int i = 0; i < impactCount; i++)
		{
			var offset = new Vector2(combatRng.RandfRange(-scatterRadius, scatterRadius), combatRng.RandfRange(-scatterRadius, scatterRadius));
			var impact = scene.Instantiate<Node2D>();
			var script = impact as MeteorImpact;
			if (script != null)
			{
				script.SpellData = spell;
				script.DamageMultiplier = damageMultiplier;
				script.AreaMultiplier = areaMultiplier;
				script.TelegraphDuration = 0.9f;
				script.SetSpellLevel(spell.CurrentLevel);
				script.PlayerRef = this;
			}
			GetParent().AddChild(impact);
			ApplyLegendaryVisual(impact, spell);
			script?.CastAt(center + offset);
		}
	}

	// delta isn't used yet, but it might be needed later
	private void MovePlayer(double delta)
	{
		var input = Vector2.Zero;
		input.X = Input.GetActionStrength("ui_right") - Input.GetActionStrength("ui_left");
		input.Y = Input.GetActionStrength("ui_down") - Input.GetActionStrength("ui_up");
		if (input.Length() > 1)
			input = input.Normalized();
		_velocity = input * Speed * GetWindSpeedMultiplier();
		Velocity = _velocity;
		MoveAndSlide();
	}

	public void AddXp(int amount)
	{
		amount = Math.Max(1, Mathf.RoundToInt(amount * growthMultiplier * GetArcaneXpBonusMultiplier()));
		CurrentXP += amount;
		if (CurrentXP >= XPToNextLevel)
		{
			CurrentXP -= CalculateXPForLevel(CurrentLevel); // Reset XP to zero, but keep overflow
			CurrentLevel += 1; // level up
			XPToNextLevel = CalculateXPForLevel(CurrentLevel);
			GD.Print($"Player leveled up to level {CurrentLevel}!");
			// Optionally, you could emit a signal or call a method to open a level-up menu here
			// Open the level-up menu here
			// LevelUpMenu? menu = GetTree().Root.GetNodeOrNull<LevelUpMenu>("LevelUpMenu");
			// if (menu != null)
			// {
			// 	menu.Show();
			// 	menu.SetOptions(); // You can pass specific options if needed
			// }
			EmitSignal(nameof(LevelGained));

		}
		EmitSignal(nameof(XpGained), CurrentXP);

	}

	public void LevelUpImmediately()
	{
		CurrentLevel += 1;
		XPToNextLevel = CalculateXPForLevel(CurrentLevel);
		GD.Print($"Player leveled up to level {CurrentLevel}!");
		EmitSignal(nameof(LevelGained));
		EmitSignal(nameof(XpGained), CurrentXP);
	}

	public bool TryAddOrLevelSpell(string selectionId, bool forceLegendary = false)
	{
		SpellData spellTemplate = ResolveSpellTemplate(selectionId);
		if (spellTemplate == null)
		{
			GD.PrintErr($"Spell '{selectionId}' not found in spell catalog.");
			return false;
		}

		SpellData existing = equippedSpells.FirstOrDefault(s => s != null && s.Id.Equals(spellTemplate.Id, StringComparison.OrdinalIgnoreCase));
		if (existing == null)
		{
			if (equippedSpells.Count >= MaxSpellSlots)
			{
				GD.PrintErr($"Cannot add spell '{spellTemplate.Id}': loadout is full ({MaxSpellSlots} slots). Remove a spell first.");
				return false;
			}

			var runtimeSpell = spellTemplate.Duplicate(true) as SpellData;
			if (runtimeSpell == null)
				return false;

			runtimeSpell.CurrentLevel = 1;
			runtimeSpell.IsLegendary = forceLegendary || combatRng.Randf() < GetLegendaryChance();
			if (runtimeSpell.IsLegendary && !runtimeSpell.Name.EndsWith(" \u2605"))
			{
				runtimeSpell.Name += " \u2605";
				GD.Print($"{runtimeSpell.Name} rolled Legendary!");
			}
			equippedSpells.Add(runtimeSpell);
			spellFireTimers[runtimeSpell.Id] = 0f;
			GD.Print($"Equipped spell {runtimeSpell.Name} at level {runtimeSpell.CurrentLevel}");
			RefreshPersistentSpellInstance(runtimeSpell);
			RefreshElementalMaxHp();
			return true;
		}

		existing.CurrentLevel = Math.Min(existing.MaxLevel, existing.CurrentLevel + 1);
		spellFireTimers[existing.Id] = 0f;
		GD.Print($"Spell {existing.Name} leveled up to {existing.CurrentLevel}");
		RefreshPersistentSpellInstance(existing);
		return true;
	}

	// Permanently removes an owned spell from this run's loadout (issue #10's full-loadout swap flow).
	// This is a temporary, in-run removal - distinct from #21's permanent cross-run pool curation.
	public bool RemoveEquippedSpell(string spellId)
	{
		if (string.IsNullOrWhiteSpace(spellId))
			return false;

		var existing = equippedSpells.FirstOrDefault(s => s != null && s.Id.Equals(spellId, StringComparison.OrdinalIgnoreCase));
		if (existing == null)
			return false;

		equippedSpells.Remove(existing);
		spellFireTimers.Remove(existing.Id);
		RemovePersistentSpellInstance(existing);
		GD.Print($"Removed spell {existing.Name} from loadout.");
		RefreshElementalMaxHp();
		return true;
	}

	public IReadOnlyList<SpellData> GetEquippedSpells() => equippedSpells;

	private void RemovePersistentSpellInstance(SpellData spell)
	{
		if (spell == null) return;

		if (spell.Id.Equals("arcane_explosion", StringComparison.OrdinalIgnoreCase))
		{
			var existing = GetChildren().OfType<ArcaneExplosion>().FirstOrDefault();
			existing?.QueueFree();
		}

		if (spell.Id.Equals("spiritual_weapon", StringComparison.OrdinalIgnoreCase))
		{
			var existing = GetChildren().OfType<SpiritualWeapon>().FirstOrDefault();
			existing?.QueueFree();
		}

		switch (spell.Id.ToLowerInvariant())
		{
			case "aegis_ward": GetChildren().OfType<AegisWard>().FirstOrDefault()?.QueueFree(); break;
			case "thornmail_barrier": GetChildren().OfType<ThornmailBarrier>().FirstOrDefault()?.QueueFree(); break;
			case "frozen_bulwark": GetChildren().OfType<FrozenBulwark>().FirstOrDefault()?.QueueFree(); break;
			case "stormguard_aura": GetChildren().OfType<StormguardAura>().FirstOrDefault()?.QueueFree(); break;
			case "venom_cloak": GetChildren().OfType<VenomCloak>().FirstOrDefault()?.QueueFree(); break;
			case "guardian_vines": GetChildren().OfType<GuardianVines>().FirstOrDefault()?.QueueFree(); break;
			case "tidal_barrier": GetChildren().OfType<TidalBarrier>().FirstOrDefault()?.QueueFree(); break;
			case "stone_bulwark": GetChildren().OfType<StoneBulwark>().FirstOrDefault()?.QueueFree(); break;
			case "blur": GetChildren().OfType<Blur>().FirstOrDefault()?.QueueFree(); break;
			case "fortunes_favor": GetChildren().OfType<FortunesFavor>().FirstOrDefault()?.QueueFree(); break;
			case "haste": GetChildren().OfType<Haste>().FirstOrDefault()?.QueueFree(); break;
		}
	}

	public List<LevelUpOption> GetLevelUpOptions(int maxOptions = 3)
	{
		bool loadoutFull = equippedSpells.Count >= MaxSpellSlots;
		var baselineElementCounts = GetElementInstanceCounts();
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		var candidates = new List<LevelUpOption>();
		var weights = new List<float>();
		foreach (var template in spellCatalog.Values)
		{
			if (template == null || string.IsNullOrWhiteSpace(template.Id))
				continue;

			SpellData equipped = equippedSpells.FirstOrDefault(s => s != null && s.Id.Equals(template.Id, StringComparison.OrdinalIgnoreCase));
			if (equipped == null)
			{
				if (!GlobalStatsManager.IsSpellUnlockedForLevelUp(saveManager?.Data, template.Id))
					continue;

				var option = new LevelUpOption
				{
					SpellId = template.Id,
					DisplayName = template.Name,
					Description = template.Description,
					NextLevel = 1,
					IsNewUnlock = true,
					IsPassive = template.IsPassive,
					RequiresSlotSwap = loadoutFull,
					Icon = template.Icon
				};
				ApplyElementPreview(option, template, baselineElementCounts, isNewUnlock: true);
				candidates.Add(option);
				weights.Add(GetOfferWeight(option, loadoutFull));
				continue;
			}

			if (equipped.CurrentLevel < equipped.MaxLevel)
			{
				int nextLevel = equipped.CurrentLevel + 1;
				var option = new LevelUpOption
				{
					SpellId = equipped.Id,
					DisplayName = equipped.Name,
					Description = equipped.Description,
					UpgradeSummary = BuildSpellUpgradeSummary(equipped, nextLevel),
					NextLevel = nextLevel,
					IsNewUnlock = false,
					IsPassive = equipped.IsPassive,
					Icon = equipped.Icon
				};
				ApplyElementPreview(option, equipped, baselineElementCounts, isNewUnlock: false);
				candidates.Add(option);
				weights.Add(GetOfferWeight(option, loadoutFull));
			}
		}

		var picked = new List<LevelUpOption>();
		var pool = new List<LevelUpOption>(candidates);
		var poolWeights = new List<float>(weights);
		while (picked.Count < maxOptions && pool.Count > 0)
		{
			int idx = PickWeightedIndex(poolWeights);
			picked.Add(pool[idx]);
			pool.RemoveAt(idx);
			poolWeights.RemoveAt(idx);
		}

		// Guarantee at least one "level up an owned spell" option when one is available (issue #2),
		// so the player isn't only ever offered brand-new spells while they still have room to grow.
		bool hasUpgradeOption = picked.Any(o => !o.IsNewUnlock);
		if (!hasUpgradeOption)
		{
			var upgradeCandidate = candidates.FirstOrDefault(o => !o.IsNewUnlock && !picked.Contains(o));
			if (upgradeCandidate != null)
			{
				if (picked.Count >= maxOptions && picked.Count > 0)
				{
					var toReplace = picked.LastOrDefault(o => o.IsNewUnlock) ?? picked[picked.Count - 1];
					picked.Remove(toReplace);
				}
				picked.Add(upgradeCandidate);
			}
		}

		return picked;
	}

	private static string BuildSpellUpgradeSummary(SpellData spell, int nextLevel)
	{
		if (spell == null || spell.LevelUpgrades == null)
			return string.Empty;

		var parts = new List<string>();
		foreach (SpellLevelUpgrade upgrade in spell.LevelUpgrades.Where(u => u != null && u.Level == nextLevel))
		{
			if (upgrade.DamageBonus != 0)
				parts.Add($"Damage {FormatSigned(upgrade.DamageBonus)}");
			if (MathF.Abs(upgrade.CooldownBonus) > 0.001f)
				parts.Add($"Cooldown {FormatSigned(upgrade.CooldownBonus)}s");
			if (upgrade.ProjectileCountBonus != 0)
				parts.Add($"Projectiles {FormatSigned(upgrade.ProjectileCountBonus)}");
			if (MathF.Abs(upgrade.RangeBonus) > 0.001f)
				parts.Add($"Range {FormatSigned(upgrade.RangeBonus)}");
			if (upgrade.Effect != SpellEffect.None && MathF.Abs(upgrade.EffectValue) > 0.001f)
				parts.Add(FormatSpellEffectUpgrade(upgrade.Effect, upgrade.EffectValue));
		}

		return parts.Count > 0 ? string.Join("   ", parts) : "Improves spell scaling";
	}

	private static string FormatSigned(int value) => value > 0 ? $"+{value}" : value.ToString();
	private static string FormatSigned(float value) => value > 0 ? $"+{value:0.##}" : value.ToString("0.##");

	private static string FormatSpellEffectUpgrade(SpellEffect effect, float value)
	{
		return effect switch
		{
			SpellEffect.Pierce => $"Pierce {FormatSigned(Mathf.RoundToInt(value))}",
			SpellEffect.Chain => $"Chain {FormatSigned(value)}",
			SpellEffect.Freeze => $"Freeze {FormatSigned(value)}s",
			SpellEffect.Burn => $"Burn {FormatSigned(value)}",
			SpellEffect.Knockback => $"Knockback {FormatSigned(value)}",
			SpellEffect.CritChance => $"Crit chance {FormatSigned(value * 100f)}%",
			SpellEffect.AreaSize => $"Area {FormatSigned(value)}",
			SpellEffect.ProjectileSpeed => $"Projectile speed {FormatSigned(value)}",
			_ => $"{effect} {FormatSigned(value)}"
		};
	}

	private float GetOfferWeight(LevelUpOption option, bool loadoutFull)
	{
		float baseWeight = option.IsNewUnlock ? NewUnlockOfferWeight : UpgradeOfferWeight;
		if (loadoutFull)
		{
			// Heavily skew toward leveling up existing spells once the loadout is full, while still
			// allowing new-spell offers so the player can choose to swap one out (issue #2 / #10).
			baseWeight = option.IsNewUnlock ? baseWeight * 0.25f : baseWeight * 2.0f;
		}
		return MathF.Max(0.01f, baseWeight);
	}

	private int PickWeightedIndex(List<float> weights)
	{
		if (weights == null || weights.Count == 0)
			return 0;

		float totalWeight = 0.0f;
		for (int i = 0; i < weights.Count; i++)
		{
			totalWeight += MathF.Max(0.0f, weights[i]);
		}

		if (totalWeight <= 0.0f)
			return levelUpRng.RandiRange(0, weights.Count - 1);

		float roll = levelUpRng.RandfRange(0.0f, totalWeight);
		float cumulative = 0.0f;
		for (int i = 0; i < weights.Count; i++)
		{
			cumulative += MathF.Max(0.0f, weights[i]);
			if (roll <= cumulative)
				return i;
		}

		return weights.Count - 1;
	}

	private SpellData ResolveSpellTemplate(string selectionId)
	{
		if (string.IsNullOrWhiteSpace(selectionId))
			return null;

		selectionId = selectionId.Trim();

		if (spellCatalog.TryGetValue(selectionId.ToLowerInvariant(), out var byId))
			return byId;

		string normalized = selectionId.Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
		foreach (var spell in spellCatalog.Values)
		{
			if (spell == null) continue;
			string spellIdNormalized = spell.Id.Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
			string spellNameNormalized = spell.Name.Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
			if (normalized == spellIdNormalized || normalized == spellNameNormalized)
				return spell;
		}

		if (normalized == "magicmissile") return spellCatalog.GetValueOrDefault("magic_missile");
		if (normalized == "arcaneexplosion") return spellCatalog.GetValueOrDefault("arcane_explosion");
		if (normalized == "spiritualweapon") return spellCatalog.GetValueOrDefault("spiritual_weapon");

		return null;
	}

	private void RefreshPersistentSpellInstance(SpellData spell)
	{
		if (spell == null) return;

		if (spell.Id.Equals("arcane_explosion", StringComparison.OrdinalIgnoreCase))
		{
			var existing = GetChildren().OfType<ArcaneExplosion>().FirstOrDefault();
			if (existing != null)
			{
				existing.SpellData = spell;
				existing.SetSpellLevel(spell.CurrentLevel);
			}
		}

		if (spell.Id.Equals("spiritual_weapon", StringComparison.OrdinalIgnoreCase))
		{
			var existing = GetChildren().OfType<SpiritualWeapon>().FirstOrDefault();
			if (existing != null)
			{
				existing.SpellData = spell;
				existing.SetSpellLevel(spell.CurrentLevel);
			}
		}

		// Defensive/passive spells (issue #13/#22): create-or-update their persistent child instance.
		switch (spell.Id.ToLowerInvariant())
		{
			case "aegis_ward": RefreshPassiveSpellInstance<AegisWard>(spell); break;
			case "thornmail_barrier": RefreshPassiveSpellInstance<ThornmailBarrier>(spell); break;
			case "frozen_bulwark": RefreshPassiveSpellInstance<FrozenBulwark>(spell); break;
			case "stormguard_aura": RefreshPassiveSpellInstance<StormguardAura>(spell); break;
			case "venom_cloak": RefreshPassiveSpellInstance<VenomCloak>(spell); break;
			case "guardian_vines": RefreshPassiveSpellInstance<GuardianVines>(spell); break;
			case "tidal_barrier": RefreshPassiveSpellInstance<TidalBarrier>(spell); break;
			case "stone_bulwark": RefreshPassiveSpellInstance<StoneBulwark>(spell); break;
			case "blur": RefreshPassiveSpellInstance<Blur>(spell); break;
			case "fortunes_favor": RefreshPassiveSpellInstance<FortunesFavor>(spell); break;
			case "haste": RefreshPassiveSpellInstance<Haste>(spell); break;
		}
	}

	private void RefreshPassiveSpellInstance<T>(SpellData spell) where T : PassiveSpellEffect, new()
	{
		var existing = GetChildren().OfType<T>().FirstOrDefault();
		if (existing == null)
		{
			existing = new T();
			AddChild(existing);
		}
		existing.SpellData = spell;
		existing.SetSpellLevel(spell.CurrentLevel);
		ApplyLegendaryVisual(existing, spell);
	}

	private void OnBodyEntered(Node body)
	{
		if (body.IsInGroup("enemies"))
		{
			overlappingEnemies.Add(body);
			// Optionally, apply damage immediately
			if (!enemyDamageCooldowns.ContainsKey(body) || enemyDamageCooldowns[body] >= DamageCooldownSeconds)
			{
				TakeDamage(1);
				enemyDamageCooldowns[body] = 0f;
			}
		}
	}

	private void OnBodyExited(Node body)
	{
		if (body.IsInGroup("enemies"))
		{
			overlappingEnemies.Remove(body);
			enemyDamageCooldowns.Remove(body);
		}
	}
}
