# 快速测试指南 - 消息持久化修复验证

## 应用启动

✅ **应用已启动**
- Dashboard: https://localhost:17148
- AgentHost API: http://localhost:5152 (或查看 Dashboard 中的端口)
- Web Frontend: http://localhost:5089 (或查看 Dashboard 中的端口)

## 测试场景

### 测试 1：发送消息不重复 ✅

**步骤**：
1. 打开 Web 应用
2. 发送消息："Hello"
3. 检查消息列表

**预期结果**：
- ✅ 消息列表显示 **1 条用户消息**（不是 2 条）
- ✅ 显示 1 条 AI 回复

**验证点**：
- 用户消息只出现一次
- AI 回复正确显示

---

### 测试 2：刷新页面保留消息 ✅

**步骤**：
1. 在会话中发送 3-5 条消息
2. 按 F5 刷新浏览器页面
3. 等待页面加载完成

**预期结果**：
- ✅ 所有历史消息正确显示
- ✅ 消息顺序正确
- ✅ 消息内容完整（包括用户消息和 AI 回复）

**验证点**：
- 刷新前后消息数量一致
- 消息内容和顺序保持不变

---

### 测试 3：切换会话显示正确消息 ✅

**步骤**：
1. 点击 "New Chat" 创建第二个会话
2. 在第二个会话中发送不同的消息："Hi, I'm in session 2"
3. 点击会话列表中的第一个会话
4. 再次切换回第二个会话

**预期结果**：
- ✅ 每个会话显示各自的历史消息
- ✅ 切换会话时消息不混乱

**验证点**：
- Session 1 显示 "Hello" 等消息
- Session 2 显示 "Hi, I'm in session 2" 等消息
- 切换会话不丢失消息

---

### 测试 4：数据库持久化 ✅

**步骤**：
1. 发送几条消息
2. 在终端中按 Ctrl+C 停止应用
3. 重新运行 `dotnet run`
4. 打开 Web 应用

**预期结果**：
- ✅ 所有会话仍然存在
- ✅ 所有历史消息从数据库正确恢复

**验证点**：
- 会话列表包含之前创建的会话
- 打开会话后显示完整的历史消息
- LiteDB 文件存在：`src/AgentGroupChat.AgentHost/bin/Debug/net9.0/Data/sessions.db`

---

### 测试 5：@mention 功能 ✅

**步骤**：
1. 发送消息："@Sunny show me a photo"
2. 等待 AI 回复

**预期结果**：
- ✅ Sunny Agent 回复消息
- ✅ 可能包含图片（50% 概率）
- ✅ 消息正确持久化

**验证点**：
- Agent 头像和名称显示为 "☀️ Sunny"
- 回复内容符合 Sunny 的性格（cheerful and optimistic）

---

## API 端点测试

### 1. 获取所有会话
```bash
curl http://localhost:5152/api/sessions
```

**预期返回**：
```json
[
  {
    "id": "xxx-xxx-xxx",
    "name": "Session 2024-10-26 ...",
    "createdAt": "...",
    "lastUpdated": "...",
    "messageCount": 5,
    "isActive": true,
    "messages": []
  }
]
```

### 2. 获取会话历史消息
```bash
curl http://localhost:5152/api/sessions/{session-id}/messages
```

**预期返回**：
```json
[
  {
    "content": "Hello",
    "isUser": true,
    "timestamp": "...",
    "messageType": "text"
  },
  {
    "agentId": "triage",
    "agentName": "Assistant",
    "agentAvatar": "🤖",
    "content": "Hello! How can I help you?",
    "isUser": false,
    "timestamp": "...",
    "messageType": "text"
  }
]
```

### 3. 发送消息
```bash
curl -X POST http://localhost:5152/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "{session-id}",
    "message": "Hello"
  }'
```

**预期返回**：包含用户消息和 AI 回复的数组

---

## 已知限制

1. **图片生成**：
   - 使用占位图片 API (via.placeholder.com)
   - 50% 概率触发
   - 需要网络连接

2. **会话清理**：
   - 旧会话不会自动删除
   - 可以手动调用 `/api/stats` 查看统计信息

---

## 故障排查

### 问题：消息不显示

**检查**：
1. 打开浏览器开发者工具 → Network 标签页
2. 检查 API 请求：
   - `GET /api/sessions/{id}/messages` 是否成功返回数据
   - 返回的数据格式是否正确

**解决**：
- 确保 AgentHost 服务正在运行
- 检查 sessionId 是否有效

### 问题：刷新后消息消失

**检查**：
1. 确认 `src/AgentGroupChat.AgentHost/bin/Debug/net9.0/Data/sessions.db` 文件存在
2. 查看文件大小是否增长（发送消息后）

**解决**：
- 如果文件不存在，检查 `PersistedSessionService` 的初始化
- 查看应用日志是否有错误

### 问题：消息重复

**检查**：
1. 确认前端代码已更新（`SendMessage` 方法不再手动添加用户消息）
2. 检查后端 API 返回的数据（应该包含用户消息）

---

## 成功标准 ✅

- [ ] 发送消息时，用户消息只显示一次
- [ ] 刷新页面后，所有历史消息正确显示
- [ ] 切换会话时，显示正确的历史消息
- [ ] 重启应用后，数据从 LiteDB 正确恢复
- [ ] @mention 功能正常工作
- [ ] API 端点返回正确的数据格式

---

## 下一步

如果所有测试通过：
1. ✅ 继续使用和测试其他功能
2. ✅ 可以开始实现更多 Agent 特性
3. ✅ 考虑添加更多测试用例

如果有问题：
1. 查看 `docs/BUG-FIX-MESSAGE-PERSISTENCE.md` 了解修复细节
2. 检查应用日志
3. 使用浏览器开发者工具调试
