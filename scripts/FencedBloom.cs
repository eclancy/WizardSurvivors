using Godot;
using System;
using WizardSurvivors.scripts;

// F1, the Fenced Bloom (.ai/stage-secrets.md 6.1): a moonflower somebody kept, behind a fence. Walk one
// full lap round the pen - outside the fence, up to LapBand px out - and it opens: healed by half, a
// seed for the First Tree, and the garden heals whoever stands in it for the rest of the run.
//
// Attached to a flower pen (Landmark) by the SecretDirector as the pen streams in. It is behaviour on
// top of the landmark's art and owns none of it: the progress lives in the director's run state, keyed
// by Landmark.Key, so a pen forgotten with its chunk and made again reads its lap back.
//
// THE TELLS. The near tell is the plan's warm glint: a 2 x 2 art-pixel core of gold.hi breathing on a
// 2.4 s period at the flower's head - warm, against a horde whose only light is cold, and the only thing
// on the pen that moves. Then the flower turns to face the player as he walks round, which is the
// whole tutorial; and the lap is drawn as it is walked, a mote of light lit on the fence for every
// stretch covered, unlit again if he walks back. When it opens the flower holds a steady glow.
public partial class FencedBloom : Node2D
{
	public SecretDirector Director { get; set; }
	public Landmark Site { get; set; }

	// From the fence posts out to here counts as walking round it (the plan's "about 200 px out").
	private const float LapBand = 200f;
	private const float HealShare = 0.5f;
	private const float SanctuaryPerSecond = 0.5f;
	private const float GlintPeriod = 2.4f;
	private const int LapMotes = 28;

	private static readonly Color GoldHi = new Color(1.0f, 0.941f, 0.753f);  // #FFF0C0, the contract's gold.hi
	private static readonly Color GoldLit = new Color(0.847f, 0.690f, 0.290f);

	private SecretDirector.SiteState state;
	private Sprite2D flower;
	private Vector2 flowerHead;
	private float lastAngle;
	private bool tracking;
	private float clock;
	private float sanctuaryOwed;

