using Godot;
using System;
using WizardSurvivors.scripts;

// THE WARM LIGHT OF A SPELL VAULT, as pixel art (.ai/achievements-and-secrets.md 6.4; .ai/art-direction.md
// sections 1-3). Two things every vault of the Swamp, the Ruins, the Frozen Waste and the Sands draws:
//
//   - the near tell, the warm glint that breathes (.ai/stage-secrets.md section 2): a 2 x 2 art-pixel core
//     of the gold row whose TONE steps with the breath - base, lit, hi and back - with a plus of halo
//     cells round it that dissolve in and out through the Bayer matrix. Never translucent, never a bloom,
//     on the world's 2-unit cell. (SecretSite.DrawGlint draws the same glint as translucent rects; it is
//     the core's and is being redrawn elsewhere, so these vaults do not lean on it.)
//   - the light he missed leaving the vault and going to him, which is the moment it pays: it rises out
//     of the vault for a beat, then arcs to him and goes in. A mote of gold - white-hot heart, the gold
//     row round it - with a short trail of cells behind it that dissolve as they age.
//
// The canvas it is drawn on must be WORLD-ALIGNED (TopLevel at the origin, as PixelInk asks), so every
// cell lands on the world's grid with the floor and the shader-drawn parts under it. Nothing allocates.
public sealed class VaultLight
{
	/// <summary>The beat it hangs over the vault before it goes, and how long it takes to reach him.</summary>
	public const float RiseSeconds = 0.45f;
	public const float TravelSeconds = 0.9f;

	// The gold row (.ai/art-direction.md section 3), the only warm light on screen that is not his.
	public static readonly Color GoldHi = new(1.0f, 0.941f, 0.753f);      // #FFF0C0
	public static readonly Color GoldLit = new(0.847f, 0.690f, 0.290f);   // #D8B04A
	public static readonly Color GoldBase = new(0.541f, 0.416f, 0.118f);  // #8A6A1E
	public static readonly Color GoldShade = new(0.329f, 0.247f, 0.063f); // #543F10
	public static readonly Color LightCore = new(1f, 1f, 1f);             // light core #FFFFFF

	private float age = -1f;
	private Vector2 from;

	public bool InFlight => age >= 0f;

	/// <summary>Lets the light go from <paramref name="at"/> (world): it rises, then travels.</summary>
	public void Release(Vector2 at)
	{
		from = at;
		age = 0f;
	}

	/// <summary>Advances it; true on the one frame it reaches him (the moment the vault pays).</summary>
	public bool Tick(float delta)
	{
		if (age < 0f)
			return false;
		age += delta;
		if (age < RiseSeconds + TravelSeconds)
			return false;
		age = -1f;
		return true;
	}

	/// <summary>Where it is at <paramref name="t"/> seconds since it was let go, travelling to <paramref name="to"/> (world).</summary>
	public Vector2 PositionAt(float t, Vector2 to)
	{
		Vector2 top = from + new Vector2(0f, -36f);
		if (t < RiseSeconds)
			return from.Lerp(top, Mathf.Clamp(t / RiseSeconds, 0f, 1f));
		float u = Mathf.Clamp((t - RiseSeconds) / TravelSeconds, 0f, 1f);
		float eased = u * u * (3f - 2f * u);
		return top.Lerp(to, eased) + new Vector2(0f, -60f * MathF.Sin(u * MathF.PI));
	}

	/// <summary>
	/// The mote on its way to <paramref name="to"/> (world: his chest, say), on a world-aligned canvas: a
	/// white-hot core in a plus of gold, a dithered halo, and a trail of cells dissolving behind it.
	/// </summary>
	public void Draw(CanvasItem canvas, Vector2 to)
	{
		if (age < 0f)
			return;
		for (int k = 6; k >= 1; k--)
		{
			float back = age - k * 0.035f;
			if (back < 0f)
				continue;
			Vector2 p = PositionAt(back, to);
			PixelInk.Block(canvas, p, 1, k <= 2 ? GoldLit : GoldBase, 1f - k / 7f);
		}
		Vector2 at = PositionAt(age, to);
		PixelInk.Block(canvas, at, 5, GoldShade, 0.3f);
		PixelInk.Block(canvas, at + new Vector2(-PixelInk.Grid, 0f), 3, GoldLit);
		PixelInk.Block(canvas, at + new Vector2(PixelInk.Grid, 0f), 3, GoldLit);
		PixelInk.Block(canvas, at + new Vector2(0f, -PixelInk.Grid), 3, GoldLit);
		PixelInk.Block(canvas, at + new Vector2(0f, PixelInk.Grid), 3, GoldLit);
		PixelInk.Block(canvas, at, 3, GoldHi);
		PixelInk.Block(canvas, at, 1, LightCore);
	}

