using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Gravewell: a grave opened in the ground on the path of the approaching swarm, which waits, and
// collapses on whatever walks into it.
//
// **Aim: the ground an enemy is about to walk over** (Player.FireGravewellTrap). It used to be random
// ground near the player, and the playtest that followed asked whether the spell did anything at all:
// most pits sat where nothing ever walked, as a faint dark disc. A pit is still put on the ground and
// never on an enemy - the enemy decides whether to walk into it - but it is put a little ahead of one,
// on the way it is coming.
//
// **Cadence: proximity.** The timer only decides when a pit is *placed*. What decides when it goes
// off is an enemy treading on its bank, so the spell resolves at the moment the crowd arrives. A pit
// cannot spring while it is opening - the wind-up is the cost of the aim being free - and it closes
// up on its own after Lifetime, so a kiting player cannot paper the arena with them (MaxWaiting).
//
// **Nothing here marks an area.** The pit is an object: generated art (tools/art/gravewell.py) whose
// bank sits at the trigger radius, so stepping on the turned earth IS the trigger. The blast is shown
// by the collapse itself - cracks and a heave of earth that run out to exactly BlastRadius and stop
// (scenes/shaders/gravewell_collapse.gdshader) - and everything it catches is yanked toward the pit
// and held by a heap of earth and fingers of Darkness at its feet for as long as the root lasts.
//
// THE ONE HIT AUTHORITY IS BlastRadius, and TriggerRadius for the trip. The collapse is tested and
// drawn out of the first; the art's bank is drawn at the second (LIP_RADIUS in the generator). Every
// area term - levels, the player, the evolutions' AreaMultiplier - is folded into BlastRadius once,
// in For(), which is the term the old trap never read.
public partial class GravewellTrap : Node2D
{
	// --- tuning. The pit's art is drawn to TriggerRadius, so a change there is a change to the art. ---
	// 94 until 2026-10-23: once the evolutions' Area reached it, a level 8 pit with Titan Cataclysm was
	// 13% above its peers even after the damage table was trimmed (SpellData_Gravewell.tres).
	public const float BaseBlastRadius = 86f;
	public const float BaseRootSeconds = 1.1f;
	public const float BaseLifetime = 12f;
	// How far ahead of an enemy, toward the player, a pit is opened: an enemy at a skeleton's walk
	// reaches the bank about as the pit finishes opening.
	public const float LeadPixels = 75f;
	// A pit never opens nearer the player than this, so one is never dug under his feet.
	public const float MinFromPlayer = 70f;
	public const int MaxPerCast = 8;
	// How far apart the pits of one cast are kept, and from pits already waiting, when the crowd
	// allows it: a little over two banks' width.
	public const float Spacing = 92f;
	// Unsprung pits standing at once. A new cast past it closes the oldest (it does not vanish: it
	// fills back in), so the field stays readable and the trigger scans stay bounded.
	public const int MaxWaiting = 10;

	[Export] public float ArmSeconds { get; set; } = 0.55f;
	// The bank of the pit: tools/art/gravewell.py LIP_RADIUS is 20 art px, 40 px at x2.
	public const float DefaultTriggerRadius = 42f;
	[Export] public float TriggerRadius { get; set; } = DefaultTriggerRadius;
	[Export] public Texture2D PitTexture { get; set; }
	[Export] public Texture2D GraspTexture { get; set; }

	/// <summary>Everything one cast decides about the pits it opens.</summary>
	public struct Setup
	{
		public int Damage;
		public float BlastRadius;
		public float RootSeconds;
		public float Lifetime;
	}

	/// <summary>
	/// The one place the spell's level, its evolutions and the player's stats become a pit. Every
	/// line on its cards reaches the trap through here: AreaSize (level 3) and the evolutions' Area
	/// into the blast, Root Duration (the generated level 4 line) into the hold, Duration into how
	/// long an unsprung pit waits.
	/// </summary>
	public static Setup For(SpellData spell, float playerArea, float damageMultiplier, float durationMultiplier)
	{
		int level = spell.CurrentLevel;
		return new Setup
		{
			Damage = Math.Max(1, Mathf.RoundToInt(spell.GetDamageAtLevel(level) * damageMultiplier)),
			BlastRadius = (BaseBlastRadius + spell.GetEffectValueAtLevel(SpellEffect.AreaSize, level))
				* playerArea * spell.GetAreaMultiplierAtLevel(level),
			RootSeconds = BaseRootSeconds + spell.GetEffectValueAtLevel(SpellEffect.RootDuration, level),
			Lifetime = BaseLifetime * durationMultiplier,
		};
	}

