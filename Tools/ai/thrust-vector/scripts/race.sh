#!/bin/bash
# usage: runrace.sh <name> "<profile0;profile1;...>" [extra player args]   (profile0 = harness racer, 1..5 = server AI by RacerId; 3-lap race, 5 AI)
NAME="$1"; PROFILES="$2"; shift 2; EXTRA="$@"
ROOT="C:/Users/dwh88/Documents/Codex/2026-10-02/claude-thrust"; EXE="$ROOT/build/BuddahGoThrust.exe"; OUT="$ROOT/runs/$NAME"
REPO="C:/Users/dwh88/UnityProject/BuddahGo/.worktree/single-player-mode/Tools/ai/thrust-vector/rollout-2026-10-02/player/$NAME"
if [ -e "$OUT" ]; then echo "exists: $OUT"; exit 1; fi
mkdir -p "$ROOT/runs" "$REPO"
"$EXE" -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile "$ROOT/runs/$NAME.player.log" --ai-a2-output "$OUT" --ai-count 5 --ai-profiles "$PROFILES" $EXTRA &
PID=$!; start=$(date +%s)
while kill -0 $PID 2>/dev/null; do
  if [ -f "$OUT/summary.json" ]; then sleep 8; kill $PID 2>/dev/null; break; fi
  if [ $(( $(date +%s) - start )) -gt 1200 ]; then echo timeout; kill $PID 2>/dev/null; break; fi
  sleep 3
done
sleep 2
cp "$OUT/summary.json" "$OUT/race-results.json" "$OUT/configuration.json" "$OUT/events.jsonl" "$REPO/" 2>/dev/null; [ -f "$OUT/perf.csv" ] && cp "$OUT/perf.csv" "$REPO/"
echo "=== $NAME"; python - "$OUT/race-results.json" <<'PY'
import json,sys
import io
d=json.load(io.open(sys.argv[1],encoding="utf-8"))
print(f"{'rank':4} {'racer':10} {'profile':30} {'laps':>4} {'total':>8} {'best':>7} {'avg':>7} {'contacts':>8} {'meanV':>6} {'maxV':>6} fin")
for i,r in enumerate(d['racers'],1):
    laps=r.get('laps') or []
    print(f"{i:4} {r['racerId']:<10} {r['profile']:30} {len(laps):4} {r['totalSeconds']:8.2f} {min(laps) if laps else 0:7.2f} {sum(laps)/len(laps) if laps else 0:7.2f} {r['collisions']:8} {r['speedSum']/max(1,r['samples']):6.1f} {r['maxSpeed']:6.1f} {r['finished']}")
PY
grep -E '"perfMedianMs"|"perfP95Ms"|"perfTicks"|"runtimeErrors"' "$OUT/summary.json"
