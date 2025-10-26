namespace AgentGroupChat.Models;

/// <summary>
/// 智能体组配置模型
/// 定义一组协作的智能体及其 Triage 路由配置
/// </summary>
public class AgentGroup
{
    /// <summary>
    /// 组唯一标识符
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 组名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 组描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Triage Agent 的系统提示词（可自定义）
    /// 如果为空，则使用默认的 Triage 提示词
    /// </summary>
    public string? TriageSystemPrompt { get; set; }

    /// <summary>
    /// 组内智能体 ID 列表
    /// </summary>
    public List<string> AgentIds { get; set; } = new();

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
}
