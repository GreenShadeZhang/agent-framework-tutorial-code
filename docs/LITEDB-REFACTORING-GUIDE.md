# LiteDB 消息持久化重构 - 使用指南

## 📚 快速导航

- [重构总结](./LITEDB-REFACTORING-SUMMARY.md) - 完整的重构说明和架构设计
- [对比文档](./LITEDB-REFACTORING-COMPARISON.md) - 新旧架构的详细对比

---

## 🎯 重构核心思想

基于 Microsoft Agent Framework 的官方最佳实践，将**消息存储**和 **Thread 状态**分离：

1. **消息** → 独立的 `messages` 集合（LiteDB）
2. **Thread 元数据** → `sessions` 集合（只保存 SessionId）
3. **ChatMessageStore** → 自动管理消息的读写

---

## 🚀 快速开始

### **步骤 1: 初始化服务**

```csharp
// Program.cs 或 Startup.cs

var builder = WebApplication.CreateBuilder(args);

// 注册 PersistedSessionService（管理 sessions 和 messages 两个集合）
builder.Services.AddSingleton<PersistedSessionService>(sp =>
{
    var logger = sp.GetService<ILogger<PersistedSessionService>>();
    return new PersistedSessionService(logger);
});

// 注册新版 AgentChatService（带 ChatMessageStoreFactory）
builder.Services.AddSingleton<AgentChatServiceRefactored>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var sessionService = sp.GetRequiredService<PersistedSessionService>();
    var logger = sp.GetService<ILogger<AgentChatServiceRefactored>>();
    var storeLogger = sp.GetService<ILogger<LiteDbChatMessageStore>>();
    
    return new AgentChatServiceRefactored(
        configuration, 
        sessionService, 
        logger, 
        storeLogger
    );
});

var app = builder.Build();
```

---

### **步骤 2: 发送消息**

```csharp
// 在你的 Controller 或 Service 中

public class ChatController : ControllerBase
{
    private readonly AgentChatServiceRefactored _chatService;
    private readonly PersistedSessionService _sessionService;

    public ChatController(
        AgentChatServiceRefactored chatService,
        PersistedSessionService sessionService)
    {
        _chatService = chatService;
        _sessionService = sessionService;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage(
        [FromBody] SendMessageRequest request)
    {
        // 1. 确保会话存在
        var session = _sessionService.GetSession(request.SessionId);
        if (session == null)
        {
            session = _sessionService.CreateSession();
        }

        // 2. 发送消息（消息自动通过 ChatMessageStore 保存）
        var summaries = await _chatService.SendMessageAsync(
            request.Message, 
            session.Id
        );

        return Ok(new
        {
            SessionId = session.Id,
            Messages = summaries
        });
    }
}
```

**关键点：**
- ✅ 消息通过 `LiteDbChatMessageStore` 自动保存到 `messages` 集合
- ✅ Thread 序列化时只保存 `SessionId`（非常小）
- ✅ 不需要手动构建 `MessageSummaries`

---

### **步骤 3: 加载历史消息**

```csharp
[HttpGet("history/{sessionId}")]
public IActionResult GetHistory(string sessionId)
{
    // 从 messages 集合直接查询（带索引，性能高）
    var history = _chatService.GetConversationHistory(sessionId);
    
    return Ok(history);
}
```

---

### **步骤 4: 管理会话**

```csharp
// 获取所有会话列表
[HttpGet("sessions")]
public IActionResult GetAllSessions()
{
    var sessions = _sessionService.GetAllSessions();
    
    // sessions 中的 ThreadData 很小（只有 SessionId），不会影响性能
    return Ok(sessions);
}

// 创建新会话
[HttpPost("sessions")]
public IActionResult CreateSession([FromBody] CreateSessionRequest request)
{
    var session = _sessionService.CreateSession(request.Name);
    return Ok(session);
}

// 删除会话
[HttpDelete("sessions/{sessionId}")]
public IActionResult DeleteSession(string sessionId)
{
    _sessionService.DeleteSession(sessionId);
    
    // ⚠️ 注意：还需要删除对应的消息
    _sessionService.ClearSessionMessages(sessionId);
    
    return NoContent();
}

// 清空会话消息（保留会话）
[HttpPost("sessions/{sessionId}/clear")]
public IActionResult ClearSession(string sessionId)
{
    _chatService.ClearConversation(sessionId);
    return Ok();
}
```

