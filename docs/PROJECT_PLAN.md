# Bad Apple Progress Bars 项目目标与计划

## 1. 项目概述

本项目是一个 Windows 桌面视频播放器，将视频帧转换为低分辨率的黑白图像，并使用多个官方 WPF `ProgressBar` 控件进行显示。

项目首先用于播放 `Bad Apple!!`，后续扩展为支持任意 FFmpeg 可解码的视频文件。

最终画面效果为：

- 白色背景代表视频中的白色区域。
- 黑色区段由 WPF 官方 `ProgressBar` 控件显示。
- 每一行允许存在多个互不连续的进度条区段。
- `ProgressBar.Foreground` 保留 WPF 原本的颜色，不强制改为黑色。
- 视频画面按帧号和时间戳同步播放。

## 2. 可行性结论

项目可行。

一个 `ProgressBar` 可以表示一段“黑色前缀 + 白色后缀”，也就是 `B*W*` 形式的序列。它不只能表示纯黑色区段。

因此，单行画面应在每个 `W→B` 转换处切分。每个切分块内部最多出现一次 `B→W` 转换，可以由一个 `ProgressBar` 表示。原来的“每个连续黑色区段对应一个进度条”表述不准确，已改为“每个单调块对应一个进度条”。

本项目不使用自绘矩形替代进度条，也不使用 Win32 `HWND` 或 `HwndHost` 承载进度条。显示层直接使用 WPF 官方 `ProgressBar` 控件，放置在 `Canvas` 中，通过位置、宽度、值和可见性表达预先烘焙好的动画。

为保证播放流畅，视频解码、灰度化、二值化和 RLE 分析全部放在播放前的烘焙阶段完成。播放阶段只流式读取烘焙文件并更新已经创建好的 `ProgressBar` 控件。

## 3. 项目目标

### 3.1 最低可用目标

- 创建 WPF Windows 窗口。
- 使用官方 WPF `ProgressBar` 控件显示画面。
- 播放 Bad Apple 视频。
- 将画面降采样为可配置的黑白网格。
- 支持每行多个进度条区段。
- 预先烘焙视频，生成可缓存的动画配置文件。
- 本地存在匹配缓存时直接流式播放。
- 本地不存在缓存时先烘焙再播放。
- 支持播放、暂停、停止。

### 3.2 扩展目标

- 支持任意 FFmpeg 可解码的视频格式。
- 支持固定阈值、Otsu 和自适应阈值。
- 支持黑白反转、亮度、对比度和裁剪模式。
- 支持拖动进度和跳转播放。
- 支持自动保持视频宽高比。
- 可选支持音频播放。
- 支持 Windows x64 独立发布。

## 4. 推荐技术方案

### 4.1 语言方案

推荐使用纯 C# 项目代码，不编写自己的 C++/CLI、native DLL 或 Win32 控件实现。

这里的“纯 C#”指业务代码、UI 代码、烘焙逻辑和播放逻辑全部使用 C#。FFmpeg 和 OpenCV 仍然通过现成的 native runtime 工作，但不会自行维护 C++ 互操作层。

### 4.2 技术栈

- .NET 8
- WPF
- WPF 官方 `ProgressBar`
- `Canvas`：承载和定位多个 `ProgressBar`
- `ControlTemplate` / `Style`：只用于调整官方进度条的布局和显示细节
- `FFmpeg.AutoGen`：烘焙阶段的视频解码、帧时间戳和像素格式处理
- OpenCvSharp：烘焙阶段的灰度化、缩放和二值化
- `ArrayPool<byte>`：减少烘焙阶段的临时内存分配
- 自定义二进制 `.bpb` 文件：保存烘焙后的进度条动画
- 可选：NAudio，用于音频输出

建议目标框架为 `net8.0-windows`，发布目标为 `win-x64`。

## 5. UI 结构和进度条表现

### 5.1 控件结构

```text
WPF Window
└── Canvas
    ├── ProgressBar 0
    ├── ProgressBar 1
    ├── ProgressBar 2
    └── ...
```

每个 `B*W*` 单调块对应一个 WPF `ProgressBar`：

```text
Canvas.Left = 单调块起点
Canvas.Top  = 所在行
Width       = 单调块长度
Height      = 进度条行高
Value       = 单调块中的黑色前缀长度
```

