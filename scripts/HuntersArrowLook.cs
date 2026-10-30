using Godot;
using WizardSurvivors.scripts;

// The Hunter's Draw arrow in flight: drawn by the bolt it rides on (scenes/HuntersArrow.tscn).
//
// It was a 32x32 sprite at a scale of 1.5 - a fractional scale, so an art pixel was three device pixels
// and a half - turned to the heading, multiplied by an over-bright cyan, and trailed by translucent
// particles. It is now one quad drawn by scenes/shaders/hunters_arrow.gdshader (mode A), which snaps
// every fragment to the world's 2 px cell before it measures it in the arrow's frame: a Metal head, a
// Wind shaft and fletching, one art pixel of occlusion backing it all and a wake of Wind streaks behind.
// It is the arrow BowDrawVisual had on the string - the same length, the same head - now loosed.
//
// **The point is the hit.** The quad is the bolt's own, so the bolt's scale (an evolution's, Heavy
// Draw's heavier arrow) grows the arrow and the collision circle together, and the point is drawn on
// the front of that circle (ElementalBolt.HitRadius): the arrow strikes what its point touches.
// The bolt's own modulate (an evolution's tint) is not used; a Legendary is its `gilded` head.
public partial class HuntersArrowLook : Node2D
{
	/// <summary>Point to nock, in the arrow's units: BowDrawVisual.ArrowLength, the arrow on the string.</summary>
	public const float ArrowLength = 52f;

	private static Shader arrowShader;
	private static readonly StringName ParamHitRadius = "hit_radius";
	private static readonly StringName ParamArrowLength = "arrow_length";
	private static readonly StringName ParamWake = "wake";
	private static readonly StringName ParamSeed = "seed";
	private static readonly StringName ParamGilded = "gilded";

	private ElementalBolt bolt;
	private ShaderMaterial material;
	private float drawnRadius = -1f;

	public override void _Ready()
	{
		bolt = GetParent() as ElementalBolt;
		arrowShader ??= GD.Load<Shader>("res://scenes/shaders/hunters_arrow.gdshader");
		material = new ShaderMaterial { Shader = arrowShader };
		Material = material;
		material.SetShaderParameter(ParamArrowLength, ArrowLength);
		material.SetShaderParameter(ParamWake, true);
		material.SetShaderParameter(ParamSeed, (GetInstanceId() % 997) * 0.731f);
		material.SetShaderParameter(ParamGilded, bolt?.SpellData?.IsLegendary ?? false);
		Refresh();
	}

	public override void _Process(double delta) => Refresh();

	private void Refresh()
	{
		float radius = bolt?.HitRadius ?? ElementalBolt.BaseHitRadius;
		if (Mathf.IsEqualApprox(radius, drawnRadius))
			return;
		drawnRadius = radius;
		material.SetShaderParameter(ParamHitRadius, radius);
		QueueRedraw();
	}

	// One quad from just past the point back over the shaft, the fletching and the wake behind it.
	public override void _Draw()
	{
		float front = drawnRadius + 4f;
		float back = drawnRadius - ArrowLength * 1.85f;
		DrawRect(new Rect2(back, -12f, front - back, 24f), Colors.White);
	}
}
