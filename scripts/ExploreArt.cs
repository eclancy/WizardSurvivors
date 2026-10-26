using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace WizardSurvivors.scripts;

/// <summary>One sprite strip from tools/art/explore_props.py, with where its foot is.</summary>
/// <param name="Ground">Rows from the top of a frame to the ground line, in PNG pixels.</param>
/// <param name="AnchorX">The column of a frame that stands on the placement point.</param>
/// <param name="Flame">The bog lantern's flame centre in a frame's PNG pixels; zero for the rest.</param>
public sealed record ExploreSprite(string Id, Texture2D Texture, int Frames, int FrameW, int FrameH,
	int Ground, int AnchorX, Vector2 Flame, int LitFrames);

/// <summary>
/// The exploration art (.ai/side-events.md, "Finding the event"): the bog lantern, the three
/// wayfinders, the arrows' badges and glyphs, and the two soft textures every glow is drawn with.
/// </summary>
/// <remarks>
/// Where each sprite's foot and the lantern's flame are comes from explore.json, which the
/// generator writes beside the PNGs, so a redrawn lantern cannot leave its glow hanging beside the
/// cage. RegressionChecks.ValidateExploreArt holds the manifest, the PNGs and the glyph list here
/// against each other.
/// </remarks>
public static class ExploreArt
{
	public const string Manifest = "res://assets/bonelight/world/explore/explore.json";
	public const string WorldDirectory = "res://assets/bonelight/world/explore/";
	public const string PoiDirectory = "res://assets/bonelight/ui/poi/";

	/// <summary>Every pictogram an arrow can carry; tools/art/explore_props.py GLYPHS draws the same set.</summary>
	public static readonly string[] Glyphs = { "lantern", "grove", "cell", "ice", "caravan", "compass", "map", "cairn", "well" };

	public static string GlyphPath(string glyph) => PoiDirectory + "poi-" + glyph + ".png";

	/// <summary>The arrow's badge: arcane cyan for his places, gold for a wayfinder seen and not yet taken.</summary>
	public static string BadgePath(bool wayfinder) => PoiDirectory + (wayfinder ? "poi-badge-found.png" : "poi-badge-his.png");

	private static Dictionary<string, ExploreSprite> cache;

	/// <summary>A sprite by id ("bog-lantern", "arcane-compass", ...), or null when missing.</summary>
	public static ExploreSprite Get(string id)
	{
		cache ??= Load();
		return cache.TryGetValue(id, out ExploreSprite sprite) ? sprite : null;
	}

	/// <summary>Reads the manifest afresh. Every PNG it names is loaded, and null when it is not imported.</summary>
	public static Dictionary<string, ExploreSprite> Load()
	{
		var result = new Dictionary<string, ExploreSprite>();
		string text = FileAccess.GetFileAsString(Manifest);
		if (string.IsNullOrEmpty(text))
			return result;
		using JsonDocument doc = JsonDocument.Parse(text);
		if (!doc.RootElement.TryGetProperty("sprites", out JsonElement sprites))
			return result;
		foreach (JsonElement s in sprites.EnumerateArray())
		{
			string id = s.GetProperty("id").GetString();
			string path = WorldDirectory + id + ".png";
			Texture2D texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
			int Int(string name) => s.TryGetProperty(name, out JsonElement v) ? v.GetInt32() : 0;
			Vector2 flame = Vector2.Zero;
			if (s.TryGetProperty("flame", out JsonElement f) && f.GetArrayLength() == 2)
				flame = new Vector2(f[0].GetInt32(), f[1].GetInt32());
			result[id] = new ExploreSprite(id, texture, Int("frames"), Int("frame_w"), Int("frame_h"),
				Int("ground"), Int("anchor_x"), flame, Int("lit_frames"));
		}
		return result;
	}

