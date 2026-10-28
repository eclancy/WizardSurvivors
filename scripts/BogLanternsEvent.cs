using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// The Blighted Swamp's side event: snuff the lanterns (.ai/side-events.md).
//
// "The lanterns still burn. Nothing living lights them." Four bog lanterns burn in a mire somewhere
// out on the map. Stand in one's light to snuff it, and what it was keeping back comes out of the
// fog. With all four dark, the thing that tends them comes looking for whoever put them out.
//
// What it asks: EXPLORE. It is the one event spread over ground rather than held at one spot, and
// the order the player takes the lanterns in is theirs to choose.
//
// THE MIRE IS OUT THERE FROM THE START (2026-10-22). The lanterns are placed at stage start from the
// run seed (SideEvent), as a loose chain: the first four to six chunks out, each of the others one
// step of 760-1100 px from the one before, so from any lantern the next one's light is at, or just
// past, the edge of the screen. The event begins at the first lantern the player reaches.
//
// AND THE LANTERNS ARE THE THING YOU SEE (same day). Reported: "the area around the lanterns in the
// bog that show the boundaries are too opaque, you can't see the lanterns. make the lanterns more
// prominent, and glowing brightly, the area is bigger and more subtle". The lantern was a 4 px line
// and a 12 px box drawn OVER the actors, with a solid black ring at 45% round it: the ring was the
// most visible thing there, and the wizard standing on the lantern hid the rest. Now:
//   - the lantern is a generated sprite (tools/art/explore_props.py) on a crook a little taller than
//     the wizard, sorted with the actors at its foot;
//   - its flame is light, drawn additively above the actors - a bright core, a halo, a pool on the
//     ground and a column above it - in the cold poison ramp, FLICKERING (his light flickers; found
//     things breathe). It gutters as it is snuffed;
//   - the area is its light's pool, 120 px (was 36), marked by a faint dashed ring on the ground,
//     under everything. The progress fills along that ring.
// Because standing in a 240 px pool is far easier than on a 72 px spot, snuffing takes 3 s (was
// 1.2), and progress drains over 4 s when the player steps out.
//
// THE TENDER IS ITS OWN CREATURE (2026-10-28). It was SpawnEventEnemy(null, ...): a roll on the
// swamp's table at elite scale, so it had no shape of its own and nothing to do but walk at the
// player. Eric: "lantern tender should have a glowing lantern and be bigger so it's much easier to
// see him. he should fire animated fire strikes and have a few different special abilities". It is
// now LanternTenderEnemy (scenes/SwampLanternTender.tscn): it carries one of these lanterns, throws
// their fire, and - once, at half health - REKINDLES one of the lanterns the player put out and is
// warded while it burns. That is the one thing this event does for it (ILanternKeeper): the relit
// lantern is snuffed the way every lantern is, by standing in its light, and the objective and its
// arrow say so. Its health still comes from the run's spawn health times TenderHealthMultiplier, so
// it is as hard as the run is wherever the player finds the mire; the win and the chest are unchanged.
public partial class BogLanternsEvent : SideEvent, ILanternKeeper
{
	/// <summary>The lantern-tender. The event's own creature, not a roll on the stage table.</summary>
	public const string TenderScenePath = "res://scenes/SwampLanternTender.tscn";

	[Export] public int LanternCount { get; set; } = 4;
	[Export] public float SnuffSeconds { get; set; } = 3f;
	[Export] public float PoolRadius { get; set; } = 120f;
	[Export] public int WispsPerLantern { get; set; } = 6;
	[Export] public float TenderHealthMultiplier { get; set; } = 22f;

	// How snuffing progress is lost outside the pool: all of it in four seconds.
	private const float SnuffDrainPerSecond = 0.25f;
	// The chain: each lantern a step from the last, never nearer than MireSpacing to another, and
	// all within MireReach of the first, so the mire is one place and not four errands.
	private const float MireStepMin = 760f;
	private const float MireStepMax = 1100f;
	private const float MireSpacing = 650f;
	private const float MireReach = 2200f;
	// After one goes out the others flare for a moment - "the others answer" - which lifts their
	// columns over a wider stretch of screen edge while the player decides where to go next.
	private const float FlareSeconds = 2.6f;

