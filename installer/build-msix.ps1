param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("x86", "x64", "ARM64")]
    [string]$Platform = "x64",
    [string]$PackageVersion = "",
    [string]$CertificatePath = "",
    [string]$CertificatePassword = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "LabelDesigner.App\LabelDesigner.App.csproj"
$outputDir = (Join-Path $repoRoot "artifacts\installer\msix\$Platform").TrimEnd('\')
$stagingRoot = (Join-Path $env:TEMP "LabelDesignerInstaller\msix\$Platform").TrimEnd('\')
$appxPackageDir = "$stagingRoot\"

if (-not (Test-Path $projectPath)) {
    throw "Project not found: $projectPath"
}

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
if (Test-Path $stagingRoot) {
    Remove-Item -Path $stagingRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

$msbuildArgs = @(
    $projectPath,
    "/t:Restore,Build",
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform",
    "/p:GenerateAppxPackageOnBuild=true",
    "/p:UapAppxPackageBuildMode=SideloadOnly",
    "/p:AppxBundle=Never",
    "/p:AppxPackageDir=$appxPackageDir"
)

if (-not [string]::IsNullOrWhiteSpace($PackageVersion)) {
    $msbuildArgs += "/p:AppxPackageVersion=$PackageVersion"
}

if (-not [string]::IsNullOrWhiteSpace($CertificatePath)) {
    if (-not (Test-Path $CertificatePath)) {
        throw "Certificate file not found: $CertificatePath"
    }
    $msbuildArgs += "/p:AppxPackageSigningEnabled=true"
    $msbuildArgs += "/p:PackageCertificateKeyFile=$CertificatePath"
    if (-not [string]::IsNullOrWhiteSpace($CertificatePassword)) {
        $msbuildArgs += "/p:PackageCertificatePassword=$CertificatePassword"
    }
}
else {
    $msbuildArgs += "/p:AppxPackageSigningEnabled=false"
}

Write-Host "Building MSIX package..."
dotnet msbuild @msbuildArgs
if ($LASTEXITCODE -ne 0) {
    throw "MSIX build failed with exit code $LASTEXITCODE"
}

$artifactRoot = Join-Path $repoRoot "artifacts\installer\msix"
$builtPackages = Get-ChildItem -Path $stagingRoot -Filter *.msix -Recurse | Sort-Object LastWriteTime -Descending
if ($builtPackages.Count -eq 0) {
    throw "No .msix package was produced in: $stagingRoot"
}

Get-ChildItem -Path $outputDir -Force | Remove-Item -Recurse -Force
Copy-Item -Path (Join-Path $stagingRoot '*') -Destination $outputDir -Recurse -Force
$builtPackages = Get-ChildItem -Path $artifactRoot -Filter *.msix -Recurse | Sort-Object LastWriteTime -Descending

Write-Host "MSIX package created:"
Write-Host $builtPackages[0].FullName
