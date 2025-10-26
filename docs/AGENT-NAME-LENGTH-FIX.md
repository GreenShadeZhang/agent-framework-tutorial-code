# Agent Name 长度限制问题修复

## 问题描述

### 错误信息
```
System.ClientModel.ClientResultException: HTTP 400 (invalid_request_error)

Invalid 'tools[0].function.name': string too long. 
Expected a string with maximum length 64, 
but got a string with length 67 instead.
```

### 根本原因

**OpenAI API 对工具函数名称有 64 字符的严格限制。**

Agent Framework 在创建 Handoff Workflow 时，会自动为每个 agent 生成 handoff 函数，函数名格式为：

```
handoff_to_{agent_name}_{guid}
```

#### 问题分析

之前的代码中，triage agent 的名称包含了 groupId：

```csharp
name: $"triage_{groupId}",  // ❌ 错误：名称太长
```

对于 `ai_world_mansion` 组：
- Triage agent name: `triage_ai_world_mansion`
- 加上框架自动生成的 GUID 后缀（32 字符）
- 生成的 handoff 函数名可能类似：
  ```
  handoff_to_anna_5aa6cb16cc96408a833f640756e0b578
  ```
  或者更复杂的格式，导致超过 64 字符限制

### 字符长度计算

| 组成部分 | 长度 | 示例 |
|---------|------|------|
| `handoff_to_` | 12 | 固定前缀 |
| agent name | 变量 | `anna` (4) 或 `triage_ai_world_mansion` (24) |
| `_` | 1 | 分隔符 |
| GUID (无连字符) | 32 | `5aa6cb16cc96408a833f640756e0b578` |
| **总计** | **45-69** | **取决于 agent name 长度** |

**安全限制**：agent name 应该 **< 20 字符**，以确保生成的函数名不超过 64 字符。

## 解决方案

### 修改前

```csharp
// ❌ 问题代码
var triageAgent = new ChatClientAgent(
    _chatClient,
    instructions: triageInstructions,
    name: $"triage_{groupId}",  // 可能很长，如 "triage_ai_world_mansion"
    description: $"Router for {group.Name}");
```

### 修改后

```csharp
// ✅ 修复后的代码
var triageAgent = new ChatClientAgent(
    _chatClient,
    instructions: triageInstructions,
    name: $"triage",  // 简短名称
    description: $"Router for {group.Name}");
```

### 关键改进

1. **移除 groupId**：triage agent 的名称从 `triage_{groupId}` 简化为 `triage`
2. **保持简短**：确保 agent name 尽可能短
3. **添加注释**：在代码中明确说明为什么需要简短名称

## 影响范围

### ✅ 不受影响的功能

- Agent 的实际功能（指令、工具等）
- Workflow 的路由逻辑
- 消息持久化
- 前端显示

### ✅ 改进的功能

- 所有 agent groups 都能正常工作
- 生成的 handoff 函数名符合 OpenAI API 限制
- 更清晰的日志输出

## 测试验证

### 1. 编译测试
```bash
cd src\AgentGroupChat.AgentHost
dotnet build
```

### 2. 运行测试
```bash
cd src\AgentGroupChat.AppHost
dotnet run
```

### 3. 功能测试

发送消息到任何 agent group，例如：
```
你好，安娜在做什么？
```

预期日志：
```
dbug: AgentChatService[0]
      Triage agent (ID: triage_{guid}) routing to: handoff_to_anna_{guid}
dbug: Microsoft.Extensions.AI.LoggingChatClient[1]
      GetStreamingResponseAsync invoked.
dbug: Microsoft.Extensions.AI.LoggingChatClient[2]
      GetStreamingResponseAsync completed.
```

**不应该** 再出现 "string too long" 错误。

## 最佳实践

### Agent Name 命名规则

1. **保持简短**：建议 < 20 字符
2. **使用简单标识符**：如 `elena`, `anna`, `triage`
3. **避免包含**：
   - 长的 GUID
   - 长的描述性文本
   - 复杂的组合 ID

### 示例

✅ **好的 agent name：**
```csharp
name: "anna"          // 4 字符
name: "triage"        // 6 字符
name: "artist"        // 6 字符
name: "researcher"    // 10 字符
```

❌ **不好的 agent name：**
```csharp
name: "triage_ai_world_mansion"              // 24 字符
name: "anna_virtual_assistant_v2"            // 28 字符
name: "specialized_researcher_for_science"   // 35 字符
```

### 计算公式

安全的 agent name 最大长度：
```
max_agent_name_length = 64 - 12 (prefix) - 1 (separator) - 32 (guid) - 3 (buffer)
                      = 16 字符
```

建议保留一些缓冲空间，所以实际建议 **< 15 字符**。

## 相关文件

修改的文件：
- ✅ `WorkflowManager.cs` - 简化了 triage agent 的名称

保持不变的文件：
- ✅ `AgentChatService.cs` - 日志逻辑已经正确处理简短名称
- ✅ `AgentRepository.cs` - Agent ID 本身就很短
- ✅ 数据库中的 Agent 配置

## 日志改进

修复后的日志将显示：

```
dbug: AgentChatService[0]
      Triage agent (ID: triage_a1b2c3d4...) routing to: handoff_to_anna_x1y2z3...
```

而不是之前的：

```
dbug: AgentChatService[0]
      Triage agent (ID: triage_ai_world_mansion_a1b2c3d4...) routing to: ...
```

## 验证清单

- [x] Agent name 长度 < 20 字符
- [x] 编译成功
- [x] 不再出现 "string too long" 错误
- [x] Handoff 功能正常工作
- [x] 日志清晰可读
- [x] 所有 agent groups 都能使用

## 总结

通过简化 agent name（特别是 triage agent），我们确保了生成的 handoff 函数名符合 OpenAI API 的 64 字符限制。

这是一个框架级别的限制，需要在设计 agent 时就考虑到。简短的 agent name 不仅能避免这个问题，还能让日志更清晰、代码更易读。

## 参考

- [OpenAI API 文档 - Function Calling](https://platform.openai.com/docs/guides/function-calling)
- [Microsoft Agent Framework - ChatClientAgent](https://github.com/microsoft/agents)
- Agent Framework 会自动为 agent 生成唯一的 GUID 后缀，这是无法避免的
- 因此，agent name 本身必须足够短

---

**问题已修复！** 🎉

现在可以正常使用任何 agent group，包括 `ai_world_mansion` 等长名称的组。
