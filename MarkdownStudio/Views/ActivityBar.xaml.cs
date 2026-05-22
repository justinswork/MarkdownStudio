using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace MarkdownStudio.Views;

public enum ActivityPane { None, Files, Search, Outline, Themes }

public sealed partial class ActivityBar : UserControl
{
    public event Action? HomeRequested;
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

    private void SetChecks(ActivityPane active)
    {
        FilesBtn.IsChecked   = active == ActivityPane.Files;
        SearchBtn.IsChecked  = active == ActivityPane.Search;
        OutlineBtn.IsChecked = active == ActivityPane.Outline;
        ThemesBtn.IsChecked  = active == ActivityPane.Themes;
    }

    private void OnHomeClicked(object sender, RoutedEventArgs e) => HomeRequested?.Invoke();

    private void OnPaneClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton t) return;
        var pane = (t.Tag as string) switch
        {
            "files"   => ActivityPane.Files,
            "search"  => ActivityPane.Search,
            "outline" => ActivityPane.Outline,
            "themes"  => ActivityPane.Themes,
            _         => ActivityPane.None,
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
