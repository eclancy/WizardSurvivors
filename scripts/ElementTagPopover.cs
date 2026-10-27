using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

/// <summary>
/// The card an <see cref="ElementChip"/> opens: what the element does at each of its three tiers,
/// how many of its tags the player holds, and how far the next tier is.
/// </summary>
/// <remarks>
/// ONE AT A TIME, ON ITS OWN LAYER. It is a CanvasLayer of its own under the tree root, above every
/// menu (the set-detail screen is 110) and below the scene transition (200), so it shows over
/// whichever screen the chip was on - the level-up cards, the chest, the pause screen - without
/// that screen having to make room for it. ProcessMode Always, because every one of those screens
/// pauses the tree.
///
/// IT CLOSES ON A TAP ANYWHERE. A full-screen catcher under the card takes the next press and
/// spends it on closing - including a press on another card or on the chip itself, which is what
/// makes a second tap on the chip a toggle. Spending it is deliberate: on the level-up screen a tap
/// that closed the card AND went through would choose whatever spell was under the finger. B or
/// Escape closes it too, and so does the selection leaving the chip (a pad user pushing the stick
/// on), the chip leaving the screen, or the screen hiding.
///
/// THE CARD IS STONE, on parchment and on stone alike: it is a note held up over the page, and a
/// dark card is the one thing that reads as "on top" on all three grounds the chips sit on. Its inks
/// are <see cref="CodexRows.Stone"/>, already measured against that face (tools/art/ui_contrast.py).
/// </remarks>
public partial class ElementTagPopover : CanvasLayer
{
	private const int PopoverLayer = 150;
	private const float MaxWidth = 440f;
	private const float EdgeMargin = 12f;
	private const float Gap = 8f;

	// The open one, if any. A Node, not a RefCounted resource, and checked with IsInstanceValid on
	// every read, so a freed card is never touched through it.
	private static ElementTagPopover current;

	private ElementChip anchor;
	// Opened on a card's behalf rather than from the chip (LevelUpMenu's Y): closes when that card
	// loses the selection, and A closes it instead of reaching the card underneath.
	private Control holder;
	private Control card;
	private bool watchFocus;
	private bool detached;
	private bool placing;

	public static bool IsOpen => current != null && IsInstanceValid(current);

	/// <summary>The chip the open card belongs to, or null.</summary>
	public static ElementChip OpenAnchor => IsOpen ? current.anchor : null;

	/// <summary>
	/// Opens the card for <paramref name="chip"/> on behalf of <paramref name="selected"/> - the card
	/// the chip sits on, which holds the selection while the chip does not. It closes when the
	/// selection leaves that card, and on A as well as B: a pad user who opened it with a button
	/// will close it with one, and A must not fall through and choose the spell under it.
	/// </summary>
	public static void OpenFor(ElementChip chip, Control selected)
	{
		Close();
		if (chip == null || !IsInstanceValid(chip) || !chip.IsInsideTree())
			return;
		var popover = new ElementTagPopover
		{
			Name = "ElementTagPopover",
			Layer = PopoverLayer,
			ProcessMode = ProcessModeEnum.Always,
			anchor = chip,
			holder = selected,
		};
		chip.GetTree().Root.AddChild(popover);
		current = popover;
	}

	/// <summary>Opens the card for <paramref name="chip"/>, or closes it if it is already open.</summary>
	public static void Toggle(ElementChip chip)
	{
		if (chip == null || !IsInstanceValid(chip) || !chip.IsInsideTree())
			return;

		bool wasThisChip = IsOpen && current.anchor == chip;
		Close();
		if (wasThisChip)
			return;

		var popover = new ElementTagPopover
		{
			Name = "ElementTagPopover",
			Layer = PopoverLayer,
			ProcessMode = ProcessModeEnum.Always,
			anchor = chip,
		};
		chip.GetTree().Root.AddChild(popover);
		current = popover;
	}

	public static void Close()
	{
		if (!IsOpen)
		{
			current = null;
			return;
		}
		ElementTagPopover closing = current;
		current = null;
		closing.Detach();
		closing.QueueFree();
	}

	public override void _Ready()
	{
		var catcher = new Control { Name = "Catcher", MouseFilter = Control.MouseFilterEnum.Stop };
		catcher.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		catcher.GuiInput += OnCatcherInput;
		AddChild(catcher);

		card = BuildCard(anchor, anchor.Tag, anchor.CountAfter, anchor.Gain);
		float width = Mathf.Min(MaxWidth, ResponsiveLayout.ViewportSize(anchor).X - EdgeMargin * 2f);
		card.CustomMinimumSize = new Vector2(width, 0f);
		card.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(card);
		card.Resized += Place;
		Callable.From(Place).CallDeferred();

		// A pad user opened it with A on a focused chip: pushing the stick on moves the selection
		// and should take the card with it. A mouse or a finger focuses the chip too (FocusMode
		// All), so this holds for them as well, and costs nothing.
		watchFocus = anchor.HasFocus();
		if (watchFocus)
			anchor.FocusExited += OnAnchorGone;
		if (holder != null && IsInstanceValid(holder))
			holder.FocusExited += OnAnchorGone;
		anchor.TreeExiting += OnAnchorGone;
		anchor.VisibilityChanged += OnAnchorVisibility;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("pause")
			|| (holder != null && @event.IsActionPressed("ui_accept")))
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnCatcherInput(InputEvent @event)
	{
		// The mouse button only, and that covers a finger: Godot emulates a left click for the first
		// touch AND delivers the touch itself, and answering both would close and reopen.
		if (@event is InputEventMouseButton { Pressed: true })
		{
			GetViewport().SetInputAsHandled();
			Close();
		}
	}