	public override void _Ready()
	{
		// Over the fence and the actors: the glint and the motes are light, and light is not sorted.
		ZIndex = 6;
		state = Director.StateOf(Site.Key);
		flower = Site.GetNodeOrNull<Sprite2D>("Flower");
		// The head: the top quarter of the flower's sprite, on its stem's column.
		flowerHead = Site.Centre + new Vector2(0f, -20f);
		if (flower?.Texture != null)
		{
			Vector2 topLeft = flower.GlobalPosition + flower.Offset;
			flowerHead = new Vector2(Site.Centre.X, topLeft.Y + flower.Texture.GetHeight() * 0.28f);
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		Player player = Director?.Player;
		if (player == null)
			return;
		Vector2 at = player.GlobalPosition;

		if (!state.Done && Director.Active)
			TrackLap(player, at);
		else
			tracking = false;
		if (state.Done && Director.Active && Site.Bounds.HasPoint(at))
			Sanctuary(player, dt);

		// The flower turns to watch him go round.
		if (flower != null)
			flower.FlipH = at.X < Site.Centre.X;

		// Drawn only while it could be on screen; a pen across the map costs nothing.
		if (at.DistanceSquaredTo(Site.Centre) < 1400f * 1400f)
			QueueRedraw();
	}

	// The lap is counted by ANGLE round the flower, not by distance walked: the pen is a 576 x 432
	// rectangle and the band round it is wider at the corners, so distance would favour hugging the
	// rails. Signed, so walking back unwinds it; leaving the band pauses it, and coming back in at a
	// different bearing does not count the arc that was skipped.
	private void TrackLap(Player player, Vector2 at)
	{
		bool inBand = !Site.Bounds.HasPoint(at) && DistanceOutside(Site.Bounds, at) <= LapBand;
		if (!inBand)
		{
			tracking = false;
			return;
		}
		float angle = (at - Site.Centre).Angle();
		if (tracking)
			state.Progress += Mathf.Wrap(angle - lastAngle, -MathF.PI, MathF.PI) / Mathf.Tau;
		lastAngle = angle;
		tracking = true;
		if (MathF.Abs(state.Progress) >= 1f)
			Bloom(player);
	}

	private void Bloom(Player player)
	{
		state.Done = true;
		state.Progress = MathF.Sign(state.Progress);
		player.Heal(Mathf.CeilToInt(player.MaxHP * HealShare));
		Director.DropSeed(flowerHead + new Vector2(0f, 40f));
		Director.Found(SecretCatalog.FencedBloomId);
	}

	// +0.5 HP a second inside the fence, in whole points as they add up.
	private void Sanctuary(Player player, float dt)
	{
		if (player.CurrentHP >= player.MaxHP)
		{
			sanctuaryOwed = 0f;
			return;
		}
		sanctuaryOwed += SanctuaryPerSecond * dt;
		if (sanctuaryOwed < 1f)
			return;
		int whole = (int)sanctuaryOwed;
		sanctuaryOwed -= whole;
		player.Heal(whole);
	}

	private static float DistanceOutside(Rect2 rect, Vector2 point)
	{
		float dx = MathF.Max(MathF.Max(rect.Position.X - point.X, 0f), point.X - rect.End.X);
		float dy = MathF.Max(MathF.Max(rect.Position.Y - point.Y, 0f), point.Y - rect.End.Y);
		return MathF.Sqrt(dx * dx + dy * dy);
	}

	public override void _Draw()
	{
		Vector2 head = ToLocal(flowerHead);
		if (state.Done)
		{
			// Open: a steady glow round the flower and a bright core. Small - it is a flower, not a spell.
			DrawCircle(head, 26f, new Color(GoldHi, 0.12f));
			DrawCircle(head, 12f, new Color(GoldHi, 0.22f));
			DrawRect(new Rect2(head - new Vector2(2f, 2f), new Vector2(4f, 4f)), GoldHi);
			return;
		}

		// The breathing glint: a 2 x 2 art-pixel core (4 x 4 on screen, the contract's x2) and a
		// one-pixel halo, rising and falling on a slow breath - a flicker would read as fire.
		float breath = 0.5f - 0.5f * MathF.Cos(clock * Mathf.Tau / GlintPeriod);
		DrawRect(new Rect2(head - new Vector2(4f, 4f), new Vector2(8f, 8f)), new Color(GoldHi, 0.18f + 0.22f * breath));
		DrawRect(new Rect2(head - new Vector2(2f, 2f), new Vector2(4f, 4f)), new Color(GoldHi, 0.45f + 0.55f * breath));

		// The lap so far, lit on the fence: one mote per stretch, from the bearing it was started on.
		float progress = MathF.Abs(state.Progress);
		if (progress <= 0.001f)
			return;
		int lit = Math.Min(LapMotes, Mathf.FloorToInt(progress * LapMotes));
		float sign = MathF.Sign(state.Progress);
		for (int k = 0; k < lit; k++)
		{
			float angle = lastAngle - sign * (k + 0.5f) * Mathf.Tau / LapMotes;
			Vector2 point = ToLocal(OnFence(angle));
			DrawRect(new Rect2(point - new Vector2(2f, 2f), new Vector2(4f, 4f)), new Color(GoldHi, 0.85f));
			DrawRect(new Rect2(point - new Vector2(4f, 4f), new Vector2(8f, 8f)), new Color(GoldLit, 0.25f));
		}
	}

	// Where a ray from the flower at this angle meets the fence line.
	private Vector2 OnFence(float angle)
	{
		Vector2 dir = Vector2.FromAngle(angle);
		Vector2 half = Site.Bounds.Size * 0.5f;
		float tx = MathF.Abs(dir.X) > 0.0001f ? half.X / MathF.Abs(dir.X) : float.MaxValue;
		float ty = MathF.Abs(dir.Y) > 0.0001f ? half.Y / MathF.Abs(dir.Y) : float.MaxValue;
		return Site.Bounds.GetCenter() + dir * MathF.Min(tx, ty);
	}
}
