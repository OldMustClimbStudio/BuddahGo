"""Validate real R8SceneCapture outputs and print per-run + across-run summaries.

Usage: python Tools/Performance/summarize_r8.py PATH_TO_CAPTURE_OUTPUT
Requires five complete repeats for every label/role; never fills missing data.
"""
import csv
import json
import statistics
import sys
from collections import defaultdict
from pathlib import Path


def percentile(values, quantile):
    values = sorted(values)
    return values[round((len(values) - 1) * quantile)]


def summarize(directory):
    groups = defaultdict(list)
    configurations = set()
    for path in sorted(Path(directory).glob("*.json")):
        meta = json.loads(path.read_text(encoding="utf-8-sig"))
        if "requestedSeconds" not in meta:
            continue
        if meta["status"] != "complete":
            raise ValueError(f"{path.name}: {meta['status']}")
        with path.with_suffix(".csv").open(encoding="utf-8-sig", newline="") as stream:
            frames = list(csv.DictReader(stream))
        if len(frames) != meta["frames"] or not frames:
            raise ValueError(f"{path.name}: empty/mismatched frame count")
        if meta["revision"] == "UNSPECIFIED" or meta["label"] == "UNSPECIFIED":
            raise ValueError(f"{path.name}: missing revision/label")
        elapsed = float(frames[-1]["elapsed_seconds"])
        if elapsed < meta["requestedSeconds"] or abs(elapsed - meta["measuredSeconds"]) > .01:
            raise ValueError(f"{path.name}: incomplete duration")
        indices = [int(row["frame"]) for row in frames]
        if any(b != a + 1 for a, b in zip(indices, indices[1:])):
            raise ValueError(f"{path.name}: missing/duplicate frames")
        main_ms = [int(row["main_thread_ns"]) / 1e6 for row in frames]
        gc = [int(row["gc_allocated_bytes"]) for row in frames]
        if min(main_ms) <= 0 or min(gc) < 0:
            raise ValueError(f"{path.name}: invalid profiler counter values")
        configurations.add(tuple(meta[key] for key in (
            "unity", "scene", "cpu", "gpu", "operatingSystem", "build", "width", "height", "quality",
            "targetFps", "vSync", "warmupSeconds", "requestedSeconds")))
        result = dict(label=meta["label"], role=meta["role"], run=meta["run"], revision=meta["revision"],
                      frames=len(frames), seconds=elapsed, main_mean_ms=statistics.mean(main_ms),
                      main_p95_ms=percentile(main_ms, .95), gc_mean_bytes=statistics.mean(gc),
                      gc_total_bytes=sum(gc), gc_bytes_per_second=sum(gc) / elapsed)
        groups[(meta["label"], meta["role"])].append(result)
    if not groups:
        raise ValueError("No real capture files found; R8 is not measured.")
    if len(configurations) != 1:
        raise ValueError("Build/hardware/scene/capture settings differ; split into comparable datasets.")
    for key, runs in groups.items():
        if sorted(row["run"] for row in runs) != [1, 2, 3, 4, 5]:
            raise ValueError(f"{key}: need exactly runs 1..5")
        if len({row["revision"] for row in runs}) != 1:
            raise ValueError(f"{key}: source revisions differ")
    writer = csv.DictWriter(sys.stdout, fieldnames=next(iter(groups.values()))[0].keys(), lineterminator="\n")
    writer.writeheader()
    for runs in groups.values():
        writer.writerows(runs)
    for key, runs in groups.items():
        for metric in ("main_mean_ms", "main_p95_ms", "gc_mean_bytes", "gc_bytes_per_second"):
            values = [run[metric] for run in runs]
            print(f"{key} {metric}: median={statistics.median(values):.6f} min={min(values):.6f} max={max(values):.6f}", file=sys.stderr)


if __name__ == "__main__":
    try:
        summarize(sys.argv[1])
    except (IndexError, ValueError, KeyError, OSError) as error:
        sys.exit(str(error))
