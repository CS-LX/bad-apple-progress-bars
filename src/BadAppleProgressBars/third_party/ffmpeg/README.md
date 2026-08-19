# Bundled FFmpeg for Windows x64

For local debug builds, place the reviewed Windows x64 `ffmpeg.exe` in this directory. The project copies it to `ffmpeg/ffmpeg.exe` beside the application, and the baker resolves only that path. It does not search the user's PATH or require a system installation.

Before committing or distributing the executable, also add the applicable FFmpeg license notices, the exact version and configure line, the binary source URL, and a source-code download URL. Do not distribute a build configured with `--enable-nonfree`.

Release assets are created by `scripts/Publish-WinX64.ps1`, which receives an FFmpeg distribution root and copies its executable, `LICENSE`, and `README.txt` into the ZIP. The currently approved input is the GPLv3 Gyan FFmpeg 8.1 essentials build; see the repository-level `THIRD_PARTY_NOTICES.md` for its exact source commit and checksum.
