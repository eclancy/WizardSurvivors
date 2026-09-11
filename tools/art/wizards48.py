# -*- coding: utf-8 -*-
"""Four player-wizard silhouettes at 48x48, under the corrected proportions.

Run: python tools/art/wizards48.py     -> tools/art/_wizards48/ (gitignored)

Replaces the four 32x32 studies in wizards.py, which were built on "the signature shape must be
wider than the body". That rule is right for a swarmer that has to be picked out of a crowd of
thirty, and wrong for the player - `.ai/world-and-tone.md` writes up why. The standing note from
the title-screen review names the failure exactly: roughly 2:1 tall-to-wide, a brim wider than
the body, bilateral symmetry, "a chess piece or a mascot". Conclave's brim was 24px against a
19px hem. It repeated the mistake it was supposed to avoid.

WHAT CHANGED, AND IT IS ALL PROPORTION AND VALUE RATHER THAN SUBJECT:

1. ABOUT 2.6:1 TALL-TO-WIDE. A ~40px figure in a 48px cell is ~15px across, not 24. Every one of
   these is slimmer than its 32px ancestor, and the hat brim now sits INSIDE the shoulder width.
   A hat can still be the signature by being TALL - height reads as authority where width reads
   as cartoon.
2. THE FIGURE SITS IN SHADE AND DEEP. The old studies filled the robe with the bright end of the
   wool ramp, which is the other half of what made them toys. Base here is `wool.shade`, the lit
   tone appears only on the key-facing edge, and separation is the rim plus the staff orb doing
   their job - which is what the light in this game is FOR.
3. OFF-AXIS SWEEP ON EVERY ONE. A mirrored silhouette reads as a piece, not a person. Each has a
   cloak, crown or stance broken off the vertical.
4. THE STAFF SETS THE TOP OF THE SILHOUETTE. Taller than the wizard, with a real head on it, so
   the tallest thing on screen is the thing making the light.

What survives from the 32px studies, because those parts were right: the face is a dark void with
two points of arcane light in it, the beard is the second silhouette and needs internal structure
to avoid being a bib, and the hem is never a flat bar.

All four are drawn from scratch. Format density - a small figure carrying one oversized signature,
read against a dark ground - is the reference; no sprite, character or asset from another game is
reproduced here.
"""
import os

import bonelight as B
import pixel as P

CELL = 48
FEET = CELL - 2              # contract section 5: feet row 46, contact row 47
CX = 23.5                    # the cell's true centre on an even width

PALETTE = B.build_palette({
    "o": B.OCC,
    "W": B.RIM,
    "d": ("wool", "lit"),   "c": ("wool", "base"),  "b": ("wool", "shade"),
    "a": ("wool", "deep"),
    "S": ("skin", "hi"),    "s": ("skin", "lit"),   "k": ("skin", "base"),
    "n": ("skin", "shade"),
    "H": ("gold", "hi"),    "G": ("gold", "lit"),   "g": ("gold", "base"),
    "u": ("gold", "shade"),
    "Z": B.ELEMENTS["light"][0], "Y": B.ELEMENTS["light"][1],
    "y": B.ELEMENTS["light"][2], "v": B.ELEMENTS["light"][3],
})


class Grid(object):
    def __init__(self, w=CELL, h=CELL):
        self.w, self.h = w, h
        self.g = [["."] * w for _ in range(h)]

    def set(self, x, y, ch):
        if 0 <= x < self.w and 0 <= y < self.h and ch != ".":
            self.g[y][x] = ch

    def span(self, y, x0, x1, ch):
        for x in range(int(round(x0)), int(round(x1)) + 1):
            self.set(x, y, ch)

    def line(self, x0, y0, x1, y1, ch):
        steps = max(abs(x1 - x0), abs(y1 - y0))
        if steps == 0:
            self.set(int(x0), int(y0), ch)
            return
        for i in range(int(steps) + 1):
            t = i / float(steps)
            self.set(int(round(x0 + (x1 - x0) * t)), int(round(y0 + (y1 - y0) * t)), ch)

    def disc(self, cx, cy, r, ch):
        for y in range(int(cy - r) - 1, int(cy + r) + 2):
            for x in range(int(cx - r) - 1, int(cx + r) + 2):
                if (x - cx) ** 2 + (y - cy) ** 2 <= r * r + 0.3:
                    self.set(x, y, ch)

    def rows(self):
        return ["".join(r) for r in self.g]


