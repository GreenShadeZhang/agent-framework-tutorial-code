# Aspire 集成完成

## ✅ 已完成的工作

### 1. 项目结构
```
workflow-designer/
├── WorkflowDesigner.AppHost/          # Aspire 编排主机
├── WorkflowDesigner.ServiceDefaults/  # 共享服务配置
├── WorkflowDesigner.Api/              # 后端 API (已集成 ServiceDefaults)
├── frontend/                          # 前端 (已配置 Vite 代理)
└── WorkflowDesigner.sln               # 统一解决方案
```

### 2. 添加的功能

#### ServiceDefaults 项目
- ✅ OpenTelemetry 遥测 (日志、指标、追踪)
- ✅ 健康检查端点 (/health, /alive)
- ✅ 服务发现支持
- ✅ HTTP 弹性处理

#### AppHost 项目
- ✅ 自动服务编排
- ✅ 统一的启动入口
- ✅ Aspire Dashboard 集成
- ✅ 前后端服务发现

#### API 项目更新
- ✅ 引用 ServiceDefaults
- ✅ 调用 `AddServiceDefaults()`
- ✅ 映射健康检查端点

#### 前端项目更新
- ✅ Vite 配置支持服务发现
- ✅ API 代理配置
- ✅ 环境变量支持

### 3. 端口分配

| 服务 | 端口 | 访问地址 |
|-----|------|---------|
| Aspire Dashboard | 17000 (HTTPS) | https://localhost:17000 |
| API (HTTPS) | 5001 | https://localhost:5001 |
| API (HTTP) | 5000 | http://localhost:5000 |
| Frontend | 5173 | http://localhost:5173 |

## 🚀 使用方法

### 启动所有服务 (推荐)
```bash
cd workflow-designer
dotnet run --project WorkflowDesigner.AppHost
```

访问：
- **应用界面**: http://localhost:5173
- **Aspire Dashboard**: https://localhost:17000
- **API Swagger**: http://localhost:5000/swagger

### 单独启动 (调试模式)

后端：
```bash
cd WorkflowDesigner.Api
dotnet run
```

前端：
```bash
cd frontend
npm install
npm run dev
```

## 📊 Aspire Dashboard 功能

访问 https://localhost:17000 可以：

1. **查看所有服务**
   - 服务状态和健康检查
   - 端点和环境变量
   - 资源利用情况

2. **实时日志**
   - 统一的日志视图
   - 按服务过滤
   - 搜索和导出

3. **分布式追踪**
   - API 调用链路
   - 性能瓶颈分析
   - 错误追踪

4. **指标监控**
   - HTTP 请求指标
   - 运行时指标
   - 自定义指标

## 🔧 配置说明

### OpenAI API Key 配置

```bash
cd WorkflowDesigner.Api
dotnet user-secrets set "OpenAI:ApiKey" "your-api-key"
dotnet user-secrets set "OpenAI:Model" "gpt-4o-mini"
```

### 健康检查端点

- `/health` - 完整健康检查
- `/alive` - 存活检查 (用于探活)

## 🎯 下一步

1. **添加数据库集成**
   ```csharp
   // 在 AppHost 中添加
   var db = builder.AddPostgres("postgres")
       .AddDatabase("workflowdb");
   
   var api = builder.AddProject("workflowdesigner-api", apiProjectPath)
       .WithReference(db);
   ```

2. **添加缓存**
   ```csharp
   var cache = builder.AddRedis("cache");
   var api = builder.AddProject("workflowdesigner-api", apiProjectPath)
       .WithReference(cache);
   ```

3. **添加消息队列**
   ```csharp
   var queue = builder.AddRabbitMQ("messaging");
   var api = builder.AddProject("workflowdesigner-api", apiProjectPath)
       .WithReference(queue);
   ```

## 📚 参考资料

- [.NET Aspire 文档](https://learn.microsoft.com/dotnet/aspire/)
- [Aspire Dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard)
- [服务发现](https://learn.microsoft.com/dotnet/aspire/service-discovery/overview)
- [OpenTelemetry](https://learn.microsoft.com/dotnet/aspire/fundamentals/telemetry)

## ⚠️ 注意事项

1. **端口冲突**: 确保 5000、5001、5173、17000 端口未被占用
2. **Node.js**: 前端需要 Node.js 18+ 和 npm
3. **证书信任**: 首次运行可能需要信任开发证书
   ```bash
   dotnet dev-certs https --trust
   ```

## 🐛 故障排查

### 问题：端口已被占用
```bash
# 查找占用端口的进程
netstat -ano | findstr :5000
# 结束进程
taskkill /PID <进程ID> /F
```

### 问题：前端无法连接 API
检查 [vite.config.ts](../frontend/vite.config.ts) 中的代理配置

### 问题：健康检查失败
访问 http://localhost:5000/health 查看详细状态

## ✨ 改进点

相比传统启动方式的优势：

1. **一键启动** - 无需分别启动前后端
2. **统一监控** - Dashboard 查看所有服务
3. **自动发现** - 前端自动发现 API 地址
4. **可观测性** - 内置日志、追踪、指标
5. **开发体验** - 热重载、实时日志
6. **生产就绪** - 健康检查、弹性处理

## 🎉 总结

Aspire 集成完成！现在可以：
- ✅ 使用 `dotnet run --project WorkflowDesigner.AppHost` 一键启动
- ✅ 在 Dashboard 中监控所有服务
- ✅ 享受现代化的微服务开发体验
