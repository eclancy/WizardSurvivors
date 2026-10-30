using System;
using System.Collections.Generic;
using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// Decides which small thing - a flower, a lilypad, a reed bed, a pebble - lies on which cell of a
/// streamed chunk. LevelTilePainter.PaintChunkDetail paints what this decides; the art is
/// tools/art/ground_detail.py. See .ai/terrain.md section 8.
/// </summary>
/// <remarks>
/// SEAM-FREE BY CONSTRUCTION. Every decision is a pure function of (stage seed, world cell) and the
/// terrain round that cell - the clustering noise is a lattice hashed in world cells, and the one
/// roll per cell is a hash of the cell - so a cell comes out the same whichever chunk asks for it
/// and whenever it is asked. Nothing here reads an RNG whose position depends on what came first
/// (.ai/terrain.md section 7, the seam rule).
///
/// CLUSTERED, BECAUSE FLOWERS ARE. A uniform scatter at any density reads as a pattern printed on
/// the ground. Each rule scales its density by a smooth noise field (3 n^2, which averages close to
/// 1), so flowers come in drifts with bare turf between them and a meadow has a different mix from
/// the wood beside it.
///
/// NEVER THE SUBJECT. Densities are set so detail is lively at a glance and gone the moment the
/// swarm arrives: every set is drawn in material tones, and the total on open ground is about one
/// cell in twelve. The sets were redrawn at a readable size in 2026-10 (a flower is a 5x5 head on
/// a stem in a clump of leaves, not a speck), and the densities came down by a third with it:
/// fewer things, each of which is a thing. A pickup or an enemy must always be the brightest thing
/// in its neighbourhood, and nothing here is emissive.
/// </remarks>
public static class GroundDetail
{
	/// <summary>Where a rule may put its set.</summary>
	public enum Site
	{
		/// <summary>Open floor (Terrain null), or the FILL cells of the named terrain - never its edges, which fade.</summary>
		Ground,
		/// <summary>Open floor with water orthogonally beside it.</summary>
		Shore,
		/// <summary>Water's fill cells: lilypads.</summary>
		WaterFill,
		/// <summary>Water's edge and outer-corner cells, where the shoreline is: reeds.</summary>
		WaterEdge,
	}

	public readonly record struct Rule(string Set, Site Site, string Terrain, float Density, int ClusterCells);

	private static Rule G(string set, float density, int cluster = 6, string terrain = null) =>
		new(set, Site.Ground, terrain, density, cluster);

	private const string Water = "water";

