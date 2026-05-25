# Releasing Markdown Studio

Markdown Studio ships through the Microsoft Store (Partner Center app
`9PK8FQXH4JKZ`). Each release is built locally by `build-release.ps1`
into an unsigned `.msixupload` package, then uploaded by hand to a new
submission in Partner Center. The Store signs the package with a
Microsoft-issued cert during certification — end users get the package
signed by Microsoft.

There's no self-signed cert to maintain. F5 in Visual Studio handles
dev-build signing automatically with its own temp key.

## Cutting a release

1. **Build the upload package.** From the repo root, in a Developer
   PowerShell for VS 2022 (so `msbuild` is on PATH):

   ```powershell
   pwsh ./build-release.ps1 -Version 0.1.0
   ```

   The script stamps the version into the manifest, builds x64+ARM64
   into a single unsigned `.msixupload`, and copies it to `./release/`.
   The manifest version bump is reverted at the end so the working
   tree stays clean.

2. **Submit to Partner Center.**
   - Open <https://partner.microsoft.com/dashboard/products/9PK8FQXH4JKZ>.
   - Start a new submission.
   - **Packages** → drag the `MarkdownStudio-<version>-x64-arm64.msixupload`
     from `./release/` into the upload area.
   - Fill out the remaining sections (description, screenshots, age
     rating, pricing, markets) for the first submission. On subsequent
     submissions most of these carry over.
   - Submit. Certification typically takes 24–72 hours for the first
     submission, faster on updates.

3. **Tag the commit** so the published version maps back to source:

   ```powershell
   git tag v0.1.0
   git push origin v0.1.0
   ```

## After certification

The app shows up at <https://apps.microsoft.com/detail/9PK8FQXH4JKZ>
and is installable via the Store app or `winget install`. Users
upgrading get the new version automatically through the Store's update
service.

## Notes

- **The unsigned `.msixupload` can't be sideloaded for local smoke
  testing.** Windows refuses to install a bundle with no valid
  signature. For day-to-day verification use F5 in Visual Studio
  (which signs with VS's own dev cert), or upload to Partner Center
  and use a flighting track for pre-release builds.
- **Don't edit the Identity Name or Publisher** in
  `MarkdownStudio/Package.appxmanifest`. They have to match exactly
  what's registered for Store ID `9PK8FQXH4JKZ` in Partner Center, or
  certification rejects the submission.
