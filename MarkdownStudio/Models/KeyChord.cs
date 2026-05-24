using System;
using System.Text;
using Windows.System;

namespace MarkdownStudio.Models;

/// <summary>
/// A single keyboard chord (one non-modifier key plus optional Ctrl/Shift/Alt).
/// Serialized as a plus-delimited string like "Ctrl+Shift+E" and displayed
/// the same way (Settings UI surfaces these to the user). Win key is omitted
/// because Windows reserves Win+* chords for shell shortcuts.
/// </summary>
public readonly struct KeyChord : IEquatable<KeyChord>
{
    public VirtualKey Key { get; }
    public VirtualKeyModifiers Modifiers { get; }

    public KeyChord(VirtualKey key, VirtualKeyModifiers modifiers = VirtualKeyModifiers.None)
    {
        Key = key;
        // Strip Windows modifier — we never bind to it and including it would
        // make round-trips through serialize/parse non-idempotent.
        Modifiers = modifiers & ~VirtualKeyModifiers.Windows;
    }

    public bool Ctrl  => (Modifiers & VirtualKeyModifiers.Control) != 0;
    public bool Shift => (Modifiers & VirtualKeyModifiers.Shift)   != 0;
    public bool Alt   => (Modifiers & VirtualKeyModifiers.Menu)    != 0;

    public bool IsEmpty => Key == VirtualKey.None;

    /// <summary>Stable serialization format (e.g. "Ctrl+Shift+E").</summary>
    public string Serialize() => Format("+");

    /// <summary>Display string used in the UI (e.g. "Ctrl+Shift+E").</summary>
    public override string ToString() => Format("+");

    private string Format(string sep)
    {
        if (IsEmpty) return string.Empty;
        var sb = new StringBuilder();
        if (Ctrl)  sb.Append("Ctrl").Append(sep);
        if (Shift) sb.Append("Shift").Append(sep);
        if (Alt)   sb.Append("Alt").Append(sep);
        sb.Append(KeyName(Key));
        return sb.ToString();
    }

    public static bool TryParse(string? s, out KeyChord chord)
    {
        chord = default;
        if (string.IsNullOrWhiteSpace(s)) return false;

        var parts = s.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        var mods = VirtualKeyModifiers.None;
        VirtualKey? main = null;
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i].Trim();
            switch (p.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    mods |= VirtualKeyModifiers.Control; break;
                case "shift":
                    mods |= VirtualKeyModifiers.Shift;   break;
                case "alt":
                case "menu":
                    mods |= VirtualKeyModifiers.Menu;    break;
                default:
                    if (TryParseKeyName(p, out var k)) main = k;
                    else return false;
                    break;
            }
        }
        if (!main.HasValue) return false;
        chord = new KeyChord(main.Value, mods);
        return true;
    }

    public bool Equals(KeyChord other) => Key == other.Key && Modifiers == other.Modifiers;
    public override bool Equals(object? obj) => obj is KeyChord c && Equals(c);
    public override int GetHashCode() => HashCode.Combine((int)Key, (int)Modifiers);
    public static bool operator ==(KeyChord a, KeyChord b) => a.Equals(b);
    public static bool operator !=(KeyChord a, KeyChord b) => !a.Equals(b);

    /// <summary>True if this key is purely a modifier — not bindable on its own.</summary>
    public static bool IsModifierOnly(VirtualKey k) =>
        k is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl
          or VirtualKey.Shift   or VirtualKey.LeftShift   or VirtualKey.RightShift
          or VirtualKey.Menu    or VirtualKey.LeftMenu    or VirtualKey.RightMenu
          or VirtualKey.LeftWindows or VirtualKey.RightWindows;

    // ---- Key name table ----
    //
    // We keep the spellings concise (matches what users expect in shortcut
    // panes everywhere else): "E", "Enter", "Esc", "F11", "PageDown", etc.
    private static string KeyName(VirtualKey k) => k switch
    {
        VirtualKey.Enter      => "Enter",
        VirtualKey.Escape     => "Esc",
        VirtualKey.Tab        => "Tab",
        VirtualKey.Space      => "Space",
        VirtualKey.Back       => "Backspace",
        VirtualKey.Delete     => "Delete",
        VirtualKey.Insert     => "Insert",
        VirtualKey.Home       => "Home",
        VirtualKey.End        => "End",
        VirtualKey.PageUp     => "PageUp",
        VirtualKey.PageDown   => "PageDown",
        VirtualKey.Left       => "Left",
        VirtualKey.Right      => "Right",
        VirtualKey.Up         => "Up",
        VirtualKey.Down       => "Down",
        >= VirtualKey.Number0 and <= VirtualKey.Number9 => ((int)k - (int)VirtualKey.Number0).ToString(),
        >= VirtualKey.A       and <= VirtualKey.Z       => ((char)('A' + (k - VirtualKey.A))).ToString(),
        >= VirtualKey.F1      and <= VirtualKey.F24     => "F" + ((int)k - (int)VirtualKey.F1 + 1),
        _ => k.ToString(),
    };

    private static bool TryParseKeyName(string name, out VirtualKey key)
    {
        switch (name.ToLowerInvariant())
        {
            case "enter":     key = VirtualKey.Enter; return true;
            case "esc":
            case "escape":    key = VirtualKey.Escape; return true;
            case "tab":       key = VirtualKey.Tab; return true;
            case "space":     key = VirtualKey.Space; return true;
            case "backspace": key = VirtualKey.Back; return true;
            case "delete":
            case "del":       key = VirtualKey.Delete; return true;
            case "insert":
            case "ins":       key = VirtualKey.Insert; return true;
            case "home":      key = VirtualKey.Home; return true;
            case "end":       key = VirtualKey.End; return true;
            case "pageup":
            case "pgup":      key = VirtualKey.PageUp; return true;
            case "pagedown":
            case "pgdn":      key = VirtualKey.PageDown; return true;
            case "left":      key = VirtualKey.Left; return true;
            case "right":     key = VirtualKey.Right; return true;
            case "up":        key = VirtualKey.Up; return true;
            case "down":      key = VirtualKey.Down; return true;
        }
        // A..Z
        if (name.Length == 1 && name[0] is >= 'A' and <= 'Z')
        {
            key = (VirtualKey)((int)VirtualKey.A + (name[0] - 'A'));
            return true;
        }
        if (name.Length == 1 && name[0] is >= 'a' and <= 'z')
        {
            key = (VirtualKey)((int)VirtualKey.A + (name[0] - 'a'));
            return true;
        }
        // 0..9
        if (name.Length == 1 && name[0] is >= '0' and <= '9')
        {
            key = (VirtualKey)((int)VirtualKey.Number0 + (name[0] - '0'));
            return true;
        }
        // F1..F24
        if ((name[0] == 'F' || name[0] == 'f') && int.TryParse(name.AsSpan(1), out var n) && n is >= 1 and <= 24)
        {
            key = (VirtualKey)((int)VirtualKey.F1 + (n - 1));
            return true;
        }
        key = VirtualKey.None;
        return false;
    }
}
