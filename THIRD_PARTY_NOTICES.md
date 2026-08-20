# Third-party notices

## Striped ProgressBar for WPF

The WPF `ProgressBar` control template in `src/BadAppleProgressBars/App.xaml`
adapts the stripe drawing and animation from
[`StripedProgressBar.xaml`](https://gist.github.com/emoacht/febe527df16dd302d55f80921e044be0),
which is an excerpt from [emoacht/WpfControlCollection](https://github.com/emoacht/WpfControlCollection).
Its stripe drawing and animation are used inside the WPF Aero ProgressBar
template described below.

The upstream work is licensed under the MIT License:

> The MIT License (MIT)
>
> Copyright (c) 2021 emoacht
>
> Permission is hereby granted, free of charge, to any person obtaining a copy
> of this software and associated documentation files (the "Software"), to deal
> in the Software without restriction, including without limitation the rights
> to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
> copies of the Software, and to permit persons to whom the Software is
> furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all
> copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
> IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
> FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
> AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
> LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
> OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
> SOFTWARE.

## WPF Aero ProgressBar template

The track, borders, glass highlight, fill-edge, and indeterminate-state
template structure in `src/BadAppleProgressBars/App.xaml` is reused from the
WPF built-in Aero theme:

- Source: <https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/Themes/PresentationFramework.Aero/themes/Aero.NormalColor.xaml>
- License: MIT, as provided by the .NET WPF repository.

Only the existing third-party stripe overlay is added inside the official
template's determinate fill area.

## FFmpeg

The Windows x64 release package includes `ffmpeg/ffmpeg.exe` only when it is created with `scripts/Publish-WinX64.ps1` and an explicitly supplied FFmpeg root.

The currently approved release input is Gyan FFmpeg `8.1-essentials_build-www.gyan.dev`:

- License: GPLv3.
- Binary SHA-256: `1A65D5B0B10D8D9A81D2824A3538046A40ED3607C906B335A166ADD87613F705`.
- Matching FFmpeg source: <https://github.com/FFmpeg/FFmpeg/commit/9047fa1b08>.
- Binary build and configuration record: <https://www.gyan.dev/ffmpeg/builds/>.

Every release ZIP must contain the exact `LICENSE` and `README.txt` copied from the selected FFmpeg distribution. This application invokes the executable as a separate process; this notice does not replace a legal review of the full release package.

## OpenCvSharp / OpenCV

The application uses `OpenCvSharp4.Windows` `4.13.0.20260627`. Its NuGet package supplies the Windows x64 native runtime during publish. See the package and upstream project for the applicable notices: <https://www.nuget.org/packages/OpenCvSharp4.Windows/> and <https://github.com/shimat/opencvsharp>.
