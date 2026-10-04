"""Offline S1 Practice benchmark runner. Standard library; never launches a process."""
import argparse
import csv
import hashlib
import json
import math
from pathlib import Path
import re
import statistics
import sys

PROFILE = "s1-practice-0ai"
SETTINGS = ("width", "height", "quality", "target_fps", "vsync")
BUDGETS = ("frame_mean_ms", "frame_p95_ms", "frame_p99_ms", "gc_bytes_per_frame",
           "gc_bytes_per_second", "managed_growth_bytes", "managed_slope_bytes_per_race")
HEADER = ["repeat", "frame", "elapsed_seconds", "frame_ms", "gc_allocated_bytes", "main_thread_ns"]


class Invalid(ValueError):
    pass


def need(condition, message):
    if not condition:
        raise Invalid(message)


def number(value, name, minimum=0, positive=False):
    need(type(value) in (int, float) and math.isfinite(value), name + ": finite number required")
    need(value > minimum if positive else value >= minimum, name + ": out of range")
    return value


def integer(value, name, minimum=0):
    need(type(value) is int and value >= minimum, name + ": integer required")
    return value


def label(value, name):
    need(isinstance(value, str) and value.strip() and len(value) <= 256, name + ": label required")
    need(not any(c in value for c in ("\\", "/", "\n", "\r")), name + ": use a label, not a path")
    need(value.lower() not in ("unknown", "unspecified", "todo"), name + ": unresolved label")
    return value


def digest(value, name, length=64):
    need(isinstance(value, str) and re.fullmatch(r"[0-9a-f]{%d}" % length, value), name + ": invalid hash")


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def reject_constant(_):
    raise Invalid("Non-finite JSON number")


def no_duplicate_keys(pairs):
    result = {}
    for key, value in pairs:
        need(key not in result, "Duplicate JSON key")
        result[key] = value
    return result


def parse_json(text):
    return json.loads(text, parse_constant=reject_constant, object_pairs_hook=no_duplicate_keys)


def read_json(path):
    return parse_json(path.read_text(encoding="utf-8-sig"))


def read_jsonl(path):
    rows = [parse_json(line) for line in path.read_text(encoding="utf-8-sig").splitlines() if line.strip()]
    need(rows and all(isinstance(row, dict) for row in rows), "Empty/invalid JSONL")
    return rows


def plan():
    settings = dict(width=1280, height=720, quality=2, target_fps=60, vsync=0)
    return {
        "schema_version": 1, "profile": PROFILE, "evidence_kind": "real", "complete": False,
        "source": {"head": None, "dirty": False, "patch_sha256": None},
        "build": {"type": "Development", "unity": None, "version": None, "backend": None, "artifact_sha256": None},
        "device": dict(os=None, cpu=None, gpu=None, graphics_api=None, ram_mb=None),
        "settings": {"requested": settings, "actual": None, "post_cleanup_actual": None,
                     "snapshot_policy": "measured-window-v2"},
        "workload": dict(seed=1729, seed_applied=False, route_id="RaceMap", route_sha256=None,
                         controller_id="solo-physical-feedback-ad-v1", controller_sha256=None,
                         input_method="dynamic-keyboard-ad", loadout=None, skin_id=None, ai_count=0,
                         physical_driving=True, teleports=0, synthetic_finishes=0, time_scale=1, laps_to_finish=3),
        "sampling": dict(policy="s1-30s-60s-3-v1", warmup_seconds=30, measure_seconds=60, repeats=3,
                         gc_counter={"available": False, "reason": "not captured",
                                     "method": "ProfilerRecorder", "name": "GC Allocated In Frame",
                                     "unit": "bytes", "sample_policy": "nonwrapping-count-advance-v2"},
                         main_counter={"available": False, "reason": "not captured"}),
        "windows": [], "frames_sha256": None, "error_count": None,
        "endurance": dict(head=None, build_sha256=None, operator_sha256=None, sha256=None),
        "escapes": dict(head=None, build_sha256=None, sha256=None),
        "budgets": dict.fromkeys(BUDGETS),
    }


def strict14_order():
    result = []
    for main in range(1, 11):
        result.append((main, False))
        if main in (3, 6, 9, 10):
            result.append((main, True))
    return result


