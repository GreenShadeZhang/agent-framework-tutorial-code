# 工作流可视化设计器 (Workflow Visual Designer)

基于 Microsoft Agent Framework 和 .NET Aspire 的可视化工作流设计器，支持拖拉拽设计、智能体管理、YAML持久化和实时执行。

## 🌟 功能特性

### 核心功能

- ✅ **智能体管理** - 创建、配置和管理智能体
- ✅ **可视化设计** - 拖拉拽方式构建工作流
- ✅ **YAML持久化** - 保存为标准YAML格式
- ✅ **实时执行** - 运行并监控工作流
- ✅ **提示词模板** - 支持动态参数注入

### 技术亮点

- 🔥 基于 Agent Framework .NET 实现
- 🎨 React Flow 驱动的可视化界面
- 📝 Scriban 模板引擎支持
- 💾 LiteDB 轻量级数据存储
- 🚀 实时流式输出 (SSE)
- ⚡ .NET Aspire 编排和可观测性

## 📁 项目结构

```
workflow-designer/
├── WorkflowDesigner.AppHost/        # Aspire 编排主机
│   ├── Program.cs                   # 服务编排配置
│   └── WorkflowDesigner.AppHost.csproj
│
├── WorkflowDesigner.ServiceDefaults/# 共享服务配置
│   ├── Extensions.cs                # OpenTelemetry、健康检查等
│   └── WorkflowDesigner.ServiceDefaults.csproj
│
├── WorkflowDesigner.Api/            # .NET 8 Web API
│   ├── Controllers/                 # API控制器
│   ├── Services/                    # 业务逻辑
│   ├── Models/                      # 数据模型
│   ├── Data/                        # 数据访问
│   ├── Utils/                       # 工具类
│   └── WorkflowDesigner.Api.csproj
│
├── frontend/                        # React + TypeScript
│   ├── src/
│   │   ├── components/              # UI组件
│   │   ├── services/                # API服务
│   │   └── stores/                  # 状态管理
│   └── vite.config.ts
│
└── WorkflowDesigner.sln             # 解决方案文件
```

## 🚀 快速开始

### 前置要求

- .NET 8.0 SDK
- Node.js 18+
- OpenAI API Key (或 Azure OpenAI)

### 使用 Aspire 启动（推荐）

```bash
cd workflow-designer

# 一键启动所有服务
dotnet run --project WorkflowDesigner.AppHost
```

这将自动启动：
- 🔧 **API 服务** - http://localhost:5000 (和 https://localhost:5001)
- 🎨 **前端界面** - http://localhost:5173
- 📊 **Aspire Dashboard** - https://localhost:17000

然后访问：
- **应用界面**: http://localhost:5173
- **Aspire Dashboard**: https://localhost:17000 (查看日志、追踪、指标)
- **API Swagger**: http://localhost:5000/swagger

### 单独启动（开发调试）

后端：
```bash
cd src/backend/WorkflowDesigner.Api

# 配置 appsettings.json

# 配置 OpenAI API Key
dotnet user-secrets set "OpenAI:ApiKey" "your-api-key"
dotnet user-secrets set "OpenAI:Model" "gpt-4o-mini"

# 运行
dotnet run
```

前端：
```bash
cd frontend

# 安装依赖
npm install

# 运行开发服务器
npm run dev
```

## 🌟 Aspire 集成特性

### 服务发现
- 前端自动发现后端 API 地址
- 无需手动配置服务端点

### 可观测性
- **日志聚合** - 统一查看所有服务日志
- **分布式追踪** - OpenTelemetry 自动追踪
- **指标监控** - 实时性能指标

### 健康检查
- API `/health` - 整体健康状态
- API `/alive` - 存活检查

### 开发体验
- 🔥 热重载支持（前端 Vite，后端 dotnet watch）
- 📊 统一的 Aspire Dashboard 监控界面
- 🚀 一键启动所有服务

## 🛠️ 配置说明

### 环境变量

后端 (User Secrets):
```bash
dotnet user-secrets set "OpenAI:ApiKey" "your-api-key"
dotnet user-secrets set "OpenAI:Model" "gpt-4o-mini"
dotnet user-secrets set "OpenAI:BaseUrl" "https://api.openai.com/v1"
```

前端 (`.env`):
```env
VITE_API_URL=/api        # API 代理路径
VITE_PORT=5173          # 前端端口
```

### 端口分配

| 服务 | 端口 | 说明 |
|-----|------|-----|
| Aspire Dashboard | 17000 (HTTPS) | 管理界面 |
| API | 5000 / 5001 | 后端服务 |
| Frontend | 5173 | 前端界面 |

## 📖 使用指南

### 1. 创建智能体

