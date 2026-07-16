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
	[Export] public SpellData MagicMissileData { get; set; }
	[Export] public SpellData ArcaneExplosionData { get; set; }
	[Export] public SpellData SpiritualWeaponData { get; set; }
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
	private Dictionary<Node, float> enemyDamageCooldowns = new Dictionary<Node, float>();
	private const float DamageCooldownSeconds = 0.2f; // 12 frames at 60fps
	private HashSet<Node> overlappingEnemies = new HashSet<Node>();
	// Damage-absorbing shield pool (e.g. Aegis Ward), consumed before HP in TakeDamage().
	private int shieldPoints = 0;




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
	private int magnetBonus = 0;
	private int extraLives = 0;
	public int RerollsPerLevelUp { get; private set; } = 0;
	public int MagnetBonus => magnetBonus;

	// Maximum number of spells the player can have equipped at once (issue #10).
	public const int MaxSpellSlots = 6;

	public override void _Ready()
	{
		AddToGroup("player");
		_velocity = Vector2.Zero;
		ApplyArcaneUpgrades();
		spellFireTimers.Clear();
		XPToNextLevel = CalculateXPForLevel(CurrentLevel);
		InitializeSpellCatalog();
		TryAddOrLevelSpell("spiritual_weapon");
		levelUpRng.Randomize();

		// Create HP bar above player
		hpBar = new ProgressBar();
		hpBar.MinValue = 0;
		hpBar.MaxValue = MaxHP;
		hpBar.Value = CurrentHP;
		hpBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hpBar.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		hpBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		hpBar.Position = new Vector2(-32, -48); // Adjust for your sprite size
		hpBar.Size = new Vector2(64, 8);
		AddChild(hpBar);

		var hurtBox = GetNode<Area2D>("HurtBox");
		if (hurtBox != null)
		{
			hurtBox.Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
			hurtBox.Connect("body_exited", new Callable(this, nameof(OnBodyExited)));
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
			Description = description
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

	public void TakeDamage(int amount)
	{
		if (IsDead)
			return;

		if (amount > 0)
			EmitSignal(nameof(DamageTaken), amount);

		int mitigated = Math.Max(0, amount);
		if (shieldPoints > 0 && mitigated > 0)
		{
			int absorbed = Math.Min(shieldPoints, mitigated);
			shieldPoints -= absorbed;
			mitigated -= absorbed;
		}
		if (mitigated > 0)
		{
			int flatReduction = GetChildren().OfType<PassiveSpellEffect>().Sum(p => p.GetFlatDamageReduction());
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

		if (recoveryPerSecond > 0.0f && CurrentHP > 0 && CurrentHP < MaxHP)
		{
			recoveryAccumulator += recoveryPerSecond * (float)delta;
			while (recoveryAccumulator >= 1.0f)
			{
				Heal(1);
				recoveryAccumulator -= 1.0f;
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

			float interval = (spell.GetCooldownAtLevel(spell.CurrentLevel) * cooldownMultiplier) / attackSpeedMultiplier;
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

				}
				spellFireTimers[spell.Id] = 0f;
			}
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
		_velocity = input * Speed;
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

	public bool TryAddOrLevelSpell(string selectionId)
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
			equippedSpells.Add(runtimeSpell);
			spellFireTimers[runtimeSpell.Id] = 0f;
			GD.Print($"Equipped spell {runtimeSpell.Name} at level {runtimeSpell.CurrentLevel}");
			RefreshPersistentSpellInstance(runtimeSpell);
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
		}
	}

	public List<LevelUpOption> GetLevelUpOptions(int maxOptions = 3)
	{
		bool loadoutFull = equippedSpells.Count >= MaxSpellSlots;
		var baselineElementCounts = GetElementInstanceCounts();
		var candidates = new List<LevelUpOption>();
		var weights = new List<float>();
		foreach (var template in spellCatalog.Values)
		{
			if (template == null || string.IsNullOrWhiteSpace(template.Id))
				continue;

			SpellData equipped = equippedSpells.FirstOrDefault(s => s != null && s.Id.Equals(template.Id, StringComparison.OrdinalIgnoreCase));
			if (equipped == null)
			{
				var option = new LevelUpOption
				{
					SpellId = template.Id,
					DisplayName = template.Name,
					Description = template.Description,
					NextLevel = 1,
					IsNewUnlock = true,
					RequiresSlotSwap = loadoutFull
				};
				ApplyElementPreview(option, template, baselineElementCounts, isNewUnlock: true);
				candidates.Add(option);
				weights.Add(GetOfferWeight(option, loadoutFull));
				continue;
			}

			if (equipped.CurrentLevel < equipped.MaxLevel)
			{
				var option = new LevelUpOption
				{
					SpellId = equipped.Id,
					DisplayName = equipped.Name,
					Description = equipped.Description,
					NextLevel = equipped.CurrentLevel + 1,
					IsNewUnlock = false
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
