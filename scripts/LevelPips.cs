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
/// A new spell's card animates pip 1, the level taking it gives.
///
/// ONE PIP PER LEVEL (it was one per level after the first, so a new spell had no pip to light).
/// Level 8 is not on a card any more (.ai/level-up-screen.md, 3a), so a card's row is MaxLevel long.
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

	private static readonly Color GlintColour = new Color(1f, 1f, 1f, 1f);

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		FocusMode = FocusModeEnum.None;
		ProcessMode = ProcessModeEnum.Always;
		float width = Count * PipSize + Mathf.Max(0, Count - 1) * Gap;
		CustomMinimumSize = new Vector2(width, PipSize);
		SetProcess(Becoming >= 0);
	}

	public override void _Process(double delta)
	{
		if (IsVisibleInTree())
			QueueRedraw();
	}

	private bool IsLit()
	{
		if (FocusOwner == null || !IsInstanceValid(FocusOwner))
			return false;
		return FocusOwner.HasFocus() || FocusOwner.IsHovered() || FocusOwner.ButtonPressed;
	}

	public override void _Draw()
	{
		float rowWidth = Count * PipSize + Mathf.Max(0, Count - 1) * Gap;
		// Centred in whatever width the container gave; the builders ask for the minimum (ShrinkBegin
		// or ShrinkCenter), so this is normally 0 and the container does the aligning.
		float x0 = Mathf.Floor((Size.X - rowWidth) * 0.5f);
		float y0 = Mathf.Floor((Size.Y - PipSize) * 0.5f);

		// 0..1..0 over a cycle, eased so it lingers full and empty rather than bouncing.
		float phase = (Time.GetTicksMsec() / 1000f) % CycleSeconds / CycleSeconds;
		float wave = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.Tau);
		bool lit = IsLit();
		float floor = lit ? 0.45f : 0.15f;
		float level = Mathf.Lerp(floor, 1f, wave);

		for (int i = 0; i < Count; i++)
		{
			var rect = new Rect2(x0 + i * (PipSize + Gap), y0, PipSize, PipSize);
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
	}
}
