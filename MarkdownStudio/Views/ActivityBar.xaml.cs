using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace MarkdownStudio.Views;

public enum ActivityPane { None, Files, Search, Outline }

public sealed partial class ActivityBar : UserControl
{
    public event Action? HomeRequested;
    public event Action? SettingsRequested;
    public event Action<ActivityPane>? PaneSelected;

    private ActivityPane _current = ActivityPane.None;

    public ActivityBar()
    {
        InitializeComponent();
    }

    public ActivityPane CurrentPane
    {
        get => _current;
        set
        {
            _current = value;
            SetChecks(value);
        }
    }

    // Visibility gating. Search appears once a file or folder is opened;
    // Outline only when there's an editor tab active. If the active pane is
    // the one being hidden, collapse the sidebar so the user isn't stuck on
    // an invisible tab.
    public bool ShowSearch
    {
        get => SearchBtn.Visibility == Visibility.Visible;
        set => SetButtonVisibility(SearchBtn, ActivityPane.Search, value);
    }
    public bool ShowOutline
    {
        get => OutlineBtn.Visibility == Visibility.Visible;
        set => SetButtonVisibility(OutlineBtn, ActivityPane.Outline, value);
    }

    private void SetButtonVisibility(ToggleButton btn, ActivityPane pane, bool visible)
    {
        btn.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (!visible && _current == pane)
        {
            _current = ActivityPane.None;
            SetChecks(ActivityPane.None);
            PaneSelected?.Invoke(ActivityPane.None);
        }
    }

    private void SetChecks(ActivityPane active)
    {
        FilesBtn.IsChecked    = active == ActivityPane.Files;
        SearchBtn.IsChecked   = active == ActivityPane.Search;
        OutlineBtn.IsChecked  = active == ActivityPane.Outline;
    }

    private void OnHomeClicked(object sender, RoutedEventArgs e) => HomeRequested?.Invoke();
    private void OnSettingsClicked(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();

    private void OnPaneClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton t) return;
        var pane = (t.Tag as string) switch
        {
            "files"    => ActivityPane.Files,
            "search"   => ActivityPane.Search,
            "outline"  => ActivityPane.Outline,
            _          => ActivityPane.None,
        };

        if (pane == _current)
        {
            // Clicking the active button collapses the sidebar
            _current = ActivityPane.None;
            SetChecks(ActivityPane.None);
            PaneSelected?.Invoke(ActivityPane.None);
            return;
        }

        _current = pane;
        SetChecks(pane);
        PaneSelected?.Invoke(pane);
    }
}
