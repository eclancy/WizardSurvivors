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
// It draws itself - the stumps, the bed and the sapling - on the art grid in the forest's own browns and
// the contract's gold, the way the side events' sites do (GaolersKeyEvent's cell), so it needs no sheet.
public partial class FirstTree : Node2D
{
	public SecretDirector Director { get; set; }

	/// <summary>The ground kept clear for it: the stump ring and a margin.</summary>
	public const float Footprint = 220f;

	private const float BedRadius = 46f;
	private const float GrowSeconds = 6f;
	private const float RingRadius = 170f;
	private const int Stumps = 9;
	private const float GlintPeriod = 2.4f;

	private static readonly Color Bark = new Color(0.36f, 0.26f, 0.18f);
	private static readonly Color BarkDark = new Color(0.20f, 0.14f, 0.10f);
	private static readonly Color Heartwood = new Color(0.62f, 0.50f, 0.34f);
	private static readonly Color Earth = new Color(0.17f, 0.13f, 0.10f, 0.92f);
	private static readonly Color DryGround = new Color(0.30f, 0.24f, 0.17f, 0.28f);
	private static readonly Color Crack = new Color(0.07f, 0.05f, 0.04f);
	private static readonly Color Leaf = new Color(0.45f, 0.72f, 0.36f);
	private static readonly Color LeafLit = new Color(0.66f, 0.86f, 0.48f);
	private static readonly Color GoldHi = new Color(1.0f, 0.941f, 0.753f);

	private enum Step { Barren, Growing, Grown }
	private Step step = Step.Barren;
	private float grown;
	private float clock;

	public override void _Ready()
	{
		ZIndex = -5; // on the ground, under the actors, as an event's site is
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
		// The clearing: a faint disc of dry ground, so the ring reads as a place and not as scatter.
		DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, 0.6f));
		DrawCircle(Vector2.Zero, RingRadius + 40f, DryGround);
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

		// The stump ring: each a cut trunk flaring to its roots, its top showing the rings - wood, not
		// rock - in sizes that vary round the ring so it reads as old growth, not as a fence.
		for (int i = 0; i < Stumps; i++)
		{
			float angle = i * Mathf.Tau / Stumps + 0.3f;
			Vector2 at = (Vector2.FromAngle(angle) * new Vector2(RingRadius, RingRadius * 0.62f)).Round();
			DrawStump(at, 26f + (i * 7 % 3) * 6f, 20f + (i * 5 % 4) * 5f);
		}

		// The seed-bed: a patch of dark earth, cracked dry until the sapling is in it.
		DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, 0.5f));
		DrawCircle(Vector2.Zero, BedRadius, Earth);
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
		if (step == Step.Barren)
		{
			DrawLine(new Vector2(-30f, -4f), new Vector2(-8f, 2f), Crack, 2f);
			DrawLine(new Vector2(-8f, 2f), new Vector2(6f, -8f), Crack, 2f);
			DrawLine(new Vector2(6f, -8f), new Vector2(28f, 4f), Crack, 2f);
			DrawLine(new Vector2(-4f, 2f), new Vector2(-2f, 14f), Crack, 2f);
		}

		float t = step == Step.Grown ? 1f : grown;
		if (step != Step.Barren)
			DrawSapling(t);

		// The near tell: dim while he has nothing to plant, bright while he carries a seed, and a
		// steady glow in the grown tree's crown.
		Vector2 head = step == Step.Barren ? new Vector2(0f, -6f) : new Vector2(0f, -SaplingHeight(t));
		if (step == Step.Grown)
		{
			DrawCircle(head, 34f, new Color(GoldHi, 0.12f));
			DrawRect(new Rect2(head - new Vector2(2f, 2f), new Vector2(4f, 4f)), GoldHi);
			return;
		}
		bool carrying = Director?.Carried != null;
		float breath = 0.5f - 0.5f * MathF.Cos(clock * Mathf.Tau / GlintPeriod);
		float strength = carrying ? 1f : 0.45f;
		DrawRect(new Rect2(head - new Vector2(4f, 4f), new Vector2(8f, 8f)), new Color(GoldHi, (0.12f + 0.2f * breath) * strength));
		DrawRect(new Rect2(head - new Vector2(2f, 2f), new Vector2(4f, 4f)), new Color(GoldHi, (0.35f + 0.65f * breath) * strength));
	}

	private void DrawStump(Vector2 foot, float width, float height)
	{
		float top = foot.Y - height;
		float half = width * 0.5f;
		// Roots spreading off the base on both sides.
		DrawColoredPolygon(new[]
		{
			new Vector2(foot.X - half - 8f, foot.Y), new Vector2(foot.X - half + 2f, foot.Y - 8f),
			new Vector2(foot.X + half - 2f, foot.Y - 8f), new Vector2(foot.X + half + 8f, foot.Y),
		}, BarkDark);
		// The trunk, a little narrower at the cut than at the ground.
		DrawColoredPolygon(new[]
		{
			new Vector2(foot.X - half, foot.Y - 2f), new Vector2(foot.X - half + 3f, top),
			new Vector2(foot.X + half - 3f, top), new Vector2(foot.X + half, foot.Y - 2f),
		}, Bark);
		DrawRect(new Rect2(foot.X - half + 2f, top + 2f, 4f, height - 4f), BarkDark);
		// The cut: heartwood, with one ring and a dark heart.
		DrawSetTransform(new Vector2(foot.X, top), 0f, new Vector2(1f, 0.4f));
		DrawCircle(Vector2.Zero, half - 3f, Heartwood);
		DrawArc(Vector2.Zero, (half - 3f) * 0.6f, 0f, Mathf.Tau, 16, Bark, 2f);
		DrawCircle(Vector2.Zero, 2.5f, BarkDark);
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	private static float SaplingHeight(float t) => 20f + 70f * t;

	// The sapling over its six seconds: a stem rising, two leaves, then a small crown.
	private void DrawSapling(float t)
	{
		float height = SaplingHeight(t);
		DrawRect(new Rect2(new Vector2(-3f, -height), new Vector2(6f, height)), Bark);
		DrawRect(new Rect2(new Vector2(-3f, -height), new Vector2(2f, height)), BarkDark);
		if (t > 0.25f)
		{
			DrawRect(new Rect2(new Vector2(-14f, -height * 0.5f), new Vector2(11f, 5f)), Leaf);
			DrawRect(new Rect2(new Vector2(3f, -height * 0.66f), new Vector2(11f, 5f)), Leaf);
		}
		if (t > 0.55f)
		{
			float crown = 12f + 22f * (t - 0.55f) / 0.45f;
			DrawCircle(new Vector2(0f, -height), crown, Leaf);
			DrawCircle(new Vector2(-crown * 0.4f, -height - crown * 0.3f), crown * 0.55f, LeafLit);
			DrawCircle(new Vector2(crown * 0.45f, -height + crown * 0.2f), crown * 0.45f, Leaf);
		}
	}
}
