namespace AgentGroupChat.Models;

/// <summary>
/// 持久化的智能体配置模型（Web客户端版本）
/// </summary>
public class PersistedAgentProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
