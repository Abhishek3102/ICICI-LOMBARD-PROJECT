namespace ClaimGuard.Application.Interfaces;

/// <summary>Reliable point-to-point work queue (RabbitMQ) with dead-letter exchange for retries.</summary>
public interface ITaskQueue
{
    Task EnqueueNotificationAsync(NotificationTask task, CancellationToken ct = default);
}
