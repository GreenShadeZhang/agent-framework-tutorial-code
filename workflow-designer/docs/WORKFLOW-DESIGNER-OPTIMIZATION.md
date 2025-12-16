# 工作流设计器优化总结

## 概述

基于对以下三个参考实现的深入分析，对当前工作流设计器进行了全面优化：

1. **AutoGen Studio** - 组件化团队构建器
2. **Agent Framework DevUI** - 执行器工作流可视化
3. **.NET Declarative Workflows** - AdaptiveDialog YAML 解析

## 优化内容

### 1. 后端模型优化

#### 1.1 DeclarativeWorkflow.cs

新增了与 Agent Framework 对齐的完整工作流模型：

**执行器类型 (30+ 种)**

| 分类 | 执行器类型 |
|------|-----------|
| 智能体 | ChatAgent, FunctionAgent, ToolAgent, MagenticOrchestrator, AzureAgent |
| 流程控制 | Condition, ConditionGroup, Foreach, Goto, BreakLoop, ContinueLoop, EndWorkflow, EndConversation |
| 状态管理 | SetVariable, SetMultipleVariables, ParseValue, EditTable, ResetVariable, ClearAllVariables |
| 消息 | SendActivity, AddConversationMessage, RetrieveConversationMessages |
| 会话 | CreateConversation, DeleteConversation, CopyConversationMessages |
| 人工输入 | Question, FunctionApproval |
| 工具 | FunctionExecutor, McpTool, OpenApiTool, CodeInterpreter, FileSearch, WebSearch |
| 工作流 | SubWorkflow, ParallelExecution, FanOut, FanIn |

**边组类型**

- `Single` - 单一连接
- `FanOut` - 扇出（并行分发）
- `FanIn` - 扇入（并行合并）
- `SwitchCase` - 条件分支

**配置模型**

```csharp
// 智能体配置
public class AgentExecutorConfig
{
    public string Name { get; set; }
    public string InstructionsTemplate { get; set; }
    public ModelConfiguration ModelConfig { get; set; }
    public List<ToolReference> Tools { get; set; }
    public List<WorkbenchConfig> Workbenches { get; set; }
    public List<HandoffConfig> Handoffs { get; set; }
    public List<VariableMapping> InputMappings { get; set; }
    public List<VariableMapping> OutputMappings { get; set; }
}

// 条件配置
public class ConditionConfig
{
    public string Expression { get; set; }
    public string? TrueBranchTarget { get; set; }
    public string? FalseBranchTarget { get; set; }
}

// 循环配置
public class ForeachConfig
{
    public string ItemsExpression { get; set; }
    public string ItemVariableName { get; set; }
    public string IndexVariableName { get; set; }
}
```

#### 1.2 YamlConversionService.cs

实现了双向 YAML 转换服务：

```csharp
// 工作流 → AdaptiveDialog YAML
public string ConvertToYaml(DeclarativeWorkflowDefinition workflow)

// AdaptiveDialog YAML → 工作流
public DeclarativeWorkflowDefinition ParseFromYaml(string yaml)
```

**支持的 AdaptiveDialog Action 类型映射**

| 执行器类型 | AdaptiveDialog $kind |
|-----------|---------------------|
| ChatAgent | Microsoft.Agents.ChatAgent |
| Condition | Microsoft.Agents.IfCondition |
| Foreach | Microsoft.Agents.Foreach |
| SetVariable | Microsoft.Agents.SetVariable |
| SendActivity | Microsoft.Agents.SendActivity |
| Question | Microsoft.Agents.TextInput |
| EndConversation | Microsoft.Agents.EndTurn |

#### 1.3 DeclarativeWorkflowsController.cs

新增 API 端点：

| 方法 | 路由 | 功能 |
|------|------|------|
| POST | /api/declarative-workflows/export-yaml | 导出 YAML |
| POST | /api/declarative-workflows/import-yaml | 导入 YAML |
| POST | /api/declarative-workflows/validate | 验证工作流 |
| POST | /api/declarative-workflows/preview-yaml | 预览 YAML |
| GET | /api/declarative-workflows/executor-types | 获取执行器类型 |
| GET | /api/declarative-workflows/executor-schema/{type} | 获取配置 Schema |

### 2. 前端优化

#### 2.1 类型定义 (workflow.ts)

完整的 TypeScript 类型定义：

```typescript
// 执行器类型分组
export const ExecutorTypeGroups = {
  agents: [...],      // 智能体
  controlFlow: [...], // 流程控制
  stateManagement: [...], // 状态管理
  messages: [...],    // 消息
  conversation: [...], // 会话
  humanInput: [...],  // 人工输入
  tools: [...],       // 工具
  workflow: [...],    // 工作流
}

// 工具函数
export function getExecutorIcon(type: ExecutorType): string
export function getExecutorLabel(type: ExecutorType): string
export function createDefaultExecutorConfig(type: ExecutorType): ExecutorConfig
export function createExecutorDefinition(type: ExecutorType, position: Position): ExecutorDefinition
```

#### 2.2 状态管理 (workflowStore.ts)

基于 Zustand 的状态管理（参考 AutoGen Studio 模式）：

```typescript
// 核心状态
interface WorkflowState {
  workflow: DeclarativeWorkflowDefinition | null;
  selectedExecutorId: string | null;
  history: HistoryEntry[];      // 历史记录（撤销/重做）
  executorStates: Record<string, ExecutorState>; // 执行状态
  isDirty: boolean;             // 修改标记
}

// 核心操作
interface WorkflowActions {
  // 执行器管理
  addExecutor(type, position): string;
  updateExecutor(id, updates): void;
  deleteExecutor(id): void;
  duplicateExecutor(id): string | null;
  
  // 边管理
  addEdge(sourceId, targetId, condition?, label?): string | null;
  deleteEdge(edgeId): void;
  
  // 历史管理
  undo(): void;
  redo(): void;
  
  // 导入导出
  exportToJson(): string;
  exportToYaml(): Promise<string>;
  validateWorkflow(): ValidationResult;
}
```

