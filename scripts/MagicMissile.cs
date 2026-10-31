using Godot;
using System;
using System.Collections.Generic;


namespace WizardSurvivors.scripts;

public partial class MagicMissile : Area2D
{
	// MAGIC MISSILE'S CARDS, EACH ITS OWN (.ai/spell-roster.md section 2h, phase 2, 2026-10-29). Two of the
	// five were numbers on a dart - +3 pierce, and +2 darts with a shorter cooldown - and the third pulled,
	// which is Hollow Star's verb. Each now changes what the dart does where the player can see it, out of
	// what it is: a homing violet dart that never misses. The ids are the ones they always were, so every
	// save's choices and the Ascension browser carry over; the behaviour is switched on by id
	// (SpellData.HasEvolution, which reads the level 4, level 8 and Apotheosis selections).

	/// <summary>Twin Volley: the dart forks on its first hit into two shards flung out to either side.</summary>
	public const string TwinVolleyId = "magic_missile_twin_volley";

	/// <summary>
	/// Threaded Needle: after a strike the dart turns on the nearest enemy it has not struck and flies on,
	/// stitching a thread of stardust through the pack.
	/// </summary>
	public const string ThreadedNeedleId = "magic_missile_piercing_needle";
	/// <summary>How many times a dart turns on a new enemy after its first strike.</summary>
	public const int ThreadStrikes = 3;
	/// <summary>How far from the dart the next enemy may be for it to turn on it.</summary>
	public const float ThreadReach = 220f;
	/// <summary>How long a stitch of the thread hangs in the air after the dart has passed.</summary>
	public const float ThreadFadeSeconds = 0.45f;
	// A threaded dart turns hard: it has to come round onto an enemy beside or behind it.
	private const float ThreadTurnSpeed = 14f;

	/// <summary>Unstable Detonation: the dart bursts where it strikes (Effect ExplosionOnHit).</summary>
	public const string UnstableId = "magic_missile_unstable_shard";

	/// <summary>
	/// Overcharge: every dart leaves a spark of static on what it strikes, and the last spark overloads it
	/// in a burst of lightning (StaticCharges).
	/// </summary>
	public const string OverchargeId = "magic_missile_gatling_barrage";
	public const int OverchargeSparks = 3;
	/// <summary>The burst's reach, before the spell's Area.</summary>
	public const float OverchargeRadius = 64f;
	/// <summary>Of a dart's hit, to everything the burst reaches - the overloaded enemy included.</summary>
	public const float OverchargeShare = 3f;
	/// <summary>How long a spark holds on an enemy that no other dart reaches.</summary>
	public const float SparkSeconds = 2.5f;

	/// <summary>
	/// Arcane Orb: each dart is loosed as a great slow orb that grinds through everything in its path
	/// and bursts where its flight ends.
	/// </summary>
	public const string ArcaneOrbId = "magic_missile_singularity_core";
	/// <summary>What the orb grinds, before the spell's and the player's Area.</summary>
	public const float OrbRadius = 26f;
	/// <summary>Of the dart's own speed.</summary>
	public const float OrbSpeedShare = 0.32f;
	public const float OrbSeconds = 1.8f;
	/// <summary>The card's cooldown: an orb is loosed less often than a dart.</summary>
	public const float OrbCooldown = 1.5f;
	/// <summary>The card's damage: every grind and the burst are a heavier dart's.</summary>
	public const float OrbDamage = 1.5f;
	/// <summary>How often the orb strikes everything inside it.</summary>
	public const float OrbTickSeconds = 0.2f;
	/// <summary>The burst where the orb's flight ends, before Area.</summary>
	public const float OrbBurstRadius = 72f;
	/// <summary>Of a dart's hit, to everything the end burst reaches.</summary>
	public const float OrbBurstShare = 2f;
	/// <summary>With Unstable Detonation the orb's end burst reaches this much further.</summary>
	public const float OrbUnstableReach = 1.3f;
	// A body is a body, not a point.
	private const float BodyAllowance = 8f;

	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float BaseSpeed { get; set; } = 400f;
	[Export] public float BaseDuration { get; set; } = 5.0f;
	[Export] public float BaseArea { get; set; } = 16.0f;
	[Export] public int BasePierce { get; set; } = 0;
	// How far a Twin Volley shard flies before it expires. A shard is aimed at open ground
	// rather than at an enemy, so nothing else would ever stop one - without this a fork in an
	// empty corner sends two projectiles across the whole arena. Kept short so the fork reads as
	// a burst at the impact point.
	[Export] public float SplitTravelDistance { get; set; } = 260f;
	// Distance this projectile may travel from its spawn before expiring. 0 = unlimited, which is
	// every missile the player fires; only Twin Volley shards set it.
	public float MaxTravelDistance { get; set; } = 0f;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float DurationMultiplier { get; set; } = 1.0f;
	// 0 for a missile the player fired, 1 for a Twin Volley shard. Shards never fork again -
	// without this a dense pack would cascade until the frame budget died.
	public int SplitGeneration { get; set; } = 0;
	// The enemy a Twin Volley shard is forbidden to damage: the one its parent just hit. A
	// shard is born overlapping that body, so without this it would collide on its first frame
	// and spend itself on the original target instead of ever leaving the impact point.
	public Node SplitIgnoreTarget { get; set; } = null;
	public Node2D PlayerRef;

