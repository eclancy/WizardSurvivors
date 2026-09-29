# Controller support and the selection highlight

How an Xbox pad plays this game, what the input map actually binds, and why menu highlighting is
one idea rather than two.

## 1. The bindings

All of it lives in `[input]` in `project.godot`. There are three action families and they do not
overlap by accident.

| Action | Keyboard / mouse | Pad | Read by |
|---|---|---|---|
| `move_up/down/left/right` | WASD, arrow keys | **left stick** (deadzone 0.2) | `Player.MovePlayer`, and the three headless harnesses |
| `ui_up/down/left/right` | arrows, WASD | left stick (deadzone **0.5**), D-pad | Godot's own focus navigation; `MenuNavigator` adds repeat |
| `ui_accept` | Enter, Kp Enter, Space | **A** | `BaseButton`, automatically, when it has focus |
| `ui_cancel` | Escape | **B**, **Start** | back out of a menu |
| `pause` | Escape | **Start** | `Node2DGame._UnhandledInput` |

**The two deadzones are the point of having two movement action sets.** A menu must be deaf to a
thumb resting on the stick, so `ui_*` ignores the first half of its travel. A walking player must
not be: at 0.5 a gentle push did nothing at all and everything past the midpoint was nearly full
speed. Movement therefore reads `move_*`, which is the same keys at 0.2. `Player.MovePlayer` read
`ui_*` until this landed, which is why stick movement felt like an on/off switch.

`RegressionChecks.ValidateInputMap` guards the shape of all of it: every action exists, A is on
`ui_accept`, Start is on `pause` and `ui_cancel`, the left stick is on both `move_*` and `ui_*`,
and `move_*`'s deadzone is still below `ui_*`'s. The input map is a wall of serialised event
objects in a text file and it is the one place in this project where a merge can delete a whole
feature without breaking the build.

**`pause` is deliberately not `ui_cancel`.** `ui_cancel` carries B, and B is "back" everywhere
else, so reading `ui_cancel` to open the pause screen would pause the run every time a player
tapped back out of habit. Inside the pause screen, either closes it — there, back *is* the gesture.

## 2. Why nothing worked before

Almost all of those bindings already existed. Two things were missing, and neither was a mapping:

1. **Nothing was focused.** Every menu in this game builds its buttons in code and not one of them
   called `GrabFocus`, so a pad user arrived on a screen where the stick moved nothing and A did
   nothing. That alone is why the game read as having no controller support.
2. **A held stick does not repeat.** Godot emits a joypad motion event only when an axis *changes*,
   so holding the stick produces one event and then silence. One flick per option is tolerable for
   three level-up cards and unusable for a spellbook.

## 3. `MenuNavigator`

One `Node`, attached to a screen's root, `ProcessMode.Always` so it survives the paused tree that
the level-up, chest and pause screens all appear on.

It grants focus and adds repeat, and **leaves the actual step to `Control.FindValidFocusNeighbor`**
— the same call Godot's own navigation makes. A repeat that walked its own list would disagree with
a single flick on any screen whose options are a grid rather than a column, and the two would then
differ in a way nobody would think to test. Repeat reads the hardware (`Input.GetJoyAxis`,
`Input.IsJoyButtonPressed`) rather than the `ui_*` actions, because reading the actions would pick
up the arrow keys too and stack this repeat on top of the OS one.

Three behaviours worth knowing before changing it:

- **Every screen opens with something selected, and focus is never given up.** Both halves of that
  sentence were once the other way round, and both were bugs — see section 6, which is the more
  useful read than this bullet.
- **Initial focus must not scroll the list.** `FollowFocus` is what a player wants when they
  *navigate* to an option below the fold and never what they want on arrival. The character select
  proved it: the Test Wizard card is taller than the rest, so focusing it as the screen opened
  scrolled the list to show its bottom edge and the screen came up past its own first heading.
  `GrabWithoutScrolling` turns the flag off around the grab — `ScrollContainer` follows focus
  synchronously from `gui_focus_changed`, so there is nothing to defer and nothing to race.
- **Screens stack, and in the steady state nobody steals.** The set-detail screen opens over the
  level-up menu, which is over the run, and each has its own navigator. A menu takes the selection
  only at the moment it *opens*, through the `Attach` call on its own show path; after that the
  screens underneath can see the selection lives elsewhere and leave it alone. Close the top screen
  and its buttons are freed, the selection becomes invalid, and the screen underneath picks it up
  on the next frame.

`Attach(root, initialFocus)` takes an optional primary action, because tree order is reading order
on most of these screens but not all: the main menu's Options button is a direct child of the root
while Start Run is buried in a `MarginContainer`, so tree order alone opened the game with Options
selected.

## 4. `MenuFocusHighlight`

A ring drawn over whatever the player is pointing at, with a stick or with a mouse. **Hover and
focus both raise it and neither owns it** — that is the whole idea. The player never has to learn
that those are two different things.

**It is a node and not a stylebox** because Godot draws exactly one stylebox per button state, so a
highlight expressed as a `focus` stylebox has to replace the frame rather than sit on top of it —
and the frames already carry meaning. The level-up screen is the case that proves it: an upgrade to
a spell already held *rests* on the lit frame, so `AddThemeStyleboxOverride("focus", cardStyleLit)`
gave those cards a focus state identical to their resting state and a controller user could not see
which of three was selected.

