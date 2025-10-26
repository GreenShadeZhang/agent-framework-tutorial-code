namespace AgentGroupChat.Models;

/// <summary>
/// Represents a chat message in the group chat.
/// 前端视图模型 - 对应后端的 ChatMessageSummary
/// </summary>
public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AgentId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string AgentAvatar { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsUser { get; set; }
    
    /// <summary>
    /// 消息类型：text, image, system, error
    /// </summary>
    public string MessageType { get; set; } = "text";
}