	private Weapon weapon;
	public Weapon Weapon
	{
		get => weapon;
		set
		{
			weapon = value;
			ApplyLegacyWeapon(value);
		}
	}

	private int damage = 1;
	private float range = 500f;
	private float speed = 400f;
	private float duration = 5.0f;
	private int pierce = 0;
	private float areaRadius = 16.0f;

	private Vector2 direction = Vector2.Zero;
	private Node target = null;
	private float turnSpeed = 6.0f;
	private float lifetime = 0f;
	private int pierceCount = 0;
	private Vector2 spawnPosition = Vector2.Zero;
	private bool hasSplit = false;

	// What this dart is, read once when it is shot (its milestones cannot change in flight).
	private bool threaded;
	private bool orb;
	private bool overcharged;
	private int threadsUsed;
	// Every enemy this dart has struck, so a threaded dart that loops back through the pack never
	// strikes one twice. Fixed: a dart strikes at most 1 + pierce + ThreadStrikes.
	private readonly Node[] struck = new Node[12];
	private int struckCount;
	// A threaded dart whose last strike is spent stays a moment, unseen and unhitting, so its thread
	// can fade where it was stitched rather than vanish with it.
	private float endingSeconds = -1f;
	private ThreadOfStardust thread;
	// The orb's clock and what it has hit (the first contact is where Twin Volley forks it).
	private float orbTick;
	private bool orbTouched;

	private static readonly List<Enemy> scratch = new(64);
	private static readonly ElementColors.Ramp ArcaneRamp = ElementColors.GetRamp(Element.Arcane);

	// THE DART AND THE ORB ARE ONE SHADER ON ONE QUAD (scenes/shaders/magic_missile.gdshader, the pixel-art
	// pass, 2026-10-29). The dart was a sprite at half its authored scale, turned to its heading - art
	// pixels half the world's on a rotated grid - under a spray of translucent GPU particles, and the orb
	// translucent DrawCircles under an antialiased DrawArc.
	private static Shader missileShader;
	private static readonly StringName ParamMode = "mode";
	private static readonly StringName ParamSize = "size";
	private static readonly StringName ParamTime = "time";
	private static readonly StringName ParamSeed = "seed";
	private static readonly StringName ParamOrbRadius = "orb_radius";
	private static readonly StringName ParamOrbEnd = "orb_end";
	private static readonly StringName ParamGilded = "gilded";
	private ShaderMaterial material;
	// The scene's own scale (the dart is drawn at the size it always was on screen); an evolution's
	// scale on top of it grows the dart.
	private float sceneScale = 1f;
	// The dart's extent in its own frame, in world units at size 1: the tail's spray behind, the tip ahead.
	private const float DartBack = 32f;
	private const float DartAhead = 15f;
	private const float DartHalf = 8f;

