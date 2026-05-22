using System.Collections.ObjectModel;

namespace MarkdownStudio.Models;

public sealed class FileTreeNode
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public ObservableCollection<FileTreeNode> Children { get; } = new();

    public string Glyph => IsDirectory ? "" : ""; // folder / document
}
