using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

public partial class LevelUpMenu : CanvasLayer
{
	[Signal] public delegate void WeaponSelectedEventHandler(string choice);
	[Signal] public delegate void RerollRequestedEventHandler();

	// The run-scoped charges. Each carries what it needs and nothing else: a ban and an augury name
	// a spell, saving a level-up names nothing.
	[Signal] public delegate void BanRequestedEventHandler(string spellId);
	[Signal] public delegate void BankRequestedEventHandler();
	[Signal] public delegate void AuguryRequestedEventHandler(string spellId);
	[Signal] public delegate void SkipRequestedEventHandler();
	[Signal] public delegate void SwapRequestedEventHandler(string newSpellId, string removedSpellId);
	[Signal] public delegate void RemoveRequestedEventHandler(string removedSpellId);
	private Button rerollButton = null!;
	private Button skipButton = null!;
	private List<LevelUpOption> currentOptions = new();
	private List<EquippedSpellInfo> currentEquippedSpells = new();
	private Dictionary<string, int> currentBaselineElementCounts = new();
	private int currentRerollsRemaining = 0;
	private LevelUpCharges currentCharges = new();
	private bool pendingBanSelection = false;
	private bool pendingAugurySelection = false;
	private HBoxContainer chargeRow;
	private Button banButton;
	private Button bankButton;
	private Button auguryButton;
	private LevelUpOption pendingSwapOption = null;
	private LevelUpOption pendingEvolutionOption = null;
	private bool pendingRemoveSelection = false;
	private const string EvolutionRootName = "EvolutionRoot";
	private const float CardWidth = 272f;
	private const float CardHeight = 468f;
	private const float UpgradeSectionWidth = 238f;
	private static readonly Vector2 IconFrameSize = new Vector2(0, 112);
	private static readonly Vector2 IconSize = new Vector2(88, 88);
	// A stacked card is a row, not a poster. In portrait every card is the full width of the
	// screen, so the desktop layout - a small icon floating in the middle above a thin centred
	// column of text - left most of the card empty on both sides and dead space underneath.
	// Narrow cards instead put the art in a fixed column on the left and read left-aligned
	// beside it, and take their height from their own content rather than a fixed card size.
	private const float NarrowRowIconColumn = 96f;

	/// <summary>The icon column, grown to suit however tall the card ended up.</summary>
	private float NarrowIconColumn()
	{
		return Mathf.Clamp(NarrowCardHeight() * 0.52f, NarrowRowIconColumn, 200f);
	}
	// A FLOOR, not the height. Three 132-tall rows in a panel a thousand units deep left the
	// bottom half of the level-up screen empty, which on the screen a player looks at more than any
	// other is the most valuable space in the game going unused. NarrowCardHeight works out what
	// each card can actually have and only falls back to this when the answer is too small to read.
	private const float NarrowRowMinHeight = 132f;

	// What the narrow layout spends on everything that is not a card: the heading, the charge row,
	// the Reroll/Skip bar, and the paddings between them. Measured against the shipping scene
	// rather than derived - the alternative is reading the ScrollContainer's height after layout,
	// which is not known at the moment the cards are built and would need a second pass.
	private const float NarrowChromeAllowance = 268f;
	// Clears the in-run HUD in portrait: the health frame (6 + 44), the XP bar under it, and the
	// element chip row below that, plus a little air. Node2DGame owns those; this only has to
	// stay out of their way now that the menu is see-through.
	private const float HudSafeTop = 112f;

