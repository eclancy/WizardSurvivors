using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// The Fallow Fields' side event: defend the well (.ai/campaign.md section 5, .ai/side-events.md).
//
// "He takes a tithe of everything that grows here." The well is the last water he has not fouled,
// and the coins the village threw into it are still down there. When the player arrives, the field
// turns on it: waves of the chapter's own creatures walk at the well - not at the player - and each
// one that reaches it fouls it a step. Hold it clean for HoldSeconds and the coin is drawn up (the
// Wishing Coin, granted on the spot by DiscoveryRewards). Fouled through, it fails, and the next run
// may try again.
//
// WHAT IT ASKS: DEFEND. Nothing else in the game does. The grove asks the player to stand still,
// the lanterns to explore, the caravan master to catch; this asks him to put himself between a
// place and what is coming at it, from a new side every wave, while the ordinary swarm keeps coming
// for him. His spells aim themselves at whatever is nearest HIM, so standing on the well is not
// enough - he has to go out to meet a wave before it arrives.
//
// THE WALKERS ARE NOT ELITES, which .ai/side-events.md's rule asks of event enemies, on purpose:
// the rule exists so the recycler never moves a key-carrier or a caravan master to the far side of
// the map, and an elite pays a 50-point orb. These are pressure, a wave every few seconds - thirty
// elites would be a level and a half of XP for one detour. They are the grove's and the lanterns'
// kind of event enemy (SpawnRing's): the chapter's table at a little more health, walking on
// Enemy.ScriptedHeading, re-aimed at the well every frame so a shove or a recycle is recovered
// from. A faint dashed tether runs from each to the well, so which of the horde is going for it is
// never a guess.
//
// Every SideEvent rule holds: placed by seed, dormant until the player arrives, timed in real
// seconds once running, nothing it places in "enemies" (the well is a sprite and a footprint, never
// a target - the breakables rule), and it stands down when the boss arrives.
public partial class WellEvent : SideEvent
{
	[Export] public float HoldSeconds { get; set; } = 45f;
	[Export] public int FoulSteps { get; set; } = 5;
	[Export] public float WaveIntervalSeconds { get; set; } = 4.6f;
	[Export] public int WalkersPerWave { get; set; } = 3;
	[Export] public float WalkerHealthMultiplier { get; set; } = 1.5f;
	[Export] public float WaveRadius { get; set; } = 600f;
	// A walker whose centre comes this near the well's foot has reached it. The well's footprint
	// stops bodies about 30 px out on every side, so this is "touching it" from any direction.
	[Export] public float WellReach { get; set; } = 62f;

	private const string WellArt = "res://assets/bonelight/world/fields/well.png";
	// The progress ring: where it is drawn round the well, on the ground.
	private const float RingRadius = 150f;
	// The well's solid foot: the stone drum, in world pixels, centred on the ground line.
	private static readonly Vector2 FootSize = new(56f, 22f);

	private Vector2 well;
	private float progress;
	private int fouled;
	private float waveTimer;
	private float lastBearing;
	private float foulFlash;
	private readonly List<Enemy> walkers = new();
	private readonly RandomNumberGenerator rng = new();
	private Sprite2D sprite;
	private StaticBody2D foot;

	protected override string Announcement => "The field has turned on the well. Keep its water clean.";
	protected override string ChestLine => "The well holds - a chest is drawn up from it";
	protected override string Objective =>
		$"Defend the well  ({Mathf.RoundToInt(progress * 100f)}%, fouled {fouled} of {FoulSteps})";
	protected override Vector2? PointerTarget => well;

	// "well" is drawn (tools/art/explore_well.py writes ui/poi/poi-well.png), but PoiPointer only
	// loads the glyphs ExploreArt.Glyphs lists, which is a shared file: until "well" is added there
	// the arrow is the badge without a pictogram.
	protected override string SiteGlyph => "well";
	// The well, its ring and room to fight round it.
	protected override float SiteFootprint => RingRadius + 60f;
	// Near enough that the well and its ring are on screen when the line comes up.
	protected override float ArrivalRadius => 420f;
	// Cold water, a little green: his, and the last water he has not taken.
	protected override Color TellColor => new Color(0.50f, 0.74f, 0.80f);

