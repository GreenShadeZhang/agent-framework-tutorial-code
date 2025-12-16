using System.Text.Json;

namespace WorkflowDesigner.Api.Models;

/// <summary>
/// 工作流 DTO - 用于 API 返回，避免序列化问题
/// </summary>
public class WorkflowDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<WorkflowNodeDto> Nodes { get; set; } = new();
    public List<WorkflowEdgeDto> Edges { get; set; } = new();
    public List<ParameterDefinition> Parameters { get; set; } = new();
    public string YamlContent { get; set; } = string.Empty;
    public string WorkflowDump { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public static WorkflowDto FromEntity(WorkflowDefinition entity, bool includeFullData = true)
    {
        return new WorkflowDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Version = entity.Version,
            Nodes = entity.Nodes.Select(WorkflowNodeDto.FromEntity).ToList(),
            Edges = entity.Edges.Select(WorkflowEdgeDto.FromEntity).ToList(),
            Parameters = entity.Parameters,
            YamlContent = includeFullData ? entity.YamlContent : string.Empty,
            WorkflowDump = includeFullData ? entity.WorkflowDump : string.Empty,
            Metadata = ConvertToStringDictionary(entity.Metadata),
            IsPublished = entity.IsPublished,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static Dictionary<string, string> ConvertToStringDictionary(Dictionary<string, object> source)
    {
        var result = new Dictionary<string, string>();
        foreach (var kvp in source)
        {
            if (kvp.Value == null)
                continue;

            if (kvp.Value is JsonElement element)
            {
                result[kvp.Key] = ConvertJsonElementToString(element);
            }
            else
            {
                result[kvp.Key] = kvp.Value.ToString() ?? string.Empty;
            }
        }
        return result;
    }

    private static string ConvertJsonElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "",
            _ => element.ToString()
        };
    }
}

public class WorkflowNodeDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public NodePosition Position { get; set; } = new();
    public Dictionary<string, string> Data { get; set; } = new();

    public static WorkflowNodeDto FromEntity(WorkflowNode entity)
    {
        return new WorkflowNodeDto
        {
            Id = entity.Id,
            Type = entity.Type.ToString(),
            Position = entity.Position,
            Data = ConvertToStringDictionary(entity.Data)
        };
    }

    private static Dictionary<string, string> ConvertToStringDictionary(Dictionary<string, object> source)
    {
        var result = new Dictionary<string, string>();
        foreach (var kvp in source)
        {
            if (kvp.Value == null)
                continue;

            if (kvp.Value is JsonElement element)
            {
                result[kvp.Key] = ConvertJsonElementToString(element);
            }
            else
            {
                result[kvp.Key] = kvp.Value.ToString() ?? string.Empty;
            }
        }
        return result;
    }

    private static string ConvertJsonElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "",
            _ => element.ToString()
        };
    }
}

public class WorkflowEdgeDto
{
    public string Id { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Condition { get; set; }

    public static WorkflowEdgeDto FromEntity(WorkflowEdge entity)
    {
        return new WorkflowEdgeDto
        {
            Id = entity.Id,
            Source = entity.Source,
            Target = entity.Target,
            Type = entity.Type.ToString(),
            Condition = entity.Condition
        };
    }
}
