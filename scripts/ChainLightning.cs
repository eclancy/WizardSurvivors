using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Dedicated Chain Lightning behavior: a lightning strike that can fork into multiple delayed arcs.
//
// An element path (Storm Surge, the level 4 branch into Water - ElementPaths) is painted here on
// every hop: the arc in the element's hot tone with a wet glow left along it. The hops after the
// first are spawned from the scene and never pass through Player.ApplyLegendaryVisual, so a tint
// laid on the first arc alone would have changed colour halfway down the chain.
public partial class ChainLightning : Area2D, IPaintsElementPath
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
	// How many hops the chain gets before any level or upgrade bonus. Zero preserves the old
	// behaviour, where the first hop had to be bought with a projectile-count or arc upgrade -
	// which is why Chain Lightning did not chain at all until level 3 despite being named for it.
	[Export] public int BaseArcDepth { get; set; } = 0;

	[Export] public bool GuaranteedSlow { get; set; } = false;
	[Export] public float SlowMultiplier { get; set; } = 0.75f;
	[Export] public float SlowDuration { get; set; } = 2.0f;
	[Export] public bool GuaranteedVulnerable { get; set; } = false;
	[Export] public float VulnerableBonus { get; set; } = 0.20f;
	[Export] public float VulnerableDuration { get; set; } = 3.0f;

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

	// SHADOW BOLT'S CARDS, EACH ITS OWN (.ai/spell-roster.md section 2e, built 2026-10-29). Shadow Bolt
	// runs on this script, and every hook below is switched on by one of its milestone ids, which no
	// Chain Lightning ever holds - Chain Lightning's behaviour is untouched. Each card changes what the
	// curse does where you can see it, and the numbers ride along; what it leaves behind is ShadowCurse's.
	//
	// Black Sigil: everything the curse touches wears a sigil at its feet; the next hit it takes from any
	// spell breaks it in a burst of shadow that lays the curse on everything near.
	public const string BlackSigilId = "shadow_bolt_withering_mark";
	public const float SigilSeconds = 4f;
	public const float SigilBurstRadius = 72f;
	public const float SigilBurstDamageShare = 4f;
	// Creeping Rot: every arc sinks into the ground as a path of rot that curses whatever crosses it.
	public const string CreepingRotId = "shadow_bolt_creeping_rot";
	public const float RotPathSeconds = 2.5f;
	// Spreading Dark: the curse forks at every jump - read through its branch and arc counts, named here
	// so the card and the script point at one id.
	public const string SpreadingDarkId = "shadow_bolt_spreading_dark";
	// Anathema: where the curse ends, a pall of night hangs over the last enemy it reached.
	public const string AnathemaId = "shadow_bolt_anathema";
	public const float PallRadius = 80f;
	public const float PallSeconds = 3f;
	public const float PallDamageShare = 1f;
	// Threefold Curse: three curses leave the staff at once, at three enemies, each chaining on its own.
	public const string ThreefoldId = "shadow_bolt_plaguebearer";
	public const int ThreefoldCurses = 3;

	// A curse the cast threw beside its first (Threefold Curse), which throws no more of its own.
	private bool volleyChild;

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
		// The strike and its arcs are in the air, over the bodies they cross (DrawLayers). The aura a
		// Shadow Bolt leaves is on the arena's floor, not a child of this.
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
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
		if (SpellData?.GetBranchElement() is Element path)
			PaintElementPath(path);
		if (SpellData?.IsLegendary == true)
			Modulate = new Color(1.0f, 0.9f, 0.45f, 1.0f);
	}

	private void PaintElementPath(Element path)
	{
		ElementColors.Ramp ramp = ElementColors.GetRamp(path);
		boltLine.DefaultColor = new Color(ramp.Hot, 0.95f);
		// The glow the Shadow Bolt scene leaves along its arc, in the element's hot tone: rain-slick
		// light hanging on the air for half a second after each hop, so the net it casts can be read
		// after the 0.09 s arc itself has gone.
		SpawnResidualAuraTrail = true;
		ResidualAuraColor = new Color(ramp.Hot, 0.6f);
		ResidualAuraDuration = 0.5f;
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
		if (!volleyChild && SpellData?.HasEvolution(ThreefoldId) == true)
			ThrowVolley(playerNode, firstTarget);
	}

	// Threefold Curse: the cast's other curses, each at the next nearest enemy in reach of the wizard,
	// each chaining on its own from there. Once a cast, so the scan and the instances are a cast's cost.
	private void ThrowVolley(Node2D playerNode, Node2D firstTarget)
	{
		Node parent = GetParent();
		PackedScene packed = string.IsNullOrEmpty(SceneFilePath) ? null : ResourceLoader.Load<PackedScene>(SceneFilePath);
		if (packed == null || parent == null || !IsInstanceValid(parent))
			return;
		float range = SpellData?.GetRangeAtLevel(CurrentLevel) ?? 450f;
		Vector2 from = playerNode.GlobalPosition;
		Node2D previous = firstTarget;
		for (int n = 1; n < ThreefoldCurses; n++)
		{
			Node2D next = null;
			float best = range * range;
			foreach (Enemy enemy in EnemyRegistry.Snapshot)
			{
				if (!IsInstanceValid(enemy) || enemy.IsDying || enemy == firstTarget || enemy == previous)
					continue;
				float d = from.DistanceSquaredTo(enemy.GlobalPosition);
				if (d < best && (n == 1 || d >= from.DistanceSquaredTo(previous.GlobalPosition)))
				{
					best = d;
					next = enemy;
				}
			}
			if (next == null)
				return;
			previous = next;
			if (packed.Instantiate() is not ChainLightning curse)
				return;
			curse.SpellData = SpellData;
			curse.CurrentLevel = CurrentLevel;
			curse.DamageMultiplier = DamageMultiplier;
			curse.AreaMultiplier = AreaMultiplier;
			curse.DurationMultiplier = DurationMultiplier;
			curse.ProjectileCountBonus = ProjectileCountBonus;
			curse.PlayerRef = PlayerRef;
			curse.Scale = Scale;
			curse.volleyChild = true;
			curse.SetSpellLevel(CurrentLevel);
			parent.AddChild(curse);
			curse.CastFromPlayer(playerNode, next);
		}
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
					CurseEnds();
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
			CurseEnds();
			QueueFree();
			return;
		}
		if (!SpawnBranches())
			CurseEnds();
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
		LeaveCurse(activeSource, activeTarget);
		showingArc = true;
		waitingToBranch = false;
		stateTimer = ArcDuration;
	}

	private int GetInitialArcDepth()
	{
		int baseDepth = Math.Max(0, (SpellData?.GetProjectileCountAtLevel(CurrentLevel) ?? 1) - 1 + ProjectileCountBonus);
		// With the Chain tag's level line (Effect Chain = more arcs), read since 2026-10-26.
		int chainDepth = (SpellData?.GetChainArcCountAtLevel(CurrentLevel) ?? 0)
			+ Mathf.RoundToInt(SpellData?.GetEffectValueAtLevel(SpellEffect.Chain, CurrentLevel) ?? 0f);
		return Math.Max(0, BaseArcDepth + baseDepth + chainDepth);
	}

	private int GetBranchCount()
	{
		int branchBonus = SpellData?.GetChainBranchCountAtLevel(CurrentLevel) ?? 0;
		return Math.Max(1, 1 + branchBonus);
	}

	// True when the chain went on from here; false when it ended on this target.
	private bool SpawnBranches()
	{
		if (remainingArcDepth <= 0 || activeTarget == null || !IsInstanceValid(activeTarget))
			return false;

		// The jump takes the evolutions' Area (Storm Reach, Cascade - the Stormcaller's Apotheosis
		// path), which promised "a wider jump" and reached nothing until 2026-10-26. The player's own
		// area stays out of it, as it always has: that is the bolt's width.
		float jumpRadius = ChainRadius * (SpellData?.GetAreaMultiplierAtLevel(CurrentLevel) ?? 1f);
		var candidates = EnemyRegistry.Snapshot
			.OfType<Node2D>()
			.Where(enemy => enemy != null
				&& IsInstanceValid(enemy)
				&& enemy.HasMethod("TakeDamage")
				&& !visitedTargets.Contains(enemy)
				&& enemy != activeTarget
				&& activeTarget.GlobalPosition.DistanceTo(enemy.GlobalPosition) <= jumpRadius)
			.OrderBy(enemy => activeTarget.GlobalPosition.DistanceTo(enemy.GlobalPosition))
			.Take(GetBranchCount())
			.ToList();

		if (candidates.Count == 0)
			return false;

		string scenePath = SceneFilePath;
		if (string.IsNullOrEmpty(scenePath))
			return false;

		Node parent = GetParent();
		if (parent == null || !IsInstanceValid(parent))
			return false;

		var packed = ResourceLoader.Load<PackedScene>(scenePath);
		if (packed == null)
			return false;

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
				// The evolution's visual scale (Thunderhead) rides every hop, not only the first.
				branch.Scale = Scale;
				branch.ChainDamageMultiplier = ChainDamageMultiplier;
				branch.BaseLineWidth = BaseLineWidth;
				branch.SetSpellLevel(CurrentLevel);
			}

			parent.AddChild(branchNode);
			if (branchNode is ChainLightning branchLightning)
				branchLightning.StartArc(activeTarget, next, remainingArcDepth - 1, chainHopIndex + 1, new HashSet<Node2D>(visitedTargets));
		}
		return true;
	}

	// The curse as one hit of this bolt lays it: what ShadowCurse's sigils, paths and palls carry on.
	private ShadowCurse.Curse CurseOfOneHit()
	{
		int poisonTick = !GuaranteedPoison ? 0 : Math.Max(1, PoisonDamagePerTick + (SpellData?.GetPoisonTickBonusAtLevel(CurrentLevel) ?? 0)
			+ Mathf.RoundToInt(SpellData?.GetEffectValueAtLevel(SpellEffect.DotDamage, CurrentLevel) ?? 0f));
		float slow = !GuaranteedSlow ? 1f
			: Mathf.Clamp(SlowMultiplier - (SpellData?.GetEffectValueAtLevel(SpellEffect.SlowPower, CurrentLevel) ?? 0f), 0.15f, 1.0f);
		float vulnerable = !GuaranteedVulnerable ? 0f
			: VulnerableBonus + (SpellData?.GetEffectValueAtLevel(SpellEffect.Vulnerability, CurrentLevel) ?? 0f);
		return new ShadowCurse.Curse(poisonTick, PoisonDuration, slow, SlowDuration, vulnerable, VulnerableDuration, BaseHitDamage());
	}

	private int BaseHitDamage() => Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 1) * DamageMultiplier));

	// Black Sigil and Creeping Rot, on every hop: the sigil on what it struck, the rot along the arc.
	private void LeaveCurse(Node2D source, Node2D target)
	{
		if (SpellData == null || PlayerRef == null)
			return;
		bool sigil = SpellData.HasEvolution(BlackSigilId);
		bool rot = SpellData.HasEvolution(CreepingRotId);
		if (!sigil && !rot)
			return;
		ShadowCurse ground = ShadowCurse.For(GetParent(), SpellData, PlayerRef);
		if (ground == null)
			return;
		ShadowCurse.Curse curse = CurseOfOneHit();
		if (sigil && target is Enemy marked)
			ground.MarkSigil(marked, SigilSeconds * DurationMultiplier, SigilBurstRadius * AreaMultiplier,
				Math.Max(1, Mathf.RoundToInt(curse.HitDamage * SigilBurstDamageShare)), curse);
		if (rot && source != null && IsInstanceValid(source))
			ground.LayPath(source.GlobalPosition, target.GlobalPosition, RotPathSeconds * DurationMultiplier, curse);
	}

	// Anathema: the curse has ended on activeTarget - night falls over it.
	private void CurseEnds()
	{
		if (SpellData?.HasEvolution(AnathemaId) != true || PlayerRef == null || activeTarget == null || !IsInstanceValid(activeTarget))
			return;
		ShadowCurse ground = ShadowCurse.For(GetParent(), SpellData, PlayerRef);
		if (ground == null)
			return;
		ShadowCurse.Curse curse = CurseOfOneHit();
		curse = curse with { HitDamage = Math.Max(1, Mathf.RoundToInt(curse.HitDamage * PallDamageShare)) };
		ground.DropPall(activeTarget.GlobalPosition, PallRadius * AreaMultiplier, PallSeconds * DurationMultiplier, curse);
	}

	private void ApplyDamage(Node target)
	{
		if (PlayerRef == null)
			return;

		int baseDamage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 1) * DamageMultiplier));
		int hopDamage = Math.Max(1, Mathf.RoundToInt(baseDamage * MathF.Pow(ChainDamageMultiplier, chainHopIndex)));
		float critBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.CritChance, CurrentLevel) ?? 0f;
		PlayerRef.DealDamageToEnemy(target, hopDamage, critBonus, allowElementalChain: false, source: SpellData);
		ApplyGuaranteedEffects(target);
	}

	private void ApplyGuaranteedEffects(Node target)
	{
		if (GuaranteedPoison && target.HasMethod("ApplyPoison"))
		{
			int poisonTick = Math.Max(1, PoisonDamagePerTick + (SpellData?.GetPoisonTickBonusAtLevel(CurrentLevel) ?? 0)
				+ Mathf.RoundToInt(SpellData?.GetEffectValueAtLevel(SpellEffect.DotDamage, CurrentLevel) ?? 0f)); // the Dot tag's level line
			target.Call("ApplyPoison", poisonTick, PoisonDuration);
		}

		// Slow magnitude scales with the spell rather than being fixed, so a debuff spell levelling
		// up actually deepens its debuffs instead of only widening its chain.
		if (GuaranteedSlow && target.HasMethod("ApplySlow"))
		{
			float bonus = SpellData?.GetEffectValueAtLevel(SpellEffect.SlowPower, CurrentLevel) ?? 0f;
			float multiplier = Mathf.Clamp(SlowMultiplier - bonus, 0.15f, 1.0f);
			target.Call("ApplySlow", multiplier, SlowDuration);
		}

		if (GuaranteedVulnerable && target.HasMethod("ApplyVulnerable"))
		{
			// The spell can raise its own vulnerability through a level-up or an evolution; Enemy
			// clamps the total, so an option that grants more cannot run away with the damage budget.
			float bonus = SpellData?.GetEffectValueAtLevel(SpellEffect.Vulnerability, CurrentLevel) ?? 0f;
			target.Call("ApplyVulnerable", VulnerableBonus + bonus, VulnerableDuration);
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

		// The bends are worked out in world space, and the line is told them in its own. They were
		// handed over as they were, which is only right while the node sits at the origin unscaled:
		// Thunderhead's x1.5 (Player.ApplyLegendaryVisual) scaled every point away from the world
		// origin, so the first hop was drawn somewhere else entirely. The scale still thickens the line.
		Vector2[] worldPoints = points.ToArray();
		var localPoints = new Vector2[worldPoints.Length];
		for (int i = 0; i < worldPoints.Length; i++)
			localPoints[i] = boltLine.ToLocal(worldPoints[i]);
		boltLine.Points = localPoints;
		boltLine.Visible = true;
		if (SpawnResidualAuraTrail)
			SpawnAuraTrail(from, to, boltLine.Width, worldPoints);
	}

	private void SpawnAuraTrail(Vector2 from, Vector2 to, float lineWidth, Vector2[] worldPoints)
	{
		if (ResidualAuraDuration <= 0.01f)
			return;

		Node parent = GetParent();
		if (parent == null || !IsInstanceValid(parent))
			return;

		if (worldPoints == null || worldPoints.Length < 2)
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
			Points = worldPoints,
			Width = Math.Max(2f, lineWidth * ResidualAuraWidthScale),
			DefaultColor = ResidualAuraColor,
			ZIndex = -1
		};
		parent.AddChild(auraLine);

		// Eased in, so the trail holds most of its strength for the first part of its life and goes
		// late: it is what the eye reads the arc by once the arc itself (ArcDuration, a few frames)
		// has gone. It faded from its first frame, and Shadow Bolt's was at a quarter strength besides
		// - Eric, 2026-10-28: "shadow bolt renders and unrenders too quickly, show it for longer so the
		// player can see it properly." The hop timing is ArcDuration's and is not touched.
		Tween trailTween = auraLine.CreateTween();
		trailTween.SetParallel(true);
		trailTween.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
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
