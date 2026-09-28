using Godot;
using WizardSurvivors.scripts;

// The dog. One of the kids drew it and called it "dog companion", so it is one: it follows the
// player through the whole chapter, runs at whatever is nearest, bites it, and cannot be killed.
//
// IT BREAKS A RULE ON PURPOSE, and the rule is worth naming rather than quietly stepping over.
// `.ai/world-and-tone.md` says the player has no allies and no escort - everyone who could help
// is in a cell - which is why `KindledWard` is careful to be a made light rather than a creature,
// with no face, no name and no loyalty. This is a creature, it has a face, and it is loyal.
//
// That is allowed here and nowhere else, because the Sketchbook is not in the fiction. It is a
// page somebody drew, it carries no CorruptionText because the dark wizard has never been there,
// and a chapter that exists outside the story is exactly the right place for the one thing the
// story forbids. Do not use this as a precedent for a campaign chapter.
//
// INVINCIBLE, and it has to be. It has no health, no TakeDamage and it is never in the "enemies"
// group - which is also what makes it untargetable by the player's own spells, for free, since
// every targeting path in this game reads that group. Nothing can kill it and nothing will try.
// A companion that could die would turn a chapter drawn by a child into a thing you can lose.
public partial class KidDog : Node2D
{
	/// <summary>How fast it moves. Slightly above the player, or it falls behind and never catches up.</summary>
	[Export] public float Speed { get; set; } = 210f;

	/// <summary>How far from the player it will chase something before giving up and coming back.</summary>
	[Export] public float LeashRadius { get; set; } = 330f;

	/// <summary>How close it tries to sit when there is nothing to bite.</summary>
	[Export] public float HeelDistance { get; set; } = 56f;

	[Export] public float BiteRadius { get; set; } = 30f;
	[Export] public float BiteIntervalSeconds { get; set; } = 0.55f;
	[Export] public int BiteDamage { get; set; } = 4;

	/// <summary>Seconds between target re-picks. Not per frame: this sweeps the whole swarm.</summary>
	[Export] public float TargetRefreshSeconds { get; set; } = 0.30f;

	private Node2D player;
	private Node2D target;
	private AnimatedSprite2D sprite;
	private float biteTimer;
	private float retargetTimer;
	private float lastFacing = 1f;

	public override void _Ready()
	{
		// Above the ground and the props, below the player: it should never be the thing hiding
		// the player from the player.
		ZIndex = 4;
		sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		sprite?.Play("moving");
		AcquirePlayer();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (player == null || !IsInstanceValid(player))
		{
			AcquirePlayer();
			if (player == null)
				return;
		}

		retargetTimer += dt;
		if (retargetTimer >= TargetRefreshSeconds)
		{
			retargetTimer = 0f;
			AcquireTarget();
		}

		Vector2 goal = ChooseGoal();
		Vector2 offset = goal - GlobalPosition;
		if (offset.Length() > 6f)
		{
			Vector2 step = offset.Normalized() * Speed * dt;
			GlobalPosition += step;
			if (Mathf.Abs(step.X) > 0.01f)
				lastFacing = Mathf.Sign(step.X);
		}

		// The drawing faces RIGHT - tail on the left, ears on the right - like every other actor
		// here, so it flips when moving left and not when moving right. The first version had
		// this backwards on the strength of a comment claiming the art faced left, and the dog
		// ran the whole chapter tail first. Check the sprite, not the comment.
		if (sprite != null)
			sprite.FlipH = lastFacing < 0f;

		TickBite(dt);
	}

	// Where it wants to be this frame: at whatever it is biting, or at the player's heel.
	private Vector2 ChooseGoal()
	{
		if (target != null && IsInstanceValid(target))
			return target.GlobalPosition;

		// Heels at an offset rather than on top of the player, so the two sprites do not sit in
		// the same pixel and read as one smeared object.
		Vector2 away = GlobalPosition - player.GlobalPosition;
		if (away.LengthSquared() < 1f)
			away = Vector2.Right;
		return player.GlobalPosition + away.Normalized() * HeelDistance;
	}

	private void TickBite(float dt)
	{
		biteTimer -= dt;
		if (biteTimer > 0f || target == null || !IsInstanceValid(target))
			return;

		if (GlobalPosition.DistanceTo(target.GlobalPosition) > BiteRadius)
			return;

		if (!target.HasMethod("TakeDamage"))
			return;

		biteTimer = BiteIntervalSeconds;
		// BOTH arguments. Enemy.TakeDamage(int, bool = false) has a C# default, but Godot matches
		// a script method by name AND argument count, so a one-argument Call fails at runtime
		// with "Nonexistent function TakeDamage in base Enemy" and deals nothing. HasMethod
		// returns true either way, which is what makes it silent.
		target.Call("TakeDamage", BiteDamage, false);
		SfxPlayer.AtPosition(SfxCatalog.EnemyMelee, GlobalPosition, -9.0f);
	}

	// Nearest enemy inside the leash, measured from the PLAYER rather than from the dog: a dog
	// that measured from itself would chase one enemy across the arena and then be in range of
	// the next one out there, and so on until it was a dot on the far side of the map.
	private void AcquireTarget()
	{
		target = null;
		float best = float.MaxValue;

		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy || !IsInstanceValid(enemy))
				continue;

			float fromPlayer = player.GlobalPosition.DistanceTo(enemy.GlobalPosition);
			if (fromPlayer > LeashRadius || fromPlayer >= best)
				continue;

			best = fromPlayer;
			target = enemy;
		}
	}

	// Through the group rather than a held reference passed in at spawn: the player node outlives
	// this one on a game over and the reverse on a level reload, and a stale managed wrapper
	// around a freed Godot object is how this project has twice produced a finalizer crash.
	private void AcquirePlayer()
	{
		foreach (Node node in GetTree().GetNodesInGroup("player"))
		{
			if (node is Node2D found && IsInstanceValid(found))
			{
				player = found;
				return;
			}
		}
	}
}
