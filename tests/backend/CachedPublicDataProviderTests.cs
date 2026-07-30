using FrontiereLiveGe.Api.Dtos;
using FrontiereLiveGe.Api.Services.PublicData;
using Microsoft.Extensions.Logging.Abstractions;

namespace FrontiereLiveGe.Api.Tests;

public sealed class CachedPublicDataProviderTests
{
    [Fact]
    public async Task GetSnapshotAsync_CachesColdFailureToProtectUpstream()
    {
        var provider = new AlwaysFailingProvider();

        var first = await provider.GetSnapshotAsync(CancellationToken.None);
        var second = await provider.GetSnapshotAsync(CancellationToken.None);

        Assert.Equal("Unavailable", first.Source.Status);
        Assert.Equal("Unavailable", second.Source.Status);
        Assert.Equal(1, provider.Attempts);
    }

    [Fact]
    public async Task GetSnapshotAsync_LabelsFallbackAsStaleAfterRefreshFailure()
    {
        var provider = new SuccessThenFailureProvider();

        var fresh = await provider.GetSnapshotAsync(CancellationToken.None);
        var fallback = await provider.GetSnapshotAsync(CancellationToken.None);
        var cachedFallback = await provider.GetSnapshotAsync(CancellationToken.None);

        Assert.Equal("Online", fresh.Source.Status);
        Assert.Equal("Stale", fallback.Source.Status);
        Assert.Equal("Stale", cachedFallback.Source.Status);
        Assert.Equal(2, provider.Attempts);
    }

    private sealed class AlwaysFailingProvider : CachedPublicDataProvider
    {
        public int Attempts { get; private set; }

        protected override TimeSpan CacheDuration => TimeSpan.FromMinutes(10);
        protected override TimeSpan MaximumStaleAge => TimeSpan.FromHours(1);
        protected override string SourceId => "test";
        protected override string SourceName => "Test";
        protected override string Coverage => "Test";
        protected override string Attribution => "Test";
        protected override string SourceUrl => "https://example.test";
        protected override Microsoft.Extensions.Logging.ILogger Logger =>
            NullLogger<AlwaysFailingProvider>.Instance;

        protected override Task<PublicDataSnapshot> FetchFreshAsync(
            DateTime checkedAtUtc,
            CancellationToken ct)
        {
            Attempts++;
            throw new HttpRequestException("Unavailable");
        }
    }

    private sealed class SuccessThenFailureProvider : CachedPublicDataProvider
    {
        public int Attempts { get; private set; }

        protected override TimeSpan CacheDuration => TimeSpan.Zero;
        protected override TimeSpan MaximumStaleAge => TimeSpan.FromHours(1);
        protected override string SourceId => "test";
        protected override string SourceName => "Test";
        protected override string Coverage => "Test";
        protected override string Attribution => "Test";
        protected override string SourceUrl => "https://example.test";
        protected override Microsoft.Extensions.Logging.ILogger Logger =>
            NullLogger<SuccessThenFailureProvider>.Instance;

        protected override Task<PublicDataSnapshot> FetchFreshAsync(
            DateTime checkedAtUtc,
            CancellationToken ct)
        {
            Attempts++;
            if (Attempts > 1)
            {
                throw new HttpRequestException("Unavailable");
            }

            return Task.FromResult(new PublicDataSnapshot
            {
                Source = OnlineStatus(checkedAtUtc, 1, 1, checkedAtUtc),
                Signals =
                [
                    new RoadSignalDto
                    {
                        Id = "signal",
                        SourceId = "test",
                        SourceName = "Test"
                    }
                ]
            });
        }
    }
}
