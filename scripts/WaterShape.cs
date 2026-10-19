using System;
using System.Collections.Generic;
using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// The shape of open water, at sub-cell resolution: for every water cell, the convex polygon of
/// it that is actually water. The SAME polygons are what the water collides with (layer 4) and
/// what WaterSurface turns into the distance field its shader draws, so the drawn shore and the
/// place the player's feet stop cannot disagree. See .ai/terrain.md section 3.
/// </summary>
/// <remarks>
/// WHY THIS EXISTS. Water used to be fourteen autotile roles, a whole 48 px cell each with a
/// rounded shoreline cut in, and a lake was visibly built from 48 px stairs. A smooth shoreline
/// needs more than any one cell knows - whether an outer corner is the corner of a big lake (round
/// it) or one step of a diagonal (cut it straight across) is decided two cells away.
///
/// THE ONE RULE: WATER NEVER LEAVES ITS CELLS. The blocking grid is per cell, and it is read by the
/// shared enemy flow field, placement, prop avoidance and the streamer's flags; none of them changed.
/// Every shape here only REMOVES water from blocked cells and never adds it to a walkable one, so
/// the drawn water is always inside the cells those four authorities call water. What makes that
/// enough for smooth shapes is that removing is all a smooth shore needs: a 45 degree staircase of
/// cells becomes a straight diagonal through the outer corners, which are half water, half bank.
///
/// HOW A CELL'S WATER IS BUILT. Start with the cell's square and intersect it with:
///  - an INSET half-plane on every side whose neighbour is dry: the shore sits Inset px in from the
///    cell edge, plus a gentle wander (piecewise linear between the cell corners, from a smooth
///    noise along the line, so it is continuous across cells and chunks);
///  - a BEVEL on every corner whose two side neighbours are water and whose diagonal is dry (the
///    inside of a bend), from one inset line to the other, so no inside corner is a right angle;
///  - a CUT from every OUTER corner nearby: from where the shore turns, walk along both sides to
///    where it turns again. If both walks end in an inside bend within two cells, the corner is a
///    step of a diagonal and the cut is a straight chord between the two bends - and consecutive
///    steps' chords line up, which is what turns a staircase into a straight shore. Otherwise it is
///    a round corner of up to CornerRadius, never more than half the side it sits on, so a lone
///    cell of water is a disc and a two-by-two pond a circle.
/// Every one of those is convex, so every cell's water is one convex polygon: one collision shape,
/// and a point-in-polygon test the distance field can afford.
///
/// SEAM-FREE BY CONSTRUCTION. Everything is a function of the blocked grid round a cell and of a
/// hash of world vertices, so a cell comes out the same from whichever chunk asks, as long as the
/// window round it is wide enough (WindowMargin).
/// </remarks>
public sealed class WaterShape
{
	public const int CellPx = 48;
	/// <summary>Where a straight shore sits, in px in from the edge of the water cell.</summary>
	public const float Inset = 9f;
	/// <summary>How far a straight shore wanders either side of Inset.</summary>
	public const float WanderAmp = 4f;
	/// <summary>The wander's wavelength, in cells: long, so a cell's stretch of shore is near straight.</summary>
	public const int WanderPeriodCells = 3;
	/// <summary>The largest round outer corner.</summary>
	public const float CornerRadius = 40f;
	/// <summary>How far, in cells, a corner looks along a side for the next bend.</summary>
	public const int MaxWalk = 3;
	private const int ArcSegments = 6;

	/// <summary>Cells the shapes are built for, round the chunk: one ring for the distance field's
	/// margin texels, a second because those need their neighbours' shorelines.</summary>
	public const int ShapeRing = 2;
	/// <summary>A cut's ear reaches up to this many cells from the corner that makes it.</summary>
	private const int CutReach = 3;
	/// <summary>Cells of blocked grid read round the chunk: shapes, plus cut sources, plus their walks.</summary>
	public const int WindowMargin = ShapeRing + CutReach + MaxWalk + 2;

	private const int SaltWander = 0x5A7E;

