# Triage Agent 消息过滤修复总结

## 问题描述

用户报告：默认情况下，triageAgent 会返回一个默认的消息放到消息列表中，但 triageAgent 应该对用户无感，不应该产生任何可见的消息。

**关键发现**：
- 刷新页面后，triage agent 的消息消失（说明没有存储到数据库 ✓）
- 发送消息时，triage agent 的消息会出现在消息列表中（说明在返回的 responses 中 ✗）
- Triage agent 的消息 ID 格式：`triage_21d5d4b338b64955a5ec223cc13e7d2b`（带有随机后缀）
- 消息内容为空，但仍然被添加到消息列表

## 根本原因

### 1. Agent ID 格式问题 ⚠️
**最关键的问题**：Workflow 框架生成的 `ExecutorId` 不是简单的 `"triage"`，而是带有随机后缀的格式：
```
triage_21d5d4b338b64955a5ec223cc13e7d2b
sunny_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
techie_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

原代码使用简单的字符串比较：
```csharp
if (agentUpdate.ExecutorId == "triage")  // ❌ 永远不会匹配！
```

这导致 triage agent 的事件**没有被跳过**，仍然创建了空的消息摘要。

### 2. 后端返回了用户消息
在 `AgentChatService.SendMessageAsync` 方法中，第 229-236 行会将用户消息添加到 `summaries` 列表中：

```csharp
// ❌ 错误：后端不应该返回用户消息
summaries.Add(new ChatMessageSummary
{
    Content = message,
    IsUser = true,
    Timestamp = DateTime.UtcNow,
    MessageType = "text"
});
```

**问题**：前端已经做了乐观更新（`Home.razor` 第 241-250 行），所以后端不应该再返回用户消息。

### 3. 缺少最终过滤
即使前面的检查失效，返回 summaries 之前也没有最后一道防线来过滤掉 triage 消息。

## 解决方案

### 修复 1: 提取 Agent ID 前缀进行比较 🔧
**文件**: `AgentChatService.cs` (第 263-278 行)

**问题**：ExecutorId 是 `"triage_xxxxx"` 而不是 `"triage"`

**修改前**:
```csharp
if (agentUpdate.ExecutorId == "triage")  // ❌ 不会匹配 "triage_xxxxx"
{
    continue;
}
```

**修改后**:
```csharp
// ✅ 提取 ID 前缀（处理 "triage_xxxxx" 格式）
var executorIdPrefix = agentUpdate.ExecutorId.Contains('_') 
    ? agentUpdate.ExecutorId.Split('_')[0] 
    : agentUpdate.ExecutorId;

if (executorIdPrefix.Equals("triage", StringComparison.OrdinalIgnoreCase))
{
    _logger?.LogDebug("Triage agent (ID: {ExecutorId}) routing to: {FunctionName}",
        agentUpdate.ExecutorId, ...);
    continue; // 跳过 triage agent 的所有处理
}
```

### 修复 2: 移除用户消息返回
**文件**: `AgentChatService.cs`

**修改前**:
```csharp
// 1️⃣ 添加用户消息摘要
summaries.Add(new ChatMessageSummary
{
    Content = message,
    IsUser = true,
    Timestamp = DateTime.UtcNow,
    MessageType = "text"
});
```

**修改后**:
```csharp
// 1️⃣ 准备消息列表（包含历史消息）
// ✅ 注意：不添加用户消息到 summaries，因为前端已经做了乐观更新
//    summaries 只用于返回 AI agent 的响应
```

### 修复 3: 保存消息时也检查前缀 🔧
**文件**: `AgentChatService.cs` (第 358-365 行)

**修改前**:
```csharp
if (currentExecutorId != null && currentExecutorId != "triage" && currentSummary != null)
```

**修改后**:
```csharp
// ✅ 提取前缀并检查
var currentExecutorIdPrefix = currentExecutorId != null && currentExecutorId.Contains('_')
    ? currentExecutorId.Split('_')[0]
    : currentExecutorId;

if (currentExecutorId != null && 
    !string.Equals(currentExecutorIdPrefix, "triage", StringComparison.OrdinalIgnoreCase) && 
    currentSummary != null)
