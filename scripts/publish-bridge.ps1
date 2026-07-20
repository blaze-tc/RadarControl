[CmdletBinding()]
param(
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',
    [switch]$FrameworkDependent
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $repositoryRoot "artifacts\publish\RadarBridge\$Runtime"
$project = Join-Path $repositoryRoot 'src\Radar.Bridge.Wpf\Radar.Bridge.Wpf.csproj'
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }

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

    Write-Host "RadarBridge published to: $outputDirectory"
}
finally {
    Pop-Location
}
