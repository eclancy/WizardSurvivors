using Godot;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

/// <summary>
/// Shared wall-aware navigation for a maze stage. Computes one distance field from the player's
/// cell over the open (non-wall) cells, and every enemy cheaply reads a movement direction from it
/// that routes along the corridors toward the player. Recomputed a few times per second, so the
/// whole horde shares one O(cells) computation instead of each running its own path search.
///
/// <see cref="Active"/> is the current maze's navigation (or null on non-maze stages).
/// </summary>
/// <remarks>
/// WHY IT IS WEIGHTED, AND NOT A PLAIN BFS. The first version was a four-way breadth-first search
/// whose flow pointed each cell at its single lowest neighbour. Every route that search finds hugs
/// the inside of every corner, because a shortest Manhattan path always does - and an enemy body is
/// 40-50 px wide on a 48 px cell. Told "go right" from the cell beside an inner corner, the body
/// overlapped the wall block below-right and slid into its face with no tangential component left:
/// pinned, still being told "go right". The Cursed Dungeon was reported as enemies "trapped in
/// other corridors most of the time", and _MazeProbe measured a tenth of the stalled horde stopped
/// against a wall rather than queued behind each other.
///
/// So the field is a Dijkstra over eight neighbours (10 straight, 14 diagonal, no cutting a wall
/// corner) with a surcharge for entering a cell that touches a wall. The cheapest route now runs
/// down the middle of a corridor and swings wide round a corner, and the flow is a gradient over
/// every cheaper neighbour rather than one of eight compass points, so a turn is a curve. Costs are
/// small integers, so the queue is a bucket queue and the whole recompute is still linear.
///
/// IT COVERS A WINDOW, NOT A MAP. The maze is endless (TerrainStreamer), so the grid here is a
/// fixed-size window over the painted chunks round the player, moved and refilled by
/// <see cref="Rebuild"/> when he crosses into another chunk.
///
/// IT STOPS AT A HORIZON. Searching all 181x181 cells of the old fixed maze cost about 10 ms a time
/// on a desktop debug build, five times a second, which a phone cannot pay. Nothing past <see cref="SearchHorizonTiles"/>
/// of walking needs a route: Enemy recycles anything that far round (Enemy.MazeRecyclePathFactor),
/// and spawning rejects it. So the search stops there, and "is this a sealed pocket" - which a
/// horizon must not be mistaken for - is answered by connected-component labels worked out once
/// per window instead (Rebuild).
/// </remarks>
public sealed class MazeNavigation
{
	public static MazeNavigation Active { get; set; }

	// Step costs, in tenths of a tile.
	private const int StraightCost = 10;
	private const int DiagonalCost = 14;
	// Surcharge for ENTERING a cell this many tiles (Chebyshev) from a wall. Large enough that a
	// route prefers the corridor's middle, small enough that a long detour is never cheaper than
	// brushing a wall for a tile or two.
	private const int TouchingWallPenalty = 12;
	private const int NearWallPenalty = 4;
	private const int MaxEdgeCost = DiagonalCost + TouchingWallPenalty;

	/// <summary>Walking distance, in tiles, beyond which the field is not computed. Must exceed
	/// Enemy.RespawnDistance * MazeRecyclePathFactor / tile (1600 * 1.6 / 48 = 53) or enemies
	/// between the two would be neither routed nor recycled.</summary>
	public const int SearchHorizonTiles = 64;
	private const int MaxSearchCost = SearchHorizonTiles * StraightCost;

	// How hard a body standing in a wall-touching cell is pushed back toward open floor, relative to
	// the unit flow. Belt and braces for the cases the weighting cannot see: an enemy shoved there by
	// the crowd, or the final approach to a player who is himself standing against the wall.
	private const float WallPushWeight = 0.45f;

	// The live tables every query reads, and a staging copy the next window is built into over
	// several frames (BeginRebuild / StepRebuild) and then swapped in whole.
	private bool[] _solid;
	private int[] _component;
	private bool[] _solidNext;
	private int[] _componentNext;
	private int[] _penaltyNext;
	private Vector2[] _wallPushNext;
	private int _playerComponent = -1;
	private int[] _penalty;
	private Vector2[] _wallPush;
	private readonly int _w;
	private readonly int _h;
	private readonly int _tile;
	// World cell of the window's [0, 0]. Moved by Rebuild.
	private int _offsetX;
	private int _offsetY;
	private readonly Vector2 _origin;
	private readonly int[] _dist;
	private readonly Vector2[] _flow;
	private readonly int[] _flowStamp;
	private int _generation;
	private bool _hasField;
	private readonly List<int>[] _buckets;
	private readonly Stack<int> _componentStack;
	// BuildClearance scratch: is there a wall within 1 / 2 cells along this row.
	private readonly bool[] _rowNear1;
	private readonly bool[] _rowNear2;
	private readonly float _recomputeInterval;
	private float _recomputeTimer;

