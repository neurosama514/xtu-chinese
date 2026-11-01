$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression

$w = "C:\xtuwork"
$setup = "$w\setup"
$res = "$setup\res"
New-Item -ItemType Directory -Force -Path $res | Out-Null

function Compress-File([string]$src, [string]$dst) {
    $in = [System.IO.File]::OpenRead($src)
    $out = [System.IO.File]::Create($dst)
    $ds = New-Object System.IO.Compression.DeflateStream($out, [System.IO.Compression.CompressionMode]::Compress)
    $in.CopyTo($ds)
    $ds.Close(); $out.Close(); $in.Close()
    Write-Output ("{0} -> {1} ({2:N1} MB)" -f (Split-Path $src -Leaf), (Split-Path $dst -Leaf), ((Get-Item $dst).Length / 1MB))
}

Compress-File "$w\apply\PerfTune.exe"              "$res\PerfTune.gz"
Compress-File "$w\apply\XtuCommon.dll"             "$res\XtuCommon.gz"
Compress-File "$w\apply\IntelOverclockingSDK.dll"  "$res\IntelOverclockingSDK.gz"
Compress-File "$w\apply\XtuCoreClient.dll"         "$res\XtuCoreClient.gz"
Compress-File "$w\apply\PerfTuneCommon.dll"        "$res\PerfTuneCommon.gz"
Compress-File "$env:USERPROFILE\Downloads\XTUSetup_7.14.2.135.exe" "$res\XTUSetup.gz"
Compress-File "$w\backup_xtu\Client\PerfTune.exe"             "$res\Orig_PerfTune.gz"
Compress-File "$w\backup_xtu\Client\XtuCommon.dll"            "$res\Orig_XtuCommon.gz"
Compress-File "$w\backup_xtu\Client\IntelOverclockingSDK.dll" "$res\Orig_IntelOverclockingSDK.gz"
Compress-File "$w\backup_xtu\Client\XtuCoreClient.dll"        "$res\Orig_XtuCoreClient.gz"
Compress-File "$w\backup_xtu\Client\PerfTuneCommon.dll"       "$res\Orig_PerfTuneCommon.gz"

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$refs = @(
  "/r:System.dll", "/r:System.Core.dll", "/r:System.Drawing.dll",
  "/r:System.Windows.Forms.dll", "/r:System.IO.Compression.dll"
)
$resources = @(
  "/resource:$res\PerfTune.gz,res.PerfTune.gz",
  "/resource:$res\XtuCommon.gz,res.XtuCommon.gz",
  "/resource:$res\IntelOverclockingSDK.gz,res.IntelOverclockingSDK.gz",
  "/resource:$res\XtuCoreClient.gz,res.XtuCoreClient.gz",
  "/resource:$res\PerfTuneCommon.gz,res.PerfTuneCommon.gz",
  "/resource:$res\XTUSetup.gz,res.XTUSetup.gz",
  "/resource:$res\Orig_PerfTune.gz,res.Orig_PerfTune.gz",
  "/resource:$res\Orig_XtuCommon.gz,res.Orig_XtuCommon.gz",
  "/resource:$res\Orig_IntelOverclockingSDK.gz,res.Orig_IntelOverclockingSDK.gz",
  "/resource:$res\Orig_XtuCoreClient.gz,res.Orig_XtuCoreClient.gz",
  "/resource:$res\Orig_PerfTuneCommon.gz,res.Orig_PerfTuneCommon.gz"
)
$out = "$setup\XTU-CN-Setup.exe"
if (Test-Path $out) { Remove-Item $out -Force }

& $csc /nologo /target:winexe /optimize+ /platform:anycpu `
    "/win32manifest:$setup\app.manifest" `
    "/out:$out" `
    $refs $resources `
    "$setup\Setup.cs" 2>&1 | ForEach-Object { $_ }

if (Test-Path $out) {
    Write-Output ("built: {0} ({1:N1} MB)" -f $out, ((Get-Item $out).Length / 1MB))
} else {
    Write-Output "BUILD FAILED"
}
