# Agent Framework YAML 工作流格式研究

## 关键发现：格式不兼容！

经过对 microsoft/agent-framework 仓库的深入研究，发现了一个**核心问题**：

**Agent Framework 使用 Bot Framework 的 AdaptiveDialog YAML 格式，而不是自定义的节点+边格式！**

## 正确的 YAML 格式结构

### 1. 根元素必须是 AdaptiveDialog/Workflow

```yaml
kind: Workflow  # 或 AdaptiveDialog (Workflow 是 AdaptiveDialog 的别名)
id: my_workflow
trigger:
  kind: OnConversationStart  # 或 OnUnknownIntent
  id: workflow_start
  actions:
    - kind: InvokeAgent
      id: agent_step_1
      # ...更多配置
```

**核心代码验证**：
```csharp
// DeclarativeWorkflowBuilder.cs#L131-L143
private static AdaptiveDialog ReadWorkflow(TextReader yamlReader)
{
    BotElement rootElement = YamlSerializer.Deserialize<BotElement>(yamlReader) 
        ?? throw new DeclarativeModelException("Workflow undefined.");

    // "Workflow" is an alias for "AdaptiveDialog"
    if (rootElement is not AdaptiveDialog workflowElement)
    {
        throw new DeclarativeModelException(
            $"Unsupported root element: {rootElement.GetType().Name}. Expected an {nameof(Workflow)}.");
    }

    return workflowElement;
}
```

### 2. Action 类型（代替我们的节点类型）

| 我们的节点类型 | Agent Framework Action | 说明 |
|--------------|----------------------|------|
| Start | OnConversationStart/OnUnknownIntent | 触发器，不是 action |
| Agent | InvokeAgent / InvokeAzureAgent | 调用智能体 |
| Condition | ConditionGroup / ConditionItem | 条件分支 |
| End | SendActivity | 发送输出消息 |

### 3. InvokeAgent Action 示例

```yaml
actions:
  - kind: InvokeAzureAgent  # 或 InvokeAgent
    id: agent_step_name
    conversationId: =System.ConversationId  # PowerFx 表达式
    agent:
      name: AgentName  # Agent 名称（不是 ID）
    output:
      messages: Local.AgentResponse  # 保存响应到变量
```

**关键点：**
- `agent.name` 引用 Agent 的**名称**，不是 ID
- 通过 `WorkflowAgentProvider.GetAsync(name)` 解析
- `conversationId` 使用 PowerFx 表达式引用系统变量

### 4. ConditionGroup (条件分支) 示例

```yaml
- kind: ConditionGroup
  id: check_condition
  conditions:
    - condition: =turn.input.value > 10  # PowerFx 表达式
      id: condition_true
      actions:
        - kind: SendActivity
          id: send_high
          activity: "Value is high"
    
    - condition: =turn.input.value <= 10
      id: condition_false
      actions:
        - kind: SendActivity
          id: send_low
          activity: "Value is low"
  
  elseActions:  # 可选的 else 分支
    - kind: SendActivity
      id: send_default
      activity: "Default message"
```

### 5. 完整示例：学生-教师工作流

```yaml
kind: Workflow
id: student_teacher_workflow
trigger:
  kind: OnConversationStart
  id: workflow_start
  actions:

    # Step 1: 学生智能体处理问题
    - kind: InvokeAzureAgent
      id: question_student
      conversationId: =System.ConversationId
      agent:
        name: StudentAgent

    # Step 2: 教师智能体提供指导
    - kind: InvokeAzureAgent
      id: question_teacher
      conversationId: =System.ConversationId
      agent:
        name: TeacherAgent
      output:
        messages: Local.TeacherResponse

    # Step 3: 增加轮次计数
    - kind: SetVariable
      id: set_count_increment
      variable: Local.TurnCount
      value: =Local.TurnCount + 1

    # Step 4: 检查是否完成
    - kind: ConditionGroup
      id: check_completion
      conditions:

        # 如果教师回复包含"CONGRATULATIONS"
        - condition: =!IsBlank(Find("CONGRATULATIONS", Upper(MessageText(Local.TeacherResponse))))
          id: check_turn_done
          actions:
            - kind: SendActivity
              id: sendActivity_done
              activity: GOLD STAR!

        # 如果还没超过4轮，继续
        - condition: =Local.TurnCount < 4
          id: check_turn_count
          actions:
            - kind: GotoAction
              id: goto_student_agent
              actionId: question_student  # 跳转回第一步

      # 超过4轮仍未完成
      elseActions:
        - kind: SendActivity
          id: sendActivity_tired
          activity: Let's try again later...
```

