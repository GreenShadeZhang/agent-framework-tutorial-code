# 框架级日志诊断指南

## 概述

为了帮助你排查聊天功能中的未知错误，我们启用了 **Microsoft.Extensions.AI** 框架内置的详细日志功能。

## 新增功能

### 1. 框架内置的 ChatClient 日志

使用 `ChatClientBuilder.UseLogging()` 方法启用框架级别的日志拦截：

```csharp
return new ChatClientBuilder(baseChatClient)
    .UseLogging(loggerFactory)
    .Build();
```

**框架自动记录的信息包括：**

- ✅ 每个请求的详细参数
- ✅ 发送到 LLM 的完整消息列表
- ✅ 消息内容（包括 System、User、Assistant 消息）
- ✅ Tool 调用详情（函数名、参数、CallId）
- ✅ Tool 执行结果
- ✅ LLM 返回的内容
- ✅ Token 使用情况（输入/输出/总计）
- ✅ 完成原因（FinishReason）
- ✅ 流式响应的所有 Chunk
- ✅ 详细的错误堆栈和内部异常

### 2. 增强的错误日志

在 `AgentChatService.SendMessageAsync` 中：

- ✅ 记录会话开始和关键步骤
- ✅ 记录历史消息加载过程
- ✅ 记录 Workflow 创建和执行
- ✅ 详细的异常信息（包括类型、消息、堆栈）
- ✅ 内部异常链追踪

### 3. 日志级别配置

**开发环境 (`appsettings.Development.json`):**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "ChatClient": "Debug",
      "Microsoft.Extensions.AI": "Debug",
      "Microsoft.Extensions.AI.OpenAI": "Debug",
      "AgentGroupChat.AgentHost.Services.AgentChatService": "Debug",
      "AgentGroupChat.AgentHost.Services.WorkflowManager": "Debug",
      ...
    }
  }
}
```

## 日志示例

### 正常流程日志

```
� Initializing OpenAI client: BaseUrl=default, Model=gpt-4o-mini

�🚀 Starting SendMessageAsync | SessionId: abc123 | GroupId: default | Message Length: 50
📝 User Message: 请帮我生成一张图片
📚 Loading message history for session abc123
📚 Loaded 10 historical messages
📋 Total messages prepared for LLM: 11 (History: 10 + Current: 1)
🔧 Getting workflow for group default
✅ Workflow ready for group default
▶️ Starting workflow execution...
📡 Workflow started, watching event stream...

dbug: Microsoft.Extensions.AI[1]
      ChatClient invoking GetResponseAsync with 11 messages.
dbug: Microsoft.Extensions.AI[2]  
      Request: {"messages":[{"role":"user","content":"你好"},{"role":"assistant","content":"你好！有什么我可以帮助你的吗？"},...]}
dbug: Microsoft.Extensions.AI[3]
      Response received in 1523ms. Tokens: Input=256, Output=48, Total=304
dbug: Microsoft.Extensions.AI[4]
      FinishReason: ToolCalls
dbug: Microsoft.Extensions.AI[5]
      Tool call: generate_image({"prompt":"一只可爱的猫"})

🔧 Tool Call | Agent: artist | Function: generate_image | Args: {"prompt":"一只可爱的猫"}
✅ Tool Result | Agent: artist | CallId: call_xyz | Result Preview: {"imageUrl":"https://..."}

🟢 Returning 1 filtered responses for session abc123
```

### 错误日志

```
🚀 Starting SendMessageAsync | SessionId: abc123 | GroupId: default | Message Length: 20
📚 Loading message history for session abc123
📚 Loaded 5 historical messages

fail: Microsoft.Extensions.AI[100]
      ChatClient GetResponseAsync failed.
      System.Net.Http.HttpRequestException: The remote server returned an error: (429) Too Many Requests
         at System.Net.Http.HttpClient.SendAsync(...)

🔴 Critical Error in SendMessageAsync | SessionId: abc123 | GroupId: default | 
Exception Type: System.Net.Http.HttpRequestException | 
Message: The remote server returned an error: (429) Too Many Requests | 
StackTrace: at System.Net.Http.HttpClient.SendAsync(...)
  ↳ Inner Exception [1] | Type: System.Net.WebException | Message: The remote server returned an error: (429) Too Many Requests
```

## 如何使用

### 1. 启动应用

```bash
cd src\AgentGroupChat.AppHost
dotnet run
```

### 2. 查看实时日志

日志会输出到控制台，框架内置的日志使用标准的日志级别：

- `dbug` = Debug 级别
- `info` = Information 级别  
- `warn` = Warning 级别
- `fail` = Error 级别

### 3. 查找特定会话

在日志中搜索会话 ID：

```powershell
# 在控制台日志中搜索
Select-String -Pattern "SessionId: abc123" -Path console.log
```

### 4. 调试步骤

当遇到错误时：

1. **查找错误日志** - 搜索 `🔴` 或 `Error`
2. **获取 SessionId** - 从错误日志中找到会话 ID
3. **查找该会话的所有请求** - 搜索 `SessionId: xxx`
4. **检查请求参数** - 查看发送给 LLM 的消息
5. **检查响应** - 查看 LLM 返回的内容
6. **检查 Tool 调用** - 查看是否有 Tool 调用失败

### 5. 常见错误模式

**Token 超限：**
```
fail: Microsoft.Extensions.AI[100]
      ChatClient GetResponseAsync failed: maximum context length exceeded
