using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

/// <summary>
/// The in-run loadout strip: the held spells as small icons in slot order, each with its level, and
/// one chip per element the player holds a tag of, in that element's colour with its progress
/// ("3/4"). Informational only - it takes no input, and the pause screen is still where a spell or
/// an element is explained.
/// </summary>
/// <remarks>
/// The user's request: "we should show the spells and tags on the game screen while playing, but
/// small. the tags can just be colors with the 2/3 symbol, and the spells can just be the icons. we
/// don't need to show the boons." The old element badges ("Fire 2/4" at 102x28) and the 48-pixel
/// spell row had been taken off the playfield as too heavy for something the player does not act
/// on mid-fight; this puts back the glance - what do I hold, how close is the next tier - at about
/// a third of the area, and leaves the reading to the pause screen.
///
/// WHERE IT SITS. Both canvases put it on the left under the health and XP bars, so the player's
/// own state is one block in one corner, but the two place it by different neighbours:
/// <list type="bullet">
/// <item>PC (landscape): a column the width of the spell row, starting under the boss bar's slot
/// (44-78, full width, only during the fight), so the bar never lands on it. The run's centred
/// lines - the side event objective, the stage intro, the onboarding tips - are centred on a
/// 1280-wide canvas and never reach x 260.</item>
/// <item>Phone (portrait): one row across the canvas, standing on the FLOOR of the touch pad's HUD
/// band (TouchControls.DefaultHudBandHeight). Every top-of-screen line above it - the boss bar,
/// the objective, the onboarding tip - spans the canvas width on a 720-wide screen, so the only
/// slot clear of all of them is the bottom of the band; and below the band is the stick, where
/// a thumb can land anywhere. SafeArea moves the band and the strip down together, so the strip
/// stays inside it. If the chips ever wrap to a second line the row grows UP, not into the pad.</item>
/// </list>
///
/// WHY IT CAN NEVER EAT A TAP. Every node here is MouseFilter Ignore. Below the band the pad's
/// layer (5) is above this one anyway, but inside the band nothing stands between a STOP control
/// here and the touch - and a strip that swallowed taps would be a dead patch of screen.
///
/// WHEN IT REDRAWS. On <see cref="Player.LoadoutChanged"/> only, coalesced to one rebuild per frame
/// (a Penknife cut raises it twice). Nothing here runs per frame and nothing is allocated per frame;
/// the slot and chip nodes are built once and re-filled.
/// </remarks>
public partial class LoadoutHud : BoxContainer
{
	/// <summary>The player whose loadout is shown. Set before adding to the tree.</summary>
	public Player? Holder { get; set; }

	// The spell icons are 32-pixel art stored at x2 (64x64 PNGs). At 32 canvas units each art pixel
	// is one unit and Nearest filtering keeps every one; at the 24-28 first sketched, Nearest drops
	// every fourth or eighth row and column, which on pixel art reads as damage, not as small.
	private const float IconSize = 32f;
	private const float SlotRim = 1f;
	private const float SlotSize = IconSize + 2f * SlotRim;
	private const int SlotGap = 2;
	private const int ChipGap = 3;
	private const int MaxSlots = Player.MaxSpellSlots + Player.BlankFolioExtraSlots;

	// Landscape. Left-aligned with the health bar (Node2DGame.ConfigurePlayerHealthHud, x 7); top
	// under the boss bar's slot (Node2DGame.ConfigureBossHud: 44 + 34), which overlaps the XP bar.
	private const float EdgeLeft = 7f;
	private const float LandscapeTop = 84f;
	private const float LandscapeWidth = MaxSlots * SlotSize + (MaxSlots - 1) * SlotGap;
	// Portrait. A few units of air above the band's floor so the row does not sit exactly on the
	// line where a thumb starts to steer.
	private const float BandFloorGap = 4f;

