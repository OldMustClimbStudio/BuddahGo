#!/bin/bash
# usage: runplayer.sh <name> <profile.json> <laps:1|3> <aiCount> [timeoutSec]
NAME="$1"; PROFILE="$2"; LAPS="${3:-1}"; AI="${4:-0}"; TMO="${5:-600}"
HERE="$(cd "$(dirname "$0")" && pwd)"; PROJECT="$(cd "$HERE/../../../.." && pwd)"
# Defaults match ThrustVectorAcceptanceBuild (git-ignored Logs/); override with THRUST_ROOT / THRUST_EXE.
ROOT="${THRUST_ROOT:-$PROJECT/Logs/thrust-vector}"; EXE="${THRUST_EXE:-$PROJECT/Logs/thrust-vector-build/BuddahGoThrust.exe}"
OUT="$ROOT/runs/$NAME"
REPO="$HERE/../rollout-2026-10-02/player/$NAME"
if [ -e "$OUT" ]; then echo "exists: $OUT"; exit 1; fi
mkdir -p "$ROOT/runs" "$REPO"
FLAG="--ai-a1-output"; [ "$LAPS" = "3" ] && FLAG="--ai-a2-output"
PFLAG="--ai-a1-profile"; [ "$LAPS" = "3" ] && PFLAG="--ai-a2-profile"
"$EXE" -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile "$ROOT/runs/$NAME.player.log" $FLAG "$OUT" $PFLAG "$PROFILE" --ai-count "$AI" &
PID=$!
start=$(date +%s)
while kill -0 $PID 2>/dev/null; do
  if [ -f "$OUT/summary.json" ]; then sleep 8; kill $PID 2>/dev/null; break; fi
  if [ $(( $(date +%s) - start )) -gt "$TMO" ]; then echo "timeout"; kill $PID 2>/dev/null; break; fi
  sleep 3
done
sleep 2
cp "$OUT/summary.json" "$OUT/configuration.json" "$OUT/events.jsonl" "$REPO/" 2>/dev/null
[ -f "$OUT/perf.csv" ] && cp "$OUT/perf.csv" "$REPO/"
echo "=== $NAME"; cat "$OUT/summary.json" 2>/dev/null || echo "no summary"
grep -c '"kind":"collision"' "$OUT/events.jsonl" 2>/dev/null | sed 's/^/collisions(events)=/'
grep -o '"kind":"[a-z-]*"' "$OUT/events.jsonl" 2>/dev/null | sort | uniq -c | sort -rn | head -12