#### 2.3 节点组件 (ExecutorNode.tsx)

动态渲染的执行器节点：

- **颜色编码** - 不同类型使用不同颜色
- **状态指示** - 运行中/错误状态可视化
- **Handle 配置** - 条件节点显示双输出口
- **内联预览** - 显示关键配置信息

```tsx
// 智能体节点显示模型和工具数量
<AgentNodeContent config={config}>
  🧠 gpt-4o
  🔧 3 工具
  🔀 2 交接
</AgentNodeContent>

// 条件节点显示表达式
<ConditionNodeContent config={config}>
  user.intent == 'booking'
</ConditionNodeContent>
```

#### 2.4 工具箱 (ExecutorToolbox.tsx)

分类展示所有执行器类型：

- 可折叠的分类面板
- 搜索过滤功能
- 拖拽添加节点
- 图标和描述提示

#### 2.5 配置模态框 (ExecutorConfigModal.tsx)

针对不同执行器类型的配置表单：

- **智能体配置** - 模型选择、指令模板、工具配置
- **条件配置** - 表达式编辑、分支目标选择
- **循环配置** - 集合表达式、变量命名
- **消息配置** - 消息类型、内容编辑

#### 2.6 画布组件 (WorkflowCanvas.tsx)

增强的工作流画布：

- 工具栏（保存、导出、验证、撤销/重做）
- 状态栏（节点数、连接数、执行状态）
- 键盘快捷键支持
- 节点拖放处理
- 边动态样式（条件分支颜色编码）

#### 2.7 设计器页面 (WorkflowDesignerPage.tsx)

完整的设计器布局：

```
┌────────────────────────────────────────────────────────┐
│ Header (工作流名称、新建、打开)                           │
├──────────┬─────────────────────────────┬───────────────┤
│          │                             │               │
│ 工具箱    │         画布                │   属性面板     │
│ (左侧)    │       (React Flow)          │   (右侧)      │
│          │                             │               │
│ - 智能体  │                             │ - 基本信息     │
│ - 流程控制│                             │ - 位置        │
│ - 状态管理│                             │ - 变量        │
│ - ...    │                             │               │
│          │                             │               │
└──────────┴─────────────────────────────┴───────────────┘
```

### 3. YAML 格式对齐

生成的 YAML 与 Agent Framework 的 AdaptiveDialog 格式完全兼容：

```yaml
$schema: https://raw.githubusercontent.com/microsoft/Agents/main/schemas/workflow.json
$kind: Microsoft.Agents.AdaptiveDialog
id: my-workflow
triggers:
  - $kind: Microsoft.Agents.OnUnknownIntent
    actions:
      - $kind: Microsoft.Agents.ChatAgent
        id: greeter-agent
        name: 欢迎智能体
        instructions: 你是一个友好的助手...
        model:
          provider: OpenAI
          model: gpt-4o
          temperature: 0.7
        
      - $kind: Microsoft.Agents.IfCondition
        condition: =user.needsHelp
        actions:
          - $kind: Microsoft.Agents.ChatAgent
            id: helper-agent
            name: 帮助智能体
        elseActions:
          - $kind: Microsoft.Agents.EndTurn
```

## 使用指南

### 启动项目

```bash
# 后端
cd WorkflowDesigner.Api
dotnet run

# 前端
cd frontend
npm install
npm run dev
```

### 创建工作流

1. 点击「新建」创建空白工作流
2. 从左侧工具箱拖拽节点到画布
3. 连接节点建立流程
4. 双击节点编辑配置
5. 点击「保存」或导出 YAML

### API 使用

```bash
# 导出 YAML
curl -X POST http://localhost:5000/api/declarative-workflows/export-yaml \
  -H "Content-Type: application/json" \
  -d @workflow.json

# 验证工作流
curl -X POST http://localhost:5000/api/declarative-workflows/validate \
  -H "Content-Type: application/json" \
  -d @workflow.json
```

## 参考资源

- [AutoGen Studio 源码](https://github.com/microsoft/autogen/tree/main/python/packages/autogen-studio)
- [Agent Framework DevUI](https://github.com/microsoft/agent-framework/tree/main/python/packages/devui)
- [.NET Declarative Workflows](https://github.com/microsoft/agent-framework/tree/main/dotnet/src/Microsoft.Agents.AI.Workflows.Declarative)

## 文件清单

### 后端新增/修改

| 文件 | 说明 |
|------|------|
| Models/DeclarativeWorkflow.cs | 声明式工作流模型 |
| Services/YamlConversionService.cs | YAML 转换服务 |
| Controllers/DeclarativeWorkflowsController.cs | API 控制器 |
| Program.cs | 服务注册 |

### 前端新增/修改

| 文件 | 说明 |
|------|------|
| types/workflow.ts | TypeScript 类型定义 |
| store/workflowStore.ts | Zustand 状态管理 |
| components/workflow/nodes/ExecutorNode.tsx | 执行器节点组件 |
| components/workflow/toolbox/ExecutorToolbox.tsx | 工具箱组件 |
| components/workflow/modals/ExecutorConfigModal.tsx | 配置模态框 |
| components/workflow/WorkflowCanvas.tsx | 画布组件 |
| components/common/SchemaFormRenderer.tsx | Schema 表单渲染器 |
| pages/WorkflowDesignerPage.tsx | 设计器页面 |
