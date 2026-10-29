using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Aegis Ward's milestones, the ones that show (.ai/spell-roster.md section 2h). The spell itself is no
// node: its cooldown refills Player's shield pool (Player, the aegis_ward branch) and TakeDamage spends it.
// What its cards add is drawn and run here - one node in the arena, fixed pools, nothing allocated per
// bolt, shard or strike - and on the Air layer, over the bodies: a ward is round the wizard, not on the
// floor.
//
//   Rampart     (level 4, was Quick Bastion) - while the shield holds it stands out from him as a dome,
//               and an enemy bolt that reaches the dome breaks on it, costing the shield only RampartCost
//               of the bolt. The dome is drawn only where a bolt breaks on it: a flash of its curve -
//               the shield is the aura Player already draws; this is the bolt guard (2d-bis) the spell
//               is, made visible.
//   Reforged    (level 4, +1 Metal, was Fortified Aegis) - every time the ward comes back it is forged
//               out of the air: ReforgeShards shards of iron fly in to him from ReforgeReach round him,
//               cutting what they pass for ReforgeShare of the shield's size.
//   Reflective Plating (level 4) - Player.TryReflectAbsorbed's strike now shows: a shaft of light from
//               him to the enemy that takes it back.
//
// Thorn Crystal Shell's burst and Sun Aegis's flash on a break stay in Player (TryCrystalShellBurst).
public partial class AegisWard : Node2D
{
	public const string RampartId = "aegis_ward_quick_bastion";
	public const string ReforgedId = "aegis_ward_fortified";
	public const string ReflectiveId = "aegis_ward_reflective";

	// --- Rampart -------------------------------------------------------------------------------------
	// The dome's radius from his centre: two bodies out, past his own body and the HurtBox, so a bolt is
	// seen to break on something that is not him.
	public const float RampartRadius = 52f;
	// What a bolt broken on the dome costs the shield, as a share of the bolt's damage.
	public const float RampartCost = 0.5f;
	private const float DomeFlashSeconds = 0.28f;
	// The arc of the dome lit by one bolt, either side of where it broke (radians).
	private const float DomeFlashHalfArc = 0.55f;

	// --- Reforged ------------------------------------------------------------------------------------
	public const int ReforgeShards = 8;
	public const float ReforgeReach = 140f;
	public const float ReforgeShare = 0.6f;
	private const float ReforgeSeconds = 0.32f;
	// A shard's half-width as a hit, before the body it meets.
	private const float ShardHalfWidth = 4f;
	private const int MaxShards = 24;
	private const int MaxHitsPerShard = 16;

	// --- Reflective Plating ---------------------------------------------------------------------------
	private const float ShaftSeconds = 0.14f;
	private const int MaxShafts = 8;
	private const int MaxFlashes = 12;

	// The balance report's count (_BalanceHarness), across the run.
	public static int StatBoltsWarded { get; private set; }
	public static int StatShardHits { get; private set; }
	public static int StatReforges { get; private set; }
	public static int StatReflects { get; private set; }

	public static bool HasStats => StatBoltsWarded + StatShardHits + StatReflects > 0;

	public static string StatsLine() =>
		$"aegis ward   : {StatBoltsWarded} bolts broken on the rampart; {StatReforges} reforges cutting {StatShardHits} times; {StatReflects} strikes reflected";

	private struct Flash
	{
		public float Angle;
		public float Age;
		public bool Live;
	}

	private struct Shard
	{
		public Vector2 Dir;      // from him out to where it started, unit
		public float Age;
		public float Last;       // how far out it was last frame
		public int Damage;
		public int Hits;
		public uint Seed;
		public bool Live;
	}

