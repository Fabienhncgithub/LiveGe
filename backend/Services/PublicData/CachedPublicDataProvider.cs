using FrontiereLiveGe.Api.Dtos;

namespace FrontiereLiveGe.Api.Services.PublicData;

public abstract class CachedPublicDataProvider : IPublicDataProvider
{
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private PublicDataSnapshot? _cachedResult;
    private PublicDataSnapshot? _lastSuccess;
    private DateTime _lastSuccessAtUtc;
    private DateTime _expiresAtUtc;

    protected abstract TimeSpan CacheDuration { get; }
    protected abstract TimeSpan MaximumStaleAge { get; }
    protected abstract string SourceId { get; }
    protected abstract string SourceName { get; }
    protected abstract string Coverage { get; }
    protected abstract string Attribution { get; }
    protected abstract string SourceUrl { get; }
    protected abstract ILogger Logger { get; }

    public async Task<PublicDataSnapshot> GetSnapshotAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (_cachedResult is not null && now < _expiresAtUtc)
        {
            return _cachedResult;
        }

        await _refreshGate.WaitAsync(ct);
        try
        {
            now = DateTime.UtcNow;
            if (_cachedResult is not null && now < _expiresAtUtc)
            {
                return _cachedResult;
            }

            try
            {
                var fresh = await FetchFreshAsync(now, ct);
                _lastSuccess = fresh;
                _cachedResult = fresh;
                _lastSuccessAtUtc = now;
                _expiresAtUtc = now.Add(CacheDuration);
                return fresh;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Public data source {SourceId} is unavailable.", SourceId);
                _expiresAtUtc = now.AddMinutes(2);

                if (_lastSuccess is not null
                    && now - _lastSuccessAtUtc <= MaximumStaleAge)
                {
                    _cachedResult = new PublicDataSnapshot
                    {
                        Source = new DataSourceStatusDto
                        {
                            Id = SourceId,
                            Name = SourceName,
                            Status = "Stale",
                            IsOfficial = true,
                            HasBillingRisk = false,
                            RecordsCount = _lastSuccess.Source.RecordsCount,
                            RelevantSignalsCount = _lastSuccess.Signals.Count,
                            CheckedAtUtc = now,
                            DataTimestampUtc = _lastSuccess.Source.DataTimestampUtc,
                            Coverage = Coverage,
                            Attribution = Attribution,
                            SourceUrl = SourceUrl,
                            Message =
                                $"La source ne répond pas. Dernier résultat connu conservé ({Math.Max(1, (int)Math.Ceiling((now - _lastSuccessAtUtc).TotalMinutes))} min)."
                        },
                        Signals = _lastSuccess.Signals
                    };
                    return _cachedResult;
                }

                _cachedResult = new PublicDataSnapshot
                {
                    Source = new DataSourceStatusDto
                    {
                        Id = SourceId,
                        Name = SourceName,
                        Status = "Unavailable",
                        IsOfficial = true,
                        HasBillingRisk = false,
                        CheckedAtUtc = now,
                        Coverage = Coverage,
                        Attribution = Attribution,
                        SourceUrl = SourceUrl,
                        Message = "Source temporairement indisponible."
                    }
                };
                return _cachedResult;
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    protected abstract Task<PublicDataSnapshot> FetchFreshAsync(DateTime checkedAtUtc, CancellationToken ct);

    protected DataSourceStatusDto OnlineStatus(
        DateTime checkedAtUtc,
        int recordsCount,
        int relevantSignalsCount,
        DateTime? dataTimestampUtc,
        string? message = null) =>
        new()
        {
            Id = SourceId,
            Name = SourceName,
            Status = "Online",
            IsOfficial = true,
            HasBillingRisk = false,
            RecordsCount = recordsCount,
            RelevantSignalsCount = relevantSignalsCount,
            CheckedAtUtc = checkedAtUtc,
            DataTimestampUtc = dataTimestampUtc,
            Coverage = Coverage,
            Attribution = Attribution,
            SourceUrl = SourceUrl,
            Message = message
        };
}
