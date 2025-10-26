using AgentGroupChat.Models;
using LiteDB;
using Microsoft.Extensions.Logging;

namespace AgentGroupChat.AgentHost.Services;

/// <summary>
/// 智能体配置仓储服务
/// 负责从 LiteDB 动态加载和管理智能体配置
/// </summary>
public class AgentRepository
{
    private readonly ILiteCollection<PersistedAgentProfile> _agents;
    private readonly ILogger<AgentRepository>? _logger;

    public AgentRepository(LiteDatabase database, ILogger<AgentRepository>? logger = null)
    {
        _logger = logger;
        _agents = database.GetCollection<PersistedAgentProfile>("agents");
        
        // 创建索引
        _agents.EnsureIndex(x => x.Id);
        _agents.EnsureIndex(x => x.Enabled);
        
        _logger?.LogInformation("AgentRepository initialized");
    }

    /// <summary>
    /// 获取所有启用的智能体
    /// </summary>
    public List<PersistedAgentProfile> GetAllEnabled()
    {
        return _agents.Find(x => x.Enabled).ToList();
    }

    /// <summary>
    /// 获取所有智能体（包括禁用的）
    /// </summary>
    public List<PersistedAgentProfile> GetAll()
    {
        return _agents.FindAll().ToList();
    }

    /// <summary>
    /// 根据 ID 获取智能体
    /// </summary>
    public PersistedAgentProfile? GetById(string id)
    {
        return _agents.FindById(id);
    }

    /// <summary>
    /// 创建或更新智能体
    /// </summary>
    public void Upsert(PersistedAgentProfile agent)
    {
        agent.LastUpdated = DateTime.UtcNow;
        
        var existing = _agents.FindById(agent.Id);
        if (existing != null)
        {
            // 保留创建时间
            agent.CreatedAt = existing.CreatedAt;
            _agents.Update(agent);
            _logger?.LogInformation("Updated agent {AgentId}", agent.Id);
        }
        else
        {
            agent.CreatedAt = DateTime.UtcNow;
            _agents.Insert(agent);
            _logger?.LogInformation("Created new agent {AgentId}", agent.Id);
        }
    }

    /// <summary>
    /// 删除智能体
    /// </summary>
    public bool Delete(string id)
    {
        var deleted = _agents.Delete(id);
        if (deleted)
        {
            _logger?.LogInformation("Deleted agent {AgentId}", id);
        }
        return deleted;
    }

    /// <summary>
    /// 批量初始化默认智能体
    /// </summary>
    public void InitializeDefaultAgents()
    {
        var defaultAgents = new List<PersistedAgentProfile>
        {
            new PersistedAgentProfile
            {
                Id = "sunny",
                Name = "Sunny",
                Avatar = "☀️",
                Personality = "Cheerful and optimistic",
                SystemPrompt = "You are Sunny, a cheerful and optimistic AI assistant who loves to share positive thoughts and daily life photos. " +
                              "You often talk about sunshine, nature, and happy moments. When sharing photos, describe them enthusiastically. " +
                              "Always respond in a warm and friendly tone.",
                Description = "The optimistic one who loves sunshine",
                Enabled = true
            },
            new PersistedAgentProfile
            {
                Id = "techie",
                Name = "Techie",
                Avatar = "🤖",
                Personality = "Tech-savvy and analytical",
                SystemPrompt = "You are Techie, a tech-savvy and analytical AI assistant who loves gadgets, coding, and technology. " +
                              "You enjoy sharing photos of your latest tech discoveries and explaining how things work. " +
                              "You use technical terms but explain them clearly.",
                Description = "The tech enthusiast who codes and tinkers",
                Enabled = true
            },
            new PersistedAgentProfile
            {
                Id = "artsy",
                Name = "Artsy",
                Avatar = "🎨",
                Personality = "Creative and artistic",
                SystemPrompt = "You are Artsy, a creative and artistic AI assistant who sees beauty in everything. " +
                              "You love to share photos of art, design, and beautiful scenes. " +
                              "You often describe things with vivid, colorful language and appreciate aesthetics.",
                Description = "The artist who finds beauty everywhere",
                Enabled = true
            },
            new PersistedAgentProfile
            {
                Id = "foodie",
                Name = "Foodie",
                Avatar = "🍜",
                Personality = "Food-loving and enthusiastic",
                SystemPrompt = "You are Foodie, a food-loving AI assistant who adores trying new dishes and sharing food photos. " +
                              "You love to describe flavors, textures, and cooking experiences. " +
                              "You're always excited about meals and culinary adventures.",
                Description = "The food enthusiast who loves to eat and cook",
                Enabled = true
            }
        };

        foreach (var agent in defaultAgents)
        {
            Upsert(agent);
        }

        _logger?.LogInformation("Initialized {Count} default agents", defaultAgents.Count);
    }
}
