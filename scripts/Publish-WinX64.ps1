[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$FfmpegRoot,

    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot 'src\BadAppleProgressBars\BadAppleProgressBars.csproj'
$ffmpegExecutable = Join-Path $FfmpegRoot 'bin\ffmpeg.exe'
$ffmpegLicense = Join-Path $FfmpegRoot 'LICENSE'
$ffmpegReadme = Join-Path $FfmpegRoot 'README.txt'

foreach ($requiredFile in @($ffmpegExecutable, $ffmpegLicense, $ffmpegReadme)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required FFmpeg distribution file is missing: $requiredFile"
    }
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
$publishDirectory = Join-Path $resolvedOutput 'BadAppleProgressBars-win-x64'
$releaseZip = Join-Path $resolvedOutput 'BadAppleProgressBars-win-x64.zip'

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

if (Test-Path -LiteralPath $releaseZip) {
    Remove-Item -LiteralPath $releaseZip -Force
}

New-Item -ItemType Directory -Force -Path $publishDirectory | Out-Null
dotnet publish $projectPath --configuration Release --runtime win-x64 --self-contained true --output $publishDirectory

$ffmpegDestination = Join-Path $publishDirectory 'ffmpeg'
New-Item -ItemType Directory -Force -Path $ffmpegDestination | Out-Null
Copy-Item -LiteralPath $ffmpegExecutable -Destination (Join-Path $ffmpegDestination 'ffmpeg.exe')
Copy-Item -LiteralPath $ffmpegLicense -Destination (Join-Path $ffmpegDestination 'LICENSE')
Copy-Item -LiteralPath $ffmpegReadme -Destination (Join-Path $ffmpegDestination 'README.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $publishDirectory 'THIRD_PARTY_NOTICES.md')

Compress-Archive -Path $publishDirectory -DestinationPath $releaseZip
Write-Output "Created GitHub Release asset: $releaseZip"
