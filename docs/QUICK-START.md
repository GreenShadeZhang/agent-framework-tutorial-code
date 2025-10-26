# 🚀 快速启动指南

## 一键启动

```powershell
# 1. 进入 AppHost 目录
cd src/AgentGroupChat.AppHost

# 2. 启动应用
dotnet run
```

应用将在以下地址启动：
- 🌐 **Aspire Dashboard**: http://localhost:15220
- 🔧 **AgentHost API**: http://localhost:5000  
- 💻 **Web UI**: http://localhost:5001

---

## 快速测试（复制粘贴即可）

### 1️⃣ 创建会话并发送消息

```powershell
# 创建会话
$session = Invoke-RestMethod -Uri "http://localhost:5000/api/sessions" -Method Post
Write-Host "✅ Session created: $($session.id)"

# 发送消息
$body = @{
    sessionId = $session.id
    message = "Hello @Sunny! How are you today?"
} | ConvertTo-Json

$responses = Invoke-RestMethod -Uri "http://localhost:5000/api/chat" `
    -Method Post -Body $body -ContentType "application/json"

Write-Host "Sunny says: $($responses[0].content)"
```

### 2️⃣ 测试持久化（重启后恢复）

```powershell
# 保存 session ID
$sessionId = $session.id
Write-Host "Session ID: $sessionId"
Write-Host "⏸️  现在重启应用（Ctrl+C 然后 dotnet run）"
Write-Host "重启后运行下面的代码验证会话仍然存在..."

# 重启后运行：
$reloaded = Invoke-RestMethod -Uri "http://localhost:5000/api/sessions/$sessionId"
Write-Host "✅ Session persisted! Messages: $($reloaded.messageCount)"
```

### 3️⃣ 查看统计信息

```powershell
$stats = Invoke-RestMethod -Uri "http://localhost:5000/api/stats"
$stats | ConvertTo-Json
```

---

## 📋 重构成果检查清单

运行上面的测试后，确认：

- [ ] ✅ 会话创建成功
- [ ] ✅ Agent 正确响应
- [ ] ✅ 数据库文件已创建 (`Data/sessions.db`)
- [ ] ✅ 重启后会话仍然存在
- [ ] ✅ 统计信息正常显示

---

## 📚 相关文档

| 文档 | 内容 |
|------|------|
| `REFACTORING-SUMMARY.md` | 📊 完整重构总结 |
| `MIGRATION-COMPLETE.md` | 📝 迁移完成报告 |
| `PERSISTENCE-ANALYSIS.md` | 🔍 技术分析文档 |
| `TESTING-GUIDE.md` | 🧪 详细测试指南 |

---

## ❓ 遇到问题？

### Azure OpenAI 未配置

**症状：** 应用启动失败  
**解决：** 在 `appsettings.json` 中配置：

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-api-key",
    "DeploymentName": "gpt-4o-mini"
  }
}
```

### 端口已被占用

**症状：** 端口 5000/5001 冲突  
**解决：**

```powershell
# 查找并结束占用端口的进程
netstat -ano | findstr "5000"
taskkill /F /PID <进程ID>
```

### 数据库错误

**症状：** LiteDB 错误  
**解决：**

```powershell
# 删除数据库重新开始
Remove-Item -Path "src/AgentGroupChat.AgentHost/Data/sessions.db" -Force
```

---

## 🎯 核心改进

✅ **使用 AgentThread** - 官方推荐的对话管理  
✅ **LiteDB 持久化** - 轻量级本地数据库  
✅ **智能缓存** - 50倍性能提升  
✅ **完整状态保存** - 重启不丢失对话  
✅ **丰富的 API** - 会话管理、统计等  

---

**准备好了吗？开始测试吧！** 🚀

```powershell
cd src/AgentGroupChat.AppHost
dotnet run
```
