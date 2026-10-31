using Godot;
using System;
using System.Diagnostics;
using System.Text;

/// <summary>
/// Where a frame goes: real frame time and its percentiles, split into the physics scripts, the
/// process scripts and everything the engine does around them, with timed sections inside the
/// systems that scale with the swarm. Off unless asked for, and then printed as PERF lines.
/// </summary>
/// <remarks>
/// <para><b>Why it exists.</b> The phone lagged "as the spawns pick up", and the only numbers there
/// were the Performance monitors, which mislead: <c>TimeProcess</c> and <c>TimePhysicsProcess</c>
/// are refreshed once a second with the WORST frame of that second, so an average of them is an
/// average of spikes. This measures every frame. See .ai/performance.md.</para>
///
/// <para><b>Turning it on.</b> <c>--profile</c> (or <c>--profile=SECONDS</c> for the report interval,
/// default 5) anywhere in the user arguments, in any build - release included, because the phone
/// runs the release-signed build. Node2DGame attaches it at <c>_Ready</c>
/// (<see cref="AttachIfRequested"/>). On a phone the argument arrives through the launch intent:
/// <c>adb shell am start -n com.ecec.wizardsurvivors/com.godot.game.GodotApp --esa command_line_params
/// "--,--profile"</c>, and the lines come out of logcat under the <c>godot</c> tag.</para>
///
/// <para><b>How the frame is split.</b> Two instances of this node bracket every callback: the head
/// runs first (lowest priority) and the tail last (highest), in both <c>_Process</c> and
/// <c>_PhysicsProcess</c>. "phys" is the physics callbacks summed over the frame's steps, "proc" the
/// process callbacks, and "engine" the rest of the frame - the physics solver, the area and body
/// signals it flushes (a projectile's hit handler runs there, before any physics callback), deferred
/// calls, and rendering. "render" is the share of that between RenderingServer's pre- and post-draw
/// signals. Headless, rendering is a dummy and costs nothing; on the phone it is real.</para>
///
/// <para><b>Sections</b> are inclusive Stopwatch spans inside the code that scales with the swarm,
/// reported as microseconds per frame and calls per frame. A disabled profiler costs each site
/// one static bool read. Add a section by adding to <see cref="Section"/> and bracketing the code
/// with <see cref="Begin"/> / <see cref="End"/>.</para>
/// </remarks>
public partial class FrameProfiler : Node
{
	public enum Section
	{
		EnemyPhysics,
		EnemySeparation,
		EnemyMove,
		EnemyTakeDamage,
		EnemyDeath,
		FloatingTextSpawn,
		HitFeedback,
		PlayerPhysics,
		PlayerSpells,
		GameProcess,
		Spawning,
		XpOrbs,
		FloatingText,
		Terrain,
		Navigation,
		// Starfall's rain: one node's _Process and both of its _Draw passes (scripts/Starfall.cs).
		Starfall,
		// The facing family: Stormsweep's arcs and Sleet Runes' pooled runes, process and draw
		// (scripts/Stormsweep.cs, scripts/SleetRunes.cs).
		Stormsweep,
		SleetRunes,
		// Mirefoot's bog: the one surface that draws every pool of the trail, process and draw
		// (scripts/MireSurface.cs). The pools' own damage ticks are LingeringZone's and are not in it.
		Mirefoot,
		// Thorned Vine: every vine's _Process - its whip stroke, its hit test and its paint
		// (scripts/ThornVine.cs, the strikes' damage included).
		ThornVine,
		Harness,
		Count,
	}

	private static readonly string[] SectionNames =
	{
		"enemy", "separation", "move", "takeDamage", "death", "dmgText", "hitFx",
		"player", "spells", "game", "spawn", "orbs", "textAnim", "terrain", "nav", "starfall", "stormsweep", "runes", "mire", "vine", "harness",
	};

	/// <summary>True while a profiler is attached and counting.</summary>
	public static bool Enabled { get; private set; }

	private static readonly long[] sectionTicks = new long[(int)Section.Count];
	private static readonly long[] sectionBytes = new long[(int)Section.Count];
	private static readonly int[] sectionCalls = new int[(int)Section.Count];

