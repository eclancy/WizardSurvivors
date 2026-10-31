using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

// THE BESTIARY (.ai/bestiary.md): every chapter's own cast, and the single authority on which scene
// fills which job in which chapter.
//
// Eric, 2026-10-24: "move the enemies on each level to be unique to that level. the forest should have
// evil forest creatures, the crypts should have evil undead, etc. each stage should have it's unique
// set of creatures." Before this, six behaviour roles (Hexer, Skull Sentry, Lunger, Exploder, Slammer,
// Summoner) were rolled in every chapter with one look each, the Warden was the one recurring miniboss
// everywhere, and seven of the nine chapter tables drew from the same four bodies. Only the Forest and
// the Sketchbook had anything of their own.
//
// THE SHAPE. This file holds the types and the lookup and nothing about any particular chapter. Each
// chapter's roster lives in its own file, StageBestiary.<Chapter>.cs, as a nested static class with a
// Build(), so the chapters can be drawn in parallel by people who never touch the same lines. A roster
// names a creature for every job the spawner has - four table bodies, the six behaviour roles and the
// miniboss - and the table that rolls the bodies, with the same shares and health multipliers the old
// environment switch in Node2DGame carried.
//
// PLANNED CREATURES AND STAND-INS. A chapter whose own creature is designed but not yet drawn names it
// anyway - its id, its name, and the scene path it WILL live at - together with a stand-in: the
// creature (always another chapter's) that fills the slot today. The moment a scene exists at the
// planned path, the chapter spawns it instead, with no edit to any roster. Drawing a creature is
// therefore creating files and nothing else, and RegressionChecks.ValidateBestiary lists every slot
// still on its stand-in at startup, which is the to-do list for the art. The stand-in is also the
// stat block the new creature is held to (ValidateBestiary compares them), because every chapter's
// difficulty mix was tuned on those numbers and a new body is a new look, not a new balance.

/// <summary>The jobs a chapter's spawner needs a creature for.</summary>
public enum BestiaryRole
{
	// The table's four bodies, rolled from the chapter's spawn table.
	/// <summary>The baseline chaser everything else is read against (was <c>enemy.tscn</c>).</summary>
	Bulk,
	/// <summary>The sprinter (was <c>FastEnemy.tscn</c>). A summoner's minion is its chapter's Fast.</summary>
	Fast,
	/// <summary>The tank (was <c>TankEnemy.tscn</c>). Also what a side event asks for when it wants a heavy body.</summary>
	Heavy,
	/// <summary>The slow wall (was <c>SlowEnemy.tscn</c>).</summary>
	Wall,

	// The six behaviour roles. Each is rolled on its own ahead of the table, on its own clock and share
	// (Node2DGame.HexerFirstSpawnSeconds and the five beside it), so a chapter's table keeps the full
	// 0..1 spread it was tuned on.
	/// <summary>The back line: a <c>RangedEnemy</c> that walks (was the Hexer).</summary>
	Ranged,
	/// <summary>The rooted turret: a <c>RangedEnemy</c> with <c>Rooted</c> (was the Skull Sentry). Never an elite.</summary>
	Turret,
	/// <summary>A <c>LungerEnemy</c>.</summary>
	Charger,
	/// <summary>An <c>ExploderEnemy</c>.</summary>
	Exploder,
	/// <summary>A <c>SlammerEnemy</c>.</summary>
	Slammer,
	/// <summary>A <c>SummonerEnemy</c>. Never an elite.</summary>
	Summoner,

	/// <summary>The recurring miniboss sent on a clock (was the Warden everywhere), and the Gaoler's key carrier.</summary>
	MiniBoss,

	/// <summary>A table body beyond the four: the Forest's wolves and wisps, the Sketchbook's one-eye and message.</summary>
	Extra,
}

