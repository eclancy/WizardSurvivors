using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

// The ascension browser and the level 8 choice, both drawn as TREES: the spell on top, an arrow down
// to each of its paths. See .ai/level-up-screen.md, sections 4 and 4e.
public partial class LevelUpMenu
{
	// THE BROWSER IS A FOREST OF SMALL TREES (from the Pixel 9: "ascensions menu should have a more
	// distinct UI for the options. the cards blend into the background. if there are two options
	// available for the same spell, it should show the spell on top, and two arrows pointing below to
	// the 2 ascension options available").
	//
	// It was a CodexRows checklist with one row per PATH: the two branches of one spell were two rows
	// each saying "Ascension of Cinderbreath" in small print, so that they were the same decision was
	// something to read rather than see; and a row was a gold-hi face on a flesh-hi page - one step of
	// value apart, which is what "blends into the background" was. Now, per spell:
	//   - THE SPELL is the node at the top: its icon, its name, its level as pips, and the checklist
	//     of every gate its paths SHARE (spell level, element tags, your level) - ticked when it holds,
	//     the shortfall in words on a rubric band when it does not. A gate the two paths do not share
	//     (an untagged branch of a hybrid takes either element; its tagged sibling only one) is written
	//     under the path it belongs to instead, so nothing claims to gate both when it gates one.
	//   - THE FORK (AscensionForkArrows): solid gold with a spark running down it into a path that is
	//     open, dashed in the quiet ink into one that is not.
	//   - THE PATHS are the Ascension's own object, the obsidian plate (CardFamilyArt) - the darkest
	//     thing on the lightest page, so they cannot blend into it. Ready: the lit frame. Not yet: the
	//     resting frame, dimmed. SEALED (never met - AscensionSightings): "???", no effect, the sealed
	//     glyph where the icon goes, and the plate dimmed further.
	// Trees with a ready path first, then the rest closest to ready first. It still only REPORTS: a
	// ready path is taken from the strip above the cards, never from here, so nothing on this page
	// is pressable - the plates and the node are focus stops for a pad and text to a finger.
	private const string AscensionScrollName = "AscensionScroll";

	// The ready node's face: gold hi, a step warmer and lighter than the page. Measured in
	// tools/art/ui_contrast.py (ASC_READY_FACE) - copy any change there.
	private static readonly Color AscensionReadyFace = new Color(1.0f, 0.941f, 0.753f);
	// The band an unmet gate sits on: the rubric at a tenth over the page (ASC_UNMET_BAND there).
	private static readonly Color AscensionUnmetBand = new Color(0.896f, 0.789f, 0.708f);

	// How far a path's plate is dimmed, as a SelfModulate - the frame only, never the words on it, so
	// greying a plate can only darken the ground under light ink (ui_contrast.py, "ascension tree").
	private static readonly Color PathSeenDim = new Color(0.78f, 0.78f, 0.84f);
	private static readonly Color PathSealedDim = new Color(0.56f, 0.56f, 0.64f);

	private const string SealedName = "???";
	private const string SealedLine = "Sealed until you first meet its requirements.";

	// The plates, built once per menu (instance fields, never static - CardFamilyArt's remarks). The
	// content margin is 0 because the padding is an inner MarginContainer: the focus overlay is a
	// sibling of it and has to cover the whole plate, or its ring sits 20 inside the edge.
	private StyleBoxTexture treePlateResting, treePlateLit;

	private (StyleBoxTexture resting, StyleBoxTexture lit) TreePlates()
	{
		treePlateResting ??= CardFamilyArt.Frame(CardFamily.Ascension, false, 0);
		treePlateLit ??= CardFamilyArt.Frame(CardFamily.Ascension, true, 0);
		return (treePlateResting, treePlateLit);
	}

	/// <summary>One path of a tree, as either page needs it.</summary>
	private sealed class TreePath
	{
		public string Id = string.Empty;
		public string Name = string.Empty;
		public string Description = string.Empty;
		public Texture2D Icon;
		/// <summary>Takeable now (browser: every gate holds; choice page: not locked).</summary>
		public bool Met;
		/// <summary>Met now, or met once before in any run - its words and icon may be shown.</summary>
		public bool Revealed;
		/// <summary>The gates this path does not share with its siblings (browser only).</summary>
		public List<AscensionRequirement> OwnGates = new();
		/// <summary>Strings only, for a caller that filled no structured gates (the probe scenes).</summary>
		public List<string> Unmet = new();
	}

