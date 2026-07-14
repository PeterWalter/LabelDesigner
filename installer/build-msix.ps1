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
$manifestPath = Join-Path $repoRoot "LabelDesigner.App\Package.appxmanifest"
$outputDir = (Join-Path $repoRoot "artifacts\installer\msix\$Platform").TrimEnd('\')
$bundleOutputDir = (Join-Path $repoRoot "artifacts\installer\bundle\$Platform").TrimEnd('\')
$stagingRoot = (Join-Path $env:TEMP "LabelDesignerInstaller\msix\$Platform").TrimEnd('\')
$appxPackageDir = "$stagingRoot\"
$generatedAssetsDir = Join-Path $stagingRoot "_generated"
$generatedCertificatePublicPath = ""
$generatedCertificateThumbprint = ""

function Get-ManifestIdentityValue {
    param(
        [string]$ManifestFile,
        [string]$AttributeName
    )

    [xml]$manifestXml = Get-Content -Path $ManifestFile
    $namespaceManager = New-Object System.Xml.XmlNamespaceManager($manifestXml.NameTable)
    $namespaceManager.AddNamespace("appx", $manifestXml.DocumentElement.NamespaceURI)
    $identityNode = $manifestXml.SelectSingleNode("/appx:Package/appx:Identity", $namespaceManager)
    if ($null -eq $identityNode) {
        throw "Unable to locate Package/Identity in manifest: $ManifestFile"
    }

    $attribute = $identityNode.Attributes[$AttributeName]
    if ($null -eq $attribute -or [string]::IsNullOrWhiteSpace($attribute.Value)) {
        throw "Manifest identity attribute '$AttributeName' is missing in: $ManifestFile"
    }

    return $attribute.Value
}

function Set-ManifestIdentityValue {
    param(
        [string]$ManifestFile,
        [string]$AttributeName,
        [string]$Value
    )

    [xml]$manifestXml = Get-Content -Path $ManifestFile
    $namespaceManager = New-Object System.Xml.XmlNamespaceManager($manifestXml.NameTable)
    $namespaceManager.AddNamespace("appx", $manifestXml.DocumentElement.NamespaceURI)
    $identityNode = $manifestXml.SelectSingleNode("/appx:Package/appx:Identity", $namespaceManager)
    if ($null -eq $identityNode) {
        throw "Unable to locate Package/Identity in manifest: $ManifestFile"
    }

    $attribute = $identityNode.Attributes[$AttributeName]
    if ($null -eq $attribute) {
        throw "Manifest identity attribute '$AttributeName' is missing in: $ManifestFile"
    }

    $attribute.Value = $Value
    $manifestXml.Save($ManifestFile)
}

function Test-PackageVersion {
    param(
        [string]$Version
    )

    if ([string]::IsNullOrWhiteSpace($Version)) {
        return $false
    }

    $parts = $Version.Split('.')
    if ($parts.Count -ne 4) {
        return $false
    }

    foreach ($part in $parts) {
        $parsed = 0
        if (-not [int]::TryParse($part, [ref]$parsed)) {
            return $false
        }

        if ($parsed -lt 0 -or $parsed -gt 65535) {
            return $false
        }
    }

    return $true
}

function Get-ResolvedPackageVersion {
    param(
        [string]$ManifestFile,
        [string]$RequestedVersion
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedVersion)) {
        if (-not (Test-PackageVersion -Version $RequestedVersion)) {
            throw "PackageVersion must be four dot-separated integers between 0 and 65535. Received: $RequestedVersion"
        }

        return $RequestedVersion
    }

    $manifestVersion = Get-ManifestIdentityValue -ManifestFile $ManifestFile -AttributeName "Version"
    if (-not (Test-PackageVersion -Version $manifestVersion)) {
        throw "Manifest version is invalid and cannot be used as the package-version base: $manifestVersion"
    }

    $parts = $manifestVersion.Split('.')
    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $now = Get-Date
    $build = (($now.Year - 2020) * 400) + $now.DayOfYear
    $revision = ($now.Hour * 3600) + ($now.Minute * 60) + $now.Second

    if ($build -lt 0 -or $build -gt 65535 -or $revision -lt 0 -or $revision -gt 65535) {
        throw "Auto-generated package version parts are out of range: $major.$minor.$build.$revision"
    }

    return "$major.$minor.$build.$revision"
}

function New-TemporarySigningCertificate {
    param(
        [string]$Subject,
        [string]$OutputDirectory
    )

    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Subject `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -FriendlyName "LabelDesigner MSIX Signing" `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy Exportable `
        -NotAfter (Get-Date).AddYears(3)

    $cerPath = Join-Path $OutputDirectory "LabelDesigner.GeneratedSigning.cer"

    Export-Certificate -Cert $certificate -FilePath $cerPath | Out-Null

    return @{
        CerPath = $cerPath
        Thumbprint = $certificate.Thumbprint
    }
}

function Get-DotNetInstallScript {
    param(
        [string]$OutputDirectory
    )

    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $scriptPath = Join-Path $OutputDirectory "dotnet-install.ps1"

    if (Test-Path $scriptPath) {
        return $scriptPath
    }

    $url = "https://dot.net/v1/dotnet-install.ps1"
    Write-Host "Downloading .NET install script from $url..."
    Invoke-WebRequest -Uri $url -OutFile $scriptPath -UseBasicParsing
    return $scriptPath
}

function Get-WindowsAppSdkRuntimeInstaller {
    param(
        [string]$OutputDirectory,
        [string]$Architecture
    )

    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $fileName = "windowsappruntimeinstall-$Architecture.exe"
    $installerPath = Join-Path $OutputDirectory $fileName

    if (Test-Path $installerPath) {
        return $installerPath
    }

    # Windows App SDK 2.1.3 runtime installer (matches the NuGet package referenced by the app).
    $url = "https://aka.ms/windowsappsdk/2.1/2.1.3/$fileName"
    Write-Host "Downloading Windows App SDK runtime installer from $url..."
    try {
        Invoke-WebRequest -Uri $url -OutFile $installerPath -UseBasicParsing
    }
    catch {
        Write-Warning "Could not download Windows App SDK runtime installer. The target machine may need to install it manually. Error: $($_.Exception.Message)"
        return $null
    }

    return $installerPath
}

function New-StandaloneInstallerBundle {
    param(
        [string]$PackageFile,
        [string]$BundleRoot,
        [string]$DotNetInstallScript,
        [string]$WindowsAppSdkInstaller
    )

    $packageDirectory = Split-Path -Parent $PackageFile
    $bundleName = "$([System.IO.Path]::GetFileNameWithoutExtension($PackageFile))_Installer"
    $bundleDirectory = Join-Path $BundleRoot $bundleName
    $bundlePackageRoot = Join-Path $bundleDirectory "package"
    $zipPath = Join-Path $BundleRoot ($bundleName + ".zip")
    $timestampSuffix = Get-Date -Format "yyyyMMdd-HHmmss"

    try {
        if (Test-Path $bundleDirectory) {
            Remove-Item -Path $bundleDirectory -Recurse -Force
        }
        if (Test-Path $zipPath) {
            Remove-Item -Path $zipPath -Force
        }
    }
    catch {
        $bundleName = "$bundleName-$timestampSuffix"
        $bundleDirectory = Join-Path $BundleRoot $bundleName
        $bundlePackageRoot = Join-Path $bundleDirectory "package"
        $zipPath = Join-Path $BundleRoot ($bundleName + ".zip")
    }

    New-Item -ItemType Directory -Path $bundlePackageRoot -Force | Out-Null
    Copy-Item -Path $packageDirectory -Destination $bundlePackageRoot -Recurse -Force

    $prereqsDirectory = Join-Path $bundleDirectory "prerequisites"
    New-Item -ItemType Directory -Path $prereqsDirectory -Force | Out-Null

    if (-not [string]::IsNullOrWhiteSpace($DotNetInstallScript) -and (Test-Path $DotNetInstallScript)) {
        Copy-Item -Path $DotNetInstallScript -Destination $prereqsDirectory -Force
    }

    if (-not [string]::IsNullOrWhiteSpace($WindowsAppSdkInstaller) -and (Test-Path $WindowsAppSdkInstaller)) {
        Copy-Item -Path $WindowsAppSdkInstaller -Destination $prereqsDirectory -Force
    }

    $installScript = @'
param(
    [string]$PackagePath = "",
    [switch]$Elevated,
    [string]$LogPath = ""
)

$ErrorActionPreference = "Stop"

function Test-IsElevated {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Write-InstallerFailureLog {
    param(
        [System.Management.Automation.ErrorRecord]$ErrorRecord,
        [string]$FailureLogPath
    )

    if ([string]::IsNullOrWhiteSpace($FailureLogPath)) {
        return
    }

    $logDirectory = Split-Path -Parent $FailureLogPath
    if (-not [string]::IsNullOrWhiteSpace($logDirectory)) {
        New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    }

    $details = @(
        "Message: $($ErrorRecord.Exception.Message)"
        ""
        "Error:"
        ($ErrorRecord | Out-String).TrimEnd()
    ) -join [Environment]::NewLine

    Set-Content -Path $FailureLogPath -Value $details -Encoding UTF8
}

function Test-DotNetDesktopRuntime {
    param(
        [string]$Channel = "10.0"
    )

    try {
        $runtimes = & dotnet --list-runtimes 2>$null
        if ($null -eq $runtimes) {
            return $false
        }

        $pattern = "Microsoft\.NETCore\.App\s+" + [regex]::Escape($Channel)
        return ($runtimes | Where-Object { $_ -match $pattern } | Select-Object -First 1) -ne $null
    }
    catch {
        return $false
    }
}

function Install-DotNetDesktopRuntime {
    param(
        [string]$Channel = "10.0"
    )

    $bundleRoot = Split-Path -Parent $PSCommandPath
    $installScript = Join-Path $bundleRoot "prerequisites\dotnet-install.ps1"

    if (-not (Test-Path $installScript)) {
        throw ".NET install script not found in prerequisites folder. Ensure the installer bundle was built correctly."
    }

    Write-Host "Installing .NET $Channel Desktop Runtime..."
    & $installScript -Channel $Channel -Runtime dotnet -InstallDir "$env:LocalProgramFiles\dotnet" -NoPath
    if ($LASTEXITCODE -ne 0) {
        throw ".NET Desktop Runtime installation failed with exit code $LASTEXITCODE."
    }

    # Refresh PATH for this process so subsequent dotnet calls succeed.
    $dotnetPath = "$env:LocalProgramFiles\dotnet"
    if (-not ($env:PATH -split ';' | Where-Object { $_ -eq $dotnetPath })) {
        $env:PATH = "$dotnetPath;$env:PATH"
    }
}

function Test-WindowsAppSdkRuntime {
    param(
        [string]$Architecture
    )

    $packageNames = @(
        "Microsoft.WindowsAppRuntime.1.6"
        "MicrosoftCorporationII.WindowsAppRuntime.Main.1"
    )

    foreach ($name in $packageNames) {
        $installed = Get-AppxPackage -Name "$name*" -Architecture $Architecture -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $installed) {
            return $true
        }
    }

    return $false
}

function Install-WindowsAppSdkRuntime {
    param(
        [string]$Architecture
    )

    $bundleRoot = Split-Path -Parent $PSCommandPath
    $installer = Join-Path $bundleRoot "prerequisites\windowsappruntimeinstall-$Architecture.exe"

    if (-not (Test-Path $installer)) {
        Write-Warning "Windows App SDK runtime installer not found in prerequisites folder. Skipping runtime install; the MSIX may still install its own dependencies."
        return
    }

    Write-Host "Installing Windows App SDK runtime ($Architecture)..."
    $process = Start-Process -FilePath $installer -ArgumentList "--quiet" -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Windows App SDK runtime installation failed with exit code $($process.ExitCode)."
    }
}

function Get-HostArchitecture {
    try {
        if ($env:PROCESSOR_ARCHITEW6432) {
            return $env:PROCESSOR_ARCHITEW6432.ToLowerInvariant()
        }
        return $env:PROCESSOR_ARCHITECTURE.ToLowerInvariant()
    }
    catch {
        return "x64"
    }
}

function Install-Prerequisites {
    $architecture = switch (Get-HostArchitecture) {
        "amd64" { "x64" }
        "x86"   { "x86" }
        "arm64" { "arm64" }
        default { "x64" }
    }

    if (-not (Test-DotNetDesktopRuntime -Channel "10.0")) {
        Install-DotNetDesktopRuntime -Channel "10.0"
    }
    else {
        Write-Host ".NET 10 Desktop Runtime is already installed."
    }

    if (-not (Test-WindowsAppSdkRuntime -Architecture $architecture)) {
        Install-WindowsAppSdkRuntime -Architecture $architecture
    }
    else {
        Write-Host "Windows App SDK runtime is already installed."
    }
}

function Get-PackageFile {
    param(
        [string]$ExplicitPackagePath
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPackagePath)) {
        if (-not (Test-Path $ExplicitPackagePath)) {
            throw "MSIX package not found: $ExplicitPackagePath"
        }

        return (Resolve-Path $ExplicitPackagePath).Path
    }

    $bundleRoot = Split-Path -Parent $PSCommandPath
    $latest = Get-ChildItem -Path (Join-Path $bundleRoot "package") -Filter *.msix -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName

    if ([string]::IsNullOrWhiteSpace($latest)) {
        throw "No MSIX package found under $bundleRoot\package."
    }

    return $latest
}

function Get-PackageCertificatePath {
    param(
        [string]$PackageFile
    )

    $packageDirectory = Split-Path -Parent $PackageFile
    $certificatePath = Get-ChildItem -Path $packageDirectory -Filter *.cer -File -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName

    if ([string]::IsNullOrWhiteSpace($certificatePath)) {
        throw "No package certificate was found beside the MSIX file in $packageDirectory."
    }

    return $certificatePath
}

function Get-PackageIdentity {
    param(
        [string]$PackageFile
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $zip = [System.IO.Compression.ZipFile]::OpenRead($PackageFile)
    try {
        $entry = $zip.Entries | Where-Object { $_.FullName -eq "AppxManifest.xml" } | Select-Object -First 1
        if ($null -eq $entry) {
            throw "AppxManifest.xml was not found in package: $PackageFile"
        }

        $reader = New-Object System.IO.StreamReader($entry.Open())
        try {
            [xml]$manifestXml = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        $identity = $manifestXml.Package.Identity
        if ($null -eq $identity -or [string]::IsNullOrWhiteSpace($identity.Name) -or [string]::IsNullOrWhiteSpace($identity.Publisher)) {
            throw "Package identity information is missing from: $PackageFile"
        }

        return [pscustomobject]@{
            Name = $identity.Name
            Publisher = $identity.Publisher
        }
    }
    finally {
        $zip.Dispose()
    }
}

function Get-PackageDependencies {
    param(
        [string]$PackageFile
    )

    $packageDirectory = Split-Path -Parent $PackageFile
    $dependencyRoot = Join-Path $packageDirectory "Dependencies"
    if (-not (Test-Path $dependencyRoot)) {
        return @()
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    function Get-HostArchitecture {
        try {
            if ($env:PROCESSOR_ARCHITEW6432) {
                return $env:PROCESSOR_ARCHITEW6432.ToLowerInvariant()
            }

            return $env:PROCESSOR_ARCHITECTURE.ToLowerInvariant()
        }
        catch {
            return "x64"
        }
    }

    function Normalize-Architecture {
        param(
            [string]$Architecture
        )

        if ([string]::IsNullOrWhiteSpace($Architecture)) {
            return ""
        }

        switch ($Architecture.ToLowerInvariant()) {
            "amd64" { return "x64" }
            "win32" { return "x86" }
            default { return $Architecture.ToLowerInvariant() }
        }
    }

    function Get-ArchitecturePreference {
        param(
            [string]$Architecture
        )

        switch (Normalize-Architecture $Architecture) {
            "arm64" { return @("neutral", "arm64", "x64", "x86", "arm") }
            "arm" { return @("neutral", "arm", "x86") }
            "x86" { return @("neutral", "x86") }
            default { return @("neutral", "x64", "x86") }
        }
    }

    $preferredArchitectures = Get-ArchitecturePreference -Architecture (Get-HostArchitecture)
    $candidates = @()

    foreach ($file in Get-ChildItem -Path $dependencyRoot -Recurse -File -Include *.appx,*.msix) {
        $zip = [System.IO.Compression.ZipFile]::OpenRead($file.FullName)
        try {
            $entry = $zip.Entries | Where-Object { $_.FullName -eq "AppxManifest.xml" } | Select-Object -First 1
            if ($null -eq $entry) {
                continue
            }

            $reader = New-Object System.IO.StreamReader($entry.Open())
            try {
                [xml]$manifestXml = $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }

            $identity = $manifestXml.Package.Identity
            $arch = Normalize-Architecture $identity.ProcessorArchitecture
            $preferenceIndex = [array]::IndexOf($preferredArchitectures, $arch)
            if ($preferenceIndex -lt 0) {
                continue
            }

            $folderName = Normalize-Architecture (Split-Path -Leaf (Split-Path -Parent $file.FullName))
            $folderPenalty = if ($folderName -eq $arch) { 0 } else { 1 }

            $candidates += [pscustomobject]@{
                Key = "$($identity.Name)|$($identity.Publisher)"
                FilePath = $file.FullName
                PreferenceIndex = $preferenceIndex
                FolderPenalty = $folderPenalty
            }
        }
        finally {
            $zip.Dispose()
        }
    }

    return @(
        $candidates |
        Sort-Object Key, PreferenceIndex, FolderPenalty, FilePath |
        Group-Object Key |
        ForEach-Object { $_.Group[0].FilePath }
    )
}

function Remove-ConflictingUnpackagedInstallations {
    param(
        [string]$PackageFile
    )

    $identity = Get-PackageIdentity -PackageFile $PackageFile
    $conflicts = @(
        Get-AppxPackage -Name $identity.Name -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Publisher -eq $identity.Publisher -and
            ([string]$_.SignatureKind -eq "None" -or [string]::IsNullOrWhiteSpace([string]$_.SignatureKind))
        } |
        Group-Object PackageFullName |
        ForEach-Object { $_.Group[0] }
    )

    if ($conflicts.Count -eq 0) {
        return
    }

    foreach ($conflict in $conflicts) {
        Write-Host "Removing existing unpackaged LabelDesigner registration:"
        Write-Host $conflict.PackageFullName

        try {
            Remove-AppxPackage -Package $conflict.PackageFullName -PreserveApplicationData
        }
        catch {
            $message = $_.Exception.Message
            throw "Failed to remove the existing unpackaged LabelDesigner registration '$($conflict.PackageFullName)'. Close LabelDesigner if it is running, then remove it manually with 'Get-AppxPackage -Name $($identity.Name) | Remove-AppxPackage -PreserveApplicationData'. $message"
        }
    }
}

function Ensure-PackageCertificateTrusted {
    param(
        [string]$CertificateFile
    )

    $certificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($CertificateFile)
    $trustedPeoplePath = "Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint)"
    $rootStorePath = "Cert:\LocalMachine\Root\$($certificate.Thumbprint)"

    if (-not (Test-Path $trustedPeoplePath)) {
        Write-Host "Importing package certificate into LocalMachine\TrustedPeople..."
        Import-Certificate -FilePath $CertificateFile -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null
    }

    if ($certificate.Subject -eq $certificate.Issuer -and -not (Test-Path $rootStorePath)) {
        Write-Host "Importing self-signed package certificate into LocalMachine\Root..."
        Import-Certificate -FilePath $CertificateFile -CertStoreLocation "Cert:\LocalMachine\Root" | Out-Null
    }
}

function Install-Package {
    param(
        [string]$PackageFile
    )

    Install-Prerequisites

    $certificatePath = Get-PackageCertificatePath -PackageFile $PackageFile
    $dependencyPaths = Get-PackageDependencies -PackageFile $PackageFile

    Remove-ConflictingUnpackagedInstallations -PackageFile $PackageFile
    Ensure-PackageCertificateTrusted -CertificateFile $certificatePath

    Write-Host "Installing package:"
    Write-Host $PackageFile

    try {
        if ($dependencyPaths.Count -gt 0) {
            Add-AppxPackage -Path $PackageFile -DependencyPath $dependencyPaths -ForceApplicationShutdown
        }
        else {
            Add-AppxPackage -Path $PackageFile -ForceApplicationShutdown
        }
    }
    catch {
        $message = $_.Exception.Message
        throw "MSIX installation failed. $message"
    }
}

function Invoke-SelfElevated {
    param(
        [string]$PackageFile
    )

    $powerShellPath = (Get-Process -Id $PID).Path
    $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Elevated"
    if (-not [string]::IsNullOrWhiteSpace($PackageFile)) {
        $arguments += " -PackagePath `"$PackageFile`""
    }
    $failureLogPath = Join-Path $env:TEMP ("LabelDesignerInstaller-" + [guid]::NewGuid().ToString("N") + ".log")
    $arguments += " -LogPath `"$failureLogPath`""

    Write-Host "Windows will show a UAC prompt so the package certificate can be trusted for installation."

    try {
        $process = Start-Process -FilePath $powerShellPath -Verb RunAs -ArgumentList $arguments -Wait -PassThru
    }
    catch [System.InvalidOperationException] {
        throw "Installation was cancelled before it started. Re-run Install-LabelDesigner.cmd and choose Yes on the Windows UAC prompt."
    }

    if ($process.ExitCode -ne 0) {
        if (Test-Path $failureLogPath) {
            $details = Get-Content -Path $failureLogPath -Raw
            throw "Package installer failed with exit code $($process.ExitCode).`n`n$details`nLog: $failureLogPath"
        }

        throw "Package installer failed with exit code $($process.ExitCode)."
    }
}

