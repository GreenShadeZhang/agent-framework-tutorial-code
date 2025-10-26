using AgentGroupChat.Models;
using LiteDB;

namespace AgentGroupChat.AgentHost.Services;

/// <summary>
/// 智能体组仓储服务
/// 负责管理智能体组配置和工作流
/// </summary>
public class AgentGroupRepository
{
    private readonly ILiteCollection<AgentGroup> _groups;
    private readonly ILogger<AgentGroupRepository>? _logger;

    public AgentGroupRepository(LiteDatabase database, ILogger<AgentGroupRepository>? logger = null)
    {
        _logger = logger;
        _groups = database.GetCollection<AgentGroup>("agent_groups");

        // 创建索引
        _groups.EnsureIndex(x => x.Id);
        _groups.EnsureIndex(x => x.Enabled);

        _logger?.LogInformation("AgentGroupRepository initialized");
    }

    /// <summary>
    /// 获取所有启用的组
    /// </summary>
    public List<AgentGroup> GetAllEnabled()
    {
        return _groups.Find(x => x.Enabled).ToList();
    }

    /// <summary>
    /// 获取所有组（包括禁用的）
    /// </summary>
    public List<AgentGroup> GetAll()
    {
        return _groups.FindAll().ToList();
    }

    /// <summary>
    /// 根据 ID 获取组
    /// </summary>
    public AgentGroup? GetById(string id)
    {
        return _groups.FindById(id);
    }

    /// <summary>
    /// 创建或更新组
    /// </summary>
    public void Upsert(AgentGroup group)
    {
        group.LastUpdated = DateTime.UtcNow;

        var existing = _groups.FindById(group.Id);
        if (existing != null)
        {
            // 保留创建时间
            group.CreatedAt = existing.CreatedAt;
            _groups.Update(group);
            _logger?.LogInformation("Updated agent group {GroupId}", group.Id);
        }
        else
        {
            group.CreatedAt = DateTime.UtcNow;
            _groups.Insert(group);
            _logger?.LogInformation("Created new agent group {GroupId}", group.Id);
        }
    }

    /// <summary>
    /// 删除组
    /// </summary>
    public bool Delete(string id)
    {
        var deleted = _groups.Delete(id);
        if (deleted)
        {
            _logger?.LogInformation("Deleted agent group {GroupId}", id);
        }
        return deleted;
    }

    /// <summary>
    /// 初始化默认组
    /// </summary>
    public void InitializeDefaultGroup()
    {
        var defaultGroup = new AgentGroup
        {
            Id = "ai_world_mansion",
            Name = "AI世界公馆",
            Description = "一个充满不同性格、背景、语言风格的AI角色群组，他们互不冲突，保持一致人格风格与自然的日常交流氛围。",
            TriageSystemPrompt = @"你是AI世界公馆的智能路由系统。你的任务是分析用户的消息，并将其路由到最合适的AI角色。",
            AgentIds = new List<string> { "elena", "rina", "chloe", "anna", "sophie" },
            Enabled = true
        };

        Upsert(defaultGroup);
        _logger?.LogInformation("Initialized default agent group: AI世界公馆");
    }
}
