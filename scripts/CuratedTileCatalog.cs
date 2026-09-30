using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// One paintable tile from the curated dungeon-floor metadata. For autotile tiles,
/// <see cref="Terrain"/> and <see cref="Role"/> are set (Role matches
/// <see cref="TerrainAutotiler.RoleKey"/>); for plain ground variants they are null.
/// </summary>
public sealed class CuratedTileEntry
{
	public string Id { get; init; }
	public string ResPath { get; init; }
	public string Terrain { get; init; }
	public string Role { get; init; }
	public string Base { get; init; }
	public bool Hazard { get; init; }

	/// <summary>
	/// True if the player cannot walk into this tile. Independent of <see cref="Hazard"/>: water
	/// blocks and does not hurt, lava hurts and does not block.
	/// </summary>
	public bool Blocking { get; init; }
}

/// <summary>
/// Loads the curated dungeon-floor <c>metadata.json</c> and indexes it into a
/// (terrain, role) -> tile lookup plus a per-material list of plain ground variants.
/// This is the bridge between <see cref="TerrainAutotiler"/> (which decides a role per
/// cell) and the actual tile textures a <see cref="LevelTilePainter"/> paints.
/// </summary>
public sealed class CuratedTileCatalog
{
	private readonly Dictionary<string, Dictionary<string, CuratedTileEntry>> _autotile = new();
	private readonly Dictionary<string, List<CuratedTileEntry>> _ground = new();
	private readonly Dictionary<string, List<CuratedTileEntry>> _terrainFills = new();

	public IReadOnlyCollection<string> Terrains => _autotile.Keys;

	/// <summary>
	/// Loads several manifests into one catalog, later files adding to earlier ones.
	/// </summary>
	/// <remarks>
	/// The project has one bought tile pack and, since the Sketchbook, one manifest of its own.
	/// Merging rather than editing the pack's file is deliberate: a manifest is an inventory of
	/// what somebody else shipped, and appending our tiles to it is how an asset update quietly
	/// deletes them.
	/// </remarks>
	public static CuratedTileCatalog LoadMany(params string[] metadataResPaths)
	{
		var catalog = new CuratedTileCatalog();
		foreach (string path in metadataResPaths)
		{
			if (!string.IsNullOrWhiteSpace(path))
				catalog.Merge(path);
		}

		return catalog;
	}

	public static CuratedTileCatalog Load(string metadataResPath) => LoadMany(metadataResPath);

	private void Merge(string metadataResPath)
	{
		CuratedTileCatalog catalog = this;
		string text = Godot.FileAccess.GetFileAsString(metadataResPath);
		if (string.IsNullOrEmpty(text))
		{
			GD.PushError($"CuratedTileCatalog: could not read '{metadataResPath}'.");
			return;
		}

		using var doc = JsonDocument.Parse(text);
		if (!doc.RootElement.TryGetProperty("frames", out var frames)
			|| frames.ValueKind != JsonValueKind.Array)
		{
			GD.PushError($"CuratedTileCatalog: '{metadataResPath}' has no 'frames' array.");
			return;
		}

		foreach (var f in frames.EnumerateArray())
		{
			if (!f.TryGetProperty("id", out var idEl)) continue;
			string id = idEl.GetString();
			string path = f.TryGetProperty("path", out var p) ? p.GetString() : null;
			if (string.IsNullOrEmpty(path)) continue;
			string resPath = path.StartsWith("res://") ? path : "res://" + path;
			bool hazard = f.TryGetProperty("hazard", out var hz) && hz.ValueKind == JsonValueKind.True;
			bool blocking = f.TryGetProperty("blocking", out var bk) && bk.ValueKind == JsonValueKind.True;

			if (f.TryGetProperty("autotile", out var at) && at.ValueKind == JsonValueKind.Object)
			{
				string terrain = at.TryGetProperty("terrain", out var te) ? te.GetString() : null;
				string role = at.TryGetProperty("role", out var re) ? re.GetString() : null;
				if (string.IsNullOrEmpty(terrain) || string.IsNullOrEmpty(role)) continue;
				string baseId = at.TryGetProperty("base", out var b) ? b.GetString() : null;

				if (!catalog._autotile.TryGetValue(terrain, out var roles))
					catalog._autotile[terrain] = roles = new Dictionary<string, CuratedTileEntry>();
				var entry = new CuratedTileEntry
				{
					Id = id, ResPath = resPath, Terrain = terrain, Role = role, Base = baseId,
					Hazard = hazard, Blocking = blocking,
				};
				roles[role] = entry;

				// Collect every fill-role tile per terrain (kept separately since roles[] dedups),
				// so a terrain like grass can be used as a solid base ground layer.
				if (role == "fill")
				{
					if (!catalog._terrainFills.TryGetValue(terrain, out var fills))
						catalog._terrainFills[terrain] = fills = new List<CuratedTileEntry>();
					fills.Add(entry);
				}
				continue;
			}

			// Plain ground variant (no autotile block): index by materialFamily for base painting.
			string material = f.TryGetProperty("materialFamily", out var mf) ? mf.GetString() : null;
			string placement = f.TryGetProperty("placement", out var pl) ? pl.GetString() : "floor";
			if (string.IsNullOrEmpty(material) || placement != "floor") continue;

			if (!catalog._ground.TryGetValue(material, out var list))
				catalog._ground[material] = list = new List<CuratedTileEntry>();
			list.Add(new CuratedTileEntry
			{
				Id = id, ResPath = resPath, Base = "floor", Hazard = hazard,
			});
		}
	}

	/// <summary>
	/// Look up the tile for a resolved (terrain, role). Falls back to the terrain's
	/// "fill" tile when the exact role is missing (e.g. a tileset without patch tiles).
	/// </summary>
	public bool TryGetTile(string terrain, string roleKey, out CuratedTileEntry entry)
	{
		entry = null;
		if (!_autotile.TryGetValue(terrain, out var roles)) return false;
		if (roles.TryGetValue(roleKey, out entry)) return true;
		return roles.TryGetValue("fill", out entry);
	}

	/// <summary>Plain ground variants for a material (e.g. "sand"), for the base layer.</summary>
	public IReadOnlyList<CuratedTileEntry> GroundVariants(string material)
	{
		if (_ground.TryGetValue(material, out var list) && list.Count > 0) return list;
		foreach (var kv in _ground)
			if (kv.Value.Count > 0) return kv.Value;
		return Array.Empty<CuratedTileEntry>();
	}

	/// <summary>
	/// All fill-role tiles for an autotile terrain (e.g. grass), so the terrain can be used
	/// as a solid base ground layer. Returns empty if the terrain has no fills.
	/// </summary>
	public IReadOnlyList<CuratedTileEntry> FillTilesFor(string terrain)
	{
		if (!string.IsNullOrEmpty(terrain) && _terrainFills.TryGetValue(terrain, out var list) && list.Count > 0)
			return list;
		return Array.Empty<CuratedTileEntry>();
	}

	/// <summary>True if any tile of this terrain is flagged as a hazard (e.g. lava, pit).</summary>
	public bool IsHazardTerrain(string terrain)
	{
		if (!_autotile.TryGetValue(terrain, out var roles)) return false;
		foreach (var e in roles.Values)
			if (e.Hazard) return true;
		return false;
	}

	/// <summary>True if this terrain cannot be walked into (e.g. water).</summary>
	public bool IsBlockingTerrain(string terrain)
	{
		if (!_autotile.TryGetValue(terrain, out var roles)) return false;
		foreach (var e in roles.Values)
			if (e.Blocking) return true;
		return false;
	}
}
