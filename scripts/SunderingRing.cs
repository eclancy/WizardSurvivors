using Godot;
using WizardSurvivors.scripts;

// Sundering, the Geomancer's Apotheosis (issue #61). Verb: hold - the arena is edited.
//
// Each Obsidian Spike cast shoves everything near the player outward and raises a broken ring of
// obsidian pillars around where the player stood. The pillars shove back and cut whatever pushes
// into them, and inside the ring the player takes less from every hit. The ring has gaps - it is
// cover, not a wall - and only one stands at a time, so the player keeps re-siting it as the wave
// moves.
//
// #61 asked for "flat damage reduction, stacking with Earth's flat-reduction identity". Flat
// reduction has since left the game (armour is one percentage stat, see Player.GetArmorPercent and
// the note on characterArmorPercent), so the ring grants armour through the same sum and the same
// cap. The pillars are not physics bodies, for Iron Palisade's reason: a body enemies collide with
// needs a new layer and every enemy mask changed, and would block the player too.
public partial class SunderingRing : Node2D
{
	/// <summary>The ring standing now, if any. Read by Player.GetArmorPercent.</summary>
	public static SunderingRing Active { get; private set; }

	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; set; }

	public const float ArmorInside = 0.20f;
	private const float Radius = 112f;
	private const float LifeSeconds = 10f;
	private const int Slots = 12;
	private const float PillarRadius = 16f;
	private const float ShoveRadius = 170f;
	private const float ShoveSpeed = 340f;
	private const float CutInterval = 0.6f;

	private readonly bool[] standing = new bool[Slots];
	private readonly Vector2[] shard = new Vector2[4];
	private readonly Vector2[] pillars = new Vector2[Slots];
	private float age;
	private float tick;
	private readonly System.Collections.Generic.Dictionary<ulong, float> nextCutAt = new();

	private static readonly Color Obsidian = new Color(0.10f, 0.08f, 0.14f, 0.95f);
	private static readonly Color Facet = new Color(0.55f, 0.45f, 0.70f, 0.9f);
	private static readonly ElementColors.Ramp EarthRamp = ElementColors.GetRamp(Element.Earth);

	/// <summary>Raises a ring at <paramref name="centre"/>, replacing any ring already standing.</summary>
	public static void Raise(Node arena, Player player, SpellData spell, Vector2 centre)
	{
		if (Active != null && IsInstanceValid(Active))
			Active.QueueFree();
		var ring = new SunderingRing { PlayerRef = player, SpellData = spell };
		arena.AddChild(ring);
		ring.GlobalPosition = centre;
		Active = ring;
		ring.Shove();
	}

	public bool Contains(Vector2 world) => GlobalPosition.DistanceTo(world) < Radius - PillarRadius;

	public override void _Ready()
	{
		ZIndex = 1;
		// Three gaps in twelve, at a random turn, so the cover is never the same shape twice.
		float turn = (float)GD.RandRange(0.0, Mathf.Tau);
		int gapStart = (int)GD.Randi() % Slots;
		for (int i = 0; i < Slots; i++)
		{
			standing[i] = !(i == gapStart || i == (gapStart + 4) % Slots || i == (gapStart + 8) % Slots);
			float a = turn + i * Mathf.Tau / Slots;
			pillars[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Radius;
		}
	}

	public override void _ExitTree()
	{
		if (Active == this)
			Active = null;
	}

	private void Shove()
	{
		float r2 = ShoveRadius * ShoveRadius;
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy e || e.IsDying) continue;
			Vector2 away = e.GlobalPosition - GlobalPosition;
			if (away.LengthSquared() > r2 || away.LengthSquared() < 1f) continue;
			e.ApplyKnockback(away.Normalized() * ShoveSpeed);
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		age += dt;
		QueueRedraw();
		if (age >= LifeSeconds)
		{
			QueueFree();
			return;
		}

		tick -= dt;
		if (tick > 0f) return;
		tick = 0.1f;
		if (PlayerRef == null || !IsInstanceValid(PlayerRef)) return;

		int damage = Mathf.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(SpellData.CurrentLevel) ?? 6) * 0.4f));
		float hit2 = (PillarRadius + 10f) * (PillarRadius + 10f);
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy e || e.IsDying) continue;
			Vector2 local = e.GlobalPosition - GlobalPosition;
			for (int i = 0; i < Slots; i++)
			{
				if (!standing[i] || local.DistanceSquaredTo(pillars[i]) > hit2) continue;
				Vector2 push = (local - pillars[i]).Normalized();
				e.ApplyKnockback(push * 220f);
				ulong id = e.GetInstanceId();
				if (!nextCutAt.TryGetValue(id, out float readyAt) || age >= readyAt)
				{
					nextCutAt[id] = age + CutInterval;
					PlayerRef.DealDamageToEnemy(e, damage, source: SpellData);
				}
				break;
			}
		}
	}

	public override void _Draw()
	{
		float rise = Mathf.Clamp(age / 0.2f, 0f, 1f);
		float fade = Mathf.Clamp((LifeSeconds - age) / 0.5f, 0f, 1f);
		// The shove as it happens: earth thrown out to exactly ShoveRadius, what it moved. The ring of
		// pillars is the cover - objects standing on the ground - and there is no disc under it any more
		// (it was an area marker; 2026-10-23).
		ImpactDebris.Draw(this, Vector2.Zero, ShoveRadius, age, (uint)(GetInstanceId() % 9973), EarthRamp,
			ImpactDebris.CountFor(ShoveRadius), Colors.White);
		for (int i = 0; i < Slots; i++)
		{
			if (!standing[i]) continue;
			Vector2 p = pillars[i];
			float h = 26f * rise;
			shard[0] = p + new Vector2(-9f, 4f);
			shard[1] = p + new Vector2(-4f, -h);
			shard[2] = p + new Vector2(3f, -h * 0.8f);
			shard[3] = p + new Vector2(9f, 4f);
			DrawColoredPolygon(shard, new Color(Obsidian, Obsidian.A * fade));
			DrawLine(p + new Vector2(-4f, -h), p + new Vector2(-6f, 2f), new Color(Facet, Facet.A * fade), 2f);
		}
	}
}
