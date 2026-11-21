using AnimalMovements.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AnimalMovements.Infrastructure.Persistence.Outbox;

public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private const int BatchSize = 20;
    private const int MaxRetries = 5;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxProcessor started (poll interval: {IntervalSeconds}s).", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while polling OutboxMessages.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("OutboxProcessor stopped.");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AnimalMovementsDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

        var pendingMessages = await dbContext.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Pending)
            .OrderBy(m => m.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return;
        }

        logger.LogDebug("OutboxProcessor picked up {Count} pending message(s).", pendingMessages.Count);

        foreach (var message in pendingMessages)
        {
            try
            {
                await publisher.PublishAsync(
                    message.Id,
                    message.EventType,
                    message.PayloadJson,
                    cancellationToken);

                message.Status = OutboxStatus.Processed;
                message.ProcessedAtUtc = DateTimeOffset.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;

                if (message.RetryCount >= MaxRetries)
                {
                    message.Status = OutboxStatus.DeadLettered;
                    logger.LogError(
                        ex,
                        "Outbox message {MessageId} ({EventType}) moved to DeadLettered after {Retries} retries.",
                        message.Id,
                        message.EventType,
                        message.RetryCount);
                }
                else
                {
                    logger.LogWarning(
                        ex,
                        "Outbox message {MessageId} ({EventType}) failed (attempt {Attempt}/{MaxRetries}).",
                        message.Id,
                        message.EventType,
                        message.RetryCount,
                        MaxRetries);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
