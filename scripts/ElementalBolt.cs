using Godot;
using System;
using System.Linq;

namespace WizardSurvivors.scripts;

// Shared script for straightforward "elemental bolt" offensive spells (issue #13 roster expansion):
// Frost Shard, Shadow Bolt, Thorn Vine, Gale Blade, Molten Shard, Chain Lightning, Void Lance,
// Glacial Spike. Behaves like MagicMissile (homing bolt with optional pierce) plus optional
// guaranteed on-hit status effects and a single chain-jump to a second nearby enemy, configured
// per-scene via exported flags instead of one bespoke script per spell.
public partial class ElementalBolt : Area2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float BaseSpeed { get; set; } = 350f;
	[Export] public float BaseDuration { get; set; } = 5.0f;
	[Export] public int BasePierce { get; set; } = 0;
	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float DurationMultiplier { get; set; } = 1.0f;
	public Node2D PlayerRef;

	// Guaranteed on-hit status effects, independent of the player's element-tier bonuses (#16).
	[Export] public bool GuaranteedSlow { get; set; } = false;
	[Export] public float SlowMultiplier { get; set; } = 0.5f;
	[Export] public float SlowDuration { get; set; } = 2.0f;
	[Export] public bool GuaranteedPoison { get; set; } = false;
	[Export] public int PoisonDamagePerTick { get; set; } = 2;
	[Export] public float PoisonDuration { get; set; } = 3.0f;
	[Export] public bool ChainToSecondTarget { get; set; } = false;
	[Export] public float ChainRadius { get; set; } = 150f;
	[Export] public float ChainDamageMultiplier { get; set; } = 0.6f;
	[Export] public bool InstantBoltVisual { get; set; } = false;
	[Export] public float InstantBoltLifetime { get; set; } = 0.16f;
	// Arc bolts (Shadow Bolt, Gale Blade) hit one enemy, vanish, then a fresh bolt is spawned at the
	// impact point and fired straight at the next enemy - a new streak per hop instead of one
	// projectile curving through the air. MaxChainBounces is how many extra enemies a cast reaches.
	[Export] public int MaxChainBounces { get; set; } = 1;
	[Export] public float ChainRespawnDelay { get; set; } = 0.12f;
	public System.Collections.Generic.HashSet<Node2D> ChainVisited;
	public int ChainBouncesRemaining { get; set; } = -1;
	public int OverrideDamage { get; set; } = -1;

	private const float ArcVisualLength = 28f;
	private Line2D boltLine;
	private float[] arcOffsets = Array.Empty<float>();
	private int arcBendCount = 0;

	private int damage = 1;
	private float range = 500f;
	private float speed = 350f;
	private float duration = 5.0f;
	private int pierce = 0;
	private float areaRadius = 8f;
	private float scaledSlowMultiplier = 0.5f;
	private float scaledSlowDuration = 2.0f;
	private int scaledPoisonTick = 2;
	private float scaledPoisonDuration = 3.0f;

	private Vector2 direction = Vector2.Zero;
	private Node target = null;
	private float turnSpeed = 6.0f;
	private float lifetime = 0f;
	private int pierceCount = 0;
	private Vector2 spawnPosition = Vector2.Zero;
	private bool instantBoltActive = false;
	private Vector2 instantBoltFromLocal = Vector2.Zero;
	private Vector2 instantBoltToLocal = Vector2.Right;
	private float instantBoltFadeAlpha = 1f;

	public override void _Ready()
	{
		RefreshComputedStats();
		boltLine = GetNodeOrNull<Line2D>("BoltLine");
		if (boltLine != null)
		{
			boltLine.Visible = true;
			boltLine.TextureMode = Line2D.LineTextureMode.Tile;
		}
		var cs = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (cs?.Shape is CircleShape2D shape)
			shape.Radius = areaRadius;
		Connect("area_entered", new Callable(this, nameof(OnAreaEntered)));
		Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
		RefreshComputedStats();
	}

	public void Shoot(Vector2 from, Vector2 to, Node enemyTarget = null)
	{
		GlobalPosition = from;
		instantBoltActive = false;
		direction = (to - from).Normalized();
		Rotation = direction.Angle();
		spawnPosition = from;
		lifetime = 0f;
		pierceCount = 0;
		if (enemyTarget is Node2D enemyNode)
		{
			float distToEnemy = (enemyNode.GlobalPosition - from).Length();
			target = distToEnemy <= range ? enemyTarget : null;
		}
		else
		{
			target = null;
		}

		if (InstantBoltVisual && enemyTarget is Node2D instantTarget)
		{
			instantBoltActive = true;
			instantBoltFadeAlpha = 1f;
			GlobalPosition = from;
			instantBoltFromLocal = Vector2.Zero;
			instantBoltToLocal = instantTarget.GlobalPosition - from;
			ConfigureArcVisual(instantBoltToLocal.Length());
			ResolveImpact(instantTarget, keepAliveForVisual: true);
			return;
		}

		ConfigureArcVisual(ArcVisualLength);
	}

	public override void _Process(double delta)
	{
		if (instantBoltActive)
		{
			lifetime += (float)delta;
			float fadeDuration = MathF.Max(0.05f, InstantBoltLifetime);
			instantBoltFadeAlpha = MathF.Max(0f, 1f - (lifetime / fadeDuration));
			UpdateArcVisual();
			if (lifetime > fadeDuration)
			{
				if (boltLine != null)
					boltLine.Visible = false;
				QueueFree();
			}
			return;
		}

		// Arc bolts fly dead straight at their locked target; only non-arc bolts home in midair.
		if (!ChainToSecondTarget && target != null && IsInstanceValid(target))
		{
			if (target is Node2D targetNode)
			{
				var toTarget = targetNode.GlobalPosition - GlobalPosition;
				if (toTarget.Length() > 0)
				{
					var tdir = toTarget.Normalized();
					var alpha = MathF.Min(1f, turnSpeed * (float)delta);
					direction = (direction * (1f - alpha) + tdir * alpha).Normalized();
					Rotation = direction.Angle();
				}
			}
		}

		if ((GlobalPosition - spawnPosition).Length() > range)
			target = null;

		Position += direction * speed * (float)delta;
		UpdateArcVisual();
		lifetime += (float)delta;
		if (duration > 0 && lifetime > duration) QueueFree();
	}

	private void OnAreaEntered(Area2D area) => HandleHit(area);
	private void OnBodyEntered(Node body) => HandleHit(body);

	private void HandleHit(Node enemy)
	{
		ResolveImpact(enemy, keepAliveForVisual: false);
	}

	private void ResolveImpact(Node enemy, bool keepAliveForVisual)
	{
		if (!enemy.IsInGroup("enemies") || !enemy.HasMethod("TakeDamage"))
			return;

		var player = PlayerRef as Player;
		float critBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.CritChance, CurrentLevel) ?? 0f;
		player?.DealDamageToEnemy(enemy, damage, critBonus);
		ApplyGuaranteedEffects(enemy);

		if (ChainToSecondTarget && player != null && enemy is Node2D hitNode2D)
		{
			ScheduleChainJump(hitNode2D, player);
			if (!keepAliveForVisual)
				QueueFree();
			return;
		}

		pierceCount++;
		if (pierceCount > pierce && !keepAliveForVisual)
			QueueFree();
		if (keepAliveForVisual)
			duration = MathF.Max(duration, InstantBoltLifetime);
	}

	// Spawn a brand new bolt at the impact point aimed straight at the next unvisited enemy after a
	// short beat. We capture plain values (not this) because the current node is freed immediately.
	private void ScheduleChainJump(Node2D hitNode, Player player)
	{
		ChainVisited ??= new System.Collections.Generic.HashSet<Node2D>();
		ChainVisited.Add(hitNode);

		if (ChainBouncesRemaining < 0)
			ChainBouncesRemaining = Math.Max(1, MaxChainBounces);
		if (ChainBouncesRemaining <= 0)
			return;

		string scenePath = SceneFilePath;
		if (string.IsNullOrEmpty(scenePath))
			return;

		Node parent = GetParent();
		if (parent == null || !IsInstanceValid(parent))
			return;

		Vector2 from = hitNode.GlobalPosition;
		float chainRadius = ChainRadius;
		var visited = ChainVisited;
		int remaining = ChainBouncesRemaining - 1;
		int nextDamage = Math.Max(1, Mathf.RoundToInt(damage * ChainDamageMultiplier));
		SpellData spell = SpellData;
		int level = CurrentLevel;
		float dmgMult = DamageMultiplier;
		float areaMult = AreaMultiplier;
		float durMult = DurationMultiplier;
		Node2D playerNode = player;

		var timer = GetTree().CreateTimer(ChainRespawnDelay);
		timer.Timeout += () =>
		{
			if (!IsInstanceValid(parent))
				return;

			Node2D next = parent.GetTree().GetNodesInGroup("enemies")
				.OfType<Node2D>()
				.Where(e => e != null && IsInstanceValid(e) && e.HasMethod("TakeDamage") && !visited.Contains(e) && from.DistanceTo(e.GlobalPosition) <= chainRadius)
				.OrderBy(e => from.DistanceTo(e.GlobalPosition))
				.FirstOrDefault();
			if (next == null)
				return;

			var packed = ResourceLoader.Load<PackedScene>(scenePath);
			if (packed == null)
				return;

			var boltNode = packed.Instantiate<Area2D>();
			if (boltNode is ElementalBolt bolt)
			{
				bolt.SpellData = spell;
				bolt.PlayerRef = playerNode;
				bolt.DamageMultiplier = dmgMult;
				bolt.AreaMultiplier = areaMult;
				bolt.DurationMultiplier = durMult;
				bolt.ChainVisited = visited;
				bolt.ChainBouncesRemaining = remaining;
				bolt.OverrideDamage = nextDamage;
				bolt.SetSpellLevel(level);
			}
			parent.AddChild(boltNode);
			(boltNode as ElementalBolt)?.Shoot(from, next.GlobalPosition, next);
		};
	}

	private void ApplyGuaranteedEffects(Node enemy)
	{
		if (GuaranteedSlow && enemy.HasMethod("ApplySlow"))
			enemy.Call("ApplySlow", scaledSlowMultiplier, scaledSlowDuration);
		if (GuaranteedPoison && enemy.HasMethod("ApplyPoison"))
			enemy.Call("ApplyPoison", scaledPoisonTick, scaledPoisonDuration);
	}

	private void ConfigureArcVisual(float visualLength)
	{
		if (boltLine == null)
			return;

		arcBendCount = (int)(GD.Randi() % 3) + 3;
		arcOffsets = new float[arcBendCount];
		float maxWobble = MathF.Min(18f, visualLength * 0.35f);
		for (int i = 0; i < arcBendCount; i++)
			arcOffsets[i] = (float)GD.RandRange(-maxWobble, maxWobble);
	}

	private void UpdateArcVisual()
	{
		if (boltLine == null)
			return;

		Vector2 from = Vector2.Left * (ArcVisualLength * 0.5f);
		Vector2 to = Vector2.Right * (ArcVisualLength * 0.5f);
		if (instantBoltActive)
		{
			from = instantBoltFromLocal;
			to = instantBoltToLocal;
		}
		Vector2 normal = Vector2.Up;
		var points = new System.Collections.Generic.List<Vector2> { from };
		for (int i = 1; i <= arcBendCount; i++)
		{
			float t = i / (float)(arcBendCount + 1);
			Vector2 basePoint = from.Lerp(to, t);
			points.Add(basePoint + normal * arcOffsets[i - 1]);
		}
		points.Add(to);

		boltLine.Width = MathF.Max(2f, 2.5f + areaRadius * 0.18f);
		float alpha = instantBoltActive ? instantBoltFadeAlpha : 0.95f;
		boltLine.DefaultColor = new Color(0.42f, 0.08f, 0.58f, alpha);
		boltLine.Points = points.ToArray();
		boltLine.Visible = alpha > 0.01f;
	}

	private void RefreshComputedStats()
	{
		damage = OverrideDamage >= 0
			? OverrideDamage
			: Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 1) * DamageMultiplier));
		range = SpellData?.GetRangeAtLevel(CurrentLevel) ?? 500f;
		speed = MathF.Max(1f, BaseSpeed + (SpellData?.GetEffectValueAtLevel(SpellEffect.ProjectileSpeed, CurrentLevel) ?? 0f));
		duration = MathF.Max(0f, BaseDuration * DurationMultiplier);
		pierce = Math.Max(0, BasePierce + (int)MathF.Round(SpellData?.GetEffectValueAtLevel(SpellEffect.Pierce, CurrentLevel) ?? 0f));
		areaRadius = MathF.Max(2f, (8f + (SpellData?.GetEffectValueAtLevel(SpellEffect.AreaSize, CurrentLevel) ?? 0f)) * AreaMultiplier);

		float slowPower = SpellData?.GetEffectValueAtLevel(SpellEffect.SlowPower, CurrentLevel) ?? 0f;
		float slowDurationBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.SlowDuration, CurrentLevel) ?? 0f;
		scaledSlowMultiplier = Mathf.Clamp(SlowMultiplier - slowPower, 0f, 0.98f);
		scaledSlowDuration = MathF.Max(0.1f, SlowDuration + slowDurationBonus);

		int poisonTickBonus = Mathf.RoundToInt(SpellData?.GetEffectValueAtLevel(SpellEffect.DotDamage, CurrentLevel) ?? 0f);
		float poisonDurationBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.ZoneDuration, CurrentLevel) ?? 0f;
		scaledPoisonTick = Math.Max(1, PoisonDamagePerTick + poisonTickBonus);
		scaledPoisonDuration = MathF.Max(0.1f, PoisonDuration + poisonDurationBonus);
	}
}
