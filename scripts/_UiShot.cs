using Godot;
using System.Linq;
using WizardSurvivors.scripts;

// A screenshot harness for menu work, and the reason menu work can be judged by eye at all.
//
// Earlier attempts in this project to capture the game came back as a flat clear colour from both
// the window backbuffer and a SubViewport, which is why the level-up rebuild was done against
// geometry dumps instead of pictures. The missing piece was not the capture API, it was WHEN:
// `RenderingServer.FramePostDraw` fires after the GPU has finished the frame, and that is the only
// point at which the viewport texture holds anything. _Ready and _Process both read the buffer
// before the draw, which is precisely the clear colour that was seen.
//
// Run:
//   "$GODOT" --path . scenes/_UiShot.tscn -- --scene=res://scenes/MainMenu.tscn --shot=main-menu
//
// Also dumps a GEOMETRY REPORT beside the image: every Control with its rect and font size, plus
// any control wider than the viewport and any font below the Micro floor. A picture shows that a
// screen looks wrong; the report says by how many pixels.
public partial class _UiShot : Node
{
	[Export] public string ScenePath { get; set; } = "res://scenes/MainMenu.tscn";
	[Export] public string ShotName { get; set; } = "shot";
	[Export] public int WarmupFrames { get; set; } = 10;

	// A sub-panel to open before shooting, by the NAME of the button that opens it. Half the menus
	// in this game are panels inside MainMenu that start hidden, and a hidden container is not laid
	// out - measuring one reports its combined minimum size rather than the width it would really
	// get, which reads as a false overflow. Pressing the real button runs the real code path, so
	// what gets measured is what a player would see.
	[Export] public string PressButton { get; set; } = "";

	// A button pressed AFTER the warmup rather than in _Ready, for anything that does not exist
	// until the run has been going a while. PressButton cannot reach the level-up menu at all: it
	// fires the moment the scene is instanced, and at that point the menu has not been built.
	[Export] public string PressLateButton { get; set; } = "";

	// --press-late=A,B,...: several late presses, one per warmup, in order - the shop's button and
	// then a Buy inside it, so a purchase (and, with --burst, its animation) can be photographed.
	private string[] pressLateQueue = System.Array.Empty<string>();
	private int pressLateIndex;

	// --focus-late=NAME: after every late press, the selection is put on the control of that name
	// (a trailing * matches by prefix), so a focus ring can be photographed where a stick would put
	// it rather than only on the screen's first stop.
	private string focusLate;
	private bool focusLateDone;

	// --demo-shop: a fresh in-memory save with a known purse and a spread of Arcane Upgrade levels -
	// one row at its maximum, some affordable, some not, one spell owned - so the shop's before and
	// after photographs show the same states (.ai/level-up-screen.md, "Arcane Upgrades").
	private bool demoShop;

	// --pad=down,down,a,b: real pad presses after the late presses, one per warmup, injected through
	// Input.ParseInputEvent (the door a pad comes in by, so the input map, MenuNavigator and the
	// viewport's focus routing are all on the path). Each step logs where the selection is before it
	// is sent, and the end logs where it came to rest - a stick walking the shop, A buying, B backing
	// out - and the shutter then photographs the ring where the stick left it.
	// Steps: up, down, left, right (the D-pad), a, b, y, and release.
	// release lets go of the bot's steering. A level-up or the pause screen opens under whatever the
	// bot was holding, and MenuNavigator ignores the pad until a direction held since the screen
	// opened is let go (MenuNavigator.NoteOpening) - so over a run, start with release to walk the
	// screen, or leave it out to photograph that lock holding the selection still.
	private string[] padScript = System.Array.Empty<string>();
	private int padStep;

	// Stops the bot taking the first option, so a level-up can be photographed instead of skipped
	// past. The dismissal exists because a gameplay shot past the first minute is otherwise always
	// the card screen over a frozen arena - this is the other half of that switch.
	[Export] public bool KeepLevelUp { get; set; } = false;

	// Grants exactly one level halfway through the warmup. The XP curve went linear and the camera
	// went wide (.ai/gameplay-design.md), and this bot dodges rather than collects, so a run no longer
	// levels inside any warmup worth waiting for. Pair with --keep-levelup to photograph the card screen.
	[Export] public bool ForceLevelUp { get; set; } = false;
	private bool levelForced;

	// Puts a boon on the forced level-up (Player.ForceBoonOnNextOffer). Boons are rare now - about
	// one level-up in five - so without this the boon card could not be photographed on demand.
	// Implies --force-levelup and --keep-levelup.
	[Export] public bool ForceBoon { get; set; } = false;
	// --force-boon=ID: the same, and the boon dealt is that one (Player.ForcedBoonId) - Taproot's
	// card, not whichever boon the roll favoured.
	private string forcedBoonId;

	// The stand-still tells (StanceGlow at the feet). From a quarter of the way in the player stands
	// still, cannot die, and has the Brazier's heat and the still clock pinned every frame
	// (Player.DebugSetStance), so the embers and Taproot's ring are photographed at a known state:
	//   --heat=X       : the Brazier's heat, 0 to 1 (needs the set: --give-relics=ember_flask,inferno_core,obsidian_heart)
	//   --still-for=S  : seconds stood still, for Taproot's ring (needs --grant-boons=taproot): under
	//                    1.5 draws it half-built, 1.5 and over rooted
	private float? pinHeat;
	private float? pinStillFor;
	// --focus-boon: also moves the selection onto the boon card before the shutter, so the
	// controller focus ring can be judged on the plaque rather than on a page beside it.
	[Export] public bool FocusBoon { get; set; } = false;
	private bool boonFocused;
	// --mixed-deal: once the forced level-up is up, re-deal it as one card of each family the hand
	// can hold - a new spell, an upgrade to a held spell, a boon - through the menu's own SetOptions,
	// so every family can be photographed side by side (.ai/level-up-screen.md, "card families").
	// The deal is drawn from Player.GetAuguryCandidates, the full list a level-up chooses from.
	// --focus-card=N: then move the selection onto card N (0-based), to judge that family's focus.
	private bool mixedDeal;
	private bool mixedDealt;
	// --with-charges / --with-strip: the forced level-up with the charge row (a Ban, a Save and an
	// Augury) and with an Ascension strip button for the first held spell - the two extra rows that
	// used to push a 1280x720 hand off the foot of the screen (LevelUpMenu.WideCardsTight).
	private bool withCharges;
	private bool withStrip;
	private bool levelExtrasShown;
	// --tall-deal=N: the same re-deal, as the N cards with the most to say - the most stat or delta
	// lines, then the longest name - so the tightest hand a 1280x720 PC can be dealt is photographed
	// on demand, and N=4 is the four-card hand (Playtest Mode deals four).
	private int tallDeal;
	private int focusCard = -1;
	private bool cardFocused;

	// Opens the chest-item picker halfway through the warmup, the same way picking up a chest does
	// (Node2DGame.OpenChestSelectionMenu), so it can be photographed without waiting for a chest.
	[Export] public bool OpenChest { get; set; } = false;
	// --drop-chest=DX,DY: a chest dropped at that offset from the player once the run settles, to
	// photograph ChestPointer's off-screen bubble. Repeatable.
	private readonly System.Collections.Generic.List<Vector2> chestDrops = new();
	private bool chestsDropped;
	private bool chestOpened;
	// --menu-queue-probe: drives Node2DGame's run menus two at a time (two chests in one step, a
	// level-up and a chest, two level-ups) and prints PASS or FAIL for each, then quits with the
	// count of failures as its exit code. Run it headless; it photographs nothing.
	private bool menuQueueProbe;
	private bool menuQueueProbeStarted;
	// --secret-probe (issue #20): walks a lap round the nearest flower pen, takes the seed the bloom
	// drops to the First Tree and plants it, checking each step (PASS/FAIL, exit code = failures) and
	// saving a picture of each as secret-N.png. Writes are off: the save it checks is in memory.
	private bool secretProbe;
	private bool secretProbeStarted;
	// --pen-camp: stands the wizard (immortal, still) inside the nearest flower pen, just in from its
	// east rail, and for 30 s counts the enemies outside the fence that have stopped moving against it
	// (2026-10-28: "standing in the fence near the big flower makes an easy way for players to get
	// enemies stuck"). Prints PENCAMP lines and saves pencamp.png; writes are off.
	private bool penCamp;
	private bool penCampStarted;
	// --landmark-census=R: counts the chunks within R of the start that would hold a flower pen, prints
	// a LANDMARKS line and quits. Placement only; nothing is painted.
	private int landmarkCensus;
	// --crowd=N: N of the chapter's enemies, forty times as tough, set down 150-280 px round the wizard
	// a third of the way through the warmup, so a spell's landing can be photographed on screen.
	private int crowd;
	private bool crowdPlaced;
	// --give-relics=id,id: relics handed to the player before --open-chest, so the picker shows
	// Full Set Enchantment progress rather than a fresh run's row of empty sets.
	// --chest-offer=id,id,id: the relics the chest offers, in place of the random roll, so a before
	// and an after photograph of the chest screen show the same three cards.
	private string[] giveRelics = System.Array.Empty<string>();
	private string[] chestOffer = System.Array.Empty<string>();
	private bool relicsGiven;

	// --fill-book: a quarter of the way in, every empty slot in the spell book is filled with spells
	// in catalog order, so a full book can be photographed - the level-up that offers no new spell,
	// the Scribe's Penknife's picker, the seventh slot. --give-relics is handed over first, so with
	// blank_folio in it the book fills to seven.
	// --max-book: --fill-book, then every spell raised to its card cap and the boon slots filled:
	// the level-up with nothing left in it, which deals the Marginalia card.
	private bool fillBook;
	private bool maxBook;
	private bool bookFilled;
	// --hud-milestones: after --fill-book, one held spell is taken to its Apotheosis (through the real
	// pick, so its field attaches), one to its level 8 Ascension and one to its level 4 mutation, so
	// the loadout strip's three milestone rims (LoadoutHud) can be photographed side by side.
	private bool hudMilestones;
	// --cut-spell=id: a third of the way in (after --fill-book), opens a chest and cuts that spell
	// with the Scribe's Penknife through the picker's own signal, so the claim path runs end to end
	// and a --force-levelup or --open-pause after it shows the book one spell lighter.
	private string cutSpellId;
	private bool spellCut;

	// Kills the player halfway through the warmup, so the game over screen can be photographed.
	// Several hits, because a run can carry an extra life that revives at half health.
	[Export] public bool ForceDeath { get; set; } = false;
	private bool deathForced;
	// --force-victory: the other ending, through the real path - Node2DGame.FinishRunAndReward with
	// Victory and the chapter's boss, halfway through the warmup - so the boss bonus, the chapter it
	// opens and the gold headline are photographed as a win would pay them (writes are off).
	private bool forceVictory;
	// --rush-steps=N (with --boss-rush): N steps of the rush from a quarter of the warmup, one every
	// RushStepFrames - an odd count ends on a boss's arrival, an even one just after a fall
	// (Node2DGame.DebugAdvanceBossRush).
	private int rushSteps;
	private int rushStepsDone;
	private int rushNextStepFrame = -1;
	private const int RushStepFrames = 40;
	// --demo-unlocks: once the results screen is up, one card of every kind is added to what the run
	// really earned (a wizard, a spell, a boon, a chapter, an ascension path, an Unlocks entry with
	// Arcane Energy) and the screen rebuilt, so every reward card can be judged on one screen. The
	// in-memory save is not touched; the cards are added to the screen's report only.
	private bool demoUnlocks;
	private bool demoUnlocksAdded;

	// Opens the level 4 mutation page for the starting spell over a forced level-up, the page a
	// player reaches by picking a level 4 card. It had never been photographed (.ai/level-up-screen.md)
	// and its ink was reported unreadable. Reached through reflection because the page is private to
	// LevelUpMenu and its option type is not a Variant - this is a debug harness, not an API.
	[Export] public bool ShowMutation { get; set; } = false;
	private bool mutationShown;
	// --show-ascension: the same page at milestone 8, the Ascension choice. Same reflection route,
	// so the two evolution pages can be compared side by side.
	private int mutationMilestone = 4;

	// --open-pause: opens the in-run escape menu halfway through the warmup, the same toggle the
	// pause action runs (Node2DGame.ToggleEscapeMenu, private, so reached through reflection).
	[Export] public bool OpenPause { get; set; } = false;
	private bool pauseOpened;
	// --pad-y: inject a pad Y press after the warmup (see the late press). Once it is sent, _Input
	// lets Y through; it otherwise drops every real button press.
	private bool padY;
	private bool padYSent;

	// The Report a bug screen (ReportBugScreen). Open it the way a player does - --press-late=
	// ReportBugButton on the main menu (with --open-menu), or --open-pause --press-late=
	// EscapeReportBug in a run - and then:
	//   --report-name=NAME  : typed into the name field once the screen is up.
	//   --report-text=TEXT  : typed into the description (a literal \n is a new line).
	//   --report-send       : presses Send, and the shutter waits for the server's answer (up to
	//                         30 s), so the photograph is the Done or the failure state.
	//   --report-back       : presses Back instead, and logs where focus went and whether the tree
	//                         is still paused - from the pause screen it must be EscapeRoot and yes.
	// Point it at a local server with WIZARDSURVIVORS_INTAKE_URL; never at the real one.
	private bool reportBack;
	private string reportName;
	private string reportText;
	private bool reportSend;
	private bool reportFilled;
	private ReportBugScreen reportScreen;
	private int reportWait;

	// Below this is the floor ResponsiveLayout.TextRole.Micro sets, so anything under it got its
	// size from a hardcoded call site rather than from the scale.
	private const int MinReadableFontSize = 16;

	// Exploration (.ai/side-events.md, "Finding the event"). The event and the wayfinder are placed
	// from the terrain seed, so pair these with --terrain-seed=N for a repeatable picture.
	//   --goto=wayfinder|event|site:N|hazard : like --teleport, to that placed thing (site N of the
	//                                   event; the stage hazard nearest the player),
	//                                   standing --goto-offset=DX,DY from it (default 0,170: just
	//                                   south, so the thing is on screen above the player).
	//   --take-wayfinder               : the chapter's wayfinder is taken a third of the way in, through
	//                                   its real path (WayfinderPickup.Take), so the arrows are up.
	//   --arrive                       : the event begins where it stands, as if the player had walked in.
	//   --explore-log is read by SideEventDirector itself and prints the placement.
	//   --force-hint                   : the chapter's hint (StageHintDirector) plays fxLead frames
	//                                   before the shutter instead of at pace 300 s, so its caption
	//                                   can be photographed. Every other rule still holds - it needs a
	//                                   save that lacks the discovery, so pair it with --fresh-save
	//                                   (in memory; writes are off) unless the real one lacks it too.
	private string gotoTarget;
	private Vector2 gotoOffset = new Vector2(0f, 170f);
	private bool forceHint;
	private bool hintForced;
	private bool takeWayfinder;
	private bool wayfinderTaken;
	private bool arriveEvent;
	private bool eventArrived;

	// --teleport=X,Y: moves the player to that world position a third of the way through the warmup,
	// so ground far from the start can be photographed - the endless terrain (TerrainStreamer) is
	// only worth checking by eye where it was streamed in, not round the origin it loads with.
	// Give it a few hundred --frames: the streamer paints one step per frame, nearest chunk first.
	private Vector2? teleportTo;
	private float cameraZoom;
	private bool teleported;

	// --hold: after the teleport the player stands on the target and stays there - re-placed every
	// frame and never killed - so a before and an after photograph on the same --terrain-seed frame
	// exactly the same ground. Without it the kiting bot wanders a few hundred pixels and two shots
	// of "the same pond" cannot be cropped side by side.
	private bool hold;

	// --clear-enemies: frees every enemy each frame, so the ground can be photographed bare. Leave it
	// off for the shot that asks whether the ground competes with the swarm.
	private bool clearEnemies;

	// --shore=up|down|left|right: finds the nearest straight shoreline with water that way, stands
	// the player three tiles back from it on land, and walks him into it for the rest of the
	// warmup instead of kiting. The photograph then shows where his feet stopped, and the log says
	// by how many pixels they cleared the water's collision line (positive) or overhung it
	// (negative), measured off the drawn pixels of his boots rather than off any collider. Needs a
	// stage with water (3, the Swamp, has the most) and a couple of hundred --frames. On the maze (stage 1) it
	// finds a wall instead, which the feet meet the same way.
	private Vector2I? shoreDirection;
	// --shore-fence: the same walk, into the nearest straight run of a flower pen's fence instead of
	// water (TerrainStreamer.FlagFence), from outside or from inside the garden - whichever is
	// nearer. The fence is on the ground's layer like water, so the same probe measures it.
	private bool shoreFence;
	private Vector2I shoreCell;
	private bool shorePlaced;

	// --shove: a third of the way in, clears the arena, stands a block of twelve skeletons (three
	// deep, four wide) just east of the player - planted, at Speed 0, so the picture is of HIM moving
	// them - and walks him east through it for the rest of the warmup, immortal. Pair with --burst to
	// see the block part (Enemy.ComputePlayerShove): e.g. --frames=150 --burst=6,6.
	private bool shove;
	private bool shovePlaced;

	// --ghost: clears the arena and stands the Sketchbook's ghost (Elim's Boo, scripts/BooEnemy.cs)
	// beside the player, who stands still and cannot die, so the miniboss can be judged against him
	// at the game scale. --ghost-wail spawns it close to the shutter with its wail primed, so
	// the shot lands mid wind-up: the attack frames and the ring together. Pair with --stage=8.
	private bool ghost;
	private bool ghostWail;
	// --ghost-phase: placed 420 px off, beyond the phase's 260 px floor, so after its first 5 s it
	// crosses to the player. --frames=480 catches it mid-crossing, thinned, over its mark.
	private bool ghostPhase;
	private Enemy ghostEnemy;
	private readonly System.Collections.Generic.List<Enemy> shoveBlock = new();

	// --tender[=strikes|oil|dark|rekindle]: the Blighted Swamp's lantern-tender (LanternTenderEnemy),
	// called through the event's own path - the event begun where it stands, every lantern snuffed
	// (BogLanternsEvent.DebugSnuffAll), the tender sited and spawned and handed the lanterns by the
	// event's Tick - then stood --tender-offset=DX,DY from the player (default 260,-60), held still,
	// never killed, with everything else cleared off the field. The player stands still and cannot
	// die. With a move named, that attack is begun fxLead frames before the shutter
	// (LanternTenderEnemy.DebugBegin), so --fx-lead sets how far into its tell the picture is:
	// --fx-lead=40 is mid-flight for the strikes, past halfway for the dark. Pair with --stage=3.
	// --tender-crowd keeps the swarm, to judge the tender against it. The rekindle needs a snuffed
	// lantern within reach of the tender, so add --goto=site:3: the call waits ten frames after it.
	private bool tenderShot;
	private LanternTenderEnemy.Move? tenderMove;
	private Vector2 tenderOffset = new Vector2(260f, -60f);
	private bool tenderCrowd;
	private bool tenderCalled;
	private bool tenderPlaced;
	private bool tenderMoveFired;
	private LanternTenderEnemy tenderEnemy;

	// --scroll=N: every visible ScrollContainer is held at N units down (a large N is the end), from
	// the late press onward, so the bottom of a long list - the Spell Index's "Still to find", Unlocks'
	// "Done" - can be photographed and not only its first screen.
	private int? scrollTo;

	private void ApplyScroll(Node node)
	{
		if (node is ScrollContainer scroll && scroll.IsVisibleInTree())
			scroll.ScrollVertical = scrollTo ?? 0;
		foreach (Node child in node.GetChildren())
			ApplyScroll(child);
	}

	// --status-showcase: a third of the way in, takes eight enemies, stands them in two rows round
	// the player and holds them there, each wearing one status (and two of them several, to show the
	// two-mark cap), with every other enemy cleared away. Writes user://<shot>.status.json beside the
	// picture with where each one landed on screen, which tools/art/status_sheet.py crops from.
	// --status-boss adds Elderbark above the player, slowed and vulnerable, for the boss scale.
	// For a whole crowd under status, use Node2DGame's --status-all (and --stress-swarm) instead.
	private bool statusShowcase;
	private bool statusBoss;
	private bool statusCrowd;
	private Vector2 showcaseCentre;
	private readonly System.Collections.Generic.List<Enemy> showcase = new();
	private Enemy showcaseBoss;

