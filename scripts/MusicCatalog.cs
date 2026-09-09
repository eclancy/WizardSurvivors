namespace WizardSurvivors.scripts;

// Which track plays during a run, and the one place that decision is made.
//
// Every chapter shares one gameplay loop today. The plan is a track per chapter, so this is a
// table keyed by stage index rather than a constant buried in Node2DGame: giving chapter 3 its
// own music becomes one row here instead of a conditional at the call site, which is the same
// argument that put the cast cue in Player's firing loop rather than in twenty spell scripts.
//
// A note for whoever adds the second track. StageCatalog is the single authority on what a
// stage IS - its name, terrain, gate and flavour - and a per-chapter music path arguably belongs
// on StageDefinition next to those. It is not there yet only because StageCatalog was still
// uncommitted when this landed. Folding this table into it later is a good change; having two
// disagreeing stage rosters is not, so move it rather than copying it.
public static class MusicCatalog
{
	public const string Directory = "res://assets/music/";

	/// <summary>
	/// The gameplay loop every chapter uses until it is given its own.
	/// Source: good_day_story - "Labyrinth Escape" (333453). See .ai/audio-manifest.md for the
	/// attribution note; the file was renamed on the way in, so that doc is the only record of
	/// where it came from.
	/// </summary>
	public const string DefaultRunTrack = Directory + "labyrinth-escape.mp3";

	/// <summary>The menu and title loop. Unchanged - listed here so both are visible together.</summary>
	public const string MenuTrack = "res://assets/Pixel_Knights.mp3";

	// Stage index to track. Only chapters that differ from DefaultRunTrack need a row, so this
	// is empty on purpose: an empty table means every chapter shares one loop, which is the
	// truth today and is easier to read than eight identical rows.
	private static readonly System.Collections.Generic.Dictionary<int, string> PerStage = new();

	/// <summary>
	/// The track for a chapter. Falls back to the shared loop for any index without a row, so a
	/// new chapter always has music rather than silence - the failure mode that would otherwise
	/// go unnoticed until someone played it.
	/// </summary>
	public static string RunTrackForStage(int stageIndex)
	{
		return PerStage.TryGetValue(stageIndex, out string path) ? path : DefaultRunTrack;
	}

	/// <summary>Every track the game can play, for validation at startup.</summary>
	public static string[] AllTracks()
	{
		var all = new System.Collections.Generic.List<string> { DefaultRunTrack, MenuTrack };
		foreach (string path in PerStage.Values)
		{
			if (!all.Contains(path))
				all.Add(path);
		}
		return all.ToArray();
	}
}