	public override void _Ready()
	{
		// A bolt in the air: over the bodies it flies among, not sorted in with them by its origin
		// (DrawLayers).
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		RefreshComputedStats();

		sceneScale = MathF.Max(0.01f, MathF.Abs(Scale.X));
		missileShader ??= GD.Load<Shader>("res://scenes/shaders/magic_missile.gdshader");
		material = new ShaderMaterial { Shader = missileShader };
		Material = material;
		material.SetShaderParameter(ParamSeed, (GetInstanceId() % 997) * 0.731f);
		var cs = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (cs != null) cs.Disabled = false;
		var shape = cs?.Shape as CircleShape2D;
		if (shape != null) shape.Radius = areaRadius;
		Connect("area_entered", new Callable(this, nameof(OnAreaEntered)));
		Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	public void Shoot(Vector2 from, Vector2 to, Node enemyTarget = null)
	{
		GlobalPosition = from;
		direction = (to - from).Normalized();
		Rotation = direction.Angle();
		// Only lock onto a target if it is within effective range from the firing position.
		spawnPosition = from;
		if (enemyTarget is Node2D enemyNode)
		{
			float distToEnemy = (enemyNode.GlobalPosition - from).Length();
			if (distToEnemy <= range)
				target = enemyTarget;
			else
				target = null; // out of range, don't home
		}
		else
		{
			target = null;
		}
		lifetime = 0f;
		pierceCount = 0;
		hasSplit = false;
		struckCount = 0;
		threadsUsed = 0;

		// A Twin Volley shard is a plain dart: it never threads, never swells into an orb.
		bool fired = SplitGeneration == 0 && SpellData != null;
		threaded = fired && SpellData.HasEvolution(ThreadedNeedleId);
		orb = fired && SpellData.HasEvolution(ArcaneOrbId);
		overcharged = SpellData != null && SpellData.HasEvolution(OverchargeId);
		material?.SetShaderParameter(ParamGilded, SpellData?.IsLegendary ?? false);
		material?.SetShaderParameter(ParamSize, MathF.Abs(Scale.X) / sceneScale);
		if (orb)
			BecomeOrb();
		QueueRedraw();
	}

	// The orb draws itself out of the radius it grinds, so the scene's scale (and any evolution's) is
	// dropped; the dart's sprite and spray are hidden behind it. It strikes by its own clock, never by
	// the physics overlap the dart uses.
	private void BecomeOrb()
	{
		Scale = Vector2.One;
		// Lit from the upper left wherever it rolls: the sphere does not turn with its heading.
		Rotation = 0f;
		speed *= OrbSpeedShare;
		// It rolls straight on through where its enemy stood, never homing: an orb that followed its
		// enemy settled on it beside the wizard, and a cast or two later he was buried under orbs.
		target = null;
		// Loosed from in front of him, not out of his chest.
		GlobalPosition += direction * (OrbHitRadius + 6f);
		material?.SetShaderParameter(ParamMode, 1);
		material?.SetShaderParameter(ParamOrbRadius, OrbHitRadius);
		orbTick = 0f;
		orbTouched = false;
		QueueRedraw();
	}

	private float OrbHitRadius => OrbRadius * AreaMultiplier * (SpellData?.GetAreaMultiplierAtLevel(CurrentLevel) ?? 1f);

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		material?.SetShaderParameter(ParamTime, lifetime);
		if (endingSeconds >= 0f)
		{
			endingSeconds -= dt;
			thread?.Tick(GlobalPosition, false);
			if (endingSeconds < 0f)
				QueueFree();
			return;
		}

		if (target != null && IsInstanceValid(target))
		{
			if (target is not Node2D targetNode) return;
			var toTarget = targetNode.GlobalPosition - GlobalPosition;
			if (toTarget.Length() > 0)
			{
				var tdir = toTarget.Normalized();
				var alpha = MathF.Min(1f, turnSpeed * dt);
				direction = (direction * (1f - alpha) + tdir * alpha).Normalized();
				if (!orb)
					Rotation = direction.Angle();
			}
		}

		// If missile has travelled beyond its range from spawn, drop any target lock.
		float travelled = (GlobalPosition - spawnPosition).Length();
		if (travelled > range)
		{
			target = null;
		}

		if (MaxTravelDistance > 0f && travelled > MaxTravelDistance)
		{
			QueueFree();
			return;
		}

		Position += direction * speed * dt;
		lifetime += dt;
		if (orb)
		{
			thread?.Tick(GlobalPosition, true);
			TickOrb(dt);
			return;
		}
		thread?.Tick(GlobalPosition, true);
		if (duration > 0 && lifetime > duration) QueueFree();
	}

