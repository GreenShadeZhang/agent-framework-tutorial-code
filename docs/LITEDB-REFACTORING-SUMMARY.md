# LiteDB 消息持久化重构总结

## 📊 重构概述

基于 Microsoft Agent Framework 的两个官方示例进行了消息持久化架构的深度重构：
- **Step06_PersistedConversations**: 简单的 Thread 序列化方式
- **Step07_3rdPartyThreadStorage**: 高级的 ChatMessageStore 分离存储方式

## 🎯 重构目标

1. **消息和 Thread 状态分离存储**
2. **减小 Thread 序列化数据大小**
3. **提升查询和存储性能**
4. **符合 Agent Framework 最佳实践**

---

## 🏗️ 新架构设计

### **数据模型层**

#### 1. `PersistedChatMessage.cs` (新增)
```csharp
// 独立的消息存储模型
public class PersistedChatMessage
{
    public string Id { get; set; }                    // {SessionId}_{MessageId}
    public string SessionId { get; set; }             // 会话 ID（索引）
    public string MessageId { get; set; }             // 消息 ID
    public DateTimeOffset Timestamp { get; set; }     // 时间戳（索引）
    public string SerializedMessage { get; set; }     // 完整的 ChatMessage JSON
    public string? MessageText { get; set; }          // 文本内容（快速搜索）
    public string? AgentId { get; set; }              // Agent ID
    public bool IsUser { get; set; }                  // 是否用户消息
    // ... 更多字段
}
```

**特点：**
- ✅ 独立的 `messages` 集合，与 `sessions` 分离
- ✅ `SessionId` 索引支持高效查询
- ✅ 包含冗余字段（如 `MessageText`）用于快速展示

#### 2. `PersistedChatSession.cs` (优化)
```csharp
// 简化的会话元数据模型
public class PersistedChatSession
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string ThreadData { get; set; }            // ⚠️ 现在只包含 SessionId，不含消息
    public int MessageCount { get; set; }             // 缓存的消息数
    public string? LastMessagePreview { get; set; }   // 最后消息预览
    public DateTime LastUpdated { get; set; }
    public int Version { get; set; } = 2;             // v2: 新架构
    // 移除: MessageSummaries
}
```

**变化：**
- ❌ 移除 `MessageSummaries` 字段（消息在独立集合）
- ✅ 添加 `LastMessagePreview` 和 `LastMessageSender`（快速展示）
- ✅ `ThreadData` 现在非常小（只有元数据）

---

### **服务层**

#### 3. `LiteDbChatMessageStore.cs` (核心新增)
```csharp
// 自定义 ChatMessageStore 实现
public class LiteDbChatMessageStore : ChatMessageStore
{
    private readonly ILiteCollection<PersistedChatMessage> _messagesCollection;
    public string SessionId { get; private set; }

    // 核心方法
    public override Task AddMessagesAsync(IEnumerable<ChatMessage> messages, ...);
    public override Task<IEnumerable<ChatMessage>> GetMessagesAsync(...);
    public override JsonElement Serialize(...); // ⚠️ 只序列化 SessionId
}
```

**关键设计：**
- ✅ 继承自 `ChatMessageStore` 基类
- ✅ `Serialize()` **只返回 SessionId**，不返回消息
- ✅ 消息存储在 LiteDB `messages` 集合
- ✅ 支持序列化状态恢复（`SerializedState` 构造函数）

#### 4. `PersistedSessionService.cs` (重构)
```csharp
public class PersistedSessionService
{
    private readonly ILiteCollection<PersistedChatSession> _sessions;
    private readonly ILiteCollection<PersistedChatMessage> _messages;  // 新增

    // 简化的 SaveThread（不再接收 summaries 参数）
    public void SaveThread(string sessionId, AgentThread thread);
    
    // 新增方法
    public ILiteCollection<PersistedChatMessage> GetMessagesCollection();
    public List<ChatMessageSummary> GetMessageSummaries(string sessionId);
    public void ClearSessionMessages(string sessionId);
}
```

**优化：**
- ✅ 管理两个集合：`sessions` 和 `messages`
- ✅ 添加消息集合的索引（`SessionId`, `Timestamp`）
- ✅ `SaveThread()` 不再需要手动传递 summaries

