namespace AgentGroupChat.Models;

/// <summary>
/// 消息摘要，用于快速展示列表和 UI 渲染
/// 不包含完整的 Agent 状态，仅用于显示目的
/// </summary>
public class ChatMessageSummary
{
    /// <summary>
    /// 消息唯一标识符
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Agent ID（例如：sunny, techie, artsy, foodie）
    /// 如果是用户消息则为 "user"
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Agent 显示名称
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Agent 头像/表情符号
    /// </summary>
    public string AgentAvatar { get; set; } = string.Empty;

    /// <summary>
    /// 消息内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 是否为用户消息
    /// </summary>
    public bool IsUser { get; set; }

    /// <summary>
    /// 消息时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 图片 URL（如果有）
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// 消息类型（用于扩展：text, image, system, error）
    /// </summary>
    public string MessageType { get; set; } = "text";
}
