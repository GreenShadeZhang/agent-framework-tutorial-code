# Workflow 模式下 Tool 调用问题分析

## 🔍 问题现象

在 Workflow 模式下,Specialist Agent 能够理解自己拥有生图的 MCP tool,但执行时返回的消息中**不包含生成的图片 URL**。

## 🎯 问题根源

### 1. Agent Framework Workflow 的工作机制

根据对 Agent Framework 源码的分析,**Workflow 模式下 Agent 的 Tool 调用确实会被执行**,但问题在于:

#### **关键发现:**
在 Workflow 模式中,Agent 通过 `RunStreamingAsync` 方法执行,该方法会:
1. ✅ **正常调用 LLM**
2. ✅ **LLM 返回 Function Call**
3. ✅ **自动执行 Tool (通过 FunctionInvokingChatClient)**
4. ❌ **Tool 执行结果只被发送回 LLM,不直接出现在最终响应的文本中**

### 2. 消息处理流程分析

#### 当前 `AgentChatService.cs` 的处理逻辑:

```csharp
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is AgentRunUpdateEvent agentUpdate)
    {
        // 只提取文本内容
        if (currentSummary != null)
        {
            currentSummary.Content += agentUpdate.Update.Text; // ❌ 只累积文本
        }

        // 检测函数调用(仅用于日志)
        if (agentUpdate.Update.Contents.OfType<FunctionCallContent>().FirstOrDefault() is FunctionCallContent call)
        {
            _logger?.LogDebug("Agent {ExecutorId} calling function: {FunctionName} with args: {Args}",
                currentExecutorId, call.Name, JsonSerializer.Serialize(call.Arguments));
        }
        // ❌ 但没有提取 FunctionResultContent!
    }
}
```

#### **问题所在:**

1. **只累积 `agentUpdate.Update.Text`**,这是 LLM 生成的普通文本
2. **没有处理 `FunctionResultContent`**,这才是 Tool 执行的结果!

### 3. Agent Framework Tool 执行流程

根据源码分析,Tool 的执行流程如下:

```
User Message
    ↓
Workflow.RunStreamingAsync
    ↓
ChatClientAgent.RunStreamingAsync
    ↓
FunctionInvokingChatClient.GetStreamingResponseAsync
    ↓
[LLM 返回 FunctionCallContent]
    ↓
[自动执行 Tool -> FunctionResultContent]
    ↓
[将 FunctionResultContent 发送回 LLM]
    ↓
[LLM 基于 Tool 结果生成最终文本]
    ↓
AgentRunUpdateEvent (包含所有 Contents)
```

**关键点:**
- `AgentRunUpdateEvent.Update.Contents` 包含:
  - `TextContent` (普通文本)
  - `FunctionCallContent` (函数调用)
  - `FunctionResultContent` (函数结果 - **包含图片URL!**)

## 🔧 解决方案

### 方案1: 修改消息处理逻辑,提取 Tool 执行结果

修改 `AgentChatService.cs` 中的消息处理逻辑:

```csharp
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is AgentRunUpdateEvent agentUpdate)
    {
        // 跳过 triage agent
        var executorIdPrefix = agentUpdate.ExecutorId.Contains('_') 
            ? agentUpdate.ExecutorId.Split('_')[0] 
            : agentUpdate.ExecutorId;
        
        if (executorIdPrefix.Equals("triage", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        // 检测到新的 specialist agent
        if (agentUpdate.ExecutorId != currentExecutorId)
        {
            currentExecutorId = agentUpdate.ExecutorId;
            var profile = GetAgentProfile(currentExecutorId);
            
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

        if (currentSummary != null)
        {
            // ✅ 1. 累积文本内容
            currentSummary.Content += agentUpdate.Update.Text;

            // ✅ 2. 提取 FunctionResultContent (Tool 执行结果)
            foreach (var content in agentUpdate.Update.Contents)
            {
                if (content is FunctionResultContent functionResult)
                {
                    _logger?.LogInformation(
                        "Tool execution result from {AgentId}: CallId={CallId}, Result={Result}",
                        currentExecutorId, 
                        functionResult.CallId, 
                        functionResult.Result);

                    // 解析并提取图片 URL
                    var resultText = functionResult.Result?.ToString() ?? "";
                    
                    // 如果结果是 JSON,尝试解析图片 URL
                    try
                    {
                        var resultObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(resultText);
                        if (resultObj != null && resultObj.ContainsKey("url"))
                        {
                            var imageUrl = resultObj["url"].GetString();
                            if (!string.IsNullOrEmpty(imageUrl))
                            {
                                // 将图片 URL 添加到消息内容中
                                currentSummary.Content += $"\n\n![Generated Image]({imageUrl})";
                                
                                _logger?.LogInformation(
                                    "Extracted image URL from tool result: {ImageUrl}", 
                                    imageUrl);
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // 如果不是 JSON,直接追加结果文本
                        currentSummary.Content += $"\n\nTool Result: {resultText}";
                    }
                }
                else if (content is FunctionCallContent functionCall)
                {
                    _logger?.LogDebug(
                        "Agent {ExecutorId} calling function: {FunctionName} with args: {Args}",
                        currentExecutorId, 
                        functionCall.Name, 
                        JsonSerializer.Serialize(functionCall.Arguments));
                }
            }
        }
    }
}
```

