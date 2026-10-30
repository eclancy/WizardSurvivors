using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// Gale Blade's Thunderhead (.ai/spell-roster.md 2e): a stroke of lightning out of the sky onto the
/// point where the blade turned. Only the look - the blade deals the strike (ElementalBolt.StrikeThunder)
/// and ImpactDebrisLayer throws the burst out to the radius it hit.
/// </summary>
/// <remarks>
/// A jagged stroke from well above the screen's view of the ground down to the point, two short forks
/// off it, and a white-hot knot where it lands, all in Lightning's ramp: drawn for a fraction of a
/// second and flickering out, as a strike does. In the air (DrawLayers.Air), over the bodies it hits.
/// The stroke is jagged once, at birth, into fixed arrays - nothing is allocated while it draws.
///
/// The pixel-art pass (2026-10-29: "does not look up to our typical visual fidelity"): it was three
/// DrawPolylines of falling width and two DrawCircles, translucent, so the bolt was a soft yellow smear
/// at every angle but straight down. Every segment is now a SpellStrokes stroke on the world's 2 px grid -
/// a white-hot spine three px across, a hot body, and the ramp's edge as a dithered glow where the old
/// translucent outer line was - and the knot is two of them at one point. The flicker that faded its alpha
/// now dissolves it by dither: on, thinning, on, gone.
/// </remarks>
public partial class ThunderStrike : Node2D
{
	private const float Lifetime = 0.32f;
	private const float Height = 300f;
	private const int Segments = 9;

	private readonly Vector2[] stroke = new Vector2[Segments + 1];
	private readonly Vector2[] forkA = new Vector2[4];
	private readonly Vector2[] forkB = new Vector2[4];
	private float age;
	private float reach = 90f;
	private SpellStrokes strokes;

	public static void Spawn(Node2D arena, Vector2 at, float reach)
	{
		if (arena == null || !IsInstanceValid(arena))
			return;
		var strike = new ThunderStrike { Name = "ThunderStrike", reach = reach };
		arena.AddChild(strike);
		strike.GlobalPosition = at;
	}

	public override void _Ready()
	{
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		strokes = new SpellStrokes(Segments + 8);
		ElementColors.Ramp lightning = ElementColors.GetRamp(Element.Lightning);
		strokes.SetTones(lightning, lightning.Edge, new Color(0.020f, 0.027f, 0.047f));
		Material = strokes.Material;
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		// From above, leaning a little, jagged the whole way down; the last point is the strike.
		float lean = rng.RandfRange(-60f, 60f);
		for (int i = 0; i <= Segments; i++)
		{
			float t = i / (float)Segments;
			float jag = i == 0 || i == Segments ? 0f : rng.RandfRange(-14f, 14f);
			stroke[i] = new Vector2(lean * (1f - t) + jag, -Height * (1f - t));
		}
		BuildFork(forkA, stroke[4], rng, -1f);
		BuildFork(forkB, stroke[6], rng, 1f);
	}

	private void BuildFork(Vector2[] fork, Vector2 from, RandomNumberGenerator rng, float side)
	{
		fork[0] = from;
		for (int i = 1; i < fork.Length; i++)
			fork[i] = fork[i - 1] + new Vector2(side * rng.RandfRange(8f, 18f), rng.RandfRange(10f, 20f));
	}

	public override void _Process(double delta)
	{
		age += (float)delta;
		if (age >= Lifetime)
		{
			QueueFree();
			return;
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		float t = age / Lifetime;
		// Full for the first third, then it flickers out: on, thinning, on, gone.
		float gone = t < 0.35f ? 0f : (MathF.Sin(age * 90f) > 0f ? t : 0.85f + 0.15f * t);
		Vector2 origin = GlobalPosition;
		strokes.Begin();
		for (int i = 0; i < Segments; i++)
			Bolt(origin + stroke[i], origin + stroke[i + 1], 3.5f, 4.5f, gone);
		for (int i = 0; i < forkA.Length - 1; i++)
		{
			Bolt(origin + forkA[i], origin + forkA[i + 1], 1.75f, 2.25f, gone);
			Bolt(origin + forkB[i], origin + forkB[i + 1], 1.75f, 2.25f, gone);
		}
		// Where it lands: a knot of white light, a fraction of the reach - the strike, not its area.
		float knot = MathF.Max(6f, reach * 0.16f) * (1.2f - 0.6f * t);
		strokes.Add(new SpellStrokes.Stroke
		{
			A = origin, B = origin, HalfA = knot * 0.5f, HalfB = knot * 0.5f, SpineHalf = knot * 0.5f,
			Spine = 0, Body = 0, Glow = knot * 0.5f, Density = 0.45f, GlowTone = 1, Dissolve = gone,
		});
		strokes.Draw(this, Colors.White);
	}

	// One straight run of the bolt: a white-hot spine, a hot body, the edge as a dithered glow round it.
	private void Bolt(Vector2 a, Vector2 b, float half, float glow, float gone)
	{
		strokes.Add(new SpellStrokes.Stroke
		{
			A = a, B = b, HalfA = half, HalfB = half, SpineHalf = MathF.Max(1f, half * 0.43f),
			Spine = 0, Body = 1, Glow = glow, Density = 0.5f, GlowTone = 3, Dissolve = gone,
		});
	}
}
