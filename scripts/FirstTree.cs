using Godot;
using System;
using WizardSurvivors.scripts;

// F4, the First Tree - the Enchanted Forest's keepsake (.ai/stage-secrets.md 6.1), and the first found
// level (issue #20). A barren clearing ten to twelve chunks out, ringed by dead stumps, with a cracked
// seed-bed in the middle and a dim glint in it. Bring the seed a Fenced Bloom drops and stand in the bed:
// it goes into the ground and a sapling grows over six seconds. It pays a full heal in the run and, the
// first time ever on the save, one found level of Vitality (+5 max HP), which stacks above the shop's cap.
//
// Without a seed the clearing is only a clearing; the glint brightens while he carries one, which says
// he has the right thing (and the seed itself leans this way - SecretSeed). Placed once at stage start by
// the SecretDirector, its ground reserved so no tree or rock is ever furnished on it.
//
// Its clearing - the dry ground, the stumps and the bed - and the sapling are two strips drawn by
// tools/art/secret_forest.py, on the world's 2-unit grid (the secrets' pixel-art pass, 2026-10-30: it
// used to draw them in code, with a translucent disc of ground, antialiased polygons and smooth circles).
// The glint and the grown tree's light are drawn here, as opaque gold stops (SecretSite.DrawGlint).
//
// Placed by the SecretDirector as the Forest's keepsake (SecretPlacement.Keepsake, stream 7 - the same
// draw it has always had), from its catalog entry in SecretCatalog.EnchantedForest.
public partial class FirstTree : SecretSite
{
	/// <summary>The ground kept clear for it: the stump ring and a margin.</summary>
	public const float Footprint = 220f;

	private const float BedRadius = 46f;
	private const float GrowSeconds = 6f;

	// The strips (secret_forest.py, saved at x2): the clearing's two frames (barren, planted), 440 x 280,
	// the bed's middle at (220, 156); the sapling's six (one a second of its growth), 96 x 120, its foot
	// at (48, 114). Its stem stands STEM art pixels tall at each frame - the crown's middle, where the
	// grown tree's light is.
	private const string ClearingPath = "res://assets/bonelight/world/forest/first-tree-clearing.png";
	private const string SaplingPath = "res://assets/bonelight/world/forest/first-tree-sapling.png";
	private static readonly Vector2 ClearingCell = new(440f, 280f);
	private static readonly Vector2 ClearingAnchor = new(-220f, -156f);
	private static readonly Vector2 SaplingCell = new(96f, 120f);
	private static readonly Vector2 SaplingAnchor = new(-48f, -114f);
	private static readonly int[] Stem = { 3, 8, 15, 22, 29, 35 };

	private enum Step { Barren, Growing, Grown }
	private Step step = Step.Barren;
	private float grown;
	private float clock;
	private Texture2D clearing;
	private Texture2D sapling;

	public override void _Ready()
	{
		ZIndex = -5; // on the ground, under the actors, as an event's site is
		clearing = ResourceLoader.Exists(ClearingPath) ? GD.Load<Texture2D>(ClearingPath) : null;
		sapling = ResourceLoader.Exists(SaplingPath) ? GD.Load<Texture2D>(SaplingPath) : null;
		if (clearing == null || sapling == null)
			GD.PushError("FirstTree: its art is missing; re-run python tools/art/secret_forest.py, then import.");
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		Player player = Director?.Player;
		if (player == null)
			return;
		bool near = player.GlobalPosition.DistanceSquaredTo(GlobalPosition) < 1400f * 1400f;

		if (step == Step.Barren && Director.Active && Director.Carried != null
			&& player.GlobalPosition.DistanceTo(GlobalPosition) <= BedRadius)
		{
			Director.PlantCarriedSeed();
			step = Step.Growing;
			SfxPlayer.AtPosition(SfxCatalog.EventReward, GlobalPosition, 0f);
		}
		else if (step == Step.Growing && Director.Active)
		{
			grown = MathF.Min(1f, grown + dt / GrowSeconds);
			if (grown >= 1f)
			{
				step = Step.Grown;
				player.Heal(player.MaxHP);
				Director.Found(SecretCatalog.FirstTreeId);
			}
		}

		if (near)
			QueueRedraw();
	}

	public override void _Draw()
	{
		// The clearing and the sapling: the strips, their art pixels on the world's 2-unit grid wherever
		// the seed put the clearing (AlignToWorldGrid shifts the drawing by under a pixel; nothing that is
		// tested moves). The clearing's second frame is the bed turned over, once the seed is in it.
		SpellInk.AlignToWorldGrid(this);
		if (clearing != null)
			DrawTextureRectRegion(clearing, new Rect2(ClearingAnchor, ClearingCell),
				new Rect2(new Vector2(step == Step.Barren ? 0f : ClearingCell.X, 0f), ClearingCell));
		float t = step == Step.Grown ? 1f : grown;
		// One frame a second of its growth: a sprout, a stem, leaves, then a crown filling out.
		int frame = Math.Min(Stem.Length - 1, (int)(t * Stem.Length));
		if (step != Step.Barren && sapling != null)
			DrawTextureRectRegion(sapling, new Rect2(SaplingAnchor, SaplingCell),
				new Rect2(new Vector2(frame * SaplingCell.X, 0f), SaplingCell));
		DrawSetTransform(Vector2.Zero);

		// The near tell: dim while he has nothing to plant, bright while he carries a seed, and a
		// steady light in the grown tree's crown. Opaque gold stops on the world's grid; DrawGlint finds
		// the grid itself.
		Vector2 head = step == Step.Barren ? new Vector2(0f, -6f) : new Vector2(0f, -Stem[frame] * 2f);
		if (step == Step.Grown)
		{
			DrawLight(this, head, 10f, 0.6f);
			return;
		}
		DrawGlint(this, head, clock, Director?.Carried != null ? 1f : 0.45f);
	}
}