---

## 🔍 内部机制详解

### **消息自动保存流程**

```
1. 用户发送消息
   ↓
2. CreateAgentForSession(sessionId, profile)
   ↓
   创建 AIAgent，注入 ChatMessageStoreFactory:
   ↓
   ChatMessageStoreFactory = ctx => 
   {
       return new LiteDbChatMessageStore(
           messagesCollection,  // ← LiteDB messages 集合
           sessionId,           // ← 当前会话 ID
           logger
       );
   }
   ↓
3. GetOrCreateThread(sessionId, agent)
   ↓
   - 如果有历史：agent.DeserializeThread(serializedState)
     → ChatMessageStore 从序列化状态恢复 SessionId
     → GetMessagesAsync() 从 messages 集合加载历史
   - 如果是新会话：agent.GetNewThread()
   ↓
4. agent.RunAsync(message, thread)
   ↓
   Agent Framework 内部调用:
   - ChatMessageStore.AddMessagesAsync([用户消息])  ← 自动保存
   - 生成 AI 响应
   - ChatMessageStore.AddMessagesAsync([AI 响应])   ← 自动保存
   ↓
5. _sessionService.SaveThread(sessionId, thread)
   ↓
   - thread.Serialize() → 只返回 SessionId（很小）
   - 更新 sessions 集合的元数据（MessageCount, LastMessagePreview 等）
```

---

### **Thread 恢复流程**

```
1. 用户继续已有会话
   ↓
2. CreateAgentForSession(sessionId, profile)
   ↓
   ChatMessageStoreFactory = ctx =>
   {
       if (ctx.SerializedState.ValueKind is JsonValueKind.String)
       {
           // ✅ 从序列化状态恢复
           return new LiteDbChatMessageStore(
               messagesCollection,
               ctx.SerializedState,  // ← 包含 SessionId
               logger
           );
       }
   }
   ↓
3. LoadThread(sessionId, agent)
   ↓
   - 从 sessions 集合读取 ThreadData（只有 SessionId）
   - agent.DeserializeThread(threadData)
     → 触发 ChatMessageStoreFactory（带 SerializedState）
     → LiteDbChatMessageStore 恢复 SessionId
   ↓
4. agent.RunAsync(message, thread)
   ↓
   Agent Framework 自动调用:
   - ChatMessageStore.GetMessagesAsync()
     → 从 messages 集合查询历史（WHERE SessionId = ...)
   - 包含历史上下文的对话继续
```

---

## 📊 数据结构示例

### **sessions 集合**
```json
{
  "_id": "550e8400-e29b-41d4-a716-446655440000",
  "Name": "My Chat Session",
  "ThreadData": "\"550e8400-e29b-41d4-a716-446655440000\"",  // ← 只是 SessionId
  "MessageCount": 25,
  "LastMessagePreview": "That sounds like a great idea! Let me...",
  "LastMessageSender": "Sunny",
  "CreatedAt": "2025-10-26T10:00:00Z",
  "LastUpdated": "2025-10-26T14:30:00Z",
  "IsActive": true,
  "Version": 2
}
```

**关键点：**
- `ThreadData` 非常小（~50 bytes），即使有 1000 条消息也不会增长

---

