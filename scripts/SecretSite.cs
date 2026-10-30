using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// What every stage secret is in the world: a code-made Node2D, no scene, that the SecretDirector placed
// or attached, and that draws its own near tell (.ai/stage-secrets.md 7.1 and 7.3). Behaviour on top of
// art: a subclass is one secret's rules and drawing, and the base is what every secret shares -
//
//   - its id and its director, and its progress in the director's run state (StateOf(Key)), never on
//     the node, so a site freed with its chunk and made again reads its progress back;
//   - the seen mark: the director notes the moment the player is within SeenRadius of TellPosition and
//     records it on the save (.ai/achievements-and-secrets.md 4.3), so the subclass does nothing for it;
//   - the warm glint that breathes (section 2), drawn the same way everywhere;
//   - for a placed site (anchor, keepsake, vault), a hook to lay out the rest of itself from the seed
//     (LayOut), the ground it makes solid (SolidFootprints) and its far tell (FarTell).
//
// THE RULE EVERY SUBCLASS INHERITS: a secret is never in "enemies" and never a target. Every targeting
// path iterates that group, and a crystal or a lantern in it would pull the whole loadout off the swarm.
public abstract partial class SecretSite : Node2D
{
	public SecretDirector Director { get; set; }

	/// <summary>The SecretCatalog id.</summary>
	public string SecretId { get; set; } = "";

	/// <summary>
	/// The run-state key: the secret's id, as there is one of most secrets a run. A secret with a site on
	/// every landmark of its kind (the Fenced Bloom) keys each by the landmark.
	/// </summary>
	public virtual string Key => SecretId;

	/// <summary>Where the near tell is: what the player has to come near for the secret to count as seen.</summary>
	public virtual Vector2 TellPosition => GlobalPosition;

	/// <summary>How near is near enough to have seen it: the near tell is on screen by then on either canvas.</summary>
	public virtual float SeenRadius => SecretDirector.SeenRadius;

	/// <summary>This site's progress for the run.</summary>
	public SecretDirector.SiteState State => Director?.StateOf(Key);

	public SecretDefinition Definition => SecretCatalog.Get(SecretId);

	protected Player Player => Director?.Player;

	/// <summary>False once the boss has come (or the run is over): progress freezes, nothing new starts.</summary>
	protected bool Active => Director != null && Director.Active;

	public override void _EnterTree() => Director?.Register(this);

	public override void _ExitTree() => Director?.Unregister(this);

	// --- placed sites ------------------------------------------------------------------------------

	/// <summary>
	/// Called once by the director after it has set this site's position and before the site enters the
	/// tree, with the placer it was placed by: anything else the secret needs laid out from the seed (a
	/// mover's path, a set of shards) is drawn from the same stream here, so it is as fixed for the run as
	/// the site is. Footprints it keeps (placer.Reserve, or any placement through it) keep decor away.
	/// </summary>
	public virtual void LayOut(ExplorePlacer placer) { }

	/// <summary>
	/// Ground this site makes solid for the run, in world pixels: stamped into the terrain's navigation
	/// flags as a fence is (TerrainStreamer.FlagFence), so the flow field routes the horde round it and
	/// nothing is spawned or dropped inside it. Read once, after LayOut. The body that stops boots and
	/// bodies there is the site's own (on LevelTilePainter.TerrainCollisionLayer).
	/// </summary>
	public virtual IEnumerable<(Vector2 Centre, float Radius)> SolidFootprints => Array.Empty<(Vector2, float)>();

	/// <summary>
	/// The far tell, if this secret has one: decor that thickens toward it over a chunk or two
	/// (.ai/stage-secrets.md section 2). Furnished into every chunk that streams in round it.
	/// </summary>
	public virtual SecretFarTell FarTell => null;

	// --- the near tell -------------------------------------------------------------------------------

	/// <summary>The contract's gold.hi, #FFF0C0: the only warm light on screen that is not his.</summary>
	public static readonly Color GoldHi = new(1.0f, 0.941f, 0.753f);

	/// <summary>gold.lit, #D8B04A: a glint's halo and the body of a warm light.</summary>
	public static readonly Color GoldLit = new(0.847f, 0.690f, 0.290f);

	/// <summary>gold.base, #8A6A1E: a dimmed glint's core at the bottom of its breath.</summary>
	public static readonly Color GoldBase = new(0.541f, 0.416f, 0.118f);

	/// <summary>A slow breath, never a flicker: a flicker is fire or his, a breath is a secret.</summary>
	public const float GlintPeriod = 2.4f;

	/// <summary>0 to 1 and back over <see cref="GlintPeriod"/>.</summary>
	public static float Breath(float clock) => 0.5f - 0.5f * MathF.Cos(clock * Mathf.Tau / GlintPeriod);

