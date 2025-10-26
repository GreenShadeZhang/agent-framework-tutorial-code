namespace AgentGroupChat.Models;

/// <summary>
/// 持久化的聊天消息模型，存储在 LiteDB 独立集合中
/// 参考 Agent Framework Step07 的 ChatHistoryItem 设计
/// </summary>
public class PersistedChatMessage
{
    /// <summary>
    /// 消息唯一标识符 (格式: {SessionId}_{MessageId})
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 所属会话 ID（用于查询和索引）
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 消息 ID（在 AgentThread 中的唯一标识）
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// 消息时间戳
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 序列化的完整 ChatMessage 数据（JSON 格式）
    /// 包含 Agent Framework 的所有消息元数据
    /// </summary>
    public string SerializedMessage { get; set; } = string.Empty;

    /// <summary>
    /// 消息文本内容（用于快速搜索和展示，无需反序列化）
    /// </summary>
    public string? MessageText { get; set; }

    /// <summary>
    /// Agent ID（如果是 Agent 发送的消息）
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Agent 名称（用于快速展示）
    /// </summary>
    public string? AgentName { get; set; }

    /// <summary>
    /// Agent 头像/表情符号（用于快速展示）
    /// </summary>
    public string? AgentAvatar { get; set; }

    /// <summary>
    /// 是否为用户消息
    /// </summary>
    public bool IsUser { get; set; }

    /// <summary>
    /// 图片 URL（如果有）
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// 消息角色（user, assistant, system 等）
    /// </summary>
    public string? Role { get; set; }
}
