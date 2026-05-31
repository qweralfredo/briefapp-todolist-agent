using BriefappTodoList.Api.Data;
using BriefappTodoList.Api.Domain.Queue;

namespace BriefappTodoList.Api.Services.Queue;

/// <summary>
/// ST-16: Publishes tasks to Tansu topics and persists TaskMessageEntity in PostgreSQL.
/// Topics follow the convention: briefapp.{boxId}.tasks
/// </summary>
public sealed class TansuPublisherService
{
    private readonly ITansuClient _tansu;
    private readonly AppDbContext _db;
    private readonly ILogger<TansuPublisherService> _logger;

    public TansuPublisherService(
        ITansuClient tansu,
        AppDbContext db,
        ILogger<TansuPublisherService> logger)
    {
        _tansu  = tansu;
        _db     = db;
        _logger = logger;
    }

    /// <summary>
    /// ST-16: Publish a task payload to the box queue.
    /// Persists record in DB for audit/retry, then publishes to Tansu.
    /// </summary>
    public async Task<TaskMessageEntity> PublishTaskAsync(
        Guid        boxId,
        object      payload,
        string      source        = "api",
        string?     workItemId    = null,
        int         maxRetries    = 3,
        string?     topicOverride = null,
        TimeSpan?   delay         = null,
        CancellationToken ct      = default)
    {
        var topic = topicOverride ?? TopicFor(boxId);

        var entity = new TaskMessageEntity
        {
            BoxId      = boxId,
            Topic      = topic,
            Payload    = System.Text.Json.JsonSerializer.Serialize(payload),
            Status     = TaskMessageStatus.Pending,
            Source     = source,
            WorkItemId = workItemId,
            MaxRetries = maxRetries,
            ScheduledAt = delay.HasValue ? DateTimeOffset.UtcNow + delay.Value : null,
        };

        _db.TaskMessages.Add(entity);
        await _db.SaveChangesAsync(ct);

        try
        {
            var result = await _tansu.PublishAsync(topic, payload, delay, ct);
            _logger.LogInformation(
                "Task {TaskId} published to {Topic} (messageId={MsgId})",
                entity.Id, topic, result.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tansu publish failed for task {TaskId}. Marked as Failed.", entity.Id);
            entity.Status = TaskMessageStatus.Failed;
            await _db.SaveChangesAsync(ct);
        }

        return entity;
    }

    /// <summary>ST-16: Requeue a failed task (used by retry engine or DLQ drain).</summary>
    public async Task<TaskMessageEntity> RequeueAsync(
        TaskMessageEntity task,
        TimeSpan?         delay = null,
        CancellationToken ct    = default)
    {
        task.Status     = TaskMessageStatus.Requeued;
        task.RetryCount++;
        task.ScheduledAt = delay.HasValue ? DateTimeOffset.UtcNow + delay.Value : null;
        await _db.SaveChangesAsync(ct);

        await _tansu.PublishAsync(task.Topic, System.Text.Json.JsonSerializer.Deserialize<object>(task.Payload)!, delay, ct);

        _logger.LogInformation(
            "Task {TaskId} requeued (attempt {Retry}/{Max})",
            task.Id, task.RetryCount, task.MaxRetries);
        return task;
    }

    /// <summary>Derives Tansu topic name from Box ID.</summary>
    public static string TopicFor(Guid boxId) => $"briefapp.{boxId:N}.tasks";

    /// <summary>ST-21: Get queue status across all or one box.</summary>
    public async Task<TansuTopicStats[]> GetQueueStatusAsync(Guid? boxId = null, CancellationToken ct = default)
    {
        var prefix = boxId.HasValue ? $"briefapp.{boxId:N}" : "briefapp.";
        return await _tansu.GetTopicStatsAsync(prefix, ct);
    }
}
