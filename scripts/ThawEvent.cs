using Godot;
using WizardSurvivors.scripts;

// The Frozen Waste's side event: the thaw (.ai/side-events.md).
//
// "A wizard lies under the ice, and the cold stands guard." A figure is sealed in a block of ice.
// The block is NOT a target and never joins "enemies" - the standing rule for anything breakable -
// so no spell can be aimed at it. It thaws from the fighting around it instead: every hit the player
// lands on an enemy within HeatRadius warms it, fire three times over, and the player's own light
// warms it slowly while they stand close. Meanwhile the cold sends more to stand guard.
//
// What it asks: fight HERE. The swarm has to be drawn onto the block and killed on it, which is the
// opposite of the kiting every run teaches. Area spells and fire are the answer, which is exactly
// what a breakable should reward.
public partial class ThawEvent : SideEvent
{
	[Export] public float HeatNeeded { get; set; } = 900f;
	[Export] public float HeatRadius { get; set; } = 150f;
	[Export] public float FireHeatMultiplier { get; set; } = 3f;
	[Export] public float WarmthRadius { get; set; } = 90f;
	[Export] public float WarmthPerSecond { get; set; } = 10f;
	[Export] public float GuardIntervalSeconds { get; set; } = 5f;

	private Vector2 block;
	private float heat;
	private float guardTimer;
	private bool listening;

	protected override string Announcement => "Someone is frozen in the ice nearby.";
	protected override string Objective => $"Thaw the ice: fight beside it  ({Mathf.RoundToInt(100f * heat / HeatNeeded)}%)";
	protected override Vector2? PointerTarget => block;

	protected override void Begin()
	{
		block = Game.FindEventSite(450f, 700f);
		Player.EnemyDamagedAt += OnEnemyDamagedAt;
		listening = true;
	}

	private void OnEnemyDamagedAt(Vector2 where, int amount, SpellData source)
	{
		if (IsResolved || where.DistanceTo(block) > HeatRadius)
			return;
		float multiplier = source?.ElementWeights != null && source.ElementWeights.ContainsKey("Fire") ? FireHeatMultiplier : 1f;
		heat += amount * multiplier;
	}

	protected override void Tick(float delta)
	{
		if (PlayerWithin(block, WarmthRadius))
			heat += WarmthPerSecond * delta;

		// The guard only comes while the player is working on the block - an event left alone
		// should not quietly fill the map.
		if (PlayerWithin(block, 360f))
		{
			guardTimer -= delta;
			if (guardTimer <= 0f)
			{
				guardTimer = GuardIntervalSeconds;
				SpawnRing(block, 4, 280f);
			}
		}

		if (heat >= HeatNeeded)
		{
			StopListening();
			Succeed(block);
		}
	}

	private void StopListening()
	{
		if (!listening)
			return;
		Player.EnemyDamagedAt -= OnEnemyDamagedAt;
		listening = false;
	}

	public override void _ExitTree()
	{
		StopListening();
		base._ExitTree();
	}

	protected override void OnStoodDown() => StopListening();

	private static readonly Color IceDeep = new Color(0.36f, 0.56f, 0.78f, 0.88f);
	private static readonly Color IceLit = new Color(0.78f, 0.92f, 1.0f, 0.95f);
	private static readonly Color Figure = new Color(0.10f, 0.12f, 0.20f, 0.85f);
	private static readonly Color Warm = new Color(1.0f, 0.62f, 0.30f, 0.95f);

	public override void _Draw()
	{
		if (IsResolved)
			return;

		Vector2 c = ToLocal(block);
		float t = Mathf.Clamp(heat / HeatNeeded, 0f, 1f);

		// The reach of the heat, so the player can see where fighting counts.
		DrawArc(c, HeatRadius, 0f, Mathf.Tau, 48, new Color(IceLit, 0.22f), 2f);

		// The block, shrinking a little as it thaws, with a figure sealed in it.
		float half = Mathf.Lerp(34f, 26f, t);
		var rect = new Rect2(c - new Vector2(half, half * 1.3f), new Vector2(half * 2f, half * 2.3f));
		DrawRect(rect, IceDeep);
		DrawCircle(c + new Vector2(0f, -12f), 7f, Figure);
		DrawRect(new Rect2(c + new Vector2(-9f, -4f), new Vector2(18f, 26f)), Figure);
		DrawRect(rect, IceLit, false, 2f);
		DrawLine(rect.Position + new Vector2(4f, 4f), rect.Position + new Vector2(12f, 4f), IceLit, 2f);

		// Cracks spread as it warms: one more per fifth.
		for (int i = 0; i < Mathf.FloorToInt(t * 5f); i++)
		{
			float a = i * 1.3f + 0.4f;
			Vector2 from = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6f;
			Vector2 to = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * half * 1.2f;
			DrawLine(from, to, IceLit, 1.5f);
		}

		DrawProgressArc(block, half * 1.6f + 8f, t, Warm);
	}
}
