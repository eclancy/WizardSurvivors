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
/// IT STOPS AT A HORIZON. Searching all 181x181 cells cost about 10 ms a time on a desktop debug
/// build, five times a second, which a phone cannot pay. Nothing past <see cref="SearchHorizonTiles"/>
/// of walking needs a route: Enemy recycles anything that far round (Enemy.MazeRecyclePathFactor),
/// and spawning rejects it. So the search stops there, and "is this a sealed pocket" - which a
/// horizon must not be mistaken for - is answered by connected-component labels worked out once
/// per maze instead.
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

	private readonly bool[] _solid;
	private readonly int[] _component;
	private int _playerComponent = -1;
	private readonly int[] _penalty;
	private readonly Vector2[] _wallPush;
	private readonly int _w;
	private readonly int _h;
	private readonly int _tile;
	private readonly int _offsetX;
	private readonly int _offsetY;
	private readonly Vector2 _origin;
	private readonly int[] _dist;
	private readonly Vector2[] _flow;
	private readonly int[] _flowStamp;
	private int _generation;
	private bool _hasField;
	private readonly List<int>[] _buckets;
	private readonly float _recomputeInterval;
	private float _recomputeTimer;

	// Eight neighbours: four straight, then four diagonal.
	private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
	private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

	public int TileSize => _tile;

	public MazeNavigation(string[,] grid, string wallTerrain, int tile, Vector2I cellOffset,
		Vector2 origin, float recomputeInterval = 0.2f)
	{
		_w = grid.GetLength(0);
		_h = grid.GetLength(1);
		int n = _w * _h;
		_solid = new bool[n];
		for (int x = 0; x < _w; x++)
			for (int y = 0; y < _h; y++)
				_solid[x + y * _w] = grid[x, y] == wallTerrain;

		_tile = tile;
		_offsetX = cellOffset.X;
		_offsetY = cellOffset.Y;
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
		_recomputeInterval = recomputeInterval;

		BuildClearance();
		BuildComponents();
	}

	public void Update(Vector2 playerWorld, float delta)
	{
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
		if (!WorldToGrid(world, out int gx, out int gy)) return false;
		int i = gx + gy * _w;
		if (_solid[i]) return false;
		return _playerComponent < 0 || _component[i] == _playerComponent;
	}

	/// <summary>True if this world position sits in a wall cell. Anything outside the grid counts as
	/// open, the same way <see cref="FlowDirectionAt"/> treats it: off-grid is unbuilt ground, not rock.</summary>
	public bool IsSolidAt(Vector2 world)
	{
		if (!WorldToGrid(world, out int gx, out int gy)) return false;
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

	// Static per maze: how close each open cell is to a wall, as an entry surcharge, and which way
	// is "away from the wall" for the cells touching one.
	private void BuildClearance()
	{
		for (int x = 0; x < _w; x++)
		{
			for (int y = 0; y < _h; y++)
			{
				int i = x + y * _w;
				if (_solid[i]) continue;

				int nearest = 3;
				Vector2 away = Vector2.Zero;
				for (int dx = -2; dx <= 2; dx++)
				{
					for (int dy = -2; dy <= 2; dy++)
					{
						if (dx == 0 && dy == 0) continue;
						if (!IsSolidCell(x + dx, y + dy)) continue;
						int cheb = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
						if (cheb < nearest) nearest = cheb;
						if (cheb == 1) away -= new Vector2(dx, dy);
					}
				}

				_penalty[i] = nearest == 1 ? TouchingWallPenalty : nearest == 2 ? NearWallPenalty : 0;
				_wallPush[i] = away == Vector2.Zero ? Vector2.Zero : away.Normalized() * WallPushWeight;
			}
		}
	}

	// Static per maze: label every open region, four-connected the way a body actually moves.
	private void BuildComponents()
	{
		System.Array.Fill(_component, -1);
		var stack = new Stack<int>();
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
