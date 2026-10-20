using Godot;

// The beat that says a side event was won (SideEvent.Succeed, .ai/side-events.md).
//
// A ghostly column of light stands up out of the ground where the reward arrives, motes drift up
// it, and a ring of pale light runs out across the floor; then it all thins away. When the reward
// is a chest, the chest rises inside the column as a translucent ghost and turns solid as the
// column fades (ChestReward.MaterialiseSeconds), so the two read as one event: the place gave
// something up.
//
// Before this, holding the grove ended with the grove simply vanishing and one small line of text
// under the timer, and the chest - when the reward was a chest - appeared at the grove's centre on
// the same frame, usually under the player's feet, and opened before he could see it.
//
// Phantasmal, not triumphant, and in Arcane cyan's light end rather than gold: gold is the elite
// rim and the chest's own trim, and the column must not be mistaken for an elite arriving. Drawn
// additively from the clock, with its two quads' arrays allocated once - it holds no per-mote state.
public partial class PhantasmRise : Node2D
{
	/// <summary>How long the column stands. The chest's materialisation is timed to finish inside it.</summary>
	public const float Seconds = 2.2f;
	private const float ColumnHeight = 230f;
	private const int Motes = 22;

	private float age;
	private readonly Vector2[] quad = new Vector2[4];
	private readonly Color[] quadColours = new Color[4];

	private static readonly Color Core = new Color(1f, 1f, 1f);
	private static readonly Color Hot = new Color(0.863f, 0.957f, 1f);    // Arcane cyan #DCF4FF
	private static readonly Color Mid = new Color(0.416f, 0.722f, 0.910f); // Arcane cyan #6AB8E8
	private static readonly Color Edge = new Color(0.165f, 0.416f, 0.604f); // Arcane cyan #2A6A9A

	/// <summary>Stands a column at <paramref name="world"/> in <paramref name="arena"/>.</summary>
	public static PhantasmRise Play(Node arena, Vector2 world)
	{
		if (arena == null || !IsInstanceValid(arena))
			return null;
		var fx = new PhantasmRise { Name = "PhantasmRise" };
		arena.AddChild(fx);
		fx.GlobalPosition = world;
		return fx;
	}

	public override void _Ready()
	{
		// Above the actors: it is a column of light, and the chest rising inside it must show
		// through it rather than over it.
		ZIndex = 5;
		Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
	}

	public override void _Process(double delta)
	{
		age += (float)delta;
		if (age >= Seconds)
		{
			QueueFree();
			return;
		}
		QueueRedraw();
	}

	private static float Hash(int i, float salt) => Mathf.PosMod(Mathf.Sin(i * 12.9898f + salt * 78.233f) * 43758.547f, 1f);

	public override void _Draw()
	{
		float t = age / Seconds;
		// Up fast, hold, then thin away over the last 45%.
		float rise = Mathf.Clamp(age / 0.3f, 0f, 1f);
		rise = 1f - (1f - rise) * (1f - rise);
		float fade = t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;
		float breathe = 1f + 0.08f * Mathf.Sin(age * 9f);

		// The ground ring, out across the floor.
		float ring = Mathf.Lerp(10f, 78f, 1f - (1f - Mathf.Min(1f, t * 1.6f)) * (1f - Mathf.Min(1f, t * 1.6f)));
		DrawArc(Vector2.Zero, ring, 0f, Mathf.Tau, 40, new Color(Mid, 0.8f * Mathf.Max(0f, 1f - t * 1.6f)), 3f);
		DrawCircle(Vector2.Zero, 30f * rise, new Color(Edge, 0.35f * fade));

		// The column: a wide pale quad with a narrow white one inside it, both fading out upward.
		float height = ColumnHeight * rise;
		Column(26f * breathe, 12f, height, new Color(Mid, 0.5f * fade), new Color(Edge, 0f));
		Column(9f * breathe, 4f, height * 0.92f, new Color(Core, 0.6f * fade), new Color(Hot, 0f));

		// Motes drifting up the column on a loop, swaying, each on its own phase.
		for (int i = 0; i < Motes; i++)
		{
			float phase = Mathf.PosMod(age * (0.55f + 0.4f * Hash(i, 1f)) + Hash(i, 2f), 1f);
			float y = -phase * height;
			float x = (Hash(i, 3f) - 0.5f) * 40f * (1f - phase * 0.6f) + Mathf.Sin(age * 3f + i) * 4f;
			float a = fade * Mathf.Sin(phase * Mathf.Pi);
			DrawCircle(new Vector2(x, y), 1.4f + 1.4f * Hash(i, 4f), new Color(i % 2 == 0 ? Hot : Mid, a));
		}
	}

	private void Column(float baseHalfWidth, float topHalfWidth, float height, Color bottom, Color top)
	{
		// A zero-height quad fails Godot's triangulation and logs an error, on the first frame.
		if (height < 2f)
			return;
		quad[0] = new Vector2(-baseHalfWidth, 0f);
		quad[1] = new Vector2(baseHalfWidth, 0f);
		quad[2] = new Vector2(topHalfWidth, -height);
		quad[3] = new Vector2(-topHalfWidth, -height);
		quadColours[0] = bottom;
		quadColours[1] = bottom;
		quadColours[2] = top;
		quadColours[3] = top;
		DrawPolygon(quad, quadColours);
	}
}
