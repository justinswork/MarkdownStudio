# Markdown Studio

A premium native Windows markdown editor — built with **WinUI 3** and **.NET 10**, with a Monaco editor on one side, a live preview on the other, and a few opinionated features designed to make working with markdown feel direct rather than fiddly.

The headline feature is **X-ray edit**: a way to peek through the rendered preview and edit any block's raw markdown right where it sits.

---

## ⌧ X-ray edit

![X-ray edit in action](docs/MarkdownXRay.gif)

You're reading the rendered preview. You spot a typo, or want to bold a word, or fix a link. Most editors send you back to the source pane to find that line and edit it there.

In Markdown Studio, you **right-click the paragraph, heading, list, code block — even select multiple blocks** — and pick **X-ray edit**. That region of the preview transforms in place into a textarea pre-filled with the underlying markdown for exactly those source lines. The textarea inherits the preview's typography (Segoe UI Variable Text, 16px, 1.7 line-height), so a long line wraps at the same point you saw it wrap in the rendered text.

A couple of touches:

- **Caret lands where you clicked.** Right-click on a word and the cursor opens in that word. (Ctrl+E works too — it just opens the cursor at the start.)
- **Multi-block edits** combine the selected paragraphs/headings/etc. into one textarea covering the full source range, including the blank lines between them.
- **Ctrl+Enter** applies your edit back to the buffer; **Esc** cancels.
- **"Apply" — not "Save"**. The change goes into the in-memory buffer, not to disk. You still control when to `Ctrl+S`. The tab gets a dirty dot until you do.

Under the hood, X-ray uses markdown-it's source-map metadata to tag every rendered block with the source line range it came from. The preview keeps the latest markdown source in memory, so it can slice out the raw text for the textarea with no host round-trip — only the *Apply* writes back to Monaco, via a small JSON message that runs `executeEdits` over the line range.

---

## Other features

**Workspace & files**
- Open a folder to browse its markdown files in a sidebar tree (`.md`, `.markdown`, `.mdown`, `.mkd`, `.mdx`). Hidden directories, `.git`, `node_modules`, build outputs are all skipped.
- Open individual files into tabs.
- A **Welcome** tab with two-column layout: hero + actions on the left, a combined **Recent** list (folders and files mixed, kind-aware icon) on the right.
- MRU persists across sessions.
- Save / Don't save / Cancel prompt on close, per-tab and on app exit.

**Editor & preview**
- **Monaco** as the editor — full keyboard, syntax highlighting, find/replace, minimap, ligatures.
- **Three view modes** in a segmented control on the top-right: **Editor / Split / Preview**. New blank files always open in Editor. Mode choice persists across sessions and applies to every open tab.
- Markdown rendered via **markdown-it** with GFM tables, task lists, footnotes, **KaTeX math**, **Mermaid diagrams**, and syntax-highlighted code blocks via highlight.js.
- **Editor ↔ Preview scroll sync.**
- **Chrome-style URL hint** in the bottom-left when you hover a link in the preview.

**Outline & search**
- **Outline** sidebar: live table of contents parsed from your headings, click to jump.
- **Search** sidebar: searches file names *and* file contents across the opened folder. Debounced, cancellable, grouped by file.

**Themes & polish**
- Six curated themes: **Follow System**, **Daylight**, **Midnight**, **Sepia**, **Solarized Light**, **Solarized Dark**. Selection persists.
- Custom Monaco themes matching each app theme; preview CSS adapts in lockstep.
- **Mica** window backdrop on Win11; caption (min/max/close) button colors follow the theme.
- **Settings** pane: editor font preset (Cascadia Code, Consolas, Fira Code, JetBrains Mono, Source Code Pro, Courier New, Segoe UI Mono), font size 10–28, tab size 1–8 — all live-applied to every open tab.
- **Distraction-free mode** (F11): hides rail, sidebar, tab strip, toolbar, status bar.

---

## Getting started

### Prerequisites

- **Windows 10 1809+ or Windows 11**
- **.NET 10 SDK** (`dotnet --version` ≥ 10.0)
- **PowerShell 5.1+** (built into Windows) — needed for `setup-web.ps1`
- **Visual Studio 2022 17.x** with the *Windows application development* workload (optional but recommended for F5 debugging)

### Build & run