	private void OnAreaEntered(Area2D area)
	{
		if (area == SplitIgnoreTarget || orb || endingSeconds >= 0f)
			return;
		if (area.IsInGroup("enemies") && area.HasMethod("TakeDamage"))
			Strike(area);
	}

	private void OnBodyEntered(Node body)
	{
		if (body == SplitIgnoreTarget || orb || endingSeconds >= 0f)
			return;
		if (body.IsInGroup("enemies") && body.HasMethod("TakeDamage"))
			Strike(body);
	}

	private void Strike(Node enemy)
	{
		if (HasStruck(enemy))
			return;
		if (struckCount < struck.Length)
			struck[struckCount++] = enemy;
		(PlayerRef as Player)?.DealDamageToEnemy(enemy, damage, CritBonus, source: SpellData);
		TriggerOnHitEffects(enemy);
		pierceCount++;

		// Threaded Needle: the strike is not the end of it. The dart turns on the nearest enemy it has
		// not struck and flies on, its range counted again from here; only when no thread is left, or
		// nothing is near enough to turn on, does it fall back to its pierce and end.
		if (threaded && threadsUsed < ThreadStrikes && enemy is Node2D from && Rethread(from.GlobalPosition))
		{
			threadsUsed++;
			return;
		}
		if (pierceCount > pierce)
			End();
	}

	private bool HasStruck(Node enemy)
	{
		for (int i = 0; i < struckCount; i++)
		{
			if (struck[i] == enemy)
				return true;
		}
		return false;
	}

	private bool Rethread(Vector2 at)
	{
		Enemy next = null;
		float best = ThreadReach * ThreadReach;
		EnemyRegistry.CollectNear(at, ThreadReach, scratch);
		foreach (Enemy candidate in scratch)
		{
			if (!IsInstanceValid(candidate) || candidate.IsDying || HasStruck(candidate))
				continue;
			float d = at.DistanceSquaredTo(candidate.GlobalPosition);
			if (d < best)
			{
				best = d;
				next = candidate;
			}
		}
		scratch.Clear();
		if (next == null)
			return false;
		target = next;
		spawnPosition = GlobalPosition;
		lifetime = 0f;
		turnSpeed = ThreadTurnSpeed;
		if (thread == null)
		{
			thread = new ThreadOfStardust();
			AddChild(thread);
		}
		thread.Stitch(at);
		return true;
	}

	// The dart's last strike is spent. A dart with a thread stays where it is, unseen and harmless, for
	// as long as its last stitch takes to fade.
	private void End()
	{
		if (thread == null)
		{
			QueueFree();
			return;
		}
		endingSeconds = ThreadFadeSeconds;
		target = null;
		speed = 0f;
		SetDeferred(Area2D.PropertyName.Monitoring, false);
		// Unseen: the dart draws nothing more (its thread is its own node, drawn on).
		QueueRedraw();
		thread.Stitch(GlobalPosition);
	}

	// The Crit tag's level line (Effect CritChance), shown on the level-up card and passed to no hit
	// until 2026-10-26.
	private float CritBonus => SpellData?.GetEffectValueAtLevel(SpellEffect.CritChance, CurrentLevel) ?? 0f;

