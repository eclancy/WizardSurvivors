using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

public partial class LevelUpMenu : CanvasLayer
{
	[Signal] public delegate void WeaponSelectedEventHandler(string choice);
	[Signal] public delegate void RerollRequestedEventHandler();

	// The run-scoped charge: a ban names a spell. (Save and Augury, the other two, were removed on
	// 2026-10-28 - "remove save and augury mechanics" - and their shop levels refunded, SaveData schema 18.)
	[Signal] public delegate void BanRequestedEventHandler(string spellId);
	[Signal] public delegate void SkipRequestedEventHandler();
	// No swap and no erase: spells are permanent once taken, and a full book is simply offered no
	// new spell (Player.GetLevelUpOptions). The one way to cut a spell out is a relic, the Scribe's
	// Penknife, and its picker lives on the chest screen (ChestItemSelectionMenu).
	private Button rerollButton = null!;
	private Button skipButton = null!;
	private List<LevelUpOption> currentOptions = new();
	private List<EquippedSpellInfo> currentEquippedSpells = new();
	private Dictionary<string, int> currentBaselineElementCounts = new();
	private int currentRerollsRemaining = 0;
	private LevelUpCharges currentCharges = new();

	/// <summary>The charges the bar is showing. Read by the balance harness, which spends them.</summary>
	public LevelUpCharges Charges => currentCharges;

	// --- The Ascension strip ------------------------------------------------------------------
	//
	// Level-up cards stop at level 7. A spell's Ascension - and a wizard's Apotheosis - is not a
	// card: it is a row of buttons ABOVE the cards, present on every level-up from the moment it is
	// earned until it is taken (Player.GetAscensionOffers). Pressing one opens the same evolution
	// page a milestone card always opened, and choosing a branch spends this level-up's pick.
	//
	// It lives inside Panel/VBoxContainer, the normal view, so it hides with the cards when the
	// evolution page or the ascension browser takes over. Rerolls and bans re-deal the cards and
	// leave it alone; it only changes when the loadout does.
	private List<LevelUpOption> currentAscensions = new();
	private const string AscendStripName = "AscendStrip";

	/// <summary>What the strip is offering. Read by the balance harness.</summary>
	public IReadOnlyList<LevelUpOption> GetAscensionOptions() => currentAscensions;

	// THE ARMING PAUSE (2026-10-28, Eric: "give a longer pause when levelling up so i dont accidentally
	// click on a level up option"). The screen opens in the middle of play - mid-click, mid-tap, A
	// held - and the cards took a press from their first frame, so a click meant for the battlefield
	// chose a card nobody had read. Worse since one menu waits for another (Node2DGame's queue): the
	// click that closes one level-up lands on the next. For InputGuardSeconds every click, tap and
	// accept is swallowed before it reaches a card (_Input runs ahead of the GUI); the stick, the
	// arrows and Y still work, so the wait is spent reading. The cards come up dimmed and brighten
	// as they arm (FadeIn), which is what says "not yet". Real time, since the tree is paused.
	public const float InputGuardSeconds = 0.8f;
	private ulong armedAtMsec;

	/// <summary>True once the arming pause is over and a press will choose.</summary>
	public bool IsArmed => Time.GetTicksMsec() >= armedAtMsec;

	private void Disarm()
	{
		armedAtMsec = Time.GetTicksMsec() + (ulong)(InputGuardSeconds * 1000f);
	}

	private static bool IsChoosingPress(InputEvent @event) =>
		@event is InputEventMouseButton { Pressed: true }
		|| @event is InputEventScreenTouch { Pressed: true }
		|| (@event.IsPressed() && @event.IsAction("ui_accept"));

	public void SetAscensions(List<LevelUpOption> ascensions)
	{
		currentAscensions = ascensions ?? new List<LevelUpOption>();
		BuildAscendStrip();
		// The strip takes a row from the hand's height: a PC's cards dealt before it may now need the
		// smaller icon (WideCardsTight), a phone's a shorter row (NarrowCardHeight).
		bool narrow = ResponsiveLayout.IsNarrow(this);
		// The page of a spell's forms hides the strip (LevelUpMenu.Variants.cs), so it is not re-dealt for it.
		if (pendingVariantOption != null && GetNodeOrNull<Control>($"Panel/VBoxContainer/{AscendStripName}") is Control hiddenStrip)
			hiddenStrip.Visible = false;
		if (pendingEvolutionOption == null && pendingVariantOption == null && currentOptions.Count > 0
			&& (narrow ? !Mathf.IsEqualApprox(NarrowCardHeight(), narrowBuiltHeight) : WideCardsTight() != wideCardsBuiltTight))
		{
			BuildButtonsFrom(currentOptions);
			UpdateRerollState(currentRerollsRemaining);
			UpdateChargeRow();
		}
		RefreshAscensionReadiness();
		// Clear of the phone's rounded corners and camera hole; nothing on desktop (SafeArea).
		SafeArea.Apply(this);
		MenuNavigator.Attach(this);
	}

	private void BuildAscendStrip()
	{
		var normalView = GetNodeOrNull<VBoxContainer>("Panel/VBoxContainer");
		if (normalView == null)
			return;

		var old = normalView.GetNodeOrNull<Node>(AscendStripName);
		if (old != null)
		{
			normalView.RemoveChild(old);
			old.QueueFree();
		}
		if (currentAscensions.Count == 0)
			return;

		var strip = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center };
		strip.AddThemeConstantOverride("h_separation", 10);
		strip.AddThemeConstantOverride("v_separation", 6);

		foreach (LevelUpOption option in currentAscensions)
		{
			bool apotheosis = option.MilestoneLevel >= WizardSurvivors.scripts.ApotheosisCatalog.MilestoneLevel;
			string form = apotheosis && option.EvolutionChoices.Count > 0 ? option.EvolutionChoices[0].DisplayName : string.Empty;
			var button = new Button
			{
				Name = $"Ascend_{option.SpellId}",
				Text = apotheosis
					? $"✶ APOTHEOSIS   {option.DisplayName} → {form}"
					: $"★ ASCEND   {option.DisplayName}",
				// Wide enough to read as the headline of the screen rather than a tab beside it.
				CustomMinimumSize = new Vector2(340, ResponsiveLayout.MinTouchTarget + 8),
			};
			ResponsiveLayout.SetFont(button, ResponsiveLayout.TextRole.Label);
			BonelightSkin.StyleButton(button, BonelightSkin.Emphasis.Normal);
			// The plate the page it opens is made of: obsidian for an Ascension, ember for the
			// Apotheosis (CardFamilyArt), so the button and the choice behind it are one object. It
			// was the screen's stone button, the same as Reroll. Gold on the obsidian 12.3:1, the
			// ember ink on the ember plate 11.1:1 (tools/art/ui_contrast.py, "strip").
			EnsureFamilyStyles();
			StyleBoxTexture plate = apotheosis ? stripEmberResting : stripObsidianResting;
			StyleBoxTexture plateLit = apotheosis ? stripEmberLit : stripObsidianLit;
			button.AddThemeStyleboxOverride("normal", plate);
			button.AddThemeStyleboxOverride("hover", plateLit);
			button.AddThemeStyleboxOverride("pressed", plateLit);
			button.AddThemeStyleboxOverride("focus", plateLit);
			Color ink = apotheosis ? new Color(1.0f, 0.70f, 0.42f) : new Color(1.0f, 0.88f, 0.50f);
			button.AddThemeColorOverride("font_color", ink);
			button.AddThemeColorOverride("font_hover_color", ink.Lightened(0.2f));
			button.AddThemeColorOverride("font_focus_color", ink.Lightened(0.2f));
			LevelUpOption captured = option;
			button.Pressed += () => OnOptionChosen(captured);
			// The same shimmer as the Ascensions button, on the same clock, so the two read as one
			// thing waiting. No ring and no badge: the strip button IS the ascension, it does not
			// need pointing at.
			AscensionReadyGlow.AttachTo(button, showRing: false).SetActive(true);
			strip.AddChild(button);
		}

