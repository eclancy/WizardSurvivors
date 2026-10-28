using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// The crater itself - the hole in the ground, not the fire in it: one quad per crater through
/// scenes/shaders/crater.gdshader, which draws the bowl, the lit rim, the cracks, the ejecta and the
/// heat in the 2 px art grid at any radius. Shared by Meteor Swarm (CraterLayer) and Cataclysm
/// (CataclysmField), which each own their craters' lifetimes and call <see cref="Draw"/>.
/// </summary>
/// <remarks>
/// A crater LIES ON THE GROUND, so whatever draws it sits at absolute z -15: over the terrain (-100)
/// and the walkable decals (-20), under the actors (0) and under everything that marks one from below
/// (the elite ring, the stance glow and the under-marks sit at -1 and -2). The rule Scorching Ray's
/// scorch set (ScorchingRayBeam.ScorchMark), and the bug this replaced: the meteor's crater was drawn
/// at z 6 and covered the wizard standing in it (2026-10-28).
///
/// The crater's numbers travel in each quad's vertex colour (heat, age, radius, how much is left), so
/// every crater on screen shares one material and one texture and the renderer batches them; nothing
/// is allocated per frame. The node drawing them must carry <see cref="Material"/> and keep its
/// modulate white - a modulate would multiply into the numbers.
/// </remarks>
public static class CraterGround
{
	public const string ShaderPath = "res://scenes/shaders/crater.gdshader";

	/// <summary>The bowl's foreshortening in the three-quarter view; crater.gdshader's SQUASH.</summary>
	public const float Squash = 0.84f;

	/// <summary>The rim's radius as a share of the hit radius (crater.gdshader's RIM_SHARE): the bowl
	/// a flame or a smoke puff stands in.</summary>
	public const float RimShare = 0.55f;

	/// <summary>Absolute z of the crater: on the ground, under every actor.</summary>
	public const int GroundZ = -15;

	/// <summary>Absolute z of the fire standing in it: just over the crater, still under every actor.</summary>
	public const int FireZ = -14;

	// The radius rides in a colour channel as radius / RadiusScale, so it must stay under 1.
	private const float RadiusScale = 1024f;
	// Seconds since the crater opened, likewise; only the first fifth of a second is read.
	private const float AgeScale = 4f;

	private static ShaderMaterial material;
	private static Texture2D white;

	/// <summary>The one material every crater quad is drawn with. Built on first use.</summary>
	public static ShaderMaterial Material
	{
		get
		{
			if (material != null && GodotObject.IsInstanceValid(material))
				return material;
			var shader = GD.Load<Shader>(ShaderPath);
			if (shader == null)
				GD.PrintErr($"CraterGround: {ShaderPath} did not load; craters will draw as plain quads.");
			material = new ShaderMaterial { Shader = shader };
			return material;
		}
	}

	// A one-pixel texture, so the quad has a UV running 0..1 across it (crater.gdshader reads its
	// position in the crater from it). Its colour is never sampled.
	private static Texture2D White
	{
		get
		{
			if (white != null && GodotObject.IsInstanceValid(white))
				return white;
			Image image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
			image.SetPixel(0, 0, Colors.White);
			white = ImageTexture.CreateFromImage(image);
			return white;
		}
	}

	/// <summary>
	/// Draws one crater into <paramref name="canvas"/> (which carries <see cref="Material"/>), centred on
	/// <paramref name="centre"/> in its local space. <paramref name="radius"/> is the radius the hit
	/// TESTED: nothing is drawn past it. <paramref name="heat"/> 1 white-hot to 0 cold,
	/// <paramref name="age"/> seconds since it opened, <paramref name="left"/> 1 whole to 0 gone.
	/// </summary>
	public static void Draw(CanvasItem canvas, Vector2 centre, float radius, float heat, float age, float left)
	{
		if (left <= 0f || radius < 8f)
			return;
		radius = MathF.Min(radius, RadiusScale - 1f);
		// On an even world pixel, so the crater's art grid is the world's.
		centre = new Vector2(MathF.Round(centre.X * 0.5f) * 2f, MathF.Round(centre.Y * 0.5f) * 2f);
		var extent = new Vector2(radius, radius * Squash);
		var parameters = new Color(Mathf.Clamp(heat, 0f, 1f), Mathf.Clamp(age / AgeScale, 0f, 1f),
			radius / RadiusScale, Mathf.Clamp(left, 0f, 1f));
		canvas.DrawTextureRect(White, new Rect2(centre - extent, extent * 2f), false, parameters);
	}

	/// <summary>
	/// The crater's heat <paramref name="age"/> seconds after it opened: white-hot for the flash, then
	/// either cooling at once (a plain impact) or held, flickering, while it burns for
	/// <paramref name="burnSeconds"/> and cooling after. <paramref name="seed"/> puts each crater's
	/// flicker on its own beat.
	/// </summary>
	public static float HeatAt(float age, float burnSeconds, float coolSeconds, uint seed)
	{
		const float flash = 0.1f;
		if (age < flash)
			return 1f;
		float flicker = 0.045f * MathF.Sin(age * 11f + seed * 0.37f) + 0.03f * MathF.Sin(age * 23f + seed * 1.3f);
		if (age < burnSeconds)
		{
			// Molten and holding: it eases down from the flash to a working heat over the first half second.
			float settle = Mathf.Clamp((age - flash) / 0.5f, 0f, 1f);
			return Mathf.Lerp(1f, 0.8f, settle) + flicker;
		}
		float from = burnSeconds > 0f ? 0.8f : 1f;
		float k = Mathf.Clamp((age - MathF.Max(flash, burnSeconds)) / MathF.Max(0.05f, coolSeconds), 0f, 1f);
		float cooled = from * MathF.Pow(1f - k, 1.6f);
		return cooled + flicker * cooled;
	}
}