	/// <summary>Where a section began: a timestamp and the main thread's allocation counter.</summary>
	public readonly struct Span
	{
		public readonly long Ticks;
		public readonly long Bytes;

		public Span(long ticks, long bytes)
		{
			Ticks = ticks;
			Bytes = bytes;
		}
	}

	/// <summary>A start to hand back to <see cref="End"/>; empty when profiling is off.</summary>
	public static Span Begin() => Enabled
		? new Span(Stopwatch.GetTimestamp(), GC.GetAllocatedBytesForCurrentThread())
		: default;

	// SAMPLED SECTIONS. A section opened by every enemy every step is opened ~8,000 times a second
	// at the cap, and a timestamp is not free: on the desktop this was written on, one Begin/End
	// pair cost about 1.5 us, which inflated the enemy figures by a millisecond a step and was 13%
	// of the main thread under a trace. So the per-body sections are timed on one physics step (or
	// process frame) in SampleEvery and scaled back up in the report. A body's cost does not care
	// which step it is measured on, so over a 5 s window the estimate is good.
	public const int SampleEvery = 4;
	private static bool sampleThisStep, sampleThisFrame;
	private static int stepCounter, frameCounter;

	/// <summary><see cref="Begin"/> on one physics step in <see cref="SampleEvery"/>, for code that runs per body per step.</summary>
	public static Span BeginSampledStep() => sampleThisStep ? Begin() : default;

	/// <summary><see cref="Begin"/> on one process frame in <see cref="SampleEvery"/>, for code that runs per node per frame.</summary>
	public static Span BeginSampledFrame() => sampleThisFrame ? Begin() : default;

	private static bool IsSampled(int section) => section == (int)Section.EnemyPhysics || section == (int)Section.EnemySeparation
		|| section == (int)Section.EnemyMove || section == (int)Section.XpOrbs || section == (int)Section.FloatingText;

	public static void End(Section section, Span start)
	{
		if (start.Ticks == 0)
			return;
		sectionTicks[(int)section] += Stopwatch.GetTimestamp() - start.Ticks;
		sectionBytes[(int)section] += GC.GetAllocatedBytesForCurrentThread() - start.Bytes;
		sectionCalls[(int)section]++;
	}

	/// <summary>Adds a profiler to <paramref name="game"/> if the command line asked for one.</summary>
	public static void AttachIfRequested(Node game)
	{
		float interval = RequestedInterval();
		if (interval <= 0f || game == null)
			return;
		var head = new FrameProfiler { Name = "FrameProfilerHead", isHead = true, reportSeconds = interval };
		var tail = new FrameProfiler { Name = "FrameProfilerTail", isHead = false };
		head.partner = tail;
		game.AddChild(head);
		game.AddChild(tail);
	}

	/// <summary>
	/// Turns the profiler on without a command line, at this report interval: for a measurement build
	/// that cannot be handed arguments (.ai/performance.md, "On the phone"). Zero leaves it to the
	/// command line.
	/// </summary>
	public static float ForcedInterval { get; set; }

	// --profile or --profile=SECONDS, in the user arguments or (an Android intent has no "--" of
	// its own unless given one) the engine's.
	private static float RequestedInterval()
	{
		if (ForcedInterval > 0f)
			return ForcedInterval;
		foreach (string[] args in new[] { OS.GetCmdlineUserArgs(), OS.GetCmdlineArgs() })
		{
			foreach (string arg in args)
			{
				if (arg == "--profile")
					return 5f;
				if (arg.StartsWith("--profile=", StringComparison.Ordinal)
					&& float.TryParse(arg.Substring(10), System.Globalization.NumberStyles.Float,
						System.Globalization.CultureInfo.InvariantCulture, out float seconds) && seconds > 0f)
					return seconds;
			}
		}
		return 0f;
	}

	private bool isHead;
	private FrameProfiler? partner;
	private float reportSeconds = 5f;

	// Shared by the two instances: the head opens a span, the tail closes it.
	private static long physicsSpanStart, processSpanStart;
	private static long windowPhysicsTicks, windowProcessTicks, windowRenderTicks, renderStart;
	private static int windowPhysicsSteps;

