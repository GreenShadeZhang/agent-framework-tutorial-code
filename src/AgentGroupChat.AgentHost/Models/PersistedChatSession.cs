namespace AgentGroupChat.Models;

/// <summary>
/// 持久化的会话模型，存储 AgentThread 序列化数据
/// 结合 LiteDB 和 Agent Framework 的官方持久化机制
/// </summary>
public class PersistedChatSession
{
    /// <summary>
    /// 会话唯一标识符
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 会话名称（用户可自定义）
    /// </summary>
    public string Name { get; set; } = $"Session {DateTime.Now:yyyy-MM-dd HH:mm}";

    /// <summary>
    /// 序列化的 AgentThread 数据（JSON 格式）
    /// 包含完整的对话上下文、状态和元数据
    /// </summary>
    public string ThreadData { get; set; } = string.Empty;

    /// <summary>
    /// 消息摘要列表（用于快速展示，不包含完整 Thread 数据）
    /// </summary>
    public List<ChatMessageSummary> MessageSummaries { get; set; } = new();

    /// <summary>
    /// 会话创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 消息总数
    /// </summary>
    public int MessageCount { get; set; } = 0;

    /// <summary>
    /// 序列化版本号（用于未来的兼容性）
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 是否为活跃会话（用于缓存优化）
    /// </summary>
    public bool IsActive { get; set; } = true;
}
