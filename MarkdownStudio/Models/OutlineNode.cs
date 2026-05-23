using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MarkdownStudio.Models;

public sealed class OutlineNode : INotifyPropertyChanged
{
    public string Title { get; set; } = string.Empty;
    public int Level { get; set; }
    public int LineNumber { get; set; }
    public ObservableCollection<OutlineNode> Children { get; } = new();

    public bool HasChildren => Children.Count > 0;

    // OutlineView binds TreeViewItem.IsExpanded TwoWay to this so user
    // collapses survive a re-parse and the "expand / collapse all" button
    // can drive every node at once. Defaults to true so heading groups
    // render expanded the first time they're shown.
    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
