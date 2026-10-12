$ErrorActionPreference = 'Stop'
$taskProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskBuild = Join-Path $taskProject 'Builds\Windows-v0.4'
$taskArchive = Join-Path $taskProject 'Builds\放学后-0.4.0-Windows.zip'
$taskStage = Join-Path $taskProject ('Logs\Packaging\v04-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$taskGame = Join-Path $taskStage '放学后-0.4.0'
if (-not (Test-Path -LiteralPath (Join-Path $taskBuild '放学后.exe'))) { throw '请先构建并验收 0.4.0。' }
New-Item -ItemType Directory -Path $taskGame -Force | Out-Null

# 发布包只装运行库和成品，排除独立 QA 存档、截图和日志。
foreach ($taskItem in Get-ChildItem -LiteralPath $taskBuild) {
    if ($taskItem.Name -ne 'Logs') { Copy-Item -LiteralPath $taskItem.FullName -Destination $taskGame -Recurse }
}
$taskReadme = @'
放学后 0.4.0

解压完整文件夹后，双击「放学后.exe」。请保留旁边的 Data 和运行库文件夹。

首页可进入八关故事、八人锦标赛、自由约战、铁盒、小卖部和跑腿。
故事和锦标赛都玩真片，双方各押一张。练习片可交易，不能押进真局。
玻璃珠用于买卖圆片。输光后可接跑腿、前往目的地并交付，再兑换真片。

按住地面向后拖动，松开出手；1/2/3 选择掌拍/重拍/斜擦。
鼠标滚轮或 Q/E 调整旋转；右键取消蓄力；Esc 暂停；M 静音。
遇到砖沿或门槛，可以用掌拍跨过障碍。

设置支持全屏、全屏窗口化、窗口化；切换后须在 15 秒内确认。
首次默认 1280×720 窗口，之后记忆显示选择和窗口尺寸。

新存档位于 LocalLow/Summer Street Studio/放学后/SummerAfterSchool-v2。
第一次运行只读导入旧 SummerAfterSchool 存档，原文件保留。
'@
[IO.File]::WriteAllText((Join-Path $taskGame '开始前请读.txt'), $taskReadme, [Text.UTF8Encoding]::new($true))
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $taskArchive) { Remove-Item -LiteralPath $taskArchive }
$taskZip = [IO.Compression.ZipFile]::Open($taskArchive, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($taskFile in Get-ChildItem -LiteralPath $taskStage -Recurse -File | Sort-Object FullName) {
        $taskEntry = $taskFile.FullName.Substring($taskStage.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskZip, $taskFile.FullName, $taskEntry, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $taskZip.Dispose() }
$taskHash = Get-FileHash -LiteralPath $taskArchive -Algorithm SHA256
$taskManifest = [PSCustomObject]@{
    version = '0.4.0'
    archive = $taskArchive
    bytes = (Get-Item -LiteralPath $taskArchive).Length
    sha256 = $taskHash.Hash
    packagedUtc = [DateTime]::UtcNow.ToString('o')
    excludes = @('Logs', 'QA saves', 'screenshots')
}
$taskManifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskProject 'Logs\release-v04.json') -Encoding UTF8
Write-Output ($taskManifest | ConvertTo-Json)
