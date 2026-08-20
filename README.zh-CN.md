# Bad Apple Progress Bars

[English](./README.md)

一个 Windows 视频播放器：它把视频帧压缩成低分辨率黑白画面，并使用大量真实的 WPF `ProgressBar` 控件播放。项目起初用于播放 Bad Apple!!，现在可处理任意 FFmpeg 能解码的视频。

播放器不会在播放过程中解码视频。首次使用会把视频烘焙为紧凑的 `.bpb` 数据流，写入按哈希识别的本地缓存；播放时只流式读取烘焙结果并更新固定的 WPF 控件池。

## 下载与运行

从 [Releases](../../releases) 下载 ZIP：

- `self-contained`：推荐，内含 .NET 运行时。
- `framework-dependent`：体积较小，需要预先安装 .NET 8 Windows Desktop Runtime。

解压后双击 `BadAppleProgressBars.exe`：

1. 选择视频文件或已烘焙的 `.bpb` 文件。
2. 选择进度条外观。
3. 视频首次打开会先烘焙再播放；之后若输入文件与渲染配置未变化，则直接复用缓存。

当前仅支持 **Windows x64**。

## 进度条外观

所有外观都使用真实的 WPF `ProgressBar` 控件：

- **Flat**：WPF Aero2 扁平轨道与填充。
- **Flat + stripes**：扁平 WPF 模板加 [Striped ProgressBar for WPF](https://gist.github.com/emoacht/febe527df16dd302d55f80921e044be0) 动态条纹层。
- **WPF Aero**：WPF 内置 Aero 轨道、边框、玻璃高光与填充边缘；该样式刻意不带条纹层。

主播放窗口的视觉树只包含 `Canvas` 和其中复用的 `ProgressBar`；文件与样式选择均在独立的启动对话框中完成。

## 命令行

命令行传入文件会跳过文件和样式选择窗口，默认使用 `aero`：

```powershell
BadAppleProgressBars.exe .\video.mp4
BadAppleProgressBars.exe --style striped .\video.mp4
BadAppleProgressBars.exe --style=flat .\video.bpb
```

`--style` 可用值：`flat`、`striped`、`aero`。

## 播放快捷键

- `Space`：暂停或继续。
- `R` / `Home`：重新播放。
- `Left` / `Right`：后退或前进一帧。

## 视频烘焙与缓存

输入视频时，随程序分发的 `ffmpeg.exe` 负责帧规范化；OpenCvSharp 负责灰度化和二值化。烘焙缓存位置：

```text
%LocalAppData%\BadAppleProgressBars\cache
```

缓存文件名包含源文件 SHA-256 与渲染配置哈希。视频文件、网格、阈值或烘焙算法配置发生变化时，会生成独立缓存。直接打开 `.bpb` 则只流式播放，不会重新烘焙。

当前默认渲染配置为 `80 × 45`、`30 FPS`、固定阈值。播放阶段不会逐帧创建或删除 ProgressBar，而是更新预先创建的固定控件池。

## 从源码构建

要求：

- Windows x64
- .NET 8 SDK
- 用于打包的、已审核 Windows x64 FFmpeg 发行包

运行测试：

```powershell
dotnet test .\BadAppleProgressBars.sln
```

运行开发版本：

```powershell
dotnet run --project .\src\BadAppleProgressBars\BadAppleProgressBars.csproj
```

使用已审核的 GPL FFmpeg 发行包根目录创建本地 ZIP：

```powershell
.\scripts\Publish-WinX64.ps1 -FfmpegRoot C:\ffmpeg -SelfContained true
.\scripts\Publish-WinX64.ps1 -FfmpegRoot C:\ffmpeg -SelfContained false
```

发布 ZIP 会携带 `ffmpeg/ffmpeg.exe`、其 `LICENSE`、`README.txt` 与 `THIRD_PARTY_NOTICES.md`。程序不会从用户的 `PATH` 中寻找 FFmpeg。

## CI 与发布

每次推送 `main` 都会运行测试，并生成两个 x64 工作流工件。推送严格符合 `vX.X.X.X` 的标签时，会自动创建或更新同名 GitHub Release，并附带两种 ZIP。

## 第三方声明

发布包包含 GPL FFmpeg 与 OpenCvSharp/OpenCV native 依赖。WPF 条纹层与 Aero 模板的声明记录在 [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md)。重新分发修改后的包前请先阅读该文件。

## 项目文档

- [项目方案](./docs/PROJECT_PLAN.md)
- [实施计划](./docs/IMPLEMENTATION_PLAN.md)