	// Eight neighbours: four straight, then four diagonal.
	private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
	private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

	public int TileSize => _tile;

	/// <summary>
	/// A window of <paramref name="windowTiles"/> square, empty until <see cref="Rebuild"/> fills it.
	/// The arrays are sized once here and reused by every rebuild, so walking does not allocate.
	/// </summary>
	public MazeNavigation(int windowTiles, int tile, Vector2 origin, float recomputeInterval = 0.2f)
	{
		_w = windowTiles;
		_h = windowTiles;
		int n = _w * _h;
		_solid = new bool[n];
		_tile = tile;
		_origin = origin;
		_dist = new int[n];
		System.Array.Fill(_dist, int.MaxValue);
		_flow = new Vector2[n];
		_flowStamp = new int[n];
		_penalty = new int[n];
		_wallPush = new Vector2[n];
		_component = new int[n];
		_buckets = new List<int>[MaxEdgeCost + 1];
		for (int i = 0; i < _buckets.Length; i++)
			_buckets[i] = new List<int>(256);
		_componentStack = new Stack<int>(1024);
		_rowNear1 = new bool[n];
		_rowNear2 = new bool[n];
		_solidNext = new bool[n];
		_componentNext = new int[n];
		_penaltyNext = new int[n];
		_wallPushNext = new Vector2[n];
		_recomputeInterval = recomputeInterval;
	}

	/// <summary>The streamer chunk the window was last centred on; see <see cref="Rebuild"/>.</summary>
	public Vector2I WindowCentreChunk { get; private set; }
	public bool HasWindow { get; private set; }

	/// <summary>True while a window is being built in the background; see <see cref="StepRebuild"/>.</summary>
	public bool IsRebuilding => _rebuildStage > 0;

	private int _rebuildStage;
	private Vector2I _nextMinCell;
	private Vector2I _nextCentreChunk;
	private TerrainStreamer _nextSource;

	/// <summary>
	/// Start moving the window so it starts at world cell <paramref name="minCell"/>. The new tables
	/// are built into a staging copy by <see cref="StepRebuild"/>, one stage per call, and swapped in
	/// whole at the end - the live window keeps answering every query until then.
	/// </summary>
	/// <remarks>
	/// THE MAZE HAS NO EDGE NOW, SO THE GRID MOVES (.ai/terrain.md, "Chunk streaming"). Node2DGame starts
	/// this when the player has gone well past the chunk the window is centred on and the streamer
	/// has finished loading round him; the window is the painted 5 x 5 chunks. Components are labelled
	/// within the window, so a corridor that only connects to the player's outside it reads as a
	/// separate pocket and is not spawned into - the cautious mistake.
	///
	/// STAGED, because the whole rebuild was 9-12 ms in one frame on a desktop debug build - a
	/// dropped frame or two on a phone, every chunk of travel. Six stages of a few ms each are not.
	/// </remarks>
	public void BeginRebuild(TerrainStreamer streamer, Vector2I minCell, Vector2I centreChunk)
	{
		_nextSource = streamer;
		_nextMinCell = minCell;
		_nextCentreChunk = centreChunk;
		_rebuildStage = 1;
	}

