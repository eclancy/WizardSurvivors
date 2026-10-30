using System;
using Godot;

namespace WizardSurvivors.scripts;

/// <summary>Which glyph an attack shows over its attacker. THE ORDER IS THE ATLAS'S ROW ORDER.</summary>
/// <remarks>tools/art/telegraph_icons.py draws the rows in this order; RegressionChecks checks the sheet's size.</remarks>
public enum TelegraphGlyph
{
	/// <summary>"!": a special that is not a strike - the lantern-tender's dark, a sweeping beam.</summary>
	Alert,
	/// <summary>Claw slashes: a lunge or a charge, coming at the player.</summary>
	Leap,
	/// <summary>An arrow onto the ground: a ring or wedge struck round the attacker, or a hit falling on a spot.</summary>
	Slam,
	/// <summary>A reticle: a bolt, a fan of missiles, a lobbed flame.</summary>
	Shot,
	/// <summary>A starburst: it blows up, or screams, all round itself.</summary>
	Burst,
	/// <summary>A skull: it is calling up the dead.</summary>
	Summon,
}

/// <summary>
/// The icon an attacker shows over its head through a wind-up: it pops in empty, fills from the bottom
/// as the wind-up runs out, blinks through its last third, and flashes white on the strike.
/// </summary>
/// <remarks>
/// Eric, playing 0.9.2: "the attacks on enemies that are telegraphed through transparent buildups
/// animations are weird. like the jump attacks. remove them. if you need to telegraph an attack, do so
/// with icon animation." Every wind-up used to be a translucent shape growing on the floor. This is
/// what replaced the growing - the CLOCK - and <see cref="TelegraphOutline"/> is what replaced the
/// shape, for the attacks where the ground matters (a ring, a wedge, a lane, a hit placed away from
/// its maker). The lunger, the caster and the summoner have no area worth marking and show the icon
/// alone.
///
/// Pixel art out of one atlas (tools/art/telegraph_icons.py, the Bonelight contract): drawn at whole
/// art pixels, never faded, never translucent. The fill is the lit column drawn over the empty one,
/// clipped from the bottom a whole art row at a time, so the gauge steps the way pixel art does.
///
/// Not a Node and allocates nothing: the owner ticks it from its physics step and draws it on a node
/// of its own at <see cref="DrawLayers.AttackIcon"/>, over every body and every projectile, so it
/// stays readable in a packed swarm. It reads the owner's telegraph and never touches it - the wind-up
/// clocks (<see cref="AttackTelegraph"/>) are exactly what they were; the pop and the flash run on
/// this object's own small clock, started by the edges of the owner's wind-up.
/// </remarks>
public sealed class TelegraphIcon
{
	public const string AtlasPath = "res://assets/bonelight/effects/telegraph-icons.png";
	public const int Cell = 16;
	public const int Columns = 4;

	/// <summary>
	/// World units to an art pixel, at the least: 48 units across, the status marks' size. At x2 an
	/// ordinary body's icon was 27 canvas pixels, about two and a half millimetres on a Pixel 9 -
	/// readable on a monitor, a speck in a swarm on the phone. A boss's is x4 (its status scale).
	/// </summary>
	public const float MinPixelScale = 3f;
	public static readonly int Rows = Enum.GetValues<TelegraphGlyph>().Length;

	private const int EmptyColumn = 0;
	private const int FullColumn = 1;
	private const int HotColumn = 2;
	private const int FlashColumn = 3;

	/// <summary>Seconds it is drawn half as big again on appearing: the pop.</summary>
	public const float PopSeconds = 0.1f;

	/// <summary>Seconds the white strike frame shows after the wind-up resolves.</summary>
	public const float FlashSeconds = 0.18f;

	/// <summary>The fraction of the wind-up after which it blinks. The outline on the ground blinks with it.</summary>
	public const float PulseFrom = 0.65f;

	// Half-blinks over the last third: six, so three blinks, faster toward the end (see PulseLit).
	private const float PulseHalfBlinks = 6f;

	private static Texture2D atlas;
	private static bool atlasLoaded;

	private TelegraphGlyph glyph;
	private float progress;
	// Seconds since the wind-up began; negative while none is running.
	private float shownFor = -1f;
	private float flashRemaining;

