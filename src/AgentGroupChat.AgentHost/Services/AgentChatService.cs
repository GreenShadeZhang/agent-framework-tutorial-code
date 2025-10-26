using AgentGroupChat.Models;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Text.Json;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace AgentGroupChat.AgentHost.Services;

/// <summary>
/// Service for managing multi-agent chat with TRUE handoff workflow support
/// 使用 AgentWorkflowBuilder 实现真正的 Handoff 模式（参考官方示例）
/// 集成 LiteDbChatMessageStore 进行消息持久化
/// 参考：https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/GettingStarted/Workflows/_Foundational/04_AgentWorkflowPatterns/Program.cs
/// </summary>
public class AgentChatService
{
    private readonly IChatClient _chatClient;
    private readonly List<AgentProfile> _agentProfiles;
    private readonly Workflow _handoffWorkflow; // ✅ 单例 workflow，在构造函数中初始化
    private readonly PersistedSessionService _sessionService;
    private readonly ImageGenerationTool _imageTool;
    private readonly McpToolService _mcpToolService;
    private readonly ILogger<AgentChatService>? _logger;
    private readonly ILogger<LiteDbChatMessageStore>? _storeLogger;

    public AgentChatService(
        IConfiguration configuration,
        PersistedSessionService sessionService,
        McpToolService mcpToolService,
        ILogger<AgentChatService>? logger = null,
        ILogger<LiteDbChatMessageStore>? storeLogger = null)
    {
        _logger = logger;
        _storeLogger = storeLogger;
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _mcpToolService = mcpToolService ?? throw new ArgumentNullException(nameof(mcpToolService));

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

        // ✅ 在构造函数中创建一次 handoff workflow（性能优化：避免每次消息都创建）
        _handoffWorkflow = CreateHandoffWorkflow();
        _logger?.LogInformation("Handoff workflow initialized successfully with {AgentCount} agents",
            _agentProfiles.Count + 1); // +1 for triage agent
    }
    public List<AgentProfile> GetAgentProfiles() => _agentProfiles;

