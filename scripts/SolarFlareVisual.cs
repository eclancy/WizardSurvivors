using Godot;
using System;

namespace WizardSurvivors.scripts;

// Solar Flare's bespoke visual, replacing the generic PlaceholderShape it used to share with
// Toxic Spore Burst. PlaceholderShape._Draw() lets Sunburst win over Ring, and SolarFlare.tscn set
// Sunburst while ElementalPulse set Ring at runtime, so the "expanding ring" animation was actually
// rendered as a growing *filled* disc - an opaque blob that swallowed the wizard and the swarm.
//
// Instead this draws the flare as light erupting from the player and washing outward, and nothing
// wide is solid: the bright elements are rays, a front that thins as it travels and a small kernel, so
// enemies stay readable straight through the blast.
//
// THE DRAWING IS THE HIT (2026-10-23, "their animations should make it clear what the hitbox is").
// It used to be emitted inside a transform squashed to 0.42 high and dropped to the feet, to read as
// a ground effect - but ElementalPulse hits a CIRCLE round the player's origin, so an enemy straight
// above or below him at the full radius was hit with nothing drawn within half that distance of it.
// It is a circle on the player's origin now, the same one the pulse tests, and the burst's own front -
// the leading shockwave, which travels out once at the moment of the hit and lands on the radius - is
// what shows the reach.
//
// PIXEL ART SINCE 2026-10-29 ("does not look up to our typical visual fidelity"). It was DrawArc,
// DrawCircle and see-through polygons - smooth hoops, soft discs, antialiased lines - and the hoops sat
// on the radius for the back half of every burst. Now it is one quad through
// scenes/shaders/solar_flare.gdshader, which snaps every fragment to the world's 2 px cell and draws
// in opaque Light and Fire tones with Bayer dithering as the only gradient. The charge pool at his boots
// is a second quad on a child on the ground layer, under the bodies, since it lies on the floor: it was
// drawn in the air over his boots. Its numbers are the ones the old drawing used, so a pulse looks
// exactly as wide, as long and as quick as it did; only the way it is drawn changed.
public partial class SolarFlareVisual : Node2D, IPulseVisual
{
	[Export] public int RayCount { get; set; } = 14;
	[Export] public int EmberCount { get; set; } = 12;
	[Export] public float BurstDuration { get; set; } = 0.55f;

	public const string ShaderPath = "res://scenes/shaders/solar_flare.gdshader";
	private static Shader shader;
	private static readonly StringName ParamPool = "pool";
	private static readonly StringName ParamRadius = "radius";
	private static readonly StringName ParamProgress = "progress";
	private static readonly StringName ParamSpin = "spin";
	private static readonly StringName ParamRays = "rays";
	private static readonly StringName ParamEmbers = "embers";
	private static readonly StringName ParamBeamCount = "beam_count";
	private static readonly StringName ParamBeamLength = "beam_length";
	private static readonly StringName ParamBeamHalf = "beam_half";
	private static readonly StringName ParamBeamTurn = "beam_turn";
	private static readonly StringName ParamCharge = "charge";
	private static readonly StringName ParamClock = "clock";
	private static readonly StringName ParamBoots = "boots";

	private float elapsed = 0f;
	private float radius = 90f;
	private float spin = 0f;
	private bool active = false;
	private ShaderMaterial burstMaterial;
	private ShaderMaterial poolMaterial;
	private Node2D poolCanvas;

	public override void _Ready()
	{
		// The flare is light washing outward through the air: over the bodies, which its rays and its
		// thinning front leave readable (DrawLayers). It was on the floor from when it was drawn as a flat
		// ellipse.
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		shader ??= GD.Load<Shader>(ShaderPath);
		burstMaterial = new ShaderMaterial { Shader = shader };
		burstMaterial.SetShaderParameter(ParamPool, false);
		Material = burstMaterial;

		// The pool at his boots lies on the floor: under every body, with the other marks at a body's
		// feet (StanceGlow, the Brazier's ring, is the same layer), never over his boots.
		poolMaterial = new ShaderMaterial { Shader = shader };
		poolMaterial.SetShaderParameter(ParamPool, true);
		poolCanvas = new Node2D { Name = "ChargePool", ZAsRelative = false, ZIndex = DrawLayers.GroundTell, Material = poolMaterial };
		AddChild(poolCanvas);
		poolCanvas.Draw += DrawCharge;
	}

	public override void _ExitTree()
	{
		if (poolCanvas != null && IsInstanceValid(poolCanvas))
			poolCanvas.Draw -= DrawCharge;
	}

	// Corona Discharge's sunbeams, set by ElementalPulse just before the Burst they belong to: the
	// count, the length and half-width the beams HIT at, and their turn. Drawn once, with that burst.
	private int beamCount;
	private float beamLength;
	private float beamHalfWidth;
	private float beamTurn;

