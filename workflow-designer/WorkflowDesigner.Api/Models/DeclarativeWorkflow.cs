namespace WorkflowDesigner.Api.Models;

/// <summary>
/// 声明式工作流定义 - 对齐 Agent Framework AdaptiveDialog 结构
/// </summary>
public class DeclarativeWorkflowDefinition
{
    /// <summary>
    /// 工作流ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 工作流名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 版本号
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// 入口执行器ID
    /// </summary>
    public string StartExecutorId { get; set; } = string.Empty;

    /// <summary>
    /// 最大迭代次数
    /// </summary>
    public int MaxIterations { get; set; } = 100;

    /// <summary>
    /// 执行器定义列表
    /// </summary>
    public List<ExecutorDefinition> Executors { get; set; } = new();

    /// <summary>
    /// 边组定义列表
    /// </summary>
    public List<EdgeGroupDefinition> EdgeGroups { get; set; } = new();

    /// <summary>
    /// 输入规范
    /// </summary>
    public InputSpecification InputSpec { get; set; } = new();

    /// <summary>
    /// 输出规范
    /// </summary>
    public OutputSpecification OutputSpec { get; set; } = new();

    /// <summary>
    /// 变量定义
    /// </summary>
    public List<VariableDefinition> Variables { get; set; } = new();

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

#region Executor Definitions

/// <summary>
/// 执行器基础定义 - 对齐 Agent Framework Executor
/// </summary>
public class ExecutorDefinition
{
    /// <summary>
    /// 执行器ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 执行器类型
    /// </summary>
    public ExecutorType Type { get; set; }

    /// <summary>
    /// 执行器名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 节点位置 (用于可视化设计器)
    /// </summary>
    public NodePosition Position { get; set; } = new();

    /// <summary>
    /// 执行器配置
    /// </summary>
    public Dictionary<string, object> Config { get; set; } = new();
}

/// <summary>
/// 执行器类型 - 对齐 Agent Framework 和 AdaptiveDialog
/// </summary>
public enum ExecutorType
{
    #region 智能体类型 (Agent Executors)

    /// <summary>
    /// 聊天智能体
    /// </summary>
    ChatAgent,

    /// <summary>
    /// 函数调用智能体
    /// </summary>
    FunctionAgent,

    /// <summary>
    /// 工具调用智能体
    /// </summary>
    ToolAgent,

    /// <summary>
    /// Magentic编排器
    /// </summary>
    MagenticOrchestrator,

    /// <summary>
    /// Azure AI 智能体
    /// </summary>
    AzureAgent,

    /// <summary>
    /// 调用 Azure AI Foundry 智能体 (Agent Framework 官方)
    /// </summary>
    InvokeAzureAgent,

    #endregion

    #region 流程控制类型 (Control Flow)

    /// <summary>
    /// 条件判断
    /// </summary>
    Condition,

    /// <summary>
    /// 条件组
    /// </summary>
    ConditionGroup,

    /// <summary>
    /// 循环
    /// </summary>
    Foreach,

    /// <summary>
    /// 跳转
    /// </summary>
    Goto,

    /// <summary>
    /// 跳转动作 (Agent Framework 官方)
    /// </summary>
    GotoAction,

    /// <summary>
    /// 中断循环
    /// </summary>
    BreakLoop,

    /// <summary>
    /// 继续循环
    /// </summary>
    ContinueLoop,

    /// <summary>
    /// 结束工作流
    /// </summary>
    EndWorkflow,

    /// <summary>
    /// 结束会话
    /// </summary>
    EndConversation,

    #endregion

    #region 状态管理类型 (State Management)

    /// <summary>
    /// 设置变量
    /// </summary>
    SetVariable,

    /// <summary>
    /// 设置文本变量 (Agent Framework 官方)
    /// </summary>
    SetTextVariable,

    /// <summary>
    /// 设置多个变量
    /// </summary>
    SetMultipleVariables,

    /// <summary>
    /// 解析值
    /// </summary>
    ParseValue,

    /// <summary>
    /// 编辑表格
    /// </summary>
    EditTable,

    /// <summary>
    /// 重置变量
    /// </summary>
    ResetVariable,