## 我们当前实现的问题

### ❌ 错误的自定义格式

```yaml
# 我们当前的格式（不兼容）
agents:
  - id: agent_1
    name: Customer Assistant
    # ...

workflow:
  nodes:
    - id: node_1
      type: Agent
      data:
        agentId: agent_1
        # ...
  edges:
    - source: node_1
      target: node_2
      # ...
```

**问题：**
1. 没有 `kind: Workflow` 根元素
2. 使用 `nodes` + `edges` 而不是 `actions` 序列
3. Agent 引用使用 `agentId` 而不是 `name`
4. 无法被 `DeclarativeWorkflowBuilder` 加载

## 正确的集成方式

### 1. YAML 转换逻辑重构

需要完全重写 `ConvertToAgentFrameworkYamlAsync` 方法：

```csharp
public async Task<string> ConvertToAdaptiveDialogYamlAsync(string workflowId)
{
    var workflow = await _workflowRepository.GetByIdAsync(workflowId);
    
    // 构建 AdaptiveDialog 结构
    var adaptiveDialog = new
    {
        kind = "Workflow",  // 或 AdaptiveDialog
        id = workflow.Id,
        trigger = new
        {
            kind = "OnUnknownIntent",
            id = $"{workflow.Id}_trigger",
            actions = BuildActionsSequence(workflow)  // 核心转换
        }
    };
    
    return SerializeToYaml(adaptiveDialog);
}

private List<object> BuildActionsSequence(WorkflowDefinition workflow)
{
    var actions = new List<object>();
    
    // 从 Start 节点开始，按照边的连接顺序构建 action 序列
    var startNode = workflow.Nodes.First(n => n.Type == WorkflowNodeType.Start);
    var currentEdge = workflow.Edges.First(e => e.Source == startNode.Id);
    
    // 递归或迭代构建 actions
    BuildActionChain(currentEdge.Target, workflow, actions);
    
    return actions;
}

private void BuildActionChain(string nodeId, WorkflowDefinition workflow, List<object> actions)
{
    var node = workflow.Nodes.First(n => n.Id == nodeId);
    
    switch (node.Type)
    {
        case WorkflowNodeType.Agent:
            // 映射到 InvokeAzureAgent
            actions.Add(new
            {
                kind = "InvokeAzureAgent",
                id = node.Id,
                conversationId = "=System.ConversationId",
                agent = new
                {
                    name = node.Data["agentName"]  // 使用 name，不是 ID
                }
            });
            
            // 继续处理下一个节点
            var nextEdge = workflow.Edges.FirstOrDefault(e => e.Source == nodeId);
            if (nextEdge != null)
            {
                BuildActionChain(nextEdge.Target, workflow, actions);
            }
            break;
            
        case WorkflowNodeType.Condition:
            // 映射到 ConditionGroup
            var trueEdge = workflow.Edges.First(e => e.Source == nodeId && e.SourceHandle == "true");
            var falseEdge = workflow.Edges.First(e => e.Source == nodeId && e.SourceHandle == "false");
            
            var trueActions = new List<object>();
            var falseActions = new List<object>();
            
            BuildActionChain(trueEdge.Target, workflow, trueActions);
            BuildActionChain(falseEdge.Target, workflow, falseActions);
            
            actions.Add(new
            {
                kind = "ConditionGroup",
                id = node.Id,
                conditions = new[]
                {
                    new
                    {
                        condition = ConvertScribanToPowerFx(node.Data["condition"]),
                        id = $"{node.Id}_true",
                        actions = trueActions
                    }
                },
                elseActions = falseActions
            });
            break;
            
        case WorkflowNodeType.End:
            actions.Add(new
            {
                kind = "SendActivity",
                id = node.Id,
                activity = "${turn.lastResult}"  // 输出最后的结果
            });
            break;
    }
}

private string ConvertScribanToPowerFx(string scribanExpression)
{
    // 将 Scriban 表达式转换为 PowerFx
    // {{ input.value > 10 }} -> =turn.input.value > 10
    return scribanExpression
        .Replace("{{", "=")
        .Replace("}}", "")
        .Replace("input.", "turn.input.")
        .Trim();
}
```

