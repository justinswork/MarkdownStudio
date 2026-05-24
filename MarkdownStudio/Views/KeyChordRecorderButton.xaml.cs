using System;
using MarkdownStudio.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace MarkdownStudio.Views;

/// <summary>
/// Press-to-record button for a single <see cref="KeyChord"/>. Click to start
/// recording — the next non-modifier KeyDown becomes the chord and is raised
/// via <see cref="ChordCaptured"/>. Esc cancels recording without firing.
/// </summary>
public sealed partial class KeyChordRecorderButton : UserControl
{
    private bool _recording;

    public KeyChord Chord { get; private set; }

    public event Action<KeyChord>? ChordCaptured;

    public KeyChordRecorderButton()
    {
        InitializeComponent();
        // KeyDown on the UserControl doesn't fire reliably for arrow / Tab /
        // Enter etc. (XAML eats them for focus traversal). Subscribing via
        // PreviewKeyDown on the inner button bypasses that.
        RecordButton.PreviewKeyDown += OnPreviewKeyDown;
        RecordButton.LostFocus      += OnLostFocus;
    }

    public void SetChord(KeyChord chord, bool fireEvent = false)
    {
        Chord = chord;
        UpdateLabel();
        if (fireEvent) ChordCaptured?.Invoke(chord);
    }

    private void OnRecordClicked(object sender, RoutedEventArgs e)
    {
        if (_recording)
        {
            // Click again while recording: cancel and revert label.
            StopRecording(commit: false);
            return;
        }
        _recording = true;
        ChordText.Text = "Press a key…";
        RecordButton.Focus(FocusState.Programmatic);
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        // If the user clicks away while the button is in "Press a key…"
        // mode, abandon the capture so the row goes back to a clean state.
        if (_recording) StopRecording(commit: false);
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_recording) return;

        // Plain modifier presses don't form a complete chord — keep waiting.
        if (KeyChord.IsModifierOnly(e.Key)) { e.Handled = true; return; }

        // Esc bails out of recording without binding (Esc itself is still
        // bindable: it just can't be its own escape hatch here).
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            StopRecording(commit: false);
            return;
        }

        var mods = GetModifiers();
        var chord = new KeyChord(e.Key, mods);
        e.Handled = true;
        Chord = chord;
        UpdateLabel();
        _recording = false;
        ChordCaptured?.Invoke(chord);
    }

    private void StopRecording(bool commit)
    {
        _recording = false;
        UpdateLabel();
        if (commit) ChordCaptured?.Invoke(Chord);
    }

    private void UpdateLabel()
    {
        ChordText.Text = Chord.IsEmpty ? "Unbound" : Chord.ToString();
    }

    private static VirtualKeyModifiers GetModifiers()
    {
        // CoreWindow.GetKeyState is the supported way to ask for modifier
        // state inside XAML key handlers — KeyRoutedEventArgs doesn't expose
        // the modifier flags directly.
        var mods = VirtualKeyModifiers.None;
        if (IsDown(VirtualKey.Control)) mods |= VirtualKeyModifiers.Control;
        if (IsDown(VirtualKey.Shift))   mods |= VirtualKeyModifiers.Shift;
        if (IsDown(VirtualKey.Menu))    mods |= VirtualKeyModifiers.Menu;
        return mods;
    }

    private static bool IsDown(VirtualKey k)
    {
        var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(k);
        return (state & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
    }
}