    public AgentProfile? GetAgentProfile(string agentId)
    {
        var agentIdPrefix = agentId.Contains('_') ? agentId.Split('_')[0] : agentId;
        return _agentProfiles.FirstOrDefault(a => a.Id.Equals(agentIdPrefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 创建真正的 Handoff Workflow（官方推荐方式）
    /// 使用 AgentWorkflowBuilder 构建 triage agent 和多个 specialist agents
    /// 实现智能路由和 agent 切换
    /// 注意：workflow 是无状态的，可以在多个会话中安全复用
    /// </summary>
    private Workflow CreateHandoffWorkflow()
    {
        // 获取所有可用的 MCP 工具
        var mcpTools = _mcpToolService.GetAllTools().ToList();

        _logger?.LogDebug("Creating handoff workflow with {ToolCount} MCP tools", mcpTools.Count);

        // 1️⃣ 动态生成 Triage Agent 的指令（基于实际的 agent profiles）
        var specialistDescriptions = string.Join("\n", _agentProfiles.Select(profile =>
            $"- {profile.Id}: {profile.Description} (Personality: {profile.Personality})"
        ));

        var triageInstructions =
            "You are a smart routing agent that analyzes user messages and decides which specialist agent should respond. " +
            "IMPORTANT: You MUST ALWAYS use the handoff function to delegate to one of the specialist agents. NEVER respond directly. " +
            "\n\nAvailable specialist agents:\n" +
            specialistDescriptions +
            "\n\nAnalyze the user's message and handoff to the most appropriate specialist. " +
            "Consider the topic, keywords, tone, and context when making your decision. " +
            "Choose the specialist whose personality and expertise best match the user's needs.";

        // 创建 Triage Agent（智能路由器）
        var triageAgent = new ChatClientAgent(
            _chatClient,
            instructions: triageInstructions,
            name: "triage",
            description: "Smart router that delegates to specialist agents");

        _logger?.LogDebug("Triage agent instructions: {Instructions}", triageInstructions);

        // 2️⃣ 创建所有 Specialist Agents
        var specialistAgents = _agentProfiles.Select(profile =>
            new ChatClientAgent(
                _chatClient,
                instructions: profile.SystemPrompt +
                    "\n\nIMPORTANT: If the user asks about something outside your expertise, " +
                    "you can suggest they ask another agent, but still provide a helpful response.",
                name: profile.Id,
                description: profile.Description)
        ).ToList();

        _logger?.LogInformation("Created {SpecialistCount} specialist agents: {AgentNames}",
            specialistAgents.Count,
            string.Join(", ", specialistAgents.Select(a => a.Name)));

        // 3️⃣ 使用 AgentWorkflowBuilder 构建 Handoff Workflow
        var builder = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent);

        // 配置 handoff 路径：triage → specialists
        builder.WithHandoffs(triageAgent, specialistAgents).WithHandoffs(specialistAgents, triageAgent);

        var workflow = builder.Build();

        _logger?.LogInformation("Handoff workflow created successfully");

        return workflow;
    }

    /// <summary>
    /// 发送消息并使用 Handoff Workflow 进行智能路由（重构版）
    /// 使用官方推荐的 AgentWorkflowBuilder + StreamingRun + WorkflowEvent 处理
    /// </summary>
    public async Task<List<ChatMessageSummary>> SendMessageAsync(string message, string sessionId)
    {
        var summaries = new List<ChatMessageSummary>();

        try
        {
            _logger?.LogDebug("Processing message for session {SessionId}: {Message}", sessionId, message);

            // 1️⃣ 添加用户消息摘要
            summaries.Add(new ChatMessageSummary
            {
                Content = message,
                IsUser = true,
                Timestamp = DateTime.UtcNow,
                MessageType = "text"
            });

            // 2️⃣ 准备消息列表（包含历史消息）
            var messages = new List<AIChatMessage>();

            // 从数据库加载历史消息
            var history = _sessionService.GetMessageSummaries(sessionId);
            foreach (var historyMsg in history)
            {
                if (historyMsg.IsUser)
                {
                    messages.Add(new AIChatMessage(ChatRole.User, historyMsg.Content));
                }
                else
                {
                    messages.Add(new AIChatMessage(ChatRole.Assistant, historyMsg.Content));
                }
            }

            // 添加当前用户消息
            messages.Add(new AIChatMessage(ChatRole.User, message));

            // 3️⃣ 运行 Workflow（✅ 复用预创建的单例 workflow，零开销）
            await using StreamingRun run = await InProcessExecution.StreamAsync(_handoffWorkflow, messages);
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

            // 4️⃣ 处理 WorkflowEvent 流，追踪不同 agent 的执行
            string? currentExecutorId = null;
            ChatMessageSummary? currentSummary = null;

            await foreach (WorkflowEvent evt in run.WatchStreamAsync())
            {
                if (evt is AgentRunUpdateEvent agentUpdate)
                {
                    // 检测到新的 agent 执行
                    if (agentUpdate.ExecutorId != currentExecutorId)
                    {
                        currentExecutorId = agentUpdate.ExecutorId;

                        // 获取 agent 的 profile 信息
                        var profile = GetAgentProfile(currentExecutorId);

                        _logger?.LogDebug("Agent switched to: {ExecutorId} ({AgentName})",
                            currentExecutorId, profile?.Name ?? currentExecutorId);

                        // 创建新的消息摘要（跳过 triage agent 的输出，它不应该有输出）
                        if (currentExecutorId != "triage")
                        {
                            currentSummary = new ChatMessageSummary
                            {
                                AgentId = currentExecutorId,
                                AgentName = profile?.Name ?? currentExecutorId,
                                AgentAvatar = profile?.Avatar ?? "🤖",
                                Content = "",
                                IsUser = false,
                                Timestamp = DateTime.UtcNow,
                                MessageType = "text"
                            };
                            summaries.Add(currentSummary);
                        }
                    }

                    // 追加文本内容（仅当不是 triage agent 时）
                    if (currentExecutorId != "triage" && currentSummary != null)
                    {
                        currentSummary.Content += agentUpdate.Update.Text;
                    }

                    // 检测函数调用（例如 handoff）
                    if (agentUpdate.Update.Contents.OfType<FunctionCallContent>().FirstOrDefault() is FunctionCallContent call)
                    {
                        _logger?.LogDebug("Agent {ExecutorId} calling function: {FunctionName} with args: {Args}",
                            currentExecutorId, call.Name, JsonSerializer.Serialize(call.Arguments));
                    }
                }
                else if (evt is WorkflowOutputEvent output)
                {
                    _logger?.LogDebug("Workflow completed for session {SessionId}", sessionId);
                    break;
                }
            }

            // 5️⃣ 检查是否需要生成图片（基于最后一个 agent 的响应）
            if (currentSummary != null && ShouldGenerateImage(currentSummary.Content))
            {
                try
                {
                    var profile = GetAgentProfile(currentExecutorId!);
                    var imageUrl = await _imageTool.GenerateImage($"{profile?.Personality ?? "casual"} scene");

                    summaries.Add(new ChatMessageSummary
                    {
                        AgentId = currentExecutorId!,
                        AgentName = currentSummary.AgentName,
                        AgentAvatar = currentSummary.AgentAvatar,
                        Content = "Here's a photo I'd like to share! 📸",
                        ImageUrl = imageUrl,
                        IsUser = false,
                        Timestamp = DateTime.UtcNow,
                        MessageType = "image"
                    });

                    _logger?.LogDebug("Generated image for agent {AgentId}", currentExecutorId);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to generate image for agent {AgentId}", currentExecutorId);
                }
            }

            // 6️⃣ 手动保存所有消息到 LiteDB
            try
            {
                var messagesToSave = new List<AIChatMessage>();

                // 用户消息
                messagesToSave.Add(new AIChatMessage(ChatRole.User, message)
                {
                    MessageId = Guid.NewGuid().ToString()
                });

                // Agent 响应消息
                foreach (var summary in summaries.Where(s => !s.IsUser && s.MessageType == "text"))
                {
                    messagesToSave.Add(new AIChatMessage(ChatRole.Assistant, summary.Content)
                    {
                        MessageId = Guid.NewGuid().ToString()
                    });
                }

                // 保存到 LiteDB
                var messageStore = new LiteDbChatMessageStore(
                    _sessionService.GetMessagesCollection(),
                    sessionId,
                    currentExecutorId ?? "assistant",
                    currentSummary?.AgentName ?? "Assistant",
                    currentSummary?.AgentAvatar ?? "🤖",
                    _storeLogger);

                await messageStore.AddMessagesAsync(messagesToSave);

                _logger?.LogInformation("Saved {Count} messages to LiteDB for session {SessionId}",
                    messagesToSave.Count, sessionId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving messages for session {SessionId}", sessionId);
            }

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
