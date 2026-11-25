using ClaimGuard.Domain.Events;

namespace ClaimGuard.Application.Interfaces;

/// <summary>Event streaming backbone (Kafka). Every state transition is appended to an immutable topic.</summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(string topic, TEvent evt, CancellationToken ct = default) where TEvent : class;
}
