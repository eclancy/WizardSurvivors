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
	private LevelUpOption pendingEvolutionOption = null;
	private bool pendingRemoveSelection = false;
	private const string EvolutionRootName = "EvolutionRoot";
	private const float CardWidth = 272f;
	private const float CardHeight = 420f;
	private const float UpgradeSectionWidth = 238f;
	private static readonly Vector2 IconFrameSize = new Vector2(0, 146);
	private static readonly Vector2 IconSize = new Vector2(114, 114);
	// A stacked card is a row, not a poster. In portrait every card is the full width of the
	// screen, so the desktop layout - a small icon floating in the middle above a thin centred
	// column of text - left most of the card empty on both sides and dead space underneath.
	// Narrow cards instead put the art in a fixed column on the left and read left-aligned
	// beside it, and take their height from their own content rather than a fixed card size.
	private const float NarrowRowIconColumn = 84f;
	private const float NarrowRowMinHeight = 104f;
	// Clears the in-run HUD in portrait: the health frame (6 + 44), the XP bar under it, and the
	// element chip row below that, plus a little air. Node2DGame owns those; this only has to
	// stay out of their way now that the menu is see-through.
	private const float HudSafeTop = 112f;

	public override void _Ready()
	{
		CenterMenuPanel();
		EnsureNarrowOptionsScroll();
		ApplyFantasyGuiSkin();

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
				skipButton.CustomMinimumSize = new Vector2(180, 48);
				parent.AddChild(skipButton);
			}
		}
		if (skipButton != null)
		{
			skipButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			skipButton.Pressed += OnSkipPressed;
		}

		FantasyGuiSkin.StyleButton(rerollButton);
		FantasyGuiSkin.StyleButton(skipButton, FantasyGuiSkin.GlyphPlay);
		GroupActionButtonsForNarrow();
		StyleHeading();
		FadeIn();

		var viewport = GetViewport();
		if (viewport != null)
		{
			viewport.SizeChanged += OnViewportSizeChanged;
		}
	}

	// Reroll and Skip stacked vertically cost two rows of a phone screen for two short words.
	// Side by side they are one thumb-height action bar at the bottom, which is where a phone
	// UI puts its actions anyway.
	private void GroupActionButtonsForNarrow()
	{
		if (!ResponsiveLayout.IsNarrow(this) || rerollButton == null || skipButton == null)
			return;

		if (rerollButton.GetParent() is not Control vbox || skipButton.GetParent() != vbox)
			return;

		int index = rerollButton.GetIndex();
		var row = new HBoxContainer { Name = "ActionRow", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("separation", 10);

		vbox.RemoveChild(rerollButton);
		vbox.RemoveChild(skipButton);
		vbox.AddChild(row);
		vbox.MoveChild(row, index);
		row.AddChild(rerollButton);
		row.AddChild(skipButton);

		foreach (Button button in new[] { rerollButton, skipButton })
		{
			button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			button.CustomMinimumSize = new Vector2(0, ResponsiveLayout.MinTouchTarget);
		}
	}

	// The heading now sits over the battlefield rather than a flat wall, so it needs an outline
	// to stay readable whatever the player is standing on.
	private void StyleHeading()
	{
		var heading = GetNodeOrNull<Label>("Panel/VBoxContainer/Label");
		if (heading == null)
			return;

		heading.AddThemeFontSizeOverride("font_size", ResponsiveLayout.IsNarrow(this) ? 22 : 20);
		heading.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.86f));
		heading.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
		heading.AddThemeConstantOverride("outline_size", 6);
	}

	// A level-up used to cut straight to a black screen, which is what made it feel like leaving
	// the game rather than pausing it. Fading the dim and the cards in over a sixth of a second
	// keeps the battlefield in view underneath the whole time. Short enough that it never
	// delays the choice; the tween runs on the paused tree because this layer processes when
	// paused.
	private void FadeIn()
	{
		var dim = GetNodeOrNull<CanvasItem>("DarkBackground");
		var panel = GetNodeOrNull<CanvasItem>("Panel");
		if (dim == null && panel == null)
			return;

		var tween = CreateTween();
		tween.SetParallel(true);
		if (dim != null)
		{
			dim.Modulate = new Color(1f, 1f, 1f, 0f);
			tween.TweenProperty(dim, "modulate:a", 1f, 0.16f);
		}
		if (panel != null)
		{
			panel.Modulate = new Color(1f, 1f, 1f, 0f);
			tween.TweenProperty(panel, "modulate:a", 1f, 0.16f).SetDelay(0.05f);
		}
	}

	private void ApplyFantasyGuiSkin()
	{
		Control panel = GetNodeOrNull<Control>("Panel");
		if (panel != null)
		{
			foreach (Node child in panel.GetChildren())
			{
				if (child is TextureRect tr && (tr.Name.ToString().Contains("Backdrop") || tr.Name.ToString().Contains("Background")))
				{
					tr.QueueFree();
				}
			}

			if (panel is Panel panelNode)
			{
				panelNode.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
			}
		}

		foreach (Node child in GetChildren())
		{
			if (child is TextureRect tr && (tr.Name.ToString().Contains("Backdrop") || tr.Name.ToString().Contains("Background")))
			{
				tr.QueueFree();
			}
		}
	}

	private void CenterMenuPanel()
	{
		var panel = GetNodeOrNull<Control>("Panel");
		if (panel == null)
			return;

		Vector2 viewportSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1152, 648);

		// The desktop clamps were actively wrong in portrait. The 860 minimum width made the panel
		// 140 units wider than a 720-wide screen, and the 700 maximum height left three stacked
		// 300-tall cards overflowing the panel and pushing the Skip button off the bottom edge.
		//
		// Portrait also has to dodge the run HUD. The menu no longer paints over the game, so the
		// health bar, timer, element chips and spell strip are all still visible up there - a
		// centred panel simply printed the heading and the first card straight through them.
		// Start below that band instead and run to the bottom edge.
		if (ResponsiveLayout.IsNarrow(this))
		{
			panel.CustomMinimumSize = Vector2.Zero;
			panel.AnchorLeft = 0f;
			panel.AnchorTop = 0f;
			panel.AnchorRight = 1f;
			panel.AnchorBottom = 1f;
			panel.OffsetLeft = 12f;
			panel.OffsetTop = HudSafeTop;
			panel.OffsetRight = -12f;
			panel.OffsetBottom = -12f;
			return;
		}

		Vector2 panelSize = new Vector2(
			Mathf.Min(1140f, Mathf.Max(860f, viewportSize.X - 52f)),
			Mathf.Min(700f, Mathf.Max(560f, viewportSize.Y - 40f)));

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
		pendingEvolutionOption = null;
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
		else if (pendingEvolutionOption != null)
		{
			BuildEvolutionSelectionButtons(pendingEvolutionOption);
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
		SfxPlayer.Global(SfxCatalog.CardSelect);
		if (option.RequiresSlotSwap)
		{
			pendingSwapOption = option;
			BuildSwapSelectionButtons(option);
			return;
		}

		if (option.IsEvolutionMilestone && option.EvolutionChoices != null && option.EvolutionChoices.Count > 0)
		{
			pendingEvolutionOption = option;
			BuildEvolutionSelectionButtons(option);
			return;
		}

		EmitSignal(nameof(WeaponSelected), option.SpellId);
		Hide();
	}

	private void OnEvolutionChosen(LevelUpOption option, SpellEvolutionOption evo)
	{
		pendingEvolutionOption = null;
		EmitSignal(nameof(WeaponSelected), $"{option.SpellId}:{evo.Id}");
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
		SfxPlayer.Global(SfxCatalog.UiBack);
		EmitSignal(nameof(SkipRequested));
		Hide();
	}

	private void OnRerollPressed()
	{
		SfxPlayer.Global(SfxCatalog.Reroll);
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

	// Drops a ScrollContainer between the panel's VBox and the Options grid on a phone.
	//
	// A stacked option is not just its 300-tall card: the element-tier rows sit *beside* the card
	// as siblings in the same grid, so three options come to roughly 1200 units - more than any
	// portrait screen. Without somewhere to scroll, the overflow pushed Reroll and Skip off the
	// bottom edge, where they could not be tapped at all. Reroll and Skip stay outside the scroll
	// so they remain pinned and reachable however tall the options get.
	private void EnsureNarrowOptionsScroll()
	{
		if (!ResponsiveLayout.IsNarrow(this))
			return;

		// Null once wrapped, so this is safe to call more than once.
		var options = GetNodeOrNull<Control>("Panel/VBoxContainer/Options");
		if (options?.GetParent() is not Control vbox)
			return;

		int index = options.GetIndex();
		var scroll = new ScrollContainer
		{
			Name = "OptionsScroll",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};

		vbox.RemoveChild(options);
		vbox.AddChild(scroll);
		vbox.MoveChild(scroll, index);
		scroll.AddChild(options);
		options.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
	}

	private Control GetOptionsContainer()
	{
		// Direct path first; once EnsureNarrowOptionsScroll has reparented the grid under a
		// ScrollContainer this misses and the recursive search below finds it by name.
		var node = GetNodeOrNull<Control>("Panel/VBoxContainer/Options");
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
		// Restore the normal view in case we are returning from the evolution overlay.
		SetNormalViewVisible(true);
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
			grid.Columns = GetResponsiveColumnCount(options.Count);
		}

		for (int i = 0; i < options.Count; i++)
		{
			var option = options[i];
			var column = new VBoxContainer();
			column.AddThemeConstantOverride("separation", 10);
			column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

			column.AddChild(ResponsiveLayout.IsNarrow(this)
				? BuildNarrowOptionCard(option)
				: BuildWideOptionCard(option));
			if (option.IsNewUnlock)
			{
				foreach (var noteControl in BuildElementNotesForOption(option))
					column.AddChild(noteControl);
			}
			container.AddChild(column);
		}
	}

	// The desktop card: a poster. Art across the top, everything centred beneath it, a fixed
	// 272x420 so three sit side by side.
	private Control BuildWideOptionCard(LevelUpOption option)
	{
		var card = new Button
		{
			CustomMinimumSize = new Vector2(CardWidth, CardHeight),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			ClipText = false,
			Text = string.Empty
		};
		card.Pressed += () => OnOptionChosen(option);
		ApplyOptionCardStyle(card, option);

		var content = new VBoxContainer();
		content.MouseFilter = Control.MouseFilterEnum.Ignore;
		content.AddThemeConstantOverride("separation", 7);
		content.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		content.OffsetLeft = 12;
		content.OffsetTop = 10;
		content.OffsetRight = -12;
		content.OffsetBottom = -10;
		card.AddChild(content);

		var iconFrame = new CenterContainer
		{
			CustomMinimumSize = IconFrameSize,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		iconFrame.AddChild(BuildOptionIcon(option, IconSize));
		content.AddChild(iconFrame);

		content.AddChild(BuildOptionTitle(option, HorizontalAlignment.Center));

		var levelPips = BuildLevelPips(option, BoxContainer.AlignmentMode.Center);
		if (levelPips != null)
		{
			levelPips.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			content.AddChild(levelPips);
		}

		content.AddChild(BuildOptionTypeLabel(option, HorizontalAlignment.Center));

		if (!string.IsNullOrWhiteSpace(option.Description))
		{
			var subtitle = BuildOptionDescription(option, HorizontalAlignment.Center);
			subtitle.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			content.AddChild(subtitle);
		}

		Control upgradeSection = BuildUpgradeSection(option, Control.SizeFlags.ShrinkCenter);
		if (upgradeSection != null)
			content.AddChild(upgradeSection);

		var tagChips = BuildSpellTagChips(option, FlowContainer.AlignmentMode.Center);
		if (tagChips != null)
		{
			// Push the chips to the bottom edge so they line up across all three cards.
			content.AddChild(new Control
			{
				SizeFlagsVertical = Control.SizeFlags.ExpandFill,
				MouseFilter = Control.MouseFilterEnum.Ignore
			});
			content.AddChild(tagChips);
		}

		return card;
	}

	// The portrait card: a row. Art in a fixed left column, text left-aligned beside it, and no
	// fixed height - a PanelContainer takes its size from the text, so a card with a long
	// description grows and a short one stays short instead of every card padding out to 300.
	// The Button is a sibling of the content rather than its parent so that it can be stretched
	// to whatever height the content settles on while still being the thing that gets clicked.
	private Control BuildNarrowOptionCard(LevelUpOption option)
	{
		var root = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		root.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

		var card = new Button
		{
			CustomMinimumSize = new Vector2(0, NarrowRowMinHeight),
			ClipText = false,
			Text = string.Empty
		};
		card.Pressed += () => OnOptionChosen(option);
		ApplyOptionCardStyle(card, option);
		root.AddChild(card);

		var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 10);
		margin.AddThemeConstantOverride("margin_bottom", 10);
		root.AddChild(margin);

		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 12);
		margin.AddChild(row);

		var iconFrame = new CenterContainer
		{
			CustomMinimumSize = new Vector2(NarrowRowIconColumn, NarrowRowIconColumn),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		iconFrame.AddChild(BuildOptionIcon(option, new Vector2(NarrowRowIconColumn, NarrowRowIconColumn)));
		row.AddChild(iconFrame);

		var text = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		text.AddThemeConstantOverride("separation", 5);
		row.AddChild(text);

		text.AddChild(BuildOptionTitle(option, HorizontalAlignment.Left));

		// Pips and the Attack/Passive label share one line: both are short, and stacking them
		// costs a whole row of height on the axis that is actually scarce here.
		var metaRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		metaRow.AddThemeConstantOverride("separation", 10);
		Control pips = BuildLevelPips(option, BoxContainer.AlignmentMode.Begin);
		if (pips != null)
		{
			pips.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
			metaRow.AddChild(pips);
		}
		metaRow.AddChild(BuildOptionTypeLabel(option, HorizontalAlignment.Left));
		text.AddChild(metaRow);

		if (!string.IsNullOrWhiteSpace(option.Description))
			text.AddChild(BuildOptionDescription(option, HorizontalAlignment.Left));

		Control upgradeSection = BuildUpgradeSection(option, Control.SizeFlags.ExpandFill);
		if (upgradeSection != null)
			text.AddChild(upgradeSection);

		var tagChips = BuildSpellTagChips(option, FlowContainer.AlignmentMode.Begin);
		if (tagChips != null)
			text.AddChild(tagChips);

		return root;
	}

	private TextureRect BuildOptionIcon(LevelUpOption option, Vector2 size)
	{
		return new TextureRect
		{
			Texture = option.Icon ?? DefaultSpellIcon,
			CustomMinimumSize = size,
			ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = GetDominantElementColor(option).Lerp(Colors.White, 0.35f)
		};
	}

	private Label BuildOptionTitle(LevelUpOption option, HorizontalAlignment alignment)
	{
		var title = new Label
		{
			Text = option.DisplayName,
			HorizontalAlignment = alignment,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		title.AddThemeFontSizeOverride("font_size", 17);
		return title;
	}

	private Label BuildOptionTypeLabel(LevelUpOption option, HorizontalAlignment alignment)
	{
		var typeLabel = new Label
		{
			Text = option.IsPassive ? "Passive" : "Attack",
			HorizontalAlignment = alignment,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		typeLabel.AddThemeFontSizeOverride("font_size", 11);
		typeLabel.AddThemeColorOverride("font_color", TypeLabelColor);
		return typeLabel;
	}

	private Label BuildOptionDescription(LevelUpOption option, HorizontalAlignment alignment)
	{
		var subtitle = new Label
		{
			Text = option.Description,
			HorizontalAlignment = alignment,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		subtitle.AddThemeFontSizeOverride("font_size", 13);
		subtitle.AddThemeColorOverride("font_color", new Color(0.86f, 0.90f, 0.96f));
		return subtitle;
	}

	// The bordered "Level Up" box listing what this pick changes. Null for a brand-new spell,
	// which has no previous level to improve on.
	private Control BuildUpgradeSection(LevelUpOption option, Control.SizeFlags horizontalFlags)
	{
		if (option.IsNewUnlock || string.IsNullOrWhiteSpace(option.UpgradeSummary))
			return null;

		var upgradeSection = new PanelContainer
		{
			// Shrink-centred desktop cards need the fixed width to stay symmetric; a row card
			// stretches to the text column instead, so it asks for nothing.
			CustomMinimumSize = horizontalFlags == Control.SizeFlags.ShrinkCenter
				? new Vector2(UpgradeSectionWidth, 0)
				: Vector2.Zero,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = horizontalFlags
		};
		var upgradeStyle = new StyleBoxFlat();
		upgradeStyle.BgColor = new Color(0f, 0f, 0f, 0f);
		upgradeStyle.BorderColor = UpgradeBorderColor;
		upgradeStyle.SetBorderWidthAll(1);
		upgradeStyle.SetCornerRadiusAll(4);
		upgradeStyle.SetContentMarginAll(8);
		upgradeSection.AddThemeStyleboxOverride("panel", upgradeStyle);

		var upgradeBox = new VBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		upgradeBox.AddThemeConstantOverride("separation", 4);
		upgradeSection.AddChild(upgradeBox);

		bool centred = horizontalFlags == Control.SizeFlags.ShrinkCenter;
		var upgradeHeader = new Label
		{
			Text = "Level Up",
			HorizontalAlignment = centred ? HorizontalAlignment.Center : HorizontalAlignment.Left,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		upgradeHeader.AddThemeFontSizeOverride("font_size", 12);
		upgradeHeader.AddThemeColorOverride("font_color", UpgradeBorderColor);
		upgradeBox.AddChild(upgradeHeader);

		var upgradeSummary = new Label
		{
			Text = option.UpgradeSummary,
			HorizontalAlignment = HorizontalAlignment.Left,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		upgradeSummary.AddThemeFontSizeOverride("font_size", 13);
		upgradeSummary.AddThemeColorOverride("font_color", new Color(0.85f, 0.95f, 1.0f));
		upgradeBox.AddChild(upgradeSummary);

		return upgradeSection;
	}

	// Shared fallback icon for spells without unique art yet (SpellData.Icon left null, issue #30).
	private static readonly Texture2D DefaultSpellIcon = null;

	// Neutral color for the Attack/Passive text label - spell cards are no longer tinted by type,
	// so the label stays a plain readable gray instead of an attack/passive accent color.
	private static readonly Color TypeLabelColor = new Color(0.70f, 0.72f, 0.78f);

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

	// Builds a row of level pips shown under each option's title, one pip per available upgrade level
	// (MaxLevel - 1). Pips are yellow-bordered with a black interior; a pip is filled solid yellow for
	// each level already gained (NextLevel - 1), so a brand-new spell shows all empty, an upgrade to
	// level 2 shows one filled, and an upgrade to the final level shows every pip filled.
	// An HBox, not an HFlow: seven pips are 112 units wide and always fit, while a flow
	// container asked for its width inside a row card collapsed to its one-pip minimum and
	// wrapped the whole strip into a vertical stack beside the spell name.
	private Control BuildLevelPips(LevelUpOption option, BoxContainer.AlignmentMode alignment)
	{
		int total = Math.Max(0, option.MaxLevel - 1);
		if (total <= 0)
			return null;

		int filled = Math.Clamp(option.NextLevel - 1, 0, total);

		var row = new HBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		row.AddThemeConstantOverride("separation", 3);
		row.Alignment = alignment;

		var pipYellow = new Color(1f, 0.84f, 0.0f);
		for (int i = 0; i < total; i++)
		{
			var pip = new PanelContainer
			{
				CustomMinimumSize = new Vector2(13, 13),
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			var style = new StyleBoxFlat
			{
				BgColor = i < filled ? pipYellow : new Color(0f, 0f, 0f, 1f),
				BorderColor = pipYellow
			};
			style.SetBorderWidthAll(2);
			style.SetCornerRadiusAll(2);
			pip.AddThemeStyleboxOverride("panel", style);
			row.AddChild(pip);
		}

		return row;
	}

	// Builds the small colored element-tag chips shown at the bottom of each option card, mirroring
	// the in-game element badges. Each chip is filled with its element's color; a weight above 1 is
	// shown as e.g. "Darkness x2". Returns null when the spell has no element tags.
	private Control BuildSpellTagChips(LevelUpOption option, FlowContainer.AlignmentMode alignment)
	{
		if (option.SpellElementTags == null || option.SpellElementTags.Count == 0)
			return null;

		var row = new HFlowContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		row.AddThemeConstantOverride("h_separation", 3);
		row.AddThemeConstantOverride("v_separation", 3);
		row.Alignment = alignment;

		foreach (var pair in option.SpellElementTags.OrderByDescending(p => p.Value).ThenBy(p => p.Key))
		{
			Color color = Enum.TryParse<Element>(pair.Key, out var element)
				? ElementColors.GetColor(element)
				: new Color(0.5f, 0.5f, 0.5f);

			var chip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			var style = new StyleBoxFlat
			{
				BgColor = new Color(color.R, color.G, color.B, 0.88f),
				BorderColor = new Color(1f, 1f, 1f, 0.28f)
			};
			style.SetBorderWidthAll(1);
			style.SetCornerRadiusAll(4);
			style.SetContentMarginAll(3);
			chip.AddThemeStyleboxOverride("panel", style);

			var label = new Label
			{
				Text = pair.Value > 1 ? $"{pair.Key} x{pair.Value}" : pair.Key,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			label.AddThemeFontSizeOverride("font_size", 10);
			label.AddThemeColorOverride("font_color", GetReadableTextColor(color));
			chip.AddChild(label);
			row.AddChild(chip);
		}

		return row;
	}

	// Mirrors Node2DGame.GetReadableTextColor so tag chips stay legible on their element-colored fill.
	private static Color GetReadableTextColor(Color background)
	{
		float luminance = (background.R * 0.299f) + (background.G * 0.587f) + (background.B * 0.114f);
		return luminance > 0.62f ? new Color(0.06f, 0.06f, 0.07f) : Colors.White;
	}

	private int GetResponsiveColumnCount(int optionCount)
	{
		if (optionCount <= 1)
			return 1;

		// On a phone-width viewport the cards become full-width rows stacked vertically. Three
		// 272-wide cards side by side overflowed both edges of a 720-wide portrait screen, and a
		// 2+1 grid reads badly for a three-way choice, so narrow collapses straight to one column.
		if (ResponsiveLayout.IsNarrow(this))
			return 1;

		return Math.Min(3, optionCount);
	}

	// Card fill.
	//
	// The cards used to be fully transparent, which worked while the menu sat on an opaque
	// near-black wall. Now that the battlefield shows through the dim behind them, transparent
	// cards would put small text over grass, corpses and spell VFX. This is a *translucent*
	// fill, not a solid one - the world still reads through every card - but it gives the text
	// a consistent ground wherever the player happened to be standing when they levelled.
	private static readonly Color CardFill = new Color(0.05f, 0.06f, 0.09f, 0.55f);

	private void ApplyOptionCardStyle(Button card, LevelUpOption option)
	{
		var normalStyle = new StyleBoxFlat();
		normalStyle.BgColor = CardFill;
		normalStyle.SetCornerRadiusAll(4);
		bool isLevelUp = !option.IsNewUnlock;
		normalStyle.SetBorderWidthAll(isLevelUp ? 3 : 1);
		normalStyle.BorderColor = isLevelUp ? UpgradeBorderColor : new Color(0.4f, 0.45f, 0.55f, 0.4f);

		var hoverStyle = normalStyle.Duplicate() as StyleBoxFlat;
		if (hoverStyle != null)
		{
			hoverStyle.BgColor = new Color(0.16f, 0.19f, 0.26f, 0.72f);
			if (!isLevelUp)
				hoverStyle.BorderColor = new Color(0.6f, 0.7f, 0.9f, 0.8f);
		}

		var pressedStyle = normalStyle.Duplicate() as StyleBoxFlat;
		if (pressedStyle != null)
		{
			pressedStyle.BgColor = new Color(0.24f, 0.28f, 0.38f, 0.80f);
		}

		card.AddThemeStyleboxOverride("normal", normalStyle);
		card.AddThemeStyleboxOverride("hover", hoverStyle ?? normalStyle);
		card.AddThemeStyleboxOverride("pressed", pressedStyle ?? normalStyle);
		card.AddThemeStyleboxOverride("focus", hoverStyle ?? normalStyle);
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
		Color elementColor = Enum.TryParse<Element>(elementName, out var element)
			? ElementColors.GetColor(element)
			: new Color(0.5f, 0.5f, 0.5f);

		var note = new PanelContainer();
		note.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		var style = new StyleBoxFlat();
		style.BgColor = CardFill;
		style.SetContentMarginAll(6);
		style.SetCornerRadiusAll(4);
		style.SetBorderWidthAll(2);
		// The orange "levels up a passive" cue takes precedence over the element-colored border.
		style.BorderColor = highlightOrange ? new Color(1.0f, 0.55f, 0.1f) : elementColor;
		note.AddThemeStyleboxOverride("panel", style);

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 1);
		note.AddChild(box);

		var title = new Label { Text = $"{elementName} {ElementPassiveDescriptions.GetProgressLabel(count)}", HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontSizeOverride("font_size", 12);
		title.AddThemeColorOverride("font_color", elementColor.Lerp(Colors.White, 0.5f));
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

		// The options container is a GridContainer sized for spell cards - stack our header, card
		// grid and back button as their own full-width rows (column count 1) instead of dumping
		// them straight into the card grid, otherwise the back button gets stretched to card height
		// and the header labels get squeezed into a card-sized cell alongside the cards.
		if (container is GridContainer outerGrid)
			outerGrid.Columns = 1;

		var header = new VBoxContainer();
		header.AddThemeConstantOverride("separation", 4);

		var label = new Label();
		label.Text = "Erase a spell from your tome";
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.AddThemeFontSizeOverride("font_size", 22);
		header.AddChild(label);

		var prompt = new Label();
		prompt.Text = $"Make room for {newOption.DisplayName} - choose a spell to forget.";
		prompt.HorizontalAlignment = HorizontalAlignment.Center;
		prompt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		prompt.AddThemeFontSizeOverride("font_size", 13);
		prompt.AddThemeColorOverride("font_color", TypeLabelColor);
		header.AddChild(prompt);

		container.AddChild(header);

		var cardGrid = new GridContainer();
		cardGrid.AddThemeConstantOverride("h_separation", 8);
		cardGrid.AddThemeConstantOverride("v_separation", 8);
		cardGrid.Columns = GetResponsiveColumnCount(currentEquippedSpells.Count);
		foreach (var equipped in currentEquippedSpells)
			cardGrid.AddChild(BuildEraseSpellCard(equipped, () => OnSwapChoiceChosen(newOption, equipped)));
		container.AddChild(cardGrid);

		var backButton = new Button();
		backButton.Text = "Back";
		backButton.CustomMinimumSize = new Vector2(160, 52);
		backButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		backButton.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		backButton.Pressed += () =>
		{
			pendingSwapOption = null;
			BuildButtonsFrom(currentOptions);
		};
		FantasyGuiSkin.StyleButton(backButton, FantasyGuiSkin.IconExit);
		container.AddChild(backButton);
	}

	// A selectable card for the erase screen: spell art, name and current level. Clicking it forgets
	// that spell to free a loadout slot for the newly chosen spell.
	private Control BuildEraseSpellCard(EquippedSpellInfo equipped, Action onPressed)
	{
		// Portrait collapses this grid to one column too, where a 150-tall square card stretched
		// to the full width is the same wasted band the level-up cards used to be. Six of them is
		// a whole screen of mostly nothing, so narrow gets the compact row instead.
		if (ResponsiveLayout.IsNarrow(this))
			return BuildNarrowEraseSpellCard(equipped, onPressed);

		var card = new Button();
		card.CustomMinimumSize = new Vector2(150, 150);
		card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		card.ClipText = false;
		card.Text = string.Empty;
		card.Pressed += onPressed;

		var cardStyle = new StyleBoxFlat();
		cardStyle.BgColor = CardFill;
		cardStyle.BorderColor = new Color(0.4f, 0.45f, 0.55f, 0.4f);
		cardStyle.SetBorderWidthAll(1);
		cardStyle.SetCornerRadiusAll(4);

		var hoverStyle = cardStyle.Duplicate() as StyleBoxFlat;
		if (hoverStyle != null) hoverStyle.BgColor = new Color(0.16f, 0.19f, 0.26f, 0.72f);

		card.AddThemeStyleboxOverride("normal", cardStyle);
		card.AddThemeStyleboxOverride("hover", hoverStyle ?? cardStyle);
		card.AddThemeStyleboxOverride("pressed", hoverStyle ?? cardStyle);
		card.AddThemeStyleboxOverride("focus", hoverStyle ?? cardStyle);

		var content = new VBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Alignment = BoxContainer.AlignmentMode.Center
		};
		content.AddThemeConstantOverride("separation", 4);
		content.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		content.OffsetLeft = 8;
		content.OffsetTop = 8;
		content.OffsetRight = -8;
		content.OffsetBottom = -8;
		card.AddChild(content);

		var iconFrame = new CenterContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		content.AddChild(iconFrame);

		var icon = new TextureRect
		{
			Texture = equipped.Icon ?? DefaultSpellIcon,
			CustomMinimumSize = new Vector2(72, 72),
			ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		iconFrame.AddChild(icon);

		var name = new Label
		{
			Text = equipped.DisplayName,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		name.AddThemeFontSizeOverride("font_size", 14);
		content.AddChild(name);

		var level = new Label
		{
			Text = $"Lv {equipped.CurrentLevel}",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		level.AddThemeFontSizeOverride("font_size", 12);
		level.AddThemeColorOverride("font_color", TypeLabelColor);
		content.AddChild(level);

		return card;
	}

	private Control BuildNarrowEraseSpellCard(EquippedSpellInfo equipped, Action onPressed)
	{
		var root = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		root.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

		var card = new Button
		{
			CustomMinimumSize = new Vector2(0, ResponsiveLayout.MinTouchTarget + 16f),
			ClipText = false,
			Text = string.Empty
		};
		card.Pressed += onPressed;

		var cardStyle = new StyleBoxFlat();
		cardStyle.BgColor = CardFill;
		cardStyle.BorderColor = new Color(0.4f, 0.45f, 0.55f, 0.4f);
		cardStyle.SetBorderWidthAll(1);
		cardStyle.SetCornerRadiusAll(4);
		var hoverStyle = cardStyle.Duplicate() as StyleBoxFlat;
		if (hoverStyle != null)
			hoverStyle.BgColor = new Color(0.16f, 0.19f, 0.26f, 0.72f);
		card.AddThemeStyleboxOverride("normal", cardStyle);
		card.AddThemeStyleboxOverride("hover", hoverStyle ?? cardStyle);
		card.AddThemeStyleboxOverride("pressed", hoverStyle ?? cardStyle);
		card.AddThemeStyleboxOverride("focus", hoverStyle ?? cardStyle);
		root.AddChild(card);

		var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_top", 6);
		margin.AddThemeConstantOverride("margin_bottom", 6);
		root.AddChild(margin);

		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 12);
		margin.AddChild(row);

		var iconFrame = new CenterContainer
		{
			CustomMinimumSize = new Vector2(48, 48),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		iconFrame.AddChild(new TextureRect
		{
			Texture = equipped.Icon ?? DefaultSpellIcon,
			CustomMinimumSize = new Vector2(48, 48),
			ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore
		});
		row.AddChild(iconFrame);

		var name = new Label
		{
			Text = equipped.DisplayName,
			HorizontalAlignment = HorizontalAlignment.Left,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		name.AddThemeFontSizeOverride("font_size", 15);
		row.AddChild(name);

		var level = new Label
		{
			Text = $"Lv {equipped.CurrentLevel}",
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		level.AddThemeFontSizeOverride("font_size", 13);
		level.AddThemeColorOverride("font_color", TypeLabelColor);
		row.AddChild(level);

		return root;
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
			FantasyGuiSkin.ApplyButtonSet(new[] { btn }, 17);
			container.AddChild(btn);
		}
	}

	// The evolution view is laid out as its own full-rect overlay on the Panel rather than inside
	// the normal view's 3-column options grid. The grid positions children as grid cells, which
	// left the title floating in dead space and the Back button stranded mid-screen; owning the
	// whole panel lets the title pin to the top, the cards fill the middle, and Back sit at the
	// bottom. The normal view is hidden wholesale while this is up.
	private void BuildEvolutionSelectionButtons(LevelUpOption option)
	{
		var panel = GetNodeOrNull<Control>("Panel");
		if (panel == null)
			return;

		ClearButtons();
		SetNormalViewVisible(false);

		// A viewport resize re-invokes this while the view is already up (OnViewportSizeChanged),
		// so drop any existing overlay first rather than stacking a second one on top.
		RemoveEvolutionRoot();

		bool isAscension = option.MilestoneLevel == 8;

		var root = new VBoxContainer { Name = EvolutionRootName };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.OffsetLeft = 24;
		root.OffsetTop = 20;
		root.OffsetRight = -24;
		root.OffsetBottom = -20;
		root.AddThemeConstantOverride("separation", 18);
		panel.AddChild(root);

		// Title block pinned to the top, replacing the normal view's "Level Up!" heading.
		var headerBox = new VBoxContainer();
		headerBox.AddThemeConstantOverride("separation", 4);
		headerBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		headerBox.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;

		bool narrow = ResponsiveLayout.IsNarrow(this);

		var title = new Label();
		title.Text = isAscension ? "★ ULTIMATE ASCENSION (Level 8) ★" : "✦ SPELL MUTATION (Level 4) ✦";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		// 30pt does not fit "★ ULTIMATE ASCENSION (Level 8) ★" across a 720-wide phone.
		title.AddThemeFontSizeOverride("font_size", narrow ? 20 : 30);
		title.AddThemeColorOverride("font_color", isAscension ? new Color(1.0f, 0.85f, 0.2f) : new Color(0.35f, 0.95f, 0.8f));
		headerBox.AddChild(title);

		var subtitle = new Label();
		subtitle.Text = isAscension
			? $"Choose 1 of 2 ultimate build-defining evolutions for {option.DisplayName}:"
			: $"Choose 1 of 3 mechanical modifications to mutate {option.DisplayName}:";
		subtitle.HorizontalAlignment = HorizontalAlignment.Center;
		subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		subtitle.AddThemeFontSizeOverride("font_size", narrow ? 13 : 15);
		subtitle.AddThemeColorOverride("font_color", new Color(0.85f, 0.9f, 0.96f));
		headerBox.AddChild(subtitle);

		root.AddChild(headerBox);

		// Desktop: cards take the whole middle band as one row, equal stretch, expanding on both
		// axes so they dominate the screen and stay symmetric for either 2 or 3 choices.
		//
		// Portrait: three 272-wide cards side by side came to 856 units on a 720-wide screen, so
		// the outer two ran off both edges and one choice could not be read at all. Stack them
		// instead, in a scroll container, as the same row cards the level-up list uses.
		if (narrow)
		{
			var scroll = new ScrollContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill,
				HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
			};
			root.AddChild(scroll);

			var cardsColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			cardsColumn.AddThemeConstantOverride("separation", 12);
			scroll.AddChild(cardsColumn);

			foreach (var evo in option.EvolutionChoices)
			{
				Control card = BuildNarrowEvolutionCard(option, evo, () => OnEvolutionChosen(option, evo));
				card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				cardsColumn.AddChild(card);
			}
		}
		else
		{
			var cardsRow = new HBoxContainer();
			cardsRow.Alignment = BoxContainer.AlignmentMode.Center;
			cardsRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			cardsRow.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			cardsRow.AddThemeConstantOverride("separation", 20);
			root.AddChild(cardsRow);

			foreach (var evo in option.EvolutionChoices)
			{
				Control card = BuildEvolutionOptionCard(option, evo, () => OnEvolutionChosen(option, evo));
				card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				card.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
				card.SizeFlagsStretchRatio = 1f;
				cardsRow.AddChild(card);
			}
		}

		// Small, centered, pinned under the cards.
		var backButton = new Button();
		backButton.Text = "Back";
		backButton.CustomMinimumSize = new Vector2(104, 48);
		backButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		backButton.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
		backButton.AddThemeFontSizeOverride("font_size", 13);
		backButton.Pressed += () =>
		{
			pendingEvolutionOption = null;
			BuildButtonsFrom(currentOptions);
		};
		FantasyGuiSkin.StyleButton(backButton, FantasyGuiSkin.IconExit);
		root.AddChild(backButton);
	}

	// Swaps between the normal level-up list and the evolution overlay. Hiding the normal view
	// wholesale takes its "Level Up! Choose a Spell:" heading, Reroll and Skip with it - an
	// evolution is not skippable, and rerolling would have silently dropped out of the view.
	private void SetNormalViewVisible(bool visible)
	{
		var normalView = GetNodeOrNull<Control>("Panel/VBoxContainer");
		if (normalView != null)
			normalView.Visible = visible;

		if (visible)
			RemoveEvolutionRoot();
	}

	// Renames before freeing because QueueFree defers to the end of the frame: without this, a
	// rebuild in the same frame would find the dying node by name and skip creating a new one.
	private void RemoveEvolutionRoot()
	{
		var existing = GetNodeOrNull<Control>($"Panel/{EvolutionRootName}");
		if (existing == null)
			return;

		existing.Name = $"{EvolutionRootName}_freeing";
		existing.QueueFree();
	}

	// The portrait mutation card, built the same way as a stacked level-up card: art in a fixed
	// left column, name / synergy badge / description / advice left-aligned beside it, and a
	// height that follows the text instead of a fixed 420 that would fit one card per screen.
	private Control BuildNarrowEvolutionCard(LevelUpOption option, SpellEvolutionOption evo, Action onPressed)
	{
		bool isAscension = evo.MilestoneLevel == 8;

		var root = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		root.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

		var card = new Button
		{
			CustomMinimumSize = new Vector2(0, NarrowRowMinHeight),
			ClipText = false,
			Text = string.Empty
		};
		card.Pressed += onPressed;
		ApplyEvolutionCardStyle(card, isAscension);
		root.AddChild(card);

		var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 10);
		margin.AddThemeConstantOverride("margin_bottom", 10);
		root.AddChild(margin);

		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 12);
		margin.AddChild(row);

		var iconFrame = new CenterContainer
		{
			CustomMinimumSize = new Vector2(NarrowRowIconColumn, NarrowRowIconColumn),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		iconFrame.AddChild(new TextureRect
		{
			Texture = evo.Icon ?? option.Icon ?? DefaultSpellIcon,
			CustomMinimumSize = new Vector2(NarrowRowIconColumn, NarrowRowIconColumn),
			ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = evo.ModulateColor != Colors.White ? evo.ModulateColor : (isAscension ? new Color(1.0f, 0.9f, 0.4f) : new Color(0.4f, 0.95f, 1.0f))
		});
		row.AddChild(iconFrame);

		var text = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		text.AddThemeConstantOverride("separation", 5);
		row.AddChild(text);

		var name = new Label
		{
			Text = evo.DisplayName,
			HorizontalAlignment = HorizontalAlignment.Left,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		name.AddThemeFontSizeOverride("font_size", 16);
		name.AddThemeColorOverride("font_color", isAscension ? new Color(1.0f, 0.9f, 0.3f) : new Color(0.5f, 1.0f, 0.9f));
		text.AddChild(name);

		if (!string.IsNullOrWhiteSpace(evo.SynergyTag))
		{
			var badge = new PanelContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			var badgeStyle = new StyleBoxFlat
			{
				BgColor = new Color(0f, 0f, 0f, 0f),
				BorderColor = isAscension ? new Color(0.9f, 0.75f, 0.2f) : new Color(0.2f, 0.85f, 0.7f)
			};
			badgeStyle.SetBorderWidthAll(1);
			badgeStyle.SetCornerRadiusAll(4);
			badgeStyle.SetContentMarginAll(4);
			badge.AddThemeStyleboxOverride("panel", badgeStyle);

			var badgeLabel = new Label
			{
				Text = $"[ {evo.SynergyTag} ]",
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			badgeLabel.AddThemeFontSizeOverride("font_size", 11);
			badgeLabel.AddThemeColorOverride("font_color", isAscension ? new Color(1.0f, 0.95f, 0.6f) : new Color(0.6f, 1.0f, 0.9f));
			badge.AddChild(badgeLabel);
			text.AddChild(badge);
		}

		if (!string.IsNullOrWhiteSpace(evo.Description))
		{
			var desc = new Label
			{
				Text = evo.Description,
				HorizontalAlignment = HorizontalAlignment.Left,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			desc.AddThemeFontSizeOverride("font_size", 12);
			desc.AddThemeColorOverride("font_color", new Color(0.88f, 0.92f, 0.98f));
			text.AddChild(desc);
		}

		if (!string.IsNullOrWhiteSpace(evo.SynergyDescription))
		{
			var advice = new Label
			{
				Text = $"💡 {evo.SynergyDescription}",
				HorizontalAlignment = HorizontalAlignment.Left,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			advice.AddThemeFontSizeOverride("font_size", 11);
			advice.AddThemeColorOverride("font_color", isAscension ? new Color(0.95f, 0.85f, 0.5f) : new Color(0.6f, 0.85f, 0.9f));
			text.AddChild(advice);
		}

		return root;
	}

	// Border and hover treatment shared by both mutation card shapes: gold for an ascension,
	// teal for an ordinary mutation, transparent fill either way.
	private static void ApplyEvolutionCardStyle(Button card, bool isAscension)
	{
		var cardStyle = new StyleBoxFlat();
		cardStyle.BgColor = CardFill;
		cardStyle.BorderColor = isAscension ? new Color(1.0f, 0.84f, 0.2f) : new Color(0.25f, 0.9f, 0.75f);
		cardStyle.SetBorderWidthAll(isAscension ? 3 : 2);
		cardStyle.SetCornerRadiusAll(6);

		var hoverStyle = cardStyle.Duplicate() as StyleBoxFlat;
		if (hoverStyle != null)
			hoverStyle.BgColor = new Color(0.16f, 0.19f, 0.26f, 0.72f);

		var pressedStyle = cardStyle.Duplicate() as StyleBoxFlat;
		if (pressedStyle != null)
			pressedStyle.BgColor = new Color(0.24f, 0.28f, 0.38f, 0.80f);

		card.AddThemeStyleboxOverride("normal", cardStyle);
		card.AddThemeStyleboxOverride("hover", hoverStyle ?? cardStyle);
		card.AddThemeStyleboxOverride("pressed", pressedStyle ?? cardStyle);
		card.AddThemeStyleboxOverride("focus", hoverStyle ?? cardStyle);
	}

	private Control BuildEvolutionOptionCard(LevelUpOption option, SpellEvolutionOption evo, Action onPressed)
	{
		bool isAscension = evo.MilestoneLevel == 8;

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 8);
		column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		// Fill the row vertically too, so the cards are the dominant element in the middle band
		// rather than sitting at their minimum height with dead space under them.
		column.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

		var card = new Button();
		card.CustomMinimumSize = new Vector2(CardWidth, CardHeight);
		card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		card.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		card.ClipText = false;
		card.Text = string.Empty;
		card.Pressed += onPressed;

		ApplyEvolutionCardStyle(card, isAscension);

		var content = new VBoxContainer();
		content.MouseFilter = Control.MouseFilterEnum.Ignore;
		content.AddThemeConstantOverride("separation", 6);
		content.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		content.OffsetLeft = 12;
		content.OffsetTop = 10;
		content.OffsetRight = -12;
		content.OffsetBottom = -10;
		card.AddChild(content);

		var iconFrame = new CenterContainer
		{
			CustomMinimumSize = new Vector2(0, 110),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		content.AddChild(iconFrame);

		var icon = new TextureRect
		{
			Texture = evo.Icon ?? option.Icon ?? DefaultSpellIcon,
			CustomMinimumSize = new Vector2(86, 86),
			ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = evo.ModulateColor != Colors.White ? evo.ModulateColor : (isAscension ? new Color(1.0f, 0.9f, 0.4f) : new Color(0.4f, 0.95f, 1.0f))
		};
		iconFrame.AddChild(icon);

		var title = new Label
		{
			Text = evo.DisplayName,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		title.AddThemeFontSizeOverride("font_size", 16);
		title.AddThemeColorOverride("font_color", isAscension ? new Color(1.0f, 0.9f, 0.3f) : new Color(0.5f, 1.0f, 0.9f));
		content.AddChild(title);

		if (!string.IsNullOrWhiteSpace(evo.SynergyTag))
		{
			var synergyBadge = new PanelContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			var badgeStyle = new StyleBoxFlat
			{
				BgColor = new Color(0f, 0f, 0f, 0f),
				BorderColor = isAscension ? new Color(0.9f, 0.75f, 0.2f) : new Color(0.2f, 0.85f, 0.7f)
			};
			badgeStyle.SetBorderWidthAll(1);
			badgeStyle.SetCornerRadiusAll(4);
			badgeStyle.SetContentMarginAll(4);
			synergyBadge.AddThemeStyleboxOverride("panel", badgeStyle);

			var badgeLabel = new Label
			{
				Text = $"[ {evo.SynergyTag} ]",
				HorizontalAlignment = HorizontalAlignment.Center,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			badgeLabel.AddThemeFontSizeOverride("font_size", 11);
			badgeLabel.AddThemeColorOverride("font_color", isAscension ? new Color(1.0f, 0.95f, 0.6f) : new Color(0.6f, 1.0f, 0.9f));
			synergyBadge.AddChild(badgeLabel);
			content.AddChild(synergyBadge);
		}

		if (!string.IsNullOrWhiteSpace(evo.Description))
		{
			var desc = new Label
			{
				Text = evo.Description,
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill
			};
			desc.AddThemeFontSizeOverride("font_size", 12);
			desc.AddThemeColorOverride("font_color", new Color(0.88f, 0.92f, 0.98f));
			content.AddChild(desc);
		}

		if (!string.IsNullOrWhiteSpace(evo.SynergyDescription))
		{
			var advice = new Label
			{
				Text = $"💡 {evo.SynergyDescription}",
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			advice.AddThemeFontSizeOverride("font_size", 11);
			advice.AddThemeColorOverride("font_color", isAscension ? new Color(0.95f, 0.85f, 0.5f) : new Color(0.6f, 0.85f, 0.9f));
			content.AddChild(advice);
		}

		column.AddChild(card);
		return column;
	}
}

