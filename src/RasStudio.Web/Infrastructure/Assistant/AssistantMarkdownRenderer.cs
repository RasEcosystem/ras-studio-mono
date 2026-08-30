using Ganss.Xss;
using Markdig;

namespace RasStudio.Web.Infrastructure.Assistant;

public sealed class AssistantMarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseSoftlineBreakAsHardlineBreak()
        .DisableHtml()
        .Build();

    private readonly Lock _renderLock = new();
    private readonly HtmlSanitizer _sanitizer = CreateSanitizer();

    public string ToSafeHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return string.Empty;

        lock (_renderLock)
        {
            var html = Markdown.ToHtml(markdown, Pipeline);
            return _sanitizer.Sanitize(html);
        }
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();

        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith([
            "a", "blockquote", "br", "code", "del", "em", "h1", "h2", "h3", "h4", "h5", "h6",
            "hr", "input", "kbd", "li", "mark", "ol", "p", "pre", "s", "strong", "sub", "sup", "table",
            "tbody", "td", "th", "thead", "tr", "ul"
        ]);

        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith([
            "checked", "disabled", "href", "title", "type"
        ]);

        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(["http", "https"]);

        return sanitizer;
    }
}
