using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

/// <summary>
/// The treasure chest: three relics (four with the Reliquary Key), pick one.
/// </summary>
/// <remarks>
/// WHAT THE SCREEN IS FOR. A chest asks two questions at once - "which of these is best for me" and
/// "does this one finish something" - and the old screen answered only the first. Each card was a
/// tall dark column holding an 80px icon, a sentence of effect and a row of 44px set badges whose
/// meaning was one press away, over a band of empty card. It is laid out like the level-up screen
/// now, because it is the same kind of moment (.ai/level-up-screen.md):
///
///   * STONE OUTSIDE, MANUSCRIPT INSIDE. The panel is stone, each relic is a torn parchment page in
///     the level-up cards' own frame, and the words on it are the level-up cards' inks.
///   * THE WHOLE PAGE IS THE BUTTON, like a level-up card - one big target for a thumb, one ring
///     for a stick. The Claim plate at its foot is drawn, not a second button: it says what
///     pressing the page does without splitting the page into two things to aim at.
///   * THE SET IS ON THE CARD, NOT BEHIND A PRESS. For every Full Set Enchantment the relic
///     belongs to: the set's icon and name, every piece as an icon (owned, this one, missing),
///     the count before and after claiming, and what each tier pays in numbers - the partial and
///     the full set's Description, which is the mechanic in one sentence for exactly this reason.
///
/// Two shapes. On a PC the pages stand side by side and share the panel's whole height, with no
/// scrolling in any chest measured. On a phone they stack, with the icon, name and Claim plate on
/// one row so the height goes to the text; a scroll container remains as the fallback for a
/// four-relic chest, the one case measured to outgrow a phone (.ai/level-up-screen.md, 4b).
/// </remarks>
public partial class ChestItemSelectionMenu : CanvasLayer
{
	[Signal] public delegate void ItemSelectedEventHandler(string itemId);
	// The Scribe's Penknife is claimed by naming the spell it cuts, so it has its own signal and
	// never comes through ItemSelected: a knife claimed without a spell would be a relic spent on
	// nothing (Player.AddChestItem refuses it for that reason).
	[Signal] public delegate void SpellCutChosenEventHandler(string spellId);

	// What this chest is offering: what Back from the knife's picker re-deals - the same cards,
	// never a fresh roll.
	private List<string> offeredItemIds = new();
	private IReadOnlyCollection<string> offeredOwnedItems = Array.Empty<string>();

	// The book as it stands when the chest opened, for the knife's picker (SetSpellBook).
	private IReadOnlyList<SpellData> heldSpells = Array.Empty<SpellData>();
	private string protectedSpellId = string.Empty;
	private Label heading;

	// BoxContainer, not HBoxContainer: Godot refuses to change orientation on the H/V subclasses
	// ("Can.t change orientation of HBoxContainer"), and this row flips to vertical on a phone.
	private BoxContainer cardsRow = null!;
	private bool narrow;
	// The Reliquary Key's chest: four relics rather than three.
	private bool fourRelics;
	// Five abreast on a PC (John Dee holding the Relic Key, .ai/passives-and-items.md section 8):
	// pages about 234 wide. Measured before this existed, the Title-sized names broke mid-word
	// ("Hearts/tone", "Buckle/r Thorn/s") and the tallest page needed 856 of 595. So the name drops
	// to the Label size in its own Cinzel face (at Body, "Heartstone" still broke), the art to 48 and
	// the set's to 40, the page margins to 12, and a set box shows its full tier only when this claim
	// completes it - the pieces row and the count still say how close it is.
	private bool fiveAbreast;

	// Wider than this and three pages become banners: a wide monitor gets keep_height's extra
	// width (DisplayProfile), and a 600px-wide relic card is harder to read, not easier.
	private const float MaxPanelWidth = 1400f;
	private const float PanelMargin = 10f;
	// Between wrapped lines of one paragraph (Label line_spacing, RichTextLabel line_separation).
	private const int WrappedLineSpacing = -4;
	// The longest stat that the phone's two-column effect list will hold unwrapped, at the Body role
	// in half of a 600-wide page.
	private const int ShortStatLength = 18;

	// The page frame and its lit state - the level-up cards' own (LevelUpMenu.BuildCardStyleBox).
	// Built per menu, never cached in a static: see the gchandle note in BonelightSkin.
	private const string PageTexturePath = "res://assets/bonelight/ui/ui-spell-page.png";
	private const string PageLitTexturePath = "res://assets/bonelight/ui/ui-spell-page-lit.png";
	// The page's corner blocks are 24 drawn (CardFamilyArt.PatchMargin); sliced at 20 they tiled the
	// gold leaf's arms and the fibre flecks along the bands. The content margin stays 20, so no page
	// on this screen changes size.
	private const int PagePatchMargin = CardFamilyArt.PatchMargin;
	private const int PageContentMargin = 20;
	private StyleBoxTexture pageResting;
	private StyleBoxTexture pageLit;
	// The Penknife's picker lists spells the player HOLDS, and a held spell is the rubricated page -
	// the torn page with a red rule round it - wherever it is the choice (CardFamilyArt,
	// .ai/level-up-screen.md "The card families"); an unmarked page is a spell not yet in the book.
	private StyleBoxTexture heldResting;
	private StyleBoxTexture heldLit;

