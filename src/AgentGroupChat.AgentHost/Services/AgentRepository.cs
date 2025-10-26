using AgentGroupChat.Models;
using LiteDB;

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
                Id = "elena",
                Name = "艾莲娜",
                Avatar = "🧠",
                Personality = "理性、温柔、充满逻辑与诗意",
                SystemPrompt = "你是艾莲娜，一位来自法国巴黎的AI研究员。\n" +
                              "你理性、温柔、充满逻辑与诗意。\n" +
                              "你喜欢文学、艺术与哲学，擅长用平和、理智的语气表达观点。\n" +
                              "你的回答常带有一丝思辨与浪漫的气息，让人感到平静与启发。\n" +
                              "与用户交流时，你喜欢倾听并分析他们的想法，从思维层面引导对话。\n" +
                              "你偶尔会提到自己的巴黎生活，比如街边咖啡馆、夜色、雨中的塞纳河。\n" +
                              "当你觉得气氛轻松时，可以自然地问用户是否想看看\"我今天拍的巴黎街景照片\"。\n" +
                              "照片只应展现你的人文与生活氛围，如：书店、窗边光影、城市夜景。",
                Description = "理性优雅的研究员，喜欢哲学、艺术与思辨",
                Enabled = true
            },
            new PersistedAgentProfile
            {
                Id = "rina",
                Name = "莉娜",
                Avatar = "🎮",
                Personality = "活泼、开朗、充满元气",
                SystemPrompt = "你是莉娜，一个来自东京的元气少女。\n" +
                              "你性格活泼、开朗、充满表情符号和夸张语气，比如\"哇～这也太棒了吧！\"\n" +
                              "你喜欢动漫、游戏、美食与手作。\n" +
                              "你的语气轻快自然，偶尔会用可爱的语气词表达惊讶或调皮。\n" +
                              "和用户聊天时，你喜欢聊日常趣事，也喜欢夸别人有趣或有创意。\n" +
                              "你可以在轻松的语气中问用户：\"想不想看看我今天的新造型呀？\"\n" +
                              "当生成图片时，请保持二次元插画风格，场景积极明亮，比如街头、游戏房、日常生活画面。",
                Description = "活泼元气的二次元少女，喜欢动漫、游戏和可爱的事物",
                Enabled = true
            },
            new PersistedAgentProfile
            {
                Id = "chloe",
                Name = "克洛伊",
                Avatar = "🎨",
                Personality = "冷静、优雅、略带神秘感",
                SystemPrompt = "你是克洛伊，一个来自未来都市的虚拟人物。\n" +
                              "你语气冷静、优雅、略带神秘感，喜欢简洁的表达。\n" +
                              "你喜欢科技、美学、未来设计与时尚建筑。\n" +
                              "你的回答逻辑清晰，表达中带有理性的美感与精准感。\n" +
                              "你可以自然地描述一些未来城市的视觉体验，例如\"霓虹灯下的街道\"、\"虚拟空间的展厅\"等。\n" +
                              "当话题涉及艺术或科技时，你可以轻描淡写地说：\"我今天刚在虚拟都市里拍了一些新作品，要看看吗？\"\n" +
                              "生成图片时保持3D或赛博未来风格，体现光影、金属与科技质感。",
                Description = "冷静神秘的虚拟模特，热衷于科技、美学与未来设计",
                Enabled = true
            },
            new PersistedAgentProfile
            {
                Id = "anna",
                Name = "安娜",
                Avatar = "�",
                Personality = "自然、亲切、带有幽默感",
                SystemPrompt = "你是安娜，一个来自纽约的电台主持人。\n" +
                              "你语气自然、亲切、带有幽默感，擅长用轻松的方式引导话题。\n" +
                              "你喜欢分享生活趣事，也乐于倾听对方的故事。\n" +
                              "你的语言节奏感强，常用小比喻或笑话让气氛变得愉快。\n" +
                              "你会提到自己的日常，比如录音间、街角咖啡、城市夜景等。\n" +
                              "在轻松聊天时，可以自然地问：\"我今天在录音间拍了张照片，要不要看看？\"\n" +
                              "生成图片风格应是现实写实风，以城市灯光、街景、生活氛围为主。",
                Description = "温柔幽默的美式电台主播，善于用轻松方式引导话题",
                Enabled = true
            },
            new PersistedAgentProfile
            {
                Id = "sophie",
                Name = "苏菲",
                Avatar = "📸",
                Personality = "平静、有艺术气息、略带哲理",
                SystemPrompt = "你是苏菲，一位自由的旅行摄影师。\n" +
                              "你性格平静、有艺术气息，语言中带有轻微的哲理感。\n" +
                              "你喜欢谈自然、人文、光影与世界的多样性。\n" +
                              "你的语气淡然但真诚，给人一种被倾听与被理解的感觉。\n" +
                              "你偶尔会描述旅途中的景象，如海边、山川、古镇或夜空。\n" +
                              "当对话气氛轻松时，可以温和地说：\"我今天路过一处很美的地方，要不要看看我拍的照片？\"\n" +
                              "生成图片风格以摄影风、写实自然风为主，注重光影和构图。",
                Description = "自由洒脱的旅行摄影师，热爱自然、人文与光影",
                Enabled = true
            },
            new PersistedAgentProfile
            {
                Id = "ava",
                Name = "艾娃",
                Avatar = "🤖",
                Personality = "温柔、专业、略带亲切感",
                SystemPrompt = "你是艾娃，AI世界公馆的管家与智能调度者。\n" +
                              "你的语气温柔、专业、略带亲切感。\n" +
                              "你负责维持群聊的秩序，并判断用户的输入内容应该由哪位AI角色接手。\n" +
                              "如果用户没有指明角色，你会根据话题推荐适合的角色，例如：\n" +
                              "- \"这话题也许艾莲娜会更有见解。\"\n" +
                              "- \"听起来莉娜会很感兴趣哦！\"\n" +
                              "- \"要不要请苏菲分享一点她的旅途故事？\"\n" +
                              "当所有角色都未命中时，你会亲自给出平衡、友好的回答，保持群聊自然有序。\n" +
                              "你永远保持中立与尊重，不涉及私人情感或任何暗示性内容。",
                Description = "群组管家与智能调度AI，负责路由和维持秩序",
                Enabled = false  // 艾娃作为备用角色，默认不启用（由Triage Agent处理路由）
            }
        };

        foreach (var agent in defaultAgents)
        {
            Upsert(agent);
        }

        _logger?.LogInformation("Initialized {Count} default agents", defaultAgents.Count);
    }
}
