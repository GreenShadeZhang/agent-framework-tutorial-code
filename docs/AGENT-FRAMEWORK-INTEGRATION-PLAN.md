# Agent Framework 集成改造方案

## 问题分析

### 当前实现的架构问题

1. **YAML 格式不兼容**
   - 当前使用自定义格式（agents + workflow.nodes）
   - Agent Framework 要求 Bot Framework AdaptiveDialog 格式
   - DeclarativeWorkflowBuilder 无法加载当前格式

2. **执行方式错误**
   - 当前：WorkflowExecutor 手动遍历节点
   - 正确：DeclarativeWorkflowBuilder.Build() + workflow.RunAsync()
   - Agent Framework 是声明式框架，不应该有手动循环

3. **Agent 解析方式不符合规范**
   - WorkflowExecutor 直接创建 ChatClientAgent
   - 应该通过 WorkflowAgentProvider.GetAsync() 解析

## 改造方案

### Phase 1: YAML 格式改造（核心任务）

#### 1.1 研究 Bot Framework AdaptiveDialog 格式

**需要搜索的示例：**
```bash
# 在 microsoft/agent-framework 仓库中搜索
- workflow-samples/*.yaml
- tests/**/*.yaml
- docs/examples/*.yaml
```

**需要了解的关键概念：**
- AdaptiveDialog 结构
- OnUnknownIntent trigger
- BeginDialog action
- IfCondition action（条件分支）
- SendActivity action（输出）

#### 1.2 映射关系设计

| 当前节点类型 | Bot Framework Action | 说明 |
|------------|---------------------|------|
| Start | - | 入口点，不需要映射 |
| Agent | BeginDialog | 调用 agent dialog |
| Condition | IfCondition | 条件分支 |
| End | SendActivity | 输出结果 |

**边（Edge）映射：**
- 普通边 → action 序列
- 条件边 → IfCondition 的 actions/elseActions

#### 1.3 实现新的 YAML 转换器

**文件：** `WorkflowService.cs`

**新方法：** `ConvertToAdaptiveDialogYamlAsync`

```csharp
public async Task<string> ConvertToAdaptiveDialogYamlAsync(string workflowId)
{
    var workflow = await _workflowRepository.GetByIdAsync(workflowId);
    
    // 构建 AdaptiveDialog 结构
    var adaptiveDialog = new
    {
        kind = "AdaptiveDialog",
        id = workflow.Id,
        recognizer = new { kind = "RegexRecognizer" },
        triggers = new[]
        {
            new
            {
                kind = "OnUnknownIntent",
                actions = BuildActions(workflow) // 核心转换逻辑
            }
        }
    };
    
    return SerializeToYaml(adaptiveDialog);
}

private List<object> BuildActions(WorkflowDefinition workflow)
{
    var actions = new List<object>();
    
    // 从 Start 节点开始
    var startNode = workflow.Nodes.First(n => n.Type == WorkflowNodeType.Start);
    var currentEdge = workflow.Edges.First(e => e.Source == startNode.Id);
    
    // 递归构建 action 树
    BuildActionChain(currentEdge.Target, workflow, actions);
    
    return actions;
}

private void BuildActionChain(string nodeId, WorkflowDefinition workflow, List<object> actions)
{
    var node = workflow.Nodes.First(n => n.Id == nodeId);
    
    switch (node.Type)
    {
        case WorkflowNodeType.Agent:
            actions.Add(new
            {
                kind = "BeginDialog",
                dialog = node.Data["agentName"] // agent 名称
            });
            // 继续下一个节点
            var nextEdge = workflow.Edges.FirstOrDefault(e => e.Source == nodeId);
            if (nextEdge != null)
            {
                BuildActionChain(nextEdge.Target, workflow, actions);
            }
            break;
            
        case WorkflowNodeType.Condition:
            var condition = node.Data["condition"]?.ToString();
            var trueEdge = workflow.Edges.First(e => e.Source == nodeId && e.SourceHandle == "true");
            var falseEdge = workflow.Edges.First(e => e.Source == nodeId && e.SourceHandle == "false");
            
            var trueActions = new List<object>();
            var falseActions = new List<object>();
            
            BuildActionChain(trueEdge.Target, workflow, trueActions);
            BuildActionChain(falseEdge.Target, workflow, falseActions);
            
            actions.Add(new
            {
                kind = "IfCondition",
                condition = ConvertToAdaptiveExpression(condition),
                actions = trueActions,
                elseActions = falseActions
            });
            break;
            
        case WorkflowNodeType.End:
            actions.Add(new
            {
                kind = "SendActivity",
                activity = "${turn.lastResult}"
            });
            break;
    }
}

private string ConvertToAdaptiveExpression(string scribanExpression)
{
    // 将 Scriban 表达式转换为 Adaptive Expression
    // 例如：{{ input.value > 10 }} -> =turn.input.value > 10
    return scribanExpression
        .Replace("{{", "=")
        .Replace("}}", "")
        .Replace("input.", "turn.input.")
        .Trim();
}
```

