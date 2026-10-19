using Godot;
using System.Collections.Generic;
using System.Diagnostics;

namespace WizardSurvivors.scripts;

/// <summary>
/// The one shared, terrain-aware navigation for enemies, on every procedural stage. Computes one
/// distance field from the player's cell over the walkable cells (not maze wall, not water), and
/// every enemy cheaply reads a movement direction from it that routes round whatever is in the way.
/// The whole horde shares one O(cells) computation instead of each running its own path search, so
/// the cost does not grow with the enemy count.
///
/// <see cref="Active"/> is the current stage's navigation (null only on the placeholder room).
/// Named for the stage it was first built for; it has served open stages too since lakes started
/// wedging the swarm (.ai/terrain.md, section 9).
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
/// corner) with a surcharge for entering a cell that touches a wall, so the cheapest route runs
/// down the middle of a corridor, swings wide round a corner and keeps a body's width off a
/// shoreline. The flow is a gradient over every cheaper neighbour rather than one of eight compass
/// points, so a turn is a curve. Costs are small integers, so the queue is a bucket queue and the
/// whole recompute is linear.
///
/// IT COVERS A WINDOW, NOT A MAP. Every stage is endless (TerrainStreamer), so the grid here is a
/// fixed-size window over the painted chunks round the player, moved and refilled by
/// <see cref="BeginRebuild"/> when he has walked well into another chunk.
///
/// IT STOPS AT A HORIZON. Searching all 181x181 cells of the old fixed maze cost about 10 ms a time
/// on a desktop debug build, five times a second, which a phone cannot pay. Nothing past <see cref="SearchHorizonTiles"/>
/// of walking needs a route: Enemy recycles anything that far round (Enemy.MazeRecyclePathFactor),
/// and spawning rejects it. So the search stops there, and "is this a sealed pocket" - which a
/// horizon must not be mistaken for - is answered by connected-component labels worked out once
/// per window instead.
///
/// IT IS BUILT A SLICE A FRAME. An open stage has far more walkable cells inside the horizon than a
/// maze does, and a whole recompute in one frame is a spike every few tenths of a second. So a field
/// is built into a second distance table, <see cref="FieldCellsPerFrame"/> cells a frame, and swapped
/// in whole when it is finished - readers never see a half-built field. It is not rebuilt at all
/// while the player stays in the cell the live one was built from: the terrain is static, so that
/// field is still exact.
/// </remarks>
public sealed class MazeNavigation
{
	public static MazeNavigation Active { get; set; }

	/// <summary>
	/// Off only for an A/B measurement (_MazeProbe and _UiShot --no-open-routing): enemies on an open
	/// stage then chase in a straight line, and spawning and recycling ignore walking distance there,
	/// exactly as before open stages had routing. The field is still built, so the probe can measure.
	/// </summary>
	public static bool OpenStageRouting { get; set; } = true;

	/// <summary>
	/// Off only for a cost A/B (_BalanceHarness --no-open-nav): an open stage then gets no window at
	/// all, so it runs exactly the code it ran before open stages had navigation.
	/// </summary>
	public static bool BuildOnOpenStages { get; set; } = true;

	/// <summary>
	/// The navigation that should steer enemies and judge spawn points and recycling: <see cref="Active"/>
	/// on a maze, and on an open stage unless <see cref="OpenStageRouting"/> is off.
	/// </summary>
	public static MazeNavigation Routing => Active != null && (Active.IsMaze || OpenStageRouting) ? Active : null;

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

	/// <summary>
	/// Cells settled per frame while a field is being built. An open window holds about 13,000 cells
	/// inside the horizon, so a field takes six or seven frames there and fewer in a maze - well
	/// inside the time between recomputes. 4096 was tried first and its slices reached 2.7 ms on a
	/// loaded desktop debug build; measured in .ai/terrain.md.
	/// </summary>
	public const int FieldCellsPerFrame = 2048;

	/// <summary>Seconds between field rebuilds while the player moves, in a maze.</summary>
	public const float MazeRecomputeInterval = 0.2f;
	/// <summary>
	/// The same on an open stage, where the field only steers enemies whose straight walk is blocked:
	/// a field two tiles stale moves the point they head round a lake toward by two tiles, which
	/// nobody can see, and costs two-fifths of the rebuilds a maze pays while walking.
	/// </summary>
	public const float OpenRecomputeInterval = 0.5f;