Two things were settled by photographing it with `_UiShot` rather than by argument:

- **The ring is a lit gold, not the inlay gold.** At the inlay's own `0.85/0.69/0.29` it vanished on
  a card that already wears a gold frame. Same hue, light turned up.
- **The wash under it is almost nothing (0.05).** It started at 0.12, and a card overlay already
  washes itself gold at 0.16 when pointed at, so the two stacked and turned the selected card
  olive — the selection stopped reading as light on the card and started reading as a different
  card. The 0.05 only exists for plain buttons, which have no wash of their own.

**Where it is allowed to live.** Children draw after their parent, so the ring is parented to what
it rings — but a card is built as an invisible overlay `Button` first and its visible content after,
so a ring inside the overlay would be buried. `Attach` therefore separates the node that reports
focus from the node the ring is drawn in, and defaults the second to the card. It then refuses any
host that is a `Container` other than `PanelContainer` or `MarginContainer`: those two fit *every*
child to their own rect, so a third child lands exactly on top of the other two, while a
`VBoxContainer` would stack the ring as a row and a `CenterContainer` would centre it at its
minimum size, which is nothing.

## 5. The bug this all started as, and what it taught

The first version shipped two rules that sounded careful and were wrong. They are worth writing
down because both are the kind of thing that looks like polish while it removes a feature.

**"Only focus something if a pad is connected."** The intent was not to show a keyboard-and-mouse
player a cursor they had not asked for. The problem is that `Input.GetConnectedJoypads()` answers
**0** for the first stretch of a process — Godot enumerates pads asynchronously — and the check ran
once, on one deferred frame. Measured with a probe on this machine it answered 0 far more often
than not, so most screens opened with nothing selected at all.

A `joy_connection_changed` subscription does **not** fix it: that signal fires on a *change*, so a
pad already enumerated before the subscription never fires it. Polling it on a timer does fix it,
and was still the wrong answer, because the premise was wrong. A freshly opened menu with its
primary action selected is what every game and every OS dialog does, it costs a mouse user nothing
now that the ring hides itself under a pointer, and it means a pad or a keyboard can act on a
screen the instant it appears rather than having to wake it up first. So the condition is gone.

**"Mouse motion releases focus."** The intent was that hover and focus draw the same ring, so a pad
user reaching for the mouse should not see two. But releasing focus meant the next press of A
arrived with nothing selected — and that press was then *spent* establishing a selection instead of
acting on one, because of a third rule that swallowed it. A single stray pixel of mouse jitter
between lining up a choice and pressing A was enough to lose the press.

This is what the player reported, in the exact words "I can move through the options on controller
but the buttons don't seem to select anything": the stick woke the screen up, navigation then
worked, and every A press that followed a twitch of the mouse went nowhere.

The double highlight is now solved where it belongs, in the ring: `MenuFocusHighlight.PointerActive`
stops it drawing for *focus* while the pointer is the device in use. Hiding a ring is free. Giving
up focus is not. **Nothing swallows `ui_accept` any more.**

**The lesson worth keeping:** a rule that makes the UI quieter must not be allowed to make it
unable to act. Both of these traded away the pad's ability to press things in exchange for
tidiness, and neither trade was visible in a build, a validator or a screenshot — only in pressing
the button.

## 6. What is not done

- **No pad has been held while playing this.** `scripts/_PadProbe.cs` closes most of the gap: it
  loads a screen, waits for it to settle, and injects a real `InputEventJoypadButton` for A — so
  the input map, `MenuNavigator._Input`, the viewport's GUI routing and `BaseButton` are all on the
  path being tested, which emitting the signal would not be. It presses up to three times and says
  which attempt fired, because "costs one press to wake up" and "permanently broken" are different
  bugs that a single-press probe cannot tell apart.

  ```
  "$GODOT_BIN" --path . scenes/_PadProbe.tscn -- --scene=res://scenes/StageSelection.tscn
  ```

  Main menu, character select and stage select all fire on attempt 1. **The paused screens —
  level-up, chest, pause — are not covered by it**: the probe has no bot, so a run never reaches a
  level-up before it ends. They are covered by reasoning instead, which is weaker: `LevelUpMenu` and
  the chest menu are `CanvasLayer`s with `process_mode` set to WhenPaused, so `can_process()` is
  true for their buttons while the tree is paused, and the navigator itself is Always. If a paused
  screen ever stops responding to A, that is the first thing to check — an added control that
  inherits Pausable will navigate perfectly and refuse to activate.

  The repeat cadence and the feel of stick-scrolling a long spellbook still want a controller in
  someone's hands.
- **No button prompts.** Nothing on screen says "A" or "Start"; the game does not know which device
  is in use and does not show it.
- **No remapping**, and no pad support on the title screen beyond "any button opens the menu",
  which it already had.
- **The right stick, triggers and shoulders are unbound.** `shoot` is still mouse-only, which
  matters not at all while every spell auto-fires.
