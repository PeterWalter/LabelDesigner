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

    $installScript = Join-Path $env:TEMP "dotnet-install.ps1"

    if (-not (Test-Path $installScript)) {
        Write-Host "Downloading .NET install script..."
        Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript -UseBasicParsing
    }

    Write-Host "Installing .NET $Channel Desktop Runtime..."
    & $installScript -Channel $Channel -Runtime dotnet -InstallDir "$env:LocalProgramFiles\dotnet" -NoPath
    if ($LASTEXITCODE -ne 0) {
        throw ".NET Desktop Runtime installation failed with exit code $LASTEXITCODE."
    }

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

    $installer = Join-Path $env:TEMP "windowsappruntimeinstall-$Architecture.exe"
    Write-Host "Downloading Windows App SDK runtime installer..."
    Invoke-WebRequest -Uri "https://aka.ms/windowsappsdk/2.1/2.1.3/windowsappruntimeinstall-$Architecture.exe" -OutFile $installer -UseBasicParsing

    Write-Host "Installing Windows App SDK runtime ($Architecture)..."
    $process = Start-Process -FilePath $installer -ArgumentList "--quiet" -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Windows App SDK runtime installation failed with exit code $($process.ExitCode)."
    }
}

function Install-Prerequisites {
    $architecture = switch ($env:PROCESSOR_ARCHITECTURE.ToLowerInvariant()) {
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
        throw "Installation was cancelled before it started. Re-run the installer and choose Yes on the Windows UAC prompt."
    }

    if ($process.ExitCode -ne 0) {
        if (Test-Path $failureLogPath) {
            $details = Get-Content -Path $failureLogPath -Raw
            throw "Package installer failed with exit code $($process.ExitCode).`n`n$details`nLog: $failureLogPath"
        }

        throw "Package installer failed with exit code $($process.ExitCode)."
    }
}

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

try {
    if ($Elevated -or (Test-IsElevated)) {
        Install-Package -PackageFile $PackagePath
    }
    else {
        Invoke-SelfElevated -PackageFile $PackagePath
    }

    Write-Host "Install complete."
}
catch {
    if ($Elevated) {
        Write-InstallerFailureLog -ErrorRecord $_ -FailureLogPath $LogPath
    }

    throw
}
