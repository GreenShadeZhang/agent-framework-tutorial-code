using AgentGroupChat.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Text.Json;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace AgentGroupChat.AgentHost.Services;

/// <summary>
/// Service for managing multi-agent chat with dynamic agent loading
/// 使用 WorkflowManager 支持基于组的 Handoff 模式
/// 从数据库动态加载智能体配置
/// </summary>
public class AgentChatService
{
    private readonly PersistedSessionService _sessionService;
    private readonly McpToolService _mcpToolService;
    private readonly WorkflowManager _workflowManager;
    private readonly AgentRepository _agentRepository;
    private readonly ILogger<AgentChatService>? _logger;
    private readonly ILogger<LiteDbChatMessageStore>? _storeLogger;
    
    // 默认组 ID（向后兼容）
    private const string DefaultGroupId = "default";

    public AgentChatService(
        PersistedSessionService sessionService,
        McpToolService mcpToolService,
        WorkflowManager workflowManager,
        AgentRepository agentRepository,
        ILogger<AgentChatService>? logger = null,
        ILogger<LiteDbChatMessageStore>? storeLogger = null)
    {
        _logger = logger;
        _storeLogger = storeLogger;
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _mcpToolService = mcpToolService ?? throw new ArgumentNullException(nameof(mcpToolService));
        _workflowManager = workflowManager ?? throw new ArgumentNullException(nameof(workflowManager));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));

        _logger?.LogInformation("AgentChatService initialized with WorkflowManager and dynamic agent loading");
    }
    
    public List<AgentProfile> GetAgentProfiles()
    {
        // 从数据库加载启用的智能体
        var persistedAgents = _agentRepository.GetAllEnabled();
        return persistedAgents.Select(a => a.ToAgentProfile()).ToList();
    }

    public AgentProfile? GetAgentProfile(string agentId)
    {
        var agentIdPrefix = agentId.Contains('_') ? agentId.Split('_')[0] : agentId;
        var persistedAgent = _agentRepository.GetById(agentIdPrefix);
        return persistedAgent?.ToAgentProfile();
    }

    /// <summary>
    /// 发送消息并使用指定组的 Handoff Workflow 进行智能路由
    /// </summary>
    public async Task<List<ChatMessageSummary>> SendMessageAsync(
        string message, 
        string sessionId, 
        string? groupId = null)
    {
        var summaries = new List<ChatMessageSummary>();

        try
        {
            // 使用默认组如果未指定
            groupId ??= DefaultGroupId;
            
            _logger?.LogInformation(
                "🚀 Starting SendMessageAsync | SessionId: {SessionId} | GroupId: {GroupId} | Message Length: {Length}", 
                sessionId, groupId, message?.Length ?? 0);
            
            _logger?.LogDebug("📝 User Message: {Message}", message);

            // 1️⃣ 准备消息列表（包含历史消息）
            var messages = new List<AIChatMessage>();

            // 从数据库加载历史消息
            _logger?.LogDebug("📚 Loading message history for session {SessionId}", sessionId);
            var history = _sessionService.GetMessageSummaries(sessionId);
            _logger?.LogInformation("📚 Loaded {Count} historical messages", history.Count);
            
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
            _logger?.LogInformation("📋 Total messages prepared for LLM: {Count} (History: {HistoryCount} + Current: 1)", 
                messages.Count, history.Count);

            // 2️⃣ 获取该组的 Workflow
            _logger?.LogDebug("🔧 Getting workflow for group {GroupId}", groupId);
            Workflow workflow = _workflowManager.GetOrCreateWorkflow(groupId);
            _logger?.LogInformation("✅ Workflow ready for group {GroupId}", groupId);

            // 3️⃣ 运行 Workflow
            _logger?.LogInformation("▶️ Starting workflow execution...");
            await using StreamingRun run = await InProcessExecution.StreamAsync(workflow, messages);
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
            _logger?.LogDebug("📡 Workflow started, watching event stream...");

            // 4️⃣ 处理 WorkflowEvent 流，追踪不同 agent 的执行
            string? currentExecutorId = null;
            ChatMessageSummary? currentSummary = null;

            await foreach (WorkflowEvent evt in run.WatchStreamAsync())
            {
                if (evt is AgentRunUpdateEvent agentUpdate)
                {
                    // ✅ 完全跳过 triage agent 的所有事件处理
                    var executorIdPrefix = agentUpdate.ExecutorId.Contains('_') 
                        ? agentUpdate.ExecutorId.Split('_')[0] 
                        : agentUpdate.ExecutorId;
                    
                    if (executorIdPrefix.Equals("triage", StringComparison.OrdinalIgnoreCase))
                    {
                        // 记录 handoff 调用用于调试
                        if (agentUpdate.Update.Contents.OfType<FunctionCallContent>().FirstOrDefault() is FunctionCallContent triageCall)
                        {
                            _logger?.LogDebug("Triage agent (ID: {ExecutorId}) routing to: {FunctionName} with args: {Args}",
                                agentUpdate.ExecutorId, triageCall.Name, JsonSerializer.Serialize(triageCall.Arguments));
                        }
                        continue;
                    }

                    // 检测到新的 specialist agent 执行
                    if (agentUpdate.ExecutorId != currentExecutorId)
                    {
                        currentExecutorId = agentUpdate.ExecutorId;

                        // 获取 agent 的 profile 信息
                        var profile = GetAgentProfile(currentExecutorId);

                        _logger?.LogDebug("Agent switched to: {ExecutorId} ({AgentName})",
                            currentExecutorId, profile?.Name ?? currentExecutorId);

                        // 创建新的消息摘要
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

                        _logger?.LogDebug("Created summary for specialist agent {AgentId}", currentExecutorId);
                    }

                    if (currentSummary != null)
                    {
                        // ✅ 核心理念：只提取 LLM 生成的文本，让 LLM 自动处理 Tool 结果
                        // FunctionInvokingChatClient 会自动：
                        // 1. 执行 Tool (FunctionCallContent)
                        // 2. 将结果发回 LLM (FunctionResultContent)
                        // 3. LLM 基于结果生成最终文本 (TextContent)
                        
                        // 记录详细的事件信息用于调试和监控
                        if (agentUpdate.Update.Contents.Count > 0)
                        {
                            _logger?.LogDebug(
                                "AgentRunUpdateEvent from {ExecutorId}: Text='{Text}', Contents Count={Count}",
                                currentExecutorId,
                                agentUpdate.Update.Text ?? "(empty)",
                                agentUpdate.Update.Contents.Count);

                            // 记录每个 Content 类型（仅用于调试和监控）
                            foreach (var content in agentUpdate.Update.Contents)
                            {
                                switch (content)
                                {
                                    case FunctionCallContent functionCall:
                                        _logger?.LogInformation(
                                            "🔧 Tool Call | Agent: {AgentId} | Function: {FunctionName} | Args: {Args}",
                                            currentExecutorId,
                                            functionCall.Name,
                                            JsonSerializer.Serialize(functionCall.Arguments));
                                        break;

                                    case FunctionResultContent functionResult:
                                        // 记录 Tool 执行结果（仅用于监控，不手动提取）
                                        var resultPreview = functionResult.Result?.ToString() ?? "(null)";
                                        if (resultPreview.Length > 200)
                                        {
                                            resultPreview = resultPreview.Substring(0, 200) + "...";
                                        }
                                        _logger?.LogInformation(
                                            "✅ Tool Result | Agent: {AgentId} | CallId: {CallId} | Result Preview: {Preview}",
                                            currentExecutorId,
                                            functionResult.CallId,
                                            resultPreview);
                                        break;

                                    case TextContent textContent:
                                        _logger?.LogDebug("📝 Text | Agent: {AgentId} | Content: '{Text}'",
                                            currentExecutorId,
                                            textContent.Text ?? "(empty)");
                                        break;

                                    case DataContent dataContent:
                                        _logger?.LogDebug("📦 Data | Agent: {AgentId}",
                                            currentExecutorId);
                                        break;

                                    default:
                                        _logger?.LogDebug("❓ Unknown | Agent: {AgentId} | Type: {Type}",
                                            currentExecutorId,
                                            content.GetType().Name);
                                        break;
                                }
                            }
                        }

                        // ✅ 只累积 LLM 生成的文本内容
                        // LLM 会自动处理 Tool 结果并生成润色后的文本
                        if (!string.IsNullOrEmpty(agentUpdate.Update.Text))
                        {
                            currentSummary.Content += agentUpdate.Update.Text;
                            _logger?.LogDebug(
                                "📄 Accumulated text for {AgentId}, total length: {Length}",
                                currentExecutorId,
                                currentSummary.Content.Length);
                        }
                    }
                }
                else if (evt is WorkflowOutputEvent output)
                {
                    _logger?.LogDebug("Workflow completed for session {SessionId}", sessionId);
                    break;
                }
            }

            _logger?.LogInformation("Collected {Count} agent responses for session {SessionId}",
                summaries.Count, sessionId);

            // 5️⃣ 手动保存所有消息到 LiteDB
            try
            {
                var currentExecutorIdPrefix = currentExecutorId != null && currentExecutorId.Contains('_')
                    ? currentExecutorId.Split('_')[0]
                    : currentExecutorId;

                if (currentExecutorId != null && 
                    !string.Equals(currentExecutorIdPrefix, "triage", StringComparison.OrdinalIgnoreCase) && 
                    currentSummary != null)
                {
                    var messagesToSave = new List<AIChatMessage>();

                    // 用户消息
                    messagesToSave.Add(new AIChatMessage(ChatRole.User, message)
                    {
                        MessageId = Guid.NewGuid().ToString()
                    });

                    // Agent 响应消息
                    foreach (var summary in summaries.Where(s => !s.IsUser && 
                                                                  s.MessageType == "text" && 
                                                                  !string.IsNullOrWhiteSpace(s.Content)))
                    {
                        messagesToSave.Add(new AIChatMessage(ChatRole.Assistant, summary.Content)
                        {
                            MessageId = Guid.NewGuid().ToString()
                        });
                    }

                    // 保存到 LiteDB
                    if (messagesToSave.Count > 0)
                    {
                        var messageStore = new LiteDbChatMessageStore(
                            _sessionService.GetMessagesCollection(),
                            sessionId,
                            currentExecutorId,
                            currentSummary.AgentName,
                            currentSummary.AgentAvatar,
                            _storeLogger);

                        await messageStore.AddMessagesAsync(messagesToSave);

                        _logger?.LogInformation("Saved {Count} messages to LiteDB for session {SessionId} (Agent: {AgentId})",
                            messagesToSave.Count, sessionId, currentExecutorId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving messages for session {SessionId}", sessionId);
            }

            // 6️⃣ 过滤掉 triage agent 消息和空消息
            var filteredSummaries = summaries.Where(s =>
            {
                var agentIdPrefix = s.AgentId.Contains('_') ? s.AgentId.Split('_')[0] : s.AgentId;
                return !string.Equals(agentIdPrefix, "triage", StringComparison.OrdinalIgnoreCase) &&
                       !string.IsNullOrWhiteSpace(s.Content);
            }).ToList();

            _logger?.LogInformation("Returning {Count} filtered responses for session {SessionId}",
                filteredSummaries.Count, sessionId);

            return filteredSummaries;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, 
                "🔴 Critical Error in SendMessageAsync | SessionId: {SessionId} | GroupId: {GroupId} | Exception Type: {ExceptionType} | Message: {ErrorMessage} | StackTrace: {StackTrace}",
                sessionId, groupId, ex.GetType().FullName, ex.Message, ex.StackTrace);

            // Log inner exceptions
            var innerEx = ex.InnerException;
            var depth = 1;
            while (innerEx != null)
            {
                _logger?.LogError(
                    "  ↳ Inner Exception [{Depth}] | Type: {ExceptionType} | Message: {ErrorMessage}",
                    depth, innerEx.GetType().FullName, innerEx.Message);
                innerEx = innerEx.InnerException;
                depth++;
            }

            summaries.Add(new ChatMessageSummary
            {
                AgentId = "system",
                AgentName = "System",
                AgentAvatar = "⚠️",
                Content = $"Error: {ex.Message}\n\nType: {ex.GetType().Name}\n\n请查看服务器日志获取详细信息。",
                IsUser = false,
                MessageType = "error",
                Timestamp = DateTime.UtcNow
            });

            return summaries;
        }
    }

    /// <summary>
    /// 获取会话的对话历史（从 LiteDB messages 集合）
    /// </summary>
    public List<ChatMessageSummary> GetConversationHistory(string sessionId)
    {
        return _sessionService.GetMessageSummaries(sessionId);
    }

    /// <summary>
    /// 清除会话的所有消息
    /// </summary>
    public void ClearConversation(string sessionId)
    {
        _sessionService.ClearSessionMessages(sessionId);
        _logger?.LogInformation("Cleared conversation for session {SessionId}", sessionId);
    }
}
