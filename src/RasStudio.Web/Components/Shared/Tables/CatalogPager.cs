namespace RasStudio.Web.Components.Shared.Tables;

/// <summary>
///     Concatenates ordered sources without re-sorting partial server pages. Only the first
///     page (for counts) and pages intersecting the requested window are fetched.
/// </summary>
internal static class CatalogPager
{
    internal static async Task<CatalogPage<TItem>> LoadAsync<TSource, TItem>(
        IReadOnlyList<TSource> sources,
        int pageNumber,
        int pageSize,
        Func<TSource, int, int, CancellationToken, Task<CatalogSlice<TItem>>> loadPage,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);

        using var concurrency = new SemaphoreSlim(8);
        var firstPages = await Task.WhenAll(sources.Select(async source =>
        {
            await concurrency.WaitAsync(cancellationToken);
            try
            {
                return await loadPage(source, 1, pageSize, cancellationToken);
            }
            finally
            {
                concurrency.Release();
            }
        }));

        var totalCount = firstPages.Sum(page => page.TotalCount);
        var totalPages = totalCount == 0 ? 0 : (totalCount - 1) / pageSize + 1;
        pageNumber = Math.Clamp(pageNumber, 1, Math.Max(1, totalPages));
        var skip = checked((pageNumber - 1) * pageSize);
        var remaining = Math.Min(pageSize, totalCount - skip);
        var items = new List<TItem>(remaining);

        for (var index = 0; index < sources.Count && remaining > 0; index++)
        {
            var first = firstPages[index];
            if (skip >= first.TotalCount)
            {
                skip -= first.TotalCount;
                continue;
            }

            var sourceRemaining = Math.Min(remaining, first.TotalCount - skip);
            while (sourceRemaining > 0)
            {
                var sourcePageNumber = skip / pageSize + 1;
                var sourcePage = sourcePageNumber == 1
                    ? first
                    : await loadPage(sources[index], sourcePageNumber, pageSize, cancellationToken);
                var offset = skip % pageSize;
                var take = Math.Min(sourceRemaining, pageSize - offset);
                // Hub offers offset pages, not a snapshot token. Do not publish a known
                // inconsistent window when a concurrent refresh changes the source count.
                if (sourcePage.TotalCount != first.TotalCount || sourcePage.Items.Count < offset + take)
                    throw new InvalidOperationException("The shadow catalog changed while loading. Reload the list.");

                items.AddRange(sourcePage.Items.Skip(offset).Take(take));
                skip += take;
                sourceRemaining -= take;
                remaining -= take;
            }

            skip = 0;
        }

        return new CatalogPage<TItem>(items, totalCount, pageNumber, totalPages);
    }
}

internal sealed record CatalogSlice<T>(IReadOnlyList<T> Items, int TotalCount);

internal sealed record CatalogPage<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int TotalPages);
