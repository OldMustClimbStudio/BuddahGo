# Local two-Editor R8 probe

These are archived validation sources, not production scripts. Use only a disposable
short-path worktree and its official ParrelSync clone. Do not open or modify the user's
other Unity projects. Do not run this alongside runtime trace capture, video encoding,
other Unity tests/builds, or unrelated heavy task work.

Copy `R8SceneCapture.cs.txt` to `Assets/Scripts/Testing/R8SceneCapture.cs` and
`ReviewR8Driver.cs.txt` to `Assets/Editor/ReviewValidation/ReviewR8Driver.cs`.
Allow Unity to generate metadata only in the disposable workspace. Keep identical
probe/driver bytes and Unity settings for every source cell; switch source revisions
only after both task Editors have exited. Never overwrite completed results.
The measured Windows source files used CRLF; Git stores the archived text with LF.
The evidence summary records both raw measurement hashes and normalized-LF hashes.

Example arguments (repeat with roles host/client and run numbers 1–3):

```text
-projectPath <disposable-worktree>
-executeMethod ReviewR8Driver.Launch
-r8Capture -r8Role host -r8Output <absolute-output-directory>
-r8Label log-only -r8Revision f1ee5ab929d7b78a6611148b53a6876474d6eb4f
-r8Run 1 -r8Warmup 30 -r8Seconds 60 -logFile <absolute-host-log>
```

Launch the host first, wait for `[R8 driver] connection initiated role=host`, then launch
the clone with `-r8Role client`. On first setup only, `-r8Clone true` asks installed
ParrelSync to create its supported clone. The driver uses localhost Tugboat port 17883,
disables simulated latency, and follows the normal session/property-selection flow.
It waits for real gameplay unlock before warming up, keeps both endpoints connected
until both metadata files exist, and exits only the two processes it was launched in.

Use the same 1920×1080 GameView and quality level 2 in each cell. The probe records
actual dimensions and refuses missing counters/scene changes/early quits; the analysis
checks dimensions, duration, frame counts and source IDs. The Editor may exceed the
requested 60 fps. Samples have no file I/O or detailed gameplay trace inside their
capture window. These measurements include Editor overhead and cannot be called
Development-player or Release performance results.

`python summarize-r8-editor.py <result-directory>` calculates every run and the median
and range of each group's per-run statistics. The numeric metrics reproduce from the
archived CSVs. Approximate UTC windows originally used metadata-file mtime; preserve
original timestamps or use the published analysis window fields after extraction.