	/// <summary>
	/// How many frames' worth of sliced work a frame of <paramref name="delta"/> game seconds should do:
	/// one at 60 fps, more when frames are long. The budgets are sized for a 60 fps frame, and a fixed
	/// number of slices per FRAME let a slow frame rate stretch a field and a window move over seconds
	/// of game time. Measured: _MazeProbe at timescale 4 on a loaded desktop (a few frames a second)
	/// fell from 15% of the horde close to the player to 10%, and from 100% routable to 96%, until the
	/// work followed the delta. A long frame is already a hitch; finishing the work in it costs less
	/// than steering the swarm by where the player was.
	/// </summary>
	public static int FramesOfWork(float delta) => System.Math.Clamp(Mathf.RoundToInt(delta * 60f), 1, MaxCatchUpFrames);
	private const int MaxCatchUpFrames = 24;

	// Window rebuild stages are bounded too (StepRebuild): the row pass in bands, the column pass in
	// bands, and the component fill a slice at a time.
	private const int RowBands = 2;
	private const int ColumnBands = 4;
	private const int ComponentCellsPerStep = 8192;

	// How hard a body standing in a wall-touching cell is pushed back toward open floor, relative to
	// the unit flow. Belt and braces for the cases the weighting cannot see: an enemy shoved there by
	// the crowd, or the final approach to a player who is himself standing against the wall.
	private const float WallPushWeight = 0.45f;

	// The live tables every query reads, and a staging copy the next window is built into over
	// several frames (BeginRebuild / StepRebuild) and then swapped in whole. "Solid" is anything a
	// body cannot walk into - wall or water; "wall" is the subset that also stops a shot.
	private bool[] _solid;
	private bool[] _wall;
	private int[] _component;
	private int[] _penalty;
	private Vector2[] _wallPush;
	private int _solidCount;
	private int _wallCount;
	private bool[] _solidNext;
	private bool[] _wallNext;
	private int[] _componentNext;
	private int[] _penaltyNext;
	private Vector2[] _wallPushNext;
	private int _solidCountNext;
	private int _wallCountNext;
	private int _playerComponent = -1;
	private readonly int _w;
	private readonly int _h;
	private readonly int _tile;
	// World cell of the window's [0, 0]. Moved by StepRebuild.
	private int _offsetX;
	private int _offsetY;
	private readonly Vector2 _origin;

	// The live distance field, and the one being built a slice at a time (StepField).
	private int[] _dist;
	private int[] _distNext;
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

	// The field build in progress. The Dial's queue is resumable because no edge costs less than
	// StraightCost: nothing is ever added to the bucket being drained, so stopping part way through
	// one and carrying on next frame from the same index sees exactly what one pass would have.
	private bool _building;
	private int _buildCost;
	private int _buildPending;
	private int _buildBucketIndex;
	private int _buildPlayerComponent;
	private int _buildFromCell;
	private long _buildTicks;
	// What the build reads: the live window's tables, or - during a window move - the staging tables
	// of the window about to be swapped in, so the new window arrives with its field already built.
	private bool[] _buildSolid;
	private int[] _buildPenalty;
	private bool _buildForNext;
	private bool _nextFieldReady;
	private int _nextPlayerComponent;
	private int _nextBuiltFromCell;
	// Window cell of the player when the live field was started; -1 forces the next recompute.
	private int _builtFromCell = -1;
	// A window stage ran this frame (StepRebuild): the field slice waits, so the two never share one.
	private bool _windowStagedThisFrame;

	// Eight neighbours: four straight, then four diagonal.
	private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
	private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

	/// <summary>True on a maze stage. A maze steers every enemy along the field all the time; an open
	/// stage only steers the ones whose straight walk to the player is blocked (Enemy).</summary>
	public bool IsMaze { get; }

	public int TileSize => _tile;

	/// <summary>The slice size this window builds its field in; <see cref="FieldCellsPerFrame"/> except
	/// in RegressionChecks, which makes it tiny to prove a sliced field equals a whole one.</summary>
	public int CellsPerFrame { get; set; } = FieldCellsPerFrame;