/// <summary>One creature, in the chapter it belongs to.</summary>
public sealed class BestiaryCreature
{
	public BestiaryCreature(string id, string displayName, string homeStageId, string scenePath, string enemyType)
	{
		Id = id;
		DisplayName = displayName;
		HomeStageId = homeStageId;
		ScenePath = scenePath;
		EnemyType = enemyType;
	}

	/// <summary>Stable id, "chapter_name". Unique across the bestiary.</summary>
	public string Id { get; }

	public string DisplayName { get; }

	/// <summary>The chapter it belongs to. A creature spawned anywhere else is borrowed.</summary>
	public string HomeStageId { get; }

	/// <summary>Where its scene lives - or, for a creature not drawn yet, where it will.</summary>
	public string ScenePath { get; }

	/// <summary>
	/// The scene's <c>Enemy.EnemyType</c>, declared here so the miniboss clock can tell its own body
	/// in the registry without instantiating anything. ValidateBestiary checks it against the scene.
	/// </summary>
	public string EnemyType { get; }

	/// <summary>True once a scene exists at <see cref="ScenePath"/>.</summary>
	public bool IsDrawn => !string.IsNullOrEmpty(ScenePath) && ResourceLoader.Exists(ScenePath);

	public override string ToString() => $"{DisplayName} ({Id})";
}

/// <summary>One job in one chapter, and the creature that does it.</summary>
public sealed class BestiarySlot
{
	private BestiarySlot(BestiaryRole role, BestiaryCreature? creature, BestiaryCreature? standIn, string absentReason,
		string statsChange = "")
	{
		Role = role;
		Creature = creature;
		StandIn = standIn;
		AbsentReason = absentReason;
		StatsChange = statsChange;
	}

	public BestiaryRole Role { get; }

	/// <summary>The chapter's creature for this job, drawn or still planned. Null when the chapter has none.</summary>
	public BestiaryCreature? Creature { get; }

	/// <summary>
	/// What spawns while <see cref="Creature"/>'s scene does not exist yet - and, once it does, the stat
	/// block the new creature is held to. Null for a creature that was drawn for this job from the start.
	/// </summary>
	public BestiaryCreature? StandIn { get; }

	/// <summary>Why the chapter has no creature for this job. Required when <see cref="Creature"/> is null.</summary>
	public string AbsentReason { get; }

	/// <summary>
	/// Empty unless the drawn creature is MEANT to differ from its stand-in's stat block, in which case
	/// this says why and ValidateBestiary stops holding it to those numbers. A new look should not
	/// quietly retune a chapter that was balanced on the old one.
	/// </summary>
	public string StatsChange { get; }

	public bool IsAbsent => Creature == null;

	/// <summary>The creature that actually spawns today: the planned one once drawn, its stand-in until then.</summary>
	public BestiaryCreature? Resolved =>
		Creature == null ? null
		: Creature.IsDrawn || StandIn == null ? Creature
		: StandIn;

	/// <summary>A creature drawn for this job and filling it now.</summary>
	public static BestiarySlot Own(BestiaryRole role, BestiaryCreature creature) => new(role, creature, null, string.Empty);

	/// <summary>A creature designed for this job, with what fills it until the creature's scene exists.</summary>
	/// <param name="statsChange">Why the creature deliberately departs from the stand-in's numbers, if it does.</param>
	public static BestiarySlot Planned(BestiaryRole role, BestiaryCreature creature, BestiaryCreature standIn,
		string statsChange = "") =>
		new(role, creature, standIn, string.Empty, statsChange);

	/// <summary>A job this chapter deliberately does not have. The spawner skips its roll entirely.</summary>
	public static BestiarySlot Absent(BestiaryRole role, string reason) => new(role, null, null, reason);
}

/// <summary>One band of a chapter's spawn table: a body, its share of the roll, its health multiplier.</summary>
public readonly struct BestiaryTableEntry
{
	public BestiaryTableEntry(BestiarySlot slot, float share, float healthMultiplier)
	{
		Slot = slot;
		Share = share;
		HealthMultiplier = healthMultiplier;
	}

	public BestiarySlot Slot { get; }
	public float Share { get; }
	public float HealthMultiplier { get; }
}

