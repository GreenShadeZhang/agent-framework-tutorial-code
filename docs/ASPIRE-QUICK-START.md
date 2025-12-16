# Workflow Designer - Aspire 快速参考

## 🚀 一键启动
```bash
cd workflow-designer
dotnet run --project WorkflowDesigner.AppHost
```

## 📍 访问地址

| 服务 | URL | 说明 |
|-----|-----|------|
| 前端应用 | http://localhost:5173 | 工作流设计器界面 |
| Aspire Dashboard | https://localhost:17000 | 监控和日志 |
| API Swagger | http://localhost:5000/swagger | API 文档 |
| 健康检查 | http://localhost:5000/health | 健康状态 |

## 🔑 配置 API Key

```bash
cd WorkflowDesigner.Api
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
dotnet user-secrets set "OpenAI:Model" "gpt-4o-mini"
```

## 📂 项目结构

```
workflow-designer/
├── WorkflowDesigner.AppHost/        # Aspire 主机 (启动这个)
├── WorkflowDesigner.ServiceDefaults/# 共享配置
├── WorkflowDesigner.Api/            # 后端 API
├── frontend/                        # React 前端
└── WorkflowDesigner.sln             # 解决方案
```

## 🛠️ 常用命令

```bash
# 构建
dotnet build WorkflowDesigner.sln

# 运行 (Aspire)
dotnet run --project WorkflowDesigner.AppHost

# 单独运行后端
cd WorkflowDesigner.Api && dotnet run

# 单独运行前端
cd frontend && npm run dev

# 清理
dotnet clean WorkflowDesigner.sln
```

## 📊 Aspire Dashboard 功能

打开 https://localhost:17000

- **Resources** - 查看所有服务状态
- **Console** - 实时日志输出
- **Traces** - 分布式追踪
- **Metrics** - 性能指标

## 🔍 健康检查端点

- `/health` - 完整健康检查
- `/alive` - 存活检查

## 🎯 开发工作流

1. 启动 Aspire: `dotnet run --project WorkflowDesigner.AppHost`
2. 打开浏览器访问 http://localhost:5173
3. 在 Dashboard 查看日志: https://localhost:17000
4. 修改代码会自动热重载

## 📚 更多文档

- [完整集成文档](ASPIRE-INTEGRATION.md)
- [项目 README](../README.md)
- [.NET Aspire 官方文档](https://learn.microsoft.com/dotnet/aspire/)

## ⚡ 特性

✅ 一键启动前后端  
✅ 自动服务发现  
✅ 统一日志和追踪  
✅ 健康检查  
✅ 热重载支持  
✅ Dashboard 监控  
