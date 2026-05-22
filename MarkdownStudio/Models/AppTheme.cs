using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace MarkdownStudio.Models;

public sealed class AppTheme
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public ElementTheme BaseTheme { get; init; }
    public Color AccentColor { get; init; }
    public Color WindowFill { get; init; }
    public Color SidebarFill { get; init; }
    public Color RailFill { get; init; }
    public Color SurfaceFill { get; init; }
    public Color BorderColor { get; init; }
    public Color TextPrimary { get; init; }
    public Color TextSecondary { get; init; }
    public string MonacoTheme { get; init; } = "vs";
    public string PreviewClassName { get; init; } = "theme-daylight";
    public bool UseMica { get; init; }
    public bool FollowsSystem { get; init; }
}

public static class AppThemes
{
    private static Color C(byte r, byte g, byte b) => Color.FromArgb(255, r, g, b);

    public static readonly AppTheme System = new()
    {
        Id = "system",
        DisplayName = "Follow System",
        BaseTheme = ElementTheme.Default,
        FollowsSystem = true,
        UseMica = true,
        AccentColor = C(79, 168, 255),
    };

    public static readonly AppTheme Daylight = new()
    {
        Id = "daylight",
        DisplayName = "Daylight",
        BaseTheme = ElementTheme.Light,
        AccentColor = C(0, 122, 204),
        WindowFill = C(252, 252, 252),
        SidebarFill = C(247, 247, 248),
        RailFill = C(238, 238, 240),
        SurfaceFill = C(255, 255, 255),
        BorderColor = C(228, 228, 232),
        TextPrimary = C(20, 22, 28),
        TextSecondary = C(110, 114, 124),
        MonacoTheme = "ms-daylight",
        PreviewClassName = "theme-daylight",
        UseMica = true,
    };

    public static readonly AppTheme Midnight = new()
    {
        Id = "midnight",
        DisplayName = "Midnight",
        BaseTheme = ElementTheme.Dark,
        AccentColor = C(86, 156, 214),
        WindowFill = C(20, 22, 28),
        SidebarFill = C(26, 28, 36),
        RailFill = C(18, 19, 25),
        SurfaceFill = C(28, 30, 38),
        BorderColor = C(45, 48, 58),
        TextPrimary = C(232, 232, 236),
        TextSecondary = C(150, 154, 164),
        MonacoTheme = "ms-midnight",
        PreviewClassName = "theme-midnight",
        UseMica = true,
    };

    public static readonly AppTheme Sepia = new()
    {
        Id = "sepia",
        DisplayName = "Sepia",
        BaseTheme = ElementTheme.Light,
        AccentColor = C(165, 102, 35),
        WindowFill = C(247, 240, 224),
        SidebarFill = C(240, 232, 214),
        RailFill = C(230, 220, 200),
        SurfaceFill = C(252, 246, 232),
        BorderColor = C(218, 207, 184),
        TextPrimary = C(64, 50, 35),
        TextSecondary = C(120, 102, 78),
        MonacoTheme = "ms-sepia",
        PreviewClassName = "theme-sepia",
        UseMica = false,
    };

    public static readonly AppTheme SolarizedLight = new()
    {
        Id = "solarized-light",
        DisplayName = "Solarized Light",
        BaseTheme = ElementTheme.Light,
        AccentColor = C(38, 139, 210),
        WindowFill = C(253, 246, 227),
        SidebarFill = C(238, 232, 213),
        RailFill = C(225, 219, 202),
        SurfaceFill = C(253, 246, 227),
        BorderColor = C(213, 207, 188),
        TextPrimary = C(88, 110, 117),
        TextSecondary = C(131, 148, 150),
        MonacoTheme = "ms-solarized-light",
        PreviewClassName = "theme-solarized-light",
        UseMica = false,
    };

    public static readonly AppTheme SolarizedDark = new()
    {
        Id = "solarized-dark",
        DisplayName = "Solarized Dark",
        BaseTheme = ElementTheme.Dark,
        AccentColor = C(38, 139, 210),
        WindowFill = C(0, 43, 54),
        SidebarFill = C(7, 54, 66),
        RailFill = C(5, 44, 54),
        SurfaceFill = C(0, 43, 54),
        BorderColor = C(20, 70, 82),
        TextPrimary = C(238, 232, 213),
        TextSecondary = C(131, 148, 150),
        MonacoTheme = "ms-solarized-dark",
        PreviewClassName = "theme-solarized-dark",
        UseMica = false,
    };

    public static IReadOnlyList<AppTheme> All { get; } = new[]
    {
        System, Daylight, Midnight, Sepia, SolarizedLight, SolarizedDark,
    };

    public static AppTheme ById(string id) => All.FirstOrDefault(t => t.Id == id) ?? System;
}
