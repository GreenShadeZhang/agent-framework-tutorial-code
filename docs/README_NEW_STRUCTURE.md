# Agent Group Chat - Professional Distributed Architecture

本项目按照 Microsoft Agent Framework 的 AgentWebChat 示例重构为专业的分体式架构。

## ✨ 新特性

- 🎨 **现代化 UI**: 专业的渐变色设计，流畅的动画效果
- 🏗️ **分布式架构**: 前后端分离，微服务编排
- 🚀 **高性能**: Blazor Server + ASP.NET Core API
- 📱 **响应式设计**: 完美适配桌面、平板、移动端
- 🔄 **服务发现**: .NET Aspire 自动服务编排
- 📊 **可观测性**: OpenTelemetry 集成

## 项目结构

```
src/
├── AgentGroupChat/                    # 原始项目（保留用于参考）
├── AgentGroupChat.Web/                # Blazor Server 前端
│   ├── Components/                    # Razor 组件
│   │   ├── Layout/                    # 布局组件
│   │   ├── Pages/                     # 页面组件
│   │   ├── App.razor                  # 应用根组件
│   │   ├── Routes.razor               # 路由配置
│   │   └── _Imports.razor             # 全局引用
│   ├── wwwroot/                       # 静态资源
│   └── Program.cs                     # Web 启动入口
├── AgentGroupChat.AgentHost/          # API 后端服务
│   ├── Models/                        # 数据模型
│   ├── Services/                      # 业务服务
│   │   ├── AgentChatService.cs       # Agent 聊天服务
│   │   ├── SessionService.cs         # 会话管理服务
│   │   └── ImageGenerationTool.cs    # 图像生成工具
│   ├── appsettings.json              # 配置文件
│   └── Program.cs                    # API 启动入口
├── AgentGroupChat.AppHost/            # .NET Aspire 启动项目
│   ├── appsettings.json              # AppHost 配置
│   └── Program.cs                    # Aspire 编排入口
├── AgentGroupChat.ServiceDefaults/    # 共享服务配置
│   └── Extensions.cs                 # 服务注册扩展
└── AgentGroupChat.sln                 # 解决方案文件
```

## 技术栈

- **.NET 9.0**
- **Blazor Server** - 前端 UI 框架
- **ASP.NET Core** - API 后端
- **.NET Aspire** - 微服务编排和启动
- **Microsoft Agents AI** - Agent 框架
- **Azure OpenAI** - LLM 服务
- **LiteDB** - 轻量级数据库（用于会话存储）

## 快速开始

### 前提条件

1. 安装 [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
2. 安装 [.NET Aspire workload](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling)
   ```powershell
   dotnet workload install aspire
   ```
3. 配置 Azure OpenAI 服务

### 配置 Azure OpenAI

在 `AgentGroupChat.AgentHost/appsettings.Development.json` 中配置：

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "DeploymentName": "gpt-4o-mini",
    "ApiKey": "your-api-key-here"
  }
}
```

或者设置环境变量：
```powershell
$env:AzureOpenAI__Endpoint = "https://your-resource.openai.azure.com/"
$env:AzureOpenAI__ApiKey = "your-api-key-here"
$env:AzureOpenAI__DeploymentName = "gpt-4o-mini"
```

### 启动应用（推荐）

使用 .NET Aspire 一键启动所有服务：

```powershell
cd src\AgentGroupChat.AppHost
dotnet run
```

Aspire Dashboard 将自动打开，显示所有服务的状态和日志。

### 单独启动服务（调试用）

#### 启动 API 后端
```powershell
cd src\AgentGroupChat.AgentHost
dotnet run
```

#### 启动 Web 前端
```powershell
cd src\AgentGroupChat.Web
dotnet run
```

## 架构说明

### AgentGroupChat.Web（前端）
- **职责**: 用户界面，展示聊天界面和会话管理
- **通信**: 通过 HttpClient 调用 AgentHost 的 API
- **端口**: 由 Aspire 动态分配，或独立运行时使用配置的端口

### AgentGroupChat.AgentHost（后端）
- **职责**: Agent 推理、Workflow 编排、会话管理
- **功能**:
  - Agent 注册和发现
  - 聊天消息处理
  - Workflow 流式响应
  - 会话持久化
- **端口**: 5390 (HTTP), 7390 (HTTPS)

### AgentGroupChat.AppHost（编排）
- **职责**: 使用 .NET Aspire 统一管理和启动所有服务
- **功能**:
  - 服务发现和依赖注入
  - 配置管理
  - 遥测和监控
  - 健康检查

### AgentGroupChat.ServiceDefaults（共享）
- **职责**: 提供所有服务的通用配置
- **功能**:
  - OpenTelemetry 配置
  - 健康检查
  - 服务发现
  - HTTP 弹性处理

## 开发指南

### 添加新的 Agent

在 `AgentGroupChat.AgentHost/Program.cs` 中注册新 Agent：

```csharp
builder.AddAIAgent("new-agent",
    instructions: "Your agent instructions here",
    description: "Agent description",
    chatClientServiceKey: "chat-model");
```

### 修改前端页面

编辑 `AgentGroupChat.Web/Components/Pages/Home.razor` 来修改主聊天界面。

### 调试技巧

1. **查看 Aspire Dashboard**: 提供服务状态、日志、指标和追踪
2. **独立调试**: 可以单独启动 AgentHost 或 Web 项目进行调试
3. **日志级别**: 在 `appsettings.Development.json` 中调整日志级别

## 参考资源

- [Microsoft Agent Framework 文档](https://github.com/microsoft/agent-framework)
- [AgentWebChat 示例](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/AgentWebChat)
- [.NET Aspire 文档](https://learn.microsoft.com/dotnet/aspire/)
- [项目文档](../docs/)

## 常见问题

### Q: 如何更改端口？
A: 修改对应项目的 `Properties/launchSettings.json` 文件。

### Q: Aspire Dashboard 无法打开？
A: 确保安装了 Aspire workload，并检查防火墙设置。

### Q: Agent 无法访问 Azure OpenAI？
A: 检查 `appsettings.Development.json` 中的配置，确保 Endpoint、ApiKey 和 DeploymentName 正确。

## 许可证

请参考项目根目录的 LICENSE 文件。
