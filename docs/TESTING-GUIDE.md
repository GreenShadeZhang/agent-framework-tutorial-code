# 快速测试指南

## 🚀 快速开始

### 1. 启动应用

```powershell
# 进入 AppHost 目录
cd src/AgentGroupChat.AppHost

# 运行应用（会启动 AgentHost 和 Web）
dotnet run
```

应用会在以下地址启动：
- **Aspire Dashboard**: http://localhost:15220
- **AgentHost API**: http://localhost:5000
- **Web UI**: http://localhost:5001

### 2. 测试 API（使用 PowerShell）

#### 创建新会话
```powershell
$response = Invoke-RestMethod -Uri "http://localhost:5000/api/sessions" -Method Post
$sessionId = $response.id
Write-Host "Created session: $sessionId"
```

#### 发送消息
```powershell
$body = @{
    sessionId = $sessionId
    message = "Hello @Sunny, how are you today?"
} | ConvertTo-Json

$responses = Invoke-RestMethod -Uri "http://localhost:5000/api/chat" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"

$responses | ForEach-Object {
    Write-Host "$($_.agentName): $($_.content)"
}
```

#### 获取会话历史
```powershell
$history = Invoke-RestMethod -Uri "http://localhost:5000/api/sessions/$sessionId/messages"
Write-Host "Total messages: $($history.Count)"
```

#### 获取统计信息
```powershell
$stats = Invoke-RestMethod -Uri "http://localhost:5000/api/stats"
Write-Host "Statistics:"
$stats | ConvertTo-Json
```

#### 清空对话
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/sessions/$sessionId/clear" -Method Post
Write-Host "Conversation cleared"
```

#### 删除会话
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/sessions/$sessionId" -Method Delete
Write-Host "Session deleted"
```

---

## 🧪 完整测试场景

### 场景 1: 基础对话流程

```powershell
# 1. 创建会话
$session = Invoke-RestMethod -Uri "http://localhost:5000/api/sessions" -Method Post
Write-Host "✅ Created session: $($session.id)"

# 2. 发送第一条消息
$msg1 = @{
    sessionId = $session.id
    message = "Hi @Sunny! Tell me about your day."
} | ConvertTo-Json

$resp1 = Invoke-RestMethod -Uri "http://localhost:5000/api/chat" `
    -Method Post -Body $msg1 -ContentType "application/json"
Write-Host "✅ Sunny responded: $($resp1[0].content)"

# 3. 发送第二条消息（测试上下文）
$msg2 = @{
    sessionId = $session.id
    message = "What did you just say about?"
} | ConvertTo-Json

$resp2 = Invoke-RestMethod -Uri "http://localhost:5000/api/chat" `
    -Method Post -Body $msg2 -ContentType "application/json"
Write-Host "✅ Sunny remembered context: $($resp2[0].content)"

# 4. 获取历史
$history = Invoke-RestMethod -Uri "http://localhost:5000/api/sessions/$($session.id)/messages"
Write-Host "✅ Total messages in history: $($history.Count)"
```

### 场景 2: 多 Agent 对话

```powershell
# 创建会话
$session = Invoke-RestMethod -Uri "http://localhost:5000/api/sessions" -Method Post

# 与 Techie 对话
$msg1 = @{
    sessionId = $session.id
    message = "@Techie, what's your favorite programming language?"
} | ConvertTo-Json

$resp1 = Invoke-RestMethod -Uri "http://localhost:5000/api/chat" `
    -Method Post -Body $msg1 -ContentType "application/json"
Write-Host "Techie: $($resp1[0].content)"

# 与 Artsy 对话
$msg2 = @{
    sessionId = $session.id
    message = "@Artsy, what inspires you?"
} | ConvertTo-Json

$resp2 = Invoke-RestMethod -Uri "http://localhost:5000/api/chat" `
    -Method Post -Body $msg2 -ContentType "application/json"
Write-Host "Artsy: $($resp2[0].content)"

# 与 Foodie 对话
$msg3 = @{
    sessionId = $session.id
    message = "@Foodie, what's for lunch?"
} | ConvertTo-Json

$resp3 = Invoke-RestMethod -Uri "http://localhost:5000/api/chat" `
    -Method Post -Body $msg3 -ContentType "application/json"
Write-Host "Foodie: $($resp3[0].content)"
```

### 场景 3: 持久化测试

```powershell
Write-Host "=== 持久化测试 ==="

# 1. 创建会话并发送消息
$session = Invoke-RestMethod -Uri "http://localhost:5000/api/sessions" -Method Post
$sessionId = $session.id

