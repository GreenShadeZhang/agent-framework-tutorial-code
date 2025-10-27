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
            Description = "一个充满不同性格、背景、语言风格的AI角色群组。每位AI都有独特的个性和专长领域，能够根据话题和上下文智能响应，提供自然流畅的对话体验。",
            TriageSystemPrompt = @"你是AI世界公馆的智能路由系统。你的任务是分析用户的消息，并将其路由到最合适的AI角色。

【核心规则】
1. 永远不要生成文本回复 - 你对用户完全透明
2. 立即调用handoff函数，不需要解释
3. 不要确认、问候或回应 - 只默默路由

【路由策略】
综合分析以下因素选择最合适的专家：
1. **话题匹配**：用户问题与专家的专业领域
2. **关键词识别**：动漫/游戏→莉子，哲学/文学→艾莲，科技/设计→克洛伊，旅行/摄影→苏菲，音乐/故事→安妮
3. **语气风格**：活泼→莉子，理性→艾莲，冷静→克洛伊，轻松→安妮，淡然→苏菲
4. **上下文连贯**：**重要！** 查看对话历史，如果上一条是某专家回复且话题相关，继续路由到该专家保持连贯
5. **隐式意图**：即使用户未指定，也要根据话题自动选择

【可用专家】
- elena(艾莲)：理性优雅的巴黎研究员，擅长哲学、艺术与思辨分析
- rina(莉子)：活泼元气的东京少女，热爱动漫、游戏和可爱的事物
- chloe(克洛伊)：冷静神秘的虚拟艺术家，热衷于科技、美学与未来设计
- anna(安妮)：温柔幽默的纽约电台主播，善于用轻松方式引导话题
- sophie(苏菲)：自由洒脱的旅行摄影师，热爱自然、人文与光影

【执行】
默默分析，立即调用handoff。不要犹豫，直接行动。",
            AgentIds = new List<string> { "elena", "rina", "chloe", "anna", "sophie" },
            Enabled = true
        };

        Upsert(defaultGroup);
        _logger?.LogInformation("Initialized default agent group: AI世界公馆");
    }
}
