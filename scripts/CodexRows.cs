using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

/// <summary>
/// The rows the Spellbook and the Deeds lists are made of, on the main menu and on the pause
/// screen's Spells and Deeds tabs alike. See .ai/level-up-screen.md, section 4b.
/// </summary>
/// <remarks>
/// Both screens came back from the phone as "overwhelming and tight": the Spellbook was a wall of
/// tall narrow parchment cards with every line centred and a heavy stone "Set aside" bar on each,
/// and Deeds was a four-column grid of small cards packing name, status, requirement and reward
/// into the same weight of text. The pause tabs said the same things a third way, in developer
/// vocabulary ("Classification: Targeting NearestEnemy | Shape ProjectileHit").
///
/// One builder for all four, so a rule about whitespace or hierarchy is made once:
/// <list type="bullet">
/// <item>A row reads top to bottom as name (Body) - one line of what it does (Label, softer ink) -
/// then quiet meta: element chips, a progress bar with its numbers, a reward led by a glyph.</item>
/// <item>Everything is left-aligned. Centred prose in a narrow card was most of the old density:
/// every line had a different left edge, so the eye had to find each one again.</item>
/// <item>One column on a phone, two on a PC (<see cref="ColumnsFor"/>), under a section heading.</item>
/// <item>A finished or not-yet-found row is quiet - no face, a fainter edge - so the rows the player
/// can act on are the ones with weight.</item>
/// </list>
///
/// Every container here ignores the mouse. A row is not a button to a pointer, so a finger drag
/// that starts on one has to reach the ScrollContainer behind it; only a real control (the
/// Spellbook's Set aside) stops input. A row that has no control of its own still gets a
/// focus-only overlay (<see cref="Entry.FocusStop"/>), because a controller can only scroll a list
/// by moving a selection through it.
/// </remarks>
public static class CodexRows
{
	/// <summary>The inks and faces of one register. Mirrored in tools/art/ui_contrast.py.</summary>
	public sealed class Palette
	{
		public Color Face;
		public Color Edge;
		public Color QuietFace;
		public Color QuietEdge;
		public Color Heading;
		public Color Ink;
		public Color InkSoft;
		public Color InkMeta;
		public Color Reward;
		public Color Done;
		public Color Flag;
		public Color Rule;
		public Color BarTrack;
		public Color BarFill;
	}

	// The parchment register, for the Spellbook page. The row face IS the page (flesh.hi), so a row
	// is carried by its edge and the whitespace around it rather than by a second, darker card - the
	// darker card (flesh.lit) is what put the faint ink at 3.5:1 before.
	// COPIED into tools/art/ui_contrast.py (VELLUM_*): change one, change both.
	public static readonly Palette Vellum = new()
	{
		Face = new Color(0.949f, 0.863f, 0.769f),       // flesh.hi, the page itself
		Edge = new Color(0.659f, 0.439f, 0.345f),       // flesh.base
		QuietFace = new Color(0.949f, 0.863f, 0.769f, 0f),
		QuietEdge = new Color(0.659f, 0.439f, 0.345f, 0.45f),
		Heading = new Color(0.235f, 0.141f, 0.212f),    // flesh.deep
		Ink = new Color(0.235f, 0.141f, 0.212f),        // flesh.deep
		InkSoft = new Color(0.204f, 0.173f, 0.298f),    // violet.base
		InkMeta = new Color(0.431f, 0.267f, 0.314f),    // flesh.shade
		Reward = new Color(0.329f, 0.247f, 0.063f),     // gold.shade
		Done = new Color(0.329f, 0.247f, 0.063f),
		Flag = new Color(0.416f, 0.125f, 0.157f),       // Rubric
		Rule = new Color(0.847f, 0.643f, 0.518f),       // flesh.lit
		BarTrack = new Color(0.847f, 0.643f, 0.518f),
		BarFill = new Color(0.306f, 0.267f, 0.439f),    // violet.lit
	};