`ProgressBar.Foreground` 保持 WPF 的默认颜色，不在方案中强制设置为黑色。背景颜色、边框和其他外观也优先保留官方默认样式；如果默认模板无法满足布局要求，只通过 WPF 官方支持的 `Style` 或 `ControlTemplate` 调整，不替换为自绘控件。

### 5.2 控件池

进度条控件在播放器初始化或确定网格尺寸后一次性创建，播放时只复用，不执行逐帧创建和删除。

```text
启动或切换网格尺寸：
    创建最大数量的 ProgressBar

播放每一帧：
    更新已有控件的位置、宽度、值和可见性
    隐藏当前帧未使用的控件
```

建议初始控件池大小按以下上限估算：

```text
最大控件数 = height × ceil(width / 2)
```

例如：

```text
80 × 45   → 最多约 1800 个 ProgressBar
120 × 68  → 最多约 4080 个 ProgressBar
160 × 90  → 最多约 7200 个 ProgressBar
```

实际最大数量和可接受帧率必须在目标机器上进行压力测试。第一版建议从 `80 × 45` 开始。

## 6. 烘焙和缓存流程

### 6.1 总流程

```text
选择视频
    ↓
计算源视频 SHA-256
    ↓
计算烘焙参数哈希
    ↓
查找本地 .bpb 缓存
    ├── 存在且匹配：直接流式播放
    └── 不存在或不匹配：执行烘焙
                         ↓
                    保存 .bpb
                         ↓
                    开始播放
```

缓存 key 不能只有视频文件哈希，因为改变网格尺寸、阈值或裁剪模式后，烘焙结果也会改变。

缓存身份至少应包含：

- 源视频 SHA-256
- 网格宽度和高度
- 阈值模式和阈值参数
- 黑白反转设置
- 裁剪或留白模式
- 烘焙算法版本

建议文件名类似：

```text
cache/4A91...D82F_80x45_t128_i0_v1.bpb
```

### 6.2 烘焙阶段

```text
while video has frames:
    frame = FFmpeg decode
    frame = resize and grayscale
    binary = threshold(frame)
    blocks = split each row at W→B transitions
    tracks = match blocks with previous frame
    append progress-bar events to .bpb
```

烘焙阶段可以较慢，因为只执行一次。烘焙过程中应显示进度、预计剩余时间和取消按钮。

### 6.3 播放阶段

```text
open .bpb
read header and index
create or reuse ProgressBar pool

while playback is running:
    read the next event block
    select events for the current frame
    update existing ProgressBar controls
    wait until the frame timestamp is reached
```

播放阶段不再使用 FFmpeg 或 OpenCV，内存中只保留少量事件块和当前控件状态。

## 7. 烘焙文件格式

建议使用自定义二进制 `.bpb` 文件，而不是 JSON。二进制格式更适合流式读取、压缩和快速跳转。

### 7.1 文件结构

```text
Header
├── Magic
├── Version
├── SourceHash
├── ProfileHash
├── Width
├── Height
├── FrameRate
├── FrameCount
└── IndexOffset

Keyframe Block 0
Event Block 0
Event Block 1
Event Block 2
...

Index
├── Frame 0    → 文件偏移
├── Frame 300  → 文件偏移
├── Frame 600  → 文件偏移
└── ...
```

### 7.2 进度条动画轨道

```text
ProgressBarTrack
├── Id
├── StartFrame
├── Duration
├── Row
└── Keyframes
    ├── FrameOffset
    ├── X
    ├── Width
    ├── Height
    └── Value
```

例如：

```text
进度条 12：
    第 100 帧出现
    持续 40 帧

    第 100 帧：x = 30, width = 20, value = 100
    第 110 帧：x = 32, width = 25, value = 100
    第 125 帧：x = 40, width = 15, value = 100
```

两个关键帧之间保持上一个状态。只有位置、宽度、高度或 `Value` 发生变化时才写入新关键帧。

第一版使用离散关键帧，不使用插值。视频本身是离散帧，离散关键帧可以保证播放结果与烘焙结果一致。

### 7.3 单行切分规则

设黑色为 `B`，白色为 `W`。一个进度条可以表达的基本块是：

```text
B*W*
```

也就是左侧可以有若干黑色，右侧可以有若干白色，但同一个进度条内部不能出现 `W→B`。

