# Boss levels

A run can now end in victory, not only in death. At `Node2DGame.TimerVictorySeconds` (30:00 since 2026-10-11; it was 15:00) the
survival clock stops and the stage's boss arrives; killing it clears the level, pays a currency
bonus, unlocks the next stage, and shows the victory screen. Related GitHub issues: #8 (Base Boss
Class & State Machine) and #12 (Boss Rush Gauntlet Mode, built - see the last section).

## Decisions

**First boss (Enchanted Forest) — Elderbark, the Treant: heavy tank, slow melee.** Enormous HP,
slow but relentless, heavy contact damage and a telegraphed ground slam. It rewards sustained DPS
and survivability, and punishes glass-cannon burst builds that cannot hold the line. Two
alternatives were considered and set aside: a Broodmother that continuously summons adds, and a
Mirage Stag whose decoys break auto-targeting. The stag was rejected *for the first boss only*,
because decoys mean touching the targeting code every spell shares — it remains a good candidate
for a later stage.

**Clearing a boss gates the next stage.** Cursed Castle is locked until the Forest boss falls.
`StageSelection` reads `SaveData.UnlockedStageIds`, which nothing wrote to before this. The
accepted cost is that half the current stage roster is closed on a fresh save.

**Normal spawning stops when the boss appears.** The timer freezes, regular waves (elites and the
Soldier miniboss included) stop, and chests stop dropping; enemies already alive are left to be
cleared. The arena drains so the fight reads as a duel rather than the usual swarm with a large
enemy inside it.

**The way home (2026-10-28).** "when you defeat the boss, we should open an arcane portal so the
player can end the run when they want to leave (also add an arrow pointing to it if it's off
screen)". The run is won the moment the boss falls, but it no longer ends on a 1.2 s timer: the boss
drops a chest where it fell and an arcane portal (`ArcanePortal`) opens a little past the body, on
ground the player can reach, and the run ends - in victory, through `FinishRunAndReward` as before -
when the player's feet step into the ring at its foot (`Node2DGame.TickVictoryPortalAndPointers`,
`EnterVictoryPortal`). Until then `bossFightActive` stays up, so the clock, the trickle, the
Warden, the side event and the secrets all stay stood down: lingering collects the chest and the
last XP and farms nothing (no Arcane Energy per minute, no new enemies). Dying on the way to the
portal still ends the run as the win it already is. In a Boss Rush only the finale opens one. The
balance bot steps in at once, so a winning bot run still ends.

**Off-screen badges.** `WorldPointer` - ChestPointer's badge and tail, in its own rim colour - points
at a living boss from the screen's edge (blood red, the boss's own current frame in the badge) and
at the open portal (arcane violet, a drawn ring). The boss counts as seen while its point is within
90 units of the screen (its body is far bigger than the point it stands on), and the badges keep
140 units down from the top edge, under the boss's bar and the banner line.

**Tools.** `_UiShot --stage=0 --rush-steps=2` brings a chapter's boss and fells it (the steps work
outside a rush now), and `--walk-into-portal` stands the wizard in the portal once it is open.

## How it is wired

- `BossCatalog` (`scripts/BossCatalog.cs`) maps a stage index to its boss: id, display name,
  banner tagline, scene, health, currency bonus, and the stage the win unlocks. **A stage with no
  entry still ends on the timer exactly as before**, so bosses can be added one stage at a time.
- `BossEnemy` (`scripts/BossEnemy.cs`) subclasses `Enemy`. It adds the identity the run result
  records, the telegraphed slam, a single enrage threshold at a third health, and the
  `BossDefeated` signal the run listens for. Chasing, damage, status and death stay in `Enemy`.
