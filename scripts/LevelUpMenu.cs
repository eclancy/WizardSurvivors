using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

public partial class LevelUpMenu : CanvasLayer
{
	[Signal] public delegate void WeaponSelectedEventHandler(string choice);
	[Signal] public delegate void RerollRequestedEventHandler();
	[Signal] public delegate void SkipRequestedEventHandler();
	[Signal] public delegate void SwapRequestedEventHandler(string newSpellId, string removedSpellId);
	private Button rerollButton = null!;
	private Button skipButton = null!;
	private List<LevelUpOption> currentOptions = new();
	private List<EquippedSpellInfo> currentEquippedSpells = new();
	private Dictionary<string, int> currentBaselineElementCounts = new();
	private int currentRerollsRemaining = 0;
	private LevelUpOption pendingSwapOption = null;

	public override void _Ready()
	{
		rerollButton = GetNodeOrNull<Button>("Panel/VBoxContainer/RerollButton");
		if (rerollButton != null)
		{
			rerollButton.Pressed += OnRerollPressed;
		}

		skipButton = GetNodeOrNull<Button>("Panel/VBoxContainer/SkipButton");
		if (skipButton == null)
		{
			// Fallback: create the skip button if the scene doesn't have one yet.
			var parent = GetNodeOrNull<Control>("Panel/VBoxContainer");
			if (parent != null)
			{
				skipButton = new Button();
				skipButton.Name = "SkipButton";
				skipButton.Text = "Skip";
				skipButton.CustomMinimumSize = new Vector2(180, 40);
				parent.AddChild(skipButton);
			}
		}
		if (skipButton != null)
		{
			skipButton.Pressed += OnSkipPressed;
		}

		var viewport = GetViewport();
		if (viewport != null)
		{
			viewport.SizeChanged += OnViewportSizeChanged;
		}
	}

	public void SetOptions(List<LevelUpOption> options = null, int rerollsRemaining = 0, List<EquippedSpellInfo> equippedSpells = null, Dictionary<string, int> baselineElementCounts = null)
	{
		GD.Print("SetOptions called");
		currentOptions = options ?? new List<LevelUpOption>();
		currentEquippedSpells = equippedSpells ?? new List<EquippedSpellInfo>();
		currentBaselineElementCounts = baselineElementCounts ?? new Dictionary<string, int>();
		currentRerollsRemaining = rerollsRemaining;
		pendingSwapOption = null;
		BuildButtonsFrom(currentOptions);
		UpdateRerollState(currentRerollsRemaining);
	}

	private void OnViewportSizeChanged()
	{
		if (!Visible)
			return;

		if (pendingSwapOption != null)
		{
			BuildSwapSelectionButtons(pendingSwapOption);
		}
		else
		{
			BuildButtonsFrom(currentOptions);
		}
		UpdateRerollState(currentRerollsRemaining);
	}

	private void OnOptionChosen(LevelUpOption option)
	{
		if (option.RequiresSlotSwap)
		{
			pendingSwapOption = option;
			BuildSwapSelectionButtons(option);
			return;
		}

		EmitSignal(nameof(WeaponSelected), option.SpellId);
		Hide();
	}

	private void OnSwapChoiceChosen(LevelUpOption newOption, EquippedSpellInfo toRemove)
	{
		EmitSignal(nameof(SwapRequested), newOption.SpellId, toRemove.Id);
		Hide();
	}

	private void OnSkipPressed()
	{
		EmitSignal(nameof(SkipRequested));
		Hide();
	}

	private void OnRerollPressed()
	{
		EmitSignal(nameof(RerollRequested));
	}

	private void UpdateRerollState(int rerollsRemaining)
	{
		if (rerollButton == null)
			return;

		rerollButton.Text = $"Reroll ({rerollsRemaining})";
		rerollButton.Disabled = rerollsRemaining <= 0;
	}

	private void ClearButtons()
	{
		var container = GetOptionsContainer();
		if (container == null) return;
		foreach (Node c in container.GetChildren()) c.QueueFree();
	}