	/// <summary>
	/// One stage of a rebuild started by <see cref="BeginRebuild"/>. Returns true on the call that
	/// swaps the new window in (which also recomputes the field round the player at once, so there is
	/// never a frame where the window has no field and every position counts as reachable).
	/// </summary>
	public bool StepRebuild(Vector2 playerWorld)
	{
		switch (_rebuildStage)
		{
			case 1:
				_nextSource.FillSolid(_solidNext, _nextMinCell, _w, _h);
				_rebuildStage = 2;
				return false;
			case 2:
				BuildClearance();
				_rebuildStage = 3;
				return false;
			case 3:
				BuildClearanceColumns(0, _h / 2);
				_rebuildStage = 4;
				return false;
			case 4:
				BuildClearanceColumns(_h / 2, _h);
				_rebuildStage = 5;
				return false;
			case 5:
				BuildComponents();
				_rebuildStage = 6;
				return false;
			case 6:
				(_solid, _solidNext) = (_solidNext, _solid);
				(_component, _componentNext) = (_componentNext, _component);
				(_penalty, _penaltyNext) = (_penaltyNext, _penalty);
				(_wallPush, _wallPushNext) = (_wallPushNext, _wallPush);
				_offsetX = _nextMinCell.X;
				_offsetY = _nextMinCell.Y;
				_hasField = false;
				_playerComponent = -1;
				WindowCentreChunk = _nextCentreChunk;
				HasWindow = true;
				_nextSource = null;
				_rebuildStage = 0;
				Recompute(playerWorld);
				_recomputeTimer = _recomputeInterval;
				return true;
			default:
				return false;
		}
	}

	/// <summary>Begin and finish a rebuild in one call. Stage start only.</summary>
	public void Rebuild(TerrainStreamer streamer, Vector2I minCell, Vector2I centreChunk, Vector2 playerWorld)
	{
		BeginRebuild(streamer, minCell, centreChunk);
		while (!StepRebuild(playerWorld)) { }
	}

	public void Update(Vector2 playerWorld, float delta)
	{
		if (!HasWindow) return;
		_recomputeTimer -= delta;
		if (_recomputeTimer > 0f) return;
		_recomputeTimer = _recomputeInterval;
		Recompute(playerWorld);
	}

	/// <summary>
	/// Unit direction that routes along the corridors toward the player, or zero where the caller
	/// should steer straight at the player instead (the player's own cell, off the grid, or a cell
	/// the field cannot reach). A position inside a wall cell gets the way back out.
	/// </summary>
	public Vector2 FlowDirectionAt(Vector2 world)
	{
		if (!_hasField || !WorldToGrid(world, out int gx, out int gy)) return Vector2.Zero;
		int i = gx + gy * _w;

		if (_solid[i])
			return EscapeWall(gx, gy);
		if (_dist[i] == int.MaxValue)
			return Vector2.Zero;

		// Lazy: only cells something is standing on are ever evaluated, once per recompute.
		if (_flowStamp[i] != _generation)
		{
			_flow[i] = ComputeFlow(gx, gy);
			_flowStamp[i] = _generation;
		}

		return _flow[i];
	}

	/// <summary>Walking distance to the player in tiles along the corridors, or -1 if it is off the grid,
	/// unreachable, or further than <see cref="SearchHorizonTiles"/> - callers treat all three as "too far".</summary>
	public float PathTilesAt(Vector2 world)
	{
		if (!_hasField || !WorldToGrid(world, out int gx, out int gy)) return -1f;
		int d = _dist[gx + gy * _w];
		return d == int.MaxValue ? -1f : d / (float)StraightCost;
	}

	/// <summary>
	/// True if something placed here could walk to the player, however far: false on a wall, outside
	/// the grid, or in a pocket the corridors do not connect to the player's. Before the first
	/// recompute the player's region is unknown, and everything open counts as reachable.
	/// </summary>
	public bool IsReachable(Vector2 world)
	{
		if (!HasWindow || !WorldToGrid(world, out int gx, out int gy)) return false;
		int i = gx + gy * _w;
		if (_solid[i]) return false;
		return _playerComponent < 0 || _component[i] == _playerComponent;
	}

	/// <summary>True if this world position sits in a wall cell. Anything outside the grid counts as
	/// open, the same way <see cref="FlowDirectionAt"/> treats it: off-grid is unbuilt ground, not rock.</summary>
	public bool IsSolidAt(Vector2 world)
	{
		if (!HasWindow || !WorldToGrid(world, out int gx, out int gy)) return false;
		return _solid[gx + gy * _w];
	}