$resolvedPackagePath = Get-PackageFile -ExplicitPackagePath $PackagePath

try {
    if ($Elevated -or (Test-IsElevated)) {
        Install-Package -PackageFile $resolvedPackagePath
    }
    else {
        Invoke-SelfElevated -PackageFile $resolvedPackagePath
    }

    Write-Host ""
    Write-Host "Install complete."
}
catch {
    if ($Elevated) {
        Write-InstallerFailureLog -ErrorRecord $_ -FailureLogPath $LogPath
    }

    throw
}
'@

    $installCommand = @'
@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-LabelDesigner.ps1"
if errorlevel 1 (
    echo.
    echo Installation failed.
    pause
    exit /b 1
)

echo.
echo Installation complete.
pause
'@

    $readme = @'
LabelDesigner Target Installer
==============================

Use this folder on the target machine. Do not copy the repository installer scripts.

Recommended:
  1. Extract this folder if it came from the ZIP file.
  2. Run Install-LabelDesigner.cmd

What the installer does:
  - checks for the .NET 10 Desktop Runtime and installs it if missing
  - checks for the Windows App SDK runtime and installs it if missing
  - requests elevation so Windows can trust the package certificate
  - imports the package .cer into LocalMachine\TrustedPeople
  - imports self-signed package certificates into LocalMachine\Root
  - installs dependency packages from the Dependencies folder
  - installs or updates LabelDesigner

