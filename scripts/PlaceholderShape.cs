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
	[Export] public bool Sunburst { get; set; } = false;
	[Export] public int SunRayCount { get; set; } = 12;
	[Export] public float SunRayLength { get; set; } = 12f;
	[Export] public bool EnableIdleMotion { get; set; } = true;
	[Export] public float PulseStrength { get; set; } = 0.05f;
	[Export] public float PulseSpeed { get; set; } = 2.2f;
	[Export] public float RotationAmplitude { get; set; } = 0.06f;
	[Export] public float RotationSpeed { get; set; } = 1.3f;

	private static ShaderMaterial sharedPulseMaterial;
	private Vector2 baseScale = Vector2.One;
	private float baseRotation = 0f;
	private float motionTime = 0f;
	private float motionPhase = 0f;

	public override void _Ready()
	{
		baseScale = Scale;
		baseRotation = Rotation;
		motionPhase = (GetInstanceId() % 37) * 0.13f;

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

	public override void _Process(double delta)
	{
		if (!EnableIdleMotion)
			return;

		motionTime += (float)delta;
		float pulseWave = Mathf.Sin((motionTime * PulseSpeed) + motionPhase);
		float rotationWave = Mathf.Sin((motionTime * RotationSpeed) + motionPhase * 1.9f);
		float ringBias = Ring ? 0.75f : 1.0f;

		Scale = baseScale * (1.0f + pulseWave * PulseStrength * ringBias);
		Rotation = baseRotation + rotationWave * RotationAmplitude * ringBias;
	}

	public override void _Draw()
	{
		if (Sunburst)
		{
			DrawCircle(Vector2.Zero, Radius, ShapeColor);
			int rayCount = Mathf.Max(3, SunRayCount);
			float innerRadius = Radius * 0.82f;
			float outerRadius = Radius + Mathf.Min(Mathf.Max(0f, SunRayLength), Radius * 0.25f);
			var rayColor = new Color(1.0f, 0.38f, 0.08f, ShapeColor.A * 0.9f);
			for (int rayIndex = 0; rayIndex < rayCount; rayIndex++)
			{
				float angle = Mathf.Tau * rayIndex / rayCount;
				var direction = Vector2.FromAngle(angle);
				DrawLine(direction * innerRadius, direction * outerRadius, rayColor, Mathf.Max(1f, RingWidth * 0.45f), true);
			}
			return;
		}

		if (Ring)
			DrawArc(Vector2.Zero, Radius, 0f, Mathf.Tau, 48, ShapeColor, RingWidth, true);
		else
			DrawCircle(Vector2.Zero, Radius, ShapeColor);
	}
}