### Phase 2: 执行引擎重构

#### 2.1 删除 WorkflowExecutor

**文件：** `WorkflowExecutor.cs`

**操作：** 删除整个文件（315 行）

**原因：** Agent Framework 自带执行引擎，不需要手动循环

#### 2.2 使用 DeclarativeWorkflowBuilder

**文件：** `WorkflowService.cs`

**新方法：** `ExecuteWorkflowWithFrameworkAsync`

```csharp
public async IAsyncEnumerable<ExecutionEvent> ExecuteWorkflowWithFrameworkAsync(
    string workflowId,
    Dictionary<string, object> input)
{
    // 1. 加载工作流
    var workflow = await _workflowRepository.GetByIdAsync(workflowId);
    var yaml = await ConvertToAdaptiveDialogYamlAsync(workflowId);
    
    // 2. 保存 YAML 到临时文件
    var yamlPath = Path.Combine(Path.GetTempPath(), $"{workflowId}.yaml");
    await File.WriteAllTextAsync(yamlPath, yaml);
    
    try
    {
        // 3. 配置 DeclarativeWorkflowOptions
        var options = new DeclarativeWorkflowOptions(_workflowAgentProvider)
        {
            Configuration = _configuration,
            LoggerFactory = _loggerFactory
        };
        
        // 4. 构建 Workflow
        var adaptiveWorkflow = DeclarativeWorkflowBuilder.Build<Dictionary<string, object>>(
            yamlPath, 
            options
        );
        
        // 5. 执行工作流
        yield return new ExecutionEvent
        {
            Type = ExecutionEventType.WorkflowStarted,
            Status = ExecutionStatus.Running,
            Message = "开始执行工作流"
        };
        
        await foreach (var frameworkEvent in adaptiveWorkflow.RunAsync(input))
        {
            // 6. 转换 Agent Framework 事件到我们的 ExecutionEvent
            yield return MapFrameworkEvent(frameworkEvent);
        }
        
        yield return new ExecutionEvent
        {
            Type = ExecutionEventType.WorkflowCompleted,
            Status = ExecutionStatus.Completed,
            Message = "工作流执行完成"
        };
    }
    finally
    {
        // 7. 清理临时文件
        if (File.Exists(yamlPath))
        {
            File.Delete(yamlPath);
        }
    }
}

private ExecutionEvent MapFrameworkEvent(object frameworkEvent)
{
    // 将 Agent Framework 的事件映射到 ExecutionEvent
    // 需要根据实际事件类型进行转换
    
    return new ExecutionEvent
    {
        Type = ExecutionEventType.NodeExecuted,
        Status = ExecutionStatus.Running,
        Message = frameworkEvent.ToString()
    };
}
```

### Phase 3: 配置 OpenAI Client

#### 3.1 添加配置

**文件：** `appsettings.json`

```json
{
  "OpenAI": {
    "ApiKey": "your-api-key-here",
    "Model": "gpt-4",
    "Endpoint": "https://api.openai.com/v1" // 可选，用于 Azure OpenAI
  }
}
```

#### 3.2 注册真实 ChatClient

**文件：** `Program.cs`

```csharp
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"] 
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var openAiModel = builder.Configuration["OpenAI:Model"] ?? "gpt-4";

if (!string.IsNullOrEmpty(openAiApiKey))
{
    builder.Services.AddSingleton<IChatClient>(sp =>
    {
        return new OpenAIChatClient(openAiModel, openAiApiKey);
    });
}
else
{
    // 开发环境使用 mock
    builder.Services.AddSingleton<IChatClient, EmptyChatClient>();
}
```

## 实施步骤

### Step 1: 研究 Agent Framework YAML 格式 [2-3 小时]

**任务：**
1. 在 GitHub 搜索 microsoft/agent-framework 的示例 YAML 文件
2. 分析 AdaptiveDialog、BeginDialog、IfCondition 结构
3. 记录映射关系：当前节点 → Bot Framework Actions

