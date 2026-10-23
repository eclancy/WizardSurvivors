using Godot;
using WizardSurvivors.scripts;

// Where a Riptide wave breaks over an enemy: a ring of foam, spray thrown up, a dark wet stain.
// Spawned by RiptideWave on ElementalBolt.Struck, one per enemy the wave pierces - at most the
// wave's pierce plus one a cast - and it frees itself.
//
// It is a reaction, not reach: nothing in it - ring or spray - goes further from the enemy than
// half the wave's swath (`Radius`, in world px - RiptideWave's crest half-width), so it never
// reaches past the band the wave itself was drawn across. A titanic wave's splash (Heavy) is the
// ground-shaking one - a second ring, a column of spray, a dark stain - because the doubled hit
// should feel like a sea landing.
//
// Pixel art like the wave: squares on the 2 px art grid (WhipLimb.Cell), Water and azure tones.
// Code-only, no scene; one node per hit and nothing allocated per frame.
public partial class RiptideSplash : Node2D
{
	public float Radius { get; set; } = 20f;
	public bool Heavy { get; set; }
	public Vector2 Heading { get; set; } = Vector2.Right;

	private const int Drops = 12;
	private const int RingCells = 16;
	private float age;
	private float life;
	private float seed;

	private static readonly Color Core = new Color(0.941f, 0.980f, 1.0f, 1f);      // water core #F0FAFF
	private static readonly Color Hot = new Color(0.706f, 0.863f, 1.0f, 1f);       // water hot  #B4DCFF
	private static readonly Color Mid = new Color(0.435f, 0.722f, 0.941f, 1f);     // water mid  #6FB8F0
	private static readonly Color AzureDeep = new Color(0.055f, 0.118f, 0.282f, 1f); // azure deep #0E1E48

	public override void _Ready()
	{
		// Over the body it breaks on, under the status marks (3).
		ZIndex = 2;
		life = Heavy ? 0.5f : 0.3f;
		seed = (GetInstanceId() % 997) * 0.37f;
	}

	public override void _Process(double delta)
	{
		age += (float)delta;
		if (age >= life)
		{
			QueueFree();
			return;
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		float t = age / life;
		float g = WhipLimb.Grid;

		// The wet stain the heavy one leaves: a dither of deep water, thinning as it soaks away.
		if (Heavy)
		{
			// Every other art pixel at most, so a stain is tens of squares rather than hundreds.
			float stain = Radius * 0.8f;
			float step = g * 2f;
			int keep = (int)(4f * (1f - t));
			for (float y = -stain; y <= stain; y += step)
			for (float x = -stain; x <= stain; x += step)
			{
				if (x * x + y * y > stain * stain)
					continue;
				int ix = (int)Mathf.PosMod(x / step, 4f), iy = (int)Mathf.PosMod(y / step, 4f);
				if ((ix + iy * 3) % 4 < keep)
					DrawRect(WhipLimb.Cell(new Vector2(x, y), g), AzureDeep);
			}
		}

		// A ring of foam opening out to the swath's edge, and a second, inner one on the heavy splash.
		float ring = Radius * (0.35f + 0.65f * Mathf.Sqrt(t));
		Color ringColour = t < 0.5f ? Core : Hot;
		for (int k = 0; k < RingCells; k++)
		{
			if ((k + (int)(t * 6f)) % 5 == 0)
				continue; // broken, so it reads as foam rather than as a drawn circle
			float a = k * Mathf.Tau / RingCells + seed;
			DrawRect(WhipLimb.Cell(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ring, g), ringColour);
			if (Heavy)
				DrawRect(WhipLimb.Cell(new Vector2(Mathf.Cos(a + 0.2f), Mathf.Sin(a + 0.2f)) * ring * 0.6f, g), Hot);
		}

		// Spray: thrown out along the wave's heading and up the screen, falling back as it fades.
		int drops = Heavy ? Drops : Drops / 2;
		for (int k = 0; k < drops; k++)
		{
			float a = Heading.Angle() + (k / (float)drops - 0.5f) * 2.4f + Mathf.Sin(seed + k * 2.1f) * 0.25f;
			float reach = Radius * (0.5f + 0.5f * Mathf.PosMod(seed * 3.7f + k * 0.61f, 1f));
			Vector2 flat = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * reach * Mathf.Sqrt(t);
			float lift = Mathf.Sin(t * Mathf.Pi) * Radius * (Heavy ? 0.9f : 0.5f);
			Vector2 at = (flat + Vector2.Up * lift).LimitLength(Radius);
			DrawRect(WhipLimb.Cell(at, g), k % 2 == 0 ? Core : Mid);
		}
	}
}