/// <summary>A chapter's whole cast: a creature for every job, and the table that rolls the bodies.</summary>
public sealed class ChapterBestiary
{
	public string StageId { get; init; } = string.Empty;

	public IReadOnlyList<BestiarySlot> Slots { get; init; } = Array.Empty<BestiarySlot>();

	/// <summary>
	/// Rolled in order on one 0..1 roll; the last band takes whatever is left. Order is kept as the old
	/// environment switch had it, so a roll lands on the same body it always did.
	/// </summary>
	public IReadOnlyList<BestiaryTableEntry> Table { get; init; } = Array.Empty<BestiaryTableEntry>();

	/// <summary>
	/// A second table from <see cref="LateTableFromPaceMinutes"/> on, or empty. Only the Sunken Cave has
	/// one: it never had a case of its own in the environment switch and took the default branch,
	/// which changed its mix at five pace minutes.
	/// </summary>
	public IReadOnlyList<BestiaryTableEntry> LateTable { get; init; } = Array.Empty<BestiaryTableEntry>();

	public float LateTableFromPaceMinutes { get; init; }

	/// <summary>
	/// Creatures that belong to the chapter and fill no slot: what its boss calls up (the Rime Guard),
	/// the Sketchbook's ghost. Listed so the ownership check knows whose they are.
	/// </summary>
	public IReadOnlyList<BestiaryCreature> Attendants { get; init; } = Array.Empty<BestiaryCreature>();

	/// <summary>
	/// True for the one chapter whose art is not held to .ai/art-direction.md - the Sketchbook, which is
	/// Eric's kids' drawings and never gets derived frames. ValidateBestiary does not ask its sheets for
	/// a death or an attack strip, and lets its heavy be a miniboss, because that is how they were drawn.
	/// </summary>
	public bool ArtContractExempt { get; init; }

	public string ChapterName => StageCatalog.GetById(StageId)?.DisplayName ?? StageId;

	/// <summary>The slot for a role. Every role but <see cref="BestiaryRole.Extra"/> has exactly one.</summary>
	public BestiarySlot? SlotFor(BestiaryRole role)
	{
		foreach (BestiarySlot slot in Slots)
		{
			if (slot.Role == role)
				return slot;
		}
		return null;
	}

	/// <summary>Every creature this roster names: slot creatures, their stand-ins, and the attendants.</summary>
	public IEnumerable<BestiaryCreature> NamedCreatures()
	{
		foreach (BestiarySlot slot in Slots)
		{
			if (slot.Creature != null)
				yield return slot.Creature;
			if (slot.StandIn != null)
				yield return slot.StandIn;
		}
		foreach (BestiaryCreature attendant in Attendants)
			yield return attendant;
	}
}

/// <summary>The bestiary: one roster per chapter, each built in its own file.</summary>
public static partial class StageBestiary
{
	/// <summary>The roles every chapter must answer for, with a creature or a stated absence.</summary>
	public static readonly BestiaryRole[] RequiredRoles =
	{
		BestiaryRole.Bulk, BestiaryRole.Fast, BestiaryRole.Heavy, BestiaryRole.Wall,
		BestiaryRole.Ranged, BestiaryRole.Turret, BestiaryRole.Charger, BestiaryRole.Exploder,
		BestiaryRole.Slammer, BestiaryRole.Summoner, BestiaryRole.MiniBoss,
	};

	private static ChapterBestiary[]? chapters;

