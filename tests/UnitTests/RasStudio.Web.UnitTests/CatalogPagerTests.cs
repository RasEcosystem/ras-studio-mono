using System.Collections.Concurrent;
using RasStudio.Web.Components.Shared.Tables;

namespace RasStudio.Web.UnitTests;

public sealed class CatalogPagerTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task PagesPreserveServerOrderWithoutMissingOrDuplicatingRows(int pageSize)
    {
        // A valid server collation need not match OrdinalIgnoreCase on the client.
        string[][] sources =
        [
            [],
            ["B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "a"],
            [],
            Enumerable.Range(0, 127).Select(index => $"second:{index}").ToArray(),
            ["Ё", "Е", "А", "Z"]
        ];
        var expected = sources.SelectMany(source => source).ToArray();
        var actual = new List<string>();

        for (var number = 1; number <= (expected.Length - 1) / pageSize + 1; number++)
        {
            var page = await CatalogPager.LoadAsync<string[], string>(
                sources,
                number,
                pageSize,
                LoadPageAsync,
                TestContext.Current.CancellationToken);
            Assert.Equal(expected.Length, page.TotalCount);
            Assert.Equal(number, page.Page);
            Assert.Equal(expected.Skip((number - 1) * pageSize).Take(pageSize), page.Items);
            actual.AddRange(page.Items);
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task DeepPageFetchesOnlyCountsAndIntersectingSourcePages()
    {
        var calls = new ConcurrentBag<(int Source, int Page)>();
        var page = await CatalogPager.LoadAsync(
            [0, 1],
            900,
            10,
            (source, number, size, _) =>
            {
                calls.Add((source, number));
                return Task.FromResult(new CatalogSlice<int>(
                    Enumerable.Range((number - 1) * size, size).ToArray(),
                    10000));
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(Enumerable.Range(8990, 10), page.Items);
        Assert.Equal(20000, page.TotalCount);
        Assert.Equal([(0, 1), (0, 900), (1, 1)], calls.OrderBy(call => call.Source).ThenBy(call => call.Page));
    }

    [Fact]
    public async Task RemovedLastPageIsClampedBeforeLoadingItsWindow()
    {
        var page = await CatalogPager.LoadAsync<string[], string>(
            [["B", "a"], ["C"]],
            12,
            2,
            LoadPageAsync,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(["C"], page.Items);
    }

    [Fact]
    public async Task EmptyCatalogResetsToFirstPage()
    {
        var page = await CatalogPager.LoadAsync<string[], string>(
            [[], []],
            12,
            10,
            LoadPageAsync,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, page.Page);
        Assert.Equal(0, page.TotalPages);
        Assert.Equal(0, page.TotalCount);
        Assert.Empty(page.Items);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChangedCountOrIncompleteSourcePageDoesNotPublishPartialResults(bool changedCount)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => CatalogPager.LoadAsync(
            [0],
            2,
            10,
            (_, number, _, _) => Task.FromResult(new CatalogSlice<int>(
                Enumerable.Range(0, number == 1 ? 10 : 9).ToArray(),
                number == 1 || !changedCount ? 20 : 19)),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FailedSourceDoesNotBecomeAnEmptySuccessfulCatalog()
    {
        await Assert.ThrowsAsync<IOException>(() => CatalogPager.LoadAsync(
            [0, 1],
            1,
            10,
            (source, _, _, _) => source == 1
                ? Task.FromException<CatalogSlice<int>>(new IOException("Unavailable"))
                : Task.FromResult(new CatalogSlice<int>([42], 1)),
            TestContext.Current.CancellationToken));
    }

    private static Task<CatalogSlice<string>> LoadPageAsync(
        string[] source,
        int number,
        int size,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new CatalogSlice<string>(
            source.Skip((number - 1) * size).Take(size).ToArray(),
            source.Length));
    }
}
