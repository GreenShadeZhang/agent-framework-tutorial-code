# 测试指南 - API 通信测试

由于 .NET SDK 10.0 RC 的 Aspire workload 限制，我们将分别启动 AgentHost 和 Web 进行测试。

## 启动步骤

### 1. 启动 AgentHost (API 后端)

打开第一个终端：

```powershell
cd c:\github\agent-framework-tutorial-code
dotnet run --project src\AgentGroupChat.AgentHost\AgentGroupChat.AgentHost.csproj
```

AgentHost 将在以下端口运行：
- HTTP: http://localhost:5390
- HTTPS: https://localhost:7390

### 2. 配置 Web 项目

在启动 Web 项目之前，需要临时修改 Web 项目的 HttpClient 配置，因为没有 Aspire 服务发现。

编辑 `src\AgentGroupChat.Web\Program.cs`，将：
```csharp
Uri baseAddress = new("https+http://agenthost");
```

临时改为：
```csharp
Uri baseAddress = new("https://localhost:7390"); // 或 http://localhost:5390
```

### 3. 启动 Web (前端)

打开第二个终端：

```powershell
cd c:\github\agent-framework-tutorial-code
dotnet run --project src\AgentGroupChat.Web\AgentGroupChat.Web.csproj
```

Web 将在配置的端口运行（通常是 http://localhost:5xxx）

### 4. 测试 API 通信

1. 在浏览器中打开 Web 应用
2. 应该能看到 Agent 列表（通过 GET /api/agents 获取）
3. 创建新会话（通过 POST /api/sessions）
4. 发送消息并查看 Agent 响应（通过 POST /api/chat）

## API 端点测试

### 测试 AgentHost API

可以使用 curl 或浏览器直接测试 API：

```powershell
# 获取 Agent 列表
curl https://localhost:7390/api/agents

# 创建新会话
curl -X POST https://localhost:7390/api/sessions

# 获取所有会话
curl https://localhost:7390/api/sessions

# 发送消息
curl -X POST https://localhost:7390/api/chat -H "Content-Type: application/json" -d '{"sessionId":"your-session-id","message":"Hello @Sunny"}'
```

## 完整 API 列表

AgentHost 提供以下 REST API：

| 方法 | 路径 | 描述 |
|------|------|------|
| GET | /api/agents | 获取所有可用的 Agent 列表 |
| GET | /api/sessions | 获取所有聊天会话 |
| POST | /api/sessions | 创建新的聊天会话 |
| GET | /api/sessions/{id} | 获取特定会话的详情和消息历史 |
| POST | /api/chat | 发送消息并获取 Agent 响应 |

## 验证点

✅ **AgentHost 成功启动**
- OpenAPI 可访问: https://localhost:7390/openapi/v1.json
- Health check 正常: https://localhost:7390/health

✅ **Web 成功连接到 AgentHost**
- Web 应用加载时显示 4 个 Agent (Sunny, Techie, Artsy, Foodie)
- 可以创建新会话
- 侧边栏显示会话列表

✅ **消息发送和响应**
- 发送消息后，用户消息立即显示
- Agent 响应在几秒内出现
- 多个 Agent 可能依次响应（取决于 @mention）

## 常见问题

### Q: AgentHost 启动失败
A: 检查 appsettings.Development.json 中的 Azure OpenAI 配置：
- AzureOpenAI:Endpoint
- AzureOpenAI:ApiKey  
- AzureOpenAI:DeploymentName

### Q: Web 无法连接到 AgentHost
A: 确认：
1. AgentHost 正在运行
2. Program.cs 中的 baseAddress 设置正确（使用实际的 localhost 地址）
3. 防火墙未阻止端口 5390 或 7390

### Q: 消息发送后无响应
A: 检查：
1. 浏览器控制台是否有错误
2. AgentHost 终端是否有异常输出
3. Azure OpenAI 配置是否正确

## 下一步

一旦解决 Aspire workload 问题（升级 SDK 或使用兼容版本），可以使用：

```powershell
dotnet run --project src\AgentGroupChat.AppHost\AgentGroupChat.AppHost.csproj
```

这将自动启动所有服务并配置服务发现，无需手动修改 baseAddress。