	/// <summary>
	/// The breathing glint at <paramref name="at"/> (world), on a world-aligned canvas. Its core is two art
	/// pixels square and steps through the gold row with the breath; the halo is a plus of cells that
	/// dissolves in with the breath and out again. <paramref name="strength"/> 0..1 holds it dimmer - deep
	/// in mud, or under snow - without ever going translucent.
	/// </summary>
	public static void DrawGlint(CanvasItem canvas, Vector2 at, float clock, float strength = 1f)
	{
		float breath = SecretSite.Breath(clock) * Mathf.Clamp(strength, 0f, 1f);
		Color core = breath > 0.62f ? GoldHi : breath > 0.28f ? GoldLit : GoldBase;
		float g = PixelInk.Grid;
		// The core: two art pixels square, its corner on the cell that holds `at`.
		Vector2 c = new Vector2(MathF.Floor(at.X / g) * g, MathF.Floor(at.Y / g) * g);
		canvas.DrawRect(new Rect2(c - new Vector2(g, g), new Vector2(2f * g, 2f * g)), core);
		// The halo: the plus round it, dissolving with the breath; the diagonals only at its height.
		float halo = Mathf.Clamp(breath * 1.25f - 0.1f, 0f, 1f);
		Color ring = breath > 0.62f ? GoldLit : GoldBase;
		PixelInk.Cell(canvas, c + new Vector2(-2f * g, -g) + new Vector2(1f, 1f), ring, halo);
		PixelInk.Cell(canvas, c + new Vector2(-2f * g, 0f) + new Vector2(1f, 1f), ring, halo);
		PixelInk.Cell(canvas, c + new Vector2(g, -g) + new Vector2(1f, 1f), ring, halo);
		PixelInk.Cell(canvas, c + new Vector2(g, 0f) + new Vector2(1f, 1f), ring, halo);
		PixelInk.Cell(canvas, c + new Vector2(-g, -2f * g) + new Vector2(1f, 1f), ring, halo);
		PixelInk.Cell(canvas, c + new Vector2(0f, -2f * g) + new Vector2(1f, 1f), ring, halo);
		PixelInk.Cell(canvas, c + new Vector2(-g, g) + new Vector2(1f, 1f), ring, halo);
		PixelInk.Cell(canvas, c + new Vector2(0f, g) + new Vector2(1f, 1f), ring, halo);
		if (breath > 0.8f)
		{
			PixelInk.Cell(canvas, c + new Vector2(-2f * g, -2f * g) + new Vector2(1f, 1f), GoldBase, 0.6f);
			PixelInk.Cell(canvas, c + new Vector2(g, g) + new Vector2(1f, 1f), GoldBase, 0.6f);
		}
	}

	/// <summary>
	/// Cells thrown out from <paramref name="centre"/> and falling back (a splash of mud, snow off a dug
	/// stone, sparks off glass): <paramref name="count"/> of them on fixed bearings, out to
	/// <paramref name="reach"/>, at <paramref name="t"/> 0..1 through the throw. They arc up and drop, and
	/// dissolve over the last third. World-aligned canvas; nothing allocates.
	/// </summary>
	public static void DrawThrow(CanvasItem canvas, Vector2 centre, float t, int count, float reach, Color a, Color b, float seed = 0f)
	{
		if (t < 0f || t >= 1f)
			return;
		float spread = 1f - (1f - t) * (1f - t);
		float cover = t < 0.66f ? 1f : 1f - (t - 0.66f) / 0.34f;
		for (int k = 0; k < count; k++)
		{
			float angle = (k + 0.37f * MathF.Sin(seed + k * 2.1f)) * Mathf.Tau / count;
			float r = reach * (0.55f + 0.45f * (0.5f + 0.5f * MathF.Sin(seed * 1.7f + k * 3.3f)));
			Vector2 dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.6f);
			Vector2 p = centre + dir * r * spread + new Vector2(0f, -26f * MathF.Sin(t * MathF.PI));
			PixelInk.Block(canvas, p, k % 3 == 0 ? 2 : 1, k % 2 == 0 ? a : b, cover);
		}
	}
}
