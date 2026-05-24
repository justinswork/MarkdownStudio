using System;
using System.Collections.Generic;
using MarkdownStudio.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MarkdownStudio.Views;

public sealed partial class ThemePickerView : UserControl
{
    public IReadOnlyList<AppTheme> Items { get; } = AppThemes.All;

    public event Action<AppTheme>? ThemeSelected;

    public ThemePickerView()
    {
        InitializeComponent();
    }

    public void SetSelected(AppTheme theme)
    {
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i].Id == theme.Id)
            {
                ThemeList.SelectedIndex = i;
                return;
            }
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeList.SelectedItem is AppTheme t) ThemeSelected?.Invoke(t);
    }

    // Opens Windows Settings → Default apps. From there the user can pick
    // Markdown Studio as the handler for .md / .markdown / etc. We can't
    // change the registration ourselves from a sandboxed MSIX app.
    private async void OnOpenDefaultApps(object sender, RoutedEventArgs e)
    {
        try { await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:defaultapps")); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Launch default apps failed: {ex.Message}"); }
    }
}
