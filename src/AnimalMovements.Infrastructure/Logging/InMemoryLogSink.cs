using Serilog.Core;
using Serilog.Events;

namespace AnimalMovements.Infrastructure.Logging;

public sealed class InMemoryLogSink(InMemoryLogStore store) : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        var category = logEvent.Properties.TryGetValue("SourceContext", out var sourceProp)
            ? sourceProp.ToString().Trim('"')
            : "Application";

        var correlationId = logEvent.Properties.TryGetValue("CorrelationId", out var corrProp)
            ? corrProp.ToString().Trim('"')
            : null;

        store.Add(
            logEvent.Timestamp.ToUniversalTime(),
            logEvent.Level.ToString(),
            category,
            logEvent.RenderMessage(),
            correlationId,
            logEvent.Exception?.GetType().Name);
    }
}