	private void TriggerOnHitEffects(Node hitTarget)
	{
		if (SpellData == null)
			return;

		TrySplitOnHit(hitTarget);
		if (overcharged && hitTarget is Enemy charged)
			StaticCharges.For(GetParent(), SpellData, PlayerRef as Player)?.AddSpark(charged,
				Math.Max(1, Mathf.RoundToInt(damage * OverchargeShare)),
				OverchargeRadius * AreaMultiplier * SpellData.GetAreaMultiplierAtLevel(CurrentLevel));

		if (SpellData.HasEffectFlag(SpellEffect.ExplosionOnHit))
		{
			// Unstable Detonation: a 60 px splash (more with area) for the card's EffectValue as a percentage
			// of the hit. It was areaRadius x2 (about 42 px) for a flat half, with the 60 read only as
			// on/off, and the splash passed no source, so no rider (2026-10-26). It drew nothing until phase
			// 2 (2026-10-29): now the burst throws arcane sparks out to exactly the reach it tested.
			float splashRadius = MathF.Max(SplashBaseRadius, areaRadius * 2.0f);
			float share = SpellData.GetEffectValueAtLevel(SpellEffect.ExplosionOnHit, CurrentLevel) / 100f;
			int splashDamage = Math.Max(1, Mathf.RoundToInt(damage * (share > 0f ? share : 0.5f)));
			var enemies = EnemyRegistry.Snapshot;
			foreach (var e in enemies)
			{
				if (e is Node2D n2d && n2d != hitTarget && GlobalPosition.DistanceTo(n2d.GlobalPosition) <= splashRadius)
				{
					(PlayerRef as Player)?.DealDamageToEnemy(n2d, splashDamage, source: SpellData);
				}
			}
			if (GetParent() is Node2D arena)
				ImpactDebrisLayer.Spawn(arena, GlobalPosition, splashRadius, ArcaneRamp, Colors.White);
		}
	}

	private const float SplashBaseRadius = 60f;

	// --- Arcane Orb ---------------------------------------------------------------------------------

	private void TickOrb(float dt)
	{
		orbTick -= dt;
		if (orbTick <= 0f)
		{
			orbTick = OrbTickSeconds;
			Grind();
		}
		material?.SetShaderParameter(ParamOrbEnd, Mathf.Clamp((lifetime / OrbSeconds - 0.8f) / 0.2f, 0f, 1f));
		if (lifetime >= OrbSeconds)
			OrbBurst();
	}

	// Everything inside the orb, struck once a tick: what it rolls over is ground down while it is in
	// it. The first contact forks it (Twin Volley), as a dart's first hit does.
	private void Grind()
	{
		if (PlayerRef is not Player player)
			return;
		float reach = OrbHitRadius;
		Vector2 at = GlobalPosition;
		EnemyRegistry.CollectNear(at, reach + BodyAllowance, scratch);
		Node first = null;
		foreach (Enemy enemy in scratch)
		{
			if (!IsInstanceValid(enemy) || enemy.IsDying || at.DistanceTo(enemy.GlobalPosition) > reach + BodyAllowance)
				continue;
			player.DealDamageToEnemy(enemy, damage, CritBonus, source: SpellData);
			first ??= enemy;
			if (threaded && struckCount < struck.Length && !HasStruck(enemy))
				struck[struckCount++] = enemy;
		}
		scratch.Clear();
		if (first != null && !orbTouched)
		{
			orbTouched = true;
			TrySplitOnHit(first);
		}
		// Threaded Needle on an orb (2026-10-30, "every ascension should respect the mutations"): the orb
		// struck through its grind and never through Strike, so the needle's turns never happened. Now,
		// once the orb has ground through something and its path runs clear, it turns on the nearest enemy
		// it has not ground and rolls on that way - straight, never homing, so it cannot settle on its
		// quarry beside the wizard - up to ThreadStrikes times inside its one roll, stitching its thread.
		if (threaded && orbTouched && first == null && threadsUsed < ThreadStrikes && OrbRethread(at))
			threadsUsed++;
	}

