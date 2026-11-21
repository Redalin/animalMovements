using System.Collections.Concurrent;

namespace AnimalMovements.Infrastructure.Logging;

public sealed class InMemoryLogStore
{
    private const int MaxEntries = 500;
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private long _sequence;

    public void Add(
        DateTimeOffset timestampUtc,
        string level,
        string category,
        string message,
        string? correlationId,
        string? exception)
    {
        var seq = Interlocked.Increment(ref _sequence);
        _entries.Enqueue(new LogEntry(seq, timestampUtc, level, category, message, correlationId, exception));

        while (_entries.Count > MaxEntries && _entries.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<LogEntry> Query(string? minLevel = null, string? search = null, int take = 200)
    {
        var minRank = GetLevelRank(minLevel);
        var clampedTake = Math.Clamp(take, 1, MaxEntries);

        var query = _entries.Where(e => GetLevelRank(e.Level) >= minRank);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e =>
                e.Message.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.Category.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (e.CorrelationId?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return query
            .OrderByDescending(e => e.Sequence)
            .Take(clampedTake)
            .ToList();
    }

    public void Clear()
    {
        while (_entries.TryDequeue(out _))
        {
        }
    }

    private static int GetLevelRank(string? level) => level?.ToLowerInvariant() switch
    {
        "verbose" or "trace" => 0,
        "debug" => 1,
        "information" or "info" => 2,
        "warning" or "warn" => 3,
        "error" => 4,
        "fatal" or "critical" => 5,
        _ => 0
    };
}
