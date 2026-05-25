<#
.SYNOPSIS
    Build and stage a Microsoft Store upload package locally.

.DESCRIPTION
    Stamps the version into Package.appxmanifest, builds an unsigned
    x64+ARM64 Store-upload bundle (.msixupload), and copies it into
    .\release\ ready to be manually uploaded to Partner Center for the
    reserved app (Store ID 9PK8FQXH4JKZ).

    The package is intentionally unsigned: Partner Center accepts
    unsigned uploads and the Store signs the final package with a
    Microsoft-issued cert during certification. Skipping local signing
    means no self-signed cert to generate or rotate.

    Note: the unsigned .msixupload can't be sideloaded for local testing
    (its inner .msixbundle has no signature Windows will trust). For
    day-to-day debugging use F5 in Visual Studio — VS signs dev builds
    with its own temporary key.

    The manifest version bump is reverted after the build so it doesn't
    show up as a stray change in your working tree.

.PARAMETER Version
    Release version. Accepts "v0.1.0", "0.1.0", or any leading-v variant.
    Internally padded to a 4-part MSIX version (e.g. 0.1.0.0).

.EXAMPLE
    pwsh ./build-release.ps1 -Version 0.1.0

.NOTES
    Run from a Developer PowerShell (or any shell where msbuild is on PATH).
    See docs/RELEASING.md for the Partner Center submission steps.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version
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

# --- Locate msbuild --------------------------------------------------------
$msbuild = (Get-Command msbuild -ErrorAction SilentlyContinue).Source
if (-not $msbuild) {
    throw "msbuild not on PATH. Run this from a Developer PowerShell, or add MSBuild from VS 2022 to PATH."
}

# --- Stamp manifest version (reverted in finally) --------------------------
# We do this with a regex replace on the raw text rather than [xml]…$xml.Save()
# because the latter reformats the whole manifest (joins attributes onto a
# single line, strips blank lines, prepends a BOM), which leaves a noisy diff
# in the working tree even after the finally-block restore.
$utf8NoBom    = New-Object System.Text.UTF8Encoding $false
$manifestPath = Join-Path $PSScriptRoot 'MarkdownStudio\Package.appxmanifest'
$originalManifest = [System.IO.File]::ReadAllText($manifestPath, [System.Text.Encoding]::UTF8)
$stampedManifest  = [regex]::Replace(
    $originalManifest,
    '(<Identity\b[^>]*\bVersion=")[\d.]+(")',
    "`${1}$msixVersion`${2}")
if ($stampedManifest -eq $originalManifest) {
    throw "Couldn't find an Identity Version= attribute in $manifestPath to stamp."
}
[System.IO.File]::WriteAllText($manifestPath, $stampedManifest, $utf8NoBom)

try {
    # --- Clean previous build output --------------------------------------
    # We wipe bin/ and obj/ alongside appxout/ because MSBuild's incremental
    # build will happily reuse a cached AppxManifest.xml from a prior run
    # (Partner Center caught this once: the on-disk manifest was correct
    # but the .msixupload still carried the stale display name).
    $appxOut = Join-Path $PSScriptRoot 'appxout'
    $binDir  = Join-Path $PSScriptRoot 'MarkdownStudio\bin'
    $objDir  = Join-Path $PSScriptRoot 'MarkdownStudio\obj'
    foreach ($d in @($appxOut, $binDir, $objDir)) {
        if (Test-Path $d) { Remove-Item -Recurse -Force $d }
    }

    # --- Build the bundle --------------------------------------------------
    # AppxBundle=Always + AppxBundlePlatforms="x64|ARM64" merges both
    # architectures into a single .msixbundle. UapAppxPackageBuildMode=
    # SideloadOnly produces a sideload package (not the .appxupload Store
    # format). MSBuild signs in-place via PackageCertificateThumbprint,
    # which it looks up in Cert:\CurrentUser\My.
    # GenerateAppxPackageOnBuild=true makes the regular Build target also
    # invoke the MSIX packaging targets. UapAppxPackageBuildMode=StoreUpload
    # produces a .msixupload (a zip containing the .msixbundle plus per-arch
    # symbol packages) — the format Partner Center ingests. Signing is off:
    # Partner Center accepts unsigned uploads and the Store signs the final
    # package with a Microsoft-issued cert during certification.
    & $msbuild "$PSScriptRoot\MarkdownStudio\MarkdownStudio.csproj" `
        /restore `
        /p:Configuration=Release `
        /p:Platform=x64 `
        /p:GenerateAppxPackageOnBuild=true `
        /p:AppxBundle=Always `
        /p:AppxBundlePlatforms="x64|ARM64" `
        /p:UapAppxPackageBuildMode=StoreUpload `
        /p:GenerateAppInstallerFile=False `
        /p:AppxAutoIncrementPackageRevision=false `
        /p:AppxPackageSigningEnabled=false `
        /p:AppxPackageDir="$appxOut\"

    if ($LASTEXITCODE -ne 0) { throw "msbuild failed with exit code $LASTEXITCODE." }

    # --- Stage artifacts --------------------------------------------------
    $upload = Get-ChildItem -Path $appxOut -Filter '*.msixupload' -Recurse | Select-Object -First 1
    if (-not $upload) { throw "Build finished but no .msixupload was produced." }

    $releaseDir = Join-Path $PSScriptRoot 'release'
    if (-not (Test-Path $releaseDir)) { New-Item -ItemType Directory -Path $releaseDir | Out-Null }

    $uploadName = "MarkdownStudio-$msixVersion-x64-arm64.msixupload"
    Copy-Item $upload.FullName (Join-Path $releaseDir $uploadName) -Force

    Write-Host ""
    Write-Host "Build complete." -ForegroundColor Green
    Write-Host "Artifact: $(Join-Path $releaseDir $uploadName)"
    Write-Host ""
    Write-Host "Next:"
    Write-Host "  1. https://partner.microsoft.com/dashboard/products/9PK8FQXH4JKZ"
    Write-Host "  2. Packages -> upload $uploadName"
    Write-Host "  3. Fill out the rest of the submission (description, screenshots,"
    Write-Host "     age rating, pricing) -> Submit to the Store."
    Write-Host "  4. Optionally: git tag $tagName && git push origin $tagName"
}
finally {
    # Revert the manifest so the local version bump isn't a stray uncommitted
    # change. (We deliberately don't commit the bump — releases are tagged on
    # the canonical commit, and the manifest only carries a real version at
    # build time.)
    [System.IO.File]::WriteAllText($manifestPath, $originalManifest, $utf8NoBom)
}
