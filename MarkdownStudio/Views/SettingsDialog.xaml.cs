using Microsoft.UI.Xaml.Controls;

namespace MarkdownStudio.Views;

// ContentDialog with tabs for General (theme), Editor, and Preview settings.
// MainWindow constructs it, calls Attach* on the inner panes with the right
// services, and ShowAsync()'s it.
public sealed partial class SettingsDialog : ContentDialog
{
    public SettingsDialog()
    {
        InitializeComponent();
    }

    public ThemePickerView      ThemePicker     => GeneralPane;
    public SettingsView         EditorSettings  => EditorPane;
    public PreviewSettingsView  PreviewSettings => PreviewPane;
}