#### 5. `AgentChatService_Refactored.cs` (新版)
```csharp
public class AgentChatServiceRefactored
{
    // 关键：不再缓存 AIAgent 实例
    // private readonly Dictionary<string, AIAgent> _aiAgents;  ❌ 移除

    // 为每个会话动态创建 Agent（带 ChatMessageStoreFactory）
    private AIAgent CreateAgentForSession(string sessionId, AgentProfile? profile)
    {
        return _chatClient.CreateAIAgent(new ChatClientAgentOptions
        {
            ChatMessageStoreFactory = ctx =>
            {
                var messagesCollection = _sessionService.GetMessagesCollection();
                
                if (ctx.SerializedState.ValueKind is JsonValueKind.String)
                {
                    // 恢复：从序列化状态中提取 SessionId
                    return new LiteDbChatMessageStore(messagesCollection, ctx.SerializedState, ...);
                }
                else
                {
                    // 新建：直接使用 sessionId
                    return new LiteDbChatMessageStore(messagesCollection, sessionId, ...);
                }
            }
        });
    }
}
```

**核心改进：**
- ✅ 每个会话创建独立的 AIAgent（带专属 `ChatMessageStore`）
- ✅ 通过 `ChatMessageStoreFactory` 注入 `LiteDbChatMessageStore`
- ✅ 支持序列化状态恢复（`ctx.SerializedState`）
- ✅ 消息自动通过 `ChatMessageStore` 保存，无需手动管理

---

## 🔄 数据流程对比

### **旧架构（v1）**
```
用户消息 
  ↓
AgentChatService.SendMessageAsync()
  ↓
agent.RunAsync() → 生成响应
  ↓
手动构建 MessageSummaries
  ↓
sessionService.SaveThread(sessionId, thread, summaries)
  ↓
Thread 序列化（包含所有消息）→ sessions.ThreadData
MessageSummaries → sessions.MessageSummaries
```

**问题：**
- ❌ 数据重复（消息在 Thread 和 MessageSummaries 中都有）
- ❌ `ThreadData` 随对话增长而膨胀
- ❌ 难以独立查询消息历史

---

### **新架构（v2）**
```
用户消息 
  ↓
AgentChatService.SendMessageAsync()
  ↓
CreateAgentForSession(sessionId) → 创建带 ChatMessageStoreFactory 的 Agent
  ↓
GetOrCreateThread(sessionId, agent) → 加载或创建 Thread
  ↓
agent.RunAsync(message, thread)
  ↓
  ├─ ChatMessageStore.AddMessagesAsync() → 自动保存到 messages 集合
  └─ 生成响应
  ↓
sessionService.SaveThread(sessionId, thread)
  ↓
Thread.Serialize() → 只返回 SessionId → sessions.ThreadData (很小)
```

**优势：**
- ✅ 消息通过 `ChatMessageStore` 自动保存
- ✅ `ThreadData` 非常小（只有 SessionId）
- ✅ 消息独立存储，易于查询和管理
- ✅ 符合官方推荐的架构模式

---

## 📦 LiteDB 集合结构

### **sessions 集合**
```json
{
  "_id": "abc123",
  "Name": "Session 2025-10-26",
  "ThreadData": "\"abc123\"",  // ⚠️ 只是 SessionId 字符串
  "MessageCount": 15,
  "LastMessagePreview": "That's a great idea! Let me...",
  "LastMessageSender": "Sunny",
  "LastUpdated": "2025-10-26T10:30:00Z",
  "Version": 2
}
```

### **messages 集合**
```json
{
  "_id": "abc123_msg001",
  "SessionId": "abc123",          // ← 索引
  "MessageId": "msg001",
  "Timestamp": "2025-10-26T10:25:00Z",  // ← 索引
  "SerializedMessage": "{\"Role\":\"user\",\"Text\":\"Hello\", ...}",
  "MessageText": "Hello",
  "AgentId": null,
  "IsUser": true
}
```

---

## 🚀 使用方式变化