	/// <summary>
	/// True if a straight line between two world points crosses no wall. Ranged attackers use this
	/// so they never fire a shot the maze would swallow - a bolt that hits the player through a
	/// wall reads as a bug, and one that stops in the wall wastes a telegraph the player dodged.
	/// </summary>
	/// <remarks>
	/// Sampled rather than traced: walls here are whole tiles, so a step of half a tile cannot skip
	/// one, and the caller runs this at most once per enemy per frame.
	/// </remarks>
	public bool HasLineOfSight(Vector2 from, Vector2 to)
	{
		Vector2 offset = to - from;
		float distance = offset.Length();
		if (distance <= 0.001f) return true;

		float step = Mathf.Max(4f, _tile * 0.5f);
		int samples = Mathf.CeilToInt(distance / step);
		Vector2 stride = offset / samples;
		for (int i = 1; i < samples; i++)
		{
			if (IsSolidAt(from + stride * i))
				return false;
		}

		return true;
	}

	// Static per window: how close each open cell is to a wall, as an entry surcharge, and which way
	// is "away from the wall" for the cells touching one. This is the row pass; BuildClearanceColumns
	// finishes it, as the next rebuild stage.
	private void BuildClearance()
	{
		// Separable, because this now runs every time the window moves rather than once per stage:
		// "a wall within 1" and "a wall within 2" (Chebyshev) are a row pass and a column pass of an OR,
		// six reads a cell instead of twenty-five. Outside the window counts as wall, as IsSolidNext says.
		for (int y = 0; y < _h; y++)
		{
			for (int x = 0; x < _w; x++)
			{
				bool r1 = IsSolidNext(x - 1, y) || IsSolidNext(x, y) || IsSolidNext(x + 1, y);
				_rowNear1[x + y * _w] = r1;
				_rowNear2[x + y * _w] = r1 || IsSolidNext(x - 2, y) || IsSolidNext(x + 2, y);
			}
		}
	}

	// The column pass of BuildClearance, over rows [y0, y1): two stages of their own, see StepRebuild.
	private void BuildClearanceColumns(int y0, int y1)
	{
		for (int y = y0; y < y1; y++)
		{
			for (int x = 0; x < _w; x++)
			{
				int i = x + y * _w;
				_wallPush[i] = Vector2.Zero;
				_penalty[i] = 0;
				if (_solid[i]) continue;

				bool touching = RowNear(_rowNear1, x, y - 1) || _rowNear1[i] || RowNear(_rowNear1, x, y + 1);
				if (!touching)
				{
					bool near = touching || RowNear(_rowNear2, x, y - 2) || RowNear(_rowNear2, x, y - 1)
						|| _rowNear2[i] || RowNear(_rowNear2, x, y + 1) || RowNear(_rowNear2, x, y + 2);
					_penalty[i] = near ? NearWallPenalty : 0;
					continue;
				}

				_penalty[i] = TouchingWallPenalty;
				Vector2 away = Vector2.Zero;
				for (int d = 0; d < 8; d++)
				{
					if (IsSolidNext(x + Dx[d], y + Dy[d]))
						away -= new Vector2(Dx[d], Dy[d]);
				}
				_wallPush[i] = away == Vector2.Zero ? Vector2.Zero : away.Normalized() * WallPushWeight;
			}
		}
	}

	// Rows outside the window are all wall, matching IsSolidNext.
	private bool RowNear(bool[] rows, int x, int y) => y < 0 || y >= _h || rows[x + y * _w];

	// Static per window: label every open region, four-connected the way a body actually moves.
	private void BuildComponents()
	{
		System.Array.Fill(_component, -1);
		Stack<int> stack = _componentStack;
		stack.Clear();
		int label = 0;
		for (int start = 0; start < _component.Length; start++)
		{
			if (_solid[start] || _component[start] >= 0) continue;
			_component[start] = label;
			stack.Push(start);
			while (stack.Count > 0)
			{
				int c = stack.Pop();
				int cx = c % _w;
				int cy = c / _w;
				for (int d = 0; d < 4; d++)
				{
					int nx = cx + Dx[d];
					int ny = cy + Dy[d];
					if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
					int ni = nx + ny * _w;
					if (_solid[ni] || _component[ni] >= 0) continue;
					_component[ni] = label;
					stack.Push(ni);
				}
			}
			label++;
		}
	}

	private bool IsSolidNext(int x, int y)
	{
		if (x < 0 || x >= _w || y < 0 || y >= _h) return true;
		return _solidNext[x + y * _w];
	}

