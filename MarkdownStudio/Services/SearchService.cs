using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarkdownStudio.Models;

namespace MarkdownStudio.Services;

public static class SearchService
{
    private const int    MaxResults     = 500;
    private const long   MaxFileSize    = 5 * 1024 * 1024;
    private const int    MaxHitsPerFile = 50;
    private const int    SnippetMaxLen  = 200;

    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".svn", ".hg", "node_modules", "bin", "obj",
        ".vs", ".vscode", ".idea", "dist", "build", "out", "target",
    };

    public static Task<List<SearchGroup>> SearchAsync(string rootFolder, string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrEmpty(rootFolder) || !Directory.Exists(rootFolder))
            return Task.FromResult(new List<SearchGroup>());

        return Task.Run(() => SearchSync(rootFolder, query, ct), ct);
    }

    private static List<SearchGroup> SearchSync(string root, string query, CancellationToken ct)
    {
        var state = new SearchState
        {
            RootFolder  = root,
            Query       = query,
            QueryLower  = query.ToLowerInvariant(),
            Ct          = ct,
        };

        Walk(root, state);

        return state.Groups.Values
            .OrderByDescending(g => g.Hits.Any(h => h.IsFileNameMatch))
            .ThenBy(g => g.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void Walk(string current, SearchState state)
    {
        if (state.Ct.IsCancellationRequested || state.TotalHits >= MaxResults) return;

        DirectoryInfo dirInfo;
        try { dirInfo = new DirectoryInfo(current); }
        catch { return; }

        if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0) return;

        IEnumerable<DirectoryInfo> subs;
        try { subs = dirInfo.EnumerateDirectories(); }
        catch { subs = Array.Empty<DirectoryInfo>(); }

        foreach (var sub in subs)
        {
            if (state.Ct.IsCancellationRequested || state.TotalHits >= MaxResults) return;
            try
            {
                if (SkipDirs.Contains(sub.Name)) continue;
                if ((sub.Attributes & FileAttributes.Hidden)       != 0) continue;
                if ((sub.Attributes & FileAttributes.System)       != 0) continue;
                if ((sub.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            }
            catch { continue; }

            Walk(sub.FullName, state);
        }

        IEnumerable<FileInfo> files;
        try { files = dirInfo.EnumerateFiles(); }
        catch { files = Array.Empty<FileInfo>(); }

        foreach (var file in files)
        {
            if (state.Ct.IsCancellationRequested || state.TotalHits >= MaxResults) return;
            try
            {
                if (!FileService.IsMarkdownFile(file.FullName)) continue;
                if ((file.Attributes & FileAttributes.Hidden) != 0) continue;
                ScanFile(file, state);
            }
            catch { /* ignore unreadable files */ }
        }
    }

    private static void ScanFile(FileInfo file, SearchState state)
    {
        var nameMatches = file.Name.ToLowerInvariant().Contains(state.QueryLower);

        // Filename-only match for huge files.
        if (file.Length > MaxFileSize)
        {
            if (nameMatches) AddFileNameHit(file, state);
            return;
        }

        if (nameMatches) AddFileNameHit(file, state);

        string[] lines;
        try { lines = File.ReadAllLines(file.FullName); }
        catch { return; }

        var hitsInThisFile = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (state.Ct.IsCancellationRequested || state.TotalHits >= MaxResults) return;
            if (hitsInThisFile >= MaxHitsPerFile) return;

            var line = lines[i];
            if (line.IndexOf(state.QueryLower, StringComparison.OrdinalIgnoreCase) < 0) continue;

            var snippet = Truncate(line.Trim(), SnippetMaxLen);
            var matchStart = snippet.IndexOf(state.Query, StringComparison.OrdinalIgnoreCase);

            var group = GetOrCreateGroup(file, state);
            group.Hits.Add(new SearchHit
            {
                FilePath     = file.FullName,
                FileName     = file.Name,
                RelativePath = GetRelative(file.FullName, state.RootFolder),
                LineNumber   = i + 1,
                Snippet      = snippet,
                Query        = state.Query,
                MatchStart   = matchStart,
            });
            state.TotalHits++;
            hitsInThisFile++;
        }
    }

    private static void AddFileNameHit(FileInfo file, SearchState state)
    {
        var group = GetOrCreateGroup(file, state);
        if (group.Hits.Any(h => h.IsFileNameMatch)) return;
        group.Hits.Insert(0, new SearchHit
        {
            FilePath     = file.FullName,
            FileName     = file.Name,
            RelativePath = GetRelative(file.FullName, state.RootFolder),
            LineNumber   = 0,
            Snippet      = string.Empty,
            Query        = state.Query,
        });
        state.TotalHits++;
    }

    private static SearchGroup GetOrCreateGroup(FileInfo file, SearchState state)
    {
        if (!state.Groups.TryGetValue(file.FullName, out var group))
        {
            group = new SearchGroup
            {
                FilePath     = file.FullName,
                FileName     = file.Name,
                RelativePath = GetRelative(file.FullName, state.RootFolder),
            };
            state.Groups[file.FullName] = group;
        }
        return group;
    }

    private static string GetRelative(string path, string root)
    {
        try
        {
            var rel = Path.GetRelativePath(root, path);
            return rel.Replace(Path.DirectorySeparatorChar, '/');
        }
        catch { return path; }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max) + "…";

    private sealed class SearchState
    {
        public string RootFolder = string.Empty;
        public string Query      = string.Empty;
        public string QueryLower = string.Empty;
        public CancellationToken Ct;
        public Dictionary<string, SearchGroup> Groups = new(StringComparer.OrdinalIgnoreCase);
        public int TotalHits;
    }
}
