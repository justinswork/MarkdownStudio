using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MarkdownStudio.Models;

public sealed class DocumentTab : INotifyPropertyChanged
{
    private string? _filePath;
    private string _content = string.Empty;
    private bool _isDirty;
    private string _displayName = "Untitled";

    public Guid Id { get; } = Guid.NewGuid();

    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath == value) return;
            _filePath = value;
            DisplayName = string.IsNullOrEmpty(value) ? "Untitled" : System.IO.Path.GetFileName(value);
            OnPropertyChanged();
        }
    }

    public string DisplayName
    {
        get => _displayName;
        private set
        {
            if (_displayName == value) return;
            _displayName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Header));
        }
    }

    public string Header => IsDirty ? DisplayName + " •" : DisplayName;

    public string Content
    {
        get => _content;
        set
        {
            if (_content == value) return;
            _content = value;
            OnPropertyChanged();
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty == value) return;
            _isDirty = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Header));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