	public void SetBeams(int count, float length, float halfWidth, float turn)
	{
		beamCount = count;
		beamLength = length;
		beamHalfWidth = halfWidth;
		beamTurn = turn;
	}

	// How far the flare has built while the player stands still (ElementalPulse.StillCharge), 0 to 1.
	// Between bursts it is light pooling on the ground at his boots: a flat white-gold patch that widens
	// and fills as the charge climbs, throbbing quicker as it fills, with motes drawn in to it - the sun
	// gathering itself. Inside the Brazier's ember ring (StanceGlow, 31 x 11) so the two stand-still
	// tells never sit on each other, and small on purpose: it says "building", and the burst says how far.
	private float charge;
	private float chargeClock;
	private Vector2 feet = new Vector2(0f, 36f);
	private const float ChargeShowsFrom = 0.05f;
	private const float PoolSquash = 0.36f;
	// The furthest a mote starts from the pool's middle, as a share of the pool's widest (solar_flare's
	// gathering): what the pool's quad has to cover.
	private const float PoolReach = 26f * 1.08f * 2.4f + 4f;

	/// <param name="boots">Where his boots stand, in this node's space (the player's Feet capsule).</param>
	public void SetCharge(float value, Vector2 boots)
	{
		feet = boots;
		float next = Mathf.Clamp(value, 0f, 1f);
		if (Mathf.IsEqualApprox(next, charge) && next < ChargeShowsFrom)
			return;
		charge = next;
		poolCanvas?.QueueRedraw();
	}

	public void Burst(float burstRadius)
	{
		radius = MathF.Max(8f, burstRadius);
		elapsed = 0f;
		active = true;
		// Rotate the spoke pattern a little every cast so repeat pulses never stamp the same
		// starburst on the floor twice in a row.
		spin += 0.37f;
		burstMaterial.SetShaderParameter(ParamRadius, radius);
		burstMaterial.SetShaderParameter(ParamSpin, spin);
		burstMaterial.SetShaderParameter(ParamRays, Math.Max(4, RayCount));
		burstMaterial.SetShaderParameter(ParamEmbers, Math.Max(0, EmberCount));
		burstMaterial.SetShaderParameter(ParamBeamCount, beamLength > 0f ? beamCount : 0);
		burstMaterial.SetShaderParameter(ParamBeamLength, beamLength);
		burstMaterial.SetShaderParameter(ParamBeamHalf, beamHalfWidth);
		burstMaterial.SetShaderParameter(ParamBeamTurn, beamTurn);
		burstMaterial.SetShaderParameter(ParamProgress, 0f);
		QueueRedraw();
		poolCanvas?.QueueRedraw();
	}

	public override void _Process(double delta)
	{
		chargeClock += (float)delta;
		if (charge >= ChargeShowsFrom && !active)
			poolCanvas?.QueueRedraw();
		if (!active)
			return;

		elapsed += (float)delta;
		if (elapsed >= BurstDuration)
		{
			active = false;
			// The beams belong to the burst they were set for.
			beamCount = 0;
			QueueRedraw();
			poolCanvas?.QueueRedraw();
			return;
		}
		burstMaterial.SetShaderParameter(ParamProgress, Mathf.Clamp(elapsed / MathF.Max(0.01f, BurstDuration), 0f, 1f));
		QueueRedraw();
	}

	// One quad over the reach and the beams; the shader decides every pixel and discards the rest. Sized
	// in world px, which is what the pulse hit in, whatever this node's scale.
	public override void _Draw()
	{
		if (!active)
			return;
		float scale = MathF.Max(0.01f, GlobalScale.X);
		float half = (MathF.Max(radius, beamCount > 0 ? beamLength : 0f) + 4f) / scale;
		DrawRect(new Rect2(-half, -half, half * 2f, half * 2f), Colors.White);
	}

	// The pool's quad, round his boots. The shader is told where they are in world px from this node.
	private void DrawCharge()
	{
		if (active || charge < ChargeShowsFrom)
			return;
		float scale = MathF.Max(0.01f, GlobalScale.X);
		poolMaterial.SetShaderParameter(ParamCharge, charge);
		poolMaterial.SetShaderParameter(ParamClock, chargeClock);
		poolMaterial.SetShaderParameter(ParamSpin, spin);
		poolMaterial.SetShaderParameter(ParamBoots, feet * scale);
		float w = PoolReach / scale;
		float h = (PoolReach * PoolSquash + 4f) / scale;
		poolCanvas.DrawRect(new Rect2(feet.X - w, feet.Y - h, w * 2f, h * 2f), Colors.White);
	}
}