def _taper(g, y0, y1, hw0, hw1, ch, lean=0.0, cx=CX):
    """The body. hw is HALF-width, so 2.6:1 on a 40px figure means hw1 about 7.7."""
    for y in range(y0, y1 + 1):
        t = (y - y0) / float(max(1, y1 - y0))
        hw = hw0 + (hw1 - hw0) * t
        c = cx + lean * t * t
        g.span(y, c - hw, c + hw, ch)


def _shade(g, y0, y1, lit, mid, dark, rim=True):
    """Key from the upper left, one tone per band, walking each row's own extent."""
    for y in range(y0, y1 + 1):
        xs = [x for x in range(g.w) if g.g[y][x] == mid]
        if not xs:
            continue
        lo, hi = min(xs), max(xs)
        width = hi - lo
        g.set(lo, y, lit)
        if width >= 4:
            g.set(lo + 1, y, lit)
        if width >= 2:
            g.set(hi, y, dark)
        if width >= 6:
            g.set(hi - 1, y, dark)
        if rim:
            g.set(lo - 1, y, "W")


def _void(g, y0, y1, hw0, hw1, cx=CX):
    """The face. Tapered - a rectangle of black reads as a letterbox."""
    for y in range(y0, y1 + 1):
        t = (y - y0) / float(max(1, y1 - y0))
        hw = hw0 + (hw1 - hw0) * t
        g.span(y, cx - hw, cx + hw, "o")


def _beard(g, cx, y0, y1, hw0, hw1, sweep=0.0):
    """Moustache over a split mass with tapering strands. Flat fill reads as a bib."""
    for y in range(y0, y1 + 1):
        t = (y - y0) / float(max(1, y1 - y0))
        hw = hw0 + (hw1 - hw0) * t
        c = cx + sweep * t
        lo, hi = int(round(c - hw)), int(round(c + hw))
        g.span(y, lo, hi, "k")
        g.set(lo, y, "s")
        if hi - lo >= 3:
            g.set(lo + 1, y, "s")
            g.set(hi, y, "n")
        if hi - lo >= 6:
            g.set(hi - 1, y, "n")
        if t < 0.76 and hi - lo >= 3:
            g.set(int(round(c)), y, "n")          # the split
    w = hw0 + 1.6
    g.span(y0 - 1, cx - w, cx + w, "k")           # the moustache, overhanging
    g.set(int(round(cx - w)), y0 - 1, "S")
    g.set(int(round(cx + w)), y0 - 1, "n")
    g.set(int(round(cx)), y0 - 1, "o")
    g.set(int(round(cx)), y0, "o")


def _hem(g, y, x0, x1, gold=True):
    """Stepped, never a flat bar - a straight line reads as a shelf he is standing on."""
    g.span(y, x0, x1, "a")
    g.span(y - 1, x0, x0 + 3, "b")
    g.span(y - 1, x1 - 3, x1, "b")
    if gold:
        g.span(y, x0 + 4, x1 - 4, "g")
    g.span(y + 1, x0, x1, "o")                    # contact


def _staff(g, sx, top, bottom, hand_y):
    """Gold shaft, brightest near the orb. Sets the top of the silhouette."""
    for y in range(int(top), int(bottom) + 1):
        t = (y - top) / float(max(1, bottom - top))
        g.span(y, sx, sx + 1, "g" if t > 0.35 else "G")
    # The grip: a sleeve that REACHES the shaft, then the hand on it. One skin pixel beside the
    # pole is a speck, and leaves the staff looking propped rather than carried.
    reach = -4 if sx > CELL / 2 else 4
    g.span(int(hand_y), min(sx, sx + reach), max(sx, sx + reach), "b")
    g.span(int(hand_y) + 1, min(sx, sx + reach), max(sx, sx + reach), "a")
    g.set(int(sx) - 1, int(hand_y), "s")
    g.set(int(sx) - 1, int(hand_y) + 1, "n")
    g.set(int(sx) + 2, int(bottom), "o")


# Where the last-drawn orb was, so frames() can re-pulse it without threading a frame index
# through all four builders. Module state rather than a return value because the builders are
# also the readable record of each design and should stay free of animation plumbing.
LAST_ORB = [None]


def _orb(g, cx, cy, r=3.0):
    """The light. A halo is the fade - the solid part is just the lamp."""
    LAST_ORB[0] = (cx, cy, r)
    g.disc(cx, cy, r + 2.0, "v")
    g.disc(cx, cy, r + 0.6, "y")
    g.disc(cx, cy, r - 0.8, "Y")
    g.disc(cx, cy, r - 2.2, "Z")


