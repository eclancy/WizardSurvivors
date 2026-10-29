using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Glacial Spike: erupts a spike of ice from the ground at a target point. It quickly spurts up
// (the spritesheet plays forward, fast), deals AoE damage to every enemy within its hit radius at
// the peak, then shakes as it retracts back into the ground (the sheet plays in reverse with a
// horizontal jitter) before freeing itself. Bigger spikes (Area) cover more ground and therefore
// hit more enemies; higher projectile counts spawn several spikes at once (see Player.FireIceSpikes).
//
// **Its own five milestones (2026-10-29, .ai/spell-roster.md section 2e).** It wore the default
// archetype's five names, every one a number on the same spike. Each id now names something the ice
// DOES, read by id (SpellData.HasEvolution) here and in Player.FireIceSpikes; the ids are unchanged, so
// saves and the Ascension browser carry over:
//   - Shatter: as a spike withdraws it bursts into a ring of ice splinters that fly out (ThrownShards).
//   - Spike Run: the cast's spikes race across the front of the pack one after another, in a line.
//   - Rimefoot: every spike leaves the ground it rose from frozen for a while, slowing what crosses it
//     (FrostPatch).
//   - Glacier Crown: eight spikes erupt in a ring round the target and stand, penning the pack inside
//     (GlacierCrown).
//   - Frozen Spire: the cast's first spike rises three times as tall and stands as a pillar of ice;
//     whatever touches it is frozen.
//
// THE LOOK (2026-10-29, the pixel-art pass: "does not look up to our typical visual fidelity"). The spike
// was a flat triangle out of a 32 px cell, and its sprite was scaled by the spike's Area - 1.3, 1.7 -
// so its art pixels came out uneven and stretched, and a burst of GPU particles, soft and translucent,
// went off at a radius of its own. Now the art is tools/art/glacial_ice.py: faceted ice lit from the
// upper left, pushed up out of a hole in the ground, in three drawings - the spike, a crown's smaller
// spike and Frozen Spire's pillar - every one shown at x2 and none ever scaled (art-direction section
// 1). What grows with Area is the reach, and the reach is now SHOWN: as the spike strikes, its ice is
// thrown out to exactly the radius it hit (ImpactDebrisLayer, Ice's ramp), as Obsidian Spike's glass
// is. The sprite stands on the world's 2 px grid, its withdrawing shake moves in whole art pixels, and
// Shatter's splinters are drawn on the same grid.
public partial class IceSpike : Node2D
{
	public const string ShatterId = "glacial_spike_amplified_impact";
	public const string SpikeRunId = "glacial_spike_hyper_acceleration";
	public const string RimefootId = "glacial_spike_reactive_catalyst";
	public const string GlacierCrownId = "glacial_spike_titan_cataclysm";
	public const string FrozenSpireId = "glacial_spike_infinite_horizon";

	/// <summary>Shatter: splinters a full-size spike throws as it withdraws (a smaller spike, fewer).</summary>
	public const int ShatterShards = 6;
	/// <summary>Shatter: a splinter's hit, as a share of the spike's.</summary>
	public const float ShatterShare = 0.5f;
	/// <summary>Shatter: how far past the spike's rim a splinter flies, as a share of its hit radius.</summary>
	public const float ShatterReach = 1.6f;
	private const float ShardSpeed = 420f;

	/// <summary>Spike Run: spikes the run adds to the cast's own.</summary>
	public const int RunExtra = 2;
	/// <summary>Spike Run: spikes in the run stand this many hit radii apart.</summary>
	public const float RunSpacing = 1.25f;
	/// <summary>Spike Run: seconds between one spike of the run and the next.</summary>
	public const float RunStepSeconds = 0.08f;

	/// <summary>
	/// Frozen Spire: drawn this many times as tall as a spike. The proposal said three; the spike already
	/// grows with its Area, and at level 8 three times was a pillar near three hundred pixels tall that hid
	/// the knot it stood in. Since 2026-10-29 it is the art's proportion, not a scale: the spire is its own
	/// drawing in tools/art/glacial_ice.py (58 art px against the spike's 30), shown at x2 like the spike.
	/// </summary>
	public const float SpireDrawScale = 2f;
	/// <summary>Frozen Spire: its eruption reaches this share of a spike's.</summary>
	public const float SpireReach = 1.4f;
	public const float SpireSeconds = 5f;
	public const float SpireFreezeSeconds = 0.8f;
	/// <summary>Frozen Spire: what touches it is struck for this share of a spike's hit.</summary>
	public const float SpireTouchShare = 0.5f;
	// One enemy is frozen by a spire at most this often, so a pillar holds what walks into it rather
	// than locking it there for its whole stand.
	private const float SpireTouchEvery = 1.2f;
	private const float SpireScanSeconds = 0.2f;
	private const int MaxTouched = 32;

