using System;
using System.Collections.Generic;
using System.Text.Json;
using MarkdownStudio.Models;
using Windows.Storage;

namespace MarkdownStudio.Services;

/// <summary>
/// Owns the live binding for every <see cref="AppCommand"/>. Persists user
/// overrides as a single JSON blob in LocalSettings so we don't have to
/// migrate one key per command if the catalog grows. Defaults come from
/// <see cref="AppCommands.All"/> and are used as the fallback whenever a
/// stored chord can't be parsed.
/// </summary>
public sealed class KeyboardShortcutService
{
    private const string StorageKey = "shortcuts.v1";

    // commandId -> active chord. Always contains every command (defaults
    // filled in if no override exists).
    private readonly Dictionary<string, KeyChord> _bindings = new();

    public event Action? Changed;

    public KeyboardShortcutService()
    {
        // Seed defaults first so every command is queryable, then layer
        // saved overrides on top.
        foreach (var cmd in AppCommands.All) _bindings[cmd.Id] = cmd.Default;
        LoadOverrides();
    }

    public KeyChord GetBinding(string commandId) =>
        _bindings.TryGetValue(commandId, out var c) ? c : default;

    /// <summary>
    /// Returns a snapshot of every (command, chord) pair in catalog order.
    /// Suitable for binding to a ListView in Settings.
    /// </summary>
    public IReadOnlyList<(AppCommand Command, KeyChord Chord)> Snapshot()
    {
        var list = new List<(AppCommand, KeyChord)>(AppCommands.All.Count);
        foreach (var cmd in AppCommands.All) list.Add((cmd, GetBinding(cmd.Id)));
        return list;
    }

    /// <summary>
    /// Returns the id of a command already using <paramref name="chord"/>
    /// (other than <paramref name="commandId"/>), or null if no conflict.
    /// Empty chords never conflict — they mean "unbound".
    /// </summary>
    public string? FindConflict(string commandId, KeyChord chord)
    {
        if (chord.IsEmpty) return null;
        foreach (var kvp in _bindings)
        {
            if (kvp.Key == commandId) continue;
            if (kvp.Value == chord) return kvp.Key;
        }
        return null;
    }

    /// <summary>
    /// Apply a new binding. If <paramref name="clearConflict"/> is true and
    /// another command currently owns <paramref name="chord"/>, that other
    /// command is unbound (set to an empty chord) so the new assignment wins.
    /// </summary>
    public void SetBinding(string commandId, KeyChord chord, bool clearConflict = false)
    {
        if (AppCommands.FindById(commandId) is null) return;
        var current = GetBinding(commandId);
        if (current == chord) return;

        if (clearConflict)
        {
            var other = FindConflict(commandId, chord);
            if (other != null) _bindings[other] = default;
        }

        _bindings[commandId] = chord;
        SaveOverrides();
        Changed?.Invoke();
    }

    /// <summary>Restore <paramref name="commandId"/> to its catalog default.</summary>
    public void ResetCommand(string commandId)
    {
        var cmd = AppCommands.FindById(commandId);
        if (cmd is null) return;
        if (_bindings[commandId] == cmd.Default) return;
        _bindings[commandId] = cmd.Default;
        SaveOverrides();
        Changed?.Invoke();
    }

    /// <summary>Restore every command to its catalog default.</summary>
    public void ResetAll()
    {
        bool anyChanged = false;
        foreach (var cmd in AppCommands.All)
        {
            if (_bindings[cmd.Id] != cmd.Default)
            {
                _bindings[cmd.Id] = cmd.Default;
                anyChanged = true;
            }
        }
        if (!anyChanged) return;
        SaveOverrides();
        Changed?.Invoke();
    }

    // ---- Persistence ----

    private void LoadOverrides()
    {
        var raw = ApplicationData.Current.LocalSettings.Values[StorageKey] as string;
        if (string.IsNullOrEmpty(raw)) return;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!_bindings.ContainsKey(prop.Name)) continue; // unknown command — ignore
                var s = prop.Value.GetString();
                if (string.IsNullOrEmpty(s))
                {
                    _bindings[prop.Name] = default;        // explicitly unbound
                }
                else if (KeyChord.TryParse(s, out var chord))
                {
                    _bindings[prop.Name] = chord;
                }
                // otherwise: keep the default we seeded
            }
        }
        catch
        {
            // Corrupt blob — silently fall back to defaults so the user
            // isn't locked out of their app.
        }
    }

    private void SaveOverrides()
    {
        // Persist only deviations from default so a future catalog tweak
        // (e.g. changing the default for Save) doesn't strand users on the
        // old chord they never explicitly chose.
        var overrides = new Dictionary<string, string>();
        foreach (var cmd in AppCommands.All)
        {
            var chord = _bindings[cmd.Id];
            if (chord != cmd.Default) overrides[cmd.Id] = chord.Serialize();
        }
        var json = JsonSerializer.Serialize(overrides);
        ApplicationData.Current.LocalSettings.Values[StorageKey] = json;
    }
}
