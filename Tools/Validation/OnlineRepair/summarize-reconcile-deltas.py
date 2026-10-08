#!/usr/bin/env python3
"""Summarize the §0 probes ([ReconcileDelta], [SpectatorStep]) from Unity Player.log / Editor.log.

Usage:
    python summarize-reconcile-deltas.py <log> [<log> ...] [--go-window-ticks 60] [--json out.json]

Groups [ReconcileDelta] lines by owner / non-owner and prints mean, p95, max of posDelta and
velDelta, the share of lines that were skipped, and the first-second-after-handoff-consume
correction stats. Prints the share of negative [SpectatorStep] forwardStep values.
Reads the whole file; no Unity dependency. See Docs/online-repair/experiments.md.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass, field
from typing import Dict, List, Optional

KV_RE = re.compile(r"(\w+)=([^\s]+)")
RECONCILE_TAG = "[ReconcileDelta]"
SPECTATOR_TAG = "[SpectatorStep]"
CONSUME_TAG = "[HandoffDebug] consuming queued handoff"
STALE_TAG = "[HandoffDebug] stale handoff adjusted"


def parse_kv(line: str) -> Dict[str, str]:
    return {k: v for k, v in KV_RE.findall(line)}


def to_float(value: Optional[str], default: float = 0.0) -> float:
    if value is None:
        return default
    try:
        return float(value)
    except ValueError:
        return default


def to_bool(value: Optional[str]) -> bool:
    return (value or "").lower() == "true"


def percentile(values: List[float], pct: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    index = int(round((len(ordered) - 1) * pct))
    return ordered[max(0, min(len(ordered) - 1, index))]


@dataclass
class Group:
    pos: List[float] = field(default_factory=list)
    vel: List[float] = field(default_factory=list)
    speed: List[float] = field(default_factory=list)
    skipped: int = 0
    total: int = 0
    stale_server_ids: int = 0

    def summary(self) -> Dict[str, float]:
        return {
            "count": self.total,
            "skipped": self.skipped,
            "staleServerId": self.stale_server_ids,
            "posMean": sum(self.pos) / len(self.pos) if self.pos else 0.0,
            "posP95": percentile(self.pos, 0.95),
            "posMax": max(self.pos) if self.pos else 0.0,
            "velMean": sum(self.vel) / len(self.vel) if self.vel else 0.0,
            "velP95": percentile(self.vel, 0.95),
            "velMax": max(self.vel) if self.vel else 0.0,
            "speedMean": sum(self.speed) / len(self.speed) if self.speed else 0.0,
            "speedMax": max(self.speed) if self.speed else 0.0,
        }


@dataclass
class HandoffWindow:
    consume_tick: int
    stale_ticks: Optional[int]
    corrections: List[float] = field(default_factory=list)
    first_large_tick: Optional[int] = None


def analyze(lines: List[str], go_window_ticks: int, large_threshold: float) -> Dict:
    groups = {"owner": Group(), "spectator": Group(), "server": Group()}
    spectator_steps: List[float] = []
    windows: List[HandoffWindow] = []
    pending_stale: Optional[int] = None

    for line in lines:
        if STALE_TAG in line:
            kv = parse_kv(line)
            pending_stale = int(to_float(kv.get("staleTicks")))
            continue
        if CONSUME_TAG in line:
            kv = parse_kv(line)
            windows.append(HandoffWindow(int(to_float(kv.get("currentTick"))), pending_stale))
            pending_stale = None
            continue
        if RECONCILE_TAG in line:
            kv = parse_kv(line)
            is_server = to_bool(kv.get("server"))
            is_owner = to_bool(kv.get("owner"))
            key = "server" if is_server else ("owner" if is_owner else "spectator")
            group = groups[key]
            group.total += 1
            if to_bool(kv.get("skipped")):
                group.skipped += 1
            pos = to_float(kv.get("posDelta"))
            vel = to_float(kv.get("velDelta"))
            group.pos.append(pos)
            group.vel.append(vel)
            group.speed.append(to_float(kv.get("speed")))
            srv_id = to_float(kv.get("srvHandoffId"), -1)
            local_id = to_float(kv.get("localHandoffId"), -1)
            if srv_id >= 0 and local_id >= 0 and srv_id < local_id:
                group.stale_server_ids += 1
            local_tick = int(to_float(kv.get("local")))
            if windows and is_owner and not is_server:
                window = windows[-1]
                if window.consume_tick <= local_tick <= window.consume_tick + go_window_ticks:
                    window.corrections.append(pos)
                    if pos > large_threshold and window.first_large_tick is None:
                        window.first_large_tick = local_tick
            continue
        if SPECTATOR_TAG in line:
            kv = parse_kv(line)
            spectator_steps.append(to_float(kv.get("forwardStep")))

    negative_steps = sum(1 for s in spectator_steps if s < -1e-4)
    result = {
        "reconcile": {k: g.summary() for k, g in groups.items()},
        "spectatorStep": {
            "count": len(spectator_steps),
            "negativeShare": (negative_steps / len(spectator_steps)) if spectator_steps else 0.0,
            "minStep": min(spectator_steps) if spectator_steps else 0.0,
        },
        "handoffWindows": [
            {
                "consumeTick": w.consume_tick,
                "staleTicks": w.stale_ticks,
                "corrections": len(w.corrections),
                "maxCorrection": max(w.corrections) if w.corrections else 0.0,
                "firstLargeTickOffset": (w.first_large_tick - w.consume_tick) if w.first_large_tick is not None else None,
            }
            for w in windows
        ],
    }
    return result


def print_report(result: Dict) -> None:
    print("reconcile corrections (u):")
    print(f"  {'group':<10}{'n':>6}{'skip':>6}{'stale':>6}{'posMean':>9}{'posP95':>9}{'posMax':>9}{'velMean':>9}{'velMax':>9}{'speedMax':>10}")
    for key, s in result["reconcile"].items():
        print(
            f"  {key:<10}{s['count']:>6}{s['skipped']:>6}{s['staleServerId']:>6}"
            f"{s['posMean']:>9.3f}{s['posP95']:>9.3f}{s['posMax']:>9.3f}"
            f"{s['velMean']:>9.3f}{s['velMax']:>9.3f}{s['speedMax']:>10.2f}"
        )
    spec = result["spectatorStep"]
    print(f"spectator steps: n={spec['count']} negativeShare={spec['negativeShare']:.4f} minStep={spec['minStep']:.3f}")
    if result["handoffWindows"]:
        print("handoff windows (owner, first N ticks after consume):")
        for w in result["handoffWindows"]:
            print(
                f"  consumeTick={w['consumeTick']} staleTicks={w['staleTicks']} corrections={w['corrections']} "
                f"maxCorrection={w['maxCorrection']:.3f} firstLargeTickOffset={w['firstLargeTickOffset']}"
            )
    else:
        print("handoff windows: none found")


def main(argv: List[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("logs", nargs="+")
    parser.add_argument("--go-window-ticks", type=int, default=60)
    parser.add_argument("--large-threshold", type=float, default=0.5)
    parser.add_argument("--json", dest="json_out")
    args = parser.parse_args(argv)

    lines: List[str] = []
    for path in args.logs:
        with open(path, "r", encoding="utf-8", errors="replace") as handle:
            lines.extend(handle.readlines())

    result = analyze(lines, args.go_window_ticks, args.large_threshold)
    print_report(result)
    if args.json_out:
        with open(args.json_out, "w", encoding="utf-8") as handle:
            json.dump(result, handle, indent=2)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