	// The stone register, for Deeds on the menu and both tabs of the pause screen.
	// COPIED into tools/art/ui_contrast.py (STONE_*): change one, change both.
	public static readonly Palette Stone = new()
	{
		Face = new Color(0.118f, 0.149f, 0.204f),       // stone.base
		Edge = new Color(0.227f, 0.275f, 0.345f),       // stone.hi
		QuietFace = new Color(0.118f, 0.149f, 0.204f, 0f),
		QuietEdge = new Color(0.227f, 0.275f, 0.345f, 0.55f),
		Heading = new Color(1.0f, 0.941f, 0.753f),      // gold.hi
		Ink = new Color(1.0f, 0.95f, 0.86f),            // HeadingInk, warm bone
		InkSoft = new Color(0.831f, 0.863f, 0.918f),    // skin.lit
		InkMeta = new Color(0.604f, 0.643f, 0.722f),    // skin.base
		Reward = new Color(0.847f, 0.690f, 0.290f),     // gold.lit
		Done = new Color(0.608f, 0.831f, 0.333f),       // grass mid
		Flag = new Color(0.95f, 0.72f, 0.45f),
		Rule = new Color(0.227f, 0.275f, 0.345f),       // stone.hi
		BarTrack = new Color(0.227f, 0.275f, 0.345f),
		BarFill = new Color(0.847f, 0.690f, 0.290f),    // gold.lit
	};

	/// <summary>Everything one row can say. Leave a field empty and its line is not built.</summary>
	public sealed class Entry
	{
		public string Title = string.Empty;
		/// <summary>One line of what it is or does.</summary>
		public string Body = string.Empty;
		public Texture2D Icon;
		/// <summary>Draws the icon as a dark shape - a page not yet found.</summary>
		public bool IconSilhouette;
		public IReadOnlyList<(Element Element, int Weight)> Elements;
		/// <summary>Said after the chips, in the meta ink: "Attuned".</summary>
		public string ChipNote = string.Empty;
		/// <summary>0..1 draws a bar, with <see cref="ProgressText"/> beside it.</summary>
		public float? Progress;
		public string ProgressText = string.Empty;
		/// <summary>A short word right of the title: "Done", "Set aside".</summary>
		public string Tag = string.Empty;
		public Texture2D TagGlyph;
		public bool TagIsDone;
		/// <summary>The reward or the hint, led by <see cref="MetaGlyph"/>.</summary>
		public string Meta = string.Empty;
		public Texture2D MetaGlyph;
		public bool MetaIsReward;
		/// <summary>No face and a faint edge: finished, set aside, or not found yet.</summary>
		public bool Quiet;
		/// <summary>A real control placed right of the title - the Spellbook's Set aside.</summary>
		public Control Trailing;
		/// <summary>
		/// Gives the row a focus-only overlay, so a controller has somewhere to stop on it. Leave it
		/// off when <see cref="Trailing"/> is an enabled button: one stop per row, or the stick has
		/// to be pushed twice to leave it.
		/// </summary>
		public bool FocusStop;
	}

	/// <summary>One column on a phone, two on a PC.</summary>
	/// <remarks>
	/// Two is the most: a third column at 1280 wide is a 360-unit card, which is where the old
	/// four-across Deeds grid came from. Measured against the canvas rather than the panel because
	/// every panel these sit in is the canvas width less a fixed margin.
	/// </remarks>
	public static int ColumnsFor(Node node) => ResponsiveLayout.IsNarrow(node) ? 1 : 2;

	// Space is the hierarchy here, so it is set in one place.
	public const int SectionSeparation = 32;
	public const int RowSeparation = 14;
	public const int ColumnSeparation = 18;
	private const int RowPaddingX = 18;
	private const int RowPaddingY = 14;
	private const int LineGap = 6;
	private const int ExtraLineSpacing = 3;
	private const float IconSize = 64f;
	private const float QuietIconSize = 32f;
	private const float GlyphBox = 32f;

	/// <summary>The list every section is added to: a column with generous air between sections.</summary>
	public static VBoxContainer BuildList(string name = null)
	{
		var list = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		if (!string.IsNullOrEmpty(name))
			list.Name = name;
		list.AddThemeConstantOverride("separation", SectionSeparation);
		return list;
	}

