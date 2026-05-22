using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MarkdownStudio.Models;
using Windows.Storage;

namespace MarkdownStudio.Services;

public sealed class MruService
{
    private const string SettingsKey = "mru.entries.v1";
    private const int Max = 30;

    private readonly object _gate = new();
    private List<MruEntry> _entries;

    public event Action? Changed;

    public MruService()
    {
        _entries = Load();
    }

    private static List<MruEntry> Load()
    {
        try
        {
            if (ApplicationData.Current.LocalSettings.Values[SettingsKey] is string raw &&
                !string.IsNullOrEmpty(raw))
            {
                return JsonSerializer.Deserialize<List<MruEntry>>(raw) ?? new();
            }
        }
        catch { }
        return new();
    }

    private void Save()
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values[SettingsKey] = JsonSerializer.Serialize(_entries);
        }
        catch { }
    }

    public IReadOnlyList<MruEntry> Entries
    {
        get { lock (_gate) return _entries.ToList(); }
    }

    public IReadOnlyList<MruEntry> Files
    {
        get { lock (_gate) return _entries.Where(e => e.Kind == MruKind.File).ToList(); }
    }

    public IReadOnlyList<MruEntry> Folders
    {
        get { lock (_gate) return _entries.Where(e => e.Kind == MruKind.Folder).ToList(); }
    }

    public void Touch(string path, MruKind kind)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        lock (_gate)
        {
            _entries.RemoveAll(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
            _entries.Insert(0, new MruEntry
            {
                Path = path,
                Kind = kind,
                LastOpened = DateTimeOffset.Now,
            });
            if (_entries.Count > Max) _entries = _entries.Take(Max).ToList();
            Save();
        }
        Changed?.Invoke();
    }

    public void Remove(string path)
    {
        lock (_gate)
        {
            var removed = _entries.RemoveAll(e =>
                string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
            if (removed == 0) return;
            Save();
        }
        Changed?.Invoke();
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (_entries.Count == 0) return;
            _entries.Clear();
            Save();
        }
        Changed?.Invoke();
    }
}