$msg = @{
    sessionId = $sessionId
    message = "Hello, remember this message!"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/chat" `
    -Method Post -Body $msg -ContentType "application/json"

Write-Host "✅ Message sent, session ID: $sessionId"
Write-Host "⏸️  Please restart the application now..."
Write-Host "Press Enter after restarting..."
Read-Host

# 2. 验证会话仍然存在
try {
    $reloadedSession = Invoke-RestMethod -Uri "http://localhost:5000/api/sessions/$sessionId"
    Write-Host "✅ Session persisted! Message count: $($reloadedSession.messageCount)"
    
    # 3. 发送新消息（测试 Thread 恢复）
    $msg2 = @{
        sessionId = $sessionId
        message = "Do you remember what I said before?"
    } | ConvertTo-Json
    
    $resp = Invoke-RestMethod -Uri "http://localhost:5000/api/chat" `
        -Method Post -Body $msg2 -ContentType "application/json"
    Write-Host "✅ Agent remembered context: $($resp[0].content)"
}
catch {
    Write-Host "❌ Session not found! Persistence failed."
}
```

### 场景 4: 性能测试

```powershell
Write-Host "=== 性能测试 ==="

# 创建测试会话
$session = Invoke-RestMethod -Uri "http://localhost:5000/api/sessions" -Method Post
$sessionId = $session.id

# 测试第一次访问（从数据库）
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$s1 = Invoke-RestMethod -Uri "http://localhost:5000/api/sessions/$sessionId"
$time1 = $sw.ElapsedMilliseconds
Write-Host "First access (DB): ${time1}ms"

# 测试第二次访问（从缓存）
$sw.Restart()
$s2 = Invoke-RestMethod -Uri "http://localhost:5000/api/sessions/$sessionId"
$time2 = $sw.ElapsedMilliseconds
Write-Host "Second access (Cache): ${time2}ms"

if ($time2 -lt $time1) {
    Write-Host "✅ Cache is working! Speedup: $([math]::Round($time1/$time2, 2))x"
} else {
    Write-Host "⚠️ Cache might not be working properly"
}
```

---

## 🔍 验证检查清单

运行测试后，检查以下项目：

### 数据持久化
- [ ] `Data/sessions.db` 文件已创建
- [ ] 重启应用后会话仍然存在
- [ ] 对话历史完整保留
- [ ] AgentThread 状态正确恢复

### 功能测试
- [ ] 创建会话成功
- [ ] 发送消息获得响应
- [ ] @mention 正确路由到对应 Agent
- [ ] 获取会话列表正常
- [ ] 删除会话成功
- [ ] 清空对话保留会话

### 性能测试
- [ ] 缓存提升性能（第二次访问更快）
- [ ] 统计信息准确
- [ ] 没有内存泄漏（长时间运行）

### 错误处理
- [ ] 无效 SessionId 返回 404
- [ ] 空消息返回 400
- [ ] 服务异常返回错误消息

---

## 🐛 常见问题

### 1. 应用启动失败

**检查：**
```powershell
# 检查端口是否被占用
netstat -ano | findstr "5000"
netstat -ano | findstr "5001"

# 检查 Azure OpenAI 配置
$env:AZURE_OPENAI_ENDPOINT
$env:AZURE_OPENAI_API_KEY
```

### 2. 数据库错误

**解决：**
```powershell
# 删除旧数据库并重新开始
Remove-Item -Path "src/AgentGroupChat.AgentHost/Data/sessions.db" -Force
```

### 3. Agent 不响应

**检查日志：**
```powershell
# 查看应用日志
# Aspire Dashboard -> Logs -> AgentHost
```

---

## 📊 预期输出示例

### 成功的测试输出

```
✅ Created session: abc123...
✅ Sunny responded: What a wonderful day! The sun is shining...
✅ Sunny remembered context: I just mentioned that the sun is shining...
✅ Total messages in history: 4

=== 统计信息 ===
{
  "TotalSessions": 5,
  "ActiveSessions": 3,
  "CachedSessions": 2,
  "DatabaseSizeBytes": 51200
}

=== 性能测试 ===
First access (DB): 8ms
Second access (Cache): 0.5ms
✅ Cache is working! Speedup: 16x
```

---

## 📝 测试报告模板

测试完成后，填写以下报告：

```
测试日期：____________________
测试人员：____________________

✅ 基础功能测试
  [ ] 会话创建
  [ ] 消息发送
  [ ] Agent 路由
  [ ] 历史获取

✅ 持久化测试
  [ ] 数据保存
  [ ] 应用重启
  [ ] Thread 恢复
  [ ] 上下文保持

✅ 性能测试
  [ ] 缓存效果: ___倍提升
  [ ] 响应时间: ___ms
  [ ] 数据库大小: ___KB

问题记录：
_________________________________
_________________________________
_________________________________

总体评价：[ ] 通过  [ ] 需要修复
```

---

## 🎯 下一步

测试通过后：
1. 提交代码到 Git
2. 更新文档
3. 部署到测试环境
4. 准备生产环境配置

测试失败时：
1. 查看日志文件
2. 检查配置
3. 参考 [TROUBLESHOOTING.md](./TROUBLESHOOTING.md)
4. 报告问题
