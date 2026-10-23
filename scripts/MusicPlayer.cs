using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// The music autoload. Three players on the Music bus, and nothing else ever plays music:
//
//   * the SET player plays a chapter's three stems (MusicCatalog.MusicSet) through one
//     AudioStreamSynchronized, so they are sample-locked by the engine rather than by three
//     players that happened to start on the same frame. Each stem's level is a fader here; the
//     run raises and lowers them by how hard it is pressing (Node2DGame.UpdateRunMusic).
//   * two SOLO players play one loop each - the menu theme, or the boss fight - and crossfade
//     against the set. There are two so that one solo loop can hand over to another: quitting
//     from a boss fight to the title fades the boss loop out on one player while the theme fades
//     in on the other, where a single player would have to cut one to start the other.
//
// Every level is a fade, never a jump, and the fades run in REAL time with ProcessMode Always,
// so the music keeps moving under a paused level-up screen and under the game-over overlay.
// The stems are normalised in tools/audio/music.py to the level they are heard at; a fader at 1
// plays a stem at unity, and the player's Music slider is the bus volume (AudioSettings), which
// this class never touches. See .ai/audio-direction.md section 11.
public partial class MusicPlayer : Node
{
#nullable enable
	public const string MusicBusName = "Music";
	public const string MasterBusName = "Master";

	public const int BedLayer = 0;
	public const int PulseLayer = 1;
	public const int LeadLayer = 2;
	private const int LayerCount = 3;

	/// <summary>A layer fader at 0 is this, not negative infinity, which a stream volume takes badly.</summary>
	private const float SilentDb = -80f;

	// Fade times, in real seconds - they describe how a change feels, so they do not follow the
	// run's pace clock. A layer arrives faster than it leaves: the swarm swelling should be heard
	// at once, and the music thinning out the moment it eases would make every lull a gap.
	public const float LayerFadeInSeconds = 4f;
	public const float LayerFadeOutSeconds = 8f;
	public const float RunFadeInSeconds = 3f;
	public const float MenuFadeInSeconds = 2f;
	public const float BossCrossfadeSeconds = 3f;
	public const float EndFadeSeconds = 2.5f;

	private AudioStreamPlayer? setPlayer;
	private readonly AudioStreamPlayer?[] soloPlayers = new AudioStreamPlayer?[2];
	private readonly Fader[] soloFades = { new(), new() };
	/// <summary>Which solo player holds the loop that is wanted; the other can only be fading out.</summary>
	private int activeSolo;
	private AudioStreamSynchronized? setStream;
	private MusicSet? currentSet;
	private readonly Dictionary<string, AudioStreamSynchronized> builtSets = new();

	private readonly float[] layerLevel = new float[LayerCount];
	private readonly float[] layerTarget = new float[LayerCount];
	private readonly float[] layerAppliedDb = new float[LayerCount];
	private readonly Fader setFade = new();

	/// <summary>The set playing (or fading), if any. Read by the startup log and nothing else.</summary>
	public string CurrentSetId => currentSet?.Id ?? string.Empty;

	public static string ResolveMusicBusName()
	{
		return AudioServer.GetBusIndex(MusicBusName) >= 0 ? MusicBusName : MasterBusName;
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		setPlayer = CreatePlayer("SetPlayer");
		soloPlayers[0] = CreatePlayer("SoloPlayerA");
		soloPlayers[1] = CreatePlayer("SoloPlayerB");
		for (int i = 0; i < LayerCount; i++)
			layerAppliedDb[i] = float.NaN;
		layerTarget[BedLayer] = 1f;
		GD.Print($"MusicPlayer: ready on bus '{ResolveMusicBusName()}'");
	}

	private AudioStreamPlayer CreatePlayer(string name)
	{
		var player = new AudioStreamPlayer
		{
			Name = name,
			Bus = ResolveMusicBusName(),
			VolumeDb = SilentDb,
			ProcessMode = ProcessModeEnum.Always,
		};
		AddChild(player);
		return player;
	}

