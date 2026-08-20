# Bad Apple Progress Bars 实施计划

本文档是 PROJECT_PLAN.md 的工程实施版本，用于指导代码开发、测试和验收。

## 1. 实施边界

### 必须遵守的设计决定

- 使用纯 C# 项目代码。
- 使用 WPF 作为窗口和 UI 框架。
- 使用官方 WPF ProgressBar 控件，不使用自绘矩形替代进度条。
- 使用 Canvas 承载和定位多个 ProgressBar。
- ProgressBar.Foreground 保留 WPF 默认颜色。
- 视频分析在播放前烘焙完成。
- 播放时从 .bpb 文件流式读取，不重新解码视频。
- 首版网格尺寸为 80 × 45。
- 首版先支持无音频的视频画面，音频属于后续扩展。

### 首版暂不实现

- Win32 HWND、HwndHost 和 PInvoke 控件承载。
- 自定义 ProgressBar 控件实现。
- GPU 专用渲染。
- 音频同步播放。
- 在线视频和摄像头输入。
- 复杂的连续插值动画。

## 2. 实施顺序

    项目骨架
        ↓
    单行 B*W* 切分算法
        ↓
    WPF ProgressBar 控件池
        ↓
    合成帧播放原型
        ↓
    .bpb 烘焙文件读写
        ↓
    .bpb 流式播放器
        ↓
    内置 FFmpeg 视频烘焙器
        ↓
    哈希缓存和参数配置
        ↓
    性能测试和通用视频支持

每一步完成对应的验收标准后，再进入下一步。

## 3. 目标目录结构

首版代码按功能拆分，避免把解码、烘焙、格式和 UI 混在窗口代码中。

    src/
    └── BadAppleProgressBars/
        ├── BadAppleProgressBars.csproj
        ├── App.xaml
        ├── App.xaml.cs
        ├── MainWindow.xaml
        ├── MainWindow.xaml.cs
        │
        ├── Domain/
        │   ├── BinaryPixel.cs
        │   ├── MonotonicBlock.cs
        │   ├── BarKeyframe.cs
        │   ├── ProgressBarTrack.cs
        │   ├── VideoProfile.cs
        │   └── BakedVideoMetadata.cs
        │
        ├── Segmentation/
        │   ├── RowBlockEncoder.cs
        │   └── TrackMatcher.cs
        │
        ├── Rendering/
        │   ├── ProgressBarPool.cs
        │   ├── ProgressBarSurface.xaml
        │   └── ProgressBarSurface.xaml.cs
        │
        ├── Baking/
        │   ├── IBakedVideoWriter.cs
        │   ├── BakedVideoWriter.cs
        │   ├── BakedVideoReader.cs
        │   └── BakeCoordinator.cs
        │
        ├── Playback/
        │   ├── PlaybackClock.cs
        │   ├── PlaybackSession.cs
        │   └── PlaybackEventApplier.cs
        │
        └── Infrastructure/
            ├── HashService.cs
            ├── BoundedEventQueue.cs
            └── NativeDependencyProbe.cs

    tests/
    └── BadAppleProgressBars.Tests/
        ├── RowBlockEncoderTests.cs
        ├── TrackMatcherTests.cs
        ├── BakedVideoFormatTests.cs
        └── HashServiceTests.cs

实际目录可以随着实现调整，但功能边界应保持不变。

## 4. 阶段一：创建项目骨架

### 目标

创建能够启动的 .NET WPF 项目，并准备后续测试项目。

### 实施内容

- 创建 net8.0-windows WPF 项目。
- 配置 win-x64 发布目标。
- 创建主窗口和基础布局。
- 添加一个用于显示内容的 Canvas。
- 添加播放状态栏，但先不接入真实视频。
- 添加单元测试项目。
- 配置 Debug 和 Release 构建。

### 验收标准

- dotnet build 成功。
- WPF 窗口可以启动和关闭。
- 工作区没有由构建产生的源码外临时文件。
- 测试项目可以被测试运行器发现。

## 5. 阶段二：实现单行单调块切分

### 目标

把一行黑白像素切分为最少数量的 B*W* 块。

### 数据结构

    public readonly record struct MonotonicBlock(
        int StartX,
        int Length,
        int BlackPrefixLength);

其中：

- StartX：块在网格行中的起点。
- Length：块的总长度。
- BlackPrefixLength：块左侧连续黑色前缀的长度。

对应 WPF ProgressBar：

    Maximum = Length
    Value   = BlackPrefixLength
    Width   = Length × CellWidth

