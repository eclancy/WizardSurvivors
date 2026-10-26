using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

// The Arcane Upgrades shop: everything bought with Arcane Energy, on the main menu.
// See .ai/level-up-screen.md, "Arcane Upgrades", for the layout and the rules it follows.
//
// It came back from the player as "unreadable": twenty upgrades and five spells in one grid of
// identical boxes, four across on a PC and two on a phone, every box a name, a Micro-size line of
// "Lv 3/8  Cost: 90 AE  Buy", a glyph-led effect and a centred "Current total bonus" sentence, the
// spells for sale sharing the grid as "Unlock Gravewell" boxes at its foot, and a maxed upgrade
// told apart by a grey "Maxed" on a button you could not press.
//
// It is the level-up screen's book now, because a level bought here is the same kind of thing as a
// level taken there:
//   - a SPELL FOR SALE is a new spell's torn page (the only thing written like a new spell is one),
//   - a PERMANENT UPGRADE is the upgrade's rubricated page with its level pips,
//   - a MASTERED upgrade is that page GILDED, with a gold seal pressed on it and no price,
// grouped into sections by what they do, one entry per card, laid out top to bottom as name, what
// it gives now and at the next level, what one level gives, then the pips and the price.
//
// THE BEHAVIOUR IS THE OLD SHOP'S, exactly: the same definitions (BuildUpgradeDefinitions), the
// same prices and ceilings (GetShopItemCost / GetShopItemMaxLevel), the same purchase - spend,
// write the level or unlock the spell, save - and the same refreshes after it.
public partial class MainMenu
{
	// --- what is on a card ----------------------------------------------------------------------

	private enum ShopRowState { Buyable, TooDear, Mastered, Owned, NotYetOpen }

	private sealed class ShopRow
	{
		public UpgradeDefinition Def = null!;
		public ArcaneShopCard Card = null!;
		public Control IconBox = null!;
		public LevelPips Pips;
		public Label LevelText;
		public Label StatNow;
		public Label StatNext;
		public Button Plate = null!;
		public Control PlateContent = null!;
		public TextureRect PlateGlyph = null!;
		public Label PlateCost = null!;
		public Label PlateAction = null!;
		public Control DoneMark = null!;
		public Control Notice = null!;
		public Label NoticeText = null!;
		public MasterySeal Seal;
		public ShopFlare Flare = null!;
		public ShopRowState State;
	}

	private readonly Dictionary<string, ShopRow> shopRows = new();
	// Each section's "2 of 7 mastered", re-counted after every purchase.
	private readonly List<(Label Tally, string[] Ids, bool Spells)> shopTallies = new();
	private ScrollContainer upgradeScroll = null!;
	private int shopColumnsBuilt;

	// The upgrades by what they do. An id that is registered and listed in no section still gets a
	// card, in a last "More" section, with a warning - a new upgrade must never vanish from the shop
	// because nobody added it here.
	private static readonly (string Title, string Note, string[] Ids)[] UpgradeSections =
	{
		("Power", "Every spell you cast, harder and more often.",
			new[] { "damage", "cooldowns", "attack_speed", "area", "duration", "amount", "crit_chance" }),
		("Survival", "Staying alive long enough to use it.",
			new[] { "vitality", "recovery", "extra_lives", "movespeed" }),
		("Fortune", "What each run gives back.",
			new[] { "magnet", "growth", "greed", "luck" }),
		("Level-ups", "More say over what each level-up offers you.",
			new[] { "rerolls", "bans", "banked_levels", "auguries", GlobalStatsManager.CurationUpgradeId }),
	};

	private const string SpellSectionTitle = "New spells";
	private const string SpellSectionNote = "Bought once, then offered at your level-ups for good.";
	private const string BoonSectionTitle = "New boons";
	private const string BoonSectionNote = "Bought once, then a boon your level-ups may deal you.";
	private const string ShopIntro = "Everything here is bought once and kept for good.";

	// --- the inks, and where each is measured ----------------------------------------------------
	// Every one is COPIED into tools/art/ui_contrast.py ("Arcane Upgrades shop"): change one, change
	// both. On a page they are the page's own inks (CardFamilyArt.PageInk, the level-up screen's
	// Ink / InkSoft / Rubric / InkGold); on the stone, the codex's Stone register.
	private static readonly Color ShopInk = CardFamilyArt.PageInk;                      // flesh deep
	private static readonly Color ShopInkSoft = CardFamilyArt.PageInkSoft;              // violet base
	private static readonly Color ShopRubric = CardFamilyArt.UpgradeRule;               // red lit
	private static readonly Color ShopInkGold = new Color(0.329f, 0.247f, 0.063f);       // gold shade
	private static readonly Color ShopInkMeta = new Color(0.431f, 0.267f, 0.314f);       // flesh shade
	// The buy plate: the lit stone button (BonelightSkin Emphasis.Primary) and its disabled frame.
	private static readonly Color PlateCostInk = new Color(1.0f, 0.941f, 0.753f);        // gold hi
	private static readonly Color PlateActionInk = new Color(1.0f, 0.98f, 0.90f);        // Primary text
	private static readonly Color PlateDimInk = new Color(0.604f, 0.643f, 0.722f);       // skin base
	// The heading band (the level-up heading's) and the purse on it.
	private static readonly Color ShopBandFill = new Color(0.043f, 0.059f, 0.094f);      // stone deep
	private static readonly Color ShopBandRule = new Color(0.541f, 0.416f, 0.118f);      // gold base
	private static readonly Color ShopHeadingInk = new Color(1f, 0.95f, 0.86f);          // HeadingInk
	private static readonly Color PurseInk = new Color(0.847f, 0.690f, 0.290f);          // gold lit
	private static readonly Color PurseSpentInk = new Color(0.95f, 0.72f, 0.45f);        // Stone.Flag

