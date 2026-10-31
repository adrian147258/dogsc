param([string]$Compiler = '', [switch]$TestBuild)
$ErrorActionPreference = 'Stop'
if (-not $Compiler) {
    $candidates = @((Join-Path $PSScriptRoot 'tools/inno-portable/ISCC.exe'), (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'), (Join-Path $env:ProgramFiles 'Inno Setup 6/ISCC.exe'))
    $Compiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $Compiler -or -not (Test-Path -LiteralPath $Compiler)) { throw '请安装 Inno Setup 6，或使用 -Compiler 指定 ISCC.exe。' }
$tag = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'version.txt') -Raw).Trim()
if ($tag -notmatch '^v\d+\.\d+\.\d+-windows(?:-beta\.\d+)?$') { throw '版本号无效。' }
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'package.ps1')
if ($LASTEXITCODE -ne 0) { throw '打包失败。' }
$packageRoot = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'share/latest-package-path.txt') -Raw).Trim()
$compilerArgs = @('/Qp', "/DPackageRoot=$packageRoot", "/DReleaseTag=$tag")
if ($TestBuild) { $compilerArgs += @('/DTestBuild=1', "/O$(Join-Path (Split-Path $packageRoot) 'installer-output-test')") }
& $Compiler @compilerArgs (Join-Path $PSScriptRoot 'installer.iss')
if ($LASTEXITCODE -ne 0) { throw '安装程序构建失败。' }
$outputName = if ($TestBuild) { 'installer-output-test' } else { 'installer-output' }
$installer = Join-Path (Split-Path $packageRoot) "$outputName/dogsc-$tag-setup-x64.exe"
if (-not (Test-Path -LiteralPath $installer)) { throw '找不到生成的安装程序。' }
Write-Output "Installer: $installer"
