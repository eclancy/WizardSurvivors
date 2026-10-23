using Godot;
using WizardSurvivors.scripts;

// The card families on the level-up screen: how each kind of choice is marked, and the inks written
// on it. See CardFamilyArt for the frames and .ai/level-up-screen.md, "The card families".
public partial class LevelUpMenu
{
	/// <summary>The family a level-up card belongs to.</summary>
	/// <remarks>
	/// Every spell card is the torn page; the family says how the scribe marked it. A level for a held
	/// spell - and Marginalia, which improves what the player has rather than adding to it - is
	/// rubricated. The card that opens the level 4 mutation is already the mutation's stained, glowing
	/// page, so the ceremony starts on the card.
	/// </remarks>
	private static CardFamily FamilyOf(LevelUpOption option)
	{
		if (option.IsBoon)
			return CardFamily.Boon;
		if (option.IsEvolutionMilestone && option.MilestoneLevel >= ApotheosisCatalog.MilestoneLevel)
			return CardFamily.Apotheosis;
		if (option.IsEvolutionMilestone && option.MilestoneLevel >= 8)
			return CardFamily.Ascension;
		if (option.IsEvolutionMilestone && option.MilestoneLevel >= 4)
			return CardFamily.Mutation;
		if (!option.IsNewUnlock || option.SpellId == Player.MarginaliaId)
			return CardFamily.Upgrade;
		return CardFamily.NewSpell;
	}

	/// <summary>The family of an evolution page's branch cards: the milestone's own.</summary>
	private static CardFamily EvolutionFamilyOf(LevelUpOption option)
	{
		if (option.MilestoneLevel >= ApotheosisCatalog.MilestoneLevel)
			return CardFamily.Apotheosis;
		return option.MilestoneLevel >= 8 ? CardFamily.Ascension : CardFamily.Mutation;
	}

	// The four inks each family writes in. Every page - new, upgrade, mutation - writes in the dark
	// parchment inks; the plaque and the two plates write in light. Measured in
	// tools/art/ui_contrast.py.
	private static Color TitleInk(CardFamily family) => family switch
	{
		CardFamily.Boon => BoonInkGold,
		CardFamily.Ascension => CardFamilyArt.AscensionTitle,
		CardFamily.Apotheosis => CardFamilyArt.ApotheosisTitle,
		_ => Ink,
	};

	private static Color SoftInk(CardFamily family) => family switch
	{
		CardFamily.Boon => BoonInkGoldSoft,
		CardFamily.Ascension => CardFamilyArt.AscensionSoft,
		CardFamily.Apotheosis => CardFamilyArt.ApotheosisSoft,
		_ => InkSoft,
	};

	private static Color BodyInk(CardFamily family) => family switch
	{
		CardFamily.Boon => BoonInk,
		CardFamily.Ascension or CardFamily.Apotheosis => CardFamilyArt.AscensionInk,
		_ => Ink,
	};

	/// <summary>The line that matters most: a level's deltas, a branch's advice. On a page it is
	/// the scribe's red, which on the upgrade page is the colour of its rubric too.</summary>
	private static Color AccentInk(CardFamily family) => family switch
	{
		CardFamily.Boon => BoonInk,
		CardFamily.Ascension or CardFamily.Apotheosis => CardFamilyArt.AscensionAdvice,
		_ => Rubric,
	};

	/// <summary>The solid chip a synergy tag sits in on a branch card.</summary>
	private static Color TagChip(CardFamily family) => family switch
	{
		CardFamily.Ascension => CardFamilyArt.AscensionChip,
		CardFamily.Apotheosis => CardFamilyArt.ApotheosisChip,
		_ => CardFamilyArt.MutationChip,
	};

	/// <summary>The evolution page's heading, in the family's darkest tone, on the vellum page.</summary>
	private static Color HeadingInkFor(CardFamily family) => family switch
	{
		CardFamily.Ascension => CardFamilyArt.AscensionHeading,
		CardFamily.Apotheosis => CardFamilyArt.ApotheosisHeading,
		_ => CardFamilyArt.MutationHeading,
	};

	// The family frames, one resting and one lit per family, built once per menu and shared by every
	// card (instance fields, never static - see EnsureCardStyles). Two content margins: the level-up
	// card's and the evolution card's, which has always sat a little tighter.
	private StyleBoxTexture upgradeResting, upgradeLit;
	private StyleBoxTexture mutationResting, mutationLit;
	private StyleBoxTexture obsidianResting, obsidianLit;
	private StyleBoxTexture emberResting, emberLit;
	private StyleBoxTexture evoMutationResting, evoMutationLit;
	private StyleBoxTexture evoObsidianResting, evoObsidianLit;
	private StyleBoxTexture evoEmberResting, evoEmberLit;
	private StyleBoxTexture stripObsidianResting, stripObsidianLit;
	private StyleBoxTexture stripEmberResting, stripEmberLit;
	private StyleBoxTexture roundelStyle;