	// The frame the eruption is whole and not yet cracked (tools/art/spell_fx.py glacial_spike: grown by
	// 0.55 of the strip, cracks from 0.6). A spike that stands is held on it.
	private const int StandFrame = 4;
	// The art's foot, in px below the centre of its cell (tools/art/glacial_ice.py: (GY + 1 - CELL_H / 2)
	// x 2): where the spike meets the ground.
	private const float ArtFootBelowCentre = 64f;
	// The sheet's three drawings (glacial_ice.py SIZES): the spike, a crown's spike, Frozen Spire.
	private static readonly StringName AnimSpike = "default";
	private static readonly StringName AnimSmall = "small";
	private static readonly StringName AnimSpire = "spire";

	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float BaseHitRadius { get; set; } = 44f;
	[Export] public float ShakeAmplitude { get; set; } = 3.5f;
	[Export] public float ShakeFrequency { get; set; } = 42f;

	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;

	// Set by Player.FireIceSpikes before CastAt: what this spike of the cast is.
	/// <summary>Seconds before it erupts (Spike Run, a crown's ring).</summary>
	public float Delay { get; set; }
	/// <summary>Its reach against the spell's spike (a crown's ring spikes are smaller).</summary>
	public float SizeScale { get; set; } = 1f;
	/// <summary>Seconds it stands whole after it strikes, before it cracks and withdraws.</summary>
	public float StandSeconds { get; set; }
	/// <summary>Frozen Spire: drawn three times as tall, stands, freezes what touches it.</summary>
	public bool Spire { get; set; }
	/// <summary>Glacier Crown: the pen this spike is part of, which also strikes each enemy once a cast.</summary>
	public GlacierCrown Crown { get; set; }

	/// <summary>The radius this spike strikes, after SetSpellLevel.</summary>
	public float HitRadius => hitRadius;

	private enum Phase { Waiting, Erupting, Standing, Cracking, Withdrawing }

	private AnimatedSprite2D spikeAnimation;
	private int damage = 7;
	private float hitRadius = 44f;
	// The spell's spike radius before this spike's SizeScale: what a spire's touch is measured from.
	private float spellRadius = 44f;
	// Any milestone's SpeedMultiplier: how much faster than the authored spurt the spike erupts.
	private float eruptSpeed = 1f;
	// Root Duration (the level table's generated line): what the spike impales is pinned this long.
	private float pinSeconds = 0f;
	private bool shatter;
	private bool rimefoot;
	private Phase phase = Phase.Waiting;
	private float phaseTime;
	private bool damageDealt = false;
	private float shakeTime = 0f;
	private float baseAnimX = 0f;
	private float footY = 19f;
	// Which drawing this spike is (Spire, a crown's smaller SizeScale, or the spike), and the sprite's
	// place with its texels on the world's grid (set in CastAt, where the spike's point is known).
	private StringName anim = AnimSpike;
	private Vector2 restAt;
	private float spireScan;
	private float clock;
	private readonly Enemy[] touched = new Enemy[MaxTouched];
	private readonly float[] touchedAt = new float[MaxTouched];
	private ThrownShards shards;
	private bool drewShards;
	private static readonly List<Enemy> scratch = new();

	private static readonly Color Occ = new Color(0.020f, 0.027f, 0.047f, 1f);

