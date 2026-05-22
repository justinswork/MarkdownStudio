using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace MarkdownStudio.Views;

public enum EditorMode { Editor, Split, Preview }

public sealed partial class ModeSegmentedControl : UserControl
{
    public event Action<EditorMode>? ModeChanged;

    private EditorMode _mode = EditorMode.Split;

    public ModeSegmentedControl()
    {
        InitializeComponent();
        UpdateVisuals();
    }

    public EditorMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            UpdateVisuals();
        }
    }

    private void OnClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn) return;
        var newMode = (btn.Tag as string) switch
        {
            "editor"  => EditorMode.Editor,
            "preview" => EditorMode.Preview,
            _         => EditorMode.Split,
        };
        if (newMode == _mode) { UpdateVisuals(); return; } // re-arm if user clicked the active one
        _mode = newMode;
        UpdateVisuals();
        ModeChanged?.Invoke(_mode);
    }

    private void UpdateVisuals()
    {
        EditorBtn.IsChecked  = _mode == EditorMode.Editor;
        SplitBtn.IsChecked   = _mode == EditorMode.Split;
        PreviewBtn.IsChecked = _mode == EditorMode.Preview;

        SolidColorBrush active = (SolidColorBrush)Application.Current.Resources["MdsSurfaceBrush"];
        SolidColorBrush activeFg = (SolidColorBrush)Application.Current.Resources["MdsTextPrimaryBrush"];
        SolidColorBrush inactiveFg = (SolidColorBrush)Application.Current.Resources["MdsTextSecondaryBrush"];

        ApplyBg(EditorBtn,  _mode == EditorMode.Editor,  active, activeFg, inactiveFg);
        ApplyBg(SplitBtn,   _mode == EditorMode.Split,   active, activeFg, inactiveFg);
        ApplyBg(PreviewBtn, _mode == EditorMode.Preview, active, activeFg, inactiveFg);
    }

    private static void ApplyBg(ToggleButton btn, bool active, Brush activeBg, Brush activeFg, Brush inactiveFg)
    {
        btn.Background = active ? activeBg : (Brush)Application.Current.Resources["MdsSidebarBrush"];
        btn.Foreground = active ? activeFg : inactiveFg;
    }
}
