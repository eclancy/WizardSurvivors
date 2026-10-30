using Godot;
using WizardSurvivors.scripts;

// The Gale Blade projectile's look: a crescent of moving air spinning out and home, drawn by the bolt it
// rides on (scenes/GaleBlade.tscn).
//
// It was a 32x32 sprite of three arcs spun with the bolt - at every angle but the four square ones its art
// pixels sat on a rotated grid - at 95% opacity under a tint, trailed by untextured GPU particles. It is
// now one quad drawn by scenes/shaders/gale_blade.gdshader, which snaps every fragment to the world's
// 2 px cell before it measures it in the blade's spinning frame (riptide_wave.gdshader's way), in opaque
// Wind tones with Bayer dithering the only gradient, and draws the spin as ghosts of the crescent behind
// it in its turn.
//
// **It is the size it cuts.** A returning blade cuts everything it passes, so it is drawn to the swath it
// cuts, as the Riptide wave is: ElementalBolt.HitRadius plus a body (RiptideWave.BodyAllowance, divided
// back out of the node's scale, since a body does not grow with the blade). An evolution's scale grows
// the node, the circle and this together. While the blade hangs in an Eddy, the wind wheeling round it
// is drawn out to ElementalBolt.EddyReachNow - the reach its recut scans.
//
// The bolt's own modulate (an evolution's tint) is not used: the shader paints the blade itself, and a
// Legendary is its `gilded` edge.
public partial class GaleBladeLook : Node2D
{
	private static Shader bladeShader;
	private static readonly StringName ParamBladeRadius = "blade_radius";
	private static readonly StringName ParamSeed = "seed";
	private static readonly StringName ParamEddyReach = "eddy_reach";
	private static readonly StringName ParamEddy = "eddy";
	private static readonly StringName ParamGilded = "gilded";

	private ElementalBolt bolt;
	private ShaderMaterial material;
	private float drawnRadius = -1f;
	private float drawnReach = -1f;
	private float drawnSwell = -1f;
	// The quad's half-size in node units, as last drawn.
	private float quadHalf;

	public override void _Ready()
	{
		bolt = GetParent() as ElementalBolt;
		bladeShader ??= GD.Load<Shader>("res://scenes/shaders/gale_blade.gdshader");
		material = new ShaderMaterial { Shader = bladeShader };
		Material = material;
		material.SetShaderParameter(ParamSeed, (GetInstanceId() % 997) * 0.731f);
		material.SetShaderParameter(ParamGilded, bolt?.SpellData?.IsLegendary ?? false);
		Refresh();
	}

	public override void _Process(double delta) => Refresh();

	private void Refresh()
	{
		if (bolt == null)
			return;
		float scale = Mathf.Max(0.05f, bolt.GlobalScale.X);
		float radius = bolt.HitRadius + RiptideWave.BodyAllowance / scale;
		if (!Mathf.IsEqualApprox(radius, drawnRadius))
		{
			drawnRadius = radius;
			material.SetShaderParameter(ParamBladeRadius, radius);
		}
		float reach = bolt.EddyReachNow;
		if (!Mathf.IsEqualApprox(reach, drawnReach))
		{
			drawnReach = reach;
			material.SetShaderParameter(ParamEddyReach, reach);
		}
		float swell = bolt.EddySwell;
		if (!Mathf.IsEqualApprox(swell, drawnSwell))
		{
			drawnSwell = swell;
			material.SetShaderParameter(ParamEddy, swell);
		}
		// The quad holds the blade, and the eddy's wind out to its reach (world px, so / scale).
		float half = Mathf.Max(radius, reach / scale) + 6f / scale;
		if (half > quadHalf || half < quadHalf * 0.6f)
		{
			quadHalf = half;
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(-quadHalf, -quadHalf, quadHalf * 2f, quadHalf * 2f), Colors.White);
	}
}
