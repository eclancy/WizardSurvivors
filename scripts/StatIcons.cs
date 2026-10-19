using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

/// <summary>
/// The glyph in front of a stat line: a sword for damage, an hourglass for cooldown, and so on.
/// The art is tools/art/stat_icons.py; this decides which glyph a line of text gets and lays the
/// lines out.
/// </summary>
/// <remarks>
/// WHY GLYPHS. A card's stats used to be one row of words in one ink - "DMG 20   CD 5.5s   RNG
/// 480" - so telling damage from cooldown meant reading the abbreviation, on a paused screen, in
/// the middle of a fight. A glyph is recognised rather than read. Colour stays a second cue (an
/// upgrade's lines are rubric, a boon's are bone) but meaning no longer depends on it, which is
/// also what makes the cards usable by a player who cannot tell the element hues apart.
///
/// WHY CLASSIFY TEXT rather than carry a stat id. Every summary is already built as text by
/// Player (BuildSpellStatSummary, BuildSpellUpgradeSummary), BoonCatalog.DescribeEffect and
/// ChestItemCatalog.GetDescription, and the balance harness and its reports read those strings.
/// Matching a keyword at the point of display costs one lookup per line and leaves every producer
/// alone. A line nothing matches gets the gold lozenge (<c>generic</c>) - a mark, never a gap - and
/// a line that opens with one of the milestone stars (✦ ★ ✶) gets nothing, because the star IS
/// its glyph.
///
/// ORDER MATTERS in <see cref="KeyFor"/>: "Slow duration" is slow, not duration; "Projectile
/// speed" is speed, not projectiles; "10 damage to nearby enemies when struck" is thorns, not
/// damage; "-1 damage taken" is the shield. The specific test always comes first.
/// </remarks>
public static class StatIcons
{
	private const string IconRoot = "res://assets/bonelight/ui/stats/stat-";

	/// <summary>The separator Player puts between the parts of a summary.</summary>
	public const string Separator = "   ";

	/// <summary>Drawn size: the 12px glyph at x2, which is a capital letter at the Body role.</summary>
	public const float IconSize = 24f;

	/// <summary>Which glyph a stat line takes, or null for a line that should have none.</summary>
	public static string KeyFor(string line)
	{
		if (string.IsNullOrWhiteSpace(line))
			return null;

		string t = line.Trim();
		// The milestone lines carry their own star.
		if (t.StartsWith("✦") || t.StartsWith("★") || t.StartsWith("✶") || t.StartsWith("★"))
			return null;

		t = t.ToLowerInvariant();

		if (t.Contains("crit"))
			return "crit";
		if (t.Contains("cooldown") || t.StartsWith("cd ") || t.Contains("attack speed") || t.Contains("cast speed"))
			return "cooldown";
		if (t.Contains("poison") || t.Contains("dot ") || t.Contains("venom"))
			return "poison";
		if (t.Contains("burn") || t.Contains("ignite"))
			return "burn";
		if (t.Contains("freeze") || t.Contains("frozen") || t.Contains("chill"))
			return "freeze";
		if (t.Contains("root"))
			return "root";
		if (t.Contains("slow"))
			return "slow";
		if (t.Contains("knockback") || t.Contains("push"))
			return "knockback";
		if (t.Contains("pierce"))
			return "pierce";
		if (t.Contains("chain") || t.Contains("branch arc") || t.Contains("jump"))
			return "chain";
		if (t.Contains("pickup") || t.Contains("magnet"))
			return "magnet";
		if (t.Contains("projectile speed"))
			return "speed";
		if (t.Contains("projectile") || (t.Length > 1 && t[0] == 'x' && char.IsDigit(t[1])))
			return "projectiles";
		if (t.Contains("duration") || t.Contains("zone"))
			return "duration";
		if (t.Contains("area") || t.Contains("radius") || t.Contains("splash"))
			return "area";
		if (t.StartsWith("rng ") || t.Contains("range"))
			return "range";
		if (t.Contains("speed"))
			return "speed";
		if (t.Contains("hp per") || t.Contains("regen") || t.Contains("heal") || t.Contains("mend"))
			return "regen";
		if (t.Contains("max hp") || t.Contains("max health") || t.EndsWith(" hp"))
			return "heart";
		if (t.Contains("evasion") || t.Contains("dodge") || t.Contains("ignores one hit"))
			return "evasion";
		if (t.Contains("luck") || t.Contains("chest") || t.Contains("relic") || t.Contains("reward"))
			return "luck";
		if (t.Contains("xp") || t.Contains("experience"))
			return "xp";
		if (t.Contains("when struck") || t.Contains("touching") || t.Contains("thorn") || t.Contains("retaliat"))
			return "thorns";
		if (t.Contains("shield") || t.Contains("armor") || t.Contains("armour") || t.Contains("defense")
			|| t.Contains("damage taken"))
			return "shield";
		if (t.Contains("damage") || t.StartsWith("dmg "))
			return "damage";
		return "generic";
	}