	private struct Shaft
	{
		public Vector2 From;
		public Vector2 To;
		public float Age;
		public bool Live;
	}

	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; set; }

	private readonly Flash[] flashes = new Flash[MaxFlashes];
	private readonly Shard[] shards = new Shard[MaxShards];
	private readonly ulong[] shardHits = new ulong[MaxShards * MaxHitsPerShard];
	private readonly Shaft[] shafts = new Shaft[MaxShafts];
	private readonly List<Enemy> nearBuffer = new();
	private readonly Vector2[] diamond = new Vector2[4];
	private readonly ElementColors.Ramp light = ElementColors.GetRamp(Element.Light);
	private readonly ElementColors.Ramp metal = ElementColors.GetRamp(Element.Metal);
	private float reach = ReforgeReach;
	private int nextFlash;
	private int nextShard;
	private int nextShaft;
	private uint seedClock;
	private bool drewLastFrame;

	public override void _Ready()
	{
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;
		GuardBolts();
		bool any = false;
		for (int i = 0; i < MaxShards; i++)
		{
			if (!shards[i].Live)
				continue;
			any = true;
			TickShard(i, dt);
		}
		for (int i = 0; i < MaxFlashes; i++)
		{
			if (!flashes[i].Live)
				continue;
			any = true;
			flashes[i].Age += dt;
			if (flashes[i].Age >= DomeFlashSeconds)
				flashes[i].Live = false;
		}
		for (int i = 0; i < MaxShafts; i++)
		{
			if (!shafts[i].Live)
				continue;
			any = true;
			shafts[i].Age += dt;
			if (shafts[i].Age >= ShaftSeconds)
				shafts[i].Live = false;
		}
		// Once more after the last one goes, or its final frame would stay on screen.
		if (any || drewLastFrame)
			QueueRedraw();
		drewLastFrame = any;
	}

	// --- Rampart ---------------------------------------------------------------------------------

	// Every enemy bolt in flight that has reached the dome while the shield holds breaks on it: no hit on
	// him, a burst of light where it broke, and the shield pays RampartCost of it (Player.SpendShield,
	// which breaks the shield - and sets off what a break sets off - if that empties it). The bolt list is
	// EnemyProjectile.Live, a handful at most.
	private void GuardBolts()
	{
		if (SpellData == null || !SpellData.HasEvolution(RampartId) || PlayerRef.ShieldPoints <= 0)
			return;
		IReadOnlyList<EnemyProjectile> bolts = EnemyProjectile.Live;
		if (bolts.Count == 0)
			return;
		Vector2 centre = PlayerRef.GlobalPosition;
		float reachSquared = RampartRadius * RampartRadius;
		for (int b = bolts.Count - 1; b >= 0 && PlayerRef.ShieldPoints > 0; b--)
		{
			EnemyProjectile bolt = bolts[b];
			if (bolt == null || !IsInstanceValid(bolt) || !bolt.InFlight)
				continue;
			Vector2 offset = bolt.GlobalPosition - centre;
			if (offset.LengthSquared() > reachSquared)
				continue;
			int cost = Math.Max(bolt.Damage > 0 ? 1 : 0, Mathf.CeilToInt(bolt.Damage * RampartCost));
			bolt.Destroy(Element.Light);
			StatBoltsWarded++;
			AddFlash(offset.Angle());
			if (cost > 0)
				PlayerRef.SpendShield(cost);
		}
	}

	private void AddFlash(float angle)
	{
		int slot = nextFlash;
		for (int i = 0; i < MaxFlashes; i++)
		{
			int s = (nextFlash + i) % MaxFlashes;
			if (!flashes[s].Live)
			{
				slot = s;
				break;
			}
		}
		nextFlash = (slot + 1) % MaxFlashes;
		flashes[slot] = new Flash { Angle = angle, Live = true };
	}

	// --- Reforged ---------------------------------------------------------------------------------

	/// <summary>
	/// The ward has come back (Player's aegis cast restored the shield): under Reforged, it is forged out
	/// of the air - ReforgeShards shards fly in to him from ReforgeReach x Area, each cutting what its
	/// path crosses for ReforgeShare of <paramref name="shieldSize"/>.
	/// </summary>
	public void Reforge(int shieldSize, float area)
	{
		if (SpellData == null || !SpellData.HasEvolution(ReforgedId))
			return;
		StatReforges++;
		reach = ReforgeReach * MathF.Sqrt(MathF.Max(0.25f, area));
		int damage = Math.Max(1, Mathf.RoundToInt(shieldSize * ReforgeShare));
		float turn = (seedClock * 0.6180339f % 1f) * Mathf.Tau;
		for (int k = 0; k < ReforgeShards; k++)
		{
			float angle = turn + k * Mathf.Tau / ReforgeShards;
			int slot = nextShard;
			for (int i = 0; i < MaxShards; i++)
			{
				int s = (nextShard + i) % MaxShards;
				if (!shards[s].Live)
				{
					slot = s;
					break;
				}
			}
			nextShard = (slot + 1) % MaxShards;
			shards[slot] = new Shard
			{
				Dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle)),
				Last = reach,
				Damage = damage,
				Seed = ++seedClock,
				Live = true,
			};
		}
		SfxPlayer.Instance?.PlayAt(SfxCatalog.PlayerShieldAbsorb, PlayerRef.GlobalPosition, 0.06f, SfxCatalog.PlayerShieldAbsorb, 120);
	}

	// How far out from him a shard is, `age` into its flight: fast at first, slowing as it closes - it is
	// drawn in, not thrown.
	private float ShardOut(float age)
	{
		float u = Mathf.Clamp(age / ReforgeSeconds, 0f, 1f);
		float eased = 1f - (1f - u) * (1f - u);
		return Mathf.Lerp(reach, 10f, eased);
	}

	// A shard strikes everything whose centre its path crossed this frame - the stretch between where it
	// was and where it is, along its line in to him - once.
	private void TickShard(int i, float dt)
	{
		ref Shard s = ref shards[i];
		s.Age += dt;
		float now = ShardOut(s.Age);
		Vector2 centre = PlayerRef.GlobalPosition;
		Vector2 a = centre + s.Dir * s.Last;
		Vector2 b = centre + s.Dir * now;
		s.Last = now;
		Strike(ref s, i, a, b);
		if (s.Age >= ReforgeSeconds)
			s.Live = false;
	}

	private void Strike(ref Shard s, int i, Vector2 a, Vector2 b)
	{
		Vector2 path = b - a;
		float length = path.Length();
		float hit = ShardHalfWidth + RiptideWave.BodyAllowance;
		EnemyRegistry.CollectNear(a + path * 0.5f, length * 0.5f + hit, nearBuffer);
		float lengthSquared = MathF.Max(0.0001f, length * length);
		int row = i * MaxHitsPerShard;
		foreach (Enemy enemy in nearBuffer)
		{
			if (enemy == null || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			Vector2 p = enemy.GlobalPosition;
			float t = Mathf.Clamp((p - a).Dot(path) / lengthSquared, 0f, 1f);
			if (p.DistanceSquaredTo(a + path * t) > hit * hit)
				continue;
			ulong id = enemy.GetInstanceId();
			bool seen = false;
			for (int h = 0; h < s.Hits; h++)
			{
				if (shardHits[row + h] == id)
				{
					seen = true;
					break;
				}
			}
			if (seen)
				continue;
			if (s.Hits < MaxHitsPerShard)
				shardHits[row + s.Hits++] = id;
			StatShardHits++;
			PlayerRef.DealDamageToEnemy(enemy, s.Damage, source: SpellData);
		}
		nearBuffer.Clear();
	}

	// --- Reflective Plating ---------------------------------------------------------------------------

	/// <summary>The reflected strike, drawn: a shaft of light from him to the enemy that took it back.</summary>
	public void ShowReflect(Vector2 from, Vector2 to)
	{
		StatReflects++;
		int slot = nextShaft;
		for (int i = 0; i < MaxShafts; i++)
		{
			int s = (nextShaft + i) % MaxShafts;
			if (!shafts[s].Live)
			{
				slot = s;
				break;
			}
		}
		nextShaft = (slot + 1) % MaxShafts;
		shafts[slot] = new Shaft { From = from, To = to, Live = true };
		QueueRedraw();
	}

	// --- drawing ---------------------------------------------------------------------------------

	public override void _Draw()
	{
		Transform2D toLocal = GlobalTransform.AffineInverse();
		Vector2 centre = PlayerRef != null && IsInstanceValid(PlayerRef) ? toLocal * PlayerRef.GlobalPosition : Vector2.Zero;
		DrawFlashes(centre);
		DrawShards(toLocal, centre);
		DrawShafts(toLocal);
	}

	private static Color Fade(Color c, float alpha) => new Color(c.R, c.G, c.B, c.A * alpha);

	private static Vector2 Snap(Vector2 p) =>
		new Vector2(MathF.Round(p.X / WhipLimb.Grid) * WhipLimb.Grid, MathF.Round(p.Y / WhipLimb.Grid) * WhipLimb.Grid);

	// Where a bolt broke, the dome lights: a stretch of its curve either side of the break, white-hot at
	// the break and cooling along it, with a ripple of plates inside it - the dome is there, it is struck.
	private void DrawFlashes(Vector2 centre)
	{
		for (int i = 0; i < MaxFlashes; i++)
		{
			if (!flashes[i].Live)
				continue;
			Flash f = flashes[i];
			float u = f.Age / DomeFlashSeconds;
			float alpha = 1f - u * u;
			float spread = DomeFlashHalfArc * (0.6f + 0.4f * u);
			const int steps = 7;
			Vector2 prev = Vector2.Zero;
			for (int k = 0; k <= steps; k++)
			{
				float t = -1f + 2f * k / steps;
				float a = f.Angle + t * spread;
				Vector2 p = centre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * RampartRadius;
				if (k > 0)
				{
					float heat = 1f - MathF.Abs(t);
					Color c = heat > 0.6f ? light.Core : heat > 0.25f ? light.Hot : light.Mid;
					DrawLine(Snap(prev), Snap(p), Fade(light.Mid, 0.45f * alpha), 6f);
					DrawLine(Snap(prev), Snap(p), Fade(c, alpha), 2f);
				}
				prev = p;
			}
			// The plates: three short chords just inside the curve, the dome's panels.
			for (int k = -1; k <= 1; k++)
			{
				float a = f.Angle + k * spread * 0.55f;
				Vector2 at = centre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (RampartRadius - 6f);
				Vector2 along = new Vector2(-MathF.Sin(a), MathF.Cos(a)) * 5f;
				DrawLine(Snap(at - along), Snap(at + along), Fade(light.Hot, 0.7f * alpha), 2f);
			}
		}
	}

	// A shard of iron flying in: a long diamond along its line, lit on the side the key light reaches,
	// with a short streak behind it in Light's ramp - the ward's light, the plate's metal.
	private void DrawShards(Transform2D toLocal, Vector2 centre)
	{
		for (int i = 0; i < MaxShards; i++)
		{
			if (!shards[i].Live)
				continue;
			Shard s = shards[i];
			float out_ = ShardOut(s.Age);
			Vector2 at = centre + s.Dir * out_;
			Vector2 inward = -s.Dir;
			Vector2 side = new Vector2(-inward.Y, inward.X);
			float alpha = Mathf.Clamp((ReforgeSeconds - s.Age) / 0.06f, 0f, 1f);
			Vector2 tip = at + inward * 6f;
			Vector2 tail = at - inward * 6f;
			DrawLine(Snap(tail), Snap(tail - inward * 12f), Fade(light.Mid, 0.6f * alpha), 2f);
			diamond[0] = Snap(tip);
			diamond[1] = Snap(at + side * 3f);
			diamond[2] = Snap(tail);
			diamond[3] = Snap(at - side * 3f);
			DrawColoredPolygon(diamond, Fade(metal.Mid, alpha));
			DrawLine(Snap(tip), Snap(tail), Fade(metal.Hot, alpha), 2f);
			DrawRect(WhipLimb.Cell(tip, 2f), Fade(light.Core, alpha));
		}
	}

	private void DrawShafts(Transform2D toLocal)
	{
		for (int i = 0; i < MaxShafts; i++)
		{
			if (!shafts[i].Live)
				continue;
			Shaft s = shafts[i];
			float alpha = 1f - s.Age / ShaftSeconds;
			Vector2 a = Snap(toLocal * s.From);
			Vector2 b = Snap(toLocal * s.To);
			DrawLine(a, b, Fade(light.Mid, 0.5f * alpha), 6f);
			DrawLine(a, b, Fade(light.Core, alpha), 2f);
			DrawRect(WhipLimb.Cell(b, 6f), Fade(light.Core, alpha));
		}
	}
}