	private bool OrbRethread(Vector2 at)
	{
		Enemy next = null;
		float best = ThreadReach * ThreadReach;
		EnemyRegistry.CollectNear(at, ThreadReach, scratch);
		foreach (Enemy candidate in scratch)
		{
			if (!IsInstanceValid(candidate) || candidate.IsDying || HasStruck(candidate))
				continue;
			float d = at.DistanceSquaredTo(candidate.GlobalPosition);
			if (d < best)
			{
				best = d;
				next = candidate;
			}
		}
		scratch.Clear();
		if (next == null)
			return false;
		Vector2 turn = (next.GlobalPosition - at).Normalized();
		if (turn == Vector2.Zero)
			return false;
		direction = turn;
		if (thread == null)
		{
			thread = new ThreadOfStardust();
			AddChild(thread);
		}
		thread.Stitch(at);
		return true;
	}

	// Where the orb's flight ends it bursts, once, over a reach two and a half times its own; Unstable
	// Detonation's splash is folded into the burst rather than set off on every grind.
	private void OrbBurst()
	{
		float areaScale = AreaMultiplier * (SpellData?.GetAreaMultiplierAtLevel(CurrentLevel) ?? 1f);
		float reach = OrbBurstRadius * areaScale;
		if (SpellData?.HasEffectFlag(SpellEffect.ExplosionOnHit) == true)
			reach *= OrbUnstableReach;
		int burst = Math.Max(1, Mathf.RoundToInt(damage * OrbBurstShare));
		if (PlayerRef is Player player)
		{
			Vector2 at = GlobalPosition;
			EnemyRegistry.CollectNear(at, reach + BodyAllowance, scratch);
			foreach (Enemy enemy in scratch)
			{
				if (IsInstanceValid(enemy) && !enemy.IsDying && at.DistanceTo(enemy.GlobalPosition) <= reach + BodyAllowance)
					player.DealDamageToEnemy(enemy, burst, CritBonus, source: SpellData);
			}
			scratch.Clear();
		}
		if (GetParent() is Node2D arena)
			ImpactDebrisLayer.Spawn(arena, GlobalPosition, reach, ArcaneRamp, Colors.White);
		// A threaded orb's thread fades where it was stitched, as a dart's does (End).
		if (thread != null)
		{
			endingSeconds = ThreadFadeSeconds;
			speed = 0f;
			thread.Stitch(GlobalPosition);
			QueueRedraw();
			return;
		}
		QueueFree();
	}

	// One quad the shader paints: round the orb at its fullest swell, or along the dart, its spray
	// included, in the node's own units. Nothing once the dart has spent its last strike.
	public override void _Draw()
	{
		if (endingSeconds >= 0f)
			return;
		if (orb)
		{
			float r = OrbHitRadius * 1.12f + 4f;
			DrawRect(new Rect2(-r, -r, r * 2f, r * 2f), Colors.White);
			return;
		}
		float sx = MathF.Max(0.01f, MathF.Abs(Scale.X));
		float sy = MathF.Max(0.01f, MathF.Abs(Scale.Y));
		float size = sx / sceneScale;
		DrawRect(new Rect2(-DartBack * size / sx, -DartHalf * size / sy, (DartBack + DartAhead) * size / sx, DartHalf * 2f * size / sy), Colors.White);
	}