Important:
  - when Windows shows the UAC prompt, choose Yes
  - if you choose No, installation is cancelled and nothing is installed
  - the bundle does not use Add-AppDevPackage.ps1; it installs the cert and package directly
  - an internet connection is required if .NET 10 or Windows App SDK runtimes need to be downloaded

If Windows blocks PowerShell script execution, use Install-LabelDesigner.cmd.
'@

    Set-Content -Path (Join-Path $bundleDirectory "Install-LabelDesigner.ps1") -Value $installScript -Encoding UTF8
    Set-Content -Path (Join-Path $bundleDirectory "Install-LabelDesigner.cmd") -Value $installCommand -Encoding ASCII
    Set-Content -Path (Join-Path $bundleDirectory "README.txt") -Value $readme -Encoding ASCII

    Compress-Archive -Path (Join-Path $bundleDirectory '*') -DestinationPath $zipPath -Force

    return @{
        Directory = $bundleDirectory
        ZipPath = $zipPath
    }
}

if (-not (Test-Path $projectPath)) {
    throw "Project not found: $projectPath"
}

if (-not (Test-Path $manifestPath)) {
    throw "Package manifest not found: $manifestPath"
}

New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
New-Item -ItemType Directory -Path $bundleOutputDir -Force | Out-Null
if (Test-Path $stagingRoot) {
    Remove-Item -Path $stagingRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

$packagePublisher = Get-ManifestIdentityValue -ManifestFile $manifestPath -AttributeName "Publisher"
$resolvedPackageVersion = Get-ResolvedPackageVersion -ManifestFile $manifestPath -RequestedVersion $PackageVersion
$originalManifestContent = Get-Content -Path $manifestPath -Raw
$originalManifestVersion = Get-ManifestIdentityValue -ManifestFile $manifestPath -AttributeName "Version"
$manifestVersionOverridden = $false

if ([string]::IsNullOrWhiteSpace($CertificatePath)) {
    $generatedCertificate = New-TemporarySigningCertificate -Subject $packagePublisher -OutputDirectory $generatedAssetsDir
    $generatedCertificatePublicPath = $generatedCertificate.CerPath
    $generatedCertificateThumbprint = $generatedCertificate.Thumbprint
}

$msbuildArgs = @(
    $projectPath,
    "/t:Restore,Build",
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform",
    "/p:GenerateAppxPackageOnBuild=true",
    "/p:UapAppxPackageBuildMode=SideloadOnly",
    "/p:AppxBundle=Never",
    "/p:AppxPackageDir=$appxPackageDir",
    "/p:InstallerBuild=true"
)

if (-not [string]::IsNullOrWhiteSpace($generatedCertificateThumbprint)) {
    $msbuildArgs += "/p:AppxPackageSigningEnabled=true"
    $msbuildArgs += "/p:PackageCertificateThumbprint=$generatedCertificateThumbprint"
}
elseif (-not [string]::IsNullOrWhiteSpace($CertificatePath)) {
    if (-not (Test-Path $CertificatePath)) {
        throw "Certificate file not found: $CertificatePath"
    }
    $msbuildArgs += "/p:AppxPackageSigningEnabled=true"
    $msbuildArgs += "/p:PackageCertificateKeyFile=$CertificatePath"
    if (-not [string]::IsNullOrWhiteSpace($CertificatePassword)) {
        $msbuildArgs += "/p:PackageCertificatePassword=$CertificatePassword"
    }
}

Write-Host "Building MSIX package..."
try {
    if ($originalManifestVersion -ne $resolvedPackageVersion) {
        Set-ManifestIdentityValue -ManifestFile $manifestPath -AttributeName "Version" -Value $resolvedPackageVersion
        $manifestVersionOverridden = $true
    }

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
    Remove-Item -Path (Join-Path $outputDir "_generated") -Recurse -Force -ErrorAction SilentlyContinue
    $builtPackages = Get-ChildItem -Path $artifactRoot -Filter *.msix -Recurse | Sort-Object LastWriteTime -Descending

    if (-not [string]::IsNullOrWhiteSpace($generatedCertificatePublicPath) -and (Test-Path $generatedCertificatePublicPath)) {
        Copy-Item -Path $generatedCertificatePublicPath -Destination $outputDir -Force
    }
    elseif (-not [string]::IsNullOrWhiteSpace($CertificatePath)) {
        $publicCertificatePath = [System.IO.Path]::ChangeExtension($CertificatePath, ".cer")
        if (Test-Path $publicCertificatePath) {
            Copy-Item -Path $publicCertificatePath -Destination $outputDir -Force
        }
    }

    $prerequisitesDir = Join-Path $stagingRoot "_prerequisites"
    $dotNetInstallScript = Get-DotNetInstallScript -OutputDirectory $prerequisitesDir
    $windowsAppSdkInstaller = Get-WindowsAppSdkRuntimeInstaller -OutputDirectory $prerequisitesDir -Architecture $Platform

    $installerBundle = New-StandaloneInstallerBundle `
        -PackageFile $builtPackages[0].FullName `
        -BundleRoot $bundleOutputDir `
        -DotNetInstallScript $dotNetInstallScript `
        -WindowsAppSdkInstaller $windowsAppSdkInstaller

    Write-Host "MSIX package created:"
    Write-Host $builtPackages[0].FullName
    Write-Host "Package version:"
    Write-Host $resolvedPackageVersion
    Write-Host "Target-machine installer bundle:"
    Write-Host $installerBundle.Directory
    Write-Host "Target-machine installer zip:"
    Write-Host $installerBundle.ZipPath
    if (-not [string]::IsNullOrWhiteSpace($generatedCertificateThumbprint)) {
        Write-Host "Generated signing certificate thumbprint:"
        Write-Host $generatedCertificateThumbprint
    }
}
finally {
    if ($manifestVersionOverridden) {
        Set-Content -Path $manifestPath -Value $originalManifestContent -Encoding UTF8
    }
}
