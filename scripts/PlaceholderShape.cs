using Godot;

namespace WizardSurvivors.scripts;

// Temporary placeholder art (no external image files needed): draws a simple colored circle or
// ring procedurally via _Draw(), with a shared pulsing glow shader for a bit of visual life.
// Intended to be swapped out for real pixel art later - every new spell/entity can use this
// until then instead of blocking on hand-made sprites.
public partial class PlaceholderShape : Node2D
{
	[Export] public Color ShapeColor { get; set; } = new Color(1, 1, 1);
	[Export] public float Radius { get; set; } = 10f;
	[Export] public bool Ring { get; set; } = false;
	[Export] public float RingWidth { get; set; } = 3f;

	private static ShaderMaterial sharedPulseMaterial;

	public override void _Ready()
	{
		if (sharedPulseMaterial == null)
		{
			var shader = GD.Load<Shader>("res://scenes/shaders/placeholder_pulse.gdshader");
			if (shader != null)
				sharedPulseMaterial = new ShaderMaterial { Shader = shader };
		}
		if (sharedPulseMaterial != null)
			Material = sharedPulseMaterial;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (Ring)
			DrawArc(Vector2.Zero, Radius, 0f, Mathf.Tau, 48, ShapeColor, RingWidth, true);
		else
			DrawCircle(Vector2.Zero, Radius, ShapeColor);
	}
}
