# Releasing Markdown Studio

Releases are built locally by `build-release.ps1`, then manually uploaded
to a GitHub Release. The signing cert never leaves your machine — it
lives in your user-account certificate store and the build script looks
it up by Subject.

## One-time setup

### 1. Generate the self-signed signing certificate

The cert's Subject **must** match the `Publisher` field in
`MarkdownStudio/Package.appxmanifest`. Today that's `CN=MarkdownStudio`.
If you change the manifest publisher you have to regenerate the cert.

Run this in PowerShell on Windows:

```powershell
# 5-year self-signed code-signing cert. It's installed into your user's
# certificate store; the private key never lands on disk.
New-SelfSignedCertificate `
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
```

That's it — no PFX file, no password, no GitHub Secrets. The cert is
in `Cert:\CurrentUser\My` and `build-release.ps1` finds it by Subject.

### 2. (Optional) Export the public .cer for your records

`build-release.ps1` already emits a fresh `.cer` next to every bundle
it produces (MSBuild does this automatically). If you want a standalone
copy to share:

```powershell
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object Subject -eq "CN=MarkdownStudio" | Select-Object -First 1
$cert | Export-Certificate -FilePath "MarkdownStudio.cer" -Type CERT
```

## Cutting a release

1. From the repo root, in a **Developer PowerShell for VS 2022** (so
   `msbuild` is on PATH):

   ```powershell
   pwsh ./build-release.ps1 -Version 0.1.0
   ```

   The script:
   - Stamps version `0.1.0.0` into `Package.appxmanifest` (reverted at
     end so your working tree stays clean).
   - Builds a signed x64+ARM64 `.msixbundle`.
   - Copies the bundle and matching `.cer` into `./release/`.

2. Tag the commit and push:

   ```powershell
   git tag v0.1.0
   git push origin v0.1.0
   ```

3. Create the GitHub Release in the UI:

   - Go to **Releases → Draft a new release**.
   - Choose the `v0.1.0` tag.
   - Drag both files from `./release/` into the attachments area.
   - Paste this into the release body (adjust version numbers):

     ```markdown
     ## Install

     Markdown Studio is signed with a self-signed certificate. Windows
     needs to trust it once before the installer will run.

     1. Download **`MarkdownStudio-0.1.0.0.cer`** below.
     2. Right-click → **Install Certificate** → **Local Machine** →
        **Place all certificates in the following store** →
        **Trusted Root Certification Authorities** → Finish.
     3. Download **`MarkdownStudio-0.1.0.0-x64-arm64.msixbundle`** below.
     4. Double-click to install.

     (Only needed on first install. Future versions install directly.)
     ```

   - Publish.

## Rotating the certificate

Self-signed certs expire (5 years in our setup). When you regenerate,
the new cert must keep `CN=MarkdownStudio` (so it still matches the
manifest publisher), and existing users will need to install the new
`.cer` before upgrading. Plan the rotation alongside a versioned
upgrade announcement.

If the old cert is still in your store, delete it after the new one is
working — `build-release.ps1` picks the cert with the *latest* `NotAfter`
when multiple match the Subject, so leaving an expired one in place is
harmless but messy.