	private readonly List<bool> lit = new();
	private readonly List<float> snuff = new();
	private readonly List<float> snuffedAt = new();
	private readonly List<Sprite2D> sprites = new();
	private ExploreSprite art;
	private Enemy tender;
	private Vector2 tenderLastSeen;
	private bool tenderCalled;
	private float flareUntil = -1f;
	private int inside = -1;

	protected override string Announcement => "Bog lanterns are burning in the fog.";
	protected override string ChestLine => "The lanterns are dark - the tender's chest is yours";

	protected override string Objective => tenderCalled
		? RekindledLantern >= 0
			? inside >= 0
				? "Stand in the light to snuff it - it wards the tender"
				: "The tender relit a lantern - snuff it to break its ward"
			: "The lantern-tender has come. Kill it"
		: inside >= 0
			? $"Stand in the light to snuff it  ({LanternCount - LitCount()} of {LanternCount})"
			: $"Snuff the bog lanterns  ({LanternCount - LitCount()} of {LanternCount})";

	// No objective arrow to the lanterns themselves: finding them IS the event. A lantern the player
	// has seen, or that the wayfinder revealed, gets a point-of-interest arrow (PoiPointer) instead.
	// Once the tender has relit one, the arrow goes to that lantern: putting it out is the thing to do.
	protected override Vector2? PointerTarget => !tenderCalled
		? null
		: RekindledLantern >= 0 ? Sites[RekindledLantern].Position : tenderLastSeen;

	protected override string SiteGlyph => "lantern";
	protected override float SiteFootprint => PoolRadius + 40f;
	// Within reach of a lantern's light, so the first one found is on screen when the line comes up.
	protected override float ArrivalRadius => 480f;
	protected override Color TellColor => FlameMid;

	public override void PlaceSites(ExplorePlacer placer)
	{
		Vector2 first = AddSite(placer.EventSite(SiteFootprint)).Position;
		var placed = new List<Vector2> { first };
		Vector2 previous = first;
		for (int i = 1; i < LanternCount; i++)
		{
			Vector2 next = placer.Near(previous, MireStepMin, MireStepMax, SiteFootprint, placed, MireSpacing, first, MireReach);
			AddSite(next);
			placed.Add(next);
			previous = next;
		}
	}

	protected override void OnPlaced()
	{
		art = ExploreArt.Get("bog-lantern");
		for (int i = 0; i < Sites.Count; i++)
		{
			lit.Add(true);
			snuff.Add(0f);
			snuffedAt.Add(-1f);
			if (art?.Texture == null)
			{
				sprites.Add(null);
				continue;
			}
			// Standing in the world, sorted with the actors at its foot (ZIndex 5 under this node's
			// -5 is the actors' 0).
			Sprite2D sprite = ExploreArt.MakeGrounded(art, ToLocal(Sites[i].Position));
			sprite.ZIndex = 5;
			sprite.Name = $"Lantern{i}";
			AddChild(sprite);
			sprites.Add(sprite);
		}
		if (art?.Texture == null)
			GD.PushError("BogLanternsEvent: bog-lantern.png or explore.json is missing; the lanterns will be light without a post. "
				+ "Re-run: python tools/art/explore_props.py, then import.");
	}

	// Lanterns are placed and drawn from the start; arriving only starts the counting.
	protected override void Begin() { }

	private int LitCount()
	{
		int n = 0;
		foreach (bool b in lit) if (b) n++;
		return n;
	}

	protected override void DormantTick(float delta) => AnimateSprites();