### 方案2: 依赖 LLM 在响应文本中包含 URL

如果 MCP Tool 返回的结果被正确发送回 LLM,LLM 应该会在其响应文本中包含图片 URL。但这取决于:

1. **Tool 的返回格式是否正确**
2. **LLM 的 System Prompt 是否指示它输出图片 URL**

#### 建议的 Specialist Agent System Prompt 增强:

```csharp
var specialistAgents = agentProfiles.Select(profile =>
    new ChatClientAgent(
        _chatClient,
        instructions: profile.SystemPrompt +
            "\n\nIMPORTANT: If the user asks about something outside your expertise, " +
            "you can suggest they ask another agent, but still provide a helpful response." +
            "\n\nWhen using tools that generate images or files, ALWAYS include the returned URLs or file paths in your response.",  // ✅ 新增
        name: profile.Id,
        description: profile.Description,
        tools: [.. mcpTools])
).ToList();
```

## 📊 验证步骤

### 1. 添加详细日志

在 `AgentChatService.cs` 中添加以下日志:

```csharp
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is AgentRunUpdateEvent agentUpdate)
    {
        // 记录所有 Contents
        _logger?.LogDebug(
            "AgentRunUpdateEvent from {ExecutorId}: Text='{Text}', Contents Count={Count}",
            agentUpdate.ExecutorId,
            agentUpdate.Update.Text,
            agentUpdate.Update.Contents.Count);

        foreach (var content in agentUpdate.Update.Contents)
        {
            _logger?.LogDebug(
                "  Content Type: {Type}, Details: {Details}",
                content.GetType().Name,
                JsonSerializer.Serialize(content));
        }
    }
}
```

### 2. 检查 MCP Tool 返回格式

确认 DashScope MCP Tool 返回的格式,应该类似:

```json
{
  "url": "https://dashscope.aliyuncs.com/...",
  "taskId": "...",
  "status": "success"
}
```

### 3. 测试 LLM 是否收到 Tool 结果

通过日志确认:
1. `FunctionCallContent` 被记录 -> Tool 被调用
2. `FunctionResultContent` 被记录 -> Tool 执行完成
3. 后续的 `TextContent` 是否包含 URL -> LLM 是否正确处理结果

## 🎯 推荐实施顺序

1. **立即实施:** 添加详细日志 (方案1的日志部分)
2. **运行测试:** 查看日志确认 Tool 是否被执行,结果是否被捕获
3. **根据日志选择:**
   - 如果看到 `FunctionResultContent` 包含 URL -> 实施方案1提取逻辑
   - 如果 LLM 文本响应中已有 URL -> 无需修改
   - 如果完全没有 `FunctionResultContent` -> 检查 Tool 配置

## 🔗 相关 Agent Framework 代码参考

### Tool 调用流程核心代码:

1. **AgentProviderExtensions.InvokeAgentAsync** (workflow 调用 agent)
   ```csharp
   // dotnet/src/Microsoft.Agents.AI.Workflows.Declarative/Extensions/AgentProviderExtensions.cs
   IAsyncEnumerable<AgentRunResponseUpdate> agentUpdates =
       inputMessages is not null ?
           agent.RunStreamingAsync([.. inputMessages], null, options, cancellationToken) :
           agent.RunStreamingAsync(null, options, cancellationToken);
   ```

2. **AIAgentHostExecutor.TakeTurnAsync** (workflow 执行 agent)
   ```csharp
   // dotnet/src/Microsoft.Agents.AI.Workflows/Specialized/AIAgentHostExecutor.cs
   await foreach (AgentRunResponseUpdate update in agentStream.ConfigureAwait(false))
   {
       await context.AddEventAsync(new AgentRunUpdateEvent(this.Id, update), cancellationToken);
       updates.Add(update);
   }
   ```

3. **FunctionInvokingChatClient** 自动处理 tool 调用
   - 检测 `FunctionCallContent`
   - 执行工具
   - 生成 `FunctionResultContent`
   - 将结果发送回 LLM

## 📝 总结

**核心问题:** 在 Workflow 模式下,Tool **确实会被执行**,但当前的消息处理逻辑**只提取了文本内容,忽略了 FunctionResultContent**。

**解决方案:** 修改 `AgentChatService.cs` 的消息处理逻辑,从 `AgentRunUpdateEvent.Update.Contents` 中提取 `FunctionResultContent`,并将图片 URL 添加到响应内容中。

**验证重点:** 
1. 确认 `FunctionResultContent` 是否存在于事件流中
2. 确认 Tool 返回的数据格式
3. 根据实际情况选择是提取 `FunctionResultContent` 还是依赖 LLM 在文本中包含 URL
