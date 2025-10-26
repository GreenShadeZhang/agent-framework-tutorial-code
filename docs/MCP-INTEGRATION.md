# MCP (Model Context Protocol) 集成文档

## 概述

本文档描述了如何将 MCP (Model Context Protocol) 集成到 AgentGroupChat 项目中，使所有智能体都能使用 MCP 服务器提供的工具。

## 实现的功能

### 1. **多 MCP 服务器支持**
- 支持配置多个 MCP 服务器端点
- 每个服务器可以独立启用/禁用
- 服务器信息包括 ID、名称、端点、描述等

### 2. **多种认证方式**
- **Bearer Token 认证**：用于阿里云 DashScope 等服务
- **OAuth 2.0 认证**：支持标准的 OAuth 流程
- **无认证**：用于公开的 MCP 服务器

### 3. **自动工具注入**
- MCP 工具自动提供给所有智能体
- 智能体可以透明地调用 MCP 服务器上的工具
- 支持工具调用和响应处理

## 文件结构

```
src/AgentGroupChat.AgentHost/
├── Models/
│   └── McpServerConfig.cs          # MCP 服务器配置模型
├── Services/
│   ├── McpToolService.cs           # MCP 工具管理服务
│   └── AgentChatService.cs         # 更新：集成 MCP 工具
├── appsettings.json                # 更新：添加 MCP 配置
├── appsettings.Development.json    # 更新：添加 MCP 配置
└── Program.cs                      # 更新：注册和初始化 MCP 服务
```

## 配置说明

### appsettings.json 配置示例

```json
{
  "McpServers": {
    "Servers": [
      {
        "Id": "dashscope-text-to-image",
        "Name": "DashScope Text-to-Image",
        "Endpoint": "https://dashscope.aliyuncs.com/api/v1/mcps/TextToImage/sse",
        "AuthType": "Bearer",
        "BearerToken": "sk-8475e1fe4aea401c845bf364ff932165",
        "TransportMode": "Sse",
        "Enabled": true,
        "Description": "阿里云 DashScope 文生图服务，用于生成图像"
      }
    ]
  }
}
```

### 配置字段说明

| 字段 | 类型 | 必需 | 说明 |
|------|------|------|------|
| `Id` | string | 是 | 服务器唯一标识符 |
| `Name` | string | 是 | 服务器显示名称 |
| `Endpoint` | string | 是 | MCP 服务器端点 URL |
| `AuthType` | string | 是 | 认证类型：`Bearer`、`OAuth` 或 `None` |
| `TransportMode` | enum | 否 | 传输模式：`AutoDetect`（默认）、`Sse`、`StreamableHttp`<br>推荐 DashScope 等 SSE 服务使用 `Sse` 模式 |
| `BearerToken` | string | 条件 | Bearer Token（当 AuthType 为 Bearer 时必需） |
| `OAuth` | object | 条件 | OAuth 配置（当 AuthType 为 OAuth 时必需） |
| `Enabled` | boolean | 否 | 是否启用此服务器（默认 true） |
| `Description` | string | 否 | 服务器描述信息 |

### TransportMode 说明

- **AutoDetect**（默认）: 自动检测传输模式，让 SDK 根据服务器响应选择最佳模式
- **Sse**: 使用 Server-Sent Events 进行流式通信，**推荐用于 DashScope 和其他基于 SSE 的服务**
- **StreamableHttp**: 使用可流式传输的 HTTP 通信

### OAuth 配置示例

```json
{
  "Id": "protected-mcp-server",
  "Name": "Protected MCP Server",
  "Endpoint": "http://localhost:7071/",
  "AuthType": "OAuth",
  "OAuth": {
    "ClientId": "demo-client",
    "ClientSecret": "demo-secret",
    "RedirectUri": "http://localhost:1179/callback",
    "AuthorizationUrl": "https://localhost:7029/authorize",
    "TokenUrl": "https://localhost:7029/token"
  },
  "Enabled": true,
  "Description": "受保护的 MCP 服务器示例"
}
```

## 核心组件

### 1. McpServerConfig 模型

定义了 MCP 服务器的配置结构：
- `McpServersConfig`：服务器列表容器
- `McpServerConfig`：单个服务器配置
- `McpOAuthConfig`：OAuth 认证配置

### 2. McpToolService 服务

负责管理所有 MCP 连接和工具：

**主要方法：**
- `InitializeAsync()`：初始化所有配置的 MCP 服务器
- `GetAllTools()`：获取所有可用的 MCP 工具
- `GetToolsByServerId(string serverId)`：获取特定服务器的工具
- `GetServerInfo()`：获取所有连接的服务器信息

