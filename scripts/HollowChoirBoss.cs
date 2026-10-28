using Godot;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

// Chapter 3, the Sunken Cave: THE HOLLOW CHOIR.
//
// Three bodies, one fight. Drop any one of them and it does not die - it goes quiet, sinks out of
// reach, and the other two sing it back up. The only way to end the Choir is to have all three
// silent at the same moment.
//
// WHY THIS FIGHT EXISTS. Every other boss in the game is answered by pointing the build at it,
// because every other boss is one health bar and the auto-fire finds it. This one cannot be, and
// the reason is structural rather than numerical: the spells in this game choose their own target,
// so a fight that requires the player to choose one is a fight the loadout cannot play for them.
// Focus everything down in turn and the first one you dropped is back before the third falls.
// The answer is spread damage, or a burst big enough to close all three inside the revive window.
//
// LINKED HEALTH is the primitive, and it is deliberately not a framework. There is no manager node
// and no shared pool: each voice holds a list of its siblings, and the check "is everyone else
// already down" is three pointer comparisons on the frame one of them falls. A boss with a
// coordinator would be a second place for the fight to be wrong.
public partial class HollowChoirBoss : BossEnemy
{
	/// <summary>
	/// False on the voice the run spawned and tracks, true on the two it calls up. Only the
	/// original emits the victory the run listens for.
	/// </summary>
	[Export] public bool IsEcho { get; set; }

	/// <summary>How many voices in total, including this one. Three is the fight.</summary>
	[Export] public int VoiceCount { get; set; } = 3;

	/// <summary>How far apart they start. Far enough that no single blast covers all three.</summary>
	[Export] public float ChoirSpreadRadius { get; set; } = 210f;

	/// <summary>Seconds a downed voice stays down before the others sing it back.</summary>
	[Export] public float ReviveSeconds { get; set; } = 6.0f;

	/// <summary>How much health a revived voice comes back with. Well under half, or it never ends.</summary>
	[Export] public float ReviveHealthFraction { get; set; } = 0.35f;

	[Export] public Color ChoirColor { get; set; } = new Color(0.45f, 0.86f, 0.92f);

	private readonly List<HollowChoirBoss> voices = new();
	private float downedRemaining = -1f;

	private bool IsDowned => downedRemaining >= 0f;

	public override void _Ready()
	{
		base._Ready();
		voices.Add(this);

		if (IsEcho)
			return;

		CallDeferred(nameof(SummonTheRest));
	}

	// Deferred because _Ready runs while this node is being added, and adding siblings to the same
	// parent mid-add is the kind of thing Godot tolerates until one day it does not.
	private void SummonTheRest()
	{
		Node arena = GetParent();
		if (arena == null || !IsInstanceValid(arena) || string.IsNullOrEmpty(SceneFilePath))
			return;

		var scene = ResourceLoader.Load<PackedScene>(SceneFilePath);
		if (scene == null)
		{
			GD.PushError($"HollowChoirBoss: could not reload its own scene '{SceneFilePath}', so it fights alone.");
			return;
		}

		int wanted = Mathf.Max(1, VoiceCount) - 1;
		for (int i = 0; i < wanted; i++)
		{
			if (scene.Instantiate() is not HollowChoirBoss echo)
			{
				GD.PushError("HollowChoirBoss: its own scene did not instantiate as a HollowChoirBoss.");
				return;
			}

			echo.IsEcho = true;
			echo.Health = Health;
			echo.BossId = BossId;
			echo.BossDisplayName = BossDisplayName;
			voices.Add(echo);
			arena.AddChild(echo);

			float angle = Mathf.Tau * (i + 1) / Mathf.Max(1, VoiceCount);
			echo.GlobalPosition = GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ChoirSpreadRadius;
		}

		// Everyone learns everyone. Done here rather than in each echo's _Ready because only this
		// node ever knows the whole set.
		//
		// THE SNAPSHOT IS LOAD-BEARING. Iterating `voices` directly while assigning into
		// `voice.voices` hits the case where voice IS this, so the Clear below is a modification
		// of the collection being enumerated - it threw InvalidOperationException on the frame
		// the fight started, abandoned the loop half done, and left the three voices with no
		// knowledge of each other. The fight then ran as three unrelated bosses that each died on
		// their own, which looks exactly like the linked health simply not being implemented.
		HollowChoirBoss[] everyone = voices.ToArray();
		foreach (HollowChoirBoss voice in everyone)
		{
			voice.voices.Clear();
			voice.voices.AddRange(everyone);
		}
	}