### 算法

只在 W→B 转换处切分：

    start = 0

    for x = 1 .. width - 1:
        if pixel[x - 1] == W and pixel[x] == B:
            emit block(start, x - start)
            start = x

    emit block(start, width - start)

### 必须测试的输入

    BWBW                 → [BW][BW]
    WBW                  → [W][BW]
    BBB                  → [BBB]
    WBBBWBWWWBBWWB       → [W][BBBW][BWWW][BBWW][B]
    WWWW                 → [WWWW]
    BBBB                 → [BBBB]
    BW                   → [BW]
    WB                   → [W][B]

### 验收标准

- 所有输出块的黑色前缀和白色后缀均满足 B*W*。
- 所有块拼接后与输入行完全一致。
- 输出块数量等于 1 + W→B 转换次数。
- 算法不修改输入像素。

## 6. 阶段三：实现 WPF ProgressBar 控件池

### 目标

将单调块显示为真正的 WPF ProgressBar 控件。

### 控件布局

    ProgressBarSurface
    └── Canvas
        └── ProgressBarPool
            ├── ProgressBar
            ├── ProgressBar
            └── ...

每个控件使用官方 ProgressBar，不使用矩形自绘。

### 控件初始化

控件池大小按网格上限计算：

    poolSize = height × ceil(width / 2)

创建时设置：

    Minimum = 0
    Maximum = 1
    Value   = 0
    Visibility = Hidden

Foreground 不主动设置，保留 WPF 默认值。

### 每帧应用状态

    collect every W→B block start column from all rows
    insert one shared 2 DIPs horizontal gap at each distinct column
    cellWidth = (Canvas.Width - gapCount × 2) / gridWidth
    cellHeight = (Canvas.Height - (gridHeight - 1) × 2) / gridHeight

    for each block in current frame:
        bar = pool[blockIndex]
        Canvas.Left = mapped block start (after its own gap)
        Canvas.Top = row × (cellHeight + 2)
        Width = mapped block end - mapped block start
        Height = cellHeight
        Maximum = Width
        Value = mapped black-prefix length
        Visibility = Visible

    for each unused pool item:
        Visibility = Hidden

第一版可以按行顺序分配控件，不立即引入跨帧 ProgressBarId 匹配。控件池正确工作后，再接入轨道复用和差分更新。

### 验收标准

- BWBW 显示为两个真实的 ProgressBar。
- WBW 显示为 [W] 和 [BW] 两个逻辑块。
- 每个不同 ProgressBar 之间保留 2 DIPs 间隙；跨过其他行间隙列的 ProgressBar 宽度和 Value 会增加 `2 × n`，且所有行列对齐。
- 控件前景色仍为 WPF 默认颜色。
- 播放帧切换时不调用 Canvas.Children.Add 或 Canvas.Children.Remove。
- 控件数量只在网格尺寸改变时重新创建。

## 7. 阶段四：合成帧播放原型

### 目标

不依赖视频文件，使用内存中的测试帧验证播放时钟、控件复用和暂停逻辑。

### 测试输入

创建一个合成动画：

    第 0 帧：BWBW
    第 1 帧：WBW
    第 2 帧：BBB
    第 3 帧：WBBBWBWWWBBWWB

扩展测试应包含：

- 全白帧。
- 全黑帧。
- 每帧区段数量完全不同。
- 区段分裂。
- 区段合并。
- 连续多帧完全不变。

### 播放时钟

- 使用 Stopwatch 或等效单调时钟。
- 根据帧时间戳决定何时应用帧。
- 不使用固定 Thread.Sleep(33) 作为同步依据。
- 暂停时冻结播放时钟。
- 继续时从暂停时间恢复。

### 验收标准

- 合成动画按帧顺序显示。
- 暂停后控件状态不继续变化。
- 继续后不会从头播放。
- 播放结束后停在最后一帧。
- 运行期间不会持续创建 WPF 控件。

## 8. 阶段五：实现 .bpb 文件格式

### 目标

把合成动画保存为可流式读取的二进制烘焙文件。

### 第一版格式

    Header
    ├── Magic: BPB1
    ├── FormatVersion
    ├── SourceHash[32]
    ├── ProfileHash[32]
    ├── Width
    ├── Height
    ├── FrameRateNumerator
    ├── FrameRateDenominator
    ├── FrameCount
    └── IndexOffset

    Frame/Event Blocks
    ├── BlockStartFrame
    ├── BlockFrameCount
    ├── UncompressedLength
    ├── CompressedLength
    └── Payload

    Index
    └── FrameStart → FileOffset