	public SpellData SpellData { get; private set; }
	public Player PlayerRef { get; private set; }
	public float BlastRadius => setup.BlastRadius;

	// The frame counts are tools/art/gravewell.py's PHASES and GRASP_FRAMES: change both.
	public const int OpenFrames = 4;
	public const int ArmedFrames = 4;
	public const int CollapseFrames = 5;
	public const int GraspFrames = 6;
	public const int PitCell = 48;
	public const int GraspW = 32;
	public const int GraspH = 16;
	// The bank's radius in the art, in art pixels (tools/art/gravewell.py LIP_RADIUS); x2 on screen.
	public const float ArtBankRadius = 20f;
	public const string CollapseShaderPath = "res://scenes/shaders/gravewell_collapse.gdshader";
	// The grasp's heap stands on row 14 of its cell; that row goes this far below an enemy's feet.
	private const float GraspFootDrop = 2f;
	private const float ArmedFrameSeconds = 0.15f;
	private const float CollapseFrameSeconds = 0.09f;
	private const float HeaveSeconds = 0.16f;
	// How long the heave takes to fall away once it has reached the radius.
	private const float RubbleSeconds = 0.18f;
	private const float CloseSeconds = 0.35f;
	private const float FizzleSeconds = 0.4f;
	private const float ScanSeconds = 0.05f;
	// The yank: whatever is caught is tugged toward the pit - about a third of the way in - so a
	// collapse visibly drags the crowd before it holds it. A tug, not a gather: pulled all the way in,
	// the crowd sat in a knot for the next pit to catch whole, and that doubled the spell's damage
	// (Hollow Star is the spell that gathers). Riding knockback, so a boss's resistance applies.
	private const float YankPerPixel = 0.8f;
	private const float MaxYank = 120f;
	private const int MaxHeld = 24;

	private static readonly List<GravewellTrap> waiting = new();
	private static Shader collapseShader;

	private enum Phase { Arming, Armed, Collapsing, Fizzling }

	private Setup setup;
	private Phase phase = Phase.Arming;
	private float phaseSeconds;
	private float totalSeconds;
	private float scanTimer;
	private Node2D ground;
	private Node2D overlay;
	private ShaderMaterial groundMaterial;
	private readonly Enemy[] held = new Enemy[MaxHeld];
	private int heldCount;

	/// <summary>Called by Player before the node enters the tree.</summary>
	public void Configure(SpellData spell, Player player, in Setup pit)
	{
		SpellData = spell;
		PlayerRef = player;
		setup = pit;
	}

	/// <summary>
	/// Closes the oldest unsprung pits until <paramref name="incoming"/> more can open within
	/// MaxWaiting. A closed pit fills back in over FizzleSeconds; it stops being a trap at once.
	/// </summary>
	public static void MakeRoom(int incoming)
	{
		int keep = Math.Max(0, MaxWaiting - incoming);
		while (waiting.Count > keep)
			waiting[0].Fizzle();
	}

	/// <summary>True if an unsprung pit already stands within <paramref name="distance"/> of the point.</summary>
	public static bool AnyWaitingNear(Vector2 globalAt, float distance)
	{
		float d2 = distance * distance;
		foreach (GravewellTrap pit in waiting)
		{
			if (pit.GlobalPosition.DistanceSquaredTo(globalAt) < d2)
				return true;
		}
		return false;
	}

	public override void _Ready()
	{
		// On the ground, under the actors: the swarm walks over the pit's bank, not behind it.
		ZIndex = -1;
		waiting.Add(this);

		// The collapse's cracks, under the pit's own art; made only when it springs.
		ground = new Node2D { Name = "Collapse", ShowBehindParent = true, Visible = false };
		AddChild(ground);
		ground.Draw += DrawGround;
		// The earth that holds what it caught, over the bodies it holds.
		overlay = new Node2D { Name = "Held", ZAsRelative = false, ZIndex = 2, Visible = false };
		AddChild(overlay);
		overlay.Draw += DrawHeld;
	}