### 2. 执行引擎替换

**删除 `WorkflowExecutor.cs`**（315 行），使用框架：

```csharp
public async IAsyncEnumerable<ExecutionEvent> ExecuteWorkflowAsync(
    string workflowId,
    Dictionary<string, object> input)
{
    // 1. 转换为 AdaptiveDialog YAML
    var yaml = await ConvertToAdaptiveDialogYamlAsync(workflowId);
    
    // 2. 保存到临时文件（DeclarativeWorkflowBuilder 需要文件路径）
    var yamlPath = Path.Combine(Path.GetTempPath(), $"{workflowId}.yaml");
    await File.WriteAllTextAsync(yamlPath, yaml);
    
    try
    {
        // 3. 配置选项
        var options = new DeclarativeWorkflowOptions(_workflowAgentProvider)
        {
            Configuration = _configuration,
            LoggerFactory = _loggerFactory
        };
        
        // 4. 构建工作流
        var workflow = DeclarativeWorkflowBuilder.Build<Dictionary<string, object>>(
            yamlPath, 
            options
        );
        
        // 5. 执行（框架处理一切）
        await using var run = await InProcessExecution.StreamAsync(workflow, input);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
        
        await foreach (var frameworkEvent in run.WatchStreamAsync())
        {
            // 6. 转换事件格式
            yield return MapToExecutionEvent(frameworkEvent);
        }
    }
    finally
    {
        File.Delete(yamlPath);
    }
}
```

### 3. WorkflowAgentProvider 正确使用

我们已经创建的 `WorkflowAgentProviderImpl` 是**唯一正确的部分**：

```csharp
public class WorkflowAgentProviderImpl : WorkflowAgentProvider
{
    public override async Task<IAgent?> GetAsync(string name, CancellationToken ct)
    {
        // 1. 尝试按 ID 查找
        var agent = await _agentRepository.GetByIdAsync(name);
        
        // 2. 如果没找到，按名称查找
        if (agent == null)
        {
            var allAgents = await _agentRepository.GetAllAsync();
            agent = allAgents.FirstOrDefault(a => a.Name == name);
        }
        
        if (agent == null) return null;
        
        // 3. 返回 ChatClientAgent
        return new ChatClientAgent(
            _chatClient,
            instructions: agent.InstructionsTemplate,
            name: agent.Name,
            description: agent.Description
        );
    }
}
```

**已注册到 DI**：
```csharp
builder.Services.AddScoped<WorkflowAgentProvider, WorkflowAgentProviderImpl>();
```

## PowerFx 表达式系统

Agent Framework 使用 PowerFx（类似 Excel 公式）而不是 Scriban：

### 变量作用域

| PowerFx 作用域 | 说明 | 示例 |
|--------------|------|------|
| `System.*` | 系统变量 | `System.ConversationId` |
| `Local.*` | 局部变量 | `Local.TurnCount` |
| `Env.*` | 环境变量 | `Env.MY_AGENT` |
| `turn.*` | 当前轮次 | `turn.input.value` |

### 常用函数

