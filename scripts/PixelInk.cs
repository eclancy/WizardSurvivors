using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// Strokes, cells and discs stamped as pixel art on the world's 2-unit art grid (.ai/art-direction.md
/// sections 1 and 3): the thin, short-lived parts of the player's spells - a chain's arc, a thread, a
/// spark, a crack, a shard in flight - that are lines rather than areas.
/// </summary>
/// <remarks>
/// THE PIXEL-ART PASS (2026-10-29, .ai/art-replacement-manifest.md section 9). These were DrawLine,
/// DrawArc and DrawCircle calls and Line2D nodes: antialiased or not, a line at any angle but the four
/// square ones is drawn as a rotated quad, which is the smooth vector look the contract rules out, and
/// every one of them faded by alpha. Here a line is walked cell by cell (Bresenham on the 2-unit grid),
/// every cell is an opaque square on that grid, and a fade is a dissolve through the 4x4 Bayer matrix -
/// the only gradient the contract allows. The areas are shaders (void_lance.gdshader's way); these are
/// the strokes, stamped as ImpactDebris stamps its debris (WhipLimb.Cell).
///
/// THE CANVAS MUST BE WORLD-ALIGNED: a node at the identity transform (TopLevel at the origin, or a
/// child of the arena), so its units are world units and its grid is the world's. Nothing here
/// allocates. Runs of cells along a line are merged into one rect, so a stroke costs a rect a step of
/// its minor axis, not a rect a cell.
/// </remarks>
public static class PixelInk
{
	public const float Grid = WhipLimb.Grid;

	private static readonly float[] BayerMatrix =
	{
		0f, 8f, 2f, 10f,
		12f, 4f, 14f, 6f,
		3f, 11f, 1f, 9f,
		15f, 7f, 13f, 5f,
	};

	/// <summary>The 4x4 ordered-dither threshold of a cell, in (0, 1).</summary>
	public static float Bayer(int cx, int cy)
	{
		int x = cx & 3;
		int y = cy & 3;
		return (BayerMatrix[x + y * 4] + 0.5f) / 16f;
	}

	/// <summary>Whether a cell survives at this coverage: 1 keeps every cell, 0 none, between dissolves.</summary>
	public static bool Keeps(int cx, int cy, float coverage) => coverage >= 1f || (coverage > 0f && coverage > Bayer(cx, cy));

	public static int CellOf(float v) => (int)MathF.Floor(v / Grid);

	/// <summary>The one art pixel <paramref name="p"/> falls in.</summary>
	public static void Cell(CanvasItem canvas, Vector2 p, Color color, float coverage = 1f)
	{
		int cx = CellOf(p.X), cy = CellOf(p.Y);
		if (Keeps(cx, cy, coverage))
			canvas.DrawRect(new Rect2(cx * Grid, cy * Grid, Grid, Grid), color);
	}

	/// <summary>A square of <paramref name="cells"/> art pixels a side, centred on the cell <paramref name="p"/> falls in.</summary>
	public static void Block(CanvasItem canvas, Vector2 p, int cells, Color color, float coverage = 1f)
	{
		int cx = CellOf(p.X) - (cells - 1) / 2, cy = CellOf(p.Y) - (cells - 1) / 2;
		if (coverage >= 1f)
		{
			canvas.DrawRect(new Rect2(cx * Grid, cy * Grid, cells * Grid, cells * Grid), color);
			return;
		}
		for (int y = 0; y < cells; y++)
		{
			for (int x = 0; x < cells; x++)
			{
				if (Keeps(cx + x, cy + y, coverage))
					canvas.DrawRect(new Rect2((cx + x) * Grid, (cy + y) * Grid, Grid, Grid), color);
			}
		}
	}

