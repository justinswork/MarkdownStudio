<#
.SYNOPSIS
    Build, sign, and stage a release MSIX bundle locally.

.DESCRIPTION
    Looks up the signing cert by Subject in the current user's certificate
    store, stamps the version into Package.appxmanifest, builds an x64+ARM64
    .msixbundle, signs it, and copies the bundle plus the public .cer into
    .\release\ ready to be manually uploaded to a GitHub Release.

    The manifest version bump is reverted after the build so it doesn't
    show up as a stray change in your working tree.

.PARAMETER Version
    Release version. Accepts "v0.1.0", "0.1.0", or any leading-v variant.
    Internally padded to a 4-part MSIX version (e.g. 0.1.0.0).

.PARAMETER CertSubject
    Subject of the code-signing certificate to look up in
    Cert:\CurrentUser\My. Must match the Publisher in
    MarkdownStudio\Package.appxmanifest. Defaults to "CN=MarkdownStudio".

.EXAMPLE
    pwsh ./build-release.ps1 -Version 0.1.0

.NOTES
    Run from a Developer PowerShell (or any shell where msbuild is on PATH).
    See docs/RELEASING.md for the one-time cert setup.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$CertSubject = 'CN=MarkdownStudio'
)

$ErrorActionPreference = 'Stop'

# --- Normalize version: "v0.1.0" → "0.1.0.0" -------------------------------
$semver = $Version.TrimStart('v')
$parts  = $semver -split '\.'
while ($parts.Count -lt 4) { $parts += '0' }
if ($parts.Count -gt 4) { throw "Version must have at most 4 parts; got '$Version'." }
$msixVersion = ($parts[0..3] -join '.')
$tagName     = if ($Version.StartsWith('v')) { $Version } else { "v$semver" }
Write-Host "Building MarkdownStudio $msixVersion (tag $tagName)"

# --- Find cert in user's cert store ----------------------------------------
$cert = Get-ChildItem -Path Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $CertSubject } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1
if (-not $cert) {
    throw "No code-signing cert with Subject '$CertSubject' in Cert:\CurrentUser\My. See docs/RELEASING.md."
}
Write-Host "Using cert: $($cert.Subject)"
Write-Host "  Thumbprint: $($cert.Thumbprint)"
Write-Host "  Expires:    $($cert.NotAfter.ToString('yyyy-MM-dd'))"

# --- Locate msbuild --------------------------------------------------------
$msbuild = (Get-Command msbuild -ErrorAction SilentlyContinue).Source
if (-not $msbuild) {
    throw "msbuild not on PATH. Run this from a Developer PowerShell, or add MSBuild from VS 2022 to PATH."
}

# --- Stamp manifest version (reverted in finally) --------------------------
$manifestPath = Join-Path $PSScriptRoot 'MarkdownStudio\Package.appxmanifest'
$xml = [xml](Get-Content $manifestPath)
$originalVersion = $xml.Package.Identity.Version
$xml.Package.Identity.Version = $msixVersion
$xml.Save((Resolve-Path $manifestPath))

try {
    # --- Clean previous build output --------------------------------------
    $appxOut = Join-Path $PSScriptRoot 'appxout'
    if (Test-Path $appxOut) { Remove-Item -Recurse -Force $appxOut }

    # --- Build the bundle --------------------------------------------------
    # AppxBundle=Always + AppxBundlePlatforms="x64|ARM64" merges both
    # architectures into a single .msixbundle. UapAppxPackageBuildMode=
    # SideloadOnly produces a sideload package (not the .appxupload Store
    # format). MSBuild signs in-place via PackageCertificateThumbprint,
    # which it looks up in Cert:\CurrentUser\My.
    & $msbuild "$PSScriptRoot\MarkdownStudio\MarkdownStudio.csproj" `
        /restore `
        /p:Configuration=Release `
        /p:Platform=x64 `
        /p:AppxBundle=Always `
        /p:AppxBundlePlatforms="x64|ARM64" `
        /p:UapAppxPackageBuildMode=SideloadOnly `
        /p:GenerateAppInstallerFile=False `
        /p:AppxPackageSigningEnabled=true `
        /p:PackageCertificateThumbprint=$($cert.Thumbprint) `
        /p:AppxPackageDir="$appxOut\"

    if ($LASTEXITCODE -ne 0) { throw "msbuild failed with exit code $LASTEXITCODE." }

    # --- Stage artifacts --------------------------------------------------
    $bundle = Get-ChildItem -Path $appxOut -Filter '*.msixbundle' -Recurse | Select-Object -First 1
    $cer    = Get-ChildItem -Path $appxOut -Filter '*.cer'        -Recurse | Select-Object -First 1
    if (-not $bundle) { throw "Build finished but no .msixbundle was produced." }
    if (-not $cer)    { throw "Build finished but no .cer was produced." }

    $releaseDir = Join-Path $PSScriptRoot 'release'
    if (-not (Test-Path $releaseDir)) { New-Item -ItemType Directory -Path $releaseDir | Out-Null }

    $bundleName = "MarkdownStudio-$msixVersion-x64-arm64.msixbundle"
    $cerName    = "MarkdownStudio-$msixVersion.cer"
    Copy-Item $bundle.FullName (Join-Path $releaseDir $bundleName) -Force
    Copy-Item $cer.FullName    (Join-Path $releaseDir $cerName)    -Force

    Write-Host ""
    Write-Host "Build complete." -ForegroundColor Green
    Write-Host "Artifacts in: $releaseDir"
    Write-Host "  - $bundleName"
    Write-Host "  - $cerName"
    Write-Host ""
    Write-Host "Next:"
    Write-Host "  1. git tag $tagName && git push origin $tagName"
    Write-Host "  2. Open https://github.com/justinswork/MarkdownStudio/releases/new?tag=$tagName"
    Write-Host "  3. Drag both files in, paste install instructions from docs/RELEASING.md."
}
finally {
    # Revert the manifest so the local version bump isn't a stray uncommitted
    # change. (We deliberately don't commit the bump — releases are tagged on
    # the canonical commit, and the manifest only carries a real version at
    # build time.)
    $xml = [xml](Get-Content $manifestPath)
    $xml.Package.Identity.Version = $originalVersion
    $xml.Save((Resolve-Path $manifestPath))
}
