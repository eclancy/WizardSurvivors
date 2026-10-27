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

	// Candle gold: Light's hot, mid and core. An element path repaints all three in its own ramp.
	private Color haloTone = new Color(1.0f, 0.86f, 0.46f);
	private Color bodyTone = new Color(1.0f, 0.93f, 0.66f);
	private Color coreTone = new Color(1.0f, 0.99f, 0.90f);

	public override void _Ready()
	{
		ZIndex = 6;
		bobSeconds = (GetInstanceId() % 29) * 0.21f;
		if (SpellData?.GetBranchElement() is Element branch)
		{
			ElementColors.Ramp ramp = ElementColors.GetRamp(branch);
			haloTone = ramp.Mid;
			bodyTone = ramp.Hot;
			coreTone = ramp.Core;
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

		if (target != null && IsInstanceValid(target))
		{
			Vector2 toTarget = target.GlobalPosition - GlobalPosition;
			if (toTarget.LengthSquared() > 1f)
				GlobalPosition += toTarget.Normalized() * DriftSpeed * dt;
		}

		strikeTimer += dt;
		if (strikeTimer >= StrikeInterval)
		{
			strikeTimer = 0f;
			Strike();
		}

		QueueRedraw();

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

	public override void _Draw()
	{
		// Guttering in the last second is the only warning the player gets that a ward is about to
		// go out, and it matters: the decision to cast the next one is made on that cue.
		float remaining = Lifetime - age;
		float fade = remaining < 1.0f ? Mathf.Max(0.15f, remaining) : 1.0f;
		float flicker = (1.0f + MathF.Sin(bobSeconds * 9.3f) * 0.08f) * DrawScale;

		var halo = new Color(haloTone, 0.22f * fade);
		var body = new Color(bodyTone, 0.55f * fade);
		var core = new Color(coreTone, 0.92f * fade);

		if (flareSeconds < FlareLength)
			DrawFlare(fade);
		// A grown ward throws its light wider than its halo: a faint outer glow, so a small sun reads
		// as a light source at a glance and not only in the instant it flares.
		if (DrawScale > 1.3f)
			DrawCircle(Vector2.Zero, HaloRadius * flicker * 1.9f, new Color(haloTone, 0.09f * fade));
		DrawCircle(Vector2.Zero, HaloRadius * flicker, halo);
		DrawCircle(Vector2.Zero, 9f * flicker, body);
		DrawCircle(Vector2.Zero, 4.5f * flicker, core);
	}

	// Every strike, tongues of light lick out from the mote to exactly StrikeRadius - the reach it
	// just burned - and draw back in. Brighter and twice as many when it caught something. The tips
	// stop half a line's width inside the radius, so nothing is drawn past what was hit.
	private void DrawFlare(float fade)
	{
		float t = flareSeconds / FlareLength;
		float reach = 1f - (1f - MathF.Min(1f, t / 0.45f)) * (1f - MathF.Min(1f, t / 0.45f));
		// A wider ward throws more tongues, and thicker ones, so its flare fills the reach it burns
		// rather than a few hairlines spread round a wider circle.
		float grown = DrawScale;
		int tongues = (flareHit ? FlareTongues * 2 : FlareTongues) * Math.Clamp(Mathf.RoundToInt(grown), 1, 3);
		float width = Mathf.Lerp(3f, 1f, t) * grown;
		float alpha = (1f - t) * fade * (flareHit ? 1f : 0.6f);
		float turn = strikes * 0.41f;
		for (int i = 0; i < tongues; i++)
		{
			float a = turn + i * Mathf.Tau / tongues;
			// Every other tongue is short, so the flare reads as flame and not as a wheel.
			float length = (i % 2 == 0 ? 1f : 0.7f) * (StrikeRadius - width * 0.5f) * reach;
			var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
			DrawLine(dir * 6f, dir * MathF.Max(6f, length), new Color(i % 2 == 0 ? bodyTone : haloTone, alpha), width);
		}
	}
}
