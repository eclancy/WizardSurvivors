using Godot;

/// <summary>
/// A spell card's level pips: one per level, the levels held filled, and the level this pick makes
/// it ALIVE - filling and glinting on a calm one-second loop - so the card says which pip it buys.
/// </summary>
/// <remarks>
/// From the Pixel 9: "on leveling up spells, animate the pip for the level it's becoming". The row
/// was static StyleBoxFlats, and the pip a pick adds was simply drawn filled, so an upgrade to level
/// 3 looked like a spell already at 3 and the card never said "this one". Three states now:
///   - held: solid, steady;
///   - becoming: a thicker ring, a fill that rises and falls, a halo that breathes with it, and on
///     dark cards a glint at the top of the breath - never still, never a blink;
///   - not yet: an empty socket.
/// ONLY AN UPGRADE CARD HAS A ROW. A new spell's card animated pip 1 for a day, and the player
/// asked for it to go ("when you are offered a spell for the first time, we do not need to see the
/// level up animation pips"): a lone breathing socket at the head of six empty ones says nothing
/// the word under the name does not. The row is the mark of the card that improves something held
/// (LevelUpMenu.BuildLevelPips), beside its red rubric and its "Level up" line.
///
/// ONE PIP PER LEVEL (it was one per level after the first, and an upgrade to 2 showed its first pip
/// already filled). Level 8 is not on a card any more (.ai/level-up-screen.md, 3a), so a card's row
/// is MaxLevel long.
///
/// CALM: a one-second cycle, eased; while the card is hovered, focused or pressed the floor rises
/// and the halo brightens, so the card about to be taken wakes, like BoonAura. The clock is
/// Time.GetTicksMsec, which runs while the level-up has the tree paused, and every row reads the same
/// clock so three cards pulse together rather than in a ripple.
///
/// No allocation per frame: colours are fields, the rects are computed into locals, and a redraw is
/// queued only while visible. ProcessMode Always, or the paused tree would freeze it on one frame.
/// </remarks>
public partial class LevelPips : Control
{
	public const float PipSize = 13f;
	public const float Gap = 3f;
	private const float CycleSeconds = 1.0f;

	/// <summary>How many pips (the card's level cap).</summary>
	public int Count { get; set; }

	/// <summary>Pips already held, filled steady.</summary>
	public int Filled { get; set; }

	/// <summary>The 0-based pip this pick reaches, animated; -1 for none.</summary>
	public int Becoming { get; set; } = -1;

	/// <summary>The ring round every pip, and the fill of a held one.</summary>
	public Color Ring { get; set; } = new Color(0.541f, 0.416f, 0.118f);

	/// <summary>An empty socket's fill.</summary>
	public Color Empty { get; set; } = new Color(0.204f, 0.173f, 0.298f, 0.16f);

	/// <summary>The fill the becoming pip rises to, and its halo.</summary>
	public Color Glow { get; set; } = new Color(0.847f, 0.690f, 0.290f);

	/// <summary>A one-pixel glint at the top of the breath. Only worth drawing on a dark card.</summary>
	public bool Glint { get; set; }

	/// <summary>The card this row belongs to: hovered, focused or pressed, the pip brightens.</summary>
	public BaseButton FocusOwner { get; set; }

	/// <summary>
	/// A second control whose focus or hover also wakes the row. The Arcane Upgrades shop's card has
	/// two stops - its buy plate when it can be bought, the card itself when it cannot - so one owner
	/// is not enough there. Null everywhere else.
	/// </summary>
	public BaseButton AlsoLitBy { get; set; }

	/// <summary>
	/// One pip's edge, and the gap between pips. The level-up cards use the defaults; the shop's
	/// twenty-level rows (Fortune Thread, Keen Focus) draw smaller pips so the row still fits a card.
	/// </summary>
	public float Pip { get; set; } = PipSize;
	public float Spacing { get; set; } = Gap;

	/// <summary>Pips per row; 0 is one row. The shop's twenty-level tracks wrap at ten, so a long
	/// track reads as two tens rather than a line of dots to count.</summary>
	public int PerRow { get; set; }

	private int Columns => PerRow > 0 ? Mathf.Min(PerRow, Count) : Count;
	private int Rows => PerRow > 0 && Count > 0 ? (Count + PerRow - 1) / PerRow : 1;

	private static readonly Color GlintColour = new Color(1f, 1f, 1f, 1f);

