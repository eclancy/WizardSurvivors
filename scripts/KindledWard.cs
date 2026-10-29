using Godot;
using System;

namespace WizardSurvivors.scripts;

// Kindled Ward: a mote of light you make, which then goes and does its job without you.
//
// This is the autonomous-agent archetype the roster has never had - the one thing no amount of
// re-pointing an existing spell can fake, because every other spell resolves the instant it is
// cast and this one is still working several seconds later, somewhere you are not.
//
// IT IS NOT A COMPANION, and that is a hard constraint rather than flavour. `.ai/world-and-tone.md`
// is explicit that the player has no allies and no escort - everyone who could help is in a cell -
// and that everything glowing on screen other than the player is *something you made*: a ward, a
// flame, a spell. So this is a made thing. It has no face, no name and no loyalty; it is a lit
// object that drifts at whatever is nearest and burns it, and when it has burned out it is gone.
// A summoned creature with its own personality would quietly delete the premise of the game.
//
// **Rimelight** (the level 4 element path into Ice, ElementPaths): the halo, body and core burn in
// Ice's ramp instead of candle gold, and whatever it burns is chilled (the rider, applied by
// Player.DealDamageToEnemy).
public partial class KindledWard : Node2D, IPaintsElementPath
{
	[Export] public float DriftSpeed { get; set; } = 95f;
	[Export] public float StrikeRadius { get; set; } = 34f;
	[Export] public float StrikeInterval { get; set; } = 0.6f;
	[Export] public float Lifetime { get; set; } = 9.0f;

	// Slower than the player and slower than most enemies, on purpose. A ward that could chase
	// anything down would make positioning irrelevant; one that drifts has to be *placed*, which
	// means the player casting it is still making a decision about where they are standing.
	[Export] public float TargetRefreshSeconds { get; set; } = 0.25f;

	public int Damage { get; set; } = 6;
	public SpellData SpellData { get; set; }
	public Player PlayerRef { get; set; }

	// EVERY CARD ITS OWN (.ai/spell-roster.md section 2e, built 2026-10-29). Each milestone changes what
	// the ward does where you can see it; the numbers ride along. The ids are the archetype's, kept so
	// every save's choices and the Ascension browser carry over.
	//
	// Flare: the first time a ward meets an enemy it flares - a burst of light FlareReachScale times as
	// wide as its burn, for FlareDamageShare times a strike. Once a ward.
	public const string FlareId = "kindled_ward_amplified_impact";
	// 1.6 and 2, not the first build's 2.2 and 2.5 with +35% on the card, which measured 81 / 84 / 143 a
	// second at levels 4 / 5 / 6 against the old Amplified Impact's 37 / 55 / 99 (2026-10-29).
	public const float FlareReachScale = 1.6f;
	public const float FlareDamageShare = 2f;
	private const float BigFlareSeconds = 0.4f;
	// Wisp Trail: as it drifts it sheds a wisp every WispSpacing of its path, each hanging WispSeconds and
	// searing what walks into it for WispDamageShare of a strike every half second (WardWisps).
	public const string WispTrailId = "kindled_ward_hyper_acceleration";
	public const float WispSpacing = 24f;
	public const float WispSeconds = 2.5f;
	public const float WispRadius = 20f;
	public const float WispDamageShare = 0.5f;
	// Pyre: today's Titan Cataclysm - the ward burns twice as wide and is drawn as a small sun. Its
	// numbers are its card's (Area and Damage); the drawing follows StrikeRadius (DrawScale).
	public const string PyreId = "kindled_ward_titan_cataclysm";
	// Kindling Flock: the wards no longer drift off at the nearest enemy. Each takes a place on a ring
	// round the wizard and turns with it, breaks off to burn whatever comes within FlockSight of its
	// place, and flies back to its place when nothing does - moths round the flame, not an orbit of
	// blades: it is the burn that does the work, where the pack comes in.
	public const string FlockId = "kindled_ward_infinite_horizon";
	public const float FlockRadius = 96f;
	public const float FlockTurnRate = 0.9f;
	public const float FlockSight = 120f;
	public const float FlockDartScale = 2.8f;

	/// <summary>Kindling Flock: this ward's place on the ring, of <see cref="FlockSize"/>. Set before it enters the tree.</summary>
	public int FlockSlot { get; set; } = -1;
	public int FlockSize { get; set; } = 1;