	/// <summary>
	/// Every chapter's roster. This list is the only line in the bestiary that names every chapter, and
	/// it is written once: drawing a chapter's creatures never changes it. It is in the order the
	/// chapters were made, NOT campaign order - that is StageCatalog's alone (.ai/campaign.md), and
	/// nothing here depends on position.
	/// </summary>
	/// <remarks>
	/// Built on first use rather than in a static initialiser, and each chapter is a nested class of its
	/// own: C# does not define the order of static field initialisers across the files of a partial
	/// class, so a chapter that borrows the Cursed Dungeon's creatures could otherwise read them before
	/// they exist. A nested class is initialised on first touch, which is always in time.
	/// </remarks>
	public static IReadOnlyList<ChapterBestiary> All => chapters ??= new[]
	{
		EnchantedForest.Build(),
		CursedDungeon.Build(),
		SunkenCave.Build(),
		BlightedSwamp.Build(),
		MysticRuins.Build(),
		FrozenWaste.Build(),
		ScorchedSands.Build(),
		Emberdeep.Build(),
		Sketchbook.Build(),
		FallowFields.Build(),
		BrokenCircle.Build(),
		// The castle (.ai/endgame-castle.md): the bridge's garrison and the Court's household. The Cursed
		// Dungeon keeps its own roster when it moves in, and the Throne has no trickle cast of its own -
		// its minions are the Court's and the bridge's, sent by ThroneRules (6.3).
		Bridge.Build(),
		Court.Build(),
	};

	public static ChapterBestiary? ForStageId(string stageId)
	{
		if (string.IsNullOrWhiteSpace(stageId))
			return null;
		foreach (ChapterBestiary chapter in All)
		{
			if (chapter.StageId.Equals(stageId, StringComparison.OrdinalIgnoreCase))
				return chapter;
		}
		return null;
	}

	/// <summary>The roster for a stage index, clamped the way <see cref="StageCatalog.GetByIndex"/> clamps.</summary>
	public static ChapterBestiary? ForStageIndex(int stageIndex) =>
		ForStageId(StageCatalog.GetByIndex(stageIndex)?.Id ?? string.Empty);

	/// <summary>Every creature any roster names, once each.</summary>
	public static IReadOnlyList<BestiaryCreature> AllCreatures()
	{
		var seen = new HashSet<BestiaryCreature>();
		var list = new List<BestiaryCreature>();
		foreach (ChapterBestiary chapter in All)
		{
			foreach (BestiaryCreature creature in chapter.NamedCreatures())
			{
				if (seen.Add(creature))
					list.Add(creature);
			}
		}
		return list;
	}

	/// <summary>The creature whose scene is at <paramref name="scenePath"/>, or null if no roster names one.</summary>
	public static BestiaryCreature? CreatureAt(string scenePath)
	{
		if (string.IsNullOrWhiteSpace(scenePath))
			return null;
		foreach (BestiaryCreature creature in AllCreatures())
		{
			if (creature.ScenePath.Equals(scenePath, StringComparison.OrdinalIgnoreCase))
				return creature;
		}
		return null;
	}

	// --- declaring a cast, for the chapter files -------------------------------------------------

	private static BestiaryCreature Creature(string homeStageId, string id, string displayName, string scenePath, string enemyType) =>
		new(id, displayName, homeStageId, scenePath, enemyType);

	private static BestiarySlot Own(BestiaryRole role, BestiaryCreature creature) => BestiarySlot.Own(role, creature);

	private static BestiarySlot Planned(BestiaryRole role, BestiaryCreature creature, BestiaryCreature standIn,
		string statsChange = "") =>
		BestiarySlot.Planned(role, creature, standIn, statsChange);

	private static BestiarySlot Absent(BestiaryRole role, string reason) => BestiarySlot.Absent(role, reason);

	private static BestiaryTableEntry Band(BestiarySlot slot, float share, float healthMultiplier) =>
		new(slot, share, healthMultiplier);
}