1. 访问 http://localhost:5173
2. 进入"智能体管理"面板
3. 点击"新建智能体"
4. 填写智能体信息:
   - 名称: `内容作家`
   - 类型: `Assistant`
   - 提示词模板: `你是一个{{language}}作家,擅长{{genre}}风格`
   - 模型配置: `gpt-4o-mini`

### 2. 设计工作流

1. 进入"工作流设计器"
2. 从左侧组件库拖拽智能体到画布
3. 连接节点创建工作流
4. 配置节点参数
5. 保存工作流

### 3. 执行工作流

1. 点击"运行"按钮
2. 填写输入参数
3. 实时查看执行状态和日志
4. 获取最终输出

### 4. 监控和调试

1. 打开 Aspire Dashboard (https://localhost:17000)
2. 查看服务状态和健康检查
3. 浏览实时日志
4. 追踪 API 调用链路
5. 监控性能指标

## 🏗️ 架构设计

### 系统架构

```
┌──────────────────┐
│ Aspire Dashboard │  ← 可观测性、日志、追踪
└──────────────────┘
         ↓
┌──────────────────────────────────────────┐
│         Aspire AppHost                    │
│  ┌─────────────┐      ┌────────────────┐ │
│  │  Frontend   │  →   │   API Service  │ │
│  │  (Vite)     │      │   (.NET 8)     │ │
│  └─────────────┘      └────────────────┘ │
└──────────────────────────────────────────┘
                              ↓
                       ┌────────────────┐
                       │ Agent          │
                       │ Framework      │
                       └────────────────┘
                              ↓
                       ┌────────────────┐
                       │ LiteDB         │
                       └────────────────┘
```

### 数据流

```
设计工作流 → 转换YAML → 保存数据库 → 加载执行 → 实时输出
```

## 🔧 开发指南

### 添加新的智能体类型

```csharp
// 1. 定义智能体类型
public enum AgentType
{
    Assistant,
    WebSurfer,
    Coder,
    Custom
}

// 2. 实现智能体逻辑
public class CustomAgent : AgentDefinition
{
    // 自定义实现
}
```

### 添加新的节点类型

```typescript
// 1. 定义节点数据类型
interface CustomNodeData extends NodeData {
  customProperty: string;
}

// 2. 实现节点组件
export function CustomNode({ data }: NodeProps<CustomNodeData>) {
  return (
    <div className="custom-node">
      {/* 自定义UI */}
    </div>
  );
}

// 3. 注册节点类型
const nodeTypes = {
  custom: CustomNode,
};
```

## 📊 性能优化

- ✅ 使用 React.memo 减少重渲染
- ✅ 虚拟化大型节点列表
- ✅ 延迟加载工作流定义
- ✅ SSE 流式输出减少延迟
- ✅ LiteDB 索引优化查询

## 🧪 测试

### 后端测试

```bash
cd src/backend
dotnet test
```

### 前端测试

```bash
cd src/frontend
npm test
```

## 📝 API 文档

API 文档通过 Swagger 自动生成,运行后端后访问:

```
http://localhost:5000/swagger
```

### 主要API端点

```
GET    /api/agents              # 获取智能体列表
POST   /api/agents              # 创建智能体
GET    /api/agents/{id}         # 获取智能体详情
PUT    /api/agents/{id}         # 更新智能体
DELETE /api/agents/{id}         # 删除智能体

GET    /api/workflows           # 获取工作流列表
POST   /api/workflows           # 创建工作流
GET    /api/workflows/{id}      # 获取工作流详情
PUT    /api/workflows/{id}      # 更新工作流
POST   /api/workflows/{id}/execute  # 执行工作流
```

## 🤝 贡献指南

1. Fork 本项目
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

## 📄 许可证

本项目基于 MIT 许可证开源 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 🙏 致谢

- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)
- [AutoGen](https://github.com/microsoft/autogen)
- [React Flow](https://reactflow.dev)
- [Scriban](https://github.com/scriban/scriban)

## � 相关文档
- [快速开始指南](../docs/WORKFLOW-DESIGNER-GETTING-STARTED.md)- [Aspire 集成指南](../docs/ASPIRE-INTEGRATION.md)
- [Aspire 快速开始](../docs/ASPIRE-QUICK-START.md)
- [功能特性总结](../docs/WORKFLOW-DESIGNER-FEATURE-SUMMARY.md)
- [项目实施计划](../docs/WORKFLOW-DESIGNER-IMPLEMENTATION-PLAN.md)

## �📮 联系方式

项目链接: [https://github.com/your-username/workflow-designer](https://github.com/your-username/workflow-designer)

---

**注意**: 本项目目前处于开发阶段,API可能会有变动。