	/// <summary>
	/// A ScrollContainer holding <paramref name="list"/>, with a gutter on the right so the scroll
	/// bar never sits on a row. A standard ScrollContainer on purpose: whatever makes finger-drag
	/// scrolling work applies to it like any other.
	/// </summary>
	public static ScrollContainer BuildScroll(string name, VBoxContainer list)
	{
		var scroll = new ScrollContainer
		{
			Name = name,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			FollowFocus = true,
		};
		scroll.AddChild(WithGutter(list));
		return scroll;
	}

	/// <summary>
	/// <paramref name="content"/> with room on the right for a scroll bar, for a list that goes into
	/// a ScrollContainer someone else built (the pause screen's).
	/// </summary>
	public static MarginContainer WithGutter(Control content)
	{
		var gutter = new MarginContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		gutter.AddThemeConstantOverride("margin_right", 18);
		gutter.AddThemeConstantOverride("margin_left", 2);
		gutter.AddThemeConstantOverride("margin_top", 2);
		gutter.AddThemeConstantOverride("margin_bottom", 12);
		gutter.AddChild(content);
		return gutter;
	}

	/// <summary>
	/// A section: a Cinzel heading with its count on the right, a rule, then the rows in
	/// <paramref name="columns"/> columns.
	/// </summary>
	public static VBoxContainer AddSection(VBoxContainer list, string title, string count, Palette palette,
		int columns, IEnumerable<Control> rows)
	{
		var section = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		section.AddThemeConstantOverride("separation", 12);

		var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		head.AddThemeConstantOverride("separation", 12);
		var heading = new Label
		{
			Text = title,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Bottom,
		};
		ResponsiveLayout.SetFont(heading, ResponsiveLayout.TextRole.Title);
		heading.AddThemeColorOverride("font_color", palette.Heading);
		head.AddChild(heading);
		if (!string.IsNullOrEmpty(count))
		{
			var tally = new Label
			{
				Text = count,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Bottom,
			};
			ResponsiveLayout.SetFont(tally, ResponsiveLayout.TextRole.Label);
			tally.AddThemeColorOverride("font_color", palette.InkMeta);
			head.AddChild(tally);
		}
		section.AddChild(head);
		section.AddChild(new ColorRect
		{
			Color = palette.Rule,
			CustomMinimumSize = new Vector2(0, 2),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});

		var grid = new GridContainer
		{
			Columns = Math.Max(1, columns),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		grid.AddThemeConstantOverride("h_separation", ColumnSeparation);
		grid.AddThemeConstantOverride("v_separation", RowSeparation);
		foreach (Control row in rows)
			grid.AddChild(row);
		section.AddChild(grid);

		list.AddChild(section);
		return section;
	}

	/// <summary>
	/// The one-line summary under a screen's heading, and optionally a bar for the whole set.
	/// </summary>
	public static Control BuildSummary(string text, Palette palette, float? fraction = null)
	{
		var box = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		box.AddThemeConstantOverride("separation", 8);
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Label);
		label.AddThemeColorOverride("font_color", palette.InkMeta);
		label.AddThemeConstantOverride("line_spacing", ExtraLineSpacing);
		box.AddChild(label);
		if (fraction is float f)
		{
			Control bar = BuildBar(f, palette);
			bar.CustomMinimumSize = new Vector2(320, 8);
			bar.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			box.AddChild(bar);
		}
		return box;
	}

