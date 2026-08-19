[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$FfmpegRoot,

    [ValidateSet('win-x64')]
    [string]$RuntimeIdentifier = 'win-x64',

    [ValidateSet('true', 'false')]
    [string]$SelfContained = 'true',

    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version = '0.0.0.0',

    [string]$ArtifactName,

    [string]$OutputDirectory,

    [string]$ExpectedFfmpegSha256 = '1A65D5B0B10D8D9A81D2824A3538046A40ED3607C906B335A166ADD87613F705'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot 'src\BadAppleProgressBars\BadAppleProgressBars.csproj'
$ffmpegExecutable = Join-Path $FfmpegRoot 'bin\ffmpeg.exe'
$ffmpegLicense = Join-Path $FfmpegRoot 'LICENSE'
$ffmpegReadme = Join-Path $FfmpegRoot 'README.txt'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot 'artifacts'
}

foreach ($requiredFile in @($ffmpegExecutable, $ffmpegLicense, $ffmpegReadme)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required FFmpeg distribution file is missing: $requiredFile"
    }
}

$actualFfmpegSha256 = (Get-FileHash -LiteralPath $ffmpegExecutable -Algorithm SHA256).Hash
if (-not [string]::Equals($actualFfmpegSha256, $ExpectedFfmpegSha256, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Unexpected ffmpeg.exe SHA-256. Expected $ExpectedFfmpegSha256, got $actualFfmpegSha256."
}

$isSelfContained = [System.Convert]::ToBoolean($SelfContained)
$flavor = if ($isSelfContained) { 'self-contained' } else { 'framework-dependent' }
if ([string]::IsNullOrWhiteSpace($ArtifactName)) {
    $ArtifactName = "BadAppleProgressBars-v$Version-$RuntimeIdentifier-$flavor"
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
$publishDirectory = Join-Path $resolvedOutput $ArtifactName
$releaseZip = Join-Path $resolvedOutput "$ArtifactName.zip"

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

if (Test-Path -LiteralPath $releaseZip) {
    Remove-Item -LiteralPath $releaseZip -Force
}

New-Item -ItemType Directory -Force -Path $publishDirectory | Out-Null
$selfContainedText = $isSelfContained.ToString().ToLowerInvariant()
dotnet publish $projectPath --configuration Release --runtime $RuntimeIdentifier --self-contained $selfContainedText --output $publishDirectory `
    -p:Version=$Version -p:AssemblyVersion=$Version -p:FileVersion=$Version -p:InformationalVersion=$Version

$ffmpegDestination = Join-Path $publishDirectory 'ffmpeg'
New-Item -ItemType Directory -Force -Path $ffmpegDestination | Out-Null
Copy-Item -LiteralPath $ffmpegExecutable -Destination (Join-Path $ffmpegDestination 'ffmpeg.exe')
Copy-Item -LiteralPath $ffmpegLicense -Destination (Join-Path $ffmpegDestination 'LICENSE')
Copy-Item -LiteralPath $ffmpegReadme -Destination (Join-Path $ffmpegDestination 'README.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $publishDirectory 'THIRD_PARTY_NOTICES.md')

Compress-Archive -Path $publishDirectory -DestinationPath $releaseZip
Write-Output "Created package: $releaseZip"
