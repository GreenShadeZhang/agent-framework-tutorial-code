namespace AgentGroupChat.Models;

/// <summary>
/// Represents a chat session with persistent storage.
/// </summary>
public class ChatSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = $"Session {DateTime.Now:yyyy-MM-dd HH:mm}";
    public List<ChatMessage> Messages { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
