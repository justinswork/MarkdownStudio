using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MarkdownStudio.Models;
using MarkdownStudio.Services;
using MarkdownStudio.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MarkdownStudio;

public sealed partial class MainWindow : Window
{
    private const string WelcomeTabId = "welcome";
    private const string ViewModeKey  = "viewMode.v1";

    private readonly AppThemeService _appTheme = new();
    private readonly MruService _mru = new();
    private readonly EditorPreferencesService _prefs = new();
    private readonly KeyboardShortcutService _shortcuts = new();
    private readonly Dictionary<TabViewItem, EditorPaneControl> _panes = new();

    private readonly WelcomeView _welcomeView = new();
    private readonly FileTreeView _fileTreeView = new();
    private readonly SearchView _searchView = new();
    private readonly OutlineView _outlineView = new();


    private TabViewItem? _welcomeTab;
    private bool _focusMode;

    // App-close prompt orchestration. _promptingClose: we're mid-loop, ignore
    // duplicate close clicks. _acceptingClose: prompts all resolved, the next
    // Closing event should let the window go through.
    private bool _promptingClose = false;
    private bool _acceptingClose = false;

    private enum SavePromptResult { Save, Discard, Cancel }
    private readonly IReadOnlyList<string> _startupFiles;

    public MainWindow() : this(null) { }