	private static readonly Dictionary<StageEnvironmentKind, Rule[]> Rules = new()
	{
		[StageEnvironmentKind.Forest] = new[]
		{
			// open turf: drifts of flowers, clover and tufts, the odd fairy ring
			G("flowers_mixed", 0.009f, 7), G("flowers_white", 0.0066f), G("flowers_yellow", 0.0066f),
			G("flowers_violet", 0.0048f), G("flowers_pink", 0.0036f), G("flowers_blue", 0.0036f, 8),
			G("flowers_red", 0.0024f, 9), G("clover", 0.0096f, 5), G("tufts", 0.013f, 4),
			G("leaves", 0.0056f), G("twigs", 0.003f, 1), G("pebbles", 0.0037f, 1),
			G("mushroom_ring", 0.0018f, 1), G("mushrooms", 0.0023f), G("mossy_stone", 0.0016f, 1),
			// a clearing is where the flowers are
			G("flowers_mixed", 0.024f, 5, "meadow"), G("flowers_yellow", 0.015f, 5, "meadow"),
			G("flowers_white", 0.015f, 5, "meadow"), G("flowers_violet", 0.009f, 5, "meadow"),
			G("clover", 0.018f, 4, "meadow"),
			// under the trees: fungus and litter
			G("mushrooms", 0.015f, 5, "moss"), G("leaves", 0.021f, 4, "moss"),
			G("mushroom_ring", 0.006f, 1, "moss"), G("twigs", 0.012f, 1, "moss"),
			G("pebbles", 0.0225f, 1, "dirt"), G("twigs", 0.01f, 1, "dirt"),
			// water
			new("reeds", Site.WaterEdge, Water, 0.176f, 4),
			new("reeds", Site.Shore, null, 0.08f, 4),
			new("tufts", Site.Shore, null, 0.052f, 1),
			new("lilypad", Site.WaterFill, Water, 0.1f, 5),
			new("lilypad_flower", Site.WaterFill, Water, 0.04f, 5),
		},
		[StageEnvironmentKind.Swamp] = new[]
		{
			G("bog_moss", 0.014f, 5), G("tufts", 0.0097f, 5), G("twigs", 0.008f, 1), G("leaves", 0.0056f),
			G("mushrooms", 0.0045f), G("pebbles", 0.003f, 1), G("flowers_white", 0.0018f, 8),
			G("bone_bits", 0.0015f, 1), G("mushroom_ring", 0.0012f, 1),
			G("mushrooms", 0.015f, 5, "moss"), G("flowers_mixed", 0.009f, 6, "moss"), G("clover", 0.012f, 5, "moss"),
			G("pebbles", 0.015f, 1, "dark_dirt"), G("twigs", 0.01f, 1, "dark_dirt"),
			new("reeds", Site.WaterEdge, Water, 0.28f, 4),
			new("reeds", Site.Shore, null, 0.16f, 4),
			new("tufts", Site.Shore, null, 0.065f, 1),
			new("lilypad", Site.WaterFill, Water, 0.15f, 4),
			new("lilypad_flower", Site.WaterFill, Water, 0.05f, 4),
		},
		[StageEnvironmentKind.Ruins] = RuinsRules(),

		// THE SUNKEN CAVE (.ai/endgame-castle.md 7.3, piece V2). It borrowed the Ruins' rules - flowers,
		// clover and tufts on a cave floor. Nothing grows here and nothing was built: grit and rubble fallen
		// from the roof, cracks in the stone, the odd bone, shards of the crystal the cave is full of (the ice
		// set's shards, cold and unlit) in drifts, pale fungus in the silt, and stones along a pool's edge.
		[StageEnvironmentKind.Cave] = new[]
		{
			G("pebbles", 0.009f, 1), G("rubble", 0.008f), G("cracks", 0.012f, 1), G("ice_shards", 0.004f, 7),
			G("bone_bits", 0.0018f, 1),
			G("mushrooms", 0.008f, 5, "dark_dirt"), G("pebbles", 0.015f, 1, "dark_dirt"),
			G("rubble", 0.012f, 1, "rocky_pit"),
			new("pebbles", Site.Shore, null, 0.06f, 1),
		},

		[StageEnvironmentKind.Ice] = new[]
		{
			G("snow", 0.02f, 6), G("frost", 0.015f, 5), G("ice_shards", 0.005f), G("pebbles", 0.0037f, 1),
			G("cracks", 0.004f, 1),
			G("frost", 0.02f, 4, "ice"), G("cracks", 0.012f, 1, "ice"),
			new("frost", Site.Shore, null, 0.05f, 1),
		},
		[StageEnvironmentKind.Desert] = new[]
		{
			G("dry_grass", 0.012f, 6), G("pebbles", 0.0075f, 1), G("cracked_earth", 0.008f, 6),
			G("bone_bits", 0.0025f, 1), G("rubble", 0.003f),
			G("dry_grass", 0.03f, 4, "cobble"), G("cracks", 0.02f, 1, "cobble"),
		},
		[StageEnvironmentKind.Volcanic] = new[]
		{
			G("ash", 0.02f, 5), G("obsidian", 0.008f), G("cracks", 0.01f, 1), G("pebbles", 0.0053f, 1),
			G("rubble", 0.003f),
		},
		[StageEnvironmentKind.Castle] = new[]
		{
			G("cracks", 0.012f, 1), G("rubble", 0.006f), G("moss_seam", 0.006f, 5), G("bone_bits", 0.003f, 1),
			G("pebbles", 0.003f, 1),
		},
		// The Sketchbook has nothing on its page but what the kids drew (CLAUDE.md): no rules.
		//
		// The two early chapters (.ai/campaign.md): Phase 1 STAND-INS, each Phase 2's to replace in
		// its own block with sets drawn in tools/art/terrain_<chapter>.py (DETAIL_SETS).
		// The Fallow Fields: its own four sets (tools/art/detail_fields.py) - stalks the reaper
		// missed and chaff in the stubble, thistles and poppies gone over where nobody ploughed -
		// the poppies and thistles thickest on the hedge banks, stones turned up in the furrows,
		// and a millpond's reeds and lilies.
		[StageEnvironmentKind.Fields] = new[]
		{
			G("stubble_tufts", 0.011f, 5), G("chaff", 0.009f, 4), G("thistle", 0.0035f, 7),
			G("poppy", 0.0035f, 8), G("pebbles", 0.003f, 1), G("dry_grass", 0.004f, 6),
			G("pebbles", 0.018f, 1, "furrow"), G("chaff", 0.006f, 1, "furrow"),
			G("poppy", 0.02f, 5, "hedge_bank"), G("thistle", 0.014f, 4, "hedge_bank"),
			G("tufts", 0.018f, 4, "hedge_bank"), G("flowers_white", 0.006f, 5, "hedge_bank"),
			new("reeds", Site.WaterEdge, Water, 0.15f, 4),
			new("reeds", Site.Shore, null, 0.05f, 4),
			new("tufts", Site.Shore, null, 0.05f, 1),
			new("lilypad", Site.WaterFill, Water, 0.08f, 5),
			new("lilypad_flower", Site.WaterFill, Water, 0.03f, 5),
		},
		// The Broken Circle (tools/art/detail_heath.py). The heather is what makes a violet floor a
		// heath, so it is the densest set here and the most clustered: drifts of it with bare ground
		// between. Cotton-grass is the wet's flag - on the peat and round the tarns, a little in the
		// open - and the outcrops shed lichened granite. No lilies: a moor tarn is bare water.
		[StageEnvironmentKind.Heath] = new[]
		{
			G("heather_tufts", 0.09f, 6), G("tufts", 0.006f, 5), G("cotton_grass", 0.004f, 7),
			G("lichen_stones", 0.004f, 1), G("pebbles", 0.003f, 1),
			G("cotton_grass", 0.06f, 4, "peat"), G("bog_moss", 0.008f, 5, "peat"),
			G("lichen_stones", 0.015f, 1, "granite"), G("cracks", 0.01f, 1, "granite"),
			G("rubble", 0.015f, 1, "rocky_pit"),
			new("reeds", Site.WaterEdge, Water, 0.10f, 4),
			new("cotton_grass", Site.Shore, null, 0.07f, 4),
			new("tufts", Site.Shore, null, 0.04f, 1),
		},

		// THE CASTLE (.ai/endgame-castle.md): STAND-INS, each its own piece's block to replace (C1 the
		// bridge's detail_bridge.py - cracks, scorch, dropped bolts, pitch stains; C3 the Court's; C4 the
		// Throne's), a blank line apart. Laid stone, so only what lies on laid stone: no growing thing.
		[StageEnvironmentKind.Bridge] = new[]
		{
			G("cracks", 0.012f, 1), G("rubble", 0.004f), G("pebbles", 0.003f, 1),
		},

		[StageEnvironmentKind.Court] = new[]
		{
			G("cracks", 0.006f, 1), G("pebbles", 0.002f, 1),
		},

		[StageEnvironmentKind.Throne] = new[]
		{
			G("cracks", 0.004f, 1),
		},
	};

