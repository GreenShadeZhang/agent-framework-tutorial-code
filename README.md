# Agent Framework Tutorial Code

这是一个基于 Microsoft Agent Framework 的教程代码仓库，包含各种智能体应用示例。

## 项目列表

### 1. Agent Group Chat - 多智能体群聊应用

位置：`src/AgentGroupChat/`

一个功能完整的多智能体群聊应用，展示了如何使用 Agent Framework 的 handoff 模式实现智能体间的协作。

**主要特性**:
- ✅ Handoff 模式实现的多智能体协作
- ✅ 四个不同性格的智能体（Sunny、Techie、Artsy、Foodie）
- ✅ 用户可以 @ 提及特定智能体
- ✅ 智能体带有头像、昵称和图片分享功能
- ✅ Blazor Server 前端界面
- ✅ LiteDB 持久化存储
- ✅ 支持创建和管理多个会话

详细文档请查看：[src/README.md](src/README.md)

## 快速开始

1. **克隆仓库**
   ```bash
   git clone https://github.com/GreenShadeZhang/agent-framework-tutorial-code-.git
   cd agent-framework-tutorial-code-
   ```

2. **进入项目目录**
   ```bash
   cd src/AgentGroupChat
   ```

3. **配置 Azure OpenAI**
   
   编辑 `appsettings.json` 或设置环境变量：
   ```bash
   export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
   export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o-mini"
   ```

4. **运行项目**
   ```bash
   dotnet run
   ```

## 技术栈

- .NET 9.0
- Blazor Server
- Microsoft Agent Framework
- Azure OpenAI
- LiteDB

## 学习路径

建议按以下顺序学习各个示例：

1. **Agent Group Chat** - 理解 handoff 模式和多智能体协作的基础

更多示例正在开发中...

## 贡献

欢迎提交 Issue 和 Pull Request！

## 许可证

MIT License