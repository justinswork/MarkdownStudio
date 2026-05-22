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

public sealed class EditorPreferences
{
    public string FontPresetId { get; set; } = FontPresets.Default.Id;
    public int    FontSize     { get; set; } = 14;
    public int    TabSize      { get; set; } = 2;

    public FontPreset Font => FontPresets.ById(FontPresetId);
}