	private static Rule[] RuinsRules() => new[]
	{
		G("rubble", 0.009f), G("cracks", 0.012f, 1), G("pebbles", 0.0075f, 1), G("moss_seam", 0.008f, 5),
		G("tufts", 0.0039f, 5), G("bone_bits", 0.002f, 1),
		G("flowers_white", 0.012f, 5, "overgrowth"), G("flowers_violet", 0.006f, 5, "overgrowth"),
		G("clover", 0.012f, 4, "overgrowth"), G("tufts", 0.013f, 4, "overgrowth"),
		G("pebbles", 0.015f, 1, "dark_dirt"), G("mushrooms", 0.0045f, 5, "dark_dirt"),
		G("rubble", 0.015f, 1, "cobble"),
	};

	public static IReadOnlyList<Rule> RulesFor(StageEnvironmentKind kind) =>
		Rules.TryGetValue(kind, out Rule[] r) ? r : Array.Empty<Rule>();

	/// <summary>Every set a stage can paint, for building its tile sources at stage start.</summary>
	public static IEnumerable<string> SetsFor(StageEnvironmentKind kind)
	{
		var seen = new HashSet<string>();
		foreach (Rule r in RulesFor(kind))
			if (seen.Add(r.Set))
				yield return r.Set;
	}

	/// <summary>Every set any stage names, for RegressionChecks to hold against the manifest.</summary>
	public static IEnumerable<string> AllSets()
	{
		var seen = new HashSet<string>();
		foreach (Rule[] rules in Rules.Values)
			foreach (Rule r in rules)
				if (seen.Add(r.Set))
					yield return r.Set;
	}

	private const int SaltRoll = 0x6D17;
	private const int SaltCluster = 0x2C1F;

