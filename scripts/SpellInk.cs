using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// The pixel-art pass's helpers for the trap and star spells (2026-10-29: Gravewell, Glacial Spike,
/// Iron Palisade, Hollow Star, Starfall, the Familiar, Mirefoot's wisps, Gale Blade's Thunderhead and the
/// Lattice). Eric, on screenshots: "does not look up to our typical visual fidelity", then "Update all
/// spells to the same quality we expect now".
/// </summary>
/// <remarks>
/// <para><b>Fades are dither.</b> <see cref="DitherByCell"/> and <see cref="DitherByTexel"/> are the
/// material a canvas draws through so that the alpha its draw calls carry is read as coverage
/// (scenes/shaders/spell_dither.gdshader): a fading mote thins out pixel by pixel, opaque, rather than
/// going translucent over what is under it. One shared material each - assigning it allocates nothing.</para>
///
/// <para><b>The grid is the world's.</b> <c>WhipLimb.Cell</c> snaps to the 2 px grid of the canvas it is
/// drawn in, which is the world's only when the canvas stands on an even world position. A trap or a well
/// stands wherever an enemy was, so <see cref="AlignToWorldGrid"/> shifts the canvas's drawing by under an
/// art pixel to put its grid on the world's - the stamped cells then line up with the floor, with every
/// shader-drawn part (which snaps to the world cell itself) and with each other across nodes. It moves
/// nothing that is tested: only where the cells are painted.</para>
/// </remarks>
public static class SpellInk
{
	public const float Grid = 2f;
	public const string DitherShaderPath = "res://scenes/shaders/spell_dither.gdshader";

	private static ShaderMaterial byCell;
	private static ShaderMaterial byTexel;
	private static readonly StringName ParamByTexel = "by_texel";

	/// <summary>Alpha as coverage on the world's 2 px cell: for canvases of stamped rects.</summary>
	public static ShaderMaterial DitherByCell => byCell ??= Make(false);

	/// <summary>Alpha as coverage on the texture's own texel: for a sprite drawn at x2.</summary>
	public static ShaderMaterial DitherByTexel => byTexel ??= Make(true);

	private static ShaderMaterial Make(bool texel)
	{
		var shader = GD.Load<Shader>(DitherShaderPath);
		if (shader == null)
		{
			GD.PushError($"SpellInk: {DitherShaderPath} failed to load.");
			return null;
		}
		var material = new ShaderMaterial { Shader = shader };
		material.SetShaderParameter(ParamByTexel, texel);
		return material;
	}

	/// <summary>
	/// The offset that puts an unrotated, unscaled canvas's 2 px grid on the world's: under an art pixel
	/// either way.
	/// </summary>
	public static Vector2 GridOffset(Node2D canvas)
	{
		Vector2 g = canvas.GlobalPosition;
		return new Vector2(MathF.Round(g.X / Grid) * Grid - g.X, MathF.Round(g.Y / Grid) * Grid - g.Y);
	}

	/// <summary>Starts a _Draw on the world's grid (see the remarks). Call it before any cell is drawn.</summary>
	public static void AlignToWorldGrid(Node2D canvas) => canvas.DrawSetTransform(GridOffset(canvas));

	/// <summary>A value snapped to the 2 px grid.</summary>
	public static float Snap(float v) => MathF.Round(v / Grid) * Grid;

	/// <summary>A point snapped to the 2 px grid.</summary>
	public static Vector2 Snap(Vector2 p) => new Vector2(Snap(p.X), Snap(p.Y));

	/// <summary>An opaque colour, multiplied by a tint and carrying <paramref name="coverage"/> as its alpha.</summary>
	public static Color Ink(Color c, Color tint, float coverage) =>
		new Color(c.R * tint.R, c.G * tint.G, c.B * tint.B, Mathf.Clamp(coverage, 0f, 1f));
}
