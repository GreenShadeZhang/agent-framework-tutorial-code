# Handoff 模式实现指南

## 🎯 核心改进

### 问题：原实现是"假 Handoff"

原始实现只是通过 `@mention` 手动选择 agent，没有真正的 workflow 编排和智能路由。

### 解决方案：真正的 Handoff Workflow

参考官方示例 [04_AgentWorkflowPatterns](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/GettingStarted/Workflows/_Foundational/04_AgentWorkflowPatterns/Program.cs)，实现了：

1. ✅ **AgentWorkflowBuilder** - 使用官方 workflow 构建器
2. ✅ **Triage Agent** - AI 智能路由器（不依赖 @mention）
3. ✅ **Handoff 机制** - 真正的 agent 切换
4. ✅ **WorkflowEvent 流** - 追踪多个 agent 的执行
5. ✅ **动态提示词** - Triage agent 的指令是动态生成的

---

## 🔧 技术实现

### 1. 动态生成 Triage Agent 指令

**关键改进：提示词通用化**

```csharp
// ✅ 动态生成（基于实际的 agent profiles）
var specialistDescriptions = string.Join("\n", _agentProfiles.Select(profile =>
    $"- {profile.Id}: {profile.Description} (Personality: {profile.Personality})"
));

var triageInstructions = 
    "You are a smart routing agent that analyzes user messages and decides which specialist agent should respond. " +
    "IMPORTANT: You MUST ALWAYS use the handoff function to delegate to one of the specialist agents. NEVER respond directly. " +
    "\n\nAvailable specialist agents:\n" +
    specialistDescriptions +
    "\n\nAnalyze the user's message and handoff to the most appropriate specialist...";
```

**优势：**
- ✅ 不硬编码具体 agent 名称
- ✅ 自动适应 `_agentProfiles` 的变化
- ✅ 易于扩展新 agent
- ✅ 易于维护

**生成示例：**
```
Available specialist agents:
- sunny: The optimistic one who loves sunshine (Personality: Cheerful and optimistic)
- techie: The tech enthusiast who codes and tinkers (Personality: Tech-savvy and analytical)
- artsy: The artist who finds beauty everywhere (Personality: Creative and artistic)
- foodie: The food enthusiast who loves to eat and cook (Personality: Food-loving and enthusiastic)
```

---

### 2. Workflow 构建

```csharp
private Workflow CreateHandoffWorkflow(string sessionId)
{
    // 1️⃣ 创建 Triage Agent（动态指令）
    var triageAgent = new ChatClientAgent(
        _chatClient,
        instructions: triageInstructions,  // 动态生成
        name: "triage",
        description: "Smart router that delegates to specialist agents");

    // 2️⃣ 创建所有 Specialist Agents
    var specialistAgents = _agentProfiles.Select(profile =>
        new ChatClientAgent(
            _chatClient,
            instructions: profile.SystemPrompt,
            name: profile.Id,
            description: profile.Description)
    ).ToList();

    // 3️⃣ 构建 Handoff Workflow
    var builder = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent);
    builder.WithHandoffs(triageAgent, specialistAgents);  // triage → specialists
    
    foreach (var specialist in specialistAgents)
    {
        builder.WithHandoffs(specialist, [triageAgent]);  // specialists → triage
    }
    
    return builder.Build();
}
```

---

### 3. WorkflowEvent 处理

```csharp
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is AgentRunUpdateEvent agentUpdate)
    {
        // 检测 agent 切换
        if (agentUpdate.ExecutorId != currentExecutorId)
        {
            currentExecutorId = agentUpdate.ExecutorId;
            var profile = GetAgentProfile(currentExecutorId);
            
            // 跳过 triage agent 的输出（它只负责路由）
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
        
        // 追加文本内容（仅非 triage agent）
        if (currentExecutorId != "triage" && currentSummary != null)
        {
            currentSummary.Content += agentUpdate.Update.Text;
        }
    }
    else if (evt is WorkflowOutputEvent output)
    {
        break;  // Workflow 完成
    }
}
```

