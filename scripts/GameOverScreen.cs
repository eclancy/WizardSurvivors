using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WizardSurvivors.scripts;

/// <summary>
/// The results screen at the end of every run, Defeat or Victory. See .ai/level-up-screen.md,
/// section 6.
/// </summary>
/// <remarks>
/// It was nine centred lines of one weight - "Enemies killed: 0", "Final loadout: Magic Missile Lv
/// 1, ...", "Arcane Energy earned: +28" - with one picture on it, the button. What the run EARNED
/// sat in the same ink as how long it lasted, the loadout was a sentence, and nothing said what
/// the boons, relics or the damage had been. Now it reads in the order a player asks:
///
///   1. What happened: Defeat or Victory, the chapter and wizard, and three headline numbers.
///   2. What I take away (Rewards): the Arcane Energy, counted up, with how it was counted, then one
///      card per thing unlocked - its picture, what kind of thing it is, how it was earned - under
///      a New seal.
///   3. What I built and how it went, behind two tabs (Your build, Stats), because together they
///      are longer than any screen.
///   4. Where to go: Retry, or the main menu.
///
/// On a PC the rewards and the tabs stand side by side, each scrolling on its own, so a typical
/// run fits a 1280x720 screen without scrolling at all. On a phone it is one column that scrolls
/// under a finger, with the buttons pinned below it.
///
/// It READS, it never grants. Everything on it was paid and saved by Node2DGame.FinishRunAndReward
/// before the screen was built (RunReport); the animations are dressing on numbers already banked,
/// and none of them holds up a button.
/// </remarks>
public partial class GameOverScreen : CanvasLayer
{
	// --- inks -----------------------------------------------------------------------------------
	// Everything on the screen writes in CodexRows.Stone, the register of the pause screen's lists,
	// so a row here is the same object as a row there. The few colours of its own are below, all
	// mirrored in tools/art/ui_contrast.py ("results screen") - change one, change both.
	private static CodexRows.Palette Stone => CodexRows.Stone;
	// The outcome words. Gold for a win, cloth red for a loss: the first thing read, so it carries
	// the result. On the heading band (stone deep) the red is 4.8:1 and the gold 14.2:1, at 34px.
	private static readonly Color VictoryInk = new(1.0f, 0.86f, 0.42f);
	private static readonly Color DefeatInk = new(0.85f, 0.32f, 0.34f);
	// LevelUpMenu.StyleHeading's band: the darkest stone, solid, a gold rule at its foot.
	private static readonly Color BandColour = new(0.043f, 0.059f, 0.094f);
	private static readonly Color BandRule = new(0.541f, 0.416f, 0.118f);
	// The milestone marks take the loadout strip's rims (LoadoutHud), so a spell wears the same
	// colour on the HUD and here: gold mutated, cyan ascended, fire at its Apotheosis. Solid, with
	// near-black words on them (9.3, 8.8 and 6.6:1).
	private static readonly Color MutationMark = new(0.847f, 0.690f, 0.290f);
	private static readonly Color AscensionMark = new(0.416f, 0.722f, 0.910f);
	private static readonly Color ApotheosisMark = new(0.910f, 0.478f, 0.102f);
	private static readonly Color HeldRim = new(0.416f, 0.494f, 0.596f);
	private static readonly Color MarkInk = new(0.06f, 0.06f, 0.07f);
	// A new unlock's card is edged in gold lit, two pixels, where an ordinary row has stone hi.
	private static readonly Color NewEdge = new(0.847f, 0.690f, 0.290f);
	// The icon well: stone deep, so a dark icon still has an edge against the row.
	private static readonly Color Well = new(0.043f, 0.059f, 0.094f);
	// Over the run: dark enough that the swarm stops competing, light enough to show the run ended
	// where it stands.
	private static readonly Color DimColour = new(0.02f, 0.03f, 0.05f, 0.80f);

	private const string RewardIconRoot = "res://assets/bonelight/ui/rewards/";
	private const string WaxSealPath = "res://assets/bonelight/ui/ui-wax-seal.png";
	private const string UnknownSpellIcon = "res://assets/bonelight/ui/spells/unknown.png";

	// A PC panel is capped so a wide monitor does not stretch a row of text across it.
	private const float MaxPanelWidth = 1200f;
	private const int SectionGap = 22;
	private const int RowGap = 10;

	// --- the shell, built once ----------------------------------------------------------------
	private PanelContainer panel;
	private VBoxContainer column;
	private VBoxContainer headerHost;
	private Control bodyHost;
	private Button continueButton;
	private Button retryButton;

	// --- per report -----------------------------------------------------------------------------
	private RunReport report;
	private readonly List<Control> revealed = new();
	private readonly List<Control> seals = new();
	private Label energyValue;
	private readonly List<Button> tabButtons = new();
	private readonly List<Control> tabPages = new();
	private int activeTab;
	private Tween revealTween;
	private bool narrow;

	public override void _Ready()
	{
		narrow = ResponsiveLayout.IsNarrow(this);
		BuildShell();
		SfxPlayer.Global(SfxCatalog.GameOver);

		// Clear of the phone's rounded corners and camera hole; nothing on desktop (SafeArea).
		SafeArea.Apply(this);
		// The selection opens on Main menu, as it opened on Continue before: A carries straight on
		// from a game over without the player putting the pad down to find the mouse.
		MenuNavigator.Attach(this, continueButton);
	}

	/// <summary>Builds the screen from a finished run. Safe to call again: it rebuilds.</summary>
	public void Present(RunReport runReport)
	{
		report = runReport;
		if (report?.Result == null || headerHost == null)
			return;

		Clear(headerHost);
		Clear(bodyHost);
		revealed.Clear();
		seals.Clear();
		tabButtons.Clear();
		tabPages.Clear();
		energyValue = null;

		BuildHeader();
		BuildBody();
		Reveal();

		// A rebuild after the first frame (a harness re-presenting) has new cards to ring and new
		// lists to prepare for a finger; the selection stays where it was, on the footer.
		GetNodeOrNull<MenuNavigator>(MenuNavigator.NodeName)?.Rescan();
	}

	/// <summary>What the screen was built from, for a harness that wants to add to it.</summary>
	public RunReport Report => report;

