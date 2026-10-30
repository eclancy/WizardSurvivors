using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts;

// Shared script for straightforward "elemental bolt" offensive spells (issue #13 roster expansion):
// Frost Shard, Shadow Bolt, Gale Blade, Molten Shard, Chain Lightning, Void Lance, Glacial Spike,
// Riptide, Hunter's Draw's arrows. (Thorn Vine left it for its own whip, scripts/ThornVine.cs.) Behaves like MagicMissile (homing bolt with optional pierce) plus optional
// guaranteed on-hit status effects and a single chain-jump to a second nearby enemy, configured
// per-scene via exported flags instead of one bespoke script per spell.
//
// EVERY EVOLUTION ITS OWN (2026-10-29, .ai/spell-roster.md 2e). Four of the spells it flies - Molten
// Shard, Riptide, Hunter's Draw and Gale Blade - had milestone cards that only moved a bolt's numbers.
// Each card now changes what the bolt DOES, switched on by its id (BoltEvolutions, read once when the
// bolt is shot): a shard that leaves slag, splits, lodges and erupts, dives into a lava pool or waits in
// a forged ring; a wave that throws what it breaks over or builds as it runs; an arrow that stays in
// what it stopped in; a blade that hangs in an eddy, comes home on a hook, trails a live wire, calls
// down lightning where it turns or bursts into five. Frost Shard takes none of them, and with no such
// card taken every path below is the one it always was.
//
// THE LOOK IS A CHILD'S. Each of the four spells' scenes carries a child that paints the bolt from the
// numbers it hits with, as pixel art on the world's 2 px grid (2026-10-29): RiptideWave, MoltenShardLook,
// GaleBladeLook and HuntersArrowLook, each one quad and one shader. What they need to know of the bolt's
// state is exposed below (HitRadius, IsLodged, LodgeFuse, IsForging, EddyReachNow, EddySwell); Live Wire's
// line is LiveWireLook, a child it spans every frame over the segment the cut measures.
public partial class ElementalBolt : Area2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float BaseSpeed { get; set; } = 350f;
	[Export] public float BaseDuration { get; set; } = 5.0f;
	[Export] public int BasePierce { get; set; } = 0;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float DurationMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;

	// Guaranteed on-hit status effects, independent of the player's element-tier bonuses (#16).
	[Export] public bool GuaranteedSlow { get; set; } = false;
	[Export] public float SlowMultiplier { get; set; } = 0.5f;
	[Export] public float SlowDuration { get; set; } = 2.0f;
	[Export] public bool GuaranteedFreeze { get; set; } = false;
	[Export] public float FreezeDuration { get; set; } = 1.2f;
	[Export] public bool GuaranteedPoison { get; set; } = false;
	[Export] public int PoisonDamagePerTick { get; set; } = 2;
	[Export] public float PoisonDuration { get; set; } = 3.0f;
	// Share of the target's max HP burned away on hit (Enemy.ApplyWither, issue #66). 0 = off.
	// Molten Shard's: it is what makes "pierces armor" true.
	[Export] public float WitherFraction { get; set; } = 0f;
	[Export] public bool AoEOnImpact { get; set; } = false;
	[Export] public float AoERadius { get; set; } = 50.0f;
	[Export] public bool ChainToSecondTarget { get; set; } = false;
	[Export] public float ChainRadius { get; set; } = 150f;
	[Export] public float ChainDamageMultiplier { get; set; } = 0.6f;
	// Every bolt so far curves toward its target in flight. An arrow should not: it goes exactly
	// where it was aimed at the moment it was loosed, which is what makes lining up a piercing
	// shot down a row of enemies a thing the player can actually do. Defaults to the old
	// behaviour so no existing scene changes.
	[Export] public bool Homing { get; set; } = true;
	[Export] public bool InstantBoltVisual { get; set; } = false;
	[Export] public float InstantBoltLifetime { get; set; } = 0.16f;
	// Arc bolts hit one enemy, vanish, then a fresh bolt is spawned at the
	// impact point and fired straight at the next enemy - a new streak per hop instead of one
	// projectile curving through the air. MaxChainBounces is how many extra enemies a cast reaches.
	[Export] public int MaxChainBounces { get; set; } = 1;
	// Returning bolts (Gale Blade, issue #64): thrown out in a straight line past the target, then
	// flown back to the player, hitting everything on both legs. Pierce is unlimited and each enemy
	// can be struck once per leg. It is the only projectile whose path is about the ground between
	// the player and the swarm rather than the enemy at the end of it.
	[Export] public bool Returning { get; set; } = false;
	// The shortest throw, before Area scaling, so a blade aimed at an enemy on top of the player
	// still makes a real arc. Otherwise the throw carries ReturnOvershoot past whatever it was aimed
	// at - never short of it, which would turn the blade round in front of its own target.
	[Export] public float MinThrowDistance { get; set; } = 200f;
	[Export] public float ReturnOvershoot { get; set; } = 70f;
	[Export] public float ReturnSpinSpeed { get; set; } = 16f;
	[Export] public float ChainRespawnDelay { get; set; } = 0.12f;
	public System.Collections.Generic.HashSet<Node2D> ChainVisited;
	public int ChainBouncesRemaining { get; set; } = -1;
	public int OverrideDamage { get; set; } = -1;

	/// <summary>
	/// 0 for a bolt the player cast; 1 for one an evolution made out of another - a split shard's
	/// half, a squall's extra blade. Only a cast's own bolt splits, turns in an eddy, strikes lightning
	/// or bursts, so a half never splits again and a squall never multiplies.
	/// </summary>
	public int Generation { get; set; } = 0;

	/// <summary>
	/// What share of the spell's knockback (its milestones' KnockbackBonus, SpellData.GetKnockbackSpeedAtLevel)
	/// this bolt carries along its heading. Riptide's Breaker throws with every wave; Hunter's Draw's
	/// Heavy Draw only with a full draw's arrows, so the volley sets 0 on the rest.
	/// </summary>
	public float KnockbackScale { get; set; } = 1f;

	/// <summary>Seconds of stagger (Enemy.ApplyShock) on everything this bolt strikes. Heavy Draw's.</summary>
	public float StaggerSeconds { get; set; } = 0f;

	private const float ArcVisualLength = 28f;
	private Line2D boltLine;
	private float[] arcOffsets = Array.Empty<float>();
	private int arcBendCount = 0;

	private int damage = 1;
	private float range = 500f;
	private float speed = 350f;
	private float duration = 5.0f;
	private int pierce = 0;
	private float areaRadius = BaseHitRadius;
	private float knockbackSpeed = 0f;

	/// <summary>The collision radius of a bolt with no Area at all, before the player's Area.</summary>
	public const float BaseHitRadius = 8f;

	/// <summary>
	/// The radius of the collision circle, before the node's own scale (an evolution's
	/// ScaleMultiplier, which scales the shape and any drawn child alike). A child that draws the
	/// projectile reads this, so the drawing can never be a different size from the hit (RiptideWave).
	/// </summary>
	public float HitRadius => areaRadius;

	/// <summary>
	/// Above zero, the hit is a band ACROSS the heading rather than a circle: a capsule HitRadius to
	/// either side and this fraction of HitRadius deep along the travel (Riptide, 0.35).
	/// </summary>
	/// <remarks>
	/// A wave is wider than it is long. While Riptide's circle was 8 px that did not matter, but a
	/// circle as deep as it is wide hits what is ahead of the crest before the crest reaches it, by up
	/// to half its width - at the sizes the wave grows to now (a level 8 wave is over five times the
	/// first, Titan Cataclysm's three times that) a skeleton would be struck a body or more before the water
	/// touched it. The band across the heading is the swath it sweeps, exactly as wide as before; only
	/// the depth changed, to the depth of the crest RiptideWave draws (its front bulges about 0.38 of
	/// the half-width ahead of centre).
	/// </remarks>
	[Export] public float CrestDepthFraction { get; set; } = 0f;

	/// <summary>Travel speed in px/s, after level bonuses. Spring Tide floods at the wave's pace.</summary>
	public float Speed => speed;

	/// <summary>The damage one hit of this bolt deals, before crits and the target's own modifiers.</summary>
	public int Damage => damage;

	/// <summary>Raised once per enemy this bolt damages, after the damage. RiptideWave splashes on it.</summary>
	public event Action<Node2D> Struck;
	private float scaledSlowMultiplier = 0.5f;
	private float scaledSlowDuration = 2.0f;
	private int scaledPoisonTick = 2;
	private float scaledPoisonDuration = 3.0f;

	private Vector2 direction = Vector2.Zero;
	private Node target = null;
	private float turnSpeed = 6.0f;
	private float lifetime = 0f;
	private int pierceCount = 0;
	private Vector2 spawnPosition = Vector2.Zero;
	private bool instantBoltActive = false;
	private Vector2 instantBoltFromLocal = Vector2.Zero;
	private Vector2 instantBoltToLocal = Vector2.Right;
	private float instantBoltFadeAlpha = 1f;
	private bool returnLeg = false;
	private float outboundDistance = 0f;
	// Instance ids struck on the current leg. Allocated once per bolt, cleared at the turn.
	private System.Collections.Generic.HashSet<ulong> struckThisLeg;
	private const float CatchRadius = 18f;

	// The node's scale when it was shot - Player.ApplyLegendaryVisual's, before an evolution grows or
	// pulses it in flight (Groundswell, an eddy, a lodged core). Every such change is relative to this.
	private Vector2 shotScale = Vector2.One;

	// --- Which evolutions this bolt carries (BoltEvolutions), read once at the shot ---
	private bool slagTrail, splitOnHit, magmaCore, caldera;
	private bool groundswell;
	private bool barbed;
	private bool eddy, hooked, liveWire, thunderhead, squall;

	// Molten Shard. The slag its flight lays (Slag Trail); the core lodged in an enemy (Magma Core);
	// the forged ring it waits in (Forge Swarm).
	private MoltenSlag slag;
	private bool lodged;
	private Node2D lodgeHost;
	private Vector2 lodgeOffset;
	private Vector2 lodgeAt;
	private float lodgeLeft;
	private bool forging;
	private Node2D forgeCentre;
	private int forgeSlot;
	private int forgeSlots = BoltEvolutions.ForgeVolley;
	private float forgeAge;
	private const float ForgeAbandonSeconds = 12f;

	// Riptide's Breaker and Hunter's Heavy Draw throw what they hit, which can carry it ahead of the
	// bolt and back into it: a body is struck once per bolt, as a returning blade's is once per leg.
	private System.Collections.Generic.HashSet<ulong> struckOnce;

	// Gale Blade. The way home bends (a hook, or one path of a squall's five); the eddy it hangs in;
	// the squall still to burst when the eddy ends; the live wire's tick, and the wire drawn
	// (LiveWireLook, a child that draws itself in world space).
	private float returnBend;
	private float returnStartDistance = 1f;
	private float eddyLeft;
	private float eddyRecut;
	private bool squallPending;
	private float wireTick;
	private LiveWireLook wireLook;
	private float eddySwell;
	private static int hookFlip;

	public override void _Ready()
	{
		// Every bolt this script flies is in the air: over the bodies, not sorted in with them by its
		// origin (DrawLayers). Riptide's wave rides two above it.
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		RefreshComputedStats();
		boltLine = GetNodeOrNull<Line2D>("BoltLine");
		if (boltLine != null)
		{
			boltLine.Visible = true;
			boltLine.TextureMode = Line2D.LineTextureMode.Tile;
		}
		var cs = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (cs != null && CrestDepthFraction > 0f)
		{
			// The capsule's long axis is local Y, which is across the heading (Shoot turns local X
			// onto it). A fresh shape: the scene's is shared by every wave in flight.
			float depth = MathF.Max(6f, areaRadius * CrestDepthFraction);
			cs.Shape = new CapsuleShape2D { Radius = depth, Height = MathF.Max(depth * 2f, areaRadius * 2f) };
		}
		else if (cs?.Shape is CircleShape2D shape)
			shape.Radius = areaRadius;
		Connect("area_entered", new Callable(this, nameof(OnAreaEntered)));
		Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
	}

	public override void _ExitTree()
	{
		// The slag outlives the shard that laid it, by exactly its own life from the last point.
		slag?.StopFeeding();
		slag = null;
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	// Which of its spell's evolutions this bolt carries. A string compare per milestone, once per shot.
	private void ReadEvolutions()
	{
		SpellData s = SpellData;
		if (s == null)
			return;
		bool cast = Generation == 0;
		slagTrail = s.HasEvolution(BoltEvolutions.SlagTrailId);
		splitOnHit = cast && s.HasEvolution(BoltEvolutions.SplittingShardId);
		magmaCore = s.HasEvolution(BoltEvolutions.MagmaCoreId);
		caldera = s.HasEvolution(BoltEvolutions.CalderaId);
		groundswell = s.HasEvolution(BoltEvolutions.GroundswellId);
		barbed = s.HasEvolution(BoltEvolutions.BarbedId);
		eddy = cast && s.HasEvolution(BoltEvolutions.EddyId);
		hooked = s.HasEvolution(BoltEvolutions.HookedThrowId);
		liveWire = cast && s.HasEvolution(BoltEvolutions.LiveWireId);
		thunderhead = cast && s.HasEvolution(BoltEvolutions.ThunderheadId);
		squall = cast && s.HasEvolution(BoltEvolutions.SquallId);
	}

	public void Shoot(Vector2 from, Vector2 to, Node enemyTarget = null)
	{
		ReadEvolutions();
		GlobalPosition = from;
		instantBoltActive = false;
		direction = (to - from).Normalized();
		Rotation = direction.Angle();
		spawnPosition = from;
		lifetime = 0f;
		pierceCount = 0;
		returnLeg = false;
		shotScale = Scale;
		if (Returning)
		{
			outboundDistance = MathF.Max(MinThrowDistance * AreaMultiplier, from.DistanceTo(to) + ReturnOvershoot);
			struckThisLeg ??= new System.Collections.Generic.HashSet<ulong>();
			struckThisLeg.Clear();
		}
		if (enemyTarget is Node2D enemyNode)
		{
			float distToEnemy = (enemyNode.GlobalPosition - from).Length();
			target = distToEnemy <= range ? enemyTarget : null;
		}
		else
		{
			target = null;
		}

		if (InstantBoltVisual && enemyTarget is Node2D instantTarget)
		{
			instantBoltActive = true;
			instantBoltFadeAlpha = 1f;
			GlobalPosition = from;
			instantBoltFromLocal = Vector2.Zero;
			instantBoltToLocal = instantTarget.GlobalPosition - from;
			ConfigureArcVisual(instantBoltToLocal.Length());
			ResolveImpact(instantTarget, keepAliveForVisual: true);
			return;
		}

		// Live Wire: the line of lightning from the hand to the blade, drawn by its own child.
		if (liveWire && wireLook == null)
		{
			wireLook = new LiveWireLook { Name = "LiveWire", Gilded = SpellData?.IsLegendary ?? false };
			AddChild(wireLook);
		}

		// Slag Trail: the shard lays its line from the hand that threw it.
		if (slagTrail && slag == null && GetParent() is Node arena && PlayerRef is Player thrower)
		{
			slag = MoltenSlag.Trail(arena, Math.Max(1, Mathf.RoundToInt(damage * BoltEvolutions.SlagTickShare)), SpellData, thrower);
			slag.Feed(from);
		}

		ConfigureArcVisual(ArcVisualLength);
	}

	public override void _Process(double delta)
	{
		if (instantBoltActive)
		{
			lifetime += (float)delta;
			float fadeDuration = MathF.Max(0.05f, InstantBoltLifetime);
			instantBoltFadeAlpha = MathF.Max(0f, 1f - (lifetime / fadeDuration));
			UpdateArcVisual();
			if (lifetime > fadeDuration)
			{
				if (boltLine != null)
					boltLine.Visible = false;
				QueueFree();
			}
			return;
		}

		if (forging)
		{
			ProcessForging();
			return;
		}

		if (lodged)
		{
			ProcessLodged((float)delta);
			return;
		}

		if (Returning)
		{
			ProcessReturning((float)delta);
			return;
		}

		// Arc bolts fly dead straight at their locked target; only non-arc bolts home in midair.
		if (Homing && !ChainToSecondTarget && target != null && IsInstanceValid(target))
		{
			if (target is Node2D targetNode)
			{
				var toTarget = targetNode.GlobalPosition - GlobalPosition;
				if (toTarget.Length() > 0)
				{
					var tdir = toTarget.Normalized();
					var alpha = MathF.Min(1f, turnSpeed * (float)delta);
					direction = (direction * (1f - alpha) + tdir * alpha).Normalized();
					Rotation = direction.Angle();
				}
			}
		}

		float travelled = (GlobalPosition - spawnPosition).Length();
		if (travelled > range)
			target = null;

		// Groundswell: the wave leaves the hand narrow and builds to its full width by the end of its
		// run. The node's scale is the hit and the drawing alike (RiptideWave), so it widens as one.
		if (groundswell)
		{
			float grown = Mathf.Lerp(BoltEvolutions.GroundswellStart, BoltEvolutions.GroundswellEnd,
				Mathf.Clamp(travelled / MathF.Max(1f, range), 0f, 1f));
			Scale = shotScale * grown;
		}

		Position += direction * speed * (float)delta;
		// The slag is laid along the shard's flight at the pack, its cast range; a shard that pierced
		// on past it flies on clean rather than drawing lines across the whole field.
		if (slag != null)
		{
			if (travelled <= range)
				slag.Feed(GlobalPosition);
			else
			{
				slag.StopFeeding();
				slag = null;
			}
		}
		UpdateArcVisual();
		lifetime += (float)delta;
		if (duration > 0 && lifetime > duration) QueueFree();
	}

	// Out in a straight line, then back to wherever the player now is. The return leg steers every
	// frame, so a player who moves while the blade is out is still caught - and the path home sweeps
	// through whatever has closed in behind it.
	private void ProcessReturning(float delta)
	{
		if (!returnLeg && (GlobalPosition - spawnPosition).Length() >= outboundDistance)
			TurnAtEnd();

		if (liveWire)
			TickLiveWire(delta);

		// Eddy: the blade hangs where it turned, spinning, before anything else happens on the way home.
		if (eddyLeft > 0f)
		{
			ProcessEddy(delta);
			lifetime += delta;
			return;
		}

		if (returnLeg)
		{
			if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			{
				QueueFree();
				return;
			}
			Vector2 home = PlayerRef.GlobalPosition - GlobalPosition;
			float homeDistance = home.Length();
			if (homeDistance <= CatchRadius)
			{
				QueueFree();
				return;
			}
			direction = home / homeDistance;
			// A hooked or squalling blade comes home at an angle that closes as it nears, so its path
			// home is a curve to one side of its throw rather than the same line back. Under 90 degrees
			// it always gains on the hand.
			if (returnBend != 0f)
				direction = direction.Rotated(returnBend * Mathf.Clamp(homeDistance / returnStartDistance, 0f, 1f));
		}

		Position += direction * speed * delta;
		Rotation += ReturnSpinSpeed * delta;
		UpdateArcVisual();
		lifetime += delta;
		if (duration > 0 && lifetime > duration) QueueFree();
	}

	private void TurnAtEnd()
	{
		returnLeg = true;
		struckThisLeg?.Clear();
		returnStartDistance = PlayerRef != null && IsInstanceValid(PlayerRef)
			? MathF.Max(1f, PlayerRef.GlobalPosition.DistanceTo(GlobalPosition)) : 1f;
		if (hooked && Generation == 0)
		{
			hookFlip++;
			returnBend = Mathf.DegToRad(BoltEvolutions.HookDegrees) * ((hookFlip & 1) == 0 ? 1f : -1f);
		}
		if (thunderhead)
			StrikeThunder();
		if (eddy)
		{
			eddyLeft = BoltEvolutions.EddySeconds;
			eddyRecut = 0f;
			squallPending = squall;
		}
		else if (squall)
			BurstIntoSquall();
	}

	// Eddy: the blade swells to EddyScale and spins hard where it hangs, cutting everything within its
	// drawn reach again every EddyRecutSeconds - a body already inside raises no new "entered" signal,
	// so the cut is a scan of the reach rather than a wait for contacts.
	private void ProcessEddy(float delta)
	{
		eddyLeft -= delta;
		float t = 1f - Mathf.Clamp(eddyLeft / BoltEvolutions.EddySeconds, 0f, 1f);
		float swell = MathF.Min(1f, MathF.Min(t, 1f - t) / 0.15f);
		eddySwell = swell;
		Scale = shotScale * Mathf.Lerp(1f, BoltEvolutions.EddyScale, swell);
		Rotation += ReturnSpinSpeed * 2.2f * delta;

		eddyRecut -= delta;
		if (eddyRecut <= 0f)
		{
			eddyRecut = BoltEvolutions.EddyRecutSeconds;
			struckThisLeg?.Clear();
			float reach = EddyReach();
			Vector2 at = GlobalPosition;
			foreach (Enemy enemy in EnemyRegistry.Snapshot)
			{
				if (IsInstanceValid(enemy) && !enemy.IsDying && at.DistanceSquaredTo(enemy.GlobalPosition) <= reach * reach)
					ResolveImpact(enemy, keepAliveForVisual: false);
			}
		}

		if (eddyLeft <= 0f)
		{
			eddyLeft = 0f;
			eddySwell = 0f;
			Scale = shotScale;
			struckThisLeg?.Clear();
			if (squallPending)
			{
				squallPending = false;
				BurstIntoSquall();
			}
		}
	}

	// The blade's drawn half-length at full swell (the sheet's blade is about 44 px end to end at scale
	// 1), with a body's allowance, as every zone in the game is measured against enemy centres.
	private float EddyReach() => 22f * BoltEvolutions.EddyScale * MathF.Max(0.01f, shotScale.X) * AreaMultiplier + RiptideWave.BodyAllowance;

	/// <summary>While the blade hangs in an Eddy, the reach its recut scans (world px); 0 otherwise. GaleBladeLook draws the wind to it.</summary>
	public float EddyReachNow => eddyLeft > 0f ? EddyReach() : 0f;

	/// <summary>How far a hanging blade has swollen into its eddy, 0..1 (0 when it is not hanging).</summary>
	public float EddySwell => eddyLeft > 0f ? eddySwell : 0f;

	// Thunderhead: lightning out of the sky onto the point the blade turns at.
	private void StrikeThunder()
	{
		if (PlayerRef is not Player player || GetParent() is not Node2D arena)
			return;
		Vector2 at = GlobalPosition;
		float reach = BoltEvolutions.ThunderRadius * AreaMultiplier;
		int hit = Math.Max(1, Mathf.RoundToInt(damage * BoltEvolutions.ThunderShare));
		float critBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.CritChance, CurrentLevel) ?? 0f;
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (!IsInstanceValid(enemy) || enemy.IsDying || at.DistanceSquaredTo(enemy.GlobalPosition) > reach * reach)
				continue;
			player.DealDamageToEnemy(enemy, hit, critBonus, source: SpellData);
			enemy.ApplyShock(BoltEvolutions.ThunderStaggerSeconds);
		}
		ThunderStrike.Spawn(arena, at, reach);
		ImpactDebrisLayer.Spawn(arena, at, reach, ElementColors.GetRamp(Element.Lightning), Colors.White);
	}

	// Squall: the blade becomes five, fanned across the way home, each with its own bend. This blade
	// takes the middle path (or keeps its hook); the four it throws off are new blades that never burst.
	private void BurstIntoSquall()
	{
		if (GetParent() is not Node arena || string.IsNullOrEmpty(SceneFilePath))
			return;
		var packed = ResourceLoader.Load<PackedScene>(SceneFilePath);
		if (packed == null)
			return;
		int blades = Math.Max(1, BoltEvolutions.SquallBlades);
		int share = Math.Max(1, Mathf.RoundToInt(damage * BoltEvolutions.SquallShare));
		float spread = Mathf.DegToRad(BoltEvolutions.SquallSpreadDegrees);
		int middle = blades / 2;
		for (int i = 0; i < blades; i++)
		{
			if (i == middle)
				continue;
			float bend = blades > 1 ? Mathf.Lerp(-spread * 0.5f, spread * 0.5f, i / (float)(blades - 1)) : 0f;
			var child = packed.Instantiate<ElementalBolt>();
			child.SpellData = SpellData;
			child.PlayerRef = PlayerRef;
			child.DamageMultiplier = DamageMultiplier;
			child.AreaMultiplier = AreaMultiplier;
			child.DurationMultiplier = DurationMultiplier;
			child.Generation = Generation + 1;
			child.OverrideDamage = share;
			child.SetSpellLevel(CurrentLevel);
			arena.AddChild(child);
			child.Modulate = Modulate;
			child.Scale = shotScale;
			child.LaunchHomeward(GlobalPosition, bend);
		}
		damage = share;
		// The middle blade comes straight home unless it was hooked.
		if (!hooked)
			returnBend = 0f;
	}

	/// <summary>Starts a returning blade already on its way home from <paramref name="from"/> (a squall's extra blades).</summary>
	public void LaunchHomeward(Vector2 from, float bendRadians)
	{
		ReadEvolutions();
		GlobalPosition = from;
		spawnPosition = from;
		shotScale = Scale;
		lifetime = 0f;
		returnLeg = true;
		struckThisLeg ??= new System.Collections.Generic.HashSet<ulong>();
		struckThisLeg.Clear();
		returnBend = bendRadians;
		Vector2 home = PlayerRef != null && IsInstanceValid(PlayerRef) ? PlayerRef.GlobalPosition - from : Vector2.Right;
		returnStartDistance = MathF.Max(1f, home.Length());
		direction = home.LengthSquared() > 0.01f ? home.Normalized().Rotated(bendRadians) : Vector2.Right;
		Rotation = direction.Angle();
	}

	// Live Wire: lightning from the hand to the blade while it is out. Every tick it cuts whatever the
	// line crosses; every few frames the line is re-jagged, so it crackles rather than hangs.
	private void TickLiveWire(float delta)
	{
		// The wire is drawn over exactly the segment the cut below measures from, every frame; the shader
		// re-jags it inside the band it cuts (live_wire.gdshader).
		if (wireLook != null && PlayerRef != null && IsInstanceValid(PlayerRef))
			wireLook.Span(PlayerRef.GlobalPosition, GlobalPosition);

		wireTick -= delta;
		if (wireTick > 0f)
			return;
		wireTick = BoltEvolutions.LiveWireTickSeconds;
		if (PlayerRef is not Player player || !IsInstanceValid(player))
			return;
		Vector2 a = player.GlobalPosition;
		Vector2 b = GlobalPosition;
		float reach = BoltEvolutions.LiveWireHalfWidth + RiptideWave.BodyAllowance;
		int hit = Math.Max(1, Mathf.RoundToInt(damage * BoltEvolutions.LiveWireShare));
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (!IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			Vector2 p = enemy.GlobalPosition;
			if (DistanceToSegment(p, a, b) <= reach)
				player.DealDamageToEnemy(enemy, hit, source: SpellData);
		}
	}

	private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
	{
		Vector2 ab = b - a;
		float lengthSquared = ab.LengthSquared();
		if (lengthSquared < 0.0001f)
			return p.DistanceTo(a);
		float t = Mathf.Clamp((p - a).Dot(ab) / lengthSquared, 0f, 1f);
		return p.DistanceTo(a + ab * t);
	}

	private void OnAreaEntered(Area2D area) => HandleHit(area);
	private void OnBodyEntered(Node body) => HandleHit(body);

	private void HandleHit(Node enemy)
	{
		ResolveImpact(enemy, keepAliveForVisual: false);
	}

	private void ResolveImpact(Node enemy, bool keepAliveForVisual)
	{
		if (!enemy.IsInGroup("enemies") || !enemy.HasMethod("TakeDamage"))
			return;
		// A forged shard waiting in its ring, or a core already lodged, strikes nothing more.
		if (forging || lodged)
			return;

		if (Returning)
		{
			// Once per enemy per leg, however often its body re-enters the blade on the same pass.
			if (struckThisLeg != null && !struckThisLeg.Add(enemy.GetInstanceId()))
				return;
		}
		else if (knockbackSpeed * KnockbackScale > 0f)
		{
			// A body thrown ahead of the bolt that threw it is not struck again when the bolt catches it.
			struckOnce ??= new System.Collections.Generic.HashSet<ulong>();
			if (!struckOnce.Add(enemy.GetInstanceId()))
				return;
		}

		// Clear homing target so if bolt pierces it flies straight instead of spinning around hit enemy
		target = null;

		var player = PlayerRef as Player;
		float critBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.CritChance, CurrentLevel) ?? 0f;
		player?.DealDamageToEnemy(enemy, damage, critBonus, source: SpellData);
		ApplyGuaranteedEffects(enemy);
		if (enemy is Node2D struckNode)
			Struck?.Invoke(struckNode);

		// Handle AoE on impact (e.g. Frost Shard shatter)
		if (AoEOnImpact)
		{
			float effectiveAoERadius = MathF.Max(10f, AoERadius * AreaMultiplier);
			foreach (var node in EnemyRegistry.Snapshot)
			{
				if (node != enemy && node is Node2D nearby2D && IsInstanceValid(nearby2D) && GlobalPosition.DistanceTo(nearby2D.GlobalPosition) <= effectiveAoERadius)
				{
					if (player != null)
						player.DealDamageToEnemy(nearby2D, Math.Max(1, Mathf.RoundToInt(damage * 0.75f)), critBonus);
					else if (nearby2D.HasMethod("TakeDamage"))
						// Two arguments - see the note in KidDog.TickBite. A one-argument Call
						// here failed silently on the no-player path.
						nearby2D.Call("TakeDamage", Math.Max(1, Mathf.RoundToInt(damage * 0.75f)), false);

					if (GuaranteedSlow && nearby2D.HasMethod("ApplySlow"))
						nearby2D.Call("ApplySlow", scaledSlowMultiplier, scaledSlowDuration);
					if (GuaranteedFreeze && nearby2D.HasMethod("ApplySlow"))
						nearby2D.Call("ApplySlow", 0.0f, FreezeDuration);
				}
			}
		}

		if (ChainToSecondTarget && player != null && enemy is Node2D hitNode2D)
		{
			ScheduleChainJump(hitNode2D, player);
			if (!keepAliveForVisual)
				QueueFree();
			return;
		}

		// A returning blade is spent by being caught, never by what it cuts.
		if (Returning)
			return;

		// Molten Shard's first hit, when a card makes it more than a hit. Magma Core lodges it in the
		// target; Splitting Shard breaks it in two; Caldera drives it into the ground. Each ends the
		// shard's flight here, so none of them pierces.
		if (!keepAliveForVisual && enemy is Node2D firstStruck)
		{
			if (magmaCore)
			{
				Lodge(firstStruck);
				return;
			}
			if (splitOnHit)
			{
				// Everything the halves need, taken now: the shard itself is freed this frame.
				var split = new SplitOrder(GlobalPosition, enemy.GetInstanceId(), direction, GetParent(), SceneFilePath,
					SpellData, PlayerRef, CurrentLevel, DamageMultiplier, AreaMultiplier, DurationMultiplier,
					Math.Max(1, Mathf.RoundToInt(damage * BoltEvolutions.SplitShare)), Generation + 1, shotScale, Modulate);
				Callable.From(() => SplitAt(split)).CallDeferred();
				QueueFree();
				return;
			}
			if (caldera)
			{
				OpenCaldera(GlobalPosition, damage);
				QueueFree();
				return;
			}
		}

		pierceCount++;
		if (pierceCount > pierce && !keepAliveForVisual)
		{
			// Barbed: the arrow that stops in an enemy stays in it, and bleeds it.
			if (barbed && enemy is Enemy host && player != null)
				LodgedArrows.Lodge(GetParent() as Node2D, host, direction, damage, SpellData, player);
			QueueFree();
		}
		if (keepAliveForVisual)
			duration = MathF.Max(duration, InstantBoltLifetime);
	}

	// --- Molten Shard -------------------------------------------------------------------------------

	// Magma Core: the shard stays in what it struck, burning brighter, and erupts a moment later. A host
	// that dies first leaves the core where it fell, still counting down.
	private void Lodge(Node2D host)
	{
		lodged = true;
		lodgeHost = host;
		lodgeOffset = (GlobalPosition - host.GlobalPosition).LimitLength(10f);
		lodgeAt = GlobalPosition;
		lodgeLeft = BoltEvolutions.MagmaFuseSeconds;
		SetDeferred(Area2D.PropertyName.Monitoring, false);
	}

	private void ProcessLodged(float delta)
	{
		if (lodgeHost != null && IsInstanceValid(lodgeHost) && !(lodgeHost is Enemy dying && dying.IsDying))
			lodgeAt = lodgeHost.GlobalPosition + lodgeOffset;
		GlobalPosition = lodgeAt;
		lodgeLeft -= delta;
		// It swells and throbs faster as the fuse runs down - the tell that it is about to go.
		float fuse = 1f - Mathf.Clamp(lodgeLeft / BoltEvolutions.MagmaFuseSeconds, 0f, 1f);
		float throb = 0.5f + 0.5f * MathF.Sin(fuse * fuse * 40f);
		Scale = shotScale * (1f + 0.35f * fuse + 0.15f * throb);
		if (lodgeLeft <= 0f)
		{
			Erupt();
			QueueFree();
		}
	}

	private void Erupt()
	{
		if (PlayerRef is not Player player || GetParent() is not Node2D arena)
			return;
		Vector2 at = GlobalPosition;
		float reach = BoltEvolutions.MagmaRadius * AreaMultiplier;
		int hit = Math.Max(1, Mathf.RoundToInt(damage * BoltEvolutions.MagmaShare));
		float critBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.CritChance, CurrentLevel) ?? 0f;
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (!IsInstanceValid(enemy) || enemy.IsDying || at.DistanceSquaredTo(enemy.GlobalPosition) > reach * reach)
				continue;
			player.DealDamageToEnemy(enemy, hit, critBonus, source: SpellData);
			if (WitherFraction > 0f)
				enemy.ApplyWither(WitherFraction);
		}
		// White, not the bolt's modulate: the shard paints itself and no card tint reaches it
		// (MoltenShardLook), so its eruption's debris is the Fire ramp as drawn, too.
		ImpactDebrisLayer.Spawn(arena, at, reach, ElementColors.GetRamp(Element.Fire), Colors.White);
		if (caldera)
			OpenCaldera(at, damage);
	}

	// Caldera: a pool of lava wells up where the shard went into the ground.
	private void OpenCaldera(Vector2 at, int hit)
	{
		if (PlayerRef is not Player player || GetParent() is not Node arena)
			return;
		MoltenSlag.Pool(arena, at, BoltEvolutions.CalderaRadius * AreaMultiplier,
			Math.Max(1, Mathf.RoundToInt(hit * BoltEvolutions.CalderaTickShare)), SpellData, player);
	}

	// Splitting Shard: two halves out of the split, each homing on the nearest enemy it was not, a
	// smaller shard for a smaller share. Deferred: it runs from a physics callback, which may not add
	// an area to the world.
	// What a split needs of the shard that split, copied out at the hit: the shard is freed that frame,
	// before the deferred split runs.
	private readonly record struct SplitOrder(Vector2 At, ulong StruckId, Vector2 Heading, Node Arena, string ScenePath,
		SpellData Spell, Node2D Thrower, int Level, float DamageMultiplier, float AreaMultiplier, float DurationMultiplier,
		int Share, int Generation, Vector2 Scale, Color Tint);

	private static void SplitAt(SplitOrder order)
	{
		if (order.Thrower == null || !IsInstanceValid(order.Thrower) || order.Arena == null || !IsInstanceValid(order.Arena)
			|| string.IsNullOrEmpty(order.ScenePath))
			return;
		var packed = ResourceLoader.Load<PackedScene>(order.ScenePath);
		if (packed == null)
			return;

		// The two nearest the split, the struck one aside: no list, no sort.
		Enemy first = null, second = null;
		float firstD = BoltEvolutions.SplitReach * BoltEvolutions.SplitReach, secondD = firstD;
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (!IsInstanceValid(enemy) || enemy.IsDying || enemy.GetInstanceId() == order.StruckId)
				continue;
			float d = order.At.DistanceSquaredTo(enemy.GlobalPosition);
			if (d < firstD) { second = first; secondD = firstD; first = enemy; firstD = d; }
			else if (d < secondD) { second = enemy; secondD = d; }
		}

		for (int i = 0; i < 2; i++)
		{
			Enemy goal = i == 0 ? first : second ?? first;
			var half = packed.Instantiate<ElementalBolt>();
			half.SpellData = order.Spell;
			half.PlayerRef = order.Thrower;
			half.DamageMultiplier = order.DamageMultiplier;
			half.AreaMultiplier = order.AreaMultiplier;
			half.DurationMultiplier = order.DurationMultiplier;
			half.Generation = order.Generation;
			half.OverrideDamage = order.Share;
			half.BasePierce = BoltEvolutions.SplitPierce;
			half.SetSpellLevel(order.Level);
			order.Arena.AddChild(half);
			half.Modulate = order.Tint;
			half.Scale = order.Scale * BoltEvolutions.SplitScale;
			// The halves leave the split apart: each turns onto its own target from a fork (they home),
			// or, with nothing near, flies on to either side of the line the shard was on. The halves
			// pierce SplitPierce on their own - the level's pierce does not carry into them.
			Vector2 fork = order.Heading.Rotated(i == 0 ? -0.6f : 0.6f);
			half.Shoot(order.At, order.At + fork * 40f, goal);
		}
	}

	/// <summary>
	/// Forge Swarm: holds this shard in the ring round <paramref name="centre"/> at <paramref name="slot"/>
	/// of <paramref name="slots"/>, inert, until <see cref="LaunchFromForge"/>.
	/// </summary>
	public void BeginForging(Node2D centre, int slot, int slots)
	{
		forging = true;
		forgeCentre = centre;
		forgeSlot = slot;
		forgeSlots = Math.Max(1, slots);
		Monitoring = false;
		shotScale = Scale;
		ProcessForging();
	}

	public bool IsForging => forging;

	/// <summary>Magma Core: the shard is lodged in what it struck, its fuse burning (MoltenShardLook heats it).</summary>
	public bool IsLodged => lodged;

	/// <summary>How far a lodged core's fuse has burnt: 0 just lodged .. 1 erupting.</summary>
	public float LodgeFuse => lodged ? 1f - Mathf.Clamp(lodgeLeft / BoltEvolutions.MagmaFuseSeconds, 0f, 1f) : 0f;

	private void ProcessForging()
	{
		// A ring that is never completed - the spell taken out of the loadout mid-forge - lets its
		// shards go out rather than circle the wizard for the rest of the run.
		forgeAge += (float)GetProcessDeltaTime();
		if (forgeCentre == null || !IsInstanceValid(forgeCentre) || forgeAge > ForgeAbandonSeconds)
		{
			QueueFree();
			return;
		}
		// One clock for the whole ring, so the shards keep their spacing as it turns.
		float phase = (float)(Time.GetTicksMsec() * 0.001) * BoltEvolutions.ForgeSpin + Mathf.Tau * forgeSlot / forgeSlots;
		Vector2 offset = new Vector2(MathF.Cos(phase), MathF.Sin(phase)) * BoltEvolutions.ForgeOrbitRadius;
		GlobalPosition = forgeCentre.GlobalPosition + offset;
		// Point out from the wizard: a crown of blades, each already aimed outward to go.
		Rotation = phase;
	}

	/// <summary>Releases a forged shard at <paramref name="goal"/> from wherever it is in the ring.</summary>
	public void LaunchFromForge(Node2D goal)
	{
		forging = false;
		Monitoring = true;
		Vector2 from = GlobalPosition;
		Vector2 to = goal != null && IsInstanceValid(goal) ? goal.GlobalPosition : from + Vector2.Right.Rotated(Rotation) * 100f;
		Shoot(from, to, goal);
	}

	// Spawn a brand new bolt at the impact point aimed straight at the next unvisited enemy after a
	// short beat. We capture plain values (not this) because the current node is freed immediately.
	private void ScheduleChainJump(Node2D hitNode, Player player)
	{
		ChainVisited ??= new System.Collections.Generic.HashSet<Node2D>();
		ChainVisited.Add(hitNode);

		if (ChainBouncesRemaining < 0)
			ChainBouncesRemaining = Math.Max(1, MaxChainBounces);
		if (ChainBouncesRemaining <= 0)
			return;

		string scenePath = SceneFilePath;
		if (string.IsNullOrEmpty(scenePath))
			return;

		Node parent = GetParent();
		if (parent == null || !IsInstanceValid(parent))
			return;

		Vector2 from = hitNode.GlobalPosition;
		float chainRadius = ChainRadius;
		var visited = ChainVisited;
		int remaining = ChainBouncesRemaining - 1;
		int nextDamage = Math.Max(1, Mathf.RoundToInt(damage * ChainDamageMultiplier));
		SpellData spell = SpellData;
		int level = CurrentLevel;
		float dmgMult = DamageMultiplier;
		float areaMult = AreaMultiplier;
		float durMult = DurationMultiplier;
		Node2D playerNode = player;

		var timer = GetTree().CreateTimer(ChainRespawnDelay);
		timer.Timeout += () =>
		{
			if (!IsInstanceValid(parent))
				return;

			Node2D next = EnemyRegistry.Snapshot
				.OfType<Node2D>()
				.Where(e => e != null && IsInstanceValid(e) && e.HasMethod("TakeDamage") && !visited.Contains(e) && from.DistanceTo(e.GlobalPosition) <= chainRadius)
				.OrderBy(e => from.DistanceTo(e.GlobalPosition))
				.FirstOrDefault();
			if (next == null)
				return;

			var packed = ResourceLoader.Load<PackedScene>(scenePath);
			if (packed == null)
				return;

			var boltNode = packed.Instantiate<Area2D>();
			if (boltNode is ElementalBolt bolt)
			{
				bolt.SpellData = spell;
				bolt.PlayerRef = playerNode;
				bolt.DamageMultiplier = dmgMult;
				bolt.AreaMultiplier = areaMult;
				bolt.DurationMultiplier = durMult;
				bolt.ChainVisited = visited;
				bolt.ChainBouncesRemaining = remaining;
				bolt.OverrideDamage = nextDamage;
				bolt.SetSpellLevel(level);
			}
			parent.AddChild(boltNode);
			(boltNode as ElementalBolt)?.Shoot(from, next.GlobalPosition, next);
		};
	}

	private void ApplyGuaranteedEffects(Node enemy)
	{
		if (GuaranteedSlow && enemy.HasMethod("ApplySlow"))
			enemy.Call("ApplySlow", scaledSlowMultiplier, scaledSlowDuration);
		if (GuaranteedFreeze && enemy.HasMethod("ApplySlow"))
			enemy.Call("ApplySlow", 0.0f, FreezeDuration);
		if (GuaranteedPoison && enemy.HasMethod("ApplyPoison"))
			enemy.Call("ApplyPoison", scaledPoisonTick, scaledPoisonDuration);
		// Typed rather than duck-typed: ApplyWither has one argument and no defaults, but a typed call
		// cannot drift out of step with the signature the way a Call() string can.
		if (WitherFraction > 0f && enemy is Enemy withering)
			withering.ApplyWither(WitherFraction);
		if (enemy is Enemy struck)
		{
			// Breaker and Heavy Draw: thrown along the line the bolt was travelling. The stagger and the
			// shove together, as Solar Flare's Blinding Flash gives them - it reels, then goes.
			float shove = knockbackSpeed * KnockbackScale;
			if (shove > 0f && direction != Vector2.Zero)
				struck.ApplyKnockback(direction * shove);
			if (StaggerSeconds > 0f)
				struck.ApplyShock(StaggerSeconds);
		}
	}

	private void ConfigureArcVisual(float visualLength)
	{
		if (boltLine == null)
			return;

		arcBendCount = (int)(GD.Randi() % 3) + 3;
		arcOffsets = new float[arcBendCount];
		float maxWobble = MathF.Min(18f, visualLength * 0.35f);
		for (int i = 0; i < arcBendCount; i++)
			arcOffsets[i] = (float)GD.RandRange(-maxWobble, maxWobble);
	}

	private void UpdateArcVisual()
	{
		if (boltLine == null)
			return;

		Vector2 from = Vector2.Left * (ArcVisualLength * 0.5f);
		Vector2 to = Vector2.Right * (ArcVisualLength * 0.5f);
		if (instantBoltActive)
		{
			from = instantBoltFromLocal;
			to = instantBoltToLocal;
		}
		Vector2 normal = Vector2.Up;
		var points = new System.Collections.Generic.List<Vector2> { from };
		for (int i = 1; i <= arcBendCount; i++)
		{
			float t = i / (float)(arcBendCount + 1);
			Vector2 basePoint = from.Lerp(to, t);
			points.Add(basePoint + normal * arcOffsets[i - 1]);
		}
		points.Add(to);

		boltLine.Width = MathF.Max(2f, 2.5f + areaRadius * 0.18f);
		float alpha = instantBoltActive ? instantBoltFadeAlpha : 0.95f;
		boltLine.DefaultColor = new Color(0.42f, 0.08f, 0.58f, alpha);
		boltLine.Points = points.ToArray();
		boltLine.Visible = alpha > 0.01f;
	}

	private void RefreshComputedStats()
	{
		damage = OverrideDamage >= 0
			? OverrideDamage
			: Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 1) * DamageMultiplier));
		range = SpellData?.GetRangeAtLevel(CurrentLevel) ?? 500f;
		// The evolutions' SpeedMultiplier as well as the level table's flat ProjectileSpeed: Hyper
		// Acceleration promised "+25% projectile speed" to every bolt here and nothing read it.
		speed = MathF.Max(1f, (BaseSpeed + (SpellData?.GetEffectValueAtLevel(SpellEffect.ProjectileSpeed, CurrentLevel) ?? 0f))
			* (SpellData?.GetSpeedMultiplier() ?? 1f));
		duration = MathF.Max(0f, BaseDuration * DurationMultiplier);
		// GetPierceAtLevel, not the raw Pierce effect: the effect alone left out every evolution's
		// PierceBonus, so a mutation promising pierce on Riptide or Molten Shard paid nothing (2026-10-22).
		// A split shard's half pierces only its own BasePierce: the level's carries no further.
		pierce = Generation > 0 && !Returning
			? Math.Max(0, BasePierce)
			: Math.Max(0, BasePierce + (SpellData?.GetPierceAtLevel(CurrentLevel) ?? 0));
		areaRadius = MathF.Max(2f, (BaseHitRadius + (SpellData?.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel) ?? 0f)) * AreaMultiplier);
		// A wave is spent by what it breaks over, and a wider wave breaks over more: a crest's pierce
		// grows with its width (2026-10-27). Riptide's 6 was set for a 40 px wave; a level 8 wave is
		// 224 px across and meets a whole rank at once, so on 6 it would be gone within a body of
		// reaching the pack and the swath it grew into would never sweep anything (75 at level 8).
		if (CrestDepthFraction > 0f)
			pierce = Mathf.RoundToInt(pierce * MathF.Max(1f, areaRadius / BaseHitRadius));
		// The milestones' KnockbackBonus, through the one conversion every spell's shove goes through.
		knockbackSpeed = SpellData?.GetKnockbackSpeedAtLevel(CurrentLevel) ?? 0f;

		float slowPower = SpellData?.GetEffectValueAtLevel(SpellEffect.SlowPower, CurrentLevel) ?? 0f;
		float slowDurationBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.SlowDuration, CurrentLevel) ?? 0f;
		scaledSlowMultiplier = Mathf.Clamp(SlowMultiplier - slowPower, 0f, 0.98f);
		scaledSlowDuration = MathF.Max(0.1f, SlowDuration + slowDurationBonus);

		int poisonTickBonus = Mathf.RoundToInt(SpellData?.GetEffectValueAtLevel(SpellEffect.DotDamage, CurrentLevel) ?? 0f);
		float poisonDurationBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.ZoneDuration, CurrentLevel) ?? 0f;
		scaledPoisonTick = Math.Max(1, PoisonDamagePerTick + poisonTickBonus);
		scaledPoisonDuration = MathF.Max(0.1f, PoisonDuration + poisonDurationBonus);
	}
}
