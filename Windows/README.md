# DogSC Windows · 原版风格适配版

本版按仓库内的原版截图和 SwiftUI 源码重做，替换了第一版的简单录屏表单。默认入口为浅银色编辑器，新建录制打开独立浮条。原版 macOS 源码保留，Windows 实现在本目录。

## 运行

双击 `Windows/bin/DogSC.Windows.exe`。环境：Windows 10/11 x64、.NET Framework 4.8。保留 `Windows/Editor.xaml`、`Windows/EditorTheme.xaml`、`Windows/tools`、`Windows/bin` 的目录关系；原版字体位于仓库的 `Resources/Fonts`。无需管理员权限。

本次更新：统一编辑器、录制浮条和导出窗口样式；背景分类、壁纸／渐变缩略图、场景预设图卡、颜色胶囊、实际光标预览、贴图素材列表与参数分组；基础音频波形和画面／波形切换。右侧设置使用细滚动条，贴图与柔化按原版布局组织。

## 已实现

|部分|当前实现|
|---|---|
|界面|原版浅银／石墨配色、随附字体、左侧工具栏、中央预览、右侧属性面板、底部独立轨道；明暗切换|
|录制浮条|显示器、窗口、指定显示器上的区域框选；30／60／90 FPS 自选并记住设置，默认 60 FPS；3 秒倒计时；选择 DirectShow 麦克风；保存后进入编辑器|
|录制性能|优先 Windows 图形捕获与 NVIDIA NVENC 硬件编码；自动处理跨显卡传输与兼容回退，状态显示实际编码方式|
|备忘录|原版银白悬浮面板、编辑／阅读、字号 16–40、背景不透明度 55%–100%、自动保存、拖动与调整大小；录制排除与区域选择时收起|
|预览|播放／暂停、拖动定位、逐帧前后；暂停读取素材原始像素，播放按显示区域与缩放倍率解码，合成画布适配屏幕实际像素（含 DPI）；画面与导出共用合成逻辑|
|时间线|真实缩略图、分割、两端裁剪、删除、左右重排、0.5／1／1.5／2 倍速；撤销／重做|
|运镜|自动缩放轨道、鼠标跟随、手动缩放片段、范围／倍率／焦点／过渡时长编辑|
|独立光标|新录制单独保存鼠标轨迹；自动形态、箭头／手形／文本／十字／圆点等样式、隐藏、大小、移动平滑、静止隐藏、时间校准、摆动；涟漪／光晕／双环／微闪点击反馈|
|贴图|PNG 透明图片与 JPG；预览中拖动／缩放，位置、旋转、透明度、圆角、描边、阴影、层级；独立时间线；进入／退出动画、曲线和时长；背景虚化／隐藏录屏；图片嵌入项目|
|柔化|局部模糊和突出区域，绑定素材坐标并跟随运镜；位置、尺寸、强度／暗度、圆角、时间范围和进出过渡；独立时间线|
|画面|留白、位置、大小、圆角、描边、阴影、点阵／纯色／渐变／自定义图片背景，浅色／深色窗口外框|
|画布|适应素材、16:9、4:3、1:1、9:16，竖屏按短边分辨率导出|
|开场|淡入与轻微放大|
|音频|已有音轨跟随裁剪、重排和变速；音量设置；麦克风采集入口|
|导出|原版风格文件名、保存位置、预览与分辨率面板；MP4，720p／1080p／1440p／4K，30／60／90 FPS，默认选择与素材帧率接近的档位；进度与取消|
|项目|保存／重开 Windows `.dogscwin`，兼容上一版原型项目；项目引用原素材，不复制大视频|

快捷键：空格播放／暂停，方向键逐帧，Ctrl+B 分割，Delete 删除选中片段，Ctrl+Z 撤销，Ctrl+Y 重做，Ctrl+O 打开，Ctrl+S 保存。拖入视频或 Windows 项目也可打开。

录制素材存放在 `Windows/recordings/日期时间/`。项目引用原始视频和自定义背景路径，移动或删除素材会影响重开。导出使用新文件名，不覆盖已有视频。

左侧「光标」调整新录制的独立光标，编辑不会改写原始视频。旧录制和导入视频中的光标已成为画面像素，不能无损替换或隐藏，需重新录制。鼠标位置与点击按裁剪、重排和变速映射到编辑时间；光标时间偏差可用「光标延后 / 毫秒」校准。

左侧「贴图」→「导入贴图」添加图片；左侧「柔化」→「添加柔化区域」，可切换「柔化／突出」。拖动预览选框移动，右下角调整尺寸；时间线拖动整段或两端控制出现时间。多张贴图使用分层轨道，可上下滚动查看；「置底／下移／上移／置顶」改变叠放顺序。贴图原始图片直接嵌入 `.dogscwin`，删除外部图片后仍可重开，但录屏素材仍需保留。预览和导出共用光标、贴图与柔化合成代码。