def _eyes(g, y, xl, xr):
    """Last thing drawn, always. The 32px pass stamped the face void over these and every
    option came out blind."""
    g.set(xl, y, "Z")
    g.set(xr, y, "Y")


# --------------------------------------------------------------------------------------------

def conclave():
    """The brim, but TALL. A high crown carries the signature where width would have made a
    mascot, and the brim stops inside the shoulders."""
    g = Grid()
    sx = 34
    _staff(g, sx, 9, FEET, 30)
    # crown: tall, kinked, leaning off the vertical
    g.line(21, 3, 21, 5, "b")
    for y in range(5, 17):
        t = (y - 5) / 11.0
        g.span(y, 21 - 3.6 * t - 0.6, 21 + 3.6 * t + 0.6, "b")
    _shade(g, 3, 16, "c", "b", "a")
    # brim: 15 across against a 16-wide hem - inside the body, not outside it
    g.span(17, 16, 30, "a")
    g.span(18, 15, 31, "a")
    g.span(17, 17, 21, "b")
    g.set(15, 18, "W")
    # body
    _taper(g, 19, FEET, 3.4, 7.6, "b")
    _shade(g, 19, FEET, "c", "b", "a")
    _void(g, 19, 25, 4.2, 3.0)
    _beard(g, CX, 26, 36, 3.4, 0.9)
    _hem(g, FEET, 16, 31)
    _orb(g, sx + 0.5, 9, 3.0)
    _eyes(g, 21, 21, 26)
    return g.rows()


def vigil():
    """Hooded, no hat. Head and cloak are one unbroken mass and the only hole in it is the
    face - the leanest read of the four."""
    g = Grid()
    sx = 13
    _staff(g, sx, 7, FEET, 30)
    g.line(24, 5, 24, 7, "b")
    for y in range(7, 22):
        t = (y - 7) / 14.0
        g.span(y, 24 - 1.0 - 6.0 * t, 24 + 1.0 + 5.4 * t, "b")
    _taper(g, 20, FEET, 6.0, 7.8, "b", lean=1.8)
    _shade(g, 5, FEET, "c", "b", "a")
    g.span(15, 19, 29, "a")                       # the brow overhangs the cavity
    _void(g, 16, 23, 4.4, 3.2, cx=24.0)
    _beard(g, 24.0, 24, 37, 4.0, 0.9)
    _hem(g, FEET, 16, 33, gold=False)
    _orb(g, sx + 0.5, 7, 3.0)
    _eyes(g, 18, 21, 26)
    return g.rows()


def archmagus():
    """Crowned and pauldroned. The shoulders carry the width so the crown does not have to -
    which is the whole correction, drawn."""
    g = Grid()
    sx = 36
    _staff(g, sx, 11, FEET, 31)
    # a stepped crown, point broken, gold finial
    g.span(6, 21, 26, "b")
    for y, (lo, hi) in enumerate([(20, 27), (20, 27), (19, 28), (19, 28), (18, 29), (18, 29)]):
        g.span(7 + y, lo, hi, "b")
    _shade(g, 6, 12, "c", "b", "a")
    g.span(5, 22, 25, "g")
    g.set(23, 4, "H")
    g.span(13, 17, 30, "G")                       # a narrow gold band, not a wide brim
    g.span(14, 16, 31, "u")
    _taper(g, 15, FEET, 4.0, 7.8, "b")
    _shade(g, 15, FEET, "c", "b", "a")
    # Pauldrons: the width lives here, which only works if they are attached. The first pass
    # had them floating clear of the robe and they read as specks beside him, not shoulders on
    # him. Each row starts inside the body edge and steps outward.
    for i, y in enumerate(range(18, 23)):
        spread = [5.5, 6.5, 6.2, 5.0, 3.4][i]
        g.span(y, CX - 4.0 - spread, CX - 3.4, "a")
        g.span(y, CX + 3.4, CX + 4.0 + spread, "a")
    g.span(18, CX - 9.5, CX - 5.0, "b")           # a lit top edge, so they read as plate
    g.set(int(CX - 9.5), 18, "c")
    g.set(int(CX - 9.5), 19, "G")                 # the one gold rivet
    g.set(int(CX + 9.5), 19, "u")
    _void(g, 15, 21, 4.2, 3.4)
    _beard(g, CX, 22, 39, 3.2, 0.9)
    _hem(g, FEET, 15, 32)
    _orb(g, sx + 0.5, 11, 3.4)
    _eyes(g, 17, 21, 26)
    return g.rows()


