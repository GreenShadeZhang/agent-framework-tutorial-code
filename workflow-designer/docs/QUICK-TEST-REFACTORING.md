# 重构后的快速测试指南

## 测试目标

验证 DeclarativeWorkflowService 重构后能够正确使用 Agent Framework 执行工作流。

## 前置条件

1. 确保 OpenAI API Key 已配置：
   ```bash
   # 在 appsettings.json 中设置
   "OpenAI": {
     "ApiKey": "your-api-key-here"
   }
   
   # 或设置环境变量
   export OPENAI_API_KEY="your-api-key-here"
   ```

2. 启动应用：
   ```bash
   cd workflow-designer/WorkflowDesigner.AppHost
   dotnet run
   ```

## 测试步骤

### 1. 创建测试 Agent

```bash
curl -X POST http://localhost:5085/api/agents \
  -H "Content-Type: application/json" \
  -d '{
    "name": "TestAgent",
    "instructionsTemplate": "You are a helpful assistant. Answer the user question: {{user_input}}"
  }'
```

记录返回的 Agent ID。

### 2. 创建声明式工作流

```bash
curl -X POST http://localhost:5085/api/declarative-workflows \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Simple Test Workflow",
    "description": "测试 Agent Framework 执行",
    "startExecutorId": "start",
    "executors": [
      {
        "id": "start",
        "name": "Start",
        "type": "SetVariable",
        "config": {
          "variableName": "greeting",
          "value": "Hello from Agent Framework!"
        }
      },
      {
        "id": "agent",
        "name": "Call Agent",
        "type": "InvokeAzureAgent",
        "config": {
          "name": "TestAgent",
          "instructionsTemplate": "You are a helpful assistant."
        }
      },
      {
        "id": "end",
        "name": "End",
        "type": "EndWorkflow"
      }
    ],
    "edgeGroups": [
      {
        "sourceExecutorId": "start",
        "edges": [
          {
            "targetExecutorId": "agent"
          }
        ]
      },
      {
        "sourceExecutorId": "agent",
        "edges": [
          {
            "targetExecutorId": "end"
          }
        ]
      }
    ]
  }'
```

记录返回的 Workflow ID。

### 3. 测试非流式执行

```bash
curl -X POST http://localhost:5085/api/declarative-workflows/{workflow-id}/execute \
  -H "Content-Type: application/json" \
  -d '{
    "userInput": "What is the capital of France?"
  }'
```

**期望结果：**
- `status`: "Completed"
- `steps`: 包含执行的步骤
- `output`: 包含执行结果

### 4. 测试流式执行（SSE）

```bash
curl -N http://localhost:5085/api/declarative-workflows/{workflow-id}/execute-stream?userInput=Tell%20me%20a%20joke
```

**期望结果：**
- 实时返回 SSE 事件
- 看到 `WorkflowStarted`, `NodeStarted`, `NodeCompleted`, `WorkflowCompleted` 事件

### 5. 测试 YAML 导入导出

#### 导出为 YAML
```bash
curl http://localhost:5085/api/declarative-workflows/{workflow-id}/export
```

保存返回的 YAML 内容。

#### 导入 YAML
```bash
curl -X POST http://localhost:5085/api/declarative-workflows/import \
  -H "Content-Type: text/plain" \
  --data-binary @workflow.yaml
```

## 验证要点

### ✅ 功能验证

1. **Agent Framework 初始化**
   - 检查日志：`Workflow built successfully`
   - 检查日志：`Calling InProcessExecution.StreamAsync`

2. **事件流转**
   - 收到 `ExecutorInvokedEvent` 并映射为 `NodeStarted`
   - 收到 `ExecutorCompletedEvent` 并映射为 `NodeCompleted`
   - 收到最终的 `WorkflowCompleted` 事件

3. **Agent 调用**
   - 检查日志：`Invoking agent TestAgent`
   - 检查日志：`ChatClient returned response`

4. **YAML 转换**
   - 导出的 YAML 是 Agent Framework 兼容格式
   - 可以重新导入并执行

### ❌ 错误检查

如果看到以下错误，说明配置有问题：