	private const float Grid = PixelInk.Grid;

	/// <summary>
	/// The warm glint (.ai/stage-secrets.md section 2): a 2 x 2 art-pixel core (4 x 4 on screen) and a
	/// one-pixel halo round it, breathing. Never a bloom - a glint the size of a spell reads as a spell.
	/// <paramref name="at"/> is in the canvas item's own space; <paramref name="strength"/> under 1 dims it
	/// (the First Tree's bed while he has nothing to plant).
	/// </summary>
	/// <remarks>
	/// PIXEL ART (2026-10-30, the secrets' pass onto the spells' contract - .ai/art-direction.md sections 1 and
	/// 3, .ai/art-replacement-manifest.md section 9). It was two translucent rects anywhere in the canvas,
	/// half-pixel offsets and all. Now every pixel is an opaque stop of the gold row on the WORLD's 2-unit
	/// grid, wherever the canvas stands, and the breath is a dissolve through the 4x4 Bayer matrix
	/// (PixelInk.Keeps), never alpha: the core's gold.hi pixels come and go over gold.lit (gold.base when
	/// dimmed), and the halo's gold.lit pixels with them, so at the bottom of a breath it is a small dull-gold
	/// square and at the top a pale one with a scatter round it. The canvas must be unrotated and unscaled,
	/// with no draw transform set when this is called: the grid is found from its global origin.
	/// </remarks>
	public static void DrawGlint(CanvasItem canvas, Vector2 at, float clock, float strength = 1f)
	{
		float breath = Breath(clock);
		Vector2 o = canvas.GetGlobalTransform().Origin;
		// The core's top-left art pixel on the world's grid: the four cells round the corner nearest it.
		int cx = (int)MathF.Round((o.X + at.X) / Grid) - 1;
		int cy = (int)MathF.Round((o.Y + at.Y) / Grid) - 1;
		float core = (0.45f + 0.55f * breath) * strength;
		float halo = (0.15f + 0.35f * breath) * strength;
		Color under = strength >= 0.6f ? GoldLit : GoldBase;
		for (int y = -1; y <= 2; y++)
		{
			for (int x = -1; x <= 2; x++)
			{
				bool rowIn = y >= 0 && y <= 1, colIn = x >= 0 && x <= 1;
				// The halo is the four sides of the core, two cells each: no corners, so it is round.
				if (!rowIn && !colIn)
					continue;
				int wx = cx + x, wy = cy + y;
				Color c;
				if (rowIn && colIn)
					c = PixelInk.Keeps(wx, wy, core) ? GoldHi : under;
				else if (PixelInk.Keeps(wx, wy, halo))
					c = GoldLit;
				else
					continue;
				canvas.DrawRect(new Rect2(wx * Grid - o.X, wy * Grid - o.Y, Grid, Grid), c);
			}
		}
	}

	/// <summary>
	/// A warm light at rest or in flight - the light a vault lets go travelling to him, an opened flower's
	/// steady glow: a disc of art pixels on the world's grid, gold.hi at its heart, gold.lit round it,
	/// thinning by the Bayer matrix toward <paramref name="radius"/> - opaque, never a translucent bloom.
	/// <paramref name="density"/> thins the outer ring further (0 leaves only the heart). Same canvas rule as
	/// <see cref="DrawGlint"/>.
	/// </summary>
	public static void DrawLight(CanvasItem canvas, Vector2 at, float radius, float density = 1f)
	{
		if (radius < Grid)
			return;
		Vector2 o = canvas.GetGlobalTransform().Origin;
		Vector2 w = o + at;
		int ccx = (int)MathF.Round(w.X / Grid), ccy = (int)MathF.Round(w.Y / Grid);
		int r = (int)MathF.Ceiling(radius / Grid);
		for (int y = -r; y < r; y++)
		{
			for (int x = -r; x < r; x++)
			{
				float d = MathF.Sqrt((x + 0.5f) * (x + 0.5f) + (y + 0.5f) * (y + 0.5f)) * Grid / radius;
				if (d > 1f)
					continue;
				int wx = ccx + x, wy = ccy + y;
				Color c;
				if (d < 0.3f)
					c = GoldHi;
				else if (d < 0.55f)
					c = PixelInk.Keeps(wx, wy, (0.55f - d) / 0.25f) ? GoldHi : GoldLit;
				else if (PixelInk.Keeps(wx, wy, density * (1f - (d - 0.55f) / 0.45f)))
					c = GoldLit;
				else
					continue;
				canvas.DrawRect(new Rect2(wx * Grid - o.X, wy * Grid - o.Y, Grid, Grid), c);
			}
		}
	}
}
