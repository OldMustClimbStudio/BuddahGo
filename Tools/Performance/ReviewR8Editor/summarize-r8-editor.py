"""Summarize actual 3 x 60 s Editor measurements; never fabricate missing cells."""
from pathlib import Path
import csv, hashlib, json, statistics, sys
from datetime import datetime, timezone, timedelta

root = Path(sys.argv[1])
cells = {}
sources = {"pre-p2": "707376f3bf0e33ad8586eba8034a9fe33caf68c8",
           "screen-on": "6129b59a3e4a7076963937d5b2b34eb7b0ecf5af",
           "log-only": "f1ee5ab929d7b78a6611148b53a6876474d6eb4f"}

def percentile(values, fraction):
    values = sorted(values)
    index = (len(values) - 1) * fraction
    lower = int(index)
    return values[lower] + (values[min(lower + 1, len(values)-1)]-values[lower])*(index-lower)

missing = []
for label, revision in sources.items():
    for role in ("host", "client"):
        runs = []
        for run in range(1, 4):
            stem = f"{label}-{role}-{run}"
            meta_path, csv_path = root / (stem + ".json"), root / (stem + ".csv")
            if not meta_path.exists() or not csv_path.exists():
                missing.append(stem)
                continue
            meta = json.loads(meta_path.read_text())
            assert meta["status"] == "complete", stem
            assert meta["revision"] == revision and meta["build"] == "Editor", stem
            assert (meta["width"], meta["height"], meta["quality"], meta["warmupSeconds"], meta["requestedSeconds"]) == (1920, 1080, 2, 30, 60), stem
            rows = list(csv.DictReader(csv_path.open()))
            assert len(rows) == meta["frames"] and meta["measuredSeconds"] >= 60, stem
            gc = [int(row["gc_allocated_bytes"]) for row in rows]
            main = [int(row["main_thread_ns"])/1e6 for row in rows]
            frames = [float(row["frame_ms"]) for row in rows]
            assert min(gc) >= 0 and min(main) > 0, stem
            end = datetime.fromtimestamp(meta_path.stat().st_mtime, timezone.utc)
            runs.append({"run": run, "metadata": meta,
                         "approximateWindowUtc": {"start": (end-timedelta(seconds=meta['measuredSeconds'])).isoformat(), "end": end.isoformat(), "method": "metadata file mtime immediately after counters stop, minus actual measured duration; not an in-probe UTC timestamp"},
                         "gcMeanBytesPerFrame": statistics.mean(gc),
                         "gcBytesPerSecond": sum(gc)/meta["measuredSeconds"],
                         "mainMeanMs": statistics.mean(main), "mainP95Ms": percentile(main, .95),
                         "frameMeanMs": statistics.mean(frames), "frameP95Ms": percentile(frames, .95),
                         "csvSha256": hashlib.sha256(csv_path.read_bytes()).hexdigest()})
        keys = ["gcMeanBytesPerFrame", "gcBytesPerSecond", "mainMeanMs", "mainP95Ms", "frameMeanMs", "frameP95Ms"]
        cells[label+"/"+role] = {"runs": runs, "medianOfRuns": {k: statistics.median(r[k] for r in runs) for k in keys} if runs else {}, "rangeOfRuns": {k: [min(r[k] for r in runs), max(r[k] for r in runs)] for k in keys} if runs else {}}
result = {"protocol": "Two local Unity Editors; same source on peers; 1920x1080; quality 2; 0 ms simulator; RaceMap; 30 s after-GO warmup + 60 s capture; 3 fresh pairs per source; detailed trace and screenshot capture absent. Sequential groups, uncontrolled machine background load; target60 is not an enforced Editor frame lock.", "complete": not missing, "missing": missing, "cells": cells}
(root/"analysis.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
for cell, data in cells.items():
    print(cell, len(data["runs"]), json.dumps(data["medianOfRuns"]))
print("complete", result["complete"], "missing", len(missing))
