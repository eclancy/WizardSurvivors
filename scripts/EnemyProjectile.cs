using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// The first thing in this game that shoots back. Until now every enemy dealt damage by touching the
// player, so the only positioning rule a run ever taught was "do not stand in the swarm". A bolt
// that crosses open ground adds the second rule: what is between you and them matters.
//
// It is deliberately slow. A ranged attack the player cannot outrun is just delayed contact damage
// with extra steps; this one can be walked out of, which is what makes the caster worth prioritising
// rather than merely worth resenting.
public partial class EnemyProjectile : Area2D
{
	[Export] public float Speed { get; set; } = 235f;
	[Export] public int Damage { get; set; } = 2;
	[Export] public float LifetimeSeconds { get; set; } = 4.0f;
	[Export] public float Radius { get; set; } = 8.0f;

	/// <summary>
	/// True when this bolt has an AnimatedSprite2D child to show instead of the drawn geometry.
	/// </summary>
	/// <remarks>
	/// The drawn bolt exists because it has to read on grass, snow, sand and black stone alike,
	/// and that argument holds for every projectile the dark wizard throws. It does not hold for
	/// the Sketchbook, whose whole point is that the art in it was drawn by children and is shown
	/// as drawn - a generated halo painted over the top of their missile would be the one thing
	/// that chapter must not do.
	/// </remarks>
	[Export] public bool UseSpriteArt { get; set; }

	// Set by whatever fired it, so a future second caster can share the scene and still read as its
	// own attack. Defaults to the cultist's violet.
	public Color CoreColor { get; set; } = new Color(0.85f, 0.62f, 1.0f);
	public Color BodyColor { get; set; } = new Color(0.55f, 0.24f, 0.86f);

	// EVERY BOLT IN FLIGHT, for the spells that guard against them (2026-10-28, Eric: "a few spells
	// should help guard you from projectiles, like familiar being able to catch projectiles"). The
	// Familiar's hounds catch one aimed at the wizard; Solar Flare's Daybreak Avatar burns every one
	// inside the flare; Spiritual Weapon's blades cut the ones they pass through. Kept by entering and
	// leaving the tree - the list a guard walks is never built per frame, and there are only ever a
	// handful (one caster's volley is five).
	private static readonly List<EnemyProjectile> live = new();

	/// <summary>The bolts in flight. Read it; never hold it across frames - a destroyed bolt leaves on tree exit.</summary>
	public static IReadOnlyList<EnemyProjectile> Live => live;

	/// <summary>Where it is heading, unit length.</summary>
	public Vector2 Heading => direction;

	/// <summary>True until it has hit, been destroyed, or worn out: whether a guard may still take it.</summary>
	public bool InFlight => !spent && IsInsideTree();

	/// <summary>A guard that has gone for this bolt, so a second does not chase the same one.</summary>
	public object ClaimedBy { get; set; }

	private Vector2 direction = Vector2.Right;
	private float lifetime;
	// One hit, then gone. The player's body and its HurtBox can both be in range on the same frame,
	// and a bolt that charged twice for one dodge-able shot would make the tell a lie.
	private bool spent;

	public override void _Ready()
	{
		// In the air, over the bodies it flies past, so a bolt is never hidden in the crowd that fired
		// it (DrawLayers).
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		// Mask bit 1 is the player's body, the same way StageHazard finds it. Layer 0 so nothing
		// else - the player's own spells especially - can ever collide with an enemy bolt.
		CollisionLayer = 0;
		CollisionMask = 1;

		var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (shape?.Shape is CircleShape2D circle)
			circle.Radius = Radius;

		BodyEntered += OnBodyEntered;
	}

	public override void _EnterTree()
	{
		live.Add(this);
	}

	public override void _ExitTree()
	{
		live.Remove(this);
		BodyEntered -= OnBodyEntered;
	}

	/// <summary>
	/// Taken out of the air by a guard: no damage, a burst of the guard's element where it was, gone.
	/// </summary>
	public void Destroy(Element guardElement)
	{
		if (spent)
			return;
		spent = true;
		if (GetParent() is Node2D arena)
		{
			ElementColors.Ramp ramp = ElementColors.GetRamp(guardElement);
			ImpactDebrisLayer.Spawn(arena, GlobalPosition, Radius * 3f, ramp, Colors.White);
		}
		QueueFree();
	}

	/// <summary>Aims the bolt from <paramref name="from"/> at <paramref name="towards"/> and starts it moving.</summary>
	public void Launch(Vector2 from, Vector2 towards) => LaunchInDirection(from, towards - from);

	/// <summary>
	/// Aims the bolt along a heading rather than at a point. This is the one a fanned volley wants:
	/// only its middle bolt is pointed at the player, and the rest are rotations of that heading.
	/// </summary>
	public void LaunchInDirection(Vector2 from, Vector2 heading)
	{
		GlobalPosition = from;
		direction = heading.LengthSquared() > 0.0001f ? heading.Normalized() : Vector2.Right;
		// The tail is drawn along local -X, so rotating the whole node keeps it behind the bolt
		// without recomputing the shape every frame.
		Rotation = direction.Angle();
	}

	public override void _PhysicsProcess(double delta)
	{
		GlobalPosition += direction * Speed * (float)delta;

		// Stone stops a bolt. The caster already checks line of sight before it fires, so this only
		// catches the case where the player ducked behind a wall mid-flight - which is the whole
		// point of having walls. Water does not stop it, and on a stage with no walls this is one
		// integer test.
		if (MazeNavigation.Active?.IsWallAt(GlobalPosition) == true)
		{
			QueueFree();
			return;
		}

		lifetime += (float)delta;
		if (lifetime >= LifetimeSeconds)
			QueueFree();
	}

	private void OnBodyEntered(Node2D body)
	{
		if (spent || !body.IsInGroup("player") || !body.HasMethod("TakeDamage"))
			return;

		// Caught in the jaws (Laelaps's passive, Player.TryCatchBolt): taken out of the air as a guard
		// takes one, with no hit at all.
		if (body is Player catcher && catcher.TryCatchBolt())
		{
			Destroy(Element.Earth);
			return;
		}

		spent = true;
		// Zero when a weakened shooter's roll came up empty (Enemy.ScaleOutgoingDamage, issue #66).
		// The bolt still breaks on the player; it just lands like a dodge.
		if (Damage > 0)
			body.Call("TakeDamage", Damage);
		QueueFree();
	}

	public override void _Draw()
	{
		if (UseSpriteArt)
			return;

		// Drawn rather than sprited because it has to read on grass, snow, sand and black stone
		// alike: a dark halo separates it from bright ground, a near-white core from dark ground.
		// Static geometry, so this runs once per bolt rather than once per frame.
		DrawCircle(Vector2.Zero, Radius * 1.55f, new Color(0.06f, 0.02f, 0.12f, 0.45f));

		// A short tapered tail, three fading discs rather than a polygon - cheap, and it survives
		// the sub-pixel scaling the camera does at different zooms.
		for (int i = 1; i <= 3; i++)
		{
			float t = i / 4.0f;
			var tail = new Color(BodyColor.R, BodyColor.G, BodyColor.B, 0.42f * (1f - t));
			DrawCircle(new Vector2(-Radius * 1.1f * i, 0f), Radius * (1f - t * 0.55f), tail);
		}

		DrawCircle(Vector2.Zero, Radius, BodyColor);
		DrawCircle(Vector2.Zero, Radius * 0.46f, CoreColor);
	}
}