	private void BuildAscensionBrowser()
	{
		var panel = GetNodeOrNull<Control>("Panel");
		if (panel == null)
			return;

		ClearButtons();
		SetNormalViewVisible(false);
		RemoveAscensionRoot();

		// Same register as the evolution screen: this is reading ABOUT magic, so it is a page.
		ShowVellumPage(true);

		bool narrow = ResponsiveLayout.IsNarrow(this);
		CodexRows.Palette ink = CodexRows.Vellum;

		var root = new VBoxContainer { Name = AscensionRootName, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.OffsetLeft = narrow ? 30 : 44;
		root.OffsetTop = 28;
		root.OffsetRight = narrow ? -30 : -44;
		root.OffsetBottom = -24;
		root.AddThemeConstantOverride("separation", 14);
		panel.AddChild(root);

		var title = new Label
		{
			Text = "★ Ascensions",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		ResponsiveLayout.SetFont(title, ResponsiveLayout.TextRole.Display);
		title.AddThemeColorOverride("font_color", InkGold);
		root.AddChild(title);

		// The rule, once. What a sealed path is, each sealed plate says for itself (SealedLine), so the
		// summary stays one line on a PC.
		root.AddChild(CodexRows.BuildSummary(
			$"Each needs its spell at level {Player.AscensionSpellLevel - 1}, {Player.AscensionElementRequirement} tags of its element " +
			$"and you at level {Player.AscensionMinimumPlayerLevel}. A ready one waits above the cards.",
			ink));

		var list = CodexRows.BuildList("AscensionList");
		ScrollContainer scroll = CodexRows.BuildScroll(AscensionScrollName, list);
		root.AddChild(scroll);

		var spells = currentEquippedSpells ?? new List<EquippedSpellInfo>();
		if (spells.Count == 0)
		{
			list.AddChild(CodexRows.MakeText("Your tome is empty. Take a spell and come back.",
				ResponsiveLayout.TextRole.Body, ink.InkSoft));
		}

		var trees = new List<(EquippedSpellInfo Spell, List<TreePath> Paths, List<AscensionRequirement> Shared, int Order)>();
		var noAscension = new List<EquippedSpellInfo>();
		foreach (EquippedSpellInfo spell in spells)
		{
			if (spell.Ascensions == null || spell.Ascensions.Count == 0)
			{
				noAscension.Add(spell);
				continue;
			}
			(List<TreePath> paths, List<AscensionRequirement> shared) = BrowserPaths(spell);
			trees.Add((spell, paths, shared, trees.Count));
		}

		// Ready first; then by the fewest failing gates on the spell's nearest path, then by how far
		// along that path is, so the near misses sit at the top of "Not yet" and the long way off at
		// the bottom.
		var ready = trees.Where(t => t.Paths.Any(p => p.Met)).ToList();
		var waiting = trees.Where(t => !t.Paths.Any(p => p.Met))
			.OrderBy(t => t.Spell.Ascensions.Min(a => a.UnmetRequirements.Count))
			.ThenByDescending(t => t.Spell.Ascensions.Max(Progress))
			.ThenBy(t => t.Order)
			.ToList();

		// Focus stops, tree by tree, wired top to bottom once the whole page exists (WireTreeFocus).
		var stops = new List<(List<Control> Node, List<Control> Paths)>();
		Control BuildOne((EquippedSpellInfo Spell, List<TreePath> Paths, List<AscensionRequirement> Shared, int Order) t)
		{
			var nodeStops = new List<Control>();
			var pathStops = new List<Control>();
			Control tree = BuildBrowserTree(t.Spell, t.Paths, t.Shared, narrow, nodeStops, pathStops);
			stops.Add((nodeStops, pathStops));
			return tree;
		}

		// One tree per row in both shapes. Thirty between trees, against six inside one: the space
		// is what says where one spell's fork ends and the next spell begins.
		const int columns = 1;
		if (ready.Count > 0)
			SpaceTrees(CodexRows.AddSection(list, "Ready", SpellCount(ready.Count), ink, columns, ready.Select(BuildOne).ToList()));
		if (waiting.Count > 0)
			SpaceTrees(CodexRows.AddSection(list, ready.Count > 0 ? "Not yet" : "Ascensions to earn",
				SpellCount(waiting.Count), ink, columns, waiting.Select(BuildOne).ToList()));

		if (noAscension.Count > 0)
		{
			// One quiet line, not a section per passive: saying so is the point (a spell missing from
			// the list would read as an oversight), but it is not something to act on.
			var passives = noAscension.Where(s => s.IsPassive).Select(s => s.DisplayName).ToList();
			var unwritten = noAscension.Where(s => !s.IsPassive).Select(s => s.DisplayName).ToList();
			var parts = new List<string>();
			if (passives.Count > 0)
				parts.Add($"{string.Join(", ", passives)} {(passives.Count == 1 ? "is a passive" : "are passives")} and {(passives.Count == 1 ? "does" : "do")} not ascend.");
			if (unwritten.Count > 0)
				parts.Add($"No ascension is written yet for {string.Join(", ", unwritten)}.");
			list.AddChild(CodexRows.MakeText(string.Join(" ", parts), ResponsiveLayout.TextRole.Label, ink.InkMeta));
		}

		var backButton = new Button
		{
			Name = "AscensionBack",
			Text = "Back",
			CustomMinimumSize = new Vector2(120, ResponsiveLayout.MinTouchTarget),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
		};
		ResponsiveLayout.SetFont(backButton, ResponsiveLayout.TextRole.Label);
		backButton.Pressed += () => BuildButtonsFrom(currentOptions);
		BonelightSkin.StyleButton(backButton);
		root.AddChild(backButton);

		WireTreeFocus(stops, backButton);

		// The cards this screen just replaced were what the pad was pointing at.
		MenuNavigator.Attach(this, backButton);
	}

	private static string SpellCount(int n) => n == 1 ? "1 spell" : $"{n} spells";

	private static void SpaceTrees(VBoxContainer section)
	{
		if (section?.GetChildCount() > 0 && section.GetChild(section.GetChildCount() - 1) is GridContainer grid)
			grid.AddThemeConstantOverride("v_separation", 30);
	}

	/// <summary>
	/// A spell's paths for the browser, and the gates they all share. A gate is shared when every path
	/// has one of its kind measuring the same thing (same have, need, element and choice of elements);
	/// the spell and player levels always are, an element gate is unless the paths ask for different
	/// elements.
	/// </summary>
	private (List<TreePath> Paths, List<AscensionRequirement> Shared) BrowserPaths(EquippedSpellInfo spell)
	{
		var branches = spell.Ascensions.Where(a => a != null).ToList();
		var shared = new List<AscensionRequirement>();
		List<AscensionRequirement> first = branches.FirstOrDefault()?.Requirements ?? new List<AscensionRequirement>();
		foreach (AscensionRequirement gate in first)
		{
			if (branches.All(b => (b.Requirements ?? new List<AscensionRequirement>()).Any(r => SameGate(r, gate))))
				shared.Add(gate);
		}

		var paths = new List<TreePath>();
		foreach (AscensionInfo branch in branches)
		{
			var path = new TreePath
			{
				Id = branch.Id,
				Name = branch.DisplayName,
				Description = branch.Description,
				Icon = branch.Icon ?? spell.Icon,
				Met = branch.IsAvailable,
				Revealed = AscensionSightings.IsRevealed(this, spell.Id, branch.Id, branch.IsAvailable),
				Unmet = branch.UnmetRequirements ?? new List<string>(),
			};
			foreach (AscensionRequirement gate in branch.Requirements ?? new List<AscensionRequirement>())
				if (!shared.Any(s => SameGate(s, gate)))
					path.OwnGates.Add(gate);
			paths.Add(path);
		}
		return (paths, shared);
	}

	private static bool SameGate(AscensionRequirement a, AscensionRequirement b)
	{
		if (a == null || b == null || a.Kind != b.Kind || a.Have != b.Have || a.Need != b.Need || a.Element != b.Element)
			return false;
		var x = a.AnyOf ?? new List<Element>();
		var y = b.AnyOf ?? new List<Element>();
		return x.Count == y.Count && x.All(y.Contains);
	}

	// 0..1, how much of the failing gates is already done - the sort key inside "Not yet".
	private static float Progress(AscensionInfo branch)
	{
		if (branch.Requirements == null || branch.Requirements.Count == 0)
			return 0f;
		return branch.Requirements.Average(r => r.Need <= 0 ? 1f : Mathf.Clamp((float)r.Have / r.Need, 0f, 1f));
	}

	/// <summary>One spell's tree in the browser: the spell, the fork, and its paths.</summary>
	private Control BuildBrowserTree(EquippedSpellInfo spell, List<TreePath> paths, List<AscensionRequirement> shared,
		bool narrow, List<Control> nodeStops, List<Control> pathStops)
	{
		bool anyMet = paths.Any(p => p.Met);
		int cap = shared.FirstOrDefault(r => r.Kind == AscensionRequirementKind.SpellLevel)?.Need ?? Player.AscensionSpellLevel - 1;

		// The checklist: every shared gate, or - for a caller that filled only the strings - every
		// string any path still fails.
		var lines = new List<Control>();
		foreach (AscensionRequirement gate in shared)
			lines.Add(BuildRequirementLine(gate, wrap: narrow));
		if (shared.Count == 0)
		{
			foreach (string unmet in paths.SelectMany(p => p.Unmet).Distinct())
				lines.Add(UnmetBand(BuildRequirementGlyphText(unmet, "todo", CodexRows.Vellum.Flag, expand: narrow), narrow));
		}

		var node = BuildTreeNode(spell.Id, spell.DisplayName, spell.Icon,
			$"Level {Mathf.Min(spell.CurrentLevel, cap)}", cap, Mathf.Min(spell.CurrentLevel, cap), -1,
			anyMet, lines, narrow, focusStop: true, nodeStops);

		bool sideBySide = FitsAcross(paths.Count, narrow, browser: true);
		// Side by side on a phone a plate is about 240 wide: its sentence is set a step smaller.
		bool small = narrow && sideBySide;
		var cards = new List<(Control Outer, Control Plate)>();
		foreach (TreePath path in paths)
		{
			(Control outer, Control plate, Control stop) = BuildBrowserPathCard(spell, path, small);
			cards.Add((outer, plate));
			pathStops.Add(stop);
		}
		return BuildTree($"AscensionTree_{spell.Id}", node, cards, paths.Select(p => p.Met).ToList(), narrow, sideBySide);
	}

	/// <summary>
	/// The spell at the top of a tree: icon, name, its level as pips, and - in the browser - the
	/// checklist of gates its paths share.
	/// </summary>
	/// <param name="pipBecoming">The 0-based pip this choice reaches, animated (LevelPips); -1 for none.</param>
	/// <param name="stops">Filled with the node's focus stops: its element chips, or one overlay.</param>
	private Control BuildTreeNode(string spellId, string name, Texture2D icon, string levelText, int pipCount, int pipFilled,
		int pipBecoming, bool ready, List<Control> checklist, bool narrow, bool focusStop, List<Control> stops)
	{
		CodexRows.Palette ink = CodexRows.Vellum;
		var node = new PanelContainer
		{
			Name = $"AscensionNode_{spellId}",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		// A solid edge in the darkest ink, not the 45% one a quiet Codex row has: the node is the root
		// of a figure, and a root you have to squint for is not one. Ready, it takes the gold face.
		var style = new StyleBoxFlat
		{
			BgColor = ready ? AscensionReadyFace : ink.Face,
			BorderColor = ready ? InkGold : ink.Ink,
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(6);
		style.SetContentMarginAll(0);
		node.AddThemeStyleboxOverride("panel", style);

		bool hasChips = checklist.Any(c => FindChips(c).Any());
		if (focusStop && !hasChips)
		{
			// First child, so the content draws over its wash; it ignores the mouse - to a finger the
			// node is text, and a drag that starts on it must reach the scroll.
			Button overlay = BonelightSkin.MakeCardClickOverlay(true);
			overlay.Name = "NodeFocus";
			overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
			node.AddChild(overlay);
			stops.Add(overlay);
		}

		var padding = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		padding.AddThemeConstantOverride("margin_left", 16);
		padding.AddThemeConstantOverride("margin_right", 16);
		padding.AddThemeConstantOverride("margin_top", 12);
		padding.AddThemeConstantOverride("margin_bottom", 14);
		node.AddChild(padding);

		var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", 6);
		padding.AddChild(column);

		var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		head.AddThemeConstantOverride("separation", 14);
		column.AddChild(head);
		if (icon != null)
		{
			head.AddChild(new TextureRect
			{
				Texture = icon,
				CustomMinimumSize = new Vector2(48, 48),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			});
		}

		// On a PC the checklist shares this row and takes the width left over, so the node is one
		// band - about 90 tall rather than 170 - and the paths under it start above the fold of a
		// 1280x720 screen. On a phone it gets lines of its own under the name.
		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = narrow ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		identity.AddThemeConstantOverride("separation", 2);
		head.AddChild(identity);
		Label title = CodexRows.MakeText(name, ResponsiveLayout.TextRole.Title, ink.Ink);
		// Never wrapped: beside the checklist flow it would otherwise give up its width a letter
		// at a time ("Cinderbre / ath"). The flow is what wraps.
		title.AutowrapMode = TextServer.AutowrapMode.Off;
		identity.AddChild(title);

		var levelRow = new HBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
		};
		levelRow.AddThemeConstantOverride("separation", 10);
		Label level = CodexRows.MakeText(levelText, ResponsiveLayout.TextRole.Label, ink.InkMeta);
		level.AutowrapMode = TextServer.AutowrapMode.Off;
		levelRow.AddChild(level);
		if (pipCount > 0)
		{
			levelRow.AddChild(new LevelPips
			{
				Count = pipCount,
				Filled = Mathf.Clamp(pipFilled, 0, pipCount),
				Becoming = pipBecoming,
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			});
		}
		// With no checklist on a PC - the choice page - the level sits beside the name and the node is
		// one row, which on a 720-high canvas is a line of the plates under it above the fold.
		if (!narrow && checklist.Count == 0)
			head.AddChild(levelRow);
		else
			identity.AddChild(levelRow);

		if (checklist.Count > 0)
		{
			// Down the node on a phone, where width is scarce; across it on a PC, where height is.
			Container lines = narrow
				? new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }
				: new HFlowContainer
				{
					MouseFilter = Control.MouseFilterEnum.Ignore,
					SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
					SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
				};
			lines.AddThemeConstantOverride("separation", 4);
			lines.AddThemeConstantOverride("h_separation", 12);
			lines.AddThemeConstantOverride("v_separation", 6);
			foreach (Control line in checklist)
				lines.AddChild(line);
			if (narrow)
			{
				var gap = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
				gap.AddThemeConstantOverride("margin_top", 4);
				gap.AddChild(lines);
				column.AddChild(gap);
			}
			else
			{
				head.AddChild(lines);
			}
			stops.AddRange(checklist.SelectMany(FindChips));
		}
		else if (!narrow)
		{
			head.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
		}

		// "Ready" at the row's end. The line that used to follow it - "Waiting at the top of every
		// level-up" - is said once, in the page's summary, rather than on every ready spell.
		if (ready)
		{
			Control tag = BuildRequirementGlyphText("Ready", "done", ink.Reward, expand: false);
			tag.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			head.AddChild(tag);
		}

		return node;
	}

	private static IEnumerable<Control> FindChips(Node node)
	{
		if (node is ElementChip chip)
			yield return chip;
		foreach (Node child in node.GetChildren())
			foreach (Control found in FindChips(child))
				yield return found;
	}

	/// <summary>
	/// One path in the browser: the obsidian plate, never pressable. Returns what the row lays out
	/// (the plate, with this path's own gates under it when it has any), the plate itself (what the
	/// fork points at), and its focus stop.
	/// </summary>
	private (Control Outer, Control Plate, Control Stop) BuildBrowserPathCard(EquippedSpellInfo spell, TreePath path, bool small)
	{
		(StyleBoxTexture resting, StyleBoxTexture lit) = TreePlates();
		var plate = new PanelContainer
		{
			Name = $"AscensionPath_{spell.Id}_{path.Id}",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			// The frame only: a dimmed plate darkens the ground, never the words on it.
			SelfModulate = path.Met ? Colors.White : path.Revealed ? PathSeenDim : PathSealedDim,
		};
		// Ready, the lit frame - brighter gilt and runes, the same face - so an open path is lit from
		// across the page.
		plate.AddThemeStyleboxOverride("panel", path.Met ? lit : resting);

		// A controller scrolls a list only by moving the selection through it: one stop per plate.
		// It does nothing when pressed (the strip is where an ascension is taken) and ignores the mouse.
		Button stop = BonelightSkin.MakeCardClickOverlay(true);
		stop.Name = "PathFocus";
		stop.MouseFilter = Control.MouseFilterEnum.Ignore;
		plate.AddChild(stop);

		plate.AddChild(BuildPathContent(path.Revealed, path.Met, path.Icon, Colors.White, path.Name, path.Description,
			extras: null, small));

		if (path.OwnGates.Count == 0)
			return (plate, plate, stop);

		// This path's own gate, under its plate, written exactly as the node's lines are.
		var outer = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		outer.AddThemeConstantOverride("separation", 6);
		outer.AddChild(plate);
		foreach (AscensionRequirement gate in path.OwnGates)
			outer.AddChild(BuildRequirementLine(gate, wrap: true));
		return (outer, plate, stop);
	}

	/// <summary>
	/// What is written on a path's plate: icon, name and what it does - or, sealed, the seal, "???"
	/// and one line saying why. <paramref name="extras"/> (the choice page's tag, element line and
	/// advice) are left out of a sealed path, since each would give part of it away.
	/// </summary>
	private Control BuildPathContent(bool revealed, bool met, Texture2D icon, Color iconTint, string name, string description,
		IEnumerable<Control> extras, bool small)
	{
		// Clear of the plate's rune band and the gilt line inside it (18 drawn pixels), with air.
		var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		margin.AddThemeConstantOverride("margin_left", 22);
		margin.AddThemeConstantOverride("margin_right", 22);
		margin.AddThemeConstantOverride("margin_top", 20);
		margin.AddThemeConstantOverride("margin_bottom", 20);

		const float iconSide = 48f;
		Control picture = revealed
			? new TextureRect
			{
				Texture = icon ?? DefaultSpellIcon,
				CustomMinimumSize = new Vector2(iconSide, iconSide),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
				SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				// Faded toward the glass while it is out of reach - never darkened, which on obsidian
				// would simply erase it.
				Modulate = met ? iconTint : new Color(iconTint, 0.55f),
			}
			: new TextureRect
			{
				// The 24-pixel sealed glyph at exactly x2, so it stays on whole pixels.
				Texture = StatIcons.Load("sealed"),
				CustomMinimumSize = new Vector2(48, 48),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
				SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};

		// The picture beside the NAME only, and the words full width under both: a picture down the
		// side of every line cost a 1280x720 PC a line of description per plate, which was the line
		// that put the lock band under the fold.
		var words = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		words.AddThemeConstantOverride("separation", 6);
		margin.AddChild(words);

		Label title = CodexRows.MakeText(revealed ? name : SealedName, ResponsiveLayout.TextRole.Title, CardFamilyArt.AscensionTitle);
		title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		if (small)
		{
			// A phone's side-by-side plate is about 240 wide: beside the picture the name had 136, and
			// "Cataclysm" at the Title size broke mid-word. The picture goes above it instead.
			words.AddChild(picture);
			words.AddChild(title);
		}
		else
		{
			var titleRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			titleRow.AddThemeConstantOverride("separation", 12);
			picture.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			titleRow.AddChild(picture);
			titleRow.AddChild(title);
			words.AddChild(titleRow);
		}

		if (revealed)
		{
			if (!string.IsNullOrWhiteSpace(description))
				words.AddChild(CodexRows.MakeText(description,
					small ? ResponsiveLayout.TextRole.Label : ResponsiveLayout.TextRole.Body, CardFamilyArt.AscensionInk));
			foreach (Control extra in extras ?? Enumerable.Empty<Control>())
				words.AddChild(extra);
		}
		else
		{
			words.AddChild(CodexRows.MakeText(SealedLine, ResponsiveLayout.TextRole.Label, CardFamilyArt.AscensionSoft));
		}
		return margin;
	}

	/// <summary>
	/// Whether <paramref name="count"/> plates fit side by side under the node at their smallest
	/// readable width - if not, they stack and the fork becomes a spine. Measured from the panel as
	/// laid out, less the page's inset, the content's margins and the scroll gutter.
	/// </summary>
	private bool FitsAcross(int count, bool narrow, bool browser)
	{
		float panelWidth = GetNodeOrNull<Control>("Panel")?.Size.X ?? 0f;
		if (panelWidth <= 0f)
			panelWidth = ResponsiveLayout.ViewportSize(this).X - 24f;
		float side = browser ? (narrow ? 30f : 44f) : 24f;
		float width = panelWidth - 2f * PageInset - 2f * side - (browser ? 20f : 0f);
		float gap = narrow ? 12f : 20f;
		float each = count <= 0 ? width : (width - (count - 1) * gap) / count;
		// A Title-role name and a sentence a line need about this much plate, frame included.
		float least = narrow ? 240f : 300f;
		return each >= least;
	}

	/// <summary>
	/// The figure: the node, the fork, the plates. On a PC the node sits centred over the plates and
	/// a little narrower than them, so the shape is a fork rather than a heading over two boxes; on a
	/// phone it takes the full width, which is the scarce axis there.
	/// </summary>
	private Control BuildTree(string name, Control node, List<(Control Outer, Control Plate)> cards, List<bool> open,
		bool narrow, bool sideBySide)
	{
		var tree = new VBoxContainer
		{
			Name = name,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		tree.AddThemeConstantOverride("separation", 0);

		if (narrow)
		{
			tree.AddChild(node);
		}
		else
		{
			var nodeRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			nodeRow.AddThemeConstantOverride("separation", 0);
			nodeRow.AddChild(Spacer(0.07f));
			node.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			node.SizeFlagsStretchRatio = 0.86f;
			nodeRow.AddChild(node);
			nodeRow.AddChild(Spacer(0.07f));
			tree.AddChild(nodeRow);
		}

		var arrows = new AscensionForkArrows
		{
			Name = "Fork",
			Shape = sideBySide ? AscensionForkArrows.Layout.Fork : AscensionForkArrows.Layout.Spine,
			Source = node,
		};
		for (int i = 0; i < cards.Count; i++)
			arrows.AddTarget(cards[i].Plate, i < open.Count && open[i]);

		if (sideBySide)
		{
			arrows.CustomMinimumSize = new Vector2(0, narrow ? 48 : 44);
			tree.AddChild(arrows);
			var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			row.AddThemeConstantOverride("separation", narrow ? 12 : 20);
			// A lone path keeps half the width, centred under the node, rather than becoming a slab.
			if (cards.Count == 1)
				row.AddChild(Spacer(0.25f));
			foreach ((Control outer, Control _) in cards)
			{
				outer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				outer.SizeFlagsStretchRatio = cards.Count == 1 ? 0.5f : 1f;
				row.AddChild(outer);
			}
			if (cards.Count == 1)
				row.AddChild(Spacer(0.25f));
			tree.AddChild(row);
		}
		else
		{
			var body = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			body.AddThemeConstantOverride("separation", 0);
			arrows.CustomMinimumSize = new Vector2(40, 0);
			body.AddChild(arrows);
			var column = new VBoxContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			column.AddThemeConstantOverride("separation", 14);
			foreach ((Control outer, Control _) in cards)
				column.AddChild(outer);
			// So the spine has length before its first turn.
			var drop = new MarginContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			drop.AddThemeConstantOverride("margin_top", 16);
			drop.AddChild(column);
			body.AddChild(drop);
			tree.AddChild(body);
		}
		return tree;
	}

	private static Control Spacer(float ratio) => new Control
	{
		SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		SizeFlagsStretchRatio = ratio,
		MouseFilter = Control.MouseFilterEnum.Ignore,
	};

	/// <summary>
	/// Spell, then left path, then right path, then the next spell: the order the figure is read in,
	/// set explicitly rather than left to geometry, which from a node centred over two plates could
	/// land on either.
	/// </summary>
	private static void WireTreeFocus(List<(List<Control> Node, List<Control> Paths)> trees, Control after)
	{
		static void Link(Control from, Control to, Side side)
		{
			if (from == null || to == null || from == to || !from.IsInsideTree() || !to.IsInsideTree())
				return;
			NodePath path = from.GetPathTo(to);
			switch (side)
			{
				case Side.Top: from.FocusNeighborTop = path; break;
				case Side.Bottom: from.FocusNeighborBottom = path; break;
				case Side.Left: from.FocusNeighborLeft = path; break;
				case Side.Right: from.FocusNeighborRight = path; break;
			}
		}

		static void Order(Control from, Control to)
		{
			if (from == null || to == null || !from.IsInsideTree() || !to.IsInsideTree())
				return;
			from.FocusNext = from.GetPathTo(to);
			to.FocusPrevious = to.GetPathTo(from);
		}

		Control previousLast = null;
		List<Control> previousPaths = null;
		for (int t = 0; t < trees.Count; t++)
		{
			List<Control> node = trees[t].Node;
			List<Control> paths = trees[t].Paths;
			Control firstNode = node.FirstOrDefault();
			Control firstPath = paths.FirstOrDefault();

			for (int i = 0; i + 1 < node.Count; i++)
				Order(node[i], node[i + 1]);
			foreach (Control stop in node)
			{
				Link(stop, firstPath, Side.Bottom);
				if (previousPaths != null && previousPaths.Count > 0)
					Link(stop, previousPaths[0], Side.Top);
			}
			for (int i = 0; i < paths.Count; i++)
			{
				if (i > 0)
				{
					Link(paths[i], paths[i - 1], Side.Left);
					Link(paths[i - 1], paths[i], Side.Right);
					Order(paths[i - 1], paths[i]);
				}
				Link(paths[i], firstNode ?? firstPath, Side.Top);
			}
			if (node.Count > 0 && firstPath != null)
				Order(node[^1], firstPath);
			if (previousLast != null && (firstNode ?? firstPath) != null)
				Order(previousLast, firstNode ?? firstPath);
			if (previousPaths != null)
				foreach (Control stop in previousPaths)
					Link(stop, firstNode ?? firstPath, Side.Bottom);

			previousLast = paths.LastOrDefault() ?? node.LastOrDefault() ?? previousLast;
			previousPaths = paths.Count > 0 ? paths : previousPaths;
		}
		if (previousPaths != null && after != null)
			foreach (Control stop in previousPaths)
				Link(stop, after, Side.Bottom);
	}

	// ---- the level 8 choice: one spell's fork ---------------------------------------------------
	//
	// The same figure as the browser, for the one spell being ascended: the spell on top with its
	// eighth pip breathing (LevelPips - the level this choice makes it), the fork, and the paths as
	// obsidian plates that ARE the buttons. A path whose element gate is not met is shown locked,
	// with what it wants on a band under it and a dashed arrow into it - and if the player has never
	// met it, sealed as well, since this is the same "has the player stood here" rule the browser
	// follows. Only here does a tap choose.
	private void BuildAscensionChoicePage(LevelUpOption option)
	{
		var panel = GetNodeOrNull<Control>("Panel");
		if (panel == null)
			return;

		ClearButtons();
		SetNormalViewVisible(false);
		// A viewport resize re-invokes this while the view is already up (OnViewportSizeChanged).
		RemoveEvolutionRoot();
		ShowVellumPage(true);

		bool narrow = ResponsiveLayout.IsNarrow(this);
		var root = new VBoxContainer { Name = EvolutionRootName };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.OffsetLeft = 24;
		root.OffsetTop = PageInset + 6f;
		root.OffsetRight = -24;
		root.OffsetBottom = -PageInset;
		root.AddThemeConstantOverride("separation", 6);
		panel.AddChild(root);

		var title = new Label
		{
			Text = "★ ASCENSION (Level 8) ★",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		ResponsiveLayout.SetFont(title, narrow ? ResponsiveLayout.TextRole.Title : ResponsiveLayout.TextRole.Display);
		title.AddThemeColorOverride("font_color", HeadingInkFor(CardFamily.Ascension));
		root.AddChild(title);

		// Counted, never asserted (the old page said "1 of 3" to a two-branch spell). The stakes in
		// the same breath: level 8 is reached only through one of these, so the other is gone.
		int count = option.EvolutionChoices?.Count ?? 0;
		var subtitle = new Label
		{
			Text = count == 1
				? $"One path ascends {option.DisplayName} to level 8."
				: $"Choose 1 of {count} paths. {option.DisplayName} takes only one, for the rest of this run.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		ResponsiveLayout.SetBodyText(subtitle);
		subtitle.AddThemeColorOverride("font_color", InkSoft);
		root.AddChild(subtitle);

		// A scroll, as before: on a 1280x720 PC a long description can still outgrow the band. Not
		// FollowFocus on a PC - focus moves sideways between plates there, and following it opened a
		// tall page scrolled to a plate's foot - but on a phone, where a stick can walk down a stack.
		var scroll = new ScrollContainer
		{
			Name = "AscensionChoiceScroll",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			FollowFocus = narrow,
		};
		root.AddChild(scroll);
		var inner = new MarginContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		inner.AddThemeConstantOverride("margin_top", 8);
		inner.AddThemeConstantOverride("margin_bottom", 8);
		scroll.AddChild(inner);

		int to = Mathf.Max(option.NextLevel, Player.AscensionSpellLevel);
		var noStops = new List<Control>();
		Control node = BuildTreeNode(option.SpellId, option.DisplayName, option.Icon, $"Level {to - 1} → {to}",
			to, to - 1, to - 1, ready: true, new List<Control>(), narrow, focusStop: false, noStops);

		bool sideBySide = FitsAcross(count, narrow, browser: false);
		bool small = narrow && sideBySide;
		var cards = new List<(Control Outer, Control Plate)>();
		var buttons = new List<Button>();
		var open = new List<bool>();
		Button firstOpen = null;
		foreach (SpellEvolutionOption evo in option.EvolutionChoices ?? new List<SpellEvolutionOption>())
		{
			string reason = null;
			option.EvolutionLockReason?.TryGetValue(evo.Id, out reason);
			bool locked = !string.IsNullOrEmpty(reason);
			bool revealed = AscensionSightings.IsRevealed(this, option.SpellId, evo.Id, metNow: !locked);
			(Control outer, Control plate, Button button) = BuildChoicePathCard(option, evo, reason, revealed, small);
			cards.Add((outer, plate));
			buttons.Add(button);
			open.Add(!locked);
			if (!locked)
				firstOpen ??= button;
		}
		inner.AddChild(BuildTree($"AscensionTree_{option.SpellId}", node, cards, open, narrow, sideBySide));

		var backButton = new Button
		{
			Name = "AscensionChoiceBack",
			Text = "Back",
			CustomMinimumSize = new Vector2(104, 48),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
		};
		ResponsiveLayout.SetFont(backButton, ResponsiveLayout.TextRole.Label);
		backButton.Pressed += () =>
		{
			pendingEvolutionOption = null;
			BuildButtonsFrom(currentOptions);
		};
		BonelightSkin.StyleButton(backButton);
		root.AddChild(backButton);

		// Left path, right path, Back: the node has nothing to press here, so it is not a stop.
		var pathStops = buttons.Where(b => !b.Disabled).Cast<Control>().ToList();
		WireTreeFocus(new List<(List<Control>, List<Control>)> { (new List<Control>(), pathStops) }, backButton);
		if (pathStops.Count > 0)
			backButton.FocusNeighborTop = backButton.GetPathTo(pathStops[0]);

		// The open path is the primary action. MenuNavigator rescans every quarter second, so this
		// only decides where the selection starts.
		MenuNavigator.Attach(this, (Control)firstOpen ?? backButton);
	}

	/// <summary>
	/// A path on the level 8 page: the obsidian plate as the button. A PanelContainer holding the
	/// Button and the words as siblings, the phone card's build, so the plate grows to its text and
	/// is one target for a thumb and one ring for a stick.
	/// </summary>
	private (Control Outer, Control Plate, Button Button) BuildChoicePathCard(LevelUpOption option, SpellEvolutionOption evo,
		string lockReason, bool revealed, bool small)
	{
		bool locked = !string.IsNullOrEmpty(lockReason);
		var plate = new PanelContainer
		{
			Name = $"AscendPath_{evo.Id}",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		plate.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

		(StyleBoxTexture resting, StyleBoxTexture lit) = TreePlates();
		var card = new Button
		{
			Name = $"AscendCard_{evo.Id}",
			Text = string.Empty,
			ClipText = false,
			Disabled = locked,
			// The frame only, as on the browser's plates.
			SelfModulate = locked ? (revealed ? PathSeenDim : PathSealedDim) : Colors.White,
		};
		CardFamilyArt.ApplyFrame(card, resting, lit);
		card.Pressed += () => OnEvolutionChosen(option, evo);
		plate.AddChild(card);

		// The tag, which way its element goes, and the advice - each a piece of what the path IS, so a
		// sealed path gets none of them.
		var extras = new List<Control>();
		if (revealed)
		{
			if (!string.IsNullOrWhiteSpace(evo.SynergyTag))
				extras.Add(BuildSynergyChip(evo.SynergyTag, CardFamily.Ascension, Control.SizeFlags.ShrinkBegin));
			Label elements = BuildEvolutionElementLine(evo, option, HorizontalAlignment.Left, CardFamily.Ascension);
			if (elements != null)
				extras.Add(elements);
			if (!string.IsNullOrWhiteSpace(evo.SynergyDescription))
				extras.Add(CodexRows.MakeText($"💡 {evo.SynergyDescription}", ResponsiveLayout.TextRole.Label,
					CardFamilyArt.AscensionAdvice));
		}

		plate.AddChild(BuildPathContent(revealed, !locked, evo.Icon ?? option.Icon, evo.ModulateColor, evo.DisplayName,
			evo.Description, extras, small));

		if (!locked)
			return (plate, plate, card);

		// What the locked path wants, on a band under it: bone on red (10.1:1), as on the old page.
		var stamp = new Label
		{
			Text = lockReason,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		ResponsiveLayout.SetFont(stamp, ResponsiveLayout.TextRole.Label);
		var band = new StyleBoxFlat { BgColor = CardFamilyArt.LockBand };
		band.SetCornerRadiusAll(3);
		band.SetContentMarginAll(4);
		stamp.AddThemeStyleboxOverride("normal", band);
		stamp.AddThemeColorOverride("font_color", CardFamilyArt.LockInk);

		var outer = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		outer.AddThemeConstantOverride("separation", 6);
		outer.AddChild(plate);
		outer.AddChild(stamp);
		return (outer, plate, card);
	}

	// ---- the checklist lines (shared by the node and a path's own gates) -------------------------

	// One gate. Met: a tick and the numbers, quiet. Not met: the empty socket, the shortfall in words,
	// in the rubric on its own band - the one thing on a greyed row that is not grey.
	// wrap: a phone, where the line is the row's width and its words wrap; on a PC each gate is one
	// unwrapped item in a flow across the row.
	private Control BuildRequirementLine(AscensionRequirement requirement, bool wrap)
	{
		CodexRows.Palette ink = CodexRows.Vellum;
		bool met = requirement.Met;
		int missing = requirement.Missing;

		string text = requirement.Kind switch
		{
			AscensionRequirementKind.SpellLevel => met
				? $"Spell level {requirement.Need}"
				: $"Spell level {requirement.Have}/{requirement.Need}  ·  {Plural(missing, "more level", "more levels")}",
			AscensionRequirementKind.PlayerLevel => met
				? $"Your level {requirement.Need}"
				: $"Your level {requirement.Have}/{requirement.Need}  ·  {Plural(missing, "more level", "more levels")}",
			// Met, the chip says it all ("Lightning 4/4"); a sentence beside it would only repeat it.
			AscensionRequirementKind.Element => met
				? string.Empty
				: $"Needs {missing} more {requirement.Element} {(missing == 1 ? "tag" : "tags")}",
			_ => string.Empty,
		};

		var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 8);
		if (wrap)
			line.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		line.AddChild(RequirementGlyph(met ? "done" : "todo"));

		// The element gate shows its chips - each tappable, opening what the element does and how many
		// the player holds - so the tag the row is asking for is the same object as on the cards. A
		// branch with no element of its own takes any of its spell's tags, so it shows each of them.
		// Where it wraps the chips get a line of their own above the words: two chips and a sentence
		// do not fit across a phone row, and squeezed into one they set the sentence a word a line.
		Container chipHolder = line;
		Container wordHolder = line;
		if (requirement.Kind == AscensionRequirementKind.Element && requirement.AnyOf.Count > 0)
		{
			if (wrap)
			{
				var stack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
				stack.AddThemeConstantOverride("separation", 2);
				var chips = new HFlowContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
				chips.AddThemeConstantOverride("h_separation", 8);
				chips.AddThemeConstantOverride("v_separation", 4);
				// Centred on the glyph's 32-unit line.
				var chipLine = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, 32) };
				chipLine.AddChild(chips);
				stack.AddChild(chipLine);
				line.AddChild(stack);
				chipHolder = chips;
				wordHolder = stack;
			}
			// Met, only the element that met it: "Arcane 1/4" beside a satisfied gate reads as a
			// shortfall that is not one.
			foreach (Element element in requirement.AnyOf)
			{
				if (met && element != requirement.Element)
					continue;
				int held = currentBaselineElementCounts != null
					&& currentBaselineElementCounts.TryGetValue(element.ToString(), out int n) ? n : 0;
				if (element == requirement.Element)
					held = requirement.Have;
				chipHolder.AddChild(CodexRows.BuildChip(element,
					$"{element} {Mathf.Min(held, requirement.Need)}/{requirement.Need}", focusable: true));
			}
		}

		if (!string.IsNullOrEmpty(text))
		{
			Label words = CodexRows.MakeText(text, ResponsiveLayout.TextRole.Label, met ? ink.InkMeta : ink.Flag);
			if (wordHolder == line)
				words.CustomMinimumSize = new Vector2(0, 32);
			if (wrap)
				words.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			else
				words.AutowrapMode = TextServer.AutowrapMode.Off;
			wordHolder.AddChild(words);
		}
		return met ? line : UnmetBand(line, wrap);
	}

	private static string Plural(int n, string one, string many) => n == 1 ? $"1 {one}" : $"{n} {many}";

	private static Control UnmetBand(Control content, bool fill)
	{
		var band = new PanelContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = fill ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkBegin,
		};
		var style = new StyleBoxFlat { BgColor = AscensionUnmetBand };
		style.SetCornerRadiusAll(4);
		style.ContentMarginLeft = 6;
		style.ContentMarginRight = 8;
		style.ContentMarginTop = 2;
		style.ContentMarginBottom = 2;
		band.AddThemeStyleboxOverride("panel", style);
		band.AddChild(content);
		return band;
	}

	private static Control RequirementGlyph(string key)
	{
		// The 24-pixel stat glyphs, unscaled in a 32 box, exactly as CodexRows sets them.
		return new TextureRect
		{
			Texture = StatIcons.Load(key),
			CustomMinimumSize = new Vector2(32, 32),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
	}

	private static Control BuildRequirementGlyphText(string text, string glyph, Color colour, bool expand)
	{
		var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 8);
		if (expand)
			line.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		line.AddChild(RequirementGlyph(glyph));
		Label label = CodexRows.MakeText(text, ResponsiveLayout.TextRole.Label, colour);
		label.CustomMinimumSize = new Vector2(0, 32);
		if (expand)
			label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		else
			label.AutowrapMode = TextServer.AutowrapMode.Off;
		line.AddChild(label);
		return line;
	}
}
