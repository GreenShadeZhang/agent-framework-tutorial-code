# Triage Agent 修复验证指南

## 快速验证步骤

### 1. 启动应用
```powershell
cd src\AgentGroupChat.AppHost
dotnet run
```

### 2. 发送测试消息
打开浏览器访问应用，发送一条消息，例如：
```
Hello, how are you today?
```

### 3. 检查返回的响应

#### ✅ 正确的响应格式
```json
[
  {
    "agentId": "sunny_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
    "agentName": "Sunny",
    "agentAvatar": "☀️",
    "content": "Hi! I'm doing wonderfully! ☀️...",
    "isUser": false,
    "messageType": "text",
    "timestamp": "2025-10-26T09:45:00Z"
  }
]
```

#### ❌ 错误的响应格式（已修复）
```json
[
  {
    "agentId": "user",
    "content": "Hello, how are you today?",
    "isUser": true,  // ❌ 不应该返回用户消息
    ...
  },
  {
    "agentId": "triage_21d5d4b338b64955a5ec223cc13e7d2b",
    "content": "",   // ❌ 空的 triage 消息
    "isUser": false,
    ...
  },
  {
    "agentId": "sunny_xxxxx",
    "content": "Hi! ...",
    "isUser": false,
    ...
  }
]
```

### 4. 检查开发者工具

打开浏览器开发者工具 (F12) → Network → 找到 `/api/chat` 请求：

**响应应该只包含**:
- ✅ Specialist agent 的响应（1-2 条）
- ❌ **没有**用户消息
- ❌ **没有** `triage_xxxxx` 的消息
- ❌ **没有**空消息

### 5. 验证数据库

#### 使用调试端点
```bash
# 获取会话的所有消息
curl http://localhost:5000/api/debug/messages/{sessionId}
```

**预期结果**:
```json
{
  "sessionId": "xxxxx",
  "totalMessages": 2,  // 用户消息 + agent 响应
  "messages": [
    {
      "agentId": "user",
      "messageText": "Hello, how are you today?",
      "isUser": true,
      ...
    },
    {
      "agentId": "sunny_xxxxx",
      "agentName": "Sunny",
      "messageText": "Hi! I'm doing wonderfully!...",
      "isUser": false,
      ...
    }
  ]
}
```

**检查点**:
- ✅ 有用户消息（保存到数据库）
- ✅ 有 specialist agent 响应
- ❌ **没有** `triage_xxxxx` 的消息

### 6. 刷新页面验证

1. 刷新浏览器页面
2. 检查消息列表是否与刷新前一致
3. 确认没有出现 triage agent 的消息

### 7. 检查应用日志

查看终端输出，应该看到类似的日志：

```
[Debug] Triage agent (ID: triage_21d5d4b338b64955a5ec223cc13e7d2b) routing to: handoff with args: {"target":"sunny"}
[Debug] Agent switched to: sunny_xxxxxxxx (Sunny)
[Debug] Created summary for specialist agent sunny_xxxxxxxx
[Information] Collected 1 agent responses for session xxx
[Information] Returning 1 filtered responses (excluded triage and empty messages) for session xxx
[Information] Saved 2 messages to LiteDB for session xxx (Agent: sunny_xxxxx)
```

**关键点**:
- ✅ "Triage agent (ID: triage_xxxxx) routing to..." - triage 被识别
- ✅ "Created summary for specialist agent" - 只为 specialist 创建 summary
- ✅ "Returning X filtered responses" - 最终过滤确保干净的响应

## 常见问题排查

### 问题 1: 仍然看到 triage 消息

**症状**: 响应中包含 `agentId: "triage_xxxxx"` 的消息

**检查**:
1. 确认代码已经编译并重启
2. 检查日志中是否有 "Triage agent (ID: triage_xxxxx)"
3. 如果没有，说明 ID 格式检查失败

**解决**: 确认修复代码已应用（检查第 265 行）

### 问题 2: 看到空消息

**症状**: 响应中有 `content: ""` 的消息

**检查**:
1. 查看消息的 `agentId`
2. 检查是否是 triage agent

**解决**: 确认最终过滤层已添加（检查返回前的过滤逻辑）

### 问题 3: 看到重复的用户消息

**症状**: 消息列表中用户消息出现两次

**检查**:
1. 前端是否做了乐观更新
2. 后端是否还在返回用户消息

**解决**: 确认后端不再添加用户消息到 summaries

## 性能验证

### 测试多次对话

发送 5-10 条消息，每次检查：
- ✅ 响应时间正常（1-3 秒）
- ✅ 消息列表正确
- ✅ 没有内存泄漏
- ✅ 日志正常

### 测试并发

打开多个浏览器标签页，同时发送消息：
- ✅ 每个会话独立
- ✅ 消息不会混淆
- ✅ triage agent 对所有会话都是透明的

## 完成标准

当以下所有项都通过时，修复验证完成：

- [ ] 发送消息后，响应中**没有**用户消息
- [ ] 发送消息后，响应中**没有** triage agent 消息
- [ ] 发送消息后，响应中**没有**空消息
- [ ] 刷新页面后，消息列表保持一致
- [ ] 数据库中**没有** triage agent 的记录
- [ ] 日志显示 triage agent 被正确识别和跳过
- [ ] 多次对话都正常工作
- [ ] 不同会话之间不会互相影响

## 回归测试

确保修复没有影响其他功能：

- [ ] 新建会话功能正常
- [ ] 删除会话功能正常
- [ ] 会话列表显示正常
- [ ] Agent 切换功能正常（如果有 @mention）
- [ ] 图片生成功能正常（如果触发）
- [ ] 错误处理正常

---

**验证完成日期**: _____________

**验证人**: _____________

**结果**: ✅ 通过 / ❌ 未通过

**备注**: _____________
