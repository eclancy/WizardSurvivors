using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// A cloud of spores or marsh gas drawn as pixel art: one quad through
/// <c>scenes/shaders/spore_cloud.gdshader</c>, carrying its numbers in its vertices so every cloud a node
/// draws shares one material and one batch. Contagion's fever clouds and plague wind and Mirefoot's Fen Gas
/// (2026-10-29, the growth spells' pixel-art pass).
/// </summary>
/// <remarks>
/// THE RADIUS PASSED IN MUST BE THE ONE THE CALLER TESTS: the shader lays the cloud's outer lobes so their
/// edges land on it and never past it. Draw into a node whose material is <see cref="MakeMaterial"/>'s and
/// that draws nothing else (a cloud's quad means nothing to any other shader, and anything else drawn there
/// would go through this one). The quad buffers are shared, so nothing allocates per frame.
/// </remarks>
public static class GasCloud
{
	private static Shader shader;
	private static readonly StringName ParamCore = "ramp_core";
	private static readonly StringName ParamHot = "ramp_hot";
	private static readonly StringName ParamMid = "ramp_mid";
	private static readonly StringName ParamEdge = "ramp_edge";
	private static readonly StringName ParamTime = "time";

	private static readonly Vector2[] Quad = new Vector2[4];
	private static readonly Vector2[] QuadUv = new Vector2[4];
	private static readonly Color[] QuadColours = new Color[4];

	/// <summary>A material for clouds of <paramref name="ramp"/>'s element.</summary>
	public static ShaderMaterial MakeMaterial(in ElementColors.Ramp ramp)
	{
		shader ??= GD.Load<Shader>("res://scenes/shaders/spore_cloud.gdshader");
		var material = new ShaderMaterial { Shader = shader };
		Paint(material, ramp);
		return material;
	}

	/// <summary>Repaints <paramref name="material"/>'s clouds in <paramref name="ramp"/>.</summary>
	public static void Paint(ShaderMaterial material, in ElementColors.Ramp ramp)
	{
		material.SetShaderParameter(ParamCore, ramp.Core);
		material.SetShaderParameter(ParamHot, ramp.Hot);
		material.SetShaderParameter(ParamMid, ramp.Mid);
		material.SetShaderParameter(ParamEdge, ramp.Edge);
	}

	/// <summary>The clock the clouds turn and breathe on.</summary>
	public static void SetTime(ShaderMaterial material, float seconds) => material.SetShaderParameter(ParamTime, seconds);

	/// <summary>
	/// One cloud at <paramref name="centre"/> in <paramref name="canvas"/>'s frame, reaching exactly
	/// <paramref name="radius"/>. <paramref name="thin"/> 0 whole .. 1 gone; <paramref name="seed"/> any
	/// number (its fraction is used); <paramref name="density"/> how much of a lobe's heart is gas, 0..1.
	/// </summary>
	public static void Draw(CanvasItem canvas, Vector2 centre, float radius, float thin, float seed, float density)
	{
		if (radius < PixelCells.Grid * 2f || thin >= 1f || density <= 0f)
			return;
		float r = radius + PixelCells.Grid;
		Quad[0] = centre + new Vector2(-r, -r);
		Quad[1] = centre + new Vector2(r, -r);
		Quad[2] = centre + new Vector2(r, r);
		Quad[3] = centre + new Vector2(-r, r);
		QuadUv[0] = new Vector2(-r, -r);
		QuadUv[1] = new Vector2(r, -r);
		QuadUv[2] = new Vector2(r, r);
		QuadUv[3] = new Vector2(-r, r);
		var data = new Color(Mathf.Clamp(thin, 0f, 1f), Mathf.PosMod(seed, 1f), Mathf.Clamp(radius / 256f, 0f, 1f), Mathf.Clamp(density, 0f, 1f));
		QuadColours[0] = QuadColours[1] = QuadColours[2] = QuadColours[3] = data;
		canvas.DrawPrimitive(Quad, QuadColours, QuadUv);
	}
}