	private bool flare;
	private bool flared;
	private bool wispTrail;
	private bool flock;
	private float bigFlareSeconds = 1f;
	private float bigFlareReach;
	private float travelled;
	private WardWisps wisps;

	private float age = 0f;
	private float strikeTimer = 0f;
	private float retargetTimer = 0f;
	private float bobSeconds = 0f;
	private Node2D target;
	// The flare each strike throws out to StrikeRadius: seconds since the last one, how many strikes
	// so far (each turns the tongues a little), and whether it found anything.
	private float flareSeconds = 1f; // past the longest flare: none until the first strike
	private int strikes;
	private bool flareHit;
	private const float FlareSeconds = 0.2f;
	private const int FlareTongues = 8;
	// The mote's own glow. It used to be a halo as wide as StrikeRadius, which was the strike's area
	// drawn as a flat disc - a marker. The flare now shows the reach, at the moment it burns.
	private const float HaloRadius = 13f;

	// Its light: Light's ramp. An element path (Rimelight) repaints all of it in its own ramp.
	private ElementColors.Ramp ramp = ElementColors.GetRamp(Element.Light);

	// PIXEL ART (2026-10-29, "Update all spells to the same quality we expect now"). The mote was three
	// translucent DrawCircles and its flare a fan of DrawLines at fractional widths and alpha. Both are now
	// scenes/shaders/kindled_ward.gdshader on one quad, snapped to the world's 2 px cell: a white-hot heart
	// in a flame of the ramp licking upward, a glitter of its light round it, a corona of short rays on a
	// grown ward, and the flare's tongues out to exactly StrikeRadius - every colour opaque, guttering
	// through the dither in its last second.
	private static Shader wardShader;
	private static readonly StringName ParamGrown = "grown";
	private static readonly StringName ParamFade = "fade";
	private static readonly StringName ParamTime = "time";
	private static readonly StringName ParamFlareT = "flare_t";
	private static readonly StringName ParamFlareRadius = "flare_radius";
	private static readonly StringName ParamFlareHit = "flare_hit";
	private static readonly StringName ParamFlareTurn = "flare_turn";
	private static readonly StringName ParamFlareTongues = "flare_tongues";
	private static readonly StringName ParamBigT = "big_t";
	private static readonly StringName ParamBigRadius = "big_radius";
	private static readonly StringName ParamBigTongues = "big_tongues";
	private static readonly StringName ParamCorona = "corona";
	private ShaderMaterial material;
	// The quad as last drawn; redrawn only when the reach it must cover changes.
	private float drawnHalf;

	public override void _Ready()
	{
		// In the air, over the bodies it burns (DrawLayers).
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		bobSeconds = (GetInstanceId() % 29) * 0.21f;
		if (SpellData?.GetBranchElement() is Element branch)
			ramp = ElementColors.GetRamp(branch);
		wardShader ??= GD.Load<Shader>("res://scenes/shaders/kindled_ward.gdshader");
		material = new ShaderMaterial { Shader = wardShader };
		material.SetShaderParameter("tone_core", ramp.Core);
		material.SetShaderParameter("tone_hot", ramp.Hot);
		material.SetShaderParameter("tone_mid", ramp.Mid);
		material.SetShaderParameter("tone_edge", ramp.Edge);
		// Two wards made together should not flicker in step.
		material.SetShaderParameter("seed", (GetInstanceId() % 997) * 0.731f);
		Material = material;
		flare = SpellData?.HasEvolution(FlareId) == true;
		wispTrail = SpellData?.HasEvolution(WispTrailId) == true;
		flock = SpellData?.HasEvolution(FlockId) == true && FlockSlot >= 0;
		if (wispTrail && PlayerRef?.GetParent() is Node2D arena)
		{
			wisps = WardWisps.For(arena, SpellData, PlayerRef);
			wisps.Paint(ramp);
		}
		AcquireTarget();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		age += dt;
		bobSeconds += dt;
		flareSeconds += dt;

		retargetTimer += dt;
		if (retargetTimer >= TargetRefreshSeconds)
		{
			retargetTimer = 0f;
			AcquireTarget();
		}

		Vector2 before = GlobalPosition;
		if (flock)
		{
			MoveInFlock(dt);
		}
		else if (target != null && IsInstanceValid(target))
		{
			Vector2 toTarget = target.GlobalPosition - GlobalPosition;
			if (toTarget.LengthSquared() > 1f)
				GlobalPosition += toTarget.Normalized() * DriftSpeed * dt;
		}

		if (wisps != null)
		{
			travelled += GlobalPosition.DistanceTo(before);
			if (travelled >= WispSpacing)
			{
				travelled = 0f;
				if (IsInstanceValid(wisps))
					wisps.Shed(GlobalPosition, WispRadius * MathF.Sqrt(DrawScale), WispSeconds,
						Math.Max(1, Mathf.RoundToInt(Damage * WispDamageShare)));
			}
		}

		// Flare: the moment it first reaches what it is drifting at, it burns at once rather than on
		// its next beat, so the burst lands where it met the pack.
		if (flare && !flared && target != null && IsInstanceValid(target)
			&& GlobalPosition.DistanceSquaredTo(target.GlobalPosition) <= StrikeRadius * StrikeRadius)
			strikeTimer = StrikeInterval;

		strikeTimer += dt;
		if (strikeTimer >= StrikeInterval)
		{
			strikeTimer = 0f;
			Strike();
		}
		bigFlareSeconds += dt;

		UpdateVisual();

		if (age >= Lifetime)
			QueueFree();
	}

