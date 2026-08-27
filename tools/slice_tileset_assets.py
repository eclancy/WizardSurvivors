from __future__ import print_function

import argparse
import codecs
import json
import os
import shutil
import zlib
from datetime import datetime

from PIL import Image


def parse_args():
    parser = argparse.ArgumentParser(description="Slice tileset/spritesheet assets using tileset_registry.json")
    parser.add_argument(
        "--registry",
        default="assets/organized/tileset_registry.json",
        help="Path to tileset registry JSON",
    )
    parser.add_argument(
        "--output-root",
        default="assets/organized/derived/slices",
        help="Output folder for sliced assets",
    )
    parser.add_argument(
        "--asset-id",
        action="append",
        default=[],
        help="Optional assetId filter (can be provided multiple times)",
    )
    parser.add_argument("--limit", type=int, default=0, help="Optional max number of assets to slice")
    parser.add_argument("--dry-run", action="store_true", help="Compute output plan without writing files")
    return parser.parse_args()


def resolve_repo_root(registry_path):
    return os.path.dirname(os.path.dirname(os.path.dirname(registry_path)))


def ensure_dir(path):
    if not os.path.isdir(path):
        os.makedirs(path)


def build_output_dir_name(asset_id):
    digest = "%08x" % (zlib.crc32(asset_id.encode("utf-8")) & 0xFFFFFFFF)
    prefix = asset_id[:40].rstrip("-")
    return "%s-%s" % (prefix, digest)


def clear_output_dir(output_dir):
    if os.path.isdir(output_dir):
        shutil.rmtree(output_dir)
    os.makedirs(output_dir)


def select_entries(entries, requested_ids, limit):
    filtered = [entry for entry in entries if entry.get("includeAutoSlice", False)]
    if requested_ids:
        requested = set(requested_ids)
        filtered = [entry for entry in filtered if entry.get("assetId") in requested]
    if limit > 0:
        filtered = filtered[:limit]
    return filtered


def slice_grid(image, cell_width, cell_height):
    frames = []
    columns = image.size[0] // cell_width
    rows = image.size[1] // cell_height
    for row in range(rows):
        for col in range(columns):
            left = col * cell_width
            top = row * cell_height
            right = left + cell_width
            bottom = top + cell_height
            frame = image.crop((left, top, right, bottom))
            frames.append((row, col, frame))
    return frames


def extract_spaced_objects(image, min_width, min_height, alpha_threshold, padding):
    width, height = image.size
    pixels = image.load()
    visited = [False] * (width * height)

    def idx(x, y):
        return (y * width) + x

    components = []
    directions = ((1, 0), (-1, 0), (0, 1), (0, -1))

    for y in range(height):
        for x in range(width):
            flat = idx(x, y)
            if visited[flat]:
                continue
            visited[flat] = True
            if pixels[x, y][3] < alpha_threshold:
                continue

            stack = [(x, y)]
            min_x = x
            max_x = x
            min_y = y
            max_y = y

            while stack:
                cx, cy = stack.pop()
                for dx, dy in directions:
                    nx = cx + dx
                    ny = cy + dy
                    if nx < 0 or ny < 0 or nx >= width or ny >= height:
                        continue
                    n_flat = idx(nx, ny)
                    if visited[n_flat]:
                        continue
                    visited[n_flat] = True
                    if pixels[nx, ny][3] >= alpha_threshold:
                        stack.append((nx, ny))
                        if nx < min_x:
                            min_x = nx
                        if nx > max_x:
                            max_x = nx
                        if ny < min_y:
                            min_y = ny
                        if ny > max_y:
                            max_y = ny

            comp_width = max_x - min_x + 1
            comp_height = max_y - min_y + 1
            if comp_width < min_width or comp_height < min_height:
                continue

            left = max(0, min_x - padding)
            top = max(0, min_y - padding)
            right = min(width, max_x + 1 + padding)
            bottom = min(height, max_y + 1 + padding)
            area = (right - left) * (bottom - top)

            crop = image.crop((left, top, right, bottom))
            components.append(
                {
                    "x": left,
                    "y": top,
                    "w": right - left,
                    "h": bottom - top,
                    "area": area,
                    "image": crop,
                }
            )

    components.sort(key=lambda item: (item["y"], item["x"], -item["area"]))
    return components


def write_json(path, payload):
    ensure_dir(os.path.dirname(path))
    with codecs.open(path, "w", "utf-8") as f:
        json.dump(payload, f, indent=2, sort_keys=False)
        f.write("\n")


def utc_now_iso():
    return datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ")


