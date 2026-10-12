param(
    [string]$UnityPath = 'D:\GameDev\Unity\6000.3.25f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$sourceProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
New-Item -ItemType Directory -Path (Join-Path $sourceProject 'Logs') -Force | Out-Null
$project = $sourceProject
$staging = $null
if ($sourceProject -match '[^\x00-\x7F]') {
    $staging = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'SummerAfterSchool-AndroidBuild'))
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    if ((Get-Item -LiteralPath $staging).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Staging directory must not be a link.' }
    # 只镜像 Unity 的三个输入目录，删除已从源工程移除的旧脚本，保留 Library 编译缓存。
    foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
        $stageTarget = [IO.Path]::GetFullPath((Join-Path $staging $folder))
        if (-not $stageTarget.StartsWith($staging + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe staging target.' }
        if ((Test-Path -LiteralPath $stageTarget) -and ((Get-Item -LiteralPath $stageTarget).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Staging input directory must not be a link.' }
        robocopy (Join-Path $sourceProject $folder) $stageTarget /MIR /XJ /NFL /NDL /NJH /NJS | Out-Null
        if ($LASTEXITCODE -gt 7) { throw "Failed to stage $folder`: $LASTEXITCODE" }
    }
    $project = $staging
}
$log = Join-Path $project 'Logs\android-build.log'
$summary = Join-Path $project 'Logs\android-build-summary.json'
New-Item -ItemType Directory -Path (Join-Path $project 'Logs') -Force | Out-Null
if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity.exe not found: $UnityPath" }
$arguments = @(
    '-batchmode', '-quit', '-projectPath', ('"' + $project + '"'),
    '-executeMethod', 'Childhood.Editor.SummerAndroidBuild.BuildAndroid',
    '-logFile', ('"' + $log + '"')
)
$buildStartedUtc = [DateTime]::UtcNow
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) {
    if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 120 }
    throw "Unity Android build failed: $($process.ExitCode)"
}
$apk = Join-Path $project 'Builds\Android-v0.5\after-school.apk'
if (-not (Test-Path -LiteralPath $apk)) { throw "APK output not found: $apk" }
$apkInfo = Get-Item -LiteralPath $apk
if ($apkInfo.Length -le 0) { throw "APK output is empty: $apk" }
if ($apkInfo.LastWriteTimeUtc -lt $buildStartedUtc) { throw "APK output is stale: $apk" }
if (-not (Test-Path -LiteralPath $summary)) { throw 'Android build summary is missing.' }
if ((Get-Item -LiteralPath $summary).LastWriteTimeUtc -lt $buildStartedUtc) { throw 'Android build summary is stale.' }
$summaryData = Get-Content -LiteralPath $summary -Raw | ConvertFrom-Json
if ($summaryData.result -ne 'Succeeded' -or $summaryData.errors -ne 0) { throw 'Android build report did not succeed.' }
if ($summaryData.apkBytes -ne $apkInfo.Length) { throw 'APK size does not match the build summary.' }
if ($staging) {
    $destination = Join-Path $sourceProject 'Builds\Android-v0.5'
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -LiteralPath $apk -Destination (Join-Path $destination 'after-school.apk') -Force
    Copy-Item -LiteralPath $log -Destination (Join-Path $sourceProject 'Logs\android-build.log') -Force
    if (Test-Path -LiteralPath $summary) {
        $summaryData = Get-Content -LiteralPath $summary -Raw | ConvertFrom-Json
        $summaryData.output = Join-Path $destination 'after-school.apk'
        $summaryData | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $sourceProject 'Logs\android-build-summary.json') -Encoding UTF8
    }
    $apk = Join-Path $destination 'after-school.apk'
}
Write-Output $apk
