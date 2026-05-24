using System.Collections.Generic;
using MarkdownStudio.Models;
using MarkdownStudio.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MarkdownStudio.Views;

/// <summary>
/// Settings → Shortcuts pane. Lists every command grouped by category with a
/// press-to-record recorder button and a per-row reset. Conflict resolution
/// surfaces via the InfoBar at the top of the panel.
/// </summary>
public sealed partial class KeyboardShortcutsView : UserControl
{
    private KeyboardShortcutService? _service;
    private readonly Dictionary<string, KeyChordRecorderButton> _recorderByCommand = new();
    private readonly Dictionary<string, KeyChord> _previousByCommand = new();

    // Pending conflict state: when a captured chord clashes with another
    // command we hold it here while the InfoBar asks the user what to do.
    private string?   _pendingCommandId;
    private KeyChord  _pendingChord;
    private string?   _pendingConflictWith;

    public KeyboardShortcutsView()
    {
        InitializeComponent();
    }

    public void Attach(KeyboardShortcutService service)
    {
        _service = service;
        BuildRows();
        _service.Changed += () => DispatcherQueue.TryEnqueue(RefreshChords);
    }

    private void BuildRows()
    {
        if (_service is null) return;
        RowsHost.Children.Clear();
        _recorderByCommand.Clear();
        _previousByCommand.Clear();

        string? currentCategory = null;
        foreach (var (cmd, chord) in _service.Snapshot())
        {
            if (cmd.Category != currentCategory)
            {
                currentCategory = cmd.Category;
                RowsHost.Children.Add(BuildCategoryHeader(cmd.Category));
            }
            RowsHost.Children.Add(BuildCommandRow(cmd, chord));
            _previousByCommand[cmd.Id] = chord;
        }
    }

    private TextBlock BuildCategoryHeader(string category) => new()
    {
        Text = category,
        Margin = new Thickness(8, 10, 8, 2),
        Style  = (Style)Application.Current.Resources["MdsCaptionStyle"],
    };

    private Grid BuildCommandRow(AppCommand cmd, KeyChord chord)
    {
        var row = new Grid
        {
            Padding = new Thickness(8, 6, 8, 6),
            ColumnSpacing = 12,
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var name = new TextBlock
        {
            Text = cmd.DisplayName,
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)Application.Current.Resources["MdsBodyStyle"],
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(name, 0);
        row.Children.Add(name);

        var recorder = new KeyChordRecorderButton { VerticalAlignment = VerticalAlignment.Center };
        recorder.SetChord(chord);
        recorder.ChordCaptured += newChord => OnChordCaptured(cmd.Id, newChord);
        Grid.SetColumn(recorder, 1);
        row.Children.Add(recorder);
        _recorderByCommand[cmd.Id] = recorder;

        var reset = new Button
        {
            Style   = (Style)Application.Current.Resources["MdsGhostButtonStyle"],
            Width   = 32,
            Height  = 32,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Content = new FontIcon { Glyph = "", FontSize = 12 }, // Undo (reset to default)
        };
        ToolTipService.SetToolTip(reset, $"Reset \"{cmd.DisplayName}\" to default ({cmd.Default})");
        reset.Click += (s, e) => _service?.ResetCommand(cmd.Id);
        Grid.SetColumn(reset, 2);
        row.Children.Add(reset);

        return row;
    }

    /// <summary>
    /// Sync every recorder's displayed chord to the service. Used after a
    /// reset-all, a per-row reset, or a conflict reassignment (which may
    /// have cleared a binding the user wasn't editing).
    /// </summary>
    private void RefreshChords()
    {
        if (_service is null) return;
        foreach (var (cmd, chord) in _service.Snapshot())
        {
            if (_recorderByCommand.TryGetValue(cmd.Id, out var rec))
            {
                rec.SetChord(chord);
                _previousByCommand[cmd.Id] = chord;
            }
        }
    }

    private void OnChordCaptured(string commandId, KeyChord newChord)
    {
        if (_service is null) return;
        var conflictId = _service.FindConflict(commandId, newChord);
        if (conflictId is null)
        {
            _service.SetBinding(commandId, newChord);
            _previousByCommand[commandId] = newChord;
            HideConflict();
            return;
        }

        // Stage the conflict — the InfoBar's buttons decide what happens next.
        _pendingCommandId    = commandId;
        _pendingChord        = newChord;
        _pendingConflictWith = conflictId;

        var otherName = AppCommands.FindById(conflictId)?.DisplayName ?? conflictId;
        ConflictBar.Message =
            $"\"{newChord}\" is currently bound to \"{otherName}\". Reassign?";
        ConflictBar.IsOpen = true;
    }

    private void OnReassignClicked(object sender, RoutedEventArgs e)
    {
        if (_service is null || _pendingCommandId is null) { HideConflict(); return; }
        _service.SetBinding(_pendingCommandId, _pendingChord, clearConflict: true);
        // RefreshChords will fire from the service's Changed event, which
        // unbinds the conflicting recorder.
        _previousByCommand[_pendingCommandId] = _pendingChord;
        HideConflict();
    }

    // Wired to the InfoBar's CloseButtonClick (X) — same signature as a
    // regular Click handler for clarity. Reverts the recorder to its prior
    // value so the cancelled chord doesn't linger on the row's label.
    private void OnCancelConflictClicked(object sender, object e)
    {
        if (_pendingCommandId is not null &&
            _recorderByCommand.TryGetValue(_pendingCommandId, out var rec) &&
            _previousByCommand.TryGetValue(_pendingCommandId, out var prior))
        {
            // The service was never touched, so just bring the label back.
            rec.SetChord(prior);
        }
        HideConflict();
    }

    private void HideConflict()
    {
        ConflictBar.IsOpen   = false;
        _pendingCommandId    = null;
        _pendingChord        = default;
        _pendingConflictWith = null;
    }

    private void OnResetAllClicked(object sender, RoutedEventArgs e) => _service?.ResetAll();
}