def main():
    args = parse_args()
    registry_path = os.path.abspath(args.registry)
    output_root = os.path.abspath(args.output_root)

    with codecs.open(registry_path, "r", "utf-8") as f:
        registry = json.load(f)

    entries = registry.get("entries", [])
    selected = select_entries(entries, args.asset_id, args.limit)
    repo_root = resolve_repo_root(registry_path)

    if not args.dry_run:
        ensure_dir(output_root)

    index_rows = []
    for entry in selected:
        asset_id = entry["assetId"]
        source_path = os.path.join(repo_root, entry["targetRelative"].replace("/", os.sep))
        asset_output_dir = os.path.join(output_root, build_output_dir_name(asset_id))
        frame_dir = os.path.join(asset_output_dir, "frames")
        slice_mode = entry.get("sliceMode", "tile_grid")

        image = Image.open(source_path).convert("RGBA")
        try:
            if slice_mode == "spaced_objects":
                min_width = int(entry.get("componentMinWidth", 8))
                min_height = int(entry.get("componentMinHeight", 8))
                alpha_threshold = int(entry.get("alphaThreshold", 1))
                padding = int(entry.get("componentPadding", 1))
                components = extract_spaced_objects(image, min_width, min_height, alpha_threshold, padding)
                frame_count = len(components)
                grid_columns = int(entry.get("columns", 0))
                grid_rows = int(entry.get("rows", 0))
            else:
                cell_width = int(entry["cellWidth"])
                cell_height = int(entry["cellHeight"])
                frames = slice_grid(image, cell_width, cell_height)
                frame_count = len(frames)
                grid_columns = image.size[0] // cell_width
                grid_rows = image.size[1] // cell_height
        finally:
            image.close()

        if args.dry_run:
            row = {
                "assetId": asset_id,
                "source": source_path,
                "sliceMode": slice_mode,
                "frameCount": frame_count,
                "dryRun": True,
            }
            if slice_mode != "spaced_objects":
                row["cellWidth"] = int(entry["cellWidth"])
                row["cellHeight"] = int(entry["cellHeight"])
            index_rows.append(row)
            continue

        clear_output_dir(asset_output_dir)
        ensure_dir(frame_dir)

        image = Image.open(source_path).convert("RGBA")
        try:
            if slice_mode == "spaced_objects":
                min_width = int(entry.get("componentMinWidth", 8))
                min_height = int(entry.get("componentMinHeight", 8))
                alpha_threshold = int(entry.get("alphaThreshold", 1))
                padding = int(entry.get("componentPadding", 1))
                components = extract_spaced_objects(image, min_width, min_height, alpha_threshold, padding)
                for i, comp in enumerate(components):
                    frame_name = "o%04d_x%04d_y%04d.png" % (i, comp["x"], comp["y"])
                    comp["image"].save(os.path.join(frame_dir, frame_name), format="PNG")
                    comp["image"].close()
            else:
                cell_width = int(entry["cellWidth"])
                cell_height = int(entry["cellHeight"])
                frames = slice_grid(image, cell_width, cell_height)
                for row_i, col_i, frame in frames:
                    frame_name = "r%03d_c%03d.png" % (row_i, col_i)
                    frame.save(os.path.join(frame_dir, frame_name), format="PNG")
        finally:
            image.close()

        metadata = {
            "assetId": asset_id,
            "sourceRelative": entry.get("sourceRelative"),
            "targetRelative": entry.get("targetRelative"),
            "category": entry.get("category"),
            "inferredKind": entry.get("inferredKind"),
            "sliceMode": slice_mode,
            "imageWidth": entry.get("imageWidth"),
            "imageHeight": entry.get("imageHeight"),
            "columns": grid_columns,
            "rows": grid_rows,
            "frameCount": frame_count,
            "generatedAtUtc": utc_now_iso(),
        }
        if slice_mode == "spaced_objects":
            metadata["componentMinWidth"] = int(entry.get("componentMinWidth", 8))
            metadata["componentMinHeight"] = int(entry.get("componentMinHeight", 8))
            metadata["alphaThreshold"] = int(entry.get("alphaThreshold", 1))
            metadata["componentPadding"] = int(entry.get("componentPadding", 1))
        else:
            metadata["cellWidth"] = int(entry["cellWidth"])
            metadata["cellHeight"] = int(entry["cellHeight"])

        write_json(os.path.join(asset_output_dir, "metadata.json"), metadata)

        index_rows.append(
            {
                "assetId": asset_id,
                "source": source_path,
                "outputDir": asset_output_dir,
                "frameCount": frame_count,
                "sliceMode": slice_mode,
                "cellWidth": entry.get("cellWidth"),
                "cellHeight": entry.get("cellHeight"),
            }
        )

    index_payload = {
        "generatedAtUtc": utc_now_iso(),
        "registryPath": registry_path,
        "outputRoot": output_root,
        "assetCount": len(index_rows),
        "assets": index_rows,
    }

    if args.dry_run:
        print(json.dumps(index_payload, indent=2))
    else:
        ensure_dir(output_root)
        write_json(os.path.join(output_root, "index.json"), index_payload)
        print("Sliced %d assets into %s" % (len(index_rows), output_root))


if __name__ == "__main__":
    main()
