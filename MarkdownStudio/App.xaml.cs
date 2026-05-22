using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Windows.Storage;

namespace MarkdownStudio;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"Unhandled: {e.Exception}");
            e.Handled = true;
        };
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var startupFiles = ExtractStartupFiles();
        _window = new MainWindow(startupFiles);
        _window.Activate();
    }

    // Pulls file paths out of the activation event when the app is launched by
    // File Explorer (or "Open with") for one or more registered file types.
    private static IReadOnlyList<string> ExtractStartupFiles()
    {
        var paths = new List<string>();
        try
        {
            var args = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (args?.Kind == ExtendedActivationKind.File &&
                args.Data is IFileActivatedEventArgs fileArgs)
            {
                foreach (var item in fileArgs.Files)
                {
                    if (item is StorageFile f && !string.IsNullOrEmpty(f.Path))
                        paths.Add(f.Path);
                }
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Activation parse failed: {ex.Message}");
        }
        return paths;
    }
}
