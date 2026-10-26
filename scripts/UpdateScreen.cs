using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using WizardSurvivors.scripts;
using HttpClient = System.Net.Http.HttpClient;

/// <summary>
/// The first thing a downloaded copy of the game shows: checks ecec.dev for a newer version and,
/// if there is one, installs it and restarts - no questions. Ported from DrawFight, where it was
/// built and tested end to end first.
///
/// How a release is laid out is in .ai/releasing.md. In short: ecec.dev/wizard-survivors/version.json
/// redirects to the newest GitHub Release's version.json, which names a small patch (the .pck and
/// the game's own DLL - everything a normal change touches) and the full zip (needed only when the
/// Godot engine itself changed). GitHub serves the files, so downloads cost nothing.
///
/// The running game cannot overwrite itself on Windows - its DLL and .pck are open - so the new
/// files are unpacked to user://update/ and a small script waits for the game to close, copies
/// them over, and starts it again. Linux does the same with a shell script, from its own pair of
/// downloads in version.json ("linux": the .tar.gz and a patch from the Linux export).
///
/// It is not a scene of its own. TitleScreen stays the main scene (run/main_scene, which every
/// harness and probe assumes) and lays this over the artwork before anything else on it starts;
/// when the check is done this frees itself and the title carries on exactly as it would have.
/// Every failure, from no internet onwards, just goes to the title. An update is never worth not
/// being able to play.
/// </summary>
public partial class UpdateScreen : Control
{
	/// <summary>The one address baked into every copy of the game. It is on ecec.dev rather than
	/// GitHub so where releases live can change without stranding old copies.</summary>
	public const string VersionUrl = "https://ecec.dev/wizard-survivors/version.json";

	/// <summary>Points a build at a test server instead - see .ai/releasing.md.</summary>
	public const string UrlOverrideVariable = "WIZARDSURVIVORS_UPDATE_URL";

	const string PckName = "WizardSurvivors.pck";

	[Signal] public delegate void FinishedEventHandler();

	public static string CurrentVersion =>
		ProjectSettings.GetSetting("application/config/version", "0.0.0").AsString();

	static string EngineVersion
	{
		get
		{
			var info = Engine.GetVersionInfo();
			return $"{info["major"]}.{info["minor"]}.{info["patch"]}";
		}
	}

	// Once per launch. Everything that returns "to the main menu" reloads TitleScreen, and a
	// second check every time the player quits a run would be a pause for nothing.
	static bool checkedThisLaunch;

	/// <summary>Only an exported Windows or Linux build updates itself, and only once per launch.
	/// Running from the editor or from `godot --path .` never does, and `--no-update` turns it off
	/// for testing a build. Android has UpdateBanner instead.</summary>
	public static bool ShouldCheck()
	{
		if (checkedThisLaunch) return false;
		if (!OS.HasFeature("template") || (!Windows && !Linux)) return false;
		return Array.IndexOf(OS.GetCmdlineUserArgs(), "--no-update") < 0
			&& Array.IndexOf(OS.GetCmdlineArgs(), "--no-update") < 0;
	}

	static bool Windows => OS.GetName() == "Windows";
	static bool Linux => OS.GetName() == "Linux";

	enum Phase { Checking, Downloading, Installing, Restart, Done }

	// Written by the worker thread, read by the main thread. Everything the screen needs is a
	// plain value, so there is nothing to lock.
	volatile Phase phase = Phase.Checking;
	volatile float progress;
	string newVersion = "";
	string applyScript = "";

	Label title;
	Label detail;
	ColorRect barBack;
	ColorRect barFill;
	int frames;
	bool finished;

	// The title screen's own ground (#05070C) and the bone-white and gold BonelightSkin uses for
	// menu type and focus, so this reads as the title screen not having finished arriving yet
	// rather than as a different program.
	static readonly Color Ground = new Color(0.019608f, 0.027451f, 0.047059f);
	static readonly Color Bone = new Color(0.94f, 0.93f, 0.88f);
	static readonly Color Quiet = new Color(0.70f, 0.72f, 0.78f);
	static readonly Color Gold = new Color(0.85f, 0.69f, 0.29f);

	const float BarWidth = 440f;
	const float BarHeight = 18f;

	public override void _Ready()
	{
		checkedThisLaunch = true;

		SetAnchorsPreset(LayoutPreset.FullRect);
		// Swallows clicks, so nothing on the title underneath can be pressed mid-update.
		MouseFilter = MouseFilterEnum.Stop;

		AddChild(new ColorRect
		{
			Color = Ground,
			AnchorRight = 1f,
			AnchorBottom = 1f,
			MouseFilter = MouseFilterEnum.Ignore,
		});

		title = CenteredLabel(-70f, 60f, ResponsiveLayout.TextRole.Title, Bone);
		detail = CenteredLabel(40f, 40f, ResponsiveLayout.TextRole.Body, Quiet);

		barBack = new ColorRect { Color = new Color(Bone, 0.18f), MouseFilter = MouseFilterEnum.Ignore, Visible = false };
		barFill = new ColorRect { Color = Gold, MouseFilter = MouseFilterEnum.Ignore, Visible = false };
		foreach (var bar in new[] { barBack, barFill })
		{
			bar.AnchorLeft = 0.5f;
			bar.AnchorRight = 0.5f;
			bar.AnchorTop = 0.5f;
			bar.AnchorBottom = 0.5f;
			bar.OffsetLeft = -BarWidth / 2f;
			bar.OffsetRight = BarWidth / 2f;
			bar.OffsetTop = 0f;
			bar.OffsetBottom = BarHeight;
			AddChild(bar);
		}

		Task.Run(Work);
	}

