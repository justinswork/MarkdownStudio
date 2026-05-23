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
        // If any node with children is currently expanded, collapse everything;
        // otherwise expand everything. Computing on click means user-driven
        // toggles of individual nodes don't desync the button intent.
        var expand = !AnyExpanded(Roots);
        SetAllExpanded(Roots, expand);
        RefreshToggleButton();
    }

    private static bool AnyExpanded(IEnumerable<OutlineNode> nodes)
    {
        foreach (var n in nodes)
        {
            if (n.HasChildren && n.IsExpanded) return true;
            if (AnyExpanded(n.Children)) return true;
        }
        return false;
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
        // E70E = ChevronUp     (everything's open — click to collapse)
        // E70D = ChevronDown   (everything's closed — click to expand)
        var anyExpanded = AnyExpanded(Roots);
        ToggleAllIcon.Glyph = anyExpanded ? "" : "";
        var tip = anyExpanded ? "Collapse all" : "Expand all";
        ToolTipService.SetToolTip(ToggleAllButton, tip);
        AutomationProperties.SetName(ToggleAllButton, tip);
    }
}
