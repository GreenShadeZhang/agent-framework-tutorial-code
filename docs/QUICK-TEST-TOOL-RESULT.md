# 快速测试指南 - Tool 结果提取修复

## 🚀 快速测试步骤

### 1. 启动应用

```powershell
cd src\AgentGroupChat.AppHost
dotnet run
```

### 2. 测试文生图功能

#### 方法1: 使用 PowerShell

```powershell
# 创建新会话
$sessionResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/sessions" -Method Post
$sessionId = $sessionResponse.id

# 发送文生图请求
$body = @{
    sessionId = $sessionId
    message = "@Artsy 请帮我生成一张美丽的夕阳风景画"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:5000/api/chat" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"

# 查看响应
$response | ConvertTo-Json -Depth 10
```

#### 方法2: 使用 Web UI

1. 打开浏览器访问 `http://localhost:5173`
2. 输入: `@Artsy 请帮我生成一张美丽的夕阳风景画`
3. 查看返回的消息是否包含图片

### 3. 查看日志

在应用运行的控制台中，查找以下日志条目：

#### ✅ 成功的日志标志

```
[Debug] Processing AgentRunUpdateEvent from Artsy: Text='...', Contents Count=2
[Debug]   Content Type: TextContent
[Debug]   Content Type: FunctionCallContent
[Information] Agent Artsy calling function: text_to_image with args: {"prompt":"美丽的夕阳风景画",...}

[Debug] Processing AgentRunUpdateEvent from Artsy: Text='', Contents Count=1
[Debug]   Content Type: FunctionResultContent
[Information] Tool execution result from Artsy: CallId=call_xxx, Result type=String
[Debug] Raw tool result for Artsy: {"url":"https://...", "taskId":"..."}
[Information] Extracted image URL from field 'url': https://...
[Information] Appended tool result to message content for agent Artsy
```

### 4. 验证响应格式

检查返回的消息内容是否包含：

#### 情况1: 仅 Markdown 图片（最常见）
```markdown
这是我为您生成的美丽夕阳风景画。

![Generated Image](https://dashscope.aliyuncs.com/api/v1/services/aigc/text2image/image-synthesis/xxx/output/image.jpg)
```

#### 情况2: LLM 文本 + Markdown 图片
```markdown
好的，我已经为您生成了一张美丽的夕阳风景画。图片已经生成完成。

![Generated Image](https://dashscope.aliyuncs.com/...)
```

#### 情况3: JSON 格式（兜底）
```markdown
这是生成结果：

```json
{
  "url": "https://...",
  "taskId": "...",
  "status": "success"
}
```
```

## 🔍 故障排查

### 问题1: 没有看到图片 URL

**检查项:**
1. 查看日志中是否有 `FunctionResultContent`
2. 查看日志中是否有 `Extracted image URL`
3. 检查 MCP 服务器配置是否正确

**解决方案:**
```powershell
# 检查 MCP 服务器状态
Invoke-RestMethod -Uri "http://localhost:5000/api/mcp/servers" | ConvertTo-Json
```

预期响应：
```json
[
  {
    "id": "dashscope-text-to-image",
    "name": "DashScope Text-to-Image",
    "toolCount": 5,
    "isConnected": true
  }
]
```

### 问题2: 看到 FunctionCallContent 但没有 FunctionResultContent

**可能原因:**
- Tool 执行失败
- 认证问题（Token 无效）
- 网络问题

**检查日志:**
```
[Error] Failed to execute tool: ...
```

**解决方案:**
1. 检查 `appsettings.json` 中的 `BearerToken`
2. 检查网络连接
3. 查看 MCP 服务器的详细错误日志

### 问题3: 返回的是 JSON 而不是图片

**可能原因:**
- Tool 返回的格式与预期不同
- URL 字段名不在预定义列表中

**解决方案:**
查看日志中的 `Raw tool result`，然后修改 `ExtractToolResult` 方法中的字段列表：

```csharp
// 在 AgentChatService.cs 中添加新的字段名
var imageUrlFields = new[] { 
    "url", 
    "image_url", 
    "imageUrl", 
    "output_url", 
    "result_url",
    "你的新字段名"  // ← 添加这里
};
```

### 问题4: 图片显示不出来

**可能原因:**
- URL 需要认证
- URL 已过期
- CORS 问题

**解决方案:**
1. 在浏览器中直接访问 URL 测试
2. 检查 URL 的有效期
3. 联系 MCP 服务提供商

## 📊 预期的完整日志流

### 成功的执行流程：

```
[Debug] Processing message for session xxx using group default: @Artsy 请帮我生成一张美丽的夕阳风景画
[Debug] Agent switched to: Artsy (Artsy)
[Debug] Created summary for specialist agent Artsy

# 第一轮更新 - LLM 决定调用工具
[Debug] Processing AgentRunUpdateEvent from Artsy: Text='', Contents Count=1
[Debug]   Content Type: FunctionCallContent
[Information] Agent Artsy calling function: text_to_image with args: {"prompt":"美丽的夕阳风景画","size":"1024x1024"}

# 第二轮更新 - 工具返回结果
[Debug] Processing AgentRunUpdateEvent from Artsy: Text='', Contents Count=1
[Debug]   Content Type: FunctionResultContent
[Information] Tool execution result from Artsy: CallId=call_123, Result type=String
[Debug] Raw tool result for Artsy: {"url":"https://dashscope.aliyuncs.com/...","taskId":"xxx"}
[Information] Extracted image URL from field 'url': https://dashscope.aliyuncs.com/...
[Information] Appended tool result to message content for agent Artsy

# 第三轮更新 - LLM 基于工具结果生成回复
[Debug] Processing AgentRunUpdateEvent from Artsy: Text='这是我为您生成的美丽夕阳风景画。', Contents Count=1
[Debug]   Content Type: TextContent

[Debug] Workflow completed for session xxx
[Information] Collected 1 agent responses for session xxx
[Information] Returning 1 filtered responses for session xxx
```

## ✅ 成功标准

测试通过需要满足：

1. ✅ 日志中看到 `FunctionCallContent` → Tool 被调用
2. ✅ 日志中看到 `FunctionResultContent` → Tool 执行完成
3. ✅ 日志中看到 `Extracted image URL` → URL 被提取
4. ✅ 日志中看到 `Appended tool result` → URL 被添加到消息
5. ✅ 响应中包含 `![Generated Image](...)` → 用户看到图片

## 🎯 下一步

测试通过后，可以：

1. **关闭详细日志**（生产环境）
   ```json
   {
     "Logging": {
       "LogLevel": {
         "AgentGroupChat.AgentHost.Services.AgentChatService": "Information"
       }
     }
   }
   ```

2. **测试其他 MCP 工具**
3. **自定义字段提取逻辑**（如果需要）
4. **添加更多格式支持**（如视频、文档等）

## 📝 反馈

如果遇到问题或有改进建议，请：

1. 检查详细日志
2. 记录 `Raw tool result` 的格式
3. 提供完整的错误信息
4. 说明预期行为 vs 实际行为
