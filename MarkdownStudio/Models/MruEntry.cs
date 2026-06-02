using System;
using System.IO;
using System.Text.Json.Serialization;

namespace MarkdownStudio.Models;

public enum MruKind { File, Folder }

public sealed class MruEntry
{
    public string Path { get; set; } = string.Empty;
    public MruKind Kind { get; set; }
    public DateTimeOffset LastOpened { get; set; }
    public bool IsPinned { get; set; }

    [JsonIgnore]
    public string DisplayName =>
        Kind == MruKind.Folder
            ? new DirectoryInfo(Path).Name
            : System.IO.Path.GetFileName(Path);

    [JsonIgnore]
    public string ParentLocation =>
        Kind == MruKind.Folder
            ? new DirectoryInfo(Path).Parent?.FullName ?? Path
            : System.IO.Path.GetDirectoryName(Path) ?? Path;

    // Segoe Fluent Icons: FolderHorizontal (E8B7) for folders, Document (E8A5) for files.
    [JsonIgnore]
    public string Glyph => Kind == MruKind.Folder ? "" : "";

    // Inline pin toggle button: a plain Pin (E718) when unpinned, and the
    // slashed Unpin (E77A) glyph once pinned, so the button itself reads as
    // "click to unpin" — the line-through affordance the user asked for.
    [JsonIgnore]
    public string PinButtonGlyph => IsPinned ? "" : "";
    [JsonIgnore]
    public string PinButtonTooltip => IsPinned ? "Unpin" : "Pin to top";

    [JsonIgnore]
    public string RelativeWhen
    {
        get
        {
            var span = DateTimeOffset.Now - LastOpened;
            if (span.TotalSeconds < 60) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return LastOpened.LocalDateTime.ToString("MMM d, yyyy");
        }
    }
}