	public override void _Ready()
	{
		// The spike is the strike: drawn over the body it erupts under, as Obsidian Spike's is, rather
		// than sorted in with the bodies by its foot (DrawLayers).
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		spikeAnimation = GetNodeOrNull<AnimatedSprite2D>("SpikeAnimation");
		if (spikeAnimation != null)
		{
			baseAnimX = spikeAnimation.Position.X;
			footY = spikeAnimation.Position.Y + ArtFootBelowCentre;
			spikeAnimation.Connect("animation_finished", new Callable(this, nameof(OnAnimationFinished)));
			spikeAnimation.Visible = false;
		}
		RefreshComputedStats();
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	public void CastAt(Vector2 position)
	{
		GlobalPosition = position;
		// A spike that stands - a spire, a crown's ring - is a thing standing on the ground the way a body
		// or a tree is, so it is sorted among the bodies by its foot rather than drawn over them all
		// (DrawLayers): what walks behind the pillar is hidden by it, what walks in front is not.
		if (StandSeconds > 0f)
			ZIndex = DrawLayers.Bodies;
		// The sprite's texels on the world's grid: the spike stands wherever its target was.
		restAt = new Vector2(baseAnimX, footY - ArtFootBelowCentre) + SpellInk.GridOffset(this);
		if (spikeAnimation != null)
			spikeAnimation.Position = restAt;
		damageDealt = false;
		shakeTime = 0f;
		phaseTime = 0f;
		phase = Phase.Waiting;
		if (Delay <= 0f)
			Erupt();
	}

	private void Erupt()
	{
		phase = Phase.Erupting;
		phaseTime = 0f;
		if (spikeAnimation == null)
			return;
		spikeAnimation.Visible = true;
		spikeAnimation.Position = restAt;
		spikeAnimation.SpeedScale = 1.6f * eruptSpeed; // quick spurt out of the ground
		spikeAnimation.Animation = anim;
		spikeAnimation.Frame = 0;
		spikeAnimation.Play(anim);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		phaseTime += dt;
		switch (phase)
		{
			case Phase.Waiting:
				if (phaseTime >= Delay)
					Erupt();
				break;
			case Phase.Erupting:
				// A spike that stands is held whole on its last uncracked frame and strikes there.
				if (StandSeconds > 0f && spikeAnimation != null && spikeAnimation.Frame >= StandFrame)
				{
					spikeAnimation.Pause();
					Strike();
					phase = Phase.Standing;
					phaseTime = 0f;
				}
				break;
			case Phase.Standing:
				if (Spire)
					TouchSpire(dt);
				if (phaseTime >= StandSeconds)
				{
					phase = Phase.Cracking;
					spikeAnimation?.Play(anim);
				}
				break;
			case Phase.Withdrawing:
				shakeTime += dt;
				if (spikeAnimation != null)
				{
					// The tremble moves in whole art pixels, so the ice never smears across the grid.
					float jitter = SpellInk.Snap(Mathf.Sin(shakeTime * ShakeFrequency) * ShakeAmplitude * MathF.Min(SizeScale, 1.5f));
					spikeAnimation.Position = restAt + new Vector2(jitter, 0f);
				}
				break;
		}

		if (shards != null && shards.Active)
		{
			shards.Update(dt);
			QueueRedraw();
			drewShards = true;
		}
		else if (drewShards)
		{
			// The last splinter has landed: clear them, and go if the spike is already back underground.
			QueueRedraw();
			drewShards = false;
			if (phase == Phase.Withdrawing && spikeAnimation != null && !spikeAnimation.IsPlaying())
				QueueFree();
		}
	}

	private void OnAnimationFinished()
	{
		switch (phase)
		{
			case Phase.Erupting:
				// Fully erupted: strike everything in range, then shake back down into the ground.
				Strike();
				Withdraw();
				break;
			case Phase.Cracking:
				Withdraw();
				break;
			case Phase.Withdrawing:
				// Splinters still in flight keep the node until they land (see _Process).
				if (shards == null || !shards.Active)
					QueueFree();
				break;
		}
	}

	private void Strike()
	{
		if (damageDealt)
			return;
		damageDealt = true;
		DealDamage();
		EmitFrostBurst();
		if (Crown != null && IsInstanceValid(Crown))
			Crown.Close();
		// Rimefoot: the ground the spike rose from stays frozen.
		if (rimefoot && GetParent() is Node2D arena)
			FrostPatch.Lay(arena, GlobalPosition, hitRadius);
	}

	private void Withdraw()
	{
		phase = Phase.Withdrawing;
		shakeTime = 0f;
		if (spikeAnimation != null)
		{
			spikeAnimation.SpeedScale = 0.7f; // slower, trembling retract
			spikeAnimation.PlayBackwards(anim);
		}
		// Shatter: as it goes back into the ground the spike bursts, and its splinters fly out past its
		// rim into what was standing round it.
		if (shatter && PlayerRef is Player player)
		{
			shards ??= new ThrownShards();
			int count = Math.Clamp(Mathf.RoundToInt(ShatterShards * SizeScale), 3, ThrownShards.MaxShards);
			shards.Throw(GlobalPosition, count, hitRadius * 0.85f, hitRadius * ShatterReach, ShardSpeed,
				(GetInstanceId() % 360) * 0.0174533f, Math.Max(1, Mathf.RoundToInt(damage * ShatterShare)), SpellData, player);
		}
	}

	private void DealDamage()
	{
		var player = PlayerRef as Player;
		float r2 = hitRadius * hitRadius;
		Vector2 at = GlobalPosition;
		EnemyRegistry.CollectNear(at, hitRadius, scratch);
		foreach (Enemy enemy in scratch)
		{
			if (!IsInstanceValid(enemy) || enemy.IsDying || at.DistanceSquaredTo(enemy.GlobalPosition) > r2)
				continue;
			// A crown strikes each enemy once, however many of its spikes it stands among.
			if (Crown != null && IsInstanceValid(Crown) && !Crown.Claim(enemy))
				continue;
			player?.DealDamageToEnemy(enemy, damage, source: SpellData);
			// A root is a slow of 0 (Enemy.ApplySlow); a boss's floor turns it into a hard slow.
			if (pinSeconds > 0f && IsInstanceValid(enemy) && !enemy.IsDying)
				enemy.ApplySlow(0f, pinSeconds);
		}
		scratch.Clear();
	}

	// Frozen Spire: whatever comes within the pillar's footprint is struck and frozen - at most once a
	// SpireTouchEvery each, so it holds an enemy against the ice rather than locking it there.
	private void TouchSpire(float dt)
	{
		spireScan -= dt;
		if (spireScan > 0f)
			return;
		spireScan = SpireScanSeconds;
		var player = PlayerRef as Player;
		float reach = spellRadius * 0.9f;
		float r2 = reach * reach;
		Vector2 at = GlobalPosition;
		int touchDamage = Math.Max(1, Mathf.RoundToInt(damage * SpireTouchShare));
		EnemyRegistry.CollectNear(at, reach, scratch);
		foreach (Enemy enemy in scratch)
		{
			if (!IsInstanceValid(enemy) || enemy.IsDying || at.DistanceSquaredTo(enemy.GlobalPosition) > r2)
				continue;
			int slot = -1;
			int oldest = 0;
			bool fresh = true;
			for (int i = 0; i < MaxTouched; i++)
			{
				if (ReferenceEquals(touched[i], enemy))
				{
					fresh = clock - touchedAt[i] >= SpireTouchEvery;
					slot = i;
					break;
				}
				if (touchedAt[i] < touchedAt[oldest])
					oldest = i;
			}
			if (!fresh)
				continue;
			if (slot < 0)
				slot = oldest;
			touched[slot] = enemy;
			touchedAt[slot] = clock;
			player?.DealDamageToEnemy(enemy, touchDamage, source: SpellData);
			if (IsInstanceValid(enemy) && !enemy.IsDying)
				enemy.ApplySlow(0f, SpireFreezeSeconds);
		}
		scratch.Clear();
	}

	// The strike's ice, thrown out from the spike to exactly the radius it hit - the reach Area grows,
	// shown by the ice itself and stopping on it (ImpactDebris), never a ring. It replaced a soft GPU
	// particle burst at a fixed 18 px of its own.
	private void EmitFrostBurst()
	{
		if (GetParent() is Node2D arena)
			ImpactDebrisLayer.Spawn(arena, GlobalPosition, hitRadius, ElementColors.GetRamp(Element.Ice), Modulate);
	}

	// Shatter's splinters, in the Ice ramp, over the bodies with the spike, on the world's grid.
	public override void _Draw()
	{
		if (shards == null || !shards.Active)
			return;
		var ice = ElementColors.GetRamp(Element.Ice);
		SpellInk.AlignToWorldGrid(this);
		shards.Draw(this, GlobalTransform.AffineInverse(), ice.Edge, ice.Mid, ice.Core, Occ);
	}

	private void RefreshComputedStats()
	{
		float sizeScale = AreaMultiplier;
		if (SpellData != null)
			sizeScale *= 1.0f + SpellData.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel);
		sizeScale = MathF.Max(0.25f, sizeScale);

		float reachScale = Spire ? SpireReach : SizeScale;
		damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 7) * DamageMultiplier));
		spellRadius = MathF.Max(6f, BaseHitRadius * sizeScale);
		hitRadius = MathF.Max(6f, spellRadius * reachScale);
		eruptSpeed = MathF.Max(0.25f, SpellData?.GetSpeedMultiplier() ?? 1f);
		pinSeconds = SpellData?.GetEffectValueAtLevel(SpellEffect.RootDuration, CurrentLevel) ?? 0f;
		shatter = SpellData?.HasEvolution(ShatterId) ?? false;
		rimefoot = SpellData?.HasEvolution(RimefootId) ?? false;

		// Never scaled (art-direction section 1): a bigger spike is a bigger drawing. Frozen Spire is its
		// pillar (SpireDrawScale times the spike's height, in the art), a crown's ring its smaller spike.
		anim = Spire ? AnimSpire : SizeScale < 0.9f ? AnimSmall : AnimSpike;
	}
}