```

### 修复 4: 添加最终安全过滤 🛡️
**文件**: `AgentChatService.cs` (返回前)

**新增代码**:
```csharp
// 6️⃣ 最后的安全检查：过滤掉所有 triage agent 消息和空消息
var filteredSummaries = summaries.Where(s =>
{
    // 提取 agent ID 前缀
    var agentIdPrefix = s.AgentId.Contains('_') ? s.AgentId.Split('_')[0] : s.AgentId;
    
    // 排除 triage agent 和空消息
    return !string.Equals(agentIdPrefix, "triage", StringComparison.OrdinalIgnoreCase) &&
           !string.IsNullOrWhiteSpace(s.Content);
}).ToList();

_logger?.LogInformation("Returning {Count} filtered responses for session {SessionId}",
    filteredSummaries.Count, sessionId);

return filteredSummaries;
```

### 修复 5: 优化 Triage Agent Prompt
**文件**: `AgentChatService.cs`

**修改前**:
```csharp
var triageInstructions =
    "You are a smart routing agent that analyzes user messages and decides which specialist agent should respond. " +
    "IMPORTANT: You MUST ALWAYS use the handoff function to delegate to one of the specialist agents. NEVER respond directly. " +
    ...
```

**修改后**:
```csharp
var triageInstructions =
    "You are an invisible routing agent. Your ONLY job is to analyze messages and call the handoff function. " +
    "CRITICAL RULES:\n" +
    "1. NEVER generate ANY text response - you are completely silent and invisible to users\n" +
    "2. IMMEDIATELY call the handoff function without any explanation or text\n" +
    "3. Do NOT acknowledge, greet, or respond - just route silently\n" +
    ...
```

## 关键技术点

### Agent ID 格式处理
Microsoft Agent Framework 在运行时会为每个 agent 生成唯一的 ExecutorId：
```
原始名称: "triage"
运行时 ID: "triage_21d5d4b338b64955a5ec223cc13e7d2b"
```

**解决方案**：提取前缀进行比较
```csharp
var prefix = id.Contains('_') ? id.Split('_')[0] : id;
if (prefix.Equals("triage", StringComparison.OrdinalIgnoreCase))
{
    // 是 triage agent
}
```

### 多层防护策略
1. **事件层过滤**（最早）：在 WorkflowEvent 处理时跳过 triage agent
2. **存储层过滤**（保存前）：保存消息时检查 agent ID 前缀
3. **返回层过滤**（最后）：返回前最终过滤 triage 消息和空消息

### 已复用的模式
`GetAgentProfile` 方法已经在第 145 行实现了同样的前缀提取逻辑：
```csharp
var agentIdPrefix = agentId.Contains('_') ? agentId.Split('_')[0] : agentId;
```

现在所有地方都使用一致的 ID 处理逻辑。

## 修改文件列表

1. `src/AgentGroupChat.AgentHost/Services/AgentChatService.cs`
   - **关键修复**：提取 ExecutorId 前缀进行 triage 判断（第 263-278 行）
   - 移除用户消息添加到 summaries
   - 保存消息时也检查 ID 前缀（第 358-365 行）
   - 添加最终过滤层（返回前）
   - 优化 triage agent 的 system prompt
   - 增强日志记录

## 测试验证

### 测试用例 1: 发送消息
**输入**: "Hello, how are you?"

**预期结果**:
```json
[
  {
    "agentId": "sunny_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
    "agentName": "Sunny",
    "agentAvatar": "☀️",
    "content": "Hi! I'm doing great! ☀️...",
    "isUser": false,
    "messageType": "text"
  }
]
```

**不应该包含**:
- ❌ 用户消息 (`isUser: true`)
- ❌ Triage agent 消息 (`agentId: "triage_xxxxx"`)
- ❌ 空消息 (`content: ""`)

### 测试用例 2: 刷新页面
**操作**: 发送消息后刷新页面

**预期结果**:
- ✅ 消息列表与刷新前一致
- ✅ 没有 triage agent 消息
- ✅ 所有消息都有内容

### 测试用例 3: 查看日志
**操作**: 查看应用日志

**预期日志**:
```
[Debug] Triage agent (ID: triage_21d5d4b338b64955a5ec223cc13e7d2b) routing to: handoff with args: {"target":"sunny"}
[Debug] Agent switched to: sunny_xxxxxxxx (Sunny)
[Debug] Created summary for specialist agent sunny_xxxxxxxx
[Information] Collected 1 agent responses for session xxx
[Information] Returning 1 filtered responses for session xxx
```

### 边缘情况测试

#### 情况 1: Agent ID 没有下划线
```csharp
ExecutorId = "triage"  // ✅ 应该被过滤
ExecutorId = "sunny"   // ✅ 应该保留
```

#### 情况 2: Agent ID 有多个下划线
```csharp
ExecutorId = "triage_abc_def_123"  // ✅ 提取 "triage"，应该被过滤
```

#### 情况 3: 空消息
```csharp
Content = ""           // ✅ 应该被最终过滤层移除
Content = "  "         // ✅ 应该被 IsNullOrWhiteSpace 过滤
Content = "Hello"      // ✅ 保留
```

## 技术细节

### 消息流程（修复后）

```
用户输入
   ↓
