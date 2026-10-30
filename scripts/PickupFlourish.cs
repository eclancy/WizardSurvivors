using Godot;

// The half-second burst on the player when he takes a consumable: an expanding ring and a spray of
// motes, in the colours of what it gave him. Code-created, no scene, frees itself.
//
// It is a child of the player rather than of the arena, so it follows him: a burst left behind at
// the pickup point reads as the potion breaking on the floor, not as him drinking it.
//
// The speed potion and the health pickup used to give nothing back but a number changing - the
// potion not even a sound. This is the shared half of their moment; the speed potion adds its trail
// (HasteTrail) for as long as the buff lasts.
//
// Drawn additively and computed from the clock rather than simulated, so it holds no per-mote state
// and allocates nothing after it is made. Bonus drops come about one kill in thirty at most, so
// there are rarely two of these at once.
public partial class PickupFlourish : Node2D
{
	public enum Kind
	{
		/// <summary>The speed potion: Wind and Light, motes thrown out wide, gust arcs swirling round.</summary>
		Haste,
		/// <summary>A health pickup: warm Light motes that rise rather than scatter.</summary>
		Mend,
		/// <summary>
		/// The Gleaning Bell: the call and the answer - one ring of the small orb's green running out
		/// wide, and motes falling IN to him from all round. Every other kind throws its motes out; this
		/// is the one thing he takes in, which is what the orbs about to stream in from off screen do.
		/// </summary>
		Glean,
	}

	private const float Seconds = 0.6f;
	private const float GleanSeconds = 0.9f;
	private float Duration => kind == Kind.Glean ? GleanSeconds : Seconds;
	private const int Motes = 14;
	private const int Gusts = 4;

	private Kind kind;
	private float age;
	private Color core;
	private Color hot;
	private Color edge;

	/// <summary>Plays the burst on <paramref name="player"/>, centred on the body rather than the feet.</summary>
	public static void Play(Node2D player, Kind kind)
	{
		if (player == null || !IsInstanceValid(player))
			return;
		var fx = new PickupFlourish { Name = "PickupFlourish", kind = kind };
		player.AddChild(fx);
	}

	public override void _Ready()
	{
		// Over the player: this is the moment itself, not something on the ground.
		ZIndex = 6;
		Position = new Vector2(0f, -6f);
		Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		// .ai/art-direction.md section 3: white-hot core, identity in the mid and edge.
		if (kind == Kind.Glean)
		{
			core = new Color(0.957f, 1f, 0.863f);          // Grass core #F4FFDC
			hot = new Color(0.812f, 0.933f, 0.580f);       // Grass hot  #CFEE94
			edge = new Color(0.608f, 0.831f, 0.333f);      // Grass mid  #9BD455 (the edge stop is too dark to add)
		}
		else if (kind == Kind.Haste)
		{
			core = new Color(1f, 1f, 1f);                  // Wind / Light core
			hot = new Color(0.918f, 0.988f, 0.965f);       // Wind hot  #EAFCF6
			edge = new Color(0.310f, 0.682f, 0.620f);      // Wind edge #4FAE9E
		}
		else
		{
			core = new Color(1f, 0.973f, 0.847f);          // Light hot  #FFF8D8
			hot = new Color(1f, 0.941f, 0.659f);           // Light mid  #FFF0A8
			edge = new Color(1f, 0.776f, 0.235f);          // Light edge #FFC63C
		}
	}

	public override void _Process(double delta)
	{
		age += (float)delta;
		if (age >= Duration)
		{
			QueueFree();
			return;
		}
		QueueRedraw();
	}

	// A cheap hash so every mote has its own angle and speed without an array or an RNG.
	private static float Hash(int i, float salt) => Mathf.PosMod(Mathf.Sin(i * 12.9898f + salt * 78.233f) * 43758.547f, 1f);

	public override void _Draw()
	{
		float t = age / Duration;
		float fade = 1f - t;
		float easeOut = 1f - (1f - t) * (1f - t);

		if (kind == Kind.Glean)
		{
			DrawGlean(t, fade, easeOut);
			return;
		}

		// The ring: fast out, thinning as it goes.
		float ringRadius = Mathf.Lerp(8f, kind == Kind.Haste ? 50f : 36f, easeOut);
		DrawArc(Vector2.Zero, ringRadius, 0f, Mathf.Tau, 32, new Color(edge, 0.85f * fade), Mathf.Lerp(3.5f, 1f, t));
		if (t < 0.35f)
			DrawCircle(Vector2.Zero, Mathf.Lerp(14f, 4f, t / 0.35f), new Color(core, 0.55f * (1f - t / 0.35f)));

		for (int i = 0; i < Motes; i++)
		{
			float angle = i * Mathf.Tau / Motes + Hash(i, 1f) * 0.5f;
			float speed = 0.6f + 0.6f * Hash(i, 2f);
			Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			Vector2 p;
			if (kind == Kind.Haste)
			{
				// Thrown out wide and swept round, as if by the gust that just went past.
				float swirl = 0.9f * easeOut;
				Vector2 swept = new Vector2(Mathf.Cos(angle + swirl), Mathf.Sin(angle + swirl));
				p = swept * (10f + 52f * speed * easeOut);
			}
			else
			{
				// A healing glow rises: a little out, mostly up.
				p = dir * (8f + 18f * speed * easeOut) + new Vector2(0f, -44f * speed * t);
			}
			float size = Mathf.Lerp(3.4f, 0.8f, t) * (0.8f + 0.4f * Hash(i, 3f));
			Color c = (i % 3) switch { 0 => core, 1 => hot, _ => edge };
			DrawCircle(p, size, new Color(c, fade));
		}

		if (kind != Kind.Haste)
			return;

		// Gust arcs: short curved strokes wrapping the body and spinning off it.
		for (int g = 0; g < Gusts; g++)
		{
			float start = g * Mathf.Tau / Gusts + t * 7.5f;
			float radius = 16f + g * 5f + 20f * easeOut;
			DrawArc(Vector2.Zero, radius, start, start + 1.1f - 0.6f * t, 10, new Color(hot, 0.7f * fade), 2f);
		}
	}

	private void DrawGlean(float t, float fade, float easeOut)
	{
		// The call: one ring running out well past the body, thinning as it goes.
		DrawArc(Vector2.Zero, Mathf.Lerp(10f, 150f, easeOut), 0f, Mathf.Tau, 48, new Color(edge, 0.7f * fade), Mathf.Lerp(4f, 1f, t));
		// The answer: motes falling in from a wide circle, slow at first and quick at the end - the
		// orbs' own acceleration (XPOrb.Glean), in miniature.
		float easeIn = t * t;
		for (int i = 0; i < Motes; i++)
		{
			float angle = i * Mathf.Tau / Motes + Hash(i, 4f) * 0.4f;
			float from = 70f + 50f * Hash(i, 5f);
			Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			Vector2 p = dir * Mathf.Lerp(from, 4f, easeIn);
			float alpha = Mathf.Clamp(t * 4f, 0f, 1f) * (t > 0.85f ? (1f - t) / 0.15f : 1f);
			float size = Mathf.Lerp(1.6f, 3.2f, easeIn) * (0.8f + 0.4f * Hash(i, 6f));
			Color c = (i % 3) switch { 0 => core, 1 => hot, _ => edge };
			DrawCircle(p, size, new Color(c, alpha));
		}
		// The glow gathering on him as they arrive.
		if (t > 0.5f)
			DrawCircle(Vector2.Zero, Mathf.Lerp(4f, 12f, (t - 0.5f) / 0.5f), new Color(core, 0.45f * fade));
	}
}