    public MainWindow(IReadOnlyList<string>? startupFiles)
    {
        _startupFiles = startupFiles ?? Array.Empty<string>();

        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Catch app close (X / Alt+F4 / Exit menu) so we can prompt for dirty tabs.
        try { AppWindow.Closing += OnAppWindowClosing; }
        catch { /* not available in some hosting modes */ }

        WireUpViews();

        _appTheme.Changed += t => DispatcherQueue.TryEnqueue(() => ApplyTheme(t));
        ApplyTheme(_appTheme.Selected);

        // Build keyboard accelerators from the service and rebuild on change.
        // Also pushes the user's x-ray chords down into each editor pane.
        ApplyShortcuts();
        _shortcuts.Changed += () => DispatcherQueue.TryEnqueue(ApplyShortcuts);

        // Restore the saved view mode before any tabs are created.
        ModeControl.Mode = LoadSavedMode();

        SetSidebarPane(ActivityPane.None);
        ShowWelcomeTab();

        // After the window's XAML root is wired up, open any files we were
        // activated with. The "make us the default markdown handler" affordance
        // lives in Settings → General → File associations rather than as an
        // unprompted first-launch dialog.
        RootGrid.Loaded += async (_, _) =>
        {
            await SeedSampleIfFirstRunAsync();
            foreach (var path in _startupFiles)
            {
                try { await OpenFileFromPathAsync(path); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Startup open failed: {ex.Message}"); }
            }
        };
    }

    private const string SampleSeededKey = "sampleSeeded.v1";

    // On the user's first launch, copy the bundled docs/Sample.md from the
    // MSIX install directory into ApplicationData.LocalFolder (which is
    // writable, unlike the install directory), and add it to the MRU so the
    // Welcome page shows a friendly starting point. Idempotent across runs.
    private async Task SeedSampleIfFirstRunAsync()
    {
        var values = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
        if (values[SampleSeededKey] is bool seeded && seeded) return;

        try
        {
            var bundled = Path.Combine(AppContext.BaseDirectory, "Samples", "Sample.md");
            if (!File.Exists(bundled))
            {
                values[SampleSeededKey] = true;
                return;
            }

            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            var dest = Path.Combine(localFolder, "Welcome to Markdown Studio.md");
            if (!File.Exists(dest))
            {
                var contents = await File.ReadAllTextAsync(bundled);
                await File.WriteAllTextAsync(dest, contents);
            }

            _mru.Touch(dest, MruKind.File);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Seeding sample failed: {ex.Message}");
        }
        finally
        {
            values[SampleSeededKey] = true;
        }
    }

    private static EditorMode LoadSavedMode()
    {
        // Default to Preview on first launch — this is a reader-first app
        // (markdown viewing being the primary use case, editing secondary).
        // Once the user picks a different mode it's persisted and that wins.
        var raw = Windows.Storage.ApplicationData.Current.LocalSettings.Values[ViewModeKey] as string;
        return Enum.TryParse<EditorMode>(raw, out var m) ? m : EditorMode.Preview;
    }

    private static void SaveMode(EditorMode mode)
    {
        Windows.Storage.ApplicationData.Current.LocalSettings.Values[ViewModeKey] = mode.ToString();
    }

    private void WireUpViews()
    {
        _welcomeView.Attach(_mru);
        _welcomeView.OpenFolderRequested += async () => await OpenFolderInteractiveAsync();
        _welcomeView.OpenFileRequested   += async () => await OpenFileInteractiveAsync();
        _welcomeView.NewFileRequested    += CreateBlankTab;
        _welcomeView.MruActivated        += OnMruActivated;

        _fileTreeView.OpenFolderRequested += async () => await OpenFolderInteractiveAsync();
        _fileTreeView.FileOpenRequested   += async path => await OpenFileFromPathAsync(path);

        _searchView.HitActivated += hit => _ = OpenFileAtLineAsync(hit.FilePath, hit.LineNumber, hit.Query);

        _outlineView.HeadingActivated += node =>
        {
            if (CurrentPane is { } pane) _ = pane.RevealLineAsync(node.LineNumber);
        };

        _prefs.Changed += p => DispatcherQueue.TryEnqueue(() =>
        {
            ApplyFontPrefs(p);
            ApplyPreviewPrefs(p);
        });

        ActivityRail.HomeRequested     += ShowWelcomeTab;
        ActivityRail.SettingsRequested += () => _ = ShowSettingsDialogAsync();
        ActivityRail.PaneSelected      += SetSidebarPane;

        ModeControl.ModeChanged += OnModeChanged;
    }

    private async Task ShowSettingsDialogAsync()
    {
        // ContentDialog hosts in its own popup root which doesn't inherit the
        // window's RequestedTheme automatically — set it explicitly so the
        // dialog chrome (title, primary button, frame) matches the app. We
        // also subscribe the dialog to AppThemeService so it re-skins live
        // when the user picks a different theme from the General tab.
        var dlg = new SettingsDialog
        {
            XamlRoot       = RootGrid.XamlRoot,
            RequestedTheme = _appTheme.EffectiveElementTheme,
        };
        dlg.AttachThemeService(_appTheme);

        // Seed the dialog's embedded WebView2s with the current app theme
        // (not the system theme) before they navigate.
        dlg.EditorSettings.InitialMonacoTheme   = _appTheme.EffectiveMonacoTheme;
        dlg.PreviewSettings.InitialPreviewTheme = _appTheme.EffectivePreviewClass;

        // The dialog instantiates fresh panes; attach the services here.
        dlg.ThemePicker.SetSelected(_appTheme.Selected);
        dlg.ThemePicker.ThemeSelected += t => _appTheme.Select(t);
        dlg.EditorSettings.Attach(_prefs);
        dlg.PreviewSettings.Attach(_prefs);
        dlg.ShortcutSettings.Attach(_shortcuts);
        // Settings dialog uses Sample.md content in both tabs' preview area.
        await dlg.EditorSettings.LoadSampleAsync();
        await dlg.PreviewSettings.LoadSampleAsync();
        await dlg.ShowAsync();
    }

    private static string? _cachedSample;
    internal static async Task<string> GetBundledSampleAsync()
    {
        if (_cachedSample != null) return _cachedSample;
        var path = Path.Combine(AppContext.BaseDirectory, "Samples", "Sample.md");
        if (!File.Exists(path)) return _cachedSample = string.Empty;
        try { _cachedSample = await File.ReadAllTextAsync(path); }
        catch { _cachedSample = string.Empty; }
        return _cachedSample;
    }

    // ---- Theme ----
    private void ApplyTheme(AppTheme theme)
    {
        var effective = theme.FollowsSystem ? _appTheme.EffectiveTheme : theme;

        RootGrid.RequestedTheme = _appTheme.EffectiveElementTheme;

        SetBrushColor("MdsWindowBrush",        effective.WindowFill);
        SetBrushColor("MdsRailBrush",          effective.RailFill);
        SetBrushColor("MdsSidebarBrush",       effective.SidebarFill);
        SetBrushColor("MdsSurfaceBrush",       effective.SurfaceFill);
        SetBrushColor("MdsBorderBrush",        effective.BorderColor);
        SetBrushColor("MdsTextPrimaryBrush",   effective.TextPrimary);
        SetBrushColor("MdsTextSecondaryBrush", effective.TextSecondary);
        SetBrushColor("MdsAccentBrush",        effective.AccentColor);
        var a = effective.AccentColor;
        SetBrushColor("MdsAccentSoftBrush",  Color.FromArgb(31, a.R, a.G, a.B));
        SetBrushColor("MdsAccentHoverBrush", Color.FromArgb(51, a.R, a.G, a.B));

        try
        {
            SystemBackdrop = effective.UseMica ? new MicaBackdrop() : null;
        }
        catch { /* unsupported on some hardware */ }

        ApplyCaptionButtonColors(effective);

        ThemeStatusText.Text = theme.DisplayName;

        var monaco  = _appTheme.EffectiveMonacoTheme;
        var preview = _appTheme.EffectivePreviewClass;
        foreach (var pane in _panes.Values)
            _ = pane.ApplyThemeAsync(monaco, preview);
    }

    private static void SetBrushColor(string key, Color color)
    {
        if (Application.Current.Resources[key] is SolidColorBrush b)
            b.Color = color;
    }

    private void ApplyFontPrefs(EditorPreferences prefs)
    {
        foreach (var pane in _panes.Values)
        {
            _ = pane.SetEditorFontAsync(prefs.Font.CssFamily, prefs.FontSize, prefs.TabSize);
            _ = pane.SetRenderWhitespaceAsync(prefs.ShowWhitespace);
        }
    }

    private void ApplyPreviewPrefs(EditorPreferences prefs)
    {
        var family = prefs.PreviewFont.CssFamily;
        var width  = prefs.PreviewWidth.CssMaxWidth;
        var cls    = prefs.PreviewHeadingStyle.CssClass;
        foreach (var pane in _panes.Values)
        {
            _ = pane.SetPreviewOptionsAsync(family, prefs.PreviewFontSize,
                                            prefs.PreviewLineHeight, width, cls);
        }
    }

    private void ApplyCaptionButtonColors(AppTheme effective)
    {
        try
        {
            var tb = AppWindow?.TitleBar;
            if (tb == null) return;

            // Background under the caption buttons stays transparent so Mica /
            // theme background shows through.
            tb.ButtonBackgroundColor         = Color.FromArgb(0, 0, 0, 0);
            tb.ButtonInactiveBackgroundColor = Color.FromArgb(0, 0, 0, 0);

            tb.ButtonForegroundColor         = effective.TextPrimary;
            tb.ButtonInactiveForegroundColor = effective.TextSecondary;

            // Subtle hover: 10% of the primary text colour so it's visible on
            // every theme.
            var hoverBg = Color.FromArgb(28, effective.TextPrimary.R,
                                              effective.TextPrimary.G,
                                              effective.TextPrimary.B);
            var pressedBg = Color.FromArgb(48, effective.TextPrimary.R,
                                                effective.TextPrimary.G,
                                                effective.TextPrimary.B);
            tb.ButtonHoverBackgroundColor   = hoverBg;
            tb.ButtonHoverForegroundColor   = effective.TextPrimary;
            tb.ButtonPressedBackgroundColor = pressedBg;
            tb.ButtonPressedForegroundColor = effective.TextPrimary;
        }
        catch { /* AppWindow not ready or unsupported */ }
    }

    // ---- Sidebar ----
    private void SetSidebarPane(ActivityPane pane)
    {
        UIElement? content = pane switch
        {
            ActivityPane.Files    => _fileTreeView,
            ActivityPane.Search   => _searchView,
            ActivityPane.Outline  => _outlineView,
            _                     => null,
        };

        if (content == null)
        {
            SidebarHost.Visibility = Visibility.Collapsed;
            SidebarPresenter.Content = null;
        }
        else
        {
            SidebarPresenter.Content = content;
            SidebarHost.Visibility = Visibility.Visible;
        }
    }

    // ---- Tabs ----
    private TabViewItem GetOrCreateWelcomeTab()
    {
        if (_welcomeTab != null) return _welcomeTab;

        var tab = new TabViewItem
        {
            IconSource = new SymbolIconSource { Symbol = Symbol.Home },
            Header = "Welcome",
            Tag = WelcomeTabId,
        };
        _welcomeTab = tab;
        Tabs.TabItems.Insert(0, tab);
        return tab;
    }

    private TabViewItem AddEditorTab(DocumentTab? doc = null)
    {
        doc ??= new DocumentTab();
        var p = _prefs.Preferences;
        var pane = new EditorPaneControl
        {
            Document = doc,
            MonacoTheme = _appTheme.EffectiveMonacoTheme,
            PreviewTheme = _appTheme.EffectivePreviewClass,
            InitialFontFamily     = p.Font.CssFamily,
            InitialFontSize       = p.FontSize,
            InitialTabSize        = p.TabSize,
            InitialShowWhitespace = p.ShowWhitespace,
            InitialPreviewFontFamily   = p.PreviewFont.CssFamily,
            InitialPreviewFontSize     = p.PreviewFontSize,
            InitialPreviewLineHeight   = p.PreviewLineHeight,
            InitialPreviewWidthCss     = p.PreviewWidth.CssMaxWidth,
            InitialPreviewHeadingClass = p.PreviewHeadingStyle.CssClass,
            // Seed x-ray shortcuts so the first keydown after pane init
            // already knows the user's chords (without waiting for the
            // post-ready push).
            InitialXrayStartChord  = _shortcuts.GetBinding(AppCommands.XrayStart).Serialize(),
            InitialXrayApplyChord  = _shortcuts.GetBinding(AppCommands.XrayApply).Serialize(),
            InitialXrayCancelChord = _shortcuts.GetBinding(AppCommands.XrayCancel).Serialize(),
        };
        // New tab inherits the current view mode (Editor / Split / Preview).
        _ = pane.SetLayoutAsync(ModeToLayout(ModeControl.Mode));
        pane.TextChanged += text => DispatcherQueue.TryEnqueue(() =>
        {
            if (CurrentPane == pane)
                _outlineView.SetNodes(OutlineService.Parse(text));
        });
        pane.FocusToggleRequested += () => DispatcherQueue.TryEnqueue(ToggleFocusMode);
        // Ctrl+S / Ctrl+Shift+S in the Monaco editor are caught by Monaco
        // before WebView2's Edge runtime can swallow them as a browser
        // "Save Page" accelerator; the editor posts them up to here.
        pane.SaveRequested   += () => DispatcherQueue.TryEnqueue(() => _ = SaveCurrentAsync(false));
        pane.SaveAsRequested += () => DispatcherQueue.TryEnqueue(() => _ = SaveCurrentAsync(true));
        // Pasting an image into an Untitled tab: the pane awaits this hook to
        // get the doc saved (so it knows where to write the .png alongside)
        // before the image bytes are written.
        pane.PasteImageNeedsSaveAsync = d => PromptSaveForPastedImageAsync(d, pane);
        pane.ImagePasted += relPath => DispatcherQueue.TryEnqueue(() =>
            StatusText.Text = $"Inserted {relPath}");

        var tab = new TabViewItem
        {
            IconSource = new SymbolIconSource { Symbol = Symbol.Document },
            Header = doc.Header,
            Tag = doc,
        };
        _panes[tab] = pane;

        doc.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DocumentTab.Header))
                DispatcherQueue.TryEnqueue(() => tab.Header = doc.Header);
        };

        Tabs.TabItems.Add(tab);
        Tabs.SelectedItem = tab;
        return tab;
    }

    private void ShowWelcomeTab()
    {
        var t = GetOrCreateWelcomeTab();
        Tabs.SelectedItem = t;
    }

    private EditorPaneControl? CurrentPane =>
        Tabs.SelectedItem is TabViewItem item && _panes.TryGetValue(item, out var p) ? p : null;

    // ---- Mode ----
    private static EditorLayout ModeToLayout(EditorMode mode) => mode switch
    {
        EditorMode.Editor  => EditorLayout.EditorOnly,
        EditorMode.Preview => EditorLayout.PreviewOnly,
        _                  => EditorLayout.Split,
    };

    private void OnModeChanged(EditorMode mode)
    {
        var layout = ModeToLayout(mode);
        foreach (var pane in _panes.Values)
            _ = pane.SetLayoutAsync(layout);
        SaveMode(mode);
    }

    // ---- File operations ----
    private async Task OpenFileInteractiveAsync()
    {
        var (path, content) = await FileService.OpenAsync(this);
        if (path == null || content == null) return;
        await OpenFileAsync(path, content);
    }

    private async Task OpenFileFromPathAsync(string path)
    {
        try
        {
            var content = await FileService.ReadAllTextAsync(path);
            await OpenFileAsync(path, content);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Couldn't open file", ex.Message);
        }
    }

    private async Task OpenFileAsync(string path, string content)
    {
        foreach (var (tab, pane) in _panes)
        {
            if (string.Equals(pane.Document?.FilePath, path, StringComparison.OrdinalIgnoreCase))
            {
                Tabs.SelectedItem = tab;
                _mru.Touch(path, MruKind.File);
                return;
            }
        }

        var doc = new DocumentTab { FilePath = path, Content = content };
        AddEditorTab(doc);
        if (CurrentPane is { } newPane) await newPane.SetContentAsync(content);
        _mru.Touch(path, MruKind.File);
    }

    private async Task OpenFolderInteractiveAsync()
    {
        var path = await FileService.PickFolderAsync(this);
        if (string.IsNullOrEmpty(path)) return;
        OpenFolder(path);
    }

    private void OpenFolder(string path)
    {
        _fileTreeView.FolderPath = path;
        _searchView.FolderPath   = path;
        _mru.Touch(path, MruKind.Folder);
        ActivityRail.CurrentPane = ActivityPane.Files;
        SetSidebarPane(ActivityPane.Files);
        _fileTreeView.PulseFolderBanner();
        StatusText.Text = $"Opened folder: {Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar))}";
        UpdateRailVisibility();
    }

    // Gate the Search / Outline rail buttons by app state:
    //   Search  — visible once any file or folder has been opened.
    //   Outline — visible only while an editor tab (not Welcome) is active.
    private void UpdateRailVisibility()
    {
        var folderOpen   = !string.IsNullOrEmpty(_fileTreeView.FolderPath);
        var hasOpenItem  = folderOpen || _panes.Count > 0;
        var hasActiveDoc = Tabs.SelectedItem is TabViewItem item && _panes.ContainsKey(item);
        ActivityRail.ShowSearch  = hasOpenItem;
        ActivityRail.ShowOutline = hasActiveDoc;
    }

    private async Task OpenFileAtLineAsync(string path, int lineNumber, string? query = null)
    {
        await OpenFileFromPathAsync(path);
        if (lineNumber > 0 && CurrentPane is { } pane)
            await pane.RevealLineAsync(lineNumber, query);
    }

    private void OnMruActivated(MruEntry entry)
    {
        if (entry.Kind == MruKind.Folder)
        {
            if (Directory.Exists(entry.Path)) OpenFolder(entry.Path);
            else _mru.Remove(entry.Path);
        }
        else
        {
            if (File.Exists(entry.Path)) _ = OpenFileFromPathAsync(entry.Path);
            else _mru.Remove(entry.Path);
        }
    }

    // ---- Menu / button handlers ----
    private void OnNew(object sender, RoutedEventArgs e) => CreateBlankTab();
    private async void OnOpenFile(object sender, RoutedEventArgs e)   => await OpenFileInteractiveAsync();
    private async void OnOpenFolder(object sender, RoutedEventArgs e) => await OpenFolderInteractiveAsync();
    private async void OnSave(object sender, RoutedEventArgs e)       => await SaveCurrentAsync(saveAs: false);
    private async void OnSaveAs(object sender, RoutedEventArgs e)     => await SaveCurrentAsync(saveAs: true);
    private void OnShowWelcome(object sender, RoutedEventArgs e)      => ShowWelcomeTab();
    private async void OnCloseTab(object sender, RoutedEventArgs e)
    {
        if (Tabs.SelectedItem is TabViewItem item) await TryCloseTabAsync(item);
    }
    private void OnExit(object sender, RoutedEventArgs e) => Close();

    private void OnSplitMode(object sender, RoutedEventArgs e)
    { ModeControl.Mode = EditorMode.Split;   OnModeChanged(EditorMode.Split); }
    private void OnEditorMode(object sender, RoutedEventArgs e)
    { ModeControl.Mode = EditorMode.Editor;  OnModeChanged(EditorMode.Editor); }
    private void OnPreviewMode(object sender, RoutedEventArgs e)
    { ModeControl.Mode = EditorMode.Preview; OnModeChanged(EditorMode.Preview); }

    private async void OnToggleWordWrap(object sender, RoutedEventArgs e) =>
        await (CurrentPane?.ToggleWordWrapAsync() ?? Task.CompletedTask);

    private async void OnFindInDocument(object sender, RoutedEventArgs e) =>
        await (CurrentPane?.OpenFindAsync() ?? Task.CompletedTask);

    private void OnToggleFocus(object sender, RoutedEventArgs e)      => ToggleFocusMode();
    private void OnToggleFocusMenu(object sender, RoutedEventArgs e)  => ToggleFocusMode();

    private void ToggleFocusMode()
    {
        _focusMode = !_focusMode;
        var hidden = _focusMode ? Visibility.Collapsed : Visibility.Visible;
        AppTitleBar.Visibility = hidden;
        RailColumn.Width    = _focusMode ? new GridLength(0) : GridLength.Auto;
        SidebarColumn.Width = _focusMode ? new GridLength(0) : GridLength.Auto;
        SidebarHost.Visibility = _focusMode ? Visibility.Collapsed : SidebarHost.Visibility;
        TopToolbar.Visibility = hidden;
        Tabs.Visibility = hidden;
        StatusBar.Visibility = hidden;
    }

    // ---- Keyboard accelerators driven by KeyboardShortcutService ----
    //
    // Rebuilt whenever bindings change. Host-side commands get a real
    // KeyboardAccelerator on RootGrid; the x-ray commands are pushed down
    // to preview.js (it owns its own keydown handler so it can react in
    // textareas where WinUI accelerators don't fire).

    private static bool IsHostCommand(string commandId) => commandId switch
    {
        AppCommands.XrayStart  or
        AppCommands.XrayApply  or
        AppCommands.XrayCancel => false,
        _                      => true,
    };

    private void ApplyShortcuts()
    {
        RootGrid.KeyboardAccelerators.Clear();
        foreach (var cmd in AppCommands.All)
        {
            if (!IsHostCommand(cmd.Id)) continue;
            var chord = _shortcuts.GetBinding(cmd.Id);
            if (chord.IsEmpty) continue;
            var accel = new KeyboardAccelerator { Key = chord.Key, Modifiers = chord.Modifiers };
            var capturedId = cmd.Id;
            accel.Invoked += (s, a) => { a.Handled = true; InvokeCommand(capturedId); };
            RootGrid.KeyboardAccelerators.Add(accel);
        }
        UpdateMenuShortcutHints();
        PushXrayShortcutsToPanes();
    }

    private void InvokeCommand(string id)
    {
        switch (id)
        {
            case AppCommands.NewFile:     AddEditorTab(); break;
            case AppCommands.OpenFile:    _ = OpenFileInteractiveAsync(); break;
            case AppCommands.OpenFolder:  _ = OpenFolderInteractiveAsync(); break;
            case AppCommands.Save:        _ = SaveCurrentAsync(false); break;
            case AppCommands.SaveAs:      _ = SaveCurrentAsync(true); break;
            case AppCommands.CloseTab:
                if (Tabs.SelectedItem is TabViewItem item) _ = TryCloseTabAsync(item);
                break;
            case AppCommands.Find:        _ = CurrentPane?.OpenFindAsync() ?? Task.CompletedTask; break;
            case AppCommands.ToggleFocus: ToggleFocusMode(); break;
        }
    }

    private void UpdateMenuShortcutHints()
    {
        string Display(string id) => _shortcuts.GetBinding(id).ToString();
        MiNew.KeyboardAcceleratorTextOverride         = Display(AppCommands.NewFile);
        MiOpenFile.KeyboardAcceleratorTextOverride    = Display(AppCommands.OpenFile);
        MiOpenFolder.KeyboardAcceleratorTextOverride  = Display(AppCommands.OpenFolder);
        MiSave.KeyboardAcceleratorTextOverride        = Display(AppCommands.Save);
        MiSaveAs.KeyboardAcceleratorTextOverride      = Display(AppCommands.SaveAs);
        MiCloseTab.KeyboardAcceleratorTextOverride    = Display(AppCommands.CloseTab);
        MiFind.KeyboardAcceleratorTextOverride        = Display(AppCommands.Find);
        MiToggleFocus.KeyboardAcceleratorTextOverride = Display(AppCommands.ToggleFocus);
    }

    private void PushXrayShortcutsToPanes()
    {
        var start  = _shortcuts.GetBinding(AppCommands.XrayStart).Serialize();
        var apply  = _shortcuts.GetBinding(AppCommands.XrayApply).Serialize();
        var cancel = _shortcuts.GetBinding(AppCommands.XrayCancel).Serialize();
        foreach (var pane in _panes.Values)
        {
            _ = pane.SetXrayShortcutsAsync(start, apply, cancel);
        }
    }

    private async Task SaveCurrentAsync(bool saveAs)
    {
        var pane = CurrentPane;
        var doc = pane?.Document;
        if (pane == null || doc == null) return;
        await SaveDocumentAsync(doc, pane, saveAs);
    }

    // Returns false if the save was aborted (e.g., the user cancelled the
    // file picker). Used by the close-prompt flow to decide whether to
    // continue the close or back out.
    private async Task<bool> SaveDocumentAsync(DocumentTab doc, EditorPaneControl pane, bool saveAs)
    {
        var text = await pane.GetContentAsync();
        doc.Content = text;

        var path = doc.FilePath;
        if (string.IsNullOrEmpty(path) || saveAs)
        {
            path = await FileService.PickSavePathAsync(this, doc.DisplayName);
            if (string.IsNullOrEmpty(path)) return false;
            doc.FilePath = path;
        }

        try { await FileService.SaveAsync(path, text); }
        catch (Exception ex)
        {
            await ShowMessageAsync("Couldn't save", ex.Message);
            return false;
        }
        doc.IsDirty = false;
        _mru.Touch(path, MruKind.File);
        StatusText.Text = $"Saved {Path.GetFileName(path)}";
        return true;
    }

    private async void OnAbout(object sender, RoutedEventArgs e)
    {
        var version = TryGetPackageVersionString();
        var body = (version is null ? "" : $"Version {version}\n\n") +
                   "A native markdown editor for Windows.\nBuilt with WinUI 3 and .NET 10.";
        await ShowMessageAsync("Markdown Studio", body);
    }

    // Reads the version stamped into Package.appxmanifest at build time
    // (build-release.ps1 stamps it from the -Version argument, so there's a
    // single source of truth). Trailing-zero parts are dropped for a tidier
    // display: 0.1.0.0 -> "0.1.0", 0.1.2.3 -> "0.1.2.3". Returns null on
    // unpackaged dev runs where Package.Current isn't available.
    private static string? TryGetPackageVersionString()
    {
        try
        {
            var v = Windows.ApplicationModel.Package.Current.Id.Version;
            if (v.Revision == 0 && v.Build == 0) return $"{v.Major}.{v.Minor}";
            if (v.Revision == 0)                 return $"{v.Major}.{v.Minor}.{v.Build}";
            return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        }
        catch
        {
            return null;
        }
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = RootGrid.XamlRoot,
        };
        await dialog.ShowAsync();
    }

    // ---- Tab events ----
    private async void Tabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args) =>
        await TryCloseTabAsync(args.Tab);

    // Returns false if the user cancelled the close (used by app-close to
    // abort the whole shutdown). Welcome tabs close without prompting.
    private async Task<bool> TryCloseTabAsync(TabViewItem item)
    {
        if (item == _welcomeTab)
        {
            _welcomeTab = null;
            Tabs.TabItems.Remove(item);
            if (Tabs.TabItems.Count == 0) ShowWelcomeTab();
            return true;
        }

        if (_panes.TryGetValue(item, out var pane) && pane.Document is { } doc && doc.IsDirty)
        {
            // Sync Document.Content with whatever's currently in the editor —
            // the change-message is debounced, so the last few keystrokes may
            // not have been flushed yet.
            try { doc.Content = await pane.GetContentAsync(); }
            catch { /* fall back to whatever we have */ }

            Tabs.SelectedItem = item;
            var result = await PromptSaveAsync(doc);
            if (result == SavePromptResult.Cancel) return false;
            if (result == SavePromptResult.Save)
            {
                if (!await SaveDocumentAsync(doc, pane, saveAs: false)) return false;
            }
        }

        _panes.Remove(item);
        Tabs.TabItems.Remove(item);
        if (Tabs.TabItems.Count == 0) ShowWelcomeTab();
        return true;
    }

    // Pasted image into an Untitled doc: explain that the doc needs a home on
    // disk first (so we can place the image next to it), then drive Save-As.
    // Returns true if the document now has a FilePath.
    private async Task<bool> PromptSaveForPastedImageAsync(DocumentTab doc, EditorPaneControl pane)
    {
        var dialog = new ContentDialog
        {
            Title = "Save document first",
            Content = "Pasted images are stored alongside the markdown file. " +
                      "Save this document first to insert the image.",
            PrimaryButtonText = "Save As...",
            CloseButtonText   = "Cancel",
            DefaultButton     = ContentDialogButton.Primary,
            XamlRoot          = RootGrid.XamlRoot,
        };
        ContentDialogResult res;
        try { res = await dialog.ShowAsync(); }
        catch { return false; }
        if (res != ContentDialogResult.Primary) return false;
        return await SaveDocumentAsync(doc, pane, saveAs: true);
    }

    private async Task<SavePromptResult> PromptSaveAsync(DocumentTab doc)
    {
        var name = string.IsNullOrEmpty(doc.DisplayName) ? "Untitled" : doc.DisplayName;
        var dialog = new ContentDialog
        {
            Title = "Save changes?",
            Content = $"\"{name}\" has unsaved changes. Save before closing?",
            PrimaryButtonText   = "Save",
            SecondaryButtonText = "Don't save",
            CloseButtonText     = "Cancel",
            DefaultButton       = ContentDialogButton.Primary,
            XamlRoot            = RootGrid.XamlRoot,
        };
        ContentDialogResult res;
        try { res = await dialog.ShowAsync(); }
        catch { return SavePromptResult.Cancel; }
        return res switch
        {
            ContentDialogResult.Primary   => SavePromptResult.Save,
            ContentDialogResult.Secondary => SavePromptResult.Discard,
            _                             => SavePromptResult.Cancel,
        };
    }

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // We already approved this close (re-entry from our own Close() call).
        if (_acceptingClose) return;
        // A prompt loop is already in flight. Just cancel the duplicate.
        if (_promptingClose) { args.Cancel = true; return; }

        // Walk dirty tabs in tab order so the user sees them left-to-right.
        var dirty = new List<TabViewItem>();
        foreach (var obj in Tabs.TabItems)
        {
            if (obj is TabViewItem t && _panes.TryGetValue(t, out var p)
                && p.Document?.IsDirty == true)
                dirty.Add(t);
        }
        if (dirty.Count == 0) return; // nothing to prompt — let the window close

        args.Cancel = true;          // hold the window open while we prompt
        _promptingClose = true;
        try
        {
            foreach (var tab in dirty)
            {
                if (!await TryCloseTabAsync(tab)) return; // user cancelled — abort shutdown
            }
            _acceptingClose = true;
            Close();
        }
        finally
        {
            _promptingClose = false;
        }
    }

    private void Tabs_AddTabButtonClick(TabView sender, object args) => CreateBlankTab();

    private void CreateBlankTab()
    {
        // A blank document has nothing to preview, so always open in Editor mode.
        if (ModeControl.Mode != EditorMode.Editor)
        {
            ModeControl.Mode = EditorMode.Editor;
            OnModeChanged(EditorMode.Editor);
        }
        AddEditorTab();
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (Tabs.SelectedItem is not TabViewItem item)
            {
                ActiveContent.Content = null;
                DocInfoText.Text = string.Empty;
                _outlineView.SetNodes(Array.Empty<OutlineNode>());
                return;
            }

            if (item == _welcomeTab)
            {
                ActiveContent.Content = _welcomeView;
                DocInfoText.Text = "Welcome";
                TopToolbar.Visibility = Visibility.Collapsed;
                _outlineView.SetNodes(Array.Empty<OutlineNode>());
                return;
            }

            if (!_focusMode) TopToolbar.Visibility = Visibility.Visible;

            if (_panes.TryGetValue(item, out var pane))
            {
                ActiveContent.Content = pane;
                var doc = pane.Document;
                DocInfoText.Text = doc?.FilePath != null
                    ? $"Markdown · UTF-8 · {Path.GetFileName(doc.FilePath)}"
                    : "Markdown · UTF-8";
                _outlineView.SetNodes(OutlineService.Parse(pane.Document?.Content ?? string.Empty));
            }
        }
        finally
        {
            UpdateRailVisibility();
        }
    }
}
