using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

/// <summary>
/// Modal that explains one synergy set in full: its flavour line, every numeric bonus it grants
/// (straight from <see cref="ChestSetEffect"/>, so the screen quotes the implementation rather
/// than paraphrasing it), and which relics are still missing.
///
/// Opened by tapping a set icon in the <see cref="ChestItemHUD"/> strip. The strip only has room
/// for a 48px icon and a progress count, so all detail lives here instead of the wall of text the
/// HUD used to render permanently over the play area.
/// </summary>
public partial class SynergyDetailScreen : CanvasLayer
{
	// Above the escape menu (layer 100): a synergy opened from the HUD must not appear behind it.
	private const int ScreenLayer = 110;

	private Control root = null!;
	private PanelContainer panel = null!;
	private TextureRect headerIcon = null!;
	private Label headerTitle = null!;
	private Label headerSubtitle = null!;
	private ScrollContainer scroll = null!;
	private VBoxContainer body = null!;

	// Only unpause on close if *we* paused. The screen can be opened over the escape menu, which
	// has its own pause, and stealing that would resume the run behind an open menu.
	private bool pausedByUs;

	public override void _Ready()
	{
		Layer = ScreenLayer;
		// Always, not WhenPaused: this screen pauses the tree itself, and a node that stops
		// processing the instant it pauses can never process its own close button.
		ProcessMode = ProcessModeEnum.Always;
		Visible = false;
		BuildStructure();
		MenuNavigator.Attach(this);
	}

