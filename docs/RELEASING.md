# Releasing Markdown Studio

Releases are produced by GitHub Actions (`.github/workflows/release.yml`)
on any pushed tag matching `v*`. The workflow builds an x64 + ARM64
`.msixbundle`, signs it with the project's self-signed certificate, and
attaches both the bundle and the public `.cer` to a GitHub Release.

## One-time setup

You only need to do this once. After it's done, every release is just
`git tag v0.x.y && git push origin v0.x.y`.

### 1. Generate the self-signed signing certificate

The cert's Subject **must** match the `Publisher` field in
`MarkdownStudio/Package.appxmanifest`. Today that's `CN=MarkdownStudio`.
If you change the manifest publisher you have to regenerate the cert.

Run this in PowerShell on Windows:

```powershell
# 5-year self-signed code-signing cert
$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject "CN=MarkdownStudio" `
    -KeyUsage DigitalSignature `
    -FriendlyName "MarkdownStudio Code Signing" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -NotAfter (Get-Date).AddYears(5) `
    -TextExtension @(
        "2.5.29.37={text}1.3.6.1.5.5.7.3.3",   # EKU: code signing
        "2.5.29.19={text}"                      # Basic constraints
    )

# Export the public cert (this is what end users install to Trusted Root).
# Safe to attach to releases; do not commit to the repo (.gitignore covers it).
$cert | Export-Certificate -FilePath "MarkdownStudio.cer" -Type CERT

# Export the PFX (private key) with a password you keep secret.
# This file goes into a GitHub Actions secret, then delete the local copy.
$pwd = Read-Host -AsSecureString -Prompt "PFX password (used in CI secret)"
$cert | Export-PfxCertificate -FilePath "MarkdownStudio.pfx" -Password $pwd
```

### 2. Upload the PFX to GitHub Secrets

```powershell
# Copy the base64-encoded PFX to your clipboard
[Convert]::ToBase64String([IO.File]::ReadAllBytes("MarkdownStudio.pfx")) | Set-Clipboard
```

In the GitHub repo → **Settings** → **Secrets and variables** → **Actions**
→ **New repository secret**, create:

- **`SIGNING_CERT_BASE64`** — paste the base64 string from your clipboard.
- **`SIGNING_CERT_PASSWORD`** — the plaintext password you set above.

### 3. Delete the local PFX

Once both secrets are saved, delete `MarkdownStudio.pfx` from your machine.
The CI runner is the only place that needs the private key. (The `.cer`
public cert can stay around — you'll attach it to releases.)

```powershell
Remove-Item MarkdownStudio.pfx
```

## Cutting a release

```powershell
# Bump version in the commit message / changelog if you want.
git tag v0.1.0
git push origin v0.1.0
```

Watch the **Actions** tab. On success, the **Releases** page will have a
new release with two attached files:

- `MarkdownStudio-0.1.0.0-x64-arm64.msixbundle`
- `MarkdownStudio-0.1.0.0.cer`

The release body already contains the install instructions for end users.

## Rotating the certificate

Self-signed certs expire (5 years in our setup). When you regenerate, the
manifest `Publisher` must still match the new cert's `CN`, and existing
users will need to install the new `.cer` before upgrading. Plan the cert
rotation alongside a versioned upgrade announcement.
