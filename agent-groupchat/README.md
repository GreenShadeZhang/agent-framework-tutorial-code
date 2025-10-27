# Agent Group Chat - 基于 Agent Framework 的多智能体群聊应用

这是一个基于 Microsoft Agent Framework 实现的 handoff 模式多智能体群聊应用。

## 功能特性

### 核心功能

- ✅ **Handoff 模式**: 基于 Microsoft Agent Framework 实现智能体之间的消息路由和切换
- ✅ **多个性格智能体**: 四个不同性格的智能体，每个都有独特的个性和回复风格
  - ☀️ **Sunny**: 阳光开朗，充满正能量
  - 🤖 **Techie**: 技术宅，喜欢分享科技知识
  - 🎨 **Artsy**: 艺术家，发现生活中的美
  - 🍜 **Foodie**: 美食家，热爱烹饪和美食
- ✅ **图片生成工具**: 每个智能体都配备图片生成功能
- ✅ **@提及功能**: 用户可以使用 @ 符号特定提及某个智能体
- ✅ **富文本回复**: 智能体回复包含昵称、头像、文字和图片
- ✅ **会话管理**: 支持创建新会话和切换历史会话
- ✅ **持久化存储**: 使用 LiteDB 持久化会话记录

### 技术栈

- **前端**: Blazor Server
- **AI 框架**: Microsoft Agent Framework (via NuGet)
- **数据库**: LiteDB (轻量级文档数据库)
- **AI 服务**: Azure OpenAI
- **认证**: Azure Identity (DefaultAzureCredential)

## 项目结构

```
agent-groupchat/
├── AgentGroupChat.Web/               # 主应用项目
│   ├── Components/                   # Blazor 组件
│   │   ├── Pages/
│   │   │   └── Home.razor           # 主聊天界面
│   │   ├── Layout/                  # 布局组件
│   │   └── _Imports.razor           # 全局引用
│   ├── Models/                      # 数据模型
│   │   ├── AgentProfile.cs          # 智能体配置
│   │   ├── ChatMessage.cs           # 聊天消息
│   │   └── ChatSession.cs           # 会话
│   ├── Services/                    # 业务服务
│   │   ├── AgentChatService.cs      # 智能体聊天服务（核心）
│   │   ├── ImageGenerationTool.cs   # 图片生成工具
│   │   └── SessionService.cs        # 会话持久化服务
│   ├── wwwroot/                     # 静态资源
│   │   ├── app.css                  # 样式文件
│   │   └── avatars/                 # 头像资源
│   ├── Program.cs                   # 应用入口
│   ├── appsettings.json             # 配置文件
│   └── AgentGroupChat.Web.csproj    # 项目文件
├── AgentGroupChat.AppHost/          # Aspire AppHost
├── AgentGroupChat.ServiceDefaults/  # 服务默认配置
├── AgentGroupChat.slnx              # 解决方案文件
└── README.md                        # 本文档
```

## 快速开始

### 前置要求

1. .NET 9.0 SDK 或更高版本
2. Azure OpenAI 服务（或 OpenAI API）
3. Visual Studio 2022 或 VS Code

### 配置步骤

1. **克隆仓库**
   ```bash
   git clone https://github.com/GreenShadeZhang/agent-framework-tutorial-code.git
   cd agent-framework-tutorial-code/agent-groupchat
   ```

2. **配置 Azure OpenAI**

   编辑 `appsettings.json` 文件：
   ```json
   {
     "AzureOpenAI": {
       "Endpoint": "https://your-resource.openai.azure.com/",
       "DeploymentName": "gpt-4o-mini"
     }
   }
   ```

   或者设置环境变量：
   ```bash
   export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
   export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o-mini"
   ```

3. **配置 Azure 认证**

   应用使用 `DefaultAzureCredential`，支持以下认证方式：
   - 环境变量
   - 托管标识
   - Visual Studio
   - Azure CLI
   - Azure PowerShell

   最简单的方式是使用 Azure CLI 登录：
   ```bash
   az login
   ```

4. **运行应用**
   ```bash
   dotnet run
   ```

   应用将在 `https://localhost:5001` 启动。

## 使用说明

### 基本操作