	// Twin Volley (magic_missile_twin_volley). The first enemy the missile touches makes it
	// fork: two shards fly straight out of the impact point at right angles to the missile's
	// heading, one to each side, for a fraction of the damage. It fires once per missile even
	// with pierce, because the upgrade is a fork on the initial hit rather than a rider on
	// every hit.
	//
	// The shards deliberately do not home and do not pick targets. Homing shards made the fork
	// a second seeking volley - it could not miss, so it was worth taking regardless of how the
	// player positioned, and in a pack it looked like three missiles converging on the same
	// knot. A fixed 90-degree spray is a positional upgrade instead: it pays when the missile
	// hits the near edge of a line of enemies, and does nothing in the open.
	private void TrySplitOnHit(Node hitTarget)
	{
		if (hasSplit || SplitGeneration > 0)
			return;
		if (!SpellData.HasEffectFlag(SpellEffect.SplitOnHit))
			return;

		// EffectValue is a percentage of this missile's damage, set by the evolution that
		// granted the fork. The fallback mirrors Twin Volley's tuned value so a shard is never
		// a no-op if some future caller leaves it unset.
		float percent = SpellData.GetEffectValueAtLevel(SpellEffect.SplitOnHit, CurrentLevel);
		if (percent <= 0f)
			percent = 30f;

		// A shard is another copy of this scene, so the missile reloads its own PackedScene
		// rather than the player having to hand one down. If that ever fails, say so - a
		// silently missing fork reads as a balance problem instead of a wiring one.
		PackedScene scene = string.IsNullOrEmpty(SceneFilePath)
			? null
			: ResourceLoader.Load<PackedScene>(SceneFilePath);
		if (scene == null)
		{
			GD.PushWarning($"[MagicMissile] Twin Volley could not reload its own scene (SceneFilePath='{SceneFilePath}'); no shards were fired.");
			return;
		}

		// Mark before spawning: a shard added to the tree can collide on the same frame, and
		// re-entering here would fork twice.
		hasSplit = true;

		// Square to the missile's heading at the moment of impact. direction is normalised and
		// never zero for a missile in flight, but a fork fired from a standstill would give two
		// shards with no heading at all, so fall back to the sprite's facing.
		Vector2 heading = direction.LengthSquared() > 0f ? direction : Vector2.Right.Rotated(Rotation);
		SpawnSplitShard(scene, heading.Rotated(MathF.PI / 2f), percent, hitTarget);
		SpawnSplitShard(scene, heading.Rotated(-MathF.PI / 2f), percent, hitTarget);
	}

	private void SpawnSplitShard(PackedScene scene, Vector2 shardDirection, float percent, Node hitTarget)
	{
		var shard = scene.Instantiate<Area2D>();
		// Nudge the shard clear of the body its parent just struck so the fork reads as two
		// shards peeling away rather than three impacts stacked on one enemy.
		Vector2 spawnOffset = shardDirection * 18f;
		shard.Position = GlobalPosition + spawnOffset;
		// Carry the evolution tint across by hand: ApplyLegendaryVisual only runs on the
		// missiles the player fires, so a shard would otherwise render untinted.
		shard.Modulate = Modulate;

		if (shard is MagicMissile shardScript)
		{
			shardScript.SpellData = SpellData;
			shardScript.DamageMultiplier = DamageMultiplier * (percent / 100f);
			shardScript.AreaMultiplier = AreaMultiplier;
			shardScript.DurationMultiplier = DurationMultiplier;
			shardScript.MaxTravelDistance = SplitTravelDistance;
			shardScript.SplitGeneration = SplitGeneration + 1;
			shardScript.SplitIgnoreTarget = hitTarget;
			shardScript.PlayerRef = PlayerRef;
			shardScript.SetSpellLevel(CurrentLevel);
		}

		// Added on the deferred flush, not here. The fork happens inside area_entered/body_entered,
		// which run while the physics server is flushing queries, and adding an Area2D then is
		// "Can't change this state while flushing queries" - the shard was left half-registered. The
		// lambda holds what it needs rather than this missile, which may be freed by then (pierce).
		Node parent = GetParent();
		Vector2 from = GlobalPosition + spawnOffset;
		Callable.From(() =>
		{
			if (parent == null || !IsInstanceValid(parent) || !parent.IsInsideTree())
			{
				shard.Free();
				return;
			}
			parent.AddChild(shard);
			// Null target: Shoot only locks on when handed an enemy, so passing none is what makes
			// the shard fly straight instead of curving into whatever is nearest.
			(shard as MagicMissile)?.Shoot(from, from + shardDirection, null);
		}).CallDeferred();
	}