	private void AcquireTarget()
	{
		Node2D nearest = null;
		float best = float.MaxValue;

		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Node2D enemy || !IsInstanceValid(node))
				continue;

			float dist = GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition);
			if (dist < best)
			{
				best = dist;
				nearest = enemy;
			}
		}

		target = nearest;
	}

	private void Strike()
	{
		flareSeconds = 0f;
		strikes++;
		flareHit = false;
		if (PlayerRef == null)
			return;

		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Node2D enemy || !IsInstanceValid(node))
				continue;

			if (GlobalPosition.DistanceTo(enemy.GlobalPosition) > StrikeRadius)
				continue;

			if (node.HasMethod("TakeDamage"))
			{
				PlayerRef.DealDamageToEnemy(node, Damage, source: SpellData);
				flareHit = true;
			}
		}

		if (flare && !flared && flareHit)
			BigFlare();
	}

	// Flare: once, the first time the ward's burn finds something - a burst of its light thrown out to
	// FlareReachScale times its burn, drawn out to exactly the reach it hit.
	private void BigFlare()
	{
		flared = true;
		bigFlareSeconds = 0f;
		bigFlareReach = StrikeRadius * FlareReachScale;
		int burst = Math.Max(1, Mathf.RoundToInt(Damage * FlareDamageShare));
		float r2 = bigFlareReach * bigFlareReach;
		foreach (Enemy enemy in EnemyRegistry.Snapshot)
		{
			if (IsInstanceValid(enemy) && !enemy.IsDying && GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition) <= r2)
				PlayerRef.DealDamageToEnemy(enemy, burst, source: SpellData);
		}
		if (GetParent() is Node2D arena)
			ImpactDebrisLayer.Spawn(arena, GlobalPosition, bigFlareReach, ramp, Colors.White);
	}

	// Kindling Flock: hold a place on the ring round the wizard, turning with it; break off for whatever
	// comes within FlockSight of that place, and come back when it is gone. Faster than a drifting ward,
	// so the flock keeps up with him - it is his, and stays where he is.
	private void MoveInFlock(float dt)
	{
		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;
		// One clock for the whole flock, so the places stay evenly spaced as the ring turns.
		float angle = Mathf.Tau * FlockSlot / Math.Max(1, FlockSize) + Time.GetTicksMsec() * 0.001f * FlockTurnRate;
		Vector2 place = PlayerRef.GlobalPosition + Vector2.FromAngle(angle) * FlockRadius;
		Vector2 goal = place;
		if (target != null && IsInstanceValid(target)
			&& place.DistanceSquaredTo(target.GlobalPosition) <= (FlockSight + StrikeRadius) * (FlockSight + StrikeRadius))
			goal = target.GlobalPosition;
		Vector2 to = goal - GlobalPosition;
		float step = DriftSpeed * FlockDartScale * dt;
		GlobalPosition = to.LengthSquared() <= step * step ? goal : GlobalPosition + to.Normalized() * step;
	}

	// The strike radius every ward starts from, before its level's Area lines, the evolutions and the
	// player's Area. A ward is drawn bigger in proportion to how much further than this it burns.
	private const float BaseStrikeRadius = 34f;

	// How much bigger this ward is drawn than a fresh one: three quarters of how much further it burns
	// (as a power), so a level 8 ward (three times the burn) is drawn over twice the size and a Titan's
	// sun four times - the mote grows with the spell where you can see it, without becoming a disc as
	// wide as the strike, which is the area marker it stopped being. It was the square root, which
	// drew a level 8 ward a third bigger, and nobody could see it grow (2026-10-27).
	private float DrawScale => MathF.Pow(MathF.Max(1f, StrikeRadius / BaseStrikeRadius), 0.75f);

	// A wider ward's flare lingers a little longer, so a burn that reaches three times as far has
	// time to be seen reaching it: up to 0.35 s of every 0.6 s strike.
	private float FlareLength => FlareSeconds * Mathf.Clamp(DrawScale, 1f, 1.75f);

	// Every number the shader draws from (kindled_ward.gdshader), and the one quad it paints, as wide as
	// the furthest thing drawn this frame: the flare's reach while it flares, else the mote and its corona.
	// Plain values a frame; nothing allocates.
	private void UpdateVisual()
	{
		if (material == null)
			return;
		// Guttering in the last second is the only warning the player gets that a ward is about to
		// go out, and it matters: the decision to cast the next one is made on that cue.
		float remaining = Lifetime - age;
		float fade = remaining < 1.0f ? Mathf.Max(0.15f, remaining) : 1.0f;
		float grown = DrawScale;
		material.SetShaderParameter(ParamGrown, grown);
		material.SetShaderParameter(ParamFade, fade);
		material.SetShaderParameter(ParamTime, bobSeconds);

		// Every strike, tongues of light lick out to exactly StrikeRadius - the reach it just burned - and
		// draw back in. Twice as many when it caught something. A wider ward throws more of them, so its
		// flare fills the reach it burns rather than a few hairlines round a wider circle.
		int tongues = (flareHit ? FlareTongues * 2 : FlareTongues) * Math.Clamp(Mathf.RoundToInt(grown), 1, 4);
		material.SetShaderParameter(ParamFlareT, flareSeconds < FlareLength ? flareSeconds / FlareLength : 1f);
		material.SetShaderParameter(ParamFlareRadius, StrikeRadius);
		material.SetShaderParameter(ParamFlareHit, flareHit);
		material.SetShaderParameter(ParamFlareTurn, strikes * 0.41f);
		material.SetShaderParameter(ParamFlareTongues, (float)Math.Min(tongues, TonguesThatFit(StrikeRadius, grown)));
		bool big = bigFlareSeconds < BigFlareSeconds;
		int bigTongues = FlareTongues * 2 * Math.Clamp(Mathf.RoundToInt(grown * FlareReachScale), 1, 4);
		material.SetShaderParameter(ParamBigT, big ? bigFlareSeconds / BigFlareSeconds : 1f);
		material.SetShaderParameter(ParamBigRadius, bigFlareReach);
		material.SetShaderParameter(ParamBigTongues, (float)Math.Min(bigTongues, TonguesThatFit(bigFlareReach, grown)));
		// A grown ward throws its light wider than its glitter: a corona, so a small sun reads as a light
		// source at a glance and not only in the instant it flares. Held well inside the strike's reach.
		float corona = grown > 1.3f ? MathF.Min(HaloRadius * grown * 1.7f, StrikeRadius * 0.7f) : 0f;
		material.SetShaderParameter(ParamCorona, corona);

		float half = MathF.Max(HaloRadius * grown * 1.1f, corona);
		if (flareSeconds < FlareLength)
			half = MathF.Max(half, StrikeRadius);
		if (big)
			half = MathF.Max(half, bigFlareReach);
		half += 4f;
		if (MathF.Abs(half - drawnHalf) > 0.01f)
		{
			drawnHalf = half;
			QueueRedraw();
		}
	}

	// How many tongues a flare can throw before they run together: half way out, the gap from one to the
	// next is at least two and a half of their roots (the shader's root is 3 px x the drawing's growth).
	// Packed tighter, a grown ward's flare filled solid and read as a disc - the marker it must not be.
	private static int TonguesThatFit(float radius, float grown)
	{
		float root = MathF.Max(2f, 3f * grown);
		return Math.Max(6, (int)(Mathf.Tau * radius * 0.5f / (root * 2.5f))) & ~1;
	}

	public override void _Draw()
	{
		if (drawnHalf > 0f)
			DrawRect(new Rect2(-drawnHalf, -drawnHalf, drawnHalf * 2f, drawnHalf * 2f), Colors.White);
	}
}
