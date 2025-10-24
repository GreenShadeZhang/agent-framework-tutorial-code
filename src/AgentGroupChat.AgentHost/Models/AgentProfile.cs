namespace AgentGroupChat.Models;

/// <summary>
/// Represents an agent profile with personality and avatar.
/// </summary>
public class AgentProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