	/// <summary>
	/// A window of <paramref name="windowTiles"/> square, empty until a rebuild fills it.
	/// The arrays are sized once here and reused by every rebuild, so walking does not allocate.
	/// </summary>
	public MazeNavigation(int windowTiles, int tile, Vector2 origin, bool isMaze)
	{
		_w = windowTiles;
		_h = windowTiles;
		int n = _w * _h;
		IsMaze = isMaze;
		_tile = tile;
		_origin = origin;
		_solid = new bool[n];
		_wall = new bool[n];
		_component = new int[n];
		_penalty = new int[n];
		_wallPush = new Vector2[n];
		_solidNext = new bool[n];
		_wallNext = new bool[n];
		_componentNext = new int[n];
		_penaltyNext = new int[n];
		_wallPushNext = new Vector2[n];
		_dist = new int[n];
		_distNext = new int[n];
		System.Array.Fill(_dist, int.MaxValue);
		_flow = new Vector2[n];
		_flowStamp = new int[n];
		_buckets = new List<int>[MaxEdgeCost + 1];
		for (int i = 0; i < _buckets.Length; i++)
			_buckets[i] = new List<int>(256);
		_componentStack = new Stack<int>(1024);
		_rowNear1 = new bool[n];
		_rowNear2 = new bool[n];
		_recomputeInterval = isMaze ? MazeRecomputeInterval : OpenRecomputeInterval;
	}

	/// <summary>The streamer chunk the window was last centred on; see <see cref="BeginRebuild"/>.</summary>
	public Vector2I WindowCentreChunk { get; private set; }
	public bool HasWindow { get; private set; }

	/// <summary>True while a field is being built a slice at a time; the previous one is still live.</summary>
	public bool IsBuildingField => _building;

	/// <summary>True while a window is being built in the background; see <see cref="StepRebuild"/>.</summary>
	public bool IsRebuilding => _rebuildStage > 0;

	private int _rebuildStage;
	// Progress inside a stage: which band of rows, and where the resumable component fill has got to.
	private int _rebuildBand;
	private int _componentScan;
	private int _componentLabel;
	private Vector2I _nextMinCell;
	private Vector2I _nextCentreChunk;
	private TerrainStreamer _nextSource;
	// RegressionChecks builds a window from a hand-made grid instead of the streamer.
	private bool[] _nextTestSolid;
	private bool[] _nextTestWall;

	/// <summary>
	/// Start moving the window so it starts at world cell <paramref name="minCell"/>. The new tables
	/// are built into a staging copy by <see cref="StepRebuild"/>, one bounded step per call, and
	/// swapped in whole at the end - the live window keeps answering every query until then.
	/// </summary>
	/// <remarks>
	/// THE STAGE HAS NO EDGE NOW, SO THE GRID MOVES (.ai/terrain.md, "Chunk streaming"). Node2DGame starts
	/// this when the player has gone well past the chunk the window is centred on and the streamer
	/// has finished loading round him; the window is the painted 5 x 5 chunks. Components are labelled
	/// within the window, so a corridor that only connects to the player's outside it reads as a
	/// separate pocket and is not spawned into - the cautious mistake.
	///
	/// STAGED, because the whole rebuild was 9-12 ms in one frame on a desktop debug build - a
	/// dropped frame or two on a phone, every chunk of travel. It was then six stages of up to 3.6 ms,
	/// the last of them the swap plus a whole field. Now that open stages move a window too, every
	/// step is bounded - row bands, column bands, a resumable component fill, the new window's field
	/// in slices - and the swap is only an exchange of references. About twenty frames a move.
	/// </remarks>
	public void BeginRebuild(TerrainStreamer streamer, Vector2I minCell, Vector2I centreChunk)
	{
		_nextSource = streamer;
		_nextMinCell = minCell;
		_nextCentreChunk = centreChunk;
		_rebuildStage = 1;
		_rebuildBand = 0;
	}

