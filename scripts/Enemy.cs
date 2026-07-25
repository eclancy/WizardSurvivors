using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
	private Vector2 knockbackVelocity = Vector2.Zero;
	private float knockbackTime = 0f;
	private const float KnockbackDuration = 0.45f;
	private float shockTimeRemaining = 0f;
	private float shockAmplitude = 0f;
	private float shockFrequency = 0f;
	private float shockPhase = 0f;
	// Slow/root status (issue #16/#22 defensive spells): multiplier 0 = fully rooted.
	private float slowMultiplier = 1f;
	private float slowTimeRemaining = 0f;
	// Poison status: flat damage per tick while poisonTimeRemaining > 0.
	private int poisonDamagePerTick = 0;
	private float poisonTimeRemaining = 0f;
	private float poisonTickTimer = 0f;
	private const float PoisonTickInterval = 1.0f;
	// Small per-enemy movement variation so paths are less robotic
	private float wanderPhase = 0f;
	private float wanderFrequency = 1f;
	private float wanderStrength = 0.25f;
	private RandomNumberGenerator rng = new RandomNumberGenerator();
	[Export] public float Speed { get; set; } = 125f;
	[Export] public int Health { get; set; } = 20;
	[Export] public string EnemyType { get; set; } = "Enemy";
	[Export] public float RespawnDistance { get; set; } = 1600f;
	[Export] public bool SpriteFacesRightByDefault { get; set; } = true;
	[Export] public float MinPlayerSeparation { get; set; } = 20f;
	[Export] public float OverlapResolveSpeed { get; set; } = 230f;
	[Export] public bool IgnoresDecorCollision { get; set; } = false;
	[Export] public bool IsMiniBoss { get; set; } = false;

	private Node2D? player;
	private AnimatedSprite2D? animatedSprite;
	private Sprite2D? sprite;
	private int maxHealth = 0;
	private PackedScene floatingTextScene = ResourceLoader.Load<PackedScene>("res://scenes/FloatingText.tscn");
	private PackedScene xpOrbScene = ResourceLoader.Load<PackedScene>("res://scenes/XPOrb.tscn");
	// Bonus Drop Table (issue #25): rare extra drops on death, chance scaled by the player's Luck stat.
	private PackedScene healthPickupScene = ResourceLoader.Load<PackedScene>("res://scenes/HealthPickup.tscn");
	private PackedScene buffItemScene = ResourceLoader.Load<PackedScene>("res://scenes/BuffItem.tscn");
	private PackedScene levelUpPickupScene = ResourceLoader.Load<PackedScene>("res://scenes/LevelUpPickup.tscn");
	private Tween? hitFlashTween;
	// The sprite's original modulate (e.g. per-type or elite tint). The hit-flash restores to this
	// instead of white so damaged enemies keep the color that signals their strength/type.
	private Color baseModulate = Colors.White;
	private Vector2 animatedSpriteBasePosition = Vector2.Zero;
	private Vector2 spriteBasePosition = Vector2.Zero;

	public override void _Ready()
	{
		maxHealth = Health;
		AddToGroup("enemies");
		ConfigureEntityCollision();
		ConfigureDecorCollisionExceptions();
		player = GetParent().GetNodeOrNull<Node2D>("CharacterBody2D");
		animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
		if (animatedSprite != null)
		{
			animatedSpriteBasePosition = animatedSprite.Position;
			if (!animatedSprite.IsPlaying())
			{
				StringName animToPlay = animatedSprite.Animation;
				if (animToPlay == default)
					animatedSprite.Play();
				else
					animatedSprite.Play(animToPlay);
			}
		}
		if (sprite != null)
			spriteBasePosition = sprite.Position;
		CanvasItem? visual = (CanvasItem?)animatedSprite ?? sprite;
		if (visual != null)
			baseModulate = visual.Modulate;
		SetProcess(true);
		SetPhysicsProcess(true);
		// Initialize per-enemy wander parameters
		rng.Randomize();
		wanderPhase = rng.Randf() * Mathf.Tau;
		wanderFrequency = rng.RandfRange(0.8f, 1.5f);
		wanderStrength = rng.RandfRange(0.1f, 0.35f);
	}

	private void ConfigureEntityCollision()
	{
		SetCollisionLayerValue(2, true);
		SetCollisionMaskValue(1, true);
		SetCollisionMaskValue(2, true);
	}

	private void ConfigureDecorCollisionExceptions()
	{
		if (!IgnoresDecorCollision)
			return;

		foreach (Node node in GetTree().GetNodesInGroup("decor_props"))
		{
			if (node is CollisionObject2D collisionObject)
				AddCollisionExceptionWith(collisionObject);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		QueueRedraw();
		bool shocked = shockTimeRemaining > 0f;
		if (shocked)
		{
			shockTimeRemaining = Math.Max(0f, shockTimeRemaining - (float)delta);
			Velocity = Vector2.Zero;
			UpdateShockVisual((float)delta);
		}
		else if (knockbackTime > 0f)
		{
			float decayThreshold = KnockbackDuration * 0.3f;
			if (knockbackTime > decayThreshold)
			{
				// First 70%: constant velocity
				Velocity = knockbackVelocity;
			}
			else
			{
				// Last 30%: linearly decay velocity to zero
				float t = knockbackTime / decayThreshold; // t goes from 1 to 0
				Velocity = knockbackVelocity * t;
			}
			knockbackTime -= (float)delta;
			if (knockbackTime <= 0f)
			{
				knockbackVelocity = Vector2.Zero;
				knockbackTime = 0f;
			}
		}
		else if (player != null && IsInstanceValid(player))
		{
			Vector2 playerOffset = player.GlobalPosition - GlobalPosition;
			float distanceToPlayer = playerOffset.Length();

			// If an enemy drifts inside the player's center/footprint, push it back out so enemies
			// cannot remain stuck intersecting the player indefinitely.
			if (distanceToPlayer < MinPlayerSeparation)
			{
				Vector2 escapeDir = distanceToPlayer > 0.001f
					? -playerOffset / distanceToPlayer
					: new Vector2(Mathf.Cos(wanderPhase), Mathf.Sin(wanderPhase));
				Velocity = escapeDir * OverlapResolveSpeed;
			}
			else
			{
				var toPlayer = playerOffset / Math.Max(distanceToPlayer, 0.001f);
				// Add a small, smooth side-to-side component so enemies don't move in a perfectly straight line.
				wanderPhase += (float)delta * wanderFrequency;
				float offset = Mathf.Sin(wanderPhase) * wanderStrength;
				var lateral = new Vector2(-toPlayer.Y, toPlayer.X);
				var variedDir = (toPlayer + lateral * offset).Normalized();
				Velocity = variedDir * Speed * slowMultiplier;
			}
		}
		MoveAndSlide();
		if (!shocked)
			UpdateShockVisual((float)delta);

		if (slowTimeRemaining > 0f)
		{
			slowTimeRemaining -= (float)delta;
			if (slowTimeRemaining <= 0f)
			{
				slowTimeRemaining = 0f;
				slowMultiplier = 1f;
			}
		}

		if (poisonTimeRemaining > 0f)
		{
			poisonTimeRemaining -= (float)delta;
			poisonTickTimer += (float)delta;
			if (poisonTickTimer >= PoisonTickInterval)
			{
				poisonTickTimer = 0f;
				if (poisonDamagePerTick > 0)
					TakeDamage(poisonDamagePerTick);
			}
			if (poisonTimeRemaining <= 0f)
			{
				poisonTimeRemaining = 0f;
				poisonDamagePerTick = 0;
			}
		}
		if (player != null && IsInstanceValid(player))
		{
			var dist = player.GlobalPosition.DistanceTo(GlobalPosition);
			if (dist > RespawnDistance)
			{
				var scene = GetTree().CurrentScene as Node;
				if (scene != null && scene.HasMethod("respawn_enemy"))
				{
					scene.CallDeferred("respawn_enemy", this);
				}
			}
		}

		UpdateFacing();
	}

	private void UpdateFacing()
	{
		float facingX = 0f;
		if (player != null && IsInstanceValid(player))
			facingX = player.GlobalPosition.X - GlobalPosition.X;
		else
			facingX = Velocity.X;

		if (Mathf.Abs(facingX) < 0.001f)
			return;

		bool shouldFaceRight = facingX > 0f;
		bool flip = SpriteFacesRightByDefault ? !shouldFaceRight : shouldFaceRight;

		if (animatedSprite != null)
			animatedSprite.FlipH = flip;
		if (sprite != null)
			sprite.FlipH = flip;
	}
	public void ApplyKnockback(Vector2 force)
	{
		knockbackVelocity = force;
		knockbackTime = KnockbackDuration;
	}

	public void ApplyShock(float duration, float shakeAmplitude = 3.5f, float shakeFrequency = 42f)
	{
		shockTimeRemaining = Math.Max(shockTimeRemaining, duration);
		shockAmplitude = Math.Max(0.5f, shakeAmplitude);
		shockFrequency = Math.Max(1f, shakeFrequency);
		shockPhase = rng.RandfRange(0f, Mathf.Tau);
	}

	// Slows (or, at multiplier 0, roots/freezes) the enemy for `duration` seconds. Refreshes to the
	// stronger effect and the longer remaining duration if already active (issue #16/#22).
	public void ApplySlow(float multiplier, float duration)
	{
		multiplier = Mathf.Clamp(multiplier, 0f, 1f);
		if (slowTimeRemaining <= 0f || multiplier < slowMultiplier)
			slowMultiplier = multiplier;
		slowTimeRemaining = Mathf.Max(slowTimeRemaining, duration);
	}

	// Applies a stacking-resistant poison DoT: takes the stronger tick damage and the longer
	// remaining duration (issue #16/#22).
	public void ApplyPoison(int damagePerTick, float duration)
	{
		poisonDamagePerTick = Math.Max(poisonDamagePerTick, damagePerTick);
		poisonTimeRemaining = Mathf.Max(poisonTimeRemaining, duration);
	}
	// 	var rng = new RandomNumberGenerator();
	// 	rng.Randomize();
	// 					var angle = rng.Randf() * (Mathf.Pi * 2.0f);
	// 	var radius = rng.RandfRange(250f, 800f);
	// 	var newPos = player.GlobalPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
	// 	ResetForRespawn(newPos, maxHealth);
	// }
	// 			}
	// 		}
	// 	}

	public void TakeDamage(int amount, bool isCrit = false)
	{
		if (amount > 0)
			GameStats.RecordDamageDealt(amount);

		PlayHitFeedback(isCrit);

		// Show floating damage text
		if (floatingTextScene != null)
		{
			var textNode = floatingTextScene.Instantiate<Node2D>();
			Color color = isCrit ? new Color(1f, 0.85f, 0.1f, 1f) : new Color(1, 0.2f, 0.2f, 1); // Yellow for crits, red otherwise
			string text = isCrit ? $"{amount}!" : amount.ToString();
			if (textNode is FloatingText ft)
			{
				ft.Text = text;
				ft.Color = color;
				ft.GlobalPosition = this.GlobalPosition;
				if (isCrit)
					ft.Scale *= 1.4f;
			}
			else
			{
				textNode.Set("Text", text);
				textNode.Set("Color", color);
				textNode.Set("GlobalPosition", this.GlobalPosition);
			}
			// Add to the enemy's parent so it persists after enemy is freed
			GetParent().AddChild(textNode);
		}
		Health -= amount;
		if (Health <= 0)
		{
			if (HasSignal("killed"))
				EmitSignal("killed");
			DropXp();
			if (IsMiniBoss)
				DropLevelUpPickup();
			TryDropBonusItem();
			// Defer freeing so FloatingText can show up for at least one frame
			CallDeferred("queue_free");
		}
	}

	private void PlayHitFeedback(bool isCrit)
	{
		CanvasItem target = (CanvasItem?)animatedSprite ?? sprite;
		if (target == null)
			return;

		hitFlashTween?.Kill();
		// Blend the flash over the enemy's base tint so its type/elite color still reads during the flash,
		// then restore to that base tint (not white) so the color is never permanently lost.
		Color flashColor = isCrit ? new Color(1.0f, 0.9f, 0.45f, 1.0f) : new Color(1.0f, 0.55f, 0.55f, 1.0f);
		target.Modulate = baseModulate * flashColor;

		Vector2 baseScale = Scale;
		Scale = baseScale * (isCrit ? 1.08f : 1.04f);

		hitFlashTween = CreateTween();
		hitFlashTween.SetParallel(true);
		hitFlashTween.TweenProperty(target, "modulate", baseModulate, 0.1f);
		hitFlashTween.TweenProperty(this, "scale", baseScale, 0.1f);
	}

	private void DropLevelUpPickup()
	{
		if (levelUpPickupScene == null)
			return;

		var item = levelUpPickupScene.Instantiate<Node2D>();
		item.GlobalPosition = GlobalPosition;
		var scene = GetTree().CurrentScene as Node;
		if (scene != null)
			scene.CallDeferred("add_child", item);
		else
			GetTree().Root.CallDeferred("add_child", item);
	}

	// Bonus Drop Table (issue #25): after the guaranteed XP orb, roll a separate low chance for one
	// extra item - a health pickup, a one-time buff item, or a bonus/mega XP orb.
	private void TryDropBonusItem()
	{
		float chance = (player as Player)?.GetBonusDropChance() ?? 0.04f;
		if (rng.Randf() >= chance)
			return;

		int roll = rng.RandiRange(0, 2);
		PackedScene sceneToSpawn = roll switch
		{
			0 => healthPickupScene,
			1 => buffItemScene,
			_ => xpOrbScene
		};
		if (sceneToSpawn == null)
			return;

		var item = sceneToSpawn.Instantiate<Node2D>();
		item.GlobalPosition = GlobalPosition;
		if (roll == 2 && item is XPOrb bonusOrb)
		{
			bonusOrb.Value *= 8;
			bonusOrb.Scale *= 1.6f;
		}

		var scene = GetTree().CurrentScene as Node;
		if (scene != null)
			scene.CallDeferred("add_child", item);
		else
			GetTree().Root.CallDeferred("add_child", item);
	}

	private void DropXp()
	{
		if (xpOrbScene != null)
		{
			var orb = xpOrbScene.Instantiate<Node2D>();
			orb.GlobalPosition = GlobalPosition;
			var scene = GetTree().CurrentScene as Node;
			if (scene != null && scene.HasMethod("AddXp"))
			{
				var cb = new Callable(scene, "AddXp");
				if (orb.HasSignal("picked_up"))
					orb.Connect("picked_up", cb);
			}
			if (scene != null)
				scene.CallDeferred("add_child", orb);
			else
				GetTree().Root.CallDeferred("add_child", orb);
		}
	}

	public void ResetForRespawn(Vector2 newPos, int newHealth)
	{
		GlobalPosition = newPos;
		Health = newHealth;
		maxHealth = newHealth;
		Velocity = Vector2.Zero;
		QueueRedraw();
	}

	private void UpdateShockVisual(float delta)
	{
		Vector2 offset = Vector2.Zero;
		if (shockTimeRemaining > 0f)
		{
			shockPhase += delta * shockFrequency;
			float decay = MathF.Max(0.25f, shockTimeRemaining / MathF.Max(0.001f, shockAmplitude));
			offset = new Vector2(Mathf.Sin(shockPhase * 2.0f), Mathf.Cos(shockPhase * 2.7f)) * shockAmplitude * decay * 0.18f;
		}

		if (animatedSprite != null)
			animatedSprite.Position = animatedSpriteBasePosition + offset;
		if (sprite != null)
			sprite.Position = spriteBasePosition + offset;
	}
}