    /// <summary>
    /// 清除所有变量
    /// </summary>
    ClearAllVariables,

    #endregion

    #region 消息类型 (Messages)

    /// <summary>
    /// 发送活动/消息
    /// </summary>
    SendActivity,

    /// <summary>
    /// 添加对话消息
    /// </summary>
    AddConversationMessage,

    /// <summary>
    /// 检索对话消息
    /// </summary>
    RetrieveConversationMessages,

    #endregion

    #region 会话管理 (Conversation)

    /// <summary>
    /// 创建会话
    /// </summary>
    CreateConversation,

    /// <summary>
    /// 删除会话
    /// </summary>
    DeleteConversation,

    /// <summary>
    /// 复制会话消息
    /// </summary>
    CopyConversationMessages,

    #endregion

    #region 人工输入 (Human Input)

    /// <summary>
    /// 问题询问
    /// </summary>
    Question,

    /// <summary>
    /// 函数审批
    /// </summary>
    FunctionApproval,

    #endregion

    #region 工具执行 (Tool Execution)

    /// <summary>
    /// 函数执行器
    /// </summary>
    FunctionExecutor,

    /// <summary>
    /// MCP工具
    /// </summary>
    McpTool,

    /// <summary>
    /// OpenAPI工具
    /// </summary>
    OpenApiTool,

    /// <summary>
    /// 代码解释器
    /// </summary>
    CodeInterpreter,

    /// <summary>
    /// 文件搜索
    /// </summary>
    FileSearch,

    /// <summary>
    /// Web搜索
    /// </summary>
    WebSearch,

    #endregion

    #region 子工作流 (Nested Workflows)

    /// <summary>
    /// 子工作流
    /// </summary>
    SubWorkflow,

    /// <summary>
    /// 并行执行
    /// </summary>
    ParallelExecution,

    /// <summary>
    /// 扇出执行
    /// </summary>
    FanOut,

    /// <summary>
    /// 扇入合并
    /// </summary>
    FanIn,

    #endregion
}

#endregion

#region Agent Configuration

/// <summary>
/// 智能体执行器配置
/// </summary>
public class AgentExecutorConfig
{
    /// <summary>
    /// 智能体定义ID (引用已保存的智能体)
    /// </summary>
    public string? AgentDefinitionId { get; set; }

    /// <summary>
    /// 智能体名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 系统指令模板 (支持Scriban语法)
    /// </summary>
    public string InstructionsTemplate { get; set; } = string.Empty;

    /// <summary>
    /// 模型配置
    /// </summary>
    public ModelConfiguration ModelConfig { get; set; } = new();

    /// <summary>
    /// 工具列表
    /// </summary>
    public List<ToolReference> Tools { get; set; } = new();

    /// <summary>
    /// 工作台配置
    /// </summary>
    public List<WorkbenchConfig> Workbenches { get; set; } = new();

    /// <summary>
    /// 交接配置
    /// </summary>
    public List<HandoffConfig> Handoffs { get; set; } = new();

    /// <summary>
    /// 输入映射
    /// </summary>
    public List<VariableMapping> InputMappings { get; set; } = new();

    /// <summary>
    /// 输出映射
    /// </summary>
    public List<VariableMapping> OutputMappings { get; set; } = new();

    /// <summary>
    /// 是否在工具使用后反思
    /// </summary>
    public bool ReflectOnToolUse { get; set; } = false;

    /// <summary>
    /// 是否启用流式响应
    /// </summary>
    public bool EnableStreaming { get; set; } = true;
}

/// <summary>
/// 模型配置
/// </summary>
public class ModelConfiguration
{
    /// <summary>
    /// 模型提供商
    /// </summary>
    public ModelProvider Provider { get; set; } = ModelProvider.OpenAI;

    /// <summary>
    /// 模型名称
    /// </summary>
    public string Model { get; set; } = "gpt-4o";