	// --- Inks -------------------------------------------------------------------------------
	//
	// Dark marks on the light page (flesh hi, #F2DCC4). Ink and InkSoft are LevelUpMenu's,
	// copied because they are private there; the rest are palette tones (tools/art/bonelight.py)
	// picked for the jobs this screen adds. EVERY ONE is mirrored in tools/art/ui_contrast.py,
	// which measures it against the page it lands on and the set box - change one, change both.
	private static readonly Color Ink = new Color(0.235f, 0.141f, 0.212f);      // flesh deep  #3C2436
	private static readonly Color InkSoft = new Color(0.204f, 0.173f, 0.298f);  // violet base #342C4C
	// Rarity, darkest to brightest in the usual order of a loot table: grey, green, blue, violet.
	private static readonly Color InkCommon = new Color(0.275f, 0.329f, 0.298f);   // linen base   #46544C
	private static readonly Color InkUncommon = new Color(0.145f, 0.329f, 0.086f); // verdant shade #255416
	private static readonly Color InkRare = new Color(0.102f, 0.243f, 0.541f);     // azure shade  #1A3E8A
	private static readonly Color InkRelic = new Color(0.306f, 0.267f, 0.439f);    // violet lit   #4E4470
	// "This relic finishes something": the one piece of news worth catching at a glance.
	private static readonly Color InkGain = InkUncommon;

	// The recessed box a set sits in: a step down from the page toward flesh lit, opaque so its
	// contrast can be measured rather than depending on what the page has drawn under it.
	private static readonly Color SetBoxGround = new Color(0.910f, 0.788f, 0.675f); // #E8C9AC
	// gold base #8A6A1E, the rule the lit page and the heading band are edged in.
	private static readonly Color SetBoxRule = new Color(0.541f, 0.416f, 0.118f);

	// LevelUpMenu's heading: bone hi on stone deep, 17.3:1 (ui_contrast.py, "heading band").
	private static readonly Color HeadingInk = new Color(1f, 0.95f, 0.86f);
	private static readonly Color BandColor = new Color(0.043f, 0.059f, 0.094f, 1f);

	public override void _Ready()
	{
		Layer = 100;
		ProcessMode = ProcessModeEnum.WhenPaused;
		narrow = ResponsiveLayout.IsNarrow(this);
		BuildUiStructure();
		// Clear of the phone's rounded corners and camera hole; nothing on desktop (SafeArea). The
		// panel is anchored to both edges on each axis it fills, so SafeArea shrinks it to fit.
		SafeArea.Apply(this);
		MenuNavigator.Attach(this);
	}