	private void RefreshComputedStats()
	{
		if (weapon != null)
		{
			ApplyLegacyWeapon(weapon);
			return;
		}

		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 1) * DamageMultiplier));
		range = SpellData?.GetRangeAtLevel(CurrentLevel) ?? 500f;
		speed = MathF.Max(1f, BaseSpeed + (SpellData?.GetEffectValueAtLevel(SpellEffect.ProjectileSpeed, CurrentLevel) ?? 0f));
		duration = MathF.Max(0f, BaseDuration * DurationMultiplier);
		pierce = Math.Max(0, BasePierce + (SpellData != null ? SpellData.GetPierceAtLevel(CurrentLevel) : 0));
		areaRadius = MathF.Max(2f, (BaseArea + (SpellData?.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel) ?? 0f)) * AreaMultiplier * (SpellData?.GetAreaMultiplierAtLevel(CurrentLevel) ?? 1f));
	}

	private void ApplyLegacyWeapon(Weapon value)
	{
		if (value == null) return;

		damage = value.Damage;
		range = value.Range;
		speed = value.Speed;
		duration = value.Duration;
		pierce = Math.Max(0, value.Pierce);
		areaRadius = MathF.Max(2f, value.Area);

		var cs = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (cs?.Shape is CircleShape2D circle)
		{
			circle.Radius = areaRadius;
		}
	}

	/// <summary>
	/// Threaded Needle's thread: the path a dart stitched through the pack since its first strike, drawn
	/// as a fading line of stardust in world space. A child of its dart (top-level, so it does not turn
	/// with it), a fixed ring of points, nothing allocated after the first stitch.
	/// </summary>
	private sealed partial class ThreadOfStardust : Node2D
	{
		private const int Points = 32;
		private const float Spacing = 10f;
		private readonly Vector2[] at = new Vector2[Points];
		private readonly float[] stamp = new float[Points];
		private int head;
		private int count;
		private float clock;
		private Vector2 now;
		private bool flying;

		public override void _Ready()
		{
			TopLevel = true;
			ZAsRelative = false;
			ZIndex = DrawLayers.Air;
			GlobalPosition = Vector2.Zero;
		}

		/// <summary>A point of the thread where the dart is now: a strike, a turn, its end.</summary>
		public void Stitch(Vector2 where)
		{
			at[head] = where;
			stamp[head] = clock;
			head = (head + 1) % Points;
			count = Math.Min(Points, count + 1);
			now = where;
		}

		public void Tick(Vector2 dart, bool stillFlying)
		{
			clock += (float)GetProcessDeltaTime();
			flying = stillFlying;
			now = dart;
			int last = (head - 1 + Points) % Points;
			if (stillFlying && count > 0 && at[last].DistanceSquaredTo(dart) >= Spacing * Spacing)
				Stitch(dart);
			QueueRedraw();
		}

		// Stamped on the world's 2-unit grid (PixelInk): an under-stroke of the Arcane edge three art pixels
		// thick and a seam of hot light one pixel down its middle, each stitch dissolving through the Bayer
		// matrix as it ages - it was two antialiased DrawLines a stitch, fading by alpha.
		public override void _Draw()
		{
			if (count < 1)
				return;
			ElementColors.Ramp ramp = ArcaneRamp;
			// Oldest to newest, each stitch as bright as it is young; the last runs on to the dart itself.
			for (int pass = 0; pass < 2; pass++)
			{
				for (int i = count - 1; i >= 0; i--)
				{
					int a = (head - 1 - i + Points * 2) % Points;
					Vector2 from = at[a];
					Vector2 to = i == 0 ? (flying ? now : from) : at[(a + 1) % Points];
					float age = clock - stamp[a];
					float life = 1f - age / ThreadFadeSeconds;
					if (life <= 0f || from == to)
						continue;
					if (pass == 0)
						PixelInk.Line(this, from, to, ramp.Edge, 3, life * 1.6f);
					else
						PixelInk.Line(this, from, to, life > 0.6f ? ramp.Hot : ramp.Mid, 1, life * 1.3f);
				}
			}
			// Motes of stardust shaken off the thread, one at every other stitch.
			for (int i = 0; i < count; i += 2)
			{
				int a = (head - 1 - i + Points * 2) % Points;
				float life = 1f - (clock - stamp[a]) / ThreadFadeSeconds;
				if (life <= 0f)
					continue;
				Vector2 drift = new Vector2(MathF.Sin(a * 2.1f), MathF.Cos(a * 1.3f)) * (1f - life) * 8f;
				PixelInk.Cell(this, at[a] + drift, ramp.Core, life * 1.5f);
			}
		}
	}
}
