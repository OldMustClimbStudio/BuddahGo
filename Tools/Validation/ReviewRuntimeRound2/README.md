# Opt-in runtime review fixture

Archived observation tools, not production gameplay sources. Use only a disposable short-path
worktree of the published P6 head and its official ParrelSync clone. Both endpoints must use
identical Assets. Keep the user's Unity processes/scenes and original worktrees untouched.

Apply `observation-probes.patch` with `git apply --check` first. Copy the three `.cs.txt`
files and `.asmdef.txt` into `Assets/ReviewValidation/Editor` with the trailing `.txt`
removed, and let Unity generate their metadata in the disposable workspace. The patch
adds observation hooks to actual gameplay paths; it changes no RPC signature or gameplay
state. Do not include it in a shipping build or R8 performance measurements.

Launch Unity 2022.3.55f1c1 using `-projectPath <short-worktree> -executeMethod
ReviewRuntimeDriver.Launch -reviewRole host -reviewLatency 100 -reviewOutput <absolute-output>
-reviewMode matrix -reviewDuration 210 -reviewVersion <source-revision> -logFile <host-log>`.
For first setup, `-reviewClone true` creates the supported ParrelSync clone. Wait for the
host JSON status to confirm `server:true`, then launch the clone with the same arguments
and `-reviewRole client`. Results are written separately; output directories must be new.
Do not mix versions within a session. `push` mode runs two static contact fixtures;
`visual` mode runs the two high-speed camera cases. `-reviewCycle true` uses real existing
EndMatch/result choices for restart/room return. These latter enhanced camera/cycle paths
are still being validated at the staged publication and are not claimed passed.

Latency is set before connection. First-second correction uses GO UTC, not a drifting
tick window. `summarize-runtime-stream.py <run-directory>` generates a completed-run
summary; `build-runtime-evidence.py <results-root>` extracts source hashes, selected raw
records, offset changes and consumption identities. Legitimate rollback in another pass
is not a duplicate. Large full JSONL recordings remain in the local task evidence folder.
