<#
.SYNOPSIS
    Generate MSIX icon variants (scale + altform-unplated) from a single
    high-resolution master PNG.

.DESCRIPTION
    MSIX expects multiple sizes of each tile/icon so Windows can pick
    the right one for the current DPI scale and context (Start menu
    tile, taskbar, jumplist, lock screen). Hand-resizing all of these
    in Photopea is tedious -- this script does it from one master.

    Outputs go to MarkdownStudio\Assets\:

      Square44x44Logo.scale-{100,125,150,200,400}.png
        Logical 44x44 at 100%, 125%, 150%, 200%, 400% display scaling.
        Windows picks one based on the user's display scale.

      Square44x44Logo.targetsize-{16,24,32,48,256}_altform-unplated.png
        Transparent-background variants Windows uses on the taskbar,
        jumplists, and lock screen. Without these, Windows falls back
        to the manifest's BackgroundColor (the "blue square" effect).

      Square150x150Logo.scale-{100,125,150,200,400}.png
        Logical 150x150 (medium Start menu tile) at various scales.

      StoreLogo.scale-{100,125,150,200,400}.png
        Logical 50x50 (Store listing thumbnail) at various scales.

    Wide310x150Logo.png, SplashScreen.png, and LockScreenLogo.png are
    NOT regenerated -- they're different aspect ratios and you've
    already hand-composed them.

.PARAMETER Master
    Path to a high-resolution square PNG with a transparent background.
    Recommended: 1024x1024. Defaults to design\AppIcon.png.

.EXAMPLE
    pwsh ./generate-icons.ps1

.EXAMPLE
    pwsh ./generate-icons.ps1 -Master path\to\my\source.png
#>
param(
    [string]$Master = 'design\AppIcon.png'
)

$ErrorActionPreference = 'Stop'

# Resolve master to an absolute path so System.Drawing finds it from
# wherever the script is invoked.
if (-not [System.IO.Path]::IsPathRooted($Master)) {
    $Master = Join-Path $PSScriptRoot $Master
}
if (-not (Test-Path $Master)) {
    throw "Master icon not found at $Master. Export your design as a 1024x1024 PNG (or pass -Master <path>) and try again."
}

$assetsDir = Join-Path $PSScriptRoot 'MarkdownStudio\Assets'
if (-not (Test-Path $assetsDir)) { New-Item -ItemType Directory -Path $assetsDir | Out-Null }

# Variants to emit. Each entry is { Name, Size } -- square only, since
# every output here is a square tile. Sizes are derived from the MSIX
# spec: logical-size * scale_pct / 100.
$variants = @(
    # Square44x44Logo -- taskbar / Start-menu small icon
    @{ Name = 'Square44x44Logo.scale-100.png'; Size = 44  },
    @{ Name = 'Square44x44Logo.scale-125.png'; Size = 55  },
    @{ Name = 'Square44x44Logo.scale-150.png'; Size = 66  },
    @{ Name = 'Square44x44Logo.scale-200.png'; Size = 88  },
    @{ Name = 'Square44x44Logo.scale-400.png'; Size = 176 },

    # Square44x44Logo altform-unplated -- transparent variants for
    # taskbar / jumplist / lock screen. These are the ones that fix
    # the "blue square around the icon" issue.
    @{ Name = 'Square44x44Logo.targetsize-16_altform-unplated.png';  Size = 16  },
    @{ Name = 'Square44x44Logo.targetsize-24_altform-unplated.png';  Size = 24  },
    @{ Name = 'Square44x44Logo.targetsize-32_altform-unplated.png';  Size = 32  },
    @{ Name = 'Square44x44Logo.targetsize-48_altform-unplated.png';  Size = 48  },
    @{ Name = 'Square44x44Logo.targetsize-256_altform-unplated.png'; Size = 256 },

    # Square150x150Logo -- medium Start menu tile
    @{ Name = 'Square150x150Logo.scale-100.png'; Size = 150 },
    @{ Name = 'Square150x150Logo.scale-125.png'; Size = 188 },
    @{ Name = 'Square150x150Logo.scale-150.png'; Size = 225 },
    @{ Name = 'Square150x150Logo.scale-200.png'; Size = 300 },
    @{ Name = 'Square150x150Logo.scale-400.png'; Size = 600 },

    # StoreLogo -- Store listing thumbnail
    @{ Name = 'StoreLogo.scale-100.png'; Size = 50  },
    @{ Name = 'StoreLogo.scale-125.png'; Size = 63  },
    @{ Name = 'StoreLogo.scale-150.png'; Size = 75  },
    @{ Name = 'StoreLogo.scale-200.png'; Size = 100 },
    @{ Name = 'StoreLogo.scale-400.png'; Size = 200 }
)

Add-Type -AssemblyName System.Drawing

# Sanity-check the master: must be square, and big enough that downsampling
# to 600 (the largest output) doesn't blur. We warn at <512 and refuse below.
$srcBmp = [System.Drawing.Image]::FromFile($Master)
try {
    if ($srcBmp.Width -ne $srcBmp.Height) {
        throw "Master icon must be square; got $($srcBmp.Width)x$($srcBmp.Height)."
    }
    if ($srcBmp.Width -lt 256) {
        throw "Master icon is $($srcBmp.Width)px -- too small. Use at least 256, 1024 recommended."
    }
    if ($srcBmp.Width -lt 600) {
        Write-Warning "Master is $($srcBmp.Width)px. The 600px Square150x150Logo.scale-400 output will look slightly soft. 1024+ recommended."
    }
    Write-Host "Master: $Master ($($srcBmp.Width)x$($srcBmp.Height))"
} finally {
    $srcBmp.Dispose()
}

function Resize-Png {
    param([string]$Source, [string]$Dest, [int]$Size)
    $src = [System.Drawing.Image]::FromFile($Source)
    $dst = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $gfx = [System.Drawing.Graphics]::FromImage($dst)
    try {
        # High-quality downsampling. PixelOffsetMode=Half kills edge
        # darkening on transparent-edged icons; CompositingMode=SourceCopy
        # preserves alpha cleanly instead of compositing onto black.
        $gfx.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $gfx.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $gfx.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $gfx.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $gfx.CompositingMode    = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $gfx.DrawImage($src, 0, 0, $Size, $Size)
        $dst.Save($Dest, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $gfx.Dispose()
        $dst.Dispose()
        $src.Dispose()
    }
}

Write-Host "Writing $($variants.Count) variants to $assetsDir"
foreach ($v in $variants) {
    $out = Join-Path $assetsDir $v.Name
    Resize-Png -Source $Master -Dest $out -Size $v.Size
    Write-Host "  $($v.Size.ToString().PadLeft(4))px  ->  $($v.Name)"
}

Write-Host ""
Write-Host "Done."
Write-Host "Re-deploy via F5 (or rerun ./build-release.ps1) to see the new icons."
Write-Host "Note: Windows aggressively caches icons. If the taskbar still shows the old artwork,"
Write-Host "uninstall and reinstall the app, or run: ie4uinit.exe -show"
