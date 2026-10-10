$ErrorActionPreference = 'Stop'
$shareRoot = Join-Path $PSScriptRoot 'share'
$packageName = 'DogSC-Windows-Trial-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
$packageRoot = Join-Path $shareRoot $packageName
$zipPath = "$packageRoot.zip"
if (Test-Path -LiteralPath $packageRoot) { throw '同名体验包已存在，请稍后重试。' }

# Explicit file list excludes recordings, projects, tests and personal settings.
$files = @{
    'bin/DogSC.Windows.exe' = 'Windows/bin/DogSC.Windows.exe'
    'bin/DogSC.Windows.exe.config' = 'Windows/bin/DogSC.Windows.exe.config'
    'Editor.xaml' = 'Windows/Editor.xaml'
    'EditorTheme.xaml' = 'Windows/EditorTheme.xaml'
    'README.md' = 'Windows/README.md'
    'Trial-README.txt' = 'README.txt'
    'tools/ffmpeg.exe' = 'Windows/tools/ffmpeg.exe'
    'tools/ffprobe.exe' = 'Windows/tools/ffprobe.exe'
    'tools/FFmpeg-LICENSE.txt' = 'Windows/tools/FFmpeg-LICENSE.txt'
    'tools/FFmpeg-README.txt' = 'Windows/tools/FFmpeg-README.txt'
    '../docs/third-party/FFmpeg-SOURCE.txt' = 'Windows/tools/FFmpeg-SOURCE.txt'
    '../THIRD_PARTY_NOTICES.md' = 'THIRD_PARTY_NOTICES.md'
    '../LICENSE' = 'LICENSE'
    '../Resources/Onboarding/SoftLight.png' = 'Resources/Onboarding/SoftLight.png'
    '../Resources/Onboarding/SOURCE.json' = 'Resources/Onboarding/SOURCE.json'
}
foreach ($entry in $files.GetEnumerator()) {
    $source = Join-Path $PSScriptRoot $entry.Key
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "缺少打包文件：$source" }
}
New-Item -ItemType Directory -Path $packageRoot | Out-Null
foreach ($entry in $files.GetEnumerator()) {
    $destination = Join-Path $packageRoot $entry.Value
    New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $entry.Key) -Destination $destination
}
$fontDestination = Join-Path $packageRoot 'Resources/Fonts'
New-Item -ItemType Directory -Force -Path $fontDestination | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '../Resources/Fonts') -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $fontDestination
}
@'
@echo off
start "" "%~dp0Windows\bin\DogSC.Windows.exe"
'@ | Set-Content -LiteralPath (Join-Path $packageRoot 'Start-DogSC.cmd') -Encoding ASCII
Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal
$packageRoot | Set-Content -LiteralPath (Join-Path $shareRoot 'latest-package-path.txt') -Encoding UTF8
Write-Output "Package: $zipPath"
