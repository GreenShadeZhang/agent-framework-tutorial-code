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
