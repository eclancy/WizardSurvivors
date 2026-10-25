using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// The "enemies" group held as a C# array, kept in step with the group by the only code that ever
/// changes it, and a grid over the swarm's positions for "who is near here" questions.
/// </summary>
/// <remarks>
/// <para><b>Why.</b> <c>GetTree().GetNodesInGroup("enemies")</c> builds a native array and hands it
/// to C# as a <c>Godot.Collections.Array</c>, and every element read out of it crosses the interop
/// boundary and looks the object up again. That is cheap once. The separation steering did it in
/// every enemy, every fourth physics step - at the 140 cap, about 35 arrays and 5,000 marshalled
/// reads a step, before each read's <c>GlobalPosition</c> crossed the boundary again - and it was
/// the largest single cost of a full swarm (.ai/performance.md). This is the same list with no
/// marshalling, and the grid turns "everyone within 73 px of me" from a walk over the whole swarm
/// into a look at a few cells.</para>
///
/// <para><b>The group stays the authority.</b> <see cref="Join"/> and <see cref="Leave"/> do the
/// group change themselves, so the two cannot drift: an enemy is in <see cref="Snapshot"/> exactly
/// when it is in the group. A dying enemy leaves both in the same call, the frame it dies, as
/// <c>Enemy.StartDeath</c> always has. Gameplay code that asks <c>IsInGroup("enemies")</c> about one
/// body still gets the same answer. Only <c>Enemy</c> and <c>ExploderEnemy</c>'s detonation call
/// these; anything else that adds to or removes from the group directly breaks the mirror, which
/// the frame profiler (<c>--profile</c>) reports as an error every time it prints.</para>
///
/// <para><b>Order.</b> Members are kept in the order they joined. Every ordinary enemy is a child of
/// the game added in spawn order, so that is the tree order the group itself returns; only a burrowed
/// boss that surfaces again goes to the back instead of its child slot.</para>
///
/// <para><b>Snapshots are immutable.</b> A caller may kill enemies while it walks one (an AoE does,
/// and a kill can start another AoE - Frost Shard - mid-walk). So a change to the membership never
/// touches an array already handed out: the next <see cref="Snapshot"/> after a change is a fresh
/// copy, which is at most one small allocation per change rather than one per caller per frame.</para>
///
/// <para><b>The grid</b> is rebuilt at most once per physics step, the first time it is asked for
/// after the step began or after someone joined, from one <c>GlobalPosition</c> read per enemy.
/// Positions in it are therefore up to one step old. <see cref="CollectNear"/> is a broad phase
/// with a margin for that; callers whose result is gameplay (a hit or a target) test the live
/// position of what it returns, exactly as they tested every enemy before.</para>
/// </remarks>
public static class EnemyRegistry
{
	public const string GroupName = "enemies";

	private static readonly List<Enemy> members = new(256);
	private static Enemy[] snapshot = Array.Empty<Enemy>();
	private static bool snapshotStale;
	private static int joinsSinceGrid;

	/// <summary>Every enemy in the "enemies" group, in join order. Never modify the array.</summary>
	public static Enemy[] Snapshot
	{
		get
		{
			if (snapshotStale)
			{
				snapshot = members.Count == 0 ? Array.Empty<Enemy>() : members.ToArray();
				snapshotStale = false;
			}
			return snapshot;
		}
	}

	/// <summary>How many enemies are in the group right now.</summary>
	public static int Count => members.Count;

	/// <summary>Puts <paramref name="enemy"/> in the "enemies" group and the registry.</summary>
	public static void Join(Enemy enemy)
	{
		if (!enemy.IsInGroup(GroupName))
			enemy.AddToGroup(GroupName);
		if (enemy.InEnemyRegistry)
			return;
		enemy.InEnemyRegistry = true;
		members.Add(enemy);
		snapshotStale = true;
		joinsSinceGrid++;
	}

	/// <summary>Takes <paramref name="enemy"/> out of the "enemies" group and the registry.</summary>
	public static void Leave(Enemy enemy)
	{
		if (enemy.IsInGroup(GroupName))
			enemy.RemoveFromGroup(GroupName);
		Forget(enemy);
	}