	// The clustering noise's lattice values for every rule over the current chunk, filled once per
	// chunk. Evaluating the noise per cell hashed four lattice corners per rule per cell - 80,000
	// hashes a chunk in the Forest, and the step took 7-15 ms in a debug build against a 2 ms frame
	// budget. A rule's lattice over one chunk is at most 18 x 18 values (a cluster of 2 cells; the
	// rules use 4 to 9, so 10 x 10), and the buffer is sized for the worst case once.
	private const int LatticeSpan = TerrainStreamer.ChunkTiles / 2 + 2;
	private static double[] lattice = Array.Empty<double>();
	private static readonly int[] latticeX0 = new int[64];
	private static readonly int[] latticeY0 = new int[64];

	/// <summary>
	/// Fill <paramref name="setPerCell"/> (indexed x + y * ChunkTiles) with the detail set for each
	/// cell of <paramref name="chunk"/>, or null. Cells inside <paramref name="reserved"/> (a
	/// landmark) are left bare. Allocation-free after the first call: the caller owns the array.
	/// </summary>
	public static void PlanChunk(TerrainStreamer streamer, TerrainChunk chunk, StageEnvironmentKind kind,
		ulong seed, Rect2I? reserved, string[] setPerCell)
	{
		Array.Clear(setPerCell);
		IReadOnlyList<Rule> rules = RulesFor(kind);
		if (rules.Count == 0)
			return;

		const int n = TerrainStreamer.ChunkTiles;
		Compiled c = CompiledFor(kind, rules);
		FillLattice(rules, chunk.MinCell, seed);

		for (int y = 0; y < n; y++)
		{
			for (int x = 0; x < n; x++)
			{
				int i = x + y * n;
				int wx = chunk.MinCell.X + x;
				int wy = chunk.MinCell.Y + y;
				if (reserved is Rect2I r && r.HasPoint(new Vector2I(wx, wy)))
					continue;
				// Walls and hazards carry nothing: a flower in the lava is a lie about the lava.
				if ((chunk.Flags[i] & (TerrainStreamer.FlagWall | TerrainStreamer.FlagHazard)) != 0)
					continue;

				string terrain = chunk.Terrain[i];
				AutotileRole role = chunk.Roles[i];

				// Which rules could apply to a cell like this one - precompiled per stage, so a cell
				// costs a lookup rather than a pass over every rule.
				Group first, second = Group.Empty;
				if (terrain == null)
				{
					first = c.Open;
					second = c.Shore;
				}
				else if (role == AutotileRole.Fill)
				{
					first = c.Fill.TryGetValue(terrain, out Group g) ? g : Group.Empty;
					if (terrain == Water)
						second = c.WaterFill;
				}
				else if (terrain == Water && IsEdgeRole(role))
				{
					first = c.WaterEdge;
				}
				else
				{
					continue;
				}

				// One roll per cell against the running sum of every applicable rule's local
				// density, so at most one set lands on a cell. The noise is at most 3x a rule's
				// density, so a roll above the group's bound can never land - most cells - and is
				// skipped before any noise or neighbour is read.
				double roll = Unit(LevelGenerator.Hash(seed, wx, wy, SaltRoll));
				if (roll >= first.Bound + second.Bound)
					continue;
				// Shore is the expensive question (four neighbours, maybe over a chunk border), so
				// it is asked only of the few open cells whose roll could still land on a shore rule.
				if (second.Rules.Length > 0 && terrain == null && !IsShore(streamer, chunk, x, y))
					second = Group.Empty;

				double sum = 0;
				string set = Pick(first, rules, wx, wy, roll, ref sum) ?? Pick(second, rules, wx, wy, roll, ref sum);
				if (set != null)
					setPerCell[i] = set;
			}
		}
	}

	private static string Pick(Group group, IReadOnlyList<Rule> rules, int wx, int wy, double roll, ref double sum)
	{
		foreach (int k in group.Rules)
		{
			Rule rule = rules[k];
			sum += rule.Density * ClusterFactor(k, rule.ClusterCells, wx, wy);
			if (roll < sum)
				return rule.Set;
		}
		return null;
	}

	/// <summary>Rule indices that can apply to one kind of cell, and 3x their summed density.</summary>
	private readonly struct Group
	{
		public static readonly Group Empty = new(Array.Empty<int>(), 0);
		public readonly int[] Rules;
		public readonly double Bound;
		public Group(int[] rules, double bound) { Rules = rules; Bound = bound; }
	}

	private sealed class Compiled
	{
		public Group Open, Shore, WaterFill, WaterEdge;
		public readonly Dictionary<string, Group> Fill = new();
	}

	private static readonly Dictionary<StageEnvironmentKind, Compiled> compiled = new();

