# Third-party notices

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