	private Control GetOptionsContainer()
	{
		// Always look for Options under Panel/VBoxContainer/Options
		var optionsPath = "Panel/VBoxContainer/Options";
		var node = GetNodeOrNull<Control>(optionsPath);
		GD.Print($"GetOptionsContainer: node at {optionsPath} is {(node != null ? "found" : "not found")}");
		if (node != null) return node;
		// fallback: search recursively
		foreach (Node child in GetChildren())
		{
			var found = FindControlByName(child, "Options");
			if (found != null) return found;
		}
		// fallback: create one if not found
		var fallback = new VBoxContainer();
		fallback.Name = "Options";
		fallback.CustomMinimumSize = new Vector2(300, 120);
		AddChild(fallback);
		return fallback;
	}

	private Control FindControlByName(Node node, string targetName)
	{
		if (node == null) return null;
		if (node is Control c && c.Name == targetName) return c;
		foreach (Node child in node.GetChildren())
		{
			var res = FindControlByName(child, targetName);
			if (res != null) return res;
		}
		return null;
	}

	private void BuildButtonsFrom(List<LevelUpOption> options)
	{
		ClearButtons();
		var container = GetOptionsContainer();
		GD.Print($"BuildButtonsFrom: container is {(container != null ? "not null" : "null")}");
		if (container == null)
		{
			GD.PushWarning("LevelUpMenu: no container found for options");
			return;
		}
		if (options == null || options.Count == 0)
		{
			var label = new Label();
			label.Text = "No upgrades available";
			container.AddChild(label);
			return;
		}

		if (container is GridContainer grid)
		{
			// Always lay the main upgrade choices out side-by-side in a single row (typically 3).
			grid.Columns = Math.Max(1, options.Count);
		}

		int maxShow = options.Count;
		GD.Print($"BuildButtonsFrom: options.Count={options.Count}, maxShow={maxShow}");
		for (int i = 0; i < maxShow; i++)
		{
			var option = options[i];
			GD.Print($"Adding button for spell: {option.DisplayName}");

			// Each option gets its own vertical column: the card, then directly below it (same
			// width, since both are ExpandFill children of this column) the elemental tag notes
			// for whatever elements THIS spell is tagged with (skipped for plain level-ups, only
			// shown for brand-new picks - see BuildElementNotesForOption).
			var column = new VBoxContainer();
			column.AddThemeConstantOverride("separation", 6);
			column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

			// The card itself IS the button (issue: whole card should be clickable, not just a
			// banner at the top), sized larger to leave room for the spell's icon.
			var card = new Button();
			card.CustomMinimumSize = new Vector2(220, 260);
			card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			card.ClipText = false;
			card.Text = string.Empty;
			card.Pressed += () => OnOptionChosen(option);

			// Upgrades (leveling up an already-equipped spell) get a distinct colored border so
			// they're easy to tell apart from brand-new picks at a glance.
			if (!option.IsNewUnlock)
			{
				var upgradeStyle = new StyleBoxFlat();
				upgradeStyle.BgColor = new Color(0.18f, 0.18f, 0.2f, 0.95f);
				upgradeStyle.SetCornerRadiusAll(4);
				upgradeStyle.SetBorderWidthAll(3);
				upgradeStyle.BorderColor = UpgradeBorderColor;
				card.AddThemeStyleboxOverride("normal", upgradeStyle);
				card.AddThemeStyleboxOverride("hover", upgradeStyle);
				card.AddThemeStyleboxOverride("pressed", upgradeStyle);
				card.AddThemeStyleboxOverride("focus", upgradeStyle);
			}

			var content = new VBoxContainer();
			content.MouseFilter = Control.MouseFilterEnum.Ignore;
			content.AddThemeConstantOverride("separation", 4);
			content.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			content.OffsetLeft = 8;
			content.OffsetTop = 8;
			content.OffsetRight = -8;
			content.OffsetBottom = -8;
			card.AddChild(content);

			var icon = new TextureRect
			{
				Texture = option.Icon ?? DefaultSpellIcon,
				CustomMinimumSize = new Vector2(0, 120),
				ExpandMode = TextureRect.ExpandModeEnum.FitHeightProportional,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				// All spell icons are reused/temporary placeholder art (issue #30, including the
				// shared DefaultSpellIcon most spells fall back to) - tint by the spell's dominant
				// element so otherwise-identical icons can still be told apart at a glance.
				Modulate = GetDominantElementColor(option).Lerp(Colors.White, 0.35f)
			};
			content.AddChild(icon);

			var title = new Label
			{
				Text = option.GetButtonText(),
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			content.AddChild(title);

			if (!string.IsNullOrWhiteSpace(option.Description))
			{
				var subtitle = new Label
				{
					Text = option.Description,
					HorizontalAlignment = HorizontalAlignment.Center,
					AutowrapMode = TextServer.AutowrapMode.WordSmart,
					MouseFilter = Control.MouseFilterEnum.Ignore
				};
				subtitle.AddThemeFontSizeOverride("font_size", 12);
				content.AddChild(subtitle);
			}

			column.AddChild(card);

			// Element tags only make sense for a brand-new spell pick (an upgrade doesn't change
			// what elements you're tagged with, since it's already equipped).
			if (option.IsNewUnlock)
			{
				foreach (var noteControl in BuildElementNotesForOption(option))
					column.AddChild(noteControl);
			}

			container.AddChild(column);
		}
	}

	// Shared fallback icon for spells without unique art yet (SpellData.Icon left null, issue #30).
	private static readonly Texture2D DefaultSpellIcon = GD.Load<Texture2D>("res://assets/Magic_Missile.png");

	// Distinct border color flagging "this option is an upgrade" (vs. a brand-new spell pick),
	// chosen to not clash with the orange "levels up a passive" note highlight.
	private static readonly Color UpgradeBorderColor = new Color(0.25f, 0.85f, 0.95f);

	// Picks the element with the highest weight this option is tagged with, for tinting its icon.
	// Falls back to white (no tint) for options with no element tags at all.
	private Color GetDominantElementColor(LevelUpOption option)
	{
		if (option.ResultingElementCounts == null || option.ResultingElementCounts.Count == 0)
			return Colors.White;

		string dominant = option.ResultingElementCounts.OrderByDescending(p => p.Value).First().Key;
		return Enum.TryParse<Element>(dominant, out var element) ? ElementColors.GetColor(element) : Colors.White;
	}

	private int GetResponsiveColumnCount(int optionCount)
	{
		if (optionCount <= 1)
			return 1;

		return Math.Min(3, optionCount);
	}

	// --- Elemental tag notes (directly below each option's card, same width, issue #15/#16) ---

	// Builds one note per element that THIS specific option is tagged with (from its own
	// ElementContribution/ResultingElementCounts, populated by Player.ApplyElementPreview - so this
	// naturally only ever shows elements relevant to that spell). If picking this option would push
	// an element to a higher tier (2/4/6 threshold) and a currently-equipped passive ability
	// (issue #22/#27) is tagged with that same element, the note gets an orange border to flag
	// "this choice would level up that passive".
	private List<Control> BuildElementNotesForOption(LevelUpOption option)
	{
		var notes = new List<Control>();
		if (option.ResultingElementCounts == null || option.ResultingElementCounts.Count == 0)
			return notes;

		foreach (string elementName in option.ResultingElementCounts.Keys.OrderBy(k => k))
		{
			int resultingCount = option.ResultingElementCounts[elementName];
			int baseCount = currentBaselineElementCounts.TryGetValue(elementName, out int b) ? b : 0;
			int baseTier = GetTierForCount(baseCount);
			int resultingTier = GetTierForCount(resultingCount);

			bool wouldLevelUpPassive = false;
			if (resultingTier > baseTier)
			{
				wouldLevelUpPassive = currentEquippedSpells.Any(s => s.IsPassive && s.ElementWeights != null && s.ElementWeights.ContainsKey(elementName));
			}

			notes.Add(BuildElementNote(elementName, resultingCount, resultingTier, wouldLevelUpPassive));
		}

		return notes;
	}

	private int GetTierForCount(int count)
	{
		if (count >= 6) return 6;
		if (count >= 4) return 4;
		if (count >= 2) return 2;
		return 0;
	}

	private Control BuildElementNote(string elementName, int count, int tier, bool highlightOrange)
	{
		var note = new PanelContainer();
		note.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.14f, 0.14f, 0.17f, 0.9f);
		style.SetContentMarginAll(6);
		style.SetCornerRadiusAll(4);
		if (highlightOrange)
		{
			style.SetBorderWidthAll(2);
			style.BorderColor = new Color(1.0f, 0.55f, 0.1f);
		}
		note.AddThemeStyleboxOverride("panel", style);

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 1);
		note.AddChild(box);

