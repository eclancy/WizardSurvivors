using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

// Dedicated Chain Lightning behavior: fast strike, then delayed jumps from the last hit target.
public partial class ChainLightning : Area2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public float ArcDuration { get; set; } = 0.09f;
	[Export] public float BounceDelay { get; set; } = 1.0f;
	[Export] public float ChainRadius { get; set; } = 220f;
	[Export] public float ChainDamageMultiplier { get; set; } = 0.78f;
	[Export] public float BaseLineWidth { get; set; } = 5.0f;

	public float DamageMultiplier { get; set; } = 1.0f;
	public float AreaMultiplier { get; set; } = 1.0f;
	public float DurationMultiplier { get; set; } = 1.0f;
	public int ProjectileCountBonus { get; set; } = 0;
	public Player PlayerRef { get; set; }

	private readonly List<Node2D> chainTargets = new List<Node2D>();
	private Line2D boltLine;
	private Node2D originNode;
	private Node2D activeSource;
	private int bounceIndex = -1;
	private float stateTimer = 0f;
	private bool showingArc = false;

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
		activeSource = playerNode;
		GlobalPosition = Vector2.Zero;
		BuildChainTargets(firstTarget);
		if (chainTargets.Count == 0)
		{
			QueueFree();
			return;
		}

		bounceIndex = 0;
		TriggerCurrentBounce();
	}

	public override void _Process(double delta)
	{
		if (bounceIndex < 0)
			return;

		stateTimer -= (float)delta;
		if (showingArc)
		{
			if (stateTimer <= 0f)
			{
				boltLine.Visible = false;
				showingArc = false;
				stateTimer = BounceDelay * DurationMultiplier;
			}
			return;
		}

		if (stateTimer > 0f)
			return;

		bounceIndex++;
		if (bounceIndex >= chainTargets.Count)
		{
			QueueFree();
			return;
		}

		TriggerCurrentBounce();
	}

	private void BuildChainTargets(Node2D firstTarget)
	{
		chainTargets.Clear();
		chainTargets.Add(firstTarget);

		int totalTargets = GetTotalChainTargets();
		var used = new HashSet<Node2D> { firstTarget };
		Node2D current = firstTarget;

		while (chainTargets.Count < totalTargets)
		{
			Node2D next = GetTree().GetNodesInGroup("enemies")
				.OfType<Node2D>()
				.Where(enemy => enemy != null
					&& IsInstanceValid(enemy)
					&& enemy.HasMethod("TakeDamage")
					&& !used.Contains(enemy)
					&& current.GlobalPosition.DistanceTo(enemy.GlobalPosition) <= ChainRadius)
				.OrderBy(enemy => current.GlobalPosition.DistanceTo(enemy.GlobalPosition))
				.FirstOrDefault();

			if (next == null)
				break;

			chainTargets.Add(next);
			used.Add(next);
			current = next;
		}
	}

	private int GetTotalChainTargets()
	{
		int projectileBounces = Math.Max(0, (SpellData?.GetProjectileCountAtLevel(CurrentLevel) ?? 1) - 1 + ProjectileCountBonus);
		int levelBounces = Math.Max(0, (CurrentLevel - 1) / 2);
		int effectBounces = Math.Max(0, Mathf.RoundToInt(SpellData?.GetEffectValueAtLevel(SpellEffect.Chain, CurrentLevel) ?? 0f));
		int totalBounces = projectileBounces + levelBounces + effectBounces;
		return Math.Max(1, 1 + totalBounces);
	}

	private void TriggerCurrentBounce()
	{
		Node2D target = chainTargets[bounceIndex];
		if (target == null || !IsInstanceValid(target) || !target.HasMethod("TakeDamage"))
		{
			stateTimer = 0.01f;
			return;
		}

		Vector2 from = activeSource != null && IsInstanceValid(activeSource)
			? activeSource.GlobalPosition
			: (originNode != null && IsInstanceValid(originNode) ? originNode.GlobalPosition : GlobalPosition);
		Vector2 to = target.GlobalPosition;
		RenderBolt(from, to);
		ApplyDamage(target);

		activeSource = target;
		showingArc = true;
		stateTimer = ArcDuration;
	}

	private void ApplyDamage(Node target)
	{
		if (PlayerRef == null)
			return;

		int baseDamage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 1) * DamageMultiplier));
		int hopDamage = Math.Max(1, Mathf.RoundToInt(baseDamage * MathF.Pow(ChainDamageMultiplier, bounceIndex)));
		float critBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.CritChance, CurrentLevel) ?? 0f;
		PlayerRef.DealDamageToEnemy(target, hopDamage, critBonus, allowElementalChain: false);
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

		// Jag the bolt with a random number of bends (2-4) and random perpendicular offsets each
		// cast so no two strikes trace the same path.
		int bends = (int)(GD.Randi() % 3) + 2;
		float maxWobble = Math.Min(30f, length * 0.22f);
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
	}
}
