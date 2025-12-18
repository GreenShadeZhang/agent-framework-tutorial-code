# 声明式工作流服务重构说明

## 重构目的

将 `DeclarativeWorkflowService` 从自实现的执行逻辑重构为使用 Microsoft Agent Framework 的标准执行引擎。

## 问题分析

### 重构前的问题

1. **DeclarativeWorkflowService 自实现执行逻辑**
   - 自己解析 YAML
   - 自己实现了 executor 执行逻辑（`ExecuteExecutorAsync`）
   - 自己管理变量和控制流
   - 没有使用 Agent Framework 的 `DeclarativeWorkflowBuilder.Build()` 和 `InProcessExecution.StreamAsync()`

2. **代码重复**
   - `WorkflowService` 已经正确使用了 Agent Framework
   - `DeclarativeWorkflowService` 重复实现了类似功能

3. **维护困难**
   - 两套执行逻辑需要分别维护
   - 自实现的逻辑可能与 Agent Framework 不一致

## 重构方案

### 架构设计

```
前端 Copilot Studio YAML
    ↓
YamlConversionService (双向转换)
    ↓
DeclarativeWorkflowDefinition (内部格式存储)
    ↓
执行时: ConvertToYaml() → Agent Framework 兼容的 YAML
    ↓
WorkflowService.ExecuteYamlWorkflowAsync()
    ↓
DeclarativeWorkflowBuilder.Build()
    ↓
InProcessExecution.StreamAsync()
```

### 重构内容

#### 1. WorkflowService 新增方法

在 `WorkflowService` 中提取了通用的 Agent Framework 执行方法：

```csharp
public async IAsyncEnumerable<ExecutionEvent> ExecuteYamlWorkflowAsync(
    string yaml,
    string userInput,
    CancellationToken cancellationToken = default)
{
    // 1. 保存临时 YAML 文件
    // 2. 使用 DeclarativeWorkflowBuilder.Build() 构建工作流
    // 3. 使用 InProcessExecution.StreamAsync() 执行
    // 4. 映射事件并流式返回
}
```

**特点：**
- 接受 YAML 字符串作为输入
- 使用 Agent Framework 的标准流程
- 可被多个服务复用

#### 2. DeclarativeWorkflowService 重构

**构造函数变更：**
```csharp
// 之前：注入 IChatClient
public DeclarativeWorkflowService(
    IRepository<DeclarativeWorkflowDefinition> repository,
    YamlConversionService yamlService,
    IChatClient chatClient,  // ❌ 删除
    ILogger<DeclarativeWorkflowService> logger)

// 之后：注入 IWorkflowService
public DeclarativeWorkflowService(
    IRepository<DeclarativeWorkflowDefinition> repository,
    YamlConversionService yamlService,
    IWorkflowService workflowService,  // ✅ 新增
    ILogger<DeclarativeWorkflowService> logger)
```

**ExecuteAsync 方法重构：**
```csharp
// 之前：自己实现执行逻辑
public async Task<DeclarativeExecutionResult> ExecuteAsync(string id, string userInput)
{
    // ❌ 自己管理变量、执行器、控制流
    var variables = new Dictionary<string, object>();
    while (!string.IsNullOrEmpty(currentExecutorId)) {
        var output = await ExecuteExecutorAsync(executor, variables, userInput);
        // ...
    }
}

// 之后：使用 Agent Framework
public async Task<DeclarativeExecutionResult> ExecuteAsync(string id, string userInput)
{
    // ✅ 转换为 YAML
    var yaml = _yamlService.ConvertToYaml(workflow);
    
    // ✅ 使用 Agent Framework 执行
    await foreach (var evt in _workflowService.ExecuteYamlWorkflowAsync(yaml, userInput))
    {
        // 收集事件并构建结果
    }
}
```

**ExecuteStreamAsync 方法重构：**
```csharp
// 之前：自己实现流式执行
public async IAsyncEnumerable<ExecutionEvent> ExecuteStreamAsync(...)
{
    // ❌ 自己管理执行流程
    while (!string.IsNullOrEmpty(currentExecutorId)) {
        yield return new ExecutionEvent { ... };
        var output = await ExecuteExecutorAsync(...);
        // ...
    }
}

// 之后：直接转发 Agent Framework 事件
public async IAsyncEnumerable<ExecutionEvent> ExecuteStreamAsync(...)
{
    // ✅ 转换为 YAML
    string yaml = _yamlService.ConvertToYaml(workflow);
    
    // ✅ 直接转发 Agent Framework 的事件流
    await foreach (var evt in _workflowService.ExecuteYamlWorkflowAsync(yaml, userInput, cancellationToken))
    {
        yield return evt;
    }
}
```

#### 3. 删除的方法

以下自实现的方法已被删除：

- ❌ `ExecuteExecutorAsync()` - 执行单个执行器
- ❌ `ExecuteSetVariable()` - 设置变量
- ❌ `ExecuteSendActivity()` - 发送活动
- ❌ `ExecuteQuestion()` - 处理问题
- ❌ `ExecuteInvokeAgentAsync()` - 调用智能体
- ❌ `GetNextExecutorId()` - 获取下一个执行器
- ❌ `ReplaceVariables()` - 替换变量引用
- ❌ `EvaluateCondition()` - 评估条件表达式

