using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WizardSurvivors.scripts;

public partial class Node2DGame : Node2D
{
	[Export] public int MaxEnemies { get; set; } = 100;
	[Export] public float TimerVictorySeconds { get; set; } = 900.0f;
	[Export] public float SpawnMinDistance { get; set; } = 250.0f;
	[Export] public float SpawnMaxDistance { get; set; } = 800.0f;
	[Export] public float SpawnMinEnemySeparation { get; set; } = 96.0f;
	[Export] public int SpawnPositionRetries { get; set; } = 8;
	[Export] public float SpawnBaseInterval { get; set; } = 2.0f;
	[Export] public float SpawnMinInterval { get; set; } = 0.2f;
	[Export] public float SpawnIntervalReductionPerMinute { get; set; } = 0.18f;
	[Export] public int SpawnBaseHealth { get; set; } = 20;
	[Export] public int SpawnHealthPerMinute { get; set; } = 18;
	[Export] public float ForestHalfHeight { get; set; } = 260.0f;
	[Export] public float CastleHalfWidth { get; set; } = 420.0f;
	[Export] public float CastleHalfHeight { get; set; } = 1400.0f;
	[Export] public float RuinsHalfSize { get; set; } = 1200.0f;
	[Export] public int BaseArcaneReward { get; set; } = 20;
	[Export] public int ArcanePerMinuteSurvived { get; set; } = 8;
	[Export] public int ArcanePerPlayerLevel { get; set; } = 2;

	private Player? player;
	private CanvasLayer? levelUpMenu;
	private CanvasLayer? escapeMenu;
	private RichTextLabel? escapeDetailText;
	private Label? escapeDetailTitle;
	private GridContainer? elementHudGrid;
	private float fireTimer = 0f;
	private float fireInterval = 1f;
	private float spawnTimer = 0f;
	private float spawnInterval = 2f;
	private float spawnHealth = 20f;
	private float timeElapsed = 0f;
	private int totalEnemiesSpawned = 0;
	private bool tookDamageBeforeFiveMinutes = false;
	private bool runFinished = false;
	private int rerollsRemainingForCurrentLevelUp = 0;
	private Vector2 stageOrigin = Vector2.Zero;
	private RandomNumberGenerator spawnRng = new RandomNumberGenerator();

	private PackedScene magicMissileScene = ResourceLoader.Load<PackedScene>("res://scenes/MagicMissile.tscn");
	private PackedScene enemyScene = ResourceLoader.Load<PackedScene>("res://scenes/enemy.tscn");
	private PackedScene fastEnemyScene = ResourceLoader.Load<PackedScene>("res://scenes/FastEnemy.tscn");
	private PackedScene slowEnemyScene = ResourceLoader.Load<PackedScene>("res://scenes/SlowEnemy.tscn");
	private PackedScene tankEnemyScene = ResourceLoader.Load<PackedScene>("res://scenes/TankEnemy.tscn");
	private PackedScene levelupMenuScene = ResourceLoader.Load<PackedScene>("res://scenes/LevelUpMenu.tscn");
	private PackedScene gameOverScene = ResourceLoader.Load<PackedScene>("res://scenes/GameOverScreen.tscn");

	private static readonly (string Id, string Path)[] SpellbookResources = new[]
	{
		("magic_missile", "res://SpellData.tres"),
		("arcane_explosion", "res://SpellData_ArcaneExplosion.tres"),
		("spiritual_weapon", "res://SpellData_SpiritualWeapon.tres"),
		("fireball", "res://SpellData_Fireball.tres"),
		("frost_shard", "res://SpellData_FrostShard.tres"),
		("shadow_bolt", "res://SpellData_ShadowBolt.tres"),
		("thorn_vine", "res://SpellData_ThornVine.tres"),
		("gale_blade", "res://SpellData_GaleBlade.tres"),
		("solar_flare", "res://SpellData_SolarFlare.tres"),
		("molten_shard", "res://SpellData_MoltenShard.tres"),
		("chain_lightning", "res://SpellData_ChainLightning.tres"),
		("toxic_spore_burst", "res://SpellData_ToxicSporeBurst.tres"),
		("obsidian_spike", "res://SpellData_ObsidianSpike.tres"),
		("cyclone_slash", "res://SpellData_CycloneSlash.tres"),
		("void_lance", "res://SpellData_VoidLance.tres"),
		("glacial_spike", "res://SpellData_GlacialSpike.tres"),
		("black_tentacles", "res://SpellData_BlackTentacles.tres"),
		("cone_of_cold", "res://SpellData_ConeOfCold.tres"),
		("scorching_ray", "res://SpellData_ScorchingRay.tres"),
		("meteor_swarm", "res://SpellData_MeteorSwarm.tres")
	};

