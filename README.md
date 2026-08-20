# Bad Apple Progress Bars

[简体中文](./README.zh-CN.md)

A Windows video player that turns each frame into a low-resolution black-and-white image rendered by real WPF `ProgressBar` controls. It began as a Bad Apple!! experiment, but accepts any video FFmpeg can decode.

The player does not decode video while it is playing. It bakes the video once into a compact `.bpb` stream, stores it in a hash-keyed local cache, and then streams that baked data into a fixed pool of WPF controls.

## Download and run

Download a ZIP from [Releases](../../releases):

- `self-contained`: recommended; includes the .NET runtime.
- `framework-dependent`: smaller; requires the .NET 8 Windows Desktop Runtime.

Extract the ZIP and double-click `BadAppleProgressBars.exe`.

1. Choose a video file or a baked `.bpb` file.
2. Choose a progress-bar appearance.
3. The player bakes a video on its first use, then starts playback. Later runs reuse the cache when both the input file and render profile match.

The player targets **Windows x64** only.

## Appearances

Every appearance uses real WPF `ProgressBar` controls.

- **Flat**: WPF Aero2 flat track and fill.
- **Flat + stripes**: the flat WPF template with the animated [Striped ProgressBar for WPF](https://gist.github.com/emoacht/febe527df16dd302d55f80921e044be0) overlay.
- **WPF Aero**: the built-in WPF Aero track, border, glass highlight, and fill edges. It intentionally has no stripe overlay.

The main playback window contains only its `Canvas` and the pooled `ProgressBar` controls. The file and appearance pickers are separate startup dialogs.

## Command line

Passing a file skips the file and appearance pickers. The default appearance in this mode is `aero`.

```powershell
BadAppleProgressBars.exe .\video.mp4
BadAppleProgressBars.exe --style striped .\video.mp4
BadAppleProgressBars.exe --style=flat .\video.bpb
```

Supported values for `--style` are `flat`, `striped`, and `aero`.

## Playback controls

- `Space`: pause or resume.
- `R` / `Home`: restart.
- `Left` / `Right`: step one frame backward or forward.

## Video baking and cache

For a video input, the bundled `ffmpeg.exe` normalizes frames and OpenCvSharp performs grayscale conversion and binary thresholding. The bake result is stored at:

```text
%LocalAppData%\BadAppleProgressBars\cache
```

Cache names contain the SHA-256 hash of the source file and the render-profile hash. A changed video, grid, threshold, or bake algorithm profile creates a distinct cache entry. A `.bpb` input is streamed directly and is not baked again.

The current render profile is 80 × 45 at 30 FPS with a fixed binary threshold. Playback never creates or destroys progress bars per frame; it updates a preallocated control pool instead.

## Build from source

Requirements:

- Windows x64
- .NET 8 SDK
- A reviewed Windows x64 FFmpeg distribution for packaging

Run tests:

```powershell
dotnet test .\BadAppleProgressBars.sln
```

Run the development build:

```powershell
dotnet run --project .\src\BadAppleProgressBars\BadAppleProgressBars.csproj
```

Create a local package with a reviewed GPL FFmpeg distribution root:

```powershell
.\scripts\Publish-WinX64.ps1 -FfmpegRoot C:\ffmpeg -SelfContained true
.\scripts\Publish-WinX64.ps1 -FfmpegRoot C:\ffmpeg -SelfContained false
```

Release ZIPs include `ffmpeg/ffmpeg.exe`, its `LICENSE`, `README.txt`, and `THIRD_PARTY_NOTICES.md`. The application never uses a user's `PATH` to find FFmpeg.

## CI and releases

Every push to `main` runs the test suite and produces self-contained and framework-dependent x64 workflow artifacts. Pushing a tag in the exact form `vX.X.X.X` creates or updates the matching GitHub Release with both ZIPs.

## Third-party notices

The release includes GPL FFmpeg and OpenCvSharp/OpenCV native dependencies. The WPF stripe overlay and Aero template attributions are documented in [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md). Review that file before redistributing modified packages.

## Project documents

- [Project plan](./docs/PROJECT_PLAN.md)
- [Implementation plan](./docs/IMPLEMENTATION_PLAN.md)
