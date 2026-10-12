param([switch]$SkipCampaign, [switch]$SkipArena)

$ErrorActionPreference = 'Stop'
$taskProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskBuild = Join-Path $taskProject 'Builds\Windows-v0.4'
$taskExe = Join-Path $taskBuild '放学后.exe'
$taskRun = Join-Path $taskProject ('Logs\QA\V04-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $taskRun -Force | Out-Null
if (-not (Test-Path -LiteralPath $taskExe)) { throw '请先运行 Tools/build.ps1。' }

# Unity 会在测试进程退出时保存原生窗口键。整组写入/重启之间保留这些键，
# 验证结束后恢复它们，避免验收改动玩家下次启动的原生窗口状态。
$taskRegistryPath = 'Software\Summer Street Studio\放学后'
$taskRegistry = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($taskRegistryPath)
$taskNativeWindow = @()
if ($null -ne $taskRegistry) {
    try {
        foreach ($taskName in $taskRegistry.GetValueNames()) {
            if ($taskName -like 'Screenmanager*') {
                $taskNativeWindow += [PSCustomObject]@{
                    name = $taskName
                    kind = $taskRegistry.GetValueKind($taskName).ToString()
                    value = $taskRegistry.GetValue($taskName, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
                }
            }
        }
    }
    finally { $taskRegistry.Dispose() }
}
ConvertTo-Json -InputObject @($taskNativeWindow) -Depth 5 | Set-Content -LiteralPath (Join-Path $taskRun 'native-window-before.json') -Encoding UTF8

function Restore-NativeWindow {
    $taskRegistry = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($taskRegistryPath)
    try {
        foreach ($taskName in $taskRegistry.GetValueNames()) {
            if ($taskName -like 'Screenmanager*' -and $taskNativeWindow.name -notcontains $taskName) {
                $taskRegistry.DeleteValue($taskName, $false)
            }
        }
        foreach ($taskValue in $taskNativeWindow) {
            $taskKind = [Microsoft.Win32.RegistryValueKind][Enum]::Parse([Microsoft.Win32.RegistryValueKind], $taskValue.kind)
            $taskRegistry.SetValue($taskValue.name, $taskValue.value, $taskKind)
        }
    }
    finally { $taskRegistry.Dispose() }
}

function Invoke-GameCheck([string]$name, [string]$flags, [string]$reportName, [int]$seconds) {
    $taskOutput = Join-Path $taskRun $name
    New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
    $taskArgs = $flags + ' --qa-output "' + $taskOutput + '" --qa-save "' + (Join-Path $taskOutput 'save') + '" -logFile "' + (Join-Path $taskOutput 'player.log') + '"'
    $taskProcess = Start-Process -FilePath $taskExe -WorkingDirectory $taskBuild -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
    if (-not $taskProcess.WaitForExit($seconds * 1000)) {
        # 只结束本函数刚创建的测试进程，不影响玩家已经打开的游戏。
        Stop-Process -Id $taskProcess.Id -Force
        throw ($name + ' 超时，报告保留在 ' + $taskOutput)
    }
    if ($taskProcess.ExitCode -ne 0) { throw ($name + ' 退出码 ' + $taskProcess.ExitCode) }
    $taskReportPath = Join-Path $taskOutput $reportName
    $taskReport = Get-Content -LiteralPath $taskReportPath -Raw | ConvertFrom-Json
    if ($taskReport.errors.Count -gt 0 -or
        ($taskReport.PSObject.Properties.Name -contains 'passed' -and -not $taskReport.passed) -or
        ($taskReport.PSObject.Properties.Name -contains 'completed' -and -not $taskReport.completed)) {
        throw ($name + ' 未通过，请检查 ' + $taskReportPath)
    }
    Write-Output ($name + ' PASS ' + $taskReportPath)
}

Push-Location $taskProject
try {
    & dotnet run --project Tests/DataRepositoryHarness/DataRepositoryHarness.csproj --configuration Release *> (Join-Path $taskRun 'data.log')
    if ($LASTEXITCODE -ne 0) { throw '数据回归失败，检查 data.log。' }
    Write-Output 'Data PASS'
    # 独立预备进程清理上次强制结束的 QA 原生窗口状态；正式写/读配对不带窗口覆盖参数。
    Invoke-GameCheck 'Display-preflight' '--smoke-test --display-write-test --expected-display Windowed -screen-width 1280 -screen-height 720 -screen-fullscreen 0' 'display-startup-report.json' 60
    foreach ($taskMode in @('FullScreenWindow', 'ExclusiveFullScreen', 'Windowed')) {
        Invoke-GameCheck ('Display-' + $taskMode + '-write') ('--smoke-test --display-write-test --expected-display ' + $taskMode) 'display-startup-report.json' 60
        Invoke-GameCheck ('Display-' + $taskMode + '-readback') ('--smoke-test --display-readback-test --expected-display ' + $taskMode) 'display-startup-report.json' 60
    }
    Invoke-GameCheck 'Product' '--smoke-test --v04-smoke' 'runtime-report.json' 1200
    Invoke-GameCheck 'World' '--world-environment-test' 'world-report.json' 600
    if (-not $SkipArena) { Invoke-GameCheck 'Arena' '--arena-test' 'arena-report.json' 1200 }
    if (-not $SkipCampaign) { Invoke-GameCheck 'Campaign' '--smoke-test --campaign-ui-test' 'campaign-report.json' 1800 }
    Write-Output ('全部所选检查通过：' + $taskRun)
}
finally { Restore-NativeWindow; Pop-Location }