	private const string SocketPath = "res://assets/bonelight/ui/ui-upgrade-socket.png";
	private const string SocketLitPath = "res://assets/bonelight/ui/ui-upgrade-socket-lit.png";
	private const string MasteredPagePath = "res://assets/bonelight/ui/ui-spell-page-mastered.png";
	private const string MasteredPageLitPath = "res://assets/bonelight/ui/ui-spell-page-mastered-lit.png";
	private const string SpellPagePath = "res://assets/bonelight/ui/ui-spell-page.png";
	private const string SpellPageLitPath = "res://assets/bonelight/ui/ui-spell-page-lit.png";
	private const string UpgradeIconRoot = "res://assets/bonelight/ui/upgrades/";

	// The socket is 80 drawn (40 authored, upgrade_socket in ui_frames.py) and the icon inside it 64.
	private const float SocketSize = 80f;
	private const float IconSize = 64f;
	// The plate is two lines - the price, then what pressing it does - inside the stone button's
	// iron band, which takes about ten units of each edge.
	private const float PlateHeight = 74f;
	// Wide enough for "Need 165 more" inside the iron band on either canvas (the PC's Label size is
	// smaller, but 150 put "more" on the band).
	private static float PlateWidth(bool narrow) => narrow ? 188f : 172f;

	// The frames, built once per menu. INSTANCE fields: a StyleBox held in a static outlives the
	// engine's reference and trips gchandle.is_released() at teardown (BonelightSkin's remarks).
	private StyleBoxTexture shopUpgradeFrame, shopUpgradeFrameLit;
	private StyleBoxTexture shopMasteredFrame, shopMasteredFrameLit;
	private StyleBoxTexture shopSpellFrame, shopSpellFrameLit;

	private Control shopPurse;
	private Tween purseTween;
	private Tween purseFlashTween;

	// --- the header ------------------------------------------------------------------------------

	// The level-up screen's heading band - the darkest stone, solid, a gold rule at its foot - with
	// the purse on its right. The purse is what a player opens the shop to check, and it stays in
	// view while the list scrolls under it: it is outside the scroll, on the band, in gold.
	private void EnsureShopHeader()
	{
		var vbox = GetNodeOrNull<VBoxContainer>("MarginContainer/VBoxContainer/Content/ArcaneUpgradesPanel/ArcaneUpgradesVBox");
		if (vbox == null || vbox.GetNodeOrNull("ShopBand") != null)
			return;

		Label title = vbox.GetNodeOrNull<Label>("ArcaneUpgradesTitle");
		var band = new PanelContainer { Name = "ShopBand", MouseFilter = Control.MouseFilterEnum.Ignore };
		var style = new StyleBoxFlat { BgColor = ShopBandFill, BorderColor = ShopBandRule };
		style.BorderWidthBottom = 2;
		style.SetCornerRadiusAll(2);
		style.ContentMarginLeft = 16;
		style.ContentMarginRight = 14;
		style.ContentMarginTop = 6;
		style.ContentMarginBottom = 8;
		band.AddThemeStyleboxOverride("panel", style);
		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 16);
		band.AddChild(row);
		vbox.AddChild(band);
		vbox.MoveChild(band, 0);

		if (title != null)
		{
			title.Reparent(row);
			title.HorizontalAlignment = HorizontalAlignment.Left;
			title.VerticalAlignment = VerticalAlignment.Center;
			title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			// Here, not in ApplyMenuSkin: that finds the heading by its scene path, which the band
			// has just changed.
			ResponsiveLayout.SetFont(title, ResponsiveLayout.TextRole.Display);
			title.AddThemeColorOverride("font_color", ShopHeadingInk);
		}

