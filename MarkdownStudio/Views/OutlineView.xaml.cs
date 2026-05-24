using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MarkdownStudio.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace MarkdownStudio.Views;

public sealed partial class OutlineView : UserControl
{
    public ObservableCollection<OutlineNode> Roots { get; } = new();

    public event Action<OutlineNode>? HeadingActivated;

    // Simple toggle: alternates between expand-all and collapse-all each click,
    // regardless of what individual chevrons happen to be doing. Starts as
    // "collapse all" because nodes default to expanded.
    private bool _nextActionIsExpand;

    public OutlineView()
    {
        InitializeComponent();
    }

    public void SetNodes(IList<OutlineNode> nodes)
    {
        Roots.Clear();
        foreach (var n in nodes) Roots.Add(n);

        int total = 0;
        bool anyHasChildren = false;
        void Count(OutlineNode node)
        {
            total++;
            if (node.HasChildren) anyHasChildren = true;
            foreach (var c in node.Children) Count(c);
        }
        foreach (var n in nodes) Count(n);

        SubtitleText.Text = total == 0 ? "No headings yet"
                                       : total == 1 ? "1 heading"
                                                    : $"{total} headings";
        EmptyState.Visibility    = total == 0          ? Visibility.Visible : Visibility.Collapsed;
        // Toggle button is only useful when at least one node has children.
        ToggleAllButton.Visibility = anyHasChildren    ? Visibility.Visible : Visibility.Collapsed;
        RefreshToggleButton();
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

    // ---- Expand / collapse all ----

    private void OnToggleAll(object sender, RoutedEventArgs e)
    {
        var expand = _nextActionIsExpand;

        // Keep the model in sync (so collapse state survives a re-parse and
        // the TwoWay binding remains consistent)…
        SetAllExpanded(Roots, expand);

        // …but also drive TreeViewNode.IsExpanded directly. TwoWay binding on
        // TreeViewItem.IsExpanded only takes effect once the container is
        // realized, and child containers aren't realized until their parent
        // is expanded — so a single model-side update only ever cascades one
        // level visually. Walking RootNodes / Children covers every level
        // including deeply nested headings.
        SetTreeNodesExpanded(Tree.RootNodes, expand);

        // Flip intent for the next click.
        _nextActionIsExpand = !expand;
        RefreshToggleButton();
    }

    private static void SetTreeNodesExpanded(IList<TreeViewNode> nodes, bool expand)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = expand;
            if (node.Children != null) SetTreeNodesExpanded(node.Children, expand);
        }
    }

    private static void SetAllExpanded(IEnumerable<OutlineNode> nodes, bool expanded)
    {
        foreach (var n in nodes)
        {
            if (n.HasChildren) n.IsExpanded = expanded;
            SetAllExpanded(n.Children, expanded);
        }
    }

    private void RefreshToggleButton()
    {
        // Both stacked chevrons point the same direction so the button reads
        // as a single "double chevron — applies to all" glyph.
        // E70D = ChevronDown   (next click expands everything)
        // E70E = ChevronUp     (next click collapses everything)
        var glyph = _nextActionIsExpand ? "" : "";
        ToggleAllIconTop.Glyph = glyph;
        ToggleAllIconBottom.Glyph = glyph;
        var tip = _nextActionIsExpand ? "Expand all" : "Collapse all";
        ToolTipService.SetToolTip(ToggleAllButton, tip);
        AutomationProperties.SetName(ToggleAllButton, tip);
    }
}
