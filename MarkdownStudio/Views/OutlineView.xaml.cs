using System;
using System.Collections.ObjectModel;
using MarkdownStudio.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace MarkdownStudio.Views;

public sealed partial class OutlineView : UserControl
{
    public ObservableCollection<OutlineNode> Roots { get; } = new();

    public event Action<OutlineNode>? HeadingActivated;

    public OutlineView()
    {
        InitializeComponent();
    }

    public void SetNodes(System.Collections.Generic.IList<OutlineNode> nodes)
    {
        Roots.Clear();
        foreach (var n in nodes) Roots.Add(n);

        int total = 0;
        void Count(OutlineNode node) { total++; foreach (var c in node.Children) Count(c); }
        foreach (var n in nodes) Count(n);

        SubtitleText.Text = total == 0 ? "No headings yet"
                                       : total == 1 ? "1 heading"
                                                    : $"{total} headings";
        EmptyState.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        // Fires for leaf nodes; for parent nodes a single click expands instead.
        if (args.InvokedItem is OutlineNode node)
            HeadingActivated?.Invoke(node);
    }

    private void OnTitleTapped(object sender, TappedRoutedEventArgs e)
    {
        // Clicking the heading text always navigates, even for nodes with children.
        // (The chevron still expands.)
        if (sender is FrameworkElement fe && fe.Tag is OutlineNode node)
        {
            HeadingActivated?.Invoke(node);
            e.Handled = true;
        }
    }
}
