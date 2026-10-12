param([string]$UnityPath = 'D:\GameDev\Unity\6000.3.25f1\Editor\Unity.exe', [string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$taskProject = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskLog = Join-Path $taskProject 'Logs\build.log'
New-Item -ItemType Directory -Path (Join-Path $taskProject 'Logs') -Force | Out-Null
if (-not (Test-Path -LiteralPath $UnityPath)) { throw 'Unity.exe not found. Pass -UnityPath with your installed Unity 6000.3 path.' }
$taskEditors = Get-CimInstance Win32_Process | Where-Object {
    $_.Name -eq 'Unity.exe' -and $_.CommandLine -and $_.CommandLine.IndexOf($taskProject, [StringComparison]::OrdinalIgnoreCase) -ge 0
}
if ($taskEditors) { throw 'This Unity project is already open. Preserve the current editor session and build later.' }
$taskArgs = @('-batchmode','-quit','-projectPath',('"' + $taskProject + '"'),'-executeMethod','Childhood.Editor.SummerBuild.BuildWindows','-logFile',('"' + $taskLog + '"'))
if ($OutputDirectory) {
    $taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
    $taskArgs += @('--windows-output', ('"' + $taskOutput + '"'))
}
else { $taskOutput = Join-Path $taskProject 'Builds\Windows-v0.4' }
$taskRunningBuild = Get-Process -Name '放学后' -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -and [IO.Path]::GetDirectoryName($_.Path) -eq $taskOutput
}
if ($taskRunningBuild) { throw 'The target build is in use. Choose an unused OutputDirectory or build later.' }
$taskProcess = Start-Process -FilePath $UnityPath -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
$taskProcess.WaitForExit()
if ($taskProcess.ExitCode -ne 0) { Get-Content -LiteralPath $taskLog -Tail 80; throw "Unity build failed: $($taskProcess.ExitCode)" }
Write-Output (Join-Path $taskOutput '放学后.exe')
