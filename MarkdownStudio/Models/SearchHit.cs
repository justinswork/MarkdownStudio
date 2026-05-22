using System.Collections.ObjectModel;

namespace MarkdownStudio.Models;

public sealed class SearchHit
{
    public string FilePath     { get; set; } = string.Empty;
    public string FileName     { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public int    LineNumber   { get; set; } // 0 = filename match
    public string Snippet      { get; set; } = string.Empty;

    public bool   IsFileNameMatch => LineNumber == 0;
    public string DisplayLabel    => LineNumber == 0
        ? "filename match"
        : $"Line {LineNumber}: {Snippet}";
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
