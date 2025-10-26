# Workflow Tool 结果提取修复说明

## 📝 问题描述

在 Workflow 模式下，Specialist Agent 能够调用 MCP Tool（如文生图工具），但返回的消息中不包含生成的图片 URL。

## 🔍 根本原因

原代码只提取了 `AgentRunUpdateEvent.Update.Text`（LLM 生成的文本），而没有处理 `FunctionResultContent`（Tool 执行结果）。

## ✅ 修复方案

### 1. 增强的消息处理逻辑

修改 `AgentChatService.cs` 的 `SendMessageAsync` 方法，完整处理所有类型的 Content：

```csharp
// 处理所有 Content 类型
foreach (var content in agentUpdate.Update.Contents)
{
    switch (content)
    {
        case FunctionCallContent functionCall:
            // 记录函数调用
            break;

        case FunctionResultContent functionResult:
            // ✅ 提取工具执行结果
            var toolResult = ExtractToolResult(functionResult, currentExecutorId);
            if (!string.IsNullOrEmpty(toolResult))
            {
                currentSummary.Content += toolResult;
            }
            break;

        case TextContent textContent:
            // 文本内容已经通过 agentUpdate.Update.Text 处理
            break;

        case DataContent dataContent:
            // 处理数据内容
            break;
    }
}
```

### 2. 智能的结果提取方法

新增 `ExtractToolResult` 方法，支持多种数据格式：

#### 策略1: JSON 格式解析
```csharp
// 查找常见的图片URL字段
var imageUrlFields = new[] { "url", "image_url", "imageUrl", "output_url", "result_url" };
foreach (var field in imageUrlFields)
{
    if (root.TryGetProperty(field, out var urlElement))
    {
        var imageUrl = urlElement.GetString();
        if (!string.IsNullOrEmpty(imageUrl))
        {
            // 返回 Markdown 格式的图片链接
            return $"\n\n![Generated Image]({imageUrl})\n";
        }
    }
}
```

#### 策略2: 纯文本处理
```csharp
// 检查是否是图片URL
if (Uri.IsWellFormedUriString(resultText, UriKind.Absolute))
{
    if (resultText.Contains(".jpg") || resultText.Contains(".png") ...)
    {
        return $"\n\n![Generated Image]({resultText})\n";
    }
    return $"\n\n{resultText}\n";
}
```

#### 策略3: 兜底方案
```csharp
// 格式化 JSON 输出
var jsonString = JsonSerializer.Serialize(root, new JsonSerializerOptions 
{ 
    WriteIndented = true 
});
return $"\n\n```json\n{jsonString}\n```\n";
```

### 3. 增强的日志记录

添加详细的调试日志：

```csharp
_logger?.LogDebug(
    "Processing AgentRunUpdateEvent from {ExecutorId}: Text='{Text}', Contents Count={Count}",
    currentExecutorId,
    agentUpdate.Update.Text,
    agentUpdate.Update.Contents.Count);

_logger?.LogDebug("  Content Type: {Type}", content.GetType().Name);

_logger?.LogInformation(
    "Tool execution result from {AgentId}: CallId={CallId}, Result type={ResultType}",
    currentExecutorId,
    functionResult.CallId,
    functionResult.Result?.GetType().Name ?? "null");
```

## 🎯 关键改进点

### 1. **完整的 Content 处理**
- ✅ 不再只处理文本，而是遍历所有 Content
- ✅ 针对不同类型采用不同策略
- ✅ 优雅降级，确保不会丢失任何信息

### 2. **多格式支持**
- ✅ JSON 格式（标准 MCP 响应）
- ✅ 纯文本 URL
- ✅ 图片文件扩展名检测
- ✅ 兜底方案（格式化 JSON）

