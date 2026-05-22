using System;
using System.IO;

namespace MarkdownStudio.Models;

public enum MruKind { File, Folder }

public sealed class MruEntry
{
    public string Path { get; set; } = string.Empty;
    public MruKind Kind { get; set; }
    public DateTimeOffset LastOpened { get; set; }

    public string DisplayName =>
        Kind == MruKind.Folder
            ? new DirectoryInfo(Path).Name
            : System.IO.Path.GetFileName(Path);

    public string ParentLocation =>
        Kind == MruKind.Folder
            ? new DirectoryInfo(Path).Parent?.FullName ?? Path
            : System.IO.Path.GetDirectoryName(Path) ?? Path;

    // Segoe Fluent Icons: FolderHorizontal (E8B7) for folders, Document (E8A5) for files.
    public string Glyph => Kind == MruKind.Folder ? "" : "";

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