	/// <summary>
	/// Drops <paramref name="enemy"/> from the registry only, leaving its group membership alone -
	/// for a body leaving the tree, which takes it out of the tree's group list by itself and puts it
	/// back on re-entering (see <c>Enemy._EnterTree</c>).
	/// </summary>
	public static void Forget(Enemy enemy)
	{
		if (!enemy.InEnemyRegistry)
			return;
		enemy.InEnemyRegistry = false;
		members.Remove(enemy);
		snapshotStale = true;
		// The arena emptied (a run ending, a scene change): let go of the last arrays handed out, so
		// statics do not keep freed enemies - and the resources their fields hold - reachable.
		if (members.Count == 0)
		{
			snapshot = Array.Empty<Enemy>();
			snapshotStale = false;
			gridEnemies = Array.Empty<Enemy>();
			gridCount = 0;
			gridPhysicsFrame = ulong.MaxValue;
		}
	}

	// --- The grid ---------------------------------------------------------------------------

	// 64 px cells: a little under the separation radius (59 px, up to 73 with its jitter), so that
	// query looks at a 3x3 block, and small enough that a 400 px AoE is still only ~150 cells.
	private const float CellSize = 64f;
	private const float InverseCellSize = 1f / CellSize;
	// Past this many cells the grid coarsens instead of growing - a swarm spread over the whole
	// recycle radius (1600 px each way) is 2,500 cells at 64, well inside it.
	private const int MaxCells = 16384;

	private static ulong gridPhysicsFrame = ulong.MaxValue;
	private static Enemy[] gridEnemies = Array.Empty<Enemy>();
	private static Vector2[] gridPositions = new Vector2[256];
	private static int gridCount;
	private static int[] cellStart = new int[1];
	private static int[] cellItems = new int[256];
	private static int[] itemCell = new int[256];
	private static int gridColumns, gridRows;
	private static float gridCellSize = CellSize, gridInverseCell = InverseCellSize;
	private static Vector2 gridOrigin;

	/// <summary>How many times the grid was rebuilt, for the profiler.</summary>
	public static int GridBuilds { get; private set; }

	private static void EnsureGrid()
	{
		ulong frame = Engine.GetPhysicsFrames();
		if (frame == gridPhysicsFrame && joinsSinceGrid == 0)
			return;
		gridPhysicsFrame = frame;
		joinsSinceGrid = 0;
		GridBuilds++;

		Enemy[] enemies = Snapshot;
		int n = enemies.Length;
		gridEnemies = enemies;
		gridCount = n;
		if (gridPositions.Length < n)
		{
			int size = Math.Max(n, gridPositions.Length * 2);
			gridPositions = new Vector2[size];
			cellItems = new int[size];
			itemCell = new int[size];
		}
		if (n == 0)
		{
			gridColumns = gridRows = 0;
			return;
		}

		float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
		for (int i = 0; i < n; i++)
		{
			Vector2 p = enemies[i].GlobalPosition;
			gridPositions[i] = p;
			if (p.X < minX) minX = p.X;
			if (p.Y < minY) minY = p.Y;
			if (p.X > maxX) maxX = p.X;
			if (p.Y > maxY) maxY = p.Y;
		}

		gridCellSize = CellSize;
		int columns, rows;
		while (true)
		{
			columns = (int)((maxX - minX) / gridCellSize) + 1;
			rows = (int)((maxY - minY) / gridCellSize) + 1;
			if ((long)columns * rows <= MaxCells)
				break;
			gridCellSize *= 2f;
		}
		gridInverseCell = 1f / gridCellSize;
		gridColumns = columns;
		gridRows = rows;
		gridOrigin = new Vector2(minX, minY);
		int cells = columns * rows;
		if (cellStart.Length < cells + 1)
			cellStart = new int[Math.Max(cells + 1, cellStart.Length * 2)];
		Array.Clear(cellStart, 0, cells + 1);

		// Counting sort into cells: count, prefix-sum, place.
		for (int i = 0; i < n; i++)
		{
			int cx = (int)((gridPositions[i].X - minX) * gridInverseCell);
			int cy = (int)((gridPositions[i].Y - minY) * gridInverseCell);
			int cell = cy * columns + cx;
			itemCell[i] = cell;
			cellStart[cell + 1]++;
		}
		for (int c = 0; c < cells; c++)
			cellStart[c + 1] += cellStart[c];
		for (int i = 0; i < n; i++)
		{
			int cell = itemCell[i];
			// cellStart[cell] is advanced while placing and restored below.
			cellItems[cellStart[cell]++] = i;
		}
		for (int c = cells; c > 0; c--)
			cellStart[c] = cellStart[c - 1];
		cellStart[0] = 0;
	}

