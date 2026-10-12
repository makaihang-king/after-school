param([switch]$DiagnosticsOnly)

$ErrorActionPreference = 'Stop'
$taskProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskBuild = Join-Path $taskProject 'Builds\Windows'
$taskExe = Join-Path $taskBuild '放学后.exe'
$taskLogs = Join-Path $taskProject 'Logs'
$taskLaunchLog = Join-Path $taskLogs 'launcher.log'
$taskPlayerLog = Join-Path $taskLogs 'play.log'

function Write-LaunchLog([string]$message) {
    Add-Content -LiteralPath $taskLaunchLog -Encoding UTF8 -Value ((Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + ' ' + $message)
}

try {
    New-Item -ItemType Directory -Path $taskLogs -Force | Out-Null
    foreach ($relative in @('放学后.exe', 'UnityPlayer.dll', '放学后_Data\globalgamemanagers', '放学后_Data\Managed\Assembly-CSharp.dll', 'MonoBleedingEdge\EmbedRuntime\mono-2.0-bdwgc.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $taskBuild $relative) -PathType Leaf)) {
            throw ('缺少游戏文件：' + $relative + '。请保留完整的 Windows 游戏文件夹。')
        }
    }
    if ($DiagnosticsOnly) { Write-Output ('LAUNCHER_FILES_OK ' + $taskExe); exit 0 }

    $taskExisting = Get-Process -Name '放学后' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $taskExe } | Select-Object -First 1
    if ($null -ne $taskExisting) {
        Write-LaunchLog ('Already running PID=' + $taskExisting.Id)
        $taskShell = New-Object -ComObject WScript.Shell
        $null = $taskShell.AppActivate($taskExisting.Id)
        exit 0
    }

    # 显式使用正常窗口；测试模式与隐藏窗口不进入日常启动入口。
    $taskArguments = @('-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0', '-logFile', ('"' + $taskPlayerLog + '"'))
    Write-LaunchLog ('Launching ' + $taskExe)
    $taskProcess = Start-Process -FilePath $taskExe -WorkingDirectory $taskBuild -ArgumentList $taskArguments -WindowStyle Normal -PassThru
    if ($taskProcess.WaitForExit(8000)) {
        Write-LaunchLog ('Exited during startup code=' + $taskProcess.ExitCode)
        if ($taskProcess.ExitCode -ne 0) { throw ('游戏启动后退出，错误码 ' + $taskProcess.ExitCode + '。详细日志：' + $taskPlayerLog) }
    }
    else {
        $taskProcess.Refresh()
        Write-LaunchLog ('Running PID=' + $taskProcess.Id + ' window=' + $taskProcess.MainWindowHandle + ' title=' + $taskProcess.MainWindowTitle)
    }
    exit 0
}
catch {
    try { Write-LaunchLog ('ERROR ' + $_.Exception.Message) } catch { }
    Add-Type -AssemblyName System.Windows.Forms
    $null = [System.Windows.Forms.MessageBox]::Show(('启动失败：' + $_.Exception.Message + "`r`n`r`n" + '启动日志：' + $taskLaunchLog), '放学后 · 启动检查', 'OK', 'Error')
    exit 1
}
