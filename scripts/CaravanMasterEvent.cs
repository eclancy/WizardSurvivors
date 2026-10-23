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
//
// The road and his camp are out on the sands from the first second (SideEvent: placed at stage
// start), and he sets off along it when the player comes near. Until then the road lies there,
// faint, with the place he escapes marked at its end.
public partial class CaravanMasterEvent : SideEvent
{
	[Export] public float RoadLength { get; set; } = 2000f;
	[Export] public float WalkSpeed { get; set; } = 52f;
	[Export] public float HealthMultiplier { get; set; } = 26f;

	private Enemy master;
	private Vector2 roadStart;
	private Vector2 roadEnd;
	private Vector2 lastSeen;

	protected override string Announcement => "A caravan master walks the trade road, carrying something.";
	protected override string ChestLine => "The caravan master falls - his chest is yours";
	protected override string Objective =>
		$"Stop the caravan master before it leaves  ({Mathf.RoundToInt(100f * RemainingFraction())}% of the road left)";
	protected override Vector2? PointerTarget => lastSeen;

	protected override string SiteGlyph => "caravan";
	protected override float SiteFootprint => 140f;
	// He sets off when the player is near enough to give chase, not before.
	protected override float ArrivalRadius => 560f;
	protected override Color TellColor => new Color(0.72f, 0.78f, 0.86f);  // steel's pale end, cold on the sand

	// Where the road samples are taken, and how wide a strip along it is kept free of decor.
	private const float RoadSampleStep = 120f;
	private const float RoadClearance = 48f;

	/// <summary>
	/// The camp (where he waits) is the site; the road runs from it on a seeded bearing, the first
	/// of twelve whose whole length is open ground, so he is never sent across a lake. The road is
	/// kept free of decor (Reserve) as well, so nothing the furnishing put down stops him.
	/// </summary>
	public override void PlaceSites(ExplorePlacer placer)
	{
		roadStart = AddSite(placer.EventSite(SiteFootprint)).Position;
		float first = placer.NextFloat() * Mathf.Tau;
		Vector2 chosen = Vector2.Zero;
		for (int k = 0; k < 12 && chosen == Vector2.Zero; k++)
		{
			Vector2 along = new Vector2(Mathf.Cos(first + k * Mathf.Tau / 12f), Mathf.Sin(first + k * Mathf.Tau / 12f));
			bool open = true;
			for (float d = RoadSampleStep; d <= RoadLength && open; d += RoadSampleStep)
				open = placer.IsClear(roadStart + along * d, RoadClearance);
			if (open || k == 11)
				chosen = along;
		}
		roadEnd = roadStart + chosen * RoadLength;
		for (float d = RoadSampleStep; d <= RoadLength; d += RoadSampleStep)
			placer.Reserve(roadStart + chosen * d, RoadClearance + 24f);
		lastSeen = roadStart;
	}

	protected override void Begin()
	{
		lastSeen = roadStart;
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
		if (IsResolved || !NearPlayer(roadStart) && !NearPlayer(roadEnd) && !NearPlayer(lastSeen))
			return;

		// The road, dashed, from where he is now (or his camp) to where he will be lost.
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