	public override void _ExitTree()
	{
		waiting.Remove(this);
		if (ground != null && IsInstanceValid(ground))
			ground.Draw -= DrawGround;
		if (overlay != null && IsInstanceValid(overlay))
			overlay.Draw -= DrawHeld;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		phaseSeconds += dt;
		totalSeconds += dt;
		QueueRedraw();

		switch (phase)
		{
			case Phase.Arming:
				// It cannot be triggered while opening, which is what stops a pit placed into a crowd
				// from resolving on the frame it lands.
				if (phaseSeconds >= ArmSeconds)
					Enter(Phase.Armed);
				break;

			case Phase.Armed:
				scanTimer -= dt;
				if (scanTimer <= 0f)
				{
					scanTimer = ScanSeconds;
					if (AnythingOnTheBank())
					{
						Collapse();
						break;
					}
				}
				if (totalSeconds >= setup.Lifetime)
					Fizzle();
				break;

			case Phase.Collapsing:
				UpdateGround();
				overlay.QueueRedraw();
				if (phaseSeconds >= HoldEnds + CloseSeconds)
					QueueFree();
				break;

			case Phase.Fizzling:
				if (phaseSeconds >= FizzleSeconds)
					QueueFree();
				break;
		}
	}

	// The collapse lasts as long as the root, and never so briefly that the heave cannot settle.
	private float HoldEnds => MathF.Max(setup.RootSeconds, HeaveSeconds + RubbleSeconds);

	private void Enter(Phase next)
	{
		phase = next;
		phaseSeconds = 0f;
	}

	private void Fizzle()
	{
		if (phase == Phase.Collapsing || phase == Phase.Fizzling)
			return;
		waiting.Remove(this);
		Enter(Phase.Fizzling);
	}

