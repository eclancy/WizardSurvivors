using Godot;
using System;
using WizardSurvivors.scripts;

// The seed an opened Fenced Bloom drops (.ai/stage-secrets.md 6.1, F1 to F4). Walk over it and he
// carries it, over his head as the Gaoler's key is carried; three hits knock it loose where he stands,
// and it lies there to be picked up again (a mistake resets, never locks - section 8). Planting it in
// the First Tree's bed is the keepsake's whole ask.
//
// THE KEEPSAKE'S FAR TELL, while the decor gradient (section 2) is unbuilt: the seed knows where it
// belongs. Carried, it puts out a shoot that leans toward the First Tree, and now and then lets a mote
// drift off that way. It is not a pointer - no arrow, no edge-of-screen marker, nothing until the
// player has earned the seed at a bloom - it is the thing in his hands wanting to go home. Without it a
// keepsake ten chunks out on a random bearing is a coin flip, which rule 7 forbids.
public partial class SecretSeed : Node2D
{
	public SecretDirector Director { get; set; }

	private const float PickupRadius = 30f;
	private const int HitsToDrop = 3;
	private const float CarryHeight = 66f;
	private const float GlintPeriod = 2.4f;
	private const float MoteEvery = 0.7f;
	private const float MoteSeconds = 1.6f;

	private static readonly Color Husk = new Color(0.55f, 0.47f, 0.30f);
	private static readonly Color HuskLit = new Color(0.80f, 0.71f, 0.45f);
	private static readonly Color Shoot = new Color(0.52f, 0.78f, 0.38f);
	private static readonly Color GoldHi = new Color(1.0f, 0.941f, 0.753f);

	private int hits;
	private bool listening;
	private float clock;

	public bool IsCarried => Director?.Carried == this;

	public override void _Ready()
	{
		ZIndex = -4; // on the ground, under the actors, like the event props
	}

	public override void _Process(double delta)
	{
		clock += (float)delta;
		Player player = Director?.Player;
		if (player == null)
			return;

		if (IsCarried)
		{
			// Follows him, for sorting and so a drop lands where he stands.
			GlobalPosition = player.GlobalPosition;
		}
		else if (Director.Carried == null && Director.Active
			&& player.GlobalPosition.DistanceTo(GlobalPosition) <= PickupRadius)
		{
			Director.Carried = this;
			hits = 0;
			Listen(true);
			ZIndex = 8; // over his hat
			SfxPlayer.Global(SfxCatalog.PickupMagnet);
		}

		if (player.GlobalPosition.DistanceSquaredTo(GlobalPosition) < 1400f * 1400f)
			QueueRedraw();
	}

	/// <summary>Called by the director when the seed goes into the First Tree's bed.</summary>
	public void Planted()
	{
		Listen(false);
		QueueFree();
	}

	// DamageTaken fires after the dodge roll, so a dodged hit does not shake it loose (the key's rule).
	private void OnPlayerDamaged(int amount)
	{
		if (!IsCarried)
			return;
		if (++hits < HitsToDrop)
			return;
		Director.Carried = null;
		Listen(false);
		ZIndex = -4;
	}

	private void Listen(bool on)
	{
		Player player = Director?.Player;
		if (player == null || on == listening)
			return;
		var callable = new Callable(this, nameof(OnPlayerDamaged));
		if (on)
			player.Connect(Player.SignalName.DamageTaken, callable);
		else if (player.IsConnected(Player.SignalName.DamageTaken, callable))
			player.Disconnect(Player.SignalName.DamageTaken, callable);
		listening = on;
	}

	public override void _ExitTree()
	{
		Listen(false);
		if (Director != null && Director.Carried == this)
			Director.Carried = null;
	}

	public override void _Draw()
	{
		Vector2 at = IsCarried ? new Vector2(0f, -CarryHeight) : Vector2.Zero;

		if (IsCarried && Director.Keepsake != null)
		{
			Vector2 toward = Director.Keepsake.GlobalPosition - GlobalPosition;
			if (toward.LengthSquared() > 1f)
			{
				Vector2 dir = toward.Normalized();
				// The shoot: a short green line from the top of the husk, leaning home.
				DrawLine(at + new Vector2(0f, -4f), at + new Vector2(0f, -4f) + dir * 12f, Shoot, 2f);
				DrawRect(new Rect2(at + new Vector2(0f, -4f) + dir * 12f - new Vector2(2f, 2f), new Vector2(4f, 4f)), Shoot);
				// A mote drifting off that way now and then, fading as it goes.
				float phase = clock % MoteEvery;
				for (float age = phase; age < MoteSeconds; age += MoteEvery)
				{
					float t = age / MoteSeconds;
					Vector2 mote = at + dir * (16f + 70f * t) + new Vector2(0f, -10f * t);
					DrawRect(new Rect2(mote - new Vector2(1.5f, 1.5f), new Vector2(3f, 3f)), new Color(GoldHi, 0.8f * (1f - t)));
				}
			}
		}

		// The husk: a teardrop of pale seed, the size of the key.
		DrawCircle(at + new Vector2(0f, 2f), 5f, Husk);
		DrawCircle(at + new Vector2(0f, -1f), 3.5f, Husk);
		DrawRect(new Rect2(at + new Vector2(-1f, -3f), new Vector2(2f, 4f)), HuskLit);

		if (!IsCarried)
		{
			// On the ground it breathes, like every secret's near tell.
			float breath = 0.5f - 0.5f * MathF.Cos(clock * Mathf.Tau / GlintPeriod);
			DrawRect(new Rect2(at + new Vector2(-4f, -10f), new Vector2(8f, 8f)), new Color(GoldHi, 0.15f + 0.2f * breath));
			DrawRect(new Rect2(at + new Vector2(-2f, -8f), new Vector2(4f, 4f)), new Color(GoldHi, 0.4f + 0.6f * breath));
		}
	}
}
