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

	/// <summary>True if this world position sits in a wall cell. Anything outside the grid counts as
	/// open, the same way <see cref="FlowDirectionAt"/> treats it: off-grid is unbuilt ground, not rock.</summary>
	public bool IsSolidAt(Vector2 world)
	{
		if (!WorldToGrid(world, out int gx, out int gy)) return false;
		return _solid[gx, gy];
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
