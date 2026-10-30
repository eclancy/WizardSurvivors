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

	/// <summary>A slow breath, never a flicker: a flicker is fire or his, a breath is a secret.</summary>
	public const float GlintPeriod = 2.4f;

	/// <summary>0 to 1 and back over <see cref="GlintPeriod"/>.</summary>
	public static float Breath(float clock) => 0.5f - 0.5f * MathF.Cos(clock * Mathf.Tau / GlintPeriod);

	/// <summary>
	/// The warm glint (.ai/stage-secrets.md section 2): a 2 x 2 art-pixel core (4 x 4 on screen) of gold.hi
	/// and a one-pixel halo, breathing. Never a bloom - a glint the size of a spell reads as a spell.
	/// <paramref name="at"/> is in the canvas item's own space.
	/// </summary>
	public static void DrawGlint(CanvasItem canvas, Vector2 at, float clock, float strength = 1f)
	{
		float breath = Breath(clock);
		canvas.DrawRect(new Rect2(at - new Vector2(4f, 4f), new Vector2(8f, 8f)), new Color(GoldHi, (0.18f + 0.22f * breath) * strength));
		canvas.DrawRect(new Rect2(at - new Vector2(2f, 2f), new Vector2(4f, 4f)), new Color(GoldHi, (0.45f + 0.55f * breath) * strength));
	}
}
