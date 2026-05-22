using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using MarkdownStudio.Models;
using MarkdownStudio.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MarkdownStudio.Views;

public sealed partial class FileTreeView : UserControl
{
    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".svn", ".hg", "node_modules", "bin", "obj",
        ".vs", ".vscode", ".idea", "dist", "build", "out", "target",
    };

    public ObservableCollection<FileTreeNode> Roots { get; } = new();

    public event Action? OpenFolderRequested;
    public event Action<string>? FileOpenRequested;

    private string? _folder;

    public string? FolderPath
    {
        get => _folder;
        set
        {
            if (_folder == value) return;
            _folder = value;
            _ = RebuildAsync();
        }
    }

    public FileTreeView()
    {
        InitializeComponent();
    }

    private const int MaxDepth = 16;

    private async Task RebuildAsync()
    {
        Roots.Clear();
        if (string.IsNullOrEmpty(_folder) || !Directory.Exists(_folder))
        {
            FolderNameText.Text = "No folder";
            FolderPathText.Text = string.Empty;
            EmptyState.Visibility = Visibility.Visible;
            return;
        }

        FolderNameText.Text = new DirectoryInfo(_folder).Name;
        FolderPathText.Text = _folder;
        EmptyState.Visibility = Visibility.Collapsed;

        FileTreeNode? root;
        try
        {
            root = await Task.Run(() =>
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                return BuildNode(_folder!, 0, visited);
            });
        }
        catch (Exception ex)
        {
            FolderPathText.Text = $"Error: {ex.Message}";
            return;
        }

        if (root != null)
        {
            foreach (var child in root.Children) Roots.Add(child);
        }
    }

    private static FileTreeNode? BuildNode(string path, int depth, HashSet<string> visited)
    {
        if (depth > MaxDepth) return null;

        DirectoryInfo dirInfo;
        try { dirInfo = new DirectoryInfo(path); }
        catch { return null; }

        // Skip symlinks / junctions to avoid cycles (e.g. C:\Documents and Settings → C:\Users)
        if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0) return null;

        string canonical;
        try { canonical = dirInfo.FullName.TrimEnd(System.IO.Path.DirectorySeparatorChar); }
        catch { return null; }
        if (!visited.Add(canonical)) return null;

        var node = new FileTreeNode
        {
            Name = dirInfo.Name,
            Path = canonical,
            IsDirectory = true,
        };

        IEnumerable<DirectoryInfo> subs;
        try { subs = dirInfo.EnumerateDirectories(); }
        catch { subs = Array.Empty<DirectoryInfo>(); }

        foreach (var sub in subs)
        {
            try
            {
                if (SkipDirs.Contains(sub.Name)) continue;
                if ((sub.Attributes & FileAttributes.Hidden)      != 0) continue;
                if ((sub.Attributes & FileAttributes.System)      != 0) continue;
                if ((sub.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            }
            catch { continue; }

            var child = BuildNode(sub.FullName, depth + 1, visited);
            if (child != null && child.Children.Count > 0) node.Children.Add(child);
        }

        IEnumerable<FileInfo> files;
        try { files = dirInfo.EnumerateFiles(); }
        catch { files = Array.Empty<FileInfo>(); }

        foreach (var file in files)
        {
            try
            {
                if (!FileService.IsMarkdownFile(file.FullName)) continue;
                if ((file.Attributes & FileAttributes.Hidden) != 0) continue;
            }
            catch { continue; }

            node.Children.Add(new FileTreeNode
            {
                Name = file.Name,
                Path = file.FullName,
                IsDirectory = false,
            });
        }

        return node;
    }

    private void OnOpenFolder(object sender, RoutedEventArgs e) => OpenFolderRequested?.Invoke();

    private void OnItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is FileTreeNode node && !node.IsDirectory)
        {
            FileOpenRequested?.Invoke(node.Path);
        }
    }
}
