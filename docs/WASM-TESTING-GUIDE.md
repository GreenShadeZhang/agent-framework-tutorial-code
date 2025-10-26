# WASM 模式测试指南

## 快速启动

### 方法 1: 使用 Aspire AppHost（推荐）

1. 打开终端，导航到 AppHost 目录：
```powershell
cd c:\github\agent-framework-tutorial-code\src\AgentGroupChat.AppHost
```

2. 运行 AppHost：
```powershell
dotnet run
```

3. 打开浏览器访问 Aspire Dashboard（通常是 `https://localhost:17000`）

4. 在 Dashboard 中找到 webfrontend 服务的 URL 并点击访问

### 方法 2: 分别启动服务

1. **启动 AgentHost API**：
```powershell
cd c:\github\agent-framework-tutorial-code\src\AgentGroupChat.AgentHost
dotnet run
```
记下 API 的端口号（例如：https://localhost:7390）

2. **更新 Web 配置**（如果需要）：
编辑 `src\AgentGroupChat.Web\wwwroot\appsettings.json`：
```json
{
  "AgentHostUrl": "https://localhost:7390"
}
```

3. **启动 Web 前端**：
```powershell
cd c:\github\agent-framework-tutorial-code\src\AgentGroupChat.Web
dotnet run
```

4. 打开浏览器访问 Web 前端（例如：https://localhost:5001）

## 测试发送按钮

### 验证步骤：

1. **页面加载检查**：
   - ✅ 页面应正常加载，显示 "Agent Group Chat" 标题
   - ✅ 侧边栏显示 "Conversations" 和 "New Chat" 按钮
   - ✅ 主区域显示可用的 Agent chips

2. **创建会话**：
   - ✅ 点击 "New Chat" 按钮
   - ✅ 侧边栏应显示新创建的会话

3. **发送消息测试**：
   - ✅ 在输入框输入测试消息，例如："Hello @Sunny, how are you?"
   - ✅ 点击 "Send" 按钮
   - ✅ 用户消息应立即显示在聊天区域（右侧，蓝色背景）
   - ✅ 应显示 "Thinking..." 加载指示器
   - ✅ Agent 响应应显示在聊天区域（左侧，白色背景）

4. **按键测试**：
   - ✅ 输入消息后按 Enter 键
   - ✅ 消息应发送（不需要点击按钮）
   - ✅ Shift+Enter 应换行而不发送

5. **禁用状态测试**：
   - ✅ 发送消息时，按钮应禁用并显示加载状态
   - ✅ 输入框为空时，按钮应禁用

## 浏览器开发者工具检查

### Network 标签：
1. 打开浏览器开发者工具 (F12)
2. 切换到 "Network" 标签
3. 发送消息后，应看到以下请求：
   - ✅ POST 请求到 `/api/chat`
   - ✅ 状态码应为 200 OK
   - ✅ 响应应包含 Agent 消息数据

### Console 标签：
1. 切换到 "Console" 标签
2. 不应有任何错误消息
3. 如果有错误，记录错误信息

## 常见问题排查

### 问题 1: 发送按钮点击没有反应
**可能原因**：
- HttpClient 配置错误
- AgentHost API 未运行
- CORS 配置问题

**解决方案**：
1. 检查浏览器 Console 是否有错误
2. 确认 AgentHost API 正在运行
3. 验证 `appsettings.json` 中的 URL 配置

### 问题 2: 页面加载很慢
**说明**：
- WASM 首次加载需要下载 .NET 运行时
- 这是正常现象
- 后续页面刷新会使用浏览器缓存，加载更快

### 问题 3: CORS 错误
**错误示例**：
```
Access to fetch at 'https://localhost:7390/api/chat' from origin 'https://localhost:5001' 
has been blocked by CORS policy
```

**解决方案**：
- 确认 AgentHost Program.cs 中已配置 CORS
- 当前配置使用 `AllowAnyOrigin()`，应该没有问题
- 如果仍有问题，检查 `app.UseCors("AllowWeb")` 是否在管道中正确调用

### 问题 4: 消息发送后没有响应
**检查点**：
1. 打开 Network 标签，查看 API 请求状态
2. 检查 AgentHost 日志输出
3. 验证 Azure OpenAI 配置是否正确

## 性能优化建议

### 首次加载优化：
```xml
<!-- 在 .csproj 中添加以下配置 -->
<PropertyGroup>
  <BlazorWebAssemblyLoadAllGlobalizationData>false</BlazorWebAssemblyLoadAllGlobalizationData>
  <InvariantGlobalization>true</InvariantGlobalization>
</PropertyGroup>
```

### 发布优化：
```powershell
# 使用 AOT 编译（需要更长的构建时间，但运行时性能更好）
dotnet publish -c Release

# 使用压缩
dotnet publish -c Release -p:BlazorEnableCompression=true
```

## 测试清单

- [ ] 页面正常加载
- [ ] 可以创建新会话
- [ ] 点击发送按钮可以发送消息
- [ ] 按 Enter 键可以发送消息
- [ ] Shift+Enter 可以换行
- [ ] 消息正确显示在聊天区域
- [ ] Agent 响应正确返回
- [ ] 加载状态正确显示
- [ ] 按钮禁用状态正常工作
- [ ] 没有 Console 错误
- [ ] Network 请求全部成功

## 成功标准

✅ **发送按钮工作正常**：点击后消息立即发送，UI 响应迅速
✅ **WASM 模式运行**：所有代码在客户端浏览器中执行
✅ **无服务器往返延迟**：UI 交互即时响应
✅ **API 通信正常**：成功调用 AgentHost API 并获取响应