	private static readonly (string Id, string DisplayName, string Description, string Elements)[] PassiveSpellbookEntries = new[]
	{
		("aegis_ward", "Aegis Ward", "Periodically grants an absorbing shield.", "Metal, Light"),
		("thornmail_barrier", "Thornmail Barrier", "Retaliates against nearby enemies when hit.", "Earth, Grass"),
		("frozen_bulwark", "Frozen Bulwark", "Chance to freeze nearby attackers when hit.", "Ice x2"),
		("stormguard_aura", "Stormguard Aura", "Strikes the nearest enemy with lightning when hit.", "Lightning, Metal"),
		("venom_cloak", "Venom Cloak", "Periodically poisons nearby enemies.", "Poison, Darkness"),
		("guardian_vines", "Guardian Vines", "Periodically roots nearby enemies.", "Grass x2"),
		("tidal_barrier", "Tidal Barrier", "Periodically knocks back and slows nearby enemies.", "Water, Wind"),
		("stone_bulwark", "Stone Bulwark", "Passively reduces incoming damage.", "Earth, Metal"),
		("blur", "Blur", "Chance to avoid incoming hits entirely.", "Arcane, Wind"),
		("fortunes_favor", "Fortune's Favor", "Passively boosts Luck.", "Arcane, Light"),
		("haste", "Haste", "Periodically grants attack-speed and move-speed surges.", "Wind, Lightning")
	};

	public override void _Ready()
	{
		spawnRng.Randomize();
		player = GetNode<Player>("CharacterBody2D"); // Strongly typed YES
		stageOrigin = player?.GlobalPosition ?? Vector2.Zero;
		player?.Connect("XpGained", new Callable(this, nameof(OnPlayerXpGained)));
		player?.Connect("LevelGained", new Callable(this, nameof(OnPlayerLevelGained)));
		player?.Connect("Died", new Callable(this, nameof(OnPlayerDied)));
		player?.Connect("DamageTaken", new Callable(this, nameof(OnPlayerDamageTaken)));

		if (HasNode("LevelUpMenu"))
			levelUpMenu = GetNode<CanvasLayer>("LevelUpMenu");
		levelUpMenu?.Hide();
		// Connect to WeaponSelected signal if menu exists at startup
		if (levelUpMenu != null)
		{
			var menuScript = levelUpMenu as Node;
			menuScript?.Connect("WeaponSelected", new Callable(this, nameof(OnWeaponSelected)));
			menuScript?.Connect("RerollRequested", new Callable(this, nameof(OnRerollRequested)));
			menuScript?.Connect("SwapRequested", new Callable(this, nameof(OnSwapRequested)));
			menuScript?.Connect("RemoveRequested", new Callable(this, nameof(OnRemoveRequested)));
			menuScript?.Connect("SkipRequested", new Callable(this, nameof(OnSkipRequested)));
		}

		var uiOverlay = GetNodeOrNull<CanvasLayer>("UIOverlay");
		if (uiOverlay != null)
		{
			elementHudGrid = new GridContainer
			{
				Name = "ElementHudGrid",
				Position = new Vector2(7, 36),
				Columns = 4,
				CustomMinimumSize = new Vector2(430, 0)
			};
			elementHudGrid.AddThemeConstantOverride("h_separation", 4);
			elementHudGrid.AddThemeConstantOverride("v_separation", 4);
			uiOverlay.AddChild(elementHudGrid);
		}
		RefreshElementHud();
		EnsureEscapeMenuUi();
		UpdateSpawnScaling();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (runFinished)
			return;

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.Escape)
		{
			ToggleEscapeMenu();
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta)
	{
		if (runFinished)
			return;

		float d = (float)delta;
		fireTimer += d;
		spawnTimer += d;
		timeElapsed += d;
		UpdateSpawnScaling();
		ClampPlayerToStageBounds();
		if (TimerVictorySeconds > 0f && timeElapsed >= TimerVictorySeconds)
		{
			FinishRunAndReward(RunOutcome.Victory);
			return;
		}

		if (fireTimer >= fireInterval)
		{
			// TODO: call fire logic
			fireTimer = 0f;
		}
		if (spawnTimer >= spawnInterval)
		{
			var currentEnemies = GetTree().GetNodesInGroup("enemies");
			if (currentEnemies.Count < MaxEnemies)
			{
				SpawnEnemy();
			}
			spawnTimer = 0f;
		}

		var background = GetNode<TextureRect>("CanvasLayer/Background");
		var camera = player.GetNode<Camera2D>("Camera2D");
		if (background.Material is ShaderMaterial material && camera != null)
		{
			var textureSize = background.Texture.GetSize();
			Vector2 offset = camera.GlobalPosition / textureSize;
			material.SetShaderParameter("scroll_offset", offset);
		}
	}

