using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace WizardSurvivors.scripts;

/// <summary>One curated prop (mine/torture set) with the gameplay flags from its metadata.</summary>
public sealed class PropEntry
{
	public string Id { get; init; }
	public string ResPath { get; init; }
	public string Placement { get; init; }   // floor / wall / ornament
	public bool IsObstacle { get; init; }     // blocks movement/shots vs walkable decoration
	public bool Hazard { get; init; }
	public string Theme { get; init; }        // flora / crystal / treasure / mining / bones / funerary / torture
	public int Width { get; init; }
	public int Height { get; init; }
}

/// <summary>
/// Loads curated prop sets (mines, torture) from their metadata.json files and indexes
/// them by placement so a scatter pass can place walkable decorations and obstacle props
/// with the correct collision behaviour. Optionally filters to a set of themes so each
/// stage only spawns thematically appropriate props (e.g. flora in a forest).
/// </summary>
public sealed class PropCatalog
{
	private readonly List<PropEntry> _floorProps = new();

	public IReadOnlyList<PropEntry> FloorProps => _floorProps;

	/// <summary>Load props from the given metadata files. If <paramref name="themes"/> is
	/// non-null and non-empty, only props whose propTheme is in the set are kept.</summary>
	public static PropCatalog Load(IReadOnlyCollection<string> themes, params string[] metadataResPaths)
	{
		var catalog = new PropCatalog();
		HashSet<string> filter = (themes != null && themes.Count > 0)
			? new HashSet<string>(themes)
			: null;
		foreach (string path in metadataResPaths)
			catalog.LoadSet(path, filter);
		return catalog;
	}

	/// <summary>Load all floor/ornament props from the given metadata files (no theme filter).</summary>
	public static PropCatalog Load(params string[] metadataResPaths)
		=> Load(null, metadataResPaths);

	private void LoadSet(string metadataResPath, HashSet<string> themeFilter)
	{
		string text = Godot.FileAccess.GetFileAsString(metadataResPath);
		if (string.IsNullOrEmpty(text))
		{
			GD.PushWarning($"PropCatalog: could not read '{metadataResPath}'.");
			return;
		}

		using var doc = JsonDocument.Parse(text);
		if (!doc.RootElement.TryGetProperty("frames", out var frames)
			|| frames.ValueKind != JsonValueKind.Array)
			return;

		foreach (var f in frames.EnumerateArray())
		{
			if (!f.TryGetProperty("id", out var idEl)) continue;
			string path = f.TryGetProperty("path", out var p) ? p.GetString() : null;
			if (string.IsNullOrEmpty(path)) continue;

			string placement = f.TryGetProperty("placement", out var pl) ? pl.GetString() : "floor";
			// Only floor and ornament props scatter onto open ground; walls are structural.
			if (placement != "floor" && placement != "ornament") continue;

			string theme = f.TryGetProperty("propTheme", out var th) ? th.GetString() : "misc";
			if (themeFilter != null && !themeFilter.Contains(theme)) continue;

			string collision = f.TryGetProperty("collision", out var c) ? c.GetString() : "decoration";
			bool hazard = f.TryGetProperty("hazard", out var hz) && hz.ValueKind == JsonValueKind.True;
			int w = f.TryGetProperty("width", out var wEl) ? wEl.GetInt32() : 48;
			int h = f.TryGetProperty("height", out var hEl) ? hEl.GetInt32() : 48;

			_floorProps.Add(new PropEntry
			{
				Id = idEl.GetString(),
				ResPath = path.StartsWith("res://") ? path : "res://" + path,
				Placement = placement,
				IsObstacle = collision == "obstacle",
				Hazard = hazard,
				Theme = theme,
				Width = w,
				Height = h,
			});
		}
	}
}
