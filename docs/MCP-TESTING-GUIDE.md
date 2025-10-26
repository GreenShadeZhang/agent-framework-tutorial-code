# MCP 集成测试指南

## ✅ 编译状态

项目已成功编译，所有错误已修复！

### 修复的问题

1. **❌ 问题 1**: `ModelContextProtocol` 包缺少版本号
   - **✅ 修复**: 添加版本号 `0.4.0-preview.3`

2. **❌ 问题 2**: `ListToolsAsync(cancellationToken)` 参数错误
   - **✅ 修复**: 移除 `cancellationToken` 参数，使用无参数版本

3. **❌ 问题 3**: `McpClient.CreateAsync` 参数顺序错误
   - **✅ 修复**: 使用命名参数 `cancellationToken: cancellationToken, loggerFactory: loggerFactory`

4. **❌ 问题 4**: `McpClientTool` 到 `AITool` 类型转换
   - **✅ 修复**: 使用 `.Cast<AITool>().ToList()` 进行转换

## 🧪 测试步骤

### 1. 运行应用

```bash
cd C:\Users\gil\Music\github\agent-framework-tutorial-code\src\AgentGroupChat.AgentHost
dotnet run
```

**预期输出：**
```
info: AgentGroupChat.AgentHost.Services.McpToolService[0]
      Initializing MCP server: DashScope Text-to-Image (https://dashscope.aliyuncs.com/api/v1/mcps/TextToImage/sse)
info: AgentGroupChat.AgentHost.Services.McpToolService[0]
      Successfully initialized MCP server 'DashScope Text-to-Image' with X tools
info: AgentGroupChat.AgentHost.Services.McpToolService[0]
      MCP service initialized with 1 active servers
```

### 2. 检查 MCP 服务器状态

打开浏览器或使用 PowerShell：

```powershell
# 查询 MCP 服务器信息
Invoke-RestMethod -Uri "http://localhost:5000/api/mcp/servers" -Method Get | ConvertTo-Json
```

**预期响应：**
```json
[
  {
    "id": "dashscope-text-to-image",
    "name": "DashScope Text-to-Image",
    "endpoint": "https://dashscope.aliyuncs.com/api/v1/mcps/TextToImage/sse",
    "description": "阿里云 DashScope 文生图服务，用于生成图像",
    "toolCount": 5,
    "isConnected": true
  }
]
```

### 3. 创建测试会话

```powershell
# 创建新会话
$session = Invoke-RestMethod -Uri "http://localhost:5000/api/sessions" -Method Post
$sessionId = $session.id
Write-Host "Created session: $sessionId"
```

### 4. 测试智能体调用 MCP 工具

```powershell
# 发送消息请求生成图像
$body = @{
    sessionId = $sessionId
    message = "@Artsy 请帮我生成一张美丽的夕阳风景画"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:5000/api/chat" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"

$response | ConvertTo-Json -Depth 10
```

**预期行为：**
1. 智能体接收到消息
2. 智能体识别需要生成图像
3. 智能体调用 DashScope MCP 工具
4. 返回生成的图像 URL

### 5. 查看应用日志

检查日志中是否有以下信息：

```
info: AgentGroupChat.AgentHost.Services.AgentChatService[0]
      Adding 5 MCP tools to agent 'Artsy'
info: AgentGroupChat.AgentHost.Services.AgentChatService[0]
      Created AIAgent 'Artsy' for session {sessionId} with 5 MCP tools
```

## 🔍 调试技巧

### 检查 MCP 连接

如果 MCP 服务器连接失败：

1. **检查网络连接**
   ```powershell
   Test-NetConnection -ComputerName dashscope.aliyuncs.com -Port 443
   ```

2. **验证 Bearer Token**
   - 确认 Token 是否有效
   - 检查 Token 是否过期

3. **查看详细日志**
   在 `appsettings.Development.json` 中启用详细日志：
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Debug",
         "AgentGroupChat.AgentHost.Services.McpToolService": "Trace"
       }
     }
   }
   ```

### 测试 Bearer Token 认证

使用 curl 或 PowerShell 直接测试 MCP 端点：

```powershell
$headers = @{
    "Authorization" = "Bearer sk-8475e1fe4aea401c845bf364ff932165"
    "Content-Type" = "application/json"
}

Invoke-RestMethod -Uri "https://dashscope.aliyuncs.com/api/v1/mcps/TextToImage/sse" `
    -Method Get `
    -Headers $headers
```

## 🎯 验收测试

### 成功标准

- [ ] 应用成功启动，无错误
- [ ] MCP 服务器成功初始化
- [ ] `/api/mcp/servers` 返回服务器列表
- [ ] 智能体可以访问 MCP 工具
- [ ] 智能体可以成功调用 MCP 工具生成图像
- [ ] 生成的图像 URL 可以访问

### 测试场景

#### 场景 1: 基础图像生成
```
用户: @Artsy 生成一张山水画
预期: 智能体调用 MCP 工具，返回图像
```

#### 场景 2: 多智能体协作
```
用户: @Sunny 和 @Artsy 一起为我创作一个阳光明媚的海滩场景
预期: 两个智能体协作，使用 MCP 工具生成图像
```

#### 场景 3: 错误处理
```
用户: @Techie 生成一张不可能的图像
预期: 智能体优雅地处理错误，返回友好的错误消息
```

## 📊 性能监控

### 监控指标

1. **MCP 连接时间**: 服务启动到 MCP 连接建立的时间
2. **工具调用延迟**: 从请求到 MCP 工具返回结果的时间
3. **成功率**: MCP 工具调用成功的百分比

### 查看性能日志

```bash
# 过滤 MCP 相关日志
dotnet run | Select-String "MCP"
```

## 🐛 常见问题

### Q1: "MCP service not initialized" 错误

**原因**: MCP 服务未在应用启动时初始化

**解决**: 检查 `Program.cs` 中是否调用了：
```csharp
var mcpService = app.Services.GetRequiredService<McpToolService>();
await mcpService.InitializeAsync();
```

### Q2: "No MCP servers configured" 警告

**原因**: 配置文件中没有启用的 MCP 服务器

**解决**: 检查 `appsettings.json` 中 `McpServers.Servers` 配置

### Q3: Bearer Token 认证失败

**原因**: Token 无效或已过期

**解决**: 
1. 验证 Token 格式
2. 检查 Token 是否过期
3. 从服务提供商获取新的 Token

## 📝 下一步

- [ ] 添加 MCP 工具使用统计
- [ ] 实现 MCP 连接重试机制
- [ ] 添加 MCP 工具调用的性能缓存
- [ ] 支持动态添加/移除 MCP 服务器
- [ ] 添加 MCP 工具调用的审计日志

## 🔗 相关文档

- [MCP 集成文档](./MCP-INTEGRATION.md)
- [项目架构文档](./ARCHITECTURE.md)
- [快速开始指南](./QUICK-START.md)
