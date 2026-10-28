using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// A world sprite that stands on the ground at a line of its own - a tree, a bush, a piece of the
/// flower pen - read from the manifest its generator writes beside the PNGs, so the art and the
/// code agree on where its foot is without a constant in either.
/// </summary>
/// <param name="Ground">The contact line, in pixels of the PNG (rows from its top).</param>
/// <param name="AnchorX">The column that stands on the placement point: a tree's trunk, a side
/// segment's posts. 0 for a sprite placed by its left edge.</param>
/// <param name="Trunk">For a tree, the width in world pixels of the only part that blocks; 0 for
/// anything you walk through.</param>
public sealed record GroundedSprite(string Id, string Kind, Texture2D Texture, int Width, int Height,
	int Ground, int AnchorX, int Trunk);

/// <summary>
/// Loads tools/art/trees.py's and tools/art/ground_detail.py's manifests, and holds the one number
/// every grounded sprite is sorted by.
/// </summary>
/// <remarks>
/// SORTING AT THE FEET (.ai/terrain.md section 10). Everything in a chunk sorts by its node's y.
/// The wizard's node is the MIDDLE of his sprite, and his boots - the footprint that meets the
/// ground, player.tscn's Feet, centred 36 px below it - are what a fence or a trunk actually stops.
/// So a grounded sprite's node is put <see cref="SortLift"/> above its ground line, and its texture
/// hangs that far below the node: the wizard is drawn in front of a trunk exactly when his
/// footprint is south of the trunk's foot, and behind a crown exactly when he stands north of it.
/// The old decor sorted at its own middle against his middle, which is why a 300 px tree could not
/// be walked behind. Enemies' origins sit a few pixels nearer their feet than his do; at 36 the
/// error for a 32 px basic enemy is about 6 px, which nothing on screen shows.
/// </remarks>
public static class WorldSprites
{
	public const string TreesManifest = "res://assets/bonelight/world/trees/trees.json";
	public const string PenManifest = "res://assets/bonelight/world/landmarks/flower-pen.json";

	/// <summary>How far above its ground line a grounded sprite's node (its sort key) sits.</summary>
	public const float SortLift = 36f;

	/// <summary>Every tree and bush, by id. Missing textures load as null (see RegressionChecks).</summary>
	public static Dictionary<string, GroundedSprite> LoadTrees()
	{
		var result = new Dictionary<string, GroundedSprite>();
		string text = FileAccess.GetFileAsString(TreesManifest);
		if (string.IsNullOrEmpty(text))
			return result;
		using JsonDocument doc = JsonDocument.Parse(text);
		if (!doc.RootElement.TryGetProperty("sprites", out JsonElement sprites))
			return result;
		foreach (JsonElement s in sprites.EnumerateArray())
		{
			GroundedSprite sprite = Read(s.GetProperty("id").GetString(), s);
			result[sprite.Id] = sprite;
		}
		return result;
	}

	/// <summary>The flower pen's layout numbers and its parts (back, front, side, post, flower, garden).</summary>
	public sealed class Pen
	{
		public int CellsW, CellsH, GateX, GateW, SideGateY, SideGateH, Line, Segment;
		public readonly Dictionary<string, GroundedSprite> Parts = new();
	}

	public static Pen LoadPen()
	{
		string text = FileAccess.GetFileAsString(PenManifest);
		if (string.IsNullOrEmpty(text))
			return null;
		using JsonDocument doc = JsonDocument.Parse(text);
		JsonElement root = doc.RootElement;
		var pen = new Pen
		{
			CellsW = root.GetProperty("cells_w").GetInt32(),
			CellsH = root.GetProperty("cells_h").GetInt32(),
			GateX = root.GetProperty("gate_x").GetInt32(),
			GateW = root.GetProperty("gate_w").GetInt32(),
			// -1 from a manifest drawn before the pen had side gates, which ValidateGroundArt reports.
			SideGateY = root.TryGetProperty("side_gate_y", out JsonElement sy) ? sy.GetInt32() : -1,
			SideGateH = root.TryGetProperty("side_gate_h", out JsonElement sh) ? sh.GetInt32() : -1,
			Line = root.GetProperty("line").GetInt32(),
			Segment = root.GetProperty("segment").GetInt32(),
		};
		foreach (JsonProperty p in root.EnumerateObject())
		{
			if (p.Value.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("path", out _))
				pen.Parts[p.Name] = Read(p.Name, p.Value);
		}
		return pen;
	}

	private static GroundedSprite Read(string id, JsonElement s)
	{
		string path = s.GetProperty("path").GetString();
		Texture2D texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		int Int(string name) => s.TryGetProperty(name, out JsonElement v) ? v.GetInt32() : 0;
		string kind = s.TryGetProperty("kind", out JsonElement k) ? k.GetString() : "part";
		return new GroundedSprite(id, kind, texture, Int("width"), Int("height"), Int("ground"), Int("anchor_x"), Int("trunk"));
	}
}
