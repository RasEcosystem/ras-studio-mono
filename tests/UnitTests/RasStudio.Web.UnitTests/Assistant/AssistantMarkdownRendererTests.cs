using RasStudio.Web.Infrastructure.Assistant;

namespace RasStudio.Web.UnitTests.Assistant;

public sealed class AssistantMarkdownRendererTests
{
    private readonly AssistantMarkdownRenderer _renderer = new();

    [Fact]
    public void ToSafeHtmlRendersCommonMarkdownElements()
    {
        var html = _renderer.ToSafeHtml("# Title\n\n- one\n- two\n\n`value`");

        Assert.Contains("<h1", html, StringComparison.Ordinal);
        Assert.Contains("<ul>", html, StringComparison.Ordinal);
        Assert.Contains("<code>value</code>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ToSafeHtmlRemovesExecutableMarkupAndUnsafeLinks()
    {
        var html = _renderer.ToSafeHtml(
            "<script>alert('xss')</script>\n\n[unsafe](javascript:alert('xss'))");

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToSafeHtmlKeepsHttpsLinks()
    {
        var html = _renderer.ToSafeHtml("[documentation](https://example.test/docs)");

        Assert.Contains("href=\"https://example.test/docs\"", html, StringComparison.Ordinal);
    }
}
