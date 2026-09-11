using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RasStudio.Web.Components;
using RasStudio.Web.IntegrationTests.Infrastructure;

namespace RasStudio.Web.IntegrationTests;

public sealed class ResourceTableFooterTests(
    RasStudioWebApplicationFactory factory) : IClassFixture<RasStudioWebApplicationFactory>
{
    [Theory]
    [InlineData(0, 1, 10, 0, false, "0-0 of 0", true, true)]
    [InlineData(5, 1, 10, 1, false, "1-5 of 5", true, true)]
    [InlineData(25, 1, 10, 3, false, "1-10 of 25", true, false)]
    [InlineData(25, 2, 10, 3, false, "11-20 of 25", false, false)]
    [InlineData(25, 3, 10, 3, false, "21-25 of 25", false, true)]
    [InlineData(25, 2, 10, 3, true, "11-20 of 25", true, true)]
    public async Task Footer_displays_range_and_disables_unavailable_navigation(
        int totalCount,
        int pageNumber,
        int pageSize,
        int totalPages,
        bool disabled,
        string expectedRange,
        bool previousDisabled,
        bool nextDisabled)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(
            scope.ServiceProvider,
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<ResourceTableFooter>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(ResourceTableFooter.TotalCount)] = totalCount,
                    [nameof(ResourceTableFooter.PageNumber)] = pageNumber,
                    [nameof(ResourceTableFooter.PageSize)] = pageSize,
                    [nameof(ResourceTableFooter.TotalPages)] = totalPages,
                    [nameof(ResourceTableFooter.Disabled)] = disabled
                }));
            return component.ToHtmlString();
        });

        Assert.Contains(expectedRange, html, StringComparison.Ordinal);
        Assert.Contains("Rows per page:", html, StringComparison.Ordinal);
        AssertButtonDisabled(html, "First page", previousDisabled);
        AssertButtonDisabled(html, "Previous page", previousDisabled);
        AssertButtonDisabled(html, "Next page", nextDisabled);
        AssertButtonDisabled(html, "Last page", nextDisabled);
    }

    private static void AssertButtonDisabled(string html, string label, bool expected)
    {
        var button = Regex.Match(
            html,
            $"""<button\b[^>]*aria-label="{Regex.Escape(label)}"[^>]*>""");
        Assert.True(button.Success, $"Button '{label}' was not rendered.");
        Assert.Equal(expected, button.Value.Contains(" disabled", StringComparison.Ordinal));
    }
}
