using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts
{
	public partial class ArcaneExplosion : Area2D
	{
		[Export] public SpellData SpellData { get; set; }
		[Export] public int CurrentLevel { get; set; } = 1;
		// Charge-driven, not timed. Player counts casts of the OTHER five spells and detonates
		// this when the count comes due - see Player.NotifyArcaneCharge. Left as an export so the
		// old behaviour is one checkbox away if the charge version does not survive playtesting.
		[Export] public bool ChargeDriven { get; set; } = true;

		[Export] public float BaseKnockbackRange { get; set; } = 100f;
		[Export] public float BaseKnockbackSpeed { get; set; } = 2.0f;
		public float DamageMultiplier { get; set; } = 1.0f;
		public float AreaMultiplier { get; set; } = 1.0f;
		public float CooldownMultiplier { get; set; } = 1.0f;
		public float DurationMultiplier { get; set; } = 1.0f;

		private Weapon weapon;
		public Weapon Weapon
		{
			get => weapon;
			set
			{
				weapon = value;
				ApplyLegacyWeapon(value);
				UpdateScale();
			}
		}

		private int damage = 5;
		private float range = 100f;
		private float cooldown = 1.5f;
		private float knockbackRange = 100f;
		private float knockbackSpeed = 2.0f;

		public Node2D PlayerRef;
		[Export]
		public float TotalLifetime = 5.0f; // total time before freeing the node; 0 = infinite
		private float lifeElapsed = 0f;
		private float fireTimer = 0f;
		private CollisionShape2D collisionShape;

		// THE BLAST IS PIXEL ART (2026-10-29, "does not look up to our typical visual fidelity"). It was
		// spell_fx.py's 32 px ring saved at x2 and shown at range / 32 - its art pixels 6 to 14 world px
		// wide - over a ring of 64 white GPU particles one world px wide, with every evolution's tint
		// multiplied over both. Now it is one quad through scenes/shaders/arcane_blast.gdshader, drawn
		// from `range`, the distance TriggerExplosion tests: a front of the force that lands on that
		// radius and breaks up there, every art pixel on the world's 2 px grid. The evolutions are painted
		// there rather than tinted, from what each one does (StartBlast).
		private const string BlastShaderPath = "res://scenes/shaders/arcane_blast.gdshader";
		private const float BlastSeconds = 0.4f;
		private static Shader blastShader;
		private static readonly StringName ParamRadius = "radius";
		private static readonly StringName ParamProgress = "progress";
		private static readonly StringName ParamSeed = "seed";
		private static readonly StringName ParamDual = "dual";
		private static readonly StringName ParamStatic = "static_nova";
		private static readonly StringName ParamSupernova = "supernova";
		private static readonly StringName ParamEventHorizon = "event_horizon";
		private static readonly StringName ParamGilded = "gilded";
		private ShaderMaterial blastMaterial;
		private float blastAge = -1f;
		private float blastRadius;
		private int blastCount;

		public override void _Ready()
		{
			// The blast is in the air, over the bodies it catches. A child of the player at z 0, it was
			// sorted at his feet, under every body south of him (DrawLayers). Its rift is on the ground layer.
			ZAsRelative = false;
			ZIndex = DrawLayers.Air;
			RefreshComputedStats();
			collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
			blastShader ??= GD.Load<Shader>(BlastShaderPath);
			blastMaterial = new ShaderMaterial { Shader = blastShader };
			Material = blastMaterial;

			// Apply initial scaling if weapon is already set
			UpdateScale();

			// Trigger immediately on spawn, then start cooldown timer
			TriggerExplosion();
			fireTimer = 0f;
		}

		private void UpdateScale()
		{
			// The collision circle follows the range as it always did; the blast is drawn from the range
			// itself (_Draw), so there is no sprite to scale.
			float baseRange = 32.0f;
			float scaleFactor = range / baseRange;

			if (collisionShape != null && collisionShape.Shape is CircleShape2D circle)
			{
				// The original circle radius is ~23, scale it proportionally
				circle.Radius = 23.0f * scaleFactor;
			}
		}

		public override void _Process(double delta)
		{
			// follow player if assigned
			if (PlayerRef != null)
			{
				GlobalPosition = PlayerRef.GlobalPosition;
			}

			// update overall lifetime and free when exceeded
			if (TotalLifetime > 0f)
			{
				lifeElapsed += (float)delta;
				if (lifeElapsed >= TotalLifetime)
				{
					QueueFree();
					return;
				}
			}
			// Timed firing only when the charge path is switched off. A spell that both ticks on
			// a clock AND detonates on charge would fire twice as often as either rule describes.
			if (!ChargeDriven)
			{
				fireTimer += (float)delta;
				if (fireTimer >= cooldown)
				{
					TriggerExplosion();
					fireTimer = 0f;
				}
			}

			if (blastAge < 0f)
				return;
			blastAge += (float)delta;
			if (blastAge >= BlastSeconds)
				blastAge = -1f;
			else
				blastMaterial?.SetShaderParameter(ParamProgress, blastAge / BlastSeconds);
			QueueRedraw();
		}

		// One quad over the blast, sized in world px - what TriggerExplosion measured in - whatever this
		// node's scale; the shader decides every pixel and discards the rest.
		public override void _Draw()
		{
			if (blastAge < 0f)
				return;
			float half = (blastRadius + 4f) / MathF.Max(0.01f, GlobalScale.X);
			DrawRect(new Rect2(-half, -half, half * 2f, half * 2f), Colors.White);
		}

		// The blast's look, from what this detonation does: a second front for Dual Shockwave, the storm's
		// crackle for Static Nova, the fire ramp for Supernova, a collapse for anything that pulls inward
		// (Event Horizon), gold for a Legendary. Set once a blast, with the radius it hit.
		private void StartBlast(bool isVortex)
		{
			if (blastMaterial == null)
				return;
			blastRadius = range;
			blastAge = 0f;
			blastCount++;
			blastMaterial.SetShaderParameter(ParamRadius, blastRadius);
			blastMaterial.SetShaderParameter(ParamProgress, 0f);
			blastMaterial.SetShaderParameter(ParamSeed, (blastCount * 0.6180339f) % 1f * 97f);
			blastMaterial.SetShaderParameter(ParamDual, SpellData?.HasEvolution(DualShockwaveId) ?? false);
			blastMaterial.SetShaderParameter(ParamStatic, SpellData?.HasEvolution(StaticNovaId) ?? false);
			blastMaterial.SetShaderParameter(ParamSupernova, SpellData?.HasEvolution(SupernovaId) ?? false);
			blastMaterial.SetShaderParameter(ParamEventHorizon, isVortex);
			blastMaterial.SetShaderParameter(ParamGilded, SpellData?.IsLegendary ?? false);
			QueueRedraw();
		}

		private const string DualShockwaveId = "arcane_explosion_dual_shockwave";
		private const string StaticNovaId = "arcane_explosion_static_nova";
		private const string SupernovaId = "arcane_explosion_supernova";

		/// <summary>Detonate now. Called by Player when the charge comes due.</summary>
		public void TriggerNow()
		{
			TriggerExplosion();
			fireTimer = 0f;
		}

		private void TriggerExplosion()
		{
			// Apply to all enemies within range.
			//
			// The group, not GetTree().CurrentScene.GetChildren(). The old form only saw enemies
			// that were DIRECT children of the scene root, so anything parented under a container
			// - a spawner, a formation node, a test harness - was silently immune. CLAUDE.md is
			// explicit that AoE iterates the group for exactly this reason.
			// Event Horizon's freeze (Effect Freeze) is a singularity, so it draws the blast inward as
			// well as holding what it catches: the card promised both, and until 2026-10-26 did neither.
			float freezeSeconds = SpellData?.GetFreezeSecondsAtLevel(CurrentLevel) ?? 0f;
			bool isVortex = freezeSeconds > 0f || (SpellData != null && SpellData.HasEffectFlag(SpellEffect.VortexPull));
			var enemies = EnemyRegistry.Snapshot.OfType<Node2D>();
			foreach (var e in enemies)
			{
				float dist = GlobalPosition.DistanceTo(e.GlobalPosition);
				if (dist <= range)
				{
					float knockback = knockbackRange * (1f - (dist / (2f * range)));
					if (knockback < knockbackRange * 0.5f)
						knockback = knockbackRange * 0.5f;

					Vector2 dir = isVortex
						? (GlobalPosition - e.GlobalPosition).Normalized()
						: (e.GlobalPosition - GlobalPosition).Normalized();

					if (e.HasMethod("ApplyKnockback"))
						e.Call("ApplyKnockback", dir * knockback * knockbackSpeed);
					if (e.HasMethod("TakeDamage"))
					{
						(PlayerRef as Player)?.DealDamageToEnemy(e, damage, source: SpellData);
					}
					// Frozen after the pull is set, so the pull still lands them nearer the middle:
					// a knockback carries on through a freeze (Enemy, the shock/knockback branch).
					if (freezeSeconds > 0f && e is Enemy held)
						held.ApplySlow(0f, freezeSeconds);
				}
			}

			OpenLingeringRift();
			StartBlast(isVortex);
		}

		// Lingering Rift (Effect ZoneDuration = seconds): the ground the blast went off on keeps hurting
		// for a while after. It promised "a pulsating hazard zone" and ArcaneExplosion left none until
		// 2026-10-26. The rift is a LingeringZone, Mirefoot's pool, in arcane colour: it stays where the
		// blast was while the player walks on, which is what makes it a zone.
		private const float RiftTickShare = 0.25f;
		private const float RiftRadiusShare = 0.8f;

		private void OpenLingeringRift()
		{
			float seconds = SpellData?.GetEffectValueAtLevel(SpellEffect.ZoneDuration, CurrentLevel) ?? 0f;
			if (seconds <= 0f || PlayerRef is not Player player)
				return;
			Node parent = player.GetParent();
			if (parent == null)
				return;

			var rift = new LingeringZone
			{
				Name = "LingeringRift",
				Radius = range * RiftRadiusShare,
				Duration = seconds * DurationMultiplier,
				TickSeconds = 0.5f,
				AppliesSlow = false,
				DamagePerTick = Math.Max(1, Mathf.RoundToInt(damage * RiftTickShare)),
				SpellData = SpellData,
				PlayerRef = player,
			};
			// A tear in the ground with the force wound out of it to the zone's radius, not a translucent
			// disc (scenes/shaders/arcane_rift.gdshader).
			BlastPoolSurface.Attach(rift, BlastPoolSurface.Kind.ArcaneRift);
			parent.AddChild(rift);
			rift.GlobalPosition = GlobalPosition;
		}

		public void SetSpellLevel(int level)
		{
			CurrentLevel = Math.Max(1, level);
			RefreshComputedStats();
			UpdateScale();
		}

		private void RefreshComputedStats()
		{
			if (weapon != null)
			{
				ApplyLegacyWeapon(weapon);
				return;
			}

			damage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 5) * DamageMultiplier));
			// With the level table's Area line (Effect AreaSize, a fraction), read since 2026-10-26.
			range = (SpellData?.GetRangeAtLevel(CurrentLevel) ?? 100f) * AreaMultiplier * (SpellData?.GetAreaMultiplierAtLevel(CurrentLevel) ?? 1f)
				* (1f + (SpellData?.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel) ?? 0f));
			cooldown = MathF.Max(0.05f, (SpellData?.GetCooldownAtLevel(CurrentLevel) ?? 1.5f) * CooldownMultiplier);
			// Effect Knockback from the level table and KnockbackBonus from an evolution (Dual
			// Shockwave) are the same units; the second was read by nothing until 2026-10-26.
			knockbackRange = MathF.Max(0f, BaseKnockbackRange + (SpellData?.GetEffectValueAtLevel(SpellEffect.Knockback, CurrentLevel) ?? 0f)
				+ (SpellData?.GetKnockbackBonusAtLevel(CurrentLevel) ?? 0f));
			knockbackSpeed = MathF.Max(0f, BaseKnockbackSpeed);
		}

		private void ApplyLegacyWeapon(Weapon value)
		{
			if (value == null) return;

			damage = value.Damage;
			range = value.Range;
			cooldown = MathF.Max(0.05f, value.Cooldown);
			knockbackRange = value.KnockbackRange;
			knockbackSpeed = value.KnockbackSpeed;
		}
	}
}