	private void OnAnchorGone() => Close();

	private void OnAnchorVisibility()
	{
		if (anchor == null || !IsInstanceValid(anchor) || !anchor.IsVisibleInTree())
			Close();
	}

	// Once only: Close detaches straight away so a dying chip cannot call back into a closing card,
	// and _ExitTree runs it again a frame later.
	private void Detach()
	{
		if (detached)
			return;
		detached = true;
		if (holder != null && IsInstanceValid(holder))
			holder.FocusExited -= OnAnchorGone;
		if (anchor == null || !IsInstanceValid(anchor))
			return;
		if (watchFocus)
			anchor.FocusExited -= OnAnchorGone;
		anchor.TreeExiting -= OnAnchorGone;
		anchor.VisibilityChanged -= OnAnchorVisibility;
	}

	// Under the chip if it fits, over it if not, and never off the screen or under the phone's
	// camera hole. Measured in the canvas the chip is drawn in, which every menu layer shares.
	private void Place()
	{
		if (card == null || anchor == null || !IsInstanceValid(anchor) || !anchor.IsInsideTree())
			return;

		// Back to the height the text actually needs at this width. The first layout pass measures the
		// wrapped labels before the width has settled, at one word a line, and a Control never shrinks
		// on its own - without this the card kept that first, screen-tall height.
		if (!placing)
		{
			placing = true;
			card.ResetSize();
			placing = false;
		}

		Transform2D xform = anchor.GetGlobalTransformWithCanvas();
		var chipRect = new Rect2(xform.Origin, anchor.Size * xform.Scale);
		Vector2 screen = ResponsiveLayout.ViewportSize(anchor);
		var (left, top, right, bottom) = SafeArea.Insets(anchor.GetViewport());
		Vector2 size = card.Size;

		float y = chipRect.End.Y + Gap;
		if (y + size.Y > screen.Y - bottom - EdgeMargin)
			y = chipRect.Position.Y - Gap - size.Y;
		y = Mathf.Clamp(y, top + EdgeMargin, Mathf.Max(top + EdgeMargin, screen.Y - bottom - EdgeMargin - size.Y));

		float x = chipRect.GetCenter().X - size.X * 0.5f;
		x = Mathf.Clamp(x, left + EdgeMargin, Mathf.Max(left + EdgeMargin, screen.X - right - EdgeMargin - size.X));

		card.Position = new Vector2(Mathf.Round(x), Mathf.Round(y));
	}

	/// <summary>How many tags of <paramref name="element"/> the player in this run holds, or -1 outside a run.</summary>
	public static int HeldCount(Node anyNode, Element element)
	{
		if (anyNode?.GetTree()?.GetFirstNodeInGroup("player") is not Player player || !IsInstanceValid(player))
			return -1;
		Dictionary<Element, int> counts = player.GetElementInstanceCounts();
		return counts.TryGetValue(element, out int held) ? held : 0;
	}

	/// <summary>The tier a count earns: 0, 2, 4 or 6 (Player.GetElementTier's thresholds).</summary>
	public static int TierFor(int count) => count >= 6 ? 6 : count >= 4 ? 4 : count >= 2 ? 2 : 0;

	private static readonly int[] Tiers = { 2, 4, 6 };

