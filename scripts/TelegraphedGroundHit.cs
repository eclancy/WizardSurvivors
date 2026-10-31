using Godot;
using WizardSurvivors.scripts;

// A single telegraphed hit placed on the ground, away from whoever caused it.
//
// GroundSlamAttack draws on its owner and resolves at the owner's feet, which is right for
// everything an enemy does to the square it is standing on and useless for everything it does to
// a square somewhere else. Falling ice, a bombardment, a rain of arrows - all of them are the same
// object: a warning appears over there, and a moment later that ground is hit.
//
// It is a bare Node2D with no scene, created in code, because there is nothing to author: the
// whole appearance is the warning shape, which GroundSlamAttack already draws, and a .tscn would
// only be a place for the numbers to disagree with the call site.
//
// Deliberately NOT an Area2D. The hit is resolved by measuring, once, against the same shape the
// player was shown - so a hazard cannot claim a pixel it never drew, and there is no collision
// mask to get wrong. It also means a dozen of these cost a dozen draw calls and no physics.
//
// What the player sees (0.9.2: no translucent build-ups): the shape at its full reach as an opaque
// dashed outline on the floor (GroundSlamAttack.Draw, TelegraphOutline), and in its middle the glyph
// an attacker wears over its head (TelegraphIcon) - an arrow onto the ground - filling as the hit
// comes, blinking with the outline in the last third, flashing as it lands. The icon is on a child at
// DrawLayers.AttackIcon, so a hit marked under the swarm is still seen coming.
public partial class TelegraphedGroundHit : Node2D
{
	// The icon's art pixel in world units: an ordinary body's, not a boss's - a volley lays several.
	private const float IconPixelScale = TelegraphIcon.MinPixelScale;

	private GroundSlamAttack shape;
	private readonly TelegraphIcon iconState = new();
	private Node2D icon;
	private bool resolved;
	// Time left before the node frees itself. Set once the hit lands, long enough for the impact
	// flash GroundSlamAttack draws to finish - freeing on the resolve frame would show no impact.
	private float teardownRemaining = -1f;
	private const float TeardownSeconds = 0.34f;

	/// <summary>
	/// Places a hit at <paramref name="where"/> and adds it to <paramref name="arena"/>.
	/// </summary>
	/// <remarks>
	/// Parented to the arena rather than to the caster, for the same reason the summoner parents
	/// its minions out: a boss that dies mid-bombardment must not delete warnings the player is
	/// already reading and dodging.
	/// </remarks>
	public static TelegraphedGroundHit Place(Node arena, Vector2 where, float windUpSeconds, float radius,
		int damage, Color warningColor, TelegraphShape telegraphShape = TelegraphShape.Ring, Vector2 facing = default)
	{
		if (arena == null || !IsInstanceValid(arena))
			return null;

		// On the floor, under every body: a warning at z 0 was sorted in among them by its centre and lay
		// across every body north of it (DrawLayers).
		var hit = new TelegraphedGroundHit { ZAsRelative = false, ZIndex = DrawLayers.GroundTell };
		hit.shape = new GroundSlamAttack(windUpSeconds, windUpSeconds, radius, damage, warningColor)
		{
			Shape = telegraphShape,
			Facing = facing == default ? Vector2.Right : facing,
		};
		// Primed, so the warning appears on the first frame. Without it the telegraph would sit out a
		// full cooldown drawing nothing, and the hit would land at twice the advertised wind-up.
		hit.shape.Prime();
		// Placed after the add: GlobalPosition on a node outside the tree is just Position, so setting
		// it first would silently drop the arena transform if the arena were ever moved off the origin.
		arena.AddChild(hit);
		hit.GlobalPosition = where;
		return hit;
	}

	public override void _Ready()
	{
		icon = new Node2D { Name = "Icon", ZAsRelative = false, ZIndex = DrawLayers.AttackIcon };
		AddChild(icon);
		icon.Draw += PaintIcon;
	}

	public override void _PhysicsProcess(double delta)
	{
		QueueRedraw();
		// Read before the shape ticks, as Enemy reads its own: the icon trails the clock by a step and
		// never touches it.
		bool iconWasShowing = iconState.IsShowing;
		iconState.Tick((float)delta, shape.IsWindingUp, shape.Glyph, shape.WindUpProgress);
		if (iconWasShowing || iconState.IsShowing)
			icon?.QueueRedraw();

		if (teardownRemaining >= 0f)
		{
			teardownRemaining -= (float)delta;
			if (teardownRemaining <= 0f)
				QueueFree();
			return;
		}

		// wantsToAttack is forced true: this object exists only to land one hit, and a gate would
		// just be a way for it to sit on the floor forever.
		if (shape.Tick((float)delta, true) != AttackTelegraph.Beat.Resolved || resolved)
			return;

		resolved = true;
		teardownRemaining = TeardownSeconds;
		ResolveAgainstPlayer();
	}

	// Found through the group rather than a held reference. A reference taken at Place() time would
	// be a managed wrapper around a node this object outlives on a game over, which is exactly the
	// shape that has twice produced a finalizer crash in this project.
	private void ResolveAgainstPlayer()
	{
		foreach (Node node in GetTree().GetNodesInGroup("player"))
		{
			if (node is Node2D target && IsInstanceValid(target))
				shape.ResolveAgainst(this, target);
		}
	}

	public override void _Draw() => shape?.Draw(this);

	// Centred on the spot: the icon's middle is the point the hit is measured from.
	private void PaintIcon() =>
		iconState.Draw(icon, new Vector2(0f, TelegraphIcon.Cell * IconPixelScale * 0.5f), IconPixelScale);
}