	protected override void OnPlaced()
	{
		well = MainSite;
		rng.Seed = (ulong)(Mathf.Abs(well.X * 7919f + well.Y * 104729f) + 1f);
		Texture2D texture = ResourceLoader.Exists(WellArt) ? GD.Load<Texture2D>(WellArt) : null;
		if (texture == null)
		{
			GD.PushError($"WellEvent: {WellArt} is missing or not imported; the well will be light without a well. "
				+ "Re-run: python tools/art/explore_well.py, then import.");
		}
		else
		{
			// Two frames, clean and fouled. Stood on its ground line and sorted at its foot the way
			// every grounded sprite is (ExploreArt.MakeGrounded): the PNG's bottom edge is the ground
			// and its middle column the site (explore_well.py's convention).
			int frameWidth = texture.GetWidth() / 2;
			int height = texture.GetHeight();
			Vector2 ground = ToLocal(well);
			sprite = new Sprite2D
			{
				Name = "Well",
				Texture = texture,
				Hframes = 2,
				Centered = false,
				Position = new Vector2(Mathf.Round(ground.X), Mathf.Round(ground.Y - WorldSprites.SortLift)),
				Offset = new Vector2(-frameWidth / 2, WorldSprites.SortLift - height),
				// ZIndex 5 under this node's -5 is the actors' 0: it sorts among them at its foot.
				ZIndex = 5,
			};
			AddChild(sprite);
		}

		// THE FOOT: the drum is solid on the ground's own layer, which the player meets with his
		// boots and every enemy with its body (.ai/terrain.md section 2), as a tree's trunk is. Not
		// in the navigation grid - 56 px is a thing a body slides off - and never a target.
		foot = new StaticBody2D
		{
			Name = "WellFoot",
			Position = ToLocal(well) + new Vector2(0f, -FootSize.Y * 0.5f),
			CollisionLayer = 1u << (LevelTilePainter.TerrainCollisionLayer - 1),
			CollisionMask = 0,
		};
		foot.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = FootSize } });
		AddChild(foot);
	}

	protected override void Begin()
	{
		well = MainSite;
		progress = 0f;
		fouled = 0;
		// The first wave comes quickly: the announcement is still up when it is seen walking in.
		waveTimer = 1.5f;
		lastBearing = rng.Randf() * Mathf.Tau;
	}

	protected override void Tick(float delta)
	{
		if (foulFlash > 0f)
			foulFlash -= delta;

		progress = Mathf.Min(1f, progress + delta / HoldSeconds);

		waveTimer -= delta;
		if (waveTimer <= 0f && progress < 0.97f)
		{
			// A little faster and a little bigger as the hold goes on: the last waves are the ones
			// that decide it.
			waveTimer = WaveIntervalSeconds * (1f - 0.3f * progress);
			SendWave(WalkersPerWave + (progress >= 0.5f ? 1 : 0));
		}

		for (int i = walkers.Count - 1; i >= 0; i--)
		{
			Enemy walker = walkers[i];
			if (IsGone(walker))
			{
				walkers.RemoveAt(i);
				continue;
			}
			Vector2 at = walker.GlobalPosition;
			if (at.DistanceTo(well) <= WellReach)
			{
				Foul(walker);
				walkers.RemoveAt(i);
				continue;
			}
			walker.ScriptedHeading = (well - at).Normalized();
		}

		if (fouled >= FoulSteps)
		{
			ReleaseWalkers();
			if (sprite != null)
				sprite.Frame = 1;
			Fail("The well is fouled - the coin stays down there.");
			return;
		}

		if (progress >= 1f)
		{
			ReleaseWalkers();
			Succeed(well);
		}
	}

	// Each wave walks in from one bearing, a new one every time and never the last one's side, so
	// the player has to go to it.
	private void SendWave(int count)
	{
		float bearing = lastBearing + Mathf.DegToRad(100f + rng.Randf() * 160f);
		lastBearing = bearing;
		for (int k = 0; k < count; k++)
		{
			float a = bearing + (k - (count - 1) * 0.5f) * 0.22f;
			Vector2 at = well + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (WaveRadius + rng.RandfRange(-40f, 40f));
			Enemy walker = Game.SpawnEventEnemy(null, at, WalkerHealthMultiplier, false);
			if (walker == null)
				continue;
			walker.ScriptedHeading = (well - walker.GlobalPosition).Normalized();
			walkers.Add(walker);
		}
	}

	// It reaches the water and is gone into it: no death, no orb - it was not killed, it got through.
	private void Foul(Enemy walker)
	{
		fouled++;
		foulFlash = 0.6f;
		walker.ScriptedHeading = Vector2.Zero;
		walker.QueueFree();
		SfxPlayer.AtPosition(SfxCatalog.EnemyMelee, well, 0f);
	}

	private void ReleaseWalkers()
	{
		foreach (Enemy walker in walkers)
			if (!IsGone(walker))
				walker.ScriptedHeading = Vector2.Zero;
		walkers.Clear();
	}

	protected override void OnStoodDown() => ReleaseWalkers();

	private static readonly Color Water = new Color(0.50f, 0.74f, 0.80f);
	private static readonly Color Fouled = new Color(0.30f, 0.42f, 0.24f);
	private static readonly Color Tether = new Color(0.62f, 0.78f, 0.84f, 0.38f);

	public override void _Draw()
	{
		if (IsResolved || !NearPlayer(well))
			return;
		if (IsDormant)
			return;

		// The progress ring, on the ground: it fills as the hold goes on.
		DrawProgressArc(well, RingRadius, progress, Water, 4f, 0.3f);
		// The foul steps, as a row of pips on the ground just in front of the well: dark once fouled,
		// and the newest flashes. (Round the ring's top they sat under the objective line.)
		Vector2 c = ToLocal(well);
		for (int k = 0; k < FoulSteps; k++)
		{
			Vector2 p = c + new Vector2((k - (FoulSteps - 1) * 0.5f) * 16f, 22f);
			bool on = k < fouled;
			bool newest = on && k == fouled - 1 && foulFlash > 0f;
			DrawCircle(p, newest ? 7f : 5f, on ? new Color(Fouled, 0.95f) : new Color(Water, 0.35f));
			DrawArc(p, newest ? 7f : 5f, 0f, Mathf.Tau, 12, new Color(0f, 0f, 0f, 0.6f), 1.5f);
		}
		// The tethers: a faint dashed line from each walker to the well, so the ones going for it
		// can be told from the ones going for him.
		foreach (Enemy walker in walkers)
		{
			if (IsGone(walker))
				continue;
			Vector2 from = ToLocal(walker.GlobalPosition);
			float length = from.DistanceTo(c);
			if (length < 1f)
				continue;
			Vector2 step = (c - from) / length;
			for (float d = 0f; d < length - 30f; d += 26f)
				DrawLine(from + step * d, from + step * Mathf.Min(d + 12f, length), Tether, 2f);
		}
	}

	// The far tell while it waits; once running, the water's own glow, dimming as it is fouled and
	// darkening for a moment each time it is.
	protected override void DrawLight(CanvasItem light)
	{
		if (IsDormant)
		{
			base.DrawLight(light);
			return;
		}
		if (!NearPlayer(well))
			return;
		float clean = 1f - fouled / (float)Mathf.Max(1, FoulSteps);
		float dip = foulFlash > 0f ? 0.5f : 1f;
		Vector2 p = ToLocal(well) + new Vector2(0f, -30f);
		ExploreArt.DrawGlow(light, p, 140f, new Color(Water, 0.16f * clean * dip * Flicker(0)));
		ExploreArt.DrawGlow(light, p, 50f, new Color(Water, 0.30f * clean * dip));
		ExploreArt.DrawShaft(light, ToLocal(well), 18f, 380f, new Color(Water, 0.18f * clean * Flicker(1)));
	}
}
