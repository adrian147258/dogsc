$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$out = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $out | Out-Null
$wpf = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/WPF'
$polish = @((Join-Path $PSScriptRoot 'EditorPolish.cs'), (Join-Path $PSScriptRoot 'EditorPolishWindows.cs'), (Join-Path $PSScriptRoot 'EditorTimelineAppearance.cs'), (Join-Path $PSScriptRoot 'EditorInspectorAppearance.cs'))
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output /main:DogSC.Replica.ReplicaProgram "/out:$out/DogSC.Windows.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Xaml.dll "/r:$wpf/WindowsBase.dll" "/r:$wpf/PresentationCore.dll" "/r:$wpf/PresentationFramework.dll" (Join-Path $PSScriptRoot 'DogSC.Windows.cs') (Join-Path $PSScriptRoot 'EditorCore.cs') (Join-Path $PSScriptRoot 'EditorApp.cs') (Join-Path $PSScriptRoot 'EditorWindows.cs') (Join-Path $PSScriptRoot 'RecorderMemo.cs') (Join-Path $PSScriptRoot 'CaptureEngine.cs') (Join-Path $PSScriptRoot 'EditorEffects.cs') (Join-Path $PSScriptRoot 'EditorEffectsUI.cs') (Join-Path $PSScriptRoot 'EffectsTests.cs') (Join-Path $PSScriptRoot 'EffectsInterfaceTests.cs') $polish
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../LICENSE') -Destination $out
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DogSC.Windows.exe.config') -Destination $out
Write-Output "Built: $out/DogSC.Windows.exe"
