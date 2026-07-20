[CmdletBinding()]
param(
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',
    [switch]$FrameworkDependent,
    [switch]$SkipUnityPackageEmbedding
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $repositoryRoot "artifacts\publish\RadarBridge\$Runtime"
$project = Join-Path $repositoryRoot 'src\Radar.Bridge.Wpf\Radar.Bridge.Wpf.csproj'
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }

if ($FrameworkDependent -and -not $SkipUnityPackageEmbedding) {
    throw 'A framework-dependent Bridge cannot be embedded in the Unity package. Use -SkipUnityPackageEmbedding or publish self-contained.'
}

Push-Location $repositoryRoot
try {
    dotnet publish $project -c Release -r $Runtime --self-contained $selfContained `
        -p:PublishSingleFile=false -o $outputDirectory
    if ($LASTEXITCODE -ne 0) { throw "Publish failed with exit code $LASTEXITCODE." }

    $profilesDirectory = Join-Path $outputDirectory 'profiles'
    New-Item -ItemType Directory -Force -Path $profilesDirectory | Out-Null
    Copy-Item -Path (Join-Path $repositoryRoot 'config\*.json') `
        -Destination $profilesDirectory -Force

    $executable = Join-Path $outputDirectory 'RadarBridge.exe'
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "Publish completed without RadarBridge.exe: $executable"
    }

    if (-not $SkipUnityPackageEmbedding) {
        $packageRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'UnityPackage\com.yuexin.radar'))
        $embeddedDirectory = [System.IO.Path]::GetFullPath((Join-Path $packageRoot "Bridge~\$Runtime"))
        $packagePrefix = $packageRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
        if (-not $embeddedDirectory.StartsWith($packagePrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Embedded Bridge target escaped the Unity package: $embeddedDirectory"
        }

        if (Test-Path -LiteralPath $embeddedDirectory) {
            Remove-Item -LiteralPath $embeddedDirectory -Recurse -Force
        }

        New-Item -ItemType Directory -Force -Path $embeddedDirectory | Out-Null
        Copy-Item -Path (Join-Path $outputDirectory '*') -Destination $embeddedDirectory -Recurse -Force

        $embeddedExecutable = Join-Path $embeddedDirectory 'RadarBridge.exe'
        if (-not (Test-Path -LiteralPath $embeddedExecutable -PathType Leaf)) {
            throw "Unity package embedding completed without RadarBridge.exe: $embeddedExecutable"
        }

        Write-Host "RadarBridge embedded in Unity package: $embeddedDirectory"
    }

    Write-Host "RadarBridge published to: $outputDirectory"
}
finally {
    Pop-Location
}
