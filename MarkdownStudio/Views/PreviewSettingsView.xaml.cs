using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MarkdownStudio.Models;
using MarkdownStudio.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;

namespace MarkdownStudio.Views;

public sealed partial class PreviewSettingsView : UserControl
{
    public IReadOnlyList<FontPreset>         FontList    { get; } = PreviewFontPresets.All;
    public IReadOnlyList<WidthPreset>        WidthList   { get; } = WidthPresets.All;
    public IReadOnlyList<HeadingStylePreset> HeadingList { get; } = HeadingStylePresets.All;

    private EditorPreferencesService? _service;
    private bool _suppressEvents;
    private List<SampleBlock> _sampleBlocks = new();

    public PreviewSettingsView()
    {
        InitializeComponent();
    }

    public void Attach(EditorPreferencesService service)
    {
        _service = service;
        ApplyPreferencesToUi(service.Preferences);
        service.Changed += p => DispatcherQueue.TryEnqueue(() => ApplyPreferencesToUi(p));
    }

    public async Task LoadSampleAsync()
    {
        var text = await MainWindow.GetBundledSampleAsync();
        _sampleBlocks = ParseSampleBlocks(text);
        RebuildPreviewStack();
        if (_service != null) UpdateSamplePreview(_service.Preferences);
    }

    private void ApplyPreferencesToUi(EditorPreferences prefs)
    {
        _suppressEvents = true;
        try
        {
            SelectByIdInto(FontCombo,    FontList,    prefs.PreviewFontPresetId, p => p.Id);
            SelectByIdInto(WidthCombo,   WidthList,   prefs.PreviewWidthId,       p => p.Id);
            SelectByIdInto(HeadingCombo, HeadingList, prefs.PreviewHeadingId,     p => p.Id);

            FontSizeSlider.Value = prefs.PreviewFontSize;
            FontSizeText.Text    = $"{prefs.PreviewFontSize} pt";

            LineHeightSlider.Value = prefs.PreviewLineHeight;
            LineHeightText.Text    = prefs.PreviewLineHeight.ToString("0.0", CultureInfo.InvariantCulture);

            UpdateSamplePreview(prefs);
        }
        finally { _suppressEvents = false; }
    }

