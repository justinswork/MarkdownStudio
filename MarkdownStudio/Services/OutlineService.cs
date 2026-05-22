using System.Collections.Generic;
using MarkdownStudio.Models;

namespace MarkdownStudio.Services;

public static class OutlineService
{
    public static List<OutlineNode> Parse(string markdown)
    {
        var flat = new List<OutlineNode>();
        if (string.IsNullOrEmpty(markdown)) return new();

        var lines = markdown.Split('\n');
        bool inFence = false;
        string? fenceMarker = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i].TrimEnd('\r');
            var trimmed = rawLine.TrimStart();

            // Track fenced code blocks ``` or ~~~
            if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
            {
                var marker = trimmed.StartsWith("```") ? "```" : "~~~";
                if (!inFence) { inFence = true; fenceMarker = marker; }
                else if (fenceMarker == marker) { inFence = false; fenceMarker = null; }
                continue;
            }
            if (inFence) continue;

            // Match ATX headings (# - ######)
            if (rawLine.Length > 0 && rawLine[0] == '#')
            {
                int level = 0;
                while (level < rawLine.Length && level < 6 && rawLine[level] == '#') level++;
                if (level >= 1 && level <= 6 &&
                    (level == rawLine.Length || rawLine[level] == ' '))
                {
                    var title = rawLine.Substring(level).Trim();
                    // strip trailing #s
                    title = title.TrimEnd('#').TrimEnd();
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        flat.Add(new OutlineNode
                        {
                            Title = title,
                            Level = level,
                            LineNumber = i + 1,
                        });
                    }
                }
            }
        }

        return BuildTree(flat);
    }

    private static List<OutlineNode> BuildTree(List<OutlineNode> flat)
    {
        var roots = new List<OutlineNode>();
        var stack = new Stack<OutlineNode>();
        foreach (var node in flat)
        {
            while (stack.Count > 0 && stack.Peek().Level >= node.Level) stack.Pop();
            if (stack.Count == 0) roots.Add(node);
            else stack.Peek().Children.Add(node);
            stack.Push(node);
        }
        return roots;
    }
}
