# R8 local Editor scene measurements — 2026-09-30

All 9 fresh host/client pairs completed: 3 repetitions for each of pre-P2, P2 with screen diagnostics, and P2 with this round’s log-only diagnostics. The same source is used on both peers in each pair. These are real RaceMap scene samples, not a Player/Release benchmark or cross-machine Steam test.

## Conditions and limits

Unity 2022.3.55f1c1, local Tugboat host + pure client, 0 ms simulator, 1920×1080, quality 2, 60 Hz network ticks, VSync 0. The driver traverses MainMenu/property selection/RaceMap, selects the first authored option and the same loadout, then supplies no gameplay input. It warms up for 30 seconds **after gameplay unlock**, records 60 seconds, and writes counters only after capture. Both peers remain connected until both captures complete.

The original proposed 5×120-second 720p Development-player protocol was not executed. This smaller 3×60-second Editor dataset was used to make the available real-scene comparison reproducible within the local validation session. Sources are grouped sequentially A, B, C rather than interleaved. This leaves order, thermal and uncontrolled background-load confounding; it does not establish a general performance guarantee.

No detailed N10/R6/R9 trace, screenshots, other task Unity, or media encoding is intentionally run during these windows. A later coordination check found media encode/decode output ended at 18:04:59 UTC, verification at 18:05:02; the nearest pre-P2 run 3 window is approximately 18:06:24–18:07:24. These available timestamps do not overlap. Windows are reconstructed from metadata mtime immediately after counter stop minus measured duration, not from a UTC clock inside the probe.

`targetFps=60` is a request, not an enforced Editor lock: actual frame counts vary. GC is reported both per observed frame and per measured second. Main Thread includes waits and Editor work; it is not isolated motor CPU time. No memory-retention or Player/Release benefit is inferred. The aggregate is the median of three independent per-run statistics, with ranges retained in the JSON; host and client are never pooled.

## Source cells

| Cell | Source | Meaning |
| --- | --- | --- |
| A pre-p2 | `707376f3bf0e33ad8586eba8034a9fe33caf68c8` | #49, before P2 |
| B screen-on | `6129b59a3e4a7076963937d5b2b34eb7b0ecf5af` | #50 before this round |
| C log-only | `f1ee5ab929d7b78a6611148b53a6876474d6eb4f` | #50 with this round’s diagnostic sampling |

A→B describes existing P2 changes; B→C describes this round’s diagnostic increment. A→C combines both. No final-stack P6 D/E pair was measured, so these results must not be used to attribute P3–P6 effects.

## Medians of three runs

| Cell / role | GC B/frame | GC B/s | Main Thread mean ms | Main Thread p95 ms |
| --- | ---: | ---: | ---: | ---: |
| pre-p2/host | 67099.5 | 5246892.5 | 12.774 | 15.051 |
| pre-p2/client | 78657.5 | 5778679.2 | 13.604 | 16.280 |
| screen-on/host | 65414.9 | 5321030.8 | 12.286 | 14.493 |
| screen-on/client | 73406.2 | 5752810.4 | 12.753 | 15.287 |
| log-only/host | 25204.1 | 2245164.9 | 10.981 | 12.801 |
| log-only/client | 26986.5 | 2257132.7 | 11.947 | 14.114 |

Allocation differences below compare medians of per-run metrics; negative means less allocation in the later cell. They describe this Editor dataset only. Frame-time results vary by role and repeat, so reduced allocation is not presented as a guaranteed frame-rate improvement.

| Comparison / role | GC B/frame difference | GC B/s difference |
| --- | ---: | ---: |
| A→B/host | -2.51% | 1.41% |
| A→B/client | -6.68% | -0.45% |
| B→C/host | -61.47% | -57.81% |
| B→C/client | -63.24% | -60.76% |
| A→C/host | -62.44% | -57.21% |
| A→C/client | -65.69% | -60.94% |

## Every run

| Cell / role / run | Frames | Seconds | Actual frames/s | GC B/frame | GC B/s | Main mean / p95 ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| pre-p2/host/1 | 4302 | 60.009809 | 71.69 | 67641.3 | 4849086.0 | 13.941 / 16.923 |
| pre-p2/host/2 | 4695 | 60.007844 | 78.24 | 67061.7 | 5246892.5 | 12.774 / 14.877 |
| pre-p2/host/3 | 4801 | 60.008236 | 80.01 | 67099.5 | 5368343.4 | 12.492 / 15.051 |
| pre-p2/client/1 | 4526 | 60.010345 | 75.42 | 80676.9 | 6084676.3 | 13.252 / 16.252 |
| pre-p2/client/2 | 4300 | 60.009025 | 71.66 | 74114.9 | 5310771.3 | 13.948 / 16.384 |
| pre-p2/client/3 | 4409 | 60.013856 | 73.47 | 78657.5 | 5778679.2 | 13.604 / 16.280 |
| screen-on/host/1 | 4881 | 60.005334 | 81.34 | 65414.9 | 5321030.8 | 12.286 / 14.493 |
| screen-on/host/2 | 4555 | 60.011295 | 75.90 | 65679.3 | 4985212.2 | 13.167 / 15.396 |
| screen-on/host/3 | 4949 | 60.008096 | 82.47 | 65276.4 | 5383486.2 | 12.118 / 14.303 |
| screen-on/client/1 | 4469 | 60.010048 | 74.47 | 73285.0 | 5457598.3 | 13.420 / 15.883 |
| screen-on/client/2 | 4730 | 60.007611 | 78.82 | 74765.7 | 5893280.0 | 12.679 / 15.029 |
| screen-on/client/3 | 4703 | 60.010573 | 78.37 | 73406.2 | 5752810.4 | 12.753 / 15.287 |
| log-only/host/1 | 3850 | 60.000300 | 64.17 | 26138.1 | 1677186.6 | 15.577 / 21.872 |
| log-only/host/2 | 5461 | 60.007524 | 91.01 | 24670.7 | 2245164.9 | 10.981 / 12.801 |
| log-only/host/3 | 5507 | 60.010246 | 91.77 | 25204.1 | 2312920.4 | 10.890 / 12.624 |
| log-only/client/1 | 5085 | 60.011552 | 84.73 | 26638.0 | 2257132.7 | 11.794 / 13.907 |
| log-only/client/2 | 4920 | 60.004233 | 81.99 | 26986.5 | 2212739.2 | 12.188 / 14.277 |
| log-only/client/3 | 5020 | 60.012954 | 83.65 | 27075.1 | 2264792.1 | 11.947 / 14.114 |

## Evidence and reproduction

[All per-run metrics, ranges, source hashes and approximate windows](evidence/review-round2/r8-editor-summary.json); [numeric CSVs and metadata](evidence/review-round2/r8-editor-csv.zip). The archive contains measurement data only, no video, desktop content, full Editor logs or credentials.

All 18 Editor logs contain a completed-capture marker and have no matches for compiler
errors or the scanned common managed-exception patterns; this does not mean warning-free
logs. [Log audit](evidence/review-round2/r8-log-audit.json) records the check per endpoint.

The exact probe and driver are archived in [ReviewR8Editor](../../Tools/Performance/ReviewR8Editor/README.md). Both are opt-in validation files; neither is included in production gameplay. Re-extract CSVs/metadata and run the archived `summarize-r8-editor.py <directory>` to reproduce numeric metrics. File-time window estimates should use the included analysis metadata if extraction changes mtimes.