	// Inks and rims, from the palette in .ai/art-direction.md. The slot backing is floor stone deep at
	// 0.85: the icons are drawn for the Bonelight dark ground, and on the Sketchbook's white paper a
	// thinner backing (0.62 was tried) left the steel rim at 1.3:1 and every milestone rim under 3:1.
	// At 0.85 the weakest, the plain held rim, is 3.0:1 on white paper (tools/art/ui_contrast.py).
	private static readonly Color SlotBacking = new(0.043f, 0.059f, 0.094f, 0.85f);
	private static readonly Color HeldRim = new(0.416f, 0.494f, 0.596f, 1f);          // steel base #6A7E98
	private static readonly Color EmptyBacking = new(0.043f, 0.059f, 0.094f, 0.28f);
	private static readonly Color EmptyRim = new(0.604f, 0.643f, 0.722f, 0.30f);      // bone base #9AA4B8, faint
	// The three milestones keep the hue their level-up family is drawn in (.ai/level-up-screen.md 2c):
	// the mutation's gilt, the ascension's arcane glass, the apotheosis's fire.
	private static readonly Color MutatedRim = new(0.847f, 0.690f, 0.290f, 1f);       // gold lit #D8B04A
	private static readonly Color AscendedRim = new(0.416f, 0.722f, 0.910f, 1f);      // arcane cyan lit #6AB8E8
	private static readonly Color ApotheosisRim = new(0.910f, 0.478f, 0.102f, 1f);    // fire mid #E87A1A
	// The level numeral: the health readout's ink and outline, so the HUD has one way of writing a
	// number over art. On its black outline it is 17:1 whatever the icon is under it.
	private static readonly Color NumeralInk = new(1f, 0.94f, 0.86f);
	private static readonly Color NumeralOutline = new(0f, 0f, 0f, 0.9f);
	private const int NumeralOutlineSize = 4;

	private sealed class SlotView
	{
		public Panel Frame = null!;
		public StyleBoxFlat Style = null!;
		public TextureRect Icon = null!;
		public Label Level = null!;
	}

	private sealed class ChipView
	{
		public Element Element;
		public PanelContainer Chip = null!;
		public Label Text = null!;
		public Control Star = null!;
	}

	private readonly SlotView[] slots = new SlotView[MaxSlots];
	private readonly Dictionary<Element, ChipView> chips = new();
	private readonly List<KeyValuePair<Element, int>> order = new();
	private HFlowContainer tagFlow = null!;
	private bool rebuildQueued;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		bool narrow = ResponsiveLayout.IsNarrow(this);
		Place(narrow);

