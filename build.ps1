param(
    [ValidateSet("all", "tools", "extract", "generate", "patch", "restore", "installer")]
    [string]$Stage = "all",
    [string]$XtuDir = "C:\Program Files\Intel\Intel(R) Extreme Tuning Utility",
    [string]$Work = ""
)
$ErrorActionPreference = "Stop"
$repo = Resolve-Path "$PSScriptRoot"
if (-not $Work) { $Work = Join-Path $repo "work" }
$env:XTU_WORK = $Work
$client = Join-Path $XtuDir "Client"
$env:XTU_ASM_DIR = $client

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpf = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"
$fx = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$build = Join-Path $repo "build"
$lib = Join-Path $repo "lib"
$apply = Join-Path $Work "apply"
$origAsm = Join-Path $Work "orig_asm"

function Ensure-Dir($p) { New-Item -ItemType Directory -Force -Path $p | Out-Null }

function Ensure-Cecil {
    Ensure-Dir $lib
    $dll = Join-Path $lib "Mono.Cecil.dll"
    if (Test-Path $dll) { return $dll }
    Write-Host "下载 Mono.Cecil ..."
    $nupkg = Join-Path $lib "cecil.zip"
    Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/Mono.Cecil/0.11.5" -OutFile $nupkg -UseBasicParsing
    Expand-Archive -Path $nupkg -DestinationPath (Join-Path $lib "cecil") -Force
    Copy-Item (Join-Path $lib "cecil\lib\net40\Mono.Cecil.dll") $dll -Force
    return $dll
}

function Build-Tools {
    Ensure-Dir $build
    Write-Output "编译 XtuBaml.exe ..."
    & $csc /nologo /target:exe "/out:$build\XtuBaml.exe" `
        "/r:$wpf\PresentationFramework.dll" "/r:$wpf\PresentationCore.dll" "/r:$wpf\WindowsBase.dll" "/r:$fx\System.Xaml.dll" `
        (Join-Path $repo "src\tools\XtuBaml.cs") | Out-Null
    Write-Output "编译 XtuPatch.exe ..."
    $cecil = Ensure-Cecil
    & $csc /nologo /target:exe "/out:$build\XtuPatch.exe" "/r:$cecil" (Join-Path $repo "src\tools\XtuPatch.cs") | Out-Null
    if (Test-Path $build) { Copy-Item $cecil $build -Force }
    Write-Output "工具就绪: $build"
}

function Stage-Extract {
    Ensure-Dir $Work; Ensure-Dir $apply; Ensure-Dir $origAsm
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo "src\scripts\extract.ps1") -XtuDir $XtuDir -Work $Work
    foreach ($f in @("PerfTune.exe", "XtuCommon.dll", "IntelOverclockingSDK.dll", "XtuCoreClient.dll", "PerfTuneCommon.dll")) {
        $dst = Join-Path $origAsm $f
        if (-not (Test-Path $dst)) {
            Copy-Item (Join-Path $client $f) $dst -Force
            Write-Output "  保存原始程序集 $f"
        }
    }
}

function Stage-Generate {
    Ensure-Dir $Work; Ensure-Dir (Join-Path $Work "out")
    # 翻译文件
    Copy-Item (Join-Path $repo "translations\*.tsv") $Work -Force
    Copy-Item (Join-Path $repo "translations\tr_*.tsv") (Join-Path $Work "out") -Force
    if (-not (Test-Path (Join-Path $Work "cands.tsv"))) {
        Write-Output "生成候选文案表 ..."
        python (Join-Path $repo "src\scripts\candidates.py")
    }
    if (-not (Test-Path (Join-Path $Work "task_baml.tsv"))) {
        python (Join-Path $repo "src\scripts\prep_tasks.py")
        python (Join-Path $repo "src\scripts\prep_round2.py")
    }
    Write-Output "生成补丁映射表 ..."
    python (Join-Path $repo "src\scripts\build_maps.py")
    python (Join-Path $repo "src\scripts\patch_themes.py")
    python (Join-Path $repo "src\scripts\extra_strings.py")
}

function Stage-Patch {
    Ensure-Dir $apply
    $baml = Join-Path $repo "build\XtuBaml.exe"
    $patch = Join-Path $repo "build\XtuPatch.exe"

    Write-Output "[1/6] 应用 BAML 译文"
    & $baml apply (Join-Path $Work "orig_baml") (Join-Path $apply "baml_map.tsv") (Join-Path $Work "baml_cn")
    & $baml validate (Join-Path $Work "baml_cn")

    Write-Output "[2/6] 重建 PerfTune.g.resources"
    & $patch resbuild (Join-Path $Work "PerfTune.g.resources") (Join-Path $Work "baml_cn") (Join-Path $apply "PerfTune.g.resources")

    Write-Output "[3/6] 改写 PerfTune.exe"
    & $patch patchasm (Join-Path $origAsm "PerfTune.exe") (Join-Path $apply "PerfTune.exe") (Join-Path $apply "resmap_perftune.tsv") (Join-Path $apply "strmap_perftune.tsv") "-"

    Write-Output "[4/6] 改写 XtuCommon.dll / XtuCoreClient.dll / PerfTuneCommon.dll"
    & $patch patchasm (Join-Path $origAsm "XtuCommon.dll") (Join-Path $apply "XtuCommon.dll") "-" (Join-Path $apply "strmap_xtucommon.tsv") "-"
    & $patch patchasm (Join-Path $origAsm "XtuCoreClient.dll") (Join-Path $apply "XtuCoreClient.dll") "-" (Join-Path $apply "strmap_coreclient.tsv") "-"
    & $patch patchasm (Join-Path $origAsm "PerfTuneCommon.dll") (Join-Path $apply "PerfTuneCommon.dll") "-" (Join-Path $apply "strmap_ptcommon.tsv") "-"

    Write-Output "[5/6] 改写 IntelOverclockingSDK.dll（元数据 + 跳过签名自校验）"
    & $patch patchasm (Join-Path $origAsm "IntelOverclockingSDK.dll") (Join-Path $apply "IntelOverclockingSDK.dll") (Join-Path $apply "resmap_sdk.tsv") "-" (Join-Path $apply "neut_sdk.tsv")

    Write-Output "[6/6] 写入安装目录"
    $backup = Join-Path $env:ProgramData "XTU-CN\backup"
    Ensure-Dir $backup
    Get-Process PerfTune -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
    foreach ($f in @("PerfTune.exe", "XtuCommon.dll", "IntelOverclockingSDK.dll", "XtuCoreClient.dll", "PerfTuneCommon.dll")) {
        $src = Join-Path $apply $f; $dst = Join-Path $client $f; $bak = Join-Path $backup $f
        if (-not (Test-Path $bak)) { Copy-Item (Join-Path $origAsm $f) $bak -Force }
        Copy-Item $src $dst -Force
        Write-Output "  已更新 $f"
    }
    Start-Process (Join-Path $client "PerfTune.exe")
    Write-Output "完成，XTU 已重启。"
}

