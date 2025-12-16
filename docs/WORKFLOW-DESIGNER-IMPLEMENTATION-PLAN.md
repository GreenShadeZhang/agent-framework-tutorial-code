# 工作流可视化设计器实施方案

## 📋 目录

1. [项目概述](#项目概述)
2. [技术分析](#技术分析)
3. [可行性评估](#可行性评估)
4. [架构设计](#架构设计)
5. [功能模块设计](#功能模块设计)
6. [实施路线图](#实施路线图)
7. [技术栈选型](#技术栈选型)
8. [数据流设计](#数据流设计)
9. [挑战与解决方案](#挑战与解决方案)

---

## 项目概述

### 目标

基于 Microsoft Agent Framework 的 .NET 实现,创建一个**可视化工作流设计器**,支持:

✅ **智能体列表管理** - 查看、创建、配置智能体  
✅ **拖拉拽工作流设计** - 可视化连接智能体节点  
✅ **工作流持久化** - 保存为 YAML 格式  
✅ **工作流执行** - 实时运行和调试  
✅ **提示词模板渲染** - 动态参数注入

---

## 技术分析

### 🔍 AutoGen Studio 分析

#### 核心实现

**1. 前端技术栈**
```typescript
// React Flow - 工作流可视化
import { ReactFlow, Node, Edge, Background, Controls } from '@xyflow/react';

// 主要组件
- TeamBuilder: 工作流构建器
- ComponentLibrary: 组件库侧边栏
- DndContext: 拖放上下文
- CustomNode: 自定义节点类型
  - TeamNode (团队节点)
  - AgentNode (智能体节点)
  - ToolNode (工具节点)
  - ModelNode (模型节点)
```

**2. 拖拽实现**
```typescript
// 使用 @dnd-kit/core
const { isOver, setNodeRef } = useDroppable({
  id: `${nodeId}@@@agent-zone`,
  data: { accepts: ['agent'] }
});

// 拖拽逻辑
- DraggablePreset: 可拖拽组件
- DroppableZone: 放置区域
- validateDropTarget: 验证放置目标
```

**3. 节点类型系统**
```typescript
interface CustomNode {
  id: string;
  type: ComponentTypes; // 'team' | 'agent' | 'tool' | 'model'
  data: {
    component: Component<ComponentConfig>;
    type: ComponentTypes;
  };
  position: { x: number; y: number };
}
```

**4. 状态管理**
```typescript
// Zustand store
const useTeamBuilderStore = create<TeamBuilderState>((set) => ({
  nodes: [],
  edges: [],
  addNode: (node) => set((state) => ({ nodes: [...state.nodes, node] })),
  updateNode: (id, data) => { /* ... */ }
}));
```

---

### 🔍 Agent Framework DevUI 分析

#### 核心实现

**1. 工作流结构**
```typescript
// Workflow 类型定义
interface Workflow {
  id: string;
  edge_groups: EdgeGroup[];
  executors: Record<string, Executor>;
  start_executor_id: string;
  max_iterations: number;
}

interface Executor {
  id: string;
  type: string;  // 'agent' | 'executor'
  name?: string;
  description?: string;
  config?: Record<string, unknown>;
}
```

**2. 边缘类型**
```typescript
interface EdgeGroup {
  kind: 'direct' | 'fan_out' | 'fan_in' | 'switch_case' | 'multi_selection';
  edges: Edge[];
}

interface Edge {
  source_id: string;
  target_id: string;
  condition_name?: string;
}
```

**3. 可视化渲染**
```tsx
// WorkflowFlow 组件
<ReactFlow
  nodes={nodes}
  edges={edges}
  nodeTypes={{ executor: ExecutorNode }}
  edgeTypes={{ selfLoop: SelfLoopEdge }}
>
  <Background variant={BackgroundVariant.Dots} />
  <Controls />
  <MiniMap />
</ReactFlow>
```

**4. 布局算法**
```typescript
// 自定义布局算法 (替代 dagre)
function applySimpleLayout(
  nodes: Node[],
  edges: Edge[],
  direction: 'TB' | 'LR'
): Node[] {
  // BFS 分层
  // 水平/垂直布局
  // 处理扇出节点间距
}
```

---

### 🔍 .NET 工作流解析分析

#### YAML 解析

**1. DeclarativeWorkflowBuilder**
```csharp
public static class DeclarativeWorkflowBuilder
{
    // 从 YAML 文件构建工作流
    public static Workflow Build<TInput>(
        string workflowFile,
        DeclarativeWorkflowOptions options,
        Func<TInput, ChatMessage>? inputTransform = null)
    {
        using StreamReader yamlReader = File.OpenText(workflowFile);
        AdaptiveDialog workflowElement = ReadWorkflow(yamlReader);
        // ... 构建工作流
    }
}
```

**2. YAML 结构**
```yaml
# 工作流定义示例
kind: AdaptiveDialog
id: my_workflow
recognizer: ...
triggers:
  - $kind: OnBeginDialog
    actions:
      - $kind: BeginDialog
        dialog: agent1
      - $kind: BeginDialog
        dialog: agent2
```

**3. 工作流序列化**
```csharp
// ToDevUIDict - 转换为前端兼容格式
public static Dictionary<string, JsonElement> ToDevUIDict(this Workflow workflow)
{
    return new Dictionary<string, JsonElement>
    {
        ["id"] = Serialize(workflow.Id),
        ["executors"] = Serialize(ConvertExecutorsToDict(workflow)),
        ["edge_groups"] = Serialize(ConvertEdgesToEdgeGroups(workflow)),
        // ...
    };
}
```

**4. 代码生成 (Eject)**
```csharp
// 将 YAML 工作流转换为 C# 代码
public static string Eject(
    string workflowFile,
    DeclarativeWorkflowLanguage workflowLanguage,
    string? workflowNamespace = null,
    string? workflowPrefix = null)
{
    // 解析 YAML
    // 生成 C# 代码
    // 返回代码字符串
}
```

---

## 可行性评估

### ✅ 完全可行

#### 理由分析

**1. .NET 框架完备性**
- ✅ `DeclarativeWorkflowBuilder` 支持 YAML 解析
- ✅ `WorkflowBuilder` 提供流式 API
- ✅ 支持多种边缘类型 (direct, fan_out, fan_in, switch_case)
- ✅ 内置类型验证和工作流验证

**2. 前端技术成熟**
- ✅ React Flow 成熟稳定,支持自定义节点
- ✅ DevUI 已实现工作流可视化
- ✅ AutoGen Studio 提供完整的拖拽实现参考

**3. 数据格式兼容**
- ✅ Agent Framework 支持 `workflow.to_dict()` 序列化
- ✅ .NET 提供 `ToDevUIDict()` 扩展方法
- ✅ YAML 格式统一 (可双向转换)

**4. 智能体集成**
- ✅ Agent Framework 支持通过 `AzureAgentProvider` 加载智能体
- ✅ 支持智能体配置 (instructions, model, tools)
- ✅ 提示词可通过配置注入

---

### ⚠️ 需要解决的挑战

#### 1. 提示词模板渲染

**挑战**
```yaml
# YAML 中的提示词如何支持动态参数?
agents:
  - id: writer
    instructions: "你是一个{{language}}作家,擅长{{genre}}风格"
```

**解决方案**
```csharp
// 使用 Handlebars.NET 或 Scriban
public class PromptTemplateRenderer
{
    public string Render(string template, Dictionary<string, object> context)
    {
        var handlebars = Handlebars.Create();
        var compiledTemplate = handlebars.Compile(template);
        return compiledTemplate(context);
    }
}

// 在工作流执行前渲染
var renderer = new PromptTemplateRenderer();
var instructions = renderer.Render(
    agentConfig.Instructions,
    new Dictionary<string, object>
    {
        ["language"] = "中文",
        ["genre"] = "科幻"
    }
);
```

#### 2. 工作流输入参数

**挑战**: 工作流需要支持参数化输入

**解决方案**
```csharp
// 扩展 DeclarativeWorkflowOptions
public class ExtendedWorkflowOptions : DeclarativeWorkflowOptions
{
    public Dictionary<string, object> Parameters { get; set; } = new();
}

// 在 YAML 中定义参数
inputs:
  - name: user_query
    type: string
    required: true
  - name: max_iterations
    type: int
    default: 100
```

#### 3. 实时工作流编辑

**挑战**: 前端编辑后如何同步到后端

**解决方案**
```typescript
// 前端保存工作流
async function saveWorkflow(workflow: WorkflowDesign) {
  // 1. 验证工作流
  const validation = validateWorkflow(workflow);
  if (!validation.valid) {
    showErrors(validation.errors);
    return;
  }
  
  // 2. 转换为 YAML
  const yaml = convertToYAML(workflow);
  
  // 3. 发送到后端
  await api.post('/api/workflows', { yaml });
}

// 后端接收并验证
[HttpPost]
public async Task<IActionResult> CreateWorkflow([FromBody] CreateWorkflowRequest request)
{
    try
    {
        // 解析 YAML
        var workflow = DeclarativeWorkflowBuilder.Build<string>(
            new StringReader(request.Yaml),
            _options
        );
        
        // 保存到数据库
        await _db.Workflows.AddAsync(new WorkflowEntity
        {
            Name = request.Name,
            Yaml = request.Yaml,
            WorkflowDump = JsonSerializer.Serialize(workflow.ToDevUIDict())
        });
        
        await _db.SaveChangesAsync();
        return Ok();
    }
    catch (Exception ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}
```

---

## 架构设计

### 系统架构图

```
┌─────────────────────────────────────────────────────────────┐
│                        前端层 (React)                        │
├──────────────────┬──────────────────┬──────────────────────┤
│  智能体管理面板  │  工作流设计器    │   执行监控面板       │
│  - 列表展示      │  - 拖拽画布      │   - 实时日志         │
│  - 创建/编辑     │  - 节点连线      │   - 状态跟踪         │
│  - 配置管理      │  - 参数配置      │   - 结果展示         │
└──────────────────┴──────────────────┴──────────────────────┘
                            ↕ REST API
┌─────────────────────────────────────────────────────────────┐
│                      API 层 (.NET 8)                         │
├──────────────────┬──────────────────┬──────────────────────┤
│  AgentController │ WorkflowController│ ExecutionController │
│  - CRUD 操作     │  - 工作流CRUD     │  - 启动执行          │
│  - 配置管理      │  - YAML转换       │  - 流式输出          │
│  - 验证          │  - 验证           │  - 状态管理          │
└──────────────────┴──────────────────┴──────────────────────┘
                            ↕
┌─────────────────────────────────────────────────────────────┐
│                    业务逻辑层                                │
├──────────────────┬──────────────────┬──────────────────────┤
│  AgentService    │ WorkflowService   │ ExecutionService    │
│  - 智能体管理    │  - 工作流构建     │  - 工作流执行        │
│  - 提示词渲染    │  - YAML序列化     │  - 事件处理          │
│  - 配置验证      │  - 参数注入       │  - 结果收集          │
└──────────────────┴──────────────────┴──────────────────────┘
                            ↕
┌─────────────────────────────────────────────────────────────┐
│              Agent Framework 集成层                          │
├──────────────────┬──────────────────┬──────────────────────┤
│ WorkflowBuilder  │ AgentProvider     │ DeclarativeBuilder  │
│ - 流式构建API    │  - 智能体加载     │  - YAML解析          │
│ - 边缘定义       │  - 配置管理       │  - 代码生成          │
└──────────────────┴──────────────────┴──────────────────────┘
                            ↕
┌─────────────────────────────────────────────────────────────┐
│                     数据层 (LiteDB/SQL)                      │
├──────────────────┬──────────────────┬──────────────────────┤
│  Agents          │  Workflows        │  ExecutionLogs      │
│  - 智能体定义    │  - 工作流定义     │  - 执行记录          │
│  - 提示词模板    │  - YAML内容       │  - 事件日志          │
└──────────────────┴──────────────────┴──────────────────────┘
```

---

## 功能模块设计

### 1. 智能体管理模块

#### 数据模型

```csharp
public class AgentDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InstructionsTemplate { get; set; } = string.Empty;
    public AgentType Type { get; set; } = AgentType.Assistant;
    public ModelConfig ModelConfig { get; set; } = new();
    public List<ToolConfig> Tools { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum AgentType
{
    Assistant,
    WebSurfer,
    Coder,
    Custom
}

public class ModelConfig
{
    public string Model { get; set; } = "gpt-4";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2000;
}
```

#### API 端点

```csharp
[ApiController]
[Route("api/agents")]
public class AgentController : ControllerBase
{
    // 获取所有智能体
    [HttpGet]
    public async Task<IActionResult> GetAgents()
    {
        var agents = await _agentService.GetAllAgentsAsync();
        return Ok(agents);
    }
    
    // 创建智能体
    [HttpPost]
    public async Task<IActionResult> CreateAgent([FromBody] CreateAgentRequest request)
    {
        var agent = await _agentService.CreateAgentAsync(request);
        return CreatedAtAction(nameof(GetAgent), new { id = agent.Id }, agent);
    }
    
    // 渲染提示词模板
    [HttpPost("{id}/render-prompt")]
    public async Task<IActionResult> RenderPrompt(
        string id,
        [FromBody] Dictionary<string, object> parameters)
    {
        var rendered = await _agentService.RenderPromptAsync(id, parameters);
        return Ok(new { prompt = rendered });
    }
}
```

---

### 2. 工作流设计器模块

#### 前端组件结构

```typescript
// WorkflowDesigner.tsx
interface WorkflowDesignerProps {
  initialWorkflow?: WorkflowDefinition;
  agents: AgentDefinition[];
  onSave: (workflow: WorkflowDefinition) => Promise<void>;
}

export function WorkflowDesigner({ initialWorkflow, agents, onSave }: WorkflowDesignerProps) {
  const [nodes, setNodes] = useState<Node[]>([]);
  const [edges, setEdges] = useState<Edge[]>([]);
  
  return (
    <div className="flex h-screen">
      {/* 左侧: 组件库 */}
      <ComponentPalette agents={agents} />
      
      {/* 中间: 画布 */}
      <DndProvider>
        <ReactFlow
          nodes={nodes}
          edges={edges}
          nodeTypes={customNodeTypes}
          onNodesChange={onNodesChange}
          onEdgesChange={onEdgesChange}
          onConnect={onConnect}
        >
          <Background />
          <Controls />
          <MiniMap />
        </ReactFlow>
      </DndProvider>
      
      {/* 右侧: 属性面板 */}
      <PropertyPanel selectedNode={selectedNode} />
    </div>
  );
}
```

#### 节点类型定义

```typescript
// 智能体节点
interface AgentNode extends Node {
  type: 'agent';
  data: {
    agentId: string;
    agentName: string;
    parameters: Record<string, unknown>;
    instructions: string;
  };
}

// 条件节点
interface ConditionNode extends Node {
  type: 'condition';
  data: {
    condition: string;
    branches: {
      true: string;  // 目标节点ID
      false: string; // 目标节点ID
    };
  };
}

// 开始/结束节点
interface StartEndNode extends Node {
  type: 'start' | 'end';
  data: {
    parameters?: Record<string, ParameterDefinition>;
  };
}
```

#### 工作流定义

```typescript
interface WorkflowDefinition {
  id: string;
  name: string;
  description: string;
  version: string;
  nodes: WorkflowNode[];
  edges: WorkflowEdge[];
  parameters: ParameterDefinition[];
  metadata: Record<string, unknown>;
}

interface WorkflowNode {
  id: string;
  type: 'agent' | 'condition' | 'start' | 'end';
  position: { x: number; y: number };
  data: Record<string, unknown>;
}

interface WorkflowEdge {
  id: string;
  source: string;
  target: string;
  condition?: string;
  type: 'direct' | 'conditional';
}
```

---

### 3. YAML 序列化模块

#### 工作流转 YAML

```csharp
public class WorkflowYamlConverter
{
    public string ConvertToYaml(WorkflowDefinition workflow)
    {
        var yamlBuilder = new StringBuilder();
        
        // 头部信息
        yamlBuilder.AppendLine($"$schema: https://schemas.botframework.com/schemas/component/v1.0/component.schema");
        yamlBuilder.AppendLine($"kind: AdaptiveDialog");
        yamlBuilder.AppendLine($"id: {workflow.Id}");
        yamlBuilder.AppendLine();
        
        // 生成 recognizer (可选)
        yamlBuilder.AppendLine("recognizer:");
        yamlBuilder.AppendLine("  $kind: Microsoft.RegexRecognizer");
        yamlBuilder.AppendLine("  intents: []");
        yamlBuilder.AppendLine();
        
        // 生成 triggers
        yamlBuilder.AppendLine("triggers:");
        yamlBuilder.AppendLine("  - $kind: Microsoft.OnBeginDialog");
        yamlBuilder.AppendLine("    actions:");
        
        // 转换节点为 actions
        var startNode = workflow.Nodes.First(n => n.Type == "start");
        GenerateActionsRecursive(yamlBuilder, startNode, workflow, indent: 6);
        
        return yamlBuilder.ToString();
    }
    
    private void GenerateActionsRecursive(
        StringBuilder builder,
        WorkflowNode node,
        WorkflowDefinition workflow,
        int indent)
    {
        var indentStr = new string(' ', indent);
        
        // 根据节点类型生成不同的 action
        switch (node.Type)
        {
            case "agent":
                var agentData = JsonSerializer.Deserialize<AgentNodeData>(
                    JsonSerializer.Serialize(node.Data));
                
                builder.AppendLine($"{indentStr}- $kind: Microsoft.BeginDialog");
                builder.AppendLine($"{indentStr}  dialog: {agentData.AgentId}");
                
                // 添加参数
                if (agentData.Parameters?.Count > 0)
                {
                    builder.AppendLine($"{indentStr}  options:");
                    foreach (var param in agentData.Parameters)
                    {
                        builder.AppendLine($"{indentStr}    {param.Key}: {param.Value}");
                    }
                }
                break;
                
            case "condition":
                var conditionData = JsonSerializer.Deserialize<ConditionNodeData>(
                    JsonSerializer.Serialize(node.Data));
                
                builder.AppendLine($"{indentStr}- $kind: Microsoft.IfCondition");
                builder.AppendLine($"{indentStr}  condition: {conditionData.Condition}");
                builder.AppendLine($"{indentStr}  actions:");
                
                // 递归处理 true 分支
                var trueNode = workflow.Nodes.First(n => n.Id == conditionData.Branches.True);
                GenerateActionsRecursive(builder, trueNode, workflow, indent + 4);
                
                builder.AppendLine($"{indentStr}  elseActions:");
                
                // 递归处理 false 分支
                var falseNode = workflow.Nodes.First(n => n.Id == conditionData.Branches.False);
                GenerateActionsRecursive(builder, falseNode, workflow, indent + 4);
                break;
        }
        
        // 查找下一个节点
        var nextEdge = workflow.Edges.FirstOrDefault(e => e.Source == node.Id);
        if (nextEdge != null)
        {
            var nextNode = workflow.Nodes.First(n => n.Id == nextEdge.Target);
            if (nextNode.Type != "end")
            {
                GenerateActionsRecursive(builder, nextNode, workflow, indent);
            }
        }
    }
}
```

#### YAML 转工作流

```csharp
public class YamlWorkflowConverter
{
    public WorkflowDefinition ConvertFromYaml(string yaml)
    {
        // 使用 DeclarativeWorkflowBuilder 解析
        var options = new DeclarativeWorkflowOptions(_agentProvider);
        var workflow = DeclarativeWorkflowBuilder.Build<string>(
            new StringReader(yaml),
            options
        );
        
        // 转换为 WorkflowDefinition
        var workflowDict = workflow.ToDevUIDict();
        
        return new WorkflowDefinition
        {
            Id = workflowDict["id"].GetString(),
            Nodes = ExtractNodes(workflowDict),
            Edges = ExtractEdges(workflowDict),
            // ...
        };
    }
}
```

---

### 4. 工作流执行模块

#### 执行服务

```csharp
public class WorkflowExecutionService
{
    private readonly IAgentProvider _agentProvider;
    private readonly IPromptTemplateRenderer _templateRenderer;
    private readonly ILogger<WorkflowExecutionService> _logger;
    
    public async IAsyncEnumerable<WorkflowEvent> ExecuteWorkflowAsync(
        string workflowId,
        Dictionary<string, object> inputs,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 1. 加载工作流定义
        var workflowEntity = await _db.Workflows.FindAsync(workflowId);
        if (workflowEntity == null)
            throw new NotFoundException($"Workflow {workflowId} not found");
        
        // 2. 渲染提示词模板
        var renderedWorkflow = await RenderWorkflowTemplatesAsync(
            workflowEntity.Yaml,
            inputs
        );
        
        // 3. 构建工作流
        var options = new DeclarativeWorkflowOptions(_agentProvider)
        {
            Configuration = BuildConfiguration(inputs)
        };
        
        var workflow = DeclarativeWorkflowBuilder.Build<string>(
            new StringReader(renderedWorkflow),
            options
        );
        
        // 4. 执行工作流 (流式)
        var executionId = Guid.NewGuid().ToString();
        yield return new WorkflowStartedEvent
        {
            ExecutionId = executionId,
            WorkflowId = workflowId,
            Timestamp = DateTime.UtcNow
        };
        
        await foreach (var evnt in workflow.RunStreamingAsync(
            inputs["query"]?.ToString() ?? "",
            cancellationToken))
        {
            // 转换并返回事件
            yield return ConvertToWorkflowEvent(evnt, executionId);
            
            // 持久化事件
            await _db.ExecutionLogs.AddAsync(new ExecutionLogEntity
            {
                ExecutionId = executionId,
                EventType = evnt.GetType().Name,
                EventData = JsonSerializer.Serialize(evnt),
                Timestamp = DateTime.UtcNow
            });
        }
        
        yield return new WorkflowCompletedEvent
        {
            ExecutionId = executionId,
            Timestamp = DateTime.UtcNow
        };
    }
    
    private async Task<string> RenderWorkflowTemplatesAsync(
        string yaml,
        Dictionary<string, object> inputs)
    {
        // 使用 Scriban 渲染模板
        var template = Template.Parse(yaml);
        return await template.RenderAsync(inputs);
    }
}
```

#### 实时流式输出

```csharp
[HttpPost("workflows/{id}/execute")]
public async Task ExecuteWorkflow(
    string id,
    [FromBody] ExecuteWorkflowRequest request,
    CancellationToken cancellationToken)
{
    Response.ContentType = "text/event-stream";
    
    await foreach (var evnt in _executionService.ExecuteWorkflowAsync(
        id,
        request.Inputs,
        cancellationToken))
    {
        var json = JsonSerializer.Serialize(evnt);
        await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
```

---

## 实施路线图

### Phase 1: 基础设施 (2周)

**后端**
- [x] 创建 .NET 8 Web API 项目
- [x] 集成 Agent Framework NuGet 包
- [x] 配置 LiteDB 数据库
- [x] 实现基础 CRUD API

**前端**
- [x] 创建 React + TypeScript 项目
- [x] 集成 React Flow
- [x] 实现基础布局
- [x] 配置 API 客户端

**时间**: 第 1-2 周

---

### Phase 2: 智能体管理 (1周)

**功能**
- [x] 智能体列表展示
- [x] 创建/编辑智能体表单
- [x] 提示词模板编辑器
- [x] 参数配置界面

**API**
```
GET    /api/agents
POST   /api/agents
GET    /api/agents/{id}
PUT    /api/agents/{id}
DELETE /api/agents/{id}
POST   /api/agents/{id}/render-prompt
```

**时间**: 第 3 周

---

### Phase 3: 工作流设计器 (2周)

**功能**
- [x] 拖拽组件库
- [x] 画布节点渲染
- [x] 节点连线功能
- [x] 属性配置面板
- [x] 工作流验证

**组件**
```typescript
- ComponentPalette
  - AgentItem (可拖拽)
- WorkflowCanvas
  - CustomNodes (Agent, Condition, Start, End)
- PropertyPanel
  - NodeProperties
  - EdgeProperties
```

**时间**: 第 4-5 周

---

### Phase 4: YAML 序列化 (1周)

**功能**
- [x] 工作流转 YAML
- [x] YAML 转工作流
- [x] 验证 YAML 语法
- [x] 保存/加载工作流

**API**
```
POST   /api/workflows
GET    /api/workflows/{id}
PUT    /api/workflows/{id}
POST   /api/workflows/{id}/yaml
POST   /api/workflows/from-yaml
```

**时间**: 第 6 周

---

### Phase 5: 提示词模板渲染 (1周)

**功能**
- [x] 集成 Scriban 模板引擎
- [x] 参数定义界面
- [x] 模板预览功能
- [x] 运行时参数注入

**示例**
```yaml
agents:
  - id: writer
    instructions: |
      你是一个{{language}}作家。
      擅长{{genre}}风格的创作。
      当前任务: {{task}}
```

**时间**: 第 7 周

---

### Phase 6: 工作流执行 (2周)

**功能**
- [x] 工作流执行服务
- [x] 实时日志输出
- [x] 状态可视化
- [x] 错误处理

**前端**
```typescript
<WorkflowExecutionPanel
  workflowId={id}
  onEvent={(event) => {
    // 更新节点状态
    updateNodeState(event.executorId, event.status);
  }}
/>
```

**时间**: 第 8-9 周

---

### Phase 7: 测试与优化 (1周)

**任务**
- [x] 单元测试
- [x] 集成测试
- [x] 性能优化
- [x] UI/UX 优化

**时间**: 第 10 周

---

## 技术栈选型

### 后端

| 技术 | 版本 | 用途 |
|------|------|------|
| .NET | 8.0 | Web API 框架 |
| Agent Framework | Latest | 工作流引擎 |
| LiteDB | 5.0 | 嵌入式数据库 |
| Scriban | 5.7 | 模板引擎 |
| Swashbuckle | 6.5 | API 文档 |

### 前端

| 技术 | 版本 | 用途 |
|------|------|------|
| React | 18 | UI 框架 |
| TypeScript | 5.0 | 类型安全 |
| React Flow | 11.11 | 工作流可视化 |
| TailwindCSS | 3.4 | 样式框架 |
| Zustand | 4.5 | 状态管理 |
| @dnd-kit/core | 6.1 | 拖拽功能 |

---

## 数据流设计

### 工作流设计流程

```
用户操作
  ↓
[拖拽智能体到画布]
  ↓
前端: 创建节点对象
  {
    id: 'agent-1',
    type: 'agent',
    data: { agentId: 'writer-001' }
  }
  ↓
[连接节点]
  ↓
前端: 创建边对象
  {
    id: 'edge-1',
    source: 'agent-1',
    target: 'agent-2'
  }
  ↓
[保存工作流]
  ↓
前端: 转换为 WorkflowDefinition
  ↓
API: POST /api/workflows
  {
    name: '内容创作流程',
    nodes: [...],
    edges: [...]
  }
  ↓
后端: 转换为 YAML
  ↓
后端: 验证 YAML
  ↓
后端: 保存到数据库
  {
    Id: '...',
    Yaml: '...',
    WorkflowDump: {...}
  }
```

### 工作流执行流程

```
用户触发
  ↓
[点击执行按钮]
  ↓
前端: 收集输入参数
  {
    query: '写一篇科幻小说',
    language: '中文',
    genre: '科幻'
  }
  ↓
API: POST /api/workflows/{id}/execute
  ↓
后端: 加载工作流YAML
  ↓
后端: 渲染提示词模板
  instructions: "你是一个中文作家,擅长科幻风格"
  ↓
后端: 构建 Workflow 对象
  ↓
后端: 执行工作流 (流式)
  ↓
后端: 发送 SSE 事件
  ← data: {"type":"executor_started","id":"agent-1"}
  ← data: {"type":"message","content":"..."}
  ← data: {"type":"executor_completed","id":"agent-1"}
  ↓
前端: 更新节点状态
  ↓
前端: 显示实时输出
```

---

## 挑战与解决方案

### 挑战 1: 复杂工作流的可视化

**问题**: 当工作流包含大量节点时,布局会变得混乱

**解决方案**
```typescript
// 使用分层布局算法
function applyLayeredLayout(nodes: Node[], edges: Edge[]): Node[] {
  // 1. 拓扑排序
  const sorted = topologicalSort(nodes, edges);
  
  // 2. 分层
  const layers = assignLayers(sorted, edges);
  
  // 3. 减少交叉
  minimizeCrossings(layers);
  
  // 4. 分配坐标
  return assignPositions(layers);
}

// 支持缩放和迷你地图
<ReactFlow
  minZoom={0.1}
  maxZoom={2}
>
  <MiniMap />
  <Controls />
</ReactFlow>
```

---

### 挑战 2: 工作流参数传递

**问题**: 如何在节点之间传递数据?

**解决方案**
```yaml
# 使用变量系统
triggers:
  - $kind: Microsoft.OnBeginDialog
    actions:
      # 1. 设置变量
      - $kind: Microsoft.SetProperty
        property: dialog.writerOutput
        value: "=turn.activity.text"
      
      # 2. 调用智能体
      - $kind: Microsoft.BeginDialog
        dialog: writer
        options:
          input: "=dialog.writerOutput"
      
      # 3. 使用上一步输出
      - $kind: Microsoft.BeginDialog
        dialog: reviewer
        options:
          input: "=turn.lastResult"
```

---

### 挑战 3: 实时状态同步

**问题**: 如何实时更新前端节点状态?

**解决方案**
```typescript
// 使用 Server-Sent Events
const eventSource = new EventSource(`/api/workflows/${id}/execute`);

eventSource.onmessage = (event) => {
  const data = JSON.parse(event.data);
  
  switch (data.type) {
    case 'executor_started':
      updateNodeState(data.id, 'running');
      break;
    case 'executor_completed':
      updateNodeState(data.id, 'completed', data.output);
      break;
    case 'executor_failed':
      updateNodeState(data.id, 'failed', data.error);
      break;
  }
};
```

---

### 挑战 4: 工作流版本管理

**问题**: 如何管理工作流的多个版本?

**解决方案**
```csharp
public class WorkflowEntity
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Version { get; set; }  // 版本号
    public string Yaml { get; set; }
    public bool IsPublished { get; set; }  // 是否已发布
    public string? ParentId { get; set; }  // 父版本ID
    public DateTime CreatedAt { get; set; }
}

// API 支持版本查询
[HttpGet("workflows/{id}/versions")]
public async Task<IActionResult> GetVersions(string id)
{
    var versions = await _db.Workflows
        .Where(w => w.Id == id || w.ParentId == id)
        .OrderByDescending(w => w.Version)
        .ToListAsync();
    return Ok(versions);
}
```

---

## 总结

### ✅ 可行性结论

该项目**完全可行**,理由如下:

1. **技术栈成熟**: React Flow + .NET Agent Framework 都是经过验证的技术
2. **参考实现完整**: AutoGen Studio 和 DevUI 提供了完整的参考
3. **框架支持良好**: Agent Framework 的 YAML 解析和工作流构建 API 完善
4. **社区活跃**: Microsoft 官方维护,文档齐全

### 📊 预估工作量

- **总开发时间**: 10 周
- **团队规模**: 2-3 人 (1 后端 + 1 前端 + 1 测试)
- **技术难度**: 中等
- **风险等级**: 低

### 🎯 关键成功因素

1. **提示词模板系统**: 灵活的参数注入是核心
2. **实时状态同步**: SSE 确保良好的用户体验
3. **工作流验证**: 在保存前进行完整性检查
4. **错误处理**: 详细的错误信息和回滚机制

### 🚀 下一步行动

1. ✅ 创建项目结构
2. ✅ 搭建开发环境
3. ✅ 实现 Phase 1 (基础设施)
4. ⏳ 开始 Phase 2 (智能体管理)

---

## 附录

### A. 示例工作流 YAML

```yaml
$schema: https://schemas.botframework.com/schemas/component/v1.0/component.schema
kind: AdaptiveDialog
id: content_creation_workflow
description: 内容创作工作流

recognizer:
  $kind: Microsoft.RegexRecognizer
  intents: []

triggers:
  - $kind: Microsoft.OnBeginDialog
    actions:
      # 1. 作家生成初稿
      - $kind: Microsoft.BeginDialog
        dialog: writer
        options:
          instructions: |
            你是一个{{language}}作家,擅长{{genre}}风格。
            请根据以下主题创作: {{topic}}
          
      # 2. 编辑审核
      - $kind: Microsoft.BeginDialog
        dialog: editor
        options:
          input: "=turn.lastResult"
          instructions: |
            请审核以下内容并提供修改建议:
            {{input}}
      
      # 3. 条件判断
      - $kind: Microsoft.IfCondition
        condition: "=turn.lastResult.score >= 80"
        actions:
          # 通过审核,直接发布
          - $kind: Microsoft.BeginDialog
            dialog: publisher
        elseActions:
          # 未通过,返回修改
          - $kind: Microsoft.BeginDialog
            dialog: writer
            options:
              instructions: |
                请根据以下反馈修改内容:
                {{turn.lastResult.feedback}}
```

### B. 前端组件示例

```tsx
// AgentNode.tsx
export function AgentNode({ data }: NodeProps<AgentNodeData>) {
  const [isEditing, setIsEditing] = useState(false);
  
  return (
    <div className={cn(
      'border-2 rounded-lg p-4 bg-white shadow-lg',
      data.state === 'running' && 'border-blue-500 animate-pulse',
      data.state === 'completed' && 'border-green-500',
      data.state === 'failed' && 'border-red-500'
    )}>
      <Handle type="target" position={Position.Top} />
      
      <div className="flex items-center gap-2">
        <Bot className="w-5 h-5" />
        <span className="font-semibold">{data.agentName}</span>
      </div>
      
      {data.state === 'running' && (
        <Loader2 className="w-4 h-4 animate-spin mt-2" />
      )}
      
      {data.output && (
        <div className="mt-2 text-sm text-gray-600">
          {truncate(data.output, 100)}
        </div>
      )}
      
      <Handle type="source" position={Position.Bottom} />
    </div>
  );
}
```

### C. 参考资源

- [Agent Framework GitHub](https://github.com/microsoft/agent-framework)
- [AutoGen GitHub](https://github.com/microsoft/autogen)
- [React Flow Docs](https://reactflow.dev)
- [Scriban Documentation](https://github.com/scriban/scriban)

---

**文档版本**: 1.0  
**创建日期**: 2025-01-15  
**最后更新**: 2025-01-15