录制浮条下方的「帧率」可选择 30、60、90 FPS；倒计时和录制期间锁定设置。最后的选择保存到 `%LOCALAPPDATA%/DogSC.Windows/recording-fps.txt`。鼠标采样最高 60 次／秒，独立于视频编码帧率；浮条状态每秒最多更新 4 次，避免高帧率文字刷新触发布局与阴影重绘。项目保留素材帧率，播放预览最高跟随到 90 FPS，导出也可选 90 FPS。该设置是目标帧率；实际独立画面的数量与屏幕刷新率、捕获速度、编码和渲染负载有关，高帧率文件可能包含重复画面，不能保证所有设备满帧。

录制优先使用 Windows 图形捕获（WGC）与 NVIDIA NVENC。启动前通过合成图像检测硬件编码；同显卡尝试直接传递 GPU 帧，不兼容时改用跨显卡传输。现代捕获失败时回退到 GDI；NVENC 不可用时回退到最多 4 个编码线程的 CPU 编码。录制进程使用较低调度优先级，兼容捕获队列从 1024 帧降至 4 帧。当前没有接入 AMD AMF／Intel QSV 自动选择。

录制状态显示「显卡编码」或「CPU 编码」，悬停可查看完整后端。每次录制目录里有 `capture-backend.txt` 和 `capture-performance.txt`，记录实际后端、目标帧率、重复／丢弃帧数与录制进程 CPU 用时。重复帧也可能只是屏幕内容未更新，不能单凭该数字认定卡顿。若后端启动失败，`capture-attempts.txt` 保留失败原因。

原始视频采用分段 MP4，每 0.5 秒提交一段；异常结束时，完整提交的片段通常仍可读取，未提交的尾段可能丢失。停止录制的并发请求共用一次保存任务，文件校验通过后才进入编辑器。编码器停止超时会终止进程并校验已提交的素材；失败时保留素材和诊断，不把失败当作成功。`capture-stop.txt` 记录停止过程，`capture-save-error.txt` 记录保存错误。已实测 30／60／90 FPS 录制、保存、重新打开和导出，以及强制中断编码后的素材解码；长时间与高负载稳定性仍需后续验证。

备忘录从录制浮条右侧便笺按钮，或编辑器的项目菜单 →「备忘录」打开。显示时不抢输入焦点；点击正文后可输入、粘贴和撤销，阅读模式仍能选中复制。拖动标题移动，拖动右下角改变大小；关闭按钮或 Esc 仅收起窗口。文稿、字号、背景不透明度和位置自动保存到 `%LOCALAPPDATA%/DogSC.Windows/memo.json`，独立于视频项目；输入停止 300 ms 后保存，收起、失去焦点和退出时立即保存。保存时保留上一份 `.bak`。

Windows 10 2004 及以上使用系统窗口捕获排除；本机已用实际 GDI 捕获验证：备忘录在本机可见，但不进入视频。系统不支持或排除设置失败时，开始录制自动收起，停止后恢复；框选区域时也暂时收起。不同系统／显示驱动、混合 DPI 尚未逐一验证。此机制与原版按录制应用过滤窗口的实现不同，Windows 的捕获排除也可能影响其他录屏工具。

## 与原版的差异

这是适配开发版，尚未完整移植所有功能：

- 屏幕 3D、摄像头、系统声音采集、录制暂停和标记尚未完成；相应入口禁用或显示说明。效果复制粘贴、高级音频编辑与单独音频导出也未完成。基础波形已实现。
- 新录制的原始视频不含光标，独立光标由编辑器合成。Windows 光标使用矢量形状绘制，自动检测箭头、文本、指示手、十字与禁止形态；部分手形与 macOS 原版素材有外观差异。移动平滑与摆动是 Windows 实现，并非原版完整光标运动引擎的逐行移植。
- 贴图与柔化参照原版默认参数、坐标体系、层级、动画曲线和合成顺序。尚未移植相邻贴图的复杂衔接过渡和摄像头联动；模糊使用 CPU 三次盒式近似高斯，阴影与光晕使用 Windows 绘制，细节可能与 Core Image 不同。复杂模糊和多层大图会增加预览／导出的计算量。
- 自动缩放使用原版默认分组参数与 `springProgress` 弹簧公式；跟随和部分分组行为仍是 Windows 实现，并非整个 RecorderCore 的逐行等价移植。
- 不兼容 macOS `.dogscproject`；Windows 项目格式独立。
- 手动缩放按编辑时间保存。修改录屏片段范围或顺序时，自动轨道重新生成；手动轨道保留时间位置并裁到总时长以内。
- 录制使用 WGC／GDI 与 FFmpeg，目标帧率可选 30／60／90 FPS。高清预览比旧版固定 800／960 像素的预览消耗更多 CPU；低配置、高分辨率或复杂项目下可能掉帧。暂停后恢复原始素材细节，最大化／调整窗口后重新生成清晰预览。鼠标采样扣除首段编码进度估算的启动延迟，仍未实现原版完整硬件时间戳同步。
- 麦克风枚举与采集已接入，但未在用户的实际麦克风上采集声音测试。预览音频通过 Windows MediaPlayer 播放，高负载下可能与 CPU 视频预览有偏差；导出使用独立音视频时间线。
- 尚未验证长时间录制、HDR、受保护窗口、混合 DPI、休眠恢复与高负载 4K 稳定性。无签名或安装器。

