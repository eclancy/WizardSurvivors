# -*- coding: utf-8 -*-
"""The four basic enemies, designed as one family.

They are drawn together on purpose: the contract's whole thesis is that class reads from
silhouette, and silhouettes can only be judged against each other. Drawn one at a time they
would each look fine and collectively merge - which is exactly what the audit measured on the
old roster (Enemy and SlowEnemy scored 1.00 mask overlap).

Class assignment follows behaviour, not decoration:

  Enemy      HP x1.0  normal speed   -> Swarmer   narrow cowl, no visible limbs
  FastEnemy  HP x0.75 speed 185      -> Runner    lean, forward-leaning, rag streaming behind
  SlowEnemy  HP x1.4  speed 95       -> Bruiser   broad, upright, horned helm and pauldrons
  TankEnemy  HP x2.2  speed 115      -> Shielder  a flat plane held to one side, asymmetric

Size follows the collision radii already in the scenes (14 / - / 16 / 19), so the visual
ladder and the hitbox ladder agree.
"""
import bonelight as B
import pixel as P

CELL = 32

# Undead eyes are cold arcane light; the SkullSentry owns the fire ramp. Keeping the two
# emissives apart is what stops "glowing eyes in the dark" collapsing into one read.
COMMON = {
    "o": B.OCC,
    "W": B.RIM,
    "S": ("skin", "hi"),    "s": ("skin", "lit"),   "n": ("skin", "base"),
    "k": ("skin", "shade"), "K": ("skin", "deep"),
    "V": ("linen", "hi"),   "v": ("linen", "lit"),  "m": ("linen", "base"),
    "u": ("linen", "shade"), "U": ("linen", "deep"),
    "J": ("steel", "hi"),   "j": ("steel", "lit"),  "i": ("steel", "base"),
    "h": ("steel", "shade"), "H": ("steel", "deep"),
    "R": ("red", "hi"),     "r": ("red", "lit"),    "t": ("red", "base"),
    "T": ("red", "shade"),
    "G": ("gold", "lit"),   "g": ("gold", "base"),
    "Z": ("arcane", "hi"),  "Y": ("arcane", "lit"), "y": ("arcane", "base"),
}
PALETTE = B.build_palette(COMMON)

# --- Enemy: swarmer. Narrow pointed cowl, no visible limbs. The baseline.
SWARMER = [
    "", "", "", "", "", "", "", "",
    "..............oo................",
    ".............oWuo...............",
    ".............oWmuo..............",
    "............oWvmmuo.............",
    "............oWvmmmuo............",
    "...........oWvmoooumo...........",
    "...........oWvmYZYumo...........",
    "...........oWvmoooumo...........",
    "..........oWvvmmmmmuUo..........",
    "..........oWvvmmmmmuUo..........",
    ".........oWvvmmmmmmmuUo.........",
    ".........oWvmmmmmmmmuUo.........",
    ".........oWvmmmmmmmmuUo.........",
    ".........oWvmmmmmmmmuUo.........",
    "..........oWvmmmmmmuUo..........",
    "..........oWvmmmmmmuUo..........",
    "...........oWvmmmmuUo...........",
    "...........oWvmmmmuUo...........",
    "............oWvmmuUo............",
    "............oUvmmuo.............",
    "............oUmoumo.............",
    "............oUo.oUo.............",
    "............ooo.ooo.............",
    ".........oooooooooooo...........",
]

# --- FastEnemy: runner. Low and stretched, head thrown forward, rag streaming behind.
RUNNER = [
    "", "", "", "", "", "", "", "", "", "", "", "", "", "",
    "..................oooo..........",
    ".................oWvmuo.........",
    "................oWvmoumo........",
    "................oWvmYZumo.......",
    "...............oWvvmoouumo......",
    "......o........oWvmmmmmuUo......",
    ".....oto......oWvvmmmmmuUo......",
    "....otRto....oWvvmmmmmmuUo......",
    "...otRrrto..oWvmmmmmmmmuUo......",
    "..otRrrrrtooWvmmmmmmmmuUo.......",
    "...oTtrrrrtvmmmmmmmmmuUo........",
    "....oTTtrrrtmmmmmmmmuUo.........",
    ".....ooTTttrmmmmmmmuUo..........",
    "........ooTTtmmmmmuUo...........",
    "...........oUmmmmuUo............",
    "...........oUmoumUo.............",
    "...........oUo.oUo..............",
    "...........ooo.ooo..............",
    "........oooooooooooo............",
]

# --- SlowEnemy: bruiser. Square pauldrons and short thick horns break the shoulder line.
BRUISER = [
    "", "", "", "",
    "....ooo..............ooo........",
    "...ojjHo............oHjjo.......",
    "...ojiHo............oHijo.......",
    "....ojiHooooooooooooHijo........",
    ".....ojijjjjjjjjjjjjiijo........",
    "......ojihhhhhhhhhhhijo.........",
    ".....ojihHHHHHHHHHHHhijo........",
    ".....ojihHooooooooooHhijo.......",
    ".....ojihHoYZYoYZYoHhijo........",
    ".....ojihHooooooooooHhijo.......",
    ".....ojihHHHHHHHHHHHHhijo.......",
    "......ojiihhhhhhhhhiijo.........",
    "..ooooooojjiiiiiiijjooooooo.....",
    ".ojjjjjohhTTTTTTTThhojjjjjo.....",
    ".ojiiijohTtttttttThojiiiijo.....",
    ".ojiiijohTtRRRRtTThojiiiijo.....",
    ".ojjjjjohTTtttttTThojjjjjo......",
    "..oooooohTTTTTTTTThoooooo.......",
    "........ohTTTTTTTTho............",
    ".........ohhTTTThho.............",
    "..........ohHHHHho..............",
    ".........oHHHo.oHHHo............",
    ".........oHHo...oHHo............",
    ".........oHHo...oHHo............",
    ".........oHHo...oHHo............",
    ".........ooo.....ooo............",
    "......oooooooooooooooo..........",
]

# --- TankEnemy: shielder. The shield overlaps the torso so it reads as held, not parked
# alongside - a detached plane fails the fill-it-black test, which is how the first pass failed.
SHIELDER = [
    "", "", "", "", "", "", "",
    "................oooooo..........",
    "...............oWjiihho.........",
    "...............oWjihhhho........",
    ".......ooooo...oWjihoooho.......",
    "......oJJJJJo..oWjihYZYho.......",
    "......oJjjjJo..oWjihoooho.......",
    "......oJjjjJoo.oWjiihhhho.......",
    "......oJjGGjooooWjjiihhhho......",
    "......oJGGGGjooiWjjiihhhho......",
    "......oJGGGGjoiijjiiihhhho......",
    "......oJjGGjooiijjiiihhhho......",
    "......oJjjjJoo.oWjiiiihhho......",
    "......oJjjjJo..oWjiiiihhho......",
    "......oJJJJJo..oWjiiiihhho......",
    ".......oJjjJo..oWjiiihhho.......",
    ".......oJJJo...oWjiiihhho.......",
    "........oJo....oWjiihhho........",
    ".........o.....oWjiihhho........",
    "...............oHhiihhHo........",
    "...............oHho.oHho........",
    "...............oHho.oHho........",
    "...............oHho.oHho........",
    "...............oHho.oHho........",
    "...............ooo...ooo........",
    "............oooooooooooo........",
]

BASES = [
    ("Enemy (swarmer)", SWARMER),
    ("FastEnemy (runner)", RUNNER),
    ("SlowEnemy (bruiser)", BRUISER),
    ("TankEnemy (shielder)", SHIELDER),
]