1. **"Failed to build workflow from YAML"**
   - 检查 YAML 格式是否正确
   - 查看详细错误日志

2. **"Agent not found"**
   - 确保 Agent 已创建
   - 检查 Agent 名称是否正确

3. **"OpenAI API key not configured"**
   - 配置 OpenAI API Key
   - 或者会使用 EmptyChatClient（返回模拟响应）

## 日志分析

### 成功执行的关键日志

```
[INFO] 开始流式执行声明式工作流: Simple Test Workflow
[INFO] Workflow built successfully
[INFO] Calling InProcessExecution.StreamAsync with input: Tell me a joke
[INFO] StreamingRun created, starting WatchStreamAsync...
[INFO] Received workflow event #1: ExecutorInvokedEvent
[INFO] Received workflow event #2: ExecutorCompletedEvent
[INFO] Received workflow event #3: ExecutorInvokedEvent
[INFO] Invoking agent TestAgent in conversation xxx
[INFO] ChatClient returned response: [response text]
[INFO] Received workflow event #4: ExecutorCompletedEvent
[INFO] WatchStreamAsync completed, total events: 5
```

### 使用 Agent Framework 的标识

- ✅ 看到 `DeclarativeWorkflowBuilder.Build`
- ✅ 看到 `InProcessExecution.StreamAsync`
- ✅ 看到 `ExecutorInvokedEvent`, `ExecutorCompletedEvent` 等事件
- ❌ 不应看到 `执行节点:` (旧的自实现日志)
- ❌ 不应看到 `初始化变量上下文` (旧的自实现日志)

## 对比测试

### 重构前 vs 重构后

#### 重构前（自实现）
```
[INFO] 开始执行声明式工作流: Test, 用户输入: hello
[INFO] 初始化变量上下文
[DEBUG] 执行节点: Start, 类型: SetVariable
[INFO] 调用智能体 TestAgent, 输入: hello
[INFO] 工作流执行完成: Test, 执行节点数: 3
```

#### 重构后（Agent Framework）
```
[INFO] 开始流式执行声明式工作流: Test
[INFO] Workflow built successfully
[INFO] Calling InProcessExecution.StreamAsync with input: hello
[INFO] Received workflow event #1: ExecutorInvokedEvent
[INFO] Invoking agent TestAgent in conversation xxx
[INFO] ChatClient returned response: ...
[INFO] WatchStreamAsync completed, total events: 5
```

## 性能测试

```bash
# 测试执行时间
time curl -X POST http://localhost:5085/api/declarative-workflows/{id}/execute \
  -H "Content-Type: application/json" \
  -d '{"userInput": "test"}'

# 测试并发
ab -n 10 -c 2 -p request.json -T application/json \
  http://localhost:5085/api/declarative-workflows/{id}/execute
```

## 故障排查

### 问题：工作流执行失败

1. 检查 YAML 转换：
   ```bash
   curl http://localhost:5085/api/declarative-workflows/{id}/export
   ```

2. 查看详细日志：
   ```bash
   # 设置日志级别为 Debug
   export ASPNETCORE_ENVIRONMENT=Development
   ```

3. 验证 Agent 配置：
   ```bash
   curl http://localhost:5085/api/agents
   ```

### 问题：没有使用 Agent Framework

检查代码：
- `DeclarativeWorkflowService.cs` 应该调用 `_workflowService.ExecuteYamlWorkflowAsync()`
- 不应该有 `ExecuteExecutorAsync`, `GetNextExecutorId` 等方法

### 问题：事件流不完整

- 检查是否正确映射了所有 Agent Framework 事件类型
- 查看 `MapWorkflowEventToExecutionEvent` 方法

## 成功标准

- ✅ 工作流能够成功执行
- ✅ 日志显示使用了 Agent Framework
- ✅ 事件流正确转发
- ✅ Agent 调用成功
- ✅ YAML 导入导出正常
- ✅ 代码中没有自实现的执行逻辑

## 下一步

测试通过后：
1. 更新前端以支持新的事件格式
2. 添加更多复杂的工作流测试
3. 性能优化和监控
4. 文档更新
