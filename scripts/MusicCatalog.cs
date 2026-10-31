namespace WizardSurvivors.scripts;

/// <summary>
/// One chapter's music: three stems of the same loop, plus the loop the boss fight swaps to.
/// </summary>
/// <remarks>
/// The stems are rendered by <c>tools/audio/music.py</c> to the same sample count, tempo and key,
/// and are played locked together by <see cref="MusicPlayer"/> through an
/// <c>AudioStreamSynchronized</c>, which is what lets the run fade one in and out without it ever
/// drifting off the beat. Bed plays always; pulse joins when the arena fills; lead joins for an
/// elite and for the last third of the run. See <c>.ai/audio-direction.md</c> section 11.
/// </remarks>
public sealed class MusicSet
{
	public MusicSet(string id, string bed, string pulse, string lead, string boss, MusicSet standIn = null)
	{
		Id = id;
		Bed = bed;
		Pulse = pulse;
		Lead = lead;
		Boss = boss;
		StandIn = standIn;
	}

	/// <summary>
	/// What plays in its place until this set's own stems are rendered and imported - another
	/// chapter's set. Null for a set that shipped with its chapter. A planned set switches itself on
	/// when its bed exists, so scoring a new chapter (tools/audio/music_&lt;chapter&gt;.py) is rendering
	/// its files and nothing else (.ai/campaign.md).
	/// </summary>
	public MusicSet StandIn { get; }

	/// <summary>True once the set's bed stem is on disk and imported.</summary>
	public bool IsRendered => Godot.ResourceLoader.Exists(Bed);

	/// <summary>The set that actually plays: this one, or its stand-in while this one is unrendered.</summary>
	public MusicSet Resolved => StandIn != null && !IsRendered ? StandIn.Resolved : this;

	public string Id { get; }

	/// <summary>Held chords and the ostinato under them. Always playing while the set is.</summary>
	public string Bed { get; }

	/// <summary>Drums and the driving bass. Faded in by enemy pressure.</summary>
	public string Pulse { get; }

	/// <summary>The hook. Faded in for elites and the late run.</summary>
	public string Lead { get; }

	/// <summary>A separate loop, not a stem: swapped to when the chapter's boss walks in.</summary>
	public string Boss { get; }

	public string[] Stems => new[] { Bed, Pulse, Lead };
}

// Which music plays where, and the one place that decision is made.
//
// A note for whoever folds this into StageCatalog. StageCatalog is the single authority on what
// a stage IS - its name, terrain, gate and flavour - and a chapter's music set arguably belongs on
// StageDefinition next to those. Folding this table into it is a good change; having two
// disagreeing stage rosters is not, so move it rather than copying it. Until then
// RegressionChecks.ValidateMusic holds the two to each other: every chapter in StageCatalog must
// have a row here, and no row may name a chapter StageCatalog does not have.
public static class MusicCatalog
{
	public const string Directory = "res://assets/music/";

	/// <summary>
	/// Every boss fight's loop until a chapter has its own: A minor, 138 BPM, 42 s.
	/// </summary>
	public const string SharedBossLoop = Directory + "boss_loop.wav";

	/// <summary>
	/// The title and every menu: one loop, A Aeolian at 72, 27 s - calm, dark, slower than
	/// anything in a chapter, and no chapter's set. MusicPlayer plays it as a solo loop, so
	/// starting a run crossfades it out under the chapter's set rather than cutting it.
	/// </summary>
	public const string MenuTheme = Directory + "menu_theme.wav";

	/// <remarks>
	/// Every set is rendered by tools/audio/music.py; edit the piece there and re-run, never the
	/// WAV. The two stock tracks that used to live in assets/ were removed before the repo was
	/// made public, because neither licence could be shown (see .ai/asset-licensing.md).
	/// </remarks>
	private static MusicSet Chapter(string id) => new(
		id,
		Directory + id + "_bed.wav",
		Directory + id + "_pulse.wav",
		Directory + id + "_lead.wav",
		SharedBossLoop);

	/// <summary>The Enchanted Forest. D Dorian, 6/8 at dotted quarter 112, 25.7 s.</summary>
	public static readonly MusicSet Forest = Chapter("forest");

	/// <summary>The Cursed Dungeon. B Locrian over a B drone, 4/4 at 132, 29.1 s.</summary>
	public static readonly MusicSet Dungeon = Chapter("dungeon");

	/// <summary>The Sunken Cave. A Aeolian, 3/4 at 160, 27.0 s.</summary>
	public static readonly MusicSet Cave = Chapter("cave");

	/// <summary>The Blighted Swamp. E Phrygian, 7/8 (2+2+3) at eighth 280, 24.0 s.</summary>
	public static readonly MusicSet Swamp = Chapter("swamp");

	/// <summary>The Mystic Ruins. F Lydian, 4/4 at 128, 30.0 s.</summary>
	public static readonly MusicSet Ruins = Chapter("ruins");

	/// <summary>The Frozen Waste. C Ionian in open fifths, 4/4 at 144 in half time, 26.7 s.</summary>
	public static readonly MusicSet Frozen = Chapter("frozen");

	/// <summary>The Scorched Sands. G Mixolydian, 6/8 at dotted quarter 104, 27.7 s.</summary>
	public static readonly MusicSet Sands = Chapter("sands");

