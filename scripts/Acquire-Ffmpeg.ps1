[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$DestinationDirectory,

    [string]$DownloadUrl = 'https://github.com/GyanD/codexffmpeg/releases/download/8.1/ffmpeg-8.1-essentials_build.zip',

    [string]$ExpectedFfmpegSha256 = '1A65D5B0B10D8D9A81D2824A3538046A40ED3607C906B335A166ADD87613F705'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$destination = [System.IO.Path]::GetFullPath($DestinationDirectory)
$archivePath = Join-Path $destination 'ffmpeg.zip'
$extractionDirectory = Join-Path $destination 'extracted'

New-Item -ItemType Directory -Force -Path $destination | Out-Null
$downloadSucceeded = $false

for ($attempt = 1; $attempt -le 3; $attempt++) {
    try {
        Invoke-WebRequest -Uri $DownloadUrl -OutFile $archivePath
        $downloadSucceeded = $true
        break
    }
    catch {
        if (Test-Path -LiteralPath $archivePath) {
            Remove-Item -LiteralPath $archivePath -Force
        }

        if ($attempt -eq 3) {
            throw
        }

        Start-Sleep -Seconds (2 * $attempt)
    }
}

if (-not $downloadSucceeded) {
    throw 'Unable to download the FFmpeg distribution.'
}

Expand-Archive -LiteralPath $archivePath -DestinationPath $extractionDirectory
$ffmpegRoot = Get-ChildItem -LiteralPath $extractionDirectory -Directory | Select-Object -First 1

if ($null -eq $ffmpegRoot) {
    throw 'The FFmpeg archive did not contain a root directory.'
}

$ffmpegExecutable = Join-Path $ffmpegRoot.FullName 'bin\ffmpeg.exe'
if (-not (Test-Path -LiteralPath $ffmpegExecutable -PathType Leaf)) {
    throw 'The FFmpeg archive did not contain bin\ffmpeg.exe.'
}

$actualHash = (Get-FileHash -LiteralPath $ffmpegExecutable -Algorithm SHA256).Hash
if (-not [string]::Equals($actualHash, $ExpectedFfmpegSha256, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Unexpected ffmpeg.exe SHA-256. Expected $ExpectedFfmpegSha256, got $actualHash."
}

Write-Output $ffmpegRoot.FullName