	private readonly int chunk;
	private readonly int win;
	private readonly int shapeSpan;
	private readonly bool[] wet;
	private Vector2I winMin;
	private ulong seed;

	// Per shape cell (index into the ShapeRing-padded square): its polygon in a shared pool.
	private readonly Vector2[] pool;
	private readonly int[] polyStart;
	private readonly int[] polyCount;
	private readonly byte[] kind; // 0 dry, 1 full, 2 partial, 3 empty (wet but nothing left)
	private int poolUsed;

	// Reused chunk to chunk: a busy chunk has a few hundred outer corners, and a new array for each
	// was garbage the collector then charged to some later frame.
	private readonly List<Cut> cuts = new();
	private int cutCount;
	private readonly List<int>[] cutsAt;

	private struct HalfPlane
	{
		public Vector2 A;
		public Vector2 N;
	}

	private sealed class Cut
	{
		public readonly HalfPlane[] Planes = new HalfPlane[ArcSegments + 6];
		public int PlaneCount;
	}

	public WaterShape(int chunkTiles)
	{
		chunk = chunkTiles;
		win = chunkTiles + 2 * WindowMargin;
		shapeSpan = chunkTiles + 2 * ShapeRing;
		wet = new bool[win * win];
		int cells = shapeSpan * shapeSpan;
		pool = new Vector2[cells * 24];
		polyStart = new int[cells];
		polyCount = new int[cells];
		kind = new byte[cells];
		cutsAt = new List<int>[cells];
		for (int i = 0; i < cells; i++)
			cutsAt[i] = new List<int>(2);
	}

	/// <summary>The blocked-grid window to fill: <see cref="WindowCells"/> square from <see cref="WindowMin"/>.</summary>
	public bool[] WetBuffer => wet;
	public int WindowCells => win;
	public Vector2I WindowMin(Vector2I chunkMinCell) => chunkMinCell - new Vector2I(WindowMargin, WindowMargin);

	/// <summary>True if any cell within reach of the chunk is water - if not, there is nothing to build.</summary>
	public bool AnyWater()
	{
		foreach (bool b in wet)
			if (b) return true;
		return false;
	}

	public bool Wet(int cx, int cy)
	{
		int x = cx - winMin.X, y = cy - winMin.Y;
		return x >= 0 && y >= 0 && x < win && y < win && wet[x + y * win];
	}

	/// <summary>
	/// Build every shape cell's polygon. <see cref="WetBuffer"/> must already hold the blocked grid of
	/// the window round the chunk whose first cell is <paramref name="chunkMinCell"/>.
	/// </summary>
	public void Build(Vector2I chunkMinCell, ulong wanderSeed)
	{
		winMin = WindowMin(chunkMinCell);
		seed = wanderSeed;
		shapeMin = chunkMinCell - new Vector2I(ShapeRing, ShapeRing);
		poolUsed = 0;
		cutCount = 0;
		foreach (List<int> l in cutsAt)
			l.Clear();

		// Cuts first: every outer corner of every wet cell whose ear could reach a shape cell.
		int r0 = -ShapeRing - CutReach, r1 = chunk + ShapeRing + CutReach;
		for (int y = r0; y < r1; y++)
		{
			for (int x = r0; x < r1; x++)
			{
				int cx = chunkMinCell.X + x, cy = chunkMinCell.Y + y;
				if (!Wet(cx, cy)) continue;
				for (int sy = -1; sy <= 1; sy += 2)
					for (int sx = -1; sx <= 1; sx += 2)
						if (!Wet(cx + sx, cy) && !Wet(cx, cy + sy))
							AddCut(cx, cy, sx, sy);
			}
		}

		for (int y = 0; y < shapeSpan; y++)
			for (int x = 0; x < shapeSpan; x++)
				BuildCell(x, y);
	}

	private Vector2I shapeMin;

	private int ShapeIndex(int cx, int cy)
	{
		int x = cx - shapeMin.X, y = cy - shapeMin.Y;
		return x >= 0 && y >= 0 && x < shapeSpan && y < shapeSpan ? x + y * shapeSpan : -1;
	}