| 函数 | 说明 | 示例 |
|-----|------|------|
| `IsBlank()` | 检查空值 | `=IsBlank(Local.Value)` |
| `Find()` | 查找文本 | `=Find("text", String)` |
| `Upper()` | 转大写 | `=Upper(turn.input)` |
| `If()` | 条件表达式 | `=If(condition, true_val, false_val)` |
| `MessageText()` | 提取消息文本 | `=MessageText(Local.Response)` |

### 表达式语法

```powerfx
# 变量赋值
=Local.Count + 1

# 条件判断
=turn.input.score > 80

# 文本查找（不区分大小写）
=!IsBlank(Find("SUCCESS", Upper(MessageText(Local.Response))))

# 复杂条件
=And(Local.Count < 5, IsBlank(Local.Error))
```

## Scriban 到 PowerFx 转换表

| Scriban | PowerFx | 说明 |
|---------|---------|------|
| `{{ input.value }}` | `=turn.input.value` | 访问输入 |
| `{{ count + 1 }}` | `=Local.count + 1` | 算术运算 |
| `{% if value > 10 %}` | `=value > 10` | 条件（在 ConditionGroup 中） |
| `{{ value \|\| "default" }}` | `=If(IsBlank(value), "default", value)` | 默认值 |
| `{{ list \| map: "name" }}` | 需要用 Table/Filter 函数 | 集合操作 |

## 支持的 Action 类型

### 控制流

- `ConditionGroup` / `ConditionItem` - 条件分支
- `GotoAction` - 跳转到指定 action
- `EndConversation` / `EndWorkflow` - 结束
- `LoopEach` - 循环遍历
- `BreakLoop` / `ContinueLoop` - 循环控制

### 变量操作

- `SetVariable` - 设置变量
- `ClearAllVariables` - 清除所有变量
- `ResetVariable` - 重置变量
- `EditTable` / `EditTableV2` - 编辑表格数据

### Agent/对话

- `InvokeAgent` / `InvokeAzureAgent` - 调用智能体
- `SendActivity` - 发送消息
- `CreateConversation` - 创建对话
- `AddConversationMessage` - 添加消息
- `RetrieveConversationMessages` - 检索消息

### 不支持的类型（会抛出异常）

- `BeginDialog` - 不支持子对话
- `HttpRequestAction` - 不支持 HTTP 请求
- `InvokeSkillAction` - 不支持技能调用
- `EmitEvent` - 不支持事件发射

## 测试用的简单工作流

### Marketing 工作流（3个智能体顺序执行）

```yaml
kind: Workflow
id: marketing_workflow
trigger:
  kind: OnUnknownIntent
  id: start_marketing
  actions:

    # 分析师
    - kind: InvokeAzureAgent
      id: analyst_step
      conversationId: =System.ConversationId
      agent:
        name: AnalystAgent
      output:
        messages: Local.AnalystOutput

    # 写手
    - kind: InvokeAzureAgent
      id: writer_step
      conversationId: =System.ConversationId
      agent:
        name: WriterAgent
      output:
        messages: Local.WriterOutput

    # 编辑
    - kind: InvokeAzureAgent
      id: editor_step
      conversationId: =System.ConversationId
      agent:
        name: EditorAgent
      output:
        messages: Local.FinalOutput

    # 输出结果
    - kind: SendActivity
      id: send_result
      activity: =MessageText(Local.FinalOutput)
```

### Condition 工作流（简单条件分支）

```yaml
kind: Workflow
id: condition_workflow
trigger:
  kind: OnUnknownIntent
  id: start_condition
  actions:

    # 设置测试变量
    - kind: SetVariable
      id: set_test_value
      variable: Local.TestValue
      value: =turn.input

    # 条件判断
    - kind: ConditionGroup
      id: check_value
      conditions:
        - condition: =Mod(Local.TestValue, 2) = 0  # 偶数
          id: even_branch
          actions:
            - kind: SendActivity
              id: send_even
              activity: "EVEN"
        
        - condition: =Mod(Local.TestValue, 2) = 1  # 奇数
          id: odd_branch
          actions:
            - kind: SendActivity
              id: send_odd
              activity: "ODD"
```

