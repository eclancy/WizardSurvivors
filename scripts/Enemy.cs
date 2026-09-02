using Godot;
using System;
using WizardSurvivors.scripts;

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
	private Vector2 cachedSeparation = Vector2.Zero;
	private const ulong SeparationUpdateInterval = 4;
	private RandomNumberGenerator rng = new RandomNumberGenerator();
	[Export] public float Speed { get; set; } = 125f;
	[Export] public int Health { get; set; } = 20;
	[Export] public string EnemyType { get; set; } = "Enemy";
	[Export] public float RespawnDistance { get; set; } = 1600f;
	[Export] public bool SpriteFacesRightByDefault { get; set; } = true;
	[Export] public float MinPlayerSeparation { get; set; } = 20f;
	[Export] public float OverlapResolveSpeed { get; set; } = 230f;
	[Export] public float EnemySpacingRadius { get; set; } = 44f;
	[Export] public float EnemySpacingStrength { get; set; } = 95f;
	[Export] public float PathNoiseStrength { get; set; } = 0.16f;
	[Export] public bool IgnoresDecorCollision { get; set; } = false;
	[Export] public bool IsMiniBoss { get; set; } = false;
	// Contact damage is dealt by the player's overlap loop, not by the enemy, so this is the value
	// it reads back (duck-typed). 1 keeps every existing enemy exactly as it was; a boss raises it
	// so its melee actually hurts.
	[Export] public int ContactDamage { get; set; } = 1;
	// 0 = knocked around like anything else, 1 = immovable. A boss that skids across the arena on
	// every hit stops reading as a boss.
	[Export] public float KnockbackResistance { get; set; } = 0f;
	// Floor for ApplySlow. Defaults to 0 so ordinary enemies can still be frozen solid; a boss
	// raises it so a freeze build cannot simply park it for the whole fight.
	[Export] public float MinSlowMultiplier { get; set; } = 0f;
	public float HealthFraction => maxHealth > 0 ? Mathf.Clamp(Health / (float)maxHealth, 0f, 1f) : 1f;
	public int MaxHealth => maxHealth;

	private Node2D? player;
	// Subclasses (BossEnemy) need the same target this one steers toward, without re-resolving it.
	protected Node2D? TargetPlayer => player;
	private AnimatedSprite2D? animatedSprite;
	private Sprite2D? sprite;
	private int maxHealth = 0;
	private PackedScene floatingTextScene = ResourceLoader.Load<PackedScene>("res://scenes/FloatingText.tscn");
	private PackedScene xpOrbScene = ResourceLoader.Load<PackedScene>("res://scenes/XPOrb.tscn");
	// Bonus Drop Table (issue #25): rare extra drops on death, chance scaled by the player's Luck stat.
	private PackedScene healthPickupScene = ResourceLoader.Load<PackedScene>("res://scenes/HealthPickup.tscn");
	private PackedScene buffItemScene = ResourceLoader.Load<PackedScene>("res://scenes/BuffItem.tscn");
	private Tween? hitFlashTween;
	// The sprite's original modulate (e.g. per-type or elite tint). The hit-flash restores to this
	// instead of white so damaged enemies keep the color that signals their strength/type.
	private Color baseModulate = Colors.White;
	private Vector2 animatedSpriteBasePosition = Vector2.Zero;
	private Vector2 spriteBasePosition = Vector2.Zero;
	// Set once health hits 0. The enemy stays in the tree for the length of its death animation,
	// but leaves the "enemies" group and drops its collision immediately so it can no longer be
	// targeted, damaged, or bump the player while the corpse plays out.
	private bool isDying = false;

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
		if (animatedSprite != null)
			animatedSprite.AnimationFinished += OnOneShotAnimationFinished;
		ConfigureEliteMarker();
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

				// In a maze, follow the shared wall-aware flow field around walls instead of
				// steering straight at the player (which would wedge enemies against walls).
				var primaryDir = toPlayer;
				var nav = MazeNavigation.Active;
				if (nav != null)
				{
					Vector2 flow = nav.FlowDirectionAt(GlobalPosition);
					if (flow != Vector2.Zero)
						primaryDir = flow;
				}

				wanderPhase += (float)delta * wanderFrequency;
				float offset = Mathf.Sin(wanderPhase) * wanderStrength;
				float noiseOffset = Mathf.Sin(wanderPhase * 1.35f + wanderStrength * 2.2f) * PathNoiseStrength;
				var lateral = new Vector2(-primaryDir.Y, primaryDir.X);
				var variedDir = (primaryDir + lateral * offset + lateral * noiseOffset).Normalized();

				if ((Engine.GetPhysicsFrames() + GetInstanceId()) % SeparationUpdateInterval == 0)
				{
					cachedSeparation = Vector2.Zero;
					float spacingRadiusSquared = EnemySpacingRadius * EnemySpacingRadius;
					foreach (Node node in GetTree().GetNodesInGroup("enemies"))
					{
						if (node is not Enemy enemy || enemy == this || !IsInstanceValid(enemy))
							continue;

						Vector2 offsetFromEnemy = GlobalPosition - enemy.GlobalPosition;
						float distanceSquared = offsetFromEnemy.LengthSquared();
						if (distanceSquared > spacingRadiusSquared || distanceSquared <= 0.001f)
							continue;

						float distance = Mathf.Sqrt(distanceSquared);
						Vector2 pushDir = offsetFromEnemy / distance;
						float weight = 1f - (distance / EnemySpacingRadius);
						cachedSeparation += pushDir * weight * EnemySpacingStrength;
					}
				}

				if (cachedSeparation.LengthSquared() > 0.001f)
				{
					var separationDir = cachedSeparation.Normalized();
					var combinedDir = (variedDir + separationDir * 0.35f).Normalized();
					Velocity = combinedDir * Speed * slowMultiplier;
				}
				else
				{
					Velocity = variedDir * Speed * slowMultiplier;
				}
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
		if (isDying)
			return;

		knockbackVelocity = force * (1f - Mathf.Clamp(KnockbackResistance, 0f, 1f));
		knockbackTime = KnockbackDuration;
	}

	public void ApplyShock(float duration, float shakeAmplitude = 3.5f, float shakeFrequency = 42f)
	{
		if (isDying)
			return;

		shockTimeRemaining = Math.Max(shockTimeRemaining, duration);
		shockAmplitude = Math.Max(0.5f, shakeAmplitude);
		shockFrequency = Math.Max(1f, shakeFrequency);
		shockPhase = rng.RandfRange(0f, Mathf.Tau);
	}

	// Slows (or, at multiplier 0, roots/freezes) the enemy for `duration` seconds. Refreshes to the
	// stronger effect and the longer remaining duration if already active (issue #16/#22).
	public void ApplySlow(float multiplier, float duration)
	{
		if (isDying)
			return;

		multiplier = Mathf.Clamp(multiplier, Mathf.Clamp(MinSlowMultiplier, 0f, 1f), 1f);
		if (slowTimeRemaining <= 0f || multiplier < slowMultiplier)
			slowMultiplier = multiplier;
		slowTimeRemaining = Mathf.Max(slowTimeRemaining, duration);
	}

	// Applies a stacking-resistant poison DoT: takes the stronger tick damage and the longer
	// remaining duration (issue #16/#22).
	public void ApplyPoison(int damagePerTick, float duration)
	{
		if (isDying)
			return;

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
		if (isDying)
			return;

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
			StartDeath();
	}

	// Drops rewards immediately, then plays the death animation if this enemy has one before
	// freeing. Rewards must not wait on the animation - the player should never lose XP because
	// a corpse was still animating when the run ended.
	protected virtual void StartDeath()
	{
		if (isDying)
			return;

		isDying = true;

		if (HasSignal("killed"))
			EmitSignal("killed");
		DropXp();
		TryDropBonusItem();

		// Leave the group first so AoE sweeps and targeting skip the corpse this same frame.
		if (IsInGroup("enemies"))
			RemoveFromGroup("enemies");
		Velocity = Vector2.Zero;
		CollisionLayer = 0;
		CollisionMask = 0;
		SetPhysicsProcess(false);

		if (animatedSprite != null && animatedSprite.SpriteFrames != null
			&& animatedSprite.SpriteFrames.HasAnimation("death"))
		{
			// Restore the base tint so a hit flash mid-death doesn't freeze the corpse red.
			animatedSprite.Modulate = baseModulate;
			hitFlashTween?.Kill();
			animatedSprite.AnimationFinished += OnDeathAnimationFinished;
			animatedSprite.Play("death");
			return;
		}

		// No death animation (e.g. BooEnemy): defer freeing so FloatingText survives a frame.
		CallDeferred("queue_free");
	}

	private void OnDeathAnimationFinished()
	{
		QueueFree();
	}

	// Every enemy sheet in the pack ships an "attack" and a "take-damage" strip that this project
	// never wired up, so enemies used to slide into the player and shrug off arrows with the same
	// looping walk cycle. These two play those strips as one-shots over the walk.
	//
	// Enemies whose SpriteFrames lack the animation - BooEnemy, SlowEnemy - fall through
	// untouched; HasAnimation is the guard, the same way StartDeath already handles a missing
	// death animation.
	private bool oneShotAnimationPlaying;

	/// <summary>Plays the attack swing. Called by the player when this enemy lands a contact hit.</summary>
	public void PlayAttackAnimation() => PlayOneShotAnimation("attack");

	// Elites were signalled by scale and tint alone, which is easy to miss in a swarm where every
	// type is already a different size and colour. A gold ring underfoot is unambiguous.
	//
	// This reuses the shield-ring sheet rather than a dedicated sprite. The obvious alternative -
	// swapping elites onto the dungeon pack's v2 character sprites - was rejected: those sets are
	// four-frame *idles* only, so an elite wearing one would lose its walk, attack, hurt and death
	// animations. A marker is a smaller change and strictly more information.
	private void ConfigureEliteMarker()
	{
		if (!IsMiniBoss)
			return;

		var frames = GD.Load<SpriteFrames>("res://scenes/resources/ShieldAuraFrames.tres");
		if (frames == null)
			return;

		var marker = new AnimatedSprite2D
		{
			Name = "EliteMarker",
			SpriteFrames = frames,
			Animation = "active",
			// Under the enemy, so a dense pack still shows a ring per elite.
			ZIndex = -1,
			Scale = new Vector2(0.55f, 0.32f),
			Position = new Vector2(0f, 8f),
			Modulate = new Color(1.0f, 0.82f, 0.30f, 0.75f)
		};
		AddChild(marker);
		marker.Play("active");
	}

	private void PlayOneShotAnimation(StringName animation)
	{
		// Never override the death animation: a corpse mid-collapse must not flinch.
		if (isDying || oneShotAnimationPlaying || animatedSprite?.SpriteFrames == null)
			return;
		if (!animatedSprite.SpriteFrames.HasAnimation(animation))
			return;

		oneShotAnimationPlaying = true;
		animatedSprite.Play(animation);
	}

	private void OnOneShotAnimationFinished()
	{
		// StartDeath connects its own handler for "death"; this one must keep out of its way.
		if (isDying)
			return;

		oneShotAnimationPlaying = false;
		if (animatedSprite?.SpriteFrames?.HasAnimation("moving") == true)
			animatedSprite.Play("moving");
	}

	public override void _ExitTree()
	{
		if (animatedSprite != null && IsInstanceValid(animatedSprite))
			animatedSprite.AnimationFinished -= OnOneShotAnimationFinished;
	}

	// Retints the enemy permanently. Goes through baseModulate rather than the sprite alone so the
	// hit flash restores to the new colour instead of snapping back to the old one - a boss that
	// darkens on enrage must stay dark for the rest of the fight.
	protected void SetBaseModulate(Color color)
	{
		baseModulate = color;
		CanvasItem target = (CanvasItem?)animatedSprite ?? sprite;
		if (target != null)
			target.Modulate = color;
	}

	private void PlayHitFeedback(bool isCrit)
	{
		PlayOneShotAnimation("hurt");

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
			if (orb is XPOrb xpOrb)
			{
				if (IsMiniBoss)
					xpOrb.Value = 50;
				else if (EnemyType.Equals("Tank", StringComparison.OrdinalIgnoreCase) || EnemyType.Equals("TankEnemy", StringComparison.OrdinalIgnoreCase))
					xpOrb.Value = 5;
				else if (EnemyType.Equals("Fast", StringComparison.OrdinalIgnoreCase) || EnemyType.Equals("FastEnemy", StringComparison.OrdinalIgnoreCase))
					xpOrb.Value = 3;
				else
					xpOrb.Value = 2;
			}
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
