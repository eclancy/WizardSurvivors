# ADR: The Apotheosis - a wizard's ultimate is the final form of its starting spell

**Date:** 2026-10-10 · **Issues:** #59 (design), #60 (systems), #61 (forms) · **Status:** accepted, built

## Decision

Every wizard has one **Apotheosis**: the final form of the spell they start with, offered in the
Ascension strip above the level-up cards (milestone 9) when four things hold on that spell at once:

1. it is at max level (8);
2. the named level 4 and level 8 evolutions were taken;
3. the wizard's element is at tier 6;
4. the wizard is the one the entry names. The gate is a (character, spell) pair.

Taking it spends the level-up pick. It is terminal: it never levels, it is never re-offered, and
`SpellData.ApplyEvolution` refuses a second one.

`scripts/ApotheosisCatalog.cs` is the single authority on gates and forms.
`RegressionChecks.ValidateApotheosisCatalog` holds the invariants `dotnet build` cannot see.

## Rejected: a hand-cast ultimate on its own button

This game has no reaction inputs. You steer, and everything else fires itself. One button that
matters more than every other input would change what kind of game it is, and it would need its
own meter, its own accessibility toggle and its own auto-cast fallback. A transformation keeps the
auto-shooter contract and puts the decision on a level-up card, where every other build decision
already lives.

## One wizard, one spell, one verb

Each form claims a verb no other form owns, so the six do not collapse into "the big one that does
more damage". The validator enforces it.

| wizard | spell | form | verb |
|---|---|---|---|
| Pyromancer | Cinderbreath | Cataclysm | burn |
| Frostweaver | Cone of Cold | The Long Winter | stop |
| Stormcaller | Chain Lightning | The Lattice | link |
| Geomancer | Obsidian Spike | Sundering | hold |
| Tituba | Riptide | Spring Tide | flood |
| Väinämöinen | Bramble Seed | Wildwood | swarm |

Unclaimed, for wizards still to come: pull, banish, contagion, rewind, displace, reflect.

## Where the build departed from the issues, and why

- **The Pyromancer's form is Cinderbreath's, not Fireball's.** The covenant is the wizard's
  *starting* spell, and the starter rework moved the Pyromancer onto Cinderbreath after #61 was
  written. Cataclysm keeps its verb and its crater chain.
- **Every required path is the one that deepens the wizard's own element.** #61 named some
  paths that did not: Chain Lightning's `forked_surge` adds no Lightning, and its level 8
  `overcharge` no longer exists. #61's own rule is that a required path must never branch away
  from the wizard's element, so the paths follow the rule rather than the stale ids.
- **Sundering grants armour, not flat reduction.** Flat reduction left the game, and armour is one
  percentage stat (`Player.GetArmorPercent`). The ring adds 20% inside it, under the same cap.
- **Two forms were added for wizards #61 did not know about**: Spring Tide (flood) and Wildwood
  (swarm), both verbs from #59's unclaimed list.
- **Level 8 is "Ascension", the final form is "Apotheosis".** Level 8 used to say "ULTIMATE
  ASCENSION", which made two "ultimate" tiers one level apart.

## Traps, and what holds each one

- **The element rug-pull.** A form only adds +1 to the wizard's own element (validated), so
  accepting it can never drop the player out of the tier that qualified them.
- **The two-element ceiling.** The validator counts base, path and form together.
- **A dead offer.** It sits in the strip above the cards on every level-up once earned, and the
  strip also guarantees the level 8 Ascension the path needs can never be skipped by levelling fast. Whether tier 6 is
  reachable at all was measured, not assumed (below).
- **Chain reactions in a swarm.** Cataclysm's craters and The Long Winter's shatters are queued
  and resolved a few per frame, never inside the kill that caused them. Craters are pooled. A shard
  can freeze an enemy at most once per 2.5 s, so a wave cannot become a permanent freeze lock. The
  Lattice caps edges per node, not total nodes.

## Measured

See `.ai/gameplay-design.md`, "The Apotheosis", for the clock times the harness recorded
(`_BalanceHarness --seek-apotheosis --all-unlocked --immortal`).