	/// <summary>
	/// Visits every enemy whose position (as of the grid's last build, up to one physics step old)
	/// lies within <paramref name="radius"/> of <paramref name="centre"/>, passing it and that position.
	/// </summary>
	/// <remarks>
	/// Allocation-free: the visitor is a struct the caller defines, called through a generic
	/// constraint, so a per-enemy query in the swarm creates no garbage. An enemy that has left the
	/// group since the build is skipped. A visitor must not query the registry itself - the walk
	/// shares one index buffer.
	/// </remarks>
	public static void ForEachNear<TVisitor>(Vector2 centre, float radius, ref TVisitor visitor)
		where TVisitor : struct, INeighbourVisitor
	{
		GatherIndices(centre, radius);
		Enemy[] enemies = gridEnemies;
		for (int k = 0; k < nearIndices.Count; k++)
		{
			int i = nearIndices[k];
			Enemy enemy = enemies[i];
			if (enemy.InEnemyRegistry)
				visitor.Visit(enemy, gridPositions[i]);
		}
		nearIndices.Clear();
	}

	/// <summary>
	/// How far an enemy can have walked since the grid was built, for <see cref="CollectNear"/>. The
	/// grid is never older than the current physics step, and nothing in the roster covers 64 px in
	/// one step (that is 3,840 px/s; a Lunger's dash is a few hundred).
	/// </summary>
	public const float StalenessMargin = 64f;

	private static readonly List<int> nearIndices = new(64);

	/// <summary>
	/// Fills <paramref name="results"/> with every enemy that could be within <paramref name="radius"/>
	/// of <paramref name="centre"/> right now - a superset, in <see cref="Snapshot"/> order.
	/// </summary>
	/// <remarks>
	/// The broad phase for a scan that used to walk the whole group: the caller keeps its own test
	/// against each enemy's live <c>GlobalPosition</c>, so what it hits is exactly what it hit before,
	/// in the same order - only enemies that could not possibly qualify are never looked at. The
	/// margin covers the grid being up to one physics step old. <paramref name="results"/> is the
	/// caller's own list, so it is safe to walk while the walk kills enemies.
	/// </remarks>
	public static void CollectNear(Vector2 centre, float radius, List<Enemy> results)
	{
		results.Clear();
		GatherIndices(centre, radius + StalenessMargin);
		nearIndices.Sort();
		Enemy[] enemies = gridEnemies;
		foreach (int i in nearIndices)
		{
			Enemy enemy = enemies[i];
			if (enemy.InEnemyRegistry)
				results.Add(enemy);
		}
		nearIndices.Clear();
	}

	// Every grid index whose stored position lies within radius of centre, into nearIndices.
	private static void GatherIndices(Vector2 centre, float radius)
	{
		nearIndices.Clear();
		EnsureGrid();
		if (gridCount == 0)
			return;

		float radiusSquared = radius * radius;
		int x0 = Math.Max(0, (int)Mathf.Floor((centre.X - radius - gridOrigin.X) * gridInverseCell));
		int y0 = Math.Max(0, (int)Mathf.Floor((centre.Y - radius - gridOrigin.Y) * gridInverseCell));
		int x1 = Math.Min(gridColumns - 1, (int)Mathf.Floor((centre.X + radius - gridOrigin.X) * gridInverseCell));
		int y1 = Math.Min(gridRows - 1, (int)Mathf.Floor((centre.Y + radius - gridOrigin.Y) * gridInverseCell));
		for (int cy = y0; cy <= y1; cy++)
		{
			int row = cy * gridColumns;
			for (int cx = x0; cx <= x1; cx++)
			{
				int cell = row + cx;
				int end = cellStart[cell + 1];
				for (int k = cellStart[cell]; k < end; k++)
				{
					int i = cellItems[k];
					Vector2 p = gridPositions[i];
					float dx = p.X - centre.X, dy = p.Y - centre.Y;
					if (dx * dx + dy * dy <= radiusSquared)
						nearIndices.Add(i);
				}
			}
		}
	}
}

/// <summary>What <see cref="EnemyRegistry.ForEachNear"/> calls for each enemy it finds.</summary>
public interface INeighbourVisitor
{
	void Visit(Enemy enemy, Vector2 position);
}