def check_settings(settings, cleanup=False):
    need(isinstance(settings, dict) and set(settings) == set(SETTINGS), "Settings fields incomplete")
    for name in SETTINGS:
        integer(settings[name], name, -1 if cleanup and name == "target_fps" else
                1 if name in ("width", "height", "target_fps") else 0)


def metadata(meta):
    need(meta["schema_version"] == 1 and type(meta["schema_version"]) is int, "Unsupported schema")
    need(meta["profile"] == PROFILE, "Only S1 Practice 0 AI supported")
    need(meta["evidence_kind"] in ("real", "synthetic"), "Evidence kind missing/invalid")
    need(meta["complete"] is True, "Capture not complete")
    need(integer(meta["error_count"], "error_count") == 0, "Runtime errors captured")
    source = meta["source"]
    digest(source["head"], "source.head", 40)
    need(type(source["dirty"]) is bool, "source.dirty required")
    if source["dirty"]:
        digest(source["patch_sha256"], "source.patch_sha256")
    else:
        need(source["patch_sha256"] is None, "Clean source must have null patch hash")
    build = meta["build"]
    need(build["type"] in ("Release", "Development"), "Player build required; Editor is not a baseline")
    for key in ("unity", "version", "backend"):
        label(build[key], "build." + key)
    digest(build["artifact_sha256"], "build.artifact_sha256")
    for key in ("os", "cpu", "gpu", "graphics_api"):
        label(meta["device"][key], "device." + key)
    integer(meta["device"]["ram_mb"], "ram_mb", 1)
    for key in ("requested", "actual"):
        check_settings(meta["settings"][key])
    need(meta["settings"]["requested"] == meta["settings"]["actual"], "Requested/actual settings differ")
    policy = meta["settings"].get("snapshot_policy")
    need(policy in (None, "measured-window-v2"), "Unsupported settings snapshot policy")
    if policy is not None:
        check_settings(meta["settings"]["post_cleanup_actual"], cleanup=True)
    work = meta["workload"]
    need(integer(work["ai_count"], "ai_count") == 0, "AI is not an S1 workload")
    integer(work["seed"], "seed")
    need(integer(work["laps_to_finish"], "laps_to_finish", 1) == 3, "S1 route requires three laps")
    need(work["seed_applied"] is True and work["physical_driving"] is True, "Seed/control provenance missing")
    need(integer(work["teleports"], "teleports") == 0 and integer(work["synthetic_finishes"], "synthetic_finishes") == 0,
         "Nonphysical intervention invalidates benchmark")
    need(number(work["time_scale"], "time_scale") == 1, "Time scale changed")
    for key in ("route_id", "controller_id", "skin_id"):
        label(work[key], key)
    for key in ("route_sha256", "controller_sha256"):
        digest(work[key], key)
    need(work["input_method"] == "dynamic-keyboard-ad", "Unsupported input method")
    need(isinstance(work["loadout"], list) and len(work["loadout"]) == 3, "Three loadout identities required")
    for value in work["loadout"]:
        label(value, "loadout")
    sampling = meta["sampling"]
    need((sampling["policy"], sampling["warmup_seconds"], sampling["measure_seconds"], sampling["repeats"])
         == ("s1-30s-60s-3-v1", 30, 60, 3), "Unsupported measurement policy")
    for key in ("gc_counter", "main_counter"):
        counter = sampling[key]
        need(type(counter["available"]) is bool, key + ": availability required")
        if not counter["available"]:
            label(counter["reason"], key + ".reason")
    gc = sampling["gc_counter"]
    if meta["settings"].get("snapshot_policy") == "measured-window-v2":
        need((gc.get("method"), gc.get("name"), gc.get("unit"), gc.get("sample_policy")) ==
             ("ProfilerRecorder", "GC Allocated In Frame", "bytes", "nonwrapping-count-advance-v2"),
             "GC capture method provenance missing or unsupported")
        need(not gc["available"] or build["type"] == "Development",
             "GC Allocated In Frame requires Development capture; do not relabel Release samples")
    for key in ("endurance", "escapes"):
        evidence = meta[key]
        digest(evidence["head"], key + ".head", 40)
        need(evidence["head"] == source["head"], key + ": game head differs")
        for field in ("sha256", "build_sha256"):
            digest(evidence[field], key + "." + field)
    # A dirty production tree cannot be matched by head alone across the evidence lanes.
    need(source["dirty"] is False, "Commit game source before qualifying evidence from separate builds")
    digest(meta["endurance"]["operator_sha256"], "endurance.operator_sha256")
    for key in BUDGETS:
        value = meta["budgets"][key]
        if value is not None:
            number(value, "budget." + key)


