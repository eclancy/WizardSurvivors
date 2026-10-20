using Godot;
using WizardSurvivors.scripts;

/// <summary>
/// Makes a button visibly alive while an Ascension is waiting: a gold ring that breathes round it,
/// a shimmer that crosses its face now and then, and a small badge with how many are ready.
/// </summary>
/// <remarks>
/// Reported from the Pixel 9: "Ascension button should be animated when an ascension is ready." The
/// strip of ASCEND buttons above the cards already appeared, but the ★ Ascensions button in the
/// corner looked the same on the level-up where nothing was close as on the one where the biggest
/// decision of the run was waiting - and the strip scrolls away with the cards on a phone.
///
/// CALM, BECAUSE IT IS ON EVERY LEVEL-UP until the player takes it. The ring breathes over 2.6 s
/// (BoonAura's resting breath, so the screen has one rhythm), and the shimmer crosses once every
/// 4 s and takes 1.1 s to do it - most of the time nothing is moving but the ring. Nothing flashes.
/// Every instance reads the same clock (Time.GetTicksMsec, which runs while the tree is paused), so
/// the corner button and the strip shimmer together rather than in a ripple.
///
/// WHEN NOTHING IS READY IT IS NOT THERE: hidden, and not processing. It never takes input
/// (MouseFilter Ignore throughout) and never takes focus.
///
/// No allocation per frame: the shimmer's two quads are fixed arrays rewritten in place, the badge
/// text is built when the count changes, and a redraw is only queued while visible.
/// </remarks>
public partial class AscensionReadyGlow : Control
{
	private const float BreathSeconds = 2.6f;
	private const float SheenPeriodSeconds = 4.0f;
	private const float SheenSweepSeconds = 1.1f;
	private const float SheenWidth = 46f;
	private const float SheenSlant = 18f;
	private const float BadgeRadius = 12f;

	// gold hi and gold lit, the contract's two brightest golds; the ring is the Ascension's colour on
	// every page it appears on (InkGold, IconGold).
	private static readonly Color RingColour = new Color(1.0f, 0.86f, 0.42f);
	private static readonly Color BadgeFill = new Color(0.847f, 0.690f, 0.290f);
	private static readonly Color BadgeRim = new Color(0.329f, 0.247f, 0.063f);
	// Near-black on gold lit: 9.9:1 (tools/art/ui_contrast.py, "ascension badge").
	private static readonly Color BadgeInk = new Color(0.06f, 0.06f, 0.07f);

	/// <summary>Draw the breathing ring round the button.</summary>
	public bool ShowRing { get; set; } = true;

	/// <summary>The number in the badge; 0 draws no badge.</summary>
	public int Count
	{
		get => count;
		set
		{
			count = Mathf.Max(0, value);
			countText = count > 9 ? "9+" : count.ToString();
			QueueRedraw();
		}
	}

	private int count;
	private string countText = string.Empty;
	private bool active;
	private Control sheen;
	private Font badgeFont;
	private int badgeFontSize = 16;

	private readonly Vector2[] leftQuad = new Vector2[4];
	private readonly Vector2[] rightQuad = new Vector2[4];
	private readonly Color[] leftColours = new Color[4];
	private readonly Color[] rightColours = new Color[4];

	/// <summary>Adds a glow to <paramref name="button"/> (once) and returns it, switched off.</summary>
	public static AscensionReadyGlow AttachTo(Control button, bool showRing)
	{
		var existing = button.GetNodeOrNull<AscensionReadyGlow>("AscensionReadyGlow");
		if (existing != null)
			return existing;

		var glow = new AscensionReadyGlow
		{
			Name = "AscensionReadyGlow",
			ShowRing = showRing,
			MouseFilter = MouseFilterEnum.Ignore,
			FocusMode = FocusModeEnum.None,
			// The level-up screen pauses the tree; a glow that froze with it would be a still.
			ProcessMode = ProcessModeEnum.Always,
			Visible = false,
		};
		glow.SetAnchorsPreset(LayoutPreset.FullRect);
		button.AddChild(glow);
		glow.SetProcess(false);
		return glow;
	}

