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
- 随 Windows x64 发布包分发的 `ffmpeg.exe`：以独立子进程完成视频解码、帧率归一化和保比例缩放
- `OpenCvSharp4.Windows`：随发布输出 native runtime，负责 BGR→灰度、二值化，以及后续 Otsu/自适应阈值扩展
- C# 烘焙器：读取 FFmpeg 的固定尺寸 BGR 原始帧流、调用 OpenCV 处理并执行单调块切分
- `ArrayPool<byte>`：减少烘焙阶段的临时内存分配
- 自定义二进制 `.bpb` 文件：保存烘焙后的进度条动画
- 可选：NAudio，用于音频输出

建议目标框架为 `net8.0-windows`，发布目标为 `win-x64`。

### 4.3 可选的 OpenCV 依赖裁剪方向（暂不实施）

当前发布包同时包含显式分发的 `ffmpeg.exe` 和 OpenCV 的 `opencv_videoio_ffmpeg*.dll`。前者是本项目实际使用的命令行解码器，负责固定 FPS、等比缩放/留白和向标准输出提供 BGR 原始帧；后者是 OpenCV `VideoCapture` / `VideoWriter` 的内部 videoio 后端插件，不能作为 `ffmpeg.exe` 的替代品启动，也不参与当前烘焙路径。

日后若需要缩小包体，可评估将 `OpenCvSharp4.Windows` 换为官方 `OpenCvSharp4.Windows.Slim`，以移除未使用的 `videoio` 与 `opencv_videoio_ffmpeg*.dll`。这是优化候选项，而不是当前决定；执行前必须同时满足：

- Slim 包仍提供当前使用的 `Mat`、BGR→灰度和二值化 API。
- 单元测试、真实视频首次烘焙、缓存命中播放和两种发布包测试全部通过。
- 发布包中显式携带的 `ffmpeg.exe`、许可证与第三方声明保持不变。
- 重新核对 Slim 包及剩余 native 依赖的许可证和实际包体收益。

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

### 5.1.1 ProgressBar 间距与对齐

为了让画面明确呈现为多个真实进度条，而不是连续色块，两个不同的
`ProgressBar` 之间保留固定 **2 DIPs** 间距，横向和纵向均适用。

横向间距按当前帧全局对齐：收集所有行的 `W→B` 切分列，在每个不同的列
插入一条 2 DIPs 的间隙。某一行的进度条若跨过其他行产生的间隙列，则其
显示宽度和已填充长度都增加对应的 `2 × n`，保持黑白前缀的比例与列对齐。

```text
第 0 行： [BW]  [BW]
第 1 行： [BWWWW]      ← 跨过第 0 行的间隙列，宽度包含该 2 DIPs
```

每帧先从 Canvas 可用宽高扣除所需间隙，再计算逻辑网格单元的宽高，保证
所有进度条与间隙共同撑满 Canvas；播放中仍只更新固定控件池。

`ProgressBar.Foreground` 保持 WPF 的默认颜色，不在方案中强制设置为黑色。背景颜色、边框和其他外观也优先保留官方默认样式；如果默认模板无法满足布局要求，只通过 WPF 官方支持的 `Style` 或 `ControlTemplate` 调整，不替换为自绘控件。

### 5.1.2 启动方式与可选外观

播放器主窗口的可视树始终只有 Canvas 与其中的官方 ProgressBar。无命令行参数启动时，先显示系统文件选择对话框，再显示独立的启动样式选择窗口；选择完成后才开始烘焙或播放。因此初次使用者不需要了解命令行，也不会在播放器 Canvas 内看到额外按钮或文本。

可选样式均复用 WPF 官方主题模板或已有的 `Striped ProgressBar for WPF` 覆盖层：

- `flat`：WPF Aero2 扁平进度条。
- `striped`：Aero2 扁平进度条加 Gist 的条纹层。
- `aero`：WPF Aero 轨道、边框、玻璃高光和填充边缘，不含条纹层。

命令行可使用 `--style flat|striped|aero <file>` 指定样式；传入文件但未指定样式时默认 `aero`。无参数交互启动时由用户选择。

阶段性开发使用的“无参数自动生成合成 .bpb 并播放”路径不保留在生产程序集。二进制格式和流式播放器的合成帧夹具只存在于测试项目。

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

实际首版缓存写入 `%LocalAppData%\BadAppleProgressBars\cache`，文件名由完整十六进制源哈希与渲染配置哈希组成，例如：

```text
4A91...D82F_AE63...9D20.bpb
```

渲染配置哈希包含网格、FPS、阈值、黑白反转、等比留白规则、烘焙算法版本和 `.bpb` 格式版本。缓存命中后还必须核对 `.bpb` 文件头内的两份哈希；损坏或不一致的缓存会重新烘焙。

### 6.2 烘焙阶段

```text
while video has frames:
    frame = bundled ffmpeg.exe decode / scale / BGR rawvideo output
    frame = OpenCV grayscale / threshold
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

- 发布包内包含 Windows x64 `ffmpeg.exe` 及对应许可证和源代码获取说明。
- 启动时传入视频路径则自动调用内置 FFmpeg 烘焙；烘焙成功后立即打开生成的 `.bpb` 播放。
- 解码 Bad Apple 视频并将其规范化为固定 30 FPS、80 × 45 的灰度帧流。
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

### FFmpeg 部署或许可证不合规

第一版固定使用 Windows x64，`ffmpeg.exe` 随程序发布，并在烘焙功能启动时检查其存在和可执行性。发布前必须记录该二进制的来源、版本、构建配置、许可证及对应源代码获取方式；不得将 `--enable-nonfree` 构建用于再分发。

### FFmpeg 许可证问题

发布前应确认所使用 FFmpeg 构建是否启用了 GPL 组件，并随程序附带相应许可证和版权声明。

### OpenCV videoio 依赖重复

完整 OpenCvSharp Windows 包会带来 OpenCV 的 FFmpeg videoio 插件，但本项目目前并不通过 OpenCV 解码视频。短期保留它以降低依赖变更风险；后续可按“可选的 OpenCV 依赖裁剪方向”验证 Slim 包，不能直接删除 DLL 或假设该插件能替代独立 `ffmpeg.exe`。

### x86 发布不可用

当前使用的 `OpenCvSharp4.Windows` runtime 已不提供 x86 支持，首版 CI 和发布包仅支持 Windows x64。若未来恢复 x86，必须先更换并验证完整的 x86 OpenCV runtime 与 FFmpeg 二进制，再扩展构建矩阵。

## 12. 调研参考

- [WPF ProgressBar Styles and Templates](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/progressbar-styles-and-templates?view=netdesktop-8.0)
- [FFmpeg Decoding API](https://ffmpeg.org/doxygen/trunk/group__lavc__decoding.html)
- [FFmpeg Filters Documentation](https://ffmpeg.org/ffmpeg-filters.html)
- [FFmpeg.AutoGen](https://github.com/Ruslan-B/FFmpeg.AutoGen)
- [OpenCvSharp](https://github.com/shimat/opencvsharp)
- [FFmpeg License and Legal Considerations](https://www.ffmpeg.org/legal.html)
- [OpenCV License](https://opencv.org/license/)
