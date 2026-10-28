using Godot;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

// BOSS RUSH (issue #12; BossRushCatalog says what the mode is and why it has this shape). The run's
// half: when each boss arrives, what felling one does, and the numbers the results screen and the save
// read afterwards.
//
// It rides the ordinary boss phase rather than running beside it. A rush boss is spawned by SpawnBoss,
// sets bossFightActive - so the clock stops, the trickle, elites, the Warden, chests, the side event and
// the secrets all stand down exactly as they do for a chapter's boss - and its fall comes back through
// CompleteBossVictory. The only differences are that a boss which is not the last hands the run back
// instead of ending it, and that the last one is the rush's finale rather than the arena's own boss.
// The finale still arrives through TickBossPhaseStart, at the ordinary boss time.
public partial class Node2DGame
{
	/// <summary>
	/// Seconds of no trickle after a rush boss falls: long enough to walk to its chest before the
	/// field fills again. Real seconds - it is how the moment feels, not where in the run it is.
	/// </summary>
	private const float BossRushBreatherSeconds = 6f;

	// Null in an ordinary run; set once in InitBossRush, before anything reads the stage.
	private IReadOnlyList<BossDefinition>? bossRushRoster;
	// The roster index of the next boss to arrive, and of the one being fought (-1 between fights).
	private int bossRushNext;
	private int bossRushCurrent = -1;
	private int bossRushFelled;
	private int bossRushArcane;
	// Real seconds spent inside fights. The run clock stops for every one of them, so the rush's
	// whole length is timeElapsed plus this - which is what its best-clear record is measured in.
	private float bossRushFightSeconds;
	private float bossRushFightStartedAt;

	/// <summary>True when this run is a Boss Rush (Global.BossRush at load).</summary>
	public bool IsBossRush => bossRushRoster != null;

	private int BossRushRosterCount => bossRushRoster?.Count ?? 0;

	/// <summary>Real seconds the rush has run, fights included. timeElapsed alone in an ordinary run.</summary>
	public float BossRushTotalSeconds => timeElapsed + bossRushFightSeconds;

	// First thing in _Ready: the rush decides the arena, so it has to before the stage is built.
	private void InitBossRush()
	{
		if (!Global.BossRush)
			return;

		IReadOnlyList<BossDefinition> roster = BossRushCatalog.Roster;
		StageDefinition? arena = BossRushCatalog.Arena;
		if (roster.Count == 0 || arena == null)
		{
			GD.PushError("Node2DGame: a Boss Rush was asked for but the campaign has no bosses to send; playing the chapter instead.");
			return;
		}

		bossRushRoster = roster;
		Global.SelectedStageIdx = arena.Index;
		GD.Print($"BOSS RUSH in {arena.DisplayName}: {roster.Count} bosses, "
			+ string.Join(", ", roster.Select((boss, i) => $"{boss.Id} at pace {BossRushArrivalPaceSeconds(i):0}s")));
	}

	private float BossRushArrivalPaceSeconds(int order) =>
		BossRushCatalog.ArrivalPaceSeconds(order, bossRushRoster?.Count ?? 1, ReferenceRunSeconds);

	/// <summary>
	/// How much of its catalog health a rush boss arriving at <paramref name="arrivalPaceSeconds"/>
	/// brings: the swarm's own health curve (UpdateSpawnScaling) at that point over its value at the
	/// ordinary boss time, which is where every boss's health was tuned. Floored at
	/// BossRushCatalog.MinHealthScale, and 1 for the finale.
	/// </summary>
	private float BossRushHealthScale(float arrivalPaceSeconds)
	{
		float end = SpawnHealthAtPaceMinutes(ReferenceRunSeconds / 60f);
		float at = SpawnHealthAtPaceMinutes(arrivalPaceSeconds / 60f);
		return end <= 0f ? 1f : Mathf.Clamp(at / end, BossRushCatalog.MinHealthScale, 1f);
	}

	// UpdateSpawnScaling's curve without the preset, which the boss gets through presetElitePowerScale.
	private float SpawnHealthAtPaceMinutes(float minutes)
	{
		float ramp = SpawnHealthRampMinutes > 0f ? Mathf.Min(1f, minutes / SpawnHealthRampMinutes) : 1f;
		return SpawnBaseHealth + minutes * SpawnHealthPerMinute * ramp;
	}

	// Every frame, before TickBossPhaseStart. Sends the next boss when the pace clock reaches it; the
	// last is left to TickBossPhaseStart, at the ordinary boss time.
	private void TickBossRush(float delta)
	{
		if (bossRushRoster == null || runFinished || bossVictoryPending)
			return;

		if (bossFightActive)
		{
			bossRushFightSeconds += delta;
			return;
		}

		int finale = bossRushRoster.Count - 1;
		if (bossRushNext >= finale || PaceSeconds < BossRushArrivalPaceSeconds(bossRushNext))
			return;

		int order = bossRushNext++;
		BossDefinition boss = bossRushRoster[order];
		float scale = BossRushHealthScale(BossRushArrivalPaceSeconds(order));
		// Before SpawnBoss, which titles the boss's bar with its place in the order (BossHudTitle).
		bossRushCurrent = order;
		if (!SpawnBoss(boss, scale))
		{
			// SpawnBoss has already said why. The rush goes on to the next rather than stalling here.
			bossRushCurrent = -1;
			GD.PushError($"Node2DGame: Boss Rush skipped {boss.Id}, which would not spawn.");
			return;
		}

		bossRushFightStartedAt = bossRushFightSeconds;
		activeBossDefinition = boss;
		bossFightActive = true;
		PlayBossMusic();
		ShowBossBanner(boss);
		GD.Print($"BOSS RUSH {order + 1}/{bossRushRoster.Count}: {boss.DisplayName} ({boss.Id}) with {bossInstance?.Health ?? 0} HP"
			+ $" (x{scale:0.00}) at pace {PaceSeconds:0}s, real {BossRushTotalSeconds:0}s, level {player?.CurrentLevel ?? 0}");
	}