	/// <summary>
	/// Play a chapter's set, fading any solo loop (the menu theme) out over the same time. If that
	/// set is already playing it is NOT restarted - it fades back up from wherever it is - so a
	/// restart, or a quick return to the same chapter, never jumps back to bar one.
	/// </summary>
	/// <param name="pulse">Starting target for the pulse stem, 0..1.</param>
	/// <param name="lead">Starting target for the lead stem, 0..1.</param>
	public void PlaySet(MusicSet set, float fadeInSeconds, float pulse = 0f, float lead = 0f)
	{
		if (setPlayer == null)
			return;

		SetLayerTargets(pulse, lead);
		FadeSolos(Mathf.Max(fadeInSeconds, 0.5f));

		if (currentSet == set && setPlayer.Playing)
		{
			setFade.To(1f, fadeInSeconds);
			return;
		}

		AudioStreamSynchronized? stream = BuildSet(set);
		if (stream == null)
			return;

		currentSet = set;
		setStream = stream;
		// A fresh start begins with every layer already at its target; the set's own fade-in is
		// the only fade the player hears.
		for (int i = 0; i < LayerCount; i++)
		{
			layerLevel[i] = layerTarget[i];
			layerAppliedDb[i] = float.NaN;
		}
		ApplyLayerVolumes();
		setPlayer.Stream = stream;
		setFade.Level = 0f;
		setFade.To(1f, fadeInSeconds);
		setPlayer.VolumeDb = FaderDb(setFade.Level);
		setPlayer.Play();
		GD.Print($"MusicPlayer: set '{set.Id}' started, {LayerCount} stems locked, pulse {layerTarget[PulseLayer]:0.##} lead {layerTarget[LeadLayer]:0.##}, fading in over {fadeInSeconds:0.#} s");
	}

	/// <summary>The run's intensity, as two fader targets. The bed is always at 1.</summary>
	public void SetLayerTargets(float pulse, float lead)
	{
		pulse = Mathf.Clamp(pulse, 0f, 1f);
		lead = Mathf.Clamp(lead, 0f, 1f);
		// One line per change of state - a handful a run - so a headless log shows the music
		// following the run without anyone having to hear it.
		if (setPlayer != null && setPlayer.Playing
			&& (!Mathf.IsEqualApprox(pulse, layerTarget[PulseLayer]) || !Mathf.IsEqualApprox(lead, layerTarget[LeadLayer])))
		{
			GD.Print($"MusicPlayer: '{CurrentSetId}' pulse {pulse:0.##} lead {lead:0.##}");
		}
		layerTarget[BedLayer] = 1f;
		layerTarget[PulseLayer] = pulse;
		layerTarget[LeadLayer] = lead;
	}

	/// <summary>
	/// Crossfade from whatever is playing to a single loop - the menu theme or the boss. The set
	/// keeps running silently underneath until its fade completes, then stops. A different solo
	/// loop still audible is handed to the other solo player and faded out there, never cut.
	/// </summary>
	public void PlaySolo(AudioStream stream, float crossfadeSeconds)
	{
		AudioStreamPlayer? player = soloPlayers[activeSolo];
		if (player == null)
			return;
		if (player.Stream != stream || !player.Playing)
		{
			if (player.Playing && soloFades[activeSolo].Level > 0f)
			{
				soloFades[activeSolo].To(0f, Mathf.Max(crossfadeSeconds, 0.5f));
				activeSolo = 1 - activeSolo;
				player = soloPlayers[activeSolo]!;
			}
			player.Stop();
			player.Stream = stream;
			Fader fade = soloFades[activeSolo];
			fade.Level = crossfadeSeconds > 0f ? 0f : 1f;
			player.VolumeDb = FaderDb(fade.Level);
			player.Play();
			GD.Print($"MusicPlayer: solo loop {stream.ResourcePath} in over {crossfadeSeconds:0.#} s, set '{CurrentSetId}' out");
		}
		soloFades[activeSolo].To(1f, crossfadeSeconds);
		setFade.To(0f, crossfadeSeconds);
	}

	/// <summary>Fade everything out and stop. The run's end, win or lose.</summary>
	public void FadeOutAll(float seconds)
	{
		setFade.To(0f, seconds);
		FadeSolos(seconds);
	}

	private void FadeSolos(float seconds)
	{
		foreach (Fader fade in soloFades)
			fade.To(0f, seconds);
	}

	/// <summary>Kept for any caller that wants one plain track. Plays it as the solo loop.</summary>
	public void PlayMusic(AudioStream stream, bool restart = false)
	{
		if (restart)
			soloPlayers[activeSolo]?.Stop();
		PlaySolo(stream, 0f);
	}

	public void StopMusic()
	{
		setPlayer?.Stop();
		setFade.Level = setFade.Target = 0f;
		for (int i = 0; i < soloPlayers.Length; i++)
		{
			soloPlayers[i]?.Stop();
			soloFades[i].Level = soloFades[i].Target = 0f;
		}
	}

	public void PauseMusic() => SetPaused(true);
	public void ResumeMusic() => SetPaused(false);

