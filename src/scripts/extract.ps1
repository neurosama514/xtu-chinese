param(
    [string]$XtuDir = "C:\Program Files\Intel\Intel(R) Extreme Tuning Utility",
    [string]$Work = ""
)
$ErrorActionPreference = "Stop"
if (-not $Work) { $Work = Join-Path (Resolve-Path "$PSScriptRoot\..\..") "work" }

$client = Join-Path $XtuDir "Client"
$perfTune = Join-Path $client "PerfTune.exe"
if (-not (Test-Path $perfTune)) { throw "找不到 PerfTune.exe，请用 -XtuDir 指定 XTU 安装目录" }

New-Item -ItemType Directory -Force -Path $Work, "$Work\orig_baml", "$Work\sdkres" | Out-Null
$env:XTU_WORK = $Work
$env:XTU_ASM_DIR = $client

function Save-Res([string]$asmPath, [string]$resName, [string]$outFile) {
    $a = [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($asmPath))
    $s = $a.GetManifestResourceStream($resName)
    if ($null -eq $s) { throw "资源不存在: $resName" }
    $fs = [System.IO.File]::Create($outFile)
    $s.CopyTo($fs); $fs.Close()
    Write-Output ("  {0} -> {1}" -f $resName, $outFile)
}

function Dump-Resources([string]$asmPath, [string]$resName, [string]$outDir) {
    $a = [System.Reflection.Assembly]::Load([System.IO.File]::ReadAllBytes($asmPath))
    $s = $a.GetManifestResourceStream($resName)
    $r = New-Object System.Resources.ResourceReader($s)
    $n = 0
    foreach ($d in $r) {
        $stream = $d.Value
        if ($stream -is [System.IO.Stream]) {
            $out = Join-Path $outDir ([string]$d.Key -replace '/', '\')
            New-Item -ItemType Directory -Force -Path (Split-Path $out) | Out-Null
            $fs = [System.IO.File]::Create($out)
            $stream.CopyTo($fs); $fs.Close()
            $n++
        }
    }
    Write-Output "  解出 $n 个资源条目到 $outDir"
}

Write-Output "[1/5] 提取 PerfTune.g.resources"
Save-Res $perfTune "PerfTune.g.resources" "$Work\PerfTune.g.resources"

Write-Output "[2/5] 解出 BAML 条目"
Dump-Resources $perfTune "PerfTune.g.resources" "$Work\orig_baml"

Write-Output "[3/5] 提取 SDK 元数据 XML"
$sdk = Join-Path $client "IntelOverclockingSDK.dll"
Save-Res $sdk "IntelOverclockingSDK.Metadata.KnobMetadata.xml" "$Work\sdkres\KnobMetadata.xml"
Save-Res $sdk "IntelOverclockingSDK.Metadata.MonitorMetadata.xml" "$Work\sdkres\MonitorMetadata.xml"

Write-Output "[4/5] 提取 Category.xml 与主题 XAML"
Save-Res $perfTune "PerfTune.Initialization.Category.xml" "$Work\sdkres\Category.xml"
Save-Res $perfTune "PerfTune.Initialization.IntelBlueColorTheme.xaml" "$Work\sdkres\IntelBlueColorTheme.xaml"
Save-Res $perfTune "PerfTune.Initialization.IntelDarkColorTheme.xaml" "$Work\sdkres\IntelDarkColorTheme.xaml"

Write-Output "[5/5] 导出 BAML 文本节点"
$dump = Join-Path $PSScriptRoot "..\..\build\XtuBaml.exe"
& $dump dump "$Work\orig_baml" "$Work\dump_orig.tsv"

Write-Output "提取完成: $Work"
