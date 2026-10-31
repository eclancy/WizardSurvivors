using System;
using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// The ground an enemy attack will hit, marked as an opaque dashed outline in pixel art: the ring, the
/// wedge or the lane at its full reach from the first frame of the wind-up, and a solid white-hot
/// outline that dissolves on the strike.
/// </summary>
/// <remarks>
/// Eric, playing 0.9.2: "the attacks on enemies that are telegraphed through transparent buildups
/// animations are weird... remove them. if you need to telegraph an attack, do so with icon
/// animation." The shapes used to be a translucent disc with a rim, growing from a third of the reach
/// to all of it. The growing was the clock; the clock is now the icon over the attacker
/// (<see cref="TelegraphIcon"/>). But where the ground matters - a boss's ring or wedge, a charge's
/// lane, a hit placed away from its maker - the player still has to see the danger to step out of it,
/// so it is marked HERE, and marked differently:
///
/// - Opaque. Every cell is a whole square on the world's 2-unit art grid (<see cref="PixelInk"/>),
///   in the attacker's warning colour over a stroke of occlusion, which keeps a pale warning readable
///   on a pale floor and a dark one on a dark floor.
/// - Dashed: a stamped boundary, not a filled area, so it says "inside this line" without laying a
///   sheet over the bodies standing in it.
/// - Whole from the first frame, never grown. It blinks white with the icon in the wind-up's last
///   third (<see cref="TelegraphIcon.PulseLit"/>), so the ground and the attacker say "now" together.
/// - The strike is the outline drawn solid in white-hot, dissolving through the Bayer matrix - the
///   only fade the art contract allows - instead of a fading translucent sheet.
///
/// The numbers are the attack's own (<see cref="GroundSlamAttack"/>: Radius, HalfAngleDegrees,
/// HalfWidth, Facing), the same ones <see cref="GroundSlamAttack.Contains"/> tests, so the line drawn
/// and the line hit are still one number.
///
/// Draws in WORLD units on any canvas: it sets the canvas's draw transform to the inverse of its
/// global transform (enemies never rotate; an elite is scaled up and a hit punches the scale), so the
/// grid is the world's and an elite's ring is drawn at the radius it hits rather than six percent
/// wider. Resets the draw transform to identity when done. Allocates nothing.
/// </remarks>
public static class TelegraphOutline
{
	/// <summary>World units of each dash and of each gap between, before they are evened out round the shape.</summary>
	public const float DashLength = 14f;
	public const float GapLength = 9f;

	// Cells across the bright stroke and across the occlusion under it.
	private const int StrokeCells = 2;
	private const int ShadowCells = 4;

	// Chords of about this length make up a curve. Short enough that a chord sits within a cell of the arc.
	private const float CurveStep = 12f;
	private const int MaxPoints = 96;

	/// <summary>The fire ramp's core (#FFF4D0): the blink and the strike, white-hot as every Bonelight strike is.</summary>
	public static readonly Color StrikeColor = new(1.0f, 0.957f, 0.816f);
	// Occlusion, #05070C: the one dark every Bonelight thing shares.
	private static readonly Color Occlusion = new(0.020f, 0.027f, 0.047f);

	private static readonly Vector2[] path = new Vector2[MaxPoints + 1];
	private static readonly float[] along = new float[MaxPoints + 1];
	private static int pathCount;

	/// <summary>The outline through a wind-up: dashes in <paramref name="color"/>, blinking white with the icon.</summary>
	public static void Warning(CanvasItem canvas, TelegraphShape shape, float radius, float halfAngleDegrees,
		float halfWidth, Vector2 facing, Color color, float progress)
	{
		if (canvas == null || radius <= PixelInk.Grid)
			return;
		Vector2 centre = BeginWorld(canvas);
		BuildPath(centre, shape, radius, halfAngleDegrees, halfWidth, facing);
		Color ink = TelegraphIcon.PulseLit(progress) ? StrikeColor : new Color(color, 1f);

		float total = along[pathCount];
		int dashes = Math.Max(3, (int)MathF.Round(total / (DashLength + GapLength)));
		float period = total / dashes;
		float dash = period * DashLength / (DashLength + GapLength);
		// The occlusion first, a cell longer at each end so every dash is capped, then the ink on it.
		for (int k = 0; k < dashes; k++)
			Span(canvas, k * period - PixelInk.Grid, k * period + dash + PixelInk.Grid, Occlusion, ShadowCells, 1f);
		for (int k = 0; k < dashes; k++)
			Span(canvas, k * period, k * period + dash, ink, StrokeCells, 1f);
		EndWorld(canvas);
	}

	/// <summary>
	/// The strike: the outline solid and white-hot, dissolving as <paramref name="remaining"/> runs
	/// from 1 to 0.
	/// </summary>
	public static void Strike(CanvasItem canvas, TelegraphShape shape, float radius, float halfAngleDegrees,
		float halfWidth, Vector2 facing, float remaining)
	{
		if (canvas == null || radius <= PixelInk.Grid || remaining <= 0f)
			return;
		Vector2 centre = BeginWorld(canvas);
		BuildPath(centre, shape, radius, halfAngleDegrees, halfWidth, facing);
		float coverage = Mathf.Clamp(remaining * 1.25f, 0f, 1f);
		float total = along[pathCount];
		Span(canvas, 0f, total, Occlusion, ShadowCells, coverage);
		Span(canvas, 0f, total, StrikeColor, StrokeCells, coverage);
		EndWorld(canvas);
	}

