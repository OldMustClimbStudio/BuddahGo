# Runs in the foreground; stop with Ctrl+C. Unity connects via HTTP Local.
$ErrorActionPreference = 'Stop'
$uvx = Get-Command uvx -ErrorAction Stop
& $uvx.Source --from 'mcpforunityserver==10.0.0' mcp-for-unity --transport http --http-url 'http://127.0.0.1:8080' --project-scoped-tools
exit $LASTEXITCODE
