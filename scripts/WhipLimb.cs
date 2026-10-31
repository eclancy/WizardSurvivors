using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// One whip-like limb - a tentacle (BlackTentacles) - laid out along a heading that bends as it goes,
/// and drawn as chunky pixel art. Thorned Vine was laid out here too until it became a whip stroke
/// (WhipStroke, 2026-10-31); it still uses the grid helpers (Grid, Cell, SnapSize).
/// </summary>
/// <remarks>
/// <para><b>Layout.</b> The limb is integrated from its root: each segment turns a little further
/// than the last, by a <c>curl</c> that grows toward the tip and a <c>wave</c> that travels down it.
/// That is what makes a limb read as a rope rather than a stick - a curve laid out by angle bends
/// the way a thing with a spine bends. With curl and wave at zero the tip lands exactly on
/// <c>root + heading x length</c>, which is how a strike puts the tip on its target on the frame the
/// damage lands: the hit and the picture are the same point.</para>
///
/// <para><b>Drawing.</b> Stamped, not stroked. A thick <c>DrawPolyline</c> is a rotated quad with a
/// smooth edge, which is the vector look the Bonelight contract rules out; here the limb is laid
/// down as squares on the game's 2 px art grid, outline pass first, then the body, then a lit edge
/// toward the key light (upper left, .ai/art-direction.md section 2). The result has the stair-step
/// edge of the sprites standing next to it. A square every half-width is enough to keep the stroke
/// solid, so a 120 px tentacle is about forty squares a pass.</para>
///
/// <para>Everything lives in buffers allocated once per limb; nothing here allocates per frame.</para>
/// </remarks>
public sealed class WhipLimb
{
	public const int Segments = 12;
	public const float Grid = 2f;

	/// <summary>Segments + 1 points, root first. Valid after <see cref="Layout"/>.</summary>
	public readonly Vector2[] Path = new Vector2[Segments + 1];

	/// <summary>Where the tip is, after <see cref="Layout"/>.</summary>
	public Vector2 Tip => Path[Segments];

	/// <summary>
	/// Lays the limb out from <paramref name="root"/>. <paramref name="curl"/> is how far, in radians,
	/// the tip is turned from the heading (it grows with the square of the distance along the limb);
	/// <paramref name="waveAmp"/> is the swing of a wave travelling root to tip, at
	/// <paramref name="wavePhase"/>.
	/// </summary>
	public void Layout(Vector2 root, float angle, float length, float curl, float waveAmp, float wavePhase)
	{
		Path[0] = root;
		Vector2 p = root;
		float step = MathF.Max(0f, length) / Segments;
		// The segment headings are integrated, so the tip's heading is the sum of the bends: the
		// last segment is turned by about `curl` from the heading, at any segment count.
		for (int j = 1; j <= Segments; j++)
		{
			float s = (j - 0.5f) / Segments;
			float theta = angle + curl * s * s + waveAmp * s * MathF.Sin(wavePhase - s * 4.5f);
			p += new Vector2(MathF.Cos(theta), MathF.Sin(theta)) * step;
			Path[j] = p;
		}
	}

	/// <summary>Position at <paramref name="s"/> (0 root, 1 tip) along the laid-out limb.</summary>
	public Vector2 At(float s)
	{
		float f = Mathf.Clamp(s, 0f, 1f) * Segments;
		int j = Math.Min((int)f, Segments - 1);
		return Path[j].Lerp(Path[j + 1], f - j);
	}

	/// <summary>Unit direction of the limb at <paramref name="s"/>.</summary>
	public Vector2 DirectionAt(float s)
	{
		float f = Mathf.Clamp(s, 0f, 1f) * Segments;
		int j = Math.Min((int)f, Segments - 1);
		Vector2 d = Path[j + 1] - Path[j];
		return d.LengthSquared() > 1e-6f ? d.Normalized() : Vector2.Right;
	}

	/// <summary>
	/// Stamps the limb onto <paramref name="canvas"/> in three passes: an outline
	/// <paramref name="outlinePad"/> px wider than the body, the body (tapering from
	/// <paramref name="baseWidth"/> to <paramref name="tipWidth"/>, coloured root to tip), and a lit
	/// edge on the side facing the key light.
	/// </summary>
	public void Draw(CanvasItem canvas, float baseWidth, float tipWidth, Color outline, Color rootColour,
		Color tipColour, Color litEdge, float outlinePad = 2f, float taper = 1f)
	{
		this.taper = taper;
		float length = 0f;
		for (int j = 0; j < Segments; j++)
			length += Path[j].DistanceTo(Path[j + 1]);
		if (length < Grid)
			return;

		Stamp(canvas, length, baseWidth + outlinePad * 2f, tipWidth + outlinePad * 2f, outline, outline);
		Stamp(canvas, length, baseWidth, tipWidth, rootColour, tipColour);

		// The lit edge: one art pixel on the upper-left side of the body, along the thicker two
		// thirds only - at the tip the limb is too thin to have a side.
		for (float d = 0f; d < length * 0.7f; d += Grid * 2f)
		{
			float s = d / length;
			float w = WidthAt(baseWidth, tipWidth, s);
			if (w < 4f)
				break;
			Vector2 dir = DirectionAt(s);
			Vector2 normal = new Vector2(-dir.Y, dir.X);
			// Of the two sides, the one facing up-left (-1, -1).
			if (normal.X + normal.Y > 0f)
				normal = -normal;
			Vector2 at = At(s) + normal * (w * 0.5f - Grid);
			canvas.DrawRect(Cell(at, Grid), litEdge);
		}
	}

	// How the width falls from root to tip: 1 is a straight wedge, above 1 a thick root that thins
	// quickly into a long whip - a tentacle, not a spike.
	private float taper = 1f;

	private float WidthAt(float baseWidth, float tipWidth, float s) =>
		tipWidth + (baseWidth - tipWidth) * MathF.Pow(1f - Mathf.Clamp(s, 0f, 1f), taper);

	// Squares along the limb, each snapped to the art grid, spaced by half their own width so the
	// stroke never shows a gap.
	private void Stamp(CanvasItem canvas, float length, float baseWidth, float tipWidth, Color rootColour, Color tipColour)
	{
		float d = 0f;
		while (d <= length)
		{
			float s = d / length;
			float w = SnapSize(WidthAt(baseWidth, tipWidth, s));
			canvas.DrawRect(Cell(At(s), w), rootColour.Lerp(tipColour, s));
			d += MathF.Max(Grid, Mathf.Floor(w * 0.5f / Grid) * Grid);
		}
	}

	/// <summary>A size rounded to whole art pixels, never below one.</summary>
	public static float SnapSize(float size) => MathF.Max(Grid, MathF.Round(size / Grid) * Grid);

	/// <summary>A square of <paramref name="size"/> centred on <paramref name="centre"/>, on the art grid.</summary>
	public static Rect2 Cell(Vector2 centre, float size)
	{
		float half = size * 0.5f;
		float x = MathF.Round((centre.X - half) / Grid) * Grid;
		float y = MathF.Round((centre.Y - half) / Grid) * Grid;
		return new Rect2(x, y, size, size);
	}
}
