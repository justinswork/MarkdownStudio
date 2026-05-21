<#
.SYNOPSIS
  Downloads Monaco editor + markdown-it + preview dependencies and generates
  placeholder MSIX assets. Run this once after cloning, and again whenever you
  bump versions in $packages below.
#>
param([switch]$Force, [switch]$SkipAssets)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$projectRoot     = Split-Path -Parent $PSCommandPath
$webRoot         = Join-Path $projectRoot 'MarkdownStudio\Web'
$editorMonacoDir = Join-Path $webRoot 'editor\monaco'
$previewLibDir   = Join-Path $webRoot 'preview\lib'
$assetsDir       = Join-Path $projectRoot 'MarkdownStudio\Assets'

New-Item -ItemType Directory -Force -Path $editorMonacoDir, $previewLibDir, $assetsDir | Out-Null

$tempDir = Join-Path $env:TEMP "mdstudio-setup-$([Guid]::NewGuid().ToString('N').Substring(0,8))"
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

try {
    function Get-NpmPackage {
        param([string]$Name, [string]$Version)
        $baseName = if ($Name.StartsWith('@')) { ($Name -split '/')[1] } else { $Name }
        $safeFolder = $Name.TrimStart('@').Replace('/', '__')
        $url        = "https://registry.npmjs.org/$Name/-/$baseName-$Version.tgz"
        $tgz        = Join-Path $tempDir "$safeFolder-$Version.tgz"
        $extract    = Join-Path $tempDir $safeFolder

        Write-Host "  -> $Name@$Version"
        Invoke-WebRequest -Uri $url -OutFile $tgz
        New-Item -ItemType Directory -Force -Path $extract | Out-Null
        tar -xzf $tgz -C $extract
        if ($LASTEXITCODE -ne 0) { throw "tar failed for $Name" }
        return (Join-Path $extract 'package')
    }

    function Copy-Tree {
        param([string]$Src, [string]$Dest)
        if (Test-Path $Dest) { Remove-Item -Recurse -Force $Dest }
        New-Item -ItemType Directory -Force -Path $Dest | Out-Null
        Copy-Item -Recurse -Force -Path (Join-Path $Src '*') -Destination $Dest
    }

    Write-Host 'Downloading web assets...'

    # ---- Monaco editor ----
    $monacoPkg = Get-NpmPackage -Name 'monaco-editor' -Version '0.52.2'
    Copy-Tree -Src (Join-Path $monacoPkg 'min\vs') -Dest (Join-Path $editorMonacoDir 'vs')

    # ---- markdown-it core ----
    $mdItPkg = Get-NpmPackage -Name 'markdown-it' -Version '14.1.0'
    Copy-Item -Force -Path (Join-Path $mdItPkg 'dist\markdown-it.min.js') `
                     -Destination (Join-Path $previewLibDir 'markdown-it.min.js')

    # ---- markdown-it-task-lists ----
    $taskPkg = Get-NpmPackage -Name 'markdown-it-task-lists' -Version '2.1.1'
    $taskJs = Join-Path $taskPkg 'dist\markdown-it-task-lists.js'
    if (-not (Test-Path $taskJs)) { $taskJs = Join-Path $taskPkg 'dist\markdown-it-task-lists.min.js' }
    Copy-Item -Force -Path $taskJs -Destination (Join-Path $previewLibDir 'markdown-it-task-lists.min.js')

    # ---- markdown-it-footnote ----
    $footPkg = Get-NpmPackage -Name 'markdown-it-footnote' -Version '4.0.0'
    $footJs = Join-Path $footPkg 'dist\markdown-it-footnote.min.js'
    if (-not (Test-Path $footJs)) { $footJs = Join-Path $footPkg 'dist\markdown-it-footnote.js' }
    Copy-Item -Force -Path $footJs -Destination (Join-Path $previewLibDir 'markdown-it-footnote.min.js')

    # ---- KaTeX ----
    $katexPkg = Get-NpmPackage -Name 'katex' -Version '0.16.11'
    Copy-Item -Force -Path (Join-Path $katexPkg 'dist\katex.min.js')  -Destination (Join-Path $previewLibDir 'katex.min.js')
    Copy-Item -Force -Path (Join-Path $katexPkg 'dist\katex.min.css') -Destination (Join-Path $previewLibDir 'katex.min.css')
    Copy-Tree -Src (Join-Path $katexPkg 'dist\fonts') -Dest (Join-Path $previewLibDir 'fonts')

    # ---- markdown-it-katex ----
    $katexMdPkg = Get-NpmPackage -Name 'markdown-it-katex' -Version '2.0.3'
    $katexMdJs = Join-Path $katexMdPkg 'dist\markdown-it-katex.min.js'
    if (-not (Test-Path $katexMdJs)) {
        # 2.0.3 doesn't actually ship a dist build; bundle a tiny shim manually
        $idx = Join-Path $katexMdPkg 'index.js'
        Copy-Item -Force -Path $idx -Destination (Join-Path $previewLibDir 'markdown-it-katex-src.js')
        # Build a minimal UMD wrapper around index.js requires
        $wrapper = @"
;(function (global) {
  if (!global.katex) { console.warn('katex not loaded'); return; }
  // Minimal port of markdown-it-katex 2.0.3 (Apache-2.0)
  function isValidDelim(state, pos) {
    var max = state.posMax, can_open = true, can_close = true;
    var prevChar = pos > 0 ? state.src.charCodeAt(pos - 1) : -1;
    var nextChar = pos + 1 <= max ? state.src.charCodeAt(pos + 1) : -1;
    if (prevChar === 0x20 || prevChar === 0x09 || (nextChar >= 0x30 && nextChar <= 0x39)) can_close = false;
    if (nextChar === 0x20 || nextChar === 0x09) can_open = false;
    return { can_open: can_open, can_close: can_close };
  }
  function math_inline(state, silent) {
    var start, match, token, res, pos;
    if (state.src[state.pos] !== '$') return false;
    res = isValidDelim(state, state.pos);
    if (!res.can_open) { if (!silent) state.pending += '$'; state.pos += 1; return true; }
    start = state.pos + 1;
    match = start;
    while ((match = state.src.indexOf('$', match)) !== -1) {
      pos = match - 1;
      while (state.src[pos] === '\\\\') pos -= 1;
      if (((match - pos) % 2) == 1) break;
      match += 1;
    }
    if (match === -1) { if (!silent) state.pending += '$'; state.pos = start; return true; }
    if (match - start === 0) { if (!silent) state.pending += '$$'; state.pos = start + 1; return true; }
    res = isValidDelim(state, match);
    if (!res.can_close) { if (!silent) state.pending += '$'; state.pos = start; return true; }
    if (!silent) {
      token = state.push('math_inline', 'math', 0);
      token.markup = '$';
      token.content = state.src.slice(start, match);
    }
    state.pos = match + 1;
    return true;
  }
  function math_block(state, start, end, silent) {
    var firstLine, lastLine, next, lastPos, found = false, token,
        pos = state.bMarks[start] + state.tShift[start],
        max = state.eMarks[start];
    if (pos + 2 > max) return false;
    if (state.src.slice(pos, pos + 2) !== '$$') return false;
    pos += 2;
    firstLine = state.src.slice(pos, max);
    if (silent) return true;
    if (firstLine.trim().slice(-2) === '$$') {
      firstLine = firstLine.trim().slice(0, -2);
      found = true;
    }
    for (next = start; !found;) {
      next++;
      if (next >= end) break;
      pos = state.bMarks[next] + state.tShift[next];
      max = state.eMarks[next];
      if (pos < max && state.tShift[next] < state.blkIndent) break;
      if (state.src.slice(pos, max).trim().slice(-2) === '$$') {
        lastPos = state.src.slice(0, max).lastIndexOf('$$');
        lastLine = state.src.slice(pos, lastPos);
        found = true;
      }
    }
    state.line = next + 1;
    token = state.push('math_block', 'math', 0);
    token.block = true;
    token.content = (firstLine && firstLine.trim() ? firstLine + '\n' : '') +
      state.getLines(start + 1, next, state.tShift[start], true) +
      (lastLine && lastLine.trim() ? lastLine : '');
    token.map = [start, state.line];
    token.markup = '$$';
    return true;
  }
  function renderInline(latex) {
    try { return global.katex.renderToString(latex, { throwOnError: false }); }
    catch (e) { return latex; }
  }
  function renderBlock(latex) {
    try { return '<p>' + global.katex.renderToString(latex, { throwOnError: false, displayMode: true }) + '</p>'; }
    catch (e) { return '<p>' + latex + '</p>'; }
  }
  global.markdownitKatex = function (md) {
    md.inline.ruler.after('escape', 'math_inline', math_inline);
    md.block.ruler.after('blockquote', 'math_block', math_block, { alt: ['paragraph', 'reference', 'blockquote', 'list'] });
    md.renderer.rules.math_inline = function (tokens, idx) { return renderInline(tokens[idx].content); };
    md.renderer.rules.math_block  = function (tokens, idx) { return renderBlock(tokens[idx].content);  };
  };
})(typeof window !== 'undefined' ? window : globalThis);
"@
        Set-Content -Path (Join-Path $previewLibDir 'markdown-it-katex.min.js') -Value $wrapper -Encoding utf8
    } else {
        Copy-Item -Force -Path $katexMdJs -Destination (Join-Path $previewLibDir 'markdown-it-katex.min.js')
    }

    # ---- highlight.js (cdn-assets ships UMD + styles) ----
    $hljsPkg = Get-NpmPackage -Name '@highlightjs/cdn-assets' -Version '11.10.0'
    Copy-Item -Force -Path (Join-Path $hljsPkg 'highlight.min.js') -Destination (Join-Path $previewLibDir 'highlight.min.js')
    Copy-Item -Force -Path (Join-Path $hljsPkg 'styles\github.min.css') -Destination (Join-Path $previewLibDir 'highlight.css')

    # ---- github-markdown-css ----
    $gmdPkg = Get-NpmPackage -Name 'github-markdown-css' -Version '5.8.1'
    Copy-Item -Force -Path (Join-Path $gmdPkg 'github-markdown.css') -Destination (Join-Path $previewLibDir 'github-markdown.css')

    # ---- mermaid ----
    $mermPkg = Get-NpmPackage -Name 'mermaid' -Version '11.4.1'
    Copy-Item -Force -Path (Join-Path $mermPkg 'dist\mermaid.min.js') -Destination (Join-Path $previewLibDir 'mermaid.min.js')

    Write-Host 'Web assets ready.'

    # ---- Placeholder MSIX assets ----
    if (-not $SkipAssets) {
        Write-Host 'Generating placeholder MSIX assets...'
        Add-Type -AssemblyName System.Drawing

        function New-PlaceholderPng {
            param([string]$Path, [int]$Width, [int]$Height, [string]$Text = 'M')
            $bmp = New-Object System.Drawing.Bitmap $Width, $Height
            $gfx = [System.Drawing.Graphics]::FromImage($bmp)
            $gfx.SmoothingMode    = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $gfx.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
            $bg = [System.Drawing.Color]::FromArgb(31, 111, 235)
            $gfx.Clear($bg)
            $fontSize = [Math]::Max([int]($Height * 0.55), 8)
            $font = New-Object System.Drawing.Font 'Segoe UI', $fontSize, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
            $brush = [System.Drawing.Brushes]::White
            $sf = New-Object System.Drawing.StringFormat
            $sf.Alignment = [System.Drawing.StringAlignment]::Center
            $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
            $rect = New-Object System.Drawing.RectangleF 0, 0, $Width, $Height
            $gfx.DrawString($Text, $font, $brush, $rect, $sf)
            $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
            $gfx.Dispose(); $bmp.Dispose(); $font.Dispose()
        }

        New-PlaceholderPng -Path (Join-Path $assetsDir 'StoreLogo.png')         -Width 50  -Height 50
        New-PlaceholderPng -Path (Join-Path $assetsDir 'Square44x44Logo.png')   -Width 44  -Height 44
        New-PlaceholderPng -Path (Join-Path $assetsDir 'Square150x150Logo.png') -Width 150 -Height 150
        New-PlaceholderPng -Path (Join-Path $assetsDir 'Wide310x150Logo.png')   -Width 310 -Height 150 -Text 'Markdown'
        New-PlaceholderPng -Path (Join-Path $assetsDir 'SplashScreen.png')      -Width 620 -Height 300 -Text 'Markdown Studio'
        New-PlaceholderPng -Path (Join-Path $assetsDir 'LockScreenLogo.png')    -Width 24  -Height 24
        Write-Host 'Assets generated.'
    }

    Write-Host 'Done.'
} finally {
    if (Test-Path $tempDir) {
        try { Remove-Item -Recurse -Force $tempDir } catch {}
    }
}
