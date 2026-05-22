using System;
using System.Collections.ObjectModel;

namespace MarkdownStudio.Models;

public sealed class SearchHit
{
    public string FilePath     { get; set; } = string.Empty;
    public string FileName     { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public int    LineNumber   { get; set; } // 0 = filename match
    public string Snippet      { get; set; } = string.Empty;
    public string Query        { get; set; } = string.Empty;

    // Index of the first occurrence of Query inside Snippet, or -1 if no match
    // (e.g. for filename-only hits where Snippet is empty).
    public int    MatchStart   { get; set; } = -1;

    public bool   IsFileNameMatch => LineNumber == 0;

    public string LinePrefix
        => LineNumber == 0 ? string.Empty : $"Line {LineNumber}: ";

    public string SnippetBeforeMatch
        => MatchStart < 0 ? Snippet : Snippet.Substring(0, MatchStart);

    public string MatchText
        => MatchStart < 0 ? string.Empty : Snippet.Substring(MatchStart, Math.Min(Query.Length, Snippet.Length - MatchStart));

    public string SnippetAfterMatch
        => MatchStart < 0 ? string.Empty : Snippet.Substring(MatchStart + Math.Min(Query.Length, Snippet.Length - MatchStart));

    public string DisplayLabel
        => LineNumber == 0 ? "filename match" : $"Line {LineNumber}: {Snippet}";
}

public sealed class SearchGroup
{
    public string FilePath     { get; set; } = string.Empty;
    public string FileName     { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public ObservableCollection<SearchHit> Hits { get; } = new();

    public int    HitCount   => Hits.Count;
    public string CountLabel => Hits.Count == 1 ? "1 match" : $"{Hits.Count} matches";
}
