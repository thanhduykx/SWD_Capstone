using System.Collections.Concurrent;
using System.Threading.Channels;
using CPMS.BLL.Reviews.Publication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CPMS.DAL.ExternalServices;

public sealed class ReviewPublicationDispatchScheduler : IReviewPublicationDispatchScheduler
{
    private readonly Channel<int> channel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });
    private readonly ConcurrentDictionary<int, byte> scheduledPublicationIds = new();

    public async ValueTask EnqueueAsync(int publicationId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(publicationId);
        if (!scheduledPublicationIds.TryAdd(publicationId, 0)) return;
        try
        {
            await channel.Writer.WriteAsync(publicationId, cancellationToken);
        }
        catch
        {
            scheduledPublicationIds.TryRemove(publicationId, out _);
            throw;
        }
    }

    public IAsyncEnumerable<int> ReadAllAsync(CancellationToken cancellationToken) =>
        channel.Reader.ReadAllAsync(cancellationToken);

    public void MarkCompleted(int publicationId) => scheduledPublicationIds.TryRemove(publicationId, out _);
}

public sealed class ReviewPublicationEmailWorker(
    ReviewPublicationDispatchScheduler queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ReviewPublicationEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await QueuePendingPublicationsAsync(stoppingToken);

        await foreach (int publicationId in queue.ReadAllAsync(stoppingToken))
        {
            var retry = false;
            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IReviewPublicationStore>();
                (int sent, int failed) = await store.DispatchEmailsAsync(publicationId, stoppingToken);
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation(
                        "Background review schedule email dispatch completed for publication {PublicationId}: {Sent} sent, {Failed} failed.",
                        publicationId,
                        sent,
                        failed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                retry = true;
                logger.LogError(exception,
                    "Background review schedule email dispatch failed for publication {PublicationId}; it will be retried.",
                    publicationId);
            }
            finally
            {
                queue.MarkCompleted(publicationId);
            }

            if (retry && !stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                await queue.EnqueueAsync(publicationId, stoppingToken);
            }
        }
    }

    private async Task QueuePendingPublicationsAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IReviewPublicationStore>();
        IReadOnlyList<int> pendingPublicationIds = await store.GetPendingPublicationIdsAsync(cancellationToken);
        foreach (int publicationId in pendingPublicationIds)
        {
            await queue.EnqueueAsync(publicationId, cancellationToken);
        }
    }
}
