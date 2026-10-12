$ErrorActionPreference = 'Stop'
$taskProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskBuild = Join-Path $taskProject 'Builds\Windows-v0.4'
$taskExe = Join-Path $taskBuild '放学后.exe'
if (-not (Test-Path -LiteralPath $taskExe -PathType Leaf)) { throw '请先构建 0.4.0。' }
$taskShell = New-Object -ComObject WScript.Shell
$taskLinks = @(
    (Join-Path ([Environment]::GetFolderPath('Desktop')) '放学后.lnk'),
    (Join-Path (Split-Path $taskProject -Parent) '放学后.lnk')
) | Select-Object -Unique
foreach ($taskPath in $taskLinks) {
    $taskLink = $taskShell.CreateShortcut($taskPath)
    $taskLink.TargetPath = $taskExe
    $taskLink.WorkingDirectory = $taskBuild
    $taskLink.Arguments = ''
    $taskLink.IconLocation = $taskExe + ',0'
    $taskLink.Description = '放学后 0.4.0 · 故事与盛夏锦标赛'
    $taskLink.Save()
    Write-Output ('SHORTCUT_OK ' + $taskPath)
}
