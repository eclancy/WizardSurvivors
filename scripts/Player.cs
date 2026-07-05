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




	// Track fire timers by spell id.
	public Dictionary<string, float> spellFireTimers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
	private Vector2 _velocity = Vector2.Zero;

	// Runtime spell instances equipped by the player.
	public List<SpellData> equippedSpells = new List<SpellData>();
	private Dictionary<string, SpellData> spellCatalog = new Dictionary<string, SpellData>(StringComparer.OrdinalIgnoreCase);
	private RandomNumberGenerator levelUpRng = new RandomNumberGenerator();
	public bool IsDead { get; private set; } = false;

	public override void _Ready()
	{
		AddToGroup("player");
		_velocity = Vector2.Zero;
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
	}

	private static SpellData CreateFallbackSpellData(string id, string name, int baseDamage, float baseCooldown, int baseProjectileCount, float baseRange, string description)
	{
		return new SpellData
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
	}

	public void TakeDamage(int amount)
	{
		if (IsDead)
			return;

		CurrentHP = Math.Max(0, CurrentHP - amount);
		if (hpBar != null)
			hpBar.Value = CurrentHP;
		if (CurrentHP <= 0)
		{
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

			float interval = spell.GetCooldownAtLevel(spell.CurrentLevel);
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
								var missile = MagicMissileScene.Instantiate<Area2D>();
								missile.Position = GlobalPosition;
								var script = missile as MagicMissile;
								if (script != null)
								{
									script.SpellData = spell;
									script.SetSpellLevel(spell.CurrentLevel);
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
							existing.SetSpellLevel(spell.CurrentLevel);
						}
					}

				}
				else if (spell.Id.Equals("spiritual_weapon", StringComparison.OrdinalIgnoreCase))
				{
					GD.Print("Firing Spiritual Weapon");

					// Ensure only one SpiritualWeapon follows this player. If not present, create and attach to player.
					bool hasSpiritualWeapon = GetChildren().OfType<Node>().Any(n => n is SpiritualWeapon);
					if (!hasSpiritualWeapon)
					{
						var spiritualWeapon = SpiritualWeaponScene.Instantiate<Node2D>();
						// Attach to player so it follows automatically; set local position to origin
						spiritualWeapon.Position = Vector2.Zero;
						var script = spiritualWeapon as SpiritualWeapon;
						if (script != null)
						{
							script.SpellData = spell;
							script.SetSpellLevel(spell.CurrentLevel);
							script.PlayerRef = this;
						}
						AddChild(spiritualWeapon);
					}
					else
					{
						var existing = GetChildren().OfType<SpiritualWeapon>().FirstOrDefault();
						if (existing != null)
						{
							existing.SpellData = spell;
							existing.SetSpellLevel(spell.CurrentLevel);
						}
					}

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

	public List<LevelUpOption> GetLevelUpOptions(int maxOptions = 3)
	{
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
					IsNewUnlock = true
				};
				candidates.Add(option);
				weights.Add(GetOfferWeight(option));
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
				candidates.Add(option);
				weights.Add(GetOfferWeight(option));
			}
		}

		if (candidates.Count <= maxOptions)
			return candidates;

		var picked = new List<LevelUpOption>(maxOptions);
		while (picked.Count < maxOptions && candidates.Count > 0)
		{
			int idx = PickWeightedIndex(weights);
			picked.Add(candidates[idx]);
			candidates.RemoveAt(idx);
			weights.RemoveAt(idx);
		}

		return picked;
	}

	private float GetOfferWeight(LevelUpOption option)
	{
		float baseWeight = option.IsNewUnlock ? NewUnlockOfferWeight : UpgradeOfferWeight;
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