## 原版参考

- `docs/images/editor.png`、`recorder.png`、`export.png`、`motion.png`
- `Sources/DogSCApp/EditorDesignTokens.swift`：配色、字体层级和圆角
- `Sources/DogSCApp/EditorWorkspaceLayout.swift`、`EditorWorkspaceSurface.swift`：布局
- `Sources/DogSCApp/RecorderSurfaceStyle.swift`：录制浮条
- `Sources/DogSCApp/RecorderMemo.swift`：备忘录布局、交互、字号／不透明度和持久化行为
- `Sources/RecorderCore/AutoZoomPlanner.swift`：点击分组、提前量和保持段
- `Sources/RecorderCore/ZoomAnimation.swift`：弹簧公式
- `Sources/RecorderCore/ProjectModel.swift`：默认参数
- `Sources/RecorderCore/OverlayTimeline.swift`、`ElementMotion.swift`：贴图／柔化模型、默认值与动画曲线
- `Sources/RecorderCore/CursorRenderGeometry.swift`、`Sources/DogSCApp/SharedFrameOverlayRenderer.swift`：点击反馈几何与叠加合成
- `Sources/DogSCApp/EditorOverlayInspector.swift`：效果属性与层级操作

Windows 主要代码：`Editor.xaml`（界面）、`EditorApp.cs`（交互）、`EditorCore.cs`（模型／运镜／共享合成和导出）、`EditorWindows.cs`（录制、导出、测试入口）、`RecorderMemo.cs`（备忘录与专用验证）、`CaptureEngine.cs`（硬件捕获／编码、回退和性能验证）、`EditorEffects.cs`（独立光标／贴图／柔化模型与渲染）、`EditorEffectsUI.cs`（效果属性、分层轨道与画布交互）。`DogSC.Windows.cs` 保留前版 FFmpeg／区域选择等基础实现，旧表单不再是入口。

## 构建与验证

```powershell
# tools 已存在时不必重复下载
powershell -NoProfile -ExecutionPolicy Bypass -File .\Windows\setup-tools.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Windows\build.ps1

# 剪辑、重排、变速、运镜、音轨、取消、竖屏、1080p60
Start-Process .\Windows\bin\DogSC.Windows.exe -ArgumentList '--editor-test' -WindowStyle Hidden -Wait
Get-Content .\Windows\bin\editor-test\results.txt

# 2560px 细线素材缩放清晰度、实际 UI 命令、播放与独立测试窗口录制
Start-Process .\Windows\bin\DogSC.Windows.exe -ArgumentList '--ui-test' -WindowStyle Hidden -Wait
Get-Content .\Windows\bin\editor-test\interface-results.txt
Get-Content .\Windows\bin\editor-test\preview-quality-results.txt

# 备忘录保存／恢复／阅读／字号／透明度／区域收起／实际捕获排除
Start-Process .\Windows\bin\DogSC.Windows.exe -ArgumentList '--memo-test' -WindowStyle Hidden -Wait
Get-Content .\Windows\bin\memo-results.txt

# 三个档位的实际窗口录制、项目帧率保存和 720p 成片导出
Start-Process .\Windows\bin\DogSC.Windows.exe -ArgumentList '--fps-test' -WindowStyle Hidden -Wait
Get-Content .\Windows\bin\fps-results.txt

# 独立光标、剪辑／变速后的轨迹、透明贴图、层级、柔化、项目嵌入图片、实际成片及 UI
Start-Process .\Windows\bin\DogSC.Windows.exe -ArgumentList '--effects-test' -WindowStyle Hidden -Wait
Get-Content .\Windows\bin\effects-results.txt

# 大尺寸合成窗口：旧／新录制进程 CPU 用时、分辨率与屏幕区域坐标
Start-Process .\Windows\bin\DogSC.Windows.exe -ArgumentList '--capture-perf-test' -WindowStyle Hidden -Wait
Get-Content .\Windows\bin\capture-performance-results.txt
```

测试视频由仓库自带 `Resources/Onboarding/SoftLight.png` 生成。录制验证只捕获独立文字测试窗口，不录制桌面或麦克风。界面图为 `Windows/bin/editor-preview.png`、`recorder-preview.png`、`export-preview-v2.png`。

备忘录测试使用 `Windows/bin/memo-test/随机目录/` 中的独立文稿，不改动真实文稿。捕获测试仅框住自建不透明测试窗口，先确认未排除的备忘录能被录到，再验证排除后的像素与背景一致。预览图为 `Windows/bin/memo-preview.png`；捕获结果位于测试目录的 `capture-results.txt`。

代码沿用原项目 MIT License，保留神烦老狗的版权声明。字体遵循 `Resources/Fonts/LICENSE.txt`；FFmpeg 及其包含组件遵循自身许可，见 `Windows/tools/FFmpeg-LICENSE.txt` 与保留的构建压缩包。这不是原作者官方 Windows 发行版。