**输出：**
- YAML 格式文档
- 节点映射表
- 示例转换代码片段

### Step 2: 实现 YAML 转换器 [4-6 小时]

**任务：**
1. 实现 `ConvertToAdaptiveDialogYamlAsync` 方法
2. 实现 `BuildActions` 递归构建 action 树
3. 实现条件表达式转换 (Scriban → Adaptive Expression)
4. 单元测试：简单工作流（start → agent → end）
5. 单元测试：条件分支工作流
6. 单元测试：多智能体顺序工作流

**输出：**
- 新的 YAML 转换代码
- 测试用例
- 转换后的 YAML 示例

### Step 3: 集成 DeclarativeWorkflowBuilder [2-3 小时]

**任务：**
1. 删除 `WorkflowExecutor.cs`
2. 实现 `ExecuteWorkflowWithFrameworkAsync`
3. 配置 DeclarativeWorkflowOptions
4. 处理 Agent Framework 事件流
5. 错误处理和日志记录

**输出：**
- 新的执行引擎代码
- 事件映射逻辑
- 错误处理机制

### Step 4: 配置 OpenAI Client [30 分钟]

**任务：**
1. 添加 OpenAI 配置到 appsettings.json
2. 注册 OpenAIChatClient
3. 测试 API 连接

**输出：**
- 配置文件
- 真实的 ChatClient 注册

### Step 5: 端到端测试 [2-3 小时]

**测试场景：**

1. **场景 1：简单智能体调用**
   - 工作流：Start → Agent(客户助手) → End
   - 输入：用户问题
   - 预期：Agent 返回回答

2. **场景 2：条件分支**
   - 工作流：Start → Condition → Agent(满意)/Agent(不满意) → End
   - 输入：满意度评分
   - 预期：根据条件调用不同 Agent

3. **场景 3：多智能体顺序**
   - 工作流：Start → Agent(筛选) → Agent(总结) → End
   - 输入：客户反馈列表
   - 预期：筛选 → 总结 → 输出

**验证点：**
- ✅ YAML 能被 DeclarativeWorkflowBuilder 加载
- ✅ WorkflowAgentProvider 正确解析 Agent
- ✅ Agent 执行返回真实 AI 响应
- ✅ 条件分支正确路由
- ✅ 事件流正确返回到前端
- ✅ 执行日志正确保存

## 风险和挑战

### 1. YAML 格式理解不足
**风险：** AdaptiveDialog 格式复杂，可能理解有误

**缓解：**
- 深入研究 Agent Framework 源码和示例
- 逐步实现，从简单工作流开始
- 编写充分的单元测试

### 2. 表达式转换困难
**风险：** Scriban 表达式转 Adaptive Expression 可能有差异

**缓解：**
- 限制支持的表达式类型
- 提供表达式验证工具
- 记录不支持的语法

### 3. 事件流映射复杂
**风险：** Agent Framework 事件结构可能与我们的 ExecutionEvent 不匹配

**缓解：**
- 研究 Agent Framework 事件类型
- 设计灵活的映射层
- 保留原始事件数据

### 4. 性能问题
**风险：** 临时文件 I/O 可能影响性能

**缓解：**
- 使用内存流代替文件（如果 DeclarativeWorkflowBuilder 支持）
- 缓存已构建的 Workflow
- 异步 I/O

## 预期收益

### 1. 符合官方规范 ✅
- 使用 Agent Framework 标准 YAML 格式
- 符合声明式工作流设计理念
- 更容易集成社区工具和示例

### 2. 减少维护成本 ✅
- 删除自定义执行引擎（315 行代码）
- 利用框架的错误处理和日志
- 框架更新自动受益

### 3. 更强大的功能 ✅
- 支持更复杂的工作流模式
- 利用 Bot Framework 的丰富功能
- 更好的调试和监控工具

### 4. 更好的可测试性 ✅
- 框架提供测试工具
- 更清晰的关注点分离
- 更容易 mock 和模拟

## 下一步行动

**立即开始：**
1. 研究 Agent Framework YAML 格式（搜索 microsoft/agent-framework 仓库）
2. 记录 AdaptiveDialog 结构和示例
3. 设计节点映射策略

**询问用户：**
- 是否有 OpenAI API Key 可用？
- 是否优先完成简单工作流测试？
- 是否需要支持复杂条件表达式？

## 参考资源

- microsoft/agent-framework GitHub 仓库
- Bot Framework Adaptive Dialogs 文档
- Microsoft.Agents.AI NuGet 包文档
- Agent Framework 社区示例