### **messages 集合**
```json
[
  {
    "_id": "550e8400-e29b-41d4-a716-446655440000_msg001",
    "SessionId": "550e8400-e29b-41d4-a716-446655440000",  // ← 索引
    "MessageId": "msg001",
    "Timestamp": "2025-10-26T10:05:00Z",                  // ← 索引
    "SerializedMessage": "{\"Role\":\"user\",\"Text\":\"Hello!\"}",
    "MessageText": "Hello!",
    "AgentId": null,
    "AgentName": null,
    "IsUser": true,
    "Role": "user"
  },
  {
    "_id": "550e8400-e29b-41d4-a716-446655440000_msg002",
    "SessionId": "550e8400-e29b-41d4-a716-446655440000",
    "MessageId": "msg002",
    "Timestamp": "2025-10-26T10:05:02Z",
    "SerializedMessage": "{\"Role\":\"assistant\",\"Text\":\"Hi there!\"}",
    "MessageText": "Hi there!",
    "AgentId": "sunny",
    "AgentName": "Sunny",
    "IsUser": false,
    "Role": "assistant"
  }
]
```

**查询优化：**
- `SessionId` 索引：快速查找某个会话的所有消息
- `Timestamp` 索引：按时间排序

---

## 🛠️ 高级用法

### **自定义消息查询**

```csharp
// 在 PersistedSessionService 中添加自定义查询方法

public List<ChatMessageSummary> SearchMessages(
    string sessionId, 
    string keyword, 
    int limit = 50)
{
    var messages = _messages
        .Find(m => m.SessionId == sessionId && 
                   m.MessageText.Contains(keyword))
        .OrderByDescending(m => m.Timestamp)
        .Take(limit)
        .ToList();

    return messages.Select(ToSummary).ToList();
}

public List<ChatMessageSummary> GetMessagesByAgent(
    string sessionId, 
    string agentId)
{
    var messages = _messages
        .Find(m => m.SessionId == sessionId && 
                   m.AgentId == agentId)
        .OrderBy(m => m.Timestamp)
        .ToList();

    return messages.Select(ToSummary).ToList();
}

private ChatMessageSummary ToSummary(PersistedChatMessage pm)
{
    return new ChatMessageSummary
    {
        AgentId = pm.AgentId ?? "user",
        AgentName = pm.AgentName ?? "User",
        Content = pm.MessageText ?? string.Empty,
        ImageUrl = pm.ImageUrl,
        IsUser = pm.IsUser,
        Timestamp = pm.Timestamp.UtcDateTime,
        MessageType = string.IsNullOrEmpty(pm.ImageUrl) ? "text" : "image"
    };
}
```

---

### **性能监控**

```csharp
public class PerformanceStats
{
    public int TotalSessions { get; set; }
    public int TotalMessages { get; set; }
    public int ActiveSessions { get; set; }
    public long DatabaseSizeBytes { get; set; }
    public int CachedSessions { get; set; }
    public double AvgMessagesPerSession { get; set; }
}

public PerformanceStats GetPerformanceStats()
{
    var stats = _sessionService.GetStatistics();
    
    return new PerformanceStats
    {
        TotalSessions = (int)stats["TotalSessions"],
        ActiveSessions = (int)stats["ActiveSessions"],
        TotalMessages = (int)stats["TotalMessages"],
        CachedSessions = (int)stats["CachedSessions"],
        DatabaseSizeBytes = (long)stats["DatabaseSizeBytes"],
        AvgMessagesPerSession = (int)stats["TotalMessages"] / 
                                Math.Max(1, (int)stats["TotalSessions"])
    };
}
```

---

### **数据清理**

```csharp
// 定期清理旧消息（保留会话但删除消息）
public void CleanupOldMessages(int daysOld = 30)
{
    var cutoffDate = DateTimeOffset.UtcNow.AddDays(-daysOld);
    
    // 删除旧消息
    var deletedCount = _messages.DeleteMany(m => m.Timestamp < cutoffDate);
    
    // 更新会话的 MessageCount
    var affectedSessions = _sessions.FindAll();
    foreach (var session in affectedSessions)
    {
        session.MessageCount = _messages.Count(m => m.SessionId == session.Id);
        _sessions.Update(session);
    }
    
    _logger?.LogInformation("Cleaned up {Count} old messages", deletedCount);
}
```

---

## ⚠️ 迁移注意事项

### **从旧架构（v1）迁移**

如果你已经有使用旧架构的数据，需要运行迁移脚本：

