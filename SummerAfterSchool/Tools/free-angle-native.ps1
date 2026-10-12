param(
    [string]$CandidatePath = 'Builds\Windows-v0.5.6-candidate',
    [string]$Name = ('FreeAngle-056-native-' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
    [int]$Width = 1280,
    [int]$Height = 720,
    [switch]$MobileUI,
    [switch]$UIOnly,
    [switch]$Teaching,
    [switch]$ManualInspection,
    [int]$TimeoutSeconds = 540
)
$ErrorActionPreference = 'Stop'
$taskProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ($Name -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw 'Name must be one safe evidence-directory name.' }
if ($Width -lt 960 -or $Height -lt 540 -or $TimeoutSeconds -lt 30) { throw 'Invalid validation viewport or timeout.' }
if ($Teaching -and $UIOnly) { throw 'Teaching uses its complete guided-experience probe; do not combine Teaching and UIOnly.' }
if ($ManualInspection -and ($Teaching -or $UIOnly -or $MobileUI)) { throw 'ManualInspection uses a separate visible desktop session.' }
if (-not [IO.Path]::IsPathRooted($CandidatePath)) { $CandidatePath = Join-Path $taskProject $CandidatePath }
$taskCandidate = [IO.Path]::GetFullPath($CandidatePath)
$taskExe = if ([IO.Path]::GetExtension($taskCandidate) -eq '.exe') { $taskCandidate } else { Join-Path $taskCandidate '放学后.exe' }
if (-not (Test-Path -LiteralPath $taskExe -PathType Leaf)) { throw ('Candidate executable not found: ' + $taskExe) }
if (Get-Process -Name '放学后','Unity' -ErrorAction SilentlyContinue) { throw 'Preserve the active Unity or player session; validation was not started.' }
$taskOutput = Join-Path $taskProject ('Logs\QA\' + $Name)
if (Test-Path -LiteralPath $taskOutput) { throw ('Keep previous evidence; choose a fresh Name: ' + $taskOutput) }
New-Item -ItemType Directory -Path $taskOutput | Out-Null

function Get-RegistrySnapshot {
    $taskKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($taskRegistryPath)
    $taskValues = @()
    if ($null -ne $taskKey) {
        try {
            foreach ($taskValueName in $taskKey.GetValueNames()) {
                $taskValues += [PSCustomObject]@{
                    name = $taskValueName
                    kind = $taskKey.GetValueKind($taskValueName).ToString()
                    value = $taskKey.GetValue($taskValueName, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
                }
            }
        }
        finally { $taskKey.Dispose() }
    }
    return ,$taskValues
}
function Get-RealSaveSnapshot {
    $taskSaves = @()
    foreach ($taskRelative in @('SummerAfterSchool-v2\collection.json','SummerAfterSchool-v2\collection.backup.json','SummerAfterSchool\collection.json','SummerAfterSchool\collection.backup.json')) {
        $taskSavePath = Join-Path $taskPersistent $taskRelative
        $taskExists = Test-Path -LiteralPath $taskSavePath -PathType Leaf
        $taskSaves += [PSCustomObject]@{ path = $taskSavePath; exists = $taskExists; sha256 = $(if ($taskExists) { (Get-FileHash -LiteralPath $taskSavePath -Algorithm SHA256).Hash } else { $null }) }
    }
    return ,$taskSaves
}
function Set-PreservedRegistryValue([Microsoft.Win32.RegistryKey]$taskKey, $taskValue) {
    $taskKind = [Microsoft.Win32.RegistryValueKind][Enum]::Parse([Microsoft.Win32.RegistryValueKind], $taskValue.kind)
    # PS 5.1 的 PSCustomObject 属性会装箱或展开数组；显式类型让 SetValue 与 kind 一致。
    switch ($taskKind.ToString()) {
        'Binary' { $taskKey.SetValue($taskValue.name, [byte[]]$taskValue.value, $taskKind) }
        'DWord' {
            $taskNumber = [long]$taskValue.value
            if ($taskNumber -ge [int]::MinValue -and $taskNumber -le [int]::MaxValue) {
                $taskKey.SetValue($taskValue.name, [int]$taskValue.value, $taskKind)
            }
            else {
                # Unity 的旧 PlayerPrefs 浮点键曾以8字节写入REG_DWORD。
                # .NET GetValue 返回Int64，但SetValue(DWord)只收Int32；按原type/bytes恢复，不能改成QWord。
                if (-not ('SummerNativeRegistryPreservation' -as [type])) {
                    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SummerNativeRegistryPreservation {
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegSetValueExW")]
    public static extern int SetRaw(IntPtr key, string name, int reserved, int kind, byte[] data, int length);
}
'@
                }
                $taskBytes = [BitConverter]::GetBytes($taskNumber)
                $taskResult = [SummerNativeRegistryPreservation]::SetRaw($taskKey.Handle.DangerousGetHandle(), $taskValue.name, 0, [int]$taskKind, $taskBytes, $taskBytes.Length)
                if ($taskResult -ne 0) { throw (New-Object ComponentModel.Win32Exception($taskResult)) }
            }
        }
        'QWord' { $taskKey.SetValue($taskValue.name, [long]$taskValue.value, $taskKind) }
        'MultiString' { $taskKey.SetValue($taskValue.name, [string[]]$taskValue.value, $taskKind) }
        'String' { $taskKey.SetValue($taskValue.name, [string]$taskValue.value, $taskKind) }
        'ExpandString' { $taskKey.SetValue($taskValue.name, [string]$taskValue.value, $taskKind) }
        'None' { $taskKey.SetValue($taskValue.name, [byte[]]$taskValue.value, $taskKind) }
        default { throw ('Unsupported preserved registry kind: ' + $taskValue.kind + ' / ' + $taskValue.name) }
    }
}

$taskRegistryPath = 'Software\Summer Street Studio\放学后'
$taskPersistent = Join-Path $env:USERPROFILE 'AppData\LocalLow\Summer Street Studio\放学后'
$taskPrefsBefore = Get-RegistrySnapshot
$taskSavesBefore = Get-RealSaveSnapshot
ConvertTo-Json -InputObject @($taskPrefsBefore) -Depth 8 | Set-Content -LiteralPath (Join-Path $taskOutput 'player-prefs-before.json') -Encoding UTF8
ConvertTo-Json -InputObject @($taskPrefsBefore | Where-Object { $_.name -like 'Screenmanager*' }) -Depth 8 | Set-Content -LiteralPath (Join-Path $taskOutput 'native-window-before.json') -Encoding UTF8
ConvertTo-Json -InputObject @($taskSavesBefore) -Depth 5 | Set-Content -LiteralPath (Join-Path $taskOutput 'real-save-before.json') -Encoding UTF8
$taskRun = [ordered]@{
    startedUtc = [DateTime]::UtcNow.ToString('o'); finishedUtc = $null; candidateExe = $taskExe
    exeSha256 = (Get-FileHash -LiteralPath $taskExe -Algorithm SHA256).Hash; width = $Width; height = $Height
    mobileUI = [bool]$MobileUI; uiOnly = [bool]$UIOnly; teaching = [bool]$Teaching; manualInspection = [bool]$ManualInspection; platform = 'WindowsStandalonePlayer'; processId = $null; exitCode = $null
    timedOut = $false; preferencesRestored = $false; realSavesUnchanged = $false; userSessionObservedAtEnd = $false; manualSessionClosedNormally = $false; passed = $false; error = $null; cleanupErrors = @()
}
$taskProcess = $null; $taskReport = $null
try {
    # 不触碰正式构建；所有验收写盘均使用这次新建的隔离目录。
    $taskFlags = if ($Teaching) { '--smoke-test --guided-experience-test' } else { '--smoke-test --free-angle-experience-test' }
    if ($ManualInspection) { $taskFlags = '--smoke-test --qa-manual-inspection' }
    if ($MobileUI) { $taskFlags += ' --mobile-ui-test' }
    if ($UIOnly) { $taskFlags += ' --free-angle-ui-only' }
    $taskArgs = $taskFlags + ' --qa-output "' + $taskOutput + '" --qa-save "' + (Join-Path $taskOutput 'save') + '" -screen-fullscreen 0 -screen-width ' + $Width + ' -screen-height ' + $Height + ' -logFile "' + (Join-Path $taskOutput 'player.log') + '"'
    $taskRun.arguments = $taskArgs
    $taskWindowStyle = if ($ManualInspection) { 'Normal' } else { 'Hidden' }
    $taskProcess = Start-Process -FilePath $taskExe -WorkingDirectory (Split-Path -Parent $taskExe) -ArgumentList $taskArgs -WindowStyle $taskWindowStyle -PassThru
    $taskRun.processId = $taskProcess.Id
    $taskProcess.Id | Set-Content -LiteralPath (Join-Path $taskOutput 'process-id.txt')
    if (-not $taskProcess.WaitForExit($TimeoutSeconds * 1000)) {
        $taskRun.timedOut = $true
        # 只释放本脚本自己创建的验收进程；不会关闭玩家或Unity编辑器。
        Stop-Process -Id $taskProcess.Id -Force
        $taskProcess.WaitForExit()
        throw ('Native validation timed out; failed evidence is retained: ' + $taskOutput)
    }
    $taskProcess.Refresh(); $taskRun.exitCode = $taskProcess.ExitCode
    if ($ManualInspection) {
        if ($taskRun.exitCode -ne 0) { throw ('Visible manual session exited with code ' + $taskRun.exitCode + '.') }
        $taskRun.manualSessionClosedNormally = $true
        $taskRun.passed = $true
    }
    else {
        $taskReportPath = Join-Path $taskOutput $(if ($Teaching) { 'guided-experience-report.json' } else { 'free-angle-experience-report.json' })
        if (-not (Test-Path -LiteralPath $taskReportPath -PathType Leaf)) { throw ('Native report missing: ' + $taskOutput) }
        # JsonUtility输出UTF-8；Windows PowerShell5.1默认Get-Content会误按ANSI解码中文。
        try { $taskReport = [IO.File]::ReadAllText($taskReportPath, [Text.Encoding]::UTF8) | ConvertFrom-Json }
        catch { throw ('Native UTF-8 report could not be parsed; inspect the retained file: ' + $taskReportPath) }
        if ($taskRun.exitCode -ne 0 -or -not $taskReport.completed -or -not $taskReport.passed -or $taskReport.errors.Count -ne 0) { throw ('Native validation failed; failed report is retained: ' + $taskReportPath) }
        $taskRun.passed = $true
    }
}
catch { $taskRun.error = $_.Exception.Message; if ($taskRun.error.Length -gt 1200) { $taskRun.error = $taskRun.error.Substring(0,1200) + ' [truncated; see retained evidence]' } }
finally {
    try { if ($null -ne $taskProcess -and -not $taskProcess.HasExited) { Stop-Process -Id $taskProcess.Id -Force; $taskProcess.WaitForExit() } }
    catch { $taskRun.cleanupErrors += ('Owned-process cleanup: ' + $_.Exception.Message) }
    try { $taskRun.userSessionObservedAtEnd = [bool](Get-Process -Name '放学后','Unity' -ErrorAction SilentlyContinue) }
    catch { $taskRun.userSessionObservedAtEnd = $true; $taskRun.cleanupErrors += ('Session inventory: ' + $_.Exception.Message) }
    if (-not $taskRun.userSessionObservedAtEnd) {
        try {
            $taskKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($taskRegistryPath)
            try {
                foreach ($taskValueName in $taskKey.GetValueNames()) {
                    if ($taskPrefsBefore.name -notcontains $taskValueName) {
                        try { $taskKey.DeleteValue($taskValueName, $false) }
                        catch { $taskRun.cleanupErrors += ('Preference delete ' + $taskValueName + ': ' + $_.Exception.Message) }
                    }
                }
                foreach ($taskValue in $taskPrefsBefore) {
                    try { Set-PreservedRegistryValue $taskKey $taskValue }
                    catch { $taskRun.cleanupErrors += ('Preference restore ' + $taskValue.name + ': ' + $_.Exception.Message) }
                }
            }
            finally { $taskKey.Dispose() }
            $taskPrefsAfter = Get-RegistrySnapshot
            $taskBeforeText = ConvertTo-Json -InputObject @($taskPrefsBefore | Sort-Object name) -Depth 8 -Compress
            $taskAfterText = ConvertTo-Json -InputObject @($taskPrefsAfter | Sort-Object name) -Depth 8 -Compress
            $taskRun.preferencesRestored = $taskBeforeText -ceq $taskAfterText
            ConvertTo-Json -InputObject @($taskPrefsAfter) -Depth 8 | Set-Content -LiteralPath (Join-Path $taskOutput 'player-prefs-after.json') -Encoding UTF8
        }
        catch { $taskRun.cleanupErrors += ('Preference preservation: ' + $_.Exception.Message) }
    }
    else {
        # 新会话出现时不覆盖玩家最新偏好；仅记录待人工恢复的旧键证据。
        $taskRun.cleanupErrors += 'A new user session appeared; preferences were not overwritten. Before-values are retained for review.'
    }
    try {
        $taskSavesAfter = Get-RealSaveSnapshot
        $taskRun.realSavesUnchanged = (ConvertTo-Json -InputObject $taskSavesBefore -Depth 5 -Compress) -ceq (ConvertTo-Json -InputObject $taskSavesAfter -Depth 5 -Compress)
        ConvertTo-Json -InputObject @($taskSavesAfter) -Depth 5 | Set-Content -LiteralPath (Join-Path $taskOutput 'real-save-after.json') -Encoding UTF8
    }
    catch { $taskRun.cleanupErrors += ('Real-save preservation: ' + $_.Exception.Message) }
    $taskRun.passed = $taskRun.passed -and $taskRun.preferencesRestored -and $taskRun.realSavesUnchanged -and -not $taskRun.userSessionObservedAtEnd -and $taskRun.cleanupErrors.Count -eq 0
    $taskRun.finishedUtc = [DateTime]::UtcNow.ToString('o')
    $taskRun | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskOutput 'native-run.json') -Encoding UTF8
    if ($null -ne $taskProcess) { try { $taskProcess.Dispose() } catch { Write-Warning $_.Exception.Message } }
}
if ($null -ne $taskReport) {
    if ($ManualInspection) { $taskReport | ConvertTo-Json -Depth 6 }
    elseif ($Teaching) {
        $taskReport | Select-Object completed,passed,version,engine,platform,isolatedSave,dragStagePassed,slapStagePassed,skimStagePassed,allCompleted,pausedFlightFrozen,cancellationPreserved,diskReadbackVerified,obliqueGestures,topDownGestures,settledShots,elapsedSeconds,@{n='checks';e={$_.assertions.Count}},@{n='captures';e={$_.captures.Count}},errors | ConvertTo-Json -Depth 6
    }
    else {
        $taskReport | Select-Object completed,passed,version,engine,platform,mobileUI,uiOnly,fullMatchValidated,fourDirectionsValidated,realTiltValidated,pauseFrozen,resumedBoard,resumedMetadataIdentical,resumedMaxPositionComponentDifference,resumedMaxNormalizedQuaternionComponentDifference,diskReadback,legacySetupDefaulted,futureSnapshotRejected,cameraLocationsVerified,decorationViewsVerified,outcome,playerScore,npcScore,elapsedSeconds,@{n='checks';e={$_.checks.Count}},@{n='shots';e={$_.shots.Count}},@{n='captures';e={$_.captures.Count}},errors | ConvertTo-Json -Depth 6
    }
}
if (-not $taskRun.passed) { throw ($taskRun.error + ' ' + ($taskRun.cleanupErrors -join ' | ') + ' Validation or preservation failed; evidence: ' + $taskOutput) }
if ($ManualInspection) { Write-Output ('Visible manual-session launch, normal exit, and preservation PASS; input actions are not asserted: ' + $taskOutput) }
else { Write-Output ('Native free-angle validation and preservation PASS: ' + $taskOutput) }