	private void BuildUiStructure()
	{
		// A dim, not a blackout - the same treatment the level-up menu got. Opening a chest
		// interrupts the run; it should not look like leaving it.
		var dim = new ColorRect
		{
			Name = "BackgroundDim",
			Color = new Color(0.02f, 0.025f, 0.04f, 0.62f),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(dim);

		// The panel used to be a fixed 940 wide by the viewport less 60 tall, centred - a third of a
		// 1280 screen went unused at the sides and the cards overran its foot. It fills the screen
		// now, less a margin, and only a very wide monitor caps it.
		Vector2 viewport = ResponsiveLayout.ViewportSize(this);
		var panel = new PanelContainer { Name = "MenuPanel" };
		panel.AnchorTop = 0f;
		panel.AnchorBottom = 1f;
		panel.OffsetTop = PanelMargin;
		panel.OffsetBottom = -PanelMargin;
		if (viewport.X - PanelMargin * 2f > MaxPanelWidth)
		{
			panel.AnchorLeft = 0.5f;
			panel.AnchorRight = 0.5f;
			panel.OffsetLeft = -MaxPanelWidth * 0.5f;
			panel.OffsetRight = MaxPanelWidth * 0.5f;
		}
		else
		{
			panel.AnchorLeft = 0f;
			panel.AnchorRight = 1f;
			panel.OffsetLeft = PanelMargin;
			panel.OffsetRight = -PanelMargin;
		}
		BonelightSkin.ApplyPanel(panel);
		AddChild(panel);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", narrow ? 10 : 8);
		panel.AddChild(column);

		// No subtitle. "Claim a relic, and advance a Full Set Enchantment" was the one line on the
		// old screen that explained the set badges; the set boxes now explain themselves, and on a
		// 720-tall screen the line cost a row of the text it was standing in for.
		heading = BuildHeading();
		column.AddChild(heading);

		cardsRow = new BoxContainer
		{
			Name = "Cards",
			Vertical = narrow,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		cardsRow.AddThemeConstantOverride("separation", 10);

		// Both shapes sit in a scroll container that only scrolls when it has to. ExpandFill on the
		// row makes the container stretch it to its own height when the cards fit, which on a PC is
		// every chest measured (including The Crown, the one relic in two sets); a phone can
		// outgrow it with the Crown or a four-relic chest. Before this the panel itself grew past the
		// foot of the screen instead, and the Claim buttons went with it.
		var scroll = new ScrollContainer
		{
			Name = "CardsScroll",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			FollowFocus = true
		};
		scroll.AddChild(cardsRow);
		column.AddChild(scroll);
	}

	// LevelUpMenu.StyleHeading's band: the darkest stone, solid, with a gold rule at its foot, so
	// the heading never sits on the stone's own texture.
	private Label BuildHeading()
	{
		var title = new Label
		{
			Text = "Treasure Chest",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			CustomMinimumSize = new Vector2(0f, narrow ? ResponsiveLayout.MinTouchTarget + 4f : 0f)
		};
		ResponsiveLayout.SetFont(title, ResponsiveLayout.TextRole.Display);
		title.AddThemeColorOverride("font_color", HeadingInk);
		var band = new StyleBoxFlat { BgColor = BandColor, BorderColor = SetBoxRule };
		band.BorderWidthBottom = 2;
		band.SetCornerRadiusAll(2);
		band.ContentMarginLeft = 12;
		band.ContentMarginRight = 12;
		band.ContentMarginTop = 2;
		band.ContentMarginBottom = 4;
		title.AddThemeStyleboxOverride("normal", band);
		return title;
	}

	/// <summary>
	/// The spells the Scribe's Penknife would choose from, and the one it will not cut. Set before
	/// SetOptions by whoever opens the chest; a chest that never offers the knife never reads it.
	/// </summary>
	public void SetSpellBook(IReadOnlyList<SpellData> spells, string signatureSpellId)
	{
		heldSpells = spells?.Where(s => s != null).ToList() ?? new List<SpellData>();
		protectedSpellId = signatureSpellId ?? string.Empty;
	}

	public void SetOptions(List<string> itemIds, IReadOnlyCollection<string> ownedItems)
	{
		if (cardsRow == null)
			return;

		ClearCards();
		if (heading != null)
			heading.Text = "Treasure Chest";

		ownedItems ??= Array.Empty<string>();
		offeredItemIds = itemIds?.ToList() ?? new List<string>();
		offeredOwnedItems = ownedItems;
		// Four relics (the Reliquary Key) side by side are a quarter of the panel each, so they
		// take the phone's tighter header rather than wrapping every line twice.
		fourRelics = itemIds.Count >= 4;
		fiveAbreast = !narrow && itemIds.Count >= 5;
		bool compact = narrow || fourRelics;
		foreach (string itemId in itemIds)
			cardsRow.AddChild(BuildOptionCard(itemId, ownedItems, compact));

		// The previous chest's cards were just freed, so the pad's selection went with them.
		MenuNavigator.Attach(this);
	}

	private void ClearCards()
	{
		foreach (Node child in cardsRow.GetChildren())
		{
			cardsRow.RemoveChild(child);
			child.QueueFree();
		}
	}

	private void EnsurePageStyles()
	{
		pageResting ??= BuildPageStyle(GD.Load<Texture2D>(PageTexturePath));
		pageLit ??= BuildPageStyle(GD.Load<Texture2D>(PageLitTexturePath));
		heldResting ??= CardFamilyArt.Frame(CardFamily.Upgrade, false, 18);
		heldLit ??= CardFamilyArt.Frame(CardFamily.Upgrade, true, 18);
	}

	// A torn edge has to TILE, not stretch, or the deckle smears into a taper at any size but the
	// one it was drawn at (.ai/level-up-screen.md, section 2).
	private static StyleBoxTexture BuildPageStyle(Texture2D texture)
	{
		var style = new StyleBoxTexture
		{
			Texture = texture,
			AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile,
			AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Tile,
		};
		style.SetTextureMarginAll(PagePatchMargin);
		style.SetContentMarginAll(PageContentMargin);
		return style;
	}

	// The page is a PanelContainer holding the Button and the content as siblings - the level-up
	// screen's narrow card. A Button does not lay out its children, so a Button parent could not
	// grow to fit the text; the PanelContainer fits both to the content's height and the Button is
	// stretched under it as the click target and the frame.
	private Control BuildOptionCard(string itemId, IReadOnlyCollection<string> ownedItems, bool compact)
	{
		EnsurePageStyles();

		var root = new PanelContainer
		{
			Name = "Relic_" + itemId,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		root.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
		// Side by side, a relic in two sets (The Crown) gets a wider page rather than a taller one:
		// height is the scarce axis on a landscape screen, and the row takes its height from its
		// tallest page, so one tall page would push every Claim plate down with it.
		int setCount = ChestItemCatalog.GetAssociatedSets(itemId).Count;
		if (!narrow)
			root.SizeFlagsStretchRatio = 1f + 0.5f * Math.Max(0, setCount - 1);

		var card = new Button
		{
			Name = "ClaimCard",
			Text = string.Empty,
			TooltipText = "Claim " + ChestItemCatalog.GetDisplayName(itemId),
			// Paused tree: the layer is WhenPaused, and a control that inherits Pausable would
			// navigate perfectly and refuse to activate (.ai/controller-and-focus.md, section 6).
			ProcessMode = ProcessModeEnum.Always
		};
		// Both states have the same parchment face; hover and focus brighten the rule and the leaf,
		// never the ground the words sit on.
		card.AddThemeStyleboxOverride("normal", pageResting);
		card.AddThemeStyleboxOverride("hover", pageLit);
		card.AddThemeStyleboxOverride("pressed", pageLit);
		card.AddThemeStyleboxOverride("focus", pageLit);
		string capturedId = itemId;
		// The knife's page opens its picker instead of claiming: what it does depends on which spell,
		// so the claim is the second press, on a spell, with Back still available until then.
		if (ChestItemCatalog.ScribesPenknife.Equals(itemId, StringComparison.OrdinalIgnoreCase))
			card.Pressed += ShowCutPicker;
		else
			card.Pressed += () => EmitSignal(SignalName.ItemSelected, capturedId);
		root.AddChild(card);

		var margin = new MarginContainer();
		int sideMargin = fiveAbreast ? 12 : 18;
		margin.AddThemeConstantOverride("margin_left", sideMargin);
		margin.AddThemeConstantOverride("margin_right", sideMargin);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		root.AddChild(margin);

		var body = new VBoxContainer();
		body.AddThemeConstantOverride("separation", 8);
		margin.AddChild(body);

		// Art, then name and rarity beside it; the effect runs the full width of the page under them,
		// where a sentence wraps least. (Setting the effect beside the art instead was tried: in a
		// page a third of the screen wide it wrapped to two or three words a line and came out
		// taller.) On a phone the Claim plate rides on the right of the header row, because there
		// the height is what is short.
		body.AddChild(BuildHeader(itemId, fiveAbreast ? 48f : compact && !narrow ? 64f : 96f));
		body.AddChild(BuildEffectList(itemId));

		bool alreadyOwned = ownedItems.Contains(itemId, StringComparer.OrdinalIgnoreCase);
		// A relic in two sets (The Crown) shows each set's full tier only, unless the claim switches
		// a partial on: two boxes with every tier in them fit neither a 720-tall screen nor a phone
		// beside two other pages, and the far tier of two far-off sets is the line that matters
		// least on the card.
		// A four-relic chest (the Reliquary Key) is brief too: four pages abreast are under 300 wide,
		// where a four-piece set with every tier in it is taller than the screen, and four stacked
		// on a phone are taller than the phone.
		bool briefSets = setCount > 1 || fourRelics;
		foreach (ChestSetDefinition set in ChestItemCatalog.GetAssociatedSets(itemId))
			body.AddChild(BuildSetBox(set, itemId, ownedItems, alreadyOwned, compact, briefSets));

		// Side by side, the plate goes at the foot and a spacer pushes it there, so the plates line
		// up across the row. Stacked on a phone it is in the header instead (BuildHeader).
		if (!narrow)
		{
			body.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
			body.AddChild(BuildClaimPlate(false, PlateTextFor(itemId)));
		}

		// Every label and container under the page lets the click through to the Button.
		BonelightSkin.MakeSubtreeClickThrough(margin);
		return root;
	}

	// --- The Scribe's Penknife: which spell to cut -------------------------------------------
	//
	// The same panel, re-dealt: the heading names the knife, one line says what a cut costs, and the
	// book is laid out as pages - the level-up cards' torn page again, so a spell looks like a spell
	// wherever it is chosen. Every held spell is shown, the wizard's own greyed and unpressable with
	// the reason on it, because a picker that silently left one out would read as a bug. Back
	// returns to the same three relics; the chest is not spent until a page is pressed.
	private void ShowCutPicker()
	{
		ClearCards();
		if (heading != null)
			heading.Text = ChestItemCatalog.GetDisplayName(ChestItemCatalog.ScribesPenknife);

		var column = new VBoxContainer
		{
			Name = "CutPicker",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		column.AddThemeConstantOverride("separation", 10);
		cardsRow.AddChild(column);

		// Prose on the stone needs the heading's calm band under it, or the stone's lips run through
		// the letters (.ai/level-up-screen.md, "the heading sits on a calm band").
		var prompt = new Label
		{
			Text = "Cut one spell out of your spell book. Its slot is free again, and it is not offered again this run.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		ResponsiveLayout.SetFont(prompt, ResponsiveLayout.TextRole.Body);
		prompt.AddThemeColorOverride("font_color", HeadingInk);
		var band = new StyleBoxFlat { BgColor = BandColor };
		band.SetCornerRadiusAll(2);
		band.SetContentMarginAll(8);
		prompt.AddThemeStyleboxOverride("normal", band);
		column.AddChild(prompt);

		// A phone stacks the pages; a PC lays them three across, so the whole book is on the screen
		// at once - two rows for six, three for the Folio's seven. Four across was tried for seven:
		// a page under 300 wide broke "Cinderbreath" in the middle of the word.
		var grid = new GridContainer
		{
			Columns = narrow ? 1 : Math.Min(3, Math.Max(1, heldSpells.Count)),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		grid.AddThemeConstantOverride("h_separation", 10);
		grid.AddThemeConstantOverride("v_separation", 14);
		column.AddChild(grid);

		Control firstCuttable = null;
		foreach (SpellData spell in heldSpells)
		{
			bool isProtected = spell.Id.Equals(protectedSpellId, StringComparison.OrdinalIgnoreCase);
			Control page = BuildSpellPage(spell, isProtected, out Button pageButton);
			grid.AddChild(page);
			if (!isProtected && firstCuttable == null)
				firstCuttable = pageButton;
		}

		column.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
		var back = new Button
		{
			Name = "CutBack",
			Text = "Back",
			CustomMinimumSize = new Vector2(160f, ResponsiveLayout.MinTouchTarget),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			ProcessMode = ProcessModeEnum.Always
		};
		ResponsiveLayout.SetFont(back, ResponsiveLayout.TextRole.Label);
		BonelightSkin.StyleButton(back);
		back.Pressed += () => SetOptions(offeredItemIds, offeredOwnedItems);
		column.AddChild(back);

		MenuNavigator.Attach(this, firstCuttable);
	}

	// One held spell as a page: art, name, level and tags. The whole page is the button, as on the
	// relic cards; the wizard's own spell is drawn faded and cannot be pressed or focused.
	private Control BuildSpellPage(SpellData spell, bool isProtected, out Button card)
	{
		EnsurePageStyles();
		var root = new PanelContainer { Name = "Cut_" + spell.Id, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		root.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

		card = new Button
		{
			Name = "CutCard",
			Text = string.Empty,
			TooltipText = isProtected ? spell.Name + " cannot be cut" : "Cut " + spell.Name,
			Disabled = isProtected,
			FocusMode = isProtected ? Control.FocusModeEnum.None : Control.FocusModeEnum.All,
			ProcessMode = ProcessModeEnum.Always
		};
		CardFamilyArt.ApplyFrame(card, heldResting, heldLit);
		string spellId = spell.Id;
		if (!isProtected)
			card.Pressed += () => EmitSignal(SignalName.SpellCutChosen, spellId);
		root.AddChild(card);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		// Faded, not hidden: the rule is something the player should see, not discover. Dark ink on
		// the page at 0.7 is still well over 4.5:1 (ui_contrast.py, "penknife").
		if (isProtected)
			margin.Modulate = new Color(1f, 1f, 1f, 0.7f);
		root.AddChild(margin);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		margin.AddChild(row);

		// Darkened toward ink in its element, as the level-up page does it (LevelUpMenu.IconTint): an
		// emissive spell icon drawn untouched is a pale mark on a pale page.
		var weights = spell.GetElementWeights();
		Color tint = weights.Count > 0
			? ElementColors.GetColor(weights.OrderByDescending(p => p.Value).First().Key).Lerp(Ink, 0.28f)
			: Colors.White;
		row.AddChild(new TextureRect
		{
			Texture = spell.Icon,
			CustomMinimumSize = new Vector2(64f, 64f),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			Modulate = tint
		});

		var text = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		text.AddThemeConstantOverride("separation", 0);
		row.AddChild(text);

		// Word, not WordSmart: a spell name may wrap between words but never inside one.
		var name = new Label { Text = spell.Name, AutowrapMode = TextServer.AutowrapMode.Word };
		ResponsiveLayout.SetFont(name, ResponsiveLayout.TextRole.Title);
		name.AddThemeColorOverride("font_color", CardFamilyArt.PageInk);
		name.AddThemeConstantOverride("line_spacing", -8);
		text.AddChild(name);

		string detail = $"Level {spell.CurrentLevel}";
		if (weights.Count > 0)
			detail += " · " + string.Join(", ", weights.OrderByDescending(p => p.Value).Select(p => p.Key.ToString()));
		var level = new Label { Text = detail, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		ResponsiveLayout.SetFont(level, ResponsiveLayout.TextRole.Label);
		// The protected page is faded, so it takes the strongest ink there.
		level.AddThemeColorOverride("font_color", isProtected ? CardFamilyArt.PageInk : CardFamilyArt.PageInkSoft);
		text.AddChild(level);

		if (isProtected)
		{
			var why = new Label { Text = "Your wizard's own spell: cannot be cut.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
			ResponsiveLayout.SetFont(why, ResponsiveLayout.TextRole.Label);
			why.AddThemeColorOverride("font_color", CardFamilyArt.PageInk);
			text.AddChild(why);
		}

		BonelightSkin.MakeSubtreeClickThrough(margin);
		return root;
	}

	private Control BuildHeader(string itemId, float iconSize)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		row.AddChild(BuildRelicIcon(itemId, iconSize));

		var text = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		text.AddThemeConstantOverride("separation", 0);
		text.AddChild(BuildName(itemId, HorizontalAlignment.Left, fiveAbreast));
		text.AddChild(BuildRarityLine(itemId));
		row.AddChild(text);

		if (narrow)
			row.AddChild(BuildClaimPlate(true, PlateTextFor(itemId)));
		return row;
	}

	// The plate says what pressing the page does. For the knife that is not a claim yet - it opens
	// the picker, and the claim is the spell pressed there.
	private static string PlateTextFor(string itemId) =>
		ChestItemCatalog.ScribesPenknife.Equals(itemId, StringComparison.OrdinalIgnoreCase) ? "Choose a spell" : "Claim";

	// 64px art drawn at x2 from 32: 96 is three screen pixels to each authored one, 64 is two, so
	// neither size smears. 80 (the old size) was neither. 64 only for four pages abreast.
	private static TextureRect BuildRelicIcon(string itemId, float size)
	{
		return new TextureRect
		{
			Texture = BonelightSkin.LoadTextureSafe(ChestItemCatalog.GetIconPath(itemId)),
			CustomMinimumSize = new Vector2(size, size),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
		};
	}

	// The thing being chosen takes the Title role, like a spell's name on a level-up page.
	private static Label BuildName(string itemId, HorizontalAlignment alignment, bool small = false)
	{
		var name = new Label
		{
			Text = ChestItemCatalog.GetDisplayName(itemId),
			HorizontalAlignment = alignment,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		ResponsiveLayout.SetFont(name, ResponsiveLayout.TextRole.Title);
		if (small)
			name.AddThemeFontSizeOverride("font_size", ResponsiveLayout.FontSize(name, ResponsiveLayout.TextRole.Label));
		name.AddThemeColorOverride("font_color", Ink);
		// Cinzel's line box is tall for capitals with no descenders; a two-line name ("Tithe of
		// Wormwood") set at its natural pitch overran the art beside it by a third of a line.
		name.AddThemeConstantOverride("line_spacing", -8);
		return name;
	}

	// "RARE  ·  Lightning": rarity in its own ink, then the element tags the relic carries. Only
	// Rare and Relic relics carry any (ChestItemCatalog.GetElementTags), so the tag is the reason a
	// rare one is worth the name - it belongs next to the word that says so.
	//
	// The tags are chips now, not words (CodexRows.BuildChip): the same tap-to-read element chip as
	// every other list, so "what does Darkness do, and how many do I hold" is answered on the page
	// that asks the player to take one. Tapping one opens the element's card and never claims the
	// relic - the page Button is a sibling of this content, not its parent.
	//
	// Each chip says what the player would hold after claiming it over the next tier's threshold -
	// "Lightning 3/4", the chip a new spell's card wears - not "Lightning x2" (2026-10-28: "when you see
	// that a mutation or item adds more of a specific tag, it should show the typical tag that tells you
	// how many tags you have out of how many you need for the next level").
	private Control BuildRarityLine(string itemId)
	{
		ChestItemRarity rarity = ChestItemCatalog.GetRarity(itemId);
		var tags = ChestItemCatalog.GetElementTags(itemId);
		var label = new Label
		{
			Text = rarity.ToString().ToUpperInvariant(),
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Label);
		label.AddThemeColorOverride("font_color", RarityInk(rarity));
		if (tags.Count == 0)
			return label;

		var line = new HFlowContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("h_separation", 8);
		line.AddThemeConstantOverride("v_separation", 4);
		line.AddChild(label);
		foreach (var (elementName, weight) in tags)
		{
			if (!Enum.TryParse(elementName, true, out Element element))
				continue;
			int held = ElementTagPopover.HeldCount(this, element);
			int after = held >= 0 ? held + weight : -1;
			string text = after >= 0 ? $"{element} {ElementPassiveDescriptions.GetProgressLabel(after)}"
				: weight > 1 ? $"{element} x{weight}" : element.ToString();
			ElementChip chip = CodexRows.BuildChip(element, text, focusable: true, countAfter: after);
			// What the claim adds; the card adds it to the count the player holds when it opens.
			chip.Gain = weight;
			line.AddChild(chip);
		}
		return line;
	}

	// What it does, one glyph-led line per stat (StatIcons), at the Body role. On a phone a list of
	// three or more short stats (the Tithe of Wormwood has five) is set in two columns: the page is
	// the full width there, and five rows of two words each spent a fifth of the screen on margin.
	private Control BuildEffectList(string itemId)
	{
		List<string> parts = SplitEffect(ChestItemCatalog.GetDescription(itemId));
		bool twoColumns = narrow && parts.Count >= 3 && parts.All(p => p.Length <= ShortStatLength);
		Container effects;
		if (twoColumns)
		{
			var grid = new GridContainer { Columns = 2 };
			grid.AddThemeConstantOverride("h_separation", 16);
			grid.AddThemeConstantOverride("v_separation", 0);
			effects = grid;
		}
		else
		{
			var list = new VBoxContainer();
			list.AddThemeConstantOverride("separation", 0);
			effects = list;
		}
		foreach (string part in parts)
		{
			Control line = StatIcons.BuildLine(part, Ink, ResponsiveLayout.TextRole.Body, expand: false, wrap: !twoColumns);
			if (twoColumns)
				line.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			// The engine sans sets its lines loosely; a wrapped effect sentence on a page this
			// narrow read as separate lines rather than one thought, and cost a line in every four.
			foreach (Label label in line.GetChildren().OfType<Label>())
				label.AddThemeConstantOverride("line_spacing", WrappedLineSpacing);
			effects.AddChild(line);
		}
		return effects;
	}

	private static Color RarityInk(ChestItemRarity rarity) => rarity switch
	{
		ChestItemRarity.Uncommon => InkUncommon,
		ChestItemRarity.Rare => InkRare,
		ChestItemRarity.Relic => InkRelic,
		_ => InkCommon,
	};

	// Drawn, not pressed: a stone button frame at Primary weight that does nothing on its own. The
	// page under it is the button. Ignoring the mouse and refusing focus keeps it out of
	// MenuNavigator's walk and off MenuFocusHighlight's list, so the stick lands on pages only.
	private static Control BuildClaimPlate(bool compact, string text)
	{
		var plate = new Button
		{
			Name = "ClaimPlate",
			Text = text,
			FocusMode = Control.FocusModeEnum.None,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = compact ? Control.SizeFlags.ShrinkEnd : Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(compact ? (text.Length > 6 ? 168f : 120f) : 0f, ResponsiveLayout.MinTouchTarget)
		};
		ResponsiveLayout.SetFont(plate, compact ? ResponsiveLayout.TextRole.Body : ResponsiveLayout.TextRole.Label);
		BonelightSkin.StyleButton(plate, BonelightSkin.Emphasis.Primary);
		return plate;
	}

	// One Full Set Enchantment the relic belongs to: what the set is, which pieces are held, and
	// what each tier pays. The news this box exists to carry is "this one finishes something", so
	// the tier a claim would switch on is marked NOW in the gain ink, the count turns the same ink,
	// and the box takes a heavier rule.
	private Control BuildSetBox(ChestSetDefinition set, string itemId, IReadOnlyCollection<string> ownedItems,
		bool alreadyOwned, bool compact, bool brief)
	{
		var (owned, total) = ChestItemCatalog.GetSetProgress(set, ownedItems);
		int after = alreadyOwned ? owned : Math.Min(total, owned + 1);
		bool hasPartial = set.PartialItemCount > 0 && set.PartialEffects != null && set.PartialEffects.Length > 0;
		bool completes = !alreadyOwned && owned < total && after >= total;
		bool reachesPartial = hasPartial && !alreadyOwned && owned < set.PartialItemCount
			&& after >= set.PartialItemCount && !completes;
		bool news = completes || reachesPartial;

		var box = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		var style = new StyleBoxFlat { BgColor = SetBoxGround, BorderColor = SetBoxRule };
		style.SetBorderWidthAll(news ? 2 : 1);
		style.SetCornerRadiusAll(3);
		style.SetContentMarginAll(8);
		box.AddThemeStyleboxOverride("panel", style);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 4);
		box.AddChild(column);

		// The set's icon - the object whole - beside its name over its pieces. The two rows beside
		// it are exactly the icon's height, so the box spends no line on the icon alone.
		var top = new HBoxContainer();
		top.AddThemeConstantOverride("separation", 10);
		column.AddChild(top);
		top.AddChild(new TextureRect
		{
			Texture = BonelightSkin.LoadTextureSafe(set.IconPath),
			CustomMinimumSize = fiveAbreast ? new Vector2(40f, 40f) : new Vector2(64f, 64f),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		});

		var nameColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		nameColumn.AddThemeConstantOverride("separation", 2);
		top.AddChild(nameColumn);

		var setName = new Label
		{
			Text = set.Name,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		ResponsiveLayout.SetFont(setName, ResponsiveLayout.TextRole.Label);
		setName.AddThemeColorOverride("font_color", Ink);
		nameColumn.AddChild(setName);

		var piecesRow = BuildPiecesRow(set, itemId, ownedItems);
		// The count, before and after, at the end of the pieces it counts: "1/3 -> 2/3" is the whole
		// decision in a handful of characters, and it reads straight off the icons beside it.
		var count = new Label
		{
			Text = alreadyOwned ? $"{owned}/{total}" : $"{owned}/{total} → {after}/{total}",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center
		};
		ResponsiveLayout.SetFont(count, ResponsiveLayout.TextRole.Label);
		count.AddThemeColorOverride("font_color", news ? InkGain : InkSoft);
		piecesRow.AddChild(count);
		nameColumn.AddChild(piecesRow);

		// Brief (a relic in two sets): the partial is shown only when this claim is
		// what switches it on.
		if (hasPartial && (!brief || reachesPartial))
		{
			bool partialHeld = owned >= set.PartialItemCount;
			string partialText = string.Join(", ", set.PartialEffects.Select(e => $"{e.Label} {e.Value}"));
			string tier = $"{set.PartialItemCount} pieces";
			if (reachesPartial)
				column.AddChild(BuildTierLine(tier + ", now:", partialText, InkGain, InkGain));
			else
				column.AddChild(BuildTierLine(partialHeld ? tier + ", held:" : tier + ":", partialText,
					InkSoft, partialHeld ? InkSoft : Ink));
		}

		// The full set's Description is the mechanic in one sentence (ChestSetDefinition.Description),
		// followed by the element tags it grants - a real effect that no screen used to mention.
		string fullText = set.Description;
		if (set.ElementTags != null && set.ElementTags.Length > 0)
			fullText += " " + string.Join(", ", set.ElementTags.Select(t => $"+{t.Weight} {t.Element}")) + ".";
		string fullTier = hasPartial ? $"All {total}" : $"Both";
		// Five abreast, the full tier is the news only when this claim is what completes it.
		if (fiveAbreast && !completes)
			return box;
		column.AddChild(completes
			? BuildTierLine(fullTier + ", now:", fullText, InkGain, Ink)
			: BuildTierLine(fullTier + ":", fullText, InkSoft, Ink));

		return box;
	}

	// One icon per piece, so "how close am I" is something the player sees rather than counts:
	// held pieces at full strength, the piece on offer ringed in gold, missing ones faded.
	//
	// A FLOW, not a box: four pages abreast (the Reliquary Key) are under 300 wide, and a row of four
	// pieces plus the count is wider than that. A box would hold the page at its width and push the
	// fourth page off the screen; a flow drops the count to the next line instead.
	private static HFlowContainer BuildPiecesRow(ChestSetDefinition set, string offeredId, IReadOnlyCollection<string> ownedItems)
	{
		var row = new HFlowContainer();
		row.AddThemeConstantOverride("h_separation", 4);
		row.AddThemeConstantOverride("v_separation", 0);
		foreach (string pieceId in set.RequiredItemIds)
		{
			bool held = ownedItems.Contains(pieceId, StringComparer.OrdinalIgnoreCase);
			bool offered = string.Equals(pieceId, offeredId, StringComparison.OrdinalIgnoreCase);

			var frame = new PanelContainer { TooltipText = ChestItemCatalog.GetDisplayName(pieceId) };
			var frameStyle = new StyleBoxFlat
			{
				BgColor = new Color(0f, 0f, 0f, 0f),
				BorderColor = offered ? SetBoxRule : new Color(0f, 0f, 0f, 0f)
			};
			frameStyle.SetBorderWidthAll(2);
			frameStyle.SetCornerRadiusAll(3);
			frameStyle.SetContentMarginAll(2);
			frame.AddThemeStyleboxOverride("panel", frameStyle);

			// 32 is the art's authored size, one screen pixel each - small, but every edge is clean.
			frame.AddChild(new TextureRect
			{
				Texture = BonelightSkin.LoadTextureSafe(ChestItemCatalog.GetIconPath(pieceId)),
				CustomMinimumSize = new Vector2(32f, 32f),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				// Faded, not greyed: a missing piece is still the shape the player is hunting for.
				Modulate = held || offered ? Colors.White : new Color(1f, 1f, 1f, 0.30f)
			});
			row.AddChild(frame);
		}
		return row;
	}

	// "2 pieces: Damage taken -5%" - the tier in its own ink, what it pays in another, as ONE
	// wrapping paragraph. A tier label in a column of its own beside the text read as a table, and
	// cost the text a fifth of a card's width on every line; the Jewelled Crown's sentence alone
	// went from four lines to six.
	private Control BuildTierLine(string tier, string text, Color tierInk, Color ink)
	{
		var label = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = true,
			ScrollActive = false,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Text = $"[color=#{tierInk.ToHtml(false)}]{Escape(tier)}[/color] {Escape(text)}"
		};
		// The Label role's size; RichTextLabel reads its own theme names for size and colour.
		label.AddThemeFontSizeOverride("normal_font_size", ResponsiveLayout.FontSize(this, ResponsiveLayout.TextRole.Label));
		label.AddThemeColorOverride("default_color", ink);
		label.AddThemeConstantOverride("line_separation", WrappedLineSpacing);
		return label;
	}

	// Set names and descriptions are plain text; a bracket in one must not open a tag.
	private static string Escape(string text) => (text ?? string.Empty).Replace("[", "[lb]");

	/// <summary>
	/// A relic's one-line description as stat lines: "+10% spell damage, -5% movement speed." is two
	/// stats and gets two glyphs. Only a list whose every part is a signed number is split - a
	/// sentence with a comma in it ("kept as a shield, up to 20%") is one thought and stays one line.
	/// </summary>
	private static List<string> SplitEffect(string description)
	{
		string text = (description ?? string.Empty).Trim().TrimEnd('.');
		string[] parts = text.Split(", ", StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length > 1 && parts.All(p => p.Length > 1 && (p[0] == '+' || p[0] == '-')))
			return parts.Select(p => p.Trim()).ToList();
		return new List<string> { text };
	}
}