	/// <summary>
	/// A Sprite2D standing on <paramref name="groundLocal"/> (in its parent's space), sorted at its
	/// foot the way trees and the flower pen are (WorldSprites.SortLift): its node sits SortLift above
	/// the ground line, so the parent's y-sort draws the wizard in front of it exactly when his boots
	/// are south of its foot.
	/// </summary>
	public static Sprite2D MakeGrounded(ExploreSprite art, Vector2 groundLocal)
	{
		return new Sprite2D
		{
			Texture = art.Texture,
			Hframes = Mathf.Max(1, art.Frames),
			Centered = false,
			Position = new Vector2(Mathf.Round(groundLocal.X), Mathf.Round(groundLocal.Y - WorldSprites.SortLift)),
			Offset = new Vector2(-art.AnchorX, WorldSprites.SortLift - art.Ground),
		};
	}

	/// <summary>The flame's offset from the lantern's ground point, in world pixels (drawn at scale 1).</summary>
	public static Vector2 FlameOffset(ExploreSprite art) => art.Flame - new Vector2(art.AnchorX, art.Ground);

	// --- light ---------------------------------------------------------------------------------
	//
	// Every glow in the exploration set - the lanterns, the far tells, the wayfinders - is one of
	// these two white textures drawn ADDITIVELY and tinted by modulate: one draw call a glow, built
	// once, never per frame. They are smooth on purpose and drawn with linear filtering, because
	// light is not a sprite: a stepped halo the size of a screen would read as a stamped disc.

	private static Texture2D radial;
	private static Texture2D shaft;

	/// <summary>A round falloff: white, alpha (1 - d)^2 from the centre to the rim.</summary>
	public static Texture2D Radial => radial ??= BuildRadial();

	/// <summary>A tall soft column: brightest at its foot (the bottom edge), fading upward and to the sides.</summary>
	public static Texture2D Shaft => shaft ??= BuildShaft();

	private static Texture2D BuildRadial()
	{
		const int size = 128;
		Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		float c = (size - 1) * 0.5f;
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float d = Mathf.Min(1f, new Vector2(x - c, y - c).Length() / c);
				float a = (1f - d) * (1f - d);
				image.SetPixel(x, y, new Color(1f, 1f, 1f, a));
			}
		}
		return ImageTexture.CreateFromImage(image);
	}

	private static Texture2D BuildShaft()
	{
		const int w = 32, h = 128;
		Image image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		float c = (w - 1) * 0.5f;
		for (int y = 0; y < h; y++)
		{
			// y = h - 1 is the foot.
			float up = 1f - y / (float)(h - 1);
			float along = Mathf.Pow(1f - up, 1.6f);
			for (int x = 0; x < w; x++)
			{
				float across = 1f - Mathf.Min(1f, Mathf.Abs(x - c) / c);
				image.SetPixel(x, y, new Color(1f, 1f, 1f, along * across * across));
			}
		}
		return ImageTexture.CreateFromImage(image);
	}

	/// <summary>Draws a round glow of <paramref name="radius"/> on <paramref name="canvas"/>.</summary>
	public static void DrawGlow(CanvasItem canvas, Vector2 centre, float radius, Color color)
	{
		if (radius < 1f || color.A <= 0.002f)
			return;
		canvas.DrawTextureRect(Radial, new Rect2(centre - new Vector2(radius, radius), new Vector2(radius, radius) * 2f), false, color);
	}

	/// <summary>Draws a column of light standing on <paramref name="foot"/>, <paramref name="height"/> tall.</summary>
	public static void DrawShaft(CanvasItem canvas, Vector2 foot, float halfWidth, float height, Color color)
	{
		if (height < 2f || color.A <= 0.002f)
			return;
		canvas.DrawTextureRect(Shaft, new Rect2(foot - new Vector2(halfWidth, height), new Vector2(halfWidth * 2f, height)), false, color);
	}

	private static CanvasItemMaterial additive;

	/// <summary>A CanvasItemMaterial that adds, shared by every glow layer. Built on first use, not
	/// in a static initialiser, so reading the manifest (RegressionChecks) never creates engine objects.</summary>
	public static CanvasItemMaterial Additive =>
		additive != null && GodotObject.IsInstanceValid(additive)
			? additive
			: additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
}
