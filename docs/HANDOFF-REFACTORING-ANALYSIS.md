# Handoff 模式重构分析

## 🔍 问题诊断

### 当前实现的问题

当前 `AgentChatService` 的实现**根本不是真正的 handoff 模式**，而是一个基于 `@mention` 的简单路由：

```csharp
// ❌ 错误实现
var mentionedAgent = DetectMentionedAgent(message);  // 手动检测 @mention
var agent = CreateAgentForSession(sessionId, mentionedAgent);  // 创建单个 agent
var agentResponse = await agent.RunAsync(message, thread);  // 直接运行
```

**问题列表：**

1. ❌ **没有使用 `AgentWorkflowBuilder`**：缺少 workflow 编排
2. ❌ **没有 Triage Agent**：应该由 AI 智能判断路由，而不是检测 `@mention`
3. ❌ **没有 Handoff 机制**：agent 之间无法切换，每次只运行一个 agent
4. ❌ **没有 `StreamingRun`**：无法处理 workflow 事件流
5. ❌ **没有 `WorkflowEvent`**：无法追踪哪个 agent 在执行

---

## ✅ 官方 Handoff 实现（正确方式）

参考：[microsoft/agent-framework - 04_AgentWorkflowPatterns](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/GettingStarted/Workflows/_Foundational/04_AgentWorkflowPatterns/Program.cs)

### 核心代码

```csharp
// 1️⃣ 创建专家 Agents
ChatClientAgent historyTutor = new(client,
    "You provide assistance with historical queries. Only respond about history.",
    "history_tutor",
    "Specialist agent for historical questions");

ChatClientAgent mathTutor = new(client,
    "You provide help with math problems. Only respond about math.",
    "math_tutor",
    "Specialist agent for math questions");

// 2️⃣ 创建 Triage Agent（路由器）
ChatClientAgent triageAgent = new(client,
    "You determine which agent to use based on the user's homework question. " +
    "ALWAYS handoff to another agent.",  // 关键：总是 handoff
    "triage_agent",
    "Routes messages to the appropriate specialist agent");

// 3️⃣ 使用 AgentWorkflowBuilder 构建 Handoff Workflow
var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
    .WithHandoffs(triageAgent, [mathTutor, historyTutor])      // triage → 专家们
    .WithHandoffs([mathTutor, historyTutor], triageAgent)      // 专家们 → triage
    .Build();

// 4️⃣ 运行 Workflow（单次对话中，多个 agent 自动切换）
await using StreamingRun run = await InProcessExecution.StreamAsync(workflow, messages);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

// 5️⃣ 处理 WorkflowEvent，追踪不同 agent 的执行
string? lastExecutorId = null;
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is AgentRunUpdateEvent e)
    {
        if (e.ExecutorId != lastExecutorId)
        {
            lastExecutorId = e.ExecutorId;
            Console.WriteLine();
            Console.WriteLine(e.ExecutorId);  // 显示当前执行的 agent
        }
        Console.Write(e.Update.Text);
    }
    else if (evt is WorkflowOutputEvent output)
    {
        return output.As<List<ChatMessage>>()!;
    }
}
```

---

## 🎯 正确的 Handoff 特性

### 1. **智能路由（AI-driven Routing）**
- Triage agent **自动分析**用户消息
- **不需要** `@mention`
- AI 决定调用哪个专家

### 2. **Agent 切换（Handoff）**
- 在**一次对话**中，可以经历多个 agent
- Triage → Specialist → Triage（循环）
- 通过 `WithHandoffs()` 配置切换路径

### 3. **Workflow 编排**
- 使用 `AgentWorkflowBuilder` 创建 workflow
- 所有 agent 在同一个 `StreamingRun` 中执行
- 通过 `WorkflowEvent` 追踪执行流程

### 4. **事件流处理**
- `AgentRunUpdateEvent`：agent 输出更新
- `WorkflowOutputEvent`：workflow 完成
- 可以区分不同 `ExecutorId`（agent 身份）

---

## 🔧 重构方案

### 架构变更

```
旧架构（伪 Handoff）:
User Message → DetectMentionedAgent() → CreateSingleAgent() → RunAsync() → Response

新架构（真 Handoff）:
User Message → Workflow.StreamAsync() → [Triage → Specialist → Triage] → WorkflowEvents → Response
```

### 实现步骤

1. **创建 Workflow**
   - 使用 `AgentWorkflowBuilder.CreateHandoffBuilderWith()`
   - 配置 triage agent 和所有专家 agents
   - 定义 handoff 路径

