[CmdletBinding()]
param(
    [ValidateRange(1, 60)]
    [int]$StartupTimeoutSeconds = 15
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $repositoryRoot 'UnityPackage\com.yuexin.radar\Bridge~\win-x64\RadarBridge.exe'
$smokeDirectory = Join-Path $repositoryRoot 'tmp\embedded-bridge-smoke'
$profile = Join-Path $smokeDirectory 'profile.json'
$parentProcess = $null
$bridgeProcess = $null

if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Embedded RadarBridge executable was not found: $executable"
}

New-Item -ItemType Directory -Force -Path $smokeDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'config\default-profile.json') -Destination $profile -Force

try {
    # A short-lived native process exercises RadarBridge's Unity parent-process monitor.
    $parentProcess = Start-Process -FilePath 'ping.exe' -ArgumentList @('-n', '9', '127.0.0.1') -WindowStyle Hidden -PassThru
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $executable
    $startInfo.Arguments = "--profile `"$profile`" --parent-pid $($parentProcess.Id) --minimized"
    $startInfo.WorkingDirectory = Split-Path -Parent $executable
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $bridgeProcess = [System.Diagnostics.Process]::Start($startInfo)

    $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    $windowTitle = ''
    $windowHandle = [IntPtr]::Zero
    while ([DateTime]::UtcNow -lt $deadline -and -not $bridgeProcess.HasExited) {
        Start-Sleep -Milliseconds 200
        $bridgeProcess.Refresh()
        $windowTitle = $bridgeProcess.MainWindowTitle
        $windowHandle = $bridgeProcess.MainWindowHandle
        if ($windowHandle -ne [IntPtr]::Zero -and -not [string]::IsNullOrWhiteSpace($windowTitle)) {
            break
        }
    }

    if ($bridgeProcess.HasExited) {
        throw "Embedded RadarBridge exited during startup with code $($bridgeProcess.ExitCode)."
    }
    if ($windowHandle -eq [IntPtr]::Zero -or [string]::IsNullOrWhiteSpace($windowTitle)) {
        throw 'Embedded RadarBridge did not create a top-level window.'
    }
    if ($windowTitle -match '失败|错误|failed|error') {
        throw "Embedded RadarBridge displayed an error window: $windowTitle"
    }

    $parentProcess.WaitForExit()
    if (-not $bridgeProcess.WaitForExit(10000)) {
        throw 'Embedded RadarBridge did not exit after its parent process ended.'
    }
    if ($bridgeProcess.ExitCode -ne 0) {
        throw "Embedded RadarBridge exited with code $($bridgeProcess.ExitCode)."
    }

    Write-Host "Embedded RadarBridge startup passed: $windowTitle"
    Write-Host 'Parent-process shutdown passed with exit code 0.'
}
finally {
    if ($bridgeProcess -and -not $bridgeProcess.HasExited) {
        Stop-Process -Id $bridgeProcess.Id -Force
    }
    if ($parentProcess -and -not $parentProcess.HasExited) {
        Stop-Process -Id $parentProcess.Id -Force
    }
    if (Test-Path -LiteralPath $profile) {
        Remove-Item -LiteralPath $profile -Force
    }
    if (Test-Path -LiteralPath $smokeDirectory) {
        Remove-Item -LiteralPath $smokeDirectory -Force
    }
}