	public static PanelContainer BuildRow(Entry entry, Palette palette)
	{
		var row = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		var style = new StyleBoxFlat
		{
			BgColor = entry.Quiet ? palette.QuietFace : palette.Face,
			BorderColor = entry.Quiet ? palette.QuietEdge : palette.Edge,
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		// No content margin on the card itself: the padding is an inner MarginContainer, so the
		// focus overlay - a sibling of it - covers the whole card and its ring sits on the edge
		// rather than 18 units inside it.
		style.SetContentMarginAll(0);
		row.AddThemeStyleboxOverride("panel", style);

		if (entry.FocusStop)
		{
			// First child, so the content draws over its wash. It ignores the mouse: to a pointer
			// the row is text, and a press that started here must be free to become a drag.
			Button overlay = BonelightSkin.MakeCardClickOverlay(true);
			overlay.Name = "RowFocus";
			overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
			row.AddChild(overlay);
		}

		var padding = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		padding.AddThemeConstantOverride("margin_left", RowPaddingX);
		padding.AddThemeConstantOverride("margin_right", RowPaddingX);
		padding.AddThemeConstantOverride("margin_top", RowPaddingY);
		padding.AddThemeConstantOverride("margin_bottom", RowPaddingY);
		row.AddChild(padding);

		var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 16);
		padding.AddChild(line);

		if (entry.Icon != null)
			line.AddChild(BuildIcon(entry.Icon, entry.Quiet && entry.IconSilhouette ? QuietIconSize : IconSize,
				entry.IconSilhouette));

		var column = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		column.AddThemeConstantOverride("separation", LineGap);
		line.AddChild(column);

		var titleRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		titleRow.AddThemeConstantOverride("separation", 10);
		var title = MakeText(entry.Title, ResponsiveLayout.TextRole.Body,
			entry.Quiet && entry.IconSilhouette ? palette.InkSoft : palette.Ink);
		title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleRow.AddChild(title);
		if (!string.IsNullOrEmpty(entry.Tag))
			titleRow.AddChild(BuildGlyphText(entry.Tag, entry.TagGlyph,
				entry.TagIsDone ? palette.Done : palette.Flag, expand: false));
		if (entry.Trailing != null)
		{
			entry.Trailing.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
			titleRow.AddChild(entry.Trailing);
		}
		column.AddChild(titleRow);

		if (!string.IsNullOrEmpty(entry.Body))
			column.AddChild(MakeText(entry.Body, ResponsiveLayout.TextRole.Label, palette.InkSoft));

		if (entry.Elements != null && entry.Elements.Count > 0)
			column.AddChild(BuildChips(entry.Elements, entry.ChipNote, palette));

		if (entry.Progress is float progress)
		{
			var bar = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			bar.AddThemeConstantOverride("separation", 12);
			Control track = BuildBar(progress, palette);
			track.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			track.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			bar.AddChild(track);
			if (!string.IsNullOrEmpty(entry.ProgressText))
			{
				Label numbers = MakeText(entry.ProgressText, ResponsiveLayout.TextRole.Label, palette.InkMeta);
				numbers.AutowrapMode = TextServer.AutowrapMode.Off;
				bar.AddChild(numbers);
			}
			column.AddChild(bar);
		}

		if (!string.IsNullOrEmpty(entry.Meta))
			column.AddChild(BuildGlyphText(entry.Meta, entry.MetaGlyph,
				entry.MetaIsReward && !entry.Quiet ? palette.Reward : palette.InkMeta, expand: true));

		return row;
	}

	/// <summary>Parses the hand-written element strings ("Earth, Grass", "Ice x2").</summary>
	public static List<(Element Element, int Weight)> ParseElements(string text)
	{
		var result = new List<(Element, int)>();
		if (string.IsNullOrWhiteSpace(text))
			return result;
		foreach (string raw in text.Split(','))
		{
			string part = raw.Trim();
			int weight = 1;
			int x = part.IndexOf(" x", StringComparison.Ordinal);
			if (x > 0 && int.TryParse(part.Substring(x + 2), out int parsed))
			{
				weight = parsed;
				part = part.Substring(0, x);
			}
			if (Enum.TryParse(part, true, out Element element))
				result.Add((element, weight));
		}
		return result;
	}

	public static List<(Element Element, int Weight)> FromWeights(Dictionary<Element, int> weights)
	{
		return weights == null
			? new List<(Element, int)>()
			: weights.OrderBy(p => p.Key.ToString()).Select(p => (p.Key, p.Value)).ToList();
	}

	/// <summary>
	/// Pages not yet found, folded together where they share a kind and a hint, in first-seen order.
	/// </summary>
	/// <remarks>
	/// Ten passives answer "Not yet obtainable." on today's roster, and ten identical rows of it was
	/// the longest stretch of either list - a screenful that says one thing. Folded, it says it once:
	/// "10 unknown passives".
	/// </remarks>
	public static List<(T First, int Count)> GroupMissing<T>(IEnumerable<T> missing, Func<T, bool> passive,
		Func<T, string> hint)
	{
		var groups = new List<(T First, int Count, bool Passive, string Hint)>();
		foreach (T item in missing)
		{
			bool p = passive(item);
			string h = hint(item);
			int at = groups.FindIndex(g => g.Passive == p && g.Hint == h);
			if (at >= 0)
				groups[at] = (groups[at].First, groups[at].Count + 1, p, h);
			else
				groups.Add((item, 1, p, h));
		}
		return groups.Select(g => (g.First, g.Count)).ToList();
	}