	private void OnPlayerXpGained(int amount)
	{
		GD.Print($"Player XP Gained: {amount}");
		var xpCounter = GetNode<ProgressBar>("UIOverlay/XPCounter");
		if (xpCounter != null)
		{
			xpCounter.Value = amount; // or player.CurrentXP if you have access, which we do
			xpCounter.MaxValue = player.XPToNextLevel;
		}
	}

	private void OnPlayerLevelGained()
	{
		GD.Print("Player Level Gained!");
		// Show the LevelUpMenu scene
		if (levelupMenuScene != null)
		{
			levelUpMenu = levelupMenuScene.Instantiate<CanvasLayer>();
			AddChild(levelUpMenu);
			// Connect to WeaponSelected signal
			var menuScript = levelUpMenu as Node;
			menuScript?.Connect("WeaponSelected", new Callable(this, nameof(OnWeaponSelected)));
			menuScript?.Connect("RerollRequested", new Callable(this, nameof(OnRerollRequested)));
			menuScript?.Connect("SwapRequested", new Callable(this, nameof(OnSwapRequested)));
			menuScript?.Connect("RemoveRequested", new Callable(this, nameof(OnRemoveRequested)));
			menuScript?.Connect("RemoveRequested", new Callable(this, nameof(OnRemoveRequested)));
			menuScript?.Connect("SkipRequested", new Callable(this, nameof(OnSkipRequested)));
		}
		if (levelUpMenu != null)
		{
			// Show the menu first
			levelUpMenu.Show();
			rerollsRemainingForCurrentLevelUp = player?.RerollsPerLevelUp ?? 0;
			if (levelUpMenu is LevelUpMenu typedMenu && player != null)
			{
				typedMenu.SetOptions(player.GetLevelUpOptions(), rerollsRemainingForCurrentLevelUp, BuildEquippedInfo(), BuildBaselineElementCounts());
			}
			else
			{
				var menuScript = levelUpMenu as Godot.Node;
				var setOptionsMethod = menuScript?.GetType().GetMethod("SetOptions");
				setOptionsMethod?.Invoke(menuScript, null);
			}
			// Pause the game
			GetTree().Paused = true;
		}
	}

	private void OnRerollRequested()
	{
		if (player == null || levelUpMenu is not LevelUpMenu typedMenu)
			return;

		if (rerollsRemainingForCurrentLevelUp <= 0)
			return;

		rerollsRemainingForCurrentLevelUp--;
		typedMenu.SetOptions(player.GetLevelUpOptions(), rerollsRemainingForCurrentLevelUp, BuildEquippedInfo(), BuildBaselineElementCounts());
	}

	// Projects the player's currently-equipped spells into the lightweight EquippedSpellInfo shape
	// LevelUpMenu needs (swap-selection prompt + elemental tag section's passive-highlight logic).
	private List<EquippedSpellInfo> BuildEquippedInfo()
	{
		if (player == null)
			return new List<EquippedSpellInfo>();

		return player.GetEquippedSpells()
			.Where(s => s != null)
			.Select(s => new EquippedSpellInfo
			{
				Id = s.Id,
				DisplayName = s.Name,
				CurrentLevel = s.CurrentLevel,
				ElementWeights = s.GetElementWeights().ToDictionary(p => p.Key.ToString(), p => p.Value),
				IsPassive = s.IsPassive
			})
			.ToList();
	}

	// The player's true current element instance counts (issue #16), keyed by Element name, used as
	// the baseline for LevelUpMenu's elemental tag section.
	private Dictionary<string, int> BuildBaselineElementCounts()
	{
		if (player == null)
			return new Dictionary<string, int>();

		return player.GetElementInstanceCounts().ToDictionary(p => p.Key.ToString(), p => p.Value);
	}