第一版可以先不压缩 payload，但字段要保留，以便后续加入分块压缩。

### 事件模型

第一版优先保证正确性，使用显式状态事件：

    BarState
    ├── SlotId
    ├── Visible
    ├── Row
    ├── StartX
    ├── Length
    ├── Maximum
    └── Value

后续再把连续不变状态压缩成 Track、StartFrame、Duration 和 Keyframes。

### 验收标准

- 写入后可以完整读回 Header。
- 写入后可以逐帧还原全部 BarState。
- 文件截断或 Magic 错误时返回明确错误。
- 不需要把整个 .bpb 文件读入内存。
- 同一测试动画写入并读回后，帧状态逐项一致。

## 9. 阶段六：实现 .bpb 流式播放器

### 目标

播放阶段只读取烘焙文件，不接触原始视频。

### 线程模型

    文件读取线程
        ↓
    有限容量事件队列
        ↓
    WPF Dispatcher/UI 线程
        ↓
    ProgressBar 控件池

读取线程负责：

- 顺序读取事件块。
- 必要时解压事件块。
- 将事件块放入有限容量队列。

UI 线程负责：

- 根据播放时钟取出到期事件。
- 更新 ProgressBar 属性。
- 隐藏未使用控件。

### 跳转

- 找到不晚于目标帧的最近 Keyframe Block。
- 清空当前控件状态。
- 应用完整状态快照。
- 顺序应用后续事件直到目标帧。

### 验收标准

- 播放结果与内存合成播放一致。
- 播放阶段不加载原始视频。
- 队列有固定上限，不会无限增长。
- 跳转后画面状态正确。
- 缓存文件读取失败时能显示可理解的错误信息。

## 10. 阶段七：接入 FFmpeg 烘焙器

### 目标

将真实视频转换为 .bpb 文件。

### 处理流程

    视频路径
        ↓
    随程序发布的 ffmpeg.exe
        ↓
    选择视频流、固定 30 FPS、缩放/留白到 80 × 45
        ↓
    通过 stdout 输出 bgr24 rawvideo 临时帧流
        ↓
    OpenCvSharp 灰度化与二值化
        ↓
    二值化
        ↓
    每行切分 B*W* 块
        ↓
    写入 .bpb

### 处理要求

- `ffmpeg.exe` 从发布目录启动，不依赖用户 PATH 或系统安装。
- 首版固定使用 `fps=30`，从而以稳定的帧时钟播放。
- 原始 BGR 帧先写入临时文件，再由 OpenCvSharp 逐帧灰度化、二值化并转换为 `.bpb`，不能将全部帧保留在托管内存。
- 烘焙取消时终止 FFmpeg、释放管道并删除临时文件和半成品 `.bpb`。
- 烘焙进度按已写入帧数显示；首版以窗口标题提示，保持窗口内容只有 Canvas 和 ProgressBar。
- 发布包须附带 FFmpeg 构建的许可证、来源、版本和源代码获取说明。

### 验收标准

- 能够烘焙 Bad Apple 视频。
- 烘焙文件可以由阶段六播放器播放。
- 播放帧率与源视频时间基本一致。
- 烘焙错误不会导致 UI 无响应。
- 烘焙取消后不会留下半成品缓存作为有效文件。

## 11. 阶段八：哈希缓存和播放控制

### 缓存流程

    source file
        ↓
    SHA-256
        + render profile hash
          (grid, FPS, threshold, invert, letterbox, algorithm, .bpb version)
        ↓
    %LocalAppData%\BadAppleProgressBars\cache\<source>_<profile>.bpb

缓存匹配必须同时验证：

- 源视频哈希。
- 渲染配置哈希，其中包含网格尺寸、FPS、阈值、黑白反转、裁剪/留白规则、烘焙算法版本和 `.bpb` 格式版本。
- `.bpb` 文件头中的源视频哈希和渲染配置哈希。

命中时直接将缓存文件交给流式播放器；文件缺失、格式错误、哈希不一致或读取失败时重新烘焙。烘焙器先写入同目录临时文件，成功后才替换正式缓存文件。

### 交互边界

窗口的可视内容继续严格保持为 `Canvas` 和其内的官方 `ProgressBar`，不在 WPF 视觉树加入按钮、文本或状态栏。首版保留键盘播放控制，烘焙进度、缓存命中和失败信息只写入窗口标题。打开文件和可视化配置面板属于后续阶段，必须在不破坏此视觉树约束的前提下另行设计。

