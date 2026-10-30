using Godot;
using WizardSurvivors.scripts;

// THE BOO - the Sketchbook's ghost, and since 2026-10-20 that chapter's second miniboss.
//
// Elim drew it (assets/enemies/booscaryspritesheet.png). At Eric's request it was redrawn bigger
// and scarier to sit with the rest of the game (tools/art/sprite_boo.py) and made a miniboss, where
// it had been a 2.5% opening spawn. It is the one kids' drawing the never-restyle rule was lifted
// for; see CLAUDE.md.
//
// It arrives on the pace clock, twice a run, interleaved with Rockem (Node2DGame.TickGhostMiniBoss),
// and it asks two things of the player that nothing else in the Sketchbook does:
//
//   THE WAIL - stand-off. Up close it stops, rears back and screams: a cold ring is marked round it
//   for a full second, a starburst filling over its head, and everything inside takes the hit when it lands. The answer is to leave. It is
//   the Slammer's lesson on a bigger body, met in a chapter that has no Slammer of its own until
//   Pushy Dude, and its attack animation is timed to the wind-up so the scream is on its face as the
//   ring lands.
//
//   THE PHASE - do not stand still at range. A player who kites it at a distance gets no peace: it
//   turns translucent and untargetable, crosses the ground toward where he is standing, and marks
//   that spot with a ring of its own before it re-forms there. The ring lands before the ghost does
//   (a burrow outlasts the tell it places, exactly as the bosses' do), so the arrival is never itself
//   an unread hit - but a player who ignores the ring under his feet takes it, and then has the
//   ghost on top of him with its wail coming off cooldown.
//
// Built entirely from the shared pieces: GroundSlamAttack for the wail, BurrowMove for the crossing,
// TelegraphedGroundHit for the mark, Enemy.SetTargetable for the untargetable half. Nothing new.
public partial class BooEnemy : Enemy
{
	[ExportGroup("Wail")]
	[Export] public float WailIntervalSeconds { get; set; } = 4.6f;
	// One second, matching the six-frame attack strip at 6 fps (sprite_boo.py). Change one, change both.
	[Export] public float WailWindUpSeconds { get; set; } = 1.0f;
	[Export] public float WailRadius { get; set; } = 150f;
	[Export] public int WailDamage { get; set; } = 4;
	// Above 1, as for the Slammer: it commits while the player is still closing, so the ring is never
	// started round someone already standing in it with no room to get out.
	[Export] public float WailEngageRadiusMultiplier { get; set; } = 1.3f;

	[ExportGroup("Phase")]
	[Export] public float PhaseFirstDelaySeconds { get; set; } = 5f;
	[Export] public float PhaseIntervalSeconds { get; set; } = 9f;
	[Export] public float PhaseTravelSeconds { get; set; } = 1.4f;
	// Lands before the ghost does - RegressionChecks.ValidateGhost holds the two apart.
	[Export] public float PhaseTellSeconds { get; set; } = 1.1f;
	[Export] public float PhaseRadius { get; set; } = 100f;
	[Export] public int PhaseDamage { get; set; } = 4;
	/// <summary>It only phases at someone keeping their distance. Up close the wail is the answer.</summary>
	[Export] public float PhaseMinDistance { get; set; } = 260f;
	/// <summary>And not from off-screen: a ring that arrives before its maker is visible is a surprise, not a tell.</summary>
	[Export] public float PhaseMaxDistance { get; set; } = 900f;
	/// <summary>How much of it shows while it crosses. Enough to track, little enough to read as not there.</summary>
	[Export] public float PhaseAlpha { get; set; } = 0.28f;

	// The cyan of its own inner light (the arcane cyan row, sprite_boo.py): the attack's colour is
	// the colour the ghost already glows, so the warning and the thing that made it read as one.
	[Export] public Color WailColor { get; set; } = new Color(0.416f, 0.722f, 0.910f);

	private GroundSlamAttack wail;
	private BurrowMove phase;
	private float untilPhase;
	private AnimatedSprite2D sprite;

	public override void _Ready()
	{
		base._Ready();
		sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		Rebuild();
	}

	private void Rebuild()
	{
		wail = new GroundSlamAttack(WailIntervalSeconds, WailWindUpSeconds, WailRadius, WailDamage, WailColor)
		{
			GlyphOverride = TelegraphGlyph.Burst,
		};
		phase = new BurrowMove(PhaseTravelSeconds);
		untilPhase = PhaseFirstDelaySeconds;
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		float dt = (float)delta;
		TickPhase(dt);
		TickWail(dt);
	}