	/// <summary>0 dry, 1 all water, 2 part water, 3 a water cell with nothing left in it. -1 out of range.</summary>
	public int KindAt(int cx, int cy)
	{
		int i = ShapeIndex(cx, cy);
		return i < 0 ? -1 : kind[i];
	}

	/// <summary>The water polygon of a partial cell, in painter-local px (clockwise on screen).</summary>
	public ReadOnlySpan<Vector2> PolygonAt(int cx, int cy)
	{
		int i = ShapeIndex(cx, cy);
		if (i < 0 || kind[i] != 2) return ReadOnlySpan<Vector2>.Empty;
		return new ReadOnlySpan<Vector2>(pool, polyStart[i], polyCount[i]);
	}

	// --- the wander ---------------------------------------------------------------------------

	// How far in from the cell edge the shore sits at a vertex, on a vertical or a horizontal line.
	// A smooth value noise ALONG the line, seeded by WHICH line, so every cell sharing the vertex
	// reads the same value and a long shore is one gentle curve rather than a zigzag.
	private float Depth(int vx, int vy, bool vertical)
	{
		int along = vertical ? vy : vx;
		int line = vertical ? vx : vy;
		int salt = SaltWander + (vertical ? 1 : 2);
		int i0 = FloorDiv(along, WanderPeriodCells);
		float f = (along - i0 * WanderPeriodCells) / (float)WanderPeriodCells;
		f = f * f * (3f - 2f * f);
		float a = Unit(LevelGenerator.Hash(seed, i0, line, salt));
		float b = Unit(LevelGenerator.Hash(seed, i0 + 1, line, salt));
		return Inset + WanderAmp * (a + (b - a) * f);
	}

	private static float Unit(ulong h) => (float)((h >> 11) * (1.0 / (1UL << 53))) * 2f - 1f;

	private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

	// A point on the inset line of a VERTICAL side at x = vx (vertex column), land on side sx, at
	// vertex row vy: the knot.
	private Vector2 KnotV(int vx, int vy, int sx) => new(vx * CellPx - sx * Depth(vx, vy, true), vy * CellPx);
	private Vector2 KnotH(int vx, int vy, int sy) => new(vx * CellPx, vy * CellPx - sy * Depth(vx, vy, false));

	// --- cuts ---------------------------------------------------------------------------------

	private enum End { Convex, Concave, Long }

