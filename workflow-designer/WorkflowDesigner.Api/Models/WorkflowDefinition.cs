namespace WorkflowDesigner.Api.Models;

/// <summary>
/// 工作流定义
/// </summary>
public class WorkflowDefinition
{
    /// <summary>
    /// 工作流ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 工作流名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 版本号
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// 工作流节点
    /// </summary>
    public List<WorkflowNode> Nodes { get; set; } = new();

    /// <summary>
    /// 工作流边
    /// </summary>
    public List<WorkflowEdge> Edges { get; set; } = new();

    /// <summary>
    /// 输入参数定义
    /// </summary>
    public List<ParameterDefinition> Parameters { get; set; } = new();

    /// <summary>
    /// YAML内容
    /// </summary>
    public string YamlContent { get; set; } = string.Empty;

    /// <summary>
    /// 序列化的工作流数据 (JSON)
    /// </summary>
    public string WorkflowDump { get; set; } = string.Empty;

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 是否已发布
    /// </summary>
    public bool IsPublished { get; set; } = false;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 工作流节点
/// </summary>
public class WorkflowNode
{
    /// <summary>
    /// 节点ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 节点类型
    /// </summary>
    public WorkflowNodeType Type { get; set; }

    /// <summary>
    /// 节点位置
    /// </summary>
    public NodePosition Position { get; set; } = new();

    /// <summary>
    /// 节点数据
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// 节点类型
/// </summary>
public enum WorkflowNodeType
{
    /// <summary>
    /// 开始节点
    /// </summary>
    Start,

    /// <summary>
    /// 智能体节点
    /// </summary>
    Agent,

    /// <summary>
    /// 条件节点
    /// </summary>
    Condition,

    /// <summary>
    /// 结束节点
    /// </summary>
    End
}

/// <summary>
/// 节点位置
/// </summary>
public class NodePosition
{
    /// <summary>
    /// X坐标
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y坐标
    /// </summary>
    public double Y { get; set; }
}

/// <summary>
/// 工作流边
/// </summary>
public class WorkflowEdge
{
    /// <summary>
    /// 边ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 源节点ID
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 目标节点ID
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// 边类型
    /// </summary>
    public WorkflowEdgeType Type { get; set; } = WorkflowEdgeType.Direct;

    /// <summary>
    /// 条件表达式 (用于条件边)
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    /// 源节点的输出句柄 (用于条件节点的 true/false 分支)
    /// </summary>
    public string? SourceHandle { get; set; }
}

/// <summary>
/// 边类型
/// </summary>
public enum WorkflowEdgeType
{
    /// <summary>
    /// 直接连接
    /// </summary>
    Direct,

    /// <summary>
    /// 条件连接
    /// </summary>
    Conditional
}

/// <summary>
/// 参数定义
/// </summary>
public class ParameterDefinition
{
    /// <summary>
    /// 参数名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 参数类型
    /// </summary>
    public string Type { get; set; } = "string";

    /// <summary>
    /// 是否必需
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// 默认值
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