## 下一步行动

### Phase 1: 研究验证（已完成 ✅）
- ✅ GitHub 搜索 Agent Framework 示例
- ✅ 理解 AdaptiveDialog 结构
- ✅ 理解 InvokeAgent action
- ✅ 理解 ConditionGroup/ConditionItem
- ✅ 理解 PowerFx 表达式语法

### Phase 2: YAML 转换重构（待开始）
1. 重写 `ConvertToAdaptiveDialogYamlAsync` 方法
2. 实现 `BuildActionsSequence` 递归构建
3. 实现 Scriban → PowerFx 表达式转换
4. 单元测试：简单顺序工作流
5. 单元测试：条件分支工作流

### Phase 3: 执行引擎集成（待开始）
1. 删除 `WorkflowExecutor.cs`
2. 实现 `ExecuteWorkflowAsync` 使用 DeclarativeWorkflowBuilder
3. 实现事件映射：框架事件 → ExecutionEvent
4. 测试端到端执行

### Phase 4: OpenAI Client 配置（待开始）
1. 添加 OpenAI 配置到 appsettings.json
2. 替换 EmptyChatClient 为 OpenAIChatClient
3. 测试真实 AI 调用

### Phase 5: 完整测试（待开始）
1. 测试场景 1：简单智能体调用
2. 测试场景 2：条件分支
3. 测试场景 3：多智能体顺序

## 关键引用

### 官方文档

- workflow-samples/README.md - 工作流示例总览
- dotnet/src/Microsoft.Agents.AI.Workflows.Declarative/README.md - 支持的 Actions

### 核心代码

- DeclarativeWorkflowBuilder.cs#L131-L143 - YAML 加载验证
- DeclarativeWorkflowBuilder.cs#L63-L81 - Build 方法
- WorkflowFactory.cs#L30-L49 - 完整的工作流创建示例
- WorkflowActionVisitor.cs - Action 类型访问者模式

### 测试示例

- dotnet/tests/.../DeclarativeWorkflowTest.cs - 单元测试示例
- dotnet/tests/.../Workflows/Condition.yaml - 条件分支 YAML
- dotnet/tests/.../Workflows/InvokeAgent.yaml - Agent 调用 YAML
- dotnet/samples/.../Marketing.yaml - 多智能体示例

### Python 等价实现

- python/packages/declarative/agent_framework_declarative/_loader.py - Python YAML 加载
- python/samples/getting_started/declarative/ - Python 示例

## 总结

**关键认知转变：**

1. **不是节点+边，是 Action 序列** ⚠️
   - 工作流不是图，是线性的 action 列表
   - 条件分支通过嵌套的 actions/elseActions 实现
   - 循环通过 GotoAction 跳转实现

2. **Agent 通过名称引用，不是 ID** ⚠️
   - YAML 中使用 `agent.name`
   - WorkflowAgentProvider.GetAsync(name) 解析
   - 需要支持按名称查找

3. **PowerFx 不是 Scriban** ⚠️
   - 表达式语法完全不同
   - 变量作用域系统不同
   - 需要实现转换器

4. **框架执行，不是手动循环** ⚠️
   - DeclarativeWorkflowBuilder.Build() 构建
   - InProcessExecution.StreamAsync() 执行
   - 框架自动处理事件流

**估计工作量：**
- YAML 转换重构：4-6 小时
- 执行引擎替换：2-3 小时
- OpenAI 配置：30 分钟
- 测试验证：2-3 小时
- **总计：9-13 小时**

**风险：**
- 表达式转换可能有边界情况
- 事件映射可能不完整
- 需要充分的测试覆盖

我们已经有了正确的基础（WorkflowAgentProviderImpl），现在需要完全重构 YAML 格式和执行方式！