	private bool AnythingOnTheBank()
	{
		float r2 = TriggerRadius * TriggerRadius;
		Vector2 at = GlobalPosition;
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is Node2D enemy && IsInstanceValid(enemy) && at.DistanceSquaredTo(enemy.GlobalPosition) <= r2)
				return true;
		}
		return false;
	}

	private void Collapse()
	{
		waiting.Remove(this);
		Enter(Phase.Collapsing);
		BuildGround();
		overlay.Visible = true;

		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;

		// The blast is wider than the trigger, on purpose: whatever set it off is caught, and so is
		// whatever was following it.
		Vector2 at = GlobalPosition;
		float r2 = setup.BlastRadius * setup.BlastRadius;
		heldCount = 0;
		foreach (Node node in EnemyRegistry.Snapshot)
		{
			if (node is not Enemy enemy || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			Vector2 offset = at - enemy.GlobalPosition;
			float d2 = offset.LengthSquared();
			if (d2 > r2)
				continue;

			PlayerRef.DealDamageToEnemy(enemy, setup.Damage, source: SpellData);
			if (!IsInstanceValid(enemy) || enemy.IsDying)
				continue;

			// Multiplier 0 is a root, per Enemy.ApplySlow. Rooting what survives is most of the
			// value: the trap is area denial, and an enemy pinned at the pit is denied area.
			enemy.ApplySlow(0f, setup.RootSeconds);
			float d = MathF.Sqrt(d2);
			if (d > 12f)
				enemy.ApplyKnockback(offset / d * MathF.Min(MaxYank, d * YankPerPixel));
			if (heldCount < MaxHeld)
				held[heldCount++] = enemy;
		}
	}

	// --- drawing -------------------------------------------------------------------------------

	public override void _Draw()
	{
		if (PitTexture == null)
			return;
		int frame;
		float alpha = 1f;
		switch (phase)
		{
			case Phase.Arming:
				frame = Math.Min(OpenFrames - 1, (int)(phaseSeconds / MathF.Max(0.01f, ArmSeconds) * OpenFrames));
				break;
			case Phase.Armed:
				frame = OpenFrames + (int)(totalSeconds / ArmedFrameSeconds) % ArmedFrames;
				break;
			case Phase.Collapsing:
			{
				int step = (int)(phaseSeconds / CollapseFrameSeconds);
				// Gape, slide in, go over: then the settled crater for the hold, and the scar at the end.
				frame = OpenFrames + ArmedFrames + (step < 3 ? step
					: phaseSeconds < HoldEnds - 0.25f ? 3 : 4);
				alpha = Mathf.Clamp((HoldEnds + CloseSeconds - phaseSeconds) / CloseSeconds, 0f, 1f);
				break;
			}
			default:
				// Filling back in: the opening played backwards.
				frame = Math.Clamp(OpenFrames - 1 - (int)(phaseSeconds / FizzleSeconds * OpenFrames), 0, OpenFrames - 1);
				alpha = Mathf.Clamp(1f - phaseSeconds / FizzleSeconds + 0.25f, 0f, 1f);
				break;
		}
		// Authored 1:1, shown at x2, centred on the trap's point.
		var src = new Rect2(frame * PitCell, 0, PitCell, PitCell);
		var dest = new Rect2(-PitCell, -PitCell, PitCell * 2f, PitCell * 2f);
		DrawTextureRectRegion(PitTexture, dest, src, new Color(1f, 1f, 1f, alpha));
	}

	private void BuildGround()
	{
		collapseShader ??= GD.Load<Shader>(CollapseShaderPath);
		if (collapseShader == null)
		{
			GD.PushError($"GravewellTrap: {CollapseShaderPath} failed to load.");
			return;
		}
		// One material per collapse, made when it springs - never per frame.
		groundMaterial = new ShaderMaterial { Shader = collapseShader };
		groundMaterial.SetShaderParameter("radius", setup.BlastRadius);
		groundMaterial.SetShaderParameter("inner", TriggerRadius * 0.8f);
		groundMaterial.SetShaderParameter("seed", (float)(GetInstanceId() % 997));
		ground.Material = groundMaterial;
		ground.Visible = true;
		UpdateGround();
		ground.QueueRedraw();
	}

	private void UpdateGround()
	{
		if (groundMaterial == null)
			return;
		float heave = Mathf.Clamp(phaseSeconds / HeaveSeconds, 0f, 1f);
		// Ease-out: the ground leaves the pit fast and slows as it reaches the edge of the blast.
		groundMaterial.SetShaderParameter("front", 1f - (1f - heave) * (1f - heave));
		groundMaterial.SetShaderParameter("rubble_fade", Mathf.Clamp((phaseSeconds - HeaveSeconds) / RubbleSeconds, 0f, 1f));
		// The cracks settle through the hold - dust filling them in from the moment the heave has
		// dropped - rather than standing at full length until the root ends; the earth heaped on what
		// it holds says the hold is still on.
		float settleFrom = HeaveSeconds + RubbleSeconds + 0.2f;
		groundMaterial.SetShaderParameter("crack_fade",
			Mathf.Clamp((phaseSeconds - settleFrom) / MathF.Max(0.1f, HoldEnds + CloseSeconds * 0.5f - settleFrom), 0f, 1f));
	}

	// One quad over the whole blast; the shader decides every pixel and draws nothing past the radius.
	private void DrawGround()
	{
		float half = setup.BlastRadius + 2f;
		ground.DrawRect(new Rect2(-half, -half, half * 2f, half * 2f), Colors.White);
	}

	// A heap of earth over each held enemy's feet with fingers of Darkness round them, rising as the
	// pit springs, holding for the root and crumbling as it ends. One texture draw per enemy.
	private void DrawHeld()
	{
		if (GraspTexture == null || phase != Phase.Collapsing)
			return;
		float t = phaseSeconds;
		int frame = t < 0.06f ? 0 : t < 0.12f ? 1
			: t < setup.RootSeconds - 0.2f ? 2 + (int)(t / 0.2f) % 2
			: t < setup.RootSeconds - 0.08f ? 4
			: t < setup.RootSeconds ? 5 : -1;
		if (frame < 0)
			return;
		var src = new Rect2(frame * GraspW, 0, GraspW, GraspH);
		for (int i = 0; i < heldCount; i++)
		{
			Enemy enemy = held[i];
			if (enemy == null || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			Vector2 feet = overlay.ToLocal(enemy.GlobalPosition) + new Vector2(0f, enemy.StatusFxGeometry.FeetY + GraspFootDrop);
			// The heap's row 14 on the feet, at x2.
			var dest = new Rect2(MathF.Round(feet.X) - GraspW, MathF.Round(feet.Y) - 2f * 14f, GraspW * 2f, GraspH * 2f);
			overlay.DrawTextureRectRegion(GraspTexture, dest, src);
		}
	}
}
