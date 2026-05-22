using System.Collections.ObjectModel;

namespace MarkdownStudio.Models;

public sealed class OutlineNode
{
    public string Title { get; set; } = string.Empty;
    public int Level { get; set; }
    public int LineNumber { get; set; }
    public ObservableCollection<OutlineNode> Children { get; } = new();

    public bool HasChildren => Children.Count > 0;
}