	// Celebrate(): the pip a purchase just filled flares - a white flash that settles into the held
	// fill, and a ring of the glow colour that opens outward and fades. Shop only.
	private const float FlareSeconds = 0.6f;
	private int flareIndex = -1;
	private ulong flareStartMs;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		FocusMode = FocusModeEnum.None;
		ProcessMode = ProcessModeEnum.Always;
		Configure();
	}

	/// <summary>
	/// Re-reads Count, Filled and Becoming after a change - the shop updates a row in place when a
	/// level is bought rather than rebuilding it, so the pip that was bought can be seen to fill.
	/// </summary>
	public void Configure()
	{
		float width = Columns * Pip + Mathf.Max(0, Columns - 1) * Spacing;
		float height = Rows * Pip + Mathf.Max(0, Rows - 1) * Spacing;
		CustomMinimumSize = new Vector2(width, height);
		SetProcess(Becoming >= 0 || flareIndex >= 0);
		QueueRedraw();
	}

	/// <summary>Flares pip <paramref name="index"/> (0-based): the level a purchase just bought.</summary>
	public void Celebrate(int index)
	{
		flareIndex = index;
		flareStartMs = Time.GetTicksMsec();
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		if (flareIndex >= 0 && (Time.GetTicksMsec() - flareStartMs) / 1000f > FlareSeconds)
		{
			flareIndex = -1;
			SetProcess(Becoming >= 0);
			QueueRedraw();
		}
		if (IsVisibleInTree())
			QueueRedraw();
	}

	private bool IsLit()
	{
		return IsLitBy(FocusOwner) || IsLitBy(AlsoLitBy);
	}

	private static bool IsLitBy(BaseButton owner)
	{
		if (owner == null || !IsInstanceValid(owner))
			return false;
		return owner.HasFocus() || owner.IsHovered() || owner.ButtonPressed;
	}

	private Rect2 PipRect(int i, float x0, float y0, float size, float gap)
	{
		int column = PerRow > 0 ? i % PerRow : i;
		int row = PerRow > 0 ? i / PerRow : 0;
		return new Rect2(x0 + column * (size + gap), y0 + row * (size + gap), size, size);
	}

	public override void _Draw()
	{
		float size = Pip;
		float gap = Spacing;
		float rowWidth = Columns * size + Mathf.Max(0, Columns - 1) * gap;
		float blockHeight = Rows * size + Mathf.Max(0, Rows - 1) * gap;
		// Centred in whatever width the container gave; the builders ask for the minimum (ShrinkBegin
		// or ShrinkCenter), so this is normally 0 and the container does the aligning.
		float x0 = Mathf.Floor((Size.X - rowWidth) * 0.5f);
		float y0 = Mathf.Floor((Size.Y - blockHeight) * 0.5f);

		// 0..1..0 over a cycle, eased so it lingers full and empty rather than bouncing.
		float phase = (Time.GetTicksMsec() / 1000f) % CycleSeconds / CycleSeconds;
		float wave = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.Tau);
		bool lit = IsLit();
		float floor = lit ? 0.45f : 0.15f;
		float level = Mathf.Lerp(floor, 1f, wave);

		for (int i = 0; i < Count; i++)
		{
			var rect = PipRect(i, x0, y0, size, gap);
			if (i == Becoming)
			{
				// The halo: a ring two pixels out, breathing with the fill.
				DrawRect(rect.Grow(2f), new Color(Glow, (lit ? 0.85f : 0.55f) * wave), false, 2f);
				DrawRect(rect, Empty, true);
				DrawRect(rect.Grow(-3f), new Color(Glow, level), true);
				// Thicker than a held pip's ring, so the pip reads as different even mid-breath.
				DrawRect(rect.Grow(-1.5f), Ring, false, 3f);
				if (Glint && wave > 0.6f)
				{
					float g = (wave - 0.6f) / 0.4f;
					DrawRect(new Rect2(rect.Position + new Vector2(3f, 3f), new Vector2(2f, 2f)), new Color(GlintColour, g), true);
				}
				continue;
			}

			DrawRect(rect, i < Filled ? Ring : Empty, true);
			DrawRect(rect.Grow(-1f), Ring, false, 2f);
		}

		if (flareIndex >= 0 && flareIndex < Count)
		{
			// Eased out: fast at first, settling. Whole-pixel growth, so the ring steps rather than
			// shimmering through fractions.
			float t = Mathf.Clamp((Time.GetTicksMsec() - flareStartMs) / 1000f / FlareSeconds, 0f, 1f);
			float ease = 1f - (1f - t) * (1f - t);
			var rect = PipRect(flareIndex, x0, y0, size, gap);
			if (t < 0.35f)
				DrawRect(rect.Grow(-1f), new Color(GlintColour, 1f - t / 0.35f), true);
			float grow = Mathf.Round(2f + ease * size * 0.9f);
			DrawRect(rect.Grow(grow), new Color(Glow, 0.9f * (1f - ease)), false, 2f);
		}
	}
}