	/// <summary>The glyph texture for a key, or null if the art is missing (never an error).</summary>
	public static Texture2D Load(string key)
	{
		return key == null ? null : BonelightSkin.LoadTextureSafe(IconRoot + key + ".png");
	}

	/// <summary>
	/// Spells the card abbreviations out. With a glyph in front, "DMG" was the one part of the line
	/// still asking to be decoded; the word costs a few characters and the cards have the room.
	/// </summary>
	public static string Expand(string part)
	{
		string t = part.Trim();
		if (t.StartsWith("DMG "))
			return "Damage " + t.Substring(4);
		if (t.StartsWith("CD "))
			return "Cooldown " + t.Substring(3);
		if (t.StartsWith("RNG "))
			return "Range " + t.Substring(4);
		if (t.Length > 1 && t[0] == 'x' && char.IsDigit(t[1]))
			return "Projectiles " + t.Substring(1);
		return t;
	}

	/// <summary>Splits a summary into its parts, in order, without empties.</summary>
	public static List<string> Split(string summary)
	{
		var parts = new List<string>();
		if (string.IsNullOrWhiteSpace(summary))
			return parts;
		foreach (string raw in summary.Split(Separator, StringSplitOptions.RemoveEmptyEntries))
		{
			string p = raw.Trim();
			if (p.Length > 0)
				parts.Add(p);
		}
		return parts;
	}

	/// <summary>
	/// One line: glyph, then text. The text wraps; the glyph stays at the top of its line.
	/// </summary>
	public static Control BuildLine(string text, Color ink, ResponsiveLayout.TextRole role, bool expand = true,
		bool wrap = true)
	{
		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 8);

		string key = KeyFor(text);
		Texture2D icon = Load(key);
		if (icon != null)
		{
			row.AddChild(new TextureRect
			{
				Name = "StatIcon",
				Texture = icon,
				CustomMinimumSize = new Vector2(IconSize, IconSize),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				TooltipText = key,
			});
		}

		var label = new Label
		{
			Text = expand ? Expand(text) : text,
			AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		ResponsiveLayout.SetFont(label, role);
		label.AddThemeColorOverride("font_color", ink);
		row.AddChild(label);
		return row;
	}

	/// <summary>
	/// A whole summary as a list, one glyph-led line per part. <paramref name="centred"/> centres
	/// the BLOCK and keeps the lines left-aligned inside it, so the glyphs form a column - a centred
	/// list with ragged glyphs reads as decoration rather than as a list.
	/// </summary>
	/// <remarks>
	/// A shrink-centred block cannot hold a wrapping label - the container asks the label for its
	/// minimum width, a wrapping label's minimum is one letter, and the result was "Damage" set one
	/// letter per line down the card. So a block is only centred when every line is short enough to
	/// stand unwrapped (<see cref="CentredLineLimit"/>); a long line (a boon's sentence) makes the
	/// list full-width, left-aligned and wrapping instead.
	/// </remarks>
	public static Control BuildList(string summary, Color ink, ResponsiveLayout.TextRole role, bool centred)
	{
		List<string> parts = Split(summary);
		bool shrink = centred && parts.TrueForAll(p => Expand(p).Length <= CentredLineLimit);
		var list = new VBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = shrink ? Control.SizeFlags.ShrinkCenter : Control.SizeFlags.ExpandFill,
		};
		list.AddThemeConstantOverride("separation", 4);
		foreach (string part in parts)
			list.AddChild(BuildLine(part, ink, role, wrap: !shrink));
		return list;
	}

	/// <summary>The longest line a centred list will hold unwrapped: about 220px at the Body role.</summary>
	private const int CentredLineLimit = 22;

	/// <summary>WCAG 2 relative luminance of a colour.</summary>
	public static float RelativeLuminance(Color c)
	{
		static float Channel(float v) => v <= 0.03928f ? v / 12.92f : MathF.Pow((v + 0.055f) / 1.055f, 2.4f);
		return 0.2126f * Channel(c.R) + 0.7152f * Channel(c.G) + 0.0722f * Channel(c.B);
	}

	/// <summary>WCAG 2 contrast ratio between two opaque colours, 1 to 21.</summary>
	public static float ContrastRatio(Color a, Color b)
	{
		float la = RelativeLuminance(a);
		float lb = RelativeLuminance(b);
		return (MathF.Max(la, lb) + 0.05f) / (MathF.Min(la, lb) + 0.05f);
	}

	/// <summary>
	/// Whichever of near-black and white reads better on <paramref name="background"/>, by WCAG
	/// contrast. The old rule thresholded perceived brightness at 0.62 and put white on the poison
	/// and grass chips at about 2.3:1.
	/// </summary>
	public static Color ReadableOn(Color background)
	{
		var dark = new Color(0.06f, 0.06f, 0.07f);
		return ContrastRatio(dark, background) >= ContrastRatio(Colors.White, background) ? dark : Colors.White;
	}
}