	private void SetPaused(bool paused)
	{
		if (setPlayer != null)
			setPlayer.StreamPaused = paused;
		foreach (AudioStreamPlayer? solo in soloPlayers)
		{
			if (solo != null)
				solo.StreamPaused = paused;
		}
	}

	/// <summary>
	/// The title and every menu: the menu theme, as a solo loop. Idempotent - the title starts it
	/// and the menu asks again, and a theme already playing is faded back up, never restarted.
	/// </summary>
	public void PlayMenuMusic()
	{
		var theme = ResourceLoader.Load<AudioStream>(MusicCatalog.MenuTheme);
		if (theme == null)
		{
			// RegressionChecks.ValidateMusic reports this at startup too.
			GD.PushWarning($"MusicPlayer: menu theme missing at {MusicCatalog.MenuTheme}. Run: python tools/audio/music.py menu");
			return;
		}
		PlaySolo(theme, MenuFadeInSeconds);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		for (int i = 0; i < LayerCount; i++)
		{
			float rate = layerTarget[i] > layerLevel[i] ? 1f / LayerFadeInSeconds : 1f / LayerFadeOutSeconds;
			layerLevel[i] = Mathf.MoveToward(layerLevel[i], layerTarget[i], rate * dt);
		}
		ApplyLayerVolumes();

		if (setPlayer != null && setFade.Step(dt))
		{
			setPlayer.VolumeDb = FaderDb(setFade.Level);
			if (setFade.Level <= 0f && setFade.Target <= 0f && setPlayer.Playing)
				setPlayer.Stop();
		}
		for (int i = 0; i < soloPlayers.Length; i++)
		{
			AudioStreamPlayer? solo = soloPlayers[i];
			Fader fade = soloFades[i];
			if (solo == null || !fade.Step(dt))
				continue;
			solo.VolumeDb = FaderDb(fade.Level);
			if (fade.Level <= 0f && fade.Target <= 0f && solo.Playing)
				solo.Stop();
		}
	}

	private void ApplyLayerVolumes()
	{
		if (setStream == null)
			return;
		for (int i = 0; i < LayerCount; i++)
		{
			float db = FaderDb(layerLevel[i]);
			// Only on a change: the stream reads these every mix, and an unchanged write is noise.
			if (Mathf.IsEqualApprox(db, layerAppliedDb[i]))
				continue;
			setStream.SetSyncStreamVolume(i, db);
			layerAppliedDb[i] = db;
		}
	}

	/// <summary>
	/// A fader position to decibels on an equal-power curve. Two uncorrelated layers crossed on a
	/// linear-amplitude fade dip about 3 dB in the middle; on sin(x * pi/2) they do not.
	/// </summary>
	private static float FaderDb(float level)
	{
		if (level <= 0.0005f)
			return SilentDb;
		float gain = Mathf.Sin(Mathf.Clamp(level, 0f, 1f) * Mathf.Pi * 0.5f);
		return Mathf.Max(SilentDb, Mathf.LinearToDb(gain));
	}

	private AudioStreamSynchronized? BuildSet(MusicSet set)
	{
		if (builtSets.TryGetValue(set.Id, out AudioStreamSynchronized? cached))
			return cached;

		string[] stems = set.Stems;
		var sync = new AudioStreamSynchronized { StreamCount = stems.Length };
		for (int i = 0; i < stems.Length; i++)
		{
			var stream = ResourceLoader.Load<AudioStream>(stems[i]);
			if (stream == null)
			{
				// RegressionChecks.ValidateMusic reports this at startup too; this is the line
				// that says which run went silent and why.
				GD.PushWarning($"MusicPlayer: stem missing at {stems[i]}; set '{set.Id}' will not play. Run: python tools/audio/music.py");
				return null;
			}
			sync.SetSyncStream(i, stream);
			sync.SetSyncStreamVolume(i, SilentDb);
		}
		builtSets[set.Id] = sync;
		return sync;
	}

	/// <summary>A level that moves toward a target at a fixed rate. Returns true while it moves.</summary>
	private sealed class Fader
	{
		public float Level;
		public float Target;
		private float rate = 1f;
		private bool dirty = true;

		public void To(float target, float seconds)
		{
			Target = Mathf.Clamp(target, 0f, 1f);
			if (seconds <= 0f)
			{
				Level = Target;
				rate = 1f;
			}
			else
			{
				rate = 1f / seconds;
			}
			dirty = true;
		}

		public bool Step(float dt)
		{
			if (!dirty && Mathf.IsEqualApprox(Level, Target))
				return false;
			Level = Mathf.MoveToward(Level, Target, rate * dt);
			dirty = !Mathf.IsEqualApprox(Level, Target);
			return true;
		}
	}
}