	private void OnWeaponSelected(string weaponId)
	{
		if (player == null)
			return;

		bool changed = player.TryAddOrLevelSpell(weaponId);
		if (!changed)
		{
			GD.PrintErr($"Could not add or level spell for selection '{weaponId}'.");
		}

		RefreshElementHud();
		CloseLevelUpMenu();
	}

	private void OnSwapRequested(string newSpellId, string removedSpellId)
	{
		if (player == null)
			return;

		if (!player.RemoveEquippedSpell(removedSpellId))
		{
			GD.PrintErr($"Could not remove spell '{removedSpellId}' for swap.");
		}

		if (!player.TryAddOrLevelSpell(newSpellId))
		{
			GD.PrintErr($"Could not add spell '{newSpellId}' after swap.");
		}

		RefreshElementHud();
		CloseLevelUpMenu();
	}

	private void OnRemoveRequested(string removedSpellId)
	{
		if (player == null)
			return;

		if (!player.RemoveEquippedSpell(removedSpellId))
		{
			GD.PrintErr($"Could not remove spell '{removedSpellId}'.");
		}

		RefreshElementHud();
		CloseLevelUpMenu();
	}

	private void OnSkipRequested()
	{
		CloseLevelUpMenu();
	}

	private void EnsureEscapeMenuUi()
	{
		if (escapeMenu != null)
			return;

		escapeMenu = new CanvasLayer
		{
			Name = "EscapeMenu",
			Layer = 100,
			ProcessMode = ProcessModeEnum.Always,
			Visible = false
		};
		AddChild(escapeMenu);

		var root = new Control
		{
			Name = "EscapeRoot",
			MouseFilter = Control.MouseFilterEnum.Stop,
			ProcessMode = ProcessModeEnum.Always
		};
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		escapeMenu.AddChild(root);

		var dim = new ColorRect
		{
			Color = new Color(0.02f, 0.02f, 0.025f, 0.82f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(dim);

		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(900, 700),
			ProcessMode = ProcessModeEnum.Always
		};
		panel.AnchorLeft = 0.5f;
		panel.AnchorTop = 0.5f;
		panel.AnchorRight = 0.5f;
		panel.AnchorBottom = 0.5f;
		panel.OffsetLeft = -450;
		panel.OffsetTop = -350;
		panel.OffsetRight = 450;
		panel.OffsetBottom = 350;
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.10f, 0.10f, 0.13f, 0.98f);
		style.BorderColor = new Color(0.36f, 0.40f, 0.52f, 0.9f);
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(6);
		panel.AddThemeStyleboxOverride("panel", style);
		root.AddChild(panel);

		var outer = new HBoxContainer();
		outer.AddThemeConstantOverride("separation", 18);
		panel.AddChild(outer);

		var nav = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(220, 0),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		nav.AddThemeConstantOverride("separation", 8);
		outer.AddChild(nav);

		var title = new Label
		{
			Text = "Paused",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 30);
		nav.AddChild(title);

		nav.AddChild(MakeEscapeButton("Resume", OnEscapeResumePressed));
		nav.AddChild(MakeEscapeButton("Restart Run", OnEscapeRestartPressed));
		nav.AddChild(MakeEscapeButton("Quit", OnEscapeQuitPressed));
		nav.AddChild(MakeEscapeButton("Run Details", ShowEscapeRunOverview));
		nav.AddChild(MakeEscapeButton("Spellbook Pool", ShowEscapeSpellbook));
		nav.AddChild(MakeEscapeButton("Achievements", ShowEscapeAchievements));

		var details = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		details.AddThemeConstantOverride("separation", 8);
		outer.AddChild(details);

		escapeDetailTitle = new Label
		{
			Text = "Run Details",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		escapeDetailTitle.AddThemeFontSizeOverride("font_size", 24);
		details.AddChild(escapeDetailTitle);

		escapeDetailText = new RichTextLabel
		{
			FitContent = false,
			ScrollActive = true,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			BbcodeEnabled = false
		};
		escapeDetailText.AddThemeFontSizeOverride("normal_font_size", 15);
		details.AddChild(escapeDetailText);
	}

	private Button MakeEscapeButton(string text, Action pressed)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(200, 44),
			ProcessMode = ProcessModeEnum.Always
		};
		button.AddThemeFontSizeOverride("font_size", 18);
		button.Pressed += pressed;
		return button;
	}

	private void ToggleEscapeMenu()
	{
		EnsureEscapeMenuUi();
		if (escapeMenu == null)
			return;

		if (escapeMenu.Visible)
		{
			OnEscapeResumePressed();
			return;
		}

		if (levelUpMenu != null && levelUpMenu.Visible)
			return;

		ShowEscapeRunOverview();
		escapeMenu.Show();
		GetTree().Paused = true;
	}

	private void OnEscapeResumePressed()
	{
		escapeMenu?.Hide();
		GetTree().Paused = false;
	}

	private void OnEscapeRestartPressed()
	{
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://scenes/node_2d_game.tscn");
	}

	private void OnEscapeQuitPressed()
	{
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
	}

	private void SetEscapeDetail(string title, string text)
	{
		if (escapeDetailTitle != null)
			escapeDetailTitle.Text = title;
		if (escapeDetailText != null)
			escapeDetailText.Text = text;
	}

	private void ShowEscapeRunOverview()
	{
		var builder = new StringBuilder();
		builder.AppendLine($"Time: {FormatTime(timeElapsed)}   Level: {player?.CurrentLevel ?? 1}   XP: {player?.CurrentXP ?? 0}/{player?.XPToNextLevel ?? 0}");
		builder.AppendLine();
		AppendCharacterPassive(builder);
		builder.AppendLine();
		AppendEquippedWeapons(builder);
		builder.AppendLine();
		AppendEquippedPassives(builder);
		builder.AppendLine();
		AppendElementPassives(builder);
		SetEscapeDetail("Run Details", builder.ToString());
	}

	private void ShowEscapeSpellbook()
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		var builder = new StringBuilder();
		builder.AppendLine("Weapons in the level-up pool");
		builder.AppendLine();
		foreach (var (id, path) in SpellbookResources)
		{
			SpellData spell = ResourceLoader.Load<SpellData>(path);
			if (spell == null)
				continue;

			string status = GlobalStatsManager.IsSpellUnlockedForLevelUp(saveManager?.Data, id) ? "Available" : "Locked";
			builder.AppendLine($"[{status}] {spell.Name} - {FormatElementWeights(spell.GetElementWeights())}");
		}

		builder.AppendLine();
		builder.AppendLine("Passives in the level-up pool");
		builder.AppendLine();
		foreach (var passive in PassiveSpellbookEntries)
		{
			string status = GlobalStatsManager.IsSpellUnlockedForLevelUp(saveManager?.Data, passive.Id) ? "Available" : "Locked";
			builder.AppendLine($"[{status}] {passive.DisplayName} - {passive.Elements}");
			builder.AppendLine($"  {passive.Description}");
		}

		SetEscapeDetail("Spellbook Pool", builder.ToString());
	}

	private void ShowEscapeAchievements()
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		var builder = new StringBuilder();
		foreach (AchievementDefinition achievement in AchievementDefinitions.All)
		{
			bool unlocked = saveManager?.Data.UnlockedAchievementIds.Any(id => id.Equals(achievement.Id, StringComparison.OrdinalIgnoreCase)) ?? false;
			builder.AppendLine($"[{(unlocked ? "Complete" : "In Progress")}] {achievement.DisplayName}");
			builder.AppendLine($"  {achievement.Description}");
			builder.AppendLine($"  {achievement.RewardText}");
			builder.AppendLine();
		}

		SetEscapeDetail("Achievement Progress", builder.ToString());
	}

	private void AppendCharacterPassive(StringBuilder builder)
	{
		CharacterData character = CharacterRoster.GetByIndex(Global.SelectedCharacterIdx);
		builder.AppendLine("Character Passive");
		if (character == null)
		{
			builder.AppendLine("  Apprentice fallback: no character passive loaded.");
			return;
		}

		string passiveName = string.IsNullOrWhiteSpace(character.StartingPassiveName) ? "Passive" : character.StartingPassiveName;
		string passiveDescription = string.IsNullOrWhiteSpace(character.StartingPassiveDescription) ? "No passive description." : character.StartingPassiveDescription;
		builder.AppendLine($"  {character.Name}: {passiveName}");
		builder.AppendLine($"  {passiveDescription}");
	}

	private void AppendEquippedWeapons(StringBuilder builder)
	{
		builder.AppendLine("Weapons");
		var weapons = player?.GetEquippedSpells().Where(s => s != null && !s.IsPassive).ToList() ?? new List<SpellData>();
		if (weapons.Count == 0)
		{
			builder.AppendLine("  None");
			return;
		}

		foreach (SpellData spell in weapons)
			builder.AppendLine($"  {FormatSpellStats(spell)}");
	}

	private void AppendEquippedPassives(StringBuilder builder)
	{
		builder.AppendLine("Equipped Passives");
		var passives = player?.GetEquippedSpells().Where(s => s != null && s.IsPassive).ToList() ?? new List<SpellData>();
		if (passives.Count == 0)
		{
			builder.AppendLine("  None");
			return;
		}

		foreach (SpellData spell in passives)
			builder.AppendLine($"  {FormatSpellStats(spell)}");
	}

	private void AppendElementPassives(StringBuilder builder)
	{
		builder.AppendLine("Element Passives");
		if (player == null)
		{
			builder.AppendLine("  None");
			return;
		}

		foreach (var pair in player.GetElementInstanceCounts().OrderByDescending(p => p.Value).ThenBy(p => p.Key.ToString()))
		{
			int tier = player.GetElementTier(pair.Key);
			string status = tier > 0 ? $"Tier {tier}: {ElementPassiveDescriptions.GetEffectText(pair.Key, tier)}" : ElementPassiveDescriptions.GetEffectText(pair.Key, tier);
			builder.AppendLine($"  {pair.Key} {pair.Value}/6 - {status}");
		}
	}

	private string FormatSpellStats(SpellData spell)
	{
		return $"{spell.Name} Lv {spell.CurrentLevel}/{spell.MaxLevel} | Damage {spell.GetDamageAtLevel(spell.CurrentLevel)} | Cooldown {spell.GetCooldownAtLevel(spell.CurrentLevel):0.##}s | Projectiles {spell.GetProjectileCountAtLevel(spell.CurrentLevel)} | Range {spell.GetRangeAtLevel(spell.CurrentLevel):0} | {FormatElementWeights(spell.GetElementWeights())}";
	}

	private static string FormatElementWeights(Dictionary<Element, int> weights)
	{
		if (weights == null || weights.Count == 0)
			return "No elements";

		return string.Join(", ", weights.OrderBy(p => p.Key.ToString()).Select(p => p.Value > 1 ? $"{p.Key} x{p.Value}" : p.Key.ToString()));
	}

	private static string FormatTime(float seconds)
	{
		int totalSeconds = Mathf.FloorToInt(seconds);
		return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
	}

	private void CloseLevelUpMenu()
	{
		// Unpause the game and remove the menu
		GetTree().Paused = false;
		if (levelUpMenu != null)
		{
			levelUpMenu.QueueFree();
			levelUpMenu = null;
		}
	}

	private void RefreshElementHud()
	{
		if (elementHudGrid == null || player == null)
			return;

		foreach (Node child in elementHudGrid.GetChildren())
			child.QueueFree();

		var counts = player.GetElementInstanceCounts();
		foreach (var pair in counts
			.Where(kvp => kvp.Value > 0)
			.OrderByDescending(kvp => kvp.Value)
			.ThenBy(kvp => kvp.Key.ToString()))
		{
			elementHudGrid.AddChild(BuildElementHudBadge(pair.Key, pair.Value));
		}
	}

	private Control BuildElementHudBadge(Element element, int count)
	{
		Color baseColor = ElementColors.GetColor(element);
		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(102, 28),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};

		var style = new StyleBoxFlat();
		style.BgColor = new Color(baseColor.R, baseColor.G, baseColor.B, 0.88f);
		style.BorderColor = new Color(1f, 1f, 1f, 0.28f);
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(4);
		style.SetContentMarginAll(5);
		panel.AddThemeStyleboxOverride("panel", style);

		var label = new Label
		{
			Text = $"{element} {ElementPassiveDescriptions.GetProgressLabel(count)}",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", 12);
		label.AddThemeColorOverride("font_color", GetReadableTextColor(baseColor));
		panel.AddChild(label);
		return panel;
	}

	private static Color GetReadableTextColor(Color background)
	{
		float luminance = (background.R * 0.299f) + (background.G * 0.587f) + (background.B * 0.114f);
		return luminance > 0.62f ? new Color(0.06f, 0.06f, 0.07f) : Colors.White;
	}

	private void UpdateSpawnScaling()
	{
		float minutesElapsed = Mathf.Max(0.0f, timeElapsed / 60.0f);
		spawnInterval = Mathf.Max(SpawnMinInterval, SpawnBaseInterval - (minutesElapsed * SpawnIntervalReductionPerMinute));
		spawnHealth = SpawnBaseHealth + (minutesElapsed * SpawnHealthPerMinute);
	}

	private void SpawnEnemy()
	{
		var selection = SelectEnemyForCurrentStage();
		var enemy = selection.Scene.Instantiate<Node2D>();
		if (enemy is Enemy typedEnemy)
		{
			typedEnemy.Health = Mathf.RoundToInt(spawnHealth * selection.HealthMultiplier);
		}

		enemy.Position = FindSeparatedSpawnPosition();
		AddChild(enemy);
		totalEnemiesSpawned++;
	}

	private Vector2 FindSeparatedSpawnPosition()
	{
		if (player != null)
		{
			Vector2 bestCandidate = player.GlobalPosition;
			float bestDistance = -1.0f;
			int attempts = Math.Max(1, SpawnPositionRetries);
			for (int i = 0; i < attempts; i++)
			{
				Vector2 candidate = GetRandomSpawnPositionAroundPlayer();
				float nearestEnemyDistance = GetNearestEnemyDistance(candidate);
				if (nearestEnemyDistance >= SpawnMinEnemySeparation)
					return candidate;

				if (nearestEnemyDistance > bestDistance)
				{
					bestDistance = nearestEnemyDistance;
					bestCandidate = candidate;
				}
			}

			return bestCandidate;
		}

		var screenSize = GetViewportRect().Size;
		return new Vector2((float)spawnRng.Randf() * screenSize.X, -50);
	}

	private Vector2 GetRandomSpawnPositionAroundPlayer()
	{
		var angle = spawnRng.Randf() * (Mathf.Pi * 2.0f);
		var radius = spawnRng.RandfRange(SpawnMinDistance, SpawnMaxDistance);
		return ClampPositionToStageBounds(player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
	}

	private float GetNearestEnemyDistance(Vector2 position)
	{
		if (SpawnMinEnemySeparation <= 0.0f)
			return float.MaxValue;

		float nearest = float.MaxValue;
		foreach (Node2D enemy in GetTree().GetNodesInGroup("enemies").OfType<Node2D>())
		{
			if (!IsInstanceValid(enemy))
				continue;

			nearest = Math.Min(nearest, position.DistanceTo(enemy.GlobalPosition));
		}

		return nearest;
	}

	private (PackedScene Scene, float HealthMultiplier) SelectEnemyForCurrentStage()
	{
		float minutesElapsed = timeElapsed / 60.0f;
		float roll = spawnRng.Randf();
		return Global.SelectedStageIdx switch
		{
			1 => roll < 0.50f ? (fastEnemyScene, 0.75f) : roll < 0.75f ? (enemyScene, 1.0f) : roll < 0.90f ? (slowEnemyScene, 1.4f) : (tankEnemyScene, 2.2f),
			2 => roll < 0.45f ? (tankEnemyScene, 2.2f) : roll < 0.70f ? (slowEnemyScene, 1.4f) : roll < 0.90f ? (enemyScene, 1.0f) : (fastEnemyScene, 0.75f),
			_ => minutesElapsed >= 5.0f
				? roll < 0.20f ? (fastEnemyScene, 0.75f) : roll < 0.35f ? (slowEnemyScene, 1.4f) : roll < 0.45f ? (tankEnemyScene, 2.2f) : (enemyScene, 1.0f)
				: roll < 0.78f ? (enemyScene, 1.0f) : roll < 0.92f ? (fastEnemyScene, 0.75f) : (slowEnemyScene, 1.4f)
		};
	}

	private void ClampPlayerToStageBounds()
	{
		if (player == null)
			return;

		player.GlobalPosition = ClampPositionToStageBounds(player.GlobalPosition);
	}

	private Vector2 ClampPositionToStageBounds(Vector2 position)
	{
		return Global.SelectedStageIdx switch
		{
			0 => new Vector2(position.X, Mathf.Clamp(position.Y, stageOrigin.Y - ForestHalfHeight, stageOrigin.Y + ForestHalfHeight)),
			1 => new Vector2(
				Mathf.Clamp(position.X, stageOrigin.X - CastleHalfWidth, stageOrigin.X + CastleHalfWidth),
				Mathf.Clamp(position.Y, stageOrigin.Y - CastleHalfHeight, stageOrigin.Y + CastleHalfHeight)),
			2 => new Vector2(
				Mathf.Clamp(position.X, stageOrigin.X - RuinsHalfSize, stageOrigin.X + RuinsHalfSize),
				Mathf.Clamp(position.Y, stageOrigin.Y - RuinsHalfSize, stageOrigin.Y + RuinsHalfSize)),
			_ => position
		};
	}

	private void OnPlayerDied()
	{
		FinishRunAndReward(RunOutcome.Defeat);
	}

	private void OnPlayerDamageTaken(int amount)
	{
		if (amount > 0 && timeElapsed <= 300f)
			tookDamageBeforeFiveMinutes = true;
	}

	public void FinishRunAndReward(RunOutcome outcome = RunOutcome.Defeat, string bossId = "")
	{
		if (runFinished)
			return;

		runFinished = true;
		RunResult result = BuildRunResult(outcome, bossId);

		int reward = CalculateArcaneReward();
		int totalCurrency = AwardArcaneEnergy(reward);
		GameStats.RecordRunResult(result);
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager != null && AchievementManager.ApplyRunAchievements(saveManager.Data, result))
			saveManager.SaveGame();
		ShowGameOver(result, reward, totalCurrency);
		GetTree().Paused = true;
	}

	private RunResult BuildRunResult(RunOutcome outcome, string bossId)
	{
		return new RunResult
		{
			Outcome = outcome,
			StageId = $"stage_{Global.SelectedStageIdx}",
			FinalPlayerLevel = player?.CurrentLevel ?? 1,
			TimeSurvived = timeElapsed,
			EnemiesKilled = CalculateKillsEstimate(),
			BossId = bossId ?? string.Empty,
			TookDamageBeforeFiveMinutes = tookDamageBeforeFiveMinutes,
			ElementCounts = BuildElementCountSnapshot(),
			EquippedSpells = BuildSpellSnapshot()
		};
	}

	private Dictionary<string, int> BuildElementCountSnapshot()
	{
		if (player == null)
			return new Dictionary<string, int>();

		return player.GetElementInstanceCounts().ToDictionary(p => p.Key.ToString(), p => p.Value);
	}

	private List<RunSpellSnapshot> BuildSpellSnapshot()
	{
		if (player == null)
			return new List<RunSpellSnapshot>();

		return player.GetEquippedSpells()
			.Where(s => s != null)
			.Select(s => new RunSpellSnapshot
			{
				Id = s.Id,
				DisplayName = s.Name,
				Level = s.CurrentLevel,
				IsLegendary = s.IsLegendary
			})
			.ToList();
	}

	private int CalculateKillsEstimate()
	{
		int activeEnemies = GetTree().GetNodesInGroup("enemies").Count;
		return Math.Max(0, totalEnemiesSpawned - activeEnemies);
	}

	private int CalculateArcaneReward()
	{
		int minutesSurvived = Mathf.FloorToInt(timeElapsed / 60.0f);
		int playerLevel = player?.CurrentLevel ?? 1;
		int baseReward = BaseArcaneReward + (minutesSurvived * ArcanePerMinuteSurvived) + (playerLevel * ArcanePerPlayerLevel);

		float rewardMultiplier = 1.0f;
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager != null && saveManager.Data.ArcaneUpgradeLevels.TryGetValue("arcane_resonance", out int resonanceLevel))
		{
			rewardMultiplier += resonanceLevel * 0.10f;
		}

		if (saveManager != null && saveManager.Data.ArcaneUpgradeLevels.TryGetValue("greed", out int greedLevel))
		{
			rewardMultiplier += greedLevel * 0.10f;
		}

		int computed = Mathf.RoundToInt(baseReward * rewardMultiplier);
		return Math.Max(1, computed);
	}

	private int AwardArcaneEnergy(int amount)
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null)
		{
			GD.PrintErr("SaveManager autoload not found. Arcane Energy reward was not persisted.");
			return 0;
		}

		saveManager.Data.TotalCurrency += amount;
		saveManager.SaveGame();
		return saveManager.Data.TotalCurrency;
	}

	private void ShowGameOver(RunResult result, int reward, int totalCurrency)
	{
		if (gameOverScene == null)
		{
			GD.PrintErr("GameOverScreen scene could not be loaded.");
			return;
		}

		var overlay = gameOverScene.Instantiate<CanvasLayer>();
		AddChild(overlay);

		if (overlay is GameOverScreen gameOver)
		{
			gameOver.SetRunResult(result);
			gameOver.SetArcaneReward(reward, totalCurrency);
		}
	}

	public void RespawnEnemy(Node enemy)
	{
		if (player == null) return;
		var newPos = FindSeparatedSpawnPosition();
		if (enemy is Enemy typedEnemy)
		{
			typedEnemy.ResetForRespawn(newPos, Mathf.RoundToInt(spawnHealth));
		}
		else if (enemy is Node2D n)
		{
			n.Position = newPos;
		}
	}
}
