# -*- coding: utf-8 -*-
"""Generate every sound effect into assets/sfx/.

Run from the repo root:

    python tools/audio/build.py                 # write the whole set
    python tools/audio/build.py cast_ impact_   # write only names containing these
    python tools/audio/build.py --preview       # also write per-category audition reels
    python tools/audio/build.py --manifest      # print the markdown table for the docs

The generators are the source, the .wav files are output. Never hand-edit a file in
assets/sfx/ - edit the builder in sfx.py and re-run. That is the same contract the art
pipeline is under, and for the same reason: an edited output silently stops matching the
code that claims to produce it.

Every sound is normalised to the peak target in the registry rather than to full scale.
Those targets ARE the mix - the game plays these at unity gain and the relative balance is
already baked in here, so a designer changes balance by editing one number in sfx.py and
re-running, not by hunting VolumeDb overrides across forty call sites.
"""
from __future__ import division

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

import sfx
import synth as S

OUT = os.path.join(ROOT, "assets", "sfx")

# A one-shot longer than this is almost always an envelope mistake rather than a choice.
LONG_WARNING = 2.6
# How far under its registry target a finished file may land before it is a bug. Anything
# more than this means the fade ate the peak or the builder returned near-silence.
UNDER_TARGET_WARNING = 1.0


def db_to_linear(d):
    return 10.0 ** (d / 20.0)


def build_one(entry):
    name, category, target_db, fn, loops = entry
    buf = fn()
    buf = S.dc_block(buf)
    if not loops:
        # Fade BEFORE normalising, and keep the fade-in under half a millisecond. Both
        # matter: a fade applied afterwards attenuates the very transient the target was
        # measured against, and a 2 ms fade-in is long enough to audibly blunt the shared
        # attack in palette.transient. A loop is not faded at all - its ends are the seam.
        buf = S.fade(buf, 0.0004, 0.006)
    buf = S.normalize(buf, db_to_linear(target_db))
    path = os.path.join(OUT, name + ".wav")
    pk, rm, secs, clipped = S.write_wav(path, buf)
    return dict(name=name, category=category, path=path, peak=pk, rms=rm, target=target_db,
                secs=secs, clipped=clipped, loops=loops, buf=buf)


def seam_error(buf, window=64):
    """How badly a loop clicks at the wrap point, as a level difference in dB."""
    if len(buf) < window * 2:
        return 0.0
    head = S.rms(buf[:window])
    tail = S.rms(buf[-window:])
    return abs(S.db(head) - S.db(tail))


def report(results):
    print("")
    print("%-22s %-8s %8s %8s %7s  %s" % ("name", "category", "peak", "rms", "len", "notes"))
    print("-" * 78)
    warnings = 0
    for r in results:
        notes = []
        if r["clipped"]:
            notes.append("CLIPPED x%d" % r["clipped"])
        if r["secs"] > LONG_WARNING and r["category"] != "meta":
            notes.append("long")
        if r["peak"] < r["target"] - UNDER_TARGET_WARNING:
            notes.append("UNDER TARGET by %.1f dB" % (r["target"] - r["peak"]))
        if r["loops"]:
            e = seam_error(r["buf"])
            notes.append("loop seam %.1f dB" % e)
            if e > 6.0:
                notes.append("SEAM AUDIBLE")
        if [n for n in notes if n.isupper() or "AUDIBLE" in n]:
            warnings += 1
        print("%-22s %-8s %7.1fd %7.1fd %6.2fs  %s"
              % (r["name"], r["category"], r["peak"], r["rms"], r["secs"], ", ".join(notes)))
    total = sum([os.path.getsize(r["path"]) for r in results])
    print("-" * 78)
    print("%d files, %.1f s of audio, %.2f MB in %s"
          % (len(results), sum([r["secs"] for r in results]), total / 1048576.0,
             os.path.relpath(OUT, ROOT)))
    if warnings:
        print("%d file(s) need attention - see notes above" % warnings)
    return warnings


def manifest(results):
    """The markdown table pasted into .ai/audio-manifest.md. Regenerate it, do not retype."""
    print("")
    print("| File | Category | Length | Peak | Loops |")
    print("|---|---|---|---|---|")
    for r in results:
        print("| `%s.wav` | %s | %.2f s | %.1f dBFS | %s |"
              % (r["name"], r["category"], r["secs"], r["peak"], "yes" if r["loops"] else "no"))


def previews(results):
    """One audition reel per category, into tools/audio/, underscore-prefixed like the art
    previews. These are working files - they are not shipped and nothing loads them."""
    cats = {}
    for r in results:
        cats.setdefault(r["category"], []).append(r)
    gap = S.silence(0.25)
    for cat, items in sorted(cats.items()):
        reel = []
        for r in items:
            reel = S.concat(reel, r["buf"], gap)
        path = os.path.join(HERE, "_preview_%s.wav" % cat)
        S.write_wav(path, reel)
        print("preview: %s (%d sounds, %.1f s)"
              % (os.path.relpath(path, ROOT), len(items), len(reel) / S.SR))


def main(argv):
    want_preview = "--preview" in argv
    want_manifest = "--manifest" in argv
    filters = [a for a in argv if not a.startswith("--")]

    entries = sfx.REGISTRY
    if filters:
        entries = [e for e in entries if any([f in e[0] for f in filters])]
    if not entries:
        print("nothing matched %r" % filters)
        return 1

    results = []
    for e in entries:
        results.append(build_one(e))
        sys.stdout.write(".")
        sys.stdout.flush()

    warnings = report(results)
    if want_manifest:
        manifest(results)
    if want_preview:
        previews(results)
    return 1 if warnings else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
