using Godot;
using WizardSurvivors.scripts;

// The way home, opened where the chapter's boss fell (2026-10-28: "when you defeat the boss, we should
// open an arcane portal so the player can end the run when they want to leave").
//
// The run is won the moment the boss falls; this only decides WHEN it ends. Node2DGame keeps the field
// still meanwhile - the clock stopped, no trickle, no Warden, no side event - so walking about before
// leaving collects what is left (the boss's chest, the last XP) and farms nothing. The player leaves by
// stepping into the ring at the portal's foot (Contains), and Node2DGame ends the run in victory.
//
// Drawn, not a sprite: an upright oval of Arcane light on the ground, a slow swirl of motes inside it,
// and a glow on the ground at its foot. The node stands at the portal's FOOT so the arena's y-sort puts
// the wizard in front of it when he is south of it and behind it when north; the glow on the ground is
// a child on the ground layer (-15, the craters' layer), under every actor.
public partial class ArcanePortal : Node2D
{
	/// <summary>How long the portal takes to open, in real seconds.</summary>
	public const float OpenSeconds = 0.9f;
	// The oval, standing on the ground: wider than the wizard, half again his height.
	private const float HalfWidth = 34f;
	private const float Height = 104f;
	// The ring at its foot that takes the player, as an ellipse flat on the ground.
	private const float FootRadiusX = 40f;
	private const float FootRadiusY = 18f;
	private const int Motes = 14;

	private float age;
	private readonly Vector2[] oval = new Vector2[33];
	private Color[] ramp = System.Array.Empty<Color>();  // core, hot, mid, edge
	private Node2D? glow;

	public override void _Ready()
	{
		ElementColors.Ramp arcane = ElementColors.GetRamp(Element.Arcane);
		ramp = new[] { arcane.Core, arcane.Hot, arcane.Mid, arcane.Edge };
		glow = new PortalGlow { Portal = this, ZIndex = -15 };
		AddChild(glow);
	}

	/// <summary>0 while it opens, 1 once it is open.</summary>
	public float Openness => Mathf.Clamp(age / OpenSeconds, 0f, 1f);

	/// <summary>True when a point on the ground is inside the ring at the portal's foot, once it is open.</summary>
	public bool Contains(Vector2 groundPoint)
	{
		if (Openness < 1f)
			return false;
		Vector2 d = groundPoint - GlobalPosition;
		return (d.X * d.X) / (FootRadiusX * FootRadiusX) + (d.Y * d.Y) / (FootRadiusY * FootRadiusY) <= 1f;
	}

	public override void _Process(double delta)
	{
		age += (float)delta;
		QueueRedraw();
		glow?.QueueRedraw();
	}

	private Color Ramp(int i, float alpha) =>
		ramp.Length == 0 ? new Color(0.6f, 0.8f, 1f, alpha) : new Color(ramp[Mathf.Clamp(i, 0, ramp.Length - 1)], alpha);

	public override void _Draw()
	{
		// Eased open: it tears wide fast and settles.
		float open = 1f - Mathf.Pow(1f - Openness, 3f);
		if (open <= 0.01f)
			return;
		float w = HalfWidth * open;
		float h = Height * (0.35f + 0.65f * open);
		Vector2 mid = new(0f, -h * 0.5f - 2f);
		float t = age;

		// The inside: a dark well with a slow arcane swirl, brighter towards the rim.
		DrawSetTransform(mid, 0f, new Vector2(w / (h * 0.5f), 1f));
		DrawCircle(Vector2.Zero, h * 0.5f, new Color(0.05f, 0.03f, 0.10f, 0.92f * open));
		DrawCircle(Vector2.Zero, h * 0.42f, Ramp(ramp.Length - 1, 0.35f * open));
		DrawCircle(Vector2.Zero, h * 0.26f, new Color(0.08f, 0.05f, 0.16f, 0.9f * open));
		for (int k = 0; k < 3; k++)
		{
			float spin = t * (1.1f + 0.35f * k) + k * 2.1f;
			DrawArc(Vector2.Zero, h * (0.18f + 0.1f * k), spin, spin + 2.4f, 18, Ramp(1 + k, 0.7f * open), 2.5f, true);
		}
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

		// The rim: two lines of light round the oval, the outer one breathing.
		float breath = 0.75f + 0.25f * Mathf.Sin(t * 3.4f);
		for (int i = 0; i < oval.Length; i++)
		{
			float a = Mathf.Tau * i / (oval.Length - 1);
			oval[i] = mid + new Vector2(Mathf.Cos(a) * w, Mathf.Sin(a) * h * 0.5f);
		}
		DrawPolyline(oval, Ramp(2, 0.55f * open * breath), 7f, true);
		DrawPolyline(oval, Ramp(0, 0.95f * open), 2.5f, true);

		// Motes drawn up into it from the ground round its foot, each on its own beat.
		for (int m = 0; m < Motes; m++)
		{
			float beat = (t * 0.55f + m * 0.137f) % 1f;
			float side = ((m * 7919) % 100) / 50f - 1f;
			Vector2 from = new(side * (FootRadiusX + 18f), 6f);
			Vector2 to = mid + new Vector2(side * w * 0.3f, 0f);
			Vector2 p = from.Lerp(to, beat * beat);
			DrawCircle(p, 1.6f + 1.2f * (1f - beat), Ramp(m % 3, (1f - beat) * open));
		}
	}

	// The light the portal throws on the ground at its foot, under every actor: the ring to step into.
	private partial class PortalGlow : Node2D
	{
		public ArcanePortal Portal = null!;

		public override void _Draw()
		{
			if (Portal == null)
				return;
			float open = Portal.Openness;
			float pulse = 0.85f + 0.15f * Mathf.Sin(Portal.age * 2.6f);
			DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, FootRadiusY / FootRadiusX));
			DrawCircle(Vector2.Zero, FootRadiusX * 1.5f * open, Portal.Ramp(3, 0.22f * open));
			DrawCircle(Vector2.Zero, FootRadiusX * open, Portal.Ramp(2, 0.30f * open * pulse));
			DrawArc(Vector2.Zero, FootRadiusX * open, 0f, Mathf.Tau, 40, Portal.Ramp(0, 0.8f * open * pulse), 2.5f, true);
			DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
		}
	}
}