	private void Recompute(Vector2 playerWorld)
	{
		if (!WorldToGrid(playerWorld, out int px, out int py))
			return;
		if (_solid[px + py * _w] && !FindNearestOpen(ref px, ref py))
			return;
		_playerComponent = _component[px + py * _w];

		System.Array.Fill(_dist, int.MaxValue);
		foreach (List<int> b in _buckets) b.Clear();

		int start = px + py * _w;
		_dist[start] = 0;
		_buckets[0].Add(start);
		int pending = 1;
		int cost = 0;

		// Dial's algorithm: every edge costs at most MaxEdgeCost, so a ring of that many buckets
		// holds the whole frontier and the queue never needs a heap.
		while (pending > 0)
		{
			List<int> bucket = _buckets[cost % _buckets.Length];
			for (int k = 0; k < bucket.Count; k++)
			{
				int c = bucket[k];
				pending--;
				if (_dist[c] != cost) continue; // superseded by a cheaper entry

				int cx = c % _w;
				int cy = c / _w;
				for (int d = 0; d < 8; d++)
				{
					int nx = cx + Dx[d];
					int ny = cy + Dy[d];
					if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
					int ni = nx + ny * _w;
					if (_solid[ni]) continue;
					// No cutting a wall corner on a diagonal: both straight cells must be open.
					if (d >= 4 && (_solid[nx + cy * _w] || _solid[cx + ny * _w])) continue;

					int nd = cost + (d < 4 ? StraightCost : DiagonalCost) + _penalty[ni];
					if (nd >= _dist[ni] || nd > MaxSearchCost) continue;
					_dist[ni] = nd;
					_buckets[nd % _buckets.Length].Add(ni);
					pending++;
				}
			}
			bucket.Clear();
			cost++;
		}

		_generation++;
		_hasField = true;
	}

	// The gradient: every cheaper neighbour pulls, in proportion to how much cheaper it is per unit
	// of distance. A corridor gives a straight line, an open hall a true diagonal rather than a
	// staircase of compass points, and a corner a curve.
	private Vector2 ComputeFlow(int x, int y)
	{
		int i = x + y * _w;
		int own = _dist[i];
		Vector2 sum = Vector2.Zero;
		for (int d = 0; d < 8; d++)
		{
			int nx = x + Dx[d];
			int ny = y + Dy[d];
			if (IsSolidCell(nx, ny)) continue;
			if (d >= 4 && (IsSolidCell(nx, y) || IsSolidCell(x, ny))) continue;
			int nd = _dist[nx + ny * _w];
			if (nd >= own) continue;

			float len = d < 4 ? 1f : 1.41421356f;
			float drop = (own - nd) / len;
			sum += new Vector2(Dx[d], Dy[d]) / len * drop;
		}

		if (sum == Vector2.Zero)
			return Vector2.Zero; // the player's own cell: steer straight at him

		return (sum.Normalized() + _wallPush[i]).Normalized();
	}

	// A body whose centre has been shoved inside a wall cell: head for the cheapest open neighbour.
	// Steering straight at the player from here was the other way to stay wedged for good.
	private Vector2 EscapeWall(int x, int y)
	{
		int best = int.MaxValue;
		Vector2 dir = Vector2.Zero;
		for (int d = 0; d < 8; d++)
		{
			int nx = x + Dx[d];
			int ny = y + Dy[d];
			if (IsSolidCell(nx, ny)) continue;
			int nd = _dist[nx + ny * _w];
			if (nd < best)
			{
				best = nd;
				dir = new Vector2(Dx[d], Dy[d]).Normalized();
			}
		}
		return dir;
	}

	private bool IsSolidCell(int x, int y)
	{
		if (x < 0 || x >= _w || y < 0 || y >= _h) return true;
		return _solid[x + y * _w];
	}

	private bool FindNearestOpen(ref int gx, ref int gy)
	{
		for (int r = 1; r <= 4; r++)
		{
			for (int dx = -r; dx <= r; dx++)
			{
				for (int dy = -r; dy <= r; dy++)
				{
					int nx = gx + dx;
					int ny = gy + dy;
					if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
					if (!_solid[nx + ny * _w])
					{
						gx = nx;
						gy = ny;
						return true;
					}
				}
			}
		}
		return false;
	}

	private bool WorldToGrid(Vector2 world, out int gx, out int gy)
	{
		int cx = Mathf.FloorToInt((world.X - _origin.X) / _tile);
		int cy = Mathf.FloorToInt((world.Y - _origin.Y) / _tile);
		gx = cx - _offsetX;
		gy = cy - _offsetY;
		return gx >= 0 && gx < _w && gy >= 0 && gy < _h;
	}
}