	/// <summary>Whether there is anything to draw: a wind-up running, or the strike's flash.</summary>
	public bool IsShowing => shownFor >= 0f || flashRemaining > 0f;

	/// <summary>
	/// The blink of the last third, as a function of the wind-up's progress alone, so the icon over the
	/// attacker and the outline on the ground (<see cref="GroundSlamAttack.Draw"/>) blink together
	/// without sharing a clock. Accelerating: the half-blinks are spaced on progress squared.
	/// </summary>
	public static bool PulseLit(float progress)
	{
		if (progress < PulseFrom)
			return false;
		float u = Mathf.Clamp((progress - PulseFrom) / (1f - PulseFrom), 0f, 1f);
		return (int)MathF.Floor(u * u * PulseHalfBlinks) % 2 == 0;
	}

	/// <summary>
	/// Advances one physics step with the owner's current reading. The rising edge of
	/// <paramref name="windingUp"/> pops the icon in; the falling edge is the strike, and flashes it.
	/// </summary>
	public void Tick(float delta, bool windingUp, TelegraphGlyph currentGlyph, float currentProgress)
	{
		if (windingUp)
		{
			if (shownFor < 0f)
			{
				shownFor = 0f;
				flashRemaining = 0f;
			}
			else
				shownFor += delta;
			glyph = currentGlyph;
			progress = Mathf.Clamp(currentProgress, 0f, 1f);
			return;
		}

		if (shownFor >= 0f)
		{
			shownFor = -1f;
			flashRemaining = FlashSeconds;
			return;
		}

		if (flashRemaining > 0f)
			flashRemaining -= delta;
	}

	/// <summary>Forgets any wind-up and flash: for a body recycled, or one that stopped being able to strike.</summary>
	public void Clear()
	{
		shownFor = -1f;
		flashRemaining = 0f;
	}

	/// <summary>
	/// Draws the icon with its bottom edge centred on <paramref name="foot"/>, in the canvas's own units,
	/// <paramref name="px"/> of them to an art pixel (3 over an ordinary body, 4 over a boss).
	/// </summary>
	public void Draw(CanvasItem canvas, Vector2 foot, float px)
	{
		if (canvas == null || !IsShowing)
			return;
		if (!atlasLoaded)
		{
			atlasLoaded = true;
			atlas = ResourceLoader.Exists(AtlasPath) ? GD.Load<Texture2D>(AtlasPath) : null;
			if (atlas == null)
				GD.PrintErr($"TelegraphIcon: {AtlasPath} did not load - run python tools/art/telegraph_icons.py and import it.");
		}
		if (atlas == null)
			return;

		int row = (int)glyph;
		bool striking = shownFor < 0f;
		// The pop on appearing and the punch of the strike: half as big again, in whole world units.
		bool big = striking ? flashRemaining > FlashSeconds * 0.5f : shownFor < PopSeconds;
		float scale = big ? MathF.Ceiling(px * 1.5f) : px;
		float size = Cell * scale;
		var dest = new Rect2(MathF.Round(foot.X - size * 0.5f), MathF.Round(foot.Y - size), size, size);

		if (striking)
		{
			canvas.DrawTextureRectRegion(atlas, dest, new Rect2(FlashColumn * Cell, row * Cell, Cell, Cell));
			return;
		}

		canvas.DrawTextureRectRegion(atlas, dest, new Rect2(EmptyColumn * Cell, row * Cell, Cell, Cell));
		// The gauge: the glyph's rows (1 to 14 of the cell) lit from the bottom as the wind-up runs out.
		// The bottom margin row is in from the first frame, so a lit row means real progress.
		int lit = Mathf.Clamp(Mathf.CeilToInt(progress * (Cell - 2)), 0, Cell - 2) + 1;
		int column = PulseLit(progress) ? HotColumn : FullColumn;
		canvas.DrawTextureRectRegion(atlas,
			new Rect2(dest.Position.X, dest.Position.Y + (Cell - lit) * scale, size, lit * scale),
			new Rect2(column * Cell, row * Cell + (Cell - lit), Cell, lit));
	}
}
