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

	/// <summary>Always playing while the set is. Alone, it is the menu.</summary>
	public string Bed { get; }

	/// <summary>Harp, bass and drum. Faded in by enemy pressure.</summary>
	public string Pulse { get; }

	/// <summary>The tune. Faded in for elites and the late run.</summary>
	public string Lead { get; }

	/// <summary>A separate loop, not a stem: swapped to when the chapter's boss walks in.</summary>
	public string Boss { get; }

	public string[] Stems => new[] { Bed, Pulse, Lead };
}

// Which music plays where, and the one place that decision is made.
//
// A note for whoever adds the second set. StageCatalog is the single authority on what a stage
// IS - its name, terrain, gate and flavour - and a per-chapter music set arguably belongs on
// StageDefinition next to those. Folding this table into it is a good change; having two
// disagreeing stage rosters is not, so move it rather than copying it.
public static class MusicCatalog
{
	public const string Directory = "res://assets/music/";

	/// <summary>
	/// Every boss fight's loop until a chapter has its own: A minor, 138 BPM, 42 s.
	/// </summary>
	public const string SharedBossLoop = Directory + "boss_loop.wav";

	/// <summary>
	/// Chapter 1, the Enchanted Forest. D Dorian, 6/8, 60 s - generated, so ours outright.
	/// </summary>
	/// <remarks>
	/// The two tracks that used to live in assets/ were removed before the repo was made public,
	/// because neither licence could be shown (see .ai/asset-licensing.md). Everything here is
	/// rendered by tools/audio/music.py; edit the piece there and re-run, never the WAV.
	/// </remarks>
	public static readonly MusicSet Forest = new(
		"forest",
		Directory + "forest_bed.wav",
		Directory + "forest_pulse.wav",
		Directory + "forest_lead.wav",
		SharedBossLoop);

	/// <summary>
	/// The set any chapter without its own row plays. The forest is the only set so far, and a
	/// chapter in the wrong key is a better failure than a chapter in silence - the silent one
	/// goes unnoticed until someone plays it. The per-chapter plan is in audio-direction.md 11.
	/// </summary>
	public static readonly MusicSet DefaultRunSet = Forest;

	/// <summary>
	/// The title and menus: the forest set with only its bed audible. The SAME set, not a copy,
	/// so starting the first chapter from the menu brings the pulse and the tune in over a bed
	/// that never stopped.
	/// </summary>
	public static readonly MusicSet MenuSet = Forest;

	// Stage index to set. Only chapters that differ from DefaultRunSet need a row.
	private static readonly System.Collections.Generic.Dictionary<int, MusicSet> PerStage = new()
	{
		[0] = Forest,
	};

	/// <summary>
	/// The set for a chapter. Falls back to the shared default for any index without a row.
	/// </summary>
	public static MusicSet RunSetForStage(int stageIndex)
	{
		return PerStage.TryGetValue(stageIndex, out MusicSet set) ? set : DefaultRunSet;
	}

	/// <summary>Every file the game can play, for validation at startup.</summary>
	public static string[] AllTracks()
	{
		var all = new System.Collections.Generic.List<string>();
		void Add(MusicSet set)
		{
			foreach (string path in set.Stems)
			{
				if (!all.Contains(path))
					all.Add(path);
			}
			if (!all.Contains(set.Boss))
				all.Add(set.Boss);
		}
		Add(DefaultRunSet);
		Add(MenuSet);
		foreach (MusicSet set in PerStage.Values)
			Add(set);
		return all.ToArray();
	}
}
