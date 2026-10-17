using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Roles in a terrain autotile set. Matches the `autotile.role` strings in the
// curated tile metadata (see RoleKey) so resolved cells can be mapped to tile ids.
public enum AutotileRole
{
	Fill,
	Patch,
	EdgeTop,
	EdgeBottom,
	EdgeLeft,
	EdgeRight,
	CornerTL,
	CornerTR,
	CornerBL,
	CornerBR,
	InnerCornerTL,
	InnerCornerTR,
	InnerCornerBL,
	InnerCornerBR
}

// The resolved tile for one cell: which terrain and which autotile role.
public readonly struct TileResolution
{
	public readonly string Terrain;
	public readonly AutotileRole Role;

	public TileResolution(string terrain, AutotileRole role)
	{
		Terrain = terrain;
		Role = role;
	}

	public override string ToString() => $"{Terrain}:{TerrainAutotiler.RoleKey(Role)}";
}

/// <summary>
/// Converts a logical terrain grid (produced by a procedural level generator) into
/// per-cell tile choices using the same corner/edge/fill convention as the curated
/// autotile metadata.
///
/// Pipeline:
///   1. Generator fills <c>string[,] grid</c> with terrain ids ("dirt","grass","water",...).
///   2. <see cref="Resolve"/> optionally runs <see cref="CleanupGrid"/> to force shapes the
///      tileset can render, then classifies every non-base cell into an <see cref="AutotileRole"/>.
///   3. Caller maps (terrain, role) -> tile id / atlas coords and paints a TileMapLayer.
///
/// Convention (region of `terrain` surrounded by base):
///   corner_tl = top-left corner of the region (base to the N and W, terrain to the E and S).
///   edge_top  = top border (base to the N, terrain E/S/W). etc.
///   patch     = isolated / unsupported shape (single cells, ends, 1-wide strips).
/// </summary>
public sealed class TerrainAutotiler
{
	// Set true only if the tileset actually contains inner (concave) corner tiles.
	// All generated curated terrains (grass/dark_dirt/water/lava/pit/sand) now ship
	// gen_<terrain>_inner_corner_{tl,tr,bl,br}, so this defaults on.
	public bool HasInnerCorners { get; set; } = true;

	// Cells with <= this many same-terrain orthogonal neighbours are removed during cleanup
	// (isolated cells and dead-end nubs the tileset cannot render cleanly).
	public int MinNeighbours { get; set; } = 2;

	public int MaxCleanupPasses { get; set; } = 8;

	/// <summary>
	/// Classify every cell of <paramref name="grid"/>. The grid is indexed [x, y].
	/// Base-terrain cells resolve to (baseTerrain, Fill). Pass a clone if you want to keep the input.
	/// </summary>
	public TileResolution[,] Resolve(string[,] grid, string baseTerrain, bool cleanup = true)
	{
		string[,] work = (string[,])grid.Clone();
		var result = new TileResolution[grid.GetLength(0), grid.GetLength(1)];
		ResolveInPlace(work, baseTerrain, cleanup, new string[grid.GetLength(0), grid.GetLength(1)], result);
		return result;
	}

