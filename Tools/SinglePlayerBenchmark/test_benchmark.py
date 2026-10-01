"""Synthetic-only tests. --fixture writes explicitly synthetic demonstration data."""
import copy
import csv
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from contextlib import redirect_stdout

import benchmark as b


HASH = "a" * 64
HEAD = "b" * 40


def make_fixture(directory):
    directory.mkdir(parents=True, exist_ok=True)
    meta = b.plan()
    meta.update(evidence_kind="synthetic", complete=True, error_count=0)
    meta["source"]["head"] = HEAD
    meta["build"].update(unity="2022.3.synthetic", version="test-only", backend="Mono", artifact_sha256=HASH)
    meta["device"].update(os="Synthetic OS", cpu="Synthetic CPU", gpu="Synthetic GPU", graphics_api="Synthetic API", ram_mb=16384)
    meta["settings"]["actual"] = copy.deepcopy(meta["settings"]["requested"])
    meta["workload"].update(seed_applied=True, route_sha256=HASH, controller_sha256=HASH,
                            loadout=["skill1", "skill2", "skill3"], skin_id="skin1")
    meta["sampling"]["gc_counter"] = dict(available=True, reason=None)
    meta["sampling"]["main_counter"] = dict(available=True, reason=None)
    meta["budgets"] = dict(frame_mean_ms=50, frame_p95_ms=50, frame_p99_ms=50,
                           gc_bytes_per_frame=2, gc_bytes_per_second=40,
                           managed_growth_bytes=0, managed_slope_bytes_per_race=0)
    with (directory / "frames.csv").open("w", newline="", encoding="utf-8") as stream:
        writer = csv.writer(stream, lineterminator="\n")
        writer.writerow(b.HEADER)
        for repeat in range(1, 4):
            start = 30 + (repeat-1) * 100
            meta["windows"].append(dict(repeat=repeat, seed=1729, route_id="RaceMap", scene="RaceMap",
                input_sha256=HASH, actual=copy.deepcopy(meta["settings"]["actual"]),
                warmup_actual_seconds=30, start_seconds=start, end_seconds=start+60,
                duration_seconds=60, frames=1200, dropped_frames=0, race_id=repeat, stage="driving",
                driving_start_seconds=start-30, driving_end_seconds=start+60,
                forced_gc_count=0, scene_change_count=0, ai_count=0))
            for frame in range(1, 1201):
                writer.writerow([repeat, repeat * 10000 + frame, frame * .05, 50, 2, 1000000])
    rows = []
    clock = 0

    def emit(kind, note=None, **fields):
        nonlocal clock
        clock += 1
        row = dict(kind=kind, note=note, realtime=clock, plan="strict14", raceSerial=serial,
                   mainIndex=main, extra=extra)
        row.update(fields)
        rows.append(row)
        return row

    def checkpoint(note, count):
        return emit("checkpoint", note, buddahs=count, reporters=count, networkObjects=10 if count else 2,
            forcedGc=True, managedAfterFullGc=100000, profilerUsedAfterGc=200000,
            monoUsedAfterGc=100000, monoHeapAfterGc=300000)

    for serial, (main, extra) in enumerate(b.strict14_order(), 1):
        if serial == 1:
            checkpoint("home-initial", 0)
        checkpoint("selection-ready", 0)
        checkpoint("GO-first-observed", 1)
        checkpoint("movement-unlocked", 1)
        for x in (1, 2):
            emit("drive", steering=0, position=dict(x=x, y=0, z=0), velocity=dict(x=1, y=0, z=0))
        clock += 300
        emit("natural-finish-timing", total=300, lapSeconds=[100, 100, 100], goUpper=0,
            lapResiduals=[0, 0, 0], laps=[dict(lap=n+2, lowerClock=(n+1)*100-.05, upperClock=(n+1)*100) for n in range(3)])
        checkpoint("results-interactive", 1)
        clock += 35 if serial == 1 else 2
        checkpoint("results-after-hold", 1)
        emit("race-completed")
        emit("button", "ReturnHome" if extra else "Rematch")
        if extra:
            checkpoint("home-clean", 0)
    emit("summary", sequencePassed=True, mainCompleted=10, extrasCompleted=4, rematchClicks=10, homeReturns=4, errorCount=0)
    write_rows(directory / "endurance.jsonl", rows)
    escapes = []
    for stage in ("selection", "intro", "driving"):
        escapes.append(dict(stage=stage, escape_pressed_seconds=1, dialog_seconds=2, confirm_seconds=3,
            home_seconds=4, reopened_seconds=5, input_blocked=True, dialog_visible=True, confirm_button="ConfirmQuit",
            home_clean=True, buddahs=0, reporters=0, client_started=False, server_started=False,
            clock_cleared=True, timing_cleared=True, reopened=True, error_count=0))
    write_rows(directory / "escapes.jsonl", escapes)
    meta["endurance"].update(head=HEAD, build_sha256=HASH, operator_sha256=HASH)
    meta["escapes"].update(head=HEAD, build_sha256=HASH)
    save_manifest(directory, meta)
    return meta


