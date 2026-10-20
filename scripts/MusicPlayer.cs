using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// The music autoload. Two players on the Music bus, and nothing else ever plays music:
//
//   * the SET player plays a chapter's three stems (MusicCatalog.MusicSet) through one
//     AudioStreamSynchronized, so they are sample-locked by the engine rather than by three
//     players that happened to start on the same frame. Each stem's level is a fader here; the
//     run raises and lowers them by how hard it is pressing (Node2DGame.UpdateRunMusic).
//   * the SOLO player plays one loop - the boss fight - and crossfades against the set.
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
	private AudioStreamPlayer? soloPlayer;
	private AudioStreamSynchronized? setStream;
	private MusicSet? currentSet;
	private readonly Dictionary<string, AudioStreamSynchronized> builtSets = new();

	private readonly float[] layerLevel = new float[LayerCount];
	private readonly float[] layerTarget = new float[LayerCount];
	private readonly float[] layerAppliedDb = new float[LayerCount];
	private readonly Fader setFade = new();
	private readonly Fader soloFade = new();

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
		soloPlayer = CreatePlayer("SoloPlayer");
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
	/// Play a chapter's set. If that set is already playing it is NOT restarted - it fades back up
	/// from wherever it is - which is what carries the menu's bed unbroken into the first chapter.
	/// </summary>
	/// <param name="pulse">Starting target for the pulse stem, 0..1.</param>
	/// <param name="lead">Starting target for the lead stem, 0..1.</param>
	public void PlaySet(MusicSet set, float fadeInSeconds, float pulse = 0f, float lead = 0f)
	{
		if (setPlayer == null)
			return;

		SetLayerTargets(pulse, lead);
		soloFade.To(0f, Mathf.Max(fadeInSeconds, 0.5f));

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
	/// Crossfade from the set to a single loop - the boss. The set keeps running silently
	/// underneath until its fade completes, then stops.
	/// </summary>
	public void PlaySolo(AudioStream stream, float crossfadeSeconds)
	{
		if (soloPlayer == null)
			return;
		if (soloPlayer.Stream != stream || !soloPlayer.Playing)
		{
			soloPlayer.Stop();
			soloPlayer.Stream = stream;
			soloFade.Level = crossfadeSeconds > 0f ? 0f : 1f;
			soloPlayer.VolumeDb = FaderDb(soloFade.Level);
			soloPlayer.Play();
			GD.Print($"MusicPlayer: solo loop {stream.ResourcePath} in over {crossfadeSeconds:0.#} s, set '{CurrentSetId}' out");
		}
		soloFade.To(1f, crossfadeSeconds);
		setFade.To(0f, crossfadeSeconds);
	}

	/// <summary>Fade everything out and stop. The run's end, win or lose.</summary>
	public void FadeOutAll(float seconds)
	{
		setFade.To(0f, seconds);
		soloFade.To(0f, seconds);
	}

	/// <summary>Kept for any caller that wants one plain track. Plays it as the solo loop.</summary>
	public void PlayMusic(AudioStream stream, bool restart = false)
	{
		if (restart)
			soloPlayer?.Stop();
		PlaySolo(stream, 0f);
	}

	public void StopMusic()
	{
		setPlayer?.Stop();
		soloPlayer?.Stop();
		setFade.Level = setFade.Target = 0f;
		soloFade.Level = soloFade.Target = 0f;
	}

	public void PauseMusic() => SetPaused(true);
	public void ResumeMusic() => SetPaused(false);

	private void SetPaused(bool paused)
	{
		if (setPlayer != null)
			setPlayer.StreamPaused = paused;
		if (soloPlayer != null)
			soloPlayer.StreamPaused = paused;
	}

	/// <summary>The title and every menu: the menu set, bed only.</summary>
	public void PlayMenuMusic()
	{
		PlaySet(MusicCatalog.MenuSet, MenuFadeInSeconds, 0f, 0f);
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
		if (soloPlayer != null && soloFade.Step(dt))
		{
			soloPlayer.VolumeDb = FaderDb(soloFade.Level);
			if (soloFade.Level <= 0f && soloFade.Target <= 0f && soloPlayer.Playing)
				soloPlayer.Stop();
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
