from __future__ import print_function

import argparse
import codecs
import json
import os

from PIL import Image


def parse_args():
    parser = argparse.ArgumentParser(description="Generate machine-readable metadata for curated extracted asset sets.")
    parser.add_argument(
        "--level-root",
        default="assets/organized/level",
        help="Root folder that contains curated tiles/props sets.",
    )
    parser.add_argument(
        "--index",
        default="assets/organized/level/curated_extractions_index.json",
        help="Path to the curated extraction index JSON.",
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


def resolve_repo_root(index_path):
    return os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(index_path))))


def read_dimensions(path):
    image = Image.open(path)
    try:
        return image.size
    finally:
        image.close()


def build_frame_entry(set_data, frame_file, frame_index, repo_root):
    frame_path = os.path.join(set_data["absSetPath"], "frames", frame_file)
    width, height = read_dimensions(frame_path)
    rel_frame_path = os.path.relpath(frame_path, repo_root).replace("\\", "/")
    entry = {
        "id": os.path.splitext(frame_file)[0],
        "fileName": frame_file,
        "frameIndex": frame_index,
        "path": rel_frame_path,
        "width": width,
        "height": height,
        "semanticName": None,
        "tags": [],
        "notes": None,
        "reviewStatus": "unreviewed",
    }

    if set_data["framePrefix"] == "dun":
        columns = int(set_data.get("columns", 0) or 0)
        if columns > 0:
            row = (frame_index - 1) // columns
            col = (frame_index - 1) % columns
            entry["gridPosition"] = {"row": row, "column": col}
            entry["usageClass"] = "tile"
            entry["tags"] = ["floor-tile", "dungeon", "curated"]
    else:
        entry["usageClass"] = "prop"
        entry["tags"] = ["curated"]

    return entry


def enrich_set_metadata(existing_metadata, set_record, repo_root):
    abs_set_path = os.path.join(repo_root, set_record["path"].replace("/", os.sep))
    frames_dir = os.path.join(abs_set_path, "frames")
    frame_files = [name for name in os.listdir(frames_dir) if name.lower().endswith(".png")]
    frame_files.sort()

    set_data = {
        "id": set_record["id"],
        "category": set_record["category"],
        "framePrefix": set_record["framePrefix"],
        "absSetPath": abs_set_path,
        "columns": existing_metadata.get("columns"),
        "rows": existing_metadata.get("rows"),
    }

    frames = []
    for index, frame_file in enumerate(frame_files, 1):
        frames.append(build_frame_entry(set_data, frame_file, index, repo_root))

    metadata = dict(existing_metadata)
    metadata["metadataVersion"] = 1
    metadata["setId"] = set_record["id"]
    metadata["category"] = set_record["category"]
    metadata["framePrefix"] = set_record["framePrefix"]
    metadata["setPath"] = set_record["path"]
    metadata["frameCount"] = len(frames)
    metadata["semanticCoverage"] = {
        "namedFrames": 0,
        "totalFrames": len(frames),
        "status": "pending_semantic_annotation",
    }
    metadata["frames"] = frames
    return metadata


def main():
    args = parse_args()
    index_path = os.path.abspath(args.index)
    repo_root = resolve_repo_root(index_path)
    index_payload = read_json(index_path)
    sets = index_payload.get("sets", [])

    for set_record in sets:
        abs_set_path = os.path.join(repo_root, set_record["path"].replace("/", os.sep))
        metadata_path = os.path.join(abs_set_path, "metadata.json")
        existing_metadata = read_json(metadata_path)
        enriched = enrich_set_metadata(existing_metadata, set_record, repo_root)
        write_json(metadata_path, enriched)
        set_record["metadataPath"] = os.path.relpath(metadata_path, repo_root).replace("\\", "/")
        set_record["frameCount"] = enriched.get("frameCount", 0)
        set_record["semanticStatus"] = enriched.get("semanticCoverage", {}).get("status")

    index_payload["metadataVersion"] = 1
    index_payload["setCount"] = len(sets)
    index_payload["semanticStatus"] = "pending_semantic_annotation"
    write_json(index_path, index_payload)
    print("Updated curated metadata for %d set(s)." % len(sets))


if __name__ == "__main__":
    main()