function Stage-Restore {
    $backup = Join-Path $env:ProgramData "XTU-CN\backup"
    if (-not (Test-Path $backup)) { throw "找不到备份目录 $backup" }
    Get-Process PerfTune -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
    foreach ($f in @("PerfTune.exe", "XtuCommon.dll", "IntelOverclockingSDK.dll", "XtuCoreClient.dll", "PerfTuneCommon.dll")) {
        Copy-Item (Join-Path $backup $f) (Join-Path $client $f) -Force
        Write-Output "  已还原 $f"
    }
    Start-Process (Join-Path $client "PerfTune.exe")
}

function Stage-Installer {
    $setup = Join-Path $repo "src\installer"
    $res = Join-Path $Work "installer_res"
    Ensure-Dir $res
    $pairs = @(
        @("$apply\PerfTune.exe", "PerfTune", "$origAsm\PerfTune.exe"),
        @("$apply\XtuCommon.dll", "XtuCommon", "$origAsm\XtuCommon.dll"),
        @("$apply\IntelOverclockingSDK.dll", "IntelOverclockingSDK", "$origAsm\IntelOverclockingSDK.dll"),
        @("$apply\XtuCoreClient.dll", "XtuCoreClient", "$origAsm\XtuCoreClient.dll"),
        @("$apply\PerfTuneCommon.dll", "PerfTuneCommon", "$origAsm\PerfTuneCommon.dll")
    )
    Add-Type -AssemblyName System.IO.Compression
    function Deflate($src, $dst) {
        $i = [System.IO.File]::OpenRead($src); $o = [System.IO.File]::Create($dst)
        $d = New-Object System.IO.Compression.DeflateStream($o, [System.IO.Compression.CompressionMode]::Compress)
        $i.CopyTo($d); $d.Close(); $o.Close(); $i.Close()
    }
    $argsList = @()
    foreach ($p in $pairs) {
        Deflate $p[0] (Join-Path $res ($p[1] + ".gz"))
        Deflate $p[2] (Join-Path $res ("Orig_" + $p[1] + ".gz"))
        $argsList += "/resource:" + (Join-Path $res ($p[1] + ".gz")) + ",res." + $p[1] + ".gz"
        $argsList += "/resource:" + (Join-Path $res ("Orig_" + $p[1] + ".gz")) + ",res.Orig_" + $p[1] + ".gz"
    }
    $vendor = Get-ChildItem (Join-Path $repo "vendor") -Filter "XTUSetup*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($vendor) {
        Deflate $vendor.FullName (Join-Path $res "XTUSetup.gz")
        $argsList += "/resource:" + (Join-Path $res "XTUSetup.gz") + ",res.XTUSetup.gz"
        Write-Output "已内置官方安装包: $($vendor.Name)"
    } else {
        Write-Output "未找到 vendor\XTUSetup_7.14.2.135.exe，安装器将不含官方安装包（仅汉化补丁）"
    }
    $out = Join-Path $repo "dist\XTU-CN-Setup.exe"
    Ensure-Dir (Split-Path $out)
    & $csc /nologo /target:winexe /optimize+ /platform:anycpu `
        "/win32manifest:$setup\app.manifest" "/out:$out" `
        "/r:System.dll" "/r:System.Core.dll" "/r:System.Drawing.dll" "/r:System.Windows.Forms.dll" "/r:System.IO.Compression.dll" `
        $argsList (Join-Path $setup "Setup.cs")
    Write-Output "安装包: $out"
}

switch ($Stage) {
    "tools" { Build-Tools }
    "extract" { Stage-Extract }
    "generate" { Stage-Generate }
    "patch" {
        if (-not (Test-Path (Join-Path $build "XtuPatch.exe"))) { Build-Tools }
        if (-not (Test-Path (Join-Path $Work "apply\baml_map.tsv"))) { Stage-Generate }
        Stage-Patch
    }
    "restore" { Stage-Restore }
    "installer" { Stage-Installer }
    "all" {
        Build-Tools
        Stage-Extract
        Stage-Generate
        Stage-Patch
        Stage-Installer
    }
}