	// The outer corner (sx, sy) of wet cell (cx, cy): both side neighbours that way are dry.
	private void AddCut(int cx, int cy, int sx, int sy)
	{
		int vx = cx + (sx > 0 ? 1 : 0), vy = cy + (sy > 0 ? 1 : 0);

		// Side A runs along x = vx away from the corner (direction -sy); side B along y = vy (-sx).
		Vector2 a0 = KnotV(vx, vy, sx), a1 = KnotV(vx, vy - sy, sx);
		Vector2 b0 = KnotH(vx, vy, sy), b1 = KnotH(vx - sx, vy, sy);
		Vector2 corner = Intersect(a0, a1, b0, b1);

		End endA = Walk(cx, cy, 0, -sy, sx, 0, out int nA);
		End endB = Walk(cx, cy, -sx, 0, 0, sy, out int nB);

		bool stepA = endA == End.Concave && nA <= 2;
		bool stepB = endB == End.Concave && nB <= 2;

		Vector2 p1, p2;
		if (stepA)
			p1 = KnotV(vx, vy - sy * nA, sx);
		else
		{
			float leg = CornerRadius;
			if (endA == End.Convex)
			{
				// The far corner's inset row: the shore across the end of the run.
				int ey = vy - sy * nA;
				float far = ey * CellPx + sy * Depth(vx, ey, false);
				leg = Mathf.Min(leg, Mathf.Abs(corner.Y - far) * 0.5f);
			}
			p1 = OnVertical(vx, vy, sx, sy, corner.Y - sy * leg);
		}
		if (stepB)
			p2 = KnotH(vx - sx * nB, vy, sy);
		else
		{
			float leg = CornerRadius;
			if (endB == End.Convex)
			{
				int ex = vx - sx * nB;
				float far = ex * CellPx + sx * Depth(ex, vy, true);
				leg = Mathf.Min(leg, Mathf.Abs(corner.X - far) * 0.5f);
			}
			p2 = OnHorizontal(vx, vy, sx, sy, corner.X - sx * leg);
		}

		// Straight across a step of a diagonal; round on a real corner; between on a mix.
		float m = stepA && stepB ? 1f : (stepA || stepB ? 1.5f : 2f);

		Vector2 da = p1 - corner, db = p2 - corner;
		if (da.LengthSquared() < 1f || db.LengthSquared() < 1f)
			return;
		Vector2 dirA = da.Normalized(), dirB = db.Normalized();
		const float L = 400f;
		Vector2 aFar = p1 + dirA * L, bFar = p2 + dirB * L;
		Vector2 far4 = aFar + bFar - corner;

		// The kept side as a convex polygon: out along A, the arc from P1 to P2, out along B.
		Span<Vector2> ring = stackalloc Vector2[ArcSegments + 5];
		int n = 0;
		ring[n++] = aFar;
		int segs = m <= 1f ? 1 : ArcSegments;
		for (int k = 0; k <= segs; k++)
		{
			float th = Mathf.Pi * 0.5f * (1f - k / (float)segs);
			float c = Mathf.Cos(th), s = Mathf.Sin(th);
			float alpha = 1f - Mathf.Pow(Mathf.Max(0f, c), 2f / m);
			float beta = 1f - Mathf.Pow(Mathf.Max(0f, s), 2f / m);
			ring[n++] = corner + da * alpha + db * beta;
		}
		ring[n++] = bFar;
		ring[n++] = far4;

		if (cutCount == cuts.Count)
			cuts.Add(new Cut());
		Cut cut = cuts[cutCount];
		cut.PlaneCount = 0;
		Vector2 inside = (aFar + bFar + far4) / 3f;
		for (int i = 0; i < n; i++)
		{
			Vector2 p = ring[i], q = ring[(i + 1) % n];
			if ((q - p).LengthSquared() < 1e-4f) continue;
			cut.Planes[cut.PlaneCount++] = Plane(p, q, inside);
		}
		int index = cutCount++;

		// Register with every shape cell the ear (corner, P1, P2) touches.
		float minX = Mathf.Min(corner.X, Mathf.Min(p1.X, p2.X)), maxX = Mathf.Max(corner.X, Mathf.Max(p1.X, p2.X));
		float minY = Mathf.Min(corner.Y, Mathf.Min(p1.Y, p2.Y)), maxY = Mathf.Max(corner.Y, Mathf.Max(p1.Y, p2.Y));
		// Shrunk, not grown: P1 and P2 often sit exactly on a cell corner, and the cell beyond that
		// corner must NOT get this cut - its edge lines, extended past P1, run through open water there.
		int x0 = Mathf.FloorToInt((minX + 0.5f) / CellPx), x1 = Mathf.FloorToInt((maxX - 0.5f) / CellPx);
		int y0 = Mathf.FloorToInt((minY + 0.5f) / CellPx), y1 = Mathf.FloorToInt((maxY - 0.5f) / CellPx);
		for (int y = y0; y <= y1; y++)
			for (int x = x0; x <= x1; x++)
			{
				int si = ShapeIndex(x, y);
				if (si >= 0)
					cutsAt[si].Add(index);
			}
	}

	// From wet cell (cx, cy), walk (dx, dy) along a side whose land is at (lx, ly): how many cells
	// until the shore turns, and which way.
	private End Walk(int cx, int cy, int dx, int dy, int lx, int ly, out int n)
	{
		for (n = 1; n <= MaxWalk; n++)
		{
			int x = cx + dx * n, y = cy + dy * n;
			if (!Wet(x, y)) return End.Convex;
			if (Wet(x + lx, y + ly)) return End.Concave;
		}
		n = MaxWalk;
		return End.Long;
	}

