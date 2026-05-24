using System.Collections.Generic;
using Windows.System;

namespace MarkdownStudio.Models;

/// <summary>
/// Metadata for a single rebindable command. The Id is the stable key used
/// for persistence (don't rename it). DisplayName is what the user sees in
/// Settings → Shortcuts. Category groups the list visually.
/// </summary>
public sealed class AppCommand
{
    public string   Id          { get; }
    public string   DisplayName { get; }
    public string   Category    { get; }
    public KeyChord Default     { get; }

    public AppCommand(string id, string displayName, string category, KeyChord @default)
    {
        Id          = id;
        DisplayName = displayName;
        Category    = category;
        Default     = @default;
    }
}

public static class AppCommands
{
    // Stable IDs — used for persistence keys and as the contract with the
    // preview JS (see Web/preview/preview.js — host.setShortcuts payload).
    public const string NewFile        = "file.new";
    public const string OpenFile       = "file.open";
    public const string OpenFolder     = "file.openFolder";
    public const string Save           = "file.save";
    public const string SaveAs         = "file.saveAs";
    public const string CloseTab       = "file.closeTab";
    public const string Find           = "edit.find";
    public const string ToggleFocus    = "view.toggleFocus";
    public const string XrayStart      = "xray.start";
    public const string XrayApply      = "xray.apply";
    public const string XrayCancel     = "xray.cancel";

    private const string CatFile  = "File";
    private const string CatEdit  = "Edit";
    private const string CatView  = "View";
    private const string CatXray  = "X-ray edit";

    /// <summary>
    /// Canonical command catalog with defaults. Order here is the order
    /// shown to the user in Settings (so list by category, then frequency).
    /// </summary>
    public static readonly IReadOnlyList<AppCommand> All = new[]
    {
        new AppCommand(NewFile,     "New file",                CatFile, new KeyChord(VirtualKey.N,    VirtualKeyModifiers.Control)),
        new AppCommand(OpenFile,    "Open file…",              CatFile, new KeyChord(VirtualKey.O,    VirtualKeyModifiers.Control)),
        new AppCommand(OpenFolder,  "Open folder…",            CatFile, new KeyChord(VirtualKey.O,    VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)),
        new AppCommand(Save,        "Save",                    CatFile, new KeyChord(VirtualKey.S,    VirtualKeyModifiers.Control)),
        new AppCommand(SaveAs,      "Save as…",                CatFile, new KeyChord(VirtualKey.S,    VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)),
        new AppCommand(CloseTab,    "Close tab",               CatFile, new KeyChord(VirtualKey.W,    VirtualKeyModifiers.Control)),
        new AppCommand(Find,        "Find in document",        CatEdit, new KeyChord(VirtualKey.F,    VirtualKeyModifiers.Control)),
        new AppCommand(ToggleFocus, "Toggle distraction-free", CatView, new KeyChord(VirtualKey.F11)),
        new AppCommand(XrayStart,   "Start X-ray edit",        CatXray, new KeyChord(VirtualKey.E,    VirtualKeyModifiers.Control)),
        new AppCommand(XrayApply,   "Apply X-ray edit",        CatXray, new KeyChord(VirtualKey.Enter, VirtualKeyModifiers.Control)),
        new AppCommand(XrayCancel,  "Cancel X-ray edit",       CatXray, new KeyChord(VirtualKey.Escape)),
    };

    public static AppCommand? FindById(string id)
    {
        foreach (var c in All) if (c.Id == id) return c;
        return null;
    }
}
