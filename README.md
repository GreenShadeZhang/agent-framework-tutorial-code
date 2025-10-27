# Agent Framework Tutorial Code

学习 Microsoft Agent Framework 的示例代码仓库。

## 📚 示例列表

### [agent-groupchat](./agent-groupchat/) - 多智能体群聊应用

基于 handoff 模式的多智能体协作应用，展示智能体间的消息路由和协作。

**特性**: 
- ✅ 四个不同性格的智能体（Sunny、Techie、Artsy、Foodie）
- ✅ @ 提及功能、图片生成、会话管理
- ✅ LiteDB 持久化存储

**技术栈**: Blazor Server, Azure OpenAI, LiteDB  
**难度**: ⭐⭐⭐

---

### basic-agent - 基础智能体 *(即将推出)*

最简单的智能体示例，适合初学者快速入门。

**技术栈**: Console App, Azure OpenAI  
**难度**: ⭐

---

### rag-agent - RAG 智能体 *(即将推出)*

展示如何构建检索增强生成（RAG）智能体。

**技术栈**: ASP.NET Core, Azure AI Search  
**难度**: ⭐⭐⭐⭐

---

### mcp-integration - MCP 集成 *(即将推出)*

展示如何集成 Model Context Protocol。

**技术栈**: .NET 9.0, MCP SDK  
**难度**: ⭐⭐⭐

## 🚀 快速开始

1. **克隆仓库**
   ```bash
   git clone https://github.com/GreenShadeZhang/agent-framework-tutorial-code.git
   cd agent-framework-tutorial-code
   ```

2. **进入任一示例目录**
   ```bash
   cd agent-groupchat
   ```

3. **按照该示例的 README.md 操作**
   
   每个示例都有详细的 README，包含配置和运行说明。

## 📖 学习路径

建议按以下顺序学习：

1. **basic-agent** - 掌握基础概念 *(即将推出)*
2. **agent-groupchat** - 理解 handoff 模式和多智能体协作
3. **rag-agent** - 学习 RAG 技术 *(即将推出)*
4. **mcp-integration** - 探索 MCP 集成 *(即将推出)*

## 🛠️ 技术栈

- .NET 9.0
- Microsoft Agent Framework
- Azure OpenAI
- Blazor Server / ASP.NET Core
- LiteDB

## 贡献

欢迎提交 Issue 和 Pull Request！

## 许可证

MIT License