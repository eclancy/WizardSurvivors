using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// The ground a blast leaves hurting, drawn as pixel art: Pyroclasm's pool of magma under a Fireball's
/// blast (scenes/shaders/magma_pool.gdshader) and Lingering Rift's tear under an Arcane Explosion
/// (scenes/shaders/arcane_rift.gdshader). A child of the LingeringZone that does the hurting.
/// </summary>
/// <remarks>
/// Both zones used to draw themselves (LingeringZone._Draw): a translucent polygon, a darker one inside
/// it and a soft bubble - flat see-through discs, which the Bonelight contract and the no-marker rule
/// both rule out, and at Hellfire Nova's size a red wash over half the screen. The zone now sets
/// DrawsItself false and this draws it instead: one quad over the zone through one shared material per
/// kind, the zone's numbers in the vertex colour (crater.gdshader's arrangement), so every pool of a
/// kind on screen is one batch and nothing is allocated per frame.
///
/// THE RADIUS IS THE HIT: <see cref="LingeringZone.Radius"/>, the reach its ticks test, and the shaders
/// draw nothing past it - the edge wanders, but only inward.
/// </remarks>
public partial class BlastPoolSurface : Node2D
{
	public enum Kind
	{
		Magma,
		ArcaneRift,
	}

	// The radius rides in a colour channel as radius / RadiusScale, so it must stay under 1.
	private const float RadiusScale = 1024f;
	// Seconds of the zone's age carried in the alpha channel, for the seep-in.
	private const float AgeScale = 4f;

	private static ShaderMaterial magmaMaterial;
	private static ShaderMaterial riftMaterial;
	private static Texture2D white;

	private LingeringZone zone;
	private Kind kind;
	private float seed;

	/// <summary>Draws <paramref name="zone"/> as <paramref name="kind"/>, and stops it drawing itself.</summary>
	public static void Attach(LingeringZone zone, Kind kind)
	{
		if (zone == null)
			return;
		zone.DrawsItself = false;
		var surface = new BlastPoolSurface { Name = "Surface" };
		surface.zone = zone;
		surface.kind = kind;
		surface.seed = (GD.Randf() * 0.98f) + 0.01f;
		zone.AddChild(surface);
	}

	private static ShaderMaterial MaterialFor(Kind kind)
	{
		ref ShaderMaterial slot = ref (kind == Kind.Magma ? ref magmaMaterial : ref riftMaterial);
		if (slot != null && IsInstanceValid(slot))
			return slot;
		string path = kind == Kind.Magma ? "res://scenes/shaders/magma_pool.gdshader" : "res://scenes/shaders/arcane_rift.gdshader";
		var shader = GD.Load<Shader>(path);
		if (shader == null)
			GD.PrintErr($"BlastPoolSurface: {path} did not load; the pool will draw as a plain quad.");
		slot = new ShaderMaterial { Shader = shader };
		return slot;
	}

	// A one-pixel texture, so the quad has a UV running 0..1 across it (CraterGround's). Never sampled.
	private static Texture2D White
	{
		get
		{
			if (white != null && IsInstanceValid(white))
				return white;
			Image image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
			image.SetPixel(0, 0, Colors.White);
			white = ImageTexture.CreateFromImage(image);
			return white;
		}
	}

	public override void _Ready()
	{
		// On the zone's own layer: the ground, under every body (LingeringZone sets it absolute).
		Material = MaterialFor(kind);
	}

	public override void _Process(double delta) => QueueRedraw();

	public override void _Draw()
	{
		if (zone == null || !IsInstanceValid(zone))
			return;
		float radius = Mathf.Min(zone.Radius, RadiusScale - 1f);
		if (radius < 8f)
			return;
		// On an even world pixel, so the pool's art grid is the world's.
		Vector2 c = ToLocal(zone.GlobalPosition);
		Vector2 snapped = new Vector2(Mathf.Round(zone.GlobalPosition.X * 0.5f) * 2f, Mathf.Round(zone.GlobalPosition.Y * 0.5f) * 2f);
		c += snapped - zone.GlobalPosition;
		float life = Mathf.Clamp(zone.Age / Mathf.Max(0.01f, zone.Duration), 0f, 1f);
		var parameters = new Color(life, radius / RadiusScale, seed, Mathf.Clamp(zone.Age / AgeScale, 0f, 1f));
		DrawTextureRect(White, new Rect2(c - new Vector2(radius, radius), new Vector2(radius * 2f, radius * 2f)), false, parameters);
	}
}