		// Pushed down clear of the Ascensions button, which is pinned to the panel's top right corner
		// rather than living in this column - on a phone-width panel the two otherwise overlap.
		var placed = new MarginContainer { Name = AscendStripName };
		// On a phone, clear of the Ascensions button pinned to the panel's corner; a PC's sits in the
		// heading band's row, above this.
		placed.AddThemeConstantOverride("margin_top", ResponsiveLayout.IsNarrow(this) ? 30 : 0);
		strip.Name = "AscendButtons";
		placed.AddChild(strip);
		normalView.AddChild(placed);
		// Directly above the cards - or above the scroll that wraps them on a phone.
		Node anchor = GetOptionsContainer();
		while (anchor != null && anchor.GetParent() != normalView)
			anchor = anchor.GetParent();
		if (anchor != null)
			normalView.MoveChild(placed, anchor.GetIndex());
	}
	private bool pendingBanSelection = false;
	private HBoxContainer chargeRow;
	private Button banButton;
	private LevelUpOption pendingEvolutionOption = null;
	private const string EvolutionRootName = "EvolutionRoot";
	private const float CardWidth = 272f;
	private const float CardHeight = 468f;
	private const float UpgradeSectionWidth = 238f;
	private static readonly Vector2 IconFrameSize = new Vector2(0, 112);
	private static readonly Vector2 IconSize = new Vector2(88, 88);
	// Four across: the icon steps down a size so the name's second line fits (BuildWideOptionCard).
	private static readonly Vector2 CompactIconFrameSize = new Vector2(0, 88);
	private static readonly Vector2 CompactIconSize = new Vector2(64, 64);
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
		GroupActionButtons();
		StyleHeading();
		Disarm();
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
	//
	// A PC gets the same row, and the charge (Ban) joins it. Stacked, the footer was
	// two rows and the charge row a third, and a 1280x720 hand with charges or an Ascension on offer
	// ran off the foot of the screen - with no way to scroll it on a pad (2026-10-27, "the level ups
	// are so large they often cause the screen to have to scroll, but you can't scroll on a
	// controller"). A phone keeps the charges on a row of their own: five buttons do not fit its width.
	private void GroupActionButtons()
	{
		if (rerollButton == null || skipButton == null)
			return;

		if (rerollButton.GetParent() is not Control vbox || skipButton.GetParent() != vbox)
			return;

		bool narrow = ResponsiveLayout.IsNarrow(this);
		int index = rerollButton.GetIndex();
		var row = new HBoxContainer
		{
			Name = "ActionRow",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		row.AddThemeConstantOverride("separation", 10);

		vbox.RemoveChild(rerollButton);
		vbox.RemoveChild(skipButton);
		vbox.AddChild(row);
		vbox.MoveChild(row, index);
		if (!narrow && chargeRow != null && chargeRow.GetParent() == vbox)
		{
			vbox.RemoveChild(chargeRow);
			chargeRow.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			row.AddChild(chargeRow);
		}
		row.AddChild(rerollButton);
		row.AddChild(skipButton);

		foreach (Button button in new[] { rerollButton, skipButton })
		{
			button.SizeFlagsHorizontal = narrow ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkCenter;
			button.CustomMinimumSize = new Vector2(narrow ? 0f : 180f, ResponsiveLayout.MinTouchTarget);
		}
	}

	// WHAT A CARD'S TAGS DO, ON A PAD. Y (or the Y key) opens the element card for the selected
	// card's first tag chip, and each press after that the next, then closes - the card a tap or a
	// click on the chip opens, which a pad could only reach before by stepping onto the chip itself,
	// and only on a new spell's card (2026-10-27, "there should also be a way to easily show what the
	// tag does when levelling up from that menu"). It reads the selected card's own chips, so it
	// works the same on a new spell, a boon, a mutation and an Ascension path; the card stays
	// selected, and moving off it, A or B close the element card (ElementTagPopover.OpenFor).
	public override void _Input(InputEvent @event)
	{
		if (!Visible)
			return;
		if (!IsArmed && IsChoosingPress(@event))
		{
			GetViewport().SetInputAsHandled();
			return;
		}
		bool y = @event is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Y }
			|| @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Y };
		if (!y)
			return;
		Control selected = GetViewport()?.GuiGetFocusOwner();
		if (selected == null || !IsAncestorOf(selected))
			return;

		// A card's chips sit under its Button (a PC card) or beside it in the same PanelContainer (a
		// phone card, an Ascension plate), so the selected control's parent holds them either way.
		var chips = new List<ElementChip>();
		CollectChips(selected.GetParent(), chips);
		if (chips.Count == 0)
			return;
		GetViewport().SetInputAsHandled();
		int open = chips.IndexOf(ElementTagPopover.OpenAnchor);
		if (open == chips.Count - 1)
			ElementTagPopover.Close();
		else
			ElementTagPopover.OpenFor(chips[open + 1], selected);
	}

	private static void CollectChips(Node node, List<ElementChip> into)
	{
		if (node == null)
			return;
		if (node is ElementChip chip && chip.IsVisibleInTree())
			into.Add(chip);
		foreach (Node child in node.GetChildren())
			CollectChips(child, into);
	}

	// Says Y is there, in the action row, while a pad is connected: a button nothing on screen names
	// is a button nobody presses. Re-read at every level-up, because Godot enumerates pads late.
	private Label tagHint;

	private void UpdateTagHint()
	{
		if (tagHint == null && skipButton?.GetParent() is HBoxContainer row)
		{
			tagHint = CodexRows.MakeText("Y: what its tags do", ResponsiveLayout.TextRole.Label, new Color(0.80f, 0.82f, 0.88f));
			tagHint.Name = "TagHint";
			tagHint.AutowrapMode = TextServer.AutowrapMode.Off;
			tagHint.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			row.AddChild(tagHint);
		}
		if (tagHint != null)
			tagHint.Visible = Input.GetConnectedJoypads().Count > 0;
	}

	// The PC hand's height budget, for WideCardsTight: the panel (CenterMenuPanel), less the column's
	// own insets, the heading band, the one action row and the gaps between the column's rows.
	private const float WideColumnInsets = 26f + 16f;
	private const float WideHeadingHeight = 56f;
	private const float WideActionRowHeight = 48f;
	private const float WideStripHeight = 56f;
	private const float WideColumnSeparation = 10f;
	// What a card needs with the full 112 icon frame and the tallest deal (four stat lines, two
	// chips, a two-line name): measured by _UiShot --tall-deal=3's card report.
	private const float RoomyWideCardHeight = 430f;

	// Whether the PC cards must take the smaller icon to fit the panel: when the Ascension strip is
	// up, or the panel is short (a 16:10 monitor at the minimum height). The cards themselves take
	// their height from their content (BuildWideOptionCard), so this is the only thing that has to
	// give.
	private bool WideCardsTight()
	{
		float panel = GetNodeOrNull<Control>("Panel")?.CustomMinimumSize.Y ?? 648f;
		float room = panel - WideColumnInsets - WideHeadingHeight - WideActionRowHeight - WideColumnSeparation * 2f;
		if (currentAscensions.Count > 0 && pendingVariantOption == null)
			room -= WideStripHeight + WideColumnSeparation;
		return room < RoomyWideCardHeight;
	}

	// The tightness the current cards were built for, so a strip arriving after them (SetAscensions
	// runs after SetOptions) re-deals them only when it changes the answer.
	private bool wideCardsBuiltTight;

	// THE HEADING SITS ON A CALM BAND, never on the floor.
	//
	// It used to be drawn straight onto the cobblestone plate with a black outline, and the player
	// reported it as completely illegible - which it was: the plate's bright stone lips ran behind
	// every glyph, so the outline was fighting a pattern of near-white lines at the same scale as the
	// letters. On a phone it was worse still: the scene's LabelSettings (whose font size beats any
	// theme override) was kept there and drew the heading at 16px, plain white, on that floor.
	//
	// Now: no LabelSettings anywhere, the Display role in the bold heading face, and a solid band of
	// the darkest stone under it with a gold rule at its foot. Bone ink on that band is about 17:1.
	// (The mutation, ascension and browser headings need no band: they are written in Ink on the
	// vellum page, which is already a calm ground, at about 10:1.) On a phone the screen says
	// only "Level Up!", left-aligned, because the Ascensions button owns the right of that row and
	// "Choose a spell" is what three cards already say.
	private void StyleHeading()
	{
		var heading = GetNodeOrNull<Label>("Panel/VBoxContainer/Label");
		if (heading == null)
			return;

		bool narrow = ResponsiveLayout.IsNarrow(this);
		heading.LabelSettings = null;
		heading.Text = narrow ? "Level Up!" : "Level Up! Choose a Spell";
		heading.HorizontalAlignment = narrow ? HorizontalAlignment.Left : HorizontalAlignment.Center;
		heading.VerticalAlignment = VerticalAlignment.Center;
		heading.CustomMinimumSize = new Vector2(0f, ResponsiveLayout.MinTouchTarget + 8f);
		ResponsiveLayout.SetFont(heading, ResponsiveLayout.TextRole.Display);
		heading.AddThemeColorOverride("font_color", HeadingInk);
		heading.AddThemeConstantOverride("outline_size", 0);
		heading.AddThemeStyleboxOverride("normal", BuildHeadingBand(narrow));
	}

	// Bone hi, warmed a touch toward the gold rule under it. #FFF2DB on stone deep #0B0F18 is
	// 17.3:1; on the old floor's bright lips it was 1.25:1. Mirrored in tools/art/ui_contrast.py,
	// which measures it - change one, change the other.
	private static readonly Color HeadingInk = new Color(1f, 0.95f, 0.86f);

	/// <summary>The band a heading sits on: the darkest stone, solid, with a gold rule at its foot.</summary>
	private static StyleBoxFlat BuildHeadingBand(bool narrow)
	{
		var band = new StyleBoxFlat
		{
			// stone deep, opaque: nothing of the floor or the fight shows through the letters.
			BgColor = new Color(0.043f, 0.059f, 0.094f, 1f),
			// gold base: the same rule that edges the footer frame.
			BorderColor = new Color(0.541f, 0.416f, 0.118f),
		};
		band.BorderWidthBottom = 2;
		band.SetCornerRadiusAll(2);
		band.ContentMarginLeft = narrow ? 14 : 12;
		// Room for the Ascensions button pinned over the band's right end.
		band.ContentMarginRight = narrow ? 180 : 12;
		band.ContentMarginTop = 2;
		band.ContentMarginBottom = 4;
		return band;
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
			// Most of the way in at once, the rest as the arming pause ends (InputGuardSeconds): the
			// cards are readable from the start and look ready only when they are.
			panel.Modulate = new Color(1f, 1f, 1f, 0f);
			tween.TweenProperty(panel, "modulate:a", ArmingAlpha, 0.16f).SetDelay(0.05f);
			tween.Chain().TweenProperty(panel, "modulate:a", 1f, 0.14f).SetDelay(InputGuardSeconds - 0.35f);
		}
	}

	private const float ArmingAlpha = 0.72f;

	// The same pause again when a choice swaps the cards for new ones under the pointer - a mutation
	// or an Apotheosis card opening its paths - or a double click picks a path unread.
	private void RearmForNewChoices()
	{
		Disarm();
		var panel = GetNodeOrNull<CanvasItem>("Panel");
		if (panel == null)
			return;
		panel.Modulate = new Color(1f, 1f, 1f, ArmingAlpha);
		var tween = CreateTween();
		tween.TweenProperty(panel, "modulate:a", 1f, 0.14f).SetDelay(InputGuardSeconds - 0.14f);
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

		// A PC is landscape now (DisplayProfile): a 1280x720 canvas, wider on a wider monitor. Taking
		// the whole viewport there stretched three pages into slabs a third of the screen wide, hid
		// the heading under the plate's top border and buried the run behind a wall of stone. The
		// panel is a centred book instead - most of the width, never wider than 1180, and the height
		// less a band that leaves the run visible above and below - so each page keeps roughly the
		// proportions it was drawn at.
		Vector2 panelSize = new Vector2(
			Mathf.Clamp(viewportSize.X * 0.9f, 860f, 1180f),
			Mathf.Clamp(viewportSize.Y - 72f, 560f, 820f));
		// The heading sat on the plate's 16px border; give it the border plus air.
		if (panel.GetNodeOrNull<Control>("VBoxContainer") is Control column)
			column.OffsetTop = 26f;
		// A header band as tall as the Ascensions button, which is pinned to the panel's top-right
		// corner: without it the third page started level with the button and ran under it.
		if (panel.GetNodeOrNull<Control>("VBoxContainer/Label") is Control heading)
			heading.CustomMinimumSize = new Vector2(0f, 56f);

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
		// A new hand is never dealt onto the page of a spell's forms.
		if (pendingVariantOption != null || GetNodeOrNull<Control>($"Panel/VBoxContainer/{VariantNoteName}") != null)
			HideVariantChrome();
		currentCharges = charges ?? new LevelUpCharges { Rerolls = rerollsRemaining };
		currentOptions = options ?? new List<LevelUpOption>();
		currentEquippedSpells = equippedSpells ?? new List<EquippedSpellInfo>();
		currentBaselineElementCounts = baselineElementCounts ?? new Dictionary<string, int>();
		currentRerollsRemaining = rerollsRemaining;
		pendingEvolutionOption = null;
		pendingBanSelection = false;
		BuildButtonsFrom(currentOptions);
		UpdateRerollState(currentRerollsRemaining);
		UpdateChargeRow();
		UpdateTagHint();
	}

	/// <summary>
	/// Updates the charge bar and returns to the normal options without touching the offer.
	/// </summary>
	/// <remarks>
	/// Separate from SetOptions because a refused ban must put the same cards back, not re-roll them:
	/// routing it through SetOptions would hand the player a free reroll.
	/// </remarks>
	public void RefreshCharges(LevelUpCharges charges)
	{
		currentCharges = charges ?? currentCharges;
		pendingBanSelection = false;
		if (pendingVariantOption != null)
			HideVariantChrome();
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

		if (pendingEvolutionOption != null)
		{
			BuildEvolutionSelectionButtons(pendingEvolutionOption);
		}
		else if (pendingVariantOption != null)
		{
			// A new canvas, the same choice: the forms are laid out again without re-arming.
			BuildVariantSelectionButtons(pendingVariantOption, rearm: false);
		}
		else if (pendingBanSelection)
		{
			BuildBanSelectionButtons();
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
		// A spell with forms opens a page of them; the spell is taken from there, in one
		// (LevelUpMenu.Variants.cs).
		if (option.OffersVariants)
		{
			BuildVariantSelectionButtons(option);
			return;
		}
		if (option.IsEvolutionMilestone && option.EvolutionChoices != null && option.EvolutionChoices.Count > 0)
		{
			pendingEvolutionOption = option;
			BuildEvolutionSelectionButtons(option);
			return;
		}

		// The spell, or the spell and the form its card is ("spell:form", SpellVariants.Selection).
		string selection = option.Selection;
		if (pendingVariantOption != null)
			HideVariantChrome();
		EmitSignal(nameof(WeaponSelected), selection);
		Hide();
	}

	private void OnEvolutionChosen(LevelUpOption option, SpellEvolutionOption evo)
	{
		pendingEvolutionOption = null;
		EmitSignal(nameof(WeaponSelected), $"{option.SpellId}:{evo.Id}");
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
		chargeRow.AddChild(banButton);

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
		banButton.Text = $"Ban ({currentCharges.Bans})";

		// Hidden entirely while the picker is open, so the player cannot start a second charge action
		// on top of an unfinished one.
		chargeRow.Visible = banButton.Visible && !pendingBanSelection && pendingEvolutionOption == null
			&& pendingVariantOption == null;
	}

	private void OnBanPressed()
	{
		SfxPlayer.Global(SfxCatalog.UiOpen);
		pendingBanSelection = true;
		BuildBanSelectionButtons();
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
			if (captured.IsBoon)
				ApplyBoonFrameToButton(btn);
			container.AddChild(btn);
		}

		container.AddChild(MakeCancelButton(() =>
		{
			pendingBanSelection = false;
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

	// How many cards the screen is showing: the hand, or the forms of one spell on its page. A card's size
	// is decided by what is on the screen with it, not by the hand behind a page.
	private int shownCount;

	private void BuildButtonsFrom(List<LevelUpOption> options)
	{
		shownCount = options?.Count ?? 0;
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
		wideCardsBuiltTight = WideCardsTight();
		narrowBuiltHeight = NarrowCardHeight();

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
		// secondary flows (evolution, ban) build their own buttons and are picked up by the
		// navigator's own rescan a few frames later.
		MenuNavigator.Attach(this);
	}

	// The desktop card: a poster. Art across the top, everything centred beneath it, a fixed
	// 272x420 so three sit side by side.
	private Control BuildWideOptionCard(LevelUpOption option)
	{
		var card = new Button
		{
			CustomMinimumSize = new Vector2(CardWidth, 0f),
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
		// The sides and the head sit just inside the frame (the icon has twelve of air of its own in
		// its 112 frame). THE FOOT is the frame's inner border plus FootClearance: the chips are pushed
		// to the bottom, and at 14 their boxes ended exactly on the page's rule and ran two pixels into
		// the plaque's groove - "the spell tags are too low on the level up pages, they are all the
		// way against the bottom of the border" (CardFamilyArt.FootInset).
		content.OffsetLeft = 16;
		content.OffsetTop = 14;
		content.OffsetRight = -16;
		float foot = CardFamilyArt.FootInset(FamilyOf(option));
		content.OffsetBottom = -foot;
		card.AddChild(content);

		// THE CARD IS AS TALL AS ITS CONTENT, and the grid stretches it to the room there is. It was a
		// fixed 468, which with the Ascension strip or the charge row put the footer below a 1280x720
		// screen; a Button does not size itself to a child it does not lay out, so its minimum follows
		// the content's (as BuildEvolutionOptionCard's does).
		content.MinimumSizeChanged += () =>
		{
			if (IsInstanceValid(card) && IsInstanceValid(content))
				card.CustomMinimumSize = new Vector2(CardWidth, content.GetCombinedMinimumSize().Y + 14f + foot);
		};

		// Four across (Playtest Mode's hand) is a card about 274 wide, where a two-word name wraps to
		// two lines; the icon gives back the 24 that costs rather than pushing the chips into the
		// frame. So does a hand with less height than a roomy card needs (WideCardsTight).
		bool fourAcross = (shownCount > 0 ? shownCount : currentOptions?.Count ?? 3) >= 4 || WideCardsTight();
		var iconFrame = new CenterContainer
		{
			CustomMinimumSize = fourAcross ? CompactIconFrameSize : IconFrameSize,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		iconFrame.AddChild(BuildFramedOptionIcon(option, fourAcross ? CompactIconSize : IconSize, card));
		content.AddChild(iconFrame);

		content.AddChild(BuildOptionTitle(option, HorizontalAlignment.Center));

		var levelPips = BuildLevelPips(option, card);
		if (levelPips != null)
		{
			levelPips.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
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
		// The head and the foot both stand FootClearance inside the frame's inner border. The row is
		// centred in the card, so this only bites when the text is taller than the card - four stat
		// lines, or the four-card hand - and that is exactly when the chips used to sit on the rule
		// (BuildWideOptionCard).
		int frameInset = CardFamilyArt.FootInset(FamilyOf(option));
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_top", frameInset);
		margin.AddThemeConstantOverride("margin_bottom", frameInset);
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
		iconFrame.AddChild(BuildFramedOptionIcon(option, new Vector2(iconColumn, iconColumn), card));
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
		// costs a whole row of height on the axis that is actually scarce here. Only an upgrade has
		// pips (BuildLevelPips), so on every other card the label has the line to itself.
		var metaRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		metaRow.AddThemeConstantOverride("separation", 10);
		Control pips = BuildLevelPips(option, card);
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

	// A spell's icon goes straight onto its page. A boon's relic sits behind the reliquary's glass
	// window (ui-boon-window.png), in the same slot, so the boon card and the spell card beside it
	// lay out alike and only the object changes.
	//
	// Behind the glass the relic is in its own light (BoonAura): a halo that breathes and a rune
	// ring that turns, in the boon's element, so a boon reads as magic and not as an item. The ring
	// needs a band round the icon to live in, and that band comes out of the GLASS, never the
	// plaque - on a phone the text starts twelve pixels from the window. The window is sized to a
	// whole multiple of the aura cell so the relic lands on exact pixels; that may overshoot the
	// slot by up to BoonAuraOvershoot, which the CenterContainer around it absorbs. On a PC that
	// makes the window 108 rather than 88, inside the 112 icon frame every card already has.
	private const float BoonAuraOvershoot = 12f;

	private Control BuildFramedOptionIcon(LevelUpOption option, Vector2 size, BaseButton card)
	{
		if (!option.IsBoon)
			return BuildOptionIcon(option, size);

		EnsureCardStyles();
		var window = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		window.AddThemeStyleboxOverride("panel", boonWindowStyle);
		float available = MathF.Min(size.X, size.Y) - BoonWindowContentMargin * 2 + BoonAuraOvershoot;
		float inner = BoonAura.Cell * Math.Max(2, BoonAura.ScaleFor(available));
		window.AddChild(new BoonAura
		{
			Icon = option.Icon ?? DefaultSpellIcon,
			AuraElement = BoonAura.ElementFor(BoonCatalog.GetById(option.SpellId)),
			FocusOwner = card,
			CustomMinimumSize = new Vector2(inner, inner),
		});
		return window;
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
			// A boon's relic is NOT tinted and never comes through here: BuildFramedOptionIcon
			// hands it to BoonAura, which draws it as it is, in its own light.
			Modulate = IconTint(option)
		};
	}

	// Every page is light, so a spell icon on one is deepened toward ink in its element. The plates
	// are dark, so the icon goes on as drawn: deepening it there would sink it into the glass.
	private Color IconTint(LevelUpOption option)
	{
		return CardFamilyArt.IsDark(FamilyOf(option))
			? Colors.White
			: GetDominantElementColor(option).Lerp(Ink, 0.28f);
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
		// Each family writes in its own inks (LevelUpMenu.Families.cs): dark on every page, light on
		// the plaque and the plates.
		title.AddThemeColorOverride("font_color", TitleInk(FamilyOf(option)));
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
			// An upgrade says so in words as well as in red: "Level up" in front of what it is, so the
			// rubric is never the only thing telling the player this improves a spell they hold.
			Text = TypeLabelText(option),
			HorizontalAlignment = alignment,
			VerticalAlignment = VerticalAlignment.Center,
			// "BOON - PERMANENT, NO SLOT" is wider than a PC page and ran off both edges. Not on a
			// phone row, where this label shares a line with the pips and wrapping it collapsed it to
			// one letter per line.
			AutowrapMode = ResponsiveLayout.IsNarrow(this) ? TextServer.AutowrapMode.Off : TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		ResponsiveLayout.SetFont(typeLabel, ResponsiveLayout.TextRole.Label);
		typeLabel.AddThemeColorOverride("font_color", SoftInk(FamilyOf(option)));
		return typeLabel;
	}

	private static string TypeLabelText(LevelUpOption option)
	{
		if (option.IsBoon)
			return "BOON - PERMANENT, NO SLOT";
		if (option.SpellId == Player.MarginaliaId)
			return "A note in the margin";
		string kind = option.IsPassive ? "Passive" : "Attack";
		return FamilyOf(option) switch
		{
			CardFamily.Mutation => $"Mutation · {kind}",
			CardFamily.Upgrade => $"Level up · {kind}",
			_ => kind,
		};
	}

	// Was BuildOptionDescription. Same slot on the card, numbers instead of prose.
	//
	// Null for an upgrade: BuildUpgradeSection already prints what the level does, and printing the
	// base stats above the deltas would be two number blocks saying almost the same thing.
	//
	// One line per stat, each led by its glyph (StatIcons): a sword before the damage, an hourglass
	// before the cooldown. It was one row of abbreviations in one ink - "DMG 20   CD 5.5s   RNG
	// 480" - so which number was which depended on reading three letters. The glyph is recognised
	// instead, and the abbreviation is spelled out beside it because the card has the room.
	private Control BuildOptionStatLine(LevelUpOption option, HorizontalAlignment alignment)
	{
		if (!option.IsNewUnlock || string.IsNullOrWhiteSpace(option.StatSummary))
			return null;

		return StatIcons.BuildList(option.StatSummary, BodyInk(FamilyOf(option)),
			ResponsiveLayout.TextRole.Body, centred: alignment == HorizontalAlignment.Center);
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
		CardFamily family = FamilyOf(option);
		// In the page's own mark: the rubric's red round an upgrade's deltas, which are written in
		// that red; the stain's teal round the mutation card's line.
		upgradeStyle.BorderColor = family switch
		{
			CardFamily.Upgrade => CardFamilyArt.UpgradeRule,
			CardFamily.Mutation => CardFamilyArt.MutationRule,
			_ => InkSoft,
		};
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

		// One glyph-led line per change, in rubric - the scribe's red for what is being added, and the
		// red the upgrade page is ruled in. The milestone lines (a mutation, an ascension) lead with
		// their own star and take no glyph.
		upgradeBox.AddChild(StatIcons.BuildList(option.UpgradeSummary, AccentInk(family),
			ResponsiveLayout.TextRole.Body, centred: false));

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

	// The level pips: one per level, the held ones filled and the one this pick reaches alive
	// (LevelPips, from the Pixel 9: "animate the pip for the level it's becoming"). It was one pip per
	// level AFTER the first, every pip the pick reaches drawn already filled, so an upgrade to 3
	// looked like a spell at 3.
	//
	// ONLY ON AN UPGRADE. "When you are offered a spell for the first time, we do not need to see the
	// level up animation pips": a new spell's row was a lone breathing socket at the head of six empty
	// ones, which says nothing the word "Attack" under its name does not, and on a PC it cost the
	// card the 20 its chips now stand clear of the frame with. The mutation card in the hand has none
	// either - it is a milestone, named as one ("Mutation · Attack"), the wax seal and the glow say
	// so, and its level is the ceremony rather than a step - and nor have a boon (it never levels)
	// or a branch on the evolution page. So the one moving pip on the screen is on the card that
	// improves something already held, which is what it was asked for.
	//
	// Gold base on the page, ring and held fill at 3.8:1 as a graphic (tools/art/ui_contrast.py,
	// "pips"); the becoming pip rises to gold lit. The gold leaf (#B88A1F) it used was 2.4:1.
	private Control BuildLevelPips(LevelUpOption option, BaseButton card)
	{
		if (FamilyOf(option) != CardFamily.Upgrade || option.MaxLevel <= 1)
			return null;

		int count = option.MaxLevel;
		return new LevelPips
		{
			Count = count,
			Filled = Math.Clamp(option.NextLevel - 1, 0, count),
			Becoming = option.NextLevel >= 1 && option.NextLevel <= count ? option.NextLevel - 1 : -1,
			Ring = PipGoldBase,
			Empty = new Color(InkSoft, 0.16f),
			Glow = PipGoldLit,
			FocusOwner = card,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
		};
	}

	private static readonly Color PipGoldBase = new Color(0.541f, 0.416f, 0.118f);   // gold base #8A6A1E
	private static readonly Color PipGoldLit = new Color(0.847f, 0.690f, 0.290f);    // gold lit #D8B04A

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
	// delta means doing arithmetic on a paused screen. What the element DOES is one tap away: each
	// chip is an ElementChip, which opens the element's card (every tier, what you hold, what this
	// pick makes it) over the screen. It used to write one tier's sentence on a line under the row.
	//
	// A tap on a chip never picks the card. On a PC the chip sits inside the card's Button and stops
	// the press before it gets there; on a phone the card Button is a sibling of this content, not
	// an ancestor, so the press never travels to it. See ElementChip.
	//
	// Nothing at all on an upgrade card. Levelling a spell you already own cannot change your tag
	// counts, so the chips would be a row of numbers that never move.
	private Control BuildSpellTagChips(LevelUpOption option, FlowContainer.AlignmentMode alignment)
	{
		if (option.SpellElementTags == null || option.SpellElementTags.Count == 0)
			return null;

		if (!option.IsNewUnlock)
			return null;

		var row = new HFlowContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		row.AddThemeConstantOverride("h_separation", 6);
		row.AddThemeConstantOverride("v_separation", 6);
		row.Alignment = alignment;

		foreach (var pair in option.SpellElementTags.OrderByDescending(p => p.Value).ThenBy(p => p.Key))
		{
			string elementName = pair.Key;
			if (!Enum.TryParse<Element>(elementName, out Element element))
				continue;
			Color color = ElementColors.GetColor(element);

			// The count AFTER taking this option, which is the number the decision turns on.
			int resulting = option.ResultingElementCounts != null
				&& option.ResultingElementCounts.TryGetValue(elementName, out int r)
				? r
				: pair.Value;

			// Measured against the chip as drawn - its colour at 88% over the page - by WCAG
			// contrast (StatIcons.ReadableOn), not by a brightness threshold, and nudged by
			// CodexRows.ChipFill where neither ink clears 4.5:1 (Fire on the boon enamel was 4.45).
			Color face = CodexRows.ChipFill(ChipBackground(color, FamilyOf(option)));
			row.AddChild(ElementChip.Create(element,
				$"{elementName} {ElementPassiveDescriptions.GetProgressLabel(resulting)}",
				face, face.Blend(new Color(InkSoft, 0.45f)), StatIcons.ReadableOn(face),
				focusable: true, countAfter: resulting, padX: 6, padY: 3));
		}

		return row.GetChildCount() > 0 ? row : null;
	}

	// The chip's colour as the eye gets it: the element at 88% over whatever card it sits on. The
	// old rule tested the element colour alone against a 0.62 brightness threshold and put white on
	// the poison and grass chips at about 2.3:1, and on fire, water, arcane and earth below 4:1.
	private static Color ChipBackground(Color element, CardFamily family)
	{
		// The family's face: flesh hi on the page, violet shade on the boon plaque's resting enamel.
		Color card = CardFamilyArt.FaceOf(family);
		return card.Lerp(new Color(element.R, element.G, element.B), 0.88f);
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
		int count = Math.Max(1, shownCount > 0 ? shownCount : currentOptions?.Count ?? 3);
		float available = ResponsiveLayout.ViewportSize(this).Y - HudSafeTop - NarrowChromeAllowance;
		// The rows a level-up can add above and below the cards: the Ascension strip (its 30 of clear
		// air, a 56-tall button and the column's gap) and the charge row. Left out, a phone hand with
		// both had its third card under the footer. The page of a spell's forms shows neither, and one
		// line of note under its heading instead.
		if (pendingVariantOption != null)
			available -= NarrowNoteAllowance;
		else
		{
			if (currentAscensions.Count > 0)
				available -= NarrowStripAllowance;
			if (ChargesShown())
				available -= NarrowChargeAllowance;
		}
		float perCard = (available - NarrowCardSeparation * (count - 1)) / count;
		return MathF.Max(NarrowRowMinHeight, perCard);
	}

	private const float NarrowCardSeparation = 12f;
	private const float NarrowStripAllowance = 96f;
	private const float NarrowChargeAllowance = 50f;
	private const float NarrowNoteAllowance = 56f;

	// Whether the charge row will show: the test UpdateChargeRow makes, before any picker is open.
	private bool ChargesShown() => currentCharges != null && currentCharges.Bans > 0;

	// The card height a phone hand was built with, so a strip arriving after it (SetAscensions runs
	// after SetOptions) re-deals the cards only when it changes it.
	private float narrowBuiltHeight;

	private int GetResponsiveColumnCount(int optionCount)
	{
		if (optionCount <= 1)
			return 1;

		// On a phone-width viewport the cards become full-width rows stacked vertically. Three
		// 272-wide cards side by side overflowed both edges of a 720-wide portrait screen, and a
		// 2+1 grid reads badly for a three-way choice, so narrow collapses straight to one column.
		if (ResponsiveLayout.IsNarrow(this))
			return 1;

		// Four across for Playtest Mode's four cards. It was three, which put the fourth card on a
		// second row: two 468-tall rows in a 648-tall panel, the heading and the footer pushed off the
		// screen. Four 272-wide cards and their gaps fit the 1120 a 1280x720 canvas gives the row.
		return Math.Min(4, optionCount);
	}

	// The card frame, drawn in tools/art/levelup_frames.py and sliced here.
	//
	// This was four runtime StyleBoxFlats: a 55%-alpha fill and a one-pixel border, floating over a
	// 62% dim over live gameplay. The swarm was legible straight through the text, and a flat
	// rounded rectangle reads as a placeholder on a screen the player looks at more than any other.
	// Now it is an opaque stone face inside a bevelled gold border, nine-sliced so the corner
	// ornaments keep their shape at any card size. 20 is the footer's stone (levelup-card.png); the
	// torn page's corner blocks are 24 (CardFamilyArt.PatchMargin), and slicing it at 20 tiled the
	// gold leaf's arms along the rule as dashes and the corner flecks across the face as dots.
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

	// THE BOON CARD IS NOT A PAGE. A spell is written; a boon is carried - a charm, a pearl, a flask
	// - so it comes in the thing you would keep such an object in: a reliquary plaque of violet
	// enamel in a gold bezel, with the relic behind a glass window and a bail at the top to hang it
	// from. tools/art/ui_frames.py:boon_plaque draws it. It is the darkest card on the screen where
	// the pages are the lightest, which is the cue that survives a glance. See
	// .ai/level-up-screen.md, "The boon card is a reliquary".
	private const string BoonCardTexturePath = "res://assets/bonelight/ui/ui-boon-plaque.png";
	private const string BoonCardLitTexturePath = "res://assets/bonelight/ui/ui-boon-plaque-lit.png";
	private const string BoonWindowTexturePath = "res://assets/bonelight/ui/ui-boon-window.png";
	private const string BoonClaspTexturePath = "res://assets/bonelight/ui/ui-boon-clasp.png";
	// The window is 24 authored pixels (48 drawn) with a 6-pixel rim, so 12 drawn per side.
	private const int BoonWindowPatchMargin = 12;
	private const int BoonWindowContentMargin = 10;

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

		int patch = tornEdge ? CardFamilyArt.PatchMargin : CardPatchMargin;
		style.TextureMarginLeft = patch;
		style.TextureMarginTop = patch;
		style.TextureMarginRight = patch;
		style.TextureMarginBottom = patch;
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
	private StyleBoxTexture boonStyleResting;
	private StyleBoxTexture boonStyleLit;
	private StyleBoxTexture boonButtonStyleResting;
	private StyleBoxTexture boonButtonStyleLit;
	private StyleBoxTexture boonWindowStyle;

	private void EnsureCardStyles()
	{
		cardStyleResting ??= BuildCardStyleBox(CardTexture(), CardContentMargin, tornEdge: true);
		cardStyleLit ??= BuildCardStyleBox(CardLitTexture(), CardContentMargin, tornEdge: true);
		footerStyleResting ??= BuildCardStyleBox(FooterTexture(), 10);
		footerStyleLit ??= BuildCardStyleBox(FooterLitTexture(), 10);
		// Tiled like the pages, for a different reason: the plaque's field is a diaper whose period
		// divides its centre block, and a stretched diaper smears into diagonals. Its 12-pixel
		// authored margin is 24 drawn, not the page's 20 - the bezel and the studs live in it.
		boonStyleResting ??= BuildBoonStyleBox(GD.Load<Texture2D>(BoonCardTexturePath), CardContentMargin);
		boonStyleLit ??= BuildBoonStyleBox(GD.Load<Texture2D>(BoonCardLitTexturePath), CardContentMargin);
		// The ban picker is one line of text, not a card of content.
		boonButtonStyleResting ??= BuildBoonStyleBox(GD.Load<Texture2D>(BoonCardTexturePath), 12);
		boonButtonStyleLit ??= BuildBoonStyleBox(GD.Load<Texture2D>(BoonCardLitTexturePath), 12);
		if (boonWindowStyle == null)
		{
			boonWindowStyle = new StyleBoxTexture { Texture = GD.Load<Texture2D>(BoonWindowTexturePath) };
			boonWindowStyle.SetTextureMarginAll(BoonWindowPatchMargin);
			boonWindowStyle.SetContentMarginAll(BoonWindowContentMargin);
		}
	}

	private const int BoonPatchMargin = 24;

	private static StyleBoxTexture BuildBoonStyleBox(Texture2D texture, int contentMargin)
	{
		var style = new StyleBoxTexture
		{
			Texture = texture,
			AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile,
			AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Tile,
		};
		style.SetTextureMarginAll(BoonPatchMargin);
		style.SetContentMarginAll(contentMargin);
		return style;
	}

	// A boon in the ban picker wears the plaque too, so the rare card is the same object
	// wherever the player meets it - a stone button there would say a boon was furniture.
	private void ApplyBoonFrameToButton(Button button)
	{
		EnsureCardStyles();
		button.AddThemeStyleboxOverride("normal", boonButtonStyleResting);
		button.AddThemeStyleboxOverride("hover", boonButtonStyleLit);
		button.AddThemeStyleboxOverride("pressed", boonButtonStyleLit);
		button.AddThemeStyleboxOverride("focus", boonButtonStyleLit);
		button.AddThemeColorOverride("font_color", BoonInkGold);
		button.AddThemeColorOverride("font_hover_color", BoonInkGold);
		button.AddThemeColorOverride("font_pressed_color", BoonInkGold);
		button.AddThemeColorOverride("font_focus_color", BoonInkGold);
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
		// A spent Reroll is still read - "(0)" is the news. BonelightSkin's disabled ink (#6B7385)
		// measured 4.2:1 on the footer's stone in the photograph (_UiShot --ink-report); this is a
		// step lighter, 5.1:1, and still plainly the dimmest word in the footer.
		button.AddThemeColorOverride("font_disabled_color", new Color(0.478f, 0.510f, 0.588f));
	}

	// THE BOON INKS. The parchment inks (Ink, InkSoft, InkGold, Rubric) are dark marks for a light
	// page and are nearly invisible on violet enamel, so a boon card writes in light: gold leaf for
	// what it is called, bone for what it does. All are palette tones, measured against the resting
	// enamel (violet shade #221C33) and the lit one (violet base #342C4C):
	//   BoonInkGold     gold hi  #FFF0C0  ~14:1 resting, ~11:1 lit   - the name
	//   BoonInkGoldSoft gold lit #D8B04A   ~7:1 resting, ~5.5:1 lit  - "BOON - PERMANENT, NO SLOT"
	//   BoonInk         bone hi  #EEF2FA  ~14:1 resting, ~11:1 lit   - the effect line
	private static readonly Color BoonInkGold = new Color(1.0f, 0.941f, 0.753f);
	private static readonly Color BoonInkGoldSoft = new Color(0.847f, 0.690f, 0.290f);
	private static readonly Color BoonInk = new Color(0.933f, 0.949f, 0.980f);

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
		if (!option.IsEvolutionMilestone || option.MilestoneLevel < 8)
			return;
		bool isApotheosis = option.MilestoneLevel >= WizardSurvivors.scripts.ApotheosisCatalog.MilestoneLevel;

		// So the sweep is cut off at the frame instead of running out over the plate.
		card.ClipContents = true;

		var sheen = new ColorRect
		{
			Name = "AscensionSheen",
			// The Apotheosis sweeps ember rather than gold: a tier above the ascension, read at a glance.
			Color = isApotheosis ? new Color(1.0f, 0.62f, 0.34f, 0.30f) : new Color(1.0f, 0.94f, 0.70f, 0.20f),
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

	// The bail the charm hangs from, pinned over the plaque's top edge at its centre. A sprite and
	// not part of the plaque because the top of the plaque is a tiled band, which can only carry
	// what is constant along it. Drawn at its authored x2 (48x36), never scaled, and it rises 12
	// pixels above the card so it reads as hanging rather than printed on.
	private static void AddBoonClasp(Button card)
	{
		var clasp = new TextureRect
		{
			Name = "BoonClasp",
			Texture = GD.Load<Texture2D>(BoonClaspTexturePath),
			StretchMode = TextureRect.StretchModeEnum.Keep,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			OffsetLeft = -24,
			OffsetRight = 24,
			OffsetTop = -12,
			OffsetBottom = 24,
		};
		card.AddChild(clasp);
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
		// Named so the screenshot harness can move the selection onto a card (_UiShot --focus-card). A
		// form's card on the choice page is named for its form.
		card.Name = "OptionCard_" + (string.IsNullOrEmpty(option.VariantId) ? option.SpellId : option.VariantId);

		// THE CARD FAMILIES (CardFamilyArt, .ai/level-up-screen.md "The card families"). Every spell
		// card is the torn page, and how the scribe marked it says which kind of choice it is:
		//   new spell  the page, unmarked        upgrade   the page rubricated: a red rule round it
		//   mutation   the page stained teal at its edge, glowing (MutationGlow), sealed in wax
		//   boon       the reliquary plaque - not a spell, so not a page
		// The upgrade was a red silk bookmark and the mutation a white vellum card for a day; the
		// player found both too loud for the two commonest choices in a run ("the level up coloring
		// should be subtler, maybe just an outline to the page"). Every family rests on its frame and
		// lifts to the lit one, and the lit one changes only the furniture - the rule's gilt, the
		// bezel, the runes - never the face under the words.
		CardFamily family = FamilyOf(option);
		StyleBoxTexture resting = cardStyleResting;
		StyleBoxTexture lit = cardStyleLit;
		switch (family)
		{
			case CardFamily.Boon:
				resting = boonStyleResting;
				lit = boonStyleLit;
				AddBoonClasp(card);
				break;
			case CardFamily.Upgrade:
				(resting, lit) = FamilyFrames(family);
				break;
			case CardFamily.Mutation:
				(resting, lit) = FamilyFrames(family);
				CardFamilyArt.AddWaxSeal(card);
				CardFamilyArt.AddMutationGlow(card);
				break;
			case CardFamily.Ascension:
			case CardFamily.Apotheosis:
				(resting, lit) = FamilyFrames(family);
				break;
		}

		card.AddThemeStyleboxOverride("normal", resting);
		card.AddThemeStyleboxOverride("hover", lit);
		card.AddThemeStyleboxOverride("pressed", lit);
		card.AddThemeStyleboxOverride("focus", lit);

		ApplyAscensionShine(card, option);
	}


	// The evolution view is laid out as its own full-rect overlay on the Panel rather than inside
	// the normal view's 3-column options grid. The grid positions children as grid cells, which
	// left the title floating in dead space and the Back button stranded mid-screen; owning the
	// whole panel lets the title pin to the top, the cards fill the middle, and Back sit at the
	// bottom. The normal view is hidden wholesale while this is up.
	// Greys out an ascension the player has not earned and writes the requirement under it.
	//
	// Done as a pass OVER the finished card rather than as a parameter threaded into the two card
	// builders, because there are two of them (narrow and wide) with different internals and the
	// lock has nothing to do with how either lays itself out. Whatever the card is made of, this
	// finds its buttons and turns them off.
	//
	// The requirement is a band BELOW the card, in the layout, not a label anchored over it. Anchored,
	// it was laid out by the phone card's PanelContainer like any other child - centred over the
	// description - and on a plate a solid band there hid the words it was explaining. Returns what
	// the caller should add: the card itself, or the card with its band.
	private Control ApplyAscensionLock(Control card, LevelUpOption option, SpellEvolutionOption evo)
	{
		if (card == null || evo == null || option?.EvolutionLockReason == null)
			return card;

		if (!option.EvolutionLockReason.TryGetValue(evo.Id, out string reason) || string.IsNullOrEmpty(reason))
			return card;

		card.Modulate = new Color(0.80f, 0.77f, 0.72f);
		DisableButtonsIn(card);

		var stamp = new Label
		{
			Text = reason,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		ResponsiveLayout.SetFont(stamp, ResponsiveLayout.TextRole.Label);
		// On a band of its own: bone on red, 10.1:1, which reads beside the parchment and the black
		// glass alike. It was Rubric straight onto the card, which on a plate is red on near-black.
		var stampBand = new StyleBoxFlat { BgColor = CardFamilyArt.LockBand };
		stampBand.SetCornerRadiusAll(3);
		stampBand.SetContentMarginAll(4);
		stamp.AddThemeStyleboxOverride("normal", stampBand);
		stamp.AddThemeColorOverride("font_color", CardFamilyArt.LockInk);

		// The PC card is already a column (card, then anything under it); a phone card is not.
		if (card is VBoxContainer column)
		{
			column.AddChild(stamp);
			return card;
		}
		var wrapper = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		wrapper.AddThemeConstantOverride("separation", 4);
		wrapper.AddChild(card);
		wrapper.AddChild(stamp);
		return wrapper;
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
		RearmForNewChoices();

		// Level 8 is a fork, not a row of cards: the spell above, an arrow down to each path
		// (BuildAscensionChoicePage, LevelUpMenu.Ascensions.cs). The mutation and the Apotheosis
		// keep this page.
		if (option.MilestoneLevel >= 8 && option.MilestoneLevel < WizardSurvivors.scripts.ApotheosisCatalog.MilestoneLevel)
		{
			BuildAscensionChoicePage(option);
			return;
		}

		ClearButtons();
		SetNormalViewVisible(false);

		// A viewport resize re-invokes this while the view is already up (OnViewportSizeChanged),
		// so drop any existing overlay first rather than stacking a second one on top.
		RemoveEvolutionRoot();

		bool isAscension = option.MilestoneLevel >= 8;
		bool isApotheosis = option.MilestoneLevel >= WizardSurvivors.scripts.ApotheosisCatalog.MilestoneLevel;

		// THE PANEL BECOMES A PAGE. This is the one screen in a run where the player is reading
		// about what their magic is turning into rather than picking the next number to go up, so
		// it is the screen the manuscript register exists for.
		ShowVellumPage(true);

		var root = new VBoxContainer { Name = EvolutionRootName };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		// Tight vertical margins: on a 720-high PC canvas every pixel here is a line of a branch's
		// description that otherwise needs scrolling to (BuildEvolutionOptionCard).
		root.OffsetLeft = 24;
		// Inside the vellum page (inset PageInset), not across its edge: at 14 the heading's top sat
		// on the page's torn rim and the stone outside it, which the ink report measured at 3.2:1.
		root.OffsetTop = PageInset + 6f;
		root.OffsetRight = -24;
		root.OffsetBottom = -PageInset;
		root.AddThemeConstantOverride("separation", 6);
		panel.AddChild(root);

		// Title block pinned to the top, replacing the normal view's "Level Up!" heading.
		var headerBox = new VBoxContainer();
		headerBox.AddThemeConstantOverride("separation", 4);
		headerBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		headerBox.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;

		bool narrow = ResponsiveLayout.IsNarrow(this);

		var title = new Label();
		// Level 8 was "ULTIMATE ASCENSION", which collided with the tier above it (#59): two things
		// called ultimate one step apart. Level 8 is the ascension; the final form is the Apotheosis.
		title.Text = isApotheosis ? "✶ APOTHEOSIS ✶"
			: isAscension ? "★ ASCENSION (Level 8) ★" : "✦ SPELL MUTATION (Level 4) ✦";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		// The Title role on a phone, Display on a PC: at the phone's Display size (40, bold) the
		// heading broke as "(LEVEL / 4) ✦" over two lines, which reads as two headings.
		ResponsiveLayout.SetFont(title, narrow ? ResponsiveLayout.TextRole.Title : ResponsiveLayout.TextRole.Display);
		// In the family's darkest tone - teal-black, glass-black, ember red - not the gold-brown that
		// made the level 8 heading the muddiest thing on a tan page.
		CardFamily pageFamily = EvolutionFamilyOf(option);
		title.AddThemeColorOverride("font_color", HeadingInkFor(pageFamily));
		headerBox.AddChild(title);

		var subtitle = new Label();
		// COUNT THE BRANCHES, do not assert a number. These read "1 of 2" and "1 of 3" as
		// literals, so a milestone offering a different number of branches - which the catalog is
		// free to do - told the player something false at the exact moment they were being asked
		// to compare what was on screen. Caught by measuring this screen with a two-branch sample
		// and reading back "Choose 1 of 3".
		int branchCount = option?.EvolutionChoices?.Count ?? 0;
		// One short line, at the Label role: the heading already says what kind of page this is.
		subtitle.Text = isApotheosis
			? $"{option.DisplayName}'s final form."
			: $"Choose 1 of {branchCount}. {option.DisplayName} keeps it for the rest of this run.";
		subtitle.HorizontalAlignment = HorizontalAlignment.Center;
		subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		ResponsiveLayout.SetFont(subtitle, ResponsiveLayout.TextRole.Label);
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

			// Margins inside the scroll, which clips: the wax seal rises 12 above a card.
			var columnMargin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			columnMargin.AddThemeConstantOverride("margin_top", 12);
			columnMargin.AddThemeConstantOverride("margin_bottom", 4);
			scroll.AddChild(columnMargin);
			var cardsColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			cardsColumn.AddThemeConstantOverride("separation", 16);
			columnMargin.AddChild(cardsColumn);

			foreach (var evo in option.EvolutionChoices)
			{
				Control card = ApplyAscensionLock(
					BuildNarrowEvolutionCard(option, evo, () => OnEvolutionChosen(option, evo)), option, evo);
				card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				cardsColumn.AddChild(card);
			}
		}
		else
		{
			// In a scroll, like the phone's column. The cards size to their text now
			// (BuildEvolutionOptionCard), and a branch with a long description and a long synergy
			// line is taller than the band a 720-high canvas leaves between the heading and Back -
			// it used to simply run out of the bottom of its card and over the plate.
			// Not FollowFocus: a card taller than the band is "visible" only once scrolled to its
			// foot, so following focus opened the page scrolled down with every icon cut in half.
			// Focus moves sideways between the cards here; the scroll only ever has to go down.
			var wideScroll = new ScrollContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill,
				HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
				FollowFocus = false
			};
			root.AddChild(wideScroll);

			var rowMargin = new MarginContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill
			};
			rowMargin.AddThemeConstantOverride("margin_top", 12);
			wideScroll.AddChild(rowMargin);
			var cardsRow = new HBoxContainer();
			cardsRow.Alignment = BoxContainer.AlignmentMode.Center;
			cardsRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			cardsRow.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			cardsRow.AddThemeConstantOverride("separation", 14);
			rowMargin.AddChild(cardsRow);

			foreach (var evo in option.EvolutionChoices)
			{
				Control card = ApplyAscensionLock(
					BuildEvolutionOptionCard(option, evo, () => OnEvolutionChosen(option, evo)), option, evo);
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
		// half off the edge of the screen - and centred on the heading band (StyleHeading), which is
		// MinTouchTarget + 8 tall and starts where the column does: 26 on a PC, 16 on a phone.
		float bandTop = ResponsiveLayout.IsNarrow(this) ? 16f : 26f;
		ascensionButton.OffsetLeft = -(156 + 24);
		ascensionButton.OffsetRight = -24;
		ascensionButton.OffsetTop = bandTop + 4f;
		ascensionButton.OffsetBottom = bandTop + 4f + ResponsiveLayout.MinTouchTarget;

		ResponsiveLayout.SetFont(ascensionButton, ResponsiveLayout.TextRole.Label);
		// Normal, not Quiet. Quiet rests on the UNLIT frame, which is the same frame a disabled
		// button wears - on a screen where everything else is bright parchment it read as greyed
		// out, which is the one thing this button must not look like.
		BonelightSkin.StyleButton(ascensionButton, BonelightSkin.Emphasis.Normal);
		ascensionButton.Pressed += BuildAscensionBrowser;
		panel.AddChild(ascensionButton);
		ascensionGlow = AscensionReadyGlow.AttachTo(ascensionButton, showRing: true);
		RefreshAscensionReadiness();

		// The offsets above are the first guess; this keeps the button centred on the heading band
		// wherever layout actually puts it (the column can grow, and SafeArea can move it). The
		// column too, not only the heading: when the column grows past the panel it moves as a whole,
		// the heading's own rect inside it never changes, and the button was left wherever the last
		// transient put it - a level-up builds its cards before it hides the empty charge row, so for
		// one layout the column is 74 taller and the button was aligned to a heading 33 above the
		// panel, and stayed there.
		if (GetNodeOrNull<Control>("Panel/VBoxContainer/Label") is Control heading)
			heading.ItemRectChanged += AlignAscensionButton;
		if (GetNodeOrNull<Control>("Panel/VBoxContainer") is Control column)
			column.ItemRectChanged += AlignAscensionButton;
	}

	private AscensionReadyGlow ascensionGlow;

	// How many spells have an Ascension the player can take right now: one in the strip above the
	// cards (Player.GetAscensionOffers, which also carries the Apotheosis), or a branch the browser
	// would mark ready. Counted per spell, because that is what the strip shows one button for.
	private int CountReadyAscensions()
	{
		var ready = new HashSet<string>();
		foreach (LevelUpOption option in currentAscensions)
			if (option != null && !string.IsNullOrEmpty(option.SpellId))
				ready.Add(option.SpellId);
		foreach (EquippedSpellInfo spell in currentEquippedSpells ?? new List<EquippedSpellInfo>())
			if (spell?.Ascensions != null && spell.Ascensions.Any(a => a.Open))
				ready.Add(spell.Id);
		return ready.Count;
	}

	// The corner button wakes up while an Ascension is waiting (AscensionReadyGlow) and is still
	// otherwise. Gold lettering as well as the motion, so a still screenshot - or a player who does
	// not notice motion - still sees it.
	private void RefreshAscensionReadiness()
	{
		if (ascensionButton == null || !IsInstanceValid(ascensionButton))
			return;

		int ready = CountReadyAscensions();
		if (ascensionGlow != null && IsInstanceValid(ascensionGlow))
		{
			ascensionGlow.Count = ready;
			ascensionGlow.SetActive(ready > 0);
		}

		if (ready > 0)
		{
			Color gold = new Color(1.0f, 0.88f, 0.50f);
			ascensionButton.AddThemeColorOverride("font_color", gold);
			ascensionButton.AddThemeColorOverride("font_hover_color", gold.Lightened(0.2f));
			ascensionButton.AddThemeColorOverride("font_focus_color", gold.Lightened(0.2f));
			// No tooltip on a touchscreen: the emulated mouse rests where the finger lifted and would raise one.
			if (!DisplayServer.IsTouchscreenAvailable())
				ascensionButton.TooltipText = ready == 1 ? "An ascension is ready" : $"{ready} ascensions are ready";
		}
		else
		{
			// Back to BonelightSkin.StyleButton's Normal inks.
			ascensionButton.AddThemeColorOverride("font_color", new Color(0.94f, 0.93f, 0.88f));
			ascensionButton.AddThemeColorOverride("font_hover_color", new Color(1.0f, 0.98f, 0.92f));
			ascensionButton.RemoveThemeColorOverride("font_focus_color");
			ascensionButton.TooltipText = string.Empty;
		}
	}

	private void AlignAscensionButton()
	{
		var panel = GetNodeOrNull<Control>("Panel");
		var heading = GetNodeOrNull<Control>("Panel/VBoxContainer/Label");
		if (panel == null || heading == null || ascensionButton == null || !IsInstanceValid(ascensionButton))
			return;
		if (!heading.IsVisibleInTree())
			return;

		float top = heading.GlobalPosition.Y - panel.GlobalPosition.Y
			+ (heading.Size.Y - ResponsiveLayout.MinTouchTarget) * 0.5f;
		ascensionButton.OffsetTop = top;
		ascensionButton.OffsetBottom = top + ResponsiveLayout.MinTouchTarget;
	}

	// The ascension browser and the level 8 fork are in LevelUpMenu.Ascensions.cs.

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
		CardFamily family = EvolutionFamilyOf(option);

		var root = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		root.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

		var card = new Button
		{
			CustomMinimumSize = new Vector2(0, NarrowRowMinHeight),
			ClipText = false,
			Text = string.Empty
		};
		card.Pressed += onPressed;
		ApplyEvolutionCardStyle(card, family);
		root.AddChild(card);

		var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		// Inside the frame's inner border: the page's rule ends 14 drawn pixels in, the plates' gold
		// line 18. The head is a Title, whose box carries its own air above the capitals; the foot -
		// the advice line - stands FootClearance clear (CardFamilyArt.FootInset), where it was 6 to 9
		// on the vellum and the plates.
		margin.AddThemeConstantOverride("margin_left", 20);
		margin.AddThemeConstantOverride("margin_right", 20);
		margin.AddThemeConstantOverride("margin_top", CardFamilyArt.InnerBorder(family) + 6);
		margin.AddThemeConstantOverride("margin_bottom", CardFamilyArt.FootInset(family));
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
		iconFrame.AddChild(BuildEvolutionIcon(option, evo, family, family == CardFamily.Mutation ? 64f : NarrowRowIconColumn));
		row.AddChild(iconFrame);

		var text = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		text.AddThemeConstantOverride("separation", 6);
		row.AddChild(text);

		var name = new Label
		{
			Text = evo.DisplayName,
			HorizontalAlignment = HorizontalAlignment.Left,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		ResponsiveLayout.SetFont(name, ResponsiveLayout.TextRole.Title);
		name.AddThemeColorOverride("font_color", TitleInk(family));
		text.AddChild(name);

		if (!string.IsNullOrWhiteSpace(evo.SynergyTag))
			text.AddChild(BuildSynergyChip(evo.SynergyTag, family, Control.SizeFlags.ShrinkBegin));

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
			desc.AddThemeColorOverride("font_color", BodyInk(family));
			text.AddChild(desc);
		}

		Control narrowElements = BuildTagGainRow(evo, option, FlowContainer.AlignmentMode.Begin, family);
		if (narrowElements != null)
			text.AddChild(narrowElements);
		// No advice line here either (BuildEvolutionOptionCard).

		return root;
	}

	// Border and hover treatment shared by both evolution card shapes.
	// The parchment ramp, from .ai/art-direction.md section 3 - `flesh` is the contract's name
	// for it; on this screen it is vellum and ink. Rubric is the `red` row, which is what a scribe
	// reached for when a line mattered more than the ones around it.
	//
	// InkSoft and Rubric were one step too light (2026-10-14): on the resting page card - the darker,
	// orange vellum every unchosen option sits on - the tags, the "+1 Ice" line and the red advice
	// lines measured about 3.4:1 and 3.7:1, and the level 4 mutation screen was reported unreadable.
	// InkSoft is now robe violet `base` (#342C4C, ~5.3:1 on the orange page, over 9:1 on the lit one)
	// rather than flesh `deep`, which is Ink itself and would erase the difference between the two;
	// Rubric is cloth red `lit` (#6A2028, ~4.6:1) rather than `hi`. InkGold already passed.
	// All four are measured against the sampled page art by tools/art/ui_contrast.py, which keeps
	// its own copy of these values: change one here, change it there.
	private static readonly Color Ink = new Color(0.235f, 0.141f, 0.212f);
	private static readonly Color InkSoft = new Color(0.204f, 0.173f, 0.298f);
	private static readonly Color InkGold = new Color(0.329f, 0.247f, 0.063f);
	private static readonly Color Rubric = new Color(0.416f, 0.125f, 0.157f);

	// The branch card is the milestone's family: the stained, glowing page for a mutation (with its
	// wax seal), the obsidian plate for an Ascension, the ember plate for the Apotheosis. The branch
	// cards were once vellum cards on the vellum page - parchment on parchment, the card a rule drawn
	// on the page rather than an object on it - and then white vellum in a dark teal band, which
	// the player found too loud. A mutation is a page again, and what lifts it off the page it lies
	// on is its torn, teal-stained edge and the light breathing out of it (MutationGlow). Rests on
	// the frame and lifts to the lit one; the face never changes.
	private void ApplyEvolutionCardStyle(Button card, CardFamily family)
	{
		(StyleBoxTexture resting, StyleBoxTexture lit) = EvolutionFrames(family);
		CardFamilyArt.ApplyFrame(card, resting, lit);
		if (family == CardFamily.Mutation)
		{
			CardFamilyArt.AddWaxSeal(card);
			CardFamilyArt.AddMutationGlow(card);
		}
	}

	private Control BuildEvolutionOptionCard(LevelUpOption option, SpellEvolutionOption evo, Action onPressed)
	{
		CardFamily family = EvolutionFamilyOf(option);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 8);
		column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		// Fill the row vertically too, so the cards are the dominant element in the middle band
		// rather than sitting at their minimum height with dead space under them.
		column.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

		var card = new Button();
		card.CustomMinimumSize = new Vector2(CardWidth, 0);
		card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		card.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		card.ClipText = false;
		card.Text = string.Empty;
		card.Pressed += onPressed;

		ApplyEvolutionCardStyle(card, family);

		// Inside the frame's inner border (the page's rule ends 14 drawn pixels in, the plates' gold
		// line 18), so no word sits on the band. The head is the Title's box, which has its own air
		// above the capitals; the foot - the advice line - stands FootClearance clear of the border
		// (CardFamilyArt.FootInset). It was 18 from the edge: 5 to 10 of air on the vellum and 1 on
		// the plates, where the advice's last line all but touched the gilt.
		const float side = 16f;
		float head = CardFamilyArt.InnerBorder(family) + 2f;
		float foot = CardFamilyArt.FootInset(family);
		var content = new VBoxContainer();
		content.MouseFilter = Control.MouseFilterEnum.Ignore;
		content.AddThemeConstantOverride("separation", 5);
		content.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		content.OffsetLeft = side;
		content.OffsetTop = head;
		content.OffsetRight = -side;
		content.OffsetBottom = -foot;
		card.AddChild(content);

		// A Button does not size itself to a child it does not lay out, so the card took a fixed
		// 468 and the text simply ran out of the bottom of it. Follow the content instead; the
		// scroll the cards sit in absorbs whatever does not fit.
		content.MinimumSizeChanged += () =>
		{
			if (IsInstanceValid(card) && IsInstanceValid(content))
				card.CustomMinimumSize = new Vector2(CardWidth, content.GetCombinedMinimumSize().Y + head + foot);
		};

		var title = new Label
		{
			Text = evo.DisplayName,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		ResponsiveLayout.SetFont(title, ResponsiveLayout.TextRole.Title);
		title.AddThemeColorOverride("font_color", TitleInk(family));
		content.AddChild(title);

		// The icon shares a row with the synergy tag rather than having a row of its own: on a
		// 720-high canvas a row of icon was the two lines of description that otherwise fell below the
		// fold once the cards became framed objects. It is drawn at 64 - two canvas units an art pixel,
		// as on a phone's branch card - since every branch has a picture of its own (EvolutionIcons,
		// 2026-10-28): at 24 it was a variant of the spell's icon and a thumbnail was enough, and the
		// 24 dropped three rows and columns of every eight. The advice line that made the room tight
		// is gone (BuildTagGainRow's remarks), and a 1280x720 mutation page still has some 150 spare.
		var iconRow = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		iconRow.AddThemeConstantOverride("separation", 10);
		iconRow.AddChild(BuildEvolutionIcon(option, evo, family, 64f));
		if (!string.IsNullOrWhiteSpace(evo.SynergyTag))
		{
			Control chip = BuildSynergyChip(evo.SynergyTag, family, Control.SizeFlags.ShrinkCenter);
			chip.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			iconRow.AddChild(chip);
		}
		content.AddChild(iconRow);

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
			desc.AddThemeColorOverride("font_color", BodyInk(family));
			content.AddChild(desc);
		}

		Control wideElements = BuildTagGainRow(evo, option, FlowContainer.AlignmentMode.Center, family);
		if (wideElements != null)
			content.AddChild(wideElements);

		// No advice line ("Synergizes with raw damage and attack speed - the shards fly straight, so
		// ..."): two or three lines a card that pushed the level 8 paths below a 1280x720 screen, saying
		// at length what the synergy chip above says in two words (2026-10-27, "redesign the sizing /
		// text displayed").

		column.AddChild(card);
		return column;
	}
	/// <summary>
	/// The element an evolution adds, as that element's own chip - "Arcane 3/4" in Arcane's colour, the
	/// count the player would hold after taking it over the next tier's threshold, the same chip a new
	/// spell's card wears - and a short note on what it keeps or costs. Null for the untagged options,
	/// which is most of them.
	/// </summary>
	/// <remarks>
	/// It was a line of text, "+1 Arcane  (+12% cooldown)", in one ink whatever the element
	/// (2026-10-27, "when you add a tag, show the tag color ... the +1 tag should not just be text, it
	/// should show the color text icon"). The chip is the one every other screen uses
	/// (CodexRows.BuildChip's ElementChip): a tap or a click opens what the element does at each tier
	/// and how close this takes the player to the next (its Gain is added to the live count), and on a
	/// pad Y does (LevelUpMenu._Input). Not a focus stop: the card is the stop, and a chip the stick
	/// also landed on would make every card two presses wide.
	///
	/// It said "Arcane +1" until 2026-10-28: "when you see that a mutation or item adds more of a specific
	/// tag, it should show the typical tag that tells you how many tags you have out of how many you need
	/// for the next level." A gain is arithmetic the player has to do on a paused screen; the count after
	/// it is the answer (BuildSpellTagChips' remarks). The +1 is still on the chip's card.
	///
	/// What it is FOR: the most consequential half of the level 4 and level 8 choice is whether the
	/// branch deepens the spell's element - keeping it attuned and pushing its tier up - or trades
	/// attunement for a second one, and a pure spell's gain is free (it is the compensation for its
	/// halved base weight) while a hybrid's is bought with cooldown, which has to be on the card or
	/// the choice looks like a free upgrade.
	/// </remarks>
	private Control BuildTagGainRow(SpellEvolutionOption evo, LevelUpOption option,
		FlowContainer.AlignmentMode alignment, CardFamily family)
	{
		if (evo?.BonusElementWeights == null || evo.BonusElementWeights.Count == 0)
			return null;

		// The spell's existing tags, so the note can say which way this branch goes. Empty when the
		// preview data was not filled in, in which case the note says only what it costs.
		Dictionary<string, int> current = option?.SpellElementTags ?? new Dictionary<string, int>();
		bool deepens = false;
		bool branches = false;

		var row = new HFlowContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Alignment = alignment,
		};
		row.AddThemeConstantOverride("h_separation", 8);
		row.AddThemeConstantOverride("v_separation", 4);

		foreach (var pair in evo.BonusElementWeights)
		{
			if (!Enum.TryParse(pair.Key, true, out Element element))
				continue;
			bool alreadyCarried = current.ContainsKey(pair.Key);
			deepens |= alreadyCarried && current.Count == 1;
			branches |= !alreadyCarried && current.Count == 1;
			Color face = CodexRows.ChipFill(ChipBackground(ElementColors.GetColor(element), family));
			// What the player holds now (every spell, boon and relic) plus what this branch adds. -1 only
			// with no player in the tree (a tool's preview), when the gain is all there is to say.
			int held = ElementTagPopover.HeldCount(this, element);
			int after = held >= 0 ? held + pair.Value : -1;
			string text = after >= 0 ? $"{element} {ElementPassiveDescriptions.GetProgressLabel(after)}" : $"{element} +{pair.Value}";
			ElementChip chip = ElementChip.Create(element, text,
				face, face.Blend(new Color(InkSoft, 0.45f)), StatIcons.ReadableOn(face),
				focusable: false, countAfter: after, padX: 8, padY: 3);
			chip.Gain = pair.Value;
			row.AddChild(chip);
		}
		if (row.GetChildCount() == 0)
			return null;

		// A trade (SpellEvolutionOption.ReplacesElement) is neither a deepening nor a branch: the spell
		// keeps the same number of tags and gives one up, so the note says which - it is not a gain,
		// so there is nothing to price.
		string note = !string.IsNullOrEmpty(evo.ReplacesElement) ? $"replaces {evo.ReplacesElement}"
			: deepens ? "stays attuned"
			: branches ? "attunement lost"
			: $"+{(SpellEvolutionCatalog.AttunementGainCooldownCost - 1f) * 100f:0}% cooldown";
		// Gold for the branch that keeps the spell attuned, the quiet ink otherwise - each in the
		// family's own register: dark gold on the page, gold lit on the glass.
		bool dark = CardFamilyArt.IsDark(family);
		Color keeps = dark ? CardFamilyArt.AscensionAdvice : InkGold;
		Label noteLabel = CodexRows.MakeText(note, ResponsiveLayout.TextRole.Label, deepens ? keeps : SoftInk(family));
		noteLabel.AutowrapMode = TextServer.AutowrapMode.Off;
		row.AddChild(noteLabel);
		return row;
	}

}
