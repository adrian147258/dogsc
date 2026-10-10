$ErrorActionPreference = 'Stop'
$toolsDir = Join-Path $PSScriptRoot 'tools'
New-Item -ItemType Directory -Force $toolsDir | Out-Null
$archive = Join-Path $toolsDir 'ffmpeg-release-essentials.zip'
$baseUri = 'https://github.com/GyanD/codexffmpeg/releases/download/9.0.2/ffmpeg-9.0.2-essentials_build.zip'
$expectedHash = '60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba'
if (-not (Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) {
    Invoke-WebRequest -Uri $baseUri -OutFile $archive
}
$actualHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
if ($expectedHash -notmatch '^[0-9a-fA-F]{64}$' -or $actualHash -ne $expectedHash) { throw 'FFmpeg SHA256 verification failed.' }
$extractDir = Join-Path $toolsDir 'extracted'
Expand-Archive -LiteralPath $archive -DestinationPath $extractDir -Force
$binaryDir = Get-ChildItem -LiteralPath $extractDir -Filter ffmpeg.exe -Recurse | Select-Object -First 1 -ExpandProperty DirectoryName
if (-not $binaryDir) { throw 'FFmpeg not found in downloaded archive.' }
Copy-Item -LiteralPath (Join-Path $binaryDir 'ffmpeg.exe') -Destination $toolsDir
Copy-Item -LiteralPath (Join-Path $binaryDir 'ffprobe.exe') -Destination $toolsDir
$licenseFile = Join-Path (Split-Path $binaryDir) 'LICENSE'
if (Test-Path -LiteralPath $licenseFile) { Copy-Item -LiteralPath $licenseFile -Destination (Join-Path $toolsDir 'FFmpeg-LICENSE.txt') }
$readmeFile = Join-Path (Split-Path $binaryDir) 'README.txt'
if (Test-Path -LiteralPath $readmeFile) { Copy-Item -LiteralPath $readmeFile -Destination (Join-Path $toolsDir 'FFmpeg-README.txt') }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../docs/third-party/FFmpeg-SOURCE.txt') -Destination (Join-Path $toolsDir 'FFmpeg-SOURCE.txt')
Write-Output "FFmpeg ready. SHA256: $actualHash"
