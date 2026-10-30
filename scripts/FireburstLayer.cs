using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// One node per arena that draws every burst of fire in the air: Fireball's detonation and Cluster
/// Bombs' bomblets (scenes/shaders/fireburst.gdshader). Over the bodies, as the blast is.
/// </summary>
/// <remarks>
/// A blast outlives the fireball that made it - Fireball frees itself 0.4 s after it goes off, and a
/// bomblet has no node at all - so the bursts live here, a slot each in a fixed array
/// (ImpactDebrisLayer's pattern), not on the fireball. Nothing is instantiated or allocated per burst,
/// every burst is one quad through one shared material, so a screen of them is one batch, and the node
/// stops processing when nothing is live.
///
/// The radius passed in MUST be the one the hit tested: the shader draws nothing past it.
/// </remarks>
public partial class FireburstLayer : Node2D
{
	public const string ShaderPath = "res://scenes/shaders/fireburst.gdshader";
	/// <summary>A burst's whole life: ImpactDebris's, so the fire and the debris thrown with it end together.</summary>
	public const float LifeSeconds = ImpactDebris.LifeSeconds;
	private const int MaxBursts = 24;
	// The radius rides in a colour channel as radius / RadiusScale, so it must stay under 1.
	private const float RadiusScale = 1024f;

	/// <summary>How a burst is painted (fireburst.gdshader's style).</summary>
	public enum Style
	{
		Fire = 0,
		Hellfire = 1,
		Sun = 2,
		Gold = 3,
	}

	private struct Burst
	{
		public Vector2 At;
		public float Radius;
		public float Age;
		public float Seed;
		public Style Style;
		public bool Live;
	}

	private readonly Burst[] bursts = new Burst[MaxBursts];
	private int next;
	private int live;
	private uint seedClock;

	private static FireburstLayer current;
	private static ShaderMaterial material;
	private static Texture2D white;

	/// <summary>
	/// A burst of fire at <paramref name="globalAt"/> reaching exactly <paramref name="radius"/>, on the
	/// layer of <paramref name="arena"/> (created there the first time).
	/// </summary>
	public static void Spawn(Node2D arena, Vector2 globalAt, float radius, Style style)
	{
		if (arena == null || !IsInstanceValid(arena))
			return;
		if (current == null || !IsInstanceValid(current) || !current.IsInsideTree() || current.GetParent() != arena)
		{
			current = new FireburstLayer { Name = "Firebursts" };
			arena.AddChild(current);
		}
		current.Add(globalAt, radius, style);
	}

	private static ShaderMaterial SharedMaterial
	{
		get
		{
			if (material != null && IsInstanceValid(material))
				return material;
			var shader = GD.Load<Shader>(ShaderPath);
			if (shader == null)
				GD.PrintErr($"FireburstLayer: {ShaderPath} did not load; fire bursts will draw as plain quads.");
			material = new ShaderMaterial { Shader = shader };
			return material;
		}
	}

	// A one-pixel texture, so each quad has a UV running 0..1 across it (CraterGround's). Never sampled.
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
		// In the air, over the bodies the blast catches (DrawLayers).
		ZIndex = DrawLayers.Air;
		ZAsRelative = false;
		Material = SharedMaterial;
		SetProcess(false);
	}

	public override void _ExitTree()
	{
		if (current == this)
			current = null;
	}

	private void Add(Vector2 globalAt, float radius, Style style)
	{
		// The oldest slot is reused when every one is live: one blast more or less on a screen that busy
		// is not seen, and the cap keeps the cost flat.
		if (!bursts[next].Live)
			live++;
		seedClock++;
		bursts[next] = new Burst
		{
			At = globalAt,
			Radius = Mathf.Min(radius, RadiusScale - 1f),
			Seed = (seedClock * 0.6180339f) % 1f,
			Style = style,
			Live = true,
		};
		next = (next + 1) % MaxBursts;
		SetProcess(true);
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		for (int i = 0; i < MaxBursts; i++)
		{
			if (!bursts[i].Live)
				continue;
			bursts[i].Age += dt;
			if (bursts[i].Age >= LifeSeconds)
			{
				bursts[i].Live = false;
				live--;
			}
		}
		QueueRedraw();
		if (live <= 0)
		{
			live = 0;
			SetProcess(false);
		}
	}

	public override void _Draw()
	{
		Texture2D tex = White;
		for (int i = 0; i < MaxBursts; i++)
		{
			Burst b = bursts[i];
			if (!b.Live || b.Radius < 8f)
				continue;
			// On an even world pixel, so the burst's art grid is the world's.
			Vector2 c = ToLocal(b.At);
			c = new Vector2(Mathf.Round(c.X * 0.5f) * 2f, Mathf.Round(c.Y * 0.5f) * 2f);
			var parameters = new Color(Mathf.Clamp(b.Age / LifeSeconds, 0f, 1f), b.Radius / RadiusScale, b.Seed,
				((int)b.Style + 0.5f) / 8f);
			DrawTextureRect(tex, new Rect2(c - new Vector2(b.Radius, b.Radius), new Vector2(b.Radius * 2f, b.Radius * 2f)), false, parameters);
		}
	}
}
