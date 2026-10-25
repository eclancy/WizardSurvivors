using Godot;

/// <summary>
/// The gold seal on an Arcane Upgrade's page once it reaches its last level, and the stamp that
/// presses it there the moment it does (.ai/level-up-screen.md, "Arcane Upgrades").
/// </summary>
/// <remarks>
/// A full-rect child of the card, drawing the seal in the card's top right corner. It is a drawn
/// Control rather than an anchored TextureRect because the card is a PanelContainer, which fits
/// every child to its own rect - a TextureRect there would be stretched across the whole page.
///
/// THE STAMP STEPS THROUGH WHOLE SCALES - x5, x4, x3, then the resting x2 - so every frame of it
/// is on exact pixels. Pixel art scaled through fractions shimmers, which is why MutationGlow moves
/// only alpha and LevelPips grows its rings a whole pixel at a time. It lands in under a quarter of
/// a second and never blocks input: it is a drawing, and the card is already finished underneath it.
///
/// No allocation per frame; it only processes while the stamp is falling. The texture is loaded
/// per call (ResourceLoader caches it): a static would outlive the engine's reference, the
/// teardown crash BonelightSkin's remarks describe.
/// </remarks>
public partial class MasterySeal : Control
{
	public const string TexturePath = "res://assets/bonelight/ui/ui-mastery-seal.png";

	/// <summary>Drawn size at rest: 22x24 authored at x2.</summary>
	public static readonly Vector2 RestSize = new Vector2(44f, 48f);

	/// <summary>From the card's top right corner to the seal's top right corner: inside the tear and
	/// clear of the rubric's corner leaf, never past the card's edge, so a ScrollContainer that clips
	/// at the top of the list cannot cut it.</summary>
	private static readonly Vector2 CornerInset = new Vector2(10f, 8f);

	private const float StampSeconds = 0.22f;
	private static readonly int[] StampScales = { 5, 4, 3, 2 };

	private Texture2D texture;
	private ulong stampStartMs;
	private bool stamping;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		FocusMode = FocusModeEnum.None;
		texture = GD.Load<Texture2D>(TexturePath);
		SetProcess(false);
	}

	/// <summary>Shows the seal at rest, or hides it.</summary>
	public void SetShown(bool shown)
	{
		stamping = false;
		SetProcess(false);
		Visible = shown;
		Modulate = Colors.White;
		QueueRedraw();
	}

	/// <summary>Presses the seal onto the page: it falls through whole scales onto its corner.</summary>
	public void Stamp()
	{
		Visible = true;
		stamping = true;
		stampStartMs = Time.GetTicksMsec();
		SetProcess(true);
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (stamping && (Time.GetTicksMsec() - stampStartMs) / 1000f >= StampSeconds)
		{
			stamping = false;
			SetProcess(false);
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (texture == null)
			return;

		int scale = 2;
		float alpha = 1f;
		if (stamping)
		{
			float t = Mathf.Clamp((Time.GetTicksMsec() - stampStartMs) / 1000f / StampSeconds, 0f, 1f);
			int step = Mathf.Min(StampScales.Length - 1, (int)(t * StampScales.Length));
			scale = StampScales[step];
			alpha = Mathf.Lerp(0.35f, 1f, (step + 1f) / StampScales.Length);
		}

		// The seal's centre at rest stays put while it falls, so it lands where it settles.
		Vector2 rest = RestSize;
		Vector2 centre = new Vector2(Size.X - CornerInset.X - rest.X * 0.5f, CornerInset.Y + rest.Y * 0.5f);
		Vector2 drawn = new Vector2(texture.GetWidth() / 2f * scale, texture.GetHeight() / 2f * scale);
		Vector2 corner = (centre - drawn * 0.5f).Round();
		DrawTextureRect(texture, new Rect2(corner, drawn), false, new Color(1f, 1f, 1f, alpha));
	}
}
