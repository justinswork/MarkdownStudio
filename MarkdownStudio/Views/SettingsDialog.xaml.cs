using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

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

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Hide();

    private void OnEscape(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        Hide();
        args.Handled = true;
    }
}