		string tierText = tier > 0 ? $" (Tier {tier})" : string.Empty;
		var title = new Label { Text = $"{elementName}: {count}{tierText}", HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontSizeOverride("font_size", 12);
		title.AddThemeColorOverride("font_color", new Color(0.65f, 0.85f, 1.0f));
		box.AddChild(title);

		var effect = new Label { Text = GetElementEffectText(elementName, tier), HorizontalAlignment = HorizontalAlignment.Center };
		effect.AddThemeFontSizeOverride("font_size", 11);
		effect.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		effect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		box.AddChild(effect);

		if (highlightOrange)
		{
			var flag = new Label { Text = "Levels up a passive!", HorizontalAlignment = HorizontalAlignment.Center };
			flag.AddThemeFontSizeOverride("font_size", 10);
			flag.AddThemeColorOverride("font_color", new Color(1.0f, 0.55f, 0.1f));
			box.AddChild(flag);
		}

		return note;
	}

	// Mirrors the tier thresholds/values used by Player.cs's Get*Bonus helpers (issue #16), as
	// player-facing text. Kept in sync manually since Player.cs's formulas are private.
	private string GetElementEffectText(string elementName, int tier)
	{
		if (tier <= 0)
			return "No bonus yet";

		return elementName switch
		{
			"Arcane" => tier switch { 6 => "+35% XP gained", 4 => "+20% XP gained", _ => "+10% XP gained" },
			"Light" => tier switch { 6 => "Heal 10% of damage dealt", 4 => "Heal 6% of damage dealt", _ => "Heal 3% of damage dealt" },
			"Darkness" => tier switch { 6 => "-35% incoming damage", 4 => "-20% incoming damage", _ => "-10% incoming damage" },
			"Metal" => tier switch { 6 => "-4 flat damage taken", 4 => "-2 flat damage taken", _ => "-1 flat damage taken" },
			"Grass" => tier switch { 6 => "+4 HP regen/sec", 4 => "+2 HP regen/sec", _ => "+1 HP regen/sec" },
			"Earth" => tier switch { 6 => "+100 max HP", 4 => "+50 max HP", _ => "+20 max HP" },
			"Wind" => tier switch { 6 => "+35% move speed", 4 => "+20% move speed", _ => "+10% move speed" },
			"Water" => tier switch { 6 => "-18% cooldowns", 4 => "-10% cooldowns", _ => "-5% cooldowns" },
			"Fire" => tier switch { 6 => "+35% damage", 4 => "+20% damage", _ => "+10% damage" },
			"Ice" => tier switch { 6 => "+35% slow on hit", 4 => "+20% slow on hit", _ => "+10% slow on hit" },
			"Poison" => tier switch { 6 => "8 poison damage/tick", 4 => "4 poison damage/tick", _ => "2 poison damage/tick" },
			_ => "No bonus yet"
		};
	}

	private void BuildSwapSelectionButtons(LevelUpOption newOption)
	{
		ClearButtons();
		var container = GetOptionsContainer();
		if (container == null)
			return;

		var label = new Label();
		label.Text = $"Loadout is full - choose a spell to remove for {newOption.DisplayName}:";
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		container.AddChild(label);

		if (container is GridContainer grid)
		{
			grid.Columns = GetResponsiveColumnCount(currentEquippedSpells.Count);
		}

		foreach (var equipped in currentEquippedSpells)
		{
			var btn = new Button();
			btn.Text = $"{equipped.DisplayName} (Lv {equipped.CurrentLevel})";
			btn.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			btn.CustomMinimumSize = new Vector2(160, 52);
			btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			btn.Pressed += () => OnSwapChoiceChosen(newOption, equipped);
			container.AddChild(btn);
		}
	}
}