	private static readonly string[] ShowcaseNames =
		{ "slow", "freeze", "poison", "vulnerable", "weaken", "wither", "slow+poison", "four (cap 2)" };
	private static readonly Vector2[] ShowcaseSlots =
	{
		new(-330f, -150f), new(-110f, -150f), new(110f, -150f), new(330f, -150f),
		new(-330f, 170f), new(-110f, 170f), new(110f, 170f), new(330f, 170f),
	};

	// The juice pass (speed potion, side event win, Bramble Seed). Each fires fxLead frames before
	// the shutter, so a --burst that follows shows the moment from its first frame. All three set
	// SaveManager.WritesDisabled: a won side event GRANTS its discovery and saves on the spot.
	//   --drop-potion / --drop-health : the pickup dropped on the player, taken through its real path.
	//   --win-event                   : starts the chapter's side event a third of the way in, holds
	//                                   the player on its site, then wins it (Succeed, by reflection).
	//   --event-chest                 : with --win-event, pays the chest even on a save that lacks
	//                                   the discovery, so the chest path can be photographed anywhere.
	//   --cast-bramble                : one Bramble Seed sown at the player's feet, through
	//                                   Player.FireBrambleSeed, so the patch has the radius a real cast has.
	//   --walk=DX,DY                  : walks the player that way instead of kiting (a trail needs motion).
	//   --fx-lead=N                   : frames between the trigger and the first shot (default 3).
	//   --start-spell=ID              : the Test Wizard's opening spell (Global.TestWizardStartingSpellId);
	//                                   pair with --character=0.
	//   --spell-stage=N[,DX,DY]       : a spell photographed against a known crowd. The arena is kept
	//                                   empty until fxLead frames before the shutter, then N skeletons
	//                                   are stood in a loose knot DX,DY from the player (default 230,0),
	//                                   shuffling toward him at a crawl and impossible to kill, and the
	//                                   player holds still and cannot die. A spell whose cooldown has
	//                                   come round fires the frame they appear, so a --burst that
	//                                   follows starts on its first frame. Give it --frames past the
	//                                   spell's cooldown (Black Tentacles is 4 s).
	//   --stage-mortal                : the stage's skeletons keep their own health and can die.
	//   --demo-evolutions=id,id       : evolutions (level 4, level 8 or an Apotheosis form id) applied
	//                                   to the first equipped spell with the --demo-spell-level state.
	private int stageCount;
	// --stage-mortal: the stage's skeletons keep their own health and can die, for the spells that
	// only show themselves on a kill (Frost Shard's shatter, Contagion's jump, Cataclysm's craters).
	private bool stageMortal;
	private Vector2 stageOffset = new Vector2(230f, 0f);
	private bool stagePlaced;
	private readonly System.Collections.Generic.List<Enemy> stageCrowd = new();
	private string[] demoEvolutions = System.Array.Empty<string>();
	private bool dropPotion;
	private bool dropHealth;
	private bool winEvent;
	private bool eventChest;
	private bool castBramble;
	private Vector2? walk;
	// --walk-from=N: the --walk starts at frame N and the player stands still before it. Hunter's Draw
	// charges on distance walked, so a burst that starts on the first step of a walk photographs the
	// bow from a quarter drawn to loosed (BowDrawVisual). With --spell-stage the walk keeps the stage's
	// rules - nothing but the crowd on the field, the player unkillable - instead of standing him still.
	private int walkFromFrame;
	// --walk-until=N: the --walk stops at frame N and the player stands still after it. The facing
	// family (Stormsweep, Sleet Runes) aims by the way he last walked, so a capture of a spell that
	// keeps facing left after he stops is a capture of Player.FacingDirection surviving the stop.
	private int walkUntilFrame;
	// --cast-on-stage: with --spell-stage, every equipped spell's cooldown comes due the frame the crowd
	// appears (Player.DebugRipenSpells), so a burst that follows starts on a cast's first frame. Aimed
	// spells already hold their cast for the crowd; this is for the ones that fire on an empty field.
	private bool castOnStage;
	private int fxLead = 3;
	private bool fxFired;
	private SideEvent wonEvent;

	private int frames;
	private bool captured;
	private bool latePressed;
	private string hoverLate;
	private bool hovered;

	// --burst=N,GAP: after the shot, N-1 more GAP frames apart, saved as <shot>-1, <shot>-2 ... from
	// the SAME run. For anything that animates (the boon aura, BoonAura.cs): separate runs deal a
	// different level-up each time, so frames taken from them would not show one thing moving.
	private int burstCount = 1;
	private int burstGap = 8;
	private int burstTaken;
	private int burstWait;

	// --grant-boons=id,id,...: gives the player those boons a quarter of the way through the warmup,
	// so the pause screen's Boons section (Node2DGame.BuildBoonListSection) has something in it.
	// Pair with --open-pause. Boons are rare, so a real run rarely holds one inside any warmup.
	private string[] grantBoons;
	private bool boonsGranted;

	// --scroll=N: scrolls every ScrollContainer N pixels down three quarters of the way through the
	// warmup, to photograph what sits below the fold (the pause screen's Boons section).
	private int scrollBy;
	private bool scrolled;

	// Gameplay shots past the first minute were impossible without this: a run pauses on its
	// first level-up about eight seconds in, and every photograph after that point came back as
	// the card screen over a frozen arena. Taking the first offered option each time is not a
	// build anybody would choose, but the subject here is the arena, not the loadout.
	// The bot. Without it every gameplay photograph was a corpse: the player stands in the spawn
	// point, the swarm closes, and the shot comes back as the game-over panel at eight seconds.
	// It only has to survive long enough to be photographed, so this is the balance harness kite
	// with nothing clever in it - move away from the weighted centre of everything close, lean
	// toward the arena middle so it cannot reverse into a wall and be pinned there.
	private static readonly string[] MoveActions = { "move_left", "move_right", "move_up", "move_down" };

	private void DriveMovement()
	{
		Node2D self = null;
		foreach (Node node in GetTree().GetNodesInGroup("player"))
		{
			if (node is Node2D found && IsInstanceValid(found))
				self = found;
		}

		if (self == null)
			return;

		Vector2 away = Vector2.Zero;
		int counted = 0;
		foreach (Node n in GetTree().GetNodesInGroup("enemies"))
		{
			if (n is not Node2D e || !IsInstanceValid(n))
				continue;

			Vector2 offset = self.GlobalPosition - e.GlobalPosition;
			float d = offset.Length();
			if (d > 260f || d <= 0.01f)
				continue;

			away += offset / d * (1f - d / 260f);
			counted++;
		}

		Vector2 toCentre = -self.GlobalPosition;
		Vector2 desired = counted > 0
			? away.Normalized() + toCentre.Normalized() * 0.45f
			: toCentre.Normalized();
		if (desired.LengthSquared() > 0.0001f)
			desired = desired.Normalized();

		Press("move_right", "move_left", desired.X);
		Press("move_down", "move_up", desired.Y);
	}

	// --shove: stand still until the block is placed, then walk east into it and keep walking.
	private void DriveShove()
	{
		if (FindNodeOfType<Player>(GetTree().Root) is not Player p)
			return;
		p.MaxHP = System.Math.Max(p.MaxHP, 100000);
		p.CurrentHP = p.MaxHP;
		if (!shovePlaced && frames >= WarmupFrames / 3)
		{
			foreach (Node n in GetTree().GetNodesInGroup("enemies"))
				if (IsInstanceValid(n))
					n.QueueFree();
			var scene = GD.Load<PackedScene>("res://scenes/enemy.tscn");
			for (int col = 0; col < 3; col++)
				for (int row = 0; row < 4; row++)
				{
					if (scene?.Instantiate() is not Enemy e)
						continue;
					e.Speed = 0f;
					e.Health = 1000000;
					e.Position = p.GlobalPosition + new Vector2(150f + col * 46f, (row - 1.5f) * 46f);
					p.GetParent().AddChild(e);
					shoveBlock.Add(e);
				}
			shovePlaced = true;
			GD.Print($"_UiShot: shove block of {shoveBlock.Count} placed east of {p.GlobalPosition}");
		}
		// Anything else that spawns is cleared, so the picture is of the block alone.
		if (shovePlaced)
			foreach (Node n in GetTree().GetNodesInGroup("enemies"))
				if (n is Enemy other && IsInstanceValid(other) && !shoveBlock.Contains(other))
					other.QueueFree();
		Press("move_right", "move_left", shovePlaced ? 1f : 0f);
		Press("move_down", "move_up", 0f);
	}

	// --ghost: stand still, immortal, with the ghost and nothing else on the field.
	private void DriveGhost()
	{
		foreach (string a in MoveActions)
			Input.ActionRelease(a);
		if (FindNodeOfType<Player>(GetTree().Root) is not Player p)
			return;
		p.MaxHP = System.Math.Max(p.MaxHP, 100000);
		p.CurrentHP = p.MaxHP;
		// Wail: placed 46 frames before the shutter with the cooldown nearly spent, so the wind-up
		// (one second, 60 frames) is about three quarters through when the picture is taken.
		int at = ghostWail ? System.Math.Max(1, WarmupFrames - 46) : WarmupFrames / 3;
		if (ghostEnemy == null && frames >= at
			&& GD.Load<PackedScene>("res://scenes/BooEnemy.tscn")?.Instantiate() is BooEnemy boo)
		{
			// Held in place, so the picture is of the two side by side rather than of it on top of
			// him: 220 is outside both the wail's engage reach (195) and inside the phase's floor
			// (260), so the idle shot shows it doing neither.
			boo.Health = 1000000;
			boo.Speed = 0f;
			if (ghostWail)
				boo.WailIntervalSeconds = 0.05f;
			boo.Position = p.GlobalPosition + new Vector2(ghostWail ? 150f : ghostPhase ? 420f : 220f, -20f);
			p.GetParent().AddChild(boo);
			ghostEnemy = boo;
			GD.Print($"_UiShot: ghost placed at {boo.Position}, player at {p.GlobalPosition}");
		}
		foreach (Node n in GetTree().GetNodesInGroup("enemies"))
			if (n is Enemy other && IsInstanceValid(other) && other != ghostEnemy)
				other.QueueFree();
	}

	// --tender: stand still, immortal; call the tender through the event, then hold it beside him.
	private void DriveTender()
	{
		foreach (string a in MoveActions)
			Input.ActionRelease(a);
		if (FindNodeOfType<Player>(GetTree().Root) is not Player p)
			return;
		p.MaxHP = System.Math.Max(p.MaxHP, 100000);
		p.CurrentHP = p.MaxHP;

		// After any --goto teleport (a third of the way in), so the tender comes where he now stands.
		if (!tenderCalled && frames >= WarmupFrames / 3 + 10
			&& FindNodeOfType<SideEventDirector>(GetTree().Root) is SideEventDirector director)
		{
			tenderCalled = true;
			if ((director.Event?.IsFound == true ? director.Event : director.Start()) is BogLanternsEvent bog)
			{
				bog.DebugSnuffAll();
				GD.Print("_UiShot: every bog lantern snuffed; the tender is called on the event's next tick");
			}
			else
				GD.PrintErr($"_UiShot: --tender needs the Blighted Swamp's lanterns (--stage=3); stage {Global.SelectedStageIdx} has {director.Event?.GetType().Name ?? "no event"}");
		}

		if (tenderCalled && tenderEnemy == null)
			tenderEnemy = FindNodeOfType<LanternTenderEnemy>(GetTree().Root);
		bool alive = tenderEnemy != null && IsInstanceValid(tenderEnemy);
		if (alive && !tenderPlaced)
		{
			tenderPlaced = true;
			// Held where the picture wants it and never killed: the subject is its shape and its tells.
			tenderEnemy.Speed = 0f;
			tenderEnemy.Health = 1000000;
			tenderEnemy.GlobalPosition = p.GlobalPosition + tenderOffset;
			GD.Print($"_UiShot: lantern-tender at {tenderEnemy.GlobalPosition}, player at {p.GlobalPosition}, lanterns {(tenderEnemy.Lanterns != null ? "handed over" : "MISSING")}");
		}
		if (alive && tenderMove is LanternTenderEnemy.Move move && !tenderMoveFired && frames >= WarmupFrames - fxLead)
		{
			tenderMoveFired = true;
			tenderEnemy.DebugBegin(move);
			GD.Print($"_UiShot: lantern-tender begins {move}, {fxLead} frames before the shutter");
		}
		if (tenderCrowd)
			return;
		foreach (Node n in GetTree().GetNodesInGroup("enemies"))
			if (n is Enemy other && IsInstanceValid(other) && other != tenderEnemy)
				other.QueueFree();
	}

	// --heat / --still-for: no input, cannot die, and the stand-still state pinned once the relics
	// and boons handed over a quarter of the way in are held.
	private void HoldStance()
	{
		foreach (string a in MoveActions)
			Input.ActionRelease(a);
		if (FindNodeOfType<Player>(GetTree().Root) is not Player p)
			return;
		p.MaxHP = System.Math.Max(p.MaxHP, 100000);
		p.CurrentHP = p.MaxHP;
		if (frames > WarmupFrames / 4)
			p.DebugSetStance(pinHeat ?? p.BrazierHeat, pinStillFor ?? p.StillSeconds);
	}

	// --hold, after the teleport: no input, back on the target, full health.
	private void HoldPosition()
	{
		foreach (string a in MoveActions)
			Input.ActionRelease(a);
		if (teleportTo is not Vector2 target || FindNodeOfType<Player>(GetTree().Root) is not Player p)
			return;
		p.GlobalPosition = target;
		p.MaxHP = System.Math.Max(p.MaxHP, 100000);
		p.CurrentHP = p.MaxHP;
	}

	private static void Press(string positive, string negative, float axis)
	{
		if (axis > 0.02f)
		{
			Input.ActionRelease(negative);
			Input.ActionPress(positive, System.Math.Min(1f, axis));
		}
		else if (axis < -0.02f)
		{
			Input.ActionRelease(positive);
			Input.ActionPress(negative, System.Math.Min(1f, -axis));
		}
		else
		{
			Input.ActionRelease(positive);
			Input.ActionRelease(negative);
		}
	}

	private void DismissLevelUpMenus(Node root)
	{
		// The menu-queue probe opens and closes level-ups itself.
		if (KeepLevelUp || menuQueueProbeStarted)
			return;

		if (root is LevelUpMenu menu && menu.Visible)
		{
			System.Collections.Generic.IReadOnlyList<LevelUpOption> options = menu.GetOfferedOptions();
			if (options != null && options.Count > 0)
				menu.EmitSignal(LevelUpMenu.SignalName.WeaponSelected, options[0].SpellId);
			return;
		}

		foreach (Node child in root.GetChildren())
			DismissLevelUpMenus(child);
	}

	// --demo-level=N / --demo-spell-level=N: the player's level, and the first equipped spell's, set a
	// quarter of the way through the warmup (before --force-levelup adds one more level), so the
	// ascension browser can be photographed with requirements part met rather than on a fresh run
	// where every line is unmet. Pair with --grant-boons for element tags.
	private int demoPlayerLevel;
	private int demoSpellLevel;
	// --demo-spells=id,id: spells added to the tome with the demo state, so the browser has more than
	// the starting spell to list.
	private string[] demoSpells = System.Array.Empty<string>();
	private bool demoApplied;
	// --seen-ascensions=a,b,...: which ascension paths the in-memory save has SEEN (AscensionSightings),
	// so the browser and the level 8 page can be photographed with a path sealed, seen but out of
	// reach, or met. Each item is `none` (forget every seen path - put it first for a repeatable
	// shot, since the real save's own are loaded), `spell_id` (every level 8 path of that held spell)
	// or `spell_id:branch_id`. Applied with the demo state; writes are already off. A path whose
	// requirements are met shows in full whatever this says - that is the rule being photographed.
	private string[] seenAscensions;
	private bool seenApplied;

	// The window this harness opens takes the keyboard when it appears, so a key pressed in another
	// program a moment earlier - Enter, most often - landed on the focused level-up card and chose it
	// before the shutter (a run photographed as the gameplay behind a menu that had already closed).
	// A photograph is driven only by its flags: real keys, buttons and pad presses are dropped here,
	// before any Control sees them. _Input runs ahead of the GUI. The tap test injects its own
	// touches and is left alone.
	public override void _Input(InputEvent @event)
	{
		if (tapTest || guardProbe)
			return;
		// The injected Y arrives a frame later (Input buffers parsed events until the next flush),
		// so it is recognised by what it is rather than by a flag around the call.
		if (padYSent && @event is InputEventJoypadButton { ButtonIndex: JoyButton.Y })
			return;
		// --pad: the scripted presses are let through the same way, once the script has started.
		if (padStep > 0 && @event is InputEventJoypadButton)
			return;
		if (@event is InputEventKey || @event is InputEventMouseButton || @event is InputEventJoypadButton
			|| @event is InputEventScreenTouch)
			GetViewport().SetInputAsHandled();
	}

