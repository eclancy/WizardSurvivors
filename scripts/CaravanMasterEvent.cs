using Godot;
using WizardSurvivors.scripts;

// The Scorched Sands' side event: the caravan master (.ai/side-events.md).
//
// "He hid the water. The dead still walk the trade road." One of the dead walks a straight road
// across the map, carrying a spell it does not know it has. It never turns toward the player - it
// is walking its route, as it did when it was alive - and if it reaches the far end it is gone.
//
// What it asks: CATCH something. Every other target in the game comes to the player. This one is
// leaving, so the player has to cut across the swarm to reach it and stay on it, and a build that
// only kills what walks into it cannot finish the job.
public partial class CaravanMasterEvent : SideEvent
{
	[Export] public float RoadLength { get; set; } = 2000f;
	// How far to the side of the player the road passes, so it crosses near them without starting
	// on top of them.
	[Export] public float RoadOffset { get; set; } = 260f;
	[Export] public float WalkSpeed { get; set; } = 52f;
	[Export] public float HealthMultiplier { get; set; } = 26f;

	private Enemy master;
	private Vector2 roadStart;
	private Vector2 roadEnd;
	private Vector2 lastSeen;

	protected override string Announcement => "A caravan master walks the trade road, carrying something.";
	protected override string Objective =>
		$"Stop the caravan master before it leaves  ({Mathf.RoundToInt(100f * RemainingFraction())}% of the road left)";
	protected override Vector2? PointerTarget => lastSeen;

	protected override void Begin()
	{
		float angle = (float)GD.RandRange(0.0, Mathf.Tau);
		Vector2 along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
		Vector2 side = new Vector2(-along.Y, along.X);
		Vector2 mid = Player.GlobalPosition + side * RoadOffset;
		roadStart = Game.ClampToStage(mid - along * RoadLength * 0.5f);
		roadEnd = Game.ClampToStage(mid + along * RoadLength * 0.5f);

		master = Game.SpawnEventEnemy("res://scenes/TankEnemy.tscn", roadStart, HealthMultiplier, true);
		if (master != null)
		{
			master.Speed = WalkSpeed;
			// A walker, not a fighter: knocked about by spells but never pushed off its route for long.
			master.KnockbackResistance = Mathf.Max(master.KnockbackResistance, 0.6f);
		}
		lastSeen = roadStart;
	}

	private float RemainingFraction()
	{
		float total = roadStart.DistanceTo(roadEnd);
		return total <= 0f ? 0f : Mathf.Clamp(lastSeen.DistanceTo(roadEnd) / total, 0f, 1f);
	}

	protected override void Tick(float delta)
	{
		if (IsGone(master))
		{
			Succeed(lastSeen);
			return;
		}

		lastSeen = master.GlobalPosition;
		// Re-aimed every frame at the end of the road, so a shove from a spell is recovered from
		// rather than walked off in a new direction.
		master.ScriptedHeading = (roadEnd - lastSeen).Normalized();

		if (lastSeen.DistanceTo(roadEnd) <= 40f)
		{
			master.QueueFree();
			Fail("The caravan master walked out of reach.");
		}
	}

	protected override void OnStoodDown()
	{
		if (!IsGone(master))
			master.ScriptedHeading = Vector2.Zero;
	}

	private static readonly Color Road = new Color(0.82f, 0.68f, 0.46f, 0.35f);
	private static readonly Color End = new Color(0.95f, 0.78f, 0.36f, 0.8f);

	public override void _Draw()
	{
		if (IsResolved)
			return;

		// The road, dashed, from where it is now to where it will be lost.
		Vector2 from = ToLocal(lastSeen);
		Vector2 to = ToLocal(roadEnd);
		float length = from.DistanceTo(to);
		Vector2 step = length > 0f ? (to - from) / length : Vector2.Zero;
		for (float d = 0f; d < length; d += 28f)
			DrawLine(from + step * d, from + step * Mathf.Min(d + 14f, length), Road, 4f);

		// Where it escapes.
		DrawArc(to, 26f, 0f, Mathf.Tau, 24, End, 3f);
		DrawLine(to + new Vector2(-10f, -10f), to + new Vector2(10f, 10f), End, 3f);
		DrawLine(to + new Vector2(-10f, 10f), to + new Vector2(10f, -10f), End, 3f);
	}
}
