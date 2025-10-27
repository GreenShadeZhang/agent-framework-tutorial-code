namespace AgentGroupChat.Models;

/// <summary>
/// Represents a chat session with persistent storage.
/// 前端视图模型 - 对应后端的 PersistedChatSession
/// </summary>
public class ChatSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = $"Session {DateTime.Now:yyyy-MM-dd HH:mm}";
    
    /// <summary>
    /// 所属的 Agent Group ID
    /// </summary>
    public string GroupId { get; set; } = string.Empty;
    
    /// <summary>
    /// 消息摘要列表（从后端的 MessageSummaries 映射）
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = new();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 消息总数
    /// </summary>
    public int MessageCount { get; set; } = 0;
    
    /// <summary>
    /// 是否为活跃会话
    /// </summary>
    public bool IsActive { get; set; } = true;
}
