using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// Debris thrown out from a hit to exactly its reach: the animation that replaced the rings the
/// player's spells used to draw round what they hit.
/// </summary>
/// <remarks>
/// "Remove aoe markers from abilities, their animations should make it clear what the hitbox is."
/// A ring at the radius - drawn before the hit, after it, or held for as long as a zone lasts - is a
/// marker; a burst whose own motion stops at the radius is an animation. So every particle here
/// travels out from the centre, eased so it leaves fast and slows, and stops inside the radius it was
/// given: a third of them (the front) land with their outer edge exactly on it, the rest short of it,
/// so what the eye is left with is where the furthest of the debris fell, not a drawn circle. Then
/// it cools down its ramp - core, hot, mid, edge - and is gone in under half a second.
///
/// THE RADIUS PASSED IN MUST BE THE ONE THE HIT TESTED - GroundSlamAttack's rule, applied to the
/// player's spells. Nothing is ever drawn past it.
///
/// Stamped on the 2 px art grid (WhipLimb.Cell), one or two squares a particle; nothing allocated.
/// The particle layout is a hash of (seed, index), so a burst needs no stored state beyond its age
/// and a pooled layer (ImpactDebrisLayer) can draw dozens of them from a fixed array.
/// </remarks>
public static class ImpactDebris
{
	public const float TravelSeconds = 0.16f;
	public const float LifeSeconds = 0.42f;

	/// <summary>Particles for a burst of this reach and sweep: denser on a wider one, bounded both ways.</summary>
	public static int CountFor(float radius, float sweep = Mathf.Tau) =>
		Mathf.Clamp((int)(radius * sweep / 16f), 8, 36);

	/// <summary>
	/// Draws one burst into <paramref name="canvas"/>, in its local coordinates.
	/// <paramref name="sweep"/> below a full turn draws a fan from <paramref name="startAngle"/> - a cone.
	/// <paramref name="tint"/> multiplies every colour (a Legendary's gold on a shared layer).
	/// </summary>
	public static void Draw(CanvasItem canvas, Vector2 centre, float radius, float age, uint seed,
		in ElementColors.Ramp ramp, int count, Color tint, float startAngle = 0f, float sweep = Mathf.Tau,
		float travel = TravelSeconds, float life = LifeSeconds, bool streaks = true)
	{
		if (age < 0f || age >= life || radius < WhipLimb.Grid * 2f || count <= 0)
			return;

		bool fullTurn = sweep >= Mathf.Tau - 0.001f;
		float grid = WhipLimb.Grid;
		for (int i = 0; i < count; i++)
		{
			float h1 = Hash(seed, i);
			float h2 = Hash(seed, i + 97);
			float h3 = Hash(seed, i + 211);
			float delay = h3 * 0.04f;
			float since = age - delay;
			if (since <= 0f)
				continue;

			// Even spacing with a little jitter: a full turn wraps, a fan keeps its edges.
			float slot = fullTurn ? i + h1 * 0.7f : i + 0.5f + (h1 - 0.5f) * 0.7f;
			float a = startAngle + slot * sweep / count;
			bool front = i % 3 == 0;
			float reach = front ? 1f : 0.3f + 0.62f * h2;

			float u = Mathf.Clamp(since / travel, 0f, 1f);
			float eased = 1f - (1f - u) * (1f - u) * (1f - u);
			// The front flies as a double square and lands as a single one.
			float size = front && u < 1f ? grid * 2f : grid;
			float d = MathF.Min(radius * reach * eased, radius - size * 0.5f);
			var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
			Vector2 at = centre + dir * d;

			float t = since / life;
			Color c = t < 0.14f ? ramp.Core : t < 0.36f ? ramp.Hot : t < 0.64f ? ramp.Mid : ramp.Edge;
			float alpha = t < 0.7f ? 1f : Mathf.Clamp((1f - t) / 0.3f, 0f, 1f);
			c = new Color(c.R * tint.R, c.G * tint.G, c.B * tint.B, alpha * tint.A);
			canvas.DrawRect(WhipLimb.Cell(at, size), c);
			// A streak behind it while it flies, one square, a stop cooler.
			if (streaks && u < 1f && d > size * 2f)
			{
				Color s = t < 0.36f ? ramp.Mid : ramp.Edge;
				canvas.DrawRect(WhipLimb.Cell(at - dir * size * 1.5f, grid),
					new Color(s.R * tint.R, s.G * tint.G, s.B * tint.B, alpha * tint.A * 0.85f));
			}
		}
	}

	// A small integer hash to 0..1, so a burst is laid out the same every frame without storing it.
	private static float Hash(uint seed, int i)
	{
		uint h = (uint)i * 374761393u + seed * 668265263u;
		h = (h ^ (h >> 13)) * 1274126177u;
		h ^= h >> 16;
		return (h & 0xFFFFFF) / 16777216f;
	}
}