2. **修改 Triage Agent Prompt**
   ```
   "You are a smart router that analyzes user messages and decides which specialist to use.
   ALWAYS use the handoff function to delegate to the appropriate agent:
   - @Sunny: cheerful, optimistic, daily life
   - @Techie: tech-savvy, coding, gadgets
   - @Artsy: creative, artistic, design
   - @Foodie: food-loving, cooking, recipes
   Never respond directly; always handoff."
   ```

3. **处理 WorkflowEvent**
   - 监听 `AgentRunUpdateEvent`
   - 追踪 `e.ExecutorId` 变化
   - 为每个 agent 的输出创建 `ChatMessageSummary`

4. **集成消息持久化**
   - 在 workflow 完成后保存所有消息
   - 使用 `LiteDbChatMessageStore`
   - 保存 thread 状态

---

## 📊 对比表

| 特性 | 当前实现（错误） | 正确实现 |
|------|-----------------|---------|
| 路由方式 | 手动检测 `@mention` | AI 智能判断（triage agent） |
| Agent 切换 | 无，每次只运行一个 | 有，一次对话中可切换多个 |
| Workflow | ❌ 无 | ✅ `AgentWorkflowBuilder` |
| 事件流 | ❌ 无 | ✅ `WorkflowEvent` |
| Handoff 函数 | ❌ 无 | ✅ `WithHandoffs()` |
| 执行模式 | `agent.RunAsync()` | `InProcessExecution.StreamAsync()` |
| Agent 协作 | ❌ 无法协作 | ✅ 可以协作（triage ↔ specialists） |

---

## 🚀 预期效果

重构后，用户体验将完全不同：

### 旧版本（伪 Handoff）
```
User: "Tell me about the weather"
System: (检测没有 @mention，使用默认 agent)
Assistant: "The weather is nice today."
```

### 新版本（真 Handoff）
```
User: "Tell me about the weather"
[Triage Agent]: (分析消息) → Handoff to @Sunny
[Sunny Agent]: "☀️ What a beautiful day! The sun is shining brightly..."
```

### 复杂场景
```
User: "Can you help me cook a Python script?"
[Triage Agent]: (分析) → 检测到 "Python" 和 "cook"
[Triage Agent]: → Handoff to @Techie（优先技术）
[Techie Agent]: "Sure! Here's a Python script..."
```

---

## 🔒 关键代码片段

### Workflow 创建
```csharp
private Workflow CreateHandoffWorkflow()
{
    // 创建 triage agent
    var triageAgent = _chatClient.CreateChatClientAgent(
        instructions: "You analyze user messages and ALWAYS handoff to specialists...",
        name: "triage",
        description: "Smart router agent");

    // 创建专家 agents
    var specialists = _agentProfiles.Select(profile =>
        _chatClient.CreateChatClientAgent(
            instructions: profile.SystemPrompt,
            name: profile.Id,
            description: profile.Description)
    ).ToList();

    // 构建 workflow
    return AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
        .WithHandoffs(triageAgent, specialists)           // triage → all specialists
        .WithHandoffs(specialists, [triageAgent])         // all specialists → triage
        .Build();
}
```

### 事件处理
```csharp
var summaries = new List<ChatMessageSummary>();
string? currentExecutorId = null;

await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is AgentRunUpdateEvent e)
    {
        if (e.ExecutorId != currentExecutorId)
        {
            currentExecutorId = e.ExecutorId;
            var profile = GetAgentProfile(e.ExecutorId);
            summaries.Add(new ChatMessageSummary
            {
                AgentId = e.ExecutorId,
                AgentName = profile?.Name ?? e.ExecutorId,
                AgentAvatar = profile?.Avatar ?? "🤖",
                Content = "",
                IsUser = false
            });
        }
        summaries.Last().Content += e.Update.Text;
    }
}
```

---

## 📝 总结

当前实现是一个**假的 handoff**，只是简单的 `@mention` 路由。真正的 handoff 需要：

1. ✅ 使用 `AgentWorkflowBuilder`
2. ✅ 创建 Triage Agent（智能路由器）
3. ✅ 配置 `WithHandoffs()` 定义切换路径
4. ✅ 使用 `StreamingRun` 和 `WorkflowEvent`
5. ✅ AI 自动判断路由，不依赖手动标记

重构后，系统将具备真正的多 agent 协作能力！🎉
