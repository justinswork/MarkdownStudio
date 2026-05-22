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

    public IReadOnlyList<MruEntry> Pinned
    {
        get
        {
            lock (_gate) return _entries
                .Where(e => e.IsPinned)
                .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public IReadOnlyList<MruEntry> Recent
    {
        get { lock (_gate) return _entries.Where(e => !e.IsPinned).ToList(); }
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
            var existing = _entries.FirstOrDefault(e =>
                string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
            var wasPinned = existing?.IsPinned ?? false;
            _entries.RemoveAll(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
            _entries.Insert(0, new MruEntry
            {
                Path = path,
                Kind = kind,
                LastOpened = DateTimeOffset.Now,
                IsPinned = wasPinned,
            });
            TrimRecentToMax();
            Save();
        }
        Changed?.Invoke();
    }

    public void SetPinned(string path, bool pinned)
    {
        lock (_gate)
        {
            var entry = _entries.FirstOrDefault(e =>
                string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
            if (entry == null || entry.IsPinned == pinned) return;
            entry.IsPinned = pinned;
            TrimRecentToMax();
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
            var removed = _entries.RemoveAll(e => !e.IsPinned);
            if (removed == 0) return;
            Save();
        }
        Changed?.Invoke();
    }

    // Cap non-pinned entries at Max; pinned ones never get evicted.
    private void TrimRecentToMax()
    {
        var nonPinned = _entries.Count(e => !e.IsPinned);
        if (nonPinned <= Max) return;
        var toRemove = nonPinned - Max;
        for (int i = _entries.Count - 1; i >= 0 && toRemove > 0; i--)
        {
            if (!_entries[i].IsPinned)
            {
                _entries.RemoveAt(i);
                toRemove--;
            }
        }
    }
}
