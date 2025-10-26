# MCP TransportMode 配置指南

## 概述

`TransportMode` 字段控制 MCP 客户端与服务器之间的通信方式。不同的 MCP 服务器可能需要不同的传输模式才能正常工作。

## TransportMode 枚举值

### 1. AutoDetect（默认值）
- **说明**: 自动检测最佳传输模式
- **何时使用**: 当你不确定服务器支持哪种模式时
- **优点**: 灵活，自适应
- **缺点**: 可能无法选择最优模式，某些服务可能不支持自动检测

### 2. Sse (Server-Sent Events)
- **说明**: 使用 Server-Sent Events 进行流式通信
- **何时使用**: 
  - DashScope 文生图服务
  - 其他基于 SSE 的 MCP 服务
  - 端点 URL 包含 `/sse` 的服务
- **优点**: 高效的单向流式传输，适合实时数据推送
- **推荐**: ⭐⭐⭐⭐⭐ 用于 DashScope

### 3. StreamableHttp
- **说明**: 使用可流式传输的 HTTP
- **何时使用**: 标准 HTTP 流式服务
- **优点**: 兼容性好，支持双向通信

## 配置示例

### 示例 1: DashScope 文生图服务（推荐 SSE）

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
        "Enabled": true,
        "Description": "阿里云 DashScope 文生图服务"
      }
    ]
  }
}
```

**为什么使用 Sse?**
- DashScope 端点明确包含 `/sse`
- SSE 模式能确保生图工具正常工作
- 避免自动检测可能导致的连接问题

### 示例 2: 标准 MCP 服务器（自动检测）

```json
{
  "McpServers": {
    "Servers": [
      {
        "Id": "standard-mcp-server",
        "Name": "Standard MCP Server",
        "Endpoint": "http://localhost:7071/",
        "AuthType": "None",
        "TransportMode": "AutoDetect",
        "Enabled": true,
        "Description": "标准 MCP 服务器，支持自动检测"
      }
    ]
  }
}
```

**为什么使用 AutoDetect?**
- 本地开发服务器，支持多种传输模式
- 让 SDK 自动选择最佳模式
- 简化配置

### 示例 3: StreamableHttp 服务

```json
{
  "McpServers": {
    "Servers": [
      {
        "Id": "streamable-http-server",
        "Name": "Streamable HTTP Server",
        "Endpoint": "https://api.example.com/mcp",
        "AuthType": "Bearer",
        "BearerToken": "your-token-here",
        "TransportMode": "StreamableHttp",
        "Enabled": true,
        "Description": "使用 StreamableHttp 的 MCP 服务"
      }
    ]
  }
}
```

### 示例 4: 多服务器配置（不同传输模式）

```json
{
  "McpServers": {
    "Servers": [
      {
        "Id": "dashscope-image",
        "Name": "DashScope Image",
        "Endpoint": "https://dashscope.aliyuncs.com/api/v1/mcps/TextToImage/sse",
        "AuthType": "Bearer",
        "BearerToken": "sk-xxx",
        "TransportMode": "Sse",
        "Enabled": true,
        "Description": "DashScope 文生图"
      },
      {
        "Id": "local-mcp",
        "Name": "Local MCP",
        "Endpoint": "http://localhost:7071/",
        "AuthType": "None",
        "TransportMode": "AutoDetect",
        "Enabled": true,
        "Description": "本地开发服务器"
      },
      {
        "Id": "cloud-service",
        "Name": "Cloud Service",
        "Endpoint": "https://api.cloud.com/mcp",
        "AuthType": "OAuth",
        "TransportMode": "StreamableHttp",
        "OAuth": {
          "ClientId": "client-id",
          "ClientSecret": "client-secret",
          "RedirectUri": "http://localhost:1179/callback"
        },
        "Enabled": true,
        "Description": "云端 MCP 服务"
      }
    ]
  }
}
```

## 故障排除

### 问题 1: 连接失败或超时

**症状**: MCP 服务器无法连接
```
Failed to initialize MCP server: DashScope Text-to-Image
```

**解决方案**: 
1. 检查 `TransportMode` 配置
2. 如果端点包含 `/sse`，尝试设置 `"TransportMode": "Sse"`
3. 如果不确定，先尝试 `AutoDetect`

### 问题 2: 工具调用失败

**症状**: 工具列表为空或调用返回错误
```
Successfully initialized MCP server 'DashScope' with 0 tools
```

**解决方案**:
- DashScope 服务必须使用 `"TransportMode": "Sse"`
- 检查 Bearer Token 是否正确
- 验证端点 URL 是否正确

### 问题 3: 性能问题或延迟

**症状**: 响应缓慢
```
Tool execution took longer than expected
```

**解决方案**:
- 对于实时流式数据，使用 `Sse`
- 对于标准请求-响应，使用 `StreamableHttp`
- 避免过度依赖 `AutoDetect`

## 最佳实践

### 1. 根据服务端点选择模式

| 端点特征 | 推荐 TransportMode |
|---------|-------------------|
| 包含 `/sse` | `Sse` |
| 标准 HTTP API | `StreamableHttp` 或 `AutoDetect` |
| 本地开发服务器 | `AutoDetect` |
| 已知支持 SSE | `Sse` |

### 2. DashScope 特定配置

对于所有 DashScope 服务，始终使用：
```json
{
  "TransportMode": "Sse"
}
```

这是经过验证的配置，能确保工具正常运行。

### 3. 开发环境 vs 生产环境

**开发环境** (`appsettings.Development.json`):
```json
{
  "TransportMode": "AutoDetect"  // 方便调试和测试
}
```

**生产环境** (`appsettings.json`):
```json
{
  "TransportMode": "Sse"  // 明确指定，避免不确定性
}
```

### 4. 日志和监控

启用详细日志查看传输模式选择：
```json
{
  "Logging": {
    "LogLevel": {
      "AgentGroupChat.AgentHost.Services.McpToolService": "Debug"
    }
  }
}
```

日志输出示例：
```
[Debug] Created Bearer token transport for DashScope with mode: Sse
```

## 性能对比

| TransportMode | 延迟 | 吞吐量 | 适用场景 |
|--------------|------|--------|----------|
| Sse | 低 | 高 | 实时流式数据，如图像生成 |
| StreamableHttp | 中 | 中 | 标准 API 调用 |
| AutoDetect | 中-高 | 中 | 开发和测试 |

## 迁移指南

### 从旧配置迁移

**旧配置**（没有 TransportMode）:
```json
{
  "Id": "dashscope-text-to-image",
  "Endpoint": "https://dashscope.aliyuncs.com/api/v1/mcps/TextToImage/sse",
  "AuthType": "Bearer",
  "BearerToken": "xxx"
}
```

**新配置**（添加 TransportMode）:
```json
{
  "Id": "dashscope-text-to-image",
  "Endpoint": "https://dashscope.aliyuncs.com/api/v1/mcps/TextToImage/sse",
  "AuthType": "Bearer",
  "BearerToken": "xxx",
  "TransportMode": "Sse"  // 新增字段
}
```

**兼容性**: 
- ✅ 如果不指定 `TransportMode`，默认为 `AutoDetect`
- ✅ 现有配置继续有效
- ⚠️ 建议为 DashScope 服务明确指定 `Sse`

## 常见问题 (FAQ)

### Q1: 我应该为所有服务使用相同的 TransportMode 吗？

**A**: 不应该。不同的 MCP 服务器可能需要不同的传输模式。根据每个服务的特性单独配置。

### Q2: AutoDetect 总是有效吗？

**A**: 不一定。某些服务（如 DashScope）需要明确指定传输模式才能正常工作。

### Q3: 如何知道应该使用哪种 TransportMode？

**A**: 
1. 查看服务端点 URL（包含 `/sse` 使用 `Sse`）
2. 查阅服务提供商文档
3. 先尝试 `AutoDetect`，如果失败则明确指定
4. 查看日志中的连接错误信息

### Q4: 可以在运行时更改 TransportMode 吗？

**A**: 目前需要重启应用。未来版本可能支持动态更新。

### Q5: TransportMode 会影响性能吗？

**A**: 会。不同的传输模式有不同的性能特征。`Sse` 通常在流式场景下性能最好。

## 技术细节

### 代码实现

TransportMode 配置的处理逻辑：

```csharp
// 只有在非 AutoDetect 时才设置 TransportMode
if (config.TransportMode != McpTransportMode.AutoDetect)
{
    transportOptions.TransportMode = ConvertToSdkTransportMode(config.TransportMode);
}
// AutoDetect 时让 SDK 自动检测
```

### 枚举定义

```csharp
public enum McpTransportMode
{
    AutoDetect = 0,  // 默认值
    Sse = 1,
    StreamableHttp = 2
}
```

## 总结

- ✅ **DashScope**: 始终使用 `Sse`
- ✅ **本地开发**: 使用 `AutoDetect` 
- ✅ **生产环境**: 明确指定传输模式
- ✅ **多服务器**: 根据每个服务特性单独配置

正确配置 `TransportMode` 能显著提升 MCP 工具的稳定性和性能！