	public override void _Ready()
	{
		// ALWAYS, before anything else. A photograph must never write the player's real save: any
		// run this harness drives can end - the bot is not immortal - and a run that ends pays its
		// Arcane Energy into user://savegame.json. Gating this on --force-death left every other
		// gameplay shot able to leak currency into the save when the bot happened to die.
		SaveManager.WritesDisabled = true;
		// So _Input still drops stray presses while a level-up has the tree paused (the only time a
		// card is there to be chosen). Nothing here overrides _Process; the shutter is FramePostDraw.
		ProcessMode = ProcessModeEnum.Always;

		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--demo-level="))
				demoPlayerLevel = int.Parse(arg.Substring("--demo-level=".Length));
			else if (arg.StartsWith("--demo-spells="))
				demoSpells = arg.Substring("--demo-spells=".Length).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
			else if (arg.StartsWith("--demo-spell-level="))
				demoSpellLevel = int.Parse(arg.Substring("--demo-spell-level=".Length));
			else if (arg.StartsWith("--seen-ascensions="))
				seenAscensions = arg.Substring("--seen-ascensions=".Length).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
			else if (arg.StartsWith("--scene="))
				ScenePath = arg.Substring("--scene=".Length);
			else if (arg.StartsWith("--shot="))
				ShotName = arg.Substring("--shot=".Length);
			else if (arg.StartsWith("--press-late="))
			{
				pressLateQueue = arg.Substring("--press-late=".Length).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
				PressLateButton = pressLateQueue.Length > 0 ? pressLateQueue[0] : "";
			}
			else if (arg.StartsWith("--focus-late="))
				focusLate = arg.Substring("--focus-late=".Length);
			else if (arg == "--demo-shop")
				demoShop = true;
			else if (arg.StartsWith("--pad="))
				padScript = arg.Substring("--pad=".Length).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
			else if (arg.StartsWith("--hover-late="))
				hoverLate = arg.Substring("--hover-late=".Length);
			else if (arg.StartsWith("--press="))
				PressButton = arg.Substring("--press=".Length);
			else if (arg == "--keep-levelup")
				KeepLevelUp = true;
			else if (arg == "--force-levelup")
				ForceLevelUp = true;
			else if (arg == "--focus-boon")
			{
				FocusBoon = true;
				ForceBoon = true;
				ForceLevelUp = true;
				KeepLevelUp = true;
			}
			else if (arg == "--with-charges" || arg == "--with-strip")
			{
				withCharges |= arg == "--with-charges";
				withStrip |= arg == "--with-strip";
				ForceLevelUp = true;
				KeepLevelUp = true;
			}
			else if (arg == "--mixed-deal")
			{
				mixedDeal = true;
				ForceLevelUp = true;
				KeepLevelUp = true;
			}
			else if (arg == "--show-apotheosis")
			{
				// The ember page. Its real forms come from ApotheosisCatalog through the strip; for a
				// photograph of the family the level 8 branches stand in, at the Apotheosis milestone.
				ShowMutation = true;
				mutationMilestone = 8;
				apotheosisPage = true;
				ForceLevelUp = true;
				KeepLevelUp = true;
			}
			else if (arg == "--lock-branch")
				lockBranch = true;
			else if (arg == "--ink-report")
				inkReport = true;
			else if (arg.StartsWith("--tall-deal="))
			{
				tallDeal = int.Parse(arg.Substring("--tall-deal=".Length));
				mixedDeal = true;
				ForceLevelUp = true;
				KeepLevelUp = true;
			}
			else if (arg.StartsWith("--focus-card="))
				focusCard = int.Parse(arg.Substring("--focus-card=".Length));
			else if (arg == "--force-boon")
			{
				ForceBoon = true;
				ForceLevelUp = true;
				KeepLevelUp = true;
			}
			else if (arg.StartsWith("--force-boon="))
			{
				forcedBoonId = arg.Substring("--force-boon=".Length).Trim();
				ForceBoon = true;
				ForceLevelUp = true;
				KeepLevelUp = true;
			}
			else if (arg.StartsWith("--heat="))
				pinHeat = float.Parse(arg.Substring("--heat=".Length), System.Globalization.CultureInfo.InvariantCulture);
			else if (arg.StartsWith("--still-for="))
				pinStillFor = float.Parse(arg.Substring("--still-for=".Length), System.Globalization.CultureInfo.InvariantCulture);
			else if (arg == "--open-chest")
				OpenChest = true;
			else if (arg == "--menu-queue-probe")
				menuQueueProbe = true;
			else if (arg.StartsWith("--crowd="))
				crowd = int.Parse(arg.Substring("--crowd=".Length));
			else if (arg.StartsWith("--landmark-census="))
				landmarkCensus = int.Parse(arg.Substring("--landmark-census=".Length));
			else if (arg == "--pen-camp")
			{
				penCamp = true;
				SaveManager.WritesDisabled = true;
			}
			else if (arg == "--secret-probe")
			{
				secretProbe = true;
				SaveManager.WritesDisabled = true;
			}
			else if (arg.StartsWith("--give-relics="))
				giveRelics = arg.Substring("--give-relics=".Length).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
			else if (arg.StartsWith("--cut-spell="))
				cutSpellId = arg.Substring("--cut-spell=".Length);
			else if (arg == "--fill-book")
				fillBook = true;
			else if (arg == "--hud-milestones")
			{
				fillBook = true;
				hudMilestones = true;
			}
			else if (arg == "--max-book")
			{
				fillBook = true;
				maxBook = true;
			}
			else if (arg.StartsWith("--chest-offer="))
				chestOffer = arg.Substring("--chest-offer=".Length).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
			else if (arg.StartsWith("--drop-chest="))
			{
				string[] xy = arg.Substring(13).Split(',');
				chestDrops.Add(new Vector2(float.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture),
					float.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture)));
			}
			else if (arg == "--force-death")
			{
				ForceDeath = true;
				// A death ends the run and pays its Arcane Energy into the save - the player's real
				// one, since this harness runs against it. Photographing a screen must not do that.
				SaveManager.WritesDisabled = true;
			}
			else if (arg == "--force-victory")
			{
				forceVictory = true;
				// A win pays more than a death does, and opens a chapter: same rule, writes off.
				SaveManager.WritesDisabled = true;
			}
			else if (arg == "--demo-unlocks")
				demoUnlocks = true;
			else if (arg == "--show-ascension")
			{
				ShowMutation = true;
				mutationMilestone = 8;
				ForceLevelUp = true;
				KeepLevelUp = true;
			}
			else if (arg == "--pad-y")
				padY = true;
			else if (arg == "--open-pause")
				OpenPause = true;
			else if (arg.StartsWith("--report-name="))
				reportName = arg.Substring("--report-name=".Length);
			else if (arg.StartsWith("--report-text="))
				reportText = arg.Substring("--report-text=".Length).Replace("\\n", "\n");
			else if (arg == "--report-send")
				reportSend = true;
			else if (arg == "--report-back")
				reportBack = true;
			else if (arg == "--show-mutation")
			{
				ShowMutation = true;
				ForceLevelUp = true;
				KeepLevelUp = true;
			}
			// Gameplay needs hundreds of frames before there is a crowd worth photographing; a
			// menu is laid out in ten.
			else if (arg.StartsWith("--frames="))
				WarmupFrames = int.Parse(arg.Substring("--frames=".Length));
			else if (arg.StartsWith("--scroll="))
			{
				// Both: scrollBy once at three quarters of the warmup, and scrollTo held from the late
				// press on. The second `--scroll=` branch below was never reached, so a list opened by
				// --press-late (the ascension browser) could not be scrolled at all.
				scrollBy = int.Parse(arg.Substring("--scroll=".Length));
				scrollTo = scrollBy;
			}
			else if (arg.StartsWith("--grant-boons="))
				grantBoons = arg.Substring("--grant-boons=".Length).Split(',');
			else if (arg.StartsWith("--burst="))
			{
				string[] burst = arg.Substring("--burst=".Length).Split(',');
				burstCount = System.Math.Max(1, int.Parse(burst[0]));
				if (burst.Length > 1)
					burstGap = System.Math.Max(1, int.Parse(burst[1]));
			}
			// Photographing a RUN rather than a menu needs the two selections a real run would
			// have made on the way in. Without them every gameplay shot is chapter one as
			// character zero, which is a silent wrong answer rather than an error.
			else if (arg.StartsWith("--stage="))
				Global.SelectedStageIdx = int.Parse(arg.Substring("--stage=".Length));
			else if (arg.StartsWith("--character="))
				Global.SelectedCharacterIdx = int.Parse(arg.Substring("--character=".Length));
			// Gameplay wants hundreds of frames before there is a crowd worth photographing, and
			// at 1x that is ten real seconds of waiting per shot.
			else if (arg.StartsWith("--timescale="))
				Engine.TimeScale = float.Parse(arg.Substring("--timescale=".Length));
			// The main menu as a player sees it: TitleScreen with the menu already open over the
			// art, the way every return-to-menu path loads it. MainMenu.tscn on its own draws the
			// menu on a black ground, which shows the layout but not what it sits on.
			else if (arg == "--open-menu")
				Global.OpenMenuImmediately = true;
			// --found-secrets=id,id: marks those stage secrets found in the in-memory save (writes off),
			// to photograph the Unlocks page's Secrets section with something in it.
			// A Boss Rush (issue #12) instead of the chapter; writes off, since its end records a best.
			else if (arg == "--boss-rush")
			{
				Global.BossRush = true;
				SaveManager.WritesDisabled = true;
			}
			// --boss-rush-open[=felled]: the rush's card on the stage list - the finale marked beaten in
			// the in-memory save (writes off), with a best of that many felled when given.
			else if (arg.StartsWith("--boss-rush-open"))
			{
				SaveManager.WritesDisabled = true;
				var rushSave = GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data;
				if (rushSave != null && BossRushCatalog.Finale is BossDefinition finale)
				{
					rushSave.Lifetime.Absorb(new RunResult { Outcome = RunOutcome.Victory, BossId = finale.Id, StageId = BossRushCatalog.Arena?.Id ?? "" });
					if (arg.StartsWith("--boss-rush-open="))
						rushSave.BossRushBestFelled = int.Parse(arg.Substring("--boss-rush-open=".Length));
				}
			}
			else if (arg.StartsWith("--rush-steps="))
				rushSteps = int.Parse(arg.Substring("--rush-steps=".Length));
			else if (arg.StartsWith("--found-secrets="))
			{
				SaveManager.WritesDisabled = true;
				var secretsSave = GetNodeOrNull<SaveManager>("/root/SaveManager");
				foreach (string id in arg.Substring("--found-secrets=".Length).Split(',', System.StringSplitOptions.RemoveEmptyEntries))
					secretsSave?.Data.RecordSecretFound(id.Trim());
			}
			else if (arg.StartsWith("--shore="))
			{
				shoreDirection = arg.Substring("--shore=".Length) switch
				{
					"up" => new Vector2I(0, -1),
					"down" => new Vector2I(0, 1),
					"left" => new Vector2I(-1, 0),
					"right" => new Vector2I(1, 0),
					_ => null,
				};
				if (shoreDirection == null)
					GD.PrintErr($"_UiShot: {arg} - expected up, down, left or right");
			}
			// The swarm chasing in straight lines on an open stage, as before it had routing - the
			// "before" half of a photograph of enemies going round a lake (.ai/terrain.md, section 9).
			else if (arg == "--shore-fence")
				shoreFence = true;
			else if (arg == "--no-open-routing")
				MazeNavigation.OpenStageRouting = false;
			// --demo-curation: two Redactions owned and Fireball set aside, in memory only, so the
			// Spell Index's curation controls can be photographed on a save that has never bought one.
			else if (arg == "--demo-curation")
			{
				SaveManager.WritesDisabled = true;
				if (GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data is SaveData demo)
				{
					demo.ArcaneUpgradeLevels[GlobalStatsManager.CurationUpgradeId] = 2;
					if (!demo.RemovedSpellIds.Contains("fireball"))
						demo.RemovedSpellIds.Add("fireball");
				}
			}
			// The unlock state a screen is photographed in, in memory only (writes are already off):
			//   --fresh-save          : a brand-new save - starters only, every secret sealed.
			//   --open-stages=a,b     : those chapters opened (stage ids), which is what reveals a
			//                           secret wizard (UnlockDefinition.RevealStageId).
			//   --free-wizards=a,b    : those wizards unlocked.
			// So the character select's three card states - unlocked, locked silhouette, sealed
			// secret - can be shot on demand without touching the player's real save.
			else if (arg == "--fresh-save")
				ReplaceSave(new SaveData());
			else if (arg.StartsWith("--open-stages="))
			{
				foreach (string id in arg.Substring("--open-stages=".Length).Split(',', System.StringSplitOptions.RemoveEmptyEntries))
					GlobalStatsManager.UnlockStage(CurrentSave(), id.Trim());
			}
			else if (arg.StartsWith("--free-wizards="))
			{
				foreach (string id in arg.Substring("--free-wizards=".Length).Split(',', System.StringSplitOptions.RemoveEmptyEntries))
					GlobalStatsManager.UnlockCharacter(CurrentSave(), id.Trim());
			}
			else if (arg.StartsWith("--scroll="))
				scrollTo = int.Parse(arg.Substring("--scroll=".Length));
			// --unlock-wizards: every wizard owned, in memory only (writes are off above), so a newly
			// added one can be photographed on the character select as a player who has earned him sees it.
			// --campaign-complete: every spell and wizard owned, in memory only, which is what opens
			// the final chapter (GlobalStatsManager.IsCampaignComplete) - for the stage list's last state.
			else if (arg == "--campaign-complete")
			{
				SaveData all = CurrentSave();
				foreach (string id in UnlockCatalog.AllSpellIds)
					GlobalStatsManager.UnlockSpell(all, id);
				foreach (string id in UnlockCatalog.AllWizardIds)
					GlobalStatsManager.UnlockCharacter(all, id);
			}
			else if (arg == "--unlock-wizards")
			{
				if (GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data is SaveData owned)
				{
					foreach (string id in UnlockCatalog.AllWizardIds)
						GlobalStatsManager.UnlockCharacter(owned, id);
				}
			}
			// --guard-probe: the level-up arming pause (LevelUpMenu.InputGuardSeconds), with real clicks.
			else if (arg == "--guard-probe")
			{
				guardProbe = true;
				ForceLevelUp = true;
				KeepLevelUp = true;
			}
			else if (arg == "--tap-test")
			{
				tapTest = true;
				ForceLevelUp = true;
				KeepLevelUp = true;
			}
			else if (arg == "--hold")
				hold = true;
			else if (arg == "--shove")
				shove = true;
			else if (arg == "--ghost")
				ghost = true;
			else if (arg == "--ghost-wail")
				ghost = ghostWail = true;
			else if (arg == "--ghost-phase")
				ghost = ghostPhase = true;
			else if (arg == "--tender")
				tenderShot = true;
			else if (arg.StartsWith("--tender="))
			{
				tenderShot = true;
				tenderMove = arg.Substring("--tender=".Length) switch
				{
					"strikes" => LanternTenderEnemy.Move.Strikes,
					"oil" => LanternTenderEnemy.Move.Oil,
					"dark" => LanternTenderEnemy.Move.Dark,
					"rekindle" => LanternTenderEnemy.Move.Rekindle,
					_ => null,
				};
			}
			else if (arg.StartsWith("--tender-offset="))
			{
				string[] xy = arg.Substring("--tender-offset=".Length).Split(',');
				tenderOffset = new Vector2(
					float.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture),
					float.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture));
			}
			else if (arg == "--tender-crowd")
				tenderCrowd = true;
			else if (arg == "--clear-enemies")
				clearEnemies = true;
			else if (arg == "--status-showcase")
				statusShowcase = true;
			else if (arg == "--status-boss")
			{
				statusShowcase = true;
				statusBoss = true;
			}
			// Node2DGame reads --status-all itself; here it only means "keep the player standing",
			// since a crowd shot is worthless as a game-over panel.
			else if (arg == "--status-all")
				statusCrowd = true;
			else if (arg == "--find-water")
				findWater = true;
			else if (arg == "--drop-potion")
				dropPotion = true;
			else if (arg == "--drop-health")
				dropHealth = true;
			else if (arg == "--win-event")
				winEvent = true;
			else if (arg == "--event-chest")
				eventChest = true;
			else if (arg == "--cast-bramble")
				castBramble = true;
			else if (arg.StartsWith("--start-spell="))
				Global.TestWizardStartingSpellId = arg.Substring("--start-spell=".Length).Trim();
			else if (arg.StartsWith("--spell-stage="))
			{
				string[] parts = arg.Substring("--spell-stage=".Length).Split(',');
				stageCount = System.Math.Max(1, int.Parse(parts[0]));
				if (parts.Length >= 3)
					stageOffset = new Vector2(float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
						float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
			}
			else if (arg == "--stage-mortal")
				stageMortal = true;
			else if (arg.StartsWith("--demo-evolutions="))
				demoEvolutions = arg.Substring("--demo-evolutions=".Length).Split(',', System.StringSplitOptions.RemoveEmptyEntries);
			else if (arg.StartsWith("--fx-lead="))
				fxLead = System.Math.Max(1, int.Parse(arg.Substring("--fx-lead=".Length)));
			else if (arg.StartsWith("--walk="))
			{
				string[] xy = arg.Substring("--walk=".Length).Split(',');
				walk = new Vector2(
					float.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture),
					float.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture)).Normalized();
			}
			else if (arg.StartsWith("--walk-from="))
				walkFromFrame = System.Math.Max(0, int.Parse(arg.Substring("--walk-from=".Length)));
			else if (arg.StartsWith("--walk-until="))
				walkUntilFrame = System.Math.Max(0, int.Parse(arg.Substring("--walk-until=".Length)));
			else if (arg == "--cast-on-stage")
				castOnStage = true;
			else if (arg.StartsWith("--goto="))
				gotoTarget = arg.Substring("--goto=".Length);
			else if (arg.StartsWith("--goto-offset="))
			{
				string[] xy = arg.Substring("--goto-offset=".Length).Split(',');
				gotoOffset = new Vector2(
					float.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture),
					float.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture));
			}
			else if (arg == "--take-wayfinder")
				takeWayfinder = true;
			else if (arg == "--force-hint")
				forceHint = true;
			else if (arg == "--arrive")
				arriveEvent = true;
			// --zoom=Z: the run camera's zoom (player.tscn's is 0.85), for a map-like view of a stretch
			// of ground - the maze's trap pattern, say. Applied at a quarter of the warmup.
			else if (arg.StartsWith("--zoom="))
				cameraZoom = float.Parse(arg.Substring("--zoom=".Length), System.Globalization.CultureInfo.InvariantCulture);
			else if (arg.StartsWith("--teleport="))
			{
				string[] xy = arg.Substring("--teleport=".Length).Split(',');
				teleportTo = new Vector2(
					float.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture),
					float.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture));
			}
		}

		if (demoShop)
			ReplaceSave(BuildDemoShopSave());

		// Status shots keep the player alive from the first frame. A death would end the run and pay
		// its Arcane Energy into the real save, so writes are off too.
		if (statusShowcase || statusCrowd || shove || ghost || tenderShot || pinHeat != null || pinStillFor != null)
			SaveManager.WritesDisabled = true;
		if (dropPotion || dropHealth || winEvent || castBramble || stageCount > 0 || demoEvolutions.Length > 0)
			SaveManager.WritesDisabled = true;
		// A run that walks to its event can win it, and a won event saves on the spot.
		if (gotoTarget != null || takeWayfinder || arriveEvent)
			SaveManager.WritesDisabled = true;

		if (!ResourceLoader.Exists(ScenePath))
		{
			GD.PrintErr($"_UiShot: no scene at '{ScenePath}'");
			GetTree().Quit(1);
			return;
		}

		var packed = GD.Load<PackedScene>(ScenePath);
		Node instance = packed?.Instantiate();
		if (instance == null)
		{
			GD.PrintErr($"_UiShot: '{ScenePath}' failed to instantiate");
			GetTree().Quit(1);
			return;
		}

		AddChild(instance);
		MoveChild(instance, 0);

		if (!string.IsNullOrEmpty(PressButton))
		{
			Button target = FindButton(instance, PressButton);
			if (target == null)
				GD.PrintErr($"_UiShot: no button named '{PressButton}'");
			else
				target.EmitSignal(BaseButton.SignalName.Pressed);
		}

		RenderingServer.FramePostDraw += OnFramePostDraw;
	}

	// The save the --fresh-save / --open-stages / --free-wizards flags edit. Writes are off from the
	// first line of _Ready, so none of it ever reaches user://savegame.json.
	private SaveData CurrentSave()
	{
		SaveManager.WritesDisabled = true;
		return GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data ?? new SaveData();
	}

	private void ReplaceSave(SaveData data)
	{
		SaveManager.WritesDisabled = true;
		GetNodeOrNull<SaveManager>("/root/SaveManager")?.ReplaceInMemory(data);
	}

	// The --demo-shop purse is 155: Gravewell (150) and Hunter's Draw (140) can be bought, Hollow
	// Star (160) and Kindled Ward (170) cannot; Runes of Ruin and Vitality are at their maximum;
	// Emberheart, Chronoweave and Phoenix Oath are affordable, Proscription, Hoarded Insight and
	// Fortune Thread are not; Molten Shard is owned; Redaction is not yet available (a fresh book);
	// Eternal Wick is one affordable level (125) from its last, so --press-late=...,Buy_duration
	// photographs the seal being stamped.
	private static SaveData BuildDemoShopSave()
	{
		var data = new SaveData { TotalCurrency = 155 };
		data.ArcaneUpgradeLevels["damage"] = 8;
		data.ArcaneUpgradeLevels["duration"] = 5;
		data.ArcaneUpgradeLevels["vitality"] = 10;
		data.ArcaneUpgradeLevels["recovery"] = 3;
		data.ArcaneUpgradeLevels["cooldowns"] = 2;
		data.ArcaneUpgradeLevels["amount"] = 1;
		data.ArcaneUpgradeLevels["rerolls"] = 1;
		data.ArcaneUpgradeLevels["bans"] = 1;
		data.ArcaneUpgradeLevels["banked_levels"] = 1;
		data.ArcaneUpgradeLevels["luck"] = 5;
		data.ArcaneUpgradeLevels["crit_chance"] = 2;
		GlobalStatsManager.UnlockSpell(data, "molten_shard");
		return data;
	}

	public override void _ExitTree()
	{
		RenderingServer.FramePostDraw -= OnFramePostDraw;
		// Left pressed, the input stays held into whatever scene loads next.
		foreach (string a in MoveActions)
			Input.ActionRelease(a);
	}

	// A trailing * matches by prefix (--press-late=ElementChip_* presses the first element chip on
	// the screen, whichever element the run happened to deal).
	private static Button FindButton(Node node, string name)
	{
		if (node is Button button && (button.Name == name
			|| (name.EndsWith("*") && button.IsVisibleInTree() && ((string)button.Name).StartsWith(name.TrimEnd('*')))))
			return button;
		foreach (Node child in node.GetChildren())
		{
			Button found = FindButton(child, name);
			if (found != null)
				return found;
		}
		return null;
	}

	// The focused control and its parent, since a card's stop is named for what it is (CardStop) and
	// its card for what it sells.
	private string FocusName()
	{
		Control owner = GetViewport()?.GuiGetFocusOwner();
		return owner == null ? "nothing" : $"{owner.GetParent()?.Name}/{owner.Name}";
	}

	// --focus-late: any visible focusable control, by name (a trailing * matches by prefix).
	private static Control FindControl(Node node, string name)
	{
		if (node is Control control && control.FocusMode != Control.FocusModeEnum.None && control.IsVisibleInTree()
			&& (control.Name == name || (name.EndsWith("*") && ((string)control.Name).StartsWith(name.TrimEnd('*')))))
			return control;
		foreach (Node child in node.GetChildren())
		{
			Control found = FindControl(child, name);
			if (found != null)
				return found;
		}
		return null;
	}

	// --tap-test (implies --force-levelup --keep-levelup): after the warmup, drives the element chip
	// card with REAL touches through Input.ParseInputEvent - so emulated mouse, GUI routing and
	// BaseButton are all on the path - and prints PASS/FAIL per behaviour: a tap on a chip opens its
	// card and does not choose the spell; a tap elsewhere closes it and chooses nothing; a drag that
	// starts on a chip opens nothing; B/Escape closes it. Then photographs the screen with the card
	// open. On a desktop a ScrollContainer does not drag-scroll from mouse events, so this proves the
	// chip lets go of a drag, not that the list scrolls under it - that half needs the phone.
	private bool tapTest;
	private bool tapTestDone;
	private int tapStep;
	private Vector2 tapAt;
	private int tapFailures;

	// --guard-probe: a real left click on the selected card the frame the level-up screen is found
	// (it must be swallowed - the screen still up a few frames later), then another once it has armed
	// (it must choose - the screen gone). PASS/FAIL per half; the shutter waits for both.
	private bool guardProbe;
	private bool guardProbeDone;
	private int guardStep;
	private int guardWait;
	private LevelUpMenu guardMenu;
	private Rect2 guardLastRect;

	private void TickGuardProbe()
	{
		if (!guardProbe || guardProbeDone)
			return;
		switch (guardStep)
		{
			case 0:
				// On a card, once the cards have laid out (the same rect two frames running) - a click
				// at a card still being placed lands on nothing and would prove nothing.
				guardMenu = FindNodeOfType<LevelUpMenu>(GetTree().Root);
				if (guardMenu == null || !guardMenu.Visible || GetViewport().GuiGetFocusOwner() is not Button early
					|| !early.Name.ToString().StartsWith("OptionCard"))
					return;
				Rect2 rect = early.GetGlobalRect();
				bool settled = rect.Size.Y > 40f && rect.IsEqualApprox(guardLastRect);
				guardLastRect = rect;
				if (!settled)
					return;
				Check($"the cards are laid out before the screen arms ({LevelUpMenu.InputGuardSeconds:0.0} s pause)", !guardMenu.IsArmed);
				ClickAt(rect.GetCenter());
				guardStep = 1;
				guardWait = 0;
				return;
			case 1:
				if (++guardWait < 6)
					return;
				Check("a click before the screen arms chooses nothing", IsInstanceValid(guardMenu) && guardMenu.Visible);
				guardStep = 2;
				return;
			case 2:
				if (!IsInstanceValid(guardMenu) || !guardMenu.IsArmed || GetViewport().GuiGetFocusOwner() is not Button armed)
					return;
				ClickAt(armed.GetGlobalRect().GetCenter());
				guardStep = 3;
				guardWait = 0;
				return;
			case 3:
				if (++guardWait < 6)
					return;
				Check("a click once the screen has armed chooses the card", !IsInstanceValid(guardMenu) || !guardMenu.Visible);
				guardProbeDone = true;
				GD.Print($"_UiShot: guard probe {(tapFailures == 0 ? "PASSED" : $"FAILED {tapFailures}")}");
				return;
		}
	}

	private void ClickAt(Vector2 canvas)
	{
		// A finger on a phone canvas, which Input turns into the left mouse button as well (Touch),
		// and a mouse on a PC one - so the guard is tested against every kind of press it swallows.
		if (ResponsiveLayout.IsNarrow(this))
		{
			Touch(canvas, true);
			Touch(canvas, false);
			return;
		}
		Vector2 window = GetTree().Root.GetFinalTransform() * canvas;
		Input.ParseInputEvent(new InputEventMouseMotion { Position = window, GlobalPosition = window });
		foreach (bool pressed in new[] { true, false })
			Input.ParseInputEvent(new InputEventMouseButton
			{
				ButtonIndex = MouseButton.Left, Pressed = pressed, Position = window, GlobalPosition = window,
				ButtonMask = pressed ? MouseButtonMask.Left : 0,
			});
	}

	private bool RunTapTest()
	{
		LevelUpMenu menu = FindNodeOfType<LevelUpMenu>(GetTree().Root);
		// A real tap is swallowed until the screen arms (LevelUpMenu.InputGuardSeconds); hold the
		// shutter and start once it has, so the test measures the chips and not the pause.
		if (menu != null && !menu.IsArmed)
			return true;
		tapStep++;
		var corner = new Vector2(24f, ResponsiveLayout.ViewportSize(this).Y * 0.5f);
		switch (tapStep)
		{
			case 1:
				ElementChip chip = FindVisibleChip(GetTree().Root);
				if (chip == null || menu == null)
				{
					GD.PrintErr("_UiShot: --tap-test found no element chip on a level-up card");
					tapTestDone = true;
					return false;
				}
				tapAt = chip.GetGlobalTransformWithCanvas() * (chip.Size * 0.5f);
				GD.Print($"_UiShot: tap test on {chip.Name} at {tapAt}");
				Touch(tapAt, true);
				break;
			case 3: Touch(tapAt, false); break;
			case 8:
				Check("a tap on a chip opens its card", ElementTagPopover.IsOpen);
				Check("...and does not choose the spell", menu != null && menu.Visible);
				Touch(corner, true);
				break;
			case 10: Touch(corner, false); break;
			case 15:
				Check("a tap elsewhere closes the card", !ElementTagPopover.IsOpen);
				Check("...and chooses nothing", menu != null && menu.Visible);
				Touch(tapAt, true);
				break;
			case 16: case 17: case 18: case 19: case 20:
				Drag(tapAt + new Vector2(0f, 18f * (tapStep - 15)), new Vector2(0f, 18f));
				break;
			case 21: Touch(tapAt + new Vector2(0f, 90f), false); break;
			case 26:
				Check("a drag that starts on a chip opens nothing", !ElementTagPopover.IsOpen);
				Check("...and chooses nothing", menu != null && menu.Visible);
				Touch(tapAt, true);
				break;
			case 28: Touch(tapAt, false); break;
			case 33:
				Check("a second tap opens it again", ElementTagPopover.IsOpen);
				Input.ParseInputEvent(new InputEventAction { Action = "ui_cancel", Pressed = true });
				break;
			case 35: Input.ParseInputEvent(new InputEventAction { Action = "ui_cancel", Pressed = false }); break;
			case 40:
				Check("B / Escape closes it", !ElementTagPopover.IsOpen);
				Check("...and the level-up screen stays", menu != null && menu.Visible);
				// Open it once more for the photograph.
				Touch(tapAt, true);
				break;
			case 42: Touch(tapAt, false); break;
			case 50:
				GD.Print(tapFailures == 0 ? "_UiShot: tap test PASSED" : $"_UiShot: tap test FAILED {tapFailures} check(s)");
				tapTestDone = true;
				return false;
		}
		return true;
	}

	private void Check(string what, bool ok)
	{
		if (!ok)
			tapFailures++;
		GD.Print($"_UiShot: tap test {(ok ? "PASS" : "FAIL")} - {what}");
	}

	// A finger, in canvas units: converted to the window the way a real touch arrives, and parsed by
	// Input, which emulates the left mouse button from it exactly as it does on the phone.
	private void Touch(Vector2 canvas, bool pressed)
	{
		Vector2 window = GetTree().Root.GetFinalTransform() * canvas;
		Input.ParseInputEvent(new InputEventScreenTouch { Index = 0, Position = window, Pressed = pressed });
	}

	private void Drag(Vector2 canvas, Vector2 relativeCanvas)
	{
		Transform2D final = GetTree().Root.GetFinalTransform();
		Input.ParseInputEvent(new InputEventScreenDrag
		{
			Index = 0,
			Position = final * canvas,
			Relative = final.BasisXform(relativeCanvas),
		});
	}

	private static ElementChip FindVisibleChip(Node node)
	{
		if (node is ElementChip chip && chip.IsVisibleInTree())
			return chip;
		foreach (Node child in node.GetChildren())
		{
			ElementChip found = FindVisibleChip(child);
			if (found != null)
				return found;
		}
		return null;
	}

	// --- --ink-report: every word on the screen against the pixels actually behind it -------------
	//
	// tools/art/ui_contrast.py measures each ink against its frame's palette tone. That cannot see
	// what the screen does on top: a modulate on a locked card, a focus ring's wash, a chip blended
	// over a card, a label that sits over a band or a gradient. This walks every visible Label and
	// text Button in a menu layer, takes its declared colour (font_color, or the hover/focus colour
	// in that state, times every modulate above it), and samples the photograph inside its rect:
	// the pixels that are not text are the ground. It reports the ratio against the dominant ground
	// and against the WORST ground that covers at least 4% of the box, to user://<shot>-ink.txt.
	// Targets: 4.5:1, or 3:1 for type 24px and up.
	private bool inkReport;
	// --lock-branch: with --show-mutation or --show-ascension, the last branch is shown locked with a
	// requirement stamped across it (LevelUpMenu.ApplyAscensionLock), to photograph the stamp.
	private bool lockBranch;
	private bool apotheosisPage;

	private void SeedSeenAscensions(Player holder)
	{
		seenApplied = true;
		SaveManager.WritesDisabled = true;
		SaveData data = GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data;
		if (data == null)
		{
			GD.PrintErr("_UiShot: --seen-ascensions needs the SaveManager autoload");
			return;
		}
		foreach (string raw in seenAscensions)
		{
			string item = raw.Trim();
			if (item == "none")
			{
				data.SeenAscensionIds.Clear();
				continue;
			}
			int colon = item.IndexOf(':');
			if (colon > 0)
			{
				data.MarkAscensionSeen(item.Substring(0, colon), item.Substring(colon + 1));
				continue;
			}
			SpellData spell = holder.GetEquippedSpells().FirstOrDefault(s => s != null && s.Id == item);
			if (spell == null)
			{
				GD.PrintErr($"_UiShot: --seen-ascensions names '{item}', which the player does not hold");
				continue;
			}
			SpellEvolutionCatalog.EnsureEvolutionCoverage(spell);
			foreach (SpellEvolutionOption branch in spell.Level8Options ?? new Godot.Collections.Array<SpellEvolutionOption>())
				if (branch != null)
					data.MarkAscensionSeen(spell.Id, branch.Id);
		}
		GD.Print($"_UiShot: seen ascensions - {(data.SeenAscensionIds.Count == 0 ? "none" : string.Join(", ", data.SeenAscensionIds))}");
	}

	private void WriteInkReport(Image image)
	{
		var lines = new System.Collections.Generic.List<string>();
		int fails = 0, total = 0;
		Vector2 canvas = GetViewport().GetVisibleRect().Size;
		Vector2 scale = new Vector2(image.GetWidth() / canvas.X, image.GetHeight() / canvas.Y);
		var controls = new System.Collections.Generic.List<Control>();
		CollectInkControls(GetTree().Root, controls);
		foreach (Control control in controls)
		{
			string text = control is Label l ? l.Text : (control as Button)?.Text;
			if (string.IsNullOrWhiteSpace(text))
				continue;
			Rect2 rect = VisibleRectOf(control);
			if (rect.Size.X < 4 || rect.Size.Y < 4)
				continue;
			Color ink = DeclaredInk(control);
			Color mod = ModulateChain(control);
			int size = control.GetThemeFontSize("font_size");
			float target = size >= 24 ? 3f : 4.5f;

			// Sample the box: anything near the ink is text, the rest is ground.
			var x0 = (int)(rect.Position.X * scale.X);
			var y0 = (int)(rect.Position.Y * scale.Y);
			var x1 = System.Math.Min(image.GetWidth(), (int)(rect.End.X * scale.X));
			var y1 = System.Math.Min(image.GetHeight(), (int)(rect.End.Y * scale.Y));
			var counts = new System.Collections.Generic.Dictionary<int, int>();
			int ground = 0;
			Color inkSeen = new Color(ink.R * mod.R, ink.G * mod.G, ink.B * mod.B);
			for (int y = System.Math.Max(0, y0); y < y1; y++)
			{
				for (int x = System.Math.Max(0, x0); x < x1; x++)
				{
					Color p = image.GetPixel(x, y);
					if (Contrast(p, inkSeen) < 2.2f)
						continue;
					int key = ((int)(p.R * 31) << 10) | ((int)(p.G * 31) << 5) | (int)(p.B * 31);
					counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
					ground++;
				}
			}
			if (ground == 0)
				continue;
			Color Unpack(int key) => new Color(((key >> 10) & 31) / 31f, ((key >> 5) & 31) / 31f, (key & 31) / 31f);
			int dominantKey = counts.OrderByDescending(kv => kv.Value).First().Key;
			Color dominant = Unpack(dominantKey);
			// The ink as it lands: its colour through every modulate, blended by their alpha onto the
			// ground (a faded locked card, a protected spell's page).
			Color effective = dominant.Lerp(inkSeen, mod.A);
			float main = Contrast(effective, dominant);
			float worst = main;
			Color worstGround = dominant;
			foreach (var kv in counts)
			{
				if (kv.Value < ground * 0.04f)
					continue;
				Color g = Unpack(kv.Key);
				float r = Contrast(g.Lerp(inkSeen, mod.A), g);
				if (r < worst)
				{
					worst = r;
					worstGround = g;
				}
			}
			total++;
			bool ok = worst >= target;
			if (!ok)
				fails++;
			string state = control is Button b ? (b.HasFocus() ? " [focus]" : b.IsHovered() ? " [hover]" : "") : "";
			string clipped = text.Replace("\n", " ");
			if (clipped.Length > 44)
				clipped = clipped.Substring(0, 44) + "...";
			lines.Add($"{(ok ? "    " : "FAIL")} {main,5:0.0}:1 worst {worst,5:0.0}:1 (target {target:0.0})  ink #{effective.ToHtml(false)} on #{dominant.ToHtml(false)}"
				+ $" worst #{worstGround.ToHtml(false)}  {size}px{state}  \"{clipped}\"  {control.GetParent()?.Name}/{control.Name}");
		}
		lines.Insert(0, $"ink report - {ShotName}: {total} text controls, {fails} below target");
		string outPath = $"user://{ShotName}-ink.txt";
		using (var file = FileAccess.Open(outPath, FileAccess.ModeFlags.Write))
			file?.StoreString(string.Join("\n", lines) + "\n");
		GD.Print($"_UiShot: ink report {total} text controls, {fails} below target -> {ProjectSettings.GlobalizePath(outPath)}");
	}

	private static void CollectInkControls(Node node, System.Collections.Generic.List<Control> into)
	{
		// Menus only: the level-up screen, the chest, the element card, the main menu (its root over
		// the title art, and whichever panel is open), the in-run pause screen (EscapeMenu) and the
		// results screen (GameOverScreen). The HUD and the world are not what this is for.
		if (node is CanvasItem item && !item.Visible)
			return;
		bool menu = node is LevelUpMenu || node is ChestItemSelectionMenu || node is MainMenu
			|| node is GameOverScreen
			|| node.Name.ToString().Contains("ElementTag") || node.Name == "EscapeMenu";
		if (menu)
		{
			CollectTextControls(node, into);
			return;
		}
		foreach (Node child in node.GetChildren())
			CollectInkControls(child, into);
	}

	private static void CollectTextControls(Node node, System.Collections.Generic.List<Control> into)
	{
		if (node is CanvasItem item && !item.Visible)
			return;
		if (node is Label || node is Button)
			into.Add((Control)node);
		foreach (Node child in node.GetChildren())
			CollectTextControls(child, into);
	}

	private static Color DeclaredInk(Control control)
	{
		if (control is Button button)
		{
			if (button.Disabled)
				return button.GetThemeColor("font_disabled_color");
			if (button.HasFocus())
				return button.GetThemeColor("font_focus_color");
			if (button.IsHovered())
				return button.GetThemeColor("font_hover_color");
		}
		return control.GetThemeColor("font_color");
	}

	// Every Modulate and the control's own SelfModulate, up to its canvas layer.
	private static Color ModulateChain(Control control)
	{
		Color m = control.SelfModulate;
		for (Node n = control; n is CanvasItem ci; n = n.GetParent())
			m *= ci.Modulate;
		return m;
	}

	// The control's rect on the canvas, cut down by the viewport and by every clipping ancestor (a
	// scroll container's below-the-fold rows are not on the screen to be measured).
	private Rect2 VisibleRectOf(Control control)
	{
		Rect2 rect = control.GetGlobalRect();
		Transform2D canvas = control.GetGlobalTransformWithCanvas();
		rect = new Rect2(canvas.Origin, rect.Size * canvas.Scale);
		// A button's words sit inside its frame's content margins; the bevel round them is not
		// what they are read against.
		if (control is Button button)
		{
			string state = button.Disabled ? "disabled" : button.HasFocus() ? "focus" : button.IsHovered() ? "hover" : "normal";
			StyleBox box = button.GetThemeStylebox(state);
			if (box != null)
			{
				float l = box.GetContentMargin(Side.Left), t = box.GetContentMargin(Side.Top);
				float r = box.GetContentMargin(Side.Right), b = box.GetContentMargin(Side.Bottom);
				rect = new Rect2(rect.Position + new Vector2(l, t) * canvas.Scale,
					rect.Size - new Vector2(l + r, t + b) * canvas.Scale);
			}
		}
		rect = rect.Intersection(GetViewport().GetVisibleRect());
		for (Node n = control.GetParent(); n is Control c; n = n.GetParent())
		{
			if (!c.ClipContents)
				continue;
			Transform2D ct = c.GetGlobalTransformWithCanvas();
			rect = rect.Intersection(new Rect2(ct.Origin, c.Size * ct.Scale));
		}
		return rect;
	}

	private static float Contrast(Color a, Color b)
	{
		static float Channel(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
		static float Lum(Color c) => 0.2126f * Channel(c.R) + 0.7152f * Channel(c.G) + 0.0722f * Channel(c.B);
		float la = Lum(a), lb = Lum(b);
		return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
	}

	private static void CollectButtons(Node node, string prefix, System.Collections.Generic.List<Button> into)
	{
		if (node is Button button && button.Name.ToString().StartsWith(prefix))
			into.Add(button);
		foreach (Node child in node.GetChildren())
			CollectButtons(child, prefix, into);
	}

	private static Button FindButtonWithChild(Node node, string childName)
	{
		if (node is Button button && button.GetNodeOrNull(childName) != null)
			return button;
		foreach (Node child in node.GetChildren())
		{
			Button found = FindButtonWithChild(child, childName);
			if (found != null)
				return found;
		}
		return null;
	}

	private static T FindNodeOfType<T>(Node root) where T : Node
	{
		if (root is T match)
			return match;
		foreach (Node child in root.GetChildren())
		{
			T found = FindNodeOfType<T>(child);
			if (found != null)
				return found;
		}
		return null;
	}

	private void OnFramePostDraw()
	{
		// Menus build themselves over several frames - fonts resolve, containers sort, and the
		// responsive column count is re-applied on a viewport signal. Capturing the first drawn
		// frame catches a half-laid-out screen, so give it a few.
		if (captured && burstTaken > 0 && burstTaken < burstCount)
		{
			// A burst can run for seconds (Bramble Seed grows for six), and a level-up or the swarm
			// arriving mid-burst would cover the thing being filmed. Keep the warmup's housekeeping.
			if (GetTree().Paused)
				DismissLevelUpMenus(this);
			if (clearEnemies)
			{
				foreach (Node n in GetTree().GetNodesInGroup("enemies"))
					if (IsInstanceValid(n))
						n.QueueFree();
			}
			if (stageCount > 0 && walk is Vector2 burstHeading)
				DriveWalk(burstHeading);
			else if (stageCount > 0)
				HoldForStage();
			// The tender's attacks play out across a burst: keep the field clear and him standing.
			if (tenderShot)
				DriveTender();
			if (++burstWait >= burstGap)
			{
				burstWait = 0;
				SaveBurstFrame();
			}
			return;
		}
		if (captured || ++frames < WarmupFrames)
		{
			// A paused tree still draws, so the pause has to be cleared from inside the draw
			// callback rather than from _Process - which does not run while a level-up is up.
			if (captured)
				return;
			if (scrollTo != null && (latePressed || string.IsNullOrEmpty(PressLateButton)))
				ApplyScroll(GetTree().Root);
			if (GetTree().Paused)
				DismissLevelUpMenus(this);
			else if (shoreDirection is Vector2I push)
			{
				// After any --teleport, so the shore is found round where he was sent.
				if (!shorePlaced && frames >= WarmupFrames / 3 && (teleportTo == null || teleported))
					PlaceAtShore(push);
				// Before placement, stand still; after it, walk into the water and keep walking.
				Press("move_right", "move_left", shorePlaced ? push.X : 0f);
				Press("move_down", "move_up", shorePlaced ? push.Y : 0f);
			}
			else if (hold && teleported)
				HoldPosition();
			else if (shove)
				DriveShove();
			else if (ghost)
				DriveGhost();
			else if (tenderShot)
				DriveTender();
			else if (wonEvent != null)
				HoldOnEventSite();
			else if (pinHeat != null || pinStillFor != null)
				HoldStance();
			else if (walk is Vector2 heading)
				DriveWalk(heading);
			else if (statusShowcase && showcase.Count > 0)
				HoldShowcase();
			else if (stageCount > 0)
				HoldForStage();
			else
				DriveMovement();
			if (stageCount > 0 && !stagePlaced && frames >= WarmupFrames - fxLead)
				PlaceStage();
			if (statusShowcase && showcase.Count == 0 && frames >= WarmupFrames / 3)
				BuildShowcase();
			if ((statusShowcase || statusCrowd) && FindNodeOfType<Player>(GetTree().Root) is Player keptAlive)
			{
				keptAlive.MaxHP = System.Math.Max(keptAlive.MaxHP, 100000);
				keptAlive.CurrentHP = keptAlive.MaxHP;
			}
			if (clearEnemies)
			{
				foreach (Node n in GetTree().GetNodesInGroup("enemies"))
					if (IsInstanceValid(n))
						n.QueueFree();
			}
			if (findWater && teleportTo == null && frames >= WarmupFrames / 3)
				FindWater();
			if (winEvent && wonEvent == null && frames >= WarmupFrames / 3)
				StartEvent();
			if (!fxFired && frames >= WarmupFrames - fxLead)
			{
				fxFired = true;
				FireJuice();
			}
			if (gotoTarget != null && teleportTo == null && frames >= WarmupFrames / 3 - 1)
				teleportTo = ResolveGoto();
			if (forceHint && !hintForced && frames >= WarmupFrames - fxLead
				&& FindNodeOfType<SideEventDirector>(GetTree().Root) is SideEventDirector hinted)
			{
				hintForced = true;
				bool played = hinted.Hints != null && hinted.Hints.ForceHint();
				GD.Print(played
					? "_UiShot: hint forced"
					: $"_UiShot: stage {Global.SelectedStageIdx} played no hint (no hint for it, the place is found, or this save owns its discovery - try --fresh-save)");
			}
			if (takeWayfinder && !wayfinderTaken && frames >= WarmupFrames / 3
				&& FindNodeOfType<SideEventDirector>(GetTree().Root) is SideEventDirector finder)
			{
				wayfinderTaken = true;
				if (finder.Wayfinder != null && IsInstanceValid(finder.Wayfinder))
					finder.Wayfinder.Take();
				else
					GD.PrintErr($"_UiShot: stage {Global.SelectedStageIdx} has no wayfinder to take");
			}
			if (arriveEvent && !eventArrived && frames >= WarmupFrames / 3
				&& FindNodeOfType<SideEventDirector>(GetTree().Root) is SideEventDirector arrival)
			{
				eventArrived = true;
				GD.Print(arrival.Start() is SideEvent begun ? $"_UiShot: {begun.GetType().Name} begun where it stands" : "_UiShot: no event to begin");
			}
			if (teleportTo is Vector2 target && !teleported && frames >= WarmupFrames / 3
				&& FindNodeOfType<Player>(GetTree().Root) is Player traveller)
			{
				traveller.GlobalPosition = target;
				teleported = true;
				GD.Print($"_UiShot: teleported the player to {target}");
			}
			if (ForceLevelUp && !levelForced && frames >= WarmupFrames / 2
				&& FindNodeOfType<Player>(GetTree().Root) is Player player)
			{
				levelForced = true;
				player.ForceBoonOnNextOffer = ForceBoon;
				player.ForcedBoonId = forcedBoonId;
				player.AddXp(System.Math.Max(1, player.XPToNextLevel - player.CurrentXP));
			}
			if (levelForced)
				TickGuardProbe();
			if (ShowMutation && levelForced && !mutationShown && frames >= WarmupFrames * 3 / 4
				&& FindNodeOfType<LevelUpMenu>(GetTree().Root) is LevelUpMenu menu
				&& FindNodeOfType<Player>(GetTree().Root) is Player owner
				&& owner.GetEquippedSpells().Count > 0)
			{
				mutationShown = true;
				SpellData spell = owner.GetEquippedSpells()[0];
				var option = new LevelUpOption
				{
					SpellId = spell.Id,
					DisplayName = spell.Name,
					Icon = spell.Icon,
					NextLevel = mutationMilestone,
					MaxLevel = mutationMilestone == 8 ? 8 : 7,
					IsEvolutionMilestone = true,
					MilestoneLevel = apotheosisPage ? ApotheosisCatalog.MilestoneLevel : mutationMilestone,
					EvolutionChoices = System.Linq.Enumerable.ToList(spell.GetEvolutionOptionsForLevel(mutationMilestone)),
				};
				// The spell's current tags, as Player.ApplyElementPreview fills them for a real offer.
				// Without them every card's element line fell back to the hybrid wording ("+12%
				// cooldown"), so a pure spell's deepening and its element path photographed as neither.
				foreach (var pair in spell.GetElementWeights())
					option.SpellElementTags[pair.Key.ToString()] = pair.Value;
				if (lockBranch && option.EvolutionChoices.Count > 1)
					option.EvolutionLockReason[option.EvolutionChoices[^1].Id] = "Requires 4 Darkness tags (1/4)";
				typeof(LevelUpMenu).GetMethod("BuildEvolutionSelectionButtons",
						System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
					?.Invoke(menu, new object[] { option });
			}
			if (mixedDeal && levelForced && !mixedDealt && frames >= WarmupFrames * 3 / 4 - 10
				&& FindNodeOfType<LevelUpMenu>(GetTree().Root) is LevelUpMenu dealMenu && dealMenu.Visible
				&& FindNodeOfType<Player>(GetTree().Root) is Player dealer)
			{
				mixedDealt = true;
				// Upgrades are only in a level-up's pool when its scarcity roll lets them in, and the
				// augury list never has them; open the offer with them in, then add the augury's list
				// for the boons.
				dealer.UpgradeOfferChance = 1f;
				dealer.BeginLevelUpOffer();
				var pool = dealer.GetLevelUpOptions(int.MaxValue).Concat(dealer.GetAuguryCandidates())
					.OrderBy(o => o.DisplayName, System.StringComparer.Ordinal).ToList();
				var hand = new System.Collections.Generic.List<LevelUpOption>();
				LevelUpOption pick(System.Func<LevelUpOption, bool> want) => pool.FirstOrDefault(o => want(o) && !hand.Contains(o));
				if (tallDeal > 0)
				{
					int lines(LevelUpOption o) => StatIcons.Split(o.IsNewUnlock ? o.StatSummary ?? "" : o.UpgradeSummary ?? "").Count;
					hand.AddRange(pool.Where(o => o.SpellId != Player.MarginaliaId)
						.GroupBy(o => o.SpellId).Select(g => g.First())
						.OrderByDescending(lines).ThenByDescending(o => o.DisplayName.Length).Take(tallDeal));
				}
				else
				{
					foreach (LevelUpOption card in new[]
					{
						pick(o => o.IsNewUnlock && !o.IsBoon && o.SpellId != Player.MarginaliaId),
						pick(o => !o.IsNewUnlock && !o.IsBoon),
						pick(o => o.IsBoon),
					})
					{
						if (card != null)
							hand.Add(card);
					}
				}
				GD.Print($"_UiShot: mixed deal - {string.Join(", ", hand.Select(o => $"{o.DisplayName} ({(o.IsBoon ? "boon" : o.IsNewUnlock ? "new" : $"upgrade to {o.NextLevel}")})"))}");
				dealMenu.SetOptions(hand, 1);
			}
			if ((withCharges || withStrip) && levelForced && !levelExtrasShown && frames >= WarmupFrames * 3 / 4 - 6
				&& FindNodeOfType<LevelUpMenu>(GetTree().Root) is LevelUpMenu extrasMenu && extrasMenu.Visible
				&& FindNodeOfType<Player>(GetTree().Root) is Player extrasHolder && extrasHolder.GetEquippedSpells().Count > 0)
			{
				levelExtrasShown = true;
				if (withCharges)
					extrasMenu.RefreshCharges(new LevelUpCharges
					{
						Rerolls = 1, Bans = 2, Banks = 1, Auguries = 1,
						AuguryCandidates = extrasHolder.GetAuguryCandidates(),
					});
				if (withStrip)
				{
					SpellData held = extrasHolder.GetEquippedSpells()[0];
					var ascend = new LevelUpOption
					{
						SpellId = held.Id,
						DisplayName = held.Name,
						Icon = held.Icon,
						NextLevel = 8,
						MaxLevel = 8,
						IsEvolutionMilestone = true,
						MilestoneLevel = 8,
						EvolutionChoices = System.Linq.Enumerable.ToList(held.GetEvolutionOptionsForLevel(8)),
					};
					extrasMenu.SetAscensions(new System.Collections.Generic.List<LevelUpOption> { ascend });
				}
				GD.Print($"_UiShot: level-up extras - charges {withCharges}, strip {withStrip}");
			}
			if (focusCard >= 0 && levelForced && !cardFocused && frames >= WarmupFrames * 3 / 4 + 4
				&& FindNodeOfType<LevelUpMenu>(GetTree().Root) is LevelUpMenu focusMenu)
			{
				// The cards are named OptionCard_<id> by LevelUpMenu, in the order they were dealt.
				var cards = new System.Collections.Generic.List<Button>();
				CollectButtons(focusMenu, "OptionCard_", cards);
				if (focusCard < cards.Count)
				{
					cardFocused = true;
					cards[focusCard].GrabFocus();
					GD.Print($"_UiShot: focus-card {focusCard} of [{string.Join(", ", cards.Select(c => c.Name.ToString()))}] -> {FocusName()}");
				}
			}
			if (FocusBoon && levelForced && !boonFocused && frames >= WarmupFrames * 3 / 4
				&& FindNodeOfType<LevelUpMenu>(GetTree().Root) is LevelUpMenu boonMenu)
			{
				// The boon card is the Button carrying the plaque's clasp (LevelUpMenu.AddBoonClasp).
				Button boonCard = FindButtonWithChild(boonMenu, "BoonClasp");
				if (boonCard != null)
				{
					boonFocused = true;
					boonCard.GrabFocus();
				}
			}
			if (ForceDeath && !deathForced && frames >= WarmupFrames / 2
				&& FindNodeOfType<Player>(GetTree().Root) is Player victim)
			{
				deathForced = true;
				for (int i = 0; i < 6 && !victim.IsDead; i++)
					victim.TakeDamage(System.Math.Max(1, victim.MaxHP * 10));
			}
			if (forceVictory && !deathForced && frames >= WarmupFrames / 2
				&& FindNodeOfType<Node2DGame>(GetTree().Root) is Node2DGame winner)
			{
				deathForced = true;
				winner.FinishRunAndReward(RunOutcome.Victory, BossCatalog.ForStageIndex(Global.SelectedStageIdx)?.Id ?? string.Empty);
			}
			// A level-one wizard stepped straight to a boss dies to it in a second; the photographs are
			// of the rush, so he is kept whole while it steps (until --force-death means otherwise).
			if (cameraZoom > 0f && frames >= WarmupFrames / 4 && FindNodeOfType<Camera2D>(GetTree().Root) is Camera2D runCamera)
				runCamera.Zoom = new Vector2(cameraZoom, cameraZoom);
			if (rushSteps > 0 && !deathForced && FindNodeOfType<Player>(GetTree().Root) is Player rushPlayer && !rushPlayer.IsDead)
				rushPlayer.CurrentHP = rushPlayer.MaxHP;
			if (rushStepsDone < rushSteps && frames >= WarmupFrames / 4 && frames >= rushNextStepFrame
				&& FindNodeOfType<Node2DGame>(GetTree().Root) is Node2DGame rush && rush.IsBossRush)
			{
				rush.DebugAdvanceBossRush();
				rushStepsDone++;
				rushNextStepFrame = frames + RushStepFrames;
				GD.Print($"_UiShot: rush step {rushStepsDone} of {rushSteps}");
			}
			if (demoUnlocks && !demoUnlocksAdded && deathForced
				&& FindNodeOfType<GameOverScreen>(GetTree().Root) is GameOverScreen results && results.Report != null)
			{
				demoUnlocksAdded = true;
				AddDemoUnlocks(results);
			}
			if (scrollBy > 0 && !scrolled && frames >= WarmupFrames * 3 / 4)
			{
				scrolled = true;
				ScrollAll(GetTree().Root);
			}
			if ((demoPlayerLevel > 0 || demoSpellLevel > 0 || demoSpells.Length > 0 || demoEvolutions.Length > 0) && !demoApplied && frames >= WarmupFrames / 4
				&& FindNodeOfType<Player>(GetTree().Root) is Player demo && demo.GetEquippedSpells().Count > 0)
			{
				demoApplied = true;
				if (demoPlayerLevel > 0)
					demo.CurrentLevel = demoPlayerLevel;
				if (demoSpellLevel > 0)
					demo.GetEquippedSpells()[0].CurrentLevel = demoSpellLevel;
				ApplyDemoEvolutions(demo.GetEquippedSpells()[0]);
				foreach (string id in demoSpells)
					if (!demo.TryAddOrLevelSpell(id.Trim()))
						GD.PrintErr($"_UiShot: could not add spell '{id}'");
				GD.Print($"_UiShot: demo state - player level {demo.CurrentLevel}, {demo.GetEquippedSpells()[0].Name} level {demo.GetEquippedSpells()[0].CurrentLevel}");
			}
			// After the demo state (same frame, so --demo-spells are held by now), before the level-up.
			if (seenAscensions != null && !seenApplied && frames >= WarmupFrames / 4
				&& FindNodeOfType<Player>(GetTree().Root) is Player seer && seer.GetEquippedSpells().Count > 0)
				SeedSeenAscensions(seer);
			if (grantBoons != null && !boonsGranted && frames >= WarmupFrames / 4
				&& FindNodeOfType<Player>(GetTree().Root) is Player holder)
			{
				boonsGranted = true;
				foreach (string id in grantBoons)
					if (!holder.TryAddBoon(id))
						GD.PrintErr($"_UiShot: could not grant boon '{id}'");
			}
			if (!reportFilled && (reportName != null || reportText != null || reportSend || reportBack)
				&& FindNodeOfType<ReportBugScreen>(GetTree().Root) is ReportBugScreen report && report.Visible)
				FillReport(report);
			if (OpenPause && !pauseOpened && frames >= WarmupFrames / 2
				&& FindNodeOfType<Node2DGame>(GetTree().Root) is Node2DGame pausable)
			{
				pauseOpened = true;
				typeof(Node2DGame).GetMethod("ToggleEscapeMenu",
						System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
					?.Invoke(pausable, null);
			}
			if (chestDrops.Count > 0 && !chestsDropped && frames >= WarmupFrames / 2
				&& FindNodeOfType<Player>(GetTree().Root) is Player dropper)
			{
				chestsDropped = true;
				foreach (Vector2 offset in chestDrops)
					dropper.GetParent().AddChild(new ChestReward { GlobalPosition = dropper.GlobalPosition + offset });
			}
			// Relics first, then the book: the Blank Folio has to be held before the fill, or the
			// fill stops at six. Both a quarter of the way in, ahead of the chest and the level-up.
			if (!relicsGiven && giveRelics.Length > 0 && frames >= WarmupFrames / 4
				&& FindNodeOfType<Player>(GetTree().Root) is Player relicTaker)
			{
				relicsGiven = true;
				foreach (string relic in giveRelics)
					relicTaker.AddChestItem(relic.Trim());
			}
			if (fillBook && !bookFilled && frames >= WarmupFrames / 4
				&& (relicsGiven || giveRelics.Length == 0)
				&& FindNodeOfType<Player>(GetTree().Root) is Player scribe)
			{
				bookFilled = true;
				FillBook(scribe);
				if (hudMilestones)
					RaiseMilestones(scribe);
			}
			if (cutSpellId != null && !spellCut && bookFilled && frames >= WarmupFrames / 3
				&& FindNodeOfType<Node2DGame>(GetTree().Root) is Node2DGame cutter
				&& FindNodeOfType<Player>(GetTree().Root) is Player cutFrom)
			{
				spellCut = true;
				cutter.OpenChestSelectionMenu();
				if (FindNodeOfType<ChestItemSelectionMenu>(GetTree().Root) is ChestItemSelectionMenu knifeChest)
					knifeChest.EmitSignal(ChestItemSelectionMenu.SignalName.SpellCutChosen, cutSpellId);
				GD.Print($"_UiShot: after the cut the book holds {cutFrom.GetEquippedSpells().Count}/{cutFrom.SpellSlots}: "
					+ string.Join(", ", cutFrom.GetEquippedSpells().Select(s => s.Id))
					+ $"; banned: {string.Join(", ", cutFrom.GetBannedSpellIds())}; knife usable again: {cutFrom.CanUsePenknife}");
			}
			if (crowd > 0 && !crowdPlaced && frames >= WarmupFrames / 3
				&& FindNodeOfType<Node2DGame>(GetTree().Root) is Node2DGame crowdGame
				&& FindNodeOfType<Player>(GetTree().Root) is Player crowdHero)
			{
				crowdPlaced = true;
				var crowdRng = new RandomNumberGenerator { Seed = 5 };
				for (int k = 0; k < crowd; k++)
				{
					float angle = Mathf.Tau * k / crowd + crowdRng.RandfRange(-0.3f, 0.3f);
					crowdGame.SpawnEventEnemy((string)null, crowdHero.GlobalPosition + Vector2.FromAngle(angle) * crowdRng.RandfRange(150f, 280f), 40f, false);
				}
			}
			if (landmarkCensus > 0 && frames >= WarmupFrames / 3
				&& FindNodeOfType<Node2DGame>(GetTree().Root) is Node2DGame census)
			{
				(int pens, int chunks) = census.DebugLandmarkCensus(landmarkCensus);
				GD.Print($"LANDMARKS {pens} flower pens in {chunks} chunks ({pens / (float)chunks:0.000} a chunk) on stage {Global.SelectedStageIdx}");
				GetTree().Quit(0);
				landmarkCensus = 0;
			}
			if (penCamp && !penCampStarted && frames >= WarmupFrames / 2
				&& FindNodeOfType<Player>(GetTree().Root) is Player camper)
			{
				penCampStarted = true;
				GetTree().Root.AddChild(new PenCampProbe { Hero = camper, Game = FindNodeOfType<Node2DGame>(GetTree().Root) });
			}
			if (secretProbe && !secretProbeStarted && frames >= WarmupFrames / 2
				&& FindNodeOfType<SecretDirector>(GetTree().Root) is SecretDirector probeSecrets
				&& FindNodeOfType<Player>(GetTree().Root) is Player secretHero)
			{
				secretProbeStarted = true;
				GetTree().Root.AddChild(new SecretProbe { Director = probeSecrets, Hero = secretHero });
			}
			if (menuQueueProbe && !menuQueueProbeStarted && frames >= WarmupFrames / 2
				&& FindNodeOfType<Node2DGame>(GetTree().Root) is Node2DGame probeGame
				&& FindNodeOfType<Player>(GetTree().Root) is Player probePlayer)
			{
				menuQueueProbeStarted = true;
				GetTree().Root.AddChild(new MenuQueueProbe { Game = probeGame, Hero = probePlayer });
			}
			if (OpenChest && !chestOpened && frames >= WarmupFrames / 2
				&& FindNodeOfType<Node2DGame>(GetTree().Root) is Node2DGame game)
			{
				chestOpened = true;
				Player relicHolder = FindNodeOfType<Player>(GetTree().Root);
				game.OpenChestSelectionMenu();
				// Re-deal the menu the game just built, through the same public call it used.
				if (chestOffer.Length > 0 && relicHolder != null
					&& FindNodeOfType<ChestItemSelectionMenu>(GetTree().Root) is ChestItemSelectionMenu chestMenu)
				{
					chestMenu.SetOptions(chestOffer.Select(id => id.Trim()).ToList(), relicHolder.GetOwnedChestItems());
				}
			}
			return;
		}
		// Late press, then one more settle pass before the shutter. Returning here rather than
		// capturing means the frame counter keeps running and the screen the press opened gets the
		// same warmup the first one had.
		if (!string.IsNullOrEmpty(PressLateButton) && !latePressed)
		{
			latePressed = true;
			Button target = FindButton(GetTree().Root, PressLateButton);
			if (target == null)
			{
				GD.PrintErr($"_UiShot: no button named '{PressLateButton}'");
			}
			else
			{
				target.EmitSignal(BaseButton.SignalName.Pressed);
				frames = 0;
				// The next name in the queue gets its own warmup, like the first.
				if (++pressLateIndex < pressLateQueue.Length)
				{
					PressLateButton = pressLateQueue[pressLateIndex];
					latePressed = false;
				}
				return;
			}
		}
		if (!string.IsNullOrEmpty(focusLate) && !focusLateDone)
		{
			focusLateDone = true;
			Control focusTarget = FindControl(GetTree().Root, focusLate);
			if (focusTarget == null)
				GD.PrintErr($"_UiShot: no focusable control named '{focusLate}'");
			else
			{
				focusTarget.GrabFocus();
				frames = WarmupFrames / 2;
				return;
			}
		}
		// --pad-y: a real Y press and release through Input.ParseInputEvent, the door a pad uses,
		// after the warmup - so the input map, the focused control's GuiInput and whatever it does
		// are all on the path. Written for the pause screen's spell details, which a pad opens with
		// Y because the toggle itself takes no focus (Node2DGame.BuildSpellDetailsToggle).
		if (padY && !padYSent)
		{
			padYSent = true;
			Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.Y, Pressed = true });
			Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = JoyButton.Y, Pressed = false });
			GD.Print($"_UiShot: sent Y to {GetViewport().GuiGetFocusOwner()?.Name ?? "nothing"}");
			frames = 0;
			return;
		}

		// --hover-late=NAME: the pointer arriving on a control, after the warmup - the character
		// select's detail pane follows the hovered card, and a locked card cannot take focus, so a
		// hover is the only way to show a locked or sealed wizard there. Emits the real signal, so
		// the screen's own handler runs.
		if (!string.IsNullOrEmpty(hoverLate) && !hovered)
		{
			hovered = true;
			if (GetTree().Root.FindChild(hoverLate, true, false) is Control hoverTarget)
			{
				hoverTarget.EmitSignal(Control.SignalName.MouseEntered);
				frames = 0;
				return;
			}
			GD.PrintErr($"_UiShot: no control named '{hoverLate}'");
		}

		if (padStep < padScript.Length)
		{
			string step = padScript[padStep++].Trim().ToLowerInvariant();
			if (step == "release")
			{
				foreach (string a in MoveActions)
					Input.ActionRelease(a);
				GD.Print($"_UiShot: pad release (the bot's steering let go) at {FocusName()}");
				frames = 0;
				return;
			}
			JoyButton? button = step switch
			{
				"up" => JoyButton.DpadUp,
				"down" => JoyButton.DpadDown,
				"left" => JoyButton.DpadLeft,
				"right" => JoyButton.DpadRight,
				"a" => JoyButton.A,
				"b" => JoyButton.B,
				"y" => JoyButton.Y,
				_ => null,
			};
			GD.Print($"_UiShot: pad {step} from {FocusName()}");
			if (button is JoyButton pressed)
			{
				Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = pressed, Pressed = true });
				Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = pressed, Pressed = false });
			}
			else
				GD.PrintErr($"_UiShot: --pad step '{step}' is not up, down, left, right, a, b, y or release");
			frames = 0;
			return;
		}
		if (padScript.Length > 0 && padStep == padScript.Length)
		{
			padStep++;
			GD.Print($"_UiShot: pad done, selection on {FocusName()}, tree paused: {GetTree().Paused}");
		}

		if (tapTest && !tapTestDone && RunTapTest())
			return;
		if (guardProbe && !guardProbeDone)
		{
			TickGuardProbe();
			return;
		}

		// A report in flight: hold the shutter for the answer, then give the screen a few frames
		// to lay out the state it changed to. 1800 frames is the 15 s timeout at 60 fps, twice.
		if (reportScreen != null && IsInstanceValid(reportScreen) && reportScreen.IsSending && reportWait < 1800)
		{
			if (++reportWait == 1800)
				GD.PrintErr("_UiShot: the report never came back");
			frames = WarmupFrames - 6;
			return;
		}

		captured = true;

		if (statusShowcase)
			WriteShowcasePositions();
		Report();
		if (shorePlaced)
			ReportShore();

		Image image = GetViewport()?.GetTexture()?.GetImage();
		if (image == null)
		{
			GD.PrintErr("_UiShot: no viewport texture");
			GetTree().Quit(1);
			return;
		}

		string path = $"user://{ShotName}.png";
		// Where the selection is in the picture, and whether the ring is drawn for it at all (it is
		// not while the pointer is the device in use), so a photograph of a focus state can be read.
		GD.Print($"_UiShot: selection at the shutter: {FocusName()}, pointer active: {MenuFocusHighlight.PointerActive}");
		if (gotoTarget == "hazard" || hold)
			GD.Print($"_UiShot: {StageHazard.BitesLanded} stage-hazard bites landed on the player's boots so far");
		Error err = image.SavePng(path);
		GD.Print(err == Error.Ok
			? $"_UiShot: {ProjectSettings.GlobalizePath(path)}"
			: $"_UiShot: save failed ({err})");
		if (inkReport)
			WriteInkReport(image);
		burstTaken = 1;
		// A probe running its own steps quits when it is done, with its own count of failures.
		if (secretProbeStarted || penCampStarted)
			return;
		if (err != Error.Ok || burstCount <= 1)
			GetTree().Quit(err == Error.Ok ? 0 : 1);
	}

	private void FillReport(ReportBugScreen report)
	{
		reportFilled = true;
		reportScreen = report;
		if (reportName != null && report.FindChild("ReportBugName", true, false) is LineEdit name)
			name.Text = reportName;
		if (reportText != null && report.FindChild("ReportBugText", true, false) is TextEdit text)
		{
			text.Text = reportText;
			// Setting Text does not emit TextChanged; the screen's counter and draft listen to it.
			text.EmitSignal(TextEdit.SignalName.TextChanged);
		}
		GD.Print($"_UiShot: filled the report ({reportText?.Length ?? 0} characters)");
		if (reportBack && FindButton(report, "ReportBugBack") is Button back)
		{
			back.EmitSignal(BaseButton.SignalName.Pressed);
			Callable.From(() => GD.Print($"_UiShot: after Back, focus is on "
				+ $"'{GetViewport().GuiGetFocusOwner()?.Name ?? "nothing"}', tree paused: {GetTree().Paused}")).CallDeferred();
			return;
		}
		if (!reportSend)
			return;
		if (FindButton(report, "ReportBugSend") is Button send)
			send.EmitSignal(BaseButton.SignalName.Pressed);
		else
			GD.PrintErr("_UiShot: no ReportBugSend button");
	}

	private void FillBook(Player scribe)
	{
		foreach (string id in UnlockCatalog.AllSpellIds)
		{
			if (scribe.IsSpellBookFull)
				break;
			if (scribe.GetEquippedSpells().Any(s => s != null && s.Id == id))
				continue;
			scribe.TryAddOrLevelSpell(id);
		}
		GD.Print($"_UiShot: book filled to {scribe.GetEquippedSpells().Count}/{scribe.SpellSlots}");
		if (!maxBook)
			return;

		foreach (SpellData spell in scribe.GetEquippedSpells().ToList())
		{
			// A level-4 card without a branch named levels the spell and applies no mutation: fine
			// for a photograph, which only needs every spell at its cap.
			while (spell.CurrentLevel < Player.CardLevelCap(spell) && scribe.TryAddOrLevelSpell(spell.Id)) { }
		}
		foreach (BoonDefinition boon in BoonCatalog.All)
		{
			if (!scribe.CanTakeMoreBoons)
				break;
			scribe.TryAddBoon(boon.Id);
		}
		GD.Print($"_UiShot: every spell at its card cap, {scribe.GetOwnedBoonIds().Count} boons held");
	}

	// --demo-unlocks: one results card of every kind, added to the report the screen was built from.
	// Real ids, so every card resolves its real picture; the save is never touched.
	private void AddDemoUnlocks(GameOverScreen results)
	{
		RunReport report = results.Report;
		var entries = report.Unlocks.Entries;
		bool Has(RunUnlockKind kind) => entries.Any(e => e.Kind == kind);

		CharacterData wizard = CharacterRoster.GetAll()
			.FirstOrDefault(c => c != null && !UnlockCatalog.IsStarter(c.Id, UnlockKind.Character) && c.Portrait != null);
		if (!Has(RunUnlockKind.Wizard) && wizard != null)
			entries.Add(new RunUnlockEntry { Kind = RunUnlockKind.Wizard, Id = wizard.Id, Name = wizard.Name, Source = "Freed during the run" });

		AchievementDefinition spellDeed = AchievementDefinitions.All.FirstOrDefault(d => !string.IsNullOrEmpty(d.SpellUnlockId));
		SpellData spell = spellDeed == null ? null : ContentValidator.LoadAllSpells()
			.FirstOrDefault(s => s.Id.Equals(spellDeed.SpellUnlockId, System.StringComparison.OrdinalIgnoreCase));
		if (!Has(RunUnlockKind.Spell) && spell != null)
			entries.Add(new RunUnlockEntry { Kind = RunUnlockKind.Spell, Id = spell.Id, Name = spell.Name, Source = $"Unlock completed: {spellDeed.DisplayName}" });

		AchievementDefinition boonDeed = AchievementDefinitions.All.FirstOrDefault(d => !string.IsNullOrEmpty(d.BoonUnlockId));
		BoonDefinition boon = boonDeed != null ? BoonCatalog.GetById(boonDeed.BoonUnlockId) : BoonCatalog.All.LastOrDefault();
		if (!Has(RunUnlockKind.Boon) && boon != null)
			entries.Add(new RunUnlockEntry { Kind = RunUnlockKind.Boon, Id = boon.Id, Name = boon.Name, Source = boonDeed != null ? $"Unlock completed: {boonDeed.DisplayName}" : "Unlock completed" });

		BossDefinition boss = BossCatalog.ForStageIndex(Global.SelectedStageIdx);
		StageDefinition opened = boss != null ? BossCatalog.ChapterOpenedBy(boss) : null;
		if (!Has(RunUnlockKind.Chapter) && opened != null)
			entries.Add(new RunUnlockEntry { Kind = RunUnlockKind.Chapter, Id = opened.Id, Name = opened.DisplayName, Source = $"Defeated {boss.DisplayName}" });

		// A path of the first held spell that has one, the way AscensionSightings words it.
		RunSpellSnapshot held = report.Result.EquippedSpells?.FirstOrDefault(s => !s.IsPassive);
		SpellData heldData = held == null ? null : ContentValidator.LoadAllSpells().FirstOrDefault(s => s.Id == held.Id);
		if (heldData != null)
			SpellEvolutionCatalog.EnsureEvolutionCoverage(heldData);
		SpellEvolutionOption branch = heldData?.Level8Options?.FirstOrDefault(b => b != null);
		if (!Has(RunUnlockKind.Ascension) && branch != null)
			entries.Add(new RunUnlockEntry
			{
				Kind = RunUnlockKind.Ascension, Id = $"{held.Id}:{branch.Id}", Name = branch.DisplayName,
				Source = $"Ascension of {held.DisplayName}", SpellId = held.Id, IconPath = EvolutionIcons.PathOrFallback(branch.Id, branch.Icon?.ResourcePath),
			});

		AchievementDefinition purse = AchievementDefinitions.All.FirstOrDefault(d => d.CurrencyReward > 0
			&& string.IsNullOrEmpty(d.SpellUnlockId) && string.IsNullOrEmpty(d.BoonUnlockId) && string.IsNullOrEmpty(d.CharacterUnlockId));
		if (!Has(RunUnlockKind.Deed) && purse != null)
		{
			entries.Add(new RunUnlockEntry { Kind = RunUnlockKind.Deed, Id = purse.Id, Name = purse.DisplayName, Source = purse.Description, Currency = purse.CurrencyReward });
			report.Arcane.UnlockBonus += purse.CurrencyReward;
			report.ArcanePurse += purse.CurrencyReward;
		}

		GD.Print($"_UiShot: demo unlocks - {string.Join(", ", entries.Select(e => $"{e.Kind} {e.Name}"))}");
		results.Present(report);
	}

	// --hud-milestones. A photograph, not a run: the mutation and the ascension are applied straight
	// to the spell (their branch is the first offered), only the Apotheosis goes through the pick.
	private void RaiseMilestones(Player scribe)
	{
		var held = scribe.GetEquippedSpells().Where(s => s != null).ToList();
		var queue = new System.Collections.Generic.List<SpellData>(held);
		// The Apotheosis first, on whichever held spell has a form at all.
		ApotheosisDefinition form = ApotheosisCatalog.All.FirstOrDefault(d => held.Any(s => s.Id == d.SpellId));
		if (form != null)
		{
			SpellData spell = held.First(s => s.Id == form.SpellId);
			queue.Remove(spell);
			spell.CurrentLevel = spell.MaxLevel;
			spell.ApplyEvolution(spell.GetEvolutionOptionsForLevel(4).FirstOrDefault(e => e.Id == form.RequiredLevel4EvolutionId));
			spell.ApplyEvolution(spell.GetEvolutionOptionsForLevel(8).FirstOrDefault(e => e.Id == form.RequiredLevel8EvolutionId));
			if (!scribe.TryAddOrLevelSpell($"{spell.Id}:{form.Form.Id}"))
				GD.PrintErr($"_UiShot: could not take {spell.Id}'s Apotheosis");
		}
		// Then a mutation and an Ascension, on the next spells that have branches to give.
		foreach (int milestone in new[] { 4, 8 })
		{
			SpellData spell = queue.FirstOrDefault(s => s.GetEvolutionOptionsForLevel(4).Count > 0
				&& (milestone == 4 || s.GetEvolutionOptionsForLevel(8).Count > 0));
			if (spell == null)
				continue;
			queue.Remove(spell);
			spell.CurrentLevel = milestone;
			spell.ApplyEvolution(spell.GetEvolutionOptionsForLevel(4)[0]);
			if (milestone == 8)
				spell.ApplyEvolution(spell.GetEvolutionOptionsForLevel(8)[0]);
		}
		scribe.EmitSignal(Player.SignalName.LoadoutChanged);
		GD.Print("_UiShot: milestones - " + string.Join(", ", held.Select(s =>
			$"{s.Id} L{s.CurrentLevel}{(s.IsApotheosis ? " apotheosis" : s.SelectedLevel8Evolution != null ? " ascended" : s.SelectedLevel4Evolution != null ? " mutated" : "")}")));
	}

	// --win-event: the chapter's own event, started now rather than at 90 s on the pace clock.
	private void StartEvent()
	{
		if (FindNodeOfType<SideEventDirector>(GetTree().Root) is not SideEventDirector director)
			return;
		wonEvent = director.Start();
		if (wonEvent == null)
		{
			GD.PrintErr($"_UiShot: stage {Global.SelectedStageIdx} has no side event");
			winEvent = false;
			return;
		}
		if (eventChest)
			wonEvent.PaysChest = true;
		GD.Print($"_UiShot: started {wonEvent.GetType().Name} (pays chest: {wonEvent.PaysChest})");
	}

	// Stands the player on the event's site - the grove's centre, the block, the cell - at full health,
	// so the photograph is of the win where the player would be standing for it.
	private void HoldOnEventSite()
	{
		foreach (string a in MoveActions)
			Input.ActionRelease(a);
		if (FindNodeOfType<Player>(GetTree().Root) is not Player p)
			return;
		p.MaxHP = System.Math.Max(p.MaxHP, 100000);
		p.CurrentHP = p.MaxHP;
		if (IsInstanceValid(wonEvent) && EventSite(wonEvent) is Vector2 site)
			p.GlobalPosition = site;
	}

	// Where the event wants the player: its objective arrow's target, or - for the bog lanterns,
	// whose lanterns carry no objective arrow - the first site not yet done.
	private static Vector2? EventSite(SideEvent sideEvent)
	{
		if (typeof(SideEvent).GetProperty("PointerTarget",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
			?.GetValue(sideEvent) is Vector2 target)
			return target;
		foreach (PointOfInterest site in sideEvent.Sites)
			if (!site.Done)
				return site.Position;
		return null;
	}

	// --goto: where the named placed thing is, plus the offset; null (with a message) if it is not there.
	private Vector2? ResolveGoto()
	{
		Vector2? at = null;
		if (gotoTarget == "hazard")
		{
			// The stage hazard nearest the player, to photograph the traps on any stage that has them.
			Vector2 from = FindNodeOfType<Player>(GetTree().Root)?.GlobalPosition ?? Vector2.Zero;
			float best = float.MaxValue;
			foreach (Node node in GetTree().GetNodesInGroup("stage_hazards"))
			{
				if (node is Node2D hazard && hazard.GlobalPosition.DistanceSquaredTo(from) < best)
				{
					best = hazard.GlobalPosition.DistanceSquaredTo(from);
					at = hazard.GlobalPosition;
				}
			}
		}
		else if (FindNodeOfType<SideEventDirector>(GetTree().Root) is not SideEventDirector director)
			return null;
		else if (gotoTarget == "wayfinder")
			at = director.Wayfinder != null && IsInstanceValid(director.Wayfinder) ? director.Wayfinder.Site.Position : null;
		else if (gotoTarget == "event")
			at = director.Event != null && director.Event.Sites.Count > 0 ? director.Event.Sites[0].Position : null;
		else if (gotoTarget.StartsWith("site:") && int.TryParse(gotoTarget.Substring(5), out int index)
			&& director.Event != null && index >= 0 && index < director.Event.Sites.Count)
			at = director.Event.Sites[index].Position;
		if (at == null)
		{
			GD.PrintErr($"_UiShot: --goto={gotoTarget}: nothing like that on stage {Global.SelectedStageIdx}");
			gotoTarget = null;
			return null;
		}
		GD.Print($"_UiShot: --goto={gotoTarget} is at {at.Value}; standing at {at.Value + gotoOffset}");
		return at.Value + gotoOffset;
	}

	private void FireJuice()
	{
		if (FindNodeOfType<Player>(GetTree().Root) is not Player p)
			return;
		if (dropPotion && GD.Load<PackedScene>("res://scenes/BuffItem.tscn")?.Instantiate() is Node2D potion)
		{
			p.GetParent().AddChild(potion);
			potion.GlobalPosition = p.GlobalPosition;
			GD.Print("_UiShot: dropped a speed potion on the player");
		}
		if (dropHealth && GD.Load<PackedScene>("res://scenes/HealthPickup.tscn")?.Instantiate() is Node2D health)
		{
			p.GetParent().AddChild(health);
			health.GlobalPosition = p.GlobalPosition;
			GD.Print("_UiShot: dropped a health pickup on the player");
		}
		if (castBramble)
		{
			var spell = GD.Load<SpellData>("res://SpellData_BrambleSeed.tres");
			object outcome = typeof(Player).GetMethod("FireBrambleSeed",
					System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
				?.Invoke(p, new object[] { spell, p.BrambleSeedScene });
			GD.Print($"_UiShot: sowed a Bramble Seed ({outcome})");
		}
		if (winEvent && wonEvent != null && IsInstanceValid(wonEvent))
		{
			Vector2 site = EventSite(wonEvent) ?? p.GlobalPosition;
			// The grove is won by its own Tick when its progress fills, with the player standing in
			// it - so fill it and let the real path run. The others are won through Succeed directly.
			var progress = wonEvent.GetType().GetField("progress",
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			if (progress != null && progress.FieldType == typeof(float))
				progress.SetValue(wonEvent, 0.999f);
			else
				typeof(SideEvent).GetMethod("Succeed",
						System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
					?.Invoke(wonEvent, new object[] { site });
			GD.Print($"_UiShot: won {wonEvent.GetType().Name} at {site}");
		}
	}

	// --spell-stage: no input, full health; before the crowd is placed the arena is kept empty so the
	// spell holds its cast for it, and after, nothing but the crowd is left standing.
	private void HoldForStage()
	{
		foreach (string a in MoveActions)
			Input.ActionRelease(a);
		KeepStage();
	}

	// --walk (from --walk-from on): walking that way. With --spell-stage, under the stage's rules.
	private void DriveWalk(Vector2 heading)
	{
		bool go = frames >= walkFromFrame && (walkUntilFrame <= 0 || frames < walkUntilFrame);
		if (walkUntilFrame > 0 && frames == walkUntilFrame && FindNodeOfType<Player>(GetTree().Root) is Player walker)
			GD.Print($"_UiShot: stopped walking at frame {frames}, facing {walker.FacingDirection} (side {walker.FacingSide})");
		Press("move_right", "move_left", go ? heading.X : 0f);
		Press("move_down", "move_up", go ? heading.Y : 0f);
		if (stageCount > 0)
			KeepStage();
	}

	// The stage's rules without the standing still: the player cannot die, nothing but the placed
	// crowd is left on the field, and the crowd's health is topped up as it always was here.
	private void KeepStage()
	{
		if (FindNodeOfType<Player>(GetTree().Root) is Player p)
		{
			p.MaxHP = System.Math.Max(p.MaxHP, 100000);
			p.CurrentHP = p.MaxHP;
		}
		foreach (Node n in GetTree().GetNodesInGroup("enemies"))
			if (n is Enemy e && IsInstanceValid(e) && !stageCrowd.Contains(e))
				e.QueueFree();
		foreach (Enemy e in stageCrowd)
			if (IsInstanceValid(e))
				e.Health = System.Math.Max(e.Health, 1000000);
	}

	private void PlaceStage()
	{
		if (FindNodeOfType<Player>(GetTree().Root) is not Player p)
			return;
		var scene = GD.Load<PackedScene>("res://scenes/enemy.tscn");
		var rng = new RandomNumberGenerator { Seed = 7 };
		Vector2 centre = p.GlobalPosition + stageOffset;
		for (int i = 0; i < stageCount; i++)
		{
			if (scene?.Instantiate() is not Enemy e)
				continue;
			// A sunflower spiral, so the knot is dense in the middle and loose at the edge.
			float a = i * 2.39996f;
			float d = 22f * Mathf.Sqrt(i);
			e.Position = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d + new Vector2(rng.RandfRange(-4f, 4f), rng.RandfRange(-4f, 4f));
			e.Speed = 18f;
			if (!stageMortal)
				e.Health = 1000000;
			p.GetParent().AddChild(e);
			stageCrowd.Add(e);
		}
		stagePlaced = true;
		if (castOnStage)
			p.DebugRipenSpells();
		GD.Print($"_UiShot: spell stage of {stageCrowd.Count} placed at {centre}, player at {p.GlobalPosition}, facing {p.FacingDirection} (side {p.FacingSide})");
	}

	private void ApplyDemoEvolutions(SpellData spell)
	{
		foreach (string raw in demoEvolutions)
		{
			string id = raw.Trim();
			SpellEvolutionOption option = spell.Level4Options?.FirstOrDefault(o => o.Id == id)
				?? spell.Level8Options?.FirstOrDefault(o => o.Id == id)
				?? ApotheosisCatalog.ForForm(id)?.Form;
			if (option == null)
			{
				GD.PrintErr($"_UiShot: {spell.Id} has no evolution '{id}'");
				continue;
			}
			spell.ApplyEvolution(option);
			GD.Print($"_UiShot: {spell.Id} took {option.DisplayName} ({id})");
		}
	}

	private void ScrollAll(Node root)
	{
		if (root is ScrollContainer scroll && scroll.IsVisibleInTree())
			scroll.ScrollVertical = scrollBy;
		foreach (Node child in root.GetChildren())
			ScrollAll(child);
	}

	private void SaveBurstFrame()
	{
		Image image = GetViewport()?.GetTexture()?.GetImage();
		string path = $"user://{ShotName}-{burstTaken}.png";
		Error err = image?.SavePng(path) ?? Error.Failed;
		GD.Print(err == Error.Ok
			? $"_UiShot: {ProjectSettings.GlobalizePath(path)}"
			: $"_UiShot: save failed ({err})");
		burstTaken++;
		if (err != Error.Ok || burstTaken >= burstCount)
			GetTree().Quit(err == Error.Ok ? 0 : 1);
	}

	// Nearest cell C whose shore faces the player: water at C, either side of it and behind it, and
	// four tiles of open, unhazardous land in front three cells wide - a straight stretch of shore,
	// so what is measured is the edge rather than a corner. The player's feet go three tiles back.
	// --find-water: picks the nearest view (from the origin, within 60 tiles) whose middle third of the
	// screen is between a fifth and a half water, and teleports there. Prints the position, so the
	// after photograph can be given the same --teleport. Water decides the shot because water is
	// the rarest thing on most stages and the one most worth looking at.
	private void BuildShowcase()
	{
		if (FindNodeOfType<Player>(GetTree().Root) is not Player p)
			return;
		var candidates = new System.Collections.Generic.List<Enemy>();
		foreach (Node n in GetTree().GetNodesInGroup("enemies"))
			if (n is Enemy e && IsInstanceValid(e) && !e.IsMiniBoss && !e.IsDying)
				candidates.Add(e);
		if (candidates.Count < ShowcaseSlots.Length)
			return;
		// Different kinds where the arena has them, so the marks are seen on several body sizes.
		candidates.Sort((a, b) => string.CompareOrdinal(a.EnemyType, b.EnemyType));
		int step = System.Math.Max(1, candidates.Count / ShowcaseSlots.Length);
		for (int i = 0; i < ShowcaseSlots.Length; i++)
			showcase.Add(candidates[System.Math.Min(candidates.Count - 1, i * step)]);
		showcaseCentre = p.GlobalPosition;
		foreach (Enemy e in showcase)
		{
			e.Speed = 0f;
			e.KnockbackResistance = 1f;
			e.Health = 1000000;
		}
		if (statusBoss && GD.Load<PackedScene>("res://scenes/ForestTreantBoss.tscn")?.Instantiate() is Enemy boss)
		{
			p.GetParent().AddChild(boss);
			boss.Speed = 0f;
			boss.KnockbackResistance = 1f;
			boss.Health = 1000000;
			showcaseBoss = boss;
		}
		GD.Print($"_UiShot: status showcase of {showcase.Count} enemies round {showcaseCentre}");
	}

	// Every frame: the player stands still at full health, each showcase enemy is put back on its
	// slot with its status topped up, and anything else is cleared away.
	private void HoldShowcase()
	{
		foreach (string a in MoveActions)
			Input.ActionRelease(a);
		if (FindNodeOfType<Player>(GetTree().Root) is Player p)
		{
			p.GlobalPosition = showcaseCentre;
			// The boss stands where the player does; the shot is of the boss.
			p.Visible = !statusBoss;
			p.MaxHP = System.Math.Max(p.MaxHP, 100000);
			p.CurrentHP = p.MaxHP;
		}
		foreach (Node n in GetTree().GetNodesInGroup("enemies"))
			if (n is Enemy other && IsInstanceValid(other) && !showcase.Contains(other) && other != showcaseBoss)
				other.QueueFree();

		for (int i = 0; i < showcase.Count; i++)
		{
			Enemy e = showcase[i];
			if (!IsInstanceValid(e))
				continue;
			e.GlobalPosition = showcaseCentre + ShowcaseSlots[i];
			e.Velocity = Vector2.Zero;
			switch (i)
			{
				case 0: e.ApplySlow(0.5f, 2f); break;
				case 1: e.ApplySlow(0f, 2f); break;
				case 2: e.ApplyPoison(0, 2f); break;
				case 3: e.ApplyVulnerable(0.2f, 2f); break;
				case 4: e.ApplyWeaken(0.2f, 2f); break;
				case 5: e.ApplyWither(0.05f); break;
				case 6: e.ApplySlow(0.5f, 2f); e.ApplyPoison(0, 2f); break;
				default:
					e.ApplyVulnerable(0.2f, 2f); e.ApplyPoison(0, 2f);
					e.ApplyWeaken(0.2f, 2f); e.ApplyWither(0.05f);
					break;
			}
		}
		if (showcaseBoss != null && IsInstanceValid(showcaseBoss))
		{
			showcaseBoss.GlobalPosition = showcaseCentre + new Vector2(0f, 10f);
			showcaseBoss.Velocity = Vector2.Zero;
			showcaseBoss.ApplySlow(0.5f, 2f);
			showcaseBoss.ApplyVulnerable(0.2f, 2f);
		}
	}

	// Where each showcase enemy is in the saved picture, in image pixels: world -> canvas (the
	// camera) -> window (the stretch), which is the space the viewport texture is in.
	private void WriteShowcasePositions()
	{
		Viewport vp = GetViewport();
		Transform2D toImage = vp.GetFinalTransform() * vp.GetCanvasTransform();
		float pxPerWorld = toImage.X.Length();
		var sb = new System.Text.StringBuilder();
		sb.Append("{\"px_per_world\": ").Append(pxPerWorld.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(", \"enemies\": [");
		bool first = true;
		void Add(string name, Enemy e)
		{
			if (e == null || !IsInstanceValid(e))
				return;
			Vector2 at = toImage * e.GlobalPosition;
			Vector2 scale = e.GlobalScale.Abs();
			StatusFxGeometry geo = e.StatusFxGeometry;
			if (!first)
				sb.Append(", ");
			first = false;
			sb.Append(System.FormattableString.Invariant(
				$"{{\"name\": \"{name}\", \"type\": \"{e.EnemyType}\", \"x\": {at.X:0.0}, \"y\": {at.Y:0.0}, \"top\": {(geo.HeadY * scale.Y * pxPerWorld):0.0}, \"bottom\": {(geo.FeetY * scale.Y * pxPerWorld):0.0}}}"));
		}
		for (int i = 0; i < showcase.Count; i++)
			Add(ShowcaseNames[i], showcase[i]);
		Add("boss: slow+vulnerable", showcaseBoss);
		sb.Append("]}");
		string path = $"user://{ShotName}.status.json";
		using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
		file?.StoreString(sb.ToString());
		GD.Print($"_UiShot: showcase positions {ProjectSettings.GlobalizePath(path)}");
	}

	private void FindWater()
	{
		findWater = false;
		if (Node2DGame.Current?.Terrain is not TerrainStreamer terrain)
			return;
		float tile = terrain.TilePixels;
		bool Water(Vector2I c) => terrain.IsBlockedAtWorld(terrain.CellOriginWorld(c) + new Vector2(tile, tile) * 0.5f);
		const int Reach = 60, HalfW = 8, HalfH = 5;
		int bestDistance = int.MaxValue;
		Vector2I? best = null;
		for (int dy = -Reach; dy <= Reach; dy += 2)
		{
			for (int dx = -Reach; dx <= Reach; dx += 2)
			{
				int distance = dx * dx + dy * dy;
				if (distance >= bestDistance || distance < 15 * 15)
					continue;
				int wet = 0, all = 0;
				for (int y = -HalfH; y <= HalfH; y++)
					for (int x = -HalfW; x <= HalfW; x++, all++)
						if (Water(new Vector2I(dx + x, dy + y)))
							wet++;
				float share = wet / (float)all;
				if (share < 0.2f || share > 0.5f)
					continue;
				bestDistance = distance;
				best = new Vector2I(dx, dy);
			}
		}
		if (best is Vector2I found)
		{
			// Stand on the nearest dry cell to the middle of that view, not in the lake.
			Vector2I dry = found;
			for (int r = 0; r < 8 && Water(dry); r++)
				for (int y = -r; y <= r && Water(dry); y++)
					for (int x = -r; x <= r; x++)
						if (!Water(found + new Vector2I(x, y))) { dry = found + new Vector2I(x, y); break; }
			found = dry;
			teleportTo = terrain.CellOriginWorld(found) + new Vector2(tile, tile) * 0.5f;
			GD.Print($"_UiShot: --find-water chose --teleport={teleportTo.Value.X:0},{teleportTo.Value.Y:0}");
		}
		else
			GD.PrintErr("_UiShot: --find-water found no view with water within 60 tiles");
	}

	private bool findWater;

	private void PlaceAtShore(Vector2I d)
	{
		if (Node2DGame.Current?.Terrain is not TerrainStreamer terrain
			|| FindNodeOfType<Player>(GetTree().Root) is not Player player)
			return;

		float tile = terrain.TilePixels;
		Vector2 Centre(Vector2I c) => terrain.CellOriginWorld(c) + new Vector2(tile, tile) * 0.5f;
		// Water or maze wall: the two things the feet are stopped by - or, with --shore-fence, a fence.
		bool Water(Vector2I c) => shoreFence
			? terrain.IsFenceAtWorld(Centre(c))
			: terrain.IsBlockedAtWorld(Centre(c)) || terrain.IsWallAtWorld(Centre(c));
		bool Open(Vector2I c)
		{
			Vector2 w = Centre(c);
			return !terrain.IsBlockedAtWorld(w) && !terrain.IsWallAtWorld(w) && !terrain.IsHazardAtWorld(w)
				&& !terrain.IsFenceAtWorld(w);
		}

		var side = new Vector2I(System.Math.Abs(d.Y), System.Math.Abs(d.X));
		Vector2I from = terrain.CellOfWorld(player.FootPosition);
		int bestDistance = int.MaxValue;
		Vector2I? best = null;
		const int Reach = 60;
		for (int dy = -Reach; dy <= Reach; dy++)
		{
			for (int dx = -Reach; dx <= Reach; dx++)
			{
				int distance = dx * dx + dy * dy;
				if (distance >= bestDistance)
					continue;
				Vector2I c = from + new Vector2I(dx, dy);
				// A fence is one cell deep, so there is nothing to require behind it.
				if (!Water(c) || !Water(c + side) || !Water(c - side) || (!shoreFence && !Water(c + d)))
					continue;
				bool clear = true;
				for (int k = 1; k <= 4 && clear; k++)
					for (int s = -1; s <= 1 && clear; s++)
						clear = Open(c - d * k + side * s);
				if (!clear)
					continue;
				bestDistance = distance;
				best = c;
			}
		}

		if (best is not Vector2I found)
		{
			GD.PrintErr($"_UiShot: no straight shore (water or wall) {d} within {Reach} tiles of {from}");
			shoreDirection = null;
			return;
		}

		shoreCell = found;
		shorePlaced = true;
		Vector2 feet = Centre(found - d * 3);
		player.GlobalPosition += feet - player.FootPosition;
		GD.Print($"_UiShot: shore cell {found}, player's feet put at {feet}, walking {d}");
	}

	// How far the drawn boots stopped from the water's collision line: positive is dry land between
	// them, negative is boot over water. Measured off the pixels of the frame on screen - the cell's
	// bottom six rows, which is the boots and the contact line under them - not off any collider,
	// so it cannot agree with the physics just because it was computed from it.
	private void ReportShore()
	{
		if (Node2DGame.Current?.Terrain is not TerrainStreamer terrain
			|| FindNodeOfType<Player>(GetTree().Root) is not Player player
			|| player.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D") is not AnimatedSprite2D sprite
			|| sprite.SpriteFrames?.GetFrameTexture(sprite.Animation, sprite.Frame) is not Texture2D texture)
			return;

		Image image = texture.GetImage();
		if (image.IsCompressed())
			image.Decompress();
		const int BootRows = 6;
		int w = image.GetWidth(), h = image.GetHeight();
		int minX = w, maxX = -1;
		for (int y = h - BootRows; y < h; y++)
			for (int x = 0; x < w; x++)
				if (image.GetPixel(x, y).A > 0.5f)
				{
					minX = System.Math.Min(minX, x);
					maxX = System.Math.Max(maxX, x);
				}
		if (maxX < 0)
			return;

		Vector2 scale = sprite.GlobalScale;
		float left = (minX - w / 2f) * scale.X, right = (maxX + 1 - w / 2f) * scale.X;
		if (sprite.FlipH)
			(left, right) = (-right, -left);
		float bootLeft = sprite.GlobalPosition.X + left;
		float bootRight = sprite.GlobalPosition.X + right;
		float bootBottom = sprite.GlobalPosition.Y + (h / 2f) * scale.Y;
		float bootTop = sprite.GlobalPosition.Y + (h / 2f - BootRows) * scale.Y;

		// The line is FOUND, not assumed: the water's collision polygons are the same ones its shader
		// draws (WaterShape), and they wander, so a fixed inset would measure the wrong place. Probe
		// outward from the boots' leading edge, across their width, for the first point on the
		// terrain layer; the nearest is where the feet would meet the shore.
		float tile = terrain.TilePixels;
		Vector2 cell = terrain.CellOriginWorld(shoreCell);
		bool wall = terrain.IsWallAtWorld(cell + new Vector2(tile, tile) * 0.5f);
		Vector2I d = shoreDirection ?? Vector2I.Zero;
		PhysicsDirectSpaceState2D space = player.GetWorld2D().DirectSpaceState;
		var query = new PhysicsPointQueryParameters2D { CollisionMask = 1u << (LevelTilePainter.TerrainCollisionLayer - 1) };
		bool Solid(Vector2 p)
		{
			query.Position = p;
			return space.IntersectPoint(query, 1).Count > 0;
		}
		(string edge, float lead) = (d.X, d.Y) switch
		{
			(0, 1) => ("bottom", bootBottom),
			(0, -1) => ("top", bootTop),
			(-1, 0) => ("left", bootLeft),
			_ => ("right", bootRight),
		};
		var dir = new Vector2(d.X, d.Y);
		float a0 = d.X == 0 ? bootLeft : bootTop, a1 = d.X == 0 ? bootRight : bootBottom;
		float clearance = float.MaxValue;
		for (float t = a0; t <= a1; t += 1f)
		{
			for (float s = -20f; s <= 40f; s += 0.25f)
			{
				Vector2 p = (d.X == 0 ? new Vector2(t, lead) : new Vector2(lead, t)) + dir * s;
				if (!Solid(p)) continue;
				clearance = System.Math.Min(clearance, s);
				break;
			}
		}
		GD.Print($"_UiShot: SHORE {d}: boots x {bootLeft:0.0}..{bootRight:0.0}, y {bootTop:0.0}..{bootBottom:0.0};"
			+ $" footprint centre {player.FootPosition}; {(wall ? "wall" : "water")} collision found"
			+ $" {(clearance == float.MaxValue ? "nowhere within 40 px" : $"{clearance:0.0} px beyond the boots' {edge} edge")}"
			+ " (negative = boots over the water)");
	}

	private static string Truncate(string text)
	{
		text = text?.Replace('\n', ' ') ?? string.Empty;
		return text.Length <= 40 ? text : text.Substring(0, 40) + "...";
	}

	private void Report()
	{
		Vector2 viewport = GetViewport().GetVisibleRect().Size;
		GD.Print($"_UiShot: viewport {viewport.X}x{viewport.Y}");

		int overflow = 0;
		int tiny = 0;
		Walk(GetChild(0), viewport, ref overflow, ref tiny);

		GD.Print($"_UiShot: {overflow} control(s) wider than the viewport, {tiny} below {MinReadableFontSize}px");

		// Menu pixel art (PixelArtRect) picks its scale from the window, so say what it picked: on a
		// fractional window the canvas scale and the screen scale differ, and the screen one is the
		// one that has to be whole.
		var art = new System.Collections.Generic.List<PixelArtRect>();
		CollectPixelArt(GetChild(0), art);
		if (art.Count > 0)
		{
			float stretch = GetViewport().GetFinalTransform().X.Length();
			GD.Print($"_UiShot: window stretch x{stretch:0.####}; pixel art at "
				+ string.Join(", ", art.Select(a => $"x{a.CanvasScale}->{a.ScreenPixelsPerArtPixel}px")));
		}
		if (FindNodeOfType<LevelUpMenu>(GetTree().Root) is LevelUpMenu levelUp && levelUp.Visible)
			ReportCards(levelUp);

		// The chest screen's one rule is that a PC chest fits without scrolling, so say by how much
		// each page fits or overflows rather than leaving it to the eye.
		if (FindNodeOfType<ChestItemSelectionMenu>(GetTree().Root) is ChestItemSelectionMenu chest
			&& chest.FindChild("CardsScroll", true, false) is ScrollContainer cards)
		{
			foreach (Node page in cards.FindChildren("Relic_*", "", true, false))
			{
				if (page is not Control c)
					continue;
				GD.Print($"_UiShot: chest page {c.Name} {c.Size.X:0}x{c.Size.Y:0}, needs {c.GetCombinedMinimumSize().Y:0} of {cards.Size.Y:0}");
				// Where the height goes: each row of the page's content column.
				if (c.GetChildren().OfType<MarginContainer>().FirstOrDefault()?.GetChildOrNull<VBoxContainer>(0) is VBoxContainer column)
				{
					foreach (Node row in column.GetChildren())
					{
						if (row is Control r)
							GD.Print($"_UiShot:     {r.Name} ({r.GetType().Name}) {r.GetCombinedMinimumSize().Y:0}");
					}
				}
			}
		}
	}

	private static void CollectPixelArt(Node node, System.Collections.Generic.List<PixelArtRect> into)
	{
		if (node is PixelArtRect art && art.IsVisibleInTree())
			into.Add(art);
		foreach (Node child in node.GetChildren())
			CollectPixelArt(child, into);
	}

	// THE CARD REPORT: where every level-up and branch card is, which frame it wears, and where its
	// content ends - in canvas units - so the gap between the last row
	// on a card and the frame's inner border can be measured from the pixels of the shot
	// (.ai/level-up-screen.md, "The foot of a card"); the photograph's scale is its width over the
	// "viewport" line above. A card is any Button wearing a family frame:
	// its "normal" stylebox is a texture out of assets/bonelight/ui/. On a PC the content is the
	// Button's child; on a phone the Button is a sibling of it, so the content is the parent's.
	// Also says whether the cards fit: the column's minimum against the panel on a PC, the scroll's
	// content against the scroll on a phone.
	private static readonly string[] CardFrames =
		{ "ui-spell-page", "ui-boon-plaque", "ui-obsidian-plate", "ui-ember-plate" };
	private static readonly string[] CardOverlays = { "WaxSeal", "BoonClasp", "AscensionSheen", "MutationGlow" };

	private void ReportCards(LevelUpMenu menu)
	{
		var buttons = new System.Collections.Generic.List<Button>();
		CollectAllButtons(menu, buttons);
		foreach (Button card in buttons)
		{
			if (!card.IsVisibleInTree() || card.GetThemeStylebox("normal") is not StyleBoxTexture frame
				|| frame.Texture == null)
				continue;
			string file = System.IO.Path.GetFileNameWithoutExtension(frame.Texture.ResourcePath);
			if (!CardFrames.Any(f => file.StartsWith(f)))
				continue;
			// The strip's ASCEND buttons wear the plate too; they are a line of text, not a card.
			if (card.Size.Y < 100f)
				continue;

			Node contentRoot = card.GetParent() is PanelContainer row ? row : card;
			float contentBottom = -1f;
			float chipBottom = -1f;
			LowestContent(contentRoot, card, ref contentBottom, ref chipBottom);
			Rect2 r = card.GetGlobalRect();
			GD.Print($"_UiShot: card {card.Name} frame={file} rect={r.Position.X:0.0},{r.Position.Y:0.0},{r.Size.X:0.0},{r.Size.Y:0.0}"
				+ $" content_bottom={contentBottom:0.0} chip_bottom={chipBottom:0.0}");
		}

		if (menu.GetNodeOrNull<Control>("Panel") is Control panel
			&& panel.GetNodeOrNull<Control>("VBoxContainer") is Control column && column.Visible)
		{
			GD.Print($"_UiShot: cards fit - panel {panel.Size.Y:0}, column needs {column.GetCombinedMinimumSize().Y:0}"
				+ $" from {column.Position.Y:0}, ends at {column.Position.Y + column.Size.Y:0}");
		}
		foreach (Node n in menu.FindChildren("*", "ScrollContainer", true, false))
		{
			if (n is ScrollContainer scroll && scroll.IsVisibleInTree() && scroll.GetChildCount() > 0
				&& scroll.GetChild(0) is Control inner)
				GD.Print($"_UiShot: cards fit - scroll {scroll.Name} {scroll.Size.Y:0}, content needs {inner.GetCombinedMinimumSize().Y:0}"
					+ $" ({(inner.GetCombinedMinimumSize().Y > scroll.Size.Y + 0.5f ? "SCROLLS" : "fits")})");
		}
	}

	private static void CollectAllButtons(Node node, System.Collections.Generic.List<Button> into)
	{
		if (node is Button b)
			into.Add(b);
		foreach (Node child in node.GetChildren())
			CollectAllButtons(child, into);
	}

	// The lowest visible content on a card: labels, chips, icons, pips, boxes - never the card
	// Button itself, its overlays (tail, seal, bail, sheen) or an empty spacer.
	private static void LowestContent(Node node, Button card, ref float bottom, ref float chipBottom)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child == card || child is not Control c || !c.Visible || CardOverlays.Contains(c.Name.ToString()))
				continue;
			bool leaf = c is Label || c is ElementChip || c is TextureRect || c is LevelPips
				|| (c is PanelContainer && c.GetThemeStylebox("panel") is not StyleBoxEmpty);
			if (leaf)
			{
				float b = c.GetGlobalRect().End.Y;
				bottom = Mathf.Max(bottom, b);
				if (c is ElementChip)
					chipBottom = Mathf.Max(chipBottom, b);
			}
			if (c is not ElementChip)
				LowestContent(c, card, ref bottom, ref chipBottom);
		}
	}

	private void Walk(Node node, Vector2 viewport, ref int overflow, ref int tiny)
	{
		if (node is Control control)
		{
			// A control wider than the viewport is the horizontal-scroll bug in its raw form: the
			// content simply does not fit the 720 units keep_width pins us to.
			if (control.Size.X > viewport.X + 0.5f)
			{
				overflow++;
				GD.Print($"  WIDE  {control.GetPath()}  {control.Size.X:0}px > {viewport.X:0}px"
					+ $"  (min {control.GetCombinedMinimumSize().X:0})");
				// A container cannot shrink below its widest child's MINIMUM, so the child with the
				// largest combined minimum is the one actually setting the width. Naming it turns
				// "this screen is too wide" into "this label is too wide", which is fixable.
				Control worst = null;
				foreach (Node child in control.GetChildren())
				{
					if (child is not Control c)
						continue;
					if (worst == null || c.GetCombinedMinimumSize().X > worst.GetCombinedMinimumSize().X)
						worst = c;
				}
				if (worst != null)
					GD.Print($"        widest child: {worst.Name} ({worst.GetType().Name}) min {worst.GetCombinedMinimumSize().X:0}"
						+ (worst is Label l ? $"  text=\"{Truncate(l.Text)}\"" : string.Empty));
			}

			if (control is ScrollContainer scroll
				&& scroll.HorizontalScrollMode != ScrollContainer.ScrollMode.Disabled)
			{
				GD.Print($"  HSCROLL  {scroll.GetPath()}  mode={scroll.HorizontalScrollMode}");
			}

			// GetThemeFontSize resolves the whole chain - local override, then theme, then
			// default - so this is the size actually drawn rather than the size someone asked for.
			if (control is Label or Button or RichTextLabel)
			{
				int size = control.GetThemeFontSize("font_size");
				if (size > 0 && size < MinReadableFontSize)
				{
					tiny++;
					GD.Print($"  TINY  {control.GetPath()}  {size}px");
				}
			}
		}

		foreach (Node child in node.GetChildren())
			Walk(child, viewport, ref overflow, ref tiny);
	}

	// --menu-queue-probe (issue #44, 2026-10-28): one step a frame, since a claimed menu is freed at the
	// end of the frame it closes in. Runs while paused - which is the point - so it is its own node.
	private partial class MenuQueueProbe : Node
	{
		public Node2DGame Game;
		public Player Hero;
		private int step;
		private int failures;

		public override void _Ready()
		{
			ProcessMode = ProcessModeEnum.Always;
		}

		private int Live<T>() where T : Node
		{
			int count = 0;
			foreach (Node node in GetTree().Root.FindChildren("*", "", true, false))
				if (node is T && !node.IsQueuedForDeletion() && (node is not CanvasLayer layer || layer.Visible))
					count++;
			return count;
		}

		private T First<T>() where T : Node
		{
			foreach (Node node in GetTree().Root.FindChildren("*", "", true, false))
				if (node is T typed && !node.IsQueuedForDeletion() && (node is not CanvasLayer layer || layer.Visible))
					return typed;
			return null;
		}

		private void Expect(string what, int chests, int levelUps, bool paused)
		{
			int c = Live<ChestItemSelectionMenu>();
			int l = Live<LevelUpMenu>();
			bool p = GetTree().Paused;
			bool ok = c == chests && l == levelUps && p == paused;
			if (!ok)
				failures++;
			GD.Print($"MENUQUEUE {(ok ? "PASS" : "FAIL")} {what}: chest menus {c} (want {chests}), level-up menus {l} (want {levelUps}), paused {p} (want {paused})");
		}

		private void ClaimChest() => First<ChestItemSelectionMenu>()?.EmitSignal(ChestItemSelectionMenu.SignalName.ItemSelected, ChestItemCatalog.Lodestone);
		private void SkipLevelUp() => First<LevelUpMenu>()?.EmitSignal(LevelUpMenu.SignalName.SkipRequested);

		public override void _Process(double delta)
		{
			switch (step++)
			{
				case 0: Game.OpenChestSelectionMenu(); Game.OpenChestSelectionMenu(); break;
				case 1: Expect("two chests in one step: one screen, one waiting", 1, 0, true); ClaimChest(); break;
				case 2: Expect("first chest claimed: the waiting chest opens", 1, 0, true); ClaimChest(); break;
				case 3: Expect("second chest claimed: the run resumes", 0, 0, false); Hero.LevelUpImmediately(); Game.OpenChestSelectionMenu(); break;
				case 4: Expect("a level-up and a chest in one step: the level-up, the chest waiting", 0, 1, true); SkipLevelUp(); break;
				case 5: Expect("level-up closed: the chest opens, still paused", 1, 0, true); ClaimChest(); break;
				case 6: Expect("chest claimed: the run resumes", 0, 0, false); Hero.LevelUpImmediately(); Hero.LevelUpImmediately(); break;
				case 7: Expect("two level-ups in one step: one screen", 0, 1, true); SkipLevelUp(); break;
				case 8: Expect("first level-up closed: the second opens", 0, 1, true); SkipLevelUp(); break;
				case 9:
					Expect("second level-up closed: the run resumes", 0, 0, false);
					GD.Print($"MENUQUEUE {(failures == 0 ? "ALL PASS" : $"{failures} FAILED")}");
					GetTree().Quit(failures);
					break;
			}
		}
	}

	// --pen-camp: the wizard held still inside a pen beside its east rail; every second, the enemies
	// outside the fence and within 220 px of it that moved under 8 px in that second are "stuck".
	private partial class PenCampProbe : Node
	{
		public Player Hero;
		public Node2DGame Game;
		private Landmark pen;
		private Vector2 camp;
		private readonly RandomNumberGenerator rng = new() { Seed = 7 };
		private float clock;
		private float sampleAt = 1f;
		private readonly System.Collections.Generic.Dictionary<ulong, Vector2> last = new();
		private int samples, stuckSum, stuckMax, nearSum, insideSum, railSum, reachedSum;
		private System.Collections.Generic.List<Rect2> rails;

		public override void _Process(double delta)
		{
			foreach (string action in new[] { "move_left", "move_right", "move_up", "move_down" })
				Input.ActionRelease(action);
			if (pen == null)
			{
				foreach (Node node in GetTree().GetNodesInGroup(Landmark.GroupName))
					if (node is Landmark l && (pen == null || l.Centre.DistanceTo(Hero.GlobalPosition) < pen.Centre.DistanceTo(Hero.GlobalPosition)))
						pen = l;
				if (pen == null)
				{
					GD.Print("PENCAMP no flower pen streamed in");
					GetTree().Quit(1);
					return;
				}
				// In from the east rail, a little over a quarter of the way down: off every gate.
				camp = new Vector2(pen.Bounds.End.X - 60f, pen.Bounds.Position.Y + pen.Bounds.Size.Y * 0.28f);
				// The rails' collision in world pixels: the plot's corner is half a cell out from the fence line.
				Vector2 plot = pen.Bounds.Position - new Vector2(24f, 24f);
				rails = new System.Collections.Generic.List<Rect2>();
				foreach (Rect2 rail in FlowerPenLayout.RailRects(48))
					rails.Add(new Rect2(rail.Position + plot, rail.Size));
				GD.Print($"PENCAMP pen {pen.Key} bounds {pen.Bounds.Size.X:0}x{pen.Bounds.Size.Y:0}, {pen.Gates.Length} gates; camping at {camp}");
			}
			Hero.GlobalPosition = camp;
			Hero.Velocity = Vector2.Zero;
			Hero.CurrentHP = Hero.MaxHP;
			clock += (float)delta;
			if (clock < sampleAt)
				return;
			sampleAt += 1f;

			// A late-run crowd: up to 70 tough bodies, topped up from a ring round the camp every
			// second, so the fence meets a swarm rather than the four a first minute brings.
			int alive = 0;
			foreach (Node node in EnemyRegistry.Snapshot)
				if (node is Enemy live && IsInstanceValid(live) && !live.IsDying)
					alive++;
			for (int k = alive; k < 70 && k < alive + 14; k++)
			{
				float angle = rng.RandfRange(0f, Mathf.Tau);
				Game?.SpawnEventEnemy((string)null, camp + Vector2.FromAngle(angle) * rng.RandfRange(700f, 860f), 40f, false);
			}

			Rect2 outer = pen.Bounds.Grow(220f);
			int stuck = 0, near = 0, inside = 0, onRail = 0, reached = 0;
			var seen = new System.Collections.Generic.Dictionary<ulong, Vector2>();
			foreach (Node node in EnemyRegistry.Snapshot)
			{
				if (node is not Enemy e || !IsInstanceValid(e) || e.IsDying)
					continue;
				Vector2 at = e.GlobalPosition;
				ulong id = e.GetInstanceId();
				seen[id] = at;
				if (at.DistanceTo(camp) < 90f)
					reached++;
				bool still = last.TryGetValue(id, out Vector2 was) && was.DistanceTo(at) < 8f;
				if (still)
				{
					foreach (Rect2 rail in rails)
					{
						if (rail.Grow(40f).HasPoint(at))
						{
							onRail++;
							break;
						}
					}
				}
				if (pen.Bounds.HasPoint(at))
				{
					inside++;
					continue;
				}
				if (!outer.HasPoint(at))
					continue;
				near++;
				if (last.TryGetValue(id, out Vector2 before) && before.DistanceTo(at) < 8f)
					stuck++;
			}
			last.Clear();
			foreach (var kv in seen)
				last[kv.Key] = kv.Value;
			if (clock < 8f)
				return;
			samples++;
			stuckSum += stuck;
			stuckMax = System.Math.Max(stuckMax, stuck);
			nearSum += near;
			insideSum += inside;
			railSum += onRail;
			reachedSum += reached;
			if (clock >= 30f)
			{
				GetViewport().GetTexture().GetImage().SavePng(OS.GetUserDataDir() + "/pencamp.png");
				GD.Print($"PENCAMP over {samples} s: stalled on a rail {railSum / (float)samples:0.0} a second, within 90 px of the wizard {reachedSum / (float)samples:0.0}; "
					+ $"stalled outside {stuckSum / (float)samples:0.0} (max {stuckMax}), outside within 220 px {nearSum / (float)samples:0.0}, "
					+ $"inside the pen {insideSum / (float)samples:0.0}, level {Hero.CurrentLevel}");
				GetTree().Quit(0);
				SetProcess(false);
			}
		}
	}

	// --secret-probe: one step a frame or a held wait, the player moved by hand (he is made immortal,
	// and the swarm is cleared at each step so a hit cannot knock the seed loose mid-probe).
	private partial class SecretProbe : Node
	{
		public SecretDirector Director;
		public Player Hero;
		private int step;
		private int failures;
		private int shots;
		private float wait;
		private float lapAngle;
		private Landmark pen;
		private int hpBefore;
		private int maxBefore;

		private void Check(string what, bool ok)
		{
			if (!ok)
				failures++;
			GD.Print($"SECRETPROBE {(ok ? "PASS" : "FAIL")} {what}");
		}

		private void Shot()
		{
			Image image = GetViewport().GetTexture().GetImage();
			string path = OS.GetUserDataDir() + $"/secret-{++shots}.png";
			image.SavePng(path);
		}

		private void Clear()
		{
			// The tool's own bot still holds whatever it last pressed; the probe moves him by hand.
			foreach (string action in new[] { "move_left", "move_right", "move_up", "move_down" })
				Input.ActionRelease(action);
			foreach (Node node in EnemyRegistry.Snapshot)
				if (node is Enemy e && IsInstanceValid(e))
					e.QueueFree();
			Hero.MaxHP = System.Math.Max(Hero.MaxHP, 20);
		}

		public override void _Process(double delta)
		{
			float dt = (float)delta;
			if (wait > 0f)
			{
				wait -= dt;
				return;
			}
			Clear();
			switch (step)
			{
				case 0:
				{
					Landmark nearest = null;
					foreach (Node node in GetTree().GetNodesInGroup(Landmark.GroupName))
						if (node is Landmark l && l.HasNode("FencedBloom")
							&& (nearest == null || l.Centre.DistanceTo(Hero.GlobalPosition) < nearest.Centre.DistanceTo(Hero.GlobalPosition)))
							nearest = l;
					Check("a flower pen streamed in with a Fenced Bloom on it", nearest != null);
					if (nearest == null)
					{
						GetTree().Quit(failures);
						return;
					}
					pen = nearest;
					Hero.CurrentHP = System.Math.Max(1, Hero.MaxHP / 4);
					hpBefore = Hero.CurrentHP;
					Hero.GlobalPosition = Lap(0f);
					step++;
					wait = 0.4f;
					return;
				}
				case 1:
					Shot();
					step++;
					return;
				case 2:
					// Round the outside of the fence, a twentieth of a lap a frame.
					lapAngle += Mathf.Tau / 120f;
					Hero.GlobalPosition = Lap(lapAngle);
					if (lapAngle >= Mathf.Tau * 0.5f && shots == 1)
						Shot();
					if (Director.StateOf(pen.Key).Done || lapAngle > Mathf.Tau * 1.3f)
					{
						Check($"one lap round the fence opens the bloom (state {Director.StateOf(pen.Key).Progress:0.00})", Director.StateOf(pen.Key).Done);
						Check($"the bloom heals by half ({hpBefore} -> {Hero.CurrentHP} of {Hero.MaxHP})", Hero.CurrentHP >= hpBefore + Hero.MaxHP / 2 - 1);
						step++;
						wait = 0.5f;
					}
					return;
				case 3:
				{
					Shot();
					SecretSeed seed = FindChildOfType<SecretSeed>(Director.Game);
					Check("the bloom dropped a seed", seed != null);
					if (seed == null)
					{
						GetTree().Quit(failures);
						return;
					}
					Hero.GlobalPosition = seed.GlobalPosition;
					step++;
					wait = 0.3f;
					return;
				}
				case 4:
					Check("walking onto the seed picks it up", Director.Carried != null);
					Check("the keepsake was placed", Director.Keepsake != null);
					if (Director.Keepsake == null || Director.Carried == null)
					{
						GetTree().Quit(failures);
						return;
					}
					GD.Print($"SECRETPROBE the First Tree is {Director.Keepsake.GlobalPosition.DistanceTo(Director.Game.StageOrigin) / 1536f:0.0} chunks out");
					// Carrying, a little way short of the bed so the picture shows the seed leaning home.
					Hero.GlobalPosition = Director.Keepsake.GlobalPosition + new Vector2(-260f, 120f);
					step++;
					wait = 0.8f;
					return;
				case 5:
					Shot();
					maxBefore = Hero.MaxHP;
					Hero.GlobalPosition = Director.Keepsake.GlobalPosition;
					step++;
					wait = 0.3f;
					return;
				case 6:
					Check("standing in the bed plants the seed", Director.Carried == null);
					Hero.GlobalPosition = Director.Keepsake.GlobalPosition + new Vector2(0f, 120f);
					step++;
					wait = 3.2f;
					return;
				case 7:
					Shot();
					step++;
					wait = 3.4f;
					return;
				case 8:
				{
					Shot();
					var saveManager = Director.GetNodeOrNull<SaveManager>("/root/SaveManager");
					Check("the First Tree is marked found on the save", saveManager?.Data.HasFoundSecret(SecretCatalog.FirstTreeId) == true);
					Check($"it paid one found level of Vitality (found {saveManager?.Data.FoundUpgradeLevel("vitality")})", saveManager?.Data.FoundUpgradeLevel("vitality") >= 1);
					Check($"and +5 max HP at once ({maxBefore} -> {Hero.MaxHP}), fully healed ({Hero.CurrentHP})", Hero.MaxHP == maxBefore + 5 && Hero.CurrentHP == Hero.MaxHP);
					GD.Print($"SECRETPROBE {(failures == 0 ? "ALL PASS" : $"{failures} FAILED")}");
					GetTree().Quit(failures);
					step++;
					return;
				}
			}
		}

		// A point on an ellipse 110 px outside the pen's fence.
		private Vector2 Lap(float angle)
		{
			Vector2 half = pen.Bounds.Size * 0.5f + new Vector2(110f, 110f);
			return pen.Bounds.GetCenter() + new Vector2(Mathf.Cos(angle) * half.X, Mathf.Sin(angle) * half.Y);
		}

		private static T FindChildOfType<T>(Node parent) where T : Node
		{
			foreach (Node child in parent.GetChildren())
				if (child is T typed && !child.IsQueuedForDeletion())
					return typed;
			return null;
		}
	}
}
