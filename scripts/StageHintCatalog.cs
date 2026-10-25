using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

/// <summary>How a hint's caption is set: a person's words, or the sound something makes.</summary>
public enum HintVoice
{
	/// <summary>Someone held there, speaking. Set in quotation marks. Never names them.</summary>
	Speech,
	/// <summary>A sound with nobody behind it - leaves, a bell. Set plainly, as a caption of the sound.</summary>
	Sound,
}

/// <summary>One chapter's hint: what it is for, how it sounds, and what its caption says.</summary>
public sealed class StageHint
{
	/// <summary>The chapter (<see cref="StageCatalog"/>) whose runs play it.</summary>
	public string StageId { get; init; } = string.Empty;

	/// <summary>
	/// The Discovery site it is about (<see cref="UnlockCatalog"/> <c>SourceId</c>). It plays only
	/// while something that site gives is still unowned on this save, and comes from that site's
	/// place in the world - today the chapter's side event (SideEventDirector.ForStage).
	/// </summary>
	public string SiteId { get; init; } = string.Empty;

	public HintVoice Voice { get; init; }

	/// <summary>The cue, by <see cref="SfxCatalog"/> name. Generated in tools/audio/sfx.py.</summary>
	public string Sound { get; init; } = string.Empty;

	/// <summary>
	/// One line per hint, in order; once they run out the last one repeats. Short enough for two
	/// lines on a phone (<see cref="StageHintCatalog.MaxLineLength"/>). Speech without its quotation
	/// marks - the caption adds them.
	/// </summary>
	public string[] Lines { get; init; } = Array.Empty<string>();
}

/// <summary>
/// The single authority on chapter hints (.ai/side-events.md, "Hints that something is out there").
/// </summary>
/// <remarks>
/// <para>Reported: "give hints part way through the runs of the unlockables on maps, such as a woman
/// yelling for help on the cursed dungeon map so the player identify that there are secrets to find".
/// Every chapter with a side event hides it four to six chunks out and announces nothing until the
/// player arrives (SideEvent), which is right for the hunt and wrong for a player who does not know
/// there is anything to hunt. So, a third of the way into a run and every two and a half minutes
/// after, while the chapter still holds something this save has not carried out, a cue comes from
/// its direction: a sound panned that way and a caption at that edge of the screen.</para>
///
/// <para>It says that something is there and roughly where. It never says what to do about it, and
/// it never names who: a rescued wizard's name is the reward (side-events.md, "Secret wizards").
/// It stops for good once the place is found - seen, or revealed by the chapter's wayfinder - since
/// from then on the point-of-interest arrow says the same thing better, and it never plays once the
/// discovery is owned, during the boss, or in the last pace minute and a half before it.</para>
///
/// <para>To add a chapter's: one entry below, keyed by stage id, naming the Discovery site its side
/// event pays out, a sound (tools/audio/sfx.py, then <see cref="SfxCatalog"/>), and its lines.
/// <c>RegressionChecks.ValidateStageHints</c> checks every one of those against the catalogs that
/// own them. A hint about something that is not a side event (a stage secret, once they exist)
/// needs StageHintDirector taught where that thing is; nothing else changes.</para>
/// </remarks>
public static class StageHintCatalog
{
	// When, on the pace clock (Node2DGame.PaceSeconds) - positions on the reference 15-minute run, so
	// a shorter run hears them proportionally earlier. A third of the way in, when the build has its
	// shape and a detour is affordable; then every two and a half minutes; and none from 13:30 on,
	// when there is no longer time to walk there and back before the boss.
	public const float FirstPaceSeconds = 300f;
	public const float RepeatPaceSeconds = 150f;
	public const float LastPaceSeconds = 810f;

	/// <summary>The longest a line may be: two lines of the Body role in the caption on a phone.</summary>
	public const int MaxLineLength = 60;

	private static readonly StageHint[] Hints =
	{
		// The grove is not a person, so nothing speaks: the leaves do.
		new()
		{
			StageId = "stage_0",
			SiteId = "the_grove",
			Voice = HintVoice.Sound,
			Sound = SfxCatalog.HintGroveWhisper,
			Lines = new[]
			{
				"Leaves whispering, though there is no wind.",
				"The whisper again. Something out there still grows.",
				"Fainter now, the whispering. It has not stopped.",
			},
		},
		// Tituba, in a cell. Taken from Barbados and jailed on an accusation (side-events.md): a
		// prisoner who has been patient for a long time, not a witch. Her name is never said.
		new()
		{
			StageId = "stage_1",
			SiteId = "gaolers_key",
			Voice = HintVoice.Speech,
			Sound = SfxCatalog.HintCellCry,
			Lines = new[]
			{
				"Help! Is someone there?",
				"Please - I can hear you. I am down here!",
				"I have been patient so long. Please, this way.",
				"Do not leave me here. Not again.",
			},
		},
		// His lanterns. "Nothing living lights them" - so the caption never says who tends them.
		new()
		{
			StageId = "stage_3",
			SiteId = "bog_lanterns",
			Voice = HintVoice.Sound,
			Sound = SfxCatalog.HintLanternFlare,
			Lines = new[]
			{
				"Far off, a cold light flares and gutters.",
				"The cold light again. Nothing living lit it.",
				"It is still burning out there.",
			},
		},
		// Vainamoinen, the singer, under the ice. He is heard, not named.
		new()
		{
			StageId = "stage_5",
			SiteId = "the_thaw",
			Voice = HintVoice.Sound,
			Sound = SfxCatalog.HintIceSong,
			Lines = new[]
			{
				"The ice groans. Under it, faintly, someone is singing.",
				"The song again, slow and deep, from under the ice.",
				"The singing is weaker now.",
			},
		},
		// The caravan master on the trade road. The bell walks; it does not wait.
		new()
		{
			StageId = "stage_6",
			SiteId = "caravan_master",
			Voice = HintVoice.Sound,
			Sound = SfxCatalog.HintCaravanBell,
			Lines = new[]
			{
				"A caravan bell, somewhere out on the sand.",
				"The bell again. Something walks the old road.",
				"The bell is still moving. It does not stop.",
			},
		},
	};

	public static IReadOnlyList<StageHint> All => Hints;

	/// <summary>The chapter's hint, or null if it has none.</summary>
	public static StageHint ForStage(string stageId)
	{
		foreach (StageHint hint in Hints)
		{
			if (string.Equals(hint.StageId, stageId, StringComparison.Ordinal))
				return hint;
		}
		return null;
	}

	/// <summary>The pace second at which hint number <paramref name="index"/> (0-based) falls due.</summary>
	public static float DuePaceSeconds(int index) => FirstPaceSeconds + Math.Max(0, index) * RepeatPaceSeconds;

	/// <summary>The line for hint number <paramref name="index"/>: in order, the last repeating.</summary>
	public static string LineFor(StageHint hint, int index)
	{
		if (hint == null || hint.Lines.Length == 0)
			return string.Empty;
		return hint.Lines[Math.Clamp(index, 0, hint.Lines.Length - 1)];
	}
}
