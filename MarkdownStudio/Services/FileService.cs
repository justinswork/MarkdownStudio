using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MarkdownStudio.Services;

public static class FileService
{
    public static readonly string[] MarkdownExtensions =
        { ".md", ".markdown", ".mdown", ".mkd", ".mdx", ".txt" };

    public static bool IsMarkdownFile(string path) =>
        Array.Exists(MarkdownExtensions, ext =>
            string.Equals(Path.GetExtension(path), ext, StringComparison.OrdinalIgnoreCase));

    public static async Task<(string? path, string? content)> OpenAsync(Window owner)
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        foreach (var ext in MarkdownExtensions)
            picker.FileTypeFilter.Add(ext);

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(owner));

        var file = await picker.PickSingleFileAsync();
        if (file == null) return (null, null);

        var text = await File.ReadAllTextAsync(file.Path);
        return (file.Path, text);
    }

    public static async Task<string?> PickFolderAsync(Window owner)
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add("*");

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(owner));

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    public static async Task<string?> PickSavePathAsync(Window owner, string? suggestedName)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedName ?? "Untitled",
        };
        picker.FileTypeChoices.Add("Markdown", new[] { ".md" });
        picker.FileTypeChoices.Add("Plain text", new[] { ".txt" });

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(owner));

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    public static async Task<string> ReadAllTextAsync(string path) =>
        await File.ReadAllTextAsync(path);

    public static async Task SaveAsync(string path, string content) =>
        await File.WriteAllTextAsync(path, content);
}
