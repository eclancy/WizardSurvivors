using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

public partial class Player : CharacterBody2D
{
	[Signal] public delegate void XpGainedEventHandler(int amount);
	[Signal] public delegate void LevelGainedEventHandler();
	[Signal] public delegate void DiedEventHandler();
	// Emitted whenever the player takes damage, before HP is reduced (issue #22). Reactive passive
	// Node2DGame listens for the damage-flash and low-health overlays.
	[Signal] public delegate void DamageTakenEventHandler(int amount);
	// Emitted whenever what the player HOLDS changes: a spell taken, levelled, mutated, ascended or
	// cut, a boon taken, a relic claimed (which can complete a Full Set and add its tags, or bind the
	// Blank Folio's seventh slot). The HUD's loadout strip (LoadoutHud) redraws on it rather than
	// polling. Raised here, at the mutation, rather than at Node2DGame's menu handlers, because the
	// handlers are not the only way in - the harnesses grant spells and boons directly.
	[Signal] public delegate void LoadoutChangedEventHandler();
	[Export] public float Speed { get; set; } = 220f;
	[Export] public PackedScene MagicMissileScene { get; set; }
	[Export] public PackedScene ArcaneExplosionScene { get; set; }
	[Export] public PackedScene SpiritualWeaponScene { get; set; }
	[Export] public PackedScene FireballScene { get; set; }
	[Export] public SpellData MagicMissileData { get; set; }
	[Export] public SpellData ArcaneExplosionData { get; set; }
	[Export] public SpellData SpiritualWeaponData { get; set; }
	[Export] public SpellData FireballData { get; set; }
	// Issue #13 roster expansion - 12 more offensive spells built on the shared ElementalBolt/
	// ElementalPulse/GroundSpike/OrbitingBlade scripts (see Player.EnsureCatalogDefaults and the
	// Fire*/FireOrRefresh* helpers below) instead of one bespoke script per spell.
	[Export] public PackedScene CinderbreathScene { get; set; }
	[Export] public PackedScene MirefootScene { get; set; }
	[Export] public PackedScene KindledWardScene { get; set; }
	[Export] public PackedScene GravewellScene { get; set; }
	[Export] public PackedScene HollowStarScene { get; set; }
	// The three found spells (.ai/side-events.md): each is a Discovery unlock from a side event.
	[Export] public PackedScene IronPalisadeScene { get; set; }
	[Export] public PackedScene ContagionScene { get; set; }
	[Export] public PackedScene BrambleSeedScene { get; set; }
	[Export] public PackedScene FrostShardScene { get; set; }
	[Export] public PackedScene RiptideScene { get; set; }
	[Export] public PackedScene ShadowBoltScene { get; set; }
	[Export] public PackedScene ThornVineScene { get; set; }
	[Export] public PackedScene GaleBladeScene { get; set; }
	[Export] public PackedScene MoltenShardScene { get; set; }
	[Export] public PackedScene ChainLightningScene { get; set; }
	[Export] public PackedScene VoidLanceScene { get; set; }
	[Export] public PackedScene GlacialSpikeScene { get; set; }
	[Export] public PackedScene SolarFlareScene { get; set; }
	[Export] public PackedScene ToxicSporeBurstScene { get; set; }
	[Export] public PackedScene ObsidianSpikeScene { get; set; }
	[Export] public PackedScene CycloneSlashScene { get; set; }
	// Issue #28 - D&D-inspired spells. ScorchingRayScene uses a beam animation script;
	// MeteorImpactScene uses a ground-targeted impact script.
	[Export] public PackedScene BlackTentaclesScene { get; set; }
	[Export] public PackedScene ConeOfColdScene { get; set; }
	[Export] public PackedScene ScorchingRayScene { get; set; }
	[Export] public PackedScene MeteorImpactScene { get; set; }
	[Export] public PackedScene HuntersArrowScene { get; set; }
	// The four elemental starters, one per playable character. They share ElementalBolt with the
	// roster above; what makes them starters is the tuning, not new machinery - each carries a
	// single element weight so the character it belongs to begins the run already pointed at one
	// element's thresholds rather than splitting weight across two.

	// --- Hunter's Draw -------------------------------------------------------------------------
	// The only spell in the game whose trigger is a player action rather than a timer: the bow
	// draws while you run and looses when you plant your feet. The cooldown still applies, but it
	// gates how fast you can nock the next arrow rather than when the shot goes off.

	// Ground covered for a full draw. Distance, not time, so standing still and wiggling the stick
	// cannot charge the bow, and so the Wind tier's move-speed bonus genuinely makes you draw
	// faster - which is the pairing the spell's element tags advertise.
	[Export] public float HuntersDrawFullDrawDistance { get; set; } = 260f;
	// Below this charge, stopping just relaxes the bow instead of loosing. Without it every
	// keyboard direction change would fling away a near-worthless arrow.
	[Export] public float HuntersDrawMinLooseCharge { get; set; } = 0.25f;
	// A stop has to last this long to count as "let go", so switching direction - which passes
	// through a frame or two of no input - does not fire the shot.
	[Export] public float HuntersDrawReleaseGrace { get; set; } = 0.10f;
	// How long a full draw can be held before the archer's arms give out and it looses anyway.
	// This is what stops a player who never stops moving from having a permanently dead spell.
	[Export] public float HuntersDrawHoldSeconds { get; set; } = 1.25f;
	// Fraction of a full draw's charge lost per second while standing still under the loose
	// threshold.
	[Export] public float HuntersDrawRelaxPerSecond { get; set; } = 1.2f;
	[Export] public float HuntersDrawMinDamageMultiplier { get; set; } = 0.45f;
	[Export] public float HuntersDrawMaxDamageMultiplier { get; set; } = 2.2f;
	[Export] public float HuntersDrawMinSpeedMultiplier { get; set; } = 0.55f;
	// Total fan width for a multi-arrow volley, in degrees. Split evenly around the aim line.
	[Export] public float HuntersDrawSpreadDegrees { get; set; } = 16f;
	// Extra pierce granted at full draw, on top of whatever the arrow scene and level give.
	[Export] public int HuntersDrawFullDrawPierceBonus { get; set; } = 1;

	private float huntersDrawCharge;
	private float huntersDrawStillSeconds;
	private float huntersDrawHoldSeconds;
	private Vector2 huntersDrawLastPosition;
	private bool huntersDrawTracking;
	private BowDrawVisual huntersDrawBow;
	[Export] public float FireInterval { get; set; } = 1.0f;
	[Export] public int StartingXP { get; set; } = 0;
	[Export] public int StartingLevel { get; set; } = 1;
	[Export] public int CurrentXP { get; set; } = 0;
	[Export] public int CurrentLevel { get; set; } = 1;
	[Export] public int XPToNextLevel { get; set; } = 10;
	[Export] public float UpgradeOfferWeight { get; set; } = 3.0f;
	[Export] public float NewUnlockOfferWeight { get; set; } = 1.0f;
	// The chance that a level-up shows a boon at all - one card, never more. Rolled once per
	// level-up in BeginLevelUpOffer, beside the upgrade roll and for the same reason: a reroll or a
	// ban re-deals WHICH boon, never WHETHER there is one.
	//
	// It used to be a per-card weight (1.2, against 1.0 for a new spell) inside the new-spell group,
	// and with fourteen boons against twenty-odd spells that put a boon on half the screens -
	// measured at 52% of hands, 0.81 boon cards per hand, so often two at once. Boons are meant to
	// be the rare card, and a weight cannot say "rare" when the pool size moves it. A chance can:
	// 0.22 measured at 21% of hands, one in 4.7, never more than one boon.
	// See .ai/passives-and-items.md, "How rare a boon is".
	[Export] public float BoonOfferChance { get; set; } = 0.22f;
	private bool boonOnOfferThisLevelUp;
	// When a boon is dealt, one carrying the element the player holds most of is this many times as
	// likely to be the one. Rarity is BoonOfferChance; this only decides which boon a rare offer is.
	[Export] public float BoonOwnElementWeight { get; set; } = 3f;

	/// <summary>
	/// Debug only: the next <see cref="BeginLevelUpOffer"/> puts a boon on the table regardless of
	/// <see cref="BoonOfferChance"/>. The screenshot harness uses it (_UiShot --force-boon), because
	/// once boons are rare a forced level-up shows one only about one time in five.
	/// </summary>
	public bool ForceBoonOnNextOffer { get; set; }
	// The chance that a level-up has upgrades to held spells in its pool at all. Rolled ONCE when the
	// level-up opens (BeginLevelUpOffer) and held until it closes, so a reroll or a ban deals again
	// from the same pool rather than fishing for an upgrade. This is what keeps a wizard's final form
	// out of the first half of a run: with an upgrade on nearly every screen, a focused player
	// climbed the starting spell to 7 almost without pause and took the Apotheosis before minute 3
	// (.ai/gameplay-design.md, "The Apotheosis").
	[Export] public float UpgradeOfferChance { get; set; } = 0.6f;
	private bool upgradesOnOfferThisLevelUp = true;

	/// <summary>Whether the level-up now open can offer upgrades to held spells. For the menu.</summary>
	public bool UpgradesOnOfferThisLevelUp => upgradesOnOfferThisLevelUp;
	// Share of the level-up draw reserved for upgrades to spells the player already owns.
	// These are group targets, not per-card weights: per-card weights alone are swamped by pool
	// size, because there are only ever 1-6 upgrade candidates against ~30 unlocked new spells.
	// Normalizing by group keeps the ratio stable no matter how much of the catalog is unlocked.
	// (A second share for a full loadout, 0.9, went with the swaps: a full book is offered no new
	// spells, so there is only one group left to share the draw.)
	[Export] public float UpgradeOfferShare { get; set; } = 0.75f;

	[Export] public int MaxHP { get; set; } = 20;
	[Export] public int CurrentHP { get; set; } = 20;
	[Export] public float BaseHealthRegenPerSecond { get; set; } = 0.25f;
	private Label earthMaxHpBonusLabel;
	private Dictionary<Node, float> enemyDamageCooldowns = new Dictionary<Node, float>();
	private const float DamageCooldownSeconds = 0.2f; // 12 frames at 60fps
	private HashSet<Node> overlappingEnemies = new HashSet<Node>();
	// Grace period after closing a pausing menu. The level-up menu can be opened while standing
	// inside a swarm, and unpausing used to resume contact damage on the very next physics tick
	// with no chance to react. While this is > 0 the player ignores all incoming damage.
	[Export] public float PostMenuInvincibilitySeconds { get; set; } = 2.0f;
	private float invincibilityTimeRemaining = 0f;
	private static readonly Shader InvincibilityShader =
		ResourceLoader.Load<Shader>("res://scenes/shaders/invincibility_white.gdshader");
	private ShaderMaterial? invincibilityMaterial;
	// The sprite's material before the grace period started, restored when it ends.
	private Material? materialBeforeInvincibility;
	private bool invincibilityVisualActive = false;
	// Damage-absorbing shield pool (e.g. Aegis Ward), consumed before HP in TakeDamage().
	private int shieldPoints = 0;

	/// <summary>The shield pool as it stands, for the health HUD.</summary>
	public int ShieldPoints => shieldPoints;

	/// <summary>
	/// How much of the Reliquary Vial's overheal can be banked, as a fraction of max HP.
	/// </summary>
	/// <remarks>
	/// The vial used to hand overheal to <see cref="AddShield"/>, which keeps the LARGER of the old
	/// and new pool - right for a spell that recharges a shield, wrong for overheal. Overheal mostly
	/// arrives one point at a time (a kill under the Tithe Chalice, a lifesteal tick), so the pool
	/// could never grow past the single biggest overheal, which was usually 1. It now accumulates,
	/// and the cap is what keeps it a buffer rather than a second health bar: at 20% it is worth a
	/// few hits, never a free life. The player asked for the mechanic to come with a number, and
	/// this is the number; ChestItemCatalog.OverhealDescription reads it.
	/// </remarks>
	public const float OverhealShieldCapFraction = 0.20f;

	/// <summary>The most overheal the shield pool will bank, in points: 20% of max HP.</summary>
	public int OverhealShieldCap => Math.Max(1, Mathf.RoundToInt(MaxHP * OverhealShieldCapFraction));
	private AnimatedSprite2D? shieldAura;
	// Tracks the Earth element's max HP tier bonus currently applied to MaxHP, so it can be
	// added/removed incrementally as element instance counts shift during a run (issue #16).
	private int earthMaxHpBonusApplied = 0;




	// Track fire timers by spell id.
	public Dictionary<string, float> spellFireTimers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
	private Vector2 _velocity = Vector2.Zero;

	// Runtime spell instances equipped by the player.
	public List<SpellData> equippedSpells = new List<SpellData>();
	private Dictionary<string, SpellData> spellCatalog = new Dictionary<string, SpellData>(StringComparer.OrdinalIgnoreCase);
	private RandomNumberGenerator levelUpRng = new RandomNumberGenerator();
	public bool IsDead { get; private set; } = false;
	private float damageMultiplier = 1.0f;
	private float cooldownMultiplier = 1.0f;
	private float attackSpeedMultiplier = 1.0f;
	private float areaMultiplier = 1.0f;
	private float durationMultiplier = 1.0f;
	private int amountBonus = 0;
	// Reused by the magic missile volley so picking targets does not allocate on every cast.
	private readonly List<Node2D> magicMissileTargets = new();
	private float growthMultiplier = 1.0f;
	private float recoveryPerSecond = 0.0f;
	private float recoveryAccumulator = 0.0f;
	// The Geomancer's starting passive, now a percentage like every other armour source.
	//
	// Worth recording that this INVERTS what it used to do. It was deliberately flat, because flat
	// reduction blunts the swarm's chip damage almost completely while leaving a boss slam nearly
	// as dangerous - and that asymmetry was the Geomancer's identity. Percentage armour does the
	// opposite: it barely touches a one-damage contact hit and takes a real bite out of a slam.
	// The trade was made knowingly. Flat reduction cannot stay in a game whose ordinary enemy deals
	// one damage, because the first point of it deletes the entire basic horde, and "one stat that
	// is easy to understand" does not survive having two kinds of mitigation to explain.
	private float characterArmorPercent = 0f;
	// Boons. Permanent for the run, never levelled, and held apart from the spell loadout.
	private readonly List<string> ownedBoonIds = new();
	private float boonArmorPercent = 0f;
	private float boonEvasionPercent = 0f;
	private int boonLuckBonus = 0;
	private float boonMoveSpeedMultiplier = 1f;

	// Reactive boons (Saltbound Chain, Rimebriar). Zero and 1f mean "not held", so the whole
	// reaction path costs one comparison per hit for a player holding neither.
	private int boonRetaliateDamage = 0;
	private float boonChillMultiplier = 1f;
	private float boonReactionCooldownRemaining = 0f;

	// One radius and one cooldown shared by both reactions, deliberately. They fire off the same
	// trigger, so two separate clocks would only let a player holding both get twice the pulses -
	// and the cooldown exists because contact damage ticks several times a second. Without it,
	// walking into a crowd would turn a reaction into a permanent damage aura, which is exactly
	// the always-on passive these replaced.
	private const float BoonReactionRadius = 130f;
	private const float BoonReactionCooldownSeconds = 1.0f;
	private const float BoonChillDurationSeconds = 2.0f;
	// Temporary buff from a Bonus Drop Table one-time-use item (issue #25). Applied additively to
	// attackSpeedMultiplier/Speed on pickup and reverted when the timer expires, so every existing
	// consumer of those two fields benefits automatically without needing its own buff-aware code path.
	private float buffAttackSpeedBonus = 0f;
	private float buffMoveSpeedPixels = 0f;
	private float buffTimeRemaining = 0f;
	private int magnetBonus = 0;
	private int extraLives = 0;
	public int RerollsPerLevelUp { get; private set; } = 0;

	// Level-up charges (see scripts/LevelUpCharges.cs). Rerolls refill every level-up; these three
	// are spent from a pool that lasts the whole run, which is what makes using one a decision.
	public int BansRemaining { get; private set; } = 0;
	public int BanksRemaining { get; private set; } = 0;
	public int AuguriesRemaining { get; private set; } = 0;

	// Level-ups deferred by a Save charge. Node2DGame spends these by reopening the menu after a
	// pick rather than by granting a level, so banking never touches the XP curve.
	public int BankedLevelUps { get; private set; } = 0;

	// Spells the player has struck off for the rest of the run.
	private readonly HashSet<string> bannedSpellIds = new(StringComparer.OrdinalIgnoreCase);

	// Set by an augury, consumed by the very next offer. One-shot on purpose: a permanent weighting
	// would quietly become "this spell is always in your options", which is a different feature.
	private string guaranteedNextOfferSpellId = string.Empty;
	public bool IsPlaytestModeEnabled { get; private set; } = false;
	public int MagnetBonus => magnetBonus + chestMagnetBonus;
	// Crit chance stat (issue #26) and Luck stat (issue #23), both driven by SaveManager.ArcaneUpgradeLevels.
	private float baseCritChance = 0f;
	private int luckLevel = 0;
	[Export] public float CritDamageMultiplier { get; set; } = 1.6f;
	[Export] public float FireProximityRange { get; set; } = 100f;
	[Export] public float IceSlowDuration { get; set; } = 2.0f;
	[Export] public float PoisonDotDuration { get; set; } = 3.0f;
	[Export] public float LightningChainRadius { get; set; } = 150f;
	[Export] public float LightningChainDamageMultiplier { get; set; } = 0.6f;
	private int lightningChainHitCounter = 0;
	private RandomNumberGenerator combatRng = new RandomNumberGenerator();
	// Selected character (issue #29) - loaded from CharacterRoster based on Global.SelectedCharacterIdx.
	private CharacterData selectedCharacter;
	private AnimatedSprite2D? bodySprite;
	// The wizard's footprint: what the ground (water, maze walls) is measured against, as opposed
	// to the body, which enemies, decor and enemy bolts meet. "make sure the wizard's feet can't
	// hang over the water": the body collider stops at y = +24 and his boots are drawn to +44, so
	// walking south his feet hung 20 px over the lake, and walking north his body stopped his
	// boots about 40 px short of it. A child body on its own mask (LevelTilePainter.TerrainCollisionLayer)
	// and no layer, so nothing else can ever touch it. See MovePlayer.
	private CharacterBody2D? feetBody;
	private CollisionShape2D? feetShape;
	// True while hurt or death is playing, so the walk/idle choice does not cut it off.
	private bool bodyOneShotPlaying;
	// Below this speed the body idles. Above it, it walks. A small floor so the crowd-slowdown
	// crawl through a swarm still reads as walking and a stick's deadzone drift does not.
	private const float WalkAnimationSpeedFloor = 12f;
	// The staff head in the body sprite's cell (CharacterVisuals.GetStaffTipCell), and the jet that
	// pours from it. Cached so following the facing every tick costs no group or child search.
	private Vector2? staffTipCell;
	// The sheet is drawn facing left (CharacterVisuals.SpriteFacesLeft), so the flip is inverted.
	private bool spriteFacesLeft;
	private Node2D? cinderbreathEmitter;
	private ulong nextHurtAnimationMsec;
	private const ulong HurtAnimationIntervalMsec = 600;
	private int lastHorizontalFacing = 1;

	// Maximum number of spells the player can have equipped at once (issue #10). The BASE book: the
	// Blank Folio adds a page for its run, so anything asking "is the book full" reads SpellSlots.
	// What stays on this constant is what must not move with a relic the player may never find -
	// the element reachability floor (RegressionChecks) and the "carry six spells" deed.
	public const int MaxSpellSlots = 6;

	/// <summary>Pages the Blank Folio binds into the book.</summary>
	public const int BlankFolioExtraSlots = 1;

	/// <summary>How many spells this run's book holds: six, seven with the Blank Folio.</summary>
	public int SpellSlots => MaxSpellSlots
		+ (ownedChestItems.Contains(ChestItemCatalog.BlankFolio) ? BlankFolioExtraSlots : 0);

	/// <summary>Every slot holds a spell. From here on a level-up offers no new spells.</summary>
	public bool IsSpellBookFull => equippedSpells.Count >= SpellSlots;

	// The spell the wizard started with, which the Scribe's Penknife will not cut: it is the wizard,
	// and it is the spell the Apotheosis is the final form of, so cutting it would close the run's
	// last milestone for good. Set once in ApplySelectedCharacter.
	private string signatureSpellId = string.Empty;

	/// <summary>How many boons a run can hold.</summary>
	/// <remarks>
	/// Six, mirroring the spell slots, so a run is two loadouts of six rather than one loadout and
	/// a growing pile. The cap is also what keeps element tags honest: boons carry tags, and
	/// uncapped tagged boons would put every element capstone within reach at once.
	/// </remarks>
	public const int MaxBoonSlots = 6;

	private const SpellScalingTag CoreDamageScaling =
		SpellScalingTag.Damage | SpellScalingTag.Cooldown;

	private readonly HashSet<string> ownedChestItems = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> completedChestSets = new(StringComparer.OrdinalIgnoreCase);
	private float chestDamageBonusPercent = 0f;
	private float chestCritBonusChance = 0f;
	private float chestDamageReductionPercent = 0f;
	private int chestMagnetBonus = 0;
	private bool chestRetaliationEnabled = false;

	// Four relics that used to be one stat at four sizes. See .ai/passives-and-items.md section 4b.
	private float wrathSecondsRemaining = 0f;
	private float basaltCooldownRemaining = 0f;
	private float ironFangTickSeconds = 0f;

	// Sets that have paid their partial bonus. Separate from completedChestSets because a set
	// pays the partial once at two pieces and the full effect once at three, and collapsing
	// them into one guard would either skip the partial or pay it twice.
	private readonly HashSet<string> partialChestSets = new();
	private float chestMoveSpeedBonusPercent = 0f;
	private float chestAreaBonusPercent = 0f;
	// New chest stat fields for expanded items
	private float chestHealingBonusPercent = 0f;
	private float chestRegenPerSecond = 0f;
	private int chestMaxHpBonus = 0;
	private float chestCritDamageBonus = 0f;
	private float chestAttackSpeedBonusPercent = 0f;
	private float chestXpBonusPercent = 0f;
	private int chestExecuteThresholdPercent = 0;
	private float chestElementalPotencyBonus = 0f;
	private float chestIceDurationBonus = 0f;
	private float chestIceSlowBonus = 0f;
	private float chestLightningChainRadiusBonus = 0f;
	private int chestLightningChainCountBonus = 0;

	// --- Set behaviours that are not flat stat bumps ---------------------------------------
	// Three sets describe mechanics rather than numbers, and used to be implemented as flat
	// bonuses that did not match their description. These fields drive the real behaviour; the
	// player-facing wording lives in ChestItemCatalog.Sets.

	// Vaultguard: "opening a chest grants a shield and heal" - granted per chest, not once.
	[Export] public int VaultguardChestShield { get; set; } = 6;
	[Export] public int VaultguardChestHeal { get; set; } = 6;

	// Emberline: "damage and area grow with each cast" - a ramp that tops out at the same
	// ceiling the flat version used to hand over for free, so it is earned rather than given.
	[Export] public float EmberlineDamagePerCast { get; set; } = 0.006f;
	[Export] public float EmberlineAreaPerCast { get; set; } = 0.004f;
	[Export] public float EmberlineDamageCap { get; set; } = 0.18f;
	[Export] public float EmberlineAreaCap { get; set; } = 0.12f;
	private float emberlineDamageStack = 0f;
	private float emberlineAreaStack = 0f;

	// Stormbound: "crits chain and your shield gives movement speed" - the speed is conditional
	// on actually holding shield points, and crits arc to a nearby second target.
	[Export] public float StormboundArcRadius { get; set; } = 170f;
	[Export] public float StormboundArcDamageMultiplier { get; set; } = 0.5f;
	[Export] public float StormboundShieldedSpeedBonus { get; set; } = 0.10f;
	private float chestItemDropRateBonus = 0f;
	private bool chestPhylacteryActive = false;
	private int chestEssenceChaliceKills = 0;

	public override void _Ready()
	{
		AddToGroup("player");
		feetBody = GetNodeOrNull<CharacterBody2D>("Feet");
		feetShape = feetBody?.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		ConfigureEntityCollision();
		bodySprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		if (bodySprite != null)
			bodySprite.AnimationFinished += OnBodyAnimationFinished;
		_velocity = Vector2.Zero;
		ApplyArcaneUpgrades();
		spellFireTimers.Clear();
		XPToNextLevel = CalculateXPForLevel(CurrentLevel);
		InitializeSpellCatalog();
		levelUpRng.Randomize();
		combatRng.Randomize();
		ApplySelectedCharacter();

		earthMaxHpBonusLabel = new Label();
		earthMaxHpBonusLabel.Position = new Vector2(36, -56);
		ResponsiveLayout.SetFont(earthMaxHpBonusLabel, ResponsiveLayout.TextRole.Micro);
		earthMaxHpBonusLabel.Modulate = ElementColors.GetColor(Element.Earth);
		AddChild(earthMaxHpBonusLabel);
		UpdateEarthMaxHpBonusLabel();

		var hurtBox = GetNode<Area2D>("HurtBox");
		if (hurtBox != null)
		{
			hurtBox.Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
			hurtBox.Connect("body_exited", new Callable(this, nameof(OnBodyExited)));
		}
	}

	private void ConfigureEntityCollision()
	{
		SetCollisionLayerValue(1, true);
		SetCollisionMaskValue(1, true);
		// Not layer 2, the enemies: he shoves ordinary enemies aside rather than being stopped by
		// them (Enemy.ComputePlayerShove, and the crowd slowdown in MovePlayer). What he cannot
		// shove - elites, minibosses, bosses - also sits on Enemy.BlockerCollisionLayer, and that he
		// still slides off. Set here as well as in player.tscn so the two can never disagree. The
		// HurtBox is a separate Area2D and still finds every enemy on layer 2, so contact damage
		// is exactly what it was.
		SetCollisionMaskValue(2, false);
		SetCollisionMaskValue(Enemy.BlockerCollisionLayer, true);

		// The body no longer meets the ground at all - the tiles are not on layer 1 any more - so
		// without the feet the player would walk straight across a lake. Fail loudly rather than
		// let that happen quietly (a renamed node in player.tscn would do it).
		if (feetBody == null || feetShape == null)
		{
			GD.PushError("Player: no Feet/CollisionShape2D under the player; water and maze walls will not stop him.");
			return;
		}
		// No layer: an enemy, a bolt or a pickup area must only ever find the body. One mask: the
		// ground. Set here as well as in player.tscn so the two can never disagree with the painter.
		feetBody.CollisionLayer = 0;
		feetBody.CollisionMask = 0;
		feetBody.SetCollisionMaskValue(LevelTilePainter.TerrainCollisionLayer, true);
		// Floating: a top-down slide, the same in every direction. Grounded mode would treat the
		// south shore as a floor and the north one as a ceiling.
		feetBody.MotionMode = CharacterBody2D.MotionModeEnum.Floating;
	}

	/// <summary>
	/// Where the wizard is standing, in world space: the middle of his footprint. Anything that asks
	/// "what is he standing on" (a lava tile, say) should ask it here, not at GlobalPosition, which
	/// is the middle of the sprite about 36 px above his boots.
	/// </summary>
	public Vector2 FootPosition => feetShape != null ? feetShape.GlobalPosition : GlobalPosition;

	// Puts the footprint's bottom edge on the bottom of the body sprite's cell, which is where every
	// character sheet stands its boots (the cell's last row is the contact line). player.tscn
	// already has it right for the 48 px sheets at x2; this keeps it right for any other cell
	// size, such as a portrait-built idle.
	private void FitFeetToSprite()
	{
		if (bodySprite?.SpriteFrames == null || feetShape?.Shape is not CapsuleShape2D capsule)
			return;
		Texture2D frame = bodySprite.SpriteFrames.GetFrameCount(bodySprite.Animation) > 0
			? bodySprite.SpriteFrames.GetFrameTexture(bodySprite.Animation, 0)
			: null;
		if (frame == null)
			return;

		float cellBottom = bodySprite.Centered
			? bodySprite.Offset.Y + frame.GetHeight() / 2f
			: bodySprite.Offset.Y + frame.GetHeight();
		float footBottom = bodySprite.Position.Y + cellBottom * bodySprite.Scale.Y;
		// The capsule lies on its side, so its radius is the footprint's half-depth.
		feetShape.Position = new Vector2(bodySprite.Position.X, footBottom - capsule.Radius);
	}

	// Loads the character selected in CharacterSelection (issue #29), applies its HP/speed modifiers,
	// and equips its starting spell + starting passive (if any) through
	// the normal TryAddOrLevelSpell flow. Falls back to Magic Missile if no character data is found
	// (e.g. running the Player scene directly without going through character selection).
	private void ApplySelectedCharacter()
	{
		selectedCharacter = CharacterRoster.GetByIndex(Global.SelectedCharacterIdx);
		if (selectedCharacter == null)
		{
			ApplyCharacterVisual(null);
			signatureSpellId = "magic_missile";
			TryAddOrLevelSpell("magic_missile");
			return;
		}
		if (chestPhylacteryActive)
		{
			chestPhylacteryActive = false;
			CurrentHP = Math.Max(1, Mathf.RoundToInt(MaxHP * 0.25f));
			GD.Print("Phylactery restored the player.");
			return;
		}

		MaxHP = Math.Max(1, Mathf.RoundToInt(MaxHP * selectedCharacter.HealthModifier));
		CurrentHP = MaxHP;
		Speed *= selectedCharacter.SpeedModifier;

		string startingSpellId = selectedCharacter.StartingSpellResource?.Id;
		if (selectedCharacter.Id.Equals("test_wizard", StringComparison.OrdinalIgnoreCase)
			&& !string.IsNullOrWhiteSpace(Global.TestWizardStartingSpellId))
		{
			startingSpellId = Global.TestWizardStartingSpellId;
		}

		signatureSpellId = string.IsNullOrWhiteSpace(startingSpellId) ? "magic_missile" : startingSpellId;
		if (!string.IsNullOrWhiteSpace(startingSpellId))
			TryAddOrLevelSpell(startingSpellId, selectedCharacter.IsLegendaryStart);
		else
			TryAddOrLevelSpell("magic_missile");

		if (!string.IsNullOrWhiteSpace(selectedCharacter.StartingPassiveId))
		{
			if (!ApplyCharacterPassiveBonus(selectedCharacter.StartingPassiveId))
				TryAddOrLevelSpell(selectedCharacter.StartingPassiveId);
		}

		ApplyCharacterVisual(selectedCharacter);
	}

	private void ApplyCharacterVisual(CharacterData character)
	{
		if (bodySprite == null)
			return;

		// The generated wizards ship a full SpriteFrames - idle, moving, hurt, death - which nothing
		// played until now: the body ran a four-frame idle built from the portrait and only ever
		// flipped. A character with full frames uses them. One without (the Test Wizard, whose
		// sprite is Eric's own drawing and is never derived) keeps the idle-only path.
		Texture2D portrait = character?.Portrait;
		if (character?.Frames != null && character.Frames.HasAnimation("idle"))
		{
			bodySprite.SpriteFrames = character.Frames;
			bodySprite.Animation = "idle";
			bodySprite.Play("idle");
		}
		else if (CharacterVisuals.TryBuildIdleFrames(portrait, out SpriteFrames characterFrames))
		{
			bodySprite.SpriteFrames = characterFrames;
			if (characterFrames.HasAnimation("idle"))
			{
				bodySprite.Animation = "idle";
				bodySprite.Play("idle");
			}
		}
		bodyOneShotPlaying = false;

		bodySprite.Modulate = CharacterVisuals.GetCharacterTint(character?.Id);
		staffTipCell = CharacterVisuals.GetStaffTipCell(character?.Id);
		spriteFacesLeft = CharacterVisuals.SpriteFacesLeft(character?.Id);
		FitFeetToSprite();
	}

