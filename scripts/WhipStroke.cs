using Godot;
using System;

namespace WizardSurvivors.scripts;

// THE STROKE OF A WHIP (2026-10-31), shared by Thorned Vine (ThornVine) and Stormsweep. Eric: "The vine
// lash ... its animation just rotates, I want it to look more like the real arc of a whip. Similarly, the
// electric whip spell should look more like a whip crack as well."
//
// What a real whip does, and what this lays out: the HAND LEADS - it swings to where the stroke ends while
// the lash beyond it still trails where it was. The BEND between the two, with a LOOP in it, rolls out along
// the lash toward the tip, and the lash behind it lies nearly straight on the new line. The tip trails the
// whole way, and as the bend reaches it, it catches up all at once - fastest at the very end of the stroke,
// which is the CRACK. A travelling wave down a flexible line, not a stick turning about its root.
//
// STROKE SPACE. A spell describes its stroke in two numbers: s along the lash (0 the hand or root .. 1 the
// tip) and w across the stroke (0 the pose it starts from .. 1 the pose it lands on), and maps them onto the
// ground it sweeps itself - Thorned Vine round a fan about its root (w an angle), Stormsweep down a band
// beside the wizard (w a height). Front(u, s) is where the lash lies across at s, u of the way through the
// stroke, and it is THE ONE AUTHORITY on what the stroke has swept: both spells draw the lash out of it and
// test their hits against it, as GroundSlamAttack.Contains is for an enemy's slam. It only ever moves
// forward at every s, so the ground behind it at s - from where the stroke started to Front - is exactly
// the ground the lash has crossed there, and at u = 1 it is 1 everywhere: the whole of it.
//
// THE LOOP is spliced into the lash at the bend (Splice), turning on the side the lash has already swept,
// and every point of it is held behind the lash itself - so the loop only ever covers ground the hit has
// already covered, and nothing is struck that was not drawn across.
//
// THE CRACK is the tip landing: u = 1, at (s, w) = (1, 1). The tip is the last part of the lash to move and
// the fastest when it lands (Front's trail eases OUT of the bend, so the tip's speed is greatest as it
// arrives), so a spell plays its crack there and then.
//
// Nothing here allocates: every caller hands in spans of its own fixed buffers.
public static class WhipStroke
{
	/// <summary>The lash's own points, hand to tip, evenly spaced in s.</summary>
	public const int Samples = 17;

	/// <summary>The loop's points: where it leaves the lash, its turn, and where it comes back (both ends included).</summary>
	public const int LoopPoints = 14;

	/// <summary>Points in a laid-out lash: its own and the loop spliced into them.</summary>
	public const int Points = Samples + LoopPoints;

	/// <summary>How one spell's whip moves: four numbers, all in stroke space.</summary>
	public readonly struct Shape
	{
		/// <summary>How far behind the hand, in w, the lash beyond the bend trails (1: it lies where it started).</summary>
		public readonly float Lag;
		/// <summary>When the hand lands (u).</summary>
		public readonly float HandEnd;
		/// <summary>When the bend leaves the hand to roll out (u).</summary>
		public readonly float WaveStart;
		/// <summary>How much of the lash (s) the bend takes up: the shorter, the sharper the step and the snap.</summary>
		public readonly float Bend;

		public Shape(float lag, float handEnd, float waveStart, float bend)
		{
			Lag = lag;
			HandEnd = MathF.Max(0.05f, handEnd);
			WaveStart = Mathf.Clamp(waveStart, 0f, 0.9f);
			Bend = MathF.Max(0.02f, bend);
		}
	}

	/// <summary>
	/// Where the lash lies across the stroke (0 where it started .. 1 where it lands) at <paramref name="s"/>
	/// along it, <paramref name="u"/> of the way through the stroke. Never decreases with u.
	/// </summary>
	public static float Front(float u, float s, in Shape shape) =>
		FrontAt(Smooth(u / shape.HandEnd), Bend(u, shape), s, shape);

	// Front with the two numbers that depend only on u worked out once (Lay does a whole lash with them).
	private static float FrontAt(float hand, float bend, float s, in Shape shape)
	{
		float x = Clamp01((s - bend) / shape.Bend);
		// Behind the bend the lash is on the hand's line; through it, it eases out to the full trail.
		float trail = 1f - (1f - x) * (1f - x);
		return MathF.Max(0f, hand - shape.Lag * trail);
	}