	// The finale, from TickBossPhaseStart: the roster's last, at full health.
	private BossDefinition? BeginBossRushFinale()
	{
		if (bossRushRoster == null)
			return null;
		bossRushCurrent = bossRushRoster.Count - 1;
		bossRushNext = bossRushRoster.Count;
		bossRushFightStartedAt = bossRushFightSeconds;
		return bossRushRoster[bossRushCurrent];
	}

	/// <summary>
	/// A boss of the rush fell. True when the run goes on (it was not the last); CompleteBossVictory
	/// then does nothing more. False for the finale, which wins the run the ordinary way.
	/// </summary>
	private bool FellBossRushBoss(string bossId)
	{
		if (bossRushRoster == null || bossRushCurrent < 0)
			return false;

		BossDefinition boss = bossRushRoster[bossRushCurrent];
		bossRushFelled++;
		bossRushArcane += Mathf.RoundToInt(boss.ArcaneVictoryBonus * BossRushCatalog.ArcaneBonusShare);
		float fight = bossRushFightSeconds - bossRushFightStartedAt;
		GD.Print($"BOSS RUSH {bossRushCurrent + 1}/{bossRushRoster.Count}: {boss.Id} felled in {fight:0.0}s, level {player?.CurrentLevel ?? 0}");

		if (bossRushCurrent >= bossRushRoster.Count - 1)
			return false;

		Vector2 at = bossInstance != null && IsInstanceValid(bossInstance) ? bossInstance.GlobalPosition
			: player?.GlobalPosition ?? stageOrigin;
		if (bossInstance != null && IsInstanceValid(bossInstance)
			&& bossInstance.IsConnected(BossEnemy.SignalName.BossDefeated, new Callable(this, nameof(OnBossDefeated))))
		{
			bossInstance.Disconnect(BossEnemy.SignalName.BossDefeated, new Callable(this, nameof(OnBossDefeated)));
		}

		bossFightActive = false;
		bossInstance = null;
		activeBossDefinition = null;
		bossRushCurrent = -1;
		bossHudRoot?.QueueFree();
		bossHudRoot = null;
		bossHudBar = null;
		bossHudLabel = null;

		// What the fight paid: a chest where it fell, and a moment to walk to it before the field
		// fills again. Placed the way a carried chest is (never under the player).
		PlaceChestNear(at, ChestSourceBoss);
		spawnGraceRemaining = Mathf.Max(spawnGraceRemaining, BossRushBreatherSeconds);
		if (runMusicSet != null)
			runMusic?.PlaySet(runMusicSet, MusicPlayer.RunFadeInSeconds, 0f, 0f);

		BossDefinition next = bossRushRoster[bossRushNext];
		ShowRunBanner($"{ShortBossName(boss)} falls  ·  {bossRushFelled} of {bossRushRoster.Count}\n{ShortBossName(next)} comes next.",
			new Color(1.0f, 0.92f, 0.66f, 1.0f), 3.0f);
		return true;
	}

	// "Elderbark", not "Elderbark, the Treant": the banners are one line each.
	private static string ShortBossName(BossDefinition boss) => boss.DisplayName.Split(',')[0].Trim();

	// The boss's name over its bar, and in a rush which one of how many it is.
	private string BossHudTitle(BossDefinition boss) =>
		bossRushRoster == null || bossRushCurrent < 0
			? boss.DisplayName
			: $"{boss.DisplayName}   ·   {bossRushCurrent + 1} of {bossRushRoster.Count}";

	// The arrival banner's first line: the name, and in a rush its place in the order.
	private string BossBannerTitle(BossDefinition boss) =>
		bossRushRoster == null || bossRushCurrent < 0
			? boss.DisplayName
			: $"{bossRushCurrent + 1} of {bossRushRoster.Count}  ·  {boss.DisplayName}";

	// F4 in a rush: the next boss now, not the finale. Never mid-fight.
	private void SkipToNextBossRushArrival()
	{
		if (bossRushRoster == null || bossFightActive)
			return;
		if (bossRushNext < bossRushRoster.Count - 1)
			timeElapsed = Mathf.Max(timeElapsed, BossRushArrivalPaceSeconds(bossRushNext) * RunPace);
		else if (!bossPhaseTriggered && TimerVictorySeconds > 0f)
			timeElapsed = TimerVictorySeconds;
	}

	/// <summary>
	/// Tools only (_UiShot --rush-steps): one step of the rush - fell the boss being fought, or bring
	/// the next one now. Nothing in play calls it.
	/// </summary>
	public void DebugAdvanceBossRush()
	{
		if (!IsBossRush || runFinished)
			return;
		if (bossFightActive && bossInstance != null && IsInstanceValid(bossInstance))
			bossInstance.TakeDamage(bossInstance.Health * 10 + 1, false);
		else
			SkipToNextBossRushArrival();
	}

	// The save's two rush records. Called from FinishRunAndReward with the run already decided.
	private void RecordBossRush(SaveData data, RunOutcome outcome)
	{
		if (bossRushRoster == null || data == null)
			return;
		data.BossRushBestFelled = Mathf.Max(data.BossRushBestFelled, bossRushFelled);
		if (outcome == RunOutcome.Victory
			&& (data.BossRushBestClearSeconds <= 0f || BossRushTotalSeconds < data.BossRushBestClearSeconds))
		{
			data.BossRushBestClearSeconds = BossRushTotalSeconds;
		}
	}
}