	// The starting bonuses a CharacterData can name in StartingPassiveId. An id that is in neither
	// this set nor the spell catalog falls through ApplyCharacterPassiveBonus to TryAddOrLevelSpell
	// and then quietly does nothing, so RegressionChecks validates the roster against this set
	// rather than waiting for a player to notice their character has no passive.
	public static readonly IReadOnlySet<string> CharacterPassiveBonusIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"cooldown_reduction_10",
		"spell_damage_10",
		"health_regen_0_5",
		"armor_10",
		// The two rescued wizards (.ai/side-events.md). Each is a scaling axis no starter owns.
		"duration_15",
		"area_10",
	};

	private bool ApplyCharacterPassiveBonus(string passiveId)
	{
		// Guarded by the set above so "recognised" has exactly one definition: a case added below
		// without its entry there is inert, which the roster validation then reports.
		if (!CharacterPassiveBonusIds.Contains(passiveId.Trim()))
			return false;

		switch (passiveId.Trim().ToLowerInvariant())
		{
			case "cooldown_reduction_10":
				cooldownMultiplier *= 0.90f;
				return true;
			case "spell_damage_10":
				damageMultiplier *= 1.10f;
				return true;
			case "health_regen_0_5":
				recoveryPerSecond += 0.5f;
				return true;
			case "armor_10":
				characterArmorPercent += 0.10f;
				return true;
			case "duration_15":
				durationMultiplier *= 1.15f;
				return true;
			case "area_10":
				areaMultiplier *= 1.10f;
				return true;
			default:
				return false;
		}
	}

	private static readonly Texture2D DefaultSpellIconTexture = GD.Load<Texture2D>("res://assets/bonelight/ui/spells/unknown.png");
	private static readonly Dictionary<string, string> SpellIconOverrides = new(StringComparer.OrdinalIgnoreCase)
	{
		["void_lance"] = "res://assets/bonelight/ui/spells/void-lance-darkness.png",
		["aegis_ward"] = "res://assets/bonelight/ui/spells/aegis-ward-light.png"
	};

	private void ApplyArcaneUpgrades()
	{
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (saveManager == null)
			return;

		int GetLevel(string id)
		{
			return saveManager.Data.ArcaneUpgradeLevels.TryGetValue(id, out int level) ? level : 0;
		}

		int damageLevel = GetLevel("damage");
		int recoveryLevel = GetLevel("recovery");
		int cooldownLevel = GetLevel("cooldowns");
		int areaLevel = GetLevel("area");
		int attackSpeedLevel = GetLevel("attack_speed");
		int durationLevel = GetLevel("duration");
		int amountLevel = GetLevel("amount");
		int moveSpeedLevel = GetLevel("movespeed");
		int magnetLevel = GetLevel("magnet");
		int growthLevel = GetLevel("growth");
		int extraLivesLevel = GetLevel("extra_lives");
		int rerollsLevel = GetLevel("rerolls");
		IsPlaytestModeEnabled = saveManager.Data.PlaytestModeEnabled;

		damageMultiplier = 1.0f + (damageLevel * 0.08f);
		recoveryPerSecond = recoveryLevel * 0.4f;
		cooldownMultiplier = MathF.Max(0.35f, 1.0f - (cooldownLevel * 0.05f));
		areaMultiplier = 1.0f + (areaLevel * 0.08f);
		attackSpeedMultiplier = 1.0f + (attackSpeedLevel * 0.06f);
		durationMultiplier = 1.0f + (durationLevel * 0.10f);
		amountBonus = amountLevel;
		growthMultiplier = 1.0f + (growthLevel * 0.10f);
		if (IsPlaytestModeEnabled)
			growthMultiplier *= 1.75f;
		magnetBonus = magnetLevel * 20;
		extraLives = extraLivesLevel;
		RerollsPerLevelUp = rerollsLevel + (IsPlaytestModeEnabled ? 1 : 0);

		int bansLevel = 0, banksLevel = 0, auguriesLevel = 0;
		saveManager.Data.ArcaneUpgradeLevels.TryGetValue("bans", out bansLevel);
		saveManager.Data.ArcaneUpgradeLevels.TryGetValue("banked_levels", out banksLevel);
		saveManager.Data.ArcaneUpgradeLevels.TryGetValue("auguries", out auguriesLevel);
		BansRemaining = bansLevel;
		BanksRemaining = banksLevel;
		AuguriesRemaining = auguriesLevel;

		float moveSpeedMultiplier = 1.0f + (moveSpeedLevel * 0.05f);
		Speed *= moveSpeedMultiplier;

		int vitalityLevel = 0;
		saveManager.Data.ArcaneUpgradeLevels.TryGetValue("vitality", out vitalityLevel);
		int bonusMaxHp = vitalityLevel * 5;
		if (bonusMaxHp > 0)
		{
			MaxHP += bonusMaxHp;
			CurrentHP += bonusMaxHp;
		}

		int critChanceLevel = GetLevel("crit_chance");
		baseCritChance = Math.Min(0.75f, critChanceLevel * 0.03f);
		luckLevel = GetLevel("luck");
	}

	private void InitializeSpellCatalog()
	{
		spellCatalog.Clear();

		AddSpellToCatalog(MagicMissileData ?? ResourceLoader.Load<SpellData>("res://SpellData.tres"));
		AddSpellToCatalog(ArcaneExplosionData ?? ResourceLoader.Load<SpellData>("res://SpellData_ArcaneExplosion.tres"));
		AddSpellToCatalog(SpiritualWeaponData ?? ResourceLoader.Load<SpellData>("res://SpellData_SpiritualWeapon.tres"));

		EnsureCatalogDefaults();
	}

	private void AddSpellToCatalog(SpellData spell)
	{
		if (spell == null || string.IsNullOrWhiteSpace(spell.Id))
			return;

		spellCatalog[spell.Id.Trim().ToLowerInvariant()] = spell;
	}

	private void EnsureCatalogDefaults()
	{
		if (!spellCatalog.ContainsKey("magic_missile"))
		{
			AddSpellToCatalog(CreateFallbackSpellData("magic_missile", "Magic Missile", 10, 0.5f, 1, 500f, "Fires a fast projectile at nearby enemies."));
		}

		if (!spellCatalog.ContainsKey("aegis_ward"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_AegisWard.tres"));

		if (!spellCatalog.ContainsKey("arcane_explosion"))
		{
			AddSpellToCatalog(CreateFallbackSpellData("arcane_explosion", "Arcane Explosion", 5, 1.5f, 1, 100f, "Creates a blast around the caster that damages nearby enemies."));
		}

		if (!spellCatalog.ContainsKey("spiritual_weapon"))
		{
			AddSpellToCatalog(CreateFallbackSpellData("spiritual_weapon", "Spiritual Weapon", 8, 1.0f, 2, 100f, "Summons spectral blades that strike enemies at intervals."));
		}

		// New active offensive spells (issue #13 roster expansion).
		if (!spellCatalog.ContainsKey("fireball"))
			AddSpellToCatalog(FireballData ?? ResourceLoader.Load<SpellData>("res://SpellData_Fireball.tres"));

		if (!spellCatalog.ContainsKey("mirefoot"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_Mirefoot.tres"));
		if (!spellCatalog.ContainsKey("kindled_ward"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_KindledWard.tres"));
		if (!spellCatalog.ContainsKey("gravewell"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_Gravewell.tres"));
		if (!spellCatalog.ContainsKey("hollow_star"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_HollowStar.tres"));
		if (!spellCatalog.ContainsKey("iron_palisade"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_IronPalisade.tres"));
		if (!spellCatalog.ContainsKey("contagion"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_Contagion.tres"));
		if (!spellCatalog.ContainsKey("bramble_seed"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_BrambleSeed.tres"));
		if (!spellCatalog.ContainsKey("cinderbreath"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_Cinderbreath.tres"));
		if (!spellCatalog.ContainsKey("frost_shard"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_FrostShard.tres"));
		if (!spellCatalog.ContainsKey("riptide"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_Riptide.tres"));
		if (!spellCatalog.ContainsKey("shadow_bolt"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_ShadowBolt.tres"));
		if (!spellCatalog.ContainsKey("thorn_vine"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_ThornVine.tres"));
		if (!spellCatalog.ContainsKey("gale_blade"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_GaleBlade.tres"));
		if (!spellCatalog.ContainsKey("solar_flare"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_SolarFlare.tres"));
		if (!spellCatalog.ContainsKey("molten_shard"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_MoltenShard.tres"));
		if (!spellCatalog.ContainsKey("chain_lightning"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_ChainLightning.tres"));
		if (!spellCatalog.ContainsKey("toxic_spore_burst"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_ToxicSporeBurst.tres"));
		if (!spellCatalog.ContainsKey("obsidian_spike"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_ObsidianSpike.tres"));
		if (!spellCatalog.ContainsKey("cyclone_slash"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_CycloneSlash.tres"));
		if (!spellCatalog.ContainsKey("void_lance"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_VoidLance.tres"));
		if (!spellCatalog.ContainsKey("glacial_spike"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_GlacialSpike.tres"));

		// D&D-inspired spells (issue #28).
		if (!spellCatalog.ContainsKey("black_tentacles"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_BlackTentacles.tres"));
		if (!spellCatalog.ContainsKey("cone_of_cold"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_ConeOfCold.tres"));
		if (!spellCatalog.ContainsKey("scorching_ray"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_ScorchingRay.tres"));
		if (!spellCatalog.ContainsKey("meteor_swarm"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_MeteorSwarm.tres"));
		if (!spellCatalog.ContainsKey("hunters_draw"))
			AddSpellToCatalog(ResourceLoader.Load<SpellData>("res://SpellData_HuntersDraw.tres"));

		EnsureSpellClassificationsAndScalingCoverage();
		EnsureSpellCatalogUniqueIcons();
	}

	private void EnsureSpellClassificationsAndScalingCoverage()
	{
		foreach (SpellData spell in spellCatalog.Values.Where(s => s != null))
		{
			ApplySpellClassificationDefaults(spell);
			EnsureRelevantLevelUps(spell);
			SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
		}
	}

	private static void ApplySpellClassificationDefaults(SpellData spell)
	{
		if (spell == null || string.IsNullOrWhiteSpace(spell.Id))
			return;

		string id = spell.Id.Trim().ToLowerInvariant();
		SpellScalingTag tags = CoreDamageScaling;
		SpellTargetingMode targeting = SpellTargetingMode.NearestEnemy;
		SpellDamageShape shape = SpellDamageShape.ProjectileHit;

		switch (id)
		{
			case "magic_missile":
				tags |= SpellScalingTag.ProjectileSpeed | SpellScalingTag.Pierce | SpellScalingTag.Crit;
				break;
			case "arcane_explosion":
				targeting = SpellTargetingMode.Self;
				shape = SpellDamageShape.RadiusBurst;
				tags |= SpellScalingTag.Area | SpellScalingTag.Knockback;
				break;
			case "spiritual_weapon":
				targeting = SpellTargetingMode.Self;
				shape = SpellDamageShape.ContactOrbit;
				tags |= SpellScalingTag.Range | SpellScalingTag.ProjectileCount | SpellScalingTag.ProjectileSpeed | SpellScalingTag.Duration | SpellScalingTag.Area;
				break;
			case "fireball":
				shape = SpellDamageShape.RadiusBurst;
				tags |= SpellScalingTag.Area | SpellScalingTag.Range;
				break;
			case "mirefoot":
				// No targeting mode fits: it does not pick a target, it pays out behind the player.
				// Self is the closest honest answer until the enum grows a BehindPlayer value.
				targeting = SpellTargetingMode.Self;
				shape = SpellDamageShape.PersistentZone;
				tags |= SpellScalingTag.Area | SpellScalingTag.Duration | SpellScalingTag.Slow | SpellScalingTag.Dot;
				break;
			case "kindled_ward":
				targeting = SpellTargetingMode.Self;
				shape = SpellDamageShape.ContactOrbit;
				tags |= SpellScalingTag.ProjectileCount | SpellScalingTag.Duration | SpellScalingTag.Area;
				break;
			case "gravewell":
				targeting = SpellTargetingMode.GroundAtEnemy;
				shape = SpellDamageShape.RadiusBurst;
				tags |= SpellScalingTag.Area | SpellScalingTag.Range | SpellScalingTag.Root;
				break;
			case "iron_palisade":
				// Placed between the player and the nearest pack, facing it.
				targeting = SpellTargetingMode.NearestEnemy;
				shape = SpellDamageShape.PersistentZone;
				tags |= SpellScalingTag.Area | SpellScalingTag.Duration;
				break;
			case "contagion":
				// One carrier, then the swarm infects itself.
				targeting = SpellTargetingMode.NearestEnemy;
				shape = SpellDamageShape.RadiusBurst;
				tags |= SpellScalingTag.Area | SpellScalingTag.Duration | SpellScalingTag.Dot;
				break;
			case "bramble_seed":
				// Dropped at the player's feet; it is the ground that does the work, later.
				targeting = SpellTargetingMode.Self;
				shape = SpellDamageShape.PersistentZone;
				tags |= SpellScalingTag.Area | SpellScalingTag.Duration | SpellScalingTag.Slow;
				break;
			case "hollow_star":
				// Opens on the densest pack and bursts once the pull has gathered it.
				targeting = SpellTargetingMode.GroundAtEnemy;
				shape = SpellDamageShape.RadiusBurst;
				tags |= SpellScalingTag.Area | SpellScalingTag.Range | SpellScalingTag.Duration;
				break;
			case "cinderbreath":
				// DirectionalCone and PersistentZone together, which no other spell is: it is a
				// cone that is HELD rather than cast, so it scales on area and duration and not on
				// anything to do with projectiles.
				targeting = SpellTargetingMode.DirectionalCone;
				shape = SpellDamageShape.PersistentZone;
				tags |= SpellScalingTag.Area | SpellScalingTag.Range | SpellScalingTag.Duration;
				break;
			case "frost_shard":
				// Targeting Self is the nearest honest answer: the spell picks nothing, the enemies
				// pick it by dying. There is no enum value for "wherever something just died".
				targeting = SpellTargetingMode.Self;
				shape = SpellDamageShape.RadiusBurst;
				tags |= SpellScalingTag.ProjectileSpeed | SpellScalingTag.Area | SpellScalingTag.Slow | SpellScalingTag.Root;
				break;
			case "riptide":
				// No Area tag: it pierces along a line rather than bursting, so area upgrades
				// would have nothing to grow. Pierce and Slow are what it actually scales on.
				tags |= SpellScalingTag.ProjectileSpeed | SpellScalingTag.Pierce | SpellScalingTag.Slow | SpellScalingTag.Range;
				break;
			case "shadow_bolt":
				tags |= SpellScalingTag.ProjectileSpeed | SpellScalingTag.Chain | SpellScalingTag.Dot;
				shape = SpellDamageShape.ChainJump;
				break;
			case "thorn_vine":
				// A vine planted by the pack and swept across it: Area is its reach, Pierce how many
				// it cuts through before it snags. No ProjectileSpeed - nothing travels.
				targeting = SpellTargetingMode.GroundAtEnemy;
				shape = SpellDamageShape.RadiusBurst;
				tags |= SpellScalingTag.Area | SpellScalingTag.Pierce | SpellScalingTag.Dot;
				break;
			case "gale_blade":
				tags |= SpellScalingTag.ProjectileSpeed | SpellScalingTag.Chain;
				shape = SpellDamageShape.ChainJump;
				break;
			case "molten_shard":
				tags |= SpellScalingTag.ProjectileSpeed | SpellScalingTag.Pierce;
				break;
			case "chain_lightning":
				targeting = SpellTargetingMode.MultiTarget;
				shape = SpellDamageShape.ChainJump;
				tags |= SpellScalingTag.Chain | SpellScalingTag.Crit;
				break;
			case "void_lance":
				tags |= SpellScalingTag.ProjectileSpeed | SpellScalingTag.Pierce;
				break;
			case "glacial_spike":
				targeting = SpellTargetingMode.GroundAtEnemy;
				shape = SpellDamageShape.RadiusBurst;
				tags |= SpellScalingTag.Area;
				break;
			case "solar_flare":
				targeting = SpellTargetingMode.Self;
				shape = SpellDamageShape.PersistentZone;
				tags |= SpellScalingTag.Area;
				break;
			case "toxic_spore_burst":
				targeting = SpellTargetingMode.Self;
				shape = SpellDamageShape.PersistentZone;
				tags |= SpellScalingTag.Area | SpellScalingTag.Dot;
				break;
			case "obsidian_spike":
				targeting = SpellTargetingMode.GroundAtEnemy;
				shape = SpellDamageShape.RadiusBurst;
				tags |= SpellScalingTag.Area;
				break;
			case "cyclone_slash":
				targeting = SpellTargetingMode.Self;
				shape = SpellDamageShape.ContactOrbit;
				tags |= SpellScalingTag.Range | SpellScalingTag.ProjectileCount | SpellScalingTag.ProjectileSpeed;
				break;
			case "black_tentacles":
				// The rift slows (Area, Slow); the tentacles in it hurt, and there are more of them
				// with every other level and with Amount (ProjectileCount). Its level table is written
				// out in full in the .tres, so none of these generates a line of its own.
				targeting = SpellTargetingMode.GroundAtEnemy;
				shape = SpellDamageShape.PersistentZone;
				tags |= SpellScalingTag.Area | SpellScalingTag.Slow | SpellScalingTag.ProjectileCount;
				break;
			case "cone_of_cold":
				targeting = SpellTargetingMode.DirectionalCone;
				shape = SpellDamageShape.RadiusBurst;
				tags |= SpellScalingTag.Area | SpellScalingTag.Slow;
				break;
			case "scorching_ray":
				targeting = SpellTargetingMode.MultiTarget;
				shape = SpellDamageShape.BeamHit;
				tags |= SpellScalingTag.ProjectileCount | SpellScalingTag.Crit;
				break;
			case "meteor_swarm":
				targeting = SpellTargetingMode.GroundAtEnemy;
				shape = SpellDamageShape.RadiusBurst;
				tags |= SpellScalingTag.Area | SpellScalingTag.Range;
				break;

			case "frozen_bulwark":
				targeting = SpellTargetingMode.Self;
				shape = SpellDamageShape.PersistentZone;
				tags = SpellScalingTag.Cooldown | SpellScalingTag.Root;
				break;
			case "guardian_vines":
				targeting = SpellTargetingMode.Self;
				shape = SpellDamageShape.PersistentZone;
				tags = SpellScalingTag.Cooldown | SpellScalingTag.Root;
				break;
			case "venom_cloak":
				targeting = SpellTargetingMode.Self;
				shape = SpellDamageShape.PersistentZone;
				tags = SpellScalingTag.Cooldown | SpellScalingTag.Dot;
				break;
			case "tidal_barrier":
				targeting = SpellTargetingMode.Self;
				shape = SpellDamageShape.PersistentZone;
				tags = SpellScalingTag.Cooldown | SpellScalingTag.Knockback | SpellScalingTag.Slow;
				break;
			default:
				if (spell.IsPassive)
				{
					targeting = SpellTargetingMode.Self;
					shape = SpellDamageShape.PersistentZone;
					tags = SpellScalingTag.Cooldown;
				}
				break;
		}

		if (spell.TargetingMode == SpellTargetingMode.Auto)
			spell.TargetingMode = targeting;
		if (spell.DamageShape == SpellDamageShape.Auto)
			spell.DamageShape = shape;
		if (spell.ScalingTagsMask == 0)
			spell.ScalingTagsMask = (int)tags;
	}

	private static void EnsureRelevantLevelUps(SpellData spell)
	{
		if (spell == null)
			return;

		spell.LevelUpgrades ??= new Godot.Collections.Array<SpellLevelUpgrade>();
		int maxLevel = Math.Max(2, spell.MaxLevel);

		for (int level = 2; level <= maxLevel; level++)
		{
			bool hasDamageBonus = spell.LevelUpgrades.Any(u => u != null && u.Level == level && u.DamageBonus != 0);
			if (!hasDamageBonus)
			{
				spell.LevelUpgrades.Add(new SpellLevelUpgrade
				{
					Level = level,
					DamageBonus = spell.IsPassive ? 0 : 1
				});
			}

			bool hasCooldownBonus = spell.LevelUpgrades.Any(u => u != null && u.Level == level && MathF.Abs(u.CooldownBonus) > 0.001f);
			if ((level % 2 == 0) && !hasCooldownBonus)
			{
				spell.LevelUpgrades.Add(new SpellLevelUpgrade
				{
					Level = level,
					CooldownBonus = -0.08f
				});
			}
		}

		SpellScalingTag tags = spell.GetScalingTags();
		if (tags == SpellScalingTag.None)
			return;

		if ((tags & SpellScalingTag.ProjectileCount) != 0 && !spell.LevelUpgrades.Any(u => u != null && u.ProjectileCountBonus > 0))
			spell.LevelUpgrades.Add(new SpellLevelUpgrade { Level = Math.Min(4, maxLevel), ProjectileCountBonus = 1 });

		if ((tags & SpellScalingTag.Pierce) != 0 && !spell.LevelUpgrades.Any(u => u != null && u.Effect == SpellEffect.Pierce && u.EffectValue > 0f))
			spell.LevelUpgrades.Add(new SpellLevelUpgrade { Level = Math.Min(4, maxLevel), Effect = SpellEffect.Pierce, EffectValue = 1f });

		if ((tags & SpellScalingTag.Chain) != 0 && !spell.LevelUpgrades.Any(u => u != null && u.Effect == SpellEffect.Chain && u.EffectValue > 0f))
			spell.LevelUpgrades.Add(new SpellLevelUpgrade { Level = Math.Min(5, maxLevel), Effect = SpellEffect.Chain, EffectValue = 1f });

		if ((tags & SpellScalingTag.ProjectileSpeed) != 0 && !spell.LevelUpgrades.Any(u => u != null && u.Effect == SpellEffect.ProjectileSpeed && MathF.Abs(u.EffectValue) > 0.001f))
			spell.LevelUpgrades.Add(new SpellLevelUpgrade { Level = Math.Min(3, maxLevel), Effect = SpellEffect.ProjectileSpeed, EffectValue = 45f });

		if ((tags & SpellScalingTag.Area) != 0 && !spell.LevelUpgrades.Any(u => u != null && u.Effect == SpellEffect.AreaSize && MathF.Abs(u.EffectValue) > 0.001f))
			spell.LevelUpgrades.Add(new SpellLevelUpgrade { Level = Math.Min(3, maxLevel), Effect = SpellEffect.AreaSize, EffectValue = 0.16f });

		if ((tags & SpellScalingTag.Crit) != 0 && !spell.LevelUpgrades.Any(u => u != null && u.Effect == SpellEffect.CritChance && MathF.Abs(u.EffectValue) > 0.001f))
			spell.LevelUpgrades.Add(new SpellLevelUpgrade { Level = Math.Min(5, maxLevel), Effect = SpellEffect.CritChance, EffectValue = 0.05f });

		if ((tags & SpellScalingTag.Slow) != 0)
		{
			if (!spell.LevelUpgrades.Any(u => u != null && u.Effect == SpellEffect.SlowPower && u.EffectValue > 0f))
				spell.LevelUpgrades.Add(new SpellLevelUpgrade { Level = Math.Min(4, maxLevel), Effect = SpellEffect.SlowPower, EffectValue = 0.08f });
			if (!spell.LevelUpgrades.Any(u => u != null && u.Effect == SpellEffect.SlowDuration && u.EffectValue > 0f))
				spell.LevelUpgrades.Add(new SpellLevelUpgrade { Level = Math.Min(6, maxLevel), Effect = SpellEffect.SlowDuration, EffectValue = 0.35f });
		}

		if ((tags & SpellScalingTag.Root) != 0 && !spell.LevelUpgrades.Any(u => u != null && u.Effect == SpellEffect.RootDuration && u.EffectValue > 0f))
			spell.LevelUpgrades.Add(new SpellLevelUpgrade { Level = Math.Min(4, maxLevel), Effect = SpellEffect.RootDuration, EffectValue = 0.35f });

		if ((tags & SpellScalingTag.Knockback) != 0 && !spell.LevelUpgrades.Any(u => u != null && u.Effect == SpellEffect.Knockback && u.EffectValue > 0f))
			spell.LevelUpgrades.Add(new SpellLevelUpgrade { Level = Math.Min(5, maxLevel), Effect = SpellEffect.Knockback, EffectValue = 24f });

		if ((tags & SpellScalingTag.Dot) != 0 && !spell.LevelUpgrades.Any(u => u != null && u.Effect == SpellEffect.DotDamage && u.EffectValue > 0f))
			spell.LevelUpgrades.Add(new SpellLevelUpgrade { Level = Math.Min(5, maxLevel), Effect = SpellEffect.DotDamage, EffectValue = 1f });

		if ((tags & SpellScalingTag.Duration) != 0 && !spell.LevelUpgrades.Any(u => u != null && u.Effect == SpellEffect.ZoneDuration && u.EffectValue > 0f))
			spell.LevelUpgrades.Add(new SpellLevelUpgrade { Level = Math.Min(6, maxLevel), Effect = SpellEffect.ZoneDuration, EffectValue = 0.45f });

		if ((tags & SpellScalingTag.Range) != 0 && !spell.LevelUpgrades.Any(u => u != null && MathF.Abs(u.RangeBonus) > 0.001f))
			spell.LevelUpgrades.Add(new SpellLevelUpgrade { Level = Math.Min(5, maxLevel), RangeBonus = 24f });

		spell.LevelUpgrades = new Godot.Collections.Array<SpellLevelUpgrade>(spell.LevelUpgrades
			.Where(u => u != null)
			.OrderBy(u => u.Level)
			.ThenBy(u => u.Effect)
			.ToArray());
	}

	private void EnsureSpellCatalogUniqueIcons()
	{
		var usedSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (SpellData spell in spellCatalog.Values.Where(s => s != null).OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase))
		{
			spell.Icon ??= ResolveDefaultSpellIcon(spell.Id) ?? DefaultSpellIconTexture;
			string signature = GetTextureSignature(spell.Icon);

			if (!string.IsNullOrWhiteSpace(signature) && usedSignatures.Add(signature))
				continue;

			Texture2D baseTexture = spell.Icon ?? DefaultSpellIconTexture;
			int attempt = 0;
			do
			{
				spell.Icon = CreateUniqueIconVariant(baseTexture, $"{spell.Id}:{attempt}");
				signature = GetTextureSignature(spell.Icon);
				attempt++;
			} while ((string.IsNullOrWhiteSpace(signature) || usedSignatures.Contains(signature)) && attempt < 8);

			if (!string.IsNullOrWhiteSpace(signature))
				usedSignatures.Add(signature);
		}
	}

	private static Texture2D? ResolveDefaultSpellIcon(string spellId)
	{
		if (string.IsNullOrWhiteSpace(spellId))
			return null;

		if (SpellIconOverrides.TryGetValue(spellId.Trim(), out string iconPath))
			return ResourceLoader.Load<Texture2D>(iconPath);

		return null;
	}

	private static string GetTextureSignature(Texture2D? texture)
	{
		if (texture == null)
			return string.Empty;

		if (!string.IsNullOrWhiteSpace(texture.ResourcePath))
			return texture.ResourcePath;

		return $"instance:{texture.GetInstanceId()}";
	}

	private static Texture2D CreateUniqueIconVariant(Texture2D baseTexture, string seed)
	{
		Image sourceImage = baseTexture.GetImage();
		if (sourceImage == null || sourceImage.IsEmpty())
			return baseTexture;

		float hue = (Mathf.Abs(seed.GetHashCode()) % 360) / 360f;
		Color tint = Color.FromHsv(hue, 0.68f, 1.0f, 1.0f);

		for (int y = 0; y < sourceImage.GetHeight(); y++)
		{
			for (int x = 0; x < sourceImage.GetWidth(); x++)
			{
				Color pixel = sourceImage.GetPixel(x, y);
				if (pixel.A <= 0.03f)
					continue;

				pixel.R = Mathf.Clamp((pixel.R * 0.58f) + (tint.R * 0.42f), 0f, 1f);
				pixel.G = Mathf.Clamp((pixel.G * 0.58f) + (tint.G * 0.42f), 0f, 1f);
				pixel.B = Mathf.Clamp((pixel.B * 0.58f) + (tint.B * 0.42f), 0f, 1f);
				sourceImage.SetPixel(x, y, pixel);
			}
		}

		return ImageTexture.CreateFromImage(sourceImage);
	}

	private static SpellData CreateFallbackSpellData(string id, string name, int baseDamage, float baseCooldown, int baseProjectileCount, float baseRange, string description)
	{
		var spell = new SpellData
		{
			Id = id,
			Name = name,
			CurrentLevel = 1,
			MaxLevel = 8,
			BaseDamage = baseDamage,
			BaseCooldown = baseCooldown,
			BaseProjectileCount = baseProjectileCount,
			BaseRange = baseRange,
			Description = description,
			Icon = ResolveDefaultSpellIcon(id) ?? DefaultSpellIconTexture
		};

		// Fallback element tags, matching the #13 starter roster, in case the .tres resource fails to load.
		if (id.Equals("magic_missile", StringComparison.OrdinalIgnoreCase))
		{
			spell.ElementWeights["Arcane"] = 1;
			spell.ElementWeights["Lightning"] = 1;
		}
		else if (id.Equals("arcane_explosion", StringComparison.OrdinalIgnoreCase))
		{
			spell.ElementWeights["Arcane"] = 1;
		}
		else if (id.Equals("spiritual_weapon", StringComparison.OrdinalIgnoreCase))
		{
			spell.ElementWeights["Arcane"] = 1;
			spell.ElementWeights["Light"] = 1;
		}

		return spell;
	}

	// --- Boons (see .ai/passives-and-items.md) ---

	public IReadOnlyList<string> GetOwnedBoonIds() => ownedBoonIds;

	public bool HasBoon(string id) =>
		ownedBoonIds.Any(b => b.Equals(id, StringComparison.OrdinalIgnoreCase));

	public bool CanTakeMoreBoons => ownedBoonIds.Count < MaxBoonSlots;

	/// <summary>Takes a boon permanently. Returns false if it is unknown, held, or the slots are full.</summary>
	public bool TryAddBoon(string id)
	{
		BoonDefinition definition = BoonCatalog.GetById(id);
		if (definition == null || HasBoon(definition.Id) || !CanTakeMoreBoons)
			return false;

		ownedBoonIds.Add(definition.Id);
		ApplyBoon(definition);
		EmitSignal(SignalName.LoadoutChanged);
		return true;
	}

	// Applied once, on acquisition. A boon has no levels, so there is nothing to re-apply later and
	// nothing to tick - which is the whole reason they need no node and no per-frame cost.
	private void ApplyBoon(BoonDefinition definition)
	{
		switch (definition.Effect)
		{
			case BoonEffect.Armor:
				boonArmorPercent += definition.Magnitude;
				break;
			case BoonEffect.MaxHealth:
				MaxHP += Mathf.RoundToInt(definition.Magnitude);
				CurrentHP += Mathf.RoundToInt(definition.Magnitude);
				break;
			case BoonEffect.Regen:
				recoveryPerSecond += definition.Magnitude;
				break;
			case BoonEffect.MoveSpeed:
				boonMoveSpeedMultiplier += definition.Magnitude;
				break;
			case BoonEffect.SpellDamage:
				damageMultiplier *= 1f + definition.Magnitude;
				break;
			case BoonEffect.SpellArea:
				areaMultiplier *= 1f + definition.Magnitude;
				break;
			case BoonEffect.CooldownReduction:
				cooldownMultiplier *= 1f - definition.Magnitude;
				break;
			case BoonEffect.PickupRadius:
				magnetBonus += Mathf.RoundToInt(definition.Magnitude);
				break;
			case BoonEffect.Evasion:
				boonEvasionPercent += definition.Magnitude;
				break;
			case BoonEffect.Luck:
				boonLuckBonus += Mathf.RoundToInt(definition.Magnitude);
				break;
			case BoonEffect.Retaliate:
				boonRetaliateDamage += Mathf.RoundToInt(definition.Magnitude);
				break;
			case BoonEffect.Chill:
				// The strongest slow wins rather than the slows multiplying, matching how
				// Enemy.ApplySlow already resolves overlapping sources.
				boonChillMultiplier = Mathf.Min(boonChillMultiplier, definition.Magnitude);
				break;
		}

	}

	/// <summary>Element tags contributed by held boons, keyed by element.</summary>
	public Dictionary<Element, int> GetBoonElementWeights()
	{
		var totals = new Dictionary<Element, int>();
		foreach (string id in ownedBoonIds)
		{
			BoonDefinition definition = BoonCatalog.GetById(id);
			if (definition == null)
				continue;

			foreach (var (elementName, weight) in definition.ElementWeights)
			{
				if (!Enum.TryParse<Element>(elementName, true, out Element element))
					continue;

				totals[element] = totals.TryGetValue(element, out int existing) ? existing + weight : weight;
			}
		}

		return totals;
	}

	// --- Element tier system (issue #10 / #13) ---

	public Dictionary<Element, int> GetElementInstanceCounts()
	{
		var totals = new Dictionary<Element, int>();
		foreach (var spell in equippedSpells)
		{
			if (spell == null) continue;
			foreach (var pair in spell.GetElementWeights())
			{
				totals[pair.Key] = totals.TryGetValue(pair.Key, out int existing) ? existing + pair.Value : pair.Value;
			}
		}

		// Boons carry tags too, which is most of how the thinner elements reach their thresholds
		// at all once passive spells are gone.
		foreach (var pair in GetBoonElementWeights())
		{
			totals[pair.Key] = totals.TryGetValue(pair.Key, out int existing) ? existing + pair.Value : pair.Value;
		}

		// Rare and Relic chest items carry a tag each; a completed Full Set Enchantment carries two.
		// Both are found rather than chosen, so they sit on top of the guaranteed floor that spells
		// and boons provide rather than being part of it.
		foreach (string itemId in ownedChestItems)
		{
			foreach (var (elementName, weight) in ChestItemCatalog.GetElementTags(itemId))
			{
				if (!Enum.TryParse<Element>(elementName, true, out Element element))
					continue;

				totals[element] = totals.TryGetValue(element, out int existing) ? existing + weight : weight;
			}
		}

		foreach (ChestSetDefinition set in ChestItemCatalog.Sets)
		{
			if (!completedChestSets.Contains(set.Id))
				continue;

			foreach (var (elementName, weight) in set.ElementTags)
			{
				if (!Enum.TryParse<Element>(elementName, true, out Element element))
					continue;

				totals[element] = totals.TryGetValue(element, out int existing) ? existing + weight : weight;
			}
		}

		return totals;
	}

	// --- Attunement ---
	//
	// No spell carries the same element twice in its base tags any more, so a single-element spell
	// contributes 1 instance where a hybrid contributes 2 (1 + 1). Attunement is what it gets back:
	// a spell that names exactly one element is *attuned* to it and hits harder the deeper that
	// element is stacked. Hybrids buy breadth; pure spells buy depth, and only pay off once the
	// player has committed to the element.
	//
	// Deliberately reads the spell's *current* weights, which include evolution bonuses. So an
	// evolution that adds a second tag of the same element keeps attunement and pushes the tier up,
	// while one that branches into a second element trades attunement away for the spread. That
	// trade is the whole point of the level 4 and level 8 choices.
	public const float AttunementTier2Multiplier = 1.20f;
	public const float AttunementTier4Multiplier = 1.45f;
	public const float AttunementTier6Multiplier = 1.80f;

	/// <summary>The single element a spell is attuned to, or null if it names none or several.</summary>
	public static Element? GetAttunedElement(SpellData spell)
	{
		Dictionary<Element, int> weights = spell?.GetElementWeights();
		if (weights == null || weights.Count != 1)
			return null;

		foreach (Element element in weights.Keys)
			return element;

		return null;
	}

	/// <summary>Damage multiplier a spell earns from being attuned. 1.0 for hybrids.</summary>
	public float GetAttunementMultiplier(SpellData spell)
	{
		// Checked before the tier lookup because GetElementTier builds the whole instance table,
		// and most spells are hybrids that will never earn anything here.
		Element? attuned = GetAttunedElement(spell);
		if (attuned == null)
			return 1.0f;

		return GetElementTier(attuned.Value) switch
		{
			>= 6 => AttunementTier6Multiplier,
			>= 4 => AttunementTier4Multiplier,
			>= 2 => AttunementTier2Multiplier,
			_ => 1.0f,
		};
	}

	/// <summary>
	/// The damage multiplier to hand a spell's projectile: the player's global multiplier with that
	/// spell's attunement folded in. Every cast site uses this rather than <c>damageMultiplier</c>
	/// directly, so attunement cannot be forgotten on a new spell.
	/// </summary>
	private float GetSpellDamageMultiplier(SpellData spell) => damageMultiplier * GetAttunementMultiplier(spell);

	// Returns the highest threshold (0, 2, 4, or 6) met by the given element's current instance count.
	public int GetElementTier(Element element)
	{
		var counts = GetElementInstanceCounts();
		int count = counts.TryGetValue(element, out int value) ? value : 0;
		if (count >= 6) return 6;
		if (count >= 4) return 4;
		if (count >= 2) return 2;
		return 0;
	}

	// Fills in a LevelUpOption's element preview fields (issue #15): for a brand-new spell, shows the
	// element counts added and their resulting totals; for an upgrade of an already-equipped spell,
	// shows the current standing (leveling up doesn't change its element weight contribution).
	private void ApplyElementPreview(LevelUpOption option, SpellData spellTemplate, Dictionary<Element, int> baselineCounts, bool isNewUnlock)
	{
		var weights = spellTemplate?.GetElementWeights();
		if (weights == null || weights.Count == 0)
			return;

		foreach (var pair in weights)
		{
			int baseCount = baselineCounts.TryGetValue(pair.Key, out int existing) ? existing : 0;
			int addedAmount = isNewUnlock ? pair.Value : 0;
			option.ElementContribution[pair.Key.ToString()] = addedAmount;
			option.ResultingElementCounts[pair.Key.ToString()] = baseCount + addedAmount;
			option.SpellElementTags[pair.Key.ToString()] = pair.Value;
		}
	}

	private float GetArcaneXpBonusMultiplier()
	{
		return GetElementTier(Element.Arcane) switch
		{
			6 => 1.35f,
			4 => 1.20f,
			2 => 1.10f,
			_ => 1.0f
		};
	}

	private float GetLightHealPercent()
	{
		return GetElementTier(Element.Light) switch
		{
			6 => 0.10f,
			4 => 0.06f,
			2 => 0.03f,
			_ => 0.0f
		};
	}

	// Called by damage-dealing spells after they hit an enemy, so the Light element can heal
	// the player for a percentage of damage dealt (see issue #16).
	public void NotifySpellDamageDealt(int damageDealt)
	{
		if (damageDealt <= 0)
			return;

		float healPercent = GetLightHealPercent();
		if (healPercent <= 0.0f)
			return;

		int healAmount = Mathf.RoundToInt(damageDealt * healPercent);
		if (healAmount > 0)
			Heal(healAmount);
	}

	// --- Remaining elemental tier bonuses (issue #16), self-contained (no on-hit hook needed) ---

	/// <summary>Darkness: the chance to avoid a hit outright.</summary>
	/// <remarks>
	/// Darkness used to be a second percentage damage reduction alongside Metal's flat one, which
	/// meant the game had two mitigation stats doing almost the same job and needing two
	/// explanations. Metal is now the single reduction stat (see <see cref="GetArmorPercent"/>) and
	/// Darkness moved to avoidance, which is a different question - "did it hit me" rather than
	/// "how hard" - and needs no second explanation next to armour.
	///
	/// It also rescues a mechanic that was about to be orphaned: evasion existed only through the
	/// Blur passive spell, and passive spells are being removed.
	/// </remarks>
	private float GetDarknessEvasionPercent()
	{
		return GetElementTier(Element.Darkness) switch
		{
			6 => 0.20f,
			4 => 0.12f,
			2 => 0.06f,
			_ => 0.0f
		};
	}

	/// <summary>The hard ceiling on Armour. Nothing may make the player immune.</summary>
	public const float MaxArmorPercent = 0.75f;

	private const float BasaltIgnoreCooldownSeconds = 10f;
	private const float WrathWindowSeconds = 4f;
	private const float WrathDamageBonus = 0.25f;

	/// <summary>
	/// Armour: the one stat in the game that reduces incoming damage.
	/// </summary>
	/// <remarks>
	/// There used to be three mitigation mechanisms stacked in sequence - a Darkness percentage, a
	/// chest-item percentage, and a flat subtraction from Metal, passives and the character bonus -
	/// and telling a player what the difference between them was took a paragraph. They are one
	/// number now, summed and capped, and it is called Armour.
	///
	/// Additive rather than multiplicative because additive is the version a player can do in their
	/// head: two sources of 10% is 20%, not 19%. The cap is what keeps that safe.
	/// </remarks>
	public float GetArmorPercent()
	{
		float armor = GetMetalArmorPercent() + GetChestDamageReductionPercent() + characterArmorPercent + boonArmorPercent;

		// Sundering (the Geomancer's Apotheosis): standing inside the obsidian ring.
		if (SunderingRing.Active != null && IsInstanceValid(SunderingRing.Active) && SunderingRing.Active.Contains(GlobalPosition))
			armor += SunderingRing.ArmorInside;

		// Protective Ward is worth more while a shield is up, which is the item's whole idea.
		if (shieldPoints > 0 && ownedChestItems.Contains(ChestItemCatalog.ProtectiveWard))
			armor += 0.15f;

		// Ironhide Cloak is plate that works while it is still whole: a big number that the first
		// serious wound takes away. That is a different decision from a small number that is always
		// there, which is what it used to be.
		if (MaxHP > 0 && CurrentHP >= MaxHP * 0.8f
			&& ownedChestItems.Contains(ChestItemCatalog.IronhideCloak))
		{
			armor += 0.20f;
		}

		return Mathf.Min(MaxArmorPercent, armor);
	}

	/// <summary>Metal's contribution to Armour.</summary>
	/// <remarks>
	/// This was flat damage reduction, and flat reduction cannot work in this game: an ordinary
	/// enemy deals <c>ContactDamage = 1</c>, so the very first Metal tier subtracted the entire
	/// contact damage of the basic horde and made the player simply immune to it. That is the
	/// failure mode of every flat mitigation stat in a game with small integer damage, and no
	/// amount of tuning fixes it - 1 minus 1 is 0 at any scale.
	/// </remarks>
	private float GetMetalArmorPercent()
	{
		return GetElementTier(Element.Metal) switch
		{
			6 => 0.28f,
			4 => 0.16f,
			2 => 0.08f,
			_ => 0.0f
		};
	}

	private float GetGrassRegenPerSecond()
	{
		return GetElementTier(Element.Grass) switch
		{
			6 => 4.0f,
			4 => 2.0f,
			2 => 1.0f,
			_ => 0.0f
		};
	}

	private int GetEarthMaxHpBonus()
	{
		return GetElementTier(Element.Earth) switch
		{
			6 => 100,
			4 => 50,
			2 => 20,
			_ => 0
		};
	}

	private float GetWindSpeedMultiplier()
	{
		return GetElementTier(Element.Wind) switch
		{
			6 => 1.35f,
			4 => 1.20f,
			2 => 1.10f,
			_ => 1.0f
		};
	}

	private float GetWaterCooldownMultiplier()
	{
		return GetElementTier(Element.Water) switch
		{
			6 => 0.82f,
			4 => 0.90f,
			2 => 0.95f,
			_ => 1.0f
		};
	}

	// --- Crit chance (issue #26) and Luck (issue #23) ---

	// Luck grants a small secondary bonus to crit chance on top of the player's own crit_chance stat.
	public float GetTotalCritChance() => Math.Min(0.9f, baseCritChance + (GetEffectiveLuckLevel() * 0.005f));

	// Normalized 0..1 progress toward "max Luck investment", for other systems (Legendary roll chance
	// #24, bonus drop chance #25) to scale their own formulas against, rather than reading luckLevel directly.
	public float GetLuckLevel01() => Math.Min(1.0f, GetEffectiveLuckLevel() * 0.05f);

	// Sums the shop/map Luck stat level with any bonus from held boons, so all Luck consumers see
	// one consistent effective value. Passive spells used to be the other source; boons are now.
	private int GetEffectiveLuckLevel() => luckLevel + boonLuckBonus;

	// --- Wormwood Tithe (the curse) ---
	//
	// Node2DGame owns the spawn clock and the enemy speed ramp, so the cost half of the curse
	// has to be asked for rather than applied. Both default to 1.0 for a player who does not
	// hold it, so the arena multiplies by them unconditionally and nothing branches.
	public bool HasCurse => ownedChestItems.Contains(ChestItemCatalog.WormwoodTithe);

	/// <summary>Multiplies the gap between spawns. Below 1.0 means more enemies.</summary>
	public float GetCurseSpawnIntervalMultiplier() => HasCurse ? 0.72f : 1.0f;

	/// <summary>Multiplies enemy move speed.</summary>
	public float GetCurseEnemySpeedMultiplier() => HasCurse ? 1.10f : 1.0f;

	// --- Legendary Spell Variant (issue #24) ---

	[Export] public Color LegendaryTintColor { get; set; } = new Color(1.4f, 1.1f, 0.35f, 1.0f);

	// Base 2% chance, scaling up to 10% at max Luck investment (see #24's decisions).
	public float GetLegendaryChance() => Math.Min(0.10f, 0.02f + (0.08f * GetLuckLevel01()));

	// Applies visual tints and scale transformations for evolution upgrades and Legendary rolls.
	private void ApplyLegendaryVisual(CanvasItem visual, SpellData spell)
	{
		if (visual == null || spell == null)
			return;

		Color tint = spell.GetModulateColor();
		if (spell.IsLegendary)
		{
			tint = tint != Colors.White ? tint.Lerp(LegendaryTintColor, 0.4f) : LegendaryTintColor;
		}

		if (tint != Colors.White)
			visual.Modulate = tint;

		float scaleMultiplier = spell.GetScaleMultiplier();
		if (MathF.Abs(scaleMultiplier - 1.0f) > 0.01f && visual is Node2D node2D)
		{
			node2D.Scale *= scaleMultiplier;
		}
	}

	// --- Bonus Drop Table (issue #25) ---

	// Base 3% chance per enemy kill, scaling up to 10% at max Luck investment.
	public float GetBonusDropChance() => Math.Min(0.25f, 0.03f + (0.07f * GetLuckLevel01()) + chestItemDropRateBonus);

	// Grants (or refreshes) a short temporary attack-speed/move-speed buff from a one-time-use
	// Bonus Drop Table item (BuffItem.cs). Applied additively to the live attackSpeedMultiplier/Speed
	// fields so every existing consumer benefits automatically; reverted when the timer expires.
	/// <summary>Seconds left on the speed potion's buff, 0 when none. HasteTrail fades out on it.</summary>
	public float TemporaryBuffSecondsRemaining => buffTimeRemaining;

	public void ApplyTemporaryBuff(float attackSpeedBonus, float moveSpeedBonus, float duration)
	{
		if (buffTimeRemaining > 0f)
		{
			attackSpeedMultiplier -= buffAttackSpeedBonus;
			Speed -= buffMoveSpeedPixels;
		}

		buffAttackSpeedBonus = attackSpeedBonus;
		buffMoveSpeedPixels = Speed * moveSpeedBonus;
		attackSpeedMultiplier += buffAttackSpeedBonus;
		Speed += buffMoveSpeedPixels;
		buffTimeRemaining = duration;
	}

	// --- Fire/Ice/Lightning/Poison tier bonuses (issue #16) - need the on-hit path below, unlike the 6 self-contained ones ---

	private float GetFireDamageBonusPercent()
	{
		float baseBonus = GetElementTier(Element.Fire) switch
		{
			6 => 0.35f,
			4 => 0.20f,
			2 => 0.10f,
			_ => 0.0f
		};

		return baseBonus * (1.0f + chestElementalPotencyBonus);
	}

	private float GetIceSlowPercent()
	{
		float baseSlow = GetElementTier(Element.Ice) switch
		{
			6 => 0.35f,
			4 => 0.20f,
			2 => 0.10f,
			_ => 0.0f
		};

		return Mathf.Min(0.9f, baseSlow * (1.0f + chestElementalPotencyBonus) + chestIceSlowBonus);
	}

	private (int HitsRequired, float DamageMultiplier) GetLightningChainRules()
	{
		var rules = GetElementTier(Element.Lightning) switch
		{
			6 => (2, LightningChainDamageMultiplier),
			4 => (4, LightningChainDamageMultiplier * 0.75f),
			2 => (6, LightningChainDamageMultiplier * 0.5f),
			_ => (0, 0.0f)
		};

		// Apply Thunderstone bonus to reduce hits required (faster chaining)
		int hitsRequired = Math.Max(1, rules.Item1 - chestLightningChainCountBonus);
		return (hitsRequired, rules.Item2 * (1.0f + chestElementalPotencyBonus));
	}

	private int GetPoisonTickDamage()
	{
		int baseDamage = GetElementTier(Element.Poison) switch
		{
			6 => 8,
			4 => 4,
			2 => 2,
			_ => 0
		};

		return Mathf.RoundToInt(baseDamage * (1.0f + chestElementalPotencyBonus));
	}

	// Shared on-hit damage-application path (issues #16 and #26). All active spells and reactive
	// passive spells that deal damage to an enemy should call this instead of calling
	// enemy.TakeDamage() directly, so crit rolls and the Fire/Ice/Lightning/Poison element tiers apply
	// consistently everywhere instead of being re-implemented per spell script. bonusCritChance
	// lets an individual spell add to the roll via its own SpellEffect.CritChance level-upgrades
	// (see ElementalBolt.cs / Scorching Ray, issue #28) on top of the player's global crit_chance/Luck.
	// source is the spell that dealt this, and it is only ever used to pick an impact sound.
	// Every damaging spell in the game already funnels through here, so this is the one place
	// impacts can be wired without twenty scene scripts each growing their own audio call - the
	// same argument that put the cast cue in the firing loop rather than in each spell.
	//
	// Enemy.TakeDamage would be an even smaller edit and is the wrong place: the enemy does not
	// know what hit it, so every element would sound the same, which defeats the entire point of
	// having twelve of them.
	/// <summary>
	/// Raised for every hit the player lands on an enemy: where it landed, how much, and which spell.
	/// </summary>
	/// <remarks>
	/// Read by side events that react to the fighting around them rather than to being hit - the
	/// Frozen Waste's ice thaws from the damage dealt beside it. That is what lets the ice stay out
	/// of the "enemies" group, and so out of every targeting path: a breakable must never be a
	/// target (see .ai/side-events.md). Subscribers unsubscribe in _ExitTree; it is static.
	/// </remarks>
	public static event Action<Vector2, int, SpellData> EnemyDamagedAt;

	/// <summary>The same hit, with the enemy itself. Read by the Apotheosis forms (The Lattice links
	/// what Chain Lightning strikes). Static; subscribers unsubscribe in _ExitTree.</summary>
	public static event Action<Enemy, int, SpellData> EnemyHit;

	/// <summary>Raised when one of the player's hits kills an enemy: who, where it fell, and which
	/// spell did it. Cataclysm's craters and The Long Winter's shatter both run on this.</summary>
	public static event Action<Enemy, Vector2, SpellData> EnemyKilled;

	public int DealDamageToEnemy(Node enemy, int baseDamage, float bonusCritChance = 0f, bool allowElementalChain = true, SpellData source = null)
	{
		if (source != null && enemy is Node2D impactTarget && IsInstanceValid(impactTarget))
		{
			// Throttled per element inside SfxPlayer, so an AoE landing on forty enemies makes
			// one sound rather than forty.
			SfxPlayer.Impact(SfxCatalog.DominantElement(source), impactTarget.GlobalPosition);
		}
		if (enemy == null || !IsInstanceValid(enemy) || baseDamage <= 0 || !enemy.HasMethod("TakeDamage"))
			return 0;

		bool targetWasAlive = enemy is Enemy typedEnemy && typedEnemy.Health > 0;
		int finalDamage = baseDamage;

		float chestCritChance = GetChestCritBonusChance();
		// Duellist's Chalk: an opening blow on something untouched always crits. Keyed off the
		// TARGET being unwounded rather off the caster, so it rewards spreading damage onto
		// fresh enemies instead of finishing one - the opposite instinct to Deathbringer.
		bool openingStrike = ownedChestItems.Contains(ChestItemCatalog.DuellistsChalk)
			&& enemy is Enemy unwounded && unwounded.HealthFraction >= 0.999f;
		bool isCrit = openingStrike || combatRng.Randf() < (GetTotalCritChance() + bonusCritChance + chestCritChance);
		if (isCrit)
		{
			// Per-spell crit damage rides on the SpellData that dealt the hit rather than on a new
			// argument, which is what keeps this from touching every call site in the game.
			float spellCritDamage = source?.GetEffectValueAtLevel(SpellEffect.CritDamage, source.CurrentLevel) ?? 0f;
			finalDamage = Mathf.RoundToInt(finalDamage * (CritDamageMultiplier + chestCritDamageBonus + spellCritDamage));
		}

		// Hoarfrost Nail: slowed things are vulnerable. This is the item that turns Ice from a
		// speed debuff into a damage multiplier for the whole loadout, which is a build axis the
		// roster did not previously have.
		if (enemy is Enemy chilled && chilled.IsSlowed && ownedChestItems.Contains(ChestItemCatalog.HoarfrostNail))
			finalDamage = Mathf.RoundToInt(finalDamage * 1.25f);

		// Crystal Prism: a spell that names exactly one element hits harder. Same bet as
		// attunement, bought with an item instead of earned by the spell.
		if (source?.ElementWeights != null && source.ElementWeights.Count == 1
			&& ownedChestItems.Contains(ChestItemCatalog.CrystalPrism))
		{
			finalDamage = Mathf.RoundToInt(finalDamage * 1.30f);
		}

		// The Vulnerable debuff. Applied here so EVERY source benefits - the whole point of it is
		// that Shadow Bolt makes the rest of the loadout hit harder rather than hitting hard itself.
		if (enemy is Enemy exposed && exposed.IsVulnerable)
			finalDamage = Mathf.RoundToInt(finalDamage * (1.0f + exposed.VulnerabilityBonus));

		if (enemy is Enemy targetEnemy)
		{
			if (targetEnemy.HealthFraction <= 0.5f && ownedChestItems.Contains(ChestItemCatalog.SpectralFang))
				finalDamage = Mathf.RoundToInt(finalDamage * 1.15f);
			if (targetEnemy.HealthFraction <= chestExecuteThresholdPercent / 100.0f)
				finalDamage = Math.Max(finalDamage, targetEnemy.Health);
		}

		float fireBonus = GetFireDamageBonusPercent();
		finalDamage = Mathf.RoundToInt(finalDamage * (1.0f + GetChestDamageBonusPercent()));
		if (fireBonus > 0.0f && enemy is Node2D enemyNode && IsInstanceValid(enemyNode)
			&& GlobalPosition.DistanceTo(enemyNode.GlobalPosition) <= FireProximityRange)
		{
			finalDamage = Mathf.RoundToInt(finalDamage * (1.0f + fireBonus));
		}
		finalDamage = Math.Max(1, finalDamage);

		enemy.Call("TakeDamage", finalDamage, isCrit);
		if (EnemyDamagedAt != null && enemy is Node2D damagedNode)
			EnemyDamagedAt(damagedNode.GlobalPosition, finalDamage, source);
		if (enemy is Enemy hitEnemy)
		{
			EnemyHit?.Invoke(hitEnemy, finalDamage, source);
			if (targetWasAlive && hitEnemy.Health <= 0)
				EnemyKilled?.Invoke(hitEnemy, hitEnemy.GlobalPosition, source);
		}
		if (targetWasAlive && enemy is Enemy defeatedEnemy && defeatedEnemy.Health <= 0)
			OnChestEnemyKilled();
		// Spells deliberately do not knock enemies back. This used to push every enemy on every
		// spell hit, which shoved the swarm around constantly and made positioning unreadable.
		// The player's own movement still shoves enemies aside (see MovePlayer).
		NotifySpellDamageDealt(finalDamage);
		GameStats.RecordDamageBySource(source?.Id, finalDamage);
		TryChainLightningDamage(enemy, finalDamage, allowElementalChain);
		TryStormboundCritArc(enemy, finalDamage, isCrit, allowElementalChain);

		float iceSlow = GetIceSlowPercent();
		if (iceSlow > 0.0f && enemy.HasMethod("ApplySlow"))
			enemy.Call("ApplySlow", 1.0f - iceSlow, IceSlowDuration * (1.0f + chestIceDurationBonus));

		int poisonTick = GetPoisonTickDamage();
		if (poisonTick > 0 && enemy.HasMethod("ApplyPoison"))
			enemy.Call("ApplyPoison", poisonTick, PoisonDotDuration);

		return finalDamage;
	}

	private void TryChainLightningDamage(Node sourceEnemy, int sourceDamage, bool allowElementalChain)
	{
		if (!allowElementalChain || sourceEnemy is not Node2D sourceNode)
			return;

		var (hitsRequired, damageMultiplier) = GetLightningChainRules();
		if (hitsRequired <= 0 || damageMultiplier <= 0.0f)
		{
			lightningChainHitCounter = 0;
			return;
		}

		lightningChainHitCounter = Math.Min(hitsRequired, lightningChainHitCounter + 1);
		if (lightningChainHitCounter < hitsRequired)
			return;

		var second = GetTree().GetNodesInGroup("enemies")
			.OfType<Node2D>()
			.Where(e => e != sourceNode && IsInstanceValid(e) && e.HasMethod("TakeDamage") && sourceNode.GlobalPosition.DistanceTo(e.GlobalPosition) <= LightningChainRadius * (1.0f + chestLightningChainRadiusBonus))
			.OrderBy(e => sourceNode.GlobalPosition.DistanceTo(e.GlobalPosition))
			.FirstOrDefault();
		if (second == null)
			return;

		lightningChainHitCounter = 0;
		int chainDamage = Math.Max(1, Mathf.RoundToInt(sourceDamage * damageMultiplier));
		DealDamageToEnemy(second, chainDamage, allowElementalChain: false);
		if (second.HasMethod("ApplyShock"))
			second.Call("ApplyShock", 0.18f, 6.0f, 60.0f);
	}

	// Applies/refreshes the Earth element's max HP tier bonus. Called whenever the equipped spell
	// list changes, since element instance counts (and therefore the Earth tier) can shift during a run.
	private void RefreshElementalMaxHp()
	{
		int currentBonus = GetEarthMaxHpBonus();
		int delta = currentBonus - earthMaxHpBonusApplied;
		if (delta == 0)
		{
			UpdateEarthMaxHpBonusLabel();
			return;
		}

		MaxHP += delta;
		CurrentHP = Math.Clamp(CurrentHP + delta, 0, MaxHP);
		earthMaxHpBonusApplied = currentBonus;
		UpdateEarthMaxHpBonusLabel();
	}

	private void UpdateEarthMaxHpBonusLabel()
	{
		if (earthMaxHpBonusLabel == null)
			return;

		earthMaxHpBonusLabel.Visible = earthMaxHpBonusApplied > 0;
		earthMaxHpBonusLabel.Text = earthMaxHpBonusApplied > 0 ? $"+{earthMaxHpBonusApplied} HP" : string.Empty;
	}

	// Starts (or extends) the post-menu grace period. Never shortens an active one, so closing a
	// chest menu immediately after a level-up cannot cut the first window short.
	public void GrantInvincibility(float seconds)
	{
		if (IsDead || seconds <= 0f)
			return;

		invincibilityTimeRemaining = Mathf.Max(invincibilityTimeRemaining, seconds);
		SetInvincibilityVisual(true);
	}

	public bool IsInvincible => invincibilityTimeRemaining > 0f;

	private void TickInvincibility(float delta)
	{
		if (invincibilityTimeRemaining <= 0f)
			return;

		invincibilityTimeRemaining = Mathf.Max(0f, invincibilityTimeRemaining - delta);
		if (invincibilityTimeRemaining <= 0f)
			SetInvincibilityVisual(false);
	}

	// Renders the character flat white for the duration so the invincible state is obvious.
	// Swaps the sprite's material rather than its modulate: modulate multiplies (so it cannot
	// whiten at all), and it already carries the per-character tint and the Blur dodge flash.
	private void SetInvincibilityVisual(bool active)
	{
		if (bodySprite == null || active == invincibilityVisualActive)
			return;

		if (active)
		{
			if (InvincibilityShader == null)
				return;

			invincibilityMaterial ??= new ShaderMaterial { Shader = InvincibilityShader };
			invincibilityMaterial.SetShaderParameter("whiten", 1.0f);
			materialBeforeInvincibility = bodySprite.Material;
			bodySprite.Material = invincibilityMaterial;
		}
		else
		{
			bodySprite.Material = materialBeforeInvincibility;
			materialBeforeInvincibility = null;
		}

		invincibilityVisualActive = active;
	}

	/// <summary>
	/// The staff head, in this node's local space: the cell point from CharacterVisuals, through the
	/// body sprite's own offset, scale and horizontal flip. Zero for a character without one.
	/// </summary>
	public Vector2 StaffTipLocal()
	{
		if (staffTipCell == null || bodySprite == null)
			return Vector2.Zero;
		// A centred sprite puts the cell centre (24, 24) on its origin; +0.5 lands on the pixel centre.
		Vector2 fromCentre = (staffTipCell.Value + new Vector2(0.5f, 0.5f) - new Vector2(24f, 24f)) * bodySprite.Scale;
		if (bodySprite.FlipH)
			fromCentre.X = -fromCentre.X;
		return bodySprite.Position + fromCentre;
	}

	// Walk while moving, idle while still. Never interrupts a hurt or death in progress.
	private void UpdateBodyAnimation()
	{
		if (bodySprite?.SpriteFrames == null || bodyOneShotPlaying || IsDead)
			return;
		string want = Velocity.Length() > WalkAnimationSpeedFloor && bodySprite.SpriteFrames.HasAnimation("moving")
			? "moving" : "idle";
		if (bodySprite.Animation != want && bodySprite.SpriteFrames.HasAnimation(want))
			bodySprite.Play(want);
	}

	private void PlayBodyOneShot(string animation)
	{
		if (bodySprite?.SpriteFrames == null || !bodySprite.SpriteFrames.HasAnimation(animation))
			return;
		// A hurt never cuts off a death, and a second hurt restarts the flinch rather than queueing.
		if (bodyOneShotPlaying && bodySprite.Animation == "death")
			return;
		bodyOneShotPlaying = true;
		bodySprite.Play(animation);
	}

	private void OnBodyAnimationFinished()
	{
		// Death holds its last frame; everything else hands back to the walk/idle choice.
		if (bodySprite != null && bodySprite.Animation == "death")
			return;
		bodyOneShotPlaying = false;
		UpdateBodyAnimation();
	}

	public void TakeDamage(int amount)
	{
		if (IsDead)
			return;

		// Post-menu grace period: ignore everything, including hazards and contact damage.
		if (invincibilityTimeRemaining > 0f)
			return;

		if (amount > 0)
		{
			float dodgeChance = Math.Min(0.75f, GetDarknessEvasionPercent() + boonEvasionPercent);
			if (dodgeChance > 0.0f && combatRng.Randf() < dodgeChance)
			{
				PlayBlurDodgeEffect();
				// A dodge must not sound like a hit - the whole point is that nothing landed.
				SfxPlayer.Global(SfxCatalog.PlayerDodge, 0.05f);
				return; // Blur (#27): incoming hit completely avoided - no signal, no HP loss, no reactions.
			}
		}

		// Basalt Carapace: one hit ignored per cooldown. Checked before the signal so a swallowed
		// hit is genuinely a non-event - nothing reacts to it, exactly like a dodge.
		if (amount > 0 && basaltCooldownRemaining <= 0f
			&& ownedChestItems.Contains(ChestItemCatalog.BasaltCarapace))
		{
			basaltCooldownRemaining = BasaltIgnoreCooldownSeconds;
			SfxPlayer.Global(SfxCatalog.PlayerShieldAbsorb);
			return;
		}

		// Wrath Amulet: being hit is what arms it. A flat damage bonus is the most common effect in
		// the relic pool; one that only pays out once they have reached you is a different item.
		if (amount > 0 && ownedChestItems.Contains(ChestItemCatalog.WrathAmulet))
			wrathSecondsRemaining = WrathWindowSeconds;

		if (amount > 0)
			EmitSignal(nameof(DamageTaken), amount);

		// Before mitigation on purpose: a reactive boon answers being struck, not being hurt. Put
		// it after the armour and shield steps and the better your defence the less it would fire,
		// which is backwards for effects sold as part of a defensive build.
		if (amount > 0)
			TriggerBoonReactions();

		int mitigated = Math.Max(0, amount);
		// One reduction step, not three. Applied before the shield pool so a shield absorbs what
		// actually would have landed.
		float armor = GetArmorPercent();
		if (armor > 0.0f && mitigated > 0)
		{
			// Floored at 1 rather than 0: armour reduces damage, it never deletes a hit. A hit that
			// rounds away is the flat-reduction failure arriving through the back door.
			mitigated = Math.Max(1, Mathf.RoundToInt(mitigated * (1.0f - armor)));
		}
		if (shieldPoints > 0 && mitigated > 0)
		{
			int absorbed = Math.Min(shieldPoints, mitigated);
			shieldPoints -= absorbed;
			mitigated -= absorbed;
			// Bright and metallic so it reads as "that was not HP". The break is a separate,
			// louder sound because losing the pool is what the player has to notice.
			SfxPlayer.Global(shieldPoints > 0 ? SfxCatalog.PlayerShieldAbsorb : SfxCatalog.PlayerShieldBreak);
			// Drop the ring on the same hit that empties the pool, so the player sees the shield
			// break rather than discovering it silently later.
			RefreshShieldAura();
		}
		CurrentHP = Math.Max(0, CurrentHP - mitigated);
		// Keyed off the HP that actually left, not off the DamageTaken signal, which carries the
		// PRE-mitigation amount. A hit fully eaten by armour or a shield is not a hurt sound.
		if (mitigated > 0)
		{
			SfxPlayer.Global(SfxCatalog.PlayerHurt, 0.06f);
			// Throttled: in a swarm a one-damage touch lands every 0.2 s, and a flinch on each one
			// would keep the wizard in the hurt pose for as long as anything is touching them.
			ulong now = Time.GetTicksMsec();
			if (CurrentHP > 0 && now >= nextHurtAnimationMsec)
			{
				nextHurtAnimationMsec = now + HurtAnimationIntervalMsec;
				PlayBodyOneShot("hurt");
			}
		}
		
		// Bastion of Spikes retaliation: trigger spike burst when HP drops below 30%
		if (chestRetaliationEnabled && mitigated > 0 && CurrentHP > 0)
		{
			float healthPercent = (float)CurrentHP / MaxHP;
			if (healthPercent < 0.30f)
			{
				TriggerBastionRetaliation();
			}
		}
		
		if (CurrentHP <= 0)
		{
			if (extraLives > 0)
			{
				RunEvents.RecordRevive();
				extraLives--;
				// Spending an extra life is a heal, not a death. Only the branch below is a death.
				SfxPlayer.Global(SfxCatalog.PlayerHeal);
				CurrentHP = Math.Max(1, MaxHP / 2);
				return;
			}

			IsDead = true;
			SfxPlayer.Global(SfxCatalog.PlayerDeath);
			// The run pauses the tree for the game over screen on this same frame, so the body is
			// let run through the pause or its death would freeze on the first frame.
			if (bodySprite != null)
				bodySprite.ProcessMode = ProcessModeEnum.Always;
			PlayBodyOneShot("death");
			GD.Print("Player died");
			EmitSignal(nameof(Died));
		}
	}

	// Both reactive boons in one sweep. They share a trigger, a radius and a cooldown, so running
	// them as two passes over the enemy group would cost twice the iteration for the same result.
	// Iron Fang, Wrath Amulet and Basalt Carapace, all of which are clocks rather than stats.
	/// <summary>Called by every enemy as it dies, wherever the killing damage came from.</summary>
	/// <remarks>
	/// Frost Shard's whole spell. It no longer fires at anything - it was the fourth nearest-enemy
	/// bolt in the roster and indistinguishable from the other three. Now every corpse shatters
	/// into whatever is standing next to it, which makes Ice the element where the horde kills
	/// itself: quiet while the player is struggling and deafening once the build is working, which
	/// is exactly the feedback curve this genre wants.
	/// </remarks>
	public void OnEnemyDiedAt(Vector2 where)
	{
		if (shatterInProgress)
			return;

		SpellData frost = equippedSpells.FirstOrDefault(
			s => s != null && s.Id.Equals("frost_shard", StringComparison.OrdinalIgnoreCase));
		if (frost == null)
			return;

		int shardDamage = Math.Max(1, Mathf.RoundToInt(
			frost.GetDamageAtLevel(frost.CurrentLevel) * GetSpellDamageMultiplier(frost)));
		float radius = MathF.Max(24f, frost.GetRangeAtLevel(frost.CurrentLevel) * GetEffectiveAreaMultiplier());

		// One level only. A shatter that kills re-enters this method, and on a dense screen an
		// uncapped cascade is both a frame spike and an instant clear - the flag keeps it a burst
		// rather than a chain reaction.
		shatterInProgress = true;
		try
		{
			foreach (Node node in GetTree().GetNodesInGroup("enemies"))
			{
				if (node is not Node2D enemy || !IsInstanceValid(node))
					continue;

				if (where.DistanceTo(enemy.GlobalPosition) > radius)
					continue;

				if (node.HasMethod("TakeDamage"))
					DealDamageToEnemy(node, shardDamage, source: frost);

				// The ice is the point, not the damage. Everything caught is slowed hard, so a
				// death in a crowd buys the player a moment rather than just a few hit points.
				if (node.HasMethod("ApplySlow"))
					node.Call("ApplySlow", ShatterSlowMultiplier, ShatterSlowSeconds);
			}
		}
		finally
		{
			shatterInProgress = false;
		}

		SpawnShatterRing(where, radius);
	}

	// Drawn at the radius that was actually hit, the GroundSlamAttack rule.
	private void SpawnShatterRing(Vector2 where, float radius)
	{
		Node2D arena = GetParent<Node2D>();
		if (arena == null)
			return;

		const int Segments = 20;
		var ring = new Line2D
		{
			Width = 2.0f,
			Closed = true,
			DefaultColor = new Color(0.78f, 0.95f, 1.0f, 0.85f),
			GlobalPosition = where,
			ZIndex = 6,
		};
		for (int i = 0; i < Segments; i++)
		{
			float a = Mathf.Tau * i / Segments;
			ring.AddPoint(new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius);
		}
		arena.AddChild(ring);

		Tween t = ring.CreateTween();
		t.SetParallel(true);
		t.TweenProperty(ring, "scale", new Vector2(1.18f, 1.18f), 0.20f);
		t.TweenProperty(ring, "modulate:a", 0.0f, 0.20f);
		t.SetParallel(false);
		t.TweenCallback(Callable.From(() => ring.QueueFree()));
	}

	private bool shatterInProgress = false;
	private const float ShatterSlowMultiplier = 0.45f;
	private const float ShatterSlowSeconds = 1.6f;

	private void TickDistinctRelics(float delta)
	{
		if (wrathSecondsRemaining > 0f)
			wrathSecondsRemaining = Mathf.Max(0f, wrathSecondsRemaining - delta);

		if (basaltCooldownRemaining > 0f)
			basaltCooldownRemaining = Mathf.Max(0f, basaltCooldownRemaining - delta);

		if (!ownedChestItems.Contains(ChestItemCatalog.IronFang))
			return;

		// Iron Fang bites whatever is touching the player, once a second. Driven off the contact
		// set the crowd slowdown already reads rather than a fresh radius scan, so it costs
		// nothing on a frame where nothing is touching you - which is most frames.
		ironFangTickSeconds += delta;
		if (ironFangTickSeconds < 1.0f)
			return;

		ironFangTickSeconds = 0f;
		foreach (Node toucher in overlappingEnemies)
		{
			if (!IsInstanceValid(toucher) || !toucher.IsInGroup("enemies"))
				continue;

			if (toucher.HasMethod("TakeDamage"))
				DealDamageToEnemy(toucher, IronFangContactDamage);
		}
	}

	private const int IronFangContactDamage = 6;

	private void TriggerBoonReactions()
	{
		bool retaliates = boonRetaliateDamage > 0;
		bool chills = boonChillMultiplier < 1f;
		if (!retaliates && !chills)
			return;

		if (boonReactionCooldownRemaining > 0f)
			return;

		boonReactionCooldownRemaining = BoonReactionCooldownSeconds;

		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (!IsInstanceValid(node) || node is not Node2D enemy)
				continue;

			if (GlobalPosition.DistanceTo(enemy.GlobalPosition) > BoonReactionRadius)
				continue;

			if (retaliates && node.HasMethod("TakeDamage"))
				DealDamageToEnemy(node, boonRetaliateDamage);

			if (chills && node.HasMethod("ApplySlow"))
				node.Call("ApplySlow", boonChillMultiplier, BoonChillDurationSeconds);
		}

		// Retaliation reads as damage numbers on its own; a slow does not read as anything, so the
		// ring is the only thing telling the player the boon they bought is working. Tinted toward
		// whichever effect fired, and toward the chain when both did, because the damage is the
		// part that needs to be attributed.
		SpawnBoonReactionRing(retaliates
			? new Color(0.78f, 0.86f, 0.95f)
			: new Color(0.62f, 0.88f, 1.0f));
	}

	// A ring of line segments rather than a sprite: there is no art for this, the shape is exactly
	// the radius that was hit, and it frees itself. Drawing the tell at the real radius is the same
	// rule GroundSlamAttack follows - a warning that lies about its reach is worse than none.
	private void SpawnBoonReactionRing(Color color)
	{
		Node2D parent2D = GetParent<Node2D>();
		if (parent2D == null)
			return;

		const int Segments = 28;
		var ring = new Line2D
		{
			Width = 3.0f,
			Closed = true,
			DefaultColor = color,
			GlobalPosition = GlobalPosition,
			ZIndex = 18,
		};

		for (int i = 0; i < Segments; i++)
		{
			float angle = Mathf.Tau * i / Segments;
			ring.AddPoint(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * BoonReactionRadius);
		}

		parent2D.AddChild(ring);

		Tween tween = ring.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(ring, "scale", new Vector2(1.12f, 1.12f), 0.22f);
		tween.TweenProperty(ring, "modulate:a", 0.0f, 0.22f);
		tween.SetParallel(false);
		tween.TweenCallback(Callable.From(() => ring.QueueFree()));
	}

	private void TriggerBastionRetaliation()
	{
		const float RetaliationRadius = 120f;
		const int BaseDamage = 8;

		int retaliationDamage = BaseDamage + (CurrentLevel * 3);
		var enemies = GetTree().GetNodesInGroup("enemies");

		foreach (Node enemy in enemies)
		{
			if (enemy is not CharacterBody2D enemyBody || !IsInstanceValid(enemy))
				continue;

			float distance = GlobalPosition.DistanceTo(enemyBody.GlobalPosition);
			if (distance <= RetaliationRadius && enemy.HasMethod("TakeDamage"))
			{
				DealDamageToEnemy(enemy, retaliationDamage);
			}
		}
	}

	private void PlayBlurDodgeEffect()
	{
		if (bodySprite?.SpriteFrames == null)
			return;

		Node2D? parent2D = GetParent<Node2D>();
		if (parent2D == null)
			return;

		for (int i = 0; i < 3; i++)
		{
			var ghost = new AnimatedSprite2D
			{
				SpriteFrames = bodySprite.SpriteFrames,
				Animation = bodySprite.Animation,
				Frame = bodySprite.Frame,
				FrameProgress = bodySprite.FrameProgress,
				Scale = bodySprite.Scale * (1.0f + i * 0.08f),
				GlobalPosition = bodySprite.GlobalPosition,
				Modulate = new Color(0.72f, 0.86f, 1.0f, 0.55f - i * 0.13f),
				ZIndex = 20
			};

			Vector2 drift = new Vector2(
				combatRng.RandfRange(-10f, 10f),
				combatRng.RandfRange(-8f, 6f));

			parent2D.AddChild(ghost);

			Tween tween = ghost.CreateTween();
			tween.SetParallel(true);
			tween.TweenProperty(ghost, "global_position", ghost.GlobalPosition + drift, 0.16f);
			tween.TweenProperty(ghost, "scale", ghost.Scale * 1.14f, 0.16f);
			tween.TweenProperty(ghost, "modulate:a", 0.0f, 0.16f);
			tween.SetParallel(false);
			tween.TweenCallback(Callable.From(() => ghost.QueueFree()));
		}

		bodySprite.Modulate = new Color(0.8f, 0.92f, 1.0f, 0.95f);
		Tween selfTween = bodySprite.CreateTween();
		selfTween.TweenProperty(bodySprite, "modulate", Colors.White, 0.1f);
	}

	public void Heal(int amount)
	{
		int adjustedAmount = Math.Max(0, Mathf.RoundToInt(amount * (1.0f + chestHealingBonusPercent)));
		int headroom = Math.Max(0, MaxHP - CurrentHP);
		int applied = Math.Min(headroom, adjustedAmount);
		CurrentHP += applied;

		// Vial of Vitality: whatever the health bar could not take becomes shield. Without it a
		// health pickup at full health is worth literally nothing, which is exactly when the
		// player is most likely to walk over one.
		int wasted = adjustedAmount - applied;
		if (wasted > 0 && ownedChestItems.Contains(ChestItemCatalog.VialOfVitality))
			AddOverhealShield(wasted);

	}

	// Overheal ADDS to the pool, up to OverhealShieldCap - unlike AddShield, which refreshes the pool
	// to the stronger of two values. The cap is on the pool, not on the overheal: a shield already
	// above it (a big Aegis Ward cast) is left alone rather than cut down, and overheal simply has
	// no room to add until that shield is spent below the cap.
	private void AddOverhealShield(int amount)
	{
		int room = OverhealShieldCap - shieldPoints;
		if (amount <= 0 || room <= 0)
			return;

		shieldPoints += Math.Min(amount, room);
		RefreshShieldAura();
	}

	/// <summary>Relic Key widens the chest offer. Read by Node2DGame when it rolls one.</summary>
	public bool HasRelicKey => ownedChestItems.Contains(ChestItemCatalog.RelicKey);

	public void AddChestItem(string itemId)
	{
		if (string.IsNullOrWhiteSpace(itemId))
			return;
		if (!ChestItemCatalog.AllItemIds.Contains(itemId, StringComparer.OrdinalIgnoreCase))
			return;
		if (ownedChestItems.Contains(itemId))
			return;
		// The knife is claimed BY cutting - there is no such thing as holding it unspent - so it only
		// arrives through TryCutSpellWithPenknife, which knows which spell.
		if (itemId.Equals(ChestItemCatalog.ScribesPenknife, StringComparison.OrdinalIgnoreCase))
		{
			GD.PrintErr("AddChestItem: the Scribe's Penknife is claimed by naming a spell to cut (TryCutSpellWithPenknife).");
			return;
		}

		ownedChestItems.Add(itemId);
		ApplyChestItemEffect(itemId);
		RefreshChestSetEffects();
		GD.Print($"Chest item acquired: {ChestItemCatalog.GetDisplayName(itemId)}");
		EmitSignal(SignalName.LoadoutChanged);
	}

	// --- The Scribe's Penknife ------------------------------------------------------------------
	//
	// Spells are permanent once taken: a level-up has no swap and no erase. The knife is the one way
	// out of a spell the player regrets, and it costs a chest pick and a rare relic to get.

	/// <summary>The held spells the knife may cut: all of them but the wizard's own.</summary>
	public List<SpellData> GetCuttableSpells()
	{
		return equippedSpells
			.Where(s => s != null && !s.Id.Equals(signatureSpellId, StringComparison.OrdinalIgnoreCase))
			.ToList();
	}

	/// <summary>The spell the knife will not cut. Shown greyed in its picker, so the rule is visible.</summary>
	public string SignatureSpellId => signatureSpellId;

	/// <summary>
	/// Whether a chest may offer the knife: the book is full (with a free slot the knife buys
	/// nothing a level-up would not), something in it can be cut, and it has not been used.
	/// </summary>
	public bool CanUsePenknife => !ownedChestItems.Contains(ChestItemCatalog.ScribesPenknife)
		&& IsSpellBookFull && GetCuttableSpells().Count > 0;

	/// <summary>
	/// Claims the knife by cutting <paramref name="spellId"/> out of the book. The spell is struck
	/// from the run's offers too, so a cut is final rather than a way to re-roll a spell at level 1.
	/// </summary>
	public bool TryCutSpellWithPenknife(string spellId)
	{
		if (ownedChestItems.Contains(ChestItemCatalog.ScribesPenknife))
			return false;
		SpellData target = GetCuttableSpells().FirstOrDefault(s => s.Id.Equals(spellId, StringComparison.OrdinalIgnoreCase));
		if (target == null)
			return false;
		if (!RemoveEquippedSpell(target.Id))
			return false;

		// Straight into the run's ban list rather than through TryBanSpell, which spends a Ban charge.
		bannedSpellIds.Add(target.Id);
		ownedChestItems.Add(ChestItemCatalog.ScribesPenknife);
		RefreshChestSetEffects();
		GD.Print($"Scribe's Penknife cut {target.Name} out of the spell book.");
		// RemoveEquippedSpell has already said so once; this covers the knife's own set progress.
		// LoadoutHud coalesces the two into one redraw.
		EmitSignal(SignalName.LoadoutChanged);
		return true;
	}

	private void ApplyChestItemEffect(string itemId)
	{
		switch (itemId.Trim())
		{
			// Damage items
			case ChestItemCatalog.RelicKey:
				// A key that widened your pickup radius was a magnet with the wrong name on it.
				// Read by Node2DGame when it rolls a chest - see HasRelicKey.
				break;
			case ChestItemCatalog.EmberFlask:
				chestDamageBonusPercent += 0.12f;
				break;
			case ChestItemCatalog.WrathAmulet:
				break; // Pays out only after you are hit - see wrathSecondsRemaining.
			case ChestItemCatalog.EtherealBlade:
				chestCritDamageBonus += 0.3f;
				break;
			case ChestItemCatalog.SpectralFang:
				// Its +15% below half health is read by ownership in DealDamageToEnemy. This used to
				// set chestExecuteThresholdPercent to 50 as well, under a comment calling it the +15%
				// - but that field is the Reaper's Scythe EXECUTE, so the blade alone killed anything
				// below half health outright, and completing the Scythe then lowered it to 30%. Found
				// re-verifying the set descriptions against the code (2026-10-20).
				break;
			case ChestItemCatalog.ObsidianHeart:
				chestDamageBonusPercent += 0.10f;
				Speed *= 0.95f; // -5% movement speed
				break;

			// Defense items
			case ChestItemCatalog.AegisSigil:
				chestDamageReductionPercent += 0.08f;
				break;
			case ChestItemCatalog.IronFang:
				break; // Bites back on contact - see TickIronFangContact.
			case ChestItemCatalog.BasaltCarapace:
				break; // Eats one hit on a cooldown - read in TakeDamage.
			case ChestItemCatalog.AegisCrown:
				chestMaxHpBonus += 30;
				MaxHP += 30;
				CurrentHP += 30;
				break;
			case ChestItemCatalog.IronhideCloak:
				break; // Conditional on health - read in GetArmorPercent.
			case ChestItemCatalog.ProtectiveWard:
				break; // Bonus applied passively during damage calculation

			// Healing & Recovery items
			case ChestItemCatalog.VialOfVitality:
				// Was +20% healing received, which is the same axis as Heart of Renewal and only
				// ever mattered when you were already hurt. Turning the waste into shield makes
				// it worth something at full health, which is when you actually pick health up.
				break; // Read in Heal.
			case ChestItemCatalog.HeartOfRenewal:
				// Regeneration only. Aegis Crown is the maximum-health item; this one used to be
				// that as well, which left the pair indistinguishable except by size.
				chestRegenPerSecond += 0.8f;
				break;
			case ChestItemCatalog.Phylactery:
				chestPhylacteryActive = true;
				break;
			case ChestItemCatalog.EssenceChalice:
				break; // Bonus applied on enemy death

			// Utility items
			case ChestItemCatalog.QuicksilverPendant:
				Speed *= 1.15f; // +15% movement speed
				break;
			case ChestItemCatalog.HasteRune:
				chestAttackSpeedBonusPercent += 0.12f;
				attackSpeedMultiplier += 0.12f;
				break;
			case ChestItemCatalog.CompassRose:
				chestXpBonusPercent += 0.15f;
				break;
			case ChestItemCatalog.LuckyCoin:
				chestItemDropRateBonus += 0.20f;
				break;

			// Elemental items
			case ChestItemCatalog.StormLattice:
				chestCritBonusChance += 0.08f;
				break;
			case ChestItemCatalog.InfernoCore:
				chestAreaBonusPercent += 0.12f;
				areaMultiplier *= 1.12f;
				break;
			case ChestItemCatalog.FrozenTear:
				chestIceDurationBonus += 0.40f;
				chestIceSlowBonus += 0.15f;
				break;
			case ChestItemCatalog.Thunderstone:
				chestLightningChainRadiusBonus += 0.50f;
				chestLightningChainCountBonus += 1;
				break;
			case ChestItemCatalog.CrystalPrism:
				// "+20% all elemental effect potency" was the vaguest line in the pool - it read as
				// a percentage on nothing in particular. A prism splits light into single colours,
				// so it now pays for spells that carry exactly one element, which is the same bet
				// attunement asks the player to make.
				break; // Read in DealDamageToEnemy.

			// Rule-changers. Three of the four do nothing here because they are conditions read
			// at the point of damage rather than stats applied on pickup - which is the whole
			// difference between this group and the twenty-five above.
			case ChestItemCatalog.CrackedPrism:
				// A genuine trade rather than an upgrade, and the only item in the game that makes
				// a number go DOWN. More shots that each hit softer is better for wide spells and
				// worse for single heavy ones, so it is a decision instead of a pickup.
				amountBonus += 1;
				damageMultiplier *= 0.75f;
				break;
			case ChestItemCatalog.DuellistsChalk:
				break; // Read in DealDamageToEnemy - it depends on the target, not on the player.
			case ChestItemCatalog.HoarfrostNail:
				break; // Read in DealDamageToEnemy - it depends on whether the target is slowed.
			case ChestItemCatalog.WormwoodTithe:
				// The curse pays out here; what it COSTS is read by Node2DGame, which owns the
				// spawn clock and the enemy speed ramp.
				chestXpBonusPercent += 0.30f;
				chestItemDropRateBonus += 0.25f;
				boonLuckBonus += 3;
				break;

			// Tome relics. Neither has a stat: the Folio is read by ownership in SpellSlots, and the
			// knife does its whole job in TryCutSpellWithPenknife and never comes through here.
			case ChestItemCatalog.BlankFolio:
				break;
		}
	}

	// A taste of the set, at two of its three pieces. Deliberately about a third of the full
	// effect: enough that the player notices the set is doing something, not enough that the third
	// piece stops mattering.
	private void ApplyPartialSetEffect(string setId)
	{
		switch (setId)
		{
			case ChestItemCatalog.VaultguardSetId:
				chestDamageReductionPercent += 0.05f;
				break;
			case ChestItemCatalog.EmberlineSetId:
				chestDamageBonusPercent += 0.06f;
				break;
			case ChestItemCatalog.StormboundSetId:
				chestCritBonusChance += 0.04f;
				break;
			case ChestItemCatalog.BastionOfSpikesSetId:
				chestDamageReductionPercent += 0.06f;
				break;
			case ChestItemCatalog.EternalGuardianSetId:
				chestMaxHpBonus += 30;
				MaxHP += 30;
				CurrentHP += 30;
				break;
			case ChestItemCatalog.ElementalMasterySetId:
				chestElementalPotencyBonus += 0.15f;
				break;
			// Three sets grew a third piece when they became objects (issue #63), and with it a
			// two-piece taste - same rule as the rest: paid once, never refunded on completion.
			case ChestItemCatalog.DeathbringerSetId:
				chestDamageBonusPercent += 0.08f;
				break;
			case ChestItemCatalog.LifeDrainSetId:
				chestMaxHpBonus += 20;
				MaxHP += 20;
				CurrentHP += 20;
				break;
			case ChestItemCatalog.FortunesFavorSetId:
				chestXpBonusPercent += 0.10f;
				break;
		}

	}

	private void RefreshChestSetEffects()
	{
		foreach (ChestSetDefinition set in ChestItemCatalog.Sets)
		{
			// The partial is paid first and is never refunded when the set completes - the full
			// effect is written as a bonus ON TOP of it, so a completed set is worth the partial
			// plus the full amount. Subtracting it back out would mean the third piece could feel
			// like a downgrade on any stat the two tiers share.
			if (set.PartialItemCount > 0 && !partialChestSets.Contains(set.Id))
			{
				int held = set.RequiredItemIds.Count(id => ownedChestItems.Contains(id));
				if (held >= set.PartialItemCount && partialChestSets.Add(set.Id))
					ApplyPartialSetEffect(set.Id);
			}

			bool complete = set.RequiredItemIds.All(id => ownedChestItems.Contains(id));
			if (complete && completedChestSets.Add(set.Id))
			{
				RunEvents.RecordChestSetCompleted(set.Id);
				switch (set.Id)
				{
					// Original sets
					case ChestItemCatalog.VaultguardSetId:
						chestDamageReductionPercent += 0.12f;
						// The shield and heal are granted per chest opened (OnChestOpened), not
						// once here - that is what "opening a chest grants a shield and heal"
						// means, and the one-off AddShield(4) never matched it.
						break;
					case ChestItemCatalog.EmberlineSetId:
						// Damage and area now ramp per cast in TickEmberlineCast rather than
						// arriving as a flat bonus the moment the set completes.
						break;
					case ChestItemCatalog.StormboundSetId:
						chestCritBonusChance += 0.1f;
						// Move speed is applied only while shielded (GetStormboundSpeedMultiplier)
						// and crits arc to a second target (TryStormboundCritArc), instead of a
						// permanent Speed multiplier that ignored both halves of the description.
						break;
					case ChestItemCatalog.BastionOfSpikesSetId:
						chestDamageReductionPercent += 0.15f;
						chestRetaliationEnabled = true;
						break;

					// New sets
					case ChestItemCatalog.DeathbringerSetId:
						chestDamageBonusPercent += 0.25f;
						chestExecuteThresholdPercent = 30; // Execute at 30% HP
						break;
					case ChestItemCatalog.EternalGuardianSetId:
						chestMaxHpBonus += 80;
						chestDamageReductionPercent += 0.18f;
						MaxHP += 80;
						break;
					case ChestItemCatalog.LifeDrainSetId:
						break; // Handled dynamically in OnEnemyKilled
					case ChestItemCatalog.ElementalMasterySetId:
						chestElementalPotencyBonus += 0.40f;
						chestIceDurationBonus += 0.40f;
						chestIceSlowBonus += 0.20f;
						chestLightningChainRadiusBonus += 0.50f;
						chestLightningChainCountBonus += 1;
						break;
					case ChestItemCatalog.SpeedDemonSetId:
						chestMoveSpeedBonusPercent += 0.20f;
						chestAttackSpeedBonusPercent += 0.15f;
						Speed *= 1.20f;
						attackSpeedMultiplier += 0.15f;
						break;
					case ChestItemCatalog.FortunesFavorSetId:
						chestItemDropRateBonus += 0.25f;
						chestXpBonusPercent += 0.25f;
						break;
				}
			}
		}
	}

	// Vaultguard's payoff: called once per chest actually consumed, so the shield and heal recur
	// for the rest of the run instead of being a single grant when the set completed.
	public void OnChestOpened()
	{
		if (!completedChestSets.Contains(ChestItemCatalog.VaultguardSetId))
			return;

		AddShield(VaultguardChestShield);
		Heal(VaultguardChestHeal);
	}

	// Emberline's ramp: one step per spell cast, clamped so it converges on a known ceiling
	// rather than growing without bound over a long run.
	private void TickEmberlineCast()
	{
		if (!completedChestSets.Contains(ChestItemCatalog.EmberlineSetId))
			return;

		emberlineDamageStack = MathF.Min(EmberlineDamageCap, emberlineDamageStack + EmberlineDamagePerCast);
		emberlineAreaStack = MathF.Min(EmberlineAreaCap, emberlineAreaStack + EmberlineAreaPerCast);
	}

	// Area actually delivered to spells. Emberline's contribution is kept as a separate factor
	// rather than folded into areaMultiplier, because that field is recomputed from the meta
	// upgrade levels and would silently discard anything multiplied into it.
	private float GetEffectiveAreaMultiplier() => areaMultiplier * (1f + emberlineAreaStack);

	// Stormbound's move speed, live-checked against the shield pool so it comes and goes with it.
	private float GetStormboundSpeedMultiplier()
	{
		if (shieldPoints <= 0 || !completedChestSets.Contains(ChestItemCatalog.StormboundSetId))
			return 1f;

		return 1f + StormboundShieldedSpeedBonus;
	}

	// Stormbound's other half: a critical hit arcs to the nearest other enemy for a fraction of
	// the damage. Gated on allowElementalChain so an arc cannot arc again.
	private void TryStormboundCritArc(Node sourceEnemy, int damage, bool isCrit, bool allowElementalChain)
	{
		if (!isCrit || !allowElementalChain || sourceEnemy is not Node2D sourceNode)
			return;
		if (!completedChestSets.Contains(ChestItemCatalog.StormboundSetId))
			return;

		var target = GetTree().GetNodesInGroup("enemies")
			.OfType<Node2D>()
			.Where(e => e != sourceNode && IsInstanceValid(e) && e.HasMethod("TakeDamage")
				&& sourceNode.GlobalPosition.DistanceTo(e.GlobalPosition) <= StormboundArcRadius)
			.OrderBy(e => sourceNode.GlobalPosition.DistanceTo(e.GlobalPosition))
			.FirstOrDefault();
		if (target == null)
			return;

		int arcDamage = Math.Max(1, Mathf.RoundToInt(damage * StormboundArcDamageMultiplier));
		DealDamageToEnemy(target, arcDamage, allowElementalChain: false);
		if (target.HasMethod("ApplyShock"))
			target.Call("ApplyShock", 0.16f, 5.0f, 55.0f);
	}

	// Wrath is folded in here rather than at a call site so every source of spell damage sees it.
	public float GetWrathDamageBonus() => wrathSecondsRemaining > 0f ? WrathDamageBonus : 0f;

	public float GetChestDamageBonusPercent() => chestDamageBonusPercent + emberlineDamageStack;
	public float GetChestDamageReductionPercent() => chestDamageReductionPercent;
	public float GetChestCritBonusChance() => chestCritBonusChance;
	public float GetChestAreaBonusPercent() => chestAreaBonusPercent;
	public float GetChestMoveSpeedBonusPercent() => chestMoveSpeedBonusPercent;
	public bool HasChestRetaliation() => chestRetaliationEnabled;
	// New chest item getters
	public float GetChestHealingBonusPercent() => chestHealingBonusPercent;
	public float GetChestRegenPerSecond() => chestRegenPerSecond;
	public int GetChestMaxHpBonus() => chestMaxHpBonus;
	public float GetChestCritDamageBonus() => chestCritDamageBonus;
	public float GetChestAttackSpeedBonusPercent() => chestAttackSpeedBonusPercent;
	public float GetChestXpBonusPercent() => chestXpBonusPercent;
	public int GetChestExecuteThresholdPercent() => chestExecuteThresholdPercent;
	public float GetChestElementalPotencyBonus() => chestElementalPotencyBonus;
	public float GetChestIceDurationBonus() => chestIceDurationBonus;
	public float GetChestIceSlowBonus() => chestIceSlowBonus;
	public float GetChestLightningChainRadiusBonus() => chestLightningChainRadiusBonus;
	public int GetChestLightningChainCountBonus() => chestLightningChainCountBonus;
	public float GetChestItemDropRateBonus() => chestItemDropRateBonus;
	public bool IsChestPhylacteryActive() => chestPhylacteryActive;
	public int GetChestEssenceChaliceKills() => chestEssenceChaliceKills;

	private void OnChestEnemyKilled()
	{
		if (ownedChestItems.Contains(ChestItemCatalog.EssenceChalice) && chestEssenceChaliceKills < 50)
		{
			chestEssenceChaliceKills++;
			Heal(1);
		}

		if (completedChestSets.Contains(ChestItemCatalog.LifeDrainSetId))
			Heal(1);
	}

	// Grants (or refreshes to the stronger value of) an absorbing shield pool (Aegis Ward, issue #13/#22).
	// The "stronger value" is deliberate for its two callers, Aegis Ward's cast and the Reliquary's
	// per-chest grant: each is a shield that RECHARGES, and stacking recharges would grow without
	// bound. Overheal is the one source that should add, and has its own path (AddOverhealShield).
	public void AddShield(int amount)
	{
		shieldPoints = Math.Max(shieldPoints, Math.Max(0, amount));
		RefreshShieldAura();
	}

	// The shield pool had no visual at all: Aegis Ward, Protective Ward and Vaultguard's per-chest
	// grant all changed how much damage the player could eat with nothing on screen to show it.
	// The ring is the one piece of feedback that makes those effects legible.
	private void RefreshShieldAura()
	{
		if (shieldAura == null || !IsInstanceValid(shieldAura))
		{
			shieldAura = GetNodeOrNull<AnimatedSprite2D>("ShieldAura");
			if (shieldAura == null)
				return;
		}

		bool active = shieldPoints > 0;
		if (active == shieldAura.Visible)
			return;

		shieldAura.Visible = active;
		if (active)
			shieldAura.Play("active");
		else
			shieldAura.Stop();
	}

	// The XP curve: level l costs XpFirstLevelCost + XpCostPerLevel * (l - 1). Linear, on purpose.
	// XP arrives at a roughly steady rate across a run, so a cost that rises steadily turns it into
	// level-ups spread across the run. The old curve (5 + 5(l-1) + 2(l-1)^2) was nearly free at the
	// start: half of a 30-minute run's level-ups landed in its first five minutes, and with them a
	// wizard's whole path to the Apotheosis (.ai/gameplay-design.md, "The Apotheosis").
	[Export] public int XpFirstLevelCost { get; set; } = 10;
	//
	// 27 since the run became 15 minutes (2026-10-16; 55 on the 30-minute run). Strategy B of
	// .ai/run-pacing.md: the Ascension and Apotheosis gates stay at 17 and 23 and each level is
	// cheaper instead, so a run still ends near level 30 - the same number of picks, the same build
	// meeting a boss whose health did not change, and each gate at the same place in the build. The
	// model predicts the Ascension at 4.8 min (fastest 4.3) and the form at 9.1 (fastest 8.2),
	// inside the 15-minute targets; not yet measured.
	[Export] public int XpCostPerLevel { get; set; } = 27;

	public int CalculateXPForLevel(int level)
	{
		int l = Math.Max(1, level);
		return Math.Max(1, XpFirstLevelCost + XpCostPerLevel * (l - 1));
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsDead)
			return;

		TickInvincibility((float)delta);

		if (boonReactionCooldownRemaining > 0f)
			boonReactionCooldownRemaining = Mathf.Max(0f, boonReactionCooldownRemaining - (float)delta);

		TickDistinctRelics((float)delta);

		// Prune before anything reads the contact set, and do not rely on body_exited to do it.
		// An enemy that dies while standing on the player leaves the "enemies" group and zeroes
		// its collision layer inside a single frame (Enemy.StartDeath), so the HurtBox may never
		// report an exit for it at all - and by the time queue_free finally did fire one,
		// OnBodyExited's group check rejected it. The corpse then stayed in overlappingEnemies for
		// the rest of the run, ticking contact damage every DamageCooldownSeconds forever and
		// permanently taxing move speed through the crowd slowdown in MovePlayer.
		PruneDepartedEnemies();

		MovePlayer(delta);
		UpdateBodyAnimation();

		// enemy damage cooldown logic
		// Update cooldown timers

		// Damage-over-time for overlapping enemies
		foreach (var enemy in overlappingEnemies)
		{
			if (!enemyDamageCooldowns.ContainsKey(enemy))
				enemyDamageCooldowns[enemy] = DamageCooldownSeconds; // So damage applies on next tick

			enemyDamageCooldowns[enemy] += (float)delta;
			if (enemyDamageCooldowns[enemy] >= DamageCooldownSeconds)
			{
				// A weakened enemy can roll 0 (Enemy.ScaleOutgoingDamage, issue #66). It still swings;
				// the touch just lands like a dodge, with no on-hurt reactions.
				int contactDamage = GetEnemyContactDamage(enemy);
				if (enemy is Enemy toucher)
					contactDamage = toucher.ScaleOutgoingDamage(contactDamage);
				if (contactDamage > 0)
					TakeDamage(contactDamage);
				PlayEnemyAttackAnimation(enemy);
				enemyDamageCooldowns[enemy] = 0f;
			}
		}

		float effectiveRegenPerSecond = BaseHealthRegenPerSecond + recoveryPerSecond + GetGrassRegenPerSecond() + GetPassiveSpellRegenPerSecond() + chestRegenPerSecond;
		if (effectiveRegenPerSecond > 0.0f && CurrentHP > 0 && CurrentHP < MaxHP)
		{
			recoveryAccumulator += effectiveRegenPerSecond * (float)delta;
			while (recoveryAccumulator >= 1.0f)
			{
				Heal(1);
				recoveryAccumulator -= 1.0f;
			}
		}

		if (buffTimeRemaining > 0f)
		{
			buffTimeRemaining -= (float)delta;
			if (buffTimeRemaining <= 0f)
			{
				buffTimeRemaining = 0f;
				attackSpeedMultiplier -= buffAttackSpeedBonus;
				Speed -= buffMoveSpeedPixels;
				buffAttackSpeedBonus = 0f;
				buffMoveSpeedPixels = 0f;
			}
		}

		// Spell firing logic
		if (MagicMissileScene == null || ArcaneExplosionScene == null || SpiritualWeaponScene == null)
		{
			GD.PrintErr("Error: MagicMissileScene or ArcaneExplosionScene or SpiritualWeaponScene is not assigned in the Player script.");
			return;
		}

		foreach (var spell in equippedSpells)
		{
			if (spell == null || string.IsNullOrWhiteSpace(spell.Id))
				continue;

			if (!spellFireTimers.ContainsKey(spell.Id))
				spellFireTimers[spell.Id] = 0f;

			float interval = (spell.GetCooldownAtLevel(spell.CurrentLevel) * cooldownMultiplier * GetWaterCooldownMultiplier()) / attackSpeedMultiplier;
			interval = MathF.Max(0.05f, interval);
			spellFireTimers[spell.Id] += (float)delta;
			if (spellFireTimers[spell.Id] >= interval)
			{
				// What the cast actually did decides the cue, the cooldown and the cast counters -
				// see CastOutcome. A spell with no branch below is Maintained: that is Frost Shard,
				// whose whole spell is OnEnemyDiedAt and which casts nothing on its cooldown.
				CastOutcome outcome = CastOutcome.Maintained;
				if (spell.Id.Equals("magic_missile", StringComparison.OrdinalIgnoreCase))
				{
					// Ask the resource for the count rather than hardcoding 1: evolutions and
					// level upgrades can both grant projectiles, and the old literal meant any
					// that did were silently ignored for this spell alone.
					int projectileCount = Math.Max(1, spell.GetProjectileCountAtLevel(spell.CurrentLevel) + amountBonus);
					float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
					EnemyTargeting.CollectNearest(GetTree(), GlobalPosition, projectileCount, magicMissileTargets, castRange);
					outcome = CastOutcome.NoTarget;

					for (int p = 0; p < projectileCount && magicMissileTargets.Count > 0; p++)
					{
						// One missile per enemy, nearest first. These home, so fanning them by
						// angle would only bend them back onto the same body - spreading the
						// volley across targets is what actually makes extra missiles count.
						// Wraps when the crowd is thinner than the volley.
						Node2D shotTarget = magicMissileTargets[p % magicMissileTargets.Count];
						if (!IsInstanceValid(shotTarget))
							continue;

						var missile = MagicMissileScene.Instantiate<Area2D>();
						missile.Position = GlobalPosition;
						var script = missile as MagicMissile;
						if (script != null)
						{
							script.SpellData = spell;
							script.DamageMultiplier = GetSpellDamageMultiplier(spell);
							script.AreaMultiplier = GetEffectiveAreaMultiplier();
							script.DurationMultiplier = durationMultiplier;
							script.SetSpellLevel(spell.CurrentLevel);
							script.PlayerRef = this;
						}
						GetParent().AddChild(missile);
						ApplyLegendaryVisual(missile, spell);
						var shootMethod = missile.GetType().GetMethod("Shoot");
						if (shootMethod != null)
						{
							shootMethod.Invoke(missile, new object[] { GlobalPosition, shotTarget.GlobalPosition, shotTarget });
						}
						outcome = CastOutcome.Fired;
					}
				}
				else if (spell.Id.Equals("aegis_ward", StringComparison.OrdinalIgnoreCase))
				{
					// The whole spell. Its cooldown is the recharge, AddShield refreshes the pool to
					// the stronger value, and Player.TakeDamage already spends and breaks it - so a
					// shield that absorbs, breaks, and comes back needs no node and no new state.
					// Only a recharge that restored something is heard; topping up a full pool is
					// Maintained, so the recharge clock still runs exactly as it did.
					int shieldBefore = shieldPoints;
					AddShield(spell.GetDamageAtLevel(spell.CurrentLevel));
					outcome = shieldPoints > shieldBefore ? CastOutcome.Fired : CastOutcome.Maintained;
				}
				else if (spell.Id.Equals("arcane_explosion", StringComparison.OrdinalIgnoreCase))
				{
					GD.Print("Firing Arcane Explosion");

					// Ensure only one ArcaneExplosion follows this player. If not present, create and attach to player.
					bool hasExplosion = GetChildren().OfType<Node>().Any(n => n is ArcaneExplosion);
					if (!hasExplosion)
					{
						var explosion = ArcaneExplosionScene.Instantiate<Area2D>();
						// Attach to player so it follows automatically; set local position to origin
						explosion.Position = Vector2.Zero;
						var script = explosion as ArcaneExplosion;
						if (script != null)
						{
							script.SpellData = spell;
							script.DamageMultiplier = GetSpellDamageMultiplier(spell);
							script.AreaMultiplier = GetEffectiveAreaMultiplier();
							script.CooldownMultiplier = cooldownMultiplier / attackSpeedMultiplier;
							script.DurationMultiplier = durationMultiplier;
							script.SetSpellLevel(spell.CurrentLevel);
							// Make the explosion follow the player and persist (TotalLifetime = 0 means infinite)
							script.PlayerRef = this;
							script.TotalLifetime = 0f;
						}
						AddChild(explosion);
						ApplyLegendaryVisual(explosion, spell);
						// It detonates once in its own _Ready, so the first cast is a real one.
						outcome = CastOutcome.Fired;
					}
					else
					{
						// Charge-driven: after the first, its own cooldown only keeps the stats
						// current, and it goes off from NotifyArcaneCharge, not from here.
						var existing = GetChildren().OfType<ArcaneExplosion>().FirstOrDefault();
						if (existing != null)
						{
							existing.SpellData = spell;
							existing.DamageMultiplier = GetSpellDamageMultiplier(spell);
							existing.AreaMultiplier = GetEffectiveAreaMultiplier();
							existing.CooldownMultiplier = cooldownMultiplier / attackSpeedMultiplier;
							existing.DurationMultiplier = durationMultiplier;
							existing.SetSpellLevel(spell.CurrentLevel);
						}
					}

				}
				else if (spell.Id.Equals("spiritual_weapon", StringComparison.OrdinalIgnoreCase))
				{
					GD.Print("Firing Spiritual Weapon");
					var spiritualWeapon = SpiritualWeaponScene.Instantiate<Node2D>();
					// Attach to player so it follows automatically; set local position to origin.
					spiritualWeapon.Position = Vector2.Zero;
					var script = spiritualWeapon as SpiritualWeapon;
					if (script != null)
					{
						script.SpellData = spell;
						script.DamageMultiplier = GetSpellDamageMultiplier(spell);
						script.AreaMultiplier = GetEffectiveAreaMultiplier();
						script.AttackSpeedMultiplier = attackSpeedMultiplier;
						script.DurationMultiplier = durationMultiplier;
						script.ProjectileCountBonus = amountBonus;
						script.SetSpellLevel(spell.CurrentLevel);
						script.PlayerRef = this;
					}
					AddChild(spiritualWeapon);
					ApplyLegendaryVisual(spiritualWeapon, spell);
					// A fresh blade circles the caster every cooldown whether or not anything is near.
					outcome = CastOutcome.Fired;
				}
				else if (spell.Id.Equals("fireball", StringComparison.OrdinalIgnoreCase) && FireballScene != null)
				{
					outcome = CastOutcome.NoTarget;
					var enemies = GetTree().GetNodesInGroup("enemies");
					if (enemies.Count > 0)
					{
						Node2D nearest = null;
						float minDist = float.MaxValue;
						foreach (var e in enemies)
						{
							if (e is Node2D n2d)
							{
								float dist = GlobalPosition.DistanceTo(n2d.GlobalPosition);
								if (dist < minDist)
								{
									minDist = dist;
									nearest = n2d;
								}
							}
						}
						if (nearest != null)
						{
							float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
							if (minDist <= castRange)
							{
								var fireball = FireballScene.Instantiate<Area2D>();
								fireball.Position = GlobalPosition;
								var script2 = fireball as Fireball;
								if (script2 != null)
								{
									script2.SpellData = spell;
									script2.DamageMultiplier = GetSpellDamageMultiplier(spell);
									script2.AreaMultiplier = areaMultiplier;
									script2.DurationMultiplier = durationMultiplier;
									script2.SetSpellLevel(spell.CurrentLevel);
									script2.PlayerRef = this;
								}
								GetParent().AddChild(fireball);
								ApplyLegendaryVisual(fireball, spell);
								script2?.Shoot(GlobalPosition, nearest.GlobalPosition);
								outcome = CastOutcome.Fired;
							}
						}
					}
				}
				else if (spell.Id.Equals("mirefoot", StringComparison.OrdinalIgnoreCase)) outcome = FireOrRefreshTrailWeaver(spell, MirefootScene);
				else if (spell.Id.Equals("kindled_ward", StringComparison.OrdinalIgnoreCase)) outcome = FireKindledWard(spell, KindledWardScene);
				else if (spell.Id.Equals("gravewell", StringComparison.OrdinalIgnoreCase)) outcome = FireGravewellTrap(spell, GravewellScene);
				else if (spell.Id.Equals("hollow_star", StringComparison.OrdinalIgnoreCase)) outcome = FireHollowStar(spell, HollowStarScene);
				else if (spell.Id.Equals("iron_palisade", StringComparison.OrdinalIgnoreCase)) outcome = FireIronPalisade(spell, IronPalisadeScene);
				else if (spell.Id.Equals("contagion", StringComparison.OrdinalIgnoreCase)) outcome = FireContagion(spell, ContagionScene);
				else if (spell.Id.Equals("bramble_seed", StringComparison.OrdinalIgnoreCase)) outcome = FireBrambleSeed(spell, BrambleSeedScene);
				else if (spell.Id.Equals("cinderbreath", StringComparison.OrdinalIgnoreCase)) outcome = FireOrRefreshFlamethrower(spell, CinderbreathScene);
				else if (spell.Id.Equals("riptide", StringComparison.OrdinalIgnoreCase)) outcome = FireBoltSpell(spell, RiptideScene);
				else if (spell.Id.Equals("shadow_bolt", StringComparison.OrdinalIgnoreCase)) outcome = FireBoltSpell(spell, ShadowBoltScene);
				else if (spell.Id.Equals("thorn_vine", StringComparison.OrdinalIgnoreCase)) outcome = FireThornVine(spell, ThornVineScene);
				else if (spell.Id.Equals("gale_blade", StringComparison.OrdinalIgnoreCase)) outcome = FireBoltSpell(spell, GaleBladeScene);
				else if (spell.Id.Equals("molten_shard", StringComparison.OrdinalIgnoreCase)) outcome = FireBoltSpell(spell, MoltenShardScene, BoltAim.RandomInRange);
				else if (spell.Id.Equals("chain_lightning", StringComparison.OrdinalIgnoreCase)) outcome = FireChainLightningSpell(spell, ChainLightningScene);
				else if (spell.Id.Equals("void_lance", StringComparison.OrdinalIgnoreCase)) outcome = FireVoidLanceSpell(spell, VoidLanceScene);
				else if (spell.Id.Equals("glacial_spike", StringComparison.OrdinalIgnoreCase)) outcome = FireIceSpikes(spell, GlacialSpikeScene);
				else if (spell.Id.Equals("solar_flare", StringComparison.OrdinalIgnoreCase)) outcome = FireOrRefreshElementalPulse(spell, SolarFlareScene);
				else if (spell.Id.Equals("toxic_spore_burst", StringComparison.OrdinalIgnoreCase)) outcome = FireOrRefreshElementalPulse(spell, ToxicSporeBurstScene);
				else if (spell.Id.Equals("obsidian_spike", StringComparison.OrdinalIgnoreCase)) outcome = FireGroundSpike(spell, ObsidianSpikeScene);
				else if (spell.Id.Equals("cyclone_slash", StringComparison.OrdinalIgnoreCase)) outcome = FireOrRefreshOrbitingBlade(spell, CycloneSlashScene);
				else if (spell.Id.Equals("black_tentacles", StringComparison.OrdinalIgnoreCase)) outcome = FireBlackTentacles(spell, BlackTentaclesScene);
				else if (spell.Id.Equals("cone_of_cold", StringComparison.OrdinalIgnoreCase)) outcome = FireConeBlast(spell, ConeOfColdScene);
				else if (spell.Id.Equals("scorching_ray", StringComparison.OrdinalIgnoreCase)) outcome = FireScorchingRay(spell, ScorchingRayScene);
				else if (spell.Id.Equals("meteor_swarm", StringComparison.OrdinalIgnoreCase)) outcome = FireMeteorSwarm(spell, MeteorImpactScene);
				else if (spell.Id.Equals("hunters_draw", StringComparison.OrdinalIgnoreCase))
				{
					// Movement decides when this looses, not the cooldown. Reaching the interval
					// only means an arrow is nocked; TickHuntersDraw returns false while the shot
					// is still being drawn, and NoTarget leaves the timer above the interval so
					// the draw keeps running next frame instead of restarting the cooldown.
					outcome = TickHuntersDraw(spell, (float)delta) ? CastOutcome.Fired : CastOutcome.NoTarget;
				}

				// Nothing in reach: the spell stays ready rather than spending its cooldown on empty
				// air, so it goes off the frame something walks into range instead of up to a whole
				// interval later. Silent, and not a cast - Emberline would otherwise ramp and Arcane
				// Explosion charge every frame the timer sits ripe.
				if (outcome == CastOutcome.NoTarget)
					continue;

				TickEmberlineCast();
				// Every spell in the game fires through this one point, so the cast cue is wired
				// here rather than in twenty scene scripts. The element decides which of the twelve
				// voices plays; SfxPlayer throttles per element so a fast build does not stutter.
				// Only for a cast that put something into the world: it used to play every cooldown
				// regardless, so the Geomancer thumped on an empty field (and a held cone, an orbit
				// or Frost Shard ticked a cue that matched nothing on screen).
				if (outcome == CastOutcome.Fired)
					SfxPlayer.Cast(SfxCatalog.DominantElement(spell), GlobalPosition);
				spellFireTimers[spell.Id] = 0f;
				NotifyArcaneCharge(spell);
			}
		}
	}

	private float GetPassiveSpellRegenPerSecond()
	{
		float regenPerSecond = 0.0f;
		foreach (SpellData spell in equippedSpells)
		{
			if (spell == null || string.IsNullOrWhiteSpace(spell.Id))
				continue;

			string spellId = spell.Id.Trim().ToLowerInvariant();
			int level = Math.Max(1, spell.CurrentLevel);
			// Nothing here any more. This granted passive regeneration for three defensive passive
			// spells; two were deleted with the rest of that roster, and the third is Aegis Ward,
			// which is a shield now and should do exactly one thing.
			_ = spellId;
			_ = level;
		}

		return regenPerSecond;
	}

	// --- Shared firing helpers for the issue #13 roster expansion spells (ElementalBolt/ElementalPulse/
	// GroundSpike/OrbitingBlade) so each new spell only needs a SpellData .tres + scene, not a new
	// branch of bespoke firing logic. ---

	// Runs every physics frame once the cooldown has nocked an arrow. Returns true only on the
	// frame the volley is actually loosed, which is the signal the firing loop uses to reset the
	// cooldown - every other frame it returns false and the loop leaves the timer ripe so this
	// state machine keeps running.
	//
	// MovePlayer has already run this frame, so comparing GlobalPosition against the previous
	// frame's measures real displacement: being walled in or slowed counts as standing still,
	// which is the honest reading of "the player is not moving".
	private bool TickHuntersDraw(SpellData spell, float delta)
	{
		if (HuntersArrowScene == null)
			return false;

		if (!huntersDrawTracking)
		{
			// First frame after nocking: establish a baseline instead of charging off whatever
			// distance was covered during the cooldown.
			huntersDrawTracking = true;
			huntersDrawLastPosition = GlobalPosition;
			huntersDrawCharge = 0f;
			huntersDrawStillSeconds = 0f;
			huntersDrawHoldSeconds = 0f;
		}

		float moved = GlobalPosition.DistanceTo(huntersDrawLastPosition);
		huntersDrawLastPosition = GlobalPosition;

		// A pixel of drift per frame is the physics solver settling, not the player running.
		bool moving = moved > 0.5f;
		float fullDraw = MathF.Max(1f, HuntersDrawFullDrawDistance);

		if (moving)
		{
			huntersDrawStillSeconds = 0f;
			huntersDrawCharge = MathF.Min(1f, huntersDrawCharge + moved / fullDraw);
		}
		else
		{
			huntersDrawStillSeconds += delta;
		}

		if (huntersDrawCharge >= 1f)
			huntersDrawHoldSeconds += delta;

		UpdateHuntersDrawBow(spell);

		// Arms give out at full draw: the shot goes whether or not the player ever stops.
		if (huntersDrawCharge >= 1f && huntersDrawHoldSeconds >= HuntersDrawHoldSeconds)
			return LooseHuntersVolley(spell, 1f);

		if (!moving && huntersDrawStillSeconds >= HuntersDrawReleaseGrace)
		{
			if (huntersDrawCharge >= HuntersDrawMinLooseCharge)
				return LooseHuntersVolley(spell, huntersDrawCharge);

			// Too shallow to be worth an arrow - let the bow relax back down.
			huntersDrawCharge = MathF.Max(0f, huntersDrawCharge - HuntersDrawRelaxPerSecond * delta);
			UpdateHuntersDrawBow(spell);
		}

		return false;
	}

	private void UpdateHuntersDrawBow(SpellData spell)
	{
		if (huntersDrawBow == null || !IsInstanceValid(huntersDrawBow))
		{
			huntersDrawBow = new BowDrawVisual { Name = "BowDrawVisual" };
			AddChild(huntersDrawBow);
		}

		Node2D aimTarget = FindHuntersDrawTarget(spell);
		Vector2 aim = aimTarget != null
			? (aimTarget.GlobalPosition - GlobalPosition)
			: new Vector2(lastHorizontalFacing >= 0 ? 1f : -1f, 0f);
		huntersDrawBow.UpdateDraw(huntersDrawCharge, aim, huntersDrawHoldSeconds);
	}

	// Nearest enemy inside the spell's range, or null. Range grows with level like every other
	// projectile spell, so a low-level bow simply cannot reach across the arena.
	private Node2D FindHuntersDrawTarget(SpellData spell)
	{
		float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
		Node2D nearest = null;
		float minDist = float.MaxValue;
		foreach (var e in GetTree().GetNodesInGroup("enemies"))
		{
			if (e is not Node2D n2d || !IsInstanceValid(n2d))
				continue;
			float dist = GlobalPosition.DistanceTo(n2d.GlobalPosition);
			if (dist < minDist && dist <= castRange)
			{
				minDist = dist;
				nearest = n2d;
			}
		}
		return nearest;
	}

	// Fires the volley. Returns true if arrows actually left the bow, which is what lets the
	// caller reset the cooldown - a release with nothing in range holds the draw instead of
	// wasting it, so the player keeps their charge until a target appears.
	private bool LooseHuntersVolley(SpellData spell, float charge)
	{
		Node2D target = FindHuntersDrawTarget(spell);
		if (target == null)
		{
			// Nothing to shoot. Keep the charge but stop the fatigue clock, otherwise the bow
			// would loose into empty air the moment the hold expired.
			huntersDrawHoldSeconds = 0f;
			return false;
		}

		charge = Mathf.Clamp(charge, 0f, 1f);
		Vector2 origin = GlobalPosition;
		Vector2 aim = (target.GlobalPosition - origin).Normalized();
		if (aim == Vector2.Zero)
			aim = Vector2.Right;

		int arrows = Math.Max(1, spell.GetProjectileCountAtLevel(spell.CurrentLevel) + amountBonus);
		float chargeDamage = Mathf.Lerp(HuntersDrawMinDamageMultiplier, HuntersDrawMaxDamageMultiplier, charge);
		float chargeSpeed = Mathf.Lerp(HuntersDrawMinSpeedMultiplier, 1f, charge);
		int chargePierce = charge >= 0.999f ? HuntersDrawFullDrawPierceBonus : 0;

		// One arrow flies straight down the aim line; extras fan evenly to either side of it.
		float spread = Mathf.DegToRad(HuntersDrawSpreadDegrees);
		float step = arrows > 1 ? spread / (arrows - 1) : 0f;
		float start = arrows > 1 ? -spread * 0.5f : 0f;

		for (int i = 0; i < arrows; i++)
		{
			var arrow = HuntersArrowScene.Instantiate<Area2D>();
			arrow.Position = origin;
			if (arrow is ElementalBolt bolt)
			{
				bolt.SpellData = spell;
				bolt.DamageMultiplier = GetSpellDamageMultiplier(spell) * chargeDamage;
				bolt.AreaMultiplier = GetEffectiveAreaMultiplier();
				bolt.DurationMultiplier = durationMultiplier;
				bolt.BaseSpeed *= chargeSpeed;
				bolt.BasePierce += chargePierce;
				bolt.SetSpellLevel(spell.CurrentLevel);
				bolt.PlayerRef = this;
			}
			GetParent().AddChild(arrow);
			ApplyLegendaryVisual(arrow, spell);

			Vector2 direction = aim.Rotated(start + step * i);
			// Aim at a point down the fan line rather than at the enemy: these arrows do not home,
			// so the spread has to be baked into where they are told to go.
			(arrow as ElementalBolt)?.Shoot(origin, origin + direction * 400f, null);
		}

		huntersDrawCharge = 0f;
		huntersDrawHoldSeconds = 0f;
		huntersDrawStillSeconds = 0f;
		huntersDrawTracking = false;
		huntersDrawBow?.Clear();
		return true;
	}

	// What one pass of the firing loop actually did with a spell whose cooldown came due. The cue,
	// the cooldown reset and the cast counters (Emberline, Arcane Explosion's charge) all key off it,
	// so every Fire* helper dispatched from that loop returns one rather than nothing.
	private enum CastOutcome
	{
		// Something new went into the world - a projectile, a zone, a strike, a restored shield.
		// The only outcome that plays the cast cue.
		Fired,
		// The spell did its cooldown's work without anything new appearing: a held or orbiting
		// spell kept current, Kindled Ward at its cap, Aegis Ward topping up a full pool, Frost
		// Shard. Spends the cooldown and still counts as a cast, exactly as before the cue was
		// gated - those spells have no other way to feed Emberline or Arcane Explosion, and their
		// balance was tuned with it - but is silent, because there is nothing to hear.
		Maintained,
		// An aimed spell with nothing in range (or a scene that failed to load). Nothing happened,
		// so nothing is spent or counted and the timer stays ripe.
		NoTarget,
	}

	// What decides a bolt's target (issue #64, .ai/spell-roster.md). Five spells shared one
	// nearest-enemy function, so they all flew at the same skeleton; the aim is now part of what
	// makes each one a different spell.
	private enum BoltAim
	{
		Nearest,
		// The enemy with the most others packed round it - a piercing bolt fired into the thick of
		// a pack does its whole job, one fired at the straggler in front does a fraction of it.
		DensestCluster,
		// Any enemy in range. Spreads the hits across the field instead of focusing the front rank.
		RandomInRange,
	}

	private CastOutcome FireBoltSpell(SpellData spell, PackedScene scene, BoltAim aim = BoltAim.Nearest)
	{
		if (scene == null) return CastOutcome.NoTarget;
		FindNearestEnemy(out var nearest, out var minDist);
		if (nearest == null) return CastOutcome.NoTarget;

		float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
		if (minDist > castRange) return CastOutcome.NoTarget;

		if (aim == BoltAim.DensestCluster)
			nearest = FindDensestEnemy(castRange) ?? nearest;
		else if (aim == BoltAim.RandomInRange)
			nearest = FindRandomEnemy(castRange) ?? nearest;

		var bolt = scene.Instantiate<Area2D>();
		bolt.Position = GlobalPosition;
		if (bolt is ChainLightning chainLightning)
		{
			chainLightning.SpellData = spell;
			chainLightning.DamageMultiplier = GetSpellDamageMultiplier(spell);
			chainLightning.AreaMultiplier = areaMultiplier;
			chainLightning.DurationMultiplier = durationMultiplier;
			chainLightning.ProjectileCountBonus = amountBonus;
			chainLightning.SetSpellLevel(spell.CurrentLevel);
			chainLightning.PlayerRef = this;
		}
		if (bolt is ElementalBolt eb)
		{
			eb.SpellData = spell;
			eb.DamageMultiplier = GetSpellDamageMultiplier(spell);
			eb.AreaMultiplier = areaMultiplier;
			eb.DurationMultiplier = durationMultiplier;
			eb.SetSpellLevel(spell.CurrentLevel);
			eb.PlayerRef = this;
		}
		GetParent().AddChild(bolt);
		ApplyLegendaryVisual(bolt, spell);
		// Riptide draws each of its forms itself (RiptideWave). An evolution's tint is a stand-in for
		// spells that do not - and Titan Cataclysm's is fire orange, which turned the sea to mud.
		// A legendary roll keeps its gold.
		if (!spell.IsLegendary && spell.Id.Equals("riptide", StringComparison.OrdinalIgnoreCase))
			bolt.Modulate = Colors.White;
		if (bolt is ChainLightning chain)
			chain.CastFromPlayer(this, nearest);
		else
			(bolt as ElementalBolt)?.Shoot(GlobalPosition, nearest.GlobalPosition, nearest);

		// Spring Tide (Tituba's Apotheosis): the wave floods the ground it travels over.
		if (spell.IsApotheosis && spell.Id.Equals("riptide", StringComparison.OrdinalIgnoreCase))
		{
			Vector2 heading = (nearest.GlobalPosition - GlobalPosition).Normalized();
			var lane = new SpringTideLane
			{
				PlayerRef = this,
				SpellData = spell,
				Heading = heading == Vector2.Zero ? Vector2.Right : heading,
				Length = castRange * 0.9f,
				HalfWidth = 40f * GetEffectiveAreaMultiplier(),
				// The water fills behind the crest at the crest's own pace, not ahead of it.
				FloodSpeed = (bolt as ElementalBolt)?.Speed ?? 0f,
			};
			GetParent().AddChild(lane);
			lane.GlobalPosition = GlobalPosition;
		}
		return CastOutcome.Fired;
	}

	private CastOutcome FireChainLightningSpell(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;
		FindNearestEnemy(out var nearest, out var minDist);
		if (nearest == null) return CastOutcome.NoTarget;

		float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
		if (minDist > castRange) return CastOutcome.NoTarget;

		var chain = scene.Instantiate<Area2D>();
		chain.Position = Vector2.Zero;
		if (chain is ChainLightning chainLightning)
		{
			chainLightning.SpellData = spell;
			chainLightning.DamageMultiplier = GetSpellDamageMultiplier(spell);
			chainLightning.AreaMultiplier = areaMultiplier;
			chainLightning.DurationMultiplier = durationMultiplier;
			chainLightning.ProjectileCountBonus = amountBonus;
			chainLightning.SetSpellLevel(spell.CurrentLevel);
			chainLightning.PlayerRef = this;
		}

		GetParent().AddChild(chain);
		ApplyLegendaryVisual(chain, spell);
		(chain as ChainLightning)?.CastFromPlayer(this, nearest);
		return CastOutcome.Fired;
	}

	private CastOutcome FireVoidLanceSpell(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;
		FindNearestEnemy(out var nearest, out var minDist);
		if (nearest == null) return CastOutcome.NoTarget;

		float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
		if (minDist > castRange) return CastOutcome.NoTarget;

		var lance = scene.Instantiate<Area2D>();
		lance.Position = Vector2.Zero;
		if (lance is VoidLance voidLance)
		{
			voidLance.SpellData = spell;
			voidLance.DamageMultiplier = GetSpellDamageMultiplier(spell);
			voidLance.AreaMultiplier = areaMultiplier;
			voidLance.DurationMultiplier = durationMultiplier;
			voidLance.SetSpellLevel(spell.CurrentLevel);
			voidLance.PlayerRef = this;
		}

		GetParent().AddChild(lance);
		ApplyLegendaryVisual(lance, spell);
		(lance as VoidLance)?.CastFromPlayer(this, nearest.GlobalPosition);
		return CastOutcome.Fired;
	}

	// Cinderbreath. Same shape as FireOrRefreshElementalPulse - one persistent child that is kept
	// current rather than a fresh cast each cooldown - because the spell owns its own burn/recharge
	// rhythm and the fire loop cannot express a duty cycle.
	//
	// It is parented to the Player rather than to the arena, so the cone travels with the caster.
	// That is the whole feel of it: you point it by moving, and it never lags behind you.
	// Mirefoot. One persistent emitter kept current, exactly like the flamethrower - the spell has
	// no casts, so the fire loop is only here to keep its stats fresh as the player levels.
	/// <summary>Every cast feeds Arcane Explosion, which has no clock of its own.</summary>
	/// <remarks>
	/// The spell used to pulse around the player on a 1.5s timer and was indistinguishable in feel
	/// from Solar Flare and Toxic Spore Burst - three self-centred zones going off on three
	/// clocks. Now it has no clock: every cast of any OTHER spell adds a charge, and it detonates
	/// when the charge comes due.
	///
	/// That makes one spell in the game care what the other five are, which is the closest thing
	/// the loadout has to a build. Six fast spells set it off constantly; a loadout of slow heavy
	/// ones makes it rare, and the player feels the difference without being told.
	///
	/// Fed from the single point every cast in the game funnels through, so it cannot miss a
	/// spell or double-count one.
	/// </remarks>
	private void NotifyArcaneCharge(SpellData caster)
	{
		if (caster == null || caster.Id.Equals("arcane_explosion", StringComparison.OrdinalIgnoreCase))
			return; // It must not charge itself, or it becomes a timer again with extra steps.

		SpellData arcane = equippedSpells.FirstOrDefault(
			s => s != null && s.Id.Equals("arcane_explosion", StringComparison.OrdinalIgnoreCase));
		if (arcane == null)
			return;

		arcaneCharge++;
		if (arcaneCharge < GetArcaneChargeRequired(arcane))
			return;

		arcaneCharge = 0;
		ArcaneExplosion live = GetChildren().OfType<ArcaneExplosion>().FirstOrDefault();
		live?.TriggerNow();
	}

	// Levelling the spell makes it come due sooner rather than hit harder-per-second, which keeps
	// its identity: it is the spell that answers how busy the rest of your loadout is.
	private static int GetArcaneChargeRequired(SpellData arcane)
	{
		int level = Math.Max(1, arcane.CurrentLevel);
		return Math.Max(ArcaneChargeFloor, ArcaneChargeBase - (level - 1) / 2);
	}

	private int arcaneCharge = 0;
	private const int ArcaneChargeBase = 8;
	private const int ArcaneChargeFloor = 3;

	private CastOutcome FireOrRefreshTrailWeaver(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;

		var existing = GetChildren().OfType<TrailWeaver>()
			.FirstOrDefault(w => w.SpellData != null && w.SpellData.Id.Equals(spell.Id, StringComparison.OrdinalIgnoreCase));

		if (existing == null)
		{
			var weaver = scene.Instantiate<Node2D>();
			weaver.Position = Vector2.Zero;
			if (weaver is TrailWeaver script)
			{
				script.SpellData = spell;
				script.PlayerRef = this;
				script.DamageMultiplier = GetSpellDamageMultiplier(spell);
				script.AreaMultiplier = GetEffectiveAreaMultiplier();
				script.DurationMultiplier = durationMultiplier;
				script.CooldownMultiplier = cooldownMultiplier / attackSpeedMultiplier;
				script.SetSpellLevel(spell.CurrentLevel);
			}
			AddChild(weaver);
			return CastOutcome.Fired;
		}
		else
		{
			existing.DamageMultiplier = GetSpellDamageMultiplier(spell);
			existing.AreaMultiplier = GetEffectiveAreaMultiplier();
			existing.DurationMultiplier = durationMultiplier;
			existing.CooldownMultiplier = cooldownMultiplier / attackSpeedMultiplier;
			existing.SetSpellLevel(spell.CurrentLevel);
		}
		// Kept current, nothing new cast: see CastOutcome.Maintained.
		return CastOutcome.Maintained;
	}

	// Kindled Ward. The cooldown makes ONE ward; the projectile count is a ceiling on how many may
	// burn at once. So a fresh cast is skipped entirely while the cap is full, and the spell quietly
	// tops itself back up as old wards gutter out.
	private CastOutcome FireKindledWard(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;

		Node2D arena = GetParent<Node2D>();
		if (arena == null) return CastOutcome.NoTarget;

		int cap = Math.Max(1, spell.GetProjectileCountAtLevel(spell.CurrentLevel) + amountBonus);
		int alive = arena.GetChildren().OfType<KindledWard>()
			.Count(w => IsInstanceValid(w) && w.SpellData != null
				&& w.SpellData.Id.Equals(spell.Id, StringComparison.OrdinalIgnoreCase));

		// Maintained rather than NoTarget: the cap is full, not the field empty, and leaving the
		// timer ripe here would re-count every ward in the arena every frame the cap stays full.
		if (alive >= cap)
			return CastOutcome.Maintained;

		var ward = scene.Instantiate<Node2D>();
		if (ward is KindledWard script)
		{
			script.SpellData = spell;
			script.PlayerRef = this;
			script.Damage = Math.Max(1, Mathf.RoundToInt(
				spell.GetDamageAtLevel(spell.CurrentLevel) * GetSpellDamageMultiplier(spell)));
			script.StrikeRadius *= GetEffectiveAreaMultiplier();
			script.Lifetime *= durationMultiplier;
		}

		arena.AddChild(ward);
		ward.GlobalPosition = GlobalPosition;
		ApplyLegendaryVisual(ward, spell);
		return CastOutcome.Fired;
	}

	// Gravewell. Placed at a random point in a ring around the player rather than on an enemy -
	// the spell is area denial, and something aimed at an enemy is not denying an area, it is
	// attacking. The inner 45% of the ring is excluded so a trap never lands under the caster.
	private CastOutcome FireGravewellTrap(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;

		Node2D arena = GetParent<Node2D>();
		if (arena == null) return CastOutcome.NoTarget;

		float range = MathF.Max(60f, spell.GetRangeAtLevel(spell.CurrentLevel));
		float angle = combatRng.Randf() * Mathf.Tau;
		float distance = Mathf.Lerp(range * 0.45f, range, combatRng.Randf());
		Vector2 where = GlobalPosition + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;

		var trap = scene.Instantiate<Node2D>();
		if (trap is GravewellTrap script)
		{
			script.SpellData = spell;
			script.PlayerRef = this;
			script.Damage = Math.Max(1, Mathf.RoundToInt(
				spell.GetDamageAtLevel(spell.CurrentLevel) * GetSpellDamageMultiplier(spell)));
			script.BlastRadius *= GetEffectiveAreaMultiplier();
		}

		arena.AddChild(trap);
		trap.GlobalPosition = where;
		ApplyLegendaryVisual(trap, spell);
		return CastOutcome.Fired;
	}

	// Hollow Star (issue #64). Opened on the densest pack in range - a pull centred on one straggler
	// gathers one straggler - and only when something is in range at all, like every aimed spell.
	private CastOutcome FireHollowStar(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;
		FindNearestEnemy(out var nearest, out var minDist);
		if (nearest == null) return CastOutcome.NoTarget;
		float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
		if (minDist > castRange) return CastOutcome.NoTarget;
		Node2D centre = FindDensestEnemy(castRange) ?? nearest;

		Node2D arena = GetParent<Node2D>();
		if (arena == null) return CastOutcome.NoTarget;

		var well = scene.Instantiate<Node2D>();
		if (well is HollowStar script)
		{
			float area = GetEffectiveAreaMultiplier();
			float reachBonus = spell.GetEffectValueAtLevel(SpellEffect.AreaSize, spell.CurrentLevel);
			script.SpellData = spell;
			script.PlayerRef = this;
			script.Damage = Math.Max(1, Mathf.RoundToInt(
				spell.GetDamageAtLevel(spell.CurrentLevel) * GetSpellDamageMultiplier(spell)));
			// Reach and collapse grow together, so a wider pull still closes on what it gathered.
			float reachScale = (script.PullRadius + reachBonus) / MathF.Max(1f, script.PullRadius);
			script.PullRadius = (script.PullRadius + reachBonus) * area;
			script.CollapseRadius *= reachScale * area;
			script.PullSeconds = (script.PullSeconds
				+ spell.GetEffectValueAtLevel(SpellEffect.ZoneDuration, spell.CurrentLevel)) * durationMultiplier;
		}

		arena.AddChild(well);
		well.GlobalPosition = centre.GlobalPosition;
		ApplyLegendaryVisual(well, spell);
		return CastOutcome.Fired;
	}

	private int ScaledSpellDamage(SpellData spell) =>
		Math.Max(1, Mathf.RoundToInt(spell.GetDamageAtLevel(spell.CurrentLevel) * GetSpellDamageMultiplier(spell)));

	// Iron Palisade (.ai/side-events.md). Driven up across the line to the nearest enemy, a little
	// short of it, so it stands between the player and the pack rather than on top of either.
	private CastOutcome FireIronPalisade(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;
		FindNearestEnemy(out var nearest, out var minDist);
		if (nearest == null) return CastOutcome.NoTarget;
		if (minDist > spell.GetRangeAtLevel(spell.CurrentLevel)) return CastOutcome.NoTarget;
		Node2D arena = GetParent<Node2D>();
		if (arena == null) return CastOutcome.NoTarget;

		Vector2 facing = (nearest.GlobalPosition - GlobalPosition).Normalized();
		if (facing == Vector2.Zero) facing = Vector2.Right;
		var wall = scene.Instantiate<Node2D>();
		if (wall is IronPalisade script)
		{
			script.SpellData = spell;
			script.PlayerRef = this;
			script.Damage = ScaledSpellDamage(spell);
			script.Facing = facing;
			script.HalfLength = (script.HalfLength + spell.GetEffectValueAtLevel(SpellEffect.AreaSize, spell.CurrentLevel)) * GetEffectiveAreaMultiplier();
			script.LifeSeconds = (script.LifeSeconds + spell.GetEffectValueAtLevel(SpellEffect.ZoneDuration, spell.CurrentLevel)) * durationMultiplier;
		}
		arena.AddChild(wall);
		wall.GlobalPosition = GlobalPosition + facing * Mathf.Clamp(minDist * 0.6f, 60f, 140f);
		ApplyLegendaryVisual(wall, spell);
		return CastOutcome.Fired;
	}

	// Contagion (.ai/side-events.md). One random carrier in range; the swarm does the rest.
	private CastOutcome FireContagion(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;
		FindNearestEnemy(out var nearest, out var minDist);
		if (nearest == null) return CastOutcome.NoTarget;
		float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
		if (minDist > castRange) return CastOutcome.NoTarget;
		if ((FindRandomEnemy(castRange) ?? nearest) is not Enemy carrier) return CastOutcome.NoTarget;
		Node2D arena = GetParent<Node2D>();
		if (arena == null) return CastOutcome.NoTarget;

		var plague = scene.Instantiate<Node2D>();
		if (plague is Contagion script)
		{
			script.SpellData = spell;
			script.PlayerRef = this;
			script.Damage = ScaledSpellDamage(spell);
			script.SpreadRadius = (script.SpreadRadius + spell.GetEffectValueAtLevel(SpellEffect.AreaSize, spell.CurrentLevel)) * GetEffectiveAreaMultiplier();
			script.MarkSeconds = (script.MarkSeconds + spell.GetEffectValueAtLevel(SpellEffect.ZoneDuration, spell.CurrentLevel)) * durationMultiplier;
		}
		arena.AddChild(plague);
		(plague as Contagion)?.InfectFirst(carrier);
		ApplyLegendaryVisual(plague, spell);
		return CastOutcome.Fired;
	}

	// Bramble Seed (.ai/side-events.md). Planted at the player's feet; the oldest of MaxAlive
	// patches withers when a new one is sown.
	private CastOutcome FireBrambleSeed(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;
		Node2D arena = GetParent<Node2D>();
		if (arena == null) return CastOutcome.NoTarget;

		BrambleSeed oldest = null;
		int alive = 0;
		foreach (Node child in arena.GetChildren())
		{
			// Only what the player sowed counts: Wildwood seedlings (generation 1) are bounded by their parent.
			// A withering patch has already been pushed out and no longer hurts, so it is not counted.
			if (child is not BrambleSeed patch || patch.IsQueuedForDeletion() || patch.IsWithering || patch.Generation != 0) continue;
			alive++;
			oldest ??= patch;
		}
		// Pulls its roots back in over half a second rather than vanishing on this frame.
		if (alive >= BrambleSeed.MaxAlive)
			oldest?.Wither();

		var seed = scene.Instantiate<Node2D>();
		if (seed is BrambleSeed script)
		{
			script.SpellData = spell;
			script.PlayerRef = this;
			script.Damage = ScaledSpellDamage(spell);
			// The evolutions' area belongs in the radius the thorns hurt at. Without the
			// GetAreaMultiplierAtLevel term Amplified Impact and Titan Cataclysm grew nothing, and
			// Bramble Seed at level 8 dealt a fifth of what the other pure spells do.
			script.MaxRadius = (script.MaxRadius + spell.GetEffectValueAtLevel(SpellEffect.AreaSize, spell.CurrentLevel))
				* GetEffectiveAreaMultiplier() * spell.GetAreaMultiplierAtLevel(spell.CurrentLevel);
			script.HoldSeconds = (script.HoldSeconds + spell.GetEffectValueAtLevel(SpellEffect.ZoneDuration, spell.CurrentLevel)) * durationMultiplier;
		}
		arena.AddChild(seed);
		seed.GlobalPosition = GlobalPosition;
		ApplyLegendaryVisual(seed, spell);
		// The patch draws its own radius, so the evolution's visual scale stays out of it: Titan
		// Cataclysm's x2 used to paint the thicket twice as wide as the ground it actually hurt.
		seed.Scale = Vector2.One;
		return CastOutcome.Fired;
	}

	private CastOutcome FireOrRefreshFlamethrower(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;

		var existing = GetChildren().OfType<Flamethrower>()
			.FirstOrDefault(f => f.SpellData != null && f.SpellData.Id.Equals(spell.Id, StringComparison.OrdinalIgnoreCase));

		if (existing == null)
		{
			var flame = scene.Instantiate<Node2D>();
			// Poured from the staff head, not the body's centre - see StaffTipLocal.
			flame.Position = StaffTipLocal();
			cinderbreathEmitter = flame;
			if (flame is Flamethrower script)
			{
				script.SpellData = spell;
				script.DamageMultiplier = GetSpellDamageMultiplier(spell);
				script.AreaMultiplier = GetEffectiveAreaMultiplier();
				script.CooldownMultiplier = cooldownMultiplier / attackSpeedMultiplier;
				script.SetSpellLevel(spell.CurrentLevel);
				script.PlayerRef = this;
			}
			AddChild(flame);
			ApplyLegendaryVisual(flame, spell);
			// Maintained, not Fired: the emitter plays its own cue each time a burn lights, the
			// first one from its _Ready, so a Fired here would play the creation cue twice.
			return CastOutcome.Maintained;
		}
		else
		{
			existing.DamageMultiplier = GetSpellDamageMultiplier(spell);
			existing.AreaMultiplier = GetEffectiveAreaMultiplier();
			existing.CooldownMultiplier = cooldownMultiplier / attackSpeedMultiplier;
			existing.SetSpellLevel(spell.CurrentLevel);
		}
		// Kept current, nothing new cast: see CastOutcome.Maintained.
		return CastOutcome.Maintained;
	}

	private CastOutcome FireOrRefreshElementalPulse(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;
		var existing = GetChildren().OfType<ElementalPulse>().FirstOrDefault(p => p.SpellData != null && p.SpellData.Id.Equals(spell.Id, StringComparison.OrdinalIgnoreCase));
		if (existing == null)
		{
			var pulse = scene.Instantiate<Area2D>();
			pulse.Position = Vector2.Zero;
			var script = pulse as ElementalPulse;
			if (script != null)
			{
				script.SpellData = spell;
				script.DamageMultiplier = GetSpellDamageMultiplier(spell);
				script.AreaMultiplier = GetEffectiveAreaMultiplier();
				script.CooldownMultiplier = cooldownMultiplier / attackSpeedMultiplier;
				script.DurationMultiplier = durationMultiplier;
				script.SetSpellLevel(spell.CurrentLevel);
				script.PlayerRef = this;
				script.TotalLifetime = 0f;
			}
			AddChild(pulse);
			ApplyLegendaryVisual(pulse, spell);
			// Maintained, not Fired: the pulse plays its own cue every time it goes off, the first
			// one from its _Ready, so a Fired here would play the creation cue twice.
			return CastOutcome.Maintained;
		}
		else
		{
			existing.DamageMultiplier = GetSpellDamageMultiplier(spell);
			existing.AreaMultiplier = GetEffectiveAreaMultiplier();
			existing.CooldownMultiplier = cooldownMultiplier / attackSpeedMultiplier;
			existing.DurationMultiplier = durationMultiplier;
			existing.SetSpellLevel(spell.CurrentLevel);
		}
		// Kept current, nothing new cast: see CastOutcome.Maintained.
		return CastOutcome.Maintained;
	}

	// Glacial Spike (issue #13): erupts one or more ice spikes from the ground at/around the nearest
	// enemy. Each spike spurts up, deals AoE damage to everything in its (Area-scaled) hit radius so a
	// bigger spike hits more enemies, then shakes as it retracts. Higher projectile counts drop several
	// spikes scattered around the target at once.
	private CastOutcome FireIceSpikes(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;
		FindNearestEnemy(out var nearest, out var minDist);
		if (nearest == null) return CastOutcome.NoTarget;

		float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
		if (minDist > castRange) return CastOutcome.NoTarget;

		int spikeCount = Math.Max(1, spell.GetProjectileCountAtLevel(spell.CurrentLevel) + amountBonus);
		const float scatterRadius = 70f;
		for (int i = 0; i < spikeCount; i++)
		{
			Vector2 target = nearest.GlobalPosition;
			if (i > 0)
				target += new Vector2(combatRng.RandfRange(-scatterRadius, scatterRadius), combatRng.RandfRange(-scatterRadius, scatterRadius));

			var spike = scene.Instantiate<Node2D>();
			var script = spike as IceSpike;
			if (script != null)
			{
				script.SpellData = spell;
				script.DamageMultiplier = GetSpellDamageMultiplier(spell);
				script.AreaMultiplier = GetEffectiveAreaMultiplier();
				script.SetSpellLevel(spell.CurrentLevel);
				script.PlayerRef = this;
			}
			GetParent().AddChild(spike);
			ApplyLegendaryVisual(spike, spell);
			script?.CastAt(target);
		}
		return CastOutcome.Fired;
	}

	private CastOutcome FireGroundSpike(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;
		var enemies = GetTree().GetNodesInGroup("enemies");
		if (enemies.Count == 0) return CastOutcome.NoTarget;

		Node2D nearest = null;
		float minDist = float.MaxValue;
		foreach (var e in enemies)
		{
			if (e is Node2D n2d)
			{
				float dist = GlobalPosition.DistanceTo(n2d.GlobalPosition);
				if (dist < minDist) { minDist = dist; nearest = n2d; }
			}
		}
		if (nearest == null) return CastOutcome.NoTarget;

		float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
		if (minDist > castRange) return CastOutcome.NoTarget;

		// One eruption per projectile, each under a DIFFERENT enemy, nearest first. The spell used to
		// ignore its projectile count entirely, so every Amount source in the game - the spell levels,
		// chest items, boons - did nothing for the Geomancer, and one spike on one enemy was why he
		// finished runs at half the other wizards' damage. Distinct targets rather than Glacial Spike's
		// scatter around one: the ground opens under the front rank of the swarm, which is the hold-
		// the-line identity, and it keeps the two spike spells reading differently.
		int spikeCount = Math.Max(1, spell.GetProjectileCountAtLevel(spell.CurrentLevel) + amountBonus);
		EnemyTargeting.CollectNearest(GetTree(), GlobalPosition, spikeCount, magicMissileTargets, castRange);
		if (magicMissileTargets.Count == 0)
			magicMissileTargets.Add(nearest);
		for (int i = 0; i < magicMissileTargets.Count; i++)
		{
			var spike = scene.Instantiate<Node2D>();
			var script = spike as GroundSpike;
			if (script != null)
			{
				script.SpellData = spell;
				script.DamageMultiplier = GetSpellDamageMultiplier(spell);
				// The evolutions' area belongs in the eruption radius - the same bug Bramble Seed had.
				// Without it Splinter Field and Tectonic Uprising grew only the drawing, the level 8 spike
				// was painted twice as wide as it hurt, and the Geomancer could not hold the swarm once the
				// 30-minute economy stopped levelling him past it.
				script.AreaMultiplier = GetEffectiveAreaMultiplier() * spell.GetAreaMultiplierAtLevel(spell.CurrentLevel);
				script.SetSpellLevel(spell.CurrentLevel);
				script.PlayerRef = this;
			}
			GetParent().AddChild(spike);
			ApplyLegendaryVisual(spike, spell);
			script?.CastAt(magicMissileTargets[i].GlobalPosition);
		}
		if (spell.IsApotheosis)
			SunderingRing.Raise(GetParent(), this, spell, GlobalPosition);
		return CastOutcome.Fired;
	}

	private CastOutcome FireOrRefreshOrbitingBlade(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;
		var existing = GetChildren().OfType<OrbitingBlade>().FirstOrDefault(o => o.SpellData != null && o.SpellData.Id.Equals(spell.Id, StringComparison.OrdinalIgnoreCase));
		if (existing == null)
		{
			var orbit = scene.Instantiate<Node2D>();
			orbit.Position = Vector2.Zero;
			var script = orbit as OrbitingBlade;
			if (script != null)
			{
				script.SpellData = spell;
				script.DamageMultiplier = GetSpellDamageMultiplier(spell);
				script.AreaMultiplier = GetEffectiveAreaMultiplier();
				script.AttackSpeedMultiplier = attackSpeedMultiplier;
				script.ProjectileCountBonus = amountBonus;
				script.SetSpellLevel(spell.CurrentLevel);
				script.PlayerRef = this;
			}
			AddChild(orbit);
			ApplyLegendaryVisual(orbit, spell);
			return CastOutcome.Fired;
		}
		else
		{
			existing.DamageMultiplier = GetSpellDamageMultiplier(spell);
			existing.AreaMultiplier = GetEffectiveAreaMultiplier();
			existing.AttackSpeedMultiplier = attackSpeedMultiplier;
			existing.ProjectileCountBonus = amountBonus;
			existing.SetSpellLevel(spell.CurrentLevel);
		}
		// Kept current, nothing new cast: see CastOutcome.Maintained.
		return CastOutcome.Maintained;
	}

	// --- Firing helpers for the issue #28 D&D-inspired spells ---

	private void FindNearestEnemy(out Node2D nearest, out float minDist)
	{
		nearest = null;
		minDist = float.MaxValue;
		foreach (var e in GetTree().GetNodesInGroup("enemies"))
		{
			if (e is Node2D n2d)
			{
				float dist = GlobalPosition.DistanceTo(n2d.GlobalPosition);
				if (dist < minDist) { minDist = dist; nearest = n2d; }
			}
		}
	}

	// Reused by FindDensestEnemy and FindRandomEnemy so a cast does not allocate a fresh list.
	private readonly List<Node2D> aimCandidates = new List<Node2D>();
	// How close two enemies must be to count as one pack, and how many candidates are scored. The
	// cap keeps the pairwise count bounded in a late-game swarm; it runs once per cast, not per frame.
	private const float ClusterRadius = 110f;
	private const int MaxClusterCandidates = 48;

	private Node2D FindDensestEnemy(float castRange)
	{
		CollectEnemiesInRange(castRange);
		Node2D best = null;
		int bestCount = -1;
		int scored = Math.Min(aimCandidates.Count, MaxClusterCandidates);
		float clusterRadiusSquared = ClusterRadius * ClusterRadius;
		for (int i = 0; i < scored; i++)
		{
			Vector2 here = aimCandidates[i].GlobalPosition;
			int count = 0;
			foreach (var other in aimCandidates)
			{
				if (here.DistanceSquaredTo(other.GlobalPosition) <= clusterRadiusSquared)
					count++;
			}
			if (count > bestCount) { bestCount = count; best = aimCandidates[i]; }
		}
		aimCandidates.Clear();
		return best;
	}

	private Node2D FindRandomEnemy(float castRange)
	{
		CollectEnemiesInRange(castRange);
		Node2D pick = aimCandidates.Count > 0 ? aimCandidates[combatRng.RandiRange(0, aimCandidates.Count - 1)] : null;
		aimCandidates.Clear();
		return pick;
	}

	private void CollectEnemiesInRange(float castRange)
	{
		aimCandidates.Clear();
		float rangeSquared = castRange * castRange;
		foreach (var e in GetTree().GetNodesInGroup("enemies"))
		{
			if (e is Node2D n2d && IsInstanceValid(n2d) && GlobalPosition.DistanceSquaredTo(n2d.GlobalPosition) <= rangeSquared)
				aimCandidates.Add(n2d);
		}
	}

	private CastOutcome FireBlackTentacles(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;
		FindNearestEnemy(out var nearest, out var minDist);
		if (nearest == null) return CastOutcome.NoTarget;
		float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
		if (minDist > castRange) return CastOutcome.NoTarget;
		// A root zone dropped on one skeleton holds one skeleton (.ai/spell-roster.md, row 24).
		nearest = FindDensestEnemy(castRange) ?? nearest;

		var zone = scene.Instantiate<Node2D>();
		var script = zone as BlackTentacles;
		if (script != null)
		{
			script.SpellData = spell;
			script.CooldownMultiplier = cooldownMultiplier;
			script.DamageMultiplier = GetSpellDamageMultiplier(spell);
			// The evolutions' area belongs in the rift's radius, as for Bramble Seed and Cone of Cold:
			// Abyssal Spread, Elder God Maw and Shadow Forest all promised a bigger zone and used to
			// grow only the drawing (by ScaleMultiplier), never what it slowed.
			script.AreaMultiplier = GetEffectiveAreaMultiplier() * spell.GetAreaMultiplierAtLevel(spell.CurrentLevel);
			script.DurationMultiplier = durationMultiplier;
			// Amount is more tentacles.
			script.TentacleBonus = amountBonus;
			script.SetSpellLevel(spell.CurrentLevel);
			script.PlayerRef = this;
		}
		GetParent().AddChild(zone);
		ApplyLegendaryVisual(zone, spell);
		script?.CastAt(nearest.GlobalPosition);
		return CastOutcome.Fired;
	}

	// Thorned Vine (id thorn_vine). A vine bursts out of the ground between the player and the
	// densest pack in range and lashes across it in one wide sweep (scripts/ThornVine.cs).
	//
	// It keeps the densest-cluster aim the bolt had, for the same reason with a better fit: the
	// sweep cuts through enemies in the order it reaches them and snags after pierce + 1, so the
	// knot of a pack is where it fills its hits fastest - a sweep at a straggler swings through
	// empty air. The vine is rooted on the player's side of the pack and swept outward across it,
	// so it reads as something the player grew to fend the pack off rather than as a projectile.
	// The spell's own projectile count is the number of vines (Infinite Horizon's +3 sprout at other
	// enemies in range); Amount is not, as for every bolt it replaced.
	private CastOutcome FireThornVine(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;
		FindNearestEnemy(out var nearest, out var minDist);
		if (nearest == null) return CastOutcome.NoTarget;
		float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
		if (minDist > castRange) return CastOutcome.NoTarget;

		Node2D knot = FindDensestEnemy(castRange) ?? nearest;
		int vines = Math.Max(1, spell.GetProjectileCountAtLevel(spell.CurrentLevel));
		for (int i = 0; i < vines; i++)
		{
			Node2D target = i == 0 ? knot : (FindRandomEnemy(castRange) ?? knot);
			var node = scene.Instantiate<Node2D>();
			var vine = node as ThornVine;
			if (vine != null)
			{
				vine.SpellData = spell;
				vine.DamageMultiplier = GetSpellDamageMultiplier(spell);
				vine.AreaMultiplier = GetEffectiveAreaMultiplier() * spell.GetAreaMultiplierAtLevel(spell.CurrentLevel);
				vine.SetSpellLevel(spell.CurrentLevel);
				vine.PlayerRef = this;
			}
			GetParent().AddChild(node);
			ApplyLegendaryVisual(node, spell);
			vine?.Launch(GlobalPosition, target.GlobalPosition);
		}
		return CastOutcome.Fired;
	}

	private CastOutcome FireConeBlast(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;
		FindNearestEnemy(out var nearest, out _);
		Vector2 direction = nearest != null ? (nearest.GlobalPosition - GlobalPosition) : Vector2.Right;

		var cone = scene.Instantiate<Node2D>();
		var script = cone as ConeBlast;
		if (script != null)
		{
			script.SpellData = spell;
			script.DamageMultiplier = GetSpellDamageMultiplier(spell);
			// The evolutions' area belongs in the cone's reach, as for Bramble Seed and Obsidian
			// Spike: Zero Point Shockwave's area never reached the hit test while its x2 scale grew
			// the drawing, and the Frostweaver stalled at the enemy cap in the 30-minute economy.
			script.AreaMultiplier = GetEffectiveAreaMultiplier() * spell.GetAreaMultiplierAtLevel(spell.CurrentLevel);
			script.SetSpellLevel(spell.CurrentLevel);
			script.PlayerRef = this;
		}
		GetParent().AddChild(cone);
		ApplyLegendaryVisual(cone, spell);
		script?.Fire(GlobalPosition, direction);
		return CastOutcome.Fired;
	}

	// Scorching Ray (issue #28): fires beam lines to nearest enemies. Each beam extends from the
	// player to the target, then retracts into the target before despawning.
	private CastOutcome FireScorchingRay(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;
		// The nearest-in-range check comes before the sort. A spell with nothing in reach is now
		// retried every frame until something arrives (CastOutcome.NoTarget), and a linear scan is
		// what that should cost - not a LINQ sort of the whole horde.
		float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
		FindNearestEnemy(out var nearest, out var minDist);
		if (nearest == null || minDist > castRange) return CastOutcome.NoTarget;

		var enemies = GetTree().GetNodesInGroup("enemies")
			.OfType<Node2D>()
			.Where(e => IsInstanceValid(e))
			.OrderBy(e => GlobalPosition.DistanceTo(e.GlobalPosition))
			.ToList();
		if (enemies.Count == 0) return CastOutcome.NoTarget;

		// Scales target count with level so the ray fans out to more enemies as it levels.
		int levelTargetBonus = (spell.CurrentLevel - 1) / 2;
		int rayCount = Math.Max(1, spell.GetProjectileCountAtLevel(spell.CurrentLevel) + amountBonus + levelTargetBonus);
		int raysFired = 0;
		for (int i = 0; i < rayCount; i++)
		{
			var target = enemies[Math.Min(i, enemies.Count - 1)];
			if (GlobalPosition.DistanceTo(target.GlobalPosition) > castRange)
				continue;

			var ray = scene.Instantiate<Node2D>();
			ray.Position = Vector2.Zero;
			if (ray is ScorchingRayBeam beam)
			{
				beam.SpellData = spell;
				beam.DamageMultiplier = GetSpellDamageMultiplier(spell);
				beam.AreaMultiplier = areaMultiplier;
				beam.SetSpellLevel(spell.CurrentLevel);
				beam.PlayerRef = this;
			}
			GetParent().AddChild(ray);
			ApplyLegendaryVisual(ray, spell);
			(ray as ScorchingRayBeam)?.Fire(GlobalPosition, target, target.GlobalPosition);
			raysFired++;
		}
		return raysFired > 0 ? CastOutcome.Fired : CastOutcome.NoTarget;
	}

	// Meteor Swarm (issue #28): telegraphed multi-impact AoE - several delayed-detonation impact
	// zones (reusing GroundSpike.cs, same telegraph-then-explode pattern as Obsidian Spike) land
	// near the nearest enemy cluster simultaneously.
	private CastOutcome FireMeteorSwarm(SpellData spell, PackedScene scene)
	{
		if (scene == null) return CastOutcome.NoTarget;
		FindNearestEnemy(out var nearest, out var minDist);
		float castRange = spell.GetRangeAtLevel(spell.CurrentLevel);
		Vector2 center = (nearest != null && minDist <= castRange) ? nearest.GlobalPosition : GlobalPosition;

		const int impactCount = 4;
		const float scatterRadius = 110f;
		for (int i = 0; i < impactCount; i++)
		{
			var offset = new Vector2(combatRng.RandfRange(-scatterRadius, scatterRadius), combatRng.RandfRange(-scatterRadius, scatterRadius));
			var impact = scene.Instantiate<Node2D>();
			var script = impact as MeteorImpact;
			if (script != null)
			{
				script.SpellData = spell;
				script.DamageMultiplier = GetSpellDamageMultiplier(spell);
				script.AreaMultiplier = GetEffectiveAreaMultiplier();
				script.TelegraphDuration = 0.9f;
				script.SetSpellLevel(spell.CurrentLevel);
				script.PlayerRef = this;
			}
			GetParent().AddChild(impact);
			ApplyLegendaryVisual(impact, spell);
			script?.CastAt(center + offset);
		}
		return CastOutcome.Fired;
	}

	// delta isn't used yet, but it might be needed later
	private void MovePlayer(double delta)
	{
		var input = Vector2.Zero;
		// move_* rather than ui_*, and the difference is the analog stick. Both action sets carry
		// the stick, but ui_* has a 0.5 deadzone because a menu must not scroll under a thumb
		// resting on the pad - which on a walking player throws away the whole first half of the
		// stick's travel and then compresses the rest, so a gentle push did nothing and everything
		// past the midpoint was nearly full speed. move_* is the same keys at a 0.2 deadzone.
		input.X = Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left");
		input.Y = Input.GetActionStrength("move_down") - Input.GetActionStrength("move_up");

		// The virtual joystick overrides rather than adds: a stick already reports an analog
		// magnitude, and summing it with a held key would push the vector past full speed before
		// the normalise below could see it as intentional. Zero when there is no pad or no touch.
		Vector2 touchInput = TouchControls.MoveVector;
		if (touchInput != Vector2.Zero)
			input = touchInput;

		if (input.X > 0.01f)
			lastHorizontalFacing = 1;
		else if (input.X < -0.01f)
			lastHorizontalFacing = -1;

		if (bodySprite != null)
			// Flip to face the way he is walking - from whichever way the sheet was drawn.
			bodySprite.FlipH = (lastHorizontalFacing < 0) != spriteFacesLeft;
		// The jet follows the staff head across a flip.
		if (cinderbreathEmitter != null && IsInstanceValid(cinderbreathEmitter))
			cinderbreathEmitter.Position = StaffTipLocal();

		if (input.Length() > 1)
			input = input.Normalized();

		// Shoving through a crowd costs speed, per body he is actually pushing: an ordinary enemy
		// touching him (the HurtBox contact set) on the side he is walking toward. Enemies beside or
		// behind him are not in his way, and a blocker is not being pushed - he slides off it.
		// This used to count every contact at 10% each down to 40%, and he was also body-blocked
		// on top of it, which is what pinned him in a pack (see Enemy.BlockerCollisionLayer).
		bool walking = input.LengthSquared() > 0.01f;
		int pushing = walking ? CountEnemiesBeingPushed(input.Normalized()) : 0;
		CrowdPushCount = pushing;
		float crowdSpeedMultiplier = Mathf.Clamp(1.0f - (pushing * CrowdSlowPerEnemy), CrowdSlowFloor, 1.0f);

		_velocity = input * Speed * GetWindSpeedMultiplier() * GetStormboundSpeedMultiplier() * crowdSpeedMultiplier * boonMoveSpeedMultiplier;
		Velocity = _velocity;
		Vector2 start = GlobalPosition;
		MoveAndSlide();
		ConstrainToGround(start, (float)delta);

		// What every enemy reads to make way: the step he actually achieved, after the crowd, the
		// blockers and the shore had their say, so a wizard stopped at the water shoves nothing.
		// Zero when he is not walking - standing still in a pack pushes no one.
		ShoveVelocity = walking ? Velocity : Vector2.Zero;
	}

	// Body half-width for the shove lane, from the 26 px wide body rectangle in player.tscn.
	public const float ShoveHalfWidth = 13f;
	// Feel numbers for pushing through a crowd. At the 220 base speed: one body 194, two 167, three
	// 141, four or more 121 px/s - still faster than a skeleton at full pace (125 x 0.85 = 106), so
	// a pack he commits to is one he gets out of, but it is never free.
	[Export] public float CrowdSlowPerEnemy { get; set; } = 0.12f;
	[Export] public float CrowdSlowFloor { get; set; } = 0.55f;

	/// <summary>The velocity his last step achieved while walking, zero while standing.</summary>
	public Vector2 ShoveVelocity { get; private set; }
	/// <summary>How many ordinary enemies he was pushing on his last step.</summary>
	public int CrowdPushCount { get; private set; }

	// Allocation-free: HashSet's enumerator is a struct, and the set is the HurtBox contact set the
	// contact damage already maintains, so this is a handful of dot products at most.
	private int CountEnemiesBeingPushed(Vector2 heading)
	{
		int pushing = 0;
		foreach (Node node in overlappingEnemies)
		{
			if (node is not Enemy enemy || !IsInstanceValid(enemy) || enemy.BlocksPlayer || enemy.IsDying)
				continue;
			if ((enemy.GlobalPosition - GlobalPosition).Dot(heading) > 0f)
				pushing++;
		}
		return pushing;
	}

	// The second half of a step: the body has just slid against enemies and decor (MoveAndSlide in
	// MovePlayer); now the same step is replayed from where it started with only the feet, against
	// only the ground, and the player ends wherever the feet did. Ground last, so it always has the
	// final word - a slide off an enemy can never carry the boots out over the water. The cost runs
	// the other way and is tiny: a shoreline slide can nudge the body a pixel into an enemy or a
	// tree, and the next frame's MoveAndSlide recovers it from that overlap before moving.
	private void ConstrainToGround(Vector2 start, float delta)
	{
		if (feetBody == null || delta <= 0f)
			return;

		Vector2 wanted = GlobalPosition - start;
		GlobalPosition = start;
		feetBody.Position = Vector2.Zero;
		feetBody.Velocity = wanted / delta;
		feetBody.MoveAndSlide();

		Vector2 travelled = feetBody.GlobalPosition - start;
		feetBody.Position = Vector2.Zero;
		GlobalPosition = start + travelled;
		// What the step actually achieved, as a single MoveAndSlide used to leave it: zero when
		// walking straight into the water, so the body idles rather than marching on the spot.
		Velocity = feetBody.Velocity;
	}

	public void AddXp(int amount)
	{
		if (IsPlaytestModeEnabled)
			amount = Mathf.RoundToInt(amount * 1.15f);

		amount = Math.Max(1, Mathf.RoundToInt(amount * growthMultiplier * GetArcaneXpBonusMultiplier() * (1.0f + chestXpBonusPercent)));
		CurrentXP += amount;
		if (CurrentXP >= XPToNextLevel)
		{
			CurrentXP -= CalculateXPForLevel(CurrentLevel); // Reset XP to zero, but keep overflow
			CurrentLevel += 1; // level up
			XPToNextLevel = CalculateXPForLevel(CurrentLevel);
			GD.Print($"Player leveled up to level {CurrentLevel}!");
			// Optionally, you could emit a signal or call a method to open a level-up menu here
			// Open the level-up menu here
			// LevelUpMenu? menu = GetTree().Root.GetNodeOrNull<LevelUpMenu>("LevelUpMenu");
			// if (menu != null)
			// {
			// 	menu.Show();
			// 	menu.SetOptions(); // You can pass specific options if needed
			// }
			EmitSignal(nameof(LevelGained));

		}
		EmitSignal(nameof(XpGained), CurrentXP);

	}

	public void LevelUpImmediately()
	{
		CurrentLevel += 1;
		XPToNextLevel = CalculateXPForLevel(CurrentLevel);
		GD.Print($"Player leveled up to level {CurrentLevel}!");
		EmitSignal(nameof(LevelGained));
		EmitSignal(nameof(XpGained), CurrentXP);
	}

	public bool TryAddOrLevelSpell(string selectionId, bool forceLegendary = false)
	{
		string spellId = selectionId;
		string evolutionId = null;
		if (!string.IsNullOrWhiteSpace(selectionId) && selectionId.Contains(':'))
		{
			var parts = selectionId.Split(':', 2);
			spellId = parts[0];
			evolutionId = parts[1];
		}

		SpellData spellTemplate = ResolveSpellTemplate(spellId);
		if (spellTemplate == null)
		{
			GD.PrintErr($"Spell '{spellId}' not found in spell catalog.");
			return false;
		}

		SpellData existing = equippedSpells.FirstOrDefault(s => s != null && s.Id.Equals(spellTemplate.Id, StringComparison.OrdinalIgnoreCase));
		if (existing == null)
		{
			if (IsSpellBookFull)
			{
				GD.PrintErr($"Cannot add spell '{spellTemplate.Id}': the spell book is full ({SpellSlots} slots). A full book is never offered a new spell.");
				return false;
			}

			var runtimeSpell = spellTemplate.Duplicate(true) as SpellData;
			if (runtimeSpell == null)
				return false;

			runtimeSpell.CurrentLevel = 1;
			runtimeSpell.IsLegendary = forceLegendary || combatRng.Randf() < GetLegendaryChance();
			if (runtimeSpell.IsLegendary && !runtimeSpell.Name.EndsWith(" \u2605"))
			{
				runtimeSpell.Name += " \u2605";
				GD.Print($"{runtimeSpell.Name} rolled Legendary!");
			}
			equippedSpells.Add(runtimeSpell);
			spellFireTimers[runtimeSpell.Id] = 0f;
			GD.Print($"Equipped spell {runtimeSpell.Name} at level {runtimeSpell.CurrentLevel}");
			RefreshPersistentSpellInstance(runtimeSpell);
			RefreshElementalMaxHp();
			EmitSignal(SignalName.LoadoutChanged);
			return true;
		}

		// Level 8 only through the Ascension. A selection with no evolution for a spell at its card
		// cap is refused rather than quietly maxing it with no branch, which is the trap the strip
		// exists to close.
		if (string.IsNullOrWhiteSpace(evolutionId) && existing.CurrentLevel >= CardLevelCap(existing))
		{
			GD.PrintErr($"'{existing.Id}' is at level {existing.CurrentLevel}; level {AscensionSpellLevel} is taken by Ascending, not by a card.");
			return false;
		}

		// The Apotheosis (issue #60) takes the pick without taking a level: the spell is already at
		// its maximum, and the form is terminal.
		if (!string.IsNullOrWhiteSpace(evolutionId) && ApotheosisCatalog.ForForm(evolutionId) is ApotheosisDefinition apotheosis
			&& apotheosis.SpellId.Equals(existing.Id, StringComparison.OrdinalIgnoreCase))
		{
			if (existing.IsApotheosis || existing.CurrentLevel < existing.MaxLevel)
				return false;
			existing.ApplyEvolution(apotheosis.Form);
			RunEvents.RecordApotheosis(apotheosis.Form.Id);
			AttachApotheosisField(existing);
			SfxPlayer.Global(SfxCatalog.SpellEvolve);
			GD.Print($"APOTHEOSIS: {existing.Name} became {apotheosis.Form.DisplayName}");
			spellFireTimers[existing.Id] = 0f;
			RefreshPersistentSpellInstance(existing);
			RefreshElementalMaxHp();
			EmitSignal(SignalName.LoadoutChanged);
			return true;
		}

		existing.CurrentLevel = Math.Min(existing.MaxLevel, existing.CurrentLevel + 1);

		if (!string.IsNullOrWhiteSpace(evolutionId))
		{
			var evo = existing.GetEvolutionOptionsForLevel(existing.CurrentLevel).FirstOrDefault(e => e != null && e.Id.Equals(evolutionId, StringComparison.OrdinalIgnoreCase))
				   ?? spellTemplate.GetEvolutionOptionsForLevel(existing.CurrentLevel).FirstOrDefault(e => e != null && e.Id.Equals(evolutionId, StringComparison.OrdinalIgnoreCase));

			if (evo != null)
			{
				existing.ApplyEvolution(evo);
				// RecordSpellEvolved existed and nothing called it, so no run ever knew it had evolved.
				RunEvents.RecordSpellEvolved(existing.Id);
				GD.Print($"Applied evolution '{evo.DisplayName}' to {existing.Name} at level {existing.CurrentLevel}!");
			}
		}

		spellFireTimers[existing.Id] = 0f;
		GD.Print($"Spell {existing.Name} leveled up to {existing.CurrentLevel}");
		RefreshPersistentSpellInstance(existing);
		EmitSignal(SignalName.LoadoutChanged);
		return true;
	}

	// Removes an owned spell from this run's loadout. The level-up swap and erase that used to call
	// this are gone - spells are permanent once taken - so its one caller is the Scribe's Penknife
	// (TryCutSpellWithPenknife). Distinct from #21's cross-run pool curation, which never touches a
	// held spell.
	private bool RemoveEquippedSpell(string spellId)
	{
		if (string.IsNullOrWhiteSpace(spellId))
			return false;

		var existing = equippedSpells.FirstOrDefault(s => s != null && s.Id.Equals(spellId, StringComparison.OrdinalIgnoreCase));
		if (existing == null)
			return false;

		equippedSpells.Remove(existing);
		spellFireTimers.Remove(existing.Id);
		RemovePersistentSpellInstance(existing);

		// The bow only ever redraws from inside TickHuntersDraw, so swapping the spell away
		// mid-draw would otherwise leave a half-drawn bow frozen on the player forever.
		if (existing.Id.Equals("hunters_draw", StringComparison.OrdinalIgnoreCase))
		{
			huntersDrawCharge = 0f;
			huntersDrawHoldSeconds = 0f;
			huntersDrawStillSeconds = 0f;
			huntersDrawTracking = false;
			huntersDrawBow?.Clear();
		}

		GD.Print($"Removed spell {existing.Name} from loadout.");
		RefreshElementalMaxHp();
		EmitSignal(SignalName.LoadoutChanged);
		return true;
	}

	public IReadOnlyList<SpellData> GetEquippedSpells() => equippedSpells;

	private void RemovePersistentSpellInstance(SpellData spell)
	{
		if (spell == null) return;

		if (spell.Id.Equals("arcane_explosion", StringComparison.OrdinalIgnoreCase))
		{
			var existing = GetChildren().OfType<ArcaneExplosion>().FirstOrDefault();
			existing?.QueueFree();
		}

		if (spell.Id.Equals("spiritual_weapon", StringComparison.OrdinalIgnoreCase))
		{
			var existing = GetChildren().OfType<SpiritualWeapon>().FirstOrDefault();
			existing?.QueueFree();
		}

	}

	// --- Level-up charges ---

	/// <summary>Strikes a spell off the offer pool for the rest of the run.</summary>
	public bool TryBanSpell(string spellId)
	{
		if (string.IsNullOrWhiteSpace(spellId) || BansRemaining <= 0)
			return false;
		// Marginalia is what a hand holds when there is nothing else; striking it would strike nothing,
		// because it is dealt after bans are applied, and the charge would be gone for no change.
		if (spellId.Equals(MarginaliaId, StringComparison.OrdinalIgnoreCase))
			return false;

		if (!bannedSpellIds.Add(spellId))
			return false; // Already banned - do not charge the player twice for it.

		BansRemaining--;
		return true;
	}

	public IReadOnlyCollection<string> GetBannedSpellIds() => bannedSpellIds;

	/// <summary>Defers this level-up, buying an extra pick at the next one.</summary>
	public bool TryBankLevelUp()
	{
		if (BanksRemaining <= 0)
			return false;

		BanksRemaining--;
		BankedLevelUps++;
		return true;
	}

	/// <summary>Spends a banked level-up. Returns false when there are none.</summary>
	public bool TrySpendBankedLevelUp()
	{
		if (BankedLevelUps <= 0)
			return false;

		BankedLevelUps--;
		return true;
	}

	/// <summary>Names a spell that the next level-up offer must contain.</summary>
	public bool TryAugurSpell(string spellId)
	{
		if (string.IsNullOrWhiteSpace(spellId) || AuguriesRemaining <= 0)
			return false;

		AuguriesRemaining--;
		guaranteedNextOfferSpellId = spellId;
		return true;
	}

	/// <summary>Everything the player could legally be offered, for the augury picker.</summary>
	/// <remarks>
	/// Runs the real candidate pass rather than listing the spell catalog, so the picker can never
	/// promise something the roll would not have produced - a locked spell, a maxed one, or one the
	/// player has already banned. Sorted by name because a randomly ordered picker is unusable.
	/// </remarks>
	public List<LevelUpOption> GetAuguryCandidates()
	{
		return GetLevelUpOptions(int.MaxValue, isAuguryBrowse: true)
			.OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	/// <summary>Snapshot of the charges, for the level-up menu.</summary>
	public LevelUpCharges BuildCharges(int rerollsRemaining)
	{
		return new LevelUpCharges
		{
			Rerolls = rerollsRemaining,
			Bans = BansRemaining,
			Banks = BanksRemaining,
			Auguries = AuguriesRemaining,
			AuguryCandidates = AuguriesRemaining > 0 ? GetAuguryCandidates() : new List<LevelUpOption>(),
		};
	}

	// --- Apotheosis (issue #59, #60) ---------------------------------------------------------------


	// Three forms run on the hit and kill events rather than inside their spell's own script, and
	// live as one node in the arena for the rest of the run. The other three are cast hooks
	// (Sundering, Spring Tide) or live in the spell (The Long Winter's ring in ConeBlast, Wildwood
	// in BrambleSeed).
	private void AttachApotheosisField(SpellData spell)
	{
		Node arena = GetParent();
		if (arena == null)
			return;
		switch (spell.Id)
		{
			case "cinderbreath":
				arena.AddChild(new CataclysmField { Name = "CataclysmField", PlayerRef = this, SpellData = spell });
				break;
			case "cone_of_cold":
				arena.AddChild(new LongWinterField { Name = "LongWinterField", PlayerRef = this, SpellData = spell });
				break;
			case "chain_lightning":
				arena.AddChild(new LatticeField { Name = "LatticeField", PlayerRef = this, SpellData = spell });
				break;
		}
	}

	/// <summary>The character this run is played as. Null only before _Ready has resolved it.</summary>
	public CharacterData SelectedCharacter => selectedCharacter;

	/// <summary>
	/// The card offering a wizard's final form, if the covenant holds on <paramref name="spell"/>:
	/// the wizard's own starting spell, at max level, down the named path, with the element at the
	/// required tier, and the player at ApotheosisCatalog.MinimumPlayerLevel. Null otherwise. Built for the else side of the level-up branch, which is the
	/// only place a maxed spell was ever looked at again.
	/// </summary>
	private LevelUpOption TryBuildApotheosisOffer(SpellData spell, Dictionary<Element, int> baselineElementCounts)
	{
		ApotheosisDefinition definition = ApotheosisCatalog.ForCharacter(selectedCharacter?.Id);
		if (!ApotheosisCatalog.SpellMeetsPath(definition, spell))
			return null;
		if (GetElementTier(definition.RequiredElement) < definition.RequiredTier)
			return null;
		if (CurrentLevel < ApotheosisCatalog.MinimumPlayerLevel)
			return null;

		var option = new LevelUpOption
		{
			SpellId = spell.Id,
			DisplayName = spell.Name,
			Description = definition.Form.Description,
			UpgradeSummary = $"✶ APOTHEOSIS - {spell.Name} becomes {definition.Form.DisplayName}",
			NextLevel = spell.MaxLevel,
			MaxLevel = spell.MaxLevel,
			IsNewUnlock = false,
			IsPassive = spell.IsPassive,
			Icon = spell.Icon,
			IsEvolutionMilestone = true,
			MilestoneLevel = ApotheosisCatalog.MilestoneLevel,
			EvolutionChoices = new List<SpellEvolutionOption> { definition.Form },
		};
		ApplyElementPreview(option, spell, baselineElementCounts, isNewUnlock: false);
		return option;
	}

	// --- The Ascension strip ------------------------------------------------------------------
	//
	// Level 8 used to be the eighth level-up card. When it came up before its element was deep
	// enough, MarkUnearnedAscensions turned it into a plain upgrade, and taking that maxed the spell
	// with no Ascension - for the rest of the run, and with it any chance of the wizard's Apotheosis.
	// Nothing on the card said so. Now level-up cards stop at level 7 (all pips full), and the
	// Ascension is a separate option at the top of the screen, offered on EVERY level-up once the
	// spell is at 7 and at least one branch is earned. It cannot be missed, and it cannot be lost.

	/// <summary>The highest level a level-up CARD can take this spell to: 7 when it has an Ascension.</summary>
	public static int CardLevelCap(SpellData spell)
	{
		if (spell == null)
			return 1;
		SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
		bool hasAscension = spell.Level8Options != null && spell.Level8Options.Count > 0
			&& spell.MaxLevel >= AscensionSpellLevel;
		return hasAscension ? AscensionSpellLevel - 1 : spell.MaxLevel;
	}

	/// <summary>
	/// Everything that belongs in the strip above the cards right now: each spell sitting at level 7
	/// with an earned Ascension branch, and the wizard's Apotheosis once its covenant holds.
	/// </summary>
	public List<LevelUpOption> GetAscensionOffers()
	{
		var offers = new List<LevelUpOption>();
		var counts = GetElementInstanceCounts();
		foreach (SpellData spell in equippedSpells)
		{
			if (spell == null)
				continue;

			if (TryBuildApotheosisOffer(spell, counts) is LevelUpOption apotheosis)
			{
				offers.Add(apotheosis);
				continue;
			}

			if (spell.CurrentLevel != CardLevelCap(spell) || spell.CurrentLevel >= spell.MaxLevel)
				continue;
			if (CurrentLevel < AscensionMinimumPlayerLevel)
				continue;

			var option = new LevelUpOption
			{
				SpellId = spell.Id,
				DisplayName = spell.Name,
				Description = spell.Description,
				UpgradeSummary = "\u2605 ASCENSION - Choose a game-defining evolution!",
				NextLevel = AscensionSpellLevel,
				MaxLevel = spell.MaxLevel,
				IsNewUnlock = false,
				IsPassive = spell.IsPassive,
				Icon = spell.Icon,
				IsEvolutionMilestone = true,
				MilestoneLevel = AscensionSpellLevel,
				EvolutionChoices = spell.GetEvolutionOptionsForLevel(AscensionSpellLevel).ToList(),
			};
			MarkUnearnedAscensions(option, spell, counts);
			// MarkUnearnedAscensions clears the milestone when no branch is earned yet. Then it is
			// not offered at all - the ascension browser says what is missing.
			if (!option.IsEvolutionMilestone)
				continue;
			ApplyElementPreview(option, spell, counts, isNewUnlock: false);
			offers.Add(option);
		}
		return offers;
	}

	/// <summary>
	/// Opens one level-up: decides, once, whether upgrades to held spells are in this level-up's
	/// pool. Node2DGame calls it when the level-up screen appears and never on a reroll or a ban.
	/// </summary>
	public void BeginLevelUpOffer()
	{
		upgradesOnOfferThisLevelUp = UpgradeOfferChance >= 1f || levelUpRng.Randf() < UpgradeOfferChance;
		boonOnOfferThisLevelUp = ForceBoonOnNextOffer || BoonOfferChance >= 1f || levelUpRng.Randf() < BoonOfferChance;
		ForceBoonOnNextOffer = false;
	}

	public List<LevelUpOption> GetLevelUpOptions(int maxOptions = 3, bool isAuguryBrowse = false)
	{
		bool loadoutFull = IsSpellBookFull;
		var baselineElementCounts = GetElementInstanceCounts();
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		var candidates = new List<LevelUpOption>();
		var weights = new List<float>();
		// Upgrades to held spells are collected apart and only joined to the pool when this level-up
		// rolled them in, when the book is full, or when leaving them out would leave nothing at all.
		var heldUpgrades = new List<LevelUpOption>();
		var heldUpgradeWeights = new List<float>();
		foreach (var template in spellCatalog.Values)
		{
			if (template == null || string.IsNullOrWhiteSpace(template.Id))
				continue;

			SpellData equipped = equippedSpells.FirstOrDefault(s => s != null && s.Id.Equals(template.Id, StringComparison.OrdinalIgnoreCase));
			if (equipped == null)
			{
				// SPELLS ARE PERMANENT ONCE TAKEN. A full book used to be offered new spells anyway,
				// each one a swap that erased a held spell to make room; the player's call was "once
				// you have your spells, that's what you've got". So a full book is offered no new
				// spell at all. The only ways to change it now are relics (the Scribe's Penknife cuts
				// one out, the Blank Folio adds a page) - .ai/gameplay-design.md, "Spells are permanent".
				if (loadoutFull)
					continue;

				// The character's own starting spell is always offerable, unlocked or not. A rescued
				// wizard opens on a spell the save may not own yet (Tituba's Riptide is a boss reward).
				// It used to matter because a swap could erase it; nothing can now (the Penknife spares
				// it), so this is a guard that the unlock check can never be the thing that loses it.
				bool ownStarter = selectedCharacter?.StartingSpellResource != null
					&& selectedCharacter.StartingSpellResource.Id.Equals(template.Id, StringComparison.OrdinalIgnoreCase);
				if (!ownStarter && !GlobalStatsManager.IsSpellUnlockedForLevelUp(saveManager?.Data, template.Id))
					continue;

				// Set aside from the spellbook. Only new offers are filtered - this branch is the
				// "not currently equipped" one, so a spell the player is already carrying keeps
				// levelling normally even if it was redacted after they picked it up.
				if (GlobalStatsManager.IsSpellRemovedFromPool(saveManager?.Data, template.Id))
					continue;

				var option = new LevelUpOption
				{
					SpellId = template.Id,
					DisplayName = template.Name,
					Description = template.Description,
					StatSummary = BuildSpellStatSummary(template),
					NextLevel = 1,
					MaxLevel = CardLevelCap(template),
					IsNewUnlock = true,
					IsPassive = template.IsPassive,
					Icon = template.Icon
				};
				ApplyElementPreview(option, template, baselineElementCounts, isNewUnlock: true);
				candidates.Add(option);
				weights.Add(GetOfferWeight(option));
				continue;
			}

			if (equipped.CurrentLevel < CardLevelCap(equipped))
			{
				int nextLevel = equipped.CurrentLevel + 1;
				var option = new LevelUpOption
				{
					SpellId = equipped.Id,
					DisplayName = equipped.Name,
					Description = equipped.Description,
					UpgradeSummary = BuildSpellUpgradeSummary(equipped, nextLevel),
					NextLevel = nextLevel,
					MaxLevel = CardLevelCap(equipped),
					IsNewUnlock = false,
					IsPassive = equipped.IsPassive,
					Icon = equipped.Icon
				};

				// Level 8 is never a card: it is the Ascension, offered in its own strip by
				// GetAscensionOffers. Only the level 4 mutation is still a card.
				if (nextLevel == 4)
				{
					var evoChoices = equipped.GetEvolutionOptionsForLevel(nextLevel);
					if (evoChoices == null || evoChoices.Count == 0)
					{
						evoChoices = template.GetEvolutionOptionsForLevel(nextLevel);
					}
					if (evoChoices != null && evoChoices.Count > 0)
					{
						option.IsEvolutionMilestone = true;
						option.MilestoneLevel = nextLevel;
						option.EvolutionChoices = evoChoices.ToList();

						// Not gated on element, unlike the Ascension: level 4 is where a build is still
						// being chosen, and gating it would just stall the player.
						option.UpgradeSummary = "✦ SPELL MUTATION (Level 4) - Choose a mechanical mutation!";
					}
				}

				ApplyElementPreview(option, equipped, baselineElementCounts, isNewUnlock: false);
				heldUpgrades.Add(option);
				heldUpgradeWeights.Add(GetOfferWeight(option));
			}
		}

		// Boons. Deliberately NOT gated on the spell loadout being full - a boon costs no slot, so a
		// level-up with a full book still has something to give that is not an upgrade.
		//
		// Collected apart from the spells and never weighed against them: whether this level-up
		// shows a boon at all is BoonOfferChance's call (BeginLevelUpOffer), and at most one is dealt
		// in after the spells are drawn. See the note on BoonOfferChance for why a weight could not
		// make them rare.
		var boonCandidates = new List<LevelUpOption>();
		var boonWeights = new List<float>();
		// The element the player holds most of. A boon carrying it is the likelier one to be dealt
		// (BoonOwnElementWeight) - see below.
		int deepestCount = baselineElementCounts.Count > 0 ? baselineElementCounts.Values.Max() : 0;
		if (CanTakeMoreBoons)
		{
			foreach (BoonDefinition boon in BoonCatalog.All)
			{
				if (HasBoon(boon.Id))
					continue;

				if (!GlobalStatsManager.IsBoonUnlocked(saveManager?.Data, boon.Id))
					continue;

				var boonOption = new LevelUpOption
				{
					SpellId = boon.Id,
					DisplayName = boon.Name,
					Description = boon.Description,
					StatSummary = BoonCatalog.DescribeEffect(boon),
					Icon = BonelightSkin.LoadTextureSafe(boon.IconPath),
					IsBoon = true,
					IsNewUnlock = true,
					NextLevel = 1,
					MaxLevel = 1,
				};

				foreach (var (elementName, weight) in boon.ElementWeights)
				{
					int baseline = baselineElementCounts.TryGetValue(
						Enum.Parse<Element>(elementName, true), out int existing) ? existing : 0;
					boonOption.SpellElementTags[elementName] = weight;
					boonOption.ElementContribution[elementName] = weight;
					boonOption.ResultingElementCounts[elementName] = baseline + weight;
				}

				// Rare boons must not close the thin elements. Water, Grass, Light, Metal and Darkness
				// reach their tiers partly through boons (.ai/passives-and-items.md section 3a), and
				// with one boon every four or five level-ups drawn evenly from fourteen, a Water build
				// would see about one Water boon a run. So the boon that IS dealt leans toward the
				// element the player is deepest in: how OFTEN a boon appears is unchanged, only WHICH.
				bool carriesDeepest = deepestCount > 0 && boon.ElementWeights.Any(e =>
					baselineElementCounts.TryGetValue(Enum.Parse<Element>(e.Element, true), out int held)
					&& held >= deepestCount);
				boonCandidates.Add(boonOption);
				boonWeights.Add(carriesDeepest ? BoonOwnElementWeight : 1f);
			}
		}

		// Bans are applied to the assembled candidate list rather than inside each build loop, so
		// one check covers spells, upgrades, evolutions and boons alike and cannot drift apart.
		RemoveBanned(candidates, weights);
		RemoveBanned(heldUpgrades, heldUpgradeWeights);
		RemoveBanned(boonCandidates, boonWeights);

		// The augury browses every boon as well as every new spell: naming one is exactly what the
		// charge is for, and a rare card is the one most worth naming.
		if (isAuguryBrowse)
		{
			candidates.AddRange(boonCandidates);
			weights.AddRange(boonWeights);
		}

		// The augury browses new spells and boons only. Naming a held spell would summon its upgrade
		// on demand, and the scarcity above would be one charge away from not existing.
		//
		// Otherwise upgrades join the pool when this level-up rolled them in, when the book is full,
		// or when without them there would be nothing to take. The scarcity roll decides between
		// deepening a spell and widening the book; a full book cannot widen, so withholding the
		// upgrades there would only deal a hand of one boon or of nothing.
		if (!isAuguryBrowse)
		{
			bool somethingElseToTake = candidates.Count > 0
				|| (boonOnOfferThisLevelUp && boonCandidates.Count > 0);
			if (upgradesOnOfferThisLevelUp || loadoutFull || !somethingElseToTake)
			{
				candidates.AddRange(heldUpgrades);
				weights.AddRange(heldUpgradeWeights);
			}
		}

		NormalizeOfferWeightsByGroup(candidates, weights);

		var picked = new List<LevelUpOption>();
		var pool = new List<LevelUpOption>(candidates);
		var poolWeights = new List<float>(weights);
		while (picked.Count < maxOptions && pool.Count > 0)
		{
			int idx = PickWeightedIndex(poolWeights);
			picked.Add(pool[idx]);
			pool.RemoveAt(idx);
			poolWeights.RemoveAt(idx);
		}

		// The boon, dealt in over one of the spells when this level-up rolled one. Also when the spells
		// left nothing at all: a full book, every spell at its cap, and a boon is the only pick that is
		// not a dead one - the same rule that lets upgrades in above.
		if (!isAuguryBrowse && boonCandidates.Count > 0 && maxOptions > 0
			&& (boonOnOfferThisLevelUp || picked.Count == 0))
		{
			LevelUpOption boon = boonCandidates[PickWeightedIndex(boonWeights)];
			// A random position, so the rare card is not always the one on the right.
			if (picked.Count >= maxOptions)
				picked[levelUpRng.RandiRange(0, picked.Count - 1)] = boon;
			else
				picked.Insert(levelUpRng.RandiRange(0, picked.Count), boon);
		}

		// The augury. Forced into the offer and then forgotten - it is a promise about one level-up,
		// not a permanent thumb on the scale. Browsing for a target must not consume it, which is
		// what isAuguryBrowse is for: GetAuguryCandidates runs this whole method to build its list.
		if (!isAuguryBrowse && !string.IsNullOrEmpty(guaranteedNextOfferSpellId))
		{
			string promisedId = guaranteedNextOfferSpellId;
			guaranteedNextOfferSpellId = string.Empty;

			bool alreadyOffered = picked.Any(o => o.SpellId.Equals(promisedId, StringComparison.OrdinalIgnoreCase));
			if (!alreadyOffered)
			{
				LevelUpOption promised = candidates.Concat(boonCandidates).FirstOrDefault(
					o => o.SpellId.Equals(promisedId, StringComparison.OrdinalIgnoreCase));

				// It can legitimately be gone - the player may have equipped it, maxed it or banned
				// it since. Silently dropping the promise is correct; the charge is already spent.
				if (promised != null)
				{
					if (picked.Count >= maxOptions && picked.Count > 0)
						picked[picked.Count - 1] = promised;
					else
						picked.Add(promised);
				}
			}
		}

		// The last resort, and the rule that keeps a level-up from ever being a dead pick: a full book
		// with every spell at its card cap, and no boon left to give (six held, or every unlocked one
		// owned). Swaps used to fill this hand. Marginalia does now - see MarginaliaId.
		if (!isAuguryBrowse && picked.Count == 0 && maxOptions > 0)
			picked.Add(BuildMarginaliaOption());

		// Note: owned-spell upgrade options are not guaranteed to appear. A level-up first decides
		// whether they are in its pool at all (UpgradeOfferChance, BeginLevelUpOffer); when they are,
		// they are weighted to show up more often than new spells (UpgradeOfferWeight vs
		// NewUnlockOfferWeight), so a screen can still legitimately offer only brand-new spells.
		return picked;
	}

	// --- Marginalia: the card for a level-up with nothing else in it ---------------------------
	//
	// With swaps gone, a late run can run out of level-up: a full book at every card cap and the
	// boon slots full. Something has to be on the screen, and it has to be worth a press, or the
	// level-up is a Skip button wearing a dialog.
	//
	// It heals AND adds damage because either half alone can be dead: a heal is nothing at full
	// health, and +2% damage is nothing to a player about to die. Small, so it never competes with
	// a real card - it only ever appears when there is no real card - and repeatable, because by
	// then every level-up is one of these, and a level-up that pays nothing is what it replaces.
	// Gold or Arcane Energy were the genre's answer (Vampire Survivors pays coins here); this game's
	// Arcane Energy is paid at the end of a run from how it went, and a per-level-up drip would make
	// late levels a currency farm rather than a fight.

	/// <summary>The option id of the fallback card. Not a spell, not a boon: Node2DGame routes it.</summary>
	public const string MarginaliaId = "marginalia";
	public const float MarginaliaHealFraction = 0.30f;
	public const float MarginaliaSpellDamageBonus = 0.02f;
	private const string MarginaliaIconPath = "res://assets/bonelight/ui/icons/marginalia.png";

	/// <summary>How many Marginalia this run has taken. Read by the balance harness.</summary>
	public int MarginaliaTaken { get; private set; }

	private LevelUpOption BuildMarginaliaOption()
	{
		return new LevelUpOption
		{
			SpellId = MarginaliaId,
			DisplayName = "Marginalia",
			Description = "Your spell book is full and every page is written. A note in the margin, instead.",
			StatSummary = $"Heal {Mathf.RoundToInt(MarginaliaHealFraction * 100f)}% of max HP{StatIcons.Separator}"
				+ $"+{Mathf.RoundToInt(MarginaliaSpellDamageBonus * 100f)}% spell damage",
			Icon = BonelightSkin.LoadTextureSafe(MarginaliaIconPath),
			// A new-unlock card with no levels: it shows its stat lines and no pips. Passive, because
			// it is neither an attack nor a boon and the card has only those three words for it.
			IsNewUnlock = true,
			IsPassive = true,
			NextLevel = 1,
			MaxLevel = 1,
		};
	}

	/// <summary>Takes the Marginalia card: heals, and adds a little spell damage for the run.</summary>
	public void TakeMarginalia()
	{
		MarginaliaTaken++;
		Heal(Mathf.Max(1, Mathf.RoundToInt(MaxHP * MarginaliaHealFraction)));
		// Multiplied in like a SpellDamage boon (ApplyBoon), so every damage reader already sees it.
		damageMultiplier *= 1f + MarginaliaSpellDamageBonus;
	}

	// The flat stats a spell arrives with, for a card that has no previous level to diff against.
	// Deliberately the same four numbers for every spell rather than a per-archetype list: the point
	// of the card is to let the player compare three options at a glance, and they cannot do that if
	// each one reports different fields.
	//
	// Zeroes are skipped rather than printed. A cone has no meaningful projectile count and a shield
	// has no range, and "Range 0" is worse than saying nothing.
	/// <summary>How deep a player must be in an ascension's element before it is offered.</summary>
	/// <remarks>
	/// Four, which is the middle element threshold. Two is most of a run away from meaningful and
	/// would gate nothing; six is the capstone and would mean most runs never see an ascension at
	/// all. Four says "you have committed to this element" without saying "you have finished".
	/// </remarks>
	public const int AscensionElementRequirement = 4;

	/// <summary>The player level below which no spell is offered its level 8 Ascension.</summary>
	/// <remarks>
	/// Without it the Ascension's timing was decided by how often the upgrade roll came up: on one
	/// set of runs it landed anywhere from minute 7 to minute 18.5 of a 30-minute run. A player
	/// level moves far less between runs. 14 is about minute 9 for the balance bot on the linear XP
	/// curve, the near edge of the 9-12 minute target (.ai/gameplay-design.md, "The Apotheosis");
	/// a human levels a little more slowly. It also keeps the Ascension apart from the Apotheosis
	/// (level 20, ApotheosisCatalog.MinimumPlayerLevel) - they used to land seconds apart. The
	/// ascension browser names it as a requirement.
	/// </remarks>
	// 16 since 2026-10-15, for the same reason as ApotheosisCatalog.MinimumPlayerLevel: the doubled
	// enemy cap put level 14 at minute 7-8, ahead of the 9-12 target. 17 after the confirming batch:
	// 16 still let the Pyromancer ascend at 7.5; the bot reaches 17 at minute 9-11.
	public const int AscensionMinimumPlayerLevel = 17;

	// Which elements an ascension is ABOUT. Its own bonus element if it grants one - that is the
	// direction the branch pushes the spell - and otherwise the spell's own tags, because an
	// ascension with no element of its own is an intensification of what the spell already is.
	private static IEnumerable<string> GetAscensionGateElements(SpellEvolutionOption evo, SpellData spell)
	{
		if (evo?.BonusElementWeights != null && evo.BonusElementWeights.Count > 0)
			return evo.BonusElementWeights.Keys.ToList();

		if (spell?.ElementWeights != null && spell.ElementWeights.Count > 0)
			return spell.ElementWeights.Keys.ToList();

		return Enumerable.Empty<string>();
	}

	/// <summary>The spell level an ascension is offered at.</summary>
	public const int AscensionSpellLevel = 8;

	/// <summary>
	/// The element requirement this branch has not met yet, phrased for the player, or null if it
	/// has. The single authority: the level 8 screen stamps it across a locked card and the
	/// ascension browser lists it, and neither has its own copy of the rule.
	/// </summary>
	private static string DescribeUnmetElementGate(SpellEvolutionOption evo, SpellData spell,
		Dictionary<Element, int> currentCounts)
	{
		AscensionRequirement gate = EvaluateElementGate(evo, spell, currentCounts);
		if (gate == null || gate.Met)
			return null;

		return $"Requires {AscensionElementRequirement} {gate.Element} ({gate.Have}/{AscensionElementRequirement})";
	}

	/// <summary>
	/// The element gate on one branch, measured: the element the player is deepest in among those
	/// that would satisfy it, and how deep. Null when the branch has nothing to be deep in. The rule
	/// lives here and only here - DescribeUnmetElementGate words it, the browser draws it.
	/// </summary>
	private static AscensionRequirement EvaluateElementGate(SpellEvolutionOption evo, SpellData spell,
		Dictionary<Element, int> currentCounts)
	{
		var gates = GetAscensionGateElements(evo, spell).ToList();
		if (gates.Count == 0)
			return null; // Nothing to be deep in; leave it available.

		var requirement = new AscensionRequirement
		{
			Kind = AscensionRequirementKind.Element,
			Need = AscensionElementRequirement,
		};
		int best = 0;
		foreach (string elementName in gates)
		{
			if (!Enum.TryParse<Element>(elementName, true, out Element element))
				continue;

			requirement.AnyOf.Add(element);
			requirement.Element ??= element;
			int held = currentCounts.TryGetValue(element, out int n) ? n : 0;
			if (held > best)
			{
				best = held;
				requirement.Element = element;
			}
		}
		if (requirement.AnyOf.Count == 0)
			return null;

		requirement.Have = best;
		return requirement;
	}

	/// <summary>
	/// Every level 8 branch a spell has, with whatever still stands between the player and it.
	/// </summary>
	/// <remarks>
	/// Read by the ascension browser on the level-up screen. It reports rather than decides: an
	/// entry with no unmet requirements is one the player will be offered the next time that spell
	/// reaches level 8, not one they can take now.
	/// </remarks>
	public List<AscensionInfo> BuildAscensionPreview(SpellData spell)
	{
		var previews = new List<AscensionInfo>();
		if (spell == null)
			return previews;

		SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
		if (spell.Level8Options == null || spell.Level8Options.Count == 0)
			return previews;

		Dictionary<Element, int> counts = GetElementInstanceCounts();

		foreach (SpellEvolutionOption evo in spell.Level8Options)
		{
			if (evo == null)
				continue;

			var info = new AscensionInfo
			{
				Id = evo.Id,
				DisplayName = evo.DisplayName,
				Description = evo.Description,
				Icon = evo.Icon ?? spell.Icon,
			};

			// Two gates, and the browser has to name both or it answers half the question. A
			// player at 4 Fire with a level 3 Fireball is not close to an ascension, and a screen
			// that showed only the element would tell them they were.
			int ready = CardLevelCap(spell);
			info.Requirements.Add(new AscensionRequirement
			{
				Kind = AscensionRequirementKind.SpellLevel,
				Have = spell.CurrentLevel,
				Need = ready,
			});
			if (spell.CurrentLevel < ready)
			{
				info.UnmetRequirements.Add(
					$"Spell level {ready} ({spell.CurrentLevel}/{ready})");
			}

			// Measured once and worded from the measurement, so the checklist the browser draws and
			// the sentence the locked level 8 card is stamped with cannot disagree.
			AscensionRequirement elementGate = EvaluateElementGate(evo, spell, counts);
			if (elementGate != null)
			{
				info.Requirements.Add(elementGate);
				string gate = DescribeUnmetElementGate(evo, spell, counts);
				if (gate != null)
					info.UnmetRequirements.Add(gate);
			}

			info.Requirements.Add(new AscensionRequirement
			{
				Kind = AscensionRequirementKind.PlayerLevel,
				Have = CurrentLevel,
				Need = AscensionMinimumPlayerLevel,
			});
			if (CurrentLevel < AscensionMinimumPlayerLevel)
				info.UnmetRequirements.Add($"Player level {AscensionMinimumPlayerLevel} ({CurrentLevel}/{AscensionMinimumPlayerLevel})");

			previews.Add(info);
		}

		return previews;
	}

	// Fills in the lock reasons. Nothing is removed from the list - a locked ascension is shown
	// greyed with what it wants, so the requirement teaches itself.
	private void MarkUnearnedAscensions(LevelUpOption option, SpellData spell,
		Dictionary<Element, int> currentCounts)
	{
		foreach (SpellEvolutionOption evo in option.EvolutionChoices)
		{
			if (evo == null)
				continue;

			var gates = GetAscensionGateElements(evo, spell).ToList();
			if (gates.Count == 0)
				continue; // Nothing to be deep in; leave it available.

			string unmet = DescribeUnmetElementGate(evo, spell, currentCounts);
			if (unmet == null)
				continue;

			option.EvolutionLockReason[evo.Id] = unmet;
		}

		// If the player has earned none of them, this is not a milestone - it is an ordinary
		// level 8. Presenting a screen where every card is greyed out would be a dead end.
		if (option.EvolutionLockReason.Count >= option.EvolutionChoices.Count)
		{
			option.IsEvolutionMilestone = false;
			option.EvolutionChoices = new List<SpellEvolutionOption>();
			option.EvolutionLockReason.Clear();
		}
	}

	private static string BuildSpellStatSummary(SpellData spell)
	{
		if (spell == null)
			return string.Empty;

		var parts = new List<string>();

		int damage = spell.GetDamageAtLevel(1);
		if (damage > 0)
			parts.Add($"DMG {damage}");

		float cooldown = spell.GetCooldownAtLevel(1);
		if (cooldown > 0.01f)
			parts.Add($"CD {cooldown:0.0}s");

		float range = spell.GetRangeAtLevel(1);
		if (range > 0.5f)
			parts.Add($"RNG {range:0}");

		int projectiles = spell.GetProjectileCountAtLevel(1);
		if (projectiles > 1)
			parts.Add(IsTentacleSpell(spell) ? $"Tentacles {projectiles}" : $"x{projectiles}");

		return string.Join("   ", parts);
	}

	private static string BuildSpellUpgradeSummary(SpellData spell, int nextLevel)
	{
		if (spell == null || spell.LevelUpgrades == null)
			return string.Empty;

		var parts = new List<string>();
		foreach (SpellLevelUpgrade upgrade in spell.LevelUpgrades.Where(u => u != null && u.Level == nextLevel))
		{
			if (upgrade.DamageBonus != 0)
			{
				// Aegis Ward stores its shield strength in the damage field so it can ride the same
				// level-up pipeline as everything else. The card has to tell the truth about it.
				bool isShield = spell.Id.Equals("aegis_ward", StringComparison.OrdinalIgnoreCase);
				parts.Add($"{(isShield ? "Shield" : "Damage")} {FormatSigned(upgrade.DamageBonus)}");
			}
			if (MathF.Abs(upgrade.CooldownBonus) > 0.001f)
				parts.Add($"Cooldown {FormatSigned(upgrade.CooldownBonus)}s");
			if (upgrade.ProjectileCountBonus != 0)
				parts.Add($"{(IsTentacleSpell(spell) ? "Tentacles" : "Projectiles")} {FormatSigned(upgrade.ProjectileCountBonus)}");
			if (upgrade.ChainArcBonus != 0)
				parts.Add($"Chain arcs {FormatSigned(upgrade.ChainArcBonus)}");
			if (upgrade.ChainBranchBonus != 0)
				parts.Add($"Branch arcs {FormatSigned(upgrade.ChainBranchBonus)}");
			if (MathF.Abs(upgrade.ChainChanceBonus) > 0.001f)
				parts.Add($"Chain chance {FormatSigned(upgrade.ChainChanceBonus * 100f)}%");
			if (upgrade.PoisonTickBonus != 0)
				parts.Add($"Poison tick {FormatSigned(upgrade.PoisonTickBonus)}");
			if (MathF.Abs(upgrade.RangeBonus) > 0.001f)
				parts.Add($"Range {FormatSigned(upgrade.RangeBonus)}");
			if (upgrade.Effect != SpellEffect.None && MathF.Abs(upgrade.EffectValue) > 0.001f)
				parts.Add(FormatSpellEffectUpgrade(upgrade.Effect, upgrade.EffectValue));
		}

		if (parts.Count > 0)
			return string.Join("   ", parts);

		if (spell != null && spell.Id.Equals("tidal_barrier", StringComparison.OrdinalIgnoreCase))
			return "Increases pulse knockback and slow potency.";

		return "Improves this spell's class-specific stats.";
	}

	// Black Tentacles' projectile count is its tentacles, and the card says so (StatIcons gives the
	// word the projectiles glyph).
	private static bool IsTentacleSpell(SpellData spell) =>
		spell != null && spell.Id.Equals("black_tentacles", StringComparison.OrdinalIgnoreCase);

	private static string FormatSigned(int value) => value > 0 ? $"+{value}" : value.ToString();
	private static string FormatSigned(float value) => value > 0 ? $"+{value:0.##}" : value.ToString("0.##");

	private static string FormatSpellEffectUpgrade(SpellEffect effect, float value)
	{
		return effect switch
		{
			SpellEffect.Pierce => $"Pierce {FormatSigned(Mathf.RoundToInt(value))}",
			SpellEffect.Chain => $"Chain jumps {FormatSigned(Mathf.RoundToInt(value))}",
			SpellEffect.Freeze => $"Freeze {FormatSigned(value)}s",
			SpellEffect.Burn => $"Burn {FormatSigned(value)}",
			SpellEffect.Knockback => $"Knockback {FormatSigned(value)}",
			SpellEffect.CritChance => $"Crit chance {FormatSigned(value * 100f)}%",
			SpellEffect.AreaSize => $"Area {FormatSigned(value)}",
			SpellEffect.ProjectileSpeed => $"Projectile speed {FormatSigned(value)}",
			SpellEffect.SlowPower => $"Slow strength {FormatSigned(value * 100f)}%",
			SpellEffect.SlowDuration => $"Slow duration {FormatSigned(value)}s",
			SpellEffect.RootDuration => $"Root duration {FormatSigned(value)}s",
			SpellEffect.DotDamage => $"DoT per tick {FormatSigned(Mathf.RoundToInt(value))}",
			SpellEffect.ZoneDuration => $"Zone duration {FormatSigned(value)}s",
			_ => $"{effect} {FormatSigned(value)}"
		};
	}

	// There used to be a full-loadout skew here (new spells x0.25, upgrades x2) that kept swaps on
	// offer but rare. A full book is offered no new spells now, so there is nothing left to skew.
	private float GetOfferWeight(LevelUpOption option)
	{
		float baseWeight = option.IsNewUnlock ? NewUnlockOfferWeight : UpgradeOfferWeight;
		return MathF.Max(0.01f, baseWeight);
	}

	// Rescales the two groups of candidates so upgrades to owned spells take a fixed share of the
	// total draw weight, regardless of how many candidates are in each group.
	//
	// Without this, per-card weights are meaningless in practice: a player with 2 spells equipped
	// and ~30 unlocked has 2 upgrade cards against ~30 new-spell cards, so even at a 3:1 per-card
	// advantage the upgrades hold only 6 of 36 weight - about 17% - and the level-up screen is
	// almost always three spells the player does not own. Group normalization makes the intended
	// ratio hold at any catalog size, and per-card weights still order candidates within a group.
	private void NormalizeOfferWeightsByGroup(List<LevelUpOption> candidates, List<float> weights)
	{
		if (candidates == null || weights == null || candidates.Count != weights.Count)
			return;

		float upgradeMass = 0f;
		float newUnlockMass = 0f;
		for (int i = 0; i < candidates.Count; i++)
		{
			float w = MathF.Max(0f, weights[i]);
			if (candidates[i].IsNewUnlock)
				newUnlockMass += w;
			else
				upgradeMass += w;
		}

		// With only one group present there is nothing to balance; leave the weights alone.
		if (upgradeMass <= 0f || newUnlockMass <= 0f)
			return;

		float upgradeShare = Mathf.Clamp(UpgradeOfferShare, 0f, 1f);
		float upgradeScale = upgradeShare / upgradeMass;
		float newUnlockScale = (1f - upgradeShare) / newUnlockMass;

		for (int i = 0; i < candidates.Count; i++)
		{
			weights[i] = MathF.Max(0f, weights[i]) * (candidates[i].IsNewUnlock ? newUnlockScale : upgradeScale);
		}
	}

	private void RemoveBanned(List<LevelUpOption> options, List<float> weights)
	{
		if (bannedSpellIds.Count == 0)
			return;
		for (int i = options.Count - 1; i >= 0; i--)
		{
			if (bannedSpellIds.Contains(options[i].SpellId))
			{
				options.RemoveAt(i);
				weights.RemoveAt(i);
			}
		}
	}

	private int PickWeightedIndex(List<float> weights)
	{
		if (weights == null || weights.Count == 0)
			return 0;

		float totalWeight = 0.0f;
		for (int i = 0; i < weights.Count; i++)
		{
			totalWeight += MathF.Max(0.0f, weights[i]);
		}

		if (totalWeight <= 0.0f)
			return levelUpRng.RandiRange(0, weights.Count - 1);

		float roll = levelUpRng.RandfRange(0.0f, totalWeight);
		float cumulative = 0.0f;
		for (int i = 0; i < weights.Count; i++)
		{
			cumulative += MathF.Max(0.0f, weights[i]);
			if (roll <= cumulative)
				return i;
		}

		return weights.Count - 1;
	}

	private SpellData ResolveSpellTemplate(string selectionId)
	{
		if (string.IsNullOrWhiteSpace(selectionId))
			return null;

		selectionId = selectionId.Trim();

		if (spellCatalog.TryGetValue(selectionId.ToLowerInvariant(), out var byId))
			return byId;

		string normalized = selectionId.Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
		foreach (var spell in spellCatalog.Values)
		{
			if (spell == null) continue;
			string spellIdNormalized = spell.Id.Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
			string spellNameNormalized = spell.Name.Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
			if (normalized == spellIdNormalized || normalized == spellNameNormalized)
				return spell;
		}

		if (normalized == "magicmissile") return spellCatalog.GetValueOrDefault("magic_missile");
		if (normalized == "arcaneexplosion") return spellCatalog.GetValueOrDefault("arcane_explosion");
		if (normalized == "spiritualweapon") return spellCatalog.GetValueOrDefault("spiritual_weapon");

		return null;
	}

	private void RefreshPersistentSpellInstance(SpellData spell)
	{
		if (spell == null) return;

		if (spell.Id.Equals("arcane_explosion", StringComparison.OrdinalIgnoreCase))
		{
			var existing = GetChildren().OfType<ArcaneExplosion>().FirstOrDefault();
			if (existing != null)
			{
				existing.SpellData = spell;
				existing.SetSpellLevel(spell.CurrentLevel);
			}
		}

		if (spell.Id.Equals("spiritual_weapon", StringComparison.OrdinalIgnoreCase))
		{
			var existing = GetChildren().OfType<SpiritualWeapon>().FirstOrDefault();
			if (existing != null)
			{
				existing.SpellData = spell;
				existing.SetSpellLevel(spell.CurrentLevel);
			}
		}

	}

	// Contact damage is owned by the player, not the enemy, so the swing animation has to be
	// driven from here. Duck-typed rather than cast to Enemy: not every thing in the "enemies"
	// group is an Enemy, and one that cannot swing simply does not.
	private static void PlayEnemyAttackAnimation(Node enemy)
	{
		if (enemy != null && IsInstanceValid(enemy) && enemy.HasMethod("PlayAttackAnimation"))
			enemy.Call("PlayAttackAnimation");
	}

	// Every ordinary enemy leaves ContactDamage at 1, so this reads exactly as the old flat
	// TakeDamage(1) did. A boss raises it, which is the only way a slow melee threat can be
	// genuinely dangerous to stand next to when the touch cooldown is a fifth of a second.
	private static int GetEnemyContactDamage(Node enemy)
	{
		if (enemy is Enemy typedEnemy && IsInstanceValid(typedEnemy))
			return Math.Max(1, typedEnemy.ContactDamage);

		return 1;
	}

	private void OnBodyEntered(Node body)
	{
		if (body.IsInGroup("enemies"))
		{
			overlappingEnemies.Add(body);
			// Optionally, apply damage immediately
			if (!enemyDamageCooldowns.ContainsKey(body) || enemyDamageCooldowns[body] >= DamageCooldownSeconds)
			{
				TakeDamage(GetEnemyContactDamage(body));
				PlayEnemyAttackAnimation(body);
				enemyDamageCooldowns[body] = 0f;
			}
		}
	}

	// No group check here on purpose. A dying enemy has already left the "enemies" group by the
	// time its exit reaches us, and guarding on membership meant those bodies were never removed.
	// Removing a node that was never in the set is a no-op, so the guard bought nothing.
	private void OnBodyExited(Node body)
	{
		overlappingEnemies.Remove(body);
		enemyDamageCooldowns.Remove(body);
	}

	// Drops anything from the contact set that can no longer legitimately hit the player: freed
	// nodes, corpses mid-death-animation, and anything that has left the "enemies" group. Uses a
	// reusable buffer because a HashSet cannot be modified while it is being enumerated, and this
	// runs every physics frame.
	private readonly List<Node> departedEnemiesBuffer = new List<Node>();

	private void PruneDepartedEnemies()
	{
		departedEnemiesBuffer.Clear();

		foreach (var enemy in overlappingEnemies)
		{
			if (!IsInstanceValid(enemy) || !enemy.IsInGroup("enemies") || (enemy is Enemy typedEnemy && typedEnemy.IsDying))
				departedEnemiesBuffer.Add(enemy);
		}

		foreach (var departed in departedEnemiesBuffer)
		{
			overlappingEnemies.Remove(departed);
			enemyDamageCooldowns.Remove(departed);
		}
	}

	/// <summary>
	/// Returns a copy of the owned chest items for UI/HUD display.
	/// </summary>
	public IReadOnlyList<string> GetOwnedChestItems()
	{
		return ownedChestItems.ToList();
	}

	/// <summary>
	/// Returns a copy of the completed chest sets for HUD display.
	/// </summary>
	public IReadOnlyList<string> GetCompletedChestSets()
	{
		return completedChestSets.ToList();
	}
}