因此切分规则是：每当扫描到 `W→B`，就在这个黑色位置前开始新的进度条块。示例：

```text
BWBW                 → [BW][BW]
WBW                  → [W][BW]
BBB                  → [BBB]
WBBBWBWWWBBWWB       → [W][BBBW][BWWW][BBWW][B]
```

以上分解均满足每个块都是 `B*W*`。其中纯白块例如 `[W]` 在视觉上可以直接使用 `Canvas` 背景表示，不一定需要创建真实的 `ProgressBar`；如果需要完整记录块结构，也可以保留为一个 `ProgressBar` 轨道。

扫描算法：

```text
start = 0

for x = 1 .. width - 1:
    if pixel[x - 1] is white and pixel[x] is black:
        emit block(start, x - start)
        start = x

emit block(start, width - start)
```

对每个块，黑色前缀长度就是该进度条的 `Value`，块总长度就是其 `Maximum` 或显示宽度。

### 7.4 进度条生命周期匹配

烘焙时需要给每个单调块分配 `ProgressBarId`：

1. 优先匹配同一行的单调块。
2. 计算与上一帧块的横向重叠。
3. 重叠超过阈值且块顺序一致时复用原来的 `ProgressBarId`。
4. 没有匹配到的块创建新的轨道。
5. 消失的块结束生命周期。
6. `W→B` 转换位置变化导致块分裂或合并时，结束旧轨道并创建新轨道。

例如：

```text
上一帧：一个区段
当前帧：两个区段

处理：
    旧 ProgressBarTrack 结束
    创建两个新的 ProgressBarTrack
```

## 8. 图像处理逻辑

### 8.1 网格尺寸

第一版建议使用 `80 × 45` 网格，以控制实际 ProgressBar 数量和 WPF 布局压力。

后续可以支持：

- `120 × 68`
- `160 × 90`
- 根据窗口尺寸和进度条间距动态计算

处理时应尽量保持原始视频宽高比。比例不一致时，默认采用等比缩放并留白，而不是直接拉伸画面。

### 8.2 二值化

默认规则：

```text
灰度值 < threshold → 黑色区段
灰度值 >= threshold → 白色背景
```

建议提供三种模式：

1. 固定阈值：适合 Bad Apple。
2. Otsu 自动阈值：适合整体亮度较稳定的视频。
3. 自适应阈值：适合光照变化较大的视频。

### 8.3 单调块识别

RLE 在这里不是单纯提取黑色连续区段，而是把每一行切分成若干个 `B*W*` 单调块。扫描到 `W→B` 转换时开始新的块：

```text
for each row:
    start = 0

    for x = 1 .. width - 1:
        if pixel[x - 1] is white and pixel[x] is black:
            emit block(start, x - start)
            start = x

    emit block(start, width - start)
```

示例：

```text
输入：  WBBBWBWWWBBWWB
输出：  [W][BBBW][BWWW][BBWW][B]
```

对每个块记录：

```text
Block.StartX
Block.Length
Block.BlackPrefixLength
```

其中 `BlackPrefixLength` 是对应 WPF `ProgressBar` 的 `Value`，`Length` 是其 `Maximum` 和显示宽度。纯白块可以由 `Canvas` 背景直接表示，也可以保留为完整的白色 `ProgressBar` 轨道。

烘焙阶段输出的是单调块和时间轨道；播放阶段将这些块映射到已有的 WPF `ProgressBar` 控件。

## 9. 播放循环

### 9.1 配置读取线程

```text
while .bpb has event blocks:
    block = read next block from file
    block = decompress if necessary
    queue.write(block)
```

读取线程不直接修改 WPF 控件，只负责读取和准备事件块。

### 9.2 WPF UI 线程

```text
on WPF rendering tick:
    currentTime = playback stopwatch

    read all events whose timestamp <= currentTime
    update changed ProgressBar controls
    hide ProgressBar controls that are no longer active
```

播放同步应使用烘焙文件中的帧率和时间戳，不应只依赖固定的 `Sleep(33)`。

每帧更新时：

- 不创建新的 `ProgressBar`。
- 不删除 `Canvas.Children`。
- 只修改位置、宽度、高度、`Value` 和可见性发生变化的控件。
- 保留 `ProgressBar.Foreground` 原本的颜色。

暂停、停止和跳转时需要：

