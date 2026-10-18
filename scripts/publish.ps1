<#
.SYNOPSIS
  Builds Vernier for 64-bit Windows as one self-contained executable plus the C++ module, and smoke-tests it.
.DESCRIPTION
  Output: artifacts\publish\Vernier-win-x64\Vernier.exe and Vernier.Native.dll beside it, nothing else (no .NET install
  needed on the target machine). The .NET runtime's own native parts are inside Vernier.exe and are unpacked by .NET to a
  per-user cache on first start. Not compressed: compression would slow every start.
  The build is NOT code signed and there is no installer or auto-update; copy the two files to use it.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\publish.ps1
#>
param(
    [switch]$SkipTests,
    [switch]$SkipSmoke
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'artifacts\publish\Vernier-win-x64'

# The native module (DXGI capture, edge light) is optional: without Visual Studio's C++ tools the build still works and uses
# the managed fallbacks. When it builds, it is shipped, and the smoke test checks that it loads.
$native = Join-Path $root 'scripts\build-native.ps1'
if (Test-Path $native) {
    try { & powershell -NoProfile -ExecutionPolicy Bypass -File $native; if ($LASTEXITCODE -ne 0) { throw "exit $LASTEXITCODE" } }
    catch { Write-Warning "Native module not built ($_). The published app will use the managed fallbacks." }
}

if (-not $SkipTests) {
    dotnet test (Join-Path $root 'tests\Vernier.Tests') -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed; not publishing.' }
}

if (Test-Path $out) { Remove-Item -Recurse -Force $out }
dotnet publish (Join-Path $root 'src\Vernier.App') -c Release -r win-x64 --self-contained true -o $out --nologo `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

$exe = Join-Path $out 'Vernier.exe'
if (-not (Test-Path $exe)) { throw "Publish did not produce $exe" }
$dll = Join-Path $out 'Vernier.Native.dll' # the csproj links it next to Vernier.exe, where LibraryImport finds it
if (Test-Path (Join-Path $root 'src\Vernier.App\native\Vernier.Native.dll')) {
    if (-not (Test-Path $dll)) { throw 'The native module was built but is not in the published folder.' }
    Write-Host 'Native module is in the published folder.'
} else { Write-Warning 'Published without the native module (managed fallbacks only).' }
$extra = Get-ChildItem $out -Recurse -File | Where-Object { $_.Name -notin @('Vernier.exe', 'Vernier.Native.dll') }
if ($extra) { throw "The published folder must hold only Vernier.exe and Vernier.Native.dll; also found: $($extra.Name -join ', ')" }

if (-not $SkipSmoke) {
    # Starts the real published app against a throwaway data folder: it opens the main window,
    # builds every page, and exits 0. Needs an interactive desktop session.
    $data = Join-Path ([IO.Path]::GetTempPath()) ("vernier-smoke-" + [Guid]::NewGuid().ToString('N'))
    $p = Start-Process -FilePath $exe -ArgumentList '--smoke', '--data', "`"$data`"" -PassThru
    if (-not $p.WaitForExit(60000)) { $p.Kill(); throw 'Smoke test timed out after 60 s.' }
    $code = $p.ExitCode
    $log = Join-Path $data 'logs\vernier.log'
    if (Test-Path $log) { Get-Content $log | Select-Object -Last 3 }
    Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue
    if ($code -ne 0) { throw "Smoke test failed with exit code $code." }
    Write-Host 'Smoke test passed: the published app started, built every page and exited cleanly.'
}

$size = [math]::Round((Get-ChildItem $out -Recurse | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host "Published to $out ($size MB)."