		var spellRow = new HBoxContainer
		{
			Name = "SpellRow",
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		spellRow.AddThemeConstantOverride("separation", SlotGap);
		AddChild(spellRow);
		for (int i = 0; i < MaxSlots; i++)
			slots[i] = BuildSlot(spellRow, i);

		tagFlow = new HFlowContainer
		{
			Name = "TagRow",
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		tagFlow.AddThemeConstantOverride("h_separation", ChipGap);
		tagFlow.AddThemeConstantOverride("v_separation", ChipGap);
		AddChild(tagFlow);
		foreach (Element element in System.Enum.GetValues<Element>())
			chips[element] = BuildChip(element);

		if (Holder != null)
			Holder.Connect(Player.SignalName.LoadoutChanged, new Callable(this, nameof(OnLoadoutChanged)));
		Rebuild();
	}

	public override void _ExitTree()
	{
		var callable = new Callable(this, nameof(OnLoadoutChanged));
		if (Holder != null && IsInstanceValid(Holder) && Holder.IsConnected(Player.SignalName.LoadoutChanged, callable))
			Holder.Disconnect(Player.SignalName.LoadoutChanged, callable);
	}

	// Anchors and offsets as built; SafeArea.Apply on the UIOverlay (Node2DGame) moves them in from
	// a phone's corners afterwards, exactly as it does the health bar, so the strip and the bar stay
	// aligned.
	private void Place(bool narrow)
	{
		AnchorLeft = 0f;
		AnchorTop = 0f;
		AnchorBottom = 0f;
		if (narrow)
		{
			// One row across the canvas: the spells, then the chips, wrapping (upwards) if they must.
			Vertical = false;
			AnchorRight = 1f;
			OffsetLeft = EdgeLeft;
			OffsetRight = -EdgeLeft;
			OffsetBottom = TouchControls.DefaultHudBandHeight - BandFloorGap;
			OffsetTop = OffsetBottom - SlotSize;
			GrowVertical = GrowDirection.Begin;
			AddThemeConstantOverride("separation", 8);
		}
		else
		{
			// A column the width of a full book: spells, then chips under them.
			Vertical = true;
			AnchorRight = 0f;
			OffsetLeft = EdgeLeft;
			OffsetRight = EdgeLeft + LandscapeWidth;
			OffsetTop = LandscapeTop;
			OffsetBottom = LandscapeTop + SlotSize;
			AddThemeConstantOverride("separation", 4);
		}
	}

	private SlotView BuildSlot(Container row, int index)
	{
		var style = new StyleBoxFlat { BgColor = EmptyBacking, BorderColor = EmptyRim };
		style.SetBorderWidthAll((int)SlotRim);
		style.SetCornerRadiusAll(3);
		var frame = new Panel
		{
			Name = $"SpellSlot{index}",
			CustomMinimumSize = new Vector2(SlotSize, SlotSize),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		frame.AddThemeStyleboxOverride("panel", style);
		row.AddChild(frame);

		var icon = new TextureRect
		{
			Name = "Icon",
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			OffsetLeft = SlotRim,
			OffsetTop = SlotRim,
			OffsetRight = SlotRim + IconSize,
			OffsetBottom = SlotRim + IconSize,
		};
		frame.AddChild(icon);

		// Bottom right, pushed down by the font's descent so the numeral sits in the corner of the
		// icon rather than floating a third of the way up it.
		var level = new Label
		{
			Name = "Level",
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		level.SetAnchorsPreset(LayoutPreset.FullRect);
		level.OffsetRight = -1f;
		level.OffsetBottom = 5f;
		frame.AddChild(level);
		ResponsiveLayout.SetFont(level, ResponsiveLayout.TextRole.Micro);
		level.AddThemeColorOverride("font_color", NumeralInk);
		level.AddThemeColorOverride("font_outline_color", NumeralOutline);
		level.AddThemeConstantOverride("outline_size", NumeralOutlineSize);

		return new SlotView { Frame = frame, Style = style, Icon = icon, Level = level };
	}

	private ChipView BuildChip(Element element)
	{
		// The same face every other element chip in the game wears (CodexRows.BuildChip): the
		// element's colour nudged to clear 4.5:1 against its ink, the ink picked by WCAG contrast,
		// and a rim of its own colour darkened so the pale ones keep an edge. Opaque, so the ratio
		// measured in tools/art/ui_contrast.py is the ratio on screen whatever the ground is.
		Color fill = CodexRows.ChipFill(ElementColors.GetColor(element));
		Color ink = StatIcons.ReadableOn(fill);
		var style = new StyleBoxFlat { BgColor = fill, BorderColor = fill.Darkened(0.35f) };
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(3);
		style.ContentMarginLeft = 5f;
		style.ContentMarginRight = 5f;
		style.ContentMarginTop = 0f;
		style.ContentMarginBottom = 0f;

		var chip = new PanelContainer
		{
			Name = $"LoadoutChip_{element}",
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false,
		};
		chip.AddThemeStyleboxOverride("panel", style);
		tagFlow.AddChild(chip);

		var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 2);
		chip.AddChild(row);

		var text = new Label
		{
			Name = "Count",
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddChild(text);
		ResponsiveLayout.SetFont(text, ResponsiveLayout.TextRole.Micro);
		text.AddThemeColorOverride("font_color", ink);

		// The top tier's star, in the chip's ink. Drawn rather than typed: the UI face (the engine's
		// Open Sans) has no star glyph, and a fallback font is not something every phone promises.
		// It draws only when the chip is laid out or shown, never per frame.
		var star = new Control
		{
			Name = "Max",
			CustomMinimumSize = new Vector2(9f, 9f),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false,
		};
		var points = new Vector2[10];
		star.Draw += () =>
		{
			Vector2 centre = star.Size / 2f;
			float outer = Mathf.Min(star.Size.X, star.Size.Y) / 2f;
			for (int i = 0; i < points.Length; i++)
			{
				float angle = -Mathf.Pi / 2f + i * Mathf.Pi / 5f;
				points[i] = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (i % 2 == 0 ? outer : outer * 0.45f);
			}
			star.DrawColoredPolygon(points, ink);
		};
		row.AddChild(star);

		return new ChipView { Element = element, Chip = chip, Text = text, Star = star };
	}

	private void OnLoadoutChanged()
	{
		if (rebuildQueued)
			return;
		rebuildQueued = true;
		// Deferred calls flush while the tree is paused, so a pick made in the level-up menu is on
		// the strip the moment the menu closes.
		CallDeferred(MethodName.Rebuild);
	}

	private void Rebuild()
	{
		rebuildQueued = false;
		if (Holder == null || !IsInstanceValid(Holder))
			return;

		IReadOnlyList<SpellData> spells = Holder.GetEquippedSpells();
		int open = Mathf.Clamp(Holder.SpellSlots, 0, MaxSlots);
		for (int i = 0; i < MaxSlots; i++)
		{
			SpellData? spell = i < spells.Count ? spells[i] : null;
			FillSlot(slots[i], spell, visible: spell != null || i < open);
		}

		// Most tags first, the way the old badges and every element list in the menus order them.
		order.Clear();
		foreach (KeyValuePair<Element, int> pair in Holder.GetElementInstanceCounts())
		{
			if (pair.Value > 0)
				order.Add(pair);
		}
		order.Sort((a, b) => a.Value != b.Value
			? b.Value.CompareTo(a.Value)
			: string.CompareOrdinal(a.Key.ToString(), b.Key.ToString()));

		foreach (ChipView view in chips.Values)
			view.Chip.Visible = false;
		for (int i = 0; i < order.Count; i++)
		{
			ChipView view = chips[order[i].Key];
			FillChip(view, order[i].Value);
			tagFlow.MoveChild(view.Chip, i);
		}
	}

	private static void FillSlot(SlotView view, SpellData? spell, bool visible)
	{
		view.Frame.Visible = visible;
		if (spell == null)
		{
			view.Style.BgColor = EmptyBacking;
			view.Style.BorderColor = EmptyRim;
			view.Icon.Texture = null;
			view.Level.Text = string.Empty;
			return;
		}

		Color rim = MilestoneRim(spell);
		view.Style.BorderColor = rim;
		// An evolved spell's backing takes a breath of its rim's hue too, showing through the gaps in
		// the icon - a one-unit rim alone is easy to miss at this size.
		view.Style.BgColor = rim == HeldRim ? SlotBacking : SlotBacking.Lerp(new Color(rim, SlotBacking.A), 0.18f);
		// An evolved spell is shown as it is now: its newest milestone's own picture (EvolutionIcons),
		// at the same 32 canvas units, one per art pixel.
		view.Icon.Texture = EvolutionIcons.Current(spell);
		view.Level.Text = spell.CurrentLevel.ToString();
	}

	private static Color MilestoneRim(SpellData spell)
	{
		if (spell.IsApotheosis)
			return ApotheosisRim;
		if (spell.SelectedLevel8Evolution != null)
			return AscendedRim;
		if (spell.SelectedLevel4Evolution != null)
			return MutatedRim;
		return HeldRim;
	}

	private static void FillChip(ChipView view, int count)
	{
		// The tier thresholds are 2, 4 and 6 (Player.GetElementTier). Below 6 the chip says what
		// every other chip in the game says (ElementPassiveDescriptions.GetProgressLabel: "1/2",
		// "3/4", "4/4"), so the strip and the level-up card never give two numbers for one state.
		// At 6 there is no next threshold: the count and a star.
		bool maxed = count >= 6;
		string text = maxed ? count.ToString() : ElementPassiveDescriptions.GetProgressLabel(count);
		if (view.Text.Text != text)
			view.Text.Text = text;
		view.Star.Visible = maxed;
		view.Chip.Visible = true;
	}
}
