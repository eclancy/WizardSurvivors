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
	public MusicSet(string id, string bed, string pulse, string lead, string boss)
	{
		Id = id;
		Bed = bed;
		Pulse = pulse;
		Lead = lead;
		Boss = boss;
	}

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

	/// <summary>Chapter 1. D Dorian, 6/8 at dotted quarter 112, 25.7 s.</summary>
	public static readonly MusicSet Forest = Chapter("forest");

	/// <summary>Chapter 2. B Locrian over a B drone, 4/4 at 132, 29.1 s.</summary>
	public static readonly MusicSet Dungeon = Chapter("dungeon");

	/// <summary>Chapter 3. A Aeolian, 3/4 at 160, 27.0 s.</summary>
	public static readonly MusicSet Cave = Chapter("cave");

	/// <summary>Chapter 4. E Phrygian, 7/8 (2+2+3) at eighth 280, 24.0 s.</summary>
	public static readonly MusicSet Swamp = Chapter("swamp");

	/// <summary>Chapter 5. F Lydian, 4/4 at 128, 30.0 s.</summary>
	public static readonly MusicSet Ruins = Chapter("ruins");

	/// <summary>Chapter 6. C Ionian in open fifths, 4/4 at 144 in half time, 26.7 s.</summary>
	public static readonly MusicSet Frozen = Chapter("frozen");

	/// <summary>Chapter 7. G Mixolydian, 6/8 at dotted quarter 104, 27.7 s.</summary>
	public static readonly MusicSet Sands = Chapter("sands");

	/// <summary>Chapter 8, the finale. A minor with the Neapolitan B flat and E major, 4/4 at 150, 25.6 s.</summary>
	public static readonly MusicSet Emberdeep = Chapter("emberdeep");

	/// <summary>The Sketchbook. C major, a toy waltz in 3/4 at 168, 25.7 s.</summary>
	public static readonly MusicSet Sketchbook = Chapter("sketchbook");

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
	};

	/// <summary>
	/// The set for a chapter. Falls back to the shared default for any index without a row.
	/// </summary>
	public static MusicSet RunSetForStage(int stageIndex)
	{
		return PerStage.TryGetValue(stageIndex, out MusicSet set) ? set : DefaultRunSet;
	}

	/// <summary>Whether a chapter has its own row, rather than falling back. For validation.</summary>
	public static bool HasSetForStage(int stageIndex) => PerStage.ContainsKey(stageIndex);

	/// <summary>The stage indices with a row, for validation against StageCatalog.</summary>
	public static System.Collections.Generic.IEnumerable<int> StageIndices => PerStage.Keys;

	/// <summary>Every distinct set, default first.</summary>
	public static MusicSet[] AllSets()
	{
		var all = new System.Collections.Generic.List<MusicSet> { DefaultRunSet };
		foreach (MusicSet set in PerStage.Values)
		{
			if (!all.Contains(set))
				all.Add(set);
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
