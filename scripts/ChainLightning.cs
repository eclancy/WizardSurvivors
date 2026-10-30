using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Dedicated Chain Lightning behavior: a lightning strike that can fork into multiple delayed arcs.
//
// An element path (Storm Surge, the level 4 branch into Water - ElementPaths) is painted here on
// every hop: the arc in the element's own ramp with a wet glow left along it. The hops after the
// first are spawned from the scene and never pass through Player.ApplyLegendaryVisual, so a tint
// laid on the first arc alone would have changed colour halfway down the chain.
//
// THE ARC IS PIXEL ART (the pixel-art pass, 2026-10-29; .ai/art-replacement-manifest.md section 9). It
// was a Line2D - antialiased in Shadow Bolt's scene - so at any angle but the four square ones a
// smooth rotated quad, and the glow it left a translucent antialiased Line2D tweened out on the arena.
// Both are stamped cell by cell on the world's 2-unit grid now (PixelInk): the arc an element-edge
// under-stroke with a seam of its hot light (white-hot on Return Stroke's way home), the glow its edge
// sinking into the floor and dissolving through the Bayer matrix (Afterglow, below). Same points, same
// timing: an arc still lasts ArcDuration, and every hop's bends are rolled as they were.
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

	// CHAIN LIGHTNING'S CARDS, EACH ITS OWN (.ai/spell-roster.md section 2h, phase 2, 2026-10-29). Four of
	// its five moved numbers on the chain - more hops and a wider jump, +40% and crits, splits on splits, x2
	// and a hop. Each now changes what the chain does where you can see it, out of what it is: a heavy bolt
	// that hops close and hard. Like Shadow Bolt's, every hook is switched on by a Chain Lightning id that no
	// Shadow Bolt holds, so Shadow Bolt is untouched. Storm Reach and Cascade are the Stormcaller's
	// Apotheosis path (ApotheosisCatalog): the Lattice links whatever the chain strikes, so it rides on
	// both unchanged. Storm Surge, the Water element path, is as it was.
	//
	// Storm Reach: the chain leaps long - every hop goes to the farthest enemy in reach, not the nearest.
	public const string StormReachId = "chain_lightning_storm_reach";
	// Return Stroke: where the chain ends, the stroke runs back along it to the wizard's hand, striking
	// every enemy on the way again.
	public const string ReturnStrokeId = "chain_lightning_dense_current";
	public const float ReturnShare = 0.8f;
	// The stroke runs back a hop at a time, quicker than it went out, so it reads as one flash going home.
	public const float ReturnStepSeconds = 0.05f;
	// Cascade: every enemy a hop kills throws CascadeForks more arcs out of its body, and the chain goes on
	// a hop further from it, up to CascadeMaxForks extra arcs and CascadeMaxHops hops in all a cast.
	public const string CascadeId = "chain_lightning_cascade";
	public const int CascadeForks = 2;
	public const int CascadeMaxForks = 12;
	public const int CascadeMaxHops = 36;
	// Electrocution: the first enemy the bolt strikes is held in the current, convulsing, struck again
	// every ElectrocuteTickSeconds for ElectrocuteShare of the bolt; if it dies held, the current leaps to
	// the nearest enemy within ElectrocuteJumpReach and holds that for what is left (Electrocutions).
	public const string ElectrocutionId = "chain_lightning_thunderhead";
	public const float ElectrocuteSeconds = 1.2f;
	public const float ElectrocuteTickSeconds = 0.2f;
	public const float ElectrocuteShare = 0.4f;
	public const float ElectrocuteJumpReach = 110f;

	// One cast's shared reckoning across every branch: the arcs Cascade's kills may still throw, and the
	// hops the whole cast may still make once it has started throwing them.
	private sealed class CastState
	{
		public int CascadeLeft;
		public int HopsLeft;
	}

	// Cascade: this hop killed what it struck, so the arcs out of it fork.
	private bool forkOnKill;

	private CastState cast;
	// Return Stroke's road home: every body the chain struck, in order from the wizard, and where each
	// stood when it was struck (a body that has since fallen is returned to where it fell).
	private List<Node2D> path;
	private List<Vector2> pathAt;
	private bool returning;
	private int returnIndex;

	private static readonly List<Enemy> nearScratch = new(64);
	private static readonly List<Node2D> picked = new(8);
	private static readonly ElementColors.Ramp LightningRamp = ElementColors.GetRamp(Element.Lightning);

	// A curse the cast threw beside its first (Threefold Curse), which throws no more of its own.
	private bool volleyChild;

	private ArcStroke arcStroke;
	// The ramp the arc and its glow are drawn in: Lightning, Shadow Bolt's Darkness, or an element path's.
	private ElementColors.Ramp arcRamp = LightningRamp;
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
		arcStroke = new ArcStroke { Name = "Arc", Visible = false };
		AddChild(arcStroke);
		if (SpellData?.Id == "shadow_bolt")
			arcRamp = ElementColors.GetRamp(Element.Darkness);
		chainRng.Randomize();
		if (SpellData?.GetBranchElement() is Element path)
			PaintElementPath(path);
		if (SpellData?.IsLegendary == true)
			Modulate = new Color(1.0f, 0.9f, 0.45f, 1.0f);
	}

	private void PaintElementPath(Element path)
	{
		arcRamp = ElementColors.GetRamp(path);
		// The glow the Shadow Bolt scene leaves along its arc, in the element's ramp: rain-slick light
		// hanging on the floor for half a second after each hop, so the net it casts can be read after the
		// 0.09 s arc itself has gone.
		SpawnResidualAuraTrail = true;
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
		if (SpellData != null)
		{
			bool cascade = SpellData.HasEvolution(CascadeId);
			cast = new CastState { CascadeLeft = cascade ? CascadeMaxForks : 0, HopsLeft = cascade ? CascadeMaxHops : int.MaxValue };
			if (SpellData.HasEvolution(ReturnStrokeId))
			{
				path = new List<Node2D> { playerNode };
				pathAt = new List<Vector2> { playerNode.GlobalPosition };
			}
		}
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
		if (returning)
		{
			TickReturn((float)delta);
			return;
		}

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
				arcStroke.Visible = false;
				showingArc = false;
				if (remainingArcDepth <= 0)
				{
					if (!ChainEnds())
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
			if (!ChainEnds())
				QueueFree();
			return;
		}
		if (SpawnBranches() || !ChainEnds())
			QueueFree();
	}

	// The chain has ended on activeTarget. True while this node lives on to carry the stroke home.
	private bool ChainEnds()
	{
		CurseEnds();
		if (path == null || path.Count < 2 || SpellData?.HasEvolution(ReturnStrokeId) != true)
			return false;
		returning = true;
		returnIndex = path.Count - 1;
		stateTimer = 0f;
		arcStroke.Homeward = true;
		return true;
	}

	// Return Stroke: from the body the chain ended on back to the wizard, one hop a step, each struck
	// again on the way; the last hop lands in his hand and strikes nothing.
	private void TickReturn(float dt)
	{
		stateTimer -= dt;
		if (stateTimer > 0f)
			return;
		if (returnIndex <= 0)
		{
			QueueFree();
			return;
		}
		Vector2 from = PathPosition(returnIndex);
		Vector2 to = PathPosition(returnIndex - 1);
		RenderBolt(from, to);
		returnIndex--;
		if (returnIndex > 0 && path[returnIndex] is Enemy struck && IsInstanceValid(struck) && !struck.IsDying && PlayerRef != null)
		{
			int baseDamage = Math.Max(1, Mathf.RoundToInt((SpellData?.GetDamageAtLevel(CurrentLevel) ?? 1) * DamageMultiplier));
			float critBonus = SpellData?.GetEffectValueAtLevel(SpellEffect.CritChance, CurrentLevel) ?? 0f;
			PlayerRef.DealDamageToEnemy(struck, Math.Max(1, Mathf.RoundToInt(baseDamage * ReturnShare)), critBonus,
				allowElementalChain: false, source: SpellData);
			ApplyImpactShock(struck);
		}
		stateTimer = ReturnStepSeconds;
	}

	private Vector2 PathPosition(int index)
	{
		Node2D node = path[index];
		if (node != null && IsInstanceValid(node))
			pathAt[index] = node.GlobalPosition;
		return pathAt[index];
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
		path?.Add(target);
		pathAt?.Add(target.GlobalPosition);
		if (chainHopIndex == 0)
			Electrocute(activeTarget);
		ApplyDamage(activeTarget);
		ApplyImpactShock(activeTarget);
		LeaveCurse(activeSource, activeTarget);
		FeedOnKill(activeTarget);
		showingArc = true;
		waitingToBranch = false;
		stateTimer = ArcDuration;
	}

	// Cascade: a body the hop just killed throws more arcs out of itself - the hop out of it forks - and the
	// chain goes on a hop further from it, out of the cast's shared allowance. The kill flashes, so the
	// player sees which kill split the storm.
	private void FeedOnKill(Node2D target)
	{
		if (cast == null || cast.CascadeLeft <= 0 || target is not Enemy killed || !killed.IsDying)
			return;
		forkOnKill = true;
		remainingArcDepth += 1;
		if (GetParent() is Node2D arena)
			ImpactDebrisLayer.Spawn(arena, killed.GlobalPosition, 26f, LightningRamp, Colors.White);
	}

	// Electrocution: the first body the bolt strikes is held in the current (Electrocutions).
	private void Electrocute(Node2D target)
	{
		if (SpellData?.HasEvolution(ElectrocutionId) != true || PlayerRef == null || target is not Enemy held)
			return;
		int baseDamage = Math.Max(1, Mathf.RoundToInt((SpellData.GetDamageAtLevel(CurrentLevel)) * DamageMultiplier));
		Electrocutions.For(GetParent(), SpellData, PlayerRef)?.Hold(held, ElectrocuteSeconds * DurationMultiplier,
			Math.Max(1, Mathf.RoundToInt(baseDamage * ElectrocuteShare)));
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
		// The nearest enemies in the jump, one per branch - or, with Storm Reach, the farthest: the chain
		// leaps the whole jump every time and sprawls across the pack instead of hopping through its edge.
		// Picked from the grid's near set by hand rather than a sorted query, so a hop allocates nothing
		// but its picks.
		bool leapFar = SpellData?.HasEvolution(StormReachId) == true;
		Vector2 from = activeTarget.GlobalPosition;
		float reach2 = jumpRadius * jumpRadius;
		int want = GetBranchCount();
		int forks = 0;
		if (forkOnKill && cast != null)
		{
			forks = Math.Min(CascadeForks, cast.CascadeLeft);
			want += forks;
		}
		if (cast != null)
			want = Math.Min(want, cast.HopsLeft);
		if (want <= 0)
			return false;
		EnemyRegistry.CollectNear(from, jumpRadius, nearScratch);
		picked.Clear();
		for (int n = 0; n < want; n++)
		{
			Enemy best = null;
			float bestD = leapFar ? -1f : float.MaxValue;
			foreach (Enemy enemy in nearScratch)
			{
				if (!IsInstanceValid(enemy) || enemy.IsDying || enemy == activeTarget || visitedTargets.Contains(enemy) || picked.Contains(enemy))
					continue;
				float d = from.DistanceSquaredTo(enemy.GlobalPosition);
				if (d > reach2)
					continue;
				if (leapFar ? d > bestD : d < bestD)
				{
					best = enemy;
					bestD = d;
				}
			}
			if (best == null)
				break;
			picked.Add(best);
		}
		nearScratch.Clear();
		if (picked.Count == 0)
			return false;
		if (cast != null)
		{
			cast.HopsLeft -= picked.Count;
			// Only the arcs the kill actually threw are spent from Cascade's allowance.
			cast.CascadeLeft -= Math.Max(0, Math.Min(forks, picked.Count - GetBranchCount()));
		}
		// Copied out before any branch strikes: a strike can kill, and nothing below may read the shared
		// lists after it.
		Node2D[] candidates = picked.ToArray();
		picked.Clear();

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
				branch.cast = cast;
				if (path != null)
				{
					branch.path = new List<Node2D>(path);
					branch.pathAt = new List<Vector2>(pathAt);
				}
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
		if (arcStroke == null)
			return;

		// The width the line had, the node's scale (an evolution's) included, in art pixels: the seam is
		// one pixel for a hop of the base width, two from about one and a half times it.
		float width = Math.Max(2f, BaseLineWidth * AreaMultiplier) * MathF.Abs(Scale.X);
		arcStroke.Seam = Math.Max(1, (int)MathF.Round(width / 5f));
		arcStroke.Ramp = arcRamp;
		Vector2 direction = to - from;
		float length = direction.Length();
		Vector2 normal = length > 0.0001f
			? direction.Normalized().Orthogonal()
			: Vector2.Up;

		// Jag the bolt with a random number of bends (3-5) and stronger perpendicular offsets each
		// cast so no two strikes trace the same path. World points: the stroke is drawn at the world's
		// origin, on the world's grid.
		int bends = (int)(GD.Randi() % 3) + 3;
		float maxWobble = Math.Min(40f, length * 0.28f);
		Vector2[] points = arcStroke.Points;
		points[0] = from;
		for (int i = 1; i <= bends; i++)
		{
			float t = i / (float)(bends + 1);
			Vector2 basePoint = from.Lerp(to, t);
			float offset = (float)GD.RandRange(-maxWobble, maxWobble);
			points[i] = basePoint + normal * offset;
		}
		points[bends + 1] = to;
		arcStroke.Count = bends + 2;
		arcStroke.Visible = true;
		arcStroke.QueueRedraw();
		if (SpawnResidualAuraTrail && length >= 4f && ResidualAuraDuration > 0.01f)
			Afterglow.For(GetParent())?.Lay(points, arcStroke.Count, ResidualAuraDuration,
				Math.Max(2, (int)MathF.Round(width * ResidualAuraWidthScale / PixelInk.Grid)), arcRamp);
	}

	/// <summary>
	/// The arc of one hop, stamped on the world's grid: the ramp's edge as an under-stroke two art pixels
	/// wider than the seam, and the seam of hot light down it - white-hot on Return Stroke's way home.
	/// Top-level at the origin, so its points are world points whatever the chain node's own transform.
	/// </summary>
	private sealed partial class ArcStroke : Node2D
	{
		public const int MaxPoints = 8;
		public readonly Vector2[] Points = new Vector2[MaxPoints];
		public int Count;
		public int Seam = 1;
		public bool Homeward;
		public ElementColors.Ramp Ramp = LightningRamp;

		public override void _Ready()
		{
			TopLevel = true;
			GlobalPosition = Vector2.Zero;
			ZAsRelative = false;
			ZIndex = DrawLayers.Air;
		}

		public override void _Draw()
		{
			if (Count < 2)
				return;
			for (int i = 1; i < Count; i++)
				PixelInk.Line(this, Points[i - 1], Points[i], Ramp.Edge, Seam + 2);
			Color seam = Homeward ? Ramp.Core : Ramp.Hot;
			for (int i = 1; i < Count; i++)
				PixelInk.Line(this, Points[i - 1], Points[i], seam, Seam);
			if (Seam >= 2 && !Homeward)
			{
				for (int i = 1; i < Count; i++)
					PixelInk.Line(this, Points[i - 1], Points[i], Ramp.Core, 1);
			}
		}
	}

	/// <summary>
	/// The glow an arc leaves on the floor (Shadow Bolt's scene, and any element path): the arc's own
	/// points in its ramp's edge, as wide as the line's glow was, thinning and dissolving through the
	/// Bayer matrix - holding most of its strength for the first part of its life and going late, so the
	/// net can be read after the arc itself has gone (Eric, 2026-10-28: "shadow bolt renders and
	/// unrenders too quickly"). A translucent antialiased Line2D and a Tween a hop until 2026-10-29; one
	/// node per arena now, a fixed ring of trails, nothing allocated per hop or per frame.
	/// </summary>
	private sealed partial class Afterglow : Node2D
	{
		private const int MaxTrails = 64;
		private const int MaxPoints = ArcStroke.MaxPoints;

		private struct Trail
		{
			public int Count;
			public int Cells;
			public float Age;
			public float Life;
			public ElementColors.Ramp Ramp;
			public bool Live;
		}

		private readonly Trail[] trails = new Trail[MaxTrails];
		private readonly Vector2[] points = new Vector2[MaxTrails * MaxPoints];
		private int next;

		private static Afterglow current;

		public static Afterglow For(Node arena)
		{
			if (current != null && IsInstanceValid(current) && current.IsInsideTree() && current.GetParent() == arena)
				return current;
			if (arena == null || !IsInstanceValid(arena))
				return null;
			current = new Afterglow { Name = "ArcAfterglow" };
			arena.AddChild(current);
			return current;
		}

		public override void _Ready()
		{
			TopLevel = true;
			GlobalPosition = Vector2.Zero;
			// On the floor, under the bodies the arc ran between.
			ZAsRelative = false;
			ZIndex = DrawLayers.Ground;
			SetProcess(false);
		}

		public override void _ExitTree()
		{
			if (current == this)
				current = null;
		}

		public void Lay(Vector2[] source, int count, float life, int cells, in ElementColors.Ramp ramp)
		{
			count = Math.Min(count, MaxPoints);
			for (int i = 0; i < count; i++)
				points[next * MaxPoints + i] = source[i];
			trails[next] = new Trail { Count = count, Cells = cells, Age = 0f, Life = life, Ramp = ramp, Live = true };
			next = (next + 1) % MaxTrails;
			SetProcess(true);
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;
			bool any = false;
			for (int i = 0; i < MaxTrails; i++)
			{
				ref Trail t = ref trails[i];
				if (!t.Live)
					continue;
				t.Age += dt;
				if (t.Age >= t.Life)
				{
					t.Live = false;
					continue;
				}
				any = true;
			}
			QueueRedraw();
			if (!any)
				SetProcess(false);
		}

		public override void _Draw()
		{
			for (int i = 0; i < MaxTrails; i++)
			{
				ref Trail t = ref trails[i];
				if (!t.Live)
					continue;
				float u = t.Age / t.Life;
				float coverage = 1f - u * u;
				int cells = Math.Max(1, (int)MathF.Round(t.Cells * (1f - u * 0.6f)));
				int at = i * MaxPoints;
				for (int k = 1; k < t.Count; k++)
					PixelInk.Line(this, points[at + k - 1], points[at + k], t.Ramp.Edge, cells, coverage);
				if (u < 0.5f)
				{
					for (int k = 1; k < t.Count; k++)
						PixelInk.Line(this, points[at + k - 1], points[at + k], t.Ramp.Mid, 1, coverage);
				}
			}
		}
	}
}
