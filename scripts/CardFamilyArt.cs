using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// Which kind of choice a card is, and the object it comes in. One family per kind, so the kind is
/// recognised by shape and material before a word is read.
/// </summary>
/// <remarks>
/// From the Pixel 9: "learning new spells should be the only thing colored like a spell page.
/// levelling up spells, and mutations, etc should all look different." Until then an upgrade was
/// the new spell's torn page with a gold rule, and the level 4 and level 8 choices were vellum cards
/// on a vellum page. See .ai/level-up-screen.md, "The card families", and
/// tools/art/ui_frames.py, where every frame here is drawn.
/// </remarks>
public enum CardFamily
{
	/// <summary>A spell not yet in the book: the torn page. The only thing that is.</summary>
	NewSpell,
	/// <summary>A level for a spell already held, or Marginalia: the red silk bookmark ribbon.</summary>
	Upgrade,
	/// <summary>A boon: the violet reliquary plaque (BoonAura, the bail).</summary>
	Boon,
	/// <summary>The level 4 branch choice, and the card that opens it: white vellum, teal band, wax seal.</summary>
	Mutation,
	/// <summary>The level 8 Ascension: an obsidian plate in a gilded rune band.</summary>
	Ascension,
	/// <summary>A wizard's final form: the same plate, its runes in the Fire ramp.</summary>
	Apotheosis,
}

/// <summary>
/// The frames, overlays and inks of the families that are not the page or the boon. Shared by the
/// level-up screen and the chest's Penknife picker (a held spell is a ribbon wherever it is chosen).
/// </summary>
/// <remarks>
/// NOTHING HERE CACHES A RESOURCE IN A STATIC. A StyleBox or Texture2D held by a static outlives the
/// engine's own reference and trips `gchandle.is_released()` at teardown (LevelUpMenu, the comment
/// on CardTexturePath). The builders return a new StyleBox per call; each menu keeps its own in
/// instance fields. Colours are structs and are safe as statics.
///
/// EVERY INK IS MEASURED against the frame as drawn, in tools/art/ui_contrast.py, which keeps a copy
/// of each value: change one here, change it there.
/// </remarks>
public static class CardFamilyArt
{
	private const string Ui = "res://assets/bonelight/ui/";
	public const string RibbonPath = Ui + "ui-ribbon-slip.png";
	public const string RibbonLitPath = Ui + "ui-ribbon-slip-lit.png";
	public const string RibbonTailPath = Ui + "ui-ribbon-tail.png";
	public const string VellumPath = Ui + "ui-sealed-vellum.png";
	public const string VellumLitPath = Ui + "ui-sealed-vellum-lit.png";
	public const string WaxSealPath = Ui + "ui-wax-seal.png";
	public const string RoundelPath = Ui + "ui-mutation-roundel.png";
	public const string ObsidianPath = Ui + "ui-obsidian-plate.png";
	public const string ObsidianLitPath = Ui + "ui-obsidian-plate-lit.png";
	public const string EmberPath = Ui + "ui-ember-plate.png";
	public const string EmberLitPath = Ui + "ui-ember-plate-lit.png";

	// Every family frame is 48 authored (96 drawn) with a 12-pixel corner block - 24 drawn.
	public const int PatchMargin = 24;

	// --- the faces, as drawn (the palette tone at the centre of each frame) -----------------------
	/// <summary>red lit #6A2028 - the ribbon.</summary>
	public static readonly Color RibbonFace = new Color(0.416f, 0.125f, 0.157f);
	/// <summary>bone hi #EEF2FA - the sealed vellum.</summary>
	public static readonly Color VellumFace = new Color(0.933f, 0.949f, 0.980f);
	/// <summary>arcane deep #0B2436 - the obsidian plate.</summary>
	public static readonly Color ObsidianFace = new Color(0.043f, 0.141f, 0.212f);
	/// <summary>red deep #16070A - the ember plate.</summary>
	public static readonly Color EmberFace = new Color(0.086f, 0.027f, 0.039f);

