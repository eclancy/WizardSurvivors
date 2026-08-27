from __future__ import print_function

import argparse
import codecs
import json
import os

from PIL import Image


def parse_args():
    parser = argparse.ArgumentParser(description="Generate suggested labels for curated asset frames.")
    parser.add_argument(
        "--metadata",
        default="assets/organized/level/props/curated/fantasy-dungeon-mines-curated/metadata.json",
        help="Path to the curated metadata JSON to enrich with suggestions.",
    )
    parser.add_argument(
        "--output",
        default="assets/organized/level/props/curated/fantasy-dungeon-mines-curated/suggestions.json",
        help="Path to write the suggestions JSON.",
    )
    return parser.parse_args()


def read_json(path):
    with codecs.open(path, "r", "utf-8-sig") as handle:
        return json.load(handle)


def write_json(path, payload):
    parent = os.path.dirname(path)
    if parent and not os.path.isdir(parent):
        os.makedirs(parent)
    with codecs.open(path, "w", "utf-8") as handle:
        json.dump(payload, handle, indent=2, sort_keys=False)
        handle.write("\n")


def load_image(path):
    image = Image.open(path).convert("RGBA")
    try:
        return image.copy()
    finally:
        image.close()


def quantized_palette(image):
    counts = {}
    width, height = image.size
    pixels = image.load()
    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            if a == 0:
                continue
            key = ((r // 32) * 32, (g // 32) * 32, (b // 32) * 32)
            counts[key] = counts.get(key, 0) + 1
    return sorted(counts.items(), key=lambda item: -item[1])


def pick_name_and_tags(image, width, height):
    palette = quantized_palette(image)
    if not palette:
        return "unknown object", ["curated"], "decoration", 0.05

    (r, g, b), count = palette[0]
    brightness = r + g + b
    aspect = float(width) / float(height) if height else 1.0

    if b >= r and b >= g:
        if aspect >= 1.4:
            name = "wide blue crystal cluster"
        elif height > width * 1.2:
            name = "tall blue crystal cluster"
        elif brightness > 400:
            name = "glowing blue crystal cluster"
        else:
            name = "blue crystal cluster"
        tags = ["crystal", "gem", "decoration", "floor"]
        role = "decoration"
        confidence = 0.86
    elif r >= g and r >= b:
        if brightness > 420 and height > width:
            name = "red crystal outcrop"
        elif width >= height * 1.2:
            name = "red crystal cluster"
        else:
            name = "red crystal outcrop"
        tags = ["crystal", "gem", "decoration", "floor"]
        role = "decoration"
        confidence = 0.84
    elif brightness < 180:
        if height >= width * 1.3:
            name = "collapsed mine doorway"
            tags = ["stone", "doorway", "rubble", "obstacle", "wall"]
            role = "obstacle"
            confidence = 0.77
        else:
            name = "stone rubble pile"
            tags = ["stone", "rubble", "obstacle", "floor"]
            role = "obstacle"
            confidence = 0.7
    else:
        if width > height * 1.4:
            name = "mine debris pile"
            tags = ["rubble", "debris", "obstacle", "floor"]
            role = "obstacle"
            confidence = 0.65
        else:
            name = "mine decoration"
            tags = ["decoration", "floor"]
            role = "decoration"
            confidence = 0.55

    alternatives = []
    if "blue" in name:
        alternatives = ["blue crystal cluster", "glowing blue crystal cluster", "medium blue crystal cluster"]
    elif "red" in name:
        alternatives = ["red crystal cluster", "red crystal outcrop", "huge red crystal cluster"]
    elif "doorway" in name:
        alternatives = ["collapsed mine doorway", "stone doorway", "rubble doorway"]
    else:
        alternatives = ["mine debris pile", "stone rubble pile", "mine decoration"]

    return name, tags, role, confidence, alternatives


def infer_tags_from_name(name):
    tokens = name.lower().replace("/", " ").replace("-", " ").split()
    tags = []
    if "crystal" in tokens:
        tags.append("crystal")
    if "gem" in tokens:
        tags.append("gem")
    if "doorway" in tokens:
        tags.extend(["doorway", "obstacle", "wall"])
    if "rubble" in tokens:
        tags.extend(["rubble", "obstacle"])
    if "stone" in tokens:
        tags.append("stone")
    if "blue" in tokens:
        tags.append("blue")
    if "red" in tokens:
        tags.append("red")
    if "large" in tokens:
        tags.append("large")
    if "medium" in tokens:
        tags.append("medium")
    if "small" in tokens:
        tags.append("small")
    if "cluster" in tokens:
        tags.append("cluster")
    if "outcrop" in tokens:
        tags.append("outcrop")
    if "wheelbarrow" in tokens or "cart" in tokens:
        tags.extend(["mine-cart", "obstacle"])
    if "decoration" not in tags:
        tags.append("decoration")
    if "floor" not in tags:
        tags.append("floor")
    # Keep ordering stable while removing duplicates.
    ordered = []
    for tag in tags:
        if tag not in ordered:
            ordered.append(tag)
    return ordered


def main():
    args = parse_args()
    metadata_path = os.path.abspath(args.metadata)
    repo_root = metadata_path
    for _ in range(7):
        repo_root = os.path.dirname(repo_root)
    metadata = read_json(metadata_path)
    frame_entries = metadata.get("frames", [])
    suggestions = {
        "generatedFrom": metadata_path,
        "setId": metadata.get("setId"),
        "framePrefix": metadata.get("framePrefix"),
        "items": [],
    }

    for frame in frame_entries:
        if frame.get("reviewStatus") == "reviewed":
            continue
        rel_path = frame["path"]
        abs_path = os.path.join(repo_root, rel_path.replace("/", os.sep))
        image = load_image(abs_path)
        try:
            if frame.get("semanticName"):
                name = frame["semanticName"]
                tags = infer_tags_from_name(name)
                role = "obstacle" if "obstacle" in tags or "wall" in tags else "decoration"
                confidence = 0.72
                alternatives = [name]
            else:
                name, tags, role, confidence, alternatives = pick_name_and_tags(image, frame["width"], frame["height"])
        finally:
            image.close()

        frame["suggestedSemanticName"] = name
        frame["suggestedTags"] = tags
        frame["suggestedGameplayRole"] = role
        frame["suggestedConfidence"] = round(confidence, 2)
        frame["suggestedAlternatives"] = alternatives
        suggestions["items"].append({
            "id": frame["id"],
            "suggestedSemanticName": name,
            "suggestedTags": tags,
            "suggestedGameplayRole": role,
            "suggestedConfidence": round(confidence, 2),
            "suggestedAlternatives": alternatives,
        })

    metadata["suggestionCoverage"] = {
        "pendingFrames": len(suggestions["items"]),
        "status": "precomputed",
    }
    write_json(metadata_path, metadata)
    write_json(os.path.abspath(args.output), suggestions)
    print("Wrote %d suggestions." % len(suggestions["items"]))


if __name__ == "__main__":
    main()
