# Agent Group Chat - 消息接收问题修复报告

## 问题描述
项目运行后收不到任何 Agent 回复的消息内容。

## 根本原因分析

通过对比 [官方示例代码](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/GettingStarted/Workflows/_Foundational/04_AgentWorkflowPatterns/Program.cs)，发现以下关键问题:

### 1. Triage Agent 系统提示不够强制
**问题**: Triage agent 的系统提示没有明确要求它"总是"切换到其他 agent，可能导致它自己回复而不是转发。

**修复前**:
```csharp
"If no specific agent is mentioned, respond with a friendly greeting and list available agents."
```

**修复后**:
```csharp
"ALWAYS handoff to another agent. Do NOT respond yourself - only route to the appropriate agent. " +
"If no specific agent is mentioned, handoff to Sunny."
```

这是官方 handoff 模式的关键模式 - triage agent 必须强制切换，不能自己回复。

### 2. 文本提取逻辑不完整
**问题**: 只尝试从 `AgentRunResponseUpdate.Text` 获取文本，但某些情况下这个属性可能为空，需要从 `Contents` 集合中提取。

**修复前**:
```csharp
responseText.Append(updateEvent.Update.Text);
```

**修复后**:
```csharp
var updateText = updateEvent.Update.Text;
if (!string.IsNullOrEmpty(updateText))
{
	responseText.Append(updateText);
}
else if (updateEvent.Update.Contents != null)
{
	foreach (var content in updateEvent.Update.Contents)
	{
		if (content is Microsoft.Extensions.AI.TextContent textContent)
		{
			responseText.Append(textContent.Text);
		}
	}
}
```

### 3. 缺少调试日志
**问题**: 没有日志输出，无法诊断事件流问题。

**修复**: 添加了详细的控制台日志:
```csharp
Console.WriteLine($"[DEBUG] Event type: {evt.GetType().Name}");
Console.WriteLine($"[DEBUG] AgentRunUpdateEvent - ExecutorId: {updateEvent.ExecutorId}, Update.Text: '{updateEvent.Update.Text}'");
```

## 修改的文件

### `src/AgentGroupChat/Services/AgentChatService.cs`

#### 修改点 1: 更新 Triage Agent 系统提示 (行 100-107)
```csharp
var triageAgent = new ChatClientAgent(_chatClient,
	"You are a triage agent that routes messages to the appropriate agent based on mentions. " +
	"When a user mentions an agent with @AgentName, you MUST handoff to that agent immediately. " +
	"Available agents: @Sunny (cheerful), @Techie (tech-savvy), @Artsy (artistic), @Foodie (food-loving). " +
	"ALWAYS handoff to another agent. Do NOT respond yourself - only route to the appropriate agent. " +
	"If no specific agent is mentioned, handoff to Sunny.",
	"triage",
	"Routes messages to the appropriate agent");
```

#### 修改点 2: 改进文本提取逻辑 (行 147-188)
```csharp
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
	// Debug logging
	Console.WriteLine($"[DEBUG] Event type: {evt.GetType().Name}");
    
	if (evt is AgentRunUpdateEvent updateEvent)
	{
		Console.WriteLine($"[DEBUG] AgentRunUpdateEvent - ExecutorId: {updateEvent.ExecutorId}, Update.Text: '{updateEvent.Update.Text}'");
        
		if (updateEvent.ExecutorId != currentAgentId)
		{
			// Save previous agent's message if any
			if (currentAgentId != null && responseText.Length > 0)
			{
				var profile = GetAgentProfile(currentAgentId);
				if (profile != null)
				{
					messages.Add(new Models.ChatMessage
					{
						AgentId = profile.Id,
						AgentName = profile.Name,
