using Godot;
using System;
using WizardSurvivors.scripts;

/// <summary>
/// A boon's relic in its own light: a breathing halo, a rune ring turning behind it, and the icon
/// on top. Everywhere a boon's picture is shown, it is shown through this.
/// </summary>
/// <remarks>
/// WHY A BOON NEEDS ONE. A boon icon is an object drawn on the material rows (tools/art/boon_icons.py)
/// - that is what keeps it from reading as a spell. It also made it read as an ITEM: Iron Rivets on
/// glass was a plate of steel, the same kind of thing as a relic out of a chest, and the player asked
/// for boons to "always look magical". The relic keeps its material drawing; the magic is what is
/// put round it, in the boon's first element (the same tag the generator bakes the fringe and inlay
/// in, so the two halves agree).
///
/// FOUR LAYERS, ONE INTEGER SCALE. Backlight, breath and ring come from one atlas,
/// assets/bonelight/ui/ui-boon-aura.png, authored 1:1 in a 44 cell - the 32 icon cell with a six
/// pixel band round it, which is where the ring lives. All three are drawn at the same whole-number
/// scale, so the relic lands on exact pixels (the old window drew a 64px icon at 68, x2.125) and the
/// ring cannot sit a fraction of a pixel off it. The caller sizes this control; it works out the
/// largest scale that fits and centres the cell.
///
/// THE BACKLIGHT NEVER PULSES. It is the light ground the dark relics (the flask, the veil, the
/// chain) are silhouetted against now that the reliquary window is night glass rather than pale
/// glass, so it stays at full strength and only the BREATH - the outer rings - rises and falls.
/// How readable a relic is never depends on where in its breath the aura happens to be.
///
/// THE RING TURNS BY FRAMES. Twelve frames drawn on the grid by the generator, not one texture
/// rotated by a transform - nearest-neighbour rotation of pixel art crawls. The breath is the only
/// thing that changes continuously, and only in alpha.
///
/// Cheap by construction: four texture draws, no allocation in _Process or _Draw, and
/// a redraw only when the ring frame or the halo's alpha has actually moved.
/// </remarks>
public partial class BoonAura : Control
{
	// Must match tools/art/boon_icons.py: AURA_CELL, AURA_FRAMES, AURA_BACKLIGHT_COLUMN,
	// AURA_BREATH_COLUMN. One atlas row per element, in Element enum order.
	public const int Cell = 44;
	private const int IconCell = 32;
	private const int Frames = 12;
	private const int BacklightColumn = 12;
	private const int BreathColumn = 13;
	private const string AtlasPath = "res://assets/bonelight/ui/ui-boon-aura.png";

	// One step of the ring every quarter second at rest - a full turn in eighteen seconds, slow
	// enough to sit still beside text - and about twice that while the card is hovered or focused,
	// so the card the player is about to take visibly wakes up.
	private const float RestFrameSeconds = 0.24f;
	private const float LitFrameSeconds = 0.11f;
	private const float RestBreathSeconds = 2.6f;
	private const float LitBreathSeconds = 1.5f;

	private static Texture2D atlas;

	/// <summary>The relic itself, drawn over the aura at the same scale.</summary>
	public Texture2D Icon { get; set; }

	/// <summary>The element the aura burns in - the boon's first tag (<see cref="ElementFor"/>).</summary>
	public Element AuraElement { get; set; } = Element.Arcane;

	/// <summary>
	/// 1 on a level-up card. Lower for a list that only reports what is owned, where the aura
	/// should say "this is magic" without pulling the eye off the row beside it.
	/// </summary>
	public float Intensity { get; set; } = 1f;

	/// <summary>
	/// The card this relic belongs to, if any. While it is hovered or focused the ring turns faster
	/// and the halo breathes brighter. Passed in rather than found: on a phone the card Button is a
	/// sibling of the content, not its ancestor.
	/// </summary>
	public BaseButton FocusOwner { get; set; }

	/// <summary>
	/// The element a boon's aura shows: its first tag, as BoonCatalog lists it. Same rule as
	/// ELEMENT in tools/art/boon_icons.py.
	/// </summary>
	public static Element ElementFor(BoonDefinition boon)
	{
		if (boon?.ElementWeights == null || boon.ElementWeights.Length == 0)
			return Element.Arcane;
		return Enum.TryParse(boon.ElementWeights[0].Element, true, out Element element) ? element : Element.Arcane;
	}

	/// <summary>The largest whole scale at which the 44 cell fits a square of this side.</summary>
	public static int ScaleFor(float side) => Math.Max(1, (int)MathF.Floor(side / Cell));

	private float ringClock;
	private float breathClock;
	private int drawnFrame = -1;
	private float drawnAlpha = -1f;

	public override void _Ready()
	{
		atlas ??= GD.Load<Texture2D>(AtlasPath);
		TextureFilter = TextureFilterEnum.Nearest;
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public override void _Process(double delta)
	{
		bool lit = IsLit();
		float d = (float)delta;
		ringClock += d / (lit ? LitFrameSeconds : RestFrameSeconds);
		breathClock += d * Mathf.Tau / (lit ? LitBreathSeconds : RestBreathSeconds);
		if (ringClock >= Frames)
			ringClock -= Frames;
		if (breathClock >= Mathf.Tau)
			breathClock -= Mathf.Tau;

		int frame = (int)ringClock;
		float alpha = HaloAlpha(lit);
		if (frame != drawnFrame || MathF.Abs(alpha - drawnAlpha) > 0.01f)
			QueueRedraw();
	}

	private bool IsLit()
	{
		if (FocusOwner == null || !IsInstanceValid(FocusOwner))
			return false;
		return FocusOwner.HasFocus() || FocusOwner.IsHovered() || FocusOwner.ButtonPressed;
	}

	private float HaloAlpha(bool lit)
	{
		// A breath, not a blink: it never goes out, and lit only raises the floor and the pace.
		float wave = 0.5f + 0.5f * MathF.Sin(breathClock);
		float low = lit ? 0.6f : 0.25f;
		return Mathf.Lerp(low, 1f, wave) * Intensity;
	}

	public override void _Draw()
	{
		drawnFrame = (int)ringClock % Frames;
		drawnAlpha = HaloAlpha(IsLit());

		int scale = ScaleFor(MathF.Min(Size.X, Size.Y));
		float side = Cell * scale;
		// Whole pixels, or the relic's x2 pixels land half on one screen pixel and half on the next.
		var origin = new Vector2(MathF.Floor((Size.X - side) / 2f), MathF.Floor((Size.Y - side) / 2f));
		var cellRect = new Rect2(origin, new Vector2(side, side));

		if (atlas != null)
		{
			float row = (int)AuraElement * Cell;
			var steady = new Color(1f, 1f, 1f, Mathf.Clamp(Intensity, 0f, 1f));
			DrawTextureRectRegion(atlas, cellRect, new Rect2(BacklightColumn * Cell, row, Cell, Cell), steady);
			DrawTextureRectRegion(atlas, cellRect, new Rect2(BreathColumn * Cell, row, Cell, Cell), new Color(1f, 1f, 1f, drawnAlpha));
			DrawTextureRectRegion(atlas, cellRect, new Rect2(drawnFrame * Cell, row, Cell, Cell), steady);
		}

		if (Icon != null)
		{
			float band = (Cell - IconCell) / 2f * scale;
			DrawTextureRect(Icon, new Rect2(origin + new Vector2(band, band), new Vector2(IconCell * scale, IconCell * scale)), false);
		}
	}
}
