# Privacy Policy

*Last updated: 25 May 2026*

Markdown Studio is a Windows Markdown editor that runs entirely on your local
device. This policy describes what data the app handles and, more importantly,
what it does **not** do with it.

## Summary

- **The app does not collect, transmit, or store any data on remote servers.**
- All settings, recents, and file content stay on your local device.
- The app has no telemetry, no analytics, no crash reporting, no ads, no
  account system, and no network calls of its own.
- The author has no access to anything you do in the app.

## What the app stores locally

When you use Markdown Studio, the app writes a small amount of data under
Windows' standard per-app data location:

```
%LOCALAPPDATA%\Packages\User.MarkdownStudio_<hash>\LocalState\
```

What lives there:

- **Recently opened files and folders** — used to populate the Welcome page's
  "Recent" list. Each entry is the file or folder path you chose to open.
- **Preferences** — your chosen theme, editor font and size, preview
  typography, keyboard shortcut bindings, last view mode (Editor / Split /
  Preview), and other settings you change in Settings.
- **A copy of the bundled `Sample.md`** — copied to LocalState on first
  launch so it's writable. You can edit or delete it freely.

This data never leaves your device. Uninstalling the app removes the entire
LocalState folder along with the package, deleting everything above.

## What the app does *not* do

- No network requests. Markdown Studio does not contact any remote server
  for telemetry, analytics, license checks, advertisements, automatic
  update checks, or any other purpose. App updates are delivered by the
  Microsoft Store itself, not by the app.
- No background activity. The app only runs while you have it open.
- No third-party SDKs that collect or transmit information.
- No access to data outside the files and folders you explicitly open.

## Files you open and edit

Files you open and save stay where you put them. The app does not upload,
copy, mirror, scan, index for remote search, or otherwise transmit the
contents of your Markdown files anywhere.

## WebView2 (Microsoft Edge runtime)

The editor and preview surfaces are hosted in Microsoft's WebView2 control,
which is a system component installed and managed by Microsoft. WebView2
itself may collect diagnostic data under Microsoft's own privacy practices.
Markdown Studio does not pass your file contents or activity to WebView2's
diagnostic stream beyond what's required to render the page locally.

See Microsoft's Privacy Statement at <https://privacy.microsoft.com/privacystatement>
for details on WebView2 / Edge data handling.

## Children

The app is suitable for general audiences and does not knowingly collect
data from anyone — including children.

## Contact

If you have questions about this policy or the app, please open an issue at
<https://github.com/justinswork/MarkdownStudio/issues>.

## Changes to this policy

If the policy ever changes, the updated version will be published at the
same URL you're reading this one at, and the "Last updated" date at the
top of the document will reflect the change.