	private static Compiled CompiledFor(StageEnvironmentKind kind, IReadOnlyList<Rule> rules)
	{
		if (compiled.TryGetValue(kind, out Compiled known))
			return known;
		Group Make(Func<Rule, bool> match)
		{
			var idx = new List<int>();
			double bound = 0;
			for (int k = 0; k < rules.Count; k++)
			{
				if (!match(rules[k])) continue;
				idx.Add(k);
				bound += 3.0 * rules[k].Density;
			}
			return new Group(idx.ToArray(), bound);
		}
		var c = new Compiled
		{
			Open = Make(r => r.Site == Site.Ground && r.Terrain == null),
			Shore = Make(r => r.Site == Site.Shore),
			WaterFill = Make(r => r.Site == Site.WaterFill),
			WaterEdge = Make(r => r.Site == Site.WaterEdge),
		};
		foreach (Rule rule in rules)
		{
			if (rule.Site == Site.Ground && rule.Terrain != null && !c.Fill.ContainsKey(rule.Terrain))
			{
				string t = rule.Terrain;
				c.Fill[t] = Make(r => r.Site == Site.Ground && r.Terrain == t);
			}
		}
		compiled[kind] = c;
		return c;
	}

	private static bool IsEdgeRole(AutotileRole role) =>
		role is AutotileRole.EdgeTop or AutotileRole.EdgeBottom or AutotileRole.EdgeLeft or AutotileRole.EdgeRight
			or AutotileRole.CornerTL or AutotileRole.CornerTR or AutotileRole.CornerBL or AutotileRole.CornerBR;

	private static bool IsShore(TerrainStreamer streamer, TerrainChunk chunk, int x, int y)
	{
		const int n = TerrainStreamer.ChunkTiles;
		return Neighbour(x + 1, y) == Water || Neighbour(x - 1, y) == Water
			|| Neighbour(x, y + 1) == Water || Neighbour(x, y - 1) == Water;

		string Neighbour(int nx, int ny) =>
			nx >= 0 && nx < n && ny >= 0 && ny < n
				? chunk.Terrain[nx + ny * n]
				: streamer.TerrainAtCell(chunk.MinCell.X + nx, chunk.MinCell.Y + ny);
	}

	// Each rule's lattice values over the chunk: corner (gx, gy) of rule k is a hash of the WORLD
	// lattice coordinate, so the field is the same whichever chunk reads it - seam-free.
	private static void FillLattice(IReadOnlyList<Rule> rules, Vector2I minCell, ulong seed)
	{
		int need = rules.Count * LatticeSpan * LatticeSpan;
		if (lattice.Length < need)
			lattice = new double[need];
		for (int k = 0; k < rules.Count && k < latticeX0.Length; k++)
		{
			int cells = rules[k].ClusterCells;
			if (cells <= 1)
				continue;
			int gx0 = FloorDiv(minCell.X, cells);
			int gy0 = FloorDiv(minCell.Y, cells);
			latticeX0[k] = gx0;
			latticeY0[k] = gy0;
			int count = FloorDiv(minCell.X + TerrainStreamer.ChunkTiles - 1, cells) - gx0 + 2;
			int s = SaltCluster + k * 131;
			int baseIndex = k * LatticeSpan * LatticeSpan;
			for (int j = 0; j < count; j++)
				for (int i = 0; i < count; i++)
					lattice[baseIndex + i + j * LatticeSpan] = Unit(LevelGenerator.Hash(seed, gx0 + i, gy0 + j, s));
		}
	}

	/// <summary>3 n^2 of a smooth value noise on a lattice of <paramref name="cells"/>: averages near 1.</summary>
	private static double ClusterFactor(int k, int cells, int wx, int wy)
	{
		if (cells <= 1)
			return 1.0;
		int gx = FloorDiv(wx, cells);
		int gy = FloorDiv(wy, cells);
		double fx = (wx - gx * cells + 0.5) / cells;
		double fy = (wy - gy * cells + 0.5) / cells;
		fx = fx * fx * (3 - 2 * fx);
		fy = fy * fy * (3 - 2 * fy);
		int baseIndex = k * LatticeSpan * LatticeSpan + (gx - latticeX0[k]) + (gy - latticeY0[k]) * LatticeSpan;
		double a = lattice[baseIndex];
		double b = lattice[baseIndex + 1];
		double c = lattice[baseIndex + LatticeSpan];
		double d = lattice[baseIndex + LatticeSpan + 1];
		double top = a + (b - a) * fx;
		double v = top + ((c + (d - c) * fx) - top) * fy;
		return 3.0 * v * v;
	}

	private static double Unit(ulong h) => (h >> 11) * (1.0 / (1UL << 53));

	private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
}
