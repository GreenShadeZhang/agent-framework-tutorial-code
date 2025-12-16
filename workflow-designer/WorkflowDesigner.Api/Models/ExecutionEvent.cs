namespace WorkflowDesigner.Api.Models;

/// <summary>
/// 执行事件 (用于 SSE 流式输出)
/// </summary>
public class ExecutionEvent
{
    /// <summary>
    /// 事件类型
    /// </summary>
    public ExecutionEventType Type { get; set; }

    /// <summary>
    /// 节点ID
    /// </summary>
    public string? NodeId { get; set; }

    /// <summary>
    /// 节点名称
    /// </summary>
    public string? NodeName { get; set; }

    /// <summary>
    /// 执行状态
    /// </summary>
    public ExecutionStatus Status { get; set; }

    /// <summary>
    /// 消息内容
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 数据
    /// </summary>
    public Dictionary<string, object>? Data { get; set; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 执行事件类型
/// </summary>
public enum ExecutionEventType
{
    /// <summary>
    /// 工作流开始
    /// </summary>
    WorkflowStarted,

    /// <summary>
    /// 工作流完成
    /// </summary>
    WorkflowCompleted,

    /// <summary>
    /// 工作流失败
    /// </summary>
    WorkflowFailed,

    /// <summary>
    /// 节点开始
    /// </summary>
    NodeStarted,

    /// <summary>
    /// 节点完成
    /// </summary>
    NodeCompleted,

    /// <summary>
    /// 节点失败
    /// </summary>
    NodeFailed,

    /// <summary>
    /// 日志消息
    /// </summary>
    LogMessage,

    /// <summary>
    /// 进度更新
    /// </summary>
    ProgressUpdate
}

/// <summary>
/// 声明式工作流执行结果
/// </summary>
public class DeclarativeExecutionResult
{
    /// <summary>
    /// 执行ID
    /// </summary>
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 工作流ID
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// 工作流名称
    /// </summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>
    /// 执行状态
    /// </summary>
    public ExecutionStatus Status { get; set; }

    /// <summary>
    /// 用户输入
    /// </summary>
    public string UserInput { get; set; } = string.Empty;

    /// <summary>
    /// 最终输出
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// 执行的节点数
    /// </summary>
    public int ExecutedNodes { get; set; }

    /// <summary>
    /// 执行步骤详情
    /// </summary>
    public List<DeclarativeExecutionStep> Steps { get; set; } = new();

    /// <summary>
    /// 变量状态
    /// </summary>
    public Dictionary<string, object> Variables { get; set; } = new();

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 声明式工作流执行步骤
/// </summary>
public class DeclarativeExecutionStep
{
    /// <summary>
    /// 执行器ID
    /// </summary>
    public string ExecutorId { get; set; } = string.Empty;

    /// <summary>
    /// 执行器名称
    /// </summary>
    public string ExecutorName { get; set; } = string.Empty;

    /// <summary>
    /// 执行器类型
    /// </summary>
    public string ExecutorType { get; set; } = string.Empty;

    /// <summary>
    /// 执行状态
    /// </summary>
    public ExecutionStatus Status { get; set; }

    /// <summary>
    /// 输出
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}