- 停止或暂停播放时钟。
- 清空待应用事件队列。
- 跳转到最近的完整状态快照。
- 从快照继续读取后续事件。

## 10. 项目阶段

### 阶段一：WPF ProgressBar 原型

- 创建 WPF 项目。
- 创建 `Canvas`。
- 创建少量官方 `ProgressBar` 控件。
- 使用测试数据设置位置、宽度、值和可见性。
- 完成单行多个区段的显示。
- 保留默认 `Foreground` 颜色。

验收标准：测试图像可以由多个真正的 WPF `ProgressBar` 控件组成，不能使用矩形自绘替代。

### 阶段二：烘焙格式和静态回放

- 定义 `.bpb` 文件头和事件结构。
- 将测试黑白帧转换为 RLE。
- 生成进度条轨道和关键帧。
- 从 `.bpb` 流式读取并控制 WPF `ProgressBar`。
- 实现关键帧快照和简单跳转。

验收标准：不依赖视频解码，播放器可以从烘焙文件还原测试动画。

### 阶段三：Bad Apple 烘焙与播放

- 接入 FFmpeg.AutoGen。
- 解码 Bad Apple 视频。
- 输出到 `80 × 45` 网格。
- 使用固定阈值二值化。
- 生成 `.bpb` 缓存。
- 实现缓存命中后直接播放。
- 实现播放、暂停、停止。

验收标准：首次打开会烘焙，后续打开直接流式播放，且没有明显逐渐累积的延迟。

### 阶段四：通用视频支持

- 支持任意 FFmpeg 可解码的视频文件。
- 加入源文件哈希和烘焙参数哈希。
- 支持固定阈值、Otsu、自适应阈值。
- 增加黑白反转、亮度、对比度和裁剪选项。
- 增加文件选择、进度显示和跳转控制。

验收标准：常见 MP4、AVI、MKV 等视频能够生成并播放对应的 `.bpb` 文件。

### 阶段五：性能和发布

- 使用固定 ProgressBar 控件池。
- 减少逐帧依赖属性更新。
- 使用有限容量事件队列。
- 对 `.bpb` 进行分块和可选压缩。
- 测试 `80 × 45`、`120 × 68` 和 `160 × 90`。
- 验证长时间播放的内存占用和 GC 次数。
- 打包 FFmpeg 和 OpenCV native runtime。
- 添加第三方许可证说明。

## 11. 主要风险和应对措施

### ProgressBar 控件数量导致卡顿

项目必须使用真正的 WPF `ProgressBar`，因此不能通过自绘绕过控件开销。应使用固定控件池、只更新变化状态，并通过压力测试确定可用网格尺寸。

### 播放阶段仍然发生频繁 GC

烘焙阶段允许进行内存分配；播放阶段使用预分配的控件、事件缓冲区和复用对象，避免逐帧创建集合、字符串和图像对象。

### 烘焙文件过大

使用关键帧、状态保持、差分事件和分块压缩。对于需要精确还原的场景，只在状态发生变化时写入记录。

### 任意视频二值化效果不稳定

提供多种阈值算法和可调参数。必要时加入灰度、模糊、对比度调整和裁剪。

### 视频播放逐渐延迟

使用烘焙文件中的时间戳驱动播放，并在事件队列积压时跳过过期事件块。

### native runtime 部署失败

第一版固定使用 Windows x64，运行时文件随程序发布，并在烘焙功能启动时检查 FFmpeg 和 OpenCV DLL 是否存在。

### FFmpeg 许可证问题

发布前应确认所使用 FFmpeg 构建是否启用了 GPL 组件，并随程序附带相应许可证和版权声明。

## 12. 调研参考

- [WPF ProgressBar Styles and Templates](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/progressbar-styles-and-templates?view=netdesktop-8.0)
- [FFmpeg Decoding API](https://ffmpeg.org/doxygen/trunk/group__lavc__decoding.html)
- [FFmpeg Filters Documentation](https://ffmpeg.org/ffmpeg-filters.html)
- [FFmpeg.AutoGen](https://github.com/Ruslan-B/FFmpeg.AutoGen)
- [OpenCvSharp](https://github.com/shimat/opencvsharp)
- [FFmpeg License and Legal Considerations](https://www.ffmpeg.org/legal.html)
- [OpenCV License](https://opencv.org/license/)