	/// <summary>
	/// One short opaque stroke from <paramref name="from"/> to <paramref name="to"/>, given as world
	/// offsets from the canvas's origin, over occlusion: a caster's aim tick.
	/// </summary>
	public static void Stroke(CanvasItem canvas, Vector2 from, Vector2 to, Color color)
	{
		if (canvas == null)
			return;
		Vector2 centre = BeginWorld(canvas);
		Vector2 along = (to - from).Normalized() * PixelInk.Grid;
		PixelInk.Line(canvas, centre + from - along, centre + to + along, Occlusion, ShadowCells);
		PixelInk.Line(canvas, centre + from, centre + to, new Color(color, 1f), StrokeCells);
		EndWorld(canvas);
	}

	// Sets the canvas to draw in world units and returns where its origin is in the world.
	private static Vector2 BeginWorld(CanvasItem canvas)
	{
		Transform2D xf = canvas.GetGlobalTransform();
		Vector2 scale = xf.Scale.Abs();
		if (scale.X < 0.0001f || scale.Y < 0.0001f)
			scale = Vector2.One;
		var inverse = new Vector2(1f / scale.X, 1f / scale.Y);
		canvas.DrawSetTransform(-xf.Origin * inverse, 0f, inverse);
		return xf.Origin;
	}

	private static void EndWorld(CanvasItem canvas) => canvas.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

	// The closed boundary of the shape, in world units, with its running length. Ring: a circle of
	// chords. Cone: the apex, out along one edge, round the arc, back down the other. Line: the lane's
	// four corners, as GroundSlamAttack.Contains bounds it (from the caster, Radius long, HalfWidth each side).
	private static void BuildPath(Vector2 centre, TelegraphShape shape, float radius, float halfAngleDegrees,
		float halfWidth, Vector2 facing)
	{
		Vector2 dir = facing.LengthSquared() > 0.0001f ? facing.Normalized() : Vector2.Right;
		pathCount = 0;
		switch (shape)
		{
			case TelegraphShape.Cone:
			{
				float half = Mathf.DegToRad(halfAngleDegrees);
				float start = dir.Angle() - half;
				int steps = Math.Clamp((int)MathF.Ceiling(half * 2f * radius / CurveStep), 2, MaxPoints - 2);
				Add(centre);
				for (int i = 0; i <= steps; i++)
					Add(centre + Vector2.FromAngle(start + half * 2f * i / steps) * radius);
				Add(centre);
				break;
			}
			case TelegraphShape.Line:
			{
				Vector2 side = new Vector2(-dir.Y, dir.X) * halfWidth;
				Vector2 end = dir * radius;
				Add(centre + side);
				Add(centre + end + side);
				Add(centre + end - side);
				Add(centre - side);
				Add(centre + side);
				break;
			}
			default:
			{
				int steps = Math.Clamp((int)MathF.Ceiling(Mathf.Tau * radius / CurveStep), 16, MaxPoints);
				for (int i = 0; i <= steps; i++)
					Add(centre + Vector2.FromAngle(Mathf.Tau * i / steps) * radius);
				break;
			}
		}
		pathCount--;
	}

	private static void Add(Vector2 point)
	{
		path[pathCount] = point;
		along[pathCount] = pathCount == 0 ? 0f : along[pathCount - 1] + path[pathCount - 1].DistanceTo(point);
		pathCount++;
	}

	// Strokes the path from length s0 to s1 (wrapping round the closed path), a chord at a time.
	private static void Span(CanvasItem canvas, float s0, float s1, Color color, int cells, float coverage)
	{
		float total = along[pathCount];
		if (total <= 0f || s1 <= s0)
			return;
		// Wrap the span into [0, total) and split it where it crosses the seam.
		while (s0 < 0f)
		{
			s0 += total;
			s1 += total;
		}
		while (s0 >= total)
		{
			s0 -= total;
			s1 -= total;
		}
		if (s1 > total)
		{
			SpanOnce(canvas, s0, total, color, cells, coverage);
			SpanOnce(canvas, 0f, Math.Min(s1 - total, total), color, cells, coverage);
			return;
		}
		SpanOnce(canvas, s0, s1, color, cells, coverage);
	}

	private static void SpanOnce(CanvasItem canvas, float s0, float s1, Color color, int cells, float coverage)
	{
		for (int i = 0; i < pathCount; i++)
		{
			float a0 = along[i], a1 = along[i + 1];
			if (a1 <= s0 || a0 >= s1 || a1 <= a0)
				continue;
			float t0 = (Math.Max(s0, a0) - a0) / (a1 - a0);
			float t1 = (Math.Min(s1, a1) - a0) / (a1 - a0);
			PixelInk.Line(canvas, path[i].Lerp(path[i + 1], t0), path[i].Lerp(path[i + 1], t1), color, cells, coverage);
		}
	}
}