### **旧方式**
```csharp
// Program.cs
var sessionService = new PersistedSessionService();
var chatService = new AgentChatService(configuration);

// 发送消息
var summaries = await chatService.SendMessageAsync(message, sessionId, sessionService);
```

### **新方式**
```csharp
// Program.cs
var sessionService = new PersistedSessionService();
var chatService = new AgentChatServiceRefactored(
    configuration, 
    sessionService  // ← 注入依赖
);

// 发送消息（更简单！）
var summaries = await chatService.SendMessageAsync(message, sessionId);
```

---

## ✅ 重构优势总结

| 方面 | 旧架构 | 新架构 |
|-----|-------|-------|
| **Thread 序列化大小** | 包含所有消息（几 MB） | 只有 SessionId（几 KB） |
| **消息查询** | 需反序列化 Thread | 直接查询 messages 集合 |
| **数据冗余** | MessageSummaries + ThreadData | 无冗余 |
| **扩展性** | 难以迁移到其他存储 | 易于切换存储（Redis/PostgreSQL） |
| **符合官方标准** | 部分符合 Step06 | 完全符合 Step06 + Step07 |
| **性能** | 随对话增长而变慢 | 索引优化，性能稳定 |

---

## 📝 迁移指南

### **如何从 v1 迁移到 v2？**

1. **保留旧数据**（可选）
   ```csharp
   // 读取 v1 会话
   var oldSessions = _sessions.Find(s => s.Version == 1).ToList();
   ```

2. **迁移消息到 messages 集合**
   ```csharp
   foreach (var session in oldSessions)
   {
       foreach (var summary in session.MessageSummaries)
       {
           var msg = new PersistedChatMessage
           {
               Id = $"{session.Id}_{Guid.NewGuid()}",
               SessionId = session.Id,
               MessageText = summary.Content,
               // ... 映射其他字段
           };
           _messages.Insert(msg);
       }
       
       // 更新会话版本
       session.Version = 2;
       session.ThreadData = JsonSerializer.Serialize(session.Id);
       _sessions.Update(session);
   }
   ```

3. **切换到新服务**
   ```csharp
   // 替换旧的 AgentChatService
   services.AddSingleton<AgentChatServiceRefactored>();
   ```

---

## 🧪 测试要点

### **关键测试场景：**

1. ✅ **新会话创建和消息保存**
   - 创建会话 → 发送消息 → 验证 messages 集合
   - 检查 ThreadData 大小（应该很小）

2. ✅ **会话恢复和历史加载**
   - 重启应用 → 加载旧会话 → 继续对话
   - 验证历史消息正确恢复

3. ✅ **多会话并发**
   - 同时处理多个会话
   - 验证消息不会混淆

4. ✅ **长对话性能**
   - 发送 100+ 条消息
   - 验证性能稳定（不随消息数增长而变慢）

---

## 🎓 学到的核心概念

1. **ChatMessageStore 模式**
   - 消息存储和 Thread 状态分离
   - `Serialize()` 只保存最小状态（如 SessionId）

2. **ChatMessageStoreFactory**
   - 通过 Factory 模式注入自定义存储
   - 支持序列化状态恢复（`ctx.SerializedState`）

3. **Agent 生命周期管理**
   - 不要缓存 Agent 实例（每个会话独立创建）
   - 每个 Thread 需要独立的 MessageStore

---

## 📚 参考资料

- [Agent Framework Step06: PersistedConversations](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/GettingStarted/Agents/Agent_Step06_PersistedConversations)
- [Agent Framework Step07: 3rdPartyThreadStorage](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/GettingStarted/Agents/Agent_Step07_3rdPartyThreadStorage)
- [LiteDB 文档](https://www.litedb.org/)

---

## ⚠️ 注意事项

1. **版本兼容性**
   - v1 和 v2 数据结构不兼容
   - 需要数据迁移脚本

2. **性能优化**
   - 确保 `SessionId` 和 `Timestamp` 索引存在
   - 定期清理旧消息

3. **错误处理**
   - ChatMessageStore 的异常需要妥善处理
   - 序列化/反序列化错误的降级策略

---

**重构完成时间**: 2025-10-26  
**版本**: v2.0  
**状态**: ✅ 完成，待测试验证