	// Planted for the wail, carried for the phase (TickPhase drives position directly), chasing
	// otherwise. Zero also suppresses the separation nudge, so the ring is drawn where it will land.
	protected override Vector2 AdjustSteering(Vector2 chaseDirection, float distanceToPlayer)
	{
		if (wail.IsWindingUp || phase.IsUnder)
			return Vector2.Zero;
		return chaseDirection;
	}

	private void TickWail(float delta)
	{
		Node2D target = TargetPlayer;
		bool inRange = !phase.IsUnder
			&& target != null && IsInstanceValid(target)
			&& GlobalPosition.DistanceTo(target.GlobalPosition) <= WailRadius * WailEngageRadiusMultiplier;

		switch (wail.Tick(delta, inRange))
		{
			case AttackTelegraph.Beat.Started:
				PlayAttackAnimation();
				break;
			case AttackTelegraph.Beat.Resolved:
				wail.ResolveAgainst(this, TargetPlayer);
				break;
		}
	}

	private void TickPhase(float delta)
	{
		switch (phase.Tick(delta))
		{
			case BurrowMove.Beat.Idle:
				untilPhase -= delta;
				if (untilPhase <= 0f && !wail.IsWindingUp)
					TryBeginPhase();
				return;

			case BurrowMove.Beat.Submerged:
				SetTargetable(false);
				// SetTargetable hides the sprite, which is right for a burrower under the floor. A ghost
				// is not under anything: it shows, thinned, so the player can watch it come.
				if (sprite != null)
					sprite.Visible = true;
				Modulate = new Color(1f, 1f, 1f, PhaseAlpha);
				return;

			case BurrowMove.Beat.Travelling:
				// Collision is off while it crosses, so there is nothing for MoveAndSlide to resolve
				// and a straight lerp is exactly what the ring it placed says is happening.
				GlobalPosition = phase.CurrentPosition;
				return;

			case BurrowMove.Beat.Surfaced:
				GlobalPosition = phase.Destination;
				SetTargetable(true);
				Modulate = Colors.White;
				untilPhase = PhaseIntervalSeconds;
				return;
		}
	}

	private void TryBeginPhase()
	{
		Node2D target = TargetPlayer;
		if (target == null || !IsInstanceValid(target))
			return;
		float distance = GlobalPosition.DistanceTo(target.GlobalPosition);
		// Not yet: try again shortly rather than burning the whole interval on a bad moment.
		if (distance < PhaseMinDistance || distance > PhaseMaxDistance)
		{
			untilPhase = 0.5f;
			return;
		}

		// Where he is standing now - aimed once, the frame it begins, and left alone.
		Vector2 destination = target.GlobalPosition;
		phase.Begin(GlobalPosition, destination);
		TelegraphedGroundHit.Place(GetParent(), destination,
			Mathf.Max(0.2f, Mathf.Min(PhaseTellSeconds, PhaseTravelSeconds - 0.1f)),
			PhaseRadius, ScaleOutgoingDamage(PhaseDamage), WailColor);
	}

	protected override void StartDeath()
	{
		// A crossing in progress would keep driving the corpse's position, and a thinned corpse
		// would play its death at a quarter opacity.
		phase?.Abort();
		Modulate = Colors.White;
		base.StartDeath();
	}

	// Elites are never recycled (Enemy._PhysicsProcess skips IsMiniBoss), but the contract is that
	// anything holding a telegraph rebuilds it here, so a future caller cannot bring one back with
	// a wail already banked.
	public override void ResetForRespawn(Vector2 newPos, int newHealth)
	{
		phase?.Abort();
		SetTargetable(true);
		Modulate = Colors.White;
		base.ResetForRespawn(newPos, newHealth);
		Rebuild();
	}

	// The wail's reach is a dashed ring on the floor, under every body (Enemy.DrawGroundTells); the scream
	// building is the starburst over its head. Not while it crosses: a thinned ghost has no wail to wind.
	protected override void DrawGroundTells(CanvasItem floor) => wail?.Draw(floor);

	protected override bool ReadAttackTell(out TelegraphGlyph glyph, out float progress)
	{
		glyph = wail?.Glyph ?? TelegraphGlyph.Burst;
		progress = wail?.WindUpProgress ?? 0f;
		return wail != null && wail.IsWindingUp;
	}
}
