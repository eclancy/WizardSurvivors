using Godot;
using WizardSurvivors.scripts;

// The Cursed Dungeon's side event: the Gaoler's key (.ai/side-events.md).
//
// "He keeps a wizard in one of these cells." A Warden - the dark wizard's jailer, already the run's
// recurring miniboss - carries the key. Kill it, pick up what it drops, and carry it to the one cell
// on the map that is locked. Three hits while carrying and the key is knocked out of your hands
// where you stand; go back for it.
//
// What it asks that nothing else does: a CARRY. Every other objective in the game is a place or a
// target. This one makes the route home matter, and makes being hit cost something other than HP.
public partial class GaolersKeyEvent : SideEvent
{
	[Export] public int HitsToDrop { get; set; } = 3;
	[Export] public float UnlockSeconds { get; set; } = 1.2f;

	private const float PickupRadius = 30f;
	private const float CellRadius = 46f;

	private enum Step { HuntWarden, KeyOnGround, Carrying }

	private Step step = Step.HuntWarden;
	private Enemy warden;
	private Vector2 wardenLastSeen;
	private Vector2 keyPosition;
	private Vector2 cellPosition;
	private int hitsWhileCarrying;
	private float unlockProgress;
	private bool listening;

	protected override string Announcement => "A Warden is carrying a cell key.";

	protected override string Objective => step switch
	{
		Step.HuntWarden => "Kill the Warden carrying the key",
		Step.KeyOnGround => "Pick up the key",
		_ => $"Carry the key to the locked cell  ({HitsToDrop - hitsWhileCarrying} hits from dropping it)",
	};

	protected override Vector2? PointerTarget => step switch
	{
		Step.HuntWarden => wardenLastSeen,
		Step.KeyOnGround => keyPosition,
		_ => cellPosition,
	};

	protected override void Begin()
	{
		Vector2 wardenSite = Game.FindEventSite(520f, 760f);
		// The cell sits on the far side of the player from the Warden, so the carry crosses the map
		// rather than being a step back to where the fight was.
		Vector2 away = Player.GlobalPosition + (Player.GlobalPosition - wardenSite).Normalized() * 900f;
		cellPosition = Game.ReachablePoint(away);
		if (cellPosition.DistanceTo(Player.GlobalPosition) < 500f)
			cellPosition = Game.FindEventSite(800f, 1100f, new[] { wardenSite }, 900f);

		warden = Game.SpawnEventEnemy("res://scenes/WardenEnemy.tscn", wardenSite, 9f, true);
		wardenLastSeen = warden != null ? warden.GlobalPosition : wardenSite;
	}

	protected override void Tick(float delta)
	{
		switch (step)
		{
			case Step.HuntWarden:
				if (!IsGone(warden))
				{
					wardenLastSeen = warden.GlobalPosition;
					break;
				}
				keyPosition = Game.ReachablePoint(wardenLastSeen);
				step = Step.KeyOnGround;
				break;

			case Step.KeyOnGround:
				if (PlayerWithin(keyPosition, PickupRadius))
				{
					step = Step.Carrying;
					hitsWhileCarrying = 0;
					Listen(true);
				}
				break;

			case Step.Carrying:
				if (PlayerWithin(cellPosition, CellRadius))
				{
					unlockProgress += delta / UnlockSeconds;
					if (unlockProgress >= 1f)
					{
						Listen(false);
						Succeed(cellPosition);
					}
				}
				else
				{
					unlockProgress = Mathf.Max(0f, unlockProgress - delta);
				}
				break;
		}
	}

	// DamageTaken fires after the dodge roll, so a dodged hit does not shake the key loose.
	private void OnPlayerDamaged(int amount)
	{
		if (step != Step.Carrying || IsResolved)
			return;
		hitsWhileCarrying++;
		if (hitsWhileCarrying < HitsToDrop)
			return;

		keyPosition = Player.GlobalPosition;
		step = Step.KeyOnGround;
		unlockProgress = 0f;
		Listen(false);
	}

	private void Listen(bool on)
	{
		if (Player == null || on == listening)
			return;
		var callable = new Callable(this, nameof(OnPlayerDamaged));
		if (on)
			Player.Connect(Player.SignalName.DamageTaken, callable);
		else if (Player.IsConnected(Player.SignalName.DamageTaken, callable))
			Player.Disconnect(Player.SignalName.DamageTaken, callable);
		listening = on;
	}

	public override void _ExitTree()
	{
		Listen(false);
		base._ExitTree();
	}

	protected override void OnStoodDown() => Listen(false);

	private static readonly Color Iron = new Color(0.34f, 0.36f, 0.42f);
	private static readonly Color IronLit = new Color(0.62f, 0.64f, 0.70f);
	private static readonly Color Brass = new Color(0.95f, 0.78f, 0.36f);
	private static readonly Color Gloom = new Color(0.03f, 0.03f, 0.05f, 0.92f);

	public override void _Draw()
	{
		if (IsResolved)
			return;

		// The cell: a dark recess behind bars, with the shape of someone sitting inside.
		Vector2 c = ToLocal(cellPosition);
		DrawRect(new Rect2(c - new Vector2(30f, 34f), new Vector2(60f, 60f)), Gloom);
		DrawCircle(c + new Vector2(0f, -6f), 6f, new Color(0.16f, 0.15f, 0.20f));
		DrawRect(new Rect2(c + new Vector2(-8f, 0f), new Vector2(16f, 20f)), new Color(0.16f, 0.15f, 0.20f));
		for (int i = 0; i < 6; i++)
		{
			float x = -27f + i * 10.8f;
			DrawLine(c + new Vector2(x, -34f), c + new Vector2(x, 26f), Iron, 3f);
			DrawLine(c + new Vector2(x - 1f, -34f), c + new Vector2(x - 1f, 26f), IronLit, 1f);
		}
		DrawRect(new Rect2(c - new Vector2(32f, 36f), new Vector2(64f, 64f)), Iron, false, 3f);
		if (step == Step.Carrying)
			DrawProgressArc(cellPosition, CellRadius, unlockProgress, Brass);

		if (step == Step.KeyOnGround)
			DrawKey(ToLocal(keyPosition));
		else if (step == Step.Carrying && Player != null)
			DrawKey(ToLocal(Player.GlobalPosition) + new Vector2(0f, -40f));
	}

	private void DrawKey(Vector2 at)
	{
		DrawCircle(at + new Vector2(-6f, 0f), 5f, Brass);
		DrawCircle(at + new Vector2(-6f, 0f), 2.2f, Gloom);
		DrawLine(at + new Vector2(-1f, 0f), at + new Vector2(10f, 0f), Brass, 3f);
		DrawLine(at + new Vector2(7f, 0f), at + new Vector2(7f, 4f), Brass, 2f);
		DrawLine(at + new Vector2(10f, 0f), at + new Vector2(10f, 5f), Brass, 2f);
	}
}
