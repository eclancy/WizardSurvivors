using Godot;
using WizardSurvivors.scripts;

// The Riptide projectile: a small travelling wave, drawn by the bolt it rides on.
//
// "Riptide projectile should look like an animated small wave of water - it currently looks like a
// thrown weapon." It was a 24x14 sprite of a blue crescent, rotated to its heading, and a hard
// silhouette spinning toward a skeleton reads as a blade whatever colour it is. Water is told apart
// from a solid by what moves INSIDE it: a lip of foam that keeps rolling over and never holds the
// same shape two frames running, a face with streaks dragged back along it, a shadow under the
// curl, and spray thrown off the crest and left behind. Every one of those is animated.
//
// It is drawn by scenes/shaders/riptide_wave.gdshader on one quad, as pixel art: each fragment snaps
// to the WORLD's 2 px art grid before it is turned into the wave's frame, so the art pixels stay
// square and upright whatever the heading. A first version drew it with polygons and read as exactly
// the crescent blade it was meant to replace.
//
// **It is the size of the swath it cuts, and only that.** The parent ElementalBolt hits with a circle
// of HitRadius (8 px, times the player's Area) against each enemy's BODY - a 40 x 24 box - so,
// measured the way every zone in the game is measured, against enemy centres, the band it hits is
// HitRadius plus half a body either side of its path. The crest spans exactly that band ACROSS the
// heading: HitRadius + BodyAllowance each side. Drawn to the bare 8 px circle it was a 16 px speck
// next to an 86 px skeleton, and it hit things a whole body-width beyond where it was drawn - the
// mirror image of the "drawn bigger than it hurts" bug, and no more honest. A wave is wider than it
// is long, so the crest lies sideways to travel. An evolution's ScaleMultiplier scales the parent
// node, and with it the collision circle and this drawing; the allowance is a body, which does not
// grow, so it is divided back out of the node's scale. The spray and the wake trail BEHIND the
// crest, never ahead of it or wider than it, so they read as wake rather than as reach.
//
// **The titanic wave.** "Riptide's 'titanic' area increase does not appear as exciting as it
// should." Titan Cataclysm doubles the hit (its x2 ScaleMultiplier scales the collision circle), and
// all it did to the picture was double a small sprite and tint it fire-orange - the default
// archetype's tint, which on water read as mud (Player.FireBoltSpell now drops it). With Titan taken
// (and so under Spring Tide, which requires it) the crest is a heavier sea: darker, deeper water, a
// trough drawn down ahead of the face, a lip half again as thick with spume heaped off it toward the
// top of the screen, twice the spray, a churned wake, and a heavy splash on every enemy it breaks
// over (RiptideSplash). All of it inside the swath the doubled circle cuts, because that is the
// payoff - what grew is what it hits.
//
// **Brackish Tide** (the level 4 element path into Poison, ElementPaths): the same wave gone foul -
// the foam in Poison's own core and hot, the face and body a sea turned murky green, and the splashes
// with it. Painted here rather than tinted, which is why Player.FireBoltSpell drops every evolution
// tint on this bolt: a multiply over water turns it to mud.
public partial class RiptideWave : Node2D
{
	public const string TitanEvolutionId = "riptide_titan_cataclysm";
	// Half the short side of an enemy's body box (enemy.tscn, 40.5 x 24.3), in world px: how far
	// beyond the hit circle an enemy's centre can be and still be struck.
	public const float BodyAllowance = 12f;

	private static Shader waveShader;
	private static readonly StringName ParamHalfWidth = "half_width";
	private static readonly StringName ParamTime = "time";
	private static readonly StringName ParamSeed = "seed";
	private static readonly StringName ParamTitanic = "titanic";
	private static readonly StringName ParamUp = "up_local";
	private static readonly StringName ParamGilded = "gilded";
	private static readonly StringName ParamHasPath = "has_path";
	private static readonly StringName ParamPathCore = "path_core";
	private static readonly StringName ParamPathHot = "path_hot";
	private static readonly StringName ParamPathMid = "path_mid";
	private static readonly StringName ParamPathEdge = "path_edge";