	/// <summary>The Emberdeep, the finale. A minor with the Neapolitan B flat and E major, 4/4 at 150, 25.6 s.</summary>
	public static readonly MusicSet Emberdeep = Chapter("emberdeep");

	/// <summary>The Sketchbook. C major, a toy waltz in 3/4 at 168, 25.7 s.</summary>
	public static readonly MusicSet Sketchbook = Chapter("sketchbook");

	// THE TWO EARLY CHAPTERS (.ai/campaign.md), planned: each names the files it will have and plays
	// a stand-in until they exist. Phase 2 scores them in tools/audio/music_fields.py and
	// music_heath.py and never edits this file.

	/// <summary>The Fallow Fields, planned: G Mixolydian, a 9/8 slip jig. The Forest's set until rendered.</summary>
	public static readonly MusicSet Fields = PlannedChapter("fields", Forest);

	/// <summary>The Broken Circle, planned: D Dorian in 5/4. The Ruins' set until rendered.</summary>
	public static readonly MusicSet Heath = PlannedChapter("heath", Ruins);

	// THE CASTLE (.ai/endgame-castle.md, piece C7; .ai/audio-manifest.md, "The castle's music"). Planned
	// sets with the Emberdeep's as their stand-in. All three are rendered and imported already, so each
	// plays its own the moment its row is here. The Cursed Dungeon keeps its own set when it moves in.
	// The Throne's is never handed over to the boss loop (the whole chapter is the fight): that is
	// ThroneRules' to arrange, not this table's - its Boss stays the shared loop for ValidateMusic.

	/// <summary>The Last Bridge: a march under wind. A Aeolian, 2/4 at 120, 24.0 s.</summary>
	public static readonly MusicSet Bridge = PlannedChapter("bridge", Emberdeep);

	/// <summary>The Court: a court dance with no dancers. An A minor minuet in 3/4 at 112, 25.7 s.</summary>
	public static readonly MusicSet Court = PlannedChapter("court", Emberdeep);

	/// <summary>The Throne: the heaviest piece. A minor with B flat and G sharp, 4/4 at 104 in half time, 27.7 s.</summary>
	public static readonly MusicSet Throne = PlannedChapter("throne", Emberdeep);

	/// <summary>
	/// The set any chapter without its own row plays. Every chapter has one today; the fallback
	/// stays because a chapter in the wrong key is a better failure than a chapter in silence -
	/// the silent one goes unnoticed until someone plays it - and ValidateMusic names the gap.
	/// </summary>
	public static readonly MusicSet DefaultRunSet = Forest;

	// Stage index (StageCatalog) to set.
	private static readonly System.Collections.Generic.Dictionary<int, MusicSet> PerStage = new()
	{
		[0] = Forest,
		[1] = Dungeon,
		[2] = Cave,
		[3] = Swamp,
		[4] = Ruins,
		[5] = Frozen,
		[6] = Sands,
		[7] = Emberdeep,
		[8] = Sketchbook,
		[9] = Fields,
		[10] = Heath,
		[11] = Bridge,
		[12] = Court,
		[13] = Throne,
	};

	private static MusicSet PlannedChapter(string id, MusicSet standIn) => new(
		id,
		Directory + id + "_bed.wav",
		Directory + id + "_pulse.wav",
		Directory + id + "_lead.wav",
		SharedBossLoop,
		standIn);

	/// <summary>
	/// The set for a chapter. Falls back to the shared default for any index without a row.
	/// </summary>
	public static MusicSet RunSetForStage(int stageIndex)
	{
		return PerStage.TryGetValue(stageIndex, out MusicSet set) ? set.Resolved : DefaultRunSet;
	}

	/// <summary>The chapter's own set, rendered or not - null without a row. For the Phase 2 to-do list.</summary>
	public static MusicSet OwnSetForStage(int stageIndex) =>
		PerStage.TryGetValue(stageIndex, out MusicSet set) ? set : null;

	/// <summary>Whether a chapter has its own row, rather than falling back. For validation.</summary>
	public static bool HasSetForStage(int stageIndex) => PerStage.ContainsKey(stageIndex);

	/// <summary>The stage indices with a row, for validation against StageCatalog.</summary>
	public static System.Collections.Generic.IEnumerable<int> StageIndices => PerStage.Keys;

	/// <summary>Every distinct set that can play, default first. A planned set counts once it is rendered.</summary>
	public static MusicSet[] AllSets()
	{
		var all = new System.Collections.Generic.List<MusicSet> { DefaultRunSet };
		foreach (MusicSet set in PerStage.Values)
		{
			if (!all.Contains(set.Resolved))
				all.Add(set.Resolved);
		}
		return all.ToArray();
	}

	/// <summary>Every file the game can play, for validation at startup.</summary>
	public static string[] AllTracks()
	{
		var all = new System.Collections.Generic.List<string> { MenuTheme };
		foreach (MusicSet set in AllSets())
		{
			foreach (string path in set.Stems)
			{
				if (!all.Contains(path))
					all.Add(path);
			}
			if (!all.Contains(set.Boss))
				all.Add(set.Boss);
		}
		return all.ToArray();
	}
}
