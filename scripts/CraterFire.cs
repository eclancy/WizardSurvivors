using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// The fire that stands in a crater, the smoke that rises off it and the embers it throws up - drawn
/// for one crater into whatever canvas owns it. Shared by Meteor Swarm (CraterLayer: a short blaze in
/// the bowl of every impact, a field of fire for Magma Craters) and Cataclysm (CataclysmField).
/// </summary>
/// <remarks>
/// The flames are tools/art/crater_fire.py's sheet - a looping blaze, a looping low lick and one puff's
/// life of smoke - at x2. A burning crater is FILLED with fire out to the radius it burns, blazes in the
/// bowl and licks on the ejecta, as Magma Craters' rings of flame were (.ai/art-direction.md section 3:
/// a lasting zone is the stuff that hurts, laid out to its radius). Each flame has its own beat, its own
/// moment of catching (outward from the middle) and its own moment of going out, burning low first, so
/// the fire spreads and dies by the flame rather than fading as a sheet.
///
/// Flames are laid out on a jittered grid walked from the top row down, so the nearer (lower) flames are
/// drawn over the further ones without sorting anything. The layout is a hash of the crater's seed and
/// the grid cell: no state is stored and nothing is allocated, so one node can draw dozens of craters.
///
/// The caller decides where this sits. A crater's fire is on the ground, under the actors
/// (<see cref="CraterGround.FireZ"/>): a flame never covers the wizard standing in it.
/// </remarks>
public static class CraterFire
{
	public const string TexturePath = "res://assets/bonelight/effects/crater-fire.png";
	// crater_fire.py's cell and rows.
	public const int CellW = 16;
	public const int CellH = 24;
	public const int Frames = 8;
	public const int RowBlaze = 0;
	public const int RowLick = 1;
	public const int RowSmoke = 2;
	private const float Fps = 12f;
	// The flames' x2 footprint, and the art row their feet stand on (the bottom of the cell).
	private const float DrawW = CellW * 2f;
	private const float DrawH = CellH * 2f;

	// A plain impact's fire: a blaze in the bowl that burns down inside a second.
	private const float ImpactFlameSeconds = 0.75f;
	// How long before a burn ends its flames start going out, one by one.
	private const float GoingOutSeconds = 0.6f;
	// A flame's last moments are a low lick.
	private const float LickSeconds = 0.35f;

	private const float SmokeSeconds = 1.4f;
	private const float SmokeRise = 46f;
	private const float SmokePeriod = 0.9f;

	private static Texture2D sheet;

	private static Texture2D Sheet
	{
		get
		{
			if (sheet != null && GodotObject.IsInstanceValid(sheet))
				return sheet;
			sheet = GD.Load<Texture2D>(TexturePath);
			return sheet;
		}
	}

	/// <summary>True while a crater opened <paramref name="age"/> seconds ago still has fire or smoke to draw.</summary>
	public static bool IsActive(float age, float burnSeconds) =>
		age < MathF.Max(burnSeconds, ImpactFlameSeconds) + SmokeSeconds;

	/// <summary>
	/// Draws one crater's smoke and flames into <paramref name="canvas"/> at <paramref name="centre"/>
	/// (its local space). <paramref name="radius"/> is the hit radius; <paramref name="burnSeconds"/>
	/// above zero fills it with fire for that long, zero is a plain impact's short blaze in the bowl.
	/// <paramref name="tint"/> multiplies the flames (an evolution's colour, already softened by the
	/// caller), never the smoke.
	/// </summary>
	/// <remarks>
	/// Call this for every crater, then <see cref="DrawEmbers"/> for every crater: the flames are one
	/// texture and the embers are plain rects, and interleaving them crater by crater breaks the
	/// renderer's batch at every switch. In this order the fire of sixteen craters is two draw calls
	/// (measured on scenes/_CraterSheet.tscn with --draws), and their holes one more.
	/// </remarks>
	public static void DrawFire(CanvasItem canvas, Vector2 centre, float radius, float age, float burnSeconds, uint seed, Color tint)
	{
		Texture2D tex = Sheet;
		if (tex == null || age < 0f || !IsActive(age, burnSeconds))
			return;
		float bowl = radius * CraterGround.RimShare;
		DrawSmoke(canvas, tex, centre, bowl, age, burnSeconds, seed);
		DrawFlames(canvas, tex, centre, radius, bowl, age, burnSeconds, burnSeconds > 0f, seed, tint);
	}

