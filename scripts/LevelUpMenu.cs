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
	[Signal] public delegate void RemoveRequestedEventHandler(string removedSpellId);
	private Button rerollButton = null!;
	private Button skipButton = null!;
	private List<LevelUpOption> currentOptions = new();
	private List<EquippedSpellInfo> currentEquippedSpells = new();
	private Dictionary<string, int> currentBaselineElementCounts = new();
	private int currentRerollsRemaining = 0;
	private LevelUpOption pendingSwapOption = null;
	private bool pendingRemoveSelection = false;
	private const float CardWidth = 240f;
	private const float CardHeight = 340f;
	private const float UpgradeSectionWidth = 190f;
	private static readonly Vector2 IconFrameSize = new Vector2(0, 132);
	private static readonly Vector2 IconSize = new Vector2(104, 104);

	public override void _Ready()
	{
		CenterMenuPanel();

		rerollButton = GetNodeOrNull<Button>("Panel/VBoxContainer/RerollButton");
		if (rerollButton != null)
		{
			rerollButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
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
			skipButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			skipButton.Pressed += OnSkipPressed;
		}

		var viewport = GetViewport();
		if (viewport != null)
		{
			viewport.SizeChanged += OnViewportSizeChanged;
		}
	}

	private void CenterMenuPanel()
	{
		var panel = GetNodeOrNull<Control>("Panel");
		if (panel == null)
			return;

		Vector2 viewportSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1152, 648);
		Vector2 panelSize = new Vector2(
			Mathf.Min(980f, Mathf.Max(720f, viewportSize.X - 64f)),
			Mathf.Min(620f, Mathf.Max(520f, viewportSize.Y - 48f))
		);

		panel.CustomMinimumSize = panelSize;
		panel.AnchorLeft = 0.5f;
		panel.AnchorTop = 0.5f;
		panel.AnchorRight = 0.5f;
		panel.AnchorBottom = 0.5f;
		panel.OffsetLeft = -panelSize.X * 0.5f;
		panel.OffsetTop = -panelSize.Y * 0.5f;
		panel.OffsetRight = panelSize.X * 0.5f;
		panel.OffsetBottom = panelSize.Y * 0.5f;
	}

	public void SetOptions(List<LevelUpOption> options = null, int rerollsRemaining = 0, List<EquippedSpellInfo> equippedSpells = null, Dictionary<string, int> baselineElementCounts = null)
	{
		GD.Print("SetOptions called");
		currentOptions = options ?? new List<LevelUpOption>();
		currentEquippedSpells = equippedSpells ?? new List<EquippedSpellInfo>();
		currentBaselineElementCounts = baselineElementCounts ?? new Dictionary<string, int>();
		currentRerollsRemaining = rerollsRemaining;
		pendingSwapOption = null;
		pendingRemoveSelection = false;
		BuildButtonsFrom(currentOptions);
		UpdateRerollState(currentRerollsRemaining);
	}

	private void OnViewportSizeChanged()
	{
		if (!Visible)
			return;

		CenterMenuPanel();

		if (pendingSwapOption != null)
		{
			BuildSwapSelectionButtons(pendingSwapOption);
		}
		else if (pendingRemoveSelection)
		{
			BuildRemoveSelectionButtons();
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

	private void OnRemoveChoiceChosen(EquippedSpellInfo toRemove)
	{
		EmitSignal(nameof(RemoveRequested), toRemove.Id);
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
			AddRemoveSpellButtonIfLoadoutFull(container);
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
			// banner at the top), with a fixed icon frame so differently-sized source art cannot
			// push text out of the card.
			var card = new Button();
			card.CustomMinimumSize = new Vector2(CardWidth, CardHeight);
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

			var iconFrame = new CenterContainer
			{
				CustomMinimumSize = IconFrameSize,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			content.AddChild(iconFrame);

			var icon = new TextureRect
			{
				Texture = option.Icon ?? DefaultSpellIcon,
				CustomMinimumSize = IconSize,
				ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				// All spell icons are reused/temporary placeholder art (issue #30, including the
				// shared DefaultSpellIcon most spells fall back to) - tint by the spell's dominant
				// element so otherwise-identical icons can still be told apart at a glance.
				Modulate = GetDominantElementColor(option).Lerp(Colors.White, 0.35f)
			};
			iconFrame.AddChild(icon);

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
					MouseFilter = Control.MouseFilterEnum.Ignore,
					SizeFlagsVertical = Control.SizeFlags.ExpandFill
				};
				subtitle.AddThemeFontSizeOverride("font_size", 12);
				content.AddChild(subtitle);
			}

			if (!option.IsNewUnlock && !string.IsNullOrWhiteSpace(option.UpgradeSummary))
			{
				var upgradeSection = new PanelContainer
				{
					CustomMinimumSize = new Vector2(UpgradeSectionWidth, 0),
					MouseFilter = Control.MouseFilterEnum.Ignore,
					SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
				};
				var upgradeStyle = new StyleBoxFlat();
				upgradeStyle.BgColor = new Color(0.10f, 0.18f, 0.20f, 0.95f);
				upgradeStyle.BorderColor = UpgradeBorderColor;
				upgradeStyle.SetBorderWidthAll(1);
				upgradeStyle.SetCornerRadiusAll(4);
				upgradeStyle.SetContentMarginAll(5);
				upgradeSection.AddThemeStyleboxOverride("panel", upgradeStyle);

				var upgradeBox = new VBoxContainer
				{
					MouseFilter = Control.MouseFilterEnum.Ignore,
					SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
				};
				upgradeBox.AddThemeConstantOverride("separation", 1);
				upgradeSection.AddChild(upgradeBox);

				var upgradeHeader = new Label
				{
					Text = "Level Up",
					HorizontalAlignment = HorizontalAlignment.Center,
					MouseFilter = Control.MouseFilterEnum.Ignore
				};
				upgradeHeader.AddThemeFontSizeOverride("font_size", 10);
				upgradeHeader.AddThemeColorOverride("font_color", UpgradeBorderColor);
				upgradeBox.AddChild(upgradeHeader);

				var upgradeSummary = new Label
				{
					Text = option.UpgradeSummary,
					HorizontalAlignment = HorizontalAlignment.Center,
					AutowrapMode = TextServer.AutowrapMode.WordSmart,
					MouseFilter = Control.MouseFilterEnum.Ignore
				};
				upgradeSummary.AddThemeFontSizeOverride("font_size", 12);
				upgradeSummary.AddThemeColorOverride("font_color", new Color(0.85f, 0.95f, 1.0f));
				upgradeBox.AddChild(upgradeSummary);

				content.AddChild(upgradeSection);
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

		AddRemoveSpellButtonIfLoadoutFull(container);
	}

	private void AddRemoveSpellButtonIfLoadoutFull(Control container)
	{
		if (container == null || currentEquippedSpells.Count < Player.MaxSpellSlots)
			return;

		var removeButton = new Button();
		removeButton.Text = "Remove a Spell";
		removeButton.CustomMinimumSize = new Vector2(220, 72);
		removeButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		removeButton.Pressed += () =>
		{
			pendingRemoveSelection = true;
			BuildRemoveSelectionButtons();
		};
		container.AddChild(removeButton);
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

		var title = new Label { Text = $"{elementName} {ElementPassiveDescriptions.GetProgressLabel(count)}", HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontSizeOverride("font_size", 12);
		title.AddThemeColorOverride("font_color", new Color(0.65f, 0.85f, 1.0f));
		box.AddChild(title);

		var effect = new Label { Text = ElementPassiveDescriptions.GetEffectText(elementName, tier), HorizontalAlignment = HorizontalAlignment.Center };
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

	private void BuildRemoveSelectionButtons()
	{
		ClearButtons();
		var container = GetOptionsContainer();
		if (container == null)
			return;

		var label = new Label();
		label.Text = "Choose a spell to remove from your loadout:";
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
			btn.Pressed += () => OnRemoveChoiceChosen(equipped);
			container.AddChild(btn);
		}
	}
}
