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
		int w = grid.GetLength(0);
		int h = grid.GetLength(1);

		string[,] work = (string[,])grid.Clone();
		if (cleanup)
		{
			CleanupGrid(work, baseTerrain);
		}

		var result = new TileResolution[w, h];
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

		return result;
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
	/// to stability so the result has no 1-wide protrusions or interior holes of a single cell.
	/// </summary>
	public void CleanupGrid(string[,] g, string baseTerrain)
	{
		int w = g.GetLength(0);
		int h = g.GetLength(1);

		for (int pass = 0; pass < MaxCleanupPasses; pass++)
		{
			bool changed = false;
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					string t = g[x, y];
					if (t == baseTerrain)
					{
						// Fill a base cell hemmed in on 3+ orthogonal sides by one terrain (a notch/hole).
						string maj = OrthoMajority(g, x, y, w, h, baseTerrain, out int majCount);
						if (maj != null && majCount >= 3)
						{
							g[x, y] = maj;
							changed = true;
						}
						continue;
					}

					int same = OrthoCount(g, x, y, w, h, t);
					if (same < MinNeighbours)
					{
						g[x, y] = baseTerrain;
						changed = true;
					}
				}
			}

			if (!changed)
			{
				break;
			}
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

	// Most common non-base terrain among the 4 orthogonal neighbours (null if none).
	private static string OrthoMajority(string[,] g, int x, int y, int w, int h, string baseTerrain, out int count)
	{
		var tally = new Dictionary<string, int>();
		void Add(int nx, int ny)
		{
			if (nx < 0 || nx >= w || ny < 0 || ny >= h) return;
			string t = g[nx, ny];
			if (t == baseTerrain) return;
			tally[t] = tally.TryGetValue(t, out int v) ? v + 1 : 1;
		}

		Add(x - 1, y);
		Add(x + 1, y);
		Add(x, y - 1);
		Add(x, y + 1);

		string best = null;
		count = 0;
		foreach (var kv in tally)
		{
			if (kv.Value > count)
			{
				count = kv.Value;
				best = kv.Key;
			}
		}

		return best;
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
