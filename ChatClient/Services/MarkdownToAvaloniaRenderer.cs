using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ChatClient.Services;

/// <summary>
/// Renders Markdown content using Markdig parser to native Avalonia controls.
/// This provides full control over rendering appearance without external library constraints.
/// </summary>
public class MarkdownToAvaloniaRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions() // Enables tables, task lists, etc.
        .Build();

    // Static color brushes to avoid dynamic resource lookups during initialization
    private static readonly SolidColorBrush HeadingBrush = new(Color.Parse("#E0E0E0"));
    private static readonly SolidColorBrush NormalTextBrush = new(Color.Parse("#CCCCCC"));
    private static readonly SolidColorBrush CodeBackgroundBrush = new(Color.Parse("#2D2D2D"));
    private static readonly SolidColorBrush CodeForegroundBrush = new(Color.Parse("#D4D4D4"));
    private static readonly SolidColorBrush QuoteBackgroundBrush = new(Color.Parse("#3A3A3A"));
    private static readonly SolidColorBrush QuoteBorderBrush = new(Color.Parse("#606060"));
    private static readonly SolidColorBrush LinkBrush = new(Color.Parse("#569CD6"));

    /// <summary>
    /// Renders markdown text to an Avalonia Control that can be displayed in the UI.
    /// </summary>
    /// <param name="markdownText">The markdown text to render.</param>
    /// <returns>A Control containing the rendered markdown content.</returns>
    public Control RenderToControl(string markdownText)
    {
        if (string.IsNullOrWhiteSpace(markdownText))
        {
            return new TextBlock { Text = "", Foreground = NormalTextBrush };
        }

        var document = Markdown.Parse(markdownText, Pipeline);
        var container = new StackPanel { Spacing = 8 };

        foreach (var block in document)
        {
            var control = RenderBlock(block);
            if (control != null)
            {
                container.Children.Add(control);
            }
        }

        return container;
    }

    private Control? RenderBlock(Block block)
    {
        return block switch
        {
            ParagraphBlock paragraph => RenderParagraph(paragraph),
            HeadingBlock heading => RenderHeading(heading),
            CodeBlock codeBlock => RenderCodeBlock(codeBlock),
            ListBlock list => RenderList(list),
            QuoteBlock quote => RenderQuote(quote),
            ThematicBreakBlock => RenderThematicBreak(),
            _ => null
        };
    }

    private Control RenderParagraph(ParagraphBlock paragraph)
    {
        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = NormalTextBrush
        };

        RenderInlines(paragraph.Inline, textBlock.Inlines);

        return textBlock;
    }

    private Control RenderHeading(HeadingBlock heading)
    {
        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeight.Bold,
            Foreground = HeadingBrush
        };

        // Set font size based on heading level
        textBlock.FontSize = heading.Level switch
        {
            1 => 24,
            2 => 20,
            3 => 16,
            4 => 14,
            5 => 12,
            _ => 11
        };

        RenderInlines(heading.Inline, textBlock.Inlines);

        return textBlock;
    }

    private Control RenderCodeBlock(CodeBlock codeBlock)
    {
        var codeText = string.Join("\n", codeBlock.Lines.Lines.Select(l => l.ToString()));

        var textBlock = new TextBlock
        {
            Text = codeText,
            FontFamily = new FontFamily("Consolas,Menlo,Monaco,Courier New,monospace"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = CodeForegroundBrush,
            Padding = new Thickness(8)
        };

        var border = new Border
        {
            Child = textBlock,
            Background = CodeBackgroundBrush,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4)
        };

        return border;
    }

    private Control RenderList(ListBlock list)
    {
        var stackPanel = new StackPanel { Spacing = 4 };
        int itemIndex = list.IsOrdered ? (string.IsNullOrEmpty(list.OrderedStart) ? 1 : int.Parse(list.OrderedStart)) : 0;

        foreach (var item in list.OfType<ListItemBlock>())
        {
            var itemPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };

            // Add bullet or number
            var prefix = list.IsOrdered ? $"{itemIndex}." : "•";
            itemPanel.Children.Add(new TextBlock
            {
                Text = prefix,
                VerticalAlignment = VerticalAlignment.Top,
                Foreground = NormalTextBrush,
                Width = 20
            });

            // Render item content
            var itemContent = new StackPanel { Spacing = 4 };
            foreach (var block in item)
            {
                var control = RenderBlock(block);
                if (control != null)
                {
                    itemContent.Children.Add(control);
                }
            }

            itemPanel.Children.Add(itemContent);
            stackPanel.Children.Add(itemPanel);

            if (list.IsOrdered)
            {
                itemIndex++;
            }
        }

        return stackPanel;
    }

    private Control RenderQuote(QuoteBlock quote)
    {
        var stackPanel = new StackPanel { Spacing = 4 };

        foreach (var block in quote)
        {
            var control = RenderBlock(block);
            if (control != null)
            {
                stackPanel.Children.Add(control);
            }
        }

        var border = new Border
        {
            Child = stackPanel,
            Background = QuoteBackgroundBrush,
            BorderBrush = QuoteBorderBrush,
            BorderThickness = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(12, 8),
            CornerRadius = new CornerRadius(4)
        };

        return border;
    }

    private Control RenderThematicBreak()
    {
        return new Border
        {
            Height = 1,
            Background = QuoteBorderBrush,
            Margin = new Thickness(0, 8)
        };
    }

    private void RenderInlines(ContainerInline? inline, InlineCollection? inlines)
    {
        if (inline == null || inlines == null) return;

        foreach (var child in inline)
        {
            switch (child)
            {
                case LiteralInline literal:
                    inlines.Add(new Run(literal.Content.ToString()) { Foreground = NormalTextBrush });
                    break;

                case LineBreakInline:
                    inlines.Add(new LineBreak());
                    break;

                case EmphasisInline emphasis:
                    var span = new Span();
                    if (emphasis.DelimiterCount == 2) // Bold
                    {
                        span.FontWeight = FontWeight.Bold;
                    }
                    else // Italic
                    {
                        span.FontStyle = FontStyle.Italic;
                    }
                    RenderInlines(emphasis, span.Inlines);
                    inlines.Add(span);
                    break;

                case CodeInline code:
                    var codeSpan = new Span
                    {
                        FontFamily = new FontFamily("Consolas,Menlo,Monaco,Courier New,monospace"),
                        Background = CodeBackgroundBrush,
                        Foreground = CodeForegroundBrush
                    };
                    codeSpan.Inlines.Add(new Run(code.Content) { Foreground = CodeForegroundBrush });
                    inlines.Add(codeSpan);
                    break;

                case LinkInline link:
                    var linkSpan = new Span
                    {
                        Foreground = LinkBrush,
                        TextDecorations = TextDecorations.Underline
                    };
                    RenderInlines(link, linkSpan.Inlines);
                    inlines.Add(linkSpan);
                    break;

                case ContainerInline container:
                    RenderInlines(container, inlines);
                    break;
            }
        }
    }
}