    /// <summary>
    /// 温度参数
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// 最大令牌数
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// API端点 (用于Azure等)
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// 部署名称 (用于Azure)
    /// </summary>
    public string? DeploymentName { get; set; }
}

/// <summary>
/// 模型提供商
/// </summary>
public enum ModelProvider
{
    OpenAI,
    AzureOpenAI,
    Anthropic,
    GoogleAI,
    Ollama,
    Custom
}

/// <summary>
/// 工具引用
/// </summary>
public class ToolReference
{
    /// <summary>
    /// 工具类型
    /// </summary>
    public ToolType Type { get; set; }

    /// <summary>
    /// 工具名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 工具配置
    /// </summary>
    public Dictionary<string, object> Config { get; set; } = new();
}

/// <summary>
/// 工具类型
/// </summary>
public enum ToolType
{
    Function,
    Mcp,
    OpenApi,
    CodeInterpreter,
    FileSearch,
    WebSearch,
    Custom
}

/// <summary>
/// 工作台配置
/// </summary>
public class WorkbenchConfig
{
    /// <summary>
    /// 工作台类型
    /// </summary>
    public WorkbenchType Type { get; set; }

    /// <summary>
    /// 工具列表
    /// </summary>
    public List<ToolReference> Tools { get; set; } = new();

    /// <summary>
    /// MCP服务器参数 (仅用于MCP工作台)
    /// </summary>
    public McpServerParams? McpServerParams { get; set; }
}

/// <summary>
/// 工作台类型
/// </summary>
public enum WorkbenchType
{
    Static,
    Mcp
}

/// <summary>
/// MCP服务器参数
/// </summary>
public class McpServerParams
{
    /// <summary>
    /// 服务器类型
    /// </summary>
    public McpServerType Type { get; set; }

    /// <summary>
    /// 命令 (用于Stdio)
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// 参数 (用于Stdio)
    /// </summary>
    public List<string>? Args { get; set; }

    /// <summary>
    /// URL (用于SSE/HTTP)
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// 环境变量
    /// </summary>
    public Dictionary<string, string>? EnvVars { get; set; }
}

/// <summary>
/// MCP服务器类型
/// </summary>
public enum McpServerType
{
    Stdio,
    Sse,
    StreamableHttp
}

/// <summary>
/// 交接配置
/// </summary>
public class HandoffConfig
{
    /// <summary>
    /// 目标智能体ID
    /// </summary>
    public string TargetAgentId { get; set; } = string.Empty;

    /// <summary>
    /// 交接条件
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    /// 消息模板
    /// </summary>
    public string? MessageTemplate { get; set; }
}

#endregion

#region Control Flow Configuration

/// <summary>
/// 条件配置
/// </summary>
public class ConditionConfig
{
    /// <summary>
    /// 条件表达式 (PowerFx或Scriban语法)
    /// </summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>
    /// True分支目标
    /// </summary>
    public string? TrueBranchTarget { get; set; }

    /// <summary>
    /// False分支目标
    /// </summary>
    public string? FalseBranchTarget { get; set; }
}

/// <summary>
/// 条件组配置
/// </summary>
public class ConditionGroupConfig
{
    /// <summary>
    /// 条件项列表
    /// </summary>
    public List<ConditionItem> Conditions { get; set; } = new();

    /// <summary>
    /// 默认分支目标
    /// </summary>
    public string? DefaultTarget { get; set; }
}

/// <summary>
/// 条件项
/// </summary>
public class ConditionItem
{
    /// <summary>
    /// 条件表达式
    /// </summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>
    /// 目标执行器ID
    /// </summary>
    public string TargetExecutorId { get; set; } = string.Empty;
}

/// <summary>
/// 循环配置
/// </summary>
public class ForeachConfig
{
    /// <summary>
    /// 迭代集合表达式
    /// </summary>
    public string ItemsExpression { get; set; } = string.Empty;

    /// <summary>
    /// 当前项变量名
    /// </summary>
    public string ItemVariableName { get; set; } = "item";

    /// <summary>
    /// 索引变量名
    /// </summary>
    public string IndexVariableName { get; set; } = "index";

    /// <summary>
    /// 循环体开始执行器ID
    /// </summary>
    public string BodyStartExecutorId { get; set; } = string.Empty;
}

#endregion

#region Edge Definitions

/// <summary>
/// 边组定义 - 对齐 Agent Framework EdgeGroup
/// </summary>
public class EdgeGroupDefinition
{
    /// <summary>
    /// 边组ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 边组类型
    /// </summary>
    public EdgeGroupType Type { get; set; }

