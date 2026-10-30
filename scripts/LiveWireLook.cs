using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// Gale Blade's Live Wire, drawn (.ai/spell-roster.md 2e): the crackling line of lightning from the wizard
/// to his blade while it is out. Only the look - the blade cuts along the wire (ElementalBolt.TickLiveWire)
/// and moves this every frame to the segment it cut along.
/// </summary>
/// <remarks>
/// One quad laid along the segment, drawn by scenes/shaders/live_wire.gdshader as pixel art on the world's
/// 2 px grid (it was three stroked polylines, one of them translucent, drawn in the blade's _Draw). The
/// node sits on the wizard's end of the segment, turned to the blade, so the quad never has to be rebuilt
/// as the blade flies - only lengthened, in steps, when the blade gets further out than it reaches.
/// Code-only, one per thrown blade, freed with it. In the air (DrawLayers.Air), over the bodies it cuts.
/// </remarks>
public partial class LiveWireLook : Node2D
{
	private static Shader wireShader;
	private static readonly StringName ParamLength = "wire_length";
	private static readonly StringName ParamHalfWidth = "half_width";
	private static readonly StringName ParamSeed = "seed";
	private static readonly StringName ParamGilded = "gilded";
	// The quad is lengthened in these steps, so a blade flying out redraws it a handful of times, not
	// every frame.
	private const float QuadStep = 64f;

	private ShaderMaterial material;
	private float quadLength;

	/// <summary>A Legendary's gold in the wire's core.</summary>
	public bool Gilded { get; set; }

	public override void _Ready()
	{
		TopLevel = true;
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		wireShader ??= GD.Load<Shader>("res://scenes/shaders/live_wire.gdshader");
		material = new ShaderMaterial { Shader = wireShader };
		Material = material;
		material.SetShaderParameter(ParamHalfWidth, BoltEvolutions.LiveWireHalfWidth);
		material.SetShaderParameter(ParamSeed, (GetInstanceId() % 997) * 0.731f);
		material.SetShaderParameter(ParamGilded, Gilded);
		Visible = false;
	}

	/// <summary>Lays the wire from <paramref name="from"/> (the point the cut measures from) to <paramref name="to"/> (the blade), world.</summary>
	public void Span(Vector2 from, Vector2 to)
	{
		Vector2 along = to - from;
		float length = along.Length();
		Visible = length > 1f;
		if (!Visible)
			return;
		GlobalPosition = from;
		GlobalRotation = along.Angle();
		material.SetShaderParameter(ParamLength, length);
		if (length > quadLength)
		{
			quadLength = Mathf.Ceil(length / QuadStep) * QuadStep;
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		float h = BoltEvolutions.LiveWireHalfWidth + WhipLimb.Grid * 2f;
		DrawRect(new Rect2(-WhipLimb.Grid * 2f, -h, quadLength + WhipLimb.Grid * 4f, h * 2f), Colors.White);
	}
}
