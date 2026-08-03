from PIL import Image
import os

src = r"assets/imported/fantasy/source_mirror/Fantasy Dungeon tilesets/Fantasy_Dungeon_A1_darker.png"
out_dir = r"assets/tilesets/candidate_previews"
os.makedirs(out_dir, exist_ok=True)

img = Image.open(src).convert("RGBA")
coords = [
    (0, 0), (16, 0), (32, 0), (48, 0), (64, 0), (80, 0),
    (96, 0), (112, 0), (128, 0), (144, 0), (160, 0), (176, 0),
    (0, 16), (16, 16), (32, 16), (48, 16), (64, 16), (80, 16),
]

for i, (x, y) in enumerate(coords[:12]):
    tile = img.crop((x, y, x + 16, y + 16))
    tile.save(os.path.join(out_dir, f"candidate_{i:02d}_{x}_{y}.png"))

print(f"saved {len(coords[:12])} tiles to {out_dir}")
