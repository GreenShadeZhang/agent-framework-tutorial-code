# 快速入门指南 (Quick Start Guide)

## 欢迎使用 Workflow Designer! 🎉

这是一个完整的工作流设计和执行系统，包含智能体管理和可视化工作流设计器。

## 第一步：启动后端服务

### 1. 打开终端并进入后端目录

```bash
cd c:\github\agent-framework-tutorial-code\workflow-designer\src\backend\WorkflowDesigner.Api
```

### 2. 运行后端服务

```bash
dotnet run
```

你会看到类似这样的输出：

```
Building...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

### 3. 测试 API

打开浏览器访问 Swagger UI：
```
https://localhost:5001/swagger
```

你应该能看到所有的 API 端点：
- `/api/agents` - 智能体管理
- `/api/workflows` - 工作流管理

## 第二步：启动前端应用

### 1. 打开**新的**终端窗口

保持后端服务运行，打开第二个终端。

### 2. 进入前端目录

```bash
cd c:\github\agent-framework-tutorial-code\workflow-designer\src\frontend
```

### 3. 运行前端开发服务器

```bash
npm run dev
```

你会看到：

```
  VITE v7.2.7  ready in 619 ms

  ➜  Local:   http://localhost:5173/
  ➜  Network: use --host to expose
```

### 4. 打开应用

在浏览器中访问：
```
http://localhost:5173
```

## 第三步：创建第一个智能体

### 方式 1：使用 Swagger UI

1. 访问 `https://localhost:5001/swagger`
2. 找到 `POST /api/agents` 端点
3. 点击 "Try it out"
4. 使用以下测试数据：

```json
{
  "name": "代码助手",
  "description": "帮助生成和解释代码的AI助手",
  "instructionsTemplate": "你是一个专业的编程助手。{{prompt}}",
  "type": "Coder",
  "modelConfig": {
    "model": "gpt-4",
    "temperature": 0.7,
    "maxTokens": 2000,
    "topP": 1.0
  },
  "tools": [],
  "metadata": {}
}
```

5. 点击 "Execute"
6. 返回前端页面 `http://localhost:5173` 刷新即可看到新创建的智能体

### 方式 2：使用 PowerShell 命令

```powershell
$body = @{
    name = "代码助手"
    description = "帮助生成和解释代码的AI助手"
    instructionsTemplate = "你是一个专业的编程助手。{{prompt}}"
    type = "Coder"
    modelConfig = @{
        model = "gpt-4"
        temperature = 0.7
        maxTokens = 2000
        topP = 1.0
    }
    tools = @()
    metadata = @{}
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/agents" `
    -Method Post `
    -ContentType "application/json" `
    -Body $body
```

## 第四步：创建工作流

### 1. 切换到工作流设计标签

在前端应用中，点击顶部的 **"工作流设计"** 标签。

### 2. 开始设计

你会看到一个可视化画布，上面有一个"开始"节点。

### 3. 添加节点（即将实现）

- 从左侧拖拽智能体节点到画布
- 连接节点构建工作流
- 配置每个节点的参数

### 4. 保存工作流

点击右上角的"保存"按钮保存你的工作流设计。

### 5. 执行工作流

点击"执行"按钮运行工作流，查看实时输出。

## 常见问题

### Q: 后端启动时出现端口占用错误

A: 修改 `Properties/launchSettings.json` 中的端口号，或者关闭占用 5000/5001 端口的其他程序。

### Q: 前端连接不到后端

A: 检查以下几点：
1. 后端服务是否正常运行（访问 `http://localhost:5000/swagger`）
2. 前端 `.env` 文件中的 `VITE_API_URL` 是否正确
3. 浏览器控制台是否有 CORS 错误（后端已配置 CORS，应该不会有问题）

### Q: 数据保存在哪里？

A: 数据保存在 LiteDB 数据库中，文件位置：
```
src/backend/WorkflowDesigner.Api/Data/workflow-designer.db
```

如需重置数据，直接删除这个文件即可。

### Q: 如何添加更多智能体类型？

A: 编辑 `Models/AgentDefinition.cs` 中的 `AgentType` 枚举：

```csharp
public enum AgentType
{
    Assistant,
    WebSurfer,
    Coder,
    Custom,
    YourNewType  // 添加你的新类型
}
```

## 下一步

### 学习更多

- 📖 阅读完整的实施计划：`../docs/WORKFLOW-DESIGNER-IMPLEMENTATION-PLAN.md`
- 📖 查看项目完成总结：`../docs/WORKFLOW-DESIGNER-PROJECT-SUMMARY.md`

### 开发功能

当前项目包含基础架构，你可以继续开发：

1. **工作流执行引擎**
   - 集成 Microsoft.Extensions.AI
   - 实现节点执行逻辑
   - 添加实时状态推送

2. **更多节点类型**
   - 条件节点
   - 循环节点
   - 并行节点
   - 自定义节点

3. **UI 增强**
   - 拖拽组件面板
   - 节点配置弹窗
   - 执行历史面板
   - 错误高亮显示

4. **高级功能**
   - 工作流模板库
   - 版本控制
   - 协作功能
   - 性能监控

## 技术支持

遇到问题？

1. 查看根目录 `../docs/` 下的相关文档
2. 检查浏览器控制台和后端日志
3. 使用 Swagger UI 测试 API
4. 查看 GitHub Issues

## 开发环境建议

### VS Code 推荐扩展

- C# Dev Kit
- ESLint
- Prettier
- Tailwind CSS IntelliSense
- REST Client

### 调试技巧

**后端调试**：
```bash
cd src/backend/WorkflowDesigner.Api
dotnet run --environment Development
```

查看详细日志，包括数据库操作和 API 调用。

**前端调试**：

浏览器按 F12 打开开发者工具，查看：
- Console - 日志和错误
- Network - API 请求
- React DevTools - 组件状态

## 祝你开发愉快！🚀

项目已经搭建完成，现在可以开始添加你想要的功能了！

---

**提示**：这只是第一阶段的基础架构，后续还有很多激动人心的功能等待实现。参考实施计划文档，按照 10 个阶段逐步完成整个系统。
