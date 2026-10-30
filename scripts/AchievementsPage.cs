using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

/// <summary>
/// The achievements page, built once for both places it is shown: the main menu's panel and the pause
/// screen's tab (.ai/achievements-and-secrets.md section 4). Static, like <see cref="CodexRows"/>, whose
/// rows it is made of.
/// </summary>
/// <remarks>
/// It was the Unlocks list, built twice - <c>MainMenu.BuildAchievementCards</c> and
/// <c>Node2DGame.ShowEscapeAchievements</c> - with only the row shared, and a Secrets section on the main
/// menu only. Eric (2026-10-29): "all achievements should be listed or revealed as the player plays the
/// game in the achievements menu in the main menu (and in the pause menu)". So:
/// <list type="bullet">
/// <item>Every achievement is on it, in three kinds of listing (4.2): a Feat from the first run; a
/// chapter's deed once the chapter is built, or a sealed row while it frees a secret wizard whose chapter
/// has not opened; a secret never named until seen.</item>
/// <item>A hidden achievement moves through three states (4.3): <b>sealed</b> - not a row of its own, one
/// quiet row per open chapter that counts them and shows each as the silhouette of the kind of thing it
/// pays; <b>seen</b> - its own row once its near tell has been on screen, named, with what was seen and
/// what it pays, never what to do; <b>found</b>.</item>
/// <item>On the menu, four tabs (All, Chapters, Feats, Secrets). Over a run, one list: this chapter
/// first with this run's numbers, then everything else not done, then Done behind a toggle (4.6).</item>
/// <item>Progress where it applies, and what each pays, led by the reward's own glyph (4.4, 4.5).</item>
/// </list>
/// It decides nothing about an achievement itself: its kind, chapter, visibility and words are piece A1's
/// (<see cref="AchievementDefinition"/>, <see cref="AchievementDefinitions.IsListed(AchievementDefinition, SaveData)"/>,
/// <see cref="AchievementDefinitions.IsSealed"/>), and what has been seen is the save's
/// (<see cref="SaveData.HasGlimpsedSecret"/>). The page only sorts and draws.
/// </remarks>
public static class AchievementsPage
{
	public enum PageMode
	{
		/// <summary>The main menu: lifetime numbers, best runs.</summary>
		Menu,
		/// <summary>The pause screen: this run's numbers folded in, this chapter first.</summary>
		Pause,
	}

	public enum Filter
	{
		All,
		Chapters,
		Feats,
		Secrets,
	}

	/// <summary>The menu's chosen tab, remembered for the session (4.6). An enum, so a static is safe.</summary>
	public static Filter MenuFilter { get; set; } = Filter.All;

	/// <summary>What a build counted, for the summary line, and the pause list's Done toggle.</summary>
	public sealed class Result
	{
		public int Earned { get; internal set; }
		public int Total { get; internal set; }
		public int SecretsFound { get; internal set; }

		internal GridContainer DoneRows;
		internal Button DoneToggle;
		internal int DoneCount;

		/// <summary>
		/// Opens or closes the pause list's Done section - its toggle, a tap, or pad Y on the tab
		/// (4.8). A selection inside the rows being closed moves to the toggle first, so the stick is
		/// never left holding something hidden.
		/// </summary>
		public void ToggleDone()
		{
			if (DoneRows == null || !GodotObject.IsInstanceValid(DoneRows))
				return;
			bool open = !DoneRows.Visible;
			if (!open && DoneToggle != null && GodotObject.IsInstanceValid(DoneToggle)
				&& DoneRows.GetViewport()?.GuiGetFocusOwner() is Control owner && DoneRows.IsAncestorOf(owner))
				DoneToggle.GrabFocus();
			DoneRows.Visible = open;
			if (DoneToggle != null && GodotObject.IsInstanceValid(DoneToggle))
				DoneToggle.Text = DoneToggleText(open, DoneCount);
		}
	}

	/// <summary>The summary under the heading: "41 of 88 earned  ·  6 secrets found".</summary>
	public static string SummaryLine(Result result, bool narrow) =>
		result == null ? string.Empty : CodexRows.UnlocksSummary(result.Earned, result.Total, result.SecretsFound, narrow);

