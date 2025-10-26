namespace AgentGroupChat.Models;

/// <summary>
/// 持久化的智能体配置模型，存储在 LiteDB 中
/// 支持动态加载和配置智能体
/// </summary>
public class PersistedAgentProfile
{
    /// <summary>
    /// 智能体唯一标识符
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 智能体显示名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 智能体头像/表情符号
    /// </summary>
    public string Avatar { get; set; } = string.Empty;

    /// <summary>
    /// 智能体性格描述
    /// </summary>
    public string Personality { get; set; } = string.Empty;

    /// <summary>
    /// 智能体系统提示词
    /// </summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>
    /// 智能体简短描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 转换为 AgentProfile 用于运行时
    /// </summary>
    public AgentProfile ToAgentProfile()
    {
        return new AgentProfile
        {
            Id = Id,
            Name = Name,
            Avatar = Avatar,
            Personality = Personality,
            SystemPrompt = SystemPrompt,
            Description = Description
        };
    }

    /// <summary>
    /// 从 AgentProfile 创建持久化模型
    /// </summary>
    public static PersistedAgentProfile FromAgentProfile(AgentProfile profile)
    {
        return new PersistedAgentProfile
        {
            Id = profile.Id,
            Name = profile.Name,
            Avatar = profile.Avatar,
            Personality = profile.Personality,
            SystemPrompt = profile.SystemPrompt,
            Description = profile.Description,
            Enabled = true,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };
    }
}
