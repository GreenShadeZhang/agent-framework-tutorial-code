# WorkflowManager MCP 工具集成总结

## 📋 概述

本文档记录了将 MCP (Model Context Protocol) 工具集成到 WorkflowManager 的实现过程，使所有专家智能体（Specialist Agents）能够使用 MCP 服务提供的工具，而智能路由智能体（Triage Agent）则保持纯路由功能，不使用任何工具。

## 🎯 目标

- ✅ 为所有专家智能体（Specialist Agents）提供 MCP 工具支持
- ✅ 智能路由智能体（Triage Agent）不使用任何工具，专注于路由功能
- ✅ 保持架构清晰，职责分离
- ✅ 添加详细的日志记录，便于调试和监控

## 🔧 关键变更

### 1. 在 WorkflowManager 中注入 McpToolService

**文件**: `AgentGroupChat.AgentHost/Services/WorkflowManager.cs`

**修改前**:
```csharp
public class WorkflowManager
{
    private readonly IChatClient _chatClient;
    private readonly AgentRepository _agentRepository;
    private readonly AgentGroupRepository _groupRepository;
    private readonly ILogger<WorkflowManager>? _logger;
    
    public WorkflowManager(
        IChatClient chatClient,
        AgentRepository agentRepository,
        AgentGroupRepository groupRepository,
        ILogger<WorkflowManager>? logger = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _groupRepository = groupRepository ?? throw new ArgumentNullException(nameof(groupRepository));
        _logger = logger;
    }
}
```

**修改后**:
```csharp
public class WorkflowManager
{
    private readonly IChatClient _chatClient;
    private readonly AgentRepository _agentRepository;
    private readonly AgentGroupRepository _groupRepository;
    private readonly McpToolService _mcpToolService;  // ✅ 新增
    private readonly ILogger<WorkflowManager>? _logger;
    
    public WorkflowManager(
        IChatClient chatClient,
        AgentRepository agentRepository,
        AgentGroupRepository groupRepository,
        McpToolService mcpToolService,  // ✅ 新增
        ILogger<WorkflowManager>? logger = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _groupRepository = groupRepository ?? throw new ArgumentNullException(nameof(groupRepository));
        _mcpToolService = mcpToolService ?? throw new ArgumentNullException(nameof(mcpToolService));  // ✅ 新增
        _logger = logger;
    }
}
```

### 2. 修改 CreateWorkflow 方法，为专家智能体添加 MCP 工具

**修改前**:
```csharp
private Workflow CreateWorkflow(string groupId)
{
    // ... 加载组配置和智能体配置 ...
    
    // 创建 Triage Agent
    var triageAgent = new ChatClientAgent(
        _chatClient,
        instructions: triageInstructions,
        name: $"triage_{groupId}",
        description: $"Router for {group.Name}");

    _logger?.LogDebug("Created triage agent for group {GroupId}", groupId);

    // 创建 Specialist Agents
    var specialistAgents = agentProfiles.Select(profile =>
        new ChatClientAgent(
            _chatClient,
            instructions: profile.SystemPrompt + /* ... */,
            name: profile.Id,
            description: profile.Description)  // ❌ 没有工具
    ).ToList();

    _logger?.LogInformation("Created {SpecialistCount} specialist agents for group {GroupId}",
        specialistAgents.Count, groupId);
        
    // ... 构建 workflow ...
}
```

**修改后**:
```csharp
private Workflow CreateWorkflow(string groupId)
{
    // ... 加载组配置和智能体配置 ...
    
    // ✅ 获取所有 MCP 工具
    var mcpTools = _mcpToolService.GetAllTools().ToList();
    _logger?.LogInformation("Loaded {McpToolCount} MCP tools for specialist agents", mcpTools.Count);

    // 创建 Triage Agent（不使用 MCP 工具，只负责路由）
    var triageAgent = new ChatClientAgent(
        _chatClient,
        instructions: triageInstructions,
        name: $"triage_{groupId}",
        description: $"Router for {group.Name}");  // ✅ 无工具

    _logger?.LogDebug("Created triage agent for group {GroupId} (no tools)", groupId);

    // ✅ 创建 Specialist Agents（使用 MCP 工具）
    var specialistAgents = agentProfiles.Select(profile =>
        new ChatClientAgent(
            _chatClient,
            instructions: profile.SystemPrompt + /* ... */,
            name: profile.Id,
            description: profile.Description,
            tools: [.. mcpTools])  // ✅ 为 Specialist Agents 添加 MCP 工具
    ).ToList();

    _logger?.LogInformation("Created {SpecialistCount} specialist agents for group {GroupId} with {McpToolCount} MCP tools each",
        specialistAgents.Count, groupId, mcpTools.Count);
        
    // ... 构建 workflow ...
}
```

## 📊 架构说明

### 智能体工具分配策略