	/// <summary>
	/// The card itself, for the popover and for a PC hover tooltip alike. <paramref name="countAfter"/>
	/// is what the count becomes if the player takes the card the chip is on, or -1.
	/// </summary>
	public static PanelContainer BuildCard(Node context, Element element, int countAfter, int gain = 0)
	{
		CodexRows.Palette ink = CodexRows.Stone;
		var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		var style = new StyleBoxFlat
		{
			BgColor = ink.Face,
			BorderColor = new Color(0.847f, 0.690f, 0.290f), // gold lit: the rim of a held-up note
			ShadowColor = new Color(0f, 0f, 0f, 0.45f),
			ShadowSize = 8,
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(6);
		style.ContentMarginLeft = 18;
		style.ContentMarginRight = 18;
		style.ContentMarginTop = 14;
		style.ContentMarginBottom = 16;
		panel.AddThemeStyleboxOverride("panel", style);

		var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", 10);
		panel.AddChild(column);

		int held = HeldCount(context, element);
		int tier = held >= 0 ? TierFor(held) : 0;
		if (countAfter < 0 && gain > 0 && held >= 0)
			countAfter = held + gain;

		// The element's name on its own colour, so the card is visibly about the chip that opened it,
		// then the count on the right.
		var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		head.AddThemeConstantOverride("separation", 12);
		Color fill = CodexRows.ChipFill(ElementColors.GetColor(element));
		var swatch = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		var swatchStyle = new StyleBoxFlat { BgColor = fill, BorderColor = fill.Darkened(0.35f) };
		swatchStyle.SetBorderWidthAll(1);
		swatchStyle.SetCornerRadiusAll(4);
		swatchStyle.ContentMarginLeft = 12;
		swatchStyle.ContentMarginRight = 12;
		swatchStyle.ContentMarginTop = 2;
		swatchStyle.ContentMarginBottom = 2;
		swatch.AddThemeStyleboxOverride("panel", swatchStyle);
		Label name = CodexRows.MakeText(element.ToString(), ResponsiveLayout.TextRole.Body, StatIcons.ReadableOn(fill));
		name.AutowrapMode = TextServer.AutowrapMode.Off;
		swatch.AddChild(name);
		head.AddChild(swatch);

		string standing = held < 0
			? "Element tags"
			: tier > 0 ? $"You hold {held} · tier {tier}" : $"You hold {held}";
		Label count = CodexRows.MakeText(standing, ResponsiveLayout.TextRole.Label, ink.InkSoft);
		count.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		count.HorizontalAlignment = HorizontalAlignment.Right;
		count.AutowrapMode = TextServer.AutowrapMode.Off;
		head.AddChild(count);
		column.AddChild(head);

		// On a choice, the number the decision turns on.
		if (countAfter >= 0 && held >= 0 && countAfter != held)
		{
			int afterTier = TierFor(countAfter);
			string change = afterTier > tier
				? $"Taking this: {held} → {countAfter}, reaching tier {afterTier}"
				: $"Taking this: {held} → {countAfter}";
			column.AddChild(CodexRows.MakeText(change, ResponsiveLayout.TextRole.Label,
				afterTier > tier ? ink.Done : ink.Reward));
		}

		column.AddChild(new ColorRect
		{
			Color = ink.Rule,
			CustomMinimumSize = new Vector2(0, 2),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});

		// The three tiers, each with what it does. A reached tier is ticked and bright; the next one
		// says how far off it is; the rest are quiet.
		int reference = held >= 0 ? held : -1;
		bool nextMarked = false;
		foreach (int threshold in Tiers)
		{
			bool reached = reference >= threshold;
			bool isNext = !reached && reference >= 0 && !nextMarked;
			if (isNext)
				nextMarked = true;

			var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			line.AddThemeConstantOverride("separation", 10);
			line.AddChild(Glyph(reached ? "done" : "todo"));

			var words = new VBoxContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			words.AddThemeConstantOverride("separation", 2);
			// Tiers are named by their thresholds everywhere in the game ("tier 6"), so the title says
			// both: the name and what the number counts.
			string title = $"Tier {threshold}  ·  {threshold} tags";
			if (isNext)
			{
				int more = threshold - reference;
				title += more == 1 ? "  ·  1 more" : $"  ·  {more} more";
			}
			words.AddChild(CodexRows.MakeText(title, ResponsiveLayout.TextRole.Label,
				reached ? ink.Heading : isNext ? ink.Flag : ink.InkMeta));
			words.AddChild(CodexRows.MakeText(ElementPassiveDescriptions.GetEffectText(element, threshold),
				ResponsiveLayout.TextRole.Label, reached || isNext || reference < 0 ? ink.Ink : ink.InkSoft));
			line.AddChild(words);
			column.AddChild(line);
		}

		// The two other things a tag count buys, which no screen used to connect to the chip.
		column.AddChild(CodexRows.MakeText(
			$"A spell that is only {element} (attuned) also deals " +
			$"+{Pct(Player.AttunementTier2Multiplier)}% / +{Pct(Player.AttunementTier4Multiplier)}% / +{Pct(Player.AttunementTier6Multiplier)}% damage at these tiers.",
			ResponsiveLayout.TextRole.Label, ink.InkMeta));
		column.AddChild(CodexRows.MakeText(
			$"An ascension that wants {element} needs {Player.AscensionElementRequirement} {element} tags.",
			ResponsiveLayout.TextRole.Label, ink.InkMeta));

		return panel;
	}

	private static int Pct(float multiplier) => Mathf.RoundToInt((multiplier - 1f) * 100f);

	private static Control Glyph(string key)
	{
		return new TextureRect
		{
			Texture = StatIcons.Load(key),
			CustomMinimumSize = new Vector2(24, 24),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
	}

	public override void _ExitTree()
	{
		Detach();
		if (current == this)
			current = null;
	}
}