→ 解决方案：清除历史消息或减少上下文
```

**API 密钥错误：**
```
🔴 Critical Error | Exception Type: System.InvalidOperationException | Message: API key not configured
→ 解决方案：检查 appsettings.json 中的 API 密钥
```

**Tool 调用失败：**
```
🔧 Tool Call | Function: generate_image
✅ Tool Result | Result: {"error":"Invalid API key"}
→ 解决方案：检查 MCP 服务配置
```

**网络超时：**
```
fail: Microsoft.Extensions.AI[100]
      ChatClient GetResponseAsync failed: The operation has timed out
→ 解决方案：检查网络连接或增加超时时间
```

## 性能考虑

- **Debug 日志有性能开销**：在生产环境使用 `Information` 或 `Warning` 级别
- **框架会自动截断大型内容**：避免日志过大
- **结构化日志**：框架使用结构化日志格式，便于分析

## 生产环境配置

在 `appsettings.json`（生产环境）中使用较低的日志级别：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Extensions.AI": "Warning",
      "AgentGroupChat.AgentHost.Services": "Warning"
    }
  }
}
```

这样只会记录：
- 警告和错误
- 关键的性能指标

## 自定义日志

如果需要更多日志，可以：

1. **修改日志级别** - 在 `appsettings.Development.json` 中调整
2. **添加自定义日志** - 在你的代码中使用 `_logger.LogDebug/Information/Warning/Error`
3. **使用结构化日志** - 所有日志都使用了结构化格式，便于查询和分析

## 日志分析工具

推荐使用以下工具分析日志：

- **Seq** - 免费的结构化日志查看器
- **Application Insights** - Azure 云端日志分析
- **Serilog** - 可以输出到多种目标（文件、数据库、云服务）

## 示例：追踪一个完整请求

```
# 1. 初始化
🔧 Initializing OpenAI client: BaseUrl=default, Model=gpt-4o-mini

# 2. 用户发送消息
🚀 Starting SendMessageAsync | SessionId: s1 | GroupId: default
📝 User Message: 帮我画一只猫

# 3. 加载历史
📚 Loaded 0 historical messages
📋 Total messages: 1

# 4. 创建 Workflow
🔧 Getting workflow for group default
✅ Workflow ready

# 5. 框架日志：LLM 请求
dbug: Microsoft.Extensions.AI[1]
      ChatClient invoking GetResponseAsync with 1 messages.
dbug: Microsoft.Extensions.AI[2]
      Request messages: [{"role":"user","content":"帮我画一只猫"}]

# 6. 框架日志：LLM 响应（需要调用 Tool）
dbug: Microsoft.Extensions.AI[3]
      Response received in 1200ms
dbug: Microsoft.Extensions.AI[4]
      FinishReason: ToolCalls
dbug: Microsoft.Extensions.AI[5]
      Tool call: generate_image({"prompt":"一只可爱的猫"})

# 7. 执行 Tool
🔧 Tool Call | Agent: artist | Function: generate_image
✅ Tool Result | CallId: call_1 | Result: {"imageUrl":"https://..."}

# 8. 框架日志：LLM 再次请求（处理 Tool 结果）
dbug: Microsoft.Extensions.AI[1]
      ChatClient invoking GetResponseAsync with 3 messages.

# 9. 框架日志：LLM 最终响应
dbug: Microsoft.Extensions.AI[3]
      Response received in 800ms
dbug: Microsoft.Extensions.AI[4]
      FinishReason: Stop

# 10. 保存消息
✅ Saved 2 messages to LiteDB

# 11. 完成
🟢 Returning 1 filtered responses
```

## 故障排查清单

- [ ] 检查 API 密钥是否正确配置
- [ ] 检查网络连接
- [ ] 查看是否有 Token 超限错误
- [ ] 检查 MCP 服务是否正常运行
- [ ] 查看是否有 Tool 调用失败
- [ ] 检查数据库连接
- [ ] 查看是否有异常堆栈
- [ ] 检查会话历史是否过长

## 总结

现在你有了完整的框架级日志诊断能力：

✅ **使用 Microsoft.Extensions.AI 内置日志功能**
✅ **框架级别的请求/响应拦截**
✅ **详细的参数和结果记录**
✅ **Tool 调用的完整追踪**
✅ **性能指标（耗时、Token 使用）**
✅ **结构化的错误信息**
✅ **内部异常链追踪**

这些日志应该能帮助你快速定位问题的根源！

## 优势

相比自定义的日志包装器，使用框架内置的日志功能有以下优势：

1. **官方支持**：由 Microsoft 维护，与框架同步更新
2. **标准格式**：使用标准的日志格式和级别
3. **更少代码**：无需维护自定义包装器
4. **更好集成**：与其他 Microsoft.Extensions.AI 功能无缝集成
5. **更高性能**：框架级别优化
