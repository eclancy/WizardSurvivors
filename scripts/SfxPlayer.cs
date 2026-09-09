using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// The one place sound effects are played from. Registered as an autoload alongside
// MusicPlayer and SaveManager; see .ai/audio-direction.md section 9.
//
// Three things this exists to stop, all of which are cheap to get wrong at a call site and
// expensive to notice afterwards:
//
//   * ALLOCATION. Nothing here calls new AudioStreamPlayer2D() or ResourceLoader.Load() while
//     the game is running. Every stream is loaded once in _Ready and every voice comes out of
//     a fixed pool, because the alternative is a frame hitch on the first fireball of a run.
//   * FLOODING. An AoE landing on forty enemies is one impact, not forty. The throttles live
//     here rather than at the call sites so the policy is in one readable place and cannot
//     drift per spell - the same reason Player owns one spellFireTimers loop instead of a
//     timer per spell.
//   * LOUDNESS DRIFT. Every file is already normalised to its mix target by
//     tools/audio/build.py, so everything plays at 0 dB and balance is changed by editing the
//     registry in tools/audio/sfx.py and re-running. Resist adding volumeDb at call sites.
//
// Levels are set by how OFTEN a sound plays, not by how important it feels: pickup_xp sits
// 19 dB under boss_death because a run collects thousands of orbs. That rule is the mix.
public partial class SfxPlayer : Node
{
#nullable enable
	public const string SfxBusName = "SFX";
	public const string MasterBusName = "Master";
	// Two children of SFX. The user volume slider lives on SFX itself, so ducking SFX_Bed can
	// never fight the slider - which is exactly what would happen if the duck wrote to the same
	// bus the player had just set. SFX_Priority is never ducked, and both feed one hard limiter.
	public const string BedBusName = "SFX_Bed";
	public const string PriorityBusName = "SFX_Priority";

	// 24 positional voices is roughly three times the worst case once the throttles below are
	// applied. It is a deliberate cap rather than a guess: past its own internal limit Godot
	// drops streams arbitrarily, which spends the budget on swarm noise and loses the boss.
	private const int PositionalVoices = 24;
	private const int GlobalVoices = 8;

	// The viewport is 720x1280 and enemies spawn off-screen. A sound from something the player
	// cannot see should be faint, not absent - it is a cue that something is out there.
	private const float MaxAudibleDistance = 900.0f;

	private const float DefaultPitchJitter = 0.05f;

	// Minimum gap between two plays sharing a throttle key, in milliseconds.
	private const int EnemyHurtGapMs = 60;
	private const int EnemyDeathGapMs = 45;
	private const int ImpactGapMs = 55;
	private const int CastGapMs = 80;
	// How long a gap breaks the XP collection streak, in milliseconds.
	private const ulong XpStreakResetMs = 600;
	// Where the ladder tops out. A long streak that kept climbing would end up somewhere only
	// a bat can hear, and the rise stops being legible long before that.
	private const float XpStreakMaxPitch = 1.5f;

	// How far the bed drops while a Critical sound plays, and how long it takes to come back.
	// Six decibels was measured rather than guessed: tools/audio/mixsim.py puts the worst-case
	// boss roar at +3.6 dB over the swarm without this and +12.1 dB with it. It deliberately
	// does not duck to silence - losing hit feedback during a boss roar is worse than the noise
	// it was fixing - and the release is short enough that two roars close together do not stack
	// into a hole in the mix.
	private const float DuckDb = -6.0f;
	private const float DuckReleaseSeconds = 0.30f;
	// Effectively do not attenuate: far past any distance the camera can show.
	private const float CriticalAudibleDistance = 100000.0f;

	public static SfxPlayer? Instance { get; private set; }

	private readonly Dictionary<string, AudioStream> streams = new();
	private readonly Dictionary<string, ulong> lastPlayedMsec = new();
	private AudioStreamPlayer2D[] positional = Array.Empty<AudioStreamPlayer2D>();
	private AudioStreamPlayer[] global = Array.Empty<AudioStreamPlayer>();
	private int nextPositional;
	private int nextGlobal;
	private readonly RandomNumberGenerator rng = new();
	private static bool warnedMissingInstance;
	private int xpStreak;
	private ulong lastXpMsec;
	private string bedBus = MasterBusName;
	private string priorityBus = MasterBusName;
	private int bedBusIndex = -1;
	private ulong duckUntilMsec;
	private float appliedDuckDb;

