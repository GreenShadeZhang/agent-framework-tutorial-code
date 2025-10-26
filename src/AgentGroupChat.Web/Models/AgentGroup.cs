namespace AgentGroupChat.Models;

/// <summary>
/// 智能体组配置模型（Web客户端版本）
/// </summary>
public class AgentGroup
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? TriageSystemPrompt { get; set; }
    public List<string> AgentIds { get; set; } = new();
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