- `Enemy` gained three exports that default to the old behaviour: `ContactDamage` (read by the
  player's overlap loop, 1 for everything else), `KnockbackResistance` and `MinSlowMultiplier`, so
  a boss cannot be shoved around the arena or parked by a freeze build for the whole fight.
- `BossId` flows into `RunResult`, where `AchievementDefinitions` already matched on it —
  `forest_cleared` fires on any victory whose boss id contains "forest". **A new boss id must
  contain its stage's keyword** ("forest", "castle", "ruins") or the win silently grants no spell;
  `RegressionChecks.ValidateBossCatalog` fails the build's startup validation if it does not.

## Known tuning, unverified

Elderbark's 4200 HP is a first guess calibrated against a 15-minute elite (~860 HP). The pace clock
(`.ai/gameplay-design.md`, "The Apotheosis") makes a 30:00 elite as tough as that 15:00 one was, but
the player arrives with roughly twice the kills behind them, so the boss is likely softer than it
was. It has not
been playtested against a real endgame loadout — expect to move it. `ContactDamage = 3` against a
0.2s touch cooldown is 15 damage per second, which is meant to make standing in the boss a hard
mistake; that number is the other one most likely to need a pass.

Press **F4** in a run to skip straight to the boss instead of playing thirty minutes to reach it.

## Boss Rush (issue #12, built 2026-10-28)

"The ultimate endgame mode with sequential boss encounters." The issue said nothing more, so these
are the decisions taken for the first version, each one a knob in `scripts/BossRushCatalog.cs` or
`scripts/Node2DGame.BossRush.cs`:

- **What it is: an ordinary run with every campaign boss spread through it.** Not a string of
  arenas and not a menu between fights: the swarm, the XP, the chests and the level-ups all run as
  usual, and the bosses come for the player one after another in campaign order. The roster is
  derived - every campaign chapter's boss that has a scene of its own - so a chapter whose boss is
  still a stand-in is left out and joins by itself when its scene exists. Today that is **ten**,
  Elderbark to the Warden of the Deep (the Sketchbook's Wizard Dude is outside the campaign).
- **Where: the final chapter, the Emberdeep.** The rush is the long road back walked in one go, which
  is what the Warden of the Deep's own fight already says, so it ends on the one boss built to end
  it. The bosses read nothing about the chapter they were made for (checked: every one only places
  its attacks and adds under its own parent), so any arena works.
- **When: on the pace clock.** The first boss at pace 2:30 (`FirstArrivalPaceSeconds`), the rest
  evenly to the ordinary boss time, where the finale arrives through the ordinary boss phase - every
  83 pace seconds with ten. The clock stops for every fight, as it does for a chapter's boss, so the
  gap between two arrivals is always the full step of swarm, and spawning, elites, the Warden,
  chests, the side event and the secrets stand down during each fight exactly as they always have.
- **How tough: scaled to where in the run each arrives.** Health times the swarm's own health curve
  at the arrival over its value at 15:00 (where every boss's health was tuned), floored at 0.2
  (`MinHealthScale`): the curve starts at 6/156, which made a minute-two boss a few seconds' work.
  The finale is at full health. Damage is not scaled - no enemy's is.
- **What a fall pays:** a chest where the boss fell, six seconds' breather from the trickle to walk
  to it, and half the boss's chapter `ArcaneVictoryBonus` (`ArcaneBonusShare`), paid win or lose for
  every boss felled. The run is won only when the last falls.
- **Who can play it: anyone whose save has beaten the Warden of the Deep in his own chapter**
  (`BossRushCatalog.IsOpen`). It is the last card on the stage list, hidden until then, like a closed
  chapter. That gate is also why the rush records nothing about chapters: its `RunResult.BossId` is
  empty, so it opens no chapter, grants no chapter deed and pays no single boss's bonus. Its own
  records are `SaveData.BossRushBestFelled` and `BossRushBestClearSeconds` (no schema bump: they
  only add, and every read is guarded), shown on its card.

The results screen says "Boss Rush · the Emberdeep", and its time tile is the whole rush, fights in,
with a bar of bosses felled. The banner and the boss bar say which of how many each one is.

**Measured** (`_BalanceHarness -- --boss-rush --all-unlocked --policy=ordinary`, `--fixed-fps 30`,
Pyromancer, seed 11). The immortal bot felled all ten; the fights took 40, 77, 48, 55, 49, 62, 91,
136, 59 and 98 seconds (the Still Warden the longest), 12 minutes of fighting in a rush of 27
minutes, ending at level 34. The mortal survivor bot died at 3.6-3.9 minutes having felled
Elderbark - but in an ordinary Emberdeep run the same bot dies at 2.3-2.4, so it is the chapter
that kills it, not the rush (each fight drains the field). Nobody has played one yet: whether ten
fights in a row read as a climax or a slog, and whether 27 minutes is too long, needs eyes. The
cheapest knobs are `MinHealthScale` and a flat multiplier on `BossRushHealthScale`.

**Tools.** `_BalanceHarness -- --boss-rush` runs one (give it `--minutes=30`; the bot's clock is real
time, fights included) and logs every arrival and fall as a `BOSS RUSH` line. `_UiShot` takes
`--boss-rush`, `--rush-steps=N` (one step brings the next boss or fells the current one) and
`--boss-rush-open[=felled]` for the stage list. F4 in a rush brings the next boss, not the finale.

**Found on the way: no banner had been readable for a long time.** `ShowRunBanner` (the chapter's
opening line and every boss's name and tagline) built its tween parallel, so the fade started on the
first frame and the panel was gone in about half a second. Fixed for all of them; the banner now
also sits below the boss's bar rather than behind it, clears the phone's spell row, and
an onboarding tip waits while one is up.
