param([string]$UnityPath = 'D:\GameDev\Unity\6000.3.25f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$taskProject = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskLog = Join-Path $taskProject 'Logs\build.log'
New-Item -ItemType Directory -Path (Join-Path $taskProject 'Logs') -Force | Out-Null
if (-not (Test-Path -LiteralPath $UnityPath)) { throw 'Unity.exe not found. Pass -UnityPath with your installed Unity 6000.3 path.' }
$taskArgs = @('-batchmode','-quit','-projectPath',('"' + $taskProject + '"'),'-executeMethod','Childhood.Editor.SummerBuild.BuildWindows','-logFile',('"' + $taskLog + '"'))
$taskProcess = Start-Process -FilePath $UnityPath -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
$taskProcess.WaitForExit()
if ($taskProcess.ExitCode -ne 0) { Get-Content -LiteralPath $taskLog -Tail 80; throw "Unity build failed: $($taskProcess.ExitCode)" }
Write-Output (Join-Path $taskProject 'Builds\Windows\放学后.exe')