	public static string UnknownTitle(bool passive, int count)
	{
		string kind = passive ? "passive" : "attack";
		return count > 1 ? $"{count} unknown {kind}s" : $"Unknown {kind}";
	}

	/// <summary>The glyph in front of a deed's reward: the spell, the boon, or the purse.</summary>
	public static Texture2D RewardGlyph(AchievementDefinition deed, Func<string, Texture2D> spellIcon)
	{
		if (deed == null)
			return null;
		if (!string.IsNullOrEmpty(deed.SpellUnlockId) && spellIcon?.Invoke(deed.SpellUnlockId) is Texture2D spell)
			return spell;
		if (!string.IsNullOrEmpty(deed.BoonUnlockId)
			&& BonelightSkin.LoadTextureSafe($"res://assets/bonelight/ui/boons/{deed.BoonUnlockId}.png") is Texture2D boon)
			return boon;
		if (deed.CurrencyReward > 0)
			return StatIcons.Load("energy");
		return StatIcons.Load("generic");
	}

	/// <summary>
	/// A deed as a row. Shared by the main menu and the pause tab so the two cannot word it
	/// differently, which they did.
	/// </summary>
	public static PanelContainer BuildDeedRow(AchievementDefinition deed, bool done, AchievementProgress progress,
		Func<string, Texture2D> spellIcon, Palette palette, bool focusStop = true)
	{
		var entry = new Entry
		{
			Title = deed.DisplayName,
			Body = deed.Description,
			Meta = deed.RewardText,
			MetaGlyph = RewardGlyph(deed, spellIcon),
			MetaIsReward = true,
			Quiet = done,
			FocusStop = focusStop,
		};
		if (done)
		{
			entry.Tag = "Done";
			entry.TagGlyph = StatIcons.Load("done");
			entry.TagIsDone = true;
		}
		else if (!string.IsNullOrEmpty(progress?.Display))
		{
			// A countable deed gets a bar and its numbers; a yes/no one gets neither, because
			// "0 / 1" reads as a broken counter (AchievementDefinition.GetProgress).
			entry.Progress = progress.Fraction;
			entry.ProgressText = progress.Display;
		}
		return BuildRow(entry, palette);
	}

	/// <summary>
	/// Unfinished deeds, closest to done first, then by the order they were written in.
	/// </summary>
	public static List<(AchievementDefinition Deed, AchievementProgress Progress)> OrderInProgress(
		IEnumerable<(AchievementDefinition Deed, AchievementProgress Progress)> deeds)
	{
		return deeds.Select((d, i) => (d, i))
			.OrderByDescending(p => string.IsNullOrEmpty(p.d.Progress?.Display) ? -1f : p.d.Progress.Fraction)
			.ThenBy(p => p.i)
			.Select(p => p.d)
			.ToList();
	}

	public static Label MakeText(string text, ResponsiveLayout.TextRole role, Color ink)
	{
		var label = new Label
		{
			Text = text,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		ResponsiveLayout.SetFont(label, role);
		label.AddThemeColorOverride("font_color", ink);
		label.AddThemeConstantOverride("line_spacing", ExtraLineSpacing);
		return label;
	}

	private static Control BuildIcon(Texture2D texture, float size, bool silhouette)
	{
		return new TextureRect
		{
			Texture = texture,
			CustomMinimumSize = new Vector2(size, size),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			// A shape, not a picture: enough to say a page is there, not enough to give it away.
			Modulate = silhouette ? new Color(0.10f, 0.08f, 0.10f, 0.45f) : Colors.White,
		};
	}

	/// <summary>A glyph and its words, the glyph at the top of the first line.</summary>
	private static Control BuildGlyphText(string text, Texture2D glyph, Color ink, bool expand)
	{
		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 8);
		if (expand)
			row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		if (glyph != null)
		{
			// The stat glyphs are drawn at 24 and the spell and boon icons at 64. Shown in the same
			// 32 box, one is centred unscaled and the other halved - both whole multiples of their
			// pixels, so neither smears.
			bool native = glyph.GetWidth() <= 24;
			row.AddChild(new TextureRect
			{
				Texture = glyph,
				CustomMinimumSize = new Vector2(GlyphBox, GlyphBox),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = native ? TextureRect.StretchModeEnum.KeepCentered : TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			});
		}
		Label label = MakeText(text, ResponsiveLayout.TextRole.Label, ink);
		label.CustomMinimumSize = new Vector2(0, GlyphBox);
		if (expand)
			label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		else
			label.AutowrapMode = TextServer.AutowrapMode.Off;
		row.AddChild(label);
		return row;
	}