	/// <summary>
	/// One step of a rebuild started by <see cref="BeginRebuild"/>. Returns true on the call that
	/// swaps the new window in, with its field already built round the player, so there is never a
	/// frame where the window has no field and every position counts as reachable.
	/// </summary>
	/// <remarks>
	/// Every step before the swap writes ONLY the staging tables (the *Next arrays). They once wrote
	/// the live penalty, push and component tables from the live solid grid, and the swap then put
	/// the stale copies live: for as long as that lasted, the maze had no wall surcharge, no push off
	/// the walls and no pocket labels at all.
	/// </remarks>
	public bool StepRebuild(Vector2 playerWorld)
	{
		_windowStagedThisFrame = true;
		switch (_rebuildStage)
		{
			case 1:
				if (_nextSource != null)
					_nextSource.FillNavigation(_solidNext, _wallNext, _nextMinCell, _w, _h);
				else
				{
					System.Array.Copy(_nextTestSolid, _solidNext, _solidNext.Length);
					System.Array.Copy(_nextTestWall, _wallNext, _wallNext.Length);
				}
				_solidCountNext = 0;
				_wallCountNext = 0;
				_rebuildBand = 0;
				_rebuildStage = 2;
				return false;
			case 2:
				BuildClearance(BandStart(_rebuildBand, RowBands), BandStart(_rebuildBand + 1, RowBands));
				if (++_rebuildBand == RowBands)
				{
					_rebuildBand = 0;
					_rebuildStage = 3;
				}
				return false;
			case 3:
				BuildClearanceColumns(BandStart(_rebuildBand, ColumnBands), BandStart(_rebuildBand + 1, ColumnBands));
				if (++_rebuildBand == ColumnBands)
				{
					BeginComponents();
					_rebuildStage = 4;
				}
				return false;
			case 4:
				if (StepComponents(ComponentCellsPerStep))
				{
					// From here the build belongs to the new window: a live one part way through is
					// dropped (the finished live field keeps answering), and Update starts no other
					// until the swap.
					_building = false;
					_nextFieldReady = false;
					_rebuildStage = BeginField(playerWorld, forNext: true) ? 5 : 6;
				}
				return false;
			case 5:
				if (StepField(CellsPerFrame))
					_rebuildStage = 6;
				return false;
			case 6:
				(_solid, _solidNext) = (_solidNext, _solid);
				(_wall, _wallNext) = (_wallNext, _wall);
				(_component, _componentNext) = (_componentNext, _component);
				(_penalty, _penaltyNext) = (_penaltyNext, _penalty);
				(_wallPush, _wallPushNext) = (_wallPushNext, _wallPush);
				_solidCount = _solidCountNext;
				_wallCount = _wallCountNext;
				_offsetX = _nextMinCell.X;
				_offsetY = _nextMinCell.Y;
				WindowCentreChunk = _nextCentreChunk;
				HasWindow = true;
				_nextSource = null;
				_nextTestSolid = null;
				_nextTestWall = null;
				_rebuildStage = 0;
				_recomputeTimer = _recomputeInterval;
				if (_nextFieldReady)
				{
					(_dist, _distNext) = (_distNext, _dist);
					_playerComponent = _nextPlayerComponent;
					_builtFromCell = _nextBuiltFromCell;
					_generation++;
					_hasField = true;
				}
				else
				{
					// The player was off the new window or walled in when its field was due: one more
					// try now, whole, so the window never goes live with the old window's distances.
					_hasField = false;
					_playerComponent = -1;
					_builtFromCell = -1;
					RecomputeNow(playerWorld);
				}
				return true;
			default:
				return false;
		}
	}

	// First row of band `band` of `bands`; band == bands gives the window's height.
	private int BandStart(int band, int bands) => band * _h / bands;

	/// <summary>Begin and finish a rebuild in one call. Stage start only.</summary>
	public void Rebuild(TerrainStreamer streamer, Vector2I minCell, Vector2I centreChunk, Vector2 playerWorld)
	{
		BeginRebuild(streamer, minCell, centreChunk);
		while (!StepRebuild(playerWorld)) { }
	}

	/// <summary>
	/// A whole window from a hand-made grid (indexed x + y * width), with its first cell at world cell
	/// <paramref name="minCell"/>. RegressionChecks only: it checks the routing on a known lake.
	/// </summary>
	public void RebuildFromGrid(bool[] solid, bool[] wall, Vector2I minCell, Vector2 playerWorld)
	{
		_nextSource = null;
		_nextTestSolid = solid;
		_nextTestWall = wall;
		_nextMinCell = minCell;
		_nextCentreChunk = Vector2I.Zero;
		_rebuildStage = 1;
		_rebuildBand = 0;
		while (!StepRebuild(playerWorld)) { }
	}

	/// <summary>
	/// Per frame. Carries on a field build if one is under way, otherwise starts one every
	/// recompute interval - unless the player is still in the cell the live field was built from.
	/// </summary>
	public void Update(Vector2 playerWorld, float delta)
	{
		if (!HasWindow) return;
		bool staged = _windowStagedThisFrame;
		_windowStagedThisFrame = false;
		// A window move owns the field build until it swaps (StepRebuild builds the new window's).
		if (_rebuildStage > 0) return;

		int budget = CellsPerFrame * FramesOfWork(delta);
		if (_building)
		{
			if (!staged)
				StepField(budget);
			return;
		}

		_recomputeTimer -= delta;
		if (_recomputeTimer > 0f) return;
		_recomputeTimer = _recomputeInterval;

		// The terrain does not move, so a field built from the player's own cell is still exact.
		// A player holding his ground costs nothing at all.
		if (_hasField && WorldToGrid(playerWorld, out int gx, out int gy) && gx + gy * _w == _builtFromCell)
			return;
		if (BeginField(playerWorld, forNext: false) && !staged)
			StepField(budget);
	}