这些功能现在全部由 Agent Framework 处理。

## Agent Framework 执行流程

### 标准执行流程

```csharp
// 1. 创建 WorkflowAgentProvider
var agentProvider = new SimpleWorkflowAgentProvider(
    chatClient, 
    agentRepository,
    logger);

// 2. 创建 DeclarativeWorkflowOptions
var options = new DeclarativeWorkflowOptions(agentProvider)
{
    LoggerFactory = loggerFactory
};

// 3. 从 YAML 文件构建 Workflow
Workflow workflow = DeclarativeWorkflowBuilder.Build<string>(yamlPath, options);

// 4. 执行 Workflow
StreamingRun run = await InProcessExecution.StreamAsync(
    workflow,
    userInput,
    cancellationToken: cancellationToken
);

// 5. 监听并处理事件
await foreach (var evt in run.WatchStreamAsync(cancellationToken))
{
    // 处理 ExecutorInvokedEvent, ExecutorCompletedEvent, ExecutorFailedEvent 等
    var executionEvent = MapWorkflowEventToExecutionEvent(evt);
    yield return executionEvent;
}
```

### 事件映射

Agent Framework 的事件类型映射到我们的 `ExecutionEvent`：

| Agent Framework Event | ExecutionEvent Type |
|----------------------|---------------------|
| `ExecutorInvokedEvent` | `NodeStarted` |
| `ExecutorCompletedEvent` | `NodeCompleted` |
| `ExecutorFailedEvent` | `NodeFailed` |
| `WorkflowErrorEvent` | `WorkflowFailed` |
| `AgentRunUpdateEvent` | `ProgressUpdate` |
| `WorkflowOutputEvent` | `LogMessage` |
| `MessageActivityEvent` | `LogMessage` |

## 优势

### 1. 使用官方标准
- 使用 Microsoft Agent Framework 的官方实现
- 享受框架的后续更新和优化
- 与 Copilot Studio 保持一致

### 2. 代码简化
- 删除了约 400 行自实现代码
- 核心执行逻辑从 ~150 行降至 ~30 行
- 更容易理解和维护

### 3. 功能完整
- 支持所有 Agent Framework 的功能
- 正确处理变量、条件、循环等
- 支持 PowerFx 表达式

### 4. 可维护性
- 单一执行引擎，统一维护
- 减少 bug 风险
- 更容易扩展

## 保留的功能

### YamlConversionService

`YamlConversionService` 继续负责格式转换：

1. **ParseFromYaml**: 将 Copilot Studio YAML 解析为内部格式
2. **ConvertToYaml**: 将内部格式转换为 Agent Framework 兼容的 YAML

这个服务的作用是：
- 前端可以编辑 Copilot Studio 格式的 YAML
- 后端存储使用内部的 `DeclarativeWorkflowDefinition` 格式
- 执行时转换为 Agent Framework 兼容的 YAML

## 测试建议

### 1. 基本执行测试
```bash
# 测试工作流执行
curl -X POST http://localhost:5000/api/declarative-workflows/{id}/execute \
  -H "Content-Type: application/json" \
  -d '{"userInput": "test message"}'
```

### 2. 流式执行测试
```bash
# 测试 SSE 流式执行
curl -N http://localhost:5000/api/declarative-workflows/{id}/execute-stream?userInput=test
```

### 3. YAML 转换测试
```bash
# 导入 Copilot Studio YAML
curl -X POST http://localhost:5000/api/declarative-workflows/import \
  -H "Content-Type: text/plain" \
  --data-binary @workflow.yaml

# 导出为 Copilot Studio YAML
curl http://localhost:5000/api/declarative-workflows/{id}/export
```

## 迁移注意事项

### 对现有工作流的影响

1. **已存储的工作流**
   - 无需修改，内部格式未变
   - 执行时自动转换为 Agent Framework YAML

2. **API 兼容性**
   - API 接口保持不变
   - 返回的事件格式可能略有差异

3. **自定义 Executor**
   - 如果有自定义的执行器类型，需要在 YamlConversionService 中添加转换逻辑

### 潜在问题

1. **变量引用格式**
   - Agent Framework 使用 PowerFx 表达式
   - 确保 YamlConversionService 正确转换变量引用

2. **条件表达式**
   - Agent Framework 使用 PowerFx 进行条件评估
   - 复杂条件可能需要调整格式

3. **Agent 调用**
   - 确保 `SimpleWorkflowAgentProvider` 正确实现
   - 验证 Agent 的 instructions 和参数传递

## 下一步工作

1. ✅ 重构 DeclarativeWorkflowService
2. ✅ 添加 ExecuteYamlWorkflowAsync 通用方法
3. ✅ 更新依赖注入配置
4. 🔲 集成测试
5. 🔲 性能测试
6. 🔲 文档更新

## 参考资料

- [Agent Framework GitHub](https://github.com/microsoft/agent-framework)
- [Agent Framework Declarative Workflows](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/GettingStarted/Workflows/Declarative)
- [DeclarativeWorkflowBuilder API](https://github.com/microsoft/agent-framework/blob/main/dotnet/src/Microsoft.Agents.AI.Workflows.Declarative/DeclarativeWorkflowBuilder.cs)