### 3. **Markdown 输出**
- ✅ 图片使用 `![alt](url)` 格式
- ✅ JSON 使用代码块 ` ```json ` 格式
- ✅ 适当的换行和格式化

### 4. **健壮的错误处理**
- ✅ Try-catch 保护每个解析步骤
- ✅ 详细的错误日志
- ✅ 失败时返回空字符串，不影响主流程

### 5. **详细的日志**
- ✅ 记录每个 Content 的类型
- ✅ 记录提取的 URL 和字段
- ✅ 记录解析过程中的关键信息
- ✅ 便于调试和监控

## 📊 测试建议

### 1. 启用详细日志
在 `appsettings.json` 中设置：
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "AgentGroupChat.AgentHost.Services.AgentChatService": "Debug"
    }
  }
}
```

### 2. 测试用例

#### 测试1: 文生图功能
```
用户消息: @Artsy 请帮我生成一张美丽的夕阳风景画
预期结果: 返回包含图片 Markdown 链接的消息
```

#### 测试2: 查看日志
检查日志中是否有：
- ✅ `Processing AgentRunUpdateEvent from ...`
- ✅ `Content Type: FunctionCallContent`
- ✅ `Tool execution result from ...`
- ✅ `Content Type: FunctionResultContent`
- ✅ `Extracted image URL from field 'url': ...`

#### 测试3: 验证响应格式
检查返回的消息是否包含：
```markdown
![Generated Image](https://dashscope.aliyuncs.com/...)
```

## 🔧 可能需要的额外配置

### 1. 确保 MCP Tool 配置正确

检查 `appsettings.json`:
```json
{
  "McpServers": {
    "Servers": [
      {
        "Id": "dashscope-text-to-image",
        "Name": "DashScope Text-to-Image",
        "Endpoint": "https://dashscope.aliyuncs.com/api/v1/mcps/TextToImage/sse",
        "AuthType": "Bearer",
        "BearerToken": "your-token-here",
        "TransportMode": "Sse",
        "Enabled": true
      }
    ]
  }
}
```

### 2. 增强 Specialist Agent 的 System Prompt

在 `WorkflowManager.cs` 中：
```csharp
var specialistAgents = agentProfiles.Select(profile =>
    new ChatClientAgent(
        _chatClient,
        instructions: profile.SystemPrompt +
            "\n\nWhen using tools that generate images or files, " +
            "ALWAYS include the returned URLs in your response. " +
            "If a tool returns a URL, mention it in your answer.",
        name: profile.Id,
        description: profile.Description,
        tools: [.. mcpTools])
).ToList();
```

## 📈 预期效果

### 修复前
```
用户: @Artsy 请帮我生成一张美丽的夕阳风景画
Agent: 好的，我已经为您生成了一张美丽的夕阳风景画。
```

### 修复后
```
用户: @Artsy 请帮我生成一张美丽的夕阳风景画
Agent: 好的，我已经为您生成了一张美丽的夕阳风景画。

![Generated Image](https://dashscope.aliyuncs.com/api/v1/services/aigc/text2image/image-synthesis/1234567890/output/image.jpg)
```

或者（如果 LLM 已经在文本中包含了 URL）：
```
用户: @Artsy 请帮我生成一张美丽的夕阳风景画
Agent: 好的，我已经为您生成了一张美丽的夕阳风景画，图片链接是：https://dashscope.aliyuncs.com/...

![Generated Image](https://dashscope.aliyuncs.com/api/v1/services/aigc/text2image/image-synthesis/1234567890/output/image.jpg)
```

## 🎓 最佳实践总结

1. **不要假设 Tool 结果的格式** - 使用多种解析策略
2. **始终记录详细日志** - 便于调试和监控
3. **优雅降级** - 即使解析失败也不影响主流程
4. **使用 Markdown** - 提供更好的用户体验
5. **类型安全** - 使用 pattern matching 和类型检查
6. **异常处理** - 每个步骤都有 try-catch 保护

## 🔗 相关文档

- [WORKFLOW-TOOL-CALL-ANALYSIS.md](./WORKFLOW-TOOL-CALL-ANALYSIS.md) - 问题分析文档
- [MCP-INTEGRATION.md](./MCP-INTEGRATION.md) - MCP 集成文档
- [WORKFLOWMANAGER-MCP-INTEGRATION.md](./WORKFLOWMANAGER-MCP-INTEGRATION.md) - WorkflowManager MCP 集成文档