		shopPurse = BuildShopPurse();
		row.AddChild(shopPurse);
	}

	// The glyph at x4 (the 12px energy orb drawn at 24, shown at 48 - a whole multiple), the amount
	// at the Title size in the prose face, and the words under it.
	private Control BuildShopPurse()
	{
		var purse = new HBoxContainer
		{
			Name = "ShopPurse",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
		};
		purse.AddThemeConstantOverride("separation", 8);
		if (StatIcons.Load("energy") is Texture2D glyph)
		{
			purse.AddChild(new TextureRect
			{
				Texture = glyph,
				CustomMinimumSize = new Vector2(48f, 48f),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			});
		}
		var words = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		words.AddThemeConstantOverride("separation", -4);
		shopEnergyAmount = new Label { Name = "Amount", MouseFilter = Control.MouseFilterEnum.Ignore };
		// The Title SIZE, not the Title face: a number is read, not a heading.
		shopEnergyAmount.AddThemeFontSizeOverride("font_size", ResponsiveLayout.FontSize(this, ResponsiveLayout.TextRole.Title));
		shopEnergyAmount.AddThemeColorOverride("font_color", PurseInk);
		words.AddChild(shopEnergyAmount);
		var caption = new Label { Text = "Arcane Energy", MouseFilter = Control.MouseFilterEnum.Ignore };
		ResponsiveLayout.SetFont(caption, ResponsiveLayout.TextRole.Micro);
		caption.AddThemeColorOverride("font_color", CodexRows.Stone.InkSoft);
		words.AddChild(caption);
		purse.AddChild(words);
		return purse;
	}

	/// <summary>The purse, at once (RefreshArcaneEnergy). A purchase animates it instead.</summary>
	private void SetShopPurse(int total)
	{
		purseTween?.Kill();
		purseTween = null;
		shopEnergyAmount.Text = FormatEnergy(total);
	}

	// The count ticks down to what is left, and what was spent rises off it and fades. Never blocks:
	// a second purchase kills the first tween and starts from the number on screen.
	private void AnimatePurse(int from, int to)
	{
		if (shopEnergyAmount == null)
			return;
		purseTween?.Kill();
		purseTween = CreateTween();
		purseTween.TweenMethod(Callable.From<float>(v => shopEnergyAmount.Text = FormatEnergy(Mathf.RoundToInt(v))),
			(float)from, (float)to, 0.45f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

		var spent = new Label
		{
			Text = "-" + FormatEnergy(from - to),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Position = new Vector2(-4f, -2f),
		};
		ResponsiveLayout.SetFont(spent, ResponsiveLayout.TextRole.Body);
		spent.AddThemeColorOverride("font_color", PurseSpentInk);
		spent.AddThemeColorOverride("font_outline_color", ShopBandFill);
		spent.AddThemeConstantOverride("outline_size", 4);
		// A child of the amount label, which is not a container, so it can be moved freely; it
		// starts left of the number and rises over the band.
		shopEnergyAmount.AddChild(spent);
		spent.Position = new Vector2(-spent.GetMinimumSize().X - 8f, 0f);
		Tween rise = spent.CreateTween();
		rise.TweenProperty(spent, "position:y", -22f, 0.7f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		rise.Parallel().TweenProperty(spent, "modulate:a", 0f, 0.7f).SetDelay(0.15f);
		rise.TweenCallback(Callable.From(spent.QueueFree));
	}

	// "Not enough": the purse flushes the spent ink and back, so the eye goes to what is short.
	private void FlashPurse()
	{
		if (shopEnergyAmount == null)
			return;
		purseFlashTween?.Kill();
		shopEnergyAmount.AddThemeColorOverride("font_color", PurseSpentInk);
		purseFlashTween = CreateTween();
		purseFlashTween.TweenInterval(0.3f);
		purseFlashTween.TweenCallback(Callable.From(() => shopEnergyAmount.AddThemeColorOverride("font_color", PurseInk)));
	}

	// --- the list ---------------------------------------------------------------------------------

	private void EnsureShopFrames()
	{
		shopUpgradeFrame ??= CardFamilyArt.Frame(CardFamily.Upgrade, false, 0);
		shopUpgradeFrameLit ??= CardFamilyArt.Frame(CardFamily.Upgrade, true, 0);
		shopMasteredFrame ??= TiledPage(MasteredPagePath);
		shopMasteredFrameLit ??= TiledPage(MasteredPageLitPath);
		shopSpellFrame ??= TiledPage(SpellPagePath);
		shopSpellFrameLit ??= TiledPage(SpellPageLitPath);
	}

	// A torn page tiles, never stretches: a stretched deckle smears into one long taper
	// (.ai/level-up-screen.md, section 2). Sliced at 24 like every family frame.
	private static StyleBoxTexture TiledPage(string path)
	{
		var style = new StyleBoxTexture
		{
			Texture = GD.Load<Texture2D>(path),
			AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile,
			AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Tile,
		};
		style.SetTextureMarginAll(CardFamilyArt.PatchMargin);
		style.SetContentMarginAll(0);
		return style;
	}

	/// <summary>
	/// Builds the whole shop: the intro, then the sections in the order that is right now.
	/// </summary>
	/// <remarks>
	/// Order is by what the player can act on (the codex's rule, .ai/level-up-screen.md 4c): while any
	/// spell is still for sale its section leads - there are nine, each changes how a run plays, and
	/// they were the least findable thing in the old grid, at its foot; once all nine are owned it
	/// goes last and quiet. Within the upgrades a maxed card stays where it is: an entry that jumped
	/// to the end the moment it was finished would take the selection with it.
	/// </remarks>
	private void BuildShop()
	{
		if (upgradeList == null)
			return;
		EnsureShopFrames();
		ClearChildren(upgradeList);
		shopRows.Clear();
		shopTallies.Clear();
		shopColumnsBuilt = CodexRows.ColumnsFor(this);
		bool narrow = ResponsiveLayout.IsNarrow(this);

		upgradeList.AddChild(CodexRows.BuildSummary(ShopIntro, CodexRows.Stone));

		SaveData data = GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data;
		var spells = upgradeDefinitions.Values.Where(d => d.IsSpellUnlock && !d.IsBoon).ToList();
		bool spellsForSale = spells.Any(d => !GlobalStatsManager.IsSpellUnlockedForLevelUp(data, d.SpellId));
		var boons = upgradeDefinitions.Values.Where(d => d.IsBoon).ToList();
		bool boonsForSale = boons.Any(d => !GlobalStatsManager.IsBoonUnlocked(data, d.BoonId));

		if (spells.Count > 0 && spellsForSale)
			AddShopSection(SpellSectionTitle, SpellSectionNote, spells, narrow, spellSection: true);
		if (boons.Count > 0 && boonsForSale)
			AddShopSection(BoonSectionTitle, BoonSectionNote, boons, narrow, spellSection: true);

		var placed = new HashSet<string>();
		foreach ((string title, string note, string[] ids) in UpgradeSections)
		{
			var defs = ids.Where(upgradeDefinitions.ContainsKey).Select(id => upgradeDefinitions[id]).ToList();
			placed.UnionWith(defs.Select(d => d.Id));
			if (defs.Count > 0)
				AddShopSection(title, note, defs, narrow, spellSection: false);
		}
		var stray = upgradeDefinitions.Values.Where(d => !d.IsSpellUnlock && !placed.Contains(d.Id)).ToList();
		if (stray.Count > 0)
		{
			GD.PushWarning($"MainMenu: Arcane Upgrades has no section for {string.Join(", ", stray.Select(d => d.Id))}; listed under More (MainMenu.Shop.cs, UpgradeSections).");
			AddShopSection("More", string.Empty, stray, narrow, spellSection: false);
		}

		if (spells.Count > 0 && !spellsForSale)
			AddShopSection(SpellSectionTitle, "All bought - every one is in your book.", spells, narrow, spellSection: true);
		if (boons.Count > 0 && !boonsForSale)
			AddShopSection(BoonSectionTitle, "All bought - every one can turn up at a level-up.", boons, narrow, spellSection: true);

		RefreshUpgradeControls();
	}

	private void AddShopSection(string title, string note, List<UpgradeDefinition> defs, bool narrow, bool spellSection)
	{
		var cards = defs.Select(d => (Control)BuildShopCard(d, narrow)).ToList();
		VBoxContainer section = CodexRows.AddSection(upgradeList, title, " ", CodexRows.Stone, shopColumnsBuilt, cards);
		// The tally CodexRows put in the heading row, filled in by RefreshUpgradeControls.
		if (section.GetChild(0) is HBoxContainer head && head.GetChildCount() > 1 && head.GetChild(1) is Label tally)
			shopTallies.Add((tally, defs.Select(d => d.Id).ToArray(), spellSection));
		if (!string.IsNullOrEmpty(note))
		{
			Label line = CodexRows.MakeText(note, ResponsiveLayout.TextRole.Label, CodexRows.Stone.InkSoft);
			section.AddChild(line);
			section.MoveChild(line, 2);
		}
	}

	// One card. Built once per visit and then UPDATED in place by RefreshUpgradeControls, so that a
	// purchase can be watched landing on the card the player pressed rather than on a rebuilt one -
	// and so the selection never leaves it.
	private ArcaneShopCard BuildShopCard(UpgradeDefinition def, bool narrow)
	{
		bool spell = def.IsSpellUnlock;
		SpellbookEntry entry = def.IsBoon ? BoonShopEntry(def.BoonId)
			: spell ? spellbookEntries.FirstOrDefault(e => e.Id.Equals(def.SpellId, StringComparison.OrdinalIgnoreCase))
			: null;
		var row = new ShopRow { Def = def };

		var card = new ArcaneShopCard
		{
			Name = "ShopCard_" + def.Id.Replace(":", "_"),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			RestingFrame = spell ? shopSpellFrame : shopUpgradeFrame,
			LitFrame = spell ? shopSpellFrameLit : shopUpgradeFrameLit,
		};
		row.Card = card;

		// THE CARD IS THE ONE STOP, for a pad and a keyboard, on every card: A on it buys what it
		// sells, or refuses out loud. It was first the plate when the card could be bought and the card
		// when it could not, and _UiShot --pad showed what that does to a grid: Godot's focus search
		// measures from rect to rect, a whole card a row down is nearer than a plate across the way,
		// so "right" from one card landed on the card below its neighbour. Cards of one size in a grid
		// navigate like the grid.
		// It ignores the POINTER (CodexRows' FocusStop): a finger or a mouse buys only through the
		// plate, so a tap that meant "let me read this" never spends anything, and a drag that starts
		// on the page scrolls the list.
		Button stop = BonelightSkin.MakeCardClickOverlay(true);
		stop.Name = "CardStop";
		stop.MouseFilter = Control.MouseFilterEnum.Ignore;
		// No gold wash: the overlay's own hover and focus tint would lay 16% gold over the parchment
		// and change how readable the selected card is. The lit frame and the ring say "selected".
		foreach (string state in new[] { "hover", "pressed", "focus" })
			stop.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
		string capturedId = def.Id;
		stop.Pressed += () => OnShopCardPressed(capturedId);
		card.AddChild(stop);
		card.FocusStop = stop;

		var padding = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		padding.AddThemeConstantOverride("margin_left", 22);
		padding.AddThemeConstantOverride("margin_right", 22);
		padding.AddThemeConstantOverride("margin_top", 18);
		// The foot of a page (.ai/level-up-screen.md 2e): the frame's inner border plus the air a card
		// has over its head, so the plate never sits on the rule.
		padding.AddThemeConstantOverride("margin_bottom", CardFamilyArt.FootInset(spell ? CardFamily.NewSpell : CardFamily.Upgrade));
		card.AddChild(padding);

		var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 16);
		padding.AddChild(line);

		row.IconBox = spell ? BuildSpellIconBox(entry) : BuildSocket(def.Id, card);
		line.AddChild(row.IconBox);

		var column = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		column.AddThemeConstantOverride("separation", 6);
		line.AddChild(column);

		// The name: what the player is choosing, in the heading face.
		Label name = CodexRows.MakeText(spell ? (entry?.DisplayName ?? def.SpellId) : def.DisplayName,
			ResponsiveLayout.TextRole.Title, ShopInk);
		column.AddChild(name);

		var footer = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		footer.AddThemeConstantOverride("separation", 10);
		if (spell)
		{
			// What it does - the whole reason to buy it - in the strongest ink, then its tags. The Label
			// size, as a codex row's line is: at the Body size a PC card ran to four lines of it.
			column.AddChild(CodexRows.MakeText(entry?.Description ?? string.Empty, ResponsiveLayout.TextRole.Label, ShopInk));
			var chips = new HFlowContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			chips.AddThemeConstantOverride("h_separation", 8);
			chips.AddThemeConstantOverride("v_separation", 6);
			if (entry != null)
			{
				// Not focus stops here: the card has one already, and a stick that stopped on every
				// chip would take three pushes to leave a card. A tap still opens the element's card.
				foreach (var (element, weight) in entry.ElementList)
					chips.AddChild(CodexRows.BuildChip(element, weight > 1 ? $"{element} x{weight}" : element.ToString(), false));
				if (entry.ElementList.Count == 1)
				{
					Label attuned = CodexRows.MakeText("Attuned", ResponsiveLayout.TextRole.Label, ShopInkSoft);
					attuned.AutowrapMode = TextServer.AutowrapMode.Off;
					chips.AddChild(attuned);
				}
			}
			footer.AddChild(chips);
		}
		else
		{
			// Now, and what the next level makes it: the stat line, led by its glyph.
			var stat = new HFlowContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			stat.AddThemeConstantOverride("h_separation", 8);
			stat.AddThemeConstantOverride("v_separation", 2);
			if (StatIcons.Load(UpgradeGlyph(def.Id)) is Texture2D glyph)
				stat.AddChild(Glyph(glyph));
			(string statLabel, _) = UpgradeStat(def.Id, 0);
			stat.AddChild(NoWrap(statLabel, ResponsiveLayout.TextRole.Body, ShopInkSoft));
			row.StatNow = NoWrap(string.Empty, ResponsiveLayout.TextRole.Body, ShopInk);
			stat.AddChild(row.StatNow);
			row.StatNext = NoWrap(string.Empty, ResponsiveLayout.TextRole.Body, ShopRubric);
			stat.AddChild(row.StatNext);
			column.AddChild(stat);

			// What ONE level gives, for the decision of whether the next is worth its price.
			column.AddChild(CodexRows.MakeText("Each level: " + def.EffectText, ResponsiveLayout.TextRole.Label, ShopInkSoft));

			// The pips over "Level 3/8", not beside it: the plate makes the foot 74 tall either way, and
			// side by side a ten-pip track, the words and the plate set the card's minimum width past
			// half a PC panel, so the two columns of cards came out different widths.
			var track = new VBoxContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			track.AddThemeConstantOverride("separation", 4);
			row.Pips = new LevelPips
			{
				Ring = new Color(0.541f, 0.416f, 0.118f),      // gold base, 3.8:1 on the page
				Empty = new Color(ShopInkSoft, 0.16f),
				Glow = new Color(0.847f, 0.690f, 0.290f),      // gold lit
				SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
			};
			track.AddChild(row.Pips);
			row.LevelText = NoWrap(string.Empty, ResponsiveLayout.TextRole.Label, ShopInkSoft);
			row.LevelText.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			track.AddChild(row.LevelText);
			footer.AddChild(track);
		}

		// The foot of the card sinks to its bottom, so the feet of a row of cards line up.
		column.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
		column.AddChild(footer);
		// The price at the right of the card's foot, on both canvases. A column of its own at the
		// card's right was tried on a PC, to save the height a foot row costs, and it cost the words
		// their width instead: half of every stat line and the spells' descriptions wrapped, which made
		// the cards taller than the foot row had.
		footer.AddChild(BuildPlateSlot(row, narrow));

		card.Plate = row.Plate;
		if (row.Pips != null)
		{
			row.Pips.FocusOwner = stop;
			row.Pips.AlsoLitBy = row.Plate;
		}

		if (!spell)
		{
			row.Seal = new MasterySeal { Name = "MasterySeal", Visible = false };
			card.AddChild(row.Seal);
		}
		row.Flare = new ShopFlare { Name = "ShopFlare" };
		card.AddChild(row.Flare);

		shopRows[def.Id] = row;
		return card;
	}

	// The icon in its socket of arcane glass (ui_frames.py:upgrade_socket): an 80 control with the
	// socket and the 64 icon drawn at whole scales, never through a container's stretch.
	private Control BuildSocket(string id, ArcaneShopCard card)
	{
		var box = new Control
		{
			CustomMinimumSize = new Vector2(SocketSize, SocketSize),
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		var socket = new TextureRect
		{
			Texture = GD.Load<Texture2D>(SocketPath),
			Size = new Vector2(SocketSize, SocketSize),
			StretchMode = TextureRect.StretchModeEnum.Keep,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		box.AddChild(socket);
		card.Socket = socket;
		card.SocketResting = socket.Texture;
		card.SocketLit = GD.Load<Texture2D>(SocketLitPath);
		Texture2D icon = BonelightSkin.LoadTextureSafe(UpgradeIconRoot + id + ".png");
		if (icon == null)
			GD.PushWarning($"MainMenu: no Arcane Upgrade icon at {UpgradeIconRoot}{id}.png (tools/art/upgrade_icons.py)");
		box.AddChild(new TextureRect
		{
			Texture = icon ?? StatIcons.Load("generic"),
			Position = new Vector2((SocketSize - IconSize) / 2f, (SocketSize - IconSize) / 2f),
			Size = new Vector2(IconSize, IconSize),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
		return box;
	}

	// A spell's own icon, bare on its page as the level-up cards draw it, in a box the socket's size
	// so the names of both kinds of card start on the same line.
	// A boon on an unlock card reads as a spell's does: its name, what it does, its tags and its icon
	// (BoonDefinition.IconPath, drawn by tools/art/boon_icons.py).
	private static SpellbookEntry BoonShopEntry(string boonId)
	{
		BoonDefinition boon = BoonCatalog.GetById(boonId);
		if (boon == null)
			return null;
		var entry = new SpellbookEntry
		{
			Id = boon.Id,
			DisplayName = boon.Name,
			Description = boon.Description,
			Icon = BonelightSkin.LoadTextureSafe(boon.IconPath),
		};
		foreach ((string element, int weight) in boon.ElementWeights)
		{
			if (Enum.TryParse(element, true, out Element parsed))
				entry.ElementList.Add((parsed, weight));
		}
		return entry;
	}

	private static Control BuildSpellIconBox(SpellbookEntry entry)
	{
		var box = new CenterContainer
		{
			CustomMinimumSize = new Vector2(SocketSize, SocketSize),
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		box.AddChild(new TextureRect
		{
			Texture = entry?.Icon ?? DefaultSpellIcon,
			CustomMinimumSize = new Vector2(IconSize, IconSize),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
		return box;
	}

	// The right end of the card's foot. One of three things shows there: the buy plate (with the
	// price, and "Buy" or how much is still needed), the mark of a finished entry (Mastered, Owned),
	// or a notice saying what opens it (Redaction on a small book).
	private Control BuildPlateSlot(ShopRow row, bool narrow)
	{
		var slot = new MarginContainer
		{
			CustomMinimumSize = new Vector2(PlateWidth(narrow), PlateHeight),
			SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		// Named Buy_<id> as it always was, so a harness can press one (_UiShot --press-late=Buy_damage;
		// _UiShot always runs with save writes off). Never a focus stop: the card is (BuildShopCard).
		var plate = new Button
		{
			Name = "Buy_" + row.Def.Id.Replace(":", "_"),
			CustomMinimumSize = new Vector2(PlateWidth(narrow), PlateHeight),
			FocusMode = Control.FocusModeEnum.None,
		};
		BonelightSkin.StyleButton(plate, BonelightSkin.Emphasis.Primary);
		string id = row.Def.Id;
		plate.Pressed += () => OnShopPlatePressed(id);
		slot.AddChild(plate);
		row.Plate = plate;

		// The words are a child of the button, not its text: a Button is not a container, so the
		// content can be anchored inside the iron band and shaken when a press is refused.
		var content = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		content.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		content.OffsetLeft = 8;
		content.OffsetRight = -8;
		content.OffsetTop = 10;
		content.OffsetBottom = -10;
		content.AddThemeConstantOverride("separation", -6);
		plate.AddChild(content);
		row.PlateContent = content;

		var price = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		price.AddThemeConstantOverride("separation", 6);
		row.PlateGlyph = Glyph(StatIcons.Load("energy"));
		row.PlateGlyph.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		price.AddChild(row.PlateGlyph);
		row.PlateCost = NoWrap(string.Empty, ResponsiveLayout.TextRole.Body, PlateCostInk);
		price.AddChild(row.PlateCost);
		content.AddChild(price);
		row.PlateAction = NoWrap(string.Empty, ResponsiveLayout.TextRole.Label, PlateActionInk);
		row.PlateAction.HorizontalAlignment = HorizontalAlignment.Center;
		content.AddChild(row.PlateAction);

		// Mastered / Owned: the done tick and the word, in the page's gold ink.
		var done = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.End,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false,
		};
		done.AddThemeConstantOverride("separation", 8);
		TextureRect tick = Glyph(StatIcons.Load("done"));
		tick.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		done.AddChild(tick);
		Label doneText = NoWrap(row.Def.IsSpellUnlock ? "Owned" : "Mastered", ResponsiveLayout.TextRole.Body, ShopInkGold);
		doneText.Name = "DoneText";
		doneText.VerticalAlignment = VerticalAlignment.Center;
		done.AddChild(doneText);
		slot.AddChild(done);
		row.DoneMark = done;

		// Not yet open: the lock glyph and what opens it, in the page's meta ink.
		var notice = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		notice.AddThemeConstantOverride("separation", 8);
		TextureRect lockGlyph = Glyph(StatIcons.Load("lock"));
		lockGlyph.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		notice.AddChild(lockGlyph);
		row.NoticeText = CodexRows.MakeText(string.Empty, ResponsiveLayout.TextRole.Label, ShopInkMeta);
		row.NoticeText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		notice.AddChild(row.NoticeText);
		slot.AddChild(notice);
		row.Notice = notice;
		return slot;
	}

	private static TextureRect Glyph(Texture2D texture)
	{
		// The stat glyphs are 24 drawn (12 at x2): shown at their own size, never scaled.
		return new TextureRect
		{
			Texture = texture,
			CustomMinimumSize = new Vector2(StatIcons.IconSize, StatIcons.IconSize),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
	}

	private static Label NoWrap(string text, ResponsiveLayout.TextRole role, Color ink)
	{
		Label label = CodexRows.MakeText(text, role, ink);
		label.AutowrapMode = TextServer.AutowrapMode.Off;
		return label;
	}

	// --- state -----------------------------------------------------------------------------------

	private ShopRowState StateOf(SaveManager saveManager, UpgradeDefinition def)
	{
		int level = GetShopItemLevel(saveManager, def);
		int maxLevel = GetShopItemMaxLevel(saveManager, def);
		// A pool-scaled row at zero capacity is not "maxed", it is not yet available - saying
		// Mastered there would read as "you have finished this" rather than "grow your book first".
		if (def.HasPoolScaledMaxLevel && maxLevel <= 0)
			return ShopRowState.NotYetOpen;
		if (level >= maxLevel)
			return def.IsSpellUnlock ? ShopRowState.Owned : ShopRowState.Mastered;
		return saveManager.Data.TotalCurrency < GetShopItemCost(def, level) ? ShopRowState.TooDear : ShopRowState.Buyable;
	}

	/// <summary>
	/// Brings every card in line with the save: its level, its price, whether it can be bought now,
	/// whether it is finished. In place - no card is rebuilt, so nothing the player is looking at
	/// moves and the selection never leaves the card it is on.
	/// </summary>
	private void RefreshUpgradeControls()
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null)
			return;

		int total = saveManager.Data.TotalCurrency;
		foreach (ShopRow row in shopRows.Values)
		{
			UpgradeDefinition def = row.Def;
			int level = GetShopItemLevel(saveManager, def);
			int maxLevel = GetShopItemMaxLevel(saveManager, def);
			int cost = GetShopItemCost(def, level);
			ShopRowState state = StateOf(saveManager, def);
			bool finished = state is ShopRowState.Mastered or ShopRowState.Owned;
			bool buyable = state == ShopRowState.Buyable;
			row.State = state;

			// The card keeps its stop whatever its state, so the selection never has to move when a
			// purchase changes what the card can do.
			row.Plate.Visible = !finished && state != ShopRowState.NotYetOpen;
			row.Plate.Disabled = !buyable;
			row.Plate.MouseDefaultCursorShape = buyable ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow;
			row.PlateCost.Text = FormatEnergy(cost);
			row.PlateCost.AddThemeColorOverride("font_color", buyable ? PlateCostInk : PlateDimInk);
			row.PlateGlyph.Modulate = buyable ? Colors.White : new Color(1f, 1f, 1f, 0.55f);
			row.PlateAction.Text = buyable
				? (def.IsSpellUnlock ? "Unlock" : "Buy")
				: $"Need {FormatEnergy(Math.Max(0, cost - total))} more";
			row.PlateAction.AddThemeColorOverride("font_color", buyable ? PlateActionInk : PlateDimInk);
			row.DoneMark.Visible = finished;
			row.Notice.Visible = state == ShopRowState.NotYetOpen;
			row.NoticeText.Text = "Locked";

			if (!def.IsSpellUnlock)
			{
				int shownLevel = Math.Clamp(level, 0, Math.Max(0, maxLevel));
				(_, string now) = UpgradeStat(def.Id, shownLevel);
				row.StatNow.Text = now;
				row.StatNext.Visible = !finished && state != ShopRowState.NotYetOpen;
				row.StatNext.Text = "→ " + UpgradeStat(def.Id, shownLevel + 1).Value;
				// Redaction's ceiling grows with the book; until the book is big enough there is none.
				row.LevelText.Text = maxLevel > 0
					? $"Level {shownLevel}/{maxLevel}"
					: $"Opens at {GlobalStatsManager.MinimumOfferablePool + 1} spells";

				int count = Math.Max(0, maxLevel);
				bool tall = count > 10;
				row.Pips.Visible = count > 0;
				row.Pips.Count = count;
				row.Pips.Filled = shownLevel;
				// The next pip breathes only when it can be bought now - a still socket is "not yet".
				row.Pips.Becoming = buyable ? shownLevel : -1;
				row.Pips.Pip = tall ? 10f : LevelPips.PipSize;
				row.Pips.Spacing = LevelPips.Gap;
				row.Pips.PerRow = tall ? 10 : 0;
				row.Pips.Configure();

				// The mastered page is the upgrade page gilded, and the seal is on it.
				row.Card.SetFrames(finished ? shopMasteredFrame : shopUpgradeFrame, finished ? shopMasteredFrameLit : shopUpgradeFrameLit);
				row.Seal?.SetShown(finished);
			}
		}

		foreach ((Label tally, string[] ids, bool spells) in shopTallies)
		{
			int done = ids.Count(id => shopRows.TryGetValue(id, out ShopRow r) && r.State is ShopRowState.Mastered or ShopRowState.Owned);
			tally.Text = spells ? $"{done} of {ids.Length} owned" : $"{done} of {ids.Length} mastered";
		}
	}

	// --- buying ----------------------------------------------------------------------------------

	private void OnShopPlatePressed(string upgradeId)
	{
		if (!shopRows.TryGetValue(upgradeId, out ShopRow row))
			return;
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null)
			return;

		int before = saveManager.Data.TotalCurrency;
		int levelBefore = GetShopItemLevel(saveManager, row.Def);
		if (!TryPurchaseUpgrade(upgradeId))
		{
			Refuse(row);
			return;
		}

		SfxPlayer.Global(SfxCatalog.UiClick);
		AnimatePurse(before, saveManager.Data.TotalCurrency);
		row.Flare.Burst(row.IconBox.GetGlobalRect().GetCenter() - row.Card.GetGlobalRect().Position);
		row.Pips?.Celebrate(levelBefore);
		if (row.State == ShopRowState.Mastered)
			row.Seal?.Stamp();
	}

	// A or Enter on a card: buys it if it can be bought, which is exactly what its plate does;
	// refused out loud if it is short of energy or not open yet; nothing if it is finished - there is
	// nothing left to want.
	private void OnShopCardPressed(string upgradeId)
	{
		if (!shopRows.TryGetValue(upgradeId, out ShopRow row))
			return;
		if (row.State == ShopRowState.Buyable)
			OnShopPlatePressed(upgradeId);
		else if (row.State is ShopRowState.TooDear or ShopRowState.NotYetOpen)
			Refuse(row);
	}

	// ui_denied (.ai/audio-manifest.md: "a locked stage or unaffordable arcane upgrade"), the plate's
	// words shaken a few whole pixels, and the purse flushed - the thing that is short.
	private void Refuse(ShopRow row)
	{
		SfxPlayer.Global(SfxCatalog.UiDenied);
		FlashPurse();
		Control content = row.Plate.Visible ? row.PlateContent : row.Notice;
		Tween shake = content.CreateTween();
		float x = content == row.PlateContent ? 8f : 0f;
		foreach (float dx in new[] { -6f, 6f, -4f, 3f, 0f })
			shake.TweenProperty(content, "position:x", x + dx, 0.04f);
	}

	private bool TryPurchaseUpgrade(string upgradeId)
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null)
			return false;

		if (!upgradeDefinitions.TryGetValue(upgradeId, out UpgradeDefinition def))
			return false;

		int currentLevel = GetShopItemLevel(saveManager, def);
		if (currentLevel >= GetShopItemMaxLevel(saveManager, def))
			return false;

		int cost = GetShopItemCost(def, currentLevel);
		if (saveManager.Data.TotalCurrency < cost)
			return false;

		saveManager.Data.TotalCurrency -= cost;
		if (def.IsBoon)
		{
			GlobalStatsManager.UnlockBoon(saveManager.Data, def.BoonId);
		}
		else if (def.IsSpellUnlock)
		{
			GlobalStatsManager.UnlockSpell(saveManager.Data, def.SpellId);
		}
		else
		{
			saveManager.Data.ArcaneUpgradeLevels[upgradeId] = currentLevel + 1;
		}
		saveManager.SaveGame();

		RefreshArcaneEnergy();
		RefreshUpgradeControls();
		RefreshSpellbookCards();
		return true;
	}

	// The list opens at its top with the first card selected, and the arrival does not scroll it
	// (.ai/controller-and-focus.md: "Initial focus must not scroll the list"). Deferred: the cards
	// were built this frame and have no rects yet.
	private void OpenShopAtTop()
	{
		upgradeScroll.ScrollVertical = 0;
		Callable.From(() =>
		{
			if (!arcaneUpgradesPanel.Visible)
				return;
			upgradeScroll.ScrollVertical = 0;
			Control first = shopRows.Values.Select(r => (Control)r.Card.FocusStop).FirstOrDefault(c => c.IsVisibleInTree());
			if (first == null)
				return;
			bool follow = upgradeScroll.FollowFocus;
			upgradeScroll.FollowFocus = false;
			first.GrabFocus();
			upgradeScroll.FollowFocus = follow;
		}).CallDeferred();
	}

	// --- what an upgrade does, as a stat line -----------------------------------------------------

	/// <summary>
	/// The stat an upgrade moves and its value at <paramref name="level"/>, from the same formulas
	/// Player.ApplyArcaneUpgrades applies them with: change one, change both.
	/// </summary>
	private static (string Label, string Value) UpgradeStat(string id, int level)
	{
		int l = Math.Max(0, level);
		static string Times(float v) => $"x{v:0.00}";
		return id switch
		{
			"damage" => ("Spell damage", Times(1f + l * 0.08f)),
			"recovery" => ("Regen", $"{l * 0.4f:0.0} HP/sec"),
			"cooldowns" => ("Cooldowns", Times(MathF.Max(0.35f, 1f - l * 0.05f))),
			"area" => ("Area and range", Times(1f + l * 0.08f)),
			"attack_speed" => ("Attack speed", Times(1f + l * 0.06f)),
			"duration" => ("Effect duration", Times(1f + l * 0.10f)),
			"amount" => ("Projectiles", $"+{l}"),
			"movespeed" => ("Move speed", Times(1f + l * 0.05f)),
			"magnet" => ("Pickup radius", $"+{l * 20}"),
			"growth" => ("XP gain", Times(1f + l * 0.10f)),
			"greed" => ("Arcane Energy", Times(1f + l * 0.10f)),
			"extra_lives" => ("Revives per run", $"{l}"),
			"rerolls" => ("Rerolls per level-up", $"{l}"),
			"bans" => ("Bans per run", $"{l}"),
			"banked_levels" => ("Saves per run", $"{l}"),
			"auguries" => ("Auguries per run", $"{l}"),
			"vitality" => ("Max HP", $"+{l * 5}"),
			"luck" => ("Luck", $"+{l}"),
			"crit_chance" => ("Crit chance", $"+{Math.Min(75, l * 3)}%"),
			GlobalStatsManager.CurationUpgradeId => ("Spells set aside", $"{l}"),
			_ => ("Level", $"{l}"),
		};
	}

	/// <summary>The stat glyph a card's stat line leads with (tools/art/stat_icons.py).</summary>
	private static string UpgradeGlyph(string id) => id switch
	{
		"damage" => "damage",
		"recovery" => "regen",
		"cooldowns" => "cooldown",
		"area" => "area",
		"attack_speed" => "cooldown",
		"duration" => "duration",
		"amount" => "projectiles",
		"movespeed" => "speed",
		"magnet" => "magnet",
		"growth" => "xp",
		"greed" => "energy",
		"extra_lives" => "heart",
		"vitality" => "heart",
		"luck" => "luck",
		"crit_chance" => "crit",
		_ => "generic",
	};
}
