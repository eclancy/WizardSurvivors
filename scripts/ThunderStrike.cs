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
		// Full for the first third, then it flickers out: on, off, on, gone.
		float alpha = t < 0.35f ? 1f : (MathF.Sin(age * 90f) > 0f ? 1f - t : 0.15f * (1f - t));
		ElementColors.Ramp ramp = ElementColors.GetRamp(Element.Lightning);
		DrawPolyline(stroke, ramp.Edge with { A = 0.5f * alpha }, 16f);
		DrawPolyline(forkA, ramp.Edge with { A = 0.4f * alpha }, 8f);
		DrawPolyline(forkB, ramp.Edge with { A = 0.4f * alpha }, 8f);
		DrawPolyline(stroke, ramp.Hot with { A = alpha }, 7f);
		DrawPolyline(forkA, ramp.Hot with { A = alpha }, 3.5f);
		DrawPolyline(forkB, ramp.Hot with { A = alpha }, 3.5f);
		DrawPolyline(stroke, ramp.Core with { A = alpha }, 3f);
		// Where it lands: a knot of white light, a fraction of the reach - the strike, not its area.
		float knot = MathF.Max(6f, reach * 0.16f) * (1.2f - 0.6f * t);
		DrawCircle(Vector2.Zero, knot, ramp.Hot with { A = 0.45f * alpha });
		DrawCircle(Vector2.Zero, knot * 0.5f, ramp.Core with { A = alpha });
	}
}