	/// <summary>Build the whole field round the player now, in one go: stage start, a window that went
	/// live without one, and _MazeProbe's cost measurement.</summary>
	public void RecomputeNow(Vector2 playerWorld)
	{
		if (BeginField(playerWorld, forNext: false))
			StepField(int.MaxValue);
	}

	/// <summary>
	/// Unit direction that routes round walls and water toward the player, or zero where the caller
	/// should steer straight at the player instead (the player's own cell, off the grid, or a cell
	/// the field cannot reach). A position inside a blocked cell gets the way back out.
	/// </summary>
	public Vector2 FlowDirectionAt(Vector2 world)
	{
		if (!_hasField || !WorldToGrid(world, out int gx, out int gy)) return Vector2.Zero;
		int i = gx + gy * _w;

		if (_solid[i])
			return EscapeWall(gx, gy);
		if (_dist[i] == int.MaxValue)
			return Vector2.Zero;

		// Lazy: only cells something is standing on are ever evaluated, once per field.
		if (_flowStamp[i] != _generation)
		{
			_flow[i] = ComputeFlow(gx, gy);
			_flowStamp[i] = _generation;
		}

		return _flow[i];
	}

	/// <summary>Walking distance to the player in tiles round walls and water, or -1 if it is off the grid,
	/// unreachable, or further than <see cref="SearchHorizonTiles"/> - callers treat all three as "too far".</summary>
	public float PathTilesAt(Vector2 world)
	{
		if (!_hasField || !WorldToGrid(world, out int gx, out int gy)) return -1f;
		int d = _dist[gx + gy * _w];
		return d == int.MaxValue ? -1f : d / (float)StraightCost;
	}

	/// <summary>
	/// True if something placed here could walk to the player, however far: false on a wall or in
	/// water, outside the grid, or in a pocket not connected to the player's. Before the first
	/// recompute the player's region is unknown, and everything open counts as reachable.
	/// </summary>
	public bool IsReachable(Vector2 world)
	{
		if (!HasWindow || !WorldToGrid(world, out int gx, out int gy)) return false;
		int i = gx + gy * _w;
		if (_solid[i]) return false;
		return _playerComponent < 0 || _component[i] == _playerComponent;
	}

	/// <summary>True if a body cannot walk into this world position (wall or water). Anything outside
	/// the grid counts as open, the same way <see cref="FlowDirectionAt"/> treats it.</summary>
	public bool IsBlockedAt(Vector2 world)
	{
		if (!HasWindow || !WorldToGrid(world, out int gx, out int gy)) return false;
		return _solid[gx + gy * _w];
	}

	/// <summary>True if this world position is a maze wall - which, unlike water, also stops a shot.
	/// One integer test on a window with no walls, which is every open stage.</summary>
	public bool IsWallAt(Vector2 world)
	{
		if (_wallCount == 0 || !HasWindow || !WorldToGrid(world, out int gx, out int gy)) return false;
		return _wall[gx + gy * _w];
	}

	/// <summary>
	/// True if a straight line between two world points crosses no wall. Ranged attackers use this
	/// so they never fire a shot the maze would swallow - a bolt that hits the player through a
	/// wall reads as a bug, and one that stops in the wall wastes a telegraph the player dodged.
	/// Water does not block it: a bolt flies over a lake.
	/// </summary>
	/// <remarks>
	/// Sampled rather than traced: walls here are whole tiles, so a step of half a tile cannot skip
	/// one, and the caller runs this at most once per enemy per frame.
	/// </remarks>
	public bool HasLineOfSight(Vector2 from, Vector2 to)
	{
		if (_wallCount == 0) return true;
		Vector2 offset = to - from;
		float distance = offset.Length();
		if (distance <= 0.001f) return true;

		float step = Mathf.Max(4f, _tile * 0.5f);
		int samples = Mathf.CeilToInt(distance / step);
		Vector2 stride = offset / samples;
		for (int i = 1; i < samples; i++)
		{
			if (IsWallAt(from + stride * i))
				return false;
		}

		return true;
	}