	private long lastFrameStamp;
	private ulong lastProcessFrame;
	private long windowStart;
	private int windowFrames;
	private double windowFrameMsSum;
	private float[] frameMs = new float[4096];
	private float[] sortBuffer = new float[4096];
	private int gen0, gen1, gen2;
	private long allocatedAtWindowStart;
	private TimeSpan gcPauseAtWindowStart;
	private int windowIndex;

	public override void _EnterTree()
	{
		ProcessPriority = isHead ? int.MinValue : int.MaxValue;
		ProcessPhysicsPriority = isHead ? int.MinValue : int.MaxValue;
		if (!isHead)
			return;
		Enabled = true;
		Array.Clear(sectionTicks);
		Array.Clear(sectionBytes);
		Array.Clear(sectionCalls);
		RenderingServer.FramePreDraw += OnFramePreDraw;
		RenderingServer.FramePostDraw += OnFramePostDraw;
		GD.Print($"PERF profiler on: report every {reportSeconds:0.#} s (frame times are real milliseconds; sections are inclusive)");
	}

	public override void _ExitTree()
	{
		if (!isHead)
			return;
		Enabled = false;
		sampleThisStep = sampleThisFrame = false;
		RenderingServer.FramePreDraw -= OnFramePreDraw;
		RenderingServer.FramePostDraw -= OnFramePostDraw;
	}

	private static void OnFramePreDraw() => renderStart = Stopwatch.GetTimestamp();

	private static void OnFramePostDraw()
	{
		if (renderStart != 0)
			windowRenderTicks += Stopwatch.GetTimestamp() - renderStart;
		renderStart = 0;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (isHead)
		{
			physicsSpanStart = Stopwatch.GetTimestamp();
			windowPhysicsSteps++;
			sampleThisStep = Enabled && (++stepCounter % SampleEvery) == 0;
		}
		else if (physicsSpanStart != 0)
		{
			windowPhysicsTicks += Stopwatch.GetTimestamp() - physicsSpanStart;
			physicsSpanStart = 0;
		}
	}

	public override void _Process(double delta)
	{
		long now = Stopwatch.GetTimestamp();
		if (!isHead)
		{
			if (processSpanStart != 0)
				windowProcessTicks += now - processSpanStart;
			processSpanStart = 0;
			return;
		}

		processSpanStart = now;
		sampleThisFrame = Enabled && (++frameCounter % SampleEvery) == 0;
		ulong processFrame = Engine.GetProcessFrames();
		// A paused tree (a level-up card) skips these callbacks, so a gap in the frame counter means
		// the period since the last call was not one frame of play. Start the window over from here.
		bool contiguous = lastFrameStamp != 0 && processFrame == lastProcessFrame + 1;
		if (contiguous)
		{
			float ms = (float)((now - lastFrameStamp) * 1000.0 / Stopwatch.Frequency);
			if (windowFrames < frameMs.Length)
				frameMs[windowFrames] = ms;
			windowFrames++;
			windowFrameMsSum += ms;
			if (ms >= HitchMs)
				ReportHitch(ms, now);
		}
		else if (windowStart == 0)
		{
			StartWindow(now);
		}
		lastFrameStamp = now;
		lastProcessFrame = processFrame;

		if (windowStart != 0 && (now - windowStart) >= reportSeconds * Stopwatch.Frequency && windowFrames > 0)
		{
			Report(now);
			StartWindow(now);
		}

		// After any window reset, so the next frame's hitch arithmetic starts from the same zero.
		Array.Copy(sectionTicks, ticksAtFrameStart, sectionTicks.Length);
		physicsTicksAtFrameStart = windowPhysicsTicks;
		processTicksAtFrameStart = windowProcessTicks;
		stepsAtFrameStart = windowPhysicsSteps;
		gcAtFrameStart = GC.CollectionCount(0);
	}