	/// <summary>
	/// Builds the page into <paramref name="list"/>, which must already be in the tree (one column on a
	/// phone, two on a PC, is read from where it is). On the menu, <paramref name="filter"/> picks the
	/// tab; over a run it is ignored, and <paramref name="currentStageId"/> is the chapter shown first.
	/// <paramref name="context"/> carries the run on the pause screen (<c>Node2DGame.SnapshotRunResult</c>)
	/// and none on the menu.
	/// </summary>
	public static Result Build(VBoxContainer list, AchievementContext context, PageMode mode, Filter filter,
		string currentStageId)
	{
		SaveData save = context?.Save ?? new SaveData();
		var page = new PageModel(save, mode == PageMode.Pause ? context?.Run : null);
		// A secret is counted by its chapter (sealed ones have no item at all); every other deed by its item.
		var deeds = page.Items.Where(i => i.Secret == null).ToList();
		int secretsFound = page.Chapters.Sum(c => c.Found);
		var result = new Result
		{
			Earned = deeds.Count(i => i.Done) + secretsFound,
			Total = deeds.Count + page.Chapters.Sum(c => c.Total),
			SecretsFound = secretsFound,
		};

		int columns = CodexRows.ColumnsFor(list);
		if (mode == PageMode.Pause)
			BuildPause(list, page, result, columns, currentStageId);
		else
			BuildMenu(list, page, filter, columns);
		return result;
	}

	// --- the tabs ------------------------------------------------------------------------------

	private static string Caption(Filter filter) => filter switch
	{
		Filter.All => "All",
		Filter.Chapters => "Chapters",
		Filter.Feats => "Feats",
		_ => "Secrets",
	};