	public static string ResolveSfxBusName()
	{
		return AudioServer.GetBusIndex(SfxBusName) >= 0 ? SfxBusName : MasterBusName;
	}

	/// <summary>A bus if it exists, otherwise the next one up. Every fallback here is silent by
	/// design but reported by RegressionChecks: a missing bus degrades the mix rather than
	/// breaking it, so nothing would ever notice on its own.</summary>
	private static string Resolve(string preferred, string fallback)
	{
		return AudioServer.GetBusIndex(preferred) >= 0 ? preferred : fallback;
	}

	public override void _Ready()
	{
		Instance = this;
		rng.Randomize();
		ProcessMode = Node.ProcessModeEnum.Always;   // menus and pause sounds must still play

		string bus = ResolveSfxBusName();
		bedBus = Resolve(BedBusName, bus);
		priorityBus = Resolve(PriorityBusName, bus);
		bedBusIndex = AudioServer.GetBusIndex(bedBus);
		positional = new AudioStreamPlayer2D[PositionalVoices];
		voiceTier = new SfxCatalog.SfxTier[PositionalVoices];
		voiceStarted = new ulong[PositionalVoices];
		for (int i = 0; i < PositionalVoices; i++)
		{
			var p = new AudioStreamPlayer2D
			{
				Bus = bedBus,
				MaxDistance = MaxAudibleDistance,
				Attenuation = 1.0f,
				ProcessMode = Node.ProcessModeEnum.Always,
			};
			AddChild(p);
			positional[i] = p;
		}
		global = new AudioStreamPlayer[GlobalVoices];
		for (int i = 0; i < GlobalVoices; i++)
		{
			var p = new AudioStreamPlayer { Bus = bedBus, ProcessMode = Node.ProcessModeEnum.Always };
			AddChild(p);
			global[i] = p;
		}

		int loaded = 0, missing = 0;
		foreach (string name in SfxCatalog.AllNames)
		{
			var stream = ResourceLoader.Load<AudioStream>(SfxCatalog.PathFor(name));
			if (stream == null)
			{
				missing++;
				GD.PushWarning($"SfxPlayer: missing sound '{name}' at {SfxCatalog.PathFor(name)}");
				continue;
			}
			streams[name] = stream;
			loaded++;
		}
		GD.Print($"SfxPlayer: {loaded} sounds preloaded on bus '{bus}'"
			+ (missing > 0 ? $", {missing} MISSING" : string.Empty));

		// Autoload order in project.godot puts SaveManager before SfxPlayer, so the save file is
		// already loaded here. This is the only place the three buses are restored, and doing it
		// from the audio autoload means it happens whichever scene the game is launched into.
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager != null)
			AudioSettings.ApplyFromSave(saveManager.Data);
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}

	// ---------------------------------------------------------------------
	// core playback
	// ---------------------------------------------------------------------

	// Which tier each positional voice is currently carrying, and when it started. Both are only
	// meaningful while that voice is Playing; a finished voice keeps stale values, which is
	// harmless because every path checks Playing first.
	private SfxCatalog.SfxTier[] voiceTier = Array.Empty<SfxCatalog.SfxTier>();
	private ulong[] voiceStarted = Array.Empty<ulong>();

	/// <summary>
	/// A free voice, or one worth taking, or null meaning do not play this at all.
	///
	/// The old rule was round-robin, which meant a boss roar could be cut off by an XP orb blip.
	/// Now a Swarm sound never steals: dropping the twenty-fifth simultaneous hit costs nothing,
	/// and stealing a cast to play it costs a lot. tools/audio/mixsim.py measured late-game
	/// steals falling from 215 to 35 under this rule.
	/// </summary>
	private AudioStreamPlayer2D? TakePositional(SfxCatalog.SfxTier tier)
	{
		for (int i = 0; i < positional.Length; i++)
		{
			int index = (nextPositional + i) % positional.Length;
			if (!positional[index].Playing)
			{
				nextPositional = (index + 1) % positional.Length;
				return Claim(index, tier);
			}
		}

		if (tier == SfxCatalog.SfxTier.Swarm)
			return null;

		int victim = -1;
		for (int i = 0; i < positional.Length; i++)
		{
			SfxCatalog.SfxTier playing = voiceTier[i];
			bool stealable = playing == SfxCatalog.SfxTier.Swarm
				|| (playing == SfxCatalog.SfxTier.Normal && tier == SfxCatalog.SfxTier.Critical);
			if (!stealable)
				continue;
			// Among equally stealable voices take the one that started earliest: it is closest to
			// finishing, so interrupting it is the least audible.
			if (victim < 0 || voiceStarted[i] < voiceStarted[victim])
				victim = i;
		}
		return victim < 0 ? null : Claim(victim, tier);
	}

	private AudioStreamPlayer2D Claim(int index, SfxCatalog.SfxTier tier)
	{
		voiceTier[index] = tier;
		voiceStarted[index] = Time.GetTicksMsec();
		return positional[index];
	}

	private AudioStreamPlayer TakeGlobal()
	{
		for (int i = 0; i < global.Length; i++)
		{
			var candidate = global[(nextGlobal + i) % global.Length];
			if (!candidate.Playing)
			{
				nextGlobal = (nextGlobal + i + 1) % global.Length;
				return candidate;
			}
		}
		var stolen = global[nextGlobal];
		nextGlobal = (nextGlobal + 1) % global.Length;
		return stolen;
	}

	private bool Throttled(string key, int minGapMs)
	{
		if (minGapMs <= 0)
			return false;
		ulong now = Time.GetTicksMsec();
		if (lastPlayedMsec.TryGetValue(key, out ulong last) && now - last < (ulong)minGapMs)
			return true;
		lastPlayedMsec[key] = now;
		return false;
	}

	private float Jitter(float amount)
	{
		return amount <= 0.0f ? 1.0f : 1.0f + rng.RandfRange(-amount, amount);
	}

	/// <summary>Play a sound at a world position. Returns false if it was suppressed.</summary>
	public bool PlayAt(string name, Vector2 globalPosition, float pitchJitter = DefaultPitchJitter,
		string? throttleKey = null, int minGapMs = 0)
	{
		if (!streams.TryGetValue(name, out var stream))
			return false;
		if (Throttled(throttleKey ?? name, minGapMs))
			return false;
		var tier = SfxCatalog.TierOf(name);
		var voice = TakePositional(tier);
		if (voice == null)
			return false;
		bool critical = tier == SfxCatalog.SfxTier.Critical;
		voice.Bus = critical ? priorityBus : bedBus;
		// A Critical sound keeps its position, and so its panning, but stops being attenuated for
		// it. mixsim measured elite_spawn at 20 dB UNDER the swarm purely because it happened to
		// spawn across the screen, and a rule that says this is the sound that matters and then
		// halves it for being 380 px away is not a rule.
		voice.MaxDistance = critical ? CriticalAudibleDistance : MaxAudibleDistance;
		voice.Stream = stream;
		voice.GlobalPosition = globalPosition;
		voice.PitchScale = Jitter(pitchJitter);
		voice.Play();
		if (critical)
			TriggerDuck(stream);
		return true;
	}

	/// <summary>Play a sound with no world position - UI, stingers, anything about the player.</summary>
	public bool Play(string name, float pitchJitter = 0.0f, string? throttleKey = null, int minGapMs = 0)
	{
		if (!streams.TryGetValue(name, out var stream))
			return false;
		if (Throttled(throttleKey ?? name, minGapMs))
			return false;
		var tier = SfxCatalog.TierOf(name);
		var voice = TakeGlobal();
		voice.Bus = tier == SfxCatalog.SfxTier.Critical ? priorityBus : bedBus;
		voice.Stream = stream;
		voice.PitchScale = Jitter(pitchJitter);
		voice.Play();
		if (tier == SfxCatalog.SfxTier.Critical)
			TriggerDuck(stream);
		return true;
	}

	// ---------------------------------------------------------------------
	// the duck
	// ---------------------------------------------------------------------

	private void TriggerDuck(AudioStream stream)
	{
		double seconds = stream.GetLength();
		if (seconds <= 0.0)
			seconds = 1.0;   // a stream that will not report a length still deserves a duck
		ulong until = Time.GetTicksMsec() + (ulong)(seconds * 1000.0);
		if (until > duckUntilMsec)
			duckUntilMsec = until;
	}

	// A scripted duck rather than a sidechained compressor. Godot does have
	// AudioEffectCompressor.Sidechain and it would sound more organic - but a fixed curve is
	// exactly reproducible in tools/audio/mixsim.py and a compressor is not, and nobody working
	// on this project can hear the difference to referee it. Predictable beats organic while
	// that is true.
	public override void _Process(double delta)
	{
		if (bedBusIndex < 0)
			return;
		ulong now = Time.GetTicksMsec();
		float db;
		if (now < duckUntilMsec)
		{
			db = DuckDb;
		}
		else
		{
			float since = (now - duckUntilMsec) / 1000.0f;
			if (since >= DuckReleaseSeconds)
			{
				if (appliedDuckDb == 0.0f)
					return;   // released and already flat: the common case, so it costs nothing
				db = 0.0f;
			}
			else
			{
				db = DuckDb * (1.0f - since / DuckReleaseSeconds);
			}
		}
		if (Mathf.Abs(db - appliedDuckDb) < 0.05f)
			return;
		appliedDuckDb = db;
		AudioServer.SetBusVolumeDb(bedBusIndex, db);
	}

	/// <summary>Play at an explicit pitch - the XP ladder needs this, nothing else should.</summary>
	public bool PlayAtPitch(string name, Vector2 globalPosition, float pitchScale)
	{
		if (!streams.TryGetValue(name, out var stream))
			return false;
		var tier = SfxCatalog.TierOf(name);
		var voice = TakePositional(tier);
		if (voice == null)
			return false;   // an orb blip is exactly the thing that should drop itself
		voice.Bus = tier == SfxCatalog.SfxTier.Critical ? priorityBus : bedBus;
		voice.MaxDistance = tier == SfxCatalog.SfxTier.Critical
			? CriticalAudibleDistance : MaxAudibleDistance;
		voice.Stream = stream;
		voice.GlobalPosition = globalPosition;
		voice.PitchScale = pitchScale;
		voice.Play();
		return true;
	}

	// ---------------------------------------------------------------------
	// static call-site helpers
	// ---------------------------------------------------------------------
	// Audio is not gameplay state, so a missing autoload degrades to silence rather than
	// throwing - but it says so once, because silent silence is the bug that never gets filed.

	private static SfxPlayer? Get()
	{
		if (Instance == null && !warnedMissingInstance)
		{
			warnedMissingInstance = true;
			GD.PushWarning("SfxPlayer: autoload not present, sound effects are disabled");
		}
		return Instance;
	}

	public static void EnemyHurt(Vector2 position)
	{
		var self = Get();
		if (self == null)
			return;
		// One throttle key for all three variants: they are one sound with three faces, and
		// throttling them separately would let three fire in the window meant to hold one.
		string name = SfxCatalog.EnemyHurtVariants[self.rng.RandiRange(0, SfxCatalog.EnemyHurtVariants.Length - 1)];
		self.PlayAt(name, position, 0.08f, "enemy_hurt", EnemyHurtGapMs);
	}

	public static void EnemyDeath(Vector2 position, bool heavy)
	{
		Get()?.PlayAt(heavy ? SfxCatalog.EnemyDeathHeavy : SfxCatalog.EnemyDeathSmall,
			position, 0.06f, heavy ? "enemy_death_heavy" : "enemy_death_small", EnemyDeathGapMs);
	}

	public static void Impact(Element element, Vector2 position)
	{
		string name = SfxCatalog.ImpactFor(element);
		Get()?.PlayAt(name, position, 0.06f, name, ImpactGapMs);
	}

	public static void Cast(Element element, Vector2 position)
	{
		string name = SfxCatalog.CastFor(element);
		Get()?.PlayAt(name, position, 0.04f, name, CastGapMs);
	}

	public static void AtPosition(string name, Vector2 position, float pitchJitter = DefaultPitchJitter)
	{
		Get()?.PlayAt(name, position, pitchJitter);
	}

	public static void Global(string name, float pitchJitter = 0.0f)
	{
		Get()?.Play(name, pitchJitter);
	}

	/// <summary>
	/// The XP orb: one file pitched up with the collection streak, reset when the streak breaks.
	/// See .ai/audio-direction.md section 7 - variation belongs at play time, not on disk, and
	/// this is the whole reason only one orb sound was generated.
	///
	/// The streak lives here rather than in XPOrb because an orb is freed the instant it is
	/// collected, so it has nowhere to keep a count, and because how the ladder behaves is a
	/// mix decision rather than a gameplay one.
	/// </summary>
	public static void PickupXp(Vector2 position)
	{
		var self = Get();
		if (self == null)
			return;
		ulong now = Time.GetTicksMsec();
		self.xpStreak = now - self.lastXpMsec > XpStreakResetMs ? 0 : self.xpStreak + 1;
		self.lastXpMsec = now;
		float pitch = Mathf.Min(1.0f + 0.03f * self.xpStreak, XpStreakMaxPitch);
		self.PlayAtPitch(SfxCatalog.PickupXp, position, pitch);
	}
}