def write_rows(path, rows):
    path.write_text("".join(json.dumps(r, allow_nan=False) + "\n" for r in rows), encoding="utf-8")


def save_manifest(directory, meta):
    meta["frames_sha256"] = b.sha(directory / "frames.csv")
    for key in ("endurance", "escapes"):
        meta[key]["sha256"] = b.sha(directory / (key + ".jsonl"))
    b.write_json(directory / "run.json", meta)


class BenchmarkTests(unittest.TestCase):
    def test_esc_stages_match_s1_design_not_results(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder)
            make_fixture(path)
            rows = b.read_jsonl(path / "escapes.jsonl")
            self.assertEqual(b.escapes_analysis(rows)["stages"], ["selection", "intro", "driving"])
            rows[1]["stage"] = "results"
            with self.assertRaises(b.Invalid):
                b.escapes_analysis(rows)

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.meta = make_fixture(self.root)

    def report(self):
        save_manifest(self.root, self.meta)
        return b.analyze(self.root)

    def invalid(self):
        report = self.report()
        self.assertFalse(report["benchmark_passed"])
        self.assertFalse(report["evidence_valid"])
        self.assertTrue(report["failures"])

    def edit_rows(self, filename, mutate):
        path = self.root / filename
        rows = b.read_jsonl(path)
        mutate(rows)
        write_rows(path, rows)

    def test_fixture_analysis_and_synthetic_separation(self):
        report = self.report()
        self.assertEqual(report["status"], "SYNTHETIC_ONLY")
        self.assertTrue(report["evidence_valid"])
        self.assertFalse(report["benchmark_passed"])
        self.assertEqual(report["failures"], [])
        self.assertEqual(report["missing"], [])
        for w in report["windows"]:
            self.assertEqual(w["frame_ms"], dict(mean=50, p95=50, p99=50))
            self.assertEqual(w["actual_fps"], 20)
            self.assertEqual(w["gc_bytes_per_frame"], 2)
            self.assertEqual(w["gc_bytes_per_second"], 40)
        self.assertEqual(report["endurance"]["results_memory"]["samples"], 8)
        self.assertEqual(report["timing_precision"], "NOT_MEASURED")

    def test_real_status_branch_only_with_complete_evidence(self):
        # Branch-coverage fixture inside temporary storage; this is not real evidence.
        self.meta["evidence_kind"] = "real"
        self.assertTrue(self.report()["benchmark_passed"])
        self.meta["budgets"]["frame_mean_ms"] = None
        self.assertFalse(self.report()["benchmark_passed"])

    def test_percentile_nearest_rank(self):
        self.assertEqual(b.percentile(list(range(1, 101)), .95), 95)
        self.assertEqual(b.percentile(list(range(1, 101)), .99), 99)
        self.assertEqual(b.percentile([7], .99), 7)

    def test_missing_metadata_fields(self):
        for section, key in (("build", "backend"), ("workload", "seed"), ("device", "gpu"), ("endurance", "head")):
            with self.subTest(section=section, key=key):
                saved = self.meta[section].pop(key)
                self.invalid()
                self.meta[section][key] = saved

    def test_metadata_rejections(self):
        mutations = [("workload", "ai_count", 1), ("workload", "teleports", 1), ("workload", "seed_applied", False),
                     ("workload", "synthetic_finishes", 1), ("workload", "physical_driving", False),
                     ("workload", "time_scale", 2), ("workload", "laps_to_finish", 1),
                     ("workload", "seed", True), ("source", "dirty", True),
                     ("endurance", "head", "c"*40), ("build", "type", "Editor")]
        for section, key, value in mutations:
            with self.subTest(key=key):
                original = self.meta[section][key]
                self.meta[section][key] = value
                self.invalid()
                self.meta[section][key] = original

    def test_incomplete_capture(self):
        self.meta["complete"] = False
        self.invalid()

    def test_whole_capture_errors(self):
        self.meta["error_count"] = 1
        self.invalid()

    def test_unset_budgets_not_passed(self):
        self.meta["budgets"] = dict.fromkeys(b.BUDGETS)
        report = self.report()
        self.assertEqual(len(report["missing"]), 7)
        self.assertFalse(report["benchmark_passed"])

    def test_budget_violation(self):
        self.meta["budgets"]["gc_bytes_per_second"] = 39
        self.assertIn("Budget exceeded: gc_bytes_per_second", self.report()["failures"])

    def test_gc_missing_is_null_not_zero(self):
        path = self.root / "frames.csv"
        text = path.read_text().replace(",2,1000000", ",,1000000")
        path.write_text(text)
        self.meta["sampling"]["gc_counter"] = dict(available=False, reason="unsupported")
        report = self.report()
        self.assertTrue(report["evidence_valid"])
        self.assertIsNone(report["windows"][0]["gc_bytes_per_frame"])
        self.assertIsNone(report["windows"][0]["gc_bytes_per_second"])
        self.assertEqual(len(report["missing"]), 3)

    def test_one_missing_gc_sample_invalidates_rate(self):
        path = self.root / "frames.csv"
        path.write_text(path.read_text().replace(",2,1000000", ",,1000000", 1))
        report = self.report()
        self.assertEqual(report["windows"][0]["gc_missing_frames"], 1)
        self.assertIsNone(report["windows"][0]["gc_bytes_per_second"])

    def test_unavailable_counter_cannot_report_zero(self):
        self.meta["sampling"]["gc_counter"] = dict(available=False, reason="unsupported")
        self.invalid()

    def test_frame_corruption(self):
        path = self.root / "frames.csv"
        original = path.read_text()
        for before, after in (("1,10002,", "1,10001,"), (",50,2,", ",NaN,2,"),
                              (",50,2,", ",50,-1,"), ("1,10001,0.05", "1,10001,0.07")):
            with self.subTest(after=after):
                path.write_text(original.replace(before, after, 1))
                self.invalid()
        path.write_text(original)

    def test_truncated_frames(self):
        path = self.root / "frames.csv"
        path.write_text("\n".join(path.read_text().splitlines()[:-1]) + "\n")
        self.invalid()

    def test_window_cannot_include_gc_scene_or_nondriving(self):
        window = self.meta["windows"][0]
        for key, value in (("forced_gc_count", 1), ("scene_change_count", 1), ("ai_count", 1),
                           ("stage", "results"), ("driving_end_seconds", 80)):
            with self.subTest(key=key):
                original = window[key]
                window[key] = value
                self.invalid()
                window[key] = original

    def test_repeats_cannot_reuse_one_race(self):
        self.meta["windows"][1]["race_id"] = 1
        self.invalid()

    def test_short_measurement_even_with_consistent_frame_count(self):
        path = self.root / "frames.csv"
        rows = path.read_text().splitlines()
        path.write_text("\n".join(r for r in rows if not r.startswith("1,11200,")) + "\n")
        self.meta["windows"][0].update(frames=1199, duration_seconds=59.95, end_seconds=89.95)
        self.invalid()

    def test_short_window_warmup_drops_and_settings(self):
        window = self.meta["windows"][0]
        for key, value in (("warmup_actual_seconds", 29), ("dropped_frames", 1), ("duration_seconds", 59),
                           ("seed", 1), ("route_id", "OtherRoute")):
            with self.subTest(key=key):
                original = window[key]
                window[key] = value
                self.invalid()
                window[key] = original
        window["actual"]["width"] = 800
        self.invalid()

    def test_endurance_cannot_trust_summary(self):
        self.edit_rows("endurance.jsonl", lambda rows: rows.__setitem__(slice(None), [rows[-1]]))
        self.invalid()

    def test_missing_lifecycle_stages(self):
        original = (self.root / "endurance.jsonl").read_text()
        for kind, note in (("natural-finish-timing", None), ("checkpoint", "results-after-hold"),
                           ("checkpoint", "home-clean"), ("checkpoint", "movement-unlocked")):
            with self.subTest(kind=kind, note=note):
                (self.root / "endurance.jsonl").write_text(original)
                self.edit_rows("endurance.jsonl", lambda rows: rows.remove(next(r for r in rows if r["kind"] == kind and (note is None or r["note"] == note))))
                self.invalid()

    def test_duplicate_finish(self):
        def mutate(rows):
            i = next(i for i, r in enumerate(rows) if r["kind"] == "natural-finish-timing")
            rows.insert(i, copy.deepcopy(rows[i]))
        self.edit_rows("endurance.jsonl", mutate)
        self.invalid()

    def test_incomplete_laps(self):
        def mutate(rows):
            r = next(r for r in rows if r["kind"] == "natural-finish-timing")
            r["lapSeconds"] = [100, 200]
        self.edit_rows("endurance.jsonl", mutate)
        self.invalid()

    def test_lap_total_mismatch(self):
        self.edit_rows("endurance.jsonl", lambda rows: next(r for r in rows if r["kind"] == "natural-finish-timing").update(total=301))
        self.invalid()

    def test_lap_observation_missing(self):
        self.edit_rows("endurance.jsonl", lambda rows: next(r for r in rows if r["kind"] == "natural-finish-timing")["laps"].pop())
        self.invalid()

    def test_memory_slope_and_budget(self):
        def mutate(rows):
            for r in rows:
                if r["note"] == "results-after-hold" and not r["extra"]:
                    r["managedAfterFullGc"] += r["mainIndex"] * 100
        self.edit_rows("endurance.jsonl", mutate)
        report = self.report()
        trend = report["endurance"]["results_memory"]
        self.assertEqual(trend["managed_growth_bytes"], 700)
        self.assertEqual(trend["managed_slope_bytes_per_race"], 100)
        self.assertIn("Budget exceeded: managed_growth_bytes", report["failures"])

    def test_residual_objects(self):
        self.edit_rows("endurance.jsonl", lambda rows: next(r for r in rows if r["note"] == "home-clean").update(buddahs=1))
        self.invalid()

    def test_home_growth_is_gated_too(self):
        def mutate(rows):
            for row in rows:
                if row["note"] == "home-clean":
                    row["managedAfterFullGc"] += row["mainIndex"] * 1000
        self.edit_rows("endurance.jsonl", mutate)
        report = self.report()
        self.assertEqual(report["budget_metrics"]["managed_slope_bytes_per_race"], 1000)
        self.assertEqual(report["budget_metrics"]["managed_growth_bytes"], 7000)
        self.assertIn("Budget exceeded: managed_growth_bytes", report["failures"])

    def test_network_object_drift(self):
        self.edit_rows("endurance.jsonl", lambda rows: next(r for r in rows if r["note"] == "home-clean").update(networkObjects=3))
        self.invalid()

    def test_no_managed_metric(self):
        self.edit_rows("endurance.jsonl", lambda rows: next(r for r in rows if r["note"] == "home-clean").update(managedAfterFullGc=0))
        self.invalid()

    def test_optional_mono_zero_missing_not_usage_zero(self):
        def mutate(rows):
            for row in rows:
                if row["kind"] == "checkpoint":
                    row["monoUsedAfterGc"] = 0
        self.edit_rows("endurance.jsonl", mutate)
        self.assertIsNone(self.report()["endurance"]["results_memory"]["optional_counters"]["monoUsedAfterGc"])

    def test_error_receipt(self):
        self.edit_rows("endurance.jsonl", lambda rows: rows[2].update(kind="unity-error", note="PRIVATE SECRET /private/path"))
        report = self.report()
        self.assertNotIn("PRIVATE SECRET", json.dumps(report))
        self.assertFalse(report["evidence_valid"])

    def test_escapes_required_and_real_cleanup(self):
        self.edit_rows("escapes.jsonl", lambda rows: rows.pop())
        self.invalid()

    def test_escape_input_must_block(self):
        self.edit_rows("escapes.jsonl", lambda rows: rows[0].update(input_blocked=False))
        self.invalid()

    def test_hash_mismatch(self):
        (self.root / "frames.csv").write_text("changed")
        report = b.analyze(self.root)  # deliberately do not recompute hashes
        self.assertIn("frames.csv hash mismatch", report["failures"])

    def test_nan_json_rejected(self):
        (self.root / "run.json").write_text('{"schema_version": NaN}')
        self.assertFalse(b.analyze(self.root)["evidence_valid"])

    def test_duplicate_json_key_rejected(self):
        with self.assertRaises(b.Invalid):
            b.parse_json('{"complete": false, "complete": true}')

    def test_nonobject_manifest_fails_without_traceback(self):
        (self.root / "run.json").write_text('[]')
        self.assertFalse(b.analyze(self.root)["benchmark_passed"])

    def test_compare_excludes_online_and_mismatched_config(self):
        a, c = copy.deepcopy(self.meta), copy.deepcopy(self.meta)
        a["evidence_kind"] = c["evidence_kind"] = "real"
        b.compare(a, c)
        c["profile"] = "online-r8"
        with self.assertRaises(b.Invalid):
            b.compare(a, c)
        c = copy.deepcopy(a)
        c["device"]["gpu"] = "different"
        with self.assertRaises(b.Invalid):
            b.compare(a, c)

    def test_cli_plan_run_and_evidence_overwrite_protection(self):
        out = self.root / "report.json"
        with redirect_stdout(io.StringIO()):
            self.assertEqual(b.main(["plan", "--out", str(self.root / "plan.json")]), 0)
            self.assertEqual(b.main(["run", "--bundle", str(self.root), "--out", str(out)]), 1)
        self.assertEqual(b.read_json(out)["status"], "SYNTHETIC_ONLY")
        self.assertEqual(b.main(["run", "--bundle", str(self.root), "--out", str(self.root / "run.json")]), 2)

    def test_legacy_inspection_never_passes_performance(self):
        report = b.inspect_strict14(self.root / "endurance.jsonl")
        self.assertEqual(report["endurance"]["main_races"], 10)
        self.assertFalse(report["benchmark_passed"])

    def test_cli_comparison_requires_compatible_real_qualified_bundles(self):
        baseline = self.root / "baseline"
        other = make_fixture(baseline)
        # Synthetic numbers exercise the branch, kept in disposable test storage.
        self.meta["evidence_kind"] = other["evidence_kind"] = "real"
        save_manifest(self.root, self.meta)
        save_manifest(baseline, other)
        args = ["run", "--bundle", str(self.root), "--baseline", str(baseline), "--out", str(self.root / "compare.json")]
        with redirect_stdout(io.StringIO()):
            self.assertEqual(b.main(args), 0)
            other["device"]["gpu"] = "different"
            save_manifest(baseline, other)
            self.assertEqual(b.main(args), 1)
        self.assertFalse(b.read_json(self.root / "compare.json")["benchmark_passed"])


if __name__ == "__main__":
    if len(sys.argv) == 3 and sys.argv[1] == "--fixture":
        target = Path(sys.argv[2])
        if target.exists():
            sys.exit("Fixture destination must not exist")
        make_fixture(target)
        print("SYNTHETIC_ONLY fixture written; no Unity/Player work performed")
    else:
        unittest.main()