	private static Control BuildChips(IReadOnlyList<(Element Element, int Weight)> elements, string note, Palette palette)
	{
		var flow = new HFlowContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		flow.AddThemeConstantOverride("h_separation", 8);
		flow.AddThemeConstantOverride("v_separation", 6);
		foreach (var (element, weight) in elements)
		{
			Color colour = ChipFill(ElementColors.GetColor(element));
			var chip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			var style = new StyleBoxFlat
			{
				BgColor = colour,
				// A darker rim of its own colour, so the pale elements (Light, Wind) still have an edge
				// on the parchment.
				BorderColor = colour.Darkened(0.35f),
			};
			style.SetBorderWidthAll(1);
			style.SetCornerRadiusAll(4);
			style.ContentMarginLeft = 10;
			style.ContentMarginRight = 10;
			style.ContentMarginTop = 2;
			style.ContentMarginBottom = 2;
			chip.AddThemeStyleboxOverride("panel", style);
			Label text = MakeText(weight > 1 ? $"{element} x{weight}" : element.ToString(),
				ResponsiveLayout.TextRole.Label, StatIcons.ReadableOn(colour));
			text.AutowrapMode = TextServer.AutowrapMode.Off;
			chip.AddChild(text);
			flow.AddChild(chip);
		}
		if (!string.IsNullOrEmpty(note))
		{
			Label tail = MakeText(note, ResponsiveLayout.TextRole.Label, palette.InkMeta);
			tail.AutowrapMode = TextServer.AutowrapMode.Off;
			flow.AddChild(tail);
		}
		return flow;
	}

	/// <summary>
	/// The element's colour, nudged until its chip text clears 4.5:1. A chip here is the solid
	/// element colour, and Earth's mid-brown measured 4.48:1 against either ink - the one hue no
	/// choice of ink rescues. Lifted or sunk a few percent toward the side its ink is not on, it
	/// stays the same hue and passes. Mirrored in tools/art/ui_contrast.py (chip_fill).
	/// </summary>
	public static Color ChipFill(Color colour)
	{
		for (int i = 0; i < 10; i++)
		{
			Color ink = StatIcons.ReadableOn(colour);
			if (StatIcons.ContrastRatio(ink, colour) >= 4.6f)
				break;
			colour = ink == Colors.White ? colour.Darkened(0.05f) : colour.Lightened(0.05f);
		}
		return colour;
	}

	// A filled bar. Not a ProgressBar node: the theme styles that like the health bar, and this is a
	// reading aid, not a gauge. Ratios rather than pixels, so it is right at any column width.
	private static Control BuildBar(float fraction, Palette palette)
	{
		fraction = Mathf.Clamp(fraction, 0f, 1f);
		var track = new PanelContainer
		{
			CustomMinimumSize = new Vector2(0, 8),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		var trackStyle = new StyleBoxFlat { BgColor = palette.BarTrack };
		trackStyle.SetCornerRadiusAll(4);
		track.AddThemeStyleboxOverride("panel", trackStyle);

		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 0);
		var fill = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = Math.Max(0.001f, fraction),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		var fillStyle = new StyleBoxFlat { BgColor = fraction > 0f ? palette.BarFill : new Color(0, 0, 0, 0) };
		fillStyle.SetCornerRadiusAll(4);
		fill.AddThemeStyleboxOverride("panel", fillStyle);
		row.AddChild(fill);
		row.AddChild(new Control
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = Math.Max(0.001f, 1f - fraction),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
		track.AddChild(row);
		return track;
	}
}
