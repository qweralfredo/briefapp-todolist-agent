using System.Threading.Channels;

namespace BriefappTodoList.Api.Services;

/// <summary>
/// Singleton broadcast service for real-time metrics events (BL-13 SP-10).
/// Uses a Channel per subscriber; publish fans out to all active readers.
/// </summary>
public sealed class MetricsEventService
{
    private readonly object _lock = new();
    private readonly List<Channel<MetricsEvent>> _subscribers = [];

    public ChannelReader<MetricsEvent> Subscribe()
    {
        var ch = Channel.CreateBounded<MetricsEvent>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = true
        });
        lock (_lock) _subscribers.Add(ch);
        return ch.Reader;
    }

    public void Unsubscribe(ChannelReader<MetricsEvent> reader)
    {
        lock (_lock)
            _subscribers.RemoveAll(ch => ch.Reader == reader);
    }

    public void Publish(MetricsEvent evt)
    {
        List<Channel<MetricsEvent>> snapshot;
        lock (_lock) snapshot = [.._subscribers];

        foreach (var ch in snapshot)
            ch.Writer.TryWrite(evt);
    }
}

/// <summary>Single metrics event broadcast to SSE subscribers.</summary>
public sealed record MetricsEvent(string EventType, object Data);