	/// <summary>
	/// True if the straight segment between two world points crosses no blocked cell (wall or water),
	/// not counting the cells at either end - a body already overlapping a shoreline cell, or a player
	/// standing on one, must not make every line to him read as blocked.
	/// </summary>
	/// <remarks>
	/// An exact grid walk (Amanatides and Woo): one step per cell boundary crossed, no sampling, and
	/// it stops at the first blocked cell. About 30 cells for an enemy at the spawn ring, and nothing
	/// at all in a window with no water or walls. Enemy runs it once per re-aim, not every frame.
	/// </remarks>
	public bool HasClearWalk(Vector2 from, Vector2 to)
	{
		if (_solidCount == 0 || !HasWindow) return true;

		float fx = (from.X - _origin.X) / _tile - _offsetX;
		float fy = (from.Y - _origin.Y) / _tile - _offsetY;
		float tx = (to.X - _origin.X) / _tile - _offsetX;
		float ty = (to.Y - _origin.Y) / _tile - _offsetY;
		int x = Mathf.FloorToInt(fx);
		int y = Mathf.FloorToInt(fy);
		int ex = Mathf.FloorToInt(tx);
		int ey = Mathf.FloorToInt(ty);
		int steps = System.Math.Abs(ex - x) + System.Math.Abs(ey - y);
		if (steps <= 1) return true;

		float dx = tx - fx;
		float dy = ty - fy;
		int stepX = dx > 0f ? 1 : -1;
		int stepY = dy > 0f ? 1 : -1;
		// t runs 0..1 along the segment; tMax is where it next crosses a cell boundary on each axis.
		float tDeltaX = dx != 0f ? 1f / System.Math.Abs(dx) : float.MaxValue;
		float tDeltaY = dy != 0f ? 1f / System.Math.Abs(dy) : float.MaxValue;
		float tMaxX = dx > 0f ? (x + 1 - fx) * tDeltaX : dx < 0f ? (fx - x) * tDeltaX : float.MaxValue;
		float tMaxY = dy > 0f ? (y + 1 - fy) * tDeltaY : dy < 0f ? (fy - y) * tDeltaY : float.MaxValue;

		for (int s = 0; s < steps; s++)
		{
			if (tMaxX < tMaxY) { x += stepX; tMaxX += tDeltaX; }
			else { y += stepY; tMaxY += tDeltaY; }
			if (x == ex && y == ey) return true;
			if (x >= 0 && x < _w && y >= 0 && y < _h && _solid[x + y * _w]) return false;
		}

		return true;
	}

	// Static per window: how close each open cell is to a wall, as an entry surcharge, and which way
	// is "away from the wall" for the cells touching one. This is the row pass over rows [y0, y1);
	// BuildClearanceColumns finishes it. It also counts what the window holds, so a window with no
	// water and no walls answers every line test at once.
	private void BuildClearance(int y0, int y1)
	{
		// Separable, because this runs every time the window moves rather than once per stage:
		// "a wall within 1" and "a wall within 2" (Chebyshev) are a row pass and a column pass of an OR,
		// six reads a cell instead of twenty-five. Outside the window counts as wall, as IsSolidNext says.
		int solidCount = 0;
		int wallCount = 0;
		for (int y = y0; y < y1; y++)
		{
			for (int x = 0; x < _w; x++)
			{
				int i = x + y * _w;
				if (_solidNext[i]) solidCount++;
				if (_wallNext[i]) wallCount++;
				bool r1 = IsSolidNext(x - 1, y) || _solidNext[i] || IsSolidNext(x + 1, y);
				_rowNear1[i] = r1;
				_rowNear2[i] = r1 || IsSolidNext(x - 2, y) || IsSolidNext(x + 2, y);
			}
		}
		_solidCountNext += solidCount;
		_wallCountNext += wallCount;
	}

	// The column pass of BuildClearance, over rows [y0, y1). It reads the row pass two rows either
	// side, which is why every row band is finished before the first column band (StepRebuild).
	private void BuildClearanceColumns(int y0, int y1)
	{
		for (int y = y0; y < y1; y++)
		{
			for (int x = 0; x < _w; x++)
			{
				int i = x + y * _w;
				_wallPushNext[i] = Vector2.Zero;
				_penaltyNext[i] = 0;
				if (_solidNext[i]) continue;

				bool touching = RowNear(_rowNear1, x, y - 1) || _rowNear1[i] || RowNear(_rowNear1, x, y + 1);
				if (!touching)
				{
					bool near = RowNear(_rowNear2, x, y - 2) || RowNear(_rowNear2, x, y - 1)
						|| _rowNear2[i] || RowNear(_rowNear2, x, y + 1) || RowNear(_rowNear2, x, y + 2);
					_penaltyNext[i] = near ? NearWallPenalty : 0;
					continue;
				}

				_penaltyNext[i] = TouchingWallPenalty;
				Vector2 away = Vector2.Zero;
				for (int d = 0; d < 8; d++)
				{
					if (IsSolidNext(x + Dx[d], y + Dy[d]))
						away -= new Vector2(Dx[d], Dy[d]);
				}
				_wallPushNext[i] = away == Vector2.Zero ? Vector2.Zero : away.Normalized() * WallPushWeight;
			}
		}
	}