	// --- the inks ------------------------------------------------------------------------------
	// THE RIBBON writes in light, like the boon: gold for the name, bone for what the level does.
	//   UpgradeTitle gold hi  #FFF0C0  10.0:1 on the ribbon
	//   UpgradeInk   bone hi  #EEF2FA  10.1:1
	//   UpgradeSoft  gold lit #D8B04A   5.5:1  - "Level up · Attack"
	public static readonly Color UpgradeTitle = new Color(1.0f, 0.941f, 0.753f);
	public static readonly Color UpgradeInk = new Color(0.933f, 0.949f, 0.980f);
	public static readonly Color UpgradeSoft = new Color(0.847f, 0.690f, 0.290f);
	/// <summary>The rule round the deltas: gold base, decoration only.</summary>
	public static readonly Color UpgradeRule = new Color(0.541f, 0.416f, 0.118f);

	// THE SEALED VELLUM writes in the page's dark inks - on a whiter, cooler skin, so every one of
	// them gains: Ink 12.4:1, InkSoft 11.6:1, Rubric 10.1:1, InkGold 8.9:1 (they were 10.6, 8.8,
	// 7.4 and 7.5 on the parchment). The synergy tag is a solid teal chip with bone ink, 8.0:1.
	public static readonly Color MutationChip = new Color(0.165f, 0.306f, 0.345f);   // lichen base #2A4E58
	/// <summary>lichen deep #16282E: the mutation heading on the page, 11.4:1.</summary>
	public static readonly Color MutationHeading = new Color(0.086f, 0.157f, 0.180f);

	// THE OBSIDIAN PLATE: gold hi for the name (14.0:1), bone hi for what it does (14.2:1), arcane lit
	// for the quiet lines (7.3:1), gold lit for the advice (7.8:1). Tag chip: arcane shade, 9.6:1.
	public static readonly Color AscensionTitle = new Color(1.0f, 0.941f, 0.753f);
	public static readonly Color AscensionInk = new Color(0.933f, 0.949f, 0.980f);
	public static readonly Color AscensionSoft = new Color(0.416f, 0.722f, 0.910f);  // arcane lit #6AB8E8
	public static readonly Color AscensionAdvice = new Color(0.847f, 0.690f, 0.290f); // gold lit
	public static readonly Color AscensionChip = new Color(0.090f, 0.255f, 0.369f);  // arcane shade #17415E
	/// <summary>arcane deep: the Ascension heading on the page, 12.0:1.</summary>
	public static readonly Color AscensionHeading = new Color(0.043f, 0.141f, 0.212f);

	// THE EMBER PLATE: Fire hot for the name (12.8:1), bone hi (17.6:1), Fire mid for the quiet lines
	// (6.8:1), gold lit for the advice (9.6:1). Tag chip: red shade.
	public static readonly Color ApotheosisTitle = new Color(1.0f, 0.784f, 0.282f);  // fire hot #FFC848
	public static readonly Color ApotheosisSoft = new Color(0.910f, 0.478f, 0.102f); // fire mid #E87A1A
	public static readonly Color ApotheosisChip = new Color(0.165f, 0.055f, 0.078f); // red shade #2A0E14
	/// <summary>red lit: the Apotheosis heading on the page, 8.5:1.</summary>
	public static readonly Color ApotheosisHeading = new Color(0.416f, 0.125f, 0.157f);

	/// <summary>Light ink for a chip filled with one of the dark chip colours above (bone hi).</summary>
	public static readonly Color ChipInk = new Color(0.933f, 0.949f, 0.980f);

	// The stamp across a locked branch: bone hi on a red lit band, 10.1:1, so it reads on the white
	// vellum and the black glass alike. It was Rubric straight onto the card, which on a dark plate
	// would be red on near-black.
	public static readonly Color LockBand = new Color(0.416f, 0.125f, 0.157f);
	public static readonly Color LockInk = new Color(0.933f, 0.949f, 0.980f);

	/// <summary>The face a family's text sits on, for anything that blends over it (chips).</summary>
	public static Color FaceOf(CardFamily family) => family switch
	{
		CardFamily.Upgrade => RibbonFace,
		CardFamily.Boon => new Color(0.133f, 0.110f, 0.200f),
		CardFamily.Mutation => VellumFace,
		CardFamily.Ascension => ObsidianFace,
		CardFamily.Apotheosis => EmberFace,
		_ => new Color(0.949f, 0.863f, 0.769f),
	};

	/// <summary>Whether the family's face is dark, so it writes in light inks.</summary>
	public static bool IsDark(CardFamily family) => family is CardFamily.Upgrade or CardFamily.Boon
		or CardFamily.Ascension or CardFamily.Apotheosis;