    /// <summary>
    /// 源执行器ID
    /// </summary>
    public string SourceExecutorId { get; set; } = string.Empty;

    /// <summary>
    /// 边列表
    /// </summary>
    public List<EdgeDefinition> Edges { get; set; } = new();
}

/// <summary>
/// 边组类型
/// </summary>
public enum EdgeGroupType
{
    /// <summary>
    /// 单边 - 无条件顺序执行
    /// </summary>
    Single,

    /// <summary>
    /// 扇出 - 并行执行多个目标
    /// </summary>
    FanOut,

    /// <summary>
    /// 扇入 - 等待多个源完成
    /// </summary>
    FanIn,

    /// <summary>
    /// 分支选择 - 基于条件选择一个目标
    /// </summary>
    SwitchCase
}

/// <summary>
/// 边定义
/// </summary>
public class EdgeDefinition
{
    /// <summary>
    /// 边ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 目标执行器ID
    /// </summary>
    public string TargetExecutorId { get; set; } = string.Empty;

    /// <summary>
    /// 条件表达式 (可选)
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    /// 边标签
    /// </summary>
    public string? Label { get; set; }
}

#endregion

#region Input/Output Specifications

/// <summary>
/// 输入规范
/// </summary>
public class InputSpecification
{
    /// <summary>
    /// 输入类型名称
    /// </summary>
    public string TypeName { get; set; } = "WorkflowInput";

    /// <summary>
    /// JSON Schema
    /// </summary>
    public JsonSchemaDefinition Schema { get; set; } = new();
}

/// <summary>
/// 输出规范
/// </summary>
public class OutputSpecification
{
    /// <summary>
    /// 输出类型名称
    /// </summary>
    public string TypeName { get; set; } = "WorkflowOutput";

    /// <summary>
    /// JSON Schema
    /// </summary>
    public JsonSchemaDefinition Schema { get; set; } = new();
}

/// <summary>
/// JSON Schema 定义
/// </summary>
public class JsonSchemaDefinition
{
    /// <summary>
    /// 类型
    /// </summary>
    public string Type { get; set; } = "object";

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 属性定义
    /// </summary>
    public Dictionary<string, PropertySchema> Properties { get; set; } = new();

    /// <summary>
    /// 必需属性列表
    /// </summary>
    public List<string> Required { get; set; } = new();
}

/// <summary>
/// 属性 Schema
/// </summary>
public class PropertySchema
{
    /// <summary>
    /// 类型
    /// </summary>
    public string Type { get; set; } = "string";

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 默认值
    /// </summary>
    public object? Default { get; set; }

    /// <summary>
    /// 枚举值
    /// </summary>
    public List<string>? Enum { get; set; }

    /// <summary>
    /// 格式
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// 数组项 Schema
    /// </summary>
    public PropertySchema? Items { get; set; }

    /// <summary>
    /// 嵌套对象属性
    /// </summary>
    public Dictionary<string, PropertySchema>? NestedProperties { get; set; }
}

#endregion

#region Variable Definitions

/// <summary>
/// 变量定义
/// </summary>
public class VariableDefinition
{
    /// <summary>
    /// 变量名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 变量类型
    /// </summary>
    public string Type { get; set; } = "string";

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 默认值
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// 作用域
    /// </summary>
    public VariableScope Scope { get; set; } = VariableScope.Workflow;
}

/// <summary>
/// 变量作用域
/// </summary>
public enum VariableScope
{
    /// <summary>
    /// 工作流级别
    /// </summary>
    Workflow,

    /// <summary>
    /// 对话级别
    /// </summary>
    Conversation,

    /// <summary>
    /// 全局级别
    /// </summary>
    Global
}

/// <summary>
/// 变量映射
/// </summary>
public class VariableMapping
{
    /// <summary>
    /// 源表达式
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 目标变量名
    /// </summary>
    public string Target { get; set; } = string.Empty;
}

#endregion
