using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// A burn on the floor - Scorching Ray's scorch where it struck and the char under Cinderbreath's fire -
/// drawn as pixel art through scenes/shaders/cinder_scorch.gdshader, one quad per burn (2026-10-29, the
/// pixel-art pass: both were translucent squashed DrawCircles with antialiased cracks).
/// </summary>
/// <remarks>
/// Built as <see cref="CraterGround"/> is: the burn's numbers ride in the quad's vertex colour (glow, how
/// much is left, radius, seed), so every burn on screen shares one material and batches, and nothing is
/// allocated a frame. The node drawing them must carry <see cref="Material"/> and keep its modulate white -
/// a modulate would multiply into the numbers.
/// </remarks>
public static class ScorchGround
{
	public const string ShaderPath = "res://scenes/shaders/cinder_scorch.gdshader";

	// The radius rides in a colour channel as radius / RadiusScale, so it must stay under 1.
	private const float RadiusScale = 256f;

	private static ShaderMaterial material;
	private static Texture2D white;

	/// <summary>The one material every burn quad is drawn with. Built on first use.</summary>
	public static ShaderMaterial Material
	{
		get
		{
			if (material != null && GodotObject.IsInstanceValid(material))
				return material;
			var shader = GD.Load<Shader>(ShaderPath);
			if (shader == null)
				GD.PrintErr($"ScorchGround: {ShaderPath} did not load; burns will draw as plain quads.");
			material = new ShaderMaterial { Shader = shader };
			return material;
		}
	}

	/// <summary>
	/// A one-pixel white texture, so a quad drawn with it has a UV running 0..1 across it - for the other
	/// per-quad shaders of these spells (ward_wisp.gdshader, spirit_quake.gdshader). Never sampled.
	/// </summary>
	public static Texture2D QuadTexture => White;

	// A one-pixel texture, so the quad has a UV running 0..1 across it. Its colour is never sampled.
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
	/// Draws one burn into <paramref name="canvas"/> (which carries <see cref="Material"/>), centred on
	/// <paramref name="centre"/> in its local space. Nothing is drawn past <paramref name="radius"/> (squashed
	/// to <see cref="CraterGround.Squash"/> in y, the floor's foreshortening). <paramref name="glow"/> 1
	/// white-hot cracks to 0 cold char; <paramref name="left"/> 1 whole to 0 gone; <paramref name="seed"/>
	/// any number, so no two burns crack alike.
	/// </summary>
	public static void Draw(CanvasItem canvas, Vector2 centre, float radius, float glow, float left, uint seed)
	{
		if (left <= 0f || radius < 4f)
			return;
		radius = MathF.Min(radius, RadiusScale - 1f);
		// On an even world pixel, so the burn's art grid is the world's.
		centre = new Vector2(MathF.Round(centre.X * 0.5f) * 2f, MathF.Round(centre.Y * 0.5f) * 2f);
		var extent = new Vector2(radius, radius * CraterGround.Squash);
		var parameters = new Color(Mathf.Clamp(glow, 0f, 1f), Mathf.Clamp(left, 0f, 1f), radius / RadiusScale,
			(seed % 997u) / 997f);
		canvas.DrawTextureRect(White, new Rect2(centre - extent, extent * 2f), false, parameters);
	}
}
