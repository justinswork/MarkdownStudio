using System;
using MarkdownStudio.Models;
using MarkdownStudio.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace MarkdownStudio.Views;

// ContentDialog with tabs for General (theme), Editor, and Preview settings.
// MainWindow constructs it, calls Attach* on the inner panes with the right
// services, and ShowAsync()'s it.
public sealed partial class SettingsDialog : ContentDialog
{
    private AppThemeService? _themeService;

    public SettingsDialog()
    {
        InitializeComponent();
    }

    public ThemePickerView      ThemePicker     => GeneralPane;
    public SettingsView         EditorSettings  => EditorPane;
    public PreviewSettingsView  PreviewSettings => PreviewPane;

    // Subscribe to live theme changes (the user can switch themes from the
    // General tab while the dialog is open) — update the dialog's
    // RequestedTheme and re-skin the embedded Monaco + preview WebView2s.
    public void AttachThemeService(AppThemeService svc)
    {
        _themeService = svc;
        svc.Changed += OnAppThemeChanged;
        Closed += OnDialogClosed;
    }

    private void OnDialogClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        if (_themeService != null) _themeService.Changed -= OnAppThemeChanged;
        Closed -= OnDialogClosed;
    }

    private void OnAppThemeChanged(AppTheme t) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_themeService == null) return;
            RequestedTheme = _themeService.EffectiveElementTheme;
            _ = EditorPane.SetThemeAsync(_themeService.EffectiveMonacoTheme);
            _ = PreviewPane.SetThemeAsync(_themeService.EffectivePreviewClass);
        });

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Hide();

    private void OnEscape(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        Hide();
        args.Handled = true;
    }
}
