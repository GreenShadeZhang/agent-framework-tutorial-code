using AgentGroupChat.Models;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Text.Json;

namespace AgentGroupChat.AgentHost.Services;

/// <summary>
/// Service for managing multi-agent chat with persistence support (重构版)
/// 使用 AIAgent 和 AgentThread 实现官方推荐的持久化机制
/// 集成 LiteDbChatMessageStore，消息和 Thread 状态分离存储
/// 参考 Agent Framework Step06 和 Step07 的最佳实践
/// </summary>
public class AgentChatService
{
    private readonly IChatClient _chatClient;
    private readonly List<AgentProfile> _agentProfiles;
    private readonly PersistedSessionService _sessionService;
    private readonly ImageGenerationTool _imageTool;
    private readonly ILogger<AgentChatService>? _logger;
    private readonly ILogger<LiteDbChatMessageStore>? _storeLogger;

    public AgentChatService(
        IConfiguration configuration, 
        PersistedSessionService sessionService,
        ILogger<AgentChatService>? logger = null,
        ILogger<LiteDbChatMessageStore>? storeLogger = null)
    {
        _logger = logger;
        _storeLogger = storeLogger;
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));

        var defaultModelProvider = configuration["DefaultModelProvider"] ?? "AzureOpenAI";

        if (defaultModelProvider == "AzureOpenAI")
        {
            var endpoint = configuration["AzureOpenAI:Endpoint"] ??
                          Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ??
                          throw new InvalidOperationException("Azure OpenAI endpoint not configured");
            var deploymentName = configuration["AzureOpenAI:DeploymentName"] ??
                                Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ??
                                "gpt-4o-mini";
            var apiKey = configuration["AzureOpenAI:ApiKey"] ??
                         Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ??
                         throw new InvalidOperationException("Azure OpenAI API key not configured");

            var azureClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey))
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
            throw new InvalidOperationException($"Unsupported DefaultModelProvider: {defaultModelProvider}");
        }

        _imageTool = new ImageGenerationTool();

        // Define agent profiles
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
        
        _logger?.LogInformation("AgentChatService initialized with {Count} agent profiles", _agentProfiles.Count);
    }

    public List<AgentProfile> GetAgentProfiles() => _agentProfiles;

    public AgentProfile? GetAgentProfile(string agentId)
    {
        var agentIdPrefix = agentId.Contains('_') ? agentId.Split('_')[0] : agentId;
        return _agentProfiles.FirstOrDefault(a => a.Id.Equals(agentIdPrefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 为指定会话和 Agent 创建 AIAgent（带 ChatMessageStoreFactory）
    /// 这是核心改进：每个会话的 Thread 都有独立的 LiteDbChatMessageStore
    /// </summary>
    private AIAgent CreateAgentForSession(string sessionId, AgentProfile? profile = null)
    {
        var instructions = profile?.SystemPrompt ?? 
            "You are a helpful AI assistant that manages a group chat with multiple agents. " +
            "When users mention @AgentName, you help route the conversation. " +
            "Available agents: @Sunny (cheerful), @Techie (tech-savvy), @Artsy (artistic), @Foodie (food-loving). " +
            "If no specific agent is mentioned, respond naturally yourself or suggest an appropriate agent. " +
            "Keep responses concise and friendly.";
        
        var name = profile?.Name ?? "Assistant";

        var agent = _chatClient.CreateAIAgent(new ChatClientAgentOptions
        {
            Instructions = instructions,
            Name = name,
            ChatMessageStoreFactory = ctx =>
            {
                // 关键：注入自定义的 LiteDbChatMessageStore
                var messagesCollection = _sessionService.GetMessagesCollection();
                
                // 如果有序列化状态（恢复会话），使用状态中的 SessionId
                // 否则使用当前 sessionId（新会话）
                if (ctx.SerializedState.ValueKind is JsonValueKind.String && 
                    !string.IsNullOrEmpty(ctx.SerializedState.GetString()))
                {
                    return new LiteDbChatMessageStore(messagesCollection, ctx.SerializedState, _storeLogger);
                }
                else
                {
                    return new LiteDbChatMessageStore(messagesCollection, sessionId, _storeLogger);
                }
            }
        });

        _logger?.LogDebug("Created AIAgent '{AgentName}' for session {SessionId}", name, sessionId);
        return agent;
    }

    /// <summary>
    /// 获取或创建 AgentThread（自动加载历史或创建新 Thread）
    /// </summary>
    private AgentThread GetOrCreateThread(string sessionId, AIAgent agent)
    {
        // 尝试从数据库加载
        var thread = _sessionService.LoadThread(sessionId, agent);
        if (thread != null)
        {
            _logger?.LogDebug("Loaded existing thread for session {SessionId}", sessionId);
            return thread;
        }

        // 创建新 Thread
        var newThread = agent.GetNewThread();
        _logger?.LogDebug("Created new thread for session {SessionId}", sessionId);
        return newThread;
    }

    /// <summary>
    /// 发送消息并使用 AgentThread 管理对话（重构版）
    /// </summary>
    public async Task<List<ChatMessageSummary>> SendMessageAsync(string message, string sessionId)
    {
        var summaries = new List<ChatMessageSummary>();

        try
        {
            _logger?.LogDebug("Processing message for session {SessionId}: {Message}", sessionId, message);

            // 1. 检测提到的 Agent
            var mentionedAgent = DetectMentionedAgent(message);
            string agentId = mentionedAgent?.Id ?? "triage";
            string agentName = mentionedAgent?.Name ?? "Assistant";
            string agentAvatar = mentionedAgent?.Avatar ?? "🤖";

            // 2. 为当前会话创建 AIAgent（带 ChatMessageStoreFactory）
            var agent = CreateAgentForSession(sessionId, mentionedAgent);

            // 3. 获取或创建 AgentThread
            var thread = GetOrCreateThread(sessionId, agent);

            // 4. 添加用户消息摘要
            summaries.Add(new ChatMessageSummary
            {
                Content = message,
                IsUser = true,
                Timestamp = DateTime.UtcNow,
                MessageType = "text"
            });

            // 5. 运行对话（消息自动保存到 LiteDbChatMessageStore）
            var agentResponse = await agent.RunAsync(message, thread);
            string response = agentResponse.Text ?? agentResponse.ToString();

            _logger?.LogDebug("Agent {AgentId} responded: {Response}", agentId, response);

            // 6. 添加 Agent 响应摘要
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

            // 7. 检查是否需要生成图片
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

            // 8. 保存 Thread 到数据库（关键步骤！）
            // 注意：消息已经通过 ChatMessageStore 自动保存，这里只保存 Thread 元数据
            _sessionService.SaveThread(sessionId, thread);
            
            _logger?.LogInformation("Saved thread for session {SessionId}", sessionId);

            return summaries;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing message for session {SessionId}", sessionId);
            
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
        var imageKeywords = new[] { "photo", "picture", "image", "show", "look", "see", "here" };
        return imageKeywords.Any(keyword => content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
               && new Random().Next(0, 2) == 0; // 50% 概率
    }

    /// <summary>
    /// 获取会话的对话历史（从 LiteDB messages 集合）
    /// </summary>
    public List<ChatMessageSummary> GetConversationHistory(string sessionId)
    {
        return _sessionService.GetMessageSummaries(sessionId);
    }

    /// <summary>
    /// 清除会话的 Thread 和所有消息
    /// </summary>
    public void ClearConversation(string sessionId)
    {
        _sessionService.ClearSessionMessages(sessionId);
        _logger?.LogInformation("Cleared conversation for session {SessionId}", sessionId);
    }
}