---

## 📊 对比：旧 vs 新

| 特性 | 旧实现（假 Handoff） | 新实现（真 Handoff） |
|------|---------------------|---------------------|
| **路由方式** | `DetectMentionedAgent()` 检测 `@mention` | AI 智能判断（Triage Agent） |
| **Agent 切换** | ❌ 无，每次只运行一个 | ✅ 有，Triage ↔ Specialist |
| **Workflow** | ❌ 无 | ✅ `AgentWorkflowBuilder` |
| **事件流** | ❌ 无 | ✅ `WorkflowEvent` 追踪 |
| **提示词** | ❌ 硬编码 agent 名称 | ✅ 动态生成（通用） |
| **扩展性** | ❌ 难扩展（需修改多处） | ✅ 易扩展（只需添加 profile） |
| **用户体验** | 必须使用 `@mention` | 自然对话，AI 自动路由 |

---

## 🚀 使用示例

### 旧版本（需要 @mention）
```
User: "@Sunny tell me something positive"
System: (检测到 @Sunny，创建 Sunny agent)
Sunny: "☀️ What a beautiful day! ..."
```

### 新版本（智能路由）
```
User: "I'm feeling down, can you cheer me up?"
[Triage Agent]: (分析情绪，检测到需要积极回应)
[Triage Agent]: → Handoff to sunny
[Sunny Agent]: "☀️ Hey there! Let me brighten your day! ..."
```

```
User: "How do I write a Python function?"
[Triage Agent]: (检测到技术问题)
[Triage Agent]: → Handoff to techie
[Techie Agent]: "🤖 Great question! Here's how you write a function in Python..."
```

---

## 🎨 添加新 Agent

只需在 `_agentProfiles` 中添加新配置，无需修改其他代码：

```csharp
new AgentProfile
{
    Id = "scientist",
    Name = "Scientist",
    Avatar = "🔬",
    Personality = "Curious and analytical",
    SystemPrompt = "You are a scientist who loves experiments and discoveries...",
    Description = "The researcher who explains science"
}
```

**自动生效：**
- ✅ Triage agent 的提示词自动包含新 agent
- ✅ Workflow 自动配置 handoff 路径
- ✅ 无需修改任何其他代码

---

## 🔍 工作流程

```
User Message
    ↓
CreateHandoffWorkflow()
    ├─ Generate dynamic triage instructions (based on _agentProfiles)
    ├─ Create ChatClientAgent for triage
    ├─ Create ChatClientAgent for each specialist
    └─ Build workflow with handoff paths
    ↓
InProcessExecution.StreamAsync()
    ↓
WorkflowEvent Stream
    ├─ [Triage Agent] analyzes message
    ├─ [Triage Agent] calls handoff function
    ├─ [Specialist Agent] responds
    └─ [WorkflowOutputEvent] workflow complete
    ↓
Extract responses & save to LiteDB
    ↓
Return ChatMessageSummary list
```

---

## ✅ 关键优势

1. **真正的 AI 路由**
   - 不需要用户知道有哪些 agent
   - AI 根据上下文智能选择
   - 更自然的对话体验

2. **完全通用**
   - Triage agent 的提示词动态生成
   - 适应任何 agent 配置
   - 易于扩展和维护

3. **符合官方最佳实践**
   - 使用 `AgentWorkflowBuilder`
   - 使用 `StreamingRun` 和 `WorkflowEvent`
   - 参考官方示例实现

4. **可追踪**
   - 通过 `ExecutorId` 知道哪个 agent 在执行
   - 可以记录 agent 切换历史
   - 便于调试和优化

---

## 📝 总结

通过这次重构，我们实现了：

✅ 真正的 Handoff 模式（参考官方示例）  
✅ 动态生成 Triage Agent 提示词（不硬编码）  
✅ 智能 AI 路由（不依赖 @mention）  
✅ 完全通用的架构（易于扩展）  
✅ 符合 Agent Framework 最佳实践  

这是一个**生产就绪**的多 agent 协作系统！🎉
