using Godot;
using WizardSurvivors.scripts;

// The Enchanted Forest's side event: hold the grove (.ai/side-events.md).
//
// "Elderbark's wood. Every path now leads inward." A ring of corrupted trees stands somewhere in the
// wood. Stand inside it and the grove slowly grows back; step out and it starts to rot again. The
// wood sends everything it has at the ring while you hold it.
//
// What it asks: STAND STILL. Every run teaches the player to keep moving, and this is the one place
// in the game where moving loses. Leaving the ring costs ground rather than ending the attempt, so
// stepping out to escape a bad moment is a choice with a price, not a failure.
public partial class GroveEvent : SideEvent
{
	[Export] public float HoldSeconds { get; set; } = 20f;
	[Export] public float RingRadius { get; set; } = 110f;
	[Export] public float DrainRate { get; set; } = 0.5f;
	[Export] public float PressureIntervalSeconds { get; set; } = 4f;

	private const int TreeCount = 8;

	private Vector2 grove;
	private float progress;
	private float pressureTimer;
	private bool inside;

	protected override string Announcement => "There is a grove in the wood that has not quite died.";
	protected override string Objective => inside
		? $"Hold the grove  ({Mathf.RoundToInt(progress * 100f)}%)"
		: $"Stand inside the grove  ({Mathf.RoundToInt(progress * 100f)}%)";
	protected override Vector2? PointerTarget => grove;

	protected override void Begin()
	{
		grove = Game.FindEventSite(400f, 650f);
	}

	protected override void Tick(float delta)
	{
		inside = PlayerWithin(grove, RingRadius);
		if (inside)
		{
			progress += delta / HoldSeconds;
			pressureTimer -= delta;
			if (pressureTimer <= 0f)
			{
				pressureTimer = PressureIntervalSeconds;
				SpawnRing(grove, 5, 330f);
			}
		}
		else
		{
			progress = Mathf.Max(0f, progress - delta * DrainRate / HoldSeconds);
		}

		if (progress >= 1f)
			Succeed(grove);
	}

	private static readonly Color Bark = new Color(0.16f, 0.12f, 0.10f);
	private static readonly Color BarkLit = new Color(0.34f, 0.26f, 0.20f);
	private static readonly Color Rot = new Color(0.22f, 0.20f, 0.26f, 0.55f);
	private static readonly Color Growth = new Color(0.46f, 0.80f, 0.40f, 0.9f);

	public override void _Draw()
	{
		if (IsResolved)
			return;

		Vector2 c = ToLocal(grove);
		// The ground inside the ring greens as it grows back.
		DrawCircle(c, RingRadius, new Color(Rot.Lerp(Growth, progress), 0.20f + progress * 0.15f));

		for (int i = 0; i < TreeCount; i++)
		{
			float a = i * Mathf.Tau / TreeCount;
			Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (RingRadius + 10f);
			// A dead trunk with two bare limbs; the limbs leaf out as the grove grows.
			DrawLine(p + new Vector2(0f, 14f), p + new Vector2(0f, -18f), Bark, 5f);
			DrawLine(p + new Vector2(0f, -6f), p + new Vector2(-9f, -16f), BarkLit, 2f);
			DrawLine(p + new Vector2(0f, -10f), p + new Vector2(8f, -20f), BarkLit, 2f);
			if (progress > (i + 1f) / (TreeCount + 1f))
			{
				DrawCircle(p + new Vector2(-9f, -17f), 4f, Growth);
				DrawCircle(p + new Vector2(8f, -21f), 4f, Growth);
			}
		}

		DrawProgressArc(grove, RingRadius, progress, Growth, 4f);
	}
}