	// Rows outside the window are all wall, matching IsSolidNext.
	private bool RowNear(bool[] rows, int x, int y) => y < 0 || y >= _h || rows[x + y * _w];

	// Static per window: label every open region, four-connected the way a body actually moves.
	// A flood fill that can stop after any cell and carry on next frame: the scan position, the
	// current label and the stack all live on the instance.
	private void BeginComponents()
	{
		System.Array.Fill(_componentNext, -1);
		_componentStack.Clear();
		_componentScan = 0;
		_componentLabel = -1;
	}

	// Label up to `budget` cells; true once every open cell has a label.
	private bool StepComponents(int budget)
	{
		Stack<int> stack = _componentStack;
		int labelled = 0;
		while (true)
		{
			while (stack.Count > 0)
			{
				if (labelled >= budget) return false;
				int c = stack.Pop();
				int cx = c % _w;
				int cy = c / _w;
				for (int d = 0; d < 4; d++)
				{
					int nx = cx + Dx[d];
					int ny = cy + Dy[d];
					if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
					int ni = nx + ny * _w;
					if (_solidNext[ni] || _componentNext[ni] >= 0) continue;
					_componentNext[ni] = _componentLabel;
					stack.Push(ni);
					labelled++;
				}
			}

			// That region is finished; the next unlabelled open cell starts another.
			while (_componentScan < _componentNext.Length
				&& (_solidNext[_componentScan] || _componentNext[_componentScan] >= 0))
				_componentScan++;
			if (_componentScan >= _componentNext.Length)
				return true;
			_componentLabel++;
			_componentNext[_componentScan] = _componentLabel;
			stack.Push(_componentScan);
			labelled++;
		}
	}

	private bool IsSolidNext(int x, int y)
	{
		if (x < 0 || x >= _w || y < 0 || y >= _h) return true;
		return _solidNext[x + y * _w];
	}

	// Seed a new field at the player's cell (or the nearest open one if he is standing on a shoreline
	// cell): on the live window, or with forNext on the staging window a move is about to swap in.
	// False if he is off that window or walled in, and the live field is left as it was.
	private bool BeginField(Vector2 playerWorld, bool forNext)
	{
		int offX = forNext ? _nextMinCell.X : _offsetX;
		int offY = forNext ? _nextMinCell.Y : _offsetY;
		bool[] solid = forNext ? _solidNext : _solid;
		if (!WorldToGrid(playerWorld, offX, offY, out int px, out int py))
			return false;
		int raw = px + py * _w;
		if (solid[raw] && !FindNearestOpen(solid, ref px, ref py))
			return false;

		int start = px + py * _w;
		System.Array.Fill(_distNext, int.MaxValue);
		foreach (List<int> b in _buckets) b.Clear();
		_distNext[start] = 0;
		_buckets[0].Add(start);
		_buildPending = 1;
		_buildCost = 0;
		_buildBucketIndex = 0;
		_buildSolid = solid;
		_buildPenalty = forNext ? _penaltyNext : _penalty;
		_buildForNext = forNext;
		_buildPlayerComponent = (forNext ? _componentNext : _component)[start];
		_buildFromCell = raw;
		_buildTicks = 0;
		_building = true;
		return true;
	}

