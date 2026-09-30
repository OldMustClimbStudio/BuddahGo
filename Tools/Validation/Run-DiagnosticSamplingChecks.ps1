param(
    [Parameter(Mandatory=$true)][string]$NUnitAssembly,
    [string]$UnityEditorData = 'C:\Program Files\Unity\Hub\Editor\2022.3.55f1c1\Editor\Data',
    [string]$OutputDirectory = (Join-Path $env:TEMP 'BuddahGo-DiagnosticSampling')
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$dotnet = (Get-Command dotnet).Source
$dotnetRoot = Split-Path $dotnet
$sdk = Get-ChildItem (Join-Path $dotnetRoot 'sdk') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$monoRoot = Join-Path $UnityEditorData 'MonoBleedingEdge'
$references = @('mscorlib.dll', 'System.dll', 'System.Core.dll') | ForEach-Object { Get-Item (Join-Path $monoRoot "lib/mono/4.5/$_") }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory = (Resolve-Path $OutputDirectory).Path
$runner = @'
using System;
using System.Reflection;
using NewBuddah.PredictionV2.Debugging;
class Runner
{
    static int Main()
    {
        int passed = 0;
        var suite = new BuddahGo.Tests.PredictionDiagnosticSamplingTests();
        foreach (var method in suite.GetType().GetMethods())
        {
            if (!Attribute.IsDefined(method, typeof(NUnit.Framework.TestAttribute))) continue;
            try { method.Invoke(suite, null); passed++; Console.WriteLine("PASS " + method.Name); }
            catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        }
        var sample = new BuddahPredictionLogSnapshot();
        for (uint i = 0; i < 100; i++) { sample.CaptureReplicate(i, 0, 0, "active"); sample.CaptureReconcile(i, 1, 0, 0, 0, 0); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (uint i = 0; i < 100000; i++) { sample.CaptureReplicate(i, 0, 0, "active"); sample.CaptureReconcile(i, 1, 0, 0, 0, 0); }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine("passed=" + passed + " capture_pairs=100000 allocated_bytes=" + allocated);
        // Supplemental pure-method allocation check, NOT a Unity scene/R8 measurement.
        return allocated == 0 ? 0 : 1;
    }
}
'@
Set-Content -LiteralPath (Join-Path $OutputDirectory 'Runner.cs') -Value $runner -Encoding utf8
Copy-Item -LiteralPath $NUnitAssembly -Destination (Join-Path $OutputDirectory 'nunit.framework.dll') -Force
foreach ($mode in @('UNITY_EDITOR', 'DEVELOPMENT_BUILD', 'RELEASE')) {
    $assembly = Join-Path $OutputDirectory "$mode.exe"
    $arguments = @('-nostdlib+', '-target:exe', '-optimize+', '-nologo', "-define:$mode", "-out:`"$assembly`"")
    $arguments += $references | ForEach-Object { "-r:`"$($_.FullName)`"" }
    $arguments += "-r:`"$NUnitAssembly`""
    $arguments += 'Assets/Scripts/New_Buddah/Debug/BuddahDiagnosticLogSampling.cs', 'Assets/Scripts/New_Buddah/Debug/BuddahPredictionLogSnapshot.cs', 'Assets/Tests/EditMode/PredictionDiagnosticSamplingTests.cs' | ForEach-Object { '"' + (Join-Path $projectRoot $_) + '"' }
    $arguments += '"' + (Join-Path $OutputDirectory 'Runner.cs') + '"'
    $response = Join-Path $OutputDirectory "$mode.rsp"
    Set-Content -LiteralPath $response -Value $arguments -Encoding utf8
    & $dotnet (Join-Path $sdk.FullName 'Roslyn/bincore/csc.dll') "@$response"
    if ($LASTEXITCODE -ne 0) { throw "Compile failed: $mode" }
    & (Join-Path $monoRoot 'bin/mono.exe') $assembly | Tee-Object -FilePath (Join-Path $OutputDirectory "$mode-results.txt")
    if ($LASTEXITCODE -ne 0) { throw "Checks failed: $mode" }
}