```powershell
git clone https://github.com/justinswork/MarkdownStudio.git
cd MarkdownStudio

# Downloads Monaco editor, markdown-it + plugins, KaTeX, Mermaid,
# highlight.js, github-markdown-css into MarkdownStudio/Web/ and
# generates the placeholder MSIX assets. Run once after cloning.
pwsh setup-web.ps1

# Build
dotnet build MarkdownStudio.sln -c Debug -p:Platform=x64
```

### Running from Visual Studio

Open `MarkdownStudio.sln`, set the **MarkdownStudio (Package)** launch profile, hit **F5**. Visual Studio handles the dev signing cert and MSIX deployment for you.

### Running without Visual Studio

After `dotnet build` succeeds, register the package and launch via the app's AUMID:

```powershell
$manifest = 'MarkdownStudio\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\AppxManifest.xml'
Add-AppxPackage -Path $manifest -Register

$pkg   = Get-AppxPackage -Name 'MarkdownStudio' | Select-Object -First 1
$appId = (Get-AppxPackageManifest $pkg.PackageFullName).Package.Applications.Application.Id
Start-Process "shell:AppsFolder\$($pkg.PackageFamilyName)!$appId"
```

---

## Keyboard shortcuts

| Action | Shortcut |
|---|---|
| New file (opens in Editor mode) | `Ctrl+N` |
| Open file | `Ctrl+O` |
| Open folder | `Ctrl+Shift+O` |
| Save | `Ctrl+S` |
| Save as | `Ctrl+Shift+S` |
| Close tab | `Ctrl+W` |
| **X-ray edit (selection or hovered block)** | `Ctrl+E` |
| **X-ray apply / cancel** | `Ctrl+Enter` / `Esc` |
| Find in editor | `Ctrl+F` |
| Toggle bold / italic | `Ctrl+B` / `Ctrl+I` |
| Insert link | `Ctrl+K` |
| Toggle inline code | `` Ctrl+` `` |
| Distraction-free mode | `F11` |
| View mode: Editor / Split / Preview | (toolbar pill) |

---

## Tech stack

- **WinUI 3** (Windows App SDK 1.8) on **.NET 10**
- **MSIX** packaging, with `.md/.markdown/.mdown/.mkd/.mdx` file associations
- **WebView2** hosting two web surfaces per tab: Monaco (editor) and markdown-it (preview)
- **Monaco editor 0.52** — editor surface, including the 5 custom themes
- **markdown-it 14** — preview rendering, with task-list, footnote, KaTeX, and a small inline plugin that injects source-line ranges (`data-line` / `data-line-end`) on every block-level element. That's what powers both scroll-sync and X-ray.
- **KaTeX 0.16** — math
- **Mermaid 11** — diagrams
- **highlight.js 11** — code block syntax highlighting
- **github-markdown-css** — base typography for the preview

### Project layout

```
MarkdownStudio/
├── MarkdownStudio.sln
├── setup-web.ps1                 ← downloads JS deps + generates MSIX asset PNGs
├── docs/                         ← screenshots
└── MarkdownStudio/
    ├── App.xaml(.cs)             ← app entry, theme resource merges
    ├── MainWindow.xaml(.cs)      ← shell: title bar, activity rail, sidebar, tab strip
    ├── Package.appxmanifest      ← MSIX manifest
    ├── Models/                   ← MruEntry, OutlineNode, AppTheme, EditorPreferences, …
    ├── Services/                 ← MruService, OutlineService, AppThemeService,
    │                                EditorPreferencesService, FileService, SearchService
    ├── Views/                    ← UserControls: ActivityBar, FileTreeView, OutlineView,
    │                                ThemePickerView, SearchView, SettingsView, WelcomeView,
    │                                EditorPaneControl, ModeSegmentedControl
    ├── Styles/AppResources.xaml  ← brushes (mutated on theme change), text + button styles
    ├── Assets/                   ← MSIX logos (generated by setup-web.ps1)
    └── Web/
        ├── editor/               ← Monaco host page (index.html, editor.js, editor.css)
        └── preview/              ← markdown-it host page (index.html, preview.js, preview.css)
```

---

## Roadmap

Things that aren't there yet but are good candidates:

- Folder watcher: pick up files added/removed from the opened folder while the app is running.
- Image paste / drag-drop into the editor.
- Git integration: file-tree decorations for modified files, simple diff view.
- Snippets and templates.
- Export to HTML / PDF.
- Custom user themes.

---

## License

TBD.