	private static void Clear(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			node.RemoveChild(child);
			child.QueueFree();
		}
	}

	// ============================================================================================
	// The shell: a dim over the run, the stone panel, a header, a body and the buttons.
	// ============================================================================================

	private void BuildShell()
	{
		var dim = new ColorRect { Name = "Dim", Color = DimColour, MouseFilter = Control.MouseFilterEnum.Stop };
		dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(dim);

		panel = new PanelContainer { Name = "Panel" };
		panel.AddThemeStyleboxOverride("panel", BonelightSkin.PanelStyle(BonelightSkin.Register.Stone));
		if (narrow)
		{
			// Nearly the whole phone: width is the scarce axis, and SafeArea insets the edges.
			panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			panel.OffsetLeft = 10f;
			panel.OffsetRight = -10f;
			panel.OffsetTop = 10f;
			panel.OffsetBottom = -10f;
		}
		else
		{
			Vector2 view = ResponsiveLayout.ViewportSize(this);
			float halfWidth = Mathf.Min(MaxPanelWidth, view.X - 40f) * 0.5f;
			float halfHeight = (view.Y - 24f) * 0.5f;
			panel.AnchorLeft = panel.AnchorRight = 0.5f;
			panel.AnchorTop = panel.AnchorBottom = 0.5f;
			panel.OffsetLeft = -halfWidth;
			panel.OffsetRight = halfWidth;
			panel.OffsetTop = -halfHeight;
			panel.OffsetBottom = halfHeight;
		}
		AddChild(panel);

		column = new VBoxContainer { Name = "Column" };
		column.AddThemeConstantOverride("separation", narrow ? 12 : 10);
		panel.AddChild(column);

		headerHost = new VBoxContainer { Name = "Header" };
		headerHost.AddThemeConstantOverride("separation", 10);
		column.AddChild(headerHost);

		bodyHost = new MarginContainer
		{
			Name = "Body",
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		column.AddChild(bodyHost);

		column.AddChild(BuildFooter());
	}

	private Control BuildFooter()
	{
		var footer = new HBoxContainer { Name = "Footer", Alignment = BoxContainer.AlignmentMode.Center };
		footer.AddThemeConstantOverride("separation", 16);

		// The same restart the pause screen's Restart runs: unpause, reload the run scene with the
		// wizard and chapter Global still holds.
		retryButton = MakeFooterButton("RetryButton", "Retry", BonelightSkin.Emphasis.Normal);
		retryButton.Pressed += OnRetryPressed;
		footer.AddChild(retryButton);

		// Named ContinueButton still: it is the button the old screen had, with the old destination,
		// and the touch probe taps it by that name (_TouchProbe.TapTarget).
		continueButton = MakeFooterButton("ContinueButton", "Main menu", BonelightSkin.Emphasis.Primary);
		continueButton.Pressed += OnContinuePressed;
		footer.AddChild(continueButton);
		return footer;
	}

	private Button MakeFooterButton(string name, string text, BonelightSkin.Emphasis emphasis)
	{
		var button = new Button
		{
			Name = name,
			Text = text,
			CustomMinimumSize = new Vector2(narrow ? 220f : 200f, narrow ? 60f : 50f),
			FocusMode = Control.FocusModeEnum.All,
		};
		ResponsiveLayout.SetFont(button, ResponsiveLayout.TextRole.Body);
		BonelightSkin.StyleButton(button, emphasis);
		ResponsiveLayout.EnsureTouchTarget(button);
		return button;
	}

	// ============================================================================================
	// 1. The headline: Defeat or Victory, where and who, and three numbers.
	// ============================================================================================

	private void BuildHeader()
	{
		RunResult result = report.Result;
		bool won = result.Outcome == RunOutcome.Victory;

		var band = new PanelContainer { Name = "OutcomeBand", MouseFilter = Control.MouseFilterEnum.Ignore };
		var bandStyle = new StyleBoxFlat { BgColor = BandColour, BorderColor = BandRule };
		bandStyle.BorderWidthBottom = 2;
		bandStyle.SetCornerRadiusAll(2);
		bandStyle.ContentMarginLeft = 16;
		bandStyle.ContentMarginRight = 16;
		bandStyle.ContentMarginTop = 4;
		bandStyle.ContentMarginBottom = 6;
		band.AddThemeStyleboxOverride("panel", bandStyle);

		var bandRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		bandRow.AddThemeConstantOverride("separation", 18);
		band.AddChild(bandRow);

		var title = new Label
		{
			Name = "OutcomeTitle",
			Text = won ? "Victory" : "Defeat",
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = narrow ? HorizontalAlignment.Center : HorizontalAlignment.Left,
			SizeFlagsHorizontal = narrow ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin,
			CustomMinimumSize = new Vector2(0f, ResponsiveLayout.MinTouchTarget),
		};
		ResponsiveLayout.SetFont(title, ResponsiveLayout.TextRole.Display);
		title.AddThemeColorOverride("font_color", won ? VictoryInk : DefeatInk);
		bandRow.AddChild(title);

		Label where = CodexRows.MakeText(BuildWhereLine(), ResponsiveLayout.TextRole.Label, Stone.InkSoft);
		where.Name = "OutcomeWhere";
		where.AutowrapMode = TextServer.AutowrapMode.WordSmart;

		var tiles = new HBoxContainer
		{
			Name = "Headline",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		tiles.AddThemeConstantOverride("separation", narrow ? 8 : 10);
		tiles.AddChild(BuildTimeTile(result, won));
		tiles.AddChild(BuildHeadlineTile("xp", result.FinalPlayerLevel.ToString(CultureInfo.InvariantCulture), "Level reached"));
		tiles.AddChild(BuildHeadlineTile("kills", Count(result.EnemiesKilled), "Enemies slain"));

		if (narrow)
		{
			where.HorizontalAlignment = HorizontalAlignment.Center;
			headerHost.AddChild(band);
			headerHost.AddChild(where);
			headerHost.AddChild(tiles);
			return;
		}

		// On a PC the outcome and its three numbers are one band: height is the scarce axis at
		// 1280x720, and every row the header does not take is a reward card the player sees without
		// scrolling. The title and where it happened stack on the left; the tiles fill the rest.
		var left = new VBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(250f, 0f),
		};
		left.AddThemeConstantOverride("separation", 0);
		bandRow.RemoveChild(title);
		title.CustomMinimumSize = Vector2.Zero;
		left.AddChild(title);
		where.HorizontalAlignment = HorizontalAlignment.Left;
		// Its own width, not the column's: wrapped at 250 a chapter name broke over two lines.
		where.AutowrapMode = TextServer.AutowrapMode.Off;
		left.AddChild(where);
		bandRow.AddChild(left);
		bandRow.AddChild(tiles);
		bandStyle.ContentMarginTop = 8;
		bandStyle.ContentMarginBottom = 10;
		headerHost.AddChild(band);
	}

	// "Chapter 1 · Enchanted Forest" over "Pyromancer" on a PC, where it stacks under the title;
	// one line on a phone.
	private string BuildWhereLine()
	{
		string chapter = string.IsNullOrWhiteSpace(report.StageName)
			? $"Chapter {report.StageIndex + 1}"
			: $"Chapter {report.StageIndex + 1}  ·  {report.StageName}";
		if (string.IsNullOrWhiteSpace(report.CharacterName))
			return chapter;
		return narrow ? $"{chapter}  ·  {report.CharacterName}" : $"{chapter}\n{report.CharacterName}";
	}

	private Control BuildTimeTile(RunResult result, bool won)
	{
		float arrival = Mathf.Max(1f, report.BossArrivalSeconds);
		// The boss by the name before its epithet - "Elderbark", not "Elderbark, the Treant" - so the
		// line under the bar stays one line on a PC. The breakdown and the chapter card say it in full.
		string boss = (report.BossName ?? string.Empty).Split(',')[0].Trim();
		string caption;
		if (won)
			caption = string.IsNullOrWhiteSpace(boss) ? "Boss defeated" : $"Defeated {boss}";
		else if (report.BossReached)
			caption = string.IsNullOrWhiteSpace(boss) ? "Fell to the boss" : $"Fell to {boss}";
		else
			caption = $"Boss at {FormatTime(arrival)}";

		float fraction = won || report.BossReached ? 1f : Mathf.Clamp(result.TimeSurvived / arrival, 0f, 1f);
		Control tile = BuildHeadlineTile("duration", FormatTime(result.TimeSurvived), "Time survived", fraction, caption, won);
		// Wider than its neighbours: it carries the boss's line as well as its own.
		tile.SizeFlagsStretchRatio = narrow ? 1.25f : 1.6f;
		return tile;
	}

	private Control BuildHeadlineTile(string glyph, string value, string label, float? progress = null,
		string caption = null, bool progressWon = false)
	{
		var tile = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		tile.AddThemeStyleboxOverride("panel", RowStyle(Stone.Face, Stone.Edge, 1, narrow ? 10 : 14, narrow ? 8 : 6));

		Label number = CodexRows.MakeText(value, ResponsiveLayout.TextRole.Title, Stone.Ink);
		number.AutowrapMode = TextServer.AutowrapMode.Off;
		number.Name = "HeadlineValue";
		Label name = CodexRows.MakeText(label, ResponsiveLayout.TextRole.Label, Stone.InkMeta);
		name.AutowrapMode = TextServer.AutowrapMode.Off;
		// Wraps rather than trims: "Defeated the Hollow Choir" is the line a winner reads.
		Label sub = progress == null ? null : CodexRows.MakeText(caption ?? string.Empty, ResponsiveLayout.TextRole.Label,
			progressWon ? Stone.Reward : Stone.InkSoft);

		if (narrow)
		{
			// Three tiles share about 600 units: the glyph stays at its file size beside the number,
			// and everything else stacks under it.
			var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			box.AddThemeConstantOverride("separation", 2);
			tile.AddChild(box);
			var top = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			top.AddThemeConstantOverride("separation", 8);
			top.AddChild(Glyph(glyph, 24f));
			top.AddChild(number);
			box.AddChild(top);
			box.AddChild(name);
			if (progress is float f)
			{
				box.AddChild(Bar(f, progressWon ? NewEdge : Stone.BarFill, 6f));
				box.AddChild(sub);
			}
			return tile;
		}

		// On a PC the glyph is 48, the 12-pixel drawing at x4 - a headline is the one place a number
		// should be recognised from across the room - and the time's caption shares the label's line.
		var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 12);
		tile.AddChild(line);
		line.AddChild(Glyph(glyph, 48f));
		var text = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		text.AddThemeConstantOverride("separation", 0);
		line.AddChild(text);
		text.AddChild(number);
		var foot = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		foot.AddThemeConstantOverride("separation", 10);
		name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		foot.AddChild(name);
		text.AddChild(foot);
		if (progress is float fraction)
		{
			text.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4), MouseFilter = Control.MouseFilterEnum.Ignore });
			text.AddChild(Bar(fraction, progressWon ? NewEdge : Stone.BarFill, 6f));
			// Under the bar, with the tile's whole width, and free to wrap. Beside the label and
			// unwrapped, "Defeated Elderbark, the Treant" set the tile's minimum width and pushed the
			// panel past the right of a 1280 screen; beside it and wrapped, it took three lines.
			text.AddChild(sub);
		}
		return tile;
	}

	// ============================================================================================
	// The body: Rewards, then the two tabs.
	// ============================================================================================

	private void BuildBody()
	{
		if (narrow)
		{
			// One column under a finger. The tabs sit below the rewards inside the same scroll, so
			// a swipe goes straight from what was earned to what was built.
			var list = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			list.AddThemeConstantOverride("separation", SectionGap);
			list.AddChild(BuildRewards());
			list.AddChild(BuildTabBar());
			foreach (Control page in BuildTabPages())
				list.AddChild(page);
			bodyHost.AddChild(Scroll("ResultsScroll", list));
		}
		else
		{
			var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			row.AddThemeConstantOverride("separation", 22);

			ScrollContainer left = Scroll("RewardsScroll", BuildRewards());
			left.SizeFlagsStretchRatio = 0.92f;
			row.AddChild(left);

			var right = new VBoxContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsStretchRatio = 1.08f,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			right.AddThemeConstantOverride("separation", 10);
			right.AddChild(BuildTabBar());
			var pages = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			foreach (Control page in BuildTabPages())
				pages.AddChild(page);
			right.AddChild(Scroll("TabScroll", pages));
			row.AddChild(right);
			bodyHost.AddChild(row);
		}
		ShowTab(activeTab);
	}

	private static ScrollContainer Scroll(string name, Control content)
	{
		ScrollContainer scroll = CodexRows.BuildScroll(name, new VBoxContainer());
		// BuildScroll wraps a list in its gutter; this content is already laid out, so swap it in.
		Node gutter = scroll.GetChild(0);
		Clear(gutter);
		content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		gutter.AddChild(content);
		return scroll;
	}

	// --- Rewards ---------------------------------------------------------------------------------

	private Control BuildRewards()
	{
		var section = new VBoxContainer { Name = "Rewards", MouseFilter = Control.MouseFilterEnum.Ignore };
		section.AddThemeConstantOverride("separation", RowGap);

		List<RunUnlockEntry> entries = report.Unlocks?.Entries ?? new List<RunUnlockEntry>();
		section.AddChild(SectionHeading("Rewards", entries.Count > 0 ? $"{entries.Count} new" : string.Empty));
		section.AddChild(BuildEnergyCard());

		if (entries.Count > 0)
		{
			foreach (RunUnlockEntry entry in entries)
				section.AddChild(BuildUnlockCard(entry));
		}
		else
		{
			section.AddChild(Quiet("Nothing new unlocked this run."));
			if (BuildClosestUnlock() is Control closest)
				section.AddChild(closest);
		}
		return section;
	}

	private Control BuildEnergyCard()
	{
		// No overlay: the breakdown toggle is this card's stop for a pad.
		var card = Row(Stone.Face, Stone.Edge, 1, focusStop: false);
		var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 14);
		RowContent(card).AddChild(line);

		line.AddChild(IconWell(Reward("reward-energy"), 64f, Stone.Reward));

		var text = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		text.AddThemeConstantOverride("separation", 4);
		line.AddChild(text);

		var amount = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		amount.AddThemeConstantOverride("separation", 10);
		// Counted up from nothing by Reveal; the final figure is set here, so a screen that never
		// animates (a harness frame, a reduced-motion setting one day) still says the right thing.
		energyValue = CodexRows.MakeText($"+{Count(report.ArcaneEarned)}", ResponsiveLayout.TextRole.Title, Stone.Reward);
		energyValue.Name = "EnergyValue";
		energyValue.AutowrapMode = TextServer.AutowrapMode.Off;
		amount.AddChild(energyValue);
		Label unit = CodexRows.MakeText("Arcane Energy", ResponsiveLayout.TextRole.Body, Stone.Ink);
		unit.AutowrapMode = TextServer.AutowrapMode.Off;
		unit.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
		amount.AddChild(unit);
		text.AddChild(amount);

		text.AddChild(CodexRows.MakeText($"You now hold {Count(report.ArcanePurse)}", ResponsiveLayout.TextRole.Label, Stone.InkSoft));

		// The breakdown is half a dozen lines, and open it pushed every unlock card below the fold
		// of a 1280x720 PC - the things a player most wants to see. So it is one press away, the
		// pause screen's Show details pattern, and stays as the player left it if the screen rebuilds.
		Control breakdown = BuildBreakdown();
		breakdown.Visible = breakdownShown;
		var toggle = new Button
		{
			Name = "BreakdownToggle",
			Text = BreakdownToggleText(),
			FocusMode = Control.FocusModeEnum.All,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
			Alignment = HorizontalAlignment.Left,
		};
		ResponsiveLayout.SetFont(toggle, ResponsiveLayout.TextRole.Label);
		CodexRows.StyleQuietToggle(toggle, Stone);
		ResponsiveLayout.EnsureTouchTarget(toggle, 120f);
		toggle.Pressed += () =>
		{
			breakdownShown = !breakdownShown;
			breakdown.Visible = breakdownShown;
			toggle.Text = BreakdownToggleText();
		};
		text.AddChild(toggle);

		RowContent(card).AddChild(breakdown);
		return card;
	}

	private bool breakdownShown;

	private string BreakdownToggleText() => breakdownShown ? "Hide how it was counted" : "How it was counted";

	// How the payout was counted, one line per part, numbers in a column on the right. Every
	// value is one CalculateArcaneReward recorded as it worked (ArcaneRewardBreakdown).
	private Control BuildBreakdown()
	{
		ArcaneRewardBreakdown a = report.Arcane ?? new ArcaneRewardBreakdown();
		var lines = new List<(string Glyph, string Label, string Value, bool Strong)>();
		lines.Add(("generic", "Base", $"+{Count(a.BaseFlat)}", false));
		if (a.Minutes > 0)
			lines.Add(("duration", $"{a.Minutes} {(a.Minutes == 1 ? "minute" : "minutes")} survived  (x{a.PerMinute})",
				$"+{Count(a.Minutes * a.PerMinute)}", false));
		lines.Add(("xp", $"Level {a.Level} reached  (x{a.PerLevel})", $"+{Count(a.Level * a.PerLevel)}", false));
		if (Math.Abs(a.UpgradeMultiplier - 1f) > 0.001f)
			lines.Add(("energy", "Arcane Upgrades", $"x{a.UpgradeMultiplier:0.00}", false));
		foreach (var (name, bonus) in a.DynamicParts)
			lines.Add(("luck", name, $"+{Mathf.RoundToInt(bonus * 100f)}%", false));
		if (Math.Abs(a.PresetScale - 1f) > 0.001f)
			lines.Add(("generic", "Balance preset", $"x{a.PresetScale:0.00}", false));
		if (a.BossBonus > 0)
			lines.Add(("crit", string.IsNullOrWhiteSpace(a.BossName) ? "Boss defeated" : $"Defeated {a.BossName}",
				$"+{Count(a.BossBonus)}", false));
		lines.Add(("energy", "This run", $"+{Count(report.ArcaneEarned)}", true));
		if (a.UnlockBonus > 0)
			lines.Add(("done", "Unlock rewards", $"+{Count(a.UnlockBonus)}", true));

		var table = new GridContainer { Columns = 3, MouseFilter = Control.MouseFilterEnum.Ignore };
		table.AddThemeConstantOverride("h_separation", 10);
		table.AddThemeConstantOverride("v_separation", 4);
		foreach (var (glyph, label, value, strong) in lines)
		{
			table.AddChild(Glyph(glyph, 24f));
			Label name = CodexRows.MakeText(label, ResponsiveLayout.TextRole.Label, strong ? Stone.Ink : Stone.InkSoft);
			name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			table.AddChild(name);
			Label number = CodexRows.MakeText(value, ResponsiveLayout.TextRole.Label, strong ? Stone.Reward : Stone.Ink);
			number.AutowrapMode = TextServer.AutowrapMode.Off;
			number.HorizontalAlignment = HorizontalAlignment.Right;
			table.AddChild(number);
		}

		var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		box.AddThemeConstantOverride("separation", 6);
		box.AddChild(new ColorRect { Color = Stone.Rule, CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore });
		box.AddChild(table);
		return box;
	}

	private Control BuildUnlockCard(RunUnlockEntry entry)
	{
		PanelContainer card = Row(Stone.Face, NewEdge, 2, focusStop: true);
		card.Name = $"Unlock_{entry.Kind}";
		var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 14);
		RowContent(card).AddChild(line);

		line.AddChild(IconWell(UnlockIcon(entry), 64f, NewEdge));

		var text = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		text.AddThemeConstantOverride("separation", 3);
		line.AddChild(text);

		var kindRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		kindRow.AddThemeConstantOverride("separation", 8);
		Label kind = CodexRows.MakeText(KindLine(entry.Kind), ResponsiveLayout.TextRole.Label, Stone.Reward);
		kind.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		kind.AutowrapMode = TextServer.AutowrapMode.Off;
		kindRow.AddChild(kind);
		kindRow.AddChild(BuildSeal());
		text.AddChild(kindRow);

		text.AddChild(CodexRows.MakeText(entry.Name, ResponsiveLayout.TextRole.Body, Stone.Ink));
		if (!string.IsNullOrWhiteSpace(entry.Source))
			text.AddChild(CodexRows.MakeText(entry.Source, ResponsiveLayout.TextRole.Label, Stone.InkSoft));
		if (entry.Currency > 0)
			text.AddChild(GlyphLine("energy", $"+{Count(entry.Currency)} Arcane Energy", Stone.Reward));

		revealed.Add(card);
		return card;
	}

	// The New seal: the mutation page's red wax seal and the word, in the flag ink. Every card in
	// Rewards is new by construction - nothing is listed that the save already had - so the seal is
	// what tells an unlock from the Arcane Energy above it, and it is what pops in.
	private Control BuildSeal()
	{
		var seal = new HBoxContainer { Name = "NewSeal", MouseFilter = Control.MouseFilterEnum.Ignore };
		seal.AddThemeConstantOverride("separation", 4);
		Texture2D wax = BonelightSkin.LoadTextureSafe(WaxSealPath);
		if (wax != null)
		{
			seal.AddChild(new TextureRect
			{
				Texture = wax,
				CustomMinimumSize = new Vector2(30f, 30f),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			});
		}
		Label word = CodexRows.MakeText("New", ResponsiveLayout.TextRole.Label, Stone.Flag);
		word.AutowrapMode = TextServer.AutowrapMode.Off;
		seal.AddChild(word);
		seals.Add(seal);
		return seal;
	}

	private static string KindLine(RunUnlockKind kind) => kind switch
	{
		RunUnlockKind.Wizard => "New wizard",
		RunUnlockKind.Spell => "New spell",
		RunUnlockKind.Boon => "New boon",
		RunUnlockKind.Chapter => "New chapter",
		RunUnlockKind.Ascension => "Ascension revealed",
		RunUnlockKind.Apotheosis => "Apotheosis revealed",
		_ => "Unlock completed",
	};

	private Texture2D UnlockIcon(RunUnlockEntry entry)
	{
		switch (entry.Kind)
		{
			case RunUnlockKind.Wizard:
				return CharacterRoster.GetAll()
					.FirstOrDefault(c => c != null && c.Id.Equals(entry.Id, StringComparison.OrdinalIgnoreCase))?.Portrait
					?? Reward("reward-deed");
			case RunUnlockKind.Spell:
				return SpellIcon(entry.Id);
			case RunUnlockKind.Boon:
				return BonelightSkin.LoadTextureSafe(BoonCatalog.GetById(entry.Id)?.IconPath) ?? Reward("reward-deed");
			case RunUnlockKind.Chapter:
				return Reward("reward-chapter");
			case RunUnlockKind.Ascension:
				return BonelightSkin.LoadTextureSafe(entry.IconPath) ?? Reward("reward-ascension");
			case RunUnlockKind.Apotheosis:
				return BonelightSkin.LoadTextureSafe(entry.IconPath) ?? Reward("reward-apotheosis");
			default:
				return Reward("reward-deed");
		}
	}

	// Nothing unlocked: say what is nearest, so the next run has something to aim at. The Unlocks
	// list's own row (CodexRows.BuildDeedRow), so it reads exactly as it does on the main menu.
	private Control BuildClosestUnlock()
	{
		SaveData data = GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data;
		if (data == null)
			return null;
		AchievementContext context = AchievementContext.ForSave(data);
		var open = AchievementDefinitions.All
			.Where(d => !GlobalStatsManager.IsAchievementUnlocked(data, d.Id))
			.Select(d => (Deed: d, Progress: d.GetProgress(context)))
			.Where(p => !string.IsNullOrEmpty(p.Progress?.Display) && p.Progress.Fraction < 1f);
		var closest = CodexRows.OrderInProgress(open).FirstOrDefault();
		if (closest.Deed == null)
			return null;

		var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		box.AddThemeConstantOverride("separation", 6);
		box.AddChild(CodexRows.MakeText("Closest unlock", ResponsiveLayout.TextRole.Label, Stone.InkMeta));
		box.AddChild(CodexRows.BuildDeedRow(closest.Deed, false, closest.Progress, SpellIcon, Stone));
		return box;
	}

	// --- the tabs --------------------------------------------------------------------------------

	private static readonly string[] TabTitles = { "Your build", "Stats" };

	private Control BuildTabBar()
	{
		var bar = new HBoxContainer { Name = "Tabs", MouseFilter = Control.MouseFilterEnum.Ignore };
		bar.AddThemeConstantOverride("separation", 6);
		var group = new ButtonGroup();
		for (int i = 0; i < TabTitles.Length; i++)
		{
			int index = i;
			var tab = new Button
			{
				Name = $"ResultsTab{i}",
				Text = TabTitles[i],
				ToggleMode = true,
				ButtonGroup = group,
				FocusMode = Control.FocusModeEnum.All,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(0f, ResponsiveLayout.MinTouchTarget),
			};
			ResponsiveLayout.SetFont(tab, ResponsiveLayout.TextRole.Title);
			CodexRows.StyleTab(tab, Stone);
			tab.Pressed += () => ShowTab(index);
			tabButtons.Add(tab);
			bar.AddChild(tab);
		}
		return bar;
	}

	private IEnumerable<Control> BuildTabPages()
	{
		Control build = BuildBuildPage();
		Control stats = BuildStatsPage();
		tabPages.Add(build);
		tabPages.Add(stats);
		return tabPages;
	}

	private void ShowTab(int index)
	{
		activeTab = Mathf.Clamp(index, 0, Math.Max(0, tabPages.Count - 1));
		for (int i = 0; i < tabPages.Count; i++)
			tabPages[i].Visible = i == activeTab;
		for (int i = 0; i < tabButtons.Count; i++)
			tabButtons[i].SetPressedNoSignal(i == activeTab);
	}

	// --- Your build ---------------------------------------------------------------------------

	private Control BuildBuildPage()
	{
		var page = new VBoxContainer { Name = "BuildPage", MouseFilter = Control.MouseFilterEnum.Ignore };
		page.AddThemeConstantOverride("separation", SectionGap);
		List<RunSpellSnapshot> spells = report.Result.EquippedSpells ?? new List<RunSpellSnapshot>();

		var attacks = spells.Where(s => !s.IsPassive).ToList();
		var passives = spells.Where(s => s.IsPassive).ToList();
		page.AddChild(Group(passives.Count > 0 ? "Attacks" : "Spells", attacks.Count.ToString(CultureInfo.InvariantCulture),
			attacks.Count == 0 ? new[] { Quiet("None") } : attacks.Select(BuildSpellRow).ToArray(), columns: 1));
		if (passives.Count > 0)
			page.AddChild(Group("Passives", passives.Count.ToString(CultureInfo.InvariantCulture),
				passives.Select(BuildSpellRow).ToArray(), columns: 1));

		// The run's element tags in total, highest first: the shape of the build in one row of
		// chips, which is what every tier and every Ascension gate was counting.
		var elements = (report.Result.ElementCounts ?? new Dictionary<string, int>())
			.Where(p => p.Value > 0)
			.OrderByDescending(p => p.Value).ThenBy(p => p.Key)
			.Select(p => (p.Key, p.Value)).ToList();
		if (elements.Count > 0)
			page.AddChild(Group("Element tags", string.Empty, new[] { Chips(elements, totals: true) }, columns: 1));

		IReadOnlyList<string> boons = report.BoonIds ?? new List<string>();
		page.AddChild(Group("Boons", boons.Count.ToString(CultureInfo.InvariantCulture),
			boons.Count == 0 ? new[] { Quiet("None taken.") } : new[] { Flow(boons.Select(BuildBoonPill)) }, columns: 1));

		IReadOnlyList<string> relics = report.RelicIds ?? new List<string>();
		var relicRows = new List<Control>();
		if (relics.Count == 0)
			relicRows.Add(Quiet("None claimed."));
		else
			relicRows.Add(Flow(relics.Select(BuildRelicPill)));
		page.AddChild(Group("Relics", relics.Count.ToString(CultureInfo.InvariantCulture), relicRows.ToArray(), columns: 1));

		// Full Set Enchantments the run started, complete ones first (the pause screen's order).
		var sets = ChestItemCatalog.Sets
			.Select(set => (Set: set, Progress: ChestItemCatalog.GetSetProgress(set, relics.ToList())))
			.Where(s => s.Progress.OwnedCount > 0)
			.OrderByDescending(s => s.Progress.OwnedCount >= s.Progress.TotalRequired)
			.ThenByDescending(s => s.Progress.TotalRequired == 0 ? 0f : s.Progress.OwnedCount / (float)s.Progress.TotalRequired)
			.ToList();
		if (sets.Count > 0)
			page.AddChild(Group("Full Set Enchantments", string.Empty,
				new[] { Flow(sets.Select(s => BuildSetPill(s.Set, s.Progress.OwnedCount, s.Progress.TotalRequired))) }, columns: 1));
		return page;
	}

	private Control BuildSpellRow(RunSpellSnapshot spell)
	{
		PanelContainer row = Row(Stone.Face, Stone.Edge, 1, focusStop: true);
		row.Name = $"Spell_{spell.Id}";
		var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 14);
		RowContent(row).AddChild(line);

		// The icon in its milestone's rim, the loadout strip's colour language.
		Color rim = !string.IsNullOrEmpty(spell.ApotheosisName) ? ApotheosisMark
			: !string.IsNullOrEmpty(spell.AscensionName) ? AscensionMark
			: !string.IsNullOrEmpty(spell.MutationName) ? MutationMark
			: HeldRim;
		line.AddChild(IconWell(SpellIcon(spell.Id, spell.IconPath), 64f, rim));

		var text = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		text.AddThemeConstantOverride("separation", 4);
		line.AddChild(text);

		var titleRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		titleRow.AddThemeConstantOverride("separation", 10);
		Label name = CodexRows.MakeText(string.IsNullOrWhiteSpace(spell.DisplayName) ? spell.Id : spell.DisplayName,
			ResponsiveLayout.TextRole.Body, Stone.Ink);
		name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleRow.AddChild(name);
		bool maxed = spell.MaxLevel > 0 && spell.Level >= spell.MaxLevel;
		Label level = CodexRows.MakeText(maxed ? $"Level {spell.Level} - max" : $"Level {spell.Level} of {spell.MaxLevel}",
			ResponsiveLayout.TextRole.Label, maxed ? Stone.Reward : Stone.InkSoft);
		level.AutowrapMode = TextServer.AutowrapMode.Off;
		level.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		titleRow.AddChild(level);
		text.AddChild(titleRow);

		if (!string.IsNullOrEmpty(spell.MutationName))
			text.AddChild(Milestone("Mutation", spell.MutationName, MutationMark));
		if (!string.IsNullOrEmpty(spell.AscensionName))
			text.AddChild(Milestone("Ascension", spell.AscensionName, AscensionMark));
		if (!string.IsNullOrEmpty(spell.ApotheosisName))
			text.AddChild(Milestone("Apotheosis", spell.ApotheosisName, ApotheosisMark));
		if (spell.Elements != null && spell.Elements.Count > 0)
			text.AddChild(Chips(spell.Elements));
		return row;
	}

	// "[Ascension] Titan Cataclysm": the milestone as a solid mark in its rim colour, the branch
	// taken in the row's ink after it.
	private static Control Milestone(string word, string branch, Color mark)
	{
		var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 8);
		var pill = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		var style = new StyleBoxFlat { BgColor = mark };
		style.SetCornerRadiusAll(4);
		style.ContentMarginLeft = 8;
		style.ContentMarginRight = 8;
		style.ContentMarginTop = 1;
		style.ContentMarginBottom = 1;
		pill.AddThemeStyleboxOverride("panel", style);
		Label label = CodexRows.MakeText(word, ResponsiveLayout.TextRole.Label, MarkInk);
		label.AutowrapMode = TextServer.AutowrapMode.Off;
		pill.AddChild(label);
		line.AddChild(pill);
		Label name = CodexRows.MakeText(branch, ResponsiveLayout.TextRole.Label, Stone.Ink);
		name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		line.AddChild(name);
		return line;
	}

	private Control BuildBoonPill(string id)
	{
		BoonDefinition boon = BoonCatalog.GetById(id);
		// The relic in its own light, as on the pause screen (Node2DGame.BuildBoonListSection): one
		// aura cell at x1, quieter than on the card, so a list of them still reads as a list.
		Control picture = new BoonAura
		{
			Icon = BonelightSkin.LoadTextureSafe(boon?.IconPath),
			AuraElement = boon != null ? BoonAura.ElementFor(boon) : Element.Arcane,
			Intensity = 0.7f,
			CustomMinimumSize = new Vector2(BoonAura.Cell, BoonAura.Cell),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		return Pill(picture, boon?.Name ?? id, null, Stone.Ink);
	}

	private Control BuildRelicPill(string id)
	{
		return Pill(Icon(BonelightSkin.LoadTextureSafe(ChestItemCatalog.GetIconPath(id)), 32f),
			ChestItemCatalog.GetDisplayName(id), null, Stone.Ink);
	}

	private Control BuildSetPill(ChestSetDefinition set, int have, int need)
	{
		bool complete = need > 0 && have >= need;
		return Pill(Icon(BonelightSkin.LoadTextureSafe(set.IconPath), 32f), set.Name,
			complete ? $"{have}/{need} complete" : $"{have}/{need}", complete ? Stone.Reward : Stone.Ink);
	}

	// --- Stats -----------------------------------------------------------------------------------

	private Control BuildStatsPage()
	{
		RunResult r = report.Result;
		var page = new VBoxContainer { Name = "StatsPage", MouseFilter = Control.MouseFilterEnum.Ignore };
		page.AddThemeConstantOverride("separation", SectionGap);

		var tiles = new List<Control>
		{
			StatTile("kills", Count(r.EnemiesKilled), "Enemies slain"),
			StatTile("elite", Count(r.ElitesKilled), "Elites slain"),
			StatTile("damage", Count(r.TotalDamageDealt), "Damage dealt"),
			StatTile("heart", Count(r.TotalDamageTaken), r.HitsTaken == 1 ? "Damage taken, 1 hit" : $"Damage taken, {Count(r.HitsTaken)} hits"),
			StatTile("xp", Count(report.XpGathered), "XP gathered"),
			StatTile("chest", Count(r.ChestsOpened), r.ChestsOpened == 1 ? "Chest opened" : "Chests opened"),
			r.HitsTaken == 0
				? StatTile("evasion", "Untouched", "Never hit all run")
				: StatTile("evasion", FormatTime(r.UndamagedSeconds), "First hit taken"),
		};
		if (r.RevivesUsed > 0)
			tiles.Add(StatTile("regen", Count(r.RevivesUsed), r.RevivesUsed == 1 ? "Extra life spent" : "Extra lives spent"));
		page.AddChild(Group("The run", string.Empty, tiles.ToArray(), columns: narrow ? 2 : 3));

		page.AddChild(Group("Damage by spell", string.Empty, BuildDamageRows().ToArray(), columns: 1));

		if (report.PlaytestMode)
			page.AddChild(Group("Playtest telemetry", string.Empty,
				new Control[] { CodexRows.MakeText(BuildTelemetrySummaryText(r), ResponsiveLayout.TextRole.Label, Stone.InkMeta) }, columns: 1));
		return page;
	}

	private Control StatTile(string glyph, string value, string label)
	{
		var tile = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		tile.AddThemeStyleboxOverride("panel", RowStyle(Stone.Face, Stone.Edge, 1, 12, 8));
		var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 10);
		tile.AddChild(line);
		line.AddChild(Glyph(glyph, 24f));
		var text = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		text.AddThemeConstantOverride("separation", 0);
		Label number = CodexRows.MakeText(value, ResponsiveLayout.TextRole.Body, Stone.Ink);
		number.AutowrapMode = TextServer.AutowrapMode.Off;
		text.AddChild(number);
		text.AddChild(CodexRows.MakeText(label, ResponsiveLayout.TextRole.Label, Stone.InkMeta));
		line.AddChild(text);
		return tile;
	}

	// Each spell's share of the damage, biggest first, as a bar against the biggest. A tick of
	// poison or burn reaches the enemy with no spell named (GameStats.RecordDamageBySource), so it
	// is one "Other" line at the foot rather than a guess at whose it was.
	private List<Control> BuildDamageRows()
	{
		var rows = new List<Control>();
		var sources = report.DamageBySource ?? new List<(string SourceId, int Damage)>();
		long sum = sources.Sum(s => (long)s.Damage);
		if (sum <= 0)
		{
			rows.Add(Quiet("No damage recorded."));
			return rows;
		}

		var named = sources.Where(s => s.SourceId != "(unattributed)").ToList();
		int other = sources.Where(s => s.SourceId == "(unattributed)").Sum(s => s.Damage);
		int top = Math.Max(1, sources.Max(s => s.Damage));
		foreach (var (id, damage) in named)
			rows.Add(DamageRow(SpellIcon(id), SpellName(id), damage, top, sum, Stone.BarFill));
		if (other > 0)
			rows.Add(DamageRow(StatIcons.Load("generic"), "Other - poison, burns and the rest", other, top, sum, Stone.InkMeta));
		return rows;
	}

	private Control DamageRow(Texture2D icon, string name, int damage, int top, long sum, Color fill)
	{
		PanelContainer row = Row(Stone.Face, Stone.Edge, 1, focusStop: false, padX: 12, padY: 8);
		var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 12);
		RowContent(row).AddChild(line);
		line.AddChild(Icon(icon, 32f));

		var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
		text.AddThemeConstantOverride("separation", 4);
		var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		head.AddThemeConstantOverride("separation", 10);
		Label label = CodexRows.MakeText(name, ResponsiveLayout.TextRole.Label, Stone.Ink);
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		head.AddChild(label);
		int share = (int)Math.Round(damage * 100.0 / Math.Max(1L, sum));
		Label amount = CodexRows.MakeText($"{Count(damage)}  ({share}%)", ResponsiveLayout.TextRole.Label, Stone.InkSoft);
		amount.AutowrapMode = TextServer.AutowrapMode.Off;
		head.AddChild(amount);
		text.AddChild(head);
		text.AddChild(Bar(damage / (float)top, fill, 6f));
		line.AddChild(text);
		return row;
	}

	// ============================================================================================
	// Reveal: short, and never in the way.
	// ============================================================================================

	// The energy counts up, the unlock cards fade in one after another, and each New seal pops as
	// its card arrives - about a second for a typical run, every button live from the first frame.
	// Nothing is laid out by the tween: alpha and a seal's scale only, so a card cannot be pressed
	// in one place and drawn in another. The tween runs on the paused tree (this layer processes
	// while paused) and dies with the screen.
	private void Reveal()
	{
		revealTween?.Kill();
		revealTween = CreateTween();
		revealTween.SetParallel(true);

		if (energyValue != null && report.ArcaneEarned > 0)
		{
			energyValue.Text = "+0";
			revealTween.TweenMethod(Callable.From<int>(ShowEnergy), 0, report.ArcaneEarned, 0.9f)
				.SetDelay(0.15f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		}

		float delay = 0.35f;
		for (int i = 0; i < revealed.Count; i++)
		{
			Control card = revealed[i];
			card.Modulate = new Color(1f, 1f, 1f, 0f);
			revealTween.TweenProperty(card, "modulate:a", 1f, 0.22f).SetDelay(delay);
			if (i < seals.Count)
			{
				Control seal = seals[i];
				seal.Modulate = new Color(1f, 1f, 1f, 0f);
				revealTween.TweenCallback(Callable.From(() => PopSeal(seal))).SetDelay(delay + 0.12f);
			}
			// Spaced while there are few to show; a long list arrives in the same second as a short one.
			delay += Mathf.Min(0.12f, 0.6f / Math.Max(1, revealed.Count));
		}
	}

	private void ShowEnergy(int value)
	{
		if (energyValue != null && IsInstanceValid(energyValue))
			energyValue.Text = $"+{Count(value)}";
	}

	private void PopSeal(Control seal)
	{
		if (seal == null || !IsInstanceValid(seal))
			return;
		seal.PivotOffset = seal.Size * 0.5f;
		seal.Scale = new Vector2(1.6f, 1.6f);
		Tween pop = seal.CreateTween();
		pop.SetParallel(true);
		pop.TweenProperty(seal, "modulate:a", 1f, 0.12f);
		pop.TweenProperty(seal, "scale", Vector2.One, 0.28f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	// ============================================================================================
	// Building blocks
	// ============================================================================================

	private static StyleBoxFlat RowStyle(Color face, Color edge, int edgeWidth, int padX, int padY)
	{
		var style = new StyleBoxFlat { BgColor = face, BorderColor = edge };
		style.SetBorderWidthAll(edgeWidth);
		style.SetCornerRadiusAll(6);
		style.ContentMarginLeft = padX;
		style.ContentMarginRight = padX;
		style.ContentMarginTop = padY;
		style.ContentMarginBottom = padY;
		return style;
	}

	// A row is a PanelContainer whose padding is an inner MarginContainer (CodexRows.BuildRow's
	// arrangement), so a focus overlay - a sibling of the padding - covers the whole card and its
	// ring sits on the edge. The overlay ignores the mouse: to a pointer the row is text, and a press
	// that starts on it must be free to become a drag of the list.
	private static PanelContainer Row(Color face, Color edge, int edgeWidth, bool focusStop, int padX = 16, int padY = 12)
	{
		var row = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		row.AddThemeStyleboxOverride("panel", RowStyle(face, edge, edgeWidth, 0, 0));
		if (focusStop)
		{
			Button overlay = BonelightSkin.MakeCardClickOverlay(true);
			overlay.Name = "RowFocus";
			overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
			row.AddChild(overlay);
		}
		var padding = new MarginContainer { Name = "Padding", MouseFilter = Control.MouseFilterEnum.Ignore };
		padding.AddThemeConstantOverride("margin_left", padX);
		padding.AddThemeConstantOverride("margin_right", padX);
		padding.AddThemeConstantOverride("margin_top", padY);
		padding.AddThemeConstantOverride("margin_bottom", padY);
		row.AddChild(padding);
		var content = new VBoxContainer { Name = "Content", MouseFilter = Control.MouseFilterEnum.Ignore };
		content.AddThemeConstantOverride("separation", 10);
		padding.AddChild(content);
		return row;
	}

	private static VBoxContainer RowContent(PanelContainer row) => row.GetNode<VBoxContainer>("Padding/Content");

	// A section heading in the CodexRows manner: bold Cinzel, a count on the right, a rule beneath.
	private static Control SectionHeading(string title, string count)
	{
		var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		box.AddThemeConstantOverride("separation", 6);
		var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		head.AddThemeConstantOverride("separation", 12);
		Label heading = CodexRows.MakeText(title, ResponsiveLayout.TextRole.Title, Stone.Heading);
		heading.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		heading.VerticalAlignment = VerticalAlignment.Bottom;
		heading.AutowrapMode = TextServer.AutowrapMode.Off;
		head.AddChild(heading);
		if (!string.IsNullOrEmpty(count))
		{
			Label tally = CodexRows.MakeText(count, ResponsiveLayout.TextRole.Label, Stone.Flag);
			tally.AutowrapMode = TextServer.AutowrapMode.Off;
			tally.VerticalAlignment = VerticalAlignment.Bottom;
			head.AddChild(tally);
		}
		box.AddChild(head);
		box.AddChild(new ColorRect { Color = Stone.Rule, CustomMinimumSize = new Vector2(0, 2), MouseFilter = Control.MouseFilterEnum.Ignore });
		return box;
	}

	// A group inside a tab: a quiet label with its count, then its rows in one or more columns.
	private static Control Group(string title, string count, Control[] rows, int columns)
	{
		var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		box.AddThemeConstantOverride("separation", 8);
		var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		head.AddThemeConstantOverride("separation", 10);
		Label label = CodexRows.MakeText(title, ResponsiveLayout.TextRole.Body, Stone.Heading);
		label.AutowrapMode = TextServer.AutowrapMode.Off;
		head.AddChild(label);
		if (!string.IsNullOrEmpty(count))
		{
			Label tally = CodexRows.MakeText(count, ResponsiveLayout.TextRole.Label, Stone.InkMeta);
			tally.AutowrapMode = TextServer.AutowrapMode.Off;
			tally.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
			head.AddChild(tally);
		}
		box.AddChild(head);

		var grid = new GridContainer
		{
			Columns = Math.Max(1, columns),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		grid.AddThemeConstantOverride("h_separation", 10);
		grid.AddThemeConstantOverride("v_separation", RowGap);
		foreach (Control row in rows)
		{
			row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			grid.AddChild(row);
		}
		box.AddChild(grid);
		return box;
	}

	private static Label Quiet(string text) => CodexRows.MakeText(text, ResponsiveLayout.TextRole.Label, Stone.InkMeta);

	private static Control Flow(IEnumerable<Control> items)
	{
		var flow = new HFlowContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		flow.AddThemeConstantOverride("h_separation", 8);
		flow.AddThemeConstantOverride("v_separation", 8);
		foreach (Control item in items)
			flow.AddChild(item);
		return flow;
	}

	// A boon, relic or set as a compact pill: its picture and its name, and a count for a set.
	private static Control Pill(Control picture, string name, string note, Color noteInk)
	{
		var pill = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		pill.AddThemeStyleboxOverride("panel", RowStyle(Stone.Face, Stone.Edge, 1, 8, 4));
		var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 8);
		pill.AddChild(line);
		picture.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		line.AddChild(picture);
		Label label = CodexRows.MakeText(name, ResponsiveLayout.TextRole.Label, Stone.Ink);
		label.AutowrapMode = TextServer.AutowrapMode.Off;
		line.AddChild(label);
		if (!string.IsNullOrEmpty(note))
		{
			Label tail = CodexRows.MakeText(note, ResponsiveLayout.TextRole.Label, noteInk);
			tail.AutowrapMode = TextServer.AutowrapMode.Off;
			line.AddChild(tail);
		}
		// Room on the right, so the text does not sit on the edge.
		line.AddChild(new Control { CustomMinimumSize = new Vector2(2, 0), MouseFilter = Control.MouseFilterEnum.Ignore });
		return pill;
	}

	// Element chips, CodexRows.BuildChip: a tap opens the element's card, the same object as on
	// every list. Not focusable - the row's overlay is the pad's stop, one per row.
	// A spell's own tags read "Fire x2" (a double tag, the codex's wording); the run's totals read
	// "Fire 7", a count.
	private static Control Chips(IEnumerable<(string Element, int Weight)> tags, bool totals = false)
	{
		var flow = new HFlowContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		flow.AddThemeConstantOverride("h_separation", 6);
		flow.AddThemeConstantOverride("v_separation", 6);
		foreach (var (name, weight) in tags)
		{
			if (!Enum.TryParse(name, true, out Element element))
				continue;
			string text = totals ? $"{element} {weight}" : weight > 1 ? $"{element} x{weight}" : element.ToString();
			flow.AddChild(CodexRows.BuildChip(element, text, false));
		}
		return flow;
	}

	// A picture on the icon well: stone deep with a rim. Icons are drawn at a whole multiple of
	// their pixels - the 64-pixel spell and reward art as drawn, a 48-pixel portrait as drawn and
	// centred - so nothing smears.
	private static Control IconWell(Texture2D texture, float size, Color rim)
	{
		var well = new PanelContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
		};
		var style = new StyleBoxFlat { BgColor = Well, BorderColor = rim };
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(6);
		style.SetContentMarginAll(4);
		well.AddThemeStyleboxOverride("panel", style);
		well.AddChild(Icon(texture, size));
		return well;
	}

	private static TextureRect Icon(Texture2D texture, float size)
	{
		bool native = texture != null && texture.GetWidth() <= size && texture.GetHeight() <= size
			&& Mathf.Abs(texture.GetWidth() - size) > 0.5f;
		return new TextureRect
		{
			Texture = texture,
			CustomMinimumSize = new Vector2(size, size),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			// A picture smaller than its box is centred at its own size (a 48 portrait in 64); one
			// as big or bigger is fitted, which for the 64 art in a 32 box is an exact half.
			StretchMode = native ? TextureRect.StretchModeEnum.KeepCentered : TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
	}

	// A stat glyph (tools/art/stat_icons.py) at its file size or a whole multiple of it.
	private static Control Glyph(string key, float size)
	{
		TextureRect glyph = Icon(StatIcons.Load(key), size);
		glyph.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		glyph.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		return glyph;
	}

	private static Control GlyphLine(string key, string text, Color ink)
	{
		var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 8);
		line.AddChild(Glyph(key, 24f));
		Label label = CodexRows.MakeText(text, ResponsiveLayout.TextRole.Label, ink);
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		line.AddChild(label);
		return line;
	}

	private static Control Bar(float fraction, Color fill, float height)
	{
		fraction = Mathf.Clamp(fraction, 0f, 1f);
		var track = new PanelContainer { CustomMinimumSize = new Vector2(0, height), MouseFilter = Control.MouseFilterEnum.Ignore };
		var trackStyle = new StyleBoxFlat { BgColor = Stone.BarTrack };
		trackStyle.SetCornerRadiusAll(3);
		track.AddThemeStyleboxOverride("panel", trackStyle);
		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 0);
		var filled = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = Math.Max(0.001f, fraction),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		var fillStyle = new StyleBoxFlat { BgColor = fraction > 0f ? fill : new Color(0, 0, 0, 0) };
		fillStyle.SetCornerRadiusAll(3);
		filled.AddThemeStyleboxOverride("panel", fillStyle);
		row.AddChild(filled);
		row.AddChild(new Control
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = Math.Max(0.001f, 1f - fraction),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
		track.AddChild(row);
		return track;
	}

	private static Texture2D Reward(string name) => BonelightSkin.LoadTextureSafe($"{RewardIconRoot}{name}.png");

	// A spell's picture: as the run drew it if it was held, otherwise from its resource.
	private Texture2D SpellIcon(string spellId) => SpellIcon(spellId, null);

	private Texture2D SpellIcon(string spellId, string iconPath)
	{
		if (!string.IsNullOrEmpty(spellId) && report?.SpellIcons != null && report.SpellIcons.TryGetValue(spellId, out Texture2D held) && held != null)
			return held;
		if (BonelightSkin.LoadTextureSafe(iconPath) is Texture2D fromPath)
			return fromPath;
		SpellData data = ContentValidator.LoadAllSpells()
			.FirstOrDefault(s => s != null && s.Id.Equals(spellId ?? string.Empty, StringComparison.OrdinalIgnoreCase));
		return data?.Icon ?? BonelightSkin.LoadTextureSafe(UnknownSpellIcon);
	}

	private string SpellName(string spellId)
	{
		RunSpellSnapshot held = report?.Result?.EquippedSpells?
			.FirstOrDefault(s => s.Id.Equals(spellId, StringComparison.OrdinalIgnoreCase));
		if (held != null && !string.IsNullOrWhiteSpace(held.DisplayName))
			return held.DisplayName;
		SpellData data = ContentValidator.LoadAllSpells()
			.FirstOrDefault(s => s != null && s.Id.Equals(spellId, StringComparison.OrdinalIgnoreCase));
		if (data != null)
			return data.Name;
		string[] words = (spellId ?? string.Empty).Split('_', StringSplitOptions.RemoveEmptyEntries);
		return words.Length == 0 ? spellId : string.Join(" ", words.Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1)));
	}

	private static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

	private static string FormatTime(float seconds)
	{
		int totalSeconds = Math.Max(0, Mathf.FloorToInt(seconds));
		int minutes = totalSeconds / 60;
		int remainder = totalSeconds % 60;
		return $"{minutes:00}:{remainder:00}";
	}

	// The playtester's readout, kept from the old screen and shown only with Playtest Mode on: damage
	// rate, hits, rerolls, pity and variety multipliers - numbers nobody reading "Defeat" is after.
	private static string BuildTelemetrySummaryText(RunResult result)
	{
		float minutes = Math.Max(0.0167f, result.TimeSurvived / 60.0f);
		float dps = result.TotalDamageDealt / minutes;
		float avgHit = result.HitsTaken > 0 ? (float)result.TotalDamageTaken / result.HitsTaken : 0f;

		string topPickedSpell = GetTopSpell(result.SpellPickCounts);
		string topUpgradedSpell = GetTopSpell(result.SpellUpgradeCounts);

		string rewardModel = result.ArcaneRewardMultiplier > 0f
			? $"Reward x{result.ArcaneRewardMultiplier:0.00}" + (string.IsNullOrWhiteSpace(result.ArcaneRewardBreakdown) ? string.Empty : $" ({result.ArcaneRewardBreakdown})")
			: string.Empty;

		return $"Damage dealt {result.TotalDamageDealt} ({dps:0} per min), damage taken {result.TotalDamageTaken} across {result.HitsTaken} hits (avg {avgHit:0.0}), level-ups {result.LevelUpsGained}, rerolls {result.RerollsUsed}, swaps {result.SwapsUsed}, removals {result.RemovalsUsed}, skips {result.SkipsUsed}, bans {result.BansUsed}, saves {result.SavesUsed}, auguries {result.AuguriesUsed}. Picks: {topPickedSpell}. Upgrades: {topUpgradedSpell}. {rewardModel}";
	}

	private static string GetTopSpell(Dictionary<string, int> counts)
	{
		if (counts == null || counts.Count == 0)
			return "none";

		var top = counts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).First();
		return $"{top.Key} x{top.Value}";
	}

	// ============================================================================================
	// The buttons - destinations unchanged.
	// ============================================================================================

	private void OnContinuePressed()
	{
		GetTree().Paused = false;
		// The menu lives on the title screen now, so going "back to the menu" means loading
		// its shell and telling it to skip the press-any-key beat.
		Global.OpenMenuImmediately = true;
		GetTree().ChangeSceneToFile("res://scenes/TitleScreen.tscn");
	}

	// The pause screen's Restart (Node2DGame.OnEscapeRestartPressed), from the results instead:
	// the same wizard on the same chapter, Global still holding both.
	private void OnRetryPressed()
	{
		GetTree().Paused = false;
		GetTree().ChangeSceneToFile("res://scenes/node_2d_game.tscn");
	}
}