	protected override GroundSlamAttack BuildSlam() =>
		new(SlamIntervalSeconds, SlamTelegraphSeconds, SlamRadius, SlamDamage, ChoirColor);

	// The Choir has no enrage. Three bodies that all sped up at once would be a difficulty cliff
	// nobody could read, and the fight already escalates on its own every time a revive lands.
	protected override float[] BuildPhaseThresholds() => System.Array.Empty<float>();

	/// <summary>
	/// The health bar shows the whole Choir, not whichever third the run happens to hold a
	/// reference to. A bar that jumped back to full when one voice revived would be lying about
	/// progress the player really had made.
	/// </summary>
	public override float BossHealthFraction
	{
		get
		{
			int current = 0;
			int max = 0;
			foreach (HollowChoirBoss voice in voices)
			{
				if (voice == null || !IsInstanceValid(voice))
					continue;
				current += voice.IsDowned ? 0 : Mathf.Max(0, voice.Health);
				max += voice.MaxHealth;
			}

			return max > 0 ? Mathf.Clamp(current / (float)max, 0f, 1f) : 0f;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);

		if (!IsDowned)
			return;

		downedRemaining -= (float)delta;
		if (downedRemaining <= 0f)
			Rise();
	}

	protected override Vector2 AdjustSteering(Vector2 chaseDirection, float distanceToPlayer) =>
		IsDowned ? Vector2.Zero : base.AdjustSteering(chaseDirection, distanceToPlayer);

	protected override bool WantsToAttack(Node2D target, float distanceToPlayer) =>
		!IsDowned && base.WantsToAttack(target, distanceToPlayer);

	/// <summary>
	/// Falling, not dying. Overriding <c>StartDeath</c> rather than intercepting damage is what
	/// keeps the rest of the game out of this: every spell, poison tick and trap still calls
	/// TakeDamage exactly as it does for anything else, and only the last step changes.
	/// </summary>
	protected override void StartDeath()
	{
		if (IsDying || IsDowned)
			return;

		if (EveryOtherVoiceIsDown())
		{
			// The whole Choir is silent at once. This is the only way the fight ends, and it ends
			// for all three in the same frame so the player never sees two thirds of a corpse.
			// Same reason as the snapshot in SummonTheRest: Silence walks into base.StartDeath,
			// and nothing on that path may be allowed to touch the list being enumerated.
			// They all die on this frame; they only go QUIET in turn - this one first, as the last
			// one struck, then the others in the order they were called up (fallOrder, the fall).
			int order = 1;
			foreach (HollowChoirBoss voice in voices.ToArray())
			{
				if (voice != null && IsInstanceValid(voice) && voice != this)
					voice.Silence(order++);
			}

			fallOrder = 0;
			base.StartDeath();
			return;
		}

		GoDown();
	}

	private bool EveryOtherVoiceIsDown()
	{
		foreach (HollowChoirBoss voice in voices)
		{
			if (voice == null || !IsInstanceValid(voice) || voice == this)
				continue;
			if (!voice.IsDowned && !voice.IsDying)
				return false;
		}

		return true;
	}

	private void GoDown()
	{
		Health = 0;
		downedRemaining = ReviveSeconds;
		// Out of the enemies group, so nothing targets a body that cannot be killed. This is the
		// untargetable-but-alive primitive; the sprite goes with it and the revive ring below is
		// the only thing left on screen.
		SetTargetable(false);
		SfxPlayer.AtPosition(SfxCatalog.BossDeath, GlobalPosition, -6.0f);
	}

