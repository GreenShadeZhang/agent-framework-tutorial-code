# 调试清单 - Agent Group Chat 接收不到消息

## 问题分析

根据对比 [官方示例](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/GettingStarted/Workflows/_Foundational/04_AgentWorkflowPatterns/Program.cs) 的代码，我发现了以下关键差异：

## 已修复的问题

### 1. ✅ TurnToken 调用
- **问题**: 需要确保调用 `await run.TrySendMessageAsync(new TurnToken(emitEvents: true));`
- **状态**: 已存在，无需修改

### 2. ✅ Triage Agent 提示词
- **问题**: Triage agent 需要明确指示"ALWAYS handoff to another agent"
- **修复**: 已更新系统提示，强制要求切换到其他 agent

### 3. ✅ 文本提取逻辑
- **问题**: `AgentRunResponseUpdate.Text` 可能为空，需要从 `Contents` 集合提取
- **修复**: 已添加备用逻辑，先尝试 `.Text`，如果为空则从 `Contents` 提取

### 4. ✅ 添加调试日志
- **修复**: 已添加 Console.WriteLine 跟踪事件流

## 测试步骤

1. **启动应用**:
   ```powershell
   cd c:\github\agent-framework-tutorial-code\src\AgentGroupChat
   dotnet run
   ```

2. **查看控制台输出**:
   - 应该看到 `[DEBUG] Event type: ...` 日志
   - 应该看到 `[DEBUG] AgentRunUpdateEvent - ExecutorId: ...` 日志

3. **在浏览器中测试**:
   - 访问 https://localhost:5001 (或控制台显示的地址)
   - 发送消息: "Hello @Sunny"
   - 检查控制台是否有事件输出
   - 检查浏览器是否显示回复

## 可能的问题和解决方案

### 如果控制台没有任何 [DEBUG] 输出:
- **原因**: Workflow 可能没有正确启动或 TurnToken 没有触发
- **检查**: 
  1. Azure OpenAI 配置是否正确 (endpoint, API key, deployment name)
  2. 是否有异常被捕获但没有显示

### 如果有 AgentRunUpdateEvent 但 Text 为空:
- **原因**: Agent 可能在输出 FunctionCall 而不是文本
- **解决**: 查看 updateEvent.Update.Contents 的内容类型

### 如果 triage agent 自己在回复:
- **原因**: 系统提示不够强制
- **解决**: 进一步加强 "ALWAYS handoff" 指令

### 如果完全没有事件:
- **可能原因**:
  1. Workflow 构建有问题
  2. StreamingRun 没有正确初始化
  3. WatchStreamAsync() 阻塞了
- **调试**: 在 `InProcessExecution.StreamAsync` 前后添加日志

## 关键代码对比

### 官方示例 (正确):
```csharp
await using StreamingRun run = await InProcessExecution.StreamAsync(workflow, messages);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
	if (evt is AgentRunUpdateEvent e)
	{
		if (e.ExecutorId != lastExecutorId)
		{
			lastExecutorId = e.ExecutorId;
			Console.WriteLine();
			Console.WriteLine(e.ExecutorId);
		}
		Console.Write(e.Update.Text);
	}
	else if (evt is WorkflowOutputEvent output)
	{
		Console.WriteLine();
		return output.As<List<ChatMessage>>()!;
	}
}
```

### 你的代码 (已修复):
```csharp
await using StreamingRun run = await InProcessExecution.StreamAsync(_workflow, chatMessages);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
	if (evt is AgentRunUpdateEvent updateEvent)
	{
