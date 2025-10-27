namespace AgentGroupChat.Models;

/// <summary>
/// 持久化的会话模型，存储 AgentThread 序列化数据
/// 结合 LiteDB 和 Agent Framework 的官方持久化机制
/// 优化版：消息存储在独立的 messages 集合中，ThreadData 只保存最小元数据
/// </summary>
public class PersistedChatSession
{
    /// <summary>
    /// 会话唯一标识符（也是 Thread 的存储键）
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 会话名称（用户可自定义）
    /// </summary>
    public string Name { get; set; } = $"Session {DateTime.Now:yyyy-MM-dd HH:mm}";

    /// <summary>
    /// 所属的 Agent Group ID（必填）
    /// </summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>
    /// 序列化的 AgentThread 状态（JSON 格式）
    /// 优化后：只包含 SessionId 等最小元数据，不包含消息内容
    /// 消息存储在独立的 PersistedChatMessage 集合中
    /// </summary>
    public string ThreadData { get; set; } = string.Empty;

    /// <summary>
    /// 会话创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 消息总数（缓存值，用于快速展示）
    /// 真实值从 messages 集合中计算
    /// </summary>
    public int MessageCount { get; set; } = 0;

    /// <summary>
    /// 序列化版本号（用于未来的兼容性和数据迁移）
    /// </summary>
    public int Version { get; set; } = 2; // v2: 使用独立消息存储

    /// <summary>
    /// 是否为活跃会话（用于缓存优化）
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 最后一条消息的预览（用于会话列表展示）
    /// </summary>
    public string? LastMessagePreview { get; set; }

    /// <summary>
    /// 最后一条消息的发送者
    /// </summary>
    public string? LastMessageSender { get; set; }
}