	private ElementalBolt bolt;
	private ShaderMaterial material;
	private bool titanic;
	// The element path the spell took at level 4, if any - Brackish Tide's Poison. The sea turns
	// toward that element's ramp, foam first (riptide_wave.gdshader), and so does every splash.
	private Element? path;
	private float time;
	private float drawnHalfWidth = -1f;

	public override void _Ready()
	{
		bolt = GetParent() as ElementalBolt;
		titanic = bolt?.SpellData?.HasEvolution(TitanEvolutionId) ?? false;
		if (bolt != null)
			bolt.Struck += OnStruck;

		waveShader ??= GD.Load<Shader>("res://scenes/shaders/riptide_wave.gdshader");
		material = new ShaderMaterial { Shader = waveShader };
		Material = material;
		// Two waves fired a moment apart should not roll in step.
		material.SetShaderParameter(ParamSeed, (GetInstanceId() % 997) * 0.731f);
		material.SetShaderParameter(ParamTitanic, titanic);
		material.SetShaderParameter(ParamGilded, bolt?.SpellData?.IsLegendary ?? false);
		path = bolt?.SpellData?.GetBranchElement();
		material.SetShaderParameter(ParamHasPath, path != null);
		if (path is Element element)
		{
			ElementColors.Ramp ramp = ElementColors.GetRamp(element);
			material.SetShaderParameter(ParamPathCore, ramp.Core);
			material.SetShaderParameter(ParamPathHot, ramp.Hot);
			material.SetShaderParameter(ParamPathMid, ramp.Mid);
			material.SetShaderParameter(ParamPathEdge, ramp.Edge);
		}
	}

	public override void _ExitTree()
	{
		if (bolt != null)
			bolt.Struck -= OnStruck;
	}

	public override void _Process(double delta)
	{
		time += (float)delta;
		material.SetShaderParameter(ParamTime, time);
		// The screen's up in the wave's frame. The heading is fixed once fired, but this is a
		// single rotation and keeps the spume right if anything ever turns the bolt.
		material.SetShaderParameter(ParamUp, Vector2.Up.Rotated(-GlobalRotation));
		float half = SwathHalfWidth();
		if (!Mathf.IsEqualApprox(half, drawnHalfWidth))
		{
			drawnHalfWidth = half;
			material.SetShaderParameter(ParamHalfWidth, half);
			QueueRedraw();
		}
	}

	/// <summary>Half the crest's width, in this node's (scaled) units: the hit circle plus a body.</summary>
	private float SwathHalfWidth()
	{
		float scale = Mathf.Max(0.01f, bolt?.GlobalScale.X ?? 1f);
		return (bolt != null ? bolt.HitRadius : 8f) + BodyAllowance / scale;
	}

	// One quad over the crest, the spray behind it and the spume above it; the shader decides
	// every pixel and discards the rest.
	public override void _Draw()
	{
		float r = drawnHalfWidth > 0f ? drawnHalfWidth : SwathHalfWidth();
		DrawRect(new Rect2(-r * 2.4f, -r * 1.2f, r * 3.4f, r * 2.4f), Colors.White);
	}

	// A splash where the wave breaks over an enemy, as wide as the swath.
	private void OnStruck(Node2D enemy)
	{
		if (bolt == null || !IsInstanceValid(enemy) || bolt.GetParent() is not Node world)
			return;
		var splash = new RiptideSplash
		{
			Radius = SwathHalfWidth() * bolt.GlobalScale.X,
			Heavy = titanic,
			Heading = Vector2.Right.Rotated(bolt.GlobalRotation),
			Path = path,
		};
		world.AddChild(splash);
		// On the art grid, so its squares line up with the wave's.
		Vector2 at = enemy.GlobalPosition;
		splash.GlobalPosition = new Vector2(Mathf.Round(at.X / WhipLimb.Grid) * WhipLimb.Grid, Mathf.Round(at.Y / WhipLimb.Grid) * WhipLimb.Grid);
	}
}