def percentile(values, q):
    """Nearest rank: sorted[ceil(q*n)-1], including n=1."""
    return sorted(values)[max(0, math.ceil(q * len(values)) - 1)]


def distribution(values):
    return dict(mean=statistics.mean(values), p95=percentile(values, .95), p99=percentile(values, .99))


def frames_analysis(meta, path):
    with path.open(encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        need(reader.fieldnames == HEADER, "Frame CSV header mismatch")
        rows = list(reader)
    need(rows, "No frame samples")
    groups = {i: [] for i in range(1, 4)}
    last_repeat = 1
    for row in rows:
        need(None not in row and all(row[k] is not None for k in HEADER), "Malformed CSV row")
        repeat = int(row["repeat"])
        need(repeat in groups and repeat >= last_repeat, "Unexpected/reordered repeat")
        last_repeat = repeat
        groups[repeat].append(row)
    windows = meta["windows"]
    need(isinstance(windows, list) and len(windows) == 3, "Three windows required")
    results = []
    previous_end = -1
    race_ids = set()
    for repeat, window in enumerate(windows, 1):
        need(integer(window["repeat"], "repeat", 1) == repeat, "Window order mismatch")
        need(integer(window["seed"], "window.seed") == meta["workload"]["seed"], "Repeat seed differs")
        need(window["route_id"] == meta["workload"]["route_id"], "Repeat route differs")
        label(window["scene"], "scene")
        need(window["scene"] == windows[0]["scene"], "Scene differs across repeats")
        digest(window["input_sha256"], "input_sha256")
        race_id = integer(window["race_id"], "race_id", 1)
        need(race_id not in race_ids, "Each repeat must start a separate physical race")
        race_ids.add(race_id)
        need(window["stage"] == "driving", "Only natural driving frames are measured")
        for key in ("forced_gc_count", "scene_change_count", "ai_count"):
            need(integer(window[key], key) == 0, "Window contaminated: " + key)
        check_settings(window["actual"])
        need(window["actual"] == meta["settings"]["actual"], "Window settings changed")
        if meta["settings"].get("snapshot_policy") == "measured-window-v2":
            check_settings(window["actual_end"])
            need(window["actual_end"] == window["actual"], "Window end settings changed")
            need(integer(window["settings_change_count"], "settings_change_count") == 0,
                 "Settings changed inside measured window")
        warmup = number(window["warmup_actual_seconds"], "warmup_actual_seconds")
        need(warmup >= 30, "Insufficient driving warmup")
        start = number(window["start_seconds"], "start_seconds")
        end = number(window["end_seconds"], "end_seconds")
        driving_start = number(window["driving_start_seconds"], "driving_start_seconds")
        driving_end = number(window["driving_end_seconds"], "driving_end_seconds")
        need(abs(start-driving_start-warmup) <= .002 and end <= driving_end,
             "Window/warmup outside uninterrupted physical driving")
        need(start - warmup >= previous_end and end > start, "Overlapping windows/warmup or invalid clock")
        previous_end = end
        need(integer(window["dropped_frames"], "dropped_frames") == 0, "Dropped frames")
        duration = number(window["duration_seconds"], "duration_seconds", positive=True)
        need(abs((end - start) - duration) <= .002, "Window clock duration mismatch")
        group = groups[repeat]
        need(len(group) == integer(window["frames"], "frames", 1), "Frame count mismatch")
        frame_values, gc_values, main_values = [], [], []
        previous_index, elapsed = None, 0.0
        for row in group:
            index = int(row["frame"])
            need(index >= 0 and (previous_index is None or index == previous_index + 1), "Noncontiguous frames")
            previous_index = index
            dt = number(float(row["frame_ms"]), "frame_ms", positive=True)
            at = number(float(row["elapsed_seconds"]), "elapsed_seconds", positive=True)
            need(at > elapsed and abs((at - elapsed) * 1000 - dt) <= .1, "Frame interval/clock mismatch")
            elapsed = at
            frame_values.append(dt)
            for column, counter, values in (("gc_allocated_bytes", "gc_counter", gc_values),
                                            ("main_thread_ns", "main_counter", main_values)):
                available = meta["sampling"][counter]["available"]
                if row[column] == "":
                    values.append(None)
                else:
                    need(available, column + ": values supplied for unavailable counter")
                    value = int(row[column])
                    integer(value, column, 1 if column == "main_thread_ns" else 0)
                    values.append(value)
        need(abs(elapsed - duration) <= .002, "CSV/manifest duration mismatch")
        need(duration >= 60 and duration - frame_values[-1] / 1000 < 60 + 1e-6,
             "Window must stop on first frame at/after 60 seconds")
        gc_complete = all(v is not None for v in gc_values)
        main_complete = all(v is not None for v in main_values)
        results.append(dict(repeat=repeat, frames=len(group), seconds=duration,
                            actual_fps=len(group) / duration, frame_ms=distribution(frame_values),
                            gc_bytes_per_frame=statistics.mean(gc_values) if gc_complete else None,
                            gc_bytes_per_second=sum(gc_values) / duration if gc_complete else None,
                            gc_missing_frames=sum(v is None for v in gc_values),
                            main_thread_ms=distribution([v / 1e6 for v in main_values]) if main_complete else None))
    return results


def one(rows, kind, note=None):
    found = [r for r in rows if r["kind"] == kind and (note is None or r.get("note") == note)]
    need(len(found) == 1, "Missing/duplicate " + kind + (":" + note if note else ""))
    return found[0]


def checkpoint(row, expected_racers):
    for key in ("buddahs", "reporters"):
        need(integer(row[key], key) == expected_racers, "Residual/missing racer objects")
    integer(row["networkObjects"], "networkObjects")
    need(row["forcedGc"] is True, "Settled post-GC checkpoint required")
    number(row["managedAfterFullGc"], "managedAfterFullGc", positive=True)
    # Unity/Mono counters may be unavailable in some Players. Preserve missingness.
    for key in ("profilerUsedAfterGc", "monoUsedAfterGc", "monoHeapAfterGc"):
        if row.get(key) is not None:
            number(row[key], key)


def memory_trend(rows):
    values = [r["managedAfterFullGc"] for r in rows]
    x = [r["mainIndex"] for r in rows]
    mid = statistics.mean(x)
    slope = sum((a-mid) * (b-statistics.mean(values)) for a, b in zip(x, values)) / sum((a-mid)**2 for a in x)
    optional = {}
    for key in ("profilerUsedAfterGc", "monoUsedAfterGc", "monoHeapAfterGc"):
        raw = [r.get(key) for r in rows]
        optional[key] = raw if all(v is not None and v > 0 for v in raw) else None
    return dict(samples=len(values), managed_bytes=values, managed_growth_bytes=max(0, max(values)-values[0]),
                managed_final_delta_bytes=values[-1]-values[0], managed_slope_bytes_per_race=slope,
                optional_counters=optional)


def endurance_analysis(rows):
    need(all(r.get("plan") == "strict14" for r in rows), "Only strict14 lifecycle accepted")
    previous = -1
    for row in rows:
        now = number(row["realtime"], "endurance.realtime")
        need(now >= previous, "Endurance clock moved backwards")
        previous = now
        need(row["kind"] not in ("failure", "unity-error", "incomplete"), "Endurance failure/error/incomplete record")
    summary = one(rows, "summary")
    need(rows[-1] is summary, "Summary must terminate endurance stream")
    need(summary["sequencePassed"] is True, "Endurance sequence failed")
    for key, value in dict(mainCompleted=10, extrasCompleted=4, rematchClicks=10, homeReturns=4, errorCount=0).items():
        need(integer(summary[key], key) == value, "Endurance summary count mismatch")
    initial = one(rows, "checkpoint", "home-initial")
    checkpoint(initial, 0)
    results, homes, timings = [], [], []
    last_action = initial["realtime"]
    for serial, (main, extra) in enumerate(strict14_order(), 1):
        race = [r for r in rows if r.get("raceSerial") == serial]
        need(race, "Missing physical race")
        need(all(r.get("mainIndex") == main and type(r.get("extra")) is bool and r["extra"] == extra for r in race),
             "Race/main/extra identity mismatch")
        selection = one(race, "checkpoint", "selection-ready")
        go = one(race, "checkpoint", "GO-first-observed")
        movement = one(race, "checkpoint", "movement-unlocked")
        finish = one(race, "natural-finish-timing")
        ready = one(race, "checkpoint", "results-interactive")
        held = one(race, "checkpoint", "results-after-hold")
        completed = one(race, "race-completed")
        action = one(race, "button", "ReturnHome" if extra else "Rematch")
        times = [last_action] + [r["realtime"] for r in (selection, go, movement, finish, ready, held, completed, action)]
        need(all(a <= b for a, b in zip(times, times[1:])), "Race stage order invalid")
        need(finish["realtime"] > movement["realtime"], "No actual driving interval")
        need(finish["realtime"] - selection["realtime"] <= 1500, "Race deadline exceeded")
        need(held["realtime"] - ready["realtime"] >= (35 if serial == 1 else 2), "Results hold incomplete")
        for row in (go, movement):
            need(integer(row["buddahs"], "buddahs") == 1 and integer(row["reporters"], "reporters") == 1,
                 "Expected one racer at GO")
        for row in (ready, held):
            checkpoint(row, 1)
        drive = [r for r in race if r["kind"] == "drive"]
        need(len(drive) >= 2 and all(movement["realtime"] <= r["realtime"] <= finish["realtime"] for r in drive),
             "Missing/out-of-stage physical driving observations")
        for row in drive:
            need(row["steering"] in (-1, 0, 1), "Invalid steering observation")
            for key in ("position", "velocity"):
                for axis in ("x", "y", "z"):
                    value = row[key][axis]
                    need(type(value) in (int, float) and math.isfinite(value), "Nonfinite physical observation")
        laps = finish["lapSeconds"]
        need(isinstance(laps, list) and len(laps) == 3, "Three complete lap durations required")
        for value in laps:
            number(value, "lapSeconds", positive=True)
        number(finish["total"], "total", positive=True)
        need(abs(sum(laps) - finish["total"]) <= 1e-6, "Lap/total mismatch")
        observations = finish["laps"]
        need(isinstance(observations, list), "Missing lap observations")
        crossings = [r for r in observations if r["lap"] >= 2]
        need([r["lap"] for r in crossings] == list(range(2, len(laps)+2)), "Incomplete observed lap sequence")
        previous_clock = number(finish["goUpper"], "goUpper")
        residuals = []
        for lap, crossing in zip(laps, crossings):
            lower = number(crossing["lowerClock"], "lowerClock")
            upper = number(crossing["upperClock"], "upperClock")
            need(lower <= upper and upper > previous_clock, "Invalid lap observation interval")
            residuals.append(lap-(upper-previous_clock))
            previous_clock = upper
        need(len(finish["lapResiduals"]) == len(laps), "Missing timing residuals")
        for expected, actual in zip(residuals, finish["lapResiduals"]):
            need(type(actual) in (float, int) and math.isfinite(actual) and abs(expected-actual) <= 1e-5,
                 "Timing residual inconsistent")
        timings.append(dict(race=serial, total_seconds=finish["total"], lap_seconds=laps,
                            lap_residual_seconds=residuals))
        last_action = action["realtime"]
        if not extra and main > 2:
            results.append(held)
        if extra:
            home = one(race, "checkpoint", "home-clean")
            checkpoint(home, 0)
            need(home["realtime"] >= last_action, "Home before return action")
            need(home["realtime"] - last_action <= 120, "ReturnHome deadline exceeded")
            homes.append(home)
            last_action = home["realtime"]
    expected_serials = set(range(1, 15))
    need(all(type(r.get("raceSerial")) is int and r["raceSerial"] in expected_serials for r in rows), "Unexpected race serial")
    need(sum(r["kind"] == "button" and r.get("note") == "Rematch" for r in rows) == 10, "Extra Rematch")
    need(sum(r["kind"] == "button" and r.get("note") == "ReturnHome" for r in rows) == 4, "Extra ReturnHome")
    for checkpoints in (results, homes):
        need(len({r["networkObjects"] for r in checkpoints}) == 1, "Same-stage NetworkObject count drift")
    need(len({len(r["lap_seconds"]) for r in timings}) == 1, "Lap target changed between races")
    need(summary["realtime"] - initial["realtime"] <= 25200, "Endurance overall deadline exceeded")
    return dict(main_races=10, extra_races=4, rematches=10, home_returns=4,
                timing_consistency="VALID", timing_precision="NOT_MEASURED",
                timings=timings, results_memory=memory_trend(results), home_memory=memory_trend(homes))


def escapes_analysis(rows):
    need(len(rows) == 3 and {r["stage"] for r in rows} == {"selection", "intro", "driving"}, "Three Esc stages required")
    for row in rows:
        times = [number(row[key], key) for key in ("escape_pressed_seconds", "dialog_seconds", "confirm_seconds", "home_seconds", "reopened_seconds")]
        need(all(a < b for a, b in zip(times, times[1:])), "Esc stage ordering invalid")
        for key in ("input_blocked", "dialog_visible", "home_clean", "clock_cleared", "timing_cleared", "reopened"):
            need(row[key] is True, "Esc check incomplete: " + key)
        for key in ("client_started", "server_started"):
            need(row[key] is False, "Esc left a running session")
        for key in ("buddahs", "reporters", "error_count"):
            need(integer(row[key], key) == 0, "Esc cleanup/error check failed")
        need(row["confirm_button"] == "ConfirmQuit", "Normal confirmation button required")
    return dict(stages=["selection", "intro", "driving"], status="VALID")


def compare(candidate, baseline):
    # Deliberately compare raw, validated bundles, not a user-edited summary report.
    keys = ("profile", "device", "workload", "sampling")
    for key in keys:
        need(candidate[key] == baseline[key], "Baseline incompatible: " + key)
    # Home cleanup configuration is retained as evidence, outside the workload.
    # Never ignore either requested settings or measured settings in comparisons.
    for key in ("requested", "actual", "snapshot_policy"):
        need(candidate["settings"].get(key) == baseline["settings"].get(key), "Baseline incompatible: settings")
    for key in ("type", "unity", "backend"):
        need(candidate["build"][key] == baseline["build"][key], "Baseline build configuration differs")
    need(candidate["evidence_kind"] == baseline["evidence_kind"] == "real", "Only real Solo baselines can be compared")


def analyze(bundle):
    report = dict(schema_version=1, profile=PROFILE, status="NOT_PASSED", benchmark_passed=False,
                  evidence_valid=False, failures=[], missing=[], timing_precision="NOT_MEASURED")
    try:
        meta = read_json(bundle / "run.json")
        need(isinstance(meta, dict), "Manifest must be a JSON object")
        report["evidence_kind"] = meta.get("evidence_kind")
        metadata(meta)
        for filename, expected in (("frames.csv", meta["frames_sha256"]), ("endurance.jsonl", meta["endurance"]["sha256"]),
                                   ("escapes.jsonl", meta["escapes"]["sha256"])):
            digest(expected, filename + " hash")
            need(sha(bundle / filename) == expected, filename + " hash mismatch")
        report["windows"] = frames_analysis(meta, bundle / "frames.csv")
        report["endurance"] = endurance_analysis(read_jsonl(bundle / "endurance.jsonl"))
        report["escapes"] = escapes_analysis(read_jsonl(bundle / "escapes.jsonl"))
        report["source_head"] = meta["source"]["head"]
        report["build_type"] = meta["build"]["type"]
        report["settings"] = meta["settings"]
        report["evidence_valid"] = True
        for window in report["windows"]:
            if window["gc_missing_frames"]:
                report["missing"].append("GC unavailable in repeat " + str(window["repeat"]))
        metrics = {}
        for suffix in ("mean", "p95", "p99"):
            metrics["frame_" + suffix + "_ms"] = max(w["frame_ms"][suffix] for w in report["windows"])
        for key in ("gc_bytes_per_frame", "gc_bytes_per_second"):
            values = [w[key] for w in report["windows"]]
            metrics[key] = max(values) if all(v is not None for v in values) else None
        memory = [report["endurance"][key] for key in ("results_memory", "home_memory")]
        for key in ("managed_growth_bytes", "managed_slope_bytes_per_race"):
            metrics[key] = max(row[key] for row in memory)
        report["budget_metrics"] = metrics
        report["budgets"] = meta["budgets"]
        for key, value in metrics.items():
            limit = meta["budgets"][key]
            if limit is None:
                report["missing"].append("Budget undecided: " + key)
            elif value is not None and value > limit:
                report["failures"].append("Budget exceeded: " + key)
        report["baseline_candidate"] = not report["missing"] and not report["failures"] and meta["evidence_kind"] == "real"
        if meta["evidence_kind"] == "synthetic":
            report["status"] = "SYNTHETIC_ONLY"
        elif not report["missing"] and not report["failures"]:
            report["status"] = "PASSED"
            report["benchmark_passed"] = True
    except (Invalid, KeyError, TypeError, ValueError, OSError, OverflowError) as exc:
        # Never echo raw log text, private paths, offending values or stacks.
        message = str(exc) if isinstance(exc, Invalid) else "Malformed/missing input (" + type(exc).__name__ + ")"
        report["failures"].append(message)
    return report


def inspect_strict14(path):
    rows = read_jsonl(path)
    result = dict(status="NOT_PASSED", benchmark_passed=False, scope="endurance only",
                  records=len(rows), missing=["frame samples", "capture metadata", "fixed seed and window evidence", "Esc receipts", "budgets"])
    try:
        result["endurance"] = endurance_analysis(rows)
    except (Invalid, KeyError, TypeError, ValueError) as exc:
        result["endurance_error"] = str(exc) if isinstance(exc, Invalid) else "Incomplete/unsupported endurance fields"
    return result


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, allow_nan=False) + "\n", encoding="utf-8")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    p = sub.add_parser("plan", help="Write capture template only; launches nothing")
    p.add_argument("--out", required=True, type=Path)
    p = sub.add_parser("run", help="Validate and analyze a completed capture bundle offline")
    p.add_argument("--bundle", required=True, type=Path)
    p.add_argument("--out", required=True, type=Path)
    p.add_argument("--baseline", type=Path)
    p = sub.add_parser("inspect-strict14", help="Read legacy endurance; never qualifies performance")
    p.add_argument("--input", required=True, type=Path)
    p.add_argument("--out", required=True, type=Path)
    args = parser.parse_args(argv)
    try:
        if args.command == "plan":
            need(not args.out.exists(), "Refusing to overwrite existing plan")
            write_json(args.out, plan())
            print("PLAN_ONLY; no runtime work performed")
            return 0
        inputs = ([args.bundle / name for name in ("run.json", "frames.csv", "endurance.jsonl", "escapes.jsonl")]
                  if args.command == "run" else [args.input])
        if args.command == "run" and args.baseline:
            inputs += [args.baseline / name for name in ("run.json", "frames.csv", "endurance.jsonl", "escapes.jsonl")]
        need(args.out.resolve() not in [p.resolve() for p in inputs], "Output would overwrite evidence")
        if args.command == "inspect-strict14":
            result = inspect_strict14(args.input)
        else:
            result = analyze(args.bundle)
            if args.baseline:
                baseline = analyze(args.baseline)
                try:
                    need(result["benchmark_passed"] and baseline["benchmark_passed"], "Both baseline and candidate must qualify")
                    compare(read_json(args.bundle / "run.json"), read_json(args.baseline / "run.json"))
                    result["comparison"] = {key: {"baseline": old, "candidate": result["budget_metrics"][key],
                                                 "delta": result["budget_metrics"][key] - old}
                                            for key, old in baseline["budget_metrics"].items()}
                except Invalid as exc:
                    result["status"] = "NOT_PASSED"
                    result["benchmark_passed"] = False
                    result["baseline_candidate"] = False
                    result["failures"].append(str(exc))
        write_json(args.out, result)
        print(result["status"])
        return 0 if result["benchmark_passed"] else 1
    except (Invalid, KeyError, TypeError, ValueError, OSError) as exc:
        print(str(exc) if isinstance(exc, Invalid) else "Unable to read/write benchmark input or output", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
