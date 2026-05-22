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

        var root = await Task.Run(() => BuildNode(_folder));
        if (root != null)
        {
            foreach (var child in root.Children)
                Roots.Add(child);
        }
    }

    private static FileTreeNode? BuildNode(string path)
    {
        try
        {
            var dirInfo = new DirectoryInfo(path);
            var node = new FileTreeNode
            {
                Name = dirInfo.Name,
                Path = dirInfo.FullName,
                IsDirectory = true,
            };

            foreach (var sub in dirInfo.EnumerateDirectories())
            {
                if (SkipDirs.Contains(sub.Name)) continue;
                if ((sub.Attributes & FileAttributes.Hidden) != 0) continue;
                var child = BuildNode(sub.FullName);
                if (child != null && (child.Children.Count > 0)) node.Children.Add(child);
            }

            foreach (var file in dirInfo.EnumerateFiles())
            {
                if (!FileService.IsMarkdownFile(file.FullName)) continue;
                if ((file.Attributes & FileAttributes.Hidden) != 0) continue;
                node.Children.Add(new FileTreeNode
                {
                    Name = file.Name,
                    Path = file.FullName,
                    IsDirectory = false,
                });
            }

            return node;
        }
        catch
        {
            return null;
        }
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