	public override void _Ready()
	{
		// The shimmer is clipped to the button's face; the ring and badge are drawn outside it, so
		// they live on this unclipped parent.
		sheen = new Control
		{
			Name = "Sheen",
			MouseFilter = MouseFilterEnum.Ignore,
			ClipContents = true,
			ShowBehindParent = true,
			// Added light, so the band brightens the stone and the gold letters under it rather than
			// painting a pale stripe over them.
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
		};
		sheen.SetAnchorsPreset(LayoutPreset.FullRect);
		sheen.Draw += DrawSheen;
		AddChild(sheen);

		badgeFont = GetThemeDefaultFont();
		badgeFontSize = ResponsiveLayout.FontSize(this, ResponsiveLayout.TextRole.Micro);
	}

	/// <summary>On while something is ready, off (hidden, not processing) otherwise.</summary>
	public void SetActive(bool value)
	{
		active = value;
		Visible = value;
		SetProcess(value);
		QueueRedraw();
		sheen?.QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (!active || !IsVisibleInTree())
			return;
		QueueRedraw();
		sheen?.QueueRedraw();
	}

	private static float Seconds() => Time.GetTicksMsec() / 1000f;

	public override void _Draw()
	{
		if (!active)
			return;

		if (ShowRing)
		{
			// 0..1..0 over a breath, eased so it lingers at the ends rather than bouncing.
			float breath = 0.5f - 0.5f * Mathf.Cos(Seconds() / BreathSeconds * Mathf.Tau);
			float inner = 0.35f + 0.45f * breath;
			var rect = new Rect2(Vector2.Zero, Size);
			DrawRect(rect.Grow(3f), new Color(RingColour, inner), false, 2f);
			DrawRect(rect.Grow(6f), new Color(RingColour, inner * 0.40f), false, 2f);
		}

		if (count > 0 && badgeFont != null)
		{
			// On the top right corner, half off it, like every notification badge the player has
			// ever seen - the one convention here that needs no explaining.
			var centre = new Vector2(Size.X - 4f, 4f);
			DrawCircle(centre, BadgeRadius + 1.5f, BadgeRim);
			DrawCircle(centre, BadgeRadius, BadgeFill);
			Vector2 textSize = badgeFont.GetStringSize(countText, HorizontalAlignment.Left, -1, badgeFontSize);
			float ascent = badgeFont.GetAscent(badgeFontSize);
			float descent = badgeFont.GetDescent(badgeFontSize);
			var baseline = new Vector2(centre.X - textSize.X * 0.5f, centre.Y + (ascent - descent) * 0.5f);
			DrawString(badgeFont, baseline, countText, HorizontalAlignment.Left, -1, badgeFontSize, BadgeInk);
		}
	}

	private void DrawSheen()
	{
		if (!active || sheen == null)
			return;

		float phase = Seconds() % SheenPeriodSeconds;
		if (phase > SheenSweepSeconds)
			return;

		float t = phase / SheenSweepSeconds;
		// Ease in and out, so the band glides across rather than snapping on at one edge.
		t = t * t * (3f - 2f * t);
		Vector2 size = sheen.Size;
		float x = Mathf.Lerp(-SheenWidth - SheenSlant, size.X + SheenSlant, t);
		float half = SheenWidth * 0.5f;
		var bright = new Color(1.0f, 0.92f, 0.62f, 0.34f);
		var clear = new Color(1.0f, 0.92f, 0.62f, 0f);

		// Two quads sharing the bright centre line: clear - bright - clear, slanted like light
		// raking across a carved face from the upper left.
		leftQuad[0] = new Vector2(x + SheenSlant, 0f);
		leftQuad[1] = new Vector2(x + half + SheenSlant, 0f);
		leftQuad[2] = new Vector2(x + half, size.Y);
		leftQuad[3] = new Vector2(x, size.Y);
		leftColours[0] = clear;
		leftColours[1] = bright;
		leftColours[2] = bright;
		leftColours[3] = clear;

		rightQuad[0] = new Vector2(x + half + SheenSlant, 0f);
		rightQuad[1] = new Vector2(x + SheenWidth + SheenSlant, 0f);
		rightQuad[2] = new Vector2(x + SheenWidth, size.Y);
		rightQuad[3] = new Vector2(x + half, size.Y);
		rightColours[0] = bright;
		rightColours[1] = clear;
		rightColours[2] = clear;
		rightColours[3] = bright;

		sheen.DrawPolygon(leftQuad, leftColours);
		sheen.DrawPolygon(rightQuad, rightColours);
	}
}