	private void BuildStructure()
	{
		root = new Control
		{
			Name = "SynergyRoot",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ProcessMode = ProcessModeEnum.Always
		};
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(root);

		// Tapping the dimmed area closes. On a phone there is no Escape key, and reaching a
		// corner button one-handed is worse than tapping anywhere outside the card.
		// Not Flat: a flat Button skips its styleboxes entirely, which silently left the arena
		// undimmed behind the card.
		var dim = new Button
		{
			Name = "BackgroundDim",
			MouseFilter = Control.MouseFilterEnum.Stop,
			ProcessMode = ProcessModeEnum.Always
		};
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		var dimStyle = new StyleBoxFlat { BgColor = new Color(0.02f, 0.025f, 0.04f, 0.86f) };
		dim.AddThemeStyleboxOverride("normal", dimStyle);
		dim.AddThemeStyleboxOverride("hover", dimStyle);
		dim.AddThemeStyleboxOverride("pressed", dimStyle);
		dim.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		dim.Pressed += Close;
		root.AddChild(dim);

		Vector2 viewport = ResponsiveLayout.ViewportSize(this);
		Vector2 panelSize = new Vector2(
			Mathf.Min(760f, viewport.X - 32f),
			Mathf.Min(1000f, viewport.Y - 96f));

		panel = new PanelContainer
		{
			Name = "SynergyPanel",
			CustomMinimumSize = panelSize,
			ProcessMode = ProcessModeEnum.Always
		};
		panel.AnchorLeft = 0.5f;
		panel.AnchorTop = 0.5f;
		panel.AnchorRight = 0.5f;
		panel.AnchorBottom = 0.5f;
		panel.OffsetLeft = -panelSize.X * 0.5f;
		panel.OffsetTop = -panelSize.Y * 0.5f;
		panel.OffsetRight = panelSize.X * 0.5f;
		panel.OffsetBottom = panelSize.Y * 0.5f;

		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.09f, 0.13f, 0.99f),
			BorderColor = new Color(0.85f, 0.68f, 0.28f, 0.95f)
		};
		panelStyle.SetBorderWidthAll(2);
		panelStyle.SetCornerRadiusAll(12);
		panelStyle.SetContentMarginAll(16);
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		root.AddChild(panel);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 12);
		panel.AddChild(column);

		var headerRow = new HBoxContainer();
		headerRow.AddThemeConstantOverride("separation", 12);
		column.AddChild(headerRow);

		headerIcon = new TextureRect
		{
			CustomMinimumSize = new Vector2(64, 64),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		headerRow.AddChild(headerIcon);

		var headerText = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		headerText.AddThemeConstantOverride("separation", 2);
		headerRow.AddChild(headerText);

		headerTitle = new Label { Text = "Synergy" };
		ResponsiveLayout.SetFont(headerTitle, ResponsiveLayout.TextRole.Title);
		headerTitle.AddThemeColorOverride("font_color", new Color(1.0f, 0.88f, 0.40f));
		headerText.AddChild(headerTitle);

		headerSubtitle = new Label { Text = string.Empty };
		ResponsiveLayout.SetFont(headerSubtitle, ResponsiveLayout.TextRole.Micro);
		headerSubtitle.AddThemeColorOverride("font_color", new Color(0.72f, 0.76f, 0.86f));
		headerText.AddChild(headerSubtitle);

		var closeButton = new Button
		{
			Text = "✕",
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			ProcessMode = ProcessModeEnum.Always
		};
		ResponsiveLayout.SetFont(closeButton, ResponsiveLayout.TextRole.Title);
		ResponsiveLayout.EnsureTouchTarget(closeButton);
		closeButton.Pressed += Close;
		headerRow.AddChild(closeButton);

		scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			ProcessMode = ProcessModeEnum.Always,
			FollowFocus = true
		};
		column.AddChild(scroll);

		body = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		body.AddThemeConstantOverride("separation", 10);
		scroll.AddChild(body);
	}

	/// <summary>Opens the screen on one synergy set, showing its bonuses and item checklist.</summary>
	public void OpenSet(ChestSetDefinition set, IReadOnlyCollection<string> ownedItems, bool isActive)
	{
		if (set == null)
			return;

		ownedItems ??= Array.Empty<string>();
		ClearBody();

		var (ownedCount, totalRequired) = ChestItemCatalog.GetSetProgress(set, ownedItems);

		headerIcon.Texture = BonelightSkin.LoadTextureSafe(set.IconPath);
		headerIcon.Modulate = isActive ? Colors.White : new Color(0.62f, 0.66f, 0.74f);
		headerTitle.Text = set.Name;
		headerSubtitle.Text = isActive
			? $"ACTIVE • all {totalRequired} relics collected"
			: $"{ownedCount} of {totalRequired} relics collected";
		headerSubtitle.AddThemeColorOverride("font_color",
			isActive ? new Color(0.55f, 1.0f, 0.62f) : new Color(0.95f, 0.82f, 0.40f));

		body.AddChild(BuildFlavourLabel(set.Description));

		// The partial tier is listed FIRST when the set has one, and that ordering is the point of
		// it: a player two relics into a three-relic set should see the thing they have already
		// earned before the thing they have not. Listing the full effect first made every partial
		// set read as "you have nothing yet".
		bool hasPartial = set.PartialItemCount > 0
			&& set.PartialEffects != null
			&& set.PartialEffects.Length > 0;
		bool partialActive = hasPartial && ownedCount >= set.PartialItemCount;

		if (hasPartial)
		{
			body.AddChild(BuildSectionHeader($"At {set.PartialItemCount} relics"));
			foreach (ChestSetEffect effect in set.PartialEffects)
				body.AddChild(BuildEffectRow(effect, partialActive));
		}

		body.AddChild(BuildSectionHeader(hasPartial ? $"At all {totalRequired} relics" : "What it does"));

		foreach (ChestSetEffect effect in set.Effects ?? Array.Empty<ChestSetEffect>())
			body.AddChild(BuildEffectRow(effect, isActive));

		if (!isActive)
		{
			// Full effects stack ON TOP of the partial rather than replacing it, so the notice says
			// "adds" rather than "switches these on" once the partial is already paying out.
			body.AddChild(BuildNoticeLabel(partialActive
				? $"Collect the remaining {totalRequired - ownedCount} relic(s) to add these as well."
				: $"Collect the remaining {totalRequired - ownedCount} relic(s) to switch these on."));
		}

		body.AddChild(BuildSectionHeader("Relics required"));
		foreach (string itemId in set.RequiredItemIds ?? Array.Empty<string>())
			body.AddChild(BuildItemRow(itemId, ownedItems.Contains(itemId, StringComparer.OrdinalIgnoreCase)));

		Present();
	}

	/// <summary>Opens the screen on the player's relic collection rather than a single set.</summary>
	public void OpenRelics(IReadOnlyCollection<string> ownedItems)
	{
		ownedItems ??= Array.Empty<string>();
		ClearBody();

		// The empty id falls through GetIconPath's default arm to the treasure-chest icon, which is
		// exactly the "relic pouch" image the HUD strip uses for this button.
		headerIcon.Texture = BonelightSkin.LoadTextureSafe(ChestItemCatalog.GetIconPath(string.Empty));
		headerIcon.Modulate = Colors.White;
		headerTitle.Text = "Relics";
		headerSubtitle.Text = ownedItems.Count == 1 ? "1 relic carried" : $"{ownedItems.Count} relics carried";
		headerSubtitle.AddThemeColorOverride("font_color", new Color(0.72f, 0.76f, 0.86f));

		if (ownedItems.Count == 0)
		{
			body.AddChild(BuildNoticeLabel("You are not carrying any relics yet. Break open a treasure chest to claim one."));
			Present();
			return;
		}

		foreach (string itemId in ownedItems)
			body.AddChild(BuildItemRow(itemId, true));

		Present();
	}

	private void Present()
	{
		Show();
		if (!GetTree().Paused)
		{
			GetTree().Paused = true;
			pausedByUs = true;
		}

		FitPanelToContent();

		// This screen opens OVER the level-up menu, which still holds the selection. Taking it here
		// is what makes the handover explicit: after this, the navigator on the screen underneath
		// leaves the selection alone because it can see that a menu outside itself owns it.
		MenuNavigator.Attach(this);
	}

	// Shrinks the card to the height its content actually needs, so a two-effect set does not open
	// as a mostly-empty full-screen slab.
	//
	// The measurement has to wait two frames: the body lives inside a ScrollContainer, whose whole
	// job is to *not* report its content's height, and the wrapped labels inside only know their
	// real height once they have been laid out at their final width. Reading body.Size after the
	// layout pass is the only number that accounts for wrapping.
	private async void FitPanelToContent()
	{
		Vector2 viewport = ResponsiveLayout.ViewportSize(this);
		float width = Mathf.Min(760f, viewport.X - 32f);
		float maxHeight = viewport.Y - 96f;

		panel.CustomMinimumSize = new Vector2(width, 0f);
		panel.OffsetLeft = -width * 0.5f;
		panel.OffsetRight = width * 0.5f;
		panel.OffsetTop = -maxHeight * 0.5f;
		panel.OffsetBottom = maxHeight * 0.5f;

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		if (!IsInstanceValid(this) || !Visible)
			return;

		// Panel height minus the scroll viewport's height is everything that is not the list:
		// content margins, the header row, and the separation between them.
		float chrome = panel.Size.Y - scroll.Size.Y;
		float desired = Mathf.Clamp(chrome + body.Size.Y, 240f, maxHeight);

		panel.OffsetTop = -desired * 0.5f;
		panel.OffsetBottom = desired * 0.5f;
	}

	public void Close()
	{
		if (!Visible)
			return;

		Hide();
		if (pausedByUs)
		{
			GetTree().Paused = false;
			pausedByUs = false;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!Visible)
			return;

		// Escape on desktop, the hardware back gesture on Android - both map to ui_cancel.
		if (@event.IsActionPressed("ui_cancel"))
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	private void ClearBody()
	{
		foreach (Node child in body.GetChildren())
		{
			body.RemoveChild(child);
			child.QueueFree();
		}
	}

	private static Label BuildFlavourLabel(string text)
	{
		var label = new Label
		{
			Text = text,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Micro);
		label.AddThemeColorOverride("font_color", new Color(0.68f, 0.88f, 1.0f));
		return label;
	}

	private static Label BuildNoticeLabel(string text)
	{
		var label = new Label
		{
			Text = text,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Micro);
		label.AddThemeColorOverride("font_color", new Color(0.78f, 0.72f, 0.52f));
		return label;
	}

	private static Label BuildSectionHeader(string text)
	{
		var label = new Label { Text = text.ToUpperInvariant() };
		ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Micro);
		label.AddThemeColorOverride("font_color", new Color(0.95f, 0.82f, 0.40f));
		return label;
	}

	// One bonus as a two-column row: what changes on the left, the signed magnitude on the right.
	// Keeping the number in its own right-aligned label is what makes a list of these scannable.
	private static Control BuildEffectRow(ChestSetEffect effect, bool isActive)
	{
		var rowPanel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		var rowStyle = new StyleBoxFlat
		{
			BgColor = isActive ? new Color(0.11f, 0.20f, 0.14f, 0.92f) : new Color(0.11f, 0.12f, 0.17f, 0.92f),
			BorderColor = isActive ? new Color(0.34f, 0.72f, 0.44f, 0.85f) : new Color(0.24f, 0.28f, 0.38f, 0.85f)
		};
		rowStyle.SetBorderWidthAll(1);
		rowStyle.SetCornerRadiusAll(5);
		rowStyle.SetContentMarginAll(8);
		rowPanel.AddThemeStyleboxOverride("panel", rowStyle);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		rowPanel.AddChild(row);

		var label = new Label
		{
			Text = effect.Label,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Micro);
		label.AddThemeColorOverride("font_color", new Color(0.90f, 0.92f, 0.98f));
		row.AddChild(label);

		var value = new Label
		{
			Text = effect.Value,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		ResponsiveLayout.SetFont(value, ResponsiveLayout.TextRole.Micro);
		value.AddThemeColorOverride("font_color",
			isActive ? new Color(0.60f, 1.0f, 0.66f) : new Color(0.72f, 0.76f, 0.86f));
		row.AddChild(value);

		return rowPanel;
	}

	// A relic in the checklist. Unowned entries stay legible but desaturated so the player can see
	// what they are hunting for without mistaking it for something they already carry.
	private static Control BuildItemRow(string itemId, bool owned)
	{
		var rowPanel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		var rowStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.10f, 0.11f, 0.16f, 0.92f),
			BorderColor = owned ? new Color(0.40f, 0.78f, 0.50f, 0.85f) : new Color(0.24f, 0.28f, 0.38f, 0.70f)
		};
		rowStyle.SetBorderWidthAll(1);
		rowStyle.SetCornerRadiusAll(5);
		rowStyle.SetContentMarginAll(8);
		rowPanel.AddThemeStyleboxOverride("panel", rowStyle);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		rowPanel.AddChild(row);

		var icon = new TextureRect
		{
			Texture = BonelightSkin.LoadTextureSafe(ChestItemCatalog.GetIconPath(itemId)),
			CustomMinimumSize = new Vector2(40, 40),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			Modulate = owned ? Colors.White : new Color(0.45f, 0.48f, 0.55f)
		};
		row.AddChild(icon);

		var textColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		textColumn.AddThemeConstantOverride("separation", 1);
		row.AddChild(textColumn);

		var nameLabel = new Label
		{
			Text = (owned ? "✓ " : "") + ChestItemCatalog.GetDisplayName(itemId)
		};
		ResponsiveLayout.SetFont(nameLabel, ResponsiveLayout.TextRole.Micro);
		nameLabel.AddThemeColorOverride("font_color",
			owned ? new Color(0.70f, 1.0f, 0.76f) : new Color(0.78f, 0.80f, 0.88f));
		textColumn.AddChild(nameLabel);

		var descLabel = new Label
		{
			Text = ChestItemCatalog.GetDescription(itemId),
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		ResponsiveLayout.SetFont(descLabel, ResponsiveLayout.TextRole.Micro);
		descLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.66f, 0.76f));
		textColumn.AddChild(descLabel);

		return rowPanel;
	}
}
