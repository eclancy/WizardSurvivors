using Godot;
using WizardSurvivors.scripts;

// THE PAGE OF A SPELL'S FORMS (.ai/decisions/2026-10-28-spell-variants.md; .ai/level-up-screen.md, "The
// page of forms"). A spell with forms (SpellData.Variants - Spiritual Weapon's axe, hammer and sword) is
// dealt as ONE card, its own; taking it does not choose, it turns the hand into a card per form, and taking
// one of those chooses the spell in that form for the rest of the run.
//
// It is the level-up screen's own hand, not an overlay: the forms are new spells being taken, so they are
// the new spell's torn page (CardFamily.NewSpell) laid out by the same BuildWideOptionCard and
// BuildNarrowOptionCard as any hand - on a PC side by side, on a phone stacked in the scroll - each with
// its own icon, numbers, what it is good at (SpellVariant.Traits) and its tag chips. Around them the
// screen changes only what would be wrong on this page: the heading asks the spell's question
// (SpellData.VariantPrompt), one line under it says the choice holds, and the footer's Reroll, Skip and
// charges, the Ascension strip and the Ascensions button give way to one Back, which returns to the hand
// as it was dealt. A form is never skipped or rerolled from here: Back, then Skip or Reroll, does both.
//
// The rules every page that swaps the cards under the pointer keeps: the arming pause starts again
// (RearmForNewChoices, so a double click cannot take a form unread), the navigator is re-attached with the
// first form selected (BuildButtonsFrom), and the panel keeps the safe-area insets it already has.
public partial class LevelUpMenu
{
	private LevelUpOption pendingVariantOption;
	private const string VariantNoteName = "VariantNote";
	private const string VariantBackName = "VariantBack";

	/// <summary>True while the page of a spell's forms is up.</summary>
	public bool ShowingVariantPage => pendingVariantOption != null;

	/// <summary>The forms on the page, while it is up; empty otherwise. Read by the tools that drive it.</summary>
	public System.Collections.Generic.IReadOnlyList<LevelUpOption> GetVariantOptions() =>
		pendingVariantOption?.Variants ?? (System.Collections.Generic.IReadOnlyList<LevelUpOption>)System.Array.Empty<LevelUpOption>();

	private void BuildVariantSelectionButtons(LevelUpOption option, bool rearm = true)
	{
		if (option == null || !option.OffersVariants)
			return;
		pendingVariantOption = option;
		if (rearm)
			RearmForNewChoices();
		BuildButtonsFrom(option.Variants);
		ShowVariantChrome(option);
		UpdateChargeRow();
	}

	private void ShowVariantChrome(LevelUpOption option)
	{
		bool narrow = ResponsiveLayout.IsNarrow(this);
		var column = GetNodeOrNull<Control>("Panel/VBoxContainer");
		var heading = GetNodeOrNull<Label>("Panel/VBoxContainer/Label");
		if (heading != null)
		{
			heading.Text = option.VariantPrompt;
			// The Ascensions button that owns the right of a phone's heading row is not on this page.
			heading.HorizontalAlignment = narrow ? HorizontalAlignment.Left : HorizontalAlignment.Center;
		}

		if (column != null)
		{
			if (column.GetNodeOrNull<Control>(AscendStripName) is Control strip)
				strip.Visible = false;
			if (column.GetNodeOrNull<Label>(VariantNoteName) == null)
			{
				// Bone on the plate's dark stone, as the heading is on its band (HeadingInk, 17:1 on the band).
				Label note = CodexRows.MakeText($"{option.DisplayName} keeps the form you take for the rest of this run.",
					ResponsiveLayout.TextRole.Label, HeadingInk);
				note.Name = VariantNoteName;
				note.HorizontalAlignment = narrow ? HorizontalAlignment.Left : HorizontalAlignment.Center;
				note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				note.MouseFilter = Control.MouseFilterEnum.Ignore;
				column.AddChild(note);
				if (heading != null && heading.GetParent() == column)
					column.MoveChild(note, heading.GetIndex() + 1);
			}
		}

		if (chargeRow != null)
			chargeRow.Visible = false;
		if (rerollButton != null)
			rerollButton.Visible = false;
		if (skipButton != null)
			skipButton.Visible = false;
		if (ascensionButton != null && IsInstanceValid(ascensionButton))
			ascensionButton.Visible = false;

		// One Back where Reroll and Skip were, in the same stone.
		if (skipButton?.GetParent() is Control row && row.GetNodeOrNull<Button>(VariantBackName) == null)
		{
			var back = new Button
			{
				Name = VariantBackName,
				Text = "Back",
				CustomMinimumSize = new Vector2(narrow ? 0f : 180f, ResponsiveLayout.MinTouchTarget),
				SizeFlagsHorizontal = narrow ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkCenter,
			};
			ResponsiveLayout.SetFont(back, ResponsiveLayout.TextRole.Label);
			BonelightSkin.StyleButton(back);
			ApplyFrameToFooterButton(back);
			back.Pressed += LeaveVariantPage;
			row.AddChild(back);
		}
	}

	private void LeaveVariantPage()
	{
		SfxPlayer.Global(SfxCatalog.UiBack);
		HideVariantChrome();
		BuildButtonsFrom(currentOptions);
		UpdateRerollState(currentRerollsRemaining);
		UpdateChargeRow();
		RefreshAscensionReadiness();
	}

	// Puts the screen back as a hand: the heading, the footer and the strip. The cards are the caller's.
	private void HideVariantChrome()
	{
		pendingVariantOption = null;
		StyleHeading();
		var column = GetNodeOrNull<Control>("Panel/VBoxContainer");
		if (column?.GetNodeOrNull<Control>(AscendStripName) is Control strip)
			strip.Visible = true;
		// Renamed before freeing, as RemoveEvolutionRoot does: a rebuild in the same frame must not find it.
		if (column?.GetNodeOrNull<Control>(VariantNoteName) is Control note)
		{
			note.Name = VariantNoteName + "_freeing";
			note.QueueFree();
		}
		if (skipButton?.GetParent()?.GetNodeOrNull<Button>(VariantBackName) is Button back)
		{
			back.Name = VariantBackName + "_freeing";
			back.QueueFree();
		}
		if (rerollButton != null)
			rerollButton.Visible = true;
		if (skipButton != null)
			skipButton.Visible = true;
	}
}