/// <summary>
/// One chapter's roster with its scenes loaded, held by Node2DGame for the length of a run, so nothing
/// on the spawn path loads a resource.
/// </summary>
public sealed class LoadedBestiary
{
	private readonly Dictionary<BestiaryRole, PackedScene> byRole = new();
	private readonly Dictionary<BestiaryRole, string> enemyTypeByRole = new();
	private readonly (PackedScene Scene, double UpTo, float Health)[] table;
	private readonly (PackedScene Scene, double UpTo, float Health)[] lateTable;
	private readonly float lateFromPaceMinutes;

	private LoadedBestiary(ChapterBestiary source)
	{
		Source = source;
		var loaded = new Dictionary<string, PackedScene>(StringComparer.OrdinalIgnoreCase);
		foreach (BestiarySlot slot in source.Slots)
		{
			if (slot.IsAbsent || slot.Role == BestiaryRole.Extra || byRole.ContainsKey(slot.Role))
				continue;
			BestiaryCreature creature = slot.Resolved!;
			PackedScene? scene = LoadScene(creature, loaded);
			if (scene == null)
				continue;
			byRole[slot.Role] = scene;
			enemyTypeByRole[slot.Role] = creature.EnemyType;
		}
		table = LoadTable(source.Table, loaded);
		lateTable = LoadTable(source.LateTable, loaded);
		lateFromPaceMinutes = source.LateTableFromPaceMinutes;
		// Loud and at run start, not a silent empty arena at the first spawn tick.
		if (table.Length == 0)
			throw new InvalidOperationException($"StageBestiary: {source.ChapterName} has no spawn table that loads.");
	}

	public ChapterBestiary Source { get; }

	public static LoadedBestiary Load(ChapterBestiary chapter) => new(chapter);

	/// <summary>The scene for a role, or null when the chapter has no creature for it.</summary>
	public PackedScene? SceneFor(BestiaryRole role) => byRole.TryGetValue(role, out PackedScene? scene) ? scene : null;

	/// <summary>The <c>EnemyType</c> of whatever fills <paramref name="role"/> today, or empty.</summary>
	public string EnemyTypeFor(BestiaryRole role) => enemyTypeByRole.TryGetValue(role, out string? type) ? type : string.Empty;

	/// <summary>One roll of the chapter's table: the body and its health multiplier.</summary>
	public (PackedScene Scene, float HealthMultiplier) PickFromTable(float roll, float paceMinutes)
	{
		var bands = lateTable.Length > 0 && paceMinutes >= lateFromPaceMinutes ? lateTable : table;
		foreach (var band in bands)
		{
			if (roll < band.UpTo)
				return (band.Scene, band.Health);
		}
		var last = bands[bands.Length - 1];
		return (last.Scene, last.Health);
	}

	private static (PackedScene Scene, double UpTo, float Health)[] LoadTable(
		IReadOnlyList<BestiaryTableEntry> entries, Dictionary<string, PackedScene> loaded)
	{
		var bands = new List<(PackedScene, double, float)>();
		double upTo = 0.0;
		foreach (BestiaryTableEntry entry in entries)
		{
			upTo += entry.Share;
			BestiaryCreature? creature = entry.Slot.Resolved;
			PackedScene? scene = creature != null ? LoadScene(creature, loaded) : null;
			// A band whose scene will not load is left out rather than spawning nothing; the error says
			// which, and ValidateBestiary has already said so at startup.
			if (scene != null)
				bands.Add((scene, upTo, entry.HealthMultiplier));
		}
		return bands.ToArray();
	}

	private static PackedScene? LoadScene(BestiaryCreature creature, Dictionary<string, PackedScene> loaded)
	{
		if (loaded.TryGetValue(creature.ScenePath, out PackedScene? cached))
			return cached;
		PackedScene? scene = ResourceLoader.Exists(creature.ScenePath)
			? ResourceLoader.Load<PackedScene>(creature.ScenePath)
			: null;
		if (scene == null)
		{
			GD.PushError($"StageBestiary: {creature} has no loadable scene at '{creature.ScenePath}', so it never spawns.");
			return null;
		}
		loaded[creature.ScenePath] = scene;
		return scene;
	}
}
