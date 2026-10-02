"""Render an A1 evidence directory without third-party dependencies.

Usage: python Tools/ai/plot_a1.py /absolute/path/to/run
Writes trajectory.svg and diagnostics.json alongside the untouched raw JSONL.
Coordinates are an equal-scale world X/Z projection. The grey line is a spline
reference, not a surveyed track boundary. Discontinuities and large sample gaps
break the line. Contact-enter counts are not unique collision episodes.
"""
import collections
import html
import json
import math
from pathlib import Path
import statistics
import sys


def read_lines(path):
    return [json.loads(row) for row in path.read_text(encoding="utf-8").splitlines() if row.strip()]


def render(directory):
    samples = read_lines(directory / "trajectory.jsonl")
    events = read_lines(directory / "events.jsonl")
    line = json.loads((directory / "racing-line.json").read_text(encoding="utf-8"))["points"]
    if not samples:
        raise ValueError("No real trajectory samples were recorded.")
    summary_path = directory / "summary.json"
    summary = json.loads(summary_path.read_text(encoding="utf-8")) if summary_path.exists() else {"reason": "incomplete"}
    comparisons = [json.loads(event["detail"]) for event in events if event["kind"] == "model-60-ticks"]
    free = [row for row in comparisons if not row["Collision"]]
    diagnostics = {
        "run_summary": summary,
        "event_counts": dict(collections.Counter(event["kind"] for event in events)),
        "samples": len(samples),
        "sample_ids_contiguous": all(row["sample"] == index for index, row in enumerate(samples)),
        "clock_monotonic": all(b["clock"] >= a["clock"] for a, b in zip(samples, samples[1:])),
        "recorded_tick_gap_counts": dict(collections.Counter(row["gapTicks"] for row in samples[1:])),
        "max_abs_lateral_m": max(abs(row["lateral"]) for row in samples),
        "mean_abs_lateral_m": statistics.mean(abs(row["lateral"]) for row in samples),
        "sampled_latest_plan_ms_mean": statistics.mean(row["planMs"] for row in samples),
        "sampled_latest_plan_ms_max": max(row["planMs"] for row in samples),
        "uninterrupted_collision_free_model_windows": len(free),
        "model_windows_with_side_contact": len(comparisons) - len(free),
        "model_windows_within_declared_tolerance": sum(row["PositionError"] <= .5 and row["VelocityError"] <= .5 and row["YawError"] <= 3 for row in free),
        "model_max_errors": {key: max((row[key] for row in free), default=None) for key in ("PositionError", "VelocityError", "YawError")},
        "checkpoint_events": [event for event in events if event["kind"] == "checkpoint"],
        "notes": ["Latest-plan cost is sampled; this is not a profiler benchmark.",
                  "A1 observes one lap; product Match completion remains separate.",
                  "Model contact flags exclude wall-contact windows; flat supporting friction is modeled."]
    }
    (directory / "diagnostics.json").write_text(json.dumps(diagnostics, indent=2, ensure_ascii=False), encoding="utf-8")

    positions = line + [row["position"] for row in samples]
    x0, x1 = min(p["x"] for p in positions), max(p["x"] for p in positions)
    z0, z1 = min(p["z"] for p in positions), max(p["z"] for p in positions)
    width, height, pad = 1200, 1000, 60
    scale = min((width - 2 * pad) / max(1, x1 - x0), (height - 200) / max(1, z1 - z0))

    def xy(point):
        return pad + (point["x"] - x0) * scale, height - 100 - (point["z"] - z0) * scale

    def points(rows):
        return " ".join("%.2f,%.2f" % xy(row) for row in rows)

    svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">',
           '<rect width="100%" height="100%" fill="white"/>',
           '<g font-family="sans-serif" fill="#172334">',
           f'<text x="40" y="32" font-size="22">A1 real trajectory — {html.escape(directory.name)}</text>',
           f'<text x="40" y="58" font-size="15">{html.escape(str(summary.get("reason")))}; lap: {summary.get("lapSeconds", "not completed")} s</text>',
           '<text x="40" y="81" font-size="14">Grey: spline reference · Blue: actual motion · Red: side contact enter · X/Z, equal scale</text>',
           f'<polyline points="{points(line + [line[0]])}" fill="none" stroke="#bdc4ce" stroke-width="5"/>']
    gaps = [row["gapTicks"] for row in samples[1:] if row["gapTicks"] > 0]
    regular_gap = statistics.median(gaps) if gaps else 1
    segment = []
    for row in samples:
        if row["discontinuity"] or row["gapTicks"] > 2 * regular_gap:
            if segment:
                svg.append(f'<polyline points="{points(segment)}" fill="none" stroke="#0070b8" stroke-width="2"/>')
            segment = []
        segment.append(row["position"])
    if segment:
        svg.append(f'<polyline points="{points(segment)}" fill="none" stroke="#0070b8" stroke-width="2"/>')
    for event in events:
        if event["kind"] not in ("collision", "stalled", "position-discontinuity"):
            continue
        x, y = xy(event["position"])
        colour = "#da3737" if event["kind"] == "collision" else "#943dcc"
        label = html.escape(f'{event["kind"]} at Match Clock {event["clock"]:.3f}: {event["detail"]}')
        svg.append(f'<circle cx="{x:.2f}" cy="{y:.2f}" r="4" fill="{colour}"><title>{label}</title></circle>')
    for row, label in ((samples[0], "Start"), (samples[-1], "Last sample")):
        x, y = xy(row["position"])
        svg.append(f'<circle cx="{x:.2f}" cy="{y:.2f}" r="6" fill="none" stroke="#153c28" stroke-width="2"/>')
        svg.append(f'<text x="{x + 8:.2f}" y="{y - 8:.2f}" font-size="13">{label}</text>')
    bar = min(200, max(10, math.floor((x1 - x0) / 500) * 100))
    svg.extend([f'<path d="M 40 957 h {bar * scale:.2f}" stroke="#172334" stroke-width="3"/>',
                f'<text x="40" y="981" font-size="13">{bar} m · Raw source: trajectory.jsonl + events.jsonl + racing-line.json</text>', '</g></svg>'])
    (directory / "trajectory.svg").write_text("\n".join(svg), encoding="utf-8")
    print(json.dumps({"lap_seconds": summary.get("lapSeconds"), "events": diagnostics["event_counts"],
                      "model_windows": len(free), "model_pass": diagnostics["model_windows_within_declared_tolerance"]}, indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    render(Path(sys.argv[1]).resolve())
