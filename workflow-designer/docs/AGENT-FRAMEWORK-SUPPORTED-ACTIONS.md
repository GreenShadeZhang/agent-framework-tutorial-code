# Agent Framework 声明式工作流支持说明

## 概述

根据 [Agent Framework 官方文档](https://github.com/microsoft/agent-framework/tree/main/dotnet/src/Microsoft.Agents.AI.Workflows.Declarative)，以下是 **实际支持** 的 Action 类型。

## ✅ 官方支持的 Action 类型

### 🤖 智能体调用

| Kind | 说明 | 示例 |
|------|------|------|
| `InvokeAzureAgent` | 调用 Azure AI Foundry 智能体 | 核心节点，用于执行 AI 对话 |

```yaml
- kind: InvokeAzureAgent
  id: my_agent
  conversationId: =System.ConversationId
  agent:
    name: MyAgentName
  input:
    arguments:
      param1: =Local.SomeVariable
    externalLoop:
      when: =Not(Local.IsComplete)
  output:
    autoSend: true
    responseObject: Local.AgentResponse
```

### 🧭 流程控制

| Kind | 说明 |
|------|------|
| `ConditionGroup` | 条件分支（支持多个条件和 elseActions） |
| `GotoAction` | 跳转到指定 Action |
| `Foreach` | 遍历集合 |
| `BreakLoop` | 跳出循环 |
| `ContinueLoop` | 继续下一次迭代 |
| `EndWorkflow` | 结束当前工作流 |
| `EndConversation` | 结束整个会话 |

```yaml
# 条件分支
- kind: ConditionGroup
  id: check_status
  conditions:
    - condition: =Local.Status = "Success"
      id: success_branch
      actions:
        - kind: SendActivity
          id: send_success
          activity: "操作成功！"
  elseActions:
    - kind: SendActivity
      id: send_failure
      activity: "操作失败，请重试"

# 跳转
- kind: GotoAction
  id: goto_start
  actionId: first_action
```

### 📝 状态管理

| Kind | 说明 |
|------|------|
| `SetVariable` | 设置单个变量 |
| `SetMultipleVariables` | 设置多个变量 |
| `ClearAllVariables` | 清除所有变量 |
| `ResetVariable` | 重置变量 |
| `ParseValue` | 解析值 |
| `EditTableV2` | 编辑表格数据 |

```yaml
# 设置变量
- kind: SetVariable
  id: set_count
  variable: Local.Count
  value: =Local.Count + 1

# 设置文本变量
- kind: SetTextVariable
  id: set_message
  variable: Local.Message
  value: "Hello World"
```

### 💬 消息与会话

| Kind | 说明 |
|------|------|
| `SendActivity` | 发送消息给用户 |
| `AddConversationMessage` | 添加消息到会话 |
| `RetrieveConversationMessages` | 获取会话消息 |
| `CreateConversation` | 创建新会话 |
| `DeleteConversation` | 删除会话 |
| `CopyConversationMessages` | 复制会话消息 |

```yaml
# 发送消息
- kind: SendActivity
  id: greeting
  activity: "你好，{Local.UserName}！"

# 创建新会话
- kind: CreateConversation
  id: create_sub_conversation
  conversationId: Local.SubConversationId
```

### 🧑‍💼 人工输入

| Kind | 说明 |
|------|------|
| `Question` | 向用户提问 |

```yaml
- kind: Question
  id: ask_name
  prompt: "请输入您的姓名"
  variable: Local.UserName
```

## ❌ 当前设计器中需要移除或标记为"实验性"的类型

以下类型是我在设计时添加的，但 **不确定** Agent Framework 是否支持：

| 类型 | 状态 |
|------|------|
| `ChatAgent` | ❌ 应改为 `InvokeAzureAgent` |
| `FunctionAgent` | ❌ 应改为 `InvokeAzureAgent` + tools |
| `ToolAgent` | ❌ 应改为 `InvokeAzureAgent` + tools |
| `MagenticOrchestrator` | ⚠️ 实验性 |
| `McpTool` | ⚠️ 需要 MCP 扩展 |
| `OpenApiTool` | ⚠️ 需要 OpenAPI 扩展 |
| `CodeInterpreter` | ⚠️ Azure 特定 |
| `FileSearch` | ⚠️ Azure 特定 |
| `WebSearch` | ⚠️ Azure 特定 |
| `SubWorkflow` | ❓ 需验证 |
| `ParallelExecution` | ❓ 需验证 |
| `FanOut/FanIn` | ❓ 需验证 |

## 📋 完整工作流示例

### 示例 1：简单问候

```yaml
kind: Workflow
trigger:
  kind: OnConversationStart
  id: greeting_workflow
  actions:
    - kind: InvokeAzureAgent
      id: greeting_agent
      conversationId: =System.ConversationId
      agent:
        name: GreetingAgent

    - kind: SendActivity
      id: send_welcome
      activity: "欢迎使用智能助手！"

    - kind: EndWorkflow
      id: end
```

### 示例 2：带条件分支的客服

```yaml
kind: Workflow
trigger:
  kind: OnConversationStart
  id: support_workflow
  actions:
    # 分类问题
    - kind: InvokeAzureAgent
      id: classifier
      agent:
        name: ClassifierAgent
      output:
        responseObject: Local.Classification

    # 根据分类路由
    - kind: ConditionGroup
      id: route
      conditions:
        - condition: =Local.Classification.Type = "Technical"
          id: tech_route
          actions:
            - kind: InvokeAzureAgent
              id: tech_agent
              agent:
                name: TechSupportAgent

      elseActions:
        - kind: InvokeAzureAgent
          id: general_agent
          agent:
            name: GeneralAgent

    - kind: EndWorkflow
      id: end
```

### 示例 3：循环处理

```yaml
kind: Workflow
trigger:
  kind: OnConversationStart
  id: loop_workflow
  actions:
    - kind: SetVariable
      id: init_count
      variable: Local.Count
      value: =0

    - kind: InvokeAzureAgent
      id: process_agent
      agent:
        name: ProcessAgent
      input:
        externalLoop:
          when: =Local.Count < 3
      output:
        responseObject: Local.Result

    - kind: SetVariable
      id: increment
      variable: Local.Count
      value: =Local.Count + 1

    - kind: ConditionGroup
      id: check_done
      conditions:
        - condition: =Local.Result.IsComplete
          id: if_complete
          actions:
            - kind: GotoAction
              id: goto_end
              actionId: workflow_end

    - kind: GotoAction
      id: goto_process
      actionId: process_agent

    - kind: EndWorkflow
      id: workflow_end
```

## 🔧 执行闭环

### 1. 导入 YAML
前端调用 `POST /api/workflows/import-yaml` 将 YAML 转换为工作流定义

### 2. 渲染画布
工作流定义加载到 React Flow 画布显示

### 3. 保存工作流
调用 `POST /api/workflows` 保存到数据库

### 4. 执行工作流
调用 `POST /api/workflows/{id}/execute-framework` 执行

### 5. 查看结果
通过 SSE 流式接收执行事件和结果

## 🎯 建议的简化节点

基于 Agent Framework 官方支持，建议保留以下核心节点：

### 必需节点
1. **InvokeAzureAgent** - 智能体调用（核心）
2. **SendActivity** - 发送消息
3. **SetVariable** - 设置变量
4. **ConditionGroup** - 条件分支
5. **GotoAction** - 跳转
6. **EndWorkflow** - 结束

### 推荐节点
7. **CreateConversation** - 创建子会话
8. **Question** - 用户输入
9. **Foreach** - 循环处理

### 可选节点
10. **BreakLoop/ContinueLoop** - 循环控制
11. **CopyConversationMessages** - 会话管理
12. **SetMultipleVariables** - 批量设置
