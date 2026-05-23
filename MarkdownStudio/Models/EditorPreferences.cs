using System.Collections.Generic;

namespace MarkdownStudio.Models;

public sealed class FontPreset
{
    public string Id          { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CssFamily   { get; set; } = string.Empty;
}

public static class FontPresets
{
    public static IReadOnlyList<FontPreset> All { get; } = new[]
    {
        new FontPreset { Id = "cascadia",     DisplayName = "Cascadia Code",     CssFamily = "'Cascadia Code', Consolas, 'Courier New', monospace" },
        new FontPreset { Id = "consolas",     DisplayName = "Consolas",          CssFamily = "Consolas, 'Courier New', monospace" },
        new FontPreset { Id = "fira",         DisplayName = "Fira Code",         CssFamily = "'Fira Code', Consolas, monospace" },
        new FontPreset { Id = "jetbrains",    DisplayName = "JetBrains Mono",    CssFamily = "'JetBrains Mono', Consolas, monospace" },
        new FontPreset { Id = "source",       DisplayName = "Source Code Pro",   CssFamily = "'Source Code Pro', Consolas, monospace" },
        new FontPreset { Id = "courier",      DisplayName = "Courier New",       CssFamily = "'Courier New', monospace" },
        new FontPreset { Id = "segoe-mono",   DisplayName = "Segoe UI Mono",     CssFamily = "'Segoe UI Mono', Consolas, monospace" },
    };

    public static FontPreset Default => All[0];

    public static FontPreset ById(string id)
    {
        foreach (var p in All) if (p.Id == id) return p;
        return Default;
    }
}

// -------- Preview-side typography --------

public static class PreviewFontPresets
{
    public static IReadOnlyList<FontPreset> All { get; } = new[]
    {
        new FontPreset { Id = "segoe-variable", DisplayName = "Segoe UI Variable",
            CssFamily = "'Segoe UI Variable Text', 'Segoe UI', -apple-system, BlinkMacSystemFont, sans-serif" },
        new FontPreset { Id = "segoe",          DisplayName = "Segoe UI",
            CssFamily = "'Segoe UI', -apple-system, BlinkMacSystemFont, sans-serif" },
        new FontPreset { Id = "system-sans",    DisplayName = "System Sans",
            CssFamily = "system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif" },
        new FontPreset { Id = "cambria",        DisplayName = "Cambria",
            CssFamily = "Cambria, Georgia, 'Times New Roman', serif" },
        new FontPreset { Id = "georgia",        DisplayName = "Georgia",
            CssFamily = "Georgia, 'Times New Roman', serif" },
        new FontPreset { Id = "constantia",     DisplayName = "Constantia",
            CssFamily = "Constantia, Georgia, serif" },
    };

    public static FontPreset Default => All[0];

    public static FontPreset ById(string id)
    {
        foreach (var p in All) if (p.Id == id) return p;
        return Default;
    }
}

public sealed class WidthPreset
{
    public string Id          { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    // CSS value for #content's max-width (px, or "100%" for full).
    public string CssMaxWidth { get; set; } = string.Empty;
}

public static class WidthPresets
{
    public static IReadOnlyList<WidthPreset> All { get; } = new[]
    {
        new WidthPreset { Id = "narrow",      DisplayName = "Narrow (640px)",      CssMaxWidth = "640px" },
        new WidthPreset { Id = "comfortable", DisplayName = "Comfortable (760px)", CssMaxWidth = "760px" },
        new WidthPreset { Id = "wide",        DisplayName = "Wide (920px)",        CssMaxWidth = "920px" },
        new WidthPreset { Id = "full",        DisplayName = "Full width",          CssMaxWidth = "100%"  },
    };

    public static WidthPreset Default => All[1]; // comfortable

    public static WidthPreset ById(string id)
    {
        foreach (var p in All) if (p.Id == id) return p;
        return Default;
    }
}

public sealed class HeadingStylePreset
{
    public string Id          { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CssClass    { get; set; } = string.Empty;
}

public static class HeadingStylePresets
{
    public static IReadOnlyList<HeadingStylePreset> All { get; } = new[]
    {
        new HeadingStylePreset { Id = "standard", DisplayName = "Standard",
            Description = "GitHub-style; H1 / H2 underlined",
            CssClass    = "headings-standard" },
        new HeadingStylePreset { Id = "minimal", DisplayName = "Minimal",
            Description = "No underline; lighter weight and tighter spacing",
            CssClass    = "headings-minimal" },
        new HeadingStylePreset { Id = "display", DisplayName = "Display",
            Description = "Larger, bolder, more generous spacing",
            CssClass    = "headings-display" },
    };

    public static HeadingStylePreset Default => All[0];

    public static HeadingStylePreset ById(string id)
    {
        foreach (var p in All) if (p.Id == id) return p;
        return Default;
    }
}

public sealed class EditorPreferences
{
    // Editor side
    public string FontPresetId   { get; set; } = FontPresets.Default.Id;
    public int    FontSize       { get; set; } = 14;
    public int    TabSize        { get; set; } = 2;
    public bool   ShowWhitespace { get; set; }

    public FontPreset Font => FontPresets.ById(FontPresetId);

    // Preview side
    public string PreviewFontPresetId { get; set; } = PreviewFontPresets.Default.Id;
    public int    PreviewFontSize     { get; set; } = 16;
    public double PreviewLineHeight   { get; set; } = 1.7;
    public string PreviewWidthId      { get; set; } = WidthPresets.Default.Id;
    public string PreviewHeadingId    { get; set; } = HeadingStylePresets.Default.Id;

    public FontPreset         PreviewFont         => PreviewFontPresets.ById(PreviewFontPresetId);
    public WidthPreset        PreviewWidth        => WidthPresets.ById(PreviewWidthId);
    public HeadingStylePreset PreviewHeadingStyle => HeadingStylePresets.ById(PreviewHeadingId);
}