	// Settle up to `budget` cells of the field under construction (into _distNext), and finish it once
	// the queue is empty: swapped in at once for the live window, held for the swap during a window
	// move. True when finished. Dial's algorithm: every edge costs at most MaxEdgeCost, so a ring of
	// that many buckets holds the whole frontier and the queue never needs a heap.
	private bool StepField(int budget)
	{
		long started = Stopwatch.GetTimestamp();
		int settled = 0;
		int[] dist = _distNext;
		bool[] solid = _buildSolid;
		int[] penalty = _buildPenalty;
		while (_buildPending > 0)
		{
			List<int> bucket = _buckets[_buildCost % _buckets.Length];
			for (; _buildBucketIndex < bucket.Count; _buildBucketIndex++)
			{
				if (settled >= budget)
				{
					RecordFieldStep(started, finished: false);
					return false;
				}

				int c = bucket[_buildBucketIndex];
				_buildPending--;
				if (dist[c] != _buildCost) continue; // superseded by a cheaper entry
				settled++;

				int cx = c % _w;
				int cy = c / _w;
				for (int d = 0; d < 8; d++)
				{
					int nx = cx + Dx[d];
					int ny = cy + Dy[d];
					if (nx < 0 || nx >= _w || ny < 0 || ny >= _h) continue;
					int ni = nx + ny * _w;
					if (solid[ni]) continue;
					// No cutting a wall corner on a diagonal: both straight cells must be open.
					if (d >= 4 && (solid[nx + cy * _w] || solid[cx + ny * _w])) continue;

					int nd = _buildCost + (d < 4 ? StraightCost : DiagonalCost) + penalty[ni];
					if (nd >= dist[ni] || nd > MaxSearchCost) continue;
					dist[ni] = nd;
					_buckets[nd % _buckets.Length].Add(ni);
					_buildPending++;
				}
			}
			bucket.Clear();
			_buildBucketIndex = 0;
			_buildCost++;
		}

		_building = false;
		if (_buildForNext)
		{
			_nextFieldReady = true;
			_nextPlayerComponent = _buildPlayerComponent;
			_nextBuiltFromCell = _buildFromCell;
		}
		else
		{
			(_dist, _distNext) = (_distNext, _dist);
			_playerComponent = _buildPlayerComponent;
			_builtFromCell = _buildFromCell;
			_generation++;
			_hasField = true;
		}
		RecordFieldStep(started, finished: true);
		return true;
	}

	// --- Cost accounting --------------------------------------------------------------------
	// Always on: two timestamps a frame at most. Read and reset by Node2DGame's PERF line.

	/// <summary>Fields completed since the last <see cref="ResetPerf"/>.</summary>
	public static int PerfFieldsBuilt { get; private set; }
	/// <summary>Sum of whole-field build times (all slices of each), ms.</summary>
	public static double PerfFieldMsTotal { get; private set; }
	/// <summary>The most expensive whole field, ms, all slices together.</summary>
	public static double PerfFieldMsWorst { get; private set; }
	/// <summary>The most expensive single slice - what one frame paid for the field - ms.</summary>
	public static double PerfSliceMsWorst { get; private set; }
	/// <summary>When set, Enemy times its navigation work (line test + flow) into <see cref="AddSteeringTicks"/>.</summary>
	public static bool MeasureSteering { get; set; }
	public static long PerfSteeringTicks { get; private set; }
	public static int PerfSteeringCalls { get; private set; }
	public static void AddSteeringTicks(long ticks) { PerfSteeringTicks += ticks; PerfSteeringCalls++; }

	public static void ResetPerf()
	{
		PerfFieldsBuilt = 0;
		PerfFieldMsTotal = PerfFieldMsWorst = PerfSliceMsWorst = 0.0;
		PerfSteeringTicks = 0;
		PerfSteeringCalls = 0;
	}

	private void RecordFieldStep(long started, bool finished)
	{
		long ticks = Stopwatch.GetTimestamp() - started;
		_buildTicks += ticks;
		double sliceMs = ticks * 1000.0 / Stopwatch.Frequency;
		if (sliceMs > PerfSliceMsWorst) PerfSliceMsWorst = sliceMs;
		if (!finished) return;
		double fieldMs = _buildTicks * 1000.0 / Stopwatch.Frequency;
		PerfFieldsBuilt++;
		PerfFieldMsTotal += fieldMs;
		if (fieldMs > PerfFieldMsWorst) PerfFieldMsWorst = fieldMs;
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

	// A body whose centre has been shoved inside a blocked cell: head for the cheapest open neighbour.
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

	private bool FindNearestOpen(bool[] solid, ref int gx, ref int gy)
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
					if (!solid[nx + ny * _w])
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

	private bool WorldToGrid(Vector2 world, out int gx, out int gy) => WorldToGrid(world, _offsetX, _offsetY, out gx, out gy);

	private bool WorldToGrid(Vector2 world, int offsetX, int offsetY, out int gx, out int gy)
	{
		int cx = Mathf.FloorToInt((world.X - _origin.X) / _tile);
		int cy = Mathf.FloorToInt((world.Y - _origin.Y) / _tile);
		gx = cx - offsetX;
		gy = cy - offsetY;
		return gx >= 0 && gx < _w && gy >= 0 && gy < _h;
	}
}
