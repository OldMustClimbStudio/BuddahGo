#!/usr/bin/env python3
"""Summarize [HandoffFrame] probe lines (fix-spec §H.0) around the launch handoff.

Usage:
    python summarize-handoff-frames.py <log-or-console-dump> [--all]

Input is any text file containing the [HandoffFrame] lines emitted by
BuddahPredictionVisualRootBridge (one per frame, owner only). Output: per-frame
forward displacement of the VisualRoot and the physics root (projected on the
direction of travel), counts of zero / negative frames, the GO -> consume tick gap,
and the largest single-frame camera FOV / offset step.
"""
import re
import sys
from dataclasses import dataclass

LINE_RE = re.compile(r"\[HandoffFrame\]\s+(.*)")
KV_RE = re.compile(r"(\w+)=(\([^)]*\)|\S+)")
VEC_RE = re.compile(r"\(\s*([-\d.]+),\s*([-\d.]+),\s*([-\d.]+)\s*\)")


def parse_vec(text):
    m = VEC_RE.match(text)
    return tuple(float(v) for v in m.groups()) if m else (0.0, 0.0, 0.0)


@dataclass
class Frame:
    f: int
    t: float
    tick: int
    phase: str
    go: bool
    pending: bool
    consumed: bool
    kin: bool
    root: tuple
    vis: tuple
    vel: tuple
    speed: float
    allowed: bool
    stab: str
    suppressed: bool
    fov: float
    cam_off: tuple
    cam_dist: float


def parse(path):
    frames = []
    with open(path, encoding="utf-8", errors="replace") as fh:
        for raw in fh:
            m = LINE_RE.search(raw)
            if not m:
                continue
            kv = dict(KV_RE.findall(m.group(1)))
            frames.append(Frame(
                f=int(kv["f"]), t=float(kv["t"]), tick=int(kv["tick"]), phase=kv["phase"],
                go=kv["go"] == "True", pending=kv["pending"] == "True", consumed=kv["consumed"] == "True",
                kin=kv["kin"] == "True", root=parse_vec(kv["root"]), vis=parse_vec(kv["vis"]),
                vel=parse_vec(kv["rbVel"]), speed=float(kv["speed"]), allowed=kv["allowed"] == "True",
                stab=kv["stab"], suppressed=kv["smootherSuppressed"] == "True", fov=float(kv["fov"]),
                cam_off=parse_vec(kv["camOff"]), cam_dist=float(kv["camDist"])))
    frames.sort(key=lambda x: x.f)
    # Drop duplicate frames (console paging can repeat lines).
    dedup = []
    for fr in frames:
        if not dedup or dedup[-1].f != fr.f:
            dedup.append(fr)
    return dedup


def planar(v):
    return (v[0], 0.0, v[2])


def norm(v):
    mag = (v[0] ** 2 + v[1] ** 2 + v[2] ** 2) ** 0.5
    return (v[0] / mag, v[1] / mag, v[2] / mag) if mag > 1e-6 else (0.0, 0.0, 0.0)


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    show_all = "--all" in sys.argv
    frames = parse(sys.argv[1])
    if len(frames) < 2:
        print("no [HandoffFrame] lines found")
        sys.exit(1)

    # Direction of travel: mean planar displacement of the visual root over the window.
    travel = norm(planar(sub(frames[-1].vis, frames[0].vis)))

    go_frame = next((fr for fr in frames if fr.go), None)
    consume_frame = next((fr for fr in frames if fr.consumed), None)
    go_tick = go_frame.tick if go_frame else None
    consume_tick = consume_frame.tick if consume_frame else None

    print(f"frames={len(frames)} window f{frames[0].f}..f{frames[-1].f} travelDir={travel[0]:.2f},{travel[2]:.2f}")
    print(f"GO first seen: frame={go_frame.f if go_frame else None} tick={go_tick}")
    print(f"consume first seen: frame={consume_frame.f if consume_frame else None} tick={consume_tick}"
          + (f" (gap={consume_tick - go_tick} ticks, {consume_frame.f - go_frame.f} frames)" if go_frame and consume_frame else ""))
    print()
    print(" frame  tick  dt(ms)  visFwd  rootFwd  speed  go pend cons kin allowed stab                      supp   fov   camDist")
    zero_vis = neg_vis = zero_root = neg_root = 0
    max_fov_step = 0.0
    max_off_step = 0.0
    prev = frames[0]
    for fr in frames[1:]:
        dt = fr.t - prev.t
        vis_fwd = dot(sub(fr.vis, prev.vis), travel)
        root_fwd = dot(sub(fr.root, prev.root), travel)
        expected = prev.speed * dt if prev.speed > 0 else 0.0
        if vis_fwd <= 1e-4:
            zero_vis += 1
        if vis_fwd < -1e-4:
            neg_vis += 1
        if root_fwd <= 1e-4:
            zero_root += 1
        if root_fwd < -1e-4:
            neg_root += 1
        max_fov_step = max(max_fov_step, abs(fr.fov - prev.fov))
        off_step = (sum((a - b) ** 2 for a, b in zip(fr.cam_off, prev.cam_off)) ** 0.5) + abs(fr.cam_dist - prev.cam_dist)
        max_off_step = max(max_off_step, off_step)
        flag = ""
        if vis_fwd <= 1e-4:
            flag = "  <-- visual stalled"
        elif expected > 0 and vis_fwd < 0.5 * expected:
            flag = "  <-- visual slow"
        near_go = go_frame and abs(fr.f - go_frame.f) <= 12
        if show_all or near_go or flag:
            print(f"{fr.f:6d} {fr.tick:5d} {dt*1000:7.1f} {vis_fwd:7.3f} {root_fwd:8.3f} {fr.speed:6.2f}  "
                  f"{int(fr.go)}   {int(fr.pending)}    {int(fr.consumed)}    {int(fr.kin)}    {int(fr.allowed)}     "
                  f"{fr.stab[:24]:24s} {int(fr.suppressed)}  {fr.fov:5.2f} {fr.cam_dist:6.2f}{flag}")
        prev = fr
    print()
    print(f"visual: zero-displacement frames={zero_vis} negative frames={neg_vis}")
    print(f"root:   zero-displacement frames={zero_root} negative frames={neg_root}")
    print(f"camera: max FOV step/frame={max_fov_step:.2f} deg, max offset step/frame={max_off_step:.2f} u")


if __name__ == "__main__":
    main()
