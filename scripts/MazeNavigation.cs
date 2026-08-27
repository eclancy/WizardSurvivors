using Godot;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

/// <summary>
/// Shared wall-aware navigation for a maze stage. Computes a BFS flow field from the
/// player's cell over open (non-wall) cells; every enemy cheaply queries a movement
/// direction that routes around walls toward the player. Recomputed a few times per
/// second, so hundreds of enemies share one O(cells) computation instead of each
/// running its own path search.
///
/// <see cref="Active"/> is the current maze's navigation (or null on non-maze stages).
/// </summary>
public sealed class MazeNavigation
{
	public static MazeNavigation Active { get; set; }

	private readonly bool[,] _solid;
	private readonly int _w;
	private readonly int _h;
	private readonly int _tile;
	private readonly int _offsetX;
	private readonly int _offsetY;
	private readonly Vector2 _origin;
	private readonly Vector2[,] _flow;
	private readonly int[,] _dist;
	private readonly Queue<(int, int)> _queue = new();
	private readonly float _recomputeInterval;
	private float _recomputeTimer;

	private static readonly int[] Dx = { 1, -1, 0, 0 };
	private static readonly int[] Dy = { 0, 0, 1, -1 };

	public MazeNavigation(string[,] grid, string wallTerrain, int tile, Vector2I cellOffset,
		Vector2 origin, float recomputeInterval = 0.2f)
	{
		_w = grid.GetLength(0);
		_h = grid.GetLength(1);
		_solid = new bool[_w, _h];
		for (int x = 0; x < _w; x++)
			for (int y = 0; y < _h; y++)
				_solid[x, y] = grid[x, y] == wallTerrain;

		_tile = tile;
		_offsetX = cellOffset.X;
		_offsetY = cellOffset.Y;
		_origin = origin;
		_flow = new Vector2[_w, _h];
		_dist = new int[_w, _h];
		_recomputeInterval = recomputeInterval;
	}

	public void Update(Vector2 playerWorld, float delta)
	{
		_recomputeTimer -= delta;
		if (_recomputeTimer > 0f) return;
		_recomputeTimer = _recomputeInterval;
		Recompute(playerWorld);
	}

	/// <summary>Unit direction that routes around walls toward the player, or zero if none.</summary>
	public Vector2 FlowDirectionAt(Vector2 world)
	{
		if (!WorldToGrid(world, out int gx, out int gy)) return Vector2.Zero;
		return _flow[gx, gy];
	}

	private void Recompute(Vector2 playerWorld)
	{
		if (!WorldToGrid(playerWorld, out int px, out int py))
			return;
		if (_solid[px, py] && !FindNearestOpen(ref px, ref py))
			return;

		for (int x = 0; x < _w; x++)
			for (int y = 0; y < _h; y++)
				_dist[x, y] = int.MaxValue;

		_queue.Clear();
		_dist[px, py] = 0;
		_queue.Enqueue((px, py));
		while (_queue.Count > 0)
		{
			var (cx, cy) = _queue.Dequeue();
			int nd = _dist[cx, cy] + 1;
			for (int d = 0; d < 4; d++)
			{
				int nx = cx + Dx[d];
				int ny = cy + Dy[d];
				if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
				if (_solid[nx, ny] || _dist[nx, ny] <= nd) continue;
				_dist[nx, ny] = nd;
				_queue.Enqueue((nx, ny));
			}
		}

		// Flow points from each open cell to its lowest-distance neighbour (toward the player).
		for (int x = 0; x < _w; x++)
		{
			for (int y = 0; y < _h; y++)
			{
				if (_solid[x, y] || _dist[x, y] == int.MaxValue)
				{
					_flow[x, y] = Vector2.Zero;
					continue;
				}

				int best = _dist[x, y];
				int bestX = x, bestY = y;
				for (int d = 0; d < 4; d++)
				{
					int nx = x + Dx[d];
					int ny = y + Dy[d];
					if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
					if (_solid[nx, ny]) continue;
					if (_dist[nx, ny] < best)
					{
						best = _dist[nx, ny];
						bestX = nx;
						bestY = ny;
					}
				}

				_flow[x, y] = (bestX == x && bestY == y)
					? Vector2.Zero
					: new Vector2(bestX - x, bestY - y).Normalized();
			}
		}
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
					if (!_solid[nx, ny])
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