前端乐观更新（立即显示用户消息）
   ↓
发送到后端 API
   ↓
AgentChatService.SendMessageAsync
   ↓
Workflow 执行
   ├── Triage Agent (ExecutorId: "triage_xxxxx")
   │    ↓
   │   提取前缀 "triage" → 匹配 → continue 跳过 ✅
   │
   └── Specialist Agent (ExecutorId: "sunny_xxxxx")
        ↓
       提取前缀 "sunny" → 不是 "triage" → 创建 summary ✅
        ↓
       累积文本内容
   ↓
最终过滤（返回前）
   ├── 过滤 triage agent (ID 前缀检查)
   ├── 过滤空消息 (IsNullOrWhiteSpace)
   └── 返回干净的 specialist 响应列表
   ↓
前端接收并显示 AI 响应
   ↓
保存到 LiteDB（ID 前缀检查，排除 triage）
```

### 关键改进

1. **ID 前缀提取一致性**：
   ```csharp
   // 统一的 ID 处理逻辑（3 处使用）
   var prefix = id.Contains('_') ? id.Split('_')[0] : id;
   ```
   - 事件处理层 (line 265)
   - 消息保存层 (line 360)
   - 最终过滤层 (line 418)

2. **三层防护**：
   - Layer 1: 事件层 - 跳过 triage 事件，不创建 summary
   - Layer 2: 存储层 - 不保存 triage 消息到数据库
   - Layer 3: 返回层 - 最终过滤确保没有 triage 或空消息

3. **职责分离**：
   - 前端：负责显示用户消息（乐观更新）
   - 后端：只返回 AI agent 的响应

4. **数据一致性**：
   - 返回的消息列表 = 实际保存到数据库的消息
   - 刷新页面后看到的消息 = 发送时看到的消息

### 调试技巧

如果遇到类似问题，检查以下日志：

```
[Debug] Triage agent (ID: triage_xxxxx) routing to: handoff
```
→ 确认 triage agent 被识别并跳过

```
[Debug] Created summary for specialist agent sunny_xxxxx
```
→ 确认只为 specialist 创建 summary

```
[Information] Returning X filtered responses
```
→ 确认最终返回的消息数量

## 总结

这次修复解决了 **Agent ID 格式不匹配** 的根本问题：

### 问题
- ❌ 代码检查 `ExecutorId == "triage"`
- ❌ 实际值是 `"triage_21d5d4b338b64955a5ec223cc13e7d2b"`
- ❌ 永远不会匹配，导致 triage 消息泄漏

### 解决
- ✅ 提取 ID 前缀进行比较
- ✅ 三层防护确保万无一失
- ✅ 统一的 ID 处理逻辑

### 效果
1. ✅ Triage agent 对用户完全无感知
2. ✅ 后端不返回重复的用户消息
3. ✅ 只有 specialist agents 的响应被显示和存储
4. ✅ 消息流程清晰、可追踪
5. ✅ 没有空消息或无效消息

修复日期：2025-10-26
最后更新：2025-10-26 (修复 Agent ID 格式匹配问题)
