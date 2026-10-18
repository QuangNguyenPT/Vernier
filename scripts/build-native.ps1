<#
.SYNOPSIS
  Builds native\Vernier.Native into src\Vernier.App\native\Vernier.Native.dll with the installed Visual Studio C++ tools.
.DESCRIPTION
  Vernier runs without this DLL (every native feature has a managed fallback), so a machine without C++ tools can still
  build and run the app. The DLL is checked in nowhere; build it with this script, or let publish.ps1 do it.
#>
param([switch]$Debug)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'native\Vernier.Native'
$out = Join-Path $root 'src\Vernier.App\native'
$obj = Join-Path $root 'native\obj'
New-Item -ItemType Directory -Force $out, $obj | Out-Null

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) { throw 'Visual Studio is not installed (vswhere.exe not found). Vernier still runs without the native module.' }
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'Visual Studio has no C++ x64 tools. Vernier still runs without the native module.' }
$vcvars = Join-Path $vs 'VC\Auxiliary\Build\vcvars64.bat'

$opt = if ($Debug) { '/Od /Zi' } else { '/O2 /GL' }
$files = (Get-ChildItem $src -Filter *.cpp | ForEach-Object { '"' + $_.FullName + '"' }) -join ' '
$cmd = "`"$vcvars`" >nul && cl /nologo /std:c++20 /EHsc /W4 /WX /permissive- /utf-8 $opt /DUNICODE /D_UNICODE /LD $files " +
       "/Fo`"$obj\\`" /Fe`"$out\Vernier.Native.dll`" /link /NOLOGO d3d11.lib dxgi.lib d2d1.lib dcomp.lib dwmapi.lib windowscodecs.lib ole32.lib user32.lib winmm.lib"
cmd /c $cmd
if ($LASTEXITCODE -ne 0) { throw "Native build failed ($LASTEXITCODE)." }
Remove-Item (Join-Path $out '*.exp'), (Join-Path $out '*.lib') -ErrorAction SilentlyContinue
Write-Host "Built $out\Vernier.Native.dll"
