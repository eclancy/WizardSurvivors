# Decision: sound effects are generated procedurally, in-repo

**Date:** 2026-09-05
**Decision:** Author the entire SFX set as Python 2.7 synthesis code under `tools/audio/`,
committing both the generators and the rendered `.wav` files. No recorded audio, no external
asset packs, no audio model in the pipeline.
**Status:** Adopted. Set generated (69 files); wiring not started.

## Context

The game shipped with music and no sound effects at all — two `.mp3` tracks and two unused
`.wav` files, with no playback call for either `.wav` anywhere in `scripts/`. Adding effects
meant choosing where they come from.

The machine constrains the answer hard. There is only Python 2.7 with PIL: no numpy, no
ffmpeg, no ImageMagick, no OGG encoder, no virtualenv, and nothing that can turn a
description into a recording. The art pipeline in `tools/art/` already lives inside exactly
that constraint and works well there.

## Alternatives

- **A licensed asset pack.** Fastest to a first sound, but every future element, enemy or
  spell needs a matching pack entry, mixing is manual per file, and the set drifts from the
  visual contract with nothing enforcing the relationship.
- **Recording and editing foley.** No microphone, no editor, and no way to iterate from a
  terminal session.
- **A hosted audio-generation service.** Would send project material to an external service,
  requires credentials this repo does not have, and yields opaque files nobody can re-derive.

## Consequences

**Good.** A sound is a diff. Rebalancing the whole mix is editing peak targets in one
registry and re-running, because `build.py` normalises every file to its target rather than
to full scale. The element palette is a data table that mirrors the colour table in
`.ai/art-direction.md`, so a new element is a row in both. Every draw is seeded, so builds
are byte-identical and a changed file always means changed code. And because the set is
code, it can be *measured*: `tools/audio/analyse.py` audits pairwise distinctness the way
the art audit measures silhouette overlap, and caught five collisions a listener would have
found only after they shipped.

**Bad, and permanent.** This produces synthetic, retro-adjacent effects and nothing else —
no voice, no foley, no recorded impact, no music. If the project later wants any of those,
it is a different pipeline, not a tuning pass on this one. Iteration is also blind: whoever
runs the generator cannot hear the output, so `--preview` audition reels and the confusion
audit substitute for ears, and the first real listening pass will move some numbers.

**Neutral.** Pure-Python synthesis is slow — a full build is about 90 seconds and the audit
several minutes. Both are rare operations and neither is on any developer's inner loop.

## See also

`.ai/audio-direction.md` (the contract), `.ai/audio-manifest.md` (the inventory and wiring
backlog), `tools/audio/` (the generators).