	protected override void Tick(float delta)
	{
		AnimateSprites();

		if (tenderCalled)
		{
			if (IsGone(tender))
			{
				Succeed(tenderLastSeen);
				return;
			}
			tenderLastSeen = tender.GlobalPosition;
			// A lantern the tender relit is put out the way they all were.
			TickSnuffing(delta);
			return;
		}

		TickSnuffing(delta);

		if (LitCount() == 0)
		{
			tenderCalled = true;
			Vector2 site = Game.FindEventSite(380f, 520f);
			tender = Game.SpawnEventEnemy(TenderScenePath, site, TenderHealthMultiplier, true);
			if (tender is LanternTenderEnemy keeper)
				keeper.Lanterns = this;
			else
			{
				// RegressionChecks.ValidateLanternTender says this at startup too. Loud, and the event
				// still has a body to kill: a missing tender would otherwise read as IsGone and win it.
				GD.PushError($"BogLanternsEvent: '{TenderScenePath}' did not give a LanternTenderEnemy; the tender is a roll on the stage table instead.");
				tender?.QueueFree();
				tender = Game.SpawnEventEnemy(null, site, TenderHealthMultiplier, true);
			}
			tenderLastSeen = tender != null ? tender.GlobalPosition : site;
		}
	}

	// Standing in a lit lantern's pool snuffs it. Before the tender, that is every lantern; after, only
	// one it relit can be lit, and putting that out frees nothing from the fog - the tender's ward was
	// what it was keeping.
	private void TickSnuffing(float delta)
	{
		inside = -1;
		for (int i = 0; i < Sites.Count; i++)
		{
			if (!lit[i])
				continue;
			if (PlayerWithin(Sites[i].Position, PoolRadius))
			{
				inside = i;
				snuff[i] += delta / SnuffSeconds;
			}
			else
			{
				snuff[i] = Mathf.Max(0f, snuff[i] - delta * SnuffDrainPerSecond);
			}

			if (snuff[i] < 1f)
				continue;

			lit[i] = false;
			snuffedAt[i] = Clock;
			Sites[i].Done = true;
			inside = -1;
			if (tenderCalled)
				continue;
			flareUntil = Clock + FlareSeconds;
			SpawnRing(Sites[i].Position, WispsPerLantern, PoolRadius + 140f);
		}
	}

	// --- ILanternKeeper: what the tender may ask of the lanterns ----------------------------------

	/// <summary>The lantern the tender relit, while it still burns; -1 otherwise.</summary>
	private int RekindledLantern
	{
		get
		{
			if (!tenderCalled)
				return -1;
			for (int i = 0; i < lit.Count; i++)
				if (lit[i])
					return i;
			return -1;
		}
	}

	public int NearestSnuffedLantern(Vector2 from, float reach)
	{
		int best = -1;
		float bestDistance = reach;
		for (int i = 0; i < Sites.Count; i++)
		{
			if (lit[i])
				continue;
			float d = Sites[i].Position.DistanceTo(from);
			if (d <= bestDistance)
			{
				best = i;
				bestDistance = d;
			}
		}
		return best;
	}

	public Vector2 LanternFlame(int index)
	{
		Vector2 foot = Sites[index].Position;
		return art != null ? foot + ExploreArt.FlameOffset(art) : foot + new Vector2(14f, -70f);
	}

	public void Rekindle(int index)
	{
		if (index < 0 || index >= lit.Count || lit[index] || IsResolved)
			return;
		lit[index] = true;
		snuff[index] = 0f;
		snuffedAt[index] = -1f;
		// It catches as the others did when one went out: its column stands up over the screen edge.
		flareUntil = Clock + FlareSeconds;
	}

	public bool IsLanternLit(int index) => index >= 0 && index < lit.Count && lit[index] && !IsResolved;

	/// <summary>
	/// For _UiShot --tender: every lantern snuffed on the next Tick, through the real path - the wisps,
	/// the tender's site and spawn, its hookup. Over 1 so the drain outside a pool cannot undo it.
	/// </summary>
	public void DebugSnuffAll()
	{
		for (int i = 0; i < snuff.Count; i++)
			snuff[i] = 2f;
	}