	// The point on side A's inset line (x = vx, land on sx, running from row vy towards -sy) at y.
	private Vector2 OnVertical(int vx, int vy, int sx, int sy, float y)
	{
		for (int k = 0; k < MaxWalk + 1; k++)
		{
			Vector2 p = KnotV(vx, vy - sy * k, sx), q = KnotV(vx, vy - sy * (k + 1), sx);
			float t = (y - p.Y) / (q.Y - p.Y);
			if (t >= -0.001f && t <= 1.001f)
				return p + (q - p) * t;
		}
		return new Vector2(KnotV(vx, vy, sx).X, y);
	}

	private Vector2 OnHorizontal(int vx, int vy, int sx, int sy, float x)
	{
		for (int k = 0; k < MaxWalk + 1; k++)
		{
			Vector2 p = KnotH(vx - sx * k, vy, sy), q = KnotH(vx - sx * (k + 1), vy, sy);
			float t = (x - p.X) / (q.X - p.X);
			if (t >= -0.001f && t <= 1.001f)
				return p + (q - p) * t;
		}
		return new Vector2(x, KnotH(vx, vy, sy).Y);
	}

	private static Vector2 Intersect(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
	{
		Vector2 r = a1 - a0, s = b1 - b0;
		float den = r.X * s.Y - r.Y * s.X;
		if (Mathf.Abs(den) < 1e-6f) return a0;
		float t = ((b0.X - a0.X) * s.Y - (b0.Y - a0.Y) * s.X) / den;
		return a0 + r * t;
	}

	private static HalfPlane Plane(Vector2 p, Vector2 q, Vector2 inside)
	{
		Vector2 n = new(-(q.Y - p.Y), q.X - p.X);
		if (n.Dot(inside - p) < 0) n = -n;
		return new HalfPlane { A = p, N = n.Normalized() };
	}

	// --- cells --------------------------------------------------------------------------------

	private readonly Vector2[] clipA = new Vector2[64];
	private readonly Vector2[] clipB = new Vector2[64];

	private void BuildCell(int sxIdx, int syIdx)
	{
		int si = sxIdx + syIdx * shapeSpan;
		int cx = shapeMin.X + sxIdx, cy = shapeMin.Y + syIdx;
		if (!Wet(cx, cy))
		{
			kind[si] = 0;
			return;
		}

		float x0 = cx * CellPx, y0 = cy * CellPx, x1 = x0 + CellPx, y1 = y0 + CellPx;
		var centre = new Vector2(x0 + CellPx * 0.5f, y0 + CellPx * 0.5f);
		bool constrained = false;
		int n = 4;
		Vector2[] a = clipA, b = clipB;
		a[0] = new Vector2(x0, y0);
		a[1] = new Vector2(x1, y0);
		a[2] = new Vector2(x1, y1);
		a[3] = new Vector2(x0, y1);

		// Insets on the dry sides.
		if (!Wet(cx - 1, cy)) { constrained = true; n = Clip(a, n, b, Plane(KnotV(cx, cy, -1), KnotV(cx, cy + 1, -1), centre)); (a, b) = (b, a); }
		if (!Wet(cx + 1, cy)) { constrained = true; n = Clip(a, n, b, Plane(KnotV(cx + 1, cy, 1), KnotV(cx + 1, cy + 1, 1), centre)); (a, b) = (b, a); }
		if (!Wet(cx, cy - 1)) { constrained = true; n = Clip(a, n, b, Plane(KnotH(cx, cy, -1), KnotH(cx + 1, cy, -1), centre)); (a, b) = (b, a); }
		if (!Wet(cx, cy + 1)) { constrained = true; n = Clip(a, n, b, Plane(KnotH(cx, cy + 1, 1), KnotH(cx + 1, cy + 1, 1), centre)); (a, b) = (b, a); }

		// Bevels on the insides of bends: side neighbours wet, diagonal dry.
		for (int sy = -1; sy <= 1; sy += 2)
		{
			for (int sx = -1; sx <= 1; sx += 2)
			{
				if (!Wet(cx + sx, cy) || !Wet(cx, cy + sy) || Wet(cx + sx, cy + sy)) continue;
				int qx = cx + (sx > 0 ? 1 : 0), qy = cy + (sy > 0 ? 1 : 0);
				// On this cell's x side: the horizontal shore of the neighbour that way, at the vertex.
				Vector2 e1 = KnotH(qx, qy, sy);
				// On this cell's y side: the vertical shore of the neighbour that way.
				Vector2 e2 = KnotV(qx, qy, sx);
				constrained = true;
				n = Clip(a, n, b, Plane(e1, e2, centre));
				(a, b) = (b, a);
			}
		}

		foreach (int ci in cutsAt[si])
		{
			Cut cut = cuts[ci];
			for (int k = 0; k < cut.PlaneCount && n > 0; k++)
			{
				n = Clip(a, n, b, cut.Planes[k]);
				(a, b) = (b, a);
			}
			constrained = true;
		}

		if (!constrained)
		{
			kind[si] = 1;
			return;
		}
		if (n < 3 || Area(a, n) < 2f)
		{
			kind[si] = 3;
			return;
		}
		if (poolUsed + n > pool.Length)
			throw new InvalidOperationException("WaterShape: polygon pool overflow");
		kind[si] = 2;
		polyStart[si] = poolUsed;
		polyCount[si] = n;
		Array.Copy(a, 0, pool, poolUsed, n);
		poolUsed += n;
	}

	private static float Area(Vector2[] p, int n)
	{
		float s = 0;
		for (int i = 0; i < n; i++)
		{
			Vector2 u = p[i], v = p[(i + 1) % n];
			s += u.X * v.Y - v.X * u.Y;
		}
		return Mathf.Abs(s) * 0.5f;
	}

	// Sutherland-Hodgman against one half-plane, src -> dst; returns the new count.
	private static int Clip(Vector2[] src, int n, Vector2[] dst, HalfPlane h)
	{
		int m = 0;
		for (int i = 0; i < n; i++)
		{
			Vector2 p = src[i], q = src[(i + 1) % n];
			float dp = h.N.Dot(p - h.A), dq = h.N.Dot(q - h.A);
			bool pin = dp >= -1e-4f, qin = dq >= -1e-4f;
			if (pin && m < dst.Length) dst[m++] = p;
			if (pin != qin && m < dst.Length)
			{
				float t = dp / (dp - dq);
				dst[m++] = p + (q - p) * t;
			}
		}
		// Drop near-duplicate points, which a plane through a vertex leaves behind.
		int k = 0;
		for (int i = 0; i < m; i++)
		{
			if (k > 0 && (dst[i] - dst[k - 1]).LengthSquared() < 1e-4f) continue;
			dst[k++] = dst[i];
		}
		if (k > 1 && (dst[k - 1] - dst[0]).LengthSquared() < 1e-4f) k--;
		return k;
	}

	// --- shoreline segments, for the distance field -----------------------------------------------

	/// <summary>
	/// Append the shoreline segments of one shape cell - the edges of its polygon that are not on
	/// the cell's own square - to <paramref name="into"/> as (a, b) pairs.
	/// </summary>
	public void ShoreSegments(int cx, int cy, List<Vector2> into)
	{
		if (KindAt(cx, cy) != 2) return;
		ReadOnlySpan<Vector2> poly = PolygonAt(cx, cy);
		float x0 = cx * CellPx, y0 = cy * CellPx, x1 = x0 + CellPx, y1 = y0 + CellPx;
		const float e = 0.01f;
		for (int i = 0; i < poly.Length; i++)
		{
			Vector2 p = poly[i], q = poly[(i + 1) % poly.Length];
			bool onBox = (Mathf.Abs(p.X - x0) < e && Mathf.Abs(q.X - x0) < e)
				|| (Mathf.Abs(p.X - x1) < e && Mathf.Abs(q.X - x1) < e)
				|| (Mathf.Abs(p.Y - y0) < e && Mathf.Abs(q.Y - y0) < e)
				|| (Mathf.Abs(p.Y - y1) < e && Mathf.Abs(q.Y - y1) < e);
			if (onBox) continue;
			into.Add(p);
			into.Add(q);
		}
	}
}