	/// <summary>
	/// The menu's four tabs: the results and pause screens' tab (<see cref="CodexRows.StyleTab"/>), a
	/// heading you can press. Each is a focus stop; left and right walk them and stop at the ends,
	/// down enters the list (Godot's own geometry), and choosing one keeps the selection on it (4.8).
	/// Named <c>AchievementsTab&lt;Filter&gt;</c> so <c>_UiShot --press-late=AchievementsTabFeats</c> opens one.
	/// </summary>
	public static HBoxContainer BuildTabs(Filter chosen, Action<Filter> choose)
	{
		var bar = new HBoxContainer
		{
			Name = "AchievementTabs",
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		bar.AddThemeConstantOverride("separation", 6);
		var group = new ButtonGroup();
		foreach (Filter filter in Enum.GetValues<Filter>())
		{
			var tab = new Button
			{
				Name = "AchievementsTab" + filter,
				Text = Caption(filter),
				ToggleMode = true,
				ButtonGroup = group,
				ButtonPressed = filter == chosen,
				FocusMode = Control.FocusModeEnum.All,
				CustomMinimumSize = new Vector2(0f, ResponsiveLayout.MinTouchTarget),
			};
			ResponsiveLayout.SetFont(tab, ResponsiveLayout.TextRole.Title);
			CodexRows.StyleTab(tab, CodexRows.Stone);
			Filter mine = filter;
			tab.Pressed += () => choose?.Invoke(mine);
			bar.AddChild(tab);
		}
		// The ends stop rather than letting Godot's geometry carry the stick off the strip into a row
		// of the list's second column.
		if (bar.GetChildCount() > 0 && bar.GetChild(0) is Button first)
			first.FocusNeighborLeft = first.GetPathTo(first);
		if (bar.GetChildCount() > 0 && bar.GetChild(bar.GetChildCount() - 1) is Button last)
			last.FocusNeighborRight = last.GetPathTo(last);
		return bar;
	}

	/// <summary>
	/// Sizes the tabs for the canvas: on a phone they share the row's width (four short words, about
	/// 520 of the 672 units inside the margin), on a PC each is as wide as a word needs, centred.
	/// </summary>
	public static void LayoutTabs(HBoxContainer bar, bool narrow)
	{
		if (bar == null)
			return;
		foreach (Node child in bar.GetChildren())
		{
			if (child is not Button tab)
				continue;
			tab.SizeFlagsHorizontal = narrow ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkCenter;
			tab.CustomMinimumSize = new Vector2(narrow ? 0f : 150f, ResponsiveLayout.MinTouchTarget);
		}
	}

	/// <summary>
	/// Shows <paramref name="filter"/> as the chosen tab. A click or a pad's A on a toggle in a group
	/// does this by itself; a press that arrives as the signal alone (the harness's, a panel opened
	/// on a remembered tab) does not, and the strip would underline one tab over another's list.
	/// </summary>
	public static void MarkTab(HBoxContainer bar, Filter filter)
	{
		if (bar == null)
			return;
		foreach (Filter each in Enum.GetValues<Filter>())
			TabFor(bar, each)?.SetPressedNoSignal(each == filter);
	}

	/// <summary>
	/// Joins the strip to the list it chose (4.8): down from any tab enters the list at its first row,
	/// and up from the first row - either column - goes back to the CHOSEN tab. Left to geometry, up
	/// from the left column on a PC landed on All whichever tab was showing, and down from Secrets
	/// dropped into the second column. Call after every build: the rows are new each time.
	/// </summary>
	public static void LinkTabsToList(HBoxContainer bar, VBoxContainer list, Filter chosen)
	{
		Button tab = TabFor(bar, chosen);
		GridContainer first = list == null ? null : FirstRows(list);
		if (tab == null || first == null)
			return;
		var stops = new List<Control>();
		for (int i = 0; i < first.GetChildCount() && i < Math.Max(1, first.Columns); i++)
		{
			if (FirstStop(first.GetChild(i)) is Control stop)
				stops.Add(stop);
		}
		if (stops.Count == 0)
			return;
		foreach (Control stop in stops)
			stop.FocusNeighborTop = stop.GetPathTo(tab);
		foreach (Node child in bar.GetChildren())
		{
			if (child is Button each)
				each.FocusNeighborBottom = each.GetPathTo(stops[0]);
		}
	}

	private static GridContainer FirstRows(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is GridContainer grid && grid.Name == CodexRows.SectionRowsName && grid.GetChildCount() > 0)
				return grid;
			if (FirstRows(child) is GridContainer found)
				return found;
		}
		return null;
	}

	// The control a row's focus lands on: its overlay, or the first focusable thing in it.
	private static Control FirstStop(Node node)
	{
		if (node is BaseButton button && button.FocusMode != Control.FocusModeEnum.None && !button.Disabled)
			return button;
		foreach (Node child in node.GetChildren())
		{
			if (FirstStop(child) is Control stop)
				return stop;
		}
		return null;
	}

	/// <summary>The tab button for <paramref name="filter"/>, to put the selection on as the panel opens.</summary>
	public static Button TabFor(HBoxContainer bar, Filter filter) =>
		bar?.GetNodeOrNull<Button>("AchievementsTab" + filter);

	// --- the model ----------------------------------------------------------------------------------

	// One achievement on the page.
	private sealed class Item
	{
		public AchievementDefinition Deed;
		public AchievementKind Kind;
		public string StageId = string.Empty;
		public SecretDefinition Secret;
		public bool Done;
		/// <summary>A hidden one, seen and not yet found.</summary>
		public bool Seen;
		/// <summary>A deed that frees a secret wizard whose chapter has not opened.</summary>
		public bool SealedWizard;
		public AchievementProgress Progress;
		/// <summary>Pause only: "this run 4:12" beside the numbers.</summary>
		public string RunNote = string.Empty;
		/// <summary>Pause only: met on this run's numbers, paid when the run ends.</summary>
		public bool MetThisRun;
		/// <summary>"Frees Gravewell - you already had it", or null (<see cref="CodexRows.OwnedRewardLine"/>).</summary>
		public string OwnedLine;
		/// <summary>The save the page is built over.</summary>
		public SaveData Save;

		public bool IsFeat => Kind == AchievementKind.Feat || Kind == AchievementKind.Collection;
	}

	// One chapter's secrets: what the sealed count row says.
	private sealed class ChapterSecrets
	{
		public StageDefinition Stage;
		public int Total;
		public int Found;
		public List<SecretDefinition> Sealed = new();
	}

	private sealed class PageModel
	{
		public readonly SaveData Save;
		public readonly List<Item> Items = new();
		public readonly List<ChapterSecrets> Chapters = new();

		public PageModel(SaveData save, global::RunResult run)
		{
			Save = save;
			RunLens lens = run != null ? new RunLens(save, run) : null;
			AchievementContext lifetime = AchievementContext.ForSave(save);
			var chapters = new Dictionary<string, ChapterSecrets>(StringComparer.OrdinalIgnoreCase);

			foreach (AchievementDefinition deed in AchievementDefinitions.All.Where(d => IsShown(d, save)))
			{
				SecretDefinition secret = string.IsNullOrEmpty(deed.SecretId) ? null : SecretCatalog.Get(deed.SecretId);
				var item = new Item
				{
					Deed = deed,
					Save = save,
					Kind = deed.Kind,
					StageId = deed.StageId ?? string.Empty,
					Secret = secret,
				};
				item.Done = GlobalStatsManager.IsAchievementUnlocked(save, deed.Id)
					// A secret or an event is done the moment it happens (AchievementManager.CompleteNow), and
					// what it measures - the save's found list, LifetimeStats.EventsWon - is the authority
					// on that; the deed is a view of it.
					|| (deed.CompletesInRun && deed.IsComplete(lifetime));

				if (secret != null)
				{
					// A secret is counted only where the player can go: a chapter not open has nothing
					// on the page, not even its count (4.3 - "each open chapter with sealed secrets").
					bool glimpsed = save.HasGlimpsedSecret(secret.Id);
					if (!item.Done && !glimpsed && !IsOpen(save, item.StageId))
						continue;
					if (!chapters.TryGetValue(item.StageId, out ChapterSecrets chapter))
					{
						chapter = new ChapterSecrets { Stage = StageCatalog.GetById(item.StageId) };
						chapters[item.StageId] = chapter;
					}
					chapter.Total++;
					if (item.Done)
						chapter.Found++;
					else if (glimpsed)
						item.Seen = true;
					else
					{
						// Sealed: not a row of its own, only a glyph on its chapter's row.
						chapter.Sealed.Add(secret);
						continue;
					}
				}
				else if (!item.Done && AchievementDefinitions.IsSealed(deed, save))
				{
					// The character select's rule: a secret wizard is a sealed blank until his chapter opens.
					item.SealedWizard = true;
				}

				if (!item.Done && !item.SealedWizard)
				{
					item.OwnedLine = CodexRows.OwnedRewardLine(deed, save);
					if (lens != null)
						lens.Measure(item);
					else
						item.Progress = deed.GetProgress(lifetime);
				}
				Items.Add(item);
			}

			// Campaign order, then the chapters outside it - the stage list's.
			foreach (StageDefinition stage in StageCatalog.InDisplayOrder)
			{
				if (chapters.TryGetValue(stage.Id, out ChapterSecrets chapter))
					Chapters.Add(chapter);
			}
		}

		public ChapterSecrets ChapterOf(string stageId) =>
			Chapters.FirstOrDefault(c => c.Stage != null && c.Stage.Id.Equals(stageId ?? string.Empty, StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsOpen(SaveData save, string stageId) =>
		GlobalStatsManager.IsStageAvailable(save, StageCatalog.GetById(stageId));

	/// <summary>
	/// Whether the page has anything to say about a deed - more than A1's
	/// <see cref="AchievementDefinitions.IsListed(AchievementDefinition, SaveData)"/>, which answers "shown in
	/// full": a sealed secret wizard's deed is a sealed row here, and a secret in the world that has not
	/// been seen is a glyph on its chapter's row. A deed for a chapter or a secret not built yet is nothing
	/// anyone can do, so it is not on the page at all (the nine spell vaults, until their chapters' agents
	/// build them).
	/// </summary>
	private static bool IsShown(AchievementDefinition deed, SaveData save)
	{
		if (deed.Visibility == AchievementVisibility.Hidden)
			return SecretCatalog.Get(deed.SecretId)?.IsBuilt == true && IsBuilt(deed.StageId);
		return AchievementDefinitions.IsListed(deed, save)
			|| (AchievementDefinitions.IsSealed(deed, save) && IsBuilt(deed.StageId));
	}

	private static bool IsBuilt(string stageId) => StageCatalog.GetById(stageId)?.IsPlayable ?? true;

	/// <summary>
	/// The pause screen's three readings of a deed (4.4). Most deeds are measured against the save's
	/// lifetime stats, which only take a run in when it ends - so the pause tab, which built its context
	/// from the save alone, showed nothing of the run being played. Here the run so far is folded into a
	/// copy of the save the way the run's end will fold it in (<see cref="LifetimeStats.Absorb"/>), and
	/// into an empty one: the first says where a deed would stand if the run ended now (the bar), the
	/// second what this run alone did ("this run 4:12"). Survivor's clock, the Coursing's hares and Full
	/// Grimoire's slots read as this run's at last, and Exterminator's kills as the total plus this run's.
	/// Nothing is written: both copies are thrown away with the page.
	/// </summary>
	private sealed class RunLens
	{
		private readonly AchievementContext lifetime;
		private readonly AchievementContext projected;
		private readonly AchievementContext runOnly;

		public RunLens(SaveData save, global::RunResult run)
		{
			lifetime = AchievementContext.ForSave(save);
			// A copy through the save's own serialisation, so a measure that reads any of it - found
			// secrets, owned spells - reads what the real save holds. No migration runs on it.
			SaveData copy = SaveData.FromVariant(save.ToGodotDictionary());
			copy.Lifetime.Absorb(run);
			projected = AchievementContext.ForRun(copy, run);
			var alone = new SaveData();
			alone.Lifetime.Absorb(run);
			runOnly = AchievementContext.ForRun(alone, run);
		}

		public void Measure(Item item)
		{
			AchievementDefinition deed = item.Deed;
			item.Progress = deed.GetProgress(projected);
			item.MetThisRun = item.Progress.IsComplete;
			if (deed.Target <= 1f || item.MetThisRun)
				return;
			float before = deed.Measure(lifetime);
			float now = item.Progress.Current;
			float alone = deed.Measure(runOnly);
			if (alone <= 0f)
				return;
			Func<float, string> format = deed.FormatValue ?? (value => ((int)value).ToString());
			// The bar is this run's own figure (a single-run best this run has beaten): say whose it is.
			// Otherwise the bar is the best or the total, and this run's figure goes beside it.
			if (Mathf.IsEqualApprox(now, alone))
				item.RunNote = before > 0f ? "this run" : string.Empty;
			else
				item.RunNote = $"this run {format(Math.Min(alone, deed.Target))}";
		}
	}

	// --- the menu ------------------------------------------------------------------------------

	private static void BuildMenu(VBoxContainer list, PageModel page, Filter filter, int columns)
	{
		switch (filter)
		{
			case Filter.Chapters:
				BuildChapters(list, page, columns);
				break;
			case Filter.Feats:
				BuildOpenAndDone(list, page.Items.Where(i => i.IsFeat).ToList(), columns, showChapter: false);
				break;
			case Filter.Secrets:
				BuildSecrets(list, page, columns);
				break;
			default:
				BuildAll(list, page, columns);
				break;
		}
	}

	// In progress (closest first), then what is still hidden, then Done.
	private static void BuildAll(VBoxContainer list, PageModel page, int columns)
	{
		var open = page.Items.Where(i => !i.Done && !i.SealedWizard).ToList();
		if (open.Count > 0)
			CodexRows.AddSection(list, "In progress", $"{open.Count} to go", CodexRows.Stone, columns,
				OrderOpen(open).Select(i => ItemRow(i, showChapter: true)));

		var hidden = HiddenRows(page, page.Chapters, page.Items.Where(i => i.SealedWizard), out int hiddenCount);
		if (hidden.Count > 0)
			CodexRows.AddSection(list, "Not yet found", $"{hiddenCount} to find", CodexRows.Stone, columns, hidden);

		var done = page.Items.Where(i => i.Done).ToList();
		if (done.Count > 0)
			CodexRows.AddSection(list, "Done", $"{done.Count}", CodexRows.Stone, columns,
				done.Select(i => ItemRow(i, showChapter: true)));
	}

	private static void BuildOpenAndDone(VBoxContainer list, List<Item> items, int columns, bool showChapter)
	{
		var open = items.Where(i => !i.Done && !i.SealedWizard).ToList();
		var done = items.Where(i => i.Done).ToList();
		if (open.Count > 0)
			CodexRows.AddSection(list, "In progress", $"{open.Count} to go", CodexRows.Stone, columns,
				OrderOpen(open).Select(i => ItemRow(i, showChapter)));
		if (done.Count > 0)
			CodexRows.AddSection(list, "Done", $"{done.Count}", CodexRows.Stone, columns,
				done.Select(i => ItemRow(i, showChapter)));
	}

	// The world map card's "what it holds" (StageRewards), as a list: one section a chapter, in campaign
	// order - its deeds, its seen secrets, the count of the rest - with what is done last and quiet.
	private static void BuildChapters(VBoxContainer list, PageModel page, int columns)
	{
		foreach (StageDefinition stage in StageCatalog.InDisplayOrder)
		{
			var mine = page.Items.Where(i => !i.IsFeat && SameStage(i.StageId, stage.Id)).ToList();
			ChapterSecrets secrets = page.ChapterOf(stage.Id);
			if (mine.Count == 0 && secrets == null)
				continue;

			var rows = new List<Control>();
			// Its deeds before its secrets, in the order they are listed (the boss first).
			rows.AddRange(mine.Where(i => !i.Done && !i.SealedWizard && i.Secret == null).Select(i => ItemRow(i, showChapter: false)));
			rows.AddRange(mine.Where(i => i.Seen).Select(i => ItemRow(i, showChapter: false)));
			if (secrets != null && secrets.Sealed.Count > 0)
				rows.Add(SealedChapterRow(secrets));
			rows.AddRange(mine.Where(i => i.SealedWizard).Select(SealedWizardRow));
			rows.AddRange(mine.Where(i => i.Done).Select(i => ItemRow(i, showChapter: false)));

			int total = mine.Count(i => i.Secret == null) + (secrets?.Total ?? 0);
			int done = mine.Count(i => i.Done && i.Secret == null) + (secrets?.Found ?? 0);
			CodexRows.AddSection(list, stage.DisplayName, $"{done} of {total}", CodexRows.Stone, columns, rows);
		}
	}

	// Each open chapter's seen secrets and the count of its sealed ones, then what has been found.
	private static void BuildSecrets(VBoxContainer list, PageModel page, int columns)
	{
		var rows = new List<Control>();
		foreach (ChapterSecrets chapter in page.Chapters)
		{
			rows.AddRange(page.Items.Where(i => i.Seen && SameStage(i.StageId, chapter.Stage?.Id)).Select(i => ItemRow(i, showChapter: true)));
			if (chapter.Sealed.Count > 0)
				rows.Add(SealedChapterRow(chapter));
		}
		int left = page.Chapters.Sum(c => c.Total - c.Found);
		if (rows.Count > 0)
			CodexRows.AddSection(list, "Still to find", $"{left} to find", CodexRows.Stone, columns, rows);

		var done = page.Items.Where(i => i.Done && i.Secret != null).ToList();
		if (done.Count > 0)
			CodexRows.AddSection(list, "Found", $"{done.Count}", CodexRows.Stone, columns,
				done.Select(i => ItemRow(i, showChapter: true)));
	}

	// --- the pause tab -------------------------------------------------------------------------

	// No sub-tabs: it is already a tab in a tab strip. This chapter first - what this run could still
	// finish, with this run's numbers - then everything else not done, then Done behind a toggle (4.6).
	private static void BuildPause(VBoxContainer list, PageModel page, Result result, int columns, string stageId)
	{
		bool narrow = ResponsiveLayout.IsNarrow(list);
		// The subtitle leads, so the tab says what it is before it says how much of it there is.
		list.AddChild(CodexRows.BuildSummary(CodexRows.UnlocksSubtitle + "\n" + SummaryLine(result, narrow),
			CodexRows.Stone, result.Total > 0 ? result.Earned / (float)result.Total : 0f));

		StageDefinition stage = StageCatalog.GetById(stageId);
		var here = page.Items.Where(i => !i.Done && !i.IsFeat && stage != null && SameStage(i.StageId, stage.Id)).ToList();
		ChapterSecrets secrets = stage != null ? page.ChapterOf(stage.Id) : null;
		var hereRows = new List<Control>();
		hereRows.AddRange(OrderOpen(here.Where(i => !i.SealedWizard).ToList()).Select(i => ItemRow(i, showChapter: false)));
		if (secrets != null && secrets.Sealed.Count > 0)
			hereRows.Add(SealedChapterRow(secrets));
		if (hereRows.Count > 0)
		{
			int total = page.Items.Count(i => !i.IsFeat && i.Secret == null && SameStage(i.StageId, stage.Id)) + (secrets?.Total ?? 0);
			int doneHere = page.Items.Count(i => i.Done && !i.IsFeat && i.Secret == null && SameStage(i.StageId, stage.Id)) + (secrets?.Found ?? 0);
			CodexRows.AddSection(list, "This chapter", $"{doneHere} of {total} done", CodexRows.Stone, columns, hereRows);
		}

		var elsewhere = page.Items.Where(i => !i.Done && !i.SealedWizard && !here.Contains(i)).ToList();
		if (elsewhere.Count > 0)
			CodexRows.AddSection(list, "In progress", $"{elsewhere.Count} to go", CodexRows.Stone, columns,
				OrderOpen(elsewhere).Select(i => ItemRow(i, showChapter: true)));

		var done = page.Items.Where(i => i.Done).ToList();
		if (done.Count == 0)
			return;
		// Closed by default: mid-run, what is finished is the least useful thing on the tab, and it was
		// most of it. A quiet text toggle, the Run tab's "Show details", that the stick can reach - and
		// pad Y anywhere on the tab does the same (Node2DGame wires MenuBackListener.PadY to ToggleDone).
		var toggle = new Button
		{
			Name = "AchievementsDoneToggle",
			Text = DoneToggleText(false, done.Count),
			FocusMode = Control.FocusModeEnum.All,
			ProcessMode = Node.ProcessModeEnum.Always,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
		};
		ResponsiveLayout.SetFont(toggle, ResponsiveLayout.TextRole.Label);
		CodexRows.StyleQuietToggle(toggle, CodexRows.Stone);
		ResponsiveLayout.EnsureTouchTarget(toggle, 150f);
		VBoxContainer section = CodexRows.AddSection(list, "Done", string.Empty, CodexRows.Stone, columns,
			done.Select(i => ItemRow(i, showChapter: true)), toggle);
		result.DoneRows = section.GetNodeOrNull<GridContainer>(CodexRows.SectionRowsName);
		result.DoneToggle = toggle;
		result.DoneCount = done.Count;
		if (result.DoneRows != null)
			result.DoneRows.Visible = false;
		toggle.Pressed += result.ToggleDone;
	}

	private static string DoneToggleText(bool open, int count) => open ? "Hide done" : $"Show done ({count})";

	// --- rows ----------------------------------------------------------------------------------

	private static bool SameStage(string a, string b) =>
		!string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && a.Equals(b, StringComparison.OrdinalIgnoreCase);

	// Closest to done first (CodexRows.OrderInProgress); a seen secret has no numbers, so it falls in
	// with the yes/no deeds after the countable ones, in the order they are listed.
	private static List<Item> OrderOpen(List<Item> items)
	{
		var byDeed = items.ToDictionary(i => i.Deed);
		return CodexRows.OrderInProgress(items.Select(i => (i.Deed, i.Progress)))
			.Select(p => byDeed[p.Deed]).ToList();
	}

	// The not-yet-found section of the All tab: each open chapter's sealed count, then each sealed wizard.
	private static List<Control> HiddenRows(PageModel page, IEnumerable<ChapterSecrets> chapters,
		IEnumerable<Item> sealedWizards, out int count)
	{
		var rows = new List<Control>();
		count = 0;
		foreach (ChapterSecrets chapter in chapters)
		{
			if (chapter.Sealed.Count == 0)
				continue;
			rows.Add(SealedChapterRow(chapter));
			count += chapter.Sealed.Count;
		}
		foreach (Item item in sealedWizards)
		{
			rows.Add(SealedWizardRow(item));
			count++;
		}
		return rows;
	}

	private static Control ItemRow(Item item, bool showChapter)
	{
		if (item.Secret != null)
			return SecretRow(item, showChapter);

		// The save goes in so the row can say a reward is already owned, and keep a locked secret
		// wizard's name off it (CodexRows.HidesWizard).
		CodexRows.Entry entry = CodexRows.DeedEntry(item.Deed, item.Done, item.Progress, SpellIcon,
			focusStop: true, save: item.Save, palette: CodexRows.Stone);
		if (!item.Done)
		{
			if (item.MetThisRun)
			{
				// Met on this run's numbers: the run's end pays it, whichever way the run ends.
				entry.Tag = "This run";
				entry.TagGlyph = StatIcons.Load("done");
				entry.TagIsDone = true;
			}
			if (!string.IsNullOrEmpty(item.RunNote) && entry.Progress != null)
				entry.ProgressText = $"{entry.ProgressText}  ·  {item.RunNote}";
		}
		return CodexRows.BuildRow(entry, CodexRows.Stone);
	}

	// A hidden achievement, seen or found. Seen: its name (what it looks like), what was seen - an
	// observation, never an instruction - and what it pays, named; quiet, with the eye. Found: what it
	// was and what opened it, and what it paid (4.3).
	private static Control SecretRow(Item item, bool showChapter)
	{
		SecretDefinition secret = item.Secret;
		string reward = item.OwnedLine ?? item.Deed.RewardText;
		string chapter = StageCatalog.GetById(item.StageId)?.DisplayName ?? string.Empty;
		var entry = new CodexRows.Entry
		{
			Title = item.Deed.DisplayName,
			Body = item.Done ? secret.FoundLine
				: !string.IsNullOrEmpty(item.Deed.SeenLine) ? item.Deed.SeenLine : secret.SeenLine,
			Meta = showChapter && !string.IsNullOrEmpty(chapter) ? $"{chapter}  ·  {reward}" : reward,
			MetaGlyph = CodexRows.RewardGlyph(item.Deed, SpellIcon),
			MetaIsReward = true,
			Quiet = true,
			FocusStop = true,
		};
		if (item.Done)
		{
			entry.Tag = "Found";
			entry.TagGlyph = StatIcons.Load("done");
			entry.TagIsDone = true;
		}
		else
		{
			entry.Tag = "Seen";
			entry.TagGlyph = StatIcons.Load("eye");
			entry.TagIsSeen = true;
		}
		return CodexRows.BuildRow(entry, CodexRows.Stone);
	}

	// A chapter's sealed secrets, folded: one quiet row with a glyph for each showing only the kind of
	// thing it pays. No name, no place, no verb - a list of named blanks is a checklist, which is a
	// different game (.ai/stage-secrets.md 7.6).
	private static Control SealedChapterRow(ChapterSecrets chapter)
	{
		int left = chapter.Sealed.Count;
		var strip = chapter.Sealed.Select(s => CodexRows.SealedSecretGlyph(s, CodexRows.Stone)).Where(t => t != null).ToList();
		return CodexRows.BuildRow(new CodexRows.Entry
		{
			Title = chapter.Stage?.DisplayName ?? string.Empty,
			Body = left == 1 ? "There is something here you have not found." : $"There are {left} things here you have not found.",
			Strip = strip,
			Progress = chapter.Total > 0 ? chapter.Found / (float)chapter.Total : 0f,
			ProgressText = $"{chapter.Found} of {chapter.Total} found",
			Quiet = true,
			FocusStop = true,
		}, CodexRows.Stone);
	}

	// A deed that frees a secret wizard, before his chapter opens: "The Broken Circle holds someone" and
	// the character select's sealed blank. Who, and how, is the chapter's to say when it opens.
	private static Control SealedWizardRow(Item item)
	{
		return CodexRows.BuildRow(new CodexRows.Entry
		{
			// RegressionChecks (A1) holds every sealable deed to having this line.
			Title = item.Deed.SealedLine,
			Meta = "Sealed until you reach it",
			MetaGlyph = BonelightSkin.LoadTextureSafe("res://assets/bonelight/characters/sealed-portrait.png"),
			Quiet = true,
			FocusStop = true,
		}, CodexRows.Stone);
	}

	// A spell's own icon, from the spell resources (the world map reads them the same way).
	private static Texture2D SpellIcon(string spellId)
	{
		SpellData spell = ContentValidator.LoadAllSpells().FirstOrDefault(
			s => s.Id.Equals(spellId ?? string.Empty, StringComparison.OrdinalIgnoreCase));
		return spell?.Icon;
	}
}
