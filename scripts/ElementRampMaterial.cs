using Godot;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

/// <summary>
/// One shared material per element that repaints a four-stop effect sprite in that element's ramp
/// (scenes/shaders/element_ramp.gdshader). For sprite-drawn spells whose element path a multiply tint
/// cannot show - a Modulate can darken orange fire, never turn it violet. Cinderbreath's Hellbreath
/// is the first user.
/// </summary>
/// <remarks>
/// Cached, so a burn that throws sixteen tongues a second shares one material rather than making one
/// per puff: one shader, one material per element ever used, nothing allocated per frame.
/// </remarks>
public static class ElementRampMaterial
{
	private static Shader shader;
	private static readonly Dictionary<Element, ShaderMaterial> cache = new();

	public static ShaderMaterial For(Element element)
	{
		if (cache.TryGetValue(element, out ShaderMaterial material) && GodotObject.IsInstanceValid(material))
			return material;

		shader ??= GD.Load<Shader>("res://scenes/shaders/element_ramp.gdshader");
		if (shader == null)
		{
			GD.PushError("ElementRampMaterial: res://scenes/shaders/element_ramp.gdshader failed to load.");
			return null;
		}

		ElementColors.Ramp ramp = ElementColors.GetRamp(element);
		material = new ShaderMaterial { Shader = shader };
		material.SetShaderParameter("core", ramp.Core);
		material.SetShaderParameter("hot", ramp.Hot);
		material.SetShaderParameter("mid", ramp.Mid);
		material.SetShaderParameter("edge", ramp.Edge);
		cache[element] = material;
		return material;
	}
}