def stormbound():
    """No staff in the hand - the light rides ahead of him, and the cloak is doing the talking.
    The most motion, and the one whose silhouette is least symmetric."""
    g = Grid()
    # cloak first and widest, flared behind to the right
    for y in range(20, FEET + 1):
        t = (y - 20) / float(FEET - 20)
        g.span(y, 25 + 1.5 * t, 28 + 10 * t, "a")
    _taper(g, 17, FEET, 3.6, 6.6, "b", lean=-1.6, cx=22.0)
    _shade(g, 17, FEET, "c", "b", "a")
    # hat blown back: the crown trails, the brim survives on the windward side only
    # ONE swept cone, base on the head, tip trailing downwind - built as a taper along a curved
    # axis rather than as separate leaning spans. Spans that each lean independently never close
    # into a shape, which is exactly why both earlier versions read as folded paper.
    for i, y in enumerate(range(7, 16)):
        t = i / 8.0
        axis = 27.5 - 8.0 * t * t
        hw = 0.8 + 5.2 * t
        g.span(y, axis - hw, axis + hw, "b")
    _shade(g, 7, 15, "c", "b", "a")
    g.span(16, 13, 27, "a")                       # brim, windward side only
    g.span(17, 12, 24, "a")
    g.set(13, 16, "W")
    g.set(12, 17, "W")
    _void(g, 18, 24, 3.8, 2.8, cx=20.0)
    _beard(g, 20.0, 25, 34, 3.0, 0.9, sweep=2.4)
    # the raised hand, and the light held out in front of it
    g.span(25, 10, 16, "b")
    g.span(26, 10, 16, "a")
    g.set(10, 25, "W")
    g.span(24, 10, 11, "s")
    _hem(g, FEET, 14, 39, gold=False)
    _orb(g, 8, 22, 3.0)
    _eyes(g, 20, 18, 22)
    return g.rows()


OPTIONS = [("conclave", conclave), ("vigil", vigil),
           ("archmagus", archmagus), ("stormbound", stormbound)]

# The idle. Two things move and both of them are the same thing: the figure breathes, and the
# light it carries breathes with it. That pairing is the lesson the title screen cost - a light
# source that holds still while the thing it lights moves reads as a decal stuck on the picture.
#
#   frame        0     1     2     3
#   body dy      0    -1    -1     0
#   orb radius  +0  +0.5  +0.5    +0
#
# One pixel, and it moves the feet with it. Two reads better but breaks the contract's anchor
# rule (feet on row cell-2 every frame), and an anchor that drifts is how a sprite ends up
# appearing to skate along the floor.
IDLE_DY = [0, -1, -1, 0]
IDLE_ORB = [0.0, 0.5, 0.5, 0.0]


def frames_for(fn):
    """Four frames from one pose: shift the whole cell, then re-pulse the orb in place."""
    out = []
    for f in range(4):
        rows = fn()
        orb = LAST_ORB[0]
        dy = IDLE_DY[f]
        if dy:
            blank = "." * CELL
            rows = (rows[-dy:] + [blank] * -dy) if dy < 0 else ([blank] * dy + rows[:-dy])
        if orb and IDLE_ORB[f]:
            g = Grid()
            g.g = [list(r) for r in rows]
            _orb(g, orb[0], orb[1] + dy, orb[2] + IDLE_ORB[f])
            rows = g.rows()
        out.append(rows)
    return out


def main():
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_wizards48")
    if not os.path.isdir(out):
        os.makedirs(out)
    sets, problems = [], []
    for name, fn in OPTIONS:
        fr = frames_for(fn)
        rows = fr[0]
        problems += P.check_anchor(name, fr, CELL)
        sets.append((name, fr))
        P.write_strip(fr, PALETTE, os.path.join(out, name + ".png"), CELL)
        # the number the correction is about
        body = [r for r in rows]
        widths = [len(r) - len(r.lstrip(".")) for r in body]
        cols = [(min(i for i, ch in enumerate(r) if ch != "."),
                 max(i for i, ch in enumerate(r) if ch != ".")) for r in body if r.strip(".")]
        used = [i for i, r in enumerate(rows) if r.strip(".")]
        h = used[-1] - used[0] + 1
        w = max(hi - lo + 1 for lo, hi in cols)
        print("%-11s %2dx%-2d  ratio %.1f:1" % (name, w, h, h / float(w)))
    P.preview(sets, PALETTE, os.path.join(out, "_gallery.png"), zoom=9)
    for p in problems:
        print("   ANCHOR: " + p)
    print("wrote %s" % out)


if __name__ == "__main__":
    main()