	/// <summary>
	/// <see cref="Resolve"/> without allocating: cleans <paramref name="work"/> IN PLACE (using
	/// <paramref name="scratch"/>, same size, as the previous pass) and writes into
	/// <paramref name="result"/>. TerrainStreamer resolves a chunk while the game runs and reuses
	/// these buffers for every chunk; the per-chunk garbage was what kept triggering collector
	/// pauses of 25-40 ms inside a chunk step.
	/// </summary>
	public void ResolveInPlace(string[,] work, string baseTerrain, bool cleanup, string[,] scratch, TileResolution[,] result)
	{
		int w = work.GetLength(0);
		int h = work.GetLength(1);
		if (cleanup)
		{
			CleanupGrid(work, baseTerrain, scratch);
		}

		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				string t = work[x, y];
				if (t == baseTerrain)
				{
					result[x, y] = new TileResolution(baseTerrain, AutotileRole.Fill);
					continue;
				}

				result[x, y] = new TileResolution(t, RoleFor(work, x, y, t, w, h));
			}
		}
	}

	private AutotileRole RoleFor(string[,] g, int x, int y, string t, int w, int h)
	{
		bool Same(int nx, int ny) => nx >= 0 && nx < w && ny >= 0 && ny < h && g[nx, ny] == t;

		bool n = Same(x, y - 1);
		bool s = Same(x, y + 1);
		bool e = Same(x + 1, y);
		bool wl = Same(x - 1, y);

		if (n && s && e && wl)
		{
			if (HasInnerCorners)
			{
				bool ne = Same(x + 1, y - 1);
				bool nw = Same(x - 1, y - 1);
				bool se = Same(x + 1, y + 1);
				bool sw = Same(x - 1, y + 1);
				if (!ne) return AutotileRole.InnerCornerTR;
				if (!nw) return AutotileRole.InnerCornerTL;
				if (!se) return AutotileRole.InnerCornerBR;
				if (!sw) return AutotileRole.InnerCornerBL;
			}

			return AutotileRole.Fill;
		}

		if (!n && !wl && e && s) return AutotileRole.CornerTL;
		if (!n && !e && wl && s) return AutotileRole.CornerTR;
		if (!s && !wl && n && e) return AutotileRole.CornerBL;
		if (!s && !e && n && wl) return AutotileRole.CornerBR;

		if (!n && e && s && wl) return AutotileRole.EdgeTop;
		if (!s && e && n && wl) return AutotileRole.EdgeBottom;
		if (!wl && n && e && s) return AutotileRole.EdgeLeft;
		if (!e && n && s && wl) return AutotileRole.EdgeRight;

		// Ends, 1-wide strips, isolated cells: the current tileset has no piece for these.
		return AutotileRole.Patch;
	}

	/// <summary>
	/// Mutates <paramref name="g"/> so its shapes are renderable by a fill+edges+corners set:
	/// removes isolated cells / dead-end nubs, and fills single-cell concave notches. Iterates
	/// to stability (at most <see cref="MaxCleanupPasses"/>) so the result has no 1-wide
	/// protrusions or interior holes of a single cell.
	/// </summary>
	/// <remarks>
	/// EACH PASS READS ONLY THE PREVIOUS PASS, NEVER ITS OWN WRITES (a Jacobi sweep, double
	/// buffered). It used to edit in place in scan order, which let one pass carry a change any
	/// distance along a row - erode one end of a strip and the next cell sees it in the same pass.
	/// That made the result depend on where the grid started, and the world is now generated in
	/// chunks (TerrainStreamer): a chunk is cleaned with a margin of its neighbours' cells, and the
	/// shared border only comes out the same from both sides if a cell's fate is a function of its
	/// neighbourhood alone. Double buffered, a change spreads at most one cell per pass, so after
	/// MaxCleanupPasses a cell is exact whenever the margin around it is wider than that
	/// (TerrainStreamer.Margin). See .ai/terrain.md, "Chunk streaming".
	/// </remarks>
	public void CleanupGrid(string[,] g, string baseTerrain) =>
		CleanupGrid(g, baseTerrain, new string[g.GetLength(0), g.GetLength(1)]);

	/// <summary><see cref="CleanupGrid(string[,], string)"/> with a caller-owned buffer, same size as <paramref name="g"/>.</summary>
	public void CleanupGrid(string[,] g, string baseTerrain, string[,] read)
	{
		int w = g.GetLength(0);
		int h = g.GetLength(1);
		System.Array.Copy(g, read, g.Length);

		for (int pass = 0; pass < MaxCleanupPasses; pass++)
		{
			bool changed = false;
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					string t = read[x, y];
					string next = t;
					if (t == baseTerrain)
					{
						// Fill a base cell hemmed in on 3+ orthogonal sides by one terrain (a notch/hole).
						string maj = OrthoMajority(read, x, y, w, h, baseTerrain, out int majCount);
						if (maj != null && majCount >= 3)
							next = maj;
					}
					else if (OrthoCount(read, x, y, w, h, t) < MinNeighbours)
					{
						next = baseTerrain;
					}

					if (next != t)
						changed = true;
					g[x, y] = next;
				}
			}

			if (!changed)
			{
				break;
			}

			// g now holds this pass; it becomes what the next pass reads. Swapping the contents
			// rather than the references keeps the caller's array as the one holding the result.
			System.Array.Copy(g, read, g.Length);
		}
	}

	private static int OrthoCount(string[,] g, int x, int y, int w, int h, string t)
	{
		int c = 0;
		if (x - 1 >= 0 && g[x - 1, y] == t) c++;
		if (x + 1 < w && g[x + 1, y] == t) c++;
		if (y - 1 >= 0 && g[x, y - 1] == t) c++;
		if (y + 1 < h && g[x, y + 1] == t) c++;
		return c;
	}

	// Most common non-base terrain among the 4 orthogonal neighbours (null if none). Ties go to the
	// first neighbour in W, E, N, S order; the only caller acts on a count of 3 or more, which out of
	// four can only ever be one terrain, so the tie rule never decides anything.
	//
	// No dictionary: this runs for every floor cell on every cleanup pass, and a chunk is cleaned
	// while the game is running (TerrainStreamer), so it must not allocate.
	private static string OrthoMajority(string[,] g, int x, int y, int w, int h, string baseTerrain, out int count)
	{
		string a = x - 1 >= 0 ? g[x - 1, y] : null;
		string b = x + 1 < w ? g[x + 1, y] : null;
		string c = y - 1 >= 0 ? g[x, y - 1] : null;
		string d = y + 1 < h ? g[x, y + 1] : null;

		string best = null;
		count = 0;
		Consider(a, a, b, c, d, baseTerrain, ref best, ref count);
		Consider(b, a, b, c, d, baseTerrain, ref best, ref count);
		Consider(c, a, b, c, d, baseTerrain, ref best, ref count);
		Consider(d, a, b, c, d, baseTerrain, ref best, ref count);
		return best;
	}

	private static void Consider(string candidate, string a, string b, string c, string d, string baseTerrain,
		ref string best, ref int count)
	{
		if (candidate == null || candidate == baseTerrain) return;
		int n = (a == candidate ? 1 : 0) + (b == candidate ? 1 : 0) + (c == candidate ? 1 : 0) + (d == candidate ? 1 : 0);
		if (n > count)
		{
			count = n;
			best = candidate;
		}
	}

	// Maps an AutotileRole to the role string used in the curated tile metadata / tile ids.
	public static string RoleKey(AutotileRole role) => role switch
	{
		AutotileRole.Fill => "fill",
		AutotileRole.Patch => "patch",
		AutotileRole.EdgeTop => "edge_top",
		AutotileRole.EdgeBottom => "edge_bottom",
		AutotileRole.EdgeLeft => "edge_left",
		AutotileRole.EdgeRight => "edge_right",
		AutotileRole.CornerTL => "corner_tl",
		AutotileRole.CornerTR => "corner_tr",
		AutotileRole.CornerBL => "corner_bl",
		AutotileRole.CornerBR => "corner_br",
		AutotileRole.InnerCornerTL => "inner_corner_tl",
		AutotileRole.InnerCornerTR => "inner_corner_tr",
		AutotileRole.InnerCornerBL => "inner_corner_bl",
		AutotileRole.InnerCornerBR => "inner_corner_br",
		_ => "fill"
	};
}