```csharp
public void MigrateFromV1ToV2()
{
    var v1Sessions = _sessions.Find(s => s.Version == 1).ToList();
    
    foreach (var session in v1Sessions)
    {
        // 1. 提取 MessageSummaries 并保存到 messages 集合
        foreach (var summary in session.MessageSummaries)
        {
            var message = new PersistedChatMessage
            {
                Id = $"{session.Id}_{Guid.NewGuid()}",
                SessionId = session.Id,
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = summary.Timestamp,
                MessageText = summary.Content,
                AgentId = summary.AgentId,
                AgentName = summary.AgentName,
                IsUser = summary.IsUser,
                ImageUrl = summary.ImageUrl,
                Role = summary.IsUser ? "user" : "assistant",
                SerializedMessage = JsonSerializer.Serialize(new
                {
                    Role = summary.IsUser ? "user" : "assistant",
                    Text = summary.Content
                })
            };
            
            _messages.Insert(message);
        }
        
        // 2. 更新会话为 v2 格式
        session.ThreadData = JsonSerializer.Serialize(session.Id);
        session.Version = 2;
        session.LastMessagePreview = session.MessageSummaries.LastOrDefault()?.Content;
        session.LastMessageSender = session.MessageSummaries.LastOrDefault()?.AgentName;
        // session.MessageSummaries 会在下次保存时自动移除
        
        _sessions.Update(session);
    }
    
    _logger?.LogInformation("Migrated {Count} sessions from v1 to v2", v1Sessions.Count);
}
```

---

## 🧪 测试建议

### **单元测试**

```csharp
[Fact]
public async Task SendMessage_ShouldSaveToMessagesCollection()
{
    // Arrange
    var sessionService = new PersistedSessionService();
    var chatService = new AgentChatServiceRefactored(..., sessionService, ...);
    var session = sessionService.CreateSession("Test");

    // Act
    await chatService.SendMessageAsync("Hello", session.Id);

    // Assert
    var messages = sessionService.GetMessageSummaries(session.Id);
    Assert.NotEmpty(messages);
    Assert.Contains(messages, m => m.Content == "Hello");
}

[Fact]
public void SaveThread_ShouldHaveSmallThreadData()
{
    // Arrange
    var sessionService = new PersistedSessionService();
    var session = sessionService.CreateSession("Test");
    var agent = CreateTestAgent(session.Id);
    var thread = agent.GetNewThread();

    // Act
    sessionService.SaveThread(session.Id, thread);

    // Assert
    var savedSession = sessionService.GetSession(session.Id);
    Assert.True(savedSession.ThreadData.Length < 100); // 应该很小
}
```

---

## 📈 性能基准

| 操作 | 性能目标 | 备注 |
|-----|---------|------|
| 创建会话 | < 5ms | 只创建元数据 |
| 发送消息 | < 50ms | 包含 AI 调用 |
| 加载历史（100条） | < 10ms | 索引查询 |
| 保存 Thread | < 2ms | 只保存元数据 |
| 查询会话列表 | < 20ms | ThreadData 很小 |

---

## 🎓 最佳实践

1. **不要缓存 AIAgent 实例**
   - 每个会话创建独立的 Agent
   - 通过 `ChatMessageStoreFactory` 关联到正确的 SessionId

2. **利用索引**
   - 确保 `SessionId` 和 `Timestamp` 索引存在
   - 自定义查询时考虑索引性能

3. **定期清理**
   - 清理旧消息释放空间
   - 归档不活跃的会话

4. **监控性能**
   - 跟踪消息数量和数据库大小
   - 设置告警阈值

---

## 📚 相关资源

- [重构总结](./LITEDB-REFACTORING-SUMMARY.md)
- [架构对比](./LITEDB-REFACTORING-COMPARISON.md)
- [Agent Framework 官方文档](https://github.com/microsoft/agent-framework)
- [LiteDB 文档](https://www.litedb.org/)

---

**版本**: v2.0  
**最后更新**: 2025-10-26  
**状态**: ✅ 生产就绪
