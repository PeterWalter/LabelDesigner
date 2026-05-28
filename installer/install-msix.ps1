param(
    [string]$PackagePath = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $defaultPath = Join-Path $repoRoot "artifacts\installer\msix"
    $latest = Get-ChildItem -Path $defaultPath -Filter *.msix -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $latest) {
        throw "No MSIX package found under $defaultPath. Run installer\build-msix.ps1 first."
    }

    $PackagePath = $latest.FullName
}

if (-not (Test-Path $PackagePath)) {
    throw "MSIX package not found: $PackagePath"
}

Write-Host "Installing package:"
Write-Host $PackagePath

Add-AppxPackage -Path $PackagePath -ForceApplicationShutdown

Write-Host "Install complete."