	private void Rise()
	{
		downedRemaining = -1f;
		Health = Mathf.Max(1, Mathf.RoundToInt(MaxHealth * ReviveHealthFraction));
		SetTargetable(true);
		// It comes back already on cooldown rather than already swinging: a revive that opened with
		// a slam would punish the player for standing where the body had been harmless.
	}

	// Called on the other two when the last one falls. Not a public kill switch - it exists so the
	// three deaths happen on one frame.
	private void Silence(int order)
	{
		fallOrder = order;
		downedRemaining = -1f;
		// Back in the group first: base.StartDeath expects to be the thing that takes it out, and
		// it also restores the sprite so there is a corpse to animate.
		SetTargetable(true);
		Health = 0;
		base.StartDeath();
	}

	public override void _Draw()
	{
		base._Draw();

		if (!IsDowned)
			return;

		// The revive clock, drawn as an arc that closes. The player has to be able to see how long
		// is left on a body they cannot hit - otherwise the correct play (switch targets now) is
		// indistinguishable from the wrong one (keep hitting the one that is already down).
		float progress = 1f - Mathf.Clamp(downedRemaining / Mathf.Max(0.01f, ReviveSeconds), 0f, 1f);
		var dim = new Color(ChoirColor.R, ChoirColor.G, ChoirColor.B, 0.30f);
		var bright = new Color(ChoirColor.R, ChoirColor.G, ChoirColor.B, 0.85f);
		DrawArc(Vector2.Zero, DownedRingRadius, 0f, Mathf.Tau, 36, dim, 2.5f, true);
		DrawArc(Vector2.Zero, DownedRingRadius, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * progress, 36, bright, 4f, true);
	}

	private const float DownedRingRadius = 40f;

	// --- The fall (BossDeath) ---------------------------------------------------------------------
	//
	// THE VOICES GO OUT ONE BY ONE. The fight's rule is that the three must fall on the same frame,
	// and they do - the rewards, the group, the victory are all that frame's (StartDeath above). What
	// is staggered is only the silence: the voice struck last sings its last note first, and then the
	// two that had already sunk rise to sing it back, as they have all fight, and cannot. Each holds
	// its note - the hood flares, the song goes out of it in three widening arcs - and then the robe,
	// with nothing in it any more, folds down onto the cave floor and is gone, leaving a ring of wet
	// stone. Cloth and water: sprite_bosses.py's bell of grave linen, and the Sunken Cave.
	private int fallOrder;
	private BossDeath.Piece deathRobe;

	/// <summary>Seconds between one voice going quiet and the next.</summary>
	private const float VoiceTurnSeconds = 0.45f;
	private const float NoteSeconds = 0.36f;

	protected internal override float DeathDelaySeconds => fallOrder * VoiceTurnSeconds;
	protected internal override float DeathSeconds => 1.5f;

	protected internal override void OnDeathBegin(BossDeath death)
	{
		deathRobe = death.CutWhole();
	}