```
┌─────────────────────────────────────────────────────────┐
│                    WorkflowManager                      │
│                                                         │
│  ┌───────────────────────────────────────────────────┐ │
│  │         Triage Agent (智能路由智能体)             │ │
│  │  - 职责: 分析用户消息并路由到专家智能体             │ │
│  │  - 工具: 无 (纯路由功能)                          │ │
│  │  - 行为: 不生成回复，只调用 handoff 函数           │ │
│  └───────────────────────────────────────────────────┘ │
│                         ↓                               │
│                      handoff                            │
│                         ↓                               │
│  ┌───────────────────────────────────────────────────┐ │
│  │       Specialist Agents (专家智能体)              │ │
│  │  - Sunny (阳光女孩)                               │ │
│  │  - Techie (技术专家)                              │ │
│  │  - Artsy (艺术家)                                 │ │
│  │  - Foodie (美食家)                                │ │
│  │                                                   │ │
│  │  每个智能体都配备:                                 │ │
│  │  ✅ 个性化系统提示词                               │ │
│  │  ✅ 所有 MCP 工具 (来自 McpToolService)           │ │
│  └───────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

### MCP 工具流程

```
用户消息
   ↓
Triage Agent (无工具)
   ↓ (分析并路由)
handoff → Specialist Agent (有 MCP 工具)
   ↓
调用 MCP 工具 (例如: DashScope 文生图)
   ↓
生成回复
   ↓
返回给用户
```

## 🔍 McpToolService 分析

### 核心功能

1. **连接管理**: 支持多个 MCP 服务器连接
2. **认证支持**: Bearer Token、OAuth、无认证
3. **传输模式**: SSE、StreamableHttp、自动检测
4. **工具管理**: 聚合所有服务器的工具，提供统一访问接口

### 关键方法

```csharp
// 初始化所有配置的 MCP 服务器
await InitializeAsync(CancellationToken cancellationToken)

// 获取所有可用的 MCP 工具
IEnumerable<AITool> GetAllTools()

// 按服务器 ID 获取工具
IEnumerable<AITool> GetToolsByServerId(string serverId)

// 获取服务器信息
IEnumerable<McpServerInfo> GetServerInfo()
```

### 配置示例 (appsettings.json)

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

## 📝 日志记录

### 新增的日志记录点

1. **工具加载日志**:
   ```
   Loaded {McpToolCount} MCP tools for specialist agents
   ```

2. **Triage Agent 创建日志**:
   ```
   Created triage agent for group {GroupId} (no tools)
   ```

3. **Specialist Agent 创建日志**:
   ```
   Created {SpecialistCount} specialist agents for group {GroupId} with {McpToolCount} MCP tools each
   ```

这些日志帮助开发者和运维人员了解:
- MCP 工具加载情况
- 每个智能体的工具配置
- 工作流创建过程

## ✅ 优势

### 1. 职责分离
- **Triage Agent**: 专注于智能路由，不受工具干扰
- **Specialist Agents**: 拥有完整的 MCP 工具能力，可以执行实际操作

### 2. 灵活性
- 所有专家智能体共享相同的工具集
- 便于统一管理和升级 MCP 工具
- 未来可扩展为每个智能体配置特定的工具子集

### 3. 可维护性
- 清晰的依赖注入
- 详细的日志记录
- 符合单一职责原则

### 4. 性能优化
- 工具列表只加载一次（在 CreateWorkflow 中）
- 通过 WorkflowCache 缓存已创建的工作流
- 避免重复加载 MCP 工具

## 🚀 后续改进建议

### 1. 工具过滤器
可以为不同的智能体配置不同的工具子集：

```csharp
// 示例：根据智能体角色过滤工具
var mcpTools = _mcpToolService.GetAllTools()
    .Where(tool => IsToolApplicableForAgent(tool, profile))
    .ToList();
```

### 2. 工具权限管理
为不同的智能体设置工具访问权限：

```csharp
public class AgentProfile
{
    public List<string> AllowedToolIds { get; set; } = new();
    public List<string> DeniedToolIds { get; set; } = new();
}
```

### 3. 动态工具加载
支持运行时动态添加或移除 MCP 工具：

```csharp
public void RefreshMcpTools()
{
    // 重新加载 MCP 工具
    _mcpToolService.ReloadTools();
    
    // 清除工作流缓存，强制重建
    ClearAllWorkflowCache();
}
```

## 📚 相关文档

- [MCP Integration Guide](./MCP-INTEGRATION.md)
- [MCP Testing Guide](./MCP-TESTING-GUIDE.md)
- [Dynamic Agent Loading](./DYNAMIC_AGENT_LOADING.md)
- [Workflow Performance Optimization](./WORKFLOW-PERFORMANCE-OPTIMIZATION.md)

## 🔗 代码文件

- `Services/WorkflowManager.cs` - 工作流管理器（已修改）
- `Services/McpToolService.cs` - MCP 工具服务
- `Models/McpServerConfig.cs` - MCP 服务器配置模型
- `appsettings.json` - MCP 服务器配置

## 📅 变更历史

- **2025-10-26**: 初始实现 - 为 Specialist Agents 集成 MCP 工具，Triage Agent 保持无工具状态

---

**注意**: 此实现确保了智能路由智能体（Triage Agent）专注于路由决策，而所有专家智能体（Specialist Agents）都拥有完整的 MCP 工具能力，可以处理实际的用户请求和执行操作。
