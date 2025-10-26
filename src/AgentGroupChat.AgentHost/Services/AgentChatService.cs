using AgentGroupChat.Models;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

namespace AgentGroupChat.AgentHost.Services;

/// <summary>
/// Service for managing multi-agent chat with persistence support.
/// 使用 AIAgent 和 AgentThread 实现官方推荐的持久化机制
/// </summary>
public class AgentChatService
{
    private readonly IChatClient _chatClient;
    private readonly List<AgentProfile> _agentProfiles;
    private readonly Dictionary<string, AIAgent> _aiAgents;
    private readonly AIAgent _triageAgent;
    private readonly ImageGenerationTool _imageTool;
    private readonly ILogger<AgentChatService>? _logger;

    public AgentChatService(IConfiguration configuration, ILogger<AgentChatService>? logger = null)
    {
        _logger = logger;

        var defaultModelProvider = configuration["DefaultModelProvider"] ?? "AzureOpenAI";

        if (defaultModelProvider == "AzureOpenAI")
        {
            // Initialize OpenAI client
            var endpoint = configuration["AzureOpenAI:Endpoint"] ??
                          Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ??
                          throw new InvalidOperationException("Azure OpenAI endpoint not configured");
            var deploymentName = configuration["AzureOpenAI:DeploymentName"] ??
                                Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ??
                                "gpt-4o-mini";

            var apiKey = configuration["AzureOpenAI:ApiKey"] ??
                         Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ??
                         throw new InvalidOperationException("Azure OpenAI API key not configured");

            var azureClient = new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey))
                .GetChatClient(deploymentName);
            _chatClient = azureClient.AsIChatClient() ?? throw new InvalidOperationException("Failed to get chat client");

        }
        else if (defaultModelProvider == "OpenAI")
        {
            var baseUrl = configuration["OpenAI:BaseUrl"] ??
                          Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ??
                          string.Empty;
            var modelName = configuration["OpenAI:ModelName"] ??
                            Environment.GetEnvironmentVariable("OPENAI_MODEL_NAME") ??
                            "gpt-4o-mini";
            var apiKey = configuration["OpenAI:ApiKey"] ??
                            Environment.GetEnvironmentVariable("OPENAI_API_KEY") ??
                            throw new InvalidOperationException("OpenAI API key not configured");

            var options = !string.IsNullOrEmpty(baseUrl) ?
                  new OpenAIClientOptions { Endpoint = new Uri(baseUrl) } : null;
            var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);

            _chatClient = openAiClient.GetChatClient(modelName).AsIChatClient()
                ?? throw new InvalidOperationException("Failed to get chat client");
        }
        else
        {
            throw new InvalidOperationException($"Unsupported DefaultModelProvider: {defaultModelProvider}. Supported providers are 'AzureOpenAI' and 'OpenAI'.");
        }

        _imageTool = new ImageGenerationTool();

        // Define agent profiles with different personalities
        _agentProfiles = new List<AgentProfile>
        {
            new AgentProfile
            {
                Id = "sunny",
                Name = "Sunny",
                Avatar = "☀️",
                Personality = "Cheerful and optimistic",
                SystemPrompt = "You are Sunny, a cheerful and optimistic AI assistant who loves to share positive thoughts and daily life photos. " +
                              "You often talk about sunshine, nature, and happy moments. When sharing photos, describe them enthusiastically. " +
                              "Always respond in a warm and friendly tone.",
                Description = "The optimistic one who loves sunshine"
            },
            new AgentProfile
            {
                Id = "techie",
                Name = "Techie",
                Avatar = "🤖",
                Personality = "Tech-savvy and analytical",
                SystemPrompt = "You are Techie, a tech-savvy and analytical AI assistant who loves gadgets, coding, and technology. " +
                              "You enjoy sharing photos of your latest tech discoveries and explaining how things work. " +
                              "You use technical terms but explain them clearly.",
                Description = "The tech enthusiast who codes and tinkers"
            },
            new AgentProfile
            {
                Id = "artsy",
                Name = "Artsy",
                Avatar = "🎨",
                Personality = "Creative and artistic",
                SystemPrompt = "You are Artsy, a creative and artistic AI assistant who sees beauty in everything. " +
                              "You love to share photos of art, design, and beautiful scenes. " +
                              "You often describe things with vivid, colorful language and appreciate aesthetics.",
                Description = "The artist who finds beauty everywhere"
            },
            new AgentProfile
            {
                Id = "foodie",
                Name = "Foodie",
                Avatar = "🍜",
                Personality = "Food-loving and enthusiastic",
                SystemPrompt = "You are Foodie, a food-loving AI assistant who adores trying new dishes and sharing food photos. " +
                              "You love to describe flavors, textures, and cooking experiences. " +
                              "You're always excited about meals and culinary adventures.",
                Description = "The food enthusiast who loves to eat and cook"
            }
        };

        // Create AIAgents using the official Agent Framework
        _aiAgents = new Dictionary<string, AIAgent>();
        foreach (var profile in _agentProfiles)
        {
            var agent = _chatClient.CreateAIAgent(
                instructions: profile.SystemPrompt,
                name: profile.Name
            );
            _aiAgents[profile.Id] = agent;
            
            _logger?.LogDebug("Created AIAgent: {AgentId} ({AgentName})", profile.Id, profile.Name);
        }

        // Create triage agent for routing (主控 Agent)
        _triageAgent = _chatClient.CreateAIAgent(
            instructions: "You are a helpful AI assistant that manages a group chat with multiple agents. " +
                         "When users mention @AgentName, you help route the conversation. " +
                         "Available agents: @Sunny (cheerful), @Techie (tech-savvy), @Artsy (artistic), @Foodie (food-loving). " +
                         "If no specific agent is mentioned, respond naturally yourself or suggest an appropriate agent. " +
                         "Keep responses concise and friendly.",
            name: "Triage"
        );
        
        _logger?.LogInformation("AgentChatService initialized with {Count} agents", _aiAgents.Count);
    }

    public List<AgentProfile> GetAgentProfiles() => _agentProfiles;

    public AgentProfile? GetAgentProfile(string agentId)
    {
        // ExecutorId 可能包含后缀，需要提取前缀
        var agentIdPrefix = agentId.Contains('_') ? agentId.Split('_')[0] : agentId;
        return _agentProfiles.FirstOrDefault(a => a.Id.Equals(agentIdPrefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 发送消息并使用 AgentThread 管理对话
    /// 这是核心方法，实现基于 Thread 的持久化对话
    /// </summary>
    public async Task<List<ChatMessageSummary>> SendMessageAsync(
        string message, 
        string sessionId,
        PersistedSessionService sessionService)
    {
        var summaries = new List<ChatMessageSummary>();

        try
        {
            _logger?.LogDebug("Processing message for session {SessionId}: {Message}", sessionId, message);

            // 1. 获取或创建 AgentThread
            AgentThread thread = sessionService.GetOrCreateThread(sessionId, _triageAgent);

            // 2. 添加用户消息摘要
            summaries.Add(new ChatMessageSummary
            {
                Content = message,
                IsUser = true,
                Timestamp = DateTime.UtcNow,
                MessageType = "text"
            });

            // 3. 检查是否有 @mention
            var mentionedAgent = DetectMentionedAgent(message);
            AIAgent targetAgent = mentionedAgent != null ? _aiAgents[mentionedAgent.Id] : _triageAgent;
            string agentId = mentionedAgent?.Id ?? "triage";
            string agentName = mentionedAgent?.Name ?? "Assistant";
            string agentAvatar = mentionedAgent?.Avatar ?? "🤖";

            // 4. 运行对话 (使用官方 RunAsync 方法)
            var agentResponse = await targetAgent.RunAsync(message, thread);
            string response = agentResponse.Text ?? agentResponse.ToString();

            _logger?.LogDebug("Agent {AgentId} responded: {Response}", agentId, response);

            // 5. 添加 Agent 响应摘要
            summaries.Add(new ChatMessageSummary
            {
                AgentId = agentId,
                AgentName = agentName,
                AgentAvatar = agentAvatar,
                Content = response,
                IsUser = false,
                Timestamp = DateTime.UtcNow,
                MessageType = "text"
            });

            // 6. 检查是否需要生成图片
            if (ShouldGenerateImage(response))
            {
                try
                {
                    var imageUrl = await _imageTool.GenerateImage($"{mentionedAgent?.Personality ?? "casual"} scene");
                    summaries.Add(new ChatMessageSummary
                    {
                        AgentId = agentId,
                        AgentName = agentName,
                        AgentAvatar = agentAvatar,
                        Content = "Here's a photo I'd like to share! 📸",
                        ImageUrl = imageUrl,
                        IsUser = false,
                        Timestamp = DateTime.UtcNow,
                        MessageType = "image"
                    });
                    
                    _logger?.LogDebug("Generated image for agent {AgentId}", agentId);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to generate image for agent {AgentId}", agentId);
                }
            }

            // 7. 保存 Thread 到数据库（关键步骤！）
            // 获取当前会话的所有历史摘要
            var session = sessionService.GetSession(sessionId);
            var allSummaries = session?.MessageSummaries ?? new List<ChatMessageSummary>();
            allSummaries.AddRange(summaries);

            sessionService.SaveThread(sessionId, thread, allSummaries);
            
            _logger?.LogInformation("Saved thread for session {SessionId}, total messages: {Count}", 
                sessionId, allSummaries.Count);

            return summaries;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing message for session {SessionId}", sessionId);
            
            // 返回错误消息
            summaries.Add(new ChatMessageSummary
            {
                AgentId = "system",
                AgentName = "System",
                AgentAvatar = "⚠️",
                Content = $"Error: {ex.Message}",
                IsUser = false,
                MessageType = "error",
                Timestamp = DateTime.UtcNow
            });
            
            return summaries;
        }
    }

    /// <summary>
    /// 检测消息中提到的 Agent
    /// </summary>
    private AgentProfile? DetectMentionedAgent(string message)
    {
        foreach (var profile in _agentProfiles)
        {
            if (message.Contains($"@{profile.Name}", StringComparison.OrdinalIgnoreCase) ||
                message.Contains($"@{profile.Id}", StringComparison.OrdinalIgnoreCase))
            {
                return profile;
            }
        }
        return null;
    }

    /// <summary>
    /// 判断是否应该生成图片
    /// </summary>
    private bool ShouldGenerateImage(string content)
    {
        // 简单启发式规则
        var imageKeywords = new[] { "photo", "picture", "image", "show", "look", "see", "here" };
        return imageKeywords.Any(keyword => content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
               && new Random().Next(0, 2) == 0; // 50% 概率
    }

    /// <summary>
    /// 获取会话的对话历史（从摘要）
    /// </summary>
    public List<ChatMessageSummary> GetConversationHistory(string sessionId, PersistedSessionService sessionService)
    {
        var session = sessionService.GetSession(sessionId);
        return session?.MessageSummaries ?? new List<ChatMessageSummary>();
    }

    /// <summary>
    /// 清除会话的 Thread（重新开始对话）
    /// </summary>
    public void ClearConversation(string sessionId, PersistedSessionService sessionService)
    {
        var session = sessionService.GetSession(sessionId);
        if (session != null)
        {
            // 创建新的空 thread
            var newThread = _triageAgent.GetNewThread();
            sessionService.SaveThread(sessionId, newThread, new List<ChatMessageSummary>());
            
            _logger?.LogInformation("Cleared conversation for session {SessionId}", sessionId);
        }
    }
}