	// A HITCH is one frame long enough to feel (HitchMs): what a mean hides. Its line says what the
	// frame was spent on - the physics callbacks over how many steps, the process callbacks, the
	// engine's share, whether a collection ran, and the sections that grew most in it. At most one a
	// second, so a run that hitches constantly does not drown its own log.
	private const float HitchMs = 50f;
	private readonly long[] ticksAtFrameStart = new long[(int)Section.Count];
	private long physicsTicksAtFrameStart, processTicksAtFrameStart;
	private int stepsAtFrameStart, gcAtFrameStart;
	private long lastHitchReport;

	private void ReportHitch(float ms, long now)
	{
		if (lastHitchReport != 0 && now - lastHitchReport < Stopwatch.Frequency)
			return;
		lastHitchReport = now;
		// The tail's span for this frame has not closed yet, so "proc" is the previous frame's and
		// the engine share is the rest. Good enough to say which kind of frame it was.
		double phys = TicksToMs(windowPhysicsTicks - physicsTicksAtFrameStart);
		double proc = TicksToMs(windowProcessTicks - processTicksAtFrameStart);
		var line = new StringBuilder(256);
		line.Append($"PERF hitch {ms:0}ms: phys={phys:0.0} over {windowPhysicsSteps - stepsAtFrameStart} steps, proc={proc:0.0},");
		line.Append($" engine={Math.Max(0.0, ms - phys - proc):0.0}, gc={GC.CollectionCount(0) - gcAtFrameStart}; top:");
		for (int pick = 0; pick < 4; pick++)
		{
			int best = -1;
			long bestTicks = 0;
			for (int i = 0; i < (int)Section.Count; i++)
			{
				long grew = sectionTicks[i] - ticksAtFrameStart[i];
				if (grew > bestTicks && (pick == 0 || !hitchPicked[i]))
				{
					best = i;
					bestTicks = grew;
				}
			}
			if (best < 0)
				break;
			hitchPicked[best] = true;
			line.Append($" {SectionNames[best]}={TicksToMs(bestTicks):0.0}");
		}
		Array.Clear(hitchPicked);
		GD.Print(line.ToString());
	}

	private readonly bool[] hitchPicked = new bool[(int)Section.Count];

	private void StartWindow(long now)
	{
		windowStart = now;
		windowFrames = 0;
		windowFrameMsSum = 0.0;
		windowPhysicsTicks = windowProcessTicks = windowRenderTicks = 0;
		windowPhysicsSteps = 0;
		Array.Clear(sectionTicks);
		Array.Clear(sectionBytes);
		Array.Clear(sectionCalls);
		gen0 = GC.CollectionCount(0);
		gen1 = GC.CollectionCount(1);
		gen2 = GC.CollectionCount(2);
		allocatedAtWindowStart = GC.GetAllocatedBytesForCurrentThread();
		gcPauseAtWindowStart = GC.GetTotalPauseDuration();
	}

	private static double TicksToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

