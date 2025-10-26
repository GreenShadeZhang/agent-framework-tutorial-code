# 会话持久化分析与重构方案

## 📋 目录
- [官方示例分析](#官方示例分析)
- [当前项目实现分析](#当前项目实现分析)
- [主要差异对比](#主要差异对比)
- [LiteDB重构方案](#litedb重构方案)
- [实施步骤](#实施步骤)

---

## 官方示例分析

### 核心实现 (Agent_Step06_PersistedConversations)

官方示例展示了如何使用 Agent Framework 的内置持久化机制：

```csharp
// 1. 创建 Agent 和 Thread
AIAgent agent = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureCliCredential())
    .GetChatClient(deploymentName)
    .CreateAIAgent(instructions: "You are good at telling jokes.", name: "Joker");

AgentThread thread = agent.GetNewThread();

// 2. 运行对话
await agent.RunAsync("Tell me a joke about a pirate.", thread);

// 3. 序列化 Thread 状态
JsonElement serializedThread = thread.Serialize();

// 4. 保存到文件
await File.WriteAllTextAsync(tempFilePath, 
    JsonSerializer.Serialize(serializedThread));

// 5. 从文件加载
JsonElement reloadedSerializedThread = 
    JsonSerializer.Deserialize<JsonElement>(
        await File.ReadAllTextAsync(tempFilePath));

// 6. 反序列化 Thread
AgentThread resumedThread = agent.DeserializeThread(reloadedSerializedThread);

// 7. 继续对话
await agent.RunAsync("Now tell the same joke...", resumedThread);
```

### 关键特性

1. **使用 `AgentThread` 对象**
   - Framework 原生的对话上下文容器
   - 自动管理消息历史和状态
   - 内置序列化/反序列化支持

2. **序列化机制**
   - `thread.Serialize()` → `JsonElement`
   - 完整保存对话状态（消息、元数据、上下文）
   - 使用标准 JSON 格式

3. **存储方式**
   - 示例使用文件系统
   - 可扩展到任何存储后端（数据库、云存储等）

4. **对话恢复**
   - `agent.DeserializeThread(jsonElement)` 
   - 完整恢复对话上下文
   - 无缝继续对话

---

## 当前项目实现分析

### 架构概览

当前项目使用自定义的持久化方案：

```
AgentGroupChat.AgentHost
├── Models/
│   ├── ChatMessage.cs       # 自定义消息模型
│   └── ChatSession.cs       # 自定义会话模型
├── Services/
│   ├── SessionService.cs    # LiteDB 持久化服务
│   └── AgentChatService.cs  # Agent 管理服务
└── Program.cs               # API 端点
```

### SessionService 实现

```csharp
public class SessionService : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<ChatSession> _sessions;

    public SessionService()
    {
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        Directory.CreateDirectory(dbPath);
        _database = new LiteDatabase(Path.Combine(dbPath, "sessions.db"));
        _sessions = _database.GetCollection<ChatSession>("sessions");
    }

    public List<ChatSession> GetAllSessions() { }
    public ChatSession? GetSession(string id) { }
    public ChatSession CreateSession(string? name = null) { }
    public void UpdateSession(ChatSession session) { }
    public void DeleteSession(string id) { }
}
```

**优点：**
- ✅ 使用 LiteDB 轻量级数据库
- ✅ 支持会话列表管理
- ✅ 实现基本 CRUD 操作
- ✅ 自动管理数据库生命周期

**缺点：**
- ❌ 存储自定义 `ChatMessage` 而非 Agent Framework 原生消息
- ❌ 无法保存 Agent 内部状态
- ❌ 不支持 `AgentThread` 的完整上下文
- ❌ 对话恢复时需要手动重建历史

### ChatSession 模型

```csharp
public class ChatSession
{
    public string Id { get; set; }
    public string Name { get; set; }
    public List<ChatMessage> Messages { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
}
```

**问题：**
- 只存储消息内容，丢失了 Agent Framework 的内部状态
- 无法保存工具调用、handoff 状态等元数据

### AgentChatService 实现

```csharp
public async Task<List<Models.ChatMessage>> SendMessageAsync(
    string message, 
    List<Models.ChatMessage> history)
{
    // 将历史转换为 ChatMessage
    var chatMessages = new List<AIChatMessage>
    {
        new(ChatRole.User, message)
    };

    // 运行 workflow（不保存 thread 状态）
    await using StreamingRun run = await InProcessExecution.StreamAsync(
        _workflow, chatMessages);
    
    // 处理响应...
}
```

**问题：**
- ❌ 每次调用只传入新消息，不利用历史上下文
- ❌ 没有使用 `AgentThread` 管理对话状态
- ❌ Workflow 状态无法持久化

---

## 主要差异对比

| 方面 | 官方示例 | 当前项目 |
|------|---------|---------|
| **对话容器** | `AgentThread` | 自定义 `ChatSession` |
| **消息格式** | Framework 原生 `ChatMessage` | 自定义 `ChatMessage` |
| **序列化** | `thread.Serialize()` | JSON 序列化自定义模型 |
| **存储** | 文件系统（示例） | LiteDB 数据库 |
| **状态保存** | 完整（消息+元数据+上下文） | 仅消息内容 |
| **对话恢复** | `DeserializeThread()` | 手动重建历史 |
| **Workflow 支持** | 内置支持 | 需要自行管理 |
| **工具调用** | 自动保存 | 无法保存 |
| **Handoff 状态** | 自动保存 | 无法保存 |

---

## LiteDB重构方案

### 方案概述

结合官方示例的 `AgentThread` 机制和当前项目的 LiteDB 存储，实现完整的会话持久化：

```
AgentThread (序列化) → JsonElement → LiteDB → JsonElement (反序列化) → AgentThread
```

### 新的数据模型

```csharp
namespace AgentGroupChat.Models;

/// <summary>
/// 持久化的会话模型，存储 AgentThread 序列化数据
/// </summary>
public class PersistedChatSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = $"Session {DateTime.Now:yyyy-MM-dd HH:mm}";
    
    // 存储序列化的 AgentThread 数据
    public string ThreadData { get; set; } = string.Empty;
    
    // 用于显示的消息摘要（可选，用于列表展示）
    public List<ChatMessageSummary> MessageSummaries { get; set; } = new();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public int MessageCount { get; set; } = 0;
}

/// <summary>
/// 消息摘要，用于快速展示列表
/// </summary>
public class ChatMessageSummary
{
    public string AgentId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
```

### 重构后的 SessionService

```csharp
using System.Text.Json;
using AgentGroupChat.Models;
using LiteDB;
using Microsoft.Agents.AI;

namespace AgentGroupChat.AgentHost.Services;

/// <summary>
/// 基于 LiteDB 的会话持久化服务，支持 AgentThread 序列化
/// </summary>
public class PersistedSessionService : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<PersistedChatSession> _sessions;

    public PersistedSessionService()
    {
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        Directory.CreateDirectory(dbPath);
        _database = new LiteDatabase(Path.Combine(dbPath, "sessions.db"));
        _sessions = _database.GetCollection<PersistedChatSession>("sessions");
        _sessions.EnsureIndex(x => x.Id);
        _sessions.EnsureIndex(x => x.LastUpdated);
    }

    /// <summary>
    /// 获取所有会话（不包含完整 Thread 数据）
    /// </summary>
    public List<PersistedChatSession> GetAllSessions()
    {
        return _sessions.FindAll()
            .OrderByDescending(s => s.LastUpdated)
            .ToList();
    }

    /// <summary>
    /// 获取特定会话（包含完整 Thread 数据）
    /// </summary>
    public PersistedChatSession? GetSession(string id)
    {
        return _sessions.FindById(id);
    }

    /// <summary>
    /// 创建新会话
    /// </summary>
    public PersistedChatSession CreateSession(string? name = null)
    {
        var session = new PersistedChatSession
        {
            Id = Guid.NewGuid().ToString(),
            Name = name ?? $"Session {DateTime.Now:yyyy-MM-dd HH:mm}",
            ThreadData = string.Empty // 空 thread，首次对话时初始化
        };
        _sessions.Insert(session);
        return session;
    }

    /// <summary>
    /// 保存 AgentThread 到会话
    /// </summary>
    public void SaveThread(string sessionId, AgentThread thread, 
        List<ChatMessageSummary>? summaries = null)
    {
        var session = _sessions.FindById(sessionId);
        if (session == null)
            throw new InvalidOperationException($"Session {sessionId} not found");

        // 序列化 AgentThread
        JsonElement serializedThread = thread.Serialize();
        session.ThreadData = JsonSerializer.Serialize(serializedThread);
        
        // 更新摘要（如果提供）
        if (summaries != null)
        {
            session.MessageSummaries = summaries;
            session.MessageCount = summaries.Count;
        }
        
        session.LastUpdated = DateTime.UtcNow;
        _sessions.Update(session);
    }

    /// <summary>
    /// 从会话加载 AgentThread
    /// </summary>
    public AgentThread? LoadThread(string sessionId, AIAgent agent)
    {
        var session = _sessions.FindById(sessionId);
        if (session == null || string.IsNullOrEmpty(session.ThreadData))
            return null;

        // 反序列化 AgentThread
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(session.ThreadData);
        return agent.DeserializeThread(jsonElement);
    }

    /// <summary>
    /// 更新会话元数据（名称等）
    /// </summary>
    public void UpdateSessionMetadata(string sessionId, string? name = null)
    {
        var session = _sessions.FindById(sessionId);
        if (session == null)
            throw new InvalidOperationException($"Session {sessionId} not found");

        if (name != null)
            session.Name = name;
        
        session.LastUpdated = DateTime.UtcNow;
        _sessions.Update(session);
    }

    /// <summary>
    /// 删除会话
    /// </summary>
    public void DeleteSession(string id)
    {
        _sessions.Delete(id);
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}
```

### 重构后的 AgentChatService

```csharp
namespace AgentGroupChat.AgentHost.Services;

public class AgentChatService
{
    private readonly IChatClient _chatClient;
    private readonly AIAgent _triageAgent; // 主 Agent
    private readonly Dictionary<string, AIAgent> _agents;
    
    public AgentChatService(IConfiguration configuration)
    {
        // 初始化 agents...
        
        // 创建主 triage agent（用于管理 thread）
        _triageAgent = _chatClient.CreateAIAgent(
            instructions: "You are a triage agent...",
            name: "Triage"
        );
    }

    /// <summary>
    /// 发送消息，支持会话持久化
    /// </summary>
    public async Task<List<ChatMessageSummary>> SendMessageAsync(
        string message, 
        string sessionId,
        PersistedSessionService sessionService)
    {
        var summaries = new List<ChatMessageSummary>();

        try
        {
            // 1. 加载或创建 AgentThread
            AgentThread thread = sessionService.LoadThread(sessionId, _triageAgent) 
                              ?? _triageAgent.GetNewThread();

            // 2. 运行对话
            var response = await _triageAgent.RunAsync(message, thread);

            // 3. 处理响应，生成摘要
            summaries.Add(new ChatMessageSummary
            {
                Content = message,
                IsUser = true,
                Timestamp = DateTime.UtcNow
            });

            summaries.Add(new ChatMessageSummary
            {
                AgentId = "triage",
                AgentName = "Triage",
                Content = response,
                IsUser = false,
                Timestamp = DateTime.UtcNow
            });

            // 4. 保存 Thread 到 LiteDB
            sessionService.SaveThread(sessionId, thread, summaries);

            return summaries;
        }
        catch (Exception ex)
        {
            // 错误处理...
            throw;
        }
    }

    /// <summary>
    /// 使用 Workflow 的版本（支持多 Agent handoff）
    /// </summary>
    public async Task<List<ChatMessageSummary>> SendMessageWithWorkflowAsync(
        string message,
        string sessionId,
        PersistedSessionService sessionService)
    {
        var summaries = new List<ChatMessageSummary>();

        try
        {
            // 1. 加载已有的消息历史作为上下文
            var session = sessionService.GetSession(sessionId);
            var chatMessages = new List<AIChatMessage>();

            // 如果有历史，可以添加为上下文（可选）
            if (session != null && session.MessageSummaries.Any())
            {
                // 添加最近的几条消息作为上下文
                var recentMessages = session.MessageSummaries.TakeLast(10);
                foreach (var msg in recentMessages)
                {
                    chatMessages.Add(new AIChatMessage(
                        msg.IsUser ? ChatRole.User : ChatRole.Assistant,
                        msg.Content
                    ));
                }
            }

            // 添加新消息
            chatMessages.Add(new AIChatMessage(ChatRole.User, message));

            // 2. 运行 Workflow
            await using StreamingRun run = await InProcessExecution.StreamAsync(
                _workflow, chatMessages);
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

            // 3. 处理事件流，收集响应
            await foreach (WorkflowEvent evt in run.WatchStreamAsync())
            {
                if (evt is AgentRunUpdateEvent updateEvent)
                {
                    // 收集 Agent 响应...
                }
            }

            // 4. 保存到 LiteDB（使用摘要模式，因为 Workflow 不直接支持 Thread）
            // 注意：Workflow 模式可能需要不同的持久化策略
            sessionService.SaveThread(sessionId, null, summaries);

            return summaries;
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
```

### 更新 API 端点

```csharp
// Program.cs

// 注册服务
builder.Services.AddSingleton<PersistedSessionService>();
builder.Services.AddSingleton<AgentChatService>();

// 发送消息端点
app.MapPost("/api/chat", async (
    ChatRequest request, 
    AgentChatService agentService, 
    PersistedSessionService sessionService) =>
{
    if (string.IsNullOrWhiteSpace(request.Message) || 
        string.IsNullOrWhiteSpace(request.SessionId))
        return Results.BadRequest("Message and SessionId are required");

    var session = sessionService.GetSession(request.SessionId);
    if (session == null)
        return Results.NotFound("Session not found");

    // 发送消息并自动持久化
    var responses = await agentService.SendMessageAsync(
        request.Message, 
        request.SessionId,
        sessionService);

    return Results.Ok(responses);
});
```

---

## 实施步骤

### Phase 1: 数据模型迁移

1. **创建新模型**
   - [ ] 创建 `PersistedChatSession.cs`
   - [ ] 创建 `ChatMessageSummary.cs`
   - [ ] 保留原有 `ChatMessage.cs` 用于 UI 展示

2. **数据库迁移**
   - [ ] 创建迁移脚本（如需要）
   - [ ] 测试新模型的 LiteDB 存储

### Phase 2: 服务重构

3. **重构 SessionService**
   - [ ] 重命名为 `PersistedSessionService`
   - [ ] 实现 `SaveThread()` 方法
   - [ ] 实现 `LoadThread()` 方法
   - [ ] 添加索引优化查询

4. **重构 AgentChatService**
   - [ ] 修改 `SendMessageAsync()` 支持 Thread
   - [ ] 集成 Thread 序列化/反序列化
   - [ ] 处理 Workflow 与 Thread 的兼容性

### Phase 3: API 更新

5. **更新端点**
   - [ ] 修改 `/api/chat` 端点
   - [ ] 更新返回数据格式
   - [ ] 保持向后兼容性（如需要）

### Phase 4: 测试

6. **单元测试**
   - [ ] SessionService 序列化测试
   - [ ] Thread 恢复测试
   - [ ] 并发访问测试

7. **集成测试**
   - [ ] 端到端对话测试
   - [ ] 会话恢复测试
   - [ ] 多会话管理测试

### Phase 5: 优化

8. **性能优化**
   - [ ] 添加缓存层（内存缓存热会话）
   - [ ] 优化大型 Thread 的序列化
   - [ ] 实现自动清理旧会话

9. **监控和日志**
   - [ ] 添加持久化性能监控
   - [ ] 记录序列化错误
   - [ ] 跟踪会话大小

---

## 高级特性建议

### 1. 混合持久化策略

```csharp
public class HybridSessionService
{
    // 热数据：内存缓存
    private readonly Dictionary<string, AgentThread> _hotThreads = new();
    
    // 冷数据：LiteDB
    private readonly PersistedSessionService _persistedService;
    
    public AgentThread GetOrLoadThread(string sessionId, AIAgent agent)
    {
        // 1. 先查缓存
        if (_hotThreads.TryGetValue(sessionId, out var cached))
            return cached;
        
        // 2. 从数据库加载
        var thread = _persistedService.LoadThread(sessionId, agent);
        
        // 3. 加入缓存
        if (thread != null)
            _hotThreads[sessionId] = thread;
        
        return thread ?? agent.GetNewThread();
    }
}
```

### 2. 自动保存策略

```csharp
public class AutoSaveSessionService
{
    private readonly Timer _autoSaveTimer;
    
    public AutoSaveSessionService()
    {
        // 每 30 秒自动保存活跃会话
        _autoSaveTimer = new Timer(AutoSave, null, 
            TimeSpan.FromSeconds(30), 
            TimeSpan.FromSeconds(30));
    }
    
    private void AutoSave(object? state)
    {
        foreach (var (sessionId, thread) in _hotThreads)
        {
            _persistedService.SaveThread(sessionId, thread);
        }
    }
}
```

### 3. 版本控制

```csharp
public class VersionedSession
{
    public string Id { get; set; }
    public int Version { get; set; } // 序列化版本
    public string ThreadData { get; set; }
    
    // 支持向后兼容
    public bool IsCompatibleWith(int currentVersion) 
    {
        return Version <= currentVersion;
    }
}
```

---

## 注意事项

### ⚠️ 潜在问题

1. **Workflow 与 AgentThread 的兼容性**
   - Workflow 使用的是流式执行模型
   - 可能需要为 Workflow 单独设计持久化方案

2. **序列化数据大小**
   - 长对话的 Thread 数据会很大
   - 考虑实现消息修剪或分页

3. **并发安全**
   - 多个请求同时修改同一会话
   - 需要实现乐观锁或悲观锁

4. **数据迁移**
   - 现有会话数据需要迁移
   - 提供向后兼容路径

### ✅ 最佳实践

1. **渐进式重构**
   - 保留原有 API，添加新端点
   - 逐步迁移功能

2. **测试覆盖**
   - 序列化/反序列化的往返测试
   - 边界条件测试

3. **错误处理**
   - 序列化失败的降级策略
   - 数据损坏的恢复机制

4. **文档更新**
   - API 文档
   - 数据模型文档
   - 迁移指南

---

## 总结

### 核心改进

1. **使用 `AgentThread`**：利用 Framework 原生的对话管理
2. **完整状态保存**：不仅是消息，还包括所有元数据
3. **LiteDB 集成**：保持轻量级本地存储的优势
4. **灵活架构**：支持缓存、自动保存等高级特性

### 收益

- ✅ 完整的对话上下文恢复
- ✅ 支持工具调用历史
- ✅ 更好的 Agent 状态管理
- ✅ 与 Framework 更新同步
- ✅ 可扩展的持久化架构

### 下一步

建议从 **Phase 1** 开始，创建新的数据模型，然后逐步迁移服务和 API。需要帮助实施具体的代码吗？