	/// <summary>The embers thrown up out of one crater's fire; see <see cref="DrawFire"/> for the order.</summary>
	public static void DrawEmbers(CanvasItem canvas, Vector2 centre, float radius, float age, float burnSeconds, uint seed, Color tint)
	{
		if (age < 0f || !IsActive(age, burnSeconds))
			return;
		DrawEmbers(canvas, centre, radius, radius * CraterGround.RimShare, age, burnSeconds, burnSeconds > 0f, seed, tint);
	}

	private static void DrawFlames(CanvasItem canvas, Texture2D tex, Vector2 centre, float radius, float bowl,
		float age, float burnSeconds, bool burning, uint seed, Color tint)
	{
		// Where flames may stand: the whole burning radius (less the flame's own half width, so its
		// fire stays inside), or just the bowl of a plain impact.
		float reach = burning ? radius - 12f : bowl * 0.8f;
		if (reach < 4f)
			return;
		// Spaced so the molten crater shows between the flames: packed as tight as the old rings, the
		// fire was one blaze-shaped wall with no crater in it.
		float spacing = burning ? MathF.Max(26f, radius * 0.45f) : MathF.Max(18f, bowl * 0.6f);
		float rowStep = spacing * 0.87f * CraterGround.Squash;
		int rows = (int)MathF.Ceiling(reach * CraterGround.Squash / rowStep);
		int cols = (int)MathF.Ceiling(reach / spacing) + 1;

		for (int j = -rows; j <= rows; j++)
		{
			for (int i = -cols; i <= cols; i++)
			{
				int cell = (j + 64) * 131 + (i + 64);
				float h1 = Hash(seed, cell);
				float h2 = Hash(seed, cell + 7919);
				float h3 = Hash(seed, cell + 104729);
				float x = i * spacing + ((j & 1) != 0 ? spacing * 0.5f : 0f) + (h1 - 0.5f) * spacing * 0.55f;
				float y = j * rowStep + (h2 - 0.5f) * rowStep * 0.5f;
				float e = (x / reach) * (x / reach) + (y / (reach * CraterGround.Squash)) * (y / (reach * CraterGround.Squash));
				if (e > 1f)
					continue;
				float out01 = MathF.Sqrt(e);
				bool inBowl = out01 * reach < bowl * 0.9f;
				// Out on the ejecta the fire is patchy: burning rock here and there, not a sheet.
				if (burning && !inBowl && h2 < 0.5f)
					continue;

				// Catches outward from the middle, goes out by the flame.
				float ignite = 0.03f + 0.17f * out01 + h3 * 0.04f;
				float goesOut = burning
					? burnSeconds - GoingOutSeconds * h1
					: ImpactFlameSeconds * (0.45f + 0.55f * h2) * (1f - 0.35f * out01);
				if (age < ignite || age >= goesOut)
					continue;
				float lit = age - ignite;
				float left = goesOut - age;
				int row = lit < 0.08f || left < LickSeconds || (burning && !inBowl && h2 < 0.8f) ? RowLick : RowBlaze;
				int frame = ((int)(age * Fps + h3 * Frames)) % Frames;

				Vector2 foot = centre + new Vector2(x, y);
				var dest = new Rect2(Snap(foot.X - DrawW * 0.5f), Snap(foot.Y - DrawH + 2f), DrawW, DrawH);
				var src = new Rect2(frame * CellW, row * CellH, CellW, CellH);
				canvas.DrawTextureRectRegion(tex, dest, src, tint);
			}
		}
	}

	// Puffs rising off the bowl: once, after a plain impact's blaze; on a steady beat while a crater
	// burns, and a last few as it goes out. Drawn first, behind the fire.
	private static void DrawSmoke(CanvasItem canvas, Texture2D tex, Vector2 centre, float bowl, float age, float burnSeconds, uint seed)
	{
		int count = Math.Clamp((int)(bowl / 12f), 2, 6);
		for (int i = 0; i < count; i++)
		{
			float hi = Hash(seed, 300 + i);
			if (burnSeconds <= 0f)
			{
				float born = 0.18f + i * 0.1f + hi * 0.1f;
				DrawPuff(canvas, tex, centre, bowl, age - born, seed, 500 + i);
				continue;
			}
			// Two generations of each puff can be in the air at once: the life outlasts the beat.
			float offset = i * SmokePeriod / count + hi * 0.2f;
			int generation = (int)MathF.Floor((age - offset) / SmokePeriod);
			for (int g = generation - 1; g <= generation; g++)
			{
				float born = offset + g * SmokePeriod;
				if (g < 0 || born > burnSeconds + 0.4f)
					continue;
				DrawPuff(canvas, tex, centre, bowl, age - born, seed, 500 + i * 37 + g * 101);
			}
		}
	}

