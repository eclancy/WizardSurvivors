using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Dedicated Chain Lightning behavior: a lightning strike that can fork into multiple delayed arcs.
public partial class ChainLightning : Area2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float ArcDuration { get; set; } = 0.09f;
	[Export] public float BounceDelay { get; set; } = 1.0f;
	[Export] public float ChainRadius { get; set; } = 220f;
	[Export] public float ChainDamageMultiplier { get; set; } = 0.78f;
	[Export] public float BaseLineWidth { get; set; } = 5.0f;
	[Export] public bool UseChainChance { get; set; } = false;
	[Export] public float BaseChainChance { get; set; } = 1.0f;
	[Export] public bool GuaranteedPoison { get; set; } = false;
	[Export] public int PoisonDamagePerTick { get; set; } = 2;
	[Export] public float PoisonDuration { get; set; } = 3.0f;
	[Export] public bool SpawnResidualAuraTrail { get; set; } = false;
	[Export] public float ResidualAuraDuration { get; set; } = 0.55f;
	[Export] public float ResidualAuraWidthScale { get; set; } = 1.65f;
	[Export] public Color ResidualAuraColor { get; set; } = new Color(0.22f, 0.05f, 0.30f, 0.34f);

	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float DurationMultiplier { get; set; } = 1.0f;
	public int ProjectileCountBonus { get; set; } = 0;
	public Player PlayerRef { get; set; }

	private Line2D boltLine;
	private Node2D originNode;
	private Node2D activeSource;
	private Node2D activeTarget;
	private HashSet<Node2D> visitedTargets = new HashSet<Node2D>();
	private int remainingArcDepth = 0;
	private int chainHopIndex = 0;
	private float stateTimer = 0f;
	private bool showingArc = false;
	private bool waitingToBranch = false;
	private readonly RandomNumberGenerator chainRng = new RandomNumberGenerator();

	public override void _Ready()
	{
		Monitoring = false;
		Monitorable = false;
		boltLine = GetNodeOrNull<Line2D>("BoltLine");
		if (boltLine == null)
		{
			boltLine = new Line2D { Name = "BoltLine", Width = BaseLineWidth, DefaultColor = new Color(1.0f, 0.95f, 0.45f, 0.95f) };
			AddChild(boltLine);
		}
		boltLine.Visible = false;
		boltLine.TextureMode = Line2D.LineTextureMode.Tile;
		chainRng.Randomize();
		if (SpellData?.IsLegendary == true)
			Modulate = new Color(1.0f, 0.9f, 0.45f, 1.0f);
	}

	public void SetSpellLevel(int level)
	{
		CurrentLevel = Math.Max(1, level);
	}

	public void CastFromPlayer(Node2D playerNode, Node2D firstTarget)
	{
		if (playerNode == null || firstTarget == null || !IsInstanceValid(firstTarget))
		{
			QueueFree();
			return;
		}

		originNode = playerNode;
		StartArc(playerNode, firstTarget, GetInitialArcDepth(), 0, new HashSet<Node2D> { playerNode });
	}

	public override void _Process(double delta)
	{
		if (HasInvalidArcEndpoints())
		{
			QueueFree();
			return;
		}

		stateTimer -= (float)delta;
		if (showingArc)
		{
			if (stateTimer <= 0f)
			{
				boltLine.Visible = false;
				showingArc = false;
				if (remainingArcDepth <= 0)
				{
					QueueFree();
					return;
				}
				waitingToBranch = true;
				stateTimer = BounceDelay * DurationMultiplier;
			}
			return;
		}

		if (!waitingToBranch || stateTimer > 0f)
			return;

		waitingToBranch = false;
		if (!CanContinueChain())
		{
			QueueFree();
			return;
		}
		SpawnBranches();
		QueueFree();
	}

	private bool CanContinueChain()
	{
		if (!UseChainChance)
			return true;

		float levelBonus = SpellData?.GetChainChanceAtLevel(CurrentLevel) ?? 0f;
		float chance = Mathf.Clamp(BaseChainChance + levelBonus, 0f, 1f);
		return chainRng.Randf() <= chance;
	}

	private bool HasInvalidArcEndpoints()
	{
		return activeTarget == null || !IsInstanceValid(activeTarget) || activeSource == null || !IsInstanceValid(activeSource);
	}

	private void StartArc(Node2D source, Node2D target, int remainingDepth, int hopIndex, HashSet<Node2D> visited)
	{
		activeSource = source;
		activeTarget = target;
		remainingArcDepth = Math.Max(0, remainingDepth);
		chainHopIndex = Math.Max(0, hopIndex);
		visitedTargets = visited ?? new HashSet<Node2D>();
		visitedTargets.Add(source);
		visitedTargets.Add(target);
		GlobalPosition = Vector2.Zero;

		RenderBolt(activeSource.GlobalPosition, activeTarget.GlobalPosition);
		ApplyDamage(activeTarget);
		ApplyImpactShock(activeTarget);
		showingArc = true;
		waitingToBranch = false;
		stateTimer = ArcDuration;
	}

	private int GetInitialArcDepth()
	{
		int baseDepth = Math.Max(0, (SpellData?.GetProjectileCountAtLevel(CurrentLevel) ?? 1) - 1 + ProjectileCountBonus);
		int chainDepth = SpellData?.GetChainArcCountAtLevel(CurrentLevel) ?? 0;
		return Math.Max(0, baseDepth + chainDepth);
	}

	private int GetBranchCount()
	{
		int branchBonus = SpellData?.GetChainBranchCountAtLevel(CurrentLevel) ?? 0;
		return Math.Max(1, 1 + branchBonus);
	}

	private void SpawnBranches()
	{
		if (remainingArcDepth <= 0 || activeTarget == null || !IsInstanceValid(activeTarget))
			return;

		var candidates = GetTree().GetNodesInGroup("enemies")
			.OfType<Node2D>()
			.Where(enemy => enemy != null
				&& IsInstanceValid(enemy)
				&& enemy.HasMethod("TakeDamage")
				&& !visitedTargets.Contains(enemy)
				&& enemy != activeTarget
				&& activeTarget.GlobalPosition.DistanceTo(enemy.GlobalPosition) <= ChainRadius)
			.OrderBy(enemy => activeTarget.GlobalPosition.DistanceTo(enemy.GlobalPosition))
			.Take(GetBranchCount())
			.ToList();

		if (candidates.Count == 0)
			return;

		string scenePath = SceneFilePath;
		if (string.IsNullOrEmpty(scenePath))
			return;

		Node parent = GetParent();
		if (parent == null || !IsInstanceValid(parent))
			return;

		var packed = ResourceLoader.Load<PackedScene>(scenePath);
		if (packed == null)
			return;

		foreach (Node2D next in candidates)
		{
			var branchNode = packed.Instantiate<Area2D>();
			if (branchNode is ChainLightning branch)
			{
				branch.SpellData = SpellData;
				branch.CurrentLevel = CurrentLevel;
				branch.DamageMultiplier = DamageMultiplier;
				branch.AreaMultiplier = AreaMultiplier;
				branch.DurationMultiplier = DurationMultiplier;
				branch.ProjectileCountBonus = ProjectileCountBonus;
				branch.PlayerRef = PlayerRef;
				branch.ArcDuration = ArcDuration;
				branch.BounceDelay = BounceDelay;
				branch.ChainRadius = ChainRadius;
				branch.ChainDamageMultiplier = ChainDamageMultiplier;
				branch.BaseLineWidth = BaseLineWidth;
				branch.SetSpellLevel(CurrentLevel);
			}

			parent.AddChild(branchNode);
			if (branchNode is ChainLightning branchLightning)
				branchLightning.StartArc(activeTarget, next, remainingArcDepth - 1, chainHopIndex + 1, new HashSet<Node2D>(visitedTargets));
		}
	}

	private void ApplyDamage(Node target)
	{
		if (PlayerRef == null)
			return;

		int baseDamage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 1) * DamageMultiplier));
		int hopDamage = Math.Max(1, Mathf.RoundToInt(baseDamage * MathF.Pow(ChainDamageMultiplier, chainHopIndex)));
		float critBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.CritChance, CurrentLevel) ?? 0f;
		PlayerRef.DealDamageToEnemy(target, hopDamage, critBonus, allowElementalChain: false);
		ApplyGuaranteedEffects(target);
	}

	private void ApplyGuaranteedEffects(Node target)
	{
		if (GuaranteedPoison && target.HasMethod("ApplyPoison"))
		{
			int poisonTick = Math.Max(1, PoisonDamagePerTick + (SpellData?.GetPoisonTickBonusAtLevel(CurrentLevel) ?? 0));
			target.Call("ApplyPoison", poisonTick, PoisonDuration);
		}
	}

	private void ApplyImpactShock(Node target)
	{
		if (target.HasMethod("ApplyShock"))
			target.Call("ApplyShock", MathF.Max(0.16f, ArcDuration * 2.0f), 5.0f, 54.0f);
		else if (target.HasMethod("ApplySlow"))
			target.Call("ApplySlow", 0f, MathF.Max(0.16f, ArcDuration * 2.0f));
	}

	private void RenderBolt(Vector2 from, Vector2 to)
	{
		if (boltLine == null)
			return;

		boltLine.Width = Math.Max(2f, BaseLineWidth * AreaMultiplier);
		Vector2 direction = to - from;
		float length = direction.Length();
		Vector2 normal = length > 0.0001f
			? direction.Normalized().Orthogonal()
			: Vector2.Up;

		// Jag the bolt with a random number of bends (3-5) and stronger perpendicular offsets each
		// cast so no two strikes trace the same path.
		int bends = (int)(GD.Randi() % 3) + 3;
		float maxWobble = Math.Min(40f, length * 0.28f);
		var points = new List<Vector2> { from };
		for (int i = 1; i <= bends; i++)
		{
			float t = i / (float)(bends + 1);
			Vector2 basePoint = from.Lerp(to, t);
			float offset = (float)GD.RandRange(-maxWobble, maxWobble);
			points.Add(basePoint + normal * offset);
		}
		points.Add(to);

		boltLine.Points = points.ToArray();
		boltLine.Visible = true;
		if (SpawnResidualAuraTrail)
			SpawnAuraTrail(from, to, boltLine.Width);
	}

	private void SpawnAuraTrail(Vector2 from, Vector2 to, float lineWidth)
	{
		if (ResidualAuraDuration <= 0.01f)
			return;

		Node parent = GetParent();
		if (parent == null || !IsInstanceValid(parent))
			return;

		if (boltLine == null || boltLine.Points == null || boltLine.Points.Length < 2)
			return;

		Vector2 direction = to - from;
		float length = direction.Length();
		if (length < 4f)
			return;

		var auraLine = new Line2D
		{
			Name = "ShadowAuraTrail",
			TopLevel = true,
			TextureMode = Line2D.LineTextureMode.Tile,
			Antialiased = true,
			Points = boltLine.Points,
			Width = Math.Max(2f, lineWidth * ResidualAuraWidthScale),
			DefaultColor = ResidualAuraColor,
			ZIndex = -1
		};
		parent.AddChild(auraLine);

		Tween trailTween = auraLine.CreateTween();
		trailTween.SetParallel(true);
		trailTween.TweenProperty(auraLine, "width", Math.Max(1f, lineWidth * 0.42f), ResidualAuraDuration);
		Color endColor = ResidualAuraColor;
		endColor.A = 0f;
		trailTween.TweenProperty(auraLine, "default_color", endColor, ResidualAuraDuration);
		trailTween.Finished += () =>
		{
			if (IsInstanceValid(auraLine))
				auraLine.QueueFree();
		};
	}
}
