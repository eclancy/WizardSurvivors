using Godot;
using WizardSurvivors.scripts;

// The Molten Shard projectile's look: a splinter of hot metal, tumbling, shedding sparks, drawn by the
// bolt it rides on (scenes/MoltenShard.tscn).
//
// It was a 32x32 sprite of a bar - turned to the bolt's heading, so at every angle but the four square
// ones its art pixels sat on a rotated grid - multiplied by an orange tint that pushed its Metal ramp off
// the palette, and trailed by untextured GPU particles: translucent specks smaller than an art pixel. It
// is now one quad drawn by scenes/shaders/molten_shard.gdshader, which snaps every fragment to the
// world's 2 px cell before it measures it in the shard's frame (riptide_wave.gdshader's way), in opaque
// Metal and Fire tones with Bayer dithering the only gradient.
//
// **It is the size it hits.** The shard is drawn from ElementalBolt.HitRadius, the collision circle's
// radius in the node's units, and a body (RiptideWave.BodyAllowance, divided back out of the node's scale,
// since a body does not grow with the shard): its half-length is the two - the reach from its centre at
// which an enemy's centre is struck, as the Riptide wave and the Gale Blade are drawn - so it traces that
// reach as it tumbles, and an evolution's scale grows the shard and the circle together. It is the length
// the old sprite was on screen. Only the streak and the sparks reach outside it, and only behind it.
//
// **It shows what the shard is doing** (BoltEvolutions): tumbling in flight; lodged in an enemy (Magma
// Core), heating through the Fire ramp and spitting sparks as the fuse burns down; held point-out in the
// forged ring round the wizard (Forge Swarm). The bolt's own modulate - an evolution's tint, a
// Legendary's gold - is not used: the shader paints the shard itself, and a Legendary is its `gilded` seam.
public partial class MoltenShardLook : Node2D
{
	private static Shader shardShader;
	private static readonly StringName ParamHitRadius = "hit_radius";
	private static readonly StringName ParamBody = "body_allowance";
	private static readonly StringName ParamSeed = "seed";
	private static readonly StringName ParamMode = "mode";
	private static readonly StringName ParamHeat = "heat";
	private static readonly StringName ParamGilded = "gilded";

	private ElementalBolt bolt;
	private ShaderMaterial material;
	private float drawnRadius = -1f;
	private float drawnBody = -1f;
	private int drawnMode = -1;
	private float drawnHeat = -1f;

	public override void _Ready()
	{
		bolt = GetParent() as ElementalBolt;
		shardShader ??= GD.Load<Shader>("res://scenes/shaders/molten_shard.gdshader");
		material = new ShaderMaterial { Shader = shardShader };
		Material = material;
		// Two shards thrown together should not tumble in step.
		material.SetShaderParameter(ParamSeed, (GetInstanceId() % 997) * 0.731f);
		material.SetShaderParameter(ParamGilded, bolt?.SpellData?.IsLegendary ?? false);
		Refresh();
	}

	public override void _Process(double delta) => Refresh();

	private void Refresh()
	{
		if (bolt == null)
			return;
		int mode = bolt.IsForging ? 2 : bolt.IsLodged ? 1 : 0;
		float heat = mode == 1 ? bolt.LodgeFuse : 0f;
		if (mode != drawnMode)
		{
			drawnMode = mode;
			material.SetShaderParameter(ParamMode, mode);
		}
		if (!Mathf.IsEqualApprox(heat, drawnHeat))
		{
			drawnHeat = heat;
			material.SetShaderParameter(ParamHeat, heat);
		}
		float radius = bolt.HitRadius;
		float body = RiptideWave.BodyAllowance / Mathf.Max(0.05f, bolt.GlobalScale.X);
		if (!Mathf.IsEqualApprox(radius, drawnRadius) || !Mathf.IsEqualApprox(body, drawnBody))
		{
			drawnRadius = radius;
			drawnBody = body;
			material.SetShaderParameter(ParamHitRadius, radius);
			material.SetShaderParameter(ParamBody, body);
			QueueRedraw();
		}
	}

	// One quad over the shard, whichever way it has tumbled, and the streak and sparks behind it; the
	// shader decides every pixel and discards the rest.
	public override void _Draw()
	{
		float scale = Mathf.Max(0.05f, bolt?.GlobalScale.X ?? 1f);
		// The shard's half-length, and a few art pixels to spare at any scale.
		float reach = Mathf.Max(2f, drawnRadius) + Mathf.Max(0f, drawnBody) + 6f / scale;
		DrawRect(new Rect2(-reach * 2.7f, -reach * 1.2f, reach * 3.9f, reach * 2.4f), Colors.White);
	}
}
