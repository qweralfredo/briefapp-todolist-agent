using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BriefappTodoList.Api.Domain;

public enum ProjectStatus
{
    Active = 0,
    Archived = 1
}

public enum BacklogItemStatus
{
    New = 0,
    Planned = 1,
    InSprint = 2,
    Done = 3,
    Blocked = 4
}

[JsonConverter(typeof(WorkItemStatusConverter))]
public enum WorkItemStatus
{
    Todo = 0,
    InProgress = 1,
    Review = 2,
    Done = 3,
    Blocked = 4
}

public enum SprintStatus
{
    Planned = 0,
    Active = 1,
    Closed = 2
}

public class WorkItemStatusConverter : JsonConverter<WorkItemStatus>
{
    public override WorkItemStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            int val = reader.GetInt32();
            if (Enum.IsDefined(typeof(WorkItemStatus), val))
            {
                return (WorkItemStatus)val;
            }
            throw new JsonException($"Invalid integer value {val} for WorkItemStatus");
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            string? valStr = reader.GetString();
            if (string.IsNullOrEmpty(valStr))
            {
                return WorkItemStatus.Todo;
            }

            // Normalize: remove underscores, hyphens, and convert to lowercase
            string normalized = valStr.Replace("_", "").Replace("-", "").ToLowerInvariant();

            if (normalized == "todo") return WorkItemStatus.Todo;
            if (normalized == "inprogress" || normalized == "in_progress") return WorkItemStatus.InProgress;
            if (normalized == "review") return WorkItemStatus.Review;
            if (normalized == "done") return WorkItemStatus.Done;
            if (normalized == "blocked") return WorkItemStatus.Blocked;

            // Fallback to Enum.TryParse (case-insensitive)
            if (Enum.TryParse<WorkItemStatus>(valStr, true, out var result))
            {
                return result;
            }

            // Also handle if the string contains a number
            if (int.TryParse(valStr, out var intVal) && Enum.IsDefined(typeof(WorkItemStatus), intVal))
            {
                return (WorkItemStatus)intVal;
            }
        }

        throw new JsonException($"Unable to parse WorkItemStatus from JSON token type {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, WorkItemStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString().ToLowerInvariant());
    }
}