	private static void DrawPuff(CanvasItem canvas, Texture2D tex, Vector2 centre, float bowl, float since, uint seed, int index)
	{
		if (since < 0f || since >= SmokeSeconds)
			return;
		float t = since / SmokeSeconds;
		float ha = Hash(seed, index);
		float hb = Hash(seed, index + 61);
		float angle = ha * Mathf.Tau;
		Vector2 from = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * CraterGround.Squash) * bowl * 0.6f * MathF.Sqrt(hb);
		float rise = SmokeRise * (0.8f + 0.4f * hb) * MathF.Pow(t, 0.8f);
		Vector2 at = centre + from + new Vector2(MathF.Sin(t * 3f + ha * 6f) * 5f + 10f * t, -rise);
		int frame = Math.Clamp((int)(t * Frames), 0, Frames - 1);
		// The puff's middle sits at art row 15 of its cell.
		var dest = new Rect2(Snap(at.X - DrawW * 0.5f), Snap(at.Y - 30f), DrawW, DrawH);
		var src = new Rect2(frame * CellW, RowSmoke * CellH, CellW, CellH);
		// The sprite thins itself; the alpha takes the last of it, so it thins rather than blinks out.
		canvas.DrawTextureRectRegion(tex, dest, src, new Color(1f, 1f, 1f, 0.8f * (1f - t * t)));
	}

	// Embers: one art pixel each, thrown up out of the fire, drifting, cooling down the fire ramp and
	// gone. A burning crater keeps throwing them; a plain impact throws one handful.
	private static readonly ElementColors.Ramp FireRamp = ElementColors.GetRamp(Element.Fire);

	private static void DrawEmbers(CanvasItem canvas, Vector2 centre, float radius, float bowl, float age,
		float burnSeconds, bool burning, uint seed, Color tint)
	{
		float area = burning ? radius - 10f : bowl;
		int count = burning ? Math.Clamp((int)(radius / 7f), 6, 18) : Math.Clamp((int)(bowl / 4f), 5, 10);
		for (int k = 0; k < count; k++)
		{
			float hk = Hash(seed, 900 + k);
			float life = 0.7f + 0.5f * hk;
			float since;
			int generation;
			if (burning)
			{
				float offset = hk * life;
				if (age < offset)
					continue;
				generation = (int)MathF.Floor((age - offset) / life);
				since = age - offset - generation * life;
				// No new ember after the fire has gone out.
				if (age - since > burnSeconds - 0.2f)
					continue;
			}
			else
			{
				generation = 0;
				since = age - 0.04f - hk * 0.12f;
			}
			if (since < 0f || since >= life)
				continue;
			float t = since / life;
			int id = 900 + k * 53 + generation * 977;
			float a = Hash(seed, id) * Mathf.Tau;
			float r = area * 0.8f * MathF.Sqrt(Hash(seed, id + 1));
			Vector2 from = new Vector2(MathF.Cos(a), MathF.Sin(a) * CraterGround.Squash) * r;
			float climb = (28f + 40f * Hash(seed, id + 2)) * (1f - (1f - t) * (1f - t));
			// A plain impact's embers are thrown outward as well as up.
			Vector2 fling = burning ? Vector2.Zero : new Vector2(MathF.Cos(a), MathF.Sin(a) * 0.5f) * 26f * t;
			Vector2 at = centre + from + fling + new Vector2(MathF.Sin(t * 6f + hk * 9f) * 4f, -climb);
			Color c = t < 0.2f ? FireRamp.Core : t < 0.45f ? FireRamp.Hot : t < 0.72f ? FireRamp.Mid : FireRamp.Edge;
			float alpha = t < 0.75f ? 1f : (1f - t) / 0.25f;
			canvas.DrawRect(WhipLimb.Cell(at, WhipLimb.Grid), new Color(c.R * tint.R, c.G * tint.G, c.B * tint.B, alpha * tint.A));
		}
	}

	private static float Snap(float v) => MathF.Round(v / WhipLimb.Grid) * WhipLimb.Grid;

	// ImpactDebris's integer hash, to 0..1: the same layout every frame without storing it.
	private static float Hash(uint seed, int i)
	{
		uint h = (uint)i * 374761393u + seed * 668265263u;
		h = (h ^ (h >> 13)) * 1274126177u;
		h ^= h >> 16;
		return (h & 0xFFFFFF) / 16777216f;
	}
}