1. **创建新会话**: 点击左侧边栏的 "➕ New Chat" 按钮
2. **切换会话**: 点击左侧会话列表中的任意会话
3. **发送消息**: 在底部输入框输入消息并点击 "Send"
4. **提及智能体**: 使用 `@AgentName` 格式提及特定智能体，例如：
   - `@Sunny 今天天气真好！`
   - `@Techie 能介绍一下 Blazor 吗？`
   - `@Artsy 分享一张美丽的风景照`
   - `@Foodie 推荐一道好吃的菜`

### 智能体特点

#### ☀️ Sunny (阳光)
- **性格**: 开朗乐观
- **风格**: 积极向上，喜欢分享正能量
- **适合话题**: 日常生活、心情分享、励志内容

#### 🤖 Techie (技术宅)
- **性格**: 理性分析
- **风格**: 技术专业，善于解释原理
- **适合话题**: 科技、编程、技术趋势

#### 🎨 Artsy (艺术家)
- **性格**: 富有创意
- **风格**: 感性表达，关注美学
- **适合话题**: 艺术、设计、审美体验

#### 🍜 Foodie (美食家)
- **性格**: 热情洋溢
- **风格**: 生动描述，充满食欲
- **适合话题**: 美食、烹饪、餐厅推荐

## 技术实现

### Handoff 模式

应用使用 Agent Framework 的 `AgentWorkflowBuilder` 实现 handoff 模式：

```csharp
// 创建 triage 智能体用于路由
var triageAgent = new ChatClientAgent(chatClient, systemPrompt, "triage", "Routes messages");

// 构建 handoff 工作流
var builder = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent);

// 添加从 triage 到各个智能体的切换
builder.WithHandoffs(triageAgent, agents);

// 添加从各个智能体返回 triage 的切换
builder.WithHandoffs(agents, triageAgent);

var workflow = builder.Build();
```

### 会话持久化

使用 LiteDB 实现轻量级持久化存储：

```csharp
public class SessionService : IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILiteCollection<ChatSession> _sessions;
    
    public SessionService()
    {
        _database = new LiteDatabase("sessions.db");
        _sessions = _database.GetCollection<ChatSession>("sessions");
    }
    
    // CRUD 操作...
}
```

### 图片生成

当前实现使用占位符图片服务。在生产环境中，可以集成：
- DALL-E (OpenAI)
- Stable Diffusion
- Azure Computer Vision

```csharp
public class ImageGenerationTool
{
    [Description("Generate an image based on a text prompt")]
    public Task<string> GenerateImage(string prompt)
    {
        // 实际实现中集成真实的图片生成 API
        return Task.FromResult(imageUrl);
    }
}
```

## 开发和扩展

### 添加新智能体

1. 在 `AgentChatService.cs` 的 `_agentProfiles` 列表中添加新的 `AgentProfile`
2. 定义智能体的性格、系统提示词和描述
3. 智能体会自动注册到 handoff 工作流中

### 自定义图片生成

修改 `ImageGenerationTool.cs` 以集成实际的图片生成服务：

```csharp
public async Task<string> GenerateImage(string prompt)
{
    // 集成 DALL-E 或其他图片生成 API
    var response = await dalleClient.GenerateImageAsync(prompt);
    return response.ImageUrl;
}
```

### 自定义界面

编辑 `wwwroot/app.css` 自定义样式，或修改 `Components/Pages/Home.razor` 调整布局。

## 故障排除

### 常见问题

1. **"Azure OpenAI endpoint not configured" 错误**
   - 检查 `appsettings.json` 中的配置
   - 或设置相应的环境变量

2. **认证失败**
   - 确保已使用 `az login` 登录
   - 或配置了正确的服务主体凭据

3. **智能体无响应**
   - 检查 Azure OpenAI 部署是否正常
   - 确认模型名称（DeploymentName）是否正确
   - 查看应用日志获取详细错误信息

4. **数据库锁定**
   - 确保没有多个应用实例同时访问数据库
   - 如需重置，删除 `Data/sessions.db` 文件

## 参考资料

- [Microsoft Agent Framework 官方文档](https://github.com/microsoft/agent-framework)
- [AgentWebChat 示例](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/AgentWebChat)
- [Workflow Patterns 示例](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/GettingStarted/Workflows/_Foundational/04_AgentWorkflowPatterns/Program.cs)
- [Blazor 文档](https://learn.microsoft.com/aspnet/core/blazor)
- [Azure OpenAI 文档](https://learn.microsoft.com/azure/ai-services/openai/)

## 许可证

本项目遵循 MIT 许可证。

## 贡献

欢迎提交 Issue 和 Pull Request！