### 验收标准

- 首次传入视频文件会生成 `.bpb` 并播放。
- 再次传入内容和渲染配置相同的文件时不启动 FFmpeg，标题显示缓存命中。
- 改变阈值、反转、网格、FPS、留白规则、算法或格式版本时不会误用旧缓存。
- 损坏或截断的缓存不会播放，程序会重新烘焙。

## 12. 阶段九：GitHub 构建与发布

### 目标

在 `main` 的每次提交上验证构建，在合法版本标签上生成 GitHub Release。首版只发布 `win-x64`，因为当前 OpenCvSharp Windows runtime 不再支持 x86。

### 后续优化候选项（不属于本阶段）

当前 `OpenCvSharp4.Windows` 发布输出包含 `opencv_videoio_ffmpeg*.dll`。当前代码只使用 OpenCV 做图像处理，视频解码仍由随包 `ffmpeg.exe` 完成，因此该 DLL 不替代 FFmpeg CLI。后续可单独验证切换到 `OpenCvSharp4.Windows.Slim` 是否能去除未使用的 videoio 依赖；在全部自动测试、真实视频烘焙/缓存回放、两种 x64 包启动和许可证复核完成前，不实施该切换。

### 工作流

- `main` 提交：执行测试和两个并行 x64 打包任务，但不创建 Release。
- 标签 `vX.X.X.X`：执行相同构建，并在两个包都成功后创建或更新同名 GitHub Release。
- 其他格式的标签失败并提示版本格式要求。
- CI 将标签去掉 `v` 后写入程序集/文件/信息版本；提交构建使用 `0.0.0.<GitHub run number>`。
- 提交工件命名为 `BadAppleProgressBars-sha-<short-sha>-win-x64-<flavor>.zip`；标签工件命名为 `BadAppleProgressBars-vX.X.X.X-win-x64-<flavor>.zip`。
- 两种 `flavor` 为 `framework-dependent`（已安装 .NET 8 Desktop Runtime）和 `self-contained`（无需用户预装 .NET）。
- CI 从锁定版本的 Gyan FFmpeg 发布包下载，并验证 `ffmpeg.exe` SHA-256 后才打包。

### 验收标准

- 推送到 `main` 后可在 Actions 下载两个 ZIP 工件。
- 推送合法 `vX.X.X.X` 标签后，Release 附带两个 ZIP。
- 两个 ZIP 都携带 `ffmpeg/ffmpeg.exe`、其 `LICENSE`、`README.txt` 和 `THIRD_PARTY_NOTICES.md`。
- `framework-dependent` 包可在具有 .NET 8 Desktop Runtime 的 x64 Windows 上启动；`self-contained` 包可在没有预装 .NET 的 x64 Windows 上启动。

## 13. 性能验收

### 播放阶段指标

- 首版目标：80 × 45、30 FPS 连续播放。
- 不能在播放时创建或删除 ProgressBar。
- 事件队列有固定容量。
- 播放阶段不读取原始视频。
- 长时间播放不出现持续增长的托管内存。
- 统计每秒实际更新控件数、帧延迟和丢帧数。

### 对比测试

至少测试以下网格：

    80 × 45
    120 × 68
    160 × 90

每种网格记录：

- 初始化时间。
- ProgressBar 控件数量。
- 播放 FPS。
- 平均帧延迟。
- 峰值托管内存。
- GC 次数和暂停时间。
- 烘焙文件大小。

## 14. 代码质量要求

- 公共领域类型使用明确的不可变数据结构或只读字段。
- 文件格式读写代码必须检查边界、版本和长度。
- 所有 IDisposable 的 FFmpeg/OpenCV 资源必须有明确的释放路径。
- 不在 WPF 窗口代码中直接实现视频解码。
- 不使用全局可变状态保存当前视频。
- 单元测试优先覆盖算法和文件格式，UI 测试覆盖关键交互。
- 每完成一个阶段再提交一次可运行状态。

## 15. 下一步执行项

下一次实施从阶段八开始：

1. 实现哈希缓存命中和损坏缓存回退测试。
2. 保持窗口视觉树只有 Canvas 与官方 ProgressBar。
3. 添加 GitHub Actions 的 x64 双包构建与版本标签发布。
4. 在真实视频上测量首次烘焙与缓存命中的时间和内存。

阶段八完成后，再进入通用视频配置与性能测量，不在播放路径重新引入 FFmpeg 或 OpenCV。

## 16. 关联文档

- [项目目标与总体方案](./PROJECT_PLAN.md)
