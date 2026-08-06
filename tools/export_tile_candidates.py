from PIL import Image
import argparse
import os

parser = argparse.ArgumentParser(description="Export tile candidate previews from a source image")
parser.add_argument("src", help="Path to the source image")
parser.add_argument("--out-dir", default=r"assets/tilesets/candidate_previews", help="Output directory for previews")
args = parser.parse_args()

src = args.src
out_dir = args.out_dir
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