    private static void SelectByIdInto<T>(
        ComboBox combo, IReadOnlyList<T> list, string id, Func<T, string> idOf)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (idOf(list[i]) == id) { combo.SelectedIndex = i; return; }
        }
    }

    // -------- Sample-driven preview --------

    private enum SampleBlockKind { H1, H2, H3, Body, Quote, Code }
    private sealed record SampleBlock(SampleBlockKind Kind, string Text);

    // Lightweight markdown-ish extraction: pulls out a few headings, body
    // paragraphs, and a blockquote from the sample so the right-hand panel
    // demonstrates the chosen typography on real content.
    private static List<SampleBlock> ParseSampleBlocks(string sample)
    {
        var blocks = new List<SampleBlock>();
        if (string.IsNullOrEmpty(sample)) return blocks;

        var lines = sample.Replace("\r\n", "\n").Split('\n');
        var buf = new System.Text.StringBuilder();
        bool inCode = false;

        void FlushBody()
        {
            if (buf.Length == 0) return;
            blocks.Add(new SampleBlock(SampleBlockKind.Body, buf.ToString().Trim()));
            buf.Clear();
        }

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("```"))
            {
                FlushBody();
                inCode = !inCode;
                continue;
            }
            if (inCode) continue;
            if (string.IsNullOrWhiteSpace(line))
            {
                FlushBody();
                continue;
            }
            if (line.StartsWith("# "))
            {
                FlushBody();
                blocks.Add(new SampleBlock(SampleBlockKind.H1, line.Substring(2).Trim()));
                continue;
            }
            if (line.StartsWith("## "))
            {
                FlushBody();
                blocks.Add(new SampleBlock(SampleBlockKind.H2, line.Substring(3).Trim()));
                continue;
            }
            if (line.StartsWith("### "))
            {
                FlushBody();
                blocks.Add(new SampleBlock(SampleBlockKind.H3, line.Substring(4).Trim()));
                continue;
            }
            if (line.StartsWith("> [!"))
            {
                FlushBody();
                var t = line.Substring(line.IndexOf(']') + 1).Trim();
                blocks.Add(new SampleBlock(SampleBlockKind.Quote, t));
                continue;
            }
            if (line.StartsWith("> "))
            {
                FlushBody();
                blocks.Add(new SampleBlock(SampleBlockKind.Quote, line.Substring(2).Trim()));
                continue;
            }
            if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                // skip list markers for the inline preview
                continue;
            }
            if (buf.Length > 0) buf.Append(' ');
            buf.Append(line);
        }
        FlushBody();

        // Trim the preview to the first interesting slice.
        if (blocks.Count > 10) blocks = blocks.GetRange(0, 10);
        return blocks;
    }

    private void RebuildPreviewStack()
    {
        PreviewStack.Children.Clear();
        foreach (var block in _sampleBlocks)
        {
            var tb = new TextBlock
            {
                Text = block.Text,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            };
            tb.Tag = block.Kind; // restyled in UpdateSamplePreview
            PreviewStack.Children.Add(tb);
        }
    }

    private void UpdateSamplePreview(EditorPreferences prefs)
    {
        if (_sampleBlocks.Count == 0) return;
        if (PreviewStack.Children.Count == 0) RebuildPreviewStack();

        var family = new FontFamily(prefs.PreviewFont.CssFamily);
        var baseSize   = prefs.PreviewFontSize;
        var lineHeight = baseSize * prefs.PreviewLineHeight;

        // Heading style preset → weight + scale factors.
        var headingId = prefs.PreviewHeadingId;
        double h1Scale, h2Scale, h3Scale;
        FontWeight headingWeight;
        Thickness h1Margin, h2Margin, h3Margin;
        switch (headingId)
        {
            case "minimal":
                h1Scale = 1.7; h2Scale = 1.35; h3Scale = 1.15;
                headingWeight = FontWeights.Medium;
                h1Margin = new Thickness(0, 12, 0, 4);
                h2Margin = new Thickness(0, 10, 0, 4);
                h3Margin = new Thickness(0,  8, 0, 4);
                break;
            case "display":
                h1Scale = 2.4; h2Scale = 1.8; h3Scale = 1.4;
                headingWeight = FontWeights.Bold;
                h1Margin = new Thickness(0, 18, 0, 10);
                h2Margin = new Thickness(0, 16, 0, 8);
                h3Margin = new Thickness(0, 14, 0, 6);
                break;
            default: // standard
                h1Scale = 1.9; h2Scale = 1.45; h3Scale = 1.2;
                headingWeight = FontWeights.SemiBold;
                h1Margin = new Thickness(0, 12, 0, 8);
                h2Margin = new Thickness(0, 12, 0, 6);
                h3Margin = new Thickness(0, 10, 0, 4);
                break;
        }

        var bodyMargin  = new Thickness(0, 0, 0, 10);
        var quoteMargin = new Thickness(0, 4, 0, 10);

        foreach (var child in PreviewStack.Children)
        {
            if (child is not TextBlock tb || tb.Tag is not SampleBlockKind kind) continue;
            tb.FontFamily = family;
            tb.LineHeight = lineHeight;

            switch (kind)
            {
                case SampleBlockKind.H1:
                    tb.FontSize   = baseSize * h1Scale;
                    tb.FontWeight = headingWeight;
                    tb.Margin     = h1Margin;
                    break;
                case SampleBlockKind.H2:
                    tb.FontSize   = baseSize * h2Scale;
                    tb.FontWeight = headingWeight;
                    tb.Margin     = h2Margin;
                    break;
                case SampleBlockKind.H3:
                    tb.FontSize   = baseSize * h3Scale;
                    tb.FontWeight = headingWeight;
                    tb.Margin     = h3Margin;
                    break;
                case SampleBlockKind.Quote:
                    tb.FontSize   = baseSize;
                    tb.FontWeight = FontWeights.Normal;
                    tb.FontStyle  = FontStyle.Italic;
                    tb.Margin     = quoteMargin;
                    tb.Opacity    = 0.82;
                    break;
                case SampleBlockKind.Body:
                default:
                    tb.FontSize   = baseSize;
                    tb.FontWeight = FontWeights.Normal;
                    tb.Margin     = bodyMargin;
                    tb.Opacity    = 1.0;
                    break;
            }
        }
    }

    // -------- Control handlers --------

    private void OnFontChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || _service == null) return;
        if (FontCombo.SelectedItem is FontPreset p) _service.SetPreviewFontPreset(p.Id);
    }

    private void OnFontSizeChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppressEvents || _service == null) return;
        var size = (int)Math.Round(e.NewValue);
        FontSizeText.Text = $"{size} pt";
        _service.SetPreviewFontSize(size);
    }

    private void OnLineHeightChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppressEvents || _service == null) return;
        var lh = Math.Round(e.NewValue * 100) / 100.0;
        LineHeightText.Text = lh.ToString("0.00", CultureInfo.InvariantCulture);
        _service.SetPreviewLineHeight(lh);
    }

    private void OnWidthChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || _service == null) return;
        if (WidthCombo.SelectedItem is WidthPreset p) _service.SetPreviewWidth(p.Id);
    }

    private void OnHeadingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || _service == null) return;
        if (HeadingCombo.SelectedItem is HeadingStylePreset p) _service.SetPreviewHeadingStyle(p.Id);
    }
}