	/// <summary>
	/// A family frame as a tiled nine-patch. Tiled for the torn page's reason: the bands carry
	/// stitching, a vine and runes, which a stretch would smear (tools/art/ui_frames.py).
	/// </summary>
	public static StyleBoxTexture Frame(CardFamily family, bool lit, int contentMargin)
	{
		string path = family switch
		{
			CardFamily.Upgrade => lit ? RibbonLitPath : RibbonPath,
			CardFamily.Mutation => lit ? VellumLitPath : VellumPath,
			CardFamily.Apotheosis => lit ? EmberLitPath : EmberPath,
			_ => lit ? ObsidianLitPath : ObsidianPath,
		};
		var style = new StyleBoxTexture
		{
			Texture = GD.Load<Texture2D>(path),
			AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile,
			AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Tile,
		};
		style.SetTextureMarginAll(PatchMargin);
		style.SetContentMarginAll(contentMargin);
		return style;
	}

	/// <summary>The four states of a card button in a family: rest on the frame, lift to the lit one.</summary>
	public static void ApplyFrame(Button card, StyleBoxTexture resting, StyleBoxTexture lit)
	{
		card.AddThemeStyleboxOverride("normal", resting);
		card.AddThemeStyleboxOverride("disabled", resting);
		card.AddThemeStyleboxOverride("hover", lit);
		card.AddThemeStyleboxOverride("pressed", lit);
		card.AddThemeStyleboxOverride("focus", lit);
	}

	/// <summary>
	/// The swallowtail end of the bookmark, hung from the foot of an upgrade card and drawn behind it
	/// so it reads as the ribbon running on out of the book. 32x18 at its authored x2; 8 of it sits
	/// under the card, 10 shows - less than the 12 between stacked cards on a phone.
	/// </summary>
	public static void AddRibbonTail(Control card)
	{
		if (card.GetNodeOrNull("RibbonTail") != null)
			return;
		card.AddChild(new TextureRect
		{
			Name = "RibbonTail",
			Texture = GD.Load<Texture2D>(RibbonTailPath),
			StretchMode = TextureRect.StretchModeEnum.Keep,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ShowBehindParent = true,
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 1f,
			AnchorBottom = 1f,
			OffsetLeft = -16,
			OffsetRight = 16,
			OffsetTop = -8,
			OffsetBottom = 10,
		});
	}

	/// <summary>
	/// The wax seal over a mutation card's top right corner, pressed over the band and the corner
	/// knot. 30x30 at its authored x2, never scaled; it rises 12 above the card, like the boon's bail,
	/// and reaches only 4 pixels past the band into the field, so a full-width name clears it.
	/// </summary>
	public static void AddWaxSeal(Control card)
	{
		if (card.GetNodeOrNull("WaxSeal") != null)
			return;
		card.AddChild(new TextureRect
		{
			Name = "WaxSeal",
			Texture = GD.Load<Texture2D>(WaxSealPath),
			StretchMode = TextureRect.StretchModeEnum.Keep,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 1f,
			AnchorRight = 1f,
			OffsetLeft = -34,
			OffsetRight = -4,
			OffsetTop = -12,
			OffsetBottom = 18,
		});
	}

	/// <summary>
	/// The illuminated-initial window a mutation's icon sits in: dark teal glass in a gold rim, so a
	/// pale branch tint still has a dark ground on white vellum. Stretched: flat centre, bands
	/// parallel to their edges.
	/// </summary>
	public static StyleBoxTexture Roundel()
	{
		var style = new StyleBoxTexture { Texture = GD.Load<Texture2D>(RoundelPath) };
		style.SetTextureMarginAll(12);
		style.SetContentMarginAll(8);
		return style;
	}

	/// <summary>A solid chip in a family's dark chip colour: a synergy tag on a mutation or ascension.</summary>
	public static StyleBoxFlat Chip(Color fill)
	{
		var style = new StyleBoxFlat { BgColor = fill };
		style.SetCornerRadiusAll(4);
		style.ContentMarginLeft = 8;
		style.ContentMarginRight = 8;
		style.ContentMarginTop = 2;
		style.ContentMarginBottom = 2;
		return style;
	}
}