	public override void _Ready()
	{
		CenterMenuPanel();
		EnsureNarrowOptionsScroll();
		ApplyMenuSkin();

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

		BuildChargeRow();
		BonelightSkin.StyleButton(rerollButton);
		BonelightSkin.StyleButton(skipButton);
		// Then the same gold frame the cards wear, so the two controls at the bottom of the screen
		// belong to it. Overridden here rather than changed in BonelightSkin: that skin dresses
		// every screen in the game, and this frame is drawn for this one.
		ApplyFrameToFooterButton(rerollButton);
		ApplyFrameToFooterButton(skipButton);
		GroupActionButtonsForNarrow();
		StyleHeading();
		FadeIn();

		var viewport = GetViewport();
		if (viewport != null)
		{
			viewport.SizeChanged += OnViewportSizeChanged;
		}

		EnsureAscensionButton();

		// The cards do not exist yet - SetOptions builds them, and re-builds them on every reroll -
		// so this attaches the navigator and SetOptions refreshes it. See the call at its end.
		MenuNavigator.Attach(this);
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

		ResponsiveLayout.SetFont(heading, ResponsiveLayout.TextRole.Display);
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

	private void ApplyMenuSkin()
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

		// The old 1140x700 ceiling left a wide screen framing a small panel in a lot of nothing.
		// The three cards are the screen, so the panel takes what the viewport will give it.
		Vector2 panelSize = new Vector2(
			Mathf.Min(1500f, Mathf.Max(860f, viewportSize.X - 52f)),
			Mathf.Min(940f, Mathf.Max(560f, viewportSize.Y - 40f)));

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

	// charges is optional so the two probe scenes that predate it (scripts/_LevelUpProbe.cs,
	// scripts/_UiShot.cs) keep compiling and simply show no charge bar.
	public void SetOptions(List<LevelUpOption> options = null, int rerollsRemaining = 0, List<EquippedSpellInfo> equippedSpells = null, Dictionary<string, int> baselineElementCounts = null, LevelUpCharges charges = null)
	{
		GD.Print("SetOptions called");
		currentCharges = charges ?? new LevelUpCharges { Rerolls = rerollsRemaining };
		currentOptions = options ?? new List<LevelUpOption>();
		currentEquippedSpells = equippedSpells ?? new List<EquippedSpellInfo>();
		currentBaselineElementCounts = baselineElementCounts ?? new Dictionary<string, int>();
		currentRerollsRemaining = rerollsRemaining;
		pendingSwapOption = null;
		pendingEvolutionOption = null;
		pendingRemoveSelection = false;
		pendingBanSelection = false;
		pendingAugurySelection = false;
		BuildButtonsFrom(currentOptions);
		UpdateRerollState(currentRerollsRemaining);
		UpdateChargeRow();
	}

	/// <summary>
	/// Updates the charge bar and returns to the normal options without touching the offer.
	/// </summary>
	/// <remarks>
	/// Separate from SetOptions because spending an augury must NOT re-roll the cards. Routing it
	/// through SetOptions would hand the player a free reroll every time they used one, which is a
	/// strictly better reroll that also costs a different charge.
	/// </remarks>
	public void RefreshCharges(LevelUpCharges charges)
	{
		currentCharges = charges ?? currentCharges;
		pendingBanSelection = false;
		pendingAugurySelection = false;
		BuildButtonsFrom(currentOptions);
		UpdateRerollState(currentRerollsRemaining);
		UpdateChargeRow();
	}

	/// <summary>The options currently on offer. Read by the balance harness, which picks one.</summary>
	/// <remarks>
	/// A dev tool needs the option DATA, not the buttons: choosing by policy (fill empty slots,
	/// then deepen the shallowest spell) rather than by pressing the first card is what stops a
	/// balance run reporting on card order instead of on the spells.
	/// </remarks>
	public IReadOnlyList<LevelUpOption> GetOfferedOptions() => currentOptions;

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
		else if (pendingBanSelection)
		{
			BuildBanSelectionButtons();
		}
		else if (pendingAugurySelection)
		{
			BuildAugurySelectionButtons();
		}
		else
		{
			BuildButtonsFrom(currentOptions);
		}
		UpdateRerollState(currentRerollsRemaining);
		UpdateChargeRow();
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

	// Built in code rather than in LevelUpMenu.tscn because the row is invisible for any player who
	// has bought none of the three upgrades, and a scene node that is hidden on almost every run is
	// worse to reason about than one that only exists when it is needed.
	private void BuildChargeRow()
	{
		if (rerollButton?.GetParent() is not Control vbox)
			return;

		chargeRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		chargeRow.AddThemeConstantOverride("separation", 8);

		banButton = MakeChargeButton("Ban", () => OnBanPressed());
		bankButton = MakeChargeButton("Save", () => OnBankPressed());
		auguryButton = MakeChargeButton("Augury", () => OnAuguryPressed());

		chargeRow.AddChild(banButton);
		chargeRow.AddChild(bankButton);
		chargeRow.AddChild(auguryButton);

		vbox.AddChild(chargeRow);
		vbox.MoveChild(chargeRow, rerollButton.GetIndex());
		chargeRow.Visible = false;
	}

	private Button MakeChargeButton(string label, System.Action onPressed)
	{
		var button = new Button
		{
			Text = label,
			CustomMinimumSize = new Vector2(104, 40),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
		};
		button.Pressed += () => onPressed();
		BonelightSkin.ApplyButtonSet(new[] { button }, 16);
		return button;
	}

	private void UpdateChargeRow()
	{
		if (chargeRow == null)
			return;

		// A charge with nothing left is hidden rather than disabled. A greyed-out button the player
		// can never use again is a permanent piece of dead furniture on a screen that is already
		// dense; the reroll button is disabled instead because it comes back every level.
		banButton.Visible = currentCharges.Bans > 0;
		bankButton.Visible = currentCharges.Banks > 0;
		auguryButton.Visible = currentCharges.Auguries > 0 && currentCharges.AuguryCandidates.Count > 0;

		banButton.Text = $"Ban ({currentCharges.Bans})";
		bankButton.Text = $"Save ({currentCharges.Banks})";
		auguryButton.Text = $"Augury ({currentCharges.Auguries})";

		bool anyVisible = banButton.Visible || bankButton.Visible || auguryButton.Visible;
		// Hidden entirely while a picker is open, so the player cannot start a second charge action
		// on top of an unfinished one.
		chargeRow.Visible = anyVisible && !pendingBanSelection && !pendingAugurySelection
			&& !pendingRemoveSelection && pendingSwapOption == null && pendingEvolutionOption == null;
	}

	private void OnBanPressed()
	{
		SfxPlayer.Global(SfxCatalog.UiOpen);
		pendingBanSelection = true;
		BuildBanSelectionButtons();
		UpdateChargeRow();
	}

	private void OnBankPressed()
	{
		SfxPlayer.Global(SfxCatalog.UiBack);
		EmitSignal(nameof(BankRequested));
		Hide();
	}

	private void OnAuguryPressed()
	{
		SfxPlayer.Global(SfxCatalog.UiOpen);
		pendingAugurySelection = true;
		BuildAugurySelectionButtons();
		UpdateChargeRow();
	}

	// Bans are chosen from the three cards on screen, not from the whole catalog. Banning something
	// you cannot see would be a spreadsheet exercise; banning the card that keeps crowding out the
	// one you want is the actual thing players want to do.
	private void BuildBanSelectionButtons()
	{
		ClearButtons();
		var container = GetOptionsContainer();
		if (container == null)
			return;

		var label = new Label
		{
			Text = "Strike a spell from the rest of the run:",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		container.AddChild(label);

		if (container is GridContainer grid)
			grid.Columns = GetResponsiveColumnCount(currentOptions.Count + 1);

		foreach (LevelUpOption option in currentOptions)
		{
			LevelUpOption captured = option;
			var btn = new Button
			{
				Text = captured.DisplayName,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(160, 52),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			};
			btn.Pressed += () => OnBanChoiceChosen(captured);
			BonelightSkin.ApplyButtonSet(new[] { btn }, 17);
			container.AddChild(btn);
		}

		container.AddChild(MakeCancelButton(() =>
		{
			pendingBanSelection = false;
			BuildButtonsFrom(currentOptions);
			UpdateChargeRow();
		}));
	}

	private void BuildAugurySelectionButtons()
	{
		ClearButtons();
		var container = GetOptionsContainer();
		if (container == null)
			return;

		var label = new Label
		{
			Text = "Name a spell to appear at your next level-up:",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		container.AddChild(label);

		if (container is GridContainer outerGrid)
			outerGrid.Columns = 1;

		// The candidate list runs to the whole unlocked roster late in a run, which is far taller
		// than the plate. A scroll is the only honest answer - capping the list would silently turn
		// "name a spell" into "name one of these twelve".
		var scroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0, 300),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		var grid = new GridContainer
		{
			Columns = GetResponsiveColumnCount(currentCharges.AuguryCandidates.Count),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		scroll.AddChild(grid);
		container.AddChild(scroll);

		foreach (LevelUpOption candidate in currentCharges.AuguryCandidates)
		{
			LevelUpOption captured = candidate;
			var btn = new Button
			{
				Text = captured.NextLevel > 1
					? $"{captured.DisplayName} (Lv {captured.NextLevel})"
					: captured.DisplayName,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(160, 48),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			};
			btn.Pressed += () => OnAuguryChoiceChosen(captured);
			BonelightSkin.ApplyButtonSet(new[] { btn }, 16);
			grid.AddChild(btn);
		}

		container.AddChild(MakeCancelButton(() =>
		{
			pendingAugurySelection = false;
			BuildButtonsFrom(currentOptions);
			UpdateChargeRow();
		}));
	}

	private Button MakeCancelButton(System.Action onPressed)
	{
		var cancel = new Button
		{
			Text = "Back",
			CustomMinimumSize = new Vector2(120, 40),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
		};
		cancel.Pressed += () =>
		{
			SfxPlayer.Global(SfxCatalog.UiBack);
			onPressed();
		};
		BonelightSkin.ApplyButtonSet(new[] { cancel }, 16);
		return cancel;
	}

	private void OnBanChoiceChosen(LevelUpOption option)
	{
		pendingBanSelection = false;
		SfxPlayer.Global(SfxCatalog.CardSelect);
		// The menu stays open: a ban is not a pick. Node2DGame re-rolls the offer and calls back in.
		EmitSignal(nameof(BanRequested), option.SpellId);
	}

	private void OnAuguryChoiceChosen(LevelUpOption option)
	{
		pendingAugurySelection = false;
		SfxPlayer.Global(SfxCatalog.CardSelect);
		EmitSignal(nameof(AuguryRequested), option.SpellId);
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
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			FollowFocus = true
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
			// Vertical too, or a wide card sits at its 468 minimum with dead panel under it.
			column.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

			// The element notes that used to sit here, and then inside the card, are gone
			// entirely: the tag chips carry the count and the effect text now, which is one row
			// instead of four saying the same thing. See BuildSpellTagChips.
			column.AddChild(ResponsiveLayout.IsNarrow(this)
				? BuildNarrowOptionCard(option)
				: BuildWideOptionCard(option));
			container.AddChild(column);
		}

		// The cards the pad was selecting have just been freed - by a level, a reroll or a ban - so
		// hand the selection to the new first card rather than leaving the screen inert. The
		// secondary flows (swap, evolution, erase) build their own buttons and are picked up by the
		// navigator's own rescan a few frames later.
		MenuNavigator.Attach(this);
	}

	// The desktop card: a poster. Art across the top, everything centred beneath it, a fixed
	// 272x420 so three sit side by side.
	private Control BuildWideOptionCard(LevelUpOption option)
	{
		var card = new Button
		{
			CustomMinimumSize = new Vector2(CardWidth, CardHeight),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
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

		// Flavour text is gone from the card by design - three paragraphs of prose is not something
		// a player reads while paused mid-fight, and it pushed the numbers they DO read off the
		// bottom. The stat line replaces it for a new spell; an upgrade shows its deltas instead.
		Control statLine = BuildOptionStatLine(option, HorizontalAlignment.Center);
		if (statLine != null)
		{
			statLine.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			content.AddChild(statLine);
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
			CustomMinimumSize = new Vector2(0, NarrowCardHeight()),
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

		float iconColumn = NarrowIconColumn();
		var iconFrame = new CenterContainer
		{
			CustomMinimumSize = new Vector2(iconColumn, iconColumn),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		iconFrame.AddChild(BuildOptionIcon(option, new Vector2(iconColumn, iconColumn)));
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

		Control wideStatLine = BuildOptionStatLine(option, HorizontalAlignment.Left);
		if (wideStatLine != null)
			text.AddChild(wideStatLine);

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
			// Deepened toward ink rather than lightened toward white: the card under it is
			// parchment now, so an element colour washed out with white is a pale mark on a pale
			// page. The element is still what picks the hue - only the direction changed.
			Modulate = GetDominantElementColor(option).Lerp(Ink, 0.28f)
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
		ResponsiveLayout.SetFont(title, ResponsiveLayout.TextRole.Title);
		// No override at all until the card became a page, so the spell name was drawn in the
		// theme's default light face - which on parchment is very nearly the parchment.
		title.AddThemeColorOverride("font_color", option.IsBoon ? InkGold : Ink);
		return title;
	}

	private Label BuildOptionTypeLabel(LevelUpOption option, HorizontalAlignment alignment)
	{
		var typeLabel = new Label
		{
			// A boon is neither. It never levels and takes no spell slot, so calling it "Attack"
			// next to a level-1-of-8 spell would be the card lying about what it is.
			// A boon says what it IS and what it costs you, because both are the thing that makes
			// it a different decision from the spell next to it: it is permanent, and it does not
			// take one of the six slots the player is rationing.
			Text = option.IsBoon ? "BOON - PERMANENT, NO SLOT"
				: option.IsPassive ? "Passive"
				: "Attack",
			HorizontalAlignment = alignment,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		ResponsiveLayout.SetFont(typeLabel, ResponsiveLayout.TextRole.Label);
		typeLabel.AddThemeColorOverride("font_color", option.IsBoon ? InkGold : InkSoft);
		return typeLabel;
	}

	// Was BuildOptionDescription. Same slot on the card, numbers instead of prose.
	//
	// Null for an upgrade: BuildUpgradeSection already prints what the level does, and printing the
	// base stats above the deltas would be two number blocks saying almost the same thing.
	private Label BuildOptionStatLine(LevelUpOption option, HorizontalAlignment alignment)
	{
		if (!option.IsNewUnlock || string.IsNullOrWhiteSpace(option.StatSummary))
			return null;

		var stats = new Label
		{
			Text = option.StatSummary,
			HorizontalAlignment = alignment,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		ResponsiveLayout.SetBodyText(stats);
		stats.AddThemeColorOverride("font_color", Ink);
		return stats;
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
		upgradeStyle.BorderColor = InkSoft;
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
		// No "Level Up" header. The line under it already reads "+1 jump, +12% damage per jump",
		// so the header restated the card's own purpose at the cost of a full line of vertical in
		// every card - and at the new type sizes that line is 18px, not 12. Emptying its Text was
		// tried first and does not work: a Label with no text still contributes its font's line
		// height, which showed up in the measured layout as a gap above every summary. The node
		// has to not exist.

		var upgradeSummary = new Label
		{
			Text = option.UpgradeSummary,
			HorizontalAlignment = HorizontalAlignment.Left,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		ResponsiveLayout.SetBodyText(upgradeSummary);
		upgradeSummary.AddThemeColorOverride("font_color", Rubric);
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

		// Gold leaf filled, a faint ink wash empty. The empty pip used to be pure black, which on
		// parchment reads as a hole rather than as a level not yet reached.
		var pipYellow = new Color(0.72f, 0.54f, 0.12f);
		for (int i = 0; i < total; i++)
		{
			var pip = new PanelContainer
			{
				CustomMinimumSize = new Vector2(13, 13),
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			var style = new StyleBoxFlat
			{
				BgColor = i < filled ? pipYellow : new Color(InkSoft, 0.16f),
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
	// One row of chips, and nothing else. This used to be a row of plain element chips PLUS a
	// stack of bordered notes underneath, each note repeating the element name with its progress
	// and a sentence - so a two-element spell spent four rows of the card saying the same two
	// words twice.
	//
	// The chip now carries the whole answer: the element, and what you would HAVE if you took this
	// (4/4, not +1). The count is what the player is actually deciding on, and reading it off a
	// delta means doing arithmetic on a paused screen. The sentence is still there, one press away,
	// on a shared line under the row.
	//
	// Nothing at all on an upgrade card. Levelling a spell you already own cannot change your tag
	// counts, so the chips would be a row of numbers that never move.
	private Control BuildSpellTagChips(LevelUpOption option, FlowContainer.AlignmentMode alignment)
	{
		if (option.SpellElementTags == null || option.SpellElementTags.Count == 0)
			return null;

		if (!option.IsNewUnlock)
			return null;

		var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		box.AddThemeConstantOverride("separation", 3);

		var row = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("h_separation", 3);
		row.AddThemeConstantOverride("v_separation", 3);
		row.Alignment = alignment;
		box.AddChild(row);

		var detail = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = alignment == FlowContainer.AlignmentMode.Center
				? HorizontalAlignment.Center
				: HorizontalAlignment.Left,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false
		};
		ResponsiveLayout.SetFont(detail, ResponsiveLayout.TextRole.Micro);
		detail.AddThemeColorOverride("font_color", Ink);
		box.AddChild(detail);

		// Which chip is open, per card. A second press closes it; a different chip swaps.
		string openElement = null;

		foreach (var pair in option.SpellElementTags.OrderByDescending(p => p.Value).ThenBy(p => p.Key))
		{
			string elementName = pair.Key;
			bool known = Enum.TryParse<Element>(elementName, out Element element);
			Color color = known ? ElementColors.GetColor(element) : new Color(0.5f, 0.5f, 0.5f);

			// The count AFTER taking this option, which is the number the decision turns on.
			int resulting = option.ResultingElementCounts != null
				&& option.ResultingElementCounts.TryGetValue(elementName, out int r)
				? r
				: pair.Value;

			var chip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
			var style = new StyleBoxFlat
			{
				BgColor = new Color(color.R, color.G, color.B, 0.88f),
				BorderColor = new Color(InkSoft, 0.45f)
			};
			style.SetBorderWidthAll(1);
			style.SetCornerRadiusAll(4);
			style.SetContentMarginAll(3);
			chip.AddThemeStyleboxOverride("panel", style);

			var label = new Label
			{
				Text = $"{elementName} {ElementPassiveDescriptions.GetProgressLabel(resulting)}",
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Micro);
			label.AddThemeColorOverride("font_color", GetReadableTextColor(color));
			chip.AddChild(label);

			string captured = elementName;
			int capturedCount = resulting;
			chip.GuiInput += @event =>
			{
				bool pressed = (@event is InputEventMouseButton mb && mb.Pressed
						&& mb.ButtonIndex == MouseButton.Left)
					|| (@event is InputEventScreenTouch touch && touch.Pressed);
				if (!pressed)
					return;

				if (openElement == captured)
				{
					openElement = null;
					detail.Visible = false;
				}
				else
				{
					openElement = captured;
					int tier = capturedCount >= 6 ? 6 : capturedCount >= 4 ? 4 : capturedCount >= 2 ? 2 : 0;
					detail.Text = ElementPassiveDescriptions.GetEffectText(captured, tier);
					detail.Visible = true;
				}

				SfxPlayer.Global(SfxCatalog.UiClick);
				chip.AcceptEvent();
			};

			row.AddChild(chip);
		}

		return box;
	}

	private static Color GetReadableTextColor(Color background)
	{
		float luminance = (background.R * 0.299f) + (background.G * 0.587f) + (background.B * 0.114f);
		return luminance > 0.62f ? new Color(0.06f, 0.06f, 0.07f) : Colors.White;
	}

	/// <summary>How tall one stacked option card should be so the three of them fill the panel.</summary>
	/// <remarks>
	/// Derived from the viewport rather than measured from the laid-out ScrollContainer, because
	/// the cards are built before that container has a size and a deferred second pass would make
	/// the screen visibly resettle. If the allowance is a little off the scroll absorbs it, which
	/// is the failure mode worth having.
	/// </remarks>
	private float NarrowCardHeight()
	{
		int count = Math.Max(1, currentOptions?.Count ?? 3);
		float available = ResponsiveLayout.ViewportSize(this).Y - HudSafeTop - NarrowChromeAllowance;
		float perCard = (available - NarrowCardSeparation * (count - 1)) / count;
		return MathF.Max(NarrowRowMinHeight, perCard);
	}

	private const float NarrowCardSeparation = 12f;

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

	// The card frame, drawn in tools/art/levelup_frames.py and sliced here.
	//
	// This was four runtime StyleBoxFlats: a 55%-alpha fill and a one-pixel border, floating over a
	// 62% dim over live gameplay. The swarm was legible straight through the text, and a flat
	// rounded rectangle reads as a placeholder on a screen the player looks at more than any other.
	// Now it is an opaque stone face inside a bevelled gold border, nine-sliced so the corner
	// ornaments keep their shape at any card size.
	private const int CardPatchMargin = 20;
	// Enough to clear the gold band and its inner shadow, so no glyph ever sits on the frame.
	private const int CardContentMargin = 18;
	// The card textures are deliberately NOT cached in statics, and this is the second time that
	// lesson has been paid for in this project.
	//
	// A Texture2D is a RefCounted. A static C# field holding one keeps the managed reference alive
	// past the point where the engine has torn the resource down, and the finalizer then trips
	// `FATAL: Condition "gchandle.is_released()" is true` - which surfaces as a Mono GC crash and
	// is really a dangling reference. Node2DGame instances this menu at startup, so the statics
	// were populated on every run: measured by bisect, the gameplay scene crashed 0 times in 4 at
	// the commit before this screen landed and 2 times in 4 at it.
	//
	// Loading per call costs nothing worth measuring: ResourceLoader keeps its own cache, so after
	// the first call this is a dictionary lookup.
	// THE SPELL CARDS ARE PAGES. A spell is a thing written in a book, and three on offer are three
	// pages out of the same book - so the card is torn parchment with a ruled text block, not the
	// carved stone slab it used to be. tools/art/ui_frames.py:torn_page_card draws them.
	//
	// The FOOTER keeps the stone. Reroll and Skip are not spells, they are the screen's furniture,
	// and giving them a torn edge would say they were the same kind of object as the choice above.
	// That is also the rule the whole UI runs on: manuscript inside stone, never the other way up.
	private const string CardTexturePath = "res://assets/bonelight/ui/ui-spell-page.png";
	private const string CardLitTexturePath = "res://assets/bonelight/ui/ui-spell-page-lit.png";
	private const string FooterTexturePath = "res://assets/bonelight/ui/levelup-card.png";
	private const string FooterLitTexturePath = "res://assets/bonelight/ui/levelup-card-hover.png";

	private static Texture2D CardTexture() => GD.Load<Texture2D>(CardTexturePath);

	private static Texture2D CardLitTexture() => GD.Load<Texture2D>(CardLitTexturePath);

	private static Texture2D FooterTexture() => GD.Load<Texture2D>(FooterTexturePath);

	private static Texture2D FooterLitTexture() => GD.Load<Texture2D>(FooterLitTexturePath);

	private static StyleBoxTexture BuildCardStyleBox(Texture2D texture, int contentMargin = CardContentMargin,
		bool tornEdge = false)
	{
		var style = new StyleBoxTexture { Texture = texture };

		// A TORN EDGE HAS TO TILE, NOT STRETCH. A nine-slice stretches its edge bands, and a
		// stretched deckle smears into one long taper - the tear simply disappears at any size
		// other than the one it was drawn at. Tiling repeats the band instead, and the tear tables
		// in ui_frames.py are the length of the band precisely so the repeat is seamless.
		if (tornEdge)
		{
			style.AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile;
			style.AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Tile;
		}

		style.TextureMarginLeft = CardPatchMargin;
		style.TextureMarginTop = CardPatchMargin;
		style.TextureMarginRight = CardPatchMargin;
		style.TextureMarginBottom = CardPatchMargin;
		style.ContentMarginLeft = contentMargin;
		style.ContentMarginTop = contentMargin;
		style.ContentMarginRight = contentMargin;
		style.ContentMarginBottom = contentMargin;
		return style;
	}

	// The four style boxes this screen uses, built once per menu and shared by every control that
	// wants one. A StyleBox is a RefCounted, so building a fresh pair per card meant the level-up
	// screen produced a small pile of short-lived C# wrappers around engine objects every time it
	// opened - and a wrapper that becomes garbage while the engine is tearing down is what trips
	// `gchandle.is_released()`. Sharing them is also simply correct: all the cards want the same
	// frame, and Godot is happy for one StyleBox to be referenced by many controls.
	//
	// INSTANCE fields, not static. Static would keep them alive past the engine's own reference,
	// which is the opposite failure and the one that took this scene from 0 crashes in 4 runs to
	// 5 in 5.
	private StyleBoxTexture cardStyleResting;
	private StyleBoxTexture cardStyleLit;
	private StyleBoxTexture footerStyleResting;
	private StyleBoxTexture footerStyleLit;

	private void EnsureCardStyles()
	{
		cardStyleResting ??= BuildCardStyleBox(CardTexture(), CardContentMargin, tornEdge: true);
		cardStyleLit ??= BuildCardStyleBox(CardLitTexture(), CardContentMargin, tornEdge: true);
		footerStyleResting ??= BuildCardStyleBox(FooterTexture(), 10);
		footerStyleLit ??= BuildCardStyleBox(FooterLitTexture(), 10);
	}

	// Same frame, tighter inside: a footer button is one line of text, not a card of content.
	private void ApplyFrameToFooterButton(Button button)
	{
		if (button == null)
			return;

		EnsureCardStyles();
		button.AddThemeStyleboxOverride("normal", footerStyleResting);
		button.AddThemeStyleboxOverride("hover", footerStyleLit);
		button.AddThemeStyleboxOverride("pressed", footerStyleLit);
		button.AddThemeStyleboxOverride("focus", footerStyleLit);
	}

	// Warm gold against the stone plate every other card wears.
	private static readonly Color BoonAccent = new Color(1.0f, 0.84f, 0.42f);
	private static readonly Color BoonCardTint = new Color(1.0f, 0.97f, 0.90f);

	// An Ultimate Ascension card shines. Nothing else on the screen does.
	//
	// A level 8 with an ascension behind it is the biggest single choice in a run, and until now it
	// looked exactly like the +2 damage next to it - the only cue was a line of text the player has
	// to stop and read. Motion is the one cue that survives a glance at a paused screen full of
	// text, so the card that matters is the card that moves.
	//
	// Two effects, deliberately quiet on their own and unmistakable together:
	//   * a band of light sweeping across the face, the standard "this one is rare" language
	//   * a slow gold breath on the whole card, so it still reads as charged between sweeps
	//
	// Only when the ascension is actually AVAILABLE. Player.MarkUnearnedAscensions clears
	// IsEvolutionMilestone when every branch is locked behind element requirements, so an
	// unearned level 8 stays an ordinary card - the shine promises something, and it has to be
	// telling the truth.
	private void ApplyAscensionShine(Button card, LevelUpOption option)
	{
		if (!option.IsEvolutionMilestone || option.MilestoneLevel != 8)
			return;

		// So the sweep is cut off at the frame instead of running out over the plate.
		card.ClipContents = true;

		var sheen = new ColorRect
		{
			Name = "AscensionSheen",
			Color = new Color(1.0f, 0.94f, 0.70f, 0.20f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			// Taller than the card so the band never shows a top or bottom edge.
			AnchorTop = -0.15f,
			AnchorBottom = 1.15f,
			AnchorLeft = SheenStart,
			AnchorRight = SheenStart + SheenWidth,
		};
		card.AddChild(sheen);

		// Anchors rather than pixel offsets, because a Control's size is not known until the
		// container has laid it out and this runs while the card is still being built. Anchors are
		// fractions of the parent, so the sweep is correct at any card width without waiting for
		// a resize or measuring anything.
		Tween sweep = sheen.CreateTween();
		sweep.SetLoops();
		sweep.TweenInterval(SheenRestSeconds);
		sweep.SetParallel(true);
		sweep.TweenProperty(sheen, "anchor_left", SheenEnd, SheenSweepSeconds)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		sweep.TweenProperty(sheen, "anchor_right", SheenEnd + SheenWidth, SheenSweepSeconds)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		sweep.SetParallel(false);
		// Snapped back rather than swept back, so it reads as one pass repeating instead of a
		// band sliding to and fro.
		sweep.TweenCallback(Callable.From(() =>
		{
			if (!IsInstanceValid(sheen))
				return;
			sheen.AnchorLeft = SheenStart;
			sheen.AnchorRight = SheenStart + SheenWidth;
		}));

		Tween breath = card.CreateTween();
		breath.SetLoops();
		breath.TweenProperty(card, "modulate", AscensionGlow, SheenBreathSeconds)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		breath.TweenProperty(card, "modulate", Colors.White, SheenBreathSeconds)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}

	private const float SheenStart = -0.30f;
	private const float SheenEnd = 1.02f;
	private const float SheenWidth = 0.22f;
	private const float SheenSweepSeconds = 0.85f;
	private const float SheenRestSeconds = 1.25f;
	private const float SheenBreathSeconds = 1.05f;
	private static readonly Color AscensionGlow = new Color(1.0f, 0.92f, 0.74f);

	private void ApplyOptionCardStyle(Button card, LevelUpOption option)
	{
		EnsureCardStyles();

		// An upgrade to a spell already held wears the lit frame as its resting state. The old
		// styling carried that distinction on a cyan border, which the gold frame replaces; saying
		// it with light instead keeps one frame design and still tells the two apart at a glance.
		bool isUpgrade = !option.IsNewUnlock;
		StyleBoxTexture resting = isUpgrade ? cardStyleLit : cardStyleResting;

		card.AddThemeStyleboxOverride("normal", resting);
		card.AddThemeStyleboxOverride("hover", cardStyleLit);
		card.AddThemeStyleboxOverride("pressed", cardStyleLit);
		card.AddThemeStyleboxOverride("focus", cardStyleLit);

		// A boon is a different KIND of thing, not a different spell, and three identically framed
		// cards hid that completely - the only cue was the word "Boon" in the same grey as "Attack".
		// Tinting the whole frame is the cue that survives being glanced at, which is the only way
		// a level-up card is ever read.
		card.Modulate = option.IsBoon ? BoonCardTint : Colors.White;

		ApplyAscensionShine(card, option);
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

		// "Full-width rows" is not what one column gets you on its own. A GridContainer sizes a
		// column to the widest *minimum* among its children and hands the leftover space out only
		// to children that ask for it, so without an expand flag this column collapsed to 160px -
		// the Back button's CustomMinimumSize - inside a 656px container. Everything else was then
		// crushed into that ribbon: the header wrapped to 160x247, and each spell name wrapped to
		// roughly one character per line in a 33px column. Both rows below therefore have to
		// expand; the Back button deliberately does not, so it stays its own width and centred.
		var header = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		header.AddThemeConstantOverride("separation", 4);

		var label = new Label();
		label.Text = "Erase a spell from your tome";
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Display);
		header.AddChild(label);

		var prompt = new Label();
		prompt.Text = $"Make room for {newOption.DisplayName} - choose a spell to forget.";
		prompt.HorizontalAlignment = HorizontalAlignment.Center;
		prompt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		ResponsiveLayout.SetBodyText(prompt);
		prompt.AddThemeColorOverride("font_color", TypeLabelColor);
		header.AddChild(prompt);

		container.AddChild(header);

		var cardGrid = new GridContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
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
		BonelightSkin.StyleButton(backButton);
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
		ResponsiveLayout.SetFont(name, ResponsiveLayout.TextRole.Title);
		content.AddChild(name);

		var level = new Label
		{
			Text = $"Lv {equipped.CurrentLevel}",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		ResponsiveLayout.SetFont(level, ResponsiveLayout.TextRole.Label);
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
		ResponsiveLayout.SetFont(name, ResponsiveLayout.TextRole.Title);
		row.AddChild(name);

		var level = new Label
		{
			Text = $"Lv {equipped.CurrentLevel}",
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		ResponsiveLayout.SetFont(level, ResponsiveLayout.TextRole.Label);
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
			BonelightSkin.ApplyButtonSet(new[] { btn }, 17);
			container.AddChild(btn);
		}
	}

	// The evolution view is laid out as its own full-rect overlay on the Panel rather than inside
	// the normal view's 3-column options grid. The grid positions children as grid cells, which
	// left the title floating in dead space and the Back button stranded mid-screen; owning the
	// whole panel lets the title pin to the top, the cards fill the middle, and Back sit at the
	// bottom. The normal view is hidden wholesale while this is up.
	// Greys out an ascension the player has not earned and stamps the requirement across it.
	//
	// Done as a pass OVER the finished card rather than as a parameter threaded into the two card
	// builders, because there are two of them (narrow and wide) with different internals and the
	// lock has nothing to do with how either lays itself out. Whatever the card is made of, this
	// finds its buttons and turns them off.
	private void ApplyAscensionLock(Control card, LevelUpOption option, SpellEvolutionOption evo)
	{
		if (card == null || evo == null || option?.EvolutionLockReason == null)
			return;

		if (!option.EvolutionLockReason.TryGetValue(evo.Id, out string reason) || string.IsNullOrEmpty(reason))
			return;

		card.Modulate = new Color(0.80f, 0.77f, 0.72f);
		DisableButtonsIn(card);

		// Anchored over the card rather than appended into it, so it lands in the same place on
		// both card layouts and cannot push either one's contents around.
		var stamp = new Label
		{
			Text = reason,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		stamp.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		stamp.OffsetTop = -46;
		stamp.OffsetBottom = -8;
		stamp.OffsetLeft = 8;
		stamp.OffsetRight = -8;
		ResponsiveLayout.SetFont(stamp, ResponsiveLayout.TextRole.Label);
		stamp.AddThemeColorOverride("font_color", Rubric);
		card.AddChild(stamp);
	}

	private static void DisableButtonsIn(Node node)
	{
		if (node is BaseButton button)
			button.Disabled = true;

		foreach (Node child in node.GetChildren())
			DisableButtonsIn(child);
	}

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

		// THE PANEL BECOMES A PAGE. This is the one screen in a run where the player is reading
		// about what their magic is turning into rather than picking the next number to go up, so
		// it is the screen the manuscript register exists for.
		ShowVellumPage(true);

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
		ResponsiveLayout.SetFont(title, ResponsiveLayout.TextRole.Display);
		title.AddThemeColorOverride("font_color", isAscension ? InkGold : Ink);
		headerBox.AddChild(title);

		var subtitle = new Label();
		// COUNT THE BRANCHES, do not assert a number. These read "1 of 2" and "1 of 3" as
		// literals, so a milestone offering a different number of branches - which the catalog is
		// free to do - told the player something false at the exact moment they were being asked
		// to compare what was on screen. Caught by measuring this screen with a two-branch sample
		// and reading back "Choose 1 of 3".
		int branchCount = option?.EvolutionChoices?.Count ?? 0;
		subtitle.Text = isAscension
			? $"Choose 1 of {branchCount} ultimate build-defining evolutions for {option.DisplayName}:"
			: $"Choose 1 of {branchCount} mechanical modifications to mutate {option.DisplayName}:";
		subtitle.HorizontalAlignment = HorizontalAlignment.Center;
		subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		ResponsiveLayout.SetBodyText(subtitle);
		subtitle.AddThemeColorOverride("font_color", InkSoft);
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
				HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
				FollowFocus = true
			};
			root.AddChild(scroll);

			var cardsColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			cardsColumn.AddThemeConstantOverride("separation", 12);
			scroll.AddChild(cardsColumn);

			foreach (var evo in option.EvolutionChoices)
			{
				Control card = BuildNarrowEvolutionCard(option, evo, () => OnEvolutionChosen(option, evo));
				ApplyAscensionLock(card, option, evo);
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
				ApplyAscensionLock(card, option, evo);
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
		ResponsiveLayout.SetFont(backButton, ResponsiveLayout.TextRole.Label);
		backButton.Pressed += () =>
		{
			pendingEvolutionOption = null;
			BuildButtonsFrom(currentOptions);
		};
		BonelightSkin.StyleButton(backButton);
		root.AddChild(backButton);
	}

	// Swaps between the normal level-up list and the evolution overlay. Hiding the normal view
	// wholesale takes its "Level Up! Choose a Spell:" heading, Reroll and Skip with it - an
	// evolution is not skippable, and rerolling would have silently dropped out of the view.

	// ---- the ascension browser ---------------------------------------------------------------
	//
	// An ascension is the biggest single decision in a run, and until now the player met each one
	// by surprise. Nothing anywhere said which of their spells HAD one, what it wanted, or how
	// close they were - the requirement only ever appeared stamped across a card they were already
	// being shown, at the one moment it was too late to go and earn it.
	//
	// This reports; it never grants. Everything in it is read from Player.BuildAscensionPreview,
	// which owns the rule, so the browser cannot drift out of agreement with the screen that
	// actually offers the choice.

	// THE PAGE HAS TO GO ON TOP OF THE PLATE, NOT UNDER IT.
	//
	// This is why the level 4 screen was unreadable. Both overlays asked for the manuscript register
	// with BonelightSkin.ApplyPanel, and on a plain Control that helper inserts its backdrop as
	// child ZERO - which on this scene is underneath "Plate", an opaque nine-patch of stone brick.
	// The vellum was drawn on every one of those screens and never once seen, so every ink colour
	// on them was landing on stonework instead of on parchment. Darkening the text alone would have
	// made it worse.
	//
	// Inset inside the plate rather than filling it, so the stone stays the outer edge and the page
	// is laid on it - which is the rule the whole UI runs on: manuscript inside stone.
	private const string PageBackdropName = "VellumPage";
	private const float PageInset = 18f;

	private void ShowVellumPage(bool visible)
	{
		var panel = GetNodeOrNull<Control>("Panel");
		if (panel == null)
			return;

		var page = panel.GetNodeOrNull<Panel>(PageBackdropName);
		if (page == null)
		{
			if (!visible)
				return;

			page = new Panel { Name = PageBackdropName, MouseFilter = Control.MouseFilterEnum.Ignore };
			page.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			page.OffsetLeft = PageInset;
			page.OffsetTop = PageInset;
			page.OffsetRight = -PageInset;
			page.OffsetBottom = -PageInset;
			page.AddThemeStyleboxOverride("panel", BonelightSkin.PanelStyle(BonelightSkin.Register.Vellum));
			panel.AddChild(page);

			// Directly after the plate. Anything added later - the overlay's own content - is
			// appended after this and so draws on the page rather than under it.
			int plateIndex = panel.GetNodeOrNull<Control>("Plate")?.GetIndex() ?? -1;
			panel.MoveChild(page, plateIndex + 1);
		}

		page.Visible = visible;
	}

	private const string AscensionRootName = "AscensionBrowser";
	private Button ascensionButton;

	private void EnsureAscensionButton()
	{
		if (ascensionButton != null && GodotObject.IsInstanceValid(ascensionButton))
			return;

		var panel = GetNodeOrNull<Control>("Panel");
		if (panel == null)
			return;

		ascensionButton = new Button
		{
			Name = "AscensionsButton",
			Text = "\u2605 Ascensions",
			CustomMinimumSize = new Vector2(156, ResponsiveLayout.MinTouchTarget),
		};

		// Pinned to the panel's top right corner, clear of the centred heading.
		ascensionButton.AnchorLeft = 1f;
		ascensionButton.AnchorRight = 1f;
		ascensionButton.AnchorTop = 0f;
		ascensionButton.AnchorBottom = 0f;
		// Inset far enough to clear the plate's own carved border, or the button reads as sitting
		// half off the edge of the screen.
		ascensionButton.OffsetLeft = -(156 + 20);
		ascensionButton.OffsetRight = -20;
		ascensionButton.OffsetTop = 18;
		ascensionButton.OffsetBottom = 18 + ResponsiveLayout.MinTouchTarget;

		ResponsiveLayout.SetFont(ascensionButton, ResponsiveLayout.TextRole.Label);
		// Normal, not Quiet. Quiet rests on the UNLIT frame, which is the same frame a disabled
		// button wears - on a screen where everything else is bright parchment it read as greyed
		// out, which is the one thing this button must not look like.
		BonelightSkin.StyleButton(ascensionButton, BonelightSkin.Emphasis.Normal);
		ascensionButton.Pressed += BuildAscensionBrowser;
		panel.AddChild(ascensionButton);
	}

	private void BuildAscensionBrowser()
	{
		var panel = GetNodeOrNull<Control>("Panel");
		if (panel == null)
			return;

		ClearButtons();
		SetNormalViewVisible(false);
		RemoveAscensionRoot();

		// Same register as the evolution screen: this is reading ABOUT magic, so it is a page.
		ShowVellumPage(true);

		var root = new VBoxContainer { Name = AscensionRootName };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.OffsetLeft = 24;
		root.OffsetTop = 20;
		root.OffsetRight = -24;
		root.OffsetBottom = -20;
		root.AddThemeConstantOverride("separation", 14);
		panel.AddChild(root);

		var title = new Label
		{
			Text = "\u2605 ULTIMATE ASCENSIONS \u2605",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		ResponsiveLayout.SetFont(title, ResponsiveLayout.TextRole.Display);
		title.AddThemeColorOverride("font_color", InkGold);
		root.AddChild(title);

		var subtitle = new Label
		{
			Text = $"Every spell in your tome, and what its ascension still wants. " +
				$"An ascension is offered when the spell reaches level {Player.AscensionSpellLevel}.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		ResponsiveLayout.SetBodyText(subtitle);
		subtitle.AddThemeColorOverride("font_color", InkSoft);
		root.AddChild(subtitle);

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			FollowFocus = true,
		};
		root.AddChild(scroll);

		var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		column.AddThemeConstantOverride("separation", 14);
		scroll.AddChild(column);

		var spells = currentEquippedSpells ?? new List<EquippedSpellInfo>();
		if (spells.Count == 0)
		{
			var empty = new Label
			{
				Text = "Your tome is empty. Take a spell and come back.",
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			};
			ResponsiveLayout.SetBodyText(empty);
			empty.AddThemeColorOverride("font_color", InkSoft);
			column.AddChild(empty);
		}
		else
		{
			foreach (EquippedSpellInfo spell in spells)
				column.AddChild(BuildAscensionSpellSection(spell));
		}

		var backButton = new Button
		{
			Text = "Back",
			CustomMinimumSize = new Vector2(104, 48),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
		};
		ResponsiveLayout.SetFont(backButton, ResponsiveLayout.TextRole.Label);
		backButton.Pressed += () => BuildButtonsFrom(currentOptions);
		BonelightSkin.StyleButton(backButton);
		root.AddChild(backButton);

		// The cards this screen just replaced were what the pad was pointing at.
		MenuNavigator.Attach(this, backButton);
	}

	/// <summary>One spell's heading and every branch beneath it.</summary>
	private Control BuildAscensionSpellSection(EquippedSpellInfo spell)
	{
		var section = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		section.AddThemeConstantOverride("separation", 6);

		var header = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		header.AddThemeConstantOverride("separation", 10);
		section.AddChild(header);

		if (spell.Icon != null)
		{
			header.AddChild(new TextureRect
			{
				Texture = spell.Icon,
				CustomMinimumSize = new Vector2(40, 40),
				ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			});
		}

		var name = new Label
		{
			Text = $"{spell.DisplayName}   (level {spell.CurrentLevel}/{Player.AscensionSpellLevel})",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center,
		};
		ResponsiveLayout.SetFont(name, ResponsiveLayout.TextRole.Title);
		name.AddThemeColorOverride("font_color", Ink);
		header.AddChild(name);

		var branches = spell.Ascensions ?? new List<AscensionInfo>();
		if (branches.Count == 0)
		{
			// Passives, and any spell the catalog has no level 8 entry for. Saying so is the point:
			// a spell missing from this list would read as an oversight rather than as an answer.
			var none = new Label
			{
				Text = spell.IsPassive
					? "A passive boon. It does not ascend."
					: "No ascension is written for this spell yet.",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			};
			ResponsiveLayout.SetBodyText(none);
			none.AddThemeColorOverride("font_color", InkSoft);
			section.AddChild(none);
			return section;
		}

		foreach (AscensionInfo branch in branches)
			section.AddChild(BuildAscensionBranchCard(branch));

		return section;
	}

	private Control BuildAscensionBranchCard(AscensionInfo branch)
	{
		var card = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		var style = new StyleBoxFlat
		{
			BgColor = new Color(InkSoft, branch.IsAvailable ? 0.10f : 0.05f),
			BorderColor = branch.IsAvailable ? InkGold : new Color(InkSoft, 0.45f),
		};
		style.SetBorderWidthAll(branch.IsAvailable ? 2 : 1);
		style.SetCornerRadiusAll(4);
		style.SetContentMarginAll(10);
		card.AddThemeStyleboxOverride("panel", style);

		// Faded rather than darkened, for the same reason the locked level 8 card is: multiplying a
		// parchment page toward black reads as a hole in it.
		if (!branch.IsAvailable)
			card.Modulate = new Color(0.80f, 0.77f, 0.72f);

		var body = new VBoxContainer();
		body.AddThemeConstantOverride("separation", 4);
		card.AddChild(body);

		var name = new Label
		{
			Text = branch.DisplayName,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		ResponsiveLayout.SetFont(name, ResponsiveLayout.TextRole.Label);
		name.AddThemeColorOverride("font_color", branch.IsAvailable ? InkGold : Ink);
		body.AddChild(name);

		if (!string.IsNullOrWhiteSpace(branch.Description))
		{
			var desc = new Label
			{
				Text = branch.Description,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			};
			ResponsiveLayout.SetBodyText(desc);
			desc.AddThemeColorOverride("font_color", Ink);
			body.AddChild(desc);
		}

		var status = new Label
		{
			// Every unmet requirement, not just the first. A player one element short AND three
			// levels short needs to know both, or they chase the wrong one.
			Text = branch.IsAvailable
				? $"Ready \u2014 offered when this spell reaches level {Player.AscensionSpellLevel}."
				: "Still needed:  " + string.Join("   \u2022   ", branch.UnmetRequirements),
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		ResponsiveLayout.SetFont(status, ResponsiveLayout.TextRole.Micro);
		status.AddThemeColorOverride("font_color", branch.IsAvailable ? InkGold : Rubric);
		body.AddChild(status);

		return card;
	}

	private void SetNormalViewVisible(bool visible)
	{
		var normalView = GetNodeOrNull<Control>("Panel/VBoxContainer");
		if (normalView != null)
			normalView.Visible = visible;

		// The button is anchored to the Panel rather than living inside the normal view, so that it
		// stays pinned to the corner whatever the options do. That also means hiding the normal
		// view does not hide it, and it would otherwise float over both overlays.
		if (ascensionButton != null && GodotObject.IsInstanceValid(ascensionButton))
			ascensionButton.Visible = visible;

		if (visible)
		{
			RemoveEvolutionRoot();
			RemoveAscensionRoot();
			ShowVellumPage(false);
		}
	}

	private void RemoveAscensionRoot()
	{
		var existing = GetNodeOrNull<Control>($"Panel/{AscensionRootName}");
		if (existing == null)
			return;

		existing.Name = $"{AscensionRootName}_freeing";
		existing.QueueFree();
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
			Modulate = evo.ModulateColor != Colors.White ? evo.ModulateColor : (isAscension ? IconGold : IconTeal)
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
		ResponsiveLayout.SetFont(name, ResponsiveLayout.TextRole.Title);
		name.AddThemeColorOverride("font_color", isAscension ? InkGold : Ink);
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
				BorderColor = isAscension ? InkGold : InkSoft
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
			ResponsiveLayout.SetFont(badgeLabel, ResponsiveLayout.TextRole.Micro);
			badgeLabel.AddThemeColorOverride("font_color", InkSoft);
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
			ResponsiveLayout.SetBodyText(desc);
			desc.AddThemeColorOverride("font_color", Ink);
			text.AddChild(desc);
		}

		Label narrowElements = BuildEvolutionElementLine(evo, option, HorizontalAlignment.Left);
		if (narrowElements != null)
			text.AddChild(narrowElements);

		if (!string.IsNullOrWhiteSpace(evo.SynergyDescription))
		{
			var advice = new Label
			{
				Text = $"💡 {evo.SynergyDescription}",
				HorizontalAlignment = HorizontalAlignment.Left,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			ResponsiveLayout.SetFont(advice, ResponsiveLayout.TextRole.Label);
			// RUBRICATED. Red ink for the line that matters most is the oldest emphasis there is,
			// and it is the one colour on the page that is not ink or gold.
			advice.AddThemeColorOverride("font_color", Rubric);
			text.AddChild(advice);
		}

		return root;
	}

	// Border and hover treatment shared by both mutation card shapes: gold for an ascension,
	// teal for an ordinary mutation, transparent fill either way.
	// The parchment ramp, from .ai/art-direction.md section 3 - `flesh` is the contract's name
	// for it; on this screen it is vellum and ink. Rubric is the `red` row, which is what a scribe
	// reached for when a line mattered more than the ones around it.
	private static readonly Color Ink = new Color(0.235f, 0.141f, 0.212f);
	private static readonly Color InkSoft = new Color(0.431f, 0.267f, 0.314f);
	private static readonly Color InkGold = new Color(0.329f, 0.247f, 0.063f);
	private static readonly Color Rubric = new Color(0.541f, 0.188f, 0.220f);

	// Icon tints for the two milestones, deep enough to read on cream. The pale gold and pale cyan
	// these replaced were chosen when this screen was dark, and on a page they washed out to
	// almost nothing - which is most of why a level 4 card was hard to read even where the words
	// were legible. Only the FALLBACK: an evolution that sets its own ModulateColor still wins.
	private static readonly Color IconGold = new Color(0.72f, 0.54f, 0.12f);
	private static readonly Color IconTeal = new Color(0.13f, 0.42f, 0.48f);

	// A ruled block of text on the page, gold-leafed at the corners. The ascension choice rests on
	// the LIT frame rather than getting a different border colour, so the rarer milestone is the
	// one catching more light - the same idea the stone cards use for hover, which is what keeps
	// one frame design doing two jobs.
	private static void ApplyEvolutionCardStyle(Button card, bool isAscension)
	{
		StyleBoxTexture Frame(string file)
		{
			var box = new StyleBoxTexture { Texture = GD.Load<Texture2D>("res://assets/bonelight/ui/" + file) };
			box.TextureMarginLeft = 24;
			box.TextureMarginTop = 24;
			box.TextureMarginRight = 24;
			box.TextureMarginBottom = 24;
			box.ContentMarginLeft = 16;
			box.ContentMarginTop = 14;
			box.ContentMarginRight = 16;
			box.ContentMarginBottom = 14;
			return box;
		}

		StyleBoxTexture resting = Frame(isAscension ? "ui-page-card-lit.png" : "ui-page-card.png");
		StyleBoxTexture lit = Frame("ui-page-card-lit.png");
		card.AddThemeStyleboxOverride("normal", resting);
		card.AddThemeStyleboxOverride("hover", lit);
		card.AddThemeStyleboxOverride("pressed", lit);
		card.AddThemeStyleboxOverride("focus", lit);
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
			Modulate = evo.ModulateColor != Colors.White ? evo.ModulateColor : (isAscension ? IconGold : IconTeal)
		};
		iconFrame.AddChild(icon);

		var title = new Label
		{
			Text = evo.DisplayName,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		ResponsiveLayout.SetFont(title, ResponsiveLayout.TextRole.Title);
		title.AddThemeColorOverride("font_color", isAscension ? InkGold : Ink);
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
				BorderColor = isAscension ? InkGold : InkSoft
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
			ResponsiveLayout.SetFont(badgeLabel, ResponsiveLayout.TextRole.Micro);
			badgeLabel.AddThemeColorOverride("font_color", InkSoft);
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
			ResponsiveLayout.SetBodyText(desc);
			desc.AddThemeColorOverride("font_color", Ink);
			content.AddChild(desc);
		}

		Label wideElements = BuildEvolutionElementLine(evo, option, HorizontalAlignment.Center);
		if (wideElements != null)
			content.AddChild(wideElements);

		if (!string.IsNullOrWhiteSpace(evo.SynergyDescription))
		{
			var advice = new Label
			{
				Text = $"💡 {evo.SynergyDescription}",
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			ResponsiveLayout.SetFont(advice, ResponsiveLayout.TextRole.Label);
			advice.AddThemeColorOverride("font_color", Rubric);
			content.AddChild(advice);
		}

		column.AddChild(card);
		return column;
	}
	/// <summary>
	/// The element an evolution adds, spelled out on its card. Without this the most consequential
	/// half of the level 4 and level 8 choice is invisible: whether the branch deepens the spell's
	/// element - keeping it attuned and pushing its tier up - or trades attunement for a second one.
	/// Returns null for the untagged options, which is most of them.
	/// </summary>
	private static Label BuildEvolutionElementLine(SpellEvolutionOption evo, LevelUpOption option, HorizontalAlignment alignment)
	{
		if (evo?.BonusElementWeights == null || evo.BonusElementWeights.Count == 0)
			return null;

		// The spell's existing tags, so the card can say which way this branch goes. Empty when the
		// preview data was not filled in, in which case the line still reports the gain and simply
		// says nothing about attunement rather than guessing.
		Dictionary<string, int> current = option?.SpellElementTags ?? new Dictionary<string, int>();
		var parts = new List<string>();
		bool deepens = false;
		bool branches = false;

		foreach (var pair in evo.BonusElementWeights)
		{
			bool alreadyCarried = current.ContainsKey(pair.Key);
			deepens |= alreadyCarried && current.Count == 1;
			branches |= !alreadyCarried && current.Count == 1;
			parts.Add($"+{pair.Value} {pair.Key}");
		}

		if (parts.Count == 0)
			return null;

		// A pure spell's gain is free - it is the compensation for its halved base weight - so the
		// only thing worth saying is which way the branch goes. A hybrid buys its gain with
		// cooldown, and that price has to be on the card or the choice looks like a free upgrade.
		string suffix;
		if (deepens)
			suffix = "  (stays attuned)";
		else if (branches)
			suffix = "  (adds a second element - attunement lost)";
		else
			suffix = $"  (+{(SpellEvolutionCatalog.AttunementGainCooldownCost - 1f) * 100f:0}% cooldown)";
		var label = new Label
		{
			Text = string.Join(", ", parts) + suffix,
			HorizontalAlignment = alignment,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Body);
		// Gold for the branch that keeps the spell attuned, plain ink for the one that trades
		// attunement away. Both have to be ink-family: this line sits on the page, not on a card.
		label.AddThemeColorOverride("font_color", deepens ? InkGold : InkSoft);
		return label;
	}

}
