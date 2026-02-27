using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;

namespace ExtintorCrm.App.Presentation.Controls;

public partial class MarkdownViewerControl : UserControl
{
    public static readonly DependencyProperty MarkdownTextProperty = DependencyProperty.Register(
        nameof(MarkdownText),
        typeof(string),
        typeof(MarkdownViewerControl),
        new PropertyMetadata(string.Empty, OnMarkdownTextChanged));

    public MarkdownViewerControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        MarkdownViewer.AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnHyperlinkRequestNavigate));
    }

    public string? MarkdownText
    {
        get => (string?)GetValue(MarkdownTextProperty);
        set => SetValue(MarkdownTextProperty, value);
    }

    private static void OnMarkdownTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewerControl control)
        {
            control.RenderMarkdown();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RenderMarkdown();
    }

    private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch
        {
            // No-op: keep UI responsive even if shell cannot open the link.
        }

        e.Handled = true;
    }

    private void RenderMarkdown()
    {
        MarkdownViewer.Document = BuildDocument(MarkdownText);
    }

    private static FlowDocument BuildDocument(string? markdown)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            TextAlignment = TextAlignment.Left,
            FontFamily = new FontFamily("Segoe UI Variable"),
            FontSize = 14,
            LineHeight = 23
        };
        document.SetResourceReference(TextElement.ForegroundProperty, "TextPrimary");

        if (string.IsNullOrWhiteSpace(markdown))
        {
            var emptyParagraph = new Paragraph(new Run("Sem observacoes."))
            {
                Margin = new Thickness(0, 0, 0, 0)
            };
            emptyParagraph.SetResourceReference(TextElement.ForegroundProperty, "TextSecondary");
            document.Blocks.Add(emptyParagraph);
            return document;
        }

        var normalized = markdown.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');

        var paragraphLines = new List<string>();
        var listItems = new List<ListItem>();
        bool? currentListOrdered = null;

        var codeBuilder = new StringBuilder();
        bool isInsideCodeBlock = false;

        void FlushParagraph()
        {
            if (paragraphLines.Count == 0)
            {
                return;
            }

            var paragraph = CreateParagraph(string.Join("\n", paragraphLines));
            document.Blocks.Add(paragraph);
            paragraphLines.Clear();
        }

        void FlushList()
        {
            if (listItems.Count == 0 || currentListOrdered is null)
            {
                return;
            }

            var list = new System.Windows.Documents.List
            {
                MarkerStyle = currentListOrdered.Value ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                Margin = new Thickness(0, 2, 0, 10)
            };

            foreach (var item in listItems)
            {
                list.ListItems.Add(item);
            }

            document.Blocks.Add(list);
            listItems.Clear();
            currentListOrdered = null;
        }

        void FlushCodeBlock()
        {
            if (codeBuilder.Length == 0)
            {
                return;
            }

            var codeText = new TextBlock
            {
                Text = codeBuilder.ToString().TrimEnd('\r', '\n'),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                LineHeight = 20,
                TextWrapping = TextWrapping.Wrap
            };
            codeText.SetResourceReference(TextElement.ForegroundProperty, "TextPrimary");

            var codeBorder = new Border
            {
                Margin = new Thickness(0, 4, 0, 10),
                Padding = new Thickness(10),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Child = codeText
            };
            codeBorder.SetResourceReference(Border.BackgroundProperty, "InputBackground");
            codeBorder.SetResourceReference(Border.BorderBrushProperty, "ControlBorder");
            document.Blocks.Add(new BlockUIContainer(codeBorder));
            codeBuilder.Clear();
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine ?? string.Empty;
            var trimmed = line.Trim();

            if (isInsideCodeBlock)
            {
                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    FlushCodeBlock();
                    isInsideCodeBlock = false;
                }
                else
                {
                    codeBuilder.AppendLine(line);
                }

                continue;
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph();
                FlushList();
                isInsideCodeBlock = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                FlushParagraph();
                FlushList();
                continue;
            }

            if (IsHorizontalRule(trimmed))
            {
                FlushParagraph();
                FlushList();
                document.Blocks.Add(CreateHorizontalRule());
                continue;
            }

            if (TryParseHeading(line, out var headingLevel, out var headingText))
            {
                FlushParagraph();
                FlushList();
                document.Blocks.Add(CreateHeading(headingText, headingLevel));
                continue;
            }

            if (TryParseListItem(line, out var isOrderedList, out var itemText))
            {
                FlushParagraph();
                if (currentListOrdered.HasValue && currentListOrdered.Value != isOrderedList)
                {
                    FlushList();
                }

                currentListOrdered ??= isOrderedList;
                listItems.Add(CreateListItem(itemText));
                continue;
            }

            if (TryParseBlockquote(line, out var quoteText))
            {
                FlushParagraph();
                FlushList();
                document.Blocks.Add(CreateQuote(quoteText));
                continue;
            }

            paragraphLines.Add(line);
        }

        if (isInsideCodeBlock)
        {
            FlushCodeBlock();
        }

        FlushParagraph();
        FlushList();

        return document;
    }

    private static Block CreateHorizontalRule()
    {
        var separator = new Border
        {
            Height = 1,
            Margin = new Thickness(0, 6, 0, 10)
        };
        separator.SetResourceReference(Border.BackgroundProperty, "ControlBorder");
        return new BlockUIContainer(separator);
    }

    private static Paragraph CreateHeading(string text, int level)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, level <= 2 ? 6 : 4, 0, level <= 2 ? 8 : 6),
            FontWeight = FontWeights.SemiBold,
            FontSize = level switch
            {
                1 => 24,
                2 => 21,
                3 => 18,
                4 => 16,
                5 => 15,
                _ => 14
            }
        };
        AddInlines(paragraph.Inlines, text);

        if (level == 1)
        {
            paragraph.BorderThickness = new Thickness(0, 0, 0, 1);
            paragraph.Padding = new Thickness(0, 0, 0, 5);
            paragraph.SetResourceReference(Block.BorderBrushProperty, "ControlBorder");
            paragraph.SetResourceReference(TextElement.ForegroundProperty, "PrimaryRed");
            return paragraph;
        }

        if (level == 2)
        {
            paragraph.BorderThickness = new Thickness(0, 0, 0, 1);
            paragraph.Padding = new Thickness(0, 0, 0, 4);
            paragraph.SetResourceReference(Block.BorderBrushProperty, "ControlBorder");
            paragraph.SetResourceReference(TextElement.ForegroundProperty, "AccentBlueBorder");
            return paragraph;
        }

        paragraph.SetResourceReference(TextElement.ForegroundProperty, "TextPrimary");
        return paragraph;
    }

    private static Paragraph CreateParagraph(string text)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 10)
        };
        AddInlines(paragraph.Inlines, text);
        return paragraph;
    }

    private static Paragraph CreateQuote(string text)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(12, 2, 0, 10),
            Padding = new Thickness(10, 4, 10, 4),
            BorderThickness = new Thickness(3, 0, 0, 0),
            FontStyle = FontStyles.Italic
        };
        AddInlines(paragraph.Inlines, text);
        paragraph.SetResourceReference(TextElement.BackgroundProperty, "SurfaceMuted");
        paragraph.SetResourceReference(Block.BorderBrushProperty, "PrimaryRed");
        paragraph.SetResourceReference(TextElement.ForegroundProperty, "TextPrimary");
        return paragraph;
    }

    private static ListItem CreateListItem(string text)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 2)
        };
        AddInlines(paragraph.Inlines, text);
        return new ListItem(paragraph);
    }

    private static void AddInlines(InlineCollection target, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var index = 0;
        while (index < text.Length)
        {
            if (text[index] == '\n')
            {
                target.Add(new LineBreak());
                index++;
                continue;
            }

            if (TryConsumeCode(text, ref index, out var codeInline))
            {
                target.Add(codeInline);
                continue;
            }

            if (TryConsumeBold(text, ref index, out var boldInline))
            {
                target.Add(boldInline);
                continue;
            }

            if (TryConsumeItalic(text, ref index, out var italicInline))
            {
                target.Add(italicInline);
                continue;
            }

            if (TryConsumeLink(text, ref index, out var linkInline))
            {
                target.Add(linkInline);
                continue;
            }

            var start = index;
            while (index < text.Length &&
                   text[index] != '\n' &&
                   text[index] != '`' &&
                   text[index] != '*' &&
                   text[index] != '[')
            {
                index++;
            }

            var plainText = text[start..index];
            if (!string.IsNullOrEmpty(plainText))
            {
                target.Add(new Run(plainText));
                continue;
            }

            target.Add(new Run(text[index].ToString()));
            index++;
        }
    }

    private static bool TryConsumeCode(string text, ref int index, out Inline inline)
    {
        inline = null!;
        if (index + 1 >= text.Length || text[index] != '`')
        {
            return false;
        }

        var endIndex = text.IndexOf('`', index + 1);
        if (endIndex <= index + 1)
        {
            return false;
        }

        var code = text[(index + 1)..endIndex];
        var run = new Run(code)
        {
            FontFamily = new FontFamily("Consolas")
        };
        run.SetResourceReference(TextElement.BackgroundProperty, "SurfaceMuted");
        run.SetResourceReference(TextElement.ForegroundProperty, "TextPrimary");

        inline = run;
        index = endIndex + 1;
        return true;
    }

    private static bool TryConsumeBold(string text, ref int index, out Inline inline)
    {
        inline = null!;
        if (index + 3 >= text.Length || text[index] != '*' || text[index + 1] != '*')
        {
            return false;
        }

        var endIndex = text.IndexOf("**", index + 2, StringComparison.Ordinal);
        if (endIndex <= index + 2)
        {
            return false;
        }

        var content = text[(index + 2)..endIndex];
        var span = new Span();
        AddInlines(span.Inlines, content);
        inline = new Bold(span);
        index = endIndex + 2;
        return true;
    }

    private static bool TryConsumeItalic(string text, ref int index, out Inline inline)
    {
        inline = null!;
        if (index + 2 >= text.Length || text[index] != '*' || text[index + 1] == '*')
        {
            return false;
        }

        var endIndex = text.IndexOf('*', index + 1);
        if (endIndex <= index + 1)
        {
            return false;
        }

        var content = text[(index + 1)..endIndex];
        var span = new Span();
        AddInlines(span.Inlines, content);
        inline = new Italic(span);
        index = endIndex + 1;
        return true;
    }

    private static bool TryConsumeLink(string text, ref int index, out Inline inline)
    {
        inline = null!;
        if (text[index] != '[')
        {
            return false;
        }

        var closeLabel = text.IndexOf(']', index + 1);
        if (closeLabel <= index + 1 || closeLabel + 2 >= text.Length || text[closeLabel + 1] != '(')
        {
            return false;
        }

        var closeUrl = text.IndexOf(')', closeLabel + 2);
        if (closeUrl <= closeLabel + 2)
        {
            return false;
        }

        var label = text[(index + 1)..closeLabel];
        var url = text[(closeLabel + 2)..closeUrl];

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var hyperlink = new Hyperlink
        {
            NavigateUri = uri
        };
        hyperlink.SetResourceReference(TextElement.ForegroundProperty, "PrimaryRed");
        AddInlines(hyperlink.Inlines, label);

        inline = hyperlink;
        index = closeUrl + 1;
        return true;
    }

    private static bool TryParseHeading(string line, out int level, out string text)
    {
        level = 0;
        text = string.Empty;
        var trimmedStart = line.TrimStart();
        if (trimmedStart.Length < 3 || trimmedStart[0] != '#')
        {
            return false;
        }

        var cursor = 0;
        while (cursor < trimmedStart.Length && trimmedStart[cursor] == '#')
        {
            cursor++;
        }

        if (cursor == 0 || cursor > 6 || cursor >= trimmedStart.Length || trimmedStart[cursor] != ' ')
        {
            return false;
        }

        level = cursor;
        text = trimmedStart[(cursor + 1)..].Trim();
        return text.Length > 0;
    }

    private static bool TryParseListItem(string line, out bool ordered, out string text)
    {
        ordered = false;
        text = string.Empty;

        var trimmed = line.TrimStart();
        if (trimmed.Length < 3)
        {
            return false;
        }

        if ((trimmed[0] == '-' || trimmed[0] == '*' || trimmed[0] == '+') && trimmed[1] == ' ')
        {
            ordered = false;
            text = trimmed[2..].Trim();
            return text.Length > 0;
        }

        var dotIndex = trimmed.IndexOf('.');
        if (dotIndex <= 0 || dotIndex >= 4 || dotIndex + 1 >= trimmed.Length || trimmed[dotIndex + 1] != ' ')
        {
            return false;
        }

        for (var i = 0; i < dotIndex; i++)
        {
            if (!char.IsDigit(trimmed[i]))
            {
                return false;
            }
        }

        ordered = true;
        text = trimmed[(dotIndex + 2)..].Trim();
        return text.Length > 0;
    }

    private static bool TryParseBlockquote(string line, out string text)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("> ", StringComparison.Ordinal))
        {
            text = trimmed[2..].Trim();
            return text.Length > 0;
        }

        text = string.Empty;
        return false;
    }

    private static bool IsHorizontalRule(string text)
    {
        if (text.Length < 3)
        {
            return false;
        }

        var noSpaces = text.Replace(" ", string.Empty);
        return noSpaces == "---" || noSpaces == "***" || noSpaces == "___";
    }
}