	/// <summary>Where along the lash (s) the bend begins, u of the way through: 0 at the hand, 1 as the tip cracks.</summary>
	public static float Bend(float u, in Shape shape)
	{
		float t = Clamp01((u - shape.WaveStart) / (1f - shape.WaveStart));
		// Faster as it goes: a tapering lash carries the wave quicker toward its thin end.
		return MathF.Pow(t, 1.4f);
	}

	/// <summary>The lash's samples at <paramref name="u"/>: Front at s = i / (Samples - 1).</summary>
	public static void Lay(float u, in Shape shape, Span<float> w)
	{
		float hand = Smooth(u / shape.HandEnd);
		float bend = Bend(u, shape);
		for (int i = 0; i < Samples; i++)
			w[i] = FrontAt(hand, bend, i / (float)(Samples - 1), shape);
	}

	/// <summary>
	/// Where the loop is (s) and how big, 0..1 of the caller's loop radius: it forms at the hand as the stroke
	/// begins, rides the bend out, thinning with the lash, and is gone as the tip cracks.
	/// </summary>
	public static void Loop(float u, in Shape shape, out float at, out float size)
	{
		float bend = Bend(u, shape);
		at = MathF.Min(1f, bend + shape.Bend * 0.3f);
		float forming = Smooth(u / (shape.WaveStart + 0.15f));
		float running = Clamp01((1f - bend) / shape.Bend);
		size = forming * MathF.Sqrt(running) * (1f - 0.45f * at);
	}

	/// <summary>Linear between the samples: the lash's w at <paramref name="s"/>.</summary>
	public static float Sample(ReadOnlySpan<float> w, float s)
	{
		float f = Clamp01(s) * (Samples - 1);
		int i = Math.Min((int)f, Samples - 2);
		return w[i] + (w[i + 1] - w[i]) * (f - i);
	}

	/// <summary>
	/// The lash in stroke space, <see cref="Points"/> pairs (s, w), hand first: the samples
	/// <paramref name="w"/> with a loop of <paramref name="radius"/> px spliced in at <paramref name="at"/>.
	/// <paramref name="alongPx"/> is px per unit of s; <paramref name="acrossPx"/> px per unit of w - at the
	/// tip, on a <paramref name="fan"/>, where it grows with s. The loop turns toward the side the lash has
	/// swept (lower w) and every point of it is held behind the lash and above <paramref name="floor"/>.
	/// </summary>
	public static void Splice(ReadOnlySpan<float> w, float at, float radius, float alongPx, float acrossPx, bool fan,
		float floor, Span<Vector2> sw)
	{
		at = Clamp01(at);
		int split = Math.Min(Samples, (int)(at * (Samples - 1) + 1e-4f) + 1);
		int o = 0;
		for (int i = 0; i < split; i++)
			sw[o++] = new Vector2(i / (float)(Samples - 1), w[i]);

		float wb = Sample(w, at);
		float step = 0.5f / (Samples - 1);
		float slope = (Sample(w, at + step) - Sample(w, at - step)) / (2f * step);
		float across = MathF.Max(1f, acrossPx * (fan ? MathF.Max(at, 0.15f) : 1f));
		float along = MathF.Max(1f, alongPx);
		// The lash's heading here in px, and the normal on its swept side: its across part is -t.X, which
		// is always below zero, because the lash always runs outward.
		var t = new Vector2(along, across * slope).Normalized();
		var n = new Vector2(t.Y, -t.X);
		for (int k = 0; k < LoopPoints; k++)
		{
			float a = Mathf.Tau * k / (LoopPoints - 1);
			// A circle on the lash at the bend, a little long along it: the loop a whip throws.
			Vector2 off = t * (radius * 1.2f * MathF.Sin(a)) + n * (radius * (1f - MathF.Cos(a)));
			float s = Clamp01(at + off.X / along);
			float ww = MathF.Min(wb + off.Y / across, Sample(w, s));
			sw[o++] = new Vector2(s, MathF.Max(ww, floor));
		}

		for (int i = split; i < Samples; i++)
			sw[o++] = new Vector2(i / (float)(Samples - 1), w[i]);
	}

	private static float Clamp01(float x) => x < 0f ? 0f : (x > 1f ? 1f : x);

	private static float Smooth(float x)
	{
		x = Clamp01(x);
		return x * x * (3f - 2f * x);
	}
}
