# Build "Music Player": generate icon -> compile a single native exe with the in-box csc
# Usage: powershell -ExecutionPolicy Bypass -File build.ps1 [-DesktopCopy <full path>]
# NOTE: keep this file ASCII-only; Windows PowerShell 5.1 reads BOM-less files as ANSI.
param(
    [string]$DesktopCopy = ""
)

$ErrorActionPreference = "Stop"

$root    = "D:\dsh\music-player"
$srcDir  = Join-Path $root "src"
$toolDir = Join-Path $root "tools"
$outDir  = Join-Path $root "build"
$csc     = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpfDir  = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"
$gacMsil = "C:\Windows\Microsoft.NET\assembly\GAC_MSIL"

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Get-GacPath([string]$name) {
    $d = Get-ChildItem (Join-Path $gacMsil $name) -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $d) { throw "assembly not found in GAC: $name" }
    return (Join-Path $d.FullName "$name.dll")
}

Write-Host "[1/3] generating icon ..." -ForegroundColor Cyan
$iconExe = Join-Path $outDir "icongen.exe"
$iconIco = Join-Path $outDir "icon.ico"
$iconPng = Join-Path $outDir "appicon.png"
# System.Drawing / System.Windows.Forms are already referenced by csc.rsp (duplicating them gives CS1703)
& $csc /nologo /target:exe /optimize+ "/out:$iconExe" (Join-Path $toolDir "IconGen.cs")
if ($LASTEXITCODE -ne 0) { throw "icon generator compile failed" }
& $iconExe $iconIco $iconPng
if ($LASTEXITCODE -ne 0) { throw "icon generation failed" }

Write-Host "[2/3] compiling player ..." -ForegroundColor Cyan
$exe = Join-Path $outDir "MusicPlayer.exe"
$refs = @(
    (Join-Path $wpfDir "PresentationCore.dll"),
    (Join-Path $wpfDir "PresentationFramework.dll"),
    (Join-Path $wpfDir "WindowsBase.dll"),
    (Get-GacPath "System.Xaml")
)
$sources = Get-ChildItem $srcDir -Filter *.cs | ForEach-Object { $_.FullName }

$cscArgs = @(
    "/nologo", "/target:winexe", "/optimize+", "/codepage:65001",
    "/main:MusicPlayer.Program",
    "/out:$exe",
    "/win32icon:$iconIco",
    "/resource:$iconPng,appicon.png"
) + ($refs | ForEach-Object { "/r:$_" }) + $sources

& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "player compile failed" }

$info = Get-Item $exe
Write-Host ("[3/3] build ok: {0} ({1:N1} KB)" -f $info.FullName, ($info.Length / 1KB)) -ForegroundColor Green

if ($DesktopCopy.Length -gt 0) {
    [System.IO.File]::Copy($exe, $DesktopCopy, $true)
    Write-Host ("copied to: {0}" -f $DesktopCopy) -ForegroundColor Green
}
