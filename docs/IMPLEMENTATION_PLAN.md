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
    FFmpeg 视频烘焙器
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

    for each block in current frame:
        bar = pool[blockIndex]
        Canvas.Left = block.StartX × cellWidth
        Canvas.Top = row × cellHeight
        Width = block.Length × cellWidth
        Height = cellHeight
        Maximum = block.Length
        Value = block.BlackPrefixLength
        Visibility = Visible

    for each unused pool item:
        Visibility = Hidden

第一版可以按行顺序分配控件，不立即引入跨帧 ProgressBarId 匹配。控件池正确工作后，再接入轨道复用和差分更新。

### 验收标准

- BWBW 显示为两个真实的 ProgressBar。
- WBW 显示为 [W] 和 [BW] 两个逻辑块。
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
    FFmpeg.AutoGen 打开输入
        ↓
    选择视频流
        ↓
    读取 PTS 和 time base
        ↓
    解码 AVFrame
        ↓
    缩放到 80 × 45
        ↓
    灰度化
        ↓
    二值化
        ↓
    每行切分 B*W* 块
        ↓
    写入 .bpb

### 处理要求

- 解码阶段使用复用的 packet/frame。
- 不在每帧创建大量托管集合。
- 记录原视频帧率和 PTS。
- 正确处理视频结束和解码器 flush。
- 烘焙取消时释放 FFmpeg 和 OpenCV 资源。
- 烘焙进度按已处理帧数显示。

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
        + VideoProfile hash
        + AlgorithmVersion
        ↓
    cache key

缓存匹配必须同时验证：

- 源视频哈希。
- 网格尺寸。
- 阈值配置。
- 黑白反转配置。
- 裁剪/留白配置。
- 烘焙格式版本。

### UI 控件

按以下顺序实现：

1. 打开文件。
2. 烘焙进度。
3. 播放/暂停。
4. 停止。
5. 当前帧和总帧数。
6. 跳转。
7. 网格和阈值配置。

## 12. 性能验收

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

## 13. 代码质量要求

- 公共领域类型使用明确的不可变数据结构或只读字段。
- 文件格式读写代码必须检查边界、版本和长度。
- 所有 IDisposable 的 FFmpeg/OpenCV 资源必须有明确的释放路径。
- 不在 WPF 窗口代码中直接实现视频解码。
- 不使用全局可变状态保存当前视频。
- 单元测试优先覆盖算法和文件格式，UI 测试覆盖关键交互。
- 每完成一个阶段再提交一次可运行状态。

## 14. 下一步执行项

下一次实施从阶段一开始：

1. 创建 WPF 项目和测试项目。
2. 创建 Canvas 和基础窗口。
3. 实现 MonotonicBlock 和 RowBlockEncoder。
4. 为 BWBW、WBW、BBB 和 WBBBWBWWWBBWWB 编写单元测试。
5. 创建最小 ProgressBar 控件池。
6. 使用合成行数据验证实际 WPF 控件显示。

阶段一和阶段二完成前，不接入 FFmpeg，避免同时引入视频解码、缓存和 UI 问题。

## 15. 关联文档

- [项目目标与总体方案](./PROJECT_PLAN.md)

