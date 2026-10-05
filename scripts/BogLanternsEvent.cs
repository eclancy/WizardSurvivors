using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// The Blighted Swamp's side event: snuff the lanterns (.ai/side-events.md).
//
// "The lanterns still burn. Nothing living lights them." Four bog lanterns burn somewhere on the
// map. Stand at one to snuff it, and what it was keeping back comes out of the fog. With all four
// dark, the thing that tends them comes looking for whoever put them out.
//
// What it asks: EXPLORE. It is the one event spread across the map rather than held at one spot,
// and the order the player takes the lanterns in is theirs to choose.
public partial class BogLanternsEvent : SideEvent
{
	[Export] public int LanternCount { get; set; } = 4;
	[Export] public float SnuffSeconds { get; set; } = 1.2f;
	[Export] public int WispsPerLantern { get; set; } = 6;
	[Export] public float TenderHealthMultiplier { get; set; } = 22f;

	private const float SnuffRadius = 36f;

	private readonly List<Vector2> lanterns = new();
	private readonly List<bool> lit = new();
	private readonly List<float> snuff = new();
	private Enemy tender;
	private Vector2 tenderLastSeen;
	private bool tenderCalled;
	private float flicker;

	protected override string Announcement => "Bog lanterns are burning in the fog.";

	protected override string Objective => tenderCalled
		? "The lantern-tender has come. Kill it"
		: $"Snuff the bog lanterns  ({LanternCount - LitCount()} of {LanternCount})";

	protected override Vector2? PointerTarget => tenderCalled ? tenderLastSeen : NearestLit();

	protected override void Begin()
	{
		for (int i = 0; i < LanternCount; i++)
		{
			lanterns.Add(Game.FindEventSite(380f, 950f, lanterns, 320f));
			lit.Add(true);
			snuff.Add(0f);
		}
	}

	private int LitCount()
	{
		int n = 0;
		foreach (bool b in lit) if (b) n++;
		return n;
	}

	private Vector2? NearestLit()
	{
		Vector2? best = null;
		float bestDistance = float.MaxValue;
		for (int i = 0; i < lanterns.Count; i++)
		{
			if (!lit[i]) continue;
			float d = Player.GlobalPosition.DistanceTo(lanterns[i]);
			if (d < bestDistance) { bestDistance = d; best = lanterns[i]; }
		}
		return best;
	}

	protected override void Tick(float delta)
	{
		flicker += delta;

		if (tenderCalled)
		{
			if (IsGone(tender))
			{
				Succeed(tenderLastSeen);
				return;
			}
			tenderLastSeen = tender.GlobalPosition;
			return;
		}

		for (int i = 0; i < lanterns.Count; i++)
		{
			if (!lit[i])
				continue;
			if (PlayerWithin(lanterns[i], SnuffRadius))
				snuff[i] += delta / SnuffSeconds;
			else
				snuff[i] = Mathf.Max(0f, snuff[i] - delta * 0.5f);

			if (snuff[i] < 1f)
				continue;

			lit[i] = false;
			SpawnRing(lanterns[i], WispsPerLantern, 140f);
		}

		if (LitCount() == 0)
		{
			tenderCalled = true;
			Vector2 site = Game.FindEventSite(380f, 520f);
			tender = Game.SpawnEventEnemy(null, site, TenderHealthMultiplier, true);
			tenderLastSeen = tender != null ? tender.GlobalPosition : site;
		}
	}

	private static readonly Color Post = new Color(0.16f, 0.14f, 0.12f);
	private static readonly Color Flame = new Color(0.62f, 0.95f, 0.55f, 0.95f);
	private static readonly Color Halo = new Color(0.45f, 0.85f, 0.40f, 0.18f);
	private static readonly Color Wick = new Color(0.08f, 0.08f, 0.08f);

	public override void _Draw()
	{
		if (IsResolved)
			return;

		for (int i = 0; i < lanterns.Count; i++)
		{
			Vector2 p = ToLocal(lanterns[i]);
			DrawLine(p + new Vector2(0f, 18f), p + new Vector2(0f, -8f), Post, 4f);
			DrawRect(new Rect2(p + new Vector2(-6f, -20f), new Vector2(12f, 13f)), Post, false, 2f);
			if (lit[i])
			{
				float size = 4f + Mathf.Sin(flicker * 9f + i) * 0.8f;
				DrawCircle(p + new Vector2(0f, -14f), 26f, Halo);
				DrawCircle(p + new Vector2(0f, -14f), size, Flame);
				DrawProgressArc(lanterns[i], SnuffRadius, snuff[i], new Color(0.85f, 0.85f, 0.95f));
			}
			else
			{
				DrawCircle(p + new Vector2(0f, -14f), 2f, Wick);
			}
		}
	}
}