	protected internal override void TickDeath(BossDeath death, float t, float dt)
	{
		if (deathRobe == null)
			return;

		// The note: a breath of the choir's light through the cloth.
		float note = BossDeath.Span(t, 0f, 0.08f) * (1f - BossDeath.Span(t, 0.25f, 0.5f));
		deathRobe.Glow = new Color(ChoirColor, 0.4f * note);

		// Then the robe empties and folds onto the floor, spreading a little as it settles.
		float fold = BossDeath.EaseIn(BossDeath.Span(t, 0.32f, 0.95f));
		deathRobe.Scale = new Vector2(1f + 0.32f * fold, 1f - 0.86f * fold);
		float dim = BossDeath.Span(t, 0.3f, 0.9f);
		deathRobe.Color = new Color(1f - 0.4f * dim, 1f - 0.35f * dim, 1f - 0.25f * dim, 1f - BossDeath.Span(t, 0.95f, 1.4f));

		if (death.Crossed(0.32f))
		{
			ElementColors.Ramp water = ElementColors.GetRamp(Element.Water);
			// What it was made of goes up out of it as it falls...
			death.Emit(new BossDeath.Throw
			{
				Size = 4f, Color = ChoirColor, ColorB = water.Hot, Light = true, NoLand = true,
				SpeedMin = 6f, SpeedMax = 30f, UpMin = 20f, UpMax = 70f, Gravity = -50f, Drag = 1f,
				Life = 1.0f, Fade = 0.6f, Flutter = 3f,
			}, 14, new Vector2(48f, 54f), new Vector2(16f, 30f), 0f, 0.02f);
			// ...and what it was soaked in runs off it.
			death.Emit(new BossDeath.Throw
			{
				Size = 2f, Color = water.Mid, ColorB = water.Edge, SpeedMin = 10f, SpeedMax = 60f,
				UpMin = 10f, UpMax = 60f, Gravity = 520f, Bounce = 0f, Friction = 12f, Life = 0.9f, Fade = 0.4f,
				DepthJitter = 14f,
			}, 12, new Vector2(48f, 70f), new Vector2(22f, 18f));
		}
	}

	// The wet stone it leaves, spreading from the heap: a dark patch with a wetter edge, uneven, and
	// never a ring - a ring round a boss is a telegraph in this game.
	protected internal override void DrawDeathGround(BossDeath death, CanvasItem canvas, float t)
	{
		if (deathRobe == null || t < 0.7f)
			return;
		ElementColors.Ramp water = ElementColors.GetRamp(Element.Water);
		float spread = BossDeath.EaseOut(BossDeath.Span(t, 0.7f, 1.45f));
		float fade = 1f - BossDeath.Span(t, 1.05f, DeathSeconds);
		float r = 22f + 46f * spread;
		var edge = new Color(water.Edge, 0.22f * fade);
		var wet = new Color(water.Edge.R * 0.25f, water.Edge.G * 0.3f, water.Edge.B * 0.45f, 0.45f * fade);
		Vector2 foot = death.Foot;
		BossDeath.Disc(canvas, foot + new Vector2(-r * 0.3f, 0f), r * 0.75f + 4f, r * 0.32f + 3f, edge);
		BossDeath.Disc(canvas, foot + new Vector2(r * 0.28f, 2f), r * 0.6f + 4f, r * 0.28f + 3f, edge);
		BossDeath.Disc(canvas, foot + new Vector2(-r * 0.3f, 0f), r * 0.75f, r * 0.32f, wet);
		BossDeath.Disc(canvas, foot + new Vector2(r * 0.28f, 2f), r * 0.6f, r * 0.28f, wet);
	}

	// The last note: the eye flares, and the song leaves the hood in three arcs either side.
	protected internal override void DrawDeathLight(BossDeath death, CanvasItem canvas, float t)
	{
		if (deathRobe == null || t > 0.9f)
			return;

		Vector2 head = death.PieceToLocal(deathRobe, new Vector2(47.5f, 20f));
		float eye = BossDeath.Span(t, 0f, 0.06f) * (1f - BossDeath.Span(t, 0.2f, 0.5f));
		BossDeath.Disc(canvas, head, 7f, 7f, new Color(ChoirColor, 0.9f * eye));

		for (int k = 0; k < 3; k++)
		{
			float born = k * 0.1f;
			float u = BossDeath.Span(t, born, born + NoteSeconds);
			if (u <= 0f || u >= 1f)
				continue;
			float r = 16f + 70f * BossDeath.EaseOut(u);
			var c = new Color(ChoirColor, 0.85f * (1f - u));
			BossDeath.Arc(canvas, head, r, r * 0.8f, -0.55f, 0.55f, c);
			BossDeath.Arc(canvas, head, r, r * 0.8f, Mathf.Pi - 0.55f, Mathf.Pi + 0.55f, c);
		}
	}
}