	private void EnsureFamilyStyles()
	{
		upgradeResting ??= CardFamilyArt.Frame(CardFamily.Upgrade, false, CardContentMargin);
		upgradeLit ??= CardFamilyArt.Frame(CardFamily.Upgrade, true, CardContentMargin);
		mutationResting ??= CardFamilyArt.Frame(CardFamily.Mutation, false, CardContentMargin);
		mutationLit ??= CardFamilyArt.Frame(CardFamily.Mutation, true, CardContentMargin);
		obsidianResting ??= CardFamilyArt.Frame(CardFamily.Ascension, false, CardContentMargin);
		obsidianLit ??= CardFamilyArt.Frame(CardFamily.Ascension, true, CardContentMargin);
		emberResting ??= CardFamilyArt.Frame(CardFamily.Apotheosis, false, CardContentMargin);
		emberLit ??= CardFamilyArt.Frame(CardFamily.Apotheosis, true, CardContentMargin);
		evoMutationResting ??= CardFamilyArt.Frame(CardFamily.Mutation, false, 16);
		evoMutationLit ??= CardFamilyArt.Frame(CardFamily.Mutation, true, 16);
		evoObsidianResting ??= CardFamilyArt.Frame(CardFamily.Ascension, false, 16);
		evoObsidianLit ??= CardFamilyArt.Frame(CardFamily.Ascension, true, 16);
		evoEmberResting ??= CardFamilyArt.Frame(CardFamily.Apotheosis, false, 16);
		evoEmberLit ??= CardFamilyArt.Frame(CardFamily.Apotheosis, true, 16);
		// The strip button's words sit clear of the rune band and the gilt line inside it (18 drawn
		// pixels in): at 12, and at 16, its box took in the gilt and the photograph measured the
		// worst ground at 4.3:1. It makes the button about 7 taller than the stone one it replaced.
		stripObsidianResting ??= StripFrame(CardFamily.Ascension, false);
		stripObsidianLit ??= StripFrame(CardFamily.Ascension, true);
		stripEmberResting ??= StripFrame(CardFamily.Apotheosis, false);
		stripEmberLit ??= StripFrame(CardFamily.Apotheosis, true);
		roundelStyle ??= CardFamilyArt.Roundel();
	}

	private static StyleBoxTexture StripFrame(CardFamily family, bool lit)
	{
		StyleBoxTexture style = CardFamilyArt.Frame(family, lit, 18);
		style.ContentMarginLeft = 20;
		style.ContentMarginRight = 20;
		return style;
	}

	/// <summary>A level-up card's frame pair for a family that is not the plain page or the boon.</summary>
	private (StyleBoxTexture resting, StyleBoxTexture lit) FamilyFrames(CardFamily family)
	{
		EnsureFamilyStyles();
		return family switch
		{
			CardFamily.Upgrade => (upgradeResting, upgradeLit),
			CardFamily.Mutation => (mutationResting, mutationLit),
			CardFamily.Apotheosis => (emberResting, emberLit),
			_ => (obsidianResting, obsidianLit),
		};
	}

	/// <summary>An evolution page's branch card frames.</summary>
	private (StyleBoxTexture resting, StyleBoxTexture lit) EvolutionFrames(CardFamily family)
	{
		EnsureFamilyStyles();
		return family switch
		{
			CardFamily.Apotheosis => (evoEmberResting, evoEmberLit),
			CardFamily.Ascension => (evoObsidianResting, evoObsidianLit),
			_ => (evoMutationResting, evoMutationLit),
		};
	}

	/// <summary>A synergy tag as a solid chip in the family's dark tone, written in bone.</summary>
	private static Control BuildSynergyChip(string tag, CardFamily family, Control.SizeFlags horizontal)
	{
		var chip = new PanelContainer
		{
			SizeFlagsHorizontal = horizontal,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		chip.AddThemeStyleboxOverride("panel", CardFamilyArt.Chip(TagChip(family)));
		var label = new Label
		{
			Text = tag,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		// Label, not Micro: Micro is for numerals and chips, and these are words.
		ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Label);
		label.AddThemeColorOverride("font_color", CardFamilyArt.ChipInk);
		chip.AddChild(label);
		return chip;
	}

	/// <summary>
	/// A branch's icon. On the mutation page it sits in the teal roundel, an illuminated initial,
	/// because a branch's own tint is often pale and a pale blue missile on a light page measured
	/// under 2:1; on the plates it sits straight on the glass, which is already dark.
	/// </summary>
	private Control BuildEvolutionIcon(LevelUpOption option, SpellEvolutionOption evo, CardFamily family, float side)
	{
		// The branch's own tint where it has one; otherwise untinted - every ground under it is dark
		// now, so the deepening toward ink the old page needed would only muddy it.
		var icon = new TextureRect
		{
			Texture = evo.Icon ?? option.Icon ?? DefaultSpellIcon,
			CustomMinimumSize = new Vector2(side, side),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = evo.ModulateColor
		};
		if (family != CardFamily.Mutation)
			return icon;

		EnsureFamilyStyles();
		var roundel = new PanelContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		roundel.AddThemeStyleboxOverride("panel", roundelStyle);
		roundel.AddChild(icon);
		return roundel;
	}
}