	/// <summary>
	/// A stroke from <paramref name="a"/> to <paramref name="b"/>, <paramref name="cells"/> art pixels thick
	/// across its run, opaque, dissolving through the Bayer matrix below full <paramref name="coverage"/>.
	/// </summary>
	public static void Line(CanvasItem canvas, Vector2 a, Vector2 b, Color color, int cells = 1, float coverage = 1f)
	{
		if (coverage <= 0f || cells <= 0)
			return;
		int x0 = CellOf(a.X), y0 = CellOf(a.Y);
		int x1 = CellOf(b.X), y1 = CellOf(b.Y);
		int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
		int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
		bool xMajor = dx >= dy;
		int lo = -(cells - 1) / 2;
		int err = dx - dy;
		int x = x0, y = y0;
		// The run being gathered: cells along the major axis that share a minor coordinate.
		int runStart = xMajor ? x : y, runMinor = xMajor ? y : x, runLength = 0;
		bool whole = coverage >= 1f;
		while (true)
		{
			if (whole)
			{
				int major = xMajor ? x : y, minor = xMajor ? y : x;
				if (runLength > 0 && minor == runMinor && major == runStart + runLength * (xMajor ? sx : sy))
				{
					runLength++;
				}
				else
				{
					FlushRun(canvas, xMajor, runStart, runMinor, runLength, xMajor ? sx : sy, lo, cells, color);
					runStart = major;
					runMinor = minor;
					runLength = 1;
				}
			}
			else
			{
				for (int k = 0; k < cells; k++)
				{
					int cx = xMajor ? x : x + lo + k, cy = xMajor ? y + lo + k : y;
					if (Keeps(cx, cy, coverage))
						canvas.DrawRect(new Rect2(cx * Grid, cy * Grid, Grid, Grid), color);
				}
			}
			if (x == x1 && y == y1)
				break;
			int e2 = 2 * err;
			if (e2 > -dy)
			{
				err -= dy;
				x += sx;
			}
			if (e2 < dx)
			{
				err += dx;
				y += sy;
			}
		}
		if (whole)
			FlushRun(canvas, xMajor, runStart, runMinor, runLength, xMajor ? sx : sy, lo, cells, color);
	}

	private static void FlushRun(CanvasItem canvas, bool xMajor, int start, int minor, int length, int step, int lo, int cells, Color color)
	{
		if (length <= 0)
			return;
		int first = step > 0 ? start : start - (length - 1);
		if (xMajor)
			canvas.DrawRect(new Rect2(first * Grid, (minor + lo) * Grid, length * Grid, cells * Grid), color);
		else
			canvas.DrawRect(new Rect2((minor + lo) * Grid, first * Grid, cells * Grid, length * Grid), color);
	}

	/// <summary>
	/// An ellipse's outline, <paramref name="cells"/> art pixels thick, stamped at a step of at most one
	/// cell round it (a circle when the radii match).
	/// </summary>
	public static void Ring(CanvasItem canvas, Vector2 centre, float rx, float ry, Color color, int cells = 1, float coverage = 1f)
	{
		if (coverage <= 0f || rx < Grid * 0.5f || ry < Grid * 0.5f)
			return;
		int steps = Math.Max(8, (int)MathF.Ceiling(Mathf.Tau * MathF.Max(rx, ry) / Grid));
		for (int k = 0; k < steps; k++)
		{
			float a = Mathf.Tau * k / steps;
			Block(canvas, centre + new Vector2(MathF.Cos(a) * rx, MathF.Sin(a) * ry), cells, color, coverage);
		}
	}

	/// <summary>A filled disc of art pixels, a row a rect, dissolving below full coverage.</summary>
	public static void Disc(CanvasItem canvas, Vector2 centre, float radius, Color color, float coverage = 1f)
	{
		if (radius < Grid * 0.5f || coverage <= 0f)
			return;
		int cx = CellOf(centre.X), cy = CellOf(centre.Y);
		int r = Math.Max(1, (int)MathF.Round(radius / Grid));
		for (int y = -r; y <= r; y++)
		{
			int half = (int)MathF.Floor(MathF.Sqrt(MathF.Max(0f, (r + 0.25f) * (r + 0.25f) - y * y)));
			if (coverage >= 1f)
			{
				canvas.DrawRect(new Rect2((cx - half) * Grid, (cy + y) * Grid, (half * 2 + 1) * Grid, Grid), color);
				continue;
			}
			for (int x = -half; x <= half; x++)
			{
				if (Keeps(cx + x, cy + y, coverage))
					canvas.DrawRect(new Rect2((cx + x) * Grid, (cy + y) * Grid, Grid, Grid), color);
			}
		}
	}
}
