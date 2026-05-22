using System;
using System.Collections.Generic;
using MarkdownStudio.Models;
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
}