**认证实现：**
- `CreateBearerTokenTransport()`：创建 Bearer Token 传输层
- `CreateOAuthTransport()`：创建 OAuth 传输层
- `CreateNoAuthTransport()`：创建无认证传输层

### 3. AgentChatService 更新

集成了 MCP 工具支持：
- 在创建智能体时自动注入 MCP 工具
- 所有智能体共享相同的 MCP 工具集
- 日志记录工具数量和使用情况

## API 端点

新增了 MCP 服务器信息查询端点：

```http
GET /api/mcp/servers
```

**响应示例：**
```json
[
  {
    "id": "dashscope-text-to-image",
    "name": "DashScope Text-to-Image",
    "endpoint": "https://dashscope.aliyuncs.com/api/v1/mcps/TextToImage/sse",
    "description": "阿里云 DashScope 文生图服务",
    "toolCount": 5,
    "isConnected": true
  }
]
```

## 使用流程

### 1. 配置 MCP 服务器

在 `appsettings.json` 或 `appsettings.Development.json` 中添加服务器配置。

### 2. 应用启动时初始化

```csharp
var mcpService = app.Services.GetRequiredService<McpToolService>();
await mcpService.InitializeAsync();
```

### 3. 智能体自动获取工具

智能体在创建时会自动获取所有可用的 MCP 工具：

```csharp
var mcpTools = _mcpToolService.GetAllTools();
var agent = _chatClient.CreateAIAgent(
    instructions: instructions,
    name: name,
    tools: [.. mcpTools]
);
```

### 4. 用户与智能体交互

用户发送消息时，智能体可以根据需要调用 MCP 工具：

```
用户: @Artsy 帮我生成一张美丽的风景画
智能体: [调用 DashScope MCP 工具生成图像]
智能体: 这是我为你生成的风景画 [图像]
```

## 安全建议

1. **敏感信息保护**
   - 不要将 Bearer Token 直接提交到代码仓库
   - 使用环境变量或 Azure Key Vault 存储密钥
   - 使用 User Secrets 进行本地开发

2. **配置示例（使用环境变量）**

```json
{
  "McpServers": {
    "Servers": [
      {
        "Id": "dashscope-text-to-image",
        "Name": "DashScope Text-to-Image",
        "Endpoint": "https://dashscope.aliyuncs.com/api/v1/mcps/TextToImage/sse",
        "AuthType": "Bearer",
        "BearerToken": "${DASHSCOPE_API_KEY}",
        "Enabled": true
      }
    ]
  }
}
```

3. **User Secrets 配置**

```bash
# 初始化 User Secrets
cd src/AgentGroupChat.AgentHost
dotnet user-secrets init

# 设置密钥
dotnet user-secrets set "McpServers:Servers:0:BearerToken" "sk-8475e1fe4aea401c845bf364ff932165"
```

## 测试步骤

### 1. 还原包并构建项目

```bash
cd src/AgentGroupChat.AgentHost
dotnet restore
dotnet build
```

### 2. 运行应用

```bash
dotnet run
```

### 3. 测试 MCP 服务器连接

```bash
# 查询 MCP 服务器信息
curl http://localhost:5000/api/mcp/servers
```

### 4. 测试智能体工具调用

通过前端或 API 发送消息，观察智能体是否能成功调用 MCP 工具。

## 故障排除

### 问题 1: MCP 包未找到

**错误信息：**
```
The type or namespace name 'ModelContextProtocol' could not be found
```

**解决方案：**
```bash
dotnet restore
dotnet build
```

### 问题 2: 连接 MCP 服务器失败

**检查项：**
1. MCP 服务器端点 URL 是否正确
2. Bearer Token 是否有效
3. 网络连接是否正常
4. 查看应用日志了解详细错误

### 问题 3: 工具未注入到智能体

**检查项：**
1. 确认 `McpToolService.InitializeAsync()` 已调用
2. 检查配置文件中 `Enabled` 字段是否为 `true`
3. 查看日志确认工具数量

## 扩展支持

### 添加新的 MCP 服务器

1. 在配置文件中添加新服务器
2. 重启应用（服务会自动初始化）
3. 所有智能体自动获得新工具

### 自定义认证方式

如需支持其他认证方式，可以在 `McpToolService` 中添加新的传输层创建方法。

## 参考资源

- [Model Context Protocol 官方文档](https://modelcontextprotocol.io/introduction)
- [Agent Framework MCP 示例](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/GettingStarted/ModelContextProtocol)
- [阿里云 DashScope API 文档](https://help.aliyun.com/zh/dashscope/)

## 版本历史

- **v1.0** (2025-10-26): 初始实现
  - 支持多 MCP 服务器配置
  - 支持 Bearer Token 和 OAuth 认证
  - 自动工具注入到所有智能体
  - 添加 MCP 服务器信息查询 API