	Label CenteredLabel(float top, float height, ResponsiveLayout.TextRole role, Color color)
	{
		var label = new Label
		{
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 0.5f,
			AnchorBottom = 0.5f,
			OffsetTop = top,
			OffsetBottom = top + height,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		// In the tree first: SetFont asks the viewport whether this is a narrow screen.
		AddChild(label);
		ResponsiveLayout.SetFont(label, role);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	public override void _Process(double delta)
	{
		frames++;
		switch (phase)
		{
			case Phase.Done:
				if (finished) return;
				finished = true;
				EmitSignal(SignalName.Finished);
				QueueFree();
				return;

			case Phase.Restart:
				if (finished) return;
				finished = true;
				// The script waits for this process to end, so it has to be started first.
				if (Linux) OS.CreateProcess("/bin/sh", new[] { applyScript });
				else OS.CreateProcess("cmd.exe", new[] { "/c", applyScript });
				GetTree().Quit();
				return;

			case Phase.Checking:
				// Most checks finish before anyone could read this; it only shows when the
				// internet is slow, and then it says why the game has not started.
				title.Text = frames > 45 ? "Checking for updates..." : "";
				detail.Text = "";
				break;

			case Phase.Downloading:
				title.Text = "Updating Wizard Survivors";
				detail.Text = $"Getting version {newVersion}...  {Mathf.RoundToInt(progress * 100.0f)}%";
				ShowBar(progress);
				break;

			case Phase.Installing:
				title.Text = "Updating Wizard Survivors";
				detail.Text = "Nearly done - the game will restart by itself.";
				ShowBar(1f);
				break;
		}
	}

	void ShowBar(float filled)
	{
		barBack.Visible = true;
		barFill.Visible = true;
		barFill.OffsetRight = barFill.OffsetLeft + BarWidth * Mathf.Clamp(filled, 0f, 1f);
	}

	// --- The worker -----------------------------------------------------------------

	async Task Work()
	{
		try
		{
			phase = await Update() ? Phase.Restart : Phase.Done;
		}
		catch (Exception e)
		{
			GD.Print($"UpdateScreen: no update ({e.GetType().Name}: {e.Message})");
			phase = Phase.Done;
		}
	}

	/// <summary>True when a new version is unpacked and the restart script is written.</summary>
	async Task<bool> Update()
	{
		using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
		http.DefaultRequestHeaders.UserAgent.ParseAdd($"WizardSurvivors/{CurrentVersion}");

		string url = OS.GetEnvironment(UrlOverrideVariable);
		if (string.IsNullOrEmpty(url)) url = VersionUrl;
		using JsonDocument doc = JsonDocument.Parse(await http.GetStringAsync(url));
		JsonElement release = doc.RootElement;
		string latest = release.GetProperty("version").GetString() ?? "";
		if (!IsNewer(latest, CurrentVersion))
		{
			GD.Print($"UpdateScreen: {CurrentVersion} is up to date");
			return false;
		}

		string installDir = Path.GetDirectoryName(OS.GetExecutablePath());
		if (!CanWriteTo(installDir))
		{
			GD.Print($"UpdateScreen: {latest} is out, but {installDir} is not writable");
			return false;
		}

		// The patch only works on the engine it was built with; anything else takes the full zip.
		// Linux has its own pair - a release from before there was a Linux build has none, and then
		// there is nothing to update to.
		bool sameEngine = release.TryGetProperty("engine", out JsonElement engine)
			&& engine.GetString() == EngineVersion;
		JsonElement files = release;
		if (Linux && !release.TryGetProperty("linux", out files))
		{
			GD.Print($"UpdateScreen: {latest} has no Linux build");
			return false;
		}
		url = sameEngine && files.TryGetProperty("patch", out JsonElement patch)
			? patch.GetString()
			: files.GetProperty("full").GetString();

		newVersion = latest;
		phase = Phase.Downloading;

		string work = ProjectSettings.GlobalizePath("user://update");
		if (Directory.Exists(work)) Directory.Delete(work, true);
		Directory.CreateDirectory(work);
		bool tarball = url.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase);
		string zipPath = Path.Combine(work, tarball ? "update.tar.gz" : "update.zip");
		await Download(url, zipPath);

		phase = Phase.Installing;
		string unpacked = Path.Combine(work, "files");
		Directory.CreateDirectory(unpacked);
		if (tarball)
		{
			// The Linux download is a tarball, so the game binary keeps its permission to run.
			using FileStream file = File.OpenRead(zipPath);
			using var gz = new GZipStream(file, CompressionMode.Decompress);
			TarFile.ExtractToDirectory(gz, unpacked, overwriteFiles: true);
		}
		else
		{
			ZipFile.ExtractToDirectory(zipPath, unpacked);
		}
		File.Delete(zipPath);

		// The full zip keeps everything in a WizardSurvivors/ folder so it unzips tidily for a
		// person; the patch does not. Either way, copy from wherever the .pck is.
		string from = FindPck(unpacked);
		if (from == null) throw new InvalidDataException($"the update has no {PckName}");

		applyScript = Path.Combine(work, Linux ? "apply.sh" : "apply.cmd");
		File.WriteAllText(applyScript, Linux
			? ApplyScriptLinux(from, installDir, OS.GetExecutablePath(), OS.GetProcessId())
			: ApplyScript(from, installDir, OS.GetExecutablePath(), OS.GetProcessId()));
		GD.Print($"UpdateScreen: {CurrentVersion} -> {latest} ready, restarting");
		return true;
	}

	async Task Download(string url, string path)
	{
		// A separate client: the five-second timeout above is for the check, not for a full zip
		// on a slow connection.
		using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
		http.DefaultRequestHeaders.UserAgent.ParseAdd($"WizardSurvivors/{CurrentVersion}");
		using HttpResponseMessage response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
		response.EnsureSuccessStatusCode();
		long total = response.Content.Headers.ContentLength ?? 0;

		progress = 0.0f;
		using Stream source = await response.Content.ReadAsStreamAsync();
		using FileStream target = File.Create(path);
		var buffer = new byte[81920];
		long done = 0;
		int read;
		while ((read = await source.ReadAsync(buffer)) > 0)
		{
			await target.WriteAsync(buffer.AsMemory(0, read));
			done += read;
			if (total > 0) progress = done / (float)total;
		}
	}

	static string FindPck(string dir)
	{
		if (File.Exists(Path.Combine(dir, PckName))) return dir;
		foreach (string sub in Directory.GetDirectories(dir))
		{
			if (File.Exists(Path.Combine(sub, PckName))) return sub;
		}
		return null;
	}

	static bool CanWriteTo(string dir)
	{
		try
		{
			string probe = Path.Combine(dir, ".update-probe");
			File.WriteAllText(probe, "");
			File.Delete(probe);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>"0.10.0" is newer than "0.9.2": compared number by number, not as text.</summary>
	public static bool IsNewer(string candidate, string current)
	{
		string[] a = candidate.Split('.');
		string[] b = current.Split('.');
		for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
		{
			int x = i < a.Length && int.TryParse(a[i], out int ax) ? ax : 0;
			int y = i < b.Length && int.TryParse(b[i], out int by) ? by : 0;
			if (x != y) return x > y;
		}
		return false;
	}

	/// <summary>
	/// Waits for the game to close, copies the new files over the old, and starts it again.
	/// `ping` is the sleep because `timeout` refuses to run without a console to read from.
	/// robocopy retries a locked file a few times, which covers the moment Windows takes to let
	/// go of a closed process's files.
	/// </summary>
	static string ApplyScript(string from, string to, string exe, int pid) =>
		"@echo off\r\n" +
		":wait\r\n" +
		$"tasklist /FI \"PID eq {pid}\" 2>nul | find \"{pid}\" >nul\r\n" +
		"if not errorlevel 1 (\r\n" +
		"  ping -n 2 127.0.0.1 >nul\r\n" +
		"  goto wait\r\n" +
		")\r\n" +
		$"robocopy \"{from}\" \"{to}\" /E /R:10 /W:1 /NFL /NDL /NJH /NJS /NP >nul\r\n" +
		$"start \"\" \"{exe}\"\r\n" +
		$"rmdir /s /q \"{from}\"\r\n";

	/// <summary>
	/// The same on Linux: wait for the game to close, copy the new files over the old, make sure
	/// the game can still be run, and start it again. Linux would let the files be replaced while
	/// the game runs, but the game would then be half old and half new until it restarted.
	/// </summary>
	static string ApplyScriptLinux(string from, string to, string exe, int pid) =>
		"#!/bin/sh\n" +
		$"while kill -0 {pid} 2>/dev/null; do sleep 0.5; done\n" +
		$"cp -Rf {Quoted(from + "/.")} {Quoted(to + "/")}\n" +
		$"chmod +x {Quoted(exe)}\n" +
		$"rm -rf {Quoted(from)}\n" +
		$"cd {Quoted(to)} && nohup {Quoted(exe)} >/dev/null 2>&1 &\n";

	/// <summary>A path in single quotes for sh, with any quote in it kept.</summary>
	static string Quoted(string path) => "'" + path.Replace("'", "'\\''") + "'";
}