	private void Report(long now)
	{
		int n = Math.Min(windowFrames, frameMs.Length);
		Array.Copy(frameMs, sortBuffer, n);
		Array.Sort(sortBuffer, 0, n);
		float Percentile(float p) => n == 0 ? 0f : sortBuffer[Math.Clamp((int)Math.Ceiling(p * n) - 1, 0, n - 1)];

		double frames = Math.Max(1, windowFrames);
		double meanMs = windowFrameMsSum / frames;
		double physMs = TicksToMs(windowPhysicsTicks) / frames;
		double procMs = TicksToMs(windowProcessTicks) / frames;
		double renderMs = TicksToMs(windowRenderTicks) / frames;
		double engineMs = Math.Max(0.0, meanMs - physMs - procMs);
		double seconds = (now - windowStart) / (double)Stopwatch.Frequency;
		long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedAtWindowStart;
		double pauseMs = (GC.GetTotalPauseDuration() - gcPauseAtWindowStart).TotalMilliseconds;

		SceneTree tree = GetTree();
		int enemies = tree.GetNodeCountInGroup("enemies");
		// The registry mirrors the group (EnemyRegistry); a profiled run is where a drift would show.
		if (EnemyRegistry.Count != enemies)
			GD.PushError($"PERF EnemyRegistry holds {EnemyRegistry.Count} enemies but the \"enemies\" group holds {enemies}: something changed the group without EnemyRegistry.Join/Leave.");
		int orbs = tree.GetNodeCountInGroup(XPOrb.GroupName);
		var line = new StringBuilder(512);
		line.Append($"PERF #{windowIndex++} {seconds:0.0}s fps={windowFrames / Math.Max(0.001, seconds):0.0}");
		line.Append($" frame={meanMs:0.00}ms p50={Percentile(0.5f):0.0} p95={Percentile(0.95f):0.0} p99={Percentile(0.99f):0.0} max={Percentile(1f):0.0}");
		line.Append($" | phys={physMs:0.00} proc={procMs:0.00} engine={engineMs:0.00} (render={renderMs:0.00}) steps/f={windowPhysicsSteps / frames:0.00}");
		line.Append($" | enemies={enemies} orbs={orbs} nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):0}");
		line.Append($" pairs={Performance.GetMonitor(Performance.Monitor.Physics2DCollisionPairs):0}");
		line.Append($" draws={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0}");
		line.Append($" | gc={GC.CollectionCount(0) - gen0}/{GC.CollectionCount(1) - gen1}/{GC.CollectionCount(2) - gen2} pause={pauseMs:0.0}ms alloc={allocated / frames / 1024.0:0.0}KB/f");
		GD.Print(line.ToString());

		var sections = new StringBuilder(512);
		sections.Append("PERF sections us/frame (calls/frame, KB allocated/frame):");
		for (int i = 0; i < (int)Section.Count; i++)
		{
			if (sectionCalls[i] == 0)
				continue;
			double scale = IsSampled(i) ? SampleEvery : 1;
			sections.Append($" {SectionNames[i]}={TicksToMs(sectionTicks[i]) * scale * 1000.0 / frames:0}({sectionCalls[i] * scale / frames:0.#},{sectionBytes[i] * scale / frames / 1024.0:0.#}K)");
		}
		GD.Print(sections.ToString());
		// Every sixth report, what the canvas is made of: what is costing the renderer.
		if ((windowIndex - 1) % 6 == 0)
			ReportCanvasCensus();
	}

	// THE CANVAS CENSUS. Every visible CanvasItem under the game, by class (script class where there
	// is one), with how many sit inside the camera's view - those are what the renderer draws each
	// frame and, on the Compatibility renderer, what decides the draw calls: consecutive items
	// batch only while they share a texture and material. A walk of the whole tree, so it runs once
	// in a while, not every report.
	private readonly System.Collections.Generic.Dictionary<string, (int All, int OnScreen)> census = new();

	private void ReportCanvasCensus()
	{
		Node? game = GetParent();
		Viewport viewport = GetViewport();
		if (game == null || viewport == null)
			return;
		Rect2 view = viewport.GetVisibleRect();
		Transform2D toWorld = viewport.GetCanvasTransform().AffineInverse();
		Vector2 a = toWorld * view.Position, b = toWorld * view.End;
		Rect2 world = new Rect2(a, b - a).Abs().Grow(64f);
		census.Clear();
		int total = 0, onScreen = 0;
		CountCanvas(game, world, ref total, ref onScreen);
		var line = new StringBuilder(512);
		line.Append($"PERF canvas: {total} visible items, {onScreen} in view {world.Size.X:0}x{world.Size.Y:0};");
		foreach (var pair in System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(census, p => p.Value.OnScreen), 14))
			line.Append($" {pair.Key}={pair.Value.OnScreen}/{pair.Value.All}");
		GD.Print(line.ToString());
	}

	private void CountCanvas(Node node, Rect2 world, ref int total, ref int onScreen)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is CanvasItem item)
			{
				if (!item.Visible)
					continue;
				string kind = child.GetScript().Obj is CSharpScript ? child.GetType().Name : child.GetClass();
				bool inView = child is Node2D n2d && world.HasPoint(n2d.GlobalPosition);
				census.TryGetValue(kind, out var counts);
				census[kind] = (counts.All + 1, counts.OnScreen + (inView ? 1 : 0));
				total++;
				if (inView)
					onScreen++;
			}
			if (child is CanvasLayer)
				continue;
			CountCanvas(child, world, ref total, ref onScreen);
		}
	}
}
