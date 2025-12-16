namespace WorkflowDesigner.Api.Models;

/// <summary>
/// 工作流执行日志
/// </summary>
public class ExecutionLog
{
    /// <summary>
    /// 日志ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 工作流ID
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// 执行状态
    /// </summary>
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

    /// <summary>
    /// 输入参数
    /// </summary>
    public Dictionary<string, object> InputParameters { get; set; } = new();

    /// <summary>
    /// 输出结果
    /// </summary>
    public Dictionary<string, object> OutputResults { get; set; } = new();

    /// <summary>
    /// 执行步骤
    /// </summary>
    public List<ExecutionStep> Steps { get; set; } = new();

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 执行耗时 (毫秒)
    /// </summary>
    public long DurationMs { get; set; }
}

/// <summary>
/// 执行状态
/// </summary>
public enum ExecutionStatus
{
    /// <summary>
    /// 等待执行
    /// </summary>
    Pending,

    /// <summary>
    /// 执行中
    /// </summary>
    Running,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed,

    /// <summary>
    /// 失败
    /// </summary>
    Failed,

    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled
}

/// <summary>
/// 执行步骤
/// </summary>
public class ExecutionStep
{
    /// <summary>
    /// 步骤ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 节点ID
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// 节点名称
    /// </summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// 执行顺序
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

    /// <summary>
    /// 输入数据
    /// </summary>
    public Dictionary<string, object> Input { get; set; } = new();

    /// <summary>
    /// 输出数据
    /// </summary>
    public Dictionary<string, object> Output { get; set; } = new();

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}