	// The three lit frames change the flame's shape; which one shows follows the same irregular
	// clock as the light, so the drawn flame and its glow gutter together.
	private void AnimateSprites()
	{
		for (int i = 0; i < sprites.Count; i++)
		{
			Sprite2D sprite = sprites[i];
			if (sprite == null)
				continue;
			if (!lit[i])
			{
				sprite.Frame = Mathf.Clamp(art.Frames - 1, 0, art.Frames - 1);
				continue;
			}
			float f = Flicker(i);
			int lits = Mathf.Max(1, art.LitFrames);
			sprite.Frame = f > 1.0f ? 1 % lits : f < 0.86f ? 2 % lits : 0;
		}
	}

	// The poison ramp (.ai/art-direction.md section 3): nothing living lights them.
	private static readonly Color FlameCore = new Color(0.973f, 1f, 0.847f);   // #F8FFD8
	private static readonly Color FlameHot = new Color(0.863f, 0.957f, 0.549f);  // #DCF48C
	private static readonly Color FlameMid = new Color(0.706f, 0.878f, 0.290f);  // #B4E04A

	// How long a snuffed lantern's light takes to die: it goes out, it does not blink off.
	private const float DieSeconds = 0.6f;

	protected override void DrawLight(CanvasItem light)
	{
		bool flaring = Clock < flareUntil;
		for (int i = 0; i < Sites.Count; i++)
		{
			Vector2 foot = Sites[i].Position;
			if (!NearPlayer(foot))
				continue;

			float strength;
			if (lit[i])
				// Guttering as it is snuffed: down to a third.
				strength = Flicker(i) * (1f - 0.65f * snuff[i]);
			else if (snuffedAt[i] >= 0f && Clock - snuffedAt[i] < DieSeconds)
				strength = 1f - (Clock - snuffedAt[i]) / DieSeconds;
			else
				continue;

			Vector2 ground = ToLocal(foot);
			Vector2 flame = art != null ? ground + ExploreArt.FlameOffset(art) : ground + new Vector2(14f, -70f);
			float flare = lit[i] && flaring ? 1f + 0.8f * (flareUntil - Clock) / FlareSeconds : 1f;

			// The far tell: a wide faint glow and a column standing up out of the flame. Brighter
			// before anyone has come, and when the others have just gone out.
			float far = (IsDormant ? 1f : 0.7f) * flare;
			ExploreArt.DrawGlow(light, flame, 480f, new Color(FlameMid, 0.075f * strength * far));
			ExploreArt.DrawShaft(light, flame, 20f, 520f * Mathf.Min(1.4f, flare), new Color(FlameMid, 0.26f * strength * far));
			ExploreArt.DrawShaft(light, flame, 6f, 470f * Mathf.Min(1.4f, flare), new Color(FlameHot, 0.32f * strength * far));

			// Its light on the ground: the pool you stand in to snuff it.
			ExploreArt.DrawGlow(light, ground, PoolRadius * 1.25f, new Color(FlameMid, 0.15f * strength));

			// The flame itself: halo, glow, core. Bright, because the brief was "glowing brightly".
			ExploreArt.DrawGlow(light, flame, 150f, new Color(FlameMid, 0.30f * strength));
			ExploreArt.DrawGlow(light, flame, 60f, new Color(FlameHot, 0.62f * strength));
			ExploreArt.DrawGlow(light, flame, 22f, new Color(FlameCore, 0.95f * strength));
		}
	}

	public override void _Draw()
	{
		if (IsResolved)
			return;

		// The pool's edge, faint and dashed, on the ground under everything. It brightens while the
		// player stands in it, and the snuffing fills along it.
		for (int i = 0; i < Sites.Count; i++)
		{
			if (!lit[i] || !NearPlayer(Sites[i].Position))
				continue;
			bool standing = i == inside;
			DrawProgressArc(Sites[i].Position, PoolRadius, snuff[i], standing ? FlameCore : FlameHot, 3f, standing ? 0.42f : 0.2f);
		}
	}
}
