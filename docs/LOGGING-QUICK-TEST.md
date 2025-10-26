# 日志诊断快速测试

## 测试步骤

### 1. 启动应用（观察初始化日志）

```bash
cd src\AgentGroupChat.AppHost
dotnet run
```

预期日志：
```
info: ChatClient[0]
      Initializing Azure OpenAI client: Endpoint=https://xxx, Deployment=gpt-4o-mini
info: AgentGroupChat.AgentHost.Services.WorkflowManager[0]
      Created and cached new workflow for group default
```

### 2. 发送一条简单消息

打开浏览器访问前端，发送消息：`你好`

预期日志：
```
🚀 Starting SendMessageAsync | SessionId: xxx | GroupId: default | Message Length: 6
📝 User Message: 你好
📚 Loaded 0 historical messages
📋 Total messages prepared for LLM: 1

🔵 [Request #1] Starting CompleteAsync | Messages: 1 | Model: gpt-4o-mini
📤 [Request #1] Chat Messages Details:
  [0] Role: User | Text: 你好 | Contents: 1

🟢 [Response #1] Completed in XXXXms | FinishReason: Stop | Tokens: XX/XX
📝 Text: 你好！有什么我可以帮助你的吗？

✅ Saved 2 messages to LiteDB
🟢 Returning 1 filtered responses
```

### 3. 发送需要调用 Tool 的消息

发送消息：`帮我生成一张猫的图片`

预期日志：
```
🚀 Starting SendMessageAsync | SessionId: xxx | GroupId: default
📝 User Message: 帮我生成一张猫的图片
📚 Loaded 2 historical messages
📋 Total messages: 3

🔵 [Request #1] Starting CompleteAsync | Messages: 3
⚙️ [Request #1] Options | Tools: 5
  🔨 Tool Available | Name: generate_image

🟢 [Response #1] Completed in XXXXms | FinishReason: ToolCalls
🔧 [Response #1] Tool Call Requested | Name: generate_image | Args: {"prompt":"一只猫"}

🔧 Tool Call | Agent: artist | Function: generate_image | Args: {"prompt":"一只猫"}
✅ Tool Result | Agent: artist | Result Preview: {"imageUrl":"..."}

🔵 [Request #2] Starting CompleteAsync | Messages: 5
🟢 [Response #2] Completed in XXXXms | FinishReason: Stop
📝 Text: 我为你生成了一张猫的图片...

✅ Saved 2 messages to LiteDB
```

### 4. 触发错误（测试错误日志）

方法 1：使用错误的 API Key

修改 `appsettings.Development.json`：
```json
"OpenAI": {
  "ApiKey": "invalid-key"
}
```

重启应用并发送消息，预期日志：
```
🔴 [Error #1] CompleteAsync failed after XXXms | Error: Incorrect API key provided
🔴 Critical Error in SendMessageAsync | 
Exception Type: System.ClientModel.ClientResultException | 
Message: Incorrect API key provided
```

方法 2：发送超长消息（触发 Token 限制）

发送一条非常长的消息（复制粘贴大量文本），预期日志：
```
🔴 [Error #1] CompleteAsync failed | Error: maximum context length exceeded
```

### 5. 查看详细的 Tool 执行日志

发送消息：`请用文生图工具帮我生成一张美丽的风景图`

在日志中搜索：
- `🔧 Tool Call` - 查看 Tool 调用
- `✅ Tool Result` - 查看 Tool 结果

### 6. 检查性能指标

在日志中搜索 `Completed in` 查看每个请求的耗时：
```
🟢 [Response #1] Completed in 1523ms
```

在日志中搜索 `Token Usage` 查看 Token 使用：
```
📊 [Response #1] Token Usage | Input: 256 | Output: 48 | Total: 304
```

## 常见问题排查

### 问题：看不到 Debug 日志

检查：
```json
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"  // 确保是 Debug
    }
  }
}
```

### 问题：日志太多，难以阅读

临时解决：
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",  // 减少其他日志
      "ChatClient": "Debug",      // 只保留关键日志
      "AgentChatService": "Debug"
    }
  }
}
```

### 问题：需要将日志保存到文件

安装 Serilog：
```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.File
```

修改 `Program.cs`：
```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();
```

## 日志筛选技巧

### PowerShell 筛选

```powershell
# 只显示错误
dotnet run 2>&1 | Select-String "🔴|Error"

# 只显示 LLM 请求/响应
dotnet run 2>&1 | Select-String "🔵|🟢"

# 只显示 Tool 调用
dotnet run 2>&1 | Select-String "🔧|✅"

# 保存到文件
dotnet run 2>&1 | Tee-Object -FilePath "app.log"
```

### 在日志文件中搜索

```powershell
# 查找特定会话的所有日志
Select-String -Path "logs\app-*.log" -Pattern "SessionId: abc123"

# 查找所有错误
Select-String -Path "logs\app-*.log" -Pattern "🔴|Error"

# 查找特定请求
Select-String -Path "logs\app-*.log" -Pattern "Request #5"
```

## 验证清单

- [ ] 能看到应用初始化日志（ChatClient 初始化）
- [ ] 能看到每条消息的开始日志（🚀）
- [ ] 能看到 LLM 请求日志（🔵）
- [ ] 能看到 LLM 响应日志（🟢）
- [ ] 能看到 Tool 调用日志（🔧）
- [ ] 能看到 Tool 结果日志（✅）
- [ ] 能看到消息保存日志（Saved X messages）
- [ ] 能看到返回结果日志（Returning X responses）
- [ ] 错误时能看到详细的堆栈（🔴）
- [ ] 能看到 Token 使用统计（📊）

## 成功标志

如果你能在日志中看到以下内容，说明日志系统工作正常：

✅ 每个请求都有唯一 ID
✅ 能追踪完整的请求->响应流程
✅ 能看到发送给 LLM 的所有参数
✅ 能看到 LLM 返回的所有内容
✅ Tool 调用过程清晰可见
✅ 错误有详细的堆栈和内部异常
✅ 性能数据（耗时、Token）完整

现在你可以开始测试并排查问题了！🎉
